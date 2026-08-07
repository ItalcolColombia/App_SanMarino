/**
 * Rango de fechas del reporte de **Gastos de inventario** — funciones PURAS
 * (sin `this`, sin DI, sin `Date.now()` adentro: el "hoy" entra por parámetro para poder testearlas).
 *
 * El usuario pidió elegir «de qué fecha hasta qué fecha» necesita el consumo, para no bajar todo el
 * histórico en cada descarga. El backend ya filtraba por rango (`fechaDesde`/`fechaHasta` en
 * `search`, `export` y `existencias`); acá vive lo que la pantalla necesita para armarlo:
 * atajos de rango, validación y el sufijo del nombre de archivo.
 *
 * Las fechas se manejan como `yyyy-MM-dd` literal (el formato del `<input type="date">` y el de la
 * columna `date` del backend). Comparar dos `yyyy-MM-dd` como strings equivale a compararlas como
 * fechas, así que no hace falta parsear: no hay corrimiento de zona horaria posible.
 */

/** Rango con extremos en `yyyy-MM-dd` (ambos inclusivos, igual que el filtro del backend). */
export interface RangoFechasGastos {
  desde: string;
  hasta: string;
}

/** Atajos ofrecidos en la tarjeta de filtros. */
export type RangoPresetGastos = 'mesActual' | 'mesAnterior' | 'ultimos30' | 'anioActual';

/** Etiqueta visible de cada atajo (la pantalla itera este orden). */
export const PRESETS_RANGO_GASTOS: ReadonlyArray<{ preset: RangoPresetGastos; label: string }> = [
  { preset: 'mesActual', label: 'Este mes' },
  { preset: 'mesAnterior', label: 'Mes anterior' },
  { preset: 'ultimos30', label: 'Últimos 30 días' },
  { preset: 'anioActual', label: 'Este año' }
];

/**
 * `yyyy-MM-dd` de una fecha usando sus componentes **locales**.
 * No sirve `toISOString()`: convierte a UTC y en Bogotá (-05) devolvería el día anterior.
 */
export function ymdLocal(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

/**
 * Extremos del atajo pedido, tomando `hoy` como referencia (calendario local del usuario).
 * `new Date(anio, mes, 0)` da el último día del mes anterior, así que 28/29/30/31 salen solos.
 */
export function calcularRangoPreset(preset: RangoPresetGastos, hoy: Date): RangoFechasGastos {
  const y = hoy.getFullYear();
  const m = hoy.getMonth();
  const d = hoy.getDate();

  switch (preset) {
    case 'mesActual':
      return { desde: ymdLocal(new Date(y, m, 1)), hasta: ymdLocal(hoy) };
    case 'mesAnterior':
      return { desde: ymdLocal(new Date(y, m - 1, 1)), hasta: ymdLocal(new Date(y, m, 0)) };
    case 'ultimos30':
      // 30 días CONTANDO hoy (hoy − 29 … hoy).
      return { desde: ymdLocal(new Date(y, m, d - 29)), hasta: ymdLocal(hoy) };
    case 'anioActual':
      return { desde: ymdLocal(new Date(y, 0, 1)), hasta: ymdLocal(hoy) };
  }
}

/**
 * Mensaje de error del rango, o `null` si es usable.
 * Rango vacío es válido (= todos los consumos, el comportamiento por defecto del módulo);
 * solo una de las dos también (desde-en-adelante / hasta-esa-fecha).
 */
export function validarRangoFechas(desde: string | null | undefined, hasta: string | null | undefined): string | null {
  const d = (desde ?? '').trim();
  const h = (hasta ?? '').trim();
  if (!d || !h) return null;
  if (d > h) return 'La fecha «Desde» no puede ser mayor que la fecha «Hasta».';
  return null;
}

/**
 * Sufijo del nombre del archivo para que dos descargas de rangos distintos no se pisen
 * (el sello `_YYYYMMDD` que agrega el helper compartido es el día de descarga, no el del rango).
 * Sin rango devuelve `''` ⇒ el nombre queda idéntico al que se generaba antes de esta mejora.
 */
export function sufijoArchivoRango(desde: string | null | undefined, hasta: string | null | undefined): string {
  const d = (desde ?? '').trim();
  const h = (hasta ?? '').trim();
  if (d && h) return `_${d}_a_${h}`;
  if (d) return `_desde_${d}`;
  if (h) return `_hasta_${h}`;
  return '';
}
