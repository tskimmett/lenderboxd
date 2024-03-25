namespace LibraryBox.Web;

using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Identity;
using IdentityUser = ElCamino.AspNetCore.Identity.AzureTable.Model.IdentityUser;
using Microsoft.AspNetCore.Mvc;
using DotNext;
using Microsoft.Extensions.Caching.Memory;

[Route("/api/register")]
public class RegisterController : Controller
{
    readonly IFido2 _fido2;
    readonly Fido2Store _fido2Store;
    readonly UserManager<IdentityUser> _userManager;
    readonly SignInManager<IdentityUser> _signInManager;
    readonly IMemoryCache _cache;

    public RegisterController(
        IFido2 fido2,
        Fido2Store fido2Store,
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        IMemoryCache cache)
    {
        _fido2 = fido2;
        _fido2Store = fido2Store;
        _userManager = userManager;
        _signInManager = signInManager;
        _cache = cache;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("credential-options")]
    public async Task<ActionResult> MakeCredentialOptions(
        [FromForm] string username,
        [FromForm] string attType,
        [FromForm] string authType,
        [FromForm] string residentKey)
    {
        if (string.IsNullOrWhiteSpace(username))
            return BadRequest("username not provided");
        else if (await UserExists(username))
            return RedirectToAction("");

        try
        {
            var user = new Fido2User
            {
                Name = username,
                Id = Fido2Store.GetUserNameInBytes(username)
            };

            var authenticatorSelection = new AuthenticatorSelection
            {
                // no clue what resident key means
                ResidentKey = residentKey.ToEnum<ResidentKeyRequirement>(),
                UserVerification = UserVerificationRequirement.Required
            };

            if (!string.IsNullOrEmpty(authType))
                authenticatorSelection.AuthenticatorAttachment = authType.ToEnum<AuthenticatorAttachment>();

            var extInputs = new AuthenticationExtensionsClientInputs
            {
                Extensions = true,
                UserVerificationMethod = true,
            };

            var options = _fido2.RequestNewCredential(user, [], authenticatorSelection, attType.ToEnum<AttestationConveyancePreference>(), extInputs);

            var challengeKey = Guid.NewGuid().ToString();
            _cache.Set(challengeKey, options.ToJson(), TimeSpan.FromMinutes(5));

            return Json(new { options, challengeKey });
        }
        catch (Exception e)
        {
            return Json(new CredentialCreateOptions { Status = "error", ErrorMessage = FormatException(e) });
        }
    }

    [HttpPost]
    [Route("credential")]
    public async Task<ActionResult> MakeCredential(
        [FromHeader(Name = "X-Challenge-Key")] string challengeKey,
        [FromBody] AuthenticatorAttestationRawResponse attestationResponse)
    {
        var jsonOptions = _cache.Get<string>(challengeKey);
        var options = CredentialCreateOptions.FromJson(jsonOptions);

        _cache.Remove(challengeKey);

        if (await UserExists(options.User.Name))
            return BadRequest("username taken");

        async Task<bool> isUniqueCredential(IsCredentialIdUniqueToUserParams args, CancellationToken cancellationToken)
        {
            return await _fido2Store.FindUserByCredentialId(args.CredentialId) is null;
        }

        var result = await _fido2.MakeNewCredentialAsync(attestationResponse, options, isUniqueCredential);
        if (result.Result != null)
        {
            var (userCreated, user) = await _fido2Store.CreateUserAsync(options.User, new FidoCredential
            {
                Id = result.Result.Id,
                Descriptor = new PublicKeyCredentialDescriptor(result.Result.Id),
                PublicKey = result.Result.PublicKey,
                UserId = result.Result.User.Id,
                UserHandle = result.Result.User.Id,
                SignCount = (int)result.Result.SignCount,
                AttestationFormat = result.Result.AttestationFormat,
                RegDate = DateTimeOffset.UtcNow,
                AaGuid = result.Result.AaGuid,
                Transports = result.Result.Transports,
                IsBackupEligible = result.Result.IsBackupEligible,
                IsBackedUp = result.Result.IsBackedUp,
                AttestationObject = result.Result.AttestationObject,
                AttestationClientDataJson = result.Result.AttestationClientDataJson,
                DevicePublicKeys = [result.Result.DevicePublicKey]
            });

            if (!userCreated.Succeeded)
                return BadRequest(userCreated.Errors);
            else
                await _signInManager.SignInAsync(user!, false);
        }

        return Json(result);
    }

    async Task<bool> UserExists(string username)
    {
        return await _userManager.FindByIdAsync(username.ToLowerInvariant()) is not null;
    }

    static string FormatException(Exception e)
    {
        return string.Format("{0}{1}", e.Message, e.InnerException != null ? " (" + e.InnerException.Message + ")" : "");
    }
}