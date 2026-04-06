import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

export interface FarmDto {
  readonly id: number;
  readonly name: string;
  readonly companyId: number;
}

export interface NucleoDto {
  readonly nucleoId: string;
  readonly granjaId: number;
  readonly nucleoNombre: string;
}

export interface GalponFilterItemDto {
  readonly galponId: string;
  readonly galponNombre: string;
  readonly nucleoId: string;
  readonly granjaId: number;
}

export interface LoteAveEngordeFilterItemDto {
  readonly loteAveEngordeId: number;
  readonly loteNombre: string;
  readonly granjaId: number;
  readonly nucleoId: string | null;
  readonly galponId: string | null;
}

export interface LoteReproductoraAveEngordeFilterDataDto {
  readonly farms: ReadonlyArray<FarmDto>;
  readonly nucleos: ReadonlyArray<NucleoDto>;
  readonly galpones: ReadonlyArray<GalponFilterItemDto>;
  readonly lotesAveEngorde: ReadonlyArray<LoteAveEngordeFilterItemDto>;
}

export interface LoteReproductoraAveEngordeDto {
  readonly id: number;
  readonly loteAveEngordeId: number;
  readonly reproductoraId: string;
  readonly nombreLote: string;
  readonly fechaEncasetamiento: string | null;
  readonly m: number | null;
  readonly h: number | null;
  readonly mixtas: number | null;
  readonly mortCajaH: number | null;
  readonly mortCajaM: number | null;
  readonly unifH: number | null;
  readonly unifM: number | null;
  readonly pesoInicialM: number | null;
  readonly pesoInicialH: number | null;
  readonly pesoMixto: number | null;
  /** Cerrado = todas las aves vendidas; Vigente = aún tiene aves */
  readonly estado: string;
  /** Saldo actual = encasetadas - mortalidad - selección - ventas */
  readonly avesActuales: number;
  /** Total aves con que se abrió el lote reproductor */
  readonly saldoApertura?: number;
  /** Hembras al abrir el lote reproductor */
  readonly avesInicioHembras?: number;
  /** Machos al abrir el lote reproductor */
  readonly avesInicioMachos?: number;
}

export interface CreateLoteReproductoraAveEngordeDto {
  loteAveEngordeId: number;
  reproductoraId: string;
  nombreLote: string;
  fechaEncasetamiento?: string | null;
  m?: number | null;
  h?: number | null;
  mixtas?: number | null;
  mortCajaH?: number | null;
  mortCajaM?: number | null;
  unifH?: number | null;
  unifM?: number | null;
  pesoInicialM?: number | null;
  pesoInicialH?: number | null;
  pesoMixto?: number | null;
}

export type UpdateLoteReproductoraAveEngordeDto = CreateLoteReproductoraAveEngordeDto;

/** Respuesta del API GET .../LoteReproductoraAveEngorde/{id}/aves-disponibles */
export interface AvesDisponiblesDto {
  readonly hembrasIniciales?: number;
  readonly machosIniciales?: number;
  readonly mortalidadAcumuladaHembras?: number;
  readonly mortalidadAcumuladaMachos?: number;
  readonly mortCajaHembras?: number;
  readonly mortCajaMachos?: number;
  readonly asignadasHembras?: number;
  readonly asignadasMachos?: number;
  readonly hembrasDisponibles: number;
  readonly machosDisponibles: number;
}

@Injectable({ providedIn: 'root' })
export class LoteReproductoraAveEngordeService {
  private readonly base = environment.apiUrl;
  private readonly resource = `${this.base}/LoteReproductoraAveEngorde`;

  constructor(private http: HttpClient) {}

  private toIso(d?: string | Date | null): string | null {
    if (!d) return null;
    if (typeof d === 'string' && d.length === 10) return new Date(d + 'T00:00:00Z').toISOString();
    const parsed = new Date(d);
    return isNaN(parsed.getTime()) ? null : parsed.toISOString();
  }

  private handleError = (err: HttpErrorResponse) => {
    const detail = err.error?.detail ?? err.error?.title ?? (typeof err.error === 'string' ? err.error : 'Error en el servidor');
    return throwError(() => ({ status: err.status, message: detail }));
  };

  getFilterData(): Observable<LoteReproductoraAveEngordeFilterDataDto> {
    return this.http.get<LoteReproductoraAveEngordeFilterDataDto>(`${this.resource}/filter-data`).pipe(catchError(this.handleError));
  }

  getAll(loteAveEngordeId?: number): Observable<ReadonlyArray<LoteReproductoraAveEngordeDto>> {
    const params = loteAveEngordeId != null ? new HttpParams().set('loteAveEngordeId', String(loteAveEngordeId)) : undefined;
    return this.http.get<LoteReproductoraAveEngordeDto[]>(this.resource, { params }).pipe(catchError(this.handleError));
  }

  getById(id: number): Observable<LoteReproductoraAveEngordeDto> {
    return this.http.get<LoteReproductoraAveEngordeDto>(`${this.resource}/${id}`).pipe(catchError(this.handleError));
  }

  create(dto: CreateLoteReproductoraAveEngordeDto): Observable<LoteReproductoraAveEngordeDto> {
    const payload = { ...dto, fechaEncasetamiento: this.toIso(dto.fechaEncasetamiento) };
    return this.http.post<LoteReproductoraAveEngordeDto>(this.resource, payload).pipe(catchError(this.handleError));
  }

  createBulk(dtos: CreateLoteReproductoraAveEngordeDto[]): Observable<ReadonlyArray<LoteReproductoraAveEngordeDto>> {
    const payload = dtos.map(d => ({ ...d, fechaEncasetamiento: this.toIso(d.fechaEncasetamiento) }));
    return this.http.post<LoteReproductoraAveEngordeDto[]>(`${this.resource}/bulk`, payload).pipe(catchError(this.handleError));
  }

  update(id: number, dto: UpdateLoteReproductoraAveEngordeDto): Observable<LoteReproductoraAveEngordeDto> {
    const payload = { ...dto, fechaEncasetamiento: this.toIso(dto.fechaEncasetamiento) };
    return this.http.put<LoteReproductoraAveEngordeDto>(`${this.resource}/${id}`, payload).pipe(catchError(this.handleError));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.resource}/${id}`).pipe(catchError(this.handleError));
  }

  getAvesDisponibles(loteAveEngordeId: number): Observable<AvesDisponiblesDto> {
    return this.http.get<AvesDisponiblesDto>(`${this.resource}/${loteAveEngordeId}/aves-disponibles`).pipe(catchError(this.handleError));
  }

  /** Código único autogenerado: prefijo LR- + 10 dígitos, sin repetirse en el lote ni en exclude. */
  getNewReproductoraCode(loteAveEngordeId: number, exclude?: string[]): Observable<string> {
    let params = new HttpParams().set('loteAveEngordeId', String(loteAveEngordeId));
    if (exclude?.length) params = params.set('exclude', exclude.join(','));
    return this.http.get<string>(`${this.resource}/new-code`, { params }).pipe(catchError(this.handleError));
  }
}
