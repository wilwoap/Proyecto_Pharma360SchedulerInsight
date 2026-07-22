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
$migrationPath = Join-Path $repositoryRoot (
    'database\migrations\20260722_PR10_DurableNotificationQueue.sql')
$localDb = Get-Command 'sqllocaldb.exe' -ErrorAction SilentlyContinue

if ($null -eq $localDb) {
    throw 'SQL Server LocalDB no esta instalado; no se probo la cola durable.'
}

if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "No existe el binario '$assemblyPath'. Ejecute build.ps1 primero."
}

if (-not (Test-Path -LiteralPath $migrationPath)) {
    throw "No existe la migracion '$migrationPath'."
}

function Assert-Contract {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Condition,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not $Condition) {
        throw "Contrato de cola durable incumplido: $Message"
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

function Invoke-NonQuery {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConnectionString,

        [Parameter(Mandatory = $true)]
        [string]$Sql
    )

    $connection = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandTimeout = 30
        $command.CommandText = $Sql
        return $command.ExecuteNonQuery()
    }
    finally {
        $connection.Dispose()
    }
}

function Invoke-Scalar {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConnectionString,

        [Parameter(Mandatory = $true)]
        [string]$Sql
    )

    $connection = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandTimeout = 30
        $command.CommandText = $Sql
        return $command.ExecuteScalar()
    }
    finally {
        $connection.Dispose()
    }
}

function Invoke-Migration {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConnectionString,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $script = Get-Content -Raw -LiteralPath $Path
    $batches = [regex]::Split(
        $script,
        '(?im)^\s*GO\s*(?:\r?\n|$)')
    $connection = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
    try {
        $connection.Open()
        foreach ($batch in $batches) {
            if ([string]::IsNullOrWhiteSpace($batch)) {
                continue
            }

            $command = $connection.CreateCommand()
            $command.CommandTimeout = 30
            $command.CommandText = $batch
            [void]$command.ExecuteNonQuery()
        }
    }
    finally {
        $connection.Dispose()
    }
}

$runId = $PID.ToString() + '_' +
    [Guid]::NewGuid().ToString('N').Substring(0, 8)
$instanceName = 'P360PR10_' + $runId
$databaseName = 'P360PR10Contract_' + $runId
if (-not $instanceName.StartsWith('P360PR10_', [StringComparison]::Ordinal)) {
    throw 'El nombre calculado para LocalDB no pertenece al espacio de PR-10.'
}

