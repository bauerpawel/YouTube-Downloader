#!/bin/bash
# Builds the Linux AppImage from the single-file binary:
#   <out>/YouTubeDownloader-<rid>.AppImage
# Linux only, on an x86_64 or aarch64 host - either host builds either target
# (only the runtime and the binary differ). Run by CI (publish job) and by
# ./build.sh appimage-x64 / appimage-arm64.
#
# Usage: appimage/package.sh <binary> <out-dir> <rid> <version>
set -euo pipefail

if [ $# -ne 4 ]; then
    echo "Usage: $0 <binary> <out-dir> <rid> <version>" >&2
    exit 2
fi

BINARY=$1
OUT=$2
RID=$3
VERSION=$4

HERE=$(cd "$(dirname "$0")" && pwd)
REPO=$(dirname "$HERE")

# Pinned and checked by SHA-256 (the "digest" GitHub shows for each asset).
# appimagetool runs on the host and carries its own mksquashfs and
# desktop-file-validate; the runtime is the target's and statically linked, so
# users need no libfuse2 - only fusermount3, which desktops have.
APPIMAGETOOL_VERSION=1.9.1
RUNTIME_VERSION=20251108

sha256_of() {
    case "$1" in
        appimagetool-x86_64.AppImage)  echo ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0 ;;
        appimagetool-aarch64.AppImage) echo f0837e7448a0c1e4e650a93bb3e85802546e60654ef287576f46c71c126a9158 ;;
        runtime-x86_64)                echo 2fca8b443c92510f1483a883f60061ad09b46b978b2631c807cd873a47ec260d ;;
        runtime-aarch64)               echo 00cbdfcf917cc6c0ff6d3347d59e0ca1f7f45a6df1a428a0d6d8a78664d87444 ;;
    esac
}

case "$RID" in
    linux-x64) ARCH=x86_64 ;;
    linux-arm64) ARCH=aarch64 ;;
    *) echo "Unsupported RID '$RID' - use linux-x64 or linux-arm64" >&2; exit 2 ;;
esac

if [ "$(uname -s)" != "Linux" ]; then
    echo "appimagetool runs on Linux only" >&2
    exit 1
fi

case "$(uname -m)" in
    x86_64|amd64) HOST_ARCH=x86_64 ;;
    aarch64|arm64) HOST_ARCH=aarch64 ;;
    *) echo "appimagetool needs an x86_64 or aarch64 Linux host (this one: $(uname -m))" >&2; exit 1 ;;
esac

if [ ! -f "$BINARY" ]; then
    echo "No such file: $BINARY" >&2
    exit 1
fi

CACHE="${XDG_CACHE_HOME:-$HOME/.cache}/ytd-appimage"
mkdir -p "$CACHE"

has_checksum() {
    echo "$(sha256_of "$1")  $2" | sha256sum -c --status
}

# $CACHE/<name> from <url>, unless a copy with the right checksum is there.
fetch() {
    local name=$1 url=$2
    local file="$CACHE/$name"
    if [ -f "$file" ] && has_checksum "$name" "$file"; then
        return
    fi
    rm -f "$file" "$file.part"
    curl -fsSL --retry 3 -o "$file.part" "$url"
    if ! has_checksum "$name" "$file.part"; then
        rm -f "$file.part"
        echo "Checksum mismatch for $name ($url)" >&2
        exit 1
    fi
    mv "$file.part" "$file"
    chmod +x "$file"
}

TOOL=appimagetool-$HOST_ARCH.AppImage
RUNTIME=runtime-$ARCH
fetch "$TOOL" "https://github.com/AppImage/appimagetool/releases/download/$APPIMAGETOOL_VERSION/$TOOL"
fetch "$RUNTIME" "https://github.com/AppImage/type2-runtime/releases/download/$RUNTIME_VERSION/$RUNTIME"

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT
APPDIR="$WORK/YouTubeDownloader.AppDir"
mkdir -p "$APPDIR/usr/bin"

# The runtime runs AppRun; through the symlink the exe still sees its real
# folder (usr/bin), where the single-file host finds its bundle.
install -m 755 "$BINARY" "$APPDIR/usr/bin/YouTubeDownloader"
ln -s usr/bin/YouTubeDownloader "$APPDIR/AppRun"
{
    cat "$HERE/youtube-downloader.desktop"
    echo "X-AppImage-Version=$VERSION"
} > "$APPDIR/youtube-downloader.desktop"
cp "$REPO/macos/AppIcon.iconset/icon_256x256.png" "$APPDIR/youtube-downloader.png"
ln -s youtube-downloader.png "$APPDIR/.DirIcon"

mkdir -p "$OUT"
TARGET="$OUT/YouTubeDownloader-$RID.AppImage"
rm -f "$TARGET"
# appimagetool is an AppImage too: extract-and-run, so building needs no FUSE
# (CI, containers). ARCH names the target, the runtime file must match it.
APPIMAGE_EXTRACT_AND_RUN=1 ARCH=$ARCH "$CACHE/$TOOL" --no-appstream \
    --runtime-file "$CACHE/$RUNTIME" "$APPDIR" "$TARGET"
chmod +x "$TARGET"
ls -la "$TARGET"
