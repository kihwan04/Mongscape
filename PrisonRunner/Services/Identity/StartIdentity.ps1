param([string]$Python = "", [string]$ConfigPath = "")
$ErrorActionPreference = 'Stop'
# A remote client uses the central Docker service; it must not start a second local DB.
$serverUrl = $env:MONGSCAPE_IDENTITY_URL
if (-not $serverUrl) {
    $configCandidates = if ($ConfigPath) { @($ConfigPath) } else { @(
        (Join-Path $PSScriptRoot '../../Builds/Mongscape/Windows/identity-server.json'),
        (Join-Path $PSScriptRoot '../../Game/identity-server.json')
    ) }
    foreach ($configPath in $configCandidates) {
        if (Test-Path -LiteralPath $configPath) {
            $serverUrl = (Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json).url
            if (-not $serverUrl) { throw 'Missing url in identity-server.json.' }
            break
        }
    }
}
if ($serverUrl) {
    $serverUri = $null
    if (-not [Uri]::TryCreate($serverUrl, [UriKind]::Absolute, [ref]$serverUri) -or
        $serverUri.AbsolutePath -ne '/' -or $serverUri.UserInfo -or $serverUri.Query -or $serverUri.Fragment -or
        ($serverUri.Scheme -ne 'https' -and -not ($serverUri.Scheme -eq 'http' -and $serverUri.IsLoopback))) {
        throw 'Use an HTTPS origin, or loopback HTTP for local Docker.'
    }
    Write-Host 'Using the configured identity server. Local Python startup skipped.'
    exit 0
}
if (-not $Python) {
    $portablePython = Join-Path $PSScriptRoot 'python/python.exe'
    $bundledPython = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
    if (Test-Path -LiteralPath $portablePython) { $Python = $portablePython } elseif (Test-Path -LiteralPath $bundledPython) { $Python = $bundledPython } else { $Python = 'python' }
}
try {
    $health = Invoke-RestMethod 'http://127.0.0.1:8765/health' -TimeoutSec 1
    if ($health.service -eq 'Mongscape Local Identity') { exit 0 }
    throw 'Port 8765 is occupied by another service.'
} catch [System.Net.WebException] { }
# The server stores profiles only in PostgreSQL; the usual setup is the Docker compose service (README_DOCKER.md).
if (-not ($env:IDENTITY_DATABASE_URL -or $env:PGHOST -or $env:PGSERVICE)) {
    Write-Warning ('Local face server not started: it needs PostgreSQL. Run the Docker compose service (README_DOCKER.md), ' +
        'or set IDENTITY_DATABASE_URL (or PGHOST/PGPORT/PGDATABASE/PGUSER with PGPASSWORD_FILE) and run this again.')
    exit 1
}
Start-Process -FilePath $Python -ArgumentList @(('"' + (Join-Path $PSScriptRoot 'server.py') + '"')) -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $PSScriptRoot 'identity.log') -RedirectStandardError (Join-Path $PSScriptRoot 'identity-error.log')
