using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace YouTubeDownloader;

public partial class MainWindow : Window
{
    private readonly List<TextBox> extraUrlBoxes = new();
    private readonly List<Button> removeUrlButtons = new();
    private readonly HttpClient httpClient;
    private readonly string appDirectory;
    private readonly string dataDirectory;
    private readonly string ffmpegVersionPath;
    private readonly string ytDlpPath;
    private readonly string ffmpegBinPath;
    private readonly string denoPath;
    private readonly string denoVersionPath;
    private readonly string nodeJsPath;

    private static Strings Ui => Strings.Current;

    public MainWindow()
    {
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

        // Remove what a previous self-update left behind (<exe>.old on Windows is
        // only deletable once the old process has exited).
        AppUpdater.CleanupLeftovers(appDirectory, Environment.ProcessPath);

        InitializeComponent();

        CbContentType.SelectionChanged += ContentType_Changed;

        CbFormat.Items.Add("mp4");
        CbFormat.Items.Add("webm");
        CbFormat.Items.Add("mkv");
        CbFormat.SelectedIndex = 0;

        ApplyTexts();
        LblStatus.Text = Ui.StatusReady;

        BtnAddUrl.Click += BtnAddUrl_Click;
        BtnDownload.Click += async (s, e) => await BtnDownload_Click();
        MiAktualizujKomponenty.Click += async (s, e) => await AktualizujKomponenty_Click();
        MiSprawdzAktualizacje.Click += async (s, e) => await CheckForAppUpdate(silent: false);
        MiInformacje.Click += async (s, e) => await Informacje_Click();

        MiThemeLight.Click += (s, e) => SetTheme("Light");
        MiThemeDark.Click += (s, e) => SetTheme("Dark");
        MiThemeSystem.Click += (s, e) => SetTheme("Default");
        MiLangPolish.Click += (s, e) => SetLanguage(LanguageSettings.Polish);
        MiLangEnglish.Click += (s, e) => SetLanguage(LanguageSettings.English);

        string currentTheme = ThemeSettings.Load(dataDirectory);
        MiThemeLight.IsChecked = currentTheme == "Light";
        MiThemeDark.IsChecked = currentTheme == "Dark";
        MiThemeSystem.IsChecked = currentTheme == "Default";

        Opened += (s, e) => CheckAndDownloadComponents();
    }

    private void SetTheme(string name)
    {
        Application.Current!.RequestedThemeVariant = ThemeSettings.ToVariant(name);
        ThemeSettings.Save(dataDirectory, name);
    }

    private void SetLanguage(string code)
    {
        Strings.Current = Strings.For(code);
        LanguageSettings.Save(dataDirectory, code);
        ApplyTexts();
    }

    // Every text of this window from Strings.Current. Runs at startup and on each
    // language switch; list selections and extra URL rows survive. The status line
    // is left alone - it keeps its language until the next status update.
    private void ApplyTexts()
    {
        MiTools.Header = Ui.MenuTools;
        MiAktualizujKomponenty.Header = Ui.MenuUpdateComponents;
        MiSprawdzAktualizacje.Header = Ui.MenuCheckAppUpdate;
        MiView.Header = Ui.MenuView;
        MiThemeLight.Header = Ui.MenuThemeLight;
        MiThemeDark.Header = Ui.MenuThemeDark;
        MiThemeSystem.Header = Ui.MenuThemeSystem;
        MiLanguage.Header = Ui.MenuLanguage;
        MiHelp.Header = Ui.MenuHelp;
        MiInformacje.Header = Ui.MenuAbout;
        MiLangPolish.IsChecked = Ui == Strings.Polish;
        MiLangEnglish.IsChecked = Ui == Strings.English;

        LblUrl.Text = Ui.LabelUrl;
        LblContentType.Text = Ui.LabelContentType;
        LblQuality.Text = Ui.LabelQuality;
        LblFormat.Text = Ui.LabelFormat;
        BtnDownload.Content = Ui.ButtonDownload;
        foreach (var box in extraUrlBoxes)
            box.PlaceholderText = Ui.PlaceholderExtraUrl;

        int contentTypeIndex = Math.Max(CbContentType.SelectedIndex, 0);
        CbContentType.Items.Clear();
        CbContentType.Items.Add(Ui.ContentTypeVideoAudio);
        CbContentType.Items.Add(Ui.ContentTypeAudioOnly);
        CbContentType.SelectedIndex = contentTypeIndex;

        int qualityIndex = Math.Max(CbQuality.SelectedIndex, 0);
        CbQuality.Items.Clear();
        foreach (int? height in YtDlpArguments.QualityHeights)
            CbQuality.Items.Add(YtDlpArguments.QualityLabel(height, Ui.QualityBest));
        CbQuality.SelectedIndex = qualityIndex;
    }

