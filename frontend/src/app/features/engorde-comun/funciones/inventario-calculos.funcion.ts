/**
 * Cálculos puros de inventario/cantidades para el modal de seguimiento engorde.
 * Sin `this`, sin DI, sin estado de Angular. Aritmética idéntica al comportamiento previo
 * (mismos `Math.round`, mismo orden, mismo manejo de null).
 */

/** Número o null desde un valor de formulario que puede venir vacío. */
export function toNumOrNull(v: any): number | null {
  if (v === null || v === undefined || v === '') return null;
  const n = typeof v === 'number' ? v : Number(v);
  return isNaN(n) ? null : n;
}

/** Convierte cantidad a kg para validar contra inventario. */
export function toKg(cantidad: number, unidad: string | null | undefined): number {
  const u = String(unidad || 'kg').trim().toLowerCase();
  if (u === 'g' || u === 'gramo' || u === 'gramos') return cantidad / 1000;
  return cantidad;
}

/** True si la unidad no es una variante conocida de kg/g (para conservar el `console.warn` legacy). */
export function esUnidadDesconocidaParaGramos(unidad: string): boolean {
  const u = unidad.toLowerCase();
  return !(
    u === 'kg' || u === 'kilogramos' || u === 'kilogramo' ||
    u === 'g' || u === 'gramos' || u === 'gramo'
  );
}

/**
 * Convierte la cantidad original del inventario a gramos para mostrar en la interfaz.
 * kg → *1000; g → tal cual; unidad desconocida → se asume kg (*1000). Redondea con Math.round.
 */
export function cantidadOriginalAGramos(cantidadOriginal: number, unidad: string): number {
  const unidadLower = unidad.toLowerCase();
  if (unidadLower === 'kg' || unidadLower === 'kilogramos' || unidadLower === 'kilogramo') {
    return Math.round(cantidadOriginal * 1000);
  } else if (unidadLower === 'g' || unidadLower === 'gramos' || unidadLower === 'gramo') {
    return Math.round(cantidadOriginal);
  }
  return Math.round(cantidadOriginal * 1000);
}

/** Normaliza un id de catálogo seleccionado a un número positivo o null. */
export function normalizarIdCatalogoSeleccion(v: number | string | null | undefined): number | null {
  if (v == null || v === '') return null;
  const n = typeof v === 'string' ? parseInt(v, 10) : Number(v);
  return Number.isFinite(n) && n > 0 ? n : null;
}
