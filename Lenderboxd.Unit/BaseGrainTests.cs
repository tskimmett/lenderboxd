namespace Lenderboxd.Unit;

using Orleans.Hosting;
using Orleans.TestingHost;

public class BaseGrainTests
{
	static TestCluster? Cluster { get; set; }

	public static T GetGrain<T>(Guid id) where T : IGrainWithGuidKey
		=> Cluster!.GrainFactory.GetGrain<T>(id);

	public static T GetGrain<T>(int id) where T : IGrainWithIntegerKey
		=> Cluster!.GrainFactory.GetGrain<T>(id);

	[ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
	public static void ClassInit(TestContext testContext)
	{
		var builder = new TestClusterBuilder();
		builder.AddSiloBuilderConfigurator<TestSiloConfig>();
		Cluster = builder.Build();
		Cluster.Deploy();
	}

	[ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
	public static void ClassCleanup()
	{
	}
}

public class TestSiloConfig : ISiloConfigurator
{
	public void Configure(ISiloBuilder siloBuilder)
	{
		siloBuilder
			.AddMemoryGrainStorage("blob")
			.AddMemoryGrainStorageAsDefault()
			.UseTransactions();
	}

}