# English UI (PL/EN) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every user-visible text available in Polish (now with proper diacritics) and English, chosen automatically from the system language on first run, switchable live from the View menu and remembered in the data directory.

**Architecture:** Three new Avalonia-free files: `Strings.cs` (one `required` property per text, a Polish and an English instance, parameterized texts as `Func<>`s), `LanguageSettings.cs` (resolve/load/save the language code, like `ThemeSettings`), `YtDlpArguments.cs` (quality -> yt-dlp args by value, not by display text). Windows set their texts in code (`ApplyTexts()` in `MainWindow`, constructors in `AboutWindow`/`MessageDialog`) from `Strings.Current`; XAML keeps no user-facing text.

**Tech Stack:** .NET 10, C# 13 (`required` members), Avalonia UI 12.1.2. No new NuGet packages.

**Spec:** `docs/superpowers/specs/2026-09-28-english-ui-design.md`

## Global Constraints

- Branch: `claude/english-ui` (already created; the spec is committed there). Commit this plan as the first step.
- Languages: `pl` and `en` only. `LanguageSettings.Resolve`: a saved `pl`/`en` wins; otherwise system culture language `pl` -> `pl`, anything else -> `en`.
- Polish texts use proper diacritics (`Błąd`, `Narzędzia`, `Jakość`, `Sprawdź aktualizacje aplikacji`). English texts: short, sentence case, standard desktop wording.
- Language names in the menu are always in their own language: `Polski`, `English`.
- Not translated: yt-dlp output, `ex.Message` from .NET, the default URL text `https://www.youtube.com/watch?v=`, the `[status]`/`[dialog]` stdout prefixes, the window title `YouTube Downloader`, format names `mp4`/`webm`/`mkv`, the `+`/`-` buttons, `PlatformNotSupportedException` messages (developer-facing, unreachable on supported OSes).
- On a live language switch the current status line is **not** re-translated.
- `yt-dlp` arguments must stay byte-for-byte identical for every quality/format/audio combination.
- `Strings.cs`, `LanguageSettings.cs`, `YtDlpArguments.cs` must not reference `Avalonia.*` - they are compiled into the scratch harness.
- Tests live only in the scratch harness `C:\Users\pawel\AppData\Local\Temp\claude\E--claude-YouTube-Downloader\004f1ae7-05cc-4f13-9798-d98989b0ce7c\scratchpad\ytd-tests` (`$HARNESS`), never in the repo.
- Event handlers are wired in constructors, never via XAML `Click=`.
- `dotnet build` 0 warnings / 0 errors in Debug and Release.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **English download with the default quality** - "Best" must still produce `-f bestvideo+bestaudio/best`, not `height<=Best`. Pinned by Task 1's characterization tests (every combination compared with the old logic).
2. **Language switched while extra URL rows exist / a quality other than the first is selected** - rows get the new placeholder, the selection survives. Pinned by Task 3's code in `ApplyTexts()` and the human check.
3. **Missing or empty translation in one language** - must fail the build or the harness, never show an empty label. Pinned by `required` (compile) and Task 2's completeness test.
4. **Diacritic-less leftovers or Polish text leaking into English** - Pinned by Task 2's leftover-word and no-Polish-letters tests plus Task 3's grep of `MainWindow.axaml.cs` for old literals.
5. **Corrupt or unexpected `language.txt`** (e.g. `EN`, `de`, whitespace, unreadable) - falls back to the system language, no crash. Pinned by Task 2's `LanguageSettings` tests.

---

## Task 1: `YtDlpArguments` - quality by value, not by display text

**Files:**
- Create: `YtDlpArguments.cs`
- Modify: `MainWindow.axaml.cs` (`BuildYtDlpArguments()`, currently lines 823-869)
- Modify (scratch): `$HARNESS/ytd-tests.csproj`, `$HARNESS/Program.cs`; create `$HARNESS/YtDlpArgumentsTests.cs`

**Interfaces:**
- Produces (`internal static class YtDlpArguments`, namespace `YouTubeDownloader`):
  - `static readonly int?[] QualityHeights = { null, 2160, 1080, 720, 480, 360, 240 }` - index-aligned with the quality list
  - `static string QualityLabel(int? maxHeight, string bestLabel)` - `null` -> `bestLabel`, `2160` -> `"4K (2160p)"`, else `"{h}p"`
  - `static string Build(bool audioOnly, int? maxHeight, string format)`

- [ ] **Step 0: Commit the plan**

```bash
git add docs/superpowers/plans/2026-09-28-english-ui.md
git commit -m "Add implementation plan: English UI (PL/EN)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 1: Write the characterization tests**

In `$HARNESS/ytd-tests.csproj`, next to the other `<Compile Include>` lines, add:
```xml
    <Compile Include="E:\claude\YouTube-Downloader\YtDlpArguments.cs" Link="YtDlpArguments.cs" />
```
In `$HARNESS/Program.cs`, after `GitHubApiTests.Run(Check);` add:
```csharp
YtDlpArgumentsTests.Run(Check);
```

`$HARNESS/YtDlpArgumentsTests.cs`:
```csharp
using YouTubeDownloader;

static class YtDlpArgumentsTests
{
    // Verbatim copy of MainWindow.BuildYtDlpArguments() as of commit 30df83f, with the
    // ComboBox reads replaced by parameters - the reference the new code must match.
    static string OldBuild(bool audioOnly, string quality, string format)
    {
        string args = "";

        if (audioOnly)
        {
            args = " -f bestaudio --extract-audio --audio-format mp3 --audio-quality 192";
        }
        else
        {
            if (quality == "Najlepsza")
            {
                args = " -f bestvideo+bestaudio/best";
            }
            else if (quality == "4K (2160p)")
            {
                args = " -f bestvideo[height<=2160]+bestaudio/best[height<=2160]";
            }
            else if (quality == "1080p")
            {
                args = " -f bestvideo[height<=1080]+bestaudio/best[height<=1080]";
            }
            else
            {
                string heightStr = quality.Replace("p", "");
                args = " -f bestvideo[height<=" + heightStr + "]+bestaudio/best[height<=" + heightStr + "]";
            }

            if (format == "mp4")
            {
                args += " --remux-video mp4";
            }
            else if (format == "webm")
            {
                args += " --remux-video webm";
            }
            else if (format == "mkv")
            {
                args += " --merge-output-format mkv";
            }
        }

        return args;
    }

    public static void Run(Action<bool, string> check)
    {
        string[] oldLabels = { "Najlepsza", "4K (2160p)", "1080p", "720p", "480p", "360p", "240p" };
        string[] formats = { "mp4", "webm", "mkv" };

        check(YtDlpArguments.QualityHeights.Length == oldLabels.Length, "ytdlp: one height per quality item");

        var mismatches = new List<string>();
        for (int i = 0; i < oldLabels.Length; i++)
        {
            foreach (string format in formats)
            {
                foreach (bool audioOnly in new[] { false, true })
                {
                    string expected = OldBuild(audioOnly, oldLabels[i], format);
                    string actual = YtDlpArguments.Build(audioOnly, YtDlpArguments.QualityHeights[i], format);
                    if (expected != actual)
                        mismatches.Add($"{oldLabels[i]}/{format}/audio={audioOnly}: '{actual}' != '{expected}'");
                }
            }
        }
        check(mismatches.Count == 0, "ytdlp: identical to old arguments for all 42 combinations " + string.Join("; ", mismatches));

        check(YtDlpArguments.Build(false, null, "mp4") == " -f bestvideo+bestaudio/best --remux-video mp4",
            "ytdlp: best quality literal");
        check(YtDlpArguments.Build(false, 720, "mkv") == " -f bestvideo[height<=720]+bestaudio/best[height<=720] --merge-output-format mkv",
            "ytdlp: 720p mkv literal");

        check(Enumerable.Range(0, oldLabels.Length).All(i => YtDlpArguments.QualityLabel(YtDlpArguments.QualityHeights[i], "Najlepsza") == oldLabels[i]),
            "ytdlp: Polish quality labels unchanged");
        check(YtDlpArguments.QualityLabel(null, "Best") == "Best", "ytdlp: best label comes from the caller (translated)");
    }
}
```

- [ ] **Step 2: Run the harness to verify it fails**

Run: `dotnet run --project "$HARNESS"`
Expected: `CS2001` - `YtDlpArguments.cs` could not be found.

- [ ] **Step 3: Implement `YtDlpArguments.cs`**

```csharp
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
```

- [ ] **Step 4: Run the harness to verify it passes**

Run: `dotnet run --project "$HARNESS"`
Expected: all `PASS`, `ALL PASSED`.

- [ ] **Step 5: Use it in `MainWindow`**

Replace the whole `BuildYtDlpArguments()` method (from `private string BuildYtDlpArguments()` through its closing `return args;` and `}`) with:
```csharp
    private string BuildYtDlpArguments()
    {
        int qualityIndex = Math.Max(CbQuality.SelectedIndex, 0);
        return YtDlpArguments.Build(
            CbContentType.SelectedIndex == 1,
            YtDlpArguments.QualityHeights[qualityIndex],
            CbFormat.SelectedItem?.ToString() ?? "mp4");
    }
