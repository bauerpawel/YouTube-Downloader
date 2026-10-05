using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;

namespace YouTubeDownloader;

internal static class AtomicDownload
{
    public static async Task DownloadAsync(HttpClient http, string url, string destination,
        Action<int>? onProgress = null, long? expectedSize = null, string? sha256 = null,
        CancellationToken cancellationToken = default)
    {
        string partialPath = destination + ".part-" + Guid.NewGuid().ToString("N");
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            long? contentLength = response.Content.Headers.ContentLength;
            if (expectedSize.HasValue && contentLength.HasValue && expectedSize != contentLength)
                throw new InvalidDataException("Download size differs from the release metadata.");

            using var hash = sha256 == null ? null : IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long totalRead = 0;
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) != 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    hash?.AppendData(buffer, 0, read);
                    totalRead += read;
                    long? size = expectedSize ?? contentLength;
                    if (size > 0)
                        onProgress?.Invoke((int)Math.Min(100, totalRead * 100 / size.Value));
                }
            }

            if (totalRead == 0 || (expectedSize.HasValue && totalRead != expectedSize) ||
                (contentLength.HasValue && totalRead != contentLength))
                throw new InvalidDataException("Download is empty or incomplete.");
            if (hash != null && !Convert.ToHexString(hash.GetHashAndReset()).Equals(sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Download SHA-256 does not match the release metadata.");

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partialPath, destination, overwrite: true);
        }
        finally
        {
            AppPaths.TryDeleteFile(partialPath);
        }
    }
}
