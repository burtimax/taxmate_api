using Application.Dtos;
using Application.Services;
using FastEndpoints;

namespace Api.Endpoints.Reports.UltimaBcs;

public sealed class ParseUltimaBcsRequest
{
    public IFormFile File { get; set; } = null!;
}

public sealed class ParseUltimaBcsEndpoint : Endpoint<ParseUltimaBcsRequest, ParseReportResponse>
{
    private readonly UltimaBcsReportService _service;

    public ParseUltimaBcsEndpoint(UltimaBcsReportService service) => _service = service;

    public override void Configure()
    {
        Post("/api/reports/ultima-bcs/parse");
        AllowAnonymous();
        AllowFileUploads();
    }

    public override async Task HandleAsync(ParseUltimaBcsRequest req, CancellationToken ct)
    {
        if (req.File is null || req.File.Length == 0)
        {
            AddError("Файл PDF не передан");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        await using var stream = req.File.OpenReadStream();
        var result = await _service.ParseAsync(stream, req.File.FileName, ct);
        if (!result.IsSuccess || result.Value is null)
        {
            AddError(result.Error ?? "Ошибка обработки отчёта");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        await SendAsync(ReportDtoMapper.FromSession(result.Value), cancellation: ct);
    }
}
