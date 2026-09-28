import { ResumenMovimientosAlimentoDia } from '../models/movimiento-alimento-seguimiento.model';
import { ymdSinTz } from './format';

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
