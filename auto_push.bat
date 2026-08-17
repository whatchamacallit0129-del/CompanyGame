@echo off
cd /d D:\CompanyProject

echo ==============================
echo GitHub 자동 커밋 및 Push
echo ==============================

git add .
git commit -m "Auto commit"
git push origin main

echo.
echo ==============================
echo 완료
echo ==============================
pause