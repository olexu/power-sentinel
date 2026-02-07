using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace PowerSentinel.Pages.Admin;

public class LoginModel : PageModel
{
    private readonly IConfiguration _config;
    public LoginModel(IConfiguration config) { _config = config; }

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string ErrorMessage { get; private set; } = string.Empty;

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        Username = Username?.Trim() ?? string.Empty;
        Password = Password?.Trim() ?? string.Empty;

        var cfgUser = (_config["Admin:Username"] ?? string.Empty).Trim();
        var cfgPass = (_config["Admin:Password"] ?? string.Empty).Trim();

        Console.WriteLine($"Admin login attempt user='{Username}' cfgUserExists={(cfgUser.Length>0)}");

        if (Username == cfgUser && Password == cfgPass)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, Username),
                new Claim(ClaimTypes.Role, "Admin")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToPage("/Index");
        }

        ErrorMessage = "Invalid credentials";
        return Page();
    }
}
