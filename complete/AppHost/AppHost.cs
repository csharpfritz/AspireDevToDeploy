var builder = DistributedApplication.CreateBuilder(args);

var nws = builder.AddExternalService("nws", "https://api.weather.gov");

var ai = builder.AddGitHubModel("ai-model", "gpt-4o-mini");

var redis = builder.AddRedis("cache");

var theApi = builder.AddProject<Projects.Api>("api")
    .WithHttpHealthCheck("/health")
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(nws);


var theWeb = builder.AddProject<Projects.MyWeatherHub>("web")
    .WaitFor(theApi)
    .WithReference(theApi)
    .WithReference(ai)
    .WithExternalHttpEndpoints();


builder.Build().Run();
