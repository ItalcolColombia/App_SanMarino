// Estado de la doble validación de un registro de seguimiento diario.
// UNA sola copia para levante, producción, pollo engorde y reproductora: si cada tabla decidiera
// por su cuenta cuándo un registro está «en retraso», bastaría con que una se olvidara del plazo
// para que el mismo día se vea pendiente en un módulo y vencido en otro.
//
// Espeja `ValidacionSeguimientoCalculos` del backend. El backend manda: cuando la fila trae
// `estadoValidacion` se usa tal cual y estas funciones solo sirven de respaldo para las respuestas
// que todavía no lo incluyen (caché offline, DTOs viejos).

/** Estados posibles. Mismos literales que el backend, para poder comparar sin traducir. */
export type EstadoValidacionSeguimiento = 'VALIDADO' | 'PENDIENTE' | 'EN_RETRASO';

/** Plazo de validación en días, contado desde la fecha del seguimiento. */
export const DIAS_PLAZO_VALIDACION = 1;

/** Forma mínima que necesita una fila para que se pueda decidir su estado. */
export interface FilaValidable {
  validado?: boolean | null;
  estadoValidacion?: string | null;
  fechaRegistro?: string | Date | null;
  fecha?: string | Date | null;
}

/** Normaliza a día calendario local, ignorando la hora (las fechas viajan a mediodía UTC). */
function aDia(valor: string | Date | null | undefined): Date | null {
  if (!valor) return null;
  const d = valor instanceof Date ? valor : new Date(valor);
  if (Number.isNaN(d.getTime())) return null;
  return new Date(d.getFullYear(), d.getMonth(), d.getDate());
}

/** Hoy, a día calendario. Parametrizable para poder testear sin tocar el reloj. */
function hoyDia(hoy?: Date): Date {
  const d = hoy ?? new Date();
  return new Date(d.getFullYear(), d.getMonth(), d.getDate());
}

/**
 * Estado de la fila. Si el backend ya lo mandó, se respeta: es el único que conoce el flag de la
 * empresa. El cálculo local es el respaldo.
 */
export function estadoValidacion(fila: FilaValidable, hoy?: Date): EstadoValidacionSeguimiento {
  const delBackend = (fila?.estadoValidacion ?? '').toUpperCase();
  if (delBackend === 'VALIDADO' || delBackend === 'PENDIENTE' || delBackend === 'EN_RETRASO') {
    return delBackend;
  }

  if (fila?.validado === true) return 'VALIDADO';

  const fecha = aDia(fila?.fechaRegistro ?? fila?.fecha);
  if (!fecha) return 'PENDIENTE';

  const limite = new Date(fecha);
  limite.setDate(limite.getDate() + DIAS_PLAZO_VALIDACION);

  return hoyDia(hoy) > limite ? 'EN_RETRASO' : 'PENDIENTE';
}

/** True si la fila hay que pintarla en rojo y mostrarle el ícono de alarma. */
export function estaEnRetraso(fila: FilaValidable, hoy?: Date): boolean {
  return estadoValidacion(fila, hoy) === 'EN_RETRASO';
}

/** Clase CSS de la fila. Vacía cuando no hay nada que señalar, para no ensuciar el `[ngClass]`. */
export function claseFilaValidacion(fila: FilaValidable, hoy?: Date): string {
  switch (estadoValidacion(fila, hoy)) {
    case 'EN_RETRASO': return 'fila-validacion--retraso';
    case 'PENDIENTE':  return 'fila-validacion--pendiente';
    default:           return '';
  }
}

/** Texto del badge de la columna Estado. */
export function etiquetaValidacion(fila: FilaValidable, hoy?: Date): string {
  switch (estadoValidacion(fila, hoy)) {
    case 'VALIDADO':   return 'Validado';
    case 'EN_RETRASO': return 'En retraso';
    default:           return 'Pendiente';
  }
}

/** Tooltip del badge: dice qué implica el estado, que es lo que el usuario necesita saber. */
export function tooltipValidacion(fila: FilaValidable, hoy?: Date): string {
  switch (estadoValidacion(fila, hoy)) {
    case 'VALIDADO':
      return 'Validado — el alimento y las aves ya se descontaron. El registro es de solo lectura.';
    case 'EN_RETRASO':
      return 'En retraso — superó el plazo de validación. Mientras no se valide, el lote no acepta días nuevos.';
    default:
      return 'Pendiente de validar — el alimento y las aves están separados, todavía no descontados. Se puede editar y eliminar.';
  }
}

/** Cuántas filas están vencidas. Alimenta el modal de alerta al entrar al lote. */
export function contarEnRetraso(filas: readonly FilaValidable[], hoy?: Date): number {
  return (filas ?? []).filter(f => estaEnRetraso(f, hoy)).length;
}

/** Cuántas filas están sin validar (incluye las vencidas). */
export function contarPendientes(filas: readonly FilaValidable[], hoy?: Date): number {
  return (filas ?? []).filter(f => estadoValidacion(f, hoy) !== 'VALIDADO').length;
}
