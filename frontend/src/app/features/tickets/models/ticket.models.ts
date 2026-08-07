// src/app/features/tickets/models/ticket.models.ts
// Tipos del módulo de tickets de soporte. Alineados con los DTOs del backend
// (ZooSanMarino.Application.DTOs.Tickets). El backend serializa en camelCase.

// Import de solo-tipos: se borra al compilar, así que el ciclo con ticket-tarea.models no existe
// en runtime (ese archivo sí importa tipos de este).
import type { EstadoSla, PrioridadTicket, TicketMetricas, TicketTarea } from './ticket-tarea.models';

export type TipoTicket = 'SOPORTE' | 'DESARROLLO' | 'REQUERIMIENTO' | 'DUDAS';

export type EstadoTicket =
  | 'ABIERTO'
  | 'EN_ANALISIS'
  | 'EN_DOCUMENTACION'
  | 'EN_IMPLEMENTACION'
  | 'EN_REVISION'
  | 'SOLUCIONADO'
  | 'CERRADO'
  | 'TRANSFERIDO'
  | 'SUSPENDIDO';

export type TipoAdjunto = 'ARCHIVO' | 'LINK';

/** Paginación genérica devuelta por el API (igual que PagedResult<T> del backend). */
export interface PagedResult<T> {
  page: number;
  pageSize: number;
  total: number;
  items: T[];
}

// ───────────────────────── Entrada ─────────────────────────

export interface TicketImagenInput {
  base64: string;
  fileName?: string | null;
  contentType?: string | null;
  sizeBytes?: number | null;
}

export interface CreateTicketRequest {
  titulo: string;
  tipo: TipoTicket;
  descripcion: string;
  /** Guid del resolutor — obligatorio (validado en backend). */
  assignedToUserGuid: string;
  imagenes?: TicketImagenInput[] | null;
  /** Guids de usuarios a notificar (copiados) — opcional. */
  notificarUserGuids?: string[];
  /** Usuario a nombre de quien se registra el caso. Solo lo acepta el backend con tickets.admin. */
  solicitanteUserGuid?: string | null;
  /** Prioridad inicial. Null ⇒ MEDIA. */
  prioridad?: PrioridadTicket | null;
}

export interface TransferirTicketRequest {
  nuevoAsignadoGuid: string;
  nota?: string | null;
}

export interface AddTicketImagenesRequest {
  imagenes: TicketImagenInput[];
}

export interface CambiarEstadoTicketRequest {
  estado: EstadoTicket;
  nota?: string | null;
  solucionDescripcion?: string | null;
}

export interface ConfirmarCierreRequest {
  nota?: string | null;
}

export interface AddTicketDocumentoRequest {
  base64: string;
  fileName?: string | null;
  contentType?: string | null;
  sizeBytes?: number | null;
}

export interface AddTicketLinkRequest {
  url: string;
  titulo?: string | null;
}

export interface TicketAdjunto {
  id: number;
  tipo: TipoAdjunto;
  fileName: string | null;
  contentType: string | null;
  sizeBytes: number | null;
  url: string | null;
  titulo: string | null;
  createdByUserId: number;
  createdAt: string;
  createdByNombre: string | null;
}

export interface TicketDocumento {
  id: number;
  contenidoBase64: string;
  contentType: string | null;
  fileName: string | null;
}

export interface CreateTicketNotaRequest {
  nota: string;
  esInterna?: boolean;
}

/** Filtros de búsqueda paginada (mis-tickets / gestión / admin). */
export interface TicketListFilter {
  anio?: number;
  estado?: string;
  tipo?: string;
  paisId?: number;
  companyId?: number;
  assignedToGuid?: string;
  prioridad?: string;
  /** Busca en código, título y descripción. */
  texto?: string;
  page?: number;
  pageSize?: number;
}

/** Usuario resolutor para el dropdown de filtro del admin. */
export interface ResolutorAdminDto {
  guid: string;
  nombre: string;
}

// ───────────────────────── Salida ─────────────────────────

