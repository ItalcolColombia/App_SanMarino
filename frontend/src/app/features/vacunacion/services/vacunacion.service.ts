// src/app/features/vacunacion/services/vacunacion.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, shareReplay } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import {
  VacunacionFilterDataDto,
  VacunacionCronogramaItemDto,
  VacunacionCronogramaItemCreateRequest,
  VacunacionCronogramaItemUpdateRequest,
  VacunacionRegistrarAplicadoRequest,
  VacunacionRegistrarNoAplicadoRequest,
  VacunacionCumplimientoFiltroRequest,
  VacunacionCumplimientoLoteDto,
  VacunacionCumplimientoDetalleDto,
  LineaProductiva,
} from '../models/vacunacion.model';

/** Vida de la caché de filter-data: navegar entre las páginas del módulo no re-descarga;
 *  un cambio de empresa/granja hecho en otro módulo se ve como muy tarde a los 5 minutos. */
const FILTER_DATA_TTL_MS = 5 * 60 * 1000;

@Injectable({ providedIn: 'root' })
export class VacunacionService {
  private readonly cronogramaBase = `${environment.apiUrl}/VacunacionCronograma`;
  private readonly registroBase = `${environment.apiUrl}/VacunacionRegistro`;
  private readonly reportesBase = `${environment.apiUrl}/VacunacionReportes`;

  private filterData$: Observable<VacunacionFilterDataDto> | null = null;
  private filterDataTs = 0;

  constructor(private http: HttpClient) {}

  /** Combos del módulo (granjas/lotes/vacunas/usuarios). Cacheado con shareReplay: las 3 páginas
   *  comparten UNA descarga. Un error limpia la caché para que el próximo intento re-consulte. */
  getFilterData(): Observable<VacunacionFilterDataDto> {
    const vencida = Date.now() - this.filterDataTs > FILTER_DATA_TTL_MS;
    if (!this.filterData$ || vencida) {
      this.filterDataTs = Date.now();
      this.filterData$ = this.http
        .get<VacunacionFilterDataDto>(`${this.cronogramaBase}/filter-data`)
        .pipe(
          catchError((err) => {
            this.filterData$ = null;
            return throwError(() => err);
          }),
          shareReplay({ bufferSize: 1, refCount: false })
        );
    }
    return this.filterData$;
  }

  /** Invalida la caché y vuelve a consultar (p. ej. botón "actualizar" o tras crear lotes/vacunas). */
  refrescarFilterData(): Observable<VacunacionFilterDataDto> {
    this.filterData$ = null;
    return this.getFilterData();
  }

  getCronogramaLote(lineaProductiva: LineaProductiva, loteId: number): Observable<VacunacionCronogramaItemDto[]> {
    return this.http.get<VacunacionCronogramaItemDto[]>(`${this.cronogramaBase}/por-lote`, {
      params: { lineaProductiva, loteId },
    });
  }

  crearItem(req: VacunacionCronogramaItemCreateRequest): Observable<VacunacionCronogramaItemDto> {
    return this.http.post<VacunacionCronogramaItemDto>(this.cronogramaBase, req);
  }

  actualizarItem(id: number, req: VacunacionCronogramaItemUpdateRequest): Observable<VacunacionCronogramaItemDto> {
    return this.http.put<VacunacionCronogramaItemDto>(`${this.cronogramaBase}/${id}`, req);
  }

  eliminarItem(id: number): Observable<void> {
    return this.http.delete<void>(`${this.cronogramaBase}/${id}`);
  }

  registrarAplicado(cronogramaItemId: number, req: VacunacionRegistrarAplicadoRequest): Observable<VacunacionCronogramaItemDto> {
    return this.http.post<VacunacionCronogramaItemDto>(`${this.registroBase}/${cronogramaItemId}/aplicar`, req);
  }

  registrarNoAplicado(cronogramaItemId: number, req: VacunacionRegistrarNoAplicadoRequest): Observable<VacunacionCronogramaItemDto> {
    return this.http.post<VacunacionCronogramaItemDto>(`${this.registroBase}/${cronogramaItemId}/no-aplicar`, req);
  }

  getCumplimiento(req: VacunacionCumplimientoFiltroRequest): Observable<VacunacionCumplimientoLoteDto[]> {
    return this.http.post<VacunacionCumplimientoLoteDto[]>(`${this.reportesBase}/cumplimiento`, req);
  }

  /** Detalle ítem a ítem del reporte (una fila por vacuna programada). */
  getCumplimientoDetalle(req: VacunacionCumplimientoFiltroRequest): Observable<VacunacionCumplimientoDetalleDto[]> {
    return this.http.post<VacunacionCumplimientoDetalleDto[]>(`${this.reportesBase}/detalle`, req);
  }
}
