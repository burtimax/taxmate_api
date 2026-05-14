namespace TestConsole.ReportParser;

public sealed class ReportDocument
{
    public string? SourceFile { get; set; }
    public string? PeriodFrom { get; set; }
    public string? PeriodTo { get; set; }
    public string? Client { get; set; }
    public string? Agreement { get; set; }

    public List<BalanceBlock> ConsolidatedCashFlow { get; set; } = new();
    public List<BalanceBlock> CashFlow { get; set; } = new();

    public List<TransferTransaction> Transfers { get; set; } = new();
    public List<Trade> Trades { get; set; } = new();
    public List<OutstandingPosition> OutstandingPositions { get; set; } = new();
    public List<TransactionDetailsBlock> TransactionDetails { get; set; } = new();

    public string? RawText { get; set; }
}

public sealed class BalanceBlock
{
    public string? Location { get; set; }
    public decimal? IncomingBalance { get; set; }
    public decimal? Credit { get; set; }
    public decimal? Debit { get; set; }
    public decimal? OutgoingBalance { get; set; }
    public string? Currency { get; set; }
}

public sealed class TransferTransaction
{
    public string? Date { get; set; }
    public string? Location { get; set; }
    public string? Type { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? Details { get; set; }
}

public sealed class UnderlyingAsset
{
    public string? Ticker { get; set; }
    public string? Isin { get; set; }
    public string? Currency { get; set; }
    public decimal? InitialPrice { get; set; }
}

public sealed class Trade
{
    public string? TradeId { get; set; }
    public string? TradeDate { get; set; }
    public string? PaymentDate { get; set; }
    public string? ProductType { get; set; }
    public string? ProductName { get; set; }
    public string? RiskProfile { get; set; }
    public string? MaturityDate { get; set; }
    public string? BuySell { get; set; }
    public string? Kind { get; set; }
    public decimal? Notional { get; set; }
    public decimal? ValueCoefficient { get; set; }
    public string? Currency { get; set; }
    public string? Venue { get; set; }
    public List<UnderlyingAsset> Underlyings { get; set; } = new();
    public string? RawBlock { get; set; }
}

public sealed class OutstandingPosition
{
    public string TradeId { get; set; } = "";
    public string? TradeDate { get; set; }
    public string? PaymentDate { get; set; }
    public string? ProductType { get; set; }
    public string? ProductName { get; set; }
    public string? RiskProfile { get; set; }
    public string? MaturityDate { get; set; }
    public string? BuySell { get; set; }

    public string? TransactionDetailsReference { get; set; }
    public decimal? CurrentMarketValuation { get; set; }
    public string? CurrentMarketValuationCurrency { get; set; }
    public string? ValuationDate { get; set; }
    public decimal? Notional { get; set; }
    public decimal? ValueCoefficient { get; set; }
    public string? Currency { get; set; }
    public string? Venue { get; set; }

    public List<UnderlyingAsset> Underlyings { get; set; } = new();
    public string? RawBlock { get; set; }
}

public sealed class TransactionDetailsBlock
{
    public string? TradeId { get; set; }
    public List<UnderlyingRef> Underlyings { get; set; } = new();
    public List<decimal> InitialPrices { get; set; } = new();
    public List<DetailScheduleRow> ScheduleRows { get; set; } = new();
    public string? RawBlock { get; set; }
}

public sealed class UnderlyingRef
{
    public string? Ticker { get; set; }
    public string? Isin { get; set; }
}

public sealed class DetailScheduleRow
{
    public string? EventDate { get; set; }
    public string? RawValues { get; set; }
}
