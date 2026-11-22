@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo Laboratorio do Tito: compilando e conferindo tudo (cerca de 1 minuto)...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath '%~dp0' -Recurse -File | Unblock-File -ErrorAction SilentlyContinue; & '%~dp0scripts\Laboratorio.ps1'"
echo.
if exist "%~dp0Dados\laboratorio-telas.png" start "" "%~dp0Dados\laboratorio-telas.png"
if exist "%~dp0Dados\laboratorio-olhos.png" start "" "%~dp0Dados\laboratorio-olhos.png"
pause
