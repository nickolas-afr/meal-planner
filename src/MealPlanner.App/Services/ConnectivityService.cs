using Microsoft.JSInterop;

namespace MealPlanner.App.Services;

/// <summary>Browser online/offline state.</summary>
public sealed class ConnectivityService
{
    private readonly IJSRuntime _js;

    public ConnectivityService(IJSRuntime js) => _js = js;

    public bool IsOnline { get; private set; } = true;

    public event Action? OnChange;

    public async Task InitializeAsync()
    {
        IsOnline = await _js.InvokeAsync<bool>("mealPlanner.isOnline");
        await _js.InvokeVoidAsync("mealPlanner.onConnectivityChange", DotNetObjectReference.Create(this));
    }

    [JSInvokable]
    public void OnConnectivityChanged(bool online)
    {
        if (online == IsOnline) return;
        IsOnline = online;
        OnChange?.Invoke();
    }
}
