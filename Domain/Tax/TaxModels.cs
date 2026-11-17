namespace Domain.Tax;

public enum OperationStatus
{
    Ok,
    NeedsManualExpense,
    Warning
}

public enum IncomeKind
{
    Fixing,
    DerivativeClose,
    Dividend,
    DividendTaxWithholding,
    Coupon,
    SecuritySale,
    SecurityPurchase,
    BrokerFee
}

public sealed class ManualExpenseOverride
{
    public string OperationId { get; set; } = "";
    public DateOnly? PurchaseDate { get; set; }
    public decimal? ExpenseAmountFx { get; set; }
    public string? Currency { get; set; }
    public decimal? FxRateOverride { get; set; }
    public string? Note { get; set; }
}

public sealed class TaxOperation
{
    public string Id { get; set; } = "";
    public IncomeKind Kind { get; set; }
    public string IncomeCode { get; set; } = "";
    public DateOnly IncomeDate { get; set; }
    public DateOnly? TaxWithheldDate { get; set; }
    public string Currency { get; set; } = "USD";
    public string CurrencyNumericCode { get; set; } = "840";
    public string Description { get; set; } = "";
    public string? TradeId { get; set; }
    public string? Isin { get; set; }
    public string? Ticker { get; set; }
    public string? Venue { get; set; }
    public decimal? Quantity { get; set; }

    public decimal IncomeAmountFx { get; set; }
    public decimal? ExpenseAmountFx { get; set; }
    public decimal? TaxWithheldFx { get; set; }
    public decimal? TaxRatePercent { get; set; }
    public decimal? CommissionFx { get; set; }

    public decimal? IncomeFxRate { get; set; }
    public decimal? ExpenseFxRate { get; set; }
    public DateOnly? ExpenseDate { get; set; }

    public decimal IncomeAmountRub { get; set; }
    public decimal ExpenseAmountRub { get; set; }
    public decimal CommissionAmountRub { get; set; }
    public decimal TaxWithheldRub { get; set; }
    public decimal FinancialResultRub { get; set; }

    public OperationStatus Status { get; set; } = OperationStatus.Ok;
    public string? StatusMessage { get; set; }
    public bool ExpenseIsManual { get; set; }
}

public sealed class TaxCodeSummary
{
    public string IncomeCode { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal IncomeRub { get; set; }
    public decimal ExpenseRub { get; set; }
    public decimal CommissionRub { get; set; }
    public decimal FinancialResultRub { get; set; }
    public decimal TaxWithheldRub { get; set; }
    public decimal TaxBaseRub { get; set; }
    public decimal CarryForwardLossRub { get; set; }
}

public sealed class DeclarationGroupSummary
{
    public string GroupCode { get; set; } = "";
    public string Title { get; set; } = "";
    public IReadOnlyList<string> IncomeCodes { get; set; } = [];
    public decimal IncomeRub { get; set; }
    public decimal ExpenseRub { get; set; }
    public decimal TaxBaseRub { get; set; }
    public decimal TaxWithheldRub { get; set; }
    public decimal Tax13Rub { get; set; }
    public decimal Tax15Rub { get; set; }
    public decimal TaxDueRub { get; set; }
}

public sealed class TaxCalculationResult
{
    public string ReportTypeId { get; set; } = "";
    public string? ClientName { get; set; }
    public string? Agreement { get; set; }
    public string? BrokerName { get; set; }
    public string? PeriodFrom { get; set; }
    public string? PeriodTo { get; set; }
    public int TaxYear { get; set; }
    public List<TaxOperation> Operations { get; set; } = [];
    public List<TaxCodeSummary> CodeSummaries { get; set; } = [];
    public List<DeclarationGroupSummary> DeclarationGroups { get; set; } = [];
    public List<string> Issues { get; set; } = [];
    public int NeedsManualExpenseCount => Operations.Count(o => o.Status == OperationStatus.NeedsManualExpense);
}
