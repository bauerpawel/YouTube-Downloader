using System.Runtime.InteropServices;
using Xunit;

namespace YouTubeDownloader.Tests;

public class AppImageUpdateTests
{
    [Theory]
    [InlineData("linux", Architecture.X64, false, true, "YouTubeDownloader-linux-x64.AppImage")]
    [InlineData("linux", Architecture.Arm64, false, true, "YouTubeDownloader-linux-arm64.AppImage")]
    [InlineData("linux", Architecture.X64, false, false, "YouTubeDownloader-linux-x64")]
    [InlineData("linux", Architecture.Arm64, false, false, "YouTubeDownloader-linux-arm64")]
    [InlineData("osx", Architecture.Arm64, true, false, "YouTubeDownloader-osx-arm64-app.zip")]
    [InlineData("osx", Architecture.X64, false, false, "YouTubeDownloader-osx-x64.zip")]
    [InlineData("win", Architecture.X64, false, false, "YouTubeDownloader-win-x64.exe")]
    public void EachInstallUpdatesFromItsOwnAsset(string os, Architecture arch, bool appBundle, bool appImage, string expected)
    {
        Assert.Equal(expected, AppUpdater.GetAssetName(os, arch, appBundle, appImage));
    }

    // An AppImage runs from a read-only mount: write access is needed where the
    // .AppImage file lives (not in /opt, say), never on the mount.
    [Fact]
    public void InstallDirectoryIsTheFolderHoldingWhatGetsReplaced()
    {
        string root = NewTempDir();
        string mountBin = Path.Combine(root, ".mount_YouTubAbC123", "usr", "bin");
        string folder = Path.Combine(root, "Aplikacje ąę");
        Assert.Equal(folder, AppUpdater.GetInstallDirectory(mountBin, null, Path.Combine(folder, "YTD test.AppImage")));

        string bundle = Path.Combine(root, "Applications", "YouTube Downloader.app");
        Assert.Equal(Path.Combine(root, "Applications"),
            AppUpdater.GetInstallDirectory(Path.Combine(bundle, "Contents", "MacOS"), bundle, null));

        string appDir = Path.Combine(root, "app");
        Assert.Equal(appDir, AppUpdater.GetInstallDirectory(appDir, null, null));
    }

    // AppImageLauncher / Gear Lever rename and move the file; an interrupted
    // update leaves <file>.new next to it.
    [Fact]
    public void LeftoversNextToTheAppImageAreRemovedAndNothingElse()
    {
        string root = NewTempDir();
        string mountBin = Path.Combine(root, ".mount_YouTubAbC123", "usr", "bin");
        string folder = Path.Combine(root, "Aplikacje ąę");
        Directory.CreateDirectory(folder);
        string image = Path.Combine(folder, "YTD test.AppImage");
        File.WriteAllText(image, "app");
        File.WriteAllText(image + AppUpdater.NewSuffix, "partial download");
        File.WriteAllText(image + AppUpdater.OldSuffix, "old");
        string other = Path.Combine(folder, "Inna.AppImage");
        File.WriteAllText(other, "x");
        File.WriteAllText(other + AppUpdater.NewSuffix, "x");

        AppUpdater.CleanupLeftovers(mountBin, image);

        Assert.True(File.Exists(image));
        Assert.False(File.Exists(image + AppUpdater.NewSuffix));
        Assert.False(File.Exists(image + AppUpdater.OldSuffix));
        Assert.True(File.Exists(other));
        Assert.True(File.Exists(other + AppUpdater.NewSuffix));
    }

    // --appimage-extract-and-run (no FUSE): the runtime takes the flag out of the
    // arguments, so the restarted AppImage must get it as an environment variable,
    // or it tries FUSE, fails and the app never comes back after an update.
    [Theory]
    [InlineData("/tmp/appimage_extracted_0123456789abcdef", true)]
    [InlineData("/tmp/appimage_extracted_0123456789abcdef/", true)]
    [InlineData("/tmp/.mount_YouTubAbC123", false)]
    [InlineData("/tmp/.mount_appimage_extracted_", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void RecognizesTheExtractAndRunFolder(string? appDir, bool expected)
    {
        Assert.Equal(expected, AppPaths.IsExtractAndRunDirectory(appDir));
    }

    [Fact]
    public void SwapOfAnExtractedAppImageRestartsItExtracted()
    {
        var extracted = AppUpdater.CreateUnixSwap("/a/YTD.AppImage.new", "/a/YTD.AppImage", extractAndRun: true);
        Assert.Equal("1", extracted.Environment["APPIMAGE_EXTRACT_AND_RUN"]);

        var mounted = AppUpdater.CreateUnixSwap("/a/YTD.AppImage.new", "/a/YTD.AppImage", extractAndRun: false);
        // A FUSE-mounted AppImage restarts with the environment it inherited.
        Assert.Equal(Environment.GetEnvironmentVariable("APPIMAGE_EXTRACT_AND_RUN"),
            mounted.Environment.TryGetValue("APPIMAGE_EXTRACT_AND_RUN", out string? value) ? value : null);

        // The script gets the paths as positional parameters either way.
        Assert.Equal("/bin/sh", extracted.FileName);
        Assert.Equal(new[] { "/a/YTD.AppImage.new", "/a/YTD.AppImage" }, extracted.ArgumentList.TakeLast(2));
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ytd-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
