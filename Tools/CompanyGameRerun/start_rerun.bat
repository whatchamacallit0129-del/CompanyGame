@echo off
setlocal
cd /d "%~dp0..\.."
python "Tools\CompanyGameRerun\app.py"
if errorlevel 1 pause
