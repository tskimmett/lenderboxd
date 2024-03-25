using Microsoft.Extensions.Hosting;

using var host = new HostBuilder()
	.UseOrleans(b =>
	{
		b.UseLocalhostClustering();
		b.AddMemoryGrainStorageAsDefault();
		b.AddMemoryGrainStorage("blob");
		b.UseTransactions();
	})
	.Build();

host.Start();
