// Funciones PURAS de DB Studio (sin DI, sin this, sin estado). Reutilizables y testeables.
import type { ColumnDto } from '../models/db-studio.models';

/** Construye el objeto WHERE para identificar una fila: usa PK si existe, si no todas las columnas. */
export function construirWherePk(
  row: Record<string, unknown>,
  columns: ColumnDto[]
): Record<string, unknown> {
  const pks = columns.filter(c => c.isPrimaryKey).map(c => c.name);
  const keys = pks.length > 0 ? pks : columns.map(c => c.name);
  const where: Record<string, unknown> = {};
  for (const k of keys) where[k] = row[k] ?? null;
  return where;
}

/** Serializa filas a CSV (con escape RFC-4180). */
export function filasACsv(columns: string[], rows: Record<string, unknown>[]): string {
  const cell = (v: unknown): string => {
    const s = v === null || v === undefined ? '' : String(v);
    return /[",\n\r]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  const lines = [columns.map(cell).join(',')];
  for (const r of rows) lines.push(columns.map(c => cell(r[c])).join(','));
  return lines.join('\n');
}

/** Descarga un blob de texto en el navegador. */
export function descargarTexto(filename: string, content: string, mime = 'text/plain'): void {
  const blob = new Blob([content], { type: mime });
  descargarBlob(blob, filename);
}

/** Descarga un blob ya armado (ej. la respuesta binaria de un export/backup) con el nombre dado. */
export function descargarBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

/** Extrae el filename de un header Content-Disposition ("attachment; filename=\"x.sql\""). */
export function filenameDesdeContentDisposition(headerValue: string | null, fallback: string): string {
  if (!headerValue) return fallback;
  const match = /filename\*?=(?:UTF-8''|")?([^";]+)"?/i.exec(headerValue);
  return match?.[1] ? decodeURIComponent(match[1]) : fallback;
}

/** Representa un valor de celda de forma compacta para la grilla. */
export function formatCell(v: unknown): string {
  if (v === null || v === undefined) return '∅';
  if (typeof v === 'object') return JSON.stringify(v);
  return String(v);
}

/** Antigüedad legible de una sesión (ej. "3m 12s"). */
export function antiguedad(iso?: string): string {
  if (!iso) return '—';
  const ms = Date.now() - new Date(iso).getTime();
  if (isNaN(ms) || ms < 0) return '—';
  const s = Math.floor(ms / 1000);
  if (s < 60) return `${s}s`;
  const m = Math.floor(s / 60);
  if (m < 60) return `${m}m ${s % 60}s`;
  const h = Math.floor(m / 60);
  return `${h}h ${m % 60}m`;
}

/** Color del badge según el estado de la sesión. */
export function colorEstado(state?: string): string {
  switch (state) {
    case 'active': return 'bg-ital-green/15 text-ital-green';
    case 'idle': return 'bg-slate-100 text-slate-500';
    case 'idle in transaction': return 'bg-amber-100 text-amber-700';
    default: return 'bg-slate-100 text-slate-500';
  }
}