/** Ítem de listado: ligero, SIN imágenes en Base64 (solo contadores). */
export interface TicketListItem {
  id: number;
  codigo: string | null;
  titulo: string;
  tipo: TipoTicket;
  estado: EstadoTicket;
  paisId: number;
  createdByUserId: number;
  assignedToUserId: number | null;
  createdAt: string;
  cantidadImagenes: number;
  cantidadNotas: number;
  // Identidad legible (nombre completo + rol en la empresa del ticket)
  createdByNombre: string | null;
  createdByRol: string | null;
  assignedToNombre: string | null;
  assignedToRol: string | null;
  paisNombre: string | null;
  // ── Gestión tipo tablero (el backend siempre los envía; default neutro) ──
  prioridad: PrioridadTicket;
  ordenTablero: number;
  fechaLimite: string | null;
  fechaInicioPlan: string | null;
  fechaFinPlan: string | null;
  horasEstimadas: number | null;
  horasRegistradas: number;
  cantidadTareas: number;
  tareasListas: number;
  avanceTareas: number;
  estadoSla: EstadoSla;
  horasParaVencer: number | null;
  /** Nombre del solicitante real (delegado si lo hay; si no, el creador). */
  solicitanteNombre: string | null;
  /** True si el caso lo registró alguien distinto del solicitante. */
  registradoPorTercero: boolean;
}

export interface TicketNota {
  id: number;
  userId: number;
  nota: string;
  estadoResultante: EstadoTicket | null;
  esInterna: boolean;
  createdAt: string;
  // Identidad legible del autor de la nota
  userNombre: string | null;
  userRol: string | null;
  userEmail: string | null;
  /** True si la nota la escribió el usuario actual (chat: burbuja a la derecha). */
  esMio: boolean;
  /** Tipo de evento cuando la nota la generó el sistema; null = comentario humano. */
  tipoEvento: string | null;
}

/** Metadata de imagen (SIN base64) — para listar miniaturas a pedir on-demand. */
export interface TicketImagenMeta {
  id: number;
  fileName: string | null;
  contentType: string | null;
  sizeBytes: number | null;
  createdAt: string;
}

/** Usuario notificado (copiado) en un ticket. */
export interface TicketNotificadoDto {
  id: number;
  userGuid: string | null;
  nombre: string | null;
  email: string;
}

/** Usuario candidato para ser notificado (dropdown del create). */
export interface UsuarioNotificableDto {
  guid: string;
  nombre: string;
  email: string;
  rol: string | null;
}

/** Detalle: incluye notas + metadata de imágenes (base64 se pide aparte). */
export interface TicketDetail {
  id: number;
  codigo: string | null;
  titulo: string;
  tipo: TipoTicket;
  estado: EstadoTicket;
  descripcion: string;
  paisId: number;
  createdByUserId: number;
  assignedToUserId: number | null;
  createdAt: string;
  fechaPrimeraApertura: string | null;
  fechaSolucion: string | null;
  notas: TicketNota[];
  imagenes: TicketImagenMeta[];
  // Identidad legible (nombre completo + rol en la empresa del ticket)
  createdByNombre: string | null;
  createdByRol: string | null;
  assignedToNombre: string | null;
  assignedToRol: string | null;
  paisNombre: string | null;
  createdByEmail: string | null;
  assignedToEmail: string | null;
  /** True si el usuario actual es el creador (oculta "Tomar"). */
  soyCreador: boolean;
  // Cierre + notificación
  solucionDescripcion: string | null;
  fechaCierreSolicitante: string | null;
  notificadoCorreo: boolean;
  fechaNotificacionCorreo: string | null;
  correoNotificadoA: string | null;
  adjuntos: TicketAdjunto[] | null;
  /** Usuarios notificados (copiados) al crear el ticket. */
  notificados?: TicketNotificadoDto[];
  // ── Solicitante delegado ("a nombre de") ──
  solicitanteUserGuid: string | null;
  solicitanteNombre: string | null;
  solicitanteRol: string | null;
  solicitanteEmail: string | null;
  /** True si el caso lo registró alguien distinto del solicitante. */
  registradoPorTercero: boolean;
  /** True si el usuario actual es el solicitante: puede confirmar cierre o reabrir. */
  soySolicitante: boolean;
  // ── Gestión tipo tablero ──
  prioridad: PrioridadTicket;
  ordenTablero: number;
  fechaLimite: string | null;
  fechaInicioPlan: string | null;
  fechaFinPlan: string | null;
  horasEstimadas: number | null;
  horasRegistradas: number;
  tareas: TicketTarea[] | null;
  metricas: TicketMetricas | null;
  /** Guid del responsable — preselecciona el desplegable de reasignación. */
  assignedToUserGuid: string | null;
}

