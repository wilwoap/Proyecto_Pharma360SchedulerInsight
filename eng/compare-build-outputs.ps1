[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BaselineDirectory,

    [Parameter(Mandatory = $true)]
    [string]$CandidateDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-OutputDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction Stop
    if (-not (Test-Path -LiteralPath $resolved.Path -PathType Container)) {
        throw "No es un directorio de salida valido: $Path"
    }

    return $resolved.Path.TrimEnd([IO.Path]::DirectorySeparatorChar)
}

function Get-RelativeFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    return @(
        Get-ChildItem -LiteralPath $Root -Recurse -File |
            ForEach-Object { $_.FullName.Substring($Root.Length + 1) } |
            Sort-Object
    )
}

function Get-ManifestDependencyIdentities {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    [xml]$manifest = Get-Content -LiteralPath $Path -Raw
    return @(
        $manifest.SelectNodes("//*[local-name()='dependency']/*[local-name()='dependentAssembly']/*[local-name()='assemblyIdentity']") |
            ForEach-Object {
                '{0}|{1}|{2}|{3}|{4}' -f $_.GetAttribute('name'), $_.GetAttribute('version'), $_.GetAttribute('publicKeyToken'), $_.GetAttribute('processorArchitecture'), $_.GetAttribute('culture')
            } |
            Sort-Object -Unique
    )
}

function Assert-SameSet {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Label,

        [Parameter(Mandatory = $true)]
        [object[]]$Baseline,

        [Parameter(Mandatory = $true)]
        [object[]]$Candidate
    )

    $differences = @(Compare-Object -ReferenceObject $Baseline -DifferenceObject $Candidate)
    if ($differences.Count -gt 0) {
        $details = $differences | ForEach-Object { "  $($_.SideIndicator) $($_.InputObject)" }
        throw "$Label no coincide:`n$($details -join [Environment]::NewLine)"
    }
}

$baselineRoot = Resolve-OutputDirectory $BaselineDirectory
$candidateRoot = Resolve-OutputDirectory $CandidateDirectory
if ([StringComparer]::OrdinalIgnoreCase.Equals($baselineRoot, $candidateRoot)) {
    throw 'La salida base y la candidata deben ser directorios distintos.'
}

$baselineFiles = Get-RelativeFiles $baselineRoot
$candidateFiles = Get-RelativeFiles $candidateRoot
Assert-SameSet -Label 'El inventario de archivos' -Baseline $baselineFiles -Candidate $candidateFiles

$changedAssemblies = foreach ($relativePath in $baselineFiles) {
    if ([IO.Path]::GetExtension($relativePath) -ine '.dll') {
        continue
    }

    $baselineHash = (Get-FileHash -LiteralPath (Join-Path $baselineRoot $relativePath) -Algorithm SHA256).Hash
    $candidateHash = (Get-FileHash -LiteralPath (Join-Path $candidateRoot $relativePath) -Algorithm SHA256).Hash
    if ($baselineHash -ne $candidateHash) {
        $relativePath
    }
}

if (@($changedAssemblies).Count -gt 0) {
    throw "Los assemblies DLL cambiaron:`n$($changedAssemblies -join [Environment]::NewLine)"
}

$applicationName = 'SchedulerP360Insight'
$baselineConfigPath = Join-Path $baselineRoot "$applicationName.exe.config"
$candidateConfigPath = Join-Path $candidateRoot "$applicationName.exe.config"
[xml]$baselineConfig = Get-Content -LiteralPath $baselineConfigPath -Raw
[xml]$candidateConfig = Get-Content -LiteralPath $candidateConfigPath -Raw
if ($baselineConfig.OuterXml -ne $candidateConfig.OuterXml) {
    throw 'La configuracion XML generada no es semanticamente equivalente.'
}

$baselineManifestPath = Join-Path $baselineRoot "$applicationName.exe.manifest"
$candidateManifestPath = Join-Path $candidateRoot "$applicationName.exe.manifest"
$baselineDependencies = Get-ManifestDependencyIdentities $baselineManifestPath
$candidateDependencies = Get-ManifestDependencyIdentities $candidateManifestPath
Assert-SameSet -Label 'Las dependencias del manifiesto' -Baseline $baselineDependencies -Candidate $candidateDependencies

Write-Host "Outputs compatibles: $($candidateFiles.Count) archivos, $(@($baselineFiles | Where-Object { [IO.Path]::GetExtension($_) -ieq '.dll' }).Count) DLL identicos, configuracion XML y manifiesto equivalentes."
