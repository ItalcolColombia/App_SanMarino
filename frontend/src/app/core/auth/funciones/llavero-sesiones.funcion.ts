import { LIMITES_SESION_POR_DEFECTO } from './politica-sesion.funcion';
import type { DatosSlot, PadronSlots, SlotSesion } from '../models/slot-sesion.model';

/**
 * Reglas puras del padrón de sesiones aparcadas.
 *
 * Acá no hay `localStorage` ni cripto: solo qué entra, qué se actualiza y **a quién se expulsa**.
 * `LlaveroSesionesService` orquesta el I/O y delega en esto.
 *
 * ## La regla que ordena el archivo
 *
 * Expulsar un slot **purga su caché de consultas**, o sea que destruye su alistamiento —instalar la
 * app y entrar una vez con señal—, que en campo cuesta un viaje a la oficina con wifi. Por eso la
 * expulsión es LRU y **nunca** se lleva puesto a alguien con capturas sin sincronizar: antes que
 * destruir trabajo de campo, se niega la comodidad (R-M2). Su cola **jamás** se toca (R9).
 */

/**
 * Tope de slots por dispositivo (R-M1). El turno real son 2-3 operarios; el cuarto es margen.
 * No es arbitrario: cada slot cuesta una partición de caché contra una cuota finita, y el padrón es
 * una lista de blancos.
 */
export const MAX_SLOTS = 4;

/** Intentos de PIN antes de destruir el blob (§1.4). */
export const MAX_INTENTOS_PIN = 5;

/** Versión del formato del padrón. */
export const VERSION_PADRON = 1;

export const PADRON_VACIO: PadronSlots = { version: VERSION_PADRON, slots: [] };

/** Cuántas capturas espera cada slot, por `slotId`. Lo deriva el service del outbox por partición. */
export type PendientesPorSlot = Readonly<Record<string, number>>;

export type ResultadoRegistro =
  | { estado: 'registrado'; padron: PadronSlots; expulsado: SlotSesion | null }
  /**
   * No entra un quinto usuario porque los 4 tienen trabajo sin subir. El mensaje tiene que nombrar
   * a alguien concreto: «conectate y enviá las capturas de fulano» es accionable, «no hay lugar» no.
   */
  | { estado: 'rechazado'; motivo: 'todos_con_capturas_pendientes'; conPendientes: readonly SlotSesion[] };

/**
 * Da de alta —o actualiza— la entrada del usuario que acaba de entrar.
 *
 * Dos logins del mismo `userId` son el **mismo slot**: se actualiza, no se duplica, y **no cuenta
 * contra el tope**. Al actualizar se conservan el `slotId` y el `saltB64` que ya tenía: son las dos
 * cosas que atan la entrada a su blob ya cifrado.
 *
 * El `slotId` y el `saltB64` de un slot nuevo llegan **por parámetro** porque generarlos es azar, y
 * el azar rompe la pureza (y con ella el test).
 */
export function registrarSlot(
  padron: PadronSlots | null | undefined,
  datos: DatosSlot,
  pendientes: PendientesPorSlot,
  ahora: number
): ResultadoRegistro {
  const slots = normalizar(padron);
  const existente = slots.find(s => s.userId === datos.userId);

  if (existente) {
    return {
      estado: 'registrado',
      expulsado: null,
      padron: conSlots(slots.map(s => (s.userId === datos.userId ? actualizar(s, datos, ahora) : s)))
    };
  }

  const nuevo = nacer(datos, ahora);

  if (slots.length < MAX_SLOTS) {
    return { estado: 'registrado', expulsado: null, padron: conSlots([...slots, nuevo]) };
  }

  const victima = elegirVictima(slots, pendientes);
  if (victima === null) {
    return {
      estado: 'rechazado',
      motivo: 'todos_con_capturas_pendientes',
      conPendientes: [...slots].sort((a, b) => a.ultimoUsoEn - b.ultimoUsoEn)
    };
  }

  return {
    estado: 'registrado',
    expulsado: victima,
    padron: conSlots([...slots.filter(s => s.slotId !== victima.slotId), nuevo])
  };
}

/**
 * A quién se expulsa: el de uso más viejo **entre los que no tienen capturas esperando**.
 *
 * `null` significa «a nadie»: todos tienen trabajo sin subir. No es un error, es la respuesta
 * correcta — y la que obliga al llamador a explicar en vez de romper algo.
 */
export function elegirVictima(
  slots: readonly SlotSesion[],
  pendientes: PendientesPorSlot
): SlotSesion | null {
  const elegibles = slots.filter(s => (pendientes[s.slotId] ?? 0) === 0);
  if (elegibles.length === 0) {
    return null;
  }
  return [...elegibles].sort((a, b) => a.ultimoUsoEn - b.ultimoUsoEn)[0];
}

/**
 * ¿Este slot agotó su jornada offline (D4)?
 *
 * Se **deriva** de `ultimoContactoOkEn`, no se guarda como bandera: un booleano persistido sería una
 * segunda verdad sobre el mismo hecho, y ya sabemos cómo termina eso.
 *
 * Un slot vencido **no se borra solo**: borrar es purgar, y purgar es destruir el alistamiento de
 * alguien que quizá vuelve mañana. Se pinta apagado y se puede elegir igual — lleva al login con red.
 */
