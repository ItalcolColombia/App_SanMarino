import { InjectionToken, Injectable, inject } from '@angular/core';

import { CacheConsultasService } from '../../shared/offline/cache-consultas.service';
import { OutboxService } from '../../shared/offline/outbox.service';
import { claveParticion } from '../../shared/offline/funciones/clave-particion.funcion';
import { TokenStorageService } from './token-storage.service';
import {
  type FuenteCripto,
  abrir,
  derivarLlave,
  hayCripto,
  nuevoIdSlot,
  nuevoSaltB64,
  sellar
} from './funciones/cripto-llavero.funcion';
import {
  PADRON_VACIO,
  type PendientesPorSlot,
  type ResultadoRegistro,
  eliminarSlot,
  registrarContactoOk,
  registrarPinFallido,
  registrarSlot,
  registrarUsoOk
} from './funciones/llavero-sesiones.funcion';
import type { AuthSession } from './auth.models';
import type { DatosSlot, PadronSlots, ResultadoActivacion, SlotSesion } from './models/slot-sesion.model';

/** Padrón: quiénes usan esta tablet. Sin cifrar a propósito (el selector se pinta sin PIN). */
const CLAVE_PADRON = 'italgranja.slots.indice';

/** Prefijo del blob cifrado de cada slot. */
const PREFIJO_BLOB = 'italgranja.slots.';

/**
 * De dónde sale la cripto. En la app es `globalThis.crypto` y nadie lo provee.
 *
 * Existe como seam **para poder probar la ausencia**: «sin `crypto.subtle` el llavero se apaga entero»
 * es la propiedad más importante de este módulo, y en Chrome —donde corren los tests— `crypto.subtle`
 * está siempre. Sin este token esa rama no se puede ejercitar y quedaría escrita pero no verificada,
 * que es lo mismo que no estar. Mismo criterio que `TRABAJO_PENDIENTE_OFFLINE`.
 */
export const FUENTE_CRIPTO_LLAVERO = new InjectionToken<FuenteCripto>('FUENTE_CRIPTO_LLAVERO');

/**
 * Llavero de sesiones aparcadas.
 *
 * Orquestador delgado: acá vive el I/O de `localStorage` y el pegamento con la caché y el outbox; las
 * dos decisiones —a quién se expulsa y cómo se cifra— están en `funciones/`, puras y con tests.
 *
 * ## Por qué `auth_session` no se toca
 *
 * La sesión **activa** sigue siendo, byte a byte, la de hoy. El multi-slot se construye **al lado**:
 * activar un slot es escribir su blob descifrado en `auth_session` y recargar. Eso deja en cero los
 * cambios en el interceptor, los guards de permisos, los 33 módulos de features y los ~190
 * componentes. El costo es que una sesión tiene dos representaciones (activa en claro, aparcada
 * cifrada), y se paga en un **único punto de conversión**: `aparcar()` y `activar()`.
 *
 * ## Fail-closed, y sin tocar la cola
 *
 * Sin `crypto.subtle` el llavero **no existe** (`disponible()` en `false`) y la app se comporta como
 * hoy: una sola sesión. Todo el I/O va en `try/catch` porque `localStorage` puede fallar por política
 * o por cuota, y un llavero roto no puede impedir entrar a trabajar.
 *
 * **Nada de acá borra el outbox** (R9), ni al expulsar un slot ni al eliminarlo: lo cacheado se vuelve
 * a pedir, una captura de campo no existe en ningún otro lado.
 */
@Injectable({ providedIn: 'root' })
export class LlaveroSesionesService {
  private readonly storage = inject(TokenStorageService);
  private readonly cacheOffline = inject(CacheConsultasService);
  private readonly outbox = inject(OutboxService);

  /** `null` significa «sin cripto» y apaga el llavero; ausente = el `crypto` del navegador. */
  private readonly cripto: FuenteCripto =
    inject(FUENTE_CRIPTO_LLAVERO, { optional: true }) ?? globalThis.crypto;

  /** ¿Se puede usar el llavero en este dispositivo? Sin cripto real, no. */
  disponible(): boolean {
    return hayCripto(this.cripto);
  }

  /** El padrón, tolerando que venga de otra versión, a medio escribir o pisado por otra pestaña. */
  leerPadron(): PadronSlots {
    try {
      const crudo = localStorage.getItem(CLAVE_PADRON);
      if (!crudo) return PADRON_VACIO;

      const padron = JSON.parse(crudo) as PadronSlots;
      return Array.isArray(padron?.slots) ? padron : PADRON_VACIO;
    } catch {
      return PADRON_VACIO;
    }
  }

  /** Los slots que NO son el de la sesión activa: son los que el selector ofrece. */
  slotsAparcados(): SlotSesion[] {
    const activo = this.storage.get()?.user?.id ?? null;
    return this.leerPadron().slots.filter(s => s.userId !== activo);
  }

