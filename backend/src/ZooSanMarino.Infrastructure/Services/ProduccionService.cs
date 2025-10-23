// src/ZooSanMarino.Infrastructure/Services/ProduccionService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class ProduccionService : IProduccionService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;

    public ProduccionService(ZooSanMarinoContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<bool> ExisteProduccionLoteAsync(int loteId)
    {
        try
        {
            var exists = await _context.ProduccionLotes
                .AsNoTracking()
                .AnyAsync(p => p.LoteId == loteId && p.DeletedAt == null);
            
            return exists;
        }
        catch (Exception ex)
        {
            // Log the exception if needed
            Console.WriteLine($"Error checking ProduccionLote existence: {ex.Message}");
            return false;
        }
    }

    public async Task<int> CrearProduccionLoteAsync(CrearProduccionLoteRequest request)
    {
        // Validar que el lote existe y pertenece a la empresa del usuario
        var lote = await _context.Lotes
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LoteId == request.LoteId && l.CompanyId == _currentUser.CompanyId);

        if (lote == null)
        {
            throw new ArgumentException("El lote especificado no existe o no pertenece a su empresa.");
        }

        // Validar que no existe ya un registro inicial para este lote
        var existe = await ExisteProduccionLoteAsync(request.LoteId);
        if (existe)
        {
            throw new InvalidOperationException("Ya existe un registro inicial de producción para este lote.");
        }

        // Validar que la fecha no sea en el futuro
        if (request.FechaInicio > DateTime.Today)
        {
            throw new ArgumentException("La fecha de inicio no puede ser en el futuro.");
        }

        var produccionLote = new ProduccionLote
        {
            LoteId = request.LoteId,
            FechaInicio = request.FechaInicio,
            AvesInicialesH = request.AvesInicialesH,
            AvesInicialesM = request.AvesInicialesM,
            Observaciones = request.Observaciones
        };

        _context.ProduccionLotes.Add(produccionLote);
        await _context.SaveChangesAsync();

        return produccionLote.Id;
    }

    public async Task<ProduccionLoteDetalleDto?> ObtenerProduccionLoteAsync(int loteId)
    {
        var produccionLote = await _context.ProduccionLotes
            .AsNoTracking()
            .Where(p => p.LoteId == loteId)
            .Select(p => new ProduccionLoteDetalleDto(
                p.Id,
                p.LoteId,
                p.FechaInicio,
                p.AvesInicialesH,
                p.AvesInicialesM,
                p.Observaciones,
                p.CreatedAt,
                p.UpdatedAt
            ))
            .FirstOrDefaultAsync();

        return produccionLote;
    }

    public async Task<int> CrearSeguimientoAsync(CrearSeguimientoRequest request)
    {
        // Validar que el ProduccionLote existe
        var produccionLote = await _context.ProduccionLotes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProduccionLoteId);

        if (produccionLote == null)
        {
            throw new ArgumentException("El registro de producción especificado no existe.");
        }

        // Validar que no existe ya un seguimiento para esta fecha
        var existeSeguimiento = await _context.ProduccionSeguimientos
            .AsNoTracking()
            .AnyAsync(s => s.ProduccionLoteId == request.ProduccionLoteId && 
                          s.FechaRegistro.Date == request.FechaRegistro.Date);

        if (existeSeguimiento)
        {
            throw new InvalidOperationException("Ya existe un seguimiento para esta fecha.");
        }

        // Validar que la fecha no sea en el futuro
        if (request.FechaRegistro > DateTime.Today)
        {
            throw new ArgumentException("La fecha de registro no puede ser en el futuro.");
        }

        var seguimiento = new ProduccionSeguimiento
        {
            ProduccionLoteId = request.ProduccionLoteId,
            FechaRegistro = request.FechaRegistro,
            MortalidadH = request.MortalidadH,
            MortalidadM = request.MortalidadM,
            ConsumoKg = request.ConsumoKg,
            HuevosTotales = request.HuevosTotales,
            HuevosIncubables = request.HuevosIncubables,
            PesoHuevo = request.PesoHuevo,
            Observaciones = request.Observaciones
        };

        _context.ProduccionSeguimientos.Add(seguimiento);
        await _context.SaveChangesAsync();

        return seguimiento.Id;
    }

    public async Task<ListaSeguimientoResponse> ListarSeguimientoAsync(int loteId, DateTime? desde, DateTime? hasta, int page, int size)
    {
        // Obtener el ProduccionLoteId para este lote
        var produccionLoteId = await _context.ProduccionLotes
            .AsNoTracking()
            .Where(p => p.LoteId == loteId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (produccionLoteId == 0)
        {
            return new ListaSeguimientoResponse(new List<SeguimientoItemDto>(), 0);
        }

        var query = _context.ProduccionSeguimientos
            .AsNoTracking()
            .Where(s => s.ProduccionLoteId == produccionLoteId);

        // Aplicar filtros de fecha
        if (desde.HasValue)
        {
            query = query.Where(s => s.FechaRegistro >= desde.Value);
        }

        if (hasta.HasValue)
        {
            query = query.Where(s => s.FechaRegistro <= hasta.Value);
        }

        // Contar total
        var total = await query.CountAsync();

        // Aplicar paginación y ordenamiento
        var items = await query
            .OrderByDescending(s => s.FechaRegistro)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(s => new SeguimientoItemDto(
                s.Id,
                s.ProduccionLoteId,
                s.FechaRegistro,
                s.MortalidadH,
                s.MortalidadM,
                s.ConsumoKg,
                s.HuevosTotales,
                s.HuevosIncubables,
                s.PesoHuevo,
                s.Observaciones,
                s.CreatedAt,
                s.UpdatedAt
            ))
            .ToListAsync();

        return new ListaSeguimientoResponse(items, total);
    }
}
