import { Injectable, signal } from '@angular/core';

import { claveEntrada, claveParticion } from './funciones/clave-particion.funcion';
import { antiguedadLegible, vigenciaCache } from './funciones/vigencia-cache.funcion';
import {
  abrirBd,
  contarEntradas,
  guardarEntrada,
  leerEntrada,
  leerTodas,
  purgarParticion,
  purgarTodo
} from './offline-db';
import type { EstadoCacheOffline, IdentidadParticion } from './models/offline.model';

/**
 * Caché de consultas para verlas sin red (F2).
 *
 * Orquestador delgado: la decisión de qué se cachea, bajo qué clave y hasta cuándo vive en las
 * funciones puras de `funciones/`. Acá solo hay IndexedDB y estado.
 *
 * ## Lo que NO hace
 *
 * No encola escrituras, no reenvía nada y no toca ningún saldo. Es solo lectura, y por eso su riesgo
 * de integridad es cero. La captura offline es F3 y sigue bloqueada por F0.A/F0.B.
 *
 * ## Por qué toda falla se traga
 *
 * Ningún error de la caché puede romper la app: si IndexedDB no está disponible (incógnito, política
 * del dispositivo, cuota llena), todo degrada a "no hay caché" y la app funciona exactamente como
 * antes de F2. Una caché que rompe la pantalla es peor que no tener caché.
 */
@Injectable({ providedIn: 'root' })
export class CacheConsultasService {
  /** La última respuesta servida vino de la caché (la barra de estado lo muestra). */
  readonly sirviendoDesdeCache = signal(false);

  /** Antigüedad legible de lo último servido desde caché. */
  readonly antiguedad = signal<string>('');

  private bd: IDBDatabase | null = null;
  private intentoApertura: Promise<IDBDatabase | null> | null = null;

  /** Abre la base una sola vez y reutiliza la promesa. */
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
   * Guarda una respuesta. Silencioso ante cualquier problema.
   * Devuelve `false` si no se guardó (sin partición válida, sin base, cuota llena…).
   */
  async guardar(identidad: IdentidadParticion, metodo: string, url: string, cuerpo: unknown): Promise<boolean> {
    const particion = claveParticion(identidad);
    if (particion === null) {
      // Fail-closed: sin identidad completa no se escribe nada. Ver `claveParticion`.
      return false;
    }

    try {
      const db = await this.conexion();
      if (!db) return false;

      await guardarEntrada(db, {
        clave: claveEntrada(particion, metodo, url),
        particion,
        cuerpo,
        guardadoEn: Date.now(),
        url
      });
      return true;
    } catch {
      return false;
    }
  }

  /**
   * Recupera una respuesta guardada, si existe y **está vigente**.
   *
   * Devuelve `undefined` cuando no hay nada que servir — distinto de `null`, que es un cuerpo
   * legítimamente nulo que alguna vez respondió el backend.
   */
  async recuperar(identidad: IdentidadParticion, metodo: string, url: string): Promise<{ cuerpo: unknown } | undefined> {
    const particion = claveParticion(identidad);
    if (particion === null) {
      return undefined;
    }

    try {
      const db = await this.conexion();
      if (!db) return undefined;

      const entrada = await leerEntrada(db, claveEntrada(particion, metodo, url));
      if (!entrada) return undefined;

      const ahora = Date.now();
      if (vigenciaCache(entrada.guardadoEn, ahora) === 'vencida') {
        // TTL duro: se propaga el error de red en vez de mostrar un número viejo como si fuera
        // de hoy. Ver `vigenciaCache`.
        return undefined;
      }

      this.sirviendoDesdeCache.set(true);
      this.antiguedad.set(antiguedadLegible(entrada.guardadoEn, ahora));

      return { cuerpo: entrada.cuerpo };
    } catch {
      return undefined;
    }
  }

  /** Vuelve al estado "en línea": lo llama el interceptor cuando una respuesta llega de la red. */
  marcarRespuestaDeRed(): void {
    if (this.sirviendoDesdeCache()) {
      this.sirviendoDesdeCache.set(false);
      this.antiguedad.set('');
    }
  }

  /**
   * Borra la partición indicada. Se usa al **cambiar de empresa**: el dato de la anterior no tiene
   * por qué seguir en el dispositivo.
   */
  async purgarParticionDe(identidad: IdentidadParticion): Promise<void> {
    const particion = claveParticion(identidad);
    if (particion === null) return;

    try {
      const db = await this.conexion();
      if (db) await purgarParticion(db, particion);
    } catch {
      /* la caché nunca rompe la app */
    }
  }

  /**
   * Borra TODA la caché. Se usa en **logout**: el dispositivo puede pasar a otras manos, y a esa
   * altura no se sabe qué particiones quedaron de sesiones anteriores.
   */
  async purgarTodo(): Promise<void> {
    try {
      const db = await this.conexion();
      if (db) await purgarTodo(db);
      this.sirviendoDesdeCache.set(false);
      this.antiguedad.set('');
    } catch {
      /* la caché nunca rompe la app */
    }
  }

  /**
   * Cierra la conexión con IndexedDB.
   *
   * En la app no hace falta —la conexión vive lo que vive la pestaña—, pero **una conexión abierta
   * bloquea indefinidamente cualquier `deleteDatabase`**. Sin esto, una suite que borra la base
   * entre pruebas se traba, y el síntoma es un timeout que apunta a la prueba en vez de a la
   * limpieza. También es la salida correcta si alguna vez hay que recrear el esquema en caliente.
   */
  cerrarConexion(): void {
    try {
      this.bd?.close();
    } catch {
      /* la caché nunca rompe la app */
    }
    this.bd = null;
    this.intentoApertura = null;
  }

  /** Estado para la pantalla de diagnóstico. */
  async estado(identidad: IdentidadParticion): Promise<EstadoCacheOffline> {
    const particion = claveParticion(identidad);

    try {
      const db = await this.conexion();
      if (!db) {
        return { disponible: false, entradas: 0, particionActual: particion, entradaMasAntigua: null, entradaMasReciente: null };
      }

      const entradas = await contarEntradas(db);
      const todas = await leerTodas(db);
      const fechas = todas.map(e => e.guardadoEn).filter(Number.isFinite);

      return {
        disponible: true,
        entradas,
        particionActual: particion,
        entradaMasAntigua: fechas.length ? Math.min(...fechas) : null,
        entradaMasReciente: fechas.length ? Math.max(...fechas) : null
      };
    } catch {
      return { disponible: false, entradas: 0, particionActual: particion, entradaMasAntigua: null, entradaMasReciente: null };
    }
  }
}