  /**
   * Registra —o refresca— el slot del usuario que acaba de entrar.
   *
   * ⚠️ **Nunca bloquea el login.** Si el padrón está lleno y los 4 tienen capturas sin subir, devuelve
   * `rechazado` y el padrón **queda como estaba**: este usuario se queda sin slot, pero su sesión
   * arranca igual. Es la única salida honesta mientras no exista el selector — negarle la entrada a
   * alguien que el servidor ya autenticó, por una cola que no puede ver ni resolver, sería peor que el
   * problema. Quien decide qué hacer con el rechazo es la UI del paso 7.
   */
  async registrarLogin(sesion: AuthSession, ahora: number = Date.now()): Promise<ResultadoRegistro> {
    const padron = this.leerPadron();

    // El gate va PRIMERO y no como parte de la condición de abajo: `datosDe` genera salt y uuid, o sea
    // que ya necesita cripto. Dejarlo después funcionaba por el orden en que se evalúan los campos,
    // que es exactamente la clase de razonamiento que el próximo cambio rompe sin darse cuenta.
    if (!this.disponible()) {
      return { estado: 'registrado', padron, expulsado: null };
    }

    const datos = this.datosDe(sesion, padron);
    if (datos === null) {
      return { estado: 'registrado', padron, expulsado: null };
    }

    const resultado = registrarSlot(padron, datos, await this.pendientesPorSlot(padron.slots), ahora);
    if (resultado.estado === 'rechazado') {
      return resultado;
    }

    // Expulsar purga la caché de esa partición y su blob — jamás su cola (R9).
    if (resultado.expulsado) {
      await this.desalojar(resultado.expulsado);
    }

    this.guardarPadron(resultado.padron);
    return resultado;
  }

  /**
   * Aparca la sesión activa: la cifra con el PIN y la deja en el llavero.
   *
   * Devuelve `false` sin escribir nada si algo falta. **No** borra la sesión activa: de eso se encarga
   * quien llame, después de confirmar que el blob quedó guardado. Al revés se perdería la sesión si el
   * sellado falla.
   */
  async aparcar(pin: string, sesion: AuthSession | null = this.storage.get()): Promise<boolean> {
    const slot = sesion ? this.slotDe(sesion.user?.id ?? null) : null;
    if (!this.disponible() || !sesion || !slot) {
      return false;
    }

    const llave = await derivarLlave(pin, slot.saltB64, this.cripto);
    if (!llave) return false;

    const blob = await sellar(sesion, llave, this.cripto);
    if (!blob) return false;

    try {
      localStorage.setItem(PREFIJO_BLOB + slot.slotId, blob);
      return true;
    } catch {
      // Cuota o política: mejor no aparcar que dejar creer que se aparcó.
      return false;
    }
  }

  /**
   * Activa un slot aparcado: descifra su sesión con el PIN y la deja como sesión **activa**.
   *
   * El PIN no se compara con nada: si está mal, el tag GCM no valida y `abrir` lanza. A los
   * `MAX_INTENTOS_PIN` fallidos el blob se destruye y el slot queda marcado `requiereReingreso` — la
   * entrada **se conserva**, para poder decírselo al operario.
   *
   * Quien llame tiene que **recargar la página** después de un `activado`: hay estado en memoria en
   * decenas de servicios, y recargar es la única garantía estructural de que nada de la empresa
   * anterior sobreviva.
   */
  async activar(slotId: string, pin: string, ahora: number = Date.now()): Promise<ResultadoActivacion> {
    const slot = this.leerPadron().slots.find(s => s.slotId === slotId);
    const blob = this.leerBlob(slotId);

    if (!this.disponible() || !slot || !blob) {
      return { estado: 'no_disponible' };
    }

    const llave = await derivarLlave(pin, slot.saltB64, this.cripto);
    if (!llave) {
      return { estado: 'no_disponible' };
    }

    let sesion: AuthSession;
    try {
      sesion = await abrir(blob, llave, this.cripto);
    } catch {
      return this.anotarPinFallido(slotId);
    }

    if (!sesion?.accessToken) {
      // Descifró pero no es una sesión usable: no se pisa la activa con basura.
      return { estado: 'no_disponible' };
    }

    // El blob ya no hace falta: esta sesión pasa a ser la activa.
    this.borrarBlob(slotId);
    this.guardarPadron(registrarUsoOk(this.leerPadron(), slotId, ahora));
    this.storage.save(sesion, true);

    return { estado: 'activado', sesion };
  }

  /** Hubo contacto real con el servidor: arranca de nuevo la jornada de ESE slot (R-M8). */
  marcarContactoOk(userId: string | null | undefined, ahora: number = Date.now()): void {
    const slot = this.slotDe(userId ?? null);
    if (!slot) return;
    this.guardarPadron(registrarContactoOk(this.leerPadron(), slot.slotId, ahora));
  }

