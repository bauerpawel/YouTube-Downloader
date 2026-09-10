# Cross-platform support, Phase 2a: Linux (x64 + ARM64)

**Data:** 2026-09-10
**Status:** Zaakceptowany, gotowy do planu implementacji

## Cel

Po migracji UI z WinForms na Avalonia (Faza 1, Windows-only), ten etap dodaje
**realne wsparcie Linuksa** (x64 + ARM64): appka ma się dać zbudować,
uruchomić i faktycznie pobierać wideo na Linuksie, nie tylko skompilować.
macOS jest świadomie odłożony na osobny, przyszły etap (inne źródło FFmpeg,
inne RID-y, brak własnych ustaleń technicznych na razie).

## Kontekst

Faza 1 (Avalonia UI) celowo nie ruszała logiki zarządzania zależnościami
(pobieranie/aktualizacja yt-dlp, FFmpeg, Deno) — cała ta logika w
`MainWindow.axaml.cs` jest dziś zaszyta pod Windows: rozszerzenia `.exe`,
komenda `where`, nazwy assetów GitHub dopasowane tylko do buildów
windowsowych, `ZipFile` jako jedyny format archiwum. Ten etap właśnie to
zmienia — to pierwszy raz, kiedy ta logika biznesowa jest świadomie
modyfikowana od czasu migracji na Avalonię.

## Ustalenia techniczne (zweryfikowane empirycznie przed napisaniem tego spec-a)

Sprawdzone bezpośrednio przez `gh api` (listing realnych assetów releasów) i
pobranie + inspekcję (`tar -tf`) prawdziwych archiwów:

| Zależność | Windows (dziś, bez zmian) | Linux x64 (nowe) | Linux ARM64 (nowe) |
|---|---|---|---|
| yt-dlp | `yt-dlp.exe` (bezpośredni download z `.../releases/latest/download/yt-dlp.exe`) | `yt-dlp_linux` (bezpośredni download, bez rozszerzenia) | `yt-dlp_linux_aarch64` |
| Deno | asset `deno-x86_64-pc-windows-msvc.zip` | asset `deno-x86_64-unknown-linux-gnu.zip` (nadal ZIP, ten sam kod ekstrakcji) | asset `deno-aarch64-unknown-linux-gnu.zip` |
| FFmpeg | asset zawierający `win64-gpl-shared` + `.zip` | asset kończący się dokładnie na `linux64-gpl.tar.xz` (**statyczny, bez `-shared`**) | asset kończący się dokładnie na `linuxarm64-gpl.tar.xz` |

**Kluczowe odkrycie o FFmpeg:** wariant `-shared` na Linuksie rozdziela
`bin/` (executable: `ffmpeg`, `ffprobe`, `ffplay`) od `lib/` (biblioteki
`.so`) — dzisiejsza logika "skopiuj wszystko z folderu `bin`" by to
przegapiła, appka nie znalazłaby bibliotek w runtime. Zweryfikowałem, że
wariant **statyczny** (`linux64-gpl.tar.xz`, bez `-shared`) ma **tylko**
`bin/` z trzema statycznie zlinkowanymi binarkami, zero zewnętrznych `.so` —
dokładnie pasuje do istniejącej logiki "skopiuj wszystko z folderu `bin`"
bez żadnych zmian w tej części kodu. Wybieramy statyczny wariant.

**Dopasowanie nazwy assetu FFmpeg** musi być precyzyjne: `EndsWith(".tar.xz")`
+ dokładny sufiks `"linux64-gpl.tar.xz"` / `"linuxarm64-gpl.tar.xz"` (nie
samo `Contains("gpl")`, bo to złapałoby też `-shared` i `lgpl`; zweryfikowane
że `EndsWith` na pełnym sufiksie poprawnie odrzuca oba warianty).

