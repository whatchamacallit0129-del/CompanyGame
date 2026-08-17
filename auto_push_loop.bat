@echo off
title CompanyProject Auto Push
cd /d D:\CompanyProject

echo ========================================
echo CompanyProject 자동 Push 시스템
echo ========================================
echo.
echo 10분마다 변경사항을 확인합니다.
echo 종료하려면 이 창을 닫으세요.
echo.

:LOOP

echo [%date% %time%] 변경사항 확인 중...

call auto_push.bat

echo.
echo 다음 확인까지 10분 대기합니다...
echo.

timeout /t 600 /nobreak >nul

goto LOOP