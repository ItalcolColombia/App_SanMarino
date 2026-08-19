import { Injectable, signal } from '@angular/core';

import { nuevoUuid, obtenerDeviceId } from '../../core/auth/funciones/device-id.funcion';
import { claveParticion } from './funciones/clave-particion.funcion';
import { resolverTipoOperacion } from './funciones/decidir-encolable.funcion';
import {
  abrirBd,
  borrarOperacion,
  guardarOperacion,
  leerOperaciones,
  leerTodasLasOperaciones
} from './offline-db';
import type { IdentidadParticion } from './models/offline.model';
import type { EstadoOutbox, OperacionPendiente } from './models/outbox.model';


/**
 * Cola de capturas hechas sin red (F3).
 *
 * Orquestador delgado: la lista blanca, el backoff y la clasificación del resultado viven en
 * `funciones/`. Acá solo hay IndexedDB y estado.
 *
 * ## La regla que no se puede romper
 *
 * 🔴 **Nada borra la cola salvo la confirmación del servidor o una persona.** Ni el logout, ni el
 * cambio de empresa, ni el kill switch. Lo cacheado se vuelve a pedir; una captura encolada no
 * existe en ningún otro lado. Por eso `purgarTodo` de la caché de consultas no toca este store, y
 * por eso el safety worker nunca borra IndexedDB.
 */
@Injectable({ providedIn: 'root' })
export class OutboxService {
  /** Cuántas capturas esperan salir. Lo lee el indicador de la barra superior. */
  readonly pendientes = signal(0);

  /** Cuántas quedaron rechazadas esperando a una persona. */
  readonly rechazadas = signal(0);

  private bd: IDBDatabase | null = null;
  private intentoApertura: Promise<IDBDatabase | null> | null = null;

  private conexion(): Promise<IDBDatabase | null> {
    if (this.bd) {
      return Promise.resolve(this.bd);
    }
    this.intentoApertura ??= abrirBd().then(db => {
      this.bd = db;
      return db;
    });
    return this.intentoApertura;
  }

  /**
   * Cierra la conexión.
   *
   * Existe por la misma razón que en `CacheConsultasService`: **una conexión abierta bloquea
   * `deleteDatabase` para siempre**, así que sin esto la limpieza entre pruebas cuelga y Jasmine
   * culpa al test equivocado.
   */
  cerrarConexion(): void {
    this.bd?.close();
    this.bd = null;
    this.intentoApertura = null;
  }

  /**
   * Encola una mutación. Devuelve la operación encolada, o `null` si no se pudo (identidad
   * incompleta, ruta no soportada, IndexedDB no disponible).
   *
   * `null` es una señal importante: el interceptor **debe** propagar el error de red en ese caso.
   * Decir "guardado" sin haber guardado es el peor resultado posible de este módulo.
   */
  async encolar(
    identidad: IdentidadParticion,
    metodo: string,
    url: string,
    payload: unknown
  ): Promise<OperacionPendiente | null> {
    const particion = claveParticion(identidad);
    const tipo = resolverTipoOperacion(metodo, url);
    if (particion === null || tipo === null) {
      return null;
    }

    const operacion: OperacionPendiente = {
      clientOpId: nuevoUuid(),
      particion,
      tipo,
      companyId: Number(identidad.companyId),
      paisId: Number(identidad.paisId),
      userId: String(identidad.userId),
      deviceId: this.deviceId(),
      capturadoAtDispositivo: new Date().toISOString(),
      metodo: metodo.trim().toUpperCase(),
      url,
      payload,
      estado: 'pendiente',
      intentos: 0,
      proximoIntentoEn: null,
      creadoEn: Date.now()
    };

    try {
      const db = await this.conexion();
      if (!db) return null;
      await guardarOperacion(db, operacion);
      await this.refrescarContadores();
      return operacion;
    } catch {
      return null;
    }
  }

  /** Operaciones de la partición actual, más viejas primero. */
  async listar(identidad: IdentidadParticion): Promise<OperacionPendiente[]> {
    const particion = claveParticion(identidad);
    if (particion === null) return [];

    try {
      const db = await this.conexion();
      return db ? await leerOperaciones(db, particion) : [];
    } catch {
      return [];
    }
  }

  /** Toda la cola, sin filtrar. Para el diagnóstico y el contador global. */
  async listarTodas(): Promise<OperacionPendiente[]> {
    try {
      const db = await this.conexion();
      return db ? await leerTodasLasOperaciones(db) : [];
    } catch {
      return [];
    }
  }

  /** Guarda el nuevo estado de una operación (intentos, error, próximo intento). */
  async actualizar(operacion: OperacionPendiente): Promise<void> {
    try {
      const db = await this.conexion();
      if (!db) return;
      await guardarOperacion(db, operacion);
      await this.refrescarContadores();
    } catch {
      /* la cola nunca puede romper la app */
    }
  }

  /**
   * Saca una operación de la cola. **Solo** con confirmación del servidor o decisión explícita de
   * una persona: no hay ningún camino automático que borre trabajo no sincronizado.
   */
  async descartar(clientOpId: string): Promise<void> {
    try {
      const db = await this.conexion();
      if (!db) return;
      await borrarOperacion(db, clientOpId);
      await this.refrescarContadores();
    } catch {
      /* idem */
    }
  }

  async estado(): Promise<EstadoOutbox> {
    const todas = await this.listarTodas();
    return {
      disponible: this.bd !== null,
      pendientes: todas.filter(o => o.estado === 'pendiente').length,
      rechazadas: todas.filter(o => o.estado === 'rechazada').length,
      masAntigua: todas.length ? todas[0].creadoEn : null
    };
  }

  /** ¿Hay trabajo sin sincronizar? Lo consulta el cierre de sesión para no purgar nada. */
  async hayTrabajoPendiente(): Promise<boolean> {
    return (await this.listarTodas()).length > 0;
  }

  async refrescarContadores(): Promise<void> {
    const estado = await this.estado();
    this.pendientes.set(estado.pendientes);
    this.rechazadas.set(estado.rechazadas);
  }

  /**
   * Identificador del equipo. Se genera una vez y **persiste entre sesiones**: identifica la tablet,
   * no al usuario. Es informativo — el servidor jamás autoriza con esto.
   */
  private deviceId(): string {
    return obtenerDeviceId();
  }
}


