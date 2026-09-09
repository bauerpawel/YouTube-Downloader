# WinForms → Avalonia UI Migration (Windows-only phase) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the WinForms UI (`MainForm.cs`) with an Avalonia UI application that has identical functionality and Polish-language UI text, while remaining Windows-only (x64 + ARM64) for this phase.

**Architecture:** Retarget the existing `YouTubeDownloader.csproj` in place (`net10.0-windows` → `net10.0`, drop `UseWindowsForms`, add Avalonia packages). Replace `MainForm.cs` with `Program.cs`, `App.axaml`/`.cs`, `MainWindow.axaml`/`.cs`, `AboutWindow.axaml`/`.cs`, and a small reusable `MessageDialog.axaml`/`.cs` (Avalonia has no built-in `MessageBox`). All business logic (dependency download/update, yt-dlp argument building, progress parsing, URL validation, multi-URL rows) is ported into `MainWindow.axaml.cs` with no behavioral changes — only API adaptations required by the new framework (`Dispatcher.UIThread` instead of `Invoke`, `IsEnabled` instead of `Enabled`, async dialogs instead of blocking `MessageBox.Show`, etc.).

**Tech Stack:** .NET 10, C# 13, Avalonia UI 12.1.2 (Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent), XAML + code-behind (no MVVM).

**Spec:** `docs/superpowers/specs/2026-09-09-avalonia-migration-design.md`

## Global Constraints

- Windows-only in this phase (win-x64 + win-arm64) — do not touch dependency-download logic (yt-dlp/FFmpeg/Deno URLs, `where` command, `.exe` paths); that is out of scope, planned for a future phase.
- No MVVM, no ReactiveUI, no Services/DI layer — logic stays in window code-behind, mirroring today's single-class structure (per approved spec, YAGNI).
- Avalonia package version: pin exactly `12.1.2` for `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent` (verified latest matching stable set on NuGet as of 2026-09-09). `Avalonia.Fonts.Inter` is deliberately **not** included — verified during plan-writing that the app builds and runs identically without it (falls back to the OS default font stack); adding it would be an unjustified extra dependency (YAGNI).
- All UI text stays in Polish, matching current strings verbatim (this is a port, not a rewrite of copy).
- No new test project — this codebase has never had one (existing CI `dotnet test` step already tolerates "no tests exist" via `continue-on-error: true`); do not introduce one as a side effect of this migration.
- **No GUI test automation is available in this environment.** Verification for every task is: (1) `dotnet build` with zero errors/warnings, (2) a smoke-test launch (start the built exe, confirm the process is still alive after ~3 seconds with no crash dialog, then close it) via the PowerShell tool. Deeper behavioral verification (does the multi-URL layout look right, does a real download work end-to-end) is a manual checklist for the user, delivered at the end of Task 6 — do not claim these are verified by the agent.
- Every code sample in this plan has already been compiled and smoke-tested against a throwaway proof-of-concept project (Avalonia 12.1.2 on net10.0) before this plan was written. Copy it as given; do not improvise alternate APIs.
- Commit messages must end with:
  ```
  Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01HUiuxppZn5rnUTdevdXScu
  ```

---

## Task 1: Project retarget + Avalonia bootstrap + MainWindow shell

**Files:**
- Modify: `YouTubeDownloader.csproj`
- Delete: `MainForm.cs`
- Create: `Program.cs`
- Create: `App.axaml`
- Create: `App.axaml.cs`
- Create: `MainWindow.axaml`
- Create: `MainWindow.axaml.cs`

**Interfaces:**
- Produces: `MainWindow` class (partial, in `YouTubeDownloader` namespace) with named fields generated from XAML: `TxtUrl` (TextBox), `BtnAddUrl` (Button), `ExtraUrlRowsPanel` (StackPanel), `CbContentType`/`CbQuality`/`CbFormat` (ComboBox), `BtnDownload` (Button), `ProgressBarDownload` (ProgressBar), `LblStatus` (TextBlock), `MiAktualizujKomponenty`/`MiInformacje` (MenuItem). Also produces private methods `GetAllUrls()`, `SetUrlRowsEnabled(bool)`, `UpdateStatus(string)`, and fields `httpClient`, `appDirectory`, `ytDlpPath`, `ffmpegBinPath`, `denoPath`, `denoVersionPath`, `nodeJsPath` — all consumed by Task 3.
- At the end of this task, `BtnDownload`, `MiAktualizujKomponenty`, `MiInformacje` are visible in the UI but **not yet wired** to any handler — that's expected and added in Tasks 3/4.

- [ ] **Step 1: Update the csproj**

Replace the full contents of `YouTubeDownloader.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <ApplicationIcon>app.ico</ApplicationIcon>
  </PropertyGroup>

  <PropertyGroup Condition="'$(Configuration)'=='Release'">
    <DebugType>none</DebugType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="12.1.2" />
    <PackageReference Include="Avalonia.Desktop" Version="12.1.2" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.1.2" />
  </ItemGroup>

  <ItemGroup>
    <AvaloniaResource Include="app.ico" />
  </ItemGroup>

</Project>
```

(The `Assets\app-logo.png` resource entry is added in Task 2, once that file exists.)

- [ ] **Step 2: Delete the old WinForms file**

```bash
git rm MainForm.cs
```

- [ ] **Step 3: Create Program.cs**

```csharp
using Avalonia;

namespace YouTubeDownloader;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
```

- [ ] **Step 4: Create App.axaml**

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="YouTubeDownloader.App"
             RequestedThemeVariant="Default">
  <Application.Styles>
    <FluentTheme />
  </Application.Styles>
