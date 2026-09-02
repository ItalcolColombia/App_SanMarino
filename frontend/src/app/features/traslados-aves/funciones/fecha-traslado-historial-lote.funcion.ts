// src/app/features/traslados-aves/funciones/fecha-traslado-historial-lote.funcion.ts
// Fecha a mostrar en la columna «Fecha» del historial de traslados de lote.
// Funcion PURA: sin `this`, sin DI, sin estado del componente.

import { fechaCortaSinTz } from '../../../shared/utils/format';

/** Lo minimo que necesita la funcion; la fila completa es `HistorialTrasladoLoteDto`. */
export interface FilaHistorialTrasladoLote {
  fechaTraslado?: string | null;
  createdAt?: string | null;
}

/**
 * Dia del traslado, listo para pintar.
 *
 * Muestra `fechaTraslado` —el dia REAL en que el lote se movio, el que eligio quien registro— y no
 * `createdAt`, que es cuando alguien lo digito: un lote movido la semana pasada y cargado hoy tiene
 * las dos distintas. Si la fila es anterior a la migracion que agrego la columna y quedo en null,
 * cae a `createdAt`, que es la mejor aproximacion que existe; recien si no hay ninguna, el guion.
 *
 * Usa `fechaCortaSinTz` y no `formatearFecha` por dos razones: (1) una fecha pura no tiene hora que
 * mostrar, y (2) `new Date("2026-09-01")` se parsea como medianoche **UTC**, asi que formatearla en
 * local la correria al **31/08** en Colombia. `fechaCortaSinTz` existe justamente para eso.
 */
export function fechaTrasladoHistorialLote(fila: FilaHistorialTrasladoLote | null | undefined): string {
  const fecha = fila?.fechaTraslado ?? fila?.createdAt;
  return fecha ? fechaCortaSinTz(fecha) : '—';
}
