# CLAUDE.md - YouTube Downloader Project Guide

## Project Overview

**YouTube Downloader** is a Windows desktop application built with .NET 10 and Avalonia UI that enables users to download videos and audio from YouTube, including from multiple links in a single run. The application automatically manages its dependencies (yt-dlp, FFmpeg, and Deno runtime) and provides a user-friendly Polish-language interface for selecting download quality and format.

### Key Information
- **Technology Stack**: .NET 10, C# 13, Avalonia UI 12.1.2
- **Target Platform**: Windows (net10.0)
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
├── Assets/
│   └── app-logo.png             # Application logo, shown in About and message dialogs
├── YouTubeDownloader.csproj     # .NET 10 project configuration (Avalonia packages)
├── app.ico                      # Application/window icon
├── logo.svg                     # Source application logo (SVG)
├── build.bat                    # Wraps `dotnet publish` for win-x64/win-arm64
├── README.md                    # Project documentation
├── LICENSE                      # Apache 2.0 license
└── CLAUDE.md                    # This file - AI assistant guide
```

### Runtime Structure (Created at Runtime)
```
Application Directory/
├── yt-dlp.exe                  # YouTube downloader CLI tool
├── deno.exe                    # Deno runtime (auto-downloaded if not in PATH)
├── deno_version.txt            # Tracks installed Deno version for update checks
├── node.exe                    # Alternative Node.js runtime
├── ffmpeg_bin/                 # FFmpeg binaries directory
│   ├── ffmpeg.exe
│   ├── ffprobe.exe
│   └── *.dll                   # FFmpeg shared libraries
├── ffmpeg_version.txt          # Tracks current FFmpeg version
└── downloads/                  # Default download location
    └── (downloaded videos)
```

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
Named elements (`x:Name`) become strongly-typed, non-nullable code-behind fields automatically - see Key Conventions:
- `TxtUrl`: TextBox for the first/primary YouTube URL
- `BtnAddUrl` / `ExtraUrlRowsPanel`: adds dynamic extra URL rows for multi-URL downloads
- `CbContentType`: ComboBox for Video+Audio or Audio-only selection
- `CbQuality`: ComboBox for quality selection (Best, 4K, 1080p, 720p, 480p, 360p, 240p)
- `CbFormat`: ComboBox for format selection (mp4, webm, mkv)
- `BtnDownload`: triggers the download
- `ProgressBarDownload`: ProgressBar for download progress
- `LblStatus`: TextBlock for status messages and real-time progress
- `MiAktualizujKomponenty` / `MiInformacje`: menu items for "Aktualizuj komponenty" (Update Components) / "Informacje" (About)

#### 2. Multi-URL Handling (`MainWindow.axaml.cs`)
**BtnAddUrl_Click() / RemoveUrlRow()**
- Dynamically add/remove extra URL rows (TextBox + remove Button) inside `ExtraUrlRowsPanel`, tracked in the `extraUrlBoxes` / `removeUrlButtons` lists

**GetAllUrls()**
- Collects and trims the primary URL plus every extra row, filtering out blanks

**SetUrlRowsEnabled()**
- Enables/disables all URL inputs and the add button while a download is running

#### 3. Dependency Management (`MainWindow.axaml.cs`)
**CheckAndDownloadComponents()**
- Checks for Deno/Node.js runtime availability (`IsRuntimeInPath()`)
- Downloads yt-dlp if missing, FFmpeg if missing
- All operations are async and report progress via `UpdateStatus()`

**DownloadDeno() / GetLatestDenoInfo()**
- Queries the GitHub API for the latest Deno release
- Downloads the Windows x86_64 MSVC build, extracts `deno.exe` from the zip
- Writes the installed version to `deno_version.txt` so `CheckAndUpdateDeno()` can detect updates later
- Handles errors gracefully via `MessageDialog`, with fallback instructions

**DownloadYtDlp()**
- Downloads the latest `yt-dlp.exe` from GitHub releases (direct file download, no unpacking)

**DownloadFFmpeg() / GetLatestFFmpegInfo()**
- Uses the GitHub API to find the latest FFmpeg autobuild (BtbN/FFmpeg-Builds)
- Downloads the `win64-gpl-shared` build, extracts the `bin` directory contents
- Stores version information in `ffmpeg_version.txt` for update checks

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
```

