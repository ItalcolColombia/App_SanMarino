/**
 * Presentación del estado de aplicación: etiqueta legible + clases Tailwind del badge.
 * Solo visual — el estado, la desviación y el flag "incumplido" ya vienen calculados del backend
 * (VacunacionCalculos). Regla de marca: verde = éxito, rojo = peligro/incumplimiento, ámbar = alerta
 * leve (no destructivo), gris = neutral/pendiente.
 */
import { VacunacionCronogramaItemDto } from '../models/vacunacion.model';

export interface EstadoVisual {
  etiqueta: string;
  claseBadge: string;
}

export function calcularEstadoVisual(item: VacunacionCronogramaItemDto): EstadoVisual {
  const registro = item.registro;
  if (!registro || registro.estado === 'Pendiente') {
    return { etiqueta: 'Pendiente', claseBadge: 'bg-gray-100 text-gray-700 border border-gray-200' };
  }

  switch (registro.estado) {
    case 'Aplicado':
      return { etiqueta: 'Aplicado a tiempo', claseBadge: 'bg-green-100 text-green-700 border border-green-200' };
    case 'AplicadoAdelantado':
      return { etiqueta: 'Aplicado adelantado', claseBadge: 'bg-amber-100 text-amber-700 border border-amber-200' };
    case 'AplicadoTardio':
      return registro.incumplido
        ? { etiqueta: `Incumplido (+${registro.diasDesviacion} d)`, claseBadge: 'bg-red-100 text-red-700 border border-red-200' }
        : { etiqueta: `Tardío (+${registro.diasDesviacion} d)`, claseBadge: 'bg-amber-100 text-amber-700 border border-amber-200' };
    case 'NoAplicado':
      return { etiqueta: 'No aplicado', claseBadge: 'bg-red-100 text-red-700 border border-red-200' };
    default:
      return { etiqueta: registro.estado, claseBadge: 'bg-gray-100 text-gray-700 border border-gray-200' };
  }
}
