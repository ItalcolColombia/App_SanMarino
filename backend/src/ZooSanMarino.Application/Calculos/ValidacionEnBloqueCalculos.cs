using System.Globalization;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Desenlace de UN registro dentro del bloque. Viaja como <c>string</c>, igual que
/// <c>RegistroValidacionDto.Estado</c>: un enum se serializaría como número y el front ya compara
/// contra literales.
/// </summary>
public static class DesenlaceValidacionEnBloque
{
    /// <summary>Se validó ahora y aplicó algo (kilos, aves o líneas de alimento).</summary>
    public const string Validado = "VALIDADO";

    /// <summary>
    /// Se validó ahora pero no aplicó nada: el día no tenía reservas —sin consumo y sin bajas—. Se
    /// separa de <see cref="Validado"/> porque el resumen tiene que poder decir la verdad sobre qué
    /// movió el bloque; contarlo como validado con efecto haría que «se aplicaron 0 kg» conviva con
    /// «se validaron 12 registros» sin explicación.
    /// </summary>
    public const string ValidadoSinEfecto = "VALIDADO_SIN_EFECTO";

    /// <summary>
    /// Ya estaba validado cuando se lo intentó (carrera con otra pestaña, o un reintento del bloque).
    /// <b>No es falla</b>: no corta.
    /// </summary>
    public const string YaValidado = "YA_VALIDADO";

    /// <summary>Falló, y es el que cortó el bloque.</summary>
    public const string Fallo = "FALLO";

    /// <summary>No se intentó: quedó después del corte, o fuera del tope.</summary>
    public const string NoIntentado = "NO_INTENTADO";
}

/// <summary>Un registro candidato del bloque, tal como sale de la lectura de pendientes.</summary>
public readonly record struct PendienteValidacion(long SeguimientoId, DateOnly Fecha);

/// <summary>
/// Una línea del reporte del bloque. Espeja el DTO pero vive acá para que el cálculo no dependa de
/// <c>Application.DTOs</c> — la dependencia va al revés.
/// </summary>
public sealed record ItemValidacionEnBloque(
    long SeguimientoId,
    DateOnly Fecha,
    string Resultado,
    int ItemsAplicados,
    decimal KgAplicados,
    int AvesDescontadas,
    string? Motivo);

/// <summary>Cierre del bloque. <c>Mensaje</c> es el texto que la UI muestra tal cual.</summary>
public sealed record ResumenValidacionEnBloque(
    int Solicitados,
    int Validados,
    int YaValidados,
    int Fallidos,
    int NoIntentados,
    decimal KgAplicados,
    int AvesDescontadas,
    long? SeguimientoCorte,
    DateOnly? FechaCorte,
    string? MotivoCorte,
    string Mensaje);

/// <summary>
/// Reglas <b>puras</b> del «validar todos los pendientes del lote»: qué se intenta, en qué orden,
/// cuándo se corta y qué se le dice al usuario. Sin EF, sin contexto, sin reloj propio.
///
/// <para>
/// <b>Por qué existe el feature.</b> Se validaba de a uno, y ItalcolPanama llegó a cargar 34 días en
/// una sola sesión. Con el plazo contado desde la creación esos días entran completos y vencen todos
/// juntos al día siguiente, así que el bloque a confirmar sólo crece.
/// </para>
///
/// <para>
/// <b>Se llama «en bloque» y no «de lote» a propósito.</b> En este repo <c>lote</c> es la parvada
/// (<c>lote_ave_engorde</c>, <c>lote_postura_levante</c>), y <c>ValidarLote</c> ya significa otras dos
/// cosas: «¿este lote es liquidable?» en liquidación técnica, y «batch» en el push offline. Bloque
/// para el batch; lote queda reservado a la parvada.
/// </para>
/// </summary>
public static class ValidacionEnBloqueCalculos
{
    /// <summary>
    /// Tope de registros por bloque. Se toman los <b>primeros N cronológicos</b> y el resto queda
    /// <see cref="DesenlaceValidacionEnBloque.NoIntentado"/>.
    ///
    /// <para>
    /// <b>Diverge a propósito del tope del push offline</b>, que rechaza el lote entero cuando se pasa.
    /// Allá el motivo es que el dispositivo daría por enviadas operaciones que el servidor nunca vio;
    /// acá no hay outbox, y validar los más viejos <b>es</b> progreso — son justamente los que
    /// bloquean el alta de días nuevos. El número supera con margen el pico medido de 34 días.
    /// </para>
    /// </summary>
    public const int MaxRegistrosPorBloque = 60;