CI (`.github/workflows/dotnet-desktop.yml`) builds and publishes both `win-x64` and `win-arm64` on every push to `main`, attaching both to the automated GitHub Release. The app remains Windows-only in practice even though Avalonia itself is cross-platform: dependency downloads target Windows binaries (`yt-dlp.exe`, `deno.exe`, Windows FFmpeg builds) and runtime detection shells out to the Windows-only `where` command.

### Testing Changes

1. **UI Changes**: Modify the relevant `.axaml` file (`MainWindow.axaml`, `AboutWindow.axaml`, or `MessageDialog.axaml`)
2. **Business Logic**: Update the relevant method in `MainWindow.axaml.cs` (dependency management, download orchestration, URL handling, or update mechanism)
3. **Dependency Management**: Modify the download methods in `MainWindow.axaml.cs` (`DownloadDeno()`, `DownloadYtDlp()`, `DownloadFFmpeg()`)
4. **Download Logic**: Update `BtnDownload_Click()` / `DownloadSingleUrlAsync()` and related methods

This environment has no GUI test automation - there is no headless/CI-runnable UI test suite. Verification here is a successful `dotnet build` plus a manual smoke-launch of the app; actual UI rendering and yt-dlp/FFmpeg download behavior can only be confirmed by running the app on Windows (see Testing Checklist below).

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
- `x:Name="TxtUrl"` in XAML generates a strongly-typed, **non-nullable** `TxtUrl` field on the code-behind class automatically - there is no manual field declaration step, unlike the old WinForms `private TextBox? txtUrl;` pattern
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
- **yt-dlp**: Always latest from GitHub releases
- **FFmpeg**: Latest autobuild from BtbN/FFmpeg-Builds (win64-gpl-shared)
- **Deno**: Latest release from denoland/deno (x86_64-pc-windows-msvc)
- **Runtime Detection**: Checks for Deno in PATH using `where deno` command

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
   - Add the element to the appropriate `.axaml` file with an `x:Name` - Avalonia's XAML compiler generates the code-behind field automatically, no manual field declaration needed
   - Wire any event handlers in the constructor after `InitializeComponent()`

3. **New Dependency**:
   - Add a download method following the `DownloadDeno()` pattern
   - Call it from `CheckAndDownloadComponents()`
   - Add version tracking if needed

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
1. **Remember XAML-named elements are non-nullable** - Avalonia generates strongly-typed fields for every `x:Name`, so defensive null checks on them are unnecessary (unlike the old WinForms `txtUrl?` pattern); still apply normal nullable-reference-type discipline to everything else
2. **Verify paths** - ensure `Path.Combine()` usage
3. **Test process execution** - check redirected output handling
4. **Validate regex patterns** - test with actual yt-dlp output
5. **Consider Windows specifics** - file paths, process commands; the app is still Windows-only in practice (see Development Workflows)

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

This project has no GUI test automation in this environment. There is no headless/CI-runnable UI test suite, so verification of anything below beyond a clean build requires a manual smoke-launch and QA pass on Windows.

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

## External Dependencies

### Runtime Dependencies (Auto-downloaded)
- **yt-dlp**: YouTube video/audio downloader
  - Source: https://github.com/yt-dlp/yt-dlp
  - License: Unlicense

- **FFmpeg**: Audio/video processing
  - Source: https://github.com/BtbN/FFmpeg-Builds
  - License: GPL (gpl-shared build)

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
9. **Update Notifications**: Check for application updates
10. **Subtitle Download**: Option to download subtitles/captions

---

**Last Updated**: 2026-09-09
**For**: AI Assistants (Claude, etc.)
**Project**: YouTube Downloader for Windows (.NET 10)
