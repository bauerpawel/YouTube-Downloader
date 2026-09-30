# YouTube Downloader

<div align="center">

<img src="logo.svg" alt="YouTube Downloader Logo" width="480"/>

![Version](https://img.shields.io/badge/Version-2.0.300926-brightgreen)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![C#](https://img.shields.io/badge/C%23-13-239120?logo=csharp)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-0078D6?logo=windows)
![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)

**Aplikacja desktopowa do pobierania wideo i audio z YouTube**

**Desktop application for downloading videos and audio from YouTube**

[Polski](#polski) | [English](#english)

</div>

---

## Polski

### 📝 Opis

YouTube Downloader to aplikacja desktopowa na Windows, Linux i macOS (x64/ARM64), zbudowana w .NET 10 i Avalonia UI, która umożliwia pobieranie filmów i plików audio z YouTube. Aplikacja automatycznie zarządza swoimi zależnościami (yt-dlp, FFmpeg, Deno) i oferuje przyjazny interfejs w języku polskim i angielskim do wyboru jakości i formatu pobierania.

### 🆕 Co nowego w wersji 2.0.300926

- 🇬🇧 **Wersja angielska** - interfejs po polsku i po angielsku. Przy pierwszym uruchomieniu język dobierany jest do języka systemu (polski system → polski, inny → angielski), zmiana w menu Widok → Język działa od razu i jest zapamiętywana
- 🔤 **Polskie znaki** - polskie teksty interfejsu pisane są teraz z polskimi znakami („Błąd”, „Narzędzia”, „Jakość”)

### 🆕 Co nowego w wersji 2.0.280926

- ⬆️ **Automatyczna aktualizacja aplikacji** - przy starcie aplikacja sprawdza, czy na GitHubie jest nowsza wersja, i proponuje jej instalację (ręcznie: Narzedzia -> "Sprawdz aktualizacje aplikacji"). Po akceptacji pobiera nową wersję, podmienia się i uruchamia ponownie
- 📁 **Narzędzia w katalogu danych użytkownika** - yt-dlp, FFmpeg, Deno i wybrany motyw są teraz przechowywane w `%LOCALAPPDATA%\YouTubeDownloader` (Windows), `~/.local/share/YouTubeDownloader` (Linux) lub `~/Library/Application Support/YouTubeDownloader` (macOS), a nie obok pliku aplikacji. Przy pierwszym uruchomieniu nowej wersji narzędzia pobiorą się ponownie, stare kopie obok aplikacji zostaną usunięte, a motyw wróci do domyślnego

### 🆕 Co nowego w wersji 2.0.110926

- 🖥️ **Przeniesienie na Avalonia UI** - aplikacja przestała być zależna wyłącznie od Windows Forms, co otworzyło drogę do wsparcia innych systemów
- 🍎🐧 **Wsparcie Linux i macOS** - obok Windows, teraz też natywne buildy na Linux i macOS, każdy w wariancie x64 i ARM64 (6 wariantów łącznie, budowane i testowane w CI)
- 🔗 **Wiele linków naraz** - przycisk "+" obok pola adresu pozwala dodać kolejne linki i pobrać kilka filmów jedną akcją
- 🌗 **Motyw jasny / ciemny / systemowy** - wybór z menu "Widok", zapamiętywany między uruchomieniami
- ℹ️ **Menu Pomoc -> Informacje** - logo, wersja, licencja oraz aktywne linki do repozytorium i strony autora
- 🔄 **Naprawiona automatyczna aktualizacja Deno** - "Aktualizuj komponenty" aktualizuje teraz też Deno, nie tylko yt-dlp i FFmpeg

### ✨ Funkcje

- 🎥 **Pobieranie wideo** - Obsługa różnych rozdzielczości (240p do 4K)
- 🎵 **Pobieranie audio** - Konwersja do formatu MP3 (192 kbps)
- 📦 **Automatyczne zarządzanie zależnościami** - Automatyczne pobieranie yt-dlp, FFmpeg i Deno
- 🎯 **Wybór jakości** - Najlepsza, 4K, 1080p, 720p, 480p, 360p, 240p
- 📁 **Wybór formatu** - mp4, webm, mkv
- 📊 **Pasek postępu** - Wizualizacja postępu pobierania w czasie rzeczywistym
- 🔄 **Aktualizacja komponentów** - Łatwa aktualizacja yt-dlp i FFmpeg z poziomu aplikacji
- 🇵🇱🇬🇧 **Interfejs PL/EN** - Polski i angielski, wybór w menu Widok → Język

### 🛠️ Wymagania

#### Do uruchomienia aplikacji:
- **System operacyjny**: Windows 10 lub nowszy, Linux (nowoczesna dystrybucja) lub macOS
- **Architektura**: x64 lub ARM64 (64-bit)
- **Połączenie internetowe**: Wymagane do pobierania filmów i zależności

#### Do kompilacji:
- **.NET 10 SDK** lub nowszy
- **Visual Studio 2022** (opcjonalnie) lub dowolny edytor obsługujący C#
- **System operacyjny**: Windows, Linux lub macOS (do kompilacji)

### 📥 Instalacja .NET 10 SDK

#### Windows:
```powershell
# Pobierz i zainstaluj z oficjalnej strony
# https://dotnet.microsoft.com/download/dotnet/10.0
```

#### Linux (Ubuntu/Debian):
```bash
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0
```

#### Weryfikacja instalacji:
```bash
dotnet --version
```

### 🔨 Kompilacja

#### 1. Klonowanie repozytorium
```bash
git clone https://github.com/bauerpawel/YouTube-Downloader.git
cd YouTube-Downloader
```

#### 2. Przywrócenie zależności
```bash
dotnet restore
```

#### 3. Budowanie projektu

**Tryb Debug:**
```bash
dotnet build
```

**Tryb Release:**
```bash
dotnet build -c Release
```

#### 4. Uruchomienie aplikacji
```bash
dotnet run
```

#### 5. Publikacja (plik wykonywalny)

**Single-file executable (zalecane):**
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Dla komputerów z procesorem ARM (np. Windows on ARM) zamień `win-x64` na `win-arm64`:
```bash
dotnet publish -c Release -r win-arm64 --self-contained true -p:PublishSingleFile=true
```

Analogicznie dla Linuksa:
```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=true
```

...i dla macOS:
```bash
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true
```

Zamiast wywoływać `dotnet publish` ręcznie, można też użyć `build.bat` (Windows, domyślnie `win-x64`) lub `build.sh` (Linux/macOS, domyślnie `linux-x64`), np. `build.bat win-arm64` lub `./build.sh osx-arm64`.

**Framework-dependent:**
```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

Plik wykonywalny zostanie utworzony w:
```
bin/Release/net10.0/win-x64/publish/
```
(dla pozostałych RID-ów analogicznie, np. `bin/Release/net10.0/win-arm64/publish/`, `bin/Release/net10.0/linux-x64/publish/`, `bin/Release/net10.0/osx-arm64/publish/` itd.)

### 🚀 Użytkowanie

1. **Uruchom aplikację** - Otwórz pobrany plik (np. `YouTubeDownloader-win-x64.exe`). Przy pierwszym uruchomieniu aplikacja sama pobierze yt-dlp, FFmpeg i Deno
2. **Wklej link** - Skopiuj link do filmu z YouTube i wklej go w pole "Link do filmu:". Przycisk "+" dodaje kolejne pola, aby pobrać kilka filmów naraz
3. **Wybierz typ** - "Wideo + Audio" lub "Tylko Audio (MP3)"
4. **Wybierz jakość** - Od "Najlepsza" do 240p (w trybie audio pole jest nieaktywne)
5. **Wybierz format** - mp4 (zalecany), webm lub mkv
6. **Kliknij "Pobierz"** - Aplikacja rozpocznie pobieranie i pokaże postęp
7. **Pliki w folderze downloads** - Pobrane pliki znajdziesz w folderze `downloads` w katalogu aplikacji

Język interfejsu zmienisz w menu **Widok → Język** (Polski / English). Przy pierwszym uruchomieniu dobierany jest do języka systemu.

### 🔒 Gatekeeper (macOS)

Aplikacja nie jest podpisana ani notaryzowana (wymagałoby to płatnego konta Apple Developer Program, którego projekt obecnie nie posiada). Przy pierwszym uruchomieniu macOS Gatekeeper wyświetli ostrzeżenie, że aplikacja pochodzi od "niezidentyfikowanego dewelopera" lub "nie może zostać zweryfikowana". Aby ją uruchomić, wystarczy raz wykonać jedną z poniższych czynności:

1. **Zalecane (działa na macOS 15 Sequoia i nowszych, a także na starszych wersjach)**: spróbuj otworzyć aplikację - zostanie zablokowana - a następnie przejdź do **Ustawienia systemowe -> Prywatność i bezpieczeństwo**, przewiń w dół do komunikatu o zablokowanej aplikacji i kliknij **"Otwórz mimo to"**. (Starszy sposób przez kliknięcie prawym przyciskiem/Control+klik -> "Otwórz" od macOS 15 Sequoia nie pokazuje już opcji "Otwórz mimo to".)
2. **Alternatywa w terminalu**: usuń atrybut kwarantanny bezpośrednio: `xattr -d com.apple.quarantine <ścieżka-do-pliku>`

Ten krok nie musi być powtarzany przy kolejnych uruchomieniach tego samego pliku. Uwaga: paczka `.zip` z wydania (budowana w CI za pomocą `Compress-Archive`) nie zachowuje uniksowego bitu wykonywalności - po rozpakowaniu na macOS lub Linuksie należy najpierw nadać uprawnienie: `chmod +x <ścieżka-do-pliku>`, zanim aplikację da się w ogóle uruchomić (niezależnie od kroku z Gatekeeperem powyżej).

### 📂 Struktura projektu

```
YouTube-Downloader/
├── Program.cs                   # Punkt wejścia aplikacji (Avalonia AppBuilder, STAThread)
├── App.axaml                    # XAML na poziomie aplikacji (rejestruje FluentTheme)
├── App.axaml.cs                 # Logika startowa - tworzy i pokazuje MainWindow
├── MainWindow.axaml             # Układ UI głównego okna (XAML)
├── MainWindow.axaml.cs          # Logika głównego okna - zarządzanie zależnościami, pobieranie,
│                                 # obsługa URL, mechanizm aktualizacji (cała logika biznesowa)
├── AboutWindow.axaml            # Układ UI okna "Informacje" (XAML)
├── AboutWindow.axaml.cs         # Logika okna "Informacje" - linki do repo/autora
├── MessageDialog.axaml          # Układ UI okna komunikatów/potwierdzeń wielokrotnego użytku (XAML)
├── MessageDialog.axaml.cs       # Logika okna komunikatów/potwierdzeń wielokrotnego użytku
│                                 # (zastępuje WinForms MessageBox)
├── ThemeSettings.cs             # Zapis/odczyt wybranego motywu (theme.txt)
├── AppPaths.cs                  # Folder aplikacji vs katalog danych użytkownika, sprzątanie starych plików
├── AppUpdater.cs                # Samoaktualizacja aplikacji z GitHub Releases
├── GitHubApi.cs                 # Zapytania do GitHub API (opcjonalny token YTD_GITHUB_TOKEN w CI)
├── Strings.cs                   # Teksty interfejsu po polsku i angielsku
├── LanguageSettings.cs          # Wybór i zapis języka (language.txt)
├── YtDlpArguments.cs            # Argumenty yt-dlp dla jakości/formatu
├── Assets/
│   └── app-logo.png             # Logo aplikacji, widoczne w oknie Informacje i oknach komunikatów
├── YouTubeDownloader.csproj     # Konfiguracja projektu .NET 10 (pakiety Avalonia)
├── app.ico                      # Ikona aplikacji/okna
├── logo.svg                     # Źródłowe logo aplikacji (SVG)
├── build.bat                    # Owija `dotnet publish` dla win-x64/win-arm64
├── build.sh                     # Owija `dotnet publish` dla linux-x64/linux-arm64/osx-x64/osx-arm64
├── README.md                    # Dokumentacja projektu
├── LICENSE                      # Licencja Apache 2.0
├── CLAUDE.md                    # Przewodnik dla asystentów AI
└── .github/
    └── workflows/
        └── dotnet-desktop.yml   # GitHub Actions CI/CD
```

### 🔧 Zależności runtime (pobierane automatycznie)

- **[yt-dlp](https://github.com/yt-dlp/yt-dlp)** - Narzędzie do pobierania z YouTube
- **[FFmpeg](https://ffmpeg.org/)** - Przetwarzanie audio/wideo (Windows/Linux: [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds); macOS: [eugeneware/ffmpeg-static](https://github.com/eugeneware/ffmpeg-static), ponieważ BtbN/FFmpeg-Builds nie publikuje wersji dla macOS)
- **[Deno](https://deno.com/)** - Runtime JavaScript/TypeScript dla yt-dlp (opcjonalnie [Node.js](https://nodejs.org/))

Narzędzia są zapisywane w katalogu danych użytkownika: `%LOCALAPPDATA%\YouTubeDownloader` (Windows), `~/.local/share/YouTubeDownloader` (Linux), `~/Library/Application Support/YouTubeDownloader` (macOS).

**Uwaga (macOS)**: binarki nie są podpisane - przy pierwszym uruchomieniu system pokaże ostrzeżenie Gatekeeper, patrz sekcja "Gatekeeper (macOS)" powyżej.

### 📄 Licencja

Ten projekt jest licencjonowany na podstawie licencji Apache License 2.0 - zobacz plik [LICENSE](LICENSE) po szczegóły.

### 🤝 Wkład w projekt

Zgłoszenia błędów i pull requesty są mile widziane na GitHub.

---

## English

### 📝 Description

YouTube Downloader is a Windows, Linux, and macOS desktop application built with .NET 10 and Avalonia UI that enables downloading videos and audio from YouTube. The application automatically manages its dependencies (yt-dlp, FFmpeg, Deno) and provides a user-friendly Polish and English interface for selecting download quality and format.

### 🆕 What's New in 2.0.300926

- 🇬🇧 **English interface** - the UI is available in Polish and English. On first launch the language follows the system language (Polish system → Polish, anything else → English); switch any time in View → Language, it applies immediately and is remembered
- 🔤 **Polish diacritics** - the Polish UI texts now use proper Polish characters

### 🆕 What's New in 2.0.280926

- ⬆️ **Automatic app updates** - on startup the app checks GitHub for a newer version and offers to install it (manually: Narzedzia (Tools) -> "Sprawdz aktualizacje aplikacji" (Check for app updates)). Once accepted, it downloads the new version, swaps itself out and restarts
- 📁 **Tools in the per-user data folder** - yt-dlp, FFmpeg, Deno and the chosen theme now live in `%LOCALAPPDATA%\YouTubeDownloader` (Windows), `~/.local/share/YouTubeDownloader` (Linux) or `~/Library/Application Support/YouTubeDownloader` (macOS) instead of next to the app. On the first launch of the new version the tools are downloaded again, old copies next to the app are removed, and the theme resets to the default

### 🆕 What's New in 2.0.110926

- 🖥️ **Migrated to Avalonia UI** - the app is no longer tied exclusively to Windows Forms, opening the door to supporting other operating systems
- 🍎🐧 **Linux and macOS support** - alongside Windows, now with native builds for Linux and macOS, each in x64 and ARM64 variants (6 variants total, built and tested in CI)
- 🔗 **Multiple links at once** - the "+" button next to the URL field lets you add more links and download several videos in a single action
- 🌗 **Light / dark / system theme** - selectable from the "Widok" (View) menu, remembered across launches
- ℹ️ **Pomoc (Help) -> Informacje (About) menu** - logo, version, license, and active links to the repository and author's site
- 🔄 **Fixed automatic Deno updates** - "Aktualizuj komponenty" (Update Components) now also updates Deno, not just yt-dlp and FFmpeg

### ✨ Features

- 🎥 **Video downloading** - Support for various resolutions (240p to 4K)
- 🎵 **Audio downloading** - Conversion to MP3 format (192 kbps)
- 📦 **Automatic dependency management** - Auto-downloads yt-dlp, FFmpeg, and Deno
- 🎯 **Quality selection** - Best, 4K, 1080p, 720p, 480p, 360p, 240p
- 📁 **Format selection** - mp4, webm, mkv
- 📊 **Progress bar** - Real-time download progress visualization
- 🔄 **Component updates** - Easy updates for yt-dlp and FFmpeg from within the app
- 🇵🇱🇬🇧 **PL/EN interface** - Polish and English, switchable in View → Language

### 🛠️ Requirements

#### To run the application:
- **Operating System**: Windows 10 or newer, Linux (a modern distribution), or macOS
- **Architecture**: x64 or ARM64 (64-bit)
- **Internet connection**: Required for downloading videos and dependencies

#### To compile:
- **.NET 10 SDK** or newer
- **Visual Studio 2022** (optional) or any C#-compatible editor
- **Operating System**: Windows, Linux, or macOS (for compilation)

### 📥 Installing .NET 10 SDK

#### Windows:
```powershell
# Download and install from official website
# https://dotnet.microsoft.com/download/dotnet/10.0
```

#### Linux (Ubuntu/Debian):
```bash
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0
```

#### Verify installation:
```bash
dotnet --version
```

### 🔨 Compilation

#### 1. Clone the repository
```bash
git clone https://github.com/bauerpawel/YouTube-Downloader.git
cd YouTube-Downloader
```

#### 2. Restore dependencies
```bash
dotnet restore
```

#### 3. Build the project

**Debug mode:**
```bash
dotnet build
```

**Release mode:**
```bash
dotnet build -c Release
```

#### 4. Run the application
```bash
dotnet run
```

#### 5. Publish (executable file)

**Single-file executable (recommended):**
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

For ARM-based PCs (e.g. Windows on ARM), swap `win-x64` for `win-arm64`:
```bash
dotnet publish -c Release -r win-arm64 --self-contained true -p:PublishSingleFile=true
```

Likewise for Linux:
```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=true
```

...and for macOS:
```bash
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true
```

Instead of calling `dotnet publish` directly, you can also use `build.bat` (Windows, defaults to `win-x64`) or `build.sh` (Linux/macOS, defaults to `linux-x64`), e.g. `build.bat win-arm64` or `./build.sh osx-arm64`.

**Framework-dependent:**
```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

The executable will be created in:
```
bin/Release/net10.0/win-x64/publish/
```
(for the other RIDs, respectively, e.g. `bin/Release/net10.0/win-arm64/publish/`, `bin/Release/net10.0/linux-x64/publish/`, `bin/Release/net10.0/osx-arm64/publish/`, etc.)

### 🚀 Usage

1. **Launch the application** - Open the downloaded file (e.g. `YouTubeDownloader-win-x64.exe`). On first launch the app downloads yt-dlp, FFmpeg and Deno by itself
2. **Paste a link** - Copy a YouTube video link and paste it into the "Video link:" field. The "+" button adds more fields to download several videos at once
3. **Select the type** - "Video + Audio" or "Audio only (MP3)"
4. **Select the quality** - From "Best" down to 240p (disabled in audio mode)
5. **Select the format** - mp4 (recommended), webm, or mkv
6. **Click "Download"** - The application starts downloading and shows the progress
7. **Files in the downloads folder** - Downloaded files will be in the `downloads` folder in the application directory

The interface language can be changed in **View → Language** (Polski / English). On first launch it follows the system language.

### 🔒 Gatekeeper (macOS)

The application is not code-signed or notarized (that requires a paid Apple Developer Program account, which this project does not currently have). On first launch, macOS Gatekeeper will refuse to open it with a warning that it is "from an unidentified developer" or "cannot be verified." You only need to do one of the following once:

1. **Recommended (works on macOS 15 Sequoia and later, and on older versions too)**: attempt to open the app - it will be blocked - then go to **System Settings -> Privacy & Security**, scroll down to the blocked-app notice, and click **"Open Anyway"**. (The older right-click/Control-click -> "Open" workaround no longer shows an "Open Anyway" option starting with macOS 15 Sequoia.)
2. **Terminal alternative**: clear the quarantine attribute directly: `xattr -d com.apple.quarantine <path-to-binary>`

This does not need to be repeated on subsequent launches of the same file. Note: the release `.zip` asset (built via `Compress-Archive` in CI) does not preserve the Unix executable bit - after unzipping on macOS or Linux, you'll need to run `chmod +x <path-to-binary>` before the app can be launched at all, regardless of the Gatekeeper step above.

### 📂 Project Structure

```
YouTube-Downloader/
├── Program.cs                   # Application entry point (Avalonia AppBuilder, STAThread)
├── App.axaml                    # Application-level XAML (registers FluentTheme)
├── App.axaml.cs                 # Startup logic - creates and shows MainWindow
├── MainWindow.axaml             # Main window UI layout (XAML)
├── MainWindow.axaml.cs          # Main window logic - dependency management, download
│                                 # orchestration, URL handling, update mechanism (all business logic)
├── AboutWindow.axaml            # "About" dialog UI layout (XAML)
├── AboutWindow.axaml.cs         # "About" dialog logic - repo/author links
├── MessageDialog.axaml          # Reusable message/confirmation dialog UI layout (XAML)
├── MessageDialog.axaml.cs       # Reusable message/confirmation dialog logic
│                                 # (replaces WinForms MessageBox)
├── ThemeSettings.cs             # Loads/saves the chosen theme (theme.txt)
├── AppPaths.cs                  # App folder vs per-user data folder, legacy-file cleanup
├── AppUpdater.cs                # App self-update from GitHub Releases
├── GitHubApi.cs                 # GitHub API requests (optional YTD_GITHUB_TOKEN in CI)
├── Strings.cs                   # UI texts in Polish and English
├── LanguageSettings.cs          # Language choice and persistence (language.txt)
├── YtDlpArguments.cs            # yt-dlp arguments for quality/format
├── Assets/
│   └── app-logo.png             # Application logo, shown in About and message dialogs
├── YouTubeDownloader.csproj     # .NET 10 project configuration (Avalonia packages)
├── app.ico                      # Application/window icon
├── logo.svg                     # Source application logo (SVG)
├── build.bat                    # Wraps `dotnet publish` for win-x64/win-arm64
├── build.sh                     # Wraps `dotnet publish` for linux-x64/linux-arm64/osx-x64/osx-arm64
├── README.md                    # Project documentation
├── LICENSE                      # Apache 2.0 license
├── CLAUDE.md                    # AI assistant guide
└── .github/
    └── workflows/
        └── dotnet-desktop.yml   # GitHub Actions CI/CD
```

### 🔧 Runtime dependencies (downloaded automatically)

- **[yt-dlp](https://github.com/yt-dlp/yt-dlp)** - YouTube downloading tool
- **[FFmpeg](https://ffmpeg.org/)** - Audio/video processing (Windows/Linux: [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds); macOS: [eugeneware/ffmpeg-static](https://github.com/eugeneware/ffmpeg-static), since BtbN/FFmpeg-Builds doesn't publish macOS builds)
- **[Deno](https://deno.com/)** - JavaScript/TypeScript runtime for yt-dlp (alternatively [Node.js](https://nodejs.org/))

The tools are stored in the per-user data folder: `%LOCALAPPDATA%\YouTubeDownloader` (Windows), `~/.local/share/YouTubeDownloader` (Linux), `~/Library/Application Support/YouTubeDownloader` (macOS).

**Note (macOS)**: binaries are unsigned - on first launch macOS Gatekeeper will show a warning, see the "Gatekeeper (macOS)" section above.

### 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

### 🤝 Contributing

Bug reports and pull requests are welcome on GitHub.

---

<div align="center">

**Made with ❤️ for the YouTube downloading community**

</div>
