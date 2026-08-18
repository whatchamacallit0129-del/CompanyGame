@echo off
setlocal

cd /d D:\CompanyProject

echo ========================================
echo CompanyProject Auto Pull Loop
echo ========================================
echo Repository: %cd%
echo Check interval: 5 seconds
echo ========================================
echo.

:LOOP

echo [%date% %time%] GitHub 확인 중...

D:\Git\cmd\git.exe fetch origin

if errorlevel 1 (
    echo [ERROR] GitHub 확인 실패
    echo 5초 후 재시도합니다.
    timeout /t 5 /nobreak >nul
    goto LOOP
)

for /f "delims=" %%A in ('D:\Git\cmd\git.exe rev-parse HEAD') do set LOCAL=%%A
for /f "delims=" %%A in ('D:\Git\cmd\git.exe rev-parse origin/main') do set REMOTE=%%A

if "%LOCAL%"=="%REMOTE%" (
    echo [OK] 변경사항 없음
) else (
    echo [UPDATE] GitHub에 새 변경사항 발견!
    echo [UPDATE] Pull 시작...

    D:\Git\cmd\git.exe pull --ff-only origin main

    if errorlevel 1 (
        echo.
        echo [ERROR] 자동 Pull 실패
        echo 로컬 변경사항 또는 Git 충돌을 확인해야 합니다.
        echo 자동 Pull을 중단합니다.
        pause
        exit /b 1
    )

    echo [SUCCESS] Pull 완료
)

echo.
timeout /t 5 /nobreak >nul
goto LOOP