using Xunit;

namespace YouTubeDownloader.Tests;

public class YouTubeUrlTests
{
    [Theory]
    [InlineData("dQw4w9WgXcQ", "https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("  dQw4w9WgXcQ  ", "https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("youtube.com/watch?v=dQw4w9WgXcQ", "https://youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("www.youtube.com/watch?v=dQw4w9WgXcQ", "https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("m.youtube.com/watch?v=dQw4w9WgXcQ", "https://m.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("youtu.be/dQw4w9WgXcQ", "https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("HTTPS://WWW.YOUTUBE.COM/watch?v=dQw4w9WgXcQ", "https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("http://www.youtube.com/watch?v=dQw4w9WgXcQ", "http://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ", "https://music.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", "https://www.youtube.com/shorts/dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=12", "https://youtu.be/dQw4w9WgXcQ?t=12")]
    public void NormalizesSupportedLinks(string input, string expected)
    {
        Assert.True(YouTubeUrl.TryNormalize(input, out var actual, out var error));
        Assert.Equal(expected, actual);
        Assert.Equal(YouTubeUrlError.None, error);
    }

    [Theory]
    [InlineData("https://youtube.com.example.org/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be.example.org/dQw4w9WgXcQ")]
    [InlineData("https://notyoutube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://notyoutu.be/dQw4w9WgXcQ")]
    [InlineData("https://yоutube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com%2eexample.org/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://example.org/youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://example.org/?redirect=youtube.com")]
    [InlineData("https://example.org/#youtu.be")]
    [InlineData("https://youtube.com@example.org/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://user:password@www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com:8443/watch?v=dQw4w9WgXcQ")]
    [InlineData("ftp://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("file:///youtube.com/video.mp4")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ\" --exec \"echo injected")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ --exec=echo")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ\n--exec=echo")]
    [InlineData("https://www.youtube.com\\@example.org/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ\0")]
    public void RejectsForeignHostsAndMalformedLinks(string input)
    {
        Assert.False(YouTubeUrl.TryNormalize(input, out var url, out var error));
        Assert.Equal("", url);
        Assert.NotEqual(YouTubeUrlError.None, error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n")]
    public void ReportsEmptyInput(string? input)
    {
        Assert.False(YouTubeUrl.TryNormalize(input, out _, out var error));
        Assert.Equal(YouTubeUrlError.Empty, error);
    }

    [Fact]
    public void PreservesEncodedQueryDataAsUrlData()
    {
        const string input = "https://www.youtube.com/watch?v=dQw4w9WgXcQ&data=%22%20--exec%20%22echo%20test";
        Assert.True(YouTubeUrl.TryNormalize(input, out var actual, out _));
        Assert.Equal(input, actual);
    }
}
