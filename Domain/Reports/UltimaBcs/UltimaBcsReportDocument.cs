namespace Domain.Reports.UltimaBcs;

public sealed class UltimaBcsReportDocument
{
    public string? SourceFile { get; set; }
    public string? PeriodFrom { get; set; }
    public string? PeriodTo { get; set; }
    public string? Client { get; set; }
    public string? Agreement { get; set; }
    public string BrokerName { get; set; } = "The Ultima World (DIFC) Limited";

    public List<CashTransfer> CashTransfers { get; set; } = [];
    public List<SecurityTransfer> SecurityTransfers { get; set; } = [];
    public List<OtcTrade> Trades { get; set; } = [];
}

public sealed class CashTransfer
{
    public string? Date { get; set; }
    public string? Location { get; set; }
    public string? Type { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? Details { get; set; }
}

public sealed class SecurityTransfer
{
    public string? TradeDate { get; set; }
    public string? TradeId { get; set; }
    public string? AssetType { get; set; }
    public string? Isin { get; set; }
    public string? Issuer { get; set; }
    public string? OperationType { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? Price { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Accrual { get; set; }
    public decimal? Commission { get; set; }
    public string? Currency { get; set; }
    public string? Venue { get; set; }
}

public sealed class OtcTrade
{
    public string? TradeId { get; set; }
    public string? TradeDate { get; set; }
    public string? PaymentDate { get; set; }
    public string? ProductType { get; set; }
    public string? ProductName { get; set; }
    public string? BuySell { get; set; }
    public string? Kind { get; set; }
    public decimal? Notional { get; set; }
    public decimal? ValueCoefficient { get; set; }
    public string? Currency { get; set; }
    public string? Venue { get; set; }
    public List<UnderlyingAsset> Underlyings { get; set; } = [];
}

public sealed class UnderlyingAsset
{
    public string? Ticker { get; set; }
    public string? Isin { get; set; }
    public string? Currency { get; set; }
    public decimal? InitialPrice { get; set; }
}
