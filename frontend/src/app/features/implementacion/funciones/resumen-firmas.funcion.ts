// src/app/features/implementacion/funciones/resumen-firmas.funcion.ts
// PURA: conteos de firmas de participantes por ítem (espejo de ImplementacionCalculos.CalcularResumenFirmas).
import { ImplementacionFirmaDto } from '../models/implementacion.models';

export interface ResumenFirmas {
  total: number;
  firmadas: number;
  rechazadas: number;
  pendientes: number;
}

export const RESUMEN_FIRMAS_VACIO: ResumenFirmas = { total: 0, firmadas: 0, rechazadas: 0, pendientes: 0 };

export function resumenFirmas(firmas: ImplementacionFirmaDto[]): ResumenFirmas {
  const total = firmas.length;
  if (!total) return RESUMEN_FIRMAS_VACIO;
  const firmadas = firmas.filter((f) => f.estado === 'firmada').length;
  const rechazadas = firmas.filter((f) => f.estado === 'rechazada').length;
  return { total, firmadas, rechazadas, pendientes: Math.max(0, total - firmadas - rechazadas) };
}

/**
 * Mensaje de error legible para las páginas del módulo a partir del error HTTP.
 * La sesión vencida (401) se explica en vez de dejar la página "pensando"/vacía.
 */
export function mensajeErrorHttp(err: any, fallback: string): string {
  if (err?.name === 'TimeoutError') {
    return 'El servidor tardó demasiado en responder (30 s). Verificá tu conexión y reintentá.';
  }
  if (err?.status === 401) {
    return 'Tu sesión expiró. Volvé a iniciar sesión para ver este módulo.';
  }
  if (err?.status === 403) {
    return 'No tenés permisos para esta acción en la empresa activa.';
  }
  return err?.error?.error ?? fallback;
}
