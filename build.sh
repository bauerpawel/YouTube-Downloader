#!/usr/bin/env bash
set -e

echo "============================================"
echo "  YouTube Downloader - budowanie pliku wykonywalnego"
echo "============================================"
echo

if ! command -v dotnet >/dev/null 2>&1; then
    echo "[BLAD] Nie znaleziono polecenia \"dotnet\"."
    echo "Zainstaluj .NET 10 SDK: https://dotnet.microsoft.com/download"
    exit 1
fi

PROJECT="YouTubeDownloader.csproj"
ALL_RIDS="win-x64 win-arm64 linux-x64 linux-arm64 osx-x64 osx-arm64"

build_one() {
    local rid="$1"
    local output="publish/$rid"

    if [ -d "$output" ]; then
        echo "Czyszczenie poprzedniego katalogu $output..."
        rm -rf "$output"
    fi

    echo
    echo "Budowanie pojedynczego pliku wykonywalnego (self-contained, $rid)..."
    dotnet publish "$PROJECT" \
        -c Release \
        -r "$rid" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:EnableCompressionInSingleFile=true \
        -o "$output"
}

# AppImage of linux-<arch>: the single-file binary, packed by appimage/package.sh.
build_appimage() {
    local arch="$1"
    local rid="linux-$arch"
    local version

    build_one "$rid"
    version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT")
    bash "$(dirname "$0")/appimage/package.sh" \
        "publish/$rid/YouTubeDownloader" "publish/appimage-$arch" "$rid" "$version"
}

RID="${1:-linux-x64}"

if [ "$RID" = "all" ]; then
    echo
    echo "Przywracanie zaleznosci..."
    dotnet restore "$PROJECT"

    FAILED=""
    set +e
    for rid in $ALL_RIDS; do
        build_one "$rid"
        if [ $? -ne 0 ]; then
            FAILED="$FAILED $rid"
        fi
    done
    set -e

    echo
    echo "============================================"
    if [ -n "$FAILED" ]; then
        echo "  Zakonczono z bledami. Nie udalo sie zbudowac:$FAILED"
        echo "============================================"
        exit 1
    else
        echo "  Wszystkie 6 wariantow zbudowane pomyslnie w publish/"
        echo "============================================"
    fi
    exit 0
fi

if [ "$RID" = "appimage-x64" ] || [ "$RID" = "appimage-arm64" ]; then
    if [ "$(uname -s)" != "Linux" ]; then
        echo "[BLAD] AppImage mozna zbudowac tylko na Linuksie (appimagetool)."
        exit 1
    fi

    ARCH="${RID#appimage-}"
    echo
    echo "Przywracanie zaleznosci..."
    dotnet restore "$PROJECT"

    build_appimage "$ARCH"

    echo
    echo "============================================"
    echo "  Gotowe! AppImage znajduje sie w:"
    echo "  $(pwd)/publish/$RID/YouTubeDownloader-linux-$ARCH.AppImage"
    echo "============================================"
    exit 0
fi

case "$RID" in
    win-x64|win-arm64|linux-x64|linux-arm64|osx-x64|osx-arm64) ;;
    *)
        echo "[BLAD] Nieznana architektura \"$RID\". Uzyj win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64, appimage-x64, appimage-arm64 lub all."
        echo "Przyklad: ./build.sh all"
        exit 1
        ;;
esac

echo
echo "Przywracanie zaleznosci..."
dotnet restore "$PROJECT"

build_one "$RID"

echo
echo "============================================"
echo "  Gotowe! Plik wykonywalny znajduje sie w:"
echo "  $(pwd)/publish/$RID/YouTubeDownloader"
echo "============================================"
echo
echo "Wskazowka: aby zbudowac wszystkie warianty naraz, uruchom: ./build.sh all"
