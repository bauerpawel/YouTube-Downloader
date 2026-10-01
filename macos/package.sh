#!/bin/bash
# Builds the macOS installer from a self-contained publish folder:
#   <out>/YouTube Downloader.app            the app bundle
#   <out>/YouTubeDownloader-<rid>.dmg       drag-to-Applications installer
#   <out>/YouTubeDownloader-<rid>-app.zip   the same bundle, for the app's self-update
# macOS only (codesign, ditto, PlistBuddy, hdiutil via dmgbuild). Run by CI.
#
# Usage: macos/package.sh <publish-dir> <out-dir> <rid> <version> <build-number>
set -euo pipefail

if [ $# -ne 5 ]; then
    echo "Usage: $0 <publish-dir> <out-dir> <rid> <version> <build-number>" >&2
    exit 2
fi

PUBLISH=$1
OUT=$2
RID=$3
VERSION=$4
BUILD=$5

HERE=$(cd "$(dirname "$0")" && pwd)
APP_NAME="YouTube Downloader"
APP="$OUT/$APP_NAME.app"
DMGBUILD_VERSION=1.6.7

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

# The whole publish folder goes into Contents/MacOS: macOS is not single-file
# (see the workflow's "Publish Application" step), the exe needs every sibling.
cp -R "$PUBLISH/." "$APP/Contents/MacOS/"
chmod +x "$APP/Contents/MacOS/YouTubeDownloader"

cp "$HERE/Info.plist" "$APP/Contents/Info.plist"
/usr/libexec/PlistBuddy \
    -c "Set :CFBundleShortVersionString $VERSION" \
    -c "Set :CFBundleVersion $BUILD" \
    "$APP/Contents/Info.plist"
plutil -lint "$APP/Contents/Info.plist"

cp "$HERE/AppIcon.icns" "$APP/Contents/Resources/"
cp -R "$HERE/en.lproj" "$HERE/pl.lproj" "$APP/Contents/Resources/"

# Ad-hoc signature over the whole bundle: Apple Silicon runs no unsigned code,
# and the bundle seal covers Info.plist and the resources. Non-Mach-O files in
# Contents/MacOS (.dll, .json) are sealed by hash in CodeResources, so the
# signature needs no extended attributes and survives a plain zip. An invalid
# signature on a quarantined app makes macOS call it "damaged" with no way to
# open it - hence the strict verify.
codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict --verbose=2 "$APP"

# Update zip: no resource forks or extended attributes - .NET's ZipFile would
# extract AppleDouble entries as stray ._ files into the bundle.
rm -f "$OUT/YouTubeDownloader-$RID-app.zip"
(cd "$OUT" && ditto -c -k --keepParent --norsrc --noextattr --noqtn "$APP_NAME.app" "YouTubeDownloader-$RID-app.zip")

# dmgbuild writes the window layout (.DS_Store) itself - no Finder/AppleScript,
# which a headless CI runner does not reliably have.
VENV="$OUT/.dmgbuild-venv"
rm -rf "$VENV"
python3 -m venv "$VENV"
"$VENV/bin/pip" install --quiet --disable-pip-version-check "dmgbuild==$DMGBUILD_VERSION"
rm -f "$OUT/YouTubeDownloader-$RID.dmg"
"$VENV/bin/dmgbuild" \
    -s "$HERE/dmg-settings.py" \
    -D app="$APP" \
    -D icon="$HERE/AppIcon.icns" \
    "$APP_NAME" "$OUT/YouTubeDownloader-$RID.dmg"
rm -rf "$VENV"

ls -la "$OUT"
