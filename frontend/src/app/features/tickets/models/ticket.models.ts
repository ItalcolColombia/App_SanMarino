// src/app/features/tickets/models/ticket.models.ts
// Tipos del módulo de tickets de soporte. Alineados con los DTOs del backend
// (ZooSanMarino.Application.DTOs.Tickets). El backend serializa en camelCase.

export type TipoTicket = 'SOPORTE' | 'DESARROLLO' | 'REQUERIMIENTO' | 'DUDAS';

export type EstadoTicket =
  | 'ABIERTO'
  | 'EN_ANALISIS'
  | 'EN_IMPLEMENTACION'
  | 'SOLUCIONADO'
  | 'TRANSFERIDO'
  | 'SUSPENDIDO';

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
  imagenes?: TicketImagenInput[] | null;
}

export interface AddTicketImagenesRequest {
  imagenes: TicketImagenInput[];
}

export interface CambiarEstadoTicketRequest {
  estado: EstadoTicket;
  nota?: string | null;
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
  page?: number;
  pageSize?: number;
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
}

export interface TicketNota {
  id: number;
  userId: number;
  nota: string;
  estadoResultante: EstadoTicket | null;
  esInterna: boolean;
  createdAt: string;
}

/** Metadata de imagen (SIN base64) — para listar miniaturas a pedir on-demand. */
export interface TicketImagenMeta {
  id: number;
  fileName: string | null;
  contentType: string | null;
  sizeBytes: number | null;
  createdAt: string;
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
  'ABIERTO', 'EN_ANALISIS', 'EN_IMPLEMENTACION', 'SOLUCIONADO', 'TRANSFERIDO', 'SUSPENDIDO',
];

export const ESTADO_LABEL: Record<EstadoTicket, string> = {
  ABIERTO: 'Abierto',
  EN_ANALISIS: 'En análisis',
  EN_IMPLEMENTACION: 'En implementación',
  SOLUCIONADO: 'Solucionado',
  TRANSFERIDO: 'Transferido',
  SUSPENDIDO: 'Suspendido',
};

/** Clases Tailwind para el badge de estado. */
export const ESTADO_BADGE: Record<EstadoTicket, string> = {
  ABIERTO:           'bg-sky-50 text-sky-700 ring-sky-200',
  EN_ANALISIS:       'bg-amber-50 text-amber-700 ring-amber-200',
  EN_IMPLEMENTACION: 'bg-indigo-50 text-indigo-700 ring-indigo-200',
  SOLUCIONADO:       'bg-emerald-50 text-emerald-700 ring-emerald-200',
  TRANSFERIDO:       'bg-slate-100 text-slate-600 ring-slate-200',
  SUSPENDIDO:        'bg-rose-50 text-rose-700 ring-rose-200',
};

/** Pasos lineales del stepper (los estados especiales se muestran aparte). */
export const STEPPER_STEPS: EstadoTicket[] = ['ABIERTO', 'EN_ANALISIS', 'EN_IMPLEMENTACION', 'SOLUCIONADO'];
export const ESTADOS_ESPECIALES: EstadoTicket[] = ['TRANSFERIDO', 'SUSPENDIDO'];
