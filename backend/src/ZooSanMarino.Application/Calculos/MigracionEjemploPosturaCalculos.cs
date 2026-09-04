// src/ZooSanMarino.Application/Calculos/MigracionEjemploPosturaCalculos.cs
using ZooSanMarino.Application.DTOs.Migracion;

namespace ZooSanMarino.Application.Calculos;

/// <summary>Un bloque de la hoja "Ejemplo": el nombre de la hoja que ilustra, sus encabezados, filas de muestra y las notas que explican las decisiones no obvias.</summary>
/// <param name="Hoja">Hoja real de la plantilla que este bloque ilustra ("Datos", "Alimento", …).</param>
/// <param name="Encabezados">Los MISMOS encabezados que emite esa hoja, en el mismo orden.</param>
/// <param name="Filas">Filas de ejemplo, alineadas 1 a 1 con <paramref name="Encabezados"/>.</param>
/// <param name="Notas">Por qué una celda quedó vacía o por qué el valor es el que es.</param>
public sealed record BloqueEjemplo(
    string Hoja,
    IReadOnlyList<string> Encabezados,
    IReadOnlyList<IReadOnlyList<string>> Filas,
    IReadOnlyList<string> Notas);

/// <summary>
/// Datos con los que se arma el ejemplo: salen de los catálogos REALES de la empresa que la plantilla
/// ya carga para la hoja "Referencias", así que el ejemplo es copiable tal cual.
/// </summary>
/// <param name="FechaBase">Primer día del ejemplo (típicamente el encasetamiento del lote).</param>
/// <param name="AlimentoNombre">Nombre de un alimento del inventario de la empresa; null si no tiene ninguno.</param>
/// <param name="ItemHuevoNombre">Nombre de un tipo de huevo declarado por el lote; null si no aplica.</param>
/// <param name="ItemHuevoNombre2">Segundo tipo de huevo, para mostrar el desglose con más de una fila por fecha.</param>
/// <param name="LoteContraparte">Nombre de otro lote de la misma fase, para "Movimientos Aves".</param>
/// <param name="SiloNombre">Silo de la granja; solo aplica a empresas que ubican el alimento por silo.</param>
public sealed record DatosEjemploPostura(
    DateTime FechaBase,
    string? AlimentoNombre = null,
    string? ItemHuevoNombre = null,
    string? ItemHuevoNombre2 = null,
    string? LoteContraparte = null,
    string? SiloNombre = null);

/// <summary>
/// Cálculo PURO que arma la hoja "Ejemplo" de la plantilla de seguimiento de postura: 3 días de
/// muestra ya resueltos, con los mismos encabezados que la plantilla acaba de emitir.
///
/// <para>
/// 🔴 El ejemplo se DERIVA de las columnas emitidas, nunca de una lista propia. Por eso no puede
/// enseñarle a una empresa una columna que su plantilla no trae (el caso que importa: mostrarle
/// mortalidad de machos o las 11 categorías de huevo a Santa Reyes, que tiene las dos cosas
/// apagadas). Una columna que el cálculo no sabe ilustrar sale VACÍA, que es información correcta:
/// todas menos "Fecha" son opcionales.
/// </para>
///
/// <para>
/// La hoja resultante NO se importa: el lector solo mira "Datos", "Alimento", "Movimientos Aves",
/// "Movimientos Huevos" y "Huevos", y cualquier otra hoja se ignora.
/// </para>
/// </summary>
public static class MigracionEjemploPosturaCalculos
{
    /// <summary>Cantidad de días de ejemplo. Tres alcanzan para mostrar variación sin volverse una tabla.</summary>
    public const int DiasDeEjemplo = 3;

    /// <summary>Rótulo que encabeza la hoja. Va en el cálculo puro para que el test lo pueda fijar.</summary>
    public const string Advertencia =
        "ESTA HOJA NO SE IMPORTA — es solo una guía. Copiá los valores a la hoja que corresponda.";

