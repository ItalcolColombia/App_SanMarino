import { HttpErrorResponse, HttpEvent, HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, catchError, from, of, switchMap, tap, throwError } from 'rxjs';

import { TokenStorageService } from '../../core/auth/token-storage.service';
import { CacheConsultasService } from './cache-consultas.service';
import { decidirCacheOffline } from './funciones/decidir-cache-offline.funcion';
import { decidirCacheable } from './funciones/decidir-cacheable.funcion';
import { decidirEncolable } from './funciones/decidir-encolable.funcion';
import { OutboxService } from './outbox.service';
import { ES_PUSH_DE_SYNC } from './sync-context';
import type { IdentidadParticion } from './models/offline.model';

/**
 * Particiones ya purgadas por no ser elegibles (D6), para no lanzar un borrado por cada request.
 * Vive a nivel de módulo porque el interceptor es una función, no una clase con estado.
 */
const purgadasPorNoElegible = new Set<string>();

/**
 * Consulta offline (F2): guarda las respuestas operativas y las sirve **cuando no hay red**.
 *
 * ## Red primero, caché solo como respaldo
 *
 * Nunca se sirve de caché habiendo conexión. Una app de gestión que muestra números viejos cuando
 * podía mostrar los buenos es peor que una que tarda un segundo más: el operario no tiene forma de
 * saber cuál de los dos está viendo. La caché entra **solo** ante `status === 0`, que es el código
 * que Angular usa cuando la petición no llegó a ningún lado.
 *
 * Un 4xx o un 5xx **no** activan la caché: son respuestas del servidor, o sea que hay red y el
 * backend tiene algo que decir. Taparlas con datos viejos escondería el problema real.
 *
 * ## Por qué acá y no en el Service Worker
 *
 * La caché del SW indexa por URL e **ignora los headers**, y la empresa activa viaja en
 * `X-Active-Company`. Dos empresas piden `GET /api/Lote` con la misma URL y respuestas distintas: el
 * SW le serviría a una la respuesta de la otra. Acá la clave la elegimos nosotros e incluye
 * `{userId, companyId, paisId}`. Ver `claveParticion` y `ngsw-config.json`.
 */
export const offlineCacheInterceptor: HttpInterceptorFn = (req, next): Observable<HttpEvent<unknown>> => {
  const cache = inject(CacheConsultasService);
  const storage = inject(TokenStorageService);
  const outbox = inject(OutboxService);

  const sesion = storage.get();
  const identidad: IdentidadParticion = {
    // El Guid es el identificador estable del usuario; `userId` es un hash numérico derivado y se
    // usa solo como respaldo. Si no hay ninguno de los dos, `claveParticion` devuelve null y no se
    // cachea nada — que es el comportamiento correcto, no una degradación.
    userId: sesion?.user?.id ?? sesion?.user?.userId ?? null,
    companyId: sesion?.activeCompanyId ?? null,
    paisId: sesion?.activePaisId ?? null
  };

  // ── Captura offline (F3) ───────────────────────────────────────────────────────────────────
  // El push del propio sync se excluye: si fallara por falta de red, se encolaría a sí mismo.
  if (!req.context.get(ES_PUSH_DE_SYNC) && decidirEncolable(req.method, req.url, identidad)) {
    return encolarSiNoHayRed(req.method, req.urlWithParams, req.body, outbox, identidad, next(req));
  }

  if (!decidirCacheable(req.method, req.url)) {
    return next(req);
  }

  // D6: las cuentas con alcance global o multiempresa no acumulan datos en el dispositivo. La
  // partición evita que una sesión lea lo de otra, pero no que el mismo equipo junte lo de todas
  // las empresas que ese usuario visita — y el dato en reposo no está cifrado (D3).
  if (!decidirCacheOffline(sesion)) {
    // Un gate que solo impide ESCRIBIR dejaría intacto —y se seguiría sirviendo— lo que la cuenta
    // hubiera cacheado antes de este cambio. Se purga una vez por partición.
    const marca = `${identidad.userId ?? ''}|${identidad.companyId ?? ''}|${identidad.paisId ?? ''}`;
    if (!purgadasPorNoElegible.has(marca)) {
      purgadasPorNoElegible.add(marca);
      void cache.purgarParticionDe(identidad);
    }
    return next(req);
  }

  return next(req).pipe(
    tap(evento => {
      if (evento instanceof HttpResponse) {
        cache.marcarRespuestaDeRed();
        // Guardar no bloquea la respuesta: si IndexedDB tarda o falla, al usuario no le importa.
        void cache.guardar(identidad, req.method, req.urlWithParams, evento.body);
      }
    }),
    catchError((error: unknown) => {
      const sinRed = error instanceof HttpErrorResponse && error.status === 0;
      if (!sinRed) {
        return throwError(() => error);
      }

      return from(cache.recuperar(identidad, req.method, req.urlWithParams)).pipe(
        switchMap(guardado => {
          if (!guardado) {
            // Sin red y sin nada vigente guardado: se propaga el error real. Devolver una
            // respuesta vacía dejaría una pantalla en blanco que el usuario leería como "no hay
            // datos", que es una afirmación distinta y falsa.
            return throwError(() => error);
          }

          return of(
            new HttpResponse({
              body: guardado.cuerpo,
              status: 200,
              statusText: 'OK (consulta guardada sin conexión)',
              url: req.urlWithParams
            })
          );
        })
      );
    })
  );
};

/**
 * Red primero, cola solo ante `status === 0`.
 *
 * ## Por qué esta rama NO pasa por el gate D6
 *
 * `decidirCacheOffline` bloquea la caché de **lectura** para super admin y cuentas multiempresa:
 * protege contra un dispositivo perdido con el snapshot de la operación completa. La cola es otra
 * cosa — son capturas propias que el servidor **nunca vio**. Bloquearla no protege ningún snapshot:
 * destruye trabajo de campo. Dos amenazas distintas, dos respuestas distintas.
 *
 * ## La respuesta sintética tiene que notarse
 *
 * Se devuelve **202** (`Accepted`) con `__offlinePendiente: true`, no un 200 con el cuerpo que
 * habría devuelto el servidor. Una pantalla que dice "guardado" a secas cuando la operación todavía
 * está en la tablet es peor que un error: el galponero cierra la app convencido de que el dato salió.
 *
 * Y si el encolado **falla** (IndexedDB no disponible, cuota llena), se propaga el error de red
 * original. Afirmar que se guardó sin haber guardado es el único resultado inaceptable de este módulo.
 */
function encolarSiNoHayRed(
  metodo: string,
  url: string,
  cuerpo: unknown,
  outbox: OutboxService,
  identidad: IdentidadParticion,
  peticion: Observable<HttpEvent<unknown>>
): Observable<HttpEvent<unknown>> {
  return peticion.pipe(
    catchError((error: unknown) => {
      const sinRed = error instanceof HttpErrorResponse && error.status === 0;
      if (!sinRed) {
        // Un 4xx/5xx no se encola: hay red y el backend tiene algo que decir. Encolarlo repetiría
        // eternamente una operación que el servidor ya rechazó.
        return throwError(() => error);
      }

      return from(outbox.encolar(identidad, metodo, url, cuerpo)).pipe(
        switchMap(operacion => {
          if (!operacion) {
            return throwError(() => error);
          }

          return of(
            new HttpResponse({
              body: { __offlinePendiente: true, clientOpId: operacion.clientOpId },
              status: 202,
              statusText: 'Accepted (guardado en el dispositivo, pendiente de enviar)',
              url
            })
          );
        })
      );
    })
  );
}
