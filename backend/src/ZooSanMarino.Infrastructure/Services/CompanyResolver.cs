// src/ZooSanMarino.Infrastructure/Services/CompanyResolver.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class CompanyResolver : ICompanyResolver
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;

    public CompanyResolver(ZooSanMarinoContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<int?> GetCompanyIdByNameAsync(string companyName)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            return null;

        // Atajo determinista: si el nombre pedido es el de la empresa activa YA VALIDADA por el
        // middleware, se responde con SU id. El middleware resolvió ese nombre contra la base y ese
        // es el id que autorizó; volver a buscarlo por nombre acá abriría una segunda fuente de
        // verdad que además no es determinista (`companies.name` no tiene índice único y la búsqueda
        // de abajo es un `FirstOrDefault` sin orden). De paso ahorra una consulta por llamada.
        var idActivo = EmpresaActivaCalculos.IdDeLaEmpresaActivaSiCoincide(
            companyName, _currentUser.ActiveCompanyName, _currentUser.CompanyId);
        if (idActivo.HasValue) return idActivo;

        var name = companyName.Trim();
        var company = await _context.Companies
            .AsNoTracking()
            // Comparación case-insensitive (evita fallos por mayúsculas/minúsculas desde el storage)
            .Where(c => EF.Functions.ILike(c.Name, name))
            .Select(c => new { c.Id })
            .FirstOrDefaultAsync();

        return company?.Id;
    }

    public async Task<CompanyDto?> GetCompanyByNameAsync(string companyName)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            return null;

        var name = companyName.Trim();
        var company = await _context.Companies
            .AsNoTracking()
            .Where(c => EF.Functions.ILike(c.Name, name))
            .Select(c => new CompanyDto(
                c.Id,
                c.Name,
                c.Identifier,
                c.DocumentType,
                c.Address,
                c.Phone,
                c.Email,
                c.Country,
                c.State,
                c.City,
                null,
                c.MobileAccess,
                c.VisualPermissions,
                c.ManejaAlimentoPorGalpon,
                c.ManejaCodigosErpAvicola,
                c.ClasificacionHuevoPorItems,
                c.PermiteTrasladoAvesCrossEtapa,
                c.CapturaHuevosEnLevante,
                c.VentaEngordePesoDiferido,
                c.PrimerRegistroSegunHoraLlegada,
                c.DiasAlimentoPrevioEncaset,
                c.ProgramacionLotesEngorde,
                c.NombreLoteIncluyeCorrida,
                c.ManejaInventarioPorSilo,
                c.ReportesAlimentoDesdeInventarioUnificado,
                c.RequiereValidacionSeguimientoDiario,
                c.SeguimientoEngordeMixto,
                c.ReporteCostosAlimentoDesdeFuentesReales,
                c.ConsumoAlimentoSoloHembras,
                c.OcultaMachosEnPostura,
                c.HuevoPrimeraPosturaHastaSemana
            ))
            .FirstOrDefaultAsync();

        return company;
    }

    public async Task<bool> IsCompanyValidAsync(string companyName)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            return false;

        var name = companyName.Trim();
        return await _context.Companies
            .AsNoTracking()
            .AnyAsync(c => EF.Functions.ILike(c.Name, name));
    }

    public async Task<IEnumerable<CompanyDto>> GetCompaniesForUserAsync(int userId)
    {
        var userIdGuid = new Guid(userId.ToString("D32").PadLeft(32, '0'));
        var companies = await _context.UserCompanies
            .AsNoTracking()
            .Include(uc => uc.Company)
            .Where(uc => uc.UserId == userIdGuid)
            .Select(uc => new CompanyDto(
                uc.Company.Id,
                uc.Company.Name,
                uc.Company.Identifier,
                uc.Company.DocumentType,
                uc.Company.Address,
                uc.Company.Phone,
                uc.Company.Email,
                uc.Company.Country,
                uc.Company.State,
                uc.Company.City,
                null,
                uc.Company.MobileAccess,
                uc.Company.VisualPermissions,
                uc.Company.ManejaAlimentoPorGalpon,
                uc.Company.ManejaCodigosErpAvicola,
                uc.Company.ClasificacionHuevoPorItems,
                uc.Company.PermiteTrasladoAvesCrossEtapa,
                uc.Company.CapturaHuevosEnLevante,
                uc.Company.VentaEngordePesoDiferido,
                uc.Company.PrimerRegistroSegunHoraLlegada,
                uc.Company.DiasAlimentoPrevioEncaset,
                uc.Company.ProgramacionLotesEngorde,
                uc.Company.NombreLoteIncluyeCorrida,
                uc.Company.ManejaInventarioPorSilo,
                uc.Company.ReportesAlimentoDesdeInventarioUnificado,
                uc.Company.RequiereValidacionSeguimientoDiario,
                uc.Company.SeguimientoEngordeMixto,
                uc.Company.ReporteCostosAlimentoDesdeFuentesReales,
                uc.Company.ConsumoAlimentoSoloHembras,
                uc.Company.OcultaMachosEnPostura,
                uc.Company.HuevoPrimeraPosturaHastaSemana
            ))
            .ToListAsync();

        return companies;
    }
}
