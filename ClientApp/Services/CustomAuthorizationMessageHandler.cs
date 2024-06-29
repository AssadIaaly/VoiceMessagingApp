using System.Net.Http.Headers;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;

namespace ClientApp.Services;

public class CustomAuthorizationMessageHandler : DelegatingHandler
{
    private readonly ILocalStorageService _localStorage;

    public CustomAuthorizationMessageHandler(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _localStorage.GetItemAsync<string>("authToken", cancellationToken);

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}