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
  min: string;
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
  ventana: VentanaFechaIngreso | null
): { min: string; max: string } {
  return ventana ? { min: ventana.min, max: ventana.max } : ventanaFechaMovimiento(hoy);
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
  ventana: VentanaFechaIngreso | null
): boolean {
  const d = (ymd ?? '').trim();
  if (!d) return true;
  const { min, max } = extremosFechaIngreso(hoy, ventana);
  return d >= min && d <= max;
}

/** Mensaje del rechazo de la pantalla, con los extremos que efectivamente se están ofreciendo. */
export function mensajeFechaIngresoFueraDeVentana(
  hoy: Date,
  ventana: VentanaFechaIngreso | null
): string {
  if (!ventana) return mensajeFechaFueraDeVentana(hoy);
  const fmt = (ymd: string) => ymd.split('-').reverse().join('/');
  return (
    `La fecha debe estar entre el ${fmt(ventana.min)} y el ${fmt(ventana.max)}. ` +
    'No se pueden registrar movimientos con fecha futura.'
  );
}

/** Texto del hint: el que armó el backend (nombra el encasetamiento) o el genérico si no hay ventana. */
export function hintFechaIngreso(hoy: Date, ventana: VentanaFechaIngreso | null): string {
  if (ventana) return ventana.ayuda;
  const { min, max } = ventanaFechaMovimiento(hoy);
  const fmt = (ymd: string) => ymd.split('-').reverse().join('/');
  return `Se admite el mes en curso (del ${fmt(min)} al ${fmt(max)}).`;
}
