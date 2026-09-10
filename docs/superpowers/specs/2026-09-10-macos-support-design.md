# Cross-platform support, Phase 2b: macOS (x64 + ARM64)

**Data:** 2026-09-10
**Status:** Zaakceptowany, gotowy do planu implementacji

## Cel

Po wsparciu Linuksa (Faza 2a), ten etap dodaje **realne wsparcie macOS**
(x64 + ARM64): appka ma się dać zbudować, uruchomić i faktycznie pobierać
wideo na macOS, weryfikowane przez CI na `macos-latest`, tak samo jak
Linux jest weryfikowany na `ubuntu-latest`.

## Kontekst

Faza 2a (Linux) już rozłożyła logikę zarządzania zależnościami
(`MainWindow.axaml.cs`) na gałęzie zależne od OS-a (`OperatingSystem.IsWindows()`),
i celowo dodała `PlatformNotSupportedException` w trzech helperach
(`GetDenoAssetName`, `GetYtDlpAssetName`, `IsMatchingFFmpegAsset`) na
wypadek uruchomienia na macOS przed tym etapem — te guardy dziś rzucają
wyjątek na macOS, co jest poprawnym, zamierzonym zachowaniem "jeszcze nie
wspieramy tego" do czasu tego etapu.

## Ustalenia techniczne (zweryfikowane empirycznie przed napisaniem tego spec-a)

| Zależność | Windows/Linux (bez zmian) | macOS (nowe) |
|---|---|---|
| yt-dlp | per-OS/arch nazwa assetu | **jeden** asset `yt-dlp_macos` — build `universal2` (zweryfikowane w workflow yt-dlp: `--target-architecture universal2`), działa natywnie na x64 **i** arm64 bez rozróżniania architektury |
| Deno | `deno-*-{pc-windows-msvc,unknown-linux-gnu}.zip` | `deno-x86_64-apple-darwin.zip` / `deno-aarch64-apple-darwin.zip` — ten sam format ZIP, ten sam wewnętrzny plik `deno` (zweryfikowane pobraniem i `unzip -l`) — **istniejący kod `DownloadDeno()` już działa bez zmian**, bo już rozróżnia tylko `IsWindows()` vs nie |
| FFmpeg | BtbN/FFmpeg-Builds (repo GitHub) | BtbN **nie publikuje buildów macOS**. Źródło: **`eugeneware/ffmpeg-static`** (GitHub, aktywnie utrzymywany - ostatni release 2025-11-14, używany też jako pakiet npm `ffmpeg-static` w ekosystemie Electron/Node, więc sprawdzony w praktyce). Assety `ffmpeg-darwin-arm64`/`ffmpeg-darwin-x64`/`ffprobe-darwin-arm64`/`ffprobe-darwin-x64` - **surowe binarki, nie archiwum** (statyczne, bez zewnętrznych zależności - potwierdzone opisem repo "static ffmpeg/ffprobe binaries") |

**Kluczowa różnica strukturalna:** ponieważ FFmpeg dla macOS pochodzi z innego
repozytorium i nie jest archiwum (dwa osobne pliki zamiast jednego
`.zip`/`.tar.xz` z folderem `bin/`), nie da się tego dołożyć jako kolejnej
gałęzi w istniejącym `IsMatchingFFmpegAsset()`/`GetLatestFFmpegInfo()`/
`DownloadFFmpeg()` (te zapytują `BtbN/FFmpeg-Builds` i rozpakowują
archiwum). Potrzebne są osobne metody dla macOS: `GetLatestFFmpegInfoMac()`
(zapytuje `eugeneware/ffmpeg-static`, zwraca **dwa** URL-e) i
`DownloadFFmpegMac()` (pobiera oba pliki bezpośrednio do `ffmpeg_bin/`,
zero rozpakowywania). `DownloadFFmpeg()` rozgałęzia się na samym początku:
macOS → `DownloadFFmpegMac()` i `return`; Windows/Linux → dotychczasowa
ścieżka bez zmian.