    /// <summary>
    /// Orden y recorte del bloque: <b>cronológico, del más viejo al más nuevo</b>, desempatando por id.
    ///
    /// <para>
    /// 🔴 <b>El orden no es cosmético: cambia el resultado.</b> La guarda de aves compara
    /// <b>totales</b> (<c>ReservaSeguimientoCalculos.MotivoAvesNoAplicable</c>) mientras el descuento
    /// recorta <b>por bucket</b> (<c>RetiroAvesEngordeCalculos.AplicarPorBucket</c>, con
    /// <c>Math.Min</c> por género). Con un lote de 100 hembras y 0 machos, un día que baja 50 machos y
    /// otro que baja 60 hembras validan los dos en un orden y cortan en el otro. Y con corte a la
    /// primera falla, el subconjunto que sobrevive depende del orden aunque la resta sea conmutativa.
    /// Por eso el orden lo impone el <b>servidor</b> y no llega nunca del cliente.
    /// </para>
    ///
    /// <para>
    /// El desempate por id existe para que dos filas del mismo día no dependan del orden en que las
    /// devolvió la consulta.
    /// </para>
    /// </summary>
    /// <param name="pendientes">Pendientes del lote, ya acotados por empresa por el service.</param>
    /// <param name="tope">Máximo a intentar. Ver <see cref="MaxRegistrosPorBloque"/>.</param>
    public static (IReadOnlyList<PendienteValidacion> Seleccionados,
                   IReadOnlyList<PendienteValidacion> FueraDeTope) OrdenDeValidacion(
        IEnumerable<PendienteValidacion>? pendientes,
        int tope = MaxRegistrosPorBloque)
    {
        var vacio = Array.Empty<PendienteValidacion>();
        if (pendientes is null) return (vacio, vacio);

        var ordenados = pendientes
            .OrderBy(p => p.Fecha)
            .ThenBy(p => p.SeguimientoId)
            .ToList();

        if (tope < 0) tope = 0;
        if (ordenados.Count <= tope) return (ordenados, vacio);

        return (ordenados.Take(tope).ToList(), ordenados.Skip(tope).ToList());
    }

    /// <summary>Item de un registro que se intentó y devolvió resultado.</summary>
    /// <param name="yaEstabaValidado">
    /// Distingue «lo validé yo ahora sin efecto» de «otra pestaña ya lo había validado». Sin ese dato
    /// los dos son <c>(0, 0, 0)</c> y el conteo del bloque miente.
    /// </param>
    public static ItemValidacionEnBloque ItemAplicado(
        PendienteValidacion p, int itemsAplicados, decimal kgAplicados, int avesDescontadas,
        bool yaEstabaValidado)
    {
        var resultado = yaEstabaValidado
            ? DesenlaceValidacionEnBloque.YaValidado
            : (itemsAplicados > 0 || kgAplicados != 0m || avesDescontadas > 0
                ? DesenlaceValidacionEnBloque.Validado
                : DesenlaceValidacionEnBloque.ValidadoSinEfecto);

        return new ItemValidacionEnBloque(
            p.SeguimientoId, p.Fecha, resultado,
            yaEstabaValidado ? 0 : itemsAplicados,
            yaEstabaValidado ? 0m : kgAplicados,
            yaEstabaValidado ? 0 : avesDescontadas,
            Motivo: null);
    }

    /// <summary>Item del registro que cortó el bloque.</summary>
    public static ItemValidacionEnBloque ItemFallido(PendienteValidacion p, string motivo) =>
        new(p.SeguimientoId, p.Fecha, DesenlaceValidacionEnBloque.Fallo, 0, 0m, 0,
            string.IsNullOrWhiteSpace(motivo) ? "No se pudo validar el registro." : motivo.Trim());

    /// <summary>Item de un registro que quedó sin intentar (corte o tope).</summary>
    public static ItemValidacionEnBloque ItemNoIntentado(PendienteValidacion p) =>
        new(p.SeguimientoId, p.Fecha, DesenlaceValidacionEnBloque.NoIntentado, 0, 0m, 0, Motivo: null);

