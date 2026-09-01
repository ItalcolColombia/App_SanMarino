// src/app/features/reportes-tecnicos/models/reporte-tecnico-guia.model.ts
/**
 * Disponibilidad de las columnas de comparación contra la GUÍA GENÉTICA.
 *
 * Una empresa puede tener su guía en la tabla dedicada (`guia_genetica_santa_reyes`), que es un
 * modelo simple: una curva por semana con % de producción, retiro acumulado de hembras y consumo
 * g/ave/día. El backend la proyecta a la forma de la guía compartida para no tocar a los
 * consumidores, y todo lo que esa tabla no tiene llega en `null`.
 *
 * Antes el reporte pintaba igual las ~17 columnas GUÍA, así que esas empresas veían una pared de
 * celdas vacías que parecen un error del reporte. Con esto el backend informa qué métricas tienen
 * dato y la tabla pinta sólo esas.
 *
 * El backend lo calcula en `GuiaMetricasDisponiblesCalculos` y **con guía compartida informa todas
 * disponibles sin inspeccionar las filas**, de modo que las empresas sin guía propia siguen viendo
 * exactamente lo de siempre.
 */
export interface GuiaMetricasDisponibles {
  /** % de producción / postura de la guía. */
  prodPorcentaje: boolean;
  /** Peso del huevo de la guía (g). */
  pesoHuevo: boolean;
  /** Huevo total ave alojada (H.TOTAL A/A) de la guía. */
  hTotalAa: boolean;
  uniformidad: boolean;
  pesoH: boolean;
  pesoM: boolean;
  /** Mortalidad SEMANAL de la guía (no la acumulada). */
  mortSemH: boolean;
  mortSemM: boolean;
  /** Retiro ACUMULADO de la guía. */
  retiroAcH: boolean;
  retiroAcM: boolean;
  /** Consumo ACUMULADO de la guía (g/ave). */
  consAcH: boolean;
  consAcM: boolean;
  /** Consumo diario de la guía (g/ave/día). */
  grAveDiaH: boolean;
  grAveDiaM: boolean;
}

/**
 * Todas disponibles. Es el valor por defecto cuando el reporte no informa disponibilidad —un DTO
 * de un backend anterior, por ejemplo—: ante la duda se pinta TODO, que es el comportamiento
 * histórico. Ocultar de más sería peor que mostrar una celda vacía.
 */
export const GUIA_TODAS_DISPONIBLES: Readonly<GuiaMetricasDisponibles> = Object.freeze({
  prodPorcentaje: true,
  pesoHuevo: true,
  hTotalAa: true,
  uniformidad: true,
  pesoH: true,
  pesoM: true,
  mortSemH: true,
  mortSemM: true,
  retiroAcH: true,
  retiroAcM: true,
  consAcH: true,
  consAcM: true,
  grAveDiaH: true,
  grAveDiaM: true
});
