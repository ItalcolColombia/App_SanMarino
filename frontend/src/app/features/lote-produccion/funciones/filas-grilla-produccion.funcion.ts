import type { SeguimientoItemDto } from '../services/produccion.service';

/** Una fila de la tabla «Registros diarios» de producción. */
export interface FilaGrillaProduccion {
  /** Registro que pinta la fila y sobre el que actúan Ver / Validar / Editar / Eliminar. */
  seg: SeguimientoItemDto;
  /** Posición del registro dentro de su día (1 = primero). */
  ordinal: number;
  /** Cuántos registros tiene ese día. 1 en el caso normal. */
  total: number;
  /** Solo el primero del día rotula fecha, edad, semana y etapa. */
  esPrimero: boolean;
}

/**
 * Despliega en una fila por registro los días que traen `registrosDelDia` (2+ registros el mismo
 * día, flag de empresa). Un día normal queda como una sola fila `1 de 1` con el mismo objeto que
 * llegó ⇒ sin el flag la tabla es idéntica a la de siempre.
 *
 * Pura: no muta la entrada. Se llama una vez por carga de `seguimientos`, no por ciclo de CD.
 */
export function filasGrillaProduccion(seguimientos: readonly SeguimientoItemDto[] | null | undefined): FilaGrillaProduccion[] {
  const filas: FilaGrillaProduccion[] = [];
  for (const dia of seguimientos ?? []) {
    const registros = dia.registrosDelDia;
    if (!registros || registros.length < 2) {
      filas.push({ seg: dia, ordinal: 1, total: 1, esPrimero: true });
      continue;
    }
    registros.forEach((seg, i) =>
      filas.push({ seg, ordinal: i + 1, total: registros.length, esPrimero: i === 0 }));
  }
  return filas;
}

/** Todos los registros visibles en la tabla (para los mapas indexados por id). */
export function registrosDeLaGrilla(filas: readonly FilaGrillaProduccion[]): SeguimientoItemDto[] {
  return filas.map(f => f.seg);
}
