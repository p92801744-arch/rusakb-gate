# Скачивает ядра в publish\bin (нужен интернет; GitHub иногда не открывается — см. README)
$ErrorActionPreference = "Stop"
$Bin = Join-Path $PSScriptRoot "publish\bin"
New-Item -ItemType Directory -Force -Path $Bin | Out-Null

function Get-LatestAssetUrl {
    param([string]$Repo, [string]$NamePattern)
    $rel = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/latest" -Headers @{ "User-Agent" = "RusakbGate" }
    $asset = $rel.assets | Where-Object { $_.name -match $NamePattern } | Select-Object -First 1
    if (-not $asset) { throw "Asset not found: $Repo $NamePattern" }
    return $asset.browser_download_url, $asset.name
}

Write-Host "Xray..."
$url, $zipName = Get-LatestAssetUrl "XTLS/Xray-core" "windows-64\.zip$"
$zip = Join-Path $env:TEMP $zipName
Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
Expand-Archive -Force $zip -DestinationPath (Join-Path $env:TEMP "xray-unpack")
Copy-Item -Force (Join-Path $env:TEMP "xray-unpack\xray.exe") $Bin
Copy-Item -Force (Join-Path $env:TEMP "xray-unpack\geoip.dat") $Bin -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $env:TEMP "xray-unpack\geosite.dat") $Bin -ErrorAction SilentlyContinue

Write-Host "sing-box..."
$url, $zipName = Get-LatestAssetUrl "SagerNet/sing-box" "windows-amd64\.zip$"
$zip = Join-Path $env:TEMP $zipName
Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
Expand-Archive -Force $zip -DestinationPath (Join-Path $env:TEMP "sb-unpack")
$sb = Get-ChildItem (Join-Path $env:TEMP "sb-unpack") -Recurse -Filter "sing-box.exe" | Select-Object -First 1
Copy-Item -Force $sb.FullName $Bin
$wt = Get-ChildItem (Join-Path $env:TEMP "sb-unpack") -Recurse -Filter "wintun.dll" | Select-Object -First 1
if ($wt) { Copy-Item -Force $wt.FullName $Bin }

Write-Host "Done:"
Get-ChildItem $Bin | Format-Table Name, Length
