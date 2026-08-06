// src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.MovimientosAves.cs
// Hoja "Movimientos Aves" de la carga masiva de Seguimiento LEVANTE y PRODUCCIÓN: movimientos de
// aves UNILATERALES sobre el lote del archivo (decisión del usuario, jul-2026).
//
//   • SALIDA  = aves que salen de ESTE lote hacia otro de la MISMA fase. Valida que el lote
//     contraparte EXISTA (debe estar creado para poder recibir) pero NO lo acredita: ese lote carga
//     su propio Ingreso en su propio archivo. Así dos archivos, uno por lote, nunca doble-cuentan.
//   • INGRESO = aves recibidas "en tránsito". Acredita a ESTE lote sin tocar al lote origen.
//   • VENTA   = descuenta a ESTE lote. En levante queda en venta_aves_cantidad/motivo de la fila
//     diaria (espejo del módulo Movimiento de Aves); PRODUCCIÓN no tiene columnas de venta — el
//     módulo vivo la codifica como sel_h/mortalidad NEGATIVOS, un hack que corrompería los
//     contadores del día, así que acá la venta queda en la auditoría + una nota en observaciones.
//
// La aplicación ESPEJA el camino vivo TrasladoAvesDesdeSegService (fila diaria con es_traslado y
// las columnas traslado_* de cada fase, acumulados del espejo LPL/LPP, aves actuales, auditoría en
// movimiento_aves ya Completada y cohorte del ingreso) pero SIN pasar por
// MovimientoAvesService.CreateAsync — ese camino auto-procesa (inventario_aves + postura +
// seguimiento) y duplicaría los efectos, la regla §2 de la spec de Fase 3. inventario_aves NO se
// toca: el camino vivo desde seguimiento tampoco lo hace (el saldo real sale de los acumulados).
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Migracion;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MigracionService
{
    /// <summary>Largo máximo de <c>lote_aves_cohortes.observaciones</c> (mismo tope del camino vivo).</summary>
    private const int MaxObservacionesCohorteMigracion = 300;

    /// <summary>Lote de la MISMA fase que puede ser contraparte de un movimiento de aves.</summary>
    private sealed record ContraparteLevante(
        int EspejoId, int LoteBaseId, int GranjaId, string Nombre, DateTime? FechaEncaset);

    /// <summary>Una fila ya validada de la hoja "Movimientos Aves".</summary>
    private sealed record MovimientoAvesMigFila(
        int NumeroFila,
        DateTime Fecha,
        MovimientoAvesMigracion Tipo,
        int Hembras,
        int Machos,
        ContraparteLevante? Contraparte,
        string? Motivo,
        string? Observaciones);

    /// <summary>Claves de lectura (título + alias) de una columna de la hoja "Movimientos Aves".</summary>
    private static string[] ClavesMovAves(string titulo) =>
        MigracionEsquemaCalculos.ClavesDeColumna(MigracionEsquemas.MovimientosAvesLevante, titulo);

    /// <summary>
    /// Lee y valida la hoja "Movimientos Aves" (opcional: sin la hoja, lista vacía sin error — los
    /// archivos anteriores se procesan igual). La contraparte se resuelve contra los lotes de la
    /// MISMA fase del archivo (levante ⇒ espejos LPL; producción ⇒ espejos LPP).
    /// </summary>
    private async Task<List<MovimientoAvesMigFila>> LeerHojaMovimientosAvesAsync(
        IFormFile file, int companyId, int loteId, bool esLevante, LotePosturaCtx? loteCtx,
        List<MigracionErrorDto> errores, CancellationToken ct)
    {
        var filas = LeerHojaOpcionalConEsquema(file, MigracionEsquemas.MovimientosAvesLevante, errores);
        if (filas.Count == 0) return new List<MovimientoAvesMigFila>();

        var candidatos = await CargarContrapartesAsync(companyId, esLevante, ct);

        var granjasPorClave = (await _ctx.Farms.AsNoTracking()
                .Where(f => f.CompanyId == companyId)
                .Select(f => new { f.Id, f.Name })
                .ToListAsync(ct))
            .ToLookup(f => MigracionCalculos.NormalizarClave(f.Name), f => f.Id);

        var resultado = new List<MovimientoAvesMigFila>();
        var clavesArchivo = new HashSet<string>();
        var hoyUtc = DateTime.UtcNow.Date;

        foreach (var fila in filas)
        {
            int e0 = errores.Count;

            if (!MigracionCalculos.TryFecha(Celda(fila, ClavesMovAves("Fecha")), out var fecha))
            { errores.Add(new(fila.Numero, "Fecha", null, "Movimientos Aves: fecha inválida o faltante.")); continue; }
            if (!ValidarFechaContraLote(fila, fecha, loteCtx, hoyUtc, errores)) continue;

            var tipoTexto = MigracionCalculos.TextoLimpio(Celda(fila, ClavesMovAves("Tipo")));
            if (!MigracionMovimientosAvesCalculos.TryMovimiento(tipoTexto, out var tipo))
            {
                errores.Add(new(fila.Numero, "Tipo", tipoTexto,
                    "Movimientos Aves: tipo no reconocido. Usá 'Salida' (aves que salen hacia otro lote), 'Ingreso' (aves recibidas en tránsito) o 'Venta'."));
                continue;
            }

            var hembras = EnteroNoNeg(fila, errores, "Hembras", ClavesMovAves("Hembras"));
            var machos = EnteroNoNeg(fila, errores, "Machos", ClavesMovAves("Machos"));
            if (errores.Count > e0) continue;
            if (hembras + machos <= 0)
            {
                errores.Add(new(fila.Numero, "Hembras", null,
                    "Movimientos Aves: indicá cuántas hembras y/o machos se mueven (mayor a 0)."));
                continue;
            }

            var contraparte = ResolverContraparteLevante(
                fila, tipo, loteId, esLevante, candidatos, granjasPorClave, errores);
            if (errores.Count > e0) continue;

            var clave = MigracionMovimientosAvesCalculos.ClaveArchivo(fecha.Date, tipo, hembras, machos);
            if (!clavesArchivo.Add(clave))
            {
                errores.Add(new(fila.Numero, "Fecha", fecha.ToString("yyyy-MM-dd"),
                    "Movimientos Aves: fila repetida (misma fecha, tipo y cantidades). Si son dos viajes reales el mismo día, cargalos como una sola fila sumada — la idempotencia no podría distinguirlos al reimportar."));
                continue;
            }

            resultado.Add(new MovimientoAvesMigFila(
                fila.Numero, fecha.Date, tipo, hembras, machos, contraparte,
                MigracionCalculos.TextoLimpio(Celda(fila, ClavesMovAves("Motivo"))),
                MigracionCalculos.TextoLimpio(Celda(fila, ClavesMovAves("Observaciones")))));
        }

        return resultado;
    }

    /// <summary>Contrapartes posibles: los lotes vivos de la fase, con su espejo y su lote base.</summary>
    private async Task<List<ContraparteLevante>> CargarContrapartesAsync(int companyId, bool esLevante, CancellationToken ct)
    {
        if (esLevante)
            return await _ctx.Set<LotePosturaLevante>().AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.DeletedAt == null
                            && x.LoteId != null && x.LotePosturaLevanteId != null)
                .Join(_ctx.Lotes.AsNoTracking().Where(l => l.DeletedAt == null),
                    x => x.LoteId, l => l.LoteId,
                    (x, l) => new ContraparteLevante(
                        x.LotePosturaLevanteId!.Value, l.LoteId!.Value, x.GranjaId, l.LoteNombre, l.FechaEncaset))
                .ToListAsync(ct);

        return await _ctx.LotePosturaProduccion.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.DeletedAt == null
                        && x.LoteId != null && x.LotePosturaProduccionId != null)
            .Join(_ctx.Lotes.AsNoTracking().Where(l => l.DeletedAt == null),
                x => x.LoteId, l => l.LoteId,
                (x, l) => new ContraparteLevante(
                    x.LotePosturaProduccionId!.Value, l.LoteId!.Value, x.GranjaId, l.LoteNombre, l.FechaEncaset))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Resuelve el "Lote Contraparte" contra los lotes de la fase, por id del lote base o por nombre
    /// (case/acento-insensible), con "Granja Contraparte" como desambiguador opcional. SALIDA la
    /// exige (el lote que recibe debe existir); INGRESO la acepta vacía o no resoluble (Advertencia:
    /// aves en tránsito sin contraparte); VENTA no la usa (si viene, se ignora con Advertencia).
    /// </summary>
    private ContraparteLevante? ResolverContraparteLevante(
        FilaCruda fila, MovimientoAvesMigracion tipo, int loteId, bool esLevante,
        IReadOnlyList<ContraparteLevante> candidatos,
        ILookup<string, int> granjasPorClave,
        List<MigracionErrorDto> errores)
    {
        var texto = MigracionCalculos.TextoLimpio(Celda(fila, ClavesMovAves("Lote Contraparte")));
        var esSalida = tipo == MovimientoAvesMigracion.Salida;
        var fase = esLevante ? "levante" : "producción";

        if (tipo == MovimientoAvesMigracion.Venta)
        {
            if (!string.IsNullOrWhiteSpace(texto))
                errores.Add(new(fila.Numero, "Lote Contraparte", texto,
                    "Venta: no lleva 'Lote Contraparte' (la venta no tiene lote destino); se ignora.", "Advertencia"));
            return null;
        }

        if (string.IsNullOrWhiteSpace(texto))
        {
            if (esSalida)
                errores.Add(new(fila.Numero, "Lote Contraparte", null,
                    "Salida: indicá el 'Lote Contraparte' — el lote que recibe las aves debe existir para poder recibirlas."));
            return null;
        }

        var filtrados = candidatos as IEnumerable<ContraparteLevante>;

        // Granja contraparte (opcional): filtra por nombre de granja o por id numérico.
        var granjaTexto = MigracionCalculos.TextoLimpio(Celda(fila, ClavesMovAves("Granja Contraparte")));
        if (!string.IsNullOrWhiteSpace(granjaTexto))
        {
            var idsGranja = int.TryParse(granjaTexto.Trim(), out var granjaIdNum)
                ? new HashSet<int> { granjaIdNum }
                : granjasPorClave[MigracionCalculos.NormalizarClave(granjaTexto)].ToHashSet();
            filtrados = filtrados.Where(c => idsGranja.Contains(c.GranjaId));
        }

        var matches = (int.TryParse(texto.Trim(), out var loteIdNum)
                ? filtrados.Where(c => c.LoteBaseId == loteIdNum)
                : filtrados.Where(c => MigracionCalculos.NormalizarClave(c.Nombre) == MigracionCalculos.NormalizarClave(texto)))
            .ToList();

        if (matches.Count == 0)
        {
            if (esSalida)
                errores.Add(new(fila.Numero, "Lote Contraparte", texto,
                    $"El lote '{texto}' no existe en {fase} para esta empresa: creá el lote destino antes de cargar la salida."));
            else
                errores.Add(new(fila.Numero, "Lote Contraparte", texto,
                    $"Ingreso: el lote origen '{texto}' no se encontró; el ingreso se registra sin contraparte (aves en tránsito).", "Advertencia"));
            return null;
        }
        if (matches.Count > 1)
        {
            errores.Add(new(fila.Numero, "Lote Contraparte", texto,
                $"'{texto}' coincide con {matches.Count} lotes de {fase}; usá el id del lote o completá 'Granja Contraparte'."));
            return null;
        }

        var contraparte = matches[0];
        if (contraparte.LoteBaseId == loteId)
        {
            errores.Add(new(fila.Numero, "Lote Contraparte", texto,
                "El lote contraparte no puede ser el mismo lote del archivo."));
            return null;
        }
        return contraparte;
    }

    /// <summary>
    /// Advertencia (no bloquea un histórico) cuando el archivo dejaría el saldo de aves en negativo:
    /// aves actuales − bajas del archivo − salidas − ventas + ingresos. El descuento real satura en
    /// 0 en levante (producción espeja al módulo vivo, que no clampa la salida).
    /// </summary>
    private async Task AdvertirSaldoAvesProyectadoAsync(
        TipoMigracion tipo, int loteId,
        IReadOnlyList<Dictionary<string, object?>> filasJson,
        IReadOnlyList<MovimientoAvesMigFila> movimientosAves,
        List<MigracionErrorDto> errores,
        CancellationToken ct)
    {
        if (movimientosAves.Count == 0) return;

        int avesH, avesM;
        if (tipo == TipoMigracion.SeguimientoLevante)
        {
            var aves = await _ctx.Set<LotePosturaLevante>().AsNoTracking()
                .Where(x => x.LoteId == loteId && x.DeletedAt == null)
                .OrderByDescending(x => x.LotePosturaLevanteId)
                .Select(x => new { x.AvesHActual, x.AvesHInicial, x.AvesMActual, x.AvesMInicial })
                .FirstOrDefaultAsync(ct);
            if (aves is null) return;
            avesH = aves.AvesHActual ?? aves.AvesHInicial ?? 0;
            avesM = aves.AvesMActual ?? aves.AvesMInicial ?? 0;
        }
        else
        {
            var aves = await _ctx.LotePosturaProduccion.AsNoTracking()
                .Where(x => x.LoteId == loteId && x.DeletedAt == null)
                .OrderByDescending(x => x.LotePosturaProduccionId)
                .Select(x => new { x.AvesHActual, x.AvesHInicial, x.AvesMActual, x.AvesMInicial })
                .FirstOrDefaultAsync(ct);
            if (aves is null) return;
            avesH = aves.AvesHActual ?? aves.AvesHInicial ?? 0;
            avesM = aves.AvesMActual ?? aves.AvesMInicial ?? 0;
        }

        static int Baja(Dictionary<string, object?> f, string clave) =>
            f.TryGetValue(clave, out var v) && v is int n ? n : 0;

        // Salidas y VENTAS restan; solo el Ingreso suma.
        var proyectado = MigracionMovimientosAvesCalculos.ProyectarSaldoAves(
            avesH, avesM,
            filasJson.Sum(f => Baja(f, "mort_h") + Baja(f, "sel_h") + Baja(f, "err_h")),
            filasJson.Sum(f => Baja(f, "mort_m") + Baja(f, "sel_m") + Baja(f, "err_m")),
            movimientosAves.Where(m => m.Tipo != MovimientoAvesMigracion.Ingreso).Sum(m => m.Hembras),
            movimientosAves.Where(m => m.Tipo != MovimientoAvesMigracion.Ingreso).Sum(m => m.Machos),
            movimientosAves.Where(m => m.Tipo == MovimientoAvesMigracion.Ingreso).Sum(m => m.Hembras),
            movimientosAves.Where(m => m.Tipo == MovimientoAvesMigracion.Ingreso).Sum(m => m.Machos));

        if (proyectado.AlgunoNegativo)
            errores.Add(new(0, "Movimientos Aves", null,
                $"Saldo de aves proyectado tras el archivo: hembras {proyectado.Hembras}, machos {proyectado.Machos}. " +
                "Un negativo indica más bajas/salidas/ventas que aves disponibles.",
                "Advertencia"));
    }

    /// <summary>
    /// Aplica los movimientos de aves DESPUÉS de la fn de seguimiento (las filas diarias del archivo
    /// ya existen y acá solo se EXTIENDEN con sus columnas, sin tocar mortalidad ni selección —
    /// mismo contrato del camino vivo). Idempotente contra <c>movimiento_aves</c>: un movimiento del
    /// mismo día con las mismas cantidades y este lote del mismo lado se OMITE (cubre el reimport
    /// del archivo y los movimientos ya registrados por pantalla). Cada fila aplica y commitea por
    /// separado: un fallo se reporta y no tumba el resto (patrón hoja Alimento).
    /// </summary>
    private async Task<(int Aplicados, int Omitidos)> AplicarMovimientosAvesLevanteAsync(
        IReadOnlyList<MovimientoAvesMigFila> movimientos, TipoMigracion tipo, int loteId,
        List<MigracionErrorDto> fallos, CancellationToken ct)
    {
        if (movimientos.Count == 0) return (0, 0);
        var esLevante = tipo == TipoMigracion.SeguimientoLevante;

        LotePosturaLevante? lpl = null;
        LotePosturaProduccion? lpp = null;
        if (esLevante)
            lpl = await _ctx.Set<LotePosturaLevante>()
                .Where(x => x.LoteId == loteId && x.DeletedAt == null && x.LotePosturaLevanteId != null)
                .OrderByDescending(x => x.LotePosturaLevanteId)
                .FirstOrDefaultAsync(ct);
        else
            lpp = await _ctx.LotePosturaProduccion
                .Where(x => x.LoteId == loteId && x.DeletedAt == null && x.LotePosturaProduccionId != null)
                .OrderByDescending(x => x.LotePosturaProduccionId)
                .FirstOrDefaultAsync(ct);

        if (lpl is null && lpp is null)
        {
            fallos.Add(new(0, "Movimientos Aves", null,
                "No se encontró el espejo del lote en su fase: los movimientos de aves no se aplicaron."));
            return (0, 0);
        }

        // Idempotencia contra la auditoría: movimientos Traslado/Venta no cancelados del rango con
        // este lote como origen (Salida/Venta) o destino (Ingreso). El lote id es global ⇒ no hace
        // falta company. El rango se amplía ±1 día y se recorta en memoria (EF traduce .Date a
        // date_trunc con la TZ de la sesión y pierde bordes — mismo patrón de FechasYaCargadasAsync).
        var desde = movimientos.Min(m => m.Fecha).Date.AddDays(-1);
        var hasta = movimientos.Max(m => m.Fecha).Date.AddDays(2);
        var clavesExistentes = (await _ctx.MovimientoAves.AsNoTracking()
                .Where(m => (m.TipoMovimiento == "Traslado" || m.TipoMovimiento == "Venta")
                            && m.Estado != "Cancelado"
                            && m.FechaMovimiento >= desde && m.FechaMovimiento < hasta
                            && (m.LoteOrigenId == loteId || m.LoteDestinoId == loteId))
                .Select(m => new { m.TipoMovimiento, m.FechaMovimiento, m.CantidadHembras, m.CantidadMachos, m.LoteOrigenId })
                .ToListAsync(ct))
            .Where(m => m.TipoMovimiento == "Traslado" || m.LoteOrigenId == loteId)
            .Select(m => MigracionMovimientosAvesCalculos.ClaveArchivo(
                m.FechaMovimiento.Date,
                m.TipoMovimiento == "Venta" ? MovimientoAvesMigracion.Venta
                    : m.LoteOrigenId == loteId ? MovimientoAvesMigracion.Salida
                    : MovimientoAvesMigracion.Ingreso,
                m.CantidadHembras, m.CantidadMachos))
            .ToHashSet();

        int aplicados = 0, omitidos = 0;
        foreach (var m in movimientos.OrderBy(x => x.Fecha).ThenBy(x => x.NumeroFila))
        {
            if (clavesExistentes.Contains(MigracionMovimientosAvesCalculos.ClaveArchivo(m.Fecha, m.Tipo, m.Hembras, m.Machos)))
            { omitidos++; continue; }

            try
            {
                if (esLevante) await AplicarMovimientoAveMigracionAsync(lpl!, m, ct);
                else await AplicarMovimientoAveProduccionAsync(lpp!, m, ct);
                aplicados++;
            }
            catch (Exception ex)
            {
                fallos.Add(new(m.NumeroFila, "Movimientos Aves", m.Fecha.ToString("yyyy-MM-dd"),
                    $"El movimiento de aves del {m.Fecha:yyyy-MM-dd} no se pudo aplicar: {ex.Message}"));
            }
        }
        return (aplicados, omitidos);
    }

    /// <summary>Aplica UNA fila en LEVANTE: fila diaria + acumulados del LPL + auditoría + cohorte (ingreso).</summary>
    private async Task AplicarMovimientoAveMigracionAsync(
        LotePosturaLevante lpl, MovimientoAvesMigFila m, CancellationToken ct)
    {
        var fechaDia = m.Fecha.Date;
        // Anclada a MEDIODÍA (patrón ResolveMovimientoCreatedAt del inventario): escrita a
        // medianoche, Npgsql legacy la guarda como 00:00 UTC y la RELEE en hora local (19:00 del día
        // ANTERIOR en Bogotá) ⇒ la clave de idempotencia compararía otro día calendario y el
        // reimport aplicaría los movimientos dos veces (visto en el smoke).
        var fechaAncla = fechaDia.AddHours(12);
        var fechaUtc = DateTime.UtcNow;
        var loteTxt = lpl.LoteId!.Value.ToString();
        var esSalida = m.Tipo == MovimientoAvesMigracion.Salida;
        var totalAves = m.Hembras + m.Machos;

        // Fila diaria por fecha CALENDARIO (rango ±1 día + recorte en memoria: la fn inserta la fecha
        // como ::timestamptz y compararla con == exacto pierde filas según la TZ de la sesión).
        var rangoDesde = fechaDia.AddDays(-1);
        var rangoHasta = fechaDia.AddDays(2);
        var seg = (await _ctx.SeguimientoDiario
                .Where(s => s.TipoSeguimiento == "levante" && s.LoteId == loteTxt
                            && (s.ReproductoraId == null || s.ReproductoraId == "")
                            && s.Fecha >= rangoDesde && s.Fecha < rangoHasta)
                .ToListAsync(ct))
            .FirstOrDefault(s => s.Fecha.Date == fechaDia);

        if (seg is null)
        {
            seg = new SeguimientoDiario
            {
                TipoSeguimiento = "levante",
                LoteId = loteTxt,
                LotePosturaLevanteId = lpl.LotePosturaLevanteId,
                Fecha = fechaAncla,
                MortalidadHembras = 0, MortalidadMachos = 0,
                SelH = 0, SelM = 0,
                ErrorSexajeHembras = 0, ErrorSexajeMachos = 0,
                Ciclo = "Traslado",
                TipoAlimento = "—",
                CreatedByUserId = _current.UserGuid?.ToString() ?? _current.UserId.ToString(),
                CreatedAt = fechaUtc
            };
            _ctx.SeguimientoDiario.Add(seg);
        }

        var contraparteNombre = m.Contraparte?.Nombre;
        var esVenta = m.Tipo == MovimientoAvesMigracion.Venta;
        if (esSalida)
        {
            lpl.LevanteTrasladoSalidaHembras += m.Hembras;
            lpl.LevanteTrasladoSalidaMachos += m.Machos;
            lpl.AvesHActual = Math.Max(0, (lpl.AvesHActual ?? 0) - m.Hembras);
            lpl.AvesMActual = Math.Max(0, (lpl.AvesMActual ?? 0) - m.Machos);

            seg.TrasladoSalidaHembras += m.Hembras;
            seg.TrasladoSalidaMachos += m.Machos;
            seg.TrasladoAvesSalida = (seg.TrasladoAvesSalida ?? 0) + totalAves;
            seg.EsTraslado = true;
            seg.TrasladoDireccion = "SALIDA";
            seg.TrasladoLoteContraparteId = m.Contraparte?.EspejoId;
            seg.TrasladoGranjaContraparteId = m.Contraparte?.GranjaId;
            var destinoTxt = $"Traslado SALIDA → {contraparteNombre}";
            seg.Observaciones = string.IsNullOrWhiteSpace(seg.Observaciones)
                ? $"{destinoTxt}. {m.Observaciones ?? ""}".Trim()
                : $"{seg.Observaciones} | {destinoTxt}";
        }
        else if (esVenta)
        {
            // Mismo efecto que el módulo Movimiento de Aves con tipo Venta sobre levante: la cantidad
            // queda en venta_aves_* de la fila diaria (NO en columnas de traslado ni acumulados) y el
            // descuento va sobre las aves actuales con clamp en 0.
            lpl.AvesHActual = Math.Max(0, (lpl.AvesHActual ?? 0) - m.Hembras);
            lpl.AvesMActual = Math.Max(0, (lpl.AvesMActual ?? 0) - m.Machos);

            seg.VentaAvesCantidad = (seg.VentaAvesCantidad ?? 0) + totalAves;
            seg.VentaAvesMotivo = m.Motivo ?? seg.VentaAvesMotivo;
            if (!string.IsNullOrWhiteSpace(m.Observaciones))
                seg.Observaciones = string.IsNullOrWhiteSpace(seg.Observaciones)
                    ? m.Observaciones
                    : $"{seg.Observaciones} | {m.Observaciones}";
        }
        else
        {
            lpl.LevanteTrasladoIngresoHembras += m.Hembras;
            lpl.LevanteTrasladoIngresoMachos += m.Machos;
            lpl.AvesHActual = (lpl.AvesHActual ?? 0) + m.Hembras;
            lpl.AvesMActual = (lpl.AvesMActual ?? 0) + m.Machos;

            seg.TrasladoIngresoHembras += m.Hembras;
            seg.TrasladoIngresoMachos += m.Machos;
            seg.TrasladoAvesEntrante = (seg.TrasladoAvesEntrante ?? 0) + totalAves;
            seg.EsTraslado = true;
            seg.TrasladoDireccion = "INGRESO";
            seg.TrasladoLoteContraparteId = m.Contraparte?.EspejoId;
            seg.TrasladoGranjaContraparteId = m.Contraparte?.GranjaId;
            var origenTxt = contraparteNombre is null
                ? "Traslado INGRESO (aves en tránsito)"
                : $"Traslado INGRESO ← {contraparteNombre}";
            seg.Observaciones = string.IsNullOrWhiteSpace(seg.Observaciones)
                ? $"{origenTxt}. {m.Observaciones ?? ""}".Trim()
                : $"{seg.Observaciones} | {origenTxt}";
        }

        seg.UpdatedAt = fechaUtc;

        await GuardarAuditoriaYCohorteAsync(
            m, fechaAncla, fechaUtc, esSalida, esVenta,
            loteBaseId: lpl.LoteId!.Value, granjaId: lpl.GranjaId, companyId: lpl.CompanyId, ct);
    }

    /// <summary>
    /// Aplica UNA fila en PRODUCCIÓN, espejando <c>TrasladoAvesDesdeSegService</c> (rama producción):
    /// misma fila diaria y acumulados <c>produccion_traslado_*</c>; la salida NO clampa en 0 (paridad
    /// con el módulo vivo). La VENTA no tiene columnas propias en producción: descuenta las aves,
    /// deja la nota en observaciones y la cantidad queda en la auditoría.
    /// </summary>
    private async Task AplicarMovimientoAveProduccionAsync(
        LotePosturaProduccion lpp, MovimientoAvesMigFila m, CancellationToken ct)
    {
        var fechaDia = m.Fecha.Date;
        var fechaAncla = fechaDia.AddHours(12);
        var fechaUtc = DateTime.UtcNow;
        var loteBaseId = lpp.LoteId!.Value;
        var esSalida = m.Tipo == MovimientoAvesMigracion.Salida;
        var esVenta = m.Tipo == MovimientoAvesMigracion.Venta;

        var rangoDesde = fechaDia.AddDays(-1);
        var rangoHasta = fechaDia.AddDays(2);
        var seg = (await _ctx.SeguimientoProduccion
                .Where(s => s.LoteId == loteBaseId && s.Fecha >= rangoDesde && s.Fecha < rangoHasta)
                .ToListAsync(ct))
            .FirstOrDefault(s => s.Fecha.Date == fechaDia);

        if (seg is null)
        {
            seg = new SeguimientoProduccion
            {
                LoteId = loteBaseId,
                LotePosturaProduccionId = lpp.LotePosturaProduccionId,
                Fecha = fechaAncla,
                MortalidadH = 0, MortalidadM = 0,
                SelH = 0, SelM = 0,
                ErrorSexajeHembras = 0, ErrorSexajeMachos = 0,
                TipoAlimento = "—",
                CompanyId = lpp.CompanyId,
                CreatedByUserId = _current.UserId,
                CreatedAt = fechaUtc
            };
            _ctx.SeguimientoProduccion.Add(seg);
        }

        var contraparteNombre = m.Contraparte?.Nombre;
        if (esSalida)
        {
            lpp.ProduccionTrasladoSalidaHembras += m.Hembras;
            lpp.ProduccionTrasladoSalidaMachos += m.Machos;
            lpp.AvesHActual = (lpp.AvesHActual ?? 0) - m.Hembras;
            lpp.AvesMActual = (lpp.AvesMActual ?? 0) - m.Machos;

            seg.TrasladoSalidaHembras += m.Hembras;
            seg.TrasladoSalidaMachos += m.Machos;
            seg.TrasladoHembras = (seg.TrasladoHembras ?? 0) + m.Hembras;
            seg.TrasladoMachos = (seg.TrasladoMachos ?? 0) + m.Machos;
            seg.LoteDestinoId = m.Contraparte?.EspejoId;
            seg.GranjaDestinoId = m.Contraparte?.GranjaId;
            seg.FechaTraslado = fechaAncla;
            seg.EsTraslado = true;
            seg.TrasladoDireccion = "SALIDA";
            seg.TrasladoLoteContraparteId = m.Contraparte?.EspejoId;
            seg.TrasladoGranjaContraparteId = m.Contraparte?.GranjaId;
            var destinoTxt = $"Traslado SALIDA → {contraparteNombre}";
            seg.TrasladoObservaciones = string.IsNullOrWhiteSpace(seg.TrasladoObservaciones)
                ? $"{destinoTxt}. {m.Observaciones ?? ""}".Trim()
                : $"{seg.TrasladoObservaciones} | {destinoTxt}";
        }
        else if (esVenta)
        {
            lpp.AvesHActual = Math.Max(0, (lpp.AvesHActual ?? 0) - m.Hembras);
            lpp.AvesMActual = Math.Max(0, (lpp.AvesMActual ?? 0) - m.Machos);

            var ventaTxt = $"Venta de aves: {m.Hembras} H / {m.Machos} M" +
                           (string.IsNullOrWhiteSpace(m.Motivo) ? "" : $" ({m.Motivo})");
            seg.Observaciones = string.IsNullOrWhiteSpace(seg.Observaciones)
                ? $"{ventaTxt}. {m.Observaciones ?? ""}".Trim()
                : $"{seg.Observaciones} | {ventaTxt}";
        }
        else
        {
            lpp.ProduccionTrasladoIngresoHembras += m.Hembras;
            lpp.ProduccionTrasladoIngresoMachos += m.Machos;
            lpp.AvesHActual = (lpp.AvesHActual ?? 0) + m.Hembras;
            lpp.AvesMActual = (lpp.AvesMActual ?? 0) + m.Machos;

            seg.TrasladoIngresoHembras += m.Hembras;
            seg.TrasladoIngresoMachos += m.Machos;
            seg.EsTraslado = true;
            seg.TrasladoDireccion = "INGRESO";
            seg.TrasladoLoteContraparteId = m.Contraparte?.EspejoId;
            seg.TrasladoGranjaContraparteId = m.Contraparte?.GranjaId;
            var origenTxt = contraparteNombre is null
                ? "Traslado INGRESO (aves en tránsito)"
                : $"Traslado INGRESO ← {contraparteNombre}";
            seg.TrasladoObservaciones = string.IsNullOrWhiteSpace(seg.TrasladoObservaciones)
                ? $"{origenTxt}. {m.Observaciones ?? ""}".Trim()
                : $"{seg.TrasladoObservaciones} | {origenTxt}";
        }

        seg.UpdatedAt = fechaUtc;
        seg.UpdatedByUserId = _current.UserId;

        await GuardarAuditoriaYCohorteAsync(
            m, fechaAncla, fechaUtc, esSalida, esVenta,
            loteBaseId, granjaId: lpp.GranjaId, companyId: lpp.CompanyId, ct);
    }

    /// <summary>
    /// Auditoría Completada en <c>movimiento_aves</c> SIN auto-procesar (nunca
    /// <c>MovimientoAvesService.CreateAsync</c>: doble conteo) + cohorte del INGRESO (edad heredada
    /// del lote origen; informativa, nunca tumba la carga). Común a las dos fases.
    /// </summary>
    private async Task GuardarAuditoriaYCohorteAsync(
        MovimientoAvesMigFila m, DateTime fechaAncla, DateTime fechaUtc, bool esSalida, bool esVenta,
        int loteBaseId, int granjaId, int companyId, CancellationToken ct)
    {
        var movimiento = new MovimientoAves
        {
            NumeroMovimiento = $"MGA-{fechaUtc:yyyyMMdd}-{Guid.NewGuid():N}"[..24],
            FechaMovimiento = fechaAncla,
            TipoMovimiento = esVenta ? "Venta" : "Traslado",
            CantidadHembras = m.Hembras,
            CantidadMachos = m.Machos,
            CantidadMixtas = 0,
            Estado = "Completado",
            FechaProcesamiento = fechaUtc,
            MotivoMovimiento = esVenta ? m.Motivo : null,
            Observaciones = string.IsNullOrWhiteSpace(m.Observaciones)
                ? "Carga masiva de seguimiento"
                : $"Carga masiva de seguimiento. {m.Observaciones}",
            UsuarioMovimientoId = _current.UserId,
            LoteOrigenId = esSalida || esVenta ? loteBaseId : m.Contraparte?.LoteBaseId,
            GranjaOrigenId = esSalida || esVenta ? granjaId : m.Contraparte?.GranjaId,
            LoteDestinoId = esSalida ? m.Contraparte?.LoteBaseId : esVenta ? null : loteBaseId,
            GranjaDestinoId = esSalida ? m.Contraparte?.GranjaId : esVenta ? null : granjaId,
            CompanyId = companyId,
            CreatedByUserId = _current.UserId,
            CreatedAt = fechaUtc
        };
        _ctx.MovimientoAves.Add(movimiento);
        await _ctx.SaveChangesAsync(ct);

        if (!esSalida && !esVenta && m.Contraparte is { FechaEncaset: DateTime encaset })
        {
            var observaciones = $"Traslado desde {m.Contraparte.Nombre} (carga masiva)";
            if (observaciones.Length > MaxObservacionesCohorteMigracion)
                observaciones = observaciones[..MaxObservacionesCohorteMigracion];

            _ctx.LoteAvesCohortes.Add(new LoteAvesCohorte
            {
                CompanyId = companyId,
                LoteId = loteBaseId,
                LoteOrigenId = m.Contraparte.LoteBaseId,
                MovimientoAvesId = movimiento.Id,
                // Ubicación de procedencia CONGELADA (la granja viene en la contraparte de la hoja).
                GranjaOrigenId = m.Contraparte.GranjaId,
                FechaIngreso = DateOnly.FromDateTime(m.Fecha.Date),
                FechaEncasetCohorte = DateOnly.FromDateTime(encaset),
                CantidadHembras = m.Hembras,
                CantidadMachos = m.Machos,
                Observaciones = observaciones,
                CreatedByUserId = _current.UserId,
                CreatedAt = fechaUtc
            });
            await _ctx.SaveChangesAsync(ct);
        }
    }
}
