// src/app/features/config/farm-management/company-test.component.ts
import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, takeUntil } from 'rxjs';
import { CompanyService } from '../../../core/services/company/company.service';
import { ActiveCompanyService } from '../../../core/auth/active-company.service';

@Component({
  selector: 'app-company-test',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-4 border rounded-lg bg-gray-50">
      <h3 class="text-lg font-semibold mb-4">🧪 Prueba de Integración API Company</h3>
      
      <div class="space-y-4">
        <!-- Estado de empresa activa -->
        <div class="p-3 bg-blue-50 rounded">
          <h4 class="font-medium text-blue-800">Empresa Activa:</h4>
          <p class="text-blue-600">{{ activeCompany || 'Ninguna seleccionada' }}</p>
        </div>

        <!-- Botón para cargar empresas -->
        <div>
          <button 
            (click)="loadCompanies()" 
            [disabled]="loading"
            class="px-4 py-2 bg-blue-500 text-white rounded hover:bg-blue-600 disabled:opacity-50">
            {{ loading ? 'Cargando...' : 'Cargar Empresas desde API' }}
          </button>
        </div>

        <!-- Resultado de la carga -->
        <div *ngIf="companies.length > 0" class="p-3 bg-green-50 rounded">
          <h4 class="font-medium text-green-800">Empresas Cargadas ({{ companies.length }}):</h4>
          <ul class="mt-2 space-y-1">
            <li *ngFor="let company of companies" class="text-green-600">
              • {{ company.name }} (ID: {{ company.id }})
            </li>
          </ul>
        </div>

        <!-- Error -->
        <div *ngIf="error" class="p-3 bg-red-50 rounded">
          <h4 class="font-medium text-red-800">Error:</h4>
          <p class="text-red-600">{{ error }}</p>
        </div>

        <!-- Debug info -->
        <div class="p-3 bg-gray-100 rounded text-sm">
          <h4 class="font-medium">Debug Info:</h4>
          <p><strong>API URL:</strong> {{ apiUrl }}</p>
          <p><strong>Empresas disponibles:</strong> {{ availableCompanies.length }}</p>
          <p><strong>Empresa activa:</strong> {{ activeCompany }}</p>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .space-y-4 > * + * { margin-top: 1rem; }
    .space-y-1 > * + * { margin-top: 0.25rem; }
  `]
})
export class CompanyTestComponent implements OnInit, OnDestroy {
  private companyService = inject(CompanyService);
  private activeCompanyService = inject(ActiveCompanyService);
  private destroy$ = new Subject<void>();

  companies: any[] = [];
  loading = false;
  error: string | null = null;
  activeCompany: string | null = null;
  availableCompanies: string[] = [];
  
  // URL del API para debug
  apiUrl = 'http://localhost:5002/api/Company';

  ngOnInit(): void {
    // Suscribirse a cambios de empresa activa
    this.activeCompanyService.activeCompany$
      .pipe(takeUntil(this.destroy$))
      .subscribe(company => {
        this.activeCompany = company;
        console.log('CompanyTest - Empresa activa:', company);
      });

    // Suscribirse a empresas disponibles
    this.activeCompanyService.availableCompanies$
      .pipe(takeUntil(this.destroy$))
      .subscribe(companies => {
        this.availableCompanies = companies;
        console.log('CompanyTest - Empresas disponibles:', companies);
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadCompanies(): void {
    this.loading = true;
    this.error = null;
    
    console.log('CompanyTest - Iniciando carga de empresas...');
    console.log('CompanyTest - Empresa activa actual:', this.activeCompany);
    
    this.companyService.getAll()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.companies = data;
          this.loading = false;
          console.log('CompanyTest - Empresas cargadas exitosamente:', data);
        },
        error: (err) => {
          this.error = err.message || 'Error desconocido';
          this.loading = false;
          console.error('CompanyTest - Error cargando empresas:', err);
        }
      });
  }
}




