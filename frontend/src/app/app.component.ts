// src/app/app.component.ts
import { Component, inject, OnInit, OnDestroy, ChangeDetectionStrategy } from '@angular/core';

import { RouterOutlet, Router } from '@angular/router';
import { PwaActualizacionService } from './core/pwa/pwa-actualizacion.service';
import { SessionTimeoutService } from './core/auth/session-timeout.service';
import { SidebarComponent } from './shared/components/sidebar/sidebar.component';
import { PwaBarraEstadoComponent } from './shared/components/pwa-barra-estado/pwa-barra-estado.component';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faBars } from '@fortawesome/free-solid-svg-icons';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent, PwaBarraEstadoComponent, FontAwesomeModule],
  templateUrl: './app.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit, OnDestroy {
  router = inject(Router);
  private pwaActualizacion = inject(PwaActualizacionService);
  private sessionTimeout = inject(SessionTimeoutService);

  faBars = faBars;

  /** Menú visible solo en rutas protegidas (oculto en login y password-recovery). */
  get showSidebar(): boolean {
    const u = this.router.url;
    return !u.includes('/login') && !u.includes('/password-recovery');
  }

  /** Sidebar se muestra/oculta; por defecto cerrado para no consumir espacio. */
  sidebarOpen = false;

  toggleSidebar(): void {
    this.sidebarOpen = !this.sidebarOpen;
  }

  /** Abre/cierra el menú desde la barra superior (evita que el clic se pierda). */
  onMenuClick(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.toggleSidebar();
  }

  closeSidebar(): void {
    this.sidebarOpen = false;
  }

  ngOnInit(): void {
    // Vigila si se publicó una versión nueva del front. A diferencia del
    // VersionCheckService que reemplaza, NO recarga por su cuenta: levanta un banner y
    // el usuario aplica cuando terminó de cargar lo que estaba cargando.
    this.pwaActualizacion.iniciar();

    // Sesión deslizante: auto-logout por inactividad (5 min) y por pérdida de conexión.
    // Se arranca/detiene solo según haya sesión activa en storage.
    this.sessionTimeout.init();
  }

  ngOnDestroy(): void {
    this.pwaActualizacion.detener();
  }
}
