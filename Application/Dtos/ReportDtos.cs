using Domain.Tax;

namespace Application.Dtos;

public sealed class ParseReportResponse
{
    public string SessionId { get; set; } = "";
    public string ReportTypeId { get; set; } = "";
    public string? FileName { get; set; }
    public ReportMetaDto Meta { get; set; } = new();
    public List<TaxOperationDto> Operations { get; set; } = [];
    public List<TaxCodeSummaryDto> Summary { get; set; } = [];
    public List<DeclarationGroupDto> DeclarationGroups { get; set; } = [];
    public List<string> Issues { get; set; } = [];
    public int NeedsManualExpenseCount { get; set; }
}

public sealed class ReportMetaDto
{
    public string? ClientName { get; set; }
    public string? Agreement { get; set; }
    public string? BrokerName { get; set; }
    public string? PeriodFrom { get; set; }
    public string? PeriodTo { get; set; }
    public int TaxYear { get; set; }
}

public sealed class TaxOperationDto
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string IncomeCode { get; set; } = "";
    public string IncomeDate { get; set; } = "";
    public string Currency { get; set; } = "";
    public string CurrencyNumericCode { get; set; } = "";
    public string Description { get; set; } = "";
    public string? TradeId { get; set; }
    public string? Isin { get; set; }
    public string? Venue { get; set; }
    public decimal? Quantity { get; set; }
    public decimal IncomeAmountFx { get; set; }
    public decimal? ExpenseAmountFx { get; set; }
    public decimal? TaxWithheldFx { get; set; }
    public decimal? TaxRatePercent { get; set; }
    public decimal? IncomeFxRate { get; set; }
    public decimal? ExpenseFxRate { get; set; }
    public string? ExpenseDate { get; set; }
    public decimal IncomeAmountRub { get; set; }
    public decimal ExpenseAmountRub { get; set; }
    public decimal CommissionAmountRub { get; set; }
    public decimal TaxWithheldRub { get; set; }
    public decimal FinancialResultRub { get; set; }
    public string Status { get; set; } = "";
    public string? StatusMessage { get; set; }
    public bool ExpenseIsManual { get; set; }
}

public sealed class TaxCodeSummaryDto
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

public sealed class DeclarationGroupDto
{
    public string GroupCode { get; set; } = "";
    public string Title { get; set; } = "";
    public decimal IncomeRub { get; set; }
    public decimal ExpenseRub { get; set; }
    public decimal TaxBaseRub { get; set; }
    public decimal TaxWithheldRub { get; set; }
    public decimal Tax13Rub { get; set; }
    public decimal Tax15Rub { get; set; }
    public decimal TaxDueRub { get; set; }
}

public sealed class RecalculateRequest
{
    public string SessionId { get; set; } = "";
    public List<ManualExpenseDto> ManualExpenses { get; set; } = [];
}

public sealed class ExportRequest
{
    public string SessionId { get; set; } = "";
    public string? Inn { get; set; }
    public List<ManualExpenseDto> ManualExpenses { get; set; } = [];
}

public sealed class ExportDeclarationRequest
{
    public string SessionId { get; set; } = "";
    public string? Inn { get; set; }
    public string? SurName { get; set; }
    public string? Name { get; set; }
    public string? Patronymic { get; set; }
    public string? Oktmo { get; set; }
    public string? InspectionNumber { get; set; }
    public List<ManualExpenseDto> ManualExpenses { get; set; } = [];
}

public sealed class ManualExpenseDto
{
    public string OperationId { get; set; } = "";
    public string? PurchaseDate { get; set; }
    public decimal? ExpenseAmountFx { get; set; }
    public string? Currency { get; set; }
    public decimal? FxRateOverride { get; set; }
    public string? Note { get; set; }
}

public static class ReportDtoMapper
{
    public static ParseReportResponse FromSession(Abstractions.ReportSession session)
    {
        var c = session.Calculation;
        return new ParseReportResponse
        {
            SessionId = session.SessionId,
            ReportTypeId = session.ReportTypeId,
            FileName = session.FileName,
            Meta = new ReportMetaDto
            {
                ClientName = c.ClientName,
                Agreement = c.Agreement,
                BrokerName = c.BrokerName,
                PeriodFrom = c.PeriodFrom,
                PeriodTo = c.PeriodTo,
                TaxYear = c.TaxYear
            },
            Operations = c.Operations.Select(MapOp).ToList(),
            Summary = c.CodeSummaries.Select(s => new TaxCodeSummaryDto
            {
                IncomeCode = s.IncomeCode,
                Description = s.Description,
                IncomeRub = s.IncomeRub,
                ExpenseRub = s.ExpenseRub,
                CommissionRub = s.CommissionRub,
                FinancialResultRub = s.FinancialResultRub,
                TaxWithheldRub = s.TaxWithheldRub,
                TaxBaseRub = s.TaxBaseRub,
                CarryForwardLossRub = s.CarryForwardLossRub
            }).ToList(),
            DeclarationGroups = c.DeclarationGroups.Select(g => new DeclarationGroupDto
            {
                GroupCode = g.GroupCode,
                Title = g.Title,
                IncomeRub = g.IncomeRub,
                ExpenseRub = g.ExpenseRub,
                TaxBaseRub = g.TaxBaseRub,
                TaxWithheldRub = g.TaxWithheldRub,
                Tax13Rub = g.Tax13Rub,
                Tax15Rub = g.Tax15Rub,
                TaxDueRub = g.TaxDueRub
            }).ToList(),
            Issues = c.Issues,
            NeedsManualExpenseCount = c.NeedsManualExpenseCount
        };
    }

    public static List<ManualExpenseOverride> ToOverrides(IEnumerable<ManualExpenseDto> dtos)
        => dtos.Select(d => new ManualExpenseOverride
        {
            OperationId = d.OperationId,
            PurchaseDate = Common.DateParsing.ParseRu(d.PurchaseDate),
            ExpenseAmountFx = d.ExpenseAmountFx,
            Currency = d.Currency,
            FxRateOverride = d.FxRateOverride,
            Note = d.Note
        }).ToList();

    private static TaxOperationDto MapOp(TaxOperation o) => new()
    {
        Id = o.Id,
        Kind = o.Kind.ToString(),
        IncomeCode = o.IncomeCode,
        IncomeDate = Common.DateParsing.FormatRu(o.IncomeDate),
        Currency = o.Currency,
        CurrencyNumericCode = o.CurrencyNumericCode,
        Description = o.Description,
        TradeId = o.TradeId,
        Isin = o.Isin,
        Venue = o.Venue,
        Quantity = o.Quantity,
        IncomeAmountFx = o.IncomeAmountFx,
        ExpenseAmountFx = o.ExpenseAmountFx,
        TaxWithheldFx = o.TaxWithheldFx,
        TaxRatePercent = o.TaxRatePercent,
        IncomeFxRate = o.IncomeFxRate,
        ExpenseFxRate = o.ExpenseFxRate,
        ExpenseDate = o.ExpenseDate is null ? null : Common.DateParsing.FormatRu(o.ExpenseDate.Value),
        IncomeAmountRub = o.IncomeAmountRub,
        ExpenseAmountRub = o.ExpenseAmountRub,
        CommissionAmountRub = o.CommissionAmountRub,
        TaxWithheldRub = o.TaxWithheldRub,
        FinancialResultRub = o.FinancialResultRub,
        Status = o.Status.ToString(),
        StatusMessage = o.StatusMessage,
        ExpenseIsManual = o.ExpenseIsManual
    };
}
