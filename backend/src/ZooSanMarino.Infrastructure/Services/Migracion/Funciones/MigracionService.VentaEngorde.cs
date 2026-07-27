// src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.VentaEngorde.cs
// Línea Engorde · Venta (histórico). Elegibilidad reutiliza los lotes de engorde NO cerrados de la
// empresa (mismo universo que la carga de seguimiento engorde).
//
// Parse/validación/prorrateo en C#; la INSERCIÓN la hace la función plpgsql fn_migracion_venta_engorde
// (numero MPE + descuento del contador del lote una vez cuando la venta nace 'Completado'; el trigger
// de BD escribe el histórico VENTA_AVES). Idempotente en la función por
// (empresa + lote + rango de día + cantidades + numero_despacho).
//
// MULTI-LOTE / DESPACHO:
//   Las filas que comparten N° Despacho + Fecha + Granja son UN despacho: comparten factura_id y el
//   peso báscula, que es el peso del CAMIÓN. El peso individual de cada línea sale de
//   MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea — la MISMA función pura que usa la venta
//   por pantalla (3 decimales, residuo a la línea con más aves) ⇒ una carga masiva y una venta
//   digitada producen exactamente los mismos números. Por eso el prorrateo vive acá y no en plpgsql:
//   duplicarlo en SQL sería duplicar el redondeo.
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Migracion;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MigracionService
{
    /// <summary>Fila de venta ya validada, antes de agrupar en despachos.</summary>
    private sealed record FilaVentaEngorde(
        int NumeroFila,
        int LoteId,
        string LoteNombre,
        string GranjaNombre,
        DateTime Fecha,
        int CantH, int CantM, int CantX,
        string? Motivo,
        string? Observaciones,
        double? PesoBruto,
        double? PesoTara,
        int? EdadAves,
        string? Raza,
        string? Placa,
        string? NumeroDespacho,
        int? TotalPollosGalpon,
        TimeOnly? HoraSalida,
        string? GuiaAgrocalidad,
        string? Sellos,
        string? Ayuno,
        string? Conductor,
        string? PlantaDestino,
        string? Descripcion,
        string Estado,
        bool EsVentaMixta)
    {
        public int TotalAves => CantH + CantM + CantX;
    }

    // ── Import ───────────────────────────────────────────────────────────────
    private async Task<MigracionResultDto> ProcesarVentaEngordeAsync(IFormFile file, bool dryRun, bool permitirParcial, int companyId, MigracionContextoDto ctx, CancellationToken ct)
    {
        const TipoMigracion tipo = TipoMigracion.VentaPolloEngorde;
        if (ctx.LoteId is not int loteCtxId) return ErrorContexto(tipo, dryRun, "Seleccioná un lote de engorde antes de importar.");
        var (loteCtx, errLote) = await ResolverLoteEngordeAsync(companyId, loteCtxId, ct);
        if (loteCtx is null) return ErrorContexto(tipo, dryRun, errLote!);

        var errores = new List<MigracionErrorDto>();
        using var stream = file.OpenReadStream();
        var filas = LeerDatosConEsquema(stream, MigracionEsquemas.Para(tipo), errores);
        if (errores.Any(e => e.Severidad == "Error")) return ResultadoConErrores(tipo, dryRun, filas.Count, errores);
        if (filas.Count == 0 && errores.Count == 0) return ResultadoVacio(tipo, dryRun);

        // Universo de lotes para resolver la columna "Lote" por nombre (mismo helper que seguimiento).
        var (lotesUbicados, lotesPorNombre) = await CargarLotesEngordeUbicadosAsync(companyId, ct);
        var loteCtxUbicado = lotesUbicados.FirstOrDefault(l => l.LoteId == loteCtxId)
            ?? new LoteEngordeUbicado(loteCtxId, loteCtx.LoteNombre, loteCtx.FechaEncaset, loteCtx.HoraEncasetamiento, string.Empty, null, null, null, null);

        var ventas = new List<FilaVentaEngorde>();

        foreach (var fila in filas)
        {
            // ── Lote de la fila: por nombres o el seleccionado en pantalla ──
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

            int e0 = errores.Count;
            var cantH = EnteroNoNeg(fila, errores, "Cantidad H", "cantidad h", "cant h", "hembras");
            var cantM = EnteroNoNeg(fila, errores, "Cantidad M", "cantidad m", "cant m", "machos");
            var cantX = EnteroNoNeg(fila, errores, "Cantidad Mixtas", "cantidad mixtas", "cant mixtas", "mixtas");
            // Espejo de Validators.min(0) del front: un peso negativo es dato corrupto, no un valor válido.
            var pesoBruto = DobleNoNeg(fila, errores, "Peso Bruto (kg)", "peso bruto (kg)", "peso bruto");
            var pesoTara = DobleNoNeg(fila, errores, "Peso Tara (kg)", "peso tara (kg)", "peso tara");
            var edad = EnteroNoNegNull(fila, errores, "Edad Aves", "edad aves", "edad");
            var totalPollos = EnteroNoNegNull(fila, errores, "Total Pollos Galpón", "total pollos galpon", "total pollos");
            var horaSalida = HoraOpc(fila, errores, "Hora Salida", "hora salida", "hora");
            var esVentaMixta = BooleanoSiNo(fila, errores, "Venta sobre mixtas", false,
                "venta sobre mixtas", "es venta mixta", "sobre mixtas", "panama");
            if (errores.Count > e0) continue;

            if (cantH + cantM + cantX <= 0)
            { errores.Add(new(fila.Numero, "Cantidad", null, "Debe vender al menos un ave (Cantidad H/M/Mixtas).")); continue; }
            if (pesoBruto is double b && pesoTara is double t && b < t)
            { errores.Add(new(fila.Numero, "Peso", $"{b}/{t}", "El peso bruto no puede ser menor que la tara.")); continue; }
            if (pesoBruto.HasValue != pesoTara.HasValue)
            { errores.Add(new(fila.Numero, "Peso", null, "Indique peso bruto Y peso tara, o deje ambos vacíos.")); continue; }

            // Estado con el que nace la venta (default 'Completado' = comportamiento histórico).
            var estadoTxt = MigracionCalculos.TextoLimpio(Celda(fila, "estado"));
            var estado = "Completado";
            if (estadoTxt is not null)
            {
                var k = MigracionCalculos.NormalizarClave(estadoTxt);
                if (k == "pendiente") estado = "Pendiente";
                else if (k != "completado")
                { errores.Add(new(fila.Numero, "Estado", estadoTxt, "Estado inválido: use 'Completado' o 'Pendiente'.")); continue; }
            }

            // La venta sobre mixtas asigna el split H/M SOBRE las mixtas del lote (Panamá): la
            // columna Cantidad Mixtas no aplica, el stock sale de mixtas y se fuerza mixtas = 0.
            if (esVentaMixta && cantX > 0)
            { errores.Add(new(fila.Numero, "Cantidad Mixtas", cantX.ToString(), "Con 'Venta sobre mixtas' = Sí las aves van en Cantidad H/M (el descuento sale de las mixtas del lote); deje Cantidad Mixtas vacía.")); continue; }

            if (estado == "Completado" && !pesoBruto.HasValue)
                errores.Add(new(fila.Numero, "Peso Bruto (kg)", null,
                    "Venta 'Completado' sin peso báscula: quedará en 0 kg en liquidación e indicadores. Use Estado = 'Pendiente' si la báscula llega después.",
                    "Advertencia"));

            ventas.Add(new FilaVentaEngorde(
                fila.Numero, lote.LoteId, lote.LoteNombre ?? $"Lote {lote.LoteId}", lote.GranjaNombre,
                fecha, cantH, cantM, cantX,
                MigracionCalculos.TextoLimpio(Celda(fila, "motivo")),
                MigracionCalculos.TextoLimpio(Celda(fila, "observaciones")),
                pesoBruto, pesoTara, edad,
                MigracionCalculos.TextoLimpio(Celda(fila, "raza")),
                MigracionCalculos.TextoLimpio(Celda(fila, "placa")),
                MigracionCalculos.TextoLimpio(Celda(fila, "n° despacho", "no despacho", "nro despacho", "numero despacho", "despacho")),
                totalPollos, horaSalida,
                MigracionCalculos.TextoLimpio(Celda(fila, "guía agrocalidad", "guia agrocalidad", "guia")),
                MigracionCalculos.TextoLimpio(Celda(fila, "sellos")),
                MigracionCalculos.TextoLimpio(Celda(fila, "ayuno")),
                MigracionCalculos.TextoLimpio(Celda(fila, "cliente / conductor", "cliente", "conductor")),
                MigracionCalculos.TextoLimpio(Celda(fila, "planta destino", "planta")),
                MigracionCalculos.TextoLimpio(Celda(fila, "descripción", "descripcion")),
                estado, esVentaMixta));
        }

        var filasJson = ArmarDespachosVentaEngorde(ventas, errores);

        return await EjecutarHistoricoAsync(tipo, dryRun, permitirParcial, file.FileName, filas.Count, errores, filasJson,
            json => _ctx.Database.SqlQueryRaw<int>(
                "SELECT public.fn_migracion_venta_engorde({0}, {1}, {2}::jsonb) AS \"Value\"",
                companyId, _current.UserId, json).FirstAsync(ct), ct);
    }

    /// <summary>
    /// Agrupa las filas en DESPACHOS por (N° Despacho + Fecha + Granja), asigna un <c>factura_id</c>
    /// por grupo y reparte el peso del camión entre sus líneas con el mismo prorrateo de la venta por
    /// pantalla. Las filas sin N° Despacho son cada una su propio movimiento suelto (sin factura),
    /// que es el comportamiento histórico de esta carga.
    /// </summary>
    private static List<Dictionary<string, object?>> ArmarDespachosVentaEngorde(
        List<FilaVentaEngorde> ventas, List<MigracionErrorDto> errores)
    {
        var salida = new List<Dictionary<string, object?>>();

        var grupos = ventas.GroupBy(v => string.IsNullOrWhiteSpace(v.NumeroDespacho)
            ? $"S:{v.NumeroFila}"                                                    // suelta
            : $"D:{MigracionCalculos.NormalizarClave(v.NumeroDespacho)}|F:{v.Fecha:yyyy-MM-dd}|G:{MigracionCalculos.NormalizarClave(v.GranjaNombre)}");

        foreach (var grupo in grupos)
        {
            var lineas = grupo.OrderBy(v => v.NumeroFila).ToList();
            var esDespacho = lineas.Count > 1 || !string.IsNullOrWhiteSpace(lineas[0].NumeroDespacho);

            // El peso es el del CAMIÓN: debe ser el mismo en todas las líneas del despacho.
            var conPeso = lineas.Where(l => l.PesoBruto.HasValue).ToList();
            double? pesoBrutoGlobal = conPeso.Count > 0 ? conPeso[0].PesoBruto : null;
            double? pesoTaraGlobal = conPeso.Count > 0 ? conPeso[0].PesoTara : null;
            var contradictoria = conPeso.FirstOrDefault(l =>
                Math.Abs(l.PesoBruto!.Value - pesoBrutoGlobal!.Value) > 0.001 ||
                Math.Abs((l.PesoTara ?? 0d) - (pesoTaraGlobal ?? 0d)) > 0.001);
            if (contradictoria is not null)
            {
                errores.Add(new(contradictoria.NumeroFila, "Peso Bruto (kg)", $"{contradictoria.PesoBruto}",
                    $"El despacho '{lineas[0].NumeroDespacho}' del {lineas[0].Fecha:yyyy-MM-dd} tiene pesos distintos entre sus filas ({pesoBrutoGlobal}/{pesoTaraGlobal} vs {contradictoria.PesoBruto}/{contradictoria.PesoTara}). El peso es el del camión: repítalo igual en todas las filas del despacho."));
                continue;
            }

            var tienePeso = pesoBrutoGlobal.HasValue && pesoTaraGlobal.HasValue;
            var pesoNetoGlobal = tienePeso ? pesoBrutoGlobal!.Value - pesoTaraGlobal!.Value : (double?)null;
            var prorrateo = tienePeso
                ? MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea(
                    pesoBrutoGlobal!.Value, pesoTaraGlobal!.Value, lineas.Select(l => l.TotalAves).ToList())
                : new MovimientoPolloEngordeCalculos.PesoLineaProrrateado[lineas.Count];

            // Una factura por despacho identificado; las ventas sueltas quedan sin factura, como hoy.
            var facturaId = esDespacho ? Guid.NewGuid() : (Guid?)null;

            for (int i = 0; i < lineas.Count; i++)
            {
                var l = lineas[i];
                salida.Add(new Dictionary<string, object?>
                {
                    ["lote_id"] = l.LoteId,
                    ["fecha"] = l.Fecha.ToString("yyyy-MM-dd"),
                    ["cant_h"] = l.CantH, ["cant_m"] = l.CantM, ["cant_x"] = l.CantX,
                    ["motivo"] = l.Motivo,
                    ["observaciones"] = l.Observaciones,
                    ["peso_bruto"] = pesoBrutoGlobal, ["peso_tara"] = pesoTaraGlobal,
                    ["peso_bruto_global"] = pesoBrutoGlobal,
                    ["peso_tara_global"] = pesoTaraGlobal,
                    ["peso_neto_global"] = pesoNetoGlobal,
                    ["peso_bruto_real"] = prorrateo[i].Bruto,
                    ["peso_tara_real"] = prorrateo[i].Tara,
                    ["peso_neto"] = prorrateo[i].Neto,
                    ["promedio_peso_ave"] = prorrateo[i].Promedio,
                    ["edad_aves"] = l.EdadAves,
                    ["raza"] = l.Raza,
                    ["placa"] = l.Placa,
                    ["factura_id"] = facturaId,
                    ["numero_despacho"] = l.NumeroDespacho,
                    ["total_pollos_galpon"] = l.TotalPollosGalpon,
                    ["hora_salida"] = l.HoraSalida?.ToString("HH:mm:ss"),
                    ["guia_agrocalidad"] = l.GuiaAgrocalidad,
                    ["sellos"] = l.Sellos,
                    ["ayuno"] = l.Ayuno,
                    ["conductor"] = l.Conductor,
                    ["planta_destino"] = l.PlantaDestino,
                    ["descripcion"] = l.Descripcion,
                    ["estado"] = l.Estado,
                    ["es_venta_mixta"] = l.EsVentaMixta
                });
            }
        }

        return salida;
    }

    // ── Plantilla ────────────────────────────────────────────────────────────
    private async Task<(byte[] Contenido, string NombreArchivo)> GenerarPlantillaVentaEngordeAsync(int companyId, MigracionContextoDto ctx, CancellationToken ct)
    {
        if (ctx.LoteId is not int loteId)
            throw new ArgumentException("Seleccioná un lote de engorde para descargar su plantilla.");
        var (lote, errLote) = await ResolverLoteEngordeAsync(companyId, loteId, ct);
        if (lote is null) throw new InvalidOperationException(errLote!);

        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Datos");
        PonerEncabezados(ws, MigracionEsquemas.VentaPolloEngorde);

        HojaInstrucciones(pkg, $"Migración Venta Engorde — Lote {lote.LoteNombre} (id {loteId})",
            "Una fila por venta y por lote en la hoja 'Datos'.",
            "• Granja / Núcleo / Galpón / Lote: opcionales. Si indicás 'Lote', la fila se carga en ESE lote (se busca por nombre, sin distinguir mayúsculas ni acentos, y Granja/Núcleo/Galpón sirven para desambiguar). Si los dejás vacíos, la fila va al lote seleccionado en pantalla.",
            "• Fecha: obligatoria (aaaa-mm-dd o dd/mm/aaaa).",
            "• Cantidad H / M / Mixtas: enteros ≥ 0 (vacío = 0); debe venderse al menos un ave por fila.",
            "• Peso Bruto / Peso Tara: opcionales, en kg, ambos o ninguno (bruto ≥ tara). El neto y el promedio por ave se calculan.",
            "• N° Despacho: las filas con el MISMO N° Despacho + Fecha + Granja son un solo despacho (varios lotes en un camión): comparten factura y el peso se reparte entre ellas en proporción a las aves. Repetí el mismo peso bruto/tara en todas las filas del despacho.",
            "• Estado: 'Completado' (default) descuenta las aves del lote. 'Pendiente' deja la venta a la espera de la báscula; el descuento ocurre al confirmarla desde Movimientos.",
            "• Venta sobre mixtas: 'Sí' (Panamá) reparte H/M sobre las MIXTAS del lote — el descuento sale de mixtas y Cantidad Mixtas debe quedar vacía.",
            "• Hora Salida: HH:mm (por ejemplo 06:30).",
            "• Motivo / Edad Aves / Raza / Placa / Observaciones / Total Pollos Galpón / Guía / Sellos / Ayuno / Cliente-Conductor / Planta Destino / Descripción: opcionales.",
            "Es idempotente: la misma venta (lote + fecha + cantidades + N° Despacho) no se duplica, aunque ya se haya registrado desde la pantalla de Movimientos.");

        return (Finalizar(pkg), $"VentaEngorde_Lote{loteId}_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
