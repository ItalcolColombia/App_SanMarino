// DTOs compartidos multi-país del flujo "Cuadrar saldos" de pollo engorde.
// Fuente unica (antes duplicados en los servicios de seguimiento de engorde por pais).

export interface FilaExcelCuadrarSaldosDto {
  fecha: string;
  saldoAlimentoKg: number | null;
  ingresoAlimentoKg: number | null;
  trasladoEntradaKg: number | null;
  trasladoSalidaKg: number | null;
  documento: string | null;
  consumoKg: number | null;
  consumoAcumuladoKg: number | null;
}

export interface InconsistenciaCuadrarSaldosDto {
  fecha: string;
  tipo: string;
  descripcion: string;
  valorExcel: number | null;
  valorSistema: number | null;
  historicoId: number | null;
  documentoExcel: string | null;
  documentoSistema: string | null;
}

export interface AccionCorreccionCuadrarSaldosDto {
  tipoAccion: string;
  historicoId: number | null;
  nuevaFecha: string | null;
  fechaInsertar: string | null;
  tipoEvento: string | null;
  cantidadKg: number | null;
  documento: string | null;
  descripcion: string | null;
}

export interface CuadrarSaldosValidarResponseDto {
  loteId: number;
  filasExcel: number;
  inconsistenciasCount: number;
  inconsistencias: InconsistenciaCuadrarSaldosDto[];
  accionesSugeridas: AccionCorreccionCuadrarSaldosDto[];
}

export interface CuadrarSaldosAplicarResponseDto {
  loteId: number;
  fechasAjustadas: number;
  registrosAnulados: number;
  registrosInsertados: number;
  metadataSegLimpiados: number;
  mensaje: string;
}
