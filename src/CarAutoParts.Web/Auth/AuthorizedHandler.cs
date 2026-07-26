using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;

namespace CarAutoParts.Web.Auth;

public sealed class AuthorizedHandler : DelegatingHandler
{
    private readonly JwtAuthStateProvider _auth;
    private readonly NavigationManager _nav;

    public AuthorizedHandler(JwtAuthStateProvider auth, NavigationManager nav)
    {
        _auth = auth;
        _nav = nav;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _auth.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            if (!path.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
            {
                await _auth.MarkUserAsLoggedOutAsync();
                var relative = _nav.ToBaseRelativePath(_nav.Uri);
                if (!relative.StartsWith("login", StringComparison.OrdinalIgnoreCase))
                    _nav.NavigateTo("/login", forceLoad: true);
            }
        }

        return response;
    }
}