</Application>
```

- [ ] **Step 5: Create App.axaml.cs**

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace YouTubeDownloader;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
```

- [ ] **Step 6: Create MainWindow.axaml**

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="YouTubeDownloader.MainWindow"
        Title="YouTube Downloader"
        Width="800" Height="500"
        MinWidth="800"
        SizeToContent="Height"
        WindowStartupLocation="CenterScreen"
        Icon="/app.ico">
  <DockPanel>
    <Menu DockPanel.Dock="Top">
      <MenuItem Header="_Narzedzia">
        <MenuItem Name="MiAktualizujKomponenty" Header="_Aktualizuj komponenty"/>
      </MenuItem>
      <MenuItem Header="_Pomoc">
        <MenuItem Name="MiInformacje" Header="_Informacje"/>
      </MenuItem>
    </Menu>

    <StackPanel Margin="20" Spacing="10">
      <TextBlock Text="Link do filmu:"/>

      <Grid ColumnDefinitions="*,Auto">
        <TextBox Name="TxtUrl" Grid.Column="0" Margin="0,0,10,0"
                  Text="https://www.youtube.com/watch?v="/>
        <Button Name="BtnAddUrl" Grid.Column="1" Content="+" Width="40"/>
      </Grid>

      <StackPanel Name="ExtraUrlRowsPanel" Spacing="5"/>

      <Grid ColumnDefinitions="150,150,150,*,100" Margin="0,10,0,0">
        <StackPanel Grid.Column="0">
          <TextBlock Text="Typ:"/>
          <ComboBox Name="CbContentType" HorizontalAlignment="Stretch"/>
        </StackPanel>
        <StackPanel Grid.Column="1" Margin="10,0,0,0">
          <TextBlock Text="Jakosc:"/>
          <ComboBox Name="CbQuality" HorizontalAlignment="Stretch"/>
        </StackPanel>
        <StackPanel Grid.Column="2" Margin="10,0,0,0">
          <TextBlock Text="Format:"/>
          <ComboBox Name="CbFormat" HorizontalAlignment="Stretch"/>
        </StackPanel>
        <Button Name="BtnDownload" Grid.Column="4" Content="Pobierz"
                Height="30" VerticalAlignment="Bottom" HorizontalAlignment="Stretch"/>
      </Grid>

      <ProgressBar Name="ProgressBarDownload" Minimum="0" Maximum="100" Height="30"/>

      <Border BorderBrush="Gray" BorderThickness="1" Height="250">
        <TextBlock Name="LblStatus" Text="Gotowy do pobierania"
                    FontFamily="Courier New" Padding="5" TextWrapping="Wrap"/>
      </Border>
    </StackPanel>
  </DockPanel>
</Window>
```

- [ ] **Step 7: Create MainWindow.axaml.cs (shell only — no dependency/download logic yet)**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace YouTubeDownloader;

public partial class MainWindow : Window
{
    private readonly List<TextBox> extraUrlBoxes = new();
    private readonly List<Button> removeUrlButtons = new();
    private readonly HttpClient httpClient;
    private readonly string appDirectory;
    private readonly string ytDlpPath;
    private readonly string ffmpegBinPath;
    private readonly string denoPath;
    private readonly string denoVersionPath;
    private readonly string nodeJsPath;

    public MainWindow()
    {
        httpClient = new HttpClient();
        appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        ytDlpPath = Path.Combine(appDirectory, "yt-dlp.exe");
        ffmpegBinPath = Path.Combine(appDirectory, "ffmpeg_bin");
        denoPath = Path.Combine(appDirectory, "deno.exe");
        denoVersionPath = Path.Combine(appDirectory, "deno_version.txt");
        nodeJsPath = Path.Combine(appDirectory, "node.exe");

        InitializeComponent();

        CbContentType.Items.Add("Wideo + Audio");
        CbContentType.Items.Add("Tylko Audio (MP3)");
        CbContentType.SelectedIndex = 0;
        CbContentType.SelectionChanged += ContentType_Changed;

        CbQuality.Items.Add("Najlepsza");
        CbQuality.Items.Add("4K (2160p)");
        CbQuality.Items.Add("1080p");
        CbQuality.Items.Add("720p");
        CbQuality.Items.Add("480p");
        CbQuality.Items.Add("360p");
        CbQuality.Items.Add("240p");
        CbQuality.SelectedIndex = 0;

        CbFormat.Items.Add("mp4");
        CbFormat.Items.Add("webm");
        CbFormat.Items.Add("mkv");
        CbFormat.SelectedIndex = 0;

        BtnAddUrl.Click += BtnAddUrl_Click;
    }

    private void ContentType_Changed(object? sender, SelectionChangedEventArgs e)
    {
        bool isAudioOnly = CbContentType.SelectedIndex == 1;
        CbQuality.IsEnabled = !isAudioOnly;
        CbFormat.IsEnabled = !isAudioOnly;
    }

    private void BtnAddUrl_Click(object? sender, RoutedEventArgs e)
    {
        var newUrlBox = new TextBox { PlaceholderText = "Wklej kolejny link do filmu..." };
        var removeBtn = new Button { Content = "-", Width = 40 };

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        newUrlBox.Margin = new Avalonia.Thickness(0, 0, 10, 0);
        Grid.SetColumn(newUrlBox, 0);
        Grid.SetColumn(removeBtn, 1);
        row.Children.Add(newUrlBox);
        row.Children.Add(removeBtn);

        removeBtn.Click += (s, args) => RemoveUrlRow(row, newUrlBox, removeBtn);

        extraUrlBoxes.Add(newUrlBox);
        removeUrlButtons.Add(removeBtn);
        ExtraUrlRowsPanel.Children.Add(row);
    }

    private void RemoveUrlRow(Grid row, TextBox box, Button button)
    {
        extraUrlBoxes.Remove(box);
        removeUrlButtons.Remove(button);
        ExtraUrlRowsPanel.Children.Remove(row);
    }

    private void SetUrlRowsEnabled(bool enabled)
    {
        TxtUrl.IsEnabled = enabled;
        BtnAddUrl.IsEnabled = enabled;
        foreach (var box in extraUrlBoxes) box.IsEnabled = enabled;
        foreach (var btn in removeUrlButtons) btn.IsEnabled = enabled;
    }

    private List<string> GetAllUrls()
    {
        var urls = new List<string> { TxtUrl.Text?.Trim() ?? "" };
        urls.AddRange(extraUrlBoxes.Select(b => b.Text?.Trim() ?? ""));
        return urls.Where(u => !string.IsNullOrWhiteSpace(u)).ToList();
    }

    private void UpdateStatus(string message)
    {
        LblStatus.Text = message;
    }
}
```

