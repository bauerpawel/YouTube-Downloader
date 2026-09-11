using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace YouTubeDownloader;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        VersionText.Text = "Wersja " + GetAppVersion();

        RepoLink.PointerPressed += async (s, e) => await OpenUrl("https://github.com/bauerpawel/YouTube-Downloader");
        AuthorLink.PointerPressed += async (s, e) => await OpenUrl("https://bauer.net.pl");
        CloseButton.Click += (s, e) => Close();
    }

    private static string GetAppVersion()
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "?";
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
