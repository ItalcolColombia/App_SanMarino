// src/ZooSanMarino.Application/Calculos/SemanasCicloPosturaCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Etapa del ciclo de vida del ave por semana de edad (desde el encasetamiento / creación del
/// ítem) y por raza. Aplica solo a empresas con <c>Company.SemanasCicloPosturaPorRaza</c> = true
/// (hoy, Santa Reyes) — las demás siguen con los cortes fijos existentes
/// (<c>ZooSanMarino.Infrastructure.Services.MovimientoAvesCalculos.EtapaProduccion</c> y su espejo
/// de frontend), que este cálculo no toca ni reemplaza.
///
/// <para>
/// Fuente: <c>Requerimientos de Italapp.docx</c> (sección «Consumo de alimento», 18-ago-2026) —
/// "Desde la creación del Item, son 8 sem en alistamiento, y 16 semanas en etapa de levante...
/// 4 semanas en etapa levante pero en granjas de producción y 74/84 semanas en etapa de postura".
/// Los cuatro primeros cortes (alistamiento/levante/levante en producción) son IGUALES para los
/// dos grupos de raza; solo la duración de la postura difiere (74 vs 84 semanas).
/// </para>
/// </summary>
public static class SemanasCicloPosturaCalculos
{
    public const string Alistamiento        = "Alistamiento";
    public const string Levante             = "Levante";
    public const string LevanteEnProduccion = "LevanteEnProduccion";
    public const string Postura             = "Postura";
    public const string FueraDeCiclo        = "FueraDeCiclo";

    public const int SemanasAlistamiento        = 8;
    public const int SemanasLevante             = 16;
    public const int SemanasLevanteEnProduccion = 4;
    public const int SemanasPosturaRojaCriolla  = 74;
    public const int SemanasPosturaBlancaAzur   = 84;

    private const int FinAlistamiento       = SemanasAlistamiento;                          // 8
    private const int FinLevante            = FinAlistamiento + SemanasLevante;             // 24
    private const int FinLevanteEnProduccion = FinLevante + SemanasLevanteEnProduccion;     // 28

    /// <summary>Tokens que identifican el grupo Blancas/Azur (84 semanas de postura).</summary>
    private static readonly string[] TokensBlancaAzur = { "LOHMANN", "AZUR" };

    /// <summary>
    /// Tokens que identifican el grupo Rojas/Criollas (74 semanas de postura). <c>BROWN</c> está acá
    /// porque nombra al ave marrón en las tres líneas que el cliente maneja (Babcock Brown, Hy Line
    /// Brown, Lohmann Brown), y es lo que desempata a <c>Lohmann Brown</c> — ver
    /// <see cref="EsGrupoBlancaAzur"/>.
    /// </summary>
    private static readonly string[] TokensRojaCriolla = { "BABCOCK", "BABCOK", "HY LINE", "HYLINE", "CRIOLLA", "BROWN" };

    /// <summary>
    /// ¿La raza pertenece al grupo Blancas/Azur? <c>null</c> si la raza es nula/vacía o no coincide
    /// con ningún token conocido — no se adivina el grupo: el caller debe tratarlo como
    /// indeterminado en vez de mostrar una etapa que podría estar mal.
    ///
    /// <para>
    /// 🔴 <b>El orden importa y no se puede invertir.</b> Los tokens se buscan por
    /// <c>Contains</c>, y <c>"Lohmann Brown"</c> contiene <c>"LOHMANN"</c>: evaluando primero
    /// Blancas/Azur se clasificaba como blanca (84 semanas, fin de ciclo en la 112) cuando el
    /// <c>Lotes.xlsx</c> del cliente la declara <c>ROJA</c> (74 semanas, fin de ciclo en la 102).
    /// Rojas/Criollas va primero porque su token <c>BROWN</c> es el más específico: <c>Lohmann
    /// LSL</c> (blanca) no lo tiene y sigue cayendo en <c>LOHMANN</c>, como siempre.
    /// </para>
    /// </summary>
    public static bool? EsGrupoBlancaAzur(string? raza)
    {
        if (string.IsNullOrWhiteSpace(raza)) return null;
        var r = raza.Trim();

        if (TokensRojaCriolla.Any(t => r.Contains(t, StringComparison.OrdinalIgnoreCase))) return false;
        if (TokensBlancaAzur.Any(t => r.Contains(t, StringComparison.OrdinalIgnoreCase))) return true;

        return null;
    }

    /// <summary>
    /// Etapa del ciclo de vida para esta raza a esta edad (semanas desde encasetamiento).
    /// <c>null</c> si la raza no se reconoce (ver <see cref="EsGrupoBlancaAzur"/>) o si
    /// <paramref name="semanasDesdeEncaset"/> es menor a 1.
    /// </summary>
    public static string? ObtenerEtapa(string? raza, int semanasDesdeEncaset)
    {
        if (semanasDesdeEncaset < 1) return null;

        var esBlancaAzur = EsGrupoBlancaAzur(raza);
        if (esBlancaAzur is null) return null;

        if (semanasDesdeEncaset <= FinAlistamiento) return Alistamiento;
        if (semanasDesdeEncaset <= FinLevante) return Levante;
        if (semanasDesdeEncaset <= FinLevanteEnProduccion) return LevanteEnProduccion;

        var semanasPostura = esBlancaAzur.Value ? SemanasPosturaBlancaAzur : SemanasPosturaRojaCriolla;
        var finPostura = FinLevanteEnProduccion + semanasPostura;

        return semanasDesdeEncaset <= finPostura ? Postura : FueraDeCiclo;
    }
}
