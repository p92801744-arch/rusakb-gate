param(
    [Parameter(Mandatory = $true)]
    [string] $ConnectHost,
    [string] $PrimaryHost = "",
    [string] $BackupHost = "",
    [int] $SshPort = 2222,
    [string] $User = "root",
    [string] $IdentityFile = "$env:USERPROFILE\.ssh\rusakb_vps_ed25519"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$InstallSh = Join-Path $PSScriptRoot "server_install.sh"
if (-not (Test-Path $InstallSh)) { throw "Missing $InstallSh" }

$sshArgs = @("-p", $SshPort, "-o", "ConnectTimeout=25", "-o", "BatchMode=yes")
if (Test-Path $IdentityFile) {
    $sshArgs += @("-i", $IdentityFile, "-o", "IdentitiesOnly=yes")
}

Write-Host "Deploy NovaVPN stack via ${User}@${ConnectHost}:${SshPort} ..."

$raw = Get-Content -Raw $InstallSh
$raw = $raw -replace "`r`n", "`n"
$raw | & ssh @sshArgs "${User}@${ConnectHost}" "cat > /tmp/novavpn_install.sh && sed -i 's/\r$//' /tmp/novavpn_install.sh && bash /tmp/novavpn_install.sh 2>&1"

if ($LASTEXITCODE -ne 0) { throw "Remote install failed (exit $LASTEXITCODE)" }

Write-Host "Fetching client profile ..."
$secrets = & ssh @sshArgs "${User}@${ConnectHost}" "cat /opt/novavpn/secrets.env; echo NOVAVPN_OTA_SPKI=\$(cat /opt/novavpn/ota/sign.spki.b64 2>/dev/null)"
$map = @{}
foreach ($line in ($secrets -split "`n")) {
    if ($line -match '^([A-Z_]+)=(.*)$') { $map[$Matches[1]] = $Matches[2].Trim() }
}

if (-not $PrimaryHost) { $PrimaryHost = "31.77.188.248" }
if (-not $BackupHost) { $BackupHost = "31.77.188.247" }

$profile = [ordered]@{
    appName          = "RusakbGate"
    serverHost       = $PrimaryHost
    serverHostBackup = $BackupHost
    uuid             = $map["NOVAVPN_UUID"]
    publicKey  = $map["NOVAVPN_REALITY_PUBLIC"]
    shortId    = $map["NOVAVPN_SHORT_ID"]
    sni        = "www.apple.com"
    socksPort  = 10818
    httpPort   = 10819
    xhttpPrimary = 443
    xhttpBackup  = 2053
}

$out = Join-Path $env:USERPROFILE "Downloads\rusakb-gate-profile.json"
$json = $profile | ConvertTo-Json -Depth 5
[System.IO.File]::WriteAllText($out, $json, (New-Object System.Text.UTF8Encoding $false))
Write-Host "Saved profile: $out"
Write-Host "Next: copy to %LOCALAPPDATA%\RusakbGate\profile.json and run RusakbGate.exe as Admin"
