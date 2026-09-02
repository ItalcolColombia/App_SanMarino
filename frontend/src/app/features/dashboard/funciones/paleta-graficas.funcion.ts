// features/dashboard/funciones/paleta-graficas.funcion.ts
//
// Los colores de las gráficas, en un solo lugar y por ROL semántico.
//
// ## Por qué no se eligen en el componente
//
// El dashboard viejo tenía un `CORPORATE_COLORS` inventado (amarillo `#f59e0b` como primario, rojo
// `#ef4444` como secundario, gris como acento) que contradecía la regla de marca del repo. Acá los
// valores salen de los tokens de `styles/theme-italfoods.scss` y el componente pide un ROL, no un
// color: así una serie de mortalidad no puede terminar pintada de verde por descuido.
//
// ⚠️ Son constantes y no `var(--ital-orange)` porque Chart.js pinta sobre `<canvas>` y necesita un
// color resuelto. Si un token cambia en el SCSS, se cambia acá también — es el precio de que estas
// funciones sean puras y testeables.

import { RolSerie } from '../models/dashboard-metricas.model';

/** Tokens de marca, copiados de `styles/theme-italfoods.scss` (`:root`). */
export const COLORES_MARCA = Object.freeze({
  /** `--ital-orange`. Marca, estructura y el dato principal. */
  naranja: '#F5821F',
  /** `--ital-orange-light`. */
  naranjaClaro: '#FBB040',
  /** `--ital-orange-dark`. */
  naranjaOscuro: '#C85A0E',
  /** `--success`. SOLO éxito. */
  exito: '#16A34A',
  /** `--danger`. SOLO peligro/alerta. */
  peligro: '#DC2626',
  /** `--ital-muted`. Referencias y ejes. */
  neutro: '#6B7280',
  /** `--ital-text`. */
  texto: '#1C1917'
});

/** Color de línea/barra por rol semántico. */
export const COLOR_POR_ROL: Readonly<Record<RolSerie, string>> = Object.freeze({
  principal: COLORES_MARCA.naranja,
  secundaria: COLORES_MARCA.naranjaOscuro,
  referencia: COLORES_MARCA.neutro,
  alerta: COLORES_MARCA.peligro,
  exito: COLORES_MARCA.exito
});

/**
 * Paleta categórica para distribuciones (una porción por granja, por lote, por concepto).
 *
 * Arranca en los naranjas de marca y sigue con tonos que se distinguen entre sí en pantalla y en
 * escala de grises. **No incluye el rojo ni el verde semánticos**: en una torta de granjas, una
 * porción roja se lee como «esta granja está mal» y no significa nada de eso.
 */
export const PALETA_CATEGORICA: readonly string[] = Object.freeze([
  '#F5821F', // naranja marca
  '#C85A0E', // naranja oscuro
  '#FBB040', // naranja claro
  '#8B5CF6', // violeta
  '#0891B2', // cian
  '#65A30D', // lima
  '#DB2777', // fucsia
  '#475569', // pizarra
  '#B45309', // ámbar oscuro
  '#0E7490' // teal
]);

/**
 * Color de la porción `indice`. Cicla si hay más categorías que colores — repetir un color es
 * preferible a generar uno aleatorio, que cambiaría entre recargas para el mismo dato.
 */
export function colorCategoria(indice: number): string {
  const n = PALETA_CATEGORICA.length;
  const i = ((indice % n) + n) % n; // tolera índices negativos
  return PALETA_CATEGORICA[i];
}

/** Los `cantidad` primeros colores de la paleta, ciclando. */
export function coloresCategorias(cantidad: number): string[] {
  if (!Number.isFinite(cantidad) || cantidad <= 0) return [];
  return Array.from({ length: Math.floor(cantidad) }, (_, i) => colorCategoria(i));
}
