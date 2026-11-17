using System.Globalization;
using System.Text.RegularExpressions;
using Application.Abstractions;
using Domain.Reports.UltimaBcs;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Infrastructure.Parsing.UltimaBcs;

public sealed class UltimaBcsReportParser : IUltimaBcsReportParser
{
    // Иногда между датами есть время сделки: 03.11.2023 15:14:46 03.11.2023
    private static readonly Regex TradeStartRegex =
        new(@"^(?<id>F[A-Z0-9]+)\s+(?<tradeDate>\d{2}\.\d{2}\.\d{4})(?:\s+\d{1,2}:\d{2}(?::\d{2})?)?\s+(?<paymentDate>\d{2}\.\d{2}\.\d{4})$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AssetLineRegex =
        new(@"^(?<ticker>[A-Z_]{1,20})\s+(?<isin>[A-Z0-9]{10,12})\s+(?<currency>[A-Z]{3})\s+(?<price>\d+(?:\.\d+)?)$",
            RegexOptions.Compiled);

    private static readonly Regex TransferRegex =
        new(@"^(?<date>\d{2}\.\d{2}\.\d{4})\s+(?<location>.+?)\s+(?<type>Credit|Debit)\s+(?<amount>\d{1,3}(?: \d{3})*(?:\.\d+)?|\d+(?:\.\d+)?)\s+(?<currency>[A-Z]{3})\s+(?<details>.+)$",
            RegexOptions.Compiled);

    private static readonly Regex Section3ValueLineRegex =
        new(@"^(?<notional>\d{1,3}(?: \d{3})*(?:\.\d+)?|\d+(?:\.\d+)?)\s+(?<valueCoefficient>\d+(?:\.\d+)?)\s+(?<currency>[A-Z]{3})\s+(?<venue>[A-Z]+)$",
            RegexOptions.Compiled);

    // Числа с пробелом тысяч: 2 000 / 100 578.00; N/A как пропуск
    private static readonly Regex SecurityTransferRegex =
        new(@"^(?<tradeDate>\d{2}\.\d{2}\.\d{4})\s+(?<tradeId>\S+)\s+(?<assetType>Shares|Bonds)\s+(?<isin>[A-Z0-9]{10,12})\s+(?<operation>Sell|Buy|Receipt|Purchase|Delivery)\s+(?<qty>\d{1,3}(?: \d{3})*(?:\.\d+)?|\d+(?:\.\d+)?)\s+(?<price>\d{1,3}(?: \d{3})*(?:\.\d+)?|\d+(?:\.\d+)?|N\/A|-)\s+(?<markup>\d{1,3}(?: \d{3})*(?:\.\d+)?|\d+(?:\.\d+)?|N\/A|-)\s+(?<amount>\d{1,3}(?: \d{3})*(?:\.\d+)?|\d+(?:\.\d+)?|N\/A|-)\s+(?<accrual>\d{1,3}(?: \d{3})*(?:\.\d+)?|\d+(?:\.\d+)?|N\/A|-)\s+(?<currency>[A-Z]{3})\s+(?<venue>\S+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public bool CanParse(Stream pdfStream)
    {
        try
        {
            var text = ExtractText(pdfStream);
            return text.Contains("Account statement report", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("OTC Equity Derivatives", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("The Ultima World", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
        finally
        {
            if (pdfStream.CanSeek) pdfStream.Position = 0;
        }
    }

    public UltimaBcsReportDocument Parse(Stream pdfStream, string? fileName = null)
    {
        var rawText = ExtractText(pdfStream);
        var lines = NormalizeLines(rawText);
        var text = string.Join("\n", lines);

        var report = new UltimaBcsReportDocument
        {
            SourceFile = fileName
        };

        ParseHeader(text, report);

        var transactionsSection = Between(text, "2. TRANSACTIONS", "3. TRADES / REPO TRADES / FUNDS PLACEMENT");
        if (string.IsNullOrWhiteSpace(transactionsSection))
            transactionsSection = Between(text, "2. TRANSACTIONS", "3. TRADES");

        // Нельзя искать просто "2.2" — совпадает с датой вида 12.02.2024
        var cashSection = Between(transactionsSection, "2.1. Funds transfers", "2.2. Securities transfers");
        if (string.IsNullOrWhiteSpace(cashSection))
            cashSection = Between(transactionsSection, "Funds transfers", "Securities transfers");
        if (string.IsNullOrWhiteSpace(cashSection))
            cashSection = transactionsSection;

        report.CashTransfers = ParseCashTransfers(cashSection);

        var securitiesSection = Between(transactionsSection, "2.2. Securities transfers", "");
        if (string.IsNullOrWhiteSpace(securitiesSection))
            securitiesSection = Between(transactionsSection, "Securities transfers", "");
        report.SecurityTransfers = ParseSecurityTransfers(securitiesSection);

        var tradesSection = Between(text, "3. TRADES / REPO TRADES / FUNDS PLACEMENT", "4. OUTSTANDING POSITION");
        if (string.IsNullOrWhiteSpace(tradesSection))
            tradesSection = Between(text, "3.1. OTC Equity Derivatives", "4. OUTSTANDING POSITION");
        report.Trades = ParseTradesSection(tradesSection);

        return report;
    }

    private static string ExtractText(Stream pdfStream)
    {
        if (pdfStream.CanSeek) pdfStream.Position = 0;
        using var doc = PdfDocument.Open(pdfStream);
        var pages = new List<string>();
        foreach (var page in doc.GetPages())
            pages.Add(ContentOrderTextExtractor.GetText(page));
        return string.Join("\n", pages);
    }

    private static List<string> NormalizeLines(string text)
        => text.Replace("\r", "\n")
            .Split('\n', StringSplitOptions.None)
            .Select(NormalizeLine)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !IsFooterOrNoise(x))
            .ToList();

    private static string NormalizeLine(string line)
    {
        line = line.Replace('\u00A0', ' ');
        line = Regex.Replace(line, @"[ \t]+", " ");
        return line.Trim();
    }

    private static bool IsFooterOrNoise(string line)
    {
        if (Regex.IsMatch(line, @"^_{20,}$")) return true;
        if (line.StartsWith("The Ultima World", StringComparison.OrdinalIgnoreCase)) return true;
        if (line.StartsWith("Unit 1002", StringComparison.OrdinalIgnoreCase)) return true;
        if (line.StartsWith("Tel:", StringComparison.OrdinalIgnoreCase)) return true;
        if (line.StartsWith("Please kindly note", StringComparison.OrdinalIgnoreCase)) return true;
        if (line.StartsWith("Head of Operations", StringComparison.OrdinalIgnoreCase)) return true;
        if (line.StartsWith("Stepanova", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string Between(string text, string start, string end)
    {
        var startIndex = string.IsNullOrEmpty(start)
            ? 0
            : text.IndexOf(start, StringComparison.OrdinalIgnoreCase);

        if (startIndex < 0) return string.Empty;
        if (!string.IsNullOrEmpty(start)) startIndex += start.Length;

        if (string.IsNullOrEmpty(end))
            return text[startIndex..].Trim();

        var endIndex = text.IndexOf(end, startIndex, StringComparison.OrdinalIgnoreCase);
        if (endIndex < 0) endIndex = text.Length;
        return text[startIndex..endIndex].Trim();
    }

    private static void ParseHeader(string text, UltimaBcsReportDocument report)
    {
        var period = Regex.Match(text,
            @"Account statement report for the period from (?<from>\d{2}\.\d{2}\.\d{4}) till (?<to>\d{2}\.\d{2}\.\d{4})",
            RegexOptions.IgnoreCase);
        if (period.Success)
        {
            report.PeriodFrom = period.Groups["from"].Value;
            report.PeriodTo = period.Groups["to"].Value;
        }

        var client = Regex.Match(text, @"Client:\s*(?<value>.+)", RegexOptions.IgnoreCase);
        if (client.Success) report.Client = client.Groups["value"].Value.Trim();

        var agreement = Regex.Match(text, @"Agreement:\s*(?<value>.+)", RegexOptions.IgnoreCase);
        if (agreement.Success) report.Agreement = agreement.Groups["value"].Value.Trim();
    }

    private static List<CashTransfer> ParseCashTransfers(string sectionText)
    {
        var list = new List<CashTransfer>();
        foreach (var line in sectionText.Split('\n').Select(NormalizeLine))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var m = TransferRegex.Match(line);
            if (m.Success)
            {
                list.Add(new CashTransfer
                {
                    Date = m.Groups["date"].Value,
                    Location = m.Groups["location"].Value,
                    Type = m.Groups["type"].Value,
                    Amount = ParseDecimal(m.Groups["amount"].Value),
                    Currency = m.Groups["currency"].Value,
                    Details = m.Groups["details"].Value
                });
                continue;
            }

            // Продолжение Details со следующей строки (например "tax rate 10 %")
            if (list.Count > 0 &&
                !Regex.IsMatch(line, @"^\d{2}\.\d{2}\.\d{4}\b") &&
                !line.StartsWith("2.", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("Transaction", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("date ", StringComparison.OrdinalIgnoreCase))
            {
                var last = list[^1];
                last.Details = string.IsNullOrWhiteSpace(last.Details)
                    ? line
                    : $"{last.Details} {line}";
            }
        }
        return list;
    }

    private static List<SecurityTransfer> ParseSecurityTransfers(string sectionText)
    {
        var list = new List<SecurityTransfer>();
        foreach (var line in sectionText.Split('\n').Select(NormalizeLine))
        {
            var m = SecurityTransferRegex.Match(line);
            if (!m.Success) continue;

            list.Add(new SecurityTransfer
            {
                TradeDate = m.Groups["tradeDate"].Value,
                TradeId = m.Groups["tradeId"].Value,
                AssetType = m.Groups["assetType"].Value,
                Isin = m.Groups["isin"].Value,
                OperationType = m.Groups["operation"].Value,
                Quantity = ParseOptionalDecimal(m.Groups["qty"].Value),
                Price = ParseOptionalDecimal(m.Groups["price"].Value),
                Amount = ParseOptionalDecimal(m.Groups["amount"].Value),
                Currency = m.Groups["currency"].Value,
                Venue = m.Groups["venue"].Value
            });
        }
        return list;
    }

    private static decimal? ParseOptionalDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value is "-" or "N/A" or "n/a") return null;
        return ParseDecimal(value);
    }

    private static List<OtcTrade> ParseTradesSection(string sectionText)
    {
        var lines = sectionText
            .Split('\n')
            .Select(NormalizeLine)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !x.StartsWith("3.1. OTC Equity Derivatives", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.StartsWith("Trade ID", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.StartsWith("Date", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.StartsWith("Product", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.StartsWith("Maturity", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.StartsWith("Underlying or credit risk assets", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.StartsWith("Venue", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return SplitByTrade(lines)
            .Select(ParseTradeBlock)
            .Where(x => x is not null)
            .Cast<OtcTrade>()
            .ToList();
    }

    private static OtcTrade? ParseTradeBlock(List<string> lines)
    {
        if (lines.Count == 0) return null;
        var first = TradeStartRegex.Match(lines[0]);
        if (!first.Success) return null;

        var trade = new OtcTrade
        {
            TradeId = first.Groups["id"].Value,
            TradeDate = first.Groups["tradeDate"].Value,
            PaymentDate = first.Groups["paymentDate"].Value
        };

        var i = 1;
        var productTypeLines = new List<string>();
        while (i < lines.Count && !lines[i].StartsWith("USD ", StringComparison.OrdinalIgnoreCase))
        {
            productTypeLines.Add(lines[i]);
            i++;
        }

        var productNameLines = new List<string>();
        while (i < lines.Count && !Regex.IsMatch(lines[i], @"^(Informed|Advanced)$", RegexOptions.IgnoreCase))
        {
            productNameLines.Add(lines[i]);
            i++;
        }

        trade.ProductType = Join(productTypeLines);
        trade.ProductName = Join(productNameLines);

        if (i < lines.Count && Regex.IsMatch(lines[i], @"^(Informed|Advanced)$", RegexOptions.IgnoreCase))
            i++;

        if (i < lines.Count)
        {
            var meta = Regex.Match(lines[i], @"^(?<maturity>\d{2}\.\d{2}\.\d{4})\s+(?<buySell>Buy|Sell)(?:\s+(?<kind>.+))?$");
            if (meta.Success)
            {
                trade.BuySell = meta.Groups["buySell"].Value;
                trade.Kind = NullIfEmpty(meta.Groups["kind"].Value);
                i++;
            }
        }

        for (; i < lines.Count; i++)
        {
            var line = lines[i];
            var asset = AssetLineRegex.Match(line);
            if (asset.Success)
            {
                trade.Underlyings.Add(new UnderlyingAsset
                {
                    Ticker = asset.Groups["ticker"].Value,
                    Isin = asset.Groups["isin"].Value,
                    Currency = asset.Groups["currency"].Value,
                    InitialPrice = ParseDecimal(asset.Groups["price"].Value)
                });
                continue;
            }

            var valueLine = Section3ValueLineRegex.Match(line);
            if (valueLine.Success)
            {
                trade.Notional = ParseDecimal(valueLine.Groups["notional"].Value);
                trade.ValueCoefficient = ParseDecimal(valueLine.Groups["valueCoefficient"].Value);
                trade.Currency = valueLine.Groups["currency"].Value;
                trade.Venue = valueLine.Groups["venue"].Value;
            }
        }

        return trade;
    }

    private static List<List<string>> SplitByTrade(List<string> lines)
    {
        var blocks = new List<List<string>>();
        List<string>? current = null;
        foreach (var line in lines)
        {
            if (TradeStartRegex.IsMatch(line))
            {
                if (current is { Count: > 0 }) blocks.Add(current);
                current = [line];
                continue;
            }
            current?.Add(line);
        }
        if (current is { Count: > 0 }) blocks.Add(current);
        return blocks;
    }

    private static decimal ParseDecimal(string value)
    {
        value = value.Replace(" ", "");
        return decimal.Parse(value, NumberStyles.Any, CultureInfo.InvariantCulture);
    }

    private static string? NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Join(IEnumerable<string> lines)
        => string.Join(" ", lines.Select(NormalizeLine).Where(x => !string.IsNullOrWhiteSpace(x)));
}
