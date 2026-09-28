using System.Globalization;
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
            string dataDirectory = AppPaths.DataDirectory;
            RequestedThemeVariant = ThemeSettings.ToVariant(ThemeSettings.Load(dataDirectory));
            Strings.Current = Strings.For(
                LanguageSettings.Resolve(LanguageSettings.Load(dataDirectory), CultureInfo.CurrentUICulture));

            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
