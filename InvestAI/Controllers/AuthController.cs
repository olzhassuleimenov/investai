using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IMongoCollection<User> _users;
    private readonly IConfiguration _config;

    public AuthController(IMongoDatabase db, IConfiguration config)
    {
        _users = db.GetCollection<User>("Users");
        _config = config;
    }

    // POST /api/v1/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterViewModel model)
    {
        var existing = await _users.Find(u => u.Email == model.Email.ToLower()).FirstOrDefaultAsync();
        if (existing != null)
            return BadRequest(new { error = "Пользователь с таким email уже существует" });

        var user = new User
        {
            Email = model.Email.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
            Name = model.Name,
            CreatedAt = DateTime.UtcNow
        };

        await _users.InsertOneAsync(user);

        var token = GenerateToken(user);
        return StatusCode(201, new
        {
            token,
            user = new { id = user.Id, email = user.Email, name = user.Name }
        });
    }

    // POST /api/v1/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginViewModel model)
    {
        var user = await _users.Find(u => u.Email == model.Email.ToLower()).FirstOrDefaultAsync();
        if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            return Unauthorized(new { error = "Неверный email или пароль" });

        var token = GenerateToken(user);
        return Ok(new
        {
            token,
            user = new { id = user.Id, email = user.Email, name = user.Name }
        });
    }

    // GET /api/v1/auth/me
    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null) return NotFound(new { error = "Пользователь не найден" });

        return Ok(new { id = user.Id, email = user.Email, name = user.Name });
    }

    private string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name)
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(int.Parse(_config["Jwt:ExpiresInDays"])),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}