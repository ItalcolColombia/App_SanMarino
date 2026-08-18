using Microsoft.EntityFrameworkCore;

namespace ZooSanMarino.Infrastructure.Services;

public partial class CompanyService
{
    private async Task<bool> IsSuperAdminAsync(int userId)
    {
        var userIdGuid = _currentUser.UserGuid
            ?? new Guid(userId.ToString("D32").PadLeft(32, '0'));

        return await SuperAdminLookup.EsSuperAdminAsync(_ctx, userIdGuid);
    }

    private async Task<bool> IsUserAdminOrAdministratorAsync(int userId)
    {
        var userIdGuid = _currentUser.UserGuid
            ?? new Guid(userId.ToString("D32").PadLeft(32, '0'));

        var roles = await _ctx.UserRoles
            .AsNoTracking()
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userIdGuid)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

        return roles.Any(r =>
            !string.IsNullOrWhiteSpace(r) &&
            (r.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
             r.Equals("administrador", StringComparison.OrdinalIgnoreCase)));
    }
}
