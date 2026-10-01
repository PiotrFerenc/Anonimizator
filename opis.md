# Anonimizator

## Technologia
- dotnet
- wzorce projektowe
- dependency injection

## Zasady
- wejście jest w czystym tekście
- wyjście jest obiektem
- rozwiązanie musi być bardzo wydajne
- anonimizowana jest część katalogowa ścieżki, nazwa pliku zostaje bez zmian
- cała część katalogowa (np. `/etc/projekty/`) jest zastępowana jednym zamiennikiem (`/wdsr/`), głębokość ścieżki nie jest zachowywana
- zamiennik to losowy ciąg 4 małych liter; wynik nie jest deterministyczny między wywołaniami
- w obrębie jednego dokumentu ta sama część katalogowa zawsze dostaje ten sam zamiennik (mapa w pamięci, tworzona na czas jednego wywołania)
- zamienniki w obrębie dokumentu nie mogą się powtarzać dla różnych katalogów (przy kolizji losujemy ponownie)

## Wykrywanie
- ręczny skaner znaków oparty o `ReadOnlySpan<char>`, bez regexów i bez zbędnych alokacji
- jeden przebieg po tekście; każdy anonimizator zgłasza dopasowania (pozycja, długość), a wyniki są scalane po pozycji
- ścieżka nie zawiera białych znaków (spacja, tab, nowa linia kończą ścieżkę)
- ścieżka linuxowa zaczyna się od `/`, windowsowa od `X:\` (litera dysku)
- przy nakładających się dopasowaniach wygrywa to, które zaczyna się wcześniej; przy tej samej pozycji dłuższe

## Architektura
- `Anonymizer.Core` – biblioteka: interfejs `IAnonymizer`, serwis anonimizujący, implementacje `linux_path` i `windows_path`, rejestracja w DI przez `AddAnonymizer()`
- `Anonymizer.Tests` – testy xunit (przykład z opisu jako test wzorcowy)
- `Anonymizer.Cli` – aplikacja konsolowa: czyta tekst ze stdin, wypisuje JSON na stdout
- nowy anonimizator = nowa klasa implementująca `IAnonymizer`, zarejestrowana w DI, bez zmian w serwisie

## Wejście
- tekst

## Wyjście
- oryginalny tekst
- tekst zanonimizowany
- lista użytych anonimizatorów
- lista zanonimizowanych miejsc:
  - original
  - anonymized
  - anonymizer

## Przykład

Wejście:
```
To jest ścieżka: /etc/projekty/main.cs
To jest też ścieżka: /etc/projekty/test.cs
ścieżka z innego miejsca /bin/controller.cs
```

Wyjście:
```json
{
  "original": "To jest ścieżka: /etc/projekty/main.cs\nTo jest też ścieżka: /etc/projekty/test.cs\nścieżka z innego miejsca /bin/controller.cs",
  "anonymized_document": "To jest ścieżka: /wdsr/main.cs\nTo jest też ścieżka: /wdsr/test.cs\nścieżka z innego miejsca /rrfd/controller.cs",
  "anonymizers": ["linux_path"],
  "anonymized": [
    {
      "original": "/etc/projekty/main.cs",
      "anonymized": "/wdsr/main.cs",
      "anonymizer": "linux_path"
    },
    {
      "original": "/etc/projekty/test.cs",
      "anonymized": "/wdsr/test.cs",
      "anonymizer": "linux_path"
    },
    {
      "original": "/bin/controller.cs",
      "anonymized": "/rrfd/controller.cs",
      "anonymizer": "linux_path"
    }
  ]
}
```

## Anonimizatory
- `linux_path` – anonimizuje ścieżki do plików linuxowych
- `windows_path` – anonimizuje ścieżki do plików windowsowych

### Rozszerzalność
- Lista anonimizatorów jest otwarta: można dodawać nowe (np. e-mail, IP, numer telefonu) bez zmian w istniejącym kodzie.
- Nowy anonimizator to osobna klasa implementująca `IAnonymizer`, zarejestrowana w DI.
- Każdy anonimizator ma unikalną nazwę (np. `linux_path`), która trafia do pól `anonymizers` i `anonymizer` w wyniku.
- Anonimizatory można dodawać także z zewnętrznych bibliotek, wystarczy zarejestrować je w kontenerze DI.
