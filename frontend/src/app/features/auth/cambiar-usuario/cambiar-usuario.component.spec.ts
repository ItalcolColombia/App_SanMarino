import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';

import { CambiarUsuarioComponent } from './cambiar-usuario.component';
import { AuthService } from '../../../core/auth/auth.service';
import { LlaveroSesionesService } from '../../../core/auth/llavero-sesiones.service';
import { TokenStorageService } from '../../../core/auth/token-storage.service';
import { ConexionService } from '../../../core/pwa/conexion.service';
import { OutboxService } from '../../../shared/offline/outbox.service';
import { SyncService } from '../../../shared/offline/sync.service';

/**
 * Aparcar la sesión propia.
 *
 * El error caro de esta pantalla no es visual: es **soltar la sesión sin haberla podido guardar**. Si
 * el cifrado falla y el storage ya se limpió, el operario queda sin sesión y sin copia, en una granja
 * sin señal. Por eso el orden —sellar primero, soltar después— está fijado con un test.
 */
describe('CambiarUsuarioComponent', () => {
  let fixture: ComponentFixture<CambiarUsuarioComponent>;
  let componente: CambiarUsuarioComponent;
  let cambiarDeUsuario: jasmine.Spy;
  let navigate: jasmine.Spy;
  let sincronizar: jasmine.Spy;
  let disponible: boolean;
  let hayConexionReal: boolean;
  let estadoOutbox: { pendientes: number; rechazadas: number };

  function texto(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function ver(testId: string): HTMLElement | null {
    return (fixture.nativeElement as HTMLElement).querySelector(`[data-testid="${testId}"]`);
  }

  async function montar(): Promise<void> {
    fixture = TestBed.createComponent(CambiarUsuarioComponent);
    componente = fixture.componentInstance;
    fixture.detectChanges();
    await componente.ngOnInit();
    fixture.detectChanges();
  }

  beforeEach(() => {
    disponible = true;
    hayConexionReal = true;
    estadoOutbox = { pendientes: 0, rechazadas: 0 };
    navigate = jasmine.createSpy('navigate').and.resolveTo(true);
    cambiarDeUsuario = jasmine.createSpy('cambiarDeUsuario').and.resolveTo(true);
    sincronizar = jasmine.createSpy('sincronizar').and.resolveTo(2);

    TestBed.configureTestingModule({
      imports: [CambiarUsuarioComponent],
      providers: [
        { provide: Router, useValue: { navigate } },
        { provide: AuthService, useValue: { cambiarDeUsuario } },
        { provide: LlaveroSesionesService, useValue: { disponible: () => disponible } },
        {
          provide: TokenStorageService,
          useValue: { get: () => ({ user: { fullName: 'Alex Londoño', username: 'alex@sanmarino.com.co' } }) }
        },
        { provide: OutboxService, useValue: { estado: () => Promise.resolve(estadoOutbox) } },
        { provide: SyncService, useValue: { sincronizar } },
        { provide: ConexionService, useValue: { hayConexionReal: () => hayConexionReal } }
      ]
    });
  });

  it('muestra de quién es la sesión que se va a guardar', async () => {
    await montar();

    expect(ver('nombre')?.textContent).toContain('Alex Londoño');
  });

  it('🔑 avisa ANTES del aviso legal: el PIN no se recupera y a los 5 fallos se borra', async () => {
    await montar();

    expect(texto()).toContain('No se guarda en ningún lado');
    expect(texto()).toContain('5 intentos equivocados');
    expect(texto()).toContain('no se pierde');
  });

  describe('el PIN', () => {
    beforeEach(async () => await montar());

    it('descarta lo que no sea dígito y corta en 6, en los dos campos', () => {
      componente.alEscribirPin('12a3-456789');
      componente.alEscribirConfirmacion('9x8 7654321');

      expect(componente.pin).toBe('123456');
      expect(componente.confirmacion).toBe('987654');
    });

    it('el botón se habilita recién con los dos campos completos', () => {
      componente.alEscribirPin('123456');
      expect(componente.pinCompleto).toBeFalse();

      componente.alEscribirConfirmacion('123456');
      expect(componente.pinCompleto).toBeTrue();
    });

    it('🔑 si no coinciden NO se aparca, y se limpia solo la confirmación', async () => {
      // Reescribir los 12 dígitos por un error en los últimos 6 es cómo se termina eligiendo un PIN
      // corto de memoria, que es justo lo que no se quiere.
      componente.alEscribirPin('123456');
      componente.alEscribirConfirmacion('654321');

      await componente.aparcar();

      expect(cambiarDeUsuario).not.toHaveBeenCalled();
      expect(componente.error).toContain('no coinciden');
      expect(componente.pin).toBe('123456');
      expect(componente.confirmacion).toBe('');
    });

    it('con los dos PIN iguales aparca y va al selector', async () => {
      componente.alEscribirPin('482913');
      componente.alEscribirConfirmacion('482913');

      await componente.aparcar();

      expect(cambiarDeUsuario).toHaveBeenCalledWith('482913');
      expect(navigate).toHaveBeenCalledWith(['/selector-usuario'], { replaceUrl: true });
    });

    it('🔑 si el cifrado falla NO se navega: la sesión sigue activa y se dice', async () => {
      cambiarDeUsuario.and.resolveTo(false);
      componente.alEscribirPin('482913');
      componente.alEscribirConfirmacion('482913');

      await componente.aparcar();

      expect(navigate).not.toHaveBeenCalled();
      expect(componente.error).toContain('Seguís conectado');
      expect(componente.aparcando).toBeFalse();
    });

    it('dos toques seguidos no aparcan dos veces', async () => {
      componente.alEscribirPin('482913');
      componente.alEscribirConfirmacion('482913');

      const primera = componente.aparcar();
      await componente.aparcar();
      await primera;

      expect(cambiarDeUsuario).toHaveBeenCalledTimes(1);
    });
  });

  describe('capturas sin enviar', () => {
    it('sin pendientes no molesta con el aviso', async () => {
      await montar();

      expect(ver('aviso-pendientes')).toBeNull();
    });

    it('🔑 con pendientes dice cuántas son y que NO se pierden', async () => {
      estadoOutbox = { pendientes: 3, rechazadas: 1 };
      await montar();

      expect(ver('aviso-pendientes')?.textContent).toContain('4');
      expect(texto()).toContain('no se pierden');
    });

    it('con red ofrece enviarlas en el acto (R-M5)', async () => {
      estadoOutbox = { pendientes: 2, rechazadas: 0 };
      await montar();

      expect(ver('enviar-ahora')).not.toBeNull();

      await componente.enviarPendientes();
      fixture.detectChanges();

      expect(sincronizar).toHaveBeenCalled();
      expect(ver('mensaje-envio')?.textContent).toContain('2');
    });

    it('🔑 sin red NO ofrece enviar ni bloquea: bloquear a quien no tiene señal es encerrarlo', async () => {
      estadoOutbox = { pendientes: 2, rechazadas: 0 };
      hayConexionReal = false;
      await montar();

      expect(ver('enviar-ahora')).toBeNull();
      expect(texto()).toContain('Sin conexión no se pueden enviar');
      // Y aparcar sigue permitido.
      componente.alEscribirPin('482913');
      componente.alEscribirConfirmacion('482913');
      await componente.aparcar();
      expect(cambiarDeUsuario).toHaveBeenCalled();
    });
  });

  it('🔑 sin llavero no ofrece aparcar: no se guarda una sesión a medio cifrar', async () => {
    disponible = false;
    await montar();

    expect(ver('sin-llavero')).not.toBeNull();
    expect(ver('guardar')).toBeNull();

    componente.alEscribirPin('482913');
    componente.alEscribirConfirmacion('482913');
    await componente.aparcar();

    expect(cambiarDeUsuario).not.toHaveBeenCalled();
  });
});
