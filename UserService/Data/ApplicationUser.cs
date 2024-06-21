using Microsoft.AspNetCore.Identity;

namespace UserService.Data;

public class ApplicationUser : IdentityUser
{
    public string Name { get; set; }
}