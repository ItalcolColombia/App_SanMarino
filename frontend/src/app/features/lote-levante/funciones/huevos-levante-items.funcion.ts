/**
 * Huevos en LEVANTE por los TIPOS declarados del lote (Santa Reyes, capacitación 14-sep-2026).
 *
 * Funciones PURAS (sin `this`, sin DI, sin estado). El backend es el autoritativo
 * (`HuevosLevanteCalculos.ResolverModo` + gate de `SeguimientoLoteLevanteService`): acá solo se
 * decide qué muestra el tab «Huevos» y qué viaja en el payload.
 *
 * Reutiliza las filas fijas de producción (`construirFilasFijasHuevo`): los dos seguimientos ofrecen
 * exactamente los mismos tipos del lote, con la misma vigencia de primera postura.
 */
import { LoteHuevoItemDto } from '../../lote/services/lote-huevo-items.service';
import { HuevoItemSeguimiento } from '../../lote-produccion/services/produccion.service';
import { HuevoFilaFija, HuevoGrupoFilasFijas } from '../../lote-produccion/models/huevo-clasificacion.model';
import { esItemEnKilos } from '../../lote-produccion/funciones/items-huevo-catalogo.funcion';
import { permiteHuevosEnLevante } from './semana-vida-levante.funcion';

/** Qué captura el tab «Huevos» de levante. Espejo de `ModoHuevosLevante` del backend. */
export type ModoHuevosLevante = 'ninguno' | 'clasificadora' | 'porItems';

/** Cantidad escrita por `catalogItemId` (vacío = `null`). */
export type CantidadesHuevoItem = Readonly<Record<number, number | null>>;

/**
 * Sin `captura_huevos_en_levante` no hay tab; con él, `clasificacion_huevo_por_items` elige los tipos
 * del lote en lugar de las 11 categorías fijas de la clasificadora.
 */
export function resolverModoHuevosLevante(
  capturaHuevosEnLevante: boolean,
  clasificacionHuevoPorItems: boolean
): ModoHuevosLevante {
  if (!capturaHuevosEnLevante) return 'ninguno';
  return clasificacionHuevoPorItems ? 'porItems' : 'clasificadora';
}

/** Estado de la consulta de tipos de huevo del lote (`GET /api/LoteHuevoItem/{loteId}`). */
export interface EstadoTiposHuevoLote {
  cargando: boolean;
  error: boolean;
  cantidad: number;
}

export interface ContextoTabHuevosLevante {
  modo: ModoHuevosLevante;
  fechaEncaset: string | Date | null | undefined;
  fechaRegistro: string | Date | null | undefined;
  /** `companies.huevos_levante_desde_semana`. `null` = sin límite. */
  desdeSemana: number | null;
  tiposDelLote: EstadoTiposHuevoLote;
}

/**
 * ¿Se muestra el tab «Huevos»?
 * - Sin captura en levante: nunca.
 * - Fecha anterior al encaset, o antes de la semana mínima de la empresa: no.
 * - Clasificadora fija (Sanmarino): sí; los tipos del lote no intervienen (comportamiento de siempre).
 * - Por ítems (Santa Reyes): solo si el lote tiene tipos declarados. Mientras la consulta viaja, o si
 *   falló, tampoco (fail-closed): ofrecer filas que no se pueden confirmar es peor que esperar.
 */
export function mostrarTabHuevosLevante(ctx: ContextoTabHuevosLevante): boolean {
  if (ctx.modo === 'ninguno') return false;
  if (!permiteHuevosEnLevante(ctx.fechaEncaset, ctx.fechaRegistro, ctx.desdeSemana)) return false;
  if (ctx.modo === 'clasificadora') return true;
  return !ctx.tiposDelLote.cargando && !ctx.tiposDelLote.error && ctx.tiposDelLote.cantidad > 0;
}

/** Tipos declarados del lote → filas fijas (sin marcas: `construirFilasFijasHuevo` las calcula). */
export function tiposDelLoteAFilas(tipos: readonly LoteHuevoItemDto[]): HuevoFilaFija[] {
  return (tipos ?? []).map(t => ({
    catalogItemId: t.catalogItemId,
    codigo: t.codigo ?? '',
    nombre: t.nombre,
    tipoHuevo: t.tipoHuevo ?? null,
    um: t.um ?? null,
    primeraPostura: t.primeraPostura,
    huerfano: false,
    fueraDeVigencia: false
  }));
}

