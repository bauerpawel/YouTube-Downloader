using System.Text.Json;

namespace YouTubeDownloader;

internal sealed record ComponentRelease(string Version, IReadOnlyList<ReleaseAsset> Assets)
{
    public static ComponentRelease Parse(string json, params string[] assetNames)
    {
        using var document = JsonDocument.Parse(json);
        return Read(document.RootElement, assetNames);
    }

    public static ComponentRelease ParseBuilds(string json, Func<string, bool> matches)
    {
        using var document = JsonDocument.Parse(json);
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (!(release.GetProperty("tag_name").GetString() ?? "").StartsWith("autobuild-", StringComparison.Ordinal))
                continue;
            foreach (var asset in release.GetProperty("assets").EnumerateArray())
            {
                string name = asset.GetProperty("name").GetString() ?? "";
                if (matches(name))
                    return Read(release, new[] { name });
            }
        }
        throw new InvalidDataException("No matching FFmpeg release asset was found.");
    }

    private static ComponentRelease Read(JsonElement release, IReadOnlyList<string> names)
    {
        string version = release.GetProperty("tag_name").GetString() ?? "";
        if (string.IsNullOrWhiteSpace(version) || names.Count == 0)
            throw new InvalidDataException("Release version or assets are missing.");
        var result = new List<ReleaseAsset>();
        foreach (string name in names)
        {
            var assets = release.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString() == name).ToArray();
            if (assets.Length != 1)
                throw new InvalidDataException($"Missing or ambiguous release asset: {name}.");
            var asset = assets[0];
            string url = asset.GetProperty("browser_download_url").GetString() ?? "";
            long size = asset.GetProperty("size").GetInt64();
            if (size <= 0 || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidDataException($"Invalid release asset metadata: {name}.");

            string? hash = null;
            if (asset.TryGetProperty("digest", out var digest) && digest.ValueKind != JsonValueKind.Null)
            {
                string value = digest.GetString() ?? "";
                if (!value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ||
                    value.Length != 71 || !value[7..].All(Uri.IsHexDigit))
                    throw new InvalidDataException($"Invalid SHA-256 digest: {name}.");
                hash = value[7..];
            }
            result.Add(new(name, url, size, hash));
        }
        return new(version, result);
    }
}
