/**
 * Extrae un mensaje legible de un error HTTP de Angular.
 *
 * Por qué existe: `HttpErrorResponse.message` es SIEMPRE el genérico de Angular
 * ("Http failure response for <url>: <status> <statusText>"), nunca el motivo real — hay que leer
 * `err.error`. El backend tiene DOS formas de responder un error: `{ error }` / `{ message }`
 * (excepciones de negocio) y, cuando el JSON del body no calza con el contrato ANTES de que el
 * controller corra (ej. un decimal tipeado en un campo `int`), la respuesta automática de
 * `[ApiController]` — `{ title, errors: { campo: [...] } }`. Sin este helper, ese segundo caso caía
 * al genérico de Angular y el modal mostraba "Http failure response…" sin decir nada (liquidación
 * Panamá, lote 13-1, 26-ago-2026).
 */
export function extraerMensajeError(err: unknown, mensajePorDefecto: string): string {
  const body = (err as { error?: unknown } | null | undefined)?.error;

  if (typeof body === 'string' && body.trim().length > 0) return body.trim();

  if (body && typeof body === 'object') {
    const b = body as {
      error?: string;
      message?: string;
      title?: string;
      errors?: Record<string, string[] | string>;
    };
    if (b.error) return b.error;
    if (b.message) return b.message;
    if (b.errors && typeof b.errors === 'object') {
      const detalles = Object.entries(b.errors)
        .map(([campo, msgs]) => {
          const texto = Array.isArray(msgs) ? msgs.join(' ') : String(msgs ?? '');
          const nombre = campo.replace(/^\$\.?/, '');
          return nombre && texto ? `${nombre}: ${texto}` : texto;
        })
        .filter(t => t.trim().length > 0);
      if (detalles.length > 0) return detalles.join(' | ');
    }
    if (b.title) return b.title;
  }

  return mensajePorDefecto;
}
