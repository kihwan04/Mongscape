param([string]$Python = "")
$ErrorActionPreference='Stop'
$projectRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$unityEditorData='C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Data'
if(-not $Python){$Python=Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'}
& $Python (Join-Path $PSScriptRoot 'compile_sources.py')
if($LASTEXITCODE -ne 0){throw 'Source compilation failed.'}
$qaCompileRoot=Join-Path $projectRoot 'Builds/QA/Compile'
& "$unityEditorData/NetCoreRuntime/dotnet.exe" "$unityEditorData/DotNetSdkRoslyn/csc.dll" /nologo /target:exe /out:"$qaCompileRoot/RuleRunner.exe" /reference:"$unityEditorData/MonoBleedingEdge/lib/mono/4.7.2-api/mscorlib.dll" /reference:"$unityEditorData/MonoBleedingEdge/lib/mono/4.7.2-api/System.dll" /reference:"$unityEditorData/MonoBleedingEdge/lib/mono/4.7.2-api/System.Core.dll" /reference:"$unityEditorData/Resources/PackageManager/BuiltInPackages/com.unity.ext.nunit/net40/unity-custom/nunit.framework.dll" (Join-Path $PSScriptRoot 'RuleRunner.cs')
if($LASTEXITCODE -ne 0){throw 'Rule runner compilation failed.'}
$env:MONO_PATH="$qaCompileRoot;$unityEditorData/Managed/UnityEngine;$unityEditorData/Resources/PackageManager/BuiltInPackages/com.unity.ext.nunit/net40/unity-custom"
Push-Location -LiteralPath $qaCompileRoot
try { & "$unityEditorData/MonoBleedingEdge/bin/mono.exe" RuleRunner.exe | Tee-Object -FilePath '../pure-rule-tests.log';if($LASTEXITCODE -ne 0){throw 'Rule tests failed.'} } finally { Pop-Location }
& $Python (Join-Path $projectRoot 'Services/Identity/test_identity.py')
if($LASTEXITCODE -ne 0){throw 'Identity integration tests failed.'}
& $Python (Join-Path $projectRoot 'Services/Identity/test_http.py')
if($LASTEXITCODE -ne 0){throw 'Identity HTTP tests failed.'}
