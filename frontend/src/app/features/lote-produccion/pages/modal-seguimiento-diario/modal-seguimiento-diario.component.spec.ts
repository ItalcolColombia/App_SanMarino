import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Subject, of } from 'rxjs';

import { ModalSeguimientoDiarioComponent } from './modal-seguimiento-diario.component';
import { CatalogoAlimentosService } from '../../../catalogo-alimentos/services/catalogo-alimentos.service';
import { InventarioService } from '../../../inventario/services/inventario.service';
import { GestionInventarioService } from '../../../gestion-inventario/services/gestion-inventario.service';
import { CountryFilterService } from '../../../../core/services/country/country-filter.service';
import { TokenStorageService } from '../../../../core/auth/token-storage.service';
import { ActiveCompanyConfigService, CompanyFlags } from '../../../../core/services/company-config/active-company-config.service';
import { UserPermissionService } from '../../../../core/auth/user-permission.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { SilosService } from '../../../silos/services/silos.service';
import { LoteHuevoItemDto, LoteHuevoItemsService } from '../../../lote/services/lote-huevo-items.service';
import type { CrearSeguimientoRequest } from '../../services/produccion.service';

/**
 * Santa Reyes, 18-sep-2026: «guarda el movimiento pero los huevos quedan en 0, y toca hacerlo 2-3 veces».
 *
 * Causa medida en el navegador: el `ngOnChanges` del modal llamaba a `resetForm()` ante CUALQUIER cambio de
 * `@Input` con el modal abierto. La consulta pesada `informacion-lote` trae `fechaEncaset` (y
 * `GET /LotePosturaProduccion/{id}` el lote base) DESPUÉS de que el operario abre «Nuevo registro»: cada
 * llegada vaciaba el formulario, y también cada vez que el padre cambiaba `loading` (al guardar y tras un
 * guardado rechazado). Además el botón Guardar no esperaba los tipos de huevo del lote.
 *
 * Las pruebas usan el componente REAL con servicios simulados: lo que se fija es el comportamiento del
 * formulario ante datos tardíos, no una función aislada.
 */
