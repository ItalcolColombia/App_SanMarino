// src/app/features/config/guia-genetica-santa-reyes/funciones/normalizar-filtros.funcion.ts
/**
 * Normaliza lo que el usuario tipeó en la barra de filtros antes de mandarlo al backend.
 * Función **pura**: sin `this`, sin DI, sin HTTP.
 */
import { GuiaGeneticaSantaReyesFiltros } from '../models/guia-genetica-santa-reyes.model';

/** Lo que se tipea en los inputs del filtro (todo texto). */
export interface FiltrosCrudosGuia {
  raza: string;
  anioGuia: string;
  edadDesde: string;
  edadHasta: string;
}

/** Entero de un input de texto; `null` si está vacío o no es un número entero válido. */
function aEnteroOpcional(texto: string | null | undefined): number | null {
  const limpio = (texto ?? '').trim();
  if (!limpio) return null;
  const n = Number(limpio);
  return Number.isInteger(n) ? n : null;
}

/**
 * Filtros crudos ⇒ filtros del request.
 *
 * 🔴 **Un rango invertido se corrige, no se manda.** «desde 60 hasta 30» es un error de tipeo
 * frecuente y el backend lo traduciría a `edad >= 60 AND edad <= 30` ⇒ **cero filas sin decir por
 * qué**. Acá se ordena el par, así el usuario ve las semanas 30–60 que quería.
 *
 * Un valor no numérico se descarta (queda `null` ⇒ el parámetro no viaja), en vez de mandar `NaN`.
 */
export function normalizarFiltrosGuia(
  crudos: FiltrosCrudosGuia,
  page: number,
  pageSize: number,
  sortBy?: string,
  sortDesc?: boolean
): GuiaGeneticaSantaReyesFiltros {
  const desde = aEnteroOpcional(crudos.edadDesde);
  const hasta = aEnteroOpcional(crudos.edadHasta);

  const rangoInvertido = desde !== null && hasta !== null && desde > hasta;

  return {
    raza: crudos.raza?.trim() || undefined,
    anioGuia: crudos.anioGuia?.trim() || undefined,
    edadDesde: rangoInvertido ? hasta : desde,
    edadHasta: rangoInvertido ? desde : hasta,
    page: Number.isInteger(page) && page > 0 ? page : 1,
    pageSize,
    sortBy,
    sortDesc
  };
}

/** ¿Hay algún filtro activo? (para pintar el botón «Limpiar» sólo cuando sirve). */
export function hayFiltrosActivos(crudos: FiltrosCrudosGuia): boolean {
  return Boolean(
    crudos.raza?.trim() ||
    crudos.anioGuia?.trim() ||
    crudos.edadDesde?.trim() ||
    crudos.edadHasta?.trim()
  );
}

/** Descripción legible de los filtros, para el encabezado del `.xlsx` exportado. */
export function describirFiltrosGuia(filtros: GuiaGeneticaSantaReyesFiltros): string {
  const partes: string[] = [];
  if (filtros.raza) partes.push(`Raza: ${filtros.raza}`);
  if (filtros.anioGuia) partes.push(`Año: ${filtros.anioGuia}`);
  if (filtros.edadDesde != null || filtros.edadHasta != null) {
    partes.push(`Semanas: ${filtros.edadDesde ?? '·'} a ${filtros.edadHasta ?? '·'}`);
  }
  return partes.length ? partes.join(' · ') : 'Sin filtros';
}
