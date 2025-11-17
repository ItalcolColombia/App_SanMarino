using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class CompanyPaisValidator : ICompanyPaisValidator
{
    private readonly ZooSanMarinoContext _context;

    public CompanyPaisValidator(ZooSanMarinoContext context)
    {
        _context = context;
    }

    public async Task<bool> ValidateCompanyPaisAsync(int companyId, int paisId)
    {
        return await _context.CompanyPaises
            .AsNoTracking()
            .AnyAsync(cp => cp.CompanyId == companyId && cp.PaisId == paisId);
    }

    public async Task<bool> ValidateUserCompanyPaisAsync(Guid userId, int companyId, int paisId)
    {
        return await _context.UserCompanies
            .AsNoTracking()
            .AnyAsync(uc => uc.UserId == userId 
                         && uc.CompanyId == companyId 
                         && uc.PaisId == paisId);
    }

    public async Task<List<int>> GetPaisesByCompanyAsync(int companyId)
    {
        return await _context.CompanyPaises
            .AsNoTracking()
            .Where(cp => cp.CompanyId == companyId)
            .Select(cp => cp.PaisId)
            .ToListAsync();
    }

    public async Task<List<int>> GetCompaniesByPaisAsync(int paisId)
    {
        return await _context.CompanyPaises
            .AsNoTracking()
            .Where(cp => cp.PaisId == paisId)
            .Select(cp => cp.CompanyId)
            .ToListAsync();
    }

    public async Task<List<(int CompanyId, int PaisId, string CompanyName, string PaisNombre)>> GetUserCompanyPaisAsync(Guid userId)
    {
        return await _context.UserCompanies
            .AsNoTracking()
            .Include(uc => uc.Company)
            .Where(uc => uc.UserId == userId)
            .Select(uc => new ValueTuple<int, int, string, string>(
                uc.CompanyId,
                0,
                uc.Company != null ? uc.Company.Name : string.Empty,
                string.Empty
            ))
            .ToListAsync();
    }
}




