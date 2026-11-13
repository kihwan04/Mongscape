param([Parameter(Mandatory=$true)][string]$GamePath,[Parameter(Mandatory=$true)][string]$OutputDirectory,[switch]$SkipPerformance)
$ErrorActionPreference='Stop'
$game=(Resolve-Path -LiteralPath $GamePath).Path
$qa=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $qa -Force | Out-Null
function Invoke-PlayerCheck([string]$Name,[string[]]$Flags,[string]$Marker){
    $output=Join-Path $qa $Name
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $log=Join-Path $qa ($Name+'.log')
    $arguments=@('-screen-width','1280','-screen-height','720','-screen-fullscreen','0','--mongscape-tests','--muhanok-data',('"'+(Join-Path $qa ($Name+'PlayerData'))+'"'),'-logFile',('"'+$log+'"'))+$Flags
    $process=Start-Process -FilePath $game -ArgumentList $arguments -WorkingDirectory (Split-Path -Parent $game) -WindowStyle Hidden -PassThru -Wait
    if($process.ExitCode -ne 0){throw "$Name exited $($process.ExitCode): $log"}
    if(-not (Select-String -LiteralPath $log -SimpleMatch $Marker -Quiet)){throw "$Name marker missing: $log"}
    [pscustomobject]@{phase=$Name;exitCode=$process.ExitCode;marker=$Marker;log=$log} | ConvertTo-Json -Compress
}
if(-not $SkipPerformance){Invoke-PlayerCheck 'After' @('--mongscape-review','--review-no-camera','--review-output',('"'+(Join-Path $qa 'After')+'"')) 'MONGSCAPE_EXPANSION_REVIEW_PASS'}
Invoke-PlayerCheck 'UiCapture' @('--muhanok-uicapture','--muhanok-out',('"'+(Join-Path $qa 'UiCapture')+'"')) 'MUHANOK_UI_CAPTURE_PASS'
Invoke-PlayerCheck 'RuntimeSmoke' @('--muhanok-smoke') 'MUHANOK_RUNTIME_SMOKE_PASS'
