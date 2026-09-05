import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SeguimientoDiarioLoteReproductoraListComponent } from './seguimiento-diario-lote-reproductora-list.component';
import { SeguimientoLoteLevanteDto } from '../../services/seguimiento-diario-lote-reproductora.service';
import { LoteReproductoraAveEngordeDto } from '../../../lote-reproductora-ave-engorde/services/lote-reproductora-ave-engorde.service';

describe('SeguimientoDiarioLoteReproductoraListComponent', () => {
  let component: SeguimientoDiarioLoteReproductoraListComponent;
  let fixture: ComponentFixture<SeguimientoDiarioLoteReproductoraListComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SeguimientoDiarioLoteReproductoraListComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SeguimientoDiarioLoteReproductoraListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  /**
   * Reporte 04-sep-2026 — granja DONA MARIA, lote reproductora `LR-0023649715` «156».
   * Datos reales de la copia de prod: encaset 30/08/2026 **sin hora** (ni el lote reproductora ni su
   * lote de engorde la traen) y tres registros que arrancan al dia siguiente. Salian como
   * «Dia 2, 3 y 4» porque la numeracion solo se corria con hora >= 13:00.
   */
  describe('columna «Dia»: el primer dia CON registro es el dia 1', () => {
    function cargarLote(hora: string | null, fechasRegistro: string[]): void {
      component.selectedReproductoraDetail = {
        fechaEncasetamiento: '2026-08-30T12:00:00Z',
        horaEncasetamiento: hora,
        horaEncasetamientoEfectiva: hora
      } as unknown as LoteReproductoraAveEngordeDto;
      component.seguimientos = fechasRegistro.map((f, i) => ({
        id: i + 1,
        fechaRegistro: f
      }) as unknown as SeguimientoLoteLevanteDto);
    }

    function diasMostrados(): Array<number | null> {
      return component.seguimientos.map(s => component.calcularEdad(s.fechaRegistro));
    }

    it('lote reportado (sin hora, arranca el 31/08) ⇒ dias 1, 2 y 3', () => {
      cargarLote(null, ['2026-08-31T12:00:00Z', '2026-09-01T12:00:00Z', '2026-09-02T12:00:00Z']);

      expect(diasMostrados()).toEqual([1, 2, 3]);
    });

    it('lote que capturo el dia del encasetamiento ⇒ ese dia sigue siendo el 1 (sin regresion)', () => {
      cargarLote(null, ['2026-08-30T12:00:00Z', '2026-08-31T12:00:00Z', '2026-09-01T12:00:00Z']);

      expect(diasMostrados()).toEqual([1, 2, 3]);
    });

    it('lote tardio que capturo igual el dia del encaset ⇒ conserva su semana 1..7', () => {
      cargarLote('21:33', ['2026-08-30T12:00:00Z', '2026-08-31T12:00:00Z']);

      expect(diasMostrados()).toEqual([1, 2]);
    });

    it('hueco de 3 dias al arrancar ⇒ el tope de 1 dia lo deja a la vista', () => {
      cargarLote(null, ['2026-09-02T12:00:00Z', '2026-09-03T12:00:00Z']);

      expect(diasMostrados()).toEqual([3, 4]);
    });

    it('sin registros la fecha sugerida sigue saliendo de la hora (guarda intacto)', () => {
      cargarLote(null, []);
      expect(component.nextSuggestedFecha).toBe('2026-08-30');

      cargarLote('21:33', []);
      expect(component.nextSuggestedFecha).toBe('2026-08-31');
    });
  });
});
