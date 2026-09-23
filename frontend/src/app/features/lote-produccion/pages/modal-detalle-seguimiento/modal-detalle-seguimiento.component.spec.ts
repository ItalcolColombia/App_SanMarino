import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';
import { CountryFilterService } from '../../../../core/services/country/country-filter.service';
import { ProduccionService, SeguimientoItemDto } from '../../services/produccion.service';
import { ModalDetalleSeguimientoComponent } from './modal-detalle-seguimiento.component';

describe('ModalDetalleSeguimientoComponent · detalle responsive', () => {
  let fixture: ComponentFixture<ModalDetalleSeguimientoComponent>;
  let component: ModalDetalleSeguimientoComponent;
  let obtenerDetalle: jasmine.Spy;

  const seguimiento = {
    id: 101,
    produccionLoteId: 20,
    fechaRegistro: '2026-09-14',
    etapa: 2,
    mortalidadH: 15,
    mortalidadM: 3,
    selH: 2,
    selM: 1,
    consKgH: 1215,
    consKgM: 10,
    consumoKg: 1225,
    huevosTotales: 7010,
    huevosIncubables: 0,
    pesoHuevo: 0,
    metadata: {
      huevoItems: [
        { catalogItemId: 656, codigo: '2520', nombre: 'Huevo primera', tipo: 'Primera', cantidad: 6600 },
        { catalogItemId: 667, codigo: '2521', nombre: 'Huevo PNC', tipo: 'Pnc', cantidad: 410 }
      ]
    }
  } as unknown as SeguimientoItemDto;

  beforeEach(async () => {
    obtenerDetalle = jasmine.createSpy('obtenerSeguimientoPorId').and.returnValue(of(seguimiento));

    await TestBed.configureTestingModule({
      imports: [ModalDetalleSeguimientoComponent],
      providers: [
        { provide: ProduccionService, useValue: { obtenerSeguimientoPorId: obtenerDetalle } },
        { provide: CountryFilterService, useValue: { isEcuadorOrPanama: () => false } },
        {
          provide: ActiveCompanyConfigService,
          useValue: {
            getFlags: () => of({
              clasificacionHuevoPorItems: true,
              ocultaMachosEnPostura: true
            })
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ModalDetalleSeguimientoComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('seguimientoId', 101);
    fixture.componentRef.setInput('isOpen', true);
    fixture.detectChanges();
  });

  it('abre en General y muestra el resumen que identifica el registro', () => {
    expect(component.pestanaActiva).toBe('general');
    expect(fixture.nativeElement.querySelector('#panel-det-general')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.record-summary').textContent).toContain('7,010');
    expect(fixture.nativeElement.querySelector('.modal-heading__context').textContent).toContain('Registro #101');
  });

  it('usa botones tactiles y cambia a la pestana Aves', () => {
    const boton: HTMLButtonElement = fixture.nativeElement.querySelector('#det-tab-aves');
    expect(boton.tagName).toBe('BUTTON');
    boton.click();
    fixture.detectChanges();

    expect(component.pestanaActiva).toBe('aves');
    expect(fixture.nativeElement.querySelector('#panel-det-aves')).toBeTruthy();
    expect(boton.getAttribute('aria-selected')).toBe('true');
  });

  it('Santa Reyes no renderiza datos de machos en Aves ni Pesaje', () => {
    component.seleccionarPestana('aves');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#panel-det-aves').textContent).not.toContain('Machos');

    component.seleccionarPestana('pesaje');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#panel-det-pesaje').textContent).not.toContain('Peso machos');
  });

  it('la clasificacion por items usa una referencia estable cargada desde metadata', () => {
    const referencia = component.huevoItemsGuardados;
    component.seleccionarPestana('huevos');
    fixture.detectChanges();

    const panel: HTMLElement = fixture.nativeElement.querySelector('#panel-det-huevos');
    expect(component.huevoItemsGuardados).toBe(referencia);
    expect(component.huevoItemsGuardados.length).toBe(2);
    expect(panel.textContent).toContain('Huevo primera');
    expect(panel.textContent).toContain('6,600');
    expect(panel.textContent).toContain('Huevo PNC');
  });

  it('al reabrir otro detalle vuelve a General', () => {
    component.seleccionarPestana('huevos');
    expect(component.pestanaActiva).toBe('huevos');

    fixture.componentRef.setInput('isOpen', false);
    fixture.detectChanges();
    fixture.componentRef.setInput('isOpen', true);
    fixture.detectChanges();

    expect(component.pestanaActiva).toBe('general');
    expect(obtenerDetalle).toHaveBeenCalledTimes(2);
  });
});
