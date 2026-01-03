import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface SeguimientoLoteLevanteDto {
  id: number;
  fechaRegistro: string;          // ISO
  loteId: string;

  mortalidadHembras: number;
  mortalidadMachos: number;
  selH: number;
  selM: number;
  errorSexajeHembras: number;
  errorSexajeMachos: number;

  tipoAlimento: string;
  consumoKgHembras: number; // Consumo convertido a kg (para cálculos)

  // Opcionales nuevos
  consumoKgMachos?: number | null;
  pesoPromH?: number | null;
  pesoPromM?: number | null;
  uniformidadH?: number | null;
  uniformidadM?: number | null;
  cvH?: number | null;
  cvM?: number | null;

  // Metadata JSONB para campos adicionales/extras
  metadata?: any | null; // JSON object con consumo original y otros campos adicionales

  observaciones?: string;
  kcalAlH?: number | null;
  protAlH?: number | null;
  kcalAveH?: number | null;
  protAveH?: number | null;
  ciclo: string;                  // "Normal" | "Reforzado"
  tipoAlimentoHembras?: number | null; // (calculo interno, no se envía en create/update)
  tipoAlimentoMachos?: number | null;  // (calculo interno, no se envía en create/update)
}

export interface CreateSeguimientoLoteLevanteDto {
  fechaRegistro: string;          // ISO
  loteId: string;

  mortalidadHembras: number;
  mortalidadMachos: number;
  selH: number;
  selM: number;
  errorSexajeHembras: number;
  errorSexajeMachos: number;

  tipoAlimento: string;
  
  // Consumo con unidad - el backend hace la conversión
  consumoHembras?: number | null;
  unidadConsumoHembras?: string; // "kg" o "g" - default "kg"
  consumoMachos?: number | null;
  unidadConsumoMachos?: string; // "kg" o "g" - default "kg"
  
  // Mantener para compatibilidad (deprecated - usar consumoHembras/Machos con unidad)
  consumoKgHembras?: number;
  consumoKgMachos?: number | null;

  pesoPromH?: number | null;
  pesoPromM?: number | null;
  uniformidadH?: number | null;
  uniformidadM?: number | null;
  cvH?: number | null;
  cvM?: number | null;

  observaciones?: string;
  kcalAlH?: number | null;
  protAlH?: number | null;
  kcalAveH?: number | null;
  protAveH?: number | null;
  ciclo: string;
  tipoAlimentoHembras?: number | null;
  tipoAlimentoMachos?: number | null;
  // Tipo de ítem (alimento, medicamento, etc.) - se guarda en Metadata
  tipoItemHembras?: string | null;
  tipoItemMachos?: string | null;
}

export interface UpdateSeguimientoLoteLevanteDto extends CreateSeguimientoLoteLevanteDto {
  id: number;
}

export interface ResultadoLevanteItemDto {
  fecha: string;            // "2025-09-08T00:00:00"
  edadDias: number | null;  // Cambiado de edadSemana a edadDias
  edadSemana?: number | null; // @deprecated - mantener para compatibilidad

  hembraViva: number | null;
  mortH: number; selH: number; errH: number;
  consKgH: number | null; pesoH: number | null; unifH: number | null; cvH: number | null;
  mortHPct: number | null; selHPct: number | null; errHPct: number | null;
  msEhH: number | null; acMortH: number | null; acSelH: number | null; acErrH: number | null;
  acConsKgH: number | null; consAcGrH: number | null; grAveDiaH: number | null;
  difConsHPct: number | null; difPesoHPct: number | null; retiroHPct: number | null; retiroHAcPct: number | null;

  machoVivo: number | null;
  mortM: number; selM: number; errM: number;
  consKgM: number | null; pesoM: number | null; unifM: number | null; cvM: number | null;
  mortMPct: number | null; selMPct: number | null; errMPct: number | null;
  msEmM: number | null; acMortM: number | null; acSelM: number | null; acErrM: number | null;
  acConsKgM: number | null; consAcGrM: number | null; grAveDiaM: number | null;
  difConsMPct: number | null; difPesoMPct: number | null; retiroMPct: number | null; retiroMAcPct: number | null;

