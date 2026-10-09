using System.Net.Http.Headers;

namespace MealPlanner.App.Services;

/// <summary>Adds the bearer access token (refreshed when near expiry) to API calls.</summary>
public sealed class TokenHandler : DelegatingHandler
{
    private readonly AuthService _auth;

    public TokenHandler(AuthService auth) => _auth = auth;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _auth.GetAccessTokenAsync();
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}
