// app/features/nucleo/services/nucleo.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, shareReplay, map, tap } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface NucleoDto {
  nucleoId:       string;
  granjaId:       number;
  nucleoNombre:   string;
  granjaNombre?:  string | null;
  companyNombre?: string | null;
  companyId?:     number | null;
}

export interface CreateNucleoDto {
  nucleoId:     string;
  granjaId:     number;
  nucleoNombre: string;
}

export interface UpdateNucleoDto {
  nucleoId:     string;
  granjaId:     number;
  nucleoNombre: string;
}

@Injectable({ providedIn: 'root' })
export class NucleoService {
  private readonly baseUrl = `${environment.apiUrl}/Nucleo`;

  // cache simple para evitar múltiples GET iguales
  private cache$?: Observable<NucleoDto[]>;

  constructor(private http: HttpClient) {}

  /** Todos los núcleos. `force=true` descarta la caché y vuelve a pedir al backend
   *  (necesario cuando cambian granjas/núcleos y hay que reflejarlo sin recargar la app). */
  getAll(force = false): Observable<NucleoDto[]> {
    if (force) this.cache$ = undefined;
    return this.cache$ ??= this.http.get<NucleoDto[]>(this.baseUrl).pipe(shareReplay(1));
  }

  /** Invalida la caché para que el próximo getAll() vuelva a consultar el backend. */
  invalidate(): void {
    this.cache$ = undefined;
  }

  /** Núcleos por granja (filtrado local) */
  getByGranja(granjaId: number): Observable<NucleoDto[]> {
    return this.getAll().pipe(
      map(list => list.filter(n => Number(n.granjaId) === Number(granjaId)))
    );
  }

  /** Uno por clave compuesta */
  getById(nucleoId: string, granjaId: number): Observable<NucleoDto> {
    return this.http.get<NucleoDto>(`${this.baseUrl}/${encodeURIComponent(nucleoId)}/${granjaId}`);
  }

  /** Crear nuevo */
  create(dto: CreateNucleoDto): Observable<NucleoDto> {
    return this.http.post<NucleoDto>(this.baseUrl, dto).pipe(tap(() => this.invalidate()));
  }

  /** Actualizar existente */
  update(dto: UpdateNucleoDto): Observable<NucleoDto> {
    return this.http.put<NucleoDto>(
      `${this.baseUrl}/${encodeURIComponent(dto.nucleoId)}/${dto.granjaId}`,
      dto
    ).pipe(tap(() => this.invalidate()));
  }

  /** Eliminar */
  delete(nucleoId: string, granjaId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${encodeURIComponent(nucleoId)}/${granjaId}`)
      .pipe(tap(() => this.invalidate()));
  }

  /** Mover el núcleo (y sus galpones/lotes) a otra granja. Invalida la caché al terminar. */
  mover(nucleoId: string, granjaOrigenId: number, granjaDestinoId: number): Observable<MoverResult> {
    return this.http.post<MoverResult>(
      `${this.baseUrl}/${encodeURIComponent(nucleoId)}/${granjaOrigenId}/mover`,
      { nucleoId, granjaOrigenId, granjaDestinoId }
    ).pipe(tap(() => this.invalidate()));
  }
}

/** Respuesta de las operaciones de mover (núcleo/galpón): éxito + mensaje + impacto. */
export interface MoverResult {
  success: boolean;
  message: string;
  galponesAfectados: number;
  lotesAfectados: number;
}
