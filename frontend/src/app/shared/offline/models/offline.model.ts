/**
 * Tipos de la capa de consulta offline (F2).
 *
 * Viven en `models/` para que las funciones puras de `funciones/` puedan tiparse sin importar
 * servicios ni componentes (convención de CLAUDE.md).
 */

/** Identidad efectiva del usuario para particionar la caché. Los tres campos son obligatorios. */
export interface IdentidadParticion {
  userId: string | number | null | undefined;
  companyId: number | null | undefined;
  paisId: number | null | undefined;
}

/** Una respuesta guardada. */
export interface EntradaCache {
  /** Clave particionada: `{userId}|{companyId}|{paisId}|{método} {url}`. */
  clave: string;
  /** Prefijo `{userId}|{companyId}|{paisId}` — permite purgar una partición entera por índice. */
  particion: string;
  /** Cuerpo de la respuesta, tal como llegó. */
  cuerpo: unknown;
  /** Momento en que se guardó (epoch ms). */
  guardadoEn: number;
  /** URL original, solo para diagnóstico. */
  url: string;
}

/** Resultado de evaluar el TTL de una entrada. */
export type Vigencia =
  /** Dentro de la ventana: se puede servir. */
  | 'vigente'
  /** Fuera de la ventana: NO se sirve, se propaga el error de red. */
  | 'vencida';

/** Estado de la caché, para la pantalla de diagnóstico. */
export interface EstadoCacheOffline {
  disponible: boolean;
  entradas: number;
  particionActual: string | null;
  entradaMasAntigua: number | null;
  entradaMasReciente: number | null;
}
