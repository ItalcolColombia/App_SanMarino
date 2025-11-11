using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class ProduccionDiariaService : IProduccionDiariaService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly IMovimientoAvesService _movimientoAvesService;

    public ProduccionDiariaService(
        ZooSanMarinoContext ctx, 
        ICurrentUser current,
        IMovimientoAvesService movimientoAvesService)
    {
        _ctx = ctx;
        _current = current;
        _movimientoAvesService = movimientoAvesService;
    }

    public async Task<IEnumerable<ProduccionDiariaDto>> GetAllAsync()
    {
        var q = from p in _ctx.SeguimientoProduccion.AsNoTracking()
                join l in _ctx.Lotes.AsNoTracking() on p.LoteId equals l.LoteId.ToString()
                where l.CompanyId == _current.CompanyId && l.DeletedAt == null
                select p;

        return await q
            .OrderByDescending(x => x.Fecha)
            .Select(x => new ProduccionDiariaDto(
                x.Id,
                x.LoteId,
                x.Fecha, // Fecha -> FechaRegistro
                x.MortalidadH, // MortalidadH -> MortalidadHembras
                x.MortalidadM, // MortalidadM -> MortalidadMachos
                x.SelH,
                (double)x.ConsKgH, // decimal -> double
                (double)x.ConsKgM, // decimal -> double
                x.HuevoTot,
                x.HuevoInc,
                x.TipoAlimento,
                x.Observaciones ?? string.Empty,
                (double?)x.PesoHuevo, // decimal -> double?
                x.Etapa
            ))
            .ToListAsync();
    }

    public async Task<IEnumerable<ProduccionDiariaDto>> GetByLoteIdAsync(int loteId)
    {
        var loteIdStr = loteId.ToString();
        var q = from p in _ctx.SeguimientoProduccion.AsNoTracking()
                join l in _ctx.Lotes.AsNoTracking() on p.LoteId equals l.LoteId.ToString()
                where l.CompanyId == _current.CompanyId && 
                      l.DeletedAt == null && 
                      p.LoteId == loteIdStr
                select p;

        return await q
            .OrderByDescending(x => x.Fecha)
            .Select(x => new ProduccionDiariaDto(
                x.Id,
                x.LoteId,
                x.Fecha, // Fecha -> FechaRegistro
                x.MortalidadH, // MortalidadH -> MortalidadHembras
                x.MortalidadM, // MortalidadM -> MortalidadMachos
                x.SelH,
                (double)x.ConsKgH, // decimal -> double
                (double)x.ConsKgM, // decimal -> double
                x.HuevoTot,
                x.HuevoInc,
                x.TipoAlimento,
                x.Observaciones ?? string.Empty,
                (double?)x.PesoHuevo, // decimal -> double?
                x.Etapa
            ))
            .ToListAsync();
    }

    public async Task<ProduccionDiariaDto> CreateAsync(CreateProduccionDiariaDto dto)
    {
        // Verificar que el lote existe y pertenece a la compañía
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId.ToString() == dto.LoteId &&
                                       l.CompanyId == _current.CompanyId &&
                                       l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote '{dto.LoteId}' no existe o no pertenece a la compañía.");

        // VALIDACIÓN CRÍTICA: Verificar que existe un ProduccionLote configurado para este lote
        var produccionLote = await _ctx.ProduccionLotes.AsNoTracking()
            .FirstOrDefaultAsync(p => p.LoteId.ToString() == dto.LoteId);
        if (produccionLote is null)
            throw new InvalidOperationException($"El lote '{dto.LoteId}' no tiene configuración de producción inicial. Debe crear primero el registro de ProduccionLote.");

        // Verificar que no existe ya un registro para la misma fecha y lote
        var existeRegistro = await _ctx.SeguimientoProduccion.AsNoTracking()
            .AnyAsync(p => p.LoteId == dto.LoteId && 
                          p.Fecha.Date == dto.FechaRegistro.Date);
        if (existeRegistro)
            throw new InvalidOperationException($"Ya existe un registro de producción diaria para el lote '{dto.LoteId}' en la fecha '{dto.FechaRegistro:yyyy-MM-dd}'.");

        var entity = new Domain.Entities.SeguimientoProduccion
        {
            LoteId = dto.LoteId,
            Fecha = dto.FechaRegistro,
            MortalidadH = dto.MortalidadHembras,
            MortalidadM = dto.MortalidadMachos,
            SelH = dto.SelH,
            ConsKgH = (decimal)dto.ConsKgH, // double -> decimal
            ConsKgM = (decimal)dto.ConsKgM, // double -> decimal
            HuevoTot = dto.HuevoTot,
            HuevoInc = dto.HuevoInc,
            TipoAlimento = dto.TipoAlimento,
            Observaciones = dto.Observaciones,
            PesoHuevo = dto.PesoHuevo.HasValue ? (decimal)dto.PesoHuevo.Value : 0, // double? -> decimal
            Etapa = dto.Etapa
        };

        _ctx.SeguimientoProduccion.Add(entity);
        await _ctx.SaveChangesAsync();

        // Registrar retiro automático si hay mortalidades o selecciones
        if (int.TryParse(dto.LoteId, out int loteIdInt))
        {
            var totalRetiradas = dto.MortalidadHembras + dto.MortalidadMachos + dto.SelH;
            if (totalRetiradas > 0)
            {
                try
                {
                    await _movimientoAvesService.RegistrarRetiroDesdeSeguimientoAsync(
                        loteId: loteIdInt,
                        hembrasRetiradas: dto.MortalidadHembras + dto.SelH,
                        machosRetirados: dto.MortalidadMachos,
                        mixtasRetiradas: 0, // Los seguimientos producción no tienen mixtas
                        fechaMovimiento: dto.FechaRegistro,
                        fuenteSeguimiento: "Produccion",
                        observaciones: $"Mortalidad H: {dto.MortalidadHembras}, M: {dto.MortalidadMachos} | Selección H: {dto.SelH} | Observaciones: {dto.Observaciones}"
                    );
                }
                catch (Exception ex)
                {
                    // Log error pero no fallar el guardado del seguimiento
                    Console.WriteLine($"Error al registrar retiro desde seguimiento producción: {ex.Message}");
                }
            }
        }

        return new ProduccionDiariaDto(
            entity.Id,
            entity.LoteId,
            entity.Fecha, // Fecha -> FechaRegistro
            entity.MortalidadH, // MortalidadH -> MortalidadHembras
            entity.MortalidadM, // MortalidadM -> MortalidadMachos
            entity.SelH,
            (double)entity.ConsKgH, // decimal -> double
            (double)entity.ConsKgM, // decimal -> double
            entity.HuevoTot,
            entity.HuevoInc,
            entity.TipoAlimento,
            entity.Observaciones ?? string.Empty,
            (double?)entity.PesoHuevo, // decimal -> double?
            entity.Etapa
        );
    }

    public async Task<ProduccionDiariaDto?> UpdateAsync(UpdateProduccionDiariaDto dto)
    {
        var entity = await _ctx.SeguimientoProduccion.FindAsync(dto.Id);
        if (entity == null) return null;

        // Verificar que el lote existe y pertenece a la compañía
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId.ToString() == dto.LoteId &&
                                       l.CompanyId == _current.CompanyId &&
                                       l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote '{dto.LoteId}' no existe o no pertenece a la compañía.");

        // Verificar que no existe ya otro registro para la misma fecha y lote (excluyendo el actual)
        var existeOtroRegistro = await _ctx.SeguimientoProduccion.AsNoTracking()
            .AnyAsync(p => p.LoteId == dto.LoteId && 
                          p.Fecha.Date == dto.FechaRegistro.Date &&
                          p.Id != dto.Id);
        if (existeOtroRegistro)
            throw new InvalidOperationException($"Ya existe otro registro de producción diaria para el lote '{dto.LoteId}' en la fecha '{dto.FechaRegistro:yyyy-MM-dd}'.");

        entity.LoteId = dto.LoteId;
        entity.Fecha = dto.FechaRegistro;
        entity.MortalidadH = dto.MortalidadHembras;
        entity.MortalidadM = dto.MortalidadMachos;
        entity.SelH = dto.SelH;
        entity.ConsKgH = (decimal)dto.ConsKgH; // double -> decimal
        entity.ConsKgM = (decimal)dto.ConsKgM; // double -> decimal
        entity.HuevoTot = dto.HuevoTot;
        entity.HuevoInc = dto.HuevoInc;
        entity.TipoAlimento = dto.TipoAlimento;
        entity.Observaciones = dto.Observaciones;
        entity.PesoHuevo = dto.PesoHuevo.HasValue ? (decimal)dto.PesoHuevo.Value : 0; // double? -> decimal
        entity.Etapa = dto.Etapa;

        await _ctx.SaveChangesAsync();

        // Registrar retiro automático si hay mortalidades o selecciones
        if (int.TryParse(dto.LoteId, out int loteIdInt))
        {
            var totalRetiradas = dto.MortalidadHembras + dto.MortalidadMachos + dto.SelH;
            if (totalRetiradas > 0)
            {
                try
                {
                    await _movimientoAvesService.RegistrarRetiroDesdeSeguimientoAsync(
                        loteId: loteIdInt,
                        hembrasRetiradas: dto.MortalidadHembras + dto.SelH,
                        machosRetirados: dto.MortalidadMachos,
                        mixtasRetiradas: 0,
                        fechaMovimiento: dto.FechaRegistro,
                        fuenteSeguimiento: "Produccion",
                        observaciones: $"Actualización - Mortalidad H: {dto.MortalidadHembras}, M: {dto.MortalidadMachos} | Selección H: {dto.SelH}"
                    );
                }
                catch (Exception ex)
                {
                    // Log error pero no fallar la actualización
                    Console.WriteLine($"Error al registrar retiro desde seguimiento producción (actualización): {ex.Message}");
                }
            }
        }

        return new ProduccionDiariaDto(
            entity.Id,
            entity.LoteId,
            entity.Fecha, // Fecha -> FechaRegistro
            entity.MortalidadH, // MortalidadH -> MortalidadHembras
            entity.MortalidadM, // MortalidadM -> MortalidadMachos
            entity.SelH,
            (double)entity.ConsKgH, // decimal -> double
            (double)entity.ConsKgM, // decimal -> double
            entity.HuevoTot,
            entity.HuevoInc,
            entity.TipoAlimento,
            entity.Observaciones ?? string.Empty,
            (double?)entity.PesoHuevo, // decimal -> double?
            entity.Etapa
        );
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _ctx.SeguimientoProduccion.FindAsync(id);
        if (entity == null) return false;

        // Verificar que el lote pertenece a la compañía
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId.ToString() == entity.LoteId &&
                                       l.CompanyId == _current.CompanyId &&
                                       l.DeletedAt == null);
        if (lote is null) return false;

        _ctx.SeguimientoProduccion.Remove(entity);
        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<ProduccionDiariaDto>> FilterAsync(FilterProduccionDiariaDto filter)
    {
        var q = from p in _ctx.SeguimientoProduccion.AsNoTracking()
                join l in _ctx.Lotes.AsNoTracking() on p.LoteId equals l.LoteId.ToString()
                where l.CompanyId == _current.CompanyId && l.DeletedAt == null
                select p;

        if (!string.IsNullOrWhiteSpace(filter.LoteId))
            q = q.Where(x => x.LoteId == filter.LoteId);
        if (filter.Desde.HasValue)
            q = q.Where(x => x.Fecha >= filter.Desde.Value);
        if (filter.Hasta.HasValue)
            q = q.Where(x => x.Fecha <= filter.Hasta.Value);

        return await q
            .OrderByDescending(x => x.Fecha)
            .Select(x => new ProduccionDiariaDto(
                x.Id,
                x.LoteId,
                x.Fecha, // Fecha -> FechaRegistro
                x.MortalidadH, // MortalidadH -> MortalidadHembras
                x.MortalidadM, // MortalidadM -> MortalidadMachos
                x.SelH,
                (double)x.ConsKgH, // decimal -> double
                (double)x.ConsKgM, // decimal -> double
                x.HuevoTot,
                x.HuevoInc,
                x.TipoAlimento,
                x.Observaciones ?? string.Empty,
                (double?)x.PesoHuevo, // decimal -> double?
                x.Etapa
            ))
            .ToListAsync();
    }

    public async Task<bool> HasProduccionLoteConfigAsync(string loteId)
    {
        // Verificar que el lote existe y pertenece a la compañía
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId.ToString() == loteId &&
                                       l.CompanyId == _current.CompanyId &&
                                       l.DeletedAt == null);
        if (lote is null) return false;

        // Verificar si existe un ProduccionLote configurado para este lote
        var produccionLote = await _ctx.ProduccionLotes.AsNoTracking()
            .FirstOrDefaultAsync(p => p.LoteId.ToString() == loteId);
        
        return produccionLote is not null;
    }
}
