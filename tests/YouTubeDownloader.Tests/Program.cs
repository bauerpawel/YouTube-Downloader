using System.Text.Json;
using System.Diagnostics;

namespace YouTubeDownloader.Tests;

// A harmless child process for verifying argument boundaries on each OS.
internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length >= 3 && args[0] == "--media-arguments")
        {
            int? height = args[2] == "best" ? null : int.Parse(args[2]);
            Console.WriteLine(JsonSerializer.Serialize(YtDlpArguments.Build(args[1] == "mp3", height, args[1])));
            return 0;
        }
        if (args.Length >= 2 && args[0] == "--process-probe")
        {
            switch (args[1])
            {
                case "large-output":
                    for (int i = 0; i < 4096; i++)
                    {
                        Console.Error.WriteLine(new string('e', 128));
                        Console.WriteLine(i);
                    }
                    return 7;
                case "marker":
                    File.WriteAllText(args[2], "started");
                    return 0;
                case "tree":
                    using (var child = Process.Start(ChildProcess.Command("--process-probe", "child"))!)
                    {
                        Console.WriteLine(JsonSerializer.Serialize(new[] { Environment.ProcessId, child.Id }));
                        child.WaitForExit();
                    }
                    return 0;
                case "child":
                    Thread.Sleep(Timeout.Infinite);
                    return 0;
                case "hang":
                    Console.WriteLine("started");
                    Thread.Sleep(Timeout.Infinite);
                    return 0;
            }
        }
        if (args.Length >= 1 && args[0] == "--data-directory")
        {
            Console.WriteLine(AppPaths.DataDirectory);
            return 0;
        }
        if (args.Length >= 3 && args[0] == "--probe-component")
        {
            Console.WriteLine(args[1] switch
            {
                "yt-dlp" => "2026.08.19",
                "deno" => "deno 2.5.0",
                "node" => "v24.1.0",
                "ffmpeg" => "ffmpeg version 7.1",
                "ffprobe" => "ffprobe version 7.1",
                _ => "unrecognized output"
            });
            if (args[2] == "hang")
                Thread.Sleep(Timeout.Infinite);
            return args[2] == "fail" ? 1 : 0;
        }
        Console.WriteLine(JsonSerializer.Serialize(args));
        return 0;
    }
}

internal static class ChildProcess
{
    public static ProcessStartInfo Command(params string[] arguments)
    {
        var command = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false,
            ArgumentList = { typeof(Program).Assembly.Location }
        };
        foreach (string argument in arguments)
            command.ArgumentList.Add(argument);
        return command;
    }
}
