/**
 * Modelos para gráficas de PRODUCTIVIDAD de pollo engorde (Panamá).
 * Solo datos reales del seguimiento diario (sin overlay de guía / estándar).
 */

/** Fila diaria de productividad (un punto por día de vida con registro). */
export interface ProductividadDiariaFila {
  /** Edad en días de vida. */
  edadDia: number;
  /** Peso promedio del ave del día (gramos, mixto). */
  gramos: number;
  /** Peso vivo en pie (quintales) = saldoAves × pesoPromAve_lb / 100. */
  qq: number;
  /** % mortalidad del día sobre saldo al inicio del día. */
  pctMortalidad: number;
  /** % selección del día sobre saldo al inicio del día. */
  pctSeleccion: number;
  /** % (mortalidad + selección) del día sobre saldo al inicio del día. */
  pctMortSel: number;
  /** Acumulado de muertes (cantidad) hasta este día. */
  mortalidadTotalAcum: number;
  /** Acumulado de selección (cantidad) hasta este día. */
  seleccionTotalAcum: number;
}

/** Fila semanal de productividad (un punto por semana). */
export interface ProductividadSemanalFila {
  /** Número de semana. */
  semana: number;
  /** Peso promedio del ave representativo de la semana (gramos, último día con peso). */
  grs: number;
  /** Peso vivo en pie (quintales) al final de la semana. */
  qq: number;
  /** Conversión alimenticia acumulada al final de la semana (consumoAcumKg / pesoVivoTotalKg). */
  ca: number;
  /** % mortalidad de la semana sobre saldo al inicio de la semana. */
  pctMortalidad: number;
  /** % selección de la semana sobre saldo al inicio de la semana. */
  pctSeleccion: number;
  /** % (mortalidad + selección) de la semana. */
  pctMortSel: number;
  /** Acumulado de muertes (cantidad) hasta el fin de la semana. */
  mortalidadTotalAcum: number;
  /** Acumulado de selección (cantidad) hasta el fin de la semana. */
  seleccionTotalAcum: number;
}

export interface ProductividadEngordeResult {
  diaria: ProductividadDiariaFila[];
  semanal: ProductividadSemanalFila[];
}
