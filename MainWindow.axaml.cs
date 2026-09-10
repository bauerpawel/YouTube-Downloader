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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

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
        ytDlpPath = Path.Combine(appDirectory, OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp");
        ffmpegBinPath = Path.Combine(appDirectory, "ffmpeg_bin");
        denoPath = Path.Combine(appDirectory, OperatingSystem.IsWindows() ? "deno.exe" : "deno");
        denoVersionPath = Path.Combine(appDirectory, "deno_version.txt");
        nodeJsPath = Path.Combine(appDirectory, OperatingSystem.IsWindows() ? "node.exe" : "node");

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
        BtnDownload.Click += async (s, e) => await BtnDownload_Click();
        MiAktualizujKomponenty.Click += async (s, e) => await AktualizujKomponenty_Click();
        MiInformacje.Click += async (s, e) => await Informacje_Click();

        MiThemeLight.Click += (s, e) => SetTheme("Light");
        MiThemeDark.Click += (s, e) => SetTheme("Dark");
        MiThemeSystem.Click += (s, e) => SetTheme("Default");

        string currentTheme = ThemeSettings.Load(appDirectory);
        MiThemeLight.IsChecked = currentTheme == "Light";
        MiThemeDark.IsChecked = currentTheme == "Dark";
        MiThemeSystem.IsChecked = currentTheme == "Default";

        Opened += (s, e) => CheckAndDownloadComponents();
    }

    private void SetTheme(string name)
    {
        Application.Current!.RequestedThemeVariant = ThemeSettings.ToVariant(name);
        ThemeSettings.Save(appDirectory, name);
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

    private static void MakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        File.SetUnixFileMode(path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
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
            string denoExeName = OperatingSystem.IsWindows() ? "deno.exe" : "deno";
            string jsRuntimeArg = runtimePath.EndsWith(denoExeName, StringComparison.OrdinalIgnoreCase) ? "" : "--js-runtimes node";
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

    private async Task Informacje_Click()
    {
        var about = new AboutWindow();
        await about.ShowDialog(this);
    }
}
