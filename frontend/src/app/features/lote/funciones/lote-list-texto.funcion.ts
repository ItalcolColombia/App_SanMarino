// src/app/features/lote/funciones/lote-list-texto.funcion.ts
// Formato de número y normalización de texto — extraído de LoteListComponent.
// Funciones PURAS: sin `this`, sin DI, sin estado del componente.
//
// No se migró al formateador central de shared/utils/format.ts: `formatNumber` acá no admite
// decimales (siempre entero), firma distinta a `formatearNumero`. Adoptarlo a la fuerza cambiaría
// la salida (refactor ≠ cambio de comportamiento) — ver CLAUDE.md, sistema de diseño compartido.

// Rango Unicode de diacriticos combinables (U+0300-U+036F), construido por código de punto en vez
// de caracter literal: un caracter combinable pegado en el fuente se corrompe con demasiada
// facilidad al pasar por herramientas de texto (heredocs, editores) sin que ningún build lo note.
const COMBINING_DIACRITICS = new RegExp(
  '[' + String.fromCharCode(0x0300) + '-' + String.fromCharCode(0x036f) + ']', 'g'
);

export function formatNumber(value: number | null | undefined): string {
  if (value == null) return '0';
  return value.toLocaleString('es-CO', { minimumFractionDigits: 0, maximumFractionDigits: 0 });
}

/** Quita tildes y pasa a minúsculas, para comparar texto sin acentos. */
export function normalize(s: string): string {
  return (s || '')
    .toLowerCase()
    .normalize('NFD')
    .replace(COMBINING_DIACRITICS, '');
}

/** `null`/`undefined`/cadena vacía (tras trim) → `null`; cualquier otra cosa → su texto. */
export function textoOrNull(value: unknown): string | null {
  const texto = value == null ? '' : String(value).trim();
  return texto === '' ? null : texto;
}
