# Новый IP VPS → сервер + Windows-рубильник

Старый IP мог попасть под блокировку. После покупки **дополнительного IP** у хостера:

## Два IP на одном VPS

- **Основной для VPN:** `31.77.188.248`
- **Запасной:** `31.77.188.247`
- **Старый `2.27.209.196`** — только для SSH в панели, для клиента **не использовать** (мог быть в блок-листе).

На сервере оба новых IP уже повешены на `ens3`. Xray слушает `0.0.0.0` — один и тот же ключ на обоих адресах.

**RusakbGate:** в `profile.json` поля `serverHost` + `serverHostBackup`. Xray **observatory** сам выбирает живой IP (leastPing).

Deploy:

```powershell
.\deploy_new_ip.ps1 -ConnectHost 2.27.209.196 -SshPort 2222 -PrimaryHost 31.77.188.248 -BackupHost 31.77.188.247
```

## 1. Привязать IP у serv.host

- В панели VPS назначь **новый публичный IP** (или подними второй интерфейс — как даёт хостер).
- Проверь с ПК: `ping НОВЫЙ_IP` (ICMP может не отвечать — нормально).
- SSH: `ssh root@НОВЫЙ_IP` (порт 22 или 2222, если уже настраивали).

## 2. Развернуть сервер одной командой с Windows

```powershell
cd D:\Projects\РУСАКБ\scripts\novavpn
.\deploy_new_ip.ps1 -VpsHost НОВЫЙ_IP -SshPort 22 -User root
```

Скрипт зальёт `server_install.sh`, поднимет Xray (443/2053/8443), MTProxy (2087), UFW, BBR.  
Секреты и **profile для приложения** сохранятся в:

`%USERPROFILE%\Downloads\rusakb-gate-profile.json`

## 3. Собрать Windows-приложение (один раз)

1. Установи [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Скачай в `clients\rusakb-gate\bin\` (см. `clients\rusakb-gate\bin\README.txt`):
   - `xray.exe`, `sing-box.exe`, `wintun.dll`
   - `geoip.dat`, `geosite.dat`
3. Сборка:

```powershell
cd D:\Projects\РУСАКБ\clients\rusakb-gate
.\build.ps1
```

Готовый exe: `clients\rusakb-gate\publish\RusakbGate.exe`

## 4. Первый запуск

1. Скопируй `rusakb-gate-profile.json` в  
   `%LOCALAPPDATA%\RusakbGate\profile.json`
2. Запусти **RusakbGate** от администратора (UAC).
3. Рубильник **Вкл** — весь трафик через VPN (кроме локалки и маршрута до IP сервера мимо туннеля).
4. Проверка: в приложении «Проверка IP» или `curl https://ifconfig.me` → IP сервера.

## Зачем своё приложение

Happ / v2rayNG / Amnezia — известные сигнатуры; их режут DPI и блокируют IP.  
**RusakbGate** — свой exe, свои порты (10818/10819), Reality xHTTP как обычный HTTPS — не завязан на чужой бренд.

## Дальше (по промпту)

- Трей, автозапуск `schtasks`, сплит `.ru` / ИИ, OTA — после того как рубильник стабильно работает на новом IP.
