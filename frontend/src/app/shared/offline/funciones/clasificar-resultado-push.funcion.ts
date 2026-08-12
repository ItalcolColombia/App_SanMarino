import type { AccionTrasPush, ResultadoPush } from '../models/outbox.model';

/**
 * Qué hacer con una operación después de ver lo que respondió el servidor.
 *
 * ## Las dos clases de rechazo (§5.5 del plan madre)
 *
 * Un rechazo en campo casi nunca es un error corregible: suele ser un hecho físico ya ocurrido. Por
 * eso lo que decide es el **código**, que es estable, y no el texto:
 *
 * - **Error de captura** (`validacion`, `contrato_obsoleto`, `regla_de_negocio`, `duplicado_en_lote`):
 *   reintentar da exactamente el mismo resultado. Va a la bandeja para que una persona decida.
 * - **Transitorio** (`error_interno`, o ninguna respuesta): el mundo puede cambiar. Se reintenta con
 *   backoff.
 *
 * `empresa_no_autorizada` es el caso interesante: **se reintenta**, no va a la bandeja. Es
 * típicamente transitorio —le están corrigiendo la asignación al usuario, o todavía no sincronizó
 * el alta de la empresa— y descartar una captura por eso perdería trabajo real. Por eso el servidor
 * tampoco lo registra: cada intento se re-evalúa contra el mundo de ese momento.
 */

/** Códigos que no mejoran reintentando: necesitan que una persona mire. */
const DEFINITIVOS: ReadonlySet<string> = new Set([
  'validacion',
  'contrato_obsoleto',
  'regla_de_negocio',
  'duplicado_en_lote'
]);

export function clasificarResultadoPush(resultado: ResultadoPush | null | undefined): AccionTrasPush {
  if (!resultado) {
    // El lote no trajo respuesta para esta operación: no se puede afirmar que se aplicó, y borrarla
    // sería perderla. Se reintenta.
    return 'reintentar';
  }

  // `replay` incluido: si el servidor dice que ya la tenía, está aplicada. Es justamente la prueba
  // de que la idempotencia funcionó y de que el reintento no duplicó nada.
  if (resultado.estado === 'aplicada' || resultado.estado === 'requiere_cuadre') {
    return 'borrar';
  }

  if (resultado.estado !== 'rechazada') {
    // Estado que este cliente no conoce (servidor más nuevo). No se borra nada por las dudas.
    return 'reintentar';
  }

  return DEFINITIVOS.has(resultado.errorCodigo ?? '') ? 'bandeja' : 'reintentar';
}

/**
 * ¿Hay que frenar el envío del resto del lote?
 *
 * Ante un 429 el servidor está pidiendo explícitamente que se pare. Seguir mandando las otras
 * operaciones es la forma más rápida de que el rate limiter deje al dispositivo afuera más tiempo.
 */
export function debeFrenar(status: number | null | undefined): boolean {
  return status === 429 || status === 503;
}
