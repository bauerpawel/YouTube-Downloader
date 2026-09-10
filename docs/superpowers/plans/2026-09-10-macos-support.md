# macOS Support (x64 + ARM64) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the app actually build, launch, and download videos on macOS (x64 + ARM64), not just compile — by adding a macOS-specific FFmpeg download path (a different upstream source than Windows/Linux), extending the existing OS-conditional Deno/yt-dlp asset selection to macOS, extending `build.sh`, and extending CI to build, publish, and smoke-test on a real `macos-latest` runner.

**Architecture:** Almost all of the OS-conditional plumbing macOS needs already exists from the Linux phase (`MakeExecutable`, `where`/`which` dispatch, OS-conditional file paths in the constructor, the `denoExeName` check in `DownloadSingleUrlAsync`, ZIP-based Deno extraction) and works unchanged on macOS — Deno's macOS release even uses the identical internal zip entry name `deno` as Linux (verified below). The only genuinely new code is: (1) two small `OperatingSystem.IsMacOS()` branches in `GetDenoAssetName()`/`GetYtDlpAssetName()`, and (2) an entirely separate FFmpeg download path, because macOS's FFmpeg source (`eugeneware/ffmpeg-static`) publishes raw executable binaries as loose release assets, not an archive with a `bin/` folder like BtbN — so it cannot reuse `IsMatchingFFmpegAsset()`/`GetLatestFFmpegInfo()`/`ExtractArchive()` at all and gets its own parallel methods instead. Everything else (CI, `build.sh`, docs) mirrors the pattern already established for Linux.

**Tech Stack:** .NET 10, C# 13, Avalonia UI 12.1.2 (unchanged). No new NuGet packages, no new namespaces — `GetLatestFFmpegInfoMac()`/`DownloadFFmpegMac()` reuse `HttpClient`, `JsonDocument`, and `DownloadFileWithProgress()` exactly as the existing Deno/yt-dlp/FFmpeg download methods do.

**Spec:** `docs/superpowers/specs/2026-09-10-macos-support-design.md`

## Global Constraints

