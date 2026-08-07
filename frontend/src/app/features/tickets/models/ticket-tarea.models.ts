// src/app/features/tickets/models/ticket-tarea.models.ts
// Tipos del tablero tipo Jira: tareas de un caso, registro de tiempos, roadmap y línea de tiempo.
// Alineados con ZooSanMarino.Application.DTOs.Tickets (el backend serializa en camelCase).

import type { EstadoTicket, TicketListItem, TipoTicket } from './ticket.models';

// ───────────────────────── Prioridad ─────────────────────────

export type PrioridadTicket = 'BAJA' | 'MEDIA' | 'ALTA' | 'CRITICA';

export const PRIORIDADES: PrioridadTicket[] = ['CRITICA', 'ALTA', 'MEDIA', 'BAJA'];

export const PRIORIDAD_LABEL: Record<PrioridadTicket, string> = {
  BAJA: 'Baja',
  MEDIA: 'Media',
  ALTA: 'Alta',
  CRITICA: 'Crítica',
};

/** Clases Tailwind del chip de prioridad. */
export const PRIORIDAD_BADGE: Record<PrioridadTicket, string> = {
  BAJA:    'bg-slate-50 text-slate-600 ring-slate-200',
  MEDIA:   'bg-sky-50 text-sky-700 ring-sky-200',
  ALTA:    'bg-amber-50 text-amber-700 ring-amber-200',
  CRITICA: 'bg-rose-50 text-rose-700 ring-rose-200',
};

/** Barra vertical de acento en la tarjeta del tablero. */
export const PRIORIDAD_ACENTO: Record<PrioridadTicket, string> = {
  BAJA:    'bg-slate-300',
  MEDIA:   'bg-sky-400',
  ALTA:    'bg-amber-400',
  CRITICA: 'bg-rose-500',
};

// ───────────────────────── SLA ─────────────────────────

export type EstadoSla = 'SIN_SLA' | 'EN_TIEMPO' | 'POR_VENCER' | 'VENCIDO' | 'CUMPLIDO' | 'INCUMPLIDO';

export const SLA_LABEL: Record<EstadoSla, string> = {
  SIN_SLA:    'Sin compromiso',
  EN_TIEMPO:  'En tiempo',
  POR_VENCER: 'Por vencer',
  VENCIDO:    'Vencido',
  CUMPLIDO:   'Cumplido',
  INCUMPLIDO: 'Fuera de plazo',
};

export const SLA_BADGE: Record<EstadoSla, string> = {
  SIN_SLA:    'bg-slate-50 text-slate-500 ring-slate-200',
  EN_TIEMPO:  'bg-emerald-50 text-emerald-700 ring-emerald-200',
  POR_VENCER: 'bg-amber-50 text-amber-700 ring-amber-200',
  VENCIDO:    'bg-rose-50 text-rose-700 ring-rose-200',
  CUMPLIDO:   'bg-emerald-50 text-emerald-700 ring-emerald-200',
  INCUMPLIDO: 'bg-rose-50 text-rose-700 ring-rose-200',
};

// ───────────────────────── Tareas ─────────────────────────

export type TipoTarea = 'TAREA' | 'HISTORIA' | 'BUG' | 'SUBTAREA' | 'DOCUMENTACION' | 'MEJORA';

export type EstadoTarea =
  | 'BACKLOG' | 'ANALISIS' | 'DOCUMENTACION' | 'EN_CURSO' | 'EN_REVISION' | 'LISTO' | 'BLOQUEADA';

/** Columnas del tablero de tareas, en orden. */
export const COLUMNAS_TAREA: EstadoTarea[] =
  ['BACKLOG', 'ANALISIS', 'DOCUMENTACION', 'EN_CURSO', 'EN_REVISION', 'LISTO', 'BLOQUEADA'];

export const TAREA_ESTADO_LABEL: Record<EstadoTarea, string> = {
  BACKLOG:       'Backlog',
  ANALISIS:      'Análisis',
  DOCUMENTACION: 'Documentación',
  EN_CURSO:      'En curso',
  EN_REVISION:   'En revisión',
  LISTO:         'Listo',
  BLOQUEADA:     'Bloqueada',
};

