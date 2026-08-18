import type { AuthSession } from '../auth.models';

/**
 * Tipos del **llavero de sesiones aparcadas** (multi-slot).
 *
 * Viven en `models/` para que las funciones puras de `funciones/` se tipen sin importar servicios ni
 * componentes (convención de CLAUDE.md).
 *
 * ## Las tres capas de storage, y por qué el padrón NO va cifrado
 *
 * | Clave | Qué guarda | Cifrado |
 * |---|---|---|
 * | `auth_session` | la sesión **activa**, tal cual hoy | ❌ (igual que hoy) |
 * | `italgranja.slots.indice` | este padrón | ❌ **a propósito** |
 * | `italgranja.slots.<slotId>` | el `AuthSession` completo del aparcado | ✅ AES-GCM |
 *
 * El selector de perfil tiene que pintarse **sin red y sin PIN**. Cifrar el padrón exigiría una llave
 * del dispositivo —o sea, una llave en el bundle—, que es exactamente el «teatro» que B9 señaló. El
 * costo asumido: el padrón revela quiénes usan esa tablet y de qué empresa. Es **estrictamente menos**
 * de lo que se expone hoy, donde la única sesión guardada entrega el token, el menú y los permisos en
 * claro.
 */

/** Una entrada del padrón: lo justo para pintar el selector sin descifrar nada (R-M7). */
export interface SlotSesion {
  /** Id del slot. Nombra también la clave del blob: `italgranja.slots.<slotId>`. */
  slotId: string;

  /** Guid del usuario. Es la identidad real: dos logins del mismo `userId` son el **mismo** slot. */
  userId: string;

  nombre: string;
  email: string;

  /** Nombre de la empresa activa, para pintarlo. `companyId`/`paisId` son los que deciden. */
  empresa: string;
  companyId: number;
  paisId: number;

  /** Última vez que este slot estuvo activo. Es el criterio de expulsión (LRU, R-M2). */
  ultimoUsoEn: number;

  /**
   * Último contacto seguro con el servidor **de este slot**. La jornada de 16 h es por slot, no por
   * dispositivo (R-M8): que B haya hablado con el servidor hace 5 minutos no le renueva la jornada a A.
   */
  ultimoContactoOkEn: number;

  /** Salt del PBKDF2 de ESTE slot, en base64. Un salt es público por diseño. */
  saltB64: string;

  /** Intentos de PIN fallidos seguidos. A los 5 el blob se destruye (§1.4). */
  intentosFallidos: number;

  /**
   * El blob se destruyó (5 PIN fallidos) y ya no se puede activar sin red. La entrada **se conserva**
   * para poder decírselo al operario: desaparecer de la lista se lee como «se perdió».
   */
  requiereReingreso?: boolean;
}

/** Lo que se guarda en `italgranja.slots.indice`. */
export interface PadronSlots {
  /** Versión del formato. Permite migrar el padrón sin dejar tablets afuera. */
  version: number;
  slots: SlotSesion[];
}

/** Datos con los que nace o se actualiza una entrada, tomados de la sesión recién iniciada. */
export interface DatosSlot {
  userId: string;
  nombre: string;
  email: string;
  empresa: string;
  companyId: number;
  paisId: number;

  /** Solo se usa si el slot es NUEVO; al actualizar se conserva el que ya tenía. */
  slotId: string;
  saltB64: string;
}

/**
 * Resultado de activar un slot aparcado.
 *
 * Tipado y no un `boolean` porque los cuatro finales llevan a pantallas distintas, y porque el PIN
 * **no se compara**: el veredicto lo da el tag GCM al descifrar.
 */
export type ResultadoActivacion =
  | { estado: 'activado'; sesion: AuthSession }
  /** El tag GCM no validó. No hay forma de distinguir «PIN mal» de «blob corrupto», y está bien así. */
  | { estado: 'pin_incorrecto'; intentosRestantes: number }
  /** Se agotaron los intentos: el blob se borró. Se entra con red. */
  | { estado: 'slot_destruido' }
  /** Sin `crypto.subtle`, sin blob o sin entrada en el padrón: el llavero se comporta como si no existiera. */
  | { estado: 'no_disponible' };
