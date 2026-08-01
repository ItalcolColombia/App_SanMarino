// Infrastructure/Services/Funciones/ProduccionService.Consultas.cs — lecturas: listado vía fn_seguimiento_diario_produccion, información del lote, seguimiento por id y mapeos a SeguimientoItemDto.
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
    public async Task<ListaSeguimientoResponse> ListarSeguimientoAsync(int? loteId, int? lotePosturaProduccionId, DateTime? desde, DateTime? hasta, int page, int size)
    {
        if (!lotePosturaProduccionId.HasValue && !loteId.HasValue)
            throw new ArgumentException("Debe especificar loteId o lotePosturaProduccionId.");

        var companyId = _currentUser.CompanyId;
        int produccionLoteId;
        int? fnLppId = null;
        int? fnLoteId = null;

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
            fnLppId = lotePosturaProduccionId.Value;
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
            fnLoteId = produccionLoteId;
        }

        // ── Lectura por fn_seguimiento_diario_produccion (ÚNICA fórmula de la grilla y sus
        //    derivados: saldos de aves, acumulados de huevos, % postura, edad/semana). El gate
        //    de alcance ya corrió arriba (fail-closed). ──
        var filas = await _context.Database
            .SqlQueryRaw<SeguimientoProduccionTablaFilaDto>(
                "SELECT * FROM fn_seguimiento_diario_produccion({0}::int, {1}::int)",
                (object?)fnLppId ?? DBNull.Value,
                (object?)fnLoteId ?? DBNull.Value)
            .ToListAsync()
            .ConfigureAwait(false);

        // La grilla lista REGISTROS: las filas movimiento-only (seg_id NULL) alimentan saldos y
        // acumulados dentro de la fn pero no son fila editable del listado.
        IEnumerable<SeguimientoProduccionTablaFilaDto> visibles = filas.Where(f => f.SegId != null);
        if (desde.HasValue) visibles = visibles.Where(f => f.FechaTs != null && f.FechaTs.Value >= desde.Value);
        if (hasta.HasValue)
        {
            var h = hasta.Value.Date.AddDays(1);
            visibles = visibles.Where(f => f.FechaTs != null && f.FechaTs.Value < h);
        }

        var ordenadas = visibles.OrderByDescending(f => f.FechaTs).ToList();
        var total = ordenadas.Count;

        var pageSafe = Math.Max(1, page);
        // size <= 0 => sin paginación (traer todo)
        var sizeSafe = size <= 0 ? 0 : Math.Clamp(size, 1, 100_000);

        var pagina = sizeSafe == 0
            ? ordenadas
            : ordenadas.Skip((pageSafe - 1) * sizeSafe).Take(sizeSafe).ToList();

        var items = pagina.Select(f => MapFnRowToSeguimientoItemDto(f, produccionLoteId)).ToList();
        return new ListaSeguimientoResponse(items, total);
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

        // ── D4: el saldo de aves lo dicta fn_seguimiento_diario_produccion (única fórmula):
        //    última fila de la serie (incluye días movimiento-only) = saldo CON error de sexaje
        //    y con los movimientos de la FASE producción (>= fecha_inicio_produccion). Antes acá
        //    vivía una segunda fórmula (sin err_sexaje y contando también los movimientos del
        //    LEVANTE, que ya están dentro de las aves iniciales del LPP — doble descuento). ──
        var ultimaFila = (await _context.Database
                .SqlQueryRaw<SeguimientoProduccionTablaFilaDto>(
                    "SELECT * FROM fn_seguimiento_diario_produccion({0}::int, NULL::int)",
                    lotePosturaProduccionId)
                .ToListAsync()
                .ConfigureAwait(false))
            .OrderBy(f => f.Fecha).ThenBy(f => f.SegId ?? 0)
            .LastOrDefault();

        var avesActualesH = ultimaFila?.SaldoAvesH ?? Math.Max(0, avesInicialesH);
        var avesActualesM = ultimaFila?.SaldoAvesM ?? Math.Max(0, avesInicialesM);

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
            Metadata: MetadataFromJsonDocument(e.Metadata),
            ErrorSexajeHembras: e.ErrorSexajeHembras,
            ErrorSexajeMachos: e.ErrorSexajeMachos,
            UniformidadHembras: e.UniformidadHembras,
            UniformidadMachos: e.UniformidadMachos,
            CvHembras: e.CvHembras,
            CvMachos: e.CvMachos,
            Ciclo: e.Ciclo,
            EsTraslado: e.EsTraslado,
            TrasladoDireccion: e.TrasladoDireccion,
            TrasladoIngresoHembras: e.TrasladoIngresoHembras,
            TrasladoIngresoMachos: e.TrasladoIngresoMachos,
            TrasladoSalidaHembras: e.TrasladoSalidaHembras,
            TrasladoSalidaMachos: e.TrasladoSalidaMachos,
            LoteDestinoId: e.LoteDestinoId,
            GranjaDestinoId: e.GranjaDestinoId
        );
    }

    /// <summary>
    /// Fila de <c>fn_seguimiento_diario_produccion</c> → contrato histórico de la grilla.
    /// Paridad byte a byte con <see cref="MapToSeguimientoItemDto"/> (incluidos
    /// <c>CreatedAt = fecha del registro</c> y <c>UpdatedAt = null</c>, que el contrato viejo
    /// ya falseaba) + los derivados que solo la fn conoce (saldos, acumulados, % postura).
    /// </summary>
    private static SeguimientoItemDto MapFnRowToSeguimientoItemDto(SeguimientoProduccionTablaFilaDto f, int produccionLoteId)
    {
        var consKgH = (decimal)(f.ConsKgH ?? 0);
        var consKgM = (decimal)(f.ConsKgM ?? 0);
        var fechaRegistro = f.FechaTs ?? f.Fecha;
        return new SeguimientoItemDto(
            (int)(f.SegId ?? 0),
            produccionLoteId,
            fechaRegistro,
            f.MortalidadHembras ?? 0,
            f.MortalidadMachos ?? 0,
            f.SelH ?? 0,
            f.SelM ?? 0,
            consKgH,
            consKgM,
            consKgH + consKgM,
            f.HuevoTot ?? 0,
            f.HuevoInc ?? 0,
            f.TipoAlimento ?? "",
            (decimal)(f.PesoHuevo ?? 0),
            f.Etapa ?? 0,
            f.Observaciones,
            CreatedAt: fechaRegistro,
            UpdatedAt: null,
            f.HuevoLimpio ?? 0,
            f.HuevoTratado ?? 0,
            f.HuevoSucio ?? 0,
            f.HuevoDeforme ?? 0,
            f.HuevoBlanco ?? 0,
            f.HuevoDobleYema ?? 0,
            f.HuevoPiso ?? 0,
            f.HuevoPequeno ?? 0,
            f.HuevoRoto ?? 0,
            f.HuevoDesecho ?? 0,
            f.HuevoOtro ?? 0,
            f.PesoH,
            f.PesoM,
            f.Uniformidad,
            f.CoeficienteVariacion,
            f.ObservacionesPesaje,
            f.ConsumoAguaDiario,
            f.ConsumoAguaPh,
            f.ConsumoAguaOrp,
            f.ConsumoAguaTemperatura,
            f.LotePosturaProduccionId,
            Metadata: MetadataFromJsonString(f.Metadata),
            ErrorSexajeHembras: f.ErrorSexajeHembras ?? 0,
            ErrorSexajeMachos: f.ErrorSexajeMachos ?? 0,
            UniformidadHembras: f.UniformidadHembras,
            UniformidadMachos: f.UniformidadMachos,
            CvHembras: f.CvHembras,
            CvMachos: f.CvMachos,
            Ciclo: f.Ciclo,
            EsTraslado: f.EsTraslado,
            TrasladoDireccion: f.TrasladoDireccion,
            TrasladoIngresoHembras: f.TrasladoIngresoHembras ?? 0,
            TrasladoIngresoMachos: f.TrasladoIngresoMachos ?? 0,
            TrasladoSalidaHembras: f.TrasladoSalidaHembras ?? 0,
            TrasladoSalidaMachos: f.TrasladoSalidaMachos ?? 0,
            LoteDestinoId: f.LoteDestinoId,
            GranjaDestinoId: f.GranjaDestinoId,
            EdadDias: f.EdadDias,
            Semana: f.Semana,
            AvesHInicioDia: f.AvesHInicioDia,
            AvesMInicioDia: f.AvesMInicioDia,
            SaldoAvesH: f.SaldoAvesH,
            SaldoAvesM: f.SaldoAvesM,
            HuevoTotAcum: f.HuevoTotAcum,
            HuevoIncAcum: f.HuevoIncAcum,
            PctPosturaDia: f.PctPosturaDia
        );
    }

    private static object? MetadataFromJsonString(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<object>(json);
        }
        catch
        {
            return null;
        }
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
}