**Format archiwum FFmpeg to `.tar.xz`**, nie ZIP — `System.IO.Compression`
go nie obsługuje. Rozpakowanie przez `Process.Start("tar", ["-xf", archive,
"-C", destDir])` — `tar` jest standardowo obecny na każdym Linuksie i sam
wykrywa kompresję xz, więc jedna komenda robi dekompresję + rozpakowanie.
Zero nowych zależności NuGet.

**Uprawnienia wykonywalności:** ani ZIP, ani proces pobierania surowego pliku
przez `HttpClient` nie ustawiają bitu +x na Linuksie. Po pobraniu/rozpakowaniu
yt-dlp, deno, ffmpeg/ffprobe trzeba jawnie wywołać
`File.SetUnixFileMode(path, ...)` (natywne w .NET, no-op tylko wywoływane
warunkowo na non-Windows).

**Detekcja architektury w runtime:**
`System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture`
(zwraca `Architecture.X64` / `Architecture.Arm64` / itd.) — standardowe API
.NET, używane do wyboru właściwego assetu Deno/FFmpeg/yt-dlp.

## Zakres

**W zakresie:**
- Linux x64 **i** ARM64 razem (te same zmiany kodu, różne stringi assetów —
  oba dostawcy już mają gotowe buildy dla obu architektur).
- Pełna logika: pobieranie zależności, budowanie argumentów yt-dlp,
  uruchamianie procesu, parsowanie postępu — wszystko ma faktycznie działać
  na Linuksie, nie tylko kompilować się.
- `build.sh` (odpowiednik `build.bat` dla Linux/macOS, na razie tylko
  `linux-x64`/`linux-arm64`).
- Rozszerzenie CI o realną weryfikację na `ubuntu-latest` (build + publish +
  headless smoke-test pod `xvfb-run`) — jedyny sposób na rzeczywistą
  weryfikację tej pracy, skoro implementacja dzieje się na Windows.
- Aktualizacja CLAUDE.md.

**Poza zakresem (świadomie odłożone):**
- macOS (osobny, przyszły etap — inne źródło FFmpeg, które trzeba dopiero
  zbadać, bo BtbN/FFmpeg-Builds nie ma buildów macOS).
- Jakiekolwiek pakowanie natywne (AppImage, `.deb`, `.rpm`) — zostajemy przy
  tym samym modelu co Windows: pojedynczy self-contained executable, bez
  instalatora.
- Refaktor logiki niezwiązany z przenośnością (żadnych zmian w UI, w
  algorytmie budowania argumentów yt-dlp, w regexach parsowania postępu —
  tylko adaptacje potrzebne do działania na Linuksie).

## Architektura / zmiany w plikach

### `MainWindow.axaml.cs` (jedyny plik z logiką biznesową)

Nowe prywatne pola/helpery:
- `private static bool IsWindows => OperatingSystem.IsWindows();` (lub
  bezpośrednie wywołania `OperatingSystem.IsWindows()` inline — do ustalenia
  w trakcie implementacji, oba warianty są tanie).
- Ścieżki (`ytDlpPath`, `denoPath`, `nodeJsPath`) budowane warunkowo: z
  `.exe` na Windows, bez rozszerzenia na Linuksie.

Zmienione metody (adaptacja, nie przepisanie):
- `GetLatestDenoInfo()` — dopasowanie nazwy assetu warunkowe po OS +
  architekturze (`RuntimeInformation.ProcessArchitecture`).
- `DownloadYtDlp()` — URL do pobrania zależny od OS + architektury (Windows:
  dzisiejszy URL bez zmian; Linux: `yt-dlp_linux` / `yt-dlp_linux_aarch64`).
  Po pobraniu na non-Windows: `MakeExecutable(ytDlpPath)`.
- `GetLatestFFmpegInfo()` — dopasowanie nazwy assetu warunkowe (Windows:
  dzisiejszy `win64-gpl-shared` + `.zip` bez zmian; Linux: dokładny sufiks
  `linux64-gpl.tar.xz` / `linuxarm64-gpl.tar.xz`).
