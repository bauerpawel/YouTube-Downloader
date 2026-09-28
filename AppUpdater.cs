using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace YouTubeDownloader;

internal sealed record ReleaseAsset(string Name, string DownloadUrl, long Size, string? Sha256);

internal sealed record ReleaseInfo(string TagName, int? BuildNumber, string HtmlUrl, ReleaseAsset? Asset);

// Self-update from this repo's GitHub Releases. No UI here - MainWindow owns the
// dialogs and progress bar - which also keeps this file free of Avalonia.
internal static class AppUpdater
{
    public const string Repo = "bauerpawel/YouTube-Downloader";
    public const string OldSuffix = ".old";
    public const string NewSuffix = ".new";
    public const string UpdateZipName = "YouTubeDownloader-update.zip";
    public const string UpdateDirectoryName = "YouTubeDownloader-update";

    private const string LatestReleaseUrl = "https://api.github.com/repos/" + Repo + "/releases/latest";

    // CI passes -p:BuildNumber=<github.run_number>; local builds have none.
    public static int? GetLocalBuildNumber() => GetBuildNumber(typeof(AppUpdater).Assembly);

    public static int? GetBuildNumber(Assembly assembly)
    {
        string? value = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildNumber")?.Value;
        return ParseNumber(value);
    }

    // "v2.0.110926-123" -> 123. <Version> itself (2.0.ddMMyy) is not monotonic, so
    // the CI run number in the tag is the only thing that can be compared.
    public static int? ParseBuildNumber(string tagName)
    {
        if (!tagName.StartsWith('v'))
            return null;

        int dash = tagName.LastIndexOf('-');
        return dash < 0 ? null : ParseNumber(tagName[(dash + 1)..]);
    }

    private static int? ParseNumber(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) ? number : null;

    public static string GetAssetName()
    {
        string os = OperatingSystem.IsWindows() ? "win"
            : OperatingSystem.IsMacOS() ? "osx"
            : OperatingSystem.IsLinux() ? "linux"
            : "";
        return GetAssetName(os, RuntimeInformation.ProcessArchitecture);
    }

    public static string GetAssetName(string os, Architecture architecture)
    {
        string arch = architecture == Architecture.Arm64 ? "arm64" : "x64";
        return os switch
        {
            "win" => $"YouTubeDownloader-win-{arch}.exe",
            "linux" => $"YouTubeDownloader-linux-{arch}",
            "osx" => $"YouTubeDownloader-osx-{arch}.zip",
            _ => throw new PlatformNotSupportedException("App self-update is only supported on Windows, Linux, and macOS.")
        };
    }

    public static async Task<ReleaseInfo> GetLatestReleaseAsync(HttpClient http, string apiUrl = LatestReleaseUrl)
    {
        string json = await http.GetStringAsync(apiUrl);
        return ParseRelease(json, GetAssetName());
    }

    public static ReleaseInfo ParseRelease(string json, string assetName)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        string tagName = root.GetProperty("tag_name").GetString() ?? "";
        string htmlUrl = root.GetProperty("html_url").GetString() ?? "";

        // CI attaches assets one RID at a time, so a brand-new release can lack
        // this platform's file for a few minutes - Asset stays null then.
        ReleaseAsset? match = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != assetName)
                continue;

            string? sha256 = null;
            if (asset.TryGetProperty("digest", out var digest) && digest.ValueKind == JsonValueKind.String)
            {
                string value = digest.GetString() ?? "";
                if (value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                    sha256 = value["sha256:".Length..].ToLowerInvariant();
            }

            match = new ReleaseAsset(
                assetName,
                asset.GetProperty("browser_download_url").GetString() ?? "",
                asset.GetProperty("size").GetInt64(),
                sha256);
            break;
        }

        return new ReleaseInfo(tagName, ParseBuildNumber(tagName), htmlUrl, match);
    }

    public static bool VerifyDownload(string path, ReleaseAsset asset)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != asset.Size)
            return false;

        if (asset.Sha256 == null)
            return true;

        using var stream = File.OpenRead(path);
        string actual = Convert.ToHexString(SHA256.HashData(stream));
        return string.Equals(actual, asset.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanWriteDirectory(string directory)
    {
        try
        {
            string probe = Path.Combine(directory, ".ytd-write-test-" + Guid.NewGuid().ToString("N"));
            using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static List<(string Source, string Target)> BuildFileList(string extractedDirectory, string appDirectory) =>
        Directory.GetFiles(extractedDirectory, "*", SearchOption.AllDirectories)
            .Select(file => (Source: file, Target: Path.Combine(appDirectory, Path.GetRelativePath(extractedDirectory, file))))
            .ToList();

    // Two phases so a failure anywhere leaves the installed app intact:
    // 1) every existing target -> target.old, 2) every source -> target.
    // Renaming a running executable is allowed on Windows (overwriting is not);
    // on Linux/macOS the running process keeps the old inode.
    public static void ApplyUpdate(IReadOnlyList<(string Source, string Target)> files)
    {
        // Before anything is touched, so a failure here costs nothing. The macOS
        // zip (Compress-Archive in CI) carries no Unix modes at all; the bit is
        // harmless on libraries.
        foreach (var (source, _) in files)
            AppPaths.MakeExecutable(source);

        var backedUp = new List<string>();
        var installed = new List<string>();
        try
        {
            foreach (var (_, target) in files)
            {
                if (!File.Exists(target))
                    continue;
                File.Move(target, target + OldSuffix, overwrite: true);
                backedUp.Add(target);
            }

            foreach (var (source, target) in files)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(source, target);
                installed.Add(target);
            }
        }
        catch
        {
            foreach (string target in installed)
                AppPaths.TryDeleteFile(target);

            foreach (string target in backedUp)
            {
                try
                {
                    File.Move(target + OldSuffix, target, overwrite: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Keep restoring the rest; the .old copy stays for manual recovery.
                }
            }

            throw;
        }

        // Unix lets these go immediately even while in use; on Windows the running
        // exe's .old stays locked until this process exits and CleanupLeftovers()
        // removes it on the next start.
        foreach (string target in backedUp)
            AppPaths.TryDeleteFile(target + OldSuffix);
    }

    public static void Relaunch(string executablePath)
    {
        using var process = Process.Start(new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? ""
        });
    }

    // Never deletes <exe>.old: after a failed rollback it may be the only copy.
    public static void DeleteUpdateDownloads(string appDirectory, string executablePath)
    {
        AppPaths.TryDeleteFile(executablePath + NewSuffix);
        AppPaths.TryDeleteFile(Path.Combine(appDirectory, UpdateZipName));
        AppPaths.TryDeleteDirectory(Path.Combine(appDirectory, UpdateDirectoryName));
    }

    // Exact names only - the app folder may be the user's Downloads folder.
    public static void CleanupLeftovers(string appDirectory, string? executablePath)
    {
        if (executablePath == null)
            return;

        AppPaths.TryDeleteFile(executablePath + OldSuffix);
        DeleteUpdateDownloads(appDirectory, executablePath);
    }
}
