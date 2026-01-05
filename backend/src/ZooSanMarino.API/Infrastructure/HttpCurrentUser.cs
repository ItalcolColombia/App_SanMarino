// src/ZooSanMarino.API/Infrastructure/HttpCurrentUser.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Infrastructure;

public sealed class HttpCurrentUser : ICurrentUser
{
    public int CompanyId { get; }
    public int UserId { get; }
    public int? PaisId { get; }
    public string? ActiveCompanyName { get; }
    public Guid? UserGuid { get; private set; }

    public HttpCurrentUser(IHttpContextAccessor accessor)
    {
        var http = accessor.HttpContext;

        // SIEMPRE leer el header X-Active-Company, independientemente de la autenticación
        ActiveCompanyName = http?.Request.Headers["X-Active-Company"].FirstOrDefault();

        // Leer PaisId del header X-Active-Pais
        var paisIdHeader = http?.Request.Headers["X-Active-Pais"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(paisIdHeader) && int.TryParse(paisIdHeader, out var pid))
        {
            PaisId = pid;
        }

        if (http?.User?.Identity?.IsAuthenticated == true)
        {
            // CompanyId: admite varios nombres de claim
            var companyClaim =
                http.User.FindFirst("company_id") ??
                http.User.FindFirst("companyId") ??
                http.User.FindFirst("tenant_id");

            // UserId: buscar primero el claim "user_id" (numérico), luego los otros
            var userIdClaim = http.User.FindFirst("user_id");
            Guid? userGuid = null;
            int uid = 0;
            
            // Intentar obtener el Guid del usuario desde NameIdentifier o sub
            var userGuidClaim =
                http.User.FindFirst(ClaimTypes.NameIdentifier) ??
                http.User.FindFirst("sub");
            
            if (userGuidClaim != null && Guid.TryParse(userGuidClaim.Value, out var parsedGuid))
            {
                userGuid = parsedGuid;
                // Si no existe "user_id", calcular hash desde el Guid
                if (userIdClaim == null)
                {
                    uid = Math.Abs(parsedGuid.GetHashCode());
                }
                else
                {
                    int.TryParse(userIdClaim.Value, out uid);
                }
            }
            else if (userIdClaim != null)
            {
                // Si solo tenemos el claim numérico, intentar parsearlo
                int.TryParse(userIdClaim.Value, out uid);
            }

            // PaisId: del claim o del header
            var paisClaim = http.User.FindFirst("pais_id") ?? http.User.FindFirst("paisId");

            int.TryParse(companyClaim?.Value, out var cid);
            
            // Si no hay PaisId en el header, intentar del claim
            if (!PaisId.HasValue && paisClaim != null)
            {
                int.TryParse(paisClaim.Value, out var pidFromClaim);
                if (pidFromClaim > 0)
                    PaisId = pidFromClaim;
            }

            CompanyId = cid;
            UserId    = uid;
            UserGuid  = userGuid;
        }
        else
        {
            // Fallback para dev/local si no hay token
            CompanyId = TryGetEnvInt("DEFAULT_COMPANY_ID", 1);
            UserId    = TryGetEnvInt("DEFAULT_USER_ID", 0);
            if (!PaisId.HasValue)
                PaisId = TryGetEnvInt("DEFAULT_PAIS_ID", null);
        }
    }

    private static int TryGetEnvInt(string key, int? def)
    {
        var envValue = Environment.GetEnvironmentVariable(key);
        if (int.TryParse(envValue, out var v))
            return v;
        return def ?? 0;
    }
}