namespace Application.Common;

public static class CurrencyCodes
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = "840",
        ["EUR"] = "978",
        ["GBP"] = "826",
        ["CHF"] = "756",
        ["CNY"] = "156",
        ["RUB"] = "643",
        ["HKD"] = "344",
    };

    public static string ToNumeric(string? currency)
        => currency is not null && Map.TryGetValue(currency, out var code) ? code : "000";
}
