// src/app/app.component.ts
import { Component, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, Router } from '@angular/router';
import { VersionCheckService } from './core/services/version-check.service';
import { SidebarComponent } from './shared/components/sidebar/sidebar.component';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faBars } from '@fortawesome/free-solid-svg-icons';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, SidebarComponent, FontAwesomeModule],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit, OnDestroy {
  router = inject(Router);
  private versionCheckService = inject(VersionCheckService);

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
  }

  ngOnDestroy(): void {
    // Stop version checking when component is destroyed
    this.versionCheckService.stopVersionChecking();
  }
}
