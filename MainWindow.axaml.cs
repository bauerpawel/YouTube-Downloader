using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
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
    private bool isBusy;
    private bool componentsReady;
    private CancellationTokenSource? downloadCancellation;
    private Task? downloadTask;
    private bool closeAfterDownload;

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
        AppUpdater.CleanupLeftovers(appDirectory, AppUpdater.UpdateTargetPath);
        if (AppPaths.AppBundlePath is { } appBundlePath)
            AppUpdater.CleanupBundleLeftovers(appBundlePath, dataDirectory);

        InitializeComponent();

        CbContentType.SelectionChanged += ContentType_Changed;

        CbFormat.Items.Add("mp4");
        CbFormat.Items.Add("webm");
        CbFormat.Items.Add("mkv");
        CbFormat.SelectedIndex = 0;

        ApplyTexts();
        LblStatus.Text = Ui.StatusCheckingComponents;

        BtnAddUrl.Click += BtnAddUrl_Click;
        BtnDownload.Click += async (s, e) =>
        {
            if (isBusy || closeAfterDownload)
                return;
            downloadTask = BtnDownload_Click();
            try { await downloadTask; }
            finally { downloadTask = null; }
        };
        BtnCancelDownload.Click += (s, e) => CancelDownload();
        Closing += async (s, e) =>
        {
            if (downloadCancellation == null)
                return;
            e.Cancel = true;
            if (closeAfterDownload)
                return;
            closeAfterDownload = true;
            CancelDownload();
            if (downloadTask != null)
                await downloadTask;
            Close();
        };
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
        SetBusy(true);
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
        BtnCancelDownload.Content = Ui.ButtonCancelDownload;
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
        CbQuality.IsEnabled = CbContentType.IsEnabled && !isAudioOnly;
        CbFormat.IsEnabled = CbContentType.IsEnabled && !isAudioOnly;
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
        CbContentType.IsEnabled = enabled;
        CbQuality.IsEnabled = enabled && CbContentType.SelectedIndex != 1;
        CbFormat.IsEnabled = enabled && CbContentType.SelectedIndex != 1;
    }

    private void CancelDownload()
    {
        if (downloadCancellation == null || downloadCancellation.IsCancellationRequested)
            return;
        BtnCancelDownload.IsEnabled = false;
        UpdateStatus(Ui.StatusCancelling);
        downloadCancellation.Cancel();
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
        isBusy = busy;
        BtnDownload.IsEnabled = !busy && componentsReady;
        MiSprawdzAktualizacje.IsEnabled = !busy;
        MiAktualizujKomponenty.IsEnabled = !busy;
    }

    // ---- Dependency management ----

    private async void CheckAndDownloadComponents()
    {
        SetBusy(true);
        componentsReady = false;
        UpdateStatus(Ui.StatusCheckingComponents);
        try
        {
            if (string.IsNullOrEmpty(await GetRuntimePathAsync()))
                await DownloadDeno();
            if (!await ComponentHealth.CheckExecutableAsync(ytDlpPath, ComponentTool.YtDlp))
            {
                UpdateStatus(Ui.StatusDownloadingYtDlp);
                await DownloadYtDlp();
            }
            if (!await ComponentHealth.CheckFFmpegAsync(ffmpegBinPath))
            {
                UpdateStatus(Ui.StatusDownloadingFFmpeg);
                await DownloadFFmpeg();
            }

            await RefreshComponentReadinessAsync();
            if (componentsReady)
                AppPaths.CleanupLegacyFiles(appDirectory, dataDirectory);
        }
        catch (Exception ex)
        {
            componentsReady = false;
            UpdateStatus(Ui.StatusComponentsUnavailable);
            if (IsVisible)
                await MessageDialog.ShowAsync(this, Ui.ErrorWithDetails(ex.Message), Ui.TitleError);
        }
        finally
        {
            SetBusy(false);
        }

        try
        {
            if (IsVisible)
                await CheckForAppUpdate(silent: true);
        }
        catch (Exception)
        {
        }
    }

    private async Task<string> GetRuntimePathAsync(CancellationToken cancellationToken = default)
    {
        if (await ComponentHealth.CheckExecutableAsync(denoPath, ComponentTool.Deno, cancellationToken))
            return denoPath;
        if (await ComponentHealth.CheckExecutableAsync(nodeJsPath, ComponentTool.Node, cancellationToken))
            return nodeJsPath;
        string systemDeno = FindSystemDeno();
        return await ComponentHealth.CheckExecutableAsync(systemDeno, ComponentTool.Deno, cancellationToken) ? systemDeno : "";
    }

    private async Task<(ComponentReadiness Health, string Runtime)> CheckComponentReadinessAsync(
        CancellationToken cancellationToken = default)
    {
        string runtime = await GetRuntimePathAsync(cancellationToken);
        string extension = OperatingSystem.IsWindows() ? ".exe" : "";
        var checks = await Task.WhenAll(
            ComponentHealth.CheckExecutableAsync(ytDlpPath, ComponentTool.YtDlp, cancellationToken),
            ComponentHealth.CheckExecutableAsync(Path.Combine(ffmpegBinPath, "ffmpeg" + extension), ComponentTool.FFmpeg, cancellationToken),
            ComponentHealth.CheckExecutableAsync(Path.Combine(ffmpegBinPath, "ffprobe" + extension), ComponentTool.FFprobe, cancellationToken));
        return (new(checks[0], checks[1], checks[2], !string.IsNullOrEmpty(runtime)), runtime);
    }

    private async Task RefreshComponentReadinessAsync()
    {
        var (health, _) = await CheckComponentReadinessAsync();
        componentsReady = health.IsReady;
        UpdateStatus(componentsReady ? Ui.StatusAllComponentsReady : Ui.StatusComponentsUnavailable);
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

    private async Task<ComponentRelease> GetLatestDenoInfo()
    {
        string json = await GitHubApi.GetStringAsync(httpClient,
            "https://api.github.com/repos/denoland/deno/releases/latest");
        return ComponentRelease.Parse(json, GetDenoAssetName());
    }

    private async Task<bool> DownloadDeno(ComponentRelease? release = null)
    {
        try
        {
            UpdateStatus(Ui.StatusDownloadingDeno);
            release ??= await GetLatestDenoInfo();
            var asset = release.Assets[0];
            await ComponentInstaller.InstallFileAsync(denoPath, async stagedPath =>
            {
                string zipPath = stagedPath + ".zip";
                try
                {
                    await DownloadFileWithProgress(asset.DownloadUrl, zipPath, asset);
                    UpdateStatus(Ui.StatusExtractingDeno);
                    using var archive = ZipFile.OpenRead(zipPath);
                    string entryName = OperatingSystem.IsWindows() ? "deno.exe" : "deno";
                    var entry = archive.GetEntry(entryName)
                        ?? throw new InvalidDataException(Ui.ErrorDenoNotFound);
                    entry.ExtractToFile(stagedPath);
                    AppPaths.MakeExecutable(stagedPath);
                }
                finally
                {
                    AppPaths.TryDeleteFile(zipPath);
                }
            }, path => ComponentHealth.CheckExecutableAsync(path, ComponentTool.Deno),
                denoVersionPath, release.Version);
            UpdateStatus(Ui.StatusDenoInstalled);
            return true;
        }
        catch (Exception ex)
        {
            UpdateStatus(Ui.StatusDenoError(ex.Message));
            if (IsVisible)
                await MessageDialog.ShowAsync(this, Ui.WarningDenoFailed, Ui.TitleWarning);
            return false;
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

    private async Task<bool> DownloadYtDlp()
    {
        try
        {
            string json = await GitHubApi.GetStringAsync(httpClient,
                "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest");
            var release = ComponentRelease.Parse(json, GetYtDlpAssetName());
            var asset = release.Assets[0];
            await ComponentInstaller.InstallFileAsync(ytDlpPath, async stagedPath =>
            {
                await DownloadFileWithProgress(asset.DownloadUrl, stagedPath, asset);
                AppPaths.MakeExecutable(stagedPath);
            }, path => ComponentHealth.CheckExecutableAsync(path, ComponentTool.YtDlp));
            UpdateStatus(Ui.StatusYtDlpDownloaded);
            return true;
        }
        catch (Exception ex)
        {
            if (IsVisible)
                await MessageDialog.ShowAsync(this, Ui.ErrorYtDlpDownload(ex.Message), Ui.TitleError);
            return false;
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

    private async Task<ComponentRelease> GetLatestFFmpegInfo()
    {
        string json = await GitHubApi.GetStringAsync(httpClient,
            "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases");
        return ComponentRelease.ParseBuilds(json, IsMatchingFFmpegAsset);
    }

    private static string GetFFmpegMacAssetName(bool isFFprobe)
    {
        bool isArm = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        string arch = isArm ? "arm64" : "x64";
        return (isFFprobe ? "ffprobe-darwin-" : "ffmpeg-darwin-") + arch;
    }

    private async Task<ComponentRelease> GetLatestFFmpegInfoMac()
    {
        string json = await GitHubApi.GetStringAsync(httpClient,
            "https://api.github.com/repos/eugeneware/ffmpeg-static/releases/latest");
        return ComponentRelease.Parse(json,
            GetFFmpegMacAssetName(isFFprobe: false), GetFFmpegMacAssetName(isFFprobe: true));
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
            CreateNoWindow = true
        };
        processInfo.ArgumentList.Add("-xf");
        processInfo.ArgumentList.Add(archivePath);
        processInfo.ArgumentList.Add("-C");
        processInfo.ArgumentList.Add(destinationPath);

        var (exitCode, error) = await ProcessRunner.RunAsync(processInfo);
        if (exitCode != 0)
            throw new Exception(Ui.ErrorTarFailed(error));
    }

    private async Task<bool> DownloadFFmpeg(ComponentRelease? release = null)
    {
        try
        {
            UpdateStatus(Ui.StatusFetchingFFmpegInfo);
            release ??= OperatingSystem.IsMacOS()
                ? await GetLatestFFmpegInfoMac() : await GetLatestFFmpegInfo();
            UpdateStatus(Ui.StatusDownloadingFFmpegVersion(release.Version));
            var assets = release.Assets;
            await ComponentInstaller.InstallDirectoryAsync(ffmpegBinPath, async stagedDirectory =>
            {
                if (OperatingSystem.IsMacOS())
                {
                    for (int i = 0; i < assets.Count; i++)
                    {
                        string path = Path.Combine(stagedDirectory, i == 0 ? "ffmpeg" : "ffprobe");
                        await DownloadFileWithProgress(assets[i].DownloadUrl, path, assets[i]);
                        AppPaths.MakeExecutable(path);
                    }
                }
                else
                {
                    string extension = OperatingSystem.IsWindows() ? ".zip" : ".tar.xz";
                    string archivePath = Path.Combine(stagedDirectory, "ffmpeg" + extension);
                    string extractPath = Path.Combine(stagedDirectory, "extracted");
                    await DownloadFileWithProgress(assets[0].DownloadUrl, archivePath, assets[0]);
                    UpdateStatus(Ui.StatusExtractingFFmpeg);
                    await ExtractArchive(archivePath, extractPath);
                    var bins = Directory.GetDirectories(extractPath, "bin", SearchOption.AllDirectories);
                    if (bins.Length != 1)
                        throw new InvalidDataException(Ui.ErrorFFmpegBinNotFound);
                    foreach (string file in Directory.GetFiles(bins[0]))
                    {
                        string path = Path.Combine(stagedDirectory, Path.GetFileName(file));
                        File.Copy(file, path);
                        AppPaths.MakeExecutable(path);
                    }
                    File.Delete(archivePath);
                    Directory.Delete(extractPath, true);
                }
            }, ComponentHealth.CheckFFmpegAsync, ffmpegVersionPath, release.Version);
            UpdateStatus(Ui.StatusFFmpegDownloaded(release.Version));
            return true;
        }
        catch (Exception ex)
        {
            if (IsVisible)
                await MessageDialog.ShowAsync(this, Ui.ErrorFFmpeg(ex.Message), Ui.TitleError);
            UpdateStatus(Ui.ErrorWithDetails(ex.Message));
            return false;
        }
    }

    private async Task DownloadFileWithProgress(string url, string destinationPath, ReleaseAsset? asset = null)
    {
        try
        {
            await AtomicDownload.DownloadAsync(httpClient, url, destinationPath,
                progress => ProgressBarDownload.Value = progress, asset?.Size, asset?.Sha256);
        }
        finally
        {
            ProgressBarDownload.Value = 0;
        }
    }

    private async Task<bool> CheckAndUpdateDeno()
    {
        try
        {
            if (!File.Exists(denoPath) && !string.IsNullOrEmpty(await GetRuntimePathAsync()))
            {
                UpdateStatus(Ui.StatusDenoSkipped);
                return true;
            }
            UpdateStatus(Ui.StatusCheckingDenoVersion);
            var release = await GetLatestDenoInfo();
            string current = File.Exists(denoVersionPath) ? (await File.ReadAllTextAsync(denoVersionPath)).Trim() : "";
            if (current == release.Version && await ComponentHealth.CheckExecutableAsync(denoPath, ComponentTool.Deno))
            {
                UpdateStatus(Ui.StatusDenoUpToDate);
                return true;
            }
            UpdateStatus(Ui.StatusDenoUpdateAvailable);
            return await DownloadDeno(release);
        }
        catch (Exception ex)
        {
            if (IsVisible)
                await MessageDialog.ShowAsync(this, Ui.ErrorDenoUpdate(ex.Message), Ui.TitleError);
            return false;
        }
    }

    private async Task<bool> CheckAndUpdateFFmpeg()
    {
        try
        {
            // Fetch and validate metadata before preparing anything. A failed API
            // request must never turn into an empty "new version" or a deletion.
            var release = OperatingSystem.IsMacOS()
                ? await GetLatestFFmpegInfoMac() : await GetLatestFFmpegInfo();
            string current = File.Exists(ffmpegVersionPath) ? (await File.ReadAllTextAsync(ffmpegVersionPath)).Trim() : "";
            if (current == release.Version && await ComponentHealth.CheckFFmpegAsync(ffmpegBinPath))
            {
                UpdateStatus(Ui.StatusFFmpegUpToDate);
                return true;
            }
            UpdateStatus(Ui.StatusFFmpegUpdateAvailable);
            return await DownloadFFmpeg(release);
        }
        catch (Exception ex)
        {
            if (IsVisible)
                await MessageDialog.ShowAsync(this, Ui.ErrorUpdate(ex.Message), Ui.TitleError);
            return false;
        }
    }

    private async Task AktualizujKomponenty_Click()
    {
        bool confirmed = await MessageDialog.ShowConfirmAsync(this, Ui.ConfirmUpdateComponents, Ui.TitleComponentsUpdate);
        if (!confirmed || isBusy)
            return;

        SetBusy(true);
        try
        {
            UpdateStatus(Ui.StatusUpdatingYtDlp);
            bool ytDlpUpdated = await DownloadYtDlp();
            if (ytDlpUpdated)
                UpdateStatus(Ui.StatusYtDlpUpdated);
            bool denoUpdated = await CheckAndUpdateDeno();
            bool ffmpegUpdated = await CheckAndUpdateFFmpeg();
            await RefreshComponentReadinessAsync();
            bool succeeded = ytDlpUpdated && denoUpdated && ffmpegUpdated && componentsReady;
            UpdateStatus(succeeded ? Ui.StatusComponentsUpdateDone : Ui.StatusComponentsUpdateFailed +
                (componentsReady ? "" : " " + Ui.StatusComponentsUnavailable));
        }
        catch (Exception ex)
        {
            UpdateStatus(Ui.StatusComponentsUpdateFailed);
            if (IsVisible)
                await MessageDialog.ShowAsync(this, Ui.ErrorWithDetails(ex.Message), Ui.TitleError);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ---- URL handling ----

    private bool ValidateUrl(string url)
    {
        if (!YouTubeUrl.TryNormalize(url, out _, out var error))
        {
            UpdateStatus(error switch
            {
                YouTubeUrlError.Empty => Ui.StatusUrlEmpty,
                YouTubeUrlError.UnsupportedHost => Ui.StatusYouTubeOnly,
                _ => Ui.StatusInvalidUrl
            });
            return false;
        }

        return true;
    }

    // ---- Download flow ----

    private ProcessStartInfo BuildYtDlpStartInfo(string normalizedUrl, string runtimePath, string downloadsDir)
    {
        int qualityIndex = Math.Max(CbQuality.SelectedIndex, 0);
        return YtDlpArguments.CreateStartInfo(
            ytDlpPath, runtimePath, ffmpegBinPath,
            Path.Combine(downloadsDir, "%(title)s.%(ext)s"), normalizedUrl,
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
        if (isBusy || closeAfterDownload)
            return;
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

        using var cancellation = new CancellationTokenSource();
        downloadCancellation = cancellation;
        BtnCancelDownload.IsVisible = true;
        BtnCancelDownload.IsEnabled = true;
        SetBusy(true);
        SetUrlRowsEnabled(false);
        try
        {
            var (health, runtimePath) = await CheckComponentReadinessAsync(cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            componentsReady = health.IsReady;
            if (!componentsReady)
            {
                UpdateStatus(Ui.StatusComponentsUnavailable);
                string message = !health.YtDlp ? Ui.ErrorYtDlpUnavailable
                    : !health.FFmpeg || !health.FFprobe ? Ui.ErrorFFmpegUnavailable : Ui.ErrorNoJsRuntime;
                await MessageDialog.ShowAsync(this, message, Ui.TitleError);
                return;
            }

            string downloadsDir = AppPaths.DownloadsDirectory;
            var result = await DownloadBatch.RunAsync(rawUrls, async (url, i, token) =>
            {
                ProgressBarDownload.Value = 0;
                string statusPrefix = rawUrls.Count > 1 ? $"[{i + 1}/{rawUrls.Count}] " : "";
                UpdateStatus(statusPrefix + Ui.StatusPreparing);
                return await DownloadSingleUrlAsync(url, runtimePath, statusPrefix, token);
            }, cancellation.Token);

            if (result.Cancelled)
            {
                UpdateStatus(Ui.StatusDownloadCancelled(result.Completed, rawUrls.Count));
            }
            else if (result.Completed == rawUrls.Count)
            {
                UpdateStatus(Ui.StatusDownloadFinished);
                await MessageDialog.ShowAsync(this, Ui.MessageDownloaded(rawUrls.Count > 1, downloadsDir), Ui.TitleSuccess);
            }
            else
            {
                UpdateStatus(Ui.StatusFinishedCount(result.Completed, rawUrls.Count));
                await MessageDialog.ShowAsync(this, Ui.MessageDownloadedCount(result.Completed, rawUrls.Count), Ui.TitleFinishedWithErrors);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            UpdateStatus(Ui.StatusDownloadCancelled(0, rawUrls.Count));
        }
        catch (Exception ex)
        {
            UpdateStatus(Ui.StatusFailed);
            await MessageDialog.ShowAsync(this, Ui.ErrorWithDetails(ex.Message), Ui.TitleError);
        }
        finally
        {
            downloadCancellation = null;
            BtnCancelDownload.IsVisible = false;
            SetBusy(false);
            SetUrlRowsEnabled(true);
            ProgressBarDownload.Value = 0;
        }
    }

    private async Task<bool> DownloadSingleUrlAsync(string rawUrl, string runtimePath, string statusPrefix,
        CancellationToken cancellationToken)
    {
        bool reportProgress = true;
        try
        {
            if (!YouTubeUrl.TryNormalize(rawUrl, out string normalizedUrl, out _))
                throw new ArgumentException(Ui.ErrorInvalidLink(rawUrl));

            string downloadsDir = AppPaths.DownloadsDirectory;
            var processInfo = BuildYtDlpStartInfo(normalizedUrl, runtimePath, downloadsDir);

            Directory.CreateDirectory(downloadsDir);

            var (exitCode, error) = await ProcessRunner.RunAsync(processInfo, data =>
            {
                if (data.Length == 0)
                    return;

                Dispatcher.UIThread.Post(() =>
                {
                    if (!reportProgress || cancellationToken.IsCancellationRequested)
                        return;
                    if (data.Contains("[download]"))
                    {
                        ParseDownloadProgress(data, statusPrefix);
                    }
                    else if (data.Contains("[info]") || data.Contains("Downloading"))
                    {
                        LblStatus.Text = statusPrefix + data;
                    }
                });
            }, cancellationToken);

            if (exitCode == 0)
            {
                ProgressBarDownload.Value = 100;
                UpdateStatus(statusPrefix + Ui.StatusDownloaded);
                return true;
            }
            else
            {
                UpdateStatus(statusPrefix + Ui.StatusDownloadError);
                await MessageDialog.ShowAsync(this, Ui.ErrorDownloadFailed(rawUrl, error), Ui.TitleError);
                return false;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            UpdateStatus(statusPrefix + Ui.StatusFailed);
            await MessageDialog.ShowAsync(this, Ui.ErrorDownloadFailed(rawUrl, ex.Message), Ui.TitleError);
            return false;
        }
        finally
        {
            reportProgress = false;
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

        if (isBusy)
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
        if (isBusy)
            return;

        bool confirmed = await MessageDialog.ShowConfirmAsync(this,
            Ui.AppUpdateAvailable(release.BuildNumber.Value, localBuild.Value),
            title);
        if (!confirmed)
            return;

        // A macOS .app bundle and an AppImage are replaced from the folder holding
        // them (/Applications, ~/Applications) - not writable for a bundle straight
        // from the mounted .dmg or translocated by Gatekeeper, or an AppImage in /opt.
        string installDirectory = AppUpdater.GetInstallDirectory(appDirectory, AppPaths.AppBundlePath, AppPaths.AppImagePath);
        if (!AppUpdater.CanWriteDirectory(installDirectory))
        {
            bool openPage = await MessageDialog.ShowConfirmAsync(this,
                Ui.AppUpdateNoWriteAccess(installDirectory),
                title);
            if (openPage)
                await OpenUrl(release.HtmlUrl);
            return;
        }

        await InstallAppUpdate(release, release.Asset);
    }

    private async Task InstallAppUpdate(ReleaseInfo release, ReleaseAsset asset)
    {
        // The .AppImage itself when started from one - see AppUpdater.UpdateTargetPath.
        string? exePath = AppUpdater.UpdateTargetPath;
        if (string.IsNullOrEmpty(exePath))
        {
            await MessageDialog.ShowAsync(this, Ui.ErrorNoAppPath, Ui.TitleError);
            return;
        }

        string? bundlePath = AppPaths.AppBundlePath;
        string downloadPath = bundlePath != null ? Path.Combine(dataDirectory, AppUpdater.UpdateZipName)
            : OperatingSystem.IsMacOS() ? Path.Combine(appDirectory, AppUpdater.UpdateZipName)
            : exePath + AppUpdater.NewSuffix;

        // Windows/Linux (single-file) and the macOS .app bundle: this process must
        // not replace its own files - a helper does that (and the restart) once we
        // have exited, see AppUpdater.StartSwapAfterExit/StartBundleSwapAfterExit.
        bool swapAfterExit = !OperatingSystem.IsMacOS() || bundlePath != null;

        SetBusy(true);
        try
        {
            UpdateStatus(Ui.StatusDownloadingAppUpdate(release.BuildNumber.GetValueOrDefault()));
            await DownloadFileWithProgress(asset.DownloadUrl, downloadPath);

            if (!AppUpdater.VerifyDownload(downloadPath, asset))
                throw new InvalidDataException(Ui.ErrorDownloadCorrupt);

            if (bundlePath != null)
            {
                UpdateStatus(Ui.StatusInstallingAppUpdate);
                string extractDirectory = Path.Combine(dataDirectory, AppUpdater.UpdateDirectoryName);
                AppPaths.TryDeleteDirectory(extractDirectory);
                var (exitCode, error) = await AppUpdater.ExtractBundleZipAsync(downloadPath, extractDirectory);
                if (exitCode != 0)
                    throw new IOException(Ui.ErrorDittoFailed(error.Trim()));
                AppPaths.TryDeleteFile(downloadPath);

                string newBundle = AppUpdater.FindExtractedBundle(extractDirectory)
                    ?? throw new InvalidDataException(Ui.ErrorDownloadCorrupt);
                AppUpdater.StartBundleSwapAfterExit(newBundle, bundlePath);
            }
            else if (swapAfterExit)
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
            if (bundlePath != null)
                AppUpdater.DeleteBundleUpdateDownloads(dataDirectory);
            SetBusy(false);
            UpdateStatus(Ui.StatusAppUpdateError(ex.Message));
            await MessageDialog.ShowAsync(this, Ui.ErrorAppUpdateFailed(ex.Message), Ui.TitleError);
            return;
        }

        UpdateStatus(Ui.StatusAppUpdatedRestarting);

        // The helper still needs <exe>.new (or the extracted bundle).
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
