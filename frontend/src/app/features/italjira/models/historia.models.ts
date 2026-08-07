// src/app/features/italjira/models/historia.models.ts
// Tipos de ItalJira: historias (épicas), backlog, tablero y roadmap.
// Alineados con ZooSanMarino.Application.DTOs.Tickets (el backend serializa en camelCase).

import type {
  EstadoTarea, PrioridadTicket, TicketTarea, TipoTarea,
} from '../../tickets/models/ticket-tarea.models';
import type { EstadoTicket, TipoTicket } from '../../tickets/models/ticket.models';

// Re-export de lo que ItalJira comparte con Tickets: quien importe desde acá no necesita saber
// que la tarea vive en el otro módulo (y evita que se dupliquen los tipos).
export type { EstadoTarea, PrioridadTicket, TicketTarea, TipoTarea };
export {
  COLUMNAS_TAREA, PRIORIDADES, PRIORIDAD_ACENTO, PRIORIDAD_BADGE, PRIORIDAD_LABEL,
  TAREA_ESTADO_DOT, TAREA_ESTADO_LABEL, TAREA_TIPO_COLOR, TAREA_TIPO_LABEL, TIPOS_TAREA,
} from '../../tickets/models/ticket-tarea.models';

/**
 * La historia usa el MISMO vocabulario de estados que la tarea (a propósito: el tablero tiene las
 * mismas columnas en los dos niveles). El alias existe para que el código de ItalJira se lea por
 * lo que significa, no por dónde vive el tipo.
 */
export type EstadoHistoria = EstadoTarea;

export interface Historia {
  id: number;
  codigo: string | null;
  titulo: string;
  descripcion: string | null;
  estado: EstadoHistoria;
  prioridad: PrioridadTicket;
  responsableUserGuid: string | null;
  responsableNombre: string | null;
  orden: number;
  horasEstimadas: number | null;
  /** Suma de las horas de sus tareas y de los casos que agrupa (la historia no registra horas). */
  horasRegistradas: number;
  fechaInicioPlan: string | null;
  fechaFinPlan: string | null;
  fechaInicioReal: string | null;
  fechaFinReal: string | null;
  etiquetas: string | null;
  /** 0..100 derivado de los trabajos vivos. Sin trabajos, sale del estado propio. */
  avancePorcentaje: number;
  trabajosTerminados: number;
  trabajosTotales: number;
  createdAt: string;
  createdByNombre: string | null;
  /** Extremos de la barra del roadmap: propios o derivados de los trabajos. */
  inicioEfectivo: string | null;
  finEfectivo: string | null;
}

/** Caso (ticket) visto desde ItalJira: lo mínimo para pintarlo dentro del árbol. */
export interface ItalJiraCaso {
  id: number;
  codigo: string | null;
  titulo: string;
  tipo: TipoTicket;
  estado: EstadoTicket;
  prioridad: PrioridadTicket;
  assignedToUserGuid: string | null;
  assignedToNombre: string | null;
  historiaId: number | null;
  horasEstimadas: number | null;
  horasRegistradas: number;
  fechaInicioPlan: string | null;
  fechaFinPlan: string | null;
  fechaLimite: string | null;
  createdAt: string;
  cantidadTareas: number;
}

export interface HistoriaDetalle {
  historia: Historia;
  tareas: TicketTarea[];
  casos: ItalJiraCaso[];
}

export interface ItalJiraResumen {
  historias: number;
  historiasEnCurso: number;
  historiasListas: number;
  tareas: number;
  tareasListas: number;
  casosSinHistoria: number;
  horasRegistradas: number;
  horasEstimadas: number | null;
}

export interface ItalJiraBacklog {
  historias: HistoriaDetalle[];
  /** Casos de usuarios que todavía no pertenecen a ninguna historia (la bandeja de entrada). */
  casosSinHistoria: ItalJiraCaso[];
  /** Tareas nacidas en desarrollo que todavía no se agruparon. */
  tareasSinHistoria: TicketTarea[];
  resumen: ItalJiraResumen;
}

export interface ItalJiraTableroColumna {
  estado: EstadoHistoria;
  historias: Historia[];
}

export interface ItalJiraTablero {
  columnas: ItalJiraTableroColumna[];
  resumen: ItalJiraResumen;
}

export interface ItalJiraRoadmapBarra {
  /** TAREA | CASO — decide el icono de la fila. */
  clase: 'TAREA' | 'CASO';
  id: number;
  codigo: string | null;
  titulo: string;
  estado: string;
  prioridad: PrioridadTicket;
  responsableNombre: string | null;
  fechaInicioPlan: string | null;
  fechaFinPlan: string | null;
}

export interface ItalJiraRoadmapItem {
  historia: Historia;
  trabajos: ItalJiraRoadmapBarra[];
}

export interface ItalJiraRoadmap {
  desde: string | null;
  hasta: string | null;
  items: ItalJiraRoadmapItem[];
}

// ───────────────────────── Requests ─────────────────────────

export interface CreateHistoriaRequest {
  titulo: string;
  descripcion?: string | null;
  estado?: EstadoHistoria | null;
  prioridad?: PrioridadTicket | null;
  responsableUserGuid?: string | null;
  horasEstimadas?: number | null;
  fechaInicioPlan?: string | null;
  fechaFinPlan?: string | null;
  etiquetas?: string | null;
}

export interface UpdateHistoriaRequest {
  titulo?: string | null;
  descripcion?: string | null;
  estado?: EstadoHistoria | null;
  prioridad?: PrioridadTicket | null;
  responsableUserGuid?: string | null;
  horasEstimadas?: number | null;
  fechaInicioPlan?: string | null;
  fechaFinPlan?: string | null;
  etiquetas?: string | null;
  /** Único modo de dejar la historia sin responsable (null significa «no tocar»). */
  quitarResponsable?: boolean;
}

export interface MoverHistoriaRequest {
  estado: EstadoHistoria;
  indice: number;
}

/** Mueve un trabajo a una historia; `null` lo devuelve a la bandeja «sin historia». */
export interface AsignarAHistoriaRequest {
  historiaId: number | null;
}

/** Alta de una tarea nacida en ItalJira (sin caso). */
export interface CreateTareaItalJiraRequest {
  titulo: string;
  descripcion?: string | null;
  tipo?: TipoTarea | null;
  estado?: EstadoTarea | null;
  prioridad?: PrioridadTicket | null;
  asignadoUserGuid?: string | null;
  parentTareaId?: number | null;
  horasEstimadas?: number | null;
  fechaInicioPlan?: string | null;
  fechaFinPlan?: string | null;
  etiquetas?: string | null;
  /** Historia a la que entra. Se ignora si viene `parentTareaId` (la subtarea hereda del padre). */
  historiaId?: number | null;
}

export interface ItalJiraFiltro {
  estado?: string;
  prioridad?: string;
  responsable?: string;
  texto?: string;
  incluirTerminadas?: boolean;
}
