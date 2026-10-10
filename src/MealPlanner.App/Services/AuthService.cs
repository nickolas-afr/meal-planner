using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Blazored.LocalStorage;
using MealPlanner.App.Storage;
using MealPlanner.Core.Auth;
using Microsoft.JSInterop;

namespace MealPlanner.App.Services;

/// <summary>Outcome of a sign-in, create or join. <see cref="Error"/> is a user-facing message.</summary>
public sealed record AuthResult(bool Ok, string? Error = null);

/// <summary>
/// Passkey accounts: owns the <see cref="TokenPair"/> (local storage key "mealplanner.auth") and talks to /api/auth/*,
/// /api/me and /api/household/invites. With <see cref="TokenHandler"/> it is the only place besides SyncService that calls the API.
/// </summary>
public sealed class AuthService
{
    public const string StorageKey = "mealplanner.auth";
    public const string ApiClientKey = "api";
    public const string AuthClientKey = "auth";

    private static readonly TimeSpan RefreshWindow = TimeSpan.FromSeconds(60);

    private readonly ILocalStorageService _storage;
    private readonly IJSRuntime _js;
    private readonly AppState _state;
    private readonly IStore _store;
    private readonly IServiceProvider _services;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private TokenPair? _tokens;
    private HttpClient? _plain;
    private HttpClient? _api;

    public AuthService(ILocalStorageService storage, IJSRuntime js, AppState state, IStore store, IServiceProvider services)
    {
        _storage = storage;
        _js = js;
        _state = state;
        _store = store;
        _services = services;
    }

    public event Action? OnChange;

    /// <summary>True when a refresh token is held.</summary>
    public bool IsSignedIn => !string.IsNullOrEmpty(_tokens?.RefreshToken);

    // The plain client has no TokenHandler (auth/* and refresh); the API client does. Resolved lazily to avoid a cycle with TokenHandler.
    private HttpClient Plain => _plain ??= _services.GetRequiredKeyedService<HttpClient>(AuthClientKey);
    private HttpClient Api => _api ??= _services.GetRequiredKeyedService<HttpClient>(ApiClientKey);

    public async Task LoadAsync()
    {
        try { _tokens = await _storage.GetItemAsync<TokenPair>(StorageKey); }
        catch { _tokens = null; }
        OnChange?.Invoke();
    }

    public Task<AuthResult> CreateHouseholdAsync(string displayName) => RegisterAsync(displayName, null);

    public Task<AuthResult> JoinAsync(string inviteCode, string displayName) =>
        RegisterAsync(displayName, MealPlanner.Core.Validation.NormalizeInviteCode(inviteCode));

    private async Task<AuthResult> RegisterAsync(string displayName, string? inviteCode)
    {
        const string cancelled = "Passkey wasn't created. Try again.";
        try
        {
            var begin = await Plain.PostAsJsonAsync("api/auth/register/begin", new RegisterBeginRequest(displayName.Trim(), inviteCode));
            if (!begin.IsSuccessStatusCode) return new AuthResult(false, await ErrorMessageAsync(begin));
            var b = await begin.Content.ReadFromJsonAsync<RegisterBeginResponse>();
            if (b is null) return Generic;

            string attestation;
            try { attestation = await _js.InvokeAsync<string>("mealPlanner.webauthn.create", b.Options.GetRawText()); }
            catch (JSException ex) when (IsCancel(ex)) { return new AuthResult(false, cancelled); }
            catch (JSException) { return Generic; }

            var done = await Plain.PostAsJsonAsync("api/auth/register/complete",
                new RegisterCompleteRequest(b.FlowId, JsonDocument.Parse(attestation).RootElement));
            return await FinishAsync(done);
        }
        catch (HttpRequestException) { return Network; }
        catch (TaskCanceledException) { return Network; }
        catch (JsonException) { return Generic; }
    }

    public async Task<AuthResult> SignInAsync()
    {
        try
        {
            var begin = await Plain.PostAsJsonAsync("api/auth/login/begin", new { });
            if (!begin.IsSuccessStatusCode) return new AuthResult(false, await ErrorMessageAsync(begin));
            var b = await begin.Content.ReadFromJsonAsync<LoginBeginResponse>();
            if (b is null) return Generic;

            string assertion;
            try { assertion = await _js.InvokeAsync<string>("mealPlanner.webauthn.get", b.Options.GetRawText()); }
            catch (JSException ex) when (IsCancel(ex)) { return new AuthResult(false, "Sign-in was cancelled."); }
            catch (JSException) { return Generic; }

            var done = await Plain.PostAsJsonAsync("api/auth/login/complete",
                new LoginCompleteRequest(b.FlowId, JsonDocument.Parse(assertion).RootElement));
            return await FinishAsync(done);
        }
        catch (HttpRequestException) { return Network; }
        catch (TaskCanceledException) { return Network; }
        catch (JsonException) { return Generic; }
    }