export const TAREA_ESTADO_DOT: Record<EstadoTarea, string> = {
  BACKLOG:       'bg-slate-400',
  ANALISIS:      'bg-amber-400',
  DOCUMENTACION: 'bg-violet-400',
  EN_CURSO:      'bg-indigo-500',
  EN_REVISION:   'bg-sky-500',
  LISTO:         'bg-emerald-500',
  BLOQUEADA:     'bg-rose-500',
};

export const TIPOS_TAREA: { value: TipoTarea; label: string; icono: string }[] = [
  { value: 'TAREA',         label: 'Tarea',         icono: '✓' },
  { value: 'HISTORIA',      label: 'Historia',      icono: '★' },
  { value: 'BUG',           label: 'Bug',           icono: '!' },
  { value: 'SUBTAREA',      label: 'Subtarea',      icono: '↳' },
  { value: 'DOCUMENTACION', label: 'Documentación', icono: '¶' },
  { value: 'MEJORA',        label: 'Mejora',        icono: '↑' },
];

export const TAREA_TIPO_LABEL: Record<TipoTarea, string> = {
  TAREA: 'Tarea',
  HISTORIA: 'Historia',
  BUG: 'Bug',
  SUBTAREA: 'Subtarea',
  DOCUMENTACION: 'Documentación',
  MEJORA: 'Mejora',
};

/** Color del cuadrito de tipo (como el icono de issue de Jira). */
export const TAREA_TIPO_COLOR: Record<TipoTarea, string> = {
  TAREA:         'bg-sky-500',
  HISTORIA:      'bg-emerald-500',
  BUG:           'bg-rose-500',
  SUBTAREA:      'bg-slate-400',
  DOCUMENTACION: 'bg-violet-500',
  MEJORA:        'bg-amber-500',
};

export interface TicketTarea {
  id: number;
  ticketId: number;
  codigo: string | null;
  tipo: TipoTarea;
  estado: EstadoTarea;
  prioridad: PrioridadTicket;
  titulo: string;
  descripcion: string | null;
  asignadoUserGuid: string | null;
  asignadoNombre: string | null;
  parentTareaId: number | null;
  orden: number;
  horasEstimadas: number | null;
  horasRegistradas: number;
  fechaInicioPlan: string | null;
  fechaFinPlan: string | null;
  fechaInicioReal: string | null;
  fechaFinReal: string | null;
  etiquetas: string | null;
  createdAt: string;
  createdByNombre: string | null;
  cantidadSubtareas: number;
}

export interface CreateTicketTareaRequest {
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
}

export interface UpdateTicketTareaRequest {
  titulo?: string | null;
  descripcion?: string | null;
  tipo?: TipoTarea | null;
  estado?: EstadoTarea | null;
  prioridad?: PrioridadTicket | null;
  asignadoUserGuid?: string | null;
  horasEstimadas?: number | null;
  fechaInicioPlan?: string | null;
  fechaFinPlan?: string | null;
  etiquetas?: string | null;
  /** Único modo de dejar la tarea sin responsable (null significa «no tocar»). */
  quitarAsignado?: boolean;
}

export interface MoverTareaRequest {
  estado: EstadoTarea;
  indice: number;
}

// ───────────────────────── Registro de tiempo ─────────────────────────

export interface TicketTiempo {
  id: number;
  ticketId: number;
  tareaId: number | null;
  tareaTitulo: string | null;
  userId: number;
  userGuid: string | null;
  userNombre: string | null;
  fecha: string;
  horas: number;
  descripcion: string | null;
  createdAt: string;
}

export interface CreateTicketTiempoRequest {
  horas: number;
  fecha?: string | null;
  descripcion?: string | null;
  tareaId?: number | null;
}

export interface TicketTiempoPorPersona {
  userGuid: string | null;
  nombre: string | null;
  horas: number;
}

export interface TicketResumenTiempos {
  horasRegistradas: number;
  horasEstimadas: number | null;
  desvioHoras: number | null;
  porPersona: TicketTiempoPorPersona[];
}

// ───────────────────────── Gestión del caso ─────────────────────────

export interface CambiarPrioridadRequest { prioridad: PrioridadTicket; }
export interface CambiarAsignadoRequest { asignadoUserGuid: string; nota?: string | null; }
export interface MoverTicketRequest { estado: EstadoTicket; indice: number; nota?: string | null; }

