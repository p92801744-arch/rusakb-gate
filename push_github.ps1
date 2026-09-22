# Create repo (once) and push. Uses GH_TOKEN / RUSAKB_GH_TOKEN or ~/.config/rusakb-gate/github.token
$ErrorActionPreference = "Stop"
$gh = "C:\Program Files\GitHub CLI\gh.exe"
Set-Location $PSScriptRoot

function Get-GhToken {
    if ($env:GH_TOKEN) { return $env:GH_TOKEN.Trim() }
    if ($env:RUSAKB_GH_TOKEN) { return $env:RUSAKB_GH_TOKEN.Trim() }
    $f = Join-Path $env:USERPROFILE ".config\rusakb-gate\github.token"
    if (Test-Path $f) { return (Get-Content $f -Raw).Trim() }
    return $null
}

$token = Get-GhToken
if (-not $token) {
    Write-Host "No token. Run: .\setup_github_token.ps1"
    Write-Host "Or set user env GH_TOKEN and restart Cursor."
    exit 1
}

$env:GH_TOKEN = $token
$status = & $gh auth status 2>&1 | Out-String
if ($status -notmatch 'Logged in') {
    $token | & $gh auth login --hostname github.com --git-protocol https --with-token 2>$null
}

& $gh auth status
if ($LASTEXITCODE -ne 0) { exit 1 }

git -c user.name="Rusakb" -c user.email="rusakb@users.noreply.github.com" status -sb | Out-Host

$remote = git remote get-url origin 2>$null
if (-not $remote) {
    & $gh repo create rusakb-gate --private `
        --description "RusakbGate VPN Windows client" `
        --source=. --remote=origin --push
} else {
    git push -u origin main
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "Repo:" (& $gh repo view -q url)
}
