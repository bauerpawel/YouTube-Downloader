# Wersja snap w Snap Store (`yt-downloader-bp`)

**Data:** 2026-09-30
**Status:** Do przeglądu

## Cel

Użytkownicy Ubuntu (i innych dystrybucji ze snapd) instalują aplikację
jednym poleceniem `sudo snap install yt-downloader-bp` i dostają nowe wersje
automatycznie ze Snap Store - każdy push na `main`, który tworzy release na
GitHubie, publikuje też snapa w kanale `stable`, na amd64 i arm64.

## Kontekst

- Kod jest już częściowo gotowy (specyfikacja
  `2026-09-28-data-dir-and-self-update-design.md`): `AppPaths.IsSnap`
  wyłącza samoaktualizację (komunikat `Strings.AppUpdateSnap` - „Aktualizacje
  tej wersji dostarcza Snap Store”), a katalog danych to `$SNAP_USER_COMMON`.
  Tamta specyfikacja odłożyła sam snap na osobny etap - to jest ten etap.
- Brakuje: pakowania (`snapcraft.yaml`), joba w CI i jednej zmiany w kodzie -
  folder pobranych leży dziś obok exe (`<folder aplikacji>/downloads`), a w
  snapie folder aplikacji (`$SNAP`) jest tylko do odczytu.

## Decyzje (ustalone z właścicielem)

