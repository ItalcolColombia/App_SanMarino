import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { TokenStorageService } from './token-storage.service';

@Injectable({ providedIn: 'root' })
export class UserPermissionService {
  private readonly storage = inject(TokenStorageService);

  /** Emits the current list of permission keys whenever the session changes. */
  readonly permissions$ = this.storage.session$.pipe(
    map(s => s?.user.permisos ?? [])
  );

  /** Synchronous check — reads directly from storage. */
  has(key: string): boolean {
    return this.storage.get()?.user.permisos?.includes(key) ?? false;
  }

  /** True if the user has at least one of the given keys. */
  hasAny(keys: string[]): boolean {
    if (keys.length === 0) return true;
    const permisos = this.storage.get()?.user.permisos ?? [];
    return keys.some(k => permisos.includes(k));
  }

  /** True if the user has every one of the given keys. */
  hasAll(keys: string[]): boolean {
    if (keys.length === 0) return true;
    const permisos = this.storage.get()?.user.permisos ?? [];
    return keys.every(k => permisos.includes(k));
  }

  /** Reactive version of `has` — emits whenever the session changes. */
  has$(key: string): Observable<boolean> {
    return this.permissions$.pipe(map(p => p.includes(key)));
  }

  /** Reactive version of `hasAny`. */
  hasAny$(keys: string[]): Observable<boolean> {
    if (keys.length === 0) return this.permissions$.pipe(map(() => true));
    return this.permissions$.pipe(map(p => keys.some(k => p.includes(k))));
  }

  /** Reactive version of `hasAll`. */
  hasAll$(keys: string[]): Observable<boolean> {
    if (keys.length === 0) return this.permissions$.pipe(map(() => true));
    return this.permissions$.pipe(map(p => keys.every(k => p.includes(k))));
  }
}
