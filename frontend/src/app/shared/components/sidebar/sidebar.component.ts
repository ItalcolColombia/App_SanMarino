import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { FontAwesomeModule, FaIconLibrary } from '@fortawesome/angular-fontawesome';
import {
  faTachometerAlt, faClipboardList, faCalendarDay, faChartBar, faHeartbeat,
  faCog, faUsers, faChevronDown, faSignOutAlt, faList, faBuilding,
  faGlobe, faMapMarkerAlt, faCity, faBoxesAlt, faWarehouse
} from '@fortawesome/free-solid-svg-icons';
import { map } from 'rxjs/operators';
import { Observable } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { MenuService, UiMenuItem } from '../../services/menu.service';
import { take } from 'rxjs/operators';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule, FontAwesomeModule],
  templateUrl: './sidebar.component.html',
  styleUrls: ['./sidebar.component.scss']
})
export class SidebarComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly menuSvc = inject(MenuService);
  private readonly router = inject(Router);

  // Íconos sueltos (para botones)
  faChevronDown = faChevronDown;
  faSignOutAlt  = faSignOutAlt;

  // Stream del árbol de menú listo para pintar
  menu$: Observable<UiMenuItem[]> = this.menuSvc.menu$;

  /** Banner Bienvenida */
  userBanner$ = this.auth.session$.pipe(
    map(s => ({
      fullName: s?.user?.fullName ?? s?.user?.username ?? 'Usuario',
      company:  s?.activeCompany ?? (s?.companies?.[0] ?? '—'),
      initials: (s?.user?.fullName ?? s?.user?.username ?? 'U')
        .trim()
        .split(/\s+/)
        .map(w => w[0])
        .join('')
        .slice(0, 2)
        .toUpperCase()
    }))
  );

  constructor(library: FaIconLibrary) {
    library.addIcons(
      faTachometerAlt, faClipboardList, faCalendarDay, faChartBar, faHeartbeat,
      faCog, faUsers, faChevronDown, faSignOutAlt, faList, faBuilding,
      faGlobe, faMapMarkerAlt, faCity, faWarehouse, faBoxesAlt
    );
  }

  ngOnInit(): void {
    // Asegura que haya menú cargado (lee storage y, si no hay, va a la API)
     this.menuSvc.ensureLoaded().pipe(take(1)).subscribe(); // dispara carga y completa
  }

  toggle(item: UiMenuItem) {
    item.expanded = !item.expanded;
  }

  logout() {
    // Vacía el menú en memoria
    this.menuSvc.reset();
    // Limpia todo lo temporal del storage y devuelve al login
    this.auth.logout({ hard: true });
    this.router.navigate(['/login'], { replaceUrl: true });
  }
}
