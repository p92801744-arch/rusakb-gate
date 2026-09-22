param(
    [string] $ProfilePath = "$env:USERPROFILE\Downloads\rusakb-gate-profile.json",
    [string] $OutZip = "$env:USERPROFILE\Downloads\RusakbGate-Setup.zip"
)

$ErrorActionPreference = "Stop"
$Here = $PSScriptRoot
$Publish = Join-Path $Here "publish_new"
if (-not (Test-Path (Join-Path $Publish "RusakbGate.exe"))) {
    $Publish = Join-Path $Here "publish"
}
if (-not (Test-Path (Join-Path $Publish "RusakbGate.exe"))) {
    Write-Host "Build RusakbGate first (publish_new or publish folder)."
    exit 1
}

$bin = Join-Path $Publish "bin"
foreach ($f in @("xray.exe", "sing-box.exe", "wintun.dll")) {
    if (-not (Test-Path (Join-Path $bin $f))) {
        Write-Host "Missing $f - run fetch_cores.ps1"
        exit 1
    }
}

if (-not (Test-Path $ProfilePath)) {
    Write-Host "Missing profile: $ProfilePath"
    exit 1
}

$staging = Join-Path $env:TEMP "RusakbGate-Setup-staging"
if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
New-Item -ItemType Directory -Force -Path (Join-Path $staging "app") | Out-Null

Copy-Item -Force (Join-Path $Publish "RusakbGate.exe") (Join-Path $staging "app")
Copy-Item -Recurse -Force $bin (Join-Path $staging "app\bin")
Copy-Item -Force $ProfilePath (Join-Path $staging "profile.json")
Copy-Item -Force (Join-Path $Here "installer\INSTALL.ps1") $staging
Copy-Item -Force (Join-Path $Here "installer\INSTALL.cmd") $staging
Copy-Item -Force (Join-Path $Here "installer\README.txt") $staging

if (Test-Path $OutZip) { Remove-Item -Force $OutZip }
Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $OutZip -CompressionLevel Optimal

Remove-Item -Recurse -Force $staging
$size = [math]::Round((Get-Item $OutZip).Length / 1MB, 1)
Write-Host "OK: $OutZip ($size MB)"
