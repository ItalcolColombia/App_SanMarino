import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, takeUntil } from 'rxjs';
import { CompanyService, Company } from '../../../core/services/company/company.service';
import { CompanySelectorComponent } from '../../../shared/components/company-selector/company-selector.component';

@Component({
  selector: 'app-company-admin-test',
  standalone: true,
  imports: [CommonModule, CompanySelectorComponent],
  template: `
    <div class="container mx-auto p-6 bg-white shadow-lg rounded-lg">
      <h2 class="text-2xl font-bold text-gray-800 mb-6">Prueba de Endpoints de Empresas</h2>
      
      <!-- Selector de empresa -->
      <div class="mb-6">
        <app-company-selector 
          (companyChanged)="onCompanyChanged($event)"
          [showLabel]="true"
          size="md"
          variant="default">
        </app-company-selector>
      </div>

      <!-- Botones de prueba -->
      <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
        <button 
          (click)="testNormalEndpoint()"
          [disabled]="loading"
          class="btn-primary">
          <span *ngIf="loading && loadingType === 'normal'" class="spinner spinner--sm mr-2"></span>
          Probar Endpoint Normal (Filtrado)
        </button>
        
        <button 
          (click)="testAdminEndpoint()"
          [disabled]="loading"
          class="btn-secondary">
          <span *ngIf="loading && loadingType === 'admin'" class="spinner spinner--sm mr-2"></span>
          Probar Endpoint Admin (Sin Filtro)
        </button>
      </div>

      <!-- Resultados -->
      <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
        <!-- Endpoint Normal -->
        <div class="border rounded-lg p-4">
          <h3 class="text-lg font-semibold text-blue-800 mb-3">
            Endpoint Normal (/api/Company)
          </h3>
          <div class="text-sm text-gray-600 mb-2">
            <strong>Empresa Activa:</strong> {{ activeCompany || 'Ninguna' }}
          </div>
          <div class="text-sm text-gray-600 mb-3">
            <strong>Empresas Encontradas:</strong> {{ normalCompanies.length }}
          </div>
          <div class="max-h-64 overflow-y-auto">
            <div *ngIf="normalCompanies.length === 0" class="text-gray-500 italic">
              Sin empresas
            </div>
            <div *ngFor="let company of normalCompanies" class="text-sm p-2 bg-blue-50 rounded mb-1">
              <strong>{{ company.name }}</strong><br>
              <span class="text-gray-600">ID: {{ company.id }} | {{ company.identifier }}</span>
            </div>
          </div>
        </div>

        <!-- Endpoint Admin -->
        <div class="border rounded-lg p-4">
          <h3 class="text-lg font-semibold text-green-800 mb-3">
            Endpoint Admin (/api/Company/admin)
          </h3>
          <div class="text-sm text-gray-600 mb-2">
            <strong>Sin Filtro:</strong> Todas las empresas
          </div>
          <div class="text-sm text-gray-600 mb-3">
            <strong>Empresas Encontradas:</strong> {{ adminCompanies.length }}
          </div>
          <div class="max-h-64 overflow-y-auto">
            <div *ngIf="adminCompanies.length === 0" class="text-gray-500 italic">
              Sin empresas
            </div>
            <div *ngFor="let company of adminCompanies" class="text-sm p-2 bg-green-50 rounded mb-1">
              <strong>{{ company.name }}</strong><br>
              <span class="text-gray-600">ID: {{ company.id }} | {{ company.identifier }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Logs -->
      <div *ngIf="logs.length > 0" class="mt-6">
        <h3 class="text-lg font-semibold text-gray-800 mb-3">Logs de Prueba</h3>
        <div class="bg-gray-100 p-4 rounded-lg max-h-48 overflow-y-auto">
          <div *ngFor="let log of logs" class="text-sm font-mono mb-1">
            <span class="text-gray-500">[{{ log.timestamp }}]</span>
            <span [class]="log.type === 'error' ? 'text-red-600' : 'text-gray-800'">{{ log.message }}</span>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .btn-primary, .btn-secondary {
      @apply px-4 py-2 rounded-md font-medium transition-colors;
    }
    .btn-primary {
      @apply bg-blue-600 text-white hover:bg-blue-700 disabled:bg-gray-400;
    }
    .btn-secondary {
      @apply bg-green-600 text-white hover:bg-green-700 disabled:bg-gray-400;
    }
    .spinner {
      @apply inline-block w-4 h-4 border-2 border-current border-t-transparent rounded-full animate-spin;
    }
    .spinner--sm {
      @apply w-3 h-3;
    }
  `]
})
export class CompanyAdminTestComponent implements OnInit, OnDestroy {
  loading = false;
  loadingType: 'normal' | 'admin' | null = null;
  
  normalCompanies: Company[] = [];
  adminCompanies: Company[] = [];
  activeCompany: string | null = null;
  
  logs: Array<{ timestamp: string; message: string; type: 'info' | 'error' }> = [];
  
  private destroy$ = new Subject<void>();

  constructor(private companyService: CompanyService) {}

  ngOnInit(): void {
    this.addLog('Componente inicializado');
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onCompanyChanged(companyName: string): void {
    this.activeCompany = companyName;
    this.addLog(`Empresa activa cambiada a: ${companyName}`);
  }

  testNormalEndpoint(): void {
    this.loading = true;
    this.loadingType = 'normal';
    this.addLog('Probando endpoint normal (/api/Company)...');
    
    this.companyService.getAll()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (companies) => {
          this.normalCompanies = companies;
          this.loading = false;
          this.loadingType = null;
          this.addLog(`Endpoint normal: ${companies.length} empresas encontradas`);
        },
        error: (error) => {
          this.loading = false;
          this.loadingType = null;
          this.addLog(`Error en endpoint normal: ${error.message}`, 'error');
        }
      });
  }

  testAdminEndpoint(): void {
    this.loading = true;
    this.loadingType = 'admin';
    this.addLog('Probando endpoint admin (/api/Company/admin)...');
    
    this.companyService.getAllForAdmin()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (companies) => {
          this.adminCompanies = companies;
          this.loading = false;
          this.loadingType = null;
          this.addLog(`Endpoint admin: ${companies.length} empresas encontradas`);
        },
        error: (error) => {
          this.loading = false;
          this.loadingType = null;
          this.addLog(`Error en endpoint admin: ${error.message}`, 'error');
        }
      });
  }

  private addLog(message: string, type: 'info' | 'error' = 'info'): void {
    const timestamp = new Date().toLocaleTimeString();
    this.logs.unshift({ timestamp, message, type });
    
    // Mantener solo los últimos 20 logs
    if (this.logs.length > 20) {
      this.logs = this.logs.slice(0, 20);
    }
  }
}




