using System.Net;
using Xunit;

namespace YouTubeDownloader.Tests;

public class ComponentInstallerTests
{
    [Theory]
    [InlineData("api")]
    [InlineData("extraction")]
    [InlineData("version-check")]
    public async Task FailedDirectoryUpdatePreservesOldBinariesAndVersion(string failure)
    {
        using var directory = new TestDirectory();
        string installed = directory.PathFor("ffmpeg_bin");
        string version = directory.PathFor("ffmpeg_version.txt");
        Directory.CreateDirectory(installed);
        await File.WriteAllTextAsync(Path.Combine(installed, "ffmpeg"), "old ffmpeg");
        await File.WriteAllTextAsync(Path.Combine(installed, "ffprobe"), "old ffprobe");
        await File.WriteAllTextAsync(version, "old release");
        using var http = new HttpClient(new StubHttpHandler(() => new(HttpStatusCode.Forbidden)));

        await Assert.ThrowsAnyAsync<Exception>(() => ComponentInstaller.InstallDirectoryAsync(installed, async staged =>
        {
            if (failure == "api")
                await GitHubApi.GetStringAsync(http, "https://api.github.com/repos/test/tool/releases/latest");
            await File.WriteAllTextAsync(Path.Combine(staged, "ffmpeg"), "new ffmpeg");
            if (failure == "extraction")
                throw new InvalidDataException("Broken archive.");
        }, _ => Task.FromResult(false), version, "new release"));

        Assert.Equal("old ffmpeg", await File.ReadAllTextAsync(Path.Combine(installed, "ffmpeg")));
        Assert.Equal("old ffprobe", await File.ReadAllTextAsync(Path.Combine(installed, "ffprobe")));
        Assert.Equal("old release", await File.ReadAllTextAsync(version));
        Assert.Equal(2, Directory.GetFileSystemEntries(directory.Root).Length);
    }

    [Fact]
    public async Task FailedSecondMacBinaryDownloadDoesNotReplaceEitherInstalledBinary()
    {
        using var directory = new TestDirectory();
        string installed = directory.PathFor("ffmpeg_bin");
        Directory.CreateDirectory(installed);
        await File.WriteAllTextAsync(Path.Combine(installed, "ffmpeg"), "old ffmpeg");
        await File.WriteAllTextAsync(Path.Combine(installed, "ffprobe"), "old ffprobe");
        int request = 0;
        using var http = new HttpClient(new StubHttpHandler(() => ++request == 1
            ? StubHttpHandler.Bytes(new byte[] { 1, 2, 3 }) : new(HttpStatusCode.ServiceUnavailable)));

        await Assert.ThrowsAsync<HttpRequestException>(() => ComponentInstaller.InstallDirectoryAsync(installed, async staged =>
        {
            await AtomicDownload.DownloadAsync(http, "https://example.invalid/ffmpeg", Path.Combine(staged, "ffmpeg"));
            await AtomicDownload.DownloadAsync(http, "https://example.invalid/ffprobe", Path.Combine(staged, "ffprobe"));
        }, _ => Task.FromResult(true)));

        Assert.Equal("old ffmpeg", await File.ReadAllTextAsync(Path.Combine(installed, "ffmpeg")));
        Assert.Equal("old ffprobe", await File.ReadAllTextAsync(Path.Combine(installed, "ffprobe")));
        Assert.Single(Directory.GetFileSystemEntries(directory.Root));
    }

    [Fact]
    public async Task FailedVersionMarkerCommitRollsBackTheDirectorySwap()
    {
        using var directory = new TestDirectory();
        string installed = directory.PathFor("ffmpeg_bin");
        Directory.CreateDirectory(installed);
        await File.WriteAllTextAsync(Path.Combine(installed, "ffmpeg"), "old executable");
        // A directory at the marker path causes the second commit operation to
        // fail on every OS, after the binary directory has already been swapped.
        string marker = directory.PathFor("ffmpeg_version.txt");
        Directory.CreateDirectory(marker);
        await File.WriteAllTextAsync(Path.Combine(marker, "preserve"), "user file");

        await Assert.ThrowsAnyAsync<Exception>(() => ComponentInstaller.InstallDirectoryAsync(installed,
            staged => File.WriteAllTextAsync(Path.Combine(staged, "ffmpeg"), "new executable"),
            _ => Task.FromResult(true), marker, "new release"));

        Assert.Equal("old executable", await File.ReadAllTextAsync(Path.Combine(installed, "ffmpeg")));
        Assert.Equal("user file", await File.ReadAllTextAsync(Path.Combine(marker, "preserve")));
        Assert.Equal(2, Directory.GetFileSystemEntries(directory.Root).Length);
    }

