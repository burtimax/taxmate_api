namespace Application.Abstractions;

public interface IFxRateProvider
{
    Task EnsureRatesAsync(IEnumerable<(string Currency, DateOnly Date)> requests, CancellationToken ct = default);
    decimal? GetRate(string currency, DateOnly date);
}
