param([Parameter(Mandatory=$true)][string]$Commit, [Parameter(Mandatory=$true)][string]$Tag)
$ErrorActionPreference = 'Stop'
if ($Commit -notmatch '^[a-f0-9]{40}$') { throw 'A complete validated commit SHA is required.' }
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = [IO.Path]::GetFullPath((Join-Path $project '../Distribution'))
$api = 'https://api.github.com/repos/kihwan04/Mongscape'
$files = @('Mongscape-Windows-x64.zip', 'SHA256SUMS.txt')
foreach ($file in $files) { if (-not (Test-Path -LiteralPath (Join-Path $output $file))) { throw "Release asset missing: $file" } }
$env:GIT_TERMINAL_PROMPT = '0'
$credentialLines = "protocol=https`nhost=github.com`n`n" | git credential fill
$credential = @{}
foreach ($line in $credentialLines) {
    $parts = $line -split '=', 2
    if ($parts.Count -eq 2) { $credential[$parts[0]] = $parts[1] }
}
if (-not $credential['password']) { throw 'Existing GitHub login is required.' }
$headers = @{ Authorization = 'Bearer ' + $credential['password']; Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28' }
try {
    $repository = Invoke-RestMethod -Uri $api -Headers $headers
    if (-not $repository.permissions.push) { throw 'Write permission to the repository is required to publish a release.' }
    # Visibility is the owner's decision; this script never changes it. A private repository's assets are not downloadable by players.
    if ($repository.private) { Write-Warning 'The repository is private: players cannot download this release until the owner makes it public.' }
    $releases = Invoke-RestMethod -Uri ($api+'/releases?per_page=100') -Headers $headers
    if (@($releases | Where-Object tag_name -eq $tag).Count -gt 0) { throw 'Release tag already exists; inspect before updating.' }
    $body = Get-Content -LiteralPath (Join-Path $project 'docs/PUBLIC_RELEASE.md') -Raw -Encoding utf8
    $releaseRequest = @{ tag_name=$tag; target_commitish=$Commit; name=('Mongscape '+$tag); body=$body; draft=$true; prerelease=$false } | ConvertTo-Json
    $release = Invoke-RestMethod -Uri ($api+'/releases') -Headers $headers -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($releaseRequest))
    $uploadBase = $release.upload_url -replace '\{.*$', ''
    foreach ($file in $files) {
        $path = Join-Path $output $file
        $type = if ($file.EndsWith('.zip')) { 'application/zip' } else { 'text/plain' }
        $asset = Invoke-RestMethod -Uri ($uploadBase+'?name='+[Uri]::EscapeDataString($file)) -Headers $headers -Method Post -ContentType $type -InFile $path -TimeoutSec 600
        if ($asset.size -ne (Get-Item -LiteralPath $path).Length) { throw "Uploaded size mismatch: $file" }
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($asset.digest -and $asset.digest -ne ('sha256:'+ $hash)) { throw "Uploaded SHA256 mismatch: $file" }
        Write-Output ('Uploaded: '+$asset.name+' ('+$asset.size+' bytes)')
    }
    $published = Invoke-RestMethod -Uri ($api+'/releases/'+$release.id) -Headers $headers -Method Patch -ContentType 'application/json' -Body '{"draft":false,"prerelease":false,"make_latest":"true"}'
    if ($published.draft) { throw 'Release publication failed.' }
    Write-Output ('Release: '+$published.html_url)
    $published.assets | Select-Object name,size,browser_download_url
} finally {
    $credentialLines=$null; $credential.Clear(); $headers.Clear()
}
