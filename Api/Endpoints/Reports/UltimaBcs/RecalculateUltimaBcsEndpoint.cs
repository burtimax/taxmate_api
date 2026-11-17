using Application.Dtos;
using Application.Services;
using FastEndpoints;

namespace Api.Endpoints.Reports.UltimaBcs;

public sealed class RecalculateUltimaBcsEndpoint : Endpoint<RecalculateRequest, ParseReportResponse>
{
    private readonly UltimaBcsReportService _service;

    public RecalculateUltimaBcsEndpoint(UltimaBcsReportService service) => _service = service;

    public override void Configure()
    {
        Post("/api/reports/ultima-bcs/recalculate");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RecalculateRequest req, CancellationToken ct)
    {
        var overrides = ReportDtoMapper.ToOverrides(req.ManualExpenses);
        var result = await _service.RecalculateAsync(req.SessionId, overrides, ct);
        if (!result.IsSuccess || result.Value is null)
        {
            AddError(result.Error ?? "Ошибка пересчёта");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        await SendAsync(ReportDtoMapper.FromSession(result.Value), cancellation: ct);
    }
}
