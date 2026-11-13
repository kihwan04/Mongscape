@echo off
setlocal
set "MUHANOK_EDITOR=C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe"
if not exist "%MUHANOK_EDITOR%" (
  echo Unity 6000.3.10f1 was not found. Open Unity through Unity Hub.
  pause
  exit /b 1
)
if exist "%~dp0Unity\Temp\UnityLockfile" (
  if not exist "%~dp0Unity\Library" mkdir "%~dp0Unity\Library"
  >"%~dp0Unity\Library\MuhanokQuickPlay.request" echo play
  echo GameScene play requested in the open Unity Editor.
  exit /b 0
)
start "" "%MUHANOK_EDITOR%" -projectPath "%~dp0Unity" -executeMethod Muhanok.Editor.MuhanokQuickPlay.PlayGameScene
