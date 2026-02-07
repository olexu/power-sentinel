#!/usr/bin/env bash
set -euo pipefail

DEVICE_ID="Device"
DEVICE_DESCRIPTION="Device"
HEARTBEAT_URL="https://your.server/heartbeat"
REQUEST_TOKEN="HeartbeatToken"
INTERVAL_SECONDS="10"

headers=( -H "Content-Type: application/json" -H "Request-Token: $REQUEST_TOKEN" )

payload=$(printf '{"deviceId":"%s","description":"%s"}' "$DEVICE_ID" "$DEVICE_DESCRIPTION")

while true; do
  if ! curl -fsS -X POST "${headers[@]}" -d "$payload" "$HEARTBEAT_URL"; then
    echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] curl POST failed" >&2
  fi
  sleep "$INTERVAL_SECONDS"
done
