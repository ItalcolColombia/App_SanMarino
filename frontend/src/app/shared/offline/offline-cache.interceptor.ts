import { HttpErrorResponse, HttpEvent, HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, catchError, from, of, switchMap, tap, throwError } from 'rxjs';

import { TokenStorageService } from '../../core/auth/token-storage.service';
import { CacheConsultasService } from './cache-consultas.service';
import { decidirCacheOffline } from './funciones/decidir-cache-offline.funcion';
import { decidirCacheable } from './funciones/decidir-cacheable.funcion';
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

  if (!decidirCacheable(req.method, req.url)) {
    return next(req);
  }

  const sesion = storage.get();
  const identidad: IdentidadParticion = {
    // El Guid es el identificador estable del usuario; `userId` es un hash numérico derivado y se
    // usa solo como respaldo. Si no hay ninguno de los dos, `claveParticion` devuelve null y no se
    // cachea nada — que es el comportamiento correcto, no una degradación.
    userId: sesion?.user?.id ?? sesion?.user?.userId ?? null,
    companyId: sesion?.activeCompanyId ?? null,
    paisId: sesion?.activePaisId ?? null
  };

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
