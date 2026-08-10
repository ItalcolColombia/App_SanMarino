/**
 * Tipos compartidos de la capa PWA.
 *
 * Viven en `models/` y no dentro de un componente para que las funciones puras de
 * `funciones/` puedan tiparse sin importar el componente (import circular). Es la
 * convención de CLAUDE.md — ver `funciones/README.md`.
 */

/** Qué debe hacer la app ante un evento del ciclo de vida del Service Worker. */
export type AccionActualizacion =
  /** No hay nada que hacer (versión detectada pero todavía descargando, o misma versión). */
  | 'ninguna'
  /** Hay una versión lista en disco: ofrecer al usuario aplicarla cuando quiera. */
  | 'ofrecer'
  /** El SW quedó en un estado del que no puede salir: hay que recargar sí o sí. */
  | 'recargar-forzado';

/** Resultado de evaluar un evento, con el motivo legible para el diagnóstico. */
export interface DecisionActualizacion {
  accion: AccionActualizacion;
  /** Texto corto para la pantalla de diagnóstico y los logs. Nunca se le muestra al operario. */
  motivo: string;
}

/** Forma mínima de los eventos de `SwUpdate.versionUpdates` que nos interesan. */
export interface EventoVersionSw {
  type: string;
  /** Presente en VERSION_READY / VERSION_DETECTED / VERSION_INSTALLATION_FAILED. */
  version?: { hash?: string };
  currentVersion?: { hash?: string };
  latestVersion?: { hash?: string };
  error?: string;
}

/** Estado del Service Worker tal como lo muestra la pantalla de diagnóstico. */
export interface EstadoSw {
  /** `serviceWorker` existe en este navegador. */
  soportado: boolean;
  /** Hay un registro para este scope. */
  registrado: boolean;
  /** El SW controla ESTA página (falso en el primer load, verdadero a partir del segundo). */
  controlando: boolean;
  /** Etiqueta corta lista para pintar. */
  etiqueta: string;
  /** Semáforo para el color del indicador. */
  severidad: 'ok' | 'aviso' | 'error';
}

/** Snapshot completo del dispositivo, exportable a JSON para soporte. */
export interface DiagnosticoPwa {
  generadoEn: string;
  buildId: string;
  url: string;
  sw: EstadoSw;
  enLinea: boolean;
  modoInstalado: boolean;
  almacenamiento: {
    usadoBytes?: number;
    cuotaBytes?: number;
    usadoLegible: string;
    cuotaLegible: string;
    persistente: boolean | null;
    /**
     * Qué pasó al pedir la persistencia. Distingue "el navegador dijo que no" de "todavía no se
     * pidió", que ante un reporte de campo llevan a diagnósticos opuestos.
     */
    gestionPersistencia: 'sin-api' | 'sin-pedir' | 'concedida' | 'denegada';
  };
  caches: string[];
  navegador: string;
}
