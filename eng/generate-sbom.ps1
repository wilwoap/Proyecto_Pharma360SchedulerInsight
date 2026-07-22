[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$PackageVersion = '1.0.0',

    [string]$SbomToolPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$buildDropPath = Join-Path $repositoryRoot "SchedulerP360Insight\bin\x64\$Configuration"
$manifestRoot = Join-Path $repositoryRoot 'artifacts\sbom'
$generatedManifestDirectory = Join-Path $manifestRoot '_manifest'
$validationPath = Join-Path $repositoryRoot 'artifacts\sbom-validation.json'
$toolVersion = '4.1.5'

if (-not (Test-Path -LiteralPath $buildDropPath -PathType Container)) {
    throw "No existe el output $Configuration. Ejecute build.ps1 antes de generar el SBOM."
}

if (-not $SbomToolPath) {
    $command = Get-Command sbom-tool -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        $SbomToolPath = $command.Source
    }
    else {
        $toolDirectory = Join-Path ([IO.Path]::GetTempPath()) 'p360-sbom-tool'
        $candidate = Join-Path $toolDirectory 'sbom-tool.exe'
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            & dotnet tool install `
                --tool-path $toolDirectory `
                Microsoft.Sbom.DotNetTool `
                --version $toolVersion
            if ($LASTEXITCODE -ne 0) {
                throw "No se pudo instalar Microsoft.Sbom.DotNetTool $toolVersion."
            }
        }

        $SbomToolPath = $candidate
    }
}

$resolvedToolPath = (Resolve-Path -LiteralPath $SbomToolPath -ErrorAction Stop).Path
$resolvedManifestRoot = [IO.Path]::GetFullPath($manifestRoot)
$resolvedArtifactsRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
if (-not $resolvedManifestRoot.StartsWith(
    $resolvedArtifactsRoot + [IO.Path]::DirectorySeparatorChar,
    [StringComparison]::OrdinalIgnoreCase)) {
    throw 'La salida SBOM debe permanecer dentro de artifacts.'
}

if (-not (Test-Path -LiteralPath $manifestRoot -PathType Container)) {
    New-Item -ItemType Directory -Path $manifestRoot | Out-Null
}

& $resolvedToolPath generate `
    -b $buildDropPath `
    -bc $repositoryRoot `
    -m $manifestRoot `
    -pn 'SchedulerP360Insight' `
    -pv $PackageVersion `
    -ps 'Bisigma Inteligencia de Negocios' `
    -nsb 'https://github.com/wilwoap/Proyecto_Pharma360SchedulerInsight' `
    -nsu 'windows-x64' `
    -D true `
    -li true `
    -pm true `
    -V Warning
if ($LASTEXITCODE -ne 0) {
    throw "La generacion del SBOM termino con codigo $LASTEXITCODE."
}

& $resolvedToolPath validate `
    -b $buildDropPath `
    -m $generatedManifestDirectory `
    -o $validationPath `
    -n `
    -mi 'SPDX:2.2' `
    -V Warning
if ($LASTEXITCODE -ne 0) {
    throw "La validacion del SBOM termino con codigo $LASTEXITCODE."
}

$validation = Get-Content -LiteralPath $validationPath -Raw | ConvertFrom-Json
if ($validation.Result -ne 'Success' -or [int]$validation.ValidationErrors.Count -ne 0) {
    throw 'Microsoft SBOM Tool no valido correctamente el manifiesto generado.'
}

$manifestPath = Join-Path $manifestRoot '_manifest\spdx_2.2\manifest.spdx.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "La herramienta no produjo el manifiesto esperado: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifestHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
Write-Host "SBOM SPDX 2.2 validado: $($manifest.packages.Count) paquetes y $($manifest.files.Count) archivos."
Write-Host "Manifiesto: $manifestPath"
Write-Host "SHA256: $manifestHash"
