@echo off
setlocal EnableExtensions EnableDelayedExpansion

cd /d D:\CompanyProject

title CompanyProject AI Unity Bridge Loop

echo ================================================
echo CompanyProject AI <-> Unity GitHub Bridge
echo ================================================
echo Project: %cd%
echo Pull interval: 3 seconds
echo.
echo IMPORTANT: stop the old auto_pull_loop.bat before starting this loop.
echo.

:LOOP
call :PULL
if errorlevel 1 (
    timeout /t 5 /nobreak >nul
    goto LOOP
)

call :PUBLISH_RESULT

timeout /t 3 /nobreak >nul
goto LOOP

:PULL
git fetch origin main >nul 2>&1
if errorlevel 1 (
    echo [%date% %time%] [WARN] git fetch failed.
    exit /b 1
)

for /f "delims=" %%A in ('git rev-parse HEAD') do set LOCAL=%%A
for /f "delims=" %%A in ('git rev-parse origin/main') do set REMOTE=%%A

if "!LOCAL!"=="!REMOTE!" exit /b 0

for /f "delims=" %%A in ('git rev-list --count HEAD..origin/main') do set AHEAD=%%A
for /f "delims=" %%A in ('git rev-list --count origin/main..HEAD') do set BEHIND=%%A

if "!BEHIND!"=="0" (
    git merge --ff-only origin/main >nul 2>&1
    if errorlevel 1 (
        echo [%date% %time%] [WARN] fast-forward failed.
        exit /b 1
    )
    echo [%date% %time%] [PULL] GitHub changes applied.
    exit /b 0
)

if "!AHEAD!"=="0" exit /b 0

git merge --no-edit origin/main >nul 2>&1
if errorlevel 1 (
    echo [%date% %time%] [CONFLICT] Merge conflict. Automatic loop paused for this cycle.
    git merge --abort >nul 2>&1
    exit /b 1
)

echo [%date% %time%] [PULL] Merged GitHub changes.
exit /b 0

:PUBLISH_RESULT
rem Unity consumes ai_command.json and writes results/ai_result.json.
rem Commit only the bridge queue/result files so unrelated local work is never staged.
git status --porcelain -- ai_command.json ai_command.processing.json results/ai_result.json > "%TEMP%\company_ai_status.txt"
set HAS_BRIDGE_CHANGE=0
for /f "delims=" %%A in (%TEMP%\company_ai_status.txt) do set HAS_BRIDGE_CHANGE=1
if "!HAS_BRIDGE_CHANGE!"=="0" exit /b 0

git add -- ai_command.json ai_command.processing.json results/ai_result.json
git diff --cached --quiet -- ai_command.json ai_command.processing.json results/ai_result.json
if not errorlevel 1 (
    git reset -- ai_command.json ai_command.processing.json results/ai_result.json >nul 2>&1
    exit /b 0
)

git commit -m "AI Unity bridge result" >nul 2>&1
if errorlevel 1 (
    git reset -- ai_command.json ai_command.processing.json results/ai_result.json >nul 2>&1
    echo [%date% %time%] [WARN] Could not commit bridge result.
    exit /b 1
)

git push origin main >nul 2>&1
if errorlevel 1 (
    echo [%date% %time%] [WARN] Could not push bridge result. Local commit preserved.
    exit /b 1
)

echo [%date% %time%] [PUSH] Unity result returned to GitHub.
exit /b 0