| Temat | Decyzja |
|---|---|
| Dystrybucja | Snap Store (nie plik `.snap` w release'ie) |
| Nazwa | `yt-downloader-bp` - zarejestrowana przez właściciela; tytuł w Store: „YouTube Downloader” |
| Kanał | Każdy push na `main` → od razu `stable`, jak GitHub Releases |
| Pobrane filmy w snapie | Systemowy folder pobranych (XDG) + `YouTube Downloader`, np. `~/Pobrane/YouTube Downloader` |
| Budowa | Podejście A: GitHub Actions pakuje gotowy, już przetestowany plik single-file z joba `publish` |
| Dane logowania CI | Sekret `SNAPCRAFT_STORE_CREDENTIALS` - już dodany przez właściciela (2026-09-30) |

Odrzucone podejścia do budowy: „Build from GitHub” w snapcraft.io (Launchpad
wydaje automatycznie tylko do `edge`, budowa .NET wymagałaby własnego
skryptu, brak testu i `BuildNumber`) oraz ręczne wgrywanie pliku `.snap`
(wymaga snapcrafta na Linuksie przy każdym wydaniu).

## Ustalenia techniczne

Sprawdzone w źródłach snapd (`interfaces/apparmor/template.go`,
`interfaces/builtin/desktop.go`, `interfaces/builtin/home.go`):

- **Strict confinement wystarcza** - klasyczny nie jest potrzebny (i wymagałby
  ręcznej zgody Store):
  - `owner @{HOME}/snap/@{SNAP_INSTANCE_NAME}/** mrkix` oraz
    `.../common/** wl` - aplikacja może pobrać yt-dlp, Deno i FFmpeg do
    `$SNAP_USER_COMMON` (`~/snap/yt-downloader-bp/common/`) i je uruchamiać.
  - `/tmp/** mrwlkix` - yt-dlp dla Linuksa (PyInstaller onefile) rozpakowuje
    biblioteki do `/tmp` i je ładuje; `/tmp` snapa jest prywatny, ale dozwolony.
  - .NET single-file rozpakowuje natywne biblioteki (Skia, HarfBuzz) do
    `$HOME/.net`, a `$HOME` w snapie to `~/snap/yt-downloader-bp/<rev>/` -
    zapis i ładowanie dozwolone.
- **Folder pobranych jest osiągalny:** interfejs `desktop` pozwala czytać
  `owner @{HOME}/.config/user-dirs.* r` z prawdziwego katalogu domowego, a
  `home` zapisywać nieukryte pliki w `@{HOME}` (prawdziwym, nie snapowym).
  Oba łączą się automatycznie na Ubuntu desktop. Prawdziwy katalog domowy
  podaje snapd w `$SNAP_REAL_HOME` (`$HOME` wskazuje na katalog snapa).
- **Reszta kodu działa w snapie bez zmian:** `CleanupLeftovers()` i
  `CleanupLegacyFiles()` nie znajdą niczego w `$SNAP` (brak znacznika), `which
  deno` widzi tylko środowisko snapa, więc Deno pobierze się do katalogu
  danych; linki z okna „Informacje” (`UseShellExecute = true` → `xdg-open`)
  obsługuje wbudowany w snapd `xdg-open` (interfejs `desktop`); zmienne
  środowiskowe (np. `YTD_GITHUB_TOKEN` w CI) przechodzą do aplikacji.

## Zakres

**W zakresie:**
- `snap/snapcraft.yaml` i `snap/gui/yt-downloader-bp.desktop`.
- `AppPaths.DownloadsDirectory` + czysta funkcja rozwiązująca folder XDG;
  użycie w `MainWindow`.
- Job `snap` w `.github/workflows/dotnet-desktop.yml`: budowa, test dymny
  zainstalowanego snapa, publikacja do `stable` (amd64 + arm64).
- CLAUDE.md, README (instalacja przez snap PL/EN, „Co nowego”).

**Poza zakresem:**
- Zmiana folderu pobranych poza snapem (zostaje `<folder aplikacji>/downloads`,
  zgodność wstecz).
- Wybór folderu pobranych przez użytkownika.
- Plik `.snap` jako asset release'u na GitHubie.
- Ubuntu Core (tam `home` nie łączy się automatycznie), inne architektury niż
  amd64/arm64.
- Opis w Store poza polami `snapcraft.yaml` (zrzuty ekranu, kategorie -
  właściciel może je dodać w panelu snapcraft.io).

## Architektura / zmiany w plikach

### Nowy `snap/snapcraft.yaml`

```yaml
name: yt-downloader-bp
title: YouTube Downloader
summary: Download YouTube videos and audio - a simple desktop app for yt-dlp
description: |
  Download videos (MP4, WebM, MKV, up to 4K) or audio only (MP3) from
  YouTube, several links in one go. The app fetches and updates its own
  tools (yt-dlp, FFmpeg, Deno) - nothing else to install. Polish and
  English interface. Files are saved to your Downloads folder, in
  "YouTube Downloader".

  Pobieranie filmów (MP4, WebM, MKV, do 4K) lub samego dźwięku (MP3) z
  YouTube, także wielu linków naraz. Aplikacja sama pobiera i aktualizuje
  swoje narzędzia (yt-dlp, FFmpeg, Deno). Interfejs po polsku i angielsku.
  Pliki trafiają do folderu Pobrane, do „YouTube Downloader”.
license: Apache-2.0
icon: logo.svg
website: https://github.com/bauerpawel/YouTube-Downloader
source-code: https://github.com/bauerpawel/YouTube-Downloader
issues: https://github.com/bauerpawel/YouTube-Downloader/issues
base: core24
grade: stable
confinement: strict
adopt-info: app

platforms:
  amd64:
  arm64:

apps:
  yt-downloader-bp:
    command: bin/YouTubeDownloader
    extensions: [gnome]
    plugs: [home, network]

parts:
  app:
    plugin: dump
    source: snap-bin/
    organize:
      YouTubeDownloader: bin/YouTubeDownloader
    prime:
      - -version.txt
    stage-packages:
      - libicu74
      - libssl3t64
      - libice6
      - libsm6
    override-build: |
      craftctl default
      craftctl set version="$(cat "$CRAFT_PART_SRC/version.txt")"
```

- `snap-bin/` nie jest w repo - tworzy go CI (plik single-file z artefaktu
  joba `publish` + `version.txt`). Dopisać `snap-bin/` do `.gitignore`.
- Rozszerzenie `gnome` (content snap `gnome-46-2404`) daje interfejsy
  `desktop`, `desktop-legacy`, `x11`, `wayland`, `opengl`, konfigurację
  czcionek, kursorów i portali. Koszt: jednorazowe pobranie wspólnego
  pakietu (kilkaset MB) u użytkownika, który nie ma innych snapów GNOME.
- `stage-packages`: ICU (globalizacja .NET), OpenSSL (HTTPS w .NET na Linuksie),
  `libice6`/`libsm6` (X11 dla Avalonii - te same biblioteki doinstalowuje dziś
  test dymny Linuksa). Duplikaty z content snapa/bazy są nieszkodliwe.
- Wersja snapa = tag release'u bez `v`, np. `2.0.300926-27` (limit Store: 32
  znaki, dozwolone `[A-Za-z0-9.+~-]`).
- Folder aplikacji w snapie to `$SNAP/bin/` (`AppContext.BaseDirectory`).

### Nowy `snap/gui/yt-downloader-bp.desktop`

`Name=YouTube Downloader`, `Exec=yt-downloader-bp`,
`Icon=${SNAP}/meta/gui/icon.svg` (snapcraft kopiuje tam `icon: logo.svg`),
`Categories=AudioVideo;Network;`, `Comment` po angielsku i `Comment[pl]` po
polsku.