**CI, architektura runnera (zweryfikowane wyszukiwaniem):** `macos-latest`
wskazuje dziś na `macos-14`-klasy obraz - **Apple Silicon (ARM64)**.
Intel/x86_64 runnery macOS (`macos-13`, `macos-15-intel`) wciąż istnieją,
ale są w trakcie wygaszania (Apple przestał wspierać x86_64, GitHub
zapowiedział wycofanie jesienią 2027) - nie budujemy na nich zależności na
przyszłość tego projektu.

**Kluczowa różnica względem Linuksa:** ARM64 Mac **potrafi** uruchomić
binarkę x86_64 przez Rosettę 2 - ale Rosetta **nie jest domyślnie
zainstalowana** na hostowanych runnerach GitHub (zweryfikowane
wyszukiwaniem), trzeba ją jawnie zainstalować:
`softwareupdate --install-rosetta --agree-to-license` (wymaga sudo, bez
promptu z tą flagą). Dzięki temu **jeden runner (`macos-latest`) wystarczy
do zbudowania i przetestowania obu architektur macOS** - w przeciwieństwie
do Linuksa, gdzie `ubuntu-latest` (x64) fizycznie nie mógł uruchomić
binarki ARM64 bez QEMU. `dotnet publish -r osx-x64`/`-r osx-arm64` to
zwykła cross-kompilacja niezależna od architektury hosta (już wielokrotnie
zweryfikowane dla innych RID-ów w tym projekcie), więc oba RID-y buduje ten
sam runner bez problemu - tylko test dymny binarki x64 potrzebuje
wcześniejszego kroku instalacji Rosetty.

**Otwarte ryzyko (nie do zweryfikowania stąd):** czy hostowane runnery
macOS GitHub Actions mają dostępną sesję okien/GUI bez dodatkowego kroku
(analogicznie do `xvfb` na Linuksie). Duże prawdopodobieństwo że tak -
te same obrazy są używane do testów UI Xcode/Simulatora, co wymaga
działającej sesji graficznej - ale nie potwierdzone empirycznie z tego
środowiska. Jeśli test dymny się nie uruchomi z powodu braku sesji GUI,
naprawa (dodanie odpowiednika `xvfb-run` dla macOS, jeśli istnieje, albo
innego obejścia) będzie wymagała iteracji po pierwszym realnym uruchomieniu
CI - dokładnie tak samo jak dla Linuksa wcześniej w tym projekcie.

## Zakres

**W zakresie:**
- macOS x64 **i** ARM64 razem (ten sam wzorzec co Linux - jeden runner,
  jeden plan).
- Pełna logika: pobieranie zależności (yt-dlp, Deno, FFmpeg z osobnego
  źródła), uruchamianie, parsowanie postępu - ma faktycznie działać na
  macOS, nie tylko kompilować.
- Rozszerzenie `build.sh` o `osx-x64`/`osx-arm64` (ten sam skrypt, więcej
  akceptowanych RID-ów - bez nowego pliku).
- Rozszerzenie CI o `macos-latest`: build (3. gałąź OS-a obok
  windows-latest/ubuntu-latest), publish + smoke-test dla obu RID-ów macOS
  (jeden runner, krok instalacji Rosetty tylko dla `osx-x64`).
- Aktualizacja CLAUDE.md i README.md, **z jawną wzmianką o Gatekeeper**
  (patrz niżej).

**Poza zakresem (świadomie odłożone):**
- Podpisywanie kodu / notaryzacja Apple - wymaga konta Apple Developer
  Program (99$/rok) po stronie użytkownika, którego nie mam jak założyć w
  jego imieniu. Zdecydowano: **nic teraz**, appka będzie działać, ale
  użytkownicy macOS zobaczą ostrzeżenie Gatekeeper przy pierwszym
  uruchomieniu pobranej binarki (standardowa sytuacja dla appek
  open-source/hobbystycznych dystrybuowanych poza App Store) - obejście
  jawnie udokumentowane w README (kliknięcie prawym → Otwórz, albo
  `xattr -d com.apple.quarantine`). Podpisywanie może zostać dodane
  później jako osobny etap, jeśli użytkownik zdobędzie konto Developer.
