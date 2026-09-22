#!/bin/bash
set -euo pipefail
CFG=/opt/novavpn/xray/config.json
SECRETS=/opt/novavpn/secrets.env
source "${SECRETS}"

DEST="www.googletagmanager.com:443"
SNI="www.googletagmanager.com"

python3 <<PY
import json
with open("${CFG}") as f:
    c = json.load(f)
c["log"]["loglevel"] = "debug"
for ib in c["inbounds"]:
    rs = ib.get("streamSettings", {}).get("realitySettings")
    if rs:
        rs["dest"] = "${DEST}"
        rs["serverNames"] = ["${SNI}"]
        rs["shortIds"] = ["${NOVAVPN_SHORT_ID}", ""]
        rs.pop("minClientVer", None)
        rs.pop("maxClientVer", None)
with open("${CFG}", "w") as f:
    json.dump(c, f, indent=2)
PY

/usr/local/bin/xray -test -config "${CFG}"
systemctl restart novavpn-xray
sleep 1
systemctl is-active novavpn-xray

PUB="${NOVAVPN_REALITY_PUBLIC}"
UUID="${NOVAVPN_UUID}"
SID="${NOVAVPN_SHORT_ID}"
HOST="${NOVAVPN_SERVER_IP}"

VISION="vless://${UUID}@${HOST}:8443?encryption=none&flow=xtls-rprx-vision&security=reality&sni=${SNI}&fp=firefox&pbk=${PUB}&sid=${SID}&type=tcp#NovaVPN-Vision8443"
XHTTP443="vless://${UUID}@${HOST}:443?encryption=none&security=reality&sni=${SNI}&fp=firefox&pbk=${PUB}&sid=${SID}&type=xhttp&path=%2F&mode=stream-one&host=${SNI}#NovaVPN-xHTTP443"
XHTTP2053="vless://${UUID}@${HOST}:2053?encryption=none&security=reality&sni=${SNI}&fp=firefox&pbk=${PUB}&sid=${SID}&type=xhttp&path=%2F&mode=stream-one&host=${SNI}#NovaVPN-xHTTP2053"

echo "NOVAVPN_PHONE_VISION=${VISION}"
echo "NOVAVPN_PHONE_XHTTP443=${XHTTP443}"
echo "NOVAVPN_PHONE_XHTTP2053=${XHTTP2053}"
echo "FIX_OK"
