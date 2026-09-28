# User Data Directory + App Self-Update Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move everything the app downloads or writes (yt-dlp, FFmpeg, Deno, version files, `theme.txt`) out of the executable's folder into a per-user data directory, then let the app update itself from this repo's GitHub Releases.

**Architecture:** Two new Avalonia-free static classes carry all the logic: `AppPaths.cs` (app folder vs data folder, legacy-file cleanup, file helpers) and `AppUpdater.cs` (release lookup, download verification, two-phase file swap with rollback, relaunch). `MainWindow.axaml.cs` only wires them to the UI (menu item, dialogs, progress bar). CI passes `github.run_number` into the build as `BuildNumber` assembly metadata, because the date-based `<Version>` (`2.0.ddMMyy`) is not monotonic and cannot be compared.

**Tech Stack:** .NET 10, C# 13, Avalonia UI 12.1.2 (unchanged). No new NuGet packages - `System.Text.Json`, `System.Security.Cryptography.SHA256`, `System.IO.Compression.ZipFile`, `HttpClient` are all in the BCL.

**Spec:** `docs/superpowers/specs/2026-09-28-data-dir-and-self-update-design.md`

## Global Constraints

- Branch: `claude/data-dir-self-update`. The spec file is still uncommitted when this plan starts - Task 1 Step 0 creates the branch and commits it first.
- `downloads/` stays next to the executable (`appDirectory`) - do not move it.
- Data directory: `$SNAP_USER_COMMON` if set, else `Environment.SpecialFolder.LocalApplicationData` + `YouTubeDownloader`; fallback to the app folder if that is empty or cannot be created.
- `AppPaths.cs` and `AppUpdater.cs` must not reference any `Avalonia.*` namespace - they are compiled into a scratch test harness outside the repo.
- **No test project in the repo** (spec decision). Tests live in a scratch harness at `C:\Users\pawel\AppData\Local\Temp\claude\E--claude-YouTube-Downloader\004f1ae7-05cc-4f13-9798-d98989b0ce7c\scratchpad\ytd-tests` (called `$HARNESS` below). Never commit it, never add it to the repo.
- Every deletion targets exact, app-specific names only - never wildcards (`*.old`) and never generic names (`update.zip`). The app folder may be the user's Downloads folder.
- The running executable's path always comes from `Environment.ProcessPath`; its file name is never assumed (users run `YouTubeDownloader-win-x64.exe`, the release asset name).
- New user-facing strings are Polish **without diacritics**, matching the rest of the UI (`Blad`, `Narzedzia`, `Sprawdz aktualizacje aplikacji`).
- Event handlers are wired in the `MainWindow()` constructor, never via XAML `Click=`.
- `dotnet build` must stay at 0 warnings, 0 errors in Debug and Release.
- **Never trigger the CI workflow via `workflow_dispatch` from this branch** - its `publish` job creates a real, public GitHub Release that the new updater would then offer to users. CI changes are verified only after merge to `main`.
- Repo for releases: `bauerpawel/YouTube-Downloader`. Release tags: `v<Version>-<run_number>`. Assets: `YouTubeDownloader-{win-x64,win-arm64}.exe`, `YouTubeDownloader-{linux-x64,linux-arm64}`, `YouTubeDownloader-{osx-x64,osx-arm64}.zip`.
- New `<Version>`: `2.0.280926`.
- Commit messages end with:
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  ```

## Review Focus

1. **App run under a non-default file name** (e.g. `YouTubeDownloader-win-x64.exe` straight from the release page) - the update must replace and relaunch *that* file. Pinned by Task 3's apply/cleanup tests (they use the asset name) and Task 5's end-to-end check (renamed exe).
2. **App folder shared with the user's own files** (portable exe in `Downloads`, next to the user's own `yt-dlp.exe`, `theme.txt`, `update.zip`, `something.old`) - nothing the app does not own may be deleted. Pinned by Task 1's marker / no-marker tests and Task 3's "unrelated user files kept" test.
3. **No internet / GitHub API rate limit at startup** - the app must start and work normally with no dialog. Pinned by Task 3's "unreachable API throws" test (the silent path's `catch` is the only guard) and Task 5's review of the silent branch.
4. **Previous process still holding `<exe>.old` on Windows** when the relaunched app starts - cleanup must not throw and must retry next start. Pinned by Task 3's locked-`.old` test.
5. **Swap fails halfway** (disk full, permission, antivirus lock) - the installed version must be left intact and the only backup copy must never be deleted. Pinned by Task 3's rollback test and the "`DeleteUpdateDownloads` keeps `.old`" test.

---

## Task 1: `AppPaths` - data directory, legacy cleanup, file helpers

**Files:**
- Create: `AppPaths.cs`
- Create (scratch, never committed): `$HARNESS/ytd-tests.csproj`, `$HARNESS/Program.cs`, `$HARNESS/AppPathsTests.cs`

**Interfaces:**
- Produces (all `internal static` on `AppPaths`, namespace `YouTubeDownloader`):
  - `const string DataFolderName = "YouTubeDownloader"`
  - `string AppDirectory { get; }` - `AppContext.BaseDirectory`
  - `string DataDirectory { get; }` - lazy; created on first access
  - `bool IsSnap { get; }` - `SNAP` env var set
  - `string ResolveDataDirectory(string? snapUserCommon, string localAppData, string appDirectory)`
  - `void MakeExecutable(string path)` - moved verbatim from `MainWindow`
  - `bool IsSameDirectory(string a, string b)`
  - `void CleanupLegacyFiles(string appDirectory, string dataDirectory)`
  - `string PickFirstPathOutside(string commandOutput, string excludedDirectory)`
  - `bool TryDeleteFile(string path)` - true when the file is gone afterwards
  - `bool TryDeleteDirectory(string path)` - true when the directory is gone afterwards

- [ ] **Step 0: Create the branch and commit the spec**

```bash
git checkout -b claude/data-dir-self-update
git add docs/superpowers/specs/2026-09-28-data-dir-and-self-update-design.md docs/superpowers/plans/2026-09-28-data-dir-and-self-update.md
git commit -m "Add design spec and plan: user data directory and app self-update

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 1: Create the scratch harness project**

`$HARNESS/ytd-tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="E:\claude\YouTube-Downloader\AppPaths.cs" Link="AppPaths.cs" />
  </ItemGroup>

  <!-- Lets AppUpdater.GetLocalBuildNumber() be tested (Task 3). -->
  <ItemGroup>
    <AssemblyMetadata Include="BuildNumber" Value="77" />
  </ItemGroup>

</Project>
```

`$HARNESS/Program.cs`:
```csharp
using YouTubeDownloader;

int failures = 0;

void Check(bool condition, string name)
{
    Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
    if (!condition)
        failures++;
}

string NewTempDir()
{
    string dir = Path.Combine(Path.GetTempPath(), "ytd-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    return dir;
}

AppPathsTests.Run(Check, NewTempDir);

Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
return failures == 0 ? 0 : 1;
```

- [ ] **Step 2: Write the failing tests**

