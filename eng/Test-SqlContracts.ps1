[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$assemblyPath = Join-Path $repositoryRoot (
    "SchedulerP360Insight\bin\x64\$Configuration\SchedulerP360Insight.exe")
$localDb = Get-Command 'sqllocaldb.exe' -ErrorAction SilentlyContinue

if ($null -eq $localDb) {
    throw 'SQL Server LocalDB no esta instalado; no se ejecuto el contrato SQL.'
}

if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "No existe el binario '$assemblyPath'. Ejecute build.ps1 primero."
}

function Assert-Contract {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Condition,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not $Condition) {
        throw "Contrato SQL incumplido: $Message"
    }
}

function Find-Exception {
    param(
        [Parameter(Mandatory = $true)]
        [Exception]$Exception,

        [Parameter(Mandatory = $true)]
        [Type]$Type
    )

    $current = $Exception
    while ($null -ne $current) {
        if ($Type.IsInstanceOfType($current)) {
            return $current
        }

        $current = $current.InnerException
    }

    return $null
}

function New-SchedulerOptions {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConnectionString,

        [Parameter(Mandatory = $true)]
        [string]$ReportsQuery,

        [Parameter(Mandatory = $true)]
        [string]$QueueQuery,

        [int]$CommandTimeoutSeconds = 2
    )

    return [SchedulerP360Insight.Configuration.SchedulerOptions]::new(
        $ConnectionString,
        $null,
        $ReportsQuery,
        $QueueQuery,
        [SchedulerP360Insight.Configuration.ParameterProviderMode]::Batch,
        $null,
        $null,
        [TimeSpan]::FromSeconds(5),
        [TimeSpan]::FromSeconds($CommandTimeoutSeconds))
}

$reportProjection = @"
SELECT
    CAST(42 AS bigint) AS report_id,
    'RVIS' AS report_uid,
    'Synthetic report' AS report_name,
    'Synthetic insight' AS report_insight,
    'report.pdf' AS report_filename,
    'html' AS report_type,
    'source' AS report_path_source,
    'output' AS report_path_output,
    '0 0/5 * * * ?' AS report_schedule,
    'Subject' AS report_subject_text,
    'HTMLBody_Plantilla_VM_01' AS report_body_resource_key,
    CAST(1 AS bit) AS report_send_mail,
    CAST(0 AS bit) AS report_send_mail_copy_supervisor
"@

$queueProjection = @"
SELECT
    CAST(11 AS int) AS cola_notificacion_id,
    CAST(42 AS int) AS report_id,
    'RVIS' AS report_uid,
    'Synthetic report' AS report_name,
    'Synthetic insight' AS report_insight,
    'html' AS report_type,
    'event' AS referencia_evento,
    'event-123' AS referencia_evento_id,
    CAST(700 AS int) AS cod_colab,
    'Synthetic recipient' AS nombre_colab,
    'recipient@example.test' AS email_colab,
    CAST(701 AS int) AS cod_sup,
    'Synthetic supervisor' AS nombre_sup,
    'supervisor@example.test' AS email_sup
WHERE @ReportId = 42
"@

$instanceName = 'P360PR08_' + $PID + '_' +
    [Guid]::NewGuid().ToString('N').Substring(0, 8)
if (-not $instanceName.StartsWith('P360PR08_', [StringComparison]::Ordinal)) {
    throw 'El nombre calculado para LocalDB no pertenece al espacio de PR-08.'
}

