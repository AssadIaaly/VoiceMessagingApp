using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;

public class AuthService(
    HttpClient httpClient,
    CustomAuthStateProvider authStateProvider,
    ILocalStorageService localStorage,
    UserService userService,
    NavigationManager navigationManager)
{
    private readonly ILocalStorageService _localStorage = localStorage;

    public async Task<bool> Register(string email, string password)
    {
        var model = new { Email = email, Password = password };
        var uri = new Uri(navigationManager.BaseUri);
        var response = await httpClient.PostAsJsonAsync($"{uri.Scheme}://{uri.Host}:5171/api/account/register", model);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> Login(string email, string password)
    {
        var model = new { Email = email, Password = password };
        var uri = new Uri(navigationManager.BaseUri);
        var response = await httpClient.PostAsJsonAsync($"{uri.Scheme}://{uri.Host}:5171/api/account/login", model);

        if (response.IsSuccessStatusCode)
        {
            var authToken = await response.Content.ReadFromJsonAsync<JwtResponse>();
            await authStateProvider.NotifyUserAuthentication(authToken.Token);
            await userService.InitializeAsync($"{uri.Scheme}://{uri.Host}:5171",localStorage);
            return true;
        }

        return false;
    }

    public async Task Logout()
    {
        await authStateProvider.NotifyUserLogout();
        //await userService.StopConnectionAsync();    }
    }
    
    public async Task<UserUpdateInfo> GetUserInfo()
    {
        var uri = new Uri(navigationManager.BaseUri);
        return await httpClient.GetFromJsonAsync<UserUpdateInfo>($"{uri.Scheme}://{uri.Host}:5171/api/account/userinfo");
    }

    public async Task UpdateUserInfo(UserUpdateInfo userInfo)
    {
        var uri = new Uri(navigationManager.BaseUri);
        var response = await httpClient.PostAsJsonAsync($"{uri.Scheme}://{uri.Host}:5171/api/account/update", userInfo);
        response.EnsureSuccessStatusCode();
    }
}

public class JwtResponse
{
    public string Token { get; set; }
}

public class UserUpdateInfo
{
    public string Name { get; set; }
    public string Email { get; set; }
}