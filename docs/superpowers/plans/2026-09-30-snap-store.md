# Snap Store Package (`yt-downloader-bp`) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the app as a strict-confined snap `yt-downloader-bp` in the Snap Store `stable` channel (amd64 + arm64), published by CI on every push to `main`, with downloads going to the user's Downloads folder inside a snap.

**Architecture:** One small code change: `AppPaths.DownloadsDirectory` (Avalonia-free, pure resolver functions) replaces the two hard-coded `Path.Combine(appDirectory, "downloads")` in `MainWindow`, because `$SNAP` is read-only. Packaging is `snap/snapcraft.yaml` (`dump` plugin over the already-built single-file binary, `core24`, `gnome` extension). CI gets a `snap` job after `publish` that packs the `linux-x64`/`linux-arm64` artifacts, smoke-tests the *installed* snap and releases it to `stable`. A temporary workflow verifies the packaging on the feature branch first, because the real job needs `publish`, which runs only on `main`.

**Tech Stack:** .NET 10 / C# 13 (unchanged), snapcraft (`core24`, `gnome` extension), GitHub Actions: `snapcore/action-build@v1`, `snapcore/action-publish@v1`, `actions/checkout@v7`, `actions/download-artifact@v8`. Local workflow lint: `actionlint` 1.7.12.

**Spec:** `docs/superpowers/specs/2026-09-30-snap-store-design.md`

## Global Constraints

- Branch: `claude/snap-store` (exists; the spec is committed on it). All work happens there. **Never merge into `main` or push `main` without the user's explicit request in this session** - a push to `main` creates a GitHub Release and, after this plan, a Snap Store `stable` release.
- Pushing `claude/snap-store` to `origin` is part of Task 2 (it runs the temporary verification workflow). That workflow never publishes.
- **Never trigger `.github/workflows/dotnet-desktop.yml` via `workflow_dispatch`** from this branch - `publish` creates a real GitHub Release and `snap` would release to Snap Store `stable`.
- Snap: name `yt-downloader-bp`, title `YouTube Downloader`, `base: core24`, `confinement: strict`, `grade: stable`, platforms `amd64` + `arm64`, channel `stable`, plugs `home`, `network` + `extensions: [gnome]`.
- Snap version = `<Version>` from `YouTubeDownloader.csproj` + `-` + `github.run_number` (e.g. `2.0.300926-28`).
- Outside a snap the downloads folder stays exactly `Path.Combine(AppPaths.AppDirectory, "downloads")` (backward compatibility).
- Inside a snap: `<XDG download dir>/YouTube Downloader`; fallback for the XDG part: `<realHome>/Downloads`. `realHome` = `$SNAP_REAL_HOME`, or `Environment.GetFolderPath(SpecialFolder.UserProfile)` when that is empty.
- `AppPaths.cs` must not reference any `Avalonia.*` namespace (it is compiled into the scratch harness).
- **No test project in the repo.** Tests live in a scratch harness at `C:\Users\pawel\AppData\Local\Temp\claude\E--claude-YouTube-Downloader\7e9393e1-05ff-4a98-b60a-fe736a8d29f2\scratchpad\ytd-tests` (`$HARNESS`). Never commit it.
- No new user-facing texts (`Strings.cs` untouched).
- The secret `SNAPCRAFT_STORE_CREDENTIALS` (already set by the owner) may appear in exactly two places in `dotnet-desktop.yml`: the job-level `HAS_SNAP_CREDENTIALS` comparison and the publish step's `env`. It must never be in scope for the smoke test (yt-dlp and Deno downloaded from the internet run there and inherit the environment).
- `dotnet build -c Release` must stay at 0 warnings, 0 errors.
- Commit messages in English, ending with:
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01GRiXPGnEGU9o7747v7x5E1
  ```

## Review Focus

1. **Localized Downloads folder** (Polish `~/Pobrane`, Russian `~/Загрузки`, French `~/Téléchargements`) - files must land in that folder, not in a new English `~/Downloads`. Pinned by Task 1's XDG tests (incl. non-ASCII and a full real-world `user-dirs.dirs`).
2. **No usable `user-dirs.dirs`** (file missing on a minimal desktop, key missing, `XDG_DOWNLOAD_DIR="$HOME"` meaning "disabled", relative garbage) - fall back to `~/Downloads/YouTube Downloader`, never throw. Pinned by Task 1's fallback tests and the snap-without-file test.
3. **Snap started without `SNAP_REAL_HOME`** (old snapd) - still a writable path (the snap's own home), never an empty/relative path. Pinned by Task 1's `null`/`""` real-home tests.
4. **Store credentials leaking to downloaded binaries** during the smoke test - the secret appears only in the publish step. Pinned by Task 3's grep check on the workflow.
5. **Slow first start of a snap** (content snap wiring, font cache) - the smoke test waits up to 120 s, polling every 5 s, instead of a fixed 60 s. Pinned by the shared smoke-test step in Task 2 (and reused verbatim in Task 3).

---

## Task 1: Downloads folder that works inside a snap

**Files:**
- Modify: `AppPaths.cs` (new members after `DataDirectory` / `ResolveDataDirectory`, new private helper at the end)
- Modify: `MainWindow.axaml.cs:956` and `MainWindow.axaml.cs:994` (the two `downloadsDir` lines)
- Modify: `CLAUDE.md` (lines 53, 386, 417-419)
- Create (scratch, never committed): `$HARNESS/ytd-tests.csproj`, `$HARNESS/Program.cs`, `$HARNESS/DownloadsTests.cs`

**Interfaces:**
- Produces (all `internal static` on `AppPaths`, namespace `YouTubeDownloader`):
  - `const string SnapDownloadsFolderName = "YouTube Downloader"`
  - `string DownloadsDirectory { get; }` - lazy, computed once
  - `string ResolveDownloadsDirectory(bool isSnap, string appDirectory, string? snapRealHome, string userProfile)` - reads `<realHome>/.config/user-dirs.dirs` from disk when `isSnap`
  - `string ResolveUserDownloadDirectory(string? userDirsContent, string realHome)` - pure
- Consumes: existing `AppPaths.IsSnap`, `AppPaths.AppDirectory`.

- [ ] **Step 1: Create the scratch harness**

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
    <Compile Include="E:\claude\YouTube-Downloader\AppPaths.cs" />
  </ItemGroup>
</Project>
```

