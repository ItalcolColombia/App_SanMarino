// src/app/shared/services/aviso-validacion.service.ts
import { Injectable, ApplicationRef, EnvironmentInjector, createComponent, ComponentRef } from '@angular/core';
import {
  ConfirmationModalComponent,
  ConfirmationModalData,
} from '../components/confirmation-modal/confirmation-modal.component';

/** Motivo puntual de un rechazo. `campo` es opcional: hay motivos que no son de un campo. */
export interface MotivoRechazo {
  campo?: string;
  detalle: string;
}

/**
 * Avisos BLOQUEANTES de los seguimientos diarios: los que el usuario **tiene que leer** antes de
 * seguir.
 *
 * <p>Nace de un reporte concreto: «hay momentos en que no muestra el motivo». Al guardar, un 400 del
 * backend —fecha repetida, alimento faltante, lote cerrado— se mostraba en un toast que se va solo, y
 * en varios flujos ni siquiera eso: el modal se cerraba como si hubiera guardado. Un toast es para
 * avisar; cuando la acción **no ocurrió** y hay algo que corregir, va un modal que hay que cerrar a
 * mano.</p>
 *
 * <p>Reusa `ConfirmationModalComponent` —la primitiva del sistema de diseño— en modo sin cancelar,
 * montándolo dinámicamente igual que `ConfirmDialogService`. No hay componente nuevo a propósito: el
 * look y el comportamiento de cierre ya están resueltos ahí.</p>
 *
 * Uso:
 *   await this.aviso.motivos('No se pudo guardar el seguimiento', [
 *     { campo: 'Alimento', detalle: 'Indicá el tipo y la cantidad en el campo Mixto.' }
 *   ]);
 */
@Injectable({ providedIn: 'root' })
export class AvisoValidacionService {
  constructor(private appRef: ApplicationRef, private injector: EnvironmentInjector) {}

  /** Modal de un solo mensaje. Resuelve cuando el usuario lo cierra. */
  mensaje(
    title: string,
    message: string,
    type: ConfirmationModalData['type'] = 'error',
    confirmText = 'Entendido'
  ): Promise<void> {
    return this.abrir({ title, message, type, confirmText, showCancel: false });
  }

  /**
   * Modal con la lista de motivos, uno por línea. Se muestra preformateado para que la lista se lea
   * como lista y no como un párrafo corrido.
   */
  motivos(title: string, motivos: readonly MotivoRechazo[], type: ConfirmationModalData['type'] = 'error'): Promise<void> {
    const cuerpo = (motivos ?? [])
      .map(m => (m.campo ? `• ${m.campo}: ${m.detalle}` : `• ${m.detalle}`))
      .join('\n');

    return this.abrir({
      title,
      message: cuerpo || 'No se pudo completar la acción.',
      type,
      confirmText: 'Entendido',
      showCancel: false,
      preformatted: true,
    });
  }

  /**
   * Modal para un error HTTP. Muestra **el mensaje del backend**, no un genérico: las reglas del
   * servidor (fecha duplicada, lote cerrado, gate de huevos, plazo de validación vencido) ya vienen
   * redactadas para el usuario y reemplazarlas por «Ocurrió un error» es justamente lo que dejaba al
   * operario sin saber qué corregir.
   */
  error(err: unknown, fallback = 'No se pudo completar la acción.', title = 'No se pudo guardar'): Promise<void> {
    return this.mensaje(title, this.mensajeDeError(err, fallback), 'error');
  }

  /**
   * Alerta roja al entrar al lote cuando hay registros sin validar. Es un aviso, no un rechazo, así
   * que no se mezcla con `error`.
   */
  alertaPendientes(mensaje: string, title = '⚠ Registros pendientes de validar'): Promise<void> {
    return this.mensaje(title, mensaje, 'error', 'Entendido');
  }

  /** Extrae el mensaje del backend (`{ message }`, `{ error }` o string plano) con fallback legible. */
  mensajeDeError(err: unknown, fallback: string): string {
    const e = err as { error?: { message?: string; error?: string } | string; message?: string };
    if (typeof e?.error === 'string' && e.error.trim()) return e.error;
    const msg =
      (e?.error as { message?: string; error?: string } | undefined)?.message ??
      (e?.error as { message?: string; error?: string } | undefined)?.error;
    if (msg && String(msg).trim()) return String(msg);
    if (e?.message && String(e.message).trim()) return String(e.message);
    return fallback;
  }

  /** Monta el modal, espera el cierre y limpia. Mismo patrón que ConfirmDialogService. */
  private abrir(data: ConfirmationModalData): Promise<void> {
    return new Promise<void>(resolve => {
      const host = document.createElement('div');
      document.body.appendChild(host);

      const ref: ComponentRef<ConfirmationModalComponent> = createComponent(ConfirmationModalComponent, {
        environmentInjector: this.injector,
        hostElement: host,
      });

      ref.instance.data = data;
      ref.instance.isOpen = true;
      this.appRef.attachView(ref.hostView);

      let settled = false;
      const done = (): void => {
        if (settled) return;
        settled = true;
        this.appRef.detachView(ref.hostView);
        ref.destroy();
        host.remove();
        resolve();
      };

      // Sin botón cancelar: confirmar, la X y el backdrop son todos «cerrar».
      ref.instance.confirmed.subscribe(done);
      ref.instance.cancelled.subscribe(done);
      ref.instance.closed.subscribe(done);
    });
  }
}
