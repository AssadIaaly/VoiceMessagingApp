using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Threading.Tasks;

public class UserHub : Hub
{
    private static ConcurrentDictionary<string, string> ConnectedUsers = new ConcurrentDictionary<string, string>();

    public override Task OnConnectedAsync()
    {
        var userName = Context.User.Identity.Name;
        var connectionId = Context.ConnectionId;
        Console.WriteLine($"User connected: {userName} with connection ID: {connectionId}");

        ConnectedUsers[userName] = connectionId;
        Clients.All.SendAsync("UpdateUsers", ConnectedUsers.Keys);

        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception exception)
    {
        var userName = Context.User.Identity.Name;
        Console.WriteLine($"User disconnected: {userName}");

        ConnectedUsers.TryRemove(userName, out _);
        
        EndCall(userName);
        
        Clients.All.SendAsync("UpdateUsers", ConnectedUsers.Keys);

        return base.OnDisconnectedAsync(exception);
    }

    public Task SendOffer(string userName, string offer)
    {
        if (ConnectedUsers.TryGetValue(userName, out var connectionId))
        {
            Console.WriteLine($"Sending offer from {Context.User.Identity.Name} to {userName}");
            return Clients.Client(connectionId).SendAsync("ReceiveOffer", Context.User.Identity.Name, offer);
        }
        return Task.CompletedTask;
    }

    public Task SendAnswer(string userName, string answer)
    {
        if (ConnectedUsers.TryGetValue(userName, out var connectionId))
        {
            Console.WriteLine($"Sending answer from {Context.User.Identity.Name} to {userName}");
            return Clients.Client(connectionId).SendAsync("ReceiveAnswer", Context.User.Identity.Name, answer);
        }
        return Task.CompletedTask;
    }

    public Task SendIceCandidate(string userName, string candidate)
    {
        if (ConnectedUsers.TryGetValue(userName, out var connectionId))
        {
            Console.WriteLine($"Sending ICE candidate from {Context.User.Identity.Name} to {userName}");
            return Clients.Client(connectionId).SendAsync("ReceiveIceCandidate", Context.User.Identity.Name, candidate);
        }
        return Task.CompletedTask;
    }
    public Task EndCall(string userName)
    {
        if (ConnectedUsers.TryGetValue(userName, out var connectionId))
        {
            Console.WriteLine($"Ending call with {userName}");
            return Clients.Client(connectionId).SendAsync("EndCall", Context.User.Identity.Name);
        }
        return Task.CompletedTask;
    }
}
