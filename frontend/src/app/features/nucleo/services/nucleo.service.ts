// app/features/nucleo/services/nucleo.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, shareReplay, map } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface NucleoDto {
  nucleoId:     string;
  granjaId:     number;
  nucleoNombre: string;
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

  /** Todos los núcleos */
  getAll(): Observable<NucleoDto[]> {
    return this.cache$ ??= this.http.get<NucleoDto[]>(this.baseUrl).pipe(shareReplay(1));
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
    return this.http.post<NucleoDto>(this.baseUrl, dto);
  }

  /** Actualizar existente */
  update(dto: UpdateNucleoDto): Observable<NucleoDto> {
    return this.http.put<NucleoDto>(
      `${this.baseUrl}/${encodeURIComponent(dto.nucleoId)}/${dto.granjaId}`,
      dto
    );
  }

  /** Eliminar */
  delete(nucleoId: string, granjaId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${encodeURIComponent(nucleoId)}/${granjaId}`);
  }
}