  relMHPct: number | null;

  pesoHGuia: number | null; unifHGuia: number | null; consAcGrHGuia: number | null; grAveDiaHGuia: number | null; mortHPctGuia: number | null;
  pesoMGuia: number | null; unifMGuia: number | null; consAcGrMGuia: number | null; grAveDiaMGuia: number | null; mortMPctGuia: number | null;
  alimentoHGuia: string | null; alimentoMGuia: string | null;
}

export interface ResultadoLevanteResponse {
  loteId: string;
  desde: string | null;
  hasta: string | null;
  total: number;
  items: ResultadoLevanteItemDto[];
}

@Injectable({ providedIn: 'root' })
export class SeguimientoLoteLevanteService {
  /** Nota: environment.apiUrl debe incluir `/api` (ej: http://localhost:5002/api) */
  private readonly baseUrl = `${environment.apiUrl}/SeguimientoLoteLevante`;

  constructor(private http: HttpClient) {}

  /** GET general usando el endpoint de filtro sin parámetros */
  getAll(): Observable<SeguimientoLoteLevanteDto[]> {
    return this.http.get<SeguimientoLoteLevanteDto[]>(`${this.baseUrl}/filtro`);
  }

  /**
   * Polyfill de GetById: trae todo y filtra.
   * (Si el backend agrega GET /{id}, cámbialo por una llamada directa.)
   */
  getById(id: number): Observable<SeguimientoLoteLevanteDto> {
    return this.getAll().pipe(
      map(list => {
        const found = (list ?? []).find(x => x.id === id);
        if (!found) throw new Error(`Seguimiento ${id} no encontrado`);
        return found;
      })
    );
  }

  /** GET por LoteId */
  getByLoteId(loteId: number): Observable<SeguimientoLoteLevanteDto[]> {  // Changed from string to number
    return this.http.get<SeguimientoLoteLevanteDto[]>(
      `${this.baseUrl}/por-lote/${encodeURIComponent(loteId.toString())}`  // Convert to string for URL
    );
  }

  /** Filtro (loteId, desde, hasta) en ISO */
  filter(params: { loteId?: string; desde?: string | Date; hasta?: string | Date }): Observable<SeguimientoLoteLevanteDto[]> {
    let hp = new HttpParams();
    if (params.loteId) hp = hp.set('loteId', params.loteId);
    if (params.desde)  hp = hp.set('desde', this.toIso(params.desde));
    if (params.hasta)  hp = hp.set('hasta', this.toIso(params.hasta));
    return this.http.get<SeguimientoLoteLevanteDto[]>(`${this.baseUrl}/filtro`, { params: hp });
  }

  /** Crear */
  create(dto: CreateSeguimientoLoteLevanteDto): Observable<SeguimientoLoteLevanteDto> {
    return this.http.post<SeguimientoLoteLevanteDto>(this.baseUrl, dto);
  }

  /** Actualizar */
  update(dto: UpdateSeguimientoLoteLevanteDto): Observable<SeguimientoLoteLevanteDto> {
    return this.http.put<SeguimientoLoteLevanteDto>(`${this.baseUrl}/${dto.id}`, dto);
  }

  /** Eliminar */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  // Helpers
  private toIso(d: string | Date): string {
    const dd = typeof d === 'string' ? new Date(d) : d;
    return dd.toISOString();
    // Si el back requiriera fecha sin hora: return dd.toISOString().substring(0, 10);
  }

  getResultado(params: {
    loteId: number;  // Changed from string to number
    desde?: string | Date;
    hasta?: string | Date;
    recalcular?: boolean;     // default true
  }): Observable<ResultadoLevanteResponse> {
    const { loteId } = params;
    let hp = new HttpParams()
      .set('recalcular', String(params.recalcular ?? true));
    if (params.desde) hp = hp.set('desde', this.toIso(params.desde));
    if (params.hasta) hp = hp.set('hasta', this.toIso(params.hasta));
    const url = `${this.baseUrl}/por-lote/${encodeURIComponent(loteId)}/resultado`;
    return this.http.get<ResultadoLevanteResponse>(url, { params: hp });
  }

 
}
