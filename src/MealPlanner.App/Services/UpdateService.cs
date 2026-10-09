using System.Reflection;
using Microsoft.JSInterop;

namespace MealPlanner.App.Services;

/// <summary>
/// The service worker update flow (architecture › PWA). interop.js tells it when a new worker is
/// installed and waiting; AppShell shows the "New version available" banner while
/// <see cref="UpdateAvailable"/> is true.
/// </summary>
public sealed class UpdateService(IJSRuntime js)
{
    public bool UpdateAvailable { get; private set; }

    public event Action? OnChange;

    /// <summary>Registers the service worker and starts listening for updates. Called once at startup.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            // Lives for the app's lifetime (the service is a singleton).
            await js.InvokeVoidAsync("mealPlanner.sw.register", DotNetObjectReference.Create(this));
        }
        catch (JSException)
        {
        }
    }

    [JSInvokable]
    public void OnUpdateAvailable() => SetAvailable();

    /// <summary>Asks the service worker to check for a new version. True when one is available (now or already waiting).</summary>
    public async Task<bool> CheckForUpdatesAsync()
    {
        if (UpdateAvailable) return true;
        try
        {
            if (!await js.InvokeAsync<bool>("mealPlanner.sw.checkForUpdate")) return false;
        }
        catch (JSException)
        {
            return false;
        }
        SetAvailable();
        return true;
    }

    /// <summary>Activates the waiting worker (SKIP_WAITING) and reloads the page once it takes control.</summary>
    public async Task ReloadAsync() => await js.InvokeVoidAsync("mealPlanner.sw.skipWaiting");

    /// <summary>The assembly's informational version, for Settings › App.</summary>
    public string Version
    {
        get
        {
            var v = typeof(UpdateService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrWhiteSpace(v)) return "dev";
            var plus = v.IndexOf('+');
            return plus > 0 ? v[..plus] : v;
        }
    }

    private void SetAvailable()
    {
        UpdateAvailable = true;
        OnChange?.Invoke();
    }
}
