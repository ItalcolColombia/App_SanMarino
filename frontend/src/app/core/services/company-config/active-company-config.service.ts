// src/app/core/services/company-config/active-company-config.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { catchError, distinctUntilChanged, map, shareReplay, tap } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { TokenStorageService } from '../../auth/token-storage.service';

/**
 * Flags de comportamiento de la EMPRESA ACTIVA (columnas tipadas en `companies`).
 *
 * Patrón multi-empresa del repo: nunca se detecta país ni nombre de empresa en el front;
 * el backend expone una columna booleana por comportamiento y el front sólo la lee.
 */
export interface CompanyFlags {
  /** Santa Reyes: la empresa maneja códigos ERP avícolas (bodega / C.O. / instalación / centro de costo). */
  manejaCodigosErpAvicola: boolean;
  /** Santa Reyes: los huevos se clasifican por ÍTEM del catálogo (Primera/Pnc) en vez de las 11 columnas fijas. */
  clasificacionHuevoPorItems: boolean;
  /** Santa Reyes: se permite trasladar aves entre etapas (Levante → Producción) registrando cohorte con la edad de origen. */
  permiteTrasladoAvesCrossEtapa: boolean;
  /**
   * La empresa captura la clasificación de huevos en el seguimiento diario de LEVANTE a partir de
   * la semana 14 de vida; al liquidar el levante el acumulado se arrastra al primer registro de
   * producción.
   */
  capturaHuevosEnLevante: boolean;
}

/** FAIL-CLOSED: si no hay empresa activa, falla el HTTP o el campo no viene → todo apagado. */
const FLAGS_APAGADOS: CompanyFlags = Object.freeze({
  manejaCodigosErpAvicola: false,
  clasificacionHuevoPorItems: false,
  permiteTrasladoAvesCrossEtapa: false,
  capturaHuevosEnLevante: false
});

/** TTL de la caché en memoria por empresa (5 minutos). */
const TTL_MS = 5 * 60 * 1000;

interface CacheEntry {
  flags: CompanyFlags;
  expiresAt: number;
}

/** Forma mínima que se consume del detalle de empresa (`GET /api/Company/{id}`). */
interface CompanyFlagsResponse {
  manejaCodigosErpAvicola?: boolean | null;
  clasificacionHuevoPorItems?: boolean | null;
  permiteTrasladoAvesCrossEtapa?: boolean | null;
  capturaHuevosEnLevante?: boolean | null;
}

@Injectable({ providedIn: 'root' })
export class ActiveCompanyConfigService {
  private readonly http = inject(HttpClient);
  private readonly storage = inject(TokenStorageService);
  private readonly baseUrl = `${environment.apiUrl}/Company`;

  /** Último valor conocido (arranca apagado: fail-closed hasta que el backend confirme). */
  private readonly flagsSubject = new BehaviorSubject<CompanyFlags>(FLAGS_APAGADOS);

  /** Flags de la empresa activa (se re-emite al cambiar de empresa o al resolver el GET). */
  readonly flags$: Observable<CompanyFlags> = this.flagsSubject.asObservable();

  /** Atajo: ¿la empresa activa maneja códigos ERP avícolas? */
  readonly manejaCodigosErpAvicola$: Observable<boolean> = this.flags$.pipe(
    map(f => f.manejaCodigosErpAvicola),
    distinctUntilChanged()
  );

  /** Atajo: ¿la empresa activa clasifica los huevos por ítems del catálogo? */
  readonly clasificacionHuevoPorItems$: Observable<boolean> = this.flags$.pipe(
    map(f => f.clasificacionHuevoPorItems),
    distinctUntilChanged()
  );

  /** Atajo: ¿la empresa activa permite traslados de aves entre etapas (Levante → Producción)? */
  readonly permiteTrasladoAvesCrossEtapa$: Observable<boolean> = this.flags$.pipe(
    map(f => f.permiteTrasladoAvesCrossEtapa),
    distinctUntilChanged()
  );

  /** Atajo: ¿la empresa activa captura huevos en el seguimiento diario de levante? */
  readonly capturaHuevosEnLevante$: Observable<boolean> = this.flags$.pipe(
    map(f => f.capturaHuevosEnLevante),
    distinctUntilChanged()
  );

  /** Caché en memoria por companyId (TTL 5 min). */
  private readonly cache = new Map<number, CacheEntry>();
  /** Peticiones en vuelo por companyId (evita N GET simultáneos desde varios formularios). */
  private readonly inFlight = new Map<number, Observable<CompanyFlags>>();
  /** Empresa activa observada en la última emisión de `session$`. */
  private currentCompanyId: number | null = null;

