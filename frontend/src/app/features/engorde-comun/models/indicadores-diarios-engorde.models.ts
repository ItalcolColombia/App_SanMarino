/** Fila de indicadores diarios — pollo engorde (registro vs guía genética Ecuador mixto). */
export interface IndicadorDiarioFilaEngorde {
  fechaYmd: string;
  dia: number;
  /** Saldo al inicio del día (antes de mort. y selección del día). Usado para g/ave y % mort. */
  avesInicioDia: number;
  /** Saldo vivo al final del día: avesEncasetadas − Σ(mort + sel + errSexaje + despachos) hasta este día. */
  avesFinDia: number;
  /** Siempre 0 para engorde mixto sin desglose por sexo en ficha. */
  avesHembrasInicioDia: number;
  avesMachosInicioDia: number;
  /** true cuando no hay desglose H/M: solo hay cálculo mixto (total kg ÷ total aves). */
  mixtoSinDesgloseSexo: boolean;

  pesoRealG: number;
  pesoTablaG: number;
  /** Peso promedio del día (último registro), por sexo, para CA. */
  pesoRealGA: number;
  pesoRealGB: number;

  gananciaDiariaRealG: number | null;
  gananciaDiariaTablaG: number;

  /** Alimento diario (g/ave): A = hembras, B = machos. null cuando no hay desglose. */
  consumoDiarioRealGA: number | null;
  consumoDiarioRealGB: number | null;
  /** Mixto: total kg del día × 1000 ÷ total aves al inicio. */
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

export type ComparativoTagEngorde = 'ok' | 'warn' | 'bad' | 'na';
