/**
 * Opciones del desplegable **Concepto** (pestañas Stock, Ingresos, Traslados y Catálogo ítems),
 * derivadas del catálogo de ítems de la empresa activa.
 *
 * La lista se armaba con un `new Set(...)` sobre el string crudo, que es **sensible a mayúsculas**,
 * mientras que todos los filtros —el local (`filterByConcept`) y el del API (`itemType` en
 * `GET /inventario-gestion/stock`)— comparan en minúsculas. Con el catálogo trayendo el mismo
 * concepto escrito de dos formas (`Otros insumos` / `Otros Insumos`), el usuario veía **dos opciones
 * que devuelven exactamente las mismas filas**.
 *
 * La migración `NormalizarConceptoCatalogoInventario` limpia los datos de hoy; esto es la red de
 * seguridad para lo que entre después (un Excel de catálogo mal capitalizado) y para las empresas
 * cuyo catálogo no conocemos. Usa la MISMA regla canónica que la migración y que el backend
 * (`EtiquetasFiltroInventarioCalculos`): **gana la variante más usada**, empate ⇒ la que ordena
 * primero. Funciones puras: sin `this`, sin DI, sin estado.
 */
import { ItemInventarioDto } from '../services/gestion-inventario.service';

/**
 * Concepto EFECTIVO de un ítem: el del catálogo y, si no tiene, su `tipoItem`. Es el mismo
 * fallback `Concepto ?? TipoItem` que aplica el backend al devolver `itemType`, así que agrupa
 * junto lo que la grilla muestra junto (167 ítems del catálogo no tienen concepto cargado).
 */
export function conceptoEfectivo(item: Pick<ItemInventarioDto, 'concepto' | 'tipoItem'>): string {
  return (item.concepto ?? item.tipoItem ?? '').trim();
}

/** Clave de agrupación: la misma normalización que usan los filtros (local y del API). */
export function normalizarConcepto(valor: string | null | undefined): string {
  return (valor ?? '').trim().toLowerCase();
}

/**
 * Opciones del desplegable: **una etiqueta por concepto**, sin importar cómo esté capitalizado en
 * el catálogo. Descarta vacíos y ordena con `localeCompare` (mismo orden que antes de este cambio).
 */
export function conceptosUnicos(items: readonly ItemInventarioDto[]): string[] {
  // 1) Cuántos ítems usa cada variante tal cual está escrita.
  const usosPorVariante = new Map<string, number>();
  for (const item of items) {
    const etiqueta = conceptoEfectivo(item);
    if (!etiqueta) continue;
    usosPorVariante.set(etiqueta, (usosPorVariante.get(etiqueta) ?? 0) + 1);
  }

  // 2) Una etiqueta por grupo normalizado: la más usada; empate ⇒ la menor en orden ordinal,
  //    para que la lista no dependa del orden en que llegaron los ítems.
  const canonicas = new Map<string, { etiqueta: string; usos: number }>();
  for (const [etiqueta, usos] of usosPorVariante) {
    const clave = normalizarConcepto(etiqueta);
    const actual = canonicas.get(clave);
    if (!actual || usos > actual.usos || (usos === actual.usos && etiqueta < actual.etiqueta)) {
      canonicas.set(clave, { etiqueta, usos });
    }
  }

  return Array.from(canonicas.values())
    .map(x => x.etiqueta)
    .sort((a, b) => a.localeCompare(b));
}