    /// <summary>
    /// Cierra el bloque: cuenta, suma y redacta.
    ///
    /// <para>
    /// <b>Invariante:</b> <c>Validados + YaValidados + Fallidos + NoIntentados == Solicitados</c>.
    /// <c>NO_INTENTADO</c> <b>no es falla</b> — sin esa separación el front sólo puede decir «fallaron
    /// 15» cuando la verdad es «falló 1 y quedan 14 sin intentar», que es lo que le dice al operario
    /// que corrija uno solo y reintente.
    /// </para>
    ///
    /// <para>
    /// Los kilos y las aves suman <b>sólo</b> lo validado ahora: un <c>YA_VALIDADO</c> aporta cero,
    /// igual que el retorno en cero del validar individual.
    /// </para>
    /// </summary>
    public static ResumenValidacionEnBloque Resumir(IReadOnlyList<ItemValidacionEnBloque>? items)
    {
        items ??= Array.Empty<ItemValidacionEnBloque>();

        var validados = items.Count(i =>
            i.Resultado == DesenlaceValidacionEnBloque.Validado ||
            i.Resultado == DesenlaceValidacionEnBloque.ValidadoSinEfecto);
        var yaValidados = items.Count(i => i.Resultado == DesenlaceValidacionEnBloque.YaValidado);
        var fallidos = items.Count(i => i.Resultado == DesenlaceValidacionEnBloque.Fallo);
        var noIntentados = items.Count(i => i.Resultado == DesenlaceValidacionEnBloque.NoIntentado);

        var kg = items.Sum(i => i.KgAplicados);
        var aves = items.Sum(i => i.AvesDescontadas);

        var corte = items.FirstOrDefault(i => i.Resultado == DesenlaceValidacionEnBloque.Fallo);

        return new ResumenValidacionEnBloque(
            Solicitados: items.Count,
            Validados: validados,
            YaValidados: yaValidados,
            Fallidos: fallidos,
            NoIntentados: noIntentados,
            KgAplicados: kg,
            AvesDescontadas: aves,
            SeguimientoCorte: corte?.SeguimientoId,
            FechaCorte: corte?.Fecha,
            MotivoCorte: corte?.Motivo,
            Mensaje: MensajeResultado(
                items.Count, validados, yaValidados, fallidos, noIntentados,
                kg, aves, corte?.Fecha, corte?.Motivo));
    }

    /// <summary>
    /// Texto del resultado, con la concordancia armada entera (sustantivo <b>y</b> verbo).
    ///
    /// <para>
    /// Tiene test byte a byte por el mismo motivo que el mensaje de bloqueo por vencidos: el defecto
    /// que ese test fija era «un registro … que superaron». El front no concatena nada de esto.
    /// </para>
    /// </summary>
    public static string MensajeResultado(
        int solicitados, int validados, int yaValidados, int fallidos, int noIntentados,
        decimal kgAplicados, int avesDescontadas,
        DateOnly? fechaCorte, string? motivoCorte)
    {
        if (solicitados <= 0) return "El lote no tiene registros pendientes de validar.";

        if (fallidos <= 0)
        {
            if (validados == 0 && yaValidados > 0)
                return yaValidados == 1
                    ? "El registro ya estaba validado."
                    : $"Los {yaValidados} registros ya estaban validados.";

            var cabecera = validados == 1
                ? "Se validó 1 registro."
                : $"Se validaron {validados} registros.";
            return $"{cabecera} {Aplicado(kgAplicados, avesDescontadas)}";
        }

        var hechos = validados == 0
            ? $"No se validó ninguno de los {solicitados} registros."
            : validados == 1
                ? $"Se validó 1 de {solicitados} registros."
                : $"Se validaron {validados} de {solicitados} registros.";

        var dia = fechaCorte?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "";
        var porque = string.IsNullOrWhiteSpace(motivoCorte)
            ? "no se pudo validar."
            : $"no se pudo validar: {motivoCorte.Trim()}";
        var corte = $" El del {dia} {porque}";

        var pendiente = noIntentados switch
        {
            0 => "",
            1 => " Quedó 1 registro sin intentar.",
            _ => $" Quedaron {noIntentados} registros sin intentar."
        };

        return $"{hechos}{corte}{pendiente} Corregí ese registro y volvé a validar.";
    }

    /// <summary>
    /// Motivo legible de un choque de concurrencia. El mensaje crudo de una excepción de EF es
    /// ilegible para un operario, y el caso existe: dos usuarios validando el mismo lote a la vez
    /// chocan contra el índice único del histórico unificado.
    /// </summary>
    public static string MotivoConflictoConcurrente() =>
        "Otro usuario está validando este lote al mismo tiempo. Esperá unos segundos y volvé a intentar.";

    /// <summary>Cola del mensaje feliz: qué se aplicó. Separado para poder testearlo suelto.</summary>
    private static string Aplicado(decimal kg, int aves)
    {
        var k = kg.ToString("0.###", CultureInfo.InvariantCulture);
        return $"Se aplicaron {k} kg de alimento y {aves} aves.";
    }
}
