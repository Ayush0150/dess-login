using Microsoft.AspNetCore.Antiforgery;

namespace backend.Middleware;

public class ApiSecurityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.Headers.CacheControl = "no-cache, no-store";
            context.Response.Headers.Pragma = "no-cache";
            if (HttpMethods.IsPost(context.Request.Method))
            {
                try
                {
                    await antiforgery.ValidateRequestAsync(context);
                }
                catch (AntiforgeryValidationException)
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(
                        new { message = "Invalid security token. Please try again." });
                    return;
                }
            }
        }
        await next(context);
    }
}
