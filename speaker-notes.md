# Speaker Notes: Aspire From Dev to Deploy

This document provides step-by-step instructions for progressing through the 5 savepoints in the Aspire conference talk, from `start` through `demo1-4` to `complete`.

## Overview

The demo showcases building a weather application called MyWeatherHub using .NET Aspire, demonstrating the journey from a basic web application to a fully distributed, cloud-ready application with AI capabilities.

## Stage 0: Start → Demo 1 (Adding Aspire)

**Goal**: Transform a basic web application into an Aspire-enabled distributed application.

### What to Do:

1. **Add Aspire Projects to Solution**
   - Add new `AppHost` project
   - Add new `ServiceDefaults` project

2. **Install NuGet Packages**

   **AppHost.csproj:**
   ```xml
   <Sdk Name="Aspire.AppHost.Sdk" Version="9.4.0" />
   <PackageReference Include="Aspire.Hosting.AppHost" Version="9.4.0" />
   ```

   **ServiceDefaults.csproj:**
   ```xml
   <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.7.0" />
   <PackageReference Include="Microsoft.Extensions.ServiceDiscovery" Version="9.4.0" />
   <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.12.0" />
   <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.12.0" />
   <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.12.0" />
   <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.12.0" />
   <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.12.0" />
   ```

3. **Add Project References**
   - Add ServiceDefaults reference to both Api and MyWeatherHub projects

4. **Update Program.cs Files**
   
   **MyWeatherHub/Program.cs:**
   - Add `builder.AddServiceDefaults();` after creating builder
   - Change HttpClient configuration from configuration-based URL to `"https+http://api"`
   - Add `app.MapDefaultEndpoints();` before mapping components

   **Api/Program.cs:**
   - Add `builder.AddServiceDefaults();` after creating builder  
   - Add `app.MapDefaultEndpoints();` before mapping API endpoints

5. **Create AppHost.cs**
   ```csharp
   var builder = DistributedApplication.CreateBuilder(args);

   var theApi = builder.AddProject<Projects.Api>("api");

   var theWeb = builder.AddProject<Projects.MyWeatherHub>("web")
       .WaitFor(theApi)
       .WithReference(theApi)
       .WithExternalHttpEndpoints();

   builder.Build().Run();
   ```

**Key Demo Points:**
- Show Aspire dashboard launching
- Highlight service discovery working between web and API
- Show telemetry and logging in the dashboard

---

## Stage 1: Demo1 → Demo2 (Adding Health Checks)

**Goal**: Add health monitoring to the API service.

### What to Do:

1. **Install NuGet Package**
   
   **Api.csproj:**
   ```xml
   <PackageReference Include="AspNetCore.HealthChecks.Uris" Version="9.0.0" />
   ```

2. **Update Api/Program.cs**
   - Add health check configuration after `builder.AddServiceDefaults()`:
   ```csharp
   builder.Services.AddHealthChecks()
       .AddUrlGroup(new Uri("https://nws"), "NWS Weather API", HealthStatus.Unhealthy,
           configureClient: (services, client) =>
           {
               client.DefaultRequestHeaders.Add("User-Agent", "Microsoft - .NET Aspire Demo");
           });
   ```

3. Add ExternalServiceResource to AppHost.cs

    ```csharp
       var nws = builder.AddExternalService("nws", "https://api.weather.gov");
    ```

**Key Demo Points:**
- Show health check endpoint working
- Demonstrate health status in Aspire dashboard
- Show what happens when external dependency is unhealthy

---

## Stage 2: Demo2 → Demo3 (Adding Redis Cache)

**Goal**: Add Redis caching to improve API performance.

### What to Do:

1. **Install NuGet Packages**
   
   **Api.csproj:**
   ```xml
   <PackageReference Include="Aspire.StackExchange.Redis.OutputCaching" Version="9.4.0" />
   ```

   **AppHost.csproj:**
   ```xml
   <PackageReference Include="Aspire.Hosting.Redis" Version="9.4.0" />
   ```

2. **Update AppHost.cs**
   - Add Redis resource (external NWS service already exists from Demo1):
   ```csharp
   var builder = DistributedApplication.CreateBuilder(args);

   var nws = builder.AddExternalService("nws", "https://api.weather.gov");

   var redis = builder.AddRedis("cache");

   var theApi = builder.AddProject<Projects.Api>("api")
       .WithHttpHealthCheck("/health")
       .WithReference(redis)
       .WaitFor(redis)
       .WithReference(nws);

   var theWeb = builder.AddProject<Projects.MyWeatherHub>("web")
       .WaitFor(theApi)
       .WithReference(theApi)
       .WithExternalHttpEndpoints();

   builder.Build().Run();
   ```