`$HARNESS/Program.cs`:
```csharp
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

DownloadsTests.Run(Check, NewTempDir);

Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
return failures == 0 ? 0 : 1;
```

- [ ] **Step 2: Write the failing tests**

`$HARNESS/DownloadsTests.cs` (paths are built with `Path.Combine` on both sides, so the tests pass on Windows as well as Linux):
```csharp
using System.Text;
using YouTubeDownloader;

static class DownloadsTests
{
    public static void Run(Action<bool, string> check, Func<string> newTempDir)
    {
        const string home = "/home/u";
        string fallback = Path.Combine(home, "Downloads");
        string Resolve(string? content) => AppPaths.ResolveUserDownloadDirectory(content, home);

        // ResolveUserDownloadDirectory - the XDG value
        check(Resolve("XDG_DOWNLOAD_DIR=\"$HOME/Pobrane\"\n") == Path.Combine(home, "Pobrane"), "xdg: $HOME/Pobrane");
        check(Resolve("XDG_DOWNLOAD_DIR=\"$HOME/Downloads\"") == Path.Combine(home, "Downloads"), "xdg: $HOME/Downloads");
        check(Resolve("XDG_DOWNLOAD_DIR=\"$HOME/Moje pobrane\"") == Path.Combine(home, "Moje pobrane"), "xdg: folder with a space");
        check(Resolve("XDG_DOWNLOAD_DIR=\"$HOME/Загрузки\"") == Path.Combine(home, "Загрузки"), "xdg: Cyrillic folder");
        check(Resolve("XDG_DOWNLOAD_DIR=\"$HOME/Téléchargements\"") == Path.Combine(home, "Téléchargements"), "xdg: accented folder");
        check(Resolve("XDG_DOWNLOAD_DIR=\"$HOME/Pobrane/\"") == Path.Combine(home, "Pobrane"), "xdg: trailing slash dropped");
        check(Resolve("XDG_DOWNLOAD_DIR=\"/data/Pobrane\"") == "/data/Pobrane", "xdg: absolute path kept");
        check(Resolve("XDG_DOWNLOAD_DIR=$HOME/Pobrane") == Path.Combine(home, "Pobrane"), "xdg: unquoted value");
        check(Resolve("XDG_DOWNLOAD_DIR=\"$HOME/Pobrane\"\r\n") == Path.Combine(home, "Pobrane"), "xdg: CRLF line ending");
        check(Resolve("XDG_DOWNLOAD_DIR=\"$HOME/Stare\"\nXDG_DOWNLOAD_DIR=\"$HOME/Nowe\"\n") == Path.Combine(home, "Nowe"),
            "xdg: last assignment wins");

        // A real Polish Ubuntu file (header comments + every key)
        string polishUbuntu = """
            # This file is written by xdg-user-dirs-update
            # If you want to change or add directories, just edit the line you're
            # interested in. All local changes will be retained on the next run.
            # Format is XDG_xxx_DIR="$HOME/yyy", where yyy is a shell-escaped
            # homedir-relative path, or XDG_xxx_DIR="/yyy", where /yyy is an
            # absolute path. No other format is supported.
            #
            XDG_DESKTOP_DIR="$HOME/Pulpit"
            XDG_DOWNLOAD_DIR="$HOME/Pobrane"
            XDG_TEMPLATES_DIR="$HOME/Szablony"
            XDG_PUBLICSHARE_DIR="$HOME/Publiczny"
            XDG_DOCUMENTS_DIR="$HOME/Dokumenty"
            XDG_MUSIC_DIR="$HOME/Muzyka"
            XDG_PICTURES_DIR="$HOME/Obrazy"
            XDG_VIDEOS_DIR="$HOME/Wideo"
            """;
        check(Resolve(polishUbuntu) == Path.Combine(home, "Pobrane"), "xdg: real Polish Ubuntu file");

        // ResolveUserDownloadDirectory - fallbacks
        check(Resolve(null) == fallback, "xdg fallback: no file");
        check(Resolve("") == fallback, "xdg fallback: empty file");
        check(Resolve("XDG_DESKTOP_DIR=\"$HOME/Pulpit\"\n") == fallback, "xdg fallback: no download key");
        check(Resolve("#XDG_DOWNLOAD_DIR=\"$HOME/Stare\"\n") == fallback, "xdg fallback: commented-out key");
        check(Resolve("XDG_DOWNLOAD_DIR=\"$HOME\"") == fallback, "xdg fallback: $HOME (disabled)");
        check(Resolve("XDG_DOWNLOAD_DIR=\"$HOME/\"") == fallback, "xdg fallback: $HOME/ (disabled)");
        check(Resolve("XDG_DOWNLOAD_DIR=\"\"") == fallback, "xdg fallback: empty value");
        check(Resolve("XDG_DOWNLOAD_DIR=\"Pobrane\"") == fallback, "xdg fallback: relative value");

        // ResolveDownloadsDirectory - outside a snap nothing changes
        check(AppPaths.ResolveDownloadsDirectory(false, "/app", "/home/u", "/profile") == Path.Combine("/app", "downloads"),
            "downloads: outside a snap next to the app");

        // Inside a snap: XDG folder from the real home + "YouTube Downloader"
        {
            string realHome = newTempDir();
            Directory.CreateDirectory(Path.Combine(realHome, ".config"));
            File.WriteAllText(Path.Combine(realHome, ".config", "user-dirs.dirs"),
                "XDG_DOWNLOAD_DIR=\"$HOME/Pobrane\"\n", new UTF8Encoding(false));
            check(AppPaths.ResolveDownloadsDirectory(true, "/snap/app/bin", realHome, "/profile")
                    == Path.Combine(realHome, "Pobrane", "YouTube Downloader"),
                "downloads: snap -> XDG folder + YouTube Downloader");
        }
        {
            string realHome = newTempDir();
            check(AppPaths.ResolveDownloadsDirectory(true, "/snap/app/bin", realHome, "/profile")
                    == Path.Combine(realHome, "Downloads", "YouTube Downloader"),
                "downloads: snap without user-dirs.dirs -> Downloads");
        }
        {
            string profile = newTempDir();
            string expected = Path.Combine(profile, "Downloads", "YouTube Downloader");
            check(AppPaths.ResolveDownloadsDirectory(true, "/snap/app/bin", null, profile) == expected,
                "downloads: snap without SNAP_REAL_HOME -> profile folder");
            check(AppPaths.ResolveDownloadsDirectory(true, "/snap/app/bin", "", profile) == expected,
                "downloads: snap with empty SNAP_REAL_HOME -> profile folder");
        }

        // The property itself: the harness runs outside a snap (SNAP unset)
        check(AppPaths.DownloadsDirectory == Path.Combine(AppPaths.AppDirectory, "downloads"),
            "downloads: DownloadsDirectory outside a snap");
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet run --project "$HARNESS"`
Expected: build error `CS0117: 'AppPaths' does not contain a definition for 'ResolveUserDownloadDirectory'` (and the same for `ResolveDownloadsDirectory`, `DownloadsDirectory`).

