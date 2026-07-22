[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$solutionPath = Join-Path $repositoryRoot 'SchedulerP360Insight.sln'
$exceptionsPath = Join-Path $repositoryRoot 'eng\dependency-exceptions.json'

function Invoke-PackageReport {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Mode
    )

    $output = @(
        & dotnet list $solutionPath package `
            "--$Mode" `
            --include-transitive `
            --format json
    )
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet list package --$Mode termino con codigo $LASTEXITCODE."
    }

    return (($output -join [Environment]::NewLine) | ConvertFrom-Json)
}

function Get-PackageEntries {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Report
    )

    foreach ($project in @($Report.projects)) {
        $frameworksProperty = $project.PSObject.Properties['frameworks']
        if ($null -eq $frameworksProperty) {
            continue
        }

        foreach ($framework in @($frameworksProperty.Value)) {
            foreach ($collectionName in @('topLevelPackages', 'transitivePackages')) {
                $collection = $framework.PSObject.Properties[$collectionName]
                if ($null -eq $collection) {
                    continue
                }

                foreach ($package in @($collection.Value)) {
                    [PSCustomObject]@{
                        Project = $project.path
                        Framework = $framework.framework
                        Package = $package
                    }
                }
            }
        }
    }
}

$forbiddenLog4NetConfiguration = @(
    & git -C $repositoryRoot grep -I -l -E `
        -e '(<log4net|log4net[.]Layout[.]XmlLayout|XmlLayoutSchemaLog4J)' `
        -- SchedulerP360Insight 2>$null
)
$grepExitCode = $LASTEXITCODE
if ($grepExitCode -gt 1) {
    throw "git grep fallo al verificar la configuracion log4net con codigo $grepExitCode."
}
if ($forbiddenLog4NetConfiguration.Count -gt 0) {
    $forbiddenLog4NetConfiguration | ForEach-Object {
        Write-Error "Configuracion log4net no permitida durante la excepcion: $_"
    }
    throw 'La excepcion de log4net prohibe configurar XmlLayout o una seccion log4net en el host.'
}

$policy = Get-Content -LiteralPath $exceptionsPath -Raw | ConvertFrom-Json
$exceptions = @($policy.exceptions)
$today = [DateTime]::UtcNow.Date
foreach ($exception in $exceptions) {
    $expiration = [DateTime]::ParseExact(
        $exception.expiresOn,
        'yyyy-MM-dd',
        [Globalization.CultureInfo]::InvariantCulture)
    if ($today -gt $expiration.Date) {
        throw "La excepcion para $($exception.packageId) vencio el $($exception.expiresOn)."
    }
}

$vulnerabilityReport = Invoke-PackageReport -Mode 'vulnerable'
$vulnerabilities = foreach ($entry in Get-PackageEntries $vulnerabilityReport) {
    $property = $entry.Package.PSObject.Properties['vulnerabilities']
    if ($null -eq $property) {
        continue
    }

    foreach ($vulnerability in @($property.Value)) {
        [PSCustomObject]@{
            PackageId = $entry.Package.id
            Version = $entry.Package.resolvedVersion
            Severity = $vulnerability.severity
            AdvisoryUrl = $vulnerability.advisoryurl
        }
    }
}
$vulnerabilities = @(
    $vulnerabilities |
        Sort-Object PackageId, Version, Severity, AdvisoryUrl -Unique
)

foreach ($finding in $vulnerabilities) {
    if ($finding.Severity -in @('High', 'Critical')) {
        throw "Vulnerabilidad $($finding.Severity) no permitida: $($finding.PackageId) $($finding.Version), $($finding.AdvisoryUrl)."
    }

    $matchingException = @(
        $exceptions | Where-Object {
            $_.packageId -eq $finding.PackageId -and
            $_.version -eq $finding.Version -and
            $_.severity -eq $finding.Severity -and
            $_.advisoryUrl -eq $finding.AdvisoryUrl
        }
    )
    if ($matchingException.Count -ne 1) {
        throw "Vulnerabilidad sin excepcion exacta: $($finding.PackageId) $($finding.Version), $($finding.AdvisoryUrl)."
    }
}

foreach ($exception in $exceptions) {
    $matchingFinding = @(
        $vulnerabilities | Where-Object {
            $_.PackageId -eq $exception.packageId -and
            $_.Version -eq $exception.version -and
            $_.Severity -eq $exception.severity -and
            $_.AdvisoryUrl -eq $exception.advisoryUrl
        }
    )
    if ($matchingFinding.Count -eq 0) {
        throw "La excepcion de $($exception.packageId) ya no corresponde a un hallazgo y debe retirarse."
    }
}

$deprecatedReport = Invoke-PackageReport -Mode 'deprecated'
$deprecatedPackages = @(Get-PackageEntries $deprecatedReport)
if ($deprecatedPackages.Count -gt 0) {
    $deprecatedPackages | ForEach-Object {
        Write-Error "Paquete obsoleto: $($_.Package.id) $($_.Package.resolvedVersion)"
    }
    throw 'Se detectaron paquetes NuGet marcados como obsoletos.'
}

Write-Host "Politica de dependencias respetada: 0 vulnerabilidades altas/criticas, $($vulnerabilities.Count) moderada excepcionada y 0 paquetes obsoletos."