- [ ] **Step 8: Build and verify**

Run: `dotnet build YouTubeDownloader.csproj -c Release --nologo -v minimal`
Expected: `Kompilacja powiodła się` / Build succeeded, 0 errors, 0 warnings.

- [ ] **Step 9: Smoke-test launch**

Run the built exe (`bin\Release\net10.0\YouTubeDownloader.exe`), wait ~3 seconds, confirm the process is still running, then close it (same pattern used earlier in this project: start process, sleep, check `Get-Process`, `Stop-Process`).
Expected: window opens titled "YouTube Downloader", shows the URL field, "+" button, Typ/Jakosc/Format combos, Pobierz button, progress bar, status box — no crash.

- [ ] **Step 10: Commit**

```bash
git add YouTubeDownloader.csproj Program.cs App.axaml App.axaml.cs MainWindow.axaml MainWindow.axaml.cs
git commit -m "$(cat <<'EOF'
Migrate UI shell from WinForms to Avalonia (bootstrap + MainWindow layout)

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HUiuxppZn5rnUTdevdXScu
EOF
)"
```

---

## Task 2: MessageDialog + app icon PNG asset

**Files:**
- Create: `Assets/app-logo.png` (generated from `app.ico`, not hand-written)
- Modify: `YouTubeDownloader.csproj` (register the new asset)
- Create: `MessageDialog.axaml`
- Create: `MessageDialog.axaml.cs`

**Interfaces:**
- Consumes: nothing from Task 1 beyond the project being buildable.
- Produces: `MessageDialog.ShowAsync(Window owner, string message, string title) : Task` and `MessageDialog.ShowConfirmAsync(Window owner, string message, string title) : Task<bool>` — consumed throughout Task 3 and Task 4 in place of every `MessageBox.Show(...)` call from the original WinForms code.

- [ ] **Step 1: Generate the PNG asset from the existing icon**

Run this PowerShell (one-time local asset generation, not shipped app code — uses `System.Drawing` only here, on this Windows dev machine, to produce a static file):

```powershell
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path "Assets" | Out-Null
$icon = New-Object System.Drawing.Icon("app.ico", 64, 64)
$bitmap = $icon.ToBitmap()
$bitmap.Save("Assets\app-logo.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
$icon.Dispose()
```

Expected: `Assets\app-logo.png` exists, 64x64 pixels (verified during plan-writing: `New-Object System.Drawing.Icon(path, 64, 64)` reliably extracts the largest available frame in this specific `app.ico`, which is 64x64 — the default `Icon` constructor without explicit size returns only 32x32, so the explicit `64, 64` request is required, not optional).

- [ ] **Step 2: Register the asset in the csproj**

In `YouTubeDownloader.csproj`, change:
```xml
  <ItemGroup>
    <AvaloniaResource Include="app.ico" />
  </ItemGroup>
```
to:
```xml
  <ItemGroup>
    <AvaloniaResource Include="app.ico" />
    <AvaloniaResource Include="Assets\app-logo.png" />
  </ItemGroup>
```

- [ ] **Step 3: Create MessageDialog.axaml**

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="YouTubeDownloader.MessageDialog"
        Width="380" SizeToContent="Height"
        CanResize="False"
        ShowInTaskbar="False"
        WindowStartupLocation="CenterOwner"
        Icon="/app.ico">
  <StackPanel Margin="20" Spacing="15">
    <StackPanel Orientation="Horizontal" Spacing="15">
      <Image Source="/Assets/app-logo.png" Width="32" Height="32" VerticalAlignment="Top"/>
      <TextBlock Name="MessageText" TextWrapping="Wrap" MaxWidth="290" VerticalAlignment="Center"/>
    </StackPanel>
    <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Spacing="10">
      <Button Name="OkButton" Content="OK" Width="80"/>
      <Button Name="CancelButton" Content="Nie" Width="80" IsVisible="False"/>
    </StackPanel>
  </StackPanel>
</Window>
```

- [ ] **Step 4: Create MessageDialog.axaml.cs**

```csharp
using System.Threading.Tasks;
using Avalonia.Controls;

namespace YouTubeDownloader;

public partial class MessageDialog : Window
{
    private bool confirmed;

    public MessageDialog()
    {
        InitializeComponent();
    }

    private MessageDialog(string message, string title, bool isConfirmation) : this()
    {
        Title = title;
        MessageText.Text = message;

        if (isConfirmation)
        {
            OkButton.Content = "Tak";
            CancelButton.IsVisible = true;
        }

        OkButton.Click += (s, e) => { confirmed = true; Close(); };
        CancelButton.Click += (s, e) => { confirmed = false; Close(); };
    }

