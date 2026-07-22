[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('Build', 'Rebuild', 'Clean')]
    [string]$Target = 'Rebuild',

    [switch]$SkipRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionPath = Join-Path $repositoryRoot 'SchedulerP360Insight.sln'
$baselinePath = Join-Path $repositoryRoot 'eng\warnings-baseline.json'
$artifactPath = Join-Path $repositoryRoot "SchedulerP360Insight\bin\x64\$Configuration\SchedulerP360Insight.exe"

function Resolve-MSBuild {
    $command = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $programFilesX86 = [Environment]::GetFolderPath('ProgramFilesX86')
    $vsWhere = Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vsWhere) {
        $candidate = & $vsWhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if ($candidate) {
            return $candidate
        }
    }

    throw 'No se encontro MSBuild. Instale Visual Studio Build Tools con el targeting pack de .NET Framework 4.8.'
}

function Invoke-MSBuildStep {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Executable,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [switch]$Capture
    )

    if ($Capture) {
        $output = & $Executable @Arguments 2>&1
        $exitCode = $LASTEXITCODE
        $output | ForEach-Object { Write-Host $_ }
        if ($exitCode -ne 0) {
            throw "MSBuild termino con codigo $exitCode."
        }
        return ($output | Out-String)
    }

    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild termino con codigo $LASTEXITCODE."
    }
}

function Assert-WarningBaseline {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BuildOutput
    )

    $baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
    $records = foreach ($line in ($BuildOutput -split '[\r\n]+')) {
        if ($line -match '(?i)\bwarning\s+([A-Z]{2,}[0-9]+)\s*:') {
            [PSCustomObject]@{
                Code = $Matches[1].ToUpperInvariant()
                Identity = $line.Trim()
            }
        }
    }

    $uniqueRecords = @($records | Sort-Object Code, Identity -Unique)
    $observedCodes = @($uniqueRecords | ForEach-Object Code | Sort-Object -Unique)
    $allowedCodes = @($baseline.allowedWarningCodes)
    $newCodes = @($observedCodes | Where-Object { $_ -notin $allowedCodes })

    if ($newCodes.Count -gt 0) {
        throw "Se detectaron codigos de advertencia fuera del baseline: $($newCodes -join ', ')."
    }

    foreach ($property in $baseline.warningMaximums.PSObject.Properties) {
        $code = $property.Name
        $maximum = [int]$property.Value
        $actual = @($uniqueRecords | Where-Object Code -eq $code).Count
        if ($actual -gt $maximum) {
            throw "La advertencia $code aumento de un maximo permitido de $maximum a $actual."
        }
    }

    $summary = if ($observedCodes.Count -eq 0) { 'ninguna' } else { $observedCodes -join ', ' }
    Write-Host "Baseline de advertencias respetado: $summary."
}

$msbuildPath = Resolve-MSBuild
Write-Host "MSBuild: $msbuildPath"
Write-Host "Configuracion: $Configuration | Plataforma: x64 | Objetivo: $Target"

$restoreOutput = ''
if (-not $SkipRestore -and $Target -ne 'Clean') {
    $restoreArguments = @(
        $solutionPath,
        '/t:Restore',
        '/m',
        '/nologo',
        '/verbosity:minimal',
        '/p:Platform=x64',
        '/p:RestorePackagesConfig=true'
    )
    $restoreOutput = Invoke-MSBuildStep -Executable $msbuildPath -Arguments $restoreArguments -Capture
}

$buildArguments = @(
    $solutionPath,
    "/t:$Target",
    '/m',
    '/nologo',
    '/verbosity:minimal',
    "/p:Configuration=$Configuration",
    '/p:Platform=x64',
    '/p:RestorePackagesConfig=true'
)
$buildOutput = Invoke-MSBuildStep -Executable $msbuildPath -Arguments $buildArguments -Capture

if ($Target -ne 'Clean') {
    $allBuildOutput = $restoreOutput + [Environment]::NewLine + $buildOutput
    Assert-WarningBaseline -BuildOutput $allBuildOutput
    if (-not (Test-Path -LiteralPath $artifactPath)) {
        throw "El build termino sin producir el ejecutable esperado: $artifactPath"
    }

    $artifactHash = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash
    Write-Host "Artefacto: $artifactPath"
    Write-Host "SHA256: $artifactHash"
}
