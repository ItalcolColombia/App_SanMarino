// features/sincronizacion-panama/funciones/estado-lote.funcion.ts
/**
 * Mapeo PURO de los estados de texto (por lote y del resultado completo) a `{ etiqueta, tono }`
 * para pintar badges con los tonos compartidos (neutro/ok/alerta/peligro).
 */
import { BadgeEstado } from '../models/sincronizacion-panama.model';

/** Badge por lote: Nuevo → ok, YaExiste → neutro, Pendiente (sin guía) → alerta, Error → peligro. */
export function badgeEstadoLote(estado: string | null | undefined): BadgeEstado {
  switch ((estado ?? '').trim()) {
    case 'Nuevo':     return { etiqueta: 'Nuevo', tono: 'ok' };
    case 'YaExiste':  return { etiqueta: 'Ya existe', tono: 'neutro' };
    case 'Pendiente': return { etiqueta: 'Pendiente (sin guía)', tono: 'alerta' };
    case 'SinGuia':   return { etiqueta: 'Pendiente (sin guía)', tono: 'alerta' };
    case 'Error':     return { etiqueta: 'Error', tono: 'peligro' };
    default:          return { etiqueta: (estado ?? '').trim() || '—', tono: 'neutro' };
  }
}

/** Badge del resultado completo (estado global de la corrida). */
export function badgeEstadoResultado(estado: string | null | undefined): BadgeEstado {
  const e = (estado ?? '').trim();
  switch (e) {
    case 'Procesado':
    case 'Completado':
    case 'Ok':
      return { etiqueta: e, tono: 'ok' };
    case 'ProcesadoParcial':
    case 'ConAdvertencias':
      return { etiqueta: e, tono: 'alerta' };
    case 'Fallido':
    case 'Error':
    case 'ConErrores':
      return { etiqueta: e, tono: 'peligro' };
    default:
      return { etiqueta: e || 'Validado', tono: 'neutro' };
  }
}
