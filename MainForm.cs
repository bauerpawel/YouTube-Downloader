using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YouTubeDownloader
{
    public partial class MainForm : Form
    {
        private TextBox? txtUrl;
        private Button? btnAddUrl;
        private Button? btnDownload;
        private ProgressBar? progressBar;
        private Label? lblStatus;
        private Label? lblContentType;
        private Label? lblQuality;
        private Label? lblFormat;
        private MenuStrip? menuStrip;
        private ComboBox? cbQuality;
        private ComboBox? cbFormat;
        private ComboBox? cbContentType;
        private readonly List<TextBox> extraUrlBoxes = new List<TextBox>();
        private readonly List<Button> removeUrlButtons = new List<Button>();
        private readonly HttpClient httpClient;
        private readonly string appDirectory;
        private readonly string ytDlpPath;
        private readonly string ffmpegBinPath;
        private readonly string denoPath;
        private readonly string denoVersionPath;
        private readonly string nodeJsPath;

        public MainForm()
        {
            httpClient = new HttpClient();
            appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            ytDlpPath = Path.Combine(appDirectory, "yt-dlp.exe");
            ffmpegBinPath = Path.Combine(appDirectory, "ffmpeg_bin");
            denoPath = Path.Combine(appDirectory, "deno.exe");
            denoVersionPath = Path.Combine(appDirectory, "deno_version.txt");
            nodeJsPath = Path.Combine(appDirectory, "node.exe");

            InitializeComponent();
            CheckAndDownloadComponents();
        }

        private void InitializeComponent()
        {
            this.Text = "YouTube Downloader";
            this.Size = new System.Drawing.Size(800, 500);
            this.StartPosition = FormStartPosition.CenterScreen;

            var iconStream = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("YouTubeDownloader.app.ico");
            if (iconStream != null)
                this.Icon = new System.Drawing.Icon(iconStream);

            menuStrip = new MenuStrip();
            ToolStripMenuItem narzedziaMenu = new ToolStripMenuItem("&Narzedzia");
            ToolStripMenuItem aktualizujKomponentyItem = new ToolStripMenuItem("&Aktualizuj komponenty", null, AktualizujKomponenty_Click);
            narzedziaMenu.DropDownItems.Add(aktualizujKomponentyItem);
            menuStrip.Items.Add(narzedziaMenu);

            ToolStripMenuItem pomocMenu = new ToolStripMenuItem("&Pomoc");
            ToolStripMenuItem informacjeItem = new ToolStripMenuItem("&Informacje", null, Informacje_Click);
            pomocMenu.DropDownItems.Add(informacjeItem);
            menuStrip.Items.Add(pomocMenu);

            Label lblUrl = new Label();
            lblUrl.Text = "Link do filmu:";
            lblUrl.Location = new System.Drawing.Point(20, 40);
            lblUrl.AutoSize = true;

            txtUrl = new TextBox();
            txtUrl.Location = new System.Drawing.Point(20, 65);
            txtUrl.Size = new System.Drawing.Size(700, 25);
            txtUrl.Text = "https://www.youtube.com/watch?v=";

            btnAddUrl = new Button();
            btnAddUrl.Text = "+";
            btnAddUrl.Location = new System.Drawing.Point(730, 65);
            btnAddUrl.Size = new System.Drawing.Size(40, 25);
            btnAddUrl.Click += BtnAddUrl_Click;

            lblContentType = new Label();
            lblContentType.Text = "Typ:";
            lblContentType.Location = new System.Drawing.Point(20, 100);
            lblContentType.AutoSize = true;

            cbContentType = new ComboBox();
            cbContentType.Location = new System.Drawing.Point(20, 125);
            cbContentType.Size = new System.Drawing.Size(150, 25);
            cbContentType.DropDownStyle = ComboBoxStyle.DropDownList;
            cbContentType.Items.AddRange(new object[] { "Wideo + Audio", "Tylko Audio (MP3)" });
            cbContentType.SelectedIndex = 0;
            cbContentType.SelectedIndexChanged += ContentType_Changed;

            lblQuality = new Label();
            lblQuality.Text = "Jakosc:";
            lblQuality.Location = new System.Drawing.Point(200, 100);
            lblQuality.AutoSize = true;

            cbQuality = new ComboBox();
            cbQuality.Location = new System.Drawing.Point(200, 125);
            cbQuality.Size = new System.Drawing.Size(150, 25);
            cbQuality.DropDownStyle = ComboBoxStyle.DropDownList;
            cbQuality.Items.AddRange(new object[] { "Najlepsza", "4K (2160p)", "1080p", "720p", "480p", "360p", "240p" });
            cbQuality.SelectedIndex = 0;

            lblFormat = new Label();
            lblFormat.Text = "Format:";
            lblFormat.Location = new System.Drawing.Point(380, 100);
            lblFormat.AutoSize = true;

            cbFormat = new ComboBox();
            cbFormat.Location = new System.Drawing.Point(380, 125);
            cbFormat.Size = new System.Drawing.Size(150, 25);
            cbFormat.DropDownStyle = ComboBoxStyle.DropDownList;
            cbFormat.Items.AddRange(new object[] { "mp4", "webm", "mkv" });
            cbFormat.SelectedIndex = 0;

            Button btnDownload = new Button();
            btnDownload.Text = "Pobierz";
            btnDownload.Location = new System.Drawing.Point(560, 125);
            btnDownload.Size = new System.Drawing.Size(100, 30);
            btnDownload.Click += BtnDownload_Click;
            this.btnDownload = btnDownload;

            progressBar = new ProgressBar();
            progressBar.Location = new System.Drawing.Point(20, 170);
            progressBar.Size = new System.Drawing.Size(750, 30);
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;

            lblStatus = new Label();
            lblStatus.Location = new System.Drawing.Point(20, 210);
            lblStatus.Size = new System.Drawing.Size(750, 250);
            lblStatus.Text = "Gotowy do pobierania";
            lblStatus.AutoSize = false;
            lblStatus.BorderStyle = BorderStyle.FixedSingle;
            lblStatus.Font = new System.Drawing.Font("Courier New", 10);

            this.Controls.Add(menuStrip);
            this.Controls.Add(lblUrl);
            this.Controls.Add(txtUrl);
            this.Controls.Add(btnAddUrl);
            this.Controls.Add(lblContentType);
            this.Controls.Add(cbContentType);
            this.Controls.Add(lblQuality);
            this.Controls.Add(cbQuality);
            this.Controls.Add(lblFormat);
            this.Controls.Add(cbFormat);
            this.Controls.Add(btnDownload);
            this.Controls.Add(progressBar);
            this.Controls.Add(lblStatus);
        }

        private void ContentType_Changed(object? sender, EventArgs e)
        {
            if (cbContentType?.SelectedIndex == 1)
            {
                if (cbQuality != null) cbQuality.Enabled = false;
                if (cbFormat != null) cbFormat.Enabled = false;
            }
            else
            {
                if (cbQuality != null) cbQuality.Enabled = true;
                if (cbFormat != null) cbFormat.Enabled = true;
            }
        }

        private void BtnAddUrl_Click(object? sender, EventArgs e)
        {
            TextBox newUrlBox = new TextBox();
            newUrlBox.Size = new System.Drawing.Size(700, 25);
            newUrlBox.PlaceholderText = "Wklej kolejny link do filmu...";

            Button removeBtn = new Button();
            removeBtn.Text = "-";
            removeBtn.Size = new System.Drawing.Size(40, 25);
            removeBtn.Click += (s, args) => RemoveUrlRow(newUrlBox, removeBtn);

            extraUrlBoxes.Add(newUrlBox);
            removeUrlButtons.Add(removeBtn);

            this.Controls.Add(newUrlBox);
            this.Controls.Add(removeBtn);

            RelayoutForm();
        }

        private void RemoveUrlRow(TextBox box, Button button)
        {
            extraUrlBoxes.Remove(box);
            removeUrlButtons.Remove(button);
            this.Controls.Remove(box);
            this.Controls.Remove(button);
            box.Dispose();
            button.Dispose();

            RelayoutForm();
        }

        private void RelayoutForm()
        {
            const int rowHeight = 30;
            int y = 95;
            for (int i = 0; i < extraUrlBoxes.Count; i++)
            {
                extraUrlBoxes[i].Location = new System.Drawing.Point(20, y);
                removeUrlButtons[i].Location = new System.Drawing.Point(730, y);
                y += rowHeight;
            }

            int offset = extraUrlBoxes.Count * rowHeight;

            if (lblContentType != null) lblContentType.Location = new System.Drawing.Point(20, 100 + offset);
            if (cbContentType != null) cbContentType.Location = new System.Drawing.Point(20, 125 + offset);
            if (lblQuality != null) lblQuality.Location = new System.Drawing.Point(200, 100 + offset);
            if (cbQuality != null) cbQuality.Location = new System.Drawing.Point(200, 125 + offset);
            if (lblFormat != null) lblFormat.Location = new System.Drawing.Point(380, 100 + offset);
            if (cbFormat != null) cbFormat.Location = new System.Drawing.Point(380, 125 + offset);
            if (btnDownload != null) btnDownload.Location = new System.Drawing.Point(560, 125 + offset);
            if (progressBar != null) progressBar.Location = new System.Drawing.Point(20, 170 + offset);
            if (lblStatus != null) lblStatus.Location = new System.Drawing.Point(20, 210 + offset);

            this.Size = new System.Drawing.Size(this.Size.Width, 500 + offset);
        }

        private void SetUrlRowsEnabled(bool enabled)
        {
            if (txtUrl != null) txtUrl.Enabled = enabled;
            if (btnAddUrl != null) btnAddUrl.Enabled = enabled;
            foreach (var box in extraUrlBoxes) box.Enabled = enabled;
            foreach (var btn in removeUrlButtons) btn.Enabled = enabled;
        }

        private List<string> GetAllUrls()
        {
            List<string> urls = new List<string>();
            if (txtUrl != null)
                urls.Add(txtUrl.Text.Trim());
            foreach (var box in extraUrlBoxes)
                urls.Add(box.Text.Trim());

            return urls.Where(u => !string.IsNullOrWhiteSpace(u)).ToList();
        }

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

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        process.WaitForExit(2000);
                        return process.ExitCode == 0;
                    }
                }
            }
            catch { }
            return false;
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
                MessageBox.Show("Blad pobierania informacji Deno: " + ex.Message, "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    MessageBox.Show("Nie znaleziono Deno dla Windows", "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show("Nie udalo sie pobrac Deno. Zainstaluj z https://deno.com", "Ostrzezenie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show("Blad pobierania yt-dlp: " + ex.Message, "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show("Blad pobierania FFmpeg info: " + ex.Message, "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

                string[] binPaths = Directory.GetDirectories(tempExtractPath, "bin", System.IO.SearchOption.AllDirectories);
                
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
                MessageBox.Show("Blad FFmpeg: " + ex.Message, "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatus("Blad: " + ex.Message);
            }
        }

        private async Task DownloadFileWithProgress(string url, string destinationPath)
        {
            using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
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
                        
                        if (canReportProgress && progressBar != null)
                        {
                            var progress = (int)((totalBytesRead * 100) / totalBytes);
                            progressBar.Value = Math.Min(progress, 100);
                        }
                    }
                }
                
                if (progressBar != null)
                    progressBar.Value = 0;
            }
        }

        private void UpdateStatus(string message)
        {
            if (lblStatus != null)
                lblStatus.Text = message;
            Application.DoEvents();
        }

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

        private void Informacje_Click(object? sender, EventArgs e)
        {
            using (Form aboutForm = new Form())
            {
                aboutForm.Text = "Informacje o programie";
                aboutForm.ClientSize = new System.Drawing.Size(400, 310);
                aboutForm.StartPosition = FormStartPosition.CenterParent;
                aboutForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                aboutForm.MaximizeBox = false;
                aboutForm.MinimizeBox = false;
                aboutForm.Icon = this.Icon;

                PictureBox pictureBox = new PictureBox();
                pictureBox.Size = new System.Drawing.Size(64, 64);
                pictureBox.Location = new System.Drawing.Point(168, 20);
                pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                pictureBox.Image = this.Icon?.ToBitmap();

                Label lblName = new Label();
                lblName.Text = "YouTube Downloader";
                lblName.Font = new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Bold);
                lblName.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
                lblName.Location = new System.Drawing.Point(0, 95);
                lblName.Size = new System.Drawing.Size(400, 30);

                Label lblVersion = new Label();
                lblVersion.Text = "Wersja 1.0.0";
                lblVersion.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
                lblVersion.Location = new System.Drawing.Point(0, 130);
                lblVersion.Size = new System.Drawing.Size(400, 20);

                Label lblLicense = new Label();
                lblLicense.Text = "Licencja: Apache License 2.0";
                lblLicense.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
                lblLicense.Location = new System.Drawing.Point(0, 155);
                lblLicense.Size = new System.Drawing.Size(400, 20);

                LinkLabel lnkRepo = new LinkLabel();
                lnkRepo.Text = "Repozytorium: github.com/bauerpawel/YouTube-Downloader";
                lnkRepo.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
                lnkRepo.Location = new System.Drawing.Point(0, 190);
                lnkRepo.Size = new System.Drawing.Size(400, 20);
                string repoUrlText = "github.com/bauerpawel/YouTube-Downloader";
                lnkRepo.LinkArea = new LinkArea(lnkRepo.Text.IndexOf(repoUrlText, StringComparison.Ordinal), repoUrlText.Length);
                lnkRepo.LinkClicked += (s, args) => OpenUrl("https://github.com/bauerpawel/YouTube-Downloader");

                LinkLabel lnkAuthor = new LinkLabel();
                lnkAuthor.Text = "Autor: bauer.net.pl";
                lnkAuthor.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
                lnkAuthor.Location = new System.Drawing.Point(0, 215);
                lnkAuthor.Size = new System.Drawing.Size(400, 20);
                string authorUrlText = "bauer.net.pl";
                lnkAuthor.LinkArea = new LinkArea(lnkAuthor.Text.IndexOf(authorUrlText, StringComparison.Ordinal), authorUrlText.Length);
                lnkAuthor.LinkClicked += (s, args) => OpenUrl("https://bauer.net.pl");

                Button btnClose = new Button();
                btnClose.Text = "Zamknij";
                btnClose.Size = new System.Drawing.Size(100, 30);
                btnClose.Location = new System.Drawing.Point(150, 255);
                btnClose.DialogResult = DialogResult.OK;

                aboutForm.Controls.Add(pictureBox);
                aboutForm.Controls.Add(lblName);
                aboutForm.Controls.Add(lblVersion);
                aboutForm.Controls.Add(lblLicense);
                aboutForm.Controls.Add(lnkRepo);
                aboutForm.Controls.Add(lnkAuthor);
                aboutForm.Controls.Add(btnClose);
                aboutForm.AcceptButton = btnClose;

                aboutForm.ShowDialog(this);
            }
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie mozna otworzyc linku: " + ex.Message, "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void AktualizujKomponenty_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show("Zaktualizowac komponenty?", "Aktualizacja", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            
            if (result == DialogResult.Yes)
            {
                if (btnDownload != null)
                    btnDownload.Enabled = false;
                
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
                        MessageBox.Show("Blad: " + ex.Message, "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                
                await CheckAndUpdateDeno();
                await CheckAndUpdateFFmpeg();
                UpdateStatus("Aktualizacja zakonczena");
                if (btnDownload != null)
                    btnDownload.Enabled = true;
            }
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
                MessageBox.Show("Blad aktualizacji Deno: " + ex.Message, "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show("Blad aktualizacji: " + ex.Message, "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

                using (var process = Process.Start(processInfo))
                {
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
            }
            catch { }

            return "";
        }

        private void ParseDownloadProgress(string line, ProgressBar? progressBar, Label? statusLabel, string statusPrefix = "")
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
                    if (progressBar != null)
                        progressBar.Value = Math.Min((int)percent, 100);
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

                if (statusLabel != null)
                    statusLabel.Text = statusPrefix + status;
            }
            catch { }
        }

        private string BuildYtDlpArguments()
        {
            string args = "";
            
            if (cbContentType?.SelectedIndex == 1)
            {
                args = " -f bestaudio --extract-audio --audio-format mp3 --audio-quality 192";
            }
            else
            {
                string quality = cbQuality?.SelectedItem?.ToString() ?? "Najlepsza";
                string format = cbFormat?.SelectedItem?.ToString() ?? "mp4";
                
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

        private async void BtnDownload_Click(object? sender, EventArgs e)
        {
            List<string> rawUrls = GetAllUrls();

            if (rawUrls.Count == 0)
            {
                MessageBox.Show("Podaj przynajmniej jeden link", "Blad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            foreach (string url in rawUrls)
            {
                if (!ValidateUrl(url))
                {
                    MessageBox.Show("Nieprawidlowy link: " + url, "Blad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            if (!File.Exists(ytDlpPath))
            {
                MessageBox.Show("yt-dlp niedostepne", "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!Directory.Exists(ffmpegBinPath))
            {
                MessageBox.Show("FFmpeg niedostepne", "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string runtimePath = GetRuntimePath();
            if (string.IsNullOrEmpty(runtimePath))
            {
                MessageBox.Show("Brak runtime Deno/Node.js", "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (btnDownload != null)
                btnDownload.Enabled = false;
            SetUrlRowsEnabled(false);

            int successCount = 0;
            string downloadsDir = Path.Combine(appDirectory, "downloads");

            for (int i = 0; i < rawUrls.Count; i++)
            {
                if (progressBar != null)
                    progressBar.Value = 0;

                string statusPrefix = rawUrls.Count > 1 ? $"[{i + 1}/{rawUrls.Count}] " : "";
                UpdateStatus(statusPrefix + "Przygotowanie...");

                bool success = await DownloadSingleUrlAsync(rawUrls[i], runtimePath, statusPrefix);
                if (success)
                    successCount++;
            }

            if (btnDownload != null)
                btnDownload.Enabled = true;
            SetUrlRowsEnabled(true);
            if (progressBar != null)
                progressBar.Value = 0;

            if (successCount == rawUrls.Count)
            {
                string message = rawUrls.Count > 1 ? "Wszystkie pliki pobrane" : "Plik pobrany";
                UpdateStatus("Pobieranie zakonczono!");
                MessageBox.Show(message + ". Lokalizacja: " + downloadsDir, "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                UpdateStatus($"Zakonczono: {successCount}/{rawUrls.Count} pobranych");
                MessageBox.Show($"Pobrano {successCount} z {rawUrls.Count} plikow.", "Zakonczono z bledami", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

                using (var process = new Process { StartInfo = processInfo })
                {
                    process.OutputDataReceived += (s, args) =>
                    {
                        if (!string.IsNullOrEmpty(args.Data))
                        {
                            Invoke(new Action(() =>
                            {
                                if (args.Data.Contains("[download]"))
                                {
                                    ParseDownloadProgress(args.Data, progressBar, lblStatus, statusPrefix);
                                }
                                else if (args.Data.Contains("[info]") || args.Data.Contains("Downloading"))
                                {
                                    if (lblStatus != null)
                                        lblStatus.Text = statusPrefix + args.Data;
                                }
                            }));
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        if (progressBar != null)
                            progressBar.Value = 100;
                        UpdateStatus(statusPrefix + "Pobrano");
                        return true;
                    }
                    else
                    {
                        string error = await process.StandardError.ReadToEndAsync();
                        UpdateStatus(statusPrefix + "Blad pobierania");
                        MessageBox.Show("Blad (" + rawUrl + "): " + error, "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus(statusPrefix + "Blad");
                MessageBox.Show("Blad (" + rawUrl + "): " + ex.Message, "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                httpClient?.Dispose();
            base.Dispose(disposing);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}