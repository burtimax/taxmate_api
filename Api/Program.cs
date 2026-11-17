using System.Text.Json;
using Application.Extensions;
using FastEndpoints;
using FastEndpoints.Swagger;
using Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFastEndpoints()
    .SwaggerDocument(o =>
    {
        o.DocumentSettings = s =>
        {
            s.Title = "TaxMate Report Parser API";
            s.Version = "v1";
        };
    });

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddCors();

var app = builder.Build();

app.UseCors(policy =>
{
    if (app.Environment.IsDevelopment())
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    }
    else
    {
        var origins = app.Configuration["AllowedOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                      ?? ["http://localhost:5173"];
        policy.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader();
    }
});

app.UseFastEndpoints(c =>
{
    c.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    c.Serializer.Options.PropertyNameCaseInsensitive = true;
}).UseSwaggerGen();

app.Run();
