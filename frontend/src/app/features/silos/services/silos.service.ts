// src/app/features/silos/services/silos.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

// ─── Contratos (espejo de Application/DTOs/SiloDtos.cs) ──────────────────────

/** Entrada de la lista maestra de silos de la empresa (1..100). */
export interface SiloCatalogoDto {
  id: number;
  companyId: number;
  numero: number;
  nombre: string;
  descripcion: string | null;
  activo: boolean;
  /** Cuántas granjas tienen asignado este silo (para no borrarlo a ciegas). */
  granjasAsignadas: number;
}

export interface CreateSiloCatalogoDto {
  numero: number;
  nombre?: string | null;
  descripcion?: string | null;
  activo?: boolean;
}

export interface UpdateSiloCatalogoDto {
  nombre?: string | null;
  descripcion?: string | null;
  activo?: boolean | null;
}

export interface GenerarRangoSilosDto {
  desde: number;
  hasta: number;
  /** `{n}` se reemplaza por el número. Default "Silo {n}". */
  patronNombre?: string | null;
}

export interface GenerarRangoSilosResultDto {
  creados: number;
  omitidos: number;
  silos: SiloCatalogoDto[];
}

/** Silo o bodega de una granja: la ubicación REAL del inventario cuando el flag está activo. */
export interface FarmSiloDto {
  id: number;
  companyId: number;
  granjaId: number;
  granjaNombre: string | null;
  siloCatalogoId: number | null;
  numero: number | null;
  nombre: string;
  /** `Silo` | `Bodega`. */
  tipo: string;
  codigoErpUbicacion: string | null;
  descripcion: string | null;
  centroOperacion: string | null;
  codigoBodega: string | null;
  activo: boolean;
  galponesAsignados: number;
  lotesAsignados: number;
}

export interface CreateFarmSiloDto {
  granjaId: number;
  tipo: string;
  siloCatalogoId?: number | null;
  nombre?: string | null;
  codigoErpUbicacion?: string | null;
  descripcion?: string | null;
  centroOperacion?: string | null;
  codigoBodega?: string | null;
  activo?: boolean;
}

export interface UpdateFarmSiloDto {
  nombre?: string | null;
  codigoErpUbicacion?: string | null;
  descripcion?: string | null;
  centroOperacion?: string | null;
  codigoBodega?: string | null;
  activo?: boolean | null;
}

export interface AsignarSilosGranjaDto {
  granjaId: number;
  siloCatalogoIds: number[];
  crearBodega?: boolean;
  nombreBodega?: string | null;
}

export interface GalponSiloDto {
  id: number;
  granjaId: number;
  nucleoId: string;
  galponId: string;
  farmSiloId: number;
  siloNombre: string;
  siloTipo: string;
  siloNumero: number | null;
  activo: boolean;
}

export interface LoteSiloDto {
  id: number;
  loteId: number;
  farmSiloId: number;
  siloNombre: string;
  siloTipo: string;
  siloNumero: number | null;
  activo: boolean;
}

/** Reemplaza el conjunto completo (SET): lo que no venga se quita. */
export interface AsignarSilosDto {
  farmSiloIds: number[];
}

// ─── Servicio ────────────────────────────────────────────────────────────────

/**
 * Silos y bodegas: lista maestra de la empresa + asignación a granja, galpón y lote.
 * Solo tiene sentido en empresas con `manejaInventarioPorSilo` (las pantallas están gateadas).
 */
@Injectable({ providedIn: 'root' })
export class SilosService {
  private readonly http = inject(HttpClient);
  private readonly catalogoUrl = `${environment.apiUrl}/SiloCatalogo`;
  private readonly farmSiloUrl = `${environment.apiUrl}/FarmSilo`;
  private readonly galponSiloUrl = `${environment.apiUrl}/GalponSilo`;
  private readonly loteSiloUrl = `${environment.apiUrl}/LoteSilo`;

  // ── Lista maestra ──────────────────────────────────────────────────────────

