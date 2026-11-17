using Application.Abstractions;
using Application.Common;
using Application.Tax;
using Domain.Reports;
using Domain.Tax;
using Shared.Contracts;

namespace Application.Services;

public sealed class UltimaBcsReportService
{
    private readonly IUltimaBcsReportParser _parser;
    private readonly UltimaBcsTaxCalculator _calculator;
    private readonly IFxRateProvider _fx;
    private readonly IReportSessionStore _sessions;
    private readonly IExplanationExcelExporter _excel;
    private readonly IDeclarationDeExporter _declaration;

    public UltimaBcsReportService(
        IUltimaBcsReportParser parser,
        UltimaBcsTaxCalculator calculator,
        IFxRateProvider fx,
        IReportSessionStore sessions,
        IExplanationExcelExporter excel,
        IDeclarationDeExporter declaration)
    {
        _parser = parser;
        _calculator = calculator;
        _fx = fx;
        _sessions = sessions;
        _excel = excel;
        _declaration = declaration;
    }

    public async Task<Result<ReportSession>> ParseAsync(Stream pdfStream, string? fileName, CancellationToken ct = default)
    {
        try
        {
            if (!_parser.CanParse(pdfStream))
                return Result.Failure<ReportSession>("Файл не похож на отчёт Ultima/BCS");

            pdfStream.Position = 0;
            var document = _parser.Parse(pdfStream, fileName);
            var calculation = await _calculator.CalculateAsync(document, _fx, null, ct);

            var session = new ReportSession
            {
                SessionId = Guid.NewGuid().ToString("N"),
                ReportTypeId = ReportTypeIds.UltimaBcs,
                FileName = fileName,
                Document = document,
                Calculation = calculation,
                ManualExpenses = []
            };
            _sessions.Save(session);
            return Result.Success(session);
        }
        catch (Exception ex)
        {
            return Result.Failure<ReportSession>($"Ошибка парсинга: {ex.Message}");
        }
    }

    public async Task<Result<ReportSession>> RecalculateAsync(
        string sessionId,
        IReadOnlyList<ManualExpenseOverride> manualExpenses,
        CancellationToken ct = default)
    {
        var session = _sessions.Get(sessionId);
        if (session is null)
            return Result.Failure<ReportSession>("Сессия не найдена или истекла. Загрузите отчёт заново.");

        session.ManualExpenses = manualExpenses.ToList();
        session.Calculation = await _calculator.CalculateAsync(session.Document, _fx, session.ManualExpenses, ct);
        _sessions.Save(session);
        return Result.Success(session);
    }

    public async Task<Result<DeclarationExportResult>> ExportExcelAsync(
        string sessionId,
        IReadOnlyList<ManualExpenseOverride>? manualExpenses,
        string? inn,
        CancellationToken ct = default)
    {
        var session = _sessions.Get(sessionId);
        if (session is null)
            return Result.Failure<DeclarationExportResult>("Сессия не найдена или истекла. Загрузите отчёт заново.");

        if (manualExpenses is { Count: > 0 })
        {
            session.ManualExpenses = manualExpenses.ToList();
            session.Calculation = await _calculator.CalculateAsync(session.Document, _fx, session.ManualExpenses, ct);
            _sessions.Save(session);
        }

        var bytes = _excel.Export(session.Calculation, inn);
        return Result.Success(new DeclarationExportResult
        {
            Content = bytes,
            FileName = ExportFileNaming.ExcelFileName(
                session.Calculation.Agreement,
                session.FileName,
                session.Calculation.TaxYear),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        });
    }

    public async Task<Result<DeclarationExportResult>> ExportDeclarationAsync(
        string sessionId,
        IReadOnlyList<ManualExpenseOverride>? manualExpenses,
        DeclarationExportOptions options,
        CancellationToken ct = default)
    {
        var session = _sessions.Get(sessionId);
        if (session is null)
            return Result.Failure<DeclarationExportResult>("Сессия не найдена или истекла. Загрузите отчёт заново.");

        if (manualExpenses is { Count: > 0 })
        {
            session.ManualExpenses = manualExpenses.ToList();
            session.Calculation = await _calculator.CalculateAsync(session.Document, _fx, session.ManualExpenses, ct);
            _sessions.Save(session);
        }

        options.SourceFileName ??= session.FileName;
        options.Agreement ??= session.Calculation.Agreement;
        var exported = _declaration.Export(session.Calculation, options);
        return Result.Success(exported);
    }
}
