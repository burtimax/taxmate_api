using System.Globalization;
using Application.Abstractions;
using Application.Common;
using ClosedXML.Excel;
using Domain.Tax;

namespace Infrastructure.Excel;

public sealed class ExplanationExcelExporter : IExplanationExcelExporter
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public byte[] Export(TaxCalculationResult result, string? inn = null)
    {
        using var workbook = new XLWorkbook();
        WriteSummarySheet(workbook, result, inn);
        WriteDerivativesSheet(workbook, result);
        WriteDividendsSheet(workbook, result);
        WriteSecuritiesSheet(workbook, result);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteSummarySheet(XLWorkbook workbook, TaxCalculationResult result, string? inn)
    {
        var ws = workbook.Worksheets.Add("Сводка");
        var r = 1;
        ws.Cell(r++, 1).Value = "ПОЯСНЕНИЕ К 3НДФЛ";
        ws.Cell(r++, 1).Value = $"Налоговый период: {result.TaxYear} год";
        ws.Cell(r++, 1).Value = $"Налогоплательщик: {result.ClientName}";
        ws.Cell(r++, 1).Value = $"ИНН налогоплательщика: {inn}";
        ws.Cell(r++, 1).Value = $"Брокер: {result.BrokerName}";
        ws.Cell(r++, 1).Value = $"Договор: {result.Agreement}";
        ws.Cell(r++, 1).Value = $"Период отчёта: {result.PeriodFrom} — {result.PeriodTo}";
        r++;

        ws.Cell(r, 1).Value = $"Расчет налоговой базы за {result.TaxYear} год";
        r++;

        var headers = new[]
        {
            "Описание", "Код вида дохода", "Доход", "Расход", "Комиссии",
            "Финансовый результат", "Удержано налогов", "Налоговая база", "Убыток"
        };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(r, i + 1).Value = headers[i];
        StyleHeader(ws.Range(r, 1, r, headers.Length));
        r++;

        foreach (var s in result.CodeSummaries)
        {
            ws.Cell(r, 1).Value = s.Description;
            ws.Cell(r, 2).Value = s.IncomeCode;
            ws.Cell(r, 3).Value = s.IncomeRub;
            ws.Cell(r, 4).Value = s.ExpenseRub;
            ws.Cell(r, 5).Value = s.CommissionRub;
            ws.Cell(r, 6).Value = s.FinancialResultRub;
            ws.Cell(r, 7).Value = s.TaxWithheldRub;
            ws.Cell(r, 8).Value = s.TaxBaseRub;
            ws.Cell(r, 9).Value = s.CarryForwardLossRub;
            FormatMoney(ws.Range(r, 3, r, 9));
            r++;
        }

        r += 2;
        ws.Cell(r++, 1).Value = "Данные для декларации. Раздел 2";
        var groupHeaders = new[]
        {
            "Код группы", "Описание", "Сумма доходов", "Сумма расходов",
            "Налоговая база", "Удержано налогов", "Налог 13%", "Налог 15%", "Налог к уплате"
        };
        for (var i = 0; i < groupHeaders.Length; i++)
            ws.Cell(r, i + 1).Value = groupHeaders[i];
        StyleHeader(ws.Range(r, 1, r, groupHeaders.Length));
        r++;

        foreach (var g in result.DeclarationGroups)
        {
            ws.Cell(r, 1).Value = g.GroupCode;
            ws.Cell(r, 2).Value = g.Title;
            ws.Cell(r, 3).Value = g.IncomeRub;
            ws.Cell(r, 4).Value = g.ExpenseRub;
            ws.Cell(r, 5).Value = g.TaxBaseRub;
            ws.Cell(r, 6).Value = g.TaxWithheldRub;
            ws.Cell(r, 7).Value = g.Tax13Rub;
            ws.Cell(r, 8).Value = g.Tax15Rub;
            ws.Cell(r, 9).Value = g.TaxDueRub;
            FormatMoney(ws.Range(r, 3, r, 9));
            r++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void WriteDerivativesSheet(XLWorkbook workbook, TaxCalculationResult result)
    {
        var ops = result.Operations
            .Where(o => o.IncomeCode == "1533")
            .OrderBy(o => o.IncomeDate)
            .ToList();
        var ws = workbook.Worksheets.Add("1533_ПФИ");
        ws.Cell(1, 1).Value = $"{result.ClientName} {result.Agreement}";
        ws.Cell(2, 1).Value = $"Расчёт по операциям за период с {result.PeriodFrom} по {result.PeriodTo}";

        var headers = new[]
        {
            "Дата", "Описание", "Тип", "Trade ID", "Объем в валюте", "Валюта",
            "Курс на дату", "Объем, руб.", "Расход FX", "Дата расхода", "Курс расхода",
            "Расход, руб.", "Фин. результат", "Статус"
        };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(4, i + 1).Value = headers[i];
        StyleHeader(ws.Range(4, 1, 4, headers.Length));

        var row = 5;
        foreach (var o in ops)
        {
            ws.Cell(row, 1).Value = DateParsing.FormatRu(o.IncomeDate);
            ws.Cell(row, 2).Value = o.Description;
            ws.Cell(row, 3).Value = o.Kind.ToString();
            ws.Cell(row, 4).Value = o.TradeId;
            ws.Cell(row, 5).Value = o.IncomeAmountFx;
            ws.Cell(row, 6).Value = o.Currency;
            ws.Cell(row, 7).Value = o.IncomeFxRate;
            ws.Cell(row, 8).Value = o.IncomeAmountRub;
            ws.Cell(row, 9).Value = o.ExpenseAmountFx;
            ws.Cell(row, 10).Value = o.ExpenseDate is null ? "" : DateParsing.FormatRu(o.ExpenseDate.Value);
            ws.Cell(row, 11).Value = o.ExpenseFxRate;
            ws.Cell(row, 12).Value = o.ExpenseAmountRub;
            ws.Cell(row, 13).Value = o.FinancialResultRub;
            ws.Cell(row, 14).Value = o.StatusMessage ?? o.Status.ToString();
            FormatMoney(ws.Range(row, 5, row, 5));
            FormatMoney(ws.Range(row, 8, row, 9));
            FormatMoney(ws.Range(row, 12, row, 13));
            row++;
        }

        row++;
        ws.Cell(row, 1).Value = "Итого доход, руб.";
        ws.Cell(row, 2).Value = ops.Sum(x => x.IncomeAmountRub);
        row++;
        ws.Cell(row, 1).Value = "Итого расход, руб.";
        ws.Cell(row, 2).Value = ops.Sum(x => x.ExpenseAmountRub);
        row++;
        ws.Cell(row, 1).Value = "Итого фин. результат, руб.";
        ws.Cell(row, 2).Value = ops.Sum(x => x.FinancialResultRub);

        ws.Columns().AdjustToContents();
    }

    private static void WriteDividendsSheet(XLWorkbook workbook, TaxCalculationResult result)
    {
        var dividends = result.Operations
            .Where(o => o.Kind == IncomeKind.Dividend)
            .OrderBy(o => o.IncomeDate)
            .ToList();
        var withholdings = result.Operations
            .Where(o => o.Kind == IncomeKind.DividendTaxWithholding)
            .ToList();

        var ws = workbook.Worksheets.Add("1010_Дивиденды");
        var headers = new[]
        {
            "Дата", "Описание", "Выплата в валюте", "Налог в валюте", "Ставка %", "Валюта",
            "Курс", "Дивиденд, руб.", "Налог, руб.", "К доплате (оценка 13%)"
        };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
        StyleHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var o in dividends)
        {
            var tax = FindMatchingWithholding(o, withholdings);
            var taxFx = tax?.TaxWithheldFx ?? o.TaxWithheldFx;
            var taxRub = tax?.TaxWithheldRub ?? o.TaxWithheldRub;
            var estimated = Math.Max(0, Math.Round(o.IncomeAmountRub * 0.13m, 0, MidpointRounding.AwayFromZero) - Math.Round(taxRub, 0, MidpointRounding.AwayFromZero));
            ws.Cell(row, 1).Value = DateParsing.FormatRu(o.IncomeDate);
            ws.Cell(row, 2).Value = o.Description;
            ws.Cell(row, 3).Value = o.IncomeAmountFx;
            ws.Cell(row, 4).Value = taxFx;
            ws.Cell(row, 5).Value = tax?.TaxRatePercent;
            ws.Cell(row, 6).Value = o.Currency;
            ws.Cell(row, 7).Value = o.IncomeFxRate;
            ws.Cell(row, 8).Value = o.IncomeAmountRub;
            ws.Cell(row, 9).Value = taxRub;
            ws.Cell(row, 10).Value = estimated;
            row++;
        }

        ws.Columns().AdjustToContents();
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
                   string.Equals(t.Currency, dividend.Currency, StringComparison.OrdinalIgnoreCase));
    }

    private static void WriteSecuritiesSheet(XLWorkbook workbook, TaxCalculationResult result)
    {
        var ops = result.Operations
            .Where(o => o.IncomeCode is "1530" or "1531")
            .OrderBy(o => o.IncomeDate)
            .ToList();
        var ws = workbook.Worksheets.Add("1530_ЦБ");
        var headers = new[]
        {
            "Дата", "Код", "Описание", "ISIN", "Venue", "Кол-во",
            "Доход FX", "Валюта", "Курс", "Доход руб", "Расход FX", "Дата покупки",
            "Курс покупки", "Расход руб", "Комиссия руб", "Фин. результат", "Статус", "Ручной расход"
        };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
        StyleHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var o in ops)
        {
            ws.Cell(row, 1).Value = DateParsing.FormatRu(o.IncomeDate);
            ws.Cell(row, 2).Value = o.IncomeCode;
            ws.Cell(row, 3).Value = o.Description;
            ws.Cell(row, 4).Value = o.Isin;
            ws.Cell(row, 5).Value = o.Venue;
            ws.Cell(row, 6).Value = o.Quantity;
            ws.Cell(row, 7).Value = o.IncomeAmountFx;
            ws.Cell(row, 8).Value = o.Currency;
            ws.Cell(row, 9).Value = o.IncomeFxRate;
            ws.Cell(row, 10).Value = o.IncomeAmountRub;
            ws.Cell(row, 11).Value = o.ExpenseAmountFx;
            ws.Cell(row, 12).Value = o.ExpenseDate is null ? "" : DateParsing.FormatRu(o.ExpenseDate.Value);
            ws.Cell(row, 13).Value = o.ExpenseFxRate;
            ws.Cell(row, 14).Value = o.ExpenseAmountRub;
            ws.Cell(row, 15).Value = o.CommissionAmountRub;
            ws.Cell(row, 16).Value = o.FinancialResultRub;
            ws.Cell(row, 17).Value = o.StatusMessage ?? o.Status.ToString();
            ws.Cell(row, 18).Value = o.ExpenseIsManual ? "да" : "";
            if (o.Status == OperationStatus.NeedsManualExpense)
                ws.Range(row, 1, row, 18).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF3CD");
            row++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF2");
    }

    private static void FormatMoney(IXLRange range)
    {
        range.Style.NumberFormat.Format = "#,##0.00";
    }
}
