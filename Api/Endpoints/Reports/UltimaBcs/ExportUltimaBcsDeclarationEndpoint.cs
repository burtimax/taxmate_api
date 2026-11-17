using Api.Http;
using Application.Abstractions;
using Application.Dtos;
using Application.Services;
using FastEndpoints;

namespace Api.Endpoints.Reports.UltimaBcs;

public sealed class ExportUltimaBcsDeclarationEndpoint : Endpoint<ExportDeclarationRequest>
{
    private readonly UltimaBcsReportService _service;

    public ExportUltimaBcsDeclarationEndpoint(UltimaBcsReportService service) => _service = service;

    public override void Configure()
    {
        Post("/api/reports/ultima-bcs/export-declaration");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ExportDeclarationRequest req, CancellationToken ct)
    {
        var overrides = ReportDtoMapper.ToOverrides(req.ManualExpenses);
        var options = new DeclarationExportOptions
        {
            Inn = req.Inn,
            SurName = req.SurName,
            Name = req.Name,
            Patronymic = req.Patronymic,
            Oktmo = req.Oktmo,
            InspectionNumber = req.InspectionNumber
        };

        var result = await _service.ExportDeclarationAsync(req.SessionId, overrides, options, ct);
        if (!result.IsSuccess || result.Value is null)
        {
            AddError(result.Error ?? "Ошибка экспорта декларации");
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