    /// <summary>
    /// Arma los bloques de ejemplo para la plantilla ya generada.
    /// </summary>
    /// <param name="esLevante">true = Seguimiento Levante; false = Seguimiento Producción.</param>
    /// <param name="flags">Flags de la empresa (los mismos que decidieron las columnas emitidas).</param>
    /// <param name="columnasDatos">Los encabezados que la hoja "Datos" emitió, en orden.</param>
    /// <param name="datos">Catálogos reales de la empresa.</param>
    /// <param name="incluyeHojaHuevos">true si la plantilla emitió la hoja "Huevos" (clasificación por ítem).</param>
    public static IReadOnlyList<BloqueEjemplo> Bloques(
        bool esLevante,
        FlagsPlantillaPostura flags,
        IReadOnlyList<string> columnasDatos,
        DatosEjemploPostura datos,
        bool incluyeHojaHuevos)
    {
        var bloques = new List<BloqueEjemplo> { BloqueDatos(esLevante, flags, columnasDatos, datos) };

        // Solo se ilustran las hojas que la plantilla realmente emitió.
        if (PlantillaPosturaCalculos.EmiteHojaAlimento(flags)) bloques.Add(BloqueAlimento(datos));
        bloques.Add(BloqueMovimientosAves(flags, datos));
        if (incluyeHojaHuevos) bloques.Add(BloqueHuevos(datos));

        return bloques;
    }

    // ── Hoja "Datos" ─────────────────────────────────────────────────────────────────────────────

    private static BloqueEjemplo BloqueDatos(
        bool esLevante, FlagsPlantillaPostura flags,
        IReadOnlyList<string> columnas, DatosEjemploPostura datos)
    {
        var emitidas = new HashSet<string>(columnas, StringComparer.Ordinal);
        // Solo se enseña el alimento del INVENTARIO si la plantilla lo ofrece: una empresa con el
        // alimento en silos no trae esos slots, y dejarle el consumo directo vacío la dejaría sin
        // ningún camino a la vista.
        var usaAlimentoDelInventario = emitidas.Contains("Alimento 1 H")
                                       && !string.IsNullOrWhiteSpace(datos.AlimentoNombre);
        // Con las categorías a la vista, el total y los incubables se DERIVAN de ellas: dejarlos
        // vacíos evita enseñar la combinación que dispara la advertencia de discrepancia.
        var hayCategorias = emitidas.Contains("Huevo Limpio");

        var filas = new List<IReadOnlyList<string>>();
        for (int dia = 0; dia < DiasDeEjemplo; dia++)
            filas.Add(columnas
                .Select(c => ValorDatos(c, dia, esLevante, usaAlimentoDelInventario, hayCategorias, datos))
                .ToList());

        var notas = new List<string>();
        if (usaAlimentoDelInventario)
            notas.Add("«Consumo H (kg)» va VACÍO a propósito: la fila usa «Alimento 1 H» + «Consumo Alimento 1 H», "
                    + "que descuentan el stock real. Si ponés los dos, el consumo directo se ignora.");
        else if (flags.ManejaInventarioPorSilo)
            notas.Add("Esta empresa ubica el alimento en SILOS. La carga masiva todavía no mueve inventario por silo, "
                    + "así que el consumo va en «Consumo H (kg)»: se guarda en el día y NO descuenta stock. "
                    + "Las entradas de alimento se cargan por pantalla.");
        else
            notas.Add("«Consumo H (kg)» es el consumo directo: se guarda en el día pero NO toca el inventario. "
                    + "Para que descuente stock, usá «Alimento 1 H» + «Consumo Alimento 1 H».");

        if (hayCategorias)
            notas.Add("«Huevo Total» y «Huevo Incubable» van VACÍOS: se calculan del desglose de las 11 categorías.");
        else if (!esLevante && flags.ClasificacionHuevoPorItems)
            notas.Add("Esta empresa clasifica el huevo POR ÍTEM del catálogo: el desglose va en la hoja «Huevos», "
                    + "no en esta. El total del día se calcula de esas filas.");

        if (flags.OcultaMachosEnPostura)
            notas.Add("Esta empresa no maneja machos en postura: la plantilla no trae ninguna columna de machos.");

        notas.Add("Una celda vacía vale 0 (conteos) o «sin dato» (pesos y agua). Solo «Fecha» es obligatoria.");
        notas.Add("Una fila por día y por lote. No repitas la misma fecha: la segunda se rechaza.");

        return new BloqueEjemplo("Datos", columnas, filas, notas);
    }