```
In the constructor, replace the seven `CbQuality.Items.Add(...)` lines with:
```csharp
        foreach (int? height in YtDlpArguments.QualityHeights)
            CbQuality.Items.Add(YtDlpArguments.QualityLabel(height, "Najlepsza"));
```
(Task 3 moves this into `ApplyTexts()` with the translated label.)

- [ ] **Step 6: Build**

Run: `dotnet build YouTubeDownloader.csproj -c Debug -warnaserror`
Expected: 0 warnings, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add YtDlpArguments.cs MainWindow.axaml.cs
git commit -m "Build yt-dlp arguments from quality values, not display text

The quality list was interpreted by its Polish label (\"Najlepsza\",
\"4K (2160p)\", Replace(\"p\", \"\")), which a translated label would break.
Arguments are unchanged for all 42 combinations (characterization test).

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Task 2: `Strings` and `LanguageSettings`

**Files:**
- Create: `Strings.cs`, `LanguageSettings.cs`
- Modify (scratch): `$HARNESS/ytd-tests.csproj`, `$HARNESS/Program.cs`; create `$HARNESS/StringsTests.cs`, `$HARNESS/LanguageSettingsTests.cs`

**Interfaces:**
- Produces:
  - `internal static class LanguageSettings`: `const string Polish = "pl"`, `const string English = "en"`, `string GetPath(string directory)`, `string? Load(string directory)`, `void Save(string directory, string code)`, `string Resolve(string? saved, CultureInfo systemCulture)`
  - `internal sealed class Strings`: the properties listed in Step 3 (names are the interface), `const string LanguageNamePolish = "Polski"`, `const string LanguageNameEnglish = "English"`, `static Strings Polish`, `static Strings English`, `static Strings Current { get; set; }`, `static Strings For(string languageCode)`

- [ ] **Step 1: Write the failing tests**

In `$HARNESS/ytd-tests.csproj` add:
```xml
    <Compile Include="E:\claude\YouTube-Downloader\Strings.cs" Link="Strings.cs" />
    <Compile Include="E:\claude\YouTube-Downloader\LanguageSettings.cs" Link="LanguageSettings.cs" />
```
In `$HARNESS/Program.cs`, after `YtDlpArgumentsTests.Run(Check);` add:
```csharp
StringsTests.Run(Check);
LanguageSettingsTests.Run(Check, NewTempDir);
```

`$HARNESS/StringsTests.cs`:
```csharp
using System.Reflection;
using YouTubeDownloader;

static class StringsTests
{
    // Every text of one instance: plain strings as-is, functions invoked with
    // recognizable arguments (strings "ARG0", "ARG1"..., ints 4240, 4241...).
    static List<(string Name, string Value, string[] Args)> Texts(Strings strings)
    {
        var result = new List<(string, string, string[])>();
        foreach (var property in typeof(Strings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            object? value = property.GetValue(strings);
            if (value is string text)
            {
                result.Add((property.Name, text, Array.Empty<string>()));
            }
            else if (value is Delegate function)
            {
                var parameters = function.GetType().GetMethod("Invoke")!.GetParameters();
                var args = new object[parameters.Length];
                var expected = new List<string>();
                for (int i = 0; i < parameters.Length; i++)
                {
                    Type type = parameters[i].ParameterType;
                    if (type == typeof(string)) { args[i] = "ARG" + i; expected.Add("ARG" + i); }
                    else if (type == typeof(int)) { args[i] = 4240 + i; expected.Add((4240 + i).ToString()); }
                    else if (type == typeof(bool)) { args[i] = true; }
                    else throw new InvalidOperationException(property.Name + ": unsupported parameter type " + type);
                }
                result.Add((property.Name, (string)function.DynamicInvoke(args)!, expected.ToArray()));
            }
            else
            {
                result.Add((property.Name + " (unexpected type)", "", Array.Empty<string>()));
            }
        }
        return result;
    }

    public static void Run(Action<bool, string> check)
    {
        foreach (var (code, strings) in new[] { ("pl", Strings.Polish), ("en", Strings.English) })
        {
            var texts = Texts(strings);
            var empty = texts.Where(t => string.IsNullOrWhiteSpace(t.Value)).Select(t => t.Name).ToList();
            check(texts.Count > 50 && empty.Count == 0, $"strings {code}: {texts.Count} texts, none empty {string.Join(", ", empty)}");
            var lostArgs = texts.Where(t => t.Args.Any(a => !t.Value.Contains(a))).Select(t => t.Name).ToList();
            check(lostArgs.Count == 0, $"strings {code}: every function uses all its arguments {string.Join(", ", lostArgs)}");
        }

        string[] asciiLeftovers =
        {
            "Blad", "Narzedzia", "Jakosc", "Sprawdz ", "zakoncz", "dostepn", "Zaktualizowac", "udalo", "mozna",
            "najnowsza wersje", "uprawnien", "Sprobuj", "Predkosc", "plikow", "Ostrzezenie", "komponentow",
            "moze", "Nieprawidlowy", "uzywany", "sciezki", "otworzyc", "srodowiska"
        };
        var leftovers = Texts(Strings.Polish).Where(t => asciiLeftovers.Any(w => t.Value.Contains(w))).Select(t => t.Name).ToList();
        check(leftovers.Count == 0, "strings pl: no diacritic-less leftovers " + string.Join(", ", leftovers));

        const string polishLetters = "ąćęłńóśźżĄĆĘŁŃÓŚŹŻ";
        var polishInEnglish = Texts(Strings.English).Where(t => t.Value.IndexOfAny(polishLetters.ToCharArray()) >= 0).Select(t => t.Name).ToList();
        check(polishInEnglish.Count == 0, "strings en: no Polish letters " + string.Join(", ", polishInEnglish));

        check(Strings.For("pl") == Strings.Polish && Strings.For("en") == Strings.English && Strings.For("xx") == Strings.English,
            "strings: For() maps codes, unknown -> English");
        check(Strings.Polish.MenuTools != Strings.English.MenuTools, "strings: instances differ");
    }
}
```

`$HARNESS/LanguageSettingsTests.cs`:
```csharp
using System.Globalization;
using YouTubeDownloader;

static class LanguageSettingsTests
{
    public static void Run(Action<bool, string> check, Func<string> newTempDir)
    {
        var pl = new CultureInfo("pl-PL");
        var en = new CultureInfo("en-US");
        var de = new CultureInfo("de-DE");

        check(LanguageSettings.Resolve("pl", en) == "pl", "language: saved pl wins over English system");
        check(LanguageSettings.Resolve("en", pl) == "en", "language: saved en wins over Polish system");
        check(LanguageSettings.Resolve(null, pl) == "pl", "language: no saved choice + Polish system -> pl");
        check(LanguageSettings.Resolve(null, en) == "en", "language: no saved choice + English system -> en");
        check(LanguageSettings.Resolve(null, de) == "en", "language: no saved choice + German system -> en");
        check(LanguageSettings.Resolve(null, CultureInfo.InvariantCulture) == "en", "language: invariant culture -> en");
        check(LanguageSettings.Resolve("garbage", pl) == "pl" && LanguageSettings.Resolve("EN", pl) == "pl",
            "language: unexpected saved value treated as none");

        string dir = newTempDir();
        check(LanguageSettings.Load(dir) == null, "language: missing file -> null");
        LanguageSettings.Save(dir, "en");
        check(LanguageSettings.Load(dir) == "en", "language: save/load round-trip");
        File.WriteAllText(LanguageSettings.GetPath(dir), " pl \r\n");
        check(LanguageSettings.Load(dir) == "pl", "language: whitespace trimmed");

        bool threw = false;
        try { LanguageSettings.Save(Path.Combine(dir, "no-such-dir"), "en"); }
        catch { threw = true; }
        check(!threw, "language: save error is swallowed");
    }
}
```

- [ ] **Step 2: Run the harness to verify it fails**

Run: `dotnet run --project "$HARNESS"`
Expected: `CS2001` for `Strings.cs` / `LanguageSettings.cs`.

- [ ] **Step 3: Implement `LanguageSettings.cs` and `Strings.cs`**

`LanguageSettings.cs`:
```csharp
using System;
using System.Globalization;
using System.IO;

namespace YouTubeDownloader;

internal static class LanguageSettings
{
    public const string Polish = "pl";
    public const string English = "en";

