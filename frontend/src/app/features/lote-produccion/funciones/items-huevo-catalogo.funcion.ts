// frontend/src/app/features/lote-produccion/funciones/items-huevo-catalogo.funcion.ts

/**
 * Clasificación de huevos por ÍTEMS (Santa Reyes) — funciones PURAS: sin `this`, sin DI,
 * sin service/toast/estado. El modal de seguimiento diario de producción las usa para
 * transformar el catálogo (`catalogo_items` con `item_type='huevo'`) en opciones agrupadas
 * por `Primera`/`Pnc` y para sumar las cantidades cargadas.
 *
 * Reutilizable por cualquier otra pantalla que exponga la misma clasificación (multi-empresa).
 */
import { CatalogItemDto } from '../../catalogo-alimentos/services/catalogo-alimentos.service';
import { HuevoItemSeguimiento } from '../services/produccion.service';
import {
  HuevoCatalogGrupo,
  HuevoCatalogOption,
  ORDEN_TIPOS_HUEVO,
  TIPO_HUEVO_SIN_CATEGORIA
} from '../models/huevo-clasificacion.model';

/** Lee una clave de la metadata del catálogo tolerando camelCase / snake_case y valores vacíos. */
function leerMeta(metadata: unknown, ...claves: string[]): string | null {
  if (!metadata || typeof metadata !== 'object') return null;
  const obj = metadata as Record<string, unknown>;
  for (const clave of claves) {
    const v = obj[clave];
    if (v === null || v === undefined) continue;
    const s = String(v).trim();
    if (s.length) return s;
  }
  return null;
}

/**
 * Convierte los ítems del catálogo (respuesta de `GET /api/catalogo-alimentos/filter?typeItem=huevo`)
 * en opciones del select. Descarta los que no tengan id.
 */
export function mapearItemsHuevoACatalogo(items: readonly CatalogItemDto[]): HuevoCatalogOption[] {
  const opciones: HuevoCatalogOption[] = [];
  for (const item of items ?? []) {
    const id = Number(item?.id) || 0;
    if (id <= 0) continue;
    const codigo = String(item.codigo ?? '').trim();
    const nombre = String(item.nombre ?? '').trim();
    opciones.push({
      id,
      codigo,
      nombre,
      tipoHuevo: leerMeta(item.metadata, 'tipoHuevo', 'tipo_huevo'),
      um: leerMeta(item.metadata, 'um', 'UM', 'unidadMedida'),
      label: codigo && nombre ? `${codigo} — ${nombre}` : (nombre || codigo || `Ítem ${id}`)
    });
  }
  return opciones;
}

/**
 * Suma las opciones del catálogo con los ítems ya guardados en el registro que ya no estén en él
 * (ítem desactivado/eliminado del catálogo): así la edición sigue mostrando lo que se guardó.
 */
export function fusionarItemsHuevoGuardados(
  opciones: readonly HuevoCatalogOption[],
  guardados: readonly HuevoItemSeguimiento[]
): HuevoCatalogOption[] {
  const resultado = [...opciones];
  const ids = new Set(resultado.map(o => o.id));
  for (const g of guardados ?? []) {
    const id = Number(g?.catalogItemId) || 0;
    if (id <= 0 || ids.has(id)) continue;
    ids.add(id);
    const codigo = String(g.codigo ?? '').trim();
    const nombre = String(g.nombre ?? '').trim();
    resultado.push({
      id,
      codigo,
      nombre,
      tipoHuevo: g.tipoHuevo?.trim() || null,
      um: g.um?.trim() || null,
      label: codigo && nombre ? `${codigo} — ${nombre}` : (nombre || codigo || `Ítem ${id}`)
    });
  }
  return resultado;
}

/**
 * Agrupa las opciones por tipo de huevo para los `<optgroup>` del select.
 * Orden: los tipos conocidos (`Primera`, `Pnc`) primero y el resto alfabético; dentro de cada
 * grupo, por código/nombre.
 */
export function agruparItemsHuevoPorTipo(opciones: readonly HuevoCatalogOption[]): HuevoCatalogGrupo[] {
  const grupos = new Map<string, HuevoCatalogOption[]>();
  for (const op of opciones ?? []) {
    const clave = op.tipoHuevo?.trim() || TIPO_HUEVO_SIN_CATEGORIA;
    const lista = grupos.get(clave);
    if (lista) lista.push(op);
    else grupos.set(clave, [op]);
  }

  const peso = (tipo: string): number => {
    const idx = ORDEN_TIPOS_HUEVO.findIndex(t => t.toLowerCase() === tipo.toLowerCase());
    return idx === -1 ? ORDEN_TIPOS_HUEVO.length : idx;
  };

  return Array.from(grupos.entries())
    .map(([tipoHuevo, items]) => ({
      tipoHuevo,
      items: [...items].sort((a, b) => a.label.localeCompare(b.label))
    }))
    .sort((a, b) => {
      const diff = peso(a.tipoHuevo) - peso(b.tipoHuevo);
      return diff !== 0 ? diff : a.tipoHuevo.localeCompare(b.tipoHuevo);
    });
}

/** Suma defensiva de las cantidades cargadas (ignora null/vacío/NaN y negativos). */
export function sumarCantidadesHuevo(cantidades: readonly unknown[]): number {
  let total = 0;
  for (const c of cantidades ?? []) {
    const n = Number(c);
    if (!isFinite(n) || n <= 0) continue;
    total += n;
  }
  return total;
}
