# YouTube Downloader

<div align="center">

<img src="logo.svg" alt="YouTube Downloader Logo" width="480"/>

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

YouTube Downloader to aplikacja desktopowa na Windows, Linux i macOS (x64/ARM64), zbudowana w .NET 10 i Avalonia UI, która umożliwia pobieranie filmów i plików audio z YouTube. Aplikacja automatycznie zarządza swoimi zależnościami (yt-dlp, FFmpeg, Deno) i oferuje przyjazny interfejs w języku polskim do wyboru jakości i formatu pobierania.

### ✨ Funkcje

- 🎥 **Pobieranie wideo** - Obsługa różnych rozdzielczości (240p do 4K)
- 🎵 **Pobieranie audio** - Konwersja do formatu MP3 (192 kbps)
- 📦 **Automatyczne zarządzanie zależnościami** - Automatyczne pobieranie yt-dlp, FFmpeg i Deno
- 🎯 **Wybór jakości** - Najlepsza, 4K, 1080p, 720p, 480p, 360p, 240p
- 📁 **Wybór formatu** - mp4, webm, mkv
- 📊 **Pasek postępu** - Wizualizacja postępu pobierania w czasie rzeczywistym
- 🔄 **Aktualizacja komponentów** - Łatwa aktualizacja yt-dlp i FFmpeg z poziomu aplikacji
- 🇵🇱 **Polski interfejs** - Pełne wsparcie języka polskiego

### 🛠️ Wymagania

#### Do uruchomienia aplikacji:
- **System operacyjny**: Windows 10 lub nowszy
- **Architektura**: x64 (64-bit)
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

**Framework-dependent:**
```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

Plik wykonywalny zostanie utworzony w:
```
bin/Release/net10.0/win-x64/publish/
```
(dla `win-arm64` odpowiednio w `bin/Release/net10.0/win-arm64/publish/`)

### 🚀 Użytkowanie

1. **Uruchom aplikację** - Otwórz `YouTubeDownloader.exe`
2. **Wklej URL YouTube** - Skopiuj link do filmu z YouTube i wklej w pole "Adres URL YouTube"
3. **Wybierz typ zawartości** - "Wideo+Audio" lub "Tylko Audio"
4. **Wybierz jakość** - Dostępne opcje zależą od filmu
5. **Wybierz format** - mp4 (zalecany), webm lub mkv
6. **Kliknij "Pobierz"** - Aplikacja rozpocznie pobieranie
7. **Pliki w folderze downloads** - Pobrane pliki znajdziesz w folderze `downloads` w katalogu aplikacji

### 🔒 Gatekeeper (macOS)

Aplikacja nie jest podpisana ani notaryzowana (wymagałoby to płatnego konta Apple Developer Program, którego projekt obecnie nie posiada). Przy pierwszym uruchomieniu macOS Gatekeeper wyświetli ostrzeżenie, że aplikacja pochodzi od "niezidentyfikowanego dewelopera" lub "nie może zostać zweryfikowana". Aby ją uruchomić, wystarczy raz wykonać jedną z poniższych czynności:

- Kliknij prawym przyciskiem myszy (lub Control+klik) na plik aplikacji i wybierz "Otwórz" z menu kontekstowego (pokaże to opcję "Otwórz mimo to", niedostępną przy zwykłym dwukliknięciu), albo
- Usuń atrybut kwarantanny z terminala: `xattr -d com.apple.quarantine <ścieżka-do-pliku>`

Ten krok nie musi być powtarzany przy kolejnych uruchomieniach tego samego pliku.

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

- **yt-dlp** - Narzędzie do pobierania z YouTube
- **FFmpeg** - Przetwarzanie audio/wideo (na macOS pobierane z `eugeneware/ffmpeg-static`, ponieważ BtbN/FFmpeg-Builds nie publikuje wersji dla macOS)
- **Deno** - Runtime JavaScript/TypeScript dla yt-dlp (opcjonalnie Node.js)

**Uwaga (macOS)**: binarki nie są podpisane - przy pierwszym uruchomieniu system pokaże ostrzeżenie Gatekeeper, patrz sekcja "Gatekeeper (macOS)" powyżej.

### 📄 Licencja

Ten projekt jest licencjonowany na podstawie licencji Apache License 2.0 - zobacz plik [LICENSE](LICENSE) po szczegóły.

### 🤝 Wkład w projekt

Zgłoszenia błędów i pull requesty są mile widziane na GitHub.

---

## English

### 📝 Description

YouTube Downloader is a Windows, Linux, and macOS desktop application built with .NET 10 and Avalonia UI that enables downloading videos and audio from YouTube. The application automatically manages its dependencies (yt-dlp, FFmpeg, Deno) and provides a user-friendly Polish-language interface for selecting download quality and format.

### ✨ Features

- 🎥 **Video downloading** - Support for various resolutions (240p to 4K)
- 🎵 **Audio downloading** - Conversion to MP3 format (192 kbps)
- 📦 **Automatic dependency management** - Auto-downloads yt-dlp, FFmpeg, and Deno
- 🎯 **Quality selection** - Best, 4K, 1080p, 720p, 480p, 360p, 240p
- 📁 **Format selection** - mp4, webm, mkv
- 📊 **Progress bar** - Real-time download progress visualization
- 🔄 **Component updates** - Easy updates for yt-dlp and FFmpeg from within the app
- 🇵🇱 **Polish interface** - Full Polish language support

### 🛠️ Requirements

#### To run the application:
- **Operating System**: Windows 10 or newer
- **Architecture**: x64 (64-bit)
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

**Framework-dependent:**
```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

The executable will be created in:
```
bin/Release/net10.0/win-x64/publish/
```
(for `win-arm64`, respectively in `bin/Release/net10.0/win-arm64/publish/`)

### 🚀 Usage

1. **Launch the application** - Open `YouTubeDownloader.exe`
2. **Paste YouTube URL** - Copy a YouTube video link and paste it in the "Adres URL YouTube" field
3. **Select content type** - "Wideo+Audio" or "Tylko Audio"
4. **Select quality** - Available options depend on the video
5. **Select format** - mp4 (recommended), webm, or mkv
6. **Click "Pobierz"** - The application will start downloading
7. **Files in downloads folder** - Downloaded files will be in the `downloads` folder in the application directory

### 🔒 Gatekeeper (macOS)

The application is not code-signed or notarized (that requires a paid Apple Developer Program account, which this project does not currently have). On first launch, macOS Gatekeeper will refuse to open it with a warning that it is "from an unidentified developer" or "cannot be verified." You only need to do one of the following once:

- Right-click (or Control-click) the app and choose "Open" from the context menu (this shows an "Open anyway" option Gatekeeper doesn't offer on a plain double-click), or
- Clear the quarantine attribute from a terminal: `xattr -d com.apple.quarantine <path-to-binary>`

This does not need to be repeated on subsequent launches of the same file.

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

- **yt-dlp** - YouTube downloading tool
- **FFmpeg** - Audio/video processing (on macOS, downloaded from `eugeneware/ffmpeg-static`, since BtbN/FFmpeg-Builds doesn't publish macOS builds)
- **Deno** - JavaScript/TypeScript runtime for yt-dlp (alternatively Node.js)

**Note (macOS)**: binaries are unsigned - on first launch macOS Gatekeeper will show a warning, see the "Gatekeeper (macOS)" section above.

### 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

### 🤝 Contributing

Bug reports and pull requests are welcome on GitHub.

---

<div align="center">

**Made with ❤️ for the YouTube downloading community**

</div>
