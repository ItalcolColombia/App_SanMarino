// src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.SeguimientoReproductora.cs
// Línea Engorde · Seguimiento reproductora (primera semana). El contexto fija el LOTE ENGORDE y la
// columna "Reproductora" del Excel identifica el lote reproductora (id/código/nombre) dentro de él.
// La INSERCIÓN reutiliza ISeguimientoDiarioLoteReproductoraService.CreateAsync (anclaje mediodía UTC,
// validación compañía/7 días/edad, descuento inventario si hubiera ítems) y luego ConfirmarAsync:
// en carga masiva cada registro queda CONFIRMADO automáticamente ("aceptación de una"), que es lo que
// dispara el trigger de cruce hacia el seguimiento pollo engorde (fn_cruce solo cuenta confirmados).
// Idempotente: fechas ya cargadas por reproductora se omiten (FilasOmitidas).
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Migracion;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MigracionService
{
    /// <summary>Datos mínimos de un lote reproductora para resolver la columna "Reproductora" y validar fechas.</summary>
    private sealed record ReproductoraElegible(int Id, string ReproductoraId, string? Codigo, string Nombre, DateTime? FechaEncasetamiento);

    // ── Elegibilidad ─────────────────────────────────────────────────────────
    // Mismo criterio que Engorde (lotes no cerrados de la empresa) pero solo lotes que ya tienen
    // al menos un lote reproductora asociado (sin reproductoras no hay nada que cargar).
    private async Task<IReadOnlyList<LoteElegibleDto>> ElegiblesReproductoraEngordeAsync(int companyId, MigracionContextoDto ctx, CancellationToken ct)
    {
        var q = _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.LoteAveEngordeId != null
                        && l.EstadoOperativoLote != "Cerrado"
                        && _ctx.LoteReproductoraAveEngorde.Any(r => (int?)r.LoteAveEngordeId == l.LoteAveEngordeId));
        if (ctx.GranjaId is int g) q = q.Where(l => l.GranjaId == g);
        if (!string.IsNullOrWhiteSpace(ctx.NucleoId)) q = q.Where(l => l.NucleoId == ctx.NucleoId);
        if (!string.IsNullOrWhiteSpace(ctx.GalponId)) q = q.Where(l => l.GalponId == ctx.GalponId);

        return await q.OrderBy(l => l.LoteNombre)
            .Select(l => new LoteElegibleDto(l.LoteAveEngordeId!.Value, l.LoteNombre, l.GranjaId, l.NucleoId, l.GalponId, "Engorde", l.EstadoOperativoLote))
            .ToListAsync(ct);
    }

    /// <summary>Lotes reproductora del lote engorde, con índice por clave normalizada (id / código / nombre).</summary>
    private async Task<(List<ReproductoraElegible> Repros, Dictionary<string, List<ReproductoraElegible>> PorClave)> CargarReproductorasAsync(int loteId, CancellationToken ct)
    {
        var repros = await _ctx.LoteReproductoraAveEngorde.AsNoTracking()
            .Where(r => r.LoteAveEngordeId == loteId)
            .OrderBy(r => r.NombreLote)
            .Select(r => new ReproductoraElegible(r.Id, r.ReproductoraId, r.CodigoReproductora, r.NombreLote, r.FechaEncasetamiento))
            .ToListAsync(ct);

        var porClave = new Dictionary<string, List<ReproductoraElegible>>();
        void Indexar(string? clave, ReproductoraElegible repro)
        {
            var k = MigracionCalculos.NormalizarClave(clave);
            if (string.IsNullOrEmpty(k)) return;
            if (!porClave.TryGetValue(k, out var lista)) porClave[k] = lista = new List<ReproductoraElegible>();
            if (!lista.Any(x => x.Id == repro.Id)) lista.Add(repro);
        }
        foreach (var r in repros)
        {
            Indexar(r.ReproductoraId, r);
            Indexar(r.Codigo, r);
            Indexar(r.Nombre, r);
        }
        return (repros, porClave);
    }

    // ── Import ───────────────────────────────────────────────────────────────
    private async Task<MigracionResultDto> ProcesarSeguimientoReproductoraAsync(IFormFile file, bool dryRun, bool permitirParcial, int companyId, MigracionContextoDto ctx, CancellationToken ct)
    {
        const TipoMigracion tipo = TipoMigracion.SeguimientoReproductoraEngorde;
        const int MaxDias = ReproductoraEngordeCalculos.DiasRecogidaReproductora;

        if (ctx.LoteId is not int loteId) return ErrorContexto(tipo, dryRun, "Seleccioná un lote de engorde antes de importar.");
        var (lote, errLote) = await ResolverLoteEngordeAsync(companyId, loteId, ct);
        if (lote is null) return ErrorContexto(tipo, dryRun, errLote!);

        var (repros, porClave) = await CargarReproductorasAsync(loteId, ct);
        if (repros.Count == 0)
            return ErrorContexto(tipo, dryRun, $"El lote {lote.LoteNombre} no tiene lotes reproductora asociados.");

        var errores = new List<MigracionErrorDto>();
        using var stream = file.OpenReadStream();
        var filas = LeerDatosConEsquema(stream, MigracionEsquemas.Para(tipo), errores);
        if (errores.Any(e => e.Severidad == "Error")) return ResultadoConErrores(tipo, dryRun, filas.Count, errores);
        if (filas.Count == 0 && errores.Count == 0) return ResultadoVacio(tipo, dryRun);

        // Fechas ya cargadas por reproductora (idempotencia) + conteo para el tope de 7 días.
        var reproIds = repros.Select(r => r.Id).ToList();
        var existentesPorRepro = (await _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.AsNoTracking()
                .Where(s => reproIds.Contains(s.LoteReproductoraAveEngordeId))
                .Select(s => new { s.LoteReproductoraAveEngordeId, s.Fecha })
                .ToListAsync(ct))
            .GroupBy(x => x.LoteReproductoraAveEngordeId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Fecha.Date).ToHashSet());

        var dtos = new List<SeguimientoLoteLevanteDto>();
        var fechasVistasPorRepro = new Dictionary<int, HashSet<DateTime>>();
        var nuevosPorRepro = new Dictionary<int, int>();
        int omitidas = 0;

        foreach (var fila in filas)
        {
            // Reproductora: obligatoria y debe resolver a UN lote reproductora del lote seleccionado.
            var reproTexto = MigracionCalculos.TextoLimpio(Celda(fila, "reproductora", "reproductora id", "repro", "codigo reproductora"));
            var reproClave = MigracionCalculos.NormalizarClave(reproTexto);
            if (string.IsNullOrEmpty(reproClave))
            { errores.Add(new(fila.Numero, "Reproductora", null, "Reproductora: obligatoria (id, código o nombre del lote reproductora).")); continue; }
            if (!porClave.TryGetValue(reproClave, out var candidatas))
            { errores.Add(new(fila.Numero, "Reproductora", reproTexto, $"La reproductora '{reproTexto}' no existe en el lote {lote.LoteNombre}.")); continue; }
            if (candidatas.Count > 1)
            { errores.Add(new(fila.Numero, "Reproductora", reproTexto, $"'{reproTexto}' es ambigua en el lote (coincide con {candidatas.Count} reproductoras); usá el id o el código.")); continue; }
            var repro = candidatas[0];

            if (!MigracionCalculos.TryFecha(Celda(fila, "fecha"), out var fecha))
            { errores.Add(new(fila.Numero, "Fecha", null, "Fecha inválida o faltante.")); continue; }

            var vistas = fechasVistasPorRepro.TryGetValue(repro.Id, out var setV)
                ? setV : fechasVistasPorRepro[repro.Id] = new HashSet<DateTime>();
            if (!vistas.Add(fecha.Date))
            { errores.Add(new(fila.Numero, "Fecha", fecha.ToString("yyyy-MM-dd"), $"Fecha repetida en el archivo para la reproductora {repro.Nombre}.")); continue; }
            if (existentesPorRepro.TryGetValue(repro.Id, out var existentes) && existentes.Contains(fecha.Date))
            { omitidas++; continue; } // ya existe → idempotente, se omite

            // Regla de fecha (espejo del servicio y del modal): edad [1, 7] respecto al encasetamiento.
            if (repro.FechaEncasetamiento.HasValue)
            {
                var edad = ReproductoraEngordeCalculos.EdadSeguimientoDias(repro.FechaEncasetamiento.Value, fecha);
                if (!ReproductoraEngordeCalculos.EsEdadSeguimientoValida(edad, MaxDias))
                {
                    errores.Add(new(fila.Numero, "Fecha", fecha.ToString("yyyy-MM-dd"),
                        edad < 1
                            ? $"{repro.Nombre}: la fecha no puede ser anterior al día siguiente del encasetamiento ({repro.FechaEncasetamiento:yyyy-MM-dd})."
                            : $"{repro.Nombre}: la fecha supera los {MaxDias} días de edad permitidos desde el encasetamiento ({repro.FechaEncasetamiento:yyyy-MM-dd})."));
                    continue;
                }
            }

            // Tope de 7 días por reproductora: registros ya en BD + nuevos aceptados del archivo.
            var yaRegistrados = existentesPorRepro.TryGetValue(repro.Id, out var setE) ? setE.Count : 0;
            var nuevos = nuevosPorRepro.GetValueOrDefault(repro.Id);
            if (yaRegistrados + nuevos >= MaxDias)
            { errores.Add(new(fila.Numero, "Fecha", fecha.ToString("yyyy-MM-dd"), $"{repro.Nombre}: superaría los {MaxDias} días de seguimiento (ya tiene {yaRegistrados} registrados).")); continue; }

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
            var cvH = DobleNoNeg(fila, errores, "CV H", "cv h", "cv hembras");
            var cvM = DobleNoNeg(fila, errores, "CV M", "cv m", "cv machos");
            if (errores.Count > e0) continue;

            // Unidad Consumo "qq" → convertir el consumo H/M a kg (×45.36, mismo redondeo que el front).
            consH = MigracionCalculos.ConsumoAKilos(consH, unidadConsumo);
            consM = MigracionCalculos.ConsumoAKilos(consM, unidadConsumo);

            nuevosPorRepro[repro.Id] = nuevos + 1;

            // Mismo request que usa el endpoint del front; el servicio ancla la fecha a mediodía UTC.
            var req = new CreateSeguimientoDiarioLoteReproductoraRequest
            {
                LoteId = repro.Id,
                FechaRegistro = DateTime.SpecifyKind(fecha.Date, DateTimeKind.Utc),
                MortalidadHembras = mortH,
                MortalidadMachos = mortM,
                SelH = selH,
                SelM = selM,
                ErrorSexajeHembras = errH,
                ErrorSexajeMachos = errM,
                TipoAlimento = MigracionCalculos.TextoLimpio(Celda(fila, "tipo alimento")) ?? string.Empty,
                ConsumoHembras = (double?)consH,
                UnidadConsumoHembras = "kg",
                ConsumoMachos = (double?)consM,
                UnidadConsumoMachos = "kg",
                PesoPromH = pesoH,
                PesoPromM = pesoM,
                UniformidadH = unifH,
                UniformidadM = unifM,
                CvH = cvH,
                CvM = cvM,
                Observaciones = MigracionCalculos.TextoLimpio(Celda(fila, "observaciones")),
                Ciclo = "Normal",
                CreatedByUserId = _current.UserId.ToString()
            };
            dtos.Add(req.ToDto());
        }

        // Orden estable: por reproductora y fecha ascendente (el cruce consolida por edad).
        var ordenados = dtos.OrderBy(d => d.LoteId).ThenBy(d => d.FechaRegistro).ToList();
        return await EjecutarSeguimientoReproductoraAsync(tipo, dryRun, permitirParcial, filas.Count, omitidas, errores, ordenados);
    }

    // ── Runner (valida → dry-run corta → CreateAsync + ConfirmarAsync fila por fila, parcial opt-in) ─
    // La confirmación automática es la diferencia con la línea de seguimiento engorde: cada registro
    // cargado queda confirmado (es lo que gatea el cruce a pollo engorde). ConfirmarAsync es idempotente.
    private async Task<MigracionResultDto> EjecutarSeguimientoReproductoraAsync(
        TipoMigracion tipo, bool dryRun, bool permitirParcial,
        int total, int omitidas, List<MigracionErrorDto> errores, List<SeguimientoLoteLevanteDto> dtos)
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
            try
            {
                var creado = await _seguimientoReproductoraService.CreateAsync(dto);
                await _seguimientoReproductoraService.ConfirmarAsync(creado.Id);
                insertados++;
            }
            catch (Exception ex)
            { fallos.Add(new(0, "Fecha", dto.FechaRegistro.ToString("yyyy-MM-dd"), $"Error al insertar/confirmar: {ex.Message}")); }
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
    private async Task<(byte[] Contenido, string NombreArchivo)> GenerarPlantillaSeguimientoReproductoraAsync(int companyId, MigracionContextoDto ctx, CancellationToken ct)
    {
        const int MaxDias = ReproductoraEngordeCalculos.DiasRecogidaReproductora;
        if (ctx.LoteId is not int loteId)
            throw new ArgumentException("Seleccioná un lote de engorde para descargar su plantilla.");
        var (lote, errLote) = await ResolverLoteEngordeAsync(companyId, loteId, ct);
        if (lote is null) throw new InvalidOperationException(errLote!);

        var (repros, _) = await CargarReproductorasAsync(loteId, ct);
        if (repros.Count == 0)
            throw new InvalidOperationException($"El lote {lote.LoteNombre} no tiene lotes reproductora asociados.");

        // Estado actual por reproductora (cargados/confirmados) para orientar la carga.
        var reproIds = repros.Select(r => r.Id).ToList();
        var estado = (await _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.AsNoTracking()
                .Where(s => reproIds.Contains(s.LoteReproductoraAveEngordeId))
                .Select(s => new { s.LoteReproductoraAveEngordeId, s.Confirmado })
                .ToListAsync(ct))
            .GroupBy(x => x.LoteReproductoraAveEngordeId)
            .ToDictionary(g => g.Key, g => (Total: g.Count(), Confirmados: g.Count(x => x.Confirmado)));

        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Datos");
        PonerEncabezados(ws, MigracionEsquemas.SeguimientoReproductoraEngorde);

        var lineas = new List<string>
        {
            "Una fila por reproductora y día en la hoja 'Datos'. Todas las filas corresponden a ESTE lote de engorde.",
            "• Reproductora: obligatoria — id, código o nombre del lote reproductora (ver lista abajo).",
            $"• Fecha: obligatoria (aaaa-mm-dd o dd/mm/aaaa), dentro de la PRIMERA SEMANA: edad 1 a {MaxDias} días desde el encasetamiento de la reproductora.",
            "• Mortalidad / Selección / Error de sexaje: enteros ≥ 0 (vacío = 0). Consumo H/M: número ≥ 0 (acepta coma o punto).",
            "• Unidad Consumo: 'kg' (default si se deja vacía) o 'qq' — con 'qq' el Consumo H/M se convierte automáticamente a kg (1 qq = 45.36 kg).",
            "• Peso (g) y CV: ≥ 0 opcionales. Uniformidad: 0 a 100 opcional.",
            $"• Máximo {MaxDias} días de seguimiento por reproductora (incluye los ya registrados en el sistema).",
            "La carga es idempotente: las fechas ya registradas de cada reproductora se omiten.",
            "IMPORTANTE: cada registro importado queda CONFIRMADO automáticamente — al completar todos los lotes",
            "reproductora de un día, ese día cruza al seguimiento del lote pollo engorde (igual que confirmar en pantalla).",
            "",
            $"Reproductoras del lote {lote.LoteNombre}:"
        };
        foreach (var r in repros)
        {
            var (tot, conf) = estado.TryGetValue(r.Id, out var st) ? st : (0, 0);
            var rango = r.FechaEncasetamiento.HasValue
                ? $"fechas válidas {r.FechaEncasetamiento.Value.Date.AddDays(1):yyyy-MM-dd} a {r.FechaEncasetamiento.Value.Date.AddDays(MaxDias):yyyy-MM-dd}"
                : "sin fecha de encasetamiento registrada";
            var codigo = string.IsNullOrWhiteSpace(r.Codigo) ? "" : $", código {r.Codigo}";
            lineas.Add($"• {r.Nombre} (id {r.ReproductoraId}{codigo}) — {rango} — cargados {tot}/{MaxDias} ({conf} confirmados).");
        }

        HojaInstrucciones(pkg, $"Migración Seguimiento Reproductora Engorde — Lote {lote.LoteNombre} (id {loteId})", lineas.ToArray());

        return (Finalizar(pkg), $"SeguimientoReproductoraEngorde_Lote{loteId}_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
