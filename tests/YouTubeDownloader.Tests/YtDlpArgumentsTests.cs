using System.Text.Json;
using Xunit;

namespace YouTubeDownloader.Tests;

public class YtDlpArgumentsTests
{
    [Theory]
    [InlineData("mp4", "bestvideo[ext=mp4][height<=720]+bestaudio[ext=m4a]/best[ext=mp4][height<=720]")]
    [InlineData("webm", "bestvideo[ext=webm][height<=720]+bestaudio[ext=webm]/best[ext=webm][height<=720]")]
    [InlineData("mkv", "bestvideo[height<=720]+bestaudio/best[height<=720]")]
    public void LimitsBothSeparateAndCombinedVideoStreams(string format, string selector)
    {
        Assert.Equal(new[]
        {
            "-f", selector, "--merge-output-format", format, "--remux-video", format
        }, YtDlpArguments.Build(false, 720, format));
    }

    [Fact]
    public void BestMp4UsesCompatibleStreamsWithoutAHeightLimit()
    {
        Assert.Equal(new[] { "-f", "bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]",
            "--merge-output-format", "mp4", "--remux-video", "mp4" },
            YtDlpArguments.Build(false, null, "mp4"));
    }

    [Fact]
    public void UnknownVideoFormatIsRejectedBeforeStartingAProcess()
    {
        Assert.Throws<ArgumentException>(() => YtDlpArguments.Build(false, null, "unknown"));
    }

    [Fact]
    public void PreservesAudioExtractionOptions()
    {
        Assert.Equal(new[] { "-f", "bestaudio", "--extract-audio", "--audio-format", "mp3", "--audio-quality", "192" },
            YtDlpArguments.Build(true, 720, "webm"));
    }

    [Theory]
    [InlineData("deno", "deno")]
    [InlineData("deno.exe", "deno")]
    [InlineData("node", "node")]
    [InlineData("node.exe", "node")]
    public void PassesRuntimeAndPathsAsIndividualArguments(string executable, string runtime)
    {
        string runtimePath = Path.Combine("tools with spaces", executable);
        const string ffmpegPath = "ffmpeg & tools";
        const string output = "downloads with spaces/%(title)s.%(ext)s";
        const string url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=test";
        var info = YtDlpArguments.CreateStartInfo("yt-dlp", runtimePath, ffmpegPath, output, url, false, null, "mp4");

        Assert.False(info.UseShellExecute);
        Assert.Empty(info.Arguments);
        Assert.Equal(new[]
        {
            "--js-runtimes", $"{runtime}:{runtimePath}",
            "-f", "bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]",
            "--merge-output-format", "mp4", "--remux-video", "mp4",
            "--ffmpeg-location", ffmpegPath, "--progress", "--newline", "-o", output, "--", url
        }, info.ArgumentList);
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ\" --exec \"echo injected")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&data=%22%20--exec%20%22echo")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&value=$()\\quoted")]
    public async Task UrlAndPathsSurviveARealProcessBoundaryWithoutAddingOptions(string url)
    {
        string runtimePath = Path.Combine("tools & \"quoted\" %value%", "deno");
        const string ffmpegPath = "ffmpeg folder with spaces\\";
        const string output = "downloads & \"quoted\"/%(title)s.%(ext)s";
        string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
        var info = YtDlpArguments.CreateStartInfo(dotnet, runtimePath, ffmpegPath, output, url, false, 720, "mp4");
        string[] expected = info.ArgumentList.ToArray();
        // Execute only our harmless argument-echo entry point, never yt-dlp or --exec.
        info.ArgumentList.Insert(0, typeof(Program).Assembly.Location);
        string? outputLine = null;

        var (exitCode, error) = await ProcessRunner.RunAsync(info, line => outputLine = line);

        Assert.Equal(0, exitCode);
        Assert.Empty(error);
        Assert.NotNull(outputLine);
        Assert.Equal(expected, JsonSerializer.Deserialize<string[]>(outputLine));
    }
}
