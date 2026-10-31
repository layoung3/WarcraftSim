#!/usr/bin/env bash
set -euo pipefail
mkdir -p /tmp/cp174-ui-captures
BROWSER="$(command -v google-chrome || command -v chromium || command -v chromium-browser || true)"
if [[ -z "$BROWSER" ]]; then
  echo "No Chromium-compatible browser available in CI image; screenshots cannot be captured here."
  exit 0
fi
ASPNETCORE_URLS=http://127.0.0.1:5142 dotnet run --project WarcraftSim.Api -c Release --no-build --no-launch-profile >/tmp/cp174-api.log 2>&1 &
SERVER_PID=$!
trap 'kill "$SERVER_PID" 2>/dev/null || true' EXIT
READY=0
for i in $(seq 1 40); do
  if curl --fail --silent http://127.0.0.1:5142/solo-dps.html >/dev/null; then READY=1; break; fi
  sleep 1
done
if [[ "$READY" -ne 1 ]]; then cat /tmp/cp174-api.log; exit 1; fi
for VIEW in desktop laptop tablet mobile desktop-talents desktop-effects laptop-talents; do
  PAGE=equipment
  case "$VIEW" in
    desktop) SIZE=1920,1080;;
    laptop) SIZE=1440,900;;
    tablet) SIZE=768,1024;;
    mobile) SIZE=390,844;;
    desktop-talents) SIZE=1920,1080; PAGE=talents;;
    desktop-effects) SIZE=1920,1080; PAGE=effects;;
    laptop-talents) SIZE=1440,900; PAGE=talents;;
  esac
  timeout 40s "$BROWSER" --headless --no-sandbox --disable-gpu --disable-dev-shm-usage \
    --window-size="$SIZE" --force-device-scale-factor=1 --virtual-time-budget=6000 \
    --screenshot="/tmp/cp174-ui-captures/${VIEW}.png" "http://127.0.0.1:5142/solo-dps.html?view=${PAGE}" >/tmp/cp174-chrome-"$VIEW".log 2>&1
  test -s "/tmp/cp174-ui-captures/${VIEW}.png"
done
echo "Captured CP174 screenshots using $BROWSER at 1920, 1440, 768, 390 widths plus talent/effects tabs."
