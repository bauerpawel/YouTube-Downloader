# CLAUDE.md - YouTube Downloader Project Guide

## Project Overview

**YouTube Downloader** is a Windows, Linux, and macOS desktop application built with .NET 10 and Avalonia UI that enables users to download videos and audio from YouTube, including from multiple links in a single run. The application automatically manages its dependencies (yt-dlp, FFmpeg, and Deno runtime) and provides a user-friendly Polish-language interface for selecting download quality and format.

### Key Information
- **Technology Stack**: .NET 10, C# 13, Avalonia UI 12.1.2
- **Target Platform**: Windows, Linux, and macOS (x64/ARM64), `net10.0`. Built and published for all three OSes, and CI-smoke-tested on `ubuntu-latest` and `macos-latest` in addition to `windows-latest` - see [Development Workflows](#development-workflows). macOS binaries are unsigned (no Apple Developer account) - users see a one-time Gatekeeper warning on first launch, see [External Dependencies](#external-dependencies)
- **License**: Apache License 2.0
- **Primary Language**: C# with Polish UI text
- **Architecture**: Avalonia UI application (XAML + code-behind, no MVVM) with external dependency management

## Repository Structure

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
├── ThemeSettings.cs             # Loads/saves the Light/Dark/System theme choice (theme.txt)
├── AppPaths.cs                  # App folder vs per-user data folder, legacy-file cleanup,
│                                 # MakeExecutable() and safe-delete helpers (no Avalonia)
├── AppUpdater.cs                # App self-update from GitHub Releases (no UI, no Avalonia)
├── Assets/
│   └── app-logo.png             # Application logo, shown in About and message dialogs
├── YouTubeDownloader.csproj     # .NET 10 project configuration (Avalonia packages)
├── app.ico                      # Application/window icon
├── logo.svg                     # Source application logo (SVG)
├── build.bat                    # Wraps `dotnet publish` for win-x64/win-arm64 (Windows)
├── build.sh                     # Wraps `dotnet publish` for linux-x64/linux-arm64/osx-x64/osx-arm64
├── README.md                    # Project documentation
├── LICENSE                      # Apache 2.0 license
└── CLAUDE.md                    # This file - AI assistant guide
```

### Runtime Structure (Created at Runtime)

The app keeps two locations apart (`AppPaths.cs`):

- **App folder** (`AppPaths.AppDirectory`, where the executable lives) holds only the app itself and `downloads/`. Self-update replaces files here and nothing else.
- **Data folder** (`AppPaths.DataDirectory`) holds everything the app downloads or writes: `%LOCALAPPDATA%\YouTubeDownloader` (Windows), `~/.local/share/YouTubeDownloader` (Linux, honours `XDG_DATA_HOME`), `~/Library/Application Support/YouTubeDownloader` (macOS), `$SNAP_USER_COMMON` inside a snap. Falls back to the app folder if the per-user location is unavailable.

On Windows:
```
Application Directory/
├── YouTubeDownloader.exe       # The app (any file name - often the release asset name)
├── node.exe                    # Optional, user-supplied; also looked up in the data folder first
└── downloads/                  # Default download location
    └── (downloaded videos)

Data Directory/
├── yt-dlp.exe                  # YouTube downloader CLI tool
├── deno.exe                    # Deno runtime (auto-downloaded if not in PATH)
├── deno_version.txt            # Tracks installed Deno version for update checks
├── node.exe                    # Optional, user-supplied alternative runtime
├── ffmpeg_bin/                 # FFmpeg binaries directory
│   ├── ffmpeg.exe
│   ├── ffprobe.exe
│   ├── ffplay.exe
│   └── *.dll                   # FFmpeg shared libraries (win64-gpl-shared build)
├── ffmpeg_version.txt          # Tracks current FFmpeg version
└── theme.txt                   # Light/Dark/Default theme choice
```

**Migration from older versions:** versions before 2.0.280926 kept the tools next to the exe. `AppPaths.CleanupLegacyFiles()` (called at the end of `CheckAndDownloadComponents()` once working copies exist in the data folder) deletes exactly `yt-dlp(.exe)`, `deno(.exe)`, `deno_version.txt`, `theme.txt`, `ffmpeg_bin/`, `ffmpeg_temp/` and finally the marker `ffmpeg_version.txt` - but only when that marker exists (proof the folder was managed by an old version; the app folder may be the user's Downloads folder). `node`, `downloads/` and the old temp archives are never touched.

On Linux, the data-folder layout is identical but every binary lacks the `.exe` extension (`yt-dlp`, `deno`, `node`), and `ffmpeg_bin/` holds `ffmpeg`/`ffprobe`/`ffplay` - no `*.so` companions, because the Linux download is the **static** FFmpeg build rather than `-shared` (see Dependency Management below for why). All downloaded/extracted binaries are marked executable via `MakeExecutable()` - strictly required for the raw HTTP download (yt-dlp has no archive to carry a Unix mode), and applied defensively after `tar`/`ZipFile` extraction too, since archive-recorded permissions can't always be relied on.

On macOS, the data-folder layout is structurally different for FFmpeg specifically: the macOS FFmpeg source (`eugeneware/ffmpeg-static`) publishes two loose, already-executable binaries (`ffmpeg`, `ffprobe`) rather than an archive containing a `bin/` folder, so `ffmpeg_bin/` on macOS never holds anything beyond those two files - there is no `ffplay`, since `eugeneware/ffmpeg-static` doesn't publish one. yt-dlp on macOS, by contrast, is a single `universal2` binary (`yt-dlp_macos`) covering both x64 and ARM64 - unlike Deno and FFmpeg, there is no separate per-architecture asset to pick between.

## Codebase Architecture

### Application Structure
The application is split across a small number of Avalonia UI files, rather than the single-file WinForms design used previously:
- **Program.cs**: Entry point. Configures and starts the Avalonia `AppBuilder` under `[STAThread]`.
- **App.axaml / App.axaml.cs**: Application-level XAML (registers `FluentTheme`) and startup logic that creates `MainWindow` via `IClassicDesktopStyleApplicationLifetime`.
- **MainWindow.axaml / MainWindow.axaml.cs**: The main window - UI layout in XAML, with essentially all business logic in the code-behind: dependency management, download orchestration, URL handling/validation, progress parsing, and the component-update mechanism.
- **AboutWindow.axaml / AboutWindow.axaml.cs**: The "About" dialog - app name/version/license and clickable repo/author links.
- **MessageDialog.axaml / MessageDialog.axaml.cs**: A reusable message-box/confirmation dialog, replacing WinForms' `MessageBox.Show()`.

### Key Components

#### 1. UI Components (`MainWindow.axaml`)
Named elements (`Name`) become strongly-typed, non-nullable code-behind fields automatically - see Key Conventions:
- `TxtUrl`: TextBox for the first/primary YouTube URL
- `BtnAddUrl` / `ExtraUrlRowsPanel`: adds dynamic extra URL rows for multi-URL downloads
- `CbContentType`: ComboBox for Video+Audio or Audio-only selection
- `CbQuality`: ComboBox for quality selection (Best, 4K, 1080p, 720p, 480p, 360p, 240p)
- `CbFormat`: ComboBox for format selection (mp4, webm, mkv)
- `BtnDownload`: triggers the download
- `ProgressBarDownload`: ProgressBar for download progress
- `LblStatus`: TextBlock for status messages and real-time progress
- `MiAktualizujKomponenty` / `MiInformacje`: menu items for "Aktualizuj komponenty" (Update Components) / "Informacje" (About)
- `MiSprawdzAktualizacje`: menu item "Sprawdz aktualizacje aplikacji" (Check for app updates), next to "Aktualizuj komponenty"

#### 2. Multi-URL Handling (`MainWindow.axaml.cs`)
**BtnAddUrl_Click() / RemoveUrlRow()**
- Dynamically add/remove extra URL rows (TextBox + remove Button) inside `ExtraUrlRowsPanel`, tracked in the `extraUrlBoxes` / `removeUrlButtons` lists

**GetAllUrls()**
- Collects and trims the primary URL plus every extra row, filtering out blanks

**SetUrlRowsEnabled()**
- Enables/disables all URL inputs and the add button while a download is running

#### 3. Dependency Management (`MainWindow.axaml.cs`)

All three dependency downloaders are OS-conditional (checked via `OperatingSystem.IsWindows()` and, on non-Windows, `RuntimeInformation.ProcessArchitecture` to distinguish x64 vs ARM64). Windows behavior is unchanged from the original WinForms/single-OS app; Linux (x64 + ARM64) and macOS (x64 + ARM64) are parallel code paths added alongside it, not a replacement.

**CheckAndDownloadComponents()**
- Checks for Deno/Node.js runtime availability (`IsRuntimeInPath()`)
- Downloads yt-dlp if missing, FFmpeg if missing
- All operations are async and report progress via `UpdateStatus()`

**IsRuntimeInPath() / GetRuntimePath() / CheckAndUpdateDeno()'s "system runtime" check - three distinct mechanisms, not one**
- `FindSystemDeno()` shells out to `where deno` (Windows) / `which deno` (Linux/macOS) and returns the first hit that **exists and lies outside the app folder** (`AppPaths.PickFirstPathOutside()`): `where` searches the current directory first, which for a double-clicked app is the app folder - an old `deno.exe` left there by a pre-2.0.280926 version must not count as system-wide. A GUI app also receives `where` output in the OEM code page decoded as ANSI, so a non-ASCII path (`C:\Users\Michał\Downloads`) comes back mangled and would no longer match the app folder - the `File.Exists` check drops such lines. `where` also prints every match on its own line, so the raw output is never used as a path
- `IsRuntimeInPath()` (called from `CheckAndDownloadComponents()` to decide whether Deno must be auto-downloaded) is `FindSystemDeno() != ""`
- `GetRuntimePath()` (used to build the yt-dlp `--js-runtimes` invocation) returns the data-folder `denoPath`, then `nodeJsPath` (data folder first, then app folder), then `FindSystemDeno()`
- `CheckAndUpdateDeno()` (used by the "Update Components" menu action) does **not** shell out at all: it only checks `File.Exists(denoPath)` as a local proxy - if the app's own managed `deno` binary isn't present, it assumes a system/external runtime is in use and skips the version-check/update entirely

**GetDenoAssetName() / DownloadDeno() / GetLatestDenoInfo()**
- Queries the GitHub API for the latest Deno release, picks the matching asset by name via `GetDenoAssetName()`:
  - Windows: `deno-x86_64-pc-windows-msvc.zip`
  - Linux x64: `deno-x86_64-unknown-linux-gnu.zip`; Linux ARM64: `deno-aarch64-unknown-linux-gnu.zip` (pattern: `deno-*-unknown-linux-gnu.zip`)
  - macOS x64: `deno-x86_64-apple-darwin.zip`; macOS ARM64: `deno-aarch64-apple-darwin.zip` (pattern: `deno-*-apple-darwin.zip`)
- Extracts the entry named `deno.exe` (Windows) or `deno` (Linux and macOS - verified the internal zip entry name is `deno` on macOS too, so no extraction-logic difference is needed) from the zip via `ZipFile` (Deno's release asset is always a zip on every OS, so this path doesn't need the `tar` handling FFmpeg needs)
- Calls `MakeExecutable(denoPath)` after extraction (no-op on Windows)
- Writes the installed version to `deno_version.txt` so `CheckAndUpdateDeno()` can detect updates later
- Handles errors gracefully via `MessageDialog`, with fallback instructions

**GetYtDlpAssetName() / DownloadYtDlp()**
- Downloads the latest yt-dlp release asset chosen by `GetYtDlpAssetName()` (direct file download, no unpacking - yt-dlp publishes single binaries per OS/arch):
  - Windows: `yt-dlp.exe`
  - Linux x64: `yt-dlp_linux`; Linux ARM64: `yt-dlp_linux_aarch64`
  - macOS: `yt-dlp_macos` - a single `universal2` binary covering both x64 and ARM64, not split by architecture like the Linux/Deno assets
- Calls `MakeExecutable(ytDlpPath)` after download, since a raw HTTP download has no executable bit on Linux/macOS

**IsMatchingFFmpegAsset() / DownloadFFmpeg() / GetLatestFFmpegInfo()** - Windows and Linux only; macOS uses a separate download path (see the next bullet)
- Uses the GitHub API to find the latest FFmpeg autobuild (BtbN/FFmpeg-Builds) and picks the release asset matching `IsMatchingFFmpegAsset()`:
  - Windows: name contains `win64-gpl-shared` and ends with `.zip`
  - Linux x64: ends with `linux64-gpl.tar.xz`; Linux ARM64: ends with `linuxarm64-gpl.tar.xz`
- **The Linux variant is deliberately the static `gpl` build, not `gpl-shared`.** BtbN's Windows `-shared` build puts `ffmpeg.exe`/`ffprobe.exe` and their `*.dll`s together in one `bin/` folder, which Windows' DLL search order resolves automatically. BtbN's Linux `-shared` build instead splits the executables (`bin/`) from their `*.so` libraries (`lib/`), which requires setting `LD_LIBRARY_PATH` (or embedding an rpath) for the executables to find their shared libraries at runtime. The static `gpl` build sidesteps that entirely: `ffmpeg`/`ffprobe` are single self-contained binaries with everything linked in, so the same "find `bin/`, copy every file in it into `ffmpeg_bin/`" logic that already works for Windows works unmodified for Linux, with no `lib/`-copying or environment-variable step needed
- Extracts via `ExtractArchive()`: on Windows, `ZipFile.ExtractToDirectory` (unchanged); on Linux, shells out to the system `tar` command (`tar -xf <archive> -C <destination>`) since the download is a `.tar.xz`, which `System.IO.Compression.ZipFile` cannot open
- After copying each file from the extracted `bin/` into `ffmpeg_bin/`, calls `MakeExecutable()` on it
- Stores version information in `ffmpeg_version.txt` for update checks

**GetFFmpegMacAssetName() / GetLatestFFmpegInfoMac() / DownloadFFmpegMac()**
- macOS FFmpeg comes from a different upstream entirely: `eugeneware/ffmpeg-static` (BtbN/FFmpeg-Builds does not publish macOS binaries)
- `GetFFmpegMacAssetName()` matches assets by **exact name equality**, not substring/suffix matching: `ffmpeg-darwin-x64`/`ffmpeg-darwin-arm64` and `ffprobe-darwin-x64`/`ffprobe-darwin-arm64`. Exact equality is required specifically because the real `eugeneware/ffmpeg-static` release also publishes `.gz`/`.LICENSE`/`.README` sidecar assets whose names contain the target asset name as a substring - a substring/suffix check would false-match those
- The two matched assets are raw executable binaries, not an archive - nothing is extracted; they're downloaded directly to `ffmpeg_bin/ffmpeg` and `ffmpeg_bin/ffprobe` and marked executable via the existing `MakeExecutable()` helper
- `DownloadFFmpeg()` and `CheckAndUpdateFFmpeg()` both branch to this path via `OperatingSystem.IsMacOS()` before reaching any BtbN-specific code

**AppPaths.MakeExecutable()**
- No-op on Windows. On Linux/macOS, calls `File.SetUnixFileMode()` to set `rwxr-xr-x` (user read/write/execute, group/other read/execute) on the given path. Needed because neither a plain HTTP download nor `ZipFile`/`tar` extraction preserves (or sets) the Unix executable bit, so every downloaded yt-dlp/Deno/FFmpeg binary would otherwise be non-executable on first run
- Moved out of `MainWindow` so `AppUpdater` can use it too

**DownloadFileWithProgress()**
- Shared helper: streams an HTTP download to disk while reporting progress on `ProgressBarDownload`

#### 4. Download Operations (`MainWindow.axaml.cs`)
**BtnDownload_Click()**
- Main download handler, wired to `BtnDownload.Click` in the constructor
- Validates every URL (see URL Handling) and dependency availability up front
- Iterates all URLs via `DownloadSingleUrlAsync()`, tracking a success count for multi-URL runs
- Reports an aggregate success/failure summary via `MessageDialog` when finished

**DownloadSingleUrlAsync()**
- Builds the yt-dlp command line for one URL and executes yt-dlp as an external process
- Parses real-time progress from stdout, handles errors and updates the UI

**BuildYtDlpArguments()**
- Constructs yt-dlp CLI arguments based on user selections
- Audio-only mode: `-f bestaudio --extract-audio --audio-format mp3 --audio-quality 192`
- Video modes: selects best video+audio combination with height constraints
- Format remuxing: `--remux-video` or `--merge-output-format`

**ParseDownloadProgress()**
- Parses yt-dlp output lines containing `[download]`
- Extracts: percentage, file size, download speed, ETA
- Updates the progress bar and status label in real time via regex

#### 5. URL Handling (`MainWindow.axaml.cs`)
**NormalizeUrl()**
- Adds `https://` prefix if missing
- Handles various YouTube URL formats (youtube.com, youtu.be, m.youtube.com)
- Converts 11-character video IDs to full URLs

**ValidateUrl()**
- Ensures the URL is not empty, contains a YouTube domain, and parses as a valid HTTP(S) URI
- Run for every URL row before a multi-URL download starts

#### 6. Update Mechanism (`MainWindow.axaml.cs`)
**AktualizujKomponenty_Click()**
- Menu handler for "Aktualizuj komponenty", wired to `MiAktualizujKomponenty.Click`
- Prompts for confirmation via `MessageDialog.ShowConfirmAsync()`
- Updates yt-dlp using self-update: `yt-dlp.exe -U`
- Delegates to `CheckAndUpdateDeno()` and `CheckAndUpdateFFmpeg()`
- Disables the download button during updates

**CheckAndUpdateDeno()**
- Compares the locally recorded Deno version (`deno_version.txt`) against the latest GitHub release
- Skips the check entirely when Deno isn't installed locally (a system-wide Deno/Node runtime is being used instead)
- Re-downloads Deno if a newer version is available

**CheckAndUpdateFFmpeg()**
- Compares local version with latest GitHub release
- Downloads and reinstalls if version mismatch, preserves the version tracking file

**CheckForAppUpdate(bool silent) / InstallAppUpdate()** (app self-update, logic in `AppUpdater.cs`)
- Runs silently at the end of `CheckAndDownloadComponents()` and loudly from `MiSprawdzAktualizacje`. Silent mode never shows errors (offline, GitHub API rate limit)
- Off inside a snap (`AppPaths.IsSnap` - the Snap Store updates it) and for local builds (no `BuildNumber` metadata)
- Compares `AppUpdater.GetLocalBuildNumber()` with the number after the last `-` in the latest release tag (`v<Version>-<run_number>`). `<Version>` itself (`2.0.ddMMyy`) is not monotonic and is never compared. Every CI build with a higher run number counts as a new version
- Picks the asset by exact name (`AppUpdater.GetAssetName()`): `YouTubeDownloader-{win-x64,win-arm64}.exe`, `YouTubeDownloader-{linux-x64,linux-arm64}`, `YouTubeDownloader-{osx-x64,osx-arm64}.zip`. A release still being published (asset missing) is treated as "no update yet"
- If the app folder is not writable, offers the release page in the browser instead
- Downloads to `<exe>.new` (Windows/Linux) or `YouTubeDownloader-update.zip` extracted to `YouTubeDownloader-update/` (macOS), verifies size (and SHA-256 when the API gives a `digest`), then `AppUpdater.ApplyUpdate()`: every existing target -> `.old`, every new file -> target, full rollback on any failure. The running exe's `.old` on Windows is removed by `AppUpdater.CleanupLeftovers()` on the next start
- Relaunches `Environment.ProcessPath` and shuts down. The exe file name is never assumed
- `SetBusy(true)` disables `BtnDownload`, `MiSprawdzAktualizacje` and `MiAktualizujKomponenty` for the whole of `CheckAndDownloadComponents()` (startup tool downloads), `AktualizujKomponenty_Click()`, video downloads and the self-update itself - so an update's final shutdown can never cut a component download short. The startup check runs after `SetBusy(false)` and skips itself whenever `BtnDownload` is disabled

#### 7. About & Message Dialogs
**AboutWindow** (`AboutWindow.axaml` / `.axaml.cs`)
- Modal window shown from `Informacje_Click()` (the `MiInformacje.Click` handler) via `ShowDialog(this)`
- Displays the app logo, name, version, and license, plus clickable repo/author links opened with `Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })`

**MessageDialog** (`MessageDialog.axaml` / `.axaml.cs`)
- Reusable modal that replaces WinForms `MessageBox`: `MessageDialog.ShowAsync(owner, message, title)` for info/error messages, `MessageDialog.ShowConfirmAsync(owner, message, title)` for Yes/No confirmations
- Used throughout `MainWindow.axaml.cs` for every user-facing error and confirmation

## Development Workflows

### Building the Application

```bash
# Restore dependencies and build
dotnet restore
dotnet build

# Build release version
dotnet build -c Release

# Run the application
dotnet run

# Publish single-file executable (x64)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Publish single-file executable (ARM64, e.g. Windows on ARM)
dotnet publish -c Release -r win-arm64 --self-contained true -p:PublishSingleFile=true

# Or use build.bat, which wraps the above (defaults to win-x64):
build.bat win-arm64

# Publish single-file executable (Linux x64)
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true

# Publish single-file executable (Linux ARM64)
dotnet publish -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=true

# Or use build.sh, which wraps the above (defaults to linux-x64):
./build.sh linux-arm64

# Publish single-file executable (macOS x64, Intel)
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true

# Publish single-file executable (macOS ARM64, Apple Silicon)
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true

# Or use build.sh, which wraps the above (defaults to linux-x64):
./build.sh osx-arm64

# Build all six RIDs in one run (either script, both cross-compile every RID
# from either OS): win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64
build.bat all
./build.sh all
```

CI (`.github/workflows/dotnet-desktop.yml`) builds and publishes all six RIDs - `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64` - on every push to `main`, running across `windows-latest`, `ubuntu-latest`, and `macos-latest` runners and attaching all six artifacts to the automated GitHub Release; the Linux jobs additionally run a headless launch smoke-test under `xvfb-run` (see Testing Changes below). The publish step passes `-p:BuildNumber=${{ github.run_number }}`, which the self-updater compares against release tags. Both the Linux and macOS smoke tests assert that the tools landed in the data folder (`~/.local/share/YouTubeDownloader`, `~/Library/Application Support/YouTubeDownloader`) and not next to the exe. Never run the workflow via `workflow_dispatch` from a feature branch - the publish job creates a real GitHub Release that the self-updater offers to every user. The `macos-latest` runner is ARM64, so the `osx-x64` leg builds via cross-compilation and is smoke-tested there through Rosetta 2, which CI installs explicitly as its own step since it is not preinstalled on the runner image. Dependency downloads and runtime detection are now OS-conditional rather than Windows-only: Windows targets `yt-dlp.exe`/`deno.exe`/the `win64-gpl-shared` FFmpeg build and shells out to `where`; Linux targets the `yt-dlp_linux*`/`deno-*-unknown-linux-gnu.zip`/static-`gpl` FFmpeg assets and shells out to `which`; macOS targets `yt-dlp_macos`/`deno-*-apple-darwin.zip`/the `eugeneware/ffmpeg-static` binaries and also shells out to `which` (see Dependency Management above for the full OS split).

### Testing Changes

1. **UI Changes**: Modify the relevant `.axaml` file (`MainWindow.axaml`, `AboutWindow.axaml`, or `MessageDialog.axaml`)
2. **Business Logic**: Update the relevant method in `MainWindow.axaml.cs` (dependency management, download orchestration, URL handling, or update mechanism)
3. **Dependency Management**: Modify the download methods in `MainWindow.axaml.cs` (`DownloadDeno()`, `DownloadYtDlp()`, `DownloadFFmpeg()`)
4. **Download Logic**: Update `BtnDownload_Click()` / `DownloadSingleUrlAsync()` and related methods

This environment has no full GUI test automation - there is no headless/CI-runnable UI *interaction* test suite on any OS. On Windows, verification is a successful `dotnet build` plus a manual smoke-launch of the app; actual UI rendering and yt-dlp/FFmpeg download behavior can only be confirmed by running the app locally (see Testing Checklist below). On Linux, CI additionally runs a headless launch-only smoke-test under `xvfb-run` on `ubuntu-latest`, which confirms the app starts without crashing but does not exercise real downloads or interactive UI - narrower coverage than the Windows manual pass, not a substitute for it. macOS gets the same category of coverage as Linux: a CI launch-and-dependency-download smoke-test on `macos-latest`, not a manual interactive QA pass. Unlike the Linux job, the macOS smoke-test does not use a virtual-display wrapper like `xvfb-run` - it assumes `macos-latest` provides a usable GUI session out of the box. That assumption is unconfirmed until a real CI run proves it out (see this plan's Global Constraints and Task 4).

### Debugging Tips

- Use Visual Studio or VS Code with C# extensions
- Set breakpoints in async methods to trace flow
- Monitor `LblStatus.Text` updates (via `UpdateStatus()`) for user feedback
- Check process output in the `OutputDataReceived` event handler
- Verify file paths in application directory

## Key Conventions

### Code Style
- **Nullable Reference Types**: Enabled (`<Nullable>enable</Nullable>`)
- **Implicit Usings**: Enabled for common namespaces
- **Naming**:
  - XAML-named elements: PascalCase (`TxtUrl`, `CbQuality`, `BtnDownload`) per Avalonia/XAML convention
  - Methods: PascalCase with descriptive names
  - Event handlers: `ControlName_EventType` pattern (e.g. `ContentType_Changed`, `BtnAddUrl_Click`)
- **Polish UI Text**: All user-facing strings are in Polish
  - "Pobierz" = Download
  - "Jakosc" = Quality
  - "Format" = Format
  - "Gotowy do pobierania" = Ready to download

### Avalonia UI Conventions
- **Framework**: Avalonia UI 12.1.2 (XAML + code-behind, no MVVM) - controls are declared in a `.axaml` file, and all logic lives in the paired `.axaml.cs` code-behind class
- `Name="TxtUrl"` in XAML generates a strongly-typed, **non-nullable** `TxtUrl` field on the code-behind class automatically - there is no manual field declaration step, unlike the old WinForms `private TextBox? txtUrl;` pattern
- Event handlers are wired up explicitly in the constructor, after `InitializeComponent()`, e.g. `BtnDownload.Click += async (s, e) => await BtnDownload_Click();` - **not** via XAML `Click="..."` attributes. This keeps XAML parsing and code-behind compilation order independent and is the pattern used throughout this codebase
- Property renames from WinForms: `IsEnabled` replaces `Enabled`; `ComboBox.SelectionChanged` replaces `ComboBox.SelectedIndexChanged`

### Async Patterns
- All I/O operations use `async/await`
- File downloads use `HttpClient` with progress reporting
- Process execution uses `WaitForExitAsync()`
- UI updates from background threads use `Dispatcher.UIThread.Post()` (Avalonia), not WinForms' `Invoke()`

### Error Handling
- Try-catch blocks around all external operations
- `MessageDialog.ShowAsync()` / `ShowConfirmAsync()` for user-facing errors and confirmations (replaces WinForms `MessageBox.Show()`)
- Status label (`LblStatus`, via `UpdateStatus()`) updates for progress and errors
- Graceful fallbacks for missing dependencies

### Process Execution
- `ProcessStartInfo` with redirected output streams
- `CreateNoWindow = true` for background processes
- `UseShellExecute = false` for output capture
- Event-based output parsing with `OutputDataReceived`

## Important Implementation Details

### Security Considerations
1. **Command Injection Prevention**: URL and path arguments are quoted and validated
2. **File Path Validation**: All paths use `Path.Combine()` for safety
3. **GitHub API**: Uses User-Agent header as required by GitHub API
4. **HTTPS Only**: All downloads use HTTPS (GitHub, yt-dlp)

### Dependency Versions
- **yt-dlp**: Always latest from GitHub releases (Windows: `yt-dlp.exe`; Linux: `yt-dlp_linux`/`yt-dlp_linux_aarch64`; macOS: `yt-dlp_macos`, a single `universal2` binary for both architectures)
- **FFmpeg**: Windows/Linux - latest autobuild from BtbN/FFmpeg-Builds (Windows: `win64-gpl-shared`; Linux: static `gpl` build - `linux64-gpl.tar.xz`/`linuxarm64-gpl.tar.xz`, see Dependency Management above for why static rather than shared). macOS - latest release from `eugeneware/ffmpeg-static` (BtbN does not publish macOS builds): loose binaries `ffmpeg-darwin-x64`/`ffmpeg-darwin-arm64` and `ffprobe-darwin-x64`/`ffprobe-darwin-arm64`, downloaded directly with no archive/extraction step
- **Deno**: Latest release from denoland/deno (Windows: `x86_64-pc-windows-msvc`; Linux: `x86_64-unknown-linux-gnu`/`aarch64-unknown-linux-gnu`; macOS: `x86_64-apple-darwin`/`aarch64-apple-darwin`)
- **Runtime Detection**: Checks for a system Deno/Node install in PATH using `where deno` (Windows) or `which deno` (Linux/macOS)

### yt-dlp Integration
The application passes specific arguments based on user selections:

**Audio-only (MP3)**:
```
-f bestaudio --extract-audio --audio-format mp3 --audio-quality 192
```

**Video (Best quality)**:
```
-f bestvideo+bestaudio/best --remux-video mp4
```

**Video (Specific resolution)**:
```
-f bestvideo[height<=1080]+bestaudio/best[height<=1080] --remux-video mp4
```

Common arguments:
- `--ffmpeg-location "path/to/ffmpeg_bin"` - FFmpeg location
- `--progress --newline` - Progress reporting
- `-o "downloads/%(title)s.%(ext)s"` - Output pattern
- `--js-runtimes node` - If using Node.js instead of Deno

### Progress Parsing
The application parses yt-dlp output using regex patterns:
- Percentage: `([0-9]+(?:\.[0-9]+)?)%`
- File size: `of\s+([0-9.]+)(MiB|GiB|KiB|B)`
- Speed: `at\s+([0-9.]+)(MiB|GiB|KiB|B)/s`
- ETA: `ETA\s+([0-9]{2}:[0-9]{2}:[0-9]{2}|[0-9]{2}:[0-9]{2})`

## Working with This Codebase

### Adding New Features

1. **New Download Options**:
   - Add to the `CbQuality`/`CbFormat` item lists in the `MainWindow()` constructor (`MainWindow.axaml.cs`)
   - Update `BuildYtDlpArguments()` to handle the new option

2. **New UI Elements**:
   - Add the element to the appropriate `.axaml` file with a `Name` - Avalonia's XAML compiler generates the code-behind field automatically, no manual field declaration needed
   - Wire any event handlers in the constructor after `InitializeComponent()`

3. **New Dependency**:
   - Add a download method following the `DownloadDeno()` pattern
   - Call it from `CheckAndDownloadComponents()`
   - Add version tracking if needed
   - Download it into `dataDirectory`, never `appDirectory`

### Modifying Existing Features

1. **Change Download Location**:
   - The `downloadsDir` computation (`Path.Combine(appDirectory, "downloads")`) appears in both `BtnDownload_Click()` and `DownloadSingleUrlAsync()` in `MainWindow.axaml.cs` - update both
   - Update the user-facing success message that reports the location

2. **Update UI Text/Language**:
   - Static text (labels, button content) lives in the `.axaml` files (e.g. `Content="Pobierz"`)
   - Dynamic text (status/error messages) is inline in `MainWindow.axaml.cs`, passed to `UpdateStatus()` and `MessageDialog`
   - Update the window `Title` in `MainWindow.axaml`

3. **Change Quality Options**:
   - Modify the `CbQuality.Items.Add(...)` calls in the `MainWindow()` constructor
   - Update the quality branches in `BuildYtDlpArguments()`

### Common Tasks

**Adding a new menu item**:
```xml
<!-- In MainWindow.axaml, inside the relevant <MenuItem Header="_Narzedzia"> or <MenuItem Header="_Pomoc"> -->
<MenuItem Name="MiNewFeature" Header="_Nowa funkcja"/>
```
```csharp
// In the MainWindow() constructor, after InitializeComponent():
MiNewFeature.Click += async (s, e) => await NewFeature_Click();
```

**Updating status message**:
```csharp
UpdateStatus("Your status message here");
```

**Executing external command**:
```csharp
var processInfo = new ProcessStartInfo
{
    FileName = "executable.exe",
    Arguments = "args",
    UseShellExecute = false,
    RedirectStandardOutput = true,
    CreateNoWindow = true
};
using (var process = Process.Start(processInfo))
{
    await process.WaitForExitAsync();
}
```

## AI Assistant Guidelines

### When Adding Features
1. **Keep the existing file split** - UI in the appropriate `.axaml`/`.axaml.cs` pair; avoid introducing new architectural layers (e.g. MVVM/ViewModels) unless the user asks for it
2. **Follow existing async/await patterns** for all I/O operations
3. **Use Polish text** for user-facing messages (or ask user for preferred language)
4. **Update progress indicators** for long-running operations
5. **Handle errors gracefully** with `MessageDialog` and status updates
6. **Test dependency availability** before executing external tools

### When Fixing Bugs
1. **Remember XAML-named elements are non-nullable** - Avalonia generates strongly-typed fields for every `Name`, so defensive null checks on them are unnecessary (unlike the old WinForms `txtUrl?` pattern); still apply normal nullable-reference-type discipline to everything else
2. **Verify paths** - ensure `Path.Combine()` usage
3. **Test process execution** - check redirected output handling
4. **Validate regex patterns** - test with actual yt-dlp output
5. **Consider both Windows and Linux specifics** when touching dependency-management or process-execution code - file paths, process commands, and the OS-conditional branches (`OperatingSystem.IsWindows()`, per-OS asset names/extraction - see Dependency Management above)

### When Refactoring
1. **Preserve Avalonia patterns** - event handlers wired in the constructor (not XAML `Click=` attributes), `Dispatcher.UIThread.Post()` for cross-thread UI updates
2. **Keep UI responsive** - use async for long operations
3. **Maintain backward compatibility** - existing downloads folder, configs
4. **Test with actual downloads** - yt-dlp behavior can change
5. **Document changes** - update this CLAUDE.md file

### Code Quality Standards
- **No compiler warnings**: Fix all nullable warnings
- **Proper disposal**: Use `using` statements for IDisposable objects
- **Exception handling**: Catch specific exceptions where possible
- **User feedback**: Always inform user of operation status
- **Layout**: Use Avalonia layout containers (`Grid`, `StackPanel`, `DockPanel`) with `HorizontalAlignment`/`Margin`/`Width`, matching the patterns already used in `MainWindow.axaml`
- **No GUI test automation exists here**: verification is `dotnet build` plus a manual smoke-launch (see Testing Checklist) - don't claim a UI or download change "works" without that, or without the user confirming

## Testing Checklist

This project has no full GUI interaction test automation. There is no headless/CI-runnable UI interaction test suite, so verification of anything below beyond a clean build requires a manual smoke-launch and QA pass on Windows; on Linux, CI covers only a headless launch-only smoke-test (see Testing Changes above), so most items below are effectively Windows-verified only unless someone confirms them manually on Linux too. macOS has the same coverage shape as Linux - a CI-only launch-and-dependency-download smoke-test on `macos-latest`, not a manual interactive QA pass - so most items below are effectively Windows-verified only unless someone confirms them manually on macOS as well.

Before committing changes, verify:
- [ ] Application builds without warnings
- [ ] UI loads correctly and all controls are visible (manual smoke-launch)
- [ ] Dependencies download successfully on first run
- [ ] Video+Audio download works for various qualities
- [ ] Audio-only download produces MP3 file
- [ ] Multiple URLs (added via the `+` button) all download, with an aggregate success/failure summary shown at the end
- [ ] Progress bar updates during download
- [ ] Error/confirmation dialogs (`MessageDialog`) display correctly
- [ ] Update components menu item functions
- [ ] About dialog opens and its repo/author links work
- [ ] URL validation accepts valid YouTube URLs
- [ ] URL normalization handles edge cases
- [ ] Files save to downloads directory
- [ ] Process cleanup occurs on errors
- [ ] Tools download into the data folder, not next to the exe; an old install's copies next to the exe are removed
- [ ] Theme choice survives a restart (theme.txt in the data folder)
- [ ] "Sprawdz aktualizacje aplikacji" on a local build shows the "aktualizacje wylaczone" message
- [ ] A CI build older than the latest release offers the update, installs it and restarts

## External Dependencies

### Runtime Dependencies (Auto-downloaded)
- **yt-dlp**: YouTube video/audio downloader
  - Source: https://github.com/yt-dlp/yt-dlp
  - License: Unlicense

- **FFmpeg**: Audio/video processing
  - Source (Windows/Linux): https://github.com/BtbN/FFmpeg-Builds
    - License: GPL (Windows: `gpl-shared` build; Linux: static `gpl` build - see Dependency Management above)
  - Source (macOS): https://github.com/eugeneware/ffmpeg-static
    - License: GPL (BtbN does not publish macOS builds; this is a separately-maintained project also used as the `ffmpeg-static` npm package)

- **Deno**: JavaScript/TypeScript runtime for yt-dlp
  - Source: https://github.com/denoland/deno
  - License: MIT
  - Alternative: Node.js (if available in PATH)

### NuGet Dependencies
Three pinned Avalonia 12.1.2 packages (see `YouTubeDownloader.csproj`):
- `Avalonia` (12.1.2) - core framework
- `Avalonia.Desktop` (12.1.2) - desktop platform backend
- `Avalonia.Themes.Fluent` (12.1.2) - the `FluentTheme` registered in `App.axaml`

Future dependency/CVE audits must cover these NuGet packages in addition to the external yt-dlp/FFmpeg/Deno binaries listed above.

### App Version

`<Version>` in `YouTubeDownloader.csproj` is the single source of truth for the app's version number (format: `Major.Minor.<ddMMyy>`, e.g. `2.0.110926` for 2026-09-11 - a date-based build number, not semantic versioning). `AssemblyVersion`/`FileVersion` are pinned separately to `2.0.0.0` in the same file, since .NET requires each of their four components to fit in 16 bits (max 65535) and a `ddMMyy` build number regularly exceeds that - only `AssemblyInformationalVersionAttribute` (populated from `<Version>` with no such limit) can hold the full date-based string. `AboutWindow.axaml.cs`'s `GetAppVersion()` reads that attribute via reflection at runtime, so the About dialog always matches `<Version>` without a second manual edit. CI's `publish` job reads the same property (`dotnet msbuild ... -getProperty:Version`) to name the GitHub Release (`v<version>-<run_number>`) - bump `<Version>` here and every consumer (About dialog, release tag/name) picks it up automatically; `README.md`'s version badge and "What's New" section are the one place that still needs a manual edit per release, since they're prose, not something a build step can regenerate.

Separately, CI passes `github.run_number` as the MSBuild property `BuildNumber`, which `YouTubeDownloader.csproj` turns into `AssemblyMetadata("BuildNumber", ...)` only when set. That number - not `<Version>` - is what the self-updater compares, and the About dialog shows it as "(build N)". Local builds have no build number, so self-update is off for them.

## macOS Gatekeeper Notice

The macOS builds of this application are **not code-signed or notarized** (that requires a paid Apple Developer Program account, which this project does not currently have). On first launch, macOS Gatekeeper will refuse to open the downloaded binary with a warning that it is "from an unidentified developer" or "cannot be verified." Users must explicitly allow it once:

1. **Recommended (works on macOS 15 Sequoia and later, and on older versions too)**: attempt to open the app - it will be blocked - then go to **System Settings -> Privacy & Security**, scroll down to the blocked-app notice, and click **"Open Anyway"**. (The older right-click/Control-click -> "Open" workaround no longer shows an "Open anyway" option starting with macOS 15 Sequoia, since this project's CI already targets `macos-latest` - a Sequoia-class image - this is the method most readers will need.)
2. **Terminal alternative**: clear the quarantine attribute directly: `xattr -d com.apple.quarantine <path-to-binary>`.

This is a one-time step per download; it does not need to be repeated on subsequent launches of the same binary. Separately, note that the release `.zip` asset (built via `Compress-Archive` in CI) does not preserve the Unix executable bit - after unzipping on macOS or Linux, run `chmod +x <path-to-binary>` before the binary can be launched at all, regardless of the Gatekeeper step above.

## Useful References

- [yt-dlp Documentation](https://github.com/yt-dlp/yt-dlp#readme)
- [FFmpeg Documentation](https://ffmpeg.org/documentation.html)
- [Avalonia UI Documentation](https://docs.avaloniaui.net/)
- [.NET 10 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10)
- [C# Async/Await Patterns](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/)

## Git Workflow

This project follows standard Git practices:
- **Branch**: Work on feature branches (e.g., `claude/feature-name-sessionid`)
- **Commits**: Use descriptive commit messages in English
- **Push**: Always push to your feature branch with retry logic
- **Pull Requests**: Target the main branch when ready


## Future Improvements to Consider

1. **Multi-language Support**: Resource files for UI text
2. **Settings Persistence**: Save quality/format preferences
3. **Playlist Support**: Download entire playlists
4. **Custom Output Directory**: Let users choose download location
5. **Thumbnail Preview**: Show video thumbnail before download
6. **Download Queue**: Support multiple simultaneous downloads
7. **Format Conversion**: Post-download conversion options
8. **Portable Mode**: Config file for portable installations
9. **Subtitle Download**: Option to download subtitles/captions

---

**Last Updated**: 2026-09-28
**For**: AI Assistants (Claude, etc.)
**Project**: YouTube Downloader for Windows, Linux, and macOS (.NET 10)
