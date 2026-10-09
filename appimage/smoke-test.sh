#!/bin/bash
# Smoke-tests a Linux AppImage the way a user runs it:
#  1. a renamed copy in a folder whose name has a space and Polish letters starts
#     through a real FUSE mount (not --appimage-extract-and-run): the runtime
#     mounts the image and execs the app in the same process
#  2. the app downloads and verifies yt-dlp and FFmpeg in the per-user data
#     folder and writes nothing next to the .AppImage
#  3. AppUpdater.cs's UnixSwapScript, run verbatim with the app's PID, replaces
#     the .AppImage only once the app has exited, then starts the new one
# Linux only (installs packages with sudo apt-get). Run by CI after
# appimage/package.sh; set YTD_GITHUB_TOKEN to avoid the anonymous GitHub API
# limit on shared runners.
#
# Usage: appimage/smoke-test.sh <AppImage>
set -euo pipefail

if [ $# -ne 1 ]; then
    echo "Usage: $0 <AppImage>" >&2
    exit 2
fi

HERE=$(cd "$(dirname "$0")" && pwd)
REPO=$(dirname "$HERE")
T=$(mktemp -d "${RUNNER_TEMP:-/tmp}/appimage-smoke.XXXXXX")
INSTALL="$T/Aplikacje ąę"
APP="$INSTALL/YTD test.AppImage"
LOG="$T/app.log"
# A fresh data folder: tools left by an earlier smoke test must not count.
export XDG_DATA_HOME="$T/data"
DATA_DIR="$XDG_DATA_HOME/YouTubeDownloader"
READY='\[status\] (All components are available|Wszystkie komponenty są dostępne)'

APP_PID=""
NEW_PID=""
XVFB_PID=""

cleanup() {
    for pid in $NEW_PID $APP_PID; do
        kill "$pid" 2>/dev/null || true
    done
    sleep 2
    for pid in $NEW_PID $APP_PID; do
        kill -KILL "$pid" 2>/dev/null || true
    done
    if [ -n "$XVFB_PID" ]; then
        kill "$XVFB_PID" 2>/dev/null || true
    fi
    rm -rf "$T" 2>/dev/null || true
}
trap cleanup EXIT

fail() {
    echo "FAIL: $*"
    echo "--- app.log"
    cat "$LOG" 2>/dev/null || true
    echo "--- files"
    ls -la "$INSTALL" "$DATA_DIR" "$DATA_DIR/ffmpeg_bin" 2>&1 || true
    echo "--- AppImage mounts"
    grep '\.mount_' /proc/mounts || true
    exit 1
}

# The exe of PID $1 lies on an AppImage mount: the runtime mounted the image
# through FUSE and exec'd AppRun -> the app.
runs_from_mount() {
    readlink "/proc/$1/exe" 2>/dev/null | grep -q '/\.mount_'
}

tools_ready() {
    [ -x "$DATA_DIR/yt-dlp" ] && [ -x "$DATA_DIR/ffmpeg_bin/ffmpeg" ] && [ -x "$DATA_DIR/ffmpeg_bin/ffprobe" ] &&
        grep -Eq "$READY" "$LOG"
}

echo "== 0. Setup"
sudo apt-get update
sudo apt-get install -y xvfb fuse3 libx11-6 libice6 libsm6 fontconfig fonts-dejavu-core
if [ ! -e /dev/fuse ]; then
    fail "/dev/fuse is missing - the AppImage cannot be mounted"
fi
mkdir -p "$INSTALL"
cp "$1" "$APP"
chmod +x "$APP"
# Own X server instead of xvfb-run, so $! below is the app's PID. Xvfb picks a
# free display itself (-displayfd): an earlier xvfb-run in the same job may still
# hold :99, and the app would then reach that server and fail its authorization.
Xvfb -displayfd 3 -screen 0 1280x1024x24 -ac 3> "$T/display" > /dev/null 2>&1 &
XVFB_PID=$!
for i in $(seq 1 20); do
    if [ -s "$T/display" ]; then
        break
    fi
    sleep 0.5
done
if [ ! -s "$T/display" ]; then
    fail "Xvfb did not start"
fi
export DISPLAY=":$(head -n 1 "$T/display")"
echo "Xvfb on $DISPLAY"

echo "== 1. Start through FUSE"
"$APP" > "$LOG" 2>&1 &
APP_PID=$!
for i in $(seq 1 30); do
    if runs_from_mount "$APP_PID"; then
        break
    fi
    if ! kill -0 "$APP_PID" 2>/dev/null; then
        fail "the AppImage exited at start"
    fi
    sleep 1
done
if ! runs_from_mount "$APP_PID"; then
    fail "the app does not run from an AppImage mount after 30 s"
fi
echo "app runs from $(readlink "/proc/$APP_PID/exe")"

echo "== 2. Tools verified in the data folder, nothing next to the AppImage"
# Up to 180 s, checked every 5 s - as in the plain Linux smoke test.
for i in $(seq 1 36); do
    sleep 5
    if ! kill -0 "$APP_PID" 2>/dev/null; then
        fail "the app exited or crashed"
    fi
    if tools_ready; then
        break
    fi
done
if ! tools_ready; then
    fail "the app did not verify its components in $DATA_DIR after 180 s"
fi
echo "the app verified all components in $DATA_DIR after $((i * 5)) s"
if [ "$(ls -A "$INSTALL")" != "YTD test.AppImage" ]; then
    fail "the app wrote next to the AppImage: $(ls -A "$INSTALL" | tr '\n' ' ')"
fi

echo "== 3. UnixSwapScript from AppUpdater.cs"
SCRIPT=$(tr -d '\r' < "$REPO/AppUpdater.cs" \
    | sed -n '/BEGIN UnixSwapScript/,/END UnixSwapScript/p' \
    | sed -n '/= """$/,/^ *""";$/p' | sed '1d;$d' | sed 's/^        //')
case "$SCRIPT" in
    "while kill -0"*) ;;
    *) fail "could not extract UnixSwapScript from AppUpdater.cs" ;;
