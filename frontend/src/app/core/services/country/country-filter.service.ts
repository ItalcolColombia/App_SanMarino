// src/app/core/services/country/country-filter.service.ts
import { Injectable, inject } from '@angular/core';
import { TokenStorageService } from '../../auth/token-storage.service';

/**
 * Servicio para verificar el país del usuario y filtrar funcionalidades
 * según el país activo. Útil para mostrar/ocultar campos específicos de países.
 */
@Injectable({
  providedIn: 'root'
})
export class CountryFilterService {
  private storage = inject(TokenStorageService);

  // ID y nombre de Ecuador (configurables si cambian)
  private readonly ECUADOR_ID = 2;
  private readonly ECUADOR_NAME = 'Ecuador';
  
  // ID y nombre de Panamá (configurables si cambian)
  private readonly PANAMA_ID = 3; // Ajustar según el ID real de Panamá en la BD
  private readonly PANAMA_NAME = 'Panamá';

  /**
   * Verifica si el usuario actual es de Ecuador
   * @returns true si el usuario es de Ecuador, false en caso contrario
   */
  isEcuador(): boolean {
    const session = this.storage.get();
    
    if (!session) {
      return false;
    }

    // Verificar por ID
    if (session.activePaisId === this.ECUADOR_ID) {
      return true;
    }

    // Verificar por nombre (case-insensitive)
    if (session.activePaisNombre && 
        session.activePaisNombre.toLowerCase() === this.ECUADOR_NAME.toLowerCase()) {
      return true;
    }

    return false;
  }

  /**
   * Verifica si el usuario es de un país específico por ID
   * @param paisId ID del país a verificar
   * @returns true si el usuario es del país especificado
   */
  isCountry(paisId: number): boolean {
    const session = this.storage.get();
    return session?.activePaisId === paisId;
  }

  /**
   * Verifica si el usuario es de un país específico por nombre
   * @param paisNombre Nombre del país a verificar (case-insensitive)
   * @returns true si el usuario es del país especificado
   */
  isCountryByName(paisNombre: string): boolean {
    const session = this.storage.get();
    if (!session?.activePaisNombre) {
      return false;
    }
    return session.activePaisNombre.toLowerCase() === paisNombre.toLowerCase();
  }

  /**
   * Obtiene el ID del país activo
   * @returns ID del país activo o undefined
   */
  getActiveCountryId(): number | undefined {
    const session = this.storage.get();
    return session?.activePaisId;
  }

  /**
   * Obtiene el nombre del país activo
   * @returns Nombre del país activo o undefined
   */
  getActiveCountryName(): string | undefined {
    const session = this.storage.get();
    return session?.activePaisNombre;
  }

  /**
   * Verifica si hay un país activo configurado
   * @returns true si hay país activo, false en caso contrario
   */
  hasActiveCountry(): boolean {
    const session = this.storage.get();
    return !!(session?.activePaisId || session?.activePaisNombre);
  }

  /**
   * Verifica si el usuario es de Panamá
   * @returns true si el usuario es de Panamá, false en caso contrario
   */
  isPanama(): boolean {
    const session = this.storage.get();
    
    if (!session) {
      return false;
    }

    // Verificar por ID
    if (session.activePaisId === this.PANAMA_ID) {
      return true;
    }

    // Verificar por nombre (case-insensitive)
    if (session.activePaisNombre && 
        session.activePaisNombre.toLowerCase() === this.PANAMA_NAME.toLowerCase()) {
      return true;
    }

    return false;
  }

  /**
   * Verifica si el usuario es de Ecuador o Panamá
   * @returns true si el usuario es de Ecuador o Panamá
   */
  isEcuadorOrPanama(): boolean {
    return this.isEcuador() || this.isPanama();
  }
}
