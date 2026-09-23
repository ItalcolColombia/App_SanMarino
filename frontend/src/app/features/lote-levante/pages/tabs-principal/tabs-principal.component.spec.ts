import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BehaviorSubject, of } from 'rxjs';

import { TokenStorageService } from '../../../../core/auth/token-storage.service';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';
import { LotePosturaLevanteDto } from '../../../lote/services/lote-postura-levante.service';
import { SeguimientoLoteLevanteDto } from '../../services/seguimiento-lote-levante.service';
import { TabsPrincipalComponent } from './tabs-principal.component';

describe('TabsPrincipalComponent (levante) · tabla y validación', () => {
  let fixture: ComponentFixture<TabsPrincipalComponent>;
  let component: TabsPrincipalComponent;

  const lote = {
    id: 7,
    loteId: 7,
    loteNombre: 'LEV-101',
    fechaEncaset: '2026-01-05',
    hembrasL: 1000,
    machosL: 100
  } as unknown as LotePosturaLevanteDto;

  const seguimiento = {
    id: 101,
    loteId: '7',
    fechaRegistro: '2026-01-12',
    mortalidadHembras: 1,
    mortalidadMachos: 0,
    selH: 0,
    selM: 0,
    errorSexajeHembras: 0,
    errorSexajeMachos: 0,
    consumoKgHembras: 10,
    consumoKgMachos: 1,
    tipoAlimento: 'INICIO',
    ciclo: 'Normal'
  } as SeguimientoLoteLevanteDto;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TabsPrincipalComponent],
      providers: [
        { provide: TokenStorageService, useValue: { get: () => null, session$: new BehaviorSubject(null) } },
        { provide: ActiveCompanyConfigService, useValue: { getFlags: () => of({ capturaHuevosEnLevante: true, ocultaMachosEnPostura: false }) } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TabsPrincipalComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('selectedLote', lote);
    fixture.componentRef.setInput('seguimientos', [seguimiento]);
    fixture.detectChanges();
  });

  it('marca una fila pendiente con el estado neutro', () => {
    fixture.componentRef.setInput('requiereValidacion', true);
    fixture.componentRef.setInput('estadoValidacionPorId', new Map([[101, 'PENDIENTE']]));
    fixture.detectChanges();

    const fila: HTMLElement = fixture.nativeElement.querySelector('table.ux-table--registro-diario tbody tr.ux-row');
    expect(fila.classList).toContain('fila-validacion--pendiente');
    expect(fila.classList).not.toContain('fila-validacion--retraso');
  });

  it('mantiene el retraso como alarma diferenciada', () => {
    fixture.componentRef.setInput('requiereValidacion', true);
    fixture.componentRef.setInput('estadoValidacionPorId', new Map([[101, 'EN_RETRASO']]));
    fixture.detectChanges();

    const fila: HTMLElement = fixture.nativeElement.querySelector('table.ux-table--registro-diario tbody tr.ux-row');
    expect(fila.classList).toContain('fila-validacion--retraso');
    expect(fixture.nativeElement.querySelector('.badge-validacion--retraso').textContent).toContain('En retraso');
  });

  it('sincroniza la barra horizontal superior y la tabla en ambos sentidos', () => {
    const superior: HTMLDivElement = fixture.nativeElement.querySelector('.table-scrollbar-top');
    const tabla: HTMLDivElement = fixture.nativeElement.querySelector('.ux-scroll--diario');

    superior.scrollLeft = 230;
    superior.dispatchEvent(new Event('scroll'));
    expect(tabla.scrollLeft).toBe(230);

    tabla.scrollLeft = 90;
    tabla.dispatchEvent(new Event('scroll'));
    expect(superior.scrollLeft).toBe(90);
  });

  it('muestra Validar con texto y conserva el evento existente', () => {
    fixture.componentRef.setInput('requiereValidacion', true);
    fixture.componentRef.setInput('puedeValidar', true);
    fixture.componentRef.setInput('estadoValidacionPorId', new Map([[101, 'PENDIENTE']]));
    const emitSpy = spyOn(component.validar, 'emit');
    fixture.detectChanges();

    const boton: HTMLButtonElement = fixture.nativeElement.querySelector('.icon-btn--validar');
    expect(boton.textContent?.trim()).toContain('Validar');
    boton.click();
    expect(emitSpy).toHaveBeenCalledOnceWith(101);
  });

  it('mantiene Acciones fija y al final de la tabla', () => {
    const tabla: HTMLTableElement = fixture.nativeElement.querySelector('table.ux-table--registro-diario');
    const encabezados = Array.from(tabla.querySelectorAll('thead th'));
    const acciones = encabezados.at(-1) as HTMLElement;
    const celdaAcciones = tabla.querySelector('tbody tr.ux-row td:last-child') as HTMLElement;

    expect(acciones.textContent?.trim()).toBe('Acciones');
    expect(acciones.classList).toContain('sticky-actions');
    expect(celdaAcciones.classList).toContain('sticky-actions');
  });
});
