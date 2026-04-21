namespace TestConsole.DollarExchangeApi;

public sealed class DollarRateItem
{
    public DateOnly Date { get; init; }
    public decimal Rate { get; init; }
    public int Units { get; init; }
}
