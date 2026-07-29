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
        int LoteId, string LoteNombre, DateTime? FechaEncaset, TimeOnly? HoraEncaset, string GranjaNombre,
        string? NucleoCodigo, string? NucleoNombre, string? GalponCodigo, string? GalponNombre,
        int GranjaId = 0)
    {
        /// <summary>Ubicación de stock del lote: es de donde sale el alimento que consume.</summary>
        public UbicacionAlimento Ubicacion => new UbicacionAlimento(GranjaId, NucleoCodigo, GalponCodigo).Normalizada();
    }

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
            .Select(l => new { Id = l.LoteAveEngordeId!.Value, l.LoteNombre, l.GranjaId, l.NucleoId, l.GalponId, l.FechaEncaset, l.HoraEncasetamiento })
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
                l.Id, l.LoteNombre, l.FechaEncaset, l.HoraEncasetamiento,
                granjaPorId.TryGetValue(l.GranjaId, out var gn) ? gn : l.GranjaId.ToString(),
                nucleoCodigo,
                nucleoCodigo is null ? null : nucleoPorClave.GetValueOrDefault((l.GranjaId, nucleoCodigo)),
                galponCodigo,
                galponCodigo is null ? null : galponPorClave.GetValueOrDefault((l.GranjaId, galponCodigo)),
                l.GranjaId);
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
        string colAlimentoCanonico, string[] headersAlimento, string colConsumoCanonico, string[] headersConsumo)
    {
        // Los mensajes citan la columna TAL COMO figura en el archivo del usuario. En la plantilla
        // MIXTA de Panamá las columnas se llaman "Alimento 1 Mixto"/"Consumo Alimento 1 Mixto" (alias
        // de las canónicas "… H"): nombrar la canónica mandaba a buscar una columna que no existe.
        var colAlimento = EtiquetaColumna(fila, colAlimentoCanonico, headersAlimento);
        var colConsumo = EtiquetaColumna(fila, colConsumoCanonico, headersConsumo);

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

    /// <summary>
    /// Claves de lectura de una columna del esquema de seguimiento engorde (título + alias). Un solo
    /// origen para validación y lectura: si el esquema acepta un alias, la celda se encuentra.
    /// </summary>
    private static string[] ClavesEngorde(string titulo) =>
        MigracionEsquemaCalculos.ClavesDeColumna(MigracionEsquemas.SeguimientoPolloEngorde, titulo);

    // ── Import ───────────────────────────────────────────────────────────────
    private async Task<MigracionResultDto> ProcesarSeguimientoEngordeAsync(IFormFile file, bool dryRun, bool permitirParcial, int companyId, MigracionContextoDto ctx, CancellationToken ct)
    {
        const TipoMigracion tipo = TipoMigracion.SeguimientoPolloEngorde;
        if (ctx.LoteId is not int loteCtxId) return ErrorContexto(tipo, dryRun, "Seleccioná un lote de engorde antes de importar.");
        var (loteCtx, errLote) = await ResolverLoteEngordeAsync(companyId, loteCtxId, ct);
        if (loteCtx is null) return ErrorContexto(tipo, dryRun, errLote!);

        var errores = new List<MigracionErrorDto>();
        List<FilaCruda> filas;
        using (var stream = file.OpenReadStream())
            filas = LeerDatosConEsquema(stream, MigracionEsquemas.Para(tipo), errores);
        if (errores.Any(e => e.Severidad == "Error")) return ResultadoConErrores(tipo, dryRun, filas.Count, errores);

        var (lotesUbicados, lotesPorNombre) = await CargarLotesEngordeUbicadosAsync(companyId, ct);
        var (_, alimentosPorClave) = await CargarAlimentosEmpresaAsync(companyId, ct);
        var loteCtxUbicado = lotesUbicados.FirstOrDefault(l => l.LoteId == loteCtxId)
            ?? new LoteEngordeUbicado(loteCtxId, loteCtx.LoteNombre, loteCtx.FechaEncaset, loteCtx.HoraEncasetamiento,
                string.Empty, loteCtx.NucleoId, null, loteCtx.GalponId, null, loteCtx.GranjaId);

        // Hoja "Alimento" (OPCIONAL): movimientos de inventario que deben existir ANTES de que el
        // consumo del seguimiento los descuente. Un archivo sin la hoja sigue el camino de siempre.
        // Se lee ANTES del corte por "archivo vacío": un archivo que trae SOLO entradas de alimento
        // (hoja Datos en blanco) es un caso válido — cargar el inventario del galpón antes de digitar
        // el seguimiento — y cortar antes lo rechazaba como si estuviera vacío.
        // El NIVEL del alimento sale del flag por empresa/granja, no se asume galpón: una granja
        // Colombia (nivel granja) hacía que RegistrarIngresoAsync lanzara "no use Núcleo/Galpón" y la
        // fila se perdiera. Con el flag, Ecuador/Panamá (galpón) se comportan exactamente igual.
        var granjaLoteEngorde = await GranjaIdDeLoteAsync(loteCtxUbicado.LoteId, ct);
        var alimentoPorGalponEngorde = await ManejaAlimentoPorGalponAsync(granjaLoteEngorde, ct);
        var movimientosAlimento = await LeerHojaAlimentoAsync(file, companyId,
            new UbicacionAlimento(granjaLoteEngorde, loteCtxUbicado.NucleoCodigo, loteCtxUbicado.GalponCodigo),
            alimentoPorGalponEngorde, errores, ct);

        // Hoja "Reproductora" (OPCIONAL): la PRIMERA SEMANA del lote. Se digita en reproductora y el
        // trigger de cruce la vuelca a los días 1-7 de engorde. Reutiliza el mismo parseo que la línea
        // de migración dedicada, así que un archivo unificado valida idéntico a cargarla por separado.
        var filasRepro = LeerHojaOpcionalConEsquema(file, MigracionEsquemas.ReproductoraEnHoja, errores);
        var parseoRepro = filasRepro.Count > 0
            ? await ParsearFilasReproductoraAsync(filasRepro, companyId, ctx, loteCtxId, loteCtx, errores, ct)
            : null;
        if (parseoRepro?.ErrorContexto is string errRepro)
            errores.Add(new(0, "Reproductora", null, $"Hoja 'Reproductora': {errRepro}"));

        if (filas.Count == 0 && movimientosAlimento.Count == 0 && filasRepro.Count == 0 && errores.Count == 0)
            return ResultadoVacio(tipo, dryRun);

        // (lote, fecha) ya cargados de TODOS los lotes abiertos, con su id: una fecha repetida ya no se
        // omite, se ACTUALIZA con lo que trae el archivo (el archivo es la version vigente del dia).
        // Las filas de origen_cruce (dias 1-7) son la excepcion: las escribe el trigger de reproductora
        // y pisarlas desde aca las dejaria peleadas con su origen.
        var idsAbiertos = lotesUbicados.Select(l => l.LoteId).ToList();
        if (!idsAbiertos.Contains(loteCtxId)) idsAbiertos.Add(loteCtxId);
        var existentes = (await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
                .Where(s => idsAbiertos.Contains(s.LoteAveEngordeId))
                .Select(s => new { s.Id, s.LoteAveEngordeId, s.Fecha, s.OrigenCruce, s.Metadata })
                .ToListAsync(ct))
            .GroupBy(x => (x.LoteAveEngordeId, x.Fecha.Date))
            .ToDictionary(g => g.Key, g => g.First());

        // El flag de la empresa se resuelve UNA vez: dentro del loop serían N consultas iguales.
        var reglaHoraActiva = await PrimerRegistroPorHoraGate.ActivaAsync(_ctx, companyId, ct);

        var dtos = new List<SeguimientoLoteLevanteDto>();
        var actualizables = new List<SeguimientoLoteLevanteDto>();   // dias ya cargados que el archivo reemplaza
        var fechasVistas = new HashSet<(int LoteId, DateTime Fecha)>();
        int omitidas = 0;
        var hoyUtc = DateTime.UtcNow.Date;
        // Consumo de alimento que el archivo va a descontar, por (ubicación, ítem): entra en la
        // simulación de balance junto con las entradas de la hoja "Alimento".
        var salidasAlimento = new Dictionary<PosicionAlimento, decimal>();

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
            // Ya cargado: los dias del cruce se respetan (su fuente es reproductora); el resto se
            // marca para actualizar con los valores del archivo.
            existentes.TryGetValue((lote.LoteId, fecha.Date), out var yaCargado);
            if (yaCargado is not null && yaCargado.OrigenCruce) { omitidas++; continue; }

            // Regla de fecha (alineada al front): nunca anterior al PRIMER DÍA CON REGISTRO del lote,
            // que es el encaset o el día siguiente si las aves llegaron a las 13:00 o después. Futura
            // solo advierte.
            if (lote.FechaEncaset.HasValue)
            {
                var horaRegla = EncasetamientoCalculos.HoraEfectiva(lote.HoraEncaset, reglaHoraActiva);
                var primerDia = EncasetamientoCalculos.PrimerDiaConRegistro(lote.FechaEncaset.Value, horaRegla);
                if (fecha.Date < primerDia.Date)
                {
                    var motivoHora = EncasetamientoCalculos.MotivoDesplazamiento(horaRegla);
                    errores.Add(new(fila.Numero, "Fecha", fecha.ToString("yyyy-MM-dd"),
                        motivoHora is null
                            ? $"{lote.LoteNombre}: la fecha es anterior al encaset del lote ({lote.FechaEncaset.Value:yyyy-MM-dd})."
                            : $"{lote.LoteNombre}: el primer registro es el {primerDia:yyyy-MM-dd} porque {motivoHora}."));
                    continue;
                }
            }
            if (fecha.Date > hoyUtc)
                errores.Add(new(fila.Numero, "Fecha", fecha.ToString("yyyy-MM-dd"), "La fecha es futura; verificá que sea intencional.", "Advertencia"));

            int e0 = errores.Count;
            // Las claves de lectura salen del ESQUEMA (título + alias), no de listas a mano: así una
            // columna renombrada por alias — como los títulos mixtos de Panamá — se lee de verdad.
            // Con listas hardcodeadas el encabezado mixto pasaba la validación pero la celda no se
            // encontraba y el día entraba en CERO, sin error ni advertencia.
            var mortH = EnteroNoNeg(fila, errores, "Mort H", ClavesEngorde("Mort H"));
            var mortM = EnteroNoNeg(fila, errores, "Mort M", ClavesEngorde("Mort M"));
            var selH = EnteroNoNeg(fila, errores, "Sel H", ClavesEngorde("Sel H"));
            var selM = EnteroNoNeg(fila, errores, "Sel M", ClavesEngorde("Sel M"));
            var errH = EnteroNoNeg(fila, errores, "Error Sexaje H", ClavesEngorde("Error Sexaje H"));
            var errM = EnteroNoNeg(fila, errores, "Error Sexaje M", ClavesEngorde("Error Sexaje M"));
            var consH = DecimalNoNeg(fila, errores, "Consumo H (kg)", ClavesEngorde("Consumo H (kg)"));
            var consM = DecimalNoNeg(fila, errores, "Consumo M (kg)", ClavesEngorde("Consumo M (kg)"));
            var unidadConsumo = LeerUnidadConsumo(fila, errores);
            var pesoH = DobleNoNeg(fila, errores, "Peso H (g)", ClavesEngorde("Peso H (g)"));
            var pesoM = DobleNoNeg(fila, errores, "Peso M (g)", ClavesEngorde("Peso M (g)"));
            var unifH = Porcentaje0a100(fila, errores, "Uniformidad H", ClavesEngorde("Uniformidad H"));
            var unifM = Porcentaje0a100(fila, errores, "Uniformidad M", ClavesEngorde("Uniformidad M"));
            // Panamá: quintales por categoría (opcionales; persisten en qq_* para el informe semanal).
            var qqMix = DecimalNoNeg(fila, errores, "QQ Mixtas", ClavesEngorde("QQ Mixtas"));
            var qqH = DecimalNoNeg(fila, errores, "QQ H", ClavesEngorde("QQ H"));
            var qqM = DecimalNoNeg(fila, errores, "QQ M", ClavesEngorde("QQ M"));

            // Hasta dos alimentos del inventario por sexo (descuentan inventario al importar).
            var itemsH = new List<ItemSeguimientoDto>();
            var itemsM = new List<ItemSeguimientoDto>();
            LeerAlimentoSlot(fila, errores, alimentosPorClave, unidadConsumo, itemsH,
                "Alimento 1 H", ClavesEngorde("Alimento 1 H"),
                "Consumo Alimento 1 H", ClavesEngorde("Consumo Alimento 1 H"));
            LeerAlimentoSlot(fila, errores, alimentosPorClave, unidadConsumo, itemsH,
                "Alimento 2 H", ClavesEngorde("Alimento 2 H"),
                "Consumo Alimento 2 H", ClavesEngorde("Consumo Alimento 2 H"));
            LeerAlimentoSlot(fila, errores, alimentosPorClave, unidadConsumo, itemsM,
                "Alimento 1 M", ClavesEngorde("Alimento 1 M"),
                "Consumo Alimento 1 M", ClavesEngorde("Consumo Alimento 1 M"));
            LeerAlimentoSlot(fila, errores, alimentosPorClave, unidadConsumo, itemsM,
                "Alimento 2 M", ClavesEngorde("Alimento 2 M"),
                "Consumo Alimento 2 M", ClavesEngorde("Consumo Alimento 2 M"));
            if (errores.Count > e0) continue;

            // Unidad Consumo "qq" → convertir el consumo directo H/M a kg (los alimentos ya se convirtieron).
            consH = MigracionCalculos.ConsumoAKilos(consH, unidadConsumo);
            consM = MigracionCalculos.ConsumoAKilos(consM, unidadConsumo);

            if (itemsH.Count > 0 && consH is > 0)
            {
                var colConsumoH = EtiquetaColumna(fila, "Consumo H (kg)", ClavesEngorde("Consumo H (kg)"));
                var colAlim1 = EtiquetaColumna(fila, "Alimento 1 H", ClavesEngorde("Alimento 1 H"));
                errores.Add(new(fila.Numero, colConsumoH, consH.Value.ToString("0.###"), $"Se ignora el consumo directo de '{colConsumoH}': la fila trae '{colAlim1}' (el consumo sale de esos alimentos).", "Advertencia"));
            }
            if (itemsM.Count > 0 && consM is > 0)
                errores.Add(new(fila.Numero, "Consumo M (kg)", consM.Value.ToString("0.###"), "Se ignora el consumo directo M: la fila trae Alimento 1/2 M (el consumo sale de esos alimentos).", "Advertencia"));

            // Día de pesaje obligatorio (espejo del modal: días 1–7 y múltiplos de 7). En carga histórica
            // no bloquea (Advertencia): el modal sí lo exige al capturar el día a día.
            // El número sobre el que se evalúa la regla depende de la empresa: con la regla de la hora
            // de llegada activa es el DÍA DE NEGOCIO (el primer día con registro es el día 1, así el
            // pesaje semanal cae al cierre de la semana); sin ella es la edad cruda, igual que siempre.
            if (lote.FechaEncaset.HasValue && pesoH is null && pesoM is null)
            {
                var edad = (int)(fecha.Date - lote.FechaEncaset.Value.Date).TotalDays;
                var horaRegla = EncasetamientoCalculos.HoraEfectiva(lote.HoraEncaset, reglaHoraActiva);
                var diaNegocio = EncasetamientoCalculos.DiaDeNegocio(fecha, lote.FechaEncaset.Value, horaRegla);
                var diaRegla = PesajeEngordeCalculos.DiaParaReglaDePesaje(edad, diaNegocio, reglaHoraActiva);
                if (PesajeEngordeCalculos.EsDiaDePesajeObligatorio(diaRegla))
                    errores.Add(new(fila.Numero, EtiquetaColumna(fila, "Peso H (g)", ClavesEngorde("Peso H (g)")), fecha.ToString("yyyy-MM-dd"),
                        $"Día {diaRegla} (días 1–7 o múltiplo de 7): es día de pesaje obligatorio y la fila no trae peso.", "Advertencia"));
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
            if (yaCargado is not null) actualizables.Add(req.ToDto((int)yaCargado.Id));
            else dtos.Add(req.ToDto());

            // Lo que esta fila va a sacar del inventario del galpón del lote. Si el día YA estaba
            // cargado no sale su consumo entero: sale la DIFERENCIA contra lo que ya se había
            // descontado (es lo que hace UpdateAsync). Contar el total volvía a restar un consumo ya
            // aplicado y la proyección daba un faltante inventado.
            var yaDescontado = yaCargado?.Metadata is null
                ? new Dictionary<int, decimal>()
                : MetadataEngordeCalculos.ParseMetadataItemsToKg(yaCargado.Metadata.RootElement);

            foreach (var item in itemsH.Concat(itemsM))
                if (item.ItemInventarioEcuadorId is int itemId && item.Cantidad > 0)
                {
                    var delta = (decimal)item.Cantidad - yaDescontado.GetValueOrDefault(itemId, 0m);
                    yaDescontado.Remove(itemId);
                    if (delta != 0)
                        MigracionAlimentoCalculos.Acumular(
                            salidasAlimento, new PosicionAlimento(lote.Ubicacion, itemId), delta);
                }

            // Alimentos que el día TENÍA y el archivo ya no trae: se devuelven al galpón.
            foreach (var kv in yaDescontado.Where(x => x.Value > 0))
                MigracionAlimentoCalculos.Acumular(
                    salidasAlimento, new PosicionAlimento(lote.Ubicacion, kv.Key), -kv.Value);
        }

        // Balance: stock actual + entradas de la hoja "Alimento" − consumos del seguimiento. Si alguna
        // posición queda negativa el archivo se rechaza ENTERO con el faltante exacto. Sin esto, el
        // descuento fallaba dentro de un catch que solo loguea: el día se guardaba y el galpón quedaba
        // descuadrado sin ninguna señal.
        // Solo cuentan los movimientos que REALMENTE se van a aplicar. Los que ya están en el
        // histórico se omiten al importar, y sumarlos acá proyectaba un saldo que nunca iba a pasar:
        // al recargar un archivo ya importado, el reporte anunciaba el doble de entradas (galpón 6:
        // 4.470,664 kg proyectados contra los 2.235,332 que en realidad quedan).
        var clavesYaAplicadas = movimientosAlimento.Count > 0
            ? await ClavesMovimientosExistentesAsync(movimientosAlimento, ct)
            : new HashSet<string>();

        var entradasAlimento = new Dictionary<PosicionAlimento, decimal>();
        foreach (var m in movimientosAlimento)
        {
            if (clavesYaAplicadas.Contains(MigracionAlimentoCalculos.ClaveIdempotencia(
                    m.Movimiento, m.Destino, m.ItemId, m.Fecha, m.CantidadKg, m.Referencia)))
                continue;

            var posDestino = new PosicionAlimento(m.Destino.Normalizada(), m.ItemId);
            if (m.Movimiento is MovimientoAlimento.Ingreso or MovimientoAlimento.Recepcion)
                MigracionAlimentoCalculos.Acumular(entradasAlimento, posDestino, m.CantidadKg);
            else if (m.Movimiento is MovimientoAlimento.Consumo)
                MigracionAlimentoCalculos.Acumular(salidasAlimento, posDestino, m.CantidadKg);
            else if (m.Movimiento is MovimientoAlimento.Traslado && m.Origen is UbicacionAlimento origen)
            {
                MigracionAlimentoCalculos.Acumular(entradasAlimento, posDestino, m.CantidadKg);
                MigracionAlimentoCalculos.Acumular(salidasAlimento, new PosicionAlimento(origen.Normalizada(), m.ItemId), m.CantidadKg);
            }
        }

        // La primera semana consume del MISMO galpón: entra en el balance como cualquier otra salida.
        foreach (var dto in parseoRepro?.Dtos ?? new List<SeguimientoLoteLevanteDto>())
        {
            if (dto.Metadata is null) continue;
            foreach (var kv in MetadataEngordeCalculos.ParseMetadataItemsToKg(dto.Metadata.RootElement))
                if (kv.Value > 0)
                    MigracionAlimentoCalculos.Acumular(
                        salidasAlimento, new PosicionAlimento(loteCtxUbicado.Ubicacion, kv.Key), kv.Value);
        }

        var posiciones = new HashSet<PosicionAlimento>(entradasAlimento.Keys);
        foreach (var p in salidasAlimento.Keys) posiciones.Add(p);
        var stockActual = await CargarStockPosicionesAsync(posiciones, ct);

        foreach (var f in MigracionAlimentoCalculos.Simular(stockActual, entradasAlimento, salidasAlimento))
        {
            var nombre = await NombreAlimentoAsync(f.Posicion.ItemId, ct);
            errores.Add(new(0, "Alimento", nombre,
                $"No alcanza el stock de {nombre} en el galpón: el archivo consume {f.Requerido:N3} kg y solo hay {f.Disponible:N3} kg " +
                $"(faltan {f.Faltante:N3} kg). Cargá la entrada que falta en la hoja 'Alimento' o corregí el consumo."));
        }

        var saldos = MigracionAlimentoCalculos.Proyectar(stockActual, entradasAlimento, salidasAlimento);

        return await EjecutarSeguimientoEngordeAsync(
            tipo, dryRun, permitirParcial, file.FileName, filas.Count, omitidas, errores, dtos,
            movimientosAlimento, saldos, parseoRepro, actualizables, ct);
    }

    // ── Runner (valida → dry-run corta → CreateAsync fila por fila, sin TX externa, parcial opt-in) ─
    private async Task<MigracionResultDto> EjecutarSeguimientoEngordeAsync(
        TipoMigracion tipo, bool dryRun, bool permitirParcial, string nombreArchivo,
        int total, int omitidas, List<MigracionErrorDto> errores, List<SeguimientoLoteLevanteDto> dtos,
        List<MovimientoAlimentoFila> movimientosAlimento, IReadOnlyList<SaldoAlimentoProyectado> saldos,
        ParseoReproductora? parseoRepro, List<SeguimientoLoteLevanteDto> actualizables, CancellationToken ct)
    {
        var dtosRepro = parseoRepro?.Dtos ?? new List<SeguimientoLoteLevanteDto>();
        if (total == 0 && errores.Count == 0 && movimientosAlimento.Count == 0
            && dtosRepro.Count == 0 && actualizables.Count == 0)
            return ResultadoVacio(tipo, dryRun);

        var hayErroresReales = errores.Any(e => e.Severidad == "Error");
        var puedeInsertarParcial = hayErroresReales && !dryRun && permitirParcial && (dtos.Count > 0 || actualizables.Count > 0);

        if (hayErroresReales && !puedeInsertarParcial)
            return ResultadoConErrores(tipo, dryRun, total, errores) with { FilasOmitidas = omitidas };

        if (dryRun)
        {
            // El dry-run informa el saldo que quedaría por alimento: es la cifra que el usuario compara
            // contra su planilla de inventario ANTES de importar de verdad.
            if (actualizables.Count > 0)
                errores.Add(new(0, "Fecha", actualizables.Count.ToString(),
                    $"{actualizables.Count} día(s) del archivo YA están cargados: al importar se REEMPLAZAN con estos valores " +
                    $"({string.Join(", ", actualizables.OrderBy(d => d.FechaRegistro).Select(d => d.FechaRegistro.ToString("dd/MM")).Take(12))}" +
                    $"{(actualizables.Count > 12 ? ", …" : "")}). Las aves y el inventario se ajustan por la diferencia.",
                    "Advertencia"));

            // != 0, no > 0: al reemplazar un día con MENOS consumo el movimiento es negativo (se
            // devuelve alimento al galpón) y con "> 0" ese caso no se informaba.
            foreach (var s in saldos.Where(x => x.Entradas != 0 || x.Salidas != 0))
            {
                var nombre = await NombreAlimentoAsync(s.Posicion.ItemId, ct);
                // Un consumo negativo es alimento que VUELVE al galpón (el archivo reemplaza un día
                // con menos consumo). Escribirlo como "− -500" es ilegible: se dice lo que pasa.
                var movimiento = s.Salidas >= 0
                    ? $"+ {s.Entradas:N3} entradas − {s.Salidas:N3} consumo"
                    : $"+ {s.Entradas:N3} entradas + {-s.Salidas:N3} devueltos (el archivo baja el consumo ya cargado)";
                errores.Add(new(0, "Alimento", nombre,
                    $"Saldo proyectado de {nombre}: {s.SaldoInicial:N3} inicial {movimiento} = {s.SaldoFinal:N3} kg.",
                    "Advertencia"));
            }
            return ResultadoOk(tipo, dryRun, total, errores) with { FilasOmitidas = omitidas };
        }

        var fallos = new List<MigracionErrorDto>();

        // El alimento ENTRA primero: el consumo de cada día descuenta de un stock que ya tiene que
        // existir. Invertir el orden es el bug que dejaba el galpón en cero.
        var (movAplicados, movOmitidos) = await AplicarMovimientosAlimentoAsync(movimientosAlimento, fallos, ct);
        omitidas += movOmitidos;

        int insertados = 0;

        // Después del alimento y ANTES de la hoja Datos: la reproductora es la primera semana del lote
        // y su trigger de cruce escribe los días 1-7 de engorde. Cada registro queda CONFIRMADO, que es
        // lo que gatea ese cruce (idéntico a la línea de migración dedicada).
        foreach (var dto in dtosRepro)
        {
            try
            {
                var creado = await _seguimientoReproductoraService.CreateAsync(dto);
                await _seguimientoReproductoraService.ConfirmarAsync(creado.Id);
                insertados++;
            }
            catch (Exception ex)
            { fallos.Add(new(0, "Reproductora", dto.FechaRegistro.ToString("yyyy-MM-dd"), $"Hoja 'Reproductora': error al insertar/confirmar (reproductora {dto.LoteId}): {ex.Message}")); }
        }
        omitidas += parseoRepro?.Omitidas ?? 0;

        foreach (var dto in dtos)
        {
            try { await _seguimientoEngordeService.CreateAsync(dto); insertados++; }
            catch (Exception ex)
            { fallos.Add(new(0, "Fecha", dto.FechaRegistro.ToString("yyyy-MM-dd"), $"Error al insertar (lote {dto.LoteId}): {ex.Message}")); }
        }

        // Días que ya estaban: el archivo manda. UpdateAsync ajusta aves e inventario por la DIFERENCIA
        // contra lo que había, así que reemplazar un día con los mismos valores no mueve nada.
        int actualizados = 0;
        foreach (var dto in actualizables)
        {
            try
            {
                // El DbContext es scoped y viene de arrastrar todo el import (movimientos de alimento,
                // reproductora, inserts). UpdateAsync recalcula el saldo de TODO el lote y deja los 41
                // seguimientos en el tracker; en la vuelta siguiente EF encuentra entradas Detached en
                // el mismo batch y revienta con "Unexpected entry.EntityState". Cada actualización
                // arranca con el tracker limpio: carga lo suyo y guarda solo eso.
                _ctx.ChangeTracker.Clear();
                if (await _seguimientoEngordeService.UpdateAsync(dto) is not null) actualizados++;
                else fallos.Add(new(0, "Fecha", dto.FechaRegistro.ToString("yyyy-MM-dd"), $"No se encontró el registro a reemplazar (lote {dto.LoteId})."));
            }
            catch (Exception ex)
            { fallos.Add(new(0, "Fecha", dto.FechaRegistro.ToString("yyyy-MM-dd"), $"Error al reemplazar el día ya cargado (lote {dto.LoteId}): {ex.Message}")); }
        }
        insertados += movAplicados + actualizados;

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

        // Empresa que no maneja el engorde por sexo (Panamá) → plantilla con columnas MIXTAS. El
        // parseo NO cambia: SeguimientoPolloEngorde acepta esos títulos como alias de las columnas H.
        var mixto = await _ctx.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => c.SeguimientoEngordeMixto)
            .FirstOrDefaultAsync(ct);
        var esquema = mixto ? MigracionEsquemas.SeguimientoPolloEngordeMixto : MigracionEsquemas.SeguimientoPolloEngorde;

        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Datos");
        PonerEncabezados(ws, esquema);

        // Hoja "Alimento": movimientos de inventario (entradas al galpón, traslados, recepciones) que
        // se aplican antes del consumo. Opcional — se puede dejar vacía y el archivo funciona igual.
        var wsAlim = pkg.Workbook.Worksheets.Add(MigracionEsquemas.AlimentoEngorde.Hoja);
        PonerEncabezados(wsAlim, MigracionEsquemas.AlimentoEngorde);

        // Hoja "Reproductora": la primera semana del lote (días 1-7), que cruza sola a engorde.
        // También opcional — el lote entero (semana 1 + días 8+ + inventario) cabe en UN archivo.
        var wsRepro = pkg.Workbook.Worksheets.Add(MigracionEsquemas.ReproductoraEnHoja.Hoja);
        PonerEncabezados(wsRepro, MigracionEsquemas.ReproductoraEnHoja);

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
            var columnasAlimento = mixto
                ? new[] { "Tipo Alimento", "Alimento 1 Mixto", "Alimento 2 Mixto" }
                : new[] { "Tipo Alimento", "Alimento 1 H", "Alimento 2 H", "Alimento 1 M", "Alimento 2 M" };
            foreach (var titulo in columnasAlimento)
                DropdownRango(ws, ColumnaLetra(IndiceColumna(esquema, titulo) + 1), rangoAlimentos);
            DropdownRango(wsAlim, ColumnaLetra(IndiceColumna(MigracionEsquemas.AlimentoEngorde, "Alimento") + 1), rangoAlimentos);
        }
        if (lotesUbicados.Count > 0)
            DropdownRango(ws, ColumnaLetra(IndiceColumna(esquema, "Lote") + 1), $"Referencias!$F$2:$F${lotesUbicados.Count + 1}");

        var comunes = new[]
        {
            "• Lote / Granja / Núcleo / Galpón: opcionales. Sin 'Lote', la fila corresponde al lote seleccionado en pantalla.",
            "  Con 'Lote' (nombre tal como aparece en el sistema; mayúsculas/minúsculas indistintas) podés cargar VARIOS lotes",
            "  en un mismo archivo; usá Granja/Núcleo/Galpón para desambiguar nombres repetidos (tabla en 'Referencias').",
            "• Fecha: obligatoria (aaaa-mm-dd o dd/mm/aaaa), no anterior al encaset del lote. Fecha futura solo advierte.",
            "• Unidad Consumo: 'kg' (default si se deja vacía) o 'qq' — aplica al consumo directo Y a los alimentos (1 qq = 45.36 kg).",
            "• Peso: ≥ 0 opcional. Uniformidad: 0 a 100 opcional.",
            "• Días de pesaje (edad 1–7 y múltiplos de 7): si la fila no trae peso se genera una advertencia (no bloquea).",
            "La carga es idempotente por lote+fecha: las fechas ya cargadas (incluidos los primeros días generados por",
            "cruce reproductora) se omiten. Al importar se descuentan las aves por mortalidad/selección y se",
            "recalcula el saldo de alimento de cada lote.",
            "",
            "HOJA 'Alimento' (opcional) — entradas de alimento al inventario, en el MISMO archivo:",
            "• Una fila por movimiento: Fecha · Movimiento · Alimento · Cantidad. El resto es opcional.",
            "• Movimiento: 'Ingreso' (default, alimento que llega de planta/bodega/otra granja), 'Traslado'",
            "  (sale de una ubicación hacia otra), 'Recepción' (acepta un traslado que quedó en tránsito)",
            "  o 'Consumo' (alimento que salió del galpón pero que ningún día de seguimiento descontó:",
            "  la primera semana del lote, ya confirmada en reproductora, o un histórico viejo). El",
            "  'Consumo' pide Referencia para poder distinguir dos salidas iguales del mismo día.",
            "• Granja/Núcleo/Galpón vacíos ⇒ el movimiento va al galpón del lote elegido en pantalla.",
            "  Para Traslado y Recepción indicá además Granja/Núcleo/Galpón Origen.",
            "• Unidad: 'kg' (default) o 'qq' (×45.36), igual que en la hoja Datos.",
            "• Referencia: número de remisión o factura. Sirve para distinguir DOS entradas del mismo",
            "  alimento, el mismo día y por la misma cantidad (sin ella, la segunda se toma por repetida).",
            "• Estos movimientos se aplican ANTES del consumo de la hoja Datos, para que el galpón tenga",
            "  stock cuando el seguimiento lo descuenta. Si el consumo supera lo disponible, el archivo",
            "  se rechaza indicando cuántos kg faltan (no se importa nada).",
            "",
            "HOJA 'Reproductora' (opcional) — la PRIMERA SEMANA del lote, en el MISMO archivo:",
            "• Mismas columnas y reglas que la línea 'Seguimiento Reproductora Engorde': si preferís,",
            "  podés seguir cargándola por separado desde ese menú y dejar esta hoja vacía.",
            "• Los días 1 al 7 se digitan acá (por reproductora); el sistema los cruza solo a los días",
            "  1-7 de pollo engorde. La hoja 'Datos' arranca en el día 8.",
            "• Cada registro queda CONFIRMADO al importar (es lo que dispara ese cruce).",
            "• Con 'Alimento 1/2 H-M' el consumo de esa semana DESCUENTA el stock del galpón.",
            "",
            "ORDEN DE PROCESO (una sola pasada): Alimento → Reproductora → Datos. Así el galpón tiene",
            "stock antes de que la primera semana y los días 8+ lo consuman. Cada hoja se reconoce por",
            "su NOMBRE y las que falten simplemente se omiten.",
        };

        var especificas = mixto
            ? new[]
            {
                "Este lote NO se maneja por sexo: cada columna es el TOTAL MIXTO del día (una sola cifra).",
                "• Mort Mixta / Sel Mixta: enteros ≥ 0 (vacío = 0). Se descuentan de las aves mixtas del lote.",
                "• Consumo Mixto (kg): alimento total del día. Si ponés 'qq' en Unidad Consumo, digitá los quintales acá",
                "  y el sistema los convierte a kg (×45.36).",
                "• Alimento 1/2 Mixto + su consumo: alternativa al consumo directo, eligiendo el alimento del inventario",
                "  (lista desplegable, hoja 'Referencias'). Al importar DESCUENTA el stock de ese alimento; dejá vacío",
                "  'Consumo Mixto (kg)' si usás esta vía.",
                "• QQ Mixtas: quintales del día, SOLO informativos para el informe semanal. NO generan consumo:",
                "  el consumo sale de 'Consumo Mixto (kg)'.",
            }
            : new[]
            {
                "Una fila por día en la hoja 'Datos'.",
                "• Mortalidad / Selección / Error de sexaje: enteros ≥ 0 (vacío = 0).",
                "• Alimento 1/2 H y M: elegí el alimento del inventario (lista desplegable, hoja 'Referencias') y su consumo.",
                "  Hasta dos alimentos por sexo por fecha; al importar se DESCUENTA el inventario de esos alimentos.",
                "• Consumo H/M (directo): solo si NO usás Alimento 1/2 (sin descuento de inventario). Número ≥ 0.",
                "• Lotes MIXTOS: cargá las cantidades en las columnas H (M = 0), igual que el formulario.",
                "• QQ Mixtas / QQ H / QQ M (Panamá): quintales de alimento por categoría, opcionales (≥ 0).",
            };

        HojaInstrucciones(pkg,
            $"Migración Seguimiento Engorde{(mixto ? " (MIXTO)" : "")} — Lote {lote.LoteNombre} (id {loteId})",
            especificas.Concat(comunes).ToArray());

        return (Finalizar(pkg), $"SeguimientoEngorde_Lote{loteId}_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
