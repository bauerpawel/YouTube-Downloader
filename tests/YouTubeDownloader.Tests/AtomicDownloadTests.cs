using System.Net;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace YouTubeDownloader.Tests;

public class AtomicDownloadTests
{
    [Theory]
    [InlineData("interrupted")]
    [InlineData("truncated")]
    [InlineData("empty")]
    [InlineData("digest")]
    [InlineData("metadata-size")]
    [InlineData("http-error")]
    public async Task FailedDownloadPreservesDestinationAndRemovesPartialFiles(string failure)
    {
        using var directory = new TestDirectory();
        string destination = directory.PathFor("yt-dlp");
        await File.WriteAllTextAsync(destination, "working old version");
        byte[] bytes = Encoding.UTF8.GetBytes("new executable");
        using var http = new HttpClient(new StubHttpHandler(() =>
        {
            if (failure == "http-error")
                return new(HttpStatusCode.ServiceUnavailable);
            if (failure == "interrupted")
                return new(HttpStatusCode.OK) { Content = new StreamContent(new InterruptedStream(bytes)) };
            var response = StubHttpHandler.Bytes(failure == "empty" ? Array.Empty<byte>() : bytes);
            if (failure == "truncated")
                response.Content.Headers.ContentLength = bytes.Length + 5;
            return response;
        }));

        await Assert.ThrowsAnyAsync<Exception>(() => AtomicDownload.DownloadAsync(http,
            "https://example.invalid/asset", destination,
            expectedSize: failure == "metadata-size" ? bytes.Length + 1 : null,
            sha256: failure == "digest" ? new string('0', 64) : null));

        Assert.Equal("working old version", await File.ReadAllTextAsync(destination));
        Assert.Equal(new[] { destination }, Directory.GetFileSystemEntries(directory.Root));
    }

    [Fact]
    public async Task VerifiedDownloadReplacesDestinationOnlyAfterAllBytesArrive()
    {
        using var directory = new TestDirectory();
        string destination = directory.PathFor("yt-dlp");
        await File.WriteAllTextAsync(destination, "old version");
        byte[] bytes = Encoding.UTF8.GetBytes("new executable");
        using var http = new HttpClient(new StubHttpHandler(() => StubHttpHandler.Bytes(bytes)));

        await AtomicDownload.DownloadAsync(http, "https://example.invalid/asset", destination,
            progress => Assert.Equal("old version", File.ReadAllText(destination)),
            bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));

        Assert.Equal(bytes, await File.ReadAllBytesAsync(destination));
        Assert.Equal(new[] { destination }, Directory.GetFileSystemEntries(directory.Root));
    }

    [Fact]
    public async Task CancellationDoesNotCreateAnInstalledFile()
    {
        using var directory = new TestDirectory();
        string destination = directory.PathFor("yt-dlp");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var http = new HttpClient(new StubHttpHandler(() => StubHttpHandler.Bytes(new byte[] { 1 })));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AtomicDownload.DownloadAsync(http,
            "https://example.invalid/asset", destination, cancellationToken: cancellation.Token));
        Assert.Empty(Directory.GetFileSystemEntries(directory.Root));
    }

    private sealed class InterruptedStream(byte[] bytes) : Stream
    {
        private bool read;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (read)
                throw new IOException("Connection interrupted after the first bytes.");
            read = true;
            bytes.AsMemory().CopyTo(buffer);
            return ValueTask.FromResult(bytes.Length);
        }
        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).GetAwaiter().GetResult();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
