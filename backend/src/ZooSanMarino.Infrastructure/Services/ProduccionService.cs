// src/ZooSanMarino.Infrastructure/Services/ProduccionService.cs
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

public class ProduccionService : IProduccionService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILoteService _loteService;
    private readonly IEspejoHuevoProduccionSyncService _espejoHuevoSync;
    private readonly ILocationScopeResolver _scopeResolver;
    private readonly IFarmInventoryConsumoService? _farmInventoryConsumo;      // Fase 2: modelo A (Colombia) — sin uso tras Fase 3 paso 2
    private readonly IColombiaInventarioConsumoService? _colombiaConsumoB;     // Fase 3 paso 2: modelo B nivel granja (Colombia)

    /// <summary>
    /// Fase 3 (paso 2): producción postura Colombia descuenta inventario en el MODELO B unificado a
    /// NIVEL GRANJA (antes Fase 2 usaba modelo A). Ecuador/Panamá no operan producción postura por
    /// esta ruta, por eso no hay flujo modelo B con galpón aquí. El descuento resuelve GranjaId del
    /// lote y descuenta desde los DTOs ItemsHembras/ItemsMachos (no re-parsea JSON), con validación
    /// previa de stock y transacción única (todo-o-nada) idéntica a levante.
    /// </summary>
    public ProduccionService(
        ZooSanMarinoContext context,
        ICurrentUser currentUser,
        ILoteService loteService,
        IEspejoHuevoProduccionSyncService espejoHuevoSync,
        ILocationScopeResolver scopeResolver,
        IFarmInventoryConsumoService? farmInventoryConsumo = null,
        IColombiaInventarioConsumoService? colombiaConsumoB = null)
    {
        _context = context;
        _currentUser = currentUser;
        _loteService = loteService;
        _espejoHuevoSync = espejoHuevoSync;
        _scopeResolver = scopeResolver;
        _farmInventoryConsumo = farmInventoryConsumo;
        _colombiaConsumoB = colombiaConsumoB;
    }

    /// <summary>
    /// Resuelve (GranjaId, ModeloInventarioConsumo) del lote de producción para gatear el descuento.
    /// País: lote.PaisId si está poblado; si no, granja→departamento→pais (misma cadena que el inventario).
    /// </summary>
    private async Task<(int? GranjaId, ModeloInventarioConsumo Modelo)> ResolverGranjaYModeloAsync(int loteId)
    {
        var lote = await _context.Lotes.AsNoTracking()
            .Where(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
            .Select(l => new { l.GranjaId, l.PaisId })
            .FirstOrDefaultAsync();
        if (lote == null) return (null, ModeloInventarioConsumo.Ninguno);

        int? paisId = lote.PaisId;
        if (paisId is not > 0)
            paisId = await _context.Farms.AsNoTracking()
                .Where(f => f.Id == lote.GranjaId)
                .Join(_context.Departamentos.AsNoTracking(),
                    f => f.DepartamentoId, d => d.DepartamentoId, (f, d) => (int?)d.PaisId)
                .FirstOrDefaultAsync();

        return (lote.GranjaId, InventarioConsumoGate.ResolverModelo(paisId));
    }

    /// <summary>
    /// Acumula por CLAVE TIPADA de ítem (conserva el origen del id: item_inventario_ecuador si viene
    /// camino-2, si no catalogItemId) los kg de los ítems del request (ItemsHembras + ItemsMachos),
    /// usando la MISMA prioridad de id y conversión g→kg que ParseMetadataItemsToKgPorOrigen.
    /// TODOS los tipos (alimento + medicamento + insumo), sin re-parsear el JSON del metadata. Id &lt;= 0 se ignora.
    /// </summary>
    private static Dictionary<ItemConsumoKey, decimal> AcumularItemsRequestPorOrigen(
        List<ItemSeguimientoDto>? itemsHembras, List<ItemSeguimientoDto>? itemsMachos)
    {
        var byItem = new Dictionary<ItemConsumoKey, decimal>();
        void Acumular(List<ItemSeguimientoDto>? items)
        {
            if (items == null) return;
            foreach (var i in items)
            {
                var id = i.ItemInventarioEcuadorId.GetValueOrDefault();
                var esItemInventario = id > 0;
                if (id <= 0) id = i.CatalogItemId;
                if (id <= 0) continue;
                var key = new ItemConsumoKey(id, esItemInventario);
                byItem[key] = byItem.GetValueOrDefault(key) + ToKg(i.Cantidad, i.Unidad);
            }
        }
        Acumular(itemsHembras);
        Acumular(itemsMachos);
        return byItem;
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

        // Fila del día creada por el arrastre de huevos del levante, si existe: habilita el MERGE
        // (sumar sobre ella) en vez del 400 por duplicado. Null ⇒ alta normal.
        SeguimientoProduccion? filaArrastre = null;

        if (lotePosturaProduccionId.HasValue)
        {
            var lpp = await _context.LotePosturaProduccion
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LotePosturaProduccionId == lotePosturaProduccionId.Value
                    && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null);
            if (lpp == null)
                throw new ArgumentException("El lote postura producción especificado no existe o no pertenece a la empresa.");
            loteId = await ResolverYSanarLoteIdAsync(lpp);

            // La unicidad real en BD es (lote_id, fecha): si otro LPP comparte el mismo Lote base,
            // sin este OR el INSERT reventaría con violación de índice único (500) en vez de 400.
            // Se trae la FILA (no AnyAsync) porque si es la del arrastre de huevos del levante hay
            // que SUMARLE el seguimiento del día en vez de rechazarlo (ver ResolverFilaDuplicada).
            // Rango de día UTC en vez de `.Fecha.Date == ...`: EF traduce eso a
            // `date_trunc('day', fecha_registro) = @p`, y date_trunc sobre timestamptz trunca en la
            // zona de la SESIÓN de la BD ⇒ con una sesión no-UTC nunca casaba y el duplicado pasaba
            // sin detectarse. El rango es correcto en cualquier zona y además sargable.
            var (diaDesde, diaHasta) = FechasPuras.RangoDiaUtc(request.FechaRegistro);
            var existenteLpp = await _context.SeguimientoProduccion
                .FirstOrDefaultAsync(s => (s.LotePosturaProduccionId == lotePosturaProduccionId || s.LoteId == loteId)
                    && s.Fecha >= diaDesde && s.Fecha < diaHasta);
            filaArrastre = ResolverFilaDuplicada(existenteLpp, "Ya existe un seguimiento para esta fecha y lote.");
        }
        else
        {
            var loteProd = await _context.Lotes
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == request.ProduccionLoteId && l.Fase == "Produccion" && l.DeletedAt == null);
            if (loteProd == null)
                throw new ArgumentException("El registro de producción (lote en fase Producción) especificado no existe.");
            loteId = loteProd.LoteId ?? request.ProduccionLoteId!.Value;

            var (diaDesde, diaHasta) = FechasPuras.RangoDiaUtc(request.FechaRegistro);
            var existente = await _context.SeguimientoProduccion
                .FirstOrDefaultAsync(s => s.LoteId == loteId && s.Fecha >= diaDesde && s.Fecha < diaHasta);
            filaArrastre = ResolverFilaDuplicada(existente, "Ya existe un seguimiento para esta fecha.");
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

        // ── Clasificación de huevos POR ÍTEMS (Santa Reyes) ───────────────────────────────
        // null o [] en creación = comportamiento actual intacto (11 columnas fijas del DTO).
        // Con ítems: se valida, se exige el flag de empresa, se guarda el desglose en el
        // metadata (conservando lo que ya escribió BuildMetadata*) y los totales salen de la suma.
        List<HuevoItemSeguimientoDto>? huevoItems = null;
        if (request.HuevoItems is { Count: > 0 })
        {
            huevoItems = await ValidarHuevoItemsAsync(loteId, request.HuevoItems).ConfigureAwait(false);
            metadata = HuevoItemsCalculos.EscribirEnMetadata(metadata, huevoItems);
        }

        // -- MERGE sobre la fila del arrastre de huevos del levante ------------------------
        // El usuario registra produccion el mismo dia en que se liquido el levante: sus huevos se
        // SUMAN a los que ya venian de levante y el resto de los campos los define su registro.
        // La marca se conserva para que el arrastre siga siendo idempotente.
        if (filaArrastre is not null)
        {
            // Se conserva la marca (para que el arrastre siga siendo idempotente) y se CIERRA la
            // ventana de merge: a partir de acá el día vuelve a admitir un solo registro.
            metadata = HuevosLevanteCalculos.CopiarMarcaArrastre(metadata, filaArrastre.Metadata);
            metadata = HuevosLevanteCalculos.MarcarSeguimientoRegistrado(metadata);
            AplicarRequestSobreFilaArrastre(filaArrastre, request, consumoKgH, consumoKgM,
                tipoAlimento, metadata);
        }

        var entity = filaArrastre ?? new SeguimientoProduccion
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
            ConsumoAguaTemperatura = request.ConsumoAguaTemperatura,
            CompanyId = _currentUser.CompanyId,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        // huevo_tot = suma de los ítems; huevo_inc y las 11 columnas fijas quedan en 0.
        if (huevoItems != null) AplicarTotalesHuevoPorItems(entity, huevoItems);

        // ── Colombia (modelo B nivel granja) — BLOQUEO ATÓMICO (Fase 3 paso 2) ────────────
        // Descuento desde los DTOs del request (TODOS los ítems), id-mapping catalogItemId→ítem B.
        // Validación previa de stock B ANTES de persistir; guardado + consumo en UNA tx. Si falta
        // stock/ítem → throw por ítem → rollback → NO se guarda el seguimiento.
        var (granjaId, modelo) = await ResolverGranjaYModeloAsync(loteId);
        if (modelo == ModeloInventarioConsumo.ModeloBNivelGranja && _colombiaConsumoB != null && granjaId is > 0 && useItems)
        {
            var byItem = AcumularItemsRequestPorOrigen(request.ItemsHembras, request.ItemsMachos);
            var positivos = byItem.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);

            await _colombiaConsumoB.ValidarStockConsumoAsync(granjaId.Value, positivos); // lanza si falta (antes de persistir)

            await using var tx = await _context.Database.BeginTransactionAsync();
            if (filaArrastre is null) _context.SeguimientoProduccion.Add(entity);
            await _context.SaveChangesAsync();
            if (positivos.Count > 0)
            {
                var refStr = $"Seguimiento producción #{entity.Id} {request.FechaRegistro:yyyy-MM-dd}";
                await _colombiaConsumoB.AplicarConsumoAsync(granjaId.Value, positivos, refStr);
                await _context.SaveChangesAsync();
            }
            await tx.CommitAsync();
            if (lotePosturaProduccionId.HasValue)
                await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(lotePosturaProduccionId.Value).ConfigureAwait(false);
            return entity.Id;
        }

        if (filaArrastre is null) _context.SeguimientoProduccion.Add(entity);
        await _context.SaveChangesAsync();
        if (lotePosturaProduccionId.HasValue)
            await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(lotePosturaProduccionId.Value).ConfigureAwait(false);
        return entity.Id;
    }

    /// <summary>
    /// Politica de duplicado por dia. Devuelve la fila SOLO si es la que creo el arrastre de huevos
    /// del levante Y todavia no se registro el seguimiento de ese dia, habilitando el merge
    /// acumulativo UNA sola vez (la regla "un registro por dia" se conserva).
    /// En cualquier otro caso lanza con el mensaje historico, es decir el 400 de siempre para todos
    /// los casos que ya existian (filas manuales, de traslado de aves, etc.).
    /// </summary>
    private static SeguimientoProduccion? ResolverFilaDuplicada(SeguimientoProduccion? existente, string mensaje)
    {
        if (existente is null) return null;
        if (HuevosLevanteCalculos.PermiteMergeSeguimiento(existente.Metadata)) return existente;
        throw new InvalidOperationException(mensaje);
    }

    /// <summary>
    /// Vuelca el request sobre la fila del arrastre: los huevos se SUMAN categoria por categoria
    /// (recalculando <c>huevo_tot</c>/<c>huevo_inc</c> desde el resultado) y el resto de los campos
    /// se reemplazan por lo que registro el usuario. No toca <c>traslado_*</c>, ni
    /// <c>lote_id</c>/<c>fecha_registro</c>/auditoria de creacion.
    /// </summary>
    private void AplicarRequestSobreFilaArrastre(
        SeguimientoProduccion fila,
        CrearSeguimientoRequest request,
        decimal consumoKgH,
        decimal consumoKgM,
        string tipoAlimento,
        JsonDocument? metadata)
    {
        var sumado = HuevosLevanteCalculos.Sumar(
            new HuevosClasificacion(
                Limpio: fila.HuevoLimpio,
                Tratado: fila.HuevoTratado,
                Sucio: fila.HuevoSucio,
                Deforme: fila.HuevoDeforme,
                Blanco: fila.HuevoBlanco,
                DobleYema: fila.HuevoDobleYema,
                Piso: fila.HuevoPiso,
                Pequeno: fila.HuevoPequeno,
                Roto: fila.HuevoRoto,
                Desecho: fila.HuevoDesecho,
                Otro: fila.HuevoOtro),
            new HuevosClasificacion(
                Limpio: request.HuevoLimpio,
                Tratado: request.HuevoTratado,
                Sucio: request.HuevoSucio,
                Deforme: request.HuevoDeforme,
                Blanco: request.HuevoBlanco,
                DobleYema: request.HuevoDobleYema,
                Piso: request.HuevoPiso,
                Pequeno: request.HuevoPequeno,
                Roto: request.HuevoRoto,
                Desecho: request.HuevoDesecho,
                Otro: request.HuevoOtro));

        fila.HuevoLimpio = sumado.Limpio;
        fila.HuevoTratado = sumado.Tratado;
        fila.HuevoSucio = sumado.Sucio;
        fila.HuevoDeforme = sumado.Deforme;
        fila.HuevoBlanco = sumado.Blanco;
        fila.HuevoDobleYema = sumado.DobleYema;
        fila.HuevoPiso = sumado.Piso;
        fila.HuevoPequeno = sumado.Pequeno;
        fila.HuevoRoto = sumado.Roto;
        fila.HuevoDesecho = sumado.Desecho;
        fila.HuevoOtro = sumado.Otro;
        // Derivados desde las 11 categorias ya sumadas (no se suman los totales del request aparte,
        // para que no puedan quedar descuadrados). Con clasificacion por items, el
        // AplicarTotalesHuevoPorItems posterior manda.
        fila.HuevoInc = sumado.Incubables;
        fila.HuevoTot = sumado.Totales;

        fila.MortalidadH = request.MortalidadH;
        fila.MortalidadM = request.MortalidadM;
        fila.SelH = request.SelH;
        fila.SelM = request.SelM;
        fila.ErrorSexajeHembras = request.ErrorSexajeHembras ?? 0;
        fila.ErrorSexajeMachos = request.ErrorSexajeMachos ?? 0;
        fila.ConsKgH = consumoKgH;
        fila.ConsKgM = consumoKgM;
        fila.TipoAlimento = tipoAlimento;
        fila.Etapa = request.Etapa;
        if (request.PesoHuevo > 0) fila.PesoHuevo = request.PesoHuevo;
        fila.PesoH = request.PesoH ?? fila.PesoH;
        fila.PesoM = request.PesoM ?? fila.PesoM;
        fila.Uniformidad = request.Uniformidad ?? fila.Uniformidad;
        fila.CoeficienteVariacion = request.CoeficienteVariacion ?? fila.CoeficienteVariacion;
        fila.ObservacionesPesaje = request.ObservacionesPesaje ?? fila.ObservacionesPesaje;
        fila.ConsumoAguaDiario = request.ConsumoAguaDiario ?? fila.ConsumoAguaDiario;
        fila.ConsumoAguaPh = request.ConsumoAguaPh ?? fila.ConsumoAguaPh;
        fila.ConsumoAguaOrp = request.ConsumoAguaOrp ?? fila.ConsumoAguaOrp;
        fila.ConsumoAguaTemperatura = request.ConsumoAguaTemperatura ?? fila.ConsumoAguaTemperatura;
        fila.Observaciones = string.IsNullOrWhiteSpace(request.Observaciones)
            ? fila.Observaciones
            : (string.IsNullOrWhiteSpace(fila.Observaciones)
                ? request.Observaciones
                : $"{fila.Observaciones} | {request.Observaciones}");
        fila.Metadata = metadata;
        fila.UpdatedByUserId = _currentUser.UserId;
        fila.UpdatedAt = DateTime.UtcNow;
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
            loteId = await ResolverYSanarLoteIdAsync(lpp);
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

        // Fase 2 (S4) — capturar el consumo ANTERIOR (desde el metadata guardado) ANTES de pisarlo,
        // para calcular el diff old/new en el descuento Colombia. Parseo TIPADO (conserva el
        // origen del id, camino 1/2) — solo la rama Colombia consume este diccionario.
        var oldByItemId = entity.Metadata != null
            ? MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(entity.Metadata.RootElement)
            : new Dictionary<ItemConsumoKey, decimal>();

        // ── Clasificación de huevos POR ÍTEMS (Santa Reyes) — edición ────────────────────
        //   null  = "no tocar": se conserva el desglose ya persistido (y sus totales), NO se pisa
        //           con los campos sueltos del DTO;
        //   []    = "quitar la clasificación por ítems": se elimina la clave del metadata y los
        //           totales vuelven a salir de los campos sueltos, como hoy;
        //   [..]  = reemplaza el desglose (se revalida) y recalcula huevo_tot / huevo_inc / 11 columnas.
        var huevoItemsPersistidos = entity.Metadata != null
            ? HuevoItemsCalculos.LeerDeMetadata(entity.Metadata.RootElement)
            : new List<HuevoItemSeguimientoDto>();

        List<HuevoItemSeguimientoDto>? huevoItems = null;
        if (request.HuevoItems is null)
        {
            if (huevoItemsPersistidos.Count > 0) huevoItems = huevoItemsPersistidos;
        }
        else if (request.HuevoItems.Count > 0)
        {
            huevoItems = await ValidarHuevoItemsAsync(loteId, request.HuevoItems).ConfigureAwait(false);
        }

        if (huevoItems != null)
            metadata = HuevoItemsCalculos.EscribirEnMetadata(metadata, huevoItems);

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
        entity.UpdatedByUserId = _currentUser.UserId;
        entity.UpdatedAt = DateTime.UtcNow;

        // huevo_tot = suma de los ítems; huevo_inc y las 11 columnas fijas quedan en 0.
        if (huevoItems != null) AplicarTotalesHuevoPorItems(entity, huevoItems);

        // ── Colombia (modelo B nivel granja) — BLOQUEO ATÓMICO en edición (Fase 3 paso 2) ──
        // diff old/new por catalogItemId (id-mapping A→B): diff>0 = consumo adicional; diff<0 = devolución.
        // Validación previa del stock B de los diff POSITIVOS ANTES de persistir; save + diff en UNA tx.
        var (granjaId, modelo) = await ResolverGranjaYModeloAsync(loteId);
        if (modelo == ModeloInventarioConsumo.ModeloBNivelGranja && _colombiaConsumoB != null && granjaId is > 0)
        {
            var newByItemId = AcumularItemsRequestPorOrigen(request.ItemsHembras, request.ItemsMachos);
            var incrementos = new Dictionary<ItemConsumoKey, decimal>();
            var allKeys = new HashSet<ItemConsumoKey>(oldByItemId.Keys);
            foreach (var k in newByItemId.Keys) allKeys.Add(k);
            foreach (var key in allKeys)
            {
                var diff = newByItemId.GetValueOrDefault(key) - oldByItemId.GetValueOrDefault(key);
                if (diff > 0) incrementos[key] = diff;
            }
            await _colombiaConsumoB.ValidarStockConsumoAsync(granjaId.Value, incrementos); // lanza si falta (antes de persistir)

            await using var tx = await _context.Database.BeginTransactionAsync();
            var refStr = $"Seguimiento producción #{entity.Id} {request.FechaRegistro:yyyy-MM-dd}";
            await _colombiaConsumoB.AplicarDiffAsync(granjaId.Value, oldByItemId, newByItemId, refStr);
            await _context.SaveChangesAsync().ConfigureAwait(false);
            await tx.CommitAsync();
            if (lotePosturaProduccionId.HasValue)
                await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(lotePosturaProduccionId.Value).ConfigureAwait(false);
            return;
        }

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
                .Select(l => new { l.LoteId, l.GranjaId, l.NucleoId, l.GalponId })
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            if (lpp == null)
                throw new ArgumentException("El lote postura producción especificado no existe o no pertenece a la empresa.");

            // Alcance granular: el LPP se resuelve por su lote (si lo tiene) y, si no, por su
            // ubicación galpón/núcleo — mismo cierre que LotePosturaProduccionService (fail-closed).
            var scopeLpp = await _scopeResolver.GetScopeAsync(lpp.GranjaId);
            var permitido = scopeLpp.IsGlobal
                || (lpp.LoteId.HasValue && scopeLpp.PermiteLote(lpp.LoteId.Value))
                || (!lpp.LoteId.HasValue && !string.IsNullOrEmpty(lpp.GalponId) && scopeLpp.PermiteGalpon(lpp.GalponId))
                || (!lpp.LoteId.HasValue && string.IsNullOrEmpty(lpp.GalponId) && !string.IsNullOrEmpty(lpp.NucleoId) && scopeLpp.PermiteNucleo(lpp.NucleoId));
            if (!permitido)
                return new ListaSeguimientoResponse(new List<SeguimientoItemDto>(), 0);

            produccionLoteId = lpp.LoteId ?? 0;
            q = q.Where(x => x.LotePosturaProduccionId == lotePosturaProduccionId.Value);
        }
        else
        {
            var lid = loteId!.Value;

            // Alcance granular: acceso directo por loteId respeta el scope (fail-closed)
            if (!await _scopeResolver.PermiteLoteAsync(lid).ConfigureAwait(false))
                return new ListaSeguimientoResponse(new List<SeguimientoItemDto>(), 0);

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
                MinFecha = g.Min(x => (DateTime?)x.Fecha),
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

        // REQ-012a: fecha de inicio de producción EFECTIVA = MIN(fecha) de los seguimientos si es
        // anterior a la almacenada (la fecha se congela con el default "hoy" al cerrar levante, pero
        // pueden existir registros anteriores). Solo se ajusta lo que se MUESTRA; no se persiste.
        var fechaInicioProduccionEfectiva = loteEntity.FechaInicioProduccion;
        if (agg?.MinFecha != null &&
            (fechaInicioProduccionEfectiva == null ||
             agg.MinFecha.Value.Date < fechaInicioProduccionEfectiva.Value.Date))
        {
            fechaInicioProduccionEfectiva = agg.MinFecha;
        }

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

        // Feature 14 — traslados acumulados por fase (Levante + Producción)
        // Se buscan en LPL (fase levante) y en el propio LPP (fase producción).
        int levInH = 0, levInM = 0, levOutH = 0, levOutM = 0;
        if (loteEntity.LotePosturaLevanteId.HasValue)
        {
            var lpl = await _context.LotePosturaLevante.AsNoTracking()
                .Where(l => l.LotePosturaLevanteId == loteEntity.LotePosturaLevanteId.Value && l.DeletedAt == null)
                .Select(l => new
                {
                    l.LevanteTrasladoIngresoHembras,
                    l.LevanteTrasladoIngresoMachos,
                    l.LevanteTrasladoSalidaHembras,
                    l.LevanteTrasladoSalidaMachos
                })
                .FirstOrDefaultAsync();
            levInH  = lpl?.LevanteTrasladoIngresoHembras ?? 0;
            levInM  = lpl?.LevanteTrasladoIngresoMachos  ?? 0;
            levOutH = lpl?.LevanteTrasladoSalidaHembras  ?? 0;
            levOutM = lpl?.LevanteTrasladoSalidaMachos   ?? 0;
        }
        int prodInH  = loteEntity.ProduccionTrasladoIngresoHembras;
        int prodInM  = loteEntity.ProduccionTrasladoIngresoMachos;
        int prodOutH = loteEntity.ProduccionTrasladoSalidaHembras;
        int prodOutM = loteEntity.ProduccionTrasladoSalidaMachos;

        var dto = new InformacionLoteDto(
            LotePosturaProduccionId: loteEntity.LotePosturaProduccionId ?? 0,
            LoteNombre: loteEntity.LoteNombre ?? "",
            Estado: string.IsNullOrWhiteSpace(loteEntity.EstadoCierre) ? "Abierta" : loteEntity.EstadoCierre!,
            FechaEncaset: loteEntity.FechaEncaset,
            FechaInicioProduccion: fechaInicioProduccionEfectiva,
            AvesInicialesH: avesInicialesH,
            AvesInicialesM: avesInicialesM,
            AvesActualesH: avesActualesH,
            AvesActualesM: avesActualesM,
            EdadSemanasProduccion: edadSemanasProduccion,
            Registros: agg?.Registros ?? 0,
            MortalidadSeleccionH: mortalidadSeleccionH,
            MortalidadSeleccionM: mortalidadSeleccionM,
            ConsumoAlimentoKgH: agg?.ConsH ?? 0m,
            ConsumoAlimentoKgM: agg?.ConsM ?? 0m,
            LevanteTrasladoIngresoHembras: levInH,
            LevanteTrasladoIngresoMachos:  levInM,
            LevanteTrasladoSalidaHembras:  levOutH,
            LevanteTrasladoSalidaMachos:   levOutM,
            ProduccionTrasladoIngresoHembras: prodInH,
            ProduccionTrasladoIngresoMachos:  prodInM,
            ProduccionTrasladoSalidaHembras:  prodOutH,
            ProduccionTrasladoSalidaMachos:   prodOutM
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
    /// Elimina un seguimiento diario de producción. Fase 3 (paso 2): para lotes Colombia (modelo B
    /// nivel granja) devuelve el stock consumido (Ingreso total) y el borrado + la devolución van
    /// en UNA transacción (todo-o-nada). Ecuador/Panamá no usan esta ruta de inventario.
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
        var loteId = e.LoteId;

        var (granjaId, modelo) = await ResolverGranjaYModeloAsync(loteId);
        if (modelo == ModeloInventarioConsumo.ModeloBNivelGranja && _colombiaConsumoB != null && granjaId is > 0)
        {
            var byItem = e.Metadata != null
                ? MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(e.Metadata.RootElement)
                : new Dictionary<ItemConsumoKey, decimal>();
            var positivos = byItem.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);

            await using var tx = await _context.Database.BeginTransactionAsync();
            if (positivos.Count > 0)
            {
                var refStr = $"Seguimiento producción #{seguimientoId} (devolución por eliminación)";
                await _colombiaConsumoB.AplicarDevolucionAsync(granjaId.Value, positivos, refStr, "Devolución por eliminación de seguimiento producción");
            }
            _context.SeguimientoProduccion.Remove(e);
            await _context.SaveChangesAsync().ConfigureAwait(false);
            await tx.CommitAsync();
            if (lppId.HasValue)
                await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(lppId.Value).ConfigureAwait(false);
            return true;
        }

        _context.SeguimientoProduccion.Remove(e);
        await _context.SaveChangesAsync().ConfigureAwait(false);
        if (lppId.HasValue)
            await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(lppId.Value).ConfigureAwait(false);
        return true;
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

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Clasificación de huevos POR ÍTEMS del catálogo (Primera/Pnc) — Santa Reyes.
    // Gateada por companies.clasificacion_huevo_por_items de la empresa DUEÑA DE LA GRANJA del
    // lote (misma empresa efectiva que resuelve el inventario, patrón farms.company_id). Cero
    // impacto para el resto de empresas: sin huevoItems en el request no se ejecuta nada de esto.
    // ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Valida el desglose de huevos por ítems del request:
    /// (a) reglas puras (cantidad ≥ 0, id &gt; 0, sin repetidos) — <see cref="HuevoItemsCalculos.Validar"/>;
    /// (b) la empresa de la granja del lote debe tener <c>clasificacion_huevo_por_items = true</c>;
    /// (c) todos los <c>catalogItemId</c> deben existir en <c>catalogo_items</c> de esa empresa con
    ///     <c>item_type = 'huevo'</c> (una sola query, comparación de conjuntos).
    /// Lanza <see cref="InvalidOperationException"/> (el controller la traduce a 400) con el detalle.
    /// </summary>
    private async Task<List<HuevoItemSeguimientoDto>> ValidarHuevoItemsAsync(int loteId, List<HuevoItemSeguimientoDto> huevoItems)
    {
        var error = HuevoItemsCalculos.Validar(huevoItems);
        if (error != null) throw new InvalidOperationException(error);

        var companyId = await ResolverCompanyIdDeGranjaDelLoteAsync(loteId).ConfigureAwait(false);

        var permite = await _context.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => (bool?)c.ClasificacionHuevoPorItems)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (permite != true)
            throw new InvalidOperationException(
                "La empresa de este lote no tiene habilitada la clasificación de huevos por ítems de inventario; use los campos de clasificación estándar.");

        var ids = huevoItems.Select(i => i.CatalogItemId).Distinct().ToArray();
        var existentes = await _context.CatalogItems.AsNoTracking()
            .Where(ci => ci.CompanyId == companyId && ci.ItemType == "huevo" && ids.Contains(ci.Id))
            .Select(ci => ci.Id)
            .ToListAsync()
            .ConfigureAwait(false);

        var faltantes = ids.Except(existentes).ToArray();
        if (faltantes.Length > 0)
            throw new InvalidOperationException(
                $"Los siguientes ítems no existen como ítem de huevo del catálogo de la empresa: {string.Join(", ", faltantes)}.");

        return huevoItems;
    }

    /// <summary>
    /// Empresa efectiva de la clasificación = empresa dueña de la GRANJA del lote (misma regla que
    /// el descuento de inventario, <c>farms.company_id</c>), no la empresa activa del token.
    /// </summary>
    private async Task<int> ResolverCompanyIdDeGranjaDelLoteAsync(int loteId)
    {
        var granjaId = await _context.Lotes.AsNoTracking()
            .Where(l => l.LoteId == loteId && l.DeletedAt == null)
            .Select(l => (int?)l.GranjaId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (granjaId is null or <= 0)
            throw new InvalidOperationException($"No se pudo resolver la granja del lote {loteId} para clasificar los huevos por ítems.");

        var companyId = await _context.Farms.AsNoTracking()
            .Where(f => f.Id == granjaId.Value)
            .Select(f => (int?)f.CompanyId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (companyId is null or <= 0)
            throw new InvalidOperationException($"No se pudo resolver la empresa de la granja {granjaId} para clasificar los huevos por ítems.");

        return companyId.Value;
    }

    /// <summary>
    /// Devuelve el <c>LoteId</c> efectivo del lote de producción y SANA la fila si estaba rota.
    /// Los LPP nacen al cerrar un levante; antes del fix de herencia ese cierre no copiaba
    /// <c>LoteId</c>/<c>LotePadreId</c>, dejando filas sin lote base (guardado imposible:
    /// <c>seguimiento_diario_produccion.lote_id</c> es NOT NULL). Si el LPP no tiene lote válido,
    /// se resuelve desde su levante de origen (SIN filtrar <c>DeletedAt</c>: la referencia de un
    /// levante soft-deleted sigue siendo válida) y se persiste la reparación en la fila LPP
    /// (self-heal, <c>ExecuteUpdate</c>). Si no es resoluble, lanza un error claro.
    /// </summary>
    private async Task<int> ResolverYSanarLoteIdAsync(LotePosturaProduccion lpp)
    {
        // Camino existente: el LPP ya tiene lote base → comportamiento idéntico, sin tocar nada.
        if (lpp.LoteId is > 0) return lpp.LoteId.Value;

        int? levLoteId = null;
        int? levPadre = null;
        if (lpp.LotePosturaLevanteId.HasValue)
        {
            // Fail-closed: solo se hereda de un levante de la MISMA empresa; una referencia
            // cruzada (solo posible por datos corruptos) cae al error claro de abajo.
            var lev = await _context.LotePosturaLevante.AsNoTracking()
                .Where(l => l.LotePosturaLevanteId == lpp.LotePosturaLevanteId.Value
                    && l.CompanyId == lpp.CompanyId)
                .Select(l => new { l.LoteId, l.LotePadreId })
                .FirstOrDefaultAsync();
            levLoteId = lev?.LoteId;
            levPadre = lev?.LotePadreId;
        }

        var resuelto = SeguimientoProduccionLoteIdCalculos.ResolverLoteIdEfectivo(lpp.LoteId, levLoteId);
        if (resuelto is not > 0)
            throw new InvalidOperationException(
                $"El lote de producción '{lpp.LoteNombre}' no tiene lote base asociado y su levante de origen tampoco lo tiene. " +
                $"No es posible registrar el seguimiento; repare el lote (lote_postura_produccion #{lpp.LotePosturaProduccionId}).");

        // Self-heal persistente: repara la fila LPP (lote_id y, solo si estaba null, lote_padre_id)
        // para que indicadores, espejo huevo y reportes también la vean sana desde ahora.
        await _context.LotePosturaProduccion
            .Where(l => l.LotePosturaProduccionId == lpp.LotePosturaProduccionId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LoteId, resuelto)
                .SetProperty(x => x.LotePadreId, x => x.LotePadreId ?? levPadre)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow)
                .SetProperty(x => x.UpdatedByUserId, _currentUser.UserId));

        return resuelto.Value;
    }

    /// <summary>
    /// Totales del día cuando la clasificación es por ítems: <c>huevo_tot</c> = suma de cantidades
    /// (lo que siguen leyendo espejo, trigger, saldos, indicadores y reportes), <c>huevo_inc</c> = 0
    /// (postura comercial, no incuba) y las 11 columnas fijas en 0 (el desglose vive en el metadata).
    /// </summary>
    private static void AplicarTotalesHuevoPorItems(SeguimientoProduccion entity, IReadOnlyCollection<HuevoItemSeguimientoDto> huevoItems)
    {
        entity.HuevoTot = HuevoItemsCalculos.SumarTotal(huevoItems);
        entity.HuevoInc = 0;
        entity.HuevoLimpio = 0;
        entity.HuevoTratado = 0;
        entity.HuevoSucio = 0;
        entity.HuevoDeforme = 0;
        entity.HuevoBlanco = 0;
        entity.HuevoDobleYema = 0;
        entity.HuevoPiso = 0;
        entity.HuevoPequeno = 0;
        entity.HuevoRoto = 0;
        entity.HuevoDesecho = 0;
        entity.HuevoOtro = 0;
    }
}
