// src/app/features/lesiones/models/lesion.models.ts

/** Origen del registro de lesión: indica desde qué módulo de seguimiento se generó. */
export type ModuloOrigenLesion = 'REPRODUCTORA' | 'APOYO' | 'ENGORDE';

/**
 * DTO devuelto por el backend al consultar lesiones.
 * Compatible con LesionDto del API .NET.
 */
export interface LesionDto {
  id: number;
  clienteId: number | null;
  farmId: number;
  galponId: string | null;
  loteId: number | null;
  loteReproductoraId: string | null;
  edadDias: number | null;
  avesMacho: number | null;
  avesHembra: number | null;
  avesMixtas: number | null;
  tipoLesion: string;
  observaciones: string | null;
  fechaRegistro: string;
  moduloOrigen: ModuloOrigenLesion;
  status: string;
  companyId: number;
  createdByUserId: number;
  createdAt: string;
  updatedByUserId?: number | null;
  updatedAt?: string | null;
  deletedAt?: string | null;
}

/** Payload para crear una nueva lesión. */
export interface CreateLesionRequest {
  clienteId: number | null;
  farmId: number;
  galponId: string | null;
  loteId: number | null;
  loteReproductoraId: string | null;
  edadDias: number | null;
  avesMacho: number | null;
  avesHembra: number | null;
  avesMixtas: number | null;
  tipoLesion: string;
  observaciones: string | null;
  fechaRegistro: string;
  moduloOrigen: ModuloOrigenLesion;
}

/** Payload para actualizar una lesión existente. */
export interface UpdateLesionRequest extends CreateLesionRequest {
  status: string;
}

/** Filtros para búsqueda paginada de lesiones. */
export interface LesionSearchRequest {
  moduloOrigen?: ModuloOrigenLesion;
  farmId?: number;
  loteId?: number;
  clienteId?: number;
  galponId?: string;
  loteReproductoraId?: string;
  tipoLesion?: string;
  fechaDesde?: string;
  fechaHasta?: string;
  soloActivos?: boolean;
  sortBy?: string;
  sortDesc?: boolean;
  page?: number;
  pageSize?: number;
}

/** Resumen agregado por tipo de lesión devuelto por el backend. */
export interface LesionResumenDto {
  tipoLesion: string;
  totalRegistros: number;
  totalAvesMacho: number;
  totalAvesHembra: number;
  totalAvesMixtas: number;
  totalAves: number;
}

/** Estructura genérica de paginación devuelta por el API. */
export interface PagedResult<T> {
  page: number;
  pageSize: number;
  total: number;
  items: T[];
}

/**
 * Catálogo inicial de tipos de lesión. Ajustable según necesidades clínicas.
 * Mantener en sync con cualquier validación que el backend aplique.
 */
export const TIPOS_LESION = [
  'Hongo',
  'Bacteriana',
  'Parasitaria',
  'Viral',
  'Mecánica',
  'Otro'
] as const;

export type TipoLesion = typeof TIPOS_LESION[number];
