using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace MealPlanner.App.Components;

/// <summary>
/// Base for modal components (sheet, dialog): while Open, traps focus inside Root, closes on Escape and
/// returns focus to the previously focused element on close. Derived markup must set @ref="Root".
/// </summary>
public abstract class TrapBase : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime Js { get; set; } = default!;

    [Parameter] public bool Open { get; set; }

    protected ElementReference Root;

    private DotNetObjectReference<TrapBase>? _self;
    private int? _handle;
    private bool _busy;

    protected abstract Task EscapeAsync();

    [JSInvokable]
    public Task OnEscape() => EscapeAsync();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_busy) return;
        if (Open && _handle is null)
        {
            _busy = true;
            _self = DotNetObjectReference.Create<TrapBase>(this);
            _handle = await Js.InvokeAsync<int>("mealPlanner.trapFocus", Root, _self);
            _busy = false;
            if (!Open) await ReleaseAsync();
        }
        else if (!Open && _handle is not null)
        {
            await ReleaseAsync();
        }
    }

    private async Task ReleaseAsync()
    {
        if (_handle is null) return;
        var handle = _handle.Value;
        _handle = null;
        _self?.Dispose();
        _self = null;
        try { await Js.InvokeVoidAsync("mealPlanner.releaseFocus", handle); }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask DisposeAsync()
    {
        try { await ReleaseAsync(); }
        catch (InvalidOperationException) { }
    }
}