- [ ] **Step 4: Implement in `AppPaths.cs`**

After the line `private static readonly Lazy<string> dataDirectory = new(CreateDataDirectory);` add:
```csharp
    private static readonly Lazy<string> downloadsDirectory = new(() => ResolveDownloadsDirectory(
        IsSnap,
        AppContext.BaseDirectory,
        Environment.GetEnvironmentVariable("SNAP_REAL_HOME"),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
```

After `public static string DataDirectory => dataDirectory.Value;` add:
```csharp
    // Next to the app, except in a snap: $SNAP is read-only there.
    public static string DownloadsDirectory => downloadsDirectory.Value;
```

After the closing brace of `ResolveDataDirectory(...)` add:
```csharp
    // Inside a snap: the user's Downloads folder (the desktop interface may read
    // ~/.config/user-dirs.dirs, the home interface may write there) plus a
    // folder of our own. $HOME is ~/snap/<name>/<revision> there; snapd passes
    // the real one in SNAP_REAL_HOME.
    public const string SnapDownloadsFolderName = "YouTube Downloader";

    public static string ResolveDownloadsDirectory(bool isSnap, string appDirectory, string? snapRealHome, string userProfile)
    {
        if (!isSnap)
            return Path.Combine(appDirectory, "downloads");

        string realHome = string.IsNullOrEmpty(snapRealHome) ? userProfile : snapRealHome;
        string? userDirs = TryReadAllText(Path.Combine(realHome, ".config", "user-dirs.dirs"));
        return Path.Combine(ResolveUserDownloadDirectory(userDirs, realHome), SnapDownloadsFolderName);
    }

    // xdg-user-dirs format: XDG_DOWNLOAD_DIR="$HOME/Pobrane" or an absolute path;
    // "$HOME" alone means the folder is disabled. The last assignment wins, as in
    // the shell. Shell escapes are not handled - xdg-user-dirs does not write
    // them for ordinary folder names.
    public static string ResolveUserDownloadDirectory(string? userDirsContent, string realHome)
    {
        const string key = "XDG_DOWNLOAD_DIR=";
        const string homePrefix = "$HOME/";
        string fallback = Path.Combine(realHome, "Downloads");

        string? value = null;
        foreach (string line in (userDirsContent ?? "").Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith(key, StringComparison.Ordinal))
                value = trimmed[key.Length..];
        }

        if (value == null)
            return fallback;

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            value = value[1..^1];

        if (value.StartsWith(homePrefix, StringComparison.Ordinal))
        {
            string relative = value[homePrefix.Length..].Trim('/');
            return relative.Length == 0 ? fallback : Path.Combine(realHome, relative);
        }

        return value.StartsWith('/') ? value : fallback;
    }
```

At the end of the class (after `TryDeleteDirectory`) add:
```csharp
    private static string? TryReadAllText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet run --project "$HARNESS"`
Expected: every line `PASS ...`, last line `ALL PASSED`, exit code 0.

- [ ] **Step 6: Use it in `MainWindow.axaml.cs`**

Both lines (`BtnDownload_Click()` ~956 and `DownloadSingleUrlAsync()` ~994):
```csharp
string downloadsDir = Path.Combine(appDirectory, "downloads");
```
become:
```csharp
string downloadsDir = AppPaths.DownloadsDirectory;
```
(Use Edit with `replace_all: true` on the exact old line - it occurs exactly twice.) Then confirm nothing else builds the old path:

Run: `grep -n '"downloads"' MainWindow.axaml.cs AppPaths.cs`
Expected: only `AppPaths.cs` (`Path.Combine(appDirectory, "downloads")` inside `ResolveDownloadsDirectory`).

- [ ] **Step 7: Build**

Run: `dotnet build -c Release`
Expected: `Ostrzeżenia: 0` / `Liczba błędów: 0` (or `0 Warning(s)` / `0 Error(s)`).

- [ ] **Step 8: Update CLAUDE.md**

1. Line 53, replace
   `- **App folder** (`AppPaths.AppDirectory`, where the executable lives) holds only the app itself and `downloads/`. Self-update replaces files here and nothing else.`
   with
   `- **App folder** (`AppPaths.AppDirectory`, where the executable lives) holds only the app itself and `downloads/`. Self-update replaces files here and nothing else. Inside a snap the app folder (`$SNAP`) is read-only, so `AppPaths.DownloadsDirectory` points to the user's Downloads folder instead: the `XDG_DOWNLOAD_DIR` from `$SNAP_REAL_HOME/.config/user-dirs.dirs` (e.g. `~/Pobrane`), falling back to `~/Downloads`, plus `YouTube Downloader`.`
