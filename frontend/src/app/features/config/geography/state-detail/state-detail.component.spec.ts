import { ComponentFixture, TestBed } from '@angular/core/testing';

import { StateDetailComponent } from './state-detail.component';

describe('StateDetailComponent', () => {
  let component: StateDetailComponent;
  let fixture: ComponentFixture<StateDetailComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StateDetailComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(StateDetailComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
