# RusakbGate — установка на этот компьютер
# Запуск: правый клик -> "Запуск с PowerShell" или двойной клик INSTALL.cmd
#Needs admin only when you turn VPN ON (UAC), not for file copy.

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$AppSrc = Join-Path $Root "app"
$ProfileSrc = Join-Path $Root "profile.json"

if (-not (Test-Path (Join-Path $AppSrc "RusakbGate.exe"))) {
    Write-Host "Ошибка: рядом с INSTALL.ps1 должна быть папка app\ с RusakbGate.exe" -ForegroundColor Red
    pause
    exit 1
}
if (-not (Test-Path $ProfileSrc)) {
    Write-Host "Ошибка: нет profile.json в комплекте" -ForegroundColor Red
    pause
    exit 1
}

$Base = Join-Path $env:LOCALAPPDATA "RusakbGate"
$AppDst = Join-Path $Base "app"
New-Item -ItemType Directory -Force -Path $AppDst | Out-Null

Write-Host "Копирую в $AppDst ..."
Copy-Item -Path (Join-Path $AppSrc "*") -Destination $AppDst -Recurse -Force
Copy-Item -Force $ProfileSrc (Join-Path $Base "profile.json")

$exe = Join-Path $AppDst "RusakbGate.exe"
foreach ($f in @("xray.exe", "sing-box.exe", "wintun.dll")) {
    $p = Join-Path $AppDst "bin\$f"
    if (-not (Test-Path $p)) {
        Write-Host "Предупреждение: нет $p" -ForegroundColor Yellow
    }
}

$desk = [Environment]::GetFolderPath("Desktop")
$wsh = New-Object -ComObject WScript.Shell
$lnk = $wsh.CreateShortcut((Join-Path $desk "RusakbGate.lnk"))
$lnk.TargetPath = $exe
$lnk.WorkingDirectory = $AppDst
$lnk.Description = "RusakbGate VPN"
$lnk.Save()

Write-Host ""
Write-Host "Готово." -ForegroundColor Green
Write-Host "1) Выключи Happ / другие VPN на этом ПК."
Write-Host "2) Запусти ярлык RusakbGate -> Запуск от имени администратора."
Write-Host "3) ВКЛ -> Проверка IP."
Write-Host ""
Write-Host "Лог: $Base\gate.log"
pause
