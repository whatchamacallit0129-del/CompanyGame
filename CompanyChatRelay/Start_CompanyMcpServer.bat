@echo off
setlocal
cd /d "%~dp0"

if exist ".venv\Scripts\python.exe" (
    ".venv\Scripts\python.exe" mcp_server.py
    goto :end
)

where py >nul 2>nul
if %errorlevel% equ 0 (
    py -3 mcp_server.py
    goto :end
)

where python >nul 2>nul
if %errorlevel% equ 0 (
    python mcp_server.py
    goto :end
)

echo Python 3 was not found. See README.md for installation steps.
pause

:end