`$HARNESS/AppPathsTests.cs`:
```csharp
using YouTubeDownloader;

static class AppPathsTests
{
    public static void Run(Action<bool, string> check, Func<string> newTempDir)
    {
        // ResolveDataDirectory
        check(AppPaths.ResolveDataDirectory("/snap/common", "/home/u/.local/share", "/app") == "/snap/common",
            "data dir: SNAP_USER_COMMON wins");
        check(AppPaths.ResolveDataDirectory(null, "/home/u/.local/share", "/app")
                == Path.Combine("/home/u/.local/share", "YouTubeDownloader"),
            "data dir: LocalApplicationData/YouTubeDownloader");
        check(AppPaths.ResolveDataDirectory("", "", "/app") == "/app",
            "data dir: falls back to app directory when LocalApplicationData is empty");

        // CleanupLegacyFiles - folder managed by an old version (marker present)
        {
            string app = newTempDir();
            string[] legacyFiles = { "yt-dlp.exe", "yt-dlp", "deno.exe", "deno", "deno_version.txt", "theme.txt", "ffmpeg_version.txt" };
            foreach (string f in legacyFiles)
                File.WriteAllText(Path.Combine(app, f), "x");
            Directory.CreateDirectory(Path.Combine(app, "ffmpeg_bin"));
            File.WriteAllText(Path.Combine(app, "ffmpeg_bin", "ffmpeg.exe"), "x");
            Directory.CreateDirectory(Path.Combine(app, "ffmpeg_temp"));
            string[] keepFiles = { "node.exe", "node", "YouTubeDownloader.exe", "notes.txt", "deno.zip", "ffmpeg.zip", "ffmpeg.tar.xz" };
            foreach (string f in keepFiles)
                File.WriteAllText(Path.Combine(app, f), "x");
            Directory.CreateDirectory(Path.Combine(app, "downloads"));
            File.WriteAllText(Path.Combine(app, "downloads", "video.mp4"), "x");

            AppPaths.CleanupLegacyFiles(app, newTempDir());

            check(legacyFiles.All(f => !File.Exists(Path.Combine(app, f))), "legacy: known files deleted (incl. marker)");
            check(!Directory.Exists(Path.Combine(app, "ffmpeg_bin")) && !Directory.Exists(Path.Combine(app, "ffmpeg_temp")),
                "legacy: known directories deleted");
            check(keepFiles.All(f => File.Exists(Path.Combine(app, f))), "legacy: node, app exe, user files and old temp archives kept");
            check(File.Exists(Path.Combine(app, "downloads", "video.mp4")), "legacy: downloads/ kept");
        }

        // No marker -> never managed by an old version: touch nothing
        {
            string app = newTempDir();
            File.WriteAllText(Path.Combine(app, "yt-dlp.exe"), "x");
            File.WriteAllText(Path.Combine(app, "theme.txt"), "x");
            AppPaths.CleanupLegacyFiles(app, newTempDir());
            check(File.Exists(Path.Combine(app, "yt-dlp.exe")) && File.Exists(Path.Combine(app, "theme.txt")),
                "legacy: without marker nothing is deleted");
        }

        // Same directory (data-dir fallback) -> no-op
        {
            string app = newTempDir();
            File.WriteAllText(Path.Combine(app, "ffmpeg_version.txt"), "x");
            File.WriteAllText(Path.Combine(app, "yt-dlp.exe"), "x");
            AppPaths.CleanupLegacyFiles(app, app + Path.DirectorySeparatorChar);
            check(File.Exists(Path.Combine(app, "yt-dlp.exe")), "legacy: same app/data directory is a no-op");
        }

        // Locked file (Windows only - Unix allows deleting open files)
        if (OperatingSystem.IsWindows())
        {
            string app = newTempDir();
            File.WriteAllText(Path.Combine(app, "ffmpeg_version.txt"), "x");
            File.WriteAllText(Path.Combine(app, "yt-dlp.exe"), "x");
            File.WriteAllText(Path.Combine(app, "theme.txt"), "x");
            bool threw = false;
            using (new FileStream(Path.Combine(app, "yt-dlp.exe"), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                try { AppPaths.CleanupLegacyFiles(app, newTempDir()); }
                catch { threw = true; }
            }
            check(!threw, "legacy: locked file does not throw");
            check(!File.Exists(Path.Combine(app, "theme.txt")), "legacy: unlocked files still deleted");
            check(File.Exists(Path.Combine(app, "ffmpeg_version.txt")), "legacy: marker kept when something could not be deleted");
            AppPaths.CleanupLegacyFiles(app, newTempDir());
            check(!File.Exists(Path.Combine(app, "yt-dlp.exe")) && !File.Exists(Path.Combine(app, "ffmpeg_version.txt")),
                "legacy: retry after unlock finishes the job");
        }

        // PickFirstPathOutside - `where deno` output (CWD hit first on Windows)
        {
            string app = newTempDir();
            string other = newTempDir();
            string output = Path.Combine(app, "deno.exe") + "\r\n" + Path.Combine(other, "deno.exe") + "\r\n";
            check(AppPaths.PickFirstPathOutside(output, app) == Path.Combine(other, "deno.exe"),
                "where: skips hit in app directory, returns first other");
            check(AppPaths.PickFirstPathOutside(Path.Combine(app, "deno.exe") + "\n", app) == "",
                "where: only app-directory hit -> empty");
            check(AppPaths.PickFirstPathOutside("", app) == "", "where: empty output -> empty");
            check(AppPaths.PickFirstPathOutside(Path.Combine(other, "deno") + "\n" + Path.Combine(other, "x", "deno"), app)
                    == Path.Combine(other, "deno"),
                "where: first matching line wins, a single path is returned");
        }

        // TryDeleteFile semantics
        {
            string dir = newTempDir();
            check(AppPaths.TryDeleteFile(Path.Combine(dir, "missing")), "delete: missing file counts as gone");
            check(AppPaths.TryDeleteFile(Path.Combine(dir, "no-such-dir", "missing")), "delete: missing parent counts as gone");
        }
    }
}
```

- [ ] **Step 3: Run the harness to verify it fails**

Run: `dotnet run --project "$HARNESS"`
Expected: build error - `AppPaths.cs` does not exist (`CS2001: Source file ... AppPaths.cs could not be found`).

- [ ] **Step 4: Implement `AppPaths.cs`**

```csharp
using System;
using System.IO;

namespace YouTubeDownloader;

// The app folder (AppDirectory) holds only the app itself plus downloads/.
// Everything the app downloads or writes lives in DataDirectory, so the app
// folder can be read-only (snap, Program Files) and self-update only ever
// replaces the app's own files.
internal static class AppPaths
{
    public const string DataFolderName = "YouTubeDownloader";

    // Every pre-data-directory version wrote this next to the exe once FFmpeg was
    // installed - proof that the app folder was managed by an old version.
    private const string LegacyMarkerFile = "ffmpeg_version.txt";

    // Deliberately excludes node(.exe) (user-supplied, never downloaded by the app)
    // and the old temp archives deno.zip/ffmpeg.zip/ffmpeg.tar.xz (too generic -
    // the app folder may be the user's Downloads folder).
    private static readonly string[] LegacyFiles =
    {
        "yt-dlp.exe", "yt-dlp", "deno.exe", "deno", "deno_version.txt", "theme.txt"
    };

    private static readonly string[] LegacyDirectories = { "ffmpeg_bin", "ffmpeg_temp" };

    private static readonly Lazy<string> dataDirectory = new(CreateDataDirectory);

    public static string AppDirectory { get; } = AppContext.BaseDirectory;

    public static string DataDirectory => dataDirectory.Value;

    public static bool IsSnap => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SNAP"));

    public static string ResolveDataDirectory(string? snapUserCommon, string localAppData, string appDirectory)
    {
        if (!string.IsNullOrEmpty(snapUserCommon))
            return snapUserCommon;

        if (string.IsNullOrEmpty(localAppData))
            return appDirectory;

        return Path.Combine(localAppData, DataFolderName);
    }

    private static string CreateDataDirectory()
    {
        string appDirectory = AppContext.BaseDirectory;
        string directory = ResolveDataDirectory(
            Environment.GetEnvironmentVariable("SNAP_USER_COMMON"),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            appDirectory);

        try
        {
            Directory.CreateDirectory(directory);
            return directory;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return appDirectory;
        }
    }

    public static void MakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        File.SetUnixFileMode(path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    public static bool IsSameDirectory(string a, string b)
    {
        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            comparison);
    }

    public static void CleanupLegacyFiles(string appDirectory, string dataDirectory)
    {
        if (IsSameDirectory(appDirectory, dataDirectory))
            return;

        string marker = Path.Combine(appDirectory, LegacyMarkerFile);
        if (!File.Exists(marker))
            return;

        bool allDeleted = true;
        foreach (string name in LegacyDirectories)
            allDeleted &= TryDeleteDirectory(Path.Combine(appDirectory, name));
        foreach (string name in LegacyFiles)
            allDeleted &= TryDeleteFile(Path.Combine(appDirectory, name));

        // Marker last, and only once everything else is gone - otherwise the next
        // start must still recognize this folder and retry.
        if (allDeleted)
            TryDeleteFile(marker);
    }

    // `where` (Windows) searches the current directory before PATH, and a
    // double-clicked app runs with its own folder as the current directory - so
    // a hit there is the app's own old copy, not a system-wide install.
    public static string PickFirstPathOutside(string commandOutput, string excludedDirectory)
    {
        foreach (string line in commandOutput.Split('\n'))
        {
            string path = line.Trim();
            if (path.Length == 0)
                continue;

            string? directory = Path.GetDirectoryName(path);
            if (directory != null && IsSameDirectory(directory, excludedDirectory))
                continue;

            return path;
        }

        return "";
    }

    public static bool TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return !File.Exists(path);
        }
    }

    public static bool TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return !Directory.Exists(path);
        }
    }
}
```

- [ ] **Step 5: Run the harness to verify it passes**

Run: `dotnet run --project "$HARNESS"`
Expected: every line `PASS ...`, last line `ALL PASSED`, exit code 0.

- [ ] **Step 6: Build the app**

Run: `dotnet build YouTubeDownloader.csproj -c Debug`
Expected: `0 Warning(s)`, `0 Error(s)`. (`AppPaths` is not used yet - that is Task 2.)

- [ ] **Step 7: Commit**

```bash
git add AppPaths.cs
git commit -m "Add AppPaths: per-user data directory and legacy-file cleanup

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Task 2: Use the data directory in the app

**Files:**
- Modify: `MainWindow.axaml.cs` (constructor lines 32-78, `CheckAndDownloadComponents` 147-168, `IsRuntimeInPath`/`GetRuntimePath` 170-228, lines 250, 295, 310, 345, 376, 429, 512, 521, 540, 543, 578, 581, 583, 632-641, 689)
- Modify: `App.axaml.cs:18-19`
- Modify: `ThemeSettings.cs` (parameter rename only)

**Interfaces:**
- Consumes: `AppPaths.AppDirectory`, `AppPaths.DataDirectory`, `AppPaths.MakeExecutable`, `AppPaths.CleanupLegacyFiles`, `AppPaths.PickFirstPathOutside` (Task 1).
- Produces: `MainWindow` fields `dataDirectory` (string) and `ffmpegVersionPath` (string); private method `FindSystemDeno() : string`. `MainWindow.MakeExecutable` no longer exists.

- [ ] **Step 1: Fields and constructor**

In the field list, add after `private readonly string appDirectory;`:
```csharp
    private readonly string dataDirectory;
    private readonly string ffmpegVersionPath;
