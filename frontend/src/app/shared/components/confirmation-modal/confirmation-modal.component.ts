import { Component, Input, Output, EventEmitter, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { 
  faCheckCircle, faExclamationTriangle, faTimesCircle, 
  faInfoCircle, faTimes, faCheck
} from '@fortawesome/free-solid-svg-icons';
import { IconProp } from '@fortawesome/fontawesome-svg-core';

export interface ConfirmationModalData {
  title: string;
  message: string;
  icon?: string;
  confirmText?: string;
  cancelText?: string;
  type?: 'success' | 'warning' | 'info' | 'error';
  showCancel?: boolean; // Si es false, solo muestra el botón de confirmar
  /** Renderiza el mensaje preservando espacios/saltos (útil para tablas). */
  preformatted?: boolean;
  /**
   * Campo de texto opcional. Si viene, el modal pide un dato además de confirmar — es el
   * reemplazo del `window.prompt()` nativo. Sin él, el modal se comporta EXACTAMENTE como antes.
   */
  input?: {
    label: string;
    /** Valor inicial del campo. */
    value?: string;
    placeholder?: string;
    /** Con `true`, Confirmar queda deshabilitado mientras el campo esté vacío. */
    required?: boolean;
  };
}

@Component({
  selector: 'app-confirmation-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, FontAwesomeModule],
  templateUrl: './confirmation-modal.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./confirmation-modal.component.scss']
})
export class ConfirmationModalComponent {
  @Input() isOpen = false;
  @Input() data: ConfirmationModalData = {
    title: 'Confirmación',
    message: '¿Estás seguro?',
    confirmText: 'Confirmar',
    cancelText: 'Cancelar',
    type: 'info',
    showCancel: true
  };

  /**
   * Texto tipeado en el campo opcional (`data.input`). Lo lee `ConfirmDialogService.askText()`
   * al confirmar; `confirmed` sigue emitiendo `void` para no romper a los llamadores de siempre.
   */
  inputValue = '';

  /** Con `input.required`, Confirmar se bloquea mientras no haya texto. */
  get confirmDeshabilitado(): boolean {
    return !!this.data.input?.required && !this.inputValue.trim();
  }

  @Output() confirmed = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();
  @Output() closed = new EventEmitter<void>();

  // Iconos
  faCheckCircle = faCheckCircle;
  faExclamationTriangle = faExclamationTriangle;
  faTimesCircle = faTimesCircle;
  faInfoCircle = faInfoCircle;
  faTimes = faTimes;
  faCheck = faCheck;

  getIconClass(): IconProp {
    switch (this.data.type) {
      case 'success':
        return faCheckCircle;
      case 'warning':
        return faExclamationTriangle;
      case 'error':
        return faTimesCircle;
      case 'info':
      default:
        return faInfoCircle;
    }
  }

  getModalClass(): string {
    const base = `modal modal--${this.data.type || 'info'}`;
    return this.data.preformatted ? `${base} modal--wide` : base;
  }

  onConfirm(event?: Event): void {
    
    
    
    if (event) {
      event.preventDefault();
      event.stopPropagation();
      
    }
    
    
    
    
    
    
    
    // NO cerrar el modal aquí, dejar que el componente padre lo maneje
    // Esto permite que el evento se procese correctamente antes de cerrar
    try {
      
      this.confirmed.emit();
      
      
      // El componente padre cerrará el modal después de procesar el evento
    } catch (error) {
      console.error('❌ ConfirmationModal: Error al emitir evento:', error);
      console.error('Stack trace:', error);
      // Si hay error, cerrar el modal
      this.close();
    }
    
    
  }

  onCancel(): void {
    this.cancelled.emit();
    this.close();
  }

  onBackdropClick(event: Event): void {
    if (event.target === event.currentTarget) {
      this.close();
    }
  }

  close(): void {
    this.isOpen = false;
    this.closed.emit();
  }
}
