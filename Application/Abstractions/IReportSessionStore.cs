using Domain.Reports.UltimaBcs;
using Domain.Tax;

namespace Application.Abstractions;

public sealed class ReportSession
{
    public string SessionId { get; set; } = "";
    public string ReportTypeId { get; set; } = "";
    public string? FileName { get; set; }
    public UltimaBcsReportDocument Document { get; set; } = new();
    public TaxCalculationResult Calculation { get; set; } = new();
    public List<ManualExpenseOverride> ManualExpenses { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public interface IReportSessionStore
{
    void Save(ReportSession session);
    ReportSession? Get(string sessionId);
}
