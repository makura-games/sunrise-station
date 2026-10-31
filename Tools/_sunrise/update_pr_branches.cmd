@echo off
setlocal

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0update_pr_branches.ps1"
set "SCRIPT_EXIT=%ERRORLEVEL%"

echo.
pause
exit /b %SCRIPT_EXIT%
