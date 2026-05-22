@echo off
setlocal

cd /d "%~dp0.."

if "%STEAM_APP_ID%"=="" (
  echo Set STEAM_APP_ID before running this script.
  exit /b 1
)

if "%STEAM_DEPOT_ID%"=="" (
  echo Set STEAM_DEPOT_ID before running this script.
  exit /b 1
)

if "%STEAMWORKS_USERNAME%"=="" (
  echo Set STEAMWORKS_USERNAME before running this script.
  exit /b 1
)

if "%STRINGTHEORY_BUILD_ROOT%"=="" (
  echo Set STRINGTHEORY_BUILD_ROOT to the build folder before running this script.
  exit /b 1
)

if "%STEAMWORKS_SDK_ROOT%"=="" (
  echo Set STEAMWORKS_SDK_ROOT to the Steamworks SDK sdk folder before running this script.
  exit /b 1
)

powershell -ExecutionPolicy Bypass -File "%~dp0UploadStringTheoryBuild.ps1" -AppId "%STEAM_APP_ID%" -DepotId "%STEAM_DEPOT_ID%" -SteamUsername "%STEAMWORKS_USERNAME%" -BuildRoot "%STRINGTHEORY_BUILD_ROOT%" -SdkRoot "%STEAMWORKS_SDK_ROOT%"

echo.
echo SteamPipe upload finished with exit code %ERRORLEVEL%.
echo Check Steamworks > SteamPipe > Builds after a successful upload.
pause
