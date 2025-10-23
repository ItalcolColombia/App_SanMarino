import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SeguimientoCalculosComponent } from './seguimiento-calculos.component';

describe('SeguimientoCalculosComponent', () => {
  let component: SeguimientoCalculosComponent;
  let fixture: ComponentFixture<SeguimientoCalculosComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SeguimientoCalculosComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(SeguimientoCalculosComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
