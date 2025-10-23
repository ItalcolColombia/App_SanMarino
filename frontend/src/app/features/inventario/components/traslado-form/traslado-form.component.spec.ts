import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TrasladoFormComponent } from './traslado-form.component';

describe('TrasladoFormComponent', () => {
  let component: TrasladoFormComponent;
  let fixture: ComponentFixture<TrasladoFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TrasladoFormComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(TrasladoFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
