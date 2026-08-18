import { claveParticion } from '../../../shared/offline/funciones/clave-particion.funcion';
import type { IdentidadParticion } from '../../../shared/offline/models/offline.model';
import type { OperacionPendiente } from '../../../shared/offline/models/outbox.model';
import type { CapturaDiagnostico } from '../models/captura-diagnostico.model';

/**
 * Marca cada captura de la cola como propia de la sesión activa o ajena.
 *
 * ## Qué problema resuelve
 *
 * `/diagnostico` **no tiene `authGuard` a propósito**: es la pantalla de rescate, y ponerle un guard
 * la haría inalcanzable justo en el escenario para el que existe (sesión vencida sin red, SW en safe
 * mode). Esa decisión sigue siendo correcta; lo que caducó es su premisa. Cuando se escribió, la
 * pantalla solo mostraba build, estado del SW y cuota. Desde F3.1 lista la cola entera con el
 * `JSON.stringify` de cada payload y un botón para descartarla — o sea que en una tablet compartida
 * cualquiera que la levante lee, y borra, lo que capturaron todos, **sin sesión**.
 *
 * La salida es: seguir **listando** las ajenas —esconderlas sería la peor variante de «se perdió»—
 * pero sin payload, sin copiar y sin descartar.
 *
 * ## Fail-closed
 *
 * Sin identidad completa **nada es propio**: todo se enmascara. Es el caso del rescate sin sesión, y
 * es el que tiene que salir bien por defecto. Una fila con la `particion` corrupta (IndexedDB no
 * valida tipos) tampoco es propia: `claveParticion` ya devolvió `null` o la comparación falla.
 *
 * No filtra ni ordena: devuelve **la misma cola, en el mismo orden**, con la decisión adosada.
 */
export function clasificarCapturasDiagnostico(
  operaciones: readonly OperacionPendiente[] | null | undefined,
  identidad: IdentidadParticion | null | undefined
): CapturaDiagnostico[] {
  if (!operaciones?.length) {
    return [];
  }

  const particion = claveParticion(identidad);

  return operaciones.map(operacion => ({
    operacion,
    propia: particion !== null && operacion.particion === particion
  }));
}
