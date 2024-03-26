var builder = DistributedApplication.CreateBuilder(args);

var azStorage = builder.AddAzureStorage("storage").RunAsEmulator();
var tables = azStorage.AddTables("tables");
var queues = azStorage.AddQueues("queues");
var orleans = builder.AddOrleans("library-box-cluster")
	  .WithClustering(tables)
	  .WithGrainStorage("librarybox", tables)
	  .WithStreaming(queues)
	  .WithGrainStorage("PubSubStore", tables);

builder.AddProject<Projects.LibraryBox_Web>("librarybox-web")
	 .WithReference(orleans);
//  .WithReplicas(1);

builder.Build().Run();
