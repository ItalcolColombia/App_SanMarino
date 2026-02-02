// src/app/core/directives/show-if-ecuador.directive.ts
import { Directive, Input, TemplateRef, ViewContainerRef, OnInit, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { CountryFilterService } from '../services/country/country-filter.service';
import { TokenStorageService } from '../auth/token-storage.service';

/**
 * Directiva estructural que muestra el elemento solo si el usuario es de Ecuador.
 * 
 * Uso:
 * ```html
 * <div *appShowIfEcuador>
 *   Este contenido solo se muestra para usuarios de Ecuador
 * </div>
 * ```
 * 
 * También se puede usar con else:
 * ```html
 * <div *appShowIfEcuador; else notEcuador>
 *   Contenido para Ecuador
 * </div>
 * <ng-template #notEcuador>
 *   Contenido para otros países
 * </ng-template>
 * ```
 */
@Directive({
  selector: '[appShowIfEcuador]',
  standalone: true
})
export class ShowIfEcuadorDirective implements OnInit, OnDestroy {
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
    const isEcuador = this.countryFilter.isEcuador();

    if (isEcuador && !this.hasView) {
      this.viewContainer.createEmbeddedView(this.templateRef);
      this.hasView = true;
    } else if (!isEcuador && this.hasView) {
      this.viewContainer.clear();
      this.hasView = false;
    }
  }
}
