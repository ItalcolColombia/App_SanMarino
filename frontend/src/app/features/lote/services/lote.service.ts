import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { LoteReproductoraDto } from '../../lote-reproductora/services/lote-reproductora.service';


// ⬇️ Resumen de mortalidad y descuentos del lote en Levante (historial + descuentos)
export interface LoteMortalidadResumenDto {
  loteId: string;
  hembrasIniciales: number;
  machosIniciales: number;
  mortCajaHembras: number;
  mortCajaMachos: number;
  mortalidadAcumHembras: number;
  mortalidadAcumMachos: number;
  selAcumHembras: number;
  selAcumMachos: number;
  errorSexajeAcumHembras: number;
  errorSexajeAcumMachos: number;
  saldoHembras: number;
  saldoMachos: number;
}

export interface LoteDto {
  loteId: number;
  loteNombre: string;

  // Claves base
  granjaId: number;
  nucleoId?: string | null;
  galponId?: string | null;

  // Datos principales
  regional?: string;
  fechaEncaset?: string;

  hembrasL?: number;
  machosL?: number;
  mixtas?: number | null;
  pesoInicialH?: number;
  pesoInicialM?: number;
  pesoMixto?: number | null;
  unifH?: number;
  unifM?: number;
  mortCajaH?: number;
  mortCajaM?: number;

  raza?: string;
  anoTablaGenetica?: number | null;
  linea?: string;
  tipoLinea?: string;
  codigoGuiaGenetica?: string;
  lineaGeneticaId?: number | null;
  tecnico?: string;

  avesEncasetadas?: number | null;

  loteErp?: string;
  lineaGenetica?: string;

  // 🔹 Relaciones completas que trae el backend
  farm?: {
    id: number;
    name: string;
    regionalId?: number | null;
    departamentoId?: number | null;
    ciudadId?: number | null;
  } | null;

  nucleo?: {
    nucleoId: string;
    nucleoNombre?: string | null;
    granjaId?: number | null;
  } | null;

  galpon?: {
    galponId: string;
    galponNombre?: string | null;
    nucleoId?: string | null;
    granjaId?: number | null;
  } | null;

  // 🔹 NUEVO: metadatos (el backend los envía en tu ejemplo)
  companyId?: number | null;
  createdByUserId?: number | null;
  createdAt?: string | null;
  updatedByUserId?: number | null;
  updatedAt?: string | null;

  // (por compatibilidad si tu backend a veces devuelve edad)
  edadInicial?: number | null;

  // Estado de traslado
  estadoTraslado?: string | null; // "normal", "trasladado", "en_transferencia"
  
  // Lote padre (Opción B)
  lotePadreId?: number | null;

  /** Fase del lote: "Levante" o "Produccion". */
  fase?: string | null;

  /** ID y nombre del país en sesión al crear; nombre de la empresa en sesión. */
  paisId?: number | null;
  paisNombre?: string | null;
  empresaNombre?: string | null;

  /** Pollo engorde: Abierto | Cerrado (liquidación operativa). */
  estadoOperativoLote?: string | null;
}


export interface CreateLoteDto extends Omit<LoteDto, 'loteId'> {
  loteId?: number; // Opcional - auto-incremento numérico
}

export interface UpdateLoteDto extends LoteDto {}

/** Respuesta del endpoint form-data: todos los catálogos para el modal crear/editar lote en una sola llamada. */
export interface LoteFormDataResponse {
  farms: Array<{ id: number; name: string; companyId?: number }>;
  nucleos: Array<{ nucleoId: string; nucleoNombre?: string | null; granjaId: number }>;
  galpones: Array<{ galponId: string; galponNombre?: string | null; nucleoId: string; granjaId: number }>;
  tecnicos: Array<{ id?: string; firstName?: string; surName?: string }>;
  companies: Array<{ id: number; name: string }>;
  razas: string[];
}

@Injectable({ providedIn: 'root' })
export class LoteService {
  private readonly baseUrl = `${environment.apiUrl}/Lote`;
  private readonly base = environment.apiUrl;
  private readonly http = inject(HttpClient);

  constructor() {}

  /** Obtiene en una sola llamada todos los datos para el formulario de crear/editar lote. */
  getFormData(): Observable<LoteFormDataResponse> {
    return this.http.get<LoteFormDataResponse>(`${this.baseUrl}/form-data`);
  }

  /** @param fase 'levante' | 'produccion' | undefined (todos, sin lotes hijo de producción) */
  getAll(fase?: 'levante' | 'produccion'): Observable<LoteDto[]> {
    if (fase) {
      return this.http.get<LoteDto[]>(this.baseUrl, { params: { fase } });
    }
    return this.http.get<LoteDto[]>(this.baseUrl);
  }

  getById(loteId: number): Observable<LoteDto> {
    return this.http.get<LoteDto>(`${this.baseUrl}/${loteId}`);
  }

  create(dto: CreateLoteDto): Observable<LoteDto> {
    return this.http.post<LoteDto>(this.baseUrl, dto);
  }

  update(dto: UpdateLoteDto): Observable<LoteDto> {
    return this.http.put<LoteDto>(`${this.baseUrl}/${dto.loteId}`, dto);
  }

  delete(loteId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${loteId}`);
  }

  getReproductorasByLote(loteId: number): Observable<LoteReproductoraDto[]> {
    return this.http.get<LoteReproductoraDto[]>(`${this.base}/LoteReproductora?loteId=${loteId}`);
  }

   // ⬇️ NUEVO: resumen de mortalidad por lote
  getResumenMortalidad(loteId: number): Observable<LoteMortalidadResumenDto> {
    // Si tu backend quedó como /api/Lotes/{id}/..., cambia baseUrl por `${this.base}/Lotes`
    return this.http.get<LoteMortalidadResumenDto>(
      `${this.baseUrl}/${loteId}/resumen-mortalidad`
    );
  }

  /** Obtiene los lotes filtrados por galpón (galponId) */
  getByGalpon(galponId: string): Observable<LoteDto[]> {
    // Mantiene el patrón de filtros por query-string usado en el archivo
    return this.http.get<LoteDto[]>(
      `${this.baseUrl}?galponId=${encodeURIComponent(galponId)}`
    );
  }

  // ======================================================
  // TRASLADO DE LOTE
  // ======================================================
  trasladarLote(request: TrasladoLoteRequest): Observable<TrasladoLoteResponse> {
    return this.http.post<TrasladoLoteResponse>(`${this.baseUrl}/trasladar`, request);
  }
}

export interface TrasladoLoteRequest {
  loteId: number;
  granjaDestinoId: number;
  nucleoDestinoId?: string | null;
  galponDestinoId?: string | null;
  observaciones?: string | null;
}

export interface TrasladoLoteResponse {
  success: boolean;
  message: string;
  loteOriginalId?: number | null;
  loteNuevoId?: number | null;
  loteNombre?: string | null;
  granjaOrigen?: string | null;
  granjaDestino?: string | null;
}
