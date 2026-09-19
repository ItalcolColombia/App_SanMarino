/**
 * Texto del aviso de éxito de un seguimiento diario de PRODUCCIÓN: «Seguimiento creado: huevos 7010 ·
 * mortalidad 15 · consumo 1215 kg».
 *
 * Función PURA (sin `this`, sin DI, sin estado). Sale del REQUEST que acaba de aceptar el servidor, o sea que
 * dice lo que quedó guardado, no lo que se cree haber tecleado.
 *
 * ## Por qué existe
 *
 * Santa Reyes (18-sep-2026): un operario repitió tres veces la misma carga de huevos (#693, #694 y #695, todos
 * de 7.010) porque nada le confirmaba que la primera había entrado: el modal se cerraba en silencio y la
 * primera línea del día seguía diciendo 0 huevos. El sistema terminó contando 21.030 huevos (183 % de postura)
 * en ese galpón. Un aviso que repite las cifras en el momento del guardado responde la pregunta que el operario
 * se hacía: «¿entraron los huevos?».
 *
 * Solo se nombra lo que trae valor; sin ninguna cifra se dice explícitamente (con la captura parcial de Santa
 * Reyes un registro puede quedar vacío, y eso también tiene que verse).
 */

/** Ítem de alimento/insumo tal como viaja en el request. */
interface ItemConsumo {
  tipoItem?: string | null;
  cantidad?: number | null;
  unidad?: string | null;
}

/** Lo mínimo que se lee del request (`CrearSeguimientoRequest` lo cumple estructuralmente). */
export interface SeguimientoGuardadoResumible {
  mortalidadH?: number | null;
  mortalidadM?: number | null;
  selH?: number | null;
  selM?: number | null;
  huevosTotales?: number | null;
  huevoItems?: readonly { cantidad?: number | null }[] | null;
  itemsHembras?: readonly ItemConsumo[] | null;
  itemsMachos?: readonly ItemConsumo[] | null;
  consumoH?: number | null;
  unidadConsumoH?: string | null;
  consumoM?: number | null;
  unidadConsumoM?: string | null;
}

/** Número finito y positivo, o 0. */
function positivo(n: number | null | undefined): number {
  const v = Number(n);
  return Number.isFinite(v) && v > 0 ? v : 0;
}

function enKilos(cantidad: number | null | undefined, unidad: string | null | undefined): number {
  const u = (unidad ?? 'kg').trim().toLowerCase();
  const n = positivo(cantidad);
  return u === 'g' || u === 'gramo' || u === 'gramos' ? n / 1000 : n;
}

/** Huevos del registro: la suma del desglose por ítems si viaja, o el total de las categorías fijas. */
export function huevosDelGuardado(r: SeguimientoGuardadoResumible): number {
  if (r.huevoItems && r.huevoItems.length > 0) {
    return r.huevoItems.reduce((suma, i) => suma + positivo(i.cantidad), 0);
  }
  return positivo(r.huevosTotales);
}

/**
 * Kilos de ALIMENTO del registro. Con ítems suma solo los de tipo alimento (un medicamento o un accesorio no
 * son consumo de alimento); sin ítems usa el consumo escalar de hembras y machos.
 */
export function consumoKgDelGuardado(r: SeguimientoGuardadoResumible): number {
  const items = [...(r.itemsHembras ?? []), ...(r.itemsMachos ?? [])];
  if (items.length > 0) {
    return items
      .filter(i => (i.tipoItem ?? '').trim().toLowerCase() === 'alimento')
      .reduce((suma, i) => suma + enKilos(i.cantidad, i.unidad), 0);
  }
  return enKilos(r.consumoH, r.unidadConsumoH) + enKilos(r.consumoM, r.unidadConsumoM);
}

/** Entero tal cual; con decimales, hasta dos (sin ceros de relleno). */
function cifra(n: number): string {
  return String(Number(n.toFixed(2)));
}

export function resumirGuardadoSeguimiento(r: SeguimientoGuardadoResumible, esEdicion: boolean): string {
  const titulo = esEdicion ? 'Seguimiento actualizado' : 'Seguimiento creado';

  const huevos = huevosDelGuardado(r);
  const mortalidad = positivo(r.mortalidadH) + positivo(r.mortalidadM);
  const seleccion = positivo(r.selH) + positivo(r.selM);
  const consumoKg = consumoKgDelGuardado(r);

  const partes: string[] = [];
  if (huevos > 0) partes.push(`huevos ${cifra(huevos)}`);
  if (mortalidad > 0) partes.push(`mortalidad ${cifra(mortalidad)}`);
  if (seleccion > 0) partes.push(`selección ${cifra(seleccion)}`);
  if (consumoKg > 0) partes.push(`consumo ${cifra(consumoKg)} kg`);

  return partes.length > 0
    ? `${titulo}: ${partes.join(' · ')}.`
    : `${titulo} (sin huevos, mortalidad ni consumo).`;
}
