using System;
using System.IO;
using Avalonia.Styling;

namespace YouTubeDownloader;

internal static class ThemeSettings
{
    public static string GetPath(string directory) => Path.Combine(directory, "theme.txt");

    public static string Load(string directory)
    {
        try
        {
            string text = File.ReadAllText(GetPath(directory)).Trim();
            return text is "Light" or "Dark" ? text : "Default";
        }
        catch
        {
            return "Default";
        }
    }

    public static void Save(string directory, string name)
    {
        try
        {
            File.WriteAllText(GetPath(directory), name);
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
