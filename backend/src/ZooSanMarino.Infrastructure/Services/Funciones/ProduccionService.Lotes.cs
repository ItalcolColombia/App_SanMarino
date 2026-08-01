// Infrastructure/Services/Funciones/ProduccionService.Lotes.cs — registro inicial de producción sobre la tabla unificada lotes (existe/crear/obtener) y listado de lotes que alcanzaron producción.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;
using LoteDtos = ZooSanMarino.Application.DTOs.Lotes;
using FarmLiteDto = ZooSanMarino.Application.DTOs.Farms.FarmLiteDto;
using NucleoLiteDto = ZooSanMarino.Application.DTOs.Shared.NucleoLiteDto;
using GalponLiteDto = ZooSanMarino.Application.DTOs.Shared.GalponLiteDto;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ProduccionService
{
    /// <summary>
    /// True solo si el lote tiene registro inicial de producción con datos llenos (tabla unificada lotes).
    /// No basta con Fase = Produccion; debe tener campos de producción llenos (HembrasInicialesProd o FechaInicioProduccion).
    /// 1) Lote hijo con LotePadreId = loteId, Fase = Produccion y datos llenos, o
    /// 2) El mismo lote con Fase = Produccion y datos llenos.
    /// </summary>
    public async Task<bool> ExisteProduccionLoteAsync(int loteId)
    {
        try
        {
            var companyId = _currentUser.CompanyId;
            // Caso 1: lote hijo en fase Producción con datos de registro inicial
            var tieneHijoProd = await _context.Lotes.AsNoTracking()
                .AnyAsync(l => l.LotePadreId == loteId && l.Fase == "Produccion" && l.DeletedAt == null && l.CompanyId == companyId
                    && (l.HembrasInicialesProd != null || l.FechaInicioProduccion != null));
            if (tieneHijoProd) return true;
            // Caso 2: mismo lote en producción con datos de registro inicial
            var mismoLoteEnProd = await _context.Lotes.AsNoTracking()
                .AnyAsync(l => l.LoteId == loteId && l.Fase == "Produccion" && l.DeletedAt == null && l.CompanyId == companyId
                    && (l.HembrasInicialesProd != null || l.FechaInicioProduccion != null));
            return mismoLoteEnProd;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking production lote existence: {ex.Message}");
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

        // Cerrar etapa Levante: registrar con cuántas aves termina (saldos actuales)
        var resumenLevante = await _loteService.GetMortalidadResumenAsync(request.LoteId);
        var etapaLevante = await _context.LoteEtapaLevante
            .FirstOrDefaultAsync(el => el.LoteId == request.LoteId);
        if (etapaLevante != null)
        {
            etapaLevante.FechaFin = request.FechaInicio.ToUniversalTime();
            etapaLevante.AvesFinHembras = resumenLevante?.SaldoHembras ?? request.AvesInicialesH;
            etapaLevante.AvesFinMachos = resumenLevante?.SaldoMachos ?? request.AvesInicialesM;
            etapaLevante.UpdatedAt = DateTime.UtcNow;
        }

        // Crear lote hijo en fase Producción (Opción B: unificado en lotes).
        // Copiar Raza y AnoTablaGenetica del padre para que indicadores y guía genética tengan datos.
        var loteProd = new Lote
        {
            LoteNombre = (lote.LoteNombre ?? "").Trim() + " - Prod",
            GranjaId = lote.GranjaId,
            NucleoId = lote.NucleoId ?? "",
            GalponId = lote.GalponId,
            Fase = "Produccion",
            LotePadreId = request.LoteId,
            FechaInicioProduccion = request.FechaInicio,
            HembrasInicialesProd = request.AvesInicialesH,
            MachosInicialesProd = request.AvesInicialesM >= 0 ? request.AvesInicialesM : 0,
            HuevosIniciales = request.HuevosIniciales,
            TipoNido = request.TipoNido,
            NucleoP = request.NucleoP,
            CicloProduccion = request.Ciclo,
            CompanyId = lote.CompanyId,
            Raza = lote.Raza,
            AnoTablaGenetica = lote.AnoTablaGenetica,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Lotes.Add(loteProd);
        await _context.SaveChangesAsync();

        return loteProd.LoteId.GetValueOrDefault();
    }

    /// <summary>
    /// Obtiene el lote en fase Producción para el lote dado (tabla unificada lotes).
    /// 1) Si existe hijo con Fase = Produccion, devuelve el hijo (Id = LoteId del hijo).
    /// 2) Si el mismo lote tiene Fase = Produccion y campos de producción llenos, devuelve ese lote (Id = loteId).
    /// </summary>
    public async Task<ProduccionLoteDetalleDto?> ObtenerProduccionLoteAsync(int loteId)
    {
        var companyId = _currentUser.CompanyId;

        // 1) Buscar lote hijo en fase Producción con datos de registro inicial
        var hijo = await _context.Lotes
            .AsNoTracking()
            .Where(l => l.LotePadreId == loteId && l.Fase == "Produccion" && l.DeletedAt == null && l.CompanyId == companyId
                && (l.HembrasInicialesProd != null || l.FechaInicioProduccion != null))
            .OrderBy(l => l.LoteId)
            .Select(l => new ProduccionLoteDetalleDto(
                l.LoteId ?? 0,
                loteId,
                l.FechaInicioProduccion ?? DateTime.MinValue,
                l.HembrasInicialesProd ?? 0,
                l.MachosInicialesProd ?? 0,
                l.HuevosIniciales ?? 0,
                l.TipoNido ?? "Manual",
                l.CicloProduccion ?? "normal",
                l.CreatedAt,
                l.UpdatedAt
            ))
            .FirstOrDefaultAsync();

        if (hijo != null) return hijo;

        // 2) Mismo lote en producción (creado directo, sin hijo)
        var mismo = await _context.Lotes
            .AsNoTracking()
            .Where(l => l.LoteId == loteId && l.CompanyId == companyId && l.DeletedAt == null
                && l.Fase == "Produccion"
                && (l.HembrasInicialesProd != null || l.FechaInicioProduccion != null))
            .Select(l => new ProduccionLoteDetalleDto(
                l.LoteId ?? 0,
                loteId,
                l.FechaInicioProduccion ?? DateTime.MinValue,
                l.HembrasInicialesProd ?? 0,
                l.MachosInicialesProd ?? 0,
                l.HuevosIniciales ?? 0,
                l.TipoNido ?? "Manual",
                l.CicloProduccion ?? "normal",
                l.CreatedAt,
                l.UpdatedAt
            ))
            .FirstOrDefaultAsync();

        return mismo;
    }

    /// <summary>
    /// Obtiene los lotes que ya alcanzaron la etapa de producción (calculada desde fechaEncaset).
    /// REQ-012b: el umbral es la semana 25 de vida (antes 26). Además corrige el off-by-one previo:
    /// con 182 días (26*7) un lote solo aparecía al iniciar la semana 27 (semanaVida = dias/7 + 1),
    /// no en la 26. Con 175 días (25*7) el lote aparece al iniciar la semana 26 y las semanas 25 ya
    /// capturadas quedan habilitadas.
    /// <paramref name="paraDestino"/> = true omite el alcance granular de ubicación (los modales de
    /// traslado usan este listado como catálogo de lote DESTINO), igual que ILoteService.GetAllAsync.
    /// </summary>
    public async Task<IEnumerable<LoteDtos.LoteDetailDto>> ObtenerLotesProduccionAsync(bool paraDestino = false)
    {
        var fechaHoy = DateTime.Today;

        // semanaVida = dias/7 + 1 (división entera). Umbral REQ-012b: 25 semanas = 175 días.
        var diasSemanaProduccion = 25 * 7; // 175 días
        var fechaLimiteProduccion = fechaHoy.AddDays(-diasSemanaProduccion);

        IQueryable<Lote> q = _context.Lotes
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .Include(l => l.Galpon)
            .Where(l =>
                l.CompanyId == _currentUser.CompanyId &&
                l.DeletedAt == null &&
                l.FechaEncaset != null &&
                l.FechaEncaset <= fechaLimiteProduccion);

        // Alcance granular núcleo/galpón/lote (lote_id es PK global ⇒ la unión entre granjas es
        // exacta). Sin granjas restringidas la query queda intacta.
        if (!paraDestino)
        {
            var restringidos = await _scopeResolver.GetAllRestrictedScopesAsync();
            if (restringidos.Count > 0)
            {
                var granjasRestringidas = restringidos.Keys.ToList();
                var lotesPermitidos = restringidos.SelectMany(kv => kv.Value.LotesPermitidos).ToList();
                q = q.Where(l => !granjasRestringidas.Contains(l.GranjaId) ||
                                 (l.LoteId != null && lotesPermitidos.Contains(l.LoteId.Value)));
            }
        }

        var lotes = await q
            .OrderBy(l => l.LoteId)
            .ToListAsync();

        // Proyectar a LoteDetailDto
        return lotes.Select(l => new LoteDtos.LoteDetailDto(
            l.LoteId ?? 0,
            l.LoteNombre,
            l.LotePosturaBaseId,
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
            l.LotePadreId,
            l.PaisId,
            l.PaisNombre,
            l.EmpresaNombre,
            l.CompanyId,
            l.CreatedByUserId,
            l.CreatedAt,
            l.UpdatedByUserId,
            l.UpdatedAt,
            // Relaciones
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
}