    private async Task<AuthResult> FinishAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) return new AuthResult(false, await ErrorMessageAsync(response));
        var pair = await response.Content.ReadFromJsonAsync<TokenPair>();
        if (pair is null) return Generic;
        await SetTokensAsync(pair);
        // First sync (LastVersion is 0 on a fresh store). Not awaited: the server may be waking up.
        _ = _services.GetService<SyncService>()?.SyncNowAsync();
        return new AuthResult(true);
    }

    /// <summary>A valid access token, refreshing first when it expires within 60 s. Null when signed out or the refresh failed.</summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        var t = _tokens;
        if (t is null) return null;
        if (t.AccessTokenExpiresAt - DateTime.UtcNow > RefreshWindow) return t.AccessToken;

        await _refreshLock.WaitAsync();
        try
        {
            t = _tokens;
            if (t is null) return null;
            if (t.AccessTokenExpiresAt - DateTime.UtcNow > RefreshWindow) return t.AccessToken;
            try
            {
                var r = await Plain.PostAsJsonAsync("api/auth/refresh", new RefreshRequest(t.RefreshToken));
                if (r.StatusCode == HttpStatusCode.Unauthorized)
                {
                    await SetTokensAsync(null);
                    return null;
                }
                if (!r.IsSuccessStatusCode) return null;
                var pair = await r.Content.ReadFromJsonAsync<TokenPair>();
                if (pair is null) return null;
                await SetTokensAsync(pair);
                return pair.AccessToken;
            }
            catch (HttpRequestException) { return null; }
            catch (TaskCanceledException) { return null; }
        }
        finally { _refreshLock.Release(); }
    }

    /// <summary>
    /// Forces a refresh. True: new tokens held. False: the server rejected the refresh token (tokens cleared).
    /// Null: the server could not be reached.
    /// </summary>
    public async Task<bool?> TryRefreshAsync()
    {
        await _refreshLock.WaitAsync();
        try
        {
            var t = _tokens;
            if (t is null) return false;
            try
            {
                var r = await Plain.PostAsJsonAsync("api/auth/refresh", new RefreshRequest(t.RefreshToken));
                if (r.StatusCode == HttpStatusCode.Unauthorized)
                {
                    await SetTokensAsync(null);
                    return false;
                }
                if (!r.IsSuccessStatusCode) return null;
                var pair = await r.Content.ReadFromJsonAsync<TokenPair>();
                if (pair is null) return null;
                await SetTokensAsync(pair);
                return true;
            }
            catch (HttpRequestException) { return null; }
            catch (TaskCanceledException) { return null; }
            catch (JsonException) { return null; }
        }
        finally { _refreshLock.Release(); }
    }

    /// <summary>Drops the tokens but keeps local data (the session is no longer valid). The user signs in again.</summary>
    public Task ExpireSessionAsync() => SetTokensAsync(null);

    public async Task SignOutAsync()
    {
        var refresh = _tokens?.RefreshToken;
        if (refresh is not null)
        {
            try { await Plain.PostAsJsonAsync("api/auth/logout", new RefreshRequest(refresh)); }
            catch (Exception) { /* best effort */ }
        }
        var empty = new AppData();
        _state.Load(empty);
        await _store.SaveAsync(empty);
        await SetTokensAsync(null);
        _state.NotifyChanged();
    }

    public async Task<MeResponse?> GetMeAsync()
    {
        try
        {
            var r = await Api.GetAsync("api/me");
            return r.IsSuccessStatusCode ? await r.Content.ReadFromJsonAsync<MeResponse>() : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException) { return null; }
    }

    public async Task<InviteResponse?> CreateInviteAsync()
    {
        try
        {
            var r = await Api.PostAsync("api/household/invites", null);
            return r.IsSuccessStatusCode ? await r.Content.ReadFromJsonAsync<InviteResponse>() : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException) { return null; }
    }

    private async Task SetTokensAsync(TokenPair? pair)
    {
        _tokens = pair;
        try
        {
            if (pair is null) await _storage.RemoveItemAsync(StorageKey);
            else await _storage.SetItemAsync(StorageKey, pair);
        }
        catch { /* storage unavailable: stay signed in for this session */ }
        OnChange?.Invoke();
    }

    private static bool IsCancel(JSException ex) =>
        ex.Message.Contains("NotAllowedError", StringComparison.Ordinal) || ex.Message.Contains("AbortError", StringComparison.Ordinal);

    private static AuthResult Network => new(false, "Couldn't reach the server. Try again.");
    private static AuthResult Generic => new(false, "Something went wrong. Try again.");

    private static async Task<string> ErrorMessageAsync(HttpResponseMessage r)
    {
        string? code = null;
        try { code = (await r.Content.ReadFromJsonAsync<ErrorResponse>())?.Error; }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or HttpRequestException) { }
        return code switch
        {
            AuthErrors.InviteInvalid => "This code is wrong or has expired. Ask for a new one.",
            AuthErrors.HouseholdFull => "This household is full.",
            AuthErrors.SignupsClosed => "New households can't be created here. Ask a member for an invite code.",
            _ => "Something went wrong. Try again."
        };
    }
}
