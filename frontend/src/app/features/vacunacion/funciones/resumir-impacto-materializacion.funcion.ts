// src/app/features/vacunacion/funciones/resumir-impacto-materializacion.funcion.ts
// PURA: sin `this`, sin DI, sin HTTP. Convierte los números del backend en la frase que decide el usuario.
import {
  AccionMaterializacion,
  VacunacionMaterializacionConteosDto,
  VacunacionMaterializacionMasivaDto,
} from '../models/vacunacion-materializador.model';

/**
 * El impacto de un lote, en una frase.
 *
 * <p>Sólo se nombra lo que tiene un número distinto de cero. Una lista de seis contadores en la que
 * cinco dicen «0» obliga a leerla entera para descubrir que no pasa nada; ésta se lee de un vistazo.</p>
 */
export function resumirImpactoLote(c: VacunacionMaterializacionConteosDto): string {
  const partes: string[] = [];

  if (c.faltantes > 0) partes.push(`${c.faltantes} ${plural(c.faltantes, 'vacuna nueva', 'vacunas nuevas')}`);
  if (c.actualizables > 0) partes.push(`${c.actualizables} ${plural(c.actualizables, 'se actualiza', 'se actualizan')}`);
  if (c.yaAplicados > 0) partes.push(`${c.yaAplicados} ya ${plural(c.yaAplicados, 'aplicada', 'aplicadas')}`);
  if (c.manuales > 0) partes.push(`${c.manuales} ${plural(c.manuales, 'cargada', 'cargadas')} a mano`);
  if (c.sinCambios > 0) partes.push(`${c.sinCambios} ya ${plural(c.sinCambios, 'estaba', 'estaban')} al día`);
  if (c.sobrantes > 0) partes.push(`${c.sobrantes} ${plural(c.sobrantes, 'sobrante', 'sobrantes')}`);

  return partes.length === 0 ? 'Sin vacunas para este lote.' : unir(partes) + '.';
}

/**
 * El impacto del masivo, en una frase. Distingue los tres números que importan y que son distintos:
 * cuántos lotes se miraron, a cuántos les toca esta plantilla y en cuántos hay algo para escribir.
 */
export function resumirImpactoMasivo(m: VacunacionMaterializacionMasivaDto): string {
  if (m.lotesAlcanzados === 0) {
    return m.lotesEvaluados === 0
      ? `No hay lotes abiertos de ${m.lineaProductiva.toLowerCase()} en esta empresa.`
      : `Ninguno de los ${m.lotesEvaluados} lotes abiertos de ${m.lineaProductiva.toLowerCase()} resuelve a esta plantilla.`;
  }

  const alcance =
    `${m.lotesAlcanzados} de ${m.lotesEvaluados} ${plural(m.lotesEvaluados, 'lote abierto', 'lotes abiertos')} ` +
    `${plural(m.lotesAlcanzados, 'toma', 'toman')} esta plantilla`;

  if (!m.conteos.escribeAlgo) return `${alcance}, y ${plural(m.lotesAlcanzados, 'ya está', 'ya están')} al día.`;

  return `${alcance}. En ${m.lotesQueEscriben} hay algo para escribir: ${bajarInicial(resumirImpactoLote(m.conteos))}`;
}

/** Etiqueta y color de una acción del detalle. El color sigue la regla de marca: verde sólo éxito, rojo sólo peligro. */
export function etiquetaAccion(accion: AccionMaterializacion): { texto: string; clase: string } {
  switch (accion) {
    case 'Crear':
      return { texto: 'Se agrega', clase: 'bg-orange-50 text-orange-700 border-orange-200' };
    case 'Actualizar':
      return { texto: 'Se actualiza', clase: 'bg-orange-50 text-orange-700 border-orange-200' };
    case 'YaAplicado':
      return { texto: 'Ya aplicada', clase: 'bg-green-50 text-green-700 border-green-200' };
    case 'Manual':
      return { texto: 'A mano', clase: 'bg-slate-100 text-slate-700 border-slate-200' };
    case 'SinCambios':
      return { texto: 'Al día', clase: 'bg-slate-100 text-slate-600 border-slate-200' };
    case 'Sobrante':
      return { texto: 'Sobrante', clase: 'bg-amber-50 text-amber-800 border-amber-200' };
  }
}

/** «Semana 5» / «Día 12» / «—» cuando el ítem no tiene objetivo (los sobrantes no lo traen). */
export function describirObjetivo(unidadObjetivo: string, valorObjetivo: number | null): string {
  if (valorObjetivo === null || !unidadObjetivo) return '—';
  return unidadObjetivo === 'Dia' ? `Día ${valorObjetivo}` : `Semana ${valorObjetivo}`;
}

function plural(n: number, singular: string, pluralForma: string): string {
  return n === 1 ? singular : pluralForma;
}

/** «a, b y c» — la coma serial no se usa en castellano. */
function unir(partes: string[]): string {
  if (partes.length === 1) return partes[0];
  return `${partes.slice(0, -1).join(', ')} y ${partes[partes.length - 1]}`;
}

function bajarInicial(frase: string): string {
  return frase.charAt(0).toLowerCase() + frase.slice(1);
}
