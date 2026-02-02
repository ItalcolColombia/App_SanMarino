// src/app/core/pipes/is-ecuador.pipe.ts
import { Pipe, PipeTransform } from '@angular/core';
import { CountryFilterService } from '../services/country/country-filter.service';

/**
 * Pipe para verificar si el usuario es de Ecuador.
 * 
 * Uso:
 * ```html
 * <div *ngIf="true | isEcuador">
 *   Contenido solo para Ecuador
 * </div>
 * ```
 * 
 * O en expresiones:
 * ```html
 * <span>{{ (true | isEcuador) ? 'Ecuador' : 'Otro país' }}</span>
 * ```
 */
@Pipe({
  name: 'isEcuador',
  standalone: true,
  pure: false // No es puro porque depende del estado de la sesión
})
export class IsEcuadorPipe implements PipeTransform {
  constructor(private countryFilter: CountryFilterService) {}

  transform(value: any): boolean {
    return this.countryFilter.isEcuador();
  }
}
