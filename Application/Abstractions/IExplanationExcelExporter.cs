using Domain.Tax;

namespace Application.Abstractions;

public interface IExplanationExcelExporter
{
    byte[] Export(TaxCalculationResult result, string? inn = null);
}
