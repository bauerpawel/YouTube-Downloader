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
