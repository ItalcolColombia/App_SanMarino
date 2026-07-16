// features/sincronizacion-panama/funciones/construir-resumen.funcion.ts
/**
 * Arma PURO el arreglo de tarjetas-contador del resumen de una corrida
 * (`ResultadoSincronizacionDto` → `TarjetaResumen[]`). El componente lo consume memoizado
 * (computed) para no reasignar por ciclo de change detection.
 */
import { ResultadoSincronizacionDto, TarjetaResumen } from '../models/sincronizacion-panama.model';
import { formatearNumero } from '../../../shared/utils/format';

const n = (v: number): string => formatearNumero(v ?? 0);

/** Tarjetas en orden de lectura: guía → granjas → galpones → lotes → seguimientos → reproductoras. */
export function construirResumen(r: ResultadoSincronizacionDto): TarjetaResumen[] {
  return [
    { etiqueta: 'Guía genética (filas)', valor: n(r.guiaGeneticaFilas), tono: 'neutro' },
    { etiqueta: 'Granjas nuevas',         valor: n(r.granjasNuevas),     tono: 'ok' },
    { etiqueta: 'Galpones nuevos',        valor: n(r.galponesNuevos),    tono: 'ok' },
    { etiqueta: 'Lotes en el año',        valor: n(r.lotesEnAnio),       tono: 'neutro' },
    { etiqueta: 'Lotes nuevos',           valor: n(r.lotesNuevos),       tono: 'ok' },
    { etiqueta: 'Lotes omitidos',         valor: n(r.lotesOmitidos),     tono: 'neutro' },
    { etiqueta: 'Lotes pendientes (sin guía)', valor: n(r.lotesPendientes), tono: r.lotesPendientes > 0 ? 'alerta' : 'neutro' },
    { etiqueta: 'Lotes con error',        valor: n(r.lotesConError),     tono: r.lotesConError > 0 ? 'peligro' : 'neutro' },
    { etiqueta: 'Seguimientos nuevos',    valor: n(r.seguimientosNuevos),   tono: 'ok' },
    { etiqueta: 'Seguimientos omitidos',  valor: n(r.seguimientosOmitidos), tono: 'neutro' },
    { etiqueta: 'Reproductoras nuevas',   valor: n(r.reproductorasNuevas),   tono: 'ok' },
    { etiqueta: 'Reproductoras omitidas', valor: n(r.reproductorasOmitidas), tono: 'neutro' },
    { etiqueta: 'Seg. reproductora nuevos',   valor: n(r.seguimientosReproNuevos),   tono: 'ok' },
    { etiqueta: 'Seg. reproductora omitidos', valor: n(r.seguimientosReproOmitidos), tono: 'neutro' },
    { etiqueta: 'Lesiones nuevas',   valor: n(r.lesionesNuevas),   tono: 'ok' },
    { etiqueta: 'Lesiones omitidas', valor: n(r.lesionesOmitidas), tono: 'neutro' }
  ];
}