- Jakiekolwiek pakowanie natywne (`.app` bundle, `.dmg`) - zostajemy przy
  tym samym modelu co Windows/Linux: pojedynczy self-contained
  executable, bez instalatora/bundla.
- Refaktor logiki niezwiązany z przenośnością.

## Architektura / zmiany w plikach

### `MainWindow.axaml.cs`

Zmienione metody (adaptacja, nie przepisanie):
- `GetDenoAssetName()` - dopisanie gałęzi macOS (`OperatingSystem.IsMacOS()`)
  zamiast dzisiejszego `throw` w gałęzi "nie-Windows-nie-Linux": arch-owe
  dopasowanie `deno-{x86_64,aarch64}-apple-darwin.zip`, ten sam wzorzec co
  gałąź Linux.
- `GetYtDlpAssetName()` - dopisanie gałęzi macOS: zwraca zawsze
  `"yt-dlp_macos"` niezależnie od architektury (universal2 - nie ma
  potrzeby sprawdzać `RuntimeInformation.ProcessArchitecture` w tej
  gałęzi).
- `IsMatchingFFmpegAsset()` - **bez zmian.** macOS w ogóle nie woła tej
  metody - ma własną ścieżkę pobierania (patrz niżej), a `DownloadFFmpeg()`
  wraca wcześniej dla macOS (patrz punkt poniżej), więc istniejący
  fallback-`throw` w tej metodzie pozostaje technicznie nieosiągalny dla
  macOS. Nie dotykamy tej metody w ogóle.
- `DownloadFFmpeg()` - na samym początku ciała metody: rozgałęzienie
  `if (OperatingSystem.IsMacOS()) { await DownloadFFmpegMac(); return; }`,
  reszta metody (Windows/Linux, archiwum + `bin/`) bez zmian.

Nowe metody:
```csharp
private static string GetFFmpegMacAssetName(bool isFFprobe)
{
    bool isArm = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
    string arch = isArm ? "arm64" : "x64";
    return (isFFprobe ? "ffprobe-darwin-" : "ffmpeg-darwin-") + arch;
}

private async Task<(string ffmpegUrl, string ffprobeUrl, string version)> GetLatestFFmpegInfoMac()
{
    // Odpytuje api.github.com/repos/eugeneware/ffmpeg-static/releases/latest,
    // dopasowuje assety po dokładnej nazwie z GetFFmpegMacAssetName(),
    // zwraca oba URL-e + tag_name jako "wersję".
}

private async Task DownloadFFmpegMac()
{
    // Pobiera oba pliki bezpośrednio do ffmpeg_bin/ffmpeg i ffmpeg_bin/ffprobe
    // (Directory.CreateDirectory(ffmpegBinPath) najpierw), MakeExecutable() na
    // każdym, zapisuje ffmpeg_version.txt. Zero rozpakowywania - to surowe binarki.
}
```

**Bez zmian:** `MakeExecutable()`, ekstrakcja ZIP w `DownloadDeno()`
(już działa dla macOS, bo już rozróżnia tylko Windows/nie-Windows),
`DownloadYtDlp()` (już generyczna, tylko `GetYtDlpAssetName()` dostaje
nową gałąź), `IsRuntimeInPath()`/`GetRuntimePath()` (już `where`/`which`
warunkowo, `which` działa identycznie na macOS i Linuksie), cała reszta
logiki UI/pobierania.

### `build.sh`

Rozszerzenie listy akceptowanych RID-ów o `osx-x64`/`osx-arm64` (ten sam
plik, ta sama logika `dotnet publish` - bez zmian flag).

