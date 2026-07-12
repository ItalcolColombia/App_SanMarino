/**
 * Construcción de etiquetas de UI (selector de lotes, columnas y tabs de la planilla) — PURAS.
 *
 * Reciben los datos por parámetro y devuelven el texto; sin `this`, sin estado. Lógica calcada de
 * los métodos originales del componente (`etiquetaLoteFiltro`, `nombreGalponPe`,
 * `etiquetaColumnaLiquidacion`, `etiquetaTabLote`).
 */
import { LiquidacionPolloEngordeItemDto } from '../services/indicador-ecuador.service';
import { GalponOption, PeLoteAveEngordeItem } from '../models/indicador-filtros.model';
import { formatearNumero } from './formato.funcion';

/** Nombre legible del galpón a partir de su id; `'—'` si no hay id. */
export function nombreGalponPe(galpones: GalponOption[], id: string | null | undefined): string {
  if (id == null || id === '') return '—';
  const g = galpones.find(x => String(x.galponId).trim() === String(id).trim());
  return (g?.galponNombre || id).trim();
}

/** Etiqueta del lote en el selector: galpón · línea · lote · fecha de encaset. */
export function etiquetaLoteFiltro(l: PeLoteAveEngordeItem, galpones: GalponOption[]): string {
  const gNom = nombreGalponPe(galpones, l.galponId);
  const line = (l.linea || '').trim();
  const enc = l.fechaEncaset ? new Date(l.fechaEncaset) : null;
  const encStr =
    enc && !isNaN(enc.getTime())
      ? enc.toLocaleDateString('es-EC', { day: '2-digit', month: '2-digit', year: 'numeric' })
      : '';
  const parts = [
    gNom,
    line || null,
    l.loteNombre || `Lote ${l.loteAveEngordeId}`,
    encStr ? `enc. ${encStr}` : null
  ].filter((x): x is string => !!x);
  return parts.join(' · ');
}

/** Encabezado de columna en la planilla: galpón · lote · edad (días de ciclo). */
export function etiquetaColumnaLiquidacion(item: LiquidacionPolloEngordeItemDto): string {
  const ind = item.indicador;
  const g = String(ind.galponNombre || ind.galponId || '—').trim();
  const loteNom = item.loteNombre || `Lote ${item.loteAveEngordeId}`;
  const edad =
    ind.edadPromedio != null && ind.edadPromedio > 0
      ? ` · ${formatearNumero(ind.edadPromedio, 1)} d`
      : '';
  return `${g} · ${loteNom}${edad}`;
}

/** Etiqueta del tab de lote: galpón · lote. */
export function etiquetaTabLote(item: LiquidacionPolloEngordeItemDto): string {
  const g = String(item.indicador.galponNombre || item.indicador.galponId || '—').trim();
  return `${g} · ${item.loteNombre || 'Lote ' + item.loteAveEngordeId}`;
}
