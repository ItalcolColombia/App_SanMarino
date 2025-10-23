import { Component, Input, Output, EventEmitter, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';

export interface ConfirmationModalData {
  title: string;
  message: string;
  icon?: string;
  confirmText?: string;
  cancelText?: string;
  type?: 'success' | 'warning' | 'info' | 'error';
}

@Component({
  selector: 'app-confirmation-modal',
  standalone: true,
  imports: [CommonModule, FontAwesomeModule],
  templateUrl: './confirmation-modal.component.html',
  styleUrls: ['./confirmation-modal.component.scss']
})
export class ConfirmationModalComponent {
  @Input() isOpen = false;
  @Input() data: ConfirmationModalData = {
    title: 'Confirmación',
    message: '¿Estás seguro?',
    confirmText: 'Confirmar',
    cancelText: 'Cancelar',
    type: 'info'
  };

  @Output() confirmed = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();
  @Output() closed = new EventEmitter<void>();

  getIconClass(): string {
    switch (this.data.type) {
      case 'success':
        return 'fas fa-check-circle';
      case 'warning':
        return 'fas fa-exclamation-triangle';
      case 'error':
        return 'fas fa-times-circle';
      case 'info':
      default:
        return 'fas fa-info-circle';
    }
  }

  getModalClass(): string {
    return `modal modal--${this.data.type || 'info'}`;
  }

  onConfirm(): void {
    this.confirmed.emit();
    this.close();
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
