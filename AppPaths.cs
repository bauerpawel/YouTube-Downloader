using System;
using System.IO;

namespace YouTubeDownloader;

// The app folder (AppDirectory) holds only the app itself plus downloads/.
// Everything the app downloads or writes lives in DataDirectory, so the app
// folder can be read-only (snap, Program Files) and self-update only ever
// replaces the app's own files.
internal static class AppPaths
{
    public const string DataFolderName = "YouTubeDownloader";

    // Every pre-data-directory version wrote this next to the exe once FFmpeg was
    // installed - proof that the app folder was managed by an old version.
    private const string LegacyMarkerFile = "ffmpeg_version.txt";

    // Deliberately excludes node(.exe) (user-supplied, never downloaded by the app)
    // and the old temp archives deno.zip/ffmpeg.zip/ffmpeg.tar.xz (too generic -
    // the app folder may be the user's Downloads folder).
    private static readonly string[] LegacyFiles =
    {
        "yt-dlp.exe", "yt-dlp", "deno.exe", "deno", "deno_version.txt", "theme.txt"
    };

    private static readonly string[] LegacyDirectories = { "ffmpeg_bin", "ffmpeg_temp" };

    private static readonly Lazy<string> dataDirectory = new(CreateDataDirectory);

    public static string AppDirectory { get; } = AppContext.BaseDirectory;

    public static string DataDirectory => dataDirectory.Value;

    public static bool IsSnap => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SNAP"));

    public static string ResolveDataDirectory(string? snapUserCommon, string localAppData, string appDirectory)
    {
        if (!string.IsNullOrEmpty(snapUserCommon))
            return snapUserCommon;

        if (string.IsNullOrEmpty(localAppData))
            return appDirectory;

        return Path.Combine(localAppData, DataFolderName);
    }

    private static string CreateDataDirectory()
    {
        string appDirectory = AppContext.BaseDirectory;
        string directory = ResolveDataDirectory(
            Environment.GetEnvironmentVariable("SNAP_USER_COMMON"),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            appDirectory);

        try
        {
            Directory.CreateDirectory(directory);
            return directory;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return appDirectory;
        }
    }

    public static void MakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        File.SetUnixFileMode(path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    public static bool IsSameDirectory(string a, string b)
    {
        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            comparison);
    }

    public static void CleanupLegacyFiles(string appDirectory, string dataDirectory)
    {
        if (IsSameDirectory(appDirectory, dataDirectory))
            return;

        string marker = Path.Combine(appDirectory, LegacyMarkerFile);
        if (!File.Exists(marker))
            return;

        bool allDeleted = true;
        foreach (string name in LegacyDirectories)
            allDeleted &= TryDeleteDirectory(Path.Combine(appDirectory, name));
        foreach (string name in LegacyFiles)
            allDeleted &= TryDeleteFile(Path.Combine(appDirectory, name));

        // Marker last, and only once everything else is gone - otherwise the next
        // start must still recognize this folder and retry.
        if (allDeleted)
            TryDeleteFile(marker);
    }

    // `where` (Windows) searches the current directory before PATH, and a
    // double-clicked app runs with its own folder as the current directory - so
    // a hit there is the app's own old copy, not a system-wide install.
    // A GUI app also gets `where` output in the OEM code page decoded as ANSI, so
    // a non-ASCII folder (C:\Users\Michał\...) comes back mangled and no longer
    // matches excludedDirectory - such a line names no real file and is skipped.
    public static string PickFirstPathOutside(string commandOutput, string excludedDirectory)
    {
        foreach (string line in commandOutput.Split('\n'))
        {
            string path = line.Trim();
            if (path.Length == 0 || !File.Exists(path))
                continue;

            string? directory = Path.GetDirectoryName(path);
            if (directory != null && IsSameDirectory(directory, excludedDirectory))
                continue;

            return path;
        }

        return "";
    }

    public static bool TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return !File.Exists(path);
        }
    }

    public static bool TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return !Directory.Exists(path);
        }
    }
}
