// src/app/features/implementacion/funciones/estado-tarea.funcion.ts
// PURA: presentación de estados y tipos (labels + colores por token CSS central, sin hex hardcodeado).
// Los objetos devueltos son constantes compartidas → referencias estables para el template (sin NG0103).
import {
  ESTADO_FIRMA_LABEL,
  ESTADO_PLAN_LABEL,
  ESTADO_TAREA_LABEL,
  EstadoFirma,
  EstadoPlan,
  EstadoTarea,
  TIPO_PLAN_LABEL,
  TipoPlan,
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

const ESTILO_FIRMA: Record<EstadoFirma, EstiloEstado> = {
  pendiente: { label: ESTADO_FIRMA_LABEL.pendiente, fg: 'var(--ital-muted)',       bg: 'var(--ital-cream)' },
  firmada:   { label: ESTADO_FIRMA_LABEL.firmada,   fg: 'var(--success)',          bg: SUCCESS_SOFT },
  rechazada: { label: ESTADO_FIRMA_LABEL.rechazada, fg: 'var(--danger)',           bg: DANGER_SOFT },
};

const ESTILO_TIPO: Record<TipoPlan, EstiloEstado> = {
  implementacion: { label: TIPO_PLAN_LABEL.implementacion, fg: 'var(--ital-orange-dark)', bg: 'var(--ital-orange-100)' },
  capacitacion:   { label: TIPO_PLAN_LABEL.capacitacion,   fg: 'var(--ital-orange)',      bg: 'var(--ital-orange-50)' },
  mixto:          { label: TIPO_PLAN_LABEL.mixto,          fg: 'var(--ital-text)',        bg: 'var(--ital-green-50)' },
};

export function estiloEstadoTarea(estado: EstadoTarea): EstiloEstado {
  return ESTILO_TAREA[estado] ?? ESTILO_TAREA.pendiente;
}

export function estiloEstadoPlan(estado: EstadoPlan): EstiloEstado {
  return ESTILO_PLAN[estado] ?? ESTILO_PLAN.borrador;
}

export function estiloEstadoFirma(estado: EstadoFirma): EstiloEstado {
  return ESTILO_FIRMA[estado] ?? ESTILO_FIRMA.pendiente;
}

export function estiloTipoPlan(tipo: TipoPlan): EstiloEstado {
  return ESTILO_TIPO[tipo] ?? ESTILO_TIPO.implementacion;
}
