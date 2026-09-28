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

        Strings ui = Strings.Current;
        Title = ui.TitleAbout;
        VersionText.Text = ui.AboutVersion(GetAppVersion());
        LblLicense.Text = ui.AboutLicense;
        LblRepository.Text = ui.AboutRepository;
        LblAuthor.Text = ui.AboutAuthor;
        CloseButton.Content = ui.ButtonClose;

        RepoLink.PointerPressed += async (s, e) => await OpenUrl("https://github.com/bauerpawel/YouTube-Downloader");
        AuthorLink.PointerPressed += async (s, e) => await OpenUrl("https://bauer.net.pl");
        CloseButton.Click += (s, e) => Close();
    }

    private static string GetAppVersion()
    {
        string version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "?";
        int? build = AppUpdater.GetLocalBuildNumber();
        return build == null ? version : $"{version} (build {build})";
    }

    private async Task OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowAsync(this, Strings.Current.ErrorCannotOpenLink(ex.Message), Strings.Current.TitleError);
        }
    }
}
