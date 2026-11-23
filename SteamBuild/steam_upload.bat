@echo off
rem Build the game with Unity, then upload the build to Steam.
rem   steam_upload.bat playtest            build + upload to the playtest app (5681720)
rem   steam_upload.bat release             build + upload to the main app     (5656650)
rem   steam_upload.bat playtest nobuild    upload the build that is already in the folder
rem   steam_upload.bat playtest noupload   build only
rem Close the Unity editor first: a batch build cannot run while the project is open.
rem The upload does not set the build live. Pick the branch in Steamworks > SteamPipe > Builds.
setlocal EnableExtensions

set "MODE=%~1"
set "OPTION=%~2"
if /i "%MODE%"=="playtest" (
    set "FOLDER=playtest"
    set "METHOD=PlaytestBuild.SteamPlaytest"
    set "SCRIPT=app_build_playtest.vdf"
) else if /i "%MODE%"=="release" (
    set "FOLDER=Release"
    set "METHOD=PlaytestBuild.SteamRelease"
    set "SCRIPT=app_build_release.vdf"
) else (
    echo Usage: steam_upload.bat playtest^|release [nobuild^|noupload]
    goto :fail
)

set "HERE=%~dp0"
for %%I in ("%HERE%..") do set "PROJECT=%%~fI"
if not defined UNITY_EXE set "UNITY_EXE=C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe"
if not defined STEAMCMD set "STEAMCMD=D:\SteamworksSDK\sdk\tools\ContentBuilder\builder\steamcmd.exe"
set "RESULTS=%HERE%%FOLDER%-results.txt"
set "BUILDLOG=%HERE%%FOLDER%-build.log"

if /i "%OPTION%"=="nobuild" goto :upload

if not exist "%UNITY_EXE%" (
    echo Unity not found: %UNITY_EXE%
    echo Set UNITY_EXE to the editor path and run again.
    goto :fail
)
if exist "%PROJECT%\Temp\UnityLockfile" (
    echo The project looks open in the Unity editor. Close it and run again.
    echo If Unity is not running, delete "%PROJECT%\Temp\UnityLockfile".
    goto :fail
)
echo [1/2] Building %MODE% into SteamBuild\%FOLDER% ... this takes several minutes.
if exist "%RESULTS%" del "%RESULTS%"
start "" /wait "%UNITY_EXE%" -batchmode -quit -projectPath "%PROJECT%" -executeMethod %METHOD% -logFile "%BUILDLOG%"
if errorlevel 1 (
    echo Build FAILED. See %BUILDLOG%
    if exist "%RESULTS%" type "%RESULTS%"
    goto :fail
)
findstr /b /c:"Succeeded" "%RESULTS%" >nul 2>&1
if errorlevel 1 (
    echo Build did not report success. See %BUILDLOG%
    if exist "%RESULTS%" type "%RESULTS%"
    goto :fail
)
type "%RESULTS%"

:upload
if /i "%OPTION%"=="noupload" goto :done
if not exist "%HERE%%FOLDER%\*.exe" (
    echo No build found in SteamBuild\%FOLDER%. Run without "nobuild" first.
    goto :fail
)
if not exist "%STEAMCMD%" (
    echo steamcmd not found: %STEAMCMD%
    echo Set STEAMCMD to its path and run again.
    goto :fail
)
if not defined STEAM_USER set /p "STEAM_USER=Steam build account name: "
if not defined STEAM_USER (
    echo No account name given.
    goto :fail
)
echo [2/2] Uploading SteamBuild\%FOLDER% to Steam (%MODE%) ...
echo steamcmd asks for the password and the Steam Guard code if it needs them.
"%STEAMCMD%" +login "%STEAM_USER%" +run_app_build "%HERE%scripts\%SCRIPT%" +quit
if errorlevel 1 (
    echo Upload FAILED. See the messages above and SteamBuild\output.
    goto :fail
)
echo Upload finished. Set the build live in Steamworks ^> SteamPipe ^> Builds.

:done
echo Done.
if not defined NOPAUSE pause
exit /b 0

:fail
if not defined NOPAUSE pause
exit /b 1
