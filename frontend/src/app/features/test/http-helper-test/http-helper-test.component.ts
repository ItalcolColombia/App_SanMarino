// src/app/features/test/http-helper-test/http-helper-test.component.ts
import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, takeUntil } from 'rxjs';
import { HttpCompanyHelperService } from '../../../core/services/http-company-helper.service';
import { CompanyService } from '../../../core/services/company/company.service';
import { FarmService } from '../../../core/services/farm/farm.service';
import { UserService } from '../../../core/services/user/user.service';
import { ActiveCompanyService } from '../../../core/auth/active-company.service';

@Component({
  selector: 'app-http-helper-test',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6 bg-gray-50 rounded-lg">
      <h2 class="text-2xl font-bold mb-6">🧪 Prueba de Helper Centralizado</h2>
      
      <!-- Estado actual -->
      <div class="mb-6 p-4 bg-blue-50 rounded">
        <h3 class="font-semibold text-blue-800 mb-2">Estado Actual:</h3>
        <p><strong>Empresa Activa:</strong> {{ activeCompany || 'Ninguna' }}</p>
        <p><strong>Empresa Disponible:</strong> {{ hasActiveCompany() ? 'Sí' : 'No' }}</p>
        <p><strong>Header Generado:</strong> {{ getHeaderPreview() }}</p>
      </div>

      <!-- Botones de prueba -->
      <div class="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
        <button 
          (click)="testCompanyService()" 
          [disabled]="loading"
          class="p-4 bg-green-500 text-white rounded hover:bg-green-600 disabled:opacity-50">
          {{ loading ? 'Cargando...' : 'Probar CompanyService' }}
        </button>
        
        <button 
          (click)="testFarmService()" 
          [disabled]="loading"
          class="p-4 bg-blue-500 text-white rounded hover:bg-blue-600 disabled:opacity-50">
          {{ loading ? 'Cargando...' : 'Probar FarmService' }}
        </button>
        
        <button 
          (click)="testUserService()" 
          [disabled]="loading"
          class="p-4 bg-purple-500 text-white rounded hover:bg-purple-600 disabled:opacity-50">
          {{ loading ? 'Cargando...' : 'Probar UserService' }}
        </button>
      </div>

      <!-- Resultados -->
      <div class="space-y-4">
        <div *ngIf="lastResult" class="p-4 bg-green-50 rounded">
          <h4 class="font-semibold text-green-800 mb-2">Último Resultado:</h4>
          <pre class="text-sm text-green-600 overflow-auto">{{ lastResult | json }}</pre>
        </div>

        <div *ngIf="lastError" class="p-4 bg-red-50 rounded">
          <h4 class="font-semibold text-red-800 mb-2">Último Error:</h4>
          <p class="text-sm text-red-600">{{ lastError }}</p>
        </div>

        <!-- Logs de debug -->
        <div class="p-4 bg-gray-100 rounded">
          <h4 class="font-semibold mb-2">Logs de Debug:</h4>
          <div class="text-sm text-gray-600 max-h-40 overflow-auto">
            <p *ngFor="let log of debugLogs">{{ log }}</p>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .space-y-4 > * + * { margin-top: 1rem; }
    pre { white-space: pre-wrap; word-wrap: break-word; }
  `]
})
export class HttpHelperTestComponent implements OnInit, OnDestroy {
  private companyHelper = inject(HttpCompanyHelperService);
  private companyService = inject(CompanyService);
  private farmService = inject(FarmService);
  private userService = inject(UserService);
  private activeCompanyService = inject(ActiveCompanyService);
  private destroy$ = new Subject<void>();

  activeCompany: string | null = null;
  loading = false;
  lastResult: any = null;
  lastError: string | null = null;
  debugLogs: string[] = [];

  ngOnInit(): void {
    // Suscribirse a cambios de empresa activa
    this.activeCompanyService.activeCompany$
      .pipe(takeUntil(this.destroy$))
      .subscribe(company => {
        this.activeCompany = company;
        this.addLog(`Empresa activa cambiada: ${company || 'Ninguna'}`);
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  getHeaderPreview(): string {
    const header = this.companyHelper.getActiveCompanyHeader();
    return JSON.stringify(header);
  }

  hasActiveCompany(): boolean {
    return this.companyHelper.hasActiveCompany();
  }

  testCompanyService(): void {
    this.loading = true;
    this.lastError = null;
    this.addLog('Iniciando prueba de CompanyService...');

    this.companyService.getAll()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => {
          this.lastResult = result;
          this.loading = false;
          this.addLog(`CompanyService.getAll exitoso: ${result.length} empresas`);
        },
        error: (error) => {
          this.lastError = error.message;
          this.loading = false;
          this.addLog(`CompanyService.getAll error: ${error.message}`);
        }
      });
  }

  testFarmService(): void {
    this.loading = true;
    this.lastError = null;
    this.addLog('Iniciando prueba de FarmService...');

    this.farmService.getAll()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => {
          this.lastResult = result;
          this.loading = false;
          this.addLog(`FarmService.getAll exitoso: ${result.length} granjas`);
        },
        error: (error) => {
          this.lastError = error.message;
          this.loading = false;
          this.addLog(`FarmService.getAll error: ${error.message}`);
        }
      });
  }

  testUserService(): void {
    this.loading = true;
    this.lastError = null;
    this.addLog('Iniciando prueba de UserService...');

    this.userService.getAll()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => {
          this.lastResult = result;
          this.loading = false;
          this.addLog(`UserService.getAll exitoso: ${result.length} usuarios`);
        },
        error: (error) => {
          this.lastError = error.message;
          this.loading = false;
          this.addLog(`UserService.getAll error: ${error.message}`);
        }
      });
  }

  private addLog(message: string): void {
    const timestamp = new Date().toLocaleTimeString();
    this.debugLogs.unshift(`[${timestamp}] ${message}`);
    
    // Mantener solo los últimos 10 logs
    if (this.debugLogs.length > 10) {
      this.debugLogs = this.debugLogs.slice(0, 10);
    }
  }
}
