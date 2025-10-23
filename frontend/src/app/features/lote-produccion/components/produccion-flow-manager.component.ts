// src/app/features/lote-produccion/components/produccion-flow-manager.component.ts
import { Component, Input, Output, EventEmitter, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LoteProduccionService } from '../services/lote-produccion.service';
import { ProduccionLoteService } from '../services/produccion-lote.service';

export interface ProduccionFlowState {
  loteId: string;
  hasProduccionLoteConfig: boolean;
  isLoading: boolean;
  error?: string;
}

@Component({
  selector: 'app-produccion-flow-manager',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="produccion-flow-manager">
      <!-- Estado de carga -->
      <div *ngIf="state.isLoading" class="loading-state">
        <div class="spinner"></div>
        <p>Verificando configuración del lote...</p>
      </div>

      <!-- Error -->
      <div *ngIf="state.error" class="error-state">
        <div class="error-icon">⚠️</div>
        <p>{{ state.error }}</p>
        <button (click)="retryCheck()" class="retry-btn">Reintentar</button>
      </div>

      <!-- Paso 1: Configurar ProduccionLote -->
      <div *ngIf="!state.isLoading && !state.error && !state.hasProduccionLoteConfig" class="step-1">
        <div class="step-header">
          <h3>Paso 1: Configuración Inicial del Lote</h3>
          <p>Este lote necesita configuración inicial antes de registrar producción diaria.</p>
        </div>
        <div class="step-content">
          <button (click)="openProduccionLoteModal()" class="btn-primary">
            Configurar Lote de Producción
          </button>
        </div>
      </div>

      <!-- Paso 2: Registrar ProduccionDiaria -->
      <div *ngIf="!state.isLoading && !state.error && state.hasProduccionLoteConfig" class="step-2">
        <div class="step-header">
          <h3>Paso 2: Registro Diario de Producción</h3>
          <p>El lote está configurado. Puede registrar la producción diaria.</p>
        </div>
        <div class="step-content">
          <button (click)="openProduccionDiariaModal()" class="btn-primary">
            Registrar Producción Diaria
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .produccion-flow-manager {
      padding: 20px;
      border: 1px solid #e0e0e0;
      border-radius: 8px;
      background: #f9f9f9;
    }

    .loading-state {
      text-align: center;
      padding: 40px;
    }

    .spinner {
      width: 40px;
      height: 40px;
      border: 4px solid #f3f3f3;
      border-top: 4px solid #3498db;
      border-radius: 50%;
      animation: spin 1s linear infinite;
      margin: 0 auto 20px;
    }

    @keyframes spin {
      0% { transform: rotate(0deg); }
      100% { transform: rotate(360deg); }
    }

    .error-state {
      text-align: center;
      padding: 40px;
      color: #e74c3c;
    }

    .error-icon {
      font-size: 48px;
      margin-bottom: 20px;
    }

    .retry-btn {
      background: #e74c3c;
      color: white;
      border: none;
      padding: 10px 20px;
      border-radius: 4px;
      cursor: pointer;
      margin-top: 20px;
    }

    .retry-btn:hover {
      background: #c0392b;
    }

    .step-header {
      margin-bottom: 20px;
    }

    .step-header h3 {
      color: #2c3e50;
      margin-bottom: 10px;
    }

    .step-header p {
      color: #7f8c8d;
      margin: 0;
    }

    .step-content {
      text-align: center;
    }

    .btn-primary {
      background: #3498db;
      color: white;
      border: none;
      padding: 12px 24px;
      border-radius: 4px;
      cursor: pointer;
      font-size: 16px;
    }

    .btn-primary:hover {
      background: #2980b9;
    }

    .step-1 {
      border-left: 4px solid #f39c12;
      padding-left: 20px;
    }

    .step-2 {
      border-left: 4px solid #27ae60;
      padding-left: 20px;
    }
  `]
})
export class ProduccionFlowManagerComponent implements OnInit {
  @Input() loteId: string = '';
  @Output() openProduccionLote = new EventEmitter<void>();
  @Output() openProduccionDiaria = new EventEmitter<void>();

  private loteProduccionService = inject(LoteProduccionService);
  private produccionLoteService = inject(ProduccionLoteService);

  state: ProduccionFlowState = {
    loteId: '',
    hasProduccionLoteConfig: false,
    isLoading: true
  };

  ngOnInit() {
    if (this.loteId) {
      this.checkProduccionLoteConfig();
    }
  }

  ngOnChanges() {
    if (this.loteId && this.loteId !== this.state.loteId) {
      this.checkProduccionLoteConfig();
    }
  }

  checkProduccionLoteConfig() {
    this.state.isLoading = true;
    this.state.error = undefined;
    this.state.loteId = this.loteId;

    this.loteProduccionService.checkProduccionLoteConfig(this.loteId).subscribe({
      next: (response) => {
        this.state.hasProduccionLoteConfig = response.hasProduccionLoteConfig;
        this.state.isLoading = false;
      },
      error: (error) => {
        this.state.error = error.message || 'Error al verificar configuración del lote';
        this.state.isLoading = false;
      }
    });
  }

  retryCheck() {
    this.checkProduccionLoteConfig();
  }

  openProduccionLoteModal() {
    this.openProduccionLote.emit();
  }

  openProduccionDiariaModal() {
    this.openProduccionDiaria.emit();
  }
}



