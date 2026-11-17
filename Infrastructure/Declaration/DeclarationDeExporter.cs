using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Abstractions;
using Application.Common;
using Domain.Tax;

namespace Infrastructure.Declaration;

public sealed class DeclarationDeExporter : IDeclarationDeExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public DeclarationExportResult Export(TaxCalculationResult result, DeclarationExportOptions? options = null)
    {
        options ??= new DeclarationExportOptions();
        var incomes = BuildCurrencyIncomes(result);
        var person = BuildPersonInfo(result, options);
        var year = result.TaxYear > 0 ? result.TaxYear : DateTime.Now.Year;
        var agreement = options.Agreement ?? result.Agreement;
        var fileName = ExportFileNaming.DeclarationFileName(agreement, options.SourceFileName, year);

        object document = year >= 2025
            ? BuildDe5(person, incomes, options)
            : BuildDe4(person, incomes, options);

        var json = JsonSerializer.Serialize(document, JsonOptions);
        return new DeclarationExportResult
        {
            Content = Encoding.UTF8.GetBytes(json),
            FileName = fileName,
            ContentType = "application/json"
        };
    }

    private static DeclarationDe4Document BuildDe4(
        PersonInfoBlock person,
        List<CurrencyIncomeItem> incomes,
        DeclarationExportOptions options) => new()
    {
        PersonInfo = person,
        InspectionNumber = options.InspectionNumber ?? "",
        CurrencyIncomeList = incomes,
        CurrencyQuantity = incomes.Count,
        SourseInfoQuantity = 0
    };

    private static DeclarationDe5Document BuildDe5(
        PersonInfoBlock person,
        List<CurrencyIncomeItem> incomes,
        DeclarationExportOptions options) => new()
    {
        PersonInfo = person,
        InspectionNumber = options.InspectionNumber ?? "",
        CurrencyIncomeList = incomes,
        CurrencyQuantity = incomes.Count,
        SourseInfoQuantity = 0
    };

    private static PersonInfoBlock BuildPersonInfo(TaxCalculationResult result, DeclarationExportOptions options)
    {
        var (sur, name, patronymic) = ResolveName(result.ClientName, options);
        return new PersonInfoBlock
        {
            SurName = sur,
            Name = name,
            Patronymic = patronymic,
            INN = options.Inn ?? "",
            CountryCodId = "643",
            OKTMO = options.Oktmo ?? "",
            BirthDate = new DateTime(1970, 1, 1)
        };
    }

    private static (string Sur, string Name, string Patronymic) ResolveName(
        string? clientName,
        DeclarationExportOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.SurName) ||
            !string.IsNullOrWhiteSpace(options.Name) ||
            !string.IsNullOrWhiteSpace(options.Patronymic))
        {
            return (options.SurName ?? "", options.Name ?? "", options.Patronymic ?? "");
        }

        if (string.IsNullOrWhiteSpace(clientName))
            return ("", "", "");

        var parts = clientName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            >= 3 => (parts[0], parts[1], string.Join(' ', parts.Skip(2))),
            2 => (parts[0], parts[1], ""),
            1 => (parts[0], "", ""),
            _ => ("", "", "")
        };
    }

    private static List<CurrencyIncomeItem> BuildCurrencyIncomes(TaxCalculationResult result)
    {
        var withholdings = result.Operations
            .Where(o => o.Kind == IncomeKind.DividendTaxWithholding)
            .ToList();

        var exportable = result.Operations
            .Where(o => o.Kind is IncomeKind.Fixing
                or IncomeKind.DerivativeClose
                or IncomeKind.Dividend
                or IncomeKind.Coupon
                or IncomeKind.SecuritySale)
            .OrderBy(o => o.IncomeDate)
            .ThenBy(o => o.Description)
            .ToList();

        var list = new List<CurrencyIncomeItem>(exportable.Count);
        foreach (var op in exportable)
        {
            list.Add(MapOperation(op, withholdings));
        }

        return list;
    }

    private static CurrencyIncomeItem MapOperation(TaxOperation op, List<TaxOperation> withholdings)
    {
        var rate = op.IncomeFxRate ?? 0m;
        var exchange = Round2(rate * 100m);
        var (country1, country2) = ResolveCountries(op);
        var (subtractCod, subtractSum) = ResolveSubtract(op);
        var (taxFx, taxRub) = ResolveTax(op, withholdings);

        return new CurrencyIncomeItem
        {
            SourseName = op.Description,
            CountryCode1 = country1,
            CountryCode2 = country2,
            IncomeGetDate = op.IncomeDate.ToDateTime(TimeOnly.MinValue),
            HoldDate = (op.TaxWithheldDate ?? op.IncomeDate).ToDateTime(TimeOnly.MinValue),
            CurrencyCode = string.IsNullOrWhiteSpace(op.CurrencyNumericCode) ? "840" : op.CurrencyNumericCode,
            CurrencyName = CurrencyDisplayName(op.Currency),
            CurrencyExchange = exchange,
            CurrencyUnits = 100,
            CurrencyExchangeHoldDate = exchange,
            CurrencyUnitsHoldDate = 100,
            IncomeRoubleSum = Round2(op.IncomeAmountRub),
            TaxCurrencySum = Round2(taxFx),
            TaxRoubleSum = Round2(taxRub),
            IsAutoExchange = false,
            TaxRate = 0,
            KIKIncomeType = 0,
            KIKNum = "",
            TaxKIKHoldRF = 0,
            IncomeCod = string.IsNullOrWhiteSpace(op.IncomeCode) ? DefaultIncomeCode(op.Kind) : op.IncomeCode,
            IncomeSum = Round2(op.IncomeAmountFx),
            SubtractCod = subtractCod,
            SubtractSum = Round2(subtractSum)
        };
    }

    private static (string Code1, string Code2) ResolveCountries(TaxOperation op) =>
        op.Kind switch
        {
            IncomeKind.Fixing or IncomeKind.DerivativeClose => ("784", "784"),
            _ => ("840", "784")
        };

    private static (string Cod, decimal Sum) ResolveSubtract(TaxOperation op) =>
        op.Kind switch
        {
            IncomeKind.Fixing => ("220", 0m),
            IncomeKind.DerivativeClose => ("220", op.ExpenseAmountRub),
            IncomeKind.SecuritySale or IncomeKind.Coupon => ("201", op.ExpenseAmountRub),
            _ => ("", 0m)
        };

    private static (decimal TaxFx, decimal TaxRub) ResolveTax(TaxOperation op, List<TaxOperation> withholdings)
    {
        if (op.Kind != IncomeKind.Dividend)
            return (op.TaxWithheldFx ?? 0m, op.TaxWithheldRub);

        var tax = FindMatchingWithholding(op, withholdings);
        return (tax?.TaxWithheldFx ?? op.TaxWithheldFx ?? 0m, tax?.TaxWithheldRub ?? op.TaxWithheldRub);
    }

    private static TaxOperation? FindMatchingWithholding(TaxOperation dividend, List<TaxOperation> withholdings)
    {
        return withholdings.FirstOrDefault(t =>
                   t.IncomeDate == dividend.IncomeDate &&
                   string.Equals(t.Currency, dividend.Currency, StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(dividend.Isin) &&
                   string.Equals(t.Isin, dividend.Isin, StringComparison.OrdinalIgnoreCase))
               ?? withholdings.FirstOrDefault(t =>
                   t.IncomeDate == dividend.IncomeDate &&
                   string.Equals(t.Currency, dividend.Currency, StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(dividend.Ticker) &&
                   string.Equals(t.Ticker, dividend.Ticker, StringComparison.OrdinalIgnoreCase))
               ?? withholdings.FirstOrDefault(t =>
                   t.IncomeDate == dividend.IncomeDate &&
                   string.Equals(t.Currency, dividend.Currency, StringComparison.OrdinalIgnoreCase));
    }

    private static string DefaultIncomeCode(IncomeKind kind) => kind switch
    {
        IncomeKind.Fixing or IncomeKind.DerivativeClose => "1533",
        IncomeKind.Dividend => "1010",
        IncomeKind.SecuritySale or IncomeKind.Coupon => "1530",
        _ => ""
    };

    private static string CurrencyDisplayName(string? currency) =>
        (currency ?? "USD").ToUpperInvariant() switch
        {
            "USD" => "Доллар США",
            "EUR" => "Евро",
            "GBP" => "Фунт стерлингов",
            "CHF" => "Швейцарский франк",
            "CNY" => "Юань",
            "HKD" => "Гонконгский доллар",
            "JPY" => "Иена",
            "RUB" or "RUR" => "Российский рубль",
            _ => currency ?? "Доллар США"
        };

    private static decimal Round2(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
