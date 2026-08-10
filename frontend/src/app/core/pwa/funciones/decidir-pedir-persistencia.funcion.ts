/** Lo que se sabe del almacenamiento al momento de decidir si pedir persistencia. */
export interface EstadoPersistencia {
  /** ¿El navegador expone la Storage API (`navigator.storage.persist`)? */
  apiDisponible: boolean;
  /** ¿Ya está concedida? (`navigator.storage.persisted()`), o `null` si no se pudo consultar. */
  yaConcedida: boolean | null;
  /** ¿Ya se pidió en esta ejecución de la app? */
  yaPedidaEnEstaSesion: boolean;
  /** ¿Hay una sesión de usuario iniciada? */
  haySesion: boolean;
}

/**
 * ¿Corresponde pedir `navigator.storage.persist()` ahora?
 *
 * ## Por qué hace falta pedirlo
 *
 * Sin la concesión, la base de la consulta offline vive en almacenamiento *best-effort*: el navegador
 * puede **desalojarla** cuando el dispositivo se quede sin espacio. Es el peor modo de falla que
 * queda, porque **no avisa**: no hay error ni log, la pantalla simplemente aparece vacía en la granja
 * como si nunca se hubiera consultado nada.
 *
 * ## Por qué con sesión y no en el arranque en frío
 *
 * Chrome concede la persistencia según el *engagement* del sitio (y automáticamente si la app está
 * instalada); Firefox le pregunta al usuario. Pedirla antes del login es donde más probable es que la
 * denieguen, y en Firefox además significa un prompt a alguien que todavía no sabe qué es la app.
 *
 * ## Por qué una sola vez
 *
 * Repetirlo cuando ya está concedido es una llamada inútil, y en los navegadores que preguntan
 * reabre el diálogo. Pedir permiso dos veces es la forma más rápida de que lo nieguen.
 */
export function decidirPedirPersistencia(estado: EstadoPersistencia): boolean {
  if (!estado.apiDisponible) return false;
  if (!estado.haySesion) return false;
  if (estado.yaPedidaEnEstaSesion) return false;

  // `null` = no se pudo consultar. Se pide igual: el peor caso es una llamada de más, contra el
  // riesgo de quedarse sin la concesión por una consulta que falló.
  return estado.yaConcedida !== true;
}