    protected override void OnClosed(EventArgs e)
    {
        httpClient.Dispose();
        base.OnClosed(e);
    }

    private void ContentType_Changed(object? sender, SelectionChangedEventArgs e)
    {
        bool isAudioOnly = CbContentType.SelectedIndex == 1;
        CbQuality.IsEnabled = !isAudioOnly;
        CbFormat.IsEnabled = !isAudioOnly;
    }

    private void BtnAddUrl_Click(object? sender, RoutedEventArgs e)
    {
        var newUrlBox = new TextBox { PlaceholderText = Ui.PlaceholderExtraUrl };
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
        // Mirrored to stdout: invisible to users (no console on a GUI app), but it is
        // the only trace a headless CI smoke test gets of where startup stopped.
        Console.WriteLine("[status] " + message);
    }

    // One switch for everything that must not overlap: video downloads, component
    // downloads/updates and the app self-update (which ends in a shutdown that would
    // otherwise cut a component download short and leave a truncated binary behind).
    private void SetBusy(bool busy)
    {
        BtnDownload.IsEnabled = !busy;
        MiSprawdzAktualizacje.IsEnabled = !busy;
        MiAktualizujKomponenty.IsEnabled = !busy;
    }

    // ---- Dependency management ----

    private async void CheckAndDownloadComponents()
    {
        SetBusy(true);
        UpdateStatus(Ui.StatusCheckingComponents);

        bool hasRuntime = File.Exists(denoPath) || File.Exists(nodeJsPath) || IsRuntimeInPath();
        if (!hasRuntime)
            await DownloadDeno();

        if (!File.Exists(ytDlpPath))
        {
            UpdateStatus(Ui.StatusDownloadingYtDlp);
            await DownloadYtDlp();
        }

        if (!Directory.Exists(ffmpegBinPath))
        {
            UpdateStatus(Ui.StatusDownloadingFFmpeg);
            await DownloadFFmpeg();
        }

        UpdateStatus(Ui.StatusAllComponentsReady);

        // Versions before the data directory kept the tools next to the exe. Remove
        // those copies only once working replacements exist in the data directory.
        if (File.Exists(ytDlpPath) && Directory.Exists(ffmpegBinPath) && !string.IsNullOrEmpty(GetRuntimePath()))
            AppPaths.CleanupLegacyFiles(appDirectory, dataDirectory);

        SetBusy(false);

        // async void caller: nothing may escape from here, and the startup check
        // must never bother the user with errors (offline, API rate limit).
        try
        {
            await CheckForAppUpdate(silent: true);
        }
        catch (Exception)
        {
        }
    }

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

    private async Task<(string downloadUrl, string version)> GetLatestDenoInfo()
    {
        try
        {
            string apiUrl = "https://api.github.com/repos/denoland/deno/releases/latest";

            var response = await GitHubApi.GetStringAsync(httpClient, apiUrl);
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
            await MessageDialog.ShowAsync(this, Ui.ErrorDenoInfo(ex.Message), Ui.TitleError);
            return ("", "");
        }
    }

