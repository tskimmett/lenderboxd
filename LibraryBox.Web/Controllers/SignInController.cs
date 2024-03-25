namespace LibraryBox.Web;

using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Identity;
using IdentityUser = ElCamino.AspNetCore.Identity.AzureTable.Model.IdentityUser;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

[Route("api/signin")]
public class SignInController : Controller
{
    readonly IFido2 _fido2;
    readonly Fido2Store _fido2Store;
    readonly SignInManager<IdentityUser> _signInManager;
    readonly IMemoryCache _cache;

    public SignInController(
        IFido2 fido2,
        Fido2Store fido2Store,
        SignInManager<IdentityUser> signInManager,
        IMemoryCache cache)
    {
        _cache = cache;
        _signInManager = signInManager;
        _fido2Store = fido2Store;

        _fido2 = fido2;
    }

    [HttpPost]
    [Route("options")]
    public async Task<ActionResult> GetAssertionOptions([FromForm] string? username, [FromForm] string userVerification)
    {
        try
        {
            IReadOnlyList<PublicKeyCredentialDescriptor> existingCredentials = [];
            if (!string.IsNullOrWhiteSpace(username))
            {
                var user = _signInManager.UserManager.FindByIdAsync(username.ToLowerInvariant())
                    ?? throw new ArgumentException("Username was not registered");
                existingCredentials = (await _fido2Store.GetCredentialsByUsernameAsync(username)).Select(c => c.Descriptor!).ToList();
            }

            var extInputs = new AuthenticationExtensionsClientInputs()
            {
                Extensions = true,
                UserVerificationMethod = true,
                DevicePubKey = new AuthenticationExtensionsDevicePublicKeyInputs()
            };

            var uv = string.IsNullOrEmpty(userVerification)
                ? UserVerificationRequirement.Discouraged
                : userVerification.ToEnum<UserVerificationRequirement>();
            var options = _fido2.GetAssertionOptions(existingCredentials, uv, extInputs);

            var challengeKey = Guid.NewGuid().ToString();
            _cache.Set(challengeKey, options.ToJson(), TimeSpan.FromMinutes(5));

            return Json(new { options, challengeKey });
        }

        catch (Exception e)
        {
            return Json(new AssertionOptions { Status = "error", ErrorMessage = FormatException(e) });
        }
    }

    [HttpPost]
    [Route("assertion")]
    public async Task<JsonResult> MakeAssertion(
        [FromHeader(Name = "X-Challenge-Key")] string challengeKey,
        [FromBody] AuthenticatorAssertionRawResponse clientResponse,
        CancellationToken cancellationToken)
    {
        try
        {
            var jsonOptions = _cache.Get<string>(challengeKey);
            var options = AssertionOptions.FromJson(jsonOptions);

            _cache.Remove(challengeKey);

            var creds = await _fido2Store.GetCredentialByIdAsync(clientResponse.Id)
                ?? throw new Exception("Unknown credentials");

            var storedCounter = creds.SignCount;

            IsUserHandleOwnerOfCredentialIdAsync callback = async (args, cancellationToken) =>
            {
                // what is UserHandle?
                var storedCreds = await _fido2Store.GetCredentialsByUserHandleAsync(args.UserHandle, cancellationToken);
                return storedCreds.Any(c => c.Descriptor!.Id.SequenceEqual(args.CredentialId));
            };

            var res = await _fido2.MakeAssertionAsync(
                clientResponse,
                options,
                creds.PublicKey,
                creds.DevicePublicKeys,
                (uint)storedCounter,
                callback,
                cancellationToken);

            await _fido2Store.UpdateSignCount(res.CredentialId, res.SignCount);

            // if (res.DevicePublicKey is not null)
            //     creds.DevicePublicKeys.Add(res.DevicePublicKey);

            var loginInfo = _fido2Store.CredentialToLoginInfo(creds);
            var result = await _signInManager.ExternalLoginSignInAsync(loginInfo.LoginProvider, loginInfo.ProviderKey, false);

            return Json(res);
        }
        catch (Exception e)
        {
            return Json(new VerifyAssertionResult { Status = "error", ErrorMessage = FormatException(e) });
        }
    }

    static string FormatException(Exception e)
    {
        return string.Format("{0}{1}", e.Message, e.InnerException != null ? " (" + e.InnerException.Message + ")" : "");
    }
}
