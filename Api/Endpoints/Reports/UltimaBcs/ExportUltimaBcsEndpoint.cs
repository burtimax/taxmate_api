using Api.Http;
using Application.Dtos;
using Application.Services;
using FastEndpoints;

namespace Api.Endpoints.Reports.UltimaBcs;

public sealed class ExportUltimaBcsEndpoint : Endpoint<ExportRequest>
{
    private readonly UltimaBcsReportService _service;

    public ExportUltimaBcsEndpoint(UltimaBcsReportService service) => _service = service;

    public override void Configure()
    {
        Post("/api/reports/ultima-bcs/export");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ExportRequest req, CancellationToken ct)
    {
        var overrides = ReportDtoMapper.ToOverrides(req.ManualExpenses);
        var result = await _service.ExportExcelAsync(req.SessionId, overrides, req.Inn, ct);
        if (!result.IsSuccess || result.Value is null)
        {
            AddError(result.Error ?? "Ошибка экспорта");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        await FileDownload.SendAsync(
            HttpContext.Response,
            result.Value.Content,
            result.Value.FileName,
            result.Value.ContentType,
            ct);
    }
}
