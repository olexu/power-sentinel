#!/usr/bin/env bash
set -euo pipefail

HEARTBEAT_URL="https://your.server/heartbeat?deviceId=deviceId"
HEARTBEAT_KEY="HeartbeatKey"
INTERVAL_SECONDS="10"

headers=( -H "Heartbeat-Key: $HEARTBEAT_KEY" )

while true; do
  if ! curl -fsS -X POST "${headers[@]}" "$HEARTBEAT_URL"; then
    echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] curl POST failed" >&2
  fi
  sleep "$INTERVAL_SECONDS"
done
