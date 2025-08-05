using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add our custom ActivitySource to OpenTelemetry tracing
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => 
    {
        tracing.AddSource("Api.Services.ForecastCacheService");
    })
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("Api.NwsManager");
    });

builder.AddRedisOutputCache("cache");

builder.Services.AddHealthChecks()
	.AddUrlGroup(new Uri("https://nws"), "NWS Weather API", HealthStatus.Unhealthy,
		configureClient: (services, client) =>
		{
			client.DefaultRequestHeaders.Add("User-Agent", "Microsoft - .NET Aspire Demo");
		});

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddNwsManager();

// Add the background service for caching popular forecasts
builder.Services.AddSingleton<Api.Services.ForecastCacheService>();
builder.Services.AddHostedService<Api.Services.ForecastCacheService>(provider => 
    provider.GetRequiredService<Api.Services.ForecastCacheService>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapDefaultEndpoints();

// Map the endpoints for the API
app.MapApiEndpoints();

app.Run();
