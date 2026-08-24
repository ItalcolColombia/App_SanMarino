// src/app/features/traslados-aves/funciones/inventario-dashboard-movimiento.funcion.ts
// Reglas de un movimiento de traslado unificado — extraído de InventarioDashboardComponent.
// Función PURA: sin `this`, sin DI, sin estado del componente.

import { TrasladoUnificado } from '../../../core/services/traslado-navigation/traslado-navigation.service';

/** Anular venta/traslado de aves: devuelve cantidades al inventario del lote (backend). */
export function puedeAnularMovimientoAves(m: TrasladoUnificado): boolean {
  if (m.tipoTraslado !== 'Aves') return false;
  const e = (m.estado ?? '').trim().toLowerCase();
  if (e === 'cancelado') return false;
  return m.id > 0;
}
