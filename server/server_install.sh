#!/bin/bash
set -euo pipefail

APP_ROOT="/opt/novavpn"
XRAY_BIN="/usr/local/bin/xray"
CFG="${APP_ROOT}/xray/config.json"
LOG_DIR="${APP_ROOT}/logs"
SECRETS="${APP_ROOT}/secrets.env"

echo "[1/8] Cleanup Amnezia and old Xray..."
systemctl stop xray 2>/dev/null || true
systemctl disable xray 2>/dev/null || true
docker ps -aq 2>/dev/null | xargs -r docker stop
docker ps -aq 2>/dev/null | xargs -r docker rm
rm -rf /opt/amnezia
mkdir -p "${APP_ROOT}"/{xray,logs,bin,www,ota}

echo "[2/8] Install / update Xray..."
if [[ -x "${XRAY_BIN}" ]]; then
  echo "Xray already installed: $("${XRAY_BIN}" version | head -1)"
else
  VER="$(curl -fsSL https://api.github.com/repos/XTLS/Xray-core/releases/latest | sed -n 's/.*"tag_name": "\(v[^"]*\)".*/\1/p' | head -1)"
  [[ -z "${VER}" ]] && VER="v26.3.27"
  ARCH="linux-64"
  TMP="/tmp/xray-${VER}.zip"
  curl -fsSL -o "${TMP}" "https://github.com/XTLS/Xray-core/releases/download/${VER}/Xray-${ARCH}.zip"
  rm -rf /tmp/xray-unpack && mkdir -p /tmp/xray-unpack
  unzip -o "${TMP}" xray -d /tmp/xray-unpack
  install -m 755 /tmp/xray-unpack/xray "${XRAY_BIN}"
  "${XRAY_BIN}" version | head -1
fi

echo "[3/8] Generate secrets..."
UUID="$(cat /proc/sys/kernel/random/uuid)"
KEYS="$("${XRAY_BIN}" x25519)"
PRIV="$(echo "${KEYS}" | awk -F': ' '/PrivateKey/ {print $2; exit}')"
PUB="$(echo "${KEYS}" | awk -F': ' '/PublicKey/ {print $2; exit}')"
[[ -z "${PRIV}" || -z "${PUB}" ]] && { echo "x25519 parse failed"; exit 1; }
SHORT_ID="$(openssl rand -hex 8)"
MT_SECRET="dd$(openssl rand -hex 15)"

mkdir -p "${APP_ROOT}/ota"
if [[ ! -f "${APP_ROOT}/ota/sign.key" ]]; then
  openssl ecparam -name prime256v1 -genkey -noout -out "${APP_ROOT}/ota/sign.key"
  openssl ec -in "${APP_ROOT}/ota/sign.key" -pubout -outform DER \
    | openssl base64 -A > "${APP_ROOT}/ota/sign.spki.b64"
fi

cat > "${SECRETS}" <<EOF
NOVAVPN_UUID=${UUID}
NOVAVPN_REALITY_PRIVATE=${PRIV}
NOVAVPN_REALITY_PUBLIC=${PUB}
NOVAVPN_SHORT_ID=${SHORT_ID}
NOVAVPN_MTPROTO_SECRET=${MT_SECRET}
NOVAVPN_SERVER_IP=$(curl -4fsS ifconfig.me 2>/dev/null || hostname -I | awk '{print $1}')
EOF
chmod 600 "${SECRETS}"

echo "[4/8] Write Xray config..."
cat > "${CFG}" <<EOF
{
  "log": {
    "access": "${LOG_DIR}/access.log",
    "error": "${LOG_DIR}/error.log",
    "loglevel": "warning"
  },
  "dns": {
    "servers": ["1.1.1.1", "8.8.8.8"],
    "queryStrategy": "UseIPv4"
  },
  "inbounds": [
    {
      "tag": "vless-xhttp-443",
      "listen": "0.0.0.0",
      "port": 443,
      "protocol": "vless",
      "settings": {
        "clients": [{"id": "${UUID}", "email": "pc-main"}],
        "decryption": "none"
      },
      "streamSettings": {
        "network": "xhttp",
        "security": "reality",
        "xhttpSettings": {"path": "/", "mode": "auto"},
        "realitySettings": {
          "show": false,
          "dest": "www.apple.com:443",
          "xver": 0,
          "serverNames": ["www.apple.com"],
          "privateKey": "${PRIV}",
          "shortIds": ["${SHORT_ID}"],
          "minClientVer": "1.8.0",
          "maxClientVer": "99.0.0"
        }
      },
      "sniffing": {"enabled": true, "destOverride": ["http", "tls", "quic"]}
    },
    {
      "tag": "vless-xhttp-2053",
      "listen": "0.0.0.0",
      "port": 2053,
      "protocol": "vless",
      "settings": {
        "clients": [{"id": "${UUID}", "email": "pc-backup"}],
        "decryption": "none"
      },
      "streamSettings": {
        "network": "xhttp",
        "security": "reality",
        "xhttpSettings": {"path": "/", "mode": "auto"},
        "realitySettings": {
          "show": false,
          "dest": "www.apple.com:443",
          "xver": 0,
          "serverNames": ["www.apple.com"],
          "privateKey": "${PRIV}",
          "shortIds": ["${SHORT_ID}"],
          "minClientVer": "1.8.0",
          "maxClientVer": "99.0.0"
        }
      },
      "sniffing": {"enabled": true, "destOverride": ["http", "tls", "quic"]}
    },
    {
      "tag": "vless-vision-8443",
      "listen": "0.0.0.0",
      "port": 8443,
      "protocol": "vless",
      "settings": {
        "clients": [{"id": "${UUID}", "email": "phone-vision", "flow": "xtls-rprx-vision"}],
        "decryption": "none"
      },
      "streamSettings": {
        "network": "tcp",
        "security": "reality",
        "realitySettings": {
          "show": false,
          "dest": "www.apple.com:443",
          "xver": 0,
          "serverNames": ["www.apple.com"],
          "privateKey": "${PRIV}",
          "shortIds": ["${SHORT_ID}"],
          "minClientVer": "1.8.0",
          "maxClientVer": "99.0.0"
        }
      },
      "sniffing": {"enabled": true, "destOverride": ["http", "tls", "quic"]}
    }
  ],
  "outbounds": [
    {"protocol": "freedom", "tag": "direct", "settings": {"domainStrategy": "UseIPv4"}},
    {"protocol": "blackhole", "tag": "block"}
  ],
  "routing": {
    "domainStrategy": "AsIs",
    "rules": [
      {"type": "field", "ip": ["::/0"], "outboundTag": "block"}
    ]
  }
}
EOF

"${XRAY_BIN}" -test -config "${CFG}"

echo "[5/8] systemd + logrotate..."
cat > /etc/systemd/system/novavpn-xray.service <<EOF
[Unit]
Description=NovaVPN Xray
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
ExecStart=${XRAY_BIN} run -config ${CFG}
Restart=on-failure
RestartSec=3
LimitNOFILE=1048576

[Install]
WantedBy=multi-user.target
EOF

cat > /etc/logrotate.d/novavpn-xray <<EOF
${LOG_DIR}/*.log {
  daily
  rotate 14
  compress
  missingok
  notifempty
  copytruncate
}
EOF

systemctl daemon-reload
systemctl enable novavpn-xray
systemctl restart novavpn-xray
sleep 1
systemctl is-active novavpn-xray

echo "[6/8] sysctl tuning..."
cat > /etc/sysctl.d/99-novavpn.conf <<'SYS'
net.core.default_qdisc=fq
net.ipv4.tcp_congestion_control=bbr
net.ipv4.tcp_fastopen=3
net.core.rmem_max=16777216
net.core.wmem_max=16777216
net.ipv4.ip_forward=1
SYS
sysctl --system >/dev/null

echo "[7/8] UFW..."
for p in 40000 31353 500 4500; do
  ufw delete allow "${p}/tcp" 2>/dev/null || true
  ufw delete allow "${p}/udp" 2>/dev/null || true
done
for p in 443 2053 8443 2087 8080; do
  ufw allow "${p}/tcp" comment NovaVPN
done
ufw status | head -25

echo "[8/8] MTProto (dd secret, official image)..."
docker rm -f novavpn-mtproto 2>/dev/null || true
docker run -d --name novavpn-mtproto --restart unless-stopped \
  -p 2087:443 \
  -e SECRET="${MT_SECRET}" \
  telegrammessenger/proxy:latest || echo "MTProto docker failed (optional)"

echo "=== PORTS ==="
ss -tlnp | egrep ':443|:2053|:8443|:2087|:8080|:22' || true

echo "=== SECRETS (save locally) ==="
cat "${SECRETS}"
echo "NOVAVPN_OTA_SPKI=$(cat ${APP_ROOT}/ota/sign.spki.b64)"

# Phone VLESS vision link
HOST="$(grep NOVAVPN_SERVER_IP "${SECRETS}" | cut -d= -f2)"
echo "NOVAVPN_PHONE_VLESS=vless://${UUID}@${HOST}:8443?encryption=none&flow=xtls-rprx-vision&security=reality&sni=www.apple.com&fp=firefox&pbk=${PUB}&sid=${SHORT_ID}&type=tcp#NovaVPN-Phone"
echo "NOVAVPN_TG_PROXY=tg://proxy?server=${HOST}&port=2087&secret=${MT_SECRET}"
echo "INSTALL_OK"
