
using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.Azure;
using Azure.Provisioning;
using Azure.Provisioning.Storage;
using Azure.ResourceManager.Storage.Models;

public static class StorageExtensions
{
	public static IResourceBuilder<AzureStorageResource> AddAzureStorage(
		 this IDistributedApplicationBuilder builder,
		 string name,
		 StorageKind kind,
		 StorageSkuName sku)
	{
#pragma warning disable AZPROVISION001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        return builder.AddAzureStorage(name, kind, sku, null);
#pragma warning restore AZPROVISION001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    }

	[Experimental("AZPROVISION001", UrlFormat = "https://aka.ms/dotnet/aspire/diagnostics#{0}")]
	public static IResourceBuilder<AzureStorageResource> AddAzureStorage(
		 this IDistributedApplicationBuilder builder,
		 string name,
		 StorageKind kind,
		 StorageSkuName sku,
		 Action<IResourceBuilder<AzureStorageResource>, ResourceModuleConstruct, StorageAccount>? configureResource)
	{
		builder.AddAzureProvisioning();

		var configureConstruct = (ResourceModuleConstruct construct) =>
		{
			var storageAccount = construct.AddStorageAccount(
				name: name,
				kind: kind,
				sku: sku
			);

			// Unfortunately Azure Storage does not list ACA as one of the resource types in which
			// the AzureServices firewall policy works. This means that we need this Azure Storage
			// account to have its default action set to Allow.
			storageAccount.AssignProperty(p => p.NetworkRuleSet.DefaultAction, "'Allow'");

			storageAccount.Properties.Tags["aspire-resource-name"] = construct.Resource.Name;

			// Set the minimum TLS version to 1.2 to ensure resources provisioned are compliant
			// with the pending deprecation of TLS 1.0 and 1.1.
			storageAccount.AssignProperty(p => p.MinimumTlsVersion, "'TLS1_2'");

			// Disable shared key access to the storage account as managed identity is configured
			// to access the storage account by default.
			storageAccount.AssignProperty(p => p.AllowSharedKeyAccess, "false");

			var blobService = new BlobService(construct);

			var blobRole = storageAccount.AssignRole(RoleDefinition.StorageBlobDataContributor);
			blobRole.AssignProperty(p => p.PrincipalId, construct.PrincipalIdParameter);
			blobRole.AssignProperty(p => p.PrincipalType, construct.PrincipalTypeParameter);

			var tableRole = storageAccount.AssignRole(RoleDefinition.StorageTableDataContributor);
			tableRole.AssignProperty(p => p.PrincipalId, construct.PrincipalIdParameter);
			tableRole.AssignProperty(p => p.PrincipalType, construct.PrincipalTypeParameter);

			var queueRole = storageAccount.AssignRole(RoleDefinition.StorageQueueDataContributor);
			queueRole.AssignProperty(p => p.PrincipalId, construct.PrincipalIdParameter);
			queueRole.AssignProperty(p => p.PrincipalType, construct.PrincipalTypeParameter);

			storageAccount.AddOutput("blobEndpoint", sa => sa.PrimaryEndpoints.BlobUri);
			storageAccount.AddOutput("queueEndpoint", sa => sa.PrimaryEndpoints.QueueUri);
			storageAccount.AddOutput("tableEndpoint", sa => sa.PrimaryEndpoints.TableUri);

			var resource = (AzureStorageResource)construct.Resource;
			var resourceBuilder = builder.CreateResourceBuilder(resource);
			configureResource?.Invoke(resourceBuilder, construct, storageAccount);
		};

		var resource = new AzureStorageResource(name, configureConstruct);

		return builder.AddResource(resource)
						  // These ambient parameters are only available in development time.
						  .WithParameter(AzureBicepResource.KnownParameters.PrincipalId)
						  .WithParameter(AzureBicepResource.KnownParameters.PrincipalType)
						  .WithManifestPublishingCallback(resource.WriteToManifest);
	}
}