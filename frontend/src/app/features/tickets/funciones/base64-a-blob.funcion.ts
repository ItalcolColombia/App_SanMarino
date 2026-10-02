// src/app/features/tickets/funciones/base64-a-blob.funcion.ts

/**
 * Quita el prefijo de data URL (`data:<tipo>;base64,`) y los espacios. Base64 puro vuelve igual.
 * El formulario de creación de tickets guardaba la data URL completa: al descargar se le anteponía
 * otro prefijo y el archivo salía dañado.
 */
export function base64Puro(contenido: string | null | undefined): string {
  let s = (contenido ?? '').trim();
  if (/^data:/i.test(s)) {
    const coma = s.indexOf(',');
    s = coma >= 0 ? s.slice(coma + 1) : '';
  }
  return s.replace(/\s+/g, '');
}

/** Decodifica el contenido (base64 puro o data URL) a un `Blob` del tipo indicado. */
export function base64ABlob(contenido: string | null | undefined, contentType?: string | null): Blob {
  const binario = atob(base64Puro(contenido));
  const bytes = new Uint8Array(binario.length);
  for (let i = 0; i < binario.length; i++) bytes[i] = binario.charCodeAt(i);
  return new Blob([bytes], { type: contentType || 'application/octet-stream' });
}
