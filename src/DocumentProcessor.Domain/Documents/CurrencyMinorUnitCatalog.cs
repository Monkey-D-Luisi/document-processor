using System.Reflection;
using System.Text.Json;

namespace DocumentProcessor.Domain.Documents;

public static class CurrencyMinorUnitCatalog
{
    public const string SnapshotDateUtc = "2026-10-20";

    private static readonly IReadOnlyDictionary<string, int> MinorUnits = Load();

    public static bool TryGetMinorUnit(string currency, out int minorUnit) =>
        MinorUnits.TryGetValue(currency, out minorUnit);

    public static decimal GetTolerance(int minorUnits)
    {
        if (minorUnits is < 0 or > 4)
            throw new ArgumentOutOfRangeException(nameof(minorUnits));

        var unit = 1m;
        for (var index = 0; index < minorUnits; index++)
            unit /= 10m;

        return unit * 2m;
    }

    public static decimal Round(decimal amount, int minorUnits) =>
        decimal.Round(amount, minorUnits, MidpointRounding.AwayFromZero);

    private static IReadOnlyDictionary<string, int> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "DocumentProcessor.Domain.Documents.CurrencyMinorUnits.json")
            ?? throw new InvalidOperationException("The ISO 4217 currency resource is missing.");
        using var resource = JsonDocument.Parse(stream);
        var currencies = resource.RootElement.GetProperty("currencies");
        var result = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var currency in currencies.EnumerateObject())
        {
            var minorUnits = currency.Value.GetInt32();
            if (currency.Name.Length != 3 || minorUnits is < 0 or > 4)
                throw new InvalidOperationException("The ISO 4217 currency resource is invalid.");

            result.Add(currency.Name, minorUnits);
        }

        return result;
    }
}