2. Line 386, replace
   `- `-o "downloads/%(title)s.%(ext)s"` - Output pattern`
   with
   `- `-o "<AppPaths.DownloadsDirectory>/%(title)s.%(ext)s"` - Output pattern`
3. Lines 417-419, replace
   ```
   1. **Change Download Location**:
      - The `downloadsDir` computation (`Path.Combine(appDirectory, "downloads")`) appears in both `BtnDownload_Click()` and `DownloadSingleUrlAsync()` in `MainWindow.axaml.cs` - update both
      - Update the user-facing success message that reports the location
   ```
   with
   ```
   1. **Change Download Location**:
      - `AppPaths.DownloadsDirectory` is the only place that decides it (`ResolveDownloadsDirectory()` - next to the app, or the XDG Downloads folder inside a snap); `BtnDownload_Click()` and `DownloadSingleUrlAsync()` both read it
      - The success message reports whatever path it returns
   ```

- [ ] **Step 9: Commit**

```bash
git add AppPaths.cs MainWindow.axaml.cs CLAUDE.md
git commit -F - <<'EOF'
Downloads folder: the user's Downloads folder inside a snap

$SNAP is read-only, so the app could not create downloads/ next to its
exe there. AppPaths.DownloadsDirectory keeps <app>/downloads outside a
snap and, inside one, uses XDG_DOWNLOAD_DIR from the real home's
user-dirs.dirs (~/Pobrane on Polish Ubuntu), falling back to ~/Downloads,
plus "YouTube Downloader". Both download code paths read it.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GRiXPGnEGU9o7747v7x5E1
EOF
```

---

## Task 2: Snap packaging, verified on the branch

**Files:**
- Create: `snap/snapcraft.yaml`
- Create: `snap/gui/yt-downloader-bp.desktop`
- Create (temporary, deleted in Task 3): `.github/workflows/snap-verify.yml`
- Modify: `.gitignore` (append two lines)

**Interfaces:**
- Consumes: the release assets `YouTubeDownloader-linux-x64` / `YouTubeDownloader-linux-arm64` of the latest GitHub Release (a single-file ELF named after the asset); `YouTubeDownloader.csproj` `<Version>`.
- Produces: `snap/snapcraft.yaml` expecting `snap-bin/YouTubeDownloader` and `snap-bin/version.txt` at build time; the three shared workflow steps **"Prepare snap-bin (binary + version)"**, **"Build snap"** (`id: build`, output `steps.build.outputs.snap`) and **"Smoke-test installed snap"**, which Task 3 copies verbatim into `dotnet-desktop.yml`.

- [ ] **Step 1: Write `snap/snapcraft.yaml`**

```yaml
name: yt-downloader-bp
title: YouTube Downloader
summary: Download YouTube videos and audio - a simple desktop app for yt-dlp
description: |
  Download videos (MP4, WebM, MKV, up to 4K) or audio only (MP3) from
  YouTube, several links in one go. The app fetches and updates its own
  tools (yt-dlp, FFmpeg, Deno) - nothing else to install. Polish and
  English interface. Files are saved to your Downloads folder, in
  "YouTube Downloader".

  Pobieranie filmów (MP4, WebM, MKV, do 4K) lub samego dźwięku (MP3) z
  YouTube, także wielu linków naraz. Aplikacja sama pobiera i aktualizuje
  swoje narzędzia (yt-dlp, FFmpeg, Deno). Interfejs po polsku i angielsku.
  Pliki trafiają do folderu Pobrane, do „YouTube Downloader”.
license: Apache-2.0
icon: logo.svg
website: https://github.com/bauerpawel/YouTube-Downloader
source-code: https://github.com/bauerpawel/YouTube-Downloader
issues: https://github.com/bauerpawel/YouTube-Downloader/issues
base: core24
grade: stable
confinement: strict
adopt-info: app

platforms:
  amd64:
  arm64:

apps:
  yt-downloader-bp:
    command: bin/YouTubeDownloader
    extensions: [gnome]
    plugs:
      - home
      - network

parts:
  app:
    # The single-file binary CI already built and smoke-tested (publish job),
    # plus version.txt - CI creates snap-bin/, it is not in the repo.
    plugin: dump
    source: snap-bin/
    organize:
      YouTubeDownloader: bin/YouTubeDownloader
    prime:
      - -version.txt
    # ICU: .NET globalization. OpenSSL: HTTPS in .NET on Linux. ICE/SM: X11
    # session libraries Avalonia loads (the Linux smoke test installs them too).
    stage-packages:
      - libicu74
      - libssl3t64
      - libice6
      - libsm6
    override-build: |
      craftctl default
      craftctl set version="$(cat "$CRAFT_PART_SRC/version.txt")"
```

- [ ] **Step 2: Write `snap/gui/yt-downloader-bp.desktop`**

```ini
[Desktop Entry]
Type=Application
Name=YouTube Downloader
Comment=Download YouTube videos and audio
Comment[pl]=Pobieranie filmów i audio z YouTube
Exec=yt-downloader-bp
Icon=${SNAP}/meta/gui/icon.svg
Terminal=false
Categories=AudioVideo;Network;
```

- [ ] **Step 3: Ignore CI build inputs/outputs**

Append to `.gitignore`:
```
snap-bin/
*.snap
```

- [ ] **Step 4: Write the temporary verification workflow**

