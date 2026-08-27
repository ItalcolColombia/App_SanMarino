import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';

import { LoteEngordeListComponent } from './lote-engorde-list.component';
import { LoteAveEngordeDto, LoteFormDataResponse } from '../../services/lote-engorde.service';

/**
 * Reporte del usuario (26-ago-2026): al editar un lote de pollo engorde, el botón "Actualizar"
 * queda deshabilitado y no toma los cambios. Causa raíz: en una empresa con
 * `companies.programacion_lotes_engorde` ON, el subscriber de `granjaId` blanqueaba `loteNombre`
 * cada vez que su valor se PATCHEABA — incluida la precarga del form al abrir un lote para editar,
 * no solo cuando el usuario cambiaba de granja a mano. Como en ese modo el template no muestra ningún
 * input para `loteNombre` (se ve el select de lote base en su lugar), el campo quedaba requerido,
 * vacío y sin ningún control en pantalla para corregirlo: el form nacía inválido en TODA edición,
 * sin importar qué tan completo estuviera el resto del lote.
 */
describe('LoteEngordeListComponent', () => {
  let fixture: ComponentFixture<LoteEngordeListComponent>;
  let component: LoteEngordeListComponent;
  let httpMock: HttpTestingController;

  const formData: LoteFormDataResponse = {
    farms: [{ id: 1, name: 'Granja Test' }],
    nucleos: [{ nucleoId: 'N1', nucleoNombre: 'Nucleo 1', granjaId: 1 }],
    galpones: [{ galponId: 'G1', galponNombre: 'Galpon 1', nucleoId: 'N1', granjaId: 1 }],
    tecnicos: [],
    companies: [{ id: 1, name: 'Empresa Test' }],
    razas: ['Cobb 500', 'Ross 308']
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [LoteEngordeListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    fixture = TestBed.createComponent(LoteEngordeListComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);

    fixture.detectChanges(); // dispara ngOnInit → loadLotes() + loadLotesBase()
    httpMock.match(() => true).forEach(req => req.flush([]));
  });

  afterEach(() => httpMock.verify());

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  function abrirModalEditando(lote: LoteAveEngordeDto, anosPorRaza: number[]) {
    component.openModal(lote);
    httpMock.expectOne(req => req.url.endsWith('/LoteAveEngorde/form-data')).flush(formData);
    httpMock.expectOne(req => req.url.endsWith('/guia-genetica-ecuador/filters'))
      .flush({ razas: formData.razas, anos: anosPorRaza });
    if (lote.raza) {
      httpMock.expectOne(req => req.url.includes('/guia-genetica-ecuador/anos')).flush(anosPorRaza);
    }
  }

  describe('empresa con programación de lotes (companies.programacion_lotes_engorde = true)', () => {
    beforeEach(() => {
      component.programacionLotes = true;
      (component as any).aplicarProgramacionLotes();
    });

    it('editar un lote YA COMPLETO no debe invalidar loteNombre ni dejar el form inválido', () => {
      const loteCompleto = {
        loteAveEngordeId: 104, loteNombre: '2601', granjaId: 1,
        fechaEncaset: '2026-01-01', raza: 'Cobb 500', anoTablaGenetica: 2022,
        loteBaseEngordeId: 9
      } as unknown as LoteAveEngordeDto;
      component.lotesBase = [{ id: 9, nombre: '2601', activo: true, granjaIds: [1] } as any];

      abrirModalEditando(loteCompleto, [2022]);

      expect(component.form.get('loteNombre')!.value).toBe('2601');
      expect(component.form.invalid).toBeFalse();
    });

    it('un lote sin lote base asignado a su granja queda con un campo pendiente identificado, no en un dead-end mudo', () => {
      const lote = {
        loteAveEngordeId: 102, loteNombre: '2601', granjaId: 1,
        fechaEncaset: '2026-01-01', raza: 'Cobb 500', anoTablaGenetica: 2022,
        loteBaseEngordeId: null
      } as unknown as LoteAveEngordeDto;
      component.lotesBase = []; // ningún lote base asignado a la granja 1 todavía

      abrirModalEditando(lote, [2022]);

      expect(component.lotesBaseParaGranja.length).toBe(0);
      expect(component.form.invalid).toBeTrue();
      expect(component.camposQueFaltan).toEqual(['Lote base']);
    });

    it('en ALTA (sin editing), cambiar de granja sigue limpiando loteNombre para recalcular la corrida', () => {
      component.form.patchValue({ loteNombre: 'preview viejo' });
      expect(component.editing).toBeNull();

      component.form.get('granjaId')!.setValue(1);

      expect(component.form.get('loteNombre')!.value).toBe('');
    });
  });

  it('un lote legado sin raza/año nace inválido, y camposQueFaltan explica exactamente qué falta', () => {
    const loteLegado = {
      loteAveEngordeId: 100, loteNombre: 'Legado 1', granjaId: 1,
      fechaEncaset: '2026-01-01', raza: null, anoTablaGenetica: null
    } as unknown as LoteAveEngordeDto;

    abrirModalEditando(loteLegado, [2019, 2020, 2021, 2022]);

    expect(component.form.invalid).toBeTrue();
    expect(component.camposQueFaltan).toEqual(['Raza (guía Ecuador)', 'Año Tabla Genética']);
  });
});
