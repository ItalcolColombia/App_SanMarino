// src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.Historicos.cs
// Fase 2 — Seguimientos históricos (Levante / Producción). Elegibilidad + plantilla por lote +
// parse/validación en C#; la INSERCIÓN MASIVA la hace la BD (funciones plpgsql set-based que
// insertan el estado final idempotente y recomputan aves_h_actual/m_actual una sola vez).
//
// El INVENTARIO de alimento lo mueve C# alrededor de esa inserción (ver MigracionService.AlimentoPostura.cs):
//   1. hoja "Alimento" (ingresos/traslados/recepciones/consumos sueltos) ANTES, para que el galpón
//      tenga stock cuando el seguimiento lo descuenta;
//   2. simulación del balance, que RECHAZA el archivo entero si el stock no alcanza;
//   3. después de la función SQL, el consumo por ítem de los días realmente persistidos, delegando en
//      el mismo servicio que usa el alta manual.
// Sin esto, migrar un histórico dejaba la granja con kilos que en la realidad ya se habían consumido.
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Migracion;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MigracionService
{
    /// <summary>Datos del lote de postura que condicionan el parseo (gates de fecha y de huevos).</summary>
    private sealed record LotePosturaCtx(
        int LoteId,
        string LoteNombre,
        DateTime? FechaEncaset,
        bool CapturaHuevosLevante,
        bool ClasificacionHuevoPorItems);

    /// <summary>Consumo por ítem de un día ya parseado, para descontarlo del inventario al importar.</summary>
    private sealed record ConsumoDiaPostura(DateTime Fecha, IReadOnlyList<ItemSeguimientoDto> Items);

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

    /// <summary>
    /// Lote + los flags de la EMPRESA DUEÑA DE LA GRANJA (no la del token): es la resolución
    /// fail-closed del patrón multi-empresa. Sin granja resoluble los flags quedan apagados, que es
    /// el comportamiento seguro (las columnas nuevas se ignoran con advertencia).
    /// </summary>
    private async Task<LotePosturaCtx?> ResolverLotePosturaCtxAsync(int loteId, CancellationToken ct)
    {
        var lote = await _ctx.Lotes.AsNoTracking()
            .Where(l => l.LoteId == loteId)
            .Select(l => new { l.LoteNombre, l.GranjaId, l.FechaEncaset })
            .FirstOrDefaultAsync(ct);
        if (lote is null) return null;

        var flags = await _ctx.Farms.AsNoTracking()
            .Where(f => f.Id == lote.GranjaId)
            .Join(_ctx.Set<Company>().AsNoTracking(), f => f.CompanyId, c => c.Id,
                (f, c) => new { c.CapturaHuevosEnLevante, c.ClasificacionHuevoPorItems })
            .FirstOrDefaultAsync(ct);

        return new LotePosturaCtx(loteId, lote.LoteNombre, lote.FechaEncaset,
            flags?.CapturaHuevosEnLevante ?? false,
            flags?.ClasificacionHuevoPorItems ?? false);
    }

    /// <summary>Claves de lectura (título + alias) de una columna del esquema de postura.</summary>
    private static string[] ClavesPostura(TipoMigracion tipo, string titulo) =>
        MigracionEsquemaCalculos.ClavesDeColumna(MigracionEsquemas.Para(tipo), titulo);

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
        var lotePosturaCtx = await ResolverLotePosturaCtxAsync(loteId, ct);
        var ctxInv = await ResolverContextoInventarioPosturaAsync(loteId, ct);
        var (alimentos, _) = await CargarAlimentosEmpresaAsync(companyId, ct);
        var huevoItems = lotePosturaCtx?.ClasificacionHuevoPorItems == true && !esLevante
            ? await CargarItemsHuevoEmpresaAsync(companyId, ct)
            : new List<(int Id, string Codigo, string Nombre, string? TipoHuevo)>();

        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Datos");
        PonerEncabezados(ws, MigracionEsquemas.Para(tipo));

        // Hoja "Alimento": las entradas de inventario del período. Sin ella el archivo se procesa
        // igual que siempre, pero el consumo por ítem no encontraría stock que descontar.
        var wsAlim = pkg.Workbook.Worksheets.Add(MigracionEsquemas.AlimentoPostura.Hoja);
        PonerEncabezados(wsAlim, MigracionEsquemas.AlimentoPostura);

        // Hoja "Movimientos Aves" (las dos fases): movimientos UNILATERALES del lote — Salida valida
        // el destino sin acreditarlo; Ingreso acredita las aves en tránsito; Venta descuenta.
        var wsMovAves = pkg.Workbook.Worksheets.Add(MigracionEsquemas.MovimientosAvesLevante.Hoja);
        PonerEncabezados(wsMovAves, MigracionEsquemas.MovimientosAvesLevante);

        // Hoja "Movimientos Huevos" (solo producción): traslados a planta y ventas de huevos.
        if (!esLevante)
            PonerEncabezados(
                pkg.Workbook.Worksheets.Add(MigracionEsquemas.MovimientosHuevosProduccion.Hoja),
                MigracionEsquemas.MovimientosHuevosProduccion);

        ExcelWorksheet? wsHuevos = null;
        if (huevoItems.Count > 0)
        {
            wsHuevos = pkg.Workbook.Worksheets.Add(MigracionEsquemas.HuevosPostura.Hoja);
            PonerEncabezados(wsHuevos, MigracionEsquemas.HuevosPostura);
        }

        // Referencias: los alimentos del inventario de la empresa (nombre + código) y, si aplica, los
        // ítems de huevo. Son los valores que el parseo acepta en las columnas correspondientes.
        var wsRef = pkg.Workbook.Worksheets.Add("Referencias");
        wsRef.Cells[1, 1].Value = "Alimento (nombre)";
        wsRef.Cells[1, 2].Value = "Código";
        for (int i = 0; i < alimentos.Count; i++)
        {
            wsRef.Cells[i + 2, 1].Value = alimentos[i].Nombre;
            wsRef.Cells[i + 2, 2].Value = alimentos[i].Codigo;
        }
        if (huevoItems.Count > 0)
        {
            wsRef.Cells[1, 4].Value = "Ítem huevo (nombre)";
            wsRef.Cells[1, 5].Value = "Código";
            wsRef.Cells[1, 6].Value = "Tipo";
            for (int i = 0; i < huevoItems.Count; i++)
            {
                wsRef.Cells[i + 2, 4].Value = huevoItems[i].Nombre;
                wsRef.Cells[i + 2, 5].Value = huevoItems[i].Codigo;
                wsRef.Cells[i + 2, 6].Value = huevoItems[i].TipoHuevo;
            }
        }

        // Lotes de la MISMA fase: los valores que acepta "Lote Contraparte" (Movimientos Aves).
        var lotesFaseRef = (await CargarContrapartesAsync(companyId, esLevante, ct))
            .Join((await _ctx.Farms.AsNoTracking().Where(f => f.CompanyId == companyId)
                    .Select(f => new { f.Id, f.Name }).ToListAsync(ct)),
                c => c.GranjaId, f => f.Id, (c, f) => new { c.LoteBaseId, c.Nombre, Granja = f.Name })
            .DistinctBy(x => x.LoteBaseId)
            .OrderBy(x => x.Nombre)
            .ToList();

        wsRef.Cells[1, 8].Value = esLevante ? "Lote levante (nombre)" : "Lote producción (nombre)";
        wsRef.Cells[1, 9].Value = "Id lote";
        wsRef.Cells[1, 10].Value = "Granja";
        for (int i = 0; i < lotesFaseRef.Count; i++)
        {
            wsRef.Cells[i + 2, 8].Value = lotesFaseRef[i].Nombre;
            wsRef.Cells[i + 2, 9].Value = lotesFaseRef[i].LoteBaseId;
            wsRef.Cells[i + 2, 10].Value = lotesFaseRef[i].Granja;
        }
        wsRef.Cells[wsRef.Dimension?.Address ?? "A1"].AutoFitColumns();

        if (alimentos.Count > 0)
        {
            var rango = $"Referencias!$A$2:$A${alimentos.Count + 1}";
            foreach (var titulo in new[] { "Alimento 1 H", "Alimento 2 H", "Alimento 1 M", "Alimento 2 M" })
                DropdownRango(ws, ColumnaLetra(IndiceColumna(MigracionEsquemas.Para(tipo), titulo) + 1), rango);
            DropdownRango(wsAlim, ColumnaLetra(IndiceColumna(MigracionEsquemas.AlimentoPostura, "Alimento") + 1), rango);
        }
        if (lotesFaseRef.Count > 0)
            DropdownRango(wsMovAves, ColumnaLetra(IndiceColumna(MigracionEsquemas.MovimientosAvesLevante, "Lote Contraparte") + 1),
                $"Referencias!$H$2:$H${lotesFaseRef.Count + 1}");
        if (wsHuevos is not null && huevoItems.Count > 0)
            DropdownRango(wsHuevos, ColumnaLetra(IndiceColumna(MigracionEsquemas.HuevosPostura, "Ítem") + 1),
                $"Referencias!$D$2:$D${huevoItems.Count + 1}");

        var nivel = ctxInv?.ManejaPorGalpon == true
            ? $"galpón ({ctxInv.NucleoId}/{ctxInv.GalponId})"
            : "granja";

        var instrucciones = new List<string>
        {
            "Una fila por día en la hoja 'Datos'. Todas las filas corresponden a ESTE lote.",
            "• Fecha: obligatoria (formato aaaa-mm-dd o dd/mm/aaaa).",
            "• Mortalidad/Selección/Error de sexaje: enteros ≥ 0 (vacío = 0).",
            "• Consumo: en kg salvo que uses la columna 'Unidad Consumo' con 'qq' (1 qq = 45,36 kg).",
            "",
            "ALIMENTO E INVENTARIO",
            $"• Este lote maneja el alimento a nivel {nivel}.",
            "• Si completás 'Alimento 1/2 H-M' + su consumo, ese consumo DESCUENTA el stock real (igual",
            "  que el registro diario). Si además ponés 'Consumo H/M (kg)', ese número se ignora.",
            "• Si solo ponés 'Consumo H/M (kg)', se guarda como consumo directo y NO toca el inventario.",
            "• La hoja 'Alimento' carga las ENTRADAS del período (Ingreso / Traslado / Recepción) y",
            "  consumos sueltos. Se aplican ANTES del seguimiento, para que haya stock que descontar.",
            "• Si el archivo consume más de lo que hay, se rechaza ENTERO indicando cuánto falta.",
            "",
            esLevante
                ? "• Peso/Uniformidad: opcionales."
                : "• Huevos: podés cargar Total/Incubable o las 11 categorías (si cargás categorías, el total y los incubables se calculan de ellas). Etapa: 1, 2 o 3.",
        };
        if (esLevante)
            instrucciones.Add("• Pesaje y agua: 'Coef. Variación H/M' (0-100), 'Observaciones Pesaje', 'Consumo Agua (L)', 'pH Agua' (0-14), 'ORP Agua (mV)' y 'Temperatura Agua (°C)' son opcionales y se guardan tal cual en el registro del día. El C.V. es el que alimenta la columna 'C.V.%' del reporte semanal de levante.");
        if (esLevante && lotePosturaCtx?.CapturaHuevosLevante == true)
            instrucciones.Add("• Huevos en levante: podés cargar las 11 categorías (+ peso) en cualquier semana; el total y los incubables se calculan del desglose.");
        instrucciones.Add("• Hoja 'Movimientos Aves': movimientos de aves de ESTE lote. 'Salida' descuenta acá y exige que el 'Lote Contraparte' exista en la misma fase (NO se le acreditan las aves: ese lote carga su propio Ingreso en su archivo). 'Ingreso' suma acá las aves recibidas en tránsito, sin tocar al lote origen. 'Venta' descuenta acá (con su 'Motivo'; sin contraparte).");
        instrucciones.Add("• No cargues en 'Movimientos Aves' movimientos que ya se registraron por pantalla, ni repitas la misma fila: se duplicarían. Dos movimientos iguales el mismo día van como una sola fila sumada.");
        if (!esLevante)
        {
            instrucciones.Add("• Hoja 'Movimientos Huevos': salidas de huevos de ESTE lote por las 11 categorías. 'Traslado' = envío a planta; 'Venta' = venta (con 'Motivo' y 'Descripción'). Descuentan de la disponibilidad del lote (espejo); si no alcanzan, el archivo se rechaza.");
            instrucciones.Add("• Pesaje y agua: 'Peso H/M (g)', 'Uniformidad', 'Coef. Variación', 'Observaciones Pesaje', 'Consumo Agua (L)', 'pH Agua' (0-14), 'ORP Agua (mV)' y 'Temperatura Agua (°C)' son opcionales y se guardan tal cual en el registro del día.");
        }
        if (wsHuevos is not null)
            instrucciones.Add("• Hoja 'Huevos': clasificación por ítem del catálogo (una fila por fecha e ítem). No la combines con las 11 categorías de la hoja 'Datos'.");
        instrucciones.Add("La carga es idempotente: reimportar el mismo archivo no duplica filas ni vuelve a descontar inventario.");

        HojaInstrucciones(pkg, $"Migración Seguimiento {(esLevante ? "Levante" : "Producción")} — Lote {lote.LoteNombre} (id {loteId})",
            instrucciones.ToArray());

        return (Finalizar(pkg), $"Seguimiento_{(esLevante ? "Levante" : "Produccion")}_Lote{loteId}_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    // ── Import ───────────────────────────────────────────────────────────────
    private async Task<MigracionResultDto> ProcesarSeguimientoLevanteAsync(IFormFile file, bool dryRun, bool permitirParcial, int companyId, MigracionContextoDto ctx, CancellationToken ct)
    {
        const TipoMigracion tipo = TipoMigracion.SeguimientoLevante;
        if (ctx.LoteId is not int loteId) return ErrorContexto(tipo, dryRun, "Seleccioná un lote elegible antes de importar.");
        if (!await EsLoteElegibleAsync(tipo, companyId, loteId, ct)) return ErrorContexto(tipo, dryRun, "El lote no está habilitado para migración de Levante.");

        var loteCtx = await ResolverLotePosturaCtxAsync(loteId, ct);
        var ctxInv = await ResolverContextoInventarioPosturaAsync(loteId, ct);

        var errores = new List<MigracionErrorDto>();
        List<FilaCruda> filas;
        using (var stream = file.OpenReadStream())
            filas = LeerDatosConEsquema(stream, MigracionEsquemas.Para(tipo), errores);
        if (errores.Any(e => e.Severidad == "Error")) return ResultadoConErrores(tipo, dryRun, filas.Count, errores);

        var movimientosAlimento = ctxInv is null
            ? new List<MovimientoAlimentoFila>()
            : await LeerHojaAlimentoPosturaAsync(file, companyId, ctxInv, errores, ct);

        var movimientosAves = await LeerHojaMovimientosAvesAsync(file, companyId, loteId, esLevante: true, loteCtx, errores, ct);

        if (filas.Count == 0 && movimientosAlimento.Count == 0 && movimientosAves.Count == 0 && errores.Count == 0)
            return ResultadoVacio(tipo, dryRun);

        var (_, alimentosPorClave) = await CargarAlimentosEmpresaAsync(companyId, ct);
        var filasJson = new List<Dictionary<string, object?>>();
        var consumos = new List<ConsumoDiaPostura>();
        var fechasVistas = new HashSet<DateTime>();
        var hoyUtc = DateTime.UtcNow.Date;

        foreach (var fila in filas)
        {
            if (!MigracionCalculos.TryFecha(Celda(fila, "fecha"), out var fecha))
            { errores.Add(new(fila.Numero, "Fecha", null, "Fecha inválida o faltante.")); continue; }
            if (!fechasVistas.Add(fecha)) { errores.Add(new(fila.Numero, "Fecha", fecha.ToString("yyyy-MM-dd"), "Fecha repetida en el archivo.")); continue; }

            int e0 = errores.Count;
            if (!ValidarFechaContraLote(fila, fecha, loteCtx, hoyUtc, errores)) continue;

            var mortH = EnteroNoNeg(fila, errores, "Mort H", ClavesPostura(tipo, "Mort H"));
            var mortM = EnteroNoNeg(fila, errores, "Mort M", ClavesPostura(tipo, "Mort M"));
            var selH = EnteroNoNeg(fila, errores, "Sel H", ClavesPostura(tipo, "Sel H"));
            var selM = EnteroNoNeg(fila, errores, "Sel M", ClavesPostura(tipo, "Sel M"));
            var errH = EnteroNoNeg(fila, errores, "Error Sexaje H", ClavesPostura(tipo, "Error Sexaje H"));
            var errM = EnteroNoNeg(fila, errores, "Error Sexaje M", ClavesPostura(tipo, "Error Sexaje M"));
            var consH = DecimalNoNeg(fila, errores, "Consumo H (kg)", ClavesPostura(tipo, "Consumo H (kg)"));
            var consM = DecimalNoNeg(fila, errores, "Consumo M (kg)", ClavesPostura(tipo, "Consumo M (kg)"));
            var pesoH = DobleOpc(fila, errores, "Peso H (g)", ClavesPostura(tipo, "Peso H (g)"));
            var pesoM = DobleOpc(fila, errores, "Peso M (g)", ClavesPostura(tipo, "Peso M (g)"));
            var unifH = DobleOpc(fila, errores, "Uniformidad H", ClavesPostura(tipo, "Uniformidad H"));
            var unifM = DobleOpc(fila, errores, "Uniformidad M", ClavesPostura(tipo, "Uniformidad M"));

            // Pesaje y agua (opcionales), a la par de lo que ya acepta producción. El C.V. importa
            // especialmente: los reportes semanales de levante lo leen de cv_hembras/cv_machos y hasta
            // acá ninguna vía de captura lo escribía.
            var cvH = Porcentaje0a100(fila, errores, "Coef. Variación H", ClavesPostura(tipo, "Coef. Variación H"));
            var cvM = Porcentaje0a100(fila, errores, "Coef. Variación M", ClavesPostura(tipo, "Coef. Variación M"));
            var obsPesaje = MigracionCalculos.TextoLimpio(Celda(fila, ClavesPostura(tipo, "Observaciones Pesaje")));
            var aguaDiario = DobleNoNeg(fila, errores, "Consumo Agua (L)", ClavesPostura(tipo, "Consumo Agua (L)"));
            var aguaPh = DobleOpc(fila, errores, "pH Agua", ClavesPostura(tipo, "pH Agua"));
            if (aguaPh is < 0 or > 14)
            {
                errores.Add(new(fila.Numero, "pH Agua", aguaPh.Value.ToString("0.##"),
                    "pH Agua: se esperaba un valor entre 0 y 14."));
                aguaPh = null;
            }
            var aguaOrp = DobleOpc(fila, errores, "ORP Agua (mV)", ClavesPostura(tipo, "ORP Agua (mV)"));
            var aguaTemp = DobleOpc(fila, errores, "Temperatura Agua (°C)", ClavesPostura(tipo, "Temperatura Agua (°C)"));

            var unidad = LeerUnidadConsumo(fila, errores);
            var (itemsH, itemsM) = LeerAlimentosPostura(tipo, fila, errores, alimentosPorClave, unidad);
            (consH, consM) = ResolverConsumoPostura(tipo, fila, errores, itemsH, itemsM, consH, consM, unidad);

            var (huevos, pesoHuevo) = LeerHuevosPostura(tipo, fila, errores);
            if (errores.Count > e0) continue;

            // Levante captura huevos si la empresa lo tiene habilitado (misma regla del modal,
            // HuevosLevanteCalculos: el tab es fijo, sin gate de semana; una fecha anterior al
            // encaset ya la rechazó ValidarFechaContraLote para toda la fila).
            if (MigracionPosturaCalculos.TraeHuevos(huevos, null, null, pesoHuevo) &&
                loteCtx?.CapturaHuevosLevante != true)
            {
                errores.Add(new(fila.Numero, "Huevo Limpio", null,
                    "Esta empresa no captura huevos en levante: las columnas de huevo se ignoran.", "Advertencia"));
                huevos = HuevosClasificacion.Cero; pesoHuevo = null;
            }

            var jsonFila = new Dictionary<string, object?>
            {
                ["lote_id"] = loteId,
                ["fecha"] = fecha.ToString("yyyy-MM-dd"),
                ["mort_h"] = mortH, ["mort_m"] = mortM, ["sel_h"] = selH, ["sel_m"] = selM,
                ["err_h"] = errH, ["err_m"] = errM,
                ["cons_h"] = consH, ["cons_m"] = consM,
                ["tipo_alimento"] = ResolverTipoAlimento(fila, itemsH, itemsM),
                ["peso_h"] = pesoH, ["peso_m"] = pesoM, ["unif_h"] = unifH, ["unif_m"] = unifM,
                ["observaciones"] = MigracionCalculos.TextoLimpio(Celda(fila, "observaciones")),
                ["cv_h"] = cvH, ["cv_m"] = cvM, ["obs_pesaje"] = obsPesaje,
                ["agua_diario"] = aguaDiario, ["agua_ph"] = aguaPh,
                ["agua_orp"] = aguaOrp, ["agua_temp"] = aguaTemp
            };
            AgregarMetadataItems(jsonFila, itemsH, itemsM);
            AgregarHuevosClasificacion(jsonFila, huevos, pesoHuevo, incluirTotales: true);

            filasJson.Add(jsonFila);
            if (itemsH.Count + itemsM.Count > 0)
                consumos.Add(new ConsumoDiaPostura(fecha, itemsH.Concat(itemsM).ToList()));
        }

        // Aviso (no bloquea) si el archivo dejaría el saldo de aves en negativo — solo con la hoja
        // "Movimientos Aves" presente, para no cambiar el reporte de los archivos históricos.
        await AdvertirSaldoAvesProyectadoAsync(tipo, loteId, filasJson, movimientosAves, errores, ct);

        return await EjecutarHistoricoPosturaAsync(tipo, dryRun, permitirParcial, filas.Count, errores, filasJson,
            consumos, movimientosAlimento, ctxInv, loteId,
            MigracionPosturaCalculos.ReferenciaConsumoLevante,
            json => _ctx.Database.SqlQueryRaw<int>(
                "SELECT public.fn_migracion_seguimiento_levante({0}, {1}, {2}::jsonb) AS \"Value\"",
                companyId, _current.UserId.ToString(), json).FirstAsync(ct), ct,
            movimientosAves);
    }

    private async Task<MigracionResultDto> ProcesarSeguimientoProduccionAsync(IFormFile file, bool dryRun, bool permitirParcial, int companyId, MigracionContextoDto ctx, CancellationToken ct)
    {
        const TipoMigracion tipo = TipoMigracion.SeguimientoProduccion;
        if (ctx.LoteId is not int loteId) return ErrorContexto(tipo, dryRun, "Seleccioná un lote elegible antes de importar.");
        if (!await EsLoteElegibleAsync(tipo, companyId, loteId, ct)) return ErrorContexto(tipo, dryRun, "El lote no está habilitado (requiere Levante cerrado + liquidado + lote Producción).");

        var loteCtx = await ResolverLotePosturaCtxAsync(loteId, ct);
        var ctxInv = await ResolverContextoInventarioPosturaAsync(loteId, ct);

        var errores = new List<MigracionErrorDto>();
        List<FilaCruda> filas;
        using (var stream = file.OpenReadStream())
            filas = LeerDatosConEsquema(stream, MigracionEsquemas.Para(tipo), errores);
        if (errores.Any(e => e.Severidad == "Error")) return ResultadoConErrores(tipo, dryRun, filas.Count, errores);

        var movimientosAlimento = ctxInv is null
            ? new List<MovimientoAlimentoFila>()
            : await LeerHojaAlimentoPosturaAsync(file, companyId, ctxInv, errores, ct);

        // Hoja "Huevos" (clasificación por ítem del catálogo — Santa Reyes). Gateada por el flag de la
        // empresa dueña de la granja, fail-closed.
        var huevoItemsPorFecha = await LeerHojaHuevosPosturaAsync(file, companyId, loteCtx, errores, ct);

        var movimientosAves = await LeerHojaMovimientosAvesAsync(file, companyId, loteId, esLevante: false, loteCtx, errores, ct);
        var movimientosHuevos = LeerHojaMovimientosHuevos(file, loteCtx, errores);

        if (filas.Count == 0 && movimientosAlimento.Count == 0 && movimientosAves.Count == 0
            && movimientosHuevos.Count == 0 && errores.Count == 0)
            return ResultadoVacio(tipo, dryRun);

        var (_, alimentosPorClave) = await CargarAlimentosEmpresaAsync(companyId, ct);
        var filasJson = new List<Dictionary<string, object?>>();
        var consumos = new List<ConsumoDiaPostura>();
        var fechasVistas = new HashSet<DateTime>();
        var hoyUtc = DateTime.UtcNow.Date;

        // Arrastre de huevos del levante: el cierre deja una fila de producción con los huevos
        // arrastrados y nada más. Si el Excel trae ese día — el caso normal, porque es el PRIMER día de
        // producción — se FUSIONA en vez de omitirse (los huevos se suman, el resto lo define el Excel),
        // exactamente como hace el alta manual.
        var fechasDelArchivo = new List<DateTime>();
        foreach (var f in filas)
            if (MigracionCalculos.TryFecha(Celda(f, "fecha"), out var fx)) fechasDelArchivo.Add(fx.Date);
        var arrastres = await ArrastresPendientesAsync(loteId, fechasDelArchivo, ct);

        foreach (var fila in filas)
        {
            if (!MigracionCalculos.TryFecha(Celda(fila, "fecha"), out var fecha))
            { errores.Add(new(fila.Numero, "Fecha", null, "Fecha inválida o faltante.")); continue; }
            if (!fechasVistas.Add(fecha)) { errores.Add(new(fila.Numero, "Fecha", fecha.ToString("yyyy-MM-dd"), "Fecha repetida en el archivo.")); continue; }

            int e0 = errores.Count;
            if (!ValidarFechaContraLote(fila, fecha, loteCtx, hoyUtc, errores)) continue;

            var mortH = EnteroNoNeg(fila, errores, "Mort H", ClavesPostura(tipo, "Mort H"));
            var mortM = EnteroNoNeg(fila, errores, "Mort M", ClavesPostura(tipo, "Mort M"));
            var selH = EnteroNoNeg(fila, errores, "Sel H", ClavesPostura(tipo, "Sel H"));
            var selM = EnteroNoNeg(fila, errores, "Sel M", ClavesPostura(tipo, "Sel M"));
            var consH = DecimalNoNeg(fila, errores, "Consumo H (kg)", ClavesPostura(tipo, "Consumo H (kg)"));
            var consM = DecimalNoNeg(fila, errores, "Consumo M (kg)", ClavesPostura(tipo, "Consumo M (kg)"));
            var huevoTot = EnteroNoNegNull(fila, errores, "Huevo Total", ClavesPostura(tipo, "Huevo Total"));
            var huevoInc = EnteroNoNegNull(fila, errores, "Huevo Incubable", ClavesPostura(tipo, "Huevo Incubable"));
            var etapa = EnteroNoNegNull(fila, errores, "Etapa", ClavesPostura(tipo, "Etapa"));

            // Campos completos del modal (opcionales): error de sexaje, pesaje corporal y agua.
            var errH = EnteroNoNeg(fila, errores, "Error Sexaje H", ClavesPostura(tipo, "Error Sexaje H"));
            var errM = EnteroNoNeg(fila, errores, "Error Sexaje M", ClavesPostura(tipo, "Error Sexaje M"));
            var pesoH = DobleNoNeg(fila, errores, "Peso H (g)", ClavesPostura(tipo, "Peso H (g)"));
            var pesoM = DobleNoNeg(fila, errores, "Peso M (g)", ClavesPostura(tipo, "Peso M (g)"));
            var unifLote = Porcentaje0a100(fila, errores, "Uniformidad", ClavesPostura(tipo, "Uniformidad"));
            var cvLote = Porcentaje0a100(fila, errores, "Coef. Variación", ClavesPostura(tipo, "Coef. Variación"));
            var obsPesaje = MigracionCalculos.TextoLimpio(Celda(fila, ClavesPostura(tipo, "Observaciones Pesaje")));
            var aguaDiario = DobleNoNeg(fila, errores, "Consumo Agua (L)", ClavesPostura(tipo, "Consumo Agua (L)"));
            var aguaPh = DobleOpc(fila, errores, "pH Agua", ClavesPostura(tipo, "pH Agua"));
            if (aguaPh is < 0 or > 14)
            {
                errores.Add(new(fila.Numero, "pH Agua", aguaPh.Value.ToString("0.##"),
                    "pH Agua: se esperaba un valor entre 0 y 14."));
                aguaPh = null;
            }
            var aguaOrp = DobleOpc(fila, errores, "ORP Agua (mV)", ClavesPostura(tipo, "ORP Agua (mV)"));
            var aguaTemp = DobleOpc(fila, errores, "Temperatura Agua (°C)", ClavesPostura(tipo, "Temperatura Agua (°C)"));

            var unidad = LeerUnidadConsumo(fila, errores);
            var (itemsH, itemsM) = LeerAlimentosPostura(tipo, fila, errores, alimentosPorClave, unidad);
            (consH, consM) = ResolverConsumoPostura(tipo, fila, errores, itemsH, itemsM, consH, consM, unidad);

            var (categorias, pesoHuevo) = LeerHuevosPostura(tipo, fila, errores);

            // Etapa: el modal exige 1..3 y la carga masiva aceptaba cualquier entero, dejando filas que
            // ningún reporte por etapa podía ubicar.
            if (!MigracionPosturaCalculos.EtapaValida(etapa))
                errores.Add(new(fila.Numero, "Etapa", etapa?.ToString(),
                    $"Etapa: se esperaba un valor entre {MigracionPosturaCalculos.EtapaMinima} y {MigracionPosturaCalculos.EtapaMaxima}."));

            var itemsHuevoDelDia = huevoItemsPorFecha.GetValueOrDefault(fecha.Date);
            if (MigracionPosturaCalculos.MezclaFuentesDeHuevos(itemsHuevoDelDia is { Count: > 0 }, categorias, huevoInc))
                errores.Add(new(fila.Numero, "Huevo Limpio", fecha.ToString("yyyy-MM-dd"),
                    "El día trae clasificación por ítems (hoja 'Huevos') Y categorías/incubables en la hoja 'Datos'. Dejá una sola fuente: con ítems, el total sale de ellos y las 11 categorías quedan en cero."));

            if (MigracionPosturaCalculos.TotalDiscrepaDeCategorias(huevoTot, categorias))
                errores.Add(new(fila.Numero, "Huevo Total", huevoTot?.ToString(),
                    $"'Huevo Total' ({huevoTot}) no coincide con la suma de las categorías ({categorias.Totales}); manda el desglose.", "Advertencia"));

            if (errores.Count > e0) continue;

            // Día del cierre del levante: los huevos del Excel se SUMAN a los arrastrados y los totales
            // se derivan del resultado (mismo criterio que AplicarRequestSobreFilaArrastre).
            var esMergeArrastre = arrastres.TryGetValue(fecha.Date, out var arrastre);
            if (esMergeArrastre)
            {
                categorias = HuevosLevanteCalculos.Sumar(arrastre.Huevos, categorias);
                errores.Add(new(fila.Numero, "Fecha", fecha.ToString("yyyy-MM-dd"),
                    $"Es el día del cierre del levante: los {arrastre.Huevos.Totales} huevos arrastrados se SUMAN a los de esta fila (total {categorias.Totales}); el resto de los datos los define el archivo.",
                    "Advertencia"));
            }

            var (totalFinal, incFinal) = itemsHuevoDelDia is { Count: > 0 }
                ? (itemsHuevoDelDia.Sum(i => i.Cantidad), 0)
                : MigracionPosturaCalculos.TotalesHuevoEfectivos(categorias, esMergeArrastre ? null : huevoTot, esMergeArrastre ? null : huevoInc);

            var jsonFila = new Dictionary<string, object?>
            {
                ["lote_id"] = loteId,
                ["fecha"] = fecha.ToString("yyyy-MM-dd"),
                ["mort_h"] = mortH, ["mort_m"] = mortM, ["sel_h"] = selH, ["sel_m"] = selM,
                ["err_h"] = errH, ["err_m"] = errM,
                ["cons_h"] = consH, ["cons_m"] = consM,
                // Con ítems de alimento por sexo el consumo se persiste separado H/M, igual que
                // ProduccionService; sin ellos se conserva el contrato histórico (todo en cons_kg_h).
                ["cons_separado"] = itemsH.Count + itemsM.Count > 0,
                ["es_merge_arrastre"] = esMergeArrastre,
                ["tipo_alimento"] = ResolverTipoAlimento(fila, itemsH, itemsM),
                ["huevo_tot"] = totalFinal, ["huevo_inc"] = incFinal, ["peso_huevo"] = pesoHuevo,
                ["etapa"] = MigracionPosturaCalculos.EtapaEfectiva(etapa),
                ["observaciones"] = MigracionCalculos.TextoLimpio(Celda(fila, "observaciones")),
                ["peso_h"] = pesoH, ["peso_m"] = pesoM,
                ["uniformidad"] = unifLote, ["coef_variacion"] = cvLote, ["obs_pesaje"] = obsPesaje,
                ["agua_diario"] = aguaDiario, ["agua_ph"] = aguaPh,
                ["agua_orp"] = aguaOrp, ["agua_temp"] = aguaTemp
            };
            AgregarMetadataItems(jsonFila, itemsH, itemsM, itemsHuevoDelDia);
            // La marca del arrastre se conserva (para que siga siendo idempotente) y se cierra la
            // ventana de merge: a partir de acá ese día vuelve a admitir un solo registro.
            if (esMergeArrastre) ConservarMarcaArrastre(jsonFila, arrastre.Metadata);
            // Con clasificación por ítems las 11 columnas quedan en cero (regla de HuevoItemsCalculos).
            AgregarHuevosClasificacion(jsonFila,
                itemsHuevoDelDia is { Count: > 0 } ? HuevosClasificacion.Cero : categorias,
                pesoHuevo, incluirTotales: false);

            filasJson.Add(jsonFila);
            if (itemsH.Count + itemsM.Count > 0)
                consumos.Add(new ConsumoDiaPostura(fecha, itemsH.Concat(itemsM).ToList()));
        }

        // Fechas de la hoja "Huevos" que no tienen fila en "Datos": no hay dónde guardarlas.
        foreach (var fecha in huevoItemsPorFecha.Keys.Where(f => !fechasVistas.Contains(f)))
            errores.Add(new(0, "Ítem", fecha.ToString("yyyy-MM-dd"),
                $"La hoja 'Huevos' trae el {fecha:yyyy-MM-dd} pero la hoja 'Datos' no tiene esa fecha; esos huevos no se cargarían."));

        // Aves: aviso de saldo proyectado. Huevos: la disponibilidad proyectada por categoría es un
        // ERROR (mismo criterio del módulo vivo, que rechaza el traslado sin huevos suficientes).
        await AdvertirSaldoAvesProyectadoAsync(tipo, loteId, filasJson, movimientosAves, errores, ct);
        await ValidarDisponibilidadHuevosProyectadaAsync(loteId, filasJson, movimientosHuevos, errores, ct);

        return await EjecutarHistoricoPosturaAsync(tipo, dryRun, permitirParcial, filas.Count, errores, filasJson,
            consumos, movimientosAlimento, ctxInv, loteId,
            MigracionPosturaCalculos.ReferenciaConsumoProduccion,
            json => _ctx.Database.SqlQueryRaw<int>(
                "SELECT public.fn_migracion_seguimiento_produccion({0}, {1}, {2}::jsonb) AS \"Value\"",
                companyId, _current.UserId, json).FirstAsync(ct), ct,
            movimientosAves, movimientosHuevos);
    }

    // ── Parseo compartido ────────────────────────────────────────────────────
    /// <summary>
    /// Fecha anterior al encasetamiento ⇒ Error (el día no existe para ese lote y ningún indicador
    /// por edad podría ubicarlo). Fecha futura ⇒ Advertencia, igual que en engorde.
    /// </summary>
    private static bool ValidarFechaContraLote(
        FilaCruda fila, DateTime fecha, LotePosturaCtx? loteCtx, DateTime hoyUtc, List<MigracionErrorDto> errores)
    {
        if (loteCtx?.FechaEncaset is DateTime encaset && fecha.Date < encaset.Date)
        {
            errores.Add(new(fila.Numero, "Fecha", fecha.ToString("yyyy-MM-dd"),
                $"{loteCtx.LoteNombre}: la fecha es anterior al encasetamiento del lote ({encaset:yyyy-MM-dd})."));
            return false;
        }
        if (fecha.Date > hoyUtc)
            errores.Add(new(fila.Numero, "Fecha", fecha.ToString("yyyy-MM-dd"),
                "La fecha es futura; verificá que sea intencional.", "Advertencia"));
        return true;
    }

    /// <summary>Los cuatro slots de alimento del inventario (2 por sexo), reusando el parseo de engorde.</summary>
    private static (List<ItemSeguimientoDto> H, List<ItemSeguimientoDto> M) LeerAlimentosPostura(
        TipoMigracion tipo, FilaCruda fila, List<MigracionErrorDto> errores,
        Dictionary<string, List<(int Id, string Nombre)>> alimentos, string unidad)
    {
        var itemsH = new List<ItemSeguimientoDto>();
        var itemsM = new List<ItemSeguimientoDto>();
        foreach (var (destino, sexo) in new[] { (itemsH, "H"), (itemsM, "M") })
            foreach (var n in new[] { 1, 2 })
                LeerAlimentoSlot(fila, errores, alimentos, unidad, destino,
                    $"Alimento {n} {sexo}", ClavesPostura(tipo, $"Alimento {n} {sexo}"),
                    $"Consumo Alimento {n} {sexo}", ClavesPostura(tipo, $"Consumo Alimento {n} {sexo}"));
        return (itemsH, itemsM);
    }

    /// <summary>
    /// Consumo final del día por sexo. Con ítems del inventario manda la suma de esos ítems (y el
    /// consumo suelto se ignora con Advertencia, espejo de engorde); sin ítems, el consumo directo
    /// convertido a kg según la unidad de la fila.
    /// </summary>
    private static (decimal? H, decimal? M) ResolverConsumoPostura(
        TipoMigracion tipo, FilaCruda fila, List<MigracionErrorDto> errores,
        List<ItemSeguimientoDto> itemsH, List<ItemSeguimientoDto> itemsM,
        decimal? consH, decimal? consM, string unidad)
    {
        if (MigracionPosturaCalculos.ConsumoDirectoIgnorado(itemsH.Count, consH))
        {
            var col = EtiquetaColumna(fila, "Consumo H (kg)", ClavesPostura(tipo, "Consumo H (kg)"));
            errores.Add(new(fila.Numero, col, consH!.Value.ToString("0.###"),
                $"Se ignora el consumo directo de '{col}': la fila trae alimentos del inventario (el consumo sale de ellos).", "Advertencia"));
        }
        if (MigracionPosturaCalculos.ConsumoDirectoIgnorado(itemsM.Count, consM))
            errores.Add(new(fila.Numero, "Consumo M (kg)", consM!.Value.ToString("0.###"),
                "Se ignora el consumo directo M: la fila trae alimentos del inventario (el consumo sale de ellos).", "Advertencia"));

        var finalH = itemsH.Count > 0 ? (decimal)itemsH.Sum(i => i.Cantidad) : MigracionCalculos.ConsumoAKilos(consH, unidad);
        var finalM = itemsM.Count > 0 ? (decimal)itemsM.Sum(i => i.Cantidad) : MigracionCalculos.ConsumoAKilos(consM, unidad);
        return (finalH, finalM);
    }

    /// <summary>Las 11 categorías de la clasificadora + el peso del huevo.</summary>
    private static (HuevosClasificacion Categorias, double? PesoHuevo) LeerHuevosPostura(
        TipoMigracion tipo, FilaCruda fila, List<MigracionErrorDto> errores)
    {
        int Cat(string titulo) => EnteroNoNeg(fila, errores, titulo, ClavesPostura(tipo, titulo));
        var categorias = new HuevosClasificacion(
            Limpio: Cat("Huevo Limpio"), Tratado: Cat("Huevo Tratado"), Sucio: Cat("Huevo Sucio"),
            Deforme: Cat("Huevo Deforme"), Blanco: Cat("Huevo Blanco"), DobleYema: Cat("Huevo Doble Yema"),
            Piso: Cat("Huevo Piso"), Pequeno: Cat("Huevo Pequeño"), Roto: Cat("Huevo Roto"),
            Desecho: Cat("Huevo Desecho"), Otro: Cat("Huevo Otro"));
        var peso = DobleOpc(fila, errores, "Peso Huevo (g)", ClavesPostura(tipo, "Peso Huevo (g)"));
        return (categorias, peso);
    }

    /// <summary>
    /// Texto de <c>tipo_alimento</c>: el de la celda o, si no viene, los nombres de los alimentos
    /// usados (mismo criterio que engorde, para que la columna nunca quede vacía cuando sí hubo
    /// alimento identificado). Recortado al largo de la columna: dos alimentos por sexo con nombres
    /// largos superan los 100 caracteres y la fn entera moría con 22001 (value too long).
    /// El tope pasó de 100 a <see cref="TipoAlimentoCalculos.MaxLongitud"/> con la migración
    /// <c>AmpliarTipoAlimentoSeguimientos</c> (2026-08-06), así que la carga masiva ya no mutila
    /// el texto salvo en casos extremos.
    /// </summary>
    private static string? ResolverTipoAlimento(FilaCruda fila, List<ItemSeguimientoDto> itemsH, List<ItemSeguimientoDto> itemsM)
    {
        var texto = MigracionCalculos.TextoLimpio(Celda(fila, "tipo alimento"));
        if (string.IsNullOrWhiteSpace(texto))
        {
            var partes = new List<string>();
            if (itemsH.Count > 0) partes.Add("H: " + string.Join(" + ", itemsH.Select(i => i.Nombre)));
            if (itemsM.Count > 0) partes.Add("M: " + string.Join(" + ", itemsM.Select(i => i.Nombre)));
            if (partes.Count == 0) return null;
            texto = string.Join(" / ", partes);
        }
        return TipoAlimentoCalculos.Recortar(texto);
    }

    /// <summary>
    /// Escribe el <c>metadata</c> jsonb del día con el mismo shape que el alta manual
    /// (<c>itemsHembras</c>/<c>itemsMachos</c>, y <c>huevoItems</c> en producción). Es lo que lee el
    /// front para pintar el desglose por alimento en la tabla diaria.
    /// </summary>
    private static void AgregarMetadataItems(
        Dictionary<string, object?> jsonFila,
        List<ItemSeguimientoDto> itemsH, List<ItemSeguimientoDto> itemsM,
        IReadOnlyList<HuevoItemSeguimientoDto>? huevoItems = null)
    {
        if (itemsH.Count == 0 && itemsM.Count == 0 && (huevoItems is null || huevoItems.Count == 0)) return;

        var meta = new Dictionary<string, object?>();
        if (itemsH.Count > 0) meta["itemsHembras"] = itemsH.Select(SerializarItem).ToList();
        if (itemsM.Count > 0) meta["itemsMachos"] = itemsM.Select(SerializarItem).ToList();
        if (huevoItems is { Count: > 0 })
            meta[HuevoItemsCalculos.MetadataKey] = huevoItems.Select(h => new Dictionary<string, object?>
            {
                ["catalogItemId"] = h.CatalogItemId,
                ["codigo"] = h.Codigo,
                ["nombre"] = h.Nombre,
                ["tipoHuevo"] = h.TipoHuevo,
                ["cantidad"] = h.Cantidad,
                ["um"] = h.Um
            }).ToList();

        jsonFila["metadata"] = meta;

        static Dictionary<string, object?> SerializarItem(ItemSeguimientoDto i) => new()
        {
            ["tipoItem"] = i.TipoItem,
            ["catalogItemId"] = i.CatalogItemId,
            ["itemInventarioEcuadorId"] = i.ItemInventarioEcuadorId,
            ["nombre"] = i.Nombre,
            ["cantidad"] = i.Cantidad,
            ["unidad"] = i.Unidad
        };
    }

    /// <summary>
    /// Copia al metadata de la fila la marca de arrastre que ya tenía la fila de producción y CIERRA la
    /// ventana de merge (<c>seguimientoRegistrado</c>), igual que el alta manual. Sin esto, el arrastre
    /// perdería su referencia (dejaría de ser idempotente) o el día admitiría un segundo merge.
    /// </summary>
    private static void ConservarMarcaArrastre(Dictionary<string, object?> jsonFila, JsonDocument? metadataArrastre)
    {
        var actual = jsonFila.TryGetValue("metadata", out var m) && m is Dictionary<string, object?> dict
            ? JsonDocument.Parse(JsonSerializer.Serialize(dict))
            : null;

        var conMarca = HuevosLevanteCalculos.MarcarSeguimientoRegistrado(
            HuevosLevanteCalculos.CopiarMarcaArrastre(actual, metadataArrastre));

        if (conMarca is null) { jsonFila.Remove("metadata"); return; }
        jsonFila["metadata"] = JsonSerializer.Deserialize<JsonElement>(conMarca.RootElement.GetRawText());
    }

    /// <summary>Vuelca las 11 categorías (y, en levante, los totales derivados) al json de la fila.</summary>
    private static void AgregarHuevosClasificacion(
        Dictionary<string, object?> jsonFila, HuevosClasificacion c, double? pesoHuevo, bool incluirTotales)
    {
        jsonFila["huevo_limpio"] = c.Limpio;
        jsonFila["huevo_tratado"] = c.Tratado;
        jsonFila["huevo_sucio"] = c.Sucio;
        jsonFila["huevo_deforme"] = c.Deforme;
        jsonFila["huevo_blanco"] = c.Blanco;
        jsonFila["huevo_doble_yema"] = c.DobleYema;
        jsonFila["huevo_piso"] = c.Piso;
        jsonFila["huevo_pequeno"] = c.Pequeno;
        jsonFila["huevo_roto"] = c.Roto;
        jsonFila["huevo_desecho"] = c.Desecho;
        jsonFila["huevo_otro"] = c.Otro;
        if (incluirTotales)
        {
            // Levante: total e incubables SIEMPRE derivados del desglose (no hay columnas propias).
            jsonFila["huevo_tot"] = c.Totales;
            jsonFila["huevo_inc"] = c.Incubables;
            jsonFila["peso_huevo"] = pesoHuevo;
        }
    }

    /// <summary>
    /// Runner GENÉRICO de carga histórica por función plpgsql (valida → dry-run corta → invoca la fn,
    /// parcial opt-in). Lo usa la venta de engorde, que no mueve inventario de alimento; postura tiene
    /// el suyo (<see cref="EjecutarHistoricoPosturaAsync"/>) porque además simula y descuenta stock.
    /// </summary>
    private async Task<MigracionResultDto> EjecutarHistoricoAsync(
        TipoMigracion tipo, bool dryRun, bool permitirParcial, string nombreArchivo,
        int total, List<MigracionErrorDto> errores, List<Dictionary<string, object?>> filasJson,
        Func<string, Task<int>> invocarFn, CancellationToken ct)
    {
        _ = nombreArchivo; // la auditoría central lo registra en ProcesarAsync
        if (total == 0 && errores.Count == 0) return ResultadoVacio(tipo, dryRun);

        var hayErroresReales = errores.Any(e => e.Severidad == "Error");
        var puedeInsertarParcial = hayErroresReales && !dryRun && permitirParcial && filasJson.Count > 0;

        if (hayErroresReales && !puedeInsertarParcial)
            return ResultadoConErrores(tipo, dryRun, total, errores);

        if (dryRun) return ResultadoOk(tipo, dryRun, total, errores);

        int insertados;
        try
        {
            var json = JsonSerializer.Serialize(filasJson);
            insertados = await invocarFn(json);
        }
        catch (Exception ex)
        {
            var err = new List<MigracionErrorDto>(errores) { new(0, "-", null, $"Error al insertar: {ex.Message}") };
            return ResultadoFallido(tipo, total, err);
        }

        if (puedeInsertarParcial)
        {
            var filasError = errores.Where(e => e.Severidad == "Error" && e.Fila > 0).Select(e => e.Fila).Distinct().Count();
            var (capados, totalReal) = MigracionEsquemaCalculos.LimitarErrores(errores, MaxErroresReportados);
            return new MigracionResultDto(tipo.ToString(), true, total, insertados, filasError, "ProcesadoParcial", dryRun, capados, 0, 0, totalReal);
        }

        return ResultadoOk(tipo, dryRun, total, errores, procesadas: insertados);
    }

    // ── Runner de POSTURA (valida → simula inventario → dry-run corta → alimento → fn BD → descuento → aves) ──
    private async Task<MigracionResultDto> EjecutarHistoricoPosturaAsync(
        TipoMigracion tipo, bool dryRun, bool permitirParcial,
        int total, List<MigracionErrorDto> errores, List<Dictionary<string, object?>> filasJson,
        List<ConsumoDiaPostura> consumos, List<MovimientoAlimentoFila> movimientosAlimento,
        ContextoInventarioPostura? ctxInv, int loteId, Func<long, DateTime, string> referencia,
        Func<string, Task<int>> invocarFn, CancellationToken ct,
        IReadOnlyList<MovimientoAvesMigFila>? movimientosAves = null,
        IReadOnlyList<MovimientoHuevosMigFila>? movimientosHuevos = null)
    {
        if (total == 0 && errores.Count == 0 && movimientosAlimento.Count == 0
            && (movimientosAves?.Count ?? 0) == 0 && (movimientosHuevos?.Count ?? 0) == 0)
            return ResultadoVacio(tipo, dryRun);

        // Fechas ya cargadas: la función SQL las omite, así que su consumo NO debe descontarse otra
        // vez (reimportar el mismo archivo descontaba el inventario dos veces) y además son las que
        // dan el FilasOmitidas real, que en postura siempre reportaba 0.
        var fechasArchivo = filasJson
            .Select(f => DateTime.Parse((string)f["fecha"]!).Date).ToList();
        var yaCargadas = await FechasYaCargadasAsync(tipo, loteId, fechasArchivo, ct);
        var omitidas = fechasArchivo.Count(yaCargadas.Contains);
        var consumosAplicables = consumos.Where(c => !yaCargadas.Contains(c.Fecha.Date)).ToList();

        // Balance de inventario: stock actual + entradas del archivo − salidas del archivo. Se corre
        // también en dry-run: es el único momento en que el usuario puede comparar el saldo proyectado
        // contra su planilla antes de tocar nada.
        IReadOnlyList<SaldoAlimentoProyectado> saldos = Array.Empty<SaldoAlimentoProyectado>();
        if (ctxInv is not null && (movimientosAlimento.Count > 0 || consumosAplicables.Count > 0))
            saldos = await SimularBalancePosturaAsync(ctxInv, movimientosAlimento, consumosAplicables, errores, ct);

        var hayErroresReales = errores.Any(e => e.Severidad == "Error");
        var puedeInsertarParcial = hayErroresReales && !dryRun && permitirParcial && filasJson.Count > 0;

        if (hayErroresReales && !puedeInsertarParcial)
            return ResultadoConErrores(tipo, dryRun, total, errores) with { FilasOmitidas = omitidas };

        foreach (var s in saldos)
            errores.Add(new(0, "Alimento", await NombreAlimentoAsync(s.Posicion.ItemId, ct),
                $"Saldo proyectado de {await NombreAlimentoAsync(s.Posicion.ItemId, ct)}: {s.SaldoInicial:N3} kg + {s.Entradas:N3} entradas − {s.Salidas:N3} consumidos = {s.SaldoFinal:N3} kg.",
                "Advertencia"));

        if (dryRun) return ResultadoOk(tipo, dryRun, total, errores) with { FilasOmitidas = omitidas };

        var fallos = new List<MigracionErrorDto>();

        // El alimento ENTRA primero: el consumo de cada día descuenta de un stock que ya tiene que
        // existir. Invertir el orden es lo que dejaba el galpón en cero.
        var (_, movOmitidos) = await AplicarMovimientosAlimentoAsync(movimientosAlimento, fallos, ct);

        int insertados;
        try
        {
            var json = JsonSerializer.Serialize(filasJson);
            insertados = await invocarFn(json);
        }
        catch (Exception ex)
        {
            var err = new List<MigracionErrorDto>(errores) { new(0, "-", null, $"Error al insertar: {ex.Message}") };
            return ResultadoFallido(tipo, total, err) with { FilasOmitidas = omitidas };
        }

        // Descuento del consumo: sobre los días que de verdad quedaron persistidos, con la referencia
        // que usa el alta manual (necesita el id, que la función devuelve como conteo — se resuelve
        // con una consulta por fecha).
        if (ctxInv is not null && consumosAplicables.Count > 0)
        {
            var ids = await IdsSeguimientoPorFechaAsync(tipo, loteId, consumosAplicables.Select(c => c.Fecha).ToList(), ct);
            var conId = consumosAplicables
                .Where(c => ids.ContainsKey(c.Fecha.Date))
                .Select(c => (c.Fecha, SeguimientoId: ids[c.Fecha.Date], c.Items))
                .ToList();
            await DescontarConsumoSeguimientoAsync(ctxInv, conId, referencia, fallos, ct);
        }

        // Movimientos de aves y de huevos DESPUÉS de la fn: las filas diarias del archivo ya existen
        // y solo se extienden con sus columnas.
        if (movimientosAves is { Count: > 0 })
        {
            var (_, avesOmitidos) = await AplicarMovimientosAvesLevanteAsync(movimientosAves, tipo, loteId, fallos, ct);
            omitidas += avesOmitidos;
        }
        if (movimientosHuevos is { Count: > 0 })
        {
            var (_, huevosOmitidos) = await AplicarMovimientosHuevosAsync(movimientosHuevos, loteId, fallos, ct);
            omitidas += huevosOmitidos;
        }

        // El alta manual recalcula el espejo de huevos tras cada registro; la fn no lo hace nunca.
        // Un recálculo ABSOLUTO al final deja la disponibilidad al día (producción solamente).
        if (tipo == TipoMigracion.SeguimientoProduccion && (insertados > 0 || movimientosHuevos is { Count: > 0 }))
            await RecalcularEspejoHuevosAsync(loteId, fallos, ct);

        errores.AddRange(fallos);
        omitidas += movOmitidos;

        if (puedeInsertarParcial)
        {
            var filasError = errores.Where(e => e.Severidad == "Error" && e.Fila > 0).Select(e => e.Fila).Distinct().Count();
            var (capados, totalReal) = MigracionEsquemaCalculos.LimitarErrores(errores, MaxErroresReportados);
            return new MigracionResultDto(tipo.ToString(), true, total, insertados, filasError, "ProcesadoParcial", dryRun, capados, omitidas, 0, totalReal);
        }

        if (fallos.Any(f => f.Severidad == "Error"))
            return ResultadoConErrores(tipo, dryRun, total, errores) with { FilasProcesadas = insertados, FilasOmitidas = omitidas };

        return ResultadoOk(tipo, dryRun, total, errores, procesadas: insertados) with { FilasOmitidas = omitidas };
    }

    /// <summary>
    /// Simula el archivo completo sobre el stock actual y agrega un ERROR por cada posición que
    /// quedaría en negativo (el archivo se rechaza entero). Devuelve el saldo proyectado por posición
    /// para el reporte del dry-run.
    /// </summary>
    private async Task<IReadOnlyList<SaldoAlimentoProyectado>> SimularBalancePosturaAsync(
        ContextoInventarioPostura ctxInv, List<MovimientoAlimentoFila> movimientos,
        List<ConsumoDiaPostura> consumos, List<MigracionErrorDto> errores, CancellationToken ct)
    {
        var entradas = new Dictionary<PosicionAlimento, decimal>();
        var salidas = new Dictionary<PosicionAlimento, decimal>();

        // Solo cuentan los movimientos que REALMENTE se van a aplicar: los ya presentes en el
        // histórico se omiten al importar, y sumarlos proyectaría un saldo que nunca va a ocurrir.
        var yaAplicados = movimientos.Count > 0
            ? await ClavesMovimientosExistentesAsync(movimientos, ct)
            : new HashSet<string>();

        foreach (var m in movimientos)
        {
            if (yaAplicados.Contains(MigracionAlimentoCalculos.ClaveIdempotencia(
                    m.Movimiento, m.Destino, m.ItemId, m.Fecha, m.CantidadKg, m.Referencia)))
                continue;

            var posDestino = new PosicionAlimento(m.Destino.Normalizada(), m.ItemId);
            if (m.Movimiento is MovimientoAlimento.Ingreso or MovimientoAlimento.Recepcion)
                MigracionAlimentoCalculos.Acumular(entradas, posDestino, m.CantidadKg);
            else if (m.Movimiento is MovimientoAlimento.Consumo)
                MigracionAlimentoCalculos.Acumular(salidas, posDestino, m.CantidadKg);
            else if (m.Movimiento is MovimientoAlimento.Traslado && m.Origen is UbicacionAlimento origen)
            {
                MigracionAlimentoCalculos.Acumular(entradas, posDestino, m.CantidadKg);
                MigracionAlimentoCalculos.Acumular(salidas, new PosicionAlimento(origen.Normalizada(), m.ItemId), m.CantidadKg);
            }
        }

        foreach (var c in consumos) AcumularConsumoItems(salidas, ctxInv, c.Items);

        var posiciones = new HashSet<PosicionAlimento>(entradas.Keys);
        foreach (var p in salidas.Keys) posiciones.Add(p);
        var stockActual = await CargarStockPosicionesAsync(posiciones, ct);

        var donde = ctxInv.ManejaPorGalpon ? "el galpón" : "la granja";
        foreach (var f in MigracionAlimentoCalculos.Simular(stockActual, entradas, salidas))
        {
            var nombre = await NombreAlimentoAsync(f.Posicion.ItemId, ct);
            errores.Add(new(0, "Alimento", nombre,
                $"No alcanza el stock de {nombre} en {donde}: el archivo consume {f.Requerido:N3} kg y solo hay {f.Disponible:N3} kg " +
                $"(faltan {f.Faltante:N3} kg). Cargá la entrada que falta en la hoja 'Alimento' o corregí el consumo."));
        }

        return MigracionAlimentoCalculos.Proyectar(stockActual, entradas, salidas);
    }
}