    /// <summary>
    /// Valor de ejemplo de UNA columna de la hoja "Datos". Un título que el cálculo no conoce
    /// devuelve cadena vacía — nunca lanza y nunca inventa una columna.
    /// </summary>
    private static string ValorDatos(
        string titulo, int dia, bool esLevante,
        bool usaAlimentoDelInventario, bool hayCategorias, DatosEjemploPostura datos)
        => titulo switch
        {
            "Fecha" => datos.FechaBase.AddDays(dia).ToString("yyyy-MM-dd"),

            "Mort H" => Serie(dia, "12", "8", "5"),
            "Mort M" => Serie(dia, "3", "1", "0"),
            "Sel H" => Serie(dia, "5", "0", "2"),
            "Sel M" => Serie(dia, "0", "0", "1"),
            "Error Sexaje H" => "0",
            "Error Sexaje M" => "0",

            // Con alimento del inventario, el consumo directo se deja vacío a propósito.
            "Consumo H (kg)" => usaAlimentoDelInventario ? "" : Serie(dia, "320.5", "318.0", "325.4"),
            "Consumo M (kg)" => usaAlimentoDelInventario ? "" : Serie(dia, "41.2", "40.8", "42.0"),
            "Unidad Consumo" => "kg",

            "Alimento 1 H" => datos.AlimentoNombre ?? "",
            "Consumo Alimento 1 H" => usaAlimentoDelInventario ? Serie(dia, "320.5", "318.0", "325.4") : "",
            "Alimento 1 M" => datos.AlimentoNombre ?? "",
            "Consumo Alimento 1 M" => usaAlimentoDelInventario ? Serie(dia, "41.2", "40.8", "42.0") : "",
            // El segundo alimento es opcional: se deja vacío para no sugerir que hace falta.
            "Alimento 2 H" or "Consumo Alimento 2 H" or "Alimento 2 M" or "Consumo Alimento 2 M" => "",

            "Peso H (g)" => esLevante ? Serie(dia, "1450", "", "") : Serie(dia, "1760", "", ""),
            "Peso M (g)" => esLevante ? Serie(dia, "1980", "", "") : Serie(dia, "2310", "", ""),
            "Uniformidad H" or "Uniformidad" => Serie(dia, "85.2", "", ""),
            "Uniformidad M" => Serie(dia, "82.7", "", ""),
            "Coef. Variación H" or "Coef. Variación" => Serie(dia, "8.4", "", ""),
            "Coef. Variación M" => Serie(dia, "9.1", "", ""),
            "Observaciones Pesaje" => Serie(dia, "Pesaje semanal", "", ""),

            // Huevos por columnas fijas (empresas sin clasificación por ítem).
            "Huevo Total" or "Huevo Incubable" when hayCategorias => "",
            "Huevo Total" => Serie(dia, "8450", "8610", "8580"),
            "Huevo Incubable" => Serie(dia, "8100", "8250", "8230"),
            "Huevo Limpio" => Serie(dia, "7900", "8040", "8020"),
            "Huevo Tratado" => Serie(dia, "200", "210", "210"),
            "Huevo Sucio" => Serie(dia, "180", "190", "185"),
            "Huevo Deforme" => Serie(dia, "60", "62", "58"),
            "Huevo Blanco" => "0",
            "Huevo Doble Yema" => Serie(dia, "10", "12", "11"),
            "Huevo Piso" => Serie(dia, "70", "75", "72"),
            "Huevo Pequeño" => Serie(dia, "20", "18", "17"),
            "Huevo Roto" => Serie(dia, "10", "3", "7"),
            "Huevo Desecho" => "0",
            "Huevo Otro" => "0",
            "Peso Huevo (g)" => Serie(dia, "62.5", "62.8", "62.6"),

            "Etapa" => "1",
            "Tipo Alimento" => "",

            "Consumo Agua (L)" => Serie(dia, "2450", "2470", "2460"),
            "pH Agua" => "7.2",
            "ORP Agua (mV)" => "650",
            "Temperatura Agua (°C)" => "22.5",

            "Observaciones" => Serie(dia, "Día normal", "", "Revisión de comederos"),

            _ => "",
        };

    // ── Hoja "Alimento" ──────────────────────────────────────────────────────────────────────────

    private static BloqueEjemplo BloqueAlimento(DatosEjemploPostura datos)
    {
        var columnas = MigracionEsquemas.AlimentoPostura.Columnas.Select(c => c.Titulo).ToList();
        var alimento = datos.AlimentoNombre ?? "(elegí uno de la hoja Referencias)";

        var filas = new List<IReadOnlyList<string>>
        {
            columnas.Select(c => c switch
            {
                "Fecha" => datos.FechaBase.ToString("yyyy-MM-dd"),
                "Movimiento" => "Ingreso",
                "Alimento" => alimento,
                "Cantidad" => "5000",
                "Unidad" => "kg",
                "Origen" => "planta",
                "Referencia" => "REM-00123",
                "Observaciones" => "Entrada del período",
                _ => "",
            }).ToList(),
        };

        var notas = new List<string>
        {
            "Esta hoja carga las ENTRADAS de alimento del período. Se aplican ANTES del seguimiento, "
                + "para que haya stock que descontar cuando la hoja «Datos» consume.",
            "Si el archivo consume más de lo que hay, se rechaza ENTERO indicando cuánto falta.",
            "Ubicación vacía = la del lote seleccionado en pantalla, que es el caso normal.",
        };

        return new BloqueEjemplo(MigracionEsquemas.AlimentoPostura.Hoja, columnas, filas, notas);
    }

