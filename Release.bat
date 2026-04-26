@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 932 >nul

echo === DMarket Build Publish Zip Start ===
echo.

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

set "APP_PROJECT=%ROOT%\DMarket\DMarket.csproj"
set "UPDATER_PROJECT=%ROOT%\DMarketUpdater\DMarketUpdater.csproj"

set "APP_PUBLISH_DIR=%ROOT%\DMarket\bin\Release\net8.0-windows\win-x64\publish"
set "UPDATER_PUBLISH_DIR=%ROOT%\DMarketUpdater\bin\Release\net8.0\win-x64\publish"

set "APP_ZIP=%ROOT%\DMarket.zip"
set "UPDATER_ZIP=%ROOT%\DMarketUpdater.zip"
set "VERSION_JSON=%ROOT%\version.json"
set "RELEASE_NOTES_JSON=%ROOT%\release-notes.json"

set "VERSION="
set /p VERSION=Enter version (ex: 1.0.0): 
if not defined VERSION (
    echo [ERROR] Version is empty.
    pause
    exit /b 1
)

echo [INFO] version=%VERSION%
echo [INFO] app project=%APP_PROJECT%
echo [INFO] updater project=%UPDATER_PROJECT%
echo.

if not exist "%APP_PROJECT%" (
    echo [ERROR] App project not found.
    echo         %APP_PROJECT%
    pause
    exit /b 1
)

if not exist "%UPDATER_PROJECT%" (
    echo [ERROR] Updater project not found.
    echo         %UPDATER_PROJECT%
    pause
    exit /b 1
)

echo [1/6] Publish Updater
dotnet publish "%UPDATER_PROJECT%" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:Version=%VERSION% /p:FileVersion=%VERSION%.0 /p:AssemblyVersion=1.0.0.0 /p:InformationalVersion=%VERSION%
if errorlevel 1 (
    echo [ERROR] Updater publish failed.
    pause
    exit /b 1
)

echo [2/6] Publish App
dotnet publish "%APP_PROJECT%" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:AssemblyName=DMarket /p:Product=DMarket /p:Version=%VERSION% /p:FileVersion=%VERSION%.0 /p:AssemblyVersion=1.0.0.0 /p:InformationalVersion=%VERSION%
if errorlevel 1 (
    echo [ERROR] App publish failed.
    pause
    exit /b 1
)

if not exist "%APP_PUBLISH_DIR%\DMarket.exe" (
    echo [ERROR] DMarket.exe not found.
    echo         %APP_PUBLISH_DIR%\DMarket.exe
    pause
    exit /b 1
)

if not exist "%UPDATER_PUBLISH_DIR%\Updater.exe" (
    echo [ERROR] Updater.exe not found.
    echo         %UPDATER_PUBLISH_DIR%\Updater.exe
    pause
    exit /b 1
)

echo [3/6] Create DMarket.zip
if exist "%APP_ZIP%" del /f /q "%APP_ZIP%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%APP_PUBLISH_DIR%\DMarket.exe' -DestinationPath '%APP_ZIP%' -Force"
if errorlevel 1 (
    echo [ERROR] Failed to create DMarket.zip
    pause
    exit /b 1
)

echo [4/6] Create DMarketUpdater.zip
if exist "%UPDATER_ZIP%" del /f /q "%UPDATER_ZIP%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%UPDATER_PUBLISH_DIR%\Updater.exe' -DestinationPath '%UPDATER_ZIP%' -Force"
if errorlevel 1 (
    echo [ERROR] Failed to create DMarketUpdater.zip
    pause
    exit /b 1
)

echo [5/6] Create version.json
> "%VERSION_JSON%" (
 echo {
 echo   "latest": "%VERSION%",
 echo   "url": "https://raw.githubusercontent.com/Chairman-bits/DMarket/main/DMarket.zip",
 echo   "urls": [
 echo     "https://raw.githubusercontent.com/Chairman-bits/DMarket/main/DMarket.zip",
 echo     "https://raw.githubusercontent.com/Chairman-bits/DMarket/main/DMarketUpdater.zip"
 echo   ],
 echo   "appExeName": "DMarket.exe"
 echo }
)

echo [6/6] Create release-notes.json
if not exist "%RELEASE_NOTES_JSON%" (
  > "%RELEASE_NOTES_JSON%" (
   echo [
   echo   {
   echo     "version": "%VERSION%",
   echo     "publishedAt": "%DATE%",
   echo     "notes": [
   echo       "DMarket initial release."
   echo     ]
   echo   }
   echo ]
  )
)

echo.
echo Done.
echo DMarket.zip        : %APP_ZIP%
echo DMarketUpdater.zip : %UPDATER_ZIP%
echo version.json       : %VERSION_JSON%
echo release-notes.json : %RELEASE_NOTES_JSON%
echo.
echo Upload these files to GitHub main branch:
echo   %APP_ZIP%
echo   %UPDATER_ZIP%
echo   %VERSION_JSON%
echo   %RELEASE_NOTES_JSON%
echo.

pause
endlocal
