var builder = DistributedApplication.CreateBuilder(args);

var nws = builder.AddExternalService("nws", "https://api.weather.gov");

var theApi = builder.AddProject<Projects.Api>("api")
    .WithHttpHealthCheck("/health")
    .WithReference(nws);


var theWeb = builder.AddProject<Projects.MyWeatherHub>("web")
    .WaitFor(theApi)
    .WithReference(theApi)
    .WithExternalHttpEndpoints();


builder.Build().Run();