  getCatalogo(soloActivos = false): Observable<SiloCatalogoDto[]> {
    const params = new HttpParams().set('soloActivos', soloActivos);
    return this.http.get<SiloCatalogoDto[]>(this.catalogoUrl, { params });
  }

  crearCatalogo(dto: CreateSiloCatalogoDto): Observable<SiloCatalogoDto> {
    return this.http.post<SiloCatalogoDto>(this.catalogoUrl, dto);
  }

  actualizarCatalogo(id: number, dto: UpdateSiloCatalogoDto): Observable<SiloCatalogoDto> {
    return this.http.put<SiloCatalogoDto>(`${this.catalogoUrl}/${id}`, dto);
  }

  eliminarCatalogo(id: number): Observable<void> {
    return this.http.delete<void>(`${this.catalogoUrl}/${id}`);
  }

  generarRango(dto: GenerarRangoSilosDto): Observable<GenerarRangoSilosResultDto> {
    return this.http.post<GenerarRangoSilosResultDto>(`${this.catalogoUrl}/generar-rango`, dto);
  }

  // ── Silos de una granja ────────────────────────────────────────────────────

  getSilosDeGranja(granjaId: number, soloActivos = false): Observable<FarmSiloDto[]> {
    const params = new HttpParams().set('granjaId', granjaId).set('soloActivos', soloActivos);
    return this.http.get<FarmSiloDto[]>(this.farmSiloUrl, { params });
  }

  crearSiloGranja(dto: CreateFarmSiloDto): Observable<FarmSiloDto> {
    return this.http.post<FarmSiloDto>(this.farmSiloUrl, dto);
  }

  actualizarSiloGranja(id: number, dto: UpdateFarmSiloDto): Observable<FarmSiloDto> {
    return this.http.put<FarmSiloDto>(`${this.farmSiloUrl}/${id}`, dto);
  }

  eliminarSiloGranja(id: number): Observable<void> {
    return this.http.delete<void>(`${this.farmSiloUrl}/${id}`);
  }

  /** Fija de una vez qué silos del catálogo tiene la granja (SET). */
  asignarSilosAGranja(dto: AsignarSilosGranjaDto): Observable<FarmSiloDto[]> {
    return this.http.post<FarmSiloDto[]>(`${this.farmSiloUrl}/asignar-desde-catalogo`, dto);
  }

  // ── Galpón ↔ silo ──────────────────────────────────────────────────────────

  getSilosDeGalpon(granjaId: number, nucleoId: string, galponId: string): Observable<GalponSiloDto[]> {
    const params = new HttpParams()
      .set('granjaId', granjaId)
      .set('nucleoId', nucleoId)
      .set('galponId', galponId);
    return this.http.get<GalponSiloDto[]>(this.galponSiloUrl, { params });
  }

  asignarSilosAGalpon(
    granjaId: number,
    nucleoId: string,
    galponId: string,
    dto: AsignarSilosDto
  ): Observable<GalponSiloDto[]> {
    const params = new HttpParams()
      .set('granjaId', granjaId)
      .set('nucleoId', nucleoId)
      .set('galponId', galponId);
    return this.http.put<GalponSiloDto[]>(this.galponSiloUrl, dto, { params });
  }

  // ── Lote ↔ silo ────────────────────────────────────────────────────────────

  getSilosDeLote(loteId: number): Observable<LoteSiloDto[]> {
    return this.http.get<LoteSiloDto[]>(`${this.loteSiloUrl}/${loteId}`);
  }

  /** Silos elegibles para el lote (los de su galpón; si no tiene, los de la granja). */
  getSilosDisponiblesDeLote(loteId: number): Observable<FarmSiloDto[]> {
    return this.http.get<FarmSiloDto[]>(`${this.loteSiloUrl}/${loteId}/disponibles`);
  }

  asignarSilosALote(loteId: number, dto: AsignarSilosDto): Observable<LoteSiloDto[]> {
    return this.http.put<LoteSiloDto[]>(`${this.loteSiloUrl}/${loteId}`, dto);
  }
}
