@echo off
set APPLENS_INTERACTIVE=1
powershell -ExecutionPolicy Bypass -File "%~dp0AppLens.ps1"
