/**
 * Agrupación de la tabla de movimientos por despacho.
 *
 * Las ventas que comparten el mismo despacho (mismo viaje, varios lotes) se colapsan en una sola
 * fila expandible; el resto quedan como filas simples. Todo el ordenamiento es descendente por
 * fecha. Función pura (sin estado de componente) → testeable y reutilizable por país.
 */
import { MovimientoPolloEngordeDto } from '../services/movimiento-pollo-engorde.service';
import { FilaTablaMovimiento } from '../models/movimiento-tabla.model';

/** Día ISO (`YYYY-MM-DD`) a partir de una fecha ISO completa. */
function fechaDiaISO(iso: string): string {
  if (!iso) return '';
  return iso.slice(0, 10);
}

function compareMovimientoFechaDesc(a: MovimientoPolloEngordeDto, b: MovimientoPolloEngordeDto): number {
  const da = new Date(a.fechaMovimiento).getTime();
  const db = new Date(b.fechaMovimiento).getTime();
  if (db !== da) return db - da;
  return (b.numeroMovimiento ?? '').localeCompare(a.numeroMovimiento ?? '');
}

function compareFilaTablaDesc(a: FilaTablaMovimiento, b: FilaTablaMovimiento): number {
  const fa = a.kind === 'despacho-grupo' ? a.fechaMovimiento : a.movimiento.fechaMovimiento;
  const fb = b.kind === 'despacho-grupo' ? b.fechaMovimiento : b.movimiento.fechaMovimiento;
  const ta = new Date(fa).getTime();
  const tb = new Date(fb).getTime();
  if (tb !== ta) return tb - ta;
  const na = a.kind === 'despacho-grupo' ? a.numeroDespacho : a.movimiento.numeroMovimiento;
  const nb = b.kind === 'despacho-grupo' ? b.numeroDespacho : b.movimiento.numeroMovimiento;
  return (nb ?? '').localeCompare(na ?? '');
}

/**
 * Construye las filas de la tabla agrupando ventas por despacho.
 *
 * R3.4: se agrupa por `facturaId` (clave robusta del despacho). Fallback al número de
 * despacho + fecha + granja para movimientos antiguos sin `facturaId`.
 */
export function construirFilasTabla(list: MovimientoPolloEngordeDto[]): FilaTablaMovimiento[] {
  const puedeAgrupar = (m: MovimientoPolloEngordeDto) =>
    m.tipoMovimiento === 'Venta' && (!!m.facturaId || !!(m.numeroDespacho ?? '').trim());

  const grupoKey = (m: MovimientoPolloEngordeDto) =>
    m.facturaId
      ? `f|${m.facturaId}`
      : `${(m.numeroDespacho ?? '').trim().toLowerCase()}|${fechaDiaISO(m.fechaMovimiento)}|${m.granjaOrigenId ?? 0}`;

  const groups = new Map<string, MovimientoPolloEngordeDto[]>();
  const sueltos: MovimientoPolloEngordeDto[] = [];

  for (const m of list) {
    if (!puedeAgrupar(m)) {
      sueltos.push(m);
      continue;
    }
    const k = grupoKey(m);
    if (!groups.has(k)) groups.set(k, []);
    groups.get(k)!.push(m);
  }

  const filas: FilaTablaMovimiento[] = [];

  for (const [, movs] of groups) {
    if (movs.length >= 2) {
      movs.sort((a, b) => (a.numeroMovimiento ?? '').localeCompare(b.numeroMovimiento ?? ''));
      const clave = grupoKey(movs[0]);
      filas.push({
        kind: 'despacho-grupo',
        clave,
        numeroDespacho:
          (movs[0].numeroDespacho ?? '').trim() ||
          (movs[0].facturaId ? `Factura ${String(movs[0].facturaId).slice(0, 8)}` : ''),
        fechaMovimiento: movs[0].fechaMovimiento,
        movimientos: movs
      });
    } else {
      sueltos.push(movs[0]);
    }
  }

  sueltos.sort(compareMovimientoFechaDesc);
  for (const m of sueltos) {
    filas.push({ kind: 'simple', movimiento: m });
  }

  filas.sort(compareFilaTablaDesc);
  return filas;
}
