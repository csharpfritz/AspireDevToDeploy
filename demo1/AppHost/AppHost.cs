var builder = DistributedApplication.CreateBuilder(args);

var theApi = builder.AddProject<Projects.Api>("api");

var theWeb = builder.AddProject<Projects.MyWeatherHub>("web")
    .WaitFor(theApi)
    .WithReference(theApi)
    .WithExternalHttpEndpoints();


builder.Build().Run();
