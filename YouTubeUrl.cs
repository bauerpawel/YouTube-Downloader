using System.Text.RegularExpressions;

namespace YouTubeDownloader;

internal enum YouTubeUrlError
{
    None,
    Empty,
    Invalid,
    UnsupportedHost
}

internal static class YouTubeUrl
{
    public static bool TryNormalize(string? input, out string normalizedUrl, out YouTubeUrlError error)
    {
        normalizedUrl = "";
        error = YouTubeUrlError.Invalid;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = YouTubeUrlError.Empty;
            return false;
        }

        string candidate = input.Trim();
        // These characters must be percent-encoded in a URL, not interpreted as
        // command-line syntax or as URI authority/path separators.
        if (candidate.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '"' or '\\'))
            return false;

        if (Regex.IsMatch(candidate, @"\A[a-zA-Z0-9_-]{11}\z"))
            candidate = "https://www.youtube.com/watch?v=" + candidate;
        else if (!candidate.Contains("://", StringComparison.Ordinal))
            candidate = "https://" + candidate;

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            uri.UserInfo.Length != 0 || !uri.IsDefaultPort)
            return false;

        // Allow youtube.com and its subdomains only at a DNS label boundary;
        // never match domain text in a path, query, userinfo or lookalike host.
        string host = uri.Host;
        if (!host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) &&
            !host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase) &&
            !host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            error = YouTubeUrlError.UnsupportedHost;
            return false;
        }

        normalizedUrl = uri.AbsoluteUri;
        error = YouTubeUrlError.None;
        return true;
    }
}
