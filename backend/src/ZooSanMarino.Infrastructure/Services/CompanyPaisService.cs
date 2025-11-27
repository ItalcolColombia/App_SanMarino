using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class CompanyPaisService : ICompanyPaisService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICompanyPaisValidator _validator;

    public CompanyPaisService(ZooSanMarinoContext context, ICompanyPaisValidator validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<CompanyPaisDto> AssignCompanyToPaisAsync(AssignCompanyPaisDto dto)
    {
        // Validar que la empresa existe
        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == dto.CompanyId);
        
        if (company == null)
            throw new InvalidOperationException($"Empresa con ID {dto.CompanyId} no encontrada");

        // Validar que el país existe
        var pais = await _context.Paises
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PaisId == dto.PaisId);
        
        if (pais == null)
            throw new InvalidOperationException($"País con ID {dto.PaisId} no encontrado");

        // Verificar si ya existe la relación
        var existing = await _context.CompanyPaises
            .FirstOrDefaultAsync(cp => cp.CompanyId == dto.CompanyId && cp.PaisId == dto.PaisId);

        if (existing != null)
            throw new InvalidOperationException("La empresa ya está asignada a este país");

        // Crear la relación
        var companyPais = new CompanyPais
        {
            CompanyId = dto.CompanyId,
            PaisId = dto.PaisId,
            CreatedAt = DateTime.UtcNow
        };

        _context.CompanyPaises.Add(companyPais);
        await _context.SaveChangesAsync();

        return new CompanyPaisDto
        {
            CompanyId = companyPais.CompanyId,
            CompanyName = company.Name,
            PaisId = companyPais.PaisId,
            PaisNombre = pais.PaisNombre,
            IsDefault = false
        };
    }

    public async Task<bool> RemoveCompanyFromPaisAsync(RemoveCompanyPaisDto dto)
    {
        var companyPais = await _context.CompanyPaises
            .FirstOrDefaultAsync(cp => cp.CompanyId == dto.CompanyId && cp.PaisId == dto.PaisId);

        if (companyPais == null)
            return false;

        // Verificar si hay usuarios asignados a esta combinación
        var hasUsers = await _context.UserCompanies
            .AnyAsync(uc => uc.CompanyId == dto.CompanyId && uc.PaisId == dto.PaisId);

        if (hasUsers)
            throw new InvalidOperationException(
                "No se puede remover la relación empresa-país porque hay usuarios asignados. " +
                "Primero debe remover las asignaciones de usuarios.");

        _context.CompanyPaises.Remove(companyPais);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<CompanyDto>> GetCompaniesByPaisAsync(int paisId)
    {
        return await _context.CompanyPaises
            .AsNoTracking()
            .Include(cp => cp.Company)
            .Where(cp => cp.PaisId == paisId)
            .Select(cp => new CompanyDto(
                cp.Company.Id,
                cp.Company.Name,
                cp.Company.Identifier,
                cp.Company.DocumentType,
                cp.Company.Address,
                cp.Company.Phone,
                cp.Company.Email,
                cp.Company.Country,
                cp.Company.State,
                cp.Company.City,
                cp.Company.MobileAccess,
                cp.Company.VisualPermissions
            ))
            .ToListAsync();
    }

    public async Task<List<PaisDto>> GetPaisesByCompanyAsync(int companyId)
    {
        return await _context.CompanyPaises
            .AsNoTracking()
            .Include(cp => cp.Pais)
            .Where(cp => cp.CompanyId == companyId)
            .Select(cp => new PaisDto(
                cp.Pais.PaisId,
                cp.Pais.PaisNombre
            ))
            .ToListAsync();
    }

    public async Task<List<CompanyPaisDto>> GetAllCompanyPaisAsync()
    {
        return await _context.CompanyPaises
            .AsNoTracking()
            .Include(cp => cp.Company)
            .Include(cp => cp.Pais)
            .Select(cp => new CompanyPaisDto
            {
                CompanyId = cp.CompanyId,
                CompanyName = cp.Company.Name,
                PaisId = cp.PaisId,
                PaisNombre = cp.Pais.PaisNombre,
                IsDefault = false // Este campo se determina desde UserCompany
            })
            .ToListAsync();
    }

    public async Task<CompanyPaisDto> AssignUserToCompanyPaisAsync(AssignUserCompanyPaisDto dto)
    {
        // Validar que la empresa pertenece al país
        var isValid = await _validator.ValidateCompanyPaisAsync(dto.CompanyId, dto.PaisId);
        if (!isValid)
            throw new InvalidOperationException("La empresa no está asignada al país especificado");

        // Validar que el usuario existe
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == dto.UserId);
        
        if (user == null)
            throw new InvalidOperationException($"Usuario con ID {dto.UserId} no encontrado");

        // Verificar si ya existe la relación
        var existing = await _context.UserCompanies
            .FirstOrDefaultAsync(uc => uc.UserId == dto.UserId 
                                     && uc.CompanyId == dto.CompanyId 
                                     && uc.PaisId == dto.PaisId);

        if (existing != null)
            throw new InvalidOperationException("El usuario ya está asignado a esta empresa-país");

        // Si se marca como default, desmarcar otros defaults del usuario
        if (dto.IsDefault)
        {
            var otherDefaults = await _context.UserCompanies
                .Where(uc => uc.UserId == dto.UserId && uc.IsDefault)
                .ToListAsync();
            
            foreach (var ud in otherDefaults)
            {
                ud.IsDefault = false;
            }
        }

        // Crear la relación
        var userCompany = new UserCompany
        {
            UserId = dto.UserId,
            CompanyId = dto.CompanyId,
            PaisId = dto.PaisId,
            IsDefault = dto.IsDefault
        };

        _context.UserCompanies.Add(userCompany);
        await _context.SaveChangesAsync();

        // Obtener información para el DTO
        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == dto.CompanyId);
        
        var pais = await _context.Paises
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PaisId == dto.PaisId);

        return new CompanyPaisDto
        {
            CompanyId = dto.CompanyId,
            CompanyName = company?.Name ?? string.Empty,
            PaisId = dto.PaisId,
            PaisNombre = pais?.PaisNombre ?? string.Empty,
            IsDefault = dto.IsDefault
        };
    }

    public async Task<bool> RemoveUserFromCompanyPaisAsync(RemoveUserCompanyPaisDto dto)
    {
        var userCompany = await _context.UserCompanies
            .FirstOrDefaultAsync(uc => uc.UserId == dto.UserId 
                                    && uc.CompanyId == dto.CompanyId 
                                    && uc.PaisId == dto.PaisId);

        if (userCompany == null)
            return false;

        _context.UserCompanies.Remove(userCompany);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<CompanyPaisDto>> GetUserCompanyPaisAsync(Guid userId)
    {
        return await _context.UserCompanies
            .AsNoTracking()
            .Include(uc => uc.Company)
            .Where(uc => uc.UserId == userId)
            .Select(uc => new CompanyPaisDto
            {
                CompanyId = uc.CompanyId,
                CompanyName = uc.Company != null ? uc.Company.Name : string.Empty,
                PaisId = 0,
                PaisNombre = string.Empty,
                IsDefault = uc.IsDefault
            })
            .ToListAsync();
    }

    public async Task<bool> ValidateCompanyPaisAsync(int companyId, int paisId)
    {
        return await _validator.ValidateCompanyPaisAsync(companyId, paisId);
    }
}





