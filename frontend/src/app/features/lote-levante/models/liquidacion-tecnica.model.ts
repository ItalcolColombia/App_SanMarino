/**
 * Tipos compartidos de la liquidación técnica del módulo lote-levante.
 *
 * Se extraen del componente `liquidacion-tecnica` para poder reutilizarlos desde `funciones/`
 * (construcción de la tabla de indicadores y del cálculo de cumplimiento) sin imports circulares.
 */

/** Valores esperados de la guía genética que consume la tabla comparativa de indicadores. */
export interface GuiaValoresEsperados {
  pesoEsperadoHembrasGuia: number;
  pesoEsperadoMachosGuia: number;
  mortalidadEsperadaHembrasGuia: number;
  mortalidadEsperadaMachosGuia: number;
  uniformidadEsperadaHembrasGuia: number;
  uniformidadEsperadaMachosGuia: number;
}

/** Entradas (ya calculadas) para el cálculo de cumplimiento general vs. la guía genética. */
export interface CumplimientoGeneralInput {
  pesoReal: number;
  pesoEsperadoGuia: number;
  consumoReal: number;
  consumoEsperadoGuia: number;
  mortalidadReal: number;
  mortalidadEsperadaGuia: number;
  conversionReal: number;
  conversionEsperadaGuia: number;
}

/** Resultado del cálculo de cumplimiento general (se asigna al estado del componente). */
export interface CumplimientoGeneralResult {
  parametrosOptimos: number;
  porcentajeCumplimientoGeneral: number;
}
