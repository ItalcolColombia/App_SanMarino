// frontend/src/app/features/indicador-ecuador/models/auditoria-liquidacion.model.ts
// Tipos del resultado de fn_auditoria_liquidacion_engorde (lo arma la BD; el front solo pinta).

export interface AuditScope {
  companyId: number;
  granjaId: number;
  granjaNombre: string;
  nucleoId: string | null;
  loteCodigo: string | null;
  lotes: number[];
}

export interface AuditResumen {
  /** false = el Excel subido está incompleto o no es el correcto (valores clave vacíos/cero). */
  excelValido: boolean;
  indicadoresComparados: number;
  fallasDato: number;
  difDefinicion: number;
  hallazgos: number;
}

export interface AuditReconRow {
  clave: string;
  label: string;
  unidad: string;
  clase: 'dato' | 'definicion';
  sistema: number | null;
  excel: number | null;
  tieneExcel: boolean;
  diferencia: number | null;
  difPct: number | null;
  cuadra: boolean;
}

export interface AuditHallazgo {
  codigo: string;
  severidad: 'critico' | 'alerta' | 'info';
  tipo: string;
  titulo: string;
  descripcion: string;
  impactoKgEstimado?: number;
  pesoAvePromedioResto?: number;
  registros?: any[];
}

export interface AuditSimIndicador {
  label: string;
  clave: string;
  sistemaActual: number | null;
  corregido: number | null;
  excel: number | null;
  cuadra: boolean;
}

export interface AuditSimulacion {
  supuesto: string;
  gapKg: number | null;
  atribuibleASinPeso: boolean;
  pesoAveImplicito: number | null;
  pesoAvePromedioResto: number | null;
  impactoSiPesaranComoResto: number | null;
  nota: string;
  indicadores: AuditSimIndicador[];
}

export interface AuditoriaLiquidacionResultado {
  scope: AuditScope;
  resumen: AuditResumen;
  reconciliacion: AuditReconRow[];
  hallazgos: AuditHallazgo[];
  simulacion: AuditSimulacion;
  generadoEn: string;
  error?: string;
}

/** Resultado de aplicar la corrección (fn_aplicar_correccion_despachos_sin_peso). */
export interface AplicarCorreccionResultado {
  ok: boolean;
  error?: string;
  kgTotal?: number;
  avesTotales?: number;
  movimientos?: number;
  aplicados?: Array<{ id: number; aves: number; pesoAsignado: number }>;
}

/** Alcance que el front pasa al endpoint (granja obligatoria; núcleo/código opcionales). */
export interface AuditoriaScopeInput {
  granjaId: number;
  nucleoId: string | null;
  loteCodigo: string | null;
}

/**
 * Etiquetas (en orden) para la plantilla descargable. DEBEN coincidir con las que reconoce el parser
 * del back (`AuditoriaLiquidacionExcelParser.LabelMap`). Si se cambia una etiqueta acá, actualizar allá.
 */
export const PLANTILLA_INDICADORES: string[] = [
  'Aves encasetadas',
  'Aves Sacrificadas',
  'Mortalidad (unidades)',
  'Mortalidad (%)',
  'Merma (unidades)',
  'Merma (%)',
  'Total de aves despachadas',
  'Ajuste en Aves',
  'Porcentaje de ajuste',
  'Supervivencia',
  'Consumo total',
  'Producción kilo en pie',
  'Merma (Kilos)',
  'Total kilos despachados a cliente',
  'Consumo ave',
  'Peso promedio',
  'Conversión',
  'Eficiencia Americana',
  'Días de engorde',
  'Productividad',
  'Edad Ponderada'
];
