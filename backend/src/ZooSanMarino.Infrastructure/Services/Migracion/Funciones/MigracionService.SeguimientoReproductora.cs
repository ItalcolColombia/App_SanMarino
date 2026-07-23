// src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.SeguimientoReproductora.cs
// Línea Engorde · Seguimiento reproductora (primera semana). El lote engorde de cada fila sale de las
// columnas de ubicación por NOMBRE (Granja/Núcleo/Galpón/Lote, sin mayúsculas/acentos) o del lote
// seleccionado en pantalla; la columna "Reproductora" identifica el lote reproductora (id/código/nombre)
// y puede quedar vacía si en pantalla se eligió una reproductora puntual (ctx.ReproductoraId).
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
    private sealed record ReproductoraInfo(int Id, string ReproductoraId, string? Codigo, string Nombre, DateTime? FechaEncasetamiento);

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

    /// <summary>
    /// Lotes reproductora de varios lotes engorde, agrupados por lote y con índice por clave
    /// normalizada (id / código / nombre) para resolver la columna "Reproductora" del Excel.
    /// </summary>
    private async Task<Dictionary<int, (List<ReproductoraInfo> Repros, Dictionary<string, List<ReproductoraInfo>> PorClave)>> CargarReproductorasDeLotesAsync(IReadOnlyCollection<int> loteIds, CancellationToken ct)
    {
        var filas = await _ctx.LoteReproductoraAveEngorde.AsNoTracking()
            .Where(r => loteIds.Contains(r.LoteAveEngordeId))
            .OrderBy(r => r.NombreLote)
            .Select(r => new { r.LoteAveEngordeId, r.Id, r.ReproductoraId, r.CodigoReproductora, r.NombreLote, r.FechaEncasetamiento })
            .ToListAsync(ct);

        var resultado = new Dictionary<int, (List<ReproductoraInfo>, Dictionary<string, List<ReproductoraInfo>>)>();
        foreach (var grupo in filas.GroupBy(f => f.LoteAveEngordeId))
        {
            var repros = grupo.Select(f => new ReproductoraInfo(f.Id, f.ReproductoraId, f.CodigoReproductora, f.NombreLote, f.FechaEncasetamiento)).ToList();
            var porClave = new Dictionary<string, List<ReproductoraInfo>>();
            void Indexar(string? clave, ReproductoraInfo repro)
            {
                var k = MigracionCalculos.NormalizarClave(clave);
                if (string.IsNullOrEmpty(k)) return;
                if (!porClave.TryGetValue(k, out var lista)) porClave[k] = lista = new List<ReproductoraInfo>();
                if (!lista.Any(x => x.Id == repro.Id)) lista.Add(repro);
            }
            foreach (var r in repros)
            {
                Indexar(r.ReproductoraId, r);
                Indexar(r.Codigo, r);
                Indexar(r.Nombre, r);
            }
            resultado[grupo.Key] = (repros, porClave);
        }
        return resultado;
    }

    /// <summary>Días cargados/confirmados por lote reproductora (para instrucciones y selector).</summary>
    private async Task<Dictionary<int, (int Total, int Confirmados)>> CargarEstadoSeguimientoReprosAsync(IReadOnlyCollection<int> reproIds, CancellationToken ct)
    {
        if (reproIds.Count == 0) return new Dictionary<int, (int, int)>();
        return (await _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.AsNoTracking()
                .Where(s => reproIds.Contains(s.LoteReproductoraAveEngordeId))
                .Select(s => new { s.LoteReproductoraAveEngordeId, s.Confirmado })
                .ToListAsync(ct))
            .GroupBy(x => x.LoteReproductoraAveEngordeId)
            .ToDictionary(g => g.Key, g => (g.Count(), g.Count(x => x.Confirmado)));
    }

    /// <summary>Reproductoras del lote engorde para el selector del módulo (opcional al cargar).</summary>
    public async Task<IReadOnlyList<ReproductoraElegibleDto>> GetReproductorasElegiblesAsync(int loteId, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var (lote, errLote) = await ResolverLoteEngordeAsync(companyId, loteId, ct);
        if (lote is null) throw new InvalidOperationException(errLote!);

        var mapa = await CargarReproductorasDeLotesAsync(new[] { loteId }, ct);
        var repros = mapa.TryGetValue(loteId, out var rl) ? rl.Repros : new List<ReproductoraInfo>();
        var estado = await CargarEstadoSeguimientoReprosAsync(repros.Select(r => r.Id).ToList(), ct);

        return repros.Select(r =>
        {
            var (total, conf) = estado.TryGetValue(r.Id, out var st) ? st : (0, 0);
            return new ReproductoraElegibleDto(r.Id, r.ReproductoraId, r.Codigo, r.Nombre, r.FechaEncasetamiento, total, conf);
        }).ToList();
    }

    // ── Import ───────────────────────────────────────────────────────────────
    private async Task<MigracionResultDto> ProcesarSeguimientoReproductoraAsync(IFormFile file, bool dryRun, bool permitirParcial, int companyId, MigracionContextoDto ctx, CancellationToken ct)
    {
        const TipoMigracion tipo = TipoMigracion.SeguimientoReproductoraEngorde;
        const int MaxDias = ReproductoraEngordeCalculos.DiasRecogidaReproductora;

        if (ctx.LoteId is not int loteCtxId) return ErrorContexto(tipo, dryRun, "Seleccioná un lote de engorde antes de importar.");
        var (loteCtx, errLote) = await ResolverLoteEngordeAsync(companyId, loteCtxId, ct);
        if (loteCtx is null) return ErrorContexto(tipo, dryRun, errLote!);

        var errores = new List<MigracionErrorDto>();
        using var stream = file.OpenReadStream();
        var filas = LeerDatosConEsquema(stream, MigracionEsquemas.Para(tipo), errores);
        if (errores.Any(e => e.Severidad == "Error")) return ResultadoConErrores(tipo, dryRun, filas.Count, errores);
        if (filas.Count == 0 && errores.Count == 0) return ResultadoVacio(tipo, dryRun);

        var (lotesUbicados, lotesPorNombre) = await CargarLotesEngordeUbicadosAsync(companyId, ct);
        var loteCtxUbicado = lotesUbicados.FirstOrDefault(l => l.LoteId == loteCtxId)
            ?? new LoteEngordeUbicado(loteCtxId, loteCtx.LoteNombre, loteCtx.FechaEncaset, string.Empty, null, null, null, null);

        var idsAbiertos = lotesUbicados.Select(l => l.LoteId).ToList();
        if (!idsAbiertos.Contains(loteCtxId)) idsAbiertos.Add(loteCtxId);
        var reprosPorLote = await CargarReproductorasDeLotesAsync(idsAbiertos, ct);

        if (!reprosPorLote.ContainsKey(loteCtxId))
            return ErrorContexto(tipo, dryRun, $"El lote {loteCtx.LoteNombre} no tiene lotes reproductora asociados.");

        // Reproductora elegida en pantalla (opcional): debe pertenecer al lote seleccionado.
        ReproductoraInfo? reproCtx = null;
        if (ctx.ReproductoraId is int reproCtxId)
        {
            reproCtx = reprosPorLote[loteCtxId].Repros.FirstOrDefault(r => r.Id == reproCtxId);
            if (reproCtx is null)
                return ErrorContexto(tipo, dryRun, "La reproductora seleccionada no pertenece al lote de engorde elegido.");
        }

        // Fechas ya cargadas por reproductora (idempotencia) + conteo para el tope de 7 días.
        var reproIdsTodos = reprosPorLote.Values.SelectMany(v => v.Item1).Select(r => r.Id).Distinct().ToList();
        var existentesPorRepro = (await _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.AsNoTracking()
                .Where(s => reproIdsTodos.Contains(s.LoteReproductoraAveEngordeId))
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
            // ── Lote engorde de la fila: por nombres (case-insensitive) o el seleccionado en pantalla ──
            var granjaTxt = MigracionCalculos.TextoLimpio(Celda(fila, "granja", "nombre granja"));
            var nucleoTxt = MigracionCalculos.TextoLimpio(Celda(fila, "nucleo", "nombre nucleo"));
            var galponTxt = MigracionCalculos.TextoLimpio(Celda(fila, "galpon", "nombre galpon"));
            var loteTxt   = MigracionCalculos.TextoLimpio(Celda(fila, "lote", "nombre lote"));

            LoteEngordeUbicado lote;
            if (loteTxt is null)
            {
                if (granjaTxt is not null || nucleoTxt is not null || galponTxt is not null)
                { errores.Add(new(fila.Numero, "Lote", null, "Indicá también el Lote cuando especificás Granja/Núcleo/Galpón (sin Lote, la fila usa el lote seleccionado en pantalla).")); continue; }
                lote = loteCtxUbicado;
            }
            else
            {
                var candidatos = lotesPorNombre.TryGetValue(MigracionCalculos.NormalizarClave(loteTxt), out var lista)
                    ? lista : new List<LoteEngordeUbicado>();
                candidatos = FiltrarPorUbicacion(candidatos, granjaTxt, nucleoTxt, galponTxt);
                if (candidatos.Count == 0)
                { errores.Add(new(fila.Numero, "Lote", loteTxt, $"No existe un lote de engorde ABIERTO llamado '{loteTxt}' que coincida con la Granja/Núcleo/Galpón indicados.")); continue; }
                if (candidatos.Count > 1)
                { errores.Add(new(fila.Numero, "Lote", loteTxt, $"El lote '{loteTxt}' es ambiguo ({candidatos.Count} coincidencias); especificá Granja, Núcleo y/o Galpón.")); continue; }
                lote = candidatos[0];
            }

            if (!reprosPorLote.TryGetValue(lote.LoteId, out var reprosLote) || reprosLote.Repros.Count == 0)
            { errores.Add(new(fila.Numero, "Reproductora", null, $"El lote {lote.LoteNombre} no tiene lotes reproductora asociados.")); continue; }

            // ── Reproductora: celda (id/código/nombre) o la elegida en pantalla ──
            var reproTexto = MigracionCalculos.TextoLimpio(Celda(fila, "reproductora", "reproductora id", "repro", "codigo reproductora"));
            var reproClave = MigracionCalculos.NormalizarClave(reproTexto);
            ReproductoraInfo repro;
            if (string.IsNullOrEmpty(reproClave))
            {
                if (reproCtx is not null && lote.LoteId == loteCtxId) repro = reproCtx;
                else
                { errores.Add(new(fila.Numero, "Reproductora", null, $"Reproductora: obligatoria para el lote {lote.LoteNombre} (id, código o nombre; o elegí una reproductora en pantalla).")); continue; }
            }
            else
            {
                if (!reprosLote.PorClave.TryGetValue(reproClave, out var candidatas))
                { errores.Add(new(fila.Numero, "Reproductora", reproTexto, $"La reproductora '{reproTexto}' no existe en el lote {lote.LoteNombre}.")); continue; }
                if (candidatas.Count > 1)
                { errores.Add(new(fila.Numero, "Reproductora", reproTexto, $"'{reproTexto}' es ambigua en el lote (coincide con {candidatas.Count} reproductoras); usá el id o el código.")); continue; }
                repro = candidatas[0];
                if (reproCtx is not null && lote.LoteId == loteCtxId && repro.Id != reproCtx.Id)
                { errores.Add(new(fila.Numero, "Reproductora", reproTexto, $"La fila indica '{repro.Nombre}' pero en pantalla seleccionaste '{reproCtx.Nombre}'; dejá la columna vacía o corregí la fila.")); continue; }
            }

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
            { fallos.Add(new(0, "Fecha", dto.FechaRegistro.ToString("yyyy-MM-dd"), $"Error al insertar/confirmar (reproductora {dto.LoteId}): {ex.Message}")); }
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

        var reprosMapa = await CargarReproductorasDeLotesAsync(new[] { loteId }, ct);
        var repros = reprosMapa.TryGetValue(loteId, out var rl) ? rl.Repros : new List<ReproductoraInfo>();
        if (repros.Count == 0)
            throw new InvalidOperationException($"El lote {lote.LoteNombre} no tiene lotes reproductora asociados.");

        // Reproductora elegida en pantalla (opcional): acota la plantilla a esa.
        ReproductoraInfo? reproCtx = null;
        if (ctx.ReproductoraId is int reproCtxId)
        {
            reproCtx = repros.FirstOrDefault(r => r.Id == reproCtxId);
            if (reproCtx is null)
                throw new InvalidOperationException("La reproductora seleccionada no pertenece al lote de engorde elegido.");
        }

        var estado = await CargarEstadoSeguimientoReprosAsync(repros.Select(r => r.Id).ToList(), ct);
        var esquema = MigracionEsquemas.SeguimientoReproductoraEngorde;

        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Datos");
        // El lote (y la reproductora, si se eligió) salen del FILTRO de pantalla: la plantilla no
        // emite las columnas de ubicación. Siguen siendo opcionales al importar — un archivo avanzado
        // puede agregarlas (Granja/Núcleo/Galpón/Lote) para cargar varios lotes de una vez.
        var columnasEmitidas = PonerEncabezadosSin(ws, esquema, new HashSet<string> { "Granja", "Núcleo", "Galpón", "Lote" });

        // Referencias: reproductoras del lote seleccionado (col A).
        var wsRef = pkg.Workbook.Worksheets.Add("Referencias");
        EscribirColumnaRef(wsRef, 1, $"Reproductoras del lote {lote.LoteNombre}", repros.Select(r => r.Nombre));
        var idxReproductora = columnasEmitidas.FindIndex(c => c.Titulo == "Reproductora");
        if (repros.Count > 0 && idxReproductora >= 0)
            DropdownRango(ws, ColumnaLetra(idxReproductora + 1), $"Referencias!$A$2:$A${repros.Count + 1}");

        var lineas = new List<string>
        {
            $"Una fila por reproductora y día en la hoja 'Datos'. Todas las filas corresponden al lote {lote.LoteNombre} (el que elegiste en pantalla).",
            reproCtx is null
                ? "• Reproductora: id, código o nombre del lote reproductora (lista en 'Referencias'). Obligatoria en cada fila salvo que elijas una reproductora en pantalla."
                : $"• Reproductora: seleccionaste '{reproCtx.Nombre}' en pantalla — podés dejar la columna vacía (las filas sin valor cargan a esa reproductora).",
            $"• Fecha: obligatoria (aaaa-mm-dd o dd/mm/aaaa), dentro de la PRIMERA SEMANA: edad 1 a {MaxDias} días desde el encasetamiento de la reproductora.",
            "• Mortalidad / Selección / Error de sexaje: enteros ≥ 0 (vacío = 0). Consumo H/M: número ≥ 0 (acepta coma o punto).",
            "• Unidad Consumo: 'kg' (default si se deja vacía) o 'qq' — con 'qq' el Consumo H/M se convierte automáticamente a kg (1 qq = 45.36 kg).",
            "• Peso (g) y CV: ≥ 0 opcionales. Uniformidad: 0 a 100 opcional.",
            $"• Máximo {MaxDias} días de seguimiento por reproductora (incluye los ya registrados en el sistema).",
            "• Avanzado: para cargar VARIOS lotes en un solo archivo podés agregar las columnas opcionales",
            "  Granja / Núcleo / Galpón / Lote (nombres tal como aparecen en el sistema; mayúsculas indistintas).",
            "La carga es idempotente: las fechas ya registradas de cada reproductora se omiten.",
            "IMPORTANTE: cada registro importado queda CONFIRMADO automáticamente — al completar todos los lotes",
            "reproductora de un día, ese día cruza al seguimiento del lote pollo engorde (igual que confirmar en pantalla).",
            "",
            $"Reproductoras del lote {lote.LoteNombre}:"
        };
        foreach (var r in repros)
        {
            if (reproCtx is not null && r.Id != reproCtx.Id) continue;
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
