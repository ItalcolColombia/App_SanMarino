// src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.SeguimientoEngorde.cs
// Línea Engorde · Seguimiento diario. Elegibilidad (lotes LoteAveEngorde no cerrados) + plantilla por
// lote + parse/validación en C#. La INSERCIÓN reutiliza ISeguimientoAvesEngordeService.CreateAsync por
// fila (decisión: replicar todos los efectos vivos — retiro de InventarioAves + recálculo de saldo;
// el descuento de inventario de alimento solo aplica si la fila trae ítems de catálogo, que la plantilla
// histórica no incluye). Idempotente: omite fechas ya cargadas (contadas en FilasOmitidas, F2). Sin
// transacción externa para no anidar con la transacción propia de la ruta Colombia (modelo-B).
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Migracion;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MigracionService
{
    // ── Elegibilidad ─────────────────────────────────────────────────────────
    private async Task<IReadOnlyList<LoteElegibleDto>> ElegiblesEngordeAsync(int companyId, MigracionContextoDto ctx, CancellationToken ct)
    {
        var q = _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.LoteAveEngordeId != null
                        && l.EstadoOperativoLote != "Cerrado");
        if (ctx.GranjaId is int g) q = q.Where(l => l.GranjaId == g);
        if (!string.IsNullOrWhiteSpace(ctx.NucleoId)) q = q.Where(l => l.NucleoId == ctx.NucleoId);
        if (!string.IsNullOrWhiteSpace(ctx.GalponId)) q = q.Where(l => l.GalponId == ctx.GalponId);

        return await q.OrderBy(l => l.LoteNombre)
            .Select(l => new LoteElegibleDto(l.LoteAveEngordeId!.Value, l.LoteNombre, l.GranjaId, l.NucleoId, l.GalponId, "Engorde", l.EstadoOperativoLote))
            .ToListAsync(ct);
    }

    // Devuelve el lote de engorde si existe en la empresa y no está cerrado (o null + mensaje de por qué).
    private async Task<(LoteAveEngorde? Lote, string? Error)> ResolverLoteEngordeAsync(int companyId, int loteId, CancellationToken ct)
    {
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .FirstOrDefaultAsync(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null, ct);
        if (lote is null) return (null, "El lote de engorde no existe en la empresa.");
        if (lote.EstadoOperativoLote == "Cerrado") return (null, $"El lote {lote.LoteNombre} está cerrado; no admite carga de seguimiento.");
        return (lote, null);
    }

    // ── Import ───────────────────────────────────────────────────────────────
    private async Task<MigracionResultDto> ProcesarSeguimientoEngordeAsync(IFormFile file, bool dryRun, bool permitirParcial, int companyId, MigracionContextoDto ctx, CancellationToken ct)
    {
        const TipoMigracion tipo = TipoMigracion.SeguimientoPolloEngorde;
        if (ctx.LoteId is not int loteId) return ErrorContexto(tipo, dryRun, "Seleccioná un lote de engorde antes de importar.");
        var (lote, errLote) = await ResolverLoteEngordeAsync(companyId, loteId, ct);
        if (lote is null) return ErrorContexto(tipo, dryRun, errLote!);

        var errores = new List<MigracionErrorDto>();
        using var stream = file.OpenReadStream();
        var filas = LeerDatosConEsquema(stream, MigracionEsquemas.Para(tipo), errores);
        if (errores.Any(e => e.Severidad == "Error")) return ResultadoConErrores(tipo, dryRun, filas.Count, errores);
        if (filas.Count == 0 && errores.Count == 0) return ResultadoVacio(tipo, dryRun);

        // Fechas ya cargadas para este lote (idempotencia: se omiten; incluye filas origen_cruce de días 1-7).
        var existentes = (await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
                .Where(s => s.LoteAveEngordeId == loteId)
                .Select(s => s.Fecha).ToListAsync(ct))
            .Select(f => f.Date).ToHashSet();

        var dtos = new List<SeguimientoLoteLevanteDto>();
        var fechasVistas = new HashSet<DateTime>();
        int omitidas = 0;

        foreach (var fila in filas)
        {
            if (!MigracionCalculos.TryFecha(Celda(fila, "fecha"), out var fecha))
            { errores.Add(new(fila.Numero, "Fecha", null, "Fecha inválida o faltante.")); continue; }
            if (!fechasVistas.Add(fecha.Date)) { errores.Add(new(fila.Numero, "Fecha", fecha.ToString("yyyy-MM-dd"), "Fecha repetida en el archivo.")); continue; }
            if (existentes.Contains(fecha.Date)) { omitidas++; continue; } // ya existe → idempotente, se omite

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

            var req = new CreateSeguimientoLoteLevanteRequest
            {
                LoteId = loteId,
                // Kind=Utc: el servicio asigna Fecha directo a una columna timestamptz (Npgsql exige Utc).
                FechaRegistro = DateTime.SpecifyKind(fecha.Date, DateTimeKind.Utc),
                MortalidadHembras = mortH,
                MortalidadMachos = mortM,
                SelH = selH,
                SelM = selM,
                ErrorSexajeHembras = errH,
                ErrorSexajeMachos = errM,
                TipoAlimento = MigracionCalculos.TextoLimpio(Celda(fila, "tipo alimento")) ?? string.Empty,
                ConsumoKgHembrasDirecto = (double?)consH,
                ConsumoKgMachosDirecto = (double?)consM,
                PesoPromH = pesoH,
                PesoPromM = pesoM,
                UniformidadH = unifH,
                UniformidadM = unifM,
                Observaciones = MigracionCalculos.TextoLimpio(Celda(fila, "observaciones")),
                Ciclo = "Normal",
                CreatedByUserId = _current.UserId.ToString()
            };
            dtos.Add(req.ToDto());
        }

        return await EjecutarSeguimientoEngordeAsync(tipo, dryRun, permitirParcial, file.FileName, filas.Count, omitidas, errores, dtos, ct);
    }

    // ── Runner (valida → dry-run corta → CreateAsync fila por fila, sin TX externa, parcial opt-in) ─
    private async Task<MigracionResultDto> EjecutarSeguimientoEngordeAsync(
        TipoMigracion tipo, bool dryRun, bool permitirParcial, string nombreArchivo,
        int total, int omitidas, List<MigracionErrorDto> errores, List<SeguimientoLoteLevanteDto> dtos, CancellationToken ct)
    {
        if (total == 0 && errores.Count == 0) return ResultadoVacio(tipo, dryRun);

        var hayErroresReales = errores.Any(e => e.Severidad == "Error");
        var puedeInsertarParcial = hayErroresReales && !dryRun && permitirParcial && dtos.Count > 0;

        if (hayErroresReales && !puedeInsertarParcial)
            return ResultadoConErrores(tipo, dryRun, total, errores) with { FilasOmitidas = omitidas };

        if (dryRun) return ResultadoOk(tipo, dryRun, total, errores) with { FilasOmitidas = omitidas };

        int insertados = 0;
        var fallos = new List<MigracionErrorDto>();
        foreach (var dto in dtos)
        {
            try { await _seguimientoEngordeService.CreateAsync(dto); insertados++; }
            catch (Exception ex)
            { fallos.Add(new(0, "Fecha", dto.FechaRegistro.ToString("yyyy-MM-dd"), $"Error al insertar: {ex.Message}")); }
        }

        var filasErrorValidacion = errores.Where(e => e.Severidad == "Error" && e.Fila > 0).Select(e => e.Fila).Distinct().Count();

        if (fallos.Count > 0)
        {
            var combinados = errores.Concat(fallos).ToList();
            var (capados, totalReal) = MigracionEsquemaCalculos.LimitarErrores(combinados, MaxErroresReportados);
            return new MigracionResultDto(tipo.ToString(), insertados > 0, total, insertados, filasErrorValidacion + fallos.Count, "ConErrores", dryRun, capados, omitidas, 0, totalReal);
        }

        var (capadosOk, totalRealOk) = MigracionEsquemaCalculos.LimitarErrores(errores, MaxErroresReportados);
        var estado = puedeInsertarParcial ? "ProcesadoParcial" : "Procesado";
        return new MigracionResultDto(tipo.ToString(), true, total, insertados, puedeInsertarParcial ? filasErrorValidacion : 0, estado, dryRun, capadosOk, omitidas, 0, totalRealOk);
    }

    // ── Plantilla ────────────────────────────────────────────────────────────
    private async Task<(byte[] Contenido, string NombreArchivo)> GenerarPlantillaSeguimientoEngordeAsync(int companyId, MigracionContextoDto ctx, CancellationToken ct)
    {
        if (ctx.LoteId is not int loteId)
            throw new ArgumentException("Seleccioná un lote de engorde para descargar su plantilla.");
        var (lote, errLote) = await ResolverLoteEngordeAsync(companyId, loteId, ct);
        if (lote is null) throw new InvalidOperationException(errLote!);

        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Datos");
        PonerEncabezados(ws, MigracionEsquemas.SeguimientoPolloEngorde);

        HojaInstrucciones(pkg, $"Migración Seguimiento Engorde — Lote {lote.LoteNombre} (id {loteId})",
            "Una fila por día en la hoja 'Datos'. Todas las filas corresponden a ESTE lote.",
            "• Fecha: obligatoria (aaaa-mm-dd o dd/mm/aaaa).",
            "• Mortalidad / Selección / Error de sexaje: enteros ≥ 0 (vacío = 0).",
            "• Consumo H/M: en kg (acepta coma o punto decimal). Peso/Uniformidad: opcionales.",
            "La carga es idempotente: las fechas ya cargadas (incluidos los primeros días generados por cruce reproductora) se omiten.",
            "Al importar se registra el retiro de aves por mortalidad/selección y se recalcula el saldo de alimento del lote.");

        return (Finalizar(pkg), $"SeguimientoEngorde_Lote{loteId}_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
