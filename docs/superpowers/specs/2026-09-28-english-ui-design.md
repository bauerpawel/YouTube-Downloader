# Interfejs w języku angielskim (PL/EN)

**Data:** 2026-09-28
**Status:** Zaakceptowany, gotowy do planu implementacji

## Cel

Aplikacja obsługuje dwa języki interfejsu: polski i angielski. Język wybierany
jest automatycznie przy pierwszym uruchomieniu (polski system → polski, każdy
inny → angielski), można go zmienić w menu, a zmiana działa natychmiast i jest
zapamiętywana.

## Kontekst

- Aplikacja jest publiczna na GitHubie, w planach jest Snap Store - interfejs
  wyłącznie po polsku ogranicza odbiorców.
- Wszystkie teksty są dziś wpisane na sztywno: w trzech plikach XAML (menu,
  etykiety, przyciski, tytuły okien) i w `MainWindow.axaml.cs` (ok. 60
  komunikatów statusu, okien i błędów, pozycje list). Polskie teksty są
  pisane bez polskich znaków (pozostałość po WinForms).
- Po poprzednim etapie (katalog danych + samoaktualizacja) wersja angielska
  dotrze do użytkowników automatycznie.

## Ustalenia techniczne

**Logika zależna od wyświetlanego tekstu.** `BuildYtDlpArguments()`
(`MainWindow.axaml.cs:823-861`) rozpoznaje jakość po napisie: porównuje z
`"Najlepsza"` i `"4K (2160p)"`, a dla pozostałych wycina „p” z tekstu
(`quality.Replace("p", "")`). Po przetłumaczeniu „Najlepsza” na „Best”
powstałby format `bestvideo[height<=Best]` - pobieranie w wersji angielskiej by
nie działało. Wybór jakości musi przejść na wartości niezależne od języka.
Typ zawartości już działa po indeksie (`SelectedIndex == 1`); formaty
(`mp4`/`webm`/`mkv`) nie są tłumaczone.

## Zakres

**W zakresie:**
- Mechanizm tłumaczeń (`Strings.cs`) z pełnym polskim i angielskim tekstem
  całego interfejsu.
- Wybór języka: automatycznie przy braku zapisu, ręcznie w menu, zapis w
  katalogu danych (`LanguageSettings.cs`).
- Przełączanie na żywo w oknie głównym.
- Poprawa polskich tekstów: polskie znaki (`Błąd`, `Narzędzia`, `Jakość`,
  `Sprawdź aktualizacje aplikacji`…).
- Wybór jakości i budowanie argumentów yt-dlp niezależne od języka
  (`YtDlpArguments.cs`), z testami przypinającymi obecne argumenty.
- CLAUDE.md, README, `<Version>`.

**Poza zakresem:**
- Kolejne języki (mechanizm je umożliwia - jedna nowa instancja `Strings`).
- Tłumaczenie wyjścia yt-dlp (jest po angielsku), treści wyjątków systemowych
  (`ex.Message` - w języku systemu), domyślnego tekstu pola URL
  (`https://www.youtube.com/watch?v=`).
- Lokalne formaty liczb/dat.
- Przetłumaczenie bieżącej linii statusu przy przełączeniu języka - zostaje w
  starym języku do następnej zmiany statusu.
- Snap.

## Architektura / zmiany w plikach

### Nowy `Strings.cs` (bez zależności od Avalonii)

```csharp
internal sealed class Strings
{
    public required string MenuTools { get; init; }            // "_Narzędzia" / "_Tools"
    public required string ButtonDownload { get; init; }       // "Pobierz" / "Download"
    // ... jedna właściwość required na każdy tekst ...
    public required Func<int, int, string> StatusDownloadedPartial { get; init; }
    // ... teksty z parametrami jako funkcje - kompilator pilnuje argumentów ...

    public static Strings Polish { get; } = new() { ... };
    public static Strings English { get; } = new() { ... };

    public static Strings Current { get; set; } = Polish;

    public static Strings For(string languageCode);   // "pl" -> Polish, wszystko inne -> English
}
```

- Pominięcie tekstu w którejkolwiek instancji to **błąd kompilacji**
  (`required`).
- Nazwy właściwości po angielsku, z przedrostkiem obszaru: `Menu…`, `Label…`,
  `Button…`, `Status…`, `Error…`, `Title…`, `Quality…`, `ContentType…`.
- Klawisze skrótu menu (`_`) są częścią tekstu, osobno dla każdego języka.
- Nazwy języków w menu zawsze w ich własnym języku („Polski”, „English”) -
  wspólne dla obu instancji.

### Nowy `LanguageSettings.cs` (wzorem `ThemeSettings.cs`)

- `Load(directory)` / `Save(directory, code)` - plik `language.txt` w
  katalogu danych; błędy odczytu/zapisu pomijane jak w `ThemeSettings`.
- `Resolve(string? saved, CultureInfo systemCulture) : string` - `"pl"` lub
  `"en"`: poprawny zapisany wybór wygrywa; bez niego (lub gdy plik zawiera coś
  innego niż `pl`/`en`) kultura systemu z językiem `pl` daje `"pl"`, każda
  inna `"en"`.

### Nowy `YtDlpArguments.cs` (bez zależności od Avalonii)

- `Build(bool audioOnly, int? maxHeight, string format) : string` - dokładnie
  te same argumenty co dzisiejsze `BuildYtDlpArguments()` dla każdej
  kombinacji (7 jakości × 3 formaty + tryb audio).
