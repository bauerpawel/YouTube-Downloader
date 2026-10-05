#!/bin/bash
# Tests what macos/package.sh built, the way a user gets it:
#  1. the .dmg holds the app, an Applications link and the window layout
#  2. the app copied out of it (as a drag to Applications does) has a valid
#     signature, Info.plist and icon
#  3. it starts through Launch Services (as a double-click does) and downloads
#     its tools into the data folder, writing nothing into the bundle
#  4. the update zip, extracted with ditto as AppUpdater does, holds a bundle
#     whose signature still verifies
#  5. AppUpdater.cs's MacBundleSwapScript, run verbatim, swaps the bundle and
#     opens it - and puts the old one back when the new one cannot be moved in
# macOS only. Run by CI after macos/package.sh; set YTD_GITHUB_TOKEN to avoid the
# anonymous GitHub API limit on shared runners.
#
# Usage: macos/smoke-test.sh <dmg> <app-zip>
set -euo pipefail

if [ $# -ne 2 ]; then
    echo "Usage: $0 <dmg> <app-zip>" >&2
    exit 2
fi

DMG=$1
APP_ZIP=$2

HERE=$(cd "$(dirname "$0")" && pwd)
REPO=$(dirname "$HERE")
APP_NAME="YouTube Downloader"
EXE_NAME=YouTubeDownloader
DATA_DIR="$HOME/Library/Application Support/YouTubeDownloader"
T=$(mktemp -d "${RUNNER_TEMP:-/tmp}/dmg-smoke.XXXXXX")
MNT="$T/mnt"
APP="$T/Applications/$APP_NAME.app"
APP_EXE="$APP/Contents/MacOS/$EXE_NAME"

stop_app() {
    pkill -f "$APP_EXE" 2>/dev/null || true
    for _ in $(seq 1 20); do
        pgrep -f "$APP_EXE" >/dev/null || return 0
        sleep 0.5
    done
    pkill -9 -f "$APP_EXE" 2>/dev/null || true
}

fail() {
    echo "FAIL: $*"
    [ -f "$T/app.log" ] && cat "$T/app.log"
    stop_app
    hdiutil detach "$MNT" -force >/dev/null 2>&1 || true
    exit 1
}

wait_for_app() {
    for _ in $(seq 1 60); do
        pgrep -f "$APP_EXE" >/dev/null && return 0
        sleep 0.5
    done
    return 1
}

echo "== 1. .dmg layout"
mkdir -p "$MNT"
hdiutil attach -nobrowse -readonly -noautoopen -mountpoint "$MNT" "$DMG"
ls -la "$MNT"
[ -d "$MNT/$APP_NAME.app" ] || fail "no $APP_NAME.app in the .dmg"
[ "$(readlink "$MNT/Applications")" = "/Applications" ] || fail "no Applications link in the .dmg"
[ -f "$MNT/.DS_Store" ] || fail "no window layout (.DS_Store) in the .dmg"
ls "$MNT"/.background.* >/dev/null 2>&1 || fail "no background image in the .dmg"

echo "== 2. install by copying out of the .dmg"
mkdir -p "$T/Applications"
ditto "$MNT/$APP_NAME.app" "$APP"
hdiutil detach "$MNT"
codesign --verify --deep --strict --verbose=2 "$APP" || fail "invalid signature after install"
# The signatures of non-Mach-O files live in extended attributes - see package.sh.
echo "extended attributes of a .dll: $(xattr "$APP/Contents/MacOS/YouTubeDownloader.dll" | tr '\n' ' ')"
plutil -lint "$APP/Contents/Info.plist" || fail "invalid Info.plist"
/usr/libexec/PlistBuddy -c "Print :CFBundleShortVersionString" -c "Print :CFBundleVersion" "$APP/Contents/Info.plist"
iconutil -c iconset -o "$T/AppIcon.iconset" "$APP/Contents/Resources/AppIcon.icns" || fail "macOS cannot read AppIcon.icns"
ls "$T/AppIcon.iconset"
[ "$(ls "$T/AppIcon.iconset" | wc -l)" -ge 10 ] || fail "AppIcon.icns lacks sizes"

echo "== 3. start through Launch Services"
rm -rf "$DATA_DIR"
open -n --env "YTD_GITHUB_TOKEN=${YTD_GITHUB_TOKEN:-}" --stdout "$T/app.log" --stderr "$T/app.log" "$APP"
wait_for_app || fail "the app did not start"
tools=0
for _ in $(seq 1 36); do
    if [ -x "$DATA_DIR/yt-dlp" ] && [ -x "$DATA_DIR/ffmpeg_bin/ffmpeg" ] && [ -x "$DATA_DIR/ffmpeg_bin/ffprobe" ] &&
       grep -Eq '\[status\] (All components are available|Wszystkie komponenty są dostępne)' "$T/app.log"; then
        tools=1
        break
    fi
    pgrep -f "$APP_EXE" >/dev/null || fail "the app stopped"
    sleep 5
done
[ "$tools" = 1 ] || fail "the app did not verify its components in $DATA_DIR after 180 s"
echo "the app verified all components in $DATA_DIR"
stop_app
[ -z "$(find "$APP" -name downloads)" ] || fail "the app created downloads/ inside the bundle"
codesign --verify --deep --strict "$APP" || fail "the app wrote into its own bundle"
cat "$T/app.log"

echo "== 4. update zip"
mkdir -p "$T/update"
ditto -x -k "$APP_ZIP" "$T/update"
[ "$(ls "$T/update")" = "$APP_NAME.app" ] || fail "the update zip must hold exactly $APP_NAME.app"
[ -z "$(find "$T/update" -name '._*')" ] || fail "AppleDouble (._) files left in the extracted update"
NEW="$T/update/$APP_NAME.app"
codesign --verify --deep --strict --verbose=2 "$NEW" || fail "update bundle signature invalid after ditto -x -k"

echo "== 5. MacBundleSwapScript from AppUpdater.cs"
SCRIPT=$(tr -d '\r' < "$REPO/AppUpdater.cs" \
    | sed -n '/BEGIN MacBundleSwapScript/,/END MacBundleSwapScript/p' \
    | sed -n '/= """$/,/^ *""";$/p' | sed '1d;$d' | sed 's/^        //')
case "$SCRIPT" in
    "while kill -0"*) ;;
    *) fail "could not extract MacBundleSwapScript from AppUpdater.cs" ;;
