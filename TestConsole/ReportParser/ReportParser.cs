using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace TestConsole.ReportParser;

public sealed class ReportParser
{
    private static readonly Regex TradeStartRegex =
        new(@"^(?<id>F[A-Z0-9]+)\s+(?<tradeDate>\d{2}\.\d{2}\.\d{4})\s+(?<paymentDate>\d{2}\.\d{2}\.\d{4})$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AssetLineRegex =
        new(@"^(?<ticker>[A-Z_]{1,20})\s+(?<isin>[A-Z0-9]{10,12})\s+(?<currency>[A-Z]{3})\s+(?<price>\d+(?:\.\d+)?)$",
            RegexOptions.Compiled);

    private static readonly Regex TransferRegex =
        new(@"^(?<date>\d{2}\.\d{2}\.\d{4})\s+(?<location>.+?)\s+(?<type>Credit|Debit)\s+(?<amount>\d[\d ]*(?:\.\d+)?)\s+(?<currency>[A-Z]{3})\s+(?<details>.+)$",
            RegexOptions.Compiled);

    private static readonly Regex Section3ValueLineRegex =
        new(@"^(?<notional>\d[\d ]*(?:\.\d+)?)\s+(?<valueCoefficient>\d+(?:\.\d+)?)\s+(?<currency>[A-Z]{3})\s+(?<venue>[A-Z]+)$",
            RegexOptions.Compiled);

    private static readonly Regex Section4ValueLineRegex =
        new(@"^Transaction details section\s+(?<cmv>\d[\d ]*(?:\.\d+)?)\s+(?<cmvCurrency>[A-Z]{3})\s+(?<valuationDate>\d{2}\.\d{2}\.\d{4})\s+(?<notional>\d[\d ]*(?:\.\d+)?)\s+(?<valueCoefficient>\d+(?:\.\d+)?)\s+(?<currency>[A-Z]{3})\s+(?<venue>[A-Z]+)$",
            RegexOptions.Compiled);

    private static readonly Regex InitialPriceRegex =
        new(@"^Initial price\s+(?<values>.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex UnderlyingInMatrixRegex =
        new(@"(?<ticker>[A-Z_]+)\s+\((?<isin>[A-Z0-9]+)\)", RegexOptions.Compiled);

    public ReportDocument Parse(string pdfPath)
    {
        var rawText = ExtractText(pdfPath);
        var lines = NormalizeLines(rawText);
        var text = string.Join("\n", lines);

        var report = new ReportDocument
        {
            SourceFile = Path.GetFileName(pdfPath),
            RawText = text
        };

        ParseHeader(text, report);
        report.ConsolidatedCashFlow = ParseBalanceBlock(Between(text, "CONSOLIDATED CASH FLOW", "1. CASH FLOW"));
        report.CashFlow = ParseBalanceBlock(Between(text, "1. CASH FLOW", "2. TRANSACTIONS"));
        report.Transfers = ParseTransfers(Between(text, "2. TRANSACTIONS", "3. TRADES / REPO TRADES / FUNDS PLACEMENT"));
        report.Trades = ParseTradesSection(Between(text, "3. TRADES / REPO TRADES / FUNDS PLACEMENT", "4. OUTSTANDING POSITION"));
        report.OutstandingPositions = MergeOutstanding(ParseOutstandingSection(Between(text, "4. OUTSTANDING POSITION", "5. OTC EQUITY DERIVATIVES TRANSACTION DETAILS")));
        report.TransactionDetails = ParseTransactionDetails(Between(text, "5. OTC EQUITY DERIVATIVES TRANSACTION DETAILS", ""));

        return report;
    }

    private static string ExtractText(string pdfPath)
    {
        using var doc = PdfDocument.Open(pdfPath);
        var pages = new List<string>();

        foreach (var page in doc.GetPages())
        {
            var text = ContentOrderTextExtractor.GetText(page);
            pages.Add(text);
        }

        return string.Join("\n", pages);
    }

    private static List<string> NormalizeLines(string text)
    {
        return text
            .Replace("\r", "\n")
            .Split('\n', StringSplitOptions.None)
            .Select(NormalizeLine)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !IsFooterOrNoise(x))
            .ToList();
    }

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

    private static void ParseHeader(string text, ReportDocument report)
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

    private static BalanceBlock? ParseBalanceBlock(string sectionText)
    {
        var line = sectionText
            .Split('\n')
            .Select(NormalizeLine)
            .FirstOrDefault(x => x.StartsWith("CLIENT MONEY ACCOUNT CASH", StringComparison.OrdinalIgnoreCase));

        if (line is null) return null;

        var m = Regex.Match(line,
            @"^(?<location>CLIENT MONEY ACCOUNT CASH \(USD\))\s+(?<incoming>\d[\d ]*(?:\.\d+)?)\s+(?<credit>\d[\d ]*(?:\.\d+)?)\s+(?<debit>\d[\d ]*(?:\.\d+)?)\s+(?<outgoing>\d[\d ]*(?:\.\d+)?)\s+(?<currency>[A-Z]{3})$");

        if (!m.Success) return null;

        return new BalanceBlock
        {
            Location = m.Groups["location"].Value,
            IncomingBalance = ParseDecimal(m.Groups["incoming"].Value),
            Credit = ParseDecimal(m.Groups["credit"].Value),
            Debit = ParseDecimal(m.Groups["debit"].Value),
            OutgoingBalance = ParseDecimal(m.Groups["outgoing"].Value),
            Currency = m.Groups["currency"].Value
        };
    }

    private static List<TransferTransaction> ParseTransfers(string sectionText)
    {
        var list = new List<TransferTransaction>();

        foreach (var line in sectionText.Split('\n').Select(NormalizeLine))
        {
            var m = TransferRegex.Match(line);
            if (!m.Success) continue;

            list.Add(new TransferTransaction
            {
                Date = m.Groups["date"].Value,
                Location = m.Groups["location"].Value,
                Type = m.Groups["type"].Value,
                Amount = ParseDecimal(m.Groups["amount"].Value),
                Currency = m.Groups["currency"].Value,
                Details = m.Groups["details"].Value
            });
        }

        return list;
    }

    private static List<Trade> ParseTradesSection(string sectionText)
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

        var blocks = SplitByTrade(lines);
        return blocks.Select(ParseTradeBlock).Where(x => x is not null).Cast<Trade>().ToList();
    }

