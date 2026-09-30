using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace YouTubeDownloader;

// Runs an external tool with both output streams drained while it runs. A
// redirected stream nobody reads fills its pipe (a few KB on Windows) and the
// tool then blocks forever - with many yt-dlp warnings or a failing tar listing
// its errors, the app waited for a download or an extraction that never ended.
internal static class ProcessRunner
{
    public static async Task<(int ExitCode, string StandardError)> RunAsync(
        ProcessStartInfo startInfo, Action<string>? onOutputLine = null)
    {
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                onOutputLine?.Invoke(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        Task<string> standardError = process.StandardError.ReadToEndAsync();

        // Also waits for the stdout events to reach end of stream.
        await process.WaitForExitAsync();
        return (process.ExitCode, await standardError);
    }
}
