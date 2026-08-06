/**
 * Filtrado del catálogo de lotes candidatos a DESTINO de un traslado de pollo engorde.
 *
 * Función PURA (sin `this`, sin DI, sin HTTP): recibe el catálogo completo y la ubicación elegida en la
 * cascada, y devuelve los lotes que caen ahí. Espejo del `filtrarLotesDestino()` del modal de traslado de
 * postura, extraído a `funciones/` para que el modal quede como orquestador delgado y el criterio sea
 * testeable.
 *
 * Reglas:
 *  - Sin granja elegida no hay candidatos (el usuario todavía no decidió a dónde va).
 *  - Núcleo y galpón son refinamientos opcionales: si están vacíos no filtran.
 *  - El lote ORIGEN nunca puede ser su propio destino.
 *  - Un lote CERRADO (liquidado) no admite aves: el backend lo rechaza con el gate B8, así que tampoco
 *    se ofrece en la lista (fallar en el select es mejor que fallar al guardar).
 */
import { LoteAveEngordeDto } from '../../lote-engorde/services/lote-engorde.service';
import { LoteDestinoOption } from '../models/venta-granja.model';

/** Ubicación elegida en la cascada de destino. */
export interface UbicacionDestinoSeleccionada {
  granjaId: number | null;
  nucleoId: string | null;
  galponId: string | null;
}

/** Normaliza un id de núcleo/galpón a texto comparable ('' cuando no hay valor). */
function txt(value: unknown): string {
  return value != null ? String(value).trim() : '';
}

/** True si el lote está liquidado/cerrado y por tanto no puede recibir aves. */
function estaCerrado(l: LoteAveEngordeDto): boolean {
  return txt(l.estadoOperativoLote).toLowerCase() === 'cerrado';
}

/**
 * Lotes Ave Engorde que pueden recibir el traslado, dados el catálogo y la ubicación elegida.
 * @param loteOrigenValue value del lote origen (`ae-123` / `rae-456`) para auto-excluirlo.
 */
export function filtrarLotesDestinoEngorde(
  catalogo: LoteAveEngordeDto[],
  ubicacion: UbicacionDestinoSeleccionada,
  loteOrigenValue: string | null
): LoteAveEngordeDto[] {
  if (ubicacion.granjaId == null) return [];

  const granjaId = Number(ubicacion.granjaId);
  const nucleoId = txt(ubicacion.nucleoId);
  const galponId = txt(ubicacion.galponId);
  // Solo se auto-excluye si el origen es un lote Ave Engorde: los ids de `rae-` son otra secuencia.
  const origenAeId =
    loteOrigenValue && loteOrigenValue.startsWith('ae-')
      ? Number(loteOrigenValue.replace('ae-', ''))
      : null;

  return (catalogo ?? []).filter((l) => {
    if (Number(l.granjaId) !== granjaId) return false;
    if (origenAeId != null && l.loteAveEngordeId === origenAeId) return false;
    if (estaCerrado(l)) return false;
    if (nucleoId && txt(l.nucleo?.nucleoId ?? l.nucleoId) !== nucleoId) return false;
    if (galponId && txt(l.galpon?.galponId ?? l.galponId) !== galponId) return false;
    return true;
  });
}

/** Opciones de `<select>` (value `ae-<id>`) ordenadas por nombre, para los lotes ya filtrados. */
export function construirOpcionesLoteDestino(lotes: LoteAveEngordeDto[]): LoteDestinoOption[] {
  return (lotes ?? [])
    .filter((l) => l.loteAveEngordeId != null)
    .map((l) => ({
      value: `ae-${l.loteAveEngordeId}`,
      label: l.loteNombre?.trim() || `Lote ${l.loteAveEngordeId}`
    }))
    .sort((a, b) => a.label.localeCompare(b.label, 'es', { numeric: true }));
}
