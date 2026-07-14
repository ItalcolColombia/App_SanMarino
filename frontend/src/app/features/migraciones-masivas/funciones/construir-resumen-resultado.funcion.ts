// features/migraciones-masivas/funciones/construir-resumen-resultado.funcion.ts
/**
 * Deriva la presentación (tarjetas de resumen + badge de estado + duración legible) de un
 * `MigracionResult` / estado de historial. Funciones puras: el componente solo las llama y pinta
 * el resultado; nada de HTTP ni estado de Angular acá.
 */
import { MigracionResult } from '../models/migracion.model';

export type Tono = 'neutro' | 'ok' | 'alerta' | 'peligro';

export interface ResumenItem {
  etiqueta: string;
  valor: string;
  tono: Tono;
}

export interface EstadoBadge {
  etiqueta: string;
  tono: Tono;
}

const ESTADOS_OK = new Set(['Validado', 'Procesado']);
const ESTADOS_ALERTA = new Set(['ProcesadoParcial']);
const ESTADOS_PELIGRO = new Set(['ConErrores', 'Fallido']);

/** Tono + etiqueta del badge de estado (Validado/Procesado → ok, ProcesadoParcial → alerta, resto → peligro). */
export function construirBadgeEstado(estado: string): EstadoBadge {
  const tono: Tono = ESTADOS_OK.has(estado)
    ? 'ok'
    : ESTADOS_ALERTA.has(estado)
      ? 'alerta'
      : ESTADOS_PELIGRO.has(estado)
        ? 'peligro'
        : 'neutro';
  return { etiqueta: estado, tono };
}

/** Duración legible: <1s → "menos de 1 s"; si no, "X,Y s" (un decimal) o "M min S s". */
export function formatearDuracion(ms: number): string {
  if (ms < 1000) return 'menos de 1 s';
  const totalSegundos = ms / 1000;
  if (totalSegundos < 60) {
    return `${totalSegundos.toFixed(1).replace('.', ',')} s`;
  }
  let minutos = Math.floor(totalSegundos / 60);
  let segundos = Math.round(totalSegundos - minutos * 60);
  if (segundos === 60) { minutos += 1; segundos = 0; }
  return `${minutos} min ${segundos} s`;
}

/**
 * Tarjetas de resumen tras validar/importar: filas totales, procesadas, omitidas (solo si hay),
 * con error, advertencias y duración.
 */
export function construirResumenResultado(r: MigracionResult): ResumenItem[] {
  const advertencias = r.errores.filter((e) => e.severidad === 'Advertencia').length;

  const items: ResumenItem[] = [
    { etiqueta: 'Filas totales', valor: String(r.filasTotales), tono: 'neutro' },
    { etiqueta: 'Procesadas', valor: String(r.filasProcesadas), tono: 'ok' }
  ];

  if (r.filasOmitidas > 0) {
    items.push({ etiqueta: 'Omitidas', valor: String(r.filasOmitidas), tono: 'alerta' });
  }

  items.push({ etiqueta: 'Con error', valor: String(r.filasError), tono: r.filasError > 0 ? 'peligro' : 'neutro' });
  items.push({ etiqueta: 'Advertencias', valor: String(advertencias), tono: advertencias > 0 ? 'alerta' : 'neutro' });
  items.push({ etiqueta: 'Duración', valor: formatearDuracion(r.duracionMs), tono: 'neutro' });

  return items;
}
