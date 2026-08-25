import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { TabsPrincipalComponent } from './tabs-principal.component';
import { SeguimientoItemDto } from '../../services/produccion.service';
import { LoteDto } from '../../../lote/services/lote.service';

/**
 * La grilla «Registros Diarios» tiene columnas gateadas por flag. Si una columna se declara en el
 * encabezado y no en el cuerpo (o al revés), la tabla sale corrida y el header cae sobre otra celda.
 * Pasó de verdad: la columna «Estado» de la doble validación vivía sólo en el `<thead>`.
 */
describe('TabsPrincipalComponent (producción) · alineación de la grilla diaria', () => {
  let fixture: ComponentFixture<TabsPrincipalComponent>;
  let component: TabsPrincipalComponent;

  const lote = {
    loteId: 7,
    loteNombre: 'A999',
    fechaEncaset: '2026-01-05'
  } as unknown as LoteDto;

  const seguimiento = {
    id: 101,
    fechaRegistro: '2026-08-20',
    mortalidadH: 1,
    mortalidadM: 0,
    selH: 0,
    selM: 0,
    consKgH: 10,
    consKgM: 0,
    huevoTot: 100,
    pesoHuevo: 62.5,
    observacionesPesaje: 'ok'
  } as unknown as SeguimientoItemDto;

  /** [th del encabezado, td de la fila de datos] tal como quedan RENDERIZADOS. */
  function anchos(): [number, number] {
    const tabla: HTMLTableElement = fixture.nativeElement.querySelector('table.ux-table--seguimiento');
    expect(tabla).withContext('la tabla de registros diarios debe estar en el DOM').toBeTruthy();
    const encabezado = Array.from(tabla.querySelectorAll('thead tr th')).length;
    const fila = tabla.querySelector('tbody tr.ux-row')!;
    const celdas = Array.from(fila.children).filter(el => el.tagName === 'TD').length;
    return [encabezado, celdas];
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TabsPrincipalComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();

    fixture = TestBed.createComponent(TabsPrincipalComponent);
    component = fixture.componentInstance;
    component.selectedLote = lote;
    component.seguimientos = [seguimiento];
    fixture.detectChanges();
  });

  it('sin doble validación: el encabezado y la fila tienen el mismo ancho', () => {
    const [th, td] = anchos();
    expect(th).toBe(td);
  });

  it('con doble validación: el encabezado y la fila siguen teniendo el mismo ancho', () => {
    component.requiereValidacion = true;
    component.estadoValidacionPorId = new Map<number, string>([[101, 'PENDIENTE']]);
    fixture.detectChanges();

    const [th, td] = anchos();
    expect(th).toBe(td);
  });

  it('con doble validación aparece exactamente una columna más que sin ella', () => {
    const [thSin, tdSin] = anchos();

    component.requiereValidacion = true;
    component.estadoValidacionPorId = new Map<number, string>([[101, 'PENDIENTE']]);
    fixture.detectChanges();

    const [thCon, tdCon] = anchos();
    expect(thCon - thSin).toBe(1);
    expect(tdCon - tdSin).toBe(1);
  });

  it('la celda de Estado pinta el badge del estado que le pasa el contenedor', () => {
    component.requiereValidacion = true;
    component.estadoValidacionPorId = new Map<number, string>([[101, 'EN_RETRASO']]);
    fixture.detectChanges();

    const badge: HTMLElement = fixture.nativeElement.querySelector(
      'table.ux-table--seguimiento tbody tr.ux-row .badge-validacion'
    );
    expect(badge).toBeTruthy();
    expect(badge.classList).toContain('badge-validacion--retraso');
    expect(badge.textContent!.trim()).toContain('En retraso');
  });

  it('con doble validación cada encabezado sigue cayendo sobre su propia celda', () => {
    component.requiereValidacion = true;
    component.estadoValidacionPorId = new Map<number, string>([[101, 'PENDIENTE']]);
    fixture.detectChanges();

    const tabla: HTMLTableElement = fixture.nativeElement.querySelector('table.ux-table--seguimiento');
    const encabezados = Array.from(tabla.querySelectorAll('thead tr th'));
    const fila = tabla.querySelector('tbody tr.ux-row')!;
    const celdas = Array.from(fila.children).filter(el => el.tagName === 'TD');

    // La primera columna es el ID del registro: si la fila estuviera corrida, no coincidiría.
    expect(encabezados[0].textContent!.trim()).toBe('ID');
    expect(celdas[0].textContent!.trim()).toBe('101');

    // Y las dos últimas son justamente las que se pisaban: Estado y Acciones.
    const ultimo = encabezados.length - 1;
    expect(encabezados[ultimo - 1].textContent!.trim()).toBe('Estado');
    expect(celdas[ultimo - 1].querySelector('.badge-validacion')).withContext('la celda bajo «Estado» es el badge').toBeTruthy();

    expect(encabezados[ultimo].textContent!.trim()).toBe('Acciones');
    expect(celdas[ultimo].querySelector('.btn-group')).withContext('la celda bajo «Acciones» son los botones').toBeTruthy();
  });
});
