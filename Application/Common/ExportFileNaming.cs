namespace Application.Common;

public static class ExportFileNaming
{
    /// <summary>Из «291798IB dated 28.10.2022» → «291798IB».</summary>
    public static string? ExtractAgreementId(string? agreement)
    {
        if (string.IsNullOrWhiteSpace(agreement))
            return null;

        var token = agreement.Trim().Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)[0];
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    public static string BuildStem(string? agreement, string? sourceFileName, string fallback)
    {
        var id = ExtractAgreementId(agreement);
        if (!string.IsNullOrEmpty(id))
            return id;

        if (!string.IsNullOrWhiteSpace(sourceFileName))
            return Path.GetFileNameWithoutExtension(sourceFileName.Trim());

        return fallback;
    }

    public static string ExcelFileName(string? agreement, string? sourceFileName, int taxYear)
        => $"{BuildStem(agreement, sourceFileName, $"Пояснение_{taxYear}")}_пояснение.xlsx";

    public static string DeclarationFileName(string? agreement, string? sourceFileName, int taxYear)
        => $"{BuildStem(agreement, sourceFileName, $"Декларация_{taxYear}")}.de{taxYear % 10}";
}
