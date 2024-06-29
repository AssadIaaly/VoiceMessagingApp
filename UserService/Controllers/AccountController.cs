using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UserService.Data;

namespace UserService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        : ControllerBase
    {
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            var user = new ApplicationUser() { UserName = model.Email, Email = model.Email };
            var result = await userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                return Ok(new { Message = "User created successfully" });
            }

            return BadRequest(result.Errors);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            var result = await signInManager.PasswordSignInAsync(model.Email, model.Password, false, false);

            if (result.Succeeded)
            {
                var user = await userManager.FindByEmailAsync(model.Email);
                var token = GenerateJwtToken(user);
                return Ok(new { Token = token });
            }

            return Unauthorized();
        }

        private string GenerateJwtToken(ApplicationUser user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes("this is my custom Secret key for authentication."); // Use a secure key

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim("name", user.Name ?? string.Empty)
                }),
                Expires = DateTime.UtcNow.AddDays(30),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
        
        [HttpGet("userinfo")]
        public async Task<IActionResult> GetUserInfo()
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var userInfo = new UserUpdateInfo
            {
                Name = user.Name,
                Email = user.Email
            };

            return Ok(userInfo);
        }

        [HttpPost("update")]
        public async Task<IActionResult> UpdateUserInfo([FromBody] UserUpdateInfo model)
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            user.Name = model.Name;
            var result = await userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                return Ok();
            }

            return BadRequest(result.Errors);
        }
    }

    public class RegisterModel
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class LoginModel
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class UserUpdateInfo
    {
        public string Name { get; set; }
        public string Email { get; set; }
    }
}
