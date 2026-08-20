/**
 * Cómo se le presenta una sesión a una persona. Funciones **puras**: sin `this`, sin DI, sin HTTP.
 * Las usan tanto la administración de usuarios como «Mis dispositivos» del perfil.
 */

/** Lo mínimo que hace falta para describir una sesión. Deliberadamente estructural, no la clase del DTO. */
export interface SesionDescribible {
  deviceId: string | null;
  userAgent: string | null;
  expiresAt: string;
  revokedAt: string | null;
  lastSeenAt: string | null;
}

/** Estado con el que se pinta la fila. */
export type EstadoSesionUi = 'activa' | 'revocada' | 'vencida';

/**
 * Nombre legible del equipo.
 *
 * Prioriza el `userAgent` porque es lo que una persona reconoce («Android», «iPhone», «Windows»);
 * el `deviceId` es un UUID y sólo sirve de desempate entre dos equipos del mismo tipo.
 */
export function describirDispositivo(sesion: SesionDescribible): string {
  const plataforma = plataformaDeUserAgent(sesion.userAgent);
  const cola = sesion.deviceId ? ` · ${sesion.deviceId.slice(0, 8)}` : '';

  if (plataforma) return `${plataforma}${cola}`;
  return sesion.deviceId ? `Equipo ${sesion.deviceId.slice(0, 8)}` : 'Equipo desconocido';
}

/** ¿Es un equipo móvil? Cambia el ícono, nada más. */
export function esDispositivoMovil(sesion: SesionDescribible): boolean {
  const ua = (sesion.userAgent ?? '').toLowerCase();
  return ua.includes('android') || ua.includes('iphone') || ua.includes('ipad') || ua.includes('mobile');
}

/**
 * Estado de la sesión. **Revocada gana sobre vencida**, igual que en el backend
 * (`RevocacionSesionCalculos.Evaluar`): si alguien la apagó a propósito, eso es lo que hay que
 * mostrar, aunque además haya vencido.
 */
export function estadoDeSesion(sesion: SesionDescribible, ahoraMs: number): EstadoSesionUi {
  if (sesion.revokedAt) return 'revocada';
  return Date.parse(sesion.expiresAt) <= ahoraMs ? 'vencida' : 'activa';
}

/**
 * «Hace 3 minutos», «hace 2 horas»… El último contacto es el dato que decide si una sesión
 * sospechosa está viva ahora mismo o quedó abierta hace días.
 */
export function haceCuanto(iso: string | null, ahoraMs: number): string {
  if (!iso) return 'sin contacto todavía';

  const ms = ahoraMs - Date.parse(iso);
  if (!Number.isFinite(ms) || ms < 0) return 'recién';

  const minutos = Math.floor(ms / 60000);
  if (minutos < 1) return 'hace instantes';
  if (minutos < 60) return `hace ${minutos} min`;

  const horas = Math.floor(minutos / 60);
  if (horas < 24) return `hace ${horas} h`;

  const dias = Math.floor(horas / 24);
  return dias === 1 ? 'hace 1 día' : `hace ${dias} días`;
}

/** Marca de plataforma dentro del user-agent. Sin pretensiones: alcanza para reconocer el equipo. */
function plataformaDeUserAgent(userAgent: string | null): string | null {
  const ua = (userAgent ?? '').toLowerCase();
  if (!ua) return null;

  if (ua.includes('android')) return 'Android';
  if (ua.includes('iphone')) return 'iPhone';
  if (ua.includes('ipad')) return 'iPad';
  if (ua.includes('windows')) return 'Windows';
  if (ua.includes('mac os') || ua.includes('macintosh')) return 'Mac';
  if (ua.includes('linux')) return 'Linux';
  return null;
}