    [Fact]
    public async Task SuccessfulDirectoryUpdateCommitsBinariesAndMarkerWithoutMixingVersions()
    {
        using var directory = new TestDirectory();
        string installed = directory.PathFor("ffmpeg_bin");
        string marker = directory.PathFor("ffmpeg_version.txt");
        Directory.CreateDirectory(installed);
        await File.WriteAllTextAsync(Path.Combine(installed, "obsolete.dll"), "old library");
        await File.WriteAllTextAsync(marker, "old release");

        await ComponentInstaller.InstallDirectoryAsync(installed, async staged =>
        {
            await File.WriteAllTextAsync(Path.Combine(staged, "ffmpeg"), "new ffmpeg");
            await File.WriteAllTextAsync(Path.Combine(staged, "ffprobe"), "new ffprobe");
            Assert.True(File.Exists(Path.Combine(installed, "obsolete.dll")));
            Assert.Equal("old release", await File.ReadAllTextAsync(marker));
        }, async staged =>
        {
            Assert.Equal("old release", await File.ReadAllTextAsync(marker));
            return await File.ReadAllTextAsync(Path.Combine(staged, "ffprobe")) == "new ffprobe";
        }, marker, "new release");

        Assert.Equal("new release", await File.ReadAllTextAsync(marker));
        Assert.Equal("new ffmpeg", await File.ReadAllTextAsync(Path.Combine(installed, "ffmpeg")));
        Assert.False(File.Exists(Path.Combine(installed, "obsolete.dll")));
        Assert.Equal(2, Directory.GetFileSystemEntries(directory.Root).Length);
    }

    [Fact]
    public async Task InvalidDownloadedExecutableDoesNotReplaceExistingFile()
    {
        using var directory = new TestDirectory();
        string installed = directory.PathFor("deno");
        string marker = directory.PathFor("deno_version.txt");
        await File.WriteAllTextAsync(installed, "working runtime");
        await File.WriteAllTextAsync(marker, "old release");

        await Assert.ThrowsAsync<InvalidDataException>(() => ComponentInstaller.InstallFileAsync(installed,
            staged => File.WriteAllTextAsync(staged, "invalid binary"), _ => Task.FromResult(false), marker, "new release"));

        Assert.Equal("working runtime", await File.ReadAllTextAsync(installed));
        Assert.Equal("old release", await File.ReadAllTextAsync(marker));
        Assert.Equal(2, Directory.GetFileSystemEntries(directory.Root).Length);
    }

    [Fact]
    public async Task SuccessfulFileUpdateChangesTheBinaryAndMarkerAfterVerification()
    {
        using var directory = new TestDirectory();
        string installed = directory.PathFor("deno");
        string marker = directory.PathFor("deno_version.txt");
        await File.WriteAllTextAsync(installed, "old executable");
        await File.WriteAllTextAsync(marker, "old release");

        await ComponentInstaller.InstallFileAsync(installed,
            staged => File.WriteAllTextAsync(staged, "new executable"), async staged =>
            {
                Assert.Equal("old executable", await File.ReadAllTextAsync(installed));
                Assert.Equal("old release", await File.ReadAllTextAsync(marker));
                return await File.ReadAllTextAsync(staged) == "new executable";
            }, marker, "new release");

        Assert.Equal("new executable", await File.ReadAllTextAsync(installed));
        Assert.Equal("new release", await File.ReadAllTextAsync(marker));
        Assert.Equal(2, Directory.GetFileSystemEntries(directory.Root).Length);
    }

    [Fact]
    public async Task FailedVersionMarkerCommitRestoresTheOriginalExecutable()
    {
        using var directory = new TestDirectory();
        string installed = directory.PathFor("deno");
        await File.WriteAllTextAsync(installed, "old executable");
        string marker = directory.PathFor("deno_version.txt");
        Directory.CreateDirectory(marker);

        await Assert.ThrowsAnyAsync<Exception>(() => ComponentInstaller.InstallFileAsync(installed,
            staged => File.WriteAllTextAsync(staged, "new executable"),
            _ => Task.FromResult(true), marker, "new release"));

        Assert.Equal("old executable", await File.ReadAllTextAsync(installed));
        Assert.True(Directory.Exists(marker));
        Assert.Equal(2, Directory.GetFileSystemEntries(directory.Root).Length);
    }

    [Fact]
    public async Task FailedInitialInstallationDoesNotLeaveAnInstalledDirectory()
    {
        using var directory = new TestDirectory();
        string installed = directory.PathFor("ffmpeg_bin");
        await Assert.ThrowsAsync<InvalidDataException>(() => ComponentInstaller.InstallDirectoryAsync(installed,
            _ => Task.CompletedTask, _ => Task.FromResult(false)));
        Assert.False(Directory.Exists(installed));
        Assert.Empty(Directory.GetFileSystemEntries(directory.Root));
    }
}
