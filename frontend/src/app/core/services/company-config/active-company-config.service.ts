// src/app/core/services/company-config/active-company-config.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { catchError, distinctUntilChanged, map, shareReplay, tap } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { TokenStorageService } from '../../auth/token-storage.service';

/**
 * Perfil de guía genética de la empresa (`companies.guia_genetica_perfil`).
 *
 * - `sanmarino`: la tabla ANCHA compartida (`guia_genetica_sanmarino_colombia`) — pantalla
 *   `/config/guia-genetica`. Es el **default neutro**: toda empresa nace acá.
 * - `reducida`: la tabla PLANA de 3 métricas (`guia_genetica_santa_reyes`) — pantalla
 *   `/config/guia-genetica-santa-reyes`.
 *
 * No es un booleano porque no es una capacidad que se enciende, sino **cuál de los dos modelos de
 * datos** usa la empresa; y una tercera empresa con un tercer modelo sería un valor más, no un flag
 * más. El backend lo resuelve con `GuiaGeneticaPerfilCalculos`.
 */
export type GuiaGeneticaPerfil = 'sanmarino' | 'reducida';

/** Valor con el que se responde cuando no hay dato: el default neutro (ver `GuiaGeneticaPerfil`). */
export const GUIA_GENETICA_PERFIL_DEFECTO: GuiaGeneticaPerfil = 'sanmarino';

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
  /**
   * El peso báscula (bruto/tara) de las VENTAS de pollo engorde llega al día siguiente: la venta se
   * registra sin peso y queda Pendiente; el peso se carga al confirmarla (modal de registro de peso),
   * que re-prorratea por lote y completa el despacho en la misma transacción.
   */
  ventaEngordePesoDiferido: boolean;
  /**
   * La HORA de llegada de las aves decide el primer día con registro del lote (engorde y
   * reproductora): desde las 13:00 el primer consumo pasa al día siguiente del encasetamiento.
   * La fecha de encaset y la edad no cambian — solo se corre el primer día con registro.
   */
  primerRegistroSegunHoraLlegada: boolean;
  /**
   * Los lotes de pollo engorde se PROGRAMAN: el catálogo de lotes base (asignado por granja) es la
   * lista de lotes a encasetar, el nombre del lote sale obligatoriamente de esa lista (numerado por
   * corrida dentro del galpón) y un gasto de inventario puede cargarse contra un lote programado que
   * todavía no está activo (desinsectación previa al encaset).
   */
  programacionLotesEngorde: boolean;
  /**
   * El nombre del lote lleva el sufijo de corrida desde la PRIMERA apertura ("96 - 1", Panamá).
   * `false` = el nombre es el del lote base tal cual ("2603", Ecuador: la corrida ya está en el
   * nombre del base) y el sufijo sólo aparece desde la segunda apertura en el mismo galpón.
   */
  nombreLoteIncluyeCorrida: boolean;
  /**
   * El inventario se ubica en SILOS y BODEGAS de la granja, no en el galpón: ingreso, traslado y
   * consumo exigen silo, y el galpón pasa a ser el filtro que despliega qué silos elegir.
   * Habilita además las pantallas de asignación de silos (lista maestra, granja, galpón y lote).
   */
  manejaInventarioPorSilo: boolean;
  /**
   * Los seguimientos diarios exigen DOBLE VALIDACION: al guardar, el registro queda pendiente y el
   * alimento y las aves se SEPARAN (reservan) en vez de descontarse; el descuento real ocurre al
   * validar. Habilita la columna Estado, el boton Validar, el semaforo de retraso y el modal de
   * pendientes al entrar al lote.
   */
  requiereValidacionSeguimientoDiario: boolean;
  /**
   * Santa Reyes: la etapa del ciclo de vida del ave (alistamiento/levante/levante en producción/
   * postura) se calcula por semana de vida y por raza, en vez de los cortes fijos 26-33/34-50/&gt;50.
   */
  semanasCicloPosturaPorRaza: boolean;
  /** Santa Reyes: el seguimiento diario no captura consumo de alimento de Machos (no se manejan en postura). */
  consumoAlimentoSoloHembras: boolean;
  /**
   * Santa Reyes: oculta la columna Machos en mortalidad/selección/peso/uniformidad/traslados/ventas
   * y retira el error de sexaje del registro diario. Solo UI — el dato sigue existiendo en el modelo
   * (lo consumen saldos e históricos de otras empresas).
   */
  ocultaMachosEnPostura: boolean;
  /** Santa Reyes: el catálogo de ítems de inventario sólo ofrece Alimento y Aves (en vez de los 6 tipos de siempre). */
  limitaTiposInventarioAlimentoYAves: boolean;
  /**
   * El listado de lotes de postura se separa en pestanas por etapa: ademas de la lista completa
   * aparecen «Lotes en Levante» y «Lotes en Produccion», cada una con los lotes de esa etapa.
   * Apagado, se ve una sola lista con la etapa en la columna Fase/Etapa.
   */
  separaLotesPosturaPorEtapa: boolean;
  /**
   * Santa Reyes: última semana de vida del lote (edad global desde encasetamiento) en la que el
   * ítem «Huevo de primera postura» sigue disponible en la clasificación por ítems. `null` = sin
   * límite configurado (todas las empresas salvo Santa Reyes) — no se oculta nada.
   */
  huevoPrimeraPosturaHastaSemana: number | null;
  /**
   * Santa Reyes: un mismo lote puede tener MÁS DE UN registro de seguimiento diario el mismo día
   * (dos turnos), tanto en levante como en producción. Los registros del día se agregan para la
   * grilla, los indicadores y los reportes: lo aditivo SUMA (mortalidad, selección, error de
   * sexaje, consumo, traslados, venta), el peso promedia y uniformidad/C.V. los gana el último
   * registro del día — misma regla que `fn_seguimiento_diario_levante` /
   * `fn_seguimiento_diario_produccion` y sus espejos `SeguimientoDiario*Calculos.AgruparPorDia`.
   *
   * Apagado (todas las demás empresas) el alta sigue rechazando el segundo registro del día, así
   * que el conteo de filas y el de días coinciden y nada de esto se activa.
   */
  permiteMultiplesSeguimientosDiarios: boolean;
  /**
   * Cuál de las dos tablas de guía genética de POSTURA administra la empresa. Ver
   * {@link GuiaGeneticaPerfil}. Fail-closed = `'sanmarino'` (el default neutro: es el perfil con el
   * que nace toda empresa, así que tratarlo así ante un error no habilita nada que no estuviera).
   */
  guiaGeneticaPerfil: GuiaGeneticaPerfil;
}

