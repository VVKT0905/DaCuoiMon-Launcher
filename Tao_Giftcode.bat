@echo off
chcp 65001 >nul
cd /d "%~dp0Tools\GiftcodeGen\bin\Publish"
start "" "GiftcodeGen.exe"
exit
