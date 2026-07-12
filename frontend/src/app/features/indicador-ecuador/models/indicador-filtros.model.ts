/**
 * Tipos de los filtros en cascada del módulo Indicador (Ecuador / Panamá).
 *
 * Extraídos desde el componente `indicador-ecuador-list` para que las funciones puras de
 * `funciones/` puedan tiparse sin depender del componente ni provocar imports circulares.
 * La forma es idéntica a la que devuelven los endpoints `SeguimientoLoteLevante/filter-data`
 * (vista general) y `LoteReproductoraAveEngorde/filter-data` (Pollo Engorde).
 */

/** Granja del selector (id + nombre). */
export interface GranjaOption {
  id: number;
  name: string;
}

/** Núcleo del selector, asociado a una granja. */
export interface NucleoOption {
  nucleoId: string;
  nucleoNombre?: string;
  granjaId: number;
}

/** Galpón del selector, asociado a núcleo + granja. */
export interface GalponOption {
  galponId: string;
  galponNombre?: string;
  nucleoId: string;
  granjaId: number;
}

/** Lote de la vista general (reproductoras/levante). */
export interface LoteOption {
  loteId: number;
  loteNombre: string;
  granjaId: number;
  nucleoId?: string | null;
  galponId?: string | null;
  fechaEncaset?: string | null;
}

/** Misma estructura que devuelve SeguimientoLoteLevante/filter-data (granjas, núcleos, galpones, lotes en una sola llamada). */
export interface FilterDataResponse {
  farms: GranjaOption[];
  nucleos: NucleoOption[];
  galpones: GalponOption[];
  lotes: LoteOption[];
}

/** Filter-data Pollo Engorde: lote de ave engorde (LoteReproductoraAveEngorde/filter-data). */
export interface PeLoteAveEngordeItem {
  loteAveEngordeId: number;
  loteNombre: string;
  granjaId: number;
  nucleoId?: string | null;
  galponId?: string | null;
  linea?: string | null;
  fechaEncaset?: string | null;
}

/** Filter-data Pollo Engorde: granjas, núcleos, galpones y lotes ave engorde. */
export interface FilterDataPolloEngordeResponse {
  farms?: GranjaOption[];
  nucleos?: NucleoOption[];
  galpones?: GalponOption[];
  lotesAveEngorde?: PeLoteAveEngordeItem[];
}
