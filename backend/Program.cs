using System.Security.Claims;
using System.Globalization;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16 * 1024);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Database connection is not configured.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDataProtection()
    .SetApplicationName("DESS")
    .PersistKeysToDbContext<AppDbContext>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "DESS.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.PermitLimit = 20;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    if (!app.Environment.IsDevelopment())
    {
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000";
        context.Response.Headers.ContentSecurityPolicy =
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data:; connect-src 'self'; base-uri 'self'; " +
            "frame-ancestors 'none'; form-action 'self'; object-src 'none'";
    }
    await next(context);
});
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(error => error.Run(async context =>
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new { message = "An unexpected error occurred." });
    }));
}

var hasFrontend = Directory.Exists(app.Environment.WebRootPath);
if (hasFrontend)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Headers.CacheControl = "no-cache, no-store";
        context.Response.Headers.Pragma = "no-cache";
        if (HttpMethods.IsPost(context.Request.Method))
        {
            try
            {
                await context.RequestServices.GetRequiredService<IAntiforgery>()
                    .ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { message = "Invalid security token. Please try again." });
                return;
            }
        }
    }
    await next(context);
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    var seedUsername = builder.Configuration["SeedUser:Username"];
    var seedPassword = builder.Configuration["SeedUser:Password"];
    if (!string.IsNullOrWhiteSpace(seedUsername) && !string.IsNullOrWhiteSpace(seedPassword)
        && !await db.Users.AnyAsync(user => user.Username == seedUsername))
    {
        var user = new User { Username = seedUsername };
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        user.PasswordHash = hasher.HashPassword(user, seedPassword);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }
}

app.MapGet("/api/health", () => Results.Ok(new { message = "DESS API is running" }));

app.MapGet("/api/auth/csrf", (HttpContext context, IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
    {
        HttpOnly = false,
        Secure = !app.Environment.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = "/"
    });
    return Results.NoContent();
});

app.MapPost("/api/auth/login", async (
    LoginRequest request, AppDbContext db, IPasswordHasher<User> hasher, HttpContext context) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password)
        || request.Username.Length > 100 || request.Password.Length > 1024)
    {
        return Results.BadRequest(new { message = "Enter a valid username and password." });
    }
    var user = await db.Users.FirstOrDefaultAsync(user => user.Username == request.Username);
    if (user is null) return Results.Unauthorized();

    var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
    if (result == PasswordVerificationResult.Failed) return Results.Unauthorized();
    if (result == PasswordVerificationResult.SuccessRehashNeeded)
    {
        user.PasswordHash = hasher.HashPassword(user, request.Password);
    }
    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.Username)
    };
    if (user.LastLoginAt is { } lastLogin)
    {
        claims.Add(new Claim("last_login", lastLogin.ToString("O", CultureInfo.InvariantCulture)));
    }
    user.LastLoginAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync();
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity));
    return Results.Ok(new { message = "Credentials verified" });
}).RequireRateLimiting("login");

app.MapGet("/api/auth/me", (HttpContext context) =>
    Results.Ok(new
    {
        username = context.User.Identity?.Name,
        lastLoginAt = context.User.FindFirstValue("last_login")
    }))
    .RequireAuthorization();

app.MapPost("/api/auth/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { message = "Logged out" });
});

app.Map("/api/{**path}", () => Results.NotFound());
if (hasFrontend)
{
    app.MapFallbackToFile("index.html");
}
app.Run();
