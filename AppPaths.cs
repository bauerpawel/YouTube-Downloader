using System;
using System.IO;

namespace YouTubeDownloader;

// The app folder (AppDirectory) holds only the app itself plus downloads/.
// Everything the app downloads or writes lives in DataDirectory, so the app
// folder can be read-only (snap, AppImage, Program Files) and self-update only ever
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

    private static readonly Lazy<string> downloadsDirectory = new(() => ResolveDownloadsDirectory(
        IsSnap,
        AppBundlePath != null,
        IsAppImage,
        AppContext.BaseDirectory,
        Environment.GetEnvironmentVariable("SNAP_REAL_HOME"),
        Environment.GetEnvironmentVariable("SNAP_USER_COMMON"),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));

    public static string AppDirectory { get; } = AppContext.BaseDirectory;

    public static string DataDirectory => dataDirectory.Value;

    // Next to the app, except in a snap and an AppImage (both run from a read-only
    // mount) and in a macOS .app bundle (it must stay unmodified and self-update
    // replaces it whole).
    public static string DownloadsDirectory => downloadsDirectory.Value;

    // macOS installed from the .dmg: the exe runs from <name>.app/Contents/MacOS.
    // null for the plain macOS folder (the .zip) and on every other OS.
    public static string? AppBundlePath { get; } =
        OperatingSystem.IsMacOS() ? FindAppBundle(AppContext.BaseDirectory) : null;

    public static string? FindAppBundle(string appDirectory)
    {
        var macOs = new DirectoryInfo(Path.TrimEndingDirectorySeparator(Path.GetFullPath(appDirectory)));
        DirectoryInfo? contents = macOs.Parent;
        DirectoryInfo? bundle = contents?.Parent;

        if (bundle == null || macOs.Name != "MacOS" || contents!.Name != "Contents"
            || !bundle.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            return null;

        return bundle.FullName;
    }

    private static readonly Lazy<bool> isSnap = new(() =>
        IsRunningFromSnap(Environment.GetEnvironmentVariable("SNAP"), AppContext.BaseDirectory));

    // SNAP alone is no proof: a snap terminal (VS Code, ...) leaks its SNAP*
    // variables into everything started from it, the plain Linux build included -
    // which would then turn self-update off and write into that other snap's
    // folders. Our own snap runs the exe from under $SNAP.
    public static bool IsSnap => isSnap.Value;

    public static bool IsRunningFromSnap(string? snap, string appDirectory) => IsUnder(snap, appDirectory);

    private static readonly Lazy<string?> appImagePath = new(() =>
        OperatingSystem.IsLinux() && !IsSnap
            ? FindAppImage(Environment.GetEnvironmentVariable("APPIMAGE"),
                Environment.GetEnvironmentVariable("APPDIR"), AppContext.BaseDirectory)
            : null);

    // Started from an AppImage: the .AppImage file. Its runtime mounts the image
    // read-only at APPDIR, where the exe runs, so self-update replaces this file
    // instead. null otherwise.
    public static string? AppImagePath => appImagePath.Value;

    public static bool IsAppImage => AppImagePath != null;

    // As with SNAP: another AppImage (Cursor, Obsidian...) leaks APPIMAGE and
    // APPDIR into its terminal and everything started from there, the plain Linux
    // build included - which would then update by replacing that app's file. Ours
    // runs the exe from under APPDIR (the mount, or the --appimage-extract-and-run
    // folder).
    public static string? FindAppImage(string? appImage, string? appDir, string appDirectory) =>
        !string.IsNullOrEmpty(appImage) && IsUnder(appDir, appDirectory) ? appImage : null;

    private static bool IsUnder(string? root, string directory)
    {
        if (string.IsNullOrEmpty(root))
            return false;

        string rootPrefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        string path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
        return path.StartsWith(rootPrefix, StringComparison.Ordinal);
    }

    public static string ResolveDataDirectory(string? snapUserCommon, string localAppData, string appDirectory)
    {
        if (!string.IsNullOrEmpty(snapUserCommon))
            return snapUserCommon;

        if (string.IsNullOrEmpty(localAppData))
            return appDirectory;

        return Path.Combine(localAppData, DataFolderName);
    }

    // Inside a snap and an AppImage: the user's Downloads folder (in a snap the
    // desktop interface may read ~/.config/user-dirs.dirs, the home interface may
    // write there) plus a folder of our own. In a snap $HOME is
    // ~/snap/<name>/<revision>; snapd passes the real one in SNAP_REAL_HOME.
    // Without it (old snapd) $SNAP_USER_COMMON, never the versioned $HOME: snapd
    // copies that on every refresh, videos included. An AppImage runs in the real
    // home. A macOS .app bundle uses ~/Downloads plus the same folder.
    public const string UserDownloadsFolderName = "YouTube Downloader";

    public static string ResolveDownloadsDirectory(bool isSnap, bool isAppBundle, bool isAppImage, string appDirectory,
        string? snapRealHome, string? snapUserCommon, string userProfile)
    {
        if (isAppBundle)
            return Path.Combine(userProfile, "Downloads", UserDownloadsFolderName);

        if (!isSnap && !isAppImage)
            return Path.Combine(appDirectory, "downloads");

        string realHome = !isSnap ? userProfile
            : !string.IsNullOrEmpty(snapRealHome) ? snapRealHome
            : !string.IsNullOrEmpty(snapUserCommon) ? snapUserCommon
            : userProfile;
        string? userDirs = TryReadAllText(Path.Combine(realHome, ".config", "user-dirs.dirs"));
        return Path.Combine(ResolveUserDownloadDirectory(userDirs, realHome), UserDownloadsFolderName);
    }

    // xdg-user-dirs format: XDG_DOWNLOAD_DIR="$HOME/Pobrane" or an absolute path;
    // "$HOME" alone means the folder is disabled. The last assignment wins, as in
    // the shell. Shell escapes are not handled - xdg-user-dirs does not write
    // them for ordinary folder names.
    public static string ResolveUserDownloadDirectory(string? userDirsContent, string realHome)
    {
        const string key = "XDG_DOWNLOAD_DIR=";
        const string homePrefix = "$HOME/";
        string fallback = Path.Combine(realHome, "Downloads");

        string? value = null;
        foreach (string line in (userDirsContent ?? "").Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith(key, StringComparison.Ordinal))
                value = trimmed[key.Length..];
        }

        if (value == null)
            return fallback;

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            value = value[1..^1];

        if (value.StartsWith(homePrefix, StringComparison.Ordinal))
        {
            string relative = value[homePrefix.Length..].Trim('/');
            return relative.Length == 0 ? fallback : Path.Combine(realHome, relative);
        }

        return value.StartsWith('/') ? value : fallback;
    }

    private static string CreateDataDirectory()
    {
        string appDirectory = AppContext.BaseDirectory;
        string directory = ResolveDataDirectory(
            IsSnap ? Environment.GetEnvironmentVariable("SNAP_USER_COMMON") : null,
            // DoNotVerify: without it .NET returns "" when ~/.local/share (or
            // XDG_DATA_HOME) does not exist yet, and the app would fall back to its
            // own folder - a read-only mount in an AppImage. CreateDirectory below
            // creates the whole path.
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.DoNotVerify),
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

    private static string? TryReadAllText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