    private async Task DownloadDeno()
    {
        try
        {
            UpdateStatus(Ui.StatusDownloadingDeno);

            var (downloadUrl, version) = await GetLatestDenoInfo();

            if (string.IsNullOrEmpty(downloadUrl))
            {
                await MessageDialog.ShowAsync(this, Ui.ErrorDenoNotFound, Ui.TitleError);
                return;
            }

            string zipPath = Path.Combine(dataDirectory, "deno.zip");
            await DownloadFileWithProgress(downloadUrl, zipPath);

            UpdateStatus(Ui.StatusExtractingDeno);

            string denoEntryName = OperatingSystem.IsWindows() ? "deno.exe" : "deno";
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                var denoEntry = archive.GetEntry(denoEntryName);
                if (denoEntry != null)
                    denoEntry.ExtractToFile(denoPath, true);
            }

            File.Delete(zipPath);

            AppPaths.MakeExecutable(denoPath);

            if (!string.IsNullOrEmpty(version))
                await File.WriteAllTextAsync(denoVersionPath, version);

            UpdateStatus(Ui.StatusDenoInstalled);
        }
        catch (Exception ex)
        {
            UpdateStatus(Ui.StatusDenoError(ex.Message));
            await MessageDialog.ShowAsync(this, Ui.WarningDenoFailed, Ui.TitleWarning);
        }
    }

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

    private async Task DownloadYtDlp()
    {
        try
        {
            string url = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/" + GetYtDlpAssetName();
            await DownloadFileWithProgress(url, ytDlpPath);
            AppPaths.MakeExecutable(ytDlpPath);
            UpdateStatus(Ui.StatusYtDlpDownloaded);
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowAsync(this, Ui.ErrorYtDlpDownload(ex.Message), Ui.TitleError);
        }
    }

    // macOS deliberately never reaches this method - it queries BtbN/FFmpeg-Builds, which only
    // publishes Windows/Linux assets. macOS FFmpeg comes from GetLatestFFmpegInfoMac() instead;
    // both callers of GetLatestFFmpegInfo() (this method's only caller) already branch away from
    // macOS via OperatingSystem.IsMacOS() checks in DownloadFFmpeg() and CheckAndUpdateFFmpeg().
    // Do not "fix" the exception message below to mention macOS - it would be inaccurate.
    private static bool IsMatchingFFmpegAsset(string assetName)
    {
        if (OperatingSystem.IsWindows())
            return assetName.Contains("win64-gpl-shared") && assetName.EndsWith(".zip");

        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("FFmpeg auto-download is only supported on Windows and Linux.");

        bool isArm = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        string suffix = isArm ? "linuxarm64-gpl.tar.xz" : "linux64-gpl.tar.xz";
        return assetName.EndsWith(suffix);
    }

    private async Task<(string downloadUrl, string version)> GetLatestFFmpegInfo()
    {
        try
        {
            string releasesUrl = "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases";
            var response = await GitHubApi.GetStringAsync(httpClient, releasesUrl);
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
            await MessageDialog.ShowAsync(this, Ui.ErrorFFmpegInfo(ex.Message), Ui.TitleError);
            return ("", "");
        }
    }

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
            string apiUrl = "https://api.github.com/repos/eugeneware/ffmpeg-static/releases/latest";

            var response = await GitHubApi.GetStringAsync(httpClient, apiUrl);
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
            await MessageDialog.ShowAsync(this, Ui.ErrorFFmpegInfo(ex.Message), Ui.TitleError);
            return ("", "", "");
        }
    }

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
            throw new Exception(Ui.ErrorTarStart);

        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            string error = await process.StandardError.ReadToEndAsync();
            throw new Exception(Ui.ErrorTarFailed(error));
        }
    }

    private async Task DownloadFFmpeg()
    {
        if (OperatingSystem.IsMacOS())
        {
            await DownloadFFmpegMac();
            return;
        }

        try
        {
            UpdateStatus(Ui.StatusFetchingFFmpegInfo);
            var (downloadUrl, version) = await GetLatestFFmpegInfo();

            if (string.IsNullOrEmpty(downloadUrl))
                throw new Exception(Ui.ErrorFFmpegLinkNotFound);

            UpdateStatus(Ui.StatusDownloadingFFmpegVersion(version));
            string archiveExtension = OperatingSystem.IsWindows() ? ".zip" : ".tar.xz";
            string archivePath = Path.Combine(dataDirectory, "ffmpeg" + archiveExtension);

            await DownloadFileWithProgress(downloadUrl, archivePath);

            UpdateStatus(Ui.StatusExtractingFFmpeg);

            if (Directory.Exists(ffmpegBinPath))
                Directory.Delete(ffmpegBinPath, true);

            string tempExtractPath = Path.Combine(dataDirectory, "ffmpeg_temp");
            if (Directory.Exists(tempExtractPath))
                Directory.Delete(tempExtractPath, true);

            await ExtractArchive(archivePath, tempExtractPath);

            string[] binPaths = Directory.GetDirectories(tempExtractPath, "bin", SearchOption.AllDirectories);

            if (binPaths.Length == 0)
                throw new Exception(Ui.ErrorFFmpegBinNotFound);

            string sourceBinPath = binPaths[0];
            Directory.CreateDirectory(ffmpegBinPath);

            foreach (string file in Directory.GetFiles(sourceBinPath))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(ffmpegBinPath, fileName);
                File.Copy(file, destFile, true);
                AppPaths.MakeExecutable(destFile);
            }

            await File.WriteAllTextAsync(ffmpegVersionPath, version);

            File.Delete(archivePath);
            Directory.Delete(tempExtractPath, true);

            UpdateStatus(Ui.StatusFFmpegDownloaded(version));
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowAsync(this, Ui.ErrorFFmpeg(ex.Message), Ui.TitleError);
            UpdateStatus(Ui.ErrorWithDetails(ex.Message));
        }
    }

    private async Task DownloadFFmpegMac()
    {
        try
        {
            UpdateStatus(Ui.StatusFetchingFFmpegInfo);
            var (ffmpegUrl, ffprobeUrl, version) = await GetLatestFFmpegInfoMac();

            if (string.IsNullOrEmpty(ffmpegUrl) || string.IsNullOrEmpty(ffprobeUrl))
                throw new Exception(Ui.ErrorFFmpegLinkNotFound);

            UpdateStatus(Ui.StatusDownloadingFFmpegVersion(version));

            if (Directory.Exists(ffmpegBinPath))
                Directory.Delete(ffmpegBinPath, true);
            Directory.CreateDirectory(ffmpegBinPath);

            string ffmpegDestPath = Path.Combine(ffmpegBinPath, "ffmpeg");
            string ffprobeDestPath = Path.Combine(ffmpegBinPath, "ffprobe");

            await DownloadFileWithProgress(ffmpegUrl, ffmpegDestPath);
            AppPaths.MakeExecutable(ffmpegDestPath);

            await DownloadFileWithProgress(ffprobeUrl, ffprobeDestPath);
            AppPaths.MakeExecutable(ffprobeDestPath);

            await File.WriteAllTextAsync(ffmpegVersionPath, version);

            UpdateStatus(Ui.StatusFFmpegDownloaded(version));
        }
        catch (Exception ex)
        {
            // Don't leave behind an empty/partial ffmpeg_bin/ - its mere existence is the
            // app's only signal that FFmpeg is installed (see CheckAndDownloadComponents()),
            // so a half-finished download here must not look like a successful install.
            if (Directory.Exists(ffmpegBinPath))
                Directory.Delete(ffmpegBinPath, true);

            await MessageDialog.ShowAsync(this, Ui.ErrorFFmpeg(ex.Message), Ui.TitleError);
            UpdateStatus(Ui.ErrorWithDetails(ex.Message));
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
                UpdateStatus(Ui.StatusDenoSkipped);
                return;
            }

            UpdateStatus(Ui.StatusCheckingDenoVersion);
            var (_, latestVersion) = await GetLatestDenoInfo();

            if (string.IsNullOrEmpty(latestVersion))
                return;

            string currentVersion = "";
            if (File.Exists(denoVersionPath))
                currentVersion = await File.ReadAllTextAsync(denoVersionPath);

            if (string.IsNullOrEmpty(currentVersion) || currentVersion != latestVersion)
            {
                UpdateStatus(Ui.StatusDenoUpdateAvailable);
                await DownloadDeno();
            }
            else
            {
                UpdateStatus(Ui.StatusDenoUpToDate);
            }
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowAsync(this, Ui.ErrorDenoUpdate(ex.Message), Ui.TitleError);
        }
    }

    private async Task CheckAndUpdateFFmpeg()
    {
        try
        {
            string latestVersion;
            if (OperatingSystem.IsMacOS())
                (_, _, latestVersion) = await GetLatestFFmpegInfoMac();
            else
                (_, latestVersion) = await GetLatestFFmpegInfo();

            string currentVersion = "";

            if (File.Exists(ffmpegVersionPath))
                currentVersion = await File.ReadAllTextAsync(ffmpegVersionPath);

            if (string.IsNullOrEmpty(currentVersion) || currentVersion != latestVersion)
            {
                UpdateStatus(Ui.StatusFFmpegUpdateAvailable);

                if (Directory.Exists(ffmpegBinPath))
                    Directory.Delete(ffmpegBinPath, true);

                await DownloadFFmpeg();
            }
            else
            {
                UpdateStatus(Ui.StatusFFmpegUpToDate);
            }
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowAsync(this, Ui.ErrorUpdate(ex.Message), Ui.TitleError);
        }
    }

    private async Task AktualizujKomponenty_Click()
    {
        bool confirmed = await MessageDialog.ShowConfirmAsync(this, Ui.ConfirmUpdateComponents, Ui.TitleComponentsUpdate);

        if (confirmed)
        {
            SetBusy(true);

            UpdateStatus(Ui.StatusUpdatingYtDlp);
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
                    UpdateStatus(Ui.StatusYtDlpUpdated);
                }
                catch (Exception ex)
                {
                    await MessageDialog.ShowAsync(this, Ui.ErrorWithDetails(ex.Message), Ui.TitleError);
                }
            }

            await CheckAndUpdateDeno();
            await CheckAndUpdateFFmpeg();
            UpdateStatus(Ui.StatusComponentsUpdateDone);
            SetBusy(false);
        }
    }

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
            UpdateStatus(Ui.StatusUrlEmpty);
            return false;
        }

        string normalizedUrl = NormalizeUrl(url);

        if (!normalizedUrl.Contains("youtube.com") && !normalizedUrl.Contains("youtu.be"))
        {
            UpdateStatus(Ui.StatusYouTubeOnly);
            return false;
        }

        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uriResult) ||
            (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
        {
            UpdateStatus(Ui.StatusInvalidUrl);
            return false;
        }

        return true;
    }

    // ---- Download flow ----

    private string BuildYtDlpArguments()
    {
        int qualityIndex = Math.Max(CbQuality.SelectedIndex, 0);
        return YtDlpArguments.Build(
            CbContentType.SelectedIndex == 1,
            YtDlpArguments.QualityHeights[qualityIndex],
            CbFormat.SelectedItem?.ToString() ?? "mp4");
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

            string status = Ui.ProgressDownloading + ": " + percent.ToString("F1") + "%";
            if (!string.IsNullOrEmpty(downloadedSize))
                status += " " + Ui.ProgressSize + ": " + downloadedSize;
            if (!string.IsNullOrEmpty(speed))
                status += " " + Ui.ProgressSpeed + ": " + speed;
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
            await MessageDialog.ShowAsync(this, Ui.ErrorNoUrl, Ui.TitleError);
            return;
        }

        foreach (string url in rawUrls)
        {
            if (!ValidateUrl(url))
            {
                await MessageDialog.ShowAsync(this, Ui.ErrorInvalidLink(url), Ui.TitleError);
                return;
            }
        }

        if (!File.Exists(ytDlpPath))
        {
            await MessageDialog.ShowAsync(this, Ui.ErrorYtDlpUnavailable, Ui.TitleError);
            return;
        }

        if (!Directory.Exists(ffmpegBinPath))
        {
            await MessageDialog.ShowAsync(this, Ui.ErrorFFmpegUnavailable, Ui.TitleError);
            return;
        }

        string runtimePath = GetRuntimePath();
        if (string.IsNullOrEmpty(runtimePath))
        {
            await MessageDialog.ShowAsync(this, Ui.ErrorNoJsRuntime, Ui.TitleError);
            return;
        }

        SetBusy(true);
        SetUrlRowsEnabled(false);

        int successCount = 0;
        string downloadsDir = AppPaths.DownloadsDirectory;

        for (int i = 0; i < rawUrls.Count; i++)
        {
            ProgressBarDownload.Value = 0;

            string statusPrefix = rawUrls.Count > 1 ? $"[{i + 1}/{rawUrls.Count}] " : "";
            UpdateStatus(statusPrefix + Ui.StatusPreparing);

            bool success = await DownloadSingleUrlAsync(rawUrls[i], runtimePath, statusPrefix);
            if (success)
                successCount++;
        }

        SetBusy(false);
        SetUrlRowsEnabled(true);
        ProgressBarDownload.Value = 0;

        if (successCount == rawUrls.Count)
        {
            UpdateStatus(Ui.StatusDownloadFinished);
            await MessageDialog.ShowAsync(this, Ui.MessageDownloaded(rawUrls.Count > 1, downloadsDir), Ui.TitleSuccess);
        }
        else
        {
            UpdateStatus(Ui.StatusFinishedCount(successCount, rawUrls.Count));
            await MessageDialog.ShowAsync(this, Ui.MessageDownloadedCount(successCount, rawUrls.Count), Ui.TitleFinishedWithErrors);
        }
    }

    private async Task<bool> DownloadSingleUrlAsync(string rawUrl, string runtimePath, string statusPrefix)
    {
        try
        {
            string normalizedUrl = NormalizeUrl(rawUrl);
            string ytDlpArgs = BuildYtDlpArguments();
            string downloadsDir = AppPaths.DownloadsDirectory;
            string outputPattern = Path.Combine(downloadsDir, "%(title)s.%(ext)s");

            StringBuilder argBuilder = new StringBuilder();
            argBuilder.Append(YtDlpArguments.JsRuntime(runtimePath));
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
                UpdateStatus(statusPrefix + Ui.StatusDownloaded);
                return true;
            }
            else
            {
                string error = await process.StandardError.ReadToEndAsync();
                UpdateStatus(statusPrefix + Ui.StatusDownloadError);
                await MessageDialog.ShowAsync(this, Ui.ErrorDownloadFailed(rawUrl, error), Ui.TitleError);
                return false;
            }
        }
        catch (Exception ex)
        {
            UpdateStatus(statusPrefix + Ui.StatusFailed);
            await MessageDialog.ShowAsync(this, Ui.ErrorDownloadFailed(rawUrl, ex.Message), Ui.TitleError);
            return false;
        }
    }

    // ---- App self-update ----

    private async Task CheckForAppUpdate(bool silent)
    {
        string title = Ui.TitleAppUpdate;

        if (AppPaths.IsSnap)
        {
            if (!silent)
                await MessageDialog.ShowAsync(this, Ui.AppUpdateSnap, title);
            return;
        }

        int? localBuild = AppUpdater.GetLocalBuildNumber();
        if (localBuild == null)
        {
            if (!silent)
                await MessageDialog.ShowAsync(this, Ui.AppUpdateLocalBuild, title);
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
                await MessageDialog.ShowAsync(this, Ui.ErrorAppUpdateCheck(ex.Message), Ui.TitleError);
            return;
        }

        if (release.BuildNumber == null || release.BuildNumber <= localBuild)
        {
            if (!silent)
                await MessageDialog.ShowAsync(this, Ui.AppUpToDate(localBuild.Value), title);
            return;
        }

        if (release.Asset == null)
        {
            if (!silent)
                await MessageDialog.ShowAsync(this, Ui.AppUpdatePublishing, title);
            return;
        }

        // The user may have started a download while GitHub was being queried.
        if (!BtnDownload.IsEnabled)
            return;

        bool confirmed = await MessageDialog.ShowConfirmAsync(this,
            Ui.AppUpdateAvailable(release.BuildNumber.Value, localBuild.Value),
            title);
        if (!confirmed)
            return;

        if (!AppUpdater.CanWriteDirectory(appDirectory))
        {
            bool openPage = await MessageDialog.ShowConfirmAsync(this,
                Ui.AppUpdateNoWriteAccess(appDirectory),
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
            await MessageDialog.ShowAsync(this, Ui.ErrorNoAppPath, Ui.TitleError);
            return;
        }

        string downloadPath = OperatingSystem.IsMacOS()
            ? Path.Combine(appDirectory, AppUpdater.UpdateZipName)
            : exePath + AppUpdater.NewSuffix;

        // Windows/Linux (single-file): this process must not replace its own file -
        // a helper does that (and the restart) once we have exited, see
        // AppUpdater.StartSwapAfterExit.
        bool swapAfterExit = !OperatingSystem.IsMacOS();

        SetBusy(true);
        try
        {
            UpdateStatus(Ui.StatusDownloadingAppUpdate(release.BuildNumber.GetValueOrDefault()));
            await DownloadFileWithProgress(asset.DownloadUrl, downloadPath);

            if (!AppUpdater.VerifyDownload(downloadPath, asset))
                throw new InvalidDataException(Ui.ErrorDownloadCorrupt);

            if (swapAfterExit)
            {
                AppUpdater.StartSwapAfterExit(downloadPath, exePath);
            }
            else
            {
                // macOS ships the whole publish folder (not single-file), zipped.
                string extractDirectory = Path.Combine(appDirectory, AppUpdater.UpdateDirectoryName);
                AppPaths.TryDeleteDirectory(extractDirectory);
                ZipFile.ExtractToDirectory(downloadPath, extractDirectory);

                UpdateStatus(Ui.StatusInstallingAppUpdate);
                AppUpdater.ApplyUpdate(AppUpdater.BuildFileList(extractDirectory, appDirectory));
            }
        }
        catch (Exception ex)
        {
            AppUpdater.DeleteUpdateDownloads(appDirectory, exePath);
            SetBusy(false);
            UpdateStatus(Ui.StatusAppUpdateError(ex.Message));
            await MessageDialog.ShowAsync(this, Ui.ErrorAppUpdateFailed(ex.Message), Ui.TitleError);
            return;
        }

        UpdateStatus(Ui.StatusAppUpdatedRestarting);

        // The helper still needs <exe>.new.
        if (!swapAfterExit)
        {
            AppUpdater.DeleteUpdateDownloads(appDirectory, exePath);

            try
            {
                AppUpdater.Relaunch(exePath);
            }
            catch (Exception ex)
            {
                await MessageDialog.ShowAsync(this,
                    Ui.AppUpdatedRestartManually(ex.Message),
                    Ui.TitleAppUpdate);
            }
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
            await MessageDialog.ShowAsync(this, Ui.ErrorCannotOpenLink(ex.Message), Ui.TitleError);
        }
    }

    private async Task Informacje_Click()
    {
        var about = new AboutWindow();
        await about.ShowDialog(this);
    }
}