`.github/workflows/snap-verify.yml` - the three steps from "Prepare snap-bin" to the end of "Smoke-test installed snap" are the shared block; keep them byte-identical to what Task 3 puts into `dotnet-desktop.yml` (same 4-space `- name:` indentation as that file):
```yaml
# TEMPORARY - delete before merging (see Task 3 of
# docs/superpowers/plans/2026-09-30-snap-store.md). Verifies snap/snapcraft.yaml
# and the snap smoke test on the feature branch: the real `snap` job in
# dotnet-desktop.yml needs the publish job, which only runs on main. Uses the
# single-file binaries of the latest release and never publishes.
name: Snap verify (temporary)

on:
  push:
    branches: [ "claude/snap-store" ]

permissions:
  contents: read

jobs:
  snap:
    strategy:
      fail-fast: false
      matrix:
        include:
          - arch: amd64
            runner: ubuntu-latest
            rid: linux-x64
          - arch: arm64
            runner: ubuntu-24.04-arm
            rid: linux-arm64

    runs-on: ${{ matrix.runner }}

    defaults:
      run:
        shell: bash

    steps:
    - name: Checkout
      uses: actions/checkout@v7

    - name: Download the latest released single-file binary
      run: |
        mkdir -p snap-bin
        curl -fsSL -o snap-bin/YouTubeDownloader \
          "https://github.com/${{ github.repository }}/releases/latest/download/YouTubeDownloader-${{ matrix.rid }}"

    - name: Prepare snap-bin (binary + version)
      run: |
        chmod +x snap-bin/YouTubeDownloader
        version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' YouTubeDownloader.csproj)
        echo "${version}-${{ github.run_number }}" > snap-bin/version.txt
        echo "Snap version: $(cat snap-bin/version.txt)"

    - name: Build snap
      id: build
      uses: snapcore/action-build@v1

    # Installs the snap the way a user gets it (strict confinement,
    # auto-connected interfaces) and checks that the app starts and can
    # download AND run its tools from $SNAP_USER_COMMON. Xvfb runs with -ac:
    # xvfb-run keeps its Xauthority file in /tmp, which a snap cannot see
    # (private /tmp). The first start of a snap builds the font cache, hence
    # up to 120 s.
    - name: Smoke-test installed snap
      env:
        YTD_GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      run: |
        sudo snap install gnome-46-2404
        sudo snap install --dangerous "${{ steps.build.outputs.snap }}"
        snap connections yt-downloader-bp
        for plug in home network; do
          if ! snap connections yt-downloader-bp | awk -v p="yt-downloader-bp:$plug" '$2 == p && $3 != "-" { ok = 1 } END { exit !ok }'; then
            echo "Interface '$plug' is not connected"
            exit 1
          fi
        done
        sudo apt-get update
        sudo apt-get install -y xvfb
        Xvfb :99 -screen 0 1280x1024x24 -ac > /dev/null 2>&1 &
        export DISPLAY=:99
        LOG="$RUNNER_TEMP/snap-app.log"
        COMMON="$HOME/snap/yt-downloader-bp/common"
        snap run yt-downloader-bp > "$LOG" 2>&1 &
        APP_PID=$!
        ok=0
        for i in $(seq 1 24); do
          sleep 5
          if ! kill -0 "$APP_PID" 2>/dev/null; then
            echo "The snap app exited or crashed"
            break
          fi
          if [ -x "$COMMON/yt-dlp" ] && [ -x "$COMMON/ffmpeg_bin/ffmpeg" ]; then
            ok=1
            break
          fi
        done
        if [ "$ok" = 1 ]; then
          echo "yt-dlp and ffmpeg were downloaded to $COMMON and are executable under strict confinement"
        else
          echo "Tools not in $COMMON after 120 s (or the app stopped)"
          ls -la "$COMMON" "$COMMON/ffmpeg_bin" 2>&1 || true
          cat "$LOG"
        fi
        pkill -f yt-downloader-bp || true
        sudo snap remove --purge yt-downloader-bp
        [ "$ok" = 1 ]
```

- [ ] **Step 5: Lint the workflow locally**

Download actionlint once into the harness (scratch, never committed):
```bash
mkdir -p "$HARNESS/tools" && cd "$HARNESS/tools"
curl -fsSLO https://github.com/rhysd/actionlint/releases/download/v1.7.12/actionlint_1.7.12_windows_amd64.zip
unzip -o actionlint_1.7.12_windows_amd64.zip actionlint.exe
```
Run (from the repo root): `"$HARNESS/tools/actionlint.exe" -shellcheck= .github/workflows/snap-verify.yml`
Expected: no output, exit code 0. (`-shellcheck=` disables the optional shellcheck integration, which is not installed.)

- [ ] **Step 6: Commit and push the branch**

```bash
git add snap/snapcraft.yaml snap/gui/yt-downloader-bp.desktop .gitignore .github/workflows/snap-verify.yml
git commit -F - <<'EOF'
Add snap packaging (yt-downloader-bp) and a temporary verification workflow

snap/snapcraft.yaml packs the published single-file binary (dump plugin,
core24, strict confinement, gnome extension, home + network). The
temporary snap-verify workflow builds it on amd64 and arm64 from the
latest release's binaries and smoke-tests the installed snap; it never
publishes and goes away before merging.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GRiXPGnEGU9o7747v7x5E1
EOF
git push -u origin claude/snap-store
```

- [ ] **Step 7: Watch the verification run**

Run: `gh run list --branch claude/snap-store --workflow snap-verify.yml --limit 1` to get the run id, then (in the background) `gh run watch <id> --exit-status --interval 30`, then
`gh run view <id> --json conclusion,jobs --jq '.conclusion, (.jobs[] | "\(.conclusion)\t\(.name)")'`
Expected: `success` for both `snap (amd64, ...)` and `snap (arm64, ...)`; the smoke-test log contains `were downloaded to /home/runner/snap/yt-downloader-bp/common and are executable under strict confinement`.

If a leg fails, read the failing step with `gh run view <id> --log-failed`, fix `snap/snapcraft.yaml` or the shared steps, commit (`Fix snap packaging: <what>` + trailers) and push again. Known fallback: if `snapcore/action-build` cannot run LXD on `ubuntu-24.04-arm`, replace the **Build snap** step (in `snap-verify.yml` now, and later in Task 3 identically) with a destructive-mode build on the runner itself (the runner is Ubuntu 24.04 = the `core24` base):
```yaml
    - name: Build snap
      id: build
      run: |
        sudo snap install snapcraft --classic
        sudo snapcraft pack --destructive-mode
        echo "snap=$(ls ./*.snap)" >> "$GITHUB_OUTPUT"
```
and add `parts/`, `stage/`, `prime/` to `.gitignore` in the same commit.

