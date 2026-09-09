# Migracja UI: Windows Forms → Avalonia UI (Faza 1: Windows-only)

**Data:** 2026-09-09
**Status:** Zaakceptowany, gotowy do planu implementacji

## Cel

Zastąpić warstwę UI aplikacji (obecnie Windows Forms, cały kod w `MainForm.cs`)
frameworkiem Avalonia UI, zachowując dokładnie tę samą funkcjonalność i
zachowanie co dziś. Aplikacja **nadal buduje się i działa tylko na Windows**
(x64 + ARM64, tak jak obecnie) — to jest fundament pod przyszłą, osobną fazę,
która doda realne wsparcie macOS/Linux.

## Kontekst

Aplikacja to jednoplikowa (obecnie) desktopowa appka WinForms (.NET 10,
`net10.0-windows`) do pobierania wideo/audio z YouTube przez yt-dlp, z
automatycznym zarządzaniem zależnościami (yt-dlp, FFmpeg, Deno). Cała logika
UI i biznesowa żyje w `MainForm.cs`.

Powód migracji: WinForms jest technologią wyłącznie dla Windows. Avalonia UI
to framework cross-platformowy (Windows/macOS/Linux + ARM64), który
docelowo umożliwi wsparcie innych systemów bez przepisywania logiki
biznesowej po raz drugi. Ta faza celowo **nie** dotyka logiki pobierania
zależności (która dziś jest zaszyta pod Windows — `.exe`, nazwy assetów
GitHub typu `win64-gpl-shared`, komenda `where`) — to osobny, następny etap.

## Zakres tej fazy

**W zakresie:**
- Wymiana WinForms → Avalonia UI dla całego istniejącego UI (główne okno +
  okno "Informacje").
- Zachowanie 1:1 obecnej logiki biznesowej (pobieranie/aktualizacja
  zależności, budowanie argumentów yt-dlp, uruchamianie procesu, parsowanie
  postępu, walidacja/normalizacja URL, multi-URL z przyciskami +/-).
- Retarget istniejącego `YouTubeDownloader.csproj` w miejscu.
- Aktualizacja `CLAUDE.md`, żeby odzwierciedlał nową architekturę.

**Poza zakresem (osobny, przyszły etap):**
- Realne wsparcie macOS/Linux: inne źródła binarek FFmpeg/yt-dlp/Deon dla
  tych systemów, zamiana komendy `where` na `which`, obsługa braku
  rozszerzenia `.exe`.
- CI na runnerach macOS/Linux.
- Jakikolwiek refaktor logiki biznesowej niezwiązany z samą wymianą UI
  (np. wprowadzanie warstwy Services/DI, MVVM) — YAGNI, nikt o to nie prosił.

## Decyzje projektowe (zatwierdzone)

1. **Zakres migracji:** tylko UI, appka pozostaje Windows-only w tej fazie.
2. **Styl UI Avalonia:** XAML (`.axaml`) + code-behind w C#, event handlery w
   stylu zbliżonym do obecnego (`Click="BtnDownload_Click"` →
   `private void BtnDownload_Click(object? sender, RoutedEventArgs e)`).
   Bez pełnego MVVM/ReactiveUI — nieuzasadniony narzut dla tej skali appki.
3. **Struktura projektu:** retarget w miejscu tego samego `.csproj` i repo
   (wariant A), nie równoległy projekt. Jedno źródło prawdy, brak
   duplikacji logiki biznesowej podczas migracji.

## Architektura / pliki

Zamiast jednego `MainForm.cs`, Avalonia naturalnie dzieli znacznik (`.axaml`)
od kodu (`.axaml.cs`) per okno. Nowa struktura plików w katalogu głównym
repo (zastępuje `MainForm.cs`):

- **`App.axaml` / `App.axaml.cs`** — bootstrap aplikacji: rejestracja motywu
  Fluent, uruchomienie `MainWindow` przy starcie (`OnFrameworkInitializationCompleted`).
- **`Program.cs`** — nowy entry point:
  ```csharp
  BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

  static AppBuilder BuildAvaloniaApp() =>
      AppBuilder.Configure<App>()
          .UsePlatformDetect()
          .LogToTrace();
  ```
  Zastępuje dzisiejszą klasę `Program` z `[STAThread] Main()`.
