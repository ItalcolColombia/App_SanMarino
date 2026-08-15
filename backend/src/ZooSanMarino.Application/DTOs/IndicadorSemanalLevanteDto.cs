namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Indicador semanal de levante (postura Colombia) calculado en la BD por
/// fn_indicadores_levante_postura. El front solo lo pinta (no calcula).
/// Nombres de columnas = snake_case de la función (mapeo por EF SqlQueryRaw).
/// </summary>
public sealed class IndicadorSemanalLevanteDto
{
    public int Semana { get; set; }
    public double AvesInicioSemana { get; set; }
    public double AvesFinSemana { get; set; }
    public double ConsumoDiario { get; set; }
    public double ConsumoTabla { get; set; }
    public double ConsumoTotalSemana { get; set; }
    public double ConversionAlimenticia { get; set; }
    public double PesoTabla { get; set; }
    public double UnifReal { get; set; }
    public double UnifTabla { get; set; }
    public double MortTabla { get; set; }
    public double DifPesoPct { get; set; }
    public double GananciaSemana { get; set; }
    public double GananciaDiariaAcumulada { get; set; }
    public double GananciaTabla { get; set; }
    public double MortalidadSem { get; set; }
    public double SeleccionSem { get; set; }
    public double ErrorSexajeSem { get; set; }
    public double MortalidadMasSeleccion { get; set; }
    public double Eficiencia { get; set; }
    public double Ip { get; set; }
    public double Vpi { get; set; }
    public double SaldoAvesSemanal { get; set; }
    public double MortalidadAcum { get; set; }
    public double SeleccionAcum { get; set; }
    public double MortalidadMasSeleccionAcum { get; set; }
    public bool PisoTermicoVisible { get; set; }
    public double PesoInicial { get; set; }
    public double PesoCierre { get; set; }
    public int DiasConRegistro { get; set; }

    // REQ-002e / REQ-010b: series POR SEXO para el selector Hembras/Machos/Ambos.
    // La fn expone columnas numeric cuyo nombre es el snake_case EXACTO de estas
    // props (…Hembras→…_hembras, …Machos→…_machos) para que EF SqlQueryRaw las
    // mapee por convención (mismo patrón probado que fn_indicadores_produccion_postura:
    // porcentaje_mortalidad_hembras↔PorcentajeMortalidadHembras). Nullable: NULL
    // cuando el sexo no tiene saldo/pesaje o la guía no trae el dato del sexo. Las
    // columnas mixtas (ConsumoDiario/ConsumoTabla/PesoCierre/MortalidadSem/…) se
    // conservan intactas ("Ambos" las sigue usando).

    // Consumo g/ave/día real (…Hembras/…Machos) y guía SIN promediar (…Tabla…).
    public decimal? ConsumoDiarioHembras { get; set; }
    public decimal? ConsumoDiarioMachos { get; set; }
    public decimal? ConsumoTablaHembras { get; set; }
    public decimal? ConsumoTablaMachos { get; set; }

    // Peso prom por sexo real (con arrastre) y guía (peso_h/_m).
    public decimal? PesoHembras { get; set; }
    public decimal? PesoMachos { get; set; }
    public decimal? PesoTablaHembras { get; set; }
    public decimal? PesoTablaMachos { get; set; }

    // % Mortalidad semanal por sexo real y guía (mort_sem_h/_m).
    public decimal? MortPctHembras { get; set; }
    public decimal? MortPctMachos { get; set; }
    public decimal? MortTablaHembras { get; set; }
    public decimal? MortTablaMachos { get; set; }

    // % Retiro semanal por sexo real (mort+sel+errSex del sexo). La guía de retiro
    // por sexo no existe en Colombia ⇒ sin columna guía (serie Guía = NULL en el chart).
    public decimal? RetiroPctHembras { get; set; }
    public decimal? RetiroPctMachos { get; set; }

    // TK-2026-000022: el RESTO de los parámetros por sexo, para que la TABLA de indicadores
    // pueda decir de qué sexo es cada número. Las columnas mixtas de arriba siguen intactas,
    // pero ojo: PesoCierre y UnifReal son el promedio aritmético simple de los dos sexos —un
    // valor que no le corresponde a ninguna ave real cuando hembra y macho pesan distinto—.
    // Mismo criterio de NULL que las series de arriba (sexo sin saldo / sin pesaje / sin guía).
    public decimal? AvesInicioHembras { get; set; }
    public decimal? AvesFinHembras { get; set; }
    public decimal? AvesInicioMachos { get; set; }
    public decimal? AvesFinMachos { get; set; }
    public decimal? ConsumoTotalSemanaHembras { get; set; }
    public decimal? ConsumoTotalSemanaMachos { get; set; }
    public decimal? UnifHembras { get; set; }
    public decimal? UnifMachos { get; set; }
    public decimal? GananciaHembras { get; set; }
    public decimal? GananciaMachos { get; set; }
    public decimal? DifPesoPctHembras { get; set; }
    public decimal? DifPesoPctMachos { get; set; }
    public decimal? SeleccionPctHembras { get; set; }
    public decimal? SeleccionPctMachos { get; set; }
    public decimal? ErrorSexajePctHembras { get; set; }
    public decimal? ErrorSexajePctMachos { get; set; }
}
