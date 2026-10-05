using System.Threading;

namespace YouTubeDownloader;

internal sealed record DownloadBatchResult(int Completed, bool Cancelled);

internal static class DownloadBatch
{
    public static async Task<DownloadBatchResult> RunAsync(IReadOnlyList<string> urls,
        Func<string, int, CancellationToken, Task<bool>> download, CancellationToken cancellationToken)
    {
        int completed = 0;
        try
        {
            for (int i = 0; i < urls.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await download(urls[i], i, cancellationToken))
                    completed++;
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new(completed, false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(completed, true);
        }
    }
}
