import { claveParticion } from './clave-particion.funcion';
import type { IdentidadParticion } from '../models/offline.model';
import type { OperacionPendiente } from '../models/outbox.model';

/**
 * De toda la cola, las operaciones que puede empujar **la sesión activa**, y solo las que ya
 * cumplieron su backoff.
 *
 * ## Por qué el filtro por partición no es un detalle
 *
 * El push sale con el token que le pega `AuthInterceptor`, o sea el de quien esté logueado **ahora**,
 * y el servidor estampa el autor desde ese token ignorando el del cuerpo (B5). Empujar la cola
 * entera, entonces, no es "mandar de más": es firmar el trabajo de un operario con la identidad de
 * otro.
 *
 * Y ya era alcanzable con un solo usuario: el JWT vence a los 60 min ⇒ `authGuard` hace `logout()`
 * ⇒ el outbox **sobrevive** (R9: nada borra capturas sin confirmación) ⇒ entra el operario del turno
 * siguiente y el `effect` de reconexión empuja lo del anterior. Misma empresa ⇒ 200 OK con el autor
 * equivocado. Empresa distinta ⇒ `empresa_no_autorizada`, que está clasificado como *reintentar*
 * (no bandeja) ⇒ reintento infinito e invisible.
 *
 * ## Fail-closed
 *
 * Sin identidad completa devuelve `[]`: **no envía nada**. Es el mismo criterio que `claveParticion`
 * —de donde sale la clave— y el que aplica el backend ante alcance ambiguo. Sin sesión no hay a
 * nombre de quién empujar, y quedarse en la cola es reversible; aplicar con el autor equivocado, no.
 *
 * ## Lo que NO hace
 *
 * No toca las operaciones ajenas: no las borra, no las marca rechazadas, no las reprograma. Siguen
 * en la cola, intactas, esperando a que su dueño vuelva a entrar (R9).
 */
export function filtrarOperacionesParticion(
  operaciones: readonly OperacionPendiente[] | null | undefined,
  identidad: IdentidadParticion | null | undefined,
  ahora: number
): OperacionPendiente[] {
  const particion = claveParticion(identidad);
  if (particion === null || !operaciones?.length) {
    return [];
  }

  // Lo rechazado no se reintenta solo: espera a que una persona lo edite o lo descarte desde la
  // bandeja. El resto del predicado es el que ya tenía `enviarPendientes`, sin cambios.
  return operaciones.filter(
    op =>
      op.particion === particion &&
      op.estado === 'pendiente' &&
      (op.proximoIntentoEn === null || op.proximoIntentoEn <= ahora)
  );
}
