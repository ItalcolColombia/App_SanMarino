import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';

import { SelectorUsuarioComponent } from './selector-usuario.component';
import { LlaveroSesionesService } from '../../../core/auth/llavero-sesiones.service';
import { MAX_INTENTOS_PIN } from '../../../core/auth/funciones/llavero-sesiones.funcion';
import type { ResultadoActivacion, SlotSesion } from '../../../core/auth/models/slot-sesion.model';

/**
 * El selector de perfil.
 *
 * Lo que se prueba acá no es el aspecto: es que **no se pierda plata en errores de UI**. Gastar un
 * intento de PIN por un dedo lento, esconder una sesión que no se puede abrir, o dejar el formulario
 * habilitado mientras la página se recarga son los tres errores que se pagan en el galpón.
 */
describe('SelectorUsuarioComponent', () => {
  const HORA = 60 * 60 * 1000;

  let fixture: ComponentFixture<SelectorUsuarioComponent>;
  let componente: SelectorUsuarioComponent;
  let recargarPagina: jasmine.Spy;
  let navigate: jasmine.Spy;
  let activar: jasmine.Spy;
  let slots: SlotSesion[];
  let pendientes: Record<string, number>;
  let disponible: boolean;

  function slot(userId: string, over: Partial<SlotSesion> = {}): SlotSesion {
    const ahora = Date.now();
    return {
      slotId: `slot-${userId}`,
      userId,
      nombre: `Operario ${userId}`,
      email: `${userId}@sanmarino.com.co`,
      empresa: 'Agroavicola Sanmarino',
      companyId: 1,
      paisId: 1,
      ultimoUsoEn: ahora - HORA,
      ultimoContactoOkEn: ahora - HORA,
      saltB64: 'salt',
      intentosFallidos: 0,
      ...over
    };
  }

  function texto(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function ver(testId: string): HTMLElement | null {
    return (fixture.nativeElement as HTMLElement).querySelector(`[data-testid="${testId}"]`);
  }

  async function montar(): Promise<void> {
    fixture = TestBed.createComponent(SelectorUsuarioComponent);
    componente = fixture.componentInstance;
    // `recargarPagina` es protected a propósito: sin interceptarlo, un `location.reload()` en el
    // runner se lleva puesta la suite entera.
    recargarPagina = spyOn(componente as unknown as { recargarPagina: () => void }, 'recargarPagina');

    fixture.detectChanges();
    // `ngOnInit` es async y su promesa no la espera nadie: `whenStable()` vuelve con la pantalla
    // todavía en "Cargando…". Se espera la carga explícitamente en vez de confiar en la estabilidad.
    await componente.recargar();
    fixture.detectChanges();
  }

  beforeEach(() => {
    slots = [slot('alex')];
    pendientes = {};
    disponible = true;
    navigate = jasmine.createSpy('navigate').and.resolveTo(true);
    activar = jasmine.createSpy('activar').and.resolveTo({ estado: 'no_disponible' } as ResultadoActivacion);

    TestBed.configureTestingModule({
      imports: [SelectorUsuarioComponent],
      providers: [
        { provide: Router, useValue: { navigate } },
        {
          provide: LlaveroSesionesService,
          useValue: {
            disponible: () => disponible,
            slotsAparcados: () => slots,
            pendientesPorSlot: () => Promise.resolve(pendientes),
            activar
          }
        }
      ]
    });
  });

  describe('la lista', () => {
    it('pinta las sesiones guardadas con empresa y hace cuánto', async () => {
      await montar();

      expect(ver('lista-slots')).not.toBeNull();
      expect(texto()).toContain('Operario alex');
      expect(texto()).toContain('Agroavicola Sanmarino');
      expect(texto()).toContain('hace 1 h');
    });

    it('sin sesiones guardadas lo dice y ofrece entrar con red', async () => {
      slots = [];
      await montar();

      expect(ver('sin-sesiones')).not.toBeNull();
      expect(ver('otro-usuario')).not.toBeNull();
    });

    it('🔑 muestra cuántas capturas esperan: es la respuesta a «¿dónde quedó lo que cargué?»', async () => {
      pendientes = { 'slot-alex': 4 };
      await montar();

      expect(ver('pendientes')?.textContent).toContain('4 sin enviar');
    });

    it('🔑 un slot vencido NO se esconde: se muestra apagado y con el motivo', async () => {
      slots = [slot('alex', { ultimoContactoOkEn: Date.now() - 20 * HORA })];
      await montar();

      expect(ver('vencido')).not.toBeNull();
      expect(texto()).toContain('Operario alex');
    });

    it('un slot con el PIN agotado dice que hay que entrar con red', async () => {
      slots = [slot('alex', { requiereReingreso: true })];
      await montar();

      expect(ver('requiere-reingreso')).not.toBeNull();
    });

    it('🔑 elegir un slot que no se puede abrir lleva al login, no a un PIN inútil', async () => {
      slots = [slot('alex', { ultimoContactoOkEn: Date.now() - 20 * HORA })];
      await montar();

      componente.elegir(componente.filas[0]);

      expect(navigate).toHaveBeenCalledWith(['/login']);
      expect(componente.elegida).toBeNull();
    });

    it('sin llavero en el dispositivo lo dice y manda al login normal', async () => {
      disponible = false;
      await montar();

      expect(ver('sin-llavero')).not.toBeNull();
      expect(ver('lista-slots')).toBeNull();
    });
  });

  describe('el PIN', () => {
    beforeEach(async () => {
      await montar();
      componente.elegir(componente.filas[0]);
      fixture.detectChanges();
    });

    it('pide 6 dígitos', () => {
      expect(componente.digitosPin).toBe(6);
      expect(texto()).toContain('PIN de 6 dígitos');
    });

    it('descarta lo que no sea dígito y corta en 6', () => {
      componente.alEscribirPin('12a3-45 6789');

      expect(componente.pin).toBe('123456');
    });

    it('🔑 no se puede intentar con el PIN incompleto: un intento gastado no se recupera', async () => {
      componente.alEscribirPin('1234');
      expect(componente.pinCompleto).toBeFalse();

      await componente.activar();

      expect(activar).not.toHaveBeenCalled();
    });

    it('🔑 el PIN incorrecto dice cuántos intentos quedan', async () => {
      activar.and.resolveTo({ estado: 'pin_incorrecto', intentosRestantes: 3 });
      componente.alEscribirPin('000000');

      await componente.activar();
      fixture.detectChanges();

      expect(ver('error-pin')?.textContent).toContain('3 intentos');
      // El campo se limpia: reintentar sobre los dígitos viejos gasta otro intento sin querer.
      expect(componente.pin).toBe('');
    });

    it('con 1 intento restante lo dice en singular y avisa qué pasa después', async () => {
      activar.and.resolveTo({ estado: 'pin_incorrecto', intentosRestantes: 1 });
      componente.alEscribirPin('000000');

      await componente.activar();

      expect(componente.error).toContain('1 intento');
      expect(componente.error).toContain('red');
    });

    it(`al ${MAX_INTENTOS_PIN}.º vuelve a la lista y avisa que la cola NO se perdió`, async () => {
      activar.and.resolveTo({ estado: 'slot_destruido' });
      componente.alEscribirPin('000000');

      await componente.activar();

      expect(componente.elegida).toBeNull();
      expect(componente.error).toContain('capturado sin enviar');
    });

    it('🔑 el PIN correcto recarga la página y NO reactiva el formulario', async () => {
      // Recargar es la garantía estructural de que nada de la empresa anterior sobreviva. Y dejar
      // `activando` en false haría parpadear el botón habilitado mientras la página se va.
      activar.and.resolveTo({ estado: 'activado', sesion: { accessToken: 'x' } as never });
      componente.alEscribirPin('482913');

      await componente.activar();

      expect(recargarPagina).toHaveBeenCalled();
      expect(componente.activando).toBeTrue();
    });

    it('el aviso de que el PIN no se recupera sin conexión está en pantalla', () => {
      expect(texto()).toContain('no hay forma de recuperarlo sin conexión');
    });

    it('«elegir otra sesión» limpia el PIN y el error', () => {
      componente.alEscribirPin('123456');
      componente.error = 'algo';

      componente.volverALaLista();

      expect(componente.elegida).toBeNull();
      expect(componente.pin).toBe('');
      expect(componente.error).toBe('');
    });

    it('dos toques seguidos en Entrar no disparan dos activaciones', async () => {
      activar.and.resolveTo({ estado: 'pin_incorrecto', intentosRestantes: 4 });
      componente.alEscribirPin('000000');

      const primera = componente.activar();
      await componente.activar();
      await primera;

      expect(activar).toHaveBeenCalledTimes(1);
    });
  });
});
