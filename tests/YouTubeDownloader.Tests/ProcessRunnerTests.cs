using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace YouTubeDownloader.Tests;

public class ProcessRunnerTests
{
    [Fact]
    public async Task DrainsLargeStdoutAndStderrWithoutDeadlockAndPreservesExitCode()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var lines = new List<string>();
        var (code, error) = await ProcessRunner.RunAsync(ChildProcess.Command("--process-probe", "large-output"),
            lines.Add, deadline.Token);

        Assert.Equal(7, code);
        Assert.Equal(4096, lines.Count);
        Assert.Equal("4095", lines[^1]);
        Assert.Equal(4096, error.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.True(error.Length > 500_000);
    }

    [Fact]
    public async Task AlreadyCancelledTokenDoesNotStartTheExecutable()
    {
        using var directory = new TestDirectory();
        string marker = directory.PathFor("started");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ProcessRunner.RunAsync(
            ChildProcess.Command("--process-probe", "marker", marker), cancellationToken: cancellation.Token));
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public async Task CancellationStopsTheParentAndItsChildBeforeReturning()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var started = new TaskCompletionSource<int[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = ProcessRunner.RunAsync(ChildProcess.Command("--process-probe", "tree"),
            line => started.TrySetResult(JsonSerializer.Deserialize<int[]>(line)!), cancellation.Token);
        int[]? pids = null;
        try
        {
            pids = await started.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.All(pids, pid => Assert.True(IsRunning(pid)));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.All(pids, pid => Assert.False(IsRunning(pid)));
        }
        finally
        {
            cancellation.Cancel();
            if (pids != null)
                foreach (int pid in pids)
                    KillIfRunning(pid);
            try { await running; } catch (OperationCanceledException) { }
        }
    }

    private static bool IsRunning(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (process.HasExited)
                return false;
            if (OperatingSystem.IsLinux())
            {
                // An orphan reaped later by PID 1 is already stopped.
                string stat = File.ReadAllText($"/proc/{pid}/stat");
                if (stat[(stat.LastIndexOf(')') + 2)] == 'Z')
                    return false;
            }
            return true;
        }
        catch (Exception error) when (error is ArgumentException or IOException or InvalidOperationException)
        {
            return false;
        }
    }

    private static void KillIfRunning(int pid)
    {
        if (!IsRunning(pid))
            return;
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill(entireProcessTree: true);
        }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
    }
}
