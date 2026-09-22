# Доступ агента Cursor к GitHub (без браузера каждый раз)

GitHub не пускает «просто так» — нужен **Personal Access Token (PAT)** или SSH-ключ.  
Токен хранится **только у тебя на ПК**, в git не попадает.

## 1. Создай токен на GitHub

1. [github.com/settings/tokens](https://github.com/settings/tokens) → **Generate new token** (classic или fine-grained).
2. Права минимум:
   - **classic:** `repo` (для приватного `rusakb-gate`)
   - **fine-grained:** репозиторий `rusakb-gate`, Contents read/write, Metadata read
3. Скопируй токен (показывается один раз).

## 2. Сохрани токен локально (выбери один способ)

### Способ A — файл (удобно для агента в этом проекте)

```powershell
cd D:\Projects\РУСАКБ\clients\rusakb-gate
.\setup_github_token.ps1
```

Вставь токен в окно — он запишется в:

`%USERPROFILE%\.config\rusakb-gate\github.token`

(права только для твоего пользователя Windows)

### Способ B — переменная среды (надёжнее для всех программ)

1. Параметры Windows → **Переменные среды** → для пользователя:
   - имя: `GH_TOKEN`
   - значение: `ghp_...`
2. **Полностью закрой и открой Cursor** (чтобы подхватил env).

Можно дублировать как `RUSAKB_GH_TOKEN` — скрипты читают оба.

## 3. Один раз: войти в gh и создать репо

```powershell
cd D:\Projects\РУСАКБ\clients\rusakb-gate
.\push_github.ps1
```

Если репозиторий уже есть — дальше только `git push`.

## 4. Что писать агенту в чате

После настройки токена:

> «Залей изменения rusakb-gate на GitHub»

Агент вызовет `push_github.ps1` или `git push` — **без** `gh auth login` в браузере.

## Безопасность

- Токен **не** отправляй в чат Cursor и **не** коммить в репозиторий.
- Утечка → GitHub → revoke token → новый + `setup_github_token.ps1` снова.
- Репозиторий лучше держать **Private** (ключи VPN в ZIP остаются у тебя, не в git).

## Если push падает

```powershell
& "C:\Program Files\GitHub CLI\gh.exe" auth status
cd D:\Projects\РУСАКБ\clients\rusakb-gate
git remote -v
git push -u origin main
```

Ошибка 403 — неверный или просроченный токен.  
Ошибка «repository not found» — создай репо через `.\push_github.ps1` или вручную на GitHub.
