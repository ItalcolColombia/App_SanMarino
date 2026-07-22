/**
 * Helpers puros de fecha para el modal de seguimiento engorde.
 * Sin `this`, sin DI, sin estado de Angular: reciben datos y devuelven un resultado.
 */
import { LoteDto } from '../../lote/services/lote.service';

/** Hoy en formato YYYY-MM-DD (local, sin zona) para <input type="date"> */
export function todayYMD(): string {
  const d = new Date();
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const dd = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${mm}-${dd}`;
}

/**
 * Calcula la fecha por defecto para un nuevo registro:
 * → día siguiente al último registro existente; si no hay registros,
 *   día siguiente al encasillamiento del lote; si nada aplica, hoy.
 * El usuario puede cambiarla libremente en el formulario.
 */
export function computeDefaultFecha(
  existingFechas: string[],
  selectedLoteId: number | null,
  lotes: LoteDto[]
): string {
  // 1. Buscar la fecha más reciente entre los registros ya creados
  let latest: Date | null = null;
  for (const raw of existingFechas) {
    if (!raw) continue;
    const ymd = toYMD(raw); // tz-aware: no resta un día con strings con offset
    if (!ymd) continue;
    const d = new Date(ymd + 'T00:00:00');
    if (!isNaN(d.getTime()) && (!latest || d > latest)) {
      latest = d;
    }
  }

  // 2. Si no hay registros, usar la fecha de encasillamiento del lote
  if (!latest && selectedLoteId) {
    const lote = lotes.find(l => String(l.loteId) === String(selectedLoteId));
    const encaset = lote?.fechaEncaset;
    if (encaset) {
      const ymd = toYMD(String(encaset));
      const d = ymd ? new Date(ymd + 'T00:00:00') : null;
      if (d && !isNaN(d.getTime())) latest = d;
    }
  }

  // 3. Sumar 1 día y devolver YYYY-MM-DD; si nada aplica, hoy
  if (latest) {
    latest.setDate(latest.getDate() + 1);
    const y = latest.getFullYear();
    const m = String(latest.getMonth() + 1).padStart(2, '0');
    const dd = String(latest.getDate()).padStart(2, '0');
    return `${y}-${m}-${dd}`;
  }
  return todayYMD();
}

/** Normaliza cadenas mm/dd/aaaa, dd/mm/aaaa, ISO o Date a YYYY-MM-DD (local) */
export function toYMD(input: string | Date | null | undefined): string | null {
  if (!input) return null;

  if (input instanceof Date && !isNaN(input.getTime())) {
    const y = input.getFullYear();
    const m = String(input.getMonth() + 1).padStart(2, '0');
    const d = String(input.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}`;
  }

  const s = String(input).trim();

  // YYYY-MM-DD
  const ymd = /^(\d{4})-(\d{2})-(\d{2})$/;
  const m1 = s.match(ymd);
  if (m1) return `${m1[1]}-${m1[2]}-${m1[3]}`;

  // mm/dd/aaaa o dd/mm/aaaa
  const sl = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/;
  const m2 = s.match(sl);
  if (m2) {
    let a = parseInt(m2[1], 10);
    let b = parseInt(m2[2], 10);
    const yyyy = parseInt(m2[3], 10);
    let mm = a, dd = b;
    if (a > 12 && b <= 12) { mm = b; dd = a; }
    const mmS = String(mm).padStart(2, '0');
    const ddS = String(dd).padStart(2, '0');
    return `${yyyy}-${mmS}-${ddS}`;
  }

  // ISO (con T). Sin zona → literal (la API guarda la fecha digitada tal cual);
  // con Z/offset → fecha UTC del instante (las "fechas puras" viajan ancladas dentro
  // del día UTC intencional, así que extraer en LOCAL restaría un día en UTC-5).
  if (/^\d{4}-\d{2}-\d{2}T/.test(s)) {
    if (!/(?:Z|[+-]\d{2}:?\d{2})$/.test(s)) return s.slice(0, 10);
    const dIso = new Date(s);
    if (!isNaN(dIso.getTime())) return dIso.toISOString().slice(0, 10);
  }

  // Otros formatos parseables → extracción LOCAL (comportamiento previo)
  const d = new Date(s);
  if (!isNaN(d.getTime())) {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  return null;
}

/** Convierte YYYY-MM-DD a ISO asegurando MEDIODÍA local → evita cruzar de día por zona horaria */
export function ymdToIsoAtNoon(ymd: string): string {
  const iso = new Date(`${ymd}T12:00:00`);
  return iso.toISOString();
}
