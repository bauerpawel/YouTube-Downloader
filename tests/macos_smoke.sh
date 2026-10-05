#!/usr/bin/env bash
set -euo pipefail

publish_dir=${1:?Usage: bash tests/macos_smoke.sh <publish-directory>}
smoke_dir=$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/ytd-macos-smoke.XXXXXX")
data_dir="$HOME/Library/Application Support/YouTubeDownloader"
app_pid=''
cleanup() {
  if [ -n "$app_pid" ]; then kill "$app_pid" 2>/dev/null || true; fi
  rm -rf "$smoke_dir"
}
trap cleanup EXIT

fail() {
  printf '%s\n' 'macOS startup did not verify all required components.'
  if [ -f "$smoke_dir/app.log" ]; then
    tail -80 "$smoke_dir/app.log"
    if [ "${GITHUB_ACTIONS:-}" = true ]; then
      python3 - "$smoke_dir/app.log" <<'PY'
from pathlib import Path
import sys
message = '\n'.join(Path(sys.argv[1]).read_text(errors='replace').splitlines()[-80:])
message = message.replace('%', '%25').replace('\r', '%0D').replace('\n', '%0A')
print('::error title=macOS component startup::' + message)
PY
    fi
  fi
  exit 1
}

cp -R "$publish_dir/." "$smoke_dir/"
chmod +x "$smoke_dir/YouTubeDownloader"
"$smoke_dir/YouTubeDownloader" > "$smoke_dir/app.log" 2>&1 &
app_pid=$!
for attempt in $(seq 1 36); do
  sleep 5
  kill -0 "$app_pid" 2>/dev/null || fail
  if [ -x "$data_dir/yt-dlp" ] && [ -x "$data_dir/ffmpeg_bin/ffmpeg" ] && [ -x "$data_dir/ffmpeg_bin/ffprobe" ] &&
     grep -Eq '\[status\] (All components are available|Wszystkie komponenty są dostępne)' "$smoke_dir/app.log"; then
    cat "$smoke_dir/app.log"
    printf '%s\n' 'macOS startup verified all required components.'
    exit 0
  fi
done
fail
