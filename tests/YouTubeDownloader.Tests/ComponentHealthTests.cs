using System.Diagnostics;
using Xunit;

namespace YouTubeDownloader.Tests;

public class ComponentHealthTests
{
    [Theory]
    [InlineData((int)ComponentTool.YtDlp, "yt-dlp")]
    [InlineData((int)ComponentTool.Deno, "deno")]
    [InlineData((int)ComponentTool.Node, "node")]
    [InlineData((int)ComponentTool.FFmpeg, "ffmpeg")]
    [InlineData((int)ComponentTool.FFprobe, "ffprobe")]
    public async Task AcceptsRecognizedSuccessfulVersionOutput(int tool, string output)
    {
        Assert.True(await ComponentHealth.CheckCommandAsync(Probe(output, "success"), (ComponentTool)tool));
    }

    [Fact]
    public async Task RejectsNonzeroExitEvenWhenVersionOutputLooksValid()
    {
        Assert.False(await ComponentHealth.CheckCommandAsync(Probe("yt-dlp", "fail"), ComponentTool.YtDlp));
    }

    [Fact]
    public async Task RejectsUnrecognizedVersionOutput()
    {
        Assert.False(await ComponentHealth.CheckCommandAsync(Probe("unknown", "success"), ComponentTool.YtDlp));
    }

    [Fact]
    public async Task TerminatesAHungVersionCheck()
    {
        Assert.False(await ComponentHealth.CheckCommandAsync(Probe("yt-dlp", "hang"), ComponentTool.YtDlp,
            TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task CallerCancellationIsPropagatedInsteadOfReportingADamagedComponent()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ComponentHealth.CheckCommandAsync(
            Probe("yt-dlp", "hang"), ComponentTool.YtDlp, cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task EmptyFilesAndDirectoriesDoNotEstablishReadiness()
    {
        using var directory = new TestDirectory();
        string executable = directory.PathFor("yt-dlp");
        await File.WriteAllBytesAsync(executable, Array.Empty<byte>());
        Assert.False(await ComponentHealth.CheckExecutableAsync(executable, ComponentTool.YtDlp));
        Assert.False(await ComponentHealth.CheckFFmpegAsync(directory.Root));
    }

    private static ProcessStartInfo Probe(string output, string mode)
    {
        string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
        return new(dotnet)
        {
            ArgumentList = { typeof(Program).Assembly.Location, "--probe-component", output, mode }
        };
    }
}
