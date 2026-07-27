// frontend/src/app/features/lote-produccion/funciones/resumir-huevo-items-por-tipo.funcion.ts

/**
 * FASE 5 · Clasificación de huevos por ÍTEMS (empresas con `companies.clasificacion_huevo_por_items`).
 *
 * Función PURA (sin `this`, sin DI, sin service/estado): resume las filas de
 * `seguimiento_diario_produccion.metadata->'huevoItems'` de UN registro diario en los totales
 * `Primera` / `Pnc` que muestran las grillas (reemplazan a las 11 columnas fijas, siempre en 0
 * para estas empresas).
 *
 * Los ítems con `tipoHuevo` desconocido NO se suman a Primera ni a Pnc (quedan en `otros`);
 * el total del día lo sigue mandando `huevo_tot` del registro.
 */
import { HuevoItemClasificable, ResumenHuevoPorTipo } from '../models/huevo-clasificacion.model';

const TIPO_PRIMERA = 'primera';
const TIPO_PNC = 'pnc';

export function resumirHuevoItemsPorTipo(
  items: readonly HuevoItemClasificable[] | null | undefined
): ResumenHuevoPorTipo {
  let primera = 0;
  let pnc = 0;
  let otros = 0;

  for (const item of items ?? []) {
    const cantidad = Number(item?.cantidad);
    if (!Number.isFinite(cantidad) || cantidad <= 0) continue;

    const tipo = (item?.tipoHuevo ?? '').trim().toLowerCase();
    if (tipo === TIPO_PRIMERA) primera += cantidad;
    else if (tipo === TIPO_PNC) pnc += cantidad;
    else otros += cantidad;
  }

  return { primera, pnc, otros, total: primera + pnc + otros };
}
