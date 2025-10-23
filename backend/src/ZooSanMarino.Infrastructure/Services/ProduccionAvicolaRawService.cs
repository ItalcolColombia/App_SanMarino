// src/ZooSanMarino.Infrastructure/Services/ProduccionAvicolaRawService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Common;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class ProduccionAvicolaRawService : IProduccionAvicolaRawService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;

    public ProduccionAvicolaRawService(ZooSanMarinoContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ProduccionAvicolaRawDto> CreateAsync(CreateProduccionAvicolaRawDto dto)
    {
        var entity = new ProduccionAvicolaRaw
        {
            CompanyId = _currentUser.CompanyId,
            AnioGuia = dto.AnioGuia,
            Raza = dto.Raza,
            Edad = dto.Edad,
            MortSemH = dto.MortSemH,
            RetiroAcH = dto.RetiroAcH,
            MortSemM = dto.MortSemM,
            RetiroAcM = dto.RetiroAcM,
            ConsAcH = dto.ConsAcH,
            ConsAcM = dto.ConsAcM,
            GrAveDiaH = dto.GrAveDiaH,
            GrAveDiaM = dto.GrAveDiaM,
            PesoH = dto.PesoH,
            PesoM = dto.PesoM,
            Uniformidad = dto.Uniformidad,
            HTotalAa = dto.HTotalAa,
            ProdPorcentaje = dto.ProdPorcentaje,
            HIncAa = dto.HIncAa,
            AprovSem = dto.AprovSem,
            PesoHuevo = dto.PesoHuevo,
            MasaHuevo = dto.MasaHuevo,
            GrasaPorcentaje = dto.GrasaPorcentaje,
            NacimPorcentaje = dto.NacimPorcentaje,
            PollitoAa = dto.PollitoAa,
            KcalAveDiaH = dto.KcalAveDiaH,
            KcalAveDiaM = dto.KcalAveDiaM,
            AprovAc = dto.AprovAc,
            GrHuevoT = dto.GrHuevoT,
            GrHuevoInc = dto.GrHuevoInc,
            GrPollito = dto.GrPollito,
            Valor1000 = dto.Valor1000,
            Valor150 = dto.Valor150,
            Apareo = dto.Apareo,
            PesoMh = dto.PesoMh
        };

        _context.ProduccionAvicolaRaw.Add(entity);
        await _context.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<IEnumerable<ProduccionAvicolaRawDto>> GetAllAsync()
    {
        return await _context.ProduccionAvicolaRaw
            .AsNoTracking()
            .Where(x => x.CompanyId == _currentUser.CompanyId)
            .Select(MapToDtoExpression())
            .ToListAsync();
    }

    public async Task<ProduccionAvicolaRawDto?> GetByIdAsync(int id)
    {
        var entity = await _context.ProduccionAvicolaRaw
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == _currentUser.CompanyId);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<ProduccionAvicolaRawDto> UpdateAsync(UpdateProduccionAvicolaRawDto dto)
    {
        var entity = await _context.ProduccionAvicolaRaw
            .FirstOrDefaultAsync(x => x.Id == dto.Id && x.CompanyId == _currentUser.CompanyId);

        if (entity == null)
            throw new KeyNotFoundException($"ProduccionAvicolaRaw con ID {dto.Id} no encontrado");

        // Actualizar propiedades
        entity.AnioGuia = dto.AnioGuia;
        entity.Raza = dto.Raza;
        entity.Edad = dto.Edad;
        entity.MortSemH = dto.MortSemH;
        entity.RetiroAcH = dto.RetiroAcH;
        entity.MortSemM = dto.MortSemM;
        entity.RetiroAcM = dto.RetiroAcM;
        entity.ConsAcH = dto.ConsAcH;
        entity.ConsAcM = dto.ConsAcM;
        entity.GrAveDiaH = dto.GrAveDiaH;
        entity.GrAveDiaM = dto.GrAveDiaM;
        entity.PesoH = dto.PesoH;
        entity.PesoM = dto.PesoM;
        entity.Uniformidad = dto.Uniformidad;
        entity.HTotalAa = dto.HTotalAa;
        entity.ProdPorcentaje = dto.ProdPorcentaje;
        entity.HIncAa = dto.HIncAa;
        entity.AprovSem = dto.AprovSem;
        entity.PesoHuevo = dto.PesoHuevo;
        entity.MasaHuevo = dto.MasaHuevo;
        entity.GrasaPorcentaje = dto.GrasaPorcentaje;
        entity.NacimPorcentaje = dto.NacimPorcentaje;
        entity.PollitoAa = dto.PollitoAa;
        entity.KcalAveDiaH = dto.KcalAveDiaH;
        entity.KcalAveDiaM = dto.KcalAveDiaM;
        entity.AprovAc = dto.AprovAc;
        entity.GrHuevoT = dto.GrHuevoT;
        entity.GrHuevoInc = dto.GrHuevoInc;
        entity.GrPollito = dto.GrPollito;
        entity.Valor1000 = dto.Valor1000;
        entity.Valor150 = dto.Valor150;
        entity.Apareo = dto.Apareo;
        entity.PesoMh = dto.PesoMh;

        await _context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.ProduccionAvicolaRaw
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == _currentUser.CompanyId);

        if (entity == null)
            return false;

        _context.ProduccionAvicolaRaw.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ZooSanMarino.Application.DTOs.Common.PagedResult<ProduccionAvicolaRawDto>> SearchAsync(ProduccionAvicolaRawSearchRequest request)
    {
        var query = _context.ProduccionAvicolaRaw
            .AsNoTracking()
            .Where(x => x.CompanyId == _currentUser.CompanyId);

        // Aplicar filtros
        if (!string.IsNullOrWhiteSpace(request.AnioGuia))
            query = query.Where(x => x.AnioGuia != null && x.AnioGuia.Contains(request.AnioGuia));

        if (!string.IsNullOrWhiteSpace(request.Raza))
            query = query.Where(x => x.Raza != null && x.Raza.Contains(request.Raza));

        if (!string.IsNullOrWhiteSpace(request.Edad))
            query = query.Where(x => x.Edad != null && x.Edad.Contains(request.Edad));

        if (request.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == request.CompanyId.Value);

        // Aplicar ordenamiento
        query = ApplyOrdering(query, request.SortBy, request.SortDesc);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(MapToDtoExpression())
            .ToListAsync();

        return new ZooSanMarino.Application.DTOs.Common.PagedResult<ProduccionAvicolaRawDto>
        {
            Items = items,
            Total = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    private static IQueryable<ProduccionAvicolaRaw> ApplyOrdering(IQueryable<ProduccionAvicolaRaw> query, string? sortBy, bool sortDesc)
    {
        return sortBy?.ToLower() switch
        {
            "anioguia" => sortDesc ? query.OrderByDescending(x => x.AnioGuia) : query.OrderBy(x => x.AnioGuia),
            "raza" => sortDesc ? query.OrderByDescending(x => x.Raza) : query.OrderBy(x => x.Raza),
            "edad" => sortDesc ? query.OrderByDescending(x => x.Edad) : query.OrderBy(x => x.Edad),
            "mortsemh" => sortDesc ? query.OrderByDescending(x => x.MortSemH) : query.OrderBy(x => x.MortSemH),
            "mortsemm" => sortDesc ? query.OrderByDescending(x => x.MortSemM) : query.OrderBy(x => x.MortSemM),
            "consach" => sortDesc ? query.OrderByDescending(x => x.ConsAcH) : query.OrderBy(x => x.ConsAcH),
            "consacm" => sortDesc ? query.OrderByDescending(x => x.ConsAcM) : query.OrderBy(x => x.ConsAcM),
            "pesoh" => sortDesc ? query.OrderByDescending(x => x.PesoH) : query.OrderBy(x => x.PesoH),
            "pesom" => sortDesc ? query.OrderByDescending(x => x.PesoM) : query.OrderBy(x => x.PesoM),
            "uniformidad" => sortDesc ? query.OrderByDescending(x => x.Uniformidad) : query.OrderBy(x => x.Uniformidad),
            _ => sortDesc ? query.OrderByDescending(x => x.Id) : query.OrderBy(x => x.Id)
        };
    }

    private static ProduccionAvicolaRawDto MapToDto(ProduccionAvicolaRaw entity)
    {
        return new ProduccionAvicolaRawDto(
            entity.Id,
            entity.CompanyId,
            entity.AnioGuia,
            entity.Raza,
            entity.Edad,
            entity.MortSemH,
            entity.RetiroAcH,
            entity.MortSemM,
            entity.RetiroAcM,
            entity.ConsAcH,
            entity.ConsAcM,
            entity.GrAveDiaH,
            entity.GrAveDiaM,
            entity.PesoH,
            entity.PesoM,
            entity.Uniformidad,
            entity.HTotalAa,
            entity.ProdPorcentaje,
            entity.HIncAa,
            entity.AprovSem,
            entity.PesoHuevo,
            entity.MasaHuevo,
            entity.GrasaPorcentaje,
            entity.NacimPorcentaje,
            entity.PollitoAa,
            entity.KcalAveDiaH,
            entity.KcalAveDiaM,
            entity.AprovAc,
            entity.GrHuevoT,
            entity.GrHuevoInc,
            entity.GrPollito,
            entity.Valor1000,
            entity.Valor150,
            entity.Apareo,
            entity.PesoMh,
            entity.CreatedAt,
            entity.UpdatedAt
        );
    }

    private static System.Linq.Expressions.Expression<Func<ProduccionAvicolaRaw, ProduccionAvicolaRawDto>> MapToDtoExpression()
    {
        return entity => new ProduccionAvicolaRawDto(
            entity.Id,
            entity.CompanyId,
            entity.AnioGuia,
            entity.Raza,
            entity.Edad,
            entity.MortSemH,
            entity.RetiroAcH,
            entity.MortSemM,
            entity.RetiroAcM,
            entity.ConsAcH,
            entity.ConsAcM,
            entity.GrAveDiaH,
            entity.GrAveDiaM,
            entity.PesoH,
            entity.PesoM,
            entity.Uniformidad,
            entity.HTotalAa,
            entity.ProdPorcentaje,
            entity.HIncAa,
            entity.AprovSem,
            entity.PesoHuevo,
            entity.MasaHuevo,
            entity.GrasaPorcentaje,
            entity.NacimPorcentaje,
            entity.PollitoAa,
            entity.KcalAveDiaH,
            entity.KcalAveDiaM,
            entity.AprovAc,
            entity.GrHuevoT,
            entity.GrHuevoInc,
            entity.GrPollito,
            entity.Valor1000,
            entity.Valor150,
            entity.Apareo,
            entity.PesoMh,
            entity.CreatedAt,
            entity.UpdatedAt
        );
    }
}
