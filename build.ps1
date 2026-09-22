$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "Установи .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
}

dotnet publish RusakbGate.csproj -c Release -r win-x64 `
    -p:PublishSingleFile=true `
    -p:SelfContained=true `
    -o publish

if (Test-Path bin) {
    New-Item -ItemType Directory -Force -Path publish\bin | Out-Null
    Copy-Item -Path bin\* -Destination publish\bin -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Готово: $PSScriptRoot\publish\RusakbGate.exe"
Write-Host "Добавь xray.exe / sing-box.exe / wintun.dll в publish\bin если ещё не копировал."
