// features/sincronizacion-panama/services/sincronizacion-panama.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  PanamaConexion,
  ProbarConexionResult,
  PanamaCliente,
  PanamaGranja,
  SincronizarPanamaRequest,
  ResultadoSincronizacionDto,
  SincronizacionHistorialPagedDto,
  SincronizacionHistorialDetalleDto
} from '../models/sincronizacion-panama.model';

/**
 * Servicio HTTP del puente de integración Panamá (ZooPanamaPollo → Pollo Engorde).
 * Apunta al controller `PuentePanamaController` (base `api/sincronizacion-panama`). Todos los
 * endpoints son POST y el JWT lo adjunta `AuthInterceptor` (no se agregan headers de auth acá).
 * La empresa activa viaja por header (X-Active-Company) del interceptor.
 */
@Injectable({ providedIn: 'root' })
export class SincronizacionPanamaService {
  private readonly base = `${environment.apiUrl}/sincronizacion-panama`;
  private readonly http = inject(HttpClient);

  /** Prueba las credenciales/URL del origen; devuelve ok + expiración del token o el error. */
  probarConexion(origen: PanamaConexion): Observable<ProbarConexionResult> {
    return this.http.post<ProbarConexionResult>(`${this.base}/probar-conexion`, origen);
  }

  /** Lista los clientes del origen para poblar el dropdown de filtros. */
  clientes(origen: PanamaConexion): Observable<PanamaCliente[]> {
    return this.http.post<PanamaCliente[]>(`${this.base}/clientes`, origen);
  }

  /** Lista las granjas del origen; si se pasa `clienteIdOrigen` filtra por ese cliente. */
  granjas(origen: PanamaConexion, clienteIdOrigen?: number | null): Observable<PanamaGranja[]> {
    let params = new HttpParams();
    if (clienteIdOrigen != null) params = params.set('clienteIdOrigen', String(clienteIdOrigen));
    return this.http.post<PanamaGranja[]>(`${this.base}/granjas`, origen, { params });
  }

  /** Previsualiza (dry-run): el backend fuerza `dryRun=true`, no inserta nada. */
  previsualizar(req: SincronizarPanamaRequest): Observable<ResultadoSincronizacionDto> {
    return this.http.post<ResultadoSincronizacionDto>(`${this.base}/previsualizar`, req);
  }

  /** Sincroniza (real): inserta/actualiza en la empresa activa. */
  sincronizar(req: SincronizarPanamaRequest): Observable<ResultadoSincronizacionDto> {
    return this.http.post<ResultadoSincronizacionDto>(`${this.base}/sincronizar`, req);
  }

  /** Historial paginado de corridas (reales y validaciones) de la empresa activa. */
  historial(page = 1, pageSize = 10, incluirValidaciones = true): Observable<SincronizacionHistorialPagedDto> {
    const params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(pageSize))
      .set('incluirValidaciones', String(incluirValidaciones));
    return this.http.get<SincronizacionHistorialPagedDto>(`${this.base}/historial`, { params });
  }

  /** Detalle completo de una corrida del historial (mismos contadores/mensajes que la previsualización). */
  historialDetalle(id: number): Observable<SincronizacionHistorialDetalleDto> {
    return this.http.get<SincronizacionHistorialDetalleDto>(`${this.base}/historial/${id}`);
  }
}
