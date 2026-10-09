using Xunit;

namespace YouTubeDownloader.Tests;

public class AppImagePathsTests
{
    private static readonly string Tmp = Path.GetTempPath();
    private static readonly string Mount = Path.Combine(Tmp, ".mount_YouTubAbC123");
    private static readonly string ExeDir = Path.Combine(Mount, "usr", "bin");
    private static readonly string Image = Path.Combine(Tmp, "Aplikacje ąę", "YTD test.AppImage");
    private static readonly string PlainDir = Path.Combine(Tmp, "home", "user", "bin");

    [Fact]
    public void ExeUnderAppDirIsTheAppImage()
    {
        Assert.Equal(Image, AppPaths.FindAppImage(Image, Mount, ExeDir));
        Assert.Equal(Image, AppPaths.FindAppImage(Image, Mount, Mount));
        Assert.Equal(Image, AppPaths.FindAppImage(Image,
            Mount + Path.DirectorySeparatorChar, ExeDir + Path.DirectorySeparatorChar));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("image", false)]
    [InlineData("image", null)]
    public void MissingVariablesMeanNoAppImage(string? appImage, bool? appDirSet)
    {
        string? image = appImage == "image" ? Image : appImage;
        string? appDir = appDirSet switch { true => Mount, false => "", null => null };
        Assert.Null(AppPaths.FindAppImage(image, appDir, ExeDir));
    }

    // Another AppImage's terminal (Cursor, Obsidian) leaks both variables into the
    // plain binary started from it - which runs from somewhere else.
    [Fact]
    public void LeakedVariablesDoNotTurnThePlainBinaryIntoAnAppImage()
    {
        Assert.Null(AppPaths.FindAppImage(Path.Combine(Tmp, "Cursor.AppImage"), Mount, PlainDir));
    }

    [Fact]
    public void AppDirNamePrefixIsNotContainment()
    {
        Assert.Null(AppPaths.FindAppImage(Image, Mount[..^1], ExeDir));
    }

    // --appimage-extract-and-run: APPDIR is the extraction folder, APPIMAGE still the file.
    [Fact]
    public void ExtractAndRunIsAnAppImage()
    {
        string extracted = Path.Combine(Tmp, "appimage_extracted_0123456789abcdef");
        Assert.Equal(Image, AppPaths.FindAppImage(Image, extracted, Path.Combine(extracted, "usr", "bin")));
    }

    [Fact]
    public void SnapDetectionIsUnchanged()
    {
        string snap = Path.Combine(Tmp, "snap", "yt-downloader-bp", "12");
        Assert.True(AppPaths.IsRunningFromSnap(snap, Path.Combine(snap, "bin")));
        Assert.False(AppPaths.IsRunningFromSnap(snap, PlainDir));
        Assert.False(AppPaths.IsRunningFromSnap(null, Path.Combine(snap, "bin")));
        Assert.False(AppPaths.IsRunningFromSnap(snap[..^1], Path.Combine(snap, "bin")));
    }

    [Fact]
    public void AppImageDownloadsGoToTheXdgDownloadsFolder()
    {
        string home = NewTempDir();
        Assert.Equal(Path.Combine(home, "Downloads", "YouTube Downloader"),
            AppPaths.ResolveDownloadsDirectory(false, false, true, ExeDir, null, null, home));

        WriteUserDirs(home);
        Assert.Equal(Path.Combine(home, "Pobrane", "YouTube Downloader"),
            AppPaths.ResolveDownloadsDirectory(false, false, true, ExeDir, null, null, home));

        // SNAP_* leaked into an AppImage (not a snap) must not move the home.
        Assert.Equal(Path.Combine(home, "Pobrane", "YouTube Downloader"),
            AppPaths.ResolveDownloadsDirectory(false, false, true, ExeDir,
                Path.Combine(Tmp, "elsewhere"), Path.Combine(Tmp, "snap-common"), home));
    }

    [Fact]
    public void OtherDownloadsFoldersAreUnchanged()
    {
        string home = NewTempDir();
        string appDir = Path.Combine(home, "app");
        Assert.Equal(Path.Combine(appDir, "downloads"),
            AppPaths.ResolveDownloadsDirectory(false, false, false, appDir, null, null, home));
        Assert.Equal(Path.Combine(home, "Downloads", "YouTube Downloader"),
            AppPaths.ResolveDownloadsDirectory(false, true, false, appDir, null, null, home));

        string realHome = NewTempDir();
        WriteUserDirs(realHome);
        Assert.Equal(Path.Combine(realHome, "Pobrane", "YouTube Downloader"),
            AppPaths.ResolveDownloadsDirectory(true, false, false, appDir, realHome, null, home));
    }

    // A fresh home may have no ~/.local/share yet (or XDG_DATA_HOME names a folder
    // that does not exist). The data folder must still be created there: the
    // fallback, the app folder, is a read-only mount in an AppImage, so no tool
    // could be installed at all.
    [Fact]
    public async Task DataDirectoryIsCreatedWhenXdgDataHomeDoesNotExistYet()
    {
        if (!OperatingSystem.IsLinux())
            return; // XDG_DATA_HOME is Linux-only; LocalApplicationData exists elsewhere

        string dataHome = Path.Combine(NewTempDir(), "missing", "data");
        var command = ChildProcess.Command("--data-directory");
        command.Environment["XDG_DATA_HOME"] = dataHome;
        command.Environment.Remove("SNAP");
        var lines = new List<string>();
        var (code, error) = await ProcessRunner.RunAsync(command, lines.Add);

        Assert.True(code == 0, error);
        Assert.Equal(Path.Combine(dataHome, AppPaths.DataFolderName), Assert.Single(lines));
        Assert.True(Directory.Exists(Path.Combine(dataHome, AppPaths.DataFolderName)));
    }

    private static void WriteUserDirs(string home)
    {
        Directory.CreateDirectory(Path.Combine(home, ".config"));
        File.WriteAllText(Path.Combine(home, ".config", "user-dirs.dirs"), "XDG_DOWNLOAD_DIR=\"$HOME/Pobrane\"\n");
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Tmp, "ytd-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