    // ── Hoja "Movimientos Aves" ──────────────────────────────────────────────────────────────────

    private static BloqueEjemplo BloqueMovimientosAves(FlagsPlantillaPostura flags, DatosEjemploPostura datos)
    {
        var columnas = MigracionEsquemas.MovimientosAvesLevante.Columnas.Select(c => c.Titulo).ToList();

        var filas = new List<IReadOnlyList<string>>
        {
            columnas.Select(c => c switch
            {
                "Fecha" => datos.FechaBase.AddDays(1).ToString("yyyy-MM-dd"),
                "Tipo" => "Salida",
                "Hembras" => "500",
                // Una empresa sin machos en postura no los mueve: la celda va vacía, no en 0.
                "Machos" => flags.OcultaMachosEnPostura ? "" : "40",
                "Lote Contraparte" => datos.LoteContraparte ?? "(elegí uno de la hoja Referencias)",
                "Observaciones" => "Traslado a otro lote",
                _ => "",
            }).ToList(),
            columnas.Select(c => c switch
            {
                "Fecha" => datos.FechaBase.AddDays(2).ToString("yyyy-MM-dd"),
                "Tipo" => "Venta",
                "Hembras" => "120",
                "Machos" => "",
                "Motivo" => "Venta de descarte",
                _ => "",
            }).ToList(),
        };

        var notas = new List<string>
        {
            "«Salida» descuenta de ESTE lote y exige que el lote contraparte exista en la misma fase; "
                + "NO le acredita las aves (ese lote carga su propio «Ingreso» en su archivo).",
            "«Ingreso» suma a este lote las aves recibidas en tránsito, sin tocar al lote origen.",
            "«Venta» descuenta de este lote y lleva «Motivo»; no lleva contraparte.",
            "No cargues acá movimientos que ya registraste por pantalla: se duplicarían.",
        };
        if (flags.OcultaMachosEnPostura)
            notas.Add("Esta empresa no maneja machos en postura: dejá «Machos» vacío.");

        return new BloqueEjemplo(MigracionEsquemas.MovimientosAvesLevante.Hoja, columnas, filas, notas);
    }

    // ── Hoja "Huevos" (clasificación por ítem del catálogo) ──────────────────────────────────────

    private static BloqueEjemplo BloqueHuevos(DatosEjemploPostura datos)
    {
        var columnas = MigracionEsquemas.HuevosPostura.Columnas.Select(c => c.Titulo).ToList();
        var item1 = datos.ItemHuevoNombre ?? "(elegí uno de la hoja Referencias)";
        var item2 = datos.ItemHuevoNombre2 ?? item1;

        var filas = new List<IReadOnlyList<string>>
        {
            columnas.Select(c => c switch
            {
                "Fecha" => datos.FechaBase.ToString("yyyy-MM-dd"),
                "Ítem" => item1,
                "Cantidad" => "7900",
                _ => "",
            }).ToList(),
            columnas.Select(c => c switch
            {
                "Fecha" => datos.FechaBase.ToString("yyyy-MM-dd"),
                "Ítem" => item2,
                "Cantidad" => "550",
                _ => "",
            }).ToList(),
        };

        var notas = new List<string>
        {
            "Varias filas por fecha: una por tipo de huevo. El total del día se calcula sumándolas.",
            "Cada fecha de esta hoja tiene que existir también en la hoja «Datos».",
            "Solo se aceptan los tipos de huevo DECLARADOS POR EL LOTE. Si el desplegable viene vacío, "
                + "editá el lote y declará qué tipos produce antes de usar esta hoja.",
            "No la combines con las 11 categorías fijas: cargar las dos fuentes el mismo día es un error.",
        };

        return new BloqueEjemplo(MigracionEsquemas.HuevosPostura.Hoja, columnas, filas, notas);
    }

    /// <summary>Valor del día <paramref name="dia"/> dentro de una serie de <see cref="DiasDeEjemplo"/> muestras.</summary>
    private static string Serie(int dia, string d0, string d1, string d2)
        => dia switch { 0 => d0, 1 => d1, _ => d2 };
}
