import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BehaviorSubject, of } from 'rxjs';

import { TokenStorageService } from '../../../../core/auth/token-storage.service';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';
import { CountryFilterService } from '../../../../core/services/country/country-filter.service';
import { CatalogoAlimentosService } from '../../../catalogo-alimentos/services/catalogo-alimentos.service';
import { GestionInventarioService } from '../../../gestion-inventario/services/gestion-inventario.service';
import { SeguimientoLoteLevanteDto, SeguimientoLoteLevanteService } from '../../services/seguimiento-lote-levante.service';
import { ModalDetalleSeguimientoLevanteComponent } from './modal-detalle-seguimiento.component';

describe('ModalDetalleSeguimientoLevanteComponent · detalle responsive', () => {
  let fixture: ComponentFixture<ModalDetalleSeguimientoLevanteComponent>;
  let component: ModalDetalleSeguimientoLevanteComponent;
  let getById: jasmine.Spy;

  const seguimiento = {
    id: 101,
    loteId: 'LEV-7',
    fechaRegistro: '2026-09-14',
    mortalidadHembras: 2,
    mortalidadMachos: 1,
    selH: 1,
    selM: 0,
    errorSexajeHembras: 0,
    errorSexajeMachos: 0,
    consumoKgHembras: 120,
    consumoKgMachos: 10,
    tipoAlimento: 'LEVANTE',
    ciclo: 'Normal',
    huevoTot: 80,
    huevoInc: 70,
    huevoLimpio: 70,
    huevoSucio: 10,
    metadata: {
      itemsHembras: [{ tipoItem: 'alimento', catalogItemId: 44, cantidad: 120, unidad: 'kg' }]
    }
  } as SeguimientoLoteLevanteDto;

  beforeEach(async () => {
    getById = jasmine.createSpy('getById').and.returnValue(of(seguimiento));

    await TestBed.configureTestingModule({
      imports: [ModalDetalleSeguimientoLevanteComponent],
      providers: [
        { provide: SeguimientoLoteLevanteService, useValue: { getById } },
        { provide: CatalogoAlimentosService, useValue: { getById: () => of({ nombre: 'Alimento levante', codigo: 'AL-44' }) } },
        { provide: GestionInventarioService, useValue: { getItemById: () => of(null) } },
        { provide: CountryFilterService, useValue: { isEcuadorOrPanama: () => false } },
        { provide: TokenStorageService, useValue: { session$: new BehaviorSubject(null) } },
        { provide: ActiveCompanyConfigService, useValue: { getFlags: () => of({ ocultaMachosEnPostura: false }) } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ModalDetalleSeguimientoLevanteComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('seguimiento', seguimiento);
    fixture.componentRef.setInput('isOpen', true);
    fixture.detectChanges();
  });

  it('abre en General y muestra el resumen del registro', () => {
    expect(component.pestanaActiva).toBe('general');
    expect(fixture.nativeElement.querySelector('#det-levante-panel-general')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.modal-heading__context').textContent).toContain('Registro #101');
    expect(fixture.nativeElement.querySelector('.record-summary').textContent).toContain('130');
  });

  it('usa botones tactiles y navega a Aves', () => {
    const boton: HTMLButtonElement = fixture.nativeElement.querySelector('#det-levante-tab-aves');
    boton.click();
    fixture.detectChanges();

    expect(component.pestanaActiva).toBe('aves');
    expect(boton.getAttribute('aria-selected')).toBe('true');
    expect(fixture.nativeElement.querySelector('#det-levante-panel-aves')).toBeTruthy();
  });

  it('conserva los ítems de Levante en una sección propia', () => {
    component.seleccionarPestana('items');
    fixture.detectChanges();

    const panel: HTMLElement = fixture.nativeElement.querySelector('#det-levante-panel-items');
    expect(panel.textContent).toContain('Hembras');
    expect(panel.textContent).toContain('AL-44 - Alimento levante');
    expect(panel.textContent).toContain('120.00');
  });

  it('muestra Huevos cuando el registro contiene captura', () => {
    expect(component.tieneHuevos).toBeTrue();
    component.seleccionarPestana('huevos');
    fixture.detectChanges();

    const panel: HTMLElement = fixture.nativeElement.querySelector('#det-levante-panel-huevos');
    expect(panel.textContent).toContain('Total huevos');
    expect(panel.textContent).toContain('80');
    expect(panel.textContent).toContain('Limpio');
  });

  it('oculta los datos de machos cuando lo exige la empresa', () => {
    component.ocultaMachosEnPostura = true;
    component.seleccionarPestana('aves');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#det-levante-panel-aves').textContent).not.toContain('Machos');
  });

  it('no hereda los ítems del registro anterior al abrir uno sin desglose', () => {
    const sinItems = { ...seguimiento, id: 102, metadata: null } as SeguimientoLoteLevanteDto;
    getById.and.returnValue(of(sinItems));

    fixture.componentRef.setInput('isOpen', false);
    fixture.detectChanges();
    fixture.componentRef.setInput('seguimiento', sinItems);
    fixture.componentRef.setInput('isOpen', true);
    fixture.detectChanges();

    expect(component.itemsHembras).toEqual([]);
    expect(component.itemsMachos).toEqual([]);
    expect(component.itemsGenerales).toEqual([]);
  });

  it('al reabrir el detalle vuelve a General y hace una sola consulta por apertura', () => {
    component.seleccionarPestana('huevos');
    fixture.componentRef.setInput('isOpen', false);
    fixture.detectChanges();
    fixture.componentRef.setInput('isOpen', true);
    fixture.detectChanges();

    expect(component.pestanaActiva).toBe('general');
    expect(getById).toHaveBeenCalledTimes(2);
  });
});
