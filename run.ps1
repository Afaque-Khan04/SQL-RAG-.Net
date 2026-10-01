$dotnet8Path = "$env:LOCALAPPDATA\Microsoft\dotnet"
if (Test-Path "$dotnet8Path\dotnet.exe") {
    $env:DOTNET_ROOT = $dotnet8Path
    $env:PATH = "$dotnet8Path;$env:PATH"
}

$dotnetExe = "$dotnet8Path\dotnet.exe"
if (-not (Test-Path $dotnetExe)) {
    $dotnetExe = "dotnet"
}

# Ensure port 5000 is free by stopping any lingering API instances or bound processes
Get-Process -Name "AdhocSystem.Api" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | ForEach-Object {
    Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue
}

Write-Host "Starting AdvRAG .NET 8 Web API & React Studio..." -ForegroundColor Green
Set-Location -Path "$PSScriptRoot\src\AdhocSystem.Api"
& $dotnetExe run --launch-profile http
