using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using System.Collections.Generic;
using System.Threading.Tasks;
using Blazored.LocalStorage;

public class UserService(IJSRuntime jsRuntime)
{
    private HubConnection _hubConnection;

    public List<string> ConnectedUsers { get; private set; } = new List<string>();

    public event Action OnUsersUpdated;
    public event Action<string> OnCallStarted;
    public event Action<string> OnCallEnded;

    public async Task InitializeAsync(string userServiceBaseUrl, ILocalStorageService localStorage)
    {
        var accessToken = await localStorage.GetItemAsync<string>("authToken");
        var uri = new Uri(userServiceBaseUrl);
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{uri.Scheme}://{uri.Host}:5171/userHub", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(accessToken);
            })
            .Build();

         _hubConnection.On<List<string>>("UpdateUsers", (users) =>
        {
            ConnectedUsers = users;
            OnUsersUpdated?.Invoke();
        });

        _hubConnection.On<string, string>("ReceiveOffer", async (user, offer) =>
        {
            Console.WriteLine($"Received offer from {user}");
            await jsRuntime.InvokeVoidAsync("webrtc.receiveOffer", user, offer);
            OnCallStarted?.Invoke(user);
        });

        _hubConnection.On<string, string>("ReceiveAnswer", async (user, answer) =>
        {
            Console.WriteLine($"Received answer from {user}");
            await jsRuntime.InvokeVoidAsync("webrtc.receiveAnswer", user, answer);
        });

        _hubConnection.On<string, string>("ReceiveIceCandidate", async (user, candidate) =>
        {
            Console.WriteLine($"Received ICE candidate from {user}");
            await jsRuntime.InvokeVoidAsync("webrtc.receiveIceCandidate", user, candidate);
        });

        _hubConnection.On<string>("EndCall", async (user) =>
        {
            Console.WriteLine($"Call ended by {user}");
            await jsRuntime.InvokeVoidAsync("webrtc.endCall");
            OnCallEnded?.Invoke(user);
        });

        await _hubConnection.StartAsync();
        Console.WriteLine("SignalR connection started");
    }

    public async Task ConnectToUser(string userName)
    {
        await jsRuntime.InvokeVoidAsync("webrtc.startCall", userName);
    }

    public async Task EndCall(string userName)
    {
        await _hubConnection.InvokeAsync("EndCall", userName);
    }

    [JSInvokable]
    public async Task SendOffer(string userName, string offer)
    {
        Console.WriteLine($"Sending offer to {userName}");
        await _hubConnection.InvokeAsync("SendOffer", userName, offer);
    }

    [JSInvokable]
    public async Task SendAnswer(string userName, string answer)
    {
        Console.WriteLine($"Sending answer to {userName}");
        await _hubConnection.InvokeAsync("SendAnswer", userName, answer);
    }

    [JSInvokable]
    public async Task SendIceCandidate(string userName, string candidate)
    {
        Console.WriteLine($"Sending ICE candidate to {userName}");
        await _hubConnection.InvokeAsync("SendIceCandidate", userName, candidate);
    }
}
