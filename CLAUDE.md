# CLAUDE.md - YouTube Downloader Project Guide

## Project Overview

**YouTube Downloader** is a Windows desktop application built with .NET 9 and Windows Forms that enables users to download videos and audio from YouTube. The application automatically manages its dependencies (yt-dlp, FFmpeg, and Deno runtime) and provides a user-friendly Polish-language interface for selecting download quality and format.

### Key Information
- **Technology Stack**: .NET 9, C# 13, Windows Forms
- **Target Platform**: Windows (net9.0-windows)
- **License**: Apache License 2.0
- **Primary Language**: C# with Polish UI text
- **Architecture**: Single-file Windows Forms application with external dependency management

## Repository Structure

```
YouTube-Downloader/
├── MainForm.cs                 # Main application - Windows Form with all logic
├── YouTubeDownloader.csproj    # .NET 9 project configuration
├── README.md                   # Project documentation
├── LICENSE                     # Apache 2.0 license
└── CLAUDE.md                   # This file - AI assistant guide
```

### Runtime Structure (Created at Runtime)
```
Application Directory/
├── yt-dlp.exe                  # YouTube downloader CLI tool
├── deno.exe                    # Deno runtime (auto-downloaded if not in PATH)
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

### Single-File Design
The entire application logic resides in `MainForm.cs` (~809 lines), which contains:
- **MainForm class**: Primary Windows Form with UI and business logic
- **Program class**: Application entry point with STAThread configuration

### Key Components

#### 1. UI Components (Lines 16-23, 44-136)
- `txtUrl`: TextBox for YouTube URL input
- `cbContentType`: ComboBox for Video+Audio or Audio-only selection
- `cbQuality`: ComboBox for quality selection (Best, 4K, 1080p, 720p, 480p, 360p, 240p)
- `cbFormat`: ComboBox for format selection (mp4, webm, mkv)
- `progressBar`: ProgressBar for download progress
- `lblStatus`: Label for status messages and real-time progress
- `menuStrip`: Menu bar with component update functionality

#### 2. Dependency Management (Lines 152-368)
**CheckAndDownloadComponents()** - Lines 152-173
- Checks for Deno/Node.js runtime availability
- Downloads yt-dlp if missing
- Downloads FFmpeg if missing
- All operations are async and report progress

**DownloadDeno()** - Lines 201-252
- Queries GitHub API for latest Deno release
- Downloads Windows x86_64 MSVC build
- Extracts deno.exe from zip archive
- Handles errors gracefully with fallback instructions

**DownloadYtDlp()** - Lines 254-266
- Downloads latest yt-dlp.exe from GitHub releases
- Direct file download without unpacking

**DownloadFFmpeg()** - Lines 314-368
- Uses GitHub API to find latest FFmpeg autobuild
- Downloads win64-gpl-shared build
- Extracts bin directory contents
- Stores version information for update checks

#### 3. Download Operations (Lines 664-789)
**BtnDownload_Click()** - Lines 664-789
- Main download handler triggered by button click
- Validates URL format and dependencies
- Builds yt-dlp command with quality/format arguments
- Executes yt-dlp as external process
- Parses real-time progress from stdout
- Handles errors and updates UI accordingly

**BuildYtDlpArguments()** - Lines 616-662
- Constructs yt-dlp CLI arguments based on user selections
- Audio-only mode: `-f bestaudio --extract-audio --audio-format mp3 --audio-quality 192`
- Video modes: Selects best video+audio combination with height constraints
- Format remuxing: `--remux-video` or `--merge-output-format`

**ParseDownloadProgress()** - Lines 573-614
- Parses yt-dlp output lines containing `[download]`
- Extracts: percentage, file size, download speed, ETA
- Updates progress bar and status label in real-time
- Uses regex for robust parsing

#### 4. URL Handling (Lines 411-458)
**NormalizeUrl()** - Lines 411-432
- Adds `https://` prefix if missing
- Handles various YouTube URL formats (youtube.com, youtu.be, m.youtube.com)
- Converts 11-character video IDs to full URLs
- Returns properly formatted URL

**ValidateUrl()** - Lines 434-458
- Ensures URL is not empty
- Verifies URL contains YouTube domain
- Validates URI format and scheme (HTTP/HTTPS)
- Provides user-friendly error messages

#### 5. Update Mechanism (Lines 460-533)
**AktualizujKomponenty_Click()** - Lines 460-501
- Menu item handler for "Update Components"
- Updates yt-dlp using self-update: `yt-dlp.exe -U`
- Checks and updates FFmpeg if newer version available
- Disables download button during updates

**CheckAndUpdateFFmpeg()** - Lines 503-533
- Compares local version with latest GitHub release
- Downloads and reinstalls if version mismatch
- Preserves version tracking file

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

# Publish single-file executable
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### Testing Changes

1. **UI Changes**: Modify `InitializeComponent()` method (lines 44-136)
2. **Business Logic**: Update relevant methods in MainForm class
3. **Dependency Management**: Modify download methods (lines 152-368)
4. **Download Logic**: Update `BtnDownload_Click()` and related methods