    public static Task ShowAsync(Window owner, string message, string title)
    {
        var dialog = new MessageDialog(message, title, isConfirmation: false);
        return dialog.ShowDialog(owner);
    }

    public static async Task<bool> ShowConfirmAsync(Window owner, string message, string title)
    {
        var dialog = new MessageDialog(message, title, isConfirmation: true);
        await dialog.ShowDialog(owner);
        return dialog.confirmed;
    }
}
```

- [ ] **Step 5: Build and verify**

Run: `dotnet build YouTubeDownloader.csproj -c Release --nologo -v minimal`
Expected: 0 errors, 0 warnings. `MessageDialog` is not called from anywhere yet, so no behavioral smoke test applies here — Task 3 exercises it for real.

- [ ] **Step 6: Commit**

```bash
git add Assets/app-logo.png YouTubeDownloader.csproj MessageDialog.axaml MessageDialog.axaml.cs
git commit -m "$(cat <<'EOF'
Add reusable MessageDialog and app icon PNG asset for Avalonia UI

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HUiuxppZn5rnUTdevdXScu
EOF
)"
```

---

## Task 3: Dependency management + download orchestration

**Files:**
- Modify: `MainWindow.axaml.cs` (add all methods below; add three lines to the existing constructor)

**Interfaces:**
- Consumes: `MessageDialog.ShowAsync`/`ShowConfirmAsync` from Task 2; `GetAllUrls()`, `SetUrlRowsEnabled(bool)`, `UpdateStatus(string)`, and the path fields from Task 1.
- Produces: a fully working download flow — matches today's WinForms app feature-for-feature except the About dialog (Task 4).

- [ ] **Step 1: Add three lines to the existing constructor**

In `MainWindow.axaml.cs`, change:
```csharp
        BtnAddUrl.Click += BtnAddUrl_Click;
    }
```
to:
```csharp
        BtnAddUrl.Click += BtnAddUrl_Click;
        BtnDownload.Click += async (s, e) => await BtnDownload_Click();
        MiAktualizujKomponenty.Click += async (s, e) => await AktualizujKomponenty_Click();

        CheckAndDownloadComponents();
    }
```

- [ ] **Step 2: Add the required usings**

At the top of `MainWindow.axaml.cs`, change:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using Avalonia.Controls;
using Avalonia.Interactivity;
```
to:
```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
```

- [ ] **Step 3: Add dependency management methods**

Append inside the `MainWindow` class (after `UpdateStatus`):

```csharp
    // ---- Dependency management ----

    private async void CheckAndDownloadComponents()
    {
        UpdateStatus("Sprawdzanie komponentow...");

        bool hasRuntime = File.Exists(denoPath) || File.Exists(nodeJsPath) || IsRuntimeInPath();
        if (!hasRuntime)
            await DownloadDeno();

        if (!File.Exists(ytDlpPath))
        {
            UpdateStatus("Pobieranie yt-dlp...");
            await DownloadYtDlp();
        }

        if (!Directory.Exists(ffmpegBinPath))
        {
            UpdateStatus("Pobieranie FFmpeg...");
            await DownloadFFmpeg();
        }

        UpdateStatus("Wszystkie komponenty sa dostepne. Gotowy do pobierania.");
    }

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

    private async Task DownloadFileWithProgress(string url, string destinationPath)
    {
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var canReportProgress = totalBytes != -1;

        using (var contentStream = await response.Content.ReadAsStreamAsync())
        using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
        {
            var buffer = new byte[8192];
            long totalBytesRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;

                if (canReportProgress)
                {
                    var progress = (int)((totalBytesRead * 100) / totalBytes);
                    ProgressBarDownload.Value = Math.Min(progress, 100);
                }
            }
        }

        ProgressBarDownload.Value = 0;
    }

    private async Task CheckAndUpdateDeno()
    {
        try
        {
            if (!File.Exists(denoPath))
            {
                UpdateStatus("Deno: pomijanie (uzywany runtime systemowy)");
                return;
            }

            UpdateStatus("Sprawdzanie wersji Deno...");
            var (_, latestVersion) = await GetLatestDenoInfo();

            if (string.IsNullOrEmpty(latestVersion))
                return;

            string currentVersion = "";
            if (File.Exists(denoVersionPath))
                currentVersion = await File.ReadAllTextAsync(denoVersionPath);

            if (string.IsNullOrEmpty(currentVersion) || currentVersion != latestVersion)
            {
                UpdateStatus("Deno: Nowa wersja dostepna");
                await DownloadDeno();
            }
            else
            {
                UpdateStatus("Deno: Wersja aktualna");
            }
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowAsync(this, "Blad aktualizacji Deno: " + ex.Message, "Blad");
        }
    }

    private async Task CheckAndUpdateFFmpeg()
    {
        try
        {
            var (_, latestVersion) = await GetLatestFFmpegInfo();

            string versionFile = Path.Combine(appDirectory, "ffmpeg_version.txt");
            string currentVersion = "";

            if (File.Exists(versionFile))
                currentVersion = await File.ReadAllTextAsync(versionFile);

            if (string.IsNullOrEmpty(currentVersion) || currentVersion != latestVersion)
            {
                UpdateStatus("FFmpeg: Nowa wersja dostepna");

                if (Directory.Exists(ffmpegBinPath))
                    Directory.Delete(ffmpegBinPath, true);

                await DownloadFFmpeg();
            }
            else
            {
                UpdateStatus("FFmpeg: Wersja aktualna");
            }
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowAsync(this, "Blad aktualizacji: " + ex.Message, "Blad");
        }
    }

    private async Task AktualizujKomponenty_Click()
    {
        bool confirmed = await MessageDialog.ShowConfirmAsync(this, "Zaktualizowac komponenty?", "Aktualizacja");

        if (confirmed)
        {
            BtnDownload.IsEnabled = false;

            UpdateStatus("Aktualizacja yt-dlp...");
            if (File.Exists(ytDlpPath))
            {
                try
                {
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = ytDlpPath,
                        Arguments = "-U",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    using (var process = Process.Start(processInfo))
                    {
                        if (process != null)
                            await process.WaitForExitAsync();
                    }
                    UpdateStatus("yt-dlp zaktualizowane");
                }
                catch (Exception ex)
                {
                    await MessageDialog.ShowAsync(this, "Blad: " + ex.Message, "Blad");
                }
            }

            await CheckAndUpdateDeno();
            await CheckAndUpdateFFmpeg();
            UpdateStatus("Aktualizacja zakonczena");
            BtnDownload.IsEnabled = true;
        }
    }
```

