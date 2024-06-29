public class UserInfo
{
    public string UserName { get; set; } // This should be the email used to identify the user
    public string Name { get; set; }     // This is the new property to hold the name of the user
    public int ConnectionCount { get; set; }
    public string ClientType { get; set; }
    public string ConnectionId { get; set; }
}