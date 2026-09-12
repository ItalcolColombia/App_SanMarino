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

/**
 * Busca un registro por id en lo que devolvió el listado, mirando **primero** los `registrosDelDia`.
 *
 * El orden importa: en un día con varios registros la fila agrupada lleva el id del primero
 * (`MIN(seg_id)`) pero los TOTALES del día. Buscar solo en `seguimientos` no encuentra al 2.º
 * registro y, para el 1.º, devuelve la fila agrupada: un diálogo que lea mortalidad o traslado de
 * ahí mostraría números que no son de ese registro. Sin días desplegados es un `find` por id común.
 */
export function buscarRegistroPorId(
  seguimientos: readonly SeguimientoItemDto[] | null | undefined,
  id: number | null | undefined
): SeguimientoItemDto | undefined {
  if (id == null) return undefined;
  for (const dia of seguimientos ?? []) {
    const registros = dia.registrosDelDia;
    if (registros && registros.length > 1) {
      const hijo = registros.find(r => r.id === id);
      if (hijo) return hijo;
      continue;
    }
    if (dia.id === id) return dia;
  }
  return undefined;
}

/** Todos los registros visibles en la tabla (para los mapas indexados por id). */
export function registrosDeLaGrilla(filas: readonly FilaGrillaProduccion[]): SeguimientoItemDto[] {
  return filas.map(f => f.seg);
}
