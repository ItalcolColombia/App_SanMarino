import { slotVencido } from '../../../../core/auth/funciones/llavero-sesiones.funcion';
import { LIMITES_SESION_POR_DEFECTO } from '../../../../core/auth/funciones/politica-sesion.funcion';
import type { SlotSesion } from '../../../../core/auth/models/slot-sesion.model';

/**
 * Qué le muestra el selector de perfil a alguien que levanta la tablet **sin red**.
 *
 * Lo que se pinta sale **solo del padrón**: nombre, empresa, hace cuánto se usó, cuántas capturas
 * esperan y si el slot puede abrirse. Nada de permisos ni de menú — eso vive dentro del blob cifrado y
 * abrirlo exige el PIN (R-M7).
 */

export type EstadoSlotSelector =
  /** Se puede abrir acá mismo con el PIN. */
  | 'activable'
  /** Pasó la jornada de 16 h sin hablar con el servidor (D4): hay que entrar con red. */
  | 'jornada_vencida'
  /** Se agotaron los intentos de PIN y el blob se destruyó: solo queda entrar con red. */
  | 'requiere_reingreso';

export interface FilaSelector {
  slot: SlotSesion;
  estado: EstadoSlotSelector;
  /** Capturas de ese operario esperando salir. Se deriva del outbox, no se guarda en el padrón. */
  pendientes: number;
  /** «hace 20 min», «hace 3 h», «ayer»… Para que «¿dónde quedó lo que cargué?» tenga respuesta. */
  hace: string;
}

/**
 * Arma las filas del selector, **de la más reciente a la más vieja**: el que más probablemente vuelva
 * es el último que usó el equipo.
 *
 * Ningún estado esconde una fila. Un slot que no se puede abrir se sigue mostrando, apagado y con el
 * motivo: desaparecer de la lista se lee como «se perdió mi sesión», y con capturas pendientes adentro
 * eso es exactamente lo que no hay que hacer sentir.
 */
export function filasSelector(
  slots: readonly SlotSesion[] | null | undefined,
  pendientes: Readonly<Record<string, number>>,
  ahora: number,
  jornadaOfflineMs: number = LIMITES_SESION_POR_DEFECTO.jornadaOfflineMs
): FilaSelector[] {
  if (!slots?.length) {
    return [];
  }

  return [...slots]
    .sort((a, b) => b.ultimoUsoEn - a.ultimoUsoEn)
    .map(slot => ({
      slot,
      estado: estadoDe(slot, ahora, jornadaOfflineMs),
      pendientes: pendientes[slot.slotId] ?? 0,
      hace: tiempoRelativo(slot.ultimoUsoEn, ahora)
    }));
}

/**
 * Estado del slot. `requiereReingreso` gana sobre el vencimiento: los dos llevan al login con red,
 * pero el motivo que se le dice al operario es distinto —«se agotaron los intentos» no es lo mismo que
 * «llevás mucho sin conectarte»— y confundirlos manda a buscar el problema al lugar equivocado.
 */
function estadoDe(slot: SlotSesion, ahora: number, jornadaOfflineMs: number): EstadoSlotSelector {
  if (slot.requiereReingreso) {
    return 'requiere_reingreso';
  }
  return slotVencido(slot, ahora, jornadaOfflineMs) ? 'jornada_vencida' : 'activable';
}

/**
 * «Hace cuánto», en grueso. No se usa `Intl.RelativeTimeFormat` porque acá el redondeo importa al
 * revés de lo habitual: **nunca decir «hace 1 h» de algo de hace 100 minutos**, que es lo que haría
 * un redondeo al más cercano. Siempre se trunca hacia abajo, y un reloj adelantado cae en «recién».
 */
export function tiempoRelativo(desde: number, ahora: number): string {
  const ms = ahora - desde;
  const min = Math.floor(ms / 60_000);

  if (min < 1) return 'recién';
  if (min < 60) return `hace ${min} min`;

  const horas = Math.floor(min / 60);
  if (horas < 24) return `hace ${horas} h`;

  const dias = Math.floor(horas / 24);
  return dias === 1 ? 'ayer' : `hace ${dias} días`;
}
