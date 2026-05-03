@echo off
setlocal
cd /d "%~dp0"

dotnet restore gameEditor.sln
if errorlevel 1 exit /b %errorlevel%

dotnet build gameEditor.sln -c Release --no-restore
if errorlevel 1 exit /b %errorlevel%

dotnet run --project src\GameEditor.App\GameEditor.App.csproj -c Release --no-build
exit /b %errorlevel%
