using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

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
