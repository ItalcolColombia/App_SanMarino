/**
 * Filas de vista del cronograma: el estado visual se calcula UNA vez al cargar los ítems,
 * no en el template por ciclo de change detection (patrón NG0103 del repo: funciones en
 * template que alocan objetos nuevos por ciclo). Referencias estables para trackBy.
 * Función pura sin estado de Angular.
 */
import { VacunacionCronogramaItemDto } from '../models/vacunacion.model';
import { calcularEstadoVisual, EstadoVisual } from './calcular-estado-visual.funcion';

export interface FilaCronograma {
  item: VacunacionCronogramaItemDto;
  estado: EstadoVisual;
}

export function construirFilasCronograma(items: VacunacionCronogramaItemDto[]): FilaCronograma[] {
  return items.map((item) => ({ item, estado: calcularEstadoVisual(item) }));
}

/** trackBy estable por id del ítem del cronograma. */
export function trackByFilaCronograma(_index: number, fila: FilaCronograma): number {
  return fila.item.id;
}
