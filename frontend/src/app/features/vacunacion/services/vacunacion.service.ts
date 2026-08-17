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
  VacunacionPendienteDto,
  LineaProductiva,
} from '../models/vacunacion.model';
import {
  VacunacionPlantillaDto,
  VacunacionPlantillaDetalleDto,
  VacunacionPlantillaItemDto,
  VacunacionPlantillaCreateRequest,
  VacunacionPlantillaUpdateRequest,
  VacunacionPlantillaItemCreateRequest,
  VacunacionPlantillaItemUpdateRequest,
  VacunacionPlantillaEfectivaDto,
} from '../models/vacunacion-plantilla.model';
import {
  VacunacionMaterializacionLoteDto,
  VacunacionMaterializacionMasivaDto,
} from '../models/vacunacion-materializador.model';

/** Vida de la caché de filter-data: navegar entre las páginas del módulo no re-descarga;
 *  un cambio de empresa/granja hecho en otro módulo se ve como muy tarde a los 5 minutos. */
const FILTER_DATA_TTL_MS = 5 * 60 * 1000;

@Injectable({ providedIn: 'root' })
export class VacunacionService {
  private readonly cronogramaBase = `${environment.apiUrl}/VacunacionCronograma`;
  private readonly registroBase = `${environment.apiUrl}/VacunacionRegistro`;
  private readonly reportesBase = `${environment.apiUrl}/VacunacionReportes`;
  private readonly plantillaBase = `${environment.apiUrl}/VacunacionPlantilla`;
  private readonly materializadorBase = `${environment.apiUrl}/VacunacionMaterializador`;

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

  /** Bandeja "hoy me toca": lo que falta registrar en todos los lotes vivos que el usuario ve.
   *  Sin caché a propósito — es la pantalla de "qué hay AHORA", y el panel del inicio se monta
   *  una vez por visita. */
  getPendientes(diasHorizonte = 7): Observable<VacunacionPendienteDto[]> {
    return this.http.get<VacunacionPendienteDto[]>(`${this.registroBase}/pendientes`, {
      params: { diasHorizonte },
    });
  }

  getCumplimiento(req: VacunacionCumplimientoFiltroRequest): Observable<VacunacionCumplimientoLoteDto[]> {
    return this.http.post<VacunacionCumplimientoLoteDto[]>(`${this.reportesBase}/cumplimiento`, req);
  }

  /** Detalle ítem a ítem del reporte (una fila por vacuna programada). */
  getCumplimientoDetalle(req: VacunacionCumplimientoFiltroRequest): Observable<VacunacionCumplimientoDetalleDto[]> {
    return this.http.post<VacunacionCumplimientoDetalleDto[]>(`${this.reportesBase}/detalle`, req);
  }

  // ─── Plantillas del plan (W1.3/W1.4) ──────────────────────────────────────
  // Sin caché a propósito: es una pantalla de administración y el usuario acaba de escribir lo que
  // está mirando. Cachearla mostraría su propio cambio con retraso.

  getPlantillas(lineaProductiva?: LineaProductiva | null, soloActivas = false): Observable<VacunacionPlantillaDto[]> {
    const params: Record<string, string> = {};
    if (lineaProductiva) params['lineaProductiva'] = lineaProductiva;
    if (soloActivas) params['soloActivas'] = 'true';
    return this.http.get<VacunacionPlantillaDto[]>(this.plantillaBase, { params });
  }

  getPlantilla(id: number): Observable<VacunacionPlantillaDetalleDto> {
    return this.http.get<VacunacionPlantillaDetalleDto>(`${this.plantillaBase}/${id}`);
  }

  crearPlantilla(req: VacunacionPlantillaCreateRequest): Observable<VacunacionPlantillaDetalleDto> {
    return this.http.post<VacunacionPlantillaDetalleDto>(this.plantillaBase, req);
  }

  actualizarPlantilla(id: number, req: VacunacionPlantillaUpdateRequest): Observable<VacunacionPlantillaDetalleDto> {
    return this.http.put<VacunacionPlantillaDetalleDto>(`${this.plantillaBase}/${id}`, req);
  }

  eliminarPlantilla(id: number): Observable<void> {
    return this.http.delete<void>(`${this.plantillaBase}/${id}`);
  }

  crearItemPlantilla(plantillaId: number, req: VacunacionPlantillaItemCreateRequest): Observable<VacunacionPlantillaItemDto> {
    return this.http.post<VacunacionPlantillaItemDto>(`${this.plantillaBase}/${plantillaId}/items`, req);
  }

  actualizarItemPlantilla(
    plantillaId: number,
    itemId: number,
    req: VacunacionPlantillaItemUpdateRequest
  ): Observable<VacunacionPlantillaItemDto> {
    return this.http.put<VacunacionPlantillaItemDto>(`${this.plantillaBase}/${plantillaId}/items/${itemId}`, req);
  }

  eliminarItemPlantilla(plantillaId: number, itemId: number): Observable<void> {
    return this.http.delete<void>(`${this.plantillaBase}/${plantillaId}/items/${itemId}`);
  }

  /** Vista previa: qué plantilla le tocaría al lote y por qué. No escribe cronograma. */
  getPlantillaEfectiva(lineaProductiva: LineaProductiva, loteId: number): Observable<VacunacionPlantillaEfectivaDto> {
    return this.http.get<VacunacionPlantillaEfectivaDto>(`${this.plantillaBase}/efectiva`, {
      params: { lineaProductiva, loteId },
    });
  }

  // ─── Materializador: del plan al cronograma (W2) ───────────────────────────
  //
  // Cada preview y su aplicar devuelven el MISMO informe: el backend los calcula con la misma función
  // pura, así que lo que la pantalla muestra antes de confirmar es lo que se escribe. Aplicar es
  // idempotente y nunca borra una fila del cronograma.

  /** Qué pasaría con el cronograma de un lote. No escribe. */
  previewMaterializacionLote(
    lineaProductiva: LineaProductiva,
    loteId: number
  ): Observable<VacunacionMaterializacionLoteDto> {
    return this.http.get<VacunacionMaterializacionLoteDto>(`${this.materializadorBase}/preview`, {
      params: { lineaProductiva, loteId },
    });
  }

  /** Aplica el plan a un lote. Correrlo de nuevo no escribe nada. */
  aplicarMaterializacionLote(
    lineaProductiva: LineaProductiva,
    loteId: number
  ): Observable<VacunacionMaterializacionLoteDto> {
    return this.http.post<VacunacionMaterializacionLoteDto>(`${this.materializadorBase}/lote`, {
      lineaProductiva,
      loteId,
    });
  }

  /** Qué pasaría con todos los lotes abiertos a los que hoy les toca esta plantilla. No escribe. */
  previewMaterializacionPlantilla(plantillaId: number): Observable<VacunacionMaterializacionMasivaDto> {
    return this.http.get<VacunacionMaterializacionMasivaDto>(`${this.materializadorBase}/preview-masivo`, {
      params: { plantillaId },
    });
  }

  /** Aplica la plantilla a sus lotes, uno por transacción: el que falle no arrastra a los otros. */
  aplicarMaterializacionPlantilla(plantillaId: number): Observable<VacunacionMaterializacionMasivaDto> {
    return this.http.post<VacunacionMaterializacionMasivaDto>(
      `${this.materializadorBase}/plantilla/${plantillaId}/aplicar`,
      {}
    );
  }
}
