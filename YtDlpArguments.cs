using System.Diagnostics;

namespace YouTubeDownloader;

// yt-dlp format arguments from language-independent values. The quality list used
// to be interpreted by its (Polish) display text, which translation would break -
// "Best".Replace("p", "") is not a height.
internal static class YtDlpArguments
{
    // Index-aligned with the quality ComboBox items; null = best available.
    public static readonly int?[] QualityHeights = { null, 2160, 1080, 720, 480, 360, 240 };

    public static string QualityLabel(int? maxHeight, string bestLabel) => maxHeight switch
    {
        null => bestLabel,
        2160 => "4K (2160p)",
        _ => $"{maxHeight}p"
    };

    // Always with the runtime's path: outside Windows yt-dlp looks for "deno" /
    // "node" only on PATH (on Windows also next to itself), and the app's own
    // runtime lives in the data folder - without the path yt-dlp ran with
    // "JS runtimes: none" on Linux, macOS and in the snap.
    public static string JsRuntime(string runtimePath)
    {
        string name = Path.GetFileNameWithoutExtension(runtimePath)
            .Equals("deno", StringComparison.OrdinalIgnoreCase) ? "deno" : "node";
        return $"{name}:{runtimePath}";
    }

    public static IReadOnlyList<string> Build(bool audioOnly, int? maxHeight, string format)
    {
        if (audioOnly)
            return new[] { "-f", "bestaudio", "--extract-audio", "--audio-format", "mp3", "--audio-quality", "192" };

        // Select streams that fit the requested container. Remuxing only changes
        // the container; it cannot turn H.264/AAC into WebM-compatible codecs.
        var (video, audio, combined) = format switch
        {
            "webm" => ("bestvideo[ext=webm]", "bestaudio[ext=webm]", "best[ext=webm]"),
            "mp4" => ("bestvideo[ext=mp4]", "bestaudio[ext=m4a]", "best[ext=mp4]"),
            "mkv" => ("bestvideo", "bestaudio", "best"),
            _ => throw new ArgumentException("Unsupported video format.", nameof(format))
        };
        string height = maxHeight.HasValue ? $"[height<={maxHeight.Value}]" : "";
        return new[]
        {
            "-f", $"{video}{height}+{audio}/{combined}{height}",
            "--merge-output-format", format, "--remux-video", format
        };
    }

    public static ProcessStartInfo CreateStartInfo(string executablePath, string runtimePath,
        string ffmpegDirectory, string outputPattern, string normalizedUrl,
        bool audioOnly, int? maxHeight, string format)
    {
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("--js-runtimes");
        startInfo.ArgumentList.Add(JsRuntime(runtimePath));
        foreach (string argument in Build(audioOnly, maxHeight, format))
            startInfo.ArgumentList.Add(argument);

        startInfo.ArgumentList.Add("--ffmpeg-location");
        startInfo.ArgumentList.Add(ffmpegDirectory);
        startInfo.ArgumentList.Add("--progress");
        startInfo.ArgumentList.Add("--newline");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(outputPattern);
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add(normalizedUrl);
        return startInfo;
    }
}
