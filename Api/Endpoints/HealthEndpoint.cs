using FastEndpoints;

namespace Api.Endpoints;

public sealed class HealthEndpoint : EndpointWithoutRequest<object>
{
    public override void Configure()
    {
        Get("/api/health");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
        => SendAsync(new { status = "ok", service = "TaxMate Report Parser API" }, cancellation: ct);
}
