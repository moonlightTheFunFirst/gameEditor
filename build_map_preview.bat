@echo off
setlocal
cd /d "%~dp0"

cmake -S src\GameEditor.MapRuntime -B Output\MapRuntime
if errorlevel 1 exit /b %errorlevel%

cmake --build Output\MapRuntime --config Release
exit /b %errorlevel%
