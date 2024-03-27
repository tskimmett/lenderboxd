var builder = DistributedApplication.CreateBuilder(args);

var azStorage = builder.AddAzureStorage("storage").RunAsEmulator();
var tables = azStorage.AddTables("tables");
var queues = azStorage.AddQueues("queues");
var orleans = builder.AddOrleans("librarybox-cluster")
		.WithClustering(tables)
		.WithGrainStorage("Default", tables)
		// .WithMemoryStreaming("streams")
		.WithStreaming(queues)
		.WithGrainStorage("PubSubStore", tables);

builder.AddProject<Projects.LibraryBox_Web>("librarybox-web")
	 .WithReference(orleans);
//  .WithReplicas(1);

builder.Build().Run();
