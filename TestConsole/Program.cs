using System.Text.Json;
using TestConsole.DollarExchangeApi;
using TestConsole.ReportParser;

var from = new DateOnly(2026, 1, 1);

var to = new DateOnly(2026, 4, 20);

using var httpClient = new HttpClient();
var service = new CbrDollarExchangeService(httpClient, new CbrDollarRatesParser());
var rates = await service.GetDollarRatesAsync(from, to);

ReportParser parser = new ReportParser();
var data = parser.Parse(
    "C:\\Users\\timof\\Documents\\Projects\\046_taxmate_report_parsing\\ReportParserApi\\TestConsole\\docs\\reports\\DataReport.pdf");
var datastr = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
//var outputPath = Path.Combine(AppContext.BaseDirectory, "usd-rates.json");
var json = JsonSerializer.Serialize(rates, new JsonSerializerOptions { WriteIndented = true });
//await File.WriteAllTextAsync(outputPath, json);

Console.WriteLine($"Диапазон: {from:dd.MM.yyyy} - {to:dd.MM.yyyy}");
Console.WriteLine($"Получено записей: {rates.Count}");
//Console.WriteLine($"Сохранено: {outputPath}");
