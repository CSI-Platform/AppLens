@echo off
setlocal EnableExtensions

set APPLENS_INTERACTIVE=0
set "CAPTURE_DIR=%USERPROFILE%\Desktop\AppLens-Capture-%COMPUTERNAME%"
set "APPLENS_OUTPUT_DIR=%CAPTURE_DIR%"
set "CAPTURE_EXIT=0"

if not exist "%CAPTURE_DIR%" mkdir "%CAPTURE_DIR%"

echo AppLens Capture
echo.
echo This will run AppLens and AppLens-Tune, then open the output folder.
echo Output folder:
echo %CAPTURE_DIR%
echo.

call :run_script "AppLens" "AppLens.ps1" "AppLens_Run_Log.txt"
call :run_script "AppLens-Tune" "AppLens-Tune.ps1" "AppLens_Tune_Run_Log.txt"

> "%CAPTURE_DIR%\README-What-To-Send.txt" echo Send this entire folder back for AppLens intake.
>>"%CAPTURE_DIR%\README-What-To-Send.txt" echo.
>>"%CAPTURE_DIR%\README-What-To-Send.txt" echo Expected report files:
>>"%CAPTURE_DIR%\README-What-To-Send.txt" echo - AppLens_Results_%COMPUTERNAME%.txt
>>"%CAPTURE_DIR%\README-What-To-Send.txt" echo - AppLens_Tune_Results_%COMPUTERNAME%.txt
>>"%CAPTURE_DIR%\README-What-To-Send.txt" echo.
>>"%CAPTURE_DIR%\README-What-To-Send.txt" echo Include the log files if either report is missing.
>>"%CAPTURE_DIR%\README-What-To-Send.txt" echo Do not include serial numbers or UUIDs unless explicitly requested.

echo.
if "%CAPTURE_EXIT%"=="0" (
  echo Capture finished.
) else (
  echo Capture finished with at least one issue. Include the log files.
)
echo.
echo Opening output folder...
start "" "%CAPTURE_DIR%"
echo.
echo Press any key to close.
pause >nul
exit /b %CAPTURE_EXIT%

:run_script
set "APP_NAME=%~1"
set "SCRIPT_NAME=%~2"
set "LOG_NAME=%~3"
set "LOG_PATH=%CAPTURE_DIR%\%LOG_NAME%"

echo Running %APP_NAME%...
echo [%DATE% %TIME%] Running %SCRIPT_NAME%>"%LOG_PATH%"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0%SCRIPT_NAME%" >>"%LOG_PATH%" 2>&1
set "SCRIPT_EXIT=0"
if errorlevel 1 set "SCRIPT_EXIT=1"
>>"%LOG_PATH%" echo [%DATE% %TIME%] Exit code: %SCRIPT_EXIT%

if "%SCRIPT_EXIT%"=="0" (
  echo %APP_NAME% finished.
) else (
  echo %APP_NAME% had an issue. See %LOG_PATH%
  set "CAPTURE_EXIT=1"
)
exit /b 0
