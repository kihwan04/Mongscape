@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Services\Identity\SetupIdentity.ps1"
pause
