using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using Blazored.LocalStorage;
using System.IdentityModel.Tokens.Jwt;

public class UserService
{
    private HubConnection _hubConnection;
    private readonly IJSRuntime _jsRuntime;
    public UserInfo CurrentUser { get; private set; }
    private string _callingClientConnectionId = null;
    private string _answeringClientConnectionId = null;
    public List<List<UserInfo>> ConnectedUsers { get; private set; } = new List<List<UserInfo>>();

    public event Action OnUsersUpdated;
    public event Action<string> OnCallStarted;
    public event Action<string> OnCallEnded;
    public event Action<string, bool, string> OnIncomingCall;
    public event Action<string> OnCallRejected;
    public event Action<string,string> OnCallAnswered;

    public UserService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync(string baseUri, ILocalStorageService localStorage)
    {
        var accessToken = await localStorage.GetItemAsync<string>("authToken");
        if (string.IsNullOrWhiteSpace(accessToken))
            return;
        DecodeJwtToken(accessToken);
        var uri = new Uri(baseUri);
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{uri.Scheme}://{uri.Host}:5171/userHub", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(accessToken);
            })
            .Build();

        _hubConnection.On<List<List<UserInfo>>>("UpdateUsers", (users) =>
        {
            ConnectedUsers = users;
            OnUsersUpdated?.Invoke();
        });

        _hubConnection.On<string, bool, string>("IncomingCall", (caller, useVideo,  connectionId) =>
        {
            Console.WriteLine($"Incoming call from {caller}");
            OnIncomingCall?.Invoke(caller, useVideo, connectionId);
        });
        
        _hubConnection.On<string>("CallRejected", (caller) =>
        {
            Console.WriteLine($"Rejected call from {caller}");
            OnCallRejected?.Invoke(caller);
        });

        _hubConnection.On<string, string>("ReceiveOffer", async (user, offer) =>
        {
            Console.WriteLine($"Received offer from {user}");
            await _jsRuntime.InvokeVoidAsync("webrtc.receiveOffer", user, offer);
            OnCallStarted?.Invoke(user);
        });

        _hubConnection.On<string, string>("ReceiveAnswer", async (user, answer) =>
        {
            Console.WriteLine($"Received answer from {user}");
            await _jsRuntime.InvokeVoidAsync("webrtc.receiveAnswer", user, answer);
        });

        _hubConnection.On<string, string>("ReceiveIceCandidate", async (user, candidate) =>
        {
            Console.WriteLine($"Received ICE candidate from {user}");
            await _jsRuntime.InvokeVoidAsync("webrtc.receiveIceCandidate", user, candidate);
        });

        _hubConnection.On<string>("EndCall", async (user) =>
        {
            Console.WriteLine($"Call ended by {user}");
            await _jsRuntime.InvokeVoidAsync("webrtc.endCall");
            OnCallEnded?.Invoke(user);
        });
        
    _hubConnection.On<string,string>("CallAnswered", async (user,answeringClientConnectionId) =>
        {
            Console.WriteLine($"Call answered by {user}" +  " ConnectionId: " + answeringClientConnectionId);
            _answeringClientConnectionId = answeringClientConnectionId;
            OnCallAnswered?.Invoke(user,answeringClientConnectionId);
        });

        await _hubConnection.StartAsync();
        CurrentUser.ConnectionId = _hubConnection?.ConnectionId;
        Console.WriteLine("SignalR connection started");
    }
    

    public async Task InitiateCall(string userName, bool useVideo)
    {
        await _hubConnection.InvokeAsync("InitiateCall", userName, useVideo);
    }

    public async Task RejectCall(string userName)
    {
        await _hubConnection.InvokeAsync("RejectCall", userName);
    }

    public async Task ConnectToUser(string userName, bool useVideo, string callingUserConnectionId)
    {
        _callingClientConnectionId = callingUserConnectionId;
        await _jsRuntime.InvokeVoidAsync("webrtc.startCall", userName, useVideo);
    }

    public async Task EndCall(string userName)
    {
        await _hubConnection.InvokeAsync("EndCall", userName);
    }

    [JSInvokable]
    public async Task SendOffer(string userName, string offer)
    {
        Console.WriteLine($"Sending offer to {userName}");
        if (!string.IsNullOrWhiteSpace(_callingClientConnectionId))
            await _hubConnection.InvokeAsync("SendOffer", userName,_callingClientConnectionId, offer);
    }

    [JSInvokable]
    public async Task SendAnswer(string userName, string answer)
    {
        Console.WriteLine($"Sending answer to {userName}, ConnectionId: {_answeringClientConnectionId}");
        if (!string.IsNullOrWhiteSpace(_answeringClientConnectionId))
            await _hubConnection.InvokeAsync("SendAnswer", userName,_answeringClientConnectionId, answer);
    }

    [JSInvokable]
    public async Task SendIceCandidate(string userName, string candidate)
    {
        Console.WriteLine($"Sending ICE candidate to {userName}");
        if (!string.IsNullOrWhiteSpace(_callingClientConnectionId))
            await _hubConnection.InvokeAsync("SendIceCandidate", userName,_callingClientConnectionId, candidate);
        if (!string.IsNullOrWhiteSpace(_answeringClientConnectionId))
            await _hubConnection.InvokeAsync("SendIceCandidate", userName,_answeringClientConnectionId, candidate);
    }

    public void ClearCallingClientConnectionId() => _callingClientConnectionId = null;
    public void ClearAnsweringClientConnectionId() => _answeringClientConnectionId = null;
    
    private void DecodeJwtToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var email = jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value;
        var name = jwtToken.Claims.FirstOrDefault(c => c.Type == "name")?.Value;

        Console.WriteLine($"Decoded token: {email}, {name}");
        CurrentUser = new UserInfo
        {
            UserName = email,
            Name = name
        };
    }

    public async Task AnswerCall(string callingUser)
    {
        await _hubConnection.InvokeAsync("AnswerCall", callingUser);
    }
}