export function slotVencido(
  slot: SlotSesion,
  ahora: number,
  jornadaOfflineMs: number = LIMITES_SESION_POR_DEFECTO.jornadaOfflineMs
): boolean {
  return ahora - slot.ultimoContactoOkEn >= jornadaOfflineMs;
}

export interface ResultadoPinFallido {
  padron: PadronSlots;
  intentosRestantes: number;
  /** El blob hay que borrarlo: se agotaron los intentos. */
  destruir: boolean;
}

/**
 * Suma un intento fallido de PIN y decide si el blob se destruye.
 *
 * El PIN **no se compara con nada**: es la entrada del KDF, y el veredicto lo da el tag GCM. Acá solo
 * se lleva la cuenta. Al llegar a `MAX_INTENTOS_PIN` la entrada queda `requiereReingreso` y el
 * contador se reinicia — la entrada **se conserva**, porque desaparecer de la lista se lee como
 * «se perdió».
 */
export function registrarPinFallido(
  padron: PadronSlots | null | undefined,
  slotId: string
): ResultadoPinFallido {
  const slots = normalizar(padron);
  const slot = slots.find(s => s.slotId === slotId);

  if (!slot) {
    return { padron: conSlots(slots), intentosRestantes: 0, destruir: false };
  }

  const intentos = slot.intentosFallidos + 1;
  const destruir = intentos >= MAX_INTENTOS_PIN;

  return {
    destruir,
    intentosRestantes: destruir ? 0 : MAX_INTENTOS_PIN - intentos,
    padron: conSlots(
      slots.map(s =>
        s.slotId === slotId
          ? {
              ...s,
              intentosFallidos: destruir ? 0 : intentos,
              requiereReingreso: destruir ? true : s.requiereReingreso
            }
          : s
      )
    )
  };
}

/**
 * El slot se activó bien: se reinicia el contador de intentos y pasa a ser el más reciente.
 *
 * `ultimoContactoOkEn` **no** se toca acá: activar un slot no es hablar con el servidor, y confundir
 * las dos cosas renovaría la jornada de 16 h sin conexión — justo el tope que D4 puso para que un
 * dispositivo perdido no sea una ventana abierta indefinidamente.
 */
export function registrarUsoOk(
  padron: PadronSlots | null | undefined,
  slotId: string,
  ahora: number
): PadronSlots {
  return conSlots(
    normalizar(padron).map(s =>
      s.slotId === slotId ? { ...s, ultimoUsoEn: ahora, intentosFallidos: 0, requiereReingreso: false } : s
    )
  );
}

/** Hubo contacto real con el servidor: arranca de nuevo la jornada de ESE slot (R-M8). */
export function registrarContactoOk(
  padron: PadronSlots | null | undefined,
  slotId: string,
  ahora: number
): PadronSlots {
  return conSlots(
    normalizar(padron).map(s => (s.slotId === slotId ? { ...s, ultimoContactoOkEn: ahora } : s))
  );
}

/** Saca la entrada. Es «cerrar sesión» de ese slot (R-M6), no una limpieza automática. */
export function eliminarSlot(padron: PadronSlots | null | undefined, slotId: string): PadronSlots {
  return conSlots(normalizar(padron).filter(s => s.slotId !== slotId));
}

// ---------------------------------------------------------------------------

/**
 * Tolera el contrato real y no el ideal: el padrón sale de `localStorage`, así que puede venir de una
 * versión vieja, a medio escribir o pisado por otra pestaña. Ante cualquier duda, vacío.
 */
function normalizar(padron: PadronSlots | null | undefined): readonly SlotSesion[] {
  return Array.isArray(padron?.slots) ? padron.slots : [];
}

function conSlots(slots: readonly SlotSesion[]): PadronSlots {
  return { version: VERSION_PADRON, slots: [...slots] };
}

function nacer(datos: DatosSlot, ahora: number): SlotSesion {
  return {
    slotId: datos.slotId,
    userId: datos.userId,
    nombre: datos.nombre,
    email: datos.email,
    empresa: datos.empresa,
    companyId: datos.companyId,
    paisId: datos.paisId,
    ultimoUsoEn: ahora,
    // Recién sale de un login, o sea que acaba de hablar con el servidor.
    ultimoContactoOkEn: ahora,
    saltB64: datos.saltB64,
    intentosFallidos: 0
  };
}

function actualizar(slot: SlotSesion, datos: DatosSlot, ahora: number): SlotSesion {
  return {
    ...slot,
    // Puede haber cambiado de empresa entre logins; el slot es del USUARIO.
    nombre: datos.nombre,
    email: datos.email,
    empresa: datos.empresa,
    companyId: datos.companyId,
    paisId: datos.paisId,
    ultimoUsoEn: ahora,
    ultimoContactoOkEn: ahora,
    intentosFallidos: 0,
    requiereReingreso: false
  };
}
