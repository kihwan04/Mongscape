@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Services\Identity\StartIdentity.ps1"
