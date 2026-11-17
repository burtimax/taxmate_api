using System.Globalization;
using System.Text.RegularExpressions;
using Application.Abstractions;
using Application.Common;
using Domain.Reports;
using Domain.Reports.UltimaBcs;
using Domain.Tax;

namespace Application.Tax;

public sealed class UltimaBcsTaxCalculator
{
    private static readonly Regex TradeIdRegex = new(@"№\s*(?<id>F[A-Z0-9]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly HashSet<string> ExchangeVenues = new(StringComparer.OrdinalIgnoreCase)
    {
        "NASDAQ", "NYSE", "AMEX", "LSE", "HKEX", "XETRA", "EURONEXT", "TSX", "ASX"
    };

    public async Task<TaxCalculationResult> CalculateAsync(
        UltimaBcsReportDocument document,
        IFxRateProvider fx,
        IReadOnlyList<ManualExpenseOverride>? manualExpenses = null,
        CancellationToken ct = default)
    {
        var ops = new List<TaxOperation>();
        var issues = new List<string>();
        var manualMap = (manualExpenses ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.OperationId))
            .ToDictionary(x => x.OperationId, StringComparer.OrdinalIgnoreCase);

        BuildCashIncomeOperations(document, ops, issues);
        BuildSecuritySaleOperations(document, ops, issues, manualMap);

        // Сначала подставляем расходы (даты покупок), затем грузим курсы
        foreach (var op in ops.Where(o => o.Kind == IncomeKind.DerivativeClose))
            ApplyDerivativeExpense(document, op, issues);

        foreach (var op in ops)
        {
            if (manualMap.TryGetValue(op.Id, out var manual))
                ApplyManualExpense(op, manual);
        }

        var rateRequests = CollectRateRequests(ops, manualMap);
        await fx.EnsureRatesAsync(rateRequests, ct);

        ApplyFxAndExpenses(ops, fx, issues);

        var taxYear = ResolveTaxYear(document);
        var codeSummaries = BuildCodeSummaries(ops);
        var groups = BuildDeclarationGroups(codeSummaries, taxYear);

        return new TaxCalculationResult
        {
            ReportTypeId = ReportTypeIds.UltimaBcs,
            ClientName = document.Client,
            Agreement = document.Agreement,
            BrokerName = document.BrokerName,
            PeriodFrom = document.PeriodFrom,
            PeriodTo = document.PeriodTo,
            TaxYear = taxYear,
            Operations = ops.OrderBy(o => o.IncomeDate).ThenBy(o => o.IncomeCode).ToList(),
            CodeSummaries = codeSummaries,
            DeclarationGroups = groups,
            Issues = issues
        };
    }

