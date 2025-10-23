import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SeguimientoDiarioLoteReproductoraListComponent } from './seguimiento-diario-lote-reproductora-list.component';

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
});
