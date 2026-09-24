import {
  MovimientoAlimentoSeguimientoDto,
  ResumenMovimientosAlimentoDia
} from '../models/movimiento-alimento-seguimiento.model';
import { ymdSinTz } from './format';

/** Agrupa movimientos sin netear entradas/salidas ni repetir referencias. */
export function agruparMovimientosAlimentoPorDia(
  movimientos: readonly MovimientoAlimentoSeguimientoDto[]
): ResumenMovimientosAlimentoDia[] {
  const porFecha = new Map<string, ResumenMovimientosAlimentoDia>();

  for (const movimiento of movimientos ?? []) {
    const fecha = ymdSinTz(movimiento.fecha);
    if (!fecha) continue;

    let resumen = porFecha.get(fecha);
    if (!resumen) {
      resumen = { fecha, ingresos: [], traslados: [], referencias: [] };
      porFecha.set(fecha, resumen);
    }

    if (movimiento.tipoMovimiento === 'INV_INGRESO') resumen.ingresos.push(movimiento);
    if (movimiento.tipoMovimiento === 'INV_TRASLADO_ENTRADA'
      || movimiento.tipoMovimiento === 'INV_TRASLADO_SALIDA') {
      resumen.traslados.push(movimiento);
    }

    const referencia = (movimiento.referencia?.trim()
      || movimiento.numeroDocumento?.trim()
      || '');
    if (referencia && !resumen.referencias.includes(referencia)) {
      resumen.referencias.push(referencia);
    }
  }

  return [...porFecha.values()].sort((a, b) => a.fecha.localeCompare(b.fecha));
}

export function indexarMovimientosAlimentoPorDia(
  resumenes: readonly ResumenMovimientosAlimentoDia[]
): ReadonlyMap<string, ResumenMovimientosAlimentoDia> {
  return new Map((resumenes ?? []).map(resumen => [resumen.fecha, resumen]));
}

export function movimientosSinSeguimiento(
  resumenes: readonly ResumenMovimientosAlimentoDia[],
  fechasSeguimiento: readonly (string | Date | null | undefined)[]
): ResumenMovimientosAlimentoDia[] {
  const fechas = new Set(fechasSeguimiento.map(ymdSinTz).filter((fecha): fecha is string => !!fecha));
  return (resumenes ?? []).filter(resumen => !fechas.has(resumen.fecha));
}
