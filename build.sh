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

RID="${1:-linux-x64}"
if [ "$RID" != "linux-x64" ] && [ "$RID" != "linux-arm64" ]; then
    echo "[BLAD] Nieznana architektura \"$RID\". Uzyj linux-x64 lub linux-arm64."
    echo "Przyklad: ./build.sh linux-arm64"
    exit 1
fi

OUTPUT="publish/$RID"

if [ -d "$OUTPUT" ]; then
    echo "Czyszczenie poprzedniego katalogu $OUTPUT..."
    rm -rf "$OUTPUT"
fi

echo
echo "Przywracanie zaleznosci..."
dotnet restore "$PROJECT"

echo
echo "Budowanie pojedynczego pliku wykonywalnego (self-contained, $RID)..."
dotnet publish "$PROJECT" \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -o "$OUTPUT"

echo
echo "============================================"
echo "  Gotowe! Plik wykonywalny znajduje sie w:"
echo "  $(pwd)/$OUTPUT/YouTubeDownloader"
echo "============================================"
echo
echo "Wskazowka: aby zbudowac dla ARM64, uruchom: ./build.sh linux-arm64"
