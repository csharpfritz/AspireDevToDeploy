using Microsoft.Extensions.AI;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MyWeatherHub;

public class ForecastSummarizer(IChatClient chatClient, ILogger<ForecastSummarizer> logger)
{
	private static readonly ActivitySource ActivitySource = new("MyWeatherHub.ForecastSummarizer");
	private static readonly Meter Meter = new("MyWeatherHub.ForecastSummarizer");
	
	// Histogram to track request duration
	private static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
		name: "forecast_summarizer_request_duration",
		unit: "ms",
		description: "Duration of forecast summarization requests to the chat client");
	
	// Counter to track token usage
	private static readonly Counter<long> TokensUsed = Meter.CreateCounter<long>(
		name: "forecast_summarizer_tokens_used",
		unit: "tokens",
		description: "Number of tokens used in forecast summarization requests");

	public async Task<string> SummarizeForecastAsync(string forecasts)
	{
		using var activity = ActivitySource.StartActivity("ForecastSummarizer.SummarizeForecastAsync");
		using var scope = logger.BeginScope("SummarizeForecast");
		
		var stopwatch = Stopwatch.StartNew();
		
		activity?.SetTag("forecast.input_length", forecasts.Length);
		activity?.SetTag("operation", "summarize_forecast");
		
		logger.LogInformation("Starting forecast summarization for forecasts: {Forecasts}", forecasts);
		
		var prompt = $"""
			You are a weather assistant. Summarize the following forecast 
			as one of the following conditions: Sunny, Cloudy, Rainy, Snowy.  
			Only those four values are allowed. Be as concise as possible.  
			I want a 1-word response with one of these options: Sunny, Cloudy, Rainy, Snowy.

			The forecast is: {forecasts}
			""";

		activity?.SetTag("ai.prompt_length", prompt.Length);
		logger.LogDebug("Sending prompt to AI: {Prompt}", prompt);

		var response = await chatClient.GetResponseAsync(prompt);
		
		stopwatch.Stop();
		var durationMs = stopwatch.Elapsed.TotalMilliseconds;

		activity?.SetTag("ai.response_received", !string.IsNullOrEmpty(response.Text));
		activity?.SetTag("ai.request_duration_ms", durationMs);
		logger.LogDebug("AI response received: {Response}", response.Text);

		// Look for one of the four values in the response
		if (string.IsNullOrEmpty(response.Text))
		{
			activity?.SetTag("forecast.condition", "Cloudy");
			activity?.SetTag("forecast.fallback_used", true);
			activity?.SetStatus(ActivityStatusCode.Error, "AI response was null or empty");
			
			// Record metrics for error case
			var errorTags = new TagList
			{
				{ "weather_condition", "Cloudy" },
				{ "fallback_used", "true" }
			};
			RequestDuration.Record(durationMs, errorTags);
			
			logger.LogWarning("AI response was null or empty, using default fallback: Cloudy");
			return "Cloudy"; // Default fallback
		}

		var condition = response.Text switch
		{
			string s when s.Contains("Snowy", StringComparison.OrdinalIgnoreCase) => "Snowy",
			string s when s.Contains("Rainy", StringComparison.OrdinalIgnoreCase) => "Rainy", 
			string s when s.Contains("Cloudy", StringComparison.OrdinalIgnoreCase) => "Cloudy",
			string s when s.Contains("Sunny", StringComparison.OrdinalIgnoreCase) => "Sunny",
			string s when s.Contains("Clear", StringComparison.OrdinalIgnoreCase) => "Sunny",
			_ => "Cloudy" // Default fallback
		};

		activity?.SetTag("forecast.condition", condition);
		activity?.SetTag("forecast.fallback_used", false);
		activity?.SetStatus(ActivityStatusCode.Ok);
		
		// Record metrics
		var tags = new TagList
		{
			{ "weather_condition", condition },
			{ "fallback_used", "false" }
		};
		
		RequestDuration.Record(durationMs, tags);
		
		// Record token usage if available from the response
		if (response.Usage != null)
		{
			var totalTokens = (response.Usage.InputTokenCount ?? 0) + (response.Usage.OutputTokenCount ?? 0);
			if (totalTokens > 0)
			{
				activity?.SetTag("ai.tokens_used", totalTokens);
				TokensUsed.Add(totalTokens, tags);
				logger.LogDebug("Token usage - Input: {InputTokens}, Output: {OutputTokens}, Total: {TotalTokens}", 
					response.Usage.InputTokenCount, response.Usage.OutputTokenCount, totalTokens);
			}
		}
		
		logger.LogInformation("Forecast summarization completed. Condition determined: {WeatherCondition}, Duration: {Duration}ms", 
			condition, durationMs);

		return condition;
	}
}
