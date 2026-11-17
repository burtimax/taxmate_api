using System.Globalization;

namespace Application.Common;

public static class DateParsing
{
    public static DateOnly? ParseRu(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateOnly.TryParseExact(value.Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;
    }

    public static string FormatRu(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
}