- [ ] **Step 4: Add URL handling methods**

Append inside the `MainWindow` class:

```csharp
    // ---- URL handling ----

    private string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "";

        url = url.Trim();

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;

        if (url.StartsWith("www.youtube.com", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("youtube.com", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("youtu.be", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("m.youtube.com", StringComparison.OrdinalIgnoreCase))
            return "https://" + url;

        if (Regex.IsMatch(url, "^[a-zA-Z0-9_-]{11}$"))
            return "https://www.youtube.com/watch?v=" + url;

        return "https://" + url;
    }

    private bool ValidateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            UpdateStatus("Blad: URL nie moze byc pusty");
            return false;
        }

        string normalizedUrl = NormalizeUrl(url);

        if (!normalizedUrl.Contains("youtube.com") && !normalizedUrl.Contains("youtu.be"))
        {
            UpdateStatus("Blad: Tylko linki YouTube");
            return false;
        }

        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uriResult) ||
            (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
        {
            UpdateStatus("Blad: Nieprawidlowy URL");
            return false;
        }

        return true;
    }
```

- [ ] **Step 5: Add the download flow methods**

Append inside the `MainWindow` class:

```csharp
    // ---- Download flow ----

    private string BuildYtDlpArguments()
    {
        string args = "";

        if (CbContentType.SelectedIndex == 1)
        {
            args = " -f bestaudio --extract-audio --audio-format mp3 --audio-quality 192";
        }
        else
        {
            string quality = CbQuality.SelectedItem?.ToString() ?? "Najlepsza";
            string format = CbFormat.SelectedItem?.ToString() ?? "mp4";

            if (quality == "Najlepsza")
            {
                args = " -f bestvideo+bestaudio/best";
            }
            else if (quality == "4K (2160p)")
            {
                args = " -f bestvideo[height<=2160]+bestaudio/best[height<=2160]";
            }
            else if (quality == "1080p")
            {
                args = " -f bestvideo[height<=1080]+bestaudio/best[height<=1080]";
            }
            else
            {
                string heightStr = quality.Replace("p", "");
                args = " -f bestvideo[height<=" + heightStr + "]+bestaudio/best[height<=" + heightStr + "]";
            }

            if (format == "mp4")
            {
                args += " --remux-video mp4";
            }
            else if (format == "webm")
            {
                args += " --remux-video webm";
            }
            else if (format == "mkv")
            {
                args += " --merge-output-format mkv";
            }
        }

        return args;
    }

    private void ParseDownloadProgress(string line, string statusPrefix = "")
    {
        try
        {
            if (!line.Contains("[download]"))
                return;

            Regex percentRegex = new Regex(@"([0-9]+(?:\.[0-9]+)?)%");
            Match percentMatch = percentRegex.Match(line);
            double percent = 0.0;
            if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedPercent))
            {
                percent = parsedPercent;
                ProgressBarDownload.Value = Math.Min((int)percent, 100);
            }

            Regex sizeRegex = new Regex(@"of\s+([0-9.]+)(MiB|GiB|KiB|B)");
            Match sizeMatch = sizeRegex.Match(line);
            string downloadedSize = sizeMatch.Success ? sizeMatch.Groups[1].Value + sizeMatch.Groups[2].Value : "";

            Regex speedRegex = new Regex(@"at\s+([0-9.]+)(MiB|GiB|KiB|B)/s");
            Match speedMatch = speedRegex.Match(line);
            string speed = speedMatch.Success ? speedMatch.Groups[1].Value + speedMatch.Groups[2].Value + "/s" : "";

            Regex etaRegex = new Regex(@"ETA\s+([0-9]{2}:[0-9]{2}:[0-9]{2}|[0-9]{2}:[0-9]{2})");
            Match etaMatch = etaRegex.Match(line);
            string eta = etaMatch.Success ? etaMatch.Groups[1].Value : "";

            string status = "Pobieranie: " + percent.ToString("F1") + "%";
            if (!string.IsNullOrEmpty(downloadedSize))
                status += " Rozmiar: " + downloadedSize;
            if (!string.IsNullOrEmpty(speed))
                status += " Predkosc: " + speed;
            if (!string.IsNullOrEmpty(eta))
                status += " ETA: " + eta;

            UpdateStatus(statusPrefix + status);
        }
        catch { }
    }

    private async Task BtnDownload_Click()
    {
        List<string> rawUrls = GetAllUrls();

        if (rawUrls.Count == 0)
        {
            await MessageDialog.ShowAsync(this, "Podaj przynajmniej jeden link", "Blad");
            return;
        }

        foreach (string url in rawUrls)
        {
            if (!ValidateUrl(url))
            {
                await MessageDialog.ShowAsync(this, "Nieprawidlowy link: " + url, "Blad");
                return;
            }
        }

        if (!File.Exists(ytDlpPath))
        {
            await MessageDialog.ShowAsync(this, "yt-dlp niedostepne", "Blad");
            return;
        }

        if (!Directory.Exists(ffmpegBinPath))
        {
            await MessageDialog.ShowAsync(this, "FFmpeg niedostepne", "Blad");
            return;
        }

        string runtimePath = GetRuntimePath();
        if (string.IsNullOrEmpty(runtimePath))
        {
            await MessageDialog.ShowAsync(this, "Brak runtime Deno/Node.js", "Blad");
            return;
        }

        BtnDownload.IsEnabled = false;
        SetUrlRowsEnabled(false);

        int successCount = 0;
        string downloadsDir = Path.Combine(appDirectory, "downloads");

        for (int i = 0; i < rawUrls.Count; i++)
        {
            ProgressBarDownload.Value = 0;

            string statusPrefix = rawUrls.Count > 1 ? $"[{i + 1}/{rawUrls.Count}] " : "";
            UpdateStatus(statusPrefix + "Przygotowanie...");

            bool success = await DownloadSingleUrlAsync(rawUrls[i], runtimePath, statusPrefix);
            if (success)
                successCount++;
        }

        BtnDownload.IsEnabled = true;
        SetUrlRowsEnabled(true);
        ProgressBarDownload.Value = 0;

        if (successCount == rawUrls.Count)
        {
            string message = rawUrls.Count > 1 ? "Wszystkie pliki pobrane" : "Plik pobrany";
            UpdateStatus("Pobieranie zakonczono!");
            await MessageDialog.ShowAsync(this, message + ". Lokalizacja: " + downloadsDir, "Sukces");
        }
        else
        {
            UpdateStatus($"Zakonczono: {successCount}/{rawUrls.Count} pobranych");
            await MessageDialog.ShowAsync(this, $"Pobrano {successCount} z {rawUrls.Count} plikow.", "Zakonczono z bledami");
        }
    }

    private async Task<bool> DownloadSingleUrlAsync(string rawUrl, string runtimePath, string statusPrefix)
    {
        try
        {
            string normalizedUrl = NormalizeUrl(rawUrl);
            string jsRuntimeArg = runtimePath.EndsWith("deno.exe", StringComparison.OrdinalIgnoreCase) ? "" : "--js-runtimes node";
            string ytDlpArgs = BuildYtDlpArguments();
            string downloadsDir = Path.Combine(appDirectory, "downloads");
            string outputPattern = Path.Combine(downloadsDir, "%(title)s.%(ext)s");

            StringBuilder argBuilder = new StringBuilder();
            if (!string.IsNullOrEmpty(jsRuntimeArg))
            {
                argBuilder.Append(jsRuntimeArg);
                argBuilder.Append(" ");
            }
            argBuilder.Append(ytDlpArgs);
            argBuilder.Append(" --ffmpeg-location \"");
            argBuilder.Append(ffmpegBinPath);
            argBuilder.Append("\" --progress --newline -o \"");
            argBuilder.Append(outputPattern);
            argBuilder.Append("\" \"");
            argBuilder.Append(normalizedUrl);
            argBuilder.Append("\"");

            var processInfo = new ProcessStartInfo
            {
                FileName = ytDlpPath,
                Arguments = argBuilder.ToString(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Directory.CreateDirectory(downloadsDir);

            using var process = new Process { StartInfo = processInfo };
            process.OutputDataReceived += (s, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    string data = args.Data;
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (data.Contains("[download]"))
                        {
                            ParseDownloadProgress(data, statusPrefix);
                        }
                        else if (data.Contains("[info]") || data.Contains("Downloading"))
                        {
                            LblStatus.Text = statusPrefix + data;
                        }
                    });
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                ProgressBarDownload.Value = 100;
                UpdateStatus(statusPrefix + "Pobrano");
                return true;
            }
            else
            {
                string error = await process.StandardError.ReadToEndAsync();
                UpdateStatus(statusPrefix + "Blad pobierania");
                await MessageDialog.ShowAsync(this, "Blad (" + rawUrl + "): " + error, "Blad");
                return false;
            }
        }
        catch (Exception ex)
        {
            UpdateStatus(statusPrefix + "Blad");
            await MessageDialog.ShowAsync(this, "Blad (" + rawUrl + "): " + ex.Message, "Blad");
            return false;
        }
    }
```

