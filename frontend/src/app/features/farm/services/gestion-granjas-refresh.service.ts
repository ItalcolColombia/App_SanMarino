// Bus de refresco entre las tabs de "Gestión de Granjas" (Granjas / Núcleos / Galpones).
// Los tres listados se renderizan a la vez (via [hidden]) y cargan una sola vez en ngOnInit,
// por lo que no se enteran de los cambios hechos en otra tab. Este bus permite que cada listado
// emita cuando hace un CRUD y que los demás reaccionen (recarguen) sin recargar la app.
import { Injectable } from '@angular/core';
import { Observable, Subject } from 'rxjs';

/** Origen del cambio: qué entidad se creó/editó/eliminó. */
export type GranjaChangeSource = 'farm' | 'nucleo' | 'galpon';

@Injectable({ providedIn: 'root' })
export class GestionGranjasRefreshService {
  private readonly _changes$ = new Subject<GranjaChangeSource>();

  /** Stream de cambios; cada listado se suscribe y decide si le afecta. */
  readonly changes$: Observable<GranjaChangeSource> = this._changes$.asObservable();

  /** Notifica que hubo un cambio (create/update/delete) sobre la entidad indicada. */
  notify(source: GranjaChangeSource): void {
    this._changes$.next(source);
  }
}
