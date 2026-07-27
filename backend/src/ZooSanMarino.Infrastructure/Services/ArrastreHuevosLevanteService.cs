// Arrastre de los huevos capturados en LEVANTE (semana 14+) al primer registro de PRODUCCIÓN,
// al liquidar el lote de levante. Orquesta: la BD filtra y trae solo las columnas de huevos, la
// aritmética la hace HuevosLevanteCalculos (puro y testeado), el espejo se recalcula al final.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <inheritdoc />
public class ArrastreHuevosLevanteService : IArrastreHuevosLevanteService
{
    private const string TipoLevante = "levante";

    /// <summary>
    /// <c>tipo_alimento</c> es NOT NULL en <c>seguimiento_diario_produccion</c>. La fila que crea el
    /// arrastre no tiene consumo, así que va con el mismo marcador que usa
    /// <c>MovimientoAvesService</c> para sus filas de sistema. Si el usuario registra el día, el
    /// merge lo reemplaza por el alimento real.
    /// </summary>
    private const string TipoAlimentoSistema = "N/A";

    private readonly ZooSanMarinoContext _ctx;
    private readonly IEspejoHuevoProduccionSyncService _espejoHuevoSync;
    private readonly ILogger<ArrastreHuevosLevanteService>? _logger;

    public ArrastreHuevosLevanteService(
        ZooSanMarinoContext ctx,
        IEspejoHuevoProduccionSyncService espejoHuevoSync,
        ILogger<ArrastreHuevosLevanteService>? logger = null)
    {
        _ctx = ctx;
        _espejoHuevoSync = espejoHuevoSync;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HuevosClasificacion> CalcularAcumuladoLevanteAsync(
        int lotePosturaLevanteId, int? loteId, CancellationToken ct = default)
    {
        var (acumulado, _) = await LeerLevanteAsync(lotePosturaLevanteId, loteId, ct);
        return acumulado;
    }

    /// <inheritdoc />
    public async Task<(int Totales, int Incubables)> ObtenerTotalesParaCierreAsync(
        int lotePosturaLevanteId, int? loteId, CancellationToken ct = default)
    {
        var acumulado = await CalcularAcumuladoLevanteAsync(lotePosturaLevanteId, loteId, ct);
        return (acumulado.Totales, acumulado.Incubables);
    }

    /// <inheritdoc />
    public async Task<HuevosClasificacion> ArrastrarAsync(
        LotePosturaLevante lev,
        LotePosturaProduccion lpp,
        DateTime fechaLiquidacion,
        int userId,
        CancellationToken ct = default)
    {
        // Los PK de lote_postura_* son int? en el modelo; una entidad traída de la BD siempre los
        // trae informados, así que si faltan es un dato inconsistente y hay que fallar explícito.
        if (lev.LotePosturaLevanteId is not { } levId)
            throw new InvalidOperationException("El lote de levante no tiene identificador.");
        if (lpp.LotePosturaProduccionId is not { } lppId)
            throw new InvalidOperationException("El lote de producción no tiene identificador.");

        var (acumulado, pesoPonderado) = await LeerLevanteAsync(levId, lev.LoteId, ct);
        if (acumulado.EsCero) return HuevosClasificacion.Cero;   // nada que arrastrar: no ensuciar con filas vacías

        // lote_id es NOT NULL en seguimiento_diario_produccion. El LPP hereda el Lote base del
        // levante en el cierre; si aun así no hay ninguno, es un dato inconsistente y hay que
        // avisar en vez de perder los huevos en silencio.
        var loteIdProd = lpp.LoteId ?? lev.LoteId;
        if (loteIdProd is null or <= 0)
            throw new InvalidOperationException(
                "No se puede arrastrar los huevos del levante a producción: el lote no tiene LoteId asociado.");

        // Mediodía UTC: mismo anclaje que usa el front para las fechas puras, así el día calendario
        // coincide con el del seguimiento que el usuario registre después.
        var fecha = FechasPuras.AnclarMediodiaUtc(fechaLiquidacion.Date);

        // Rango de día UTC (no `.Fecha.Date ==`): ver FechasPuras.RangoDiaUtc — date_trunc sobre
        // timestamptz depende de la zona de la sesión de la BD.
        var (diaDesde, diaHasta) = FechasPuras.RangoDiaUtc(fecha);
        var fila = await _ctx.SeguimientoProduccion
            .FirstOrDefaultAsync(s => s.LoteId == loteIdProd.Value
                                   && s.Fecha >= diaDesde && s.Fecha < diaHasta, ct);

        // Idempotencia: solo se arrastra lo que falta contra lo ya marcado en la fila.
        var yaAplicado = HuevosLevanteCalculos.LeerArrastreAplicado(fila?.Metadata);
        var delta = HuevosLevanteCalculos.Delta(acumulado, yaAplicado);
        if (delta.EsCero)
        {
            _logger?.LogInformation(
                "Arrastre de huevos levante {LotePosturaLevanteId}: sin cambios (ya aplicado huevo_tot={Total}).",
                levId, acumulado.Totales);
            return HuevosClasificacion.Cero;
        }

        if (fila is null)
        {
            fila = new SeguimientoProduccion
            {
                LoteId = loteIdProd.Value,
                LotePosturaProduccionId = lppId,
                Fecha = fecha,
                TipoAlimento = TipoAlimentoSistema,
                Etapa = MovimientoAvesCalculos.EtapaProduccion(
                    lev.FechaEncaset.HasValue ? HuevosLevanteCalculos.SemanaVida(fecha, lev.FechaEncaset.Value) : 26),
                Observaciones = "Huevos arrastrados del levante",
                CompanyId = lev.CompanyId,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            _ctx.SeguimientoProduccion.Add(fila);
        }
        else
        {
            // Fila preexistente (traslado de aves, o el usuario ya registró el día): se ACUMULA sin
            // tocar traslado_*, mortalidad, consumo ni el resto de sus datos.
            fila.LotePosturaProduccionId ??= lppId;
            fila.Observaciones = ConcatenarObservacion(fila.Observaciones, "Huevos arrastrados del levante");
            fila.UpdatedByUserId = userId;
            fila.UpdatedAt = DateTime.UtcNow;
        }

        AcumularHuevos(fila, delta);

        // El peso del huevo se toma del promedio ponderado del levante solo si la fila no traía uno
        // propio (el del usuario manda).
        if (pesoPonderado.HasValue && fila.PesoHuevo <= 0)
            fila.PesoHuevo = (decimal)pesoPonderado.Value;

        // La marca guarda el acumulado TOTAL aplicado (no el delta) y conserva el resto del metadata.
        fila.Metadata = HuevosLevanteCalculos.EscribirMarcaArrastre(
            fila.Metadata, acumulado, levId, fecha);

        await _ctx.SaveChangesAsync(ct);
        await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(lppId, ct);

        _logger?.LogInformation(
            "Arrastre de huevos levante {LotePosturaLevanteId} → LPP {Lpp} fecha {Fecha:yyyy-MM-dd}: huevo_tot +{Delta} (acumulado {Total}).",
            levId, lppId, fecha, delta.Totales, acumulado.Totales);

        return delta;
    }

    /// <summary>
    /// Lee de la BD las columnas de huevos de los seguimientos de LEVANTE del lote y devuelve el
    /// acumulado más el peso promedio ponderado.
    /// <para>
    /// La consulta filtra por tipo + lote en SQL y proyecta SOLO 13 columnas; el conjunto está
    /// acotado por la duración del levante (un registro por día, ~175 filas como máximo), así que
    /// el fold se hace con las funciones puras ya testeadas en vez de replicar la aritmética en SQL.
    /// </para>
    /// <para>
    /// Se aceptan las dos formas de vincular el registro al lote (<c>LotePosturaLevanteId</c> o el
    /// <c>LoteId</c> base como texto), igual que hace el resto del módulo con los registros legacy.
    /// </para>
    /// </summary>
    private async Task<(HuevosClasificacion Acumulado, double? PesoPonderado)> LeerLevanteAsync(
        int lotePosturaLevanteId, int? loteId, CancellationToken ct)
    {
        var loteIdTexto = loteId?.ToString();

        var filas = await _ctx.SeguimientoDiario.AsNoTracking()
            .Where(s => s.TipoSeguimiento == TipoLevante
                     && (s.LotePosturaLevanteId == lotePosturaLevanteId
                         || (loteIdTexto != null && s.LoteId == loteIdTexto)))
            .Select(s => new
            {
                s.HuevoLimpio,
                s.HuevoTratado,
                s.HuevoSucio,
                s.HuevoDeforme,
                s.HuevoBlanco,
                s.HuevoDobleYema,
                s.HuevoPiso,
                s.HuevoPequeno,
                s.HuevoRoto,
                s.HuevoDesecho,
                s.HuevoOtro,
                s.PesoHuevo
            })
            .ToListAsync(ct);

        var acumulado = HuevosClasificacion.Cero;
        var pesos = new List<(double? Peso, int Totales)>(filas.Count);

        foreach (var f in filas)
        {
            var dia = new HuevosClasificacion(
                Limpio: f.HuevoLimpio ?? 0,
                Tratado: f.HuevoTratado ?? 0,
                Sucio: f.HuevoSucio ?? 0,
                Deforme: f.HuevoDeforme ?? 0,
                Blanco: f.HuevoBlanco ?? 0,
                DobleYema: f.HuevoDobleYema ?? 0,
                Piso: f.HuevoPiso ?? 0,
                Pequeno: f.HuevoPequeno ?? 0,
                Roto: f.HuevoRoto ?? 0,
                Desecho: f.HuevoDesecho ?? 0,
                Otro: f.HuevoOtro ?? 0);

            if (dia.EsCero) continue;

            acumulado = HuevosLevanteCalculos.Sumar(acumulado, dia);
            pesos.Add((f.PesoHuevo, dia.Totales));
        }

        return (acumulado, HuevosLevanteCalculos.PesoHuevoPonderado(pesos));
    }

    /// <summary>
    /// Suma el delta sobre las columnas de la fila de producción y recalcula
    /// <c>huevo_tot</c>/<c>huevo_inc</c> desde las 11 categorías resultantes (nunca sumándolos
    /// aparte, para que no puedan quedar descuadrados).
    /// </summary>
    private static void AcumularHuevos(SeguimientoProduccion fila, HuevosClasificacion delta)
    {
        var resultado = HuevosLevanteCalculos.Sumar(
            new HuevosClasificacion(
                Limpio: fila.HuevoLimpio,
                Tratado: fila.HuevoTratado,
                Sucio: fila.HuevoSucio,
                Deforme: fila.HuevoDeforme,
                Blanco: fila.HuevoBlanco,
                DobleYema: fila.HuevoDobleYema,
                Piso: fila.HuevoPiso,
                Pequeno: fila.HuevoPequeno,
                Roto: fila.HuevoRoto,
                Desecho: fila.HuevoDesecho,
                Otro: fila.HuevoOtro),
            delta);

        fila.HuevoLimpio = resultado.Limpio;
        fila.HuevoTratado = resultado.Tratado;
        fila.HuevoSucio = resultado.Sucio;
        fila.HuevoDeforme = resultado.Deforme;
        fila.HuevoBlanco = resultado.Blanco;
        fila.HuevoDobleYema = resultado.DobleYema;
        fila.HuevoPiso = resultado.Piso;
        fila.HuevoPequeno = resultado.Pequeno;
        fila.HuevoRoto = resultado.Roto;
        fila.HuevoDesecho = resultado.Desecho;
        fila.HuevoOtro = resultado.Otro;
        fila.HuevoInc = resultado.Incubables;
        fila.HuevoTot = resultado.Totales;
    }

    /// <summary>Concatena observaciones con " | " (mismo patrón que el resto del módulo).</summary>
    private static string ConcatenarObservacion(string? actual, string nueva) =>
        string.IsNullOrWhiteSpace(actual) ? nueva
        : actual.Contains(nueva, StringComparison.OrdinalIgnoreCase) ? actual
        : $"{actual} | {nueva}";
}
