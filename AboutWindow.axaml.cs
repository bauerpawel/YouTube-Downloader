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
