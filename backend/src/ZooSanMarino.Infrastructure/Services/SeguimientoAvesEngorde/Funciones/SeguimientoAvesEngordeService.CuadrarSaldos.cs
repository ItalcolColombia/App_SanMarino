// "Cuadrar saldos": valida las filas de un Excel de movimientos de alimento contra el histórico
// unificado del lote y aplica las correcciones (ajuste de fecha, anulación, inserción) más la
// reconciliación de metadata de documento. Partial de SeguimientoAvesEngordeService.
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SeguimientoAvesEngordeService
{
    public async Task<CuadrarSaldosValidarResponseDto> ValidarCuadrarSaldosAsync(
        int loteId,
        IReadOnlyList<FilaExcelCuadrarSaldosDto> filasExcel)
    {
        var companyId = _current.CompanyId;

        var loteInfo = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null)
            .Select(l => new { l.GranjaId, l.NucleoId, l.GalponId })
            .SingleOrDefaultAsync()
            ?? throw new InvalidOperationException($"Lote {loteId} no encontrado.");

        int farmId     = loteInfo.GranjaId;
        string nucId   = (loteInfo.NucleoId ?? "").Trim();
        string galId   = (loteInfo.GalponId ?? "").Trim();

        // Rango de fechas: primer y último seguimiento registrado en la aplicación para este lote.
        // Esto evita mezclar registros de otros lotes que hayan usado el mismo galpón en otras épocas.
        var (fechaMinSeg, fechaMaxSeg) = await CalcularRangoFechasLoteAsync(loteId);

        // Si no hay seguimientos en la aplicación, usar el rango que trae el propio Excel
        if (fechaMinSeg == null && filasExcel.Count > 0)
        {
            var excelDates = filasExcel
                .Select(f => DateTime.TryParseExact(f.Fecha?.Trim(), "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : (DateTime?)null)
                .Where(d => d.HasValue).Select(d => d!.Value).ToList();
            if (excelDates.Count > 0)
            {
                fechaMinSeg = excelDates.Min();
                fechaMaxSeg = excelDates.Max();
            }
        }

        // Cargar movimientos de alimento relevantes del galpón, acotados al rango del lote (sin anulados, sin devoluciones de sistema)
        var histQuery = _ctx.LoteRegistroHistoricoUnificados
            .Where(h => h.CompanyId == companyId
                && !h.Anulado
                && !(h.TipoEvento == "INV_INGRESO"
                     && h.Referencia != null
                     && h.Referencia.StartsWith("Seguimiento aves engorde #"))
                && !((h.Referencia != null && h.Referencia.Contains("devolución por eliminación"))
                   || (h.Referencia != null && h.Referencia.Contains("devolucion por eliminacion")))
                && (h.TipoEvento == "INV_INGRESO"
                    || h.TipoEvento == "INV_TRASLADO_ENTRADA"
                    || h.TipoEvento == "INV_TRASLADO_SALIDA")
                && h.FarmId == farmId
                && (h.NucleoId == null ? "" : h.NucleoId.Trim()) == nucId
                && (h.GalponId == null ? "" : h.GalponId.Trim()) == galId);

        if (fechaMinSeg.HasValue)
            histQuery = histQuery.Where(h => h.FechaOperacion >= fechaMinSeg.Value.Date);
        if (fechaMaxSeg.HasValue)
            histQuery = histQuery.Where(h => h.FechaOperacion <= fechaMaxSeg.Value.Date.AddDays(1).AddTicks(-1));

        var hist = await histQuery
            .OrderBy(h => h.FechaOperacion)
            .ThenBy(h => h.Id)
            .ToListAsync();

        // Agrupar por fecha efectiva
        var histByDate = new Dictionary<string, List<LoteRegistroHistoricoUnificado>>(StringComparer.Ordinal);
        foreach (var h in hist)
        {
            var ymd = YmdHistoricoEfectivo(h);
            if (ymd == null) continue;
            if (!histByDate.TryGetValue(ymd, out var lst)) { lst = new(); histByDate[ymd] = lst; }
            lst.Add(h);
        }

        // Excel por fecha
        var excelByDate = new Dictionary<string, FilaExcelCuadrarSaldosDto>(StringComparer.Ordinal);
        foreach (var f in filasExcel)
        {
            var k = (f.Fecha ?? "").Trim();
            if (!string.IsNullOrEmpty(k)) excelByDate.TryAdd(k, f);
        }

        // Todas las fechas a revisar (unión ordenada)
        var allDates = new SortedSet<string>(histByDate.Keys.Concat(excelByDate.Keys), StringComparer.Ordinal);

        var inconsistencias = new List<InconsistenciaCuadrarSaldosDto>();
        var acciones = new List<AccionCorreccionCuadrarSaldosDto>();

        // Colección de IDs ya asignados a una acción (para no sugerir el mismo movimiento dos veces)
        var histIdsUsados = new HashSet<long>();

        foreach (var fecha in allDates)
        {
            excelByDate.TryGetValue(fecha, out var excelFila);
            histByDate.TryGetValue(fecha, out var histFila);
            histFila ??= [];

            // Totales del sistema en esta fecha
            var sysIngresoKg = histFila
                .Where(h => h.TipoEvento == "INV_INGRESO")
                .Sum(h => h.CantidadKg ?? 0m);
            var sysTrasladoEntradaKg = histFila
                .Where(h => h.TipoEvento == "INV_TRASLADO_ENTRADA")
                .Sum(h => h.CantidadKg ?? 0m);
            var sysTrasladoSalidaKg = histFila
                .Where(h => h.TipoEvento == "INV_TRASLADO_SALIDA")
                .Sum(h => Math.Abs(h.CantidadKg ?? 0m));
            var sysDocumentos = histFila
                .Select(h => (h.NumeroDocumento?.Trim() ?? h.Referencia?.Trim() ?? "").Trim())
                .Where(d => !string.IsNullOrEmpty(d))
                .Distinct()
                .ToList();

            // ── Ingresos ──
            var excelIngreso = excelFila?.IngresoAlimentoKg ?? 0m;
            if (Math.Abs(excelIngreso - sysIngresoKg) > 0.001m)
            {
                var primerHistId = histFila.FirstOrDefault(h => h.TipoEvento == "INV_INGRESO")?.Id;
                var sysDoc = sysDocumentos.FirstOrDefault();

                if (excelIngreso > 0 && sysIngresoKg == 0)
                {
                    inconsistencias.Add(new("INGRESO_FALTANTE", fecha,
                        $"Excel tiene ingreso de {excelIngreso:F3} kg pero el sistema no registra ninguno ese día.",
                        excelIngreso, 0m, null, excelFila?.Documento, null));

                    // Buscar candidato en otro día: primero por documento+monto, luego solo monto
                    var candidato = BuscarCandidatoHistorico(hist, histIdsUsados, "INV_INGRESO",
                        excelIngreso, excelFila?.Documento);

                    if (candidato != null)
                    {
                        histIdsUsados.Add(candidato.Id);
                        acciones.Add(new(
                            "AJUSTAR_FECHA", candidato.Id, fecha,
                            null, null, null,
                            candidato.NumeroDocumento ?? candidato.Referencia,
                            $"Mover ingreso de {excelIngreso:F3} kg (ID:{candidato.Id}, fecha actual: {YmdHistoricoEfectivo(candidato)}) → {fecha}"));
                    }
                    else
                    {
                        acciones.Add(new(
                            "INSERTAR", null, null,
                            fecha, "INV_INGRESO", excelIngreso,
                            excelFila?.Documento,
                            $"Insertar ingreso de {excelIngreso:F3} kg en {fecha} (doc: {excelFila?.Documento ?? "-"})"));
                    }
                }
                else if (excelIngreso == 0 && sysIngresoKg > 0)
                {
                    inconsistencias.Add(new("INGRESO_SOBRANTE", fecha,
                        $"Sistema tiene ingreso de {sysIngresoKg:F3} kg pero el Excel no muestra ninguno ese día.",
                        0m, sysIngresoKg, primerHistId, null, sysDoc));

                    foreach (var h in histFila.Where(x => x.TipoEvento == "INV_INGRESO"))
                    {
                        if (histIdsUsados.Add(h.Id))
                            acciones.Add(new(
                                "ANULAR", h.Id, null, null, null, null, null,
                                $"Anular ingreso sobrante de {h.CantidadKg:F3} kg en {fecha} (ID:{h.Id})"));
                    }
                }
                else
                {
                    inconsistencias.Add(new("INGRESO_MONTO_DIFERENTE", fecha,
                        $"Ingreso Excel: {excelIngreso:F3} kg — sistema: {sysIngresoKg:F3} kg.",
                        excelIngreso, sysIngresoKg, primerHistId, excelFila?.Documento, sysDoc));
                }
            }

            // ── Traslados entrada ──
            var excelTrasladoEntrada = excelFila?.TrasladoEntradaKg ?? 0m;
            if (Math.Abs(excelTrasladoEntrada - sysTrasladoEntradaKg) > 0.001m)
            {
                var h0 = histFila.FirstOrDefault(h => h.TipoEvento == "INV_TRASLADO_ENTRADA");

                if (excelTrasladoEntrada > 0 && sysTrasladoEntradaKg == 0)
                {
                    inconsistencias.Add(new("TRASLADO_ENTRADA_FALTANTE", fecha,
                        $"Excel tiene traslado entrada de {excelTrasladoEntrada:F3} kg pero el sistema no registra ninguno ese día.",
                        excelTrasladoEntrada, 0m, null, excelFila?.Documento, null));

                    var candidato = BuscarCandidatoHistorico(hist, histIdsUsados, "INV_TRASLADO_ENTRADA",
                        excelTrasladoEntrada, null);
                    if (candidato != null)
                    {
                        histIdsUsados.Add(candidato.Id);
                        acciones.Add(new(
                            "AJUSTAR_FECHA", candidato.Id, fecha, null, null, null, null,
                            $"Mover traslado entrada de {excelTrasladoEntrada:F3} kg (ID:{candidato.Id}) → {fecha}"));
                    }
                    else
                    {
                        acciones.Add(new(
                            "INSERTAR", null, null,
                            fecha, "INV_TRASLADO_ENTRADA", excelTrasladoEntrada, excelFila?.Documento,
                            $"Insertar traslado entrada de {excelTrasladoEntrada:F3} kg en {fecha}"));
                    }
                }
                else if (excelTrasladoEntrada == 0 && sysTrasladoEntradaKg > 0)
                {
                    inconsistencias.Add(new("TRASLADO_ENTRADA_SOBRANTE", fecha,
                        $"Sistema tiene traslado entrada de {sysTrasladoEntradaKg:F3} kg pero Excel no.",
                        0m, sysTrasladoEntradaKg, h0?.Id, null, null));

                    foreach (var h in histFila.Where(x => x.TipoEvento == "INV_TRASLADO_ENTRADA"))
                    {
                        if (histIdsUsados.Add(h.Id))
                            acciones.Add(new(
                                "ANULAR", h.Id, null, null, null, null, null,
                                $"Anular traslado entrada sobrante de {h.CantidadKg:F3} kg en {fecha} (ID:{h.Id})"));
                    }
                }
                else
                {
                    inconsistencias.Add(new("TRASLADO_ENTRADA_DIFERENTE", fecha,
                        $"Traslado entrada Excel: {excelTrasladoEntrada:F3} kg — sistema: {sysTrasladoEntradaKg:F3} kg.",
                        excelTrasladoEntrada, sysTrasladoEntradaKg, h0?.Id, null, null));
                }
            }

            // ── Traslados salida ──
            var excelTrasladoSalida = excelFila?.TrasladoSalidaKg ?? 0m;
            if (Math.Abs(excelTrasladoSalida - sysTrasladoSalidaKg) > 0.001m)
            {
                var h0 = histFila.FirstOrDefault(h => h.TipoEvento == "INV_TRASLADO_SALIDA");

                if (excelTrasladoSalida > 0 && sysTrasladoSalidaKg == 0)
                {
                    inconsistencias.Add(new("TRASLADO_SALIDA_FALTANTE", fecha,
                        $"Excel tiene traslado salida de {excelTrasladoSalida:F3} kg pero el sistema no registra ninguno ese día.",
                        excelTrasladoSalida, 0m, null, null, null));

                    var candidato = BuscarCandidatoHistorico(hist, histIdsUsados, "INV_TRASLADO_SALIDA",
                        excelTrasladoSalida, null);
                    if (candidato != null)
                    {
                        histIdsUsados.Add(candidato.Id);
                        acciones.Add(new(
                            "AJUSTAR_FECHA", candidato.Id, fecha, null, null, null, null,
                            $"Mover traslado salida de {excelTrasladoSalida:F3} kg (ID:{candidato.Id}) → {fecha}"));
                    }
                    else
                    {
                        acciones.Add(new(
                            "INSERTAR", null, null,
                            fecha, "INV_TRASLADO_SALIDA", excelTrasladoSalida, null,
                            $"Insertar traslado salida de {excelTrasladoSalida:F3} kg en {fecha}"));
                    }
                }
                else if (excelTrasladoSalida == 0 && sysTrasladoSalidaKg > 0)
                {
                    inconsistencias.Add(new("TRASLADO_SALIDA_SOBRANTE", fecha,
                        $"Sistema tiene traslado salida de {sysTrasladoSalidaKg:F3} kg pero Excel no.",
                        0m, sysTrasladoSalidaKg, h0?.Id, null, null));

                    foreach (var h in histFila.Where(x => x.TipoEvento == "INV_TRASLADO_SALIDA"))
                    {
                        if (histIdsUsados.Add(h.Id))
                            acciones.Add(new(
                                "ANULAR", h.Id, null, null, null, null, null,
                                $"Anular traslado salida sobrante de {Math.Abs(h.CantidadKg ?? 0):F3} kg en {fecha} (ID:{h.Id})"));
                    }
                }
                else
                {
                    inconsistencias.Add(new("TRASLADO_SALIDA_DIFERENTE", fecha,
                        $"Traslado salida Excel: {excelTrasladoSalida:F3} kg — sistema: {sysTrasladoSalidaKg:F3} kg.",
                        excelTrasladoSalida, sysTrasladoSalidaKg, h0?.Id, null, null));
                }
            }

            // ── Documento ──
            var excelDoc = (excelFila?.Documento ?? "").Trim();
            if (!string.IsNullOrEmpty(excelDoc) && sysDocumentos.Count > 0)
            {
                var matchDoc = sysDocumentos.Any(d =>
                    string.Equals(d, excelDoc, StringComparison.OrdinalIgnoreCase));
                if (!matchDoc)
                {
                    inconsistencias.Add(new("DOCUMENTO_DIFERENTE", fecha,
                        $"Documento Excel: \"{excelDoc}\" — sistema: \"{string.Join(", ", sysDocumentos)}\".",
                        null, null,
                        histFila.FirstOrDefault()?.Id,
                        excelDoc, string.Join(", ", sysDocumentos)));
                }
            }
        }

        return new CuadrarSaldosValidarResponseDto(
            loteId,
            filasExcel.Count,
            inconsistencias.Count,
            inconsistencias,
            acciones);
    }

    public async Task<CuadrarSaldosAplicarResponseDto> AplicarCuadrarSaldosAsync(
        int loteId,
        IReadOnlyList<AccionCorreccionCuadrarSaldosDto> acciones,
        IReadOnlyList<FilaExcelCuadrarSaldosDto>? filasExcel = null)
    {
        var companyId = _current.CompanyId;

        var loteInfo = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null)
            .Select(l => new { l.GranjaId, l.NucleoId, l.GalponId })
            .SingleOrDefaultAsync()
            ?? throw new InvalidOperationException($"Lote {loteId} no encontrado.");

        int fechasAjustadas = 0, registrosAnulados = 0, registrosInsertados = 0;
        var nuevosParaFixOrigenId = new List<LoteRegistroHistoricoUnificado>();

        // Cargar de una vez los IDs a modificar
        var idsModificar = acciones
            .Where(a => a.HistoricoId.HasValue && a.TipoAccion is "AJUSTAR_FECHA" or "ANULAR")
            .Select(a => a.HistoricoId!.Value)
            .Distinct()
            .ToList();

        var entidades = idsModificar.Count > 0
            ? await _ctx.LoteRegistroHistoricoUnificados
                .Where(h => idsModificar.Contains(h.Id) && h.CompanyId == companyId)
                .ToListAsync()
            : [];

        var entidadPorId = entidades.ToDictionary(h => h.Id);

        foreach (var accion in acciones)
        {
            switch (accion.TipoAccion)
            {
                case "AJUSTAR_FECHA":
                    if (accion.HistoricoId.HasValue
                        && !string.IsNullOrEmpty(accion.NuevaFecha)
                        && DateTime.TryParse(accion.NuevaFecha, null,
                            System.Globalization.DateTimeStyles.AdjustToUniversal, out var nuevaFecha)
                        && entidadPorId.TryGetValue(accion.HistoricoId.Value, out var hAj))
                    {
                        hAj.FechaOperacion = nuevaFecha.Date;
                        fechasAjustadas++;
                    }
                    break;

                case "ANULAR":
                    if (accion.HistoricoId.HasValue
                        && entidadPorId.TryGetValue(accion.HistoricoId.Value, out var hAn))
                    {
                        hAn.Anulado = true;
                        registrosAnulados++;
                    }
                    break;

                case "INSERTAR":
                    if (!string.IsNullOrEmpty(accion.FechaInsertar)
                        && !string.IsNullOrEmpty(accion.TipoEvento)
                        && accion.CantidadKg.HasValue
                        && DateTime.TryParse(accion.FechaInsertar, null,
                            System.Globalization.DateTimeStyles.AdjustToUniversal, out var fechaIns))
                    {
                        var nuevo = new LoteRegistroHistoricoUnificado
                        {
                            CompanyId  = companyId,
                            LoteAveEngordeId = loteId,
                            FarmId     = loteInfo.GranjaId,
                            NucleoId   = loteInfo.NucleoId,
                            GalponId   = loteInfo.GalponId,
                            FechaOperacion = fechaIns.Date,
                            TipoEvento = accion.TipoEvento,
                            OrigenTabla = "cuadrar_saldos_engorde",
                            OrigenId   = 0, // se corrige tras SaveChanges usando el Id generado
                            CantidadKg = accion.TipoEvento == "INV_TRASLADO_SALIDA"
                                ? -Math.Abs(accion.CantidadKg.Value)
                                : accion.CantidadKg.Value,
                            Unidad     = "kg",
                            NumeroDocumento = accion.Documento,
                            Referencia = $"Cuadre saldos Excel — {accion.Descripcion ?? accion.TipoEvento}",
                            Anulado    = false,
                            CreatedAt  = DateTimeOffset.UtcNow
                        };
                        _ctx.LoteRegistroHistoricoUnificados.Add(nuevo);
                        nuevosParaFixOrigenId.Add(nuevo);
                        registrosInsertados++;
                    }
                    break;
            }
        }

        if (fechasAjustadas + registrosAnulados + registrosInsertados > 0)
        {
            // Primer save: AJUSTAR_FECHA y ANULAR se resuelven aquí;
            // los INSERTAR obtienen su Id autogenerado pero OrigenId=0 aún.
            // Guardamos por separado AJUSTAR/ANULAR primero si hay INSERTs para
            // evitar la violación del unique (origen_tabla, origen_id).
            if (nuevosParaFixOrigenId.Count > 0)
            {
                // Guardar solo las modificaciones existentes (AJUSTAR/ANULAR)
                // sin los nuevos todavía, para no violar el unique con OrigenId=0.
                var sinNuevos = nuevosParaFixOrigenId
                    .Select(n => _ctx.Entry(n))
                    .ToList();
                sinNuevos.ForEach(e => e.State = Microsoft.EntityFrameworkCore.EntityState.Detached);

                if (fechasAjustadas + registrosAnulados > 0)
                    await _ctx.SaveChangesAsync();

                // Volver a adjuntar y guardar los nuevos uno a uno para que
                // cada uno obtenga su Id antes del siguiente (el unique lo exige).
                foreach (var n in nuevosParaFixOrigenId)
                {
                    _ctx.LoteRegistroHistoricoUnificados.Add(n);
                    await _ctx.SaveChangesAsync();
                    n.OrigenId = (int)(n.Id & 0x7FFFFFFF); // Id cabe en int; siempre > 0
                    await _ctx.SaveChangesAsync();
                }
            }
            else
            {
                await _ctx.SaveChangesAsync();
            }
        }

        await RecalcularSaldoAlimentoPorLoteAsync(loteId, companyId);

        var metadataLimpiados = filasExcel != null && filasExcel.Count > 0
            ? await ReconciliarMetadataDocumentoAsync(loteId, filasExcel)
            : 0;

        return new CuadrarSaldosAplicarResponseDto(
            loteId,
            fechasAjustadas,
            registrosAnulados,
            registrosInsertados,
            metadataLimpiados,
            $"Correcciones aplicadas: {fechasAjustadas} fecha(s) ajustada(s), " +
            $"{registrosAnulados} registro(s) anulado(s), " +
            $"{registrosInsertados} registro(s) insertado(s)" +
            (metadataLimpiados > 0 ? $", {metadataLimpiados} seguimiento(s) con metadata corregida." : "."));
    }

    /// <summary>
    /// Limpia las claves de documento (documento, documentoAlimento, nroDocumento, numeroDocumento)
    /// de la metadata de seguimientos cuyas fechas no tienen movimientos reales en el Excel.
    /// Esto elimina "fechas fantasma" donde el histórico fue corregido pero la metadata del
    /// seguimiento diario todavía referencia el documento anterior.
    /// </summary>
    private async Task<int> ReconciliarMetadataDocumentoAsync(
        int loteId,
        IReadOnlyList<FilaExcelCuadrarSaldosDto> filasExcel)
    {
        // Fechas del Excel donde hay al menos un movimiento real (ingreso, traslado o documento)
        var fechasValidasExcel = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fila in filasExcel)
        {
            var tieneMovimiento = (fila.IngresoAlimentoKg.HasValue && fila.IngresoAlimentoKg > 0)
                || (fila.TrasladoEntradaKg.HasValue && fila.TrasladoEntradaKg > 0)
                || (fila.TrasladoSalidaKg.HasValue && fila.TrasladoSalidaKg > 0)
                || !string.IsNullOrWhiteSpace(fila.Documento);
            if (tieneMovimiento)
                fechasValidasExcel.Add(fila.Fecha); // YYYY-MM-DD
        }

        if (fechasValidasExcel.Count == 0) return 0;

        var seguimientos = await _ctx.SeguimientoDiarioAvesEngorde
            .Where(s => s.LoteAveEngordeId == loteId && s.Metadata != null)
            .ToListAsync();

        int limpiados = 0;
        foreach (var seg in seguimientos)
        {
            if (seg.Metadata is null) continue;

            var fechaSeg = seg.Fecha.ToString("yyyy-MM-dd");
            if (fechasValidasExcel.Contains(fechaSeg)) continue;

            try
            {
                var root = seg.Metadata.RootElement;
                if (root.ValueKind != JsonValueKind.Object) continue;

                var tieneClaveDoc = _docMetadataKeys.Any(k => root.TryGetProperty(k, out _));
                if (!tieneClaveDoc) continue;

                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(root.GetRawText())
                           ?? new Dictionary<string, JsonElement>();

                var modificado = false;
                foreach (var key in _docMetadataKeys)
                {
                    if (dict.Remove(key))
                        modificado = true;
                }

                if (modificado)
                {
                    seg.Metadata = dict.Count > 0
                        ? JsonDocument.Parse(JsonSerializer.Serialize(dict))
                        : null;
                    limpiados++;
                }
            }
            catch { /* metadata malformado: no modificar */ }
        }

        if (limpiados > 0)
            await _ctx.SaveChangesAsync();

        return limpiados;
    }
}
