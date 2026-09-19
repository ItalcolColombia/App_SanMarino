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
  /**
   * Renglón AGRUPADO del día (el que ya suma la fn canónica): solo en la PRIMERA fila de un día con 2+ registros.
   *
   * Existe porque esa primera fila muestra SOLO su registro: con los huevos cargados en el 2.º o el 3.º, la
   * línea que el operario lee primero para ese día dice «huevos 0» y parece que no entraron. Santa Reyes
   * (18-sep-2026): repitió tres veces la misma carga de 7.010 huevos por eso (el sistema llegó a contar 21.030,
   * 183 % de postura). Esta referencia deja rotular el total del día sin tocar las cifras ni los botones de
   * cada registro (cada uno sigue editable por su propia fila).
   */
  totalDia: SeguimientoItemDto | null;
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
      filas.push({ seg: dia, ordinal: 1, total: 1, esPrimero: true, totalDia: null });
      continue;
    }
    registros.forEach((seg, i) =>
      filas.push({ seg, ordinal: i + 1, total: registros.length, esPrimero: i === 0, totalDia: i === 0 ? dia : null }));
  }
  return filas;
}

/**
 * Detalle del «Total del día» para el tooltip: lo que la fila del día ya suma además de los huevos. Solo lo que trae
 * valor; con `incluirMachos` falso (Santa Reyes no maneja machos en postura) se omiten las cifras de machos.
 */
export function detalleTotalDia(dia: SeguimientoItemDto, incluirMachos: boolean): string {
  const n = (v: number | null | undefined): number => (Number.isFinite(Number(v)) ? Number(v) : 0);
  const mortalidad = n(dia.mortalidadH) + (incluirMachos ? n(dia.mortalidadM) : 0);
  const seleccion = n(dia.selH) + (incluirMachos ? n(dia.selM) : 0);
  const consumo = n(dia.consKgH) + (incluirMachos ? n(dia.consKgM) : 0);

  const partes: string[] = [];
  if (mortalidad > 0) partes.push(`mortalidad ${mortalidad}`);
  if (seleccion > 0) partes.push(`selección ${seleccion}`);
  if (consumo > 0) partes.push(`consumo ${Number(consumo.toFixed(2))} kg`);
  return partes.length > 0
    ? `Suma de los ${dia.registrosDelDia?.length ?? 0} registros del día: ${partes.join(' · ')}`
    : `Suma de los ${dia.registrosDelDia?.length ?? 0} registros del día`;
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
