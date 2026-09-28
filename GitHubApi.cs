using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace YouTubeDownloader;

// GitHub REST API calls (Deno/FFmpeg release lookup, app self-update). Anonymous
// requests are limited to 60/hour per IP, which shared CI runners (notably macOS)
// exhaust - the app then stops at an error dialog nobody can click. When
// YTD_GITHUB_TOKEN is set (CI passes secrets.GITHUB_TOKEN), requests are
// authenticated. Users normally never set it, so nothing changes for them.
internal static class GitHubApi
{
    public const string TokenVariable = "YTD_GITHUB_TOKEN";

    private const string ApiHost = "api.github.com";

    public static HttpRequestMessage CreateRequest(string url, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        // Only ever to the API host itself - never to download/redirect hosts.
        if (!string.IsNullOrEmpty(token) &&
            string.Equals(new Uri(url).Host, ApiHost, StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    public static async Task<string> GetStringAsync(HttpClient http, string url)
    {
        using var request = CreateRequest(url, Environment.GetEnvironmentVariable(TokenVariable));
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
