import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { CityDetailComponent } from './city-detail.component';

describe('CityDetailComponent', () => {
  let component: CityDetailComponent;
  let fixture: ComponentFixture<CityDetailComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CityDetailComponent],
      // El componente inyecta ActivatedRoute y hace HTTP en ngOnInit; sin estos providers
      // el spec revienta con NG0201. No fallaba antes porque el harness no compilaba specs.
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(CityDetailComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
