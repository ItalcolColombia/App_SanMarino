// features/migraciones-masivas/models/migracion.model.ts
// Tipos compartidos del módulo de Migraciones Masivas (espejo de los DTOs del backend).

export type TipoMigracionCodigo =
  | 'Granjas'
  | 'Nucleos'
  | 'Galpones'
  | 'SeguimientoLevante'
  | 'SeguimientoProduccion'
  | 'Ventas'
  | 'MovimientoAves'
  | 'MovimientoHuevos'
  // Línea Engorde
  | 'LotesPolloEngorde'
  | 'SeguimientoPolloEngorde'
  | 'SeguimientoReproductoraEngorde'
  | 'VentaPolloEngorde';

export interface TipoMigracionInfo {
  codigo: TipoMigracionCodigo;
  nombre: string;
  descripcion: string;
  requiereLote: boolean;
  fase: string;
  disponible: boolean;
}

/** "Error" bloquea el all-or-nothing; "Advertencia" es informativa y no bloquea la importación. */
export type MigracionSeveridad = 'Error' | 'Advertencia';

export interface MigracionError {
  fila: number;
  columna: string;
  valor?: string | null;
  mensaje: string;
  severidad: MigracionSeveridad;
}

export interface MigracionResult {
  tipo: string;
  exito: boolean;
  filasTotales: number;
  filasProcesadas: number;
  filasError: number;
  /** Filas no procesadas por idempotencia (ya existían en BD); no cuentan como error. */
  filasOmitidas: number;
  // Validado | Procesado | ProcesadoParcial | ConErrores | Fallido
  estado: string;
  fueDryRun: boolean;
  errores: MigracionError[];
  /** Duración total de la corrida, en milisegundos. */
  duracionMs: number;
  /** Total real de errores/advertencias detectados, antes del cap que trae `errores` (máx. 300). */
  totalErrores: number;
}

export interface LoteElegible {
  loteId: number;
  loteNombre: string;
  granjaId: number;
  nucleoId?: string | null;
  galponId?: string | null;
  fase: string;
  estado?: string | null;
}

/** Lote reproductora de un lote engorde (endpoint `/reproductoras`; selector opcional). */
export interface ReproductoraElegible {
  id: number;
  reproductoraId: string;
  codigo?: string | null;
  nombre: string;
  fechaEncasetamiento?: string | null;
  /** Días de seguimiento ya registrados / confirmados (máx 7). */
  cargados: number;
  confirmados: number;
}

export interface MigracionHistorial {
  id: number;
  tipo: string;
  nombreArchivo: string;
  filasTotales: number;
  filasProcesadas: number;
  filasError: number;
  estado: string;
  fechaProceso: string;
  usuarioId: number;
  filasOmitidas: number;
  duracionMs: number | null;
  fueDryRun: boolean;
  tieneErrores: boolean;
}

/** Página del historial de auditoría de migraciones (endpoint `/historial` paginado). */
export interface MigracionHistorialPaged {
  items: MigracionHistorial[];
  total: number;
  page: number;
  pageSize: number;
}

/** Selección jerárquica que acompaña a la validación/importación (la empresa va por header). */
export interface MigracionContexto {
  granjaId?: number | null;
  nucleoId?: string | null;
  galponId?: string | null;
  loteId?: number | null;
  /** Solo Seguimiento Reproductora Engorde: reproductora puntual elegida (opcional). */
  reproductoraId?: number | null;
}
