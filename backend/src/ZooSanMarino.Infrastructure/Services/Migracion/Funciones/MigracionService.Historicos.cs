// src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.Historicos.cs
// Fase 2 — Seguimientos históricos (Levante / Producción). Elegibilidad + plantilla por lote +
// parse/validación en C#; la INSERCIÓN MASIVA la hace la BD (funciones plpgsql set-based que
// insertan el estado final idempotente y recomputan aves_h_actual/m_actual una sola vez).
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Migracion;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MigracionService
{
    // ── Elegibilidad ─────────────────────────────────────────────────────────
    private async Task<IReadOnlyList<LoteElegibleDto>> ElegiblesHistoricosAsync(TipoMigracion tipo, int companyId, MigracionContextoDto ctx, CancellationToken ct)
    {
        List<int> loteIdsElegibles;
        if (tipo == TipoMigracion.SeguimientoLevante)
        {
            loteIdsElegibles = await _ctx.Set<LotePosturaLevante>().AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.DeletedAt == null && x.LoteId != null)
                .Select(x => x.LoteId!.Value).Distinct().ToListAsync(ct);
        }
        else // Producción: LPL Cerrado + liquidado + existe LPP
        {
            var lplCerradasLiquidadas = _ctx.Set<LotePosturaLevante>().AsNoTracking()
                .Where(lpl => lpl.CompanyId == companyId && lpl.DeletedAt == null && lpl.EstadoCierre == "Cerrado"
                    && _ctx.Set<LiquidacionCierreLoteLevante>().Any(li => li.LotePosturaLevanteId == lpl.LotePosturaLevanteId))
                .Select(lpl => lpl.LotePosturaLevanteId);

            loteIdsElegibles = await _ctx.Set<LotePosturaProduccion>().AsNoTracking()
                .Where(lpp => lpp.CompanyId == companyId && lpp.DeletedAt == null && lpp.LoteId != null
                    && lpp.LotePosturaLevanteId != null && lplCerradasLiquidadas.Contains(lpp.LotePosturaLevanteId))
                .Select(lpp => lpp.LoteId!.Value).Distinct().ToListAsync(ct);
        }

        if (loteIdsElegibles.Count == 0) return Array.Empty<LoteElegibleDto>();

        var q = _ctx.Lotes.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.LoteId != null && loteIdsElegibles.Contains(l.LoteId!.Value));
        if (ctx.GranjaId is int g) q = q.Where(l => l.GranjaId == g);
        if (!string.IsNullOrWhiteSpace(ctx.NucleoId)) q = q.Where(l => l.NucleoId == ctx.NucleoId);
        if (!string.IsNullOrWhiteSpace(ctx.GalponId)) q = q.Where(l => l.GalponId == ctx.GalponId);

        return await q.OrderBy(l => l.LoteNombre)
            .Select(l => new LoteElegibleDto(l.LoteId!.Value, l.LoteNombre, l.GranjaId, l.NucleoId, l.GalponId, l.Fase ?? "", null))
            .ToListAsync(ct);
    }

    private async Task<bool> EsLoteElegibleAsync(TipoMigracion tipo, int companyId, int loteId, CancellationToken ct)
    {
        var elegibles = await ElegiblesHistoricosAsync(tipo, companyId, new MigracionContextoDto(null, null, null, loteId), ct);
        return elegibles.Any(e => e.LoteId == loteId);
    }

    // ── Plantillas por lote ──────────────────────────────────────────────────
    private async Task<(byte[] Contenido, string NombreArchivo)> GenerarPlantillaSeguimientoAsync(TipoMigracion tipo, int companyId, MigracionContextoDto ctx, CancellationToken ct)
    {
        if (ctx.LoteId is not int loteId)
            throw new ArgumentException("Seleccioná un lote elegible para descargar su plantilla.");
        var lote = await _ctx.Lotes.AsNoTracking().FirstOrDefaultAsync(l => l.LoteId == loteId && l.CompanyId == companyId && l.DeletedAt == null, ct)
                   ?? throw new ArgumentException("El lote no existe en la empresa.");
        if (!await EsLoteElegibleAsync(tipo, companyId, loteId, ct))
            throw new InvalidOperationException($"El lote {lote.LoteNombre} no está habilitado para migración de {(tipo == TipoMigracion.SeguimientoLevante ? "Levante" : "Producción")}.");

        var esLevante = tipo == TipoMigracion.SeguimientoLevante;
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Datos");

        var headers = esLevante
            ? new[] { "Fecha", "Mort H", "Mort M", "Sel H", "Sel M", "Error Sexaje H", "Error Sexaje M", "Consumo H (kg)", "Consumo M (kg)", "Tipo Alimento", "Peso H (g)", "Peso M (g)", "Uniformidad H", "Uniformidad M", "Observaciones" }
            : new[] { "Fecha", "Mort H", "Mort M", "Sel H", "Sel M", "Consumo H (kg)", "Consumo M (kg)", "Huevo Total", "Huevo Incubable", "Peso Huevo (g)", "Etapa", "Observaciones" };
        PonerEncabezados(ws, headers);

        HojaInstrucciones(pkg, $"Migración Seguimiento {(esLevante ? "Levante" : "Producción")} — Lote {lote.LoteNombre} (id {loteId})",
            "Una fila por día en la hoja 'Datos'. Todas las filas corresponden a ESTE lote.",
            "• Fecha: obligatoria (formato aaaa-mm-dd o dd/mm/aaaa).",
            "• Mortalidad/Selección/Error de sexaje: enteros ≥ 0 (vacío = 0).",
            "• Consumo: en kg (acepta coma o punto decimal).",
            esLevante ? "• Peso/Uniformidad: opcionales." : "• Huevos: totales del día; Etapa: número de etapa.",
            "La carga es idempotente: reimportar el mismo archivo no duplica filas.");

        return (Finalizar(pkg), $"Seguimiento_{(esLevante ? "Levante" : "Produccion")}_Lote{loteId}_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    // ── Import ───────────────────────────────────────────────────────────────
    private async Task<MigracionResultDto> ProcesarSeguimientoLevanteAsync(IFormFile file, bool dryRun, int companyId, MigracionContextoDto ctx, CancellationToken ct)
    {
        const TipoMigracion tipo = TipoMigracion.SeguimientoLevante;
        if (ctx.LoteId is not int loteId) return ErrorContexto(tipo, dryRun, "Seleccioná un lote elegible antes de importar.");
        if (!await EsLoteElegibleAsync(tipo, companyId, loteId, ct)) return ErrorContexto(tipo, dryRun, "El lote no está habilitado para migración de Levante.");

        using var stream = file.OpenReadStream();
        var filas = LeerDatos(stream, "Datos");
        if (filas.Count == 0) return ResultadoVacio(tipo, dryRun);

        var errores = new List<MigracionErrorDto>();
        var filasJson = new List<Dictionary<string, object?>>();
        var fechasVistas = new HashSet<DateTime>();

        foreach (var fila in filas)
        {
            if (!MigracionCalculos.TryFecha(Celda(fila, "fecha"), out var fecha))
            { errores.Add(new(fila.Numero, "Fecha", null, "Fecha inválida o faltante.")); continue; }
            if (!fechasVistas.Add(fecha)) { errores.Add(new(fila.Numero, "Fecha", fecha.ToString("yyyy-MM-dd"), "Fecha repetida en el archivo.")); continue; }

            int e0 = errores.Count;
            var mortH = EnteroNoNeg(fila, errores, "Mort H", "mort h", "mortalidad hembras");
            var mortM = EnteroNoNeg(fila, errores, "Mort M", "mort m", "mortalidad machos");
            var selH = EnteroNoNeg(fila, errores, "Sel H", "sel h");
            var selM = EnteroNoNeg(fila, errores, "Sel M", "sel m");
            var errH = EnteroNoNeg(fila, errores, "Error Sexaje H", "error sexaje h");
            var errM = EnteroNoNeg(fila, errores, "Error Sexaje M", "error sexaje m");
            var consH = DecimalNoNeg(fila, errores, "Consumo H (kg)", "consumo h (kg)", "consumo h");
            var consM = DecimalNoNeg(fila, errores, "Consumo M (kg)", "consumo m (kg)", "consumo m");
            var pesoH = DobleOpc(fila, errores, "Peso H (g)", "peso h (g)", "peso h");
            var pesoM = DobleOpc(fila, errores, "Peso M (g)", "peso m (g)", "peso m");
            var unifH = DobleOpc(fila, errores, "Uniformidad H", "uniformidad h");
            var unifM = DobleOpc(fila, errores, "Uniformidad M", "uniformidad m");
            if (errores.Count > e0) continue;

            filasJson.Add(new Dictionary<string, object?>
            {
                ["lote_id"] = loteId,
                ["fecha"] = fecha.ToString("yyyy-MM-dd"),
                ["mort_h"] = mortH, ["mort_m"] = mortM, ["sel_h"] = selH, ["sel_m"] = selM,
                ["err_h"] = errH, ["err_m"] = errM,
                ["cons_h"] = consH, ["cons_m"] = consM,
                ["tipo_alimento"] = MigracionCalculos.TextoLimpio(Celda(fila, "tipo alimento")),
                ["peso_h"] = pesoH, ["peso_m"] = pesoM, ["unif_h"] = unifH, ["unif_m"] = unifM,
                ["observaciones"] = MigracionCalculos.TextoLimpio(Celda(fila, "observaciones"))
            });
        }

        return await EjecutarHistoricoAsync(tipo, dryRun, companyId, file.FileName, filas.Count, errores, filasJson,
            json => _ctx.Database.SqlQueryRaw<int>(
                "SELECT public.fn_migracion_seguimiento_levante({0}, {1}, {2}::jsonb) AS \"Value\"",
                companyId, _current.UserId.ToString(), json).FirstAsync(ct), ct);
    }

    private async Task<MigracionResultDto> ProcesarSeguimientoProduccionAsync(IFormFile file, bool dryRun, int companyId, MigracionContextoDto ctx, CancellationToken ct)
    {
        const TipoMigracion tipo = TipoMigracion.SeguimientoProduccion;
        if (ctx.LoteId is not int loteId) return ErrorContexto(tipo, dryRun, "Seleccioná un lote elegible antes de importar.");
        if (!await EsLoteElegibleAsync(tipo, companyId, loteId, ct)) return ErrorContexto(tipo, dryRun, "El lote no está habilitado (requiere Levante cerrado + liquidado + lote Producción).");

        using var stream = file.OpenReadStream();
        var filas = LeerDatos(stream, "Datos");
        if (filas.Count == 0) return ResultadoVacio(tipo, dryRun);

        var errores = new List<MigracionErrorDto>();
        var filasJson = new List<Dictionary<string, object?>>();
        var fechasVistas = new HashSet<DateTime>();

        foreach (var fila in filas)
        {
            if (!MigracionCalculos.TryFecha(Celda(fila, "fecha"), out var fecha))
            { errores.Add(new(fila.Numero, "Fecha", null, "Fecha inválida o faltante.")); continue; }
            if (!fechasVistas.Add(fecha)) { errores.Add(new(fila.Numero, "Fecha", fecha.ToString("yyyy-MM-dd"), "Fecha repetida en el archivo.")); continue; }

            int e0 = errores.Count;
            var mortH = EnteroNoNeg(fila, errores, "Mort H", "mort h", "mortalidad hembras");
            var mortM = EnteroNoNeg(fila, errores, "Mort M", "mort m", "mortalidad machos");
            var selH = EnteroNoNeg(fila, errores, "Sel H", "sel h");
            var selM = EnteroNoNeg(fila, errores, "Sel M", "sel m");
            var consH = DecimalNoNeg(fila, errores, "Consumo H (kg)", "consumo h (kg)", "consumo h");
            var consM = DecimalNoNeg(fila, errores, "Consumo M (kg)", "consumo m (kg)", "consumo m");
            var huevoTot = EnteroNoNeg(fila, errores, "Huevo Total", "huevo total");
            var huevoInc = EnteroNoNeg(fila, errores, "Huevo Incubable", "huevo incubable");
            var pesoHuevo = DobleOpc(fila, errores, "Peso Huevo (g)", "peso huevo (g)", "peso huevo");
            var etapa = EnteroNoNeg(fila, errores, "Etapa", "etapa");
            if (errores.Count > e0) continue;

            filasJson.Add(new Dictionary<string, object?>
            {
                ["lote_id"] = loteId,
                ["fecha"] = fecha.ToString("yyyy-MM-dd"),
                ["mort_h"] = mortH, ["mort_m"] = mortM, ["sel_h"] = selH, ["sel_m"] = selM,
                ["err_h"] = 0, ["err_m"] = 0,
                ["cons_h"] = consH, ["cons_m"] = consM,
                ["huevo_tot"] = huevoTot, ["huevo_inc"] = huevoInc, ["peso_huevo"] = pesoHuevo,
                ["etapa"] = etapa == 0 ? 1 : etapa,
                ["observaciones"] = MigracionCalculos.TextoLimpio(Celda(fila, "observaciones"))
            });
        }

        return await EjecutarHistoricoAsync(tipo, dryRun, companyId, file.FileName, filas.Count, errores, filasJson,
            json => _ctx.Database.SqlQueryRaw<int>(
                "SELECT public.fn_migracion_seguimiento_produccion({0}, {1}, {2}::jsonb) AS \"Value\"",
                companyId, _current.UserId, json).FirstAsync(ct), ct);
    }

    // ── Runner de histórico (valida → dry-run corta → invoca función BD) ─────
    private async Task<MigracionResultDto> EjecutarHistoricoAsync(
        TipoMigracion tipo, bool dryRun, int companyId, string nombreArchivo,
        int total, List<MigracionErrorDto> errores, List<Dictionary<string, object?>> filasJson,
        Func<string, Task<int>> invocarFn, CancellationToken ct)
    {
        if (total == 0) return ResultadoVacio(tipo, dryRun);
        if (errores.Count > 0)
        {
            if (!dryRun)
                await RegistrarAuditoriaAsync(tipo, companyId, nombreArchivo, total, 0,
                    errores.Select(e => e.Fila).Where(f => f > 0).Distinct().Count(), "ConErrores", SerializarErrores(errores), ct);
            return ResultadoConErrores(tipo, dryRun, total, errores);
        }
        if (dryRun) return ResultadoOk(tipo, dryRun, total);

        int insertados;
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(filasJson);
            insertados = await invocarFn(json);
        }
        catch (Exception ex)
        {
            var err = new[] { new MigracionErrorDto(0, "-", null, $"Error al insertar: {ex.Message}") };
            await RegistrarAuditoriaAsync(tipo, companyId, nombreArchivo, total, 0, total, "Fallido", SerializarErrores(err), ct);
            return ResultadoConErrores(tipo, dryRun, total, err);
        }
        await RegistrarAuditoriaAsync(tipo, companyId, nombreArchivo, total, insertados, 0, "Procesado", null, ct);
        return new MigracionResultDto(tipo.ToString(), true, total, insertados, 0, "Procesado", dryRun, Array.Empty<MigracionErrorDto>());
    }

    // ── Helpers de celda numérica ────────────────────────────────────────────
    private static int EnteroNoNeg(FilaCruda fila, List<MigracionErrorDto> errores, string etiqueta, params string[] headers)
    {
        var cell = Celda(fila, headers);
        if (MigracionCalculos.EsVacia(cell)) return 0;
        if (!MigracionCalculos.TryEntero(cell, out var v) || v < 0)
        { errores.Add(new(fila.Numero, etiqueta, MigracionCalculos.TextoLimpio(cell), $"{etiqueta}: se esperaba un entero ≥ 0.")); return 0; }
        return v;
    }

    private static decimal? DecimalNoNeg(FilaCruda fila, List<MigracionErrorDto> errores, string etiqueta, params string[] headers)
    {
        var cell = Celda(fila, headers);
        if (MigracionCalculos.EsVacia(cell)) return null;
        if (!MigracionCalculos.TryDecimal(cell, out var v) || v < 0)
        { errores.Add(new(fila.Numero, etiqueta, MigracionCalculos.TextoLimpio(cell), $"{etiqueta}: se esperaba un número ≥ 0.")); return null; }
        return v;
    }

    private static double? DobleOpc(FilaCruda fila, List<MigracionErrorDto> errores, string etiqueta, params string[] headers)
    {
        var cell = Celda(fila, headers);
        if (MigracionCalculos.EsVacia(cell)) return null;
        if (!MigracionCalculos.TryDecimal(cell, out var v))
        { errores.Add(new(fila.Numero, etiqueta, MigracionCalculos.TextoLimpio(cell), $"{etiqueta}: número inválido.")); return null; }
        return (double)v;
    }

    private static MigracionResultDto ErrorContexto(TipoMigracion tipo, bool dryRun, string mensaje)
        => new(tipo.ToString(), false, 0, 0, 0, "ConErrores", dryRun, new[] { new MigracionErrorDto(0, "Lote", null, mensaje) });
}