/** FAIL-CLOSED: si no hay empresa activa, falla el HTTP o el campo no viene → todo apagado. */
const FLAGS_APAGADOS: CompanyFlags = Object.freeze({
  manejaCodigosErpAvicola: false,
  clasificacionHuevoPorItems: false,
  permiteTrasladoAvesCrossEtapa: false,
  capturaHuevosEnLevante: false,
  ventaEngordePesoDiferido: false,
  primerRegistroSegunHoraLlegada: false,
  programacionLotesEngorde: false,
  nombreLoteIncluyeCorrida: false,
  manejaInventarioPorSilo: false,
  requiereValidacionSeguimientoDiario: false,
  semanasCicloPosturaPorRaza: false,
  consumoAlimentoSoloHembras: false,
  ocultaMachosEnPostura: false,
  limitaTiposInventarioAlimentoYAves: false,
  separaLotesPosturaPorEtapa: false,
  huevoPrimeraPosturaHastaSemana: null,
  permiteMultiplesSeguimientosDiarios: false,
  guiaGeneticaPerfil: GUIA_GENETICA_PERFIL_DEFECTO
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
  ventaEngordePesoDiferido?: boolean | null;
  primerRegistroSegunHoraLlegada?: boolean | null;
  programacionLotesEngorde?: boolean | null;
  nombreLoteIncluyeCorrida?: boolean | null;
  manejaInventarioPorSilo?: boolean | null;
  requiereValidacionSeguimientoDiario?: boolean | null;
  semanasCicloPosturaPorRaza?: boolean | null;
  consumoAlimentoSoloHembras?: boolean | null;
  ocultaMachosEnPostura?: boolean | null;
  limitaTiposInventarioAlimentoYAves?: boolean | null;
  separaLotesPosturaPorEtapa?: boolean | null;
  huevoPrimeraPosturaHastaSemana?: number | null;
  permiteMultiplesSeguimientosDiarios?: boolean | null;
  /** `companies.guia_genetica_perfil` — llega como texto libre; se valida contra los conocidos. */
  guiaGeneticaPerfil?: string | null;
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

  /** Atajo: ¿la empresa activa carga el peso de la venta de engorde al confirmarla (báscula diferida)? */
  readonly ventaEngordePesoDiferido$: Observable<boolean> = this.flags$.pipe(
    map(f => f.ventaEngordePesoDiferido),
    distinctUntilChanged()
  );

  /** Atajo: ¿la empresa activa programa los lotes de engorde (lote base obligatorio)? */
  readonly programacionLotesEngorde$: Observable<boolean> = this.flags$.pipe(
    map(f => f.programacionLotesEngorde),
    distinctUntilChanged()
  );

  /** Atajo: ¿la empresa activa ubica el inventario en silos/bodegas en vez del galpón? */
  readonly manejaInventarioPorSilo$: Observable<boolean> = this.flags$.pipe(
    map(f => f.manejaInventarioPorSilo),
    distinctUntilChanged()
  );

  /** Atajo: ¿los seguimientos diarios de la empresa activa exigen doble validación? */
  readonly requiereValidacionSeguimientoDiario$: Observable<boolean> = this.flags$.pipe(
    map(f => f.requiereValidacionSeguimientoDiario),
    distinctUntilChanged()
  );

  /** Atajo: ¿la empresa activa calcula la etapa del ciclo de vida por semana y por raza? */
  readonly semanasCicloPosturaPorRaza$: Observable<boolean> = this.flags$.pipe(
    map(f => f.semanasCicloPosturaPorRaza),
    distinctUntilChanged()
  );

  /** Atajo: ¿la empresa activa no captura consumo de alimento de Machos? */
  readonly consumoAlimentoSoloHembras$: Observable<boolean> = this.flags$.pipe(
    map(f => f.consumoAlimentoSoloHembras),
    distinctUntilChanged()
  );

  /** Atajo: ¿la empresa activa oculta Machos en mortalidad/selección/peso/uniformidad/ventas? */
  readonly ocultaMachosEnPostura$: Observable<boolean> = this.flags$.pipe(
    map(f => f.ocultaMachosEnPostura),
    distinctUntilChanged()
  );

  /** Atajo: ¿el catálogo de ítems de inventario de la empresa activa se limita a Alimento y Aves? */
  readonly limitaTiposInventarioAlimentoYAves$: Observable<boolean> = this.flags$.pipe(
    map(f => f.limitaTiposInventarioAlimentoYAves),
    distinctUntilChanged()
  );

  /** Atajo: ¿el listado de lotes de postura se separa en pestanas por etapa? */
  readonly separaLotesPosturaPorEtapa$: Observable<boolean> = this.flags$.pipe(
    map(f => f.separaLotesPosturaPorEtapa),
    distinctUntilChanged()
  );

  /** Atajo: última semana de vida con «Huevo de primera postura» vigente (`null` = sin límite). */
  readonly huevoPrimeraPosturaHastaSemana$: Observable<number | null> = this.flags$.pipe(
    map(f => f.huevoPrimeraPosturaHastaSemana),
    distinctUntilChanged()
  );

  /** Atajo: ¿la empresa activa acepta más de un seguimiento diario por lote y día? */
  readonly permiteMultiplesSeguimientosDiarios$: Observable<boolean> = this.flags$.pipe(
    map(f => f.permiteMultiplesSeguimientosDiarios),
    distinctUntilChanged()
  );

  /** Atajo: perfil de guía genética de la empresa activa (`sanmarino` | `reducida`). */
  readonly guiaGeneticaPerfil$: Observable<GuiaGeneticaPerfil> = this.flags$.pipe(
    map(f => f.guiaGeneticaPerfil),
    distinctUntilChanged()
  );

  /** Atajo: ¿la empresa activa administra la guía genética REDUCIDA (tabla plana de 3 métricas)? */
  readonly usaGuiaGeneticaReducida$: Observable<boolean> = this.flags$.pipe(
    map(f => f.guiaGeneticaPerfil === 'reducida'),
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

  /** Azúcar: sólo el flag de peso diferido en la venta de pollo engorde de la empresa activa. */
  ventaEngordePesoDiferido(): Observable<boolean> {
    return this.getFlags().pipe(map(f => f.ventaEngordePesoDiferido));
  }

  /** Azúcar: sólo el flag de "el primer registro lo decide la hora de llegada" de la empresa activa. */
  primerRegistroSegunHoraLlegada(): Observable<boolean> {
    return this.getFlags().pipe(map(f => f.primerRegistroSegunHoraLlegada));
  }

  /** Azúcar: sólo el flag de programación de lotes de engorde de la empresa activa. */
  programacionLotesEngorde(): Observable<boolean> {
    return this.getFlags().pipe(map(f => f.programacionLotesEngorde));
  }

  /** Azúcar: sólo el perfil de guía genética de la empresa activa. */
  guiaGeneticaPerfil(): Observable<GuiaGeneticaPerfil> {
    return this.getFlags().pipe(map(f => f.guiaGeneticaPerfil));
  }

  /** Azúcar: ¿la empresa activa administra la guía genética reducida? */
  usaGuiaGeneticaReducida(): Observable<boolean> {
    return this.getFlags().pipe(map(f => f.guiaGeneticaPerfil === 'reducida'));
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
      capturaHuevosEnLevante: dto?.capturaHuevosEnLevante === true,
      ventaEngordePesoDiferido: dto?.ventaEngordePesoDiferido === true,
      primerRegistroSegunHoraLlegada: dto?.primerRegistroSegunHoraLlegada === true,
      programacionLotesEngorde: dto?.programacionLotesEngorde === true,
      nombreLoteIncluyeCorrida: dto?.nombreLoteIncluyeCorrida === true,
      manejaInventarioPorSilo: dto?.manejaInventarioPorSilo === true,
      requiereValidacionSeguimientoDiario: dto?.requiereValidacionSeguimientoDiario === true,
      semanasCicloPosturaPorRaza: dto?.semanasCicloPosturaPorRaza === true,
      consumoAlimentoSoloHembras: dto?.consumoAlimentoSoloHembras === true,
      ocultaMachosEnPostura: dto?.ocultaMachosEnPostura === true,
      limitaTiposInventarioAlimentoYAves: dto?.limitaTiposInventarioAlimentoYAves === true,
      separaLotesPosturaPorEtapa: dto?.separaLotesPosturaPorEtapa === true,
      huevoPrimeraPosturaHastaSemana: typeof dto?.huevoPrimeraPosturaHastaSemana === 'number'
        ? dto.huevoPrimeraPosturaHastaSemana
        : null,
      permiteMultiplesSeguimientosDiarios: dto?.permiteMultiplesSeguimientosDiarios === true,
      // Sólo se acepta un perfil CONOCIDO. Un valor nuevo que el front todavía no entiende cae al
      // default neutro en vez de habilitar una pantalla equivocada — igual criterio que el backend,
      // que ante un valor desconocido lanza en vez de adivinar.
      guiaGeneticaPerfil: dto?.guiaGeneticaPerfil?.trim().toLowerCase() === 'reducida'
        ? 'reducida'
        : GUIA_GENETICA_PERFIL_DEFECTO
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
      actual.capturaHuevosEnLevante === flags.capturaHuevosEnLevante &&
      actual.ventaEngordePesoDiferido === flags.ventaEngordePesoDiferido &&
      actual.primerRegistroSegunHoraLlegada === flags.primerRegistroSegunHoraLlegada &&
      actual.programacionLotesEngorde === flags.programacionLotesEngorde &&
      actual.nombreLoteIncluyeCorrida === flags.nombreLoteIncluyeCorrida &&
      actual.manejaInventarioPorSilo === flags.manejaInventarioPorSilo &&
      actual.requiereValidacionSeguimientoDiario === flags.requiereValidacionSeguimientoDiario &&
      actual.semanasCicloPosturaPorRaza === flags.semanasCicloPosturaPorRaza &&
      actual.consumoAlimentoSoloHembras === flags.consumoAlimentoSoloHembras &&
      actual.ocultaMachosEnPostura === flags.ocultaMachosEnPostura &&
      actual.limitaTiposInventarioAlimentoYAves === flags.limitaTiposInventarioAlimentoYAves &&
      actual.separaLotesPosturaPorEtapa === flags.separaLotesPosturaPorEtapa &&
      actual.huevoPrimeraPosturaHastaSemana === flags.huevoPrimeraPosturaHastaSemana &&
      actual.permiteMultiplesSeguimientosDiarios === flags.permiteMultiplesSeguimientosDiarios &&
      actual.guiaGeneticaPerfil === flags.guiaGeneticaPerfil
    ) return;
    this.flagsSubject.next(flags);
  }
}
