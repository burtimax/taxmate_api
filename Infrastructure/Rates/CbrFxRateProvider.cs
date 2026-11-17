using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Rates;

public sealed class CbrFxRateProvider : IFxRateProvider
{
    private static readonly Dictionary<string, string> CbrCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = "R01235",
        ["EUR"] = "R01239",
        ["GBP"] = "R01035",
        ["CHF"] = "R01775",
        ["CNY"] = "R01375",
        ["HKD"] = "R01200",
    };

    private readonly HttpClient _http;
    private readonly ILogger<CbrFxRateProvider> _logger;
    private readonly ConcurrentDictionary<string, decimal> _cache = new(StringComparer.OrdinalIgnoreCase);

    static CbrFxRateProvider()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public CbrFxRateProvider(HttpClient http, ILogger<CbrFxRateProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task EnsureRatesAsync(IEnumerable<(string Currency, DateOnly Date)> requests, CancellationToken ct = default)
    {
        var grouped = requests
            .Where(r => !string.Equals(r.Currency, "RUB", StringComparison.OrdinalIgnoreCase))
            .Where(r => CbrCodes.ContainsKey(r.Currency))
            .GroupBy(r => r.Currency.ToUpperInvariant());

        foreach (var currencyGroup in grouped)
        {
            var currency = currencyGroup.Key;
            var dates = currencyGroup.Select(x => x.Date).Distinct().OrderBy(x => x).ToList();
            if (dates.Count == 0) continue;

            var missing = dates.Where(d => !_cache.ContainsKey(Key(currency, d))).ToList();
            if (missing.Count == 0) continue;

            var from = missing.Min().AddDays(-14);
            var to = missing.Max().AddDays(3);

            // CBR лучше отвечает на ограниченных окнах
            for (var cursor = from; cursor <= to; cursor = cursor.AddDays(90))
            {
                var windowTo = cursor.AddDays(89);
                if (windowTo > to) windowTo = to;
                await LoadRangeAsync(currency, cursor, windowTo, ct);
            }

            foreach (var date in missing)
            {
                if (_cache.ContainsKey(Key(currency, date))) continue;

                var filled = false;
                for (var back = 1; back <= 14; back++)
                {
                    var prev = date.AddDays(-back);
                    if (_cache.TryGetValue(Key(currency, prev), out var rate))
                    {
                        _cache[Key(currency, date)] = rate;
                        filled = true;
                        break;
                    }
                }

                if (!filled)
                {
                    // точечная догрузка вокруг даты
                    await LoadRangeAsync(currency, date.AddDays(-14), date.AddDays(3), ct);
                    for (var back = 0; back <= 14; back++)
                    {
                        var prev = date.AddDays(-back);
                        if (_cache.TryGetValue(Key(currency, prev), out var rate))
                        {
                            _cache[Key(currency, date)] = rate;
                            break;
                        }
                    }
                }
            }
        }
    }

    public decimal? GetRate(string currency, DateOnly date)
    {
        if (string.Equals(currency, "RUB", StringComparison.OrdinalIgnoreCase))
            return 1m;

        return _cache.TryGetValue(Key(currency, date), out var rate) ? rate : null;
    }

    private async Task LoadRangeAsync(string currency, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (!CbrCodes.TryGetValue(currency, out var cbrCode))
            return;

        var url =
            $"https://www.cbr.ru/scripts/XML_dynamic.asp?date_req1={from:dd/MM/yyyy}&date_req2={to:dd/MM/yyyy}&VAL_NM_RQ={cbrCode}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", "TaxMateParser/1.0");
            using var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            var xml = Encoding.GetEncoding("windows-1251").GetString(bytes);
            var doc = XDocument.Parse(xml);
            var loaded = 0;

            foreach (var record in doc.Descendants("Record"))
            {
                var dateAttr = record.Attribute("Date")?.Value;
                var valueText = record.Element("Value")?.Value;
                var nominalText = record.Element("Nominal")?.Value ?? "1";
                if (dateAttr is null || valueText is null) continue;

                if (!DateOnly.TryParseExact(dateAttr, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    continue;

                var normalized = valueText.Replace(',', '.');
                if (!decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                    continue;
                if (!int.TryParse(nominalText, out var nominal) || nominal <= 0)
                    nominal = 1;

                _cache[Key(currency, date)] = value / nominal;
                loaded++;
            }

            _logger.LogInformation("ЦБ {Currency}: загружено {Count} курсов за {From}-{To}", currency, loaded, from, to);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось загрузить курсы ЦБ для {Currency} {From}-{To}", currency, from, to);
        }
    }

    private static string Key(string currency, DateOnly date)
        => $"{currency.ToUpperInvariant()}|{date:yyyy-MM-dd}";
}
