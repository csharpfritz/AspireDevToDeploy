using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Memory;
using Api.Data;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api
{
    public class NwsManager(HttpClient httpClient, IMemoryCache cache, IWebHostEnvironment webHostEnvironment)
    {
        private static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        private static readonly Meter meter = new("Api.NwsManager");
        private static readonly Histogram<double> forecastRequestDuration = meter.CreateHistogram<double>(
            "nws_forecast_request_duration",
            "ms",
            "Duration of forecast requests in milliseconds");
        private static readonly Counter<long> forecastRequestCounter = meter.CreateCounter<long>(
            "nws_forecast_requests_total",
            "Total number of forecast requests");

        public async Task<Zone[]?> GetZonesAsync()
        {
            return await cache.GetOrCreateAsync("zones", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

                var zones = await httpClient.GetFromJsonAsync<ZonesResponse>("/zones?type=forecast", options);
                return zones?.Features
                           ?.Where(f => f.Properties?.ObservationStations?.Count > 0)
                           .Select(f => (Zone)f)
                           .Distinct()
                           .ToArray() ?? [];

            });
        }

        private static int forecastCount = 0;

        public async Task<Forecast[]> GetForecastByZoneAsync(string zoneId)
        {
            var stopwatch = Stopwatch.StartNew();
            var cacheHit = false;
            
            try
            {
                // Check if we have a cached forecast first
                var cacheKey = $"forecast_{zoneId}";
                if (cache.TryGetValue(cacheKey, out Forecast[]? cachedForecast) && cachedForecast != null)
                {
                    cacheHit = true;
                    return cachedForecast;
                }

                // Create an exception every 5 calls to simulate and error for testing
                forecastCount++;

                if (forecastCount % 5 == 0)
                {
                    throw new Exception("Random exception thrown by NwsManager.GetForecastAsync");
                }

                var zoneIdSegment = HttpUtility.UrlEncode(zoneId);
                var zoneUrl = $"/zones/forecast/{zoneIdSegment}/forecast";
                var forecasts = await httpClient.GetFromJsonAsync<ForecastResponse>(zoneUrl, options);
                var result = forecasts
                       ?.Properties
                       ?.Periods
                       ?.Select(p => (Forecast)p)
                       .ToArray() ?? [];

                // Cache the result for future requests
                cache.Set(cacheKey, result, TimeSpan.FromMinutes(15));
                
                return result;
            }
            finally
            {
                stopwatch.Stop();
                
                // Record metrics
                var tags = new TagList
                {
                    { "zone_id", zoneId },
                    { "cache_hit", cacheHit ? "true" : "false" }
                };
                
                forecastRequestDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
                forecastRequestCounter.Add(1, tags);
            }
        }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public static class NwsManagerExtensions
    {
        public static IServiceCollection AddNwsManager(this IServiceCollection services)
        {
            services.AddHttpClient<Api.NwsManager>(client =>
            {
                client.BaseAddress = new Uri("https://nws");
                client.DefaultRequestHeaders.Add("User-Agent", "Microsoft - .NET Aspire Demo");
            });

            services.AddMemoryCache();

            return services;
        }

        public static WebApplication? MapApiEndpoints(this WebApplication app)
        {
            app.UseOutputCache();

            app.MapGet("/zones", async (Api.NwsManager manager) =>
                {
                    var zones = await manager.GetZonesAsync();
                    return TypedResults.Ok(zones);
                })
                .CacheOutput(policy => policy.Expire(TimeSpan.FromHours(1)))
                .WithName("GetZones")
                .WithOpenApi();

            app.MapGet("/forecast/{zoneId}", async Task<Results<Ok<Api.Forecast[]>, NotFound>> (Api.NwsManager manager, string zoneId) =>
                {
                    try
                    {
                        var forecasts = await manager.GetForecastByZoneAsync(zoneId);
                        return TypedResults.Ok(forecasts);
                    }
                    catch (HttpRequestException)
                    {
                        return TypedResults.NotFound();
                    }
                })
                .CacheOutput(policy => policy.Expire(TimeSpan.FromMinutes(15)).SetVaryByRouteValue("zoneId"))
                .WithName("GetForecastByZone")
                .WithOpenApi();

            app.MapGet("/cache/status", (IMemoryCache cache) =>
                {
                    var popularZones = new[] { "DCZ001", "NYZ072", "PAZ071" };
                    var status = popularZones.Select(zoneId =>
                    {
                        var cacheKey = $"forecast_{zoneId}";
                        var isCached = cache.TryGetValue(cacheKey, out var cachedData);
                        return new { ZoneId = zoneId, IsCached = isCached, HasData = cachedData != null };
                    }).ToArray();
                    
                    return TypedResults.Ok(status);
                })
                .WithName("GetCacheStatus")
                .WithOpenApi();

            app.MapPost("/cache/refresh", async (Api.Services.ForecastCacheService cacheService) =>
                {
                    await cacheService.RefreshCacheAsync();
                    return TypedResults.Ok(new { Message = "Cache refresh triggered successfully" });
                })
                .WithName("RefreshCache")
                .WithOpenApi();

            return app;
        }
    }
}