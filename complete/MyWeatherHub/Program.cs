using MyWeatherHub;
using MyWeatherHub.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add our custom ActivitySource and Meter for ForecastSummarizer to OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => 
    {
        tracing.AddSource("MyWeatherHub.ForecastSummarizer");
    })
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("MyWeatherHub.ForecastSummarizer");
    });

builder.AddAzureChatCompletionsClient("ai-model")
    .AddChatClient();

builder.Services.AddTransient<ForecastSummarizer>();


// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient<NwsManager>(c =>
{
    var url = "https+http://api";

    c.BaseAddress = new(url);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapDefaultEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
