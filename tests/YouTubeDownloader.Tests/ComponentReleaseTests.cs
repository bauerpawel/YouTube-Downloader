using System.Text.Json;
using Xunit;

namespace YouTubeDownloader.Tests;

public class ComponentReleaseTests
{
    [Theory]
    [InlineData("")]
    [InlineData("sha256:abc")]
    [InlineData("md5:0123456789abcdef")]
    public void RejectsMalformedPublishedDigests(string digest)
    {
        Assert.Throws<InvalidDataException>(() => ComponentRelease.Parse(Release("v1", "deno.zip", digest), "deno.zip"));
    }

    [Fact]
    public void MissingAssetCannotBecomeAnEmptyNewVersion()
    {
        Assert.Throws<InvalidDataException>(() => ComponentRelease.Parse(Release("v1", "other.zip"), "deno.zip"));
    }

    [Fact]
    public void RequiresANonemptyVersion()
    {
        Assert.Throws<InvalidDataException>(() => ComponentRelease.Parse(Release("", "deno.zip"), "deno.zip"));
    }

    [Fact]
    public void RetainsAssetSizeAndPublishedHash()
    {
        string hash = new('a', 64);
        var release = ComponentRelease.Parse(Release("v1", "deno.zip", "sha256:" + hash), "deno.zip");
        Assert.Equal("v1", release.Version);
        Assert.Equal(123, release.Assets[0].Size);
        Assert.Equal(hash, release.Assets[0].Sha256);
    }

    [Fact]
    public void FFmpegVersionComesFromTheReleaseContainingTheSelectedAsset()
    {
        string json = "[" + Release("autobuild-new", "unsupported.zip") + "," +
            Release("autobuild-old", "ffmpeg-linux64-gpl.tar.xz") + "]";
        var release = ComponentRelease.ParseBuilds(json, name => name.EndsWith("linux64-gpl.tar.xz"));
        Assert.Equal("autobuild-old", release.Version);
        Assert.Equal("ffmpeg-linux64-gpl.tar.xz", release.Assets[0].Name);
    }

    private static string Release(string tag, string asset, string? digest = null) => JsonSerializer.Serialize(new
    {
        tag_name = tag,
        assets = new[] { new { name = asset, browser_download_url = "https://example.invalid/asset", size = 123, digest } }
    });
}
