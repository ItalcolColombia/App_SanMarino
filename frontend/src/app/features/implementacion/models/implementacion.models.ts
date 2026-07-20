// src/app/features/implementacion/models/implementacion.models.ts
// Espejo de los DTOs del backend (ImplementacionController / ImplementacionDtos.cs).

export type EstadoPlan = 'borrador' | 'en_progreso' | 'completado' | 'cancelado';
export type EstadoTarea = 'pendiente' | 'completada' | 'confirmada';

export interface ImplementacionPlanDto {
  id: number;
  companyId: number;
  paisId: number | null;
  nombre: string;
  descripcion: string | null;
  fechaInicio: string | null;
  fechaFin: string | null;
  estado: EstadoPlan;
  totalTareas: number;
  tareasCompletadas: number;
  tareasConfirmadas: number;
  porcentajeAvance: number;
  porcentajeConfirmado: number;
  createdAt: string;
}

export interface ImplementacionPlanCreateRequest {
  nombre: string;
  descripcion: string | null;
  fechaInicio: string | null;
  fechaFin: string | null;
  usarPlantilla: boolean;
}

export interface ImplementacionPlanUpdateRequest {
  nombre: string;
  descripcion: string | null;
  fechaInicio: string | null;
  fechaFin: string | null;
  /** Solo "cancelado" es manual; otro valor reactiva y el estado real se rederiva. */
  estado: string | null;
}

export interface ImplementacionTareaDto {
  id: number;
  planId: number;
  categoria: string;
  titulo: string;
  descripcion: string | null;
  orden: number;
  fechaProgramada: string | null;
  roleId: number | null;
  roleNombre: string | null;
  asignadoUserId: string | null;
  asignadoNombre: string | null;
  estado: EstadoTarea;
  vencida: boolean;
  fechaCompletada: string | null;
  completadaPorNombre: string | null;
  fechaConfirmada: string | null;
  confirmadaPorNombre: string | null;
  observaciones: string | null;
}

export interface ImplementacionPlanDetalleDto {
  plan: ImplementacionPlanDto;
  tareas: ImplementacionTareaDto[];
}

export interface ImplementacionTareaRequest {
  categoria: string;
  titulo: string;
  descripcion: string | null;
  orden: number | null;
  fechaProgramada: string | null;
  roleId: number | null;
  asignadoUserId: string | null;
}

export interface ImplementacionConfirmarRequest {
  observaciones: string | null;
}

export interface ImplementacionMiTareaDto {
  id: number;
  planId: number;
  planNombre: string;
  categoria: string;
  titulo: string;
  descripcion: string | null;
  fechaProgramada: string | null;
  estado: EstadoTarea;
  vencida: boolean;
  fechaCompletada: string | null;
  completadaPorNombre: string | null;
  fechaConfirmada: string | null;
  observaciones: string | null;
}

export interface ImplementacionUsuarioAsignableDto {
  id: string;
  nombre: string;
  cedula: string;
}

export interface ImplementacionRolAsignableDto {
  id: number;
  nombre: string;
}

export const ESTADO_PLAN_LABEL: Record<EstadoPlan, string> = {
  borrador: 'Borrador',
  en_progreso: 'En progreso',
  completado: 'Completado',
  cancelado: 'Cancelado',
};

export const ESTADO_TAREA_LABEL: Record<EstadoTarea, string> = {
  pendiente: 'Pendiente',
  completada: 'Completada',
  confirmada: 'Confirmada',
};
