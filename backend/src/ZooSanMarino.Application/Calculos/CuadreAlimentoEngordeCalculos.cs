// Clasificación del cuadre de alimento de pollo engorde por galpón.
//
// El invariante lo calcula `fn_cuadre_alimento_engorde` en la BD; acá vive la LECTURA de ese número:
// qué se considera cuadrado, qué merece alerta y con qué prioridad. Es puro y testeado a propósito,
// porque la tolerancia y el orden de severidad son decisiones de negocio que si viven inline en una
// consulta nadie vuelve a mirar.
//
// Contexto: el descuadre que originó el trabajo de jul-2026 lo detectó un humano de operación semanas
// después de producirse. Nada en el sistema lo verificaba.
namespace ZooSanMarino.Application.Calculos;

/// <summary>Veredicto del cuadre de un galpón, de menor a mayor severidad.</summary>
public enum EstadoCuadreAlimento
{
    /// <summary>El saldo de la tabla diaria coincide con el stock físico y no hay filas negativas.</summary>
    Ok = 0,

    /// <summary>
    /// Cuadra contra el stock pero el ciclo tiene días con saldo negativo. No es un error de cálculo:
    /// dice que se consumió alimento cuya llegada no está registrada (decisión v9, se muestra tal cual).
    /// </summary>
    SaldoNegativo = 1,

    /// <summary>
    /// La tabla diaria y el inventario dejaron de contar lo mismo. Esto SÍ es un defecto: o falta
    /// propagar un movimiento, o el saldo se está calculando distinto en algún camino.
    /// </summary>
    Descuadrado = 2,
}

public static class CuadreAlimentoEngordeCalculos
{
    /// <summary>
    /// Tolerancia en kg. 1 kg absorbe el redondeo a 3 decimales de <c>saldo_alimento_kg</c> acumulado a
    /// lo largo de un ciclo, sin llegar a tapar ningún descuadre real: los que se vieron en producción
    /// iban de 480 kg a 37.880 kg.
    /// </summary>
    public const decimal ToleranciaKg = 1m;

    /// <summary>
    /// Clasifica un galpón. <paramref name="descuadreKg"/> es
    /// <c>saldo_tabla − (stock − movimientos_posteriores)</c>, tal como lo devuelve
    /// <c>fn_cuadre_alimento_engorde</c>.
    /// <para>El descuadre pesa más que el saldo negativo: el primero es un defecto, el segundo es
    /// información de la operación.</para>
    /// </summary>
    public static EstadoCuadreAlimento Clasificar(decimal descuadreKg, int filasNegativas)
    {
        if (Math.Abs(descuadreKg) > ToleranciaKg) return EstadoCuadreAlimento.Descuadrado;
        if (filasNegativas > 0)                   return EstadoCuadreAlimento.SaldoNegativo;
        return EstadoCuadreAlimento.Ok;
    }

    /// <summary>¿Este galpón exige que alguien lo mire?</summary>
    public static bool RequiereAtencion(decimal descuadreKg, int filasNegativas)
        => Clasificar(descuadreKg, filasNegativas) != EstadoCuadreAlimento.Ok;

    /// <summary>
    /// Explicación en una línea para la pantalla y para el log, en el idioma de la operación.
    /// </summary>
    public static string Describir(decimal descuadreKg, int filasNegativas)
        => Clasificar(descuadreKg, filasNegativas) switch
        {
            EstadoCuadreAlimento.Descuadrado when descuadreKg > 0 =>
                $"La tabla diaria muestra {Kg(descuadreKg)} kg de MÁS que el inventario.",
            EstadoCuadreAlimento.Descuadrado =>
                $"La tabla diaria muestra {Kg(Math.Abs(descuadreKg))} kg de MENOS que el inventario.",
            EstadoCuadreAlimento.SaldoNegativo =>
                $"Cuadra contra el inventario, pero {filasNegativas} día(s) cierran en negativo: " +
                "se consumió alimento cuya llegada no está registrada.",
            _ => "Cuadra con el inventario.",
        };

    private static string Kg(decimal v) => v.ToString("N1", System.Globalization.CultureInfo.InvariantCulture);
}
