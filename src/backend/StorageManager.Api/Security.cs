using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StorageManager.Infrastructure;

namespace StorageManager.Api;

public static class Security
{
    public static Guid Org(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("organization_id")!);
    public static Guid Actor(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static string ActorName(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Name) ?? "";

    public static ClaimsPrincipal Principal(AppUser user) => new(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.Name),
        new Claim(ClaimTypes.Email, user.Email ?? ""),
        new Claim(ClaimTypes.Role, user.Role),
        new Claim("organization_id", user.OrganizationId.ToString()),
        new Claim("security_stamp", user.SecurityStamp ?? "")
    ], IdentityConstants.ApplicationScheme));

    public static async Task ValidateSession(CookieValidatePrincipalContext context)
    {
        var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var org = context.Principal?.FindFirstValue("organization_id");
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        AppUser? user = null;
        if (Guid.TryParse(id, out var userId) && Guid.TryParse(org, out var orgId))
            user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.OrganizationId == orgId && x.IsActive, context.HttpContext.RequestAborted);
        if (user is null || context.Principal is null || user.SecurityStamp != context.Principal.FindFirstValue("security_stamp") || user.Role != context.Principal.FindFirstValue(ClaimTypes.Role))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    }

    public static IResult Problem(int status, string code, string detail, IDictionary<string, string[]>? errors = null)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = code };
        if (errors is not null) extensions["errors"] = errors;
        return Results.Problem(statusCode: status, title: status switch
        {
            400 => "Dados inválidos",
            401 => "Autenticação necessária",
            403 => "Acesso negado",
            404 => "Não encontrado",
            409 => "Conflito",
            429 => "Muitas tentativas",
            503 => "Serviço indisponível",
            _ => "Erro ao processar solicitação"
        }, detail: detail, extensions: extensions);
    }
}