- [ ] **Step 6: Build and verify**

Run: `dotnet build YouTubeDownloader.csproj -c Release --nologo -v minimal`
Expected: 0 errors, 0 warnings.

- [ ] **Step 7: Smoke-test launch**

Start the built exe, wait ~3 seconds, confirm still running, close it. Note: on first launch in a clean environment this will attempt real downloads of yt-dlp/FFmpeg/Deno (identical to today's WinForms behavior) — if `bin\Release\net10.0\` already has these from a previous run, it skips straight to "Gotowy do pobierania".
Expected: no crash; status label updates as components are checked.

- [ ] **Step 8: Commit**

```bash
git add MainWindow.axaml.cs
git commit -m "$(cat <<'EOF'
Port dependency management and download orchestration to MainWindow

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HUiuxppZn5rnUTdevdXScu
EOF
)"
```

---

## Task 4: AboutWindow ("Informacje" dialog)

**Files:**
- Create: `AboutWindow.axaml`
- Create: `AboutWindow.axaml.cs`
- Modify: `MainWindow.axaml.cs` (wire the menu item + add `Informacje_Click`)

**Interfaces:**
- Consumes: `MessageDialog.ShowAsync` (Task 2), `Assets/app-logo.png` asset (Task 2), `MiInformacje` field (Task 1).
- Produces: full feature parity with today's WinForms app.

- [ ] **Step 1: Create AboutWindow.axaml**

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="YouTubeDownloader.AboutWindow"
        Title="Informacje o programie"
        Width="400" SizeToContent="Height"
        CanResize="False"
        WindowStartupLocation="CenterOwner"
        Icon="/app.ico">
  <StackPanel Margin="20" Spacing="10" HorizontalAlignment="Center">
    <Image Source="/Assets/app-logo.png" Width="64" Height="64" HorizontalAlignment="Center"/>
    <TextBlock Text="YouTube Downloader" FontSize="18" FontWeight="Bold" HorizontalAlignment="Center"/>
    <TextBlock Text="Wersja 1.0.0" HorizontalAlignment="Center"/>
    <TextBlock Text="Licencja: Apache License 2.0" HorizontalAlignment="Center"/>

    <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
      <TextBlock Text="Repozytorium: "/>
      <TextBlock Name="RepoLink" Text="github.com/bauerpawel/YouTube-Downloader"
                  Foreground="DodgerBlue" TextDecorations="Underline" Cursor="Hand"/>
    </StackPanel>

    <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
      <TextBlock Text="Autor: "/>
      <TextBlock Name="AuthorLink" Text="bauer.net.pl"
                  Foreground="DodgerBlue" TextDecorations="Underline" Cursor="Hand"/>
    </StackPanel>

    <Button Name="CloseButton" Content="Zamknij" Width="100" HorizontalAlignment="Center" Margin="0,10,0,0"/>
  </StackPanel>
</Window>
```

- [ ] **Step 2: Create AboutWindow.axaml.cs**

```csharp
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace YouTubeDownloader;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        RepoLink.PointerPressed += async (s, e) => await OpenUrl("https://github.com/bauerpawel/YouTube-Downloader");
        AuthorLink.PointerPressed += async (s, e) => await OpenUrl("https://bauer.net.pl");
        CloseButton.Click += (s, e) => Close();
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
}
```

- [ ] **Step 3: Wire the menu item in MainWindow.axaml.cs**

Change:
```csharp
        BtnAddUrl.Click += BtnAddUrl_Click;
        BtnDownload.Click += async (s, e) => await BtnDownload_Click();
        MiAktualizujKomponenty.Click += async (s, e) => await AktualizujKomponenty_Click();

        CheckAndDownloadComponents();
    }
