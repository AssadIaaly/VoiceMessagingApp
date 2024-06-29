using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

public class UserHub : Hub
{
    // Use a thread-safe collection to store user info
    private static ConcurrentDictionary<string, List<UserInfo>> ConnectedUsers = new ConcurrentDictionary<string, List<UserInfo>>();

    public override Task OnConnectedAsync()
    {
        var userName = Context?.User?.Identity?.Name;
        var connectionId = Context?.ConnectionId;
        var userDisplayName = Context?.User?.Claims?.FirstOrDefault(c => c.Type == "name")?.Value ?? userName;
        
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new Exception("User is not authenticated");
            return Task.CompletedTask;
        }
        
        Console.WriteLine($"User connected: {userName} (display name: {userDisplayName}) with connection ID: {connectionId}");

        var userInfo = new UserInfo
        {
            UserName = userName,
            Name = userDisplayName,
            ConnectionId = connectionId,
            ClientType = GetClientType(Context.GetHttpContext().Request.Headers["User-Agent"])
        };
        if (ConnectedUsers.ContainsKey(userName))
        {
            ConnectedUsers[userName].Add(userInfo);
        }
        else
        {
            ConnectedUsers[userName] = new List<UserInfo> { userInfo };
        }

        Clients.All.SendAsync("UpdateUsers", ConnectedUsers.Values);

        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception exception)
    {
        var userName = Context.User.Identity.Name;
        var connectionId = Context.ConnectionId;
        
        // Remove the user from the list of connected users
        if (ConnectedUsers.ContainsKey(userName))
        {
            var user = ConnectedUsers[userName].FirstOrDefault(u => u.ConnectionId == connectionId);
            if (user != null)
            {
                ConnectedUsers[userName].Remove(user);
            }
            if (ConnectedUsers[userName].Count == 0)
            {
                ConnectedUsers.TryRemove(userName, out _);
                EndCall(userName);
            }
        }

        Clients.All.SendAsync("UpdateUsers", ConnectedUsers.Values);

        return base.OnDisconnectedAsync(exception);
    }

    public Task InitiateCall(string userName, bool useVideo)
    {
        if (ConnectedUsers.TryGetValue(userName, out var userInfo))
        {
            // Get the connectionId
            var connectionId = Context.ConnectionId;
            Console.WriteLine($"Initiating call from {Context.User.Identity.Name} to {userName}" + " with connectionId " + connectionId);
            // send call to all clients of the user
            return Clients.Clients(userInfo.Select(u => u.ConnectionId).ToList()).SendAsync("IncomingCall", Context.User.Identity.Name, useVideo, connectionId);
            //return Clients.Client(userInfo.ConnectionId).SendAsync("IncomingCall", Context.User.Identity.Name, useVideo);
        }
        return Task.CompletedTask;
    }

    public async Task RejectCall(string userName)
    {
        if (ConnectedUsers.TryGetValue(userName, out var userInfo))
        {
            Console.WriteLine($"Rejecting call from {Context.User.Identity.Name} to {userName}");
            await Clients.Clients(userInfo.Select(u => u.ConnectionId).ToList()).SendAsync("CallRejected", Context.User.Identity.Name);
        }
        // Get also other connections of the same same user and reject the call
        if (ConnectedUsers.TryGetValue(Context.User.Identity.Name, out var userInfos))
        {
                await Clients.Clients(userInfos.Select(u => u.ConnectionId)).SendAsync("CallRejected", Context.User.Identity.Name);
        }
    }

    public async Task SendOffer(string userName, string connectionId, string offer)
    {
        if (ConnectedUsers.TryGetValue(userName, out var userInfo))
        {
            Console.WriteLine($"Sending offer from {Context.User.Identity.Name} to {userName}");
            // if userInfo does not contains conectionId throw error
            if (userInfo.All(i => i.ConnectionId != connectionId))
                throw new Exception("ConnectionId does not exist!");
            await Clients.Client(connectionId).SendAsync("ReceiveOffer",Context.User.Identity.Name, offer);
        }
    }

    public async Task SendAnswer(string userName,string connectionId, string answer)
    {
        Console.WriteLine($"PreSending answer from {Context.User.Identity.Name} to {userName}");
        if (ConnectedUsers.TryGetValue(userName, out var userInfo))
        {
            Console.WriteLine($"Sending answer from {Context.User.Identity.Name} to {userName}");
            if (userInfo.All(i => i.ConnectionId != connectionId))
            {
                Console.WriteLine("ConnectionId does not exist!");
                return;
            }
            await Clients.Client(connectionId).SendAsync("ReceiveAnswer", Context.User.Identity.Name, answer);
        }
    }

    public async Task SendIceCandidate(string userName, string connectionId,string candidate)
    {
        if (ConnectedUsers.TryGetValue(userName, out var userInfo))
        {
            Console.WriteLine($"Sending ICE candidate from {Context.User.Identity.Name} to {userName}");
            if (userInfo.All(i => i.ConnectionId != connectionId))
                throw new Exception("ConnectionId does not exist!");
            await Clients.Client(connectionId).SendAsync("ReceiveIceCandidate", Context.User.Identity.Name, candidate);
        }
    }

    public async Task EndCall(string userName)
    {
        if (ConnectedUsers.TryGetValue(userName, out var userInfo))
        {
            Console.WriteLine($"Ending call with {userName}");
            var users = userInfo.Select(u => u.ConnectionId).ToList();
            if (users.Count > 0)
                await Clients.Clients(users).SendAsync("EndCall", Context.User.Identity.Name);
            //return Clients.Client(userInfo.ConnectionId).SendAsync("EndCall", Context.User.Identity.Name);
        }
    }
    public async Task AnswerCall(string callingUser)
    {
        if (ConnectedUsers.TryGetValue(callingUser, out var userInfos))
        {
            Console.WriteLine("Answering call from " + callingUser + " to " + Context.User.Identity.Name + " with connectionId " + Context.ConnectionId);
            var connectionId = Context.ConnectionId;
            await Clients.Clients(userInfos.Select(i=>i.ConnectionId)).SendAsync("CallAnswered", Context.User.Identity.Name, connectionId);
        }
    }
    private string GetClientType(string userAgent)
    {
        if (userAgent.Contains("Mobile"))
        {
            return "Mobile";
        }
        else if (userAgent.Contains("Windows") || userAgent.Contains("Macintosh") || userAgent.Contains("Linux"))
        {
            return "Desktop";
        }
        else
        {
            return "Unknown";
        }
    }
}

public class UserInfo
{
    public string UserName { get; set; }
    public string Name { get; set; }
    public string ConnectionId { get; set; }
    public string ClientType { get; set; }
}
