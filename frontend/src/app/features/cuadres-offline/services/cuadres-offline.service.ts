import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import type { CuadrePendiente } from '../models/cuadre-pendiente.model';

export type { CuadrePendiente } from '../models/cuadre-pendiente.model';

/**
 * Bandeja de cuadre de las capturas offline (F7 del push).
 *
 * ## Lo que este servicio NO hace, y no es un olvido
 *
 * No repone kilos. `resolver` **sólo marca visto** — es una decisión de negocio explícita del
 * backend: reponer desde acá sería una segunda fórmula para el mismo número que ya calcula el
 * ingreso normal de inventario, y ese es justamente el defecto que el repo tiene prohibido repetir.
 * El faltante se corrige cargando el ingreso por el módulo de inventario, como siempre.
 *
 * Tampoco manda `companyId` ni filtra por empresa: el servidor resuelve el alcance fail-closed
 * contra la sesión (`ListarCuadresPendientesAsync`). Mandar el id desde el front abriría la puerta a
 * pedir el de otra empresa.
 */
@Injectable({ providedIn: 'root' })
export class CuadresOfflineService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/Sync/cuadres`;

  /** Filas `requiere_cuadre` de la empresa activa que nadie marcó como vistas. */
  listar(): Observable<CuadrePendiente[]> {
    return this.http.get<CuadrePendiente[]>(this.base);
  }

  /**
   * Marca una fila como revisada. Devuelve 204; un 404 significa que ya no estaba pendiente
   * (alguien más la resolvió, o es de otra empresa) — la pantalla lo trata como «ya no está», no
   * como un error rojo.
   */
  resolver(id: number): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/resolver`, {});
  }
}
