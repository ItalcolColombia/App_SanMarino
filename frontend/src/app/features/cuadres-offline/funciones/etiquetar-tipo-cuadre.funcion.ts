/**
 * Nombre legible de un tipo de operación del contrato de sync.
 *
 * ## Por qué existe
 *
 * `sync_operaciones.tipo` guarda el identificador del contrato (`seguimiento_levante_crear`), que es
 * **estable a propósito**: el cliente decide con él y cambiarlo rompe dispositivos ya instalados.
 * Pero quien abre la bandeja es un supervisor de granja, no quien escribió el contrato.
 *
 * ⚠️ **Un tipo desconocido devuelve el identificador crudo, nunca `undefined` ni «Desconocido».**
 * Un servidor más nuevo puede mandar un tipo que este cliente no conoce (es exactamente el caso que
 * `clasificarResultadoPush` contempla con `reintentar`); tapar ese identificador dejaría al
 * supervisor mirando una fila sin nombre y sin forma de reportarla.
 */
/**
 * ⚠️ **Al agregar un tipo a `SyncPushCalculos.Tipos`, agregalo también acá.** No rompe nada si se
 * olvida —la bandeja muestra el identificador crudo— pero el supervisor lee `gasto_inventario_crear`
 * en vez de «Gasto de inventario». Pasó al abrir la pantalla por primera vez con datos reales.
 */
const ETIQUETAS: Readonly<Record<string, string>> = {
  seguimiento_levante_crear: 'Seguimiento diario · levante',
  seguimiento_produccion_crear: 'Seguimiento diario · producción',
  seguimiento_engorde_crear: 'Seguimiento diario · pollo engorde',
  seguimiento_reproductora_engorde_crear: 'Seguimiento diario · reproductora engorde',
  gasto_inventario_crear: 'Gasto de inventario'
};

export function etiquetarTipoCuadre(tipo: string | null | undefined): string {
  if (!tipo) {
    // Sin tipo no hay nada que traducir, y un guion es más honesto que inventar una etiqueta.
    return '—';
  }

  return ETIQUETAS[tipo] ?? tipo;
}
