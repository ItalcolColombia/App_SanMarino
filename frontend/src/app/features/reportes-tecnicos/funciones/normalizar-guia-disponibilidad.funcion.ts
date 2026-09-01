// src/app/features/reportes-tecnicos/funciones/normalizar-guia-disponibilidad.funcion.ts
/**
 * Función PURA (sin `this`, sin DI, sin service/estado).
 *
 * Convierte lo que llega en el DTO del reporte en un `GuiaMetricasDisponibles` completo y seguro
 * de usar en el template.
 *
 * **Fail-open a propósito**: si el reporte no informa disponibilidad (campo ausente, `null`, o un
 * objeto a medias), se asume que TODAS las métricas están disponibles. Ocultar columnas por un
 * campo que no llegó sería mucho peor que mostrar una celda vacía — y deja al reporte funcionando
 * igual que antes contra cualquier backend que todavía no mande el campo.
 */
import { GuiaMetricasDisponibles, GUIA_TODAS_DISPONIBLES } from '../models/reporte-tecnico-guia.model';

export function normalizarGuiaDisponibilidad(
  origen: Partial<GuiaMetricasDisponibles> | null | undefined
): GuiaMetricasDisponibles {
  if (!origen || typeof origen !== 'object') return { ...GUIA_TODAS_DISPONIBLES };

  const claves = Object.keys(GUIA_TODAS_DISPONIBLES) as (keyof GuiaMetricasDisponibles)[];
  const resultado = { ...GUIA_TODAS_DISPONIBLES } as GuiaMetricasDisponibles;

  for (const clave of claves) {
    const valor = origen[clave];
    // Sólo un `false` explícito oculta la columna. `undefined`/`null` = el backend no opinó ⇒ se pinta.
    resultado[clave] = valor === undefined || valor === null ? true : valor === true;
  }

  return resultado;
}
