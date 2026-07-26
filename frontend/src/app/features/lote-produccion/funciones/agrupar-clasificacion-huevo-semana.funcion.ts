// frontend/src/app/features/lote-produccion/funciones/agrupar-clasificacion-huevo-semana.funcion.ts

/**
 * FASE 5 · Clasificación de huevos por ÍTEMS (empresas con `companies.clasificacion_huevo_por_items`).
 *
 * Función PURA (sin `this`, sin DI, sin service/estado): convierte las filas planas que devuelve el
 * endpoint hermano de indicadores (`POST /api/Produccion/clasificacion-huevo-items`, una fila por
 * semana × ítem) en un índice `semana → { total, tipos[{ tipoHuevo, subtotal, items[] }] }`.
 *
 * El componente la llama UNA vez por respuesta y guarda el Map: el template lee referencias
 * estables (no aloca por ciclo de change detection).
 *
 * Reutilizable por cualquier pantalla que muestre el mismo desglose (multi-empresa).
 */
import { ClasificacionHuevoItemSemanaDto } from '../services/produccion.service';
import {
  ClasificacionHuevoItemAgrupado,
  ClasificacionHuevoSemanaAgrupada,
  ClasificacionHuevoTipoAgrupado,
  ORDEN_TIPOS_HUEVO,
  TIPO_HUEVO_SIN_CATEGORIA
} from '../models/huevo-clasificacion.model';

/** Peso de orden del tipo: los conocidos (`Primera`, `Pnc`) primero; el resto, al final. */
function pesoTipo(tipo: string): number {
  const idx = ORDEN_TIPOS_HUEVO.findIndex(t => t.toLowerCase() === tipo.toLowerCase());
  return idx === -1 ? ORDEN_TIPOS_HUEVO.length : idx;
}

/** Texto normalizado (trim) o `null` si queda vacío. */
function texto(valor: unknown): string | null {
  if (valor === null || valor === undefined) return null;
  const s = String(valor).trim();
  return s.length ? s : null;
}

export function agruparClasificacionHuevoPorSemana(
  filas: readonly ClasificacionHuevoItemSemanaDto[] | null | undefined
): Map<number, ClasificacionHuevoSemanaAgrupada> {
  // semana → tipoHuevo → claveItem → ítem acumulado
  const acumulado = new Map<number, Map<string, Map<string, ClasificacionHuevoItemAgrupado>>>();

  for (const fila of filas ?? []) {
    const semanaCruda = fila?.semana;
    // `Number(null)` es 0: sin este guard una fila sin semana crearía una "semana 0" fantasma.
    if (semanaCruda === null || semanaCruda === undefined) continue;
    const semana = Number(semanaCruda);
    if (!Number.isFinite(semana)) continue;

    const cantidad = Number(fila?.cantidad);
    if (!Number.isFinite(cantidad) || cantidad <= 0) continue; // filas en 0 → no ensucian la vista

    const tipoHuevo = texto(fila?.tipoHuevo) ?? TIPO_HUEVO_SIN_CATEGORIA;
    const codigo = texto(fila?.codigo) ?? '';
    const nombre = texto(fila?.nombre) ?? '';
    const label = codigo && nombre ? `${codigo} — ${nombre}` : (nombre || codigo || 'Ítem sin nombre');

    let porTipo = acumulado.get(semana);
    if (!porTipo) {
      porTipo = new Map<string, Map<string, ClasificacionHuevoItemAgrupado>>();
      acumulado.set(semana, porTipo);
    }

    let porItem = porTipo.get(tipoHuevo);
    if (!porItem) {
      porItem = new Map<string, ClasificacionHuevoItemAgrupado>();
      porTipo.set(tipoHuevo, porItem);
    }

    const clave = `${codigo}|${nombre}`;
    const previo = porItem.get(clave);
    if (previo) previo.cantidad += cantidad;
    else porItem.set(clave, { codigo, nombre, cantidad, label });
  }

  const resultado = new Map<number, ClasificacionHuevoSemanaAgrupada>();
  for (const [semana, porTipo] of acumulado) {
    const tipos: ClasificacionHuevoTipoAgrupado[] = Array.from(porTipo.entries())
      .map(([tipoHuevo, porItem]) => {
        const items = Array.from(porItem.values()).sort((a, b) => a.label.localeCompare(b.label));
        return {
          tipoHuevo,
          subtotal: items.reduce((acc, it) => acc + it.cantidad, 0),
          items
        };
      })
      .sort((a, b) => {
        const diff = pesoTipo(a.tipoHuevo) - pesoTipo(b.tipoHuevo);
        return diff !== 0 ? diff : a.tipoHuevo.localeCompare(b.tipoHuevo);
      });

    resultado.set(semana, {
      semana,
      total: tipos.reduce((acc, t) => acc + t.subtotal, 0),
      tipos
    });
  }

  return resultado;
}
