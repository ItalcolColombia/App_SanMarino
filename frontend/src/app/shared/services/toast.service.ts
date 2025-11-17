// src/app/shared/services/toast.service.ts
import { Injectable, ComponentRef, ApplicationRef, Injector, createComponent, EnvironmentInjector } from '@angular/core';
import { ToastNotificationComponent, ToastConfig } from '../components/toast-notification/toast-notification.component';

@Injectable({
  providedIn: 'root'
})
export class ToastService {
  private toasts: ComponentRef<ToastNotificationComponent>[] = [];
  private container?: HTMLDivElement;

  constructor(
    private appRef: ApplicationRef,
    private injector: EnvironmentInjector
  ) {
    this.createContainer();
  }

  private createContainer(): void {
    // Crear contenedor en el body
    this.container = document.createElement('div');
    this.container.className = 'toast-wrapper';
    this.container.style.cssText = `
      position: fixed;
      top: 20px;
      right: 20px;
      z-index: 10000;
      display: flex;
      flex-direction: column;
      align-items: flex-end;
      pointer-events: none;
    `;
    document.body.appendChild(this.container);
  }

  show(config: ToastConfig): void {
    if (!this.container) {
      this.createContainer();
    }

    // Crear componente usando createComponent (Angular 17+)
    const componentRef = createComponent(ToastNotificationComponent, {
      environmentInjector: this.injector
    });
    
    componentRef.instance.config = {
      duration: config.duration ?? 5000,
      type: config.type ?? 'info',
      message: config.message,
      title: config.title
    };

    // Agregar al DOM
    if (this.container) {
      this.container.appendChild(componentRef.location.nativeElement);
      this.appRef.attachView(componentRef.hostView);
      this.toasts.push(componentRef);

      // Auto-remover después de la animación
      if (config.duration && config.duration > 0) {
        setTimeout(() => {
          this.remove(componentRef);
        }, config.duration + 300);
      }
    }
  }

  success(message: string, title?: string, duration?: number): void {
    this.show({ message, type: 'success', title, duration });
  }

  error(message: string, title?: string, duration?: number): void {
    this.show({ message, type: 'error', title, duration: duration ?? 7000 });
  }

  warning(message: string, title?: string, duration?: number): void {
    this.show({ message, type: 'warning', title, duration });
  }

  info(message: string, title?: string, duration?: number): void {
    this.show({ message, type: 'info', title, duration });
  }

  private remove(componentRef: ComponentRef<ToastNotificationComponent>): void {
    const index = this.toasts.indexOf(componentRef);
    if (index > -1) {
      this.toasts.splice(index, 1);
      componentRef.instance.close();
      setTimeout(() => {
        this.appRef.detachView(componentRef.hostView);
        componentRef.destroy();
      }, 300);
    }
  }

  clear(): void {
    this.toasts.forEach(toast => {
      this.appRef.detachView(toast.hostView);
      toast.destroy();
    });
    this.toasts = [];
  }
}

