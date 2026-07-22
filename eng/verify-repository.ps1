[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$trackedFiles = @(& git -C $repositoryRoot ls-files)
if ($LASTEXITCODE -ne 0) {
    throw 'No se pudo obtener el inventario de archivos versionados.'
}

$forbiddenFiles = @(
    $trackedFiles | Where-Object {
        $path = $_ -replace '\\', '/'
        $path -match '(^|/)(bin|obj|packages|publish|\.vs)(/|$)' -or
        $path -match '(?i)\.(pfx|p12|key|pem|suo|user|deploy|bak)$'
    }
)

if ($forbiddenFiles.Count -gt 0) {
    $forbiddenFiles | ForEach-Object { Write-Error "Artefacto prohibido en Git: $_" }
    throw 'El repositorio contiene artefactos locales, certificados o salidas de build.'
}

$googlePrefix = 'AI' + 'za'
$googlePattern = $googlePrefix + '[0-9A-Za-z_-]{35}'
$serverLabels = '(data' + ' source|server)'
$passwordLabels = '(pass' + 'word|pwd)'
$connectionPattern = $serverLabels + '[^;\"]{0,300}' + $passwordLabels + '[[:space:]]*='
$privateKeyPattern = '-----BEGIN ' + '(RSA |EC |OPENSSH )?' + 'PRIVATE KEY-----'
$githubPrefix = 'gh' + '[pousr]_'
$githubPattern = $githubPrefix + '[0-9A-Za-z]{20,}'
$patterns = @($googlePattern, $connectionPattern, $privateKeyPattern, $githubPattern)
$secretFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

foreach ($pattern in $patterns) {
    $matches = @(& git -C $repositoryRoot grep -I -l -i -E -e $pattern -- . 2>$null)
    $grepExitCode = $LASTEXITCODE
    if ($grepExitCode -gt 1) {
        throw "git grep fallo al evaluar una regla de secretos con codigo $grepExitCode."
    }
    foreach ($match in $matches) {
        [void]$secretFiles.Add($match)
    }
}

if ($secretFiles.Count -gt 0) {
    $secretFiles | Sort-Object | ForEach-Object { Write-Error "Posible secreto versionado en: $_" }
    throw 'La verificacion de secretos no fue superada. El contenido sensible no se imprime.'
}

Write-Host "Repositorio verificado: $($trackedFiles.Count) archivos versionados; sin artefactos prohibidos ni secretos de alta confianza."
