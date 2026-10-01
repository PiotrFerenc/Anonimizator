# Anonimizator – plan implementacji

Specyfikacja: `opis.md`. Ten plik opisuje, jak ją zrealizować. Kod i nazwy w angielskim, dokumentacja po polsku.

## 1. Założenia

- .NET 10, C#, solution `Anonymizer.slnx` (format `.slnx`, jak w pozostałych projektach workspace).
- Trzy projekty: `Anonymizer.Core` (biblioteka), `Anonymizer.Tests` (xunit), `Anonymizer.Cli` (konsola).
- Zero zewnętrznych zależności w `Core` poza `Microsoft.Extensions.DependencyInjection.Abstractions`. W CLI `Microsoft.Extensions.DependencyInjection`. Serializacja JSON przez `System.Text.Json`.
- Wydajność: jeden przebieg po tekście, wektoryzowane szukanie kandydatów (`SearchValues<char>`), praca na `ReadOnlySpan<char>`, alokacje tylko na wynik i listę dopasowań.

## 2. Struktura

```
Anonimizator/
  Anonymizer.slnx
  Anonymizer.Core/
    IAnonymizer.cs
    AnonymizationService.cs        (IAnonymizationService + implementacja)
    AnonymizationResult.cs         (rekordy wyniku)
    ReplacementMap.cs
    LinuxPathAnonymizer.cs
    WindowsPathAnonymizer.cs
    ServiceCollectionExtensions.cs (AddAnonymizer)
  Anonymizer.Tests/
    LinuxPathAnonymizerTests.cs
    WindowsPathAnonymizerTests.cs
    AnonymizationServiceTests.cs
    ReplacementMapTests.cs
  Anonymizer.Cli/
    Program.cs
  Anonymizer.Bench/
    Program.cs                     (benchmark samego serwisu: `dotnet run -c Release --project Anonymizer.Bench`)
```

## 3. Kontrakty

### Wynik (`AnonymizationResult.cs`)

```csharp
public sealed record AnonymizedItem(string Original, string Anonymized, string Anonymizer);

public sealed record AnonymizationResult(
    string Original,
    string AnonymizedDocument,
    IReadOnlyList<string> Anonymizers,
    IReadOnlyList<AnonymizedItem> Anonymized);
```

Nazwy JSON (`original`, `anonymized_document`, `anonymizers`, `anonymized`, `anonymizer`) dają `JsonNamingPolicy.SnakeCaseLower`. Nie trzeba atrybutów na właściwościach.

`Anonymizers` zawiera nazwy anonimizatorów, które coś zmieniły, bez powtórzeń, w kolejności pierwszego użycia.

### Anonimizator (`IAnonymizer.cs`)

```csharp
public interface IAnonymizer
{
    string Name { get; }                       // "linux_path", "windows_path"
    string Triggers { get; }                   // znaki, na których może się zacząć wykrywanie, np. "/"

    // text[triggerIndex] jest jednym z Triggers. Zwraca true, gdy znalazł ścieżkę:
    // [Start, Start+Length) to cała ścieżka, [DirStart, DirStart+DirLength) to część
    // katalogowa do zamiany (pozycje bezwzględne w text).
    bool TryMatch(ReadOnlySpan<char> text, int triggerIndex, int cursor, out PathMatch match);

    string Replace(in PathMatch match, ReadOnlySpan<char> text, ReplacementMap map);
}

public readonly record struct PathMatch(int Start, int Length, int DirStart, int DirLength);
```

- `cursor` to koniec poprzedniego dopasowania. Anonimizator nie może zwrócić `Start < cursor` (windows cofa się o jedną literę dysku).
- `DirStart`/`DirLength` wskazują część katalogową do zamiany (patrz 4.1, 4.2). Reszta ścieżki zostaje.
- `Replace` składa zanonimizowaną ścieżkę: prefiks + zamiennik z `map` + plik.

### Serwis

```csharp
public interface IAnonymizationService
{
    AnonymizationResult Anonymize(string text);
}
```

### Mapa zamienników (`ReplacementMap.cs`)

