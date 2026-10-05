# Testy

Testy .NET wymagają SDK .NET 10 i działają na Windows, Linuksie i macOS:

```sh
dotnet test tests/YouTubeDownloader.Tests/YouTubeDownloader.Tests.csproj -c Debug
dotnet test tests/YouTubeDownloader.Tests/YouTubeDownloader.Tests.csproj -c Release
```

Obejmują walidację adresów, granice argumentów procesów, instalację i aktualizacje
komponentów, integralność pobrań, obsługę dużych strumieni stdout/stderr,
zatrzymywanie procesów potomnych i anulowanie kolejki.

Testy multimedialne wymagają dodatkowo Pythona 3.10+, yt-dlp, FFmpeg z kodekami
libx264/libvpx-vp9/libopus oraz ffprobe. Narzędzia muszą być dostępne w PATH;
zmienna `YT_DLP` może wskazywać plik wykonywalny yt-dlp. CI używa yt-dlp 2026.08.19.

```sh
python -m pip install yt-dlp==2026.08.19
dotnet build tests/YouTubeDownloader.Tests/YouTubeDownloader.Tests.csproj -c Release
python tests/media_smoke.py --argument-helper tests/YouTubeDownloader.Tests/bin/Release/net10.0/YouTubeDownloader.Tests.dll
```

Skrypt generuje krótkie klipy i udostępnia je przez lokalny serwer HTTP. Korzysta
z argumentów tworzonych przez kod aplikacji, uruchamia yt-dlp i FFmpeg, a wynik
sprawdza przez ffprobe. Weryfikuje zgodność strumieni WebM, limit jakości,
MP4/MKV, pojedynczy strumień zapisywany jako MKV, MP3 i błąd przy braku
strumieni WebM. Nie pobiera filmów z YouTube. Brak narzędzi powoduje błąd testu.

CI uruchamia testy .NET w Debug i Release na trzech systemach, zachowuje raporty
TRX, wykonuje testy multimedialne na Linuksie oraz sprawdza uruchomienie paczek
macOS x64 i ARM64 wraz z pobraniem i weryfikacją komponentów. Wszystkie te
kontrole muszą przejść przed publikacją. Błąd uruchomienia macOS jest zapisywany
również w adnotacjach GitHub Actions. Testy te nie zastępują sprawdzenia pełnej
interakcji GUI i prawdziwego pobierania z YouTube.
