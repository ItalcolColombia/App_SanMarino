import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FontAwesomeModule, FaIconLibrary } from '@fortawesome/angular-fontawesome';
import { faHome } from '@fortawesome/free-solid-svg-icons';
import { AuthService } from '../../core/auth/auth.service';
import { TokenStorageService } from '../../core/auth/token-storage.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterModule, FontAwesomeModule],
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss']
})
export class HomeComponent implements OnInit {
  private auth = inject(AuthService);
  private storage = inject(TokenStorageService);

  readonly appName = environment.appName;
  readonly appTagline = environment.appTagline;

  loadingMenu = true;
  menuError: string | null = null;

  constructor(library: FaIconLibrary) {
    library.addIcons(faHome);
  }

  ngOnInit(): void {
    // Cargar el menú después del login
    this.loadMenu();
  }

  loadMenu(): void {
    const session = this.storage.get();

    // Si ya hay menú cargado, no volver a cargar
    if (session?.menu && session.menu.length > 0) {
      console.log('✅ Menú ya está cargado en la sesión');
      this.loadingMenu = false;
      return;
    }

    console.log('📋 Cargando menú desde el backend...');
    this.loadingMenu = true;
    this.menuError = null;

    // Obtener companyId de la sesión si está disponible
    const activeCompanyId = session?.activeCompany
      ? this.getCompanyIdFromName(session.activeCompany, session.companies ?? [])
      : undefined;

    this.auth.loadMenu(activeCompanyId).subscribe({
      next: (menuData) => {
        console.log('✅ Menú cargado exitosamente', {
          menuItems: menuData.menu.length,
          menusByRole: menuData.menusByRole.length
        });
        this.loadingMenu = false;
      },
      error: (error) => {
        console.error('❌ Error al cargar el menú:', error);
        this.menuError = 'Error al cargar el menú. Por favor, recarga la página.';
        this.loadingMenu = false;
      }
    });
  }

  private getCompanyIdFromName(companyName: string, companies: string[]): number | undefined {
    // Esta función debería obtener el ID de la compañía desde su nombre
    // Por ahora, retornamos undefined ya que el backend acepta companyId como parámetro opcional
    // TODO: Implementar lógica para obtener companyId desde el nombre de la compañía si es necesario
    return undefined;
  }
}
