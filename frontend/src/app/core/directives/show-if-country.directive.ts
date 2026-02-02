// src/app/core/directives/show-if-country.directive.ts
import { Directive, Input, TemplateRef, ViewContainerRef, OnInit, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { CountryFilterService } from '../services/country/country-filter.service';
import { TokenStorageService } from '../auth/token-storage.service';

/**
 * Directiva estructural que muestra el elemento solo si el usuario es del país especificado.
 * 
 * Uso por ID:
 * ```html
 * <div *appShowIfCountry="2">
 *   Este contenido solo se muestra para el país con ID 2
 * </div>
 * ```
 * 
 * Uso por nombre:
 * ```html
 * <div *appShowIfCountry="'Ecuador'">
 *   Este contenido solo se muestra para Ecuador
 * </div>
 * ```
 * 
 * También se puede usar con else:
 * ```html
 * <div *appShowIfCountry="2; else otherCountry">
 *   Contenido para país específico
 * </div>
 * <ng-template #otherCountry>
 *   Contenido para otros países
 * </ng-template>
 * ```
 */
@Directive({
  selector: '[appShowIfCountry]',
  standalone: true
})
export class ShowIfCountryDirective implements OnInit, OnDestroy {
  @Input() appShowIfCountry!: number | string;
  private hasView = false;
  private subscription?: Subscription;

  constructor(
    private templateRef: TemplateRef<any>,
    private viewContainer: ViewContainerRef,
    private countryFilter: CountryFilterService,
    private storage: TokenStorageService
  ) {}

  ngOnInit(): void {
    // Verificar estado inicial
    this.updateView();

    // Suscribirse a cambios en la sesión para actualizar la vista
    this.subscription = this.storage.session$.subscribe(() => {
      this.updateView();
    });
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
  }

  private updateView(): void {
    if (this.appShowIfCountry === undefined || this.appShowIfCountry === null) {
      if (this.hasView) {
        this.viewContainer.clear();
        this.hasView = false;
      }
      return;
    }

    let isCountry = false;

    // Verificar por ID (número) o por nombre (string)
    if (typeof this.appShowIfCountry === 'number') {
      isCountry = this.countryFilter.isCountry(this.appShowIfCountry);
    } else {
      isCountry = this.countryFilter.isCountryByName(this.appShowIfCountry);
    }

    if (isCountry && !this.hasView) {
      this.viewContainer.createEmbeddedView(this.templateRef);
      this.hasView = true;
    } else if (!isCountry && this.hasView) {
      this.viewContainer.clear();
      this.hasView = false;
    }
  }
}