    public static string GetPath(string directory) => Path.Combine(directory, "language.txt");

    public static string? Load(string directory)
    {
        try
        {
            return File.ReadAllText(GetPath(directory)).Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Save(string directory, string code)
    {
        try
        {
            File.WriteAllText(GetPath(directory), code);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // A saved choice wins; with none (or an unexpected value) a Polish system gets
    // Polish and every other system English.
    public static string Resolve(string? saved, CultureInfo systemCulture)
    {
        if (saved is Polish or English)
            return saved;

        return systemCulture.TwoLetterISOLanguageName == Polish ? Polish : English;
    }
}
```

`Strings.cs` (UTF-8; every Polish text with diacritics):
```csharp
using System;

namespace YouTubeDownloader;

// Every user-visible text, per language. A property left out of either instance is
// a compile error (required), so no language can ship with a missing text.
// Parameterized texts are functions, so argument count and types are checked too.
// Menu access keys (_) are part of the text and chosen per language.
internal sealed class Strings
{
    // Shown in their own language in both UIs, so each is findable from the other.
    public const string LanguageNamePolish = "Polski";
    public const string LanguageNameEnglish = "English";

    // ---- Menu ----
    public required string MenuTools { get; init; }
    public required string MenuUpdateComponents { get; init; }
    public required string MenuCheckAppUpdate { get; init; }
    public required string MenuView { get; init; }
    public required string MenuThemeLight { get; init; }
    public required string MenuThemeDark { get; init; }
    public required string MenuThemeSystem { get; init; }
    public required string MenuLanguage { get; init; }
    public required string MenuHelp { get; init; }
    public required string MenuAbout { get; init; }

    // ---- Main window ----
    public required string LabelUrl { get; init; }
    public required string LabelContentType { get; init; }
    public required string LabelQuality { get; init; }
    public required string LabelFormat { get; init; }
    public required string ButtonDownload { get; init; }
    public required string PlaceholderExtraUrl { get; init; }
    public required string ContentTypeVideoAudio { get; init; }
    public required string ContentTypeAudioOnly { get; init; }
    public required string QualityBest { get; init; }
    public required string ProgressDownloading { get; init; }
    public required string ProgressSize { get; init; }
    public required string ProgressSpeed { get; init; }

    // ---- Status line ----
    public required string StatusReady { get; init; }
    public required string StatusCheckingComponents { get; init; }
    public required string StatusDownloadingDeno { get; init; }
    public required string StatusExtractingDeno { get; init; }
    public required string StatusDenoInstalled { get; init; }
    public required Func<string, string> StatusDenoError { get; init; }
    public required string StatusDownloadingYtDlp { get; init; }
    public required string StatusYtDlpDownloaded { get; init; }
    public required string StatusDownloadingFFmpeg { get; init; }
    public required string StatusFetchingFFmpegInfo { get; init; }
    public required Func<string, string> StatusDownloadingFFmpegVersion { get; init; }
    public required string StatusExtractingFFmpeg { get; init; }
    public required Func<string, string> StatusFFmpegDownloaded { get; init; }
    public required string StatusAllComponentsReady { get; init; }
    public required string StatusDenoSkipped { get; init; }
    public required string StatusCheckingDenoVersion { get; init; }
    public required string StatusDenoUpdateAvailable { get; init; }
    public required string StatusDenoUpToDate { get; init; }
    public required string StatusFFmpegUpdateAvailable { get; init; }
    public required string StatusFFmpegUpToDate { get; init; }
    public required string StatusUpdatingYtDlp { get; init; }
    public required string StatusYtDlpUpdated { get; init; }
    public required string StatusComponentsUpdateDone { get; init; }
    public required string StatusUrlEmpty { get; init; }
    public required string StatusYouTubeOnly { get; init; }
    public required string StatusInvalidUrl { get; init; }
    public required string StatusPreparing { get; init; }
    public required string StatusDownloaded { get; init; }
    public required string StatusDownloadError { get; init; }
    public required string StatusFailed { get; init; }
    public required string StatusDownloadFinished { get; init; }
    public required Func<int, int, string> StatusFinishedCount { get; init; }
    public required Func<int, string> StatusDownloadingAppUpdate { get; init; }
    public required string StatusInstallingAppUpdate { get; init; }
    public required Func<string, string> StatusAppUpdateError { get; init; }
    public required string StatusAppUpdatedRestarting { get; init; }

    // ---- Dialog titles and buttons ----
    public required string TitleError { get; init; }
    public required string TitleWarning { get; init; }
    public required string TitleSuccess { get; init; }
    public required string TitleFinishedWithErrors { get; init; }
    public required string TitleComponentsUpdate { get; init; }
    public required string TitleAppUpdate { get; init; }
    public required string TitleAbout { get; init; }
    public required string ButtonOk { get; init; }
    public required string ButtonYes { get; init; }
    public required string ButtonNo { get; init; }
    public required string ButtonClose { get; init; }

    // ---- About ----
    public required Func<string, string> AboutVersion { get; init; }
    public required string AboutLicense { get; init; }
    public required string AboutRepository { get; init; }
    public required string AboutAuthor { get; init; }

    // ---- Dialog messages and errors ----
    public required string ConfirmUpdateComponents { get; init; }
    public required Func<string, string> ErrorWithDetails { get; init; }
    public required Func<string, string> ErrorDenoInfo { get; init; }
    public required string ErrorDenoNotFound { get; init; }
    public required string WarningDenoFailed { get; init; }
    public required Func<string, string> ErrorYtDlpDownload { get; init; }
    public required Func<string, string> ErrorFFmpegInfo { get; init; }
    public required Func<string, string> ErrorFFmpeg { get; init; }
    public required string ErrorFFmpegLinkNotFound { get; init; }
    public required string ErrorFFmpegBinNotFound { get; init; }
    public required string ErrorTarStart { get; init; }
    public required Func<string, string> ErrorTarFailed { get; init; }
    public required Func<string, string> ErrorDenoUpdate { get; init; }
    public required Func<string, string> ErrorUpdate { get; init; }
    public required string ErrorNoUrl { get; init; }
    public required Func<string, string> ErrorInvalidLink { get; init; }
    public required string ErrorYtDlpUnavailable { get; init; }
    public required string ErrorFFmpegUnavailable { get; init; }
    public required string ErrorNoJsRuntime { get; init; }
    public required Func<string, string, string> ErrorDownloadFailed { get; init; }
    public required Func<bool, string, string> MessageDownloaded { get; init; }
    public required Func<int, int, string> MessageDownloadedCount { get; init; }
    public required string AppUpdateSnap { get; init; }
    public required string AppUpdateLocalBuild { get; init; }
    public required Func<string, string> ErrorAppUpdateCheck { get; init; }
    public required Func<int, string> AppUpToDate { get; init; }
    public required string AppUpdatePublishing { get; init; }
    public required Func<int, int, string> AppUpdateAvailable { get; init; }
    public required Func<string, string> AppUpdateNoWriteAccess { get; init; }
    public required string ErrorNoAppPath { get; init; }
    public required string ErrorDownloadCorrupt { get; init; }
    public required Func<string, string> ErrorAppUpdateFailed { get; init; }
    public required Func<string, string> AppUpdatedRestartManually { get; init; }
    public required Func<string, string> ErrorCannotOpenLink { get; init; }

    public static Strings Polish { get; } = new()
    {
        MenuTools = "_Narzędzia",
        MenuUpdateComponents = "_Aktualizuj komponenty",
        MenuCheckAppUpdate = "_Sprawdź aktualizacje aplikacji",
        MenuView = "_Widok",
        MenuThemeLight = "_Jasny",
        MenuThemeDark = "_Ciemny",
        MenuThemeSystem = "_Systemowy",
        MenuLanguage = "Jęz_yk",
        MenuHelp = "_Pomoc",
        MenuAbout = "_Informacje",

        LabelUrl = "Link do filmu:",
        LabelContentType = "Typ:",
        LabelQuality = "Jakość:",
        LabelFormat = "Format:",
        ButtonDownload = "Pobierz",
        PlaceholderExtraUrl = "Wklej kolejny link do filmu...",
        ContentTypeVideoAudio = "Wideo + Audio",
        ContentTypeAudioOnly = "Tylko Audio (MP3)",
        QualityBest = "Najlepsza",
        ProgressDownloading = "Pobieranie",
        ProgressSize = "Rozmiar",
        ProgressSpeed = "Prędkość",

        StatusReady = "Gotowy do pobierania",
        StatusCheckingComponents = "Sprawdzanie komponentów...",
        StatusDownloadingDeno = "Pobieranie Deno runtime...",
        StatusExtractingDeno = "Rozpakowywanie Deno...",
        StatusDenoInstalled = "Deno zainstalowane",
        StatusDenoError = error => "Błąd Deno: " + error,
        StatusDownloadingYtDlp = "Pobieranie yt-dlp...",
        StatusYtDlpDownloaded = "yt-dlp pobrane",
        StatusDownloadingFFmpeg = "Pobieranie FFmpeg...",
        StatusFetchingFFmpegInfo = "Pobieranie informacji o FFmpeg...",
        StatusDownloadingFFmpegVersion = version => $"Pobieranie FFmpeg ({version})...",
        StatusExtractingFFmpeg = "Rozpakowywanie FFmpeg...",
        StatusFFmpegDownloaded = version => "FFmpeg pobrane. Wersja: " + version,
        StatusAllComponentsReady = "Wszystkie komponenty są dostępne. Gotowy do pobierania.",
        StatusDenoSkipped = "Deno: pomijanie (używany runtime systemowy)",
        StatusCheckingDenoVersion = "Sprawdzanie wersji Deno...",
        StatusDenoUpdateAvailable = "Deno: dostępna nowa wersja",
        StatusDenoUpToDate = "Deno: wersja aktualna",
        StatusFFmpegUpdateAvailable = "FFmpeg: dostępna nowa wersja",
        StatusFFmpegUpToDate = "FFmpeg: wersja aktualna",
        StatusUpdatingYtDlp = "Aktualizacja yt-dlp...",
        StatusYtDlpUpdated = "yt-dlp zaktualizowane",
        StatusComponentsUpdateDone = "Aktualizacja zakończona",
        StatusUrlEmpty = "Błąd: URL nie może być pusty",
        StatusYouTubeOnly = "Błąd: tylko linki YouTube",
        StatusInvalidUrl = "Błąd: nieprawidłowy URL",
        StatusPreparing = "Przygotowanie...",
        StatusDownloaded = "Pobrano",
        StatusDownloadError = "Błąd pobierania",
        StatusFailed = "Błąd",
        StatusDownloadFinished = "Pobieranie zakończone!",
        StatusFinishedCount = (downloaded, total) => $"Zakończono: pobrano {downloaded}/{total}",
        StatusDownloadingAppUpdate = build => $"Pobieranie nowej wersji aplikacji (build {build})...",
        StatusInstallingAppUpdate = "Instalowanie nowej wersji aplikacji...",
        StatusAppUpdateError = error => "Błąd aktualizacji aplikacji: " + error,
        StatusAppUpdatedRestarting = "Aplikacja zaktualizowana. Ponowne uruchamianie...",

        TitleError = "Błąd",
        TitleWarning = "Ostrzeżenie",
        TitleSuccess = "Sukces",
        TitleFinishedWithErrors = "Zakończono z błędami",
        TitleComponentsUpdate = "Aktualizacja",
        TitleAppUpdate = "Aktualizacja aplikacji",
        TitleAbout = "Informacje o programie",
        ButtonOk = "OK",
        ButtonYes = "Tak",
        ButtonNo = "Nie",
        ButtonClose = "Zamknij",

        AboutVersion = version => "Wersja " + version,
        AboutLicense = "Licencja: Apache License 2.0",
        AboutRepository = "Repozytorium: ",
        AboutAuthor = "Autor: ",

        ConfirmUpdateComponents = "Zaktualizować komponenty?",
        ErrorWithDetails = error => "Błąd: " + error,
        ErrorDenoInfo = error => "Błąd pobierania informacji o Deno: " + error,
        ErrorDenoNotFound = "Nie znaleziono Deno dla tego systemu",
        WarningDenoFailed = "Nie udało się pobrać Deno. Zainstaluj je z https://deno.com",
        ErrorYtDlpDownload = error => "Błąd pobierania yt-dlp: " + error,
        ErrorFFmpegInfo = error => "Błąd pobierania informacji o FFmpeg: " + error,
        ErrorFFmpeg = error => "Błąd FFmpeg: " + error,
        ErrorFFmpegLinkNotFound = "Nie znaleziono linku do FFmpeg",
        ErrorFFmpegBinNotFound = "Nie znaleziono folderu bin",
        ErrorTarStart = "Nie udało się uruchomić tar",
        ErrorTarFailed = error => "tar zakończył się błędem: " + error,
        ErrorDenoUpdate = error => "Błąd aktualizacji Deno: " + error,
        ErrorUpdate = error => "Błąd aktualizacji: " + error,
        ErrorNoUrl = "Podaj przynajmniej jeden link",
        ErrorInvalidLink = url => "Nieprawidłowy link: " + url,
        ErrorYtDlpUnavailable = "yt-dlp jest niedostępne",
        ErrorFFmpegUnavailable = "FFmpeg jest niedostępne",
        ErrorNoJsRuntime = "Brak środowiska Deno/Node.js",
        ErrorDownloadFailed = (url, error) => $"Błąd ({url}): {error}",
        MessageDownloaded = (many, location) => (many ? "Wszystkie pliki pobrane" : "Plik pobrany") + ". Lokalizacja: " + location,
        MessageDownloadedCount = (downloaded, total) => $"Pobrano {downloaded} z {total} plików.",
        AppUpdateSnap = "Aktualizacje tej wersji dostarcza Snap Store.",
        AppUpdateLocalBuild = "Wersja zbudowana lokalnie - aktualizacje aplikacji są wyłączone.",
        ErrorAppUpdateCheck = error => "Nie udało się sprawdzić aktualizacji: " + error,
        AppUpToDate = build => $"Masz najnowszą wersję aplikacji (build {build}).",
        AppUpdatePublishing = "Nowa wersja jest w trakcie publikacji. Spróbuj za kilka minut.",
        AppUpdateAvailable = (latest, current) =>
            $"Dostępna jest nowa wersja aplikacji (build {latest}, obecna: {current}). Zaktualizować teraz? Aplikacja uruchomi się ponownie.",
        AppUpdateNoWriteAccess = folder =>
            $"Brak uprawnień do zapisu w folderze aplikacji ({folder}). Otworzyć stronę nowej wersji, aby pobrać ją ręcznie?",
        ErrorNoAppPath = "Nie można ustalić ścieżki aplikacji.",
        ErrorDownloadCorrupt = "Pobrany plik jest niekompletny lub uszkodzony",
        ErrorAppUpdateFailed = error => "Nie udało się zaktualizować aplikacji: " + error,
        AppUpdatedRestartManually = error => $"Aplikacja została zaktualizowana. Uruchom ją ponownie ręcznie. ({error})",
        ErrorCannotOpenLink = error => "Nie można otworzyć linku: " + error,
    };

    public static Strings English { get; } = new()
    {
        MenuTools = "_Tools",
        MenuUpdateComponents = "_Update components",
        MenuCheckAppUpdate = "_Check for app updates",
        MenuView = "_View",
        MenuThemeLight = "_Light",
        MenuThemeDark = "_Dark",
        MenuThemeSystem = "_System",
        MenuLanguage = "Lan_guage",
        MenuHelp = "_Help",
        MenuAbout = "_About",

        LabelUrl = "Video link:",
        LabelContentType = "Type:",
        LabelQuality = "Quality:",
        LabelFormat = "Format:",
        ButtonDownload = "Download",
        PlaceholderExtraUrl = "Paste another video link...",
        ContentTypeVideoAudio = "Video + Audio",
        ContentTypeAudioOnly = "Audio only (MP3)",
        QualityBest = "Best",
        ProgressDownloading = "Downloading",
        ProgressSize = "Size",
        ProgressSpeed = "Speed",

        StatusReady = "Ready to download",
        StatusCheckingComponents = "Checking components...",
        StatusDownloadingDeno = "Downloading the Deno runtime...",
        StatusExtractingDeno = "Extracting Deno...",
        StatusDenoInstalled = "Deno installed",
        StatusDenoError = error => "Deno error: " + error,
        StatusDownloadingYtDlp = "Downloading yt-dlp...",
        StatusYtDlpDownloaded = "yt-dlp downloaded",
        StatusDownloadingFFmpeg = "Downloading FFmpeg...",
        StatusFetchingFFmpegInfo = "Fetching FFmpeg release info...",
        StatusDownloadingFFmpegVersion = version => $"Downloading FFmpeg ({version})...",
        StatusExtractingFFmpeg = "Extracting FFmpeg...",
        StatusFFmpegDownloaded = version => "FFmpeg downloaded. Version: " + version,
        StatusAllComponentsReady = "All components are available. Ready to download.",
        StatusDenoSkipped = "Deno: skipped (a system runtime is in use)",
        StatusCheckingDenoVersion = "Checking the Deno version...",
        StatusDenoUpdateAvailable = "Deno: new version available",
        StatusDenoUpToDate = "Deno: up to date",
        StatusFFmpegUpdateAvailable = "FFmpeg: new version available",
        StatusFFmpegUpToDate = "FFmpeg: up to date",
        StatusUpdatingYtDlp = "Updating yt-dlp...",
        StatusYtDlpUpdated = "yt-dlp updated",
        StatusComponentsUpdateDone = "Update finished",
        StatusUrlEmpty = "Error: the URL cannot be empty",
        StatusYouTubeOnly = "Error: YouTube links only",
        StatusInvalidUrl = "Error: invalid URL",
        StatusPreparing = "Preparing...",
        StatusDownloaded = "Downloaded",
        StatusDownloadError = "Download error",
        StatusFailed = "Error",
        StatusDownloadFinished = "Download finished!",
        StatusFinishedCount = (downloaded, total) => $"Finished: {downloaded}/{total} downloaded",
        StatusDownloadingAppUpdate = build => $"Downloading the new app version (build {build})...",
        StatusInstallingAppUpdate = "Installing the new app version...",
        StatusAppUpdateError = error => "App update error: " + error,
        StatusAppUpdatedRestarting = "App updated. Restarting...",

        TitleError = "Error",
        TitleWarning = "Warning",
        TitleSuccess = "Success",
        TitleFinishedWithErrors = "Finished with errors",
        TitleComponentsUpdate = "Update",
        TitleAppUpdate = "App update",
        TitleAbout = "About",
        ButtonOk = "OK",
        ButtonYes = "Yes",
        ButtonNo = "No",
        ButtonClose = "Close",

        AboutVersion = version => "Version " + version,
        AboutLicense = "License: Apache License 2.0",
        AboutRepository = "Repository: ",
        AboutAuthor = "Author: ",

        ConfirmUpdateComponents = "Update components?",
        ErrorWithDetails = error => "Error: " + error,
        ErrorDenoInfo = error => "Could not fetch Deno release info: " + error,
        ErrorDenoNotFound = "No Deno build found for this system",
        WarningDenoFailed = "Could not download Deno. Install it from https://deno.com",
        ErrorYtDlpDownload = error => "yt-dlp download error: " + error,
        ErrorFFmpegInfo = error => "Could not fetch FFmpeg release info: " + error,
        ErrorFFmpeg = error => "FFmpeg error: " + error,
        ErrorFFmpegLinkNotFound = "FFmpeg download link not found",
        ErrorFFmpegBinNotFound = "bin folder not found",
        ErrorTarStart = "Could not start tar",
        ErrorTarFailed = error => "tar failed: " + error,
        ErrorDenoUpdate = error => "Deno update error: " + error,
        ErrorUpdate = error => "Update error: " + error,
        ErrorNoUrl = "Enter at least one link",
        ErrorInvalidLink = url => "Invalid link: " + url,
        ErrorYtDlpUnavailable = "yt-dlp is not available",
        ErrorFFmpegUnavailable = "FFmpeg is not available",
        ErrorNoJsRuntime = "No Deno/Node.js runtime found",
        ErrorDownloadFailed = (url, error) => $"Error ({url}): {error}",
        MessageDownloaded = (many, location) => (many ? "All files downloaded" : "File downloaded") + ". Location: " + location,
        MessageDownloadedCount = (downloaded, total) => $"Downloaded {downloaded} of {total} files.",
        AppUpdateSnap = "This version is updated through the Snap Store.",
        AppUpdateLocalBuild = "Locally built version - app updates are disabled.",
        ErrorAppUpdateCheck = error => "Could not check for updates: " + error,
        AppUpToDate = build => $"You have the latest version of the app (build {build}).",
        AppUpdatePublishing = "A new version is being published. Try again in a few minutes.",
        AppUpdateAvailable = (latest, current) =>
            $"A new version of the app is available (build {latest}, current: {current}). Update now? The app will restart.",
        AppUpdateNoWriteAccess = folder =>
            $"No write access to the app folder ({folder}). Open the new version's page to download it manually?",
        ErrorNoAppPath = "Cannot determine the app's location.",
        ErrorDownloadCorrupt = "The downloaded file is incomplete or corrupted",
        ErrorAppUpdateFailed = error => "Could not update the app: " + error,
        AppUpdatedRestartManually = error => $"The app has been updated. Please restart it manually. ({error})",
        ErrorCannotOpenLink = error => "Cannot open the link: " + error,
    };

    public static Strings Current { get; set; } = Polish;

    public static Strings For(string languageCode) =>
        languageCode == LanguageSettings.Polish ? Polish : English;
}
```

- [ ] **Step 4: Run the harness to verify it passes**

Run: `dotnet run --project "$HARNESS"`
Expected: all `PASS`, `ALL PASSED`.

- [ ] **Step 5: Build**

Run: `dotnet build YouTubeDownloader.csproj -c Debug -warnaserror`
Expected: 0 warnings, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add Strings.cs LanguageSettings.cs
git commit -m "Add Polish and English UI texts and language resolution

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Task 3: Wire the texts into the windows

**Files:**
- Modify: `MainWindow.axaml`, `MainWindow.axaml.cs`, `AboutWindow.axaml`, `AboutWindow.axaml.cs`, `MessageDialog.axaml`, `MessageDialog.axaml.cs`, `App.axaml.cs`

**Interfaces:**
- Consumes: `Strings.*`, `LanguageSettings.*` (Task 2), `YtDlpArguments.QualityHeights`/`QualityLabel` (Task 1).
- Produces: XAML names `MiTools`, `MiView`, `MiHelp`, `MiLanguage`, `MiLangPolish`, `MiLangEnglish`, `LblUrl`, `LblContentType`, `LblQuality`, `LblFormat` (MainWindow); `LblLicense`, `LblRepository`, `LblAuthor` (AboutWindow). `MainWindow.ApplyTexts()`, `MainWindow.SetLanguage(string)`, `private static Strings Ui => Strings.Current`.

- [ ] **Step 1: RED - the app still ignores the language setting**

With the current build, write `en` to the language file and check the first status line (Windows, PowerShell):
```powershell
dotnet build YouTubeDownloader.csproj -c Debug 2>&1 | Select-String "Liczba błędów|error"
$bin = "E:\claude\YouTube-Downloader\bin\Debug\net10.0"
$data = Join-Path $env:LOCALAPPDATA "YouTubeDownloader"
$langFile = Join-Path $data "language.txt"
$backup = if (Test-Path $langFile) { Get-Content $langFile -Raw } else { $null }
Set-Content $langFile "en" -NoNewline
$out = Join-Path $env:TEMP "ytd-lang-test.txt"
$p = Start-Process "$bin\YouTubeDownloader.exe" -WorkingDirectory $bin -RedirectStandardOutput $out -PassThru
Start-Sleep -Seconds 8
Stop-Process -Id $p.Id -Force
Get-Content $out | Select-Object -First 2
if ($backup -eq $null) { Remove-Item $langFile } else { Set-Content $langFile $backup -NoNewline }
```
Expected: `[status] Sprawdzanie komponentow...` (Polish, ignoring `en`).

- [ ] **Step 2: `App.axaml.cs` - pick the language at startup**

Replace:
```csharp
            RequestedThemeVariant = ThemeSettings.ToVariant(ThemeSettings.Load(AppPaths.DataDirectory));
```
with:
```csharp
            string dataDirectory = AppPaths.DataDirectory;
            RequestedThemeVariant = ThemeSettings.ToVariant(ThemeSettings.Load(dataDirectory));
            Strings.Current = Strings.For(
                LanguageSettings.Resolve(LanguageSettings.Load(dataDirectory), CultureInfo.CurrentUICulture));
```
and add `using System.Globalization;` at the top.

- [ ] **Step 3: `MainWindow.axaml` - names instead of texts**

Replace the whole `<Menu DockPanel.Dock="Top">...</Menu>` block with:
```xml
    <Menu DockPanel.Dock="Top">
      <MenuItem Name="MiTools">
        <MenuItem Name="MiAktualizujKomponenty"/>
        <MenuItem Name="MiSprawdzAktualizacje"/>
      </MenuItem>
      <MenuItem Name="MiView">
        <MenuItem Name="MiThemeLight" ToggleType="Radio" GroupName="ThemeGroup"/>
        <MenuItem Name="MiThemeDark" ToggleType="Radio" GroupName="ThemeGroup"/>
        <MenuItem Name="MiThemeSystem" ToggleType="Radio" GroupName="ThemeGroup"/>
        <Separator/>
        <MenuItem Name="MiLanguage">
          <MenuItem Name="MiLangPolish" Header="Polski" ToggleType="Radio" GroupName="LanguageGroup"/>
          <MenuItem Name="MiLangEnglish" Header="English" ToggleType="Radio" GroupName="LanguageGroup"/>
        </MenuItem>
      </MenuItem>
      <MenuItem Name="MiHelp">
        <MenuItem Name="MiInformacje"/>
      </MenuItem>
    </Menu>
```
Then:
- `<TextBlock Text="Link do filmu:"/>` -> `<TextBlock Name="LblUrl"/>`
- `<TextBlock Text="Typ:"/>` -> `<TextBlock Name="LblContentType"/>`
- `<TextBlock Text="Jakosc:"/>` -> `<TextBlock Name="LblQuality"/>`
- `<TextBlock Text="Format:"/>` -> `<TextBlock Name="LblFormat"/>`
- In `BtnDownload`, delete `Content="Pobierz"`.
- In `LblStatus`, delete `Text="Gotowy do pobierania"`.

- [ ] **Step 4: `MainWindow.axaml.cs` - `Ui`, `ApplyTexts()`, `SetLanguage()`, constructor**

Add at the top of the class body (after the fields):
```csharp
    private static Strings Ui => Strings.Current;
```

In the constructor, replace the two `CbContentType.Items.Add(...)` lines, the `CbContentType.SelectedIndex = 0;` line, the quality `foreach` + `CbQuality.SelectedIndex = 0;` (from Task 1) with nothing, and right after the `CbFormat` block (after `CbFormat.SelectedIndex = 0;`) add:
```csharp
        ApplyTexts();
        LblStatus.Text = Ui.StatusReady;
```
Keep `CbContentType.SelectionChanged += ContentType_Changed;` where it is.

After the theme wiring (`MiThemeSystem.Click += ...`), add:
```csharp
        MiLangPolish.Click += (s, e) => SetLanguage(LanguageSettings.Polish);
        MiLangEnglish.Click += (s, e) => SetLanguage(LanguageSettings.English);
```

Add these methods after `SetTheme(...)`:
```csharp
    private void SetLanguage(string code)
    {
        Strings.Current = Strings.For(code);
        LanguageSettings.Save(dataDirectory, code);
        ApplyTexts();
    }

    // Every text of this window from Strings.Current. Runs at startup and on each
    // language switch; list selections and extra URL rows survive. The status line
    // is left alone - it keeps its language until the next status update.
    private void ApplyTexts()
    {
        MiTools.Header = Ui.MenuTools;
        MiAktualizujKomponenty.Header = Ui.MenuUpdateComponents;
        MiSprawdzAktualizacje.Header = Ui.MenuCheckAppUpdate;
        MiView.Header = Ui.MenuView;
        MiThemeLight.Header = Ui.MenuThemeLight;
        MiThemeDark.Header = Ui.MenuThemeDark;
        MiThemeSystem.Header = Ui.MenuThemeSystem;
        MiLanguage.Header = Ui.MenuLanguage;
        MiHelp.Header = Ui.MenuHelp;
        MiInformacje.Header = Ui.MenuAbout;
        MiLangPolish.IsChecked = Ui == Strings.Polish;
        MiLangEnglish.IsChecked = Ui == Strings.English;

        LblUrl.Text = Ui.LabelUrl;
        LblContentType.Text = Ui.LabelContentType;
        LblQuality.Text = Ui.LabelQuality;
        LblFormat.Text = Ui.LabelFormat;
        BtnDownload.Content = Ui.ButtonDownload;
        foreach (var box in extraUrlBoxes)
            box.PlaceholderText = Ui.PlaceholderExtraUrl;

        int contentTypeIndex = Math.Max(CbContentType.SelectedIndex, 0);
        CbContentType.Items.Clear();
        CbContentType.Items.Add(Ui.ContentTypeVideoAudio);
        CbContentType.Items.Add(Ui.ContentTypeAudioOnly);
        CbContentType.SelectedIndex = contentTypeIndex;

        int qualityIndex = Math.Max(CbQuality.SelectedIndex, 0);
        CbQuality.Items.Clear();
        foreach (int? height in YtDlpArguments.QualityHeights)
            CbQuality.Items.Add(YtDlpArguments.QualityLabel(height, Ui.QualityBest));
        CbQuality.SelectedIndex = qualityIndex;
    }
```

In `ParseDownloadProgress`, replace:
```csharp
            string status = "Pobieranie: " + percent.ToString("F1") + "%";
            if (!string.IsNullOrEmpty(downloadedSize))
                status += " Rozmiar: " + downloadedSize;
            if (!string.IsNullOrEmpty(speed))
                status += " Predkosc: " + speed;
```
with:
```csharp
            string status = Ui.ProgressDownloading + ": " + percent.ToString("F1") + "%";
            if (!string.IsNullOrEmpty(downloadedSize))
                status += " " + Ui.ProgressSize + ": " + downloadedSize;
            if (!string.IsNullOrEmpty(speed))
                status += " " + Ui.ProgressSpeed + ": " + speed;
```

- [ ] **Step 5: `MainWindow.axaml.cs` - replace every remaining literal**

Replace each old literal (search by its text; line numbers have shifted) with the new expression. `TitleError` replaces every `"Blad"` dialog title.

| Old | New |
|---|---|
| `PlaceholderText = "Wklej kolejny link do filmu..."` | `PlaceholderText = Ui.PlaceholderExtraUrl` |
| `UpdateStatus("Sprawdzanie komponentow...")` | `UpdateStatus(Ui.StatusCheckingComponents)` |
| `UpdateStatus("Pobieranie yt-dlp...")` | `UpdateStatus(Ui.StatusDownloadingYtDlp)` |
| `UpdateStatus("Pobieranie FFmpeg...")` | `UpdateStatus(Ui.StatusDownloadingFFmpeg)` |
| `UpdateStatus("Wszystkie komponenty sa dostepne. Gotowy do pobierania.")` | `UpdateStatus(Ui.StatusAllComponentsReady)` |
| `"Blad pobierania informacji Deno: " + ex.Message, "Blad"` | `Ui.ErrorDenoInfo(ex.Message), Ui.TitleError` |
| `UpdateStatus("Pobieranie Deno runtime...")` | `UpdateStatus(Ui.StatusDownloadingDeno)` |
| `"Nie znaleziono Deno dla tego systemu", "Blad"` | `Ui.ErrorDenoNotFound, Ui.TitleError` |
| `UpdateStatus("Rozpakowywanie Deno...")` | `UpdateStatus(Ui.StatusExtractingDeno)` |
| `UpdateStatus("Deno zainstalowane")` | `UpdateStatus(Ui.StatusDenoInstalled)` |
| `UpdateStatus("Blad Deno: " + ex.Message)` | `UpdateStatus(Ui.StatusDenoError(ex.Message))` |
| `"Nie udalo sie pobrac Deno. Zainstaluj z https://deno.com", "Ostrzezenie"` | `Ui.WarningDenoFailed, Ui.TitleWarning` |
| `UpdateStatus("yt-dlp pobrane")` | `UpdateStatus(Ui.StatusYtDlpDownloaded)` |
| `"Blad pobierania yt-dlp: " + ex.Message, "Blad"` | `Ui.ErrorYtDlpDownload(ex.Message), Ui.TitleError` |
| `"Blad pobierania FFmpeg info: " + ex.Message, "Blad"` | `Ui.ErrorFFmpegInfo(ex.Message), Ui.TitleError` |
| `"Blad pobierania informacji FFmpeg: " + ex.Message, "Blad"` | `Ui.ErrorFFmpegInfo(ex.Message), Ui.TitleError` |
| `throw new Exception("Nie udalo sie uruchomic tar")` | `throw new Exception(Ui.ErrorTarStart)` |
| `throw new Exception("tar zakonczyl sie bledem: " + error)` | `throw new Exception(Ui.ErrorTarFailed(error))` |
| `UpdateStatus("Pobieranie informacji FFmpeg...")` (2x) | `UpdateStatus(Ui.StatusFetchingFFmpegInfo)` |
| `throw new Exception("Nie znaleziono linku do FFmpeg")` (2x) | `throw new Exception(Ui.ErrorFFmpegLinkNotFound)` |
| `UpdateStatus("Pobieranie FFmpeg (" + version + ")...")` (2x) | `UpdateStatus(Ui.StatusDownloadingFFmpegVersion(version))` |
| `UpdateStatus("Rozpakowywanie FFmpeg...")` | `UpdateStatus(Ui.StatusExtractingFFmpeg)` |
| `throw new Exception("Nie znaleziono folderu bin")` | `throw new Exception(Ui.ErrorFFmpegBinNotFound)` |
| `UpdateStatus("FFmpeg pobrane. Wersja: " + version)` (2x) | `UpdateStatus(Ui.StatusFFmpegDownloaded(version))` |
| `"Blad FFmpeg: " + ex.Message, "Blad"` (2x) | `Ui.ErrorFFmpeg(ex.Message), Ui.TitleError` |
| `UpdateStatus("Blad: " + ex.Message)` (2x) | `UpdateStatus(Ui.ErrorWithDetails(ex.Message))` |
| `UpdateStatus("Deno: pomijanie (uzywany runtime systemowy)")` | `UpdateStatus(Ui.StatusDenoSkipped)` |
| `UpdateStatus("Sprawdzanie wersji Deno...")` | `UpdateStatus(Ui.StatusCheckingDenoVersion)` |
| `UpdateStatus("Deno: Nowa wersja dostepna")` | `UpdateStatus(Ui.StatusDenoUpdateAvailable)` |
| `UpdateStatus("Deno: Wersja aktualna")` | `UpdateStatus(Ui.StatusDenoUpToDate)` |
| `"Blad aktualizacji Deno: " + ex.Message, "Blad"` | `Ui.ErrorDenoUpdate(ex.Message), Ui.TitleError` |
| `UpdateStatus("FFmpeg: Nowa wersja dostepna")` | `UpdateStatus(Ui.StatusFFmpegUpdateAvailable)` |
| `UpdateStatus("FFmpeg: Wersja aktualna")` | `UpdateStatus(Ui.StatusFFmpegUpToDate)` |
| `"Blad aktualizacji: " + ex.Message, "Blad"` | `Ui.ErrorUpdate(ex.Message), Ui.TitleError` |
| `"Zaktualizowac komponenty?", "Aktualizacja"` | `Ui.ConfirmUpdateComponents, Ui.TitleComponentsUpdate` |
| `UpdateStatus("Aktualizacja yt-dlp...")` | `UpdateStatus(Ui.StatusUpdatingYtDlp)` |
| `UpdateStatus("yt-dlp zaktualizowane")` | `UpdateStatus(Ui.StatusYtDlpUpdated)` |
| `"Blad: " + ex.Message, "Blad"` | `Ui.ErrorWithDetails(ex.Message), Ui.TitleError` |
| `UpdateStatus("Aktualizacja zakonczena")` | `UpdateStatus(Ui.StatusComponentsUpdateDone)` |
| `UpdateStatus("Blad: URL nie moze byc pusty")` | `UpdateStatus(Ui.StatusUrlEmpty)` |
| `UpdateStatus("Blad: Tylko linki YouTube")` | `UpdateStatus(Ui.StatusYouTubeOnly)` |
| `UpdateStatus("Blad: Nieprawidlowy URL")` | `UpdateStatus(Ui.StatusInvalidUrl)` |
| `"Podaj przynajmniej jeden link", "Blad"` | `Ui.ErrorNoUrl, Ui.TitleError` |
| `"Nieprawidlowy link: " + url, "Blad"` | `Ui.ErrorInvalidLink(url), Ui.TitleError` |
| `"yt-dlp niedostepne", "Blad"` | `Ui.ErrorYtDlpUnavailable, Ui.TitleError` |
| `"FFmpeg niedostepne", "Blad"` | `Ui.ErrorFFmpegUnavailable, Ui.TitleError` |
| `"Brak runtime Deno/Node.js", "Blad"` | `Ui.ErrorNoJsRuntime, Ui.TitleError` |
| `UpdateStatus(statusPrefix + "Przygotowanie...")` | `UpdateStatus(statusPrefix + Ui.StatusPreparing)` |
| `string message = rawUrls.Count > 1 ? "Wszystkie pliki pobrane" : "Plik pobrany";` | *(delete the line)* |
| `UpdateStatus("Pobieranie zakonczono!")` | `UpdateStatus(Ui.StatusDownloadFinished)` |
| `message + ". Lokalizacja: " + downloadsDir, "Sukces"` | `Ui.MessageDownloaded(rawUrls.Count > 1, downloadsDir), Ui.TitleSuccess` |
| `` UpdateStatus($"Zakonczono: {successCount}/{rawUrls.Count} pobranych") `` | `UpdateStatus(Ui.StatusFinishedCount(successCount, rawUrls.Count))` |
| `` $"Pobrano {successCount} z {rawUrls.Count} plikow.", "Zakonczono z bledami" `` | `Ui.MessageDownloadedCount(successCount, rawUrls.Count), Ui.TitleFinishedWithErrors` |
| `UpdateStatus(statusPrefix + "Pobrano")` | `UpdateStatus(statusPrefix + Ui.StatusDownloaded)` |
| `UpdateStatus(statusPrefix + "Blad pobierania")` | `UpdateStatus(statusPrefix + Ui.StatusDownloadError)` |
| `"Blad (" + rawUrl + "): " + error, "Blad"` | `Ui.ErrorDownloadFailed(rawUrl, error), Ui.TitleError` |
| `UpdateStatus(statusPrefix + "Blad")` | `UpdateStatus(statusPrefix + Ui.StatusFailed)` |
| `"Blad (" + rawUrl + "): " + ex.Message, "Blad"` | `Ui.ErrorDownloadFailed(rawUrl, ex.Message), Ui.TitleError` |
| `const string title = "Aktualizacja aplikacji";` | `string title = Ui.TitleAppUpdate;` |
| `"Aktualizacje tej wersji dostarcza Snap Store."` | `Ui.AppUpdateSnap` |
| `"Wersja zbudowana lokalnie - aktualizacje aplikacji sa wylaczone."` | `Ui.AppUpdateLocalBuild` |
| `"Nie udalo sie sprawdzic aktualizacji: " + ex.Message, "Blad"` | `Ui.ErrorAppUpdateCheck(ex.Message), Ui.TitleError` |
| `` $"Masz najnowsza wersje aplikacji (build {localBuild})." `` | `Ui.AppUpToDate(localBuild.Value)` |
| `"Nowa wersja jest w trakcie publikacji. Sprobuj za kilka minut."` | `Ui.AppUpdatePublishing` |
| the two-line `$"Dostepna jest nowa wersja ..." + "Zaktualizowac teraz? ..."` argument | `Ui.AppUpdateAvailable(release.BuildNumber.Value, localBuild.Value)` |
| the two-line `"Brak uprawnien do zapisu ..." + "Otworzyc strone ..."` argument | `Ui.AppUpdateNoWriteAccess(appDirectory)` |
| `"Nie mozna ustalic sciezki aplikacji.", "Blad"` | `Ui.ErrorNoAppPath, Ui.TitleError` |
| `` UpdateStatus($"Pobieranie nowej wersji aplikacji (build {release.BuildNumber})...") `` | `UpdateStatus(Ui.StatusDownloadingAppUpdate(release.BuildNumber.GetValueOrDefault()))` |
| `new InvalidDataException("Pobrany plik jest niekompletny lub uszkodzony")` | `new InvalidDataException(Ui.ErrorDownloadCorrupt)` |
| `UpdateStatus("Instalowanie nowej wersji aplikacji...")` | `UpdateStatus(Ui.StatusInstallingAppUpdate)` |
| `UpdateStatus("Blad aktualizacji aplikacji: " + ex.Message)` | `UpdateStatus(Ui.StatusAppUpdateError(ex.Message))` |
| `"Nie udalo sie zaktualizowac aplikacji: " + ex.Message, "Blad"` | `Ui.ErrorAppUpdateFailed(ex.Message), Ui.TitleError` |
| `UpdateStatus("Aplikacja zaktualizowana. Ponowne uruchamianie...")` | `UpdateStatus(Ui.StatusAppUpdatedRestarting)` |
| `"Aplikacja zostala zaktualizowana. Uruchom ja ponownie recznie. (" + ex.Message + ")", "Aktualizacja aplikacji"` | `Ui.AppUpdatedRestartManually(ex.Message), Ui.TitleAppUpdate` |
| `"Nie mozna otworzyc linku: " + ex.Message, "Blad"` | `Ui.ErrorCannotOpenLink(ex.Message), Ui.TitleError` |

Then verify nothing Polish is left (Grep tool or bash):
```bash
grep -nE '"[^"]*(Blad|Błąd|Pobier|Nie |Aktualiz|Wersja|Sprawdz|zakoncz|dostep|Gotowy|Przygotow|Rozpakow|Instalow|Lokalizacja|Sukces|Ostrzez|Brak |Podaj|Masz |Nowa |Dostepna|Tylko|Najlepsza|Wideo|Wklej|Jakosc|Link do|Rozmiar|Predkosc|zainstalowane|pobrane|pomijanie|Zakonczono)' MainWindow.axaml.cs
```
Expected: no output.

- [ ] **Step 6: `AboutWindow` and `MessageDialog`**

`AboutWindow.axaml`: delete `Title="Informacje o programie"`; `Text="Wersja 2.0.0"` -> no `Text`; `<TextBlock Text="Licencja: Apache License 2.0" .../>` -> `<TextBlock Name="LblLicense" HorizontalAlignment="Center"/>`; `<TextBlock Text="Repozytorium: "/>` -> `<TextBlock Name="LblRepository"/>`; `<TextBlock Text="Autor: "/>` -> `<TextBlock Name="LblAuthor"/>`; in `CloseButton` delete `Content="Zamknij"`.

`AboutWindow.axaml.cs` constructor - replace `VersionText.Text = "Wersja " + GetAppVersion();` with:
```csharp
        Strings ui = Strings.Current;
        Title = ui.TitleAbout;
        VersionText.Text = ui.AboutVersion(GetAppVersion());
        LblLicense.Text = ui.AboutLicense;
        LblRepository.Text = ui.AboutRepository;
        LblAuthor.Text = ui.AboutAuthor;
        CloseButton.Content = ui.ButtonClose;
```
and in `OpenUrl` replace `"Nie mozna otworzyc linku: " + ex.Message, "Blad"` with `Strings.Current.ErrorCannotOpenLink(ex.Message), Strings.Current.TitleError`.

`MessageDialog.axaml`: in `OkButton` delete `Content="OK"`; in `CancelButton` delete `Content="Nie"`.

`MessageDialog.axaml.cs` - in the private constructor replace:
```csharp
        if (isConfirmation)
        {
            OkButton.Content = "Tak";
            CancelButton.IsVisible = true;
        }
```
with:
```csharp
        OkButton.Content = isConfirmation ? Strings.Current.ButtonYes : Strings.Current.ButtonOk;
        CancelButton.Content = Strings.Current.ButtonNo;
        CancelButton.IsVisible = isConfirmation;
```

- [ ] **Step 7: Build (Debug and Release) and harness**

Run: `dotnet build YouTubeDownloader.csproj -c Debug -warnaserror`, `dotnet build YouTubeDownloader.csproj -c Release -warnaserror`, `dotnet run --project "$HARNESS"`
Expected: 0 warnings/0 errors twice; `ALL PASSED`.

- [ ] **Step 8: GREEN - the language setting drives the UI**

Run the Step 1 script again with `en`, then a second time with `Set-Content $langFile "pl" -NoNewline` in place of `"en"`.
Expected: with `en` the first line is `[status] Checking components...`; with `pl` it starts with `[status] Sprawdzanie komponent` (the redirected file may show `ó` mangled - compare the ASCII prefix only). The language file is restored afterwards.

- [ ] **Step 9: Commit**

```bash
git add MainWindow.axaml MainWindow.axaml.cs AboutWindow.axaml AboutWindow.axaml.cs MessageDialog.axaml MessageDialog.axaml.cs App.axaml.cs
git commit -m "Translate the UI: Polish and English, switchable in View > Language

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Task 4: Documentation and version

**Files:**
- Modify: `YouTubeDownloader.csproj` (maybe), `README.md`, `CLAUDE.md`

- [ ] **Step 1: Version**

Run `date +%d%m%y`. If it prints `280926`, keep `<Version>2.0.280926</Version>` and the badge, and add the bullets below to the existing `2.0.280926` sections. Otherwise set `<Version>2.0.<output></Version>`, update the badge `Version-2.0.<output>-brightgreen`, and add new sections `### 🆕 Co nowego w wersji 2.0.<output>` / `### 🆕 What's New in 2.0.<output>` above the `2.0.280926` ones containing the bullets below.

PL bullets:
```markdown
- 🇬🇧 **Wersja angielska** - interfejs po polsku i po angielsku. Przy pierwszym uruchomieniu język dobierany jest do języka systemu (polski system → polski, inny → angielski), zmiana w menu Widok → Język działa od razu i jest zapamiętywana
- 🔤 **Polskie znaki** - polskie teksty interfejsu pisane są teraz z polskimi znakami („Błąd”, „Narzędzia”, „Jakość”)
```
EN bullets:
```markdown
- 🇬🇧 **English interface** - the UI is available in Polish and English. On first launch the language follows the system language (Polish system → Polish, anything else → English); switch any time in View → Language, it applies immediately and is remembered
- 🔤 **Polish diacritics** - the Polish UI texts now use proper Polish characters
```

- [ ] **Step 2: README descriptions and features**

- PL description: `oferuje przyjazny interfejs w języku polskim do wyboru` -> `oferuje przyjazny interfejs w języku polskim i angielskim do wyboru`
- EN description: `provides a user-friendly Polish-language interface for selecting` -> `provides a user-friendly Polish and English interface for selecting`
- PL feature `- 🇵🇱 **Polski interfejs** - Pełne wsparcie języka polskiego` -> `- 🇵🇱🇬🇧 **Interfejs PL/EN** - Polski i angielski, wybór w menu Widok → Język`
- EN feature `- 🇵🇱 **Polish interface** - Full Polish language support` -> `- 🇵🇱🇬🇧 **PL/EN interface** - Polish and English, switchable in View → Language`
- In both "Project Structure" trees, after the `GitHubApi.cs` line add (PL / EN):
```
├── Strings.cs                   # Teksty interfejsu po polsku i angielsku
├── LanguageSettings.cs          # Wybór i zapis języka (language.txt)
├── YtDlpArguments.cs            # Argumenty yt-dlp dla jakości/formatu
```
```
├── Strings.cs                   # UI texts in Polish and English
├── LanguageSettings.cs          # Language choice and persistence (language.txt)
├── YtDlpArguments.cs            # yt-dlp arguments for quality/format
```

- [ ] **Step 3: CLAUDE.md**

1. Line 5: `provides a user-friendly Polish-language interface` -> `provides a user-friendly Polish and English interface`; Key Information `**Primary Language**: C# with Polish UI text` -> `**Primary Language**: C# with Polish and English UI text (see `Strings.cs`)`.
2. Repository Structure tree, after the `GitHubApi.cs` line:
```
├── Strings.cs                   # Every UI text, Polish + English instances (required members)
├── LanguageSettings.cs          # Resolves/loads/saves the UI language (language.txt, data folder)
├── YtDlpArguments.cs            # yt-dlp format args from quality values (not display text)
```
3. Runtime Structure data-folder tree: after `└── theme.txt ...` change it to `├── theme.txt ...` and add `└── language.txt                # UI language: pl or en`.
4. UI Components list: add `- `MiLanguage` -> `MiLangPolish` / `MiLangEnglish`: View -> Language radio items ("Polski", "English" - always in their own language)` and `- `LblUrl`, `LblContentType`, `LblQuality`, `LblFormat`: labels whose text is set from `Strings` in `ApplyTexts()``.
5. Replace the **Polish UI Text** bullet (and its four sub-bullets) under Code Style with:
```markdown
- **UI text lives only in `Strings.cs`**, in both `Strings.Polish` and `Strings.English`. Every property is `required`, so a text missing from either language is a compile error; parameterized texts are `Func<>` properties. Code reads them via `Strings.Current` (`Ui` in `MainWindow`). Polish texts use proper diacritics; menu access keys (`_`) are chosen per language. XAML holds no user-facing text - windows set it in code (`MainWindow.ApplyTexts()`, the `AboutWindow`/`MessageDialog` constructors)
```
6. Replace "2. **Update UI Text/Language**" sub-bullets with:
```markdown
   - Add/change the property in `Strings.cs` and set it in **both** instances
   - Main-window static texts are applied in `MainWindow.ApplyTexts()` (runs at startup and on every language switch); dialogs/About read `Strings.Current` when constructed
   - A new language = a new `Strings` instance, a code in `LanguageSettings`, a case in `Strings.For()` and a menu item
```
7. Replace "3. **Change Quality Options**" sub-bullets with:
```markdown
   - Edit `YtDlpArguments.QualityHeights` (value per list position) and, if needed, `QualityLabel()`; only the "best" label is translated (`Strings.QualityBest`)
   - `YtDlpArguments.Build()` turns the value into yt-dlp arguments - never branch on display text
```
8. AI Assistant Guidelines "3. **Use Polish text**..." -> `3. **Add every new user-facing text to `Strings.cs` in both languages** (Polish with diacritics)`.
9. Testing Checklist: add `- [ ] Language switch (View -> Language) updates menu, labels and lists immediately; the choice survives a restart` and `- [ ] A download works with the English UI ("Best" and a fixed quality)`.

- [ ] **Step 4: Build and commit**

Run: `dotnet build YouTubeDownloader.csproj -c Release -warnaserror` -> 0/0.
```bash
git add YouTubeDownloader.csproj README.md CLAUDE.md
git commit -m "Document the PL/EN UI

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## After all tasks

- Final whole-branch review (fresh reviewer, Review Focus above), fix pass.
- HUMAN CHECKPOINT (Windows, user): switch to English and back (menu, labels, lists, extra URL rows, selection kept); error dialog and About in the new language; restart keeps the language; a real download in English with "Best" and 720p; Polish diacritics render and nothing is clipped.
- Then superpowers:finishing-a-development-branch.
