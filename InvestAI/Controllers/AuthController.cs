using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Security.Claims;

public class AuthController : Controller
{
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<Portfolio> _portfolios;

    public AuthController(IMongoDatabase db)
    {
        _users = db.GetCollection<User>("Users");
        _portfolios = db.GetCollection<Portfolio>("Portfolios");
    }

    // GET /Auth/Login
    public IActionResult Login() => View();

    // POST /Auth/Login
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _users
            .Find(u => u.Email == vm.Email.ToLower())
            .FirstOrDefaultAsync();

        if (user == null || !BCrypt.Net.BCrypt.Verify(vm.Password, user.PasswordHash))
        {
            ViewBag.Error = "Неверный email или пароль";
            return View(vm);
        }

        await SignInUser(user);
        return RedirectToAction("Dashboard", "Portfolio");
    }

    // GET /Auth/Register
    public IActionResult Register() => View();

    // POST /Auth/Register
    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var exists = await _users
            .Find(u => u.Email == vm.Email.ToLower())
            .FirstOrDefaultAsync();

        if (exists != null)
        {
            ModelState.AddModelError("", "Email уже занят");
            return View(vm);
        }

        var user = new User
        {
            Email = vm.Email.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.Password),
            Name = vm.Name,
            CreatedAt = DateTime.UtcNow
        };
        await _users.InsertOneAsync(user);

        await _portfolios.InsertOneAsync(new Portfolio
        {
            UserId = user.Id,
            Name = "Мой портфель",
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await SignInUser(user);
        return RedirectToAction("Dashboard", "Portfolio");
    }

    // GET /Auth/Logout
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        return RedirectToAction("Login");
    }

    private async Task SignInUser(User user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name,           user.Name),
            new Claim(ClaimTypes.Email,          user.Email)
        };
        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync("Cookies", principal);
    }
}