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
        return GetAssetName(os, RuntimeInformation.ProcessArchitecture, AppPaths.AppBundlePath != null, AppPaths.IsAppImage);
    }

    // macOS has two installs: the plain folder (.zip, what every version before the
    // .dmg installed and still updates from) and the .app bundle (.dmg), which
    // updates from a zip of the whole bundle. Linux likewise: the plain single-file
    // binary and the AppImage, each updating from its own asset.
    public static string GetAssetName(string os, Architecture architecture, bool appBundle = false, bool appImage = false)
    {
        string arch = architecture == Architecture.Arm64 ? "arm64" : "x64";
        return os switch
        {
            "win" => $"YouTubeDownloader-win-{arch}.exe",
            "linux" => appImage ? $"YouTubeDownloader-linux-{arch}.AppImage" : $"YouTubeDownloader-linux-{arch}",
            "osx" => appBundle ? $"YouTubeDownloader-osx-{arch}-app.zip" : $"YouTubeDownloader-osx-{arch}.zip",
            _ => throw new PlatformNotSupportedException("App self-update is only supported on Windows, Linux, and macOS.")
        };
    }

    public static async Task<ReleaseInfo> GetLatestReleaseAsync(HttpClient http, string apiUrl = LatestReleaseUrl)
    {
        string json = await GitHubApi.GetStringAsync(http, apiUrl);
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

    // The file self-update replaces: the .AppImage when started from one (the exe
    // inside lives on a read-only mount), otherwise the running exe.
    public static string? UpdateTargetPath => AppPaths.AppImagePath ?? Environment.ProcessPath;

    // The folder self-update writes to. A macOS .app bundle is replaced as a whole
    // and an AppImage as a file - both from the folder holding them.
    public static string GetInstallDirectory(string appDirectory, string? appBundlePath, string? appImagePath)
    {
        string? holder = appBundlePath ?? appImagePath;
        return holder != null ? Path.GetDirectoryName(holder) ?? appDirectory : appDirectory;
    }

    public static List<(string Source, string Target)> BuildFileList(string extractedDirectory, string appDirectory) =>
        Directory.GetFiles(extractedDirectory, "*", SearchOption.AllDirectories)
            .Select(file => (Source: file, Target: Path.Combine(appDirectory, Path.GetRelativePath(extractedDirectory, file))))
            .ToList();

    // macOS plain folder only (single-file Windows/Linux use StartSwapAfterExit, the
    // .app bundle StartBundleSwapAfterExit). Two phases so a failure anywhere
    // leaves the installed app intact:
    // 1) every existing target -> target.old, 2) every source -> target.
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

        // Unix lets these go immediately even while in use.
        foreach (string target in backedUp)
            AppPaths.TryDeleteFile(target + OldSuffix);
    }

    // Windows and Linux (single-file). The single-file host reopens its own exe by
    // path each time an assembly is loaded for the first time (CoreCLR
    // PEImage::TryOpenFile), so the running process must never see that path
    // replaced: after an in-process swap the next new assembly (System.IO.Pipes,
    // for Process.Start on Linux) is read from the new file at the old file's
    // offsets and fails to load. A shell replaces the exe once this process has
    // exited and starts it - the old copy if the replacement failed.
    public static void StartSwapAfterExit(string downloadPath, string executablePath)
    {
        AppPaths.MakeExecutable(downloadPath);

        ProcessStartInfo startInfo = OperatingSystem.IsWindows()
            ? WindowsSwapAfterExit(downloadPath, executablePath)
            : UnixSwapAfterExit(downloadPath, executablePath);
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.WorkingDirectory = Path.GetDirectoryName(executablePath) ?? "";

        using var process = Process.Start(startInfo);
    }

    // Waits for this PID, then renames (atomic within one folder) and starts the
    // result - the exe, or the .AppImage, whose old copy the AppImage runtime may
    // still hold open (Linux allows that). Positional parameters ($1..$3), so no
    // path ever needs shell quoting. appimage/smoke-test.sh runs this exact text
    // against a real AppImage - keep it a raw literal between the BEGIN/END lines.
    // BEGIN UnixSwapScript
    private const string UnixSwapScript = """
        while kill -0 "$1" 2>/dev/null; do sleep 0.2; done
        mv -f "$2" "$3"
        exec "$3"
        """;
    // END UnixSwapScript

    private static ProcessStartInfo UnixSwapAfterExit(string downloadPath, string executablePath) =>
        new("/bin/sh")
        {
            // A raw literal takes the source file's line endings - CRLF in a Windows
            // checkout (build.bat cross-compiles linux too), and sh rejects "done\r".
            ArgumentList =
            {
                "-c", UnixSwapScript.ReplaceLineEndings("\n"), "sh",
                Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
                downloadPath, executablePath
            }
        };

    // cmd cannot wait for a PID and needs not: Windows refuses to replace an exe
    // that is still running, so `move` succeeds exactly once the app is gone.
    // Retried once a second; after two minutes it starts whatever exe is there.
    // Every value is read with delayed expansion (!VAR!), which inserts it
    // verbatim after all other parsing - a folder name with %, !, ^ or & stays
    // intact. ping by full path: cmd looks in the current directory (the app
    // folder, maybe Downloads) before PATH. The loop needs its own parentheses,
    // otherwise cmd takes the trailing `& start` as part of the loop body.
    private const string WindowsSwapScript =
        "(for /l %i in (1,1,120) do @(" +
            "move /y \"!YTD_UPDATE_SOURCE!\" \"!YTD_UPDATE_TARGET!\" >nul 2>&1" +
            " && (start \"\" \"!YTD_UPDATE_TARGET!\" & exit)" +
            " & \"!SystemRoot!\\System32\\PING.EXE\" -n 2 127.0.0.1 >nul" +
        ")) & start \"\" \"!YTD_UPDATE_TARGET!\"";

    private static ProcessStartInfo WindowsSwapAfterExit(string downloadPath, string executablePath) =>
        new(Path.Combine(Environment.SystemDirectory, "cmd.exe"))
        {
            // /s: cmd strips only the outer quotes and runs the rest as written.
            Arguments = "/d /v:on /s /c \"" + WindowsSwapScript + "\"",
            Environment =
            {
                ["YTD_UPDATE_SOURCE"] = downloadPath,
                ["YTD_UPDATE_TARGET"] = executablePath
            }
        };

    // macOS .app bundle: codesign keeps the signatures of the non-Mach-O files in
    // Contents/MacOS (.dll, .json) in extended attributes. ditto carries them (and
    // the Unix modes) through the zip - macos/package.sh zips with ditto too -
    // while .NET's ZipFile drops them and leaves a bundle that fails verification.
    public static Task<(int ExitCode, string StandardError)> ExtractBundleZipAsync(string zipPath, string destination) =>
        ProcessRunner.RunAsync(new ProcessStartInfo("/usr/bin/ditto")
        {
            ArgumentList = { "-x", "-k", zipPath, destination },
            CreateNoWindow = true
        });

    // macOS .app bundle: the zip holds exactly one <name>.app; null otherwise.
    public static string? FindExtractedBundle(string extractedDirectory)
    {
        string[] bundles = Directory.GetDirectories(extractedDirectory, "*.app");
        return bundles.Length == 1 && File.Exists(Path.Combine(bundles[0], "Contents", "Info.plist"))
            ? bundles[0]
            : null;
    }

    // macOS .app bundle: replaced as a whole once this process has exited, so the
    // running app never loads an assembly from the new version and the bundle's
    // code signature stays intact. The old bundle is renamed to <bundle>.old first
    // and put back if the new one cannot be moved in; either way the bundle at the
    // original path is opened again. Positional parameters ($1..$3), as in
    // UnixSwapScript. CI runs this exact text against real bundles - keep it a raw
    // literal between the BEGIN/END lines.
    // BEGIN MacBundleSwapScript
    private const string MacBundleSwapScript = """
        while kill -0 "$1" 2>/dev/null; do sleep 0.2; done
        rm -rf "$3.old"
        if mv "$3" "$3.old"; then
          if mv "$2" "$3"; then rm -rf "$3.old"; else mv "$3.old" "$3"; fi
        fi
        exec /usr/bin/open "$3"
        """;
    // END MacBundleSwapScript

    public static void StartBundleSwapAfterExit(string newBundlePath, string bundlePath)
    {
        var startInfo = new ProcessStartInfo("/bin/sh")
        {
            // A raw literal takes the source file's line endings - CRLF in a Windows
            // checkout (build.bat cross-compiles osx too), and sh rejects "done\r".
            ArgumentList =
            {
                "-c", MacBundleSwapScript.ReplaceLineEndings("\n"), "sh",
                Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
                newBundlePath, bundlePath
            },
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(bundlePath) ?? "/"
        };

        using var process = Process.Start(startInfo);
    }

    // macOS .app bundle: the download and its extraction live in the data folder -
    // nothing may be written into the bundle itself.
    public static void DeleteBundleUpdateDownloads(string dataDirectory)
    {
        AppPaths.TryDeleteFile(Path.Combine(dataDirectory, UpdateZipName));
        AppPaths.TryDeleteDirectory(Path.Combine(dataDirectory, UpdateDirectoryName));
    }

    // This process runs from bundlePath, so <bundle>.old is never the only copy.
    public static void CleanupBundleLeftovers(string bundlePath, string dataDirectory)
    {
        AppPaths.TryDeleteDirectory(bundlePath + OldSuffix);
        DeleteBundleUpdateDownloads(dataDirectory);
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
