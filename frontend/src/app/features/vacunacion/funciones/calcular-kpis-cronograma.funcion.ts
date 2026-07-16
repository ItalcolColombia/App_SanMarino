/**
 * KPIs del cronograma del lote seleccionado, calculados UNA vez al cargar (no en template).
 * Solo agrega estados que ya vienen decididos por el backend. Función pura.
 */
import { VacunacionCronogramaItemDto } from '../models/vacunacion.model';

export interface KpisCronograma {
  total: number;
  pendientes: number;
  aplicadasATiempo: number;
  adelantadas: number;
  /** Tardías totales (leves + incumplidas). */
  tardias: number;
  /** Subconjunto de tardías que superó el umbral (rojo). */
  incumplidas: number;
  noAplicadas: number;
  /** % de ítems ya resueltos (con registro distinto de pendiente) sobre el total. */
  porcentajeAvance: number | null;
  /** % aplicado a tiempo sobre el total programado (misma semántica que el reporte). */
  porcentajeATiempo: number | null;
}

export function calcularKpisCronograma(items: VacunacionCronogramaItemDto[]): KpisCronograma {
  const total = items.length;
  let pendientes = 0;
  let aplicadasATiempo = 0;
  let adelantadas = 0;
  let tardias = 0;
  let incumplidas = 0;
  let noAplicadas = 0;

  for (const item of items) {
    const estado = item.registro?.estado ?? 'Pendiente';
    switch (estado) {
      case 'Aplicado':
        aplicadasATiempo++;
        break;
      case 'AplicadoAdelantado':
        adelantadas++;
        break;
      case 'AplicadoTardio':
        tardias++;
        if (item.registro?.incumplido) incumplidas++;
        break;
      case 'NoAplicado':
        noAplicadas++;
        break;
      default:
        pendientes++;
    }
  }

  const resueltas = total - pendientes;
  return {
    total,
    pendientes,
    aplicadasATiempo,
    adelantadas,
    tardias,
    incumplidas,
    noAplicadas,
    porcentajeAvance: total ? Math.round((100 * resueltas) / total) : null,
    porcentajeATiempo: total ? Math.round((100 * aplicadasATiempo) / total) : null,
  };
}
