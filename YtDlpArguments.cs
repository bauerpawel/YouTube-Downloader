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
        return $"--js-runtimes \"{name}:{runtimePath}\"";
    }

    public static string Build(bool audioOnly, int? maxHeight, string format)
    {
        if (audioOnly)
            return " -f bestaudio --extract-audio --audio-format mp3 --audio-quality 192";

        string args = maxHeight == null
            ? " -f bestvideo+bestaudio/best"
            : $" -f bestvideo[height<={maxHeight}]+bestaudio/best[height<={maxHeight}]";

        return args + format switch
        {
            "mp4" => " --remux-video mp4",
            "webm" => " --remux-video webm",
            "mkv" => " --merge-output-format mkv",
            _ => ""
        };
    }
}
