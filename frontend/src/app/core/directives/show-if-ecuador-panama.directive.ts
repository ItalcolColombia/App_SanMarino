// src/app/core/directives/show-if-ecuador-panama.directive.ts
import { Directive, TemplateRef, ViewContainerRef, OnInit, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { CountryFilterService } from '../services/country/country-filter.service';
import { TokenStorageService } from '../auth/token-storage.service';

/**
 * Directiva estructural que muestra el elemento solo si el usuario es de Ecuador o Panamá.
 * 
 * Uso:
 * ```html
 * <div *appShowIfEcuadorPanama>
 *   Este contenido solo se muestra para usuarios de Ecuador o Panamá
 * </div>
 * ```
 */
@Directive({
  selector: '[appShowIfEcuadorPanama]',
  standalone: true
})
export class ShowIfEcuadorPanamaDirective implements OnInit, OnDestroy {
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
    const isEcuadorOrPanama = this.countryFilter.isEcuadorOrPanama();

    if (isEcuadorOrPanama && !this.hasView) {
      this.viewContainer.createEmbeddedView(this.templateRef);
      this.hasView = true;
    } else if (!isEcuadorOrPanama && this.hasView) {
      this.viewContainer.clear();
      this.hasView = false;
    }
  }
}
