using System.Net;
using System.Net.Http.Json;
using MealPlanner.App.Storage;
using MealPlanner.Core;
using MealPlanner.Core.Sync;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace MealPlanner.App.Services;

/// <summary>
/// Pushes the outbox and pulls changes through POST api/sync (architecture › SyncService).
/// Triggers: app start, document visible, online, <see cref="RequestSync"/> (1 s debounce) and a 10 s timer
/// while the Shopping page and the document are visible. One sync at a time; a request during a run queues exactly one more.
/// </summary>
public sealed class SyncService : ISyncTrigger, IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan[] Backoff =
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30)];
    private static readonly TimeSpan BackoffMax = TimeSpan.FromSeconds(60);

    private enum Outcome { Ok, NetworkFailure, Rejected, Expired, Aborted }

    private readonly AppState _state;
    private readonly IStore _store;
    private readonly AuthService _auth;
    private readonly ConnectivityService _net;
    private readonly NavigationManager _nav;
    private readonly IJSRuntime _js;
    private readonly IServiceProvider _services;

    private HttpClient? _api;
    private Timer? _debounce;
    private Timer? _retry;
    private Timer? _poll;
    private DotNetObjectReference<SyncService>? _self;

    private bool _running;
    private bool _pending;
    private bool _shoppingVisible;
    private bool _documentVisible = true;
    private bool _started;
    private int _failures;
    private bool _lastAttemptFailed;
    private DateTime? _lastSuccess;
    private bool _bannerShown;

    // PlanService is resolved lazily: it depends on EntityWriter → ISyncTrigger, which is this service,
    // so taking it in the constructor would recurse forever through the ISyncTrigger factory.
    public SyncService(AppState state, IStore store, AuthService auth, ConnectivityService net,
        NavigationManager nav, IJSRuntime js, IServiceProvider services)
    {
        _state = state;
        _store = store;
        _auth = auth;
        _net = net;
        _nav = nav;
        _js = js;
        _services = services;
    }

    public event Action? OnChange;

    private PlanService Plan => _services.GetRequiredService<PlanService>();

    private HttpClient Api => _api ??= _services.GetRequiredKeyedService<HttpClient>(AuthService.ApiClientKey);

    /// <summary>True when offline, or when the last attempt failed and there has been no success for over 60 s.</summary>
    public bool ShowOfflineBanner =>
        _auth.IsSignedIn &&
        (!_net.IsOnline || (_lastAttemptFailed && (_lastSuccess is null || DateTime.UtcNow - _lastSuccess > StaleAfter)));

    /// <summary>Subscribes to visibility, connectivity and sign-in changes, then starts the app-start sync (not awaited).</summary>
    public async Task StartAsync()
    {
        if (_started) return;
        _started = true;
        _net.OnChange += OnConnectivityChanged;
        _auth.OnChange += OnAuthChanged;
        _self = DotNetObjectReference.Create(this);
        try
        {
            _documentVisible = await _js.InvokeAsync<bool>("mealPlanner.visibility.isVisible");
            await _js.InvokeVoidAsync("mealPlanner.visibility.onVisibilityChange", _self);
        }
        catch (JSException) { _documentVisible = true; }
        UpdatePoll();
        if (_auth.IsSignedIn) _ = SyncNowAsync();
    }

    public void SetShoppingVisible(bool visible)
    {
        _shoppingVisible = visible;
        UpdatePoll();
    }

    /// <summary>Debounced (1 s) sync request after a local change.</summary>
    public void RequestSync()
    {
        if (!_auth.IsSignedIn) return;
        _debounce?.Dispose();
        _debounce = new Timer(_ => _ = SyncNowAsync(), null, Debounce, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Runs a sync now. If one is running, exactly one more runs after it.</summary>
    public async Task SyncNowAsync()
    {
        if (!_auth.IsSignedIn || !_net.IsOnline) return;
        if (_running) { _pending = true; return; }
        _running = true;
        try
        {
            do
            {
                _pending = false;
                _debounce?.Dispose();
                _retry?.Dispose();
                if (!_auth.IsSignedIn || !_net.IsOnline) break;
                var outcome = await RunOnceAsync();
                HandleOutcome(outcome);
            } while (_pending);
        }
        finally { _running = false; }
    }

    private void HandleOutcome(Outcome outcome)
    {
        switch (outcome)
        {
            case Outcome.Ok:
                _failures = 0;
                _lastAttemptFailed = false;
                _lastSuccess = DateTime.UtcNow;
                break;
            case Outcome.NetworkFailure:
                _lastAttemptFailed = true;
                var delay = _failures < Backoff.Length ? Backoff[_failures] : BackoffMax;
                _failures++;
                _retry?.Dispose();
                _retry = new Timer(_ => _ = SyncNowAsync(), null, delay, Timeout.InfiniteTimeSpan);
                break;
            case Outcome.Rejected:
                // 400: our request was invalid; retrying the same thing won't help. Wait for the next trigger.
                break;
            case Outcome.Expired:
                _failures = 0;
                _lastAttemptFailed = false;
                break;
        }
        UpdateBanner();
    }

    private async Task<Outcome> RunOnceAsync()
    {
        var data = _state.Data;
        var ids = data.Outbox.Keys.ToList();
        var seqs = data.Outbox.ToDictionary(e => e.Key, e => e.Value.Seq);
        var chunks = ids.Count == 0 ? [ids] : ids.Chunk(SyncLimits.MaxChanges).Select(c => c.ToList()).ToList();

        foreach (var chunk in chunks)
        {
            var changes = SyncMerge.BuildChanges(chunk, _state.AllEntities());
            var (status, response) = await PostAsync(new SyncRequest(data.LastVersion, changes));
            if (!ReferenceEquals(data, _state.Data) || !_auth.IsSignedIn) return Outcome.Aborted;

            if (status == HttpStatusCode.Unauthorized)
            {
                var refreshed = await _auth.TryRefreshAsync();
                if (refreshed is null) return Outcome.NetworkFailure;
                if (refreshed == true) (status, response) = await PostAsync(new SyncRequest(data.LastVersion, changes));
                if (!ReferenceEquals(data, _state.Data) || !_auth.IsSignedIn) return Outcome.Aborted;
                if (refreshed == false || status == HttpStatusCode.Unauthorized)
                {
                    await _auth.ExpireSessionAsync();
                    _nav.NavigateTo("/welcome?reason=expired", replace: true);
                    return Outcome.Expired;
                }
            }

            if (status == HttpStatusCode.BadRequest)
            {
                Console.Error.WriteLine("Sync rejected by the server (400).");
                return Outcome.Rejected;
            }
            if (response is null) return Outcome.NetworkFailure;

            foreach (var id in chunk)
            {
                if (data.Outbox.TryGetValue(id, out var entry) && seqs.TryGetValue(id, out var seq) && entry.Seq == seq)
                    data.Outbox.Remove(id);
            }

            var result = SyncMerge.Apply(response, _state.AllEntities(), data.Outbox.Keys.ToHashSet());
            foreach (var e in result.Upserts) _state.Put(e);
            foreach (var e in result.Removals) _state.Remove(e);
            data.LastVersion = response.ServerVersion;
            await _store.SaveAsync(data);
            _state.NotifyChanged();
        }

        try { await Plan.PruneAsync(); }
        catch (Exception ex) { Console.Error.WriteLine($"Prune after sync failed: {ex.Message}"); }
        return Outcome.Ok;
    }

    private async Task<(HttpStatusCode Status, SyncResponse? Response)> PostAsync(SyncRequest request)
    {
        try
        {
            using var cts = new CancellationTokenSource(RequestTimeout);
            using var http = await Api.PostAsJsonAsync("api/sync", request, CoreJson.Options, cts.Token);
            if (http.IsSuccessStatusCode)
            {
                var body = await http.Content.ReadFromJsonAsync<SyncResponse>(CoreJson.Options, cts.Token);
                return (http.StatusCode, body);
            }
            // 5xx counts as a network failure (null response); 400 and 401 are handled by the caller.
            return (http.StatusCode, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or System.Text.Json.JsonException)
        {
            return (HttpStatusCode.ServiceUnavailable, null);
        }
    }

    [JSInvokable]
    public void OnVisibilityChanged(bool visible)
    {
        _documentVisible = visible;
        UpdatePoll();
        if (visible) _ = SyncNowAsync();
    }

    private void OnConnectivityChanged()
    {
        UpdateBanner();
        if (_net.IsOnline) _ = SyncNowAsync();
    }

    private void OnAuthChanged()
    {
        if (_auth.IsSignedIn) return;
        // Signed out (or session expired): stop everything and forget sync state.
        _debounce?.Dispose();
        _retry?.Dispose();
        _debounce = _retry = null;
        _pending = false;
        _failures = 0;
        _lastAttemptFailed = false;
        _lastSuccess = null;
        UpdatePoll();
        UpdateBanner();
    }

    private void UpdatePoll()
    {
        var want = _shoppingVisible && _documentVisible && _auth.IsSignedIn;
        if (want && _poll is null)
            _poll = new Timer(_ => _ = SyncNowAsync(), null, PollInterval, PollInterval);
        else if (!want && _poll is not null)
        {
            _poll.Dispose();
            _poll = null;
        }
    }

    private void UpdateBanner()
    {
        var show = ShowOfflineBanner;
        if (show == _bannerShown) return;
        _bannerShown = show;
        OnChange?.Invoke();
    }

    public void Dispose()
    {
        _debounce?.Dispose();
        _retry?.Dispose();
        _poll?.Dispose();
        _self?.Dispose();
        _net.OnChange -= OnConnectivityChanged;
        _auth.OnChange -= OnAuthChanged;
    }
}
