@echo off
cd /d D:\CompanyProject

echo ==============================
echo CompanyProject Auto Push
echo ==============================

git add .

git diff --cached --quiet

if %errorlevel%==0 (
    echo 변경사항 없음 - Push하지 않습니다.
    exit /b 0
)

git commit -m "Auto commit - %date% %time%"

if %errorlevel% neq 0 (
    echo Commit 실패!
    exit /b 1
)

git push origin main

if %errorlevel% neq 0 (
    echo Push 실패!
    exit /b 1
)

echo Push 완료!