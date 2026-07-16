// features/sincronizacion-panama/funciones/construir-confirmacion.funcion.ts
/**
 * Arma PURO el banner de confirmación de una corrida terminada (`ResultadoSincronizacionDto` →
 * `{ tono, titulo, detalle }`): confirmación inequívoca de que la sincronización real SÍ pasó
 * (o de qué haría la validación), con el resumen de lo migrado y el nº de corrida auditada.
 * Regla de marca: verde SOLO para éxito; ámbar para advertencias; rojo para fallo.
 */
import { ResultadoSincronizacionDto, Tono } from '../models/sincronizacion-panama.model';
import { formatearNumero } from '../../../shared/utils/format';

export interface ConfirmacionCorrida {
  tono: Tono;
  titulo: string;
  detalle: string;
}

const n = (v: number): string => formatearNumero(v ?? 0);

/** "N lotes · M seguimientos · K reproductoras · J seg. repro · L lesiones" (solo lo > 0, o lotes siempre). */
function resumenMigrado(r: ResultadoSincronizacionDto, futuro: boolean): string {
  const verbo = futuro ? 'a crear' : 'creados';
  const partes: string[] = [`${n(r.lotesNuevos)} lote(s) ${verbo}`];
  if (r.seguimientosNuevos > 0) partes.push(`${n(r.seguimientosNuevos)} seguimientos`);
  if (r.reproductorasNuevas > 0) partes.push(`${n(r.reproductorasNuevas)} reproductoras`);
  if (r.seguimientosReproNuevos > 0) partes.push(`${n(r.seguimientosReproNuevos)} seg. reproductora`);
  if (r.lesionesNuevas > 0) partes.push(`${n(r.lesionesNuevas)} lesiones`);
  if (r.granjasNuevas > 0) partes.push(`${n(r.granjasNuevas)} granjas`);
  if (r.galponesNuevos > 0) partes.push(`${n(r.galponesNuevos)} galpones`);
  return partes.join(' · ');
}

export function construirConfirmacion(r: ResultadoSincronizacionDto): ConfirmacionCorrida {
  const seg = (r.duracionMs / 1000).toFixed(1);
  const corrida = r.auditoriaId != null ? ` · auditada como corrida #${r.auditoriaId}` : '';
  const pendientes = r.lotesPendientes > 0 ? ` · ${n(r.lotesPendientes)} pendiente(s) SIN crear` : '';
  const errores = r.lotesConError > 0 ? ` · ${n(r.lotesConError)} con error` : '';

  if (r.estado === 'Fallido') {
    return {
      tono: 'peligro',
      titulo: r.dryRun ? 'La validación falló' : 'La sincronización falló — NO se migraron datos',
      detalle: `${r.mensajes[0] ?? 'Revisá los mensajes.'}${corrida}`
    };
  }

  if (r.dryRun) {
    return {
      tono: r.lotesPendientes > 0 || r.estado === 'ConAdvertencias' ? 'alerta' : 'neutro',
      titulo: 'Validación completada — esto es lo que se migrará (no se modificó nada)',
      detalle: `${resumenMigrado(r, true)}${pendientes}${errores}${corrida}`
    };
  }

  if (r.estado === 'ConAdvertencias') {
    return {
      tono: 'alerta',
      titulo: `Sincronización completada CON ADVERTENCIAS en ${seg} s`,
      detalle: `${resumenMigrado(r, false)}${pendientes}${errores}${corrida} · revisá los mensajes de abajo.`
    };
  }

  return {
    tono: 'ok',
    titulo: `✔ Sincronización completada en ${seg} s — los datos SÍ se migraron`,
    detalle: `${resumenMigrado(r, false)}${corrida}`
  };
}