- Tablica wartości jakości: `null` (najlepsza), 2160, 1080, 720, 480, 360,
  240 - kolejność zgodna z pozycjami listy.
- `MainWindow.BuildYtDlpArguments()` sprowadza się do wywołania `Build()` z
  `SelectedIndex` przełożonym na `maxHeight`.

### `App.axaml.cs`

Przed utworzeniem `MainWindow`, obok motywu:
`Strings.Current = Strings.For(LanguageSettings.Resolve(LanguageSettings.Load(AppPaths.DataDirectory), CultureInfo.CurrentUICulture))`.

### `MainWindow.axaml` / `MainWindow.axaml.cs`

- Z XAML znikają polskie napisy (pozostaje `Title="YouTube Downloader"`,
  domyślny tekst pola URL, `+` i znaczniki bez tekstu).
- Etykiety bez nazw dostają `Name`: `LblUrl`, `LblContentType`,
  `LblQuality`, `LblFormat`; menu nadrzędne: `MiTools`, `MiView`, `MiHelp`.
- Nowe menu: Widok → `MiLanguage` („_Język” / „_Language”) → `MiLangPolish`
  („Polski”), `MiLangEnglish` („English”), przełączniki radiowe (jak motyw),
  oddzielone separatorem od opcji motywu.
- `ApplyTexts()` - ustawia wszystkie teksty okna z `Strings.Current`: menu,
  etykiety, przycisk „Pobierz”, podpowiedź we wszystkich wierszach URL
  (także już dodanych), odbudowuje listy typu i jakości z zachowaniem
  `SelectedIndex`. Wołana w konstruktorze i po zmianie języka.
- `SetLanguage(code)`: `Strings.Current = Strings.For(code)`,
  `LanguageSettings.Save(dataDirectory, code)`, `ApplyTexts()`, zaznaczenie
  przełączników. Obsługa zdarzeń w konstruktorze (konwencja projektu).
- Każdy komunikat statusu, okna i błędu w kodzie korzysta z
  `Strings.Current`.

### `AboutWindow` i `MessageDialog`

- Teksty ustawiane w konstruktorze z `Strings.Current` (tworzone przy każdym
  otwarciu, więc zawsze w bieżącym języku). Etykiety w `AboutWindow.axaml`
  dostają `Name`.
- `MessageDialog`: przyciski „OK” / „Tak”·„Nie” ↔ „OK” / „Yes”·„No”.

### Bez zmian

Przedrostki `[status]`/`[dialog]` w logu na standardowe wyjście (treść
tłumaczona, przedrostki nie), logika pobierania i aktualizacji, CI.

## Obsługa błędów

- Nieczytelny lub nieznany `language.txt` → jak brak zapisu (język systemu).
- Błąd zapisu `language.txt` → pominięty (jak motyw); język działa do końca
  sesji.

## Testowanie / weryfikacja

Program testowy poza repozytorium (scratchpad), kompilujący pliki bez Avalonii
przez `<Compile Include>` - jak w poprzednim etapie:
- **Kompletność tekstów:** każda właściwość `string` w obu instancjach
  niepusta; każda funkcja wywołana z przykładowymi argumentami zwraca
  niepusty tekst zawierający te argumenty.
- **`LanguageSettings`:** zapisany `pl`/`en` wygrywa; brak zapisu + `pl-PL` →
  `pl`; brak zapisu + `en-US`/`de-DE`/`InvariantCulture` → `en`; śmieci w
  pliku → jak brak zapisu; zapis i odczyt dają ten sam kod.
- **`YtDlpArguments.Build`:** oczekiwane argumenty zapisane z **obecnego**
  kodu (przed zmianą) dla wszystkich kombinacji; po zmianie identyczne.
- `dotnet build` bez ostrzeżeń (Debug i Release).

Test ręczny (Windows, użytkownik):
- przełączenie na English i z powrotem: menu, etykiety, listy zmieniają się od
  razu, wybrana jakość/format zostają;
- okna błędów i „Informacje” w nowym języku;
- po ponownym uruchomieniu język zostaje;
- prawdziwe pobieranie w wersji angielskiej: „Best” i np. 720p;
- polskie znaki wyświetlają się poprawnie, nic się nie ucina.

CI bez zmian (testy dymne sprawdzają start i pobranie narzędzi).

## Dokumentacja i wersja

- CLAUDE.md: konwencja „Polish UI Text” → „teksty interfejsu wyłącznie w
  `Strings.cs`, zawsze w obu językach”; nowe pliki w strukturze repozytorium;
  sekcje „Update UI Text/Language” i „Change Quality Options” zaktualizowane
  (jakość = tekst w `Strings` + wartość w `YtDlpArguments`).
- README: interfejs PL/EN w opisie i funkcjach, „Co nowego”.
- `<Version>`: data dnia wdrożenia (`2.0.ddMMyy`); jeśli nadal 280926 -
  dopisanie do sekcji „Co nowego w wersji 2.0.280926”.

## Ryzyka / otwarte pytania

- **Długość tekstów:** kolumny etykiet po 150 px, minimalna szerokość okna
  800 px. Wszystko powinno się zmieścić, potwierdzi test ręczny.
- **Czcionki:** polskie znaki - FluentTheme/systemowe czcionki Windows i
  macOS je mają, na Linuksie CI instaluje DejaVu (pełne pokrycie). Wygląd
  sprawdzany tylko ręcznie na Windows.
