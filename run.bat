@echo off
set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"

echo Starting AdvRAG .NET 8 Web API ^& React Studio...
powershell -Command "Get-Process -Name 'AdhocSystem.Api' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue"
cd /d "%~dp0src\AdhocSystem.Api"
"%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" run --launch-profile http