esac
printf '%s\n' "$SCRIPT"

cp "$APP" "$APP.new"
NEW_INODE=$(stat -c %i "$APP.new")
# Started like AppUpdater.StartSwapAfterExit: sh -c <script> sh <pid> <new> <target>.
# The script ends in exec, so this PID becomes the updated app.
/bin/sh -c "$SCRIPT" sh "$APP_PID" "$APP.new" "$APP" >> "$LOG" 2>&1 &
NEW_PID=$!
sleep 3
if [ ! -e "$APP.new" ]; then
    fail "swap: the AppImage was replaced while the app was still running"
fi

OLD_PID=$APP_PID
kill "$OLD_PID"
for i in $(seq 1 15); do
    if ! kill -0 "$OLD_PID" 2>/dev/null; then
        break
    fi
    sleep 1
done
if kill -0 "$OLD_PID" 2>/dev/null; then
    echo "warning: the app ignored SIGTERM for 15 s - killing it"
    kill -KILL "$OLD_PID" 2>/dev/null || true
fi
APP_PID=""

for i in $(seq 1 60); do
    if runs_from_mount "$NEW_PID"; then
        break
    fi
    sleep 1
done
if [ -e "$APP.new" ]; then
    fail "swap: $APP.new left behind"
fi
if [ "$(stat -c %i "$APP")" != "$NEW_INODE" ]; then
    fail "swap: the new AppImage is not installed"
fi
if ! runs_from_mount "$NEW_PID"; then
    fail "swap: the updated AppImage was not started"
fi
echo "swap: replaced after the app exited, new one runs from $(readlink "/proc/$NEW_PID/exe")"

echo "AppImage smoke test passed"
