using System;
using System.Globalization;
using System.IO;

namespace YouTubeDownloader;

internal static class LanguageSettings
{
    public const string Polish = "pl";
    public const string English = "en";

    public static string GetPath(string directory) => Path.Combine(directory, "language.txt");

    public static string? Load(string directory)
    {
        try
        {
            return File.ReadAllText(GetPath(directory)).Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Save(string directory, string code)
    {
        try
        {
            File.WriteAllText(GetPath(directory), code);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // A saved choice wins; with none (or an unexpected value) a Polish system gets
    // Polish and every other system English.
    public static string Resolve(string? saved, CultureInfo systemCulture)
    {
        if (saved is Polish or English)
            return saved;

        return systemCulture.TwoLetterISOLanguageName == Polish ? Polish : English;
    }
}
