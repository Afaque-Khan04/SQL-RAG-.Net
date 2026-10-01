@echo off
set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"

powershell -NoProfile -Command "Get-Process -Name 'AdhocSystem.Api' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue; Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }"
cd /d "%~dp0src\AdhocSystem.Api"
"%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" run --launch-profile http
