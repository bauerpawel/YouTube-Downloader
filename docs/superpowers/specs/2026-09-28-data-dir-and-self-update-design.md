# Katalog danych użytkownika + samoaktualizacja aplikacji z GitHub

**Data:** 2026-09-28
**Status:** Zaakceptowany, gotowy do planu implementacji

## Cel

1. Aplikacja przestaje zapisywać cokolwiek obok własnego pliku
   wykonywalnego (poza `downloads/`). Narzędzia (yt-dlp, FFmpeg, Deno), pliki
   wersji i `theme.txt` trafiają do katalogu danych użytkownika.
2. Aplikacja potrafi sama sprawdzić, pobrać i zainstalować nowszą wersję
   siebie z GitHub Releases tego repozytorium.

## Kontekst

- Dziś wszystko ląduje w `AppDomain.CurrentDomain.BaseDirectory`
  (`MainWindow.axaml.cs:35`, `App.axaml.cs:18`). To blokuje: dystrybucję jako
  snap (katalog snapa jest tylko do odczytu), pakiet `.app` na macOS
  (zapis do podpisanego bundla), uruchamianie z `Program Files` na Windows.
- Oddzielenie danych od plików aplikacji jest też warunkiem czystej
  samoaktualizacji: w folderze aplikacji zostają tylko pliki samej
  aplikacji, więc podmiana dotyczy tylko ich.
- Ta specyfikacja jest pierwszym krokiem. **Wersja angielska interfejsu**
  to osobny, kolejny projekt (osobna specyfikacja), wdrażany po tym - dzięki
  działającej samoaktualizacji dotrze do użytkowników automatycznie.
  Snap (Snap Store) to również osobny, późniejszy etap.

## Ustalenia techniczne

**Numer wersji nie jest monotoniczny.** `<Version>` ma format
`2.0.ddMMyy`, więc np. `2.0.051026` (5 października) jest liczbowo mniejsze od
`2.0.110926` (11 września). Porównywanie wersji jest niemożliwe - jedynym
rosnącym identyfikatorem wydania jest `github.run_number`, który CI już
dokleja do tagu: `v<Version>-<run_number>` (np. `v2.0.110926-123`).

**Każdy push na `main` tworzy wydanie**, także zmiany samego README.
Zdecydowano: **każdy build z wyższym `run_number` jest nową wersją** do
zaproponowania użytkownikowi.

**Wydanie jest przez chwilę niekompletne.** Job `publish` ma
`max-parallel: 1` - pierwszy RID tworzy wydanie, kolejne dokładają pliki.
Aplikacja musi obsłużyć wydanie bez pliku dla swojego systemu.

**Pliki wydania per system** (już publikowane przez CI, bez zmian):

| RID | Plik z wydania | Charakter |
|---|---|---|
| `win-x64` / `win-arm64` | `YouTubeDownloader-win-x64.exe` / `-win-arm64.exe` | pojedynczy plik (single-file) |
| `linux-x64` / `linux-arm64` | `YouTubeDownloader-linux-x64` / `-linux-arm64` | pojedynczy plik (single-file) |
| `osx-x64` / `osx-arm64` | `YouTubeDownloader-osx-x64.zip` / `-osx-arm64.zip` | zip całego folderu publish (nie single-file), traci bit wykonywania |

**Podmiana działającego pliku:** Windows nie pozwala nadpisać działającego
`.exe`, ale pozwala zmienić jego nazwę. Linux i macOS pozwalają zastąpić plik
(nowy i-węzeł, działający proces trzyma stary). Podpisy ad-hoc macOS są
zapisane w samych plikach Mach-O, więc przetrwają przeniesienie.

**Znacznik „pobrane z internetu”:** pliki zapisane przez `HttpClient` nie
dostają `Zone.Identifier` (Windows) ani `com.apple.quarantine` (macOS), więc
SmartScreen/Gatekeeper nie zaprotestują przy restarcie.

## Zakres

**W zakresie:**
- Katalog danych użytkownika (`AppPaths.cs`) i przeniesienie tam narzędzi,
  plików wersji, plików tymczasowych i `theme.txt`.
- Sprzątanie starych plików obok aplikacji (bez migracji - narzędzia
  pobierają się od nowa, motyw wraca do domyślnego).
