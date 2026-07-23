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
        var hoyUtc = DateTime.UtcNow.Date;

        foreach (var fila in filas)
        {
            if (!MigracionCalculos.TryFecha(Celda(fila, "fecha"), out var fecha))
            { errores.Add(new(fila.Numero, "Fecha", null, "Fecha inválida o faltante.")); continue; }
            if (!fechasVistas.Add(fecha.Date)) { errores.Add(new(fila.Numero, "Fecha", fecha.ToString("yyyy-MM-dd"), "Fecha repetida en el archivo.")); continue; }
            if (existentes.Contains(fecha.Date)) { omitidas++; continue; } // ya existe → idempotente, se omite

            // Regla de fecha (alineada al front): nunca anterior al encaset del lote; futura solo advierte.
            if (lote.FechaEncaset.HasValue && fecha.Date < lote.FechaEncaset.Value.Date)
            { errores.Add(new(fila.Numero, "Fecha", fecha.ToString("yyyy-MM-dd"), $"La fecha es anterior al encaset del lote ({lote.FechaEncaset.Value:yyyy-MM-dd}).")); continue; }
            if (fecha.Date > hoyUtc)
                errores.Add(new(fila.Numero, "Fecha", fecha.ToString("yyyy-MM-dd"), "La fecha es futura; verificá que sea intencional.", "Advertencia"));

            int e0 = errores.Count;
            var mortH = EnteroNoNeg(fila, errores, "Mort H", "mort h", "mortalidad hembras");
            var mortM = EnteroNoNeg(fila, errores, "Mort M", "mort m", "mortalidad machos");
            var selH = EnteroNoNeg(fila, errores, "Sel H", "sel h");
            var selM = EnteroNoNeg(fila, errores, "Sel M", "sel m");
            var errH = EnteroNoNeg(fila, errores, "Error Sexaje H", "error sexaje h");
            var errM = EnteroNoNeg(fila, errores, "Error Sexaje M", "error sexaje m");
            var consH = DecimalNoNeg(fila, errores, "Consumo H (kg)", "consumo h (kg)", "consumo h");
            var consM = DecimalNoNeg(fila, errores, "Consumo M (kg)", "consumo m (kg)", "consumo m");
            var unidadConsumo = LeerUnidadConsumo(fila, errores);
            var pesoH = DobleNoNeg(fila, errores, "Peso H (g)", "peso h (g)", "peso h");
            var pesoM = DobleNoNeg(fila, errores, "Peso M (g)", "peso m (g)", "peso m");
            var unifH = Porcentaje0a100(fila, errores, "Uniformidad H", "uniformidad h");
            var unifM = Porcentaje0a100(fila, errores, "Uniformidad M", "uniformidad m");
            // Panamá: quintales por categoría (opcionales; persisten en qq_* para el informe semanal).
            var qqMix = DecimalNoNeg(fila, errores, "QQ Mixtas", "qq mixtas", "quintales mixtas");
            var qqH = DecimalNoNeg(fila, errores, "QQ H", "qq h", "qq hembras", "quintales hembras");
            var qqM = DecimalNoNeg(fila, errores, "QQ M", "qq m", "qq machos", "quintales machos");
            if (errores.Count > e0) continue;

            // Unidad Consumo "qq" → convertir el consumo H/M a kg (×45.36, mismo redondeo que el front).
            consH = MigracionCalculos.ConsumoAKilos(consH, unidadConsumo);
            consM = MigracionCalculos.ConsumoAKilos(consM, unidadConsumo);

            // Día de pesaje obligatorio (espejo del modal: edad 1–7 y múltiplos de 7). En carga histórica
            // no bloquea (Advertencia): el modal sí lo exige al capturar el día a día.
            if (lote.FechaEncaset.HasValue && pesoH is null && pesoM is null)
            {
                var edad = (int)(fecha.Date - lote.FechaEncaset.Value.Date).TotalDays;
                if ((edad >= 1 && edad <= 7) || (edad > 7 && edad % 7 == 0))
                    errores.Add(new(fila.Numero, "Peso H (g)", fecha.ToString("yyyy-MM-dd"),
                        $"Día {edad} (edad 1–7 o múltiplo de 7): es día de pesaje obligatorio y la fila no trae peso.", "Advertencia"));
            }

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
                QqMixtas = qqMix,
                QqHembras = qqH,
                QqMachos = qqM,
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
            "• Fecha: obligatoria (aaaa-mm-dd o dd/mm/aaaa), no anterior al encaset del lote. Fecha futura solo advierte.",
            "• Mortalidad / Selección / Error de sexaje: enteros ≥ 0 (vacío = 0).",
            "• Consumo H/M: número ≥ 0 (acepta coma o punto decimal). Peso: ≥ 0 opcional. Uniformidad: 0 a 100 opcional.",
            "• Unidad Consumo: 'kg' (default si se deja vacía) o 'qq' — con 'qq' el Consumo H/M se convierte automáticamente a kg (1 qq = 45.36 kg).",
            "• Días de pesaje (edad 1–7 y múltiplos de 7): si la fila no trae peso se genera una advertencia (no bloquea).",
            "• Lotes MIXTOS (Panamá): cargá las cantidades en las columnas H (M = 0), igual que el formulario.",
            "• QQ Mixtas / QQ H / QQ M (Panamá): quintales de alimento por categoría, opcionales (≥ 0).",
            "La carga es idempotente: las fechas ya cargadas (incluidos los primeros días generados por cruce reproductora) se omiten.",
            "Al importar se registra el retiro de aves por mortalidad/selección y se recalcula el saldo de alimento del lote.");

        return (Finalizar(pkg), $"SeguimientoEngorde_Lote{loteId}_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