esac
printf '%s\n' "$SCRIPT"

# A bundle keeps its directory inode across a rename - that tells them apart.
inode() { stat -f %i "$1"; }

# Success: the new bundle replaces the old one once the "app" (sleep) exits.
NEW_INODE=$(inode "$NEW")
sleep 2 & STANDIN=$!
/bin/sh -c "$SCRIPT" sh "$STANDIN" "$NEW" "$APP"
[ "$(inode "$APP")" = "$NEW_INODE" ] || fail "swap: the new bundle is not installed"
[ ! -e "$APP.old" ] || fail "swap: $APP.old left behind"
[ ! -e "$NEW" ] || fail "swap: the new bundle is still in the update folder"
wait_for_app || fail "swap: the updated app was not started"
stop_app
echo "swap: new bundle installed and started"

# Failure: the new bundle cannot be moved in - the old one comes back and starts.
OLD_INODE=$(inode "$APP")
sleep 1 & STANDIN=$!
/bin/sh -c "$SCRIPT" sh "$STANDIN" "$T/update/missing.app" "$APP" 2>&1 || true
[ "$(inode "$APP")" = "$OLD_INODE" ] || fail "rollback: the old bundle was not put back"
[ ! -e "$APP.old" ] || fail "rollback: $APP.old left behind"
wait_for_app || fail "rollback: the old app was not started"
stop_app
echo "rollback: old bundle restored and started"

rm -rf "$T"
echo "macOS .dmg smoke test passed"
