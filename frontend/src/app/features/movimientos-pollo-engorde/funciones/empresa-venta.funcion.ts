/**
 * «Empresa de venta» del despacho de pollo engorde (a quién se vendió o se envió).
 *
 * Las opciones salen de la lista maestra `venta_pollo_engorde_empresa` de la empresa activa (por defecto
 * «Planta») y el valor elegido se guarda como TEXTO en `plantaDestino`: los ids de las opciones de una lista
 * maestra cambian cada vez que se edita la lista, así que el texto es la identidad estable.
 *
 * Funciones puras (sin estado de Angular): las usan la lista, los dos modales de venta y el Excel.
 */

/**
 * Valor del filtro «Sin empresa» (ventas que no tienen empresa guardada: anteriores a este campo o cargadas sin
 * ella). No puede chocar con una empresa real: las opciones de la lista maestra no llevan guiones bajos dobles.
 */
export const SIN_EMPRESA_VENTA = '__SIN_EMPRESA__';

/** Texto que se muestra cuando las líneas de un mismo despacho tienen empresas distintas. */
export const VARIAS_EMPRESAS_VENTA = 'Varias';

/** Clave de comparación: sin espacios en los bordes y sin distinguir mayúsculas de minúsculas. */
export function claveEmpresaVenta(valor: string | null | undefined): string {
  return (valor ?? '').trim().toLowerCase();
}

/**
 * Une las fuentes de empresas en una sola lista SIN repetidos (por clave: ignora mayúsculas y espacios) y sin
 * vacíos. Conserva el orden: primero lo que trae la primera fuente (la lista maestra, en su orden), luego lo que
 * aporten las siguientes en el orden en que aparecen. Devuelve el texto ya recortado de la PRIMERA aparición.
 *
 * Sirve para que una empresa guardada en una venta vieja (o renombrada/borrada de la lista después) siga
 * apareciendo como opción y como filtro, en vez de desaparecer de la pantalla.
 */
export function unirOpcionesEmpresaVenta(
  ...fuentes: ReadonlyArray<ReadonlyArray<string | null | undefined>>
): string[] {
  const vistas = new Set<string>();
  const resultado: string[] = [];
  for (const fuente of fuentes) {
    for (const valor of fuente ?? []) {
      const texto = (valor ?? '').trim();
      const clave = texto.toLowerCase();
      if (!clave || vistas.has(clave)) continue;
      vistas.add(clave);
      resultado.push(texto);
    }
  }
  return resultado;
}

/**
 * ¿Una fila entra en el filtro de empresa de la tabla?
 * - Sin filtro (`''`) entran todas.
 * - `SIN_EMPRESA_VENTA` entran solo las VENTAS sin empresa (un traslado nunca tiene empresa de venta, así que no
 *   cuenta como «sin empresa»).
 * - Cualquier otro valor: coincidencia exacta de la empresa, sin distinguir mayúsculas ni espacios.
 */
export function coincideEmpresaVenta(
  tipoMovimiento: string | null | undefined,
  plantaDestino: string | null | undefined,
  filtro: string | null | undefined
): boolean {
  const f = (filtro ?? '').trim();
  if (!f) return true;
  if (f === SIN_EMPRESA_VENTA) return tipoMovimiento === 'Venta' && claveEmpresaVenta(plantaDestino) === '';
  return claveEmpresaVenta(plantaDestino) === claveEmpresaVenta(f);
}

/**
 * Empresa que se muestra para un despacho (todas las líneas del mismo viaje):
 * - todas con la misma empresa → ese texto;
 * - empresas distintas, o unas con empresa y otras sin ella → «Varias» (mostrar solo una escondería la diferencia);
 * - ninguna con empresa → `null`.
 */
export function resumenEmpresaVenta(
  movimientos: ReadonlyArray<{ plantaDestino?: string | null }>
): string | null {
  if (!movimientos.length) return null;
  const claves = new Set(movimientos.map((m) => claveEmpresaVenta(m.plantaDestino)));
  if (claves.size > 1) return VARIAS_EMPRESAS_VENTA;
  const unica = [...claves][0];
  if (!unica) return null;
  return (movimientos[0].plantaDestino ?? '').trim();
}
