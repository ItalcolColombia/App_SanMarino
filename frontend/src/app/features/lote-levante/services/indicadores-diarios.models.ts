/** Fila de indicadores diarios (registro vs guía genética Ecuador mixto). */
export interface IndicadorDiarioFila {
  fechaYmd: string;
  dia: number;
  /** Total aves al inicio del día (antes de mort. y selección del día). */
  avesInicioDia: number;
  /** Aves hembras y machos al inicio del día (para g/ave A y B). */
  avesHembrasInicioDia: number;
  avesMachosInicioDia: number;
  /** Si el lote no tiene H/M en ficha, solo hay cálculo mixto (total kg ÷ total aves). */
  mixtoSinDesgloseSexo: boolean;

  pesoRealG: number;
  pesoTablaG: number;
  /** Peso promedio del día (último registro), por sexo, para CA. */
  pesoRealGA: number;
  pesoRealGB: number;

  gananciaDiariaRealG: number | null;
  gananciaDiariaTablaG: number;

  /** Alimento diario (g/ave): A = hembras, B = machos (kg del día ÷ aves al inicio por sexo). */
  consumoDiarioRealGA: number | null;
  consumoDiarioRealGB: number | null;
  /** Mixto: total kg del día × 1000 ÷ total aves al inicio (misma base que antes). */
  consumoDiarioRealG: number;
  consumoDiarioTablaG: number;

  /** Acumulado (g/ave) = suma día a día del g/ave de cada sexo / mixto. */
  alimentoAcumRealGA: number | null;
  alimentoAcumRealGB: number | null;
  alimentoAcumRealG: number;
  alimentoAcumTablaG: number;

  /** CA = alimento acumulado (g/ave) ÷ peso (g) del mismo sexo o mixto. */
  caRealA: number | null;
  caRealB: number | null;
  caReal: number | null;
  caTabla: number;

  mortSelRealPct: number;
  mortSelTablaPct: number;
  difPesoVsTablaPct: number;
  /** % acumulado de pérdidas (mort.+sel.) desde el inicio del lote hasta el fin del día. */
  mortAcumPct: number;
}

export type ComparativoTag = 'ok' | 'warn' | 'bad' | 'na';