    private static void BuildCashIncomeOperations(
        UltimaBcsReportDocument document,
        List<TaxOperation> ops,
        List<string> issues)
    {
        var transfers = document.CashTransfers;
        for (var i = 0; i < transfers.Count; i++)
        {
            var t = transfers[i];
            if (!string.Equals(t.Type, "Credit", StringComparison.OrdinalIgnoreCase))
                continue;

            var details = t.Details ?? "";
            var date = DateParsing.ParseRu(t.Date);
            if (date is null || t.Amount is null || string.IsNullOrWhiteSpace(t.Currency))
            {
                issues.Add($"Пропущена строка cash transfer: {t.Date} {details}");
                continue;
            }

            var tradeId = ExtractTradeId(details);
            var currency = t.Currency!;

            if (details.StartsWith("Fixing", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(CreateOp(
                    id: $"fixing-{i}-{tradeId ?? date.Value.ToString("yyyyMMdd")}",
                    kind: IncomeKind.Fixing,
                    code: "1533",
                    date: date.Value,
                    currency: currency,
                    description: details,
                    tradeId: tradeId,
                    incomeFx: t.Amount.Value));
                continue;
            }

            if (details.Contains("Early termination", StringComparison.OrdinalIgnoreCase) ||
                details.StartsWith("Execution", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(CreateOp(
                    id: $"deriv-close-{i}-{tradeId ?? date.Value.ToString("yyyyMMdd")}",
                    kind: IncomeKind.DerivativeClose,
                    code: "1533",
                    date: date.Value,
                    currency: currency,
                    description: details,
                    tradeId: tradeId,
                    incomeFx: t.Amount.Value));
                continue;
            }

            if (details.Contains("Dividend Payment", StringComparison.OrdinalIgnoreCase) &&
                !details.Contains("Tax on Dividend", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(CreateOp(
                    id: $"div-{i}-{date.Value:yyyyMMdd}",
                    kind: IncomeKind.Dividend,
                    code: "1010",
                    date: date.Value,
                    currency: currency,
                    description: details,
                    incomeFx: t.Amount.Value,
                    isin: ExtractIsin(details),
                    ticker: ExtractTicker(details)));
                continue;
            }

            if (details.Contains("coupon", StringComparison.OrdinalIgnoreCase) &&
                !details.Contains("OTC Equity Derivative", StringComparison.OrdinalIgnoreCase) &&
                !details.Contains("Trade settlement", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(CreateOp(
                    id: $"coupon-{i}-{date.Value:yyyyMMdd}",
                    kind: IncomeKind.Coupon,
                    code: "1530",
                    date: date.Value,
                    currency: currency,
                    description: details,
                    incomeFx: t.Amount.Value));
            }
        }

        AddDividendTaxWithholdingOperations(transfers, ops, issues);
    }

    private static void AddDividendTaxWithholdingOperations(
        List<CashTransfer> transfers,
        List<TaxOperation> ops,
        List<string> issues)
    {
        for (var i = 0; i < transfers.Count; i++)
        {
            var t = transfers[i];
            if (!string.Equals(t.Type, "Debit", StringComparison.OrdinalIgnoreCase))
                continue;

            var details = t.Details ?? "";
            if (!details.Contains("Tax on Dividend", StringComparison.OrdinalIgnoreCase))
                continue;

            var date = DateParsing.ParseRu(t.Date);
            if (date is null || t.Amount is null || string.IsNullOrWhiteSpace(t.Currency))
            {
                issues.Add($"Пропущено удержание налога: {t.Date} {details}");
                continue;
            }

            var op = CreateOp(
                id: $"div-tax-{i}-{date.Value:yyyyMMdd}",
                kind: IncomeKind.DividendTaxWithholding,
                code: "1010",
                date: date.Value,
                currency: t.Currency!,
                description: details,
                incomeFx: 0,
                taxWithheldFx: t.Amount.Value,
                isin: ExtractIsin(details),
                ticker: ExtractTickerFromTaxDetails(details));
            op.TaxWithheldDate = date.Value;
            op.TaxRatePercent = ExtractTaxRatePercent(details);
            ops.Add(op);
        }
    }

    private static void BuildSecuritySaleOperations(
        UltimaBcsReportDocument document,
        List<TaxOperation> ops,
        List<string> issues,
        Dictionary<string, ManualExpenseOverride> manualMap)
    {
        var buys = document.SecurityTransfers
            .Where(x => string.Equals(x.OperationType, "Buy", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.OperationType, "Purchase", StringComparison.OrdinalIgnoreCase))
            .Select(x => new SecurityLot(x))
            .Where(x => x.RemainingQty > 0 && !string.IsNullOrWhiteSpace(x.Isin))
            .OrderBy(x => x.Date)
            .ToList();

        var sells = document.SecurityTransfers
            .Where(x => string.Equals(x.OperationType, "Sell", StringComparison.OrdinalIgnoreCase))
            .ToList();

        for (var i = 0; i < sells.Count; i++)
        {
            var sell = sells[i];
            var date = DateParsing.ParseRu(sell.TradeDate);
            if (date is null || sell.Amount is null || string.IsNullOrWhiteSpace(sell.Currency))
            {
                issues.Add($"Пропущена продажа ЦБ: {sell.TradeDate} {sell.Isin}");
                continue;
            }

            var code = ResolveSecurityIncomeCode(sell.Venue);
            var id = $"sell-{i}-{sell.Isin}-{date.Value:yyyyMMdd}";
            var qty = sell.Quantity ?? 0m;
            decimal matchedQty = 0;
            decimal expenseFx = 0;
            DateOnly? expenseDate = null;
            string? expenseCurrency = null;
            var matched = false;

            if (qty > 0 && !string.IsNullOrWhiteSpace(sell.Isin))
            {
                foreach (var lot in buys.Where(b =>
                             string.Equals(b.Isin, sell.Isin, StringComparison.OrdinalIgnoreCase) &&
                             b.RemainingQty > 0 &&
                             b.Date <= date.Value))
                {
                    var take = Math.Min(lot.RemainingQty, qty - matchedQty);
                    if (take <= 0) break;

                    var unitCost = lot.UnitCost;
                    expenseFx += unitCost * take;
                    lot.RemainingQty -= take;
                    matchedQty += take;
                    expenseDate ??= lot.Date;
                    expenseCurrency ??= lot.Currency;
                    matched = true;

                    if (matchedQty >= qty) break;
                }
            }

            var needsManual = !matched || matchedQty < qty;
            if (manualMap.ContainsKey(id))
                needsManual = false;

            var op = CreateOp(
                id: id,
                kind: IncomeKind.SecuritySale,
                code: code,
                date: date.Value,
                currency: sell.Currency!,
                description: $"{sell.AssetType} Sell {sell.Issuer ?? sell.Isin} qty={sell.Quantity}",
                tradeId: sell.TradeId,
                incomeFx: sell.Amount.Value,
                isin: sell.Isin,
                ticker: sell.Issuer,
                venue: sell.Venue,
                quantity: sell.Quantity,
                commissionFx: sell.Commission);

            if (matched && expenseFx > 0)
            {
                op.ExpenseAmountFx = expenseFx;
                op.ExpenseDate = expenseDate;
                if (!string.IsNullOrWhiteSpace(expenseCurrency))
                    op.Currency = sell.Currency!;
            }

            // Floating-point qty noise: если почти всё покрыто FIFO — считаем ок
            if (needsManual && matchedQty > 0 && qty > 0 && matchedQty / qty >= 0.999m)
            {
                needsManual = false;
                op.StatusMessage = null;
            }

            if (needsManual && !manualMap.ContainsKey(id))
            {
                op.Status = OperationStatus.NeedsManualExpense;
                op.StatusMessage = matchedQty > 0
                    ? $"Найдена только часть покупки ({matchedQty} из {qty}). Укажите расходы вручную."
                    : "Покупка не найдена в отчёте. Укажите расходы вручную.";
            }

            ops.Add(op);
        }
    }

    private static IEnumerable<(string Currency, DateOnly Date)> CollectRateRequests(
        List<TaxOperation> ops,
        Dictionary<string, ManualExpenseOverride> manualMap)
    {
        foreach (var op in ops)
        {
            yield return (op.Currency, op.IncomeDate);
            if (op.ExpenseDate is not null)
                yield return (op.Currency, op.ExpenseDate.Value);
        }

        foreach (var m in manualMap.Values)
        {
            if (m.PurchaseDate is null) continue;
            var cur = string.IsNullOrWhiteSpace(m.Currency) ? "USD" : m.Currency!;
            yield return (cur, m.PurchaseDate.Value);
        }
    }

    private static void ApplyFxAndExpenses(
        List<TaxOperation> ops,
        IFxRateProvider fx,
        List<string> issues)
    {
        foreach (var op in ops)
        {
            var incomeRate = fx.GetRate(op.Currency, op.IncomeDate);
            if (incomeRate is null)
            {
                op.Status = op.Status == OperationStatus.NeedsManualExpense ? op.Status : OperationStatus.Warning;
                op.StatusMessage = AppendMessage(op.StatusMessage, $"Нет курса {op.Currency} на {DateParsing.FormatRu(op.IncomeDate)}");
                issues.Add(op.StatusMessage!);
            }
            else
            {
                op.IncomeFxRate = incomeRate;
                if (op.Kind == IncomeKind.DividendTaxWithholding)
                {
                    op.IncomeAmountRub = 0;
                    if (op.TaxWithheldFx is not null)
                        op.TaxWithheldRub = Round2(op.TaxWithheldFx.Value * incomeRate.Value);
                }
                else
                {
                    op.IncomeAmountRub = Round2(op.IncomeAmountFx * incomeRate.Value);
                    if (op.TaxWithheldFx is not null)
                        op.TaxWithheldRub = Round2(op.TaxWithheldFx.Value * incomeRate.Value);
                    if (op.CommissionFx is not null)
                        op.CommissionAmountRub = Round2(op.CommissionFx.Value * incomeRate.Value);
                }
            }

            if (op.ExpenseAmountFx is not null && op.ExpenseAmountFx > 0)
            {
                var expenseDate = op.ExpenseDate ?? op.IncomeDate;
                var expenseRate = op.ExpenseFxRate ?? fx.GetRate(op.Currency, expenseDate);
                if (expenseRate is null)
                {
                    op.Status = OperationStatus.Warning;
                    op.StatusMessage = AppendMessage(op.StatusMessage, $"Нет курса для расхода на {DateParsing.FormatRu(expenseDate)}");
                }
                else
                {
                    op.ExpenseFxRate = expenseRate;
                    op.ExpenseDate = expenseDate;
                    op.ExpenseAmountRub = Round2(op.ExpenseAmountFx.Value * expenseRate.Value);
                }
            }

            if (op.Kind == IncomeKind.DividendTaxWithholding)
                op.FinancialResultRub = 0;
            else
                op.FinancialResultRub = Round2(op.IncomeAmountRub - op.ExpenseAmountRub - op.CommissionAmountRub);

            if (op.Status == OperationStatus.NeedsManualExpense)
                continue;

            if (op.Status != OperationStatus.Warning)
                op.Status = OperationStatus.Ok;
        }
    }

    private static void ApplyDerivativeExpense(UltimaBcsReportDocument document, TaxOperation op, List<string> issues)
    {
        if (string.IsNullOrWhiteSpace(op.TradeId))
        {
            op.Status = OperationStatus.Warning;
            op.StatusMessage = "Не найден Trade ID для сопоставления покупки ПФИ";
            issues.Add($"{op.Description}: нет Trade ID");
            return;
        }

        var eventKind = op.Description.Contains("Early termination", StringComparison.OrdinalIgnoreCase)
            ? "Early termination"
            : "Execution";

        var trade = document.Trades.FirstOrDefault(t =>
            string.Equals(t.TradeId, op.TradeId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(t.BuySell, "Buy", StringComparison.OrdinalIgnoreCase) &&
            (t.Kind?.Contains(eventKind, StringComparison.OrdinalIgnoreCase) == true ||
             string.Equals(t.Kind, eventKind, StringComparison.OrdinalIgnoreCase)));

        trade ??= document.Trades.FirstOrDefault(t =>
            string.Equals(t.TradeId, op.TradeId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(t.BuySell, "Buy", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(t.Kind, "New", StringComparison.OrdinalIgnoreCase) ||
             string.IsNullOrWhiteSpace(t.Kind)));

        // Prefer the New/purchase trade for expense basis when closing
        var purchase = document.Trades
            .Where(t => string.Equals(t.TradeId, op.TradeId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(t.BuySell, "Buy", StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => DateParsing.ParseRu(t.TradeDate) ?? DateOnly.MaxValue)
            .FirstOrDefault();

        purchase ??= trade;

        if (purchase?.Notional is null)
        {
            op.Status = OperationStatus.Warning;
            op.StatusMessage = $"Не найдена покупка ПФИ в секции 3.1 для {op.TradeId}";
            issues.Add(op.StatusMessage);
            return;
        }

        var coefficient = purchase.ValueCoefficient ?? 1m;
        op.ExpenseAmountFx = purchase.Notional.Value * coefficient;
        op.ExpenseDate = DateParsing.ParseRu(purchase.TradeDate);
        if (!string.IsNullOrWhiteSpace(purchase.Currency))
        {
            // expense currency should match purchase; keep income currency for income conversion
        }
    }

    private static void ApplyManualExpense(TaxOperation op, ManualExpenseOverride manual)
    {
        if (manual.ExpenseAmountFx is null || manual.PurchaseDate is null)
            return;

        op.ExpenseAmountFx = manual.ExpenseAmountFx;
        op.ExpenseDate = manual.PurchaseDate;
        if (!string.IsNullOrWhiteSpace(manual.Currency))
            op.Currency = manual.Currency!;
        if (manual.FxRateOverride is not null)
            op.ExpenseFxRate = manual.FxRateOverride;
        op.ExpenseIsManual = true;
        op.Status = OperationStatus.Ok;
        op.StatusMessage = string.IsNullOrWhiteSpace(manual.Note) ? "Расход указан вручную" : manual.Note;
    }

    private static List<TaxCodeSummary> BuildCodeSummaries(List<TaxOperation> ops)
    {
        var incomeKinds = new HashSet<IncomeKind>
        {
            IncomeKind.Fixing,
            IncomeKind.DerivativeClose,
            IncomeKind.Dividend,
            IncomeKind.Coupon,
            IncomeKind.SecuritySale
        };

        var codes = ops
            .Where(o => incomeKinds.Contains(o.Kind) || o.Kind == IncomeKind.DividendTaxWithholding)
            .Select(o => o.IncomeCode)
            .Distinct()
            .OrderBy(x => x);

        var result = new List<TaxCodeSummary>();
        foreach (var code in codes)
        {
            var incomeOps = ops.Where(o => o.IncomeCode == code && incomeKinds.Contains(o.Kind)).ToList();
            var withholdingOps = ops.Where(o => o.IncomeCode == code && o.Kind == IncomeKind.DividendTaxWithholding).ToList();
            if (incomeOps.Count == 0 && withholdingOps.Count == 0) continue;

            var income = Round2(incomeOps.Sum(x => x.IncomeAmountRub));
            var expense = Round2(incomeOps.Sum(x => x.ExpenseAmountRub));
            var commission = Round2(incomeOps.Sum(x => x.CommissionAmountRub));
            var financial = Round2(income - expense - commission);
            var withheld = Round2(
                withholdingOps.Sum(x => x.TaxWithheldRub) +
                incomeOps.Sum(x => x.TaxWithheldRub));
            var taxBase = financial > 0 ? financial : 0;
            var loss = financial < 0 ? Math.Abs(financial) : 0;

            result.Add(new TaxCodeSummary
            {
                IncomeCode = code,
                Description = DescribeCode(code),
                IncomeRub = income,
                ExpenseRub = expense,
                CommissionRub = commission,
                FinancialResultRub = financial,
                TaxWithheldRub = withheld,
                TaxBaseRub = taxBase,
                CarryForwardLossRub = loss
            });
        }

        return result;
    }

    private static List<DeclarationGroupSummary> BuildDeclarationGroups(List<TaxCodeSummary> codes, int taxYear)
    {
        var groups = new List<(string Code, string Title, string[] IncomeCodes)>
        {
            ("01", "Дивиденды", ["1010"]),
            ("02", "Операции с ЦБ", ["1530", "1531"]),
            ("03", "Операции с ПФИ", ["1533"]),
        };

        var result = new List<DeclarationGroupSummary>();
        foreach (var g in groups)
        {
            var subset = codes.Where(c => g.IncomeCodes.Contains(c.IncomeCode)).ToList();
            if (subset.Count == 0) continue;

            var income = Round2(subset.Sum(x => x.IncomeRub));
            var expense = Round2(subset.Sum(x => x.ExpenseRub + x.CommissionRub));
            var taxBase = Round2(subset.Sum(x => x.TaxBaseRub));
            var withheld = Round2(subset.Sum(x => x.TaxWithheldRub));
            var (tax13, tax15) = SplitProgressiveTax(taxBase, taxYear);
            var due = Math.Max(0, Round0(tax13 + tax15) - Round0(withheld));

            result.Add(new DeclarationGroupSummary
            {
                GroupCode = g.Code,
                Title = g.Title,
                IncomeCodes = g.IncomeCodes,
                IncomeRub = income,
                ExpenseRub = expense,
                TaxBaseRub = taxBase,
                TaxWithheldRub = withheld,
                Tax13Rub = Round0(tax13),
                Tax15Rub = Round0(tax15),
                TaxDueRub = due
            });
        }

        return result;
    }

    private static (decimal Tax13, decimal Tax15) SplitProgressiveTax(decimal taxBase, int taxYear)
    {
        if (taxBase <= 0) return (0, 0);

        // 2024–2026: 13% up to 2.4M, 15% above. From 2027 broader scale — keep 13/15 for v1.
        const decimal threshold = 2_400_000m;
        var base13 = Math.Min(taxBase, threshold);
        var base15 = Math.Max(0, taxBase - threshold);
        _ = taxYear;
        return (base13 * 0.13m, base15 * 0.15m);
    }

    private static TaxOperation CreateOp(
        string id,
        IncomeKind kind,
        string code,
        DateOnly date,
        string currency,
        string description,
        decimal incomeFx,
        string? tradeId = null,
        decimal? taxWithheldFx = null,
        string? isin = null,
        string? ticker = null,
        string? venue = null,
        decimal? quantity = null,
        decimal? commissionFx = null)
        => new()
        {
            Id = id,
            Kind = kind,
            IncomeCode = code,
            IncomeDate = date,
            TaxWithheldDate = date,
            Currency = currency,
            CurrencyNumericCode = CurrencyCodes.ToNumeric(currency),
            Description = description,
            TradeId = tradeId,
            Isin = isin,
            Ticker = ticker,
            Venue = venue,
            Quantity = quantity,
            IncomeAmountFx = incomeFx,
            TaxWithheldFx = taxWithheldFx,
            CommissionFx = commissionFx
        };

    private static string ResolveSecurityIncomeCode(string? venue)
    {
        if (string.IsNullOrWhiteSpace(venue)) return "1530";
        if (venue.Equals("OTC", StringComparison.OrdinalIgnoreCase) ||
            venue.Equals("ОТС", StringComparison.OrdinalIgnoreCase))
            return "1531";
        if (ExchangeVenues.Contains(venue)) return "1530";
        return venue.Contains("OTC", StringComparison.OrdinalIgnoreCase) ? "1531" : "1530";
    }

    private static string? ExtractTradeId(string details)
    {
        var m = TradeIdRegex.Match(details);
        return m.Success ? m.Groups["id"].Value : null;
    }

    private static string? ExtractIsin(string details)
    {
        var m = Regex.Match(details, @"\b([A-Z]{2}[A-Z0-9]{9}\d)\b");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? ExtractTicker(string details)
    {
        var m = Regex.Match(details, @"Dividend Payment\s+\d[\d ]*\s+([A-Z0-9._]+)\s+[A-Z]{2}[A-Z0-9]{9}\d", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? ExtractTickerFromTaxDetails(string details)
    {
        var m = Regex.Match(details, @"Tax on Dividend Payment\s+\d[\d ]*\s+([A-Z0-9._]+)\s+[A-Z]{2}[A-Z0-9]{9}\d", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : ExtractTicker(details);
    }

    private static decimal? ExtractTaxRatePercent(string details)
    {
        var m = Regex.Match(details, @"tax\s*rate\s*(?<rate>\d+(?:[.,]\d+)?)\s*%", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var raw = m.Groups["rate"].Value.Replace(',', '.');
        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var rate) ? rate : null;
    }

    private static int ResolveTaxYear(UltimaBcsReportDocument document)
    {
        var to = DateParsing.ParseRu(document.PeriodTo);
        if (to is not null) return to.Value.Year;
        var from = DateParsing.ParseRu(document.PeriodFrom);
        return from?.Year ?? DateTime.UtcNow.Year;
    }

    private static string DescribeCode(string code) => code switch
    {
        "1010" => "Дивиденды",
        "1530" => "ЦБ обращающиеся / купоны",
        "1531" => "ЦБ необращающиеся (OTC)",
        "1533" => "ПФИ / OTC Equity Derivatives",
        _ => $"Код {code}"
    };

    private static string AppendMessage(string? existing, string next)
        => string.IsNullOrWhiteSpace(existing) ? next : $"{existing}; {next}";

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal Round0(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);

    private sealed class SecurityLot
    {
        public SecurityLot(SecurityTransfer transfer)
        {
            Date = DateParsing.ParseRu(transfer.TradeDate) ?? DateOnly.MinValue;
            Isin = transfer.Isin;
            Currency = transfer.Currency ?? "USD";
            RemainingQty = transfer.Quantity ?? 0;
            var amount = transfer.Amount ?? ((transfer.Price ?? 0) * RemainingQty);
            UnitCost = RemainingQty > 0 ? amount / RemainingQty : 0;
        }

        public DateOnly Date { get; }
        public string? Isin { get; }
        public string Currency { get; }
        public decimal RemainingQty { get; set; }
        public decimal UnitCost { get; }
    }
}
