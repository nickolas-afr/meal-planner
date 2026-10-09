using System.Text.Json;

namespace MealPlanner.Core.Auth;

// Request and response bodies for /api/auth/*, /api/me and /api/household/invites
// (architecture › Endpoints). Shared by App and Api so the JSON can't drift.
// WebAuthn payloads travel as raw JSON: the API produces/consumes Fido2's types,
// the App hands them to js/interop.js unchanged.

public sealed record TokenPair(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken);

public sealed record RegisterBeginRequest(string DisplayName, string? InviteCode);

/// <param name="Options">PublicKeyCredentialCreationOptions (base64url binary fields).</param>
public sealed record RegisterBeginResponse(string FlowId, JsonElement Options);

/// <param name="Attestation">The PublicKeyCredential from navigator.credentials.create, as JSON (base64url).</param>
public sealed record RegisterCompleteRequest(string FlowId, JsonElement Attestation);

/// <param name="Options">PublicKeyCredentialRequestOptions (base64url binary fields, empty allowCredentials).</param>
public sealed record LoginBeginResponse(string FlowId, JsonElement Options);

/// <param name="Assertion">The PublicKeyCredential from navigator.credentials.get, as JSON (base64url).</param>
public sealed record LoginCompleteRequest(string FlowId, JsonElement Assertion);

public sealed record RefreshRequest(string RefreshToken);

public sealed record MeResponse(Guid UserId, string DisplayName, HouseholdInfo Household, IReadOnlyList<MemberInfo> Members);

public sealed record HouseholdInfo(Guid Id, string Name);

public sealed record MemberInfo(Guid UserId, string DisplayName);

public sealed record InviteResponse(string Code, DateTime ExpiresAt);

/// <summary>Error body for 4xx responses, e.g. { "error": "invite_invalid" }.</summary>
public sealed record ErrorResponse(string Error);

public static class AuthErrors
{
    public const string InviteInvalid = "invite_invalid";
    public const string HouseholdFull = "household_full";
    public const string InvalidRequest = "invalid_request";
    public const string PasskeyFailed = "passkey_failed";
    public const string FlowExpired = "flow_expired";
    public const string InvalidToken = "invalid_token";
}
