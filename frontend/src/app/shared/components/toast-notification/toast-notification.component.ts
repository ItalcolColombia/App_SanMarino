// src/app/shared/components/toast-notification/toast-notification.component.ts
import { Component, Input, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { 
  faCheckCircle, faExclamationTriangle, faTimesCircle, 
  faInfoCircle, faTimes, faXmark
} from '@fortawesome/free-solid-svg-icons';

export type ToastType = 'success' | 'error' | 'warning' | 'info';

export interface ToastConfig {
  message: string;
  type?: ToastType;
  duration?: number; // en milisegundos, 0 = no auto-close
  title?: string;
}

@Component({
  selector: 'app-toast-notification',
  standalone: true,
  imports: [CommonModule, FontAwesomeModule],
  templateUrl: './toast-notification.component.html',
  styleUrls: ['./toast-notification.component.scss']
})
export class ToastNotificationComponent implements OnInit, OnDestroy {
  @Input() config!: ToastConfig;

  // Iconos
  faCheckCircle = faCheckCircle;
  faExclamationTriangle = faExclamationTriangle;
  faTimesCircle = faTimesCircle;
  faInfoCircle = faInfoCircle;
  faTimes = faTimes;
  faXmark = faXmark;

  visible = false;
  private timeoutId?: number;

  ngOnInit(): void {
    // Animación de entrada
    setTimeout(() => {
      this.visible = true;
    }, 10);

    // Auto-cerrar si tiene duración
    if (this.config.duration && this.config.duration > 0) {
      this.timeoutId = window.setTimeout(() => {
        this.close();
      }, this.config.duration);
    }
  }

  ngOnDestroy(): void {
    if (this.timeoutId) {
      clearTimeout(this.timeoutId);
    }
  }

  get type(): ToastType {
    return this.config.type || 'info';
  }

  get icon() {
    switch (this.type) {
      case 'success':
        return this.faCheckCircle;
      case 'error':
        return this.faTimesCircle;
      case 'warning':
        return this.faExclamationTriangle;
      default:
        return this.faInfoCircle;
    }
  }

  get title(): string {
    return this.config.title || this.getDefaultTitle();
  }

  getDefaultTitle(): string {
    switch (this.type) {
      case 'success':
        return 'Éxito';
      case 'error':
        return 'Error';
      case 'warning':
        return 'Advertencia';
      default:
        return 'Información';
    }
  }

  close(): void {
    this.visible = false;
    // Esperar a que termine la animación antes de emitir el evento
    setTimeout(() => {
      // El componente padre manejará la eliminación
    }, 300);
  }
}

