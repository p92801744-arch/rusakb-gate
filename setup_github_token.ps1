# Saves GitHub PAT locally for gh/git (never commit this token)
$ErrorActionPreference = "Stop"
$dir = Join-Path $env:USERPROFILE ".config\rusakb-gate"
$file = Join-Path $dir "github.token"
New-Item -ItemType Directory -Force -Path $dir | Out-Null

Write-Host "Paste GitHub token (ghp_... or github_pat_...), then Enter:"
$secure = Read-Host -AsSecureString
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $token = [Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
}

if ([string]::IsNullOrWhiteSpace($token)) { throw "Empty token" }
[System.IO.File]::WriteAllText($file, $token.Trim(), (New-Object System.Text.UTF8Encoding $false))

# Restrict to current user
icacls $file /inheritance:r /grant:r "$env:USERNAME`:F" | Out-Null

$gh = "C:\Program Files\GitHub CLI\gh.exe"
$token.Trim() | & $gh auth login --hostname github.com --git-protocol https --with-token

Write-Host "OK. Token file: $file"
Write-Host "Now run: .\push_github.ps1"
