/**
 * Presentación de un pendiente de la bandeja: rótulo con los días y color del chip.
 * Puro — la situación y los días ya vienen resueltos del backend (`fn_vacunacion_pendientes`,
 * especificada por `VacunacionPendientesCalculos`). Acá sólo se traduce a texto.
 *
 * Regla de marca: rojo = peligro (vencido), ámbar = alerta leve (hoy toca), gris = neutral.
 */
import { SituacionPendiente, VacunacionPendienteDto } from '../models/vacunacion.model';

export interface PendienteVisual {
  etiqueta: string;
  claseBadge: string;
}

const dia = (n: number): string => `${n} ${n === 1 ? 'día' : 'días'}`;

export function describirPendiente(
  situacion: SituacionPendiente,
  dias: number,
): PendienteVisual {
  switch (situacion) {
    case 'Vencido':
      return {
        etiqueta: `Vencida hace ${dia(dias)}`,
        claseBadge: 'bg-red-100 text-red-700 border border-red-200',
      };
    case 'EnFranja':
      return {
        etiqueta: 'Toca ahora',
        claseBadge: 'bg-amber-100 text-amber-700 border border-amber-200',
      };
    default:
      return {
        etiqueta: `En ${dia(Math.abs(dias))}`,
        claseBadge: 'bg-gray-100 text-gray-700 border border-gray-200',
      };
  }
}

/** Dónde hay que ir: granja · núcleo · galpón, sin los separadores de los niveles vacíos. */
export function ubicacionDePendiente(p: VacunacionPendienteDto): string {
  return [p.granjaNombre, p.nucleoId, p.galponId].filter((x) => !!x && `${x}`.trim()).join(' · ');
}

/** Cuándo estaba programada, en el vocabulario de la línea (semana de edad / día de edad / fecha). */
export function objetivoDePendiente(p: VacunacionPendienteDto): string {
  if (p.unidadObjetivo === 'Semana' && p.valorObjetivo != null) return `Semana ${p.valorObjetivo}`;
  if (p.unidadObjetivo === 'Dia' && p.valorObjetivo != null) return `Día ${p.valorObjetivo}`;
  return 'Fecha fija';
}

export const trackByPendiente = (_: number, p: VacunacionPendienteDto): number => p.cronogramaItemId;