- **`MainWindow.axaml` / `MainWindow.axaml.cs`** — główne okno aplikacji.
  Znacznik: pasek URL + przycisk "+", pionowy `StackPanel` na dodatkowe
  wiersze URL (z przyciskiem "-" per wiersz), combo typ/jakość/format,
  przycisk Pobierz, pasek postępu, etykieta statusu, menu
  Narzędzia/Pomoc. Code-behind zawiera **całą dzisiejszą logikę biznesową
  przeniesioną bez zmian funkcjonalnych**:
  - `CheckAndDownloadComponents`, `IsRuntimeInPath`, `GetRuntimePath`
  - `DownloadDeno` / `GetLatestDenoInfo` / `CheckAndUpdateDeno`
  - `DownloadYtDlp`
  - `DownloadFFmpeg` / `GetLatestFFmpegInfo` / `CheckAndUpdateFFmpeg`
  - `DownloadFileWithProgress`
  - `NormalizeUrl` / `ValidateUrl`
  - `BuildYtDlpArguments`
  - `ParseDownloadProgress`
  - `BtnDownload_Click` / `DownloadSingleUrlAsync`
  - `GetAllUrls` / `SetUrlRowsEnabled` (obsługa multi-URL)
  - `AktualizujKomponenty_Click`
  - `OpenUrl`
  - pozostałe pomocnicze/eventowe bez zmian logiki: `UpdateStatus`,
    `ContentType_Changed`, `Informacje_Click` (otwiera `AboutWindow`
    zamiast dzisiejszego dynamicznie budowanego `Form`)

  Powyższa lista jest ilustracyjna, nie ścisłym kontraktem — pełne pokrycie
  1:1 względem obecnego `MainForm.cs` zostanie zweryfikowane w trakcie
  implementacji (każda publiczna/prywatna metoda biznesowa musi mieć swój
  odpowiednik po migracji, nic nie ma zniknąć po cichu).

  Multi-URL (dodawanie/usuwanie wierszy) upraszcza się względem obecnej
  implementacji: zamiast ręcznego przeliczania pikselowych pozycji
  wszystkich kontrolek poniżej (`RelayoutForm()` w dzisiejszym kodzie),
  wiersze URL trafiają do pionowego `StackPanel`, który sam się przepycha —
  dodanie/usunięcie wiersza to tylko dodanie/usunięcie elementu z jego
  `Children`, bez ręcznej matematyki współrzędnych. `BtnAddUrl_Click` i
  `RemoveUrlRow` (odpowiedniki dzisiejszych) zostają, ale bez wywołania
  `RelayoutForm()` — ono po prostu przestaje być potrzebne.

- **`AboutWindow.axaml` / `AboutWindow.axaml.cs`** — okno "Informacje"
  (logo, "Wersja 1.0.0", "Licencja: Apache License 2.0", nieklikalny
  prefiks "Repozytorium:"/"Autor:" + klikalny adres obok niego).

  Avalonia nie ma gotowego odpowiednika `LinkLabel` z klikalnym tylko
  fragmentem tekstu (`LinkArea`). Rozwiązanie: poziomy `StackPanel` z dwoma
  kontrolkami — zwykły `TextBlock` (prefiks, nieklikalny) + osobny
  klikalny `TextBlock` (`Cursor="Hand"`, `TextDecorations="Underline"`,
  wyróżniony kolor) z handlerem `PointerPressed` wywołującym `OpenUrl(...)`.
  `TextBlock` zamiast `Button` — bo `Button` ma domyślny padding/obramowanie,
  które trzeba by mocno przestylować, żeby wyglądał jak inline-owy link.
  `OpenUrl` bez zmian: `Process.Start` z `UseShellExecute = true`.

## Zależności NuGet

Pierwsza zmiana względem stanu "0 pakietów NuGet" (dziś projekt używa tylko
BCL). Dodawane pakiety:

- `Avalonia`
- `Avalonia.Desktop`
- `Avalonia.Themes.Fluent`
- `Avalonia.Fonts.Inter` (jeśli potrzebny fallback fontów — do potwierdzenia
  w trakcie implementacji, czy jest wymagany)

Wersje: najnowsze stabilne w momencie implementacji (do sprawdzenia w
NuGet.org / GitHub releases Avalonia przy pisaniu planu/implementacji).

To odnotowanie jest istotne, bo od teraz te pakiety trzeba też uwzględniać
przy przyszłych audytach CVE (jak audyt wykonany wcześniej w tym repo, gdy
projekt nie miał żadnych zależności NuGet).

## Ikona / logo

