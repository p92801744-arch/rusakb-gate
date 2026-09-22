# Writes fresh client configs (UTF-8 no BOM) into %LOCALAPPDATA%\RusakbGate
$ErrorActionPreference = "Stop"
$profilePath = "$env:LOCALAPPDATA\RusakbGate\profile.json"
if (-not (Test-Path $profilePath)) {
    Copy-Item "$env:USERPROFILE\Downloads\rusakb-gate-profile.json" $profilePath -Force
}
$p = Get-Content $profilePath -Raw | ConvertFrom-Json
$hosts = @($p.serverHost)
if ($p.serverHostBackup) { $hosts += $p.serverHostBackup }

$exclude = @(
    "192.168.0.0/16", "10.0.0.0/8", "172.16.0.0/12",
    "127.0.0.0/8", "169.254.0.0/16", "224.0.0.0/4"
) + ($hosts | ForEach-Object { "$_/32" })

$directRules = $hosts | ForEach-Object { @{ ip_cidr = @("$_/32"); outbound = "direct" } }

$sb = @{
    log = @{ level = "info"; timestamp = $true }
    dns = @{
        servers = @(
            @{ type = "udp"; tag = "dns-remote"; server = "1.1.1.1"; detour = "proxy" },
            @{ type = "local"; tag = "dns-local" }
        )
        final = "dns-remote"
        strategy = "ipv4_only"
    }
    inbounds = @(
        @{
            type = "tun"
            tag = "tun-in"
            interface_name = "RusakbGate"
            address = @("10.0.85.1/24")
            mtu = 9000
            stack = "system"
            auto_route = $true
            strict_route = $true
            route_exclude_address = $exclude
        }
    )
    outbounds = @(
        @{ type = "socks"; tag = "proxy"; server = "127.0.0.1"; server_port = [int]$p.socksPort },
        @{ type = "direct"; tag = "direct" }
    )
    route = @{
        rules = $directRules
        final = "proxy"
        default_domain_resolver = @{ server = "dns-local" }
    }
}

function Out-Utf8NoBom($path, $obj) {
    $json = $obj | ConvertTo-Json -Depth 12 -Compress:$false
    [System.IO.File]::WriteAllText($path, $json, (New-Object System.Text.UTF8Encoding $false))
}

$work = Split-Path $profilePath -Parent
Out-Utf8NoBom (Join-Path $work "sing-box.json") $sb

# xray: primary host only (stable), backup as second outbound without observatory
$out443 = @{
    protocol = "vless"
    tag = "proxy443"
    settings = @{
        vnext = @(@{
            address = $p.serverHost
            port = [int]$p.xhttpPrimary
            users = @(@{ id = $p.uuid; encryption = "none" })
        })
    }
    streamSettings = @{
        network = "xhttp"
        security = "reality"
        xhttpSettings = @{
            path = "/"
            mode = "stream-one"
            xPaddingBytes = "100-1000"
            xmux = @{
                maxConcurrency = 48
                cMaxReuseTimes = 0
                hMaxRequestTimes = 0
                hMaxReusableSecs = 0
                hKeepAlivePeriod = 20
            }
        }
        realitySettings = @{
            serverName = $p.sni
            fingerprint = "firefox"
            publicKey = $p.publicKey
            shortId = $p.shortId
            spiderX = "/"
        }
    }
}

$out2053 = @{
    protocol = "vless"
    tag = "proxy2053"
    settings = @{
        vnext = @(@{
            address = $p.serverHost
            port = [int]$p.xhttpBackup
            users = @(@{ id = $p.uuid; encryption = "none" })
        })
    }
    streamSettings = $out443.streamSettings
}

if ($p.serverHostBackup) {
    $out443b = $out443.Clone()
    $out443b.tag = "proxy443-backup"
    $out443b.settings.vnext[0].address = $p.serverHostBackup
    $outbounds = @(
        @{ protocol = "freedom"; tag = "direct" },
        $out443,
        $out443b,
        $out2053
    )
    $routing = @{
        domainStrategy = "AsIs"
        rules = @(
            @{ type = "field"; inboundTag = @("socks-in", "http-in"); outboundTag = "proxy443" },
            @{ type = "field"; inboundTag = @("socks-in", "http-in"); outboundTag = "proxy443-backup"; network = "tcp,udp" }
        )
    }
} else {
    $outbounds = @(@{ protocol = "freedom"; tag = "direct" }, $out443, $out2053)
    $routing = @{
        domainStrategy = "AsIs"
        rules = @(@{ type = "field"; inboundTag = @("socks-in", "http-in"); outboundTag = "proxy443" })
    }
}

$xray = @{
    log = @{ loglevel = "warning" }
    inbounds = @(
        @{ tag = "socks-in"; listen = "127.0.0.1"; port = [int]$p.socksPort; protocol = "socks"; settings = @{ udp = $true } },
        @{ tag = "http-in"; listen = "127.0.0.1"; port = [int]$p.httpPort; protocol = "http" }
    )
    outbounds = $outbounds
    routing = $routing
}

Out-Utf8NoBom (Join-Path $work "xray-client.json") $xray
Write-Host "OK: $work"