### Debugging Tips

- Use Visual Studio or VS Code with C# extensions
- Set breakpoints in async methods to trace flow
- Monitor `lblStatus` updates for user feedback
- Check process output in `OutputDataReceived` event handler
- Verify file paths in application directory

## Key Conventions

### Code Style
- **Nullable Reference Types**: Enabled (`<Nullable>enable</Nullable>`)
- **Implicit Usings**: Enabled for common namespaces
- **Naming**:
  - Fields: camelCase with nullable suffix (`txtUrl?`, `btnDownload?`)
  - Methods: PascalCase with descriptive names
  - Event handlers: `ControlName_EventType` pattern
- **Polish UI Text**: All user-facing strings are in Polish
  - "Pobierz" = Download
  - "Jakosc" = Quality
  - "Format" = Format
  - "Gotowy do pobierania" = Ready to download

### Async Patterns
- All I/O operations use `async/await`
- File downloads use `HttpClient` with progress reporting
- Process execution uses `WaitForExitAsync()`
- UI updates use `Invoke()` from background threads

### Error Handling
- Try-catch blocks around all external operations
- `MessageBox` for user-facing errors
- Status label updates for progress and errors
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
   - Add to `cbQuality` or `cbFormat` initialization (lines 84-101)
   - Update `BuildYtDlpArguments()` to handle new option

2. **New UI Elements**:
   - Declare field in class (lines 16-29)
   - Initialize in `InitializeComponent()` (lines 44-136)
   - Add to `this.Controls.Add()` collection

3. **New Dependency**:
   - Add download method following `DownloadDeno()` pattern
   - Call from `CheckAndDownloadComponents()`
   - Add version tracking if needed

### Modifying Existing Features

1. **Change Download Location**:
   - Modify `downloadsDir` variable in `BtnDownload_Click()` (line 707)
   - Update user message with new location (line 767)

2. **Update UI Text/Language**:
   - All text is inline in `InitializeComponent()` and methods
   - Search and replace Polish text with desired language
   - Update window title in line 46

3. **Change Quality Options**:
   - Modify `cbQuality.Items.AddRange()` (line 88)
   - Update switch cases in `BuildYtDlpArguments()` (lines 625-645)

### Common Tasks

**Adding a new menu item**:
```csharp
ToolStripMenuItem newItem = new ToolStripMenuItem("Menu Text", null, NewItem_Click);
narzedziaMenu.DropDownItems.Add(newItem);
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
1. **Maintain single-file structure** unless absolutely necessary to split
2. **Follow existing async/await patterns** for all I/O operations
3. **Use Polish text** for user-facing messages (or ask user for preferred language)
4. **Update progress indicators** for long-running operations
5. **Handle errors gracefully** with MessageBox and status updates
6. **Test dependency availability** before executing external tools

### When Fixing Bugs
1. **Check null safety** - all UI controls are nullable
2. **Verify paths** - ensure Path.Combine() usage
3. **Test process execution** - check redirected output handling
4. **Validate regex patterns** - test with actual yt-dlp output
5. **Consider Windows specifics** - file paths, process commands

### When Refactoring
1. **Preserve Windows Forms patterns** - event handlers, Invoke() for threading
2. **Keep UI responsive** - use async for long operations
3. **Maintain backward compatibility** - existing downloads folder, configs
4. **Test with actual downloads** - yt-dlp behavior can change
5. **Document changes** - update this CLAUDE.md file

### Code Quality Standards
- **No compiler warnings**: Fix all nullable warnings
- **Proper disposal**: Use `using` statements for IDisposable objects
- **Exception handling**: Catch specific exceptions where possible
- **User feedback**: Always inform user of operation status
- **Accessibility**: Use AutoSize and proper anchoring for UI elements

## Testing Checklist

Before committing changes, verify:
- [ ] Application builds without warnings
- [ ] UI loads correctly and all controls are visible
- [ ] Dependencies download successfully on first run
- [ ] Video+Audio download works for various qualities
- [ ] Audio-only download produces MP3 file
- [ ] Progress bar updates during download
- [ ] Error messages display correctly
- [ ] Update components menu item functions
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
None - project uses only .NET 9 BCL (Base Class Library)

## Useful References

- [yt-dlp Documentation](https://github.com/yt-dlp/yt-dlp#readme)
- [FFmpeg Documentation](https://ffmpeg.org/documentation.html)
- [Windows Forms Documentation](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/)
- [.NET 9 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9)
- [C# Async/Await Patterns](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/)

## Git Workflow

This project follows standard Git practices:
- **Branch**: Work on feature branches (e.g., `claude/feature-name-sessionid`)
- **Commits**: Use descriptive commit messages in English
- **Push**: Always push to your feature branch with retry logic
- **Pull Requests**: Target the main branch when ready

### Current Branch
As of this documentation, development is on:
```
claude/create-codebase-documentation-01DjxCrHzAFWa6dothenfX9q
```

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

**Last Updated**: 2025-11-14
**For**: AI Assistants (Claude, etc.)
**Project**: YouTube Downloader for Windows (.NET 9)