  constructor() {
    // Cambio de empresa activa (login, switch de empresa, logout) → invalidar y volver a fail-closed.
    this.storage.session$.subscribe(session => {
      const companyId = session?.activeCompanyId ?? null;
      if (companyId === this.currentCompanyId) return;
      this.currentCompanyId = companyId;
      this.cache.clear();
      this.inFlight.clear();
      this.flagsSubject.next(FLAGS_APAGADOS);
    });
  }

  /**
   * Flags de la empresa activa. Emite una sola vez y completa.
   * Usa caché (TTL 5 min) y comparte la petición en vuelo entre llamadores.
   * Ante cualquier error o campo ausente devuelve todos los flags en `false`.
   */
  getFlags(): Observable<CompanyFlags> {
    const companyId = this.storage.get()?.activeCompanyId ?? null;
    if (companyId == null) {
      this.publish(null, FLAGS_APAGADOS);
      return of(FLAGS_APAGADOS);
    }

    const cached = this.cache.get(companyId);
    if (cached && cached.expiresAt > Date.now()) {
      this.publish(companyId, cached.flags);
      return of(cached.flags);
    }

    const pending = this.inFlight.get(companyId);
    if (pending) return pending;

    const request$ = this.http.get<CompanyFlagsResponse>(`${this.baseUrl}/${companyId}`).pipe(
      map(dto => this.mapFlags(dto)),
      // Solo se cachean respuestas OK: un error no debe dejar los campos ocultos 5 minutos.
      tap(flags => this.cache.set(companyId, { flags, expiresAt: Date.now() + TTL_MS })),
      catchError(() => of(FLAGS_APAGADOS)),
      tap(flags => {
        this.inFlight.delete(companyId);
        this.publish(companyId, flags);
      }),
      shareReplay({ bufferSize: 1, refCount: false })
    );

    this.inFlight.set(companyId, request$);
    return request$;
  }

  /** Azúcar: sólo el flag de códigos ERP avícolas de la empresa activa. */
  manejaCodigosErpAvicola(): Observable<boolean> {
    return this.getFlags().pipe(map(f => f.manejaCodigosErpAvicola));
  }

  /** Azúcar: sólo el flag de clasificación de huevos por ítems de la empresa activa. */
  clasificacionHuevoPorItems(): Observable<boolean> {
    return this.getFlags().pipe(map(f => f.clasificacionHuevoPorItems));
  }

  /** Azúcar: sólo el flag de traslado de aves cross-etapa (Levante → Producción) de la empresa activa. */
  permiteTrasladoAvesCrossEtapa(): Observable<boolean> {
    return this.getFlags().pipe(map(f => f.permiteTrasladoAvesCrossEtapa));
  }

  /** Azúcar: sólo el flag de captura de huevos en levante de la empresa activa. */
  capturaHuevosEnLevante(): Observable<boolean> {
    return this.getFlags().pipe(map(f => f.capturaHuevosEnLevante));
  }

  /** Descarta la caché (p. ej. tras editar la empresa en configuración). */
  invalidate(): void {
    this.cache.clear();
    this.inFlight.clear();
  }

  /** Mapeo defensivo: cualquier valor distinto de `true` deja el flag apagado. */
  private mapFlags(dto: CompanyFlagsResponse | null | undefined): CompanyFlags {
    return {
      manejaCodigosErpAvicola: dto?.manejaCodigosErpAvicola === true,
      clasificacionHuevoPorItems: dto?.clasificacionHuevoPorItems === true,
      permiteTrasladoAvesCrossEtapa: dto?.permiteTrasladoAvesCrossEtapa === true,
      capturaHuevosEnLevante: dto?.capturaHuevosEnLevante === true
    };
  }

  /** Publica sólo si la empresa sigue siendo la activa (evita pisar tras un switch). */
  private publish(companyId: number | null, flags: CompanyFlags): void {
    if (companyId !== this.currentCompanyId) return;
    const actual = this.flagsSubject.value;
    if (
      actual.manejaCodigosErpAvicola === flags.manejaCodigosErpAvicola &&
      actual.clasificacionHuevoPorItems === flags.clasificacionHuevoPorItems &&
      actual.permiteTrasladoAvesCrossEtapa === flags.permiteTrasladoAvesCrossEtapa &&
      actual.capturaHuevosEnLevante === flags.capturaHuevosEnLevante
    ) return;
    this.flagsSubject.next(flags);
  }
}