- `DownloadFFmpeg()` — po pobraniu: na Windows dzisiejsza ścieżka
  `ZipFile.ExtractToDirectory` bez zmian; na Linuksie nowa gałąź wywołująca
  `tar` przez `Process.Start`, potem `MakeExecutable` na `ffmpeg`/`ffprobe`
  w skopiowanym `ffmpeg_bin/`. Logika "znajdź folder `bin` i skopiuj jego
  zawartość" zostaje **bez zmian** — działa identycznie dla obu OS-ów dzięki
  wyborowi statycznego wariantu FFmpeg.
- `DownloadDeno()` — po ekstrakcji ZIP na non-Windows: `MakeExecutable(denoPath)`.
- `IsRuntimeInPath()` / `GetRuntimePath()` — `FileName = "where"` → warunkowo
  `"where"` (Windows) / `"which"` (Linux).

Nowa prywatna metoda pomocnicza:
```csharp
private static void MakeExecutable(string path)
{
    if (OperatingSystem.IsWindows())
        return;

    File.SetUnixFileMode(path,
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
}
```

Ekstrakcja FFmpeg na Linuksie przez `tar` (nowy kod, nie port — bezpieczne
budowanie argumentów przez `ArgumentList`, nie ręczne sklejanie stringa):
```csharp
var processInfo = new ProcessStartInfo
{
    FileName = "tar",
    UseShellExecute = false,
    CreateNoWindow = true
};
processInfo.ArgumentList.Add("-xf");
processInfo.ArgumentList.Add(zipPath);
processInfo.ArgumentList.Add("-C");
processInfo.ArgumentList.Add(tempExtractPath);
```

**Bez zmian w tym etapie:** `BuildYtDlpArguments()`, `ParseDownloadProgress()`,
`NormalizeUrl()`/`ValidateUrl()`, `BtnDownload_Click()`/
`DownloadSingleUrlAsync()`, cała logika UI (multi-URL, motyw, About, menu) —
te działają już dziś cross-platform bez zmian (czysty BCL + Avalonia).

### `YouTubeDownloader.csproj`

Bez zmian strukturalnych — publikacja nadal przez `dotnet publish -r <rid>`,
Avalonia i cały BCL już są cross-platformowe. `<ApplicationIcon>app.ico</ApplicationIcon>`
jest honorowane tylko przy publikacji na Windows i tak zostaje (na Linuksie
MSBuild go po prostu ignoruje, bez błędu — do zweryfikowania w trakcie
implementacji, niskie ryzyko).

### `build.sh` (nowy plik)

Odpowiednik `build.bat`: bash, przyjmuje RID jako argument (`linux-x64`
domyślnie, akceptuje też `linux-arm64`), waliduje wejście, wywołuje
`dotnet publish` z tymi samymi flagami co Windows (self-contained,
single-file), wypisuje ścieżkę wynikową.

### CI (`.github/workflows/dotnet-desktop.yml`)

- Job `build`: macierz rozszerzona o wymiar `os: [windows-latest, ubuntu-latest]`
  (obok istniejącego `configuration: [Debug, Release]`) — `runs-on: ${{ matrix.os }}`.
  Realnie weryfikuje że projekt buduje się na obu platformach na każdy push.
- Job `publish`: macierz zmieniona z płaskiej listy `rid` na
  `matrix.include` parujące `rid` z właściwym `os`:
  ```yaml
  strategy:
    max-parallel: 1
    matrix:
      include:
        - rid: win-x64
          os: windows-latest
        - rid: win-arm64
          os: windows-latest
        - rid: linux-x64
          os: ubuntu-latest
        - rid: linux-arm64
          os: ubuntu-latest
  runs-on: ${{ matrix.os }}
  ```
  (`linux-arm64` to standardowy .NET RID — do potwierdzenia przy pierwszym
  `dotnet publish`, ale nazewnictwo jest udokumentowane i stabilne).
  Kroki publikacji zostają w PowerShell (`shell: pwsh`) na obu platformach —
  `ubuntu-latest` ma preinstalowany PowerShell Core, więc jeden skrypt
  zamiast dublowania w bash. Do zweryfikowania w trakcie implementacji, że
  `Compress-Archive`/`Copy-Item` faktycznie działają pod `pwsh` na Ubuntu
  (oczekiwane: tak, to standardowe cmdlety PowerShell Core, ale nie
  testowane w tym środowisku).