- macOS x64 **and** ARM64 together in this plan, both built and smoke-tested from a single `macos-latest` (Apple Silicon/ARM64) CI runner — `osx-x64` runs under Rosetta 2, which is not preinstalled on the runner and must be explicitly installed as a CI step.
- Windows and Linux behavior must not change. Every new/modified branch adds a macOS case without altering the existing Windows or Linux cases — where a method is changed, the Windows and Linux branches must remain byte-identical to what exists today.
- No new NuGet packages. macOS FFmpeg binaries are downloaded directly (no archive, no extraction step at all — not even the `tar` shell-out Linux uses).
- Code signing / notarization is explicitly **out of scope** for this plan (product decision, see spec). macOS users will see a Gatekeeper warning on first launch of the unsigned binary; this plan's only obligation regarding that is to document the workaround in the README, not to solve it.
- **This session cannot run or smoke-test macOS binaries** (implementation happens on Windows). Local verification per task is: `dotnet build` (Windows, must stay 0 errors/0 warnings — proves no regression) plus `dotnet publish -r osx-x64` and `-r osx-arm64` succeeding (proves the new code at least compiles/cross-compiles for macOS). **Real behavioral verification of the macOS code paths only happens via CI on a real `macos-latest` runner** (Task 4's smoke-test step) — do not claim macOS-specific logic is "verified working" from local checks alone; say "compiles for macOS, logic reviewed, not yet run on macOS" until a real CI run confirms it.
- **Open risk, not to be silently resolved by an implementer:** it is unconfirmed whether the hosted `macos-latest` runner has a GUI/window session available by default the way `xvfb` provides one on Linux. Task 4's smoke-test step is written on the assumption that it does (no `xvfb`-equivalent wrapper is used) — if the first real CI run shows the app failing to launch specifically because of a missing display/window-server, that is a CI-environment finding to report and fix in a follow-up, not a sign the plan's application-level logic (asset URLs, download code) is wrong. Do not reflexively rewrite the download logic in response to a smoke-test failure without first checking whether the failure is display-related (check `app.log`).
- Every fact this plan states about external file/asset names was verified by the controller against the real, current GitHub releases before this plan was written (downloaded and inspected with `gh api` / `curl` / `unzip -l` / `file`, not guessed) — see the verified-facts notes inline in each task.
- Commit messages must end with:
  ```
  Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01HUiuxppZn5rnUTdevdXScu
  ```

---

## Task 1: macOS branches for Deno and yt-dlp asset selection

**Files:**
- Modify: `MainWindow.axaml.cs`

**Interfaces:**
- Modifies existing `GetDenoAssetName() : string` and `GetYtDlpAssetName() : string` (private static helpers already consumed by `GetLatestDenoInfo()` and `DownloadYtDlp()` respectively — call sites do not change).

**Verified facts (checked against the real GitHub releases before writing this plan):**
- `denoland/deno` latest release (`v2.9.6` at verification time) has assets `deno-x86_64-apple-darwin.zip` and `deno-aarch64-apple-darwin.zip`. Downloaded `deno-x86_64-apple-darwin.zip` and ran `unzip -l` on it: it contains exactly one entry, named `deno` (no extension) — identical internal naming to the Linux zips. This means `DownloadDeno()`'s existing entry-name logic (`OperatingSystem.IsWindows() ? "deno.exe" : "deno"`) already picks the right entry on macOS with **no changes needed** there.
- `yt-dlp/yt-dlp` latest release has an asset named exactly `yt-dlp_macos` (a `universal2` binary — one file works on both x64 and ARM64, no architecture branch needed for this one, unlike Deno/Linux/FFmpeg).

- [ ] **Step 1: Add the macOS branch to `GetDenoAssetName()`**

Change:
```csharp
    private static string GetDenoAssetName()
    {
        if (OperatingSystem.IsWindows())
            return "deno-x86_64-pc-windows-msvc.zip";

        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Deno auto-download is only supported on Windows and Linux.");

        bool isArm = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        return isArm ? "deno-aarch64-unknown-linux-gnu.zip" : "deno-x86_64-unknown-linux-gnu.zip";
    }
```
to:
```csharp
    private static string GetDenoAssetName()
    {
        if (OperatingSystem.IsWindows())
            return "deno-x86_64-pc-windows-msvc.zip";

        bool isArm = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        if (OperatingSystem.IsMacOS())
            return isArm ? "deno-aarch64-apple-darwin.zip" : "deno-x86_64-apple-darwin.zip";

        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Deno auto-download is only supported on Windows, Linux, and macOS.");

        return isArm ? "deno-aarch64-unknown-linux-gnu.zip" : "deno-x86_64-unknown-linux-gnu.zip";
    }
```

- [ ] **Step 2: Add the macOS branch to `GetYtDlpAssetName()`**

Change:
```csharp
    private static string GetYtDlpAssetName()
    {
        if (OperatingSystem.IsWindows())
            return "yt-dlp.exe";

        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("yt-dlp auto-download is only supported on Windows and Linux.");

        bool isArm = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        return isArm ? "yt-dlp_linux_aarch64" : "yt-dlp_linux";
    }
```
to:
```csharp
    private static string GetYtDlpAssetName()
    {
        if (OperatingSystem.IsWindows())
            return "yt-dlp.exe";

        if (OperatingSystem.IsMacOS())
            return "yt-dlp_macos";

        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("yt-dlp auto-download is only supported on Windows, Linux, and macOS.");

        bool isArm = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        return isArm ? "yt-dlp_linux_aarch64" : "yt-dlp_linux";
    }
```

Note: `yt-dlp_macos` does not vary by architecture (it's a `universal2` binary) — do not add an `isArm` branch for this one, that would be wrong.

- [ ] **Step 3: Build and verify**

Run: `dotnet build YouTubeDownloader.csproj -c Release --nologo -v minimal`
Expected: 0 errors, 0 warnings.

- [ ] **Step 4: Cross-compile check for both macOS RIDs**

Run: `dotnet publish YouTubeDownloader.csproj -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./publish/osx-x64-check`
Run: `dotnet publish YouTubeDownloader.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./publish/osx-arm64-check`
Expected: both succeed (proves the new code compiles for macOS; does NOT prove the macOS download logic actually works at runtime — that's CI's job in Task 4). Delete both `./publish/osx-*-check` directories afterward, they are not meant to be committed.

- [ ] **Step 5: Smoke-test launch (Windows)**

Start the built Windows exe, wait ~3s, confirm alive, close. Expected: no crash, Windows behavior unchanged (still downloads `yt-dlp.exe` / `deno-x86_64-pc-windows-msvc.zip` exactly as before). This task changes zero Windows or Linux runtime behavior.

- [ ] **Step 6: Commit**

```bash
git add MainWindow.axaml.cs
git commit -m "$(cat <<'EOF'
Add macOS branches for Deno and yt-dlp asset selection

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HUiuxppZn5rnUTdevdXScu
EOF
)"
```

---

## Task 2: macOS FFmpeg download logic

**Files:**
- Modify: `MainWindow.axaml.cs`

**Interfaces:**
- Consumes: `MakeExecutable(string)` (existing, from the Linux phase), `DownloadFileWithProgress(string, string)` (existing).
- Produces: `GetFFmpegMacAssetName(bool isFFprobe) : string`, `GetLatestFFmpegInfoMac() : Task<(string ffmpegUrl, string ffprobeUrl, string version)>`, `DownloadFFmpegMac() : Task` — new private helpers, not consumed outside this task.

**Verified facts (checked against the real `eugeneware/ffmpeg-static` GitHub release before writing this plan):**
- Latest release (`b6.1.1` at verification time) has assets named exactly `ffmpeg-darwin-x64`, `ffmpeg-darwin-arm64`, `ffprobe-darwin-x64`, `ffprobe-darwin-arm64` (plus `.gz`-suffixed and `.LICENSE`/`.README` sidecar variants of each, which is why this plan matches asset names with **exact equality**, not `Contains`/`EndsWith` — a `Contains("ffmpeg-darwin-x64")` check would also match `ffmpeg-darwin-x64.gz`, and while the alphabetical ordering GitHub returns assets in happens to put the exact name first in this specific case, exact equality removes the risk entirely rather than depending on asset ordering).
- Downloaded the first 100 bytes of `ffmpeg-darwin-arm64` and ran `file` on it: reports `Mach-O 64-bit arm64 executable` — confirming these are raw, directly-executable binaries, not archives. There is nothing to extract; the downloaded file *is* the final `ffmpeg`/`ffprobe` binary, just needing the executable bit set (`MakeExecutable`) same as every other downloaded Linux/macOS binary in this app.
- **Important integration finding, not mentioned in the design spec:** `CheckAndUpdateFFmpeg()` (used by the "Aktualizuj komponenty" menu action) currently calls `GetLatestFFmpegInfo()` (the BtbN/Windows+Linux method) unconditionally to get the latest version for comparison. On macOS this would call into `IsMatchingFFmpegAsset()`, which still throws `PlatformNotSupportedException` for any OS that isn't Windows or Linux (Task 1 did not touch this method, and this plan does not touch it either — see Step 2's note below for why). Left unchanged, `CheckAndUpdateFFmpeg()` would **crash the update-check menu action on macOS**. This task fixes that by branching `CheckAndUpdateFFmpeg()` on `OperatingSystem.IsMacOS()` to use `GetLatestFFmpegInfoMac()` instead — this is a necessary part of "macOS support actually works," not an optional extra.

- [ ] **Step 1: Add `GetFFmpegMacAssetName()` and `GetLatestFFmpegInfoMac()`**

Add these two new methods immediately after `GetLatestFFmpegInfo()` (i.e., right before `ExtractArchive`):

```csharp
    private static string GetFFmpegMacAssetName(bool isFFprobe)
    {
        bool isArm = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        string arch = isArm ? "arm64" : "x64";
        return (isFFprobe ? "ffprobe-darwin-" : "ffmpeg-darwin-") + arch;
    }

    private async Task<(string ffmpegUrl, string ffprobeUrl, string version)> GetLatestFFmpegInfoMac()
    {
        try
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("YouTubeDownloader/1.0");
            string apiUrl = "https://api.github.com/repos/eugeneware/ffmpeg-static/releases/latest";

            var response = await httpClient.GetStringAsync(apiUrl);
            var jsonDoc = JsonDocument.Parse(response);
            var root = jsonDoc.RootElement;

            string version = root.GetProperty("tag_name").GetString() ?? "";
            string ffmpegAssetName = GetFFmpegMacAssetName(isFFprobe: false);
            string ffprobeAssetName = GetFFmpegMacAssetName(isFFprobe: true);

            string ffmpegUrl = "";
            string ffprobeUrl = "";
            var assets = root.GetProperty("assets");
            foreach (var asset in assets.EnumerateArray())
            {
                string name = asset.GetProperty("name").GetString() ?? "";
                if (name == ffmpegAssetName)
                    ffmpegUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                else if (name == ffprobeAssetName)
                    ffprobeUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
            }

            return (ffmpegUrl, ffprobeUrl, version);
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowAsync(this, "Blad pobierania informacji FFmpeg: " + ex.Message, "Blad");
            return ("", "", "");
        }
    }
```

Note: `IsMatchingFFmpegAsset()` and `GetLatestFFmpegInfo()` (the BtbN-querying methods) are **not modified** in this step or anywhere in this task — macOS never calls them, it has this fully separate pair of methods instead.

- [ ] **Step 2: Add `DownloadFFmpegMac()`**

Add this new method immediately after `DownloadFFmpeg()` (i.e., right before `DownloadFileWithProgress`):

```csharp
    private async Task DownloadFFmpegMac()
    {
        try
        {
            UpdateStatus("Pobieranie informacji FFmpeg...");
            var (ffmpegUrl, ffprobeUrl, version) = await GetLatestFFmpegInfoMac();

            if (string.IsNullOrEmpty(ffmpegUrl) || string.IsNullOrEmpty(ffprobeUrl))
                throw new Exception("Nie znaleziono linku do FFmpeg");

            UpdateStatus("Pobieranie FFmpeg (" + version + ")...");

            if (Directory.Exists(ffmpegBinPath))
                Directory.Delete(ffmpegBinPath, true);
            Directory.CreateDirectory(ffmpegBinPath);

            string ffmpegDestPath = Path.Combine(ffmpegBinPath, "ffmpeg");
            string ffprobeDestPath = Path.Combine(ffmpegBinPath, "ffprobe");

            await DownloadFileWithProgress(ffmpegUrl, ffmpegDestPath);
            MakeExecutable(ffmpegDestPath);

            await DownloadFileWithProgress(ffprobeUrl, ffprobeDestPath);
            MakeExecutable(ffprobeDestPath);

            string versionFile = Path.Combine(appDirectory, "ffmpeg_version.txt");
            await File.WriteAllTextAsync(versionFile, version);

            UpdateStatus("FFmpeg pobrane. Wersja: " + version);
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowAsync(this, "Blad FFmpeg: " + ex.Message, "Blad");
            UpdateStatus("Blad: " + ex.Message);
        }
    }
```

This method never calls `ExtractArchive()` — there is nothing to extract, `ffmpeg`/`ffprobe` are downloaded directly to their final location in `ffmpeg_bin/`.

- [ ] **Step 3: Branch `DownloadFFmpeg()` to `DownloadFFmpegMac()` on macOS**

Change:
```csharp
    private async Task DownloadFFmpeg()
    {
        try
        {
            UpdateStatus("Pobieranie informacji FFmpeg...");
            var (downloadUrl, version) = await GetLatestFFmpegInfo();
```
to:
```csharp
    private async Task DownloadFFmpeg()
    {
        if (OperatingSystem.IsMacOS())
        {
            await DownloadFFmpegMac();
            return;
        }

        try
        {
            UpdateStatus("Pobieranie informacji FFmpeg...");
            var (downloadUrl, version) = await GetLatestFFmpegInfo();
```
(The rest of `DownloadFFmpeg()` — everything after this point in the method — is unchanged. Windows and Linux still reach it exactly as before; macOS returns before ever reaching it.)

- [ ] **Step 4: Fix `CheckAndUpdateFFmpeg()` to use the macOS info source**

This is the integration bug described above — without this step, clicking "Aktualizuj komponenty" on macOS throws `PlatformNotSupportedException` from deep inside `GetLatestFFmpegInfo()`.

Change:
```csharp
    private async Task CheckAndUpdateFFmpeg()
    {
        try
        {
            var (_, latestVersion) = await GetLatestFFmpegInfo();

            string versionFile = Path.Combine(appDirectory, "ffmpeg_version.txt");
```
to:
```csharp
    private async Task CheckAndUpdateFFmpeg()
    {
        try
        {
            string latestVersion;
            if (OperatingSystem.IsMacOS())
                (_, _, latestVersion) = await GetLatestFFmpegInfoMac();
            else
                (_, latestVersion) = await GetLatestFFmpegInfo();

            string versionFile = Path.Combine(appDirectory, "ffmpeg_version.txt");
```
(The rest of `CheckAndUpdateFFmpeg()` is unchanged — it already calls `DownloadFFmpeg()` generically, which by Step 3 already branches to `DownloadFFmpegMac()` on macOS.)

- [ ] **Step 5: Build and verify**

Run: `dotnet build YouTubeDownloader.csproj -c Release --nologo -v minimal`
Expected: 0 errors, 0 warnings.

- [ ] **Step 6: Cross-compile check for both macOS RIDs**

Run: `dotnet publish YouTubeDownloader.csproj -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./publish/osx-x64-check`
Run: `dotnet publish YouTubeDownloader.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./publish/osx-arm64-check`
Expected: both succeed. Delete both `./publish/osx-*-check` directories afterward.

- [ ] **Step 7: Smoke-test launch (Windows)**

Start the built Windows exe, wait ~3s, confirm alive, close. Expected: no crash, Windows FFmpeg download behavior unchanged (still calls `GetLatestFFmpegInfo()`/BtbN via the unmodified `else` branch in `DownloadFFmpeg()` and `CheckAndUpdateFFmpeg()`).

- [ ] **Step 8: Commit**

```bash
git add MainWindow.axaml.cs
git commit -m "$(cat <<'EOF'
Add macOS FFmpeg download logic (eugeneware/ffmpeg-static)

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HUiuxppZn5rnUTdevdXScu
EOF
)"
```

---

## Task 3: build.sh — accept osx-x64/osx-arm64

**Files:**
- Modify: `build.sh`

**Interfaces:** None (standalone script, not consumed by app code).

- [ ] **Step 1: Extend the RID validation and help text**

Change:
```bash
RID="${1:-linux-x64}"
if [ "$RID" != "linux-x64" ] && [ "$RID" != "linux-arm64" ]; then
    echo "[BLAD] Nieznana architektura \"$RID\". Uzyj linux-x64 lub linux-arm64."
    echo "Przyklad: ./build.sh linux-arm64"
    exit 1
fi
```
to:
```bash
RID="${1:-linux-x64}"
case "$RID" in
    linux-x64|linux-arm64|osx-x64|osx-arm64) ;;
    *)
        echo "[BLAD] Nieznana architektura \"$RID\". Uzyj linux-x64, linux-arm64, osx-x64 lub osx-arm64."
        echo "Przyklad: ./build.sh osx-arm64"
        exit 1
        ;;
esac
```
(Switched from the two-armed `[ ... ] && [ ... ]` check to a `case` statement — four allowed values is unwieldy as a chain of `&&`-joined `!=` checks, and `case` is the idiomatic bash way to validate against a fixed set.)

- [ ] **Step 2: Update the trailing hint line**

Change:
```bash
echo "Wskazowka: aby zbudowac dla ARM64, uruchom: ./build.sh linux-arm64"
```
to:
```bash
echo "Wskazowka: inne architektury: ./build.sh linux-arm64 | ./build.sh osx-x64 | ./build.sh osx-arm64"
```

- [ ] **Step 3: Verify Windows build still passes**

This task doesn't touch any `.cs`/`.axaml` file. Run: `dotnet build YouTubeDownloader.csproj -c Release --nologo -v minimal` as a sanity check. Expected: 0 errors, 0 warnings (unchanged from before this task).

There is no way to actually run `build.sh` from this Windows session (bash script). Note this in the report — it is reviewed for correctness (the `case` statement's syntax, matching the same output-path/publish-flags logic already used for the Linux RIDs) but not executed, matching how `build.sh` itself was verified when first created in the Linux phase.

- [ ] **Step 4: Commit**

```bash
git add build.sh
git commit -m "$(cat <<'EOF'
Extend build.sh to accept osx-x64/osx-arm64

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HUiuxppZn5rnUTdevdXScu
EOF
)"
```

---

## Task 4: CI workflow — build macOS, publish macOS RIDs, smoke-test

**Files:**
- Modify: `.github/workflows/dotnet-desktop.yml`

**Interfaces:** None (CI config only).

This is the task that provides **real** verification of everything Tasks 1-2 wrote, since this session cannot run macOS binaries locally.

- [ ] **Step 1: Replace the entire workflow file**

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
        os: [windows-latest, ubuntu-latest, macos-latest]

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
          - rid: osx-x64
            os: macos-latest
            exeName: YouTubeDownloader
            releaseExeName: YouTubeDownloader-osx-x64
          - rid: osx-arm64
            os: macos-latest
            exeName: YouTubeDownloader
            releaseExeName: YouTubeDownloader-osx-arm64

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
    - name: Mark binary executable (Linux/macOS)
      if: runner.os == 'Linux' || runner.os == 'macOS'
      shell: bash
      run: chmod +x ./publish/${{ matrix.rid }}/${{ matrix.exeName }}

    # Headless smoke-test: the only real proof (in this whole project) that the
    # Linux dependency-download logic (added in the Linux support phase) actually
    # works, since local development happens on Windows. linux-x64 only:
    # ubuntu-latest is an x86-64 runner and cannot execute an aarch64 binary
    # without QEMU/binfmt_misc registration, which this workflow does not set up.
    # Runs against a copy in an isolated temp directory (not ./publish/<rid>/),
    # since the app downloads its own dependencies on startup and that directory
    # gets zipped/uploaded as the release artifact.
    - name: Smoke-test published binary (linux-x64 only)
      if: matrix.rid == 'linux-x64'
      shell: bash
      run: |
        sudo apt-get update
        sudo apt-get install -y xvfb libx11-6 libice6 libsm6 fontconfig fonts-dejavu-core
        SMOKE="$RUNNER_TEMP/smoke"
        mkdir -p "$SMOKE"
        cp ./publish/${{ matrix.rid }}/${{ matrix.exeName }} "$SMOKE/"
        chmod +x "$SMOKE/${{ matrix.exeName }}"
        xvfb-run --auto-servernum "$SMOKE/${{ matrix.exeName }}" > "$SMOKE/app.log" 2>&1 &
        APP_PID=$!
        sleep 60
        if kill -0 $APP_PID 2>/dev/null; then
          echo "App is running (PID $APP_PID) after 60s"
        else
          echo "App failed to start or crashed"
          cat "$SMOKE/app.log"
          exit 1
        fi
        if [ -x "$SMOKE/yt-dlp" ] && [ -x "$SMOKE/ffmpeg_bin/ffmpeg" ]; then
          echo "yt-dlp and ffmpeg were downloaded and are executable - Linux dependency logic verified"
        else
          echo "App is running, but yt-dlp/ffmpeg were not downloaded/executable as expected"
          ls -la "$SMOKE" "$SMOKE/ffmpeg_bin" 2>&1
          cat "$SMOKE/app.log"
          pkill -f "$SMOKE/${{ matrix.exeName }}" || true
          exit 1
        fi
        pkill -f "$SMOKE/${{ matrix.exeName }}" || true
        kill $APP_PID 2>/dev/null || true
        rm -rf "$SMOKE"

    # osx-x64 runs on the ARM64 macos-latest runner via Rosetta 2, which is not
    # preinstalled - it must be explicitly installed before the smoke-test step
    # below can execute the x64 binary. Not needed for osx-arm64 (runs natively)
    # or for the publish step above (a pure cross-compile, independent of host arch).
    - name: Install Rosetta (osx-x64 on ARM64 runner)
      if: matrix.rid == 'osx-x64'
      shell: bash
      run: sudo softwareupdate --install-rosetta --agree-to-license

    # Headless smoke-test: the only real proof (in this whole project) that the
    # macOS dependency-download logic (Task 2) actually works, since local
    # development happens on Windows. Runs against a copy in an isolated temp
    # directory (not ./publish/<rid>/), since the app downloads its own
    # dependencies on startup and that directory gets zipped/uploaded as the
    # release artifact. Runs for both osx-x64 (via Rosetta, installed above) and
    # osx-arm64 (native) - both are runner.os == 'macOS' on this single runner.
    - name: Smoke-test published binary (macOS)
      if: runner.os == 'macOS'
      shell: bash
      run: |
        SMOKE="$RUNNER_TEMP/smoke"
        mkdir -p "$SMOKE"
        cp ./publish/${{ matrix.rid }}/${{ matrix.exeName }} "$SMOKE/"
        chmod +x "$SMOKE/${{ matrix.exeName }}"
        "$SMOKE/${{ matrix.exeName }}" > "$SMOKE/app.log" 2>&1 &
        APP_PID=$!
        sleep 60
        if kill -0 $APP_PID 2>/dev/null; then
          echo "App is running (PID $APP_PID) after 60s"
        else
          echo "App failed to start or crashed"
          cat "$SMOKE/app.log"
          exit 1
        fi
        if [ -x "$SMOKE/yt-dlp" ] && [ -x "$SMOKE/ffmpeg_bin/ffmpeg" ] && [ -x "$SMOKE/ffmpeg_bin/ffprobe" ]; then
          echo "yt-dlp and ffmpeg/ffprobe were downloaded and are executable - macOS dependency logic verified"
        else
          echo "App is running, but yt-dlp/ffmpeg were not downloaded/executable as expected"
          ls -la "$SMOKE" "$SMOKE/ffmpeg_bin" 2>&1
          cat "$SMOKE/app.log"
          pkill -f "$SMOKE/${{ matrix.exeName }}" || true
          exit 1
        fi
        pkill -f "$SMOKE/${{ matrix.exeName }}" || true
        kill $APP_PID 2>/dev/null || true
        rm -rf "$SMOKE"

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

    - name: Mark release asset executable (Linux/macOS)
      if: runner.os == 'Linux' || runner.os == 'macOS'
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
Extend CI to build, publish, and smoke-test macOS x64/ARM64

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HUiuxppZn5rnUTdevdXScu
EOF
)"
```

**Note for the controller (not a step for the implementer):** this workflow only actually runs on a `push`/`pull_request`/`workflow_dispatch` event against GitHub. Merely committing it locally does not execute it — getting real macOS CI feedback requires pushing this branch (or merging) to GitHub, which is a decision for the human to make explicitly, not something to do automatically as part of this task. Per this plan's Global Constraints, the macOS smoke-test step's behavior (particularly: does the runner have a usable GUI session without an `xvfb`-equivalent step) is a genuine open question that only a real CI run answers.

---

## Task 5: Documentation — CLAUDE.md and README.md

**Files:**
- Modify: `CLAUDE.md`
- Modify: `README.md`

**Interfaces:** None (documentation only).

- [ ] **Step 1: Read both files as they currently stand**

Both were already rewritten for the Linux phase and reflect that architecture — this task adds macOS to them, it does not replace them wholesale.

- [ ] **Step 2: Update CLAUDE.md's platform/overview language**

Change the "Target Platform" line:
```
- **Target Platform**: Windows and Linux (x64/ARM64), `net10.0`. Built and published for both OSes, and CI-smoke-tested on `ubuntu-latest` in addition to `windows-latest` - see [Development Workflows](#development-workflows). macOS is not supported yet (tracked under Future Improvements)
```
to:
```
- **Target Platform**: Windows, Linux, and macOS (x64/ARM64), `net10.0`. Built and published for all three OSes, and CI-smoke-tested on `ubuntu-latest` and `macos-latest` in addition to `windows-latest` - see [Development Workflows](#development-workflows). macOS binaries are unsigned (no Apple Developer account) - users see a one-time Gatekeeper warning on first launch, see [External Dependencies](#external-dependencies)
```

Update the Project Overview's first paragraph ("**YouTube Downloader** is a Windows and Linux desktop application...") to "**YouTube Downloader** is a Windows, Linux, and macOS desktop application...".

- [ ] **Step 3: Update the Repository Structure comment for build.sh**

Change:
```
├── build.sh                    # Wraps `dotnet publish` for linux-x64/linux-arm64 (Linux)
```
to:
```
├── build.sh                    # Wraps `dotnet publish` for linux-x64/linux-arm64/osx-x64/osx-arm64
```

- [ ] **Step 4: Add a macOS paragraph to Runtime Structure**

Immediately after the existing Linux runtime-structure paragraph (the one explaining the static vs `-shared` FFmpeg choice), add a macOS paragraph explaining the structural difference: FFmpeg on macOS comes from a different upstream (`eugeneware/ffmpeg-static`) as two loose, already-executable binaries (`ffmpeg`, `ffprobe`) rather than an archive with a `bin/` folder — so `ffmpeg_bin/` on macOS never has anything beyond those two files (no `ffplay`, since `eugeneware/ffmpeg-static` doesn't publish one). Note that yt-dlp on macOS is a single `universal2` binary (no separate x64/ARM64 asset, unlike Deno and FFmpeg).

- [ ] **Step 5: Extend the Dependency Management section's method descriptions**

In the `GetDenoAssetName() / DownloadDeno() / GetLatestDenoInfo()` bullet, add the macOS asset names to the list: `deno-x86_64-apple-darwin.zip` / `deno-aarch64-apple-darwin.zip` (pattern: `deno-*-apple-darwin.zip`), and note the internal zip entry name is `deno` on macOS too (same as Linux, verified — no extraction-logic difference needed).

In the `GetYtDlpAssetName() / DownloadYtDlp()` bullet, add: macOS: `yt-dlp_macos` (a single `universal2` binary, not split by architecture like the Linux/Deno assets).

Replace the `IsMatchingFFmpegAsset() / DownloadFFmpeg() / GetLatestFFmpegInfo()` bullet's introduction to make clear it now only covers Windows/Linux, and add a new bullet immediately after it, titled `GetFFmpegMacAssetName() / GetLatestFFmpegInfoMac() / DownloadFFmpegMac()`, explaining: macOS FFmpeg comes from `eugeneware/ffmpeg-static` (BtbN/FFmpeg-Builds does not publish macOS binaries), assets are matched by **exact name equality** (`ffmpeg-darwin-x64`/`ffmpeg-darwin-arm64`/`ffprobe-darwin-x64`/`ffprobe-darwin-arm64` — exact equality specifically because the release also publishes `.gz`/`.LICENSE`/`.README` sidecar assets whose names contain the target name as a substring), the two matched assets are raw executable binaries (not an archive - nothing is extracted, they are downloaded directly to `ffmpeg_bin/ffmpeg` and `ffmpeg_bin/ffprobe` and marked executable via the existing `MakeExecutable()` helper), and `DownloadFFmpeg()`/`CheckAndUpdateFFmpeg()` both branch to this path via `OperatingSystem.IsMacOS()` before reaching any BtbN-specific code.

- [ ] **Step 6: Update Development Workflows**

Add macOS publish commands alongside the existing Linux ones:
```bash
# Publish single-file executable (macOS x64, Intel)
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true

# Publish single-file executable (macOS ARM64, Apple Silicon)
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true

# Or use build.sh, which wraps the above (defaults to linux-x64):
./build.sh osx-arm64
```

Update the CI paragraph that currently ends "...builds and publishes all four RIDs - `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64` - on every push to `main`, running across `windows-latest` and `ubuntu-latest` runners..." to mention all six RIDs and all three runners (`windows-latest`, `ubuntu-latest`, `macos-latest`), and add a sentence noting `osx-x64` builds via cross-compilation on the ARM64 `macos-latest` runner and is smoke-tested there via Rosetta 2 (explicitly installed as a CI step, since it is not preinstalled). Update the sentence describing OS-conditional dependency logic to include macOS's distinct FFmpeg source.

- [ ] **Step 7: Update Testing Changes' coverage caveat**

The existing paragraph explains Windows gets a full manual pass while Linux CI only proves headless launch-without-crash. Extend it to note macOS gets the same category of coverage as Linux (CI launch + dependency-download smoke-test on `macos-latest`, not a manual interactive QA pass) — and add the explicit caveat that the macOS smoke-test's assumption about `macos-latest` having a usable GUI session without a virtual-display step is unconfirmed until a real CI run proves it (see this plan's Global Constraints and Task 4).

- [ ] **Step 8: Update Dependency Versions**

Change:
```
- **yt-dlp**: Always latest from GitHub releases (Windows: `yt-dlp.exe`; Linux: `yt-dlp_linux`/`yt-dlp_linux_aarch64`)
- **FFmpeg**: Latest autobuild from BtbN/FFmpeg-Builds (Windows: `win64-gpl-shared`; Linux: static `gpl` build - `linux64-gpl.tar.xz`/`linuxarm64-gpl.tar.xz`, see Dependency Management above for why static rather than shared)
- **Deno**: Latest release from denoland/deno (Windows: `x86_64-pc-windows-msvc`; Linux: `x86_64-unknown-linux-gnu`/`aarch64-unknown-linux-gnu`)
```
to:
```
- **yt-dlp**: Always latest from GitHub releases (Windows: `yt-dlp.exe`; Linux: `yt-dlp_linux`/`yt-dlp_linux_aarch64`; macOS: `yt-dlp_macos`, a single `universal2` binary for both architectures)
- **FFmpeg**: Windows/Linux - latest autobuild from BtbN/FFmpeg-Builds (Windows: `win64-gpl-shared`; Linux: static `gpl` build - `linux64-gpl.tar.xz`/`linuxarm64-gpl.tar.xz`, see Dependency Management above for why static rather than shared). macOS - latest release from `eugeneware/ffmpeg-static` (BtbN does not publish macOS builds): loose binaries `ffmpeg-darwin-x64`/`ffmpeg-darwin-arm64` and `ffprobe-darwin-x64`/`ffprobe-darwin-arm64`, downloaded directly with no archive/extraction step
- **Deno**: Latest release from denoland/deno (Windows: `x86_64-pc-windows-msvc`; Linux: `x86_64-unknown-linux-gnu`/`aarch64-unknown-linux-gnu`; macOS: `x86_64-apple-darwin`/`aarch64-apple-darwin`)
```

- [ ] **Step 9: Update External Dependencies and add the Gatekeeper note**

Change the FFmpeg bullet under "Runtime Dependencies (Auto-downloaded)":
```
- **FFmpeg**: Audio/video processing
  - Source: https://github.com/BtbN/FFmpeg-Builds
  - License: GPL (Windows: `gpl-shared` build; Linux: static `gpl` build - see Dependency Management above)
```
to:
```
- **FFmpeg**: Audio/video processing
  - Source (Windows/Linux): https://github.com/BtbN/FFmpeg-Builds
    - License: GPL (Windows: `gpl-shared` build; Linux: static `gpl` build - see Dependency Management above)
  - Source (macOS): https://github.com/eugeneware/ffmpeg-static
    - License: GPL (BtbN does not publish macOS builds; this is a separately-maintained project also used as the `ffmpeg-static` npm package)
```

Immediately after the "NuGet Dependencies" section, add a new subsection documenting the Gatekeeper caveat:
```markdown
## macOS Gatekeeper Notice

The macOS builds of this application are **not code-signed or notarized** (that requires a paid Apple Developer Program account, which this project does not currently have). On first launch, macOS Gatekeeper will refuse to open the downloaded binary with a warning that it is "from an unidentified developer" or "cannot be verified." Users must explicitly allow it once: either right-click (or Control-click) the app and choose "Open" from the context menu (this shows an "Open anyway" option Gatekeeper doesn't offer on a plain double-click), or clear the quarantine attribute from a terminal: `xattr -d com.apple.quarantine <path-to-binary>`. This is a one-time step per download; it does not need to be repeated on subsequent launches of the same binary.
```

- [ ] **Step 10: Update Future Improvements**

Remove item 11 (macOS Support — it now describes work this plan implements, so it is no longer a future improvement) from CLAUDE.md's "Future Improvements to Consider" list, and renumber the remaining items if the list uses explicit numbers that would otherwise leave a gap (items 1-10 stay as-is since 11 was last; removing it needs no renumbering).

- [ ] **Step 11: Update CLAUDE.md's footer**

Change:
```
**Last Updated**: 2026-09-10
**For**: AI Assistants (Claude, etc.)
**Project**: YouTube Downloader for Windows and Linux (.NET 10)
```
to:
```
**Last Updated**: 2026-09-10
**For**: AI Assistants (Claude, etc.)
**Project**: YouTube Downloader for Windows, Linux, and macOS (.NET 10)
```
(Use the actual date this task runs, if different from 2026-09-10.)

- [ ] **Step 12: Update README.md (both Polish and English sections)**

In both language sections, mirror the same set of changes:
- Platform badge: change `![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux-0078D6?logo=windows)` to `![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-0078D6?logo=windows)`.
- Opening description paragraph: "aplikacja desktopowa na Windows i Linux (x64/ARM64)" → "aplikacja desktopowa na Windows, Linux i macOS (x64/ARM64)" (Polish); "Windows and Linux desktop application" → "Windows, Linux, and macOS desktop application" (English).
- The "Struktura projektu" / "Project Structure" code block: update the `build.sh` comment line the same way as CLAUDE.md's Step 3 above.
- Add a short "macOS" bullet under "Zależności runtime" / "Runtime dependencies", or extend the existing FFmpeg bullet, noting the `eugeneware/ffmpeg-static` source for macOS and a one-line pointer to the Gatekeeper caveat (full detail lives in CLAUDE.md; README only needs a user-facing summary: "Uwaga (macOS): binarki nie sa podpisane - przy pierwszym uruchomieniu system pokaze ostrzezenie Gatekeeper, patrz sekcja ponizej." / "Note (macOS): binaries are unsigned - on first launch macOS Gatekeeper will show a warning, see below.").
- Add a short "🔒 Gatekeeper (macOS)" subsection (Polish and English versions) near the Usage section, giving the two workarounds (right-click → Open, or `xattr -d com.apple.quarantine`) in user-facing terms — this is the README's own copy of the same fact CLAUDE.md documents for AI-assistant context, phrased for end users rather than for a future code-modifying AI.

Do **not** change the "🛠️ Wymagania" / "Requirements" section's "Do uruchomienia aplikacji" / "To run the application" OS requirement line (`Windows 10 lub nowszy` / `Windows 10 or newer`) unless you also add the equivalent for Linux and macOS — check whether that line was already left Windows-only after the Linux phase (i.e., this is a pre-existing gap, not one introduced by this task) before deciding whether to touch it. If it's a pre-existing Linux-support gap, leave it as-is; fixing unrelated pre-existing documentation gaps is out of scope for this plan.

- [ ] **Step 13: Sanity-check build is unaffected**

Run: `dotnet build YouTubeDownloader.csproj -c Release --nologo -v minimal` — this task only touches Markdown, so this is a pure regression check. Expected: 0 errors, 0 warnings.

- [ ] **Step 14: Commit**

```bash
git add CLAUDE.md README.md
git commit -m "$(cat <<'EOF'
Document macOS support in CLAUDE.md and README.md

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HUiuxppZn5rnUTdevdXScu
EOF
)"
```

---

## Task 6: Local verification pass

**Files:** None (verification only).

**Interfaces:** None.

- [ ] **Step 1: Full Windows build**

Run: `dotnet build YouTubeDownloader.csproj -c Release --nologo -v minimal`
Expected: 0 errors, 0 warnings.

- [ ] **Step 2: Windows smoke-test (regression check, matching the pattern used in the Avalonia and Linux phases)**

Run `build.bat win-x64`, smoke-test the resulting exe (Start-Process/Get-Process/Stop-Process, ~3s). Confirm it still downloads `yt-dlp.exe`/`deno-x86_64-pc-windows-msvc.zip`/the BtbN Windows FFmpeg build exactly as before this plan.

- [ ] **Step 3: macOS cross-compile check (both RIDs)**

Run: `dotnet publish YouTubeDownloader.csproj -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./publish/osx-x64`
Run: `dotnet publish YouTubeDownloader.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./publish/osx-arm64`
Expected: both succeed, producing `publish/osx-x64/YouTubeDownloader` and `publish/osx-arm64/YouTubeDownloader` (no extension, same as Linux). **Do not attempt to run either** — they are macOS Mach-O binaries, this session's host is Windows.

- [ ] **Step 4: Linux cross-compile regression check (both RIDs)**

Run: `dotnet publish YouTubeDownloader.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./publish/linux-x64`
Run: `dotnet publish YouTubeDownloader.csproj -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./publish/linux-arm64`
Expected: both still succeed (this plan should not have broken Linux cross-compilation — regression check only, Task 1/2's `else` branches are meant to be untouched).

- [ ] **Step 5: Report results, with an explicit caveat**

Summarize to the controller: Windows build/publish/smoke-test status (should be unchanged from before this plan — regression check), Linux publish status (still compiles — regression check), macOS publish status (compiles, cannot be run here). State clearly and explicitly: **the macOS-specific logic (asset selection, direct binary download, `CheckAndUpdateFFmpeg()`'s macOS branch) has been code-reviewed and cross-compiles, but has NOT been executed on macOS by this task** — that only happens when Task 4's CI workflow actually runs on GitHub (which requires a push, a decision for the human). Also flag the open Global Constraints risk again explicitly: whether `macos-latest`'s smoke-test needs a virtual-display step is unconfirmed until that CI run happens.
