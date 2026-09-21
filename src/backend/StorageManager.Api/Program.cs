using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StorageManager.Api;
using StorageManager.Core;
using StorageManager.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Configure ConnectionStrings__Default para o PostgreSQL.");
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connection));
builder.Services.AddIdentityCore<AppUser>(o =>
{
    o.User.RequireUniqueEmail = true;
    o.Password.RequiredLength = 12;
    o.Password.RequireNonAlphanumeric = true;
    o.Password.RequireDigit = true;
    o.Password.RequireUppercase = true;
    o.Password.RequireLowercase = true;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<AppDbContext>().AddSignInManager().AddDefaultTokenProviders();
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("StorageManager");
var keysPath = builder.Configuration["DataProtection:Path"];
if (!string.IsNullOrWhiteSpace(keysPath)) dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddCookie(IdentityConstants.ApplicationScheme, o =>
{
    o.Cookie.Name = "StorageManager.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = false;
    o.Events = new CookieAuthenticationEvents
    {
        OnValidatePrincipal = Security.ValidateSession,
        OnRedirectToLogin = context => Security.Problem(401, "unauthenticated", "Entre na sua conta para continuar.").ExecuteAsync(context.HttpContext),
        OnRedirectToAccessDenied = context => Security.Problem(403, "forbidden", "Você não tem permissão para esta ação.").ExecuteAsync(context.HttpContext)
    };
});
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("Write", p => p.RequireAuthenticatedUser().RequireRole(Roles.Admin, Roles.Operator));
    o.AddPolicy("Admin", p => p.RequireAuthenticatedUser().RequireRole(Roles.Admin));
});
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.Name = "StorageManager.Csrf";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 20,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    }));
    o.OnRejected = (context, _) => new ValueTask(Security.Problem(429, "rate_limited", "Muitas tentativas. Aguarde um minuto e tente novamente.").ExecuteAsync(context.HttpContext));
});
builder.Services.AddOpenApi();
builder.Services.AddScoped<InventoryService>();

var app = builder.Build();
if (args.Contains("--migrate", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    return;
}

app.Use(async (context, next) =>
{
    try
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "same-origin";
        if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
        if (!context.Request.IsHttps && (!app.Environment.IsDevelopment() || context.Connection.RemoteIpAddress is { } ip && !IPAddress.IsLoopback(ip)))
        {
            await Security.Problem(400, "https_required", "Use HTTPS. HTTP é aceito somente em desenvolvimento local.").ExecuteAsync(context);
            return;
        }
        await next(context);
    }
    catch (DomainException ex) { await Security.Problem(ex.Status, ex.Code, ex.Message, ex.Errors).ExecuteAsync(context); }
    catch (AntiforgeryValidationException) { await Security.Problem(400, "csrf_invalid", "Sua proteção de sessão expirou. Atualize a página e tente novamente.").ExecuteAsync(context); }
    catch (BadHttpRequestException) { await Security.Problem(400, "invalid_request", "Verifique o formato dos dados enviados.").ExecuteAsync(context); }
    catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
    { await Security.Problem(409, "duplicate", "Já existe um registro com esse código ou e-mail.").ExecuteAsync(context); }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        app.Logger.LogError(ex, "Request failed, trace {TraceId}", context.TraceIdentifier);
        await Security.Problem(500, "server_error", "Não foi possível concluir a operação. Tente novamente.").ExecuteAsync(context);
    }
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/v1") && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method))
        await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
    await next(context);
});

app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
{
    try
    {
        var ready = await db.Database.CanConnectAsync(ct) && !(await db.Database.GetPendingMigrationsAsync(ct)).Any();
        return ready ? Results.Ok(new { status = "Healthy" }) : Security.Problem(503, "not_ready", "O serviço não está pronto.");
    }
    catch { return Security.Problem(503, "not_ready", "O serviço não está pronto."); }
}).AllowAnonymous();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
var api = app.MapGroup("/api/v1");
api.MapIdentityEndpoints();
api.MapCatalogEndpoints();
api.MapInventoryEndpoints();
app.MapFallback("/api/{**path}", () =>
    Security.Problem(404, "endpoint_not_found", "O endpoint solicitado não existe."));
if (File.Exists(Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html")))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}
await app.RunAsync();

public partial class Program;
