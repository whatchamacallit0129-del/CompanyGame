@echo off
setlocal EnableExtensions

cd /d D:\CompanyProject

title CompanyProject Auto Pull Loop

echo ========================================
echo CompanyProject Auto Pull Loop
echo ========================================
echo Repository: %cd%
echo Check interval: 5 seconds
echo ========================================
echo.

:LOOP

echo [%date% %time%] GitHub check...

call D:\CompanyProject\auto_pull.bat
set PULL_RESULT=%ERRORLEVEL%

if "%PULL_RESULT%"=="0" (
    echo [OK] Sync check completed.
) else if "%PULL_RESULT%"=="2" (
    echo [CONFLICT] Local and GitHub histories have conflicts.
    echo [WAIT] Automatic pull will retry after 5 seconds.
) else (
    echo [WARN] Auto Pull returned code %PULL_RESULT%.
    echo [WAIT] Retrying after 5 seconds.
)

echo.
timeout /t 5 /nobreak >nul
goto LOOP