$instanceCreated = $false
try {
    & $localDb.Source create $instanceName -s | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "No se pudo crear la instancia LocalDB '$instanceName'."
    }

    $instanceCreated = $true
    $connectionString =
        "Data Source=(localdb)\$instanceName;" +
        'Initial Catalog=master;Integrated Security=True;' +
        'Connect Timeout=5;Max Pool Size=5;Application Name=P360-PR08-Contract;'

    [void][Reflection.Assembly]::LoadFrom($assemblyPath)

    $options = New-SchedulerOptions `
        -ConnectionString $connectionString `
        -ReportsQuery $reportProjection `
        -QueueQuery $queueProjection

    $reportSource =
        [SchedulerP360Insight.Scheduling.SqlReportScheduleSource]::new($options)
    $reports = $reportSource.LoadAsync(
        [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    Assert-Contract ($reports.Count -eq 1) 'schedule: cantidad inesperada.'
    Assert-Contract ($reports[0].ReportUID -eq 'RVIS') 'schedule: mapeo UID.'
    Assert-Contract ($reports[0].ReportSendMail) 'schedule: mapeo booleano.'

    $queue =
        [SchedulerP360Insight.Data.SqlNotificationQueueRepository]::new($options)
    $notifications = $queue.LoadPendingAsync(
        42,
        [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    Assert-Contract ($notifications.Count -eq 1) 'cola: cantidad inesperada.'
    Assert-Contract (
        $notifications[0].ColaNotificacionId -eq 11) 'cola: mapeo de ID.'

    $empty = $queue.LoadPendingAsync(
        999,
        [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    Assert-Contract ($empty.Count -eq 0) 'cola vacia debe ser resultado valido.'

    for ($iteration = 0; $iteration -lt 25; $iteration++) {
        $batch = $reportSource.LoadAsync(
            [Threading.CancellationToken]::None).GetAwaiter().GetResult()
        Assert-Contract ($batch.Count -eq 1) 'pooling: lectura incompleta.'
    }

    $missingOptions = New-SchedulerOptions `
        -ConnectionString $connectionString `
        -ReportsQuery 'SELECT * FROM dbo.P360_Object_Does_Not_Exist' `
        -QueueQuery $queueProjection
    $missingSource =
        [SchedulerP360Insight.Scheduling.SqlReportScheduleSource]::new(
            $missingOptions)
    $missingError = $null
    try {
        [void]$missingSource.LoadAsync(
            [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    }
    catch {
        $missingError = Find-Exception `
            -Exception $_.Exception `
            -Type ([SchedulerP360Insight.Data.DataAccessException])
    }

    Assert-Contract ($null -ne $missingError) 'objeto ausente sin error tipado.'
    Assert-Contract (
        $missingError.FailureKind -eq
            [SchedulerP360Insight.Data.DataFailureKind]::Permanent) `
        'objeto ausente no fue clasificado como permanente.'

    $timeoutOptions = New-SchedulerOptions `
        -ConnectionString $connectionString `
        -ReportsQuery ("WAITFOR DELAY '00:00:03';`n" + $reportProjection) `
        -QueueQuery $queueProjection `
        -CommandTimeoutSeconds 1
    $timeoutSource =
        [SchedulerP360Insight.Scheduling.SqlReportScheduleSource]::new(
            $timeoutOptions)
    $timeoutError = $null
    try {
        [void]$timeoutSource.LoadAsync(
            [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    }
    catch {
        $timeoutError = Find-Exception `
            -Exception $_.Exception `
            -Type ([SchedulerP360Insight.Data.DataAccessException])
    }

    Assert-Contract ($null -ne $timeoutError) 'timeout sin error tipado.'
    Assert-Contract (
        $timeoutError.FailureKind -eq
            [SchedulerP360Insight.Data.DataFailureKind]::Timeout) `
        'timeout no fue clasificado correctamente.'

    $cancelOptions = New-SchedulerOptions `
        -ConnectionString $connectionString `
        -ReportsQuery $reportProjection `
        -QueueQuery ("WAITFOR DELAY '00:00:05';`n" + $queueProjection) `
        -CommandTimeoutSeconds 30
    $cancelQueue =
        [SchedulerP360Insight.Data.SqlNotificationQueueRepository]::new(
            $cancelOptions)
    $cancelledError = $null
    $cancellation = [Threading.CancellationTokenSource]::new()
    try {
        $cancellation.CancelAfter(200)
        [void]$cancelQueue.LoadPendingAsync(
            42,
            $cancellation.Token).GetAwaiter().GetResult()
    }
    catch {
        $cancelledError = Find-Exception `
            -Exception $_.Exception `
            -Type ([OperationCanceledException])
    }
    finally {
        $cancellation.Dispose()
    }

    Assert-Contract (
        $null -ne $cancelledError) 'cancelacion no detuvo el comando SQL.'

    Write-Host 'Contrato SQL LocalDB aprobado:'
    Write-Host '  schedule/cola: mapeo y resultado vacio'
    Write-Host '  objeto ausente: permanente'
    Write-Host '  timeout: acotado y tipado'
    Write-Host '  cancelacion: propagada'
    Write-Host '  pooling/dispose: 25 ciclos con Max Pool Size=5'
}
finally {
    [System.Data.SqlClient.SqlConnection]::ClearAllPools()
    if ($instanceCreated) {
        & $localDb.Source stop $instanceName -k | Out-Host
        & $localDb.Source delete $instanceName | Out-Host
    }
}
