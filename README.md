# YouTube Downloader

<div align="center">

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![C#](https://img.shields.io/badge/C%23-13-239120?logo=csharp)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)
![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)

**Aplikacja desktopowa do pobierania wideo i audio z YouTube**

**Desktop application for downloading videos and audio from YouTube**

[Polski](#polski) | [English](#english)

</div>

---

## Polski

### 📝 Opis

YouTube Downloader to aplikacja desktopowa dla systemu Windows, zbudowana w .NET 9 i Windows Forms, która umożliwia pobieranie filmów i plików audio z YouTube. Aplikacja automatycznie zarządza swoimi zależnościami (yt-dlp, FFmpeg, Deno) i oferuje przyjazny interfejs w języku polskim do wyboru jakości i formatu pobierania.

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
- **.NET 9 SDK** lub nowszy
- **Visual Studio 2022** (opcjonalnie) lub dowolny edytor obsługujący C#
- **System operacyjny**: Windows, Linux lub macOS (do kompilacji)

### 📥 Instalacja .NET 9 SDK

#### Windows:
```powershell
# Pobierz i zainstaluj z oficjalnej strony
# https://dotnet.microsoft.com/download/dotnet/9.0
```

#### Linux (Ubuntu/Debian):
```bash
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 9.0
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

**Framework-dependent:**
```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

Plik wykonywalny zostanie utworzony w:
```
bin/Release/net9.0-windows/win-x64/publish/
```

### 🚀 Użytkowanie

1. **Uruchom aplikację** - Otwórz `YouTubeDownloader.exe`
2. **Wklej URL YouTube** - Skopiuj link do filmu z YouTube i wklej w pole "Adres URL YouTube"
3. **Wybierz typ zawartości** - "Wideo+Audio" lub "Tylko Audio"
4. **Wybierz jakość** - Dostępne opcje zależą od filmu
5. **Wybierz format** - mp4 (zalecany), webm lub mkv
6. **Kliknij "Pobierz"** - Aplikacja rozpocznie pobieranie
7. **Pliki w folderze downloads** - Pobrane pliki znajdziesz w folderze `downloads` w katalogu aplikacji

### 📂 Struktura projektu

```
YouTube-Downloader/
├── MainForm.cs                 # Główna aplikacja - Windows Form z całą logiką
├── YouTubeDownloader.csproj    # Konfiguracja projektu .NET 9
├── README.md                   # Dokumentacja projektu
├── CLAUDE.md                   # Przewodnik dla asystentów AI
├── LICENSE                     # Licencja Apache 2.0
└── .github/
    └── workflows/
        └── dotnet-desktop.yml  # GitHub Actions CI/CD
```

### 🔧 Zależności runtime (pobierane automatycznie)

- **yt-dlp** - Narzędzie do pobierania z YouTube
- **FFmpeg** - Przetwarzanie audio/wideo
- **Deno** - Runtime JavaScript/TypeScript dla yt-dlp (opcjonalnie Node.js)

### 📄 Licencja

Ten projekt jest licencjonowany na podstawie licencji Apache License 2.0 - zobacz plik [LICENSE](LICENSE) po szczegóły.

### 🤝 Wkład w projekt

Zgłoszenia błędów i pull requesty są mile widziane na GitHub.

---

## English

### 📝 Description

YouTube Downloader is a Windows desktop application built with .NET 9 and Windows Forms that enables downloading videos and audio from YouTube. The application automatically manages its dependencies (yt-dlp, FFmpeg, Deno) and provides a user-friendly Polish-language interface for selecting download quality and format.

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
- **.NET 9 SDK** or newer
- **Visual Studio 2022** (optional) or any C#-compatible editor
- **Operating System**: Windows, Linux, or macOS (for compilation)

### 📥 Installing .NET 9 SDK

#### Windows:
```powershell
# Download and install from official website
# https://dotnet.microsoft.com/download/dotnet/9.0
```

#### Linux (Ubuntu/Debian):
```bash
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 9.0
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

**Framework-dependent:**
```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

The executable will be created in:
```
bin/Release/net9.0-windows/win-x64/publish/
```

### 🚀 Usage

1. **Launch the application** - Open `YouTubeDownloader.exe`
2. **Paste YouTube URL** - Copy a YouTube video link and paste it in the "Adres URL YouTube" field
3. **Select content type** - "Wideo+Audio" or "Tylko Audio"
4. **Select quality** - Available options depend on the video
5. **Select format** - mp4 (recommended), webm, or mkv
6. **Click "Pobierz"** - The application will start downloading
7. **Files in downloads folder** - Downloaded files will be in the `downloads` folder in the application directory

### 📂 Project Structure

```
YouTube-Downloader/
├── MainForm.cs                 # Main application - Windows Form with all logic
├── YouTubeDownloader.csproj    # .NET 9 project configuration
├── README.md                   # Project documentation
├── CLAUDE.md                   # AI assistant guide
├── LICENSE                     # Apache 2.0 license
└── .github/
    └── workflows/
        └── dotnet-desktop.yml  # GitHub Actions CI/CD
```

### 🔧 Runtime dependencies (downloaded automatically)

- **yt-dlp** - YouTube downloading tool
- **FFmpeg** - Audio/video processing
- **Deno** - JavaScript/TypeScript runtime for yt-dlp (alternatively Node.js)

### 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

### 🤝 Contributing

Bug reports and pull requests are welcome on GitHub.

---

<div align="center">

**Made with ❤️ for the YouTube downloading community**

</div>
