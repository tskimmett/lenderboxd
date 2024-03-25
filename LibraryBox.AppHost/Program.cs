using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// var storage = builder.AddAzureStorage("storage");

// if (builder.Environment.IsDevelopment())
// {
//     storage.UseEmulator();
// }
// else
// {
//     builder.AddAzureProvisioning();
// }

// var clusteringTable = storage.AddTables("clustering");
// var grainStorage = storage.AddBlobs("grainstate");

// var orleans = builder.AddOrleans("my-app")
//     .WithClustering(clusteringTable)
//     .WithGrainStorage("Default", grainStorage);

// // For local development, instead of using the emulator,
// // one can use the in memory provider from Orleans:
// // var orleans = builder.AddOrleans("my-app")
// //     .WithDevelopmentClustering()
// //     .WithMemoryGrainStorage("Default");

// builder.AddProject<Projects.LibraryBox_Web>("silo")
//        .WithReference(orleans);

builder.AddProject<Projects.LibraryBox_Web>("silo");

using var app = builder.Build();

await app.RunAsync();
