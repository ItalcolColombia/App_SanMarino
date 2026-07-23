// src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.SeguimientoEngorde.cs
// Línea Engorde · Seguimiento diario. Elegibilidad (lotes LoteAveEngorde no cerrados) + plantilla por
// lote + parse/validación en C#. La INSERCIÓN reutiliza ISeguimientoAvesEngordeService.CreateAsync por
// fila (decisión: replicar todos los efectos vivos — retiro de InventarioAves + recálculo de saldo;
// el descuento de inventario de alimento aplica cuando la fila trae Alimento 1/2 del inventario).
// La fila puede UBICAR su lote por NOMBRES (Granja/Núcleo/Galpón/Lote, comparación sin mayúsculas
// ni acentos); sin columna Lote usa el lote seleccionado en pantalla. Idempotente: omite (lote, fecha)
// ya cargados (contadas en FilasOmitidas; incluye filas origen_cruce de días 1-7). Sin transacción
// externa para no anidar con la transacción propia de la ruta Colombia (modelo-B).
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

    // ── Localización por nombres (case/acento-insensible) ────────────────────
    /// <summary>Lote engorde abierto con los nombres de su ubicación, para resolver filas por texto.</summary>
    private sealed record LoteEngordeUbicado(
        int LoteId, string LoteNombre, DateTime? FechaEncaset, string GranjaNombre,
        string? NucleoCodigo, string? NucleoNombre, string? GalponCodigo, string? GalponNombre);

    /// <summary>
    /// Lotes engorde ABIERTOS de la empresa con granja/núcleo/galpón resueltos a nombre, más un índice
    /// por nombre de lote normalizado (NormalizarClave = sin mayúsculas/acentos). El usuario llena la
    /// plantilla con los nombres tal como los ve en pantalla; acá se comparan normalizados.
    /// </summary>
    private async Task<(List<LoteEngordeUbicado> Lotes, Dictionary<string, List<LoteEngordeUbicado>> PorNombre)> CargarLotesEngordeUbicadosAsync(int companyId, CancellationToken ct)
    {
        var lotes = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.LoteAveEngordeId != null
                        && l.EstadoOperativoLote != "Cerrado")
            .Select(l => new { Id = l.LoteAveEngordeId!.Value, l.LoteNombre, l.GranjaId, l.NucleoId, l.GalponId, l.FechaEncaset })
            .ToListAsync(ct);

        var granjaIds = lotes.Select(l => l.GranjaId).Distinct().ToList();
        var granjas = await _ctx.Farms.AsNoTracking()
            .Where(f => granjaIds.Contains(f.Id))
            .Select(f => new { f.Id, f.Name })
            .ToListAsync(ct);
        var granjaPorId = granjas.GroupBy(f => f.Id).ToDictionary(gr => gr.Key, gr => gr.First().Name);

        var nucleos = await _ctx.Nucleos.AsNoTracking()
            .Where(n => granjaIds.Contains(n.GranjaId))
            .Select(n => new { n.NucleoId, n.GranjaId, n.NucleoNombre })
            .ToListAsync(ct);
        var nucleoPorClave = nucleos.GroupBy(n => (n.GranjaId, Codigo: n.NucleoId.Trim()))
            .ToDictionary(gr => gr.Key, gr => gr.First().NucleoNombre);

        var galpones = await _ctx.Galpones.AsNoTracking()
            .Where(ga => granjaIds.Contains(ga.GranjaId))
            .Select(ga => new { ga.GalponId, ga.GranjaId, ga.GalponNombre })
            .ToListAsync(ct);
        var galponPorClave = galpones.GroupBy(ga => (ga.GranjaId, Codigo: ga.GalponId.Trim()))
            .ToDictionary(gr => gr.Key, gr => gr.First().GalponNombre);

        var ubicados = lotes.Select(l =>
        {
            var nucleoCodigo = string.IsNullOrWhiteSpace(l.NucleoId) ? null : l.NucleoId.Trim();
            var galponCodigo = string.IsNullOrWhiteSpace(l.GalponId) ? null : l.GalponId.Trim();
            return new LoteEngordeUbicado(
                l.Id, l.LoteNombre, l.FechaEncaset,
                granjaPorId.TryGetValue(l.GranjaId, out var gn) ? gn : l.GranjaId.ToString(),
                nucleoCodigo,
                nucleoCodigo is null ? null : nucleoPorClave.GetValueOrDefault((l.GranjaId, nucleoCodigo)),
                galponCodigo,
                galponCodigo is null ? null : galponPorClave.GetValueOrDefault((l.GranjaId, galponCodigo)));
        }).ToList();

        var porNombre = ubicados.GroupBy(l => MigracionCalculos.NormalizarClave(l.LoteNombre))
            .Where(gr => !string.IsNullOrEmpty(gr.Key))
            .ToDictionary(gr => gr.Key, gr => gr.ToList());
        return (ubicados, porNombre);
    }

    /// <summary>Acota los candidatos por Granja/Núcleo/Galpón cuando la fila los trae (nombre O código, normalizados).</summary>
    private static List<LoteEngordeUbicado> FiltrarPorUbicacion(List<LoteEngordeUbicado> candidatos, string? granja, string? nucleo, string? galpon)
    {
        static bool Coincide(string valorNormalizado, string? codigo, string? nombre) =>
            valorNormalizado == MigracionCalculos.NormalizarClave(codigo) ||
            valorNormalizado == MigracionCalculos.NormalizarClave(nombre);

        var q = candidatos.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(granja))
        {
            var k = MigracionCalculos.NormalizarClave(granja);
            q = q.Where(c => MigracionCalculos.NormalizarClave(c.GranjaNombre) == k);
        }
        if (!string.IsNullOrWhiteSpace(nucleo))
        {
            var k = MigracionCalculos.NormalizarClave(nucleo);
            q = q.Where(c => Coincide(k, c.NucleoCodigo, c.NucleoNombre));
        }
        if (!string.IsNullOrWhiteSpace(galpon))
        {
            var k = MigracionCalculos.NormalizarClave(galpon);
            q = q.Where(c => Coincide(k, c.GalponCodigo, c.GalponNombre));
        }
        return q.ToList();
    }

    // ── Alimentos del inventario (concepto "alimento") ───────────────────────
    /// <summary>
    /// Ítems de alimento ACTIVOS de la empresa (inventario unificado item_inventario), como lista para
    /// la hoja Referencias y como índice por nombre/código normalizados para resolver las columnas
    /// "Alimento 1/2 H-M" del Excel.
    /// </summary>
    private async Task<(List<(int Id, string Codigo, string Nombre)> Lista, Dictionary<string, List<(int Id, string Nombre)>> PorClave)> CargarAlimentosEmpresaAsync(int companyId, CancellationToken ct)
    {
        var items = await _ctx.ItemInventario.AsNoTracking()
            .Where(i => i.CompanyId == companyId && i.Activo && i.TipoItem.ToLower() == "alimento")
            .OrderBy(i => i.Nombre)
            .Select(i => new { i.Id, i.Codigo, i.Nombre })
            .ToListAsync(ct);

        var porClave = new Dictionary<string, List<(int Id, string Nombre)>>();
        void Indexar(string? clave, int id, string nombre)
        {
            var k = MigracionCalculos.NormalizarClave(clave);
            if (string.IsNullOrEmpty(k)) return;
            if (!porClave.TryGetValue(k, out var lista)) porClave[k] = lista = new List<(int, string)>();
            if (!lista.Any(x => x.Id == id)) lista.Add((id, nombre));
        }
        foreach (var i in items) { Indexar(i.Nombre, i.Id, i.Nombre); Indexar(i.Codigo, i.Id, i.Nombre); }

        return (items.Select(i => (i.Id, i.Codigo, i.Nombre)).ToList(), porClave);
    }

    /// <summary>
    /// Lee un par (Alimento N, Consumo Alimento N) de un sexo: resuelve el ítem del inventario por
    /// nombre o código (sin mayúsculas/acentos) y agrega el ItemSeguimientoDto con la cantidad en kg
    /// (aplica la Unidad Consumo de la fila). Alimento sin consumo &gt; 0, consumo sin alimento y
    /// alimento inexistente/ambiguo son errores de fila.
    /// </summary>
    private static void LeerAlimentoSlot(
        FilaCruda fila, List<MigracionErrorDto> errores,
        Dictionary<string, List<(int Id, string Nombre)>> alimentos, string unidadConsumo,
        List<ItemSeguimientoDto> destino,
        string colAlimento, string[] headersAlimento, string colConsumo, string[] headersConsumo)
    {
        var nombreTxt = MigracionCalculos.TextoLimpio(Celda(fila, headersAlimento));
        int e0 = errores.Count;
        var cantidad = DecimalNoNeg(fila, errores, colConsumo, headersConsumo);
        if (errores.Count > e0) return; // consumo inválido: ya reportado

        if (nombreTxt is null && cantidad is null) return;
        if (nombreTxt is null)
        {
            if (cantidad is > 0)
                errores.Add(new(fila.Numero, colAlimento, null, $"{colAlimento}: indicá el alimento del consumo informado en {colConsumo}."));
            return;
        }
        if (cantidad is null or <= 0)
        { errores.Add(new(fila.Numero, colConsumo, null, $"{colConsumo}: requerido (> 0) cuando indicás {colAlimento}.")); return; }

        if (!alimentos.TryGetValue(MigracionCalculos.NormalizarClave(nombreTxt), out var matches) || matches.Count == 0)
        { errores.Add(new(fila.Numero, colAlimento, nombreTxt, $"El alimento '{nombreTxt}' no existe en el inventario de la empresa (concepto alimento, activo). Usá el nombre o código de la hoja Referencias.")); return; }
        if (matches.Count > 1)
        { errores.Add(new(fila.Numero, colAlimento, nombreTxt, $"'{nombreTxt}' coincide con {matches.Count} alimentos distintos; usá el código.")); return; }

        var kg = MigracionCalculos.ConsumoAKilos(cantidad, unidadConsumo)!.Value;
        destino.Add(new ItemSeguimientoDto
        {
            TipoItem = "alimento",
            CatalogItemId = 0,
            ItemInventarioEcuadorId = matches[0].Id, // inventario unificado (camino 2 en todos los países)
            Nombre = matches[0].Nombre,
            Cantidad = (double)kg,
            Unidad = "kg"
        });
    }

    // ── Import ───────────────────────────────────────────────────────────────
    private async Task<MigracionResultDto> ProcesarSeguimientoEngordeAsync(IFormFile file, bool dryRun, bool permitirParcial, int companyId, MigracionContextoDto ctx, CancellationToken ct)
    {
        const TipoMigracion tipo = TipoMigracion.SeguimientoPolloEngorde;
        if (ctx.LoteId is not int loteCtxId) return ErrorContexto(tipo, dryRun, "Seleccioná un lote de engorde antes de importar.");
        var (loteCtx, errLote) = await ResolverLoteEngordeAsync(companyId, loteCtxId, ct);
        if (loteCtx is null) return ErrorContexto(tipo, dryRun, errLote!);

        var errores = new List<MigracionErrorDto>();
        using var stream = file.OpenReadStream();
        var filas = LeerDatosConEsquema(stream, MigracionEsquemas.Para(tipo), errores);
        if (errores.Any(e => e.Severidad == "Error")) return ResultadoConErrores(tipo, dryRun, filas.Count, errores);
        if (filas.Count == 0 && errores.Count == 0) return ResultadoVacio(tipo, dryRun);

        var (lotesUbicados, lotesPorNombre) = await CargarLotesEngordeUbicadosAsync(companyId, ct);
        var (_, alimentosPorClave) = await CargarAlimentosEmpresaAsync(companyId, ct);
        var loteCtxUbicado = lotesUbicados.FirstOrDefault(l => l.LoteId == loteCtxId)
            ?? new LoteEngordeUbicado(loteCtxId, loteCtx.LoteNombre, loteCtx.FechaEncaset, string.Empty, null, null, null, null);

        // (lote, fecha) ya cargados de TODOS los lotes abiertos (idempotencia multi-lote en una consulta;
        // incluye filas origen_cruce de días 1-7).
        var idsAbiertos = lotesUbicados.Select(l => l.LoteId).ToList();
        if (!idsAbiertos.Contains(loteCtxId)) idsAbiertos.Add(loteCtxId);
        var existentes = (await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
                .Where(s => idsAbiertos.Contains(s.LoteAveEngordeId))
                .Select(s => new { s.LoteAveEngordeId, s.Fecha })
                .ToListAsync(ct))
            .Select(x => (x.LoteAveEngordeId, x.Fecha.Date))
            .ToHashSet();

        var dtos = new List<SeguimientoLoteLevanteDto>();
        var fechasVistas = new HashSet<(int LoteId, DateTime Fecha)>();
        int omitidas = 0;
        var hoyUtc = DateTime.UtcNow.Date;

        foreach (var fila in filas)
        {
            // Lote de la fila: por nombres (case-insensitive) o el seleccionado en pantalla.
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

            if (!MigracionCalculos.TryFecha(Celda(fila, "fecha"), out var fecha))
            { errores.Add(new(fila.Numero, "Fecha", null, "Fecha inválida o faltante.")); continue; }
            if (!fechasVistas.Add((lote.LoteId, fecha.Date)))
            { errores.Add(new(fila.Numero, "Fecha", fecha.ToString("yyyy-MM-dd"), $"Fecha repetida en el archivo para el lote {lote.LoteNombre}.")); continue; }
            if (existentes.Contains((lote.LoteId, fecha.Date))) { omitidas++; continue; } // ya existe → idempotente, se omite

            // Regla de fecha (alineada al front): nunca anterior al encaset del lote; futura solo advierte.
            if (lote.FechaEncaset.HasValue && fecha.Date < lote.FechaEncaset.Value.Date)
            { errores.Add(new(fila.Numero, "Fecha", fecha.ToString("yyyy-MM-dd"), $"{lote.LoteNombre}: la fecha es anterior al encaset del lote ({lote.FechaEncaset.Value:yyyy-MM-dd}).")); continue; }
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

            // Hasta dos alimentos del inventario por sexo (descuentan inventario al importar).
            var itemsH = new List<ItemSeguimientoDto>();
            var itemsM = new List<ItemSeguimientoDto>();
            LeerAlimentoSlot(fila, errores, alimentosPorClave, unidadConsumo, itemsH,
                "Alimento 1 H", new[] { "alimento 1 h", "alimento 1 hembras", "alimento uno hembras" },
                "Consumo Alimento 1 H", new[] { "consumo alimento 1 h", "consumo 1 h", "consumo alimento uno hembras" });
            LeerAlimentoSlot(fila, errores, alimentosPorClave, unidadConsumo, itemsH,
                "Alimento 2 H", new[] { "alimento 2 h", "alimento 2 hembras", "alimento dos hembras" },
                "Consumo Alimento 2 H", new[] { "consumo alimento 2 h", "consumo 2 h", "consumo alimento dos hembras" });
            LeerAlimentoSlot(fila, errores, alimentosPorClave, unidadConsumo, itemsM,
                "Alimento 1 M", new[] { "alimento 1 m", "alimento 1 machos", "alimento uno machos" },
                "Consumo Alimento 1 M", new[] { "consumo alimento 1 m", "consumo 1 m", "consumo alimento uno machos" });
            LeerAlimentoSlot(fila, errores, alimentosPorClave, unidadConsumo, itemsM,
                "Alimento 2 M", new[] { "alimento 2 m", "alimento 2 machos", "alimento dos machos" },
                "Consumo Alimento 2 M", new[] { "consumo alimento 2 m", "consumo 2 m", "consumo alimento dos machos" });
            if (errores.Count > e0) continue;

            // Unidad Consumo "qq" → convertir el consumo directo H/M a kg (los alimentos ya se convirtieron).
            consH = MigracionCalculos.ConsumoAKilos(consH, unidadConsumo);
            consM = MigracionCalculos.ConsumoAKilos(consM, unidadConsumo);

            if (itemsH.Count > 0 && consH is > 0)
                errores.Add(new(fila.Numero, "Consumo H (kg)", consH.Value.ToString("0.###"), "Se ignora el consumo directo H: la fila trae Alimento 1/2 H (el consumo sale de esos alimentos).", "Advertencia"));
            if (itemsM.Count > 0 && consM is > 0)
                errores.Add(new(fila.Numero, "Consumo M (kg)", consM.Value.ToString("0.###"), "Se ignora el consumo directo M: la fila trae Alimento 1/2 M (el consumo sale de esos alimentos).", "Advertencia"));

            // Día de pesaje obligatorio (espejo del modal: edad 1–7 y múltiplos de 7). En carga histórica
            // no bloquea (Advertencia): el modal sí lo exige al capturar el día a día.
            if (lote.FechaEncaset.HasValue && pesoH is null && pesoM is null)
            {
                var edad = (int)(fecha.Date - lote.FechaEncaset.Value.Date).TotalDays;
                if ((edad >= 1 && edad <= 7) || (edad > 7 && edad % 7 == 0))
                    errores.Add(new(fila.Numero, "Peso H (g)", fecha.ToString("yyyy-MM-dd"),
                        $"Día {edad} (edad 1–7 o múltiplo de 7): es día de pesaje obligatorio y la fila no trae peso.", "Advertencia"));
            }

            // Tipo Alimento: el texto de la celda o, si no viene, los nombres de los alimentos usados.
            var tipoAlimentoTxt = MigracionCalculos.TextoLimpio(Celda(fila, "tipo alimento"));
            if (string.IsNullOrWhiteSpace(tipoAlimentoTxt))
            {
                var nombres = itemsH.Concat(itemsM).Select(i => i.Nombre).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
                tipoAlimentoTxt = nombres.Count > 0 ? string.Join(" / ", nombres) : string.Empty;
            }

            var req = new CreateSeguimientoLoteLevanteRequest
            {
                LoteId = lote.LoteId,
                // Kind=Utc: el servicio asigna Fecha directo a una columna timestamptz (Npgsql exige Utc).
                FechaRegistro = DateTime.SpecifyKind(fecha.Date, DateTimeKind.Utc),
                MortalidadHembras = mortH,
                MortalidadMachos = mortM,
                SelH = selH,
                SelM = selM,
                ErrorSexajeHembras = errH,
                ErrorSexajeMachos = errM,
                TipoAlimento = tipoAlimentoTxt ?? string.Empty,
                ItemsHembras = itemsH.Count > 0 ? itemsH : null,
                ItemsMachos = itemsM.Count > 0 ? itemsM : null,
                // Consumo directo solo cuando la fila NO trae alimentos del inventario (con ítems, el
                // total sale de la suma de los alimentos — misma semántica que el modal).
                ConsumoKgHembrasDirecto = itemsH.Count > 0 ? null : (double?)consH,
                ConsumoKgMachosDirecto = itemsM.Count > 0 ? null : (double?)consM,
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
            { fallos.Add(new(0, "Fecha", dto.FechaRegistro.ToString("yyyy-MM-dd"), $"Error al insertar (lote {dto.LoteId}): {ex.Message}")); }
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

        var (lotesUbicados, _) = await CargarLotesEngordeUbicadosAsync(companyId, ct);
        var (alimentos, _) = await CargarAlimentosEmpresaAsync(companyId, ct);
        var esquema = MigracionEsquemas.SeguimientoPolloEngorde;

        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Datos");
        PonerEncabezados(ws, esquema);

        // Referencias: alimentos de la empresa (col A) + lotes abiertos con su ubicación (cols C..F).
        var wsRef = pkg.Workbook.Worksheets.Add("Referencias");
        EscribirColumnaRef(wsRef, 1, "Alimentos (inventario de la empresa)", alimentos.Select(a => a.Nombre));
        wsRef.Cells[1, 3].Value = "Granja"; wsRef.Cells[1, 3].Style.Font.Bold = true;
        wsRef.Cells[1, 4].Value = "Núcleo"; wsRef.Cells[1, 4].Style.Font.Bold = true;
        wsRef.Cells[1, 5].Value = "Galpón"; wsRef.Cells[1, 5].Style.Font.Bold = true;
        wsRef.Cells[1, 6].Value = "Lote"; wsRef.Cells[1, 6].Style.Font.Bold = true;
        int rr = 2;
        foreach (var lu in lotesUbicados.OrderBy(x => x.GranjaNombre).ThenBy(x => x.LoteNombre))
        {
            wsRef.Cells[rr, 3].Value = lu.GranjaNombre;
            wsRef.Cells[rr, 4].Value = lu.NucleoNombre ?? lu.NucleoCodigo;
            wsRef.Cells[rr, 5].Value = lu.GalponNombre ?? lu.GalponCodigo;
            wsRef.Cells[rr, 6].Value = lu.LoteNombre;
            rr++;
        }

        // Dropdowns sobre Datos: alimentos para Tipo Alimento + Alimento 1/2 H-M; lotes para Lote.
        if (alimentos.Count > 0)
        {
            var rangoAlimentos = $"Referencias!$A$2:$A${alimentos.Count + 1}";
            foreach (var titulo in new[] { "Tipo Alimento", "Alimento 1 H", "Alimento 2 H", "Alimento 1 M", "Alimento 2 M" })
                DropdownRango(ws, ColumnaLetra(IndiceColumna(esquema, titulo) + 1), rangoAlimentos);
        }
        if (lotesUbicados.Count > 0)
            DropdownRango(ws, ColumnaLetra(IndiceColumna(esquema, "Lote") + 1), $"Referencias!$F$2:$F${lotesUbicados.Count + 1}");

        HojaInstrucciones(pkg, $"Migración Seguimiento Engorde — Lote {lote.LoteNombre} (id {loteId})",
            "Una fila por día en la hoja 'Datos'.",
            "• Lote / Granja / Núcleo / Galpón: opcionales. Sin 'Lote', la fila corresponde al lote seleccionado en pantalla.",
            "  Con 'Lote' (nombre tal como aparece en el sistema; mayúsculas/minúsculas indistintas) podés cargar VARIOS lotes",
            "  en un mismo archivo; usá Granja/Núcleo/Galpón para desambiguar nombres repetidos (tabla en 'Referencias').",
            "• Fecha: obligatoria (aaaa-mm-dd o dd/mm/aaaa), no anterior al encaset del lote. Fecha futura solo advierte.",
            "• Mortalidad / Selección / Error de sexaje: enteros ≥ 0 (vacío = 0).",
            "• Alimento 1/2 H y M: elegí el alimento del inventario (lista desplegable, hoja 'Referencias') y su consumo.",
            "  Hasta dos alimentos por sexo por fecha; al importar se DESCUENTA el inventario de esos alimentos.",
            "• Consumo H/M (directo): solo si NO usás Alimento 1/2 (sin descuento de inventario). Número ≥ 0.",
            "• Unidad Consumo: 'kg' (default si se deja vacía) o 'qq' — aplica al consumo directo Y a los alimentos (1 qq = 45.36 kg).",
            "• Peso: ≥ 0 opcional. Uniformidad: 0 a 100 opcional.",
            "• Días de pesaje (edad 1–7 y múltiplos de 7): si la fila no trae peso se genera una advertencia (no bloquea).",
            "• Lotes MIXTOS (Panamá): cargá las cantidades en las columnas H (M = 0), igual que el formulario.",
            "• QQ Mixtas / QQ H / QQ M (Panamá): quintales de alimento por categoría, opcionales (≥ 0).",
            "La carga es idempotente por lote+fecha: las fechas ya cargadas (incluidos los primeros días generados por",
            "cruce reproductora) se omiten. Al importar se registra el retiro de aves por mortalidad/selección y se",
            "recalcula el saldo de alimento de cada lote.");

        return (Finalizar(pkg), $"SeguimientoEngorde_Lote{loteId}_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
