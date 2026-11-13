# Runs the identity server tests against a throwaway PostgreSQL 17 cluster on 127.0.0.1:55432.
# The cluster lives in a new temp folder and is stopped and deleted afterwards, even when tests fail.
param([string]$Python = '', [string]$PgBin = '', [int]$Port = 55432)
$ErrorActionPreference = 'Stop'
$PgVersion = '17.6-1'
function Assert-TestTempPath([string]$Target,[string]$Prefix) {
    $tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $full=[IO.Path]::GetFullPath($Target)
    if(-not $full.StartsWith($tempRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
        -not ([IO.Path]::GetFileName($full)).StartsWith($Prefix,[StringComparison]::Ordinal)) { throw 'Unexpected test cleanup path.' }
}
if (-not $Python) {
    $portablePython = Join-Path $PSScriptRoot 'python/python.exe'
    if (Test-Path -LiteralPath $portablePython) { $Python = $portablePython } else { $Python = 'python' }
}
if (-not $PgBin) {
    $pgRoot = if ($env:MONGSCAPE_PGSQL) { $env:MONGSCAPE_PGSQL } else { Join-Path $PSScriptRoot '.runtime/pgsql' }
    if (-not (Test-Path -LiteralPath (Join-Path $pgRoot 'bin/pg_ctl.exe'))) {
        if ($env:MONGSCAPE_PGSQL) { throw "MONGSCAPE_PGSQL has no bin\pg_ctl.exe: $pgRoot" }
        # Official EDB portable binaries; the archive's top folder is pgsql/.
        $download = Join-Path ([IO.Path]::GetTempPath()) ('mongscape-pgsql-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $download | Out-Null
        try {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            $ProgressPreference = 'SilentlyContinue'
            $zip = Join-Path $download 'pgsql.zip'
            Write-Host "Downloading PostgreSQL $PgVersion binaries..."
            Invoke-WebRequest -UseBasicParsing -OutFile $zip "https://get.enterprisedb.com/postgresql/postgresql-$PgVersion-windows-x64-binaries.zip"
            Expand-Archive -LiteralPath $zip -DestinationPath $download
            New-Item -ItemType Directory -Force -Path (Split-Path $pgRoot) | Out-Null
            Move-Item -LiteralPath (Join-Path $download 'pgsql') -Destination $pgRoot
        } finally { Assert-TestTempPath $download 'mongscape-pgsql-'; Remove-Item -LiteralPath $download -Recurse -Force -ErrorAction SilentlyContinue }
    }
    $PgBin = Join-Path $pgRoot 'bin'
}
$pgCtl = Join-Path $PgBin 'pg_ctl.exe'; $initDb = Join-Path $PgBin 'initdb.exe'; $psql = Join-Path $PgBin 'psql.exe'
$listener = $null
try { $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $Port); $listener.Start() }
catch { throw "Port $Port is in use; stop that server or pass -Port." } finally { if ($listener) { $listener.Stop() } }

function Invoke-Native([string]$File, [string[]]$Arguments, [string]$Log) {
    # File redirection plus WaitForExit: pg_ctl's background postgres must not hold a pipe we wait on.
    $process = Start-Process -FilePath $File -ArgumentList $Arguments -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput "$Log.out" -RedirectStandardError "$Log.err"
    $null = $process.Handle # Windows PowerShell reports no ExitCode unless the handle is opened early
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        Get-Content -LiteralPath "$Log.out", "$Log.err" -ErrorAction SilentlyContinue | Write-Host
        throw "$(Split-Path $File -Leaf) failed with exit code $($process.ExitCode)."
    }
}

$root = Join-Path ([IO.Path]::GetTempPath()) ('mongscape-pgtest-' + [guid]::NewGuid().ToString('N'))
$data = Join-Path $root 'data'
New-Item -ItemType Directory -Path $root | Out-Null
$adminPassword = [guid]::NewGuid().ToString('N'); $appPassword = [guid]::NewGuid().ToString('N')
$started = $false; $exitCode = 1
try {
    [IO.File]::WriteAllText((Join-Path $root 'pwfile'), $adminPassword)
    Invoke-Native $initDb @('-D', "`"$data`"", '-U', 'postgres', '--encoding=UTF8', '--locale=C',
        '--auth-host=scram-sha-256', '--auth-local=scram-sha-256', '--pwfile', "`"$(Join-Path $root 'pwfile')`"") (Join-Path $root 'initdb')
    Invoke-Native $pgCtl @('-D', "`"$data`"", '-l', "`"$(Join-Path $root 'postgres.log')`"",
        '-o', "`"-h 127.0.0.1 -p $Port`"", '-w', '-t', '60', 'start') (Join-Path $root 'start')
    $started = $true
    # Same split as Docker: the tests connect as a non-superuser app role that owns only its database.
    $setup = Join-Path $root 'setup.sql'
    Set-Content -LiteralPath $setup -Encoding ASCII -Value @"
CREATE ROLE mongscape LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD '$appPassword';
CREATE DATABASE mongscape_test OWNER mongscape;
\connect mongscape_test
REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT USAGE, CREATE ON SCHEMA public TO mongscape;
"@
    $env:PGPASSWORD = $adminPassword
    try {
        Invoke-Native $psql @('-h', '127.0.0.1', '-p', "$Port", '-U', 'postgres', '-d', 'postgres', '-v', 'ON_ERROR_STOP=1', '-f', "`"$setup`"") (Join-Path $root 'psql')
    } finally { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }
    $env:MONGSCAPE_TEST_POSTGRES_URL = "postgresql://mongscape:$appPassword@127.0.0.1:$Port/mongscape_test"
    Push-Location $PSScriptRoot
    try {
        # unittest reports on stderr; when that is redirected, Stop would turn its first line into an error.
        $ErrorActionPreference = 'Continue'
        & $Python -m unittest -v test_identity test_http
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = 'Stop'
        Pop-Location; Remove-Item Env:MONGSCAPE_TEST_POSTGRES_URL -ErrorAction SilentlyContinue
    }
} finally {
    if ($started -or (Test-Path -LiteralPath (Join-Path $data 'postmaster.pid'))) {
        try { Invoke-Native $pgCtl @('-D', "`"$data`"", '-m', 'fast', '-w', 'stop') (Join-Path $root 'stop') }
        catch { Write-Warning "PostgreSQL stop failed: $_" }
    }
    Assert-TestTempPath $root 'mongscape-pgtest-'
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $root) { Write-Warning "Could not delete $root" }
}
if ($exitCode -ne 0) { Write-Host "Identity tests failed (exit $exitCode)."; exit $exitCode }
Write-Host 'Identity tests passed on a throwaway PostgreSQL; the cluster is stopped and deleted.'
