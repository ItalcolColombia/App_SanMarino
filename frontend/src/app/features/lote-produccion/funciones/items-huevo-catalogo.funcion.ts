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
  HuevoFilaFija,
  HuevoGrupoFilasFijas,
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

/** Lee una clave booleana de la metadata del catálogo (tolerando camelCase / snake_case). */
function leerMetaBool(metadata: unknown, ...claves: string[]): boolean {
  if (!metadata || typeof metadata !== 'object') return false;
  const obj = metadata as Record<string, unknown>;
  for (const clave of claves) {
    if (obj[clave] === true) return true;
  }
  return false;
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
      primeraPostura: leerMetaBool(item.metadata, 'primeraPostura', 'primera_postura'),
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
      // El desglose guardado no persiste `primeraPostura`: un ítem ya elegido y guardado se
      // mantiene editable siempre, la vigencia solo decide qué se OFRECE como opción nueva.
      primeraPostura: false,
      label: codigo && nombre ? `${codigo} — ${nombre}` : (nombre || codigo || `Ítem ${id}`)
    });
  }
  return resultado;
}

/**
 * ¿Sigue vigente el ítem «Huevo de primera postura» a esta semana de vida del lote?
 * Espejo de `HuevoPrimeraPosturaCalculos.EsVigente` (backend) — mismo criterio fail-open: sin
 * límite configurado o sin semana de vida calculable, no se oculta nada.
 */
export function esVigentePrimeraPostura(hastaSemana: number | null, semanaVida: number | null): boolean {
  if (hastaSemana == null) return true;
  if (semanaVida == null) return true;
  return semanaVida <= hastaSemana;
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


// ===================== F7.3 · FILAS FIJAS =====================

/** Peso de ordenamiento de un tipo: los conocidos primero, lo desconocido al final. */
function pesoTipo(tipo: string): number {
  const idx = ORDEN_TIPOS_HUEVO.findIndex(t => t.toLowerCase() === tipo.toLowerCase());
  return idx === -1 ? ORDEN_TIPOS_HUEVO.length : idx;
}

/**
 * F7.3 — arma las filas FIJAS del seguimiento diario a partir de los ítems que el LOTE declaró
 * producir. Reemplaza al `<select>` + «agregar ítem»: el conjunto ya no lo elige el operario.
 *
 * Espejo de `HuevoItemsCalculos.PesoTipoHuevo` (backend) en el orden de los grupos.
 *
 * @param declarados Ítems que el lote declara (`GET /api/LoteHuevoItem/{loteId}`).
 * @param guardados  Ítems que el registro que se está EDITANDO ya tenía. Los que no estén entre los
 *                   declarados se agregan igual, marcados `huerfano`: el lote pudo cambiar su
 *                   declaración después de que el registro se guardó, y perder ese dato en silencio
 *                   sería peor que mostrarlo con una marca.
 * @param semanaVida Semana de vida del lote a la fecha del registro, para la vigencia de primera
 *                   postura (F7.4). `null` = sin fecha calculable ⇒ no se bloquea nada.
 * @param huevoPrimeraPosturaHastaSemana Límite de la empresa. `null` = sin regla.
 */
export function construirFilasFijasHuevo(
  declarados: readonly HuevoFilaFija[],
  guardados: readonly { catalogItemId: number; codigo?: string | null; nombre?: string | null; tipoHuevo?: string | null; um?: string | null }[],
  semanaVida: number | null,
  huevoPrimeraPosturaHastaSemana: number | null
): HuevoGrupoFilasFijas[] {
  const filas: HuevoFilaFija[] = declarados.map(d => ({
    ...d,
    huerfano: false,
    fueraDeVigencia: d.primeraPostura && !esVigentePrimeraPostura(huevoPrimeraPosturaHastaSemana, semanaVida)
  }));

  const ids = new Set(filas.map(f => f.catalogItemId));
  for (const g of guardados ?? []) {
    const id = Number(g?.catalogItemId) || 0;
    if (id <= 0 || ids.has(id)) continue;
    ids.add(id);
    filas.push({
      catalogItemId: id,
      codigo: String(g.codigo ?? '').trim(),
      nombre: String(g.nombre ?? '').trim() || `Ítem ${id}`,
      tipoHuevo: g.tipoHuevo?.trim() || null,
      um: g.um?.trim() || null,
      // Un ítem huérfano NO se marca como primera postura: no se conoce su metadata actual y la
      // vigencia solo decide qué se OFRECE, nunca bloquea lo ya guardado.
      primeraPostura: false,
      huerfano: true,
      fueraDeVigencia: false
    });
  }

  const grupos = new Map<string, HuevoFilaFija[]>();
  for (const f of filas) {
    const clave = f.tipoHuevo?.trim() || TIPO_HUEVO_SIN_CATEGORIA;
    const lista = grupos.get(clave);
    if (lista) lista.push(f);
    else grupos.set(clave, [f]);
  }

  return Array.from(grupos.entries())
    .map(([tipoHuevo, fs]) => ({
      tipoHuevo,
      filas: [...fs].sort((a, b) => a.nombre.localeCompare(b.nombre))
    }))
    .sort((a, b) => {
      const diff = pesoTipo(a.tipoHuevo) - pesoTipo(b.tipoHuevo);
      return diff !== 0 ? diff : a.tipoHuevo.localeCompare(b.tipoHuevo);
    });
}

/**
 * D2 — ¿este ítem se mide en kilos? Los que sí admiten decimales en el input; los de unidades no.
 * El catálogo de Santa Reyes tiene `HUEVO RECUPERACION BOLSA KIL` con `um = 'KIL'`: se pesa, no se
 * cuenta, y hasta acá el front redondeaba 12,5 kg a 13 en silencio.
 */
export function esItemEnKilos(um: string | null | undefined): boolean {
  return (um ?? '').trim().toUpperCase() === 'KIL';
}
