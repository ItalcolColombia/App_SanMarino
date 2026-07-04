using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class CompanyService
{
    public async Task<IEnumerable<CompanyDto>> GetAllAsync()
    {
        var isSuperAdmin = await IsSuperAdminAsync(_currentUser.UserId);
        var isAdmin = await IsUserAdminOrAdministratorAsync(_currentUser.UserId);

        if (isSuperAdmin || isAdmin)
        {
            var all = await _ctx.Companies
                .AsNoTracking()
                .Include(c => c.Logo)
                .OrderBy(c => c.Name)
                .ToListAsync();
            return all.Select(ToDto).ToList();
        }

        var userIdGuid = _currentUser.UserGuid
            ?? new Guid(_currentUser.UserId.ToString("D32").PadLeft(32, '0'));

        var companies = await _ctx.UserCompanies
            .AsNoTracking()
            .Include(uc => uc.Company)
                .ThenInclude(c => c.Logo)
            .Where(uc => uc.UserId == userIdGuid)
            .OrderBy(uc => uc.Company.Name)
            .Select(uc => uc.Company)
            .ToListAsync();

        return companies.Select(ToDto).ToList();
    }

    public async Task<IEnumerable<CompanyDto>> GetAllForAdminAsync()
    {
        var companies = await _ctx.Companies
            .AsNoTracking()
            .Include(c => c.Logo)
            .ToListAsync();
        return companies.Select(ToDto).OrderBy(c => c.Name).ToList();
    }

    public async Task<CompanyDto?> GetByIdAsync(int id)
    {
        var c = await _ctx.Companies
            .AsNoTracking()
            .Include(c => c.Logo)
            .FirstOrDefaultAsync(x => x.Id == id);
        return c == null ? null : ToDto(c);
    }

    public async Task<CompanyDto> CreateAsync(CreateCompanyDto dto)
    {
        var c = new Company
        {
            Name              = dto.Name,
            Identifier        = dto.Identifier,
            DocumentType      = dto.DocumentType,
            Address           = dto.Address,
            Phone             = dto.Phone,
            Email             = dto.Email,
            Country           = dto.Country,
            State             = dto.State,
            City              = dto.City,
            VisualPermissions = dto.VisualPermissions,
            MobileAccess      = dto.MobileAccess,
            ManejaAlimentoPorGalpon = dto.ManejaAlimentoPorGalpon
        };

        _ctx.Companies.Add(c);
        await _ctx.SaveChangesAsync();

        if (CompanyCalculos.TryExtractLogo(dto.LogoDataUrl, out var bytes, out var contentType, out var clear)
            && !clear && bytes != null)
        {
            await UpsertLogoAsync(c.Id, bytes, contentType!);
        }

        var result = await GetByIdAsync(c.Id);
        if (result is null) throw new InvalidOperationException("Created company could not be retrieved.");
        return result;
    }

    public async Task<CompanyDto?> UpdateAsync(UpdateCompanyDto dto)
    {
        var c = await _ctx.Companies.FindAsync(dto.Id);
        if (c is null) return null;

        c.Name              = dto.Name;
        c.Identifier        = dto.Identifier;
        c.DocumentType      = dto.DocumentType;
        c.Address           = dto.Address;
        c.Phone             = dto.Phone;
        c.Email             = dto.Email;
        c.Country           = dto.Country;
        c.State             = dto.State;
        c.City              = dto.City;
        c.VisualPermissions = dto.VisualPermissions;
        c.MobileAccess      = dto.MobileAccess;
        c.ManejaAlimentoPorGalpon = dto.ManejaAlimentoPorGalpon;

        await _ctx.SaveChangesAsync();

        // Logo: null = no cambiar; "" = borrar; dataUrl = upsert
        if (CompanyCalculos.TryExtractLogo(dto.LogoDataUrl, out var bytes, out var contentType, out var clear))
        {
            if (clear)
                await DeleteLogoAsync(c.Id);
            else if (bytes != null)
                await UpsertLogoAsync(c.Id, bytes, contentType!);
        }

        return await GetByIdAsync(c.Id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var c = await _ctx.Companies.FindAsync(id);
        if (c is null) return false;
        _ctx.Companies.Remove(c);
        await _ctx.SaveChangesAsync();
        return true;
    }
}
