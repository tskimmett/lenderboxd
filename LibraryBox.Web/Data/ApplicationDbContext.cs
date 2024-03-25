namespace LibraryBox.Web;

using ElCamino.AspNetCore.Identity.AzureTable;
using ElCamino.AspNetCore.Identity.AzureTable.Model;

public class ApplicationDbContext : IdentityCloudContext
{
	public ApplicationDbContext(IdentityConfiguration config) : base(config)
	{
	}
}
