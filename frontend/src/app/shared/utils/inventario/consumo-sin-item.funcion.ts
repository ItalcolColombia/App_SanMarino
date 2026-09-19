// frontend/src/app/shared/utils/inventario/consumo-sin-item.funcion.ts
// Regla compartida por los seguimientos diarios de levante y de producción: una fila de consumo con
// cantidad tiene que decir QUÉ producto es. Funciones PURAS (sin DI, sin `this`) — las usan los dos
// modales, que arman el request con el mismo criterio.
//
// Por qué existe. Al armar el request, cada modal descarta en silencio las filas sin ítem (sin producto
// no hay id que descontar). Si el operario tecleaba la cantidad y se olvidaba de elegir el ítem, el
// registro se guardaba SIN ese consumo —`cons_kg = 0`, sin movimiento de inventario— y nadie se enteraba:
// producción #676, sep-2026. La regla no depende de ningún flag de empresa: una cantidad sin producto es
// inválida en todas partes; el flag `permite_seguimiento_diario_parcial` relaja qué se puede dejar VACÍO,
// no permite PERDER una cantidad ya tecleada.

/** Una fila de consumo tal como está en el formulario. */
export interface FilaConsumo {
  /** Valor del desplegable «Ítem»: id positivo si eligió; `null` / `''` / `0` / `undefined` si no. */
  catalogItemId: unknown;
  cantidad: unknown;
  /**
   * El operario interactuó con la fila en esta sesión (`FormGroup.dirty`).
   *
   * Una fila que solo se HIDRATÓ desde el registro guardado no cuenta. Un registro cargado por migración
   * masiva o por la app móvil guarda su consumo como escalar, sin ítem: al abrirlo, el modal arma una fila
   * con esa cantidad y sin ítem. Hoy se puede corregir la mortalidad de ese registro y el escalar se
   * conserva; exigirle un ítem inventaría una salida de inventario nueva solo por tocar otra cosa.
   * En cuanto el operario toca la fila deja de ser un escalar heredado y aplica la regla completa.
   */
  editadaPorElOperario: boolean;
}

/** Un bloque del formulario: «Hembras», «Machos», «Ítems generales». */
export interface BloqueConsumo {
  /** Rótulo para el mensaje. */
  nombre: string;
  filas: readonly (FilaConsumo | null | undefined)[];
}

/** Dónde está una fila con cantidad y sin ítem. */
export interface FilaSinItem {
  bloque: string;
  /** Posición de la fila dentro de su bloque, desde 1. */
  fila: number;
  /** El bloque tiene más de una fila: el mensaje tiene que decir cuál. */
  enBloqueDeVarias: boolean;
}

/** Aviso corto que se pinta bajo el select «Ítem» de la fila afectada (una sola fuente para las plantillas). */
export const MENSAJE_ITEM_REQUERIDO_EN_FILA = 'Elija el ítem: hay una cantidad sin producto.';

/** Aviso del pie del modal: se ve desde cualquier pestaña, no solo desde la que tiene la fila. */
export const MENSAJE_ITEM_REQUERIDO_EN_PIE =
  'Hay una cantidad sin ítem (pestaña General). Elija el producto o deje la cantidad en 0.';

/**
 * True si la fila declara una cantidad positiva pero no eligió ítem, y el operario la tocó.
 *
 * Cantidad 0, vacía, negativa o no numérica no cuenta: es una fila sin usar. El ítem se compara como
 * número porque el desplegable entrega ids numéricos, pero un id llegado como texto también vale.
 */
export function filaTieneCantidadSinItem(fila: FilaConsumo | null | undefined): boolean {
  if (!fila?.editadaPorElOperario) return false;
  if (!(Number(fila.cantidad) > 0)) return false;
  return !(Number(fila.catalogItemId) > 0);
}

/**
 * Filas con cantidad y sin ítem de todos los bloques, en el orden en que aparecen (bloque y luego fila).
 * Tolera bloques o filas nulas: el llamador arma los bloques desde FormArrays que pueden estar vacíos.
 */
export function filasConCantidadSinItem(bloques: readonly BloqueConsumo[] | null | undefined): FilaSinItem[] {
  const resultado: FilaSinItem[] = [];
  for (const bloque of bloques ?? []) {
    const filas = bloque?.filas ?? [];
    filas.forEach((fila, indice) => {
      if (filaTieneCantidadSinItem(fila)) {
        resultado.push({ bloque: bloque.nombre, fila: indice + 1, enBloqueDeVarias: filas.length > 1 });
      }
    });
  }
  return resultado;
}

/**
 * Texto para el toast de `onSave()`: nombra el bloque y, si el bloque tiene varias filas, cuál.
 * Sin filas afectadas devuelve cadena vacía.
 */
export function mensajeCantidadSinItem(pendientes: readonly FilaSinItem[]): string {
  if (pendientes.length === 0) return '';
  const donde = [...new Set(pendientes.map(p => (p.enBloqueDeVarias ? `${p.bloque} (fila ${p.fila})` : p.bloque)))];
  return pendientes.length === 1
    ? `Falta elegir el ítem de ${donde[0]}: ingresó una cantidad sin producto. Seleccione el ítem o deje la cantidad en 0.`
    : `Falta elegir el ítem de ${donde.join(', ')}: ingresó cantidades sin producto. Seleccione el ítem de cada una o deje la cantidad en 0.`;
}
