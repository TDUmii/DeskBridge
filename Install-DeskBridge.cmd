@echo off
setlocal
title Install DeskBridge
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\install-native-host.ps1" -HostPath "%~dp0DeskBridge.Host.exe"
if errorlevel 1 (
  echo.
  echo DeskBridge installation failed. Read INSTALL.txt for the three setup steps.
  pause
  exit /b 1
)
start "" "%~dp0DeskBridge.App.exe"
echo.
echo DeskBridge is installed and has been opened.
timeout /t 3 /nobreak >nul
