// src/app/shared/services/confirm-dialog.service.ts
import { Injectable, ApplicationRef, EnvironmentInjector, createComponent, ComponentRef } from '@angular/core';
import {
  ConfirmationModalComponent,
  ConfirmationModalData,
} from '../components/confirmation-modal/confirmation-modal.component';

/** Resultado interno: si confirmó y, cuando el modal pedía texto, qué se tipeó. */
interface ResultadoDialogo {
  confirmado: boolean;
  texto: string;
}

/**
 * Reemplazo del `window.confirm()` / `window.prompt()` nativos por el modal del sistema de diseño
 * (`ConfirmationModalComponent`), pero con una API `await`-able: monta el modal dinámicamente en el
 * `body` (igual patrón que `ToastService`) y resuelve cuando el usuario decide.
 *
 * Uso en el llamador:
 *   if (!(await this.confirmDialog.ask({ title: 'Eliminar', message: '¿Seguro?' }))) return;
 *
 *   const motivo = await this.confirmDialog.askText({
 *     title: 'Anular', message: '...', input: { label: 'Motivo', value: 'Anulado por usuario' }
 *   });
 *   if (motivo === null) return;   // canceló
 */
@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  constructor(private appRef: ApplicationRef, private injector: EnvironmentInjector) {}

  /** Confirmación sí/no. Resuelve `true` al confirmar, `false` al cancelar / cerrar / click fuera. */
  async ask(data: ConfirmationModalData): Promise<boolean> {
    return (await this.abrir(data)).confirmado;
  }

  /**
   * Variante con campo de texto — el reemplazo de `window.prompt()`.
   *
   * Resuelve con el texto tipeado (ya `trim`-eado) si el usuario confirma, o `null` si cancela o
   * cierra, para que el llamador pueda distinguir «escribió vacío» de «no quiso».
   */
  async askText(
    data: ConfirmationModalData & { input: NonNullable<ConfirmationModalData['input']> }
  ): Promise<string | null> {
    const { confirmado, texto } = await this.abrir(data);
    return confirmado ? texto.trim() : null;
  }

  private abrir(data: ConfirmationModalData): Promise<ResultadoDialogo> {
    return new Promise<ResultadoDialogo>((resolve) => {
      const host = document.createElement('div');
      document.body.appendChild(host);

      const ref: ComponentRef<ConfirmationModalComponent> = createComponent(ConfirmationModalComponent, {
        environmentInjector: this.injector,
        hostElement: host,
      });

      // El llamador provee title/message; defaults sensatos para el resto (spread primero para no duplicar props).
      ref.instance.data = {
        ...data,
        confirmText: data.confirmText ?? 'Confirmar',
        cancelText: data.cancelText ?? 'Cancelar',
        type: data.type ?? 'warning',
        showCancel: data.showCancel ?? true,
      };
      ref.instance.inputValue = data.input?.value ?? '';
      ref.instance.isOpen = true;
      this.appRef.attachView(ref.hostView);

      let settled = false;
      const done = (confirmado: boolean): void => {
        if (settled) return;
        settled = true;
        // El texto se LEE ANTES de destruir el componente: después de `ref.destroy()` la instancia
        // ya no es un lugar del que valga la pena leer estado.
        const texto = ref.instance.inputValue ?? '';
        this.appRef.detachView(ref.hostView);
        ref.destroy();
        host.remove();
        resolve({ confirmado, texto });
      };

      // Confirmar NO cierra el modal por sí mismo (el componente delega el cierre al padre).
      ref.instance.confirmed.subscribe(() => done(true));
      // Cancelar emite `cancelled` (+ luego `closed`); la X/backdrop solo `closed`. El guard evita doble resolución.
      ref.instance.cancelled.subscribe(() => done(false));
      ref.instance.closed.subscribe(() => done(false));
    });
  }
}