    private static Trade? ParseTradeBlock(List<string> lines)
    {
        if (lines.Count == 0) return null;

        var first = TradeStartRegex.Match(lines[0]);
        if (!first.Success) return null;

        var trade = new Trade
        {
            TradeId = first.Groups["id"].Value,
            TradeDate = first.Groups["tradeDate"].Value,
            PaymentDate = first.Groups["paymentDate"].Value,
            RawBlock = string.Join("\n", lines)
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
        {
            trade.RiskProfile = lines[i];
            i++;
        }

        if (i < lines.Count)
        {
            var meta = Regex.Match(lines[i], @"^(?<maturity>\d{2}\.\d{2}\.\d{4})\s+(?<buySell>Buy|Sell)(?:\s+(?<kind>.+))?$");
            if (meta.Success)
            {
                trade.MaturityDate = meta.Groups["maturity"].Value;
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

    private static List<OutstandingPosition> ParseOutstandingSection(string sectionText)
    {
        var lines = sectionText
            .Split('\n')
            .Select(NormalizeLine)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !x.StartsWith("4.1. OTC Equity Derivatives", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.StartsWith("Trade ID", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.StartsWith("Date", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.StartsWith("Product", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.StartsWith("Maturity", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.StartsWith("Underlying or credit risk assets", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.StartsWith("Venue", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.StartsWith("Weight", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var blocks = SplitByTrade(lines);
        return blocks.Select(ParseOutstandingBlock).Where(x => x is not null).Cast<OutstandingPosition>().ToList();
    }

    private static OutstandingPosition? ParseOutstandingBlock(List<string> lines)
    {
        if (lines.Count == 0) return null;

        var first = TradeStartRegex.Match(lines[0]);
        if (!first.Success) return null;

        var item = new OutstandingPosition
        {
            TradeId = first.Groups["id"].Value,
            TradeDate = first.Groups["tradeDate"].Value,
            PaymentDate = first.Groups["paymentDate"].Value,
            RawBlock = string.Join("\n", lines)
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

        item.ProductType = Join(productTypeLines);
        item.ProductName = Join(productNameLines);

        if (i < lines.Count && Regex.IsMatch(lines[i], @"^(Informed|Advanced)$", RegexOptions.IgnoreCase))
        {
            item.RiskProfile = lines[i];
            i++;
        }

        if (i < lines.Count)
        {
            var meta = Regex.Match(lines[i], @"^(?<maturity>\d{2}\.\d{2}\.\d{4})\s+(?<buySell>Buy|Sell)$");
            if (meta.Success)
            {
                item.MaturityDate = meta.Groups["maturity"].Value;
                item.BuySell = meta.Groups["buySell"].Value;
                i++;
            }
        }

        for (; i < lines.Count; i++)
        {
            var line = lines[i];

            var asset = AssetLineRegex.Match(line);
            if (asset.Success)
            {
                item.Underlyings.Add(new UnderlyingAsset
                {
                    Ticker = asset.Groups["ticker"].Value,
                    Isin = asset.Groups["isin"].Value,
                    Currency = asset.Groups["currency"].Value,
                    InitialPrice = ParseDecimal(asset.Groups["price"].Value)
                });
                continue;
            }

            var valueLine = Section4ValueLineRegex.Match(line);
            if (valueLine.Success)
            {
                item.TransactionDetailsReference = "Transaction details section";
                item.CurrentMarketValuation = ParseDecimal(valueLine.Groups["cmv"].Value);
                item.CurrentMarketValuationCurrency = valueLine.Groups["cmvCurrency"].Value;
                item.ValuationDate = valueLine.Groups["valuationDate"].Value;
                item.Notional = ParseDecimal(valueLine.Groups["notional"].Value);
                item.ValueCoefficient = ParseDecimal(valueLine.Groups["valueCoefficient"].Value);
                item.Currency = valueLine.Groups["currency"].Value;
                item.Venue = valueLine.Groups["venue"].Value;
            }
        }

        return item;
    }

    private static List<OutstandingPosition> MergeOutstanding(List<OutstandingPosition> items)
    {
        var map = new Dictionary<string, OutstandingPosition>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (!map.TryGetValue(item.TradeId, out var existing))
            {
                map[item.TradeId] = item;
                continue;
            }

            existing.ProductType ??= item.ProductType;
            existing.ProductName ??= item.ProductName;
            existing.RiskProfile ??= item.RiskProfile;
            existing.MaturityDate ??= item.MaturityDate;
            existing.BuySell ??= item.BuySell;
            existing.CurrentMarketValuation ??= item.CurrentMarketValuation;
            existing.CurrentMarketValuationCurrency ??= item.CurrentMarketValuationCurrency;
            existing.ValuationDate ??= item.ValuationDate;
            existing.Notional ??= item.Notional;
            existing.ValueCoefficient ??= item.ValueCoefficient;
            existing.Currency ??= item.Currency;
            existing.Venue ??= item.Venue;

            foreach (var u in item.Underlyings)
            {
                if (!existing.Underlyings.Any(x => x.Ticker == u.Ticker && x.Isin == u.Isin))
                    existing.Underlyings.Add(u);
            }

            existing.RawBlock += "\n" + item.RawBlock;
        }

        return map.Values.ToList();
    }

    private static List<TransactionDetailsBlock> ParseTransactionDetails(string sectionText)
    {
        var lines = sectionText
            .Split('\n')
            .Select(NormalizeLine)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var text = string.Join("\n", lines);
        var chunks = Regex.Split(text, @"(?=Transaction details\s+F[A-Z0-9]+)")
            .Select(x => x.Trim())
            .Where(x => x.StartsWith("Transaction details F", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var result = new List<TransactionDetailsBlock>();

        foreach (var chunk in chunks)
        {
            var blockLines = chunk.Split('\n').Select(NormalizeLine).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (blockLines.Count == 0) continue;

            var idMatch = Regex.Match(blockLines[0], @"Transaction details\s+(?<id>F[A-Z0-9]+)", RegexOptions.IgnoreCase);
            if (!idMatch.Success) continue;

            var block = new TransactionDetailsBlock
            {
                TradeId = idMatch.Groups["id"].Value,
                RawBlock = chunk
            };

            var underlyingsLine = blockLines.FirstOrDefault(x =>
                x.Contains("(") && x.Contains(")") &&
                !x.StartsWith("Initial price", StringComparison.OrdinalIgnoreCase) &&
                !x.StartsWith("Transaction details", StringComparison.OrdinalIgnoreCase));

            if (underlyingsLine is not null)
            {
                foreach (Match m in UnderlyingInMatrixRegex.Matches(underlyingsLine))
                {
                    block.Underlyings.Add(new UnderlyingRef
                    {
                        Ticker = m.Groups["ticker"].Value,
                        Isin = m.Groups["isin"].Value
                    });
                }
            }

            var initialPriceLine = blockLines.FirstOrDefault(x => x.StartsWith("Initial price", StringComparison.OrdinalIgnoreCase));
            if (initialPriceLine is not null)
            {
                var p = InitialPriceRegex.Match(initialPriceLine);
                if (p.Success)
                {
                    foreach (Match num in Regex.Matches(p.Groups["values"].Value, @"\d+(?:\.\d+)?"))
                    {
                        block.InitialPrices.Add(ParseDecimal(num.Value));
                    }
                }
            }

            foreach (var line in blockLines)
            {
                if (!Regex.IsMatch(line, @"\d{2}\.\d{2}\.\d{4}")) continue;

                var parts = Regex.Split(line, @"(?=\d{2}\.\d{2}\.\d{4}\b)")
                    .Select(x => x.Trim())
                    .Where(x => Regex.IsMatch(x, @"^\d{2}\.\d{2}\.\d{4}\b"))
                    .ToList();

                foreach (var part in parts)
                {
                    var date = Regex.Match(part, @"^(?<date>\d{2}\.\d{2}\.\d{4})");
                    if (!date.Success) continue;

                    block.ScheduleRows.Add(new DetailScheduleRow
                    {
                        EventDate = date.Groups["date"].Value,
                        RawValues = part[date.Length..].Trim()
                    });
                }
            }

            result.Add(block);
        }

        return result;
    }

    private static List<List<string>> SplitByTrade(List<string> lines)
    {
        var blocks = new List<List<string>>();
        List<string>? current = null;

        foreach (var line in lines)
        {
            if (TradeStartRegex.IsMatch(line))
            {
                if (current is not null && current.Count > 0)
                    blocks.Add(current);

                current = new List<string> { line };
                continue;
            }

            if (current is not null)
                current.Add(line);
        }

        if (current is not null && current.Count > 0)
            blocks.Add(current);

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
