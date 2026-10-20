using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AuthService authService, IAntiforgery antiforgery, IHostEnvironment environment) : ControllerBase
{
    [HttpGet("csrf")]
    public IActionResult Csrf()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });
        return NoContent();
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(
        LoginRequest request, CancellationToken cancellationToken)
    {
        var principal = await authService.AuthenticateAsync(request, cancellationToken);
        if (principal is null) return Unauthorized();

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return Ok(new { message = "Credentials verified" });
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            username = User.Identity?.Name
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "Logged out" });
    }
}