/** Una imagen on-demand (con base64). */
export interface TicketImagen {
  id: number;
  imagenBase64: string;
  contentType: string | null;
  fileName: string | null;
}

// ───────────────────────── Catálogos / helpers de UI ─────────────────────────

export const TIPOS_TICKET: { value: TipoTicket; label: string; descripcion: string }[] = [
  { value: 'SOPORTE',       label: 'Soporte',       descripcion: 'Ayuda con el uso o un problema operativo' },
  { value: 'DESARROLLO',    label: 'Desarrollo',    descripcion: 'Error técnico / bug a corregir' },
  { value: 'REQUERIMIENTO', label: 'Requerimiento', descripcion: 'Nueva funcionalidad o mejora' },
  { value: 'DUDAS',         label: 'Dudas',         descripcion: 'Consulta o pregunta' },
];

export const TIPO_LABEL: Record<TipoTicket, string> = {
  SOPORTE: 'Soporte',
  DESARROLLO: 'Desarrollo',
  REQUERIMIENTO: 'Requerimiento',
  DUDAS: 'Dudas',
};

export const ESTADOS_TICKET: EstadoTicket[] = [
  'ABIERTO', 'EN_ANALISIS', 'EN_DOCUMENTACION', 'EN_IMPLEMENTACION', 'EN_REVISION',
  'SOLUCIONADO', 'CERRADO', 'TRANSFERIDO', 'SUSPENDIDO',
];

/** Fases en las que el equipo trabaja el caso (columnas centrales del tablero). */
export const FASES_TRABAJO: EstadoTicket[] =
  ['EN_ANALISIS', 'EN_DOCUMENTACION', 'EN_IMPLEMENTACION', 'EN_REVISION'];

/** Columnas del tablero kanban de casos, de izquierda a derecha. */
export const COLUMNAS_TABLERO: EstadoTicket[] = [
  'ABIERTO', 'EN_ANALISIS', 'EN_DOCUMENTACION', 'EN_IMPLEMENTACION', 'EN_REVISION',
  'SOLUCIONADO', 'CERRADO',
];

export const ESTADO_LABEL: Record<EstadoTicket, string> = {
  ABIERTO: 'Abierto',
  EN_ANALISIS: 'En análisis',
  EN_DOCUMENTACION: 'En documentación',
  EN_IMPLEMENTACION: 'En implementación',
  EN_REVISION: 'En revisión',
  SOLUCIONADO: 'Solucionado',
  CERRADO: 'Cerrado',
  TRANSFERIDO: 'Transferido',
  SUSPENDIDO: 'Suspendido',
};

/** Clases Tailwind para el badge de estado. */
export const ESTADO_BADGE: Record<EstadoTicket, string> = {
  ABIERTO:           'bg-sky-50 text-sky-700 ring-sky-200',
  EN_ANALISIS:       'bg-amber-50 text-amber-700 ring-amber-200',
  EN_DOCUMENTACION:  'bg-violet-50 text-violet-700 ring-violet-200',
  EN_IMPLEMENTACION: 'bg-indigo-50 text-indigo-700 ring-indigo-200',
  EN_REVISION:       'bg-cyan-50 text-cyan-700 ring-cyan-200',
  SOLUCIONADO:       'bg-emerald-50 text-emerald-700 ring-emerald-200',
  CERRADO:           'bg-ital-green text-white ring-ital-green-dark',
  TRANSFERIDO:       'bg-slate-100 text-slate-600 ring-slate-200',
  SUSPENDIDO:        'bg-rose-50 text-rose-700 ring-rose-200',
};

