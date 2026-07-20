// src/app/features/implementacion/funciones/estado-tarea.funcion.ts
// PURA: presentación de estados (labels + colores por token CSS central, sin hex hardcodeado).
import {
  ESTADO_PLAN_LABEL,
  ESTADO_TAREA_LABEL,
  EstadoPlan,
  EstadoTarea,
} from '../models/implementacion.models';

export interface EstiloEstado {
  label: string;
  /** Color de texto (token CSS var). */
  fg: string;
  /** Color de fondo suave (token CSS var). */
  bg: string;
}

// Fondos suaves derivados del token central (sin hex hardcodeado en el módulo).
const SUCCESS_SOFT = 'color-mix(in srgb, var(--success) 12%, transparent)';
const DANGER_SOFT = 'color-mix(in srgb, var(--danger) 10%, transparent)';

const ESTILO_TAREA: Record<EstadoTarea, EstiloEstado> = {
  pendiente:  { label: ESTADO_TAREA_LABEL.pendiente,  fg: 'var(--ital-muted)',       bg: 'var(--ital-cream)' },
  completada: { label: ESTADO_TAREA_LABEL.completada, fg: 'var(--ital-orange-dark)', bg: 'var(--ital-orange-100)' },
  confirmada: { label: ESTADO_TAREA_LABEL.confirmada, fg: 'var(--success)',          bg: SUCCESS_SOFT },
};

const ESTILO_PLAN: Record<EstadoPlan, EstiloEstado> = {
  borrador:    { label: ESTADO_PLAN_LABEL.borrador,    fg: 'var(--ital-muted)',       bg: 'var(--ital-cream)' },
  en_progreso: { label: ESTADO_PLAN_LABEL.en_progreso, fg: 'var(--ital-orange-dark)', bg: 'var(--ital-orange-100)' },
  completado:  { label: ESTADO_PLAN_LABEL.completado,  fg: 'var(--success)',          bg: SUCCESS_SOFT },
  cancelado:   { label: ESTADO_PLAN_LABEL.cancelado,   fg: 'var(--danger)',           bg: DANGER_SOFT },
};

export function estiloEstadoTarea(estado: EstadoTarea): EstiloEstado {
  return ESTILO_TAREA[estado] ?? ESTILO_TAREA.pendiente;
}

export function estiloEstadoPlan(estado: EstadoPlan): EstiloEstado {
  return ESTILO_PLAN[estado] ?? ESTILO_PLAN.borrador;
}