  /** Elimina un slot: su entrada, su blob y su caché. Su cola queda intacta (R9). */
  async eliminar(slotId: string): Promise<void> {
    const slot = this.leerPadron().slots.find(s => s.slotId === slotId);
    if (slot) {
      await this.desalojar(slot);
    }
    this.guardarPadron(eliminarSlot(this.leerPadron(), slotId));
  }

  /** El equipo cambia de manos: se van todos los slots y sus blobs. La cola sigue intacta (R9). */
  borrarTodos(): void {
    for (const slot of this.leerPadron().slots) {
      this.borrarBlob(slot.slotId);
    }
    try {
      localStorage.removeItem(CLAVE_PADRON);
    } catch {
      /* un llavero que no se puede borrar no puede romper la app */
    }
  }

  // ---------------------------------------------------------------------------

  /**
   * Cuántas capturas espera cada slot. Se **deriva** del outbox en el momento, por partición: tenerlo
   * guardado en el padrón sería un segundo número para la misma verdad.
   *
   * Pública porque la usan dos consumidores con el mismo criterio: la expulsión —que no puede
   * llevarse puesto trabajo sin subir— y el selector, que muestra el número para que «¿dónde quedó lo
   * que cargué?» tenga respuesta.
   */
  async pendientesPorSlot(slots: readonly SlotSesion[]): Promise<PendientesPorSlot> {
    const cola = await this.outbox.listarTodas();
    if (cola.length === 0 || slots.length === 0) {
      return {};
    }

    const porParticion = new Map<string, number>();
    for (const op of cola) {
      porParticion.set(op.particion, (porParticion.get(op.particion) ?? 0) + 1);
    }

    const pendientes: Record<string, number> = {};
    for (const slot of slots) {
      const particion = claveParticion(slot);
      pendientes[slot.slotId] = particion === null ? 0 : porParticion.get(particion) ?? 0;
    }
    return pendientes;
  }

  /** Saca del dispositivo lo reconstruible de un slot: su blob y su caché. NUNCA su cola. */
  private async desalojar(slot: SlotSesion): Promise<void> {
    this.borrarBlob(slot.slotId);
    await this.cacheOffline.purgarParticionDe(slot);
  }

  private anotarPinFallido(slotId: string): ResultadoActivacion {
    const { padron, intentosRestantes, destruir } = registrarPinFallido(this.leerPadron(), slotId);
    this.guardarPadron(padron);

    if (destruir) {
      this.borrarBlob(slotId);
      return { estado: 'slot_destruido' };
    }
    return { estado: 'pin_incorrecto', intentosRestantes };
  }

  /**
   * Datos del slot a partir de la sesión. `null` si falta cualquiera de los tres ids de partición: sin
   * ellos no se puede ni purgar su caché ni contar su cola, así que no hay slot que registrar.
   */
  private datosDe(sesion: AuthSession, padron: PadronSlots): DatosSlot | null {
    const userId = sesion.user?.id ?? null;
    const companyId = sesion.activeCompanyId ?? null;
    const paisId = sesion.activePaisId ?? null;

    if (claveParticion({ userId, companyId, paisId }) === null) {
      return null;
    }

    const existente = padron.slots.find(s => s.userId === userId);

    // En un re-login estos dos se descartan (la función pura conserva los del slot), pero se calculan
    // igual: si la fuente de cripto está apagada, acá es donde se corta.
    const saltB64 = existente?.saltB64 ?? nuevoSaltB64(this.cripto);
    const slotId = existente?.slotId ?? nuevoIdSlot(this.cripto);
    if (!saltB64 || !slotId) {
      return null;
    }

    return {
      userId: String(userId),
      nombre: this.nombreDe(sesion),
      email: sesion.user?.username ?? '',
      empresa: sesion.activeCompany ?? '',
      companyId: Number(companyId),
      paisId: Number(paisId),
      slotId,
      saltB64
    };
  }

  private nombreDe(sesion: AuthSession): string {
    const u = sesion.user;
    const compuesto = [u?.firstName, u?.surName].filter(Boolean).join(' ').trim();
    return u?.fullName?.trim() || compuesto || u?.username || 'Operario';
  }

  private slotDe(userId: string | null): SlotSesion | null {
    if (!userId) return null;
    return this.leerPadron().slots.find(s => s.userId === userId) ?? null;
  }

  private leerBlob(slotId: string): string | null {
    try {
      return localStorage.getItem(PREFIJO_BLOB + slotId);
    } catch {
      return null;
    }
  }

  private borrarBlob(slotId: string): void {
    try {
      localStorage.removeItem(PREFIJO_BLOB + slotId);
    } catch {
      /* idem */
    }
  }

  private guardarPadron(padron: PadronSlots): void {
    try {
      localStorage.setItem(CLAVE_PADRON, JSON.stringify(padron));
    } catch {
      /* idem */
    }
  }
}