/** Acento lateral (border-left) por estado — para las cards. */
export const ESTADO_BORDER: Record<EstadoTicket, string> = {
  ABIERTO:           'border-l-sky-400',
  EN_ANALISIS:       'border-l-amber-400',
  EN_DOCUMENTACION:  'border-l-violet-400',
  EN_IMPLEMENTACION: 'border-l-indigo-400',
  EN_REVISION:       'border-l-cyan-400',
  SOLUCIONADO:       'border-l-emerald-500',
  CERRADO:           'border-l-ital-green',
  TRANSFERIDO:       'border-l-slate-400',
  SUSPENDIDO:        'border-l-rose-400',
};

/** Punto de color por estado (para filtros/segmentos). */
export const ESTADO_DOT: Record<EstadoTicket, string> = {
  ABIERTO:           'bg-sky-400',
  EN_ANALISIS:       'bg-amber-400',
  EN_DOCUMENTACION:  'bg-violet-400',
  EN_IMPLEMENTACION: 'bg-indigo-400',
  EN_REVISION:       'bg-cyan-400',
  SOLUCIONADO:       'bg-emerald-500',
  CERRADO:           'bg-ital-green',
  TRANSFERIDO:       'bg-slate-400',
  SUSPENDIDO:        'bg-rose-400',
};

/** Pasos lineales del stepper (los estados especiales se muestran aparte). */
export const STEPPER_STEPS: EstadoTicket[] = [
  'ABIERTO', 'EN_ANALISIS', 'EN_DOCUMENTACION', 'EN_IMPLEMENTACION', 'EN_REVISION', 'SOLUCIONADO', 'CERRADO',
];
export const ESTADOS_ESPECIALES: EstadoTicket[] = ['TRANSFERIDO', 'SUSPENDIDO'];

/**
 * Transiciones que ofrece la UI al RESOLUTOR (el cierre lo confirma el solicitante aparte).
 * Espeja `TicketEstados.Transiciones` del backend: las cuatro fases de trabajo se mueven
 * libremente entre sí, que es lo que hace usable el tablero.
 */
export const TRANSICIONES: Record<EstadoTicket, EstadoTicket[]> = {
  ABIERTO:           ['EN_ANALISIS', 'EN_DOCUMENTACION', 'EN_IMPLEMENTACION', 'EN_REVISION', 'SUSPENDIDO', 'TRANSFERIDO'],
  EN_ANALISIS:       ['EN_DOCUMENTACION', 'EN_IMPLEMENTACION', 'EN_REVISION', 'SOLUCIONADO', 'SUSPENDIDO', 'TRANSFERIDO'],
  EN_DOCUMENTACION:  ['EN_ANALISIS', 'EN_IMPLEMENTACION', 'EN_REVISION', 'SOLUCIONADO', 'SUSPENDIDO', 'TRANSFERIDO'],
  EN_IMPLEMENTACION: ['EN_ANALISIS', 'EN_DOCUMENTACION', 'EN_REVISION', 'SOLUCIONADO', 'SUSPENDIDO', 'TRANSFERIDO'],
  EN_REVISION:       ['EN_ANALISIS', 'EN_DOCUMENTACION', 'EN_IMPLEMENTACION', 'SOLUCIONADO', 'SUSPENDIDO', 'TRANSFERIDO'],
  SOLUCIONADO:       ['EN_ANALISIS'],
  CERRADO:           [],
  TRANSFERIDO:       ['EN_ANALISIS', 'EN_DOCUMENTACION', 'EN_IMPLEMENTACION', 'EN_REVISION', 'SUSPENDIDO'],
  SUSPENDIDO:        ['EN_ANALISIS', 'EN_DOCUMENTACION', 'EN_IMPLEMENTACION', 'EN_REVISION'],
};

/** Claves de permiso del módulo (se siembran en la tabla `permissions`). */
export const TICKET_PERMS = {
  crear: 'tickets.crear',
  gestionar: 'tickets.gestionar',
  admin: 'tickets.admin',
} as const;
