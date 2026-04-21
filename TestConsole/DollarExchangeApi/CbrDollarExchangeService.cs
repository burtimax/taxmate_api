using System.Globalization;

namespace TestConsole.DollarExchangeApi;

public sealed class CbrDollarExchangeService
{
    private const string CurrencyCodeUsd = "R01235";
    private readonly HttpClient _httpClient;
    private readonly CbrDollarRatesParser _parser;

    public CbrDollarExchangeService(HttpClient httpClient, CbrDollarRatesParser parser)
    {
        _httpClient = httpClient;
        _parser = parser;
    }

    public async Task<IReadOnlyCollection<DollarRateItem>> GetDollarRatesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (from > to)
        {
            throw new ArgumentException("Дата 'from' должна быть меньше или равна 'to'.");
        }

        var query = BuildQuery(from, to);
        var url = $"https://cbr.ru/currency_base/dynamics/?{query}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        request.Headers.TryAddWithoutValidation(
            "user-agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return _parser.Parse(html);
    }

    private static string BuildQuery(DateOnly from, DateOnly to)
    {
        var parameters = new Dictionary<string, string>
        {
            ["UniDbQuery.Posted"] = "True",
            ["UniDbQuery.so"] = "1",
            ["UniDbQuery.mode"] = "1",
            ["UniDbQuery.date_req1"] = string.Empty,
            ["UniDbQuery.date_req2"] = string.Empty,
            ["UniDbQuery.VAL_NM_RQ"] = CurrencyCodeUsd,
            ["UniDbQuery.From"] = from.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            ["UniDbQuery.To"] = to.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
        };

        return string.Join("&", parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
    }
}