### CI (`.github/workflows/dotnet-desktop.yml`)

- Job `build`: macierz `os` rozszerzona o `macos-latest` (obok
  `windows-latest`/`ubuntu-latest`).
- Job `publish`: `matrix.include` dostaje dwa nowe wpisy (`osx-x64`,
  `osx-arm64`), oba z `os: macos-latest`, `exeName: YouTubeDownloader`
  (bez rozszerzenia, jak Linux), własne `releaseExeName`.
- Kroki `chmod +x` (już istniejące dla `runner.os == 'Linux'`) rozszerzone
  o `|| runner.os == 'macOS'` (ten sam powód - bit wykonywalności trzeba
  ustawić jawnie).
- Nowy krok "Install Rosetta (osx-x64 on ARM64 runner)":
  `if: matrix.rid == 'osx-x64'`, `softwareupdate --install-rosetta --agree-to-license`.
- Nowy krok testu dymnego dla macOS: `if: runner.os == 'macOS'` (pokrywa
  obie architektury macOS z jednego runnera - `osx-arm64` natywnie,
  `osx-x64` przez Rosettę po poprzednim kroku), ten sam wzorzec co
  naprawiony test Linux: izolowany katalog tymczasowy (`$RUNNER_TEMP`),
  realna asercja że `yt-dlp` i `ffmpeg_bin/ffmpeg` zostały pobrane i są
  wykonywalne, nie tylko "proces żyje".

### CLAUDE.md / README.md

Dokumentacja nowej logiki (źródło FFmpeg dla macOS, universal2 yt-dlp,
niuans Rosetty w CI) oraz **jawna, widoczna wzmianka o Gatekeeper** w
README (sekcja użytkowania/instalacji) - użytkownicy macOS muszą wiedzieć
z góry, że zobaczą ostrzeżenie systemowe i jak je obejść, zamiast myśleć że
appka jest zepsuta/złośliwa.

## Obsługa błędów

Bez zmian filozofii: `try/catch` wokół operacji zewnętrznych,
`MessageDialog` dla błędów użytkownika. `DownloadFFmpegMac()` dostaje
analogiczny `try/catch` jak dzisiejszy `DownloadFFmpeg()`.

## Testowanie / weryfikacja

Ten sam model co Linux: lokalnie (Windows) tylko `dotnet publish -r osx-x64`
i `-r osx-arm64` **kończą się sukcesem** (kompilacja/cross-publish, nie
uruchomienie - nie da się uruchomić binarki macOS z Windows). Rzeczywista
weryfikacja działania żyje wyłącznie w CI (`macos-latest` + asercje
plikowe w teście dymnym) - tak samo jak dla Linuksa, ten etap jest w pełni
zweryfikowany dopiero po zielonym przebiegu CI, nie po lokalnym buildzie.

## Ryzyka / otwarte pytania do etapu implementacji

- Czy hostowany runner macOS ma domyślnie działającą sesję GUI (patrz
  wyżej) - nieznane stąd, do potwierdzenia pierwszym realnym uruchomieniem
  CI.
- Dokładna nazwa .NET RID dla macOS ARM64 to `osx-arm64` (standardowa,
  jak `linux-arm64`) - do potwierdzenia przy pierwszym `dotnet publish`.
- `eugeneware/ffmpeg-static` nie ma tak częstych/ciągłych wydań jak
  BtbN (BtbN publikuje praktycznie ciągłe "autobuild-" z mastera FFmpeg;
  eugeneware/ffmpeg-static ma rzadsze, konkretne wersje - ostatnia
  `b6.1.1` z 2025-11-14). To akceptowalna niespójność "świeżości" FFmpeg
  między platformami, nie blokująca - FFmpeg 6.1.x jest w pełni
  funkcjonalny do transkodowania/remuksowania, co jest jedynym użyciem w
  tej appce.
