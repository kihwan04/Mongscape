param(
    [Parameter(Mandatory=$true)][string]$ServerUrl,
    [Parameter(Mandatory=$true)][string]$GameDirectory
)
$ErrorActionPreference = 'Stop'
$serverUri = $null
if (-not [Uri]::TryCreate($ServerUrl, [UriKind]::Absolute, [ref]$serverUri) -or
    $serverUri.AbsolutePath -ne '/' -or $serverUri.UserInfo -or $serverUri.Query -or $serverUri.Fragment -or
    ($serverUri.Scheme -ne 'https' -and -not ($serverUri.Scheme -eq 'http' -and $serverUri.IsLoopback))) {
    throw 'Use an HTTPS origin, or loopback HTTP for local Docker.'
}
$gameRoot = (Resolve-Path -LiteralPath $GameDirectory).Path
if (-not (Test-Path -LiteralPath (Join-Path $gameRoot 'Mongscape.exe')) -and
    -not (Test-Path -LiteralPath (Join-Path $gameRoot 'Assets') -PathType Container)) {
    throw 'Choose the folder containing Mongscape.exe, or the Unity project folder.'
}
$configPath = Join-Path $gameRoot 'identity-server.json'
@{url=$serverUri.GetLeftPart([UriPartial]::Authority)} | ConvertTo-Json |
    Set-Content -LiteralPath $configPath -Encoding UTF8
Write-Host ('Client configured: '+$configPath)
