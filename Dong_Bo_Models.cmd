@echo off
chcp 65001 >nul
title Dong Bo Yes Steve Model - Da Cuoi Mon
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0sync_models.ps1"
pause