export interface ActualizarPlanificacionRequest {
  fechaInicioPlan?: string | null;
  fechaFinPlan?: string | null;
  fechaLimite?: string | null;
  horasEstimadas?: number | null;
  limpiarFechaInicioPlan?: boolean;
  limpiarFechaFinPlan?: boolean;
  limpiarFechaLimite?: boolean;
  limpiarHorasEstimadas?: boolean;
}

// ───────────────────────── Tablero ─────────────────────────

/**
 * Filtros que comparten tablero, roadmap, panel de indicadores y reporte. Uno solo para las
 * cuatro vistas: si se desincronizan, el Excel deja de coincidir con lo que se ve en pantalla.
 */
export interface TicketTableroFiltro {
  anio?: number;
  tipo?: string;
  prioridad?: string;
  paisId?: number;
  companyId?: number;
  assignedToGuid?: string;
  texto?: string;
  maxPorColumna?: number;
  /** Selección múltiple de países; tiene prioridad sobre `paisId`. */
  paisIds?: number[];
  /** Selección múltiple de empresas; tiene prioridad sobre `companyId`. */
  companyIds?: number[];
  /** Rango de creación (`yyyy-MM-dd`). Si viene, manda sobre `anio`. */
  desde?: string;
  hasta?: string;
  estado?: string;
  estadoSla?: string;
}

export interface TicketTableroColumna {
  estado: EstadoTicket;
  label: string;
  total: number;
  items: TicketListItem[];
}

export interface TicketTableroResumen {
  total: number;
  abiertos: number;
  enCurso: number;
  solucionados: number;
  cerrados: number;
  vencidos: number;
  porVencer: number;
  sinAsignar: number;
  horasRegistradas: number;
}

export interface TicketTablero {
  columnas: TicketTableroColumna[];
  resumen: TicketTableroResumen;
}

// ───────────────────────── Roadmap ─────────────────────────

export interface TicketRoadmapTarea {
  id: number;
  codigo: string | null;
  titulo: string;
  tipo: TipoTarea;
  estado: EstadoTarea;
  asignadoNombre: string | null;
  fechaInicioPlan: string | null;
  fechaFinPlan: string | null;
}

export interface TicketRoadmapItem {
  id: number;
  codigo: string | null;
  titulo: string;
  tipo: TipoTicket;
  estado: EstadoTicket;
  prioridad: PrioridadTicket;
  assignedToNombre: string | null;
  fechaInicioPlan: string | null;
  fechaFinPlan: string | null;
  fechaLimite: string | null;
  createdAt: string;
  fechaSolucion: string | null;
  avanceTareas: number;
  estadoSla: EstadoSla;
  tareas: TicketRoadmapTarea[];
}

export interface TicketRoadmap {
  desde: string | null;
  hasta: string | null;
  items: TicketRoadmapItem[];
}

// ───────────────────────── Línea de tiempo ─────────────────────────

export type TipoEventoTimeline =
  | 'CREADO' | 'ASIGNADO' | 'APERTURA' | 'ESTADO' | 'COMENTARIO' | 'SISTEMA'
  | 'ADJUNTO' | 'TAREA' | 'TIEMPO' | 'SOLUCION' | 'CIERRE' | 'NOTIFICACION';

export interface TicketTimelineEvento {
  momento: string;
  tipo: TipoEventoTimeline;
  titulo: string;
  detalle: string | null;
  autor: string | null;
  estadoResultante: EstadoTicket | null;
  esInterna: boolean;
  referenciaId: number | null;
}

// ───────────────────────── Métricas ─────────────────────────

export interface TicketPermanenciaEstado {
  estado: EstadoTicket;
  horas: number;
}

export interface TicketMetricas {
  horasPrimeraRespuesta: number | null;
  horasResolucion: number;
  horasConfirmacionCierre: number | null;
  estadoSla: EstadoSla;
  horasParaVencer: number | null;
  avanceTareas: number;
  avanceFlujo: number;
  cantidadTareas: number;
  tareasListas: number;
  horasRegistradas: number;
  horasEstimadas: number | null;
  desvioHoras: number | null;
  permanenciaPorEstado: TicketPermanenciaEstado[];
}

// ───────────────────────── Panel de indicadores ─────────────────────────