- Tworzona na jedno wywołanie `Anonymize`. Nie jest współdzielona między wątkami ani wywołaniami.
- `GetOrCreate`: jeśli klucz już jest, zwraca ten sam zamiennik, jeśli nie, losuje.
- Zamiennik: 4 losowe litery `a-z` (`Random.Next(26)`). `HashSet<string>` użytych zamienników. Kolizja z innym katalogiem daje ponowne losowanie.
- Zabezpieczenie przed wyczerpaniem przestrzeni (26^4 = 456 976): po 8 nieudanych próbach z rzędu długość zamiennika rośnie o 1.
- Konstruktor przyjmuje `Random`. W produkcji `Random.Shared` (bezpieczny wątkowo), w testach `new Random(seed)`.
- Klucz to cała część katalogowa łącznie z prefiksem (np. `/etc/projekty/`, `C:\Users\Jan\`). Linux porównywany `Ordinal`, Windows `OrdinalIgnoreCase`, więc mapa ma osobny słownik na anonimizator (`GetOrCreate(string anonymizerName, ReadOnlySpan<char> key, bool ignoreCase)`, wyszukiwanie przez `GetAlternateLookup<ReadOnlySpan<char>>`, więc trafienie nie alokuje), a zbiór użytych zamienników wspólny.

## 4. Anonimizatory

### 4.1 `linux_path`

- Trigger: `/`.
- Granica początku: `/` stoi na początku tekstu, po białym znaku albo po jednym ze znaków `" ' ( = [ , <`. Dzięki temu nie łapiemy `https://host/a/b.html` (poprzedza `:`), `and/or`, `1/2/2024`, `a//b`.
- Koniec ścieżki: pierwszy biały znak albo jeden ze znaków `" ' < > | * ? ` ` ` lub koniec tekstu. Potem odcinamy końcowe znaki interpunkcyjne `. , ; : ) ] '` `"`, żeby `"zobacz /etc/a/b.cs."` dało ścieżkę bez kropki.
- Ścieżka jest ważna, gdy ma co najmniej dwa `/`, a po ostatnim `/` jest niepusta nazwa pliku. `/etc` i `/etc/projekty/` są pomijane (brak pliku).
- Część do zamiany: od pierwszego `/` do ostatniego `/` włącznie (`/etc/projekty/`). Zamiennik: `/` + token + `/`. Wynik: `/wdsr/main.cs`.
- Katalog w jednym segmencie też jest zamieniany: `/bin/controller.cs` na `/rrfd/controller.cs`.

### 4.2 `windows_path`

- Trigger: `:` (stoi na pozycji `triggerIndex`, litera dysku na `triggerIndex-1`).
- Warunek: `text[i-1]` to litera ASCII, przed nią początek tekstu albo znak spoza `[A-Za-z0-9]`, a po `:` stoi `\`.
- Koniec ścieżki: jak w Linuxie (znaki zabronione w Windows: `" < > | * ?`, biały znak, interpunkcja końcowa). Biały znak kończy ścieżkę (spacje w ścieżkach poza zakresem).
- Ważna, gdy między `X:\` a ostatnim `\` jest co najmniej jeden niepusty segment i jest niepusta nazwa pliku. `C:\file.txt` jest pomijane.
- Część do zamiany: od znaku po `X:\` do ostatniego `\` włącznie. Wynik: `C:\abcd\file.txt`. Litera dysku zostaje.
- Separator `/` w ścieżkach windowsowych (`C:/Users/...`) poza zakresem.

## 5. Serwis – algorytm

`AnonymizationService(IEnumerable<IAnonymizer> anonymizers, Random random)` w konstruktorze:

1. Zbiera `Triggers` (napisy ze znakami startowymi) wszystkich anonimizatorów w jedno `SearchValues<char>` oraz osobne `SearchValues<char>` na anonimizator.
2. Zapamiętuje anonimizatory w tablicy (bez LINQ na gorącej ścieżce).

`Anonymize(string text)`:

1. `null` rzuca `ArgumentNullException`. Pusty tekst zwraca wynik z pustymi listami.
2. `map = new ReplacementMap(random)`, `items = new List<AnonymizedItem>()`, `sb = new StringBuilder(text.Length)`, `cursor = 0` (początek tekstu jeszcze niedopisanego do `sb`), `scan = 0` (miejsce, od którego szukamy kolejnego znaku startowego).
3. Pętla:
   - `i = scan + span[scan..].IndexOfAny(allTriggers)` (wynik względny, dodać `scan`). Brak trafienia kończy pętlę.
   - Dla każdego anonimizatora, którego `Triggers` zawiera `span[i]`, wywołaj `TryMatch(span, i, cursor, out m)`. Z trafień wybierz najwcześniejszy `Start`, przy remisie dłuższy `Length`.
   - Brak trafienia: `scan = i + 1`, `cursor` bez zmian (tekst między dopasowaniami jest dopisywany dopiero przy następnym trafieniu lub na końcu), następna iteracja.
   - Trafienie: dopisz do `sb` tekst od `cursor` do `m.Start`, potem `anonymizer.Replace(...)`, dodaj `AnonymizedItem(originalSubstring, replaced, anonymizer.Name)`, ustaw `cursor = scan = m.Start + m.Length`.
4. Dopisz resztę tekstu od `cursor`. Zbuduj `AnonymizationResult`. `Anonymizers` to nazwy z `items` bez powtórzeń.
5. Brak dopasowań: `AnonymizedDocument` to ten sam obiekt `string` co `Original` (bez kopiowania).

Dopasowania z natury się nie nakładają, bo każde zaczyna się nie wcześniej niż `cursor`.

## 6. DI i wzorce

- `AddAnonymizer(this IServiceCollection)`: rejestruje `IAnonymizer` (`LinuxPathAnonymizer`, `WindowsPathAnonymizer`) jako singletony, `Random.Shared`, `IAnonymizationService` jako singleton. Serwis jest bezstanowy, stan jest w `ReplacementMap` na wywołanie.
- Wzorce: Strategy (`IAnonymizer`), kompozycja strategii w serwisie (jedna pętla, wiele anonimizatorów), wstrzykiwanie zależności. Nowy anonimizator to nowa klasa plus jedna linia rejestracji, bez zmian w serwisie.

### Dodawanie nowych anonimizatorów

Wymóg: lista anonimizatorów jest otwarta, także dla kodu spoza `Anonymizer.Core`.

- Serwis dostaje `IEnumerable<IAnonymizer>` z DI, więc zna tylko interfejs. Wszystkie zarejestrowane implementacje są używane automatycznie.
- Rejestracja: `services.AddSingleton<IAnonymizer, MyAnonymizer>()`. Dla wygody w `Core` dodajemy `AddAnonymizer<T>() where T : class, IAnonymizer` (opakowanie tej jednej linii).
- Serwis buduje zbiór `Triggers` w konstruktorze z tego, co jest w DI, więc nowy anonimizator nie wymaga zmian w pętli.
- Kontrakt dla autora anonimizatora (opisać w XML-doc na `IAnonymizer`):
  - `Name` jest unikalne (małe litery, `snake_case`). Serwis przy starcie rzuca `InvalidOperationException` na duplikat.
  - `TryMatch` nie alokuje i nie zwraca `Start < cursor`.
  - `Replace` używa `ReplacementMap` dla spójności w dokumencie albo własnej logiki, jeśli zamiennik nie jest losowym tokenem.
- Test: serwis z dodatkowym testowym anonimizatorem (np. stały ciąg `SECRET` na `***`) trafia do wyniku pod własną nazwą, a duplikat nazwy rzuca wyjątek.
- Poza zakresem: ładowanie wtyczek z plików DLL w runtime i konfiguracja z pliku. Zewnętrzna biblioteka rejestruje się w DI sama.

## 7. CLI

`Program.cs`:

1. `ServiceCollection` + `AddAnonymizer()`, `BuildServiceProvider()`.
2. Czyta cały stdin (`Console.In.ReadToEnd()`).
3. Wywołuje `IAnonymizationService.Anonymize`.
4. Wypisuje JSON na stdout (`WriteIndented = true`, `SnakeCaseLower`, `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, żeby polskie znaki i `\` nie były zamieniane na `\uXXXX`).
5. Pusty stdin: wynik z pustymi listami, kod 0. Wyjątek: komunikat na stderr, kod 1.

Przykład: `cat plik.txt | dotnet run --project Anonymizer.Cli`.

## 8. Testy (xunit)

Losowość: testy używają `new Random(seed)` i sprawdzają strukturę, nie konkretne litery.

`AnonymizationServiceTests`:
- Przykład z `opis.md`: 3 dopasowania, `Anonymizers == ["linux_path"]`, wynik pasuje do wzorca `/[a-z]{4}/main.cs`, `/[a-z]{4}/test.cs`, `/[a-z]{4}/controller.cs`.
- Ten sam katalog daje ten sam zamiennik (`main.cs` i `test.cs`), różne katalogi dają różne.
- `Original` identyczny z wejściem, znaki nowej linii zachowane w `AnonymizedDocument`.
- Tekst poza ścieżkami bez zmian bajt w bajt.
- `items[i].Original` to dokładny podciąg wejścia, `items[i].Anonymized` występuje w dokumencie wynikowym.
- Pusty tekst, tekst bez ścieżek (zwraca to samo `string`), `null`.
- Linux i Windows w jednym tekście: oba nazwy w `Anonymizers`, brak powtórzeń zamienników między nimi.
- Dodatkowy testowy anonimizator zarejestrowany w DI jest używany bez zmian w serwisie, a duplikat `Name` rzuca `InvalidOperationException`.
- Wymuszona kolizja: mapa z zajętym zamiennikiem losuje ponownie (test `ReplacementMap` z deterministycznym `Random` lub podmienioną przestrzenią).

`LinuxPathAnonymizerTests` (przypadki wejście → czy i co dopasowano):
- `/etc/projekty/main.cs` tak, `/bin/controller.cs` tak, `"/a/b.cs"` w cudzysłowie tak, `(/a/b.cs)` tak.
- `/etc`, `/etc/projekty/` (bez pliku) nie.
- `https://host/a/b.html`, `and/or`, `1/2/2024`, `a//b.cs` nie.
- Końcowa kropka/przecinek nie wchodzą do ścieżki.

`WindowsPathAnonymizerTests`:
- `C:\Users\Jan\a.txt` daje `C:\xxxx\a.txt`, `c:\x\y.cs` (mała litera) tak.
- `C:\a.txt` nie, `file:C:\...`/`abC:\x\y` (litera dysku nie na granicy) nie.
- Ścieżka kończona cudzysłowem i interpunkcją.

`ReplacementMapTests`:
- Powtórne `GetOrCreate` zwraca to samo, unikalność zamienników, długość 4, tylko `a-z`, wzrost długości po wyczerpaniu.

## 9. Wydajność

- Brak regexów, LINQ i `Substring` na gorącej ścieżce. `Substring` tylko dla dopasowań (potrzebne do wyniku).
- `IndexOfAny(SearchValues<char>)` jest wektoryzowane, więc tekst bez ścieżek jest przeglądany szybko.
- `StringBuilder` z pojemnością `text.Length`. Bez dopasowań nic nie alokujemy poza listą (leniwie tworzona przy pierwszym trafieniu).
- Weryfikacja ręczna po implementacji: wygenerować plik ~50–100 MB (powtórzony fragment z przykładu plus zwykły tekst), uruchomić przez CLI z `time`. Cel: przepustowość rzędu setek MB/s dla tekstu rzadko zawierającego ścieżki oraz brak nieliniowego wzrostu czasu. BenchmarkDotNet tylko jeśli wynik będzie podejrzany.
- Świadome ograniczenie: lista wyników i wynikowy `string` są w pamięci w całości (wejście i wyjście to obiekt, nie strumień). Strumieniowanie wykracza poza `opis.md`.

## 10. Kolejność prac

1. Sprawdzić środowisko: `dotnet --list-sdks` (potrzebny .NET 10), `df -h /` (dysk na tym hoście się zapełnia).
2. `dotnet new sln -n Anonymizer --format slnx`, projekty: `classlib` (Core), `xunit` (Tests), `console` (Cli). Referencje: Tests i Cli wskazują Core. Pakiety DI.
3. `AnonymizationResult`, `PathMatch`, `IAnonymizer`, `ReplacementMap` plus testy mapy.
4. `LinuxPathAnonymizer` plus jego testy.
5. `AnonymizationService` plus `AddAnonymizer`, test przykładu z `opis.md` (zielony po tym kroku dla samego Linuxa).
6. `WindowsPathAnonymizer` plus testy, testy mieszane.
7. CLI, ręczny test z przykładem.
8. Test wydajności z punktu 9.
9. `dotnet build` i `dotnet test` bez ostrzeżeń.

## 11. Poza zakresem

- Ścieżki ze spacjami, ścieżki względne (`./a/b.cs`, `../x`), ścieżki UNC (`\\host\share`), `~/`, adresy URL.
- Powtarzalność wyniku między wywołaniami (seed), strumieniowanie, API HTTP.
- Konkretne dodatkowe anonimizatory (e-mail, IP itd.). Mechanizm dodawania jest w planie (sekcja 6), same anonimizatory nie.

## 12. Zmiany względem `opis.md`

Reguły doprecyzowane w planie i potwierdzone, opisane też w `opis.md` (sekcja „Wykrywanie”): granice początku ścieżki (4.1), pomijanie ścieżek bez pliku, odcinanie końcowej interpunkcji, brak wykrywania ścieżek zaczynających się po `:` (np. `path:/etc/a.cs`).

## 13. Stan realizacji

Wszystkie kroki z sekcji 10 wykonane. Wyniki `Anonymizer.Bench` (50 mln znaków): bez ścieżek 11 ms, rzadkie ścieżki 29 ms, 1 mln dopasowań (35 mln znaków) 430 ms. Alokacje na dopasowanie ograniczone do: podciąg oryginału, zamiennik, `AnonymizedItem`.
