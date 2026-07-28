// Semana del año con la convención WEEKNUM de Excel (return_type 1):
// las semanas arrancan en DOMINGO y la semana 1 es la que contiene el 1-ene.
//
// NO es la semana ISO. Verificado contra el archivo fuente del Informe RA
// Pesadas: 1825/1825 filas coinciden con WEEKNUM y solo 1736 con ISO. Es la
// misma fórmula que aplican las funciones SQL y ResumenSemanalRaPesadasCalculos;
// acá vive solo para preseleccionar la semana actual en el filtro.
//
// Función PURA (sin this, sin DI).

/** Día del año, 1-based. */
function diaDelAnio(fecha: Date): number {
  const inicio = new Date(fecha.getFullYear(), 0, 1);
  const dia = 24 * 60 * 60 * 1000;
  return Math.floor((fecha.getTime() - inicio.getTime()) / dia) + 1;
}

export function semanaExcel(fecha: Date): number {
  const dowEnero1 = new Date(fecha.getFullYear(), 0, 1).getDay(); // 0 = domingo
  return Math.floor((diaDelAnio(fecha) - 1 + dowEnero1) / 7) + 1;
}
