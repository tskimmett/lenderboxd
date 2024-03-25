namespace LibraryBox.Web;

using Azure.Data.Tables;
using ElCamino.AspNetCore.Identity.AzureTable.Helpers;
using ElCamino.AspNetCore.Identity.AzureTable.Model;
using Fido2NetLib;
using Microsoft.AspNetCore.Identity;
using System.Text;
using IdentityUser = ElCamino.AspNetCore.Identity.AzureTable.Model.IdentityUser;

public class Fido2Store
{
    readonly UserManager<IdentityUser> _userStore;

    readonly TableServiceClient _tableService;
    readonly TableClient _credentials;
    readonly DefaultKeyHelper _keyHelper = new();

    public Fido2Store(IdentityConfiguration idConfig, UserManager<IdentityUser> userStore)
    {
        _userStore = userStore;
        TableClientOptions options = new();
        options.Diagnostics.IsLoggingEnabled = true;
        options.Diagnostics.IsLoggingContentEnabled = true;
        _tableService = new TableServiceClient(idConfig.StorageConnectionString, options);
        _credentials = _tableService.GetTableClient("Credentials");
        _credentials.CreateIfNotExists();
    }

    public async Task<IEnumerable<FidoCredential>> GetCredentialsByUsernameAsync(string username)
    {
        var user = await _userStore.FindByNameAsync(username.ToLowerInvariant());
        if (user is null)
            return [];

        var logins = await _userStore.GetLoginsAsync(user);
        return (await Task.WhenAll(logins.Select(l => GetCredentialsByRowKey(l.ProviderKey))))
            .NotNull();
    }

    public async Task RemoveCredentialsByUsernameAsync(string username)
    {
        var actions = (await GetCredentialsByUsernameAsync(username))
            .Select(cred => new TableTransactionAction(TableTransactionActionType.Delete, cred));

        await _credentials.SubmitTransactionAsync(actions);
    }

    public Task<FidoCredential?> GetCredentialByIdAsync(byte[] id)
    {
        var credentialKey = CredentialIdToRowKey(id);
        return GetCredentialsByRowKey(credentialKey);
    }

    async Task<FidoCredential?> GetCredentialsByRowKey(string credentialKey)
        => await _credentials.GetEntityOrDefaultAsync<FidoCredential>(credentialKey, credentialKey);


    string CredentialIdToRowKey(byte[] id)
        => _keyHelper.ConvertKeyToHash(Convert.ToBase64String(id))!;

    // signin
    public Task<ICollection<FidoStoredCredential>> GetCredentialsByUserHandleAsync(byte[] userHandle, CancellationToken cancellationToken)
    {
        // see what userHandle is
        return Task.FromResult<ICollection<FidoStoredCredential>>([]);
    }

    public UserLoginInfo CredentialToLoginInfo(FidoCredential credential)
        => new("fido2", CredentialIdToRowKey(credential.Id), null);

    public async Task<(IdentityResult, IdentityUser?)> CreateUserAsync(Fido2User fidoUser, FidoCredential credential)
    {
        var user = new IdentityUser
        {
            Id = Encoding.UTF8.GetString(fidoUser.Id),
            UserName = fidoUser.Name
        };

        var userResult = await _userStore.CreateAsync(user);

        if (!userResult.Succeeded)
            return (userResult, null);

        credential.UserId = fidoUser.Id;
        credential.RowKey = credential.PartitionKey = CredentialIdToRowKey(credential.Id);

        var credResults = await Task.WhenAll(
            _credentials.AddEntityAsync(credential)
                .ContinueWith(res => !res.Result.IsError),
            _userStore.AddLoginAsync(user, CredentialToLoginInfo(credential))
                .ContinueWith(res => res.Result.Succeeded)
        );

        if (credResults.Any(succeeded => !succeeded))
        {
            // cleanup credential and user
            await Task.WhenAll(_credentials.DeleteEntityAsync(credential.PartitionKey, credential.RowKey), _userStore.DeleteAsync(user));
            userResult = IdentityResult.Failed(new IdentityError { Code = "666", Description = "Failed to store user credentials" });
        }

        return (userResult, user);
    }

    public async Task<IdentityUser?> FindUserByCredentialId(byte[] credentialId)
        => await _userStore.FindByLoginAsync("fido2", CredentialIdToRowKey(credentialId));


    public static byte[] GetUserNameInBytes(string userName)
        => Encoding.UTF8.GetBytes(userName.ToLowerInvariant());

    public async Task UpdateSignCount(byte[] credentialId, uint signCount)
    {
        var key = CredentialIdToRowKey(credentialId);
        var credential = await GetCredentialsByRowKey(key)
            ?? throw new InvalidOperationException($"Provided id ({CredentialIdToRowKey(credentialId)}) does not map to stored credential.");

        credential.SignCount = (int)signCount;
        await _credentials.UpdateEntityAsync(credential, credential.ETag, TableUpdateMode.Replace);
    }

}

public static class Fido2Extenstions
{
    public static IEnumerable<T> NotNull<T>(this IEnumerable<T?> enumerable) where T : class
        => enumerable.Where(e => e != null).Select(e => e!);
}