export interface TicketResumenIndicadores {
  total: number;
  abiertos: number;
  enCurso: number;
  solucionados: number;
  cerrados: number;
  suspendidos: number;
  vencidos: number;
  porVencer: number;
  sinAsignar: number;
  tareasTotal: number;
  tareasListas: number;
  tareasPendientes: number;
  horasEstimadas: number;
  horasRegistradas: number;
  /** Horas promedio hasta que el equipo tomó el caso. Null si ninguno fue tomado. */
  promedioPrimeraRespuesta: number | null;
  promedioResolucion: number | null;
  promedioConfirmacionCierre: number | null;
  /** % de casos con compromiso que se cumplieron. Null si ninguno tiene fecha límite. */
  efectividad: number | null;
  porcentajeResueltos: number;
  avanceTareas: number;
  conCompromiso: number;
  compromisoCumplido: number;
}

/** Desglose de un agrupador con identidad: se usa igual para país y para empresa. */
export interface TicketIndicadorGrupo {
  id: number;
  nombre: string;
  total: number;
  abiertos: number;
  enCurso: number;
  resueltos: number;
  vencidos: number;
  horasRegistradas: number;
  avanceTareas: number;
  promedioResolucion: number | null;
  efectividad: number | null;
}

export interface TicketIndicadorCategoria {
  clave: string;
  label: string;
  total: number;
  resueltos: number;
  vencidos: number;
  promedioResolucion: number | null;
}

export interface TicketIndicadorResponsable {
  guid: string | null;
  nombre: string;
  asignados: number;
  resueltos: number;
  vencidos: number;
  horasRegistradas: number;
  tareasListas: number;
  promedioResolucion: number | null;
}

export interface TicketIndicadores {
  resumen: TicketResumenIndicadores;
  porPais: TicketIndicadorGrupo[];
  porEmpresa: TicketIndicadorGrupo[];
  porEstado: TicketIndicadorCategoria[];
  porTipo: TicketIndicadorCategoria[];
  porPrioridad: TicketIndicadorCategoria[];
  porResponsable: TicketIndicadorResponsable[];
}

// ───────────────────────── Reporte detallado ─────────────────────────

export interface TicketReporteCaso {
  id: number;
  codigo: string | null;
  pais: string | null;
  empresa: string | null;
  tipo: string;
  estado: string;
  prioridad: string;
  titulo: string;
  solicitante: string | null;
  solicitanteEmail: string | null;
  registradoPor: string | null;
  responsable: string | null;
  createdAt: string;
  primeraApertura: string | null;
  fechaSolucion: string | null;
  fechaCierre: string | null;
  fechaLimite: string | null;
  estadoSla: EstadoSla;
  horasPrimeraRespuesta: number | null;
  horasResolucion: number;
  fechaInicioPlan: string | null;
  fechaFinPlan: string | null;
  horasEstimadas: number | null;
  horasRegistradas: number;
  desvioHoras: number | null;
  tareasTotal: number;
  tareasListas: number;
  avanceTareas: number;
  solucionDescripcion: string | null;
}

export interface TicketReporteTarea {
  codigoCaso: string | null;
  tituloCaso: string | null;
  pais: string | null;
  codigo: string | null;
  tipo: string;
  estado: string;
  prioridad: string;
  titulo: string;
  responsable: string | null;
  horasEstimadas: number | null;
  horasRegistradas: number;
  fechaInicioPlan: string | null;
  fechaFinPlan: string | null;
  fechaInicioReal: string | null;
  fechaFinReal: string | null;
  createdAt: string;
}

export interface TicketReporteTiempo {
  codigoCaso: string | null;
  tituloCaso: string | null;
  pais: string | null;
  tarea: string | null;
  persona: string | null;
  fecha: string;
  horas: number;
  descripcion: string | null;
}

export interface TicketReporte {
  indicadores: TicketIndicadores;
  casos: TicketReporteCaso[];
  tareas: TicketReporteTarea[];
  tiempos: TicketReporteTiempo[];
  /** Filtros aplicados en texto, para el encabezado de cada hoja del Excel. */
  filtrosAplicados: string[];
}

// ───────────────────────── Solicitante delegado ─────────────────────────

export interface SolicitanteCandidato {
  guid: string;
  nombre: string;
  email: string | null;
  rol: string | null;
  empresa: string | null;
  cedula: string | null;
}
