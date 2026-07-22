// src/app/app.component.ts
import { Component, inject, OnInit, OnDestroy, ChangeDetectionStrategy } from '@angular/core';

import { RouterOutlet, Router } from '@angular/router';
import { VersionCheckService } from './core/services/version-check.service';
import { SessionTimeoutService } from './core/auth/session-timeout.service';
import { SidebarComponent } from './shared/components/sidebar/sidebar.component';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faBars } from '@fortawesome/free-solid-svg-icons';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent, FontAwesomeModule],
  templateUrl: './app.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit, OnDestroy {
  router = inject(Router);
  private versionCheckService = inject(VersionCheckService);
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
    // Start checking for application updates
    // This will periodically check if a new version has been deployed
    // and force a reload if detected
    this.versionCheckService.startVersionChecking();

    // Sesión deslizante: auto-logout por inactividad (5 min) y por pérdida de conexión.
    // Se arranca/detiene solo según haya sesión activa en storage.
    this.sessionTimeout.init();
  }

  ngOnDestroy(): void {
    // Stop version checking when component is destroyed
    this.versionCheckService.stopVersionChecking();
  }
}
