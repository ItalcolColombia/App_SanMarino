export interface VeterinariaLoteDto { id: number; nombre: string; fase?: string | null; }
export interface VeterinariaGalponDto { id: string; nombre: string; lotes: VeterinariaLoteDto[]; }
export interface VeterinariaNucleoDto {
  id: string;
  nombre: string;
  galpones: VeterinariaGalponDto[];
  lotesSinGalpon: VeterinariaLoteDto[];
}
export interface VeterinariaGranjaDto {
  id: number;
  nombre: string;
  latitud?: number | null;
  longitud?: number | null;
  nucleos: VeterinariaNucleoDto[];
  lotesSinUbicacion: VeterinariaLoteDto[];
  totalGalpones: number;
  totalLotes: number;
}

export type EstadoVisita = 'PROGRAMADA' | 'REALIZADA' | 'CANCELADA';
export interface VisitaTecnicaDto {
  id: number;
  farmId: number;
  farmNombre: string;
  nucleoId?: string | null;
  nucleoNombre?: string | null;
  galponId?: string | null;
  galponNombre?: string | null;
  loteId?: number | null;
  loteNombre?: string | null;
  titulo: string;
  objetivo?: string | null;
  fechaProgramada: string;
  fechaRealizada?: string | null;
  estado: EstadoVisita;
  observaciones?: string | null;
  veterinarioUserId: string;
  veterinarioNombre: string;
  totalTareas: number;
  tareasPendientes: number;
  createdAt: string;
}

export interface VisitaTecnicaRequest {
  farmId: number;
  nucleoId: string | null;
  galponId: string | null;
  loteId: number | null;
  titulo: string;
  objetivo: string | null;
  fechaProgramada: string;
}

export type EstadoTareaCampo = 'PENDIENTE' | 'REALIZADA' | 'CANCELADA';
export type EstadoTemporalTarea = 'PROXIMA' | 'ACTIVA' | 'VENCIDA' | 'CERRADA';
export interface TareaCampoDto {
  id: number;
  visitaId?: number | null;
  farmId: number;
  farmNombre: string;
  nucleoId?: string | null;
  nucleoNombre?: string | null;
  galponId?: string | null;
  galponNombre?: string | null;
  loteId?: number | null;
  loteNombre?: string | null;
  titulo: string;
  instrucciones?: string | null;
  fechaInicio: string;
  fechaFin: string;
  requiereObservacion: boolean;
  requiereFoto: boolean;
  estado: EstadoTareaCampo;
  estadoTemporal: EstadoTemporalTarea;
  creadaPorUserId: string;
  creadaPorNombre: string;
  realizadaPorUserId?: string | null;
  realizadaPorNombre?: string | null;
  fechaRealizada?: string | null;
  observacionCumplimiento?: string | null;
  cantidadEvidencias: number;
  puedeCumplir: boolean;
  createdAt: string;
}

export interface TareaCampoRequest {
  visitaId: number | null;
  farmId: number;
  nucleoId: string | null;
  galponId: string | null;
  loteId: number | null;
  titulo: string;
  instrucciones: string | null;
  fechaInicio: string;
  fechaFin: string;
  requiereObservacion: boolean;
  requiereFoto: boolean;
}

export interface TareaCampoEvidenciaInput {
  base64: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
}

export interface CumplirTareaCampoRequest {
  observacion: string | null;
  evidencias: TareaCampoEvidenciaInput[];
}

export interface VeterinariaResumenDto {
  granjasAsignadas: number;
  visitasProximas: number;
  tareasPendientes: number;
  tareasVencidas: number;
}
export interface GestionVeterinariaInicioDto {
  resumen: VeterinariaResumenDto;
  tareas: TareaCampoDto[];
}
