@echo off
setlocal DisableDelayedExpansion
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Support\ImporterApp\setup.ps1" %*
if errorlevel 1 (
  echo.
  echo Setup did not finish. See the message above and run this file again.
  pause
  exit /b 1
)
