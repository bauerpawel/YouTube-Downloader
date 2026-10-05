using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace YouTubeDownloader;

internal enum ComponentTool { YtDlp, Deno, Node, FFmpeg, FFprobe }

internal sealed record ComponentReadiness(bool YtDlp, bool FFmpeg, bool FFprobe, bool Runtime)
{
    public bool IsReady => YtDlp && FFmpeg && FFprobe && Runtime;
}

internal static class ComponentHealth
{
    public static Task<bool> CheckExecutableAsync(string path, ComponentTool tool,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                return Task.FromResult(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(false);
        }

        var command = new ProcessStartInfo(path) { CreateNoWindow = true };
        command.ArgumentList.Add(tool is ComponentTool.FFmpeg or ComponentTool.FFprobe ? "-version" : "--version");
        return CheckCommandAsync(command, tool, cancellationToken: cancellationToken);
    }

    public static async Task<bool> CheckCommandAsync(ProcessStartInfo command, ComponentTool tool,
        TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Initial tool startup under Rosetta can exceed 15 seconds (including
        // Deno and yt-dlp). Keep checks bounded without rejecting those valid
        // binaries. User cancellation still interrupts the check immediately.
        var deadline = timeout ?? (OperatingSystem.IsMacOS() &&
            RuntimeInformation.ProcessArchitecture == Architecture.X64
                ? TimeSpan.FromSeconds(90) : TimeSpan.FromSeconds(15));
        cancellation.CancelAfter(deadline);
        var elapsed = Stopwatch.StartNew();
        bool recognized = false;
        string output = "";
        try
        {
            var (code, error) = await ProcessRunner.RunAsync(command, line =>
            {
                if (output.Length < 4096)
                    output += line + Environment.NewLine;
                recognized |= tool switch
                {
                    ComponentTool.YtDlp => Regex.IsMatch(line, @"\A\d{4}\.\d{2}\.\d{2}(?:\D|$)"),
                    ComponentTool.Deno => line.StartsWith("deno ", StringComparison.OrdinalIgnoreCase),
                    ComponentTool.Node => Regex.IsMatch(line, @"\Av\d+\.\d+\.\d+"),
                    ComponentTool.FFmpeg => line.StartsWith("ffmpeg version ", StringComparison.OrdinalIgnoreCase),
                    ComponentTool.FFprobe => line.StartsWith("ffprobe version ", StringComparison.OrdinalIgnoreCase),
                    _ => false
                };
            }, cancellation.Token);
            if (code != 0 || !recognized)
                Console.WriteLine($"[component] {tool}: version check failed (exit {code}). " +
                    $"stdout: {output.Trim()} stderr: {error[..Math.Min(error.Length, 4096)].Trim()}");
            else if (elapsed.Elapsed >= TimeSpan.FromSeconds(15))
                Console.WriteLine($"[component] {tool}: version check passed in {elapsed.Elapsed.TotalSeconds:F1} seconds.");
            return code == 0 && recognized;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[component] {tool}: version check timed out after {deadline.TotalSeconds} seconds. " +
                $"stdout: {output.Trim()}");
            return false;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or
            UnauthorizedAccessException or InvalidOperationException)
        {
            Console.WriteLine($"[component] {tool}: version check failed: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> CheckFFmpegAsync(string directory)
    {
        string extension = OperatingSystem.IsWindows() ? ".exe" : "";
        var checks = await Task.WhenAll(
            CheckExecutableAsync(Path.Combine(directory, "ffmpeg" + extension), ComponentTool.FFmpeg),
            CheckExecutableAsync(Path.Combine(directory, "ffprobe" + extension), ComponentTool.FFprobe));
        return checks.All(usable => usable);
    }
}
