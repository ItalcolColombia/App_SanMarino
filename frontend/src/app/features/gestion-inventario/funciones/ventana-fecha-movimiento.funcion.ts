// src/app/features/gestion-inventario/funciones/ventana-fecha-movimiento.funcion.ts
//
// Ventana de fechas admitida para los movimientos de inventario que se cargan A MANO por pantalla:
// del día 1 del mes en curso hasta HOY.
//
// Espejo EXACTO de `VentanaFechaMovimientoInventarioCalculos` (backend). Acá es UX —acota el
// datepicker y avisa antes de gastar un request—; la regla que manda es la del controller.

/** Fecha local en formato `yyyy-MM-dd` (el que usan los `input[type=date]`). */
export function aYmd(fecha: Date): string {
  const mm = String(fecha.getMonth() + 1).padStart(2, '0');
  const dd = String(fecha.getDate()).padStart(2, '0');
  return `${fecha.getFullYear()}-${mm}-${dd}`;
}

/** Extremos de la ventana, listos para los atributos `min`/`max` del datepicker. */
export function ventanaFechaMovimiento(hoy: Date): { min: string; max: string } {
  return {
    min: aYmd(new Date(hoy.getFullYear(), hoy.getMonth(), 1)),
    max: aYmd(hoy)
  };
}

/**
 * ¿La fecha elegida cae dentro de la ventana? Vacío o nulo se considera válido: la validación de
 * «campo obligatorio» es otra y tiene su propio mensaje.
 *
 * La comparación es de cadenas `yyyy-MM-dd`, que es lexicográficamente equivalente a comparar
 * fechas y no pasa por `new Date(...)` (que interpreta `yyyy-MM-dd` como UTC y corre el día).
 */
export function esFechaMovimientoPermitida(ymd: string | null | undefined, hoy: Date): boolean {
  const d = (ymd ?? '').trim();
  if (!d) return true;
  const { min, max } = ventanaFechaMovimiento(hoy);
  return d >= min && d <= max;
}

/** Mensaje único del rechazo, con los dos extremos de la ventana nombrados. */
export function mensajeFechaFueraDeVentana(hoy: Date): string {
  const fmt = (ymd: string) => ymd.split('-').reverse().join('/');
  const { min, max } = ventanaFechaMovimiento(hoy);
  return (
    `La fecha debe estar dentro del mes en curso: entre el ${fmt(min)} y el ${fmt(max)}. ` +
    'No se pueden registrar movimientos de meses anteriores ni con fecha futura.'
  );
}
