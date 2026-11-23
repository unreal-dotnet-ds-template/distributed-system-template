using Dst.Aspires.AppHost.Resources;

var builder = DistributedApplication.CreateBuilder(args);

// orleans storages for clustering (tables) and grain state (blobs); Azurite locally, Azure Storage when published
var storage = builder.AddAzureStorage("Dst-storage")
    .WithProvisioning()
    .RunAsEmulator();

var orleansClustering = storage.AddTables("Dst-orleans-clustering");
var orleansGrainState = storage.AddBlobs("Dst-orleans-grainstate");

// orleans cluster
var orleans = builder.AddOrleans("Dst-orleansCluster")
    .WithClustering(orleansClustering)
    .WithGrainStorage("DefaultStore", orleansGrainState);

// orleans silo -> backend. you run logic here.
var webOrleansSilo = builder.AddDotnetProject("Dst-web-orleans-silo", "../../OrleansSilo/Dst.OrleansSilo.WebApp/Dst.OrleansSilo.WebApp.csproj")
    .WithReference(orleans)
    .WithHttpHealthCheck("/health", endpointName: "http")
    .WithTelemetry(
        orleansClustering,
        orleansGrainState)
    .WithSiloAuth()
    .WaitFor(orleansClustering)
    .WaitFor(orleansGrainState)
    .WithReplicas(1);

// orleans client -> frontend. you call grains here.
var webApi = builder.AddDotnetProject("Dst-web-api", "../../Dst.WebApiApp/Dst.WebApiApp.csproj")
    .WithReference(orleans.AsClient()) // client-only reference
    .WithExternalHttpEndpoints()
    .WithTelemetry(
        orleansClustering,
        orleansGrainState)
    .WithWebApiAuth()
    .WithHttpHealthCheck("/health", endpointName: "http")
    .WaitFor(webOrleansSilo)
    .WithReplicas(1);

builder.Build().Run();
