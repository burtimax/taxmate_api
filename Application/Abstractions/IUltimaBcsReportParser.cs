using Domain.Reports.UltimaBcs;

namespace Application.Abstractions;

public interface IUltimaBcsReportParser
{
    bool CanParse(Stream pdfStream);
    UltimaBcsReportDocument Parse(Stream pdfStream, string? fileName = null);
}