### `AppPaths.cs`

```csharp
public const string SnapDownloadsFolderName = "YouTube Downloader";

// Poza snapem: <folder aplikacji>/downloads (bez zmian). W snapie folder
// aplikacji jest tylko do odczytu: systemowy folder pobranych + podfolder.
public static string DownloadsDirectory { get; }

// Czysta funkcja: treść user-dirs.dirs (null = brak pliku) + prawdziwy
// katalog domowy -> systemowy folder pobranych.
public static string ResolveUserDownloadDirectory(string? userDirsContent, string realHome);
```

Reguły `ResolveUserDownloadDirectory` (format z xdg-user-dirs):
- Linie puste i zaczynające się od `#` są pomijane; brana jest ostatnia linia
  `XDG_DOWNLOAD_DIR=...` (jak przypisanie w shellu).
- Wartość bez otaczających cudzysłowów. `$HOME/<ścieżka>` → `realHome/<ścieżka>`;
  ścieżka bezwzględna (`/...`) → bez zmian.
- `$HOME` lub `$HOME/` (xdg-user-dirs: folder wyłączony), wartość pusta,
  względna lub nierozpoznana, brak linii, `userDirsContent == null` →
  `realHome/Downloads`.
- Sekwencje ucieczki shella w wartości (`\"`, `\$`) nie są obsługiwane -
  xdg-user-dirs sam ich nie zapisuje dla zwykłych nazw folderów.

`DownloadsDirectory` w snapie: `realHome` = `$SNAP_REAL_HOME`, a gdy pusty -
`Environment.GetFolderPath(SpecialFolder.UserProfile)` (snapowy `$HOME`,
zapisywalny). Treść pliku `realHome/.config/user-dirs.dirs` czytana w
try/catch (`IOException`, `UnauthorizedAccessException` → `null`). Wynik:
`Path.Combine(ResolveUserDownloadDirectory(...), SnapDownloadsFolderName)`.
Wartość liczona raz (`Lazy<string>`, jak `DataDirectory`).

### `MainWindow.axaml.cs`

Oba miejsca `Path.Combine(appDirectory, "downloads")` (`BtnDownload_Click()`,
`DownloadSingleUrlAsync()`) → `AppPaths.DownloadsDirectory`. Istniejące
`Directory.CreateDirectory(downloadsDir)` tworzy brakujący podfolder (i
ewentualnie cały łańcuch). Komunikat po pobraniu już wypisuje tę ścieżkę.
Bez nowych tekstów w `Strings.cs`.

### CI (`.github/workflows/dotnet-desktop.yml`) - nowy job `snap`

- `needs: publish`, ten sam warunek `if` co `publish` (push /
  `workflow_dispatch`). Porażka snapa nie cofa release'u na GitHubie.
- Macierz: `amd64` na `ubuntu-latest` z artefaktem `YouTubeDownloader-linux-x64`;
  `arm64` na `ubuntu-24.04-arm` z artefaktem `YouTubeDownloader-linux-arm64`.
  Oba równolegle.
- Kroki:
  1. Checkout.
  2. `snap-bin/version.txt` = `<Version>` z `YouTubeDownloader.csproj`
     (odczytany `grep`/`sed`, bez dotnet) + `-${{ github.run_number }}`.
  3. `actions/download-artifact` do `snap-bin/`, `chmod +x`.
  4. `snapcore/action-build@v1` (id `build`) → `steps.build.outputs.snap`.
  5. Test dymny (bash, `env: YTD_GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}`):
     - `sudo snap install gnome-46-2404`, potem
       `sudo snap install --dangerous <snap>`;
     - `snap connections yt-downloader-bp` - `home` i `network` podłączone;
     - `apt-get install xvfb`, `xvfb-run --auto-servernum yt-downloader-bp`
       w tle, po 60 s proces nadal działa;
     - `~/snap/yt-downloader-bp/common/yt-dlp` i
       `~/snap/yt-downloader-bp/common/ffmpeg_bin/ffmpeg` istnieją i są
       wykonywalne; przy porażce wypisanie `app.log` i zawartości katalogów;
     - zakończenie procesu i `sudo snap remove --purge yt-downloader-bp`.
  6. `snapcore/action-publish@v1` z `env: SNAPCRAFT_STORE_CREDENTIALS`,
     `snap: ${{ steps.build.outputs.snap }}`, `release: stable`. Wykonywany
     tylko gdy sekret jest ustawiony (sekret mapowany na zmienną env joba i
     warunek `if: env.SNAPCRAFT_STORE_CREDENTIALS != ''`); bez sekretu krok
     jest pomijany, a osobny krok wypisuje ostrzeżenie `::warning::`.

### CLAUDE.md / README.md

- CLAUDE.md: w strukturze repo `snap/`; w „Runtime Structure” folder pobranych
  w snapie; opis `AppPaths.DownloadsDirectory` (sekcja „Change Download
  Location” - już nie dwa `Path.Combine`, tylko `AppPaths`); opis joba `snap`,
  sekretu `SNAPCRAFT_STORE_CREDENTIALS` (jak go odnowić:
  `snapcraft export-login --snaps=yt-downloader-bp
  --acls=package_access,package_push,package_update,package_release <plik>`,
  wygasa - domyślnie po roku) i rozszerzenie ostrzeżenia o
  `workflow_dispatch` (job publikuje też do Snap Store `stable`).
- README (PL i EN): instalacja `sudo snap install yt-downloader-bp`, gdzie
  snap zapisuje filmy, że aktualizuje go Snap Store; wpis w „Co nowego”.

## Obsługa błędów

- Nieczytelny/brakujący `user-dirs.dirs` → `realHome/Downloads` (bez
  komunikatu; folder powstaje przy pierwszym pobraniu).
- Brak uprawnień do zapisu w folderze pobranych (np. odłączony interfejs
  `home`) → istniejąca obsługa błędu pobierania (`MessageDialog` z treścią
  wyjątku); bez nowej logiki.
- CI: porażka budowy lub testu dymnego zatrzymuje job przed publikacją;
  błędne/wygasłe dane logowania → porażka kroku publikacji z komunikatem
  snapcrafta; release na GitHubie pozostaje nietknięty.

## Testowanie / weryfikacja

- Lokalnie (Windows): jednorazowa aplikacja konsolowa podpinająca `AppPaths.cs`
  sprawdza `ResolveUserDownloadDirectory`: `"$HOME/Pobrane"`, `"$HOME/Downloads"`,
  ścieżka bezwzględna, `"$HOME/"` (wyłączony), linia zakomentowana, kilka linii
  (ostatnia wygrywa), brak linii, `null`, wartość bez cudzysłowów, folder ze
  spacją. `dotnet build -c Release` bez ostrzeżeń.
- Poza snapem `DownloadsDirectory` = `<folder aplikacji>/downloads`
  (sprawdzone w tej samej aplikacji testowej - poza snapem `SNAP` nie jest
  ustawiony).
- CI: test dymny zainstalowanego snapa na amd64 i arm64 (confinement, pobranie
  i uruchomienie narzędzi, interfejsy).
- Ręcznie (właściciel, Ubuntu): `sudo snap install yt-downloader-bp`,
  pobranie jednego filmu → plik w `~/Pobrane/YouTube Downloader`; menu
  „Sprawdź aktualizacje aplikacji” → komunikat o Snap Store; okno
  „Informacje” otwiera linki.

## Kroki po stronie właściciela

- [x] Konto Ubuntu One / snapcraft.io, rejestracja nazwy `yt-downloader-bp`.
- [x] Sekret `SNAPCRAFT_STORE_CREDENTIALS` w repozytorium (2026-09-30).
- [ ] Po pierwszej publikacji: opcjonalnie zrzuty ekranu i kategorie w panelu
  snapcraft.io → Listing.
- [ ] Odnowienie sekretu przed wygaśnięciem danych logowania.

## Ryzyka / otwarte pytania

- **Pierwsza publikacja może trafić do ręcznej weryfikacji Store** (nowy snap,
  nazwa ze skrótem od znaku towarowego). Wtedy krok publikacji zgłosi status
  „review”, a wersja pojawi się w `stable` po akceptacji - bez zmian w kodzie.
- **Runner `ubuntu-24.04-arm` + LXD w `snapcore/action-build`** - nie
  sprawdzone w tym repo; jeśli LXD tam nie działa, zapasowo
  `snapcraft-args: --destructive-mode` (budowa bezpośrednio na runnerze
  24.04 = baza core24).
- **`snap install --dangerous` a content snap** - test instaluje
  `gnome-46-2404` jawnie przed aplikacją, żeby nie zależeć od
  automatycznego doinstalowania domyślnego dostawcy.
- **Rozmiar dla użytkownika** - content snap GNOME przy pierwszej instalacji.
- **Wygasanie danych logowania** - publikacja zacznie się nie udawać po
  terminie; opisane w CLAUDE.md.
- **Test dymny nie sprawdza zapisu do folderu pobranych** (brak automatyzacji
  UI) - pokrywa to test funkcji parsującej i ręczne pobranie filmu.