/** Todas las filas, sin agrupar, en el mismo orden en que se pintan. */
export function filasPlanasHuevo(grupos: readonly HuevoGrupoFilasFijas[]): HuevoFilaFija[] {
  const filas: HuevoFilaFija[] = [];
  for (const g of grupos ?? []) filas.push(...g.filas);
  return filas;
}

/** Cantidades iniciales al editar, desde el desglose guardado (`metadata.huevoItems`). */
export function cantidadesDesdeGuardados(guardados: readonly HuevoItemSeguimiento[]): Record<number, number | null> {
  const cantidades: Record<number, number | null> = {};
  for (const g of guardados ?? []) {
    const id = Number(g?.catalogItemId) || 0;
    if (id > 0) cantidades[id] = Number(g.cantidad) || 0;
  }
  return cantidades;
}

/** Suma de las cantidades positivas de estas filas (ignora vacíos, NaN y negativos). */
export function subtotalCantidades(filas: readonly HuevoFilaFija[], cantidades: CantidadesHuevoItem): number {
  let total = 0;
  for (const fila of filas ?? []) {
    const n = Number(cantidades?.[fila.catalogItemId]);
    if (Number.isFinite(n) && n > 0) total += n;
  }
  return total;
}

/** Paso del input: los ítems que se PESAN (`um = 'KIL'`) admiten decimales en pantalla. */
export function pasoCantidadHuevoLevante(fila: Pick<HuevoFilaFija, 'um'>): string {
  return esItemEnKilos(fila.um) ? '0.01' : '1';
}

/**
 * Validación previa al guardado. Devuelve el mensaje del PRIMER problema o `null`.
 * El contrato del backend es `int Cantidad`: un decimal se avisa en vez de redondearse en silencio,
 * y una fila de primera postura fuera de vigencia no puede llevar cantidad (el backend la rechaza).
 */
export function validarHuevoItemsLevante(
  filas: readonly HuevoFilaFija[],
  cantidades: CantidadesHuevoItem
): string | null {
  for (const fila of filas ?? []) {
    const bruto = cantidades?.[fila.catalogItemId];
    if (bruto === null || bruto === undefined) continue;
    const n = Number(bruto);
    if (!Number.isFinite(n) || n === 0) continue;

    if (n < 0) return `La cantidad de «${fila.nombre}» no puede ser negativa.`;
    if (!Number.isInteger(n)) {
      return esItemEnKilos(fila.um)
        ? `«${fila.nombre}» se pesa en kilos, pero se guarda en unidades enteras: ajustá la cantidad.`
        : `La cantidad de «${fila.nombre}» debe ser un número entero.`;
    }
    if (fila.fueraDeVigencia) {
      return `«${fila.nombre}» es huevo de primera postura y está fuera de vigencia para esta semana del lote.`;
    }
  }
  return null;
}

/**
 * Payload `huevoItems`: una entrada por fila con cantidad > 0, completada con los datos del tipo (o
 * del desglose guardado si la fila quedó huérfana). Las filas en 0 no viajan: ensuciarían el jsonb
 * sin cambiar el total.
 */
export function construirHuevoItemsPayloadLevante(
  filas: readonly HuevoFilaFija[],
  cantidades: CantidadesHuevoItem,
  guardados: readonly HuevoItemSeguimiento[]
): HuevoItemSeguimiento[] {
  const porId = new Map((guardados ?? []).map(g => [g.catalogItemId, g]));
  const payload: HuevoItemSeguimiento[] = [];

  for (const fila of filas ?? []) {
    const n = Number(cantidades?.[fila.catalogItemId]);
    if (!Number.isFinite(n) || n <= 0) continue;

    const guardado = porId.get(fila.catalogItemId);
    payload.push({
      catalogItemId: fila.catalogItemId,
      codigo: fila.codigo || guardado?.codigo || null,
      nombre: fila.nombre || guardado?.nombre || null,
      tipoHuevo: fila.tipoHuevo ?? guardado?.tipoHuevo ?? null,
      cantidad: Math.round(n),
      um: fila.um ?? guardado?.um ?? null
    });
  }
  return payload;
}
