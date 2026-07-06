// src/app/shared/services/confirm-dialog.service.ts
import { Injectable, ApplicationRef, EnvironmentInjector, createComponent, ComponentRef } from '@angular/core';
import {
  ConfirmationModalComponent,
  ConfirmationModalData,
} from '../components/confirmation-modal/confirmation-modal.component';

/**
 * Reemplazo del `window.confirm()` nativo por el modal del sistema de diseño
 * (`ConfirmationModalComponent`), pero con una API `await`-able: `ask()` monta el modal
 * dinámicamente en el `body` (igual patrón que `ToastService`) y resuelve `true` si el
 * usuario confirma, `false` si cancela / cierra / hace click fuera.
 *
 * Uso en el llamador:
 *   if (!(await this.confirmDialog.ask({ title: 'Eliminar', message: '¿Seguro?' }))) return;
 */
@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  constructor(private appRef: ApplicationRef, private injector: EnvironmentInjector) {}

  ask(data: ConfirmationModalData): Promise<boolean> {
    return new Promise<boolean>((resolve) => {
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
      ref.instance.isOpen = true;
      this.appRef.attachView(ref.hostView);

      let settled = false;
      const done = (result: boolean): void => {
        if (settled) return;
        settled = true;
        this.appRef.detachView(ref.hostView);
        ref.destroy();
        host.remove();
        resolve(result);
      };

      // Confirmar NO cierra el modal por sí mismo (el componente delega el cierre al padre).
      ref.instance.confirmed.subscribe(() => done(true));
      // Cancelar emite `cancelled` (+ luego `closed`); la X/backdrop solo `closed`. El guard evita doble resolución.
      ref.instance.cancelled.subscribe(() => done(false));
      ref.instance.closed.subscribe(() => done(false));
    });
  }
}
