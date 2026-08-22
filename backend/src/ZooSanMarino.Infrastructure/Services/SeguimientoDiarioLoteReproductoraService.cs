// Seguimiento diario por lote reproductora aves de engorde. Persiste en seguimiento_diario_lote_reproductora_aves_engorde.
// DTO reutiliza SeguimientoLoteLevanteDto con LoteId = lote_reproductora_ave_engorde_id.
// Inventario: mismo patrón que SeguimientoAvesEngordeService — descuenta al crear, ajusta al editar, restituye al eliminar.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class SeguimientoDiarioLoteReproductoraService : ISeguimientoDiarioLoteReproductoraService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly IInventarioGestionService? _inventarioGestionService;
    /// <summary>Doble validación: separa en vez de descontar cuando la empresa la tiene activa.</summary>
    private readonly IValidacionSeguimientoService? _validacion;

    public SeguimientoDiarioLoteReproductoraService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        IInventarioGestionService? inventarioGestionService = null,
        IValidacionSeguimientoService? validacion = null)
    {
        _ctx = ctx;
        _current = current;
        _inventarioGestionService = inventarioGestionService;
        _validacion = validacion;
    }

    private static SeguimientoLoteLevanteDto MapToDto(SeguimientoDiarioLoteReproductoraAvesEngorde e)
    {
        return new SeguimientoLoteLevanteDto(
            Id: (int)e.Id,
            LoteId: e.LoteReproductoraAveEngordeId,
            LotePosturaLevanteId: null,
            FechaRegistro: e.Fecha,
            MortalidadHembras: e.MortalidadHembras ?? 0,
            MortalidadMachos: e.MortalidadMachos ?? 0,
            SelH: e.SelH ?? 0,
            SelM: e.SelM ?? 0,
            ErrorSexajeHembras: e.ErrorSexajeHembras ?? 0,
            ErrorSexajeMachos: e.ErrorSexajeMachos ?? 0,
            ConsumoKgHembras: (double)(e.ConsumoKgHembras ?? 0),
            TipoAlimento: e.TipoAlimento ?? "",
            Observaciones: e.Observaciones,
            KcalAlH: e.KcalAlH,
            ProtAlH: e.ProtAlH,
            KcalAveH: e.KcalAveH,
            ProtAveH: e.ProtAveH,
            Ciclo: e.Ciclo ?? "Normal",
            ConsumoKgMachos: e.ConsumoKgMachos.HasValue ? (double)e.ConsumoKgMachos.Value : null,
            PesoPromH: e.PesoPromHembras,
            PesoPromM: e.PesoPromMachos,
            UniformidadH: e.UniformidadHembras,
            UniformidadM: e.UniformidadMachos,
            CvH: e.CvHembras,
            CvM: e.CvMachos,
            Metadata: e.Metadata,
            ItemsAdicionales: e.ItemsAdicionales,
            ConsumoAguaDiario: e.ConsumoAguaDiario,
            ConsumoAguaPh: e.ConsumoAguaPh,
            ConsumoAguaOrp: e.ConsumoAguaOrp,
            ConsumoAguaTemperatura: e.ConsumoAguaTemperatura,
            CreatedByUserId: e.CreatedByUserId,
            SaldoAlimentoKg: null,
            QqMixtas: e.QqMixtas,
            QqHembras: e.QqHembras,
            QqMachos: e.QqMachos,
            Confirmado: e.Confirmado,
            ConfirmadoAt: e.ConfirmadoAt,
            ConfirmadoPor: e.ConfirmadoPor,
            // Reproductora no tiene columna propia: su `confirmado` ES la validación.
            Validado: e.Confirmado,
            ValidadoAt: e.ConfirmadoAt,
            ValidadoPor: e.ConfirmadoPor
        );
    }

    // ─── Helpers de inventario ────────────────────────────────────────────────

    /// <summary>
    /// Parsea itemsHembras/Machos/Generales de la metadata → mapa itemId → kg total.
    /// Delega en el cálculo puro central compartido (un solo lugar → un solo test).
    /// Antes había una copia idéntica acá + su propio ToKg.
    /// </summary>
    private static Dictionary<int, decimal> ParseMetadataItemsToKg(JsonElement root)
        => ZooSanMarino.Application.Calculos.MetadataEngordeCalculos.ParseMetadataItemsToKg(root);

    /// <summary>
    /// Obtiene farmId, nucleoId y galponId trazando LoteReproductora → LoteAveEngorde.
    /// </summary>
    private async Task<(int FarmId, string? NucleoId, string? GalponId)?> GetLoteUbicacionAsync(int loteReproductoraId)
    {
        var row = await (
            from lr in _ctx.LoteReproductoraAveEngorde.AsNoTracking()
            join lae in _ctx.LoteAveEngorde.AsNoTracking() on lr.LoteAveEngordeId equals lae.LoteAveEngordeId
            where lr.Id == loteReproductoraId
            select new { lae.GranjaId, lae.NucleoId, lae.GalponId }
        ).FirstOrDefaultAsync();

        if (row is null) return null;
        return (row.GranjaId, row.NucleoId, row.GalponId);
    }

    /// <summary>
    /// País efectivo para gatear el descuento del inventario modelo B (S1). Aquí el origen es la
    /// GRANJA del lote reproductora (no hay lote.PaisId): farm.DepartamentoId → departamentos.PaisId,
    /// la misma cadena que usa el inventario. Devuelve null si no se puede resolver.
    /// </summary>
    private async Task<int?> ResolverPaisIdPorGranjaAsync(int granjaId)
        => await _ctx.Farms.AsNoTracking()
            .Where(f => f.Id == granjaId)
            .Join(_ctx.Departamentos.AsNoTracking(),
                f => f.DepartamentoId, d => d.DepartamentoId, (f, d) => (int?)d.PaisId)
            .FirstOrDefaultAsync();

    // ─── Queries ──────────────────────────────────────────────────────────────

    public async Task<IEnumerable<SeguimientoLoteLevanteDto>> GetByLoteReproductoraAsync(int loteReproductoraId)
    {
        var companyId = _current.CompanyId;
        var exists = await (from l in _ctx.LoteReproductoraAveEngorde.AsNoTracking()
                           join lae in _ctx.LoteAveEngorde.AsNoTracking() on l.LoteAveEngordeId equals lae.LoteAveEngordeId
                           where l.Id == loteReproductoraId && lae.CompanyId == companyId && lae.DeletedAt == null
                           select 1).AnyAsync();
        if (!exists) return Array.Empty<SeguimientoLoteLevanteDto>();

        var list = await _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde
            .AsNoTracking()
            .Where(s => s.LoteReproductoraAveEngordeId == loteReproductoraId)
            .OrderBy(s => s.Fecha)
            .ToListAsync();
        return list.Select(MapToDto);
    }

    public async Task<SeguimientoLoteLevanteDto?> GetByIdAsync(int id)
    {
        var companyId = _current.CompanyId;
        var e = await (from s in _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.AsNoTracking()
                       join l in _ctx.LoteReproductoraAveEngorde.AsNoTracking() on s.LoteReproductoraAveEngordeId equals l.Id
                       join lae in _ctx.LoteAveEngorde.AsNoTracking() on l.LoteAveEngordeId equals lae.LoteAveEngordeId
                       where s.Id == id && lae.CompanyId == companyId && lae.DeletedAt == null
                       select s).SingleOrDefaultAsync();
        return e is null ? null : MapToDto(e);
    }

    public async Task<IEnumerable<SeguimientoLoteLevanteDto>> FilterAsync(int? loteReproductoraId, DateTime? desde, DateTime? hasta)
    {
        var companyId = _current.CompanyId;
        // Rango por DÍA completo en UTC: las fechas se guardan ancladas a mediodía UTC
        // (FechasPuras), así que un "hasta" a medianoche excluiría los registros de ese día.
        var desdeUtc = FechasPuras.AnclarMediodiaUtc(desde)?.AddHours(-12);
        var hastaExcl = FechasPuras.AnclarMediodiaUtc(hasta)?.AddHours(12);
        var q = from s in _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.AsNoTracking()
                join l in _ctx.LoteReproductoraAveEngorde.AsNoTracking() on s.LoteReproductoraAveEngordeId equals l.Id
                join lae in _ctx.LoteAveEngorde.AsNoTracking() on l.LoteAveEngordeId equals lae.LoteAveEngordeId
                where lae.CompanyId == companyId && lae.DeletedAt == null
                   && (!loteReproductoraId.HasValue || s.LoteReproductoraAveEngordeId == loteReproductoraId.Value)
                   && (!desdeUtc.HasValue || s.Fecha >= desdeUtc.Value)
                   && (!hastaExcl.HasValue || s.Fecha < hastaExcl.Value)
                orderby s.Fecha
                select s;
        var list = await q.ToListAsync();
        return list.Select(MapToDto);
    }

    // ─── Create ───────────────────────────────────────────────────────────────

    public async Task<SeguimientoLoteLevanteDto> CreateAsync(SeguimientoLoteLevanteDto dto)
    {
        var companyId = _current.CompanyId;
        var filaCreate = await (from l in _ctx.LoteReproductoraAveEngorde.AsNoTracking()
                             join lae in _ctx.LoteAveEngorde.AsNoTracking() on l.LoteAveEngordeId equals lae.LoteAveEngordeId
                             where l.Id == dto.LoteId && lae.CompanyId == companyId && lae.DeletedAt == null
                             select new { Lote = l, lae.EstadoOperativoLote }).SingleOrDefaultAsync();
        if (filaCreate is null)
            throw new InvalidOperationException($"Lote reproductora aves de engorde '{dto.LoteId}' no existe o no pertenece a la compañía.");

        // Gate B6 — el trigger de cruce (trg_cruce_reproductora_engorde) hace DELETE+INSERT de los
        // días 1-7 del seguimiento del LOTE DE ENGORDE desde la BD, donde ningún gate de C# lo
        // alcanza: con el lote liquidado se corta acá, antes de escribir la reproductora.
        LiquidacionCongeladaGateCalculos.ValidarEscritura(
            filaCreate.EstadoOperativoLote, OperacionLoteEngordeLiquidado.SeguimientoReproductora);
        var loteRep = filaCreate.Lote;

        // Regla: máximo 7 días de seguimiento por lote reproductora
        const int MaxDiasSeguimiento = 7;
        var totalRegistros = await _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde
            .CountAsync(s => s.LoteReproductoraAveEngordeId == dto.LoteId);
        if (totalRegistros >= MaxDiasSeguimiento)
            throw new InvalidOperationException(
                $"Este lote reproductora ya tiene {totalRegistros} días de seguimiento registrados. El máximo permitido es {MaxDiasSeguimiento}.");

        // Regla de fecha: el día del encasetamiento es el DÍA 1 de la semana de recogida (edad 0);
        // se acepta edad [edadMinima, 7] — la tolerancia en edad 7 deja completar lotes que arrancaron
        // al día siguiente del encaset (numeración previa). El cruce consolida edades 0..7.
        // edadMinima sube a 1 si las aves llegaron a las 13:00 o después: ese día ya no consumen.
        var fechaAnclada = FechasPuras.AnclarMediodiaUtc(dto.FechaRegistro);
        if (loteRep.FechaEncasetamiento.HasValue)
        {
            var horaRegla = EncasetamientoCalculos.HoraEfectiva(
                loteRep.HoraEncasetamiento, await PrimerRegistroPorHoraGate.ActivaAsync(_ctx, _current.CompanyId));
            var edadMinima = EncasetamientoCalculos.EdadMinimaConRegistro(horaRegla);
            var edad = ReproductoraEngordeCalculos.EdadSeguimientoDias(loteRep.FechaEncasetamiento.Value, fechaAnclada);
            if (!ReproductoraEngordeCalculos.EsEdadSeguimientoValida(edad, MaxDiasSeguimiento, edadMinima))
                throw new InvalidOperationException(
                    edad < edadMinima
                        ? MensajeFechaMuyTemprana(loteRep.FechaEncasetamiento.Value, horaRegla)
                        : "La fecha del seguimiento supera la primera semana de recogida contada desde el encasetamiento.");
        }

        var ent = new SeguimientoDiarioLoteReproductoraAvesEngorde
        {
            LoteReproductoraAveEngordeId = dto.LoteId,
            Fecha = fechaAnclada,
            MortalidadHembras = dto.MortalidadHembras,
            MortalidadMachos = dto.MortalidadMachos,
            SelH = dto.SelH,
            SelM = dto.SelM,
            ErrorSexajeHembras = dto.ErrorSexajeHembras,
            ErrorSexajeMachos = dto.ErrorSexajeMachos,
            ConsumoKgHembras = (decimal)dto.ConsumoKgHembras,
            ConsumoKgMachos = dto.ConsumoKgMachos.HasValue ? (decimal)dto.ConsumoKgMachos.Value : null,
            TipoAlimento = dto.TipoAlimento,
            Observaciones = dto.Observaciones,
            Ciclo = dto.Ciclo,
            PesoPromHembras = dto.PesoPromH,
            PesoPromMachos = dto.PesoPromM,
            UniformidadHembras = dto.UniformidadH,
            UniformidadMachos = dto.UniformidadM,
            CvHembras = dto.CvH,
            CvMachos = dto.CvM,
            ConsumoAguaDiario = dto.ConsumoAguaDiario,
            ConsumoAguaPh = dto.ConsumoAguaPh,
            ConsumoAguaOrp = dto.ConsumoAguaOrp,
            ConsumoAguaTemperatura = dto.ConsumoAguaTemperatura,
            Metadata = dto.Metadata,
            ItemsAdicionales = dto.ItemsAdicionales,
            KcalAlH = dto.KcalAlH,
            ProtAlH = dto.ProtAlH,
            KcalAveH = dto.KcalAveH,
            ProtAveH = dto.ProtAveH,
            CreatedByUserId = dto.CreatedByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null,
            // Panamá: quintales por categoría (el DTO ya los traía; antes se descartaban al persistir)
            QqMixtas = dto.QqMixtas,
            QqHembras = dto.QqHembras,
            QqMachos = dto.QqMachos
        };
        // ── Doble validación ───────────────────────────────────────────────────────────────────
        // Reproductora ya tenía `confirmado`; ahora esa marca es la MISMA doble validación: mientras
        // no se confirma, el alimento queda separado (no descontado) y el cruce a pollo engorde sigue
        // sin dispararse, que es como venía funcionando.
        var separa = _validacion is not null
                  && ValidacionSeguimientoCalculos.SeparaAlGuardar(await _validacion.RequiereValidacionAsync());
        if (separa)
        {
            // Era el único de los cinco módulos que no cortaba el alta con días vencidos sin confirmar,
            // aunque el flag lo promete por escrito. Sin esto, un lote acumula días sin confirmar y el
            // alimento queda separado sin techo.
            await _validacion!.AsegurarPuedeRegistrarDiaAsync(ModuloSeguimiento.Reproductora, dto.LoteId);
            // Los kilos sueltos van SIEMPRE: la app móvil, la carga masiva por Excel y la PWA mandan
            // el consumo como campo, no como ítems de inventario, y mirando sólo el metadata el guard
            // rechazaba con «no tiene alimento» un registro que sí lo traía.
            SeparacionSeguimientoHelper.ValidarAlimentoObligatorio(
                ModuloSeguimiento.Reproductora, loteEsMixto: false, dto.Metadata, dto.FechaRegistro,
                (decimal)dto.ConsumoKgHembras, (decimal)(dto.ConsumoKgMachos ?? 0));
        }

        // El stock se comprueba ANTES de guardar. Antes el registro se persistía primero y el consumo
        // iba después dentro de un catch que ni siquiera logueaba (Console.WriteLine): se podía cargar
        // un día de un alimento sin un solo kilo en el galpón y nadie se enteraba.
        if (!separa && _inventarioGestionService != null && dto.Metadata != null)
        {
            var ubicacionPrev = await GetLoteUbicacionAsync(dto.LoteId);
            if (ubicacionPrev.HasValue &&
                InventarioConsumoGate.DebeDescontarModeloB(await ResolverPaisIdPorGranjaAsync(ubicacionPrev.Value.FarmId)))
            {
                var (farmPrev, nucPrev, galPrev) = ubicacionPrev.Value;
                await _inventarioGestionService.ValidarStockConsumoAsync(
                    farmPrev, nucPrev?.Trim(), galPrev?.Trim(),
                    ParseMetadataItemsToKg(dto.Metadata.RootElement));
            }
        }

        _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.Add(ent);
        await _ctx.SaveChangesAsync();

        if (separa)
        {
            var ubicacionSep = await GetLoteUbicacionAsync(dto.LoteId);
            await _validacion!.SepararAsync(SeparacionSeguimientoHelper.Contexto(
                ModuloSeguimiento.Reproductora, ent.Id,
                ubicacionSep.HasValue ? await ResolverPaisIdPorGranjaAsync(ubicacionSep.Value.FarmId) : null,
                ubicacionSep?.FarmId ?? 0, ubicacionSep?.NucleoId, ubicacionSep?.GalponId,
                dto.LoteId, dto.LoteId.ToString(), dto.FechaRegistro, dto.Metadata,
                dto.MortalidadHembras, dto.SelH, dto.ErrorSexajeHembras,
                dto.MortalidadMachos, dto.SelM, dto.ErrorSexajeMachos,
                poblacionEsMixta: false));
        }

        // Descontar inventario por ítems consumidos
        if (!separa && _inventarioGestionService != null && dto.Metadata != null)
        {
            try
            {
                var ubicacion = await GetLoteUbicacionAsync(dto.LoteId);
                // Gate por PAÍS (S1): solo Ecuador/Panamá descuentan del modelo B; para lotes Colombia
                // NO se invoca (evita el descuento cross-país silencioso por el fallback catalogItemId).
                if (ubicacion.HasValue &&
                    InventarioConsumoGate.DebeDescontarModeloB(await ResolverPaisIdPorGranjaAsync(ubicacion.Value.FarmId)))
                {
                    var (farmId, nucleoId, galponId) = ubicacion.Value;
                    var byItem = ParseMetadataItemsToKg(dto.Metadata.RootElement);
                    var refStr = $"Seguimiento reproductora #{ent.Id} {dto.FechaRegistro:yyyy-MM-dd}";
                    foreach (var kv in byItem)
                        if (kv.Value > 0)
                            // Fecha del movimiento = día del seguimiento (no el de la carga): en una
                            // carga histórica de la primera semana, todos caían el mismo día y el
                            // kardex del galpón quedaba ilegible.
                            await _inventarioGestionService.RegistrarConsumoAsync(new InventarioGestionConsumoRequest(
                                farmId, nucleoId?.Trim(), galponId?.Trim(), kv.Key, kv.Value, "kg", refStr, null,
                                FechaMovimiento: dto.FechaRegistro.Date));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al registrar consumo inventario (reproductora): {ex.Message}");
            }
        }

        return MapToDto(ent);
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    public async Task<SeguimientoLoteLevanteDto?> UpdateAsync(SeguimientoLoteLevanteDto dto)
    {
        var companyId = _current.CompanyId;
        var filaUpd = await (from s in _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde
                         join l in _ctx.LoteReproductoraAveEngorde.AsNoTracking() on s.LoteReproductoraAveEngordeId equals l.Id
                         join lae in _ctx.LoteAveEngorde.AsNoTracking() on l.LoteAveEngordeId equals lae.LoteAveEngordeId
                         where s.Id == dto.Id && lae.CompanyId == companyId && lae.DeletedAt == null
                         select new { Ent = s, lae.EstadoOperativoLote }).SingleOrDefaultAsync();
        if (filaUpd is null) return null;

        // Gate B6 — editar reproductora re-dispara el cruce sobre los días 1-7 del lote de engorde.
        LiquidacionCongeladaGateCalculos.ValidarEscritura(
            filaUpd.EstadoOperativoLote, OperacionLoteEngordeLiquidado.SeguimientoReproductora);
        var ent = filaUpd.Ent;

        // Un registro confirmado es de solo lectura: para corregirlo se elimina (retorna aves/consumo) y se recrea.
        if (ent.Confirmado)
            throw new InvalidOperationException(
                "El registro está confirmado y no puede editarse. Elimínelo (se retornan aves y consumo) para corregirlo.");

        // Regla de fecha (defensa en profundidad, espejo del Create): día 1 = día del encasetamiento
        // (edad 0); se acepta edad [0, 7] respecto al encasetamiento.
        const int MaxDiasSeguimiento = 7;
        var loteUpd = await _ctx.LoteReproductoraAveEngorde.AsNoTracking()
            .Where(l => l.Id == ent.LoteReproductoraAveEngordeId)
            .Select(l => new { l.FechaEncasetamiento, l.HoraEncasetamiento })
            .FirstOrDefaultAsync();
        var encasetUpd = loteUpd?.FechaEncasetamiento;
        var fechaAncladaUpd = FechasPuras.AnclarMediodiaUtc(dto.FechaRegistro);
        if (encasetUpd.HasValue)
        {
            var horaReglaUpd = EncasetamientoCalculos.HoraEfectiva(
                loteUpd!.HoraEncasetamiento, await PrimerRegistroPorHoraGate.ActivaAsync(_ctx, _current.CompanyId));
            var edadMinimaUpd = EncasetamientoCalculos.EdadMinimaConRegistro(horaReglaUpd);
            var edadUpd = ReproductoraEngordeCalculos.EdadSeguimientoDias(encasetUpd.Value, fechaAncladaUpd);
            if (!ReproductoraEngordeCalculos.EsEdadSeguimientoValida(edadUpd, MaxDiasSeguimiento, edadMinimaUpd))
                throw new InvalidOperationException(
                    edadUpd < edadMinimaUpd
                        ? MensajeFechaMuyTemprana(encasetUpd.Value, horaReglaUpd)
                        : "La fecha del seguimiento supera la primera semana de recogida contada desde el encasetamiento.");
        }

        // ── Doble validación ───────────────────────────────────────────────────────────────────
        var separaUpd = _validacion is not null
                     && ValidacionSeguimientoCalculos.SeparaAlGuardar(await _validacion.RequiereValidacionAsync());
        if (separaUpd)
            SeparacionSeguimientoHelper.ValidarAlimentoObligatorio(
                ModuloSeguimiento.Reproductora, loteEsMixto: false, dto.Metadata, dto.FechaRegistro,
                (decimal)dto.ConsumoKgHembras, (decimal)(dto.ConsumoKgMachos ?? 0));

        // Capturar ítems anteriores antes de actualizar
        var oldByItemId = ent.Metadata != null
            ? ParseMetadataItemsToKg(ent.Metadata.RootElement)
            : new Dictionary<int, decimal>();

        ent.Fecha = fechaAncladaUpd;
        ent.MortalidadHembras = dto.MortalidadHembras;
        ent.MortalidadMachos = dto.MortalidadMachos;
        ent.SelH = dto.SelH;
        ent.SelM = dto.SelM;
        ent.ErrorSexajeHembras = dto.ErrorSexajeHembras;
        ent.ErrorSexajeMachos = dto.ErrorSexajeMachos;
        ent.ConsumoKgHembras = (decimal)dto.ConsumoKgHembras;
        ent.ConsumoKgMachos = dto.ConsumoKgMachos.HasValue ? (decimal)dto.ConsumoKgMachos.Value : null;
        ent.TipoAlimento = dto.TipoAlimento;
        ent.Observaciones = dto.Observaciones;
        ent.Ciclo = dto.Ciclo;
        ent.PesoPromHembras = dto.PesoPromH;
        ent.PesoPromMachos = dto.PesoPromM;
        ent.UniformidadHembras = dto.UniformidadH;
        ent.UniformidadMachos = dto.UniformidadM;
        ent.CvHembras = dto.CvH;
        ent.CvMachos = dto.CvM;
        ent.ConsumoAguaDiario = dto.ConsumoAguaDiario;
        ent.ConsumoAguaPh = dto.ConsumoAguaPh;
        ent.ConsumoAguaOrp = dto.ConsumoAguaOrp;
        ent.ConsumoAguaTemperatura = dto.ConsumoAguaTemperatura;
        // Panamá: quintales por categoría (espejo del Create).
        ent.QqMixtas = dto.QqMixtas;
        ent.QqHembras = dto.QqHembras;
        ent.QqMachos = dto.QqMachos;
        ent.Metadata = dto.Metadata;
        ent.ItemsAdicionales = dto.ItemsAdicionales;
        ent.KcalAlH = dto.KcalAlH;
        ent.ProtAlH = dto.ProtAlH;
        ent.KcalAveH = dto.KcalAveH;
        ent.ProtAveH = dto.ProtAveH;
        ent.UpdatedAt = DateTime.UtcNow;
        // La entidad se cargó con una query con joins AsNoTracking → NO queda rastreada,
        // por lo que asignar propiedades no emite UPDATE. Forzar el estado Modified para
        // persistir TODAS las columnas (incl. fecha y jsonb) y disparar el trigger de cruce.
        // Solo los INCREMENTOS consumen: la edición a la baja devuelve, y devolver nunca se queda sin
        // stock. Se comprueban ANTES de guardar, igual que en el alta.
        if (!separaUpd && _inventarioGestionService != null && (dto.Metadata != null || oldByItemId.Count > 0))
        {
            var ubicacionPrev = await GetLoteUbicacionAsync(dto.LoteId);
            if (ubicacionPrev.HasValue &&
                InventarioConsumoGate.DebeDescontarModeloB(await ResolverPaisIdPorGranjaAsync(ubicacionPrev.Value.FarmId)))
            {
                var nuevos = dto.Metadata != null
                    ? ParseMetadataItemsToKg(dto.Metadata.RootElement)
                    : new Dictionary<int, decimal>();
                var incrementos = new Dictionary<int, decimal>();
                foreach (var itemId in new HashSet<int>(oldByItemId.Keys.Concat(nuevos.Keys)))
                {
                    var diff = nuevos.GetValueOrDefault(itemId) - oldByItemId.GetValueOrDefault(itemId);
                    if (diff > 0) incrementos[itemId] = diff;
                }
                var (farmPrev, nucPrev, galPrev) = ubicacionPrev.Value;
                await _inventarioGestionService.ValidarStockConsumoAsync(
                    farmPrev, nucPrev?.Trim(), galPrev?.Trim(), incrementos);
            }
        }

        // Mismo patrón que SeguimientoAvesEngordeService.UpdateAsync.
        _ctx.Entry(ent).State = EntityState.Modified;
        _ctx.Entry(ent).Property(e => e.Metadata).IsModified = true;
        _ctx.Entry(ent).Property(e => e.ItemsAdicionales).IsModified = true;
        await _ctx.SaveChangesAsync();

        // Editar un pendiente REESCRIBE la separación: nada que devolver, nunca se descontó.
        if (separaUpd)
        {
            var ubicacionUpd = await GetLoteUbicacionAsync(dto.LoteId);
            await _validacion!.SepararAsync(SeparacionSeguimientoHelper.Contexto(
                ModuloSeguimiento.Reproductora, ent.Id,
                ubicacionUpd.HasValue ? await ResolverPaisIdPorGranjaAsync(ubicacionUpd.Value.FarmId) : null,
                ubicacionUpd?.FarmId ?? 0, ubicacionUpd?.NucleoId, ubicacionUpd?.GalponId,
                dto.LoteId, dto.LoteId.ToString(), dto.FechaRegistro, dto.Metadata,
                dto.MortalidadHembras, dto.SelH, dto.ErrorSexajeHembras,
                dto.MortalidadMachos, dto.SelM, dto.ErrorSexajeMachos,
                poblacionEsMixta: false));
        }

        // Ajustar inventario: consumir diferencia positiva, devolver diferencia negativa
        if (!separaUpd && _inventarioGestionService != null && (dto.Metadata != null || oldByItemId.Count > 0))
        {
            try
            {
                var ubicacion = await GetLoteUbicacionAsync(dto.LoteId);
                // Gate por PAÍS (S1): solo Ecuador/Panamá ajustan el modelo B.
                if (ubicacion.HasValue &&
                    InventarioConsumoGate.DebeDescontarModeloB(await ResolverPaisIdPorGranjaAsync(ubicacion.Value.FarmId)))
                {
                    var (farmId, nucleoId, galponId) = ubicacion.Value;
                    var newByItemId = dto.Metadata != null
                        ? ParseMetadataItemsToKg(dto.Metadata.RootElement)
                        : new Dictionary<int, decimal>();
                    var allItemIds = new HashSet<int>(oldByItemId.Keys);
                    foreach (var k in newByItemId.Keys) allItemIds.Add(k);
                    var refStr = $"Seguimiento reproductora #{dto.Id} {dto.FechaRegistro:yyyy-MM-dd}";
                    foreach (var itemId in allItemIds)
                    {
                        var newQty = newByItemId.GetValueOrDefault(itemId);
                        var oldQty = oldByItemId.GetValueOrDefault(itemId);
                        var diff = newQty - oldQty;
                        if (diff > 0)
                            await _inventarioGestionService.RegistrarConsumoAsync(new InventarioGestionConsumoRequest(
                                farmId, nucleoId?.Trim(), galponId?.Trim(), itemId, diff, "kg", refStr + " (ajuste)", null));
                        else if (diff < 0)
                            await _inventarioGestionService.RegistrarIngresoAsync(new InventarioGestionIngresoRequest(
                                farmId, nucleoId?.Trim(), galponId?.Trim(), itemId, -diff, "kg", refStr + " (devolución)", "Devolución desde seguimiento reproductora"));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al actualizar inventario (reproductora): {ex.Message}");
            }
        }

        return MapToDto(ent);
    }

    // ─── Confirmar ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Confirma un registro reproductora. Al guardar dispara trg_cruce_reproductora_engorde:
    /// la función de cruce solo cuenta registros confirmados, así el día sincroniza hacia el
    /// seguimiento pollo engorde cuando TODOS los lotes reproductora confirmaron esa edad.
    /// Idempotente: si ya estaba confirmado no re-dispara nada.
    /// </summary>
    public async Task<SeguimientoLoteLevanteDto?> ConfirmarAsync(int id)
    {
        var companyId = _current.CompanyId;
        // Scoping por compañía sin arrastrar la entidad al tracker por el join.
        var gateConfirmar = await (from s in _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.AsNoTracking()
                               join l in _ctx.LoteReproductoraAveEngorde.AsNoTracking() on s.LoteReproductoraAveEngordeId equals l.Id
                               join lae in _ctx.LoteAveEngorde.AsNoTracking() on l.LoteAveEngordeId equals lae.LoteAveEngordeId
                               where s.Id == id && lae.CompanyId == companyId && lae.DeletedAt == null
                               select new { lae.EstadoOperativoLote }).FirstOrDefaultAsync();
        if (gateConfirmar is null) return null;

        // Gate B6 — confirmar dispara el cruce: DELETE+INSERT de los días 1-7 del lote de engorde.
        LiquidacionCongeladaGateCalculos.ValidarEscritura(
            gateConfirmar.EstadoOperativoLote, OperacionLoteEngordeLiquidado.SeguimientoReproductora);

        var ent = await _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde
            .SingleOrDefaultAsync(s => s.Id == id);
        if (ent is null) return null;

        // Idempotente en cuanto al registro, pero igual se sincronizan las bajas del cruce: es la vía
        // de reparación de los lotes que se confirmaron antes de que esto existiera (re-confirmar un
        // día pone al día el maestro de aves sin tocar nada más).
        if (ent.Confirmado)
        {
            await SincronizarBajasCruceAsync(ent.LoteReproductoraAveEngordeId);
            return MapToDto(ent);
        }

        // Con doble validación, confirmar ES validar: aplica el alimento que estaba separado y recién
        // después escribe `confirmado` (lo que dispara el cruce). Sin el flag, el camino es el de
        // siempre — el alimento ya se había descontado al crear.
        if (_validacion is not null && await _validacion.RequiereValidacionAsync())
        {
            await _validacion.ValidarAsync(ModuloSeguimiento.Reproductora, ent.Id);
            await _ctx.Entry(ent).ReloadAsync();
            await SincronizarBajasCruceAsync(ent.LoteReproductoraAveEngordeId);
            return MapToDto(ent);
        }

        ent.Confirmado = true;
        ent.ConfirmadoAt = DateTime.UtcNow;
        ent.ConfirmadoPor = _current.UserId > 0 ? _current.UserId.ToString() : null;
        ent.UpdatedAt = DateTime.UtcNow;
        // Entidad rastreada por PK → EF emite UPDATE de las columnas cambiadas → dispara el trigger de cruce.
        await _ctx.SaveChangesAsync();

        // El cruce ya escribió (o rehízo) los días 1-7 de engorde por SQL. Sus bajas tienen que llegar
        // al maestro de aves del lote igual que las de los días 8+: si no, `hembras_l/machos_l` queda
        // por encima del real y el sistema deja despachar aves que ya murieron.
        await SincronizarBajasCruceAsync(ent.LoteReproductoraAveEngordeId);

        return MapToDto(ent);
    }

    /// <summary>
    /// Lleva al maestro del lote de engorde las bajas de los días que generó el cruce. Idempotente; los
    /// fallos se registran sin tumbar la confirmación (el cruce ya ocurrió y el reporte diario, que
    /// calcula desde <c>aves_encasetadas</c>, sigue mostrando el saldo correcto).
    /// </summary>
    private async Task SincronizarBajasCruceAsync(int loteReproductoraId)
    {
        try
        {
            var loteEngordeId = await _ctx.LoteReproductoraAveEngorde.AsNoTracking()
                .Where(l => l.Id == loteReproductoraId)
                .Select(l => l.LoteAveEngordeId)
                .FirstOrDefaultAsync();
            if (loteEngordeId <= 0) return;

            await RetiroAvesEngordeAplicador.SincronizarCruceAsync(_ctx, _current.CompanyId, loteEngordeId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al sincronizar las bajas del cruce con el maestro de aves: {ex.Message}");
        }
    }

    // ─── Delete ───────────────────────────────────────────────────────────────

    public async Task<bool> DeleteAsync(int id)
    {
        var companyId = _current.CompanyId;
        var filaDel = await (from s in _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde
                         join l in _ctx.LoteReproductoraAveEngorde.AsNoTracking() on s.LoteReproductoraAveEngordeId equals l.Id
                         join lae in _ctx.LoteAveEngorde.AsNoTracking() on l.LoteAveEngordeId equals lae.LoteAveEngordeId
                         where s.Id == id && lae.CompanyId == companyId && lae.DeletedAt == null
                         select new { Ent = s, lae.EstadoOperativoLote }).SingleOrDefaultAsync();
        if (filaDel is null) return false;

        // Gate B6 — eliminar reproductora re-dispara el cruce sobre los días 1-7 del lote de engorde.
        LiquidacionCongeladaGateCalculos.ValidarEscritura(
            filaDel.EstadoOperativoLote, OperacionLoteEngordeLiquidado.SeguimientoReproductora);
        var ent = filaDel.Ent;

        // ── Guard de cierre: un lote cerrado solo permite eliminar si fue reabierto con novedad ──
        const int MaxDiasSeguimiento = 7;
        var loteRep = await _ctx.LoteReproductoraAveEngorde
            .SingleOrDefaultAsync(l => l.Id == ent.LoteReproductoraAveEngordeId);
        if (loteRep is not null)
        {
            // Coherente con el estado del lote: cerrado = los 7 días CONFIRMADOS (no por nº de registros
            // ni por aves agotadas). Mientras haya pendientes el lote sigue Vigente → se puede eliminar.
            var numConfirmados = await _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde
                .CountAsync(s => s.LoteReproductoraAveEngordeId == loteRep.Id && s.Confirmado);
            var cerrado = numConfirmados >= MaxDiasSeguimiento;

            if (cerrado && !loteRep.Reabierto)
                throw new InvalidOperationException(
                    "El lote reproductora está cerrado. Reábralo con una novedad para poder eliminar registros.");
        }

        // Doble validación: borrar un pendiente solo libera la separación; no hay stock que restituir
        // porque nunca se descontó.
        var separaDel = _validacion is not null
                     && ValidacionSeguimientoCalculos.SeparaAlGuardar(await _validacion.RequiereValidacionAsync());
        if (separaDel)
            await _validacion!.LiberarAsync(ModuloSeguimiento.Reproductora, ent.Id);

        // Restituir stock antes de eliminar
        if (!separaDel && _inventarioGestionService != null && ent.Metadata != null)
        {
            try
            {
                var ubicacion = await GetLoteUbicacionAsync(ent.LoteReproductoraAveEngordeId);
                // Gate por PAÍS (S1): solo Ecuador/Panamá devuelven al modelo B.
                if (ubicacion.HasValue &&
                    InventarioConsumoGate.DebeDescontarModeloB(await ResolverPaisIdPorGranjaAsync(ubicacion.Value.FarmId)))
                {
                    var (farmId, nucleoId, galponId) = ubicacion.Value;
                    var byItem = ParseMetadataItemsToKg(ent.Metadata.RootElement);
                    var refStr = $"Seguimiento reproductora #{id} (devolución por eliminación)";
                    foreach (var kv in byItem)
                        if (kv.Value > 0)
                            await _inventarioGestionService.RegistrarIngresoAsync(new InventarioGestionIngresoRequest(
                                farmId, nucleoId?.Trim(), galponId?.Trim(), kv.Key, kv.Value, "kg", refStr, "Devolución por eliminación de seguimiento reproductora"));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al devolver inventario al eliminar seguimiento reproductora: {ex.Message}");
            }
        }

        _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.Remove(ent);

        // "Recierra solo": al eliminar se consume la reapertura; el estado se recalcula.
        // Se conserva novedad_apertura / reabierto_at como histórico del último motivo.
        if (loteRep is not null && loteRep.Reabierto)
        {
            loteRep.Reabierto = false;
            loteRep.UpdatedAt = DateTime.UtcNow;
        }

        await _ctx.SaveChangesAsync();

        // El trigger acaba de rehacer (o borrar) los días 1-7 de engorde: hay que devolver al maestro
        // las aves de los días que desaparecieron y descontar las de los que se regeneraron.
        await SincronizarBajasCruceAsync(ent.LoteReproductoraAveEngordeId);

        return true;
    }

    /// <summary>
    /// Mensaje de "fecha demasiado temprana". Cuando el lote llegó a las 13:00 o después, el día del
    /// encasetamiento no admite registro y hay que decir POR QUÉ: si no, el usuario ve rechazada una
    /// fecha que ayer era válida y lo lee como un bug.
    /// </summary>
    private static string MensajeFechaMuyTemprana(DateTime fechaEncasetamiento, TimeOnly? horaEncasetamiento)
    {
        var motivo = EncasetamientoCalculos.MotivoDesplazamiento(horaEncasetamiento);
        if (motivo is null)
            return "La fecha del seguimiento no puede ser anterior a la fecha de encasetamiento del lote reproductora (el día del encasetamiento es el día 1).";

        var primerDia = EncasetamientoCalculos.PrimerDiaConRegistro(fechaEncasetamiento, horaEncasetamiento);
        return $"El primer registro de este lote es el {primerDia:yyyy-MM-dd}: {motivo}.";
    }
}
