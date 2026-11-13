param([string]$Python = "")
$ErrorActionPreference = "Stop"
$identityRoot = $PSScriptRoot
if (-not $Python) {
    $portablePython = Join-Path $PSScriptRoot 'python/python.exe'
    $bundledPython = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
    if (Test-Path -LiteralPath $portablePython) { $Python = $portablePython } elseif (Test-Path -LiteralPath $bundledPython) { $Python = $bundledPython } else { $Python = 'python' }
}
& $Python "$identityRoot\setup_runtime.py"
if ($LASTEXITCODE -ne 0) { throw "Python runtime installation failed." }
& $Python "$identityRoot\setup_postgres_runtime.py"
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL driver installation failed.' }
& $Python "$identityRoot\setup_models.py"
if ($LASTEXITCODE -ne 0) { throw 'Face model download failed.' }
# Profiles live only in PostgreSQL; the usual setup is the Docker compose service (README_DOCKER.md).
if (-not ($env:IDENTITY_DATABASE_URL -or $env:PGHOST -or $env:PGSERVICE)) {
    throw ('Runtime and models are ready, but the database was not initialized: the face server needs PostgreSQL. ' +
        'Set IDENTITY_DATABASE_URL (or PGHOST/PGPORT/PGDATABASE/PGUSER with PGPASSWORD_FILE) and run SetupIdentity.ps1 again.')
}
& $Python "$identityRoot\server.py" --init-db
if ($LASTEXITCODE -ne 0) { throw 'Database initialization failed.' }
Write-Host 'Face server runtime and PostgreSQL schema are ready. Run StartIdentity.ps1.'
