/**
 * Tipos de la clasificación de huevos capturada en el Seguimiento Diario de LEVANTE
 * (semana 14+, empresas con `companies.captura_huevos_en_levante = true`).
 *
 * Son los mismos 11 tipos que usa producción; viven en `models/` para que las funciones puras de
 * `funciones/` puedan tiparlos sin importar el componente (evita import circular).
 */

/** Las 11 categorías de la clasificadora fija, en el orden en que se muestran en el formulario. */
export const CLASIFICADORA_HUEVO_KEYS = [
  'huevoLimpio',
  'huevoTratado',
  'huevoSucio',
  'huevoDeforme',
  'huevoBlanco',
  'huevoDobleYema',
  'huevoPiso',
  'huevoPequeno',
  'huevoRoto',
  'huevoDesecho',
  'huevoOtro'
] as const;

export type ClasificadoraHuevoKey = (typeof CLASIFICADORA_HUEVO_KEYS)[number];

/** Valores crudos de la clasificadora tal como salen del formulario (pueden venir null/string). */
export type ValoresClasificadoraHuevo = Partial<Record<ClasificadoraHuevoKey, unknown>>;

/** Totales derivados de la clasificadora. */
export interface TotalesHuevo {
  /** Incubables = limpio + tratado. */
  incubables: number;
  /** Totales = incubables + las 9 no incubables. */
  totales: number;
}