- Samoaktualizacja (`AppUpdater.cs`) na Windows, Linux i macOS.
- Nowa pozycja menu, sprawdzanie przy starcie.
- `BuildNumber` przekazywany z CI; numer buildu w oknie „Informacje”.
- Poprawka wielokrotnego dopisywania `User-Agent`.
- CI: `BuildNumber` w `dotnet publish`, testy dymne sprawdzające nowy katalog
  danych.
- CLAUDE.md, README (What's New), podbicie `<Version>` na `2.0.280926`.

**Poza zakresem:**
- Zmiana lokalizacji `downloads/` (osobny krok; zostaje obok aplikacji).
- Wersja angielska interfejsu (osobna specyfikacja).
- Snap / Snap Store, pakiet `.app` na macOS.
- Opcja „pomiń tę wersję”, aktualizacje przyrostowe, kanały beta.
- Projekt testów jednostkowych (wymagałby przebudowy repozytorium - główny
  csproj domyślnie wciąga wszystkie `**/*.cs`, a CI uruchamia `dotnet test`
  tylko na nim).

## Architektura / zmiany w plikach

### Nowy `AppPaths.cs`

Mała klasa statyczna, jak istniejący `ThemeSettings.cs`.

```csharp
public static class AppPaths
{
    // Folder aplikacji (bez zmian względem dzisiejszego appDirectory).
    public static string AppDirectory { get; } = AppContext.BaseDirectory;

    // SNAP_USER_COMMON, jeśli ustawiony (snap); w przeciwnym razie
    // LocalApplicationData/YouTubeDownloader:
    //   Windows: %LOCALAPPDATA%\YouTubeDownloader
    //   Linux:   ~/.local/share/YouTubeDownloader
    //   macOS:   ~/Library/Application Support/YouTubeDownloader
    // Tworzony przy pierwszym dostępie (Directory.CreateDirectory).
    public static string DataDirectory { get; }

    public static bool IsSnap => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SNAP"));
}
```

### `MainWindow.axaml.cs` - katalog danych

- `ytDlpPath`, `ffmpegBinPath`, `denoPath`, `denoVersionPath`,
  `ffmpeg_version.txt` (linie 543, 583, 689) oraz pliki tymczasowe
  (`deno.zip` - l. 295, archiwum FFmpeg - l. 512, `ffmpeg_temp/` - l. 521)
  budowane od `AppPaths.DataDirectory` zamiast `appDirectory`.
- `nodeJsPath`: najpierw `DataDirectory`, potem folder aplikacji (ręcznie
  wrzucony Node dalej działa). `node` nigdy nie jest usuwany.
- `downloads/` (l. 941, 980) - **bez zmian**, dalej `appDirectory`.
- `ThemeSettings.Load/Save` wywoływane z `AppPaths.DataDirectory`
  (tu i w `App.axaml.cs:18`).

**Sprzątanie starych plików** - `AppPaths.CleanupLegacyFiles(appDirectory, dataDirectory)`,
wołane na końcu `CheckAndDownloadComponents()`, tylko gdy w nowym miejscu są
już yt-dlp, `ffmpeg_bin/` i dostępny jest runtime JS (lokalny Deno/Node albo
systemowy). Usuwa z folderu aplikacji wyłącznie znane nazwy:
`yt-dlp(.exe)`, `deno(.exe)`, `deno_version.txt`, `ffmpeg_bin/`,
`ffmpeg_version.txt`, `theme.txt`, `ffmpeg_temp/`. Nigdy: `downloads/`,
`node(.exe)`, pliki aplikacji. Zabezpieczenia:
- **Znacznik:** sprzątanie działa tylko, gdy w folderze aplikacji jest
  `ffmpeg_version.txt` - dowód, że folderem zarządzała stara wersja. Folder
  aplikacji bywa folderem Pobrane użytkownika, gdzie `yt-dlp.exe` czy
  `theme.txt` mogą być plikami użytkownika.
- Pliki tymczasowe starych wersji (`deno.zip`, `ffmpeg.zip`,
  `ffmpeg.tar.xz`) **nie są usuwane** - to zbyt ogólne nazwy.
- Znacznik usuwany na końcu i tylko wtedy, gdy wszystko inne się usunęło -
  inaczej sprzątanie ponowi się przy następnym starcie.
- Każdy błąd (np. brak uprawnień, plik zablokowany) pomijany po cichu.
- Nic nie robi, gdy `AppDirectory` i `DataDirectory` to ten sam folder.

**Systemowy Deno - poprawka wymagana przez migrację.** `where deno` na Windows
przeszukuje najpierw **bieżący katalog**, a przy uruchomieniu dwuklikiem jest
nim folder aplikacji. Stary `deno.exe` obok aplikacji zostałby uznany za
„systemowy” - Deno nie pobrałby się do `DataDirectory`, a sprzątanie usunęłoby
jedyną kopię w trakcie działania aplikacji. Dodatkowo `where` zwraca
wszystkie trafienia w osobnych liniach, a `GetRuntimePath()` dziś zwraca cały
wielolinijkowy wynik jako ścieżkę. Nowa metoda `FindSystemDeno()` używana przez
`IsRuntimeInPath()` i `GetRuntimePath()`: pierwsza linia wyniku `where`/`which`
spoza folderu aplikacji (logika wyboru w
`AppPaths.PickFirstPathOutside(output, excludedDirectory)`).

**Awaryjny katalog danych:** jeśli `LocalApplicationData` jest puste (np. brak
`HOME` na Linuksie) albo katalogu nie da się utworzyć, `DataDirectory` =
`AppDirectory` (dzisiejsze zachowanie) - zamiast wyjątku przy starcie.

### Nowy `AppUpdater.cs` - logika aktualizacji (bez UI)

```csharp
public static class AppUpdater
{
    public const string Repo = "bauerpawel/YouTube-Downloader";

    // Z AssemblyMetadata("BuildNumber"); null dla buildu lokalnego.
    public static int? GetLocalBuildNumber();

    // "v2.0.110926-123" -> 123; null, gdy format nie pasuje.
    public static int? ParseBuildNumber(string tagName);

    // YouTubeDownloader-{win|linux|osx}-{x64|arm64}[.exe|.zip]
    // wg OperatingSystem.Is*() i RuntimeInformation.ProcessArchitecture.
    public static string GetAssetName();

    // GET /repos/{Repo}/releases/latest -> ReleaseInfo(tag, build, html_url, Asset).
    // Asset (url, size, digest?) jest null, gdy wydanie nie ma jeszcze pliku
    // dla tego systemu. Błędy sieci/API propagowane jako wyjątek.
    public static Task<ReleaseInfo> GetLatestReleaseAsync(HttpClient http);

    // Test zapisu do AppDirectory (utworzenie i usunięcie pliku próbnego).
    public static bool CanWriteAppDirectory();

    // Rozmiar (+ SHA-256, jeśli API podało digest) zgodny z assetem.
    public static bool VerifyDownload(string path, ReleaseAsset asset);

    // Dwufazowa podmiana: listy (nowyPlik, docelowyPlik).
    // Faza 1: każdy istniejący docelowy -> docelowy + ".old".
    // Faza 2: nowy -> docelowy. Błąd w dowolnym miejscu: przywrócenie .old.
    // Potem MakeExecutable() na głównym pliku (no-op na Windows).
    // Linux/macOS: pliki .old usuwane od razu po udanej podmianie (usunięcie
    // działającego pliku jest tam dozwolone). Windows: zostaje jeden
    // "<exe>.old", usuwany przy następnym starcie.
    public static void ApplyUpdate(IReadOnlyList<(string source, string target)> files);

    // Start nowego procesu z Environment.ProcessPath.
    public static void Relaunch();

    // Przy starcie: usuwa z AppDirectory WYŁĄCZNIE dokładne nazwy:
    // "<exe>.old", "<exe>.new", "YouTubeDownloader-update.zip",
    // "YouTubeDownloader-update/" - nigdy po masce (*.old) ani ogólnych nazw
    // (update.zip), bo folder aplikacji może być np. folderem Pobrane użytkownika.
    // Błędy ignorowane (na Windows stary .exe może być chwilę zablokowany -
    // zniknie przy kolejnym starcie).
    public static void CleanupLeftovers();
}
```

Nazwa pliku wykonywalnego zawsze pochodzi z `Environment.ProcessPath`, nigdy
nie jest zakładana - użytkownicy Windows/Linux uruchamiają plik pod nazwą
assetu (`YouTubeDownloader-win-x64.exe`), nie `YouTubeDownloader.exe`.

`MakeExecutable()` przenoszony z `MainWindow` do `AppPaths` jako
`AppPaths.MakeExecutable()`, żeby używały go i `MainWindow`, i `AppUpdater`.

### `MainWindow.axaml.cs` - przepływ aktualizacji

- Nowa pozycja menu **Narzedzia → „Sprawdz aktualizacje aplikacji”**
  (`MiSprawdzAktualizacje`), obok „Aktualizuj komponenty”. Nowe teksty bez
  polskich znaków - zgodnie z obecną konwencją w całej aplikacji.
- `AppUpdater.CleanupLeftovers()` wołane na starcie (konstruktor, przed
  `Opened`).
- **Przy starcie:** po `CheckAndDownloadComponents()` - `CheckForAppUpdate(silent: true)`.
- **Z menu:** `CheckForAppUpdate(silent: false)`.
- `CheckForAppUpdate(bool silent)`:
  1. Snap → (tylko `!silent`) komunikat „Aktualizacje tej wersji dostarcza
     Snap Store”, koniec.
  2. Brak `BuildNumber` → (tylko `!silent`) komunikat „Wersja zbudowana
     lokalnie - aktualizacje wylaczone”, koniec.
  3. Trwa pobieranie lub aktualizacja komponentów (`!BtnDownload.IsEnabled`)
     → koniec (z menu nie da się tu trafić - pozycja wyłączona).
  4. `GetLatestReleaseAsync()`; błąd sieci / limit API → `silent`: cisza,
     inaczej komunikat z błędem.
  5. Build zdalny ≤ lokalny → `!silent`: „Masz najnowsza wersje”.
  6. Wydanie bez pliku dla systemu → `!silent`: „Nowa wersja jest w trakcie
     publikacji, sprobuj za kilka minut”.
  7. Pytanie `ShowConfirmAsync`: „Dostepna jest nowa wersja aplikacji (build
     N, obecna: M). Zaktualizowac teraz? Aplikacja uruchomi sie ponownie.”
     Odmowa → koniec (zapyta znowu przy następnym starcie).
  8. `CanWriteAppDirectory()` = false → komunikat + propozycja otwarcia
     `html_url` wydania w przeglądarce.
  9. Pobranie przez istniejące `DownloadFileWithProgress()` (pasek postępu):
     Windows/Linux → `<exe>.new`; macOS → `YouTubeDownloader-update.zip`,
     rozpakowany (`ZipFile.ExtractToDirectory`, chroni przed path traversal)
     do `YouTubeDownloader-update/`.
  10. `VerifyDownload()`; niezgodność → usunięcie pobranych plików, komunikat.
  11. `ApplyUpdate()` - Windows/Linux: jedna para (`.new` → exe);
      macOS: każdy plik z `YouTubeDownloader-update/` → odpowiadający w
      `AppDirectory`.
  12. `Relaunch()` i zamknięcie okna/aplikacji
      (`IClassicDesktopStyleApplicationLifetime.Shutdown()`).
- Na czas pobierania filmów i `AktualizujKomponenty_Click()` pozycja menu
  wyłączona razem z `BtnDownload` (te same miejsca, w których dziś
  przełączane jest `BtnDownload.IsEnabled`); także w trakcie samej
  aktualizacji aplikacji.

**Poprawka przy okazji:** `httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(...)`
jest dziś wołane przy każdym zapytaniu (l. 250, 376, 429), więc nagłówek
rośnie z każdym wywołaniem. Ustawiany raz w konstruktorze, wywołania w
metodach usunięte.

### `YouTubeDownloader.csproj`

```xml
<ItemGroup Condition="'$(BuildNumber)' != ''">
  <AssemblyMetadata Include="BuildNumber" Value="$(BuildNumber)" />
</ItemGroup>
```

`<Version>` → `2.0.280926`.

### `AboutWindow.axaml.cs`

`GetAppVersion()` dokleja „(build N)”, gdy `AppUpdater.GetLocalBuildNumber()`
nie jest null: „Wersja 2.0.280926 (build 124)”.

### CI (`.github/workflows/dotnet-desktop.yml`)

- Krok „Publish Application”: `-p:BuildNumber=${{ github.run_number }}`.
- Test dymny Linux: asercja plików w `$HOME/.local/share/YouTubeDownloader/`
  (`yt-dlp`, `ffmpeg_bin/ffmpeg`) zamiast `$SMOKE/`.
- Test dymny macOS: asercja w
  `$HOME/Library/Application Support/YouTubeDownloader/` (`yt-dlp`,
  `ffmpeg_bin/ffmpeg`, `ffmpeg_bin/ffprobe`) zamiast `$SMOKE/`.
- Test dymny nie zobaczy pytania o aktualizację: najnowsze wydanie ma w tym
  momencie `run_number` niższy lub równy testowanemu buildowi.

### CLAUDE.md / README.md

- CLAUDE.md: „Runtime Structure” (folder aplikacji vs katalog danych per
  OS), nowe pliki `AppPaths.cs`/`AppUpdater.cs`, opis mechanizmu
  aktualizacji, `BuildNumber` w CI i sekcji „App Version”, zmienione ścieżki
  w testach dymnych, pozycja menu w liście komponentów UI.
- README: „What's New” dla `2.0.280926` + badge wersji; informacja, gdzie
  teraz leżą narzędzia.

## Obsługa błędów

- Sprawdzanie przy starcie: wszystkie błędy ciche.
- Każdy błąd przed fazą podmiany (pobieranie, weryfikacja, rozpakowanie):
  usunięcie `.new` / `YouTubeDownloader-update.zip` /
  `YouTubeDownloader-update/`, `MessageDialog` z
  błędem, aplikacja działa dalej w starej wersji.
- Błąd w trakcie podmiany: przywrócenie wszystkich `.old`, komunikat.
- Błąd `Relaunch()` po udanej podmianie: komunikat „Zaktualizowano -
  uruchom aplikacje ponownie recznie”, aplikacja się zamyka.

## Testowanie / weryfikacja

- `dotnet build` bez ostrzeżeń (Debug i Release).
- Logika `AppPaths` i `AppUpdater` (celowo bez zależności od Avalonii)
  sprawdzana tymczasowym programem testowym **poza repozytorium** (katalog
  scratchpad sesji, kompiluje te dwa pliki przez `<Compile Include>`), nigdy
  nie commitowanym - zgodnie z decyzją „bez projektu testów w repo”.
- Zielone CI: testy dymne Linux i macOS potwierdzają start i pobranie
  narzędzi do nowego katalogu danych.
- Ręczny test samoaktualizacji na Windows (wymaga dwóch wydań):
  1. uruchomienie buildu N (pierwsze wydanie z tą funkcją),
  2. dowolny push tworzący build N+1,
  3. start N → pytanie o aktualizację → akceptacja,
  4. restart, „Informacje” pokazuje build N+1; brak `*.old` po kolejnym
     starcie.
- Ręcznie na Windows: pozycja menu w buildzie lokalnym pokazuje komunikat o
  wyłączonych aktualizacjach; stare pliki obok exe znikają po pobraniu
  narzędzi do `%LOCALAPPDATA%\YouTubeDownloader`.
- Samoaktualizacja na Linuksie i macOS pozostaje niezweryfikowana, dopóki
  ktoś nie przetestuje jej ręcznie na takiej maszynie.

## Ryzyka / otwarte pytania

- **Pierwsze wydanie z tą funkcją trzeba pobrać ręcznie** - obecne wersje
  nie mają mechanizmu aktualizacji.
- Pole `digest` (SHA-256) w API assetów GitHub - używane, jeśli obecne;
  w przeciwnym razie tylko weryfikacja rozmiaru.
- Limit GitHub API bez uwierzytelnienia (60 zapytań/h na IP) - wystarcza
  przy jednym zapytaniu na start; przy przekroczeniu sprawdzanie przy
  starcie po cichu się nie udaje.
- macOS: pliki usunięte w nowej wersji zostają w folderze aplikacji
  (podmieniamy tylko pliki obecne w zipie) - nieszkodliwe.
- osx-x64 uruchomiony przez Rosettę zgłasza architekturę x64, więc pobierze
  wersję x64 - zgodnie z tym, co użytkownik już uruchamia.
