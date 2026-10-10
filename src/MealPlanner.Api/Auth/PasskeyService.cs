using System.Security.Cryptography;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using MealPlanner.Api.Data;
using MealPlanner.Api.Options;
using MealPlanner.Core;
using MealPlanner.Core.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MealPlanner.Api.Auth;

public sealed class PasskeyService(IFido2 fido2, IMemoryCache cache, AppDb db, TokenService tokens, IOptions<AuthOptions> authOptions)
{
    public const int MaxMembers = 4;
    private static readonly TimeSpan FlowLifetime = TimeSpan.FromMinutes(5);
    private const string InviteAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private sealed record PendingRegistration(CredentialCreateOptions Options, Guid UserId, string DisplayName, string? InviteCode);

    private sealed record PendingLogin(AssertionOptions Options);

    public async Task<RegisterBeginResponse> BeginRegisterAsync(RegisterBeginRequest request)
    {
        if (Validation.DisplayName(request.DisplayName) is not null)
            throw new ApiException(400, AuthErrors.InvalidRequest);
        string name = request.DisplayName.Trim();

        string? code = null;
        if (string.IsNullOrWhiteSpace(request.InviteCode) && !authOptions.Value.AllowNewHouseholds)
            throw new ApiException(403, AuthErrors.SignupsClosed);
        if (!string.IsNullOrWhiteSpace(request.InviteCode))
        {
            code = Validation.NormalizeInviteCode(request.InviteCode);
            Invite invite = await FindValidInvite(code);
            // Check here too (complete re-checks), so nobody makes a passkey for a full household.
            if (await db.Users.CountAsync(u => u.HouseholdId == invite.HouseholdId) >= MaxMembers)
                throw new ApiException(409, AuthErrors.HouseholdFull);
        }

        Guid userId = Guid.NewGuid();
        CredentialCreateOptions options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User { Id = userId.ToByteArray(), Name = name, DisplayName = name },
            ExcludeCredentials = [],
            AuthenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Required,
                UserVerification = UserVerificationRequirement.Required,
            },
            AttestationPreference = AttestationConveyancePreference.None,
        });
        string flowId = Store(new PendingRegistration(options, userId, name, code));
        return new RegisterBeginResponse(flowId, JsonSerializer.SerializeToElement(options));
    }

    public async Task<TokenPair> CompleteRegisterAsync(RegisterCompleteRequest request)
    {
        var pending = Take<PendingRegistration>(request.FlowId);
        RegisteredPublicKeyCredential credential;
        try
        {
            var attestation = request.Attestation.Deserialize<AuthenticatorAttestationRawResponse>()!;
            credential = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = attestation,
                OriginalOptions = pending.Options,
                IsCredentialIdUniqueToUserCallback = async (p, ct) =>
                    !await db.Passkeys.AnyAsync(k => k.CredentialId == p.CredentialId, ct),
            });
        }
        catch (Exception e) when (e is Fido2VerificationException or JsonException or NullReferenceException or ArgumentException)
        {
            throw new ApiException(400, AuthErrors.PasskeyFailed);
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        DateTime now = DateTime.UtcNow;
        Household household;
        Invite? invite = null;
        if (pending.InviteCode is null)
        {
            if (!authOptions.Value.AllowNewHouseholds)
                throw new ApiException(403, AuthErrors.SignupsClosed);
            household = new Household { Id = Guid.NewGuid(), Name = $"{pending.DisplayName}'s household", CreatedAt = now };
            db.Households.Add(household);
        }
        else
        {
            invite = await FindValidInvite(pending.InviteCode);
            if (await db.Users.CountAsync(u => u.HouseholdId == invite.HouseholdId) >= MaxMembers)
                throw new ApiException(409, AuthErrors.HouseholdFull);
            household = await db.Households.FirstAsync(h => h.Id == invite.HouseholdId);
        }

        var user = new User { Id = pending.UserId, HouseholdId = household.Id, DisplayName = pending.DisplayName, CreatedAt = now };
        db.Users.Add(user);
        db.Passkeys.Add(new Passkey
        {
            CredentialId = credential.Id,
            UserId = user.Id,
            PublicKey = credential.PublicKey,
            SignCount = credential.SignCount,
            UserHandle = user.Id.ToByteArray(),
            CreatedAt = now,
        });
        if (invite is not null)
        {
            invite.UsedAt = now;
            invite.UsedBy = user.Id;
        }
        await db.SaveChangesAsync();
        TokenPair pair = await tokens.IssueAsync(user);
        await tx.CommitAsync();
        return pair;
    }

    public LoginBeginResponse BeginLogin()
    {
        AssertionOptions options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [],
            UserVerification = UserVerificationRequirement.Required,
        });
        string flowId = Store(new PendingLogin(options));
        return new LoginBeginResponse(flowId, JsonSerializer.SerializeToElement(options));
    }

    public async Task<TokenPair> CompleteLoginAsync(LoginCompleteRequest request)
    {
        var pending = Take<PendingLogin>(request.FlowId);
        Passkey passkey;
        try
        {
            var assertion = request.Assertion.Deserialize<AuthenticatorAssertionRawResponse>()!;
            passkey = await db.Passkeys.FirstOrDefaultAsync(k => k.CredentialId == assertion.RawId)
                ?? throw new ApiException(400, AuthErrors.PasskeyFailed);
            if (assertion.Response.UserHandle is null || !assertion.Response.UserHandle.AsSpan().SequenceEqual(passkey.UserHandle))
                throw new ApiException(400, AuthErrors.PasskeyFailed);

            VerifyAssertionResult result = await fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = assertion,
                OriginalOptions = pending.Options,
                StoredPublicKey = passkey.PublicKey,
                StoredSignatureCounter = (uint)passkey.SignCount,
                IsUserHandleOwnerOfCredentialIdCallback = (p, ct) =>
                    Task.FromResult(p.UserHandle.AsSpan().SequenceEqual(passkey.UserHandle)),
            });
            passkey.SignCount = result.SignCount;
        }
        catch (Exception e) when (e is Fido2VerificationException or JsonException or NullReferenceException or ArgumentException)
        {
            throw new ApiException(400, AuthErrors.PasskeyFailed);
        }

        passkey.LastUsedAt = DateTime.UtcNow;
        User user = await db.Users.FirstAsync(u => u.Id == passkey.UserId);
        return await tokens.IssueAsync(user); // saves the passkey update too
    }

    public async Task<InviteResponse> CreateInviteAsync(Guid userId, Guid householdId)
    {
        if (await db.Users.CountAsync(u => u.HouseholdId == householdId) >= MaxMembers)
            throw new ApiException(409, AuthErrors.HouseholdFull);
        DateTime now = DateTime.UtcNow;
        var invite = new Invite
        {
            Code = new string(Enumerable.Range(0, Validation.InviteCodeLength)
                .Select(_ => InviteAlphabet[RandomNumberGenerator.GetInt32(InviteAlphabet.Length)]).ToArray()),
            HouseholdId = householdId,
            CreatedBy = userId,
            CreatedAt = now,
            ExpiresAt = now.AddHours(48),
        };
        db.Invites.Add(invite);
        await db.SaveChangesAsync();
        return new InviteResponse(invite.Code, invite.ExpiresAt);
    }

    private async Task<Invite> FindValidInvite(string code)
    {
        Invite? invite = await db.Invites.FirstOrDefaultAsync(i => i.Code == code);
        if (invite is null || invite.UsedAt is not null || invite.ExpiresAt <= DateTime.UtcNow)
            throw new ApiException(400, AuthErrors.InviteInvalid);
        return invite;
    }

    private string Store(object pending)
    {
        string flowId = Guid.NewGuid().ToString("N");
        cache.Set(flowId, pending, FlowLifetime);
        return flowId;
    }

    private T Take<T>(string? flowId) where T : class
    {
        if (flowId is null || !cache.TryGetValue(flowId, out object? value) || value is not T pending)
            throw new ApiException(400, AuthErrors.FlowExpired);
        cache.Remove(flowId);
        return pending;
    }
}
