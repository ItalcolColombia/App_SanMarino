// src/app/features/gestion-inventario/funciones/ventana-fecha-movimiento.funcion.ts
//
// Ventana de fechas admitida para los movimientos de inventario que se cargan A MANO por pantalla:
// del día 1 del mes en curso o de hoy − 15 días —el que llegue más atrás— hasta HOY, o sin piso con
// el permiso de fecha retroactiva.
//
// 🔑 20-ago-2026: la ventana BASE ya no vive acá — la manda
// `shared/utils/fecha/ventana-fecha-registro.funcion.ts` (espejo de `VentanaFechaRegistroCalculos`
// del backend) y este archivo la delega, agregando encima lo único propio de inventario: la
// excepción D4 del alimento previo al encasetamiento.
//
// Acá es UX —acota el datepicker y avisa antes de gastar un request—; la regla que manda es la del
// controller.

import {
  esFechaRegistroPermitida,
  extremosVentanaRegistro,
  mensajeFechaRegistroFueraDeVentana,
  hintVentanaFechaRegistro
} from '../../../shared/utils/fecha/ventana-fecha-registro.funcion';

export { aYmd, PERMISO_FECHA_RETROACTIVA } from '../../../shared/utils/fecha/ventana-fecha-registro.funcion';

/** Extremos de la ventana, listos para los atributos `min`/`max` del datepicker. */
export function ventanaFechaMovimiento(hoy: Date, puedeRetroactivar: boolean): { min: string | null; max: string } {
  return extremosVentanaRegistro(hoy, puedeRetroactivar);
}

/**
 * ¿La fecha elegida cae dentro de la ventana? Vacío o nulo se considera válido: la validación de
 * «campo obligatorio» es otra y tiene su propio mensaje.
 */
export function esFechaMovimientoPermitida(
  ymd: string | null | undefined,
  hoy: Date,
  puedeRetroactivar: boolean
): boolean {
  return esFechaRegistroPermitida(ymd, hoy, puedeRetroactivar);
}

/** Mensaje único del rechazo, con los dos extremos de la ventana nombrados. */
export function mensajeFechaFueraDeVentana(hoy: Date, puedeRetroactivar: boolean): string {
  return mensajeFechaRegistroFueraDeVentana(hoy, puedeRetroactivar);
}

// ─── D4: la ventana de las dos puertas de INGRESO ────────────────────────────
//
// El alimento llega a la granja días ANTES que los pollitos, así que con un encasetamiento a
// principio de mes su fecha real cae en el mes anterior. El backend YA la acepta (excepción D4 del
// controller); lo que faltaba era que la pantalla dejara tipearla.
//
// ⚠️ Acá NO se replica la regla completa, a propósito: el encasetamiento que manda es el más cercano
// a partir de la fecha que el usuario elija, así que un espejo en TS resolvería otro encaset y
// rechazaría fechas que el backend acepta — el mismo defecto, del otro lado. La pantalla sólo ofrece
// el rango ENVOLVENTE que el backend le informa y deja que el 400 del controller diga la última
// palabra, con su mensaje, que nombra el encaset y el rango exacto.

/** Lo que el backend informa sobre la ventana de un ingreso. `null` = todavía no se consultó. */
export interface VentanaFechaIngreso {
  min: string | null;
  max: string;
  proximoEncaset: string | null;
  diasVentanaEmpresa: number;
  ayuda: string;
}

/**
 * Extremos del datepicker de un ingreso: los que informó el backend, o los de la regla vigente
 * mientras no haya respuesta (sin ubicación completa, o si la consulta falló). Nunca bloquea de más:
 * ante la duda vale la ventana clásica y el rechazo fino lo hace el controller.
 */
export function extremosFechaIngreso(
  hoy: Date,
  ventana: VentanaFechaIngreso | null,
  puedeRetroactivar: boolean
): { min: string | null; max: string } {
  return ventana ? { min: ventana.min, max: ventana.max } : ventanaFechaMovimiento(hoy, puedeRetroactivar);
}

/**
 * ¿La fecha elegida cae dentro de lo que la pantalla ofrece? Vacío o nulo se considera válido, igual
 * que en {@link esFechaMovimientoPermitida}.
 *
 * Sólo corta lo que ninguna ventana admite (el futuro, o antes del mínimo ofrecido). Lo que cae en el
 * hueco entre los dos tramos viaja y lo rechaza el controller: es la única punta que sabe qué
 * encasetamiento corresponde a esa fecha.
 */
export function esFechaIngresoOfrecible(
  ymd: string | null | undefined,
  hoy: Date,
  ventana: VentanaFechaIngreso | null,
  puedeRetroactivar: boolean
): boolean {
  const d = (ymd ?? '').trim();
  if (!d) return true;
  const { min, max } = extremosFechaIngreso(hoy, ventana, puedeRetroactivar);
  return (min === null || d >= min) && d <= max;
}

/** Mensaje del rechazo de la pantalla, con los extremos que efectivamente se están ofreciendo. */
export function mensajeFechaIngresoFueraDeVentana(
  hoy: Date,
  ventana: VentanaFechaIngreso | null,
  puedeRetroactivar: boolean
): string {
  if (!ventana) return mensajeFechaFueraDeVentana(hoy, puedeRetroactivar);
  const fmt = (ymd: string) => ymd.split('-').reverse().join('/');
  const desde = ventana.min ? `entre el ${fmt(ventana.min)} y el` : 'hasta el';
  return `La fecha debe estar ${desde} ${fmt(ventana.max)}. No se pueden registrar movimientos con fecha futura.`;
}

/** Texto del hint: el que armó el backend (nombra el encasetamiento) o el genérico si no hay ventana. */
export function hintFechaIngreso(hoy: Date, ventana: VentanaFechaIngreso | null, puedeRetroactivar: boolean): string {
  if (ventana) return ventana.ayuda;
  return hintVentanaFechaRegistro(hoy, puedeRetroactivar);
}
