using Domain.Tax;

namespace Application.Abstractions;

public sealed class DeclarationExportOptions
{
    public string? Inn { get; set; }
    public string? SurName { get; set; }
    public string? Name { get; set; }
    public string? Patronymic { get; set; }
    public string? Oktmo { get; set; }
    public string? InspectionNumber { get; set; }
    public string? SourceFileName { get; set; }
    public string? Agreement { get; set; }
}

public sealed class DeclarationExportResult
{
    public byte[] Content { get; set; } = [];
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/json";
}

public interface IDeclarationDeExporter
{
    DeclarationExportResult Export(TaxCalculationResult result, DeclarationExportOptions? options = null);
}