```
to:
```csharp
        BtnAddUrl.Click += BtnAddUrl_Click;
        BtnDownload.Click += async (s, e) => await BtnDownload_Click();
        MiAktualizujKomponenty.Click += async (s, e) => await AktualizujKomponenty_Click();
        MiInformacje.Click += async (s, e) => await Informacje_Click();

        CheckAndDownloadComponents();
    }
```

- [ ] **Step 4: Add Informacje_Click to MainWindow.axaml.cs**

Append inside the `MainWindow` class:

```csharp
    private async Task Informacje_Click()
    {
        var about = new AboutWindow();
        await about.ShowDialog(this);
    }
```

- [ ] **Step 5: Build and verify**

Run: `dotnet build YouTubeDownloader.csproj -c Release --nologo -v minimal`
Expected: 0 errors, 0 warnings.

- [ ] **Step 6: Smoke-test launch**

Start the built exe, wait ~3 seconds, confirm still running, close it.
Expected: no crash; app is now at full feature parity with the pre-migration WinForms version.

- [ ] **Step 7: Commit**

```bash
git add AboutWindow.axaml AboutWindow.axaml.cs MainWindow.axaml.cs
git commit -m "$(cat <<'EOF'
Add AboutWindow (Informacje dialog) to Avalonia UI, reaching feature parity

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HUiuxppZn5rnUTdevdXScu
EOF
)"
```

---

## Task 5: Update CLAUDE.md documentation

**Files:**
- Modify: `CLAUDE.md`

**Interfaces:** None (documentation only).

- [ ] **Step 1: Rewrite "Repository Structure"**

Replace the `MainForm.cs` entry and the "Single-File Design" description with the new file list: `Program.cs`, `App.axaml`/`App.axaml.cs`, `MainWindow.axaml`/`MainWindow.axaml.cs`, `AboutWindow.axaml`/`AboutWindow.axaml.cs`, `MessageDialog.axaml`/`MessageDialog.axaml.cs`, `Assets/app-logo.png`.

- [ ] **Step 2: Rewrite "Codebase Architecture" and "Key Components"**

Replace line-number references into `MainForm.cs` (which no longer exists) with references to the new files and their responsibilities, matching the breakdown used in this plan's task list (dependency management and download orchestration both live in `MainWindow.axaml.cs`; the About dialog is `AboutWindow`; message boxes are `MessageDialog`).

- [ ] **Step 3: Update "Key Conventions"**

- Replace "Windows Forms" framework references with "Avalonia UI 12.1.2 (XAML + code-behind, no MVVM)".
- Note that XAML-named elements (`x:Name`) become code-behind fields automatically — no manual field declarations for them, unlike the old WinForms pattern.
- Note the event-wiring convention: controls declared in XAML, event handlers wired in the constructor via `Control.Event += Handler;` (not XAML `Click=` attributes), to keep XAML/code-behind compile-order independent — matches this plan's actual code.
- Note `IsEnabled` (Avalonia) replaces `Enabled` (WinForms), `SelectionChanged` replaces `SelectedIndexChanged` for `ComboBox`.

- [ ] **Step 4: Update "NuGet Dependencies"**

Replace "None - project uses only .NET 10 BCL (Base Class Library)" with the three pinned Avalonia packages (`Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`) and their version (12.1.2), and a note that future CVE audits must now check these too.

- [ ] **Step 5: Update the "Publish single-file executable" command block**

Update `TargetFramework` references from `net10.0-windows` to `net10.0` anywhere they appear (should already be consistent after Tasks 1-4, but check for stale mentions), and confirm the win-x64/win-arm64 publish commands are unchanged (already correct from the earlier ARM64 work).

- [ ] **Step 6: Update "Testing Checklist" and "AI Assistant Guidelines"**

Remove WinForms-specific guidance (`InitializeComponent()` line-number references, "all UI controls are nullable" — Avalonia XAML-named fields are non-nullable per this plan's verified POC). Add a note matching this plan's Global Constraints: no GUI test automation is available in this environment; verification is build + smoke-launch, with manual QA for actual UI/download behavior.

- [ ] **Step 7: Update "Last Updated" date**

Change to `2026-09-09`.

- [ ] **Step 8: Commit**

```bash
git add CLAUDE.md
git commit -m "$(cat <<'EOF'
Update CLAUDE.md to reflect Avalonia UI architecture

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HUiuxppZn5rnUTdevdXScu
EOF
)"
```

---

## Task 6: Final end-to-end verification

**Files:** None (verification only, plus a manual QA checklist artifact for the user).

**Interfaces:** None.

- [ ] **Step 1: Full Release build**

Run: `dotnet build YouTubeDownloader.csproj -c Release --nologo -v minimal`
Expected: 0 errors, 0 warnings.

- [ ] **Step 2: Confirm CI needs no changes**

Read `.github/workflows/dotnet-desktop.yml`. Confirm the `build` job (`dotnet restore`/`build`/`test`) and `publish` job (`dotnet publish -r ${{ matrix.rid }} --self-contained true -p:PublishSingleFile=true ...` for `[win-x64, win-arm64]`) reference no WinForms-specific flags (`UseWindowsForms` is not set anywhere in the workflow) — they are UI-framework-agnostic and require no edits. Do not modify this file if that holds.

- [ ] **Step 3: Publish and smoke-test win-x64**

Run: `build.bat win-x64` (or the equivalent `dotnet publish` command from `build.bat`).
Then start `publish\win-x64\YouTubeDownloader.exe`, wait ~3 seconds, confirm still running, close it.
Expected: publish succeeds, exe launches without crash — self-contained single-file publish was already confirmed to work with Avalonia 12.1.2 during plan-writing (proof-of-concept build+publish+launch all succeeded).

- [ ] **Step 4: Publish and smoke-test win-arm64**

Run: `build.bat win-arm64`.
Then start `publish\win-arm64\YouTubeDownloader.exe` — **only if this machine's CPU is ARM64**. If running on x64 hardware (as in this session), confirm the publish step itself succeeds (file exists, correct size) but skip the launch smoke test — an ARM64 binary cannot run on x64 hardware. Note this limitation explicitly when reporting results; it mirrors exactly how the ARM64 build was verified earlier in this project (build succeeds locally, published binary correctness relies on the CI matrix / real ARM64 hardware for a true run smoke-test).

- [ ] **Step 5: Deliver a manual QA checklist to the user**

Since no GUI automation is available, report this checklist to the user for them to run manually (do not claim any of these are agent-verified):
- [ ] App window opens at the expected size, title "YouTube Downloader", app icon visible in the title bar.
- [ ] Click "+" next to the URL field: a new row with a text field (placeholder "Wklej kolejny link do filmu...") and a "-" button appears; window grows to fit it.
- [ ] Click "-" on that new row: it disappears; window shrinks back.
- [ ] Add 2-3 URL rows, then remove them in a different order than added — no visual glitches, no leftover empty rows.
- [ ] Switch "Typ" to "Tylko Audio (MP3)": "Jakosc" and "Format" combo boxes become disabled; switch back and they re-enable.
- [ ] Menu "Narzedzia" -> "Aktualizuj komponenty": confirmation dialog appears with "Tak"/"Nie" buttons (Polish text, app icon shown); clicking "Nie" cancels with no further action; clicking "Tak" runs the update flow and status text updates.
- [ ] Menu "Pomoc" -> "Informacje": About window opens, showing the app logo, "YouTube Downloader", "Wersja 1.0.0", "Licencja: Apache License 2.0", and two lines where only the address portion (not the "Repozytorium:"/"Autor:" prefix) is a clickable, underlined link.
- [ ] Click the repository link: opens `https://github.com/bauerpawel/YouTube-Downloader` in the default browser.
- [ ] Click the author link: opens `https://bauer.net.pl` in the default browser.
- [ ] Close the About window with "Zamknij": returns focus to the main window.
- [ ] Paste a real YouTube URL, click "Pobierz": progress bar and status text update live during download; on success, a dialog reports success with the downloads folder path; the file appears in `downloads\`.
- [ ] Try downloading with an empty URL field and no extra rows: a "Podaj przynajmniej jeden link" dialog appears, no download attempted.
- [ ] Try downloading with an invalid (non-YouTube) URL: a "Nieprawidlowy link: ..." dialog appears, no download attempted.
- [ ] Add two valid URLs and click "Pobierz": both download sequentially, status shows `[1/2]`/`[2/2]` prefixes, final dialog reports "Wszystkie pliki pobrane" (or the partial-success variant if one fails).

- [ ] **Step 6: Report results**

Summarize to the user: build status, which smoke tests passed, and hand off the manual QA checklist above (as a message, not a new file — this is one-time verification guidance, not project documentation).
