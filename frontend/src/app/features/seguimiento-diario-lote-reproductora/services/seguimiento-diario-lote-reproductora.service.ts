/**
 * Servicio para Seguimiento Diario Lote Reproductora Aves de Engorde.
 * API: api/SeguimientoDiarioLoteReproductora
 * Persiste en tabla seguimiento_diario_lote_reproductora_aves_engorde (FK a lote_reproductora_ave_engorde.id).
 * Request propio: CreateSeguimientoDiarioLoteReproductoraDto (no comparte tipo con levante).
 */
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

import type { SeguimientoLoteLevanteDto } from '../../lote-levante/services/seguimiento-lote-levante.service';
import type { ItemSeguimientoDto } from '../../lote-levante/services/seguimiento-lote-levante.service';

export type { SeguimientoLoteLevanteDto };

/** Request para crear seguimiento diario reproductora. Misma forma JSON que el backend CreateSeguimientoDiarioLoteReproductoraRequest. loteId = lote_reproductora_ave_engorde_id. */
export interface CreateSeguimientoDiarioLoteReproductoraDto {
  fechaRegistro: string;
  loteId: string | number;
  mortalidadHembras: number;
  mortalidadMachos: number;
  selH: number;
  selM: number;
  errorSexajeHembras: number;
  errorSexajeMachos: number;
  tipoAlimento: string;
  itemsHembras?: ItemSeguimientoDto[] | null;
  itemsMachos?: ItemSeguimientoDto[] | null;
  consumoHembras?: number | null;
  unidadConsumoHembras?: string | null;
  consumoMachos?: number | null;
  unidadConsumoMachos?: string | null;
  pesoPromH?: number | null;
  pesoPromM?: number | null;
  uniformidadH?: number | null;
  uniformidadM?: number | null;
  cvH?: number | null;
  cvM?: number | null;
  observaciones?: string | null;
  kcalAlH?: number | null;
  protAlH?: number | null;
  kcalAveH?: number | null;
  protAveH?: number | null;
  ciclo: string;
  consumoAguaDiario?: number | null;
  consumoAguaPh?: number | null;
  consumoAguaOrp?: number | null;
  consumoAguaTemperatura?: number | null;
  createdByUserId?: string | null;
}

export interface UpdateSeguimientoDiarioLoteReproductoraDto extends CreateSeguimientoDiarioLoteReproductoraDto {
  id: number;
}

export interface LoteReproductoraSeguimientoFilterItemDto {
  id: number;
  nombreLote: string;
  loteAveEngordeId: number;
}

export interface SeguimientoDiarioLoteReproductoraFilterDataDto {
  farms: Array<{ id: number; name: string }>;
  nucleos: Array<{ nucleoId: string; nucleoNombre: string; granjaId: number }>;
  galpones: Array<{ galponId: string; galponNombre: string; nucleoId: string; granjaId: number }>;
  lotes: Array<{ loteId: number; loteNombre: string; granjaId: number; nucleoId: string | null; galponId: string | null }>;
  lotesReproductora: LoteReproductoraSeguimientoFilterItemDto[];
}

@Injectable({ providedIn: 'root' })
export class SeguimientoDiarioLoteReproductoraService {
  private readonly baseUrl = `${environment.apiUrl}/SeguimientoDiarioLoteReproductora`;

  constructor(private http: HttpClient) {}

  getFilterData(): Observable<SeguimientoDiarioLoteReproductoraFilterDataDto> {
    return this.http.get<SeguimientoDiarioLoteReproductoraFilterDataDto>(`${this.baseUrl}/filter-data`);
  }

  getById(id: number): Observable<SeguimientoLoteLevanteDto> {
    return this.http.get<SeguimientoLoteLevanteDto>(`${this.baseUrl}/${id}`);
  }

  getByLoteReproductoraId(loteReproductoraId: number): Observable<SeguimientoLoteLevanteDto[]> {
    return this.http.get<SeguimientoLoteLevanteDto[]>(
      `${this.baseUrl}/por-lote-reproductora/${encodeURIComponent(String(loteReproductoraId))}`
    );
  }

  filter(params: {
    loteReproductoraId?: number;
    desde?: string | Date;
    hasta?: string | Date;
  }): Observable<SeguimientoLoteLevanteDto[]> {
    let hp = new HttpParams();
    if (params.loteReproductoraId != null) hp = hp.set('loteReproductoraId', String(params.loteReproductoraId));
    if (params.desde) hp = hp.set('desde', this.toIso(params.desde));
    if (params.hasta) hp = hp.set('hasta', this.toIso(params.hasta));
    return this.http.get<SeguimientoLoteLevanteDto[]>(`${this.baseUrl}/filtro`, { params: hp });
  }

  create(dto: CreateSeguimientoDiarioLoteReproductoraDto): Observable<SeguimientoLoteLevanteDto> {
    return this.http.post<SeguimientoLoteLevanteDto>(this.baseUrl, dto);
  }

  update(dto: UpdateSeguimientoDiarioLoteReproductoraDto): Observable<SeguimientoLoteLevanteDto> {
    return this.http.put<SeguimientoLoteLevanteDto>(`${this.baseUrl}/${dto.id}`, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  private toIso(d: string | Date): string {
    const dd = typeof d === 'string' ? new Date(d) : d;
    return dd.toISOString();
  }
}
