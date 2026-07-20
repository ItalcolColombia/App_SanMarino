// frontend/src/app/features/lote-produccion/funciones/empty-state-causa-indicadores.funcion.ts

/**
 * REQ-000c — Causa probable del empty-state de indicadores/gráficas de producción.
 * Función PURA: sin `this`, sin DI. Reutilizada por tabla-lista-indicadores y graficas-principal
 * para reemplazar el mensaje genérico ("Agrega registros desde la semana 26") por la causa real
 * cuando se puede inferir (encaset futuro / sin guía genética cargada para raza-año del lote).
 */
export interface CausaEmptyStateIndicadoresParams {
  /** InformacionLoteDto.fechaEncaset (o equivalente) del lote consultado. */
  fechaEncaset?: string | Date | null;
  /** Si la respuesta del backend trajo datos de guía genética para el lote. */
  tieneDatosGuiaGenetica: boolean;
  /** Mensaje que arma el backend cuando el lote tiene Raza/Año pero no hay guía cargada para esa combinación. */
  mensajeGuiaGenetica?: string | null;
}

export function causaEmptyStateIndicadores(params: CausaEmptyStateIndicadoresParams): string | null {
  const { fechaEncaset, tieneDatosGuiaGenetica, mensajeGuiaGenetica } = params;

  if (fechaEncaset) {
    const enc = new Date(fechaEncaset as any);
    if (!isNaN(enc.getTime()) && enc.getTime() > Date.now()) {
      return 'La fecha de encasetamiento es posterior a hoy: revisá el lote.';
    }
  }

  if (!tieneDatosGuiaGenetica) {
    return mensajeGuiaGenetica || 'Sin guía genética cargada para raza/año del lote.';
  }

  return null;
}
