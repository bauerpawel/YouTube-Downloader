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
