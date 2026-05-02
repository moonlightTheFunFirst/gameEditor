@echo off
setlocal
cd /d "%~dp0"

if exist Output rmdir /S /Q Output
if errorlevel 1 exit /b %errorlevel%

dotnet restore src\GameEditor.App\GameEditor.App.csproj -r win-x64
if errorlevel 1 exit /b %errorlevel%

dotnet publish src\GameEditor.App\GameEditor.App.csproj -c Release -r win-x64 --self-contained false --no-restore -o Output
if errorlevel 1 exit /b %errorlevel%

echo Output\gameEditor.exe
exit /b 0
