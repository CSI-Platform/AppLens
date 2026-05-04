@echo off
setlocal EnableExtensions
set APPLENS_INTERACTIVE=0
set "APPLENS_LOG=%USERPROFILE%\Desktop\AppLens_Tune_Run_Log.txt"

echo AppLens-Tune is running. This can take a few minutes on busy machines.
echo [%DATE% %TIME%] Running AppLens-Tune.ps1>"%APPLENS_LOG%"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0AppLens-Tune.ps1" >>"%APPLENS_LOG%" 2>&1
set "APPLENS_EXIT=0"
if errorlevel 1 set "APPLENS_EXIT=1"
>>"%APPLENS_LOG%" echo [%DATE% %TIME%] Exit code: %APPLENS_EXIT%

echo.
if "%APPLENS_EXIT%"=="0" (
  echo AppLens-Tune finished. Check your Desktop for AppLens_Tune_Results_*.md.
) else (
  echo AppLens-Tune did not finish successfully.
)
echo Log: %APPLENS_LOG%
echo.
echo Press any key to close.
pause >nul
exit /b %APPLENS_EXIT%
