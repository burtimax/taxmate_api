using System.Globalization;
using HtmlAgilityPack;

namespace TestConsole.DollarExchangeApi;

public sealed class CbrDollarRatesParser
{
    public IReadOnlyCollection<DollarRateItem> Parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var table = doc.DocumentNode.SelectSingleNode("//table[contains(@class,'data')]");
        if (table is null)
        {
            throw new InvalidOperationException("На странице не найдена таблица курсов table.data.");
        }

        var rows = table.SelectNodes("./tr") ?? new HtmlNodeCollection(table);
        var result = new List<DollarRateItem>();

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("./td");
            if (cells is null || cells.Count < 3)
            {
                continue;
            }

            var dateText = cells[0].InnerText.Trim();
            var unitsText = cells[1].InnerText.Trim();
            var rateText = cells[2].InnerText.Trim();

            if (!DateOnly.TryParseExact(dateText, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }

            if (!int.TryParse(unitsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var units))
            {
                continue;
            }

            var normalizedRate = rateText.Replace(" ", string.Empty).Replace(',', '.');
            if (!decimal.TryParse(normalizedRate, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate))
            {
                continue;
            }

            result.Add(new DollarRateItem
            {
                Date = date,
                Units = units,
                Rate = rate
            });
        }

        return result
            .OrderBy(x => x.Date)
            .ToArray();
    }
}
