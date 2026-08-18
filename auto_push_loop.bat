```bat
@echo off
title CompanyProject Auto Push
cd /d D:\CompanyProject

echo ========================================
echo CompanyProject Auto Push
echo ========================================
echo 30초마다 변경사항을 확인합니다.
echo 새 파일도 자동으로 추가하고 Push합니다.
echo 종료하려면 이 창을 닫으세요.
echo.

:LOOP

echo [%date% %time%] 변경사항 확인 중...

git add -A

git diff --cached --quiet

if %errorlevel%==0 (
    echo [OK] 변경사항 없음
) else (
    echo [UPDATE] 변경사항 발견!

    git commit -m "Auto commit - %date% %time%"

    if errorlevel 1 (
        echo [ERROR] Commit 실패!
    ) else (
        git push origin main

        if errorlevel 1 (
            echo [ERROR] Push 실패!
        ) else (
            echo [SUCCESS] Push 완료!
        )
    )
)

echo.
echo 다음 확인까지 30초 대기합니다...
echo.

timeout /t 30 /nobreak >nul

goto LOOP
```
