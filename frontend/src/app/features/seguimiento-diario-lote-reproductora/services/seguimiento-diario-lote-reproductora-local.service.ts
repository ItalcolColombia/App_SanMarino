import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { delay, map } from 'rxjs/operators';

export interface SeguimientoReproDto {
  id: number;
  fechaRegistro: string;  // ISO
  loteId: string;
  reproductoraId: string; // clave secundaria
  mortalidadHembras: number;
  mortalidadMachos: number;
  selH: number;
  selM: number;
  errorSexajeHembras: number;
  errorSexajeMachos: number;
  tipoAlimento: string;
  consumoKgHembras: number;
  observaciones?: string;
  kcalAlH?: number | null;
  protAlH?: number | null;
  kcalAveH?: number | null;
  protAveH?: number | null;
  ciclo: string; // Normal | Reforzado
}

export type CreateSeguimientoReproDto = Omit<SeguimientoReproDto, 'id'>;
export type UpdateSeguimientoReproDto = SeguimientoReproDto;

const STORAGE_KEY = 'sm_seg_diario_repro_v1';

@Injectable({ providedIn: 'root' })
export class SeguimientoDiarioLoteReproductoraLocalService {
  private readonly _all$ = new BehaviorSubject<SeguimientoReproDto[]>(this.load());
  readonly all$ = this._all$.asObservable();

  // Queries
  getAll(): Observable<SeguimientoReproDto[]> { return this.all$; }
  getByLoteYRepro(loteId: string, reproductoraId: string): Observable<SeguimientoReproDto[]> {
    return this.all$.pipe(map(list => list.filter(
      x => x.loteId === loteId && x.reproductoraId === reproductoraId
    )));
  }

  // CRUD
  create(dto: CreateSeguimientoReproDto): Observable<SeguimientoReproDto> {
    const next: SeguimientoReproDto = { ...dto, id: this.nextId() };
    const data = [...this._all$.value, next];
    this.persist(data);
    return of(next).pipe(delay(120));
  }
  update(dto: UpdateSeguimientoReproDto): Observable<SeguimientoReproDto> {
    const data = this._all$.value.map(x => x.id === dto.id ? { ...dto } : x);
    this.persist(data);
    return of(dto).pipe(delay(120));
  }
  delete(id: number): Observable<void> {
    const data = this._all$.value.filter(x => x.id !== id);
    this.persist(data);
    return of(void 0).pipe(delay(100));
  }

  // utils
  private nextId(): number {
    const max = this._all$.value.reduce((m, x) => x.id > m ? x.id : m, 0);
    return max + 1;
  }
  private persist(data: SeguimientoReproDto[]) {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(data));
    this._all$.next(data);
  }
  private load(): SeguimientoReproDto[] {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      return raw ? JSON.parse(raw) as SeguimientoReproDto[] : [];
    } catch { return []; }
  }
}
