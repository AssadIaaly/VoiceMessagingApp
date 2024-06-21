using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ClientApp;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });


// Register Blazored LocalStorage
builder.Services.AddBlazoredLocalStorage();

builder.Services.AddCascadingAuthenticationState();

// Add MudBlazor services
builder.Services.AddMudServices();
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider => provider.GetRequiredService<CustomAuthStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthService>();

// Register SignalR UserService
builder.Services.AddSingleton<UserService>();

builder.Services.AddLogging();

var app = builder.Build();

var userService = app.Services.GetRequiredService<UserService>();
var localStorage = app.Services.GetRequiredService<ILocalStorageService>();
var navigationManager = app.Services.GetRequiredService<NavigationManager>();
await userService.InitializeAsync(navigationManager.BaseUri,localStorage);

// if (!string.IsNullOrEmpty(userService.GetCurrentUserName()))
// {
//     await userService.StartConnectionAsync();
// }

await app.RunAsync();