- `app.ico` zostaje jako ikona okna — Avalonia `WindowIcon` obsługuje
  `.ico` cross-platformowo (wczytywane ze strumienia, podobnie jak dziś
  przez embedded resource).
- Do okna "Informacje" (`PictureBox` → `Image` w Avalonia) potrzebny jest
  `Bitmap`. Zamiast `System.Drawing.Icon.ToBitmap()` (Windows-only, GDI+ —
  niepożądane w kodzie, który ma być gotowy pod przyszłą fazę
  cross-platform), jednorazowo wyeksportuję PNG z istniejącej ikony i osadzę
  go jako `AvaloniaResource`, wczytywany przez `new Bitmap(assetStream)`.

## csproj

- `TargetFramework`: `net10.0-windows` → `net10.0`.
- Usunięcie `<UseWindowsForms>true</UseWindowsForms>`.
- `<OutputType>WinExe</OutputType>` i `<ApplicationIcon>app.ico</ApplicationIcon>`
  zostają bez zmian (nadal honorowane przy publikacji self-contained na
  Windows).
- Dodanie referencji do pakietów Avalonia wymienionych wyżej.
- `<Nullable>enable</Nullable>` i `<ImplicitUsings>enable</ImplicitUsings>`
  zostają.

## CI / build.bat

Bez zmian strukturalnych:
- `.github/workflows/dotnet-desktop.yml` nadal `runs-on: windows-latest`,
  nadal macierz `rid: [win-x64, win-arm64]` w jobie `publish`. Jedyna
  różnica: `dotnet publish` zbuduje teraz appkę Avalonia zamiast WinForms —
  RID-y, self-contained, single-file zostają identyczne.
- `build.bat` bez zmian (parametr RID, walidacja, wywołanie
  `dotnet publish`).

## Obsługa błędów

Bez zmian względem obecnego zachowania: `try/catch` wokół operacji
zewnętrznych (pobieranie plików, uruchamianie procesów), aktualizacje
etykiety statusu. Avalonia nie ma wbudowanego `MessageBox` — zamiast
dodawać kolejną zależność NuGet (np. `MessageBox.Avalonia`) tylko po to,
zbuduję proste własne modalne okno dialogowe (w stylu `AboutWindow`:
tekst + ikona + przycisk OK, ewentualnie Yes/No dla potwierdzeń typu
"Zaktualizować komponenty?") i użyję go wszędzie tam, gdzie dziś jest
`MessageBox.Show(...)`.

## Testowanie / weryfikacja

Jak w dotychczasowych zmianach w tym repo: weryfikacja przez kompilację +
smoke-test uruchomienia (proces żyje po starcie, brak wyjątku). Brak
narzędzi do automatyzacji natywnego GUI Windows/Avalonia w tym środowisku —
**ręczna weryfikacja UI (multi-URL, okno Informacje, pełny przebieg
pobierania) będzie potrzebna po stronie użytkownika** po zakończeniu
migracji.

## Dokumentacja

`CLAUDE.md` wymaga aktualizacji obejmującej:
- Sekcję "Codebase Architecture" (dziś opisuje `MainForm.cs` z numerami
  linii — do przepisania pod nową strukturę wieloplikową).
- "Repository Structure" (nowe pliki `.axaml`/`.axaml.cs`, `Program.cs`).
- "Key Conventions" → konwencje Avalonia zamiast WinForms
  (`InitializeComponent()` generowany z XAML zamiast ręcznie pisany,
  event handlery nadal `ControlName_EventType`).
- "NuGet Dependencies: None" → lista pakietów Avalonia.
- Sekcję "AI Assistant Guidelines" tam, gdzie odnosi się do
  `InitializeComponent()`/WinForms-specific patterns.

Aktualizacja dokumentacji jest częścią planu implementacji, nie czymś do
domykania na etapie tego spec-a.

## Ryzyka / otwarte pytania do etapu implementacji

- Dokładna wersja pakietów Avalonia do przypięcia (sprawdzić najnowsze
  stabilne w momencie implementacji).
- Czy `Avalonia.Fonts.Inter` jest faktycznie potrzebny, czy domyślne fonty
  systemowe Windows wystarczą (Segoe UI już używane w oknie "Informacje").
- Ewentualne różnice w zachowaniu `Process`/`HttpClient` między WinForms a
  Avalonia — oczekiwane: brak, to czysty BCL, niezależny od frameworku UI.
