// src/ZooSanMarino.Infrastructure/Services/ProduccionService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;
using LoteDtos = ZooSanMarino.Application.DTOs.Lotes;
using FarmLiteDto = ZooSanMarino.Application.DTOs.Farms.FarmLiteDto;
using NucleoLiteDto = ZooSanMarino.Application.DTOs.Shared.NucleoLiteDto;
using GalponLiteDto = ZooSanMarino.Application.DTOs.Shared.GalponLiteDto;

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
            var loteIdStr = loteId.ToString();
            var exists = await _context.ProduccionLotes
                .AsNoTracking()
                .AnyAsync(p => p.LoteId == loteIdStr && p.DeletedAt == null);
            
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
        if (request.FechaInicio.Date > DateTime.Today)
        {
            throw new ArgumentException("La fecha de inicio no puede ser en el futuro.");
        }

        var produccionLote = new ProduccionLote
        {
            LoteId = request.LoteId.ToString(),
            FechaInicio = request.FechaInicio,
            AvesInicialesH = request.AvesInicialesH,
            AvesInicialesM = request.AvesInicialesM,
            HuevosIniciales = request.HuevosIniciales,
            TipoNido = request.TipoNido,
            GranjaId = lote.GranjaId,
            NucleoId = lote.NucleoId ?? "",
            NucleoP = request.NucleoP,
            GalponId = lote.GalponId,
            Ciclo = request.Ciclo
        };

        _context.ProduccionLotes.Add(produccionLote);
        await _context.SaveChangesAsync();

        return produccionLote.Id;
    }

    public async Task<ProduccionLoteDetalleDto?> ObtenerProduccionLoteAsync(int loteId)
    {
        var loteIdStr = loteId.ToString();
        var produccionLote = await _context.ProduccionLotes
            .AsNoTracking()
            .Where(p => p.LoteId == loteIdStr)
            .Select(p => new ProduccionLoteDetalleDto(
                p.Id,
                int.Parse(p.LoteId),
                p.FechaInicio,
                p.AvesInicialesH,
                p.AvesInicialesM,
                p.HuevosIniciales,
                p.TipoNido,
                p.Ciclo,
                p.CreatedAt,
                p.UpdatedAt
            ))
            .FirstOrDefaultAsync();

        return produccionLote;
    }

    public async Task<int> CrearSeguimientoAsync(CrearSeguimientoRequest request)
    {
        // Validar que el ProduccionLote existe y obtener el LoteId
        var produccionLote = await _context.ProduccionLotes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProduccionLoteId);

        if (produccionLote == null)
        {
            throw new ArgumentException("El registro de producción especificado no existe.");
        }

        // Usar LoteId directamente (es string en la BD)
        var loteId = produccionLote.LoteId;

        // Validar que no existe ya un seguimiento para esta fecha y lote
        var existeSeguimiento = await _context.SeguimientoProduccion
            .AsNoTracking()
            .AnyAsync(s => s.LoteId == loteId && 
                          s.Fecha.Date == request.FechaRegistro.Date);

        if (existeSeguimiento)
        {
            throw new InvalidOperationException("Ya existe un seguimiento para esta fecha.");
        }

        // Validar que la fecha no sea en el futuro
        if (request.FechaRegistro.Date > DateTime.Today)
        {
            throw new ArgumentException("La fecha de registro no puede ser en el futuro.");
        }

        // Convertir consumo a kg si viene en gramos
        decimal consumoKgH = 0;
        if (request.ConsumoH.HasValue && request.ConsumoH.Value > 0)
        {
            var unidadH = (request.UnidadConsumoH ?? "kg").ToLower().Trim();
            if (unidadH == "g" || unidadH == "gramos" || unidadH == "gramo")
            {
                consumoKgH = (decimal)(request.ConsumoH.Value / 1000.0); // Convertir gramos a kg
            }
            else
            {
                consumoKgH = (decimal)request.ConsumoH.Value; // Ya está en kg
            }
        }
        
        decimal consumoKgM = 0;
        if (request.ConsumoM.HasValue && request.ConsumoM.Value > 0)
        {
            var unidadM = (request.UnidadConsumoM ?? "kg").ToLower().Trim();
            if (unidadM == "g" || unidadM == "gramos" || unidadM == "gramo")
            {
                consumoKgM = (decimal)(request.ConsumoM.Value / 1000.0); // Convertir gramos a kg
            }
            else
            {
                consumoKgM = (decimal)request.ConsumoM.Value; // Ya está en kg
            }
        }
        
        // Construir metadata JSONB
        var metadata = BuildMetadata(
            request.ConsumoH, request.UnidadConsumoH, 
            request.ConsumoM, request.UnidadConsumoM,
            request.TipoItemHembras, request.TipoItemMachos,
            request.TipoAlimentoHembras, request.TipoAlimentoMachos
        );

        // Crear seguimiento usando SeguimientoProduccion (tabla produccion_diaria)
        var seguimiento = new Domain.Entities.SeguimientoProduccion
        {
            LoteId = loteId,
            Fecha = request.FechaRegistro,
            MortalidadH = request.MortalidadH,
            MortalidadM = request.MortalidadM,
            SelH = request.SelH,
            SelM = request.SelM,
            ConsKgH = consumoKgH,
            ConsKgM = consumoKgM,
            HuevoTot = request.HuevosTotales,
            HuevoInc = request.HuevosIncubables,
            // Campos de Clasificadora de Huevos
            HuevoLimpio = request.HuevoLimpio,
            HuevoTratado = request.HuevoTratado,
            HuevoSucio = request.HuevoSucio,
            HuevoDeforme = request.HuevoDeforme,
            HuevoBlanco = request.HuevoBlanco,
            HuevoDobleYema = request.HuevoDobleYema,
            HuevoPiso = request.HuevoPiso,
            HuevoPequeno = request.HuevoPequeno,
            HuevoRoto = request.HuevoRoto,
            HuevoDesecho = request.HuevoDesecho,
            HuevoOtro = request.HuevoOtro,
            TipoAlimento = request.TipoAlimento,
            PesoHuevo = request.PesoHuevo,
            Etapa = request.Etapa,
            Observaciones = request.Observaciones,
            // Campos de Pesaje Semanal
            PesoH = request.PesoH,
            PesoM = request.PesoM,
            Uniformidad = request.Uniformidad,
            CoeficienteVariacion = request.CoeficienteVariacion,
            ObservacionesPesaje = request.ObservacionesPesaje,
            Metadata = metadata
        };

        _context.SeguimientoProduccion.Add(seguimiento);
        await _context.SaveChangesAsync();

        return seguimiento.Id;
    }

    public async Task<ListaSeguimientoResponse> ListarSeguimientoAsync(int loteId, DateTime? desde, DateTime? hasta, int page, int size)
    {
        // Convertir int loteId a string (la BD usa text)
        var loteIdStr = loteId.ToString();
        
        // Obtener el ProduccionLoteId para este lote (para el DTO)
        var produccionLoteId = await _context.ProduccionLotes
            .AsNoTracking()
            .Where(p => p.LoteId == loteIdStr)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        // Consultar SeguimientoProduccion usando LoteId directamente
        var query = _context.SeguimientoProduccion
            .AsNoTracking()
            .Where(s => s.LoteId == loteIdStr);

        // Aplicar filtros de fecha
        if (desde.HasValue)
        {
            query = query.Where(s => s.Fecha >= desde.Value);
        }

        if (hasta.HasValue)
        {
            query = query.Where(s => s.Fecha <= hasta.Value);
        }

        // Contar total
        var total = await query.CountAsync();

        // Obtener registros con paginación y mapear a DTO
        var seguimientos = await query
            .OrderByDescending(s => s.Fecha)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        // Mapear a SeguimientoItemDto
        var items = seguimientos.Select(s => new SeguimientoItemDto(
            s.Id,
            produccionLoteId, // ProduccionLoteId para el DTO
            s.Fecha, // FechaRegistro
            s.MortalidadH,
            s.MortalidadM,
            s.SelH,
            s.SelM,
            s.ConsKgH,
            s.ConsKgM,
            s.ConsKgH + s.ConsKgM, // ConsumoKg = suma de consumos (para compatibilidad)
            s.HuevoTot, // HuevosTotales
            s.HuevoInc, // HuevosIncubables
            s.TipoAlimento,
            s.PesoHuevo,
            s.Etapa,
            s.Observaciones,
            DateTime.MinValue, // CreatedAt (SeguimientoProduccion no tiene este campo)
            null, // UpdatedAt
            // Campos de Clasificadora de Huevos
            s.HuevoLimpio,
            s.HuevoTratado,
            s.HuevoSucio,
            s.HuevoDeforme,
            s.HuevoBlanco,
            s.HuevoDobleYema,
            s.HuevoPiso,
            s.HuevoPequeno,
            s.HuevoRoto,
            s.HuevoDesecho,
            s.HuevoOtro,
            // Campos de Pesaje Semanal
            s.PesoH,
            s.PesoM,
            s.Uniformidad,
            s.CoeficienteVariacion,
            s.ObservacionesPesaje
        )).ToList();

        return new ListaSeguimientoResponse(items, total);
    }

    public async Task<SeguimientoItemDto?> ObtenerSeguimientoPorIdAsync(int seguimientoId)
    {
        var seguimiento = await _context.SeguimientoProduccion
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == seguimientoId);

        if (seguimiento == null)
        {
            return null;
        }

        // Obtener el ProduccionLoteId
        var produccionLoteId = await _context.ProduccionLotes
            .AsNoTracking()
            .Where(p => p.LoteId == seguimiento.LoteId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        // Mapear a SeguimientoItemDto
        return new SeguimientoItemDto(
            seguimiento.Id,
            produccionLoteId,
            seguimiento.Fecha,
            seguimiento.MortalidadH,
            seguimiento.MortalidadM,
            seguimiento.SelH,
            seguimiento.SelM,
            seguimiento.ConsKgH,
            seguimiento.ConsKgM,
            seguimiento.ConsKgH + seguimiento.ConsKgM,
            seguimiento.HuevoTot,
            seguimiento.HuevoInc,
            seguimiento.TipoAlimento,
            seguimiento.PesoHuevo,
            seguimiento.Etapa,
            seguimiento.Observaciones,
            DateTime.MinValue,
            null,
            // Campos de Clasificadora de Huevos
            seguimiento.HuevoLimpio,
            seguimiento.HuevoTratado,
            seguimiento.HuevoSucio,
            seguimiento.HuevoDeforme,
            seguimiento.HuevoBlanco,
            seguimiento.HuevoDobleYema,
            seguimiento.HuevoPiso,
            seguimiento.HuevoPequeno,
            seguimiento.HuevoRoto,
            seguimiento.HuevoDesecho,
            seguimiento.HuevoOtro,
            // Campos de Pesaje Semanal
            seguimiento.PesoH,
            seguimiento.PesoM,
            seguimiento.Uniformidad,
            seguimiento.CoeficienteVariacion,
            seguimiento.ObservacionesPesaje
        );
    }

    /// <summary>
    /// Elimina un seguimiento diario de producción
    /// </summary>
    public async Task<bool> EliminarSeguimientoAsync(int seguimientoId)
    {
        var seguimiento = await _context.SeguimientoProduccion.FindAsync(seguimientoId);
        if (seguimiento == null) return false;

        // Validar que el lote pertenece a la compañía del usuario actual
        var loteIdStr = seguimiento.LoteId;
        var lote = await _context.Lotes
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LoteId.ToString() == loteIdStr && 
                                      l.CompanyId == _currentUser.CompanyId && 
                                      l.DeletedAt == null);
        
        if (lote == null) return false;

        _context.SeguimientoProduccion.Remove(seguimiento);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Obtiene los lotes que tienen semana 26 o superior (calculada desde fechaEncaset)
    /// Solo muestra lotes que han alcanzado la semana 26 de producción
    /// </summary>
    public async Task<IEnumerable<LoteDtos.LoteDetailDto>> ObtenerLotesProduccionAsync()
    {
        var fechaHoy = DateTime.Today;
        
        // Calcular la fecha límite: para estar en semana 26, deben haber pasado al menos 26 semanas (182 días)
        // Semana 26 = días desde fechaEncaset >= 182 días
        // Por lo tanto: fechaEncaset <= fechaHoy - 182 días
        var diasSemana26 = 26 * 7; // 182 días
        var fechaLimiteSemana26 = fechaHoy.AddDays(-diasSemana26);

        // Un lote está en semana 26 o más si: fechaEncaset <= fechaLimiteSemana26
        // Esto significa que han pasado al menos 26 semanas desde fechaEncaset
        var lotes = await _context.Lotes
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .Include(l => l.Galpon)
            .Where(l => 
                l.CompanyId == _currentUser.CompanyId && 
                l.DeletedAt == null &&
                l.FechaEncaset != null &&
                l.FechaEncaset <= fechaLimiteSemana26)
            .OrderBy(l => l.LoteId)
            .ToListAsync();

        // Proyectar a LoteDetailDto
        return lotes.Select(l => new LoteDtos.LoteDetailDto(
            l.LoteId ?? 0,
            l.LoteNombre,
            l.GranjaId,
            l.NucleoId,
            l.GalponId,
            l.Regional,
            l.FechaEncaset,
            l.HembrasL,
            l.MachosL,
            l.PesoInicialH,
            l.PesoInicialM,
            l.UnifH,
            l.UnifM,
            l.MortCajaH,
            l.MortCajaM,
            l.Raza,
            l.AnoTablaGenetica,
            l.Linea,
            l.TipoLinea,
            l.CodigoGuiaGenetica,
            l.LineaGeneticaId,
            l.Tecnico,
            l.Mixtas,
            l.PesoMixto,
            l.AvesEncasetadas,
            l.EdadInicial,
            l.LoteErp,
            l.EstadoTraslado,
            l.LotePadreId,  // ← NUEVO: ID del lote padre
            l.CompanyId,
            l.CreatedByUserId,
            l.CreatedAt,
            l.UpdatedByUserId,
            l.UpdatedAt,
            // Cargar relaciones
            l.Farm != null ? new FarmLiteDto(
                l.Farm.Id,
                l.Farm.Name,
                l.Farm.RegionalId,
                l.Farm.DepartamentoId,
                l.Farm.MunicipioId
            ) : throw new InvalidOperationException($"Lote {l.LoteId} no tiene Farm asociado"),
            l.Nucleo != null ? new NucleoLiteDto(
                l.Nucleo.NucleoId,
                l.Nucleo.NucleoNombre,
                l.Nucleo.GranjaId
            ) : null,
            l.Galpon != null ? new GalponLiteDto(
                l.Galpon.GalponId,
                l.Galpon.GalponNombre,
                l.Galpon.NucleoId,
                l.Galpon.GranjaId
            ) : null
        ));
    }
    
    /// <summary>
    /// Construye el objeto Metadata JSONB con los campos adicionales.
    /// </summary>
    private static System.Text.Json.JsonDocument? BuildMetadata(
        double? consumoHembras, string? unidadHembras, 
        double? consumoMachos, string? unidadMachos,
        string? tipoItemHembras, string? tipoItemMachos,
        int? tipoAlimentoHembras, int? tipoAlimentoMachos)
    {
        var metadata = new Dictionary<string, object?>();
        
        // Consumo original con unidad
        if (consumoHembras.HasValue)
        {
            metadata["consumoOriginalHembras"] = consumoHembras.Value;
            metadata["unidadConsumoOriginalHembras"] = unidadHembras ?? "kg";
        }
        
        if (consumoMachos.HasValue)
        {
            metadata["consumoOriginalMachos"] = consumoMachos.Value;
            metadata["unidadConsumoOriginalMachos"] = unidadMachos ?? "kg";
        }
        
        // Tipo de ítem (alimento, medicamento, etc.)
        if (!string.IsNullOrWhiteSpace(tipoItemHembras))
        {
            metadata["tipoItemHembras"] = tipoItemHembras;
        }
        
        if (!string.IsNullOrWhiteSpace(tipoItemMachos))
        {
            metadata["tipoItemMachos"] = tipoItemMachos;
        }
        
        // IDs de alimentos seleccionados
        if (tipoAlimentoHembras.HasValue)
        {
            metadata["tipoAlimentoHembras"] = tipoAlimentoHembras.Value;
        }
        
        if (tipoAlimentoMachos.HasValue)
        {
            metadata["tipoAlimentoMachos"] = tipoAlimentoMachos.Value;
        }
        
        if (metadata.Count == 0) return null;
        
        return System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(metadata));
    }
}