- Nowy krok w joby `publish`, tylko dla `rid` zaczynających się na `linux`:
  headless smoke-test appki pod `xvfb-run` (uruchomienie self-contained
  exe z wirtualnym X11 displayem, sprawdzenie że proces startuje i nie
  crashuje w ciągu kilku sekund, potem zabicie procesu) — to jedyna realna
  weryfikacja działania appki na Linuksie w tym projekcie, bo lokalnie
  (Windows) można tylko sprawdzić że `dotnet publish -r linux-x64` się
  buduje, nie że binarka faktycznie odpala się i działa.

### CLAUDE.md

Aktualizacja sekcji opisujących logikę zależności (dziś opisuje tylko
Windows-owe ścieżki/komendy) o rozgałęzienia per-OS, oraz o nowe RID-y i
`build.sh`.

## Obsługa błędów

Bez zmian filozofii względem Fazy 1: `try/catch` wokół operacji
zewnętrznych, komunikaty przez istniejący `MessageDialog`. Nowa gałąź `tar`
(ekstrakcja FFmpeg) dostaje ten sam wzorzec obsługi błędu co dzisiejsza
ekstrakcja ZIP (rzuć wyjątek z czytelnym komunikatem jeśli `tar` zwróci
niezerowy kod wyjścia, złap go w istniejącym `catch` w `DownloadFFmpeg()`).

## Testowanie / weryfikacja

Ten etap ma **fundamentalnie inne ograniczenie weryfikacyjne** niż Faza 1:
implementacja dzieje się na Windows, więc **nie da się lokalnie uruchomić
ani smoke-testować binarki linuksowej**. Weryfikacja lokalna ogranicza się
do: `dotnet build` (bez zmian, nadal Windows), `dotnet publish -r linux-x64`
i `-r linux-arm64` **kończą się sukcesem** (sam fakt kompilacji/publikacji
cross-compile, nie uruchomienia). Rzeczywista weryfikacja działania appki
na Linuksie żyje wyłącznie w CI (`ubuntu-latest` + `xvfb-run` smoke-test) —
to nowy, wyższy poziom zaufania niż lokalny smoke-test z Fazy 1, ale
wymaga, żeby ten etap uznać za w pełni zweryfikowany dopiero po zielonym
przebiegu CI na Ubuntu, nie tylko po lokalnym buildzie.

## Ryzyka / otwarte pytania do etapu implementacji

- Dokładna nazwa .NET RID dla Linux ARM64 (`linux-arm64` — standardowa,
  ale zweryfikować przy pierwszym `dotnet publish`).
- Czy `Compress-Archive`/`Copy-Item` pod `pwsh` na `ubuntu-latest` faktycznie
  działają identycznie jak na Windows — nie przetestowane w tym środowisku,
  ryzyko niskie (to natywne cmdlety PowerShell Core), ale pierwszy zielony
  przebieg CI na Ubuntu jest jedynym prawdziwym potwierdzeniem.
- Czy `<ApplicationIcon>` w `.csproj` powoduje błąd publikacji na Linuksie
  (oczekiwane: nie, MSBuild po prostu go ignoruje poza Windows) — do
  potwierdzenia przy pierwszym CI runie na Ubuntu.
- `xvfb-run` musi być zainstalowany na `ubuntu-latest` runnerze GitHub —
  do zweryfikowania (oczekiwane: dostępny przez `apt-get install -y xvfb`
  jako krok przygotowawczy, standardowy pakiet).
