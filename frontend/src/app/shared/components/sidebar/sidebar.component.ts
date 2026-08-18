import { Component, OnInit, OnDestroy, inject, Input, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { FontAwesomeModule, FaIconLibrary } from '@fortawesome/angular-fontawesome';
import {
  faTachometerAlt, faClipboardList, faCalendarDay, faChartBar, faHeartbeat,
  faCog, faUsers, faChevronDown, faSignOutAlt, faList, faBuilding,
  faGlobe, faMapMarkerAlt, faCity, faBoxesAlt, faWarehouse, faDollarSign,
  faLayerGroup, faChartLine, faEgg, faHome, faBars, faKey, faUserShield, faScrewdriverWrench,
  faTimes, faUser, faCircle
} from '@fortawesome/free-solid-svg-icons';
import { map, filter, take, takeUntil } from 'rxjs/operators';
import { Observable, Subject } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { LlaveroSesionesService } from '../../../core/auth/llavero-sesiones.service';
import { OutboxService } from '../../offline/outbox.service';
import { ConfirmDialogService } from '../../services/confirm-dialog.service';
import { MenuService, UiMenuItem } from '../../services/menu.service';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule, FontAwesomeModule],
  templateUrl: './sidebar.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./sidebar.component.scss']
})
export class SidebarComponent implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly llavero = inject(LlaveroSesionesService);
  private readonly outbox = inject(OutboxService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly menuSvc = inject(MenuService);
  private readonly router = inject(Router);
  private readonly destroy$ = new Subject<void>();

  /** Sidebar en modo overlay: abierto/cerrado desde app (no consume espacio cuando cerrado). */
  @Input() isOpen = false;
  @Output() close = new EventEmitter<void>();

  faChevronDown = faChevronDown;
  faSignOutAlt  = faSignOutAlt;
  faTimes       = faTimes;
  faUser        = faUser;
  faCircle      = faCircle;
  faBuilding    = faBuilding;
  faScrewdriverWrench = faScrewdriverWrench;

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
      faTimes, faUser, faCircle
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

  /** ¿Este equipo puede guardar sesiones? Sin cripto no se ofrece «cambiar de usuario». */
  get hayLlavero(): boolean {
    return this.llavero.disponible();
  }

  /**
   * ¿Hay alguien adentro?
   *
   * El sidebar se muestra por lista de rutas, no por sesión, así que en pantallas públicas como
   * `/diagnostico` aparece **sin sesión**. Las tres salidas del pie son destructivas —una borra el
   * equipo entero— y no pueden quedar al alcance de cualquiera que levante la tablet. Se gatea acá,
   * por el dato, y no por la ruta: una ruta nueva que se olvide de la lista no rompe la regla.
   */
  haySesion$ = this.auth.session$.pipe(map(s => !!s?.accessToken));

  /** Aparcar: la sesión se guarda cifrada y entra otro. No purga nada (R-M6). */
  cambiarDeUsuario(): void {
    this.router.navigate(['/cambiar-usuario']);
  }

  /**
   * Cerrar sesión. Cambia respecto de antes: se purga **solo la partición propia** y se elimina el
   * slot propio; lo de los otros operarios del equipo queda intacto (R-M6, fix F-5).
   *
   * Con capturas sin enviar se avisa antes (R-M5): quedan en la tablet hasta que esa persona vuelva a
   * entrar, y eso hay que poder saberlo **antes** de salir, no después.
   */
  async logout(): Promise<void> {
    if (!(await this.confirmarSiHayCapturas())) {
      return;
    }

    this.menuSvc.reset();
    this.auth.logout({ hard: true });
    this.router.navigate(['/login'], { replaceUrl: true });
  }

  /**
   * Borrar el dispositivo: se van **todos** los slots y **toda** la caché. Es la acción de «este
   * equipo cambia de manos», y por eso pide confirmación aparte del logout normal.
   *
   * La cola de capturas **no se toca**, ni siquiera acá (R9), y el diálogo lo dice: es la primera
   * pregunta que aparece y la respuesta tranquiliza.
   */
  async borrarDispositivo(): Promise<void> {
    const pendientes = await this.capturasPendientes();
    const otros = this.llavero.slotsAparcados().length;

    const confirmado = await this.confirmDialog.ask({
      title: 'Borrar los datos de este dispositivo',
      message:
        'Se van a borrar todas las sesiones guardadas' +
        (otros > 0 ? ` (${otros} además de la tuya)` : '') +
        ' y todo lo consultado sin conexión. Cada operario va a tener que volver a entrar con red.' +
        (pendientes > 0
          ? ` Las ${pendientes} captura(s) sin enviar NO se borran: siguen en la tablet.`
          : ''),
      type: 'error',
      confirmText: 'Borrar el dispositivo'
    });
    if (!confirmado) return;

    this.menuSvc.reset();
    this.auth.logout({ alcance: 'dispositivo' });
    this.router.navigate(['/login'], { replaceUrl: true });
  }

  /** `true` = seguir adelante. Sin capturas pendientes no se molesta a nadie. */
  private async confirmarSiHayCapturas(): Promise<boolean> {
    const pendientes = await this.capturasPendientes();
    if (pendientes === 0) {
      return true;
    }

    return this.confirmDialog.ask({
      title: 'Hay capturas sin enviar',
      message:
        `Tenés ${pendientes} captura(s) que todavía no llegaron al servidor. No se pierden: quedan en ` +
        'esta tablet hasta que vuelvas a entrar con tu usuario. ¿Cerrar sesión igual?',
      type: 'warning',
      confirmText: 'Cerrar sesión'
    });
  }

  private async capturasPendientes(): Promise<number> {
    const estado = await this.outbox.estado();
    return estado.pendientes + estado.rechazadas;
  }
}