$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$databaseFileStem = Join-Path $temporaryRoot $databaseName
$databaseDataPath = [IO.Path]::GetFullPath($databaseFileStem + '.mdf')
$databaseLogPath = [IO.Path]::GetFullPath($databaseFileStem + '_log.ldf')
$expectedPrefix = $temporaryRoot.TrimEnd('\') + '\P360PR10Contract_'
foreach ($path in @($databaseDataPath, $databaseLogPath)) {
    if (-not $path.StartsWith(
        $expectedPrefix,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "La ruta calculada no pertenece al espacio temporal PR-10: $path"
    }
}

$instanceCreated = $false
$databaseCreated = $false
$masterConnectionString = $null
try {
    & $localDb.Source create $instanceName -s | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "No se pudo crear la instancia LocalDB '$instanceName'."
    }

    $instanceCreated = $true
    $masterConnectionString =
        "Data Source=(localdb)\$instanceName;" +
        'Initial Catalog=master;Integrated Security=True;' +
        'Connect Timeout=5;Max Pool Size=10;' +
        'Application Name=P360-PR10-Contract;'

    $safeDataPath = $databaseDataPath.Replace("'", "''")
    $safeLogPath = $databaseLogPath.Replace("'", "''")
    $createDatabaseSql =
        "CREATE DATABASE [$databaseName] " +
        "ON PRIMARY (NAME=N'$databaseName', FILENAME=N'$safeDataPath') " +
        "LOG ON (NAME=N'${databaseName}_log', FILENAME=N'$safeLogPath');"
    [void](Invoke-NonQuery `
        -ConnectionString $masterConnectionString `
        -Sql $createDatabaseSql)
    $databaseCreated = $true
    [void](Invoke-NonQuery `
        -ConnectionString $masterConnectionString `
        -Sql (
            "ALTER DATABASE [$databaseName] " +
            'SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;'))

    $connectionString =
        "Data Source=(localdb)\$instanceName;" +
        "Initial Catalog=$databaseName;Integrated Security=True;" +
        'Connect Timeout=5;Max Pool Size=10;' +
        'Application Name=P360-PR10-Contract;'

    [void](Invoke-NonQuery -ConnectionString $connectionString -Sql @'
CREATE SCHEMA P360Insight;
'@)

    [void](Invoke-NonQuery -ConnectionString $connectionString -Sql @'
CREATE TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
(
    cola_notificacion_id int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    report_id int NOT NULL,
    report_uid varchar(32) NOT NULL,
    report_name nvarchar(128) NOT NULL,
    report_insight nvarchar(128) NOT NULL,
    report_type varchar(32) NOT NULL,
    referencia_evento varchar(64) NOT NULL,
    referencia_evento_id varchar(64) NOT NULL,
    cod_colab int NOT NULL,
    nombre_colab nvarchar(128) NOT NULL,
    email_colab varchar(254) NOT NULL,
    cod_sup int NOT NULL,
    nombre_sup nvarchar(128) NOT NULL,
    email_sup varchar(254) NOT NULL,
    enviado bit NOT NULL CONSTRAINT DF_PR10_Test_Enviado DEFAULT (0),
    intentos_envio int NOT NULL CONSTRAINT DF_PR10_Test_Intentos DEFAULT (0),
    fecha_envio datetime NULL
);

EXEC(N'
CREATE VIEW P360Insight.V_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
AS
SELECT
    cola_notificacion_id,
    report_id,
    report_uid,
    report_name,
    report_insight,
    report_type,
    referencia_evento,
    referencia_evento_id,
    cod_colab,
    nombre_colab,
    email_colab,
    cod_sup,
    nombre_sup,
    email_sup
FROM P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
WHERE enviado = 0;
');
'@)

    Invoke-Migration `
        -ConnectionString $connectionString `
        -Path $migrationPath
    Invoke-Migration `
        -ConnectionString $connectionString `
        -Path $migrationPath

    [void](Invoke-NonQuery -ConnectionString $connectionString -Sql @'
INSERT INTO P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
(
    report_id, report_uid, report_name, report_insight, report_type,
    referencia_evento, referencia_evento_id, cod_colab, nombre_colab,
    email_colab, cod_sup, nombre_sup, email_sup
)
VALUES
    (42, 'R42', 'Report 42 A', 'Insight', 'html', 'event', '42-A', 1,
        'Recipient', 'recipient@example.test', 11, 'Supervisor',
        'supervisor@example.test'),
    (42, 'R42', 'Report 42 B', 'Insight', 'html', 'event', '42-B', 2,
        'Recipient', 'recipient@example.test', 12, 'Supervisor',
        'supervisor@example.test'),
    (43, 'R43', 'Report 43', 'Insight', 'html', 'event', '43-A', 3,
        'Recipient', 'recipient@example.test', 13, 'Supervisor',
        'supervisor@example.test'),
    (44, 'R44', 'Report 44', 'Insight', 'html', 'event', '44-A', 4,
        'Recipient', 'recipient@example.test', 14, 'Supervisor',
        'supervisor@example.test'),
    (45, 'R45', 'Report 45', 'Insight', 'html', 'event', '45-A', 5,
        'Recipient', 'recipient@example.test', 15, 'Supervisor',
        'supervisor@example.test'),
    (46, 'R46', 'Report 46', 'Insight', 'html', 'event', '46-A', 6,
        'Recipient', 'recipient@example.test', 16, 'Supervisor',
        'supervisor@example.test');
'@)

    [void][Reflection.Assembly]::LoadFrom($assemblyPath)
    $options =
        [SchedulerP360Insight.Configuration.SchedulerOptions]::new(
            $connectionString,
            $null,
            'SELECT 1',
            'SELECT 1 WHERE @ReportId > 0',
            [SchedulerP360Insight.Configuration.ParameterProviderMode]::Batch,
            $null,
            $null,
            [TimeSpan]::FromSeconds(5),
            [TimeSpan]::FromSeconds(10),
            [TimeZoneInfo]::Utc,
            [SchedulerP360Insight.Configuration.QuartzMisfirePolicy]::FireOnceNow,
            $true,
            10,
            [SchedulerP360Insight.Configuration.NotificationQueueMode]::Durable,
            1,
            [TimeSpan]::FromSeconds(30),
            2,
            [TimeSpan]::FromSeconds(2),
            [TimeSpan]::FromSeconds(10))

    $repositoryA =
        [SchedulerP360Insight.Data.SqlNotificationQueueRepository]::new($options)
    $repositoryB =
        [SchedulerP360Insight.Data.SqlNotificationQueueRepository]::new($options)
    $none = [Threading.CancellationToken]::None

    [void]$repositoryA.VerifyDurableSchemaAsync(
        $none).GetAwaiter().GetResult()

    $claimTaskA = $repositoryA.ClaimPendingAsync(42, 'worker-a', $none)
    $claimTaskB = $repositoryB.ClaimPendingAsync(42, 'worker-b', $none)
    $claimA = $claimTaskA.GetAwaiter().GetResult()
    $claimB = $claimTaskB.GetAwaiter().GetResult()
    Assert-Contract `
        (($claimA.Count + $claimB.Count) -eq 2) `
        'dos workers no reclamaron exactamente dos filas.'
    Assert-Contract `
        ($claimA[0].ColaNotificacionId -ne $claimB[0].ColaNotificacionId) `
        'dos workers reclamaron la misma fila.'
    Assert-Contract `
        ($claimA[0].LeaseToken -ne $claimB[0].LeaseToken) `
        'los claims no tienen tokens independientes.'
    Assert-Contract `
        ($repositoryA.MarkSentAsync($claimA[0], $none).GetAwaiter().GetResult()) `
        'el primer claim no se pudo completar.'
    Assert-Contract `
        ($repositoryB.MarkSentAsync($claimB[0], $none).GetAwaiter().GetResult()) `
        'el segundo claim no se pudo completar.'

    $retryClaim = $repositoryA.ClaimPendingAsync(
        43, 'worker-retry', $none).GetAwaiter().GetResult()[0]
    $stableKey = $retryClaim.NotificationKey
    $transient =
        [SchedulerP360Insight.Services.NotificationFailureDecision]::new(
            $false,
            'smtp.transient')
    $retryDisposition = $repositoryA.RecordFailureAsync(
        $retryClaim,
        $transient,
        $none).GetAwaiter().GetResult()
    Assert-Contract `
        ($retryDisposition -eq
            [SchedulerP360Insight.Services.NotificationFailureDisposition]::RetryScheduled) `
        'el primer fallo transitorio no programo reintento.'
    $notDue = $repositoryA.ClaimPendingAsync(
        43, 'worker-too-early', $none).GetAwaiter().GetResult()
    Assert-Contract ($notDue.Count -eq 0) 'se reclamo antes de next_attempt_utc.'

    [void](Invoke-NonQuery -ConnectionString $connectionString -Sql @'
UPDATE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
SET p360_next_attempt_utc = DATEADD(second, -1, SYSUTCDATETIME())
WHERE report_id = 43;
'@)

    $secondRetry = $repositoryA.ClaimPendingAsync(
        43, 'worker-retry-2', $none).GetAwaiter().GetResult()[0]
    Assert-Contract `
        ($secondRetry.NotificationKey -eq $stableKey) `
        'la idempotency key cambio entre intentos.'
    $deadDisposition = $repositoryA.RecordFailureAsync(
        $secondRetry,
        $transient,
        $none).GetAwaiter().GetResult()
    Assert-Contract `
        ($deadDisposition -eq
            [SchedulerP360Insight.Services.NotificationFailureDisposition]::DeadLetter) `
        'el maximo de intentos no llevo a dead-letter.'

    $deadCount = [int](Invoke-Scalar `
        -ConnectionString $connectionString `
        -Sql "SELECT COUNT(*) FROM P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES WHERE report_id=43 AND p360_delivery_status='dead_letter';")
    Assert-Contract ($deadCount -eq 1) 'dead-letter no quedo persistido.'

    $keyLiteral = $stableKey.ToString('D')
    [void](Invoke-NonQuery -ConnectionString $connectionString -Sql (
        "EXEC P360Insight.SP_RequeueDeadScheduledReportNotification " +
        "@notification_key='$keyLiteral', " +
        "@operator=N'contract-test', @reason=N'validated manual recovery';"))
    $manualRetry = $repositoryA.ClaimPendingAsync(
        43, 'worker-manual', $none).GetAwaiter().GetResult()[0]
    Assert-Contract `
        ($manualRetry.NotificationKey -eq $stableKey) `
        'el reproceso manual cambio la idempotency key.'
    Assert-Contract `
        ($manualRetry.AttemptCount -eq 1) `
        'el reproceso manual no reinicio el contador auditado.'
    Assert-Contract `
        ($repositoryA.MarkSentAsync($manualRetry, $none).GetAwaiter().GetResult()) `
        'el reproceso manual no se pudo completar.'

    $completeClaim = $repositoryA.ClaimPendingAsync(
        44, 'worker-complete', $none).GetAwaiter().GetResult()[0]
    $validToken = $completeClaim.LeaseToken
    $completeClaim.LeaseToken = [Guid]::NewGuid()
    Assert-Contract `
        (-not $repositoryA.MarkSentAsync(
            $completeClaim,
            $none).GetAwaiter().GetResult()) `
        'un token obsoleto completo una fila.'
    $completeClaim.LeaseToken = $validToken
    Assert-Contract `
        ($repositoryA.RenewLeaseAsync(
            $completeClaim,
            $none).GetAwaiter().GetResult()) `
        'el propietario no pudo renovar el lease vigente.'
    Assert-Contract `
        ($repositoryA.MarkSentAsync(
            $completeClaim,
            $none).GetAwaiter().GetResult()) `
        'el propietario no pudo completar la fila.'

    $expiryClaim = $repositoryA.ClaimPendingAsync(
        45, 'worker-expired', $none).GetAwaiter().GetResult()[0]
    $expiryKey = $expiryClaim.NotificationKey
    [void](Invoke-NonQuery -ConnectionString $connectionString -Sql @'
UPDATE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
SET p360_lease_until_utc = DATEADD(second, -1, SYSUTCDATETIME())
WHERE report_id = 45;
'@)
    Assert-Contract `
        (-not $repositoryA.RenewLeaseAsync(
            $expiryClaim,
            $none).GetAwaiter().GetResult()) `
        'un lease expirado fue revivido.'
    $reclaimed = $repositoryB.ClaimPendingAsync(
        45, 'worker-reclaimed', $none).GetAwaiter().GetResult()[0]
    Assert-Contract `
        ($reclaimed.NotificationKey -eq $expiryKey) `
        'el reclaim cambio la idempotency key.'
    Assert-Contract `
        ($reclaimed.LeaseToken -ne $expiryClaim.LeaseToken) `
        'el reclaim reutilizo el lease token.'
    Assert-Contract `
        (-not $repositoryA.MarkSentAsync(
            $expiryClaim,
            $none).GetAwaiter().GetResult()) `
        'el worker anterior completo despues del reclaim.'
    Assert-Contract `
        ($repositoryB.MarkSentAsync(
            $reclaimed,
            $none).GetAwaiter().GetResult()) `
        'el nuevo propietario no pudo completar.'

    $crashFirst = $repositoryA.ClaimPendingAsync(
        46, 'worker-crash-1', $none).GetAwaiter().GetResult()[0]
    [void](Invoke-NonQuery -ConnectionString $connectionString -Sql @'
UPDATE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
SET p360_lease_until_utc = DATEADD(second, -1, SYSUTCDATETIME())
WHERE report_id = 46;
'@)
    $crashSecond = $repositoryB.ClaimPendingAsync(
        46, 'worker-crash-2', $none).GetAwaiter().GetResult()[0]
    Assert-Contract `
        ($crashSecond.AttemptCount -eq 2) `
        'el reclaim tras caida no incremento el intento.'
    [void](Invoke-NonQuery -ConnectionString $connectionString -Sql @'
UPDATE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
SET p360_lease_until_utc = DATEADD(second, -1, SYSUTCDATETIME())
WHERE report_id = 46;
'@)
    $afterCrashLimit = $repositoryA.ClaimPendingAsync(
        46, 'worker-crash-3', $none).GetAwaiter().GetResult()
    Assert-Contract `
        ($afterCrashLimit.Count -eq 0) `
        'una fila agotada por caidas volvio a reclamarse.'
    $crashDeadCount = [int](Invoke-Scalar `
        -ConnectionString $connectionString `
        -Sql "SELECT COUNT(*) FROM P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES WHERE report_id=46 AND p360_delivery_status='dead_letter';")
    Assert-Contract `
        ($crashDeadCount -eq 1) `
        'una fila agotada por expiracion no llego a dead-letter.'

    $auditCount = [int](Invoke-Scalar `
        -ConnectionString $connectionString `
        -Sql 'SELECT COUNT(*) FROM P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES_AUDIT;')
    Assert-Contract ($auditCount -ge 17) 'faltan transiciones de auditoria.'

    $processingCount = [int](Invoke-Scalar `
        -ConnectionString $connectionString `
        -Sql "SELECT COUNT(*) FROM P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES WHERE p360_delivery_status='processing';")
    Assert-Contract ($processingCount -eq 0) 'quedaron leases activos tras la prueba.'

    [void](Invoke-NonQuery `
        -ConnectionString $connectionString `
        -Sql (
            'DROP PROCEDURE ' +
            'P360Insight.SP_RenewScheduledReportNotificationLease;'))
    $schemaError = $null
    try {
        [void]$repositoryA.VerifyDurableSchemaAsync(
            $none).GetAwaiter().GetResult()
    }
    catch {
        $schemaError = Find-Exception `
            -Exception $_.Exception `
            -Type ([SchedulerP360Insight.Data.DataAccessException])
    }

    Assert-Contract ($null -ne $schemaError) 'el esquema incompleto no fallo.'
    Assert-Contract `
        ($schemaError.FailureKind -eq
            [SchedulerP360Insight.Data.DataFailureKind]::Permanent) `
        'el preflight incompleto no fue clasificado permanente.'
    Invoke-Migration `
        -ConnectionString $connectionString `
        -Path $migrationPath
    [void]$repositoryA.VerifyDurableSchemaAsync(
        $none).GetAwaiter().GetResult()

    Write-Host 'Contrato de cola durable LocalDB aprobado:'
    Write-Host '  migracion expand-only reejecutable'
    Write-Host '  preflight de esquema fail-fast'
    Write-Host '  claim atomico sin doble propietario'
    Write-Host '  lease/token, renovacion, expiracion y reclaim'
    Write-Host '  backoff, maximo de intentos y dead-letter'
    Write-Host '  caidas repetidas agotan intentos sin dejar processing atascado'
    Write-Host '  reproceso manual auditado e idempotency key estable'
    Write-Host '  confirmacion condicionada al propietario'
}
finally {
    [System.Data.SqlClient.SqlConnection]::ClearAllPools()
    if ($instanceCreated) {
        if ($databaseCreated -and $null -ne $masterConnectionString) {
            try {
                [void](Invoke-NonQuery `
                    -ConnectionString $masterConnectionString `
                    -Sql (
                        "IF DB_ID(N'$databaseName') IS NOT NULL BEGIN " +
                        "ALTER DATABASE [$databaseName] SET SINGLE_USER " +
                        'WITH ROLLBACK IMMEDIATE; ' +
                        "DROP DATABASE [$databaseName]; END;"))
            }
            catch {
                Write-Warning (
                    'No se pudo ejecutar DROP DATABASE durante cleanup: ' +
                    $_.Exception.GetType().Name)
            }
        }

        [System.Data.SqlClient.SqlConnection]::ClearAllPools()
        & $localDb.Source stop $instanceName -k | Out-Host
        foreach ($path in @($databaseDataPath, $databaseLogPath)) {
            if ([IO.File]::Exists($path)) {
                [IO.File]::Delete($path)
            }

            if ([IO.File]::Exists($path)) {
                throw "Quedo un archivo LocalDB sintetico: $path"
            }
        }

        & $localDb.Source delete $instanceName | Out-Host
    }
}
