using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class CompanyService
{
    private async Task UpsertLogoAsync(int companyId, byte[] bytes, string contentType)
    {
        var existing = await _ctx.CompanyLogos
            .FirstOrDefaultAsync(l => l.CompanyId == companyId);

        if (existing is null)
        {
            _ctx.CompanyLogos.Add(new CompanyLogo
            {
                CompanyId        = companyId,
                LogoBytes        = bytes,
                LogoContentType  = contentType
            });
        }
        else
        {
            existing.LogoBytes       = bytes;
            existing.LogoContentType = contentType;
        }

        await _ctx.SaveChangesAsync();
    }

    private async Task DeleteLogoAsync(int companyId)
    {
        var existing = await _ctx.CompanyLogos
            .FirstOrDefaultAsync(l => l.CompanyId == companyId);

        if (existing is not null)
        {
            _ctx.CompanyLogos.Remove(existing);
            await _ctx.SaveChangesAsync();
        }
    }
}
