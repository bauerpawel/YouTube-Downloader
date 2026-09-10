# Linux Support (x64 + ARM64) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the app actually build, launch, and download videos on Linux (x64 + ARM64), not just compile — by adapting the dependency-management logic (yt-dlp/FFmpeg/Deno download + extraction) to be OS-aware, adding a Linux build script, and extending CI to build and headlessly smoke-test on a real Ubuntu runner.

**Architecture:** All changes live in `MainWindow.axaml.cs` (the only file with dependency-management logic), plus a new `build.sh`, an extended CI workflow, and doc updates. Every OS-specific branch uses `OperatingSystem.IsWindows()` / `RuntimeInformation.ProcessArchitecture` to pick the right asset name, file extension, and extraction method — the Windows code path is preserved byte-for-byte wherever possible so existing behavior does not regress.

**Tech Stack:** .NET 10, C# 13, Avalonia UI 12.1.2 (unchanged). New: `System.Runtime.InteropServices` (architecture detection), `File.SetUnixFileMode` (POSIX permissions), system `tar` invoked via `Process` (no new NuGet dependency).

**Spec:** `docs/superpowers/specs/2026-09-10-linux-support-design.md`

## Global Constraints

- Linux x64 **and** ARM64 together in this plan. macOS is explicitly out of scope (separate future phase).
- Windows behavior must not change. Every new branch is `OperatingSystem.IsWindows() ? <existing behavior> : <new Linux behavior>` — the Windows side of every ternary/branch must be byte-identical to what exists today.
- No new NuGet packages. FFmpeg's `.tar.xz` is extracted by shelling out to the system `tar` command (present on every Linux distro), not a library.
- No refactor of UI, `BuildYtDlpArguments()`, `ParseDownloadProgress()`, `NormalizeUrl()`/`ValidateUrl()`, or the download orchestration flow — those already work cross-platform unchanged.
- **This session cannot run or smoke-test Linux binaries** (implementation happens on Windows). Local verification per task is: `dotnet build` (Windows, must stay 0 errors/0 warnings — proves no regression) plus `dotnet publish -r linux-x64` and `-r linux-arm64` succeeding (proves the new code at least compiles/cross-compiles for Linux). **Real behavioral verification of the Linux code paths only happens via CI on a real `ubuntu-latest` runner** (Task 5's headless smoke-test step) — do not claim Linux-specific logic is "verified working" from local checks alone; say "compiles for Linux, logic reviewed, not yet run on Linux" until a real CI run confirms it.
- Every fact this plan states about external file formats/names (Deno/yt-dlp/FFmpeg asset names, FFmpeg's internal archive structure, the Deno zip's internal entry name) was verified by the controller against the real, current GitHub releases before this plan was written (downloaded and inspected, not guessed).
- Commit messages must end with:
  ```
  Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01HUiuxppZn5rnUTdevdXScu
  ```

---

## Task 1: OS-conditional paths, MakeExecutable helper, where/which, deno.exe check

**Files:**
- Modify: `MainWindow.axaml.cs`

**Interfaces:**
- Produces: `private static void MakeExecutable(string path)` — no-ops on Windows, sets `rwxr-xr-x` via `File.SetUnixFileMode` elsewhere. Consumed by Tasks 2 and 3.
- Produces OS-aware `ytDlpPath`, `denoPath`, `nodeJsPath` fields (extension included only on Windows) — consumed by Tasks 2 and 3, and already consumed as-is by every existing method that reads these fields (no other call site needs to change).

This task alone causes **zero behavior change on Windows** (`OperatingSystem.IsWindows()` is always true there) — it only lays plumbing. Safe to verify with a normal Windows build + smoke test.

- [ ] **Step 1: Make the three dependency paths OS-conditional**

In `MainWindow.axaml.cs`, in the constructor, change:
```csharp
        httpClient = new HttpClient();
        appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        ytDlpPath = Path.Combine(appDirectory, "yt-dlp.exe");
        ffmpegBinPath = Path.Combine(appDirectory, "ffmpeg_bin");
        denoPath = Path.Combine(appDirectory, "deno.exe");
        denoVersionPath = Path.Combine(appDirectory, "deno_version.txt");
        nodeJsPath = Path.Combine(appDirectory, "node.exe");
```
to:
```csharp
        httpClient = new HttpClient();
        appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        ytDlpPath = Path.Combine(appDirectory, OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp");
        ffmpegBinPath = Path.Combine(appDirectory, "ffmpeg_bin");
        denoPath = Path.Combine(appDirectory, OperatingSystem.IsWindows() ? "deno.exe" : "deno");
        denoVersionPath = Path.Combine(appDirectory, "deno_version.txt");
        nodeJsPath = Path.Combine(appDirectory, OperatingSystem.IsWindows() ? "node.exe" : "node");
```

- [ ] **Step 2: `where` → `which` in `IsRuntimeInPath()`**

Change:
```csharp
    private bool IsRuntimeInPath()
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = "deno",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                process.WaitForExit(2000);
                return process.ExitCode == 0;
            }
        }
        catch { }
        return false;
    }
```
to:
```csharp
    private bool IsRuntimeInPath()
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
            if (process != null)
            {
                process.WaitForExit(2000);
                return process.ExitCode == 0;
            }
        }
        catch { }
        return false;
    }
```

- [ ] **Step 3: `where` → `which` in `GetRuntimePath()`**

Change:
```csharp
    private string GetRuntimePath()
    {
        if (File.Exists(denoPath))
            return denoPath;

        if (File.Exists(nodeJsPath))
            return nodeJsPath;

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = "deno",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                process.WaitForExit();
                if (process.ExitCode == 0)
                {
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    if (!string.IsNullOrEmpty(output))
                        return output;
                }
            }
        }
        catch { }

        return "";
    }
```
to:
```csharp
    private string GetRuntimePath()
    {
        if (File.Exists(denoPath))
            return denoPath;

        if (File.Exists(nodeJsPath))
            return nodeJsPath;

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
            if (process != null)
            {
                process.WaitForExit();
                if (process.ExitCode == 0)
                {
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    if (!string.IsNullOrEmpty(output))
                        return output;
                }
            }
        }
        catch { }

        return "";
    }
```

- [ ] **Step 4: Fix the hardcoded `"deno.exe"` check in `DownloadSingleUrlAsync`**

Change:
```csharp
            string normalizedUrl = NormalizeUrl(rawUrl);
            string jsRuntimeArg = runtimePath.EndsWith("deno.exe", StringComparison.OrdinalIgnoreCase) ? "" : "--js-runtimes node";
```
to:
```csharp
            string normalizedUrl = NormalizeUrl(rawUrl);
            string denoExeName = OperatingSystem.IsWindows() ? "deno.exe" : "deno";
            string jsRuntimeArg = runtimePath.EndsWith(denoExeName, StringComparison.OrdinalIgnoreCase) ? "" : "--js-runtimes node";
```

- [ ] **Step 5: Add the `MakeExecutable` helper**

Add this new method immediately after `DownloadFileWithProgress` (i.e., right before `CheckAndUpdateDeno`):
```csharp
    private static void MakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        File.SetUnixFileMode(path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }
```
It is not called from anywhere yet — that's expected, Tasks 2 and 3 call it. (An unused private method is not a C# compiler warning, so this will not break the "0 warnings" build check.)

- [ ] **Step 6: Build and verify**

Run: `dotnet build YouTubeDownloader.csproj -c Release --nologo -v minimal`
Expected: 0 errors, 0 warnings.

- [ ] **Step 7: Smoke-test launch**

Start the built exe (PowerShell: Start-Process, wait ~3s, Get-Process to confirm alive, Stop-Process to close). Expected: no crash, behaves identically to before this task (this task changes zero Windows runtime behavior).

- [ ] **Step 8: Commit**

```bash
git add MainWindow.axaml.cs
git commit -m "$(cat <<'EOF'
Add OS-conditional dependency paths and Unix executable-permission helper

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HUiuxppZn5rnUTdevdXScu
EOF
)"
```

---

## Task 2: Cross-platform Deno + yt-dlp download logic

**Files:**
- Modify: `MainWindow.axaml.cs`

**Interfaces:**
- Consumes: `MakeExecutable(string)` from Task 1.
- Produces: `GetDenoAssetName() : string`, `GetYtDlpAssetName() : string` — private helpers, not consumed elsewhere in this plan, but follow the same naming pattern Task 3 will use for FFmpeg.

- [ ] **Step 1: Add the `System.Runtime.InteropServices` using**

Change the top-of-file using block from:
```csharp
using System.Net.Http;
using System.Text;
```
to:
```csharp
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
```

- [ ] **Step 2: Make Deno asset selection OS/architecture-aware**

Verified asset names (checked against the real `denoland/deno` latest release before writing this plan): Windows `deno-x86_64-pc-windows-msvc.zip` (unchanged), Linux x64 `deno-x86_64-unknown-linux-gnu.zip`, Linux ARM64 `deno-aarch64-unknown-linux-gnu.zip`. All three are ZIP files containing a single entry — on Windows that entry is named `deno.exe`, on Linux it is named exactly `deno` (verified by downloading and listing the real Linux zip's contents).

Change:
```csharp
    private async Task<(string downloadUrl, string version)> GetLatestDenoInfo()
    {
        try
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("YouTubeDownloader/1.0");
            string apiUrl = "https://api.github.com/repos/denoland/deno/releases/latest";

            var response = await httpClient.GetStringAsync(apiUrl);
            var jsonDoc = JsonDocument.Parse(response);
            var root = jsonDoc.RootElement;

            string version = root.GetProperty("tag_name").GetString() ?? "";

            string downloadUrl = "";
            var assets = root.GetProperty("assets");
            foreach (var asset in assets.EnumerateArray())
            {
                string name = asset.GetProperty("name").GetString() ?? "";
                if (name.Contains("deno-x86_64-pc-windows-msvc.zip"))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                    break;
                }
            }

            return (downloadUrl, version);
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowAsync(this, "Blad pobierania informacji Deno: " + ex.Message, "Blad");
            return ("", "");
        }
    }
```
to:
```csharp
    private static string GetDenoAssetName()
    {
        if (OperatingSystem.IsWindows())
            return "deno-x86_64-pc-windows-msvc.zip";

        bool isArm = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        return isArm ? "deno-aarch64-unknown-linux-gnu.zip" : "deno-x86_64-unknown-linux-gnu.zip";
    }

    private async Task<(string downloadUrl, string version)> GetLatestDenoInfo()
    {
        try
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("YouTubeDownloader/1.0");
            string apiUrl = "https://api.github.com/repos/denoland/deno/releases/latest";

            var response = await httpClient.GetStringAsync(apiUrl);
            var jsonDoc = JsonDocument.Parse(response);
            var root = jsonDoc.RootElement;

            string version = root.GetProperty("tag_name").GetString() ?? "";
            string assetName = GetDenoAssetName();

            string downloadUrl = "";
            var assets = root.GetProperty("assets");
            foreach (var asset in assets.EnumerateArray())
            {
                string name = asset.GetProperty("name").GetString() ?? "";
                if (name.Contains(assetName))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                    break;
                }
            }

            return (downloadUrl, version);
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowAsync(this, "Blad pobierania informacji Deno: " + ex.Message, "Blad");
            return ("", "");
        }
    }
```

- [ ] **Step 3: Extract the right zip entry and mark it executable**

Change:
```csharp
    private async Task DownloadDeno()
    {
        try
        {
            UpdateStatus("Pobieranie Deno runtime...");

            var (downloadUrl, version) = await GetLatestDenoInfo();

            if (string.IsNullOrEmpty(downloadUrl))
            {
                await MessageDialog.ShowAsync(this, "Nie znaleziono Deno dla Windows", "Blad");
                return;
            }

            string zipPath = Path.Combine(appDirectory, "deno.zip");
            await DownloadFileWithProgress(downloadUrl, zipPath);

            UpdateStatus("Rozpakowywanie Deno...");

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                var denoEntry = archive.GetEntry("deno.exe");
                if (denoEntry != null)
                    denoEntry.ExtractToFile(denoPath, true);
            }

            File.Delete(zipPath);

            if (!string.IsNullOrEmpty(version))
                await File.WriteAllTextAsync(denoVersionPath, version);

            UpdateStatus("Deno zainstalowane");
        }
        catch (Exception ex)
        {
            UpdateStatus("Blad Deno: " + ex.Message);
            await MessageDialog.ShowAsync(this, "Nie udalo sie pobrac Deno. Zainstaluj z https://deno.com", "Ostrzezenie");
        }
    }
```
to:
```csharp
    private async Task DownloadDeno()
    {
        try
        {
            UpdateStatus("Pobieranie Deno runtime...");

            var (downloadUrl, version) = await GetLatestDenoInfo();

            if (string.IsNullOrEmpty(downloadUrl))
            {
                await MessageDialog.ShowAsync(this, "Nie znaleziono Deno dla tego systemu", "Blad");
                return;
            }

            string zipPath = Path.Combine(appDirectory, "deno.zip");
            await DownloadFileWithProgress(downloadUrl, zipPath);

            UpdateStatus("Rozpakowywanie Deno...");

            string denoEntryName = OperatingSystem.IsWindows() ? "deno.exe" : "deno";
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                var denoEntry = archive.GetEntry(denoEntryName);
                if (denoEntry != null)
                    denoEntry.ExtractToFile(denoPath, true);
            }

            File.Delete(zipPath);

            MakeExecutable(denoPath);

            if (!string.IsNullOrEmpty(version))
                await File.WriteAllTextAsync(denoVersionPath, version);

            UpdateStatus("Deno zainstalowane");
        }
        catch (Exception ex)
        {
            UpdateStatus("Blad Deno: " + ex.Message);
            await MessageDialog.ShowAsync(this, "Nie udalo sie pobrac Deno. Zainstaluj z https://deno.com", "Ostrzezenie");
        }
    }
```

- [ ] **Step 4: Make yt-dlp download OS/architecture-aware**

Verified against the real `yt-dlp/yt-dlp` latest release: Windows keeps today's `yt-dlp.exe` (unchanged), Linux x64 is asset `yt-dlp_linux`, Linux ARM64 is asset `yt-dlp_linux_aarch64` — both direct-downloadable via the same `.../releases/latest/download/<name>` URL pattern already used today (confirmed both URLs resolve with HTTP 200 before writing this plan).

Change:
```csharp
    private async Task DownloadYtDlp()
    {
        try
        {
            string url = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
            await DownloadFileWithProgress(url, ytDlpPath);
            UpdateStatus("yt-dlp pobrane");
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowAsync(this, "Blad pobierania yt-dlp: " + ex.Message, "Blad");
        }
    }
```
to:
```csharp
    private static string GetYtDlpAssetName()
    {
        if (OperatingSystem.IsWindows())
            return "yt-dlp.exe";

        bool isArm = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        return isArm ? "yt-dlp_linux_aarch64" : "yt-dlp_linux";
    }

    private async Task DownloadYtDlp()
    {
        try
        {
            string url = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/" + GetYtDlpAssetName();
            await DownloadFileWithProgress(url, ytDlpPath);
            MakeExecutable(ytDlpPath);
            UpdateStatus("yt-dlp pobrane");
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowAsync(this, "Blad pobierania yt-dlp: " + ex.Message, "Blad");
        }
    }
```

- [ ] **Step 5: Build and verify**

Run: `dotnet build YouTubeDownloader.csproj -c Release --nologo -v minimal`
Expected: 0 errors, 0 warnings.

- [ ] **Step 6: Cross-compile check for Linux**

Run: `dotnet publish YouTubeDownloader.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./publish/linux-x64-check`
Expected: publish succeeds (proves the new code compiles for Linux; does NOT prove the Linux download logic actually works at runtime — that's CI's job in Task 5). Delete `./publish/linux-x64-check` afterward, it's not meant to be committed.

- [ ] **Step 7: Smoke-test launch (Windows)**

Start the built Windows exe, wait ~3s, confirm alive, close. Expected: no crash, Windows behavior unchanged (still downloads `yt-dlp.exe` / `deno-x86_64-pc-windows-msvc.zip` exactly as before).

- [ ] **Step 8: Commit**

```bash
git add MainWindow.axaml.cs
git commit -m "$(cat <<'EOF'
Add cross-platform Deno and yt-dlp download logic for Linux x64/ARM64

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HUiuxppZn5rnUTdevdXScu
EOF
)"
```

---

## Task 3: Cross-platform FFmpeg download/extraction logic

**Files:**
- Modify: `MainWindow.axaml.cs`

**Interfaces:**
- Consumes: `MakeExecutable(string)` from Task 1, `System.Runtime.InteropServices` using from Task 2.
- Produces: `IsMatchingFFmpegAsset(string) : bool`, `ExtractArchive(string, string) : Task` — private helpers, not consumed outside this task.

- [ ] **Step 1: Make FFmpeg asset selection OS/architecture-aware**

Verified against the real `BtbN/FFmpeg-Builds` releases: Windows keeps today's exact match (`Contains("win64-gpl-shared") && EndsWith(".zip")`, unchanged). For Linux, use the **static** build variant (no `-shared` suffix) — verified by downloading and inspecting both variants: the `-shared` build splits executables (`bin/`) from `.so` libraries (`lib/`), which today's "copy everything from the `bin` folder" logic would miss entirely (silent runtime failure, ffmpeg unable to find its libraries); the static (non-`-shared`) build has **only** a `bin/` folder with statically-linked `ffmpeg`/`ffprobe`/`ffplay` and no external `.so` dependency, so it works with the existing "copy `bin/*`" logic completely unchanged. Match Linux assets by exact suffix `linux64-gpl.tar.xz` (x64) / `linuxarm64-gpl.tar.xz` (ARM64) — verified these suffixes correctly exclude both the `-shared` and `lgpl` variants (`EndsWith` on the full suffix, not a loose `Contains("gpl")`).

Change:
```csharp
    private async Task<(string downloadUrl, string version)> GetLatestFFmpegInfo()
    {
        try
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("YouTubeDownloader/1.0");
            string releasesUrl = "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases";
            var response = await httpClient.GetStringAsync(releasesUrl);
            var jsonDoc = JsonDocument.Parse(response);
            var releases = jsonDoc.RootElement;

            string autobuildTag = "";
            string downloadUrl = "";

            foreach (var release in releases.EnumerateArray())
            {
                string tagName = release.GetProperty("tag_name").GetString() ?? "";

                if (tagName.StartsWith("autobuild-"))
                {
                    autobuildTag = tagName;

                    var assets = release.GetProperty("assets");
                    foreach (var asset in assets.EnumerateArray())
                    {
                        string assetName = asset.GetProperty("name").GetString() ?? "";
                        if (assetName.Contains("win64-gpl-shared") && assetName.EndsWith(".zip"))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(downloadUrl))
                        break;
                }
            }

            return (downloadUrl, autobuildTag);
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowAsync(this, "Blad pobierania FFmpeg info: " + ex.Message, "Blad");
            return ("", "");
        }
    }
```
to:
```csharp
    private static bool IsMatchingFFmpegAsset(string assetName)
    {
        if (OperatingSystem.IsWindows())
            return assetName.Contains("win64-gpl-shared") && assetName.EndsWith(".zip");

        bool isArm = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        string suffix = isArm ? "linuxarm64-gpl.tar.xz" : "linux64-gpl.tar.xz";
        return assetName.EndsWith(suffix);
    }

    private async Task<(string downloadUrl, string version)> GetLatestFFmpegInfo()
    {
        try
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("YouTubeDownloader/1.0");
            string releasesUrl = "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases";
            var response = await httpClient.GetStringAsync(releasesUrl);
            var jsonDoc = JsonDocument.Parse(response);
            var releases = jsonDoc.RootElement;

            string autobuildTag = "";
            string downloadUrl = "";

            foreach (var release in releases.EnumerateArray())
            {
                string tagName = release.GetProperty("tag_name").GetString() ?? "";

                if (tagName.StartsWith("autobuild-"))
                {
                    autobuildTag = tagName;

                    var assets = release.GetProperty("assets");
                    foreach (var asset in assets.EnumerateArray())
                    {
                        string assetName = asset.GetProperty("name").GetString() ?? "";
                        if (IsMatchingFFmpegAsset(assetName))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(downloadUrl))
                        break;
                }
            }

            return (downloadUrl, autobuildTag);
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowAsync(this, "Blad pobierania FFmpeg info: " + ex.Message, "Blad");
            return ("", "");
        }
    }
```

- [ ] **Step 2: Add archive extraction abstraction and use it in `DownloadFFmpeg`**

Note: the local variable `zipPath` is renamed to `archivePath` throughout `DownloadFFmpeg` — this is deliberate (it is no longer always a zip), not a mismatched edit.

Change:
```csharp
    private async Task DownloadFFmpeg()
    {
        try
        {
            UpdateStatus("Pobieranie informacji FFmpeg...");
            var (downloadUrl, version) = await GetLatestFFmpegInfo();

            if (string.IsNullOrEmpty(downloadUrl))
                throw new Exception("Nie znaleziono linku do FFmpeg");

            UpdateStatus("Pobieranie FFmpeg (" + version + ")...");
            string zipPath = Path.Combine(appDirectory, "ffmpeg.zip");

            await DownloadFileWithProgress(downloadUrl, zipPath);

            UpdateStatus("Rozpakowywanie FFmpeg...");

            if (Directory.Exists(ffmpegBinPath))
                Directory.Delete(ffmpegBinPath, true);

            string tempExtractPath = Path.Combine(appDirectory, "ffmpeg_temp");
            if (Directory.Exists(tempExtractPath))
                Directory.Delete(tempExtractPath, true);

            ZipFile.ExtractToDirectory(zipPath, tempExtractPath);

            string[] binPaths = Directory.GetDirectories(tempExtractPath, "bin", SearchOption.AllDirectories);

            if (binPaths.Length == 0)
                throw new Exception("Nie znaleziono folderu bin");

            string sourceBinPath = binPaths[0];
            Directory.CreateDirectory(ffmpegBinPath);

            foreach (string file in Directory.GetFiles(sourceBinPath))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(ffmpegBinPath, fileName);
                File.Copy(file, destFile, true);
            }

            string versionFile = Path.Combine(appDirectory, "ffmpeg_version.txt");
            await File.WriteAllTextAsync(versionFile, version);

            File.Delete(zipPath);
            Directory.Delete(tempExtractPath, true);

            UpdateStatus("FFmpeg pobrane. Wersja: " + version);
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowAsync(this, "Blad FFmpeg: " + ex.Message, "Blad");
            UpdateStatus("Blad: " + ex.Message);
        }
    }
```
to:
```csharp
    private static async Task ExtractArchive(string archivePath, string destinationPath)
    {
        if (OperatingSystem.IsWindows())
        {
            ZipFile.ExtractToDirectory(archivePath, destinationPath);
            return;
        }

        Directory.CreateDirectory(destinationPath);
        var processInfo = new ProcessStartInfo
        {
            FileName = "tar",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };
        processInfo.ArgumentList.Add("-xf");
        processInfo.ArgumentList.Add(archivePath);
        processInfo.ArgumentList.Add("-C");
        processInfo.ArgumentList.Add(destinationPath);

        using var process = Process.Start(processInfo);
        if (process == null)
            throw new Exception("Nie udalo sie uruchomic tar");

        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            string error = await process.StandardError.ReadToEndAsync();
            throw new Exception("tar zakonczyl sie bledem: " + error);
        }
    }

    private async Task DownloadFFmpeg()
    {
        try
        {
            UpdateStatus("Pobieranie informacji FFmpeg...");
            var (downloadUrl, version) = await GetLatestFFmpegInfo();

            if (string.IsNullOrEmpty(downloadUrl))
                throw new Exception("Nie znaleziono linku do FFmpeg");

            UpdateStatus("Pobieranie FFmpeg (" + version + ")...");
            string archiveExtension = OperatingSystem.IsWindows() ? ".zip" : ".tar.xz";
            string archivePath = Path.Combine(appDirectory, "ffmpeg" + archiveExtension);

            await DownloadFileWithProgress(downloadUrl, archivePath);

            UpdateStatus("Rozpakowywanie FFmpeg...");

            if (Directory.Exists(ffmpegBinPath))
                Directory.Delete(ffmpegBinPath, true);

            string tempExtractPath = Path.Combine(appDirectory, "ffmpeg_temp");
            if (Directory.Exists(tempExtractPath))
                Directory.Delete(tempExtractPath, true);

            await ExtractArchive(archivePath, tempExtractPath);

            string[] binPaths = Directory.GetDirectories(tempExtractPath, "bin", SearchOption.AllDirectories);

            if (binPaths.Length == 0)
                throw new Exception("Nie znaleziono folderu bin");

            string sourceBinPath = binPaths[0];
            Directory.CreateDirectory(ffmpegBinPath);

            foreach (string file in Directory.GetFiles(sourceBinPath))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(ffmpegBinPath, fileName);
                File.Copy(file, destFile, true);
                MakeExecutable(destFile);
            }

            string versionFile = Path.Combine(appDirectory, "ffmpeg_version.txt");
            await File.WriteAllTextAsync(versionFile, version);

            File.Delete(archivePath);
            Directory.Delete(tempExtractPath, true);

            UpdateStatus("FFmpeg pobrane. Wersja: " + version);
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowAsync(this, "Blad FFmpeg: " + ex.Message, "Blad");
            UpdateStatus("Blad: " + ex.Message);
        }
    }
```
(`MakeExecutable(destFile)` is called for every copied file — a no-op on Windows, so it is safe to call unconditionally rather than special-casing which files need it.)

- [ ] **Step 3: Build and verify**

Run: `dotnet build YouTubeDownloader.csproj -c Release --nologo -v minimal`
Expected: 0 errors, 0 warnings.

- [ ] **Step 4: Cross-compile check for both Linux RIDs**

Run: `dotnet publish YouTubeDownloader.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./publish/linux-x64-check`
Run: `dotnet publish YouTubeDownloader.csproj -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./publish/linux-arm64-check`
Expected: both succeed. Delete both `./publish/linux-*-check` directories afterward.

- [ ] **Step 5: Smoke-test launch (Windows)**

Start the built Windows exe, wait ~3s, confirm alive, close. Expected: no crash, Windows FFmpeg download/extraction behavior unchanged (still `ZipFile.ExtractToDirectory` on the `win64-gpl-shared` zip).

- [ ] **Step 6: Commit**

```bash
git add MainWindow.axaml.cs
git commit -m "$(cat <<'EOF'
Add cross-platform FFmpeg download/extraction logic for Linux x64/ARM64

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HUiuxppZn5rnUTdevdXScu
EOF
)"
```

---

## Task 4: build.sh for local Linux/macOS builds

**Files:**
- Create: `build.sh`

**Interfaces:** None (standalone script, not consumed by app code).

- [ ] **Step 1: Create build.sh**

```bash
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
```

- [ ] **Step 2: Mark it executable and verify Windows can still build**

Since this file is authored on Windows (no native execute bit), explicitly mark it executable in git's index so it is checked out with the right permissions on Linux/macOS:
```bash
chmod +x build.sh
git update-index --chmod=+x build.sh
```

Run: `dotnet build YouTubeDownloader.csproj -c Release --nologo -v minimal` — this task doesn't touch any `.cs`/`.axaml` file, so this is just a sanity check that nothing else broke. Expected: 0 errors, 0 warnings (unchanged from before this task).

There is no way to actually run `build.sh` from this Windows session (bash script). Note this in the report — it is reviewed for correctness (mirrors `build.bat`'s exact logic, translated to bash) but not executed.

- [ ] **Step 3: Commit**

```bash
git add build.sh
git commit -m "$(cat <<'EOF'
Add build.sh for local Linux publish (mirrors build.bat)

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HUiuxppZn5rnUTdevdXScu
EOF
)"
```

---

## Task 5: CI workflow — build Linux, publish Linux RIDs, headless smoke-test

**Files:**
- Modify: `.github/workflows/dotnet-desktop.yml`

**Interfaces:** None (CI config only).

This is the task that provides **real** verification of everything Tasks 1-3 wrote, since this session cannot run Linux binaries locally.

- [ ] **Step 1: Replace the entire workflow file**

The existing hardcoded `YouTubeDownloader.exe` filenames (in the artifact/release steps) must become RID-aware, since the Linux binary has no `.exe` extension. Also, every `run:` step in this file uses PowerShell syntax (`$env:X`, backtick line continuation) — `ubuntu-latest` defaults to `bash`, not `pwsh`, so a workflow-level `defaults: run: shell: pwsh` is added (PowerShell Core is preinstalled on `ubuntu-latest`) rather than rewriting every step in two shells.

Replace the full contents of `.github/workflows/dotnet-desktop.yml` with:

```yaml
# This workflow builds and publishes the YouTube Downloader .NET Avalonia application

name: .NET Desktop

permissions:
  actions: write
  contents: write

on:
  push:
    branches: [ "main" ]
  pull_request:
    branches: [ "main" ]
  workflow_dispatch:

env:
  Solution_Name: YouTubeDownloader.csproj

defaults:
  run:
    shell: pwsh

jobs:
  build:
    strategy:
      matrix:
        configuration: [Debug, Release]
        os: [windows-latest, ubuntu-latest]

    runs-on: ${{ matrix.os }}

    steps:
    - name: Checkout
      uses: actions/checkout@v7
      with:
        fetch-depth: 0

    # Install the .NET 10 workload
    - name: Install .NET 10
      uses: actions/setup-dotnet@v6
      with:
        dotnet-version: 10.0.x

    # Restore dependencies
    - name: Restore dependencies
      run: dotnet restore $env:Solution_Name

    # Build the application
    - name: Build
      run: dotnet build $env:Solution_Name -c ${{ matrix.configuration }} --no-restore

    # Execute all unit tests in the solution
    - name: Execute unit tests
      run: dotnet test $env:Solution_Name -c ${{ matrix.configuration }} --no-build --verbosity normal
      continue-on-error: true  # Continue if no tests exist

  publish:
    needs: build
    if: github.event_name == 'push' || github.event_name == 'workflow_dispatch'  # not on forked PRs

    strategy:
      max-parallel: 1  # avoid racing when RIDs update the same GitHub Release
      matrix:
        include:
          - rid: win-x64
            os: windows-latest
            exeName: YouTubeDownloader.exe
            releaseExeName: YouTubeDownloader-win-x64.exe
          - rid: win-arm64
            os: windows-latest
            exeName: YouTubeDownloader.exe
            releaseExeName: YouTubeDownloader-win-arm64.exe
          - rid: linux-x64
            os: ubuntu-latest
            exeName: YouTubeDownloader
            releaseExeName: YouTubeDownloader-linux-x64
          - rid: linux-arm64
            os: ubuntu-latest
            exeName: YouTubeDownloader
            releaseExeName: YouTubeDownloader-linux-arm64

    runs-on: ${{ matrix.os }}

    steps:
    - name: Checkout
      uses: actions/checkout@v7
      with:
        fetch-depth: 0

    # Install the .NET 10 workload
    - name: Install .NET 10
      uses: actions/setup-dotnet@v6
      with:
        dotnet-version: 10.0.x

    # Restore dependencies
    - name: Restore dependencies
      run: dotnet restore $env:Solution_Name

    # Publish the self-contained single-file application for this architecture
    - name: Publish Application (${{ matrix.rid }})
      run: |
        dotnet publish $env:Solution_Name `
          -c Release `
          -r ${{ matrix.rid }} `
          --self-contained true `
          -p:PublishSingleFile=true `
          -p:IncludeNativeLibrariesForSelfExtract=true `
          -p:EnableCompressionInSingleFile=true `
          -o ./publish/${{ matrix.rid }}

    # ZIP/copy steps below don't reliably preserve the Unix executable bit across
    # platforms, so the binary is (re-)marked executable at each point it's used.
    - name: Mark binary executable (Linux)
      if: runner.os == 'Linux'
      shell: bash
      run: chmod +x ./publish/${{ matrix.rid }}/${{ matrix.exeName }}

    # Headless smoke-test: the only real proof (in this whole project) that the
    # Linux build actually starts, since local development happens on Windows.
    - name: Smoke-test published binary (Linux)
      if: runner.os == 'Linux'
      shell: bash
      run: |
        sudo apt-get update && sudo apt-get install -y xvfb
        xvfb-run --auto-servernum ./publish/${{ matrix.rid }}/${{ matrix.exeName }} &
        APP_PID=$!
        sleep 5
        if kill -0 $APP_PID 2>/dev/null; then
          echo "App is running (PID $APP_PID)"
          kill $APP_PID
        else
          echo "App failed to start or crashed"
          exit 1
        fi

    # Package publish folder - create zip of the publish folder
    - name: Package publish folder
      run: Compress-Archive -Path ./publish/${{ matrix.rid }}/* -DestinationPath ./publish/${{ matrix.rid }}/YouTubeDownloader-${{ matrix.rid }}.zip -Force

    # Verify publish output
    - name: Verify publish output
      run: dir ./publish/${{ matrix.rid }}

    # Upload build artifacts
    - name: Upload Release Artifacts
      uses: actions/upload-artifact@v7
      with:
        name: YouTubeDownloader-${{ matrix.rid }}
        path: ./publish/${{ matrix.rid }}/${{ matrix.exeName }}
        if-no-files-found: error

    # Upload all published files as artifact
    - name: Upload Full Release Package
      uses: actions/upload-artifact@v7
      with:
        name: YouTubeDownloader-${{ matrix.rid }}-full-package
        path: ./publish/${{ matrix.rid }}/
        if-no-files-found: error

    # GitHub release assets must have unique file names across the whole release,
    # so give the per-RID exe a distinct name before attaching it.
    - name: Prepare release asset names
      run: Copy-Item ./publish/${{ matrix.rid }}/${{ matrix.exeName }} ./publish/${{ matrix.rid }}/${{ matrix.releaseExeName }}

    - name: Mark release asset executable (Linux)
      if: runner.os == 'Linux'
      shell: bash
      run: chmod +x ./publish/${{ matrix.rid }}/${{ matrix.releaseExeName }}

    # Create/update a GitHub Release and upload the exe and zip as release assets
    # Note: actions/create-release and actions/upload-release-asset are archived/unmaintained,
    # replaced with softprops/action-gh-release which covers both in one step.
    - name: Create Release
      uses: softprops/action-gh-release@v3
      env:
        GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      with:
        tag_name: v${{ github.run_number }}-${{ github.sha }}
        name: Automated Release ${{ github.run_number }}
        body: Automated release for run ${{ github.run_id }} (commit ${{ github.sha }})
        draft: false
        prerelease: false
        files: |
          ./publish/${{ matrix.rid }}/${{ matrix.releaseExeName }}
          ./publish/${{ matrix.rid }}/YouTubeDownloader-${{ matrix.rid }}.zip
```

- [ ] **Step 2: Validate YAML syntax**

Run (from a Python environment, if available): `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/dotnet-desktop.yml')); print('YAML OK')"`. If Python/PyYAML isn't available, visually re-read the file for indentation consistency instead — do not skip this check silently.

- [ ] **Step 3: Confirm nothing outside this file changed**

Run: `dotnet build YouTubeDownloader.csproj -c Release --nologo -v minimal` — this task only touches YAML, so this is a sanity check. Expected: 0 errors, 0 warnings (unchanged).

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/dotnet-desktop.yml
git commit -m "$(cat <<'EOF'
Extend CI to build, publish, and smoke-test Linux x64/ARM64

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HUiuxppZn5rnUTdevdXScu
EOF
)"
```

**Note for the controller (not a step for the implementer):** this workflow only actually runs on a `push`/`pull_request`/`workflow_dispatch` event against GitHub. Merely committing it locally does not execute it — getting real Linux CI feedback requires pushing this branch (or merging) to GitHub, which is a decision for the human to make explicitly, not something to do automatically as part of this task.

---

## Task 6: Update CLAUDE.md for cross-platform dependency logic

**Files:**
- Modify: `CLAUDE.md`

**Interfaces:** None (documentation only).

- [ ] **Step 1: Read the current CLAUDE.md**

Read the file as it stands now (it was already rewritten for the Avalonia migration and reflects that architecture) — this task adds to it, not replaces it wholesale.

- [ ] **Step 2: Document the OS-conditional dependency logic**

Wherever CLAUDE.md currently describes dependency download/update behavior (yt-dlp/FFmpeg/Deno paths, asset names, extraction), add a note that this logic is now OS-conditional: Windows behavior is unchanged from before; Linux (x64 + ARM64) downloads different GitHub release assets (name the pattern: yt-dlp direct binary `yt-dlp_linux`/`yt-dlp_linux_aarch64`, Deno zip `deno-*-unknown-linux-gnu.zip`, FFmpeg **static** tar.xz `*linux64-gpl.tar.xz`/`*linuxarm64-gpl.tar.xz` — explicitly note it is the static, non-`-shared` FFmpeg variant and why), extracts `.tar.xz` via the system `tar` command instead of `ZipFile`, and marks downloaded binaries executable via `File.SetUnixFileMode` (the `MakeExecutable` helper) since neither the zip/tar extraction nor the raw download preserves the Unix executable bit.

- [ ] **Step 3: Document build.sh and the new RIDs**

Wherever CLAUDE.md documents `build.bat`/publish commands, add the Linux equivalents: `./build.sh` (defaults to `linux-x64`) / `./build.sh linux-arm64`, and the `dotnet publish -r linux-x64` / `-r linux-arm64` commands.

- [ ] **Step 4: Update the "macOS/Linux not supported" framing**

CLAUDE.md currently states the app is Windows-only. Update this to say Windows + Linux (x64/ARM64) are supported; macOS remains unsupported (future phase). Do not overstate — this plan's Linux support is verified by CI on `ubuntu-latest`, not by running on a variety of real-world Linux distros/desktop environments, so keep the language proportionate ("built and smoke-tested on Ubuntu via CI" rather than "supports Linux" unqualified, if CLAUDE.md's existing tone makes that distinction meaningful).

- [ ] **Step 5: Update "Last Updated" date**

Change to `2026-09-10` (or the actual date this task runs, if different).

- [ ] **Step 6: Commit**

```bash
git add CLAUDE.md
git commit -m "$(cat <<'EOF'
Update CLAUDE.md for cross-platform (Linux) dependency logic

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HUiuxppZn5rnUTdevdXScu
EOF
)"
```

---

## Task 7: Local verification pass

**Files:** None (verification only).

**Interfaces:** None.

- [ ] **Step 1: Full Windows build**

Run: `dotnet build YouTubeDownloader.csproj -c Release --nologo -v minimal`
Expected: 0 errors, 0 warnings.

- [ ] **Step 2: Windows smoke-test (both RIDs, matching the existing pattern from the Avalonia migration)**

Run `build.bat win-x64`, smoke-test the resulting exe (Start-Process/Get-Process/Stop-Process, ~3s). Run `build.bat win-arm64`, confirm publish succeeds (do not attempt to launch — this session's host is x64).

- [ ] **Step 3: Linux cross-compile check (both RIDs)**

Run: `dotnet publish YouTubeDownloader.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./publish/linux-x64`
Run: `dotnet publish YouTubeDownloader.csproj -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./publish/linux-arm64`
Expected: both succeed, producing `publish/linux-x64/YouTubeDownloader` and `publish/linux-arm64/YouTubeDownloader` (no `.exe` extension). **Do not attempt to run either** — they are Linux ELF binaries, this session's host is Windows.

- [ ] **Step 4: Report results, with an explicit caveat**

Summarize to the controller: Windows build/publish/smoke-test status (should be unchanged from before this plan — regression check), Linux publish status (compiles, cannot be run here). State clearly and explicitly: **the Linux-specific logic (asset selection, tar extraction, chmod) has been code-reviewed and cross-compiles, but has NOT been executed on Linux by this task** — that only happens when Task 5's CI workflow actually runs on GitHub (which requires a push, a decision for the human).

