param([ValidateSet('EditMode','PlayMode','Build')][string]$Phase='EditMode', [string]$ProjectPath, [string]$ReportDirectory, [string]$TestFilter)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$reportRoot=if($ReportDirectory){[IO.Path]::GetFullPath($ReportDirectory)}else{Join-Path $projectRoot 'Builds/QA/MuhanokTestMerge'}
$qa=Join-Path $reportRoot ($Phase+'-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $qa -Force | Out-Null
$project=if($ProjectPath){[IO.Path]::GetFullPath($ProjectPath)}else{Join-Path $projectRoot 'Unity'}
if(-not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))){throw 'Unity project path missing'}
$log=Join-Path $qa 'Unity.log'
$xml=Join-Path $qa 'tests.xml'
$arguments=@('-batchmode','-projectPath',('"'+$project+'"'),'-logFile',('"'+$log+'"'),'--mongscape-tests','--muhanok-data',('"'+(Join-Path $qa 'PlayerData')+'"'))
if($Phase -eq 'Build'){$arguments+=@('-quit','-executeMethod','Muhanok.Editor.MuhanokBuildTools.BuildWindows')}
else{$arguments+=@('-runTests','-testPlatform',$Phase,'-testResults',('"'+$xml+'"'))}
if($Phase -ne 'Build' -and $TestFilter){$arguments+=@('-testFilter',$TestFilter)}
# Default Hub folder first, then the Hub's custom install folder (Unity Hub > Preferences > Installs).
$unity='C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe'
$hubPath=Join-Path $env:APPDATA 'UnityHub/secondaryInstallPath.json'
if(-not (Test-Path -LiteralPath $unity) -and (Test-Path -LiteralPath $hubPath)){$unity=Join-Path (Get-Content -LiteralPath $hubPath -Raw | ConvertFrom-Json) '6000.3.10f1/Editor/Unity.exe'}
if(-not (Test-Path -LiteralPath $unity)){throw 'Unity 6000.3.10f1 not found'}
$process=Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
$result=@{phase=$Phase;exitCode=$process.ExitCode;log=$log;project=$project}
if($TestFilter){$result.filter=$TestFilter}
if($Phase -ne 'Build' -and (Test-Path -LiteralPath $xml)){
    [xml]$tests=Get-Content -LiteralPath $xml -Raw
    $result.result=[string]$tests.'test-run'.result
    $result.passed=[int]$tests.'test-run'.passed
    $result.failed=[int]$tests.'test-run'.failed
    $result.xml=$xml
}
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reportRoot ($Phase+'-latest.json')) -Encoding UTF8
$result | ConvertTo-Json -Compress | Write-Output
if($process.ExitCode -ne 0){Select-String -LiteralPath $log -Pattern 'error CS|Exception:|No valid Unity|FAIL' | Select-Object -Last 12 | ForEach-Object {$_.Line};exit 1}
if($Phase -eq 'Build'){
    if(-not (Select-String -LiteralPath $log -SimpleMatch 'MUHANOK_BUILD_PASS')){throw 'Windows build pass marker missing'}
}elseif($result.result -ne 'Passed' -or $result.passed -lt 1){throw 'Unity tests failed, no tests matched, or results missing'}
exit 0
