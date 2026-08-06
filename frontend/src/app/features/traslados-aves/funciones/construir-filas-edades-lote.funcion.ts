import { fechaCortaSinTz } from '../../../shared/utils/format';
import { CohortesLoteDto, FilaEdadLote } from '../models/cohorte-lote.model';

/**
 * Arma las filas de la tabla "Edades en el lote" a partir de la respuesta del backend.
 *
 * FUNCIÓN PURA: sin `this`, sin DI, sin HTTP. Las edades (días/semanas) vienen
 * calculadas por el backend y se copian tal cual — acá NO se recalcula ninguna edad.
 * Las fechas se formatean con `fechaCortaSinTz` (fechas puras: sin corrimiento de zona).
 *
 * Siempre devuelve al menos la fila de las aves PROPIAS del lote; las cohortes
 * recibidas van después, en el orden que las entrega el backend.
 */
export function construirFilasEdadesLote(dto: CohortesLoteDto | null | undefined): FilaEdadLote[] {
  const filas: FilaEdadLote[] = [];

  const hembrasPropias = numeroONull(dto?.hembrasPropias);
  const machosPropias = numeroONull(dto?.machosPropias);

  filas.push({
    clave: 'propia',
    tipo: 'propia',
    origen: 'Aves propias del lote',
    ubicacionOrigen: '—',
    fechaIngreso: '—',
    fechaEncaset: fechaCortaSinTz(dto?.fechaEncasetPropia ?? null),
    // Estimación del backend (saldo − recibidas): permite cuadrar propias + recibidas = saldo.
    hembras: hembrasPropias,
    machos: machosPropias,
    total: hembrasPropias == null && machosPropias == null ? null : (hembrasPropias ?? 0) + (machosPropias ?? 0),
    edadDias: numeroONull(dto?.edadPropiaDias),
    edadSemanas: numeroONull(dto?.edadPropiaSemanas),
    observaciones: null
  });

  for (const c of dto?.cohortes ?? []) {
    const hembras = numeroONull(c?.cantidadHembras);
    const machos = numeroONull(c?.cantidadMachos);
    const idOrigen = c?.loteOrigenId ?? null;
    const nombreOrigen = (c?.loteOrigenNombre ?? '').trim();
    filas.push({
      clave: `cohorte-${c?.id ?? filas.length}`,
      tipo: 'cohorte',
      origen: nombreOrigen || (idOrigen != null ? `Lote ${idOrigen}` : 'Lote origen desconocido'),
      ubicacionOrigen: (c?.ubicacionOrigen ?? '').trim() || '—',
      fechaIngreso: fechaCortaSinTz(c?.fechaIngreso ?? null),
      fechaEncaset: fechaCortaSinTz(c?.fechaEncasetCohorte ?? null),
      hembras,
      machos,
      total: hembras == null && machos == null ? null : (hembras ?? 0) + (machos ?? 0),
      edadDias: numeroONull(c?.edadDias),
      edadSemanas: numeroONull(c?.edadSemanas),
      observaciones: (c?.observaciones ?? '').trim() || null
    });
  }

  return filas;
}

/** Normaliza a número finito; cualquier otra cosa (null, undefined, NaN, string) → null. */
function numeroONull(v: unknown): number | null {
  return typeof v === 'number' && Number.isFinite(v) ? v : null;
}
