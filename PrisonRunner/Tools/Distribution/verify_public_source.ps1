$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$workspace = [IO.Path]::GetFullPath((Join-Path $project '..'))
$output = Join-Path $workspace 'Distribution'
$backup = Join-Path $output 'PrivateMixamoBackup'
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe'
New-Item -ItemType Directory -Path $backup -Force | Out-Null
$motions = Join-Path $project 'Unity/Assets/External/Staging/Mixamo/Animations'
$controllers = @('Monkey','Police') | ForEach-Object { Join-Path $project "Unity/Assets/Game/Content/ArtV2/${_}Humanoid.overrideController" }
$savedControllers = @{}
foreach ($controller in $controllers) { $savedControllers[$controller] = [IO.File]::ReadAllBytes($controller) }
$moved = @()
try {
    foreach ($file in Get-ChildItem -LiteralPath $motions -File | Where-Object { $_.Name -match '\.fbx(\.meta)?$' }) {
        $source = [IO.Path]::GetFullPath($file.FullName)
        $destination = [IO.Path]::GetFullPath((Join-Path $backup $file.Name))
        if (-not $source.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar) -or -not $destination.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar)) { throw 'Backup path outside workspace' }
        if (Test-Path -LiteralPath $destination) { throw "Backup already exists: $destination" }
        Move-Item -LiteralPath $source -Destination $destination
        $moved += @{ Source = $source; Backup = $destination }
    }
    $steps = @(
        @{ Name='PublicSource'; Extra=@('-quit','-executeMethod','Muhanok.Editor.MuhanokMixamoImport.ValidatePublicSource') },
        @{ Name='PublicEditMode'; Extra=@('-runTests','-testPlatform','EditMode','-testResults',('"'+(Join-Path $output 'PublicEditMode.xml')+'"')) },
        @{ Name='PublicPlayMode'; Extra=@('-runTests','-testPlatform','PlayMode','-testResults',('"'+(Join-Path $output 'PublicPlayMode.xml')+'"')) }
    )
    foreach ($step in $steps) {
        $log = Join-Path $output ($step.Name + '.log')
        $arguments = @('-batchmode','-projectPath',('"'+(Join-Path $project 'Unity')+'"'),'-logFile',('"'+$log+'"')) + $step.Extra
        $process = Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
        if ($process.ExitCode -ne 0) { throw ($step.Name + ' failed with code ' + $process.ExitCode) }
        if ($step.Name -eq 'PublicSource') {
            if (-not (Select-String -LiteralPath $log -SimpleMatch 'MUHANOK_PUBLIC_SOURCE_PASS')) { throw 'Public source validation marker missing' }
        } else {
            $resultPath = Join-Path $output ($step.Name+'.xml')
            [xml]$result = Get-Content -LiteralPath $resultPath -Raw
            if ($result.'test-run'.result -ne 'Passed') { throw ($step.Name+' tests failed') }
            Write-Output ($step.Name+': '+$result.'test-run'.passed+' passed, '+$result.'test-run'.failed+' failed')
        }
        Write-Output ($step.Name+' PASS')
    }
} finally {
    foreach ($file in $moved) {
        if (Test-Path -LiteralPath $file.Source) { throw ('Cannot restore over existing file: '+$file.Source) }
        Move-Item -LiteralPath $file.Backup -Destination $file.Source
    }
    foreach ($controller in $controllers) { [IO.File]::WriteAllBytes($controller, $savedControllers[$controller]) }
    Write-Output 'Local Mixamo files and original controller bindings restored.'
}
