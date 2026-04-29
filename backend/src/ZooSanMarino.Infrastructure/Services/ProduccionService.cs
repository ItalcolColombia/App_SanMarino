// src/ZooSanMarino.Infrastructure/Services/ProduccionService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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

public class ProduccionService : IProduccionService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILoteService _loteService;
    private readonly IEspejoHuevoProduccionSyncService _espejoHuevoSync;

    /// <summary>
    /// Seguimiento diario postura (producción) no aplica consumo/devolución sobre inventario-gestion.
    /// El inventario nuevo (inventario-gestion / item_inventario_ecuador) es exclusivo del módulo Seguimiento diario pollo de engorde.
    /// </summary>
    public ProduccionService(
        ZooSanMarinoContext context,
        ICurrentUser currentUser,
        ILoteService loteService,
        IEspejoHuevoProduccionSyncService espejoHuevoSync)
    {
        _context = context;
        _currentUser = currentUser;
        _loteService = loteService;
        _espejoHuevoSync = espejoHuevoSync;
    }

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

    public async Task<int> CrearSeguimientoAsync(CrearSeguimientoRequest request)
    {
        if (!request.LotePosturaProduccionId.HasValue && !request.ProduccionLoteId.HasValue)
            throw new ArgumentException("Debe especificar ProduccionLoteId o LotePosturaProduccionId.");
        if (request.LotePosturaProduccionId.HasValue && request.ProduccionLoteId.HasValue)
            throw new ArgumentException("Especifique solo ProduccionLoteId o LotePosturaProduccionId, no ambos.");

        int loteId;
        int? lotePosturaProduccionId = request.LotePosturaProduccionId;

        if (lotePosturaProduccionId.HasValue)
        {
            var lpp = await _context.LotePosturaProduccion
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LotePosturaProduccionId == lotePosturaProduccionId.Value
                    && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null);
            if (lpp == null)
                throw new ArgumentException("El lote postura producción especificado no existe o no pertenece a la empresa.");
            loteId = lpp.LoteId ?? 0;
            if (loteId <= 0)
                throw new InvalidOperationException("El lote postura producción no tiene LoteId asociado (requerido para guardar en produccion_diaria).");

            var existeSeguimientoLpp = await _context.SeguimientoProduccion.AsNoTracking()
                .AnyAsync(s => s.LotePosturaProduccionId == lotePosturaProduccionId
                    && s.Fecha.Date == request.FechaRegistro.Date);
            if (existeSeguimientoLpp)
                throw new InvalidOperationException("Ya existe un seguimiento para esta fecha y lote.");
        }
        else
        {
            var loteProd = await _context.Lotes
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == request.ProduccionLoteId && l.Fase == "Produccion" && l.DeletedAt == null);
            if (loteProd == null)
                throw new ArgumentException("El registro de producción (lote en fase Producción) especificado no existe.");
            loteId = loteProd.LoteId ?? request.ProduccionLoteId!.Value;

            var existeSeguimiento = await _context.SeguimientoProduccion.AsNoTracking()
                .AnyAsync(s => s.LoteId == loteId && s.Fecha.Date == request.FechaRegistro.Date);
            if (existeSeguimiento)
                throw new InvalidOperationException("Ya existe un seguimiento para esta fecha.");
        }

        // Validar que la fecha no sea en el futuro
        if (request.FechaRegistro.Date > DateTime.Today)
        {
            throw new ArgumentException("La fecha de registro no puede ser en el futuro.");
        }

        decimal consumoKgH;
        decimal consumoKgM;
        JsonDocument? metadata;
        JsonDocument? itemsAdicionales = null;
        var tipoAlimento = request.TipoAlimento ?? string.Empty;

        var useItems = (request.ItemsHembras != null && request.ItemsHembras.Count > 0) ||
                       (request.ItemsMachos != null && request.ItemsMachos.Count > 0);

        if (useItems)
        {
            var (alimentosHembras, otrosHembras) = SepararAlimentosYOtrosItems(request.ItemsHembras);
            var (alimentosMachos, otrosMachos) = SepararAlimentosYOtrosItems(request.ItemsMachos);
            consumoKgH = (decimal)CalcularConsumoTotalAlimentos(alimentosHembras);
            consumoKgM = (decimal)CalcularConsumoTotalAlimentos(alimentosMachos);
            if (string.IsNullOrWhiteSpace(tipoAlimento))
                tipoAlimento = ConstruirTipoAlimentoString(request.ItemsHembras, request.ItemsMachos);
            metadata = BuildMetadataFromItems(request.ItemsHembras, request.ItemsMachos,
                request.ConsumoH, request.UnidadConsumoH, request.ConsumoM, request.UnidadConsumoM,
                request.TipoItemHembras, request.TipoItemMachos,
                request.TipoAlimentoHembras, request.TipoAlimentoMachos);
            itemsAdicionales = BuildItemsAdicionales(otrosHembras, otrosMachos);
        }
        else
        {
            consumoKgH = 0;
            if (request.ConsumoH.HasValue && request.ConsumoH.Value > 0)
            {
                var unidadH = (request.UnidadConsumoH ?? "kg").ToLower().Trim();
                consumoKgH = unidadH == "g" || unidadH == "gramos" || unidadH == "gramo"
                    ? (decimal)(request.ConsumoH.Value / 1000.0)
                    : (decimal)request.ConsumoH.Value;
            }
            consumoKgM = 0;
            if (request.ConsumoM.HasValue && request.ConsumoM.Value > 0)
            {
                var unidadM = (request.UnidadConsumoM ?? "kg").ToLower().Trim();
                consumoKgM = unidadM == "g" || unidadM == "gramos" || unidadM == "gramo"
                    ? (decimal)(request.ConsumoM.Value / 1000.0)
                    : (decimal)request.ConsumoM.Value;
            }
            metadata = BuildMetadata(
                request.ConsumoH, request.UnidadConsumoH,
                request.ConsumoM, request.UnidadConsumoM,
                request.TipoItemHembras, request.TipoItemMachos,
                request.TipoAlimentoHembras, request.TipoAlimentoMachos
            );
        }

        var entity = new SeguimientoProduccion
        {
            LoteId = loteId,
            LotePosturaProduccionId = lotePosturaProduccionId,
            Fecha = request.FechaRegistro,
            MortalidadH = request.MortalidadH,
            MortalidadM = request.MortalidadM,
            SelH = request.SelH,
            SelM = request.SelM,
            ConsKgH = consumoKgH,
            ConsKgM = consumoKgM,
            HuevoTot = request.HuevosTotales,
            HuevoInc = request.HuevosIncubables,
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
            TipoAlimento = tipoAlimento,
            Observaciones = request.Observaciones,
            PesoHuevo = request.PesoHuevo,
            Etapa = request.Etapa,
            PesoH = request.PesoH,
            PesoM = request.PesoM,
            Uniformidad = request.Uniformidad,
            CoeficienteVariacion = request.CoeficienteVariacion,
            ObservacionesPesaje = request.ObservacionesPesaje,
            Metadata = metadata,
            ConsumoAguaDiario = request.ConsumoAguaDiario,
            ConsumoAguaPh = request.ConsumoAguaPh,
            ConsumoAguaOrp = request.ConsumoAguaOrp,
            ConsumoAguaTemperatura = request.ConsumoAguaTemperatura
        };

        _context.SeguimientoProduccion.Add(entity);
        await _context.SaveChangesAsync();
        if (lotePosturaProduccionId.HasValue)
            await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(lotePosturaProduccionId.Value).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task ActualizarSeguimientoAsync(int id, CrearSeguimientoRequest request)
    {
        if (!request.LotePosturaProduccionId.HasValue && !request.ProduccionLoteId.HasValue)
            throw new ArgumentException("Debe especificar ProduccionLoteId o LotePosturaProduccionId.");
        if (request.LotePosturaProduccionId.HasValue && request.ProduccionLoteId.HasValue)
            throw new ArgumentException("Especifique solo ProduccionLoteId o LotePosturaProduccionId, no ambos.");

        int loteId;
        int? lotePosturaProduccionId = request.LotePosturaProduccionId;

        if (lotePosturaProduccionId.HasValue)
        {
            var lpp = await _context.LotePosturaProduccion.AsNoTracking()
                .FirstOrDefaultAsync(l => l.LotePosturaProduccionId == lotePosturaProduccionId.Value
                    && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null);
            if (lpp == null)
                throw new ArgumentException("El lote postura producción especificado no existe o no pertenece a la empresa.");
            loteId = lpp.LoteId ?? 0;
            if (loteId <= 0)
                throw new InvalidOperationException("El lote postura producción no tiene LoteId asociado (requerido para guardar en produccion_diaria).");
        }
        else
        {
            var loteProd = await _context.Lotes.AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == request.ProduccionLoteId && l.Fase == "Produccion" && l.DeletedAt == null);
            if (loteProd == null)
                throw new ArgumentException("El registro de producción (lote en fase Producción) especificado no existe.");
            loteId = loteProd.LoteId ?? request.ProduccionLoteId!.Value;
        }

        if (request.FechaRegistro.Date > DateTime.Today)
            throw new ArgumentException("La fecha de registro no puede ser en el futuro.");

        decimal consumoKgH;
        decimal consumoKgM;
        JsonDocument? metadata;
        JsonDocument? itemsAdicionales = null;
        var tipoAlimento = request.TipoAlimento ?? string.Empty;

        var useItems = (request.ItemsHembras != null && request.ItemsHembras.Count > 0) ||
                       (request.ItemsMachos != null && request.ItemsMachos.Count > 0);

        if (useItems)
        {
            var (alimentosHembras, otrosHembras) = SepararAlimentosYOtrosItems(request.ItemsHembras);
            var (alimentosMachos, otrosMachos) = SepararAlimentosYOtrosItems(request.ItemsMachos);
            consumoKgH = (decimal)CalcularConsumoTotalAlimentos(alimentosHembras);
            consumoKgM = (decimal)CalcularConsumoTotalAlimentos(alimentosMachos);
            if (string.IsNullOrWhiteSpace(tipoAlimento))
                tipoAlimento = ConstruirTipoAlimentoString(request.ItemsHembras, request.ItemsMachos);
            metadata = BuildMetadataFromItems(request.ItemsHembras, request.ItemsMachos,
                request.ConsumoH, request.UnidadConsumoH, request.ConsumoM, request.UnidadConsumoM,
                request.TipoItemHembras, request.TipoItemMachos,
                request.TipoAlimentoHembras, request.TipoAlimentoMachos);
            itemsAdicionales = BuildItemsAdicionales(otrosHembras, otrosMachos);
        }
        else
        {
            consumoKgH = 0;
            if (request.ConsumoH.HasValue && request.ConsumoH.Value > 0)
            {
                var unidadH = (request.UnidadConsumoH ?? "kg").ToLowerInvariant().Trim();
                consumoKgH = unidadH == "g" || unidadH == "gramos" || unidadH == "gramo"
                    ? (decimal)(request.ConsumoH.Value / 1000.0)
                    : (decimal)request.ConsumoH.Value;
            }
            consumoKgM = 0;
            if (request.ConsumoM.HasValue && request.ConsumoM.Value > 0)
            {
                var unidadM = (request.UnidadConsumoM ?? "kg").ToLowerInvariant().Trim();
                consumoKgM = unidadM == "g" || unidadM == "gramos" || unidadM == "gramo"
                    ? (decimal)(request.ConsumoM.Value / 1000.0)
                    : (decimal)request.ConsumoM.Value;
            }
            metadata = BuildMetadata(
                request.ConsumoH, request.UnidadConsumoH,
                request.ConsumoM, request.UnidadConsumoM,
                request.TipoItemHembras, request.TipoItemMachos,
                request.TipoAlimentoHembras, request.TipoAlimentoMachos
            );
        }

        var entity = await _context.SeguimientoProduccion
            .FirstOrDefaultAsync(x => x.Id == id)
            .ConfigureAwait(false);
        if (entity == null)
            throw new InvalidOperationException("No se encontró el registro o no tiene permisos para actualizarlo.");

        entity.LoteId = loteId;
        entity.LotePosturaProduccionId = lotePosturaProduccionId;
        entity.Fecha = request.FechaRegistro;
        entity.MortalidadH = request.MortalidadH;
        entity.MortalidadM = request.MortalidadM;
        entity.SelH = request.SelH;
        entity.SelM = request.SelM;
        entity.ConsKgH = consumoKgH;
        entity.ConsKgM = consumoKgM;
        entity.HuevoTot = request.HuevosTotales;
        entity.HuevoInc = request.HuevosIncubables;
        entity.HuevoLimpio = request.HuevoLimpio;
        entity.HuevoTratado = request.HuevoTratado;
        entity.HuevoSucio = request.HuevoSucio;
        entity.HuevoDeforme = request.HuevoDeforme;
        entity.HuevoBlanco = request.HuevoBlanco;
        entity.HuevoDobleYema = request.HuevoDobleYema;
        entity.HuevoPiso = request.HuevoPiso;
        entity.HuevoPequeno = request.HuevoPequeno;
        entity.HuevoRoto = request.HuevoRoto;
        entity.HuevoDesecho = request.HuevoDesecho;
        entity.HuevoOtro = request.HuevoOtro;
        entity.TipoAlimento = tipoAlimento;
        entity.Observaciones = request.Observaciones;
        entity.PesoHuevo = request.PesoHuevo;
        entity.Etapa = request.Etapa;
        entity.PesoH = request.PesoH;
        entity.PesoM = request.PesoM;
        entity.Uniformidad = request.Uniformidad;
        entity.CoeficienteVariacion = request.CoeficienteVariacion;
        entity.ObservacionesPesaje = request.ObservacionesPesaje;
        entity.Metadata = metadata;
        entity.ConsumoAguaDiario = request.ConsumoAguaDiario;
        entity.ConsumoAguaPh = request.ConsumoAguaPh;
        entity.ConsumoAguaOrp = request.ConsumoAguaOrp;
        entity.ConsumoAguaTemperatura = request.ConsumoAguaTemperatura;

        await _context.SaveChangesAsync().ConfigureAwait(false);
        if (lotePosturaProduccionId.HasValue)
            await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(lotePosturaProduccionId.Value).ConfigureAwait(false);
    }

    public async Task<ListaSeguimientoResponse> ListarSeguimientoAsync(int? loteId, int? lotePosturaProduccionId, DateTime? desde, DateTime? hasta, int page, int size)
    {
        if (!lotePosturaProduccionId.HasValue && !loteId.HasValue)
            throw new ArgumentException("Debe especificar loteId o lotePosturaProduccionId.");

        var companyId = _currentUser.CompanyId;
        int produccionLoteId;
        IQueryable<SeguimientoProduccion> q = _context.SeguimientoProduccion.AsNoTracking();

        if (lotePosturaProduccionId.HasValue)
        {
            // Validar pertenencia a compañía y obtener el loteId asociado
            var lpp = await _context.LotePosturaProduccion.AsNoTracking()
                .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.LotePosturaProduccionId == lotePosturaProduccionId.Value)
                .Select(l => new { l.LoteId })
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            if (lpp == null)
                throw new ArgumentException("El lote postura producción especificado no existe o no pertenece a la empresa.");

            produccionLoteId = lpp.LoteId ?? 0;
            q = q.Where(x => x.LotePosturaProduccionId == lotePosturaProduccionId.Value);
        }
        else
        {
            var lid = loteId!.Value;
            Lote? loteProd = await _context.Lotes.AsNoTracking()
                .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.Fase == "Produccion" && l.LotePadreId == lid)
                .OrderBy(l => l.LoteId)
                .FirstOrDefaultAsync();
            if (loteProd == null)
                loteProd = await _context.Lotes.AsNoTracking()
                    .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.Fase == "Produccion" && l.LoteId == lid)
                    .FirstOrDefaultAsync();
            produccionLoteId = loteProd?.LoteId ?? lid;
            q = q.Where(x => x.LoteId == produccionLoteId);
        }

        if (desde.HasValue) q = q.Where(x => x.Fecha >= desde.Value);
        if (hasta.HasValue)
        {
            var h = hasta.Value.Date.AddDays(1);
            q = q.Where(x => x.Fecha < h);
        }

        var total = await q.LongCountAsync().ConfigureAwait(false);

        var pageSafe = Math.Max(1, page);
        // size <= 0 => sin paginación (traer todo)
        var sizeSafe = size <= 0 ? 0 : Math.Clamp(size, 1, 100_000);

        var ordered = q.OrderByDescending(x => x.Fecha);
        var entities = sizeSafe == 0
            ? await ordered.ToListAsync().ConfigureAwait(false)
            : await ordered
                .Skip((pageSafe - 1) * sizeSafe)
                .Take(sizeSafe)
                .ToListAsync()
                .ConfigureAwait(false);

        var items = entities.Select(e => MapToSeguimientoItemDto(e, produccionLoteId)).ToList();
        return new ListaSeguimientoResponse(items, (int)total);
    }

    public async Task<InformacionLoteResponse> ObtenerInformacionLoteAsync(int lotePosturaProduccionId)
    {
        var companyId = _currentUser.CompanyId;

        var loteEntity = await _context.LotePosturaProduccion
            .FirstOrDefaultAsync(l => l.CompanyId == companyId && l.DeletedAt == null && l.LotePosturaProduccionId == lotePosturaProduccionId)
            .ConfigureAwait(false);

        if (loteEntity == null || (loteEntity.LotePosturaProduccionId ?? 0) <= 0)
            throw new ArgumentException("El lote postura producción especificado no existe o no pertenece a la empresa.");

        var agg = await _context.SeguimientoProduccion
            .AsNoTracking()
            .Where(s => s.LotePosturaProduccionId == lotePosturaProduccionId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Registros = g.Count(),
                MortalidadH = g.Sum(x => (int?)x.MortalidadH) ?? 0,
                MortalidadM = g.Sum(x => (int?)x.MortalidadM) ?? 0,
                SelH = g.Sum(x => (int?)x.SelH) ?? 0,
                SelM = g.Sum(x => (int?)x.SelM) ?? 0,
                ConsH = g.Sum(x => (decimal?)x.ConsKgH) ?? 0m,
                ConsM = g.Sum(x => (decimal?)x.ConsKgM) ?? 0m
            })
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        var avesInicialesH = loteEntity.AvesHInicial ?? loteEntity.HembrasInicialesProd ?? 0;
        var avesInicialesM = loteEntity.AvesMInicial ?? loteEntity.MachosInicialesProd ?? 0;

        var mortalidadSeleccionH = (agg?.MortalidadH ?? 0) + (agg?.SelH ?? 0);
        var mortalidadSeleccionM = (agg?.MortalidadM ?? 0) + (agg?.SelM ?? 0);

        // Sumar salidas por movimientos completados (ventas, traslados desde este lote)
        var movSalidas = loteEntity.LoteId.HasValue
            ? await _context.MovimientoAves
                .AsNoTracking()
                .Where(m => m.LoteOrigenId == loteEntity.LoteId.Value
                         && m.Estado == "Completado"
                         && m.CompanyId == companyId
                         && m.DeletedAt == null)
                .GroupBy(_ => 1)
                .Select(g => new { H = g.Sum(x => (int?)x.CantidadHembras) ?? 0, M = g.Sum(x => (int?)x.CantidadMachos) ?? 0 })
                .FirstOrDefaultAsync()
                .ConfigureAwait(false)
            : null;

        // Sumar entradas por traslados hacia este lote
        var movEntradas = loteEntity.LoteId.HasValue
            ? await _context.MovimientoAves
                .AsNoTracking()
                .Where(m => m.LoteDestinoId == loteEntity.LoteId.Value
                         && m.TipoMovimiento == "Traslado"
                         && m.Estado == "Completado"
                         && m.CompanyId == companyId
                         && m.DeletedAt == null)
                .GroupBy(_ => 1)
                .Select(g => new { H = g.Sum(x => (int?)x.CantidadHembras) ?? 0, M = g.Sum(x => (int?)x.CantidadMachos) ?? 0 })
                .FirstOrDefaultAsync()
                .ConfigureAwait(false)
            : null;

        var totalSalidasH = movSalidas?.H ?? 0;
        var totalSalidasM = movSalidas?.M ?? 0;
        var totalEntradasH = movEntradas?.H ?? 0;
        var totalEntradasM = movEntradas?.M ?? 0;

        // Calcular aves actuales incluyendo mortalidad y movimientos
        var avesActualesH = Math.Max(0, avesInicialesH - mortalidadSeleccionH - totalSalidasH + totalEntradasH);
        var avesActualesM = Math.Max(0, avesInicialesM - mortalidadSeleccionM - totalSalidasM + totalEntradasM);

        // Persistir si el valor almacenado difiere del calculado
        if (loteEntity.AvesHActual != avesActualesH || loteEntity.AvesMActual != avesActualesM)
        {
            loteEntity.AvesHActual = avesActualesH;
            loteEntity.AvesMActual = avesActualesM;
            loteEntity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        var edadSemanasProduccion = 0;
        if (loteEntity.FechaEncaset.HasValue)
        {
            var weeksDesdeEncaset = Math.Max(0, ((DateTime.Today.Date - loteEntity.FechaEncaset.Value.Date).Days / 7) + 1);
            // En el módulo se usa "semana de vida" (inicia en 26 para producción).
            // Para mantener consistencia con indicadores y tabla, devolvemos la semana de vida (>= 26).
            edadSemanasProduccion = Math.Max(26, weeksDesdeEncaset);
        }

        var dto = new InformacionLoteDto(
            LotePosturaProduccionId: loteEntity.LotePosturaProduccionId ?? 0,
            LoteNombre: loteEntity.LoteNombre ?? "",
            Estado: string.IsNullOrWhiteSpace(loteEntity.EstadoCierre) ? "Abierta" : loteEntity.EstadoCierre!,
            FechaEncaset: loteEntity.FechaEncaset,
            FechaInicioProduccion: loteEntity.FechaInicioProduccion,
            AvesInicialesH: avesInicialesH,
            AvesInicialesM: avesInicialesM,
            AvesActualesH: avesActualesH,
            AvesActualesM: avesActualesM,
            EdadSemanasProduccion: edadSemanasProduccion,
            Registros: agg?.Registros ?? 0,
            MortalidadSeleccionH: mortalidadSeleccionH,
            MortalidadSeleccionM: mortalidadSeleccionM,
            ConsumoAlimentoKgH: agg?.ConsH ?? 0m,
            ConsumoAlimentoKgM: agg?.ConsM ?? 0m
        );

        return new InformacionLoteResponse(dto);
    }

    private static object? MetadataFromJsonDocument(System.Text.Json.JsonDocument? doc)
    {
        if (doc == null) return null;
        try
        {
            return JsonSerializer.Deserialize<object>(doc.RootElement.GetRawText());
        }
        catch
        {
            return null;
        }
    }

    private static SeguimientoItemDto MapToSeguimientoItemDto(SeguimientoProduccion e, int produccionLoteId)
    {
        var consKgH = e.ConsKgH;
        var consKgM = e.ConsKgM;
        return new SeguimientoItemDto(
            e.Id,
            produccionLoteId,
            e.Fecha,
            e.MortalidadH,
            e.MortalidadM,
            e.SelH,
            e.SelM,
            consKgH,
            consKgM,
            consKgH + consKgM,
            e.HuevoTot,
            e.HuevoInc,
            e.TipoAlimento ?? "",
            e.PesoHuevo,
            e.Etapa,
            e.Observaciones,
            CreatedAt: e.Fecha,
            UpdatedAt: null,
            e.HuevoLimpio,
            e.HuevoTratado,
            e.HuevoSucio,
            e.HuevoDeforme,
            e.HuevoBlanco,
            e.HuevoDobleYema,
            e.HuevoPiso,
            e.HuevoPequeno,
            e.HuevoRoto,
            e.HuevoDesecho,
            e.HuevoOtro,
            e.PesoH,
            e.PesoM,
            e.Uniformidad,
            e.CoeficienteVariacion,
            e.ObservacionesPesaje,
            e.ConsumoAguaDiario,
            e.ConsumoAguaPh,
            e.ConsumoAguaOrp,
            e.ConsumoAguaTemperatura,
            e.LotePosturaProduccionId,
            Metadata: MetadataFromJsonDocument(e.Metadata)
        );
    }

    public async Task<SeguimientoItemDto?> ObtenerSeguimientoPorIdAsync(int seguimientoId)
    {
        var e = await _context.SeguimientoProduccion.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == seguimientoId)
            .ConfigureAwait(false);
        if (e == null)
            return null;

        // Validar compañía por lote
        var isMine = await _context.Lotes.AsNoTracking()
            .AnyAsync(l => l.LoteId == e.LoteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
            .ConfigureAwait(false);
        if (!isMine) return null;

        return MapToSeguimientoItemDto(e, e.LoteId);
    }

    /// <summary>
    /// Elimina un seguimiento diario de producción (tabla unificada seguimiento_diario).
    /// El inventario nuevo (inventario-gestion) no se usa en este módulo; solo en Seguimiento diario pollo de engorde.
    /// </summary>
    public async Task<bool> EliminarSeguimientoAsync(int seguimientoId)
    {
        var e = await _context.SeguimientoProduccion
            .FirstOrDefaultAsync(x => x.Id == seguimientoId)
            .ConfigureAwait(false);
        if (e == null) return false;

        var isMine = await _context.Lotes.AsNoTracking()
            .AnyAsync(l => l.LoteId == e.LoteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
            .ConfigureAwait(false);
        if (!isMine) return false;

        var lppId = e.LotePosturaProduccionId;
        _context.SeguimientoProduccion.Remove(e);
        await _context.SaveChangesAsync().ConfigureAwait(false);
        if (lppId.HasValue)
            await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(lppId.Value).ConfigureAwait(false);
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
        
        return JsonDocument.Parse(JsonSerializer.Serialize(metadata));
    }

    private static (List<ItemSeguimientoDto> alimentos, List<ItemSeguimientoDto> otrosItems) SepararAlimentosYOtrosItems(List<ItemSeguimientoDto>? items)
    {
        if (items == null || items.Count == 0)
            return (new List<ItemSeguimientoDto>(), new List<ItemSeguimientoDto>());
        var alimentos = new List<ItemSeguimientoDto>();
        var otros = new List<ItemSeguimientoDto>();
        foreach (var item in items)
        {
            if (string.Equals(item.TipoItem?.Trim(), "alimento", StringComparison.OrdinalIgnoreCase))
                alimentos.Add(item);
            else
                otros.Add(item);
        }
        return (alimentos, otros);
    }

    private static double CalcularConsumoTotalAlimentos(List<ItemSeguimientoDto>? items)
    {
        if (items == null || items.Count == 0) return 0;
        double total = 0;
        foreach (var item in items)
        {
            if (!string.Equals(item.TipoItem?.Trim(), "alimento", StringComparison.OrdinalIgnoreCase)) continue;
            var unidad = item.Unidad?.ToLower().Trim() ?? "kg";
            var cantidadKg = item.Cantidad;
            if (unidad == "g" || unidad == "gramos" || unidad == "gramo")
                cantidadKg = item.Cantidad / 1000.0;
            total += cantidadKg;
        }
        return total;
    }

    private static JsonDocument? BuildItemsAdicionales(List<ItemSeguimientoDto>? itemsHembras, List<ItemSeguimientoDto>? itemsMachos)
    {
        var dict = new Dictionary<string, object?>();
        if (itemsHembras != null && itemsHembras.Count > 0)
            dict["itemsHembras"] = itemsHembras.Select(i => new { tipoItem = i.TipoItem, catalogItemId = i.CatalogItemId, cantidad = i.Cantidad, unidad = i.Unidad }).ToList();
        if (itemsMachos != null && itemsMachos.Count > 0)
            dict["itemsMachos"] = itemsMachos.Select(i => new { tipoItem = i.TipoItem, catalogItemId = i.CatalogItemId, cantidad = i.Cantidad, unidad = i.Unidad }).ToList();
        if (dict.Count == 0) return null;
        return JsonDocument.Parse(JsonSerializer.Serialize(dict));
    }

    private static string ConstruirTipoAlimentoString(List<ItemSeguimientoDto>? itemsHembras, List<ItemSeguimientoDto>? itemsMachos)
    {
        var parts = new List<string>();
        if (itemsHembras != null)
            foreach (var i in itemsHembras)
                if (string.Equals(i.TipoItem?.Trim(), "alimento", StringComparison.OrdinalIgnoreCase))
                    parts.Add($"H:{i.CatalogItemId}");
        if (itemsMachos != null)
            foreach (var i in itemsMachos)
                if (string.Equals(i.TipoItem?.Trim(), "alimento", StringComparison.OrdinalIgnoreCase))
                    parts.Add($"M:{i.CatalogItemId}");
        return parts.Count > 0 ? string.Join(" / ", parts) : string.Empty;
    }

    private static JsonDocument? BuildMetadataFromItems(
        List<ItemSeguimientoDto>? itemsHembras,
        List<ItemSeguimientoDto>? itemsMachos,
        double? consumoH, string? unidadH, double? consumoM, string? unidadM,
        string? tipoItemHembras, string? tipoItemMachos,
        int? tipoAlimentoHembras, int? tipoAlimentoMachos)
    {
        var metadata = new Dictionary<string, object?>();
        if (itemsHembras != null && itemsHembras.Count > 0)
            metadata["itemsHembras"] = itemsHembras.Select(i => new { tipoItem = i.TipoItem, catalogItemId = i.CatalogItemId, itemInventarioEcuadorId = i.ItemInventarioEcuadorId, cantidad = i.Cantidad, unidad = i.Unidad }).ToList();
        if (itemsMachos != null && itemsMachos.Count > 0)
            metadata["itemsMachos"] = itemsMachos.Select(i => new { tipoItem = i.TipoItem, catalogItemId = i.CatalogItemId, itemInventarioEcuadorId = i.ItemInventarioEcuadorId, cantidad = i.Cantidad, unidad = i.Unidad }).ToList();
        if ((itemsHembras == null || itemsHembras.Count == 0) && consumoH.HasValue) { metadata["consumoOriginalHembras"] = consumoH.Value; metadata["unidadConsumoOriginalHembras"] = unidadH ?? "kg"; }
        if ((itemsMachos == null || itemsMachos.Count == 0) && consumoM.HasValue) { metadata["consumoOriginalMachos"] = consumoM.Value; metadata["unidadConsumoOriginalMachos"] = unidadM ?? "kg"; }
        if (!string.IsNullOrWhiteSpace(tipoItemHembras)) metadata["tipoItemHembras"] = tipoItemHembras;
        if (!string.IsNullOrWhiteSpace(tipoItemMachos)) metadata["tipoItemMachos"] = tipoItemMachos;
        if (tipoAlimentoHembras.HasValue) metadata["tipoAlimentoHembras"] = tipoAlimentoHembras.Value;
        if (tipoAlimentoMachos.HasValue) metadata["tipoAlimentoMachos"] = tipoAlimentoMachos.Value;
        if (metadata.Count == 0) return null;
        return JsonDocument.Parse(JsonSerializer.Serialize(metadata));
    }

    private static decimal ToKg(double cantidad, string unidad)
    {
        var u = (unidad ?? "kg").Trim().ToLowerInvariant();
        if (u == "g" || u == "gramos" || u == "gramo") return (decimal)(cantidad / 1000.0);
        return (decimal)cantidad;
    }
}
