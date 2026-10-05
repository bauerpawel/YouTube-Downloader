using Xunit;

namespace YouTubeDownloader.Tests;

public class DownloadBatchTests
{
    [Fact]
    public async Task CancellationStopsTheActiveProcessAndDoesNotStartRemainingUrls()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var visited = new List<string>();

        var result = await DownloadBatch.RunAsync(new[] { "completed", "cancelled", "must-not-start" },
            async (url, index, token) =>
            {
                visited.Add(url);
                if (index == 0)
                    return true;
                await ProcessRunner.RunAsync(ChildProcess.Command("--process-probe", "hang"),
                    _ => cancellation.Cancel(), token);
                return true;
            }, cancellation.Token);

        Assert.Equal(new[] { "completed", "cancelled" }, visited);
        Assert.Equal(1, result.Completed);
        Assert.True(result.Cancelled);
    }

    [Fact]
    public async Task CancellationBeforeTheFirstUrlStartsNoWork()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = await DownloadBatch.RunAsync(new[] { "must-not-start" },
            (_, _, _) => throw new InvalidOperationException("Work was started."), cancellation.Token);

        Assert.Equal(0, result.Completed);
        Assert.True(result.Cancelled);
    }

    [Fact]
    public async Task AFailedUrlDoesNotStopOtherUrlsAndIsNotCountedAsCompleted()
    {
        var visited = new List<string>();
        var result = await DownloadBatch.RunAsync(new[] { "failed", "success" }, (url, _, _) =>
        {
            visited.Add(url);
            return Task.FromResult(url == "success");
        }, CancellationToken.None);

        Assert.Equal(new[] { "failed", "success" }, visited);
        Assert.Equal(1, result.Completed);
        Assert.False(result.Cancelled);
    }

    [Fact]
    public async Task AnUnrelatedCancellationExceptionIsNotReportedAsUserCancellation()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(() => DownloadBatch.RunAsync(new[] { "url" },
            (_, _, _) => throw new OperationCanceledException(), CancellationToken.None));
    }
}
