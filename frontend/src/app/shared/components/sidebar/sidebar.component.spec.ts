import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';

import { SidebarComponent } from './sidebar.component';
import { AuthService } from '../../../core/auth/auth.service';
import { LlaveroSesionesService } from '../../../core/auth/llavero-sesiones.service';
import { OutboxService } from '../../offline/outbox.service';
import { ConfirmDialogService } from '../../services/confirm-dialog.service';
import { MenuService } from '../../services/menu.service';
import type { AuthSession } from '../../../core/auth/auth.models';

/**
 * El pie del sidebar: las tres salidas.
 *
 * Dos cosas se prueban acá y ninguna es cosmética. La primera: **sin sesión no se muestran**, porque
 * el sidebar se decide por ruta y en pantallas públicas como `/diagnostico` aparece igual — y una de
 * las tres borra el equipo entero. La segunda: con capturas sin enviar se **avisa antes** de salir,
 * no después.
 */
describe('SidebarComponent · el pie', () => {
  const sesion = { accessToken: 'token', user: { id: 'guid-alex' } } as unknown as AuthSession;

  let fixture: ComponentFixture<SidebarComponent>;
  let componente: SidebarComponent;
  let session$: BehaviorSubject<AuthSession | null>;
  let ask: jasmine.Spy;
  let logout: jasmine.Spy;
  let navigate: jasmine.Spy;
  let estadoOutbox: { pendientes: number; rechazadas: number };
  let disponible: boolean;

  function ver(testId: string): HTMLElement | null {
    return (fixture.nativeElement as HTMLElement).querySelector(`[data-testid="${testId}"]`);
  }

  function montar(): void {
    // El router va REAL (`provideRouter`): la plantilla usa `routerLink`/`routerLinkActive`, que
    // necesitan `ActivatedRoute`. Solo se espia `navigate`.
    navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(SidebarComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();
  }

  beforeEach(() => {
    session$ = new BehaviorSubject<AuthSession | null>(sesion);
    estadoOutbox = { pendientes: 0, rechazadas: 0 };
    disponible = true;
    ask = jasmine.createSpy('ask').and.resolveTo(true);
    logout = jasmine.createSpy('logout');

    TestBed.configureTestingModule({
      imports: [SidebarComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { session$: session$.asObservable(), logout } },
        {
          provide: LlaveroSesionesService,
          useValue: { disponible: () => disponible, slotsAparcados: () => [] }
        },
        { provide: OutboxService, useValue: { estado: () => Promise.resolve(estadoOutbox) } },
        { provide: ConfirmDialogService, useValue: { ask } },
        { provide: MenuService, useValue: { menu$: of([]), ensureLoaded: () => of([]), reset: () => undefined } }
      ]
    });
  });

  describe('visibilidad', () => {
    it('con sesión se ven las tres salidas', () => {
      montar();

      expect(ver('cambiar-usuario')).not.toBeNull();
      expect(ver('cerrar-sesion')).not.toBeNull();
      expect(ver('borrar-dispositivo')).not.toBeNull();
    });

    it('🔑 SIN sesión no se ve ninguna: «borrar el dispositivo» no puede quedar a mano de cualquiera', () => {
      session$.next(null);
      montar();

      expect(ver('cambiar-usuario')).toBeNull();
      expect(ver('cerrar-sesion')).toBeNull();
      expect(ver('borrar-dispositivo')).toBeNull();
    });

    it('sin llavero en el equipo no se ofrece «cambiar de usuario», pero sí las otras dos', () => {
      disponible = false;
      montar();

      expect(ver('cambiar-usuario')).toBeNull();
      expect(ver('cerrar-sesion')).not.toBeNull();
    });
  });

  describe('cerrar sesión', () => {
    beforeEach(() => montar());

    it('sin capturas pendientes no molesta con un diálogo', async () => {
      await componente.logout();

      expect(ask).not.toHaveBeenCalled();
      expect(logout).toHaveBeenCalledWith({ hard: true });
      expect(navigate).toHaveBeenCalledWith(['/login'], { replaceUrl: true });
    });

    it('🔑 con capturas pendientes avisa ANTES de salir, y dice que no se pierden', async () => {
      estadoOutbox = { pendientes: 2, rechazadas: 1 };

      await componente.logout();

      expect(ask).toHaveBeenCalled();
      expect((ask.calls.mostRecent().args[0] as { message: string }).message).toContain('3');
      expect((ask.calls.mostRecent().args[0] as { message: string }).message).toContain('No se pierden');
    });

    it('🔑 si dice que no, NO se cierra nada', async () => {
      estadoOutbox = { pendientes: 1, rechazadas: 0 };
      ask.and.resolveTo(false);

      await componente.logout();

      expect(logout).not.toHaveBeenCalled();
      expect(navigate).not.toHaveBeenCalled();
    });
  });

  describe('borrar el dispositivo', () => {
    beforeEach(() => montar());

    it('pide confirmación y recién ahí borra, con alcance de dispositivo', async () => {
      await componente.borrarDispositivo();

      expect(ask).toHaveBeenCalled();
      expect(logout).toHaveBeenCalledWith({ alcance: 'dispositivo' });
    });

    it('🔑 el diálogo dice que las capturas sin enviar NO se borran', async () => {
      estadoOutbox = { pendientes: 4, rechazadas: 0 };

      await componente.borrarDispositivo();

      const mensaje = (ask.calls.mostRecent().args[0] as { message: string }).message;
      expect(mensaje).toContain('4');
      expect(mensaje).toContain('NO se borran');
    });

    it('cancelar no borra nada', async () => {
      ask.and.resolveTo(false);

      await componente.borrarDispositivo();

      expect(logout).not.toHaveBeenCalled();
      expect(navigate).not.toHaveBeenCalled();
    });
  });

  it('«cambiar de usuario» lleva a la pantalla de aparcar, sin cerrar nada', () => {
    montar();

    componente.cambiarDeUsuario();

    expect(navigate).toHaveBeenCalledWith(['/cambiar-usuario']);
    expect(logout).not.toHaveBeenCalled();
  });
});
