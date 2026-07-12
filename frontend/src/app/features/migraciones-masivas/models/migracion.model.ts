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
  | 'VentaPolloEngorde';

export interface TipoMigracionInfo {
  codigo: TipoMigracionCodigo;
  nombre: string;
  descripcion: string;
  requiereLote: boolean;
  fase: string;
  disponible: boolean;
}

export interface MigracionError {
  fila: number;
  columna: string;
  valor?: string | null;
  mensaje: string;
}

export interface MigracionResult {
  tipo: string;
  exito: boolean;
  filasTotales: number;
  filasProcesadas: number;
  filasError: number;
  estado: string;
  fueDryRun: boolean;
  errores: MigracionError[];
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
}

/** Selección jerárquica que acompaña a la validación/importación (la empresa va por header). */
export interface MigracionContexto {
  granjaId?: number | null;
  nucleoId?: string | null;
  galponId?: string | null;
  loteId?: number | null;
}
