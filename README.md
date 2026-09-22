# RusakbGate

Личный Windows-VPN клиент (рубильник ВКЛ/ВЫКЛ): WPF + xray + sing-box TUN.

Серверная часть — в папке `server/` (Xray Reality xHTTP на VPS).

## Сборка (Windows)

1. [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Ядра в `bin/`:

   ```powershell
   .\fetch_cores.ps1
   ```

3. Профиль (ключи **не в git**):

   ```powershell
   copy $env:USERPROFILE\Downloads\rusakb-gate-profile.json $env:LOCALAPPDATA\RusakbGate\profile.json
   ```

   Образец полей: `profile.example.json`

4. Сборка:

   ```powershell
   .\build.ps1
   ```

   Exe: `publish\RusakbGate.exe` или `publish_new\` после параллельной сборки.

## Установочный ZIP для других ПК

```powershell
.\pack_setup.ps1
```

Архив: `%USERPROFILE%\Downloads\RusakbGate-Setup.zip` — распаковать, `INSTALL.cmd`.

## Сервер (новый IP VPS)

```powershell
cd server
.\deploy_new_ip.ps1 -ConnectHost SSH_HOST -SshPort 2222 -PrimaryHost VPN_IP -BackupHost VPN_IP2
```

Подробнее: `docs/new-ip-playbook.md`

## GitHub — первый push

```powershell
cd D:\Projects\РУСАКБ\clients\rusakb-gate
git init
git add .
git commit -m "Initial RusakbGate client and server scripts"
```

На [github.com/new](https://github.com/new) создай **приватный** репозиторий `rusakb-gate` (без README).

```powershell
git branch -M main
git remote add origin https://github.com/ТВОЙ_ЛОГИН/rusakb-gate.git
git push -u origin main
```

Дальнейшие обновления:

```powershell
git add .
git commit -m "описание изменения"
git push
```

После обновления кода на машине с SDK снова `.\pack_setup.ps1` и раздай новый ZIP.

## Нельзя коммитить

- `profile.json` с реальными UUID/ключами
- `bin/*.exe`, `publish*/`, `*.zip`
- логи и `%LOCALAPPDATA%` копии
