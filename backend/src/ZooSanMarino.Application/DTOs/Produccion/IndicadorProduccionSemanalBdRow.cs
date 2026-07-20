namespace ZooSanMarino.Application.DTOs.Produccion;

/// <summary>
/// Fila cruda devuelta por fn_indicadores_produccion_postura (cálculo en la BD).
/// Nombres de propiedades = snake_case de la función, mapeados por EF SqlQueryRaw
/// (mismo patrón que IndicadorSemanalLevanteDto de C1). El servicio la convierte al
/// contrato público IndicadorProduccionSemanalDto (decimal) sin alterar valores.
/// double? refleja que la fn devuelve NULL cuando no hay dato/guía (== decimal? del DTO).
/// </summary>
public sealed class IndicadorProduccionSemanalBdRow
{
    public int Semana { get; set; }
    public DateTime FechaInicioSemana { get; set; }
    public DateTime FechaFinSemana { get; set; }
    public int TotalRegistros { get; set; }

    public int MortalidadHembras { get; set; }
    public int MortalidadMachos { get; set; }
    public double PorcentajeMortalidadHembras { get; set; }
    public double PorcentajeMortalidadMachos { get; set; }
    public double? MortalidadGuiaHembras { get; set; }
    public double? MortalidadGuiaMachos { get; set; }
    public double? DiferenciaMortalidadHembras { get; set; }
    public double? DiferenciaMortalidadMachos { get; set; }

    public int SeleccionHembras { get; set; }
    public double PorcentajeSeleccionHembras { get; set; }

    public double ConsumoKgHembras { get; set; }
    public double ConsumoKgMachos { get; set; }
    public double ConsumoTotalKg { get; set; }
    public double ConsumoPromedioDiarioKg { get; set; }
    public double? ConsumoGuiaHembras { get; set; }
    public double? ConsumoGuiaMachos { get; set; }
    public double? DiferenciaConsumoHembras { get; set; }
    public double? DiferenciaConsumoMachos { get; set; }

    public int HuevosTotales { get; set; }
    public int HuevosIncubables { get; set; }
    public double PromedioHuevosPorDia { get; set; }
    public double EficienciaProduccion { get; set; }
    public double? HuevosTotalesGuia { get; set; }
    public double? HuevosIncubablesGuia { get; set; }
    public double? PorcentajeProduccionGuia { get; set; }
    public double? DiferenciaHuevosTotales { get; set; }
    public double? DiferenciaHuevosIncubables { get; set; }
    public double? DiferenciaPorcentajeProduccion { get; set; }

    public double? PesoHuevoPromedio { get; set; }
    public double? PesoHuevoGuia { get; set; }
    public double? DiferenciaPesoHuevo { get; set; }

    public double? PesoPromedioHembras { get; set; }
    public double? PesoPromedioMachos { get; set; }
    public double? PesoGuiaHembras { get; set; }
    public double? PesoGuiaMachos { get; set; }
    public double? DiferenciaPesoHembras { get; set; }
    public double? DiferenciaPesoMachos { get; set; }

    public double? UniformidadPromedio { get; set; }
    public double? UniformidadGuia { get; set; }
    public double? DiferenciaUniformidad { get; set; }

    public double? CoeficienteVariacionPromedio { get; set; }

    public int HuevosLimpios { get; set; }
    public int HuevosTratados { get; set; }
    public int HuevosSucios { get; set; }
    public int HuevosDeformes { get; set; }
    public int HuevosBlancos { get; set; }
    public int HuevosDobleYema { get; set; }
    public int HuevosPiso { get; set; }
    public int HuevosPequenos { get; set; }
    public int HuevosRotos { get; set; }
    public int HuevosDesecho { get; set; }
    public int HuevosOtro { get; set; }

    public int AvesHembrasInicioSemana { get; set; }
    public int AvesMachosInicioSemana { get; set; }
    public int AvesHembrasFinSemana { get; set; }
    public int AvesMachosFinSemana { get; set; }

    public double HtaaReal { get; set; }
    public double HiaaReal { get; set; }

    // REQ-004: %Retiro REAL por sexo (mortalidad + selección). Mapean a las columnas de la fn
    // retiro_sem_h / retiro_sem_m / retiro_ac_h / retiro_ac_m (vía convención snake_case).
    public double RetiroSemH { get; set; }
    public double RetiroSemM { get; set; }
    public double RetiroAcH { get; set; }
    public double RetiroAcM { get; set; }

    // Verenice rev 6-jul-26: %Retiro acumulado de GUÍA por sexo (columna "% Retiro (Real vs Guía)"
    // del front que quedaba vacía). Mapea a retiro_ac_h_guia / retiro_ac_m_guia de la fn.
    public double? RetiroAcHGuia { get; set; }
    public double? RetiroAcMGuia { get; set; }
}
