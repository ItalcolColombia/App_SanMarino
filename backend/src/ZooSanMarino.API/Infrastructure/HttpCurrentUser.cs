// src/ZooSanMarino.API/Infrastructure/HttpCurrentUser.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Infrastructure;

public sealed class HttpCurrentUser : ICurrentUser
{
    public int CompanyId { get; }
    public int UserId { get; }
    public string? ActiveCompanyName { get; }

    public HttpCurrentUser(IHttpContextAccessor accessor)
    {
        var http = accessor.HttpContext;

        // SIEMPRE leer el header X-Active-Company, independientemente de la autenticación
        ActiveCompanyName = http?.Request.Headers["X-Active-Company"].FirstOrDefault();

        if (http?.User?.Identity?.IsAuthenticated == true)
        {
            // CompanyId: admite varios nombres de claim
            var companyClaim =
                http.User.FindFirst("company_id") ??
                http.User.FindFirst("companyId") ??
                http.User.FindFirst("tenant_id");

            // UserId: típicos nombres de claim
            var userClaim =
                http.User.FindFirst(ClaimTypes.NameIdentifier) ??
                http.User.FindFirst("sub") ??
                http.User.FindFirst("user_id");

            int.TryParse(companyClaim?.Value, out var cid);
            int.TryParse(userClaim?.Value, out var uid);

            CompanyId = cid;
            UserId    = uid;
        }
        else
        {
            // Fallback para dev/local si no hay token
            CompanyId = TryGetEnvInt("DEFAULT_COMPANY_ID", 1);
            UserId    = TryGetEnvInt("DEFAULT_USER_ID", 0);
        }
    }

    private static int TryGetEnvInt(string key, int def)
        => int.TryParse(Environment.GetEnvironmentVariable(key), out var v) ? v : def;
}