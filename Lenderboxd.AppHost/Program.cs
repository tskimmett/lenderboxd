var builder = DistributedApplication.CreateBuilder(args);

var azStorage = builder.AddAzureStorage("storage").RunAsEmulator();
var tables = azStorage.AddTables("tables");
var queues = azStorage.AddQueues("queues");
var orleans = builder.AddOrleans("lenderboxd-cluster")
		.WithClustering(tables)
		.WithGrainStorage("Default", tables)
		.WithGrainStorage("PubSubStore", tables);

builder.AddProject<Projects.Lenderboxd_Web>("lenderboxd-web")
	.WithExternalHttpEndpoints()
	.WithReference(orleans)
	.WithReference(queues)
	.WithReplicas(1);

builder.Build().Run();
