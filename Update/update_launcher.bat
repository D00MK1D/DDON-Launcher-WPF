@echo off
setlocal ENABLEDELAYEDEXPANSION

:: -------- CONFIG --------
set REPO_OWNER=D00MK1D
set REPO_NAME=DDON-Launcher
set ASSET_NAME=DDO_Launcher.exe
set OUTPUT_EXE=DDO_Launcher.exe
:: ------------------------

echo Fetching latest release info...

curl -s https://api.github.com/repos/%REPO_OWNER%/%REPO_NAME%/releases/latest > latest.json

if %ERRORLEVEL% neq 0 (
    echo ERROR: Failed to contact GitHub API.
    pause
    exit /b 1
)

echo Parsing JSON for asset URL...

set LINE=
for /f "delims=" %%A in ('findstr /i /c:"browser_download_url" latest.json ^| findstr /i "%ASSET_NAME%"') do (
    set LINE=%%A
)

if not defined LINE (
    echo ERROR: Could not find asset "%ASSET_NAME%" in the latest release.
    del latest.json
    pause
    exit /b 1
)

:: Extract text after the first colon ONLY
set RAW_URL=%LINE:*": "=%

:: Clean trailing characters
set RAW_URL=!RAW_URL:"=!
set RAW_URL=!RAW_URL:,=!

set URL=!RAW_URL!

if "!URL!"=="" (
    echo ERROR: Failed to parse browser_download_url.
    del latest.json
    pause
    exit /b 1
)

echo Download URL found:
echo !URL!
echo.

echo Removing old EXE (if exists)...
del "%OUTPUT_EXE%" 2>nul

echo Downloading latest version...
curl -L -o "%OUTPUT_EXE%" "!URL!"

if %ERRORLEVEL% neq 0 (
    echo ERROR: Download failed.
    del latest.json
    pause
    exit /b 1
)

echo Cleaning up...
del latest.json
echo.

echo Update complete.
echo.

echo Cleaning up updater script...
start "" cmd /c "timeout /t 2 >nul & del update_launcher.bat

exit /b 0