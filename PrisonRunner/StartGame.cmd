@echo off
setlocal
set "MONGSCAPE_DIR=%~dp0Builds\Mongscape\Windows"
set "MONGSCAPE_GAME=%MONGSCAPE_DIR%\Mongscape.exe"
if not exist "%MONGSCAPE_DIR%\Mongscape.complete" (
  if exist "%~dp0Services\Identity\.runtime\cv2\__init__.py" powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Services\Identity\StartIdentity.ps1" -ConfigPath "%~dp0Unity\identity-server.json"
  echo Opening the current Mongscape source in Unity.
  call "%~dp0PlayInUnity.cmd"
  exit /b
)
if exist "%~dp0Services\Identity\.runtime\cv2\__init__.py" powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Services\Identity\StartIdentity.ps1" -ConfigPath "%MONGSCAPE_DIR%\identity-server.json"
start "" /d "%MONGSCAPE_DIR%" "%MONGSCAPE_GAME%" %*
