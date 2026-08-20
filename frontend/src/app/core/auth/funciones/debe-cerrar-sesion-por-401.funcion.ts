import { HttpErrorResponse } from '@angular/common/http';

/**
 * Marca que el backend pone en los rechazos del gate de plataforma (SECRET_UP).
 * Espeja `PlatformSecretMiddleware.PlatformFailureValue` en C#.
 */
export const MOTIVO_FALLA_PLATAFORMA = 'platform-secret';

/**
 * Marca del 401 de **sesión revocada** (B1). Espeja `RevocacionSesionCalculos.MotivoRevocada`.
 * A diferencia del de plataforma, éste **sí** cierra la sesión: es el caso de autenticación por
 * excelencia —alguien apagó esta sesión a propósito— y el token ya no sirve para nada.
 */
export const MOTIVO_SESION_REVOCADA = 'sesion-revocada';

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

/**
 * ¿Este 401 es una sesión **revocada** desde el servidor?
 *
 * Cerrar la sesión ya lo hacía `debeCerrarSesionPor401` —todo 401 con token que no sea de
 * plataforma cierra—; lo que agrega distinguirlo es el **motivo**: sin esto, al operario cuya
 * tablet acaban de revocar se le dice «tu sesión expiró», que es falso y lo manda a esperar en vez
 * de a hablar con quien la cerró.
 */
export function esSesionRevocada(err: unknown): boolean {
  if (!(err instanceof HttpErrorResponse) || err.status !== 401) return false;
  return tieneMotivo(err, MOTIVO_SESION_REVOCADA);
}

/** Reconoce el rechazo del gate de plataforma, tolerando cuerpo string, objeto o ausente. */
function esFallaDePlataforma(err: HttpErrorResponse): boolean {
  return tieneMotivo(err, MOTIVO_FALLA_PLATAFORMA);
}

/**
 * ¿El 401 trae este `errorCode`? Se lee del CUERPO (en dev el front es otro origen y no puede
 * leer cabeceras personalizadas); la cabecera queda de respaldo para mismo origen y para `curl`.
 */
function tieneMotivo(err: HttpErrorResponse, motivo: string): boolean {
  const cuerpo: unknown = err.error;

  if (typeof cuerpo === 'string') {
    return cuerpo.includes(motivo);
  }

  if (cuerpo && typeof cuerpo === 'object') {
    const codigo = (cuerpo as { errorCode?: unknown }).errorCode;
    if (codigo === motivo) return true;
  }

  // Mismo origen (producción): la cabecera sí es legible y sirve de respaldo.
  return err.headers?.get('X-Auth-Failure') === motivo;
}
