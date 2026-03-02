import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

const BASE = `${environment.apiUrl}/mapas`;

export interface EjecutarMapaRequest {
  fechaDesde?: string | null;
  fechaHasta?: string | null;
  granjaIds?: number[] | null;
  tipoDato?: string | null;
  formatoExport?: string;
}

export interface EjecutarMapaResponse {
  ejecucionId: number;
  estado: string;
  mensaje?: string | null;
}

export interface MapaEjecucionEstadoDto {
  id: number;
  mapaId: number;
  estado: string;
  mensajeError?: string | null;
  mensajeEstado?: string | null;
  pasoActual?: number | null;
  totalPasos?: number | null;
  tipoArchivo?: string | null;
  fechaEjecucion: string;
  puedeDescargar: boolean;
}

export interface MapaEjecucionHistorialDto {
  id: number;
  mapaId: number;
  estado: string;
  mensajeError?: string | null;
  fechaEjecucion: string;
  puedeDescargar: boolean;
  tipoArchivo?: string | null;
}

export interface MapaListDto {
  id: number;
  nombre: string;
  descripcion: string | null;
  codigoPlantilla: string | null;
  isActive: boolean;
  paisId: number | null;
  createdAt: string;
  totalEjecuciones: number;
  ultimaEjecucionAt: string | null;
}

export interface MapaPasoDto {
  id?: number;
  mapaId?: number;
  orden: number;
  tipo: string;
  nombreEtiqueta?: string | null;
  scriptSql?: string | null;
  opciones?: string | null;
}

export interface MapaDetailDto {
  id: number;
  nombre: string;
  descripcion: string | null;
  codigoPlantilla: string | null;
  isActive: boolean;
  companyId: number;
  paisId: number | null;
  createdAt: string;
  pasos: MapaPasoDto[];
  totalEjecuciones: number;
}

export interface CreateMapaDto {
  nombre: string;
  descripcion?: string | null;
  codigoPlantilla?: string | null;
  paisId?: number | null;
  isActive?: boolean;
}

export interface UpdateMapaDto {
  nombre: string;
  descripcion?: string | null;
  codigoPlantilla?: string | null;
  paisId?: number | null;
  isActive?: boolean;
}

/** Opciones de plantilla para crear/editar mapa */
export const MAPA_PLANTILLAS: { value: string; label: string }[] = [
  { value: '', label: 'Sin plantilla' },
  { value: 'granjas_huevos_alimento', label: 'Granjas, huevos y alimento' },
  { value: 'entrada_ciesa', label: 'Entrada CIESA (encabezado documento)' }
];

@Injectable({ providedIn: 'root' })
export class MapasService {
  constructor(private http: HttpClient) {}

  getAll(): Observable<MapaListDto[]> {
    return this.http.get<MapaListDto[]>(BASE);
  }

  getById(id: number): Observable<MapaDetailDto> {
    return this.http.get<MapaDetailDto>(`${BASE}/${id}`);
  }

  create(dto: CreateMapaDto): Observable<MapaDetailDto> {
    return this.http.post<MapaDetailDto>(BASE, dto);
  }

  update(id: number, dto: UpdateMapaDto): Observable<MapaDetailDto> {
    return this.http.put<MapaDetailDto>(`${BASE}/${id}`, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${BASE}/${id}`);
  }

  savePasos(mapaId: number, pasos: MapaPasoDto[]): Observable<void> {
    return this.http.put<void>(`${BASE}/${mapaId}/pasos`, pasos);
  }

  ejecutar(mapaId: number, request: EjecutarMapaRequest): Observable<EjecutarMapaResponse> {
    return this.http.post<EjecutarMapaResponse>(`${BASE}/${mapaId}/ejecutar`, request ?? {});
  }

  getEjecucionEstado(ejecucionId: number): Observable<MapaEjecucionEstadoDto> {
    return this.http.get<MapaEjecucionEstadoDto>(`${BASE}/ejecuciones/${ejecucionId}`);
  }

  getEjecucionesByMapa(mapaId: number, limit = 20): Observable<MapaEjecucionHistorialDto[]> {
    return this.http.get<MapaEjecucionHistorialDto[]>(`${BASE}/${mapaId}/ejecuciones`, { params: { limit: String(limit) } });
  }

  descargarEjecucion(ejecucionId: number): Observable<{ blob: Blob; fileName: string }> {
    return this.http.get(`${BASE}/ejecuciones/${ejecucionId}/descargar`, {
      responseType: 'blob',
      observe: 'response'
    }).pipe(
      map(res => {
        const blob = res.body!;
        let fileName = `mapa_ejecucion_${ejecucionId}.xlsx`;
        const disp = res.headers.get('Content-Disposition');
        if (disp) {
          const match = /filename\*?=(?:UTF-8'')?"?([^";\n]+)"?/i.exec(disp) || /filename="?([^";\n]+)"?/i.exec(disp);
          if (match?.[1]) fileName = match[1].trim();
        }
        return { blob, fileName };
      })
    );
  }
}
