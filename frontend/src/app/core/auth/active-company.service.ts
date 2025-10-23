import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { TokenStorageService } from './token-storage.service';

@Injectable({ providedIn: 'root' })
export class ActiveCompanyService {
  private storage = inject(TokenStorageService);

  // Observable para la empresa activa
  readonly activeCompany$ = this.storage.session$.pipe(
    map(session => session?.activeCompany || null)
  );

  // Observable para todas las empresas disponibles
  readonly availableCompanies$ = this.storage.session$.pipe(
    map(session => session?.companies || [])
  );

  /**
   * Establece la empresa activa
   * @param companyName Nombre de la empresa a activar
   */
  setActiveCompany(companyName: string): void {
    const session = this.storage.get();
    if (!session) {
      console.warn('No hay sesión activa para cambiar empresa');
      return;
    }

    // Verificar que la empresa existe en las empresas disponibles
    if (!session.companies.includes(companyName)) {
      console.warn(`Empresa "${companyName}" no está disponible para este usuario`);
      return;
    }

    this.storage.setActiveCompany(companyName);
    console.log(`Empresa activa cambiada a: ${companyName}`);
  }

  /**
   * Obtiene la empresa activa actual
   */
  getActiveCompany(): string | null {
    return this.storage.get()?.activeCompany || null;
  }

  /**
   * Obtiene todas las empresas disponibles
   */
  getAvailableCompanies(): string[] {
    return this.storage.get()?.companies || [];
  }

  /**
   * Verifica si una empresa está disponible para el usuario
   */
  isCompanyAvailable(companyName: string): boolean {
    return this.getAvailableCompanies().includes(companyName);
  }

  /**
   * Obtiene la primera empresa disponible como fallback
   */
  getDefaultCompany(): string | null {
    const companies = this.getAvailableCompanies();
    return companies.length > 0 ? companies[0] : null;
  }
}