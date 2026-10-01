# Anonimizator

Biblioteka .NET 10 anonimizująca ścieżki plików (Linux i Windows) w czystym tekście. Specyfikacja: `opis.md`, plan: `PLAN.md`.

## Użycie

```csharp
var services = new ServiceCollection().AddAnonymizer().BuildServiceProvider();
AnonymizationResult result = services.GetRequiredService<IAnonymizationService>().Anonymize(text);
```

CLI: `cat plik.txt | dotnet run --project Anonymizer.Cli` (JSON na stdout).

## Nowy anonimizator

Klasa implementująca `IAnonymizer` (unikalne `Name`, `Triggers`, `TryMatch`, `Replace`) i rejestracja: `services.AddAnonymizer<MyAnonymizer>()`.

## Polecenia

- `dotnet build`
- `dotnet test`
- `dotnet run -c Release --project Anonymizer.Bench` – benchmark serwisu
