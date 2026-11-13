param([Parameter(Mandatory=$true)][string]$GamePath,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$game=(Resolve-Path -LiteralPath $GamePath).Path
$qa=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $qa -Force | Out-Null
foreach($phase in @('ForkReview','RuntimeSmoke')){
    $output=Join-Path $qa $phase
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $log=Join-Path $qa ($phase+'.log')
    $flags=if($phase -eq 'ForkReview'){@('--mongscape-review','--review-details','--review-no-camera','--review-output',('"'+$output+'"'))}else{@('--muhanok-smoke')}
    $marker=if($phase -eq 'ForkReview'){'MONGSCAPE_EXPANSION_REVIEW_PASS'}else{'MUHANOK_RUNTIME_SMOKE_PASS'}
    $arguments=@('-screen-width','1280','-screen-height','720','-screen-fullscreen','0','--mongscape-tests','--muhanok-data',('"'+(Join-Path $qa ($phase+'PlayerData'))+'"'),'-logFile',('"'+$log+'"'))+$flags
    $process=Start-Process -FilePath $game -ArgumentList $arguments -WorkingDirectory (Split-Path -Parent $game) -WindowStyle Hidden -PassThru -Wait
    $result=[pscustomobject]@{phase=$phase;exitCode=$process.ExitCode;marker=$marker;log=$log}
    $result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $qa ($phase+'-latest.json')) -Encoding UTF8
    $result | ConvertTo-Json -Compress
    if($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch $marker -Quiet)){throw "$phase failed; inspect $log"}
}
exit 0
