import { Component, OnInit, OnDestroy, inject, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { FontAwesomeModule, FaIconLibrary } from '@fortawesome/angular-fontawesome';
import {
  faTachometerAlt, faClipboardList, faCalendarDay, faChartBar, faHeartbeat,
  faCog, faUsers, faChevronDown, faSignOutAlt, faList, faBuilding,
  faGlobe, faMapMarkerAlt, faCity, faBoxesAlt, faWarehouse, faDollarSign,
  faLayerGroup, faChartLine, faEgg, faHome, faBars, faKey, faUserShield, faScrewdriverWrench,
  faTimes
} from '@fortawesome/free-solid-svg-icons';
import { map, filter, take, takeUntil } from 'rxjs/operators';
import { Observable, Subject } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { MenuService, UiMenuItem } from '../../services/menu.service';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule, FontAwesomeModule],
  templateUrl: './sidebar.component.html',
  styleUrls: ['./sidebar.component.scss']
})
export class SidebarComponent implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly menuSvc = inject(MenuService);
  private readonly router = inject(Router);
  private readonly destroy$ = new Subject<void>();

  /** Sidebar en modo overlay: abierto/cerrado desde app (no consume espacio cuando cerrado). */
  @Input() isOpen = false;
  @Output() close = new EventEmitter<void>();

  faChevronDown = faChevronDown;
  faSignOutAlt  = faSignOutAlt;
  faTimes       = faTimes;

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

  companyLogo$ = this.auth.session$.pipe(
    map(s => s?.activeCompanyLogoDataUrl ?? null)
  );

  constructor(library: FaIconLibrary) {
    library.addIcons(
      faTachometerAlt, faClipboardList, faCalendarDay, faChartBar, faHeartbeat,
      faCog, faUsers, faChevronDown, faSignOutAlt, faList, faBuilding,
      faGlobe, faMapMarkerAlt, faCity, faWarehouse, faBoxesAlt, faDollarSign,
      faLayerGroup, faChartLine, faEgg, faHome, faBars, faKey, faUserShield, faScrewdriverWrench,
      faTimes
    );
  }

  onClose(): void {
    this.close.emit();
  }

  ngOnInit(): void {
    this.menuSvc.ensureLoaded().pipe(take(1)).subscribe();
    this.router.events
      .pipe(
        filter((e): e is NavigationEnd => e instanceof NavigationEnd),
        takeUntil(this.destroy$)
      )
      .subscribe(() => this.close.emit());
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
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