Stop after two failed fix attempts on the same leg and report to the user with the failing log excerpt instead of guessing further.

---

## Task 3: Snap job in the release workflow

**Files:**
- Modify: `.github/workflows/dotnet-desktop.yml` (append the `snap` job at the end of `jobs:`)
- Delete: `.github/workflows/snap-verify.yml`
- Modify: `CLAUDE.md` (repository tree, CI paragraph, Testing Changes, Testing Checklist)
- Modify: `README.md` (install section, usage step 7, project tree, What's New - PL and EN)

**Interfaces:**
- Consumes: the shared steps from Task 2 (verified green); the `publish` job's artifacts `YouTubeDownloader-linux-x64` / `YouTubeDownloader-linux-arm64` (each contains one file `YouTubeDownloader`); secret `SNAPCRAFT_STORE_CREDENTIALS`.
- Produces: job `snap` (matrix `amd64`/`arm64`) releasing to Snap Store `stable`.

- [ ] **Step 0: Baseline lint of the unchanged workflow**

Run (actionlint was downloaded in Task 2 Step 5): `"$HARNESS/tools/actionlint.exe" -shellcheck= .github/workflows/dotnet-desktop.yml > "$HARNESS/actionlint-baseline.txt"; cat "$HARNESS/actionlint-baseline.txt"`
Record whatever it prints - these are pre-existing findings, not part of this plan. They are not fixed here.

- [ ] **Step 1: Append the `snap` job to `dotnet-desktop.yml`**

At the end of the file (after the `publish` job's last step), add - the three shared steps are copied **verbatim** from the green `snap-verify.yml`:
```yaml

  # Snap Store package yt-downloader-bp (docs/superpowers/specs/2026-09-30-snap-store-design.md).
  # Packs the linux single-file binaries the publish job just built and
  # smoke-tested, tests the installed snap, then releases it to stable. Runs
  # after publish, so a failing snap never holds back the GitHub Release.
  snap:
    needs: publish
    if: github.event_name == 'push' || github.event_name == 'workflow_dispatch'  # not on forked PRs

    strategy:
      fail-fast: false
      matrix:
        include:
          - arch: amd64
            runner: ubuntu-latest
            rid: linux-x64
          - arch: arm64
            runner: ubuntu-24.04-arm
            rid: linux-arm64

    runs-on: ${{ matrix.runner }}

    # Only whether the secret exists. The secret itself reaches the publish step
    # alone: the smoke test runs yt-dlp and Deno downloaded from the internet,
    # and they inherit the environment.
    env:
      HAS_SNAP_CREDENTIALS: ${{ secrets.SNAPCRAFT_STORE_CREDENTIALS != '' }}

    defaults:
      run:
        shell: bash

    steps:
    - name: Checkout
      uses: actions/checkout@v7

    - name: Download the published single-file binary
      uses: actions/download-artifact@v8
      with:
        name: YouTubeDownloader-${{ matrix.rid }}
        path: snap-bin

    - name: Prepare snap-bin (binary + version)
      run: |
        chmod +x snap-bin/YouTubeDownloader
        version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' YouTubeDownloader.csproj)
        echo "${version}-${{ github.run_number }}" > snap-bin/version.txt
        echo "Snap version: $(cat snap-bin/version.txt)"

    - name: Build snap
      id: build
      uses: snapcore/action-build@v1

    # Installs the snap the way a user gets it (strict confinement,
    # auto-connected interfaces) and checks that the app starts and can
    # download AND run its tools from $SNAP_USER_COMMON. Xvfb runs with -ac:
    # xvfb-run keeps its Xauthority file in /tmp, which a snap cannot see
    # (private /tmp). The first start of a snap builds the font cache, hence
    # up to 120 s.
    - name: Smoke-test installed snap
      env:
        YTD_GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      run: |
        sudo snap install gnome-46-2404
        sudo snap install --dangerous "${{ steps.build.outputs.snap }}"
        snap connections yt-downloader-bp
        for plug in home network; do
          if ! snap connections yt-downloader-bp | awk -v p="yt-downloader-bp:$plug" '$2 == p && $3 != "-" { ok = 1 } END { exit !ok }'; then
            echo "Interface '$plug' is not connected"
            exit 1
          fi
        done
        sudo apt-get update
        sudo apt-get install -y xvfb
        Xvfb :99 -screen 0 1280x1024x24 -ac > /dev/null 2>&1 &
        export DISPLAY=:99
        LOG="$RUNNER_TEMP/snap-app.log"
        COMMON="$HOME/snap/yt-downloader-bp/common"
        snap run yt-downloader-bp > "$LOG" 2>&1 &
        APP_PID=$!
        ok=0
        for i in $(seq 1 24); do
          sleep 5
          if ! kill -0 "$APP_PID" 2>/dev/null; then
            echo "The snap app exited or crashed"
            break
          fi
          if [ -x "$COMMON/yt-dlp" ] && [ -x "$COMMON/ffmpeg_bin/ffmpeg" ]; then
            ok=1
            break
          fi
        done
        if [ "$ok" = 1 ]; then
          echo "yt-dlp and ffmpeg were downloaded to $COMMON and are executable under strict confinement"
        else
          echo "Tools not in $COMMON after 120 s (or the app stopped)"
          ls -la "$COMMON" "$COMMON/ffmpeg_bin" 2>&1 || true
          cat "$LOG"
        fi
        pkill -f yt-downloader-bp || true
        sudo snap remove --purge yt-downloader-bp
        [ "$ok" = 1 ]

    - name: Publish to Snap Store (stable)
      if: env.HAS_SNAP_CREDENTIALS == 'true'
      uses: snapcore/action-publish@v1
      env:
        SNAPCRAFT_STORE_CREDENTIALS: ${{ secrets.SNAPCRAFT_STORE_CREDENTIALS }}
      with:
        snap: ${{ steps.build.outputs.snap }}
        release: stable

    - name: Warn that the snap was not published
      if: env.HAS_SNAP_CREDENTIALS != 'true'
      run: echo "::warning::SNAPCRAFT_STORE_CREDENTIALS is not set - the snap was built and tested but not published"
```
The three steps from "Prepare snap-bin" to `[ "$ok" = 1 ]` must be byte-identical to `snap-verify.yml` - if Task 2 had to change them (e.g. the destructive-mode fallback), copy the **green** version from `snap-verify.yml`, not the text above. Step 2 checks this.

- [ ] **Step 2: Verify the shared steps are identical**

Run (bash, repo root):
```bash
block() { awk '/- name: Prepare snap-bin/,/\[ "\$ok" = 1 \]/' "$1"; }
diff <(block .github/workflows/snap-verify.yml) <(block .github/workflows/dotnet-desktop.yml) && echo IDENTICAL
```
Expected: `IDENTICAL`.

- [ ] **Step 3: Verify the secret stays out of the smoke test**

Run:
```bash
grep -n 'secrets.SNAPCRAFT_STORE_CREDENTIALS' .github/workflows/dotnet-desktop.yml
awk '/- name: Publish to Snap Store/,/release: stable/' .github/workflows/dotnet-desktop.yml | grep -c 'secrets.SNAPCRAFT_STORE_CREDENTIALS'
```
Expected: the first command prints exactly two lines - `HAS_SNAP_CREDENTIALS: ${{ secrets.SNAPCRAFT_STORE_CREDENTIALS != '' }}` and `SNAPCRAFT_STORE_CREDENTIALS: ${{ secrets.SNAPCRAFT_STORE_CREDENTIALS }}`; the second prints `1`.

- [ ] **Step 4: Delete the temporary workflow and lint**

```bash
git rm .github/workflows/snap-verify.yml
"$HARNESS/tools/actionlint.exe" -shellcheck= .github/workflows/dotnet-desktop.yml > "$HARNESS/actionlint-after.txt"
diff "$HARNESS/actionlint-baseline.txt" "$HARNESS/actionlint-after.txt" && echo "NO NEW FINDINGS"
```
Expected: `NO NEW FINDINGS` (line numbers of pre-existing findings do not move - the job is appended at the end of the file). Any new line points into the `snap` job: fix it. (Run Step 2's diff **before** this step - it needs `snap-verify.yml`.)

- [ ] **Step 5: CLAUDE.md**

1. Repository tree: after the line
   `├── build.sh                     # Wraps `dotnet publish` for linux-x64/linux-arm64/osx-x64/osx-arm64`
   insert
   ```
   ├── snap/
   │   ├── snapcraft.yaml           # Snap Store package yt-downloader-bp (core24, strict, gnome extension)
   │   └── gui/
   │       └── yt-downloader-bp.desktop  # Menu entry inside the snap
   ```
2. After the long CI paragraph that starts with `CI (`.github/workflows/dotnet-desktop.yml`) builds and publishes all six RIDs` (ends with `(see Dependency Management above for the full OS split).`), add a new paragraph:
   ```
   **Snap Store (`snap` job).** After `publish`, a two-leg job (`amd64` on `ubuntu-latest`, `arm64` on `ubuntu-24.04-arm`) packs the `YouTubeDownloader-linux-x64`/`-linux-arm64` artifacts with `snap/snapcraft.yaml` (`dump` plugin over the single-file binary, `core24`, strict confinement, `gnome` extension, plugs `home` + `network`; version `<Version>-<run_number>` via `snap-bin/version.txt`), installs the result with `snap install --dangerous`, checks that `home` and `network` are connected, runs it under `Xvfb :99 -ac` (a snap cannot read `xvfb-run`'s Xauthority in `/tmp`) and waits up to 120 s for `yt-dlp` and `ffmpeg_bin/ffmpeg` in `~/snap/yt-downloader-bp/common`, then releases it to `stable` with `snapcore/action-publish`. The Store login is the repository secret `SNAPCRAFT_STORE_CREDENTIALS`; only the publish step sees it - the job env carries just `HAS_SNAP_CREDENTIALS`, because the smoke test runs yt-dlp and Deno downloaded from the internet. Without the secret the snap is still built and tested and a warning replaces the publish. The credentials expire (by default after a year): renew them on any Linux with `snapcraft export-login --snaps=yt-downloader-bp --acls=package_access,package_push,package_update,package_release creds.txt` and replace the secret. `workflow_dispatch` from a feature branch would also push a snap to `stable`. Inside the snap: data in `$SNAP_USER_COMMON`, self-update off (`AppPaths.IsSnap`), downloads in `AppPaths.DownloadsDirectory`.
   ```
3. In the "Testing Changes" paragraph, after the sentence `CI runs have confirmed it: the app launches and downloads its tools there.` append: ` The snap gets the same launch-and-tool-download smoke test, run against the installed snap under strict confinement (see the `snap` job above).`
4. Testing Checklist: after the line `- [ ] A download works with the English UI ("Best" and a fixed quality)` add
   ```
   - [ ] Snap (`sudo snap install yt-downloader-bp` on Ubuntu): the app starts, a download lands in `~/Pobrane/YouTube Downloader` (or `~/Downloads/...` on an English system), "Sprawdź aktualizacje aplikacji" says the Snap Store updates it, the About links open
   ```

- [ ] **Step 6: README.md (PL and EN)**

Version rule first: if today's `ddMMyy` differs from the suffix of `<Version>` in `YouTubeDownloader.csproj` (currently `2.0.300926`), set `<Version>` to `2.0.<ddMMyy>`, update the badge `![Version](https://img.shields.io/badge/Version-2.0.<ddMMyy>-brightgreen)`, and put the What's New bullets below under a new heading `### 🆕 Co nowego w wersji 2.0.<ddMMyy>` / `### 🆕 What's New in 2.0.<ddMMyy>` placed above the current one. Otherwise append them to the existing `2.0.300926` sections.

1. What's New bullet (PL): `- 🐧 **Snap Store** - aplikacja jest dostępna jako snap: `sudo snap install yt-downloader-bp` (amd64 i arm64), aktualizowana automatycznie przez Snap Store. Pobrane pliki trafiają do folderu Pobrane, do „YouTube Downloader”`
   (EN): `- 🐧 **Snap Store** - the app is available as a snap: `sudo snap install yt-downloader-bp` (amd64 and arm64), updated automatically by the Snap Store. Downloads go to your Downloads folder, in "YouTube Downloader"`
2. New section directly above `### 🚀 Użytkowanie`:
   ````
   ### 🐧 Instalacja przez Snap (Linux)

   [![yt-downloader-bp](https://snapcraft.io/yt-downloader-bp/badge.svg)](https://snapcraft.io/yt-downloader-bp)

   ```bash
   sudo snap install yt-downloader-bp
   ```

   Wersję snap aktualizuje Snap Store (automatyczna aktualizacja w samej aplikacji jest w niej wyłączona). Pobrane pliki trafiają do systemowego folderu pobranych, do podfolderu `YouTube Downloader` (np. `~/Pobrane/YouTube Downloader`).

   ````
   and directly above `### 🚀 Usage`:
   ````
   ### 🐧 Install via Snap (Linux)

   [![yt-downloader-bp](https://snapcraft.io/yt-downloader-bp/badge.svg)](https://snapcraft.io/yt-downloader-bp)

   ```bash
   sudo snap install yt-downloader-bp
   ```

   The snap is updated by the Snap Store (the in-app self-update is off there). Downloads go to your system Downloads folder, into a `YouTube Downloader` subfolder (e.g. `~/Downloads/YouTube Downloader`).

   ````
3. Usage step 7 (PL): replace
   `7. **Pliki w folderze downloads** - Pobrane pliki znajdziesz w folderze `downloads` w katalogu aplikacji`
   with
   `7. **Pliki w folderze downloads** - Pobrane pliki znajdziesz w folderze `downloads` w katalogu aplikacji (w wersji snap: `YouTube Downloader` w folderze Pobrane)`
   (EN): replace
   `7. **Files in the downloads folder** - Downloaded files will be in the `downloads` folder in the application directory`
   with
   `7. **Files in the downloads folder** - Downloaded files will be in the `downloads` folder in the application directory (snap: `YouTube Downloader` in your Downloads folder)`
4. Both project trees: after the `build.sh` line (PL: `├── build.sh                     # Owija `dotnet publish` dla linux-x64/linux-arm64/osx-x64/osx-arm64`; EN: `├── build.sh                     # Wraps `dotnet publish` for linux-x64/linux-arm64/osx-x64/osx-arm64`) insert
   PL:
   ```
   ├── snap/                        # Pakiet Snap Store (snapcraft.yaml, skrót w menu)
   ```
   EN:
   ```
   ├── snap/                        # Snap Store package (snapcraft.yaml, menu entry)
   ```

- [ ] **Step 7: Build and commit**

Run: `dotnet build -c Release` - expected 0 warnings, 0 errors (only relevant if `<Version>` changed, but cheap).

```bash
git add .github/workflows/dotnet-desktop.yml CLAUDE.md README.md YouTubeDownloader.csproj
git commit -F - <<'EOF'
CI: build, smoke-test and publish the snap to Snap Store stable

New `snap` job after `publish` (amd64 + arm64): packs the published
linux single-file binaries with snap/snapcraft.yaml, installs the snap,
checks home/network, runs it under Xvfb until yt-dlp and FFmpeg are in
$SNAP_USER_COMMON, then releases it to stable. Only the publish step sees
SNAPCRAFT_STORE_CREDENTIALS; without it the snap is built and tested and
a warning replaces the publish. The steps are the ones verified by the
temporary snap-verify workflow, which is removed here.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GRiXPGnEGU9o7747v7x5E1
EOF
git push
```
(`git add` of an unchanged `YouTubeDownloader.csproj` is a no-op. The push does not run any workflow: `snap-verify.yml` is gone and `dotnet-desktop.yml` runs only for `main`/PRs.)

---

## Task 4: Release (only on the user's explicit request)

**Files:** none (git + CI + Store checks).

- [ ] **Step 1: Ask the user** whether to merge `claude/snap-store` into `main` and push. Do nothing further without an explicit yes.

- [ ] **Step 2: Merge and push**

```bash
git checkout main
git fetch origin
git merge --ff-only claude/snap-store
git push origin main
```
If `--ff-only` fails (main moved), stop and ask the user.

- [ ] **Step 3: Watch CI**

Get the run id: `gh run list --branch main --limit 1 --json databaseId,headSha --jq '.[0]'` (headSha must equal `git rev-parse HEAD`). Poll in the background until `status == completed` (the publish matrix alone takes ~12 min, then the snap legs), then:
`gh run view <id> --json conclusion,jobs --jq '.conclusion, (.jobs[] | "\(.conclusion)\t\(.name)")'`
Expected: `success`; both `snap (amd64, …)` and `snap (arm64, …)` succeed.

If the publish step reports that the upload is held for manual review (a first upload / trademark name), that is not a code failure: tell the user to check https://snapcraft.io/yt-downloader-bp/releases and wait for the Store.

- [ ] **Step 4: Confirm the Store has it**

```bash
curl -s -H "Snap-Device-Series: 16" https://api.snapcraft.io/v2/snaps/info/yt-downloader-bp \
  | python -c "import json,sys; [print(c['channel']['name'], c['channel']['architecture'], c['version']) for c in json.load(sys.stdin)['channel-map']]"
```
Expected: lines `stable amd64 2.0.<ddMMyy>-<run_number>` and `stable arm64 2.0.<ddMMyy>-<run_number>`.

- [ ] **Step 5: Hand over the manual check** to the user (Ubuntu): `sudo snap install yt-downloader-bp`, start it from the app menu, download one video → it appears in `~/Pobrane/YouTube Downloader`; Tools → "Sprawdź aktualizacje aplikacji" shows "Aktualizacje tej wersji dostarcza Snap Store."; Help → About links open in the browser.

- [ ] **Step 6: Delete the merged branch** (local and remote) only if the user asks.