```

Replace the constructor's path block:
```csharp
        httpClient = new HttpClient();
        appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        ytDlpPath = Path.Combine(appDirectory, OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp");
        ffmpegBinPath = Path.Combine(appDirectory, "ffmpeg_bin");
        denoPath = Path.Combine(appDirectory, OperatingSystem.IsWindows() ? "deno.exe" : "deno");
        denoVersionPath = Path.Combine(appDirectory, "deno_version.txt");
        nodeJsPath = Path.Combine(appDirectory, OperatingSystem.IsWindows() ? "node.exe" : "node");
```
with:
```csharp
        httpClient = new HttpClient();
        // Set once: adding it per request (as before) appended a duplicate value each call.
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("YouTubeDownloader/1.0");
        appDirectory = AppPaths.AppDirectory;
        dataDirectory = AppPaths.DataDirectory;
        ytDlpPath = Path.Combine(dataDirectory, OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp");
        ffmpegBinPath = Path.Combine(dataDirectory, "ffmpeg_bin");
        ffmpegVersionPath = Path.Combine(dataDirectory, "ffmpeg_version.txt");
        denoPath = Path.Combine(dataDirectory, OperatingSystem.IsWindows() ? "deno.exe" : "deno");
        denoVersionPath = Path.Combine(dataDirectory, "deno_version.txt");

        // Node is never downloaded by the app - users drop it in by hand, so keep
        // honouring a copy next to the exe from before the data directory existed.
        string nodeName = OperatingSystem.IsWindows() ? "node.exe" : "node";
        string dataNodePath = Path.Combine(dataDirectory, nodeName);
        nodeJsPath = File.Exists(dataNodePath) ? dataNodePath : Path.Combine(appDirectory, nodeName);
```

Change `ThemeSettings.Load(appDirectory)` (line 72) to `ThemeSettings.Load(dataDirectory)` and, in `SetTheme`, `ThemeSettings.Save(appDirectory, name)` to `ThemeSettings.Save(dataDirectory, name)`.

- [ ] **Step 2: `App.axaml.cs` and `ThemeSettings.cs`**

`App.axaml.cs` - replace:
```csharp
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            RequestedThemeVariant = ThemeSettings.ToVariant(ThemeSettings.Load(appDirectory));
```
with:
```csharp
            RequestedThemeVariant = ThemeSettings.ToVariant(ThemeSettings.Load(AppPaths.DataDirectory));
```

`ThemeSettings.cs` - rename the parameter `appDirectory` to `directory` in `GetPath`, `Load`, `Save` (body unchanged otherwise):
```csharp
    public static string GetPath(string directory) => Path.Combine(directory, "theme.txt");

    public static string Load(string directory)
    {
        try
        {
            string text = File.ReadAllText(GetPath(directory)).Trim();
```
```csharp
    public static void Save(string directory, string name)
    {
        try
        {
            File.WriteAllText(GetPath(directory), name);
```

- [ ] **Step 3: System Deno lookup that ignores the app folder**

Replace `IsRuntimeInPath()` and `GetRuntimePath()` (lines 170-228) with:
```csharp
    private bool IsRuntimeInPath() => !string.IsNullOrEmpty(FindSystemDeno());

    private string GetRuntimePath()
    {
        if (File.Exists(denoPath))
            return denoPath;

        if (File.Exists(nodeJsPath))
            return nodeJsPath;

        return FindSystemDeno();
    }

    // First `where`/`which` hit outside the app folder. `where` also returns every
    // match on its own line, so the raw output is never usable as a path directly.
    private string FindSystemDeno()
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "where" : "which",
                Arguments = "deno",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
                return "";

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? AppPaths.PickFirstPathOutside(output, appDirectory) : "";
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return "";
        }
    }
```

- [ ] **Step 4: Paths inside the download methods**

- Delete the line `httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("YouTubeDownloader/1.0");` in `GetLatestDenoInfo()`, `GetLatestFFmpegInfo()` and `GetLatestFFmpegInfoMac()` (3 occurrences).
- `DownloadDeno()`: `Path.Combine(appDirectory, "deno.zip")` -> `Path.Combine(dataDirectory, "deno.zip")`.
- `DownloadFFmpeg()`: `Path.Combine(appDirectory, "ffmpeg" + archiveExtension)` -> `Path.Combine(dataDirectory, "ffmpeg" + archiveExtension)`; `Path.Combine(appDirectory, "ffmpeg_temp")` -> `Path.Combine(dataDirectory, "ffmpeg_temp")`.
- `DownloadFFmpeg()`, `DownloadFFmpegMac()`, `CheckAndUpdateFFmpeg()`: delete each `string versionFile = Path.Combine(appDirectory, "ffmpeg_version.txt");` and replace the following uses of `versionFile` with `ffmpegVersionPath`.
- Delete the `MakeExecutable` method from `MainWindow` and replace every `MakeExecutable(` call (in `DownloadDeno`, `DownloadYtDlp`, `DownloadFFmpeg`, `DownloadFFmpegMac`) with `AppPaths.MakeExecutable(`.
- Leave both `Path.Combine(appDirectory, "downloads")` lines untouched.

- [ ] **Step 5: Remove the old copies once the new ones work**

At the end of `CheckAndDownloadComponents()`, after `UpdateStatus("Wszystkie komponenty sa dostepne. Gotowy do pobierania.");`, add:
```csharp

        // Versions before the data directory kept the tools next to the exe. Remove
        // those copies only once working replacements exist in the data directory.
        if (File.Exists(ytDlpPath) && Directory.Exists(ffmpegBinPath) && !string.IsNullOrEmpty(GetRuntimePath()))
            AppPaths.CleanupLegacyFiles(appDirectory, dataDirectory);
```

- [ ] **Step 6: Build**

Run: `dotnet build YouTubeDownloader.csproj -c Debug`
Expected: `0 Warning(s)`, `0 Error(s)`. Then search `MainWindow.axaml.cs` for `appDirectory` (Grep tool) - the only remaining uses are the field, its assignment, the two `downloads` lines, `nodeJsPath`, `FindSystemDeno`, and `CleanupLegacyFiles`.

- [ ] **Step 7: Migration smoke run on Windows (old layout -> data directory)**

This downloads the real tools into `%LOCALAPPDATA%\YouTubeDownloader` on this machine - the same place the app will use from now on. Run in the background with a 10-minute timeout:
```powershell
$bin = "E:\claude\YouTube-Downloader\bin\Debug\net10.0"
$data = Join-Path $env:LOCALAPPDATA "YouTubeDownloader"
# Fake an old install next to the exe (0-byte deno.exe also proves `where` ignores the app folder)
Set-Content "$bin\ffmpeg_version.txt" "autobuild-old"
Set-Content "$bin\yt-dlp.exe" "fake"
Set-Content "$bin\theme.txt" "Dark"
New-Item -ItemType File "$bin\deno.exe" -Force | Out-Null
New-Item -ItemType Directory "$bin\ffmpeg_bin" -Force | Out-Null
$p = Start-Process "$bin\YouTubeDownloader.exe" -WorkingDirectory $bin -PassThru
$deadline = (Get-Date).AddMinutes(9)
while ((Get-Date) -lt $deadline -and (Test-Path "$bin\ffmpeg_version.txt")) { Start-Sleep -Seconds 10 }
Stop-Process -Id $p.Id -Force
"data yt-dlp:     " + (Test-Path "$data\yt-dlp.exe")
"data ffmpeg:     " + (Test-Path "$data\ffmpeg_bin\ffmpeg.exe")
"data ffmpeg ver: " + (Test-Path "$data\ffmpeg_version.txt")
"data deno:       " + (Test-Path "$data\deno.exe") + "  (False is OK only if a system-wide deno exists: " + [bool](Get-Command deno -ErrorAction SilentlyContinue) + ")"
"legacy left:     " + ((@("yt-dlp.exe","deno.exe","theme.txt","ffmpeg_version.txt","ffmpeg_bin") | Where-Object { Test-Path "$bin\$_" }) -join ", ")
```
Expected: `data yt-dlp`, `data ffmpeg`, `data ffmpeg ver` all `True`; `data deno` `True` unless a system-wide `deno` exists; `legacy left:` empty.

- [ ] **Step 8: Commit**

```bash
git add MainWindow.axaml.cs App.axaml.cs ThemeSettings.cs
git commit -m "Store tools and settings in the per-user data directory

Tools (yt-dlp, FFmpeg, Deno), their version files and theme.txt move out of
the executable's folder; copies left there by older versions are removed once
the new ones are in place. System Deno lookup now ignores hits in the app
folder (where.exe searches the current directory first) and uses only the
first match. User-Agent is set once instead of appended per request.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Task 3: `AppUpdater` - release lookup, verification, swap, cleanup

**Files:**
- Create: `AppUpdater.cs`
- Modify (scratch): `$HARNESS/ytd-tests.csproj`, `$HARNESS/Program.cs`
- Create (scratch): `$HARNESS/AppUpdaterTests.cs`

**Interfaces:**
- Consumes: `AppPaths.MakeExecutable`, `AppPaths.TryDeleteFile`, `AppPaths.TryDeleteDirectory` (Task 1).
- Produces (namespace `YouTubeDownloader`):
  - `internal sealed record ReleaseAsset(string Name, string DownloadUrl, long Size, string? Sha256)`
  - `internal sealed record ReleaseInfo(string TagName, int? BuildNumber, string HtmlUrl, ReleaseAsset? Asset)`
  - `internal static class AppUpdater` with:
    - `const string Repo`, `OldSuffix = ".old"`, `NewSuffix = ".new"`, `UpdateZipName = "YouTubeDownloader-update.zip"`, `UpdateDirectoryName = "YouTubeDownloader-update"`
    - `int? GetLocalBuildNumber()`, `int? GetBuildNumber(Assembly assembly)`, `int? ParseBuildNumber(string tagName)`
    - `string GetAssetName()`, `string GetAssetName(string os, Architecture architecture)` (`os` is `"win"`, `"linux"` or `"osx"`)
    - `Task<ReleaseInfo> GetLatestReleaseAsync(HttpClient http, string apiUrl = <latest release URL>)` - throws on network/API errors
    - `ReleaseInfo ParseRelease(string json, string assetName)`
    - `bool VerifyDownload(string path, ReleaseAsset asset)`
    - `bool CanWriteDirectory(string directory)`
    - `List<(string Source, string Target)> BuildFileList(string extractedDirectory, string appDirectory)`
    - `void ApplyUpdate(IReadOnlyList<(string Source, string Target)> files)` - throws after rolling back
    - `void Relaunch(string executablePath)`
    - `void DeleteUpdateDownloads(string appDirectory, string executablePath)` - `.new`, zip, extract dir; never `.old`
    - `void CleanupLeftovers(string appDirectory, string? executablePath)` - the above plus `<exe>.old`

- [ ] **Step 1: Add `AppUpdater.cs` to the harness and write the failing tests**

In `$HARNESS/ytd-tests.csproj`, add next to the `AppPaths.cs` include:
```xml
    <Compile Include="E:\claude\YouTube-Downloader\AppUpdater.cs" Link="AppUpdater.cs" />
```

In `$HARNESS/Program.cs`, after `AppPathsTests.Run(Check, NewTempDir);` add:
```csharp
await AppUpdaterTests.Run(Check, NewTempDir);
```

`$HARNESS/AppUpdaterTests.cs`:
```csharp
using System.Runtime.InteropServices;
using System.Text;
using YouTubeDownloader;

static class AppUpdaterTests
{
    public static async Task Run(Action<bool, string> check, Func<string> newTempDir)
    {
        // ParseBuildNumber
        check(AppUpdater.ParseBuildNumber("v2.0.110926-123") == 123, "tag: build number after last dash");
        check(AppUpdater.ParseBuildNumber("v2.0.110926") == null, "tag: no build suffix -> null");
        check(AppUpdater.ParseBuildNumber("v2.0.110926-abc") == null, "tag: non-numeric suffix -> null");
        check(AppUpdater.ParseBuildNumber("v2.0.110926-") == null, "tag: empty suffix -> null");
        check(AppUpdater.ParseBuildNumber("2.0.110926-5") == null, "tag: missing v prefix -> null");

        // Build number from assembly metadata (harness csproj sets BuildNumber=77)
        check(AppUpdater.GetLocalBuildNumber() == 77, "metadata: BuildNumber read from assembly");

        // Asset names
        check(AppUpdater.GetAssetName("win", Architecture.X64) == "YouTubeDownloader-win-x64.exe", "asset: win-x64");
        check(AppUpdater.GetAssetName("win", Architecture.Arm64) == "YouTubeDownloader-win-arm64.exe", "asset: win-arm64");
        check(AppUpdater.GetAssetName("linux", Architecture.X64) == "YouTubeDownloader-linux-x64", "asset: linux-x64");
        check(AppUpdater.GetAssetName("linux", Architecture.Arm64) == "YouTubeDownloader-linux-arm64", "asset: linux-arm64");
        check(AppUpdater.GetAssetName("osx", Architecture.X64) == "YouTubeDownloader-osx-x64.zip", "asset: osx-x64");
        check(AppUpdater.GetAssetName("osx", Architecture.Arm64) == "YouTubeDownloader-osx-arm64.zip", "asset: osx-arm64");
        bool unsupportedThrew = false;
        try { AppUpdater.GetAssetName("", Architecture.X64); }
        catch (PlatformNotSupportedException) { unsupportedThrew = true; }
        check(unsupportedThrew, "asset: unknown OS throws PlatformNotSupportedException");

        // ParseRelease
        const string json = """
            {
              "tag_name": "v2.0.280926-124",
              "html_url": "https://github.com/bauerpawel/YouTube-Downloader/releases/tag/v2.0.280926-124",
              "assets": [
                { "name": "YouTubeDownloader-win-x64.exe", "browser_download_url": "https://example.invalid/a.exe", "size": 1234, "digest": "sha256:ABCDEF" },
                { "name": "YouTubeDownloader-win-x64.zip", "browser_download_url": "https://example.invalid/a.zip", "size": 999, "digest": null },
                { "name": "YouTubeDownloader-win-x64.exe.sig", "browser_download_url": "https://example.invalid/a.sig", "size": 1 }
              ]
            }
            """;
        var release = AppUpdater.ParseRelease(json, "YouTubeDownloader-win-x64.exe");
        check(release.TagName == "v2.0.280926-124" && release.BuildNumber == 124, "release: tag and build parsed");
        check(release.HtmlUrl.EndsWith("/v2.0.280926-124"), "release: html_url parsed");
        check(release.Asset is { DownloadUrl: "https://example.invalid/a.exe", Size: 1234, Sha256: "abcdef" },
            "release: exact-name asset (not the .sig sidecar) with lower-cased sha256");
        check(AppUpdater.ParseRelease(json, "YouTubeDownloader-win-x64.zip").Asset is { Sha256: null },
            "release: null digest -> no sha256");
        check(AppUpdater.ParseRelease(json, "YouTubeDownloader-linux-x64").Asset == null,
            "release: missing asset (publish in progress) -> Asset null");

        // VerifyDownload - "hello" has a well-known SHA-256
        {
            string dir = newTempDir();
            string file = Path.Combine(dir, "f");
            File.WriteAllBytes(file, Encoding.ASCII.GetBytes("hello"));
            const string helloSha = "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";
            check(AppUpdater.VerifyDownload(file, new ReleaseAsset("f", "", 5, helloSha)), "verify: size + sha256 match");
            check(AppUpdater.VerifyDownload(file, new ReleaseAsset("f", "", 5, null)), "verify: size match, no digest");
            check(!AppUpdater.VerifyDownload(file, new ReleaseAsset("f", "", 6, null)), "verify: size mismatch");
            check(!AppUpdater.VerifyDownload(file, new ReleaseAsset("f", "", 5, new string('0', 64))), "verify: sha256 mismatch");
            check(!AppUpdater.VerifyDownload(Path.Combine(dir, "missing"), new ReleaseAsset("f", "", 5, null)), "verify: missing file");
        }

        // CanWriteDirectory
        check(AppUpdater.CanWriteDirectory(newTempDir()), "write test: writable temp dir");
        check(!AppUpdater.CanWriteDirectory(Path.Combine(newTempDir(), "does-not-exist")), "write test: missing directory -> false");
        {
            string dir = newTempDir();
            AppUpdater.CanWriteDirectory(dir);
            check(Directory.GetFileSystemEntries(dir).Length == 0, "write test: probe file removed");
        }

        // ApplyUpdate - single file, Windows/Linux shape, exe under the asset name
        {
            string dir = newTempDir();
            string exe = Path.Combine(dir, "YouTubeDownloader-win-x64.exe");
            File.WriteAllText(exe, "old");
            File.WriteAllText(exe + ".new", "new");
            AppUpdater.ApplyUpdate(new List<(string, string)> { (exe + ".new", exe) });
            check(File.ReadAllText(exe) == "new", "apply: target replaced");
            check(!File.Exists(exe + ".new") && !File.Exists(exe + ".old"), "apply: no .new/.old left (nothing locked)");
        }

        // ApplyUpdate - folder, macOS shape, including a file new in this version
        {
            string app = newTempDir();
            string extracted = newTempDir();
            File.WriteAllText(Path.Combine(app, "YouTubeDownloader"), "old");
            File.WriteAllText(Path.Combine(extracted, "YouTubeDownloader"), "new");
            File.WriteAllText(Path.Combine(extracted, "libNew.dylib"), "lib");
            Directory.CreateDirectory(Path.Combine(extracted, "sub"));
            File.WriteAllText(Path.Combine(extracted, "sub", "res.bin"), "res");
            var files = AppUpdater.BuildFileList(extracted, app);
            check(files.Count == 3 && files.Any(f => f.Target == Path.Combine(app, "sub", "res.bin")),
                "file list: nested files mapped into app dir");
            AppUpdater.ApplyUpdate(files);
            check(File.ReadAllText(Path.Combine(app, "YouTubeDownloader")) == "new"
                && File.ReadAllText(Path.Combine(app, "libNew.dylib")) == "lib"
                && File.ReadAllText(Path.Combine(app, "sub", "res.bin")) == "res",
                "apply: folder update installs replaced, new and nested files");
            if (!OperatingSystem.IsWindows())
                check(File.GetUnixFileMode(Path.Combine(app, "YouTubeDownloader")).HasFlag(UnixFileMode.UserExecute),
                    "apply: executable bit set");
        }

        // ApplyUpdate - failure in phase 2 rolls everything back
        {
            string dir = newTempDir();
            string a = Path.Combine(dir, "a");
            File.WriteAllText(a, "old-a");
            File.WriteAllText(a + ".new", "new-a");
            string blocker = Path.Combine(dir, "blocker");
            File.WriteAllText(blocker, "a file, so it cannot be used as a directory");
            string bSource = Path.Combine(dir, "b.src");
            File.WriteAllText(bSource, "new-b");
            bool threw = false;
            try
            {
                AppUpdater.ApplyUpdate(new List<(string, string)> { (a + ".new", a), (bSource, Path.Combine(blocker, "b")) });
            }
            catch (IOException) { threw = true; }
            check(threw, "rollback: failure is rethrown");
            check(File.ReadAllText(a) == "old-a", "rollback: original file restored");
            check(!File.Exists(a + ".old"), "rollback: no .old left behind");
        }

        // DeleteUpdateDownloads / CleanupLeftovers - exact names only
        {
            string app = newTempDir();
            string exe = Path.Combine(app, "YouTubeDownloader-win-x64.exe");
            foreach (string f in new[] { exe, exe + ".old", exe + ".new", Path.Combine(app, "YouTubeDownloader-update.zip"),
                                         Path.Combine(app, "other.old"), Path.Combine(app, "update.zip") })
                File.WriteAllText(f, "x");
            Directory.CreateDirectory(Path.Combine(app, "YouTubeDownloader-update", "nested"));

            AppUpdater.DeleteUpdateDownloads(app, exe);
            check(File.Exists(exe + ".old"), "downloads cleanup: keeps .old (may be the only copy after a failed rollback)");
            check(!File.Exists(exe + ".new") && !File.Exists(Path.Combine(app, "YouTubeDownloader-update.zip"))
                && !Directory.Exists(Path.Combine(app, "YouTubeDownloader-update")),
                "downloads cleanup: removes .new, zip and extract dir");

            AppUpdater.CleanupLeftovers(app, exe);
            check(!File.Exists(exe + ".old"), "leftovers: removes <exe>.old");
            check(File.Exists(exe) && File.Exists(Path.Combine(app, "other.old")) && File.Exists(Path.Combine(app, "update.zip")),
                "leftovers: exe and unrelated user files kept");
        }

        // CleanupLeftovers while the previous process still holds <exe>.old (Windows)
        if (OperatingSystem.IsWindows())
        {
            string app = newTempDir();
            string exe = Path.Combine(app, "YouTubeDownloader.exe");
            File.WriteAllText(exe + ".old", "x");
            bool threw = false;
            using (new FileStream(exe + ".old", FileMode.Open, FileAccess.Read, FileShare.None))
            {
                try { AppUpdater.CleanupLeftovers(app, exe); }
                catch { threw = true; }
            }
            check(!threw && File.Exists(exe + ".old"), "leftovers: locked .old is skipped without throwing");
        }

        // Network failure surfaces as an exception - MainWindow's silent startup check must catch it
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            bool threw = false;
            try { await AppUpdater.GetLatestReleaseAsync(http, "http://127.0.0.1:9/releases/latest"); }
            catch (HttpRequestException) { threw = true; }
            catch (TaskCanceledException) { threw = true; }
            check(threw, "network: unreachable API throws");
        }
    }
}
```

- [ ] **Step 2: Run the harness to verify it fails**

Run: `dotnet run --project "$HARNESS"`
Expected: build error - `AppUpdater.cs` could not be found (`CS2001`).

- [ ] **Step 3: Implement `AppUpdater.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace YouTubeDownloader;

internal sealed record ReleaseAsset(string Name, string DownloadUrl, long Size, string? Sha256);

internal sealed record ReleaseInfo(string TagName, int? BuildNumber, string HtmlUrl, ReleaseAsset? Asset);

// Self-update from this repo's GitHub Releases. No UI here - MainWindow owns the
// dialogs and progress bar - which also keeps this file free of Avalonia.
internal static class AppUpdater
{
    public const string Repo = "bauerpawel/YouTube-Downloader";
    public const string OldSuffix = ".old";
    public const string NewSuffix = ".new";
    public const string UpdateZipName = "YouTubeDownloader-update.zip";
    public const string UpdateDirectoryName = "YouTubeDownloader-update";

    private const string LatestReleaseUrl = "https://api.github.com/repos/" + Repo + "/releases/latest";

    // CI passes -p:BuildNumber=<github.run_number>; local builds have none.
    public static int? GetLocalBuildNumber() => GetBuildNumber(typeof(AppUpdater).Assembly);

    public static int? GetBuildNumber(Assembly assembly)
    {
        string? value = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildNumber")?.Value;
        return ParseNumber(value);
    }

    // "v2.0.110926-123" -> 123. <Version> itself (2.0.ddMMyy) is not monotonic, so
    // the CI run number in the tag is the only thing that can be compared.
    public static int? ParseBuildNumber(string tagName)
    {
        if (!tagName.StartsWith('v'))
            return null;

        int dash = tagName.LastIndexOf('-');
        return dash < 0 ? null : ParseNumber(tagName[(dash + 1)..]);
    }

    private static int? ParseNumber(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) ? number : null;

    public static string GetAssetName()
    {
        string os = OperatingSystem.IsWindows() ? "win"
            : OperatingSystem.IsMacOS() ? "osx"
            : OperatingSystem.IsLinux() ? "linux"
            : "";
        return GetAssetName(os, RuntimeInformation.ProcessArchitecture);
    }

    public static string GetAssetName(string os, Architecture architecture)
    {
        string arch = architecture == Architecture.Arm64 ? "arm64" : "x64";
        return os switch
        {
            "win" => $"YouTubeDownloader-win-{arch}.exe",
            "linux" => $"YouTubeDownloader-linux-{arch}",
            "osx" => $"YouTubeDownloader-osx-{arch}.zip",
            _ => throw new PlatformNotSupportedException("App self-update is only supported on Windows, Linux, and macOS.")
        };
    }

    public static async Task<ReleaseInfo> GetLatestReleaseAsync(HttpClient http, string apiUrl = LatestReleaseUrl)
    {
        string json = await http.GetStringAsync(apiUrl);
        return ParseRelease(json, GetAssetName());
    }

    public static ReleaseInfo ParseRelease(string json, string assetName)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        string tagName = root.GetProperty("tag_name").GetString() ?? "";
        string htmlUrl = root.GetProperty("html_url").GetString() ?? "";

        // CI attaches assets one RID at a time, so a brand-new release can lack
        // this platform's file for a few minutes - Asset stays null then.
        ReleaseAsset? match = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != assetName)
                continue;

            string? sha256 = null;
            if (asset.TryGetProperty("digest", out var digest) && digest.ValueKind == JsonValueKind.String)
            {
                string value = digest.GetString() ?? "";
                if (value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                    sha256 = value["sha256:".Length..].ToLowerInvariant();
            }

            match = new ReleaseAsset(
                assetName,
                asset.GetProperty("browser_download_url").GetString() ?? "",
                asset.GetProperty("size").GetInt64(),
                sha256);
            break;
        }

        return new ReleaseInfo(tagName, ParseBuildNumber(tagName), htmlUrl, match);
    }

    public static bool VerifyDownload(string path, ReleaseAsset asset)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != asset.Size)
            return false;

        if (asset.Sha256 == null)
            return true;

        using var stream = File.OpenRead(path);
        string actual = Convert.ToHexString(SHA256.HashData(stream));
        return string.Equals(actual, asset.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanWriteDirectory(string directory)
    {
        try
        {
            string probe = Path.Combine(directory, ".ytd-write-test-" + Guid.NewGuid().ToString("N"));
            using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static List<(string Source, string Target)> BuildFileList(string extractedDirectory, string appDirectory) =>
        Directory.GetFiles(extractedDirectory, "*", SearchOption.AllDirectories)
            .Select(file => (Source: file, Target: Path.Combine(appDirectory, Path.GetRelativePath(extractedDirectory, file))))
            .ToList();

    // Two phases so a failure anywhere leaves the installed app intact:
    // 1) every existing target -> target.old, 2) every source -> target.
    // Renaming a running executable is allowed on Windows (overwriting is not);
    // on Linux/macOS the running process keeps the old inode.
    public static void ApplyUpdate(IReadOnlyList<(string Source, string Target)> files)
    {
        // Before anything is touched, so a failure here costs nothing. The macOS
        // zip (Compress-Archive in CI) carries no Unix modes at all; the bit is
        // harmless on libraries.
        foreach (var (source, _) in files)
            AppPaths.MakeExecutable(source);

        var backedUp = new List<string>();
        var installed = new List<string>();
        try
        {
            foreach (var (_, target) in files)
            {
                if (!File.Exists(target))
                    continue;
                File.Move(target, target + OldSuffix, overwrite: true);
                backedUp.Add(target);
            }

            foreach (var (source, target) in files)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(source, target);
                installed.Add(target);
            }
        }
        catch
        {
            foreach (string target in installed)
                AppPaths.TryDeleteFile(target);

            foreach (string target in backedUp)
            {
                try
                {
                    File.Move(target + OldSuffix, target, overwrite: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Keep restoring the rest; the .old copy stays for manual recovery.
                }
            }

            throw;
        }

        // Unix lets these go immediately even while in use; on Windows the running
        // exe's .old stays locked until this process exits and CleanupLeftovers()
        // removes it on the next start.
        foreach (string target in backedUp)
            AppPaths.TryDeleteFile(target + OldSuffix);
    }

    public static void Relaunch(string executablePath)
    {
        using var process = Process.Start(new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? ""
        });
    }

    // Never deletes <exe>.old: after a failed rollback it may be the only copy.
    public static void DeleteUpdateDownloads(string appDirectory, string executablePath)
    {
        AppPaths.TryDeleteFile(executablePath + NewSuffix);
        AppPaths.TryDeleteFile(Path.Combine(appDirectory, UpdateZipName));
        AppPaths.TryDeleteDirectory(Path.Combine(appDirectory, UpdateDirectoryName));
    }

    // Exact names only - the app folder may be the user's Downloads folder.
    public static void CleanupLeftovers(string appDirectory, string? executablePath)
    {
        if (executablePath == null)
            return;

        AppPaths.TryDeleteFile(executablePath + OldSuffix);
        DeleteUpdateDownloads(appDirectory, executablePath);
    }
}
```

- [ ] **Step 4: Run the harness to verify it passes**

Run: `dotnet run --project "$HARNESS"`
Expected: every line `PASS ...` (Task 1's and Task 3's), last line `ALL PASSED`, exit code 0.

- [ ] **Step 5: Build the app**

Run: `dotnet build YouTubeDownloader.csproj -c Debug`
Expected: `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 6: Commit**

```bash
git add AppUpdater.cs
git commit -m "Add AppUpdater: GitHub release lookup and safe two-phase file swap

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Task 4: Build number in the assembly and the About dialog

**Files:**
- Modify: `YouTubeDownloader.csproj`
- Modify: `AboutWindow.axaml.cs:22-27`

**Interfaces:**
- Consumes: `AppUpdater.GetLocalBuildNumber()` (Task 3).
- Produces: `AssemblyMetadata("BuildNumber", "<n>")` on the app assembly when built with `-p:BuildNumber=<n>`.

- [ ] **Step 1: Verify the metadata is absent today**

Run:
```powershell
dotnet build YouTubeDownloader.csproj -c Release -p:BuildNumber=5
Select-String -Path obj\Release\net10.0\YouTubeDownloader.AssemblyInfo.cs -Pattern 'BuildNumber'
```
Expected: no match (property is ignored so far).

- [ ] **Step 2: Add the metadata item to the csproj**

After the existing `<ItemGroup>` with the `AvaloniaResource` items, add:
```xml
  <!-- CI passes -p:BuildNumber=<github.run_number>; the self-updater compares it
       with the number in the latest release tag (v<Version>-<run_number>). Local
       builds have none, which turns self-update off for them. -->
  <ItemGroup Condition="'$(BuildNumber)' != ''">
    <AssemblyMetadata Include="BuildNumber" Value="$(BuildNumber)" />
  </ItemGroup>
```

- [ ] **Step 3: Show the build number in About**

Replace `GetAppVersion()` in `AboutWindow.axaml.cs`:
```csharp
    private static string GetAppVersion()
    {
        string version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "?";
        int? build = AppUpdater.GetLocalBuildNumber();
        return build == null ? version : $"{version} (build {build})";
    }
```

- [ ] **Step 4: Verify the metadata appears only when passed**

Run:
```powershell
dotnet build YouTubeDownloader.csproj -c Release -p:BuildNumber=5
Select-String -Path obj\Release\net10.0\YouTubeDownloader.AssemblyInfo.cs -Pattern 'AssemblyMetadataAttribute\("BuildNumber", "5"\)'
dotnet build YouTubeDownloader.csproj -c Release
Select-String -Path obj\Release\net10.0\YouTubeDownloader.AssemblyInfo.cs -Pattern 'BuildNumber'
```
Expected: first `Select-String` prints one match; second prints nothing. Both builds `0 Warning(s)`.

- [ ] **Step 5: Commit**

```bash
git add YouTubeDownloader.csproj AboutWindow.axaml.cs
git commit -m "Embed CI build number as assembly metadata and show it in About

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Task 5: Update check and install flow in the UI

**Files:**
- Modify: `MainWindow.axaml:12-14`
- Modify: `MainWindow.axaml.cs` (usings, constructor, `CheckAndDownloadComponents`, `AktualizujKomponenty_Click` lines 721/753, `BtnDownload_Click` lines 937/955, new methods before `Informacje_Click`)

**Interfaces:**
- Consumes: `AppPaths.IsSnap`, `AppPaths.TryDeleteDirectory` (Task 1); `AppUpdater.*`, `ReleaseInfo`, `ReleaseAsset` (Task 3).
- Produces: XAML-named `MiSprawdzAktualizacje`; private `SetBusy(bool)`, `CheckForAppUpdate(bool silent)`, `InstallAppUpdate(ReleaseInfo, ReleaseAsset)`, `OpenUrl(string)`.

- [ ] **Step 1: Menu item**

In `MainWindow.axaml`, inside `<MenuItem Header="_Narzedzia">`, after `MiAktualizujKomponenty`:
```xml
        <MenuItem Name="MiSprawdzAktualizacje" Header="_Sprawdz aktualizacje aplikacji"/>
```

- [ ] **Step 2: Usings, startup cleanup, wiring**

Add to the usings of `MainWindow.axaml.cs`:
```csharp
using Avalonia.Controls.ApplicationLifetimes;
```

In the constructor, right after the `nodeJsPath = ...` line (before `InitializeComponent();`):
```csharp

        // Remove what a previous self-update left behind (<exe>.old on Windows is
        // only deletable once the old process has exited).
        AppUpdater.CleanupLeftovers(appDirectory, Environment.ProcessPath);
```

After `MiAktualizujKomponenty.Click += ...`:
```csharp
        MiSprawdzAktualizacje.Click += async (s, e) => await CheckForAppUpdate(silent: false);
```

- [ ] **Step 3: One switch for "something is running"**

Add after `UpdateStatus()`:
```csharp
    private void SetBusy(bool busy)
    {
        BtnDownload.IsEnabled = !busy;
        MiSprawdzAktualizacje.IsEnabled = !busy;
    }
```
Replace `BtnDownload.IsEnabled = false;` with `SetBusy(true);` and `BtnDownload.IsEnabled = true;` with `SetBusy(false);` in `AktualizujKomponenty_Click()` and `BtnDownload_Click()` (4 lines total). `!BtnDownload.IsEnabled` keeps meaning "busy".

- [ ] **Step 4: Startup check**

At the very end of `CheckAndDownloadComponents()` (after the `CleanupLegacyFiles` block from Task 2):
```csharp

        // async void caller: nothing may escape from here, and the startup check
        // must never bother the user with errors (offline, API rate limit).
        try
        {
            await CheckForAppUpdate(silent: true);
        }
        catch (Exception)
        {
        }
```

- [ ] **Step 5: Check and install methods**

Add before `Informacje_Click()`:
```csharp
    // ---- App self-update ----

    private async Task CheckForAppUpdate(bool silent)
    {
        const string title = "Aktualizacja aplikacji";

        if (AppPaths.IsSnap)
        {
            if (!silent)
                await MessageDialog.ShowAsync(this, "Aktualizacje tej wersji dostarcza Snap Store.", title);
            return;
        }

        int? localBuild = AppUpdater.GetLocalBuildNumber();
        if (localBuild == null)
        {
            if (!silent)
                await MessageDialog.ShowAsync(this, "Wersja zbudowana lokalnie - aktualizacje aplikacji sa wylaczone.", title);
            return;
        }

        if (!BtnDownload.IsEnabled)
            return;

        ReleaseInfo release;
        try
        {
            release = await AppUpdater.GetLatestReleaseAsync(httpClient);
        }
        catch (Exception ex)
        {
            if (!silent)
                await MessageDialog.ShowAsync(this, "Nie udalo sie sprawdzic aktualizacji: " + ex.Message, "Blad");
            return;
        }

        if (release.BuildNumber == null || release.BuildNumber <= localBuild)
        {
            if (!silent)
                await MessageDialog.ShowAsync(this, $"Masz najnowsza wersje aplikacji (build {localBuild}).", title);
            return;
        }

        if (release.Asset == null)
        {
            if (!silent)
                await MessageDialog.ShowAsync(this, "Nowa wersja jest w trakcie publikacji. Sprobuj za kilka minut.", title);
            return;
        }

        // The user may have started a download while GitHub was being queried.
        if (!BtnDownload.IsEnabled)
            return;

        bool confirmed = await MessageDialog.ShowConfirmAsync(this,
            $"Dostepna jest nowa wersja aplikacji (build {release.BuildNumber}, obecna: {localBuild}). " +
            "Zaktualizowac teraz? Aplikacja uruchomi sie ponownie.",
            title);
        if (!confirmed)
            return;

        if (!AppUpdater.CanWriteDirectory(appDirectory))
        {
            bool openPage = await MessageDialog.ShowConfirmAsync(this,
                "Brak uprawnien do zapisu w folderze aplikacji (" + appDirectory + "). " +
                "Otworzyc strone nowej wersji, aby pobrac ja recznie?",
                title);
            if (openPage)
                await OpenUrl(release.HtmlUrl);
            return;
        }

        await InstallAppUpdate(release, release.Asset);
    }

    private async Task InstallAppUpdate(ReleaseInfo release, ReleaseAsset asset)
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            await MessageDialog.ShowAsync(this, "Nie mozna ustalic sciezki aplikacji.", "Blad");
            return;
        }

        string downloadPath = OperatingSystem.IsMacOS()
            ? Path.Combine(appDirectory, AppUpdater.UpdateZipName)
            : exePath + AppUpdater.NewSuffix;

        SetBusy(true);
        try
        {
            UpdateStatus($"Pobieranie nowej wersji aplikacji (build {release.BuildNumber})...");
            await DownloadFileWithProgress(asset.DownloadUrl, downloadPath);

            if (!AppUpdater.VerifyDownload(downloadPath, asset))
                throw new InvalidDataException("Pobrany plik jest niekompletny lub uszkodzony");

            List<(string Source, string Target)> files;
            if (OperatingSystem.IsMacOS())
            {
                // macOS ships the whole publish folder (not single-file), zipped.
                string extractDirectory = Path.Combine(appDirectory, AppUpdater.UpdateDirectoryName);
                AppPaths.TryDeleteDirectory(extractDirectory);
                ZipFile.ExtractToDirectory(downloadPath, extractDirectory);
                files = AppUpdater.BuildFileList(extractDirectory, appDirectory);
            }
            else
            {
                files = new List<(string Source, string Target)> { (downloadPath, exePath) };
            }

            UpdateStatus("Instalowanie nowej wersji aplikacji...");
            AppUpdater.ApplyUpdate(files);
        }
        catch (Exception ex)
        {
            AppUpdater.DeleteUpdateDownloads(appDirectory, exePath);
            SetBusy(false);
            UpdateStatus("Blad aktualizacji aplikacji: " + ex.Message);
            await MessageDialog.ShowAsync(this, "Nie udalo sie zaktualizowac aplikacji: " + ex.Message, "Blad");
            return;
        }

        AppUpdater.DeleteUpdateDownloads(appDirectory, exePath);
        UpdateStatus("Aplikacja zaktualizowana. Ponowne uruchamianie...");

        try
        {
            AppUpdater.Relaunch(exePath);
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowAsync(this,
                "Aplikacja zostala zaktualizowana. Uruchom ja ponownie recznie. (" + ex.Message + ")",
                "Aktualizacja aplikacji");
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
        else
            Close();
    }

    private async Task OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowAsync(this, "Nie mozna otworzyc linku: " + ex.Message, "Blad");
        }
    }
```

- [ ] **Step 6: Build (Debug and Release)**

Run: `dotnet build YouTubeDownloader.csproj -c Debug` and `dotnet build YouTubeDownloader.csproj -c Release`
Expected: both `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 7: Review the silent path (Review Focus 3)**

Read `CheckForAppUpdate` top to bottom with `silent: true` in mind and confirm: every `MessageDialog` call except the confirmation is behind `if (!silent)`; `GetLatestReleaseAsync` is inside `try`; the caller in `CheckAndDownloadComponents` wraps the call. No code change expected.

- [ ] **Step 8: Prepare the end-to-end build for the human check**

```powershell
$e2e = "C:\Users\pawel\AppData\Local\Temp\claude\E--claude-YouTube-Downloader\004f1ae7-05cc-4f13-9798-d98989b0ce7c\scratchpad\update-e2e"
Remove-Item $e2e -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish YouTubeDownloader.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:BuildNumber=1 -o $e2e
Rename-Item "$e2e\YouTubeDownloader.exe" "YouTubeDownloader-win-x64.exe"
Get-ChildItem $e2e
```
Expected: publish succeeds; the folder holds `YouTubeDownloader-win-x64.exe`.

- [ ] **Step 9: HUMAN CHECKPOINT - ask the user to run these checks and report back**

This session cannot click through dialogs. Ask the user to:
1. Run `<e2e>\YouTubeDownloader-win-x64.exe` (build 1). Wait until the status says components are ready. **Expected:** a dialog "Dostepna jest nowa wersja aplikacji (build N, obecna: 1)...". Click "Tak". **Expected:** progress bar moves, the app closes and reopens by itself; "Pomoc -> Informacje" shows the current public release version (without "(build ...)", since releases before this feature have no build number). A `YouTubeDownloader-win-x64.exe.old` file remains in the folder - expected only in this test, because the old release that is now running has no cleanup code.
2. Run `dotnet run --project E:\claude\YouTube-Downloader` and choose "Narzedzia -> Sprawdz aktualizacje aplikacji". **Expected:** "Wersja zbudowana lokalnie - aktualizacje aplikacji sa wylaczone."
3. In the same window change the theme (Widok -> Ciemny), close, `dotnet run` again. **Expected:** dark theme kept; `%LOCALAPPDATA%\YouTubeDownloader\theme.txt` contains `Dark`.

Do not continue to Task 6 until the user confirms 1-3 or reports a problem.

- [ ] **Step 10: Commit**

```bash
git add MainWindow.axaml MainWindow.axaml.cs
git commit -m "Add app self-update: startup check and Narzedzia menu item

Checks the latest GitHub release on startup (silently) and from the new
\"Sprawdz aktualizacje aplikacji\" menu item; on confirmation downloads the
asset for this OS/architecture, verifies it, swaps it in and relaunches.
Disabled for local builds and inside a snap.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Task 6: CI - build number and data-directory smoke tests

**Files:**
- Modify: `.github/workflows/dotnet-desktop.yml` (publish step 136-145, Linux smoke test 168-207, macOS smoke test 218-257)

**Interfaces:**
- Consumes: `BuildNumber` MSBuild property (Task 4); `AppPaths.DataDirectory` locations (Task 1).

- [ ] **Step 1: Pass the build number**

In "Publish Application (${{ matrix.rid }})", add a line before `-o ./publish/${{ matrix.rid }}`:
```yaml
          -p:BuildNumber=${{ github.run_number }} `
```

- [ ] **Step 2: Linux smoke test checks the data directory**

Replace the comment block above "Smoke-test published binary (linux-x64 only)" (lines 168-175) with:
```yaml
    # Headless smoke-test: the only real proof (in this whole project) that the
    # Linux dependency-download logic actually works, since local development
    # happens on Windows. linux-x64 only: ubuntu-latest is an x86-64 runner and
    # cannot execute an aarch64 binary without QEMU/binfmt_misc registration,
    # which this workflow does not set up. The app downloads its tools into the
    # per-user data directory (~/.local/share/YouTubeDownloader), never next to
    # the exe - the test asserts both. Runs against a copy in an isolated temp
    # directory so nothing the app writes can end up in the release artifact.
```
In that step's script, add right after `mkdir -p "$SMOKE"`:
```bash
        DATA_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/YouTubeDownloader"
```
and replace the `if [ -x "$SMOKE/yt-dlp" ] ... fi` block with:
```bash
        if [ -x "$DATA_DIR/yt-dlp" ] && [ -x "$DATA_DIR/ffmpeg_bin/ffmpeg" ] && [ ! -e "$SMOKE/yt-dlp" ]; then
          echo "yt-dlp and ffmpeg were downloaded to $DATA_DIR and are executable - Linux dependency logic verified"
        else
          echo "App is running, but yt-dlp/ffmpeg are not in $DATA_DIR (or were written next to the exe)"
          ls -la "$SMOKE" "$DATA_DIR" "$DATA_DIR/ffmpeg_bin" 2>&1
          cat "$SMOKE/app.log"
          pkill -f "$SMOKE/${{ matrix.exeName }}" || true
          exit 1
        fi
```

- [ ] **Step 3: macOS smoke test checks the data directory**

Replace the comment block above "Smoke-test published binary (macOS)" (lines 218-224) with:
```yaml
    # Headless smoke-test: the only real proof (in this whole project) that the
    # macOS dependency-download logic actually works, since local development
    # happens on Windows. The app downloads its tools into the per-user data
    # directory (~/Library/Application Support/YouTubeDownloader), never next to
    # the exe - the test asserts both. Runs for both osx-x64 (via Rosetta,
    # installed above) and osx-arm64 (native) - both are runner.os == 'macOS'.
```
In that step's script, add right after `mkdir -p "$SMOKE"`:
```bash
        DATA_DIR="$HOME/Library/Application Support/YouTubeDownloader"
```
and replace the `if [ -x "$SMOKE/yt-dlp" ] ... fi` block with:
```bash
        if [ -x "$DATA_DIR/yt-dlp" ] && [ -x "$DATA_DIR/ffmpeg_bin/ffmpeg" ] && [ -x "$DATA_DIR/ffmpeg_bin/ffprobe" ] && [ ! -e "$SMOKE/yt-dlp" ]; then
          echo "yt-dlp and ffmpeg/ffprobe were downloaded to $DATA_DIR and are executable - macOS dependency logic verified"
        else
          echo "App is running, but yt-dlp/ffmpeg are not in $DATA_DIR (or were written next to the exe)"
          ls -la "$SMOKE" "$DATA_DIR" "$DATA_DIR/ffmpeg_bin" 2>&1
          cat "$SMOKE/app.log"
          pkill -f "$SMOKE/${{ matrix.exeName }}" || true
          exit 1
        fi
```

- [ ] **Step 4: Review the diff**

Run: `git diff .github/workflows/dotnet-desktop.yml`
Check: indentation matches the surrounding YAML (steps at 4 spaces, `run: |` bodies at 8); `$DATA_DIR` is always double-quoted (the macOS path contains a space); the PowerShell line continuation backtick is present on the new publish line. Do **not** dispatch the workflow from this branch (see Global Constraints) - it is verified by the first CI run on `main` after merge.

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/dotnet-desktop.yml
git commit -m "CI: pass run number as BuildNumber; smoke tests check data directory

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Task 7: Version bump and documentation

**Files:**
- Modify: `YouTubeDownloader.csproj` (`<Version>`)
- Modify: `README.md` (badge line 7, PL sections from line 29 and 167-203, EN sections from line 221 and 359-395)
- Modify: `CLAUDE.md`

- [ ] **Step 1: Version**

`YouTubeDownloader.csproj`: `<Version>2.0.110926</Version>` -> `<Version>2.0.280926</Version>`.
`README.md` line 7: `Version-2.0.110926-brightgreen` -> `Version-2.0.280926-brightgreen`.

- [ ] **Step 2: README - What's New (both languages)**

Insert before `### 🆕 Co nowego w wersji 2.0.110926`:
```markdown
### 🆕 Co nowego w wersji 2.0.280926

- ⬆️ **Automatyczna aktualizacja aplikacji** - przy starcie aplikacja sprawdza, czy na GitHubie jest nowsza wersja, i proponuje jej instalację (ręcznie: Narzedzia -> "Sprawdz aktualizacje aplikacji"). Po akceptacji pobiera nową wersję, podmienia się i uruchamia ponownie
- 📁 **Narzędzia w katalogu danych użytkownika** - yt-dlp, FFmpeg, Deno i wybrany motyw są teraz przechowywane w `%LOCALAPPDATA%\YouTubeDownloader` (Windows), `~/.local/share/YouTubeDownloader` (Linux) lub `~/Library/Application Support/YouTubeDownloader` (macOS), a nie obok pliku aplikacji. Przy pierwszym uruchomieniu nowej wersji narzędzia pobiorą się ponownie, stare kopie obok aplikacji zostaną usunięte, a motyw wróci do domyślnego

```
Insert before `### 🆕 What's New in 2.0.110926`:
```markdown
### 🆕 What's New in 2.0.280926

- ⬆️ **Automatic app updates** - on startup the app checks GitHub for a newer version and offers to install it (manually: Narzedzia (Tools) -> "Sprawdz aktualizacje aplikacji" (Check for app updates)). Once accepted, it downloads the new version, swaps itself out and restarts
- 📁 **Tools in the per-user data folder** - yt-dlp, FFmpeg, Deno and the chosen theme now live in `%LOCALAPPDATA%\YouTubeDownloader` (Windows), `~/.local/share/YouTubeDownloader` (Linux) or `~/Library/Application Support/YouTubeDownloader` (macOS) instead of next to the app. On the first launch of the new version the tools are downloaded again, old copies next to the app are removed, and the theme resets to the default

```

- [ ] **Step 3: README - project structure and runtime dependencies (both languages)**

In both "Struktura projektu" and "Project Structure" trees, after the two `MessageDialog.axaml.cs` lines insert (PL / EN respectively):
```
├── ThemeSettings.cs             # Zapis/odczyt wybranego motywu (theme.txt)
├── AppPaths.cs                  # Folder aplikacji vs katalog danych użytkownika, sprzątanie starych plików
├── AppUpdater.cs                # Samoaktualizacja aplikacji z GitHub Releases
```
```
├── ThemeSettings.cs             # Loads/saves the chosen theme (theme.txt)
├── AppPaths.cs                  # App folder vs per-user data folder, legacy-file cleanup
├── AppUpdater.cs                # App self-update from GitHub Releases
```
After the "Zależności runtime" bullet list (before the macOS note) add:
```markdown
Narzędzia są zapisywane w katalogu danych użytkownika: `%LOCALAPPDATA%\YouTubeDownloader` (Windows), `~/.local/share/YouTubeDownloader` (Linux), `~/Library/Application Support/YouTubeDownloader` (macOS).
```
After the "Runtime dependencies" bullet list (before the macOS note) add:
```markdown
The tools are stored in the per-user data folder: `%LOCALAPPDATA%\YouTubeDownloader` (Windows), `~/.local/share/YouTubeDownloader` (Linux), `~/Library/Application Support/YouTubeDownloader` (macOS).
```

- [ ] **Step 4: CLAUDE.md**

Make these edits (keep everything else):

1. **Repository Structure** tree - after the `MessageDialog.axaml.cs` lines insert:
```
├── ThemeSettings.cs             # Loads/saves the Light/Dark/System theme choice (theme.txt)
├── AppPaths.cs                  # App folder vs per-user data folder, legacy-file cleanup,
│                                 # MakeExecutable() and safe-delete helpers (no Avalonia)
├── AppUpdater.cs                # App self-update from GitHub Releases (no UI, no Avalonia)
```

2. **Runtime Structure** - replace the opening line `On Windows:` and the tree under it with:
````markdown
The app keeps two locations apart (`AppPaths.cs`):

- **App folder** (`AppPaths.AppDirectory`, where the executable lives) holds only the app itself and `downloads/`. Self-update replaces files here and nothing else.
- **Data folder** (`AppPaths.DataDirectory`) holds everything the app downloads or writes: `%LOCALAPPDATA%\YouTubeDownloader` (Windows), `~/.local/share/YouTubeDownloader` (Linux, honours `XDG_DATA_HOME`), `~/Library/Application Support/YouTubeDownloader` (macOS), `$SNAP_USER_COMMON` inside a snap. Falls back to the app folder if the per-user location is unavailable.

On Windows:
```
Application Directory/
├── YouTubeDownloader.exe       # The app (any file name - often the release asset name)
├── node.exe                    # Optional, user-supplied; also looked up in the data folder first
└── downloads/                  # Default download location

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
````
Then in the two following paragraphs change `On Linux, the layout is identical` to `On Linux, the data-folder layout is identical`, and `On macOS, the layout is structurally different for FFmpeg specifically` to `On macOS, the data-folder layout is structurally different for FFmpeg specifically`.

3. **Key Components -> 1. UI Components** - add:
```markdown
- `MiSprawdzAktualizacje`: menu item "Sprawdz aktualizacje aplikacji" (Check for app updates), next to "Aktualizuj komponenty"
```

4. **Dependency Management -> IsRuntimeInPath() / GetRuntimePath()** bullet - replace its three sub-bullets with:
```markdown
- `FindSystemDeno()` shells out to `where deno` (Windows) / `which deno` (Linux/macOS) and returns the first hit **outside the app folder** (`AppPaths.PickFirstPathOutside()`): `where` searches the current directory first, which for a double-clicked app is the app folder - an old `deno.exe` left there by a pre-2.0.280926 version must not count as system-wide. `where` also prints every match on its own line, so the raw output is never used as a path
- `IsRuntimeInPath()` (called from `CheckAndDownloadComponents()` to decide whether Deno must be auto-downloaded) is `FindSystemDeno() != ""`
- `GetRuntimePath()` (used to build the yt-dlp `--js-runtimes` invocation) returns the data-folder `denoPath`, then `nodeJsPath` (data folder first, then app folder), then `FindSystemDeno()`
- `CheckAndUpdateDeno()` (used by the "Update Components" menu action) does **not** shell out at all: it only checks `File.Exists(denoPath)` as a local proxy - if the app's own managed `deno` binary isn't present, it assumes a system/external runtime is in use and skips the version-check/update entirely
```

5. **MakeExecutable()** heading/bullet - rename to `AppPaths.MakeExecutable()` and add: "Moved out of `MainWindow` so `AppUpdater` can use it too."

6. **6. Update Mechanism** - append:
```markdown
**CheckForAppUpdate(bool silent) / InstallAppUpdate()** (app self-update, logic in `AppUpdater.cs`)
- Runs silently at the end of `CheckAndDownloadComponents()` and loudly from `MiSprawdzAktualizacje`. Silent mode never shows errors (offline, GitHub API rate limit)
- Off inside a snap (`AppPaths.IsSnap` - the Snap Store updates it) and for local builds (no `BuildNumber` metadata)
- Compares `AppUpdater.GetLocalBuildNumber()` with the number after the last `-` in the latest release tag (`v<Version>-<run_number>`). `<Version>` itself (`2.0.ddMMyy`) is not monotonic and is never compared. Every CI build with a higher run number counts as a new version
- Picks the asset by exact name (`AppUpdater.GetAssetName()`): `YouTubeDownloader-{win-x64,win-arm64}.exe`, `YouTubeDownloader-{linux-x64,linux-arm64}`, `YouTubeDownloader-{osx-x64,osx-arm64}.zip`. A release still being published (asset missing) is treated as "no update yet"
- If the app folder is not writable, offers the release page in the browser instead
- Downloads to `<exe>.new` (Windows/Linux) or `YouTubeDownloader-update.zip` extracted to `YouTubeDownloader-update/` (macOS), verifies size (and SHA-256 when the API gives a `digest`), then `AppUpdater.ApplyUpdate()`: every existing target -> `.old`, every new file -> target, full rollback on any failure. The running exe's `.old` on Windows is removed by `AppUpdater.CleanupLeftovers()` on the next start
- Relaunches `Environment.ProcessPath` and shuts down. The exe file name is never assumed
```

7. **Development Workflows** - in the CI paragraph, directly after the sentence ending `(see Testing Changes below).` insert: "The publish step passes `-p:BuildNumber=${{ github.run_number }}`, which the self-updater compares against release tags. Both the Linux and macOS smoke tests assert that the tools landed in the data folder (`~/.local/share/YouTubeDownloader`, `~/Library/Application Support/YouTubeDownloader`) and not next to the exe. Never run the workflow via `workflow_dispatch` from a feature branch - the publish job creates a real GitHub Release that the self-updater offers to every user."

8. **Working with This Codebase -> New Dependency** - add: "- Download it into `dataDirectory`, never `appDirectory`".

9. **App Version** - append: "Separately, CI passes `github.run_number` as the MSBuild property `BuildNumber`, which `YouTubeDownloader.csproj` turns into `AssemblyMetadata("BuildNumber", ...)` only when set. That number - not `<Version>` - is what the self-updater compares, and the About dialog shows it as \"(build N)\". Local builds have no build number, so self-update is off for them."

10. **Testing Checklist** - add:
```markdown
- [ ] Tools download into the data folder, not next to the exe; an old install's copies next to the exe are removed
- [ ] Theme choice survives a restart (theme.txt in the data folder)
- [ ] "Sprawdz aktualizacje aplikacji" on a local build shows the "aktualizacje wylaczone" message
- [ ] A CI build older than the latest release offers the update, installs it and restarts
```

11. **Last Updated**: `2026-09-28`.

- [ ] **Step 5: Build**

Run: `dotnet build YouTubeDownloader.csproj -c Release`
Expected: `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 6: Commit**

```bash
git add YouTubeDownloader.csproj README.md CLAUDE.md
git commit -m "Bump version to 2.0.280926; document data folder and self-update

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## After all tasks

- Final whole-branch review (see the execution method), then superpowers:finishing-a-development-branch.
- Tell the user plainly what is verified and what is not: harness + Windows smoke run + their manual checks cover Windows; Linux/macOS data-folder logic is verified only by the first CI run on `main`; self-update on Linux/macOS stays unverified until someone runs it there. The first release containing the updater must be downloaded manually by existing users.
