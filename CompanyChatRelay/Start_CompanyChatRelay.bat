@echo off
setlocal
cd /d "%~dp0"

where py >nul 2>nul
if %errorlevel% equ 0 (
    py -3 relay_server.py
    goto :end
)

where python >nul 2>nul
if %errorlevel% equ 0 (
    python relay_server.py
    goto :end
)

echo Python 3 was not found. Install Python 3 and make sure py or python is on PATH.
pause

:end