describe('ModalSeguimientoDiarioComponent · datos que llegan tarde y guardado con huevos', () => {
  let fixture: ComponentFixture<ModalSeguimientoDiarioComponent>;
  let component: ModalSeguimientoDiarioComponent;

  /** Cada `GET /LoteHuevoItem/{lote}` queda pendiente hasta que la prueba lo resuelva (o lo haga fallar). */
  class TiposHuevoSimulado {
    llamadas: Array<{ loteId: number; respuesta: Subject<LoteHuevoItemDto[]> }> = [];
    getByLote(loteId: number) {
      const respuesta = new Subject<LoteHuevoItemDto[]>();
      this.llamadas.push({ loteId, respuesta });
      return respuesta.asObservable();
    }
    get ultima() { return this.llamadas[this.llamadas.length - 1]; }
  }

  const tipo = (catalogItemId: number, nombre: string, tipoHuevo: 'Primera' | 'Pnc'): LoteHuevoItemDto => ({
    id: catalogItemId, loteId: 152, catalogItemId, codigo: String(catalogItemId), nombre, tipoHuevo,
    um: 'UND', primeraPostura: false, itemActivo: true, activo: true
  });
  const TIPOS = [tipo(656, 'HUEVO SIN CLASIFICAR ROJO', 'Primera'), tipo(666, 'HUEVO MANCHADO ROJO', 'Pnc')];

  /** Solo lo que el componente lee: con `clasificacionHuevoPorItems` (Santa Reyes) y sin silos ni inventario. */
  const FLAGS = {
    clasificacionHuevoPorItems: true,
    manejaInventarioPorSilo: false,
    semanasCicloPosturaPorRaza: false,
    consumoAlimentoSoloHembras: false,
    permiteSeguimientoDiarioParcial: false,
    ocultaMachosEnPostura: false,
    huevoPrimeraPosturaHastaSemana: null
  } as unknown as CompanyFlags;

  let tipos: TiposHuevoSimulado;
  let toast: jasmine.SpyObj<ToastService>;
  let confirmar: jasmine.Spy;
  let emitidos: CrearSeguimientoRequest[];

  /** Arma el modal REAL con los servicios simulados y la empresa (flags) indicada. */
  async function montar(flags: CompanyFlags): Promise<void> {
    tipos = new TiposHuevoSimulado();
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error', 'warning', 'info']);
    confirmar = jasmine.createSpy('ask').and.resolveTo(true);

    await TestBed.configureTestingModule({
      imports: [ModalSeguimientoDiarioComponent],
      providers: [
        { provide: LoteHuevoItemsService, useValue: tipos },
        { provide: ActiveCompanyConfigService, useValue: { getFlags: () => of(flags) } },
        { provide: ToastService, useValue: toast },
        { provide: ConfirmDialogService, useValue: { ask: confirmar } },
        { provide: CountryFilterService, useValue: { isEcuadorOrPanama: () => false, isColombia: () => false } },
        { provide: TokenStorageService, useValue: { get: () => ({ user: { id: 'u-1' } }) } },
        { provide: UserPermissionService, useValue: { has: () => false } },
        { provide: SilosService, useValue: {} },
        { provide: CatalogoAlimentosService, useValue: {} },
        { provide: InventarioService, useValue: {} },
        { provide: GestionInventarioService, useValue: {} }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ModalSeguimientoDiarioComponent);
    component = fixture.componentInstance;
    emitidos = [];
    component.save.subscribe(r => emitidos.push(r));
    fixture.detectChanges(); // ngOnInit: formulario y flags
  }

  beforeEach(async () => { await montar(FLAGS); });

  afterEach(() => {
    // Ninguna consulta simulada queda colgada: el `timeout` real de 20 s no debe sobrevivir a la prueba.
    for (const l of tipos.llamadas) if (!l.respuesta.closed) l.respuesta.complete();
    fixture.destroy();
  });

  // ── ayudas ──────────────────────────────────────────────────────────────────────────────────────

  /** «Nuevo registro» tal como lo abre el padre: lote (LPP), lote base y modal abierto en el mismo ciclo. */
  function abrir(loteId: number | null = 152): void {
    fixture.componentRef.setInput('lotePosturaProduccionId', 20);
    fixture.componentRef.setInput('loteId', loteId);
    fixture.componentRef.setInput('isOpen', true);
    fixture.detectChanges();
  }

  function resolverTipos(lista: LoteHuevoItemDto[] = TIPOS): void {
    tipos.ultima.respuesta.next(lista);
    tipos.ultima.respuesta.complete();
    fixture.detectChanges();
  }

  function cambiarInput(nombre: string, valor: unknown): void {
    fixture.componentRef.setInput(nombre, valor);
    fixture.detectChanges();
  }

  const escribirMortalidad = (n: number) => component.form.get('mortalidadH')!.setValue(n);
  const escribirHuevo = (catalogItemId: number, n: number) =>
    component.huevoItemsArray.at(component.indiceFilaHuevo(catalogItemId)).get('cantidad')!.setValue(n);
  const leerHuevo = (catalogItemId: number) =>
    component.huevoItemsArray.at(component.indiceFilaHuevo(catalogItemId)).get('cantidad')!.value;

  // ── D1 · lo tecleado sobrevive a los datos tardíos ─────────────────────────────────────────────

  it('un fechaEncaset tardío (informacion-lote) NO borra lo tecleado — el defecto medido', () => {
    abrir();
    resolverTipos();
    escribirMortalidad(9);
    escribirHuevo(656, 5000);
    escribirHuevo(666, 300);

    cambiarInput('fechaEncaset', '2026-08-19T19:00:00-05:00');

    expect(component.form.get('mortalidadH')!.value).toBe(9);
    expect(leerHuevo(656)).toBe(5000);
    expect(leerHuevo(666)).toBe(300);
    expect(component.totalHuevosClasificados).toBe(5300);
  });

  it('cambiar `loading` (guardar / guardado rechazado) NO vacía el formulario', () => {
    abrir();
    resolverTipos();
    escribirMortalidad(4);
    escribirHuevo(656, 1200);

    cambiarInput('loading', true);   // el padre empieza a guardar
    cambiarInput('loading', false);  // …y el backend lo rechaza: el modal sigue abierto

    expect(component.form.get('mortalidadH')!.value).toBe(4);
    expect(leerHuevo(656)).toBe(1200);
  });

  it('el lote base que llega tarde recarga los tipos de huevo SIN borrar lo tecleado', () => {
    abrir(null);                       // el padre aún no resolvió el lote base
    escribirMortalidad(7);
    expect(component.errorHuevoItemsDelLote).toBeTrue();   // sin lote no hay dónde teclear huevos

    cambiarInput('loteId', 152);       // llega el lote base
    expect(tipos.ultima.loteId).toBe(152);
    resolverTipos();

    expect(component.errorHuevoItemsDelLote).toBeFalse();
    expect(component.hayFilasHuevo).toBeTrue();
    expect(component.form.get('mortalidadH')!.value).toBe(7);
  });

  it('un cambio de granja/galpón con el modal abierto tampoco lo reinicia', () => {
    abrir();
    resolverTipos();
    escribirHuevo(656, 800);

    cambiarInput('galponId', 'G0497');

    expect(leerHuevo(656)).toBe(800);
  });

  // ── Abrir SÍ reinicia (y limpia el aviso viejo) ─────────────────────────────────────────────────

  it('cerrar y volver a abrir deja el formulario en blanco y sin aviso de un guardado anterior', () => {
    abrir();
    resolverTipos();
    escribirMortalidad(11);
    escribirHuevo(656, 2000);
    component.showMessageModal = true;   // el aviso que antes sobrevivía al cierre

    cambiarInput('isOpen', false);
    cambiarInput('isOpen', true);
    resolverTipos();

    expect(component.form.get('mortalidadH')!.value).toBe(0);
    expect(leerHuevo(656)).toBe(0);
    expect(component.showMessageModal).toBeFalse();
  });

  // ── D4 · Guardar espera / confirma según los tipos de huevo ────────────────────────────────────

  it('mientras los tipos de huevo viajan Guardar espera: no emite y avisa', async () => {
    abrir();
    expect(component.cargandoHuevoItemsDelLote).toBeTrue();
    expect(component.guardadoEsperaTiposHuevo).toBeTrue();

    await component.onSave();

    expect(emitidos.length).toBe(0);
    expect(toast.warning).toHaveBeenCalledTimes(1);
  });

  it('con los tipos cargados guarda y los huevos viajan en el request', async () => {
    abrir();
    resolverTipos();
    escribirMortalidad(15);
    escribirHuevo(656, 6600);
    escribirHuevo(666, 410);
    expect(component.guardadoEsperaTiposHuevo).toBeFalse();

    await component.onSave();

    expect(emitidos.length).toBe(1);
    expect(emitidos[0].mortalidadH).toBe(15);
    expect((emitidos[0].huevoItems ?? []).map(h => `${h.catalogItemId}:${h.cantidad}`)).toEqual(['656:6600', '666:410']);
    expect(confirmar).not.toHaveBeenCalled();
  });

  it('un dato tardío entre teclear y guardar NO deja el registro sin huevos (el síntoma reportado)', async () => {
    abrir();
    resolverTipos();
    escribirHuevo(656, 5000);
    cambiarInput('fechaEncaset', '2026-08-19T19:00:00-05:00');   // llega informacion-lote
    escribirMortalidad(3);                                        // el operario sigue con la pestaña General

    await component.onSave();

    expect(emitidos[0].mortalidadH).toBe(3);
    expect(emitidos[0].huevoItems?.length).toBe(1);
    expect(emitidos[0].huevoItems![0].cantidad).toBe(5000);
  });

  it('la consulta de tipos falló: pide confirmar y, si se cancela, no guarda', async () => {
    const consola = spyOn(console, 'error');
    abrir();
    tipos.ultima.respuesta.error(new Error('sin red'));
    fixture.detectChanges();
    expect(component.errorHuevoItemsDelLote).toBeTrue();
    expect(component.guardadoEsperaTiposHuevo).toBeFalse();   // confirmar, no esperar
    confirmar.and.resolveTo(false);

    await component.onSave();

    expect(confirmar).toHaveBeenCalledTimes(1);
    expect(emitidos.length).toBe(0);
    expect(consola).toHaveBeenCalled();   // el fallo queda en consola para diagnóstico
  });

  it('la consulta de tipos falló y se confirma: guarda SIN huevos, sabiéndolo', async () => {
    spyOn(console, 'error');
    abrir();
    tipos.ultima.respuesta.error(new Error('sin red'));
    fixture.detectChanges();
    confirmar.and.resolveTo(true);

    await component.onSave();

    expect(emitidos.length).toBe(1);
    expect(emitidos[0].huevoItems).toEqual([]);
  });

  it('«Reintentar» vuelve a pedir los tipos sin cerrar el modal ni vaciar lo tecleado', () => {
    spyOn(console, 'error');
    abrir();
    tipos.ultima.respuesta.error(new Error('sin red'));
    fixture.detectChanges();
    escribirMortalidad(6);
    const antes = tipos.llamadas.length;

    component.reintentarTiposHuevo();
    expect(tipos.llamadas.length).toBe(antes + 1);
    resolverTipos();

    expect(component.errorHuevoItemsDelLote).toBeFalse();
    expect(component.hayFilasHuevo).toBeTrue();
    expect(component.form.get('mortalidadH')!.value).toBe(6);
  });

  it('un guardado ya en curso no se repite (`loading`)', async () => {
    abrir();
    resolverTipos();
    cambiarInput('loading', true);

    await component.onSave();

    expect(emitidos.length).toBe(0);
  });

  // ── Empresas SIN clasificación por ítems (Sanmarino, Ecuador, Panamá, Demo) ─────────────────────

  describe('empresa con las 11 categorías fijas (flag apagado)', () => {
    beforeEach(async () => {
      fixture.destroy();
      TestBed.resetTestingModule();
      await montar({ ...FLAGS, clasificacionHuevoPorItems: false } as CompanyFlags);
    });

    it('nunca consulta ni espera los tipos de huevo, y Guardar no se gatea', async () => {
      abrir();

      expect(tipos.llamadas.length).toBe(0);
      expect(component.guardadoEsperaTiposHuevo).toBeFalse();

      await component.onSave();

      expect(emitidos.length).toBe(1);
      expect(confirmar).not.toHaveBeenCalled();
      expect(toast.warning).not.toHaveBeenCalled();
    });

    it('un dato tardío tampoco borra lo tecleado y las categorías viajan como siempre', async () => {
      abrir();
      component.form.get('huevoLimpio')!.setValue(4000);
      component.form.get('huevoSucio')!.setValue(120);

      cambiarInput('fechaEncaset', '2026-08-19T19:00:00-05:00');
      await component.onSave();

      expect(component.form.get('huevoLimpio')!.value).toBe(4000);
      expect(emitidos[0].huevoLimpio).toBe(4000);
      expect(emitidos[0].huevoSucio).toBe(120);
      expect(emitidos[0].huevosTotales).toBe(4120);
      expect(emitidos[0].huevoItems).toBeUndefined();
    });
  });
});
