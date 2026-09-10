using System;
using System.IO;
using Avalonia.Styling;

namespace YouTubeDownloader;

internal static class ThemeSettings
{
    public static string GetPath(string appDirectory) => Path.Combine(appDirectory, "theme.txt");

    public static string Load(string appDirectory)
    {
        try
        {
            string text = File.ReadAllText(GetPath(appDirectory)).Trim();
            return text is "Light" or "Dark" ? text : "Default";
        }
        catch
        {
            return "Default";
        }
    }

    public static void Save(string appDirectory, string name)
    {
        try
        {
            File.WriteAllText(GetPath(appDirectory), name);
        }
        catch
        {
        }
    }

    public static ThemeVariant ToVariant(string name) => name switch
    {
        "Light" => ThemeVariant.Light,
        "Dark" => ThemeVariant.Dark,
        _ => ThemeVariant.Default
    };
}
