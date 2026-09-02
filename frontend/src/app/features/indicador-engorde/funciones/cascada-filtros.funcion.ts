/**
 * Filtros en cascada del módulo Indicador — funciones PURAS (sin `this`, sin DI, sin estado).
 *
 * Replican EXACTAMENTE el comportamiento previo de `applyPeCascade` / `applyFilterCascade` /
 * `aplicarFiltroCronologico` / `peLotesFiltradosPorFecha` del componente, incluyendo el orden de
 * los filtros y en qué rama se aplica (o no) el filtro cronológico Año-Corrida.
 */
import {
  GalponOption,
  LoteOption,
  NucleoOption,
  PeLoteAveEngordeItem
} from '../models/indicador-filtros.model';

/** Código concatenado Año+Corrida — vacío si no hay año seleccionado. */
export function construirCodigoAnioCorrida(anio: string | null, corrida: string | null): string {
  if (!anio) return '';
  return anio + (corrida ?? '');
}

/** Filtra lotes cuyo nombre comienza con el código Año-Corrida. Sin código ⇒ devuelve la lista tal cual. */
export function aplicarFiltroCronologico(
  lotes: PeLoteAveEngordeItem[],
  codigo: string
): PeLoteAveEngordeItem[] {
  if (!codigo) return lotes;
  return lotes.filter(l => l.loteNombre && l.loteNombre.startsWith(codigo));
}

/** Resultado de la cascada Pollo Engorde. */
export interface CascadaPeResult {
  nucleos: NucleoOption[];
  galpones: GalponOption[];
  lotesAveEngorde: PeLoteAveEngordeItem[];
}

/**
 * Cascada Granja → Núcleo → Galpón → Lote (ave engorde). El filtro cronológico (Año-Corrida) SOLO
 * se aplica cuando están seleccionados granja + núcleo + galpón (igual que el código original).
 */
export function filtrarCascadaPe(input: {
  granjaId: number | null;
  nucleoId: string | null;
  galponId: string | null;
  allNucleos: NucleoOption[];
  allGalpones: GalponOption[];
  allLotes: PeLoteAveEngordeItem[];
  codigoCronologico: string;
}): CascadaPeResult {
  const { granjaId, nucleoId, galponId, allNucleos, allGalpones, allLotes, codigoCronologico } = input;

  if (!granjaId) {
    return { nucleos: [], galpones: [], lotesAveEngorde: [] };
  }
  const gid = Number(granjaId);
  const nucleos = allNucleos.filter(n => n.granjaId === gid);

  if (!nucleoId) {
    return {
      nucleos,
      galpones: allGalpones.filter(g => g.granjaId === gid),
      lotesAveEngorde: allLotes.filter(l => l.granjaId === gid)
    };
  }
  const nid = String(nucleoId).trim();
  const galpones = allGalpones.filter(g => g.granjaId === gid && String(g.nucleoId).trim() === nid);
  let lotesAveEngorde = allLotes.filter(
    l => l.granjaId === gid && String(l.nucleoId || '').trim() === nid
  );

  if (!galponId) {
    return { nucleos, galpones, lotesAveEngorde };
  }
  const gpid = String(galponId).trim();
  lotesAveEngorde = lotesAveEngorde.filter(l => String(l.galponId || '').trim() === gpid);

  // Aplicar filtro cronológico (Año-Corrida) al final de la cascada.
  lotesAveEngorde = aplicarFiltroCronologico(lotesAveEngorde, codigoCronologico);

  return { nucleos, galpones, lotesAveEngorde };
}

/** Resultado de la cascada de la vista general. */
export interface CascadaGeneralResult {
  nucleos: NucleoOption[];
  galpones: GalponOption[];
  lotes: LoteOption[];
}

/** Cascada Granja → Núcleo → Galpón → Lote de la vista general (sin filtro cronológico). */
export function filtrarCascadaGeneral(input: {
  granjaId: number | null;
  nucleoId: string | null;
  galponId: string | null;
  allNucleos: NucleoOption[];
  allGalpones: GalponOption[];
  allLotes: LoteOption[];
}): CascadaGeneralResult {
  const { granjaId, nucleoId, galponId, allNucleos, allGalpones, allLotes } = input;

  if (!granjaId) {
    return { nucleos: [], galpones: [], lotes: [] };
  }
  const gid = Number(granjaId);
  const nucleos = allNucleos.filter(n => n.granjaId === gid);

  if (!nucleoId) {
    return {
      nucleos,
      galpones: allGalpones.filter(g => g.granjaId === gid),
      lotes: allLotes.filter(l => l.granjaId === gid)
    };
  }
  const nid = String(nucleoId).trim();
  const galpones = allGalpones.filter(g => g.granjaId === gid && String(g.nucleoId).trim() === nid);
  let lotes = allLotes.filter(l => l.granjaId === gid && String(l.nucleoId || '').trim() === nid);

  if (!galponId) {
    return { nucleos, galpones, lotes };
  }
  const gpid = String(galponId).trim();
  lotes = lotes.filter(l => String(l.galponId || '').trim() === gpid);

  return { nucleos, galpones, lotes };
}

/**
 * Lotes filtrados por fecha de encaset según el tipo de filtro activo. En el caso por defecto
 * devuelve la MISMA referencia `base` (estabilidad para change detection, igual que el getter original).
 */
export function filtrarLotesPorFechaEncaset(input: {
  base: PeLoteAveEngordeItem[];
  tipoFiltroFecha: 'todos' | 'rango' | 'anio' | 'meses';
  filtroAnio: number | null;
  filtroMeses: number[];
  filtroDesde: string;
  filtroHasta: string;
}): PeLoteAveEngordeItem[] {
  const { base, tipoFiltroFecha, filtroAnio, filtroMeses, filtroDesde, filtroHasta } = input;
  switch (tipoFiltroFecha) {
    case 'anio':
      if (!filtroAnio) return base;
      return base.filter(l => l.fechaEncaset && new Date(l.fechaEncaset).getFullYear() === filtroAnio);
    case 'meses':
      if (!filtroMeses.length) return base;
      return base.filter(l => {
        if (!l.fechaEncaset) return false;
        return filtroMeses.includes(new Date(l.fechaEncaset).getMonth() + 1);
      });
    case 'rango':
      if (!filtroDesde || !filtroHasta) return base;
      return base.filter(l => {
        if (!l.fechaEncaset) return false;
        const d = l.fechaEncaset.substring(0, 10);
        return d >= filtroDesde && d <= filtroHasta;
      });
    default:
      return base;
  }
}
