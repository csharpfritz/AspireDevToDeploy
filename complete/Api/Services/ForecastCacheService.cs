using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;

namespace Api.Services
{
    public class ForecastCacheService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ForecastCacheService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(15);
        
        // Activity source for OpenTelemetry tracing
        private static readonly ActivitySource ActivitySource = new("Api.Services.ForecastCacheService");
        
        // Popular locations to pre-cache
        private readonly string[] _popularZones = new[]
        {
            "DCZ001",  // Washington DC
            "NYZ072",  // New York (Manhattan)  
            "PAZ071"   // Philadelphia
        };

        public ForecastCacheService(IServiceProvider serviceProvider, ILogger<ForecastCacheService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Forecast Cache Service started. Will run every {Interval} minutes.", _interval.TotalMinutes);
            
            while (!stoppingToken.IsCancellationRequested)
            {
                await CacheForecastsAsync(stoppingToken);
                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task CacheForecastsAsync(CancellationToken cancellationToken)
        {
            using var activity = ActivitySource.StartActivity("ForecastCacheService.CacheForecastsAsync");
            activity?.SetTag("service", "ForecastCacheService");
            activity?.SetTag("operation", "cache_popular_forecasts");
            activity?.SetTag("zone_count", _popularZones.Length);
            
            _logger.LogInformation("Starting forecast cache update for {ZoneCount} popular zones.", _popularZones.Length);
            
            using var scope = _serviceProvider.CreateScope();
            var nwsManager = scope.ServiceProvider.GetRequiredService<NwsManager>();
            var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();

            var tasks = _popularZones.Select(async zoneId =>
            {
                using var zoneActivity = ActivitySource.StartActivity($"ForecastCacheService.CacheZone.{zoneId}");
                zoneActivity?.SetTag("zone_id", zoneId);
                zoneActivity?.SetTag("operation", "cache_zone_forecast");
                
                try
                {
                    _logger.LogDebug("Caching forecast for zone {ZoneId}", zoneId);
                    
                    // Pre-cache the forecast with a longer expiration
                    var cacheKey = $"forecast_{zoneId}";
                    var forecast = await nwsManager.GetForecastByZoneAsync(zoneId);
                    
                    cache.Set(cacheKey, forecast, TimeSpan.FromMinutes(15));
                    
                    zoneActivity?.SetTag("forecast_periods", forecast.Length);
                    zoneActivity?.SetTag("status", "success");
                    
                    _logger.LogDebug("Successfully cached forecast for zone {ZoneId} with {ForecastCount} periods", 
                        zoneId, forecast.Length);
                }
                catch (Exception ex)
                {
                    zoneActivity?.SetTag("status", "error");
                    zoneActivity?.SetTag("error_message", ex.Message);
                    zoneActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    
                    _logger.LogWarning(ex, "Failed to cache forecast for zone {ZoneId}", zoneId);
                }
            });

            await Task.WhenAll(tasks);
            
            activity?.SetTag("status", "completed");
            _logger.LogInformation("Completed forecast cache update.");
        }

        /// <summary>
        /// Manually trigger a cache refresh (for testing purposes)
        /// </summary>
        public async Task RefreshCacheAsync()
        {
            await CacheForecastsAsync(CancellationToken.None);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Forecast Cache Service is stopping.");
            await base.StopAsync(cancellationToken);
        }
    }
}
