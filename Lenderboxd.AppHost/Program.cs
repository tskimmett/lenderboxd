using Azure.Provisioning.Storage;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// var azStorage = builder.AddAzureStorage("storage", StorageKind.Storage, StorageSkuName.StandardLrs).RunAsEmulator();
var azStorage = builder.AddAzureStorage("storage").ConfigureInfrastructure(c =>
{
	var account = c.GetProvisionableResources().OfType<StorageAccount>().Single();
	account.Kind = StorageKind.Storage;
	account.Sku = new() { Name = StorageSkuName.StandardLrs };
}).RunAsEmulator();

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
	.WithReplicas(builder.Environment.IsDevelopment() ? 1 : 0);

builder.Build().Run();
