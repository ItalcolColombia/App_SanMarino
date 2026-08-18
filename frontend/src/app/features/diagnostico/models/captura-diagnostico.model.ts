import type { OperacionPendiente } from '../../../shared/offline/models/outbox.model';

/**
 * Una fila de «Capturas sin enviar», ya decidida: si es de la sesión que está mirando la pantalla o
 * de otra.
 *
 * Vive en `models/` para que la función pura de `funciones/` se tipe sin importar el componente
 * (convención de CLAUDE.md).
 */
export interface CapturaDiagnostico {
  operacion: OperacionPendiente;

  /**
   * ¿La capturó la sesión activa? Solo entonces se muestra el payload y se ofrece copiarla o
   * descartarla. `false` también cuando **no hay sesión**, que es como se abre esta pantalla en un
   * rescate.
   */
  propia: boolean;
}
