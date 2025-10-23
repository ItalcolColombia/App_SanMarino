import { ComponentFixture, TestBed } from '@angular/core/testing';

import { InventarioTabsComponent } from './inventario-tabs.component';

describe('InventarioTabsComponent', () => {
  let component: InventarioTabsComponent;
  let fixture: ComponentFixture<InventarioTabsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InventarioTabsComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(InventarioTabsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
