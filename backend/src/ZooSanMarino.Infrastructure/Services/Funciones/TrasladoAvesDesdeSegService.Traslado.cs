// Funciones/TrasladoAvesDesdeSegService.Traslado.cs
// Ejecución del traslado de aves desde el seguimiento diario: validación de etapa (con soporte
// cross-etapa Levante→Producción por empresa), patas de origen y destino por etapa, auditoría en
// MovimientoAves y registro de la cohorte en el lote destino. Todo en una sola transacción.
// Fechas puras ancladas a MEDIODÍA UTC (FechasPuras) y fila diaria ubicada por DÍA CALENDARIO:
// mismo contrato que la hoja "Movimientos Aves" de la carga masiva, para que ambos caminos se
// detecten mutuamente pese al releído corrido de Npgsql legacy.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Traslados;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class TrasladoAvesDesdeSegService
{
    public async Task<TrasladoAvesResultDto> EjecutarTrasladoDesdeSegAsync(
        TrasladoAvesDesdeSegDiarioDto dto,
        int usuarioId,
        CancellationToken ct = default)
    {
        // ── 0. Validación de etapa ───────────────────────────────────────
        //   Misma etapa → permitido siempre (camino histórico, sin consultas extra).
        //   Etapas distintas → solo si la EMPRESA del lote origen lo permite y el sentido es
        //   Levante→Producción; si no, se lanza el mensaje de bloqueo de siempre.
        var esMismaEtapa = LoteCohortesCalculos.EsMismaEtapa(dto.TipoOrigen, dto.TipoDestino);
        if (!esMismaEtapa)
            await ValidarCrossEtapaAsync(dto, ct);

        await using var tx = await _ctx.Database.BeginTransactionAsync(ct);
        try
        {
            var companyId = await GetEffectiveCompanyIdAsync(ct);
            // Fecha pura ANCLADA A MEDIODÍA UTC: escrita a medianoche, Npgsql (modo legacy) la guarda
            // como 00:00 UTC y la RELEE convertida a hora local (19:00 del día ANTERIOR en Bogotá),
            // corriendo el día calendario de la fila y de la idempotencia de la carga masiva.
            var fechaAncla = FechasPuras.AnclarMediodiaUtc(dto.FechaSeguimiento);
            var fechaUtc  = DateTime.UtcNow;

            int? granjaDestinoIdOut = dto.GranjaDestinoId;

            var origenEsLevante  = LoteCohortesCalculos.EsLevante(dto.TipoOrigen);
            var destinoEsLevante = LoteCohortesCalculos.EsLevante(dto.TipoDestino);

            // ── 1/2. ORIGEN: cargar espejo, validar lote base y validar stock ─
            LotePosturaLevante?    lplOrigen = null;
            LotePosturaProduccion? lppOrigen = null;
            LadoTraslado origen;
            if (origenEsLevante)
            {
                lplOrigen = await CargarOrigenLevanteAsync(dto, companyId, ct);
                origen = LadoDe(lplOrigen);
            }
            else
            {
                lppOrigen = await CargarOrigenProduccionAsync(dto, companyId, ct);
                origen = LadoDe(lppOrigen);
            }

            // ── 3. DESTINO: cargar espejo y validar lote base ────────────
            LotePosturaLevante?    lplDestino = null;
            LotePosturaProduccion? lppDestino = null;
            LadoTraslado destino;
            if (destinoEsLevante)
            {
                // El chequeo "origen == destino" solo aplica dentro de la misma etapa (histórico).
                lplDestino = await CargarDestinoLevanteAsync(dto, companyId, esMismaEtapa ? origen.EspejoId : null, ct);
                destino = LadoDe(lplDestino);
            }
            else
            {
                lppDestino = await CargarDestinoProduccionAsync(dto, companyId, ct);
                destino = LadoDe(lppDestino);
            }

            granjaDestinoIdOut ??= destino.GranjaId;

            // ── 4/5. Pata ORIGEN: acumulados de salida + registro SALIDA ──
            if (origenEsLevante)
                await AplicarSalidaLevanteAsync(lplOrigen!, destino, dto, fechaAncla, fechaUtc, usuarioId, ct);
            else
                await AplicarSalidaProduccionAsync(lppOrigen!, destino, dto, fechaAncla, fechaUtc, usuarioId, companyId, ct);

            // ── 6. Pata DESTINO: acumulados de ingreso + registro INGRESO ─
            if (destinoEsLevante)
                await AplicarIngresoLevanteAsync(lplDestino!, origen, dto, fechaAncla, fechaUtc, usuarioId, ct);
            else
                await AplicarIngresoProduccionAsync(lppDestino!, origen, dto, fechaAncla, fechaUtc, usuarioId, companyId, ct);

            // ── 7. Auditoría — MovimientoAves ─────────────────────────────
            var movimiento = new MovimientoAves
            {
                NumeroMovimiento = $"TSD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..24],
                FechaMovimiento = fechaAncla,
                TipoMovimiento = "Traslado",
                CantidadHembras = dto.TrasladoHembras,
                CantidadMachos = dto.TrasladoMachos,
                CantidadMixtas = 0,
                Estado = "Completado",
                FechaProcesamiento = DateTime.UtcNow,
                Observaciones = dto.Observaciones,
                UsuarioMovimientoId = usuarioId,
                GranjaDestinoId = granjaDestinoIdOut,
                // Destino: el lote base receptor. Quedaba NULL y eso rompía dos cosas — la auditoría no
                // decía a qué lote fueron las aves, y la idempotencia de la carga masiva (que busca por
                // `LoteDestinoId == loteId` para detectar un Ingreso ya aplicado) no veía este traslado y
                // lo habría vuelto a aplicar al reimportar el lote receptor.
                LoteDestinoId = destino.LoteBaseId,
                // Origen del movimiento: el lote base y la granja del espejo ya cargado.
                LoteOrigenId = origen.LoteBaseId,
                GranjaOrigenId = origen.GranjaId,
                Placa = dto.Placa,
                Conductor = dto.Conductor,
                Sellos = dto.Sellos
            };

            _ctx.MovimientoAves.Add(movimiento);
            await _ctx.SaveChangesAsync(ct);

            // ── 7b. Cohorte del lote DESTINO (edad de las aves recibidas) ──
            //   Misma transacción; nunca hace fallar el traslado si no hay fecha de encaset.
            await RegistrarCohorteDestinoAsync(origen, destino, dto, movimiento.Id, companyId, usuarioId, ct);

            await tx.CommitAsync(ct);

            // ── 8. Leer saldo final REAL del origen para devolver ────────
            int avesHFinal = 0, avesMFinal = 0;
            if (origenEsLevante)
            {
                var lpl = await _ctx.LotePosturaLevante.AsNoTracking()
                    .Where(l => l.LotePosturaLevanteId == dto.LoteOrigenId).FirstOrDefaultAsync(ct);
                if (lpl?.LoteId is int loteIdF)
                {
                    var res = await _loteService.GetMortalidadResumenAsync(loteIdF);
                    if (res != null)
                    {
                        avesHFinal = res.SaldoHembras;
                        avesMFinal = res.SaldoMachos;
                    }
                    else
                    {
                        avesHFinal = lpl.AvesHActual ?? 0;
                        avesMFinal = lpl.AvesMActual ?? 0;
                    }
                }
            }
            else
            {
                var lpp = await _ctx.LotePosturaProduccion.AsNoTracking()
                    .Where(l => l.LotePosturaProduccionId == dto.LoteOrigenId).FirstOrDefaultAsync(ct);
                avesHFinal = lpp?.AvesHActual ?? 0;
                avesMFinal = lpp?.AvesMActual ?? 0;
            }

            return new TrasladoAvesResultDto(
                Exitoso: true,
                Mensaje: "Traslado ejecutado correctamente.",
                MovimientoAvesId: movimiento.Id,
                AvesHActualOrigen: avesHFinal,
                AvesMActualOrigen: avesMFinal
            );
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Validación cross-etapa (solo se ejecuta cuando las etapas NO coinciden)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Traslado entre etapas: exige que la EMPRESA dueña de la granja del lote origen tenga
    /// <c>permite_traslado_aves_cross_etapa</c> y que el sentido sea Levante→Producción; además,
    /// que el lote destino sea de la MISMA empresa. Fail-closed: si la empresa del origen no se
    /// puede resolver, se comporta como si el flag estuviera apagado (mensaje de bloqueo de siempre).
    /// </summary>
    private async Task ValidarCrossEtapaAsync(TrasladoAvesDesdeSegDiarioDto dto, CancellationToken ct)
    {
        var companyOrigen = await ResolverCompanyIdDeEspejoAsync(
            dto.LoteOrigenId, LoteCohortesCalculos.EsLevante(dto.TipoOrigen), ct);

        var empresaPermite = companyOrigen is int co && await EmpresaPermiteCrossEtapaAsync(co, ct);

        if (!LoteCohortesCalculos.PuedeTrasladarCrossEtapa(empresaPermite, dto.TipoOrigen, dto.TipoDestino))
            throw new InvalidOperationException(
                LoteCohortesCalculos.MensajeCrossEtapaBloqueado(dto.TipoOrigen, dto.TipoDestino));

        var companyDestino = await ResolverCompanyIdDeEspejoAsync(
            dto.LoteDestinoId, LoteCohortesCalculos.EsLevante(dto.TipoDestino), ct);

        if (companyDestino is null || companyDestino != companyOrigen)
            throw new InvalidOperationException(
                "El traslado entre etapas exige que el lote origen y el lote destino pertenezcan a la " +
                "misma empresa (según la granja de cada lote).");
    }

    /// <summary>Empresa (por <c>farms.company_id</c>) de la granja del espejo Levante/Producción indicado.</summary>
    private async Task<int?> ResolverCompanyIdDeEspejoAsync(int espejoId, bool esLevante, CancellationToken ct)
    {
        var granjaId = esLevante
            ? await _ctx.LotePosturaLevante.AsNoTracking()
                .Where(l => l.LotePosturaLevanteId == espejoId && l.DeletedAt == null)
                .Select(l => (int?)l.GranjaId).FirstOrDefaultAsync(ct)
            : await _ctx.LotePosturaProduccion.AsNoTracking()
                .Where(l => l.LotePosturaProduccionId == espejoId && l.DeletedAt == null)
                .Select(l => (int?)l.GranjaId).FirstOrDefaultAsync(ct);

        return granjaId is null ? null : await ResolverCompanyIdDeGranjaAsync(granjaId.Value, ct);
    }

    /// <summary>Lee el flag tipado <c>companies.permite_traslado_aves_cross_etapa</c>.</summary>
    private async Task<bool> EmpresaPermiteCrossEtapaAsync(int companyId, CancellationToken ct) =>
        await _ctx.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => (bool?)c.PermiteTrasladoAvesCrossEtapa)
            .FirstOrDefaultAsync(ct) == true;

    // ─────────────────────────────────────────────────────────────────────
    // Carga y validación de cada lado
    // ─────────────────────────────────────────────────────────────────────

    private static LadoTraslado LadoDe(LotePosturaLevante lpl) => new(
        lpl.LotePosturaLevanteId!.Value, lpl.LoteId!.Value, lpl.GranjaId, lpl.LoteNombre, lpl.FechaEncaset);

    private static LadoTraslado LadoDe(LotePosturaProduccion lpp) => new(
        lpp.LotePosturaProduccionId!.Value, lpp.LoteId!.Value, lpp.GranjaId, lpp.LoteNombre, lpp.FechaEncaset);

    /// <summary>
    /// LPL origen + validación de stock con el SALDO REAL (incluye mortalidad/sel/error + traslados).
    /// </summary>
    private async Task<LotePosturaLevante> CargarOrigenLevanteAsync(
        TrasladoAvesDesdeSegDiarioDto dto, int companyId, CancellationToken ct)
    {
        var lplOrigen = await _ctx.LotePosturaLevante
            .Where(l => l.LotePosturaLevanteId == dto.LoteOrigenId
                     && l.CompanyId == companyId
                     && l.DeletedAt == null)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Lote levante origen no encontrado.");

        if (lplOrigen.LoteId is null)
            throw new InvalidOperationException("El lote levante origen no tiene un Lote base asignado.");

        var resumenOrigen = await _loteService.GetMortalidadResumenAsync(lplOrigen.LoteId.Value)
            ?? throw new InvalidOperationException("No se pudo calcular el saldo real del lote origen.");

        if (resumenOrigen.SaldoHembras < dto.TrasladoHembras)
            throw new InvalidOperationException(
                $"Stock insuficiente (real): hay {resumenOrigen.SaldoHembras} hembras vivas, " +
                $"se intentaron trasladar {dto.TrasladoHembras}.");
        if (resumenOrigen.SaldoMachos < dto.TrasladoMachos)
            throw new InvalidOperationException(
                $"Stock insuficiente (real): hay {resumenOrigen.SaldoMachos} machos vivos, " +
                $"se intentaron trasladar {dto.TrasladoMachos}.");

        return lplOrigen;
    }

    /// <summary>LPL destino. <paramref name="espejoOrigenId"/> no nulo ⇒ se valida que no sea el mismo lote.</summary>
    private async Task<LotePosturaLevante> CargarDestinoLevanteAsync(
        TrasladoAvesDesdeSegDiarioDto dto, int companyId, int? espejoOrigenId, CancellationToken ct)
    {
        var lplDestino = await _ctx.LotePosturaLevante
            .Where(l => l.LotePosturaLevanteId == dto.LoteDestinoId
                     && l.CompanyId == companyId
                     && l.DeletedAt == null)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Lote levante destino no encontrado.");

        if (espejoOrigenId.HasValue && lplDestino.LotePosturaLevanteId == espejoOrigenId.Value)
            throw new InvalidOperationException("El lote origen y destino no pueden ser el mismo.");
        if (lplDestino.LoteId is null)
            throw new InvalidOperationException("El lote destino no tiene un Lote base asignado.");

        return lplDestino;
    }

    /// <summary>LPP origen + validación de stock contra las aves actuales del espejo (paridad Feature 14).</summary>
    private async Task<LotePosturaProduccion> CargarOrigenProduccionAsync(
        TrasladoAvesDesdeSegDiarioDto dto, int companyId, CancellationToken ct)
    {
        var lppOrigen = await _ctx.LotePosturaProduccion
            .Where(l => l.LotePosturaProduccionId == dto.LoteOrigenId
                     && l.CompanyId == companyId
                     && l.DeletedAt == null)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Lote producción origen no encontrado.");

        if (lppOrigen.LoteId is null)
            throw new InvalidOperationException("El lote producción origen no tiene un Lote base asignado.");
        if ((lppOrigen.AvesHActual ?? 0) < dto.TrasladoHembras)
            throw new InvalidOperationException($"Stock insuficiente: solo hay {lppOrigen.AvesHActual ?? 0} hembras disponibles.");
        if ((lppOrigen.AvesMActual ?? 0) < dto.TrasladoMachos)
            throw new InvalidOperationException($"Stock insuficiente de machos: solo hay {lppOrigen.AvesMActual ?? 0} disponibles.");

        return lppOrigen;
    }

    private async Task<LotePosturaProduccion> CargarDestinoProduccionAsync(
        TrasladoAvesDesdeSegDiarioDto dto, int companyId, CancellationToken ct)
    {
        var lppDestino = await _ctx.LotePosturaProduccion
            .Where(l => l.LotePosturaProduccionId == dto.LoteDestinoId
                     && l.CompanyId == companyId
                     && l.DeletedAt == null)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Lote producción destino no encontrado.");

        if (lppDestino.LoteId is null)
            throw new InvalidOperationException("El lote destino no tiene un Lote base asignado.");

        return lppDestino;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Patas del traslado (aritmética idéntica a la que vivía inline)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Día calendario de la fecha anclada y rango ampliado <c>[día−1, día+2)</c> para ubicar la fila
    /// diaria existente. Las filas reales del día pueden estar a medianoche (históricas), a mediodía
    /// (ancladas) o a la hora que mandó el front (alta manual), y Npgsql legacy además relee los
    /// instantes corridos a hora local ⇒ se consulta el rango y se recorta en memoria por
    /// <c>Fecha.Date</c> (patrón FechasYaCargadasAsync de la carga masiva).
    /// </summary>
    private static (DateTime Dia, DateTime Desde, DateTime Hasta) RangoDiaCalendario(DateTime fechaAncla)
    {
        var dia = fechaAncla.Date;
        return (dia, dia.AddDays(-1), dia.AddDays(2));
    }

    /// <summary>
    /// Pata SALIDA en Levante: acumulados de traslado del LPL origen + UPSERT del registro SALIDA en
    /// seguimiento_diario. Si ya existe un SD para ese día+lote (manual, de carga masiva o de otro
    /// traslado) se extiende con los campos de traslado (no se tocan mortalidad/selección: el traslado
    /// vive en sus propias columnas).
    /// </summary>
    private async Task AplicarSalidaLevanteAsync(
        LotePosturaLevante lplOrigen, LadoTraslado destino, TrasladoAvesDesdeSegDiarioDto dto,
        DateTime fechaAncla, DateTime fechaUtc, int usuarioId, CancellationToken ct)
    {
        lplOrigen.LevanteTrasladoSalidaHembras += dto.TrasladoHembras;
        lplOrigen.LevanteTrasladoSalidaMachos  += dto.TrasladoMachos;
        lplOrigen.AvesHActual = Math.Max(0, (lplOrigen.AvesHActual ?? 0) - dto.TrasladoHembras);
        lplOrigen.AvesMActual = Math.Max(0, (lplOrigen.AvesMActual ?? 0) - dto.TrasladoMachos);

        var totalAves = dto.TrasladoHembras + dto.TrasladoMachos;

        var (fechaDia, rangoDesde, rangoHasta) = RangoDiaCalendario(fechaAncla);
        var segSalida = (await _ctx.SeguimientoDiario
                .Where(s => s.TipoSeguimiento == "levante"
                         && s.LoteId == lplOrigen.LoteId!.Value.ToString()
                         && s.Fecha >= rangoDesde && s.Fecha < rangoHasta)
                .ToListAsync(ct))
            .FirstOrDefault(s => s.Fecha.Date == fechaDia);

        if (segSalida is null)
        {
            segSalida = new SeguimientoDiario
            {
                // El traslado YA movió el maestro y no separa nada: la fila nace validada. Con
                // `validado = false` (el default) estas filas aparecían pendientes en las empresas con
                // doble validación y, a las 24 h, BLOQUEABAN el alta de días nuevos del lote sin que
                // hubiera nada que validar — mismo defecto que tenían los Crud.
                Validado = true,
                TipoSeguimiento = "levante",
                LoteId = lplOrigen.LoteId!.Value.ToString(),
                LotePosturaLevanteId = lplOrigen.LotePosturaLevanteId,
                Fecha = fechaAncla,
                MortalidadHembras = 0, MortalidadMachos = 0,
                SelH = 0, SelM = 0,
                ErrorSexajeHembras = 0, ErrorSexajeMachos = 0,
                Ciclo = "Traslado",
                TipoAlimento = "—",
                CreatedByUserId = _current.UserGuid?.ToString() ?? usuarioId.ToString(),
                CreatedAt = fechaUtc
            };
            _ctx.SeguimientoDiario.Add(segSalida);
        }

        // Acumulamos sobre lo que hubiera (caso: varios traslados en el mismo día)
        segSalida.TrasladoSalidaHembras += dto.TrasladoHembras;
        segSalida.TrasladoSalidaMachos  += dto.TrasladoMachos;
        segSalida.TrasladoAvesSalida     = (segSalida.TrasladoAvesSalida ?? 0) + totalAves;
        segSalida.EsTraslado             = true;
        segSalida.TrasladoDireccion      = "SALIDA";
        segSalida.TrasladoLoteContraparteId   = destino.EspejoId;
        segSalida.TrasladoGranjaContraparteId = destino.GranjaId;
        segSalida.Observaciones = string.IsNullOrWhiteSpace(segSalida.Observaciones)
            ? $"Traslado SALIDA → {destino.LoteNombre}. {dto.Observaciones ?? ""}".Trim()
            : $"{segSalida.Observaciones} | Traslado SALIDA → {destino.LoteNombre}";
        segSalida.UpdatedAt = fechaUtc;
    }

    /// <summary>Pata INGRESO en Levante: acumulados del LPL destino + UPSERT del registro INGRESO.</summary>
    private async Task AplicarIngresoLevanteAsync(
        LotePosturaLevante lplDestino, LadoTraslado origen, TrasladoAvesDesdeSegDiarioDto dto,
        DateTime fechaAncla, DateTime fechaUtc, int usuarioId, CancellationToken ct)
    {
        lplDestino.LevanteTrasladoIngresoHembras += dto.TrasladoHembras;
        lplDestino.LevanteTrasladoIngresoMachos  += dto.TrasladoMachos;
        lplDestino.AvesHActual = (lplDestino.AvesHActual ?? 0) + dto.TrasladoHembras;
        lplDestino.AvesMActual = (lplDestino.AvesMActual ?? 0) + dto.TrasladoMachos;

        var totalAves = dto.TrasladoHembras + dto.TrasladoMachos;

        var (fechaDia, rangoDesde, rangoHasta) = RangoDiaCalendario(fechaAncla);
        var segIngreso = (await _ctx.SeguimientoDiario
                .Where(s => s.TipoSeguimiento == "levante"
                         && s.LoteId == lplDestino.LoteId!.Value.ToString()
                         && s.Fecha >= rangoDesde && s.Fecha < rangoHasta)
                .ToListAsync(ct))
            .FirstOrDefault(s => s.Fecha.Date == fechaDia);

        if (segIngreso is null)
        {
            segIngreso = new SeguimientoDiario
            {
                // El traslado YA movió el maestro y no separa nada: la fila nace validada. Con
                // `validado = false` (el default) estas filas aparecían pendientes en las empresas con
                // doble validación y, a las 24 h, BLOQUEABAN el alta de días nuevos del lote sin que
                // hubiera nada que validar — mismo defecto que tenían los Crud.
                Validado = true,
                TipoSeguimiento = "levante",
                LoteId = lplDestino.LoteId!.Value.ToString(),
                LotePosturaLevanteId = lplDestino.LotePosturaLevanteId,
                Fecha = fechaAncla,
                MortalidadHembras = 0, MortalidadMachos = 0,
                SelH = 0, SelM = 0,
                ErrorSexajeHembras = 0, ErrorSexajeMachos = 0,
                Ciclo = "Traslado",
                TipoAlimento = "—",
                CreatedByUserId = _current.UserGuid?.ToString() ?? usuarioId.ToString(),
                CreatedAt = fechaUtc
            };
            _ctx.SeguimientoDiario.Add(segIngreso);
        }

        segIngreso.TrasladoIngresoHembras += dto.TrasladoHembras;
        segIngreso.TrasladoIngresoMachos  += dto.TrasladoMachos;
        segIngreso.TrasladoAvesEntrante    = (segIngreso.TrasladoAvesEntrante ?? 0) + totalAves;
        segIngreso.EsTraslado              = true;
        segIngreso.TrasladoDireccion       = "INGRESO";
        segIngreso.TrasladoLoteContraparteId   = origen.EspejoId;
        segIngreso.TrasladoGranjaContraparteId = origen.GranjaId;
        segIngreso.Observaciones = string.IsNullOrWhiteSpace(segIngreso.Observaciones)
            ? $"Traslado INGRESO ← {origen.LoteNombre}. {dto.Observaciones ?? ""}".Trim()
            : $"{segIngreso.Observaciones} | Traslado INGRESO ← {origen.LoteNombre}";
        segIngreso.UpdatedAt = fechaUtc;
    }

    /// <summary>
    /// Pata SALIDA en Producción (Feature 14, paridad con Levante): acumulados del LPP origen +
    /// UPSERT del registro SALIDA en la canónica seguimiento_diario_produccion.
    /// </summary>
    private async Task AplicarSalidaProduccionAsync(
        LotePosturaProduccion lppOrigen, LadoTraslado destino, TrasladoAvesDesdeSegDiarioDto dto,
        DateTime fechaAncla, DateTime fechaUtc, int usuarioId, int companyId, CancellationToken ct)
    {
        lppOrigen.ProduccionTrasladoSalidaHembras += dto.TrasladoHembras;
        lppOrigen.ProduccionTrasladoSalidaMachos  += dto.TrasladoMachos;
        lppOrigen.AvesHActual = (lppOrigen.AvesHActual ?? 0) - dto.TrasladoHembras;
        lppOrigen.AvesMActual = (lppOrigen.AvesMActual ?? 0) - dto.TrasladoMachos;

        int createdByIdP = usuarioId; // AuditableEntity.CreatedByUserId es int

        var (fechaDia, rangoDesde, rangoHasta) = RangoDiaCalendario(fechaAncla);
        var segSalidaP = (await _ctx.SeguimientoProduccion
                .Where(s => s.LoteId == lppOrigen.LoteId!.Value
                         && s.Fecha >= rangoDesde && s.Fecha < rangoHasta)
                .ToListAsync(ct))
            .FirstOrDefault(s => s.Fecha.Date == fechaDia);

        if (segSalidaP is null)
        {
            segSalidaP = new SeguimientoProduccion
            {
                // El traslado YA movió el maestro y no separa nada: la fila nace validada. Con
                // `validado = false` (el default) estas filas aparecían pendientes en las empresas con
                // doble validación y, a las 24 h, BLOQUEABAN el alta de días nuevos del lote sin que
                // hubiera nada que validar — mismo defecto que tenían los Crud.
                Validado = true,
                LoteId = lppOrigen.LoteId!.Value,
                Fecha = fechaAncla,
                MortalidadH = 0, MortalidadM = 0,
                SelH = 0, SelM = 0,
                ErrorSexajeHembras = 0, ErrorSexajeMachos = 0,
                TipoAlimento = "—",
                CompanyId = companyId,
                CreatedByUserId = createdByIdP,
                CreatedAt = fechaUtc
            };
            _ctx.SeguimientoProduccion.Add(segSalidaP);
        }

        segSalidaP.TrasladoSalidaHembras += dto.TrasladoHembras;
        segSalidaP.TrasladoSalidaMachos  += dto.TrasladoMachos;
        segSalidaP.TrasladoHembras       = (segSalidaP.TrasladoHembras ?? 0) + dto.TrasladoHembras;
        segSalidaP.TrasladoMachos        = (segSalidaP.TrasladoMachos  ?? 0) + dto.TrasladoMachos;
        segSalidaP.LoteDestinoId         = destino.EspejoId;
        segSalidaP.GranjaDestinoId       = destino.GranjaId;
        segSalidaP.FechaTraslado         = fechaAncla;
        segSalidaP.EsTraslado            = true;
        segSalidaP.TrasladoDireccion     = "SALIDA";
        segSalidaP.TrasladoLoteContraparteId   = destino.EspejoId;
        segSalidaP.TrasladoGranjaContraparteId = destino.GranjaId;
        segSalidaP.TrasladoObservaciones = string.IsNullOrWhiteSpace(segSalidaP.TrasladoObservaciones)
            ? $"Traslado SALIDA → {destino.LoteNombre}. {dto.Observaciones ?? ""}".Trim()
            : $"{segSalidaP.TrasladoObservaciones} | Traslado SALIDA → {destino.LoteNombre}";
        segSalidaP.UpdatedAt = fechaUtc;
        segSalidaP.UpdatedByUserId = createdByIdP;
    }

    /// <summary>Pata INGRESO en Producción: acumulados del LPP destino + UPSERT del registro INGRESO.</summary>
    private async Task AplicarIngresoProduccionAsync(
        LotePosturaProduccion lppDestino, LadoTraslado origen, TrasladoAvesDesdeSegDiarioDto dto,
        DateTime fechaAncla, DateTime fechaUtc, int usuarioId, int companyId, CancellationToken ct)
    {
        lppDestino.ProduccionTrasladoIngresoHembras += dto.TrasladoHembras;
        lppDestino.ProduccionTrasladoIngresoMachos  += dto.TrasladoMachos;
        lppDestino.AvesHActual = (lppDestino.AvesHActual ?? 0) + dto.TrasladoHembras;
        lppDestino.AvesMActual = (lppDestino.AvesMActual ?? 0) + dto.TrasladoMachos;

        int createdByIdP = usuarioId;

        var (fechaDia, rangoDesde, rangoHasta) = RangoDiaCalendario(fechaAncla);
        var segIngresoP = (await _ctx.SeguimientoProduccion
                .Where(s => s.LoteId == lppDestino.LoteId!.Value
                         && s.Fecha >= rangoDesde && s.Fecha < rangoHasta)
                .ToListAsync(ct))
            .FirstOrDefault(s => s.Fecha.Date == fechaDia);

        if (segIngresoP is null)
        {
            segIngresoP = new SeguimientoProduccion
            {
                // El traslado YA movió el maestro y no separa nada: la fila nace validada. Con
                // `validado = false` (el default) estas filas aparecían pendientes en las empresas con
                // doble validación y, a las 24 h, BLOQUEABAN el alta de días nuevos del lote sin que
                // hubiera nada que validar — mismo defecto que tenían los Crud.
                Validado = true,
                LoteId = lppDestino.LoteId!.Value,
                Fecha = fechaAncla,
                MortalidadH = 0, MortalidadM = 0,
                SelH = 0, SelM = 0,
                ErrorSexajeHembras = 0, ErrorSexajeMachos = 0,
                TipoAlimento = "—",
                CompanyId = companyId,
                CreatedByUserId = createdByIdP,
                CreatedAt = fechaUtc
            };
            _ctx.SeguimientoProduccion.Add(segIngresoP);
        }

        segIngresoP.TrasladoIngresoHembras += dto.TrasladoHembras;
        segIngresoP.TrasladoIngresoMachos  += dto.TrasladoMachos;
        segIngresoP.EsTraslado              = true;
        segIngresoP.TrasladoDireccion       = "INGRESO";
        segIngresoP.TrasladoLoteContraparteId   = origen.EspejoId;
        segIngresoP.TrasladoGranjaContraparteId = origen.GranjaId;
        segIngresoP.TrasladoObservaciones = string.IsNullOrWhiteSpace(segIngresoP.TrasladoObservaciones)
            ? $"Traslado INGRESO ← {origen.LoteNombre}. {dto.Observaciones ?? ""}".Trim()
            : $"{segIngresoP.TrasladoObservaciones} | Traslado INGRESO ← {origen.LoteNombre}";
        segIngresoP.UpdatedAt = fechaUtc;
        segIngresoP.UpdatedByUserId = createdByIdP;
    }
}
