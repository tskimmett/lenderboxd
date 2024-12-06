using Azure.Provisioning.Storage;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var fdb = builder.AddFoundationDb(
    "fdb",
    apiVersion: 730,
    root: "/lenderboxd",
    port: 4500,
    clusterVersion: "7.3.46",
    rollForward: FdbVersionPolicy.Exact)
.WithDefaults(tracing: FoundationDB.Client.FdbTracingOptions.All)
.WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Projects.Lenderboxd_Web>("lenderboxd-web")
    .WithExternalHttpEndpoints()
    .WithReference(fdb)
    .WithReplicas(builder.Environment.IsDevelopment() ? 1 : 0);

builder.Build().Run();