3. **Update Api/Program.cs** (implementation details would be in API service registration)

**Key Demo Points:**
- Show Redis container automatically starting
- Demonstrate caching improving response times
- Show Redis data in Aspire dashboard
- Highlight external service reference to NWS API

---

## Stage 3: Demo3 → Demo4 (Adding AI Capabilities)

**Goal**: Integrate AI-powered forecast summarization using GitHub Models.

### What to Do:

1. **Install NuGet Packages**
   
   **MyWeatherHub.csproj:**
   ```xml
   <PackageReference Include="Aspire.Azure.AI.Inference" Version="9.4.0-preview.1.25378.8" />
   <PackageReference Include="Microsoft.Extensions.AI" Version="9.7.1" />
   <PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="9.7.1-preview.1.25365.4" />
   ```

2. **Update AppHost.cs**
   - Add GitHub Model resource:
   ```csharp
   var ai = builder.AddGitHubModel("ai-model", "gpt-4o-mini");

   var theWeb = builder.AddProject<Projects.MyWeatherHub>("web")
       .WaitFor(theApi)
       .WithReference(theApi)
       .WithReference(ai)
       .WithExternalHttpEndpoints();
   ```

3. **Add New File: ForecastSummarizer.cs**
   - Create new service class for AI-powered forecast summarization
   - Include proper telemetry and logging
   - Add metrics for monitoring AI usage

4. **Update MyWeatherHub/Program.cs**
   - Register ForecastSummarizer service
   - Configure AI client

**Key Demo Points:**
- Show AI integration working
- Demonstrate forecast summarization
- Show AI metrics and telemetry in dashboard
- Highlight GitHub Models integration

---

## Stage 4: Demo4 → Complete (Deployment Ready)

**Goal**: Make the application ready for Azure deployment.

### What to Do:

1. **Add Azure Deployment Configuration**
   
   **Create azure.yaml:**
   ```yaml
   name: complete
   services:  
     app:
       language: dotnet
       project: ./AppHost/AppHost.csproj
       host: containerapp
   ```

2. **Add next-steps.md**
   - Documentation for post-deployment steps
   - Instructions for CI/CD setup
   - Troubleshooting guide

**Key Demo Points:**
- Show `azd init` command
- Demonstrate `azd up` deployment process
- Show running application in Azure
- Highlight Container Apps integration

---

## Demo Flow Tips

### Recommended Demo Sequence:

1. **Start with "start" folder** - Show basic web app
2. **Move to demo1** - "Let's add Aspire!" - Show service discovery magic
3. **Progress to demo2** - "Now let's add health monitoring"
4. **Advance to demo3** - "Time for some caching with Redis"
5. **Jump to demo4** - "Let's add AI to make it smart"
6. **Finish with complete** - "And now let's deploy to Azure"

### Key Talking Points:

- **Service Discovery**: No more hardcoded URLs
- **Observability**: Built-in telemetry, logging, and metrics
- **Resource Management**: Automatic container orchestration
- **Health Monitoring**: Built-in health checks and monitoring
- **Scalability**: Redis caching for performance
- **AI Integration**: Modern AI capabilities with GitHub Models
- **Cloud Deployment**: One-command deployment to Azure

### Common Issues to Address:

- Docker Desktop must be running for Redis
- GitHub token needed for AI models
- Azure CLI required for deployment
- Show fallback scenarios when services are unavailable

### Audience Engagement:

- Ask about current microservices pain points
- Show before/after configuration complexity
- Highlight developer productivity improvements
- Demonstrate how Aspire reduces boilerplate code

---

## Prerequisites for Demo

- .NET 9 SDK installed
- Docker Desktop running
- Visual Studio 2022 or VS Code with C# extension
- Azure CLI (for deployment demo)
- GitHub account with access to GitHub Models (for AI demo)

## Backup Plans

- If Docker fails: Skip Redis demo, mention it would work
- If AI fails: Show code structure, mention it would work with proper token
- If Azure deployment fails: Show azure.yaml and explain the process
