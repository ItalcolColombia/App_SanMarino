// src/app/features/vacunacion/models/vacunacion.model.ts
// Tipos alineados 1:1 con los DTOs de backend/src/ZooSanMarino.Application/DTOs/Vacunacion/*.

export type LineaProductiva = 'Levante' | 'Produccion' | 'Engorde';
export type UnidadObjetivo = 'Semana' | 'Dia' | 'Fecha';
export type EstadoAplicacion = 'Pendiente' | 'Aplicado' | 'AplicadoTardio' | 'AplicadoAdelantado' | 'NoAplicado';

export interface FarmDtoLite {
  id: number;
  companyId: number;
  name: string;
  [key: string]: unknown;
}

export interface VacunacionRegistroAplicacionDto {
  id: number;
  estado: EstadoAplicacion;
  fechaAplicacion: string | null;
  diasDesviacion: number | null;
  incumplido: boolean;
  motivoDescripcion: string | null;
  usuarioRegistraId: number;
  usuarioRegistraNombre: string | null;
  aplicadoPorUserId: number | null;
  aplicadoPorUserNombre: string | null;
  aplicadoPorNombreLibre: string | null;
}

export interface VacunacionCronogramaItemDto {
  id: number;
  lineaProductiva: LineaProductiva;
  loteId: number;
  loteNombre: string;
  granjaId: number;
  granjaNombre: string | null;
  nucleoId: string | null;
  galponId: string | null;
  itemInventarioId: number;
  itemInventarioNombre: string;
  unidadObjetivo: UnidadObjetivo;
  valorObjetivo: number | null;
  fechaObjetivo: string | null;
  rangoDiasAntes: number;
  rangoDiasDespues: number;
  fechaInicioFranja: string;
  fechaFinFranja: string;
  orden: number;
  activo: boolean;
  notas: string | null;
  registro: VacunacionRegistroAplicacionDto | null;
}

export interface VacunacionCronogramaItemCreateRequest {
  lineaProductiva: LineaProductiva;
  loteId: number;
  itemInventarioId: number;
  unidadObjetivo: UnidadObjetivo;
  valorObjetivo: number | null;
  fechaObjetivo: string | null;
  rangoDiasAntes: number;
  rangoDiasDespues: number;
  orden?: number;
  notas?: string | null;
}

export interface VacunacionCronogramaItemUpdateRequest {
  itemInventarioId: number;
  unidadObjetivo: UnidadObjetivo;
  valorObjetivo: number | null;
  fechaObjetivo: string | null;
  rangoDiasAntes: number;
  rangoDiasDespues: number;
  orden: number;
  activo: boolean;
  notas: string | null;
}

export interface VacunacionRegistrarAplicadoRequest {
  motivoDescripcion: string | null;
  aplicadoPorUserId: number | null;
  aplicadoPorNombreLibre: string | null;
}

export interface VacunacionRegistrarNoAplicadoRequest {
  motivoDescripcion: string;
}

export interface VacunacionLoteOpcionDto {
  loteId: number;
  lineaProductiva: LineaProductiva;
  loteNombre: string;
  granjaId: number;
  nucleoId: string | null;
  galponId: string | null;
  fechaEncaset: string | null;
  estadoCierre: string | null;
}

export interface VacunacionVacunaOpcionDto {
  id: number;
  codigo: string;
  nombre: string;
  unidad: string;
}

/** Usuario del sistema para "aplicado por" (id = UserId entero del sistema). */
export interface VacunacionUsuarioOpcionDto {
  id: number;
  nombre: string | null;
}

export interface VacunacionFilterDataDto {
  granjas: FarmDtoLite[];
  lotes: VacunacionLoteOpcionDto[];
  vacunas: VacunacionVacunaOpcionDto[];
  usuarios: VacunacionUsuarioOpcionDto[];
}

export interface VacunacionCumplimientoFiltroRequest {
  granjaIds?: number[] | null;
  nucleoId?: string | null;
  galponId?: string | null;
  loteIds?: number[] | null;
  lineaProductiva?: LineaProductiva | null;
  fechaDesde?: string | null;
  fechaHasta?: string | null;
}

export interface VacunacionCumplimientoLoteDto {
  loteId: number;
  loteNombre: string;
  lineaProductiva: LineaProductiva;
  granjaId: number;
  granjaNombre: string | null;
  totalProgramadas: number;
  totalATiempo: number;
  totalTardio1Semana: number;
  totalTardio2MasSemanas: number;
  totalNoAplicado: number;
  totalPendiente: number;
  porcentajeATiempo: number;
  porcentajeTardio: number;
  porcentajeNoAplicado: number;
  promedioDiasAtraso: number | null;
}

/** Detalle ítem a ítem del reporte de cumplimiento (POST /VacunacionReportes/detalle). */
export interface VacunacionCumplimientoDetalleDto {
  itemId: number;
  granjaId: number;
  granjaNombre: string | null;
  loteId: number;
  loteNombre: string | null;
  lineaProductiva: LineaProductiva;
  nucleoId: string | null;
  galponId: string | null;
  vacunaNombre: string;
  unidadObjetivo: UnidadObjetivo;
  valorObjetivo: number | null;
  fechaObjetivoEfectiva: string | null;
  fechaInicioFranja: string | null;
  fechaFinFranja: string | null;
  estado: EstadoAplicacion;
  fechaAplicacion: string | null;
  diasDesviacion: number | null;
  incumplido: boolean;
  motivo: string | null;
  aplicadoPor: string | null;
  registradoPor: string | null;
  notas: string | null;
}

/** Etiqueta legible por línea, para selects y encabezados. */
export const LINEA_PRODUCTIVA_LABEL: Record<LineaProductiva, string> = {
  Levante: 'Levante (Postura)',
  Produccion: 'Producción (Postura)',
  Engorde: 'Engorde',
};

/** Unidad de objetivo por defecto según línea (Postura = semana, Engorde = día). */
export const UNIDAD_OBJETIVO_POR_LINEA: Record<LineaProductiva, UnidadObjetivo> = {
  Levante: 'Semana',
  Produccion: 'Semana',
  Engorde: 'Dia',
};
