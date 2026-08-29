@echo off
setlocal
title Uninstall DeskBridge native connection
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\uninstall-native-host.ps1"
echo.
echo Native Messaging registration removed. Local files were not deleted.
pause
