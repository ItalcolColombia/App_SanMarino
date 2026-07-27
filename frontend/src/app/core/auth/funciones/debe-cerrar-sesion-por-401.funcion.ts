import { HttpErrorResponse } from '@angular/common/http';

/**
 * Marca que el backend pone en los rechazos del gate de plataforma (SECRET_UP).
 * Espeja `PlatformSecretMiddleware.PlatformFailureValue` en C#.
 */
export const MOTIVO_FALLA_PLATAFORMA = 'platform-secret';

/**
 * ¿Este 401 significa que la sesión del usuario terminó?
 *
 * **No todos los 401 son iguales.** El backend devuelve 401 en dos situaciones que no
 * tienen nada que ver entre sí:
 *
 *  1. **Autenticación** — el JWT venció o se invalidó. La sesión efectivamente terminó.
 *  2. **Plataforma** — `PlatformSecretMiddleware` no reconoce el origen de la petición
 *     (falta el `X-Secret-Up`, no se pudo desencriptar, o no coincide). El usuario y su
 *     token están perfectos; lo que falla es el gate de origen.
 *
 * Tratar el caso 2 como el caso 1 es lo que hacía el interceptor hasta ahora, y tiene una
 * consecuencia seria: **rotar el SECRET_UP desloguea a todos los dispositivos a la vez** y,
 * cuando exista la cola de sincronización offline, se lleva puesto el almacenamiento donde
 * vive el trabajo de campo sin subir.
 *
 * La señal se lee del **cuerpo** (`errorCode`) y no de la cabecera `X-Auth-Failure`: en
 * desarrollo el front (`:4200`) y el backend (`:5002`) son orígenes distintos, y una cabecera
 * de respuesta personalizada no es legible desde JS sin `Access-Control-Expose-Headers`.
 * El cuerpo siempre está disponible. La cabecera queda para `curl` y logs.
 *
 * @param err       El error de la petición.
 * @param teniaToken Si la petición llevaba `Authorization`. El login no lo lleva, y su 401
 *                   es "credenciales inválidas", no "sesión vencida".
 */
export function debeCerrarSesionPor401(err: unknown, teniaToken: boolean): boolean {
  if (!(err instanceof HttpErrorResponse) || err.status !== 401) return false;

  // Petición sin token (login) → el 401 es "credenciales inválidas". No hay sesión que cerrar.
  if (!teniaToken) return false;

  // Rechazo del gate de plataforma → el problema es el origen, no el usuario.
  if (esFallaDePlataforma(err)) return false;

  return true;
}

/** Reconoce el rechazo del gate de plataforma, tolerando cuerpo string, objeto o ausente. */
function esFallaDePlataforma(err: HttpErrorResponse): boolean {
  const cuerpo: unknown = err.error;

  if (typeof cuerpo === 'string') {
    return cuerpo.includes(MOTIVO_FALLA_PLATAFORMA);
  }

  if (cuerpo && typeof cuerpo === 'object') {
    const codigo = (cuerpo as { errorCode?: unknown }).errorCode;
    if (codigo === MOTIVO_FALLA_PLATAFORMA) return true;
  }

  // Mismo origen (producción): la cabecera sí es legible y sirve de respaldo.
  return err.headers?.get('X-Auth-Failure') === MOTIVO_FALLA_PLATAFORMA;
}
