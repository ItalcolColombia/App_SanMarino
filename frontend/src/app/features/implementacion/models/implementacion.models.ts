// src/app/features/implementacion/models/implementacion.models.ts
// Espejo de los DTOs del backend (ImplementacionController / ImplementacionDtos.cs).

export type EstadoPlan = 'borrador' | 'en_progreso' | 'completado' | 'cancelado';
export type EstadoTarea = 'pendiente' | 'completada' | 'confirmada';
export type TipoPlan = 'implementacion' | 'capacitacion' | 'mixto';
export type EstadoFirma = 'pendiente' | 'firmada' | 'rechazada';

export interface ImplementacionPlanDto {
  id: number;
  companyId: number;
  paisId: number | null;
  nombre: string;
  descripcion: string | null;
  tipo: TipoPlan;
  fechaInicio: string | null;
  fechaFin: string | null;
  estado: EstadoPlan;
  implementadorUserId: string | null;
  implementadorNombre: string | null;
  implementadorEmail: string | null;
  creadoPorUserGuid: string | null;
  creadoPorNombre: string | null;
  creadoPorEmail: string | null;
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
  tipo: TipoPlan;
  fechaInicio: string | null;
  fechaFin: string | null;
  /** null → el encargado queda el mismo creador. */
  implementadorUserId: string | null;
  usarPlantilla: boolean;
}

export interface ImplementacionPlanUpdateRequest {
  nombre: string;
  descripcion: string | null;
  tipo: TipoPlan;
  fechaInicio: string | null;
  fechaFin: string | null;
  /** null → el encargado vuelve al creador. */
  implementadorUserId: string | null;
  /** Solo "cancelado" es manual; otro valor reactiva y el estado real se rederiva. */
  estado: string | null;
}

/** Participante (asistente) de un ítem y su respuesta: firma digitada o novedad. */
export interface ImplementacionFirmaDto {
  id: number;
  tareaId: number;
  userId: string;
  nombre: string;
  cedula: string;
  email: string | null;
  estado: EstadoFirma;
  firmaTexto: string | null;
  nota: string | null;
  fechaRespuesta: string | null;
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
  firmas: ImplementacionFirmaDto[];
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

export interface ImplementacionParticipantesRequest {
  userIds: string[];
}

export interface ImplementacionFirmarRequest {
  firmaTexto: string;
  nota: string | null;
}

export interface ImplementacionRechazarRequest {
  motivo: string;
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

/** Punto donde YO soy participante: detalle de lo realizado + mi firma/novedad. */
export interface ImplementacionMiFirmaDto {
  firmaId: number;
  tareaId: number;
  planId: number;
  planNombre: string;
  planTipo: TipoPlan;
  categoria: string;
  tareaTitulo: string;
  tareaDescripcion: string | null;
  fechaProgramada: string | null;
  tareaEstado: EstadoTarea;
  fechaCompletada: string | null;
  completadaPorNombre: string | null;
  implementadorNombre: string | null;
  miEstado: EstadoFirma;
  firmaTexto: string | null;
  nota: string | null;
  fechaRespuesta: string | null;
}

export interface ImplementacionUsuarioAsignableDto {
  id: string;
  nombre: string;
  cedula: string;
  email: string | null;
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

export const TIPO_PLAN_LABEL: Record<TipoPlan, string> = {
  implementacion: 'Implementación',
  capacitacion: 'Capacitación',
  mixto: 'Implementación + capacitación',
};

export const ESTADO_FIRMA_LABEL: Record<EstadoFirma, string> = {
  pendiente: 'Pendiente de firma',
  firmada: 'Firmada',
  rechazada: 'Con novedad',
};
