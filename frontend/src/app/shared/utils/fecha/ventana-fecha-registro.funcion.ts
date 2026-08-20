// src/app/shared/utils/fecha/ventana-fecha-registro.funcion.ts
//
// Ventana BASE de fechas para los registros que se cargan A MANO por pantalla (movimientos de
// inventario, movimientos de aves, movimientos y ventas de pollo engorde, traslados de aves y de
// huevos, gastos de inventario): del día 1 del mes en curso o de hoy − 15 días —el que llegue más
// atrás— hasta HOY, o sin piso si el usuario tiene el permiso de fecha retroactiva.
//
// Espejo EXACTO de `VentanaFechaRegistroCalculos` (backend, Application/Calculos). Acá es UX —acota
// el datepicker y avisa antes de gastar un request—; la regla que manda es la del controller.
//
// PURA: sin `this`, sin DI, sin service. El permiso se resuelve afuera (con
// `UserPermissionService.has('registros.fecha_retroactiva')`, síncrono) y se pasa como parámetro,
// igual que el backend recibe `puedeRetroactivar` ya resuelto por el controller.

/** Permiso que destraba el campo de fecha hacia atrás. Mismo key que el catálogo del backend. */
export const PERMISO_FECHA_RETROACTIVA = 'registros.fecha_retroactiva';

/** Días hacia atrás que la ventana admite siempre, incluso a principio de mes. */
export const DIAS_RETROACTIVIDAD_BASE = 15;

/** Fecha local en formato `yyyy-MM-dd` (el que usan los `input[type=date]`). */
export function aYmd(fecha: Date): string {
  const mm = String(fecha.getMonth() + 1).padStart(2, '0');
  const dd = String(fecha.getDate()).padStart(2, '0');
  return `${fecha.getFullYear()}-${mm}-${dd}`;
}

/** Primer día que la ventana admite: el 1 del mes de `hoy`, o `hoy − 15`, el que llegue más atrás. */
export function primerDiaAdmitido(hoy: Date): Date {
  const primeroDelMes = new Date(hoy.getFullYear(), hoy.getMonth(), 1);
  const pisoRodante = new Date(hoy.getFullYear(), hoy.getMonth(), hoy.getDate() - DIAS_RETROACTIVIDAD_BASE);
  return pisoRodante < primeroDelMes ? pisoRodante : primeroDelMes;
}

/**
 * Extremos de la ventana, listos para los atributos `min`/`max` del datepicker.
 * `min: null` = sin piso: el usuario tiene el permiso y el datepicker no debe llevar atributo `min`.
 */
export function extremosVentanaRegistro(
  hoy: Date,
  puedeRetroactivar: boolean
): { min: string | null; max: string } {
  return {
    min: puedeRetroactivar ? null : aYmd(primerDiaAdmitido(hoy)),
    max: aYmd(hoy)
  };
}

/**
 * ¿La fecha elegida cae dentro de lo admitido? Vacío o nulo se considera válido: la validación de
 * «campo obligatorio» es otra y tiene su propio mensaje.
 *
 * La comparación es de cadenas `yyyy-MM-dd`, que es lexicográficamente equivalente a comparar
 * fechas y no pasa por `new Date(...)` (que interpreta `yyyy-MM-dd` como UTC y corre el día).
 */
export function esFechaRegistroPermitida(
  ymd: string | null | undefined,
  hoy: Date,
  puedeRetroactivar: boolean
): boolean {
  const d = (ymd ?? '').trim();
  if (!d) return true;

  const hoyYmd = aYmd(hoy);
  if (d > hoyYmd) return false; // el futuro no lo abre ningún permiso

  return puedeRetroactivar || d >= aYmd(primerDiaAdmitido(hoy));
}

/** Mensaje único del rechazo, para que todas las pantallas del alcance digan lo mismo. */
export function mensajeFechaRegistroFueraDeVentana(hoy: Date, puedeRetroactivar: boolean): string {
  const fmt = (ymd: string) => ymd.split('-').reverse().join('/');
  const hasta = fmt(aYmd(hoy));

  if (puedeRetroactivar) {
    return `La fecha no puede ser posterior a hoy (${hasta}). El permiso de fecha retroactiva abre el pasado, no el futuro.`;
  }

  const desde = fmt(aYmd(primerDiaAdmitido(hoy)));
  return (
    `La fecha debe estar entre el ${desde} y el ${hasta}: se admiten el mes en curso y los ` +
    `últimos ${DIAS_RETROACTIVIDAD_BASE} días. Para registrar una fecha anterior hace falta el ` +
    'permiso de fecha retroactiva. Tampoco se admiten fechas futuras.'
  );
}

/** Texto de ayuda del datepicker, con la misma regla que el rechazo. */
export function hintVentanaFechaRegistro(hoy: Date, puedeRetroactivar: boolean): string {
  const fmt = (ymd: string) => ymd.split('-').reverse().join('/');

  if (puedeRetroactivar) {
    return `Tenés permiso de fecha retroactiva: podés registrar cualquier fecha anterior. El máximo sigue siendo hoy (${fmt(aYmd(hoy))}).`;
  }

  return (
    `Se admite del ${fmt(aYmd(primerDiaAdmitido(hoy)))} al ${fmt(aYmd(hoy))} ` +
    `(el mes en curso y los últimos ${DIAS_RETROACTIVIDAD_BASE} días).`
  );
}
