@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d D:\CompanyProject

title CompanyProject Auto Pull

echo ========================================
echo CompanyProject Auto Pull
echo ========================================
echo.

git fetch origin main
if errorlevel 1 goto FETCH_FAIL

for /f "delims=" %%A in ('git rev-parse HEAD') do set LOCAL=%%A
for /f "delims=" %%A in ('git rev-parse origin/main') do set REMOTE=%%A

if "!LOCAL!"=="!REMOTE!" goto UP_TO_DATE

for /f "delims=" %%A in ('git rev-list --count HEAD..origin/main') do set AHEAD=%%A
for /f "delims=" %%A in ('git rev-list --count origin/main..HEAD') do set BEHIND=%%A

echo [INFO] Local commits ahead: !BEHIND!
echo [INFO] GitHub commits ahead: !AHEAD!
echo.

rem Case 1: local has no unique commits. Fast-forward safely.
if "!BEHIND!"=="0" goto FAST_FORWARD

rem Case 2: both sides have commits. Preserve local work and merge.
if not "!AHEAD!"=="0" goto MERGE

:FAST_FORWARD
echo [UPDATE] Fast-forwarding local main to origin/main...
git merge --ff-only origin/main
if errorlevel 1 goto PULL_FAIL
goto SUCCESS

:MERGE
echo [UPDATE] Local and GitHub histories diverged.
echo [UPDATE] Creating a merge commit while preserving local changes...
git merge --no-edit origin/main
if errorlevel 1 goto CONFLICT

goto SUCCESS

:UP_TO_DATE
echo [OK] Already up to date.
goto SUCCESS

:CONFLICT
echo.
echo [CONFLICT] Git merge conflict detected.
echo [ERROR] Automatic Pull stopped to prevent data loss.
echo [ERROR] Resolve the conflict in D:\CompanyProject, then run this script again.
exit /b 2

:FETCH_FAIL
echo [ERROR] Git fetch failed. Check network/authentication.
exit /b 3

:PULL_FAIL
echo [ERROR] Safe fast-forward failed. No files were force-reset.
exit /b 4

:SUCCESS
echo.
echo [OK] Auto Pull completed successfully.
exit /b 0
