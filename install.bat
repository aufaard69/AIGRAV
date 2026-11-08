@echo off
setlocal
title AIGRAV Suite Installer
echo ========================================================
echo         AIGRAV Automated One-Click Installer
echo ========================================================
echo.
echo Launching PowerShell Installer with ExecutionPolicy Bypass...
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-AIGRAV.ps1"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Installation encountered an issue. Exit code: %ERRORLEVEL%
    pause
)
endlocal
