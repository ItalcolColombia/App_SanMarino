using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.API.Infrastructure;

/// <summary>
/// Resuelve la empresa activa (header X-Active-Company) a CompanyId y la valida contra
/// las empresas asignadas al usuario (UserCompanies). Si es válida, la guarda en HttpContext.Items
/// para que ICurrentUser pueda usarla como CompanyId efectivo en toda la app.
/// </summary>
public sealed class ActiveCompanyMiddleware
{
    public const string EffectiveCompanyIdItemKey = "EffectiveCompanyId";
    public const string EffectiveCompanyNameItemKey = "EffectiveCompanyName";

    private readonly RequestDelegate _next;

    public ActiveCompanyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ZooSanMarinoContext db)
    {
        var companyName = context.Request.Headers["X-Active-Company"].FirstOrDefault();
        var companyIdHeader = context.Request.Headers["X-Active-Company-Id"].FirstOrDefault();

        // Preferir ID de empresa desde storage (X-Active-Company-Id) cuando venga
        int? companyIdFromHeader = null;
        if (!string.IsNullOrWhiteSpace(companyIdHeader) && int.TryParse(companyIdHeader, out var cidFromHeader) && cidFromHeader > 0)
            companyIdFromHeader = cidFromHeader;

        if (companyIdFromHeader.HasValue)
        {
            var cid = companyIdFromHeader.Value;
            if (string.IsNullOrWhiteSpace(companyName))
                companyName = await db.Companies.AsNoTracking().Where(c => c.Id == cid).Select(c => c.Name).FirstOrDefaultAsync() ?? "";
            var name = (companyName ?? "").Trim();

            if (context.User?.Identity?.IsAuthenticated != true)
            {
                context.Items[EffectiveCompanyIdItemKey] = cid;
                context.Items[EffectiveCompanyNameItemKey] = name;
                await _next(context);
                return;
            }
            var claim = context.User.FindFirst(ClaimTypes.NameIdentifier) ?? context.User.FindFirst("sub");
            if (claim != null && Guid.TryParse(claim.Value, out var uid))
            {
                var userEmail = await db.UserLogins.AsNoTracking().Include(ul => ul.Login).Where(ul => ul.UserId == uid).Select(ul => ul.Login.email).FirstOrDefaultAsync();
                var isSuperAdmin = !string.IsNullOrWhiteSpace(userEmail) && userEmail.Trim().Equals("moiesbbuga@gmail.com", StringComparison.OrdinalIgnoreCase);
                var canUse = isSuperAdmin || await db.UserCompanies.AsNoTracking().AnyAsync(uc => uc.UserId == uid && uc.CompanyId == cid);
                if (canUse)
                {
                    context.Items[EffectiveCompanyIdItemKey] = cid;
                    context.Items[EffectiveCompanyNameItemKey] = name;
                    await _next(context);
                    return;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(companyName))
        {
            await _next(context);
            return;
        }

        companyName = companyName.Trim();

        // Resolver CompanyId por nombre (case-insensitive)
        var companyId = await db.Companies
            .AsNoTracking()
            .Where(c => EF.Functions.ILike(c.Name, companyName))
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync();

        if (!companyId.HasValue || companyId.Value <= 0)
        {
            // Header inválido: no romper flujo; caer al CompanyId del token
            await _next(context);
            return;
        }

        // Si no está autenticado, permitir (útil en dev/local con DEFAULT_COMPANY_ID)
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            context.Items[EffectiveCompanyIdItemKey] = companyId.Value;
            context.Items[EffectiveCompanyNameItemKey] = companyName;
            await _next(context);
            return;
        }

        // Obtener Guid del usuario desde claims
        var userGuidClaim =
            context.User.FindFirst(ClaimTypes.NameIdentifier) ??
            context.User.FindFirst("sub");

        if (userGuidClaim == null || !Guid.TryParse(userGuidClaim.Value, out var userGuid))
        {
            // Sin Guid: no romper flujo
            await _next(context);
            return;
        }

        // Super admin: permitido para cualquier empresa (misma regla que UserPermissionService)
        var email = await db.UserLogins
            .AsNoTracking()
            .Include(ul => ul.Login)
            .Where(ul => ul.UserId == userGuid)
            .Select(ul => ul.Login.email)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(email) &&
            email.Trim().Equals("moiesbbuga@gmail.com", StringComparison.OrdinalIgnoreCase))
        {
            context.Items[EffectiveCompanyIdItemKey] = companyId.Value;
            context.Items[EffectiveCompanyNameItemKey] = companyName;
            await _next(context);
            return;
        }

        // Validar pertenencia del usuario a la empresa activa
        var allowed = await db.UserCompanies
            .AsNoTracking()
            .AnyAsync(uc => uc.UserId == userGuid && uc.CompanyId == companyId.Value);

        if (allowed)
        {
            context.Items[EffectiveCompanyIdItemKey] = companyId.Value;
            context.Items[EffectiveCompanyNameItemKey] = companyName;
        }
        // Si no está permitido, no setear EffectiveCompanyId -> se usará CompanyId del token

        await _next(context);
    }
}

