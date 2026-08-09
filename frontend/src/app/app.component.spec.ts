import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideServiceWorker } from '@angular/service-worker';

import { AppComponent } from './app.component';

/**
 * Este archivo venía del scaffold de Angular CLI y afirmaba cosas que esta app nunca
 * tuvo: una propiedad `title` con el valor 'frontend' y un `<h1>Hello, frontend</h1>`.
 * No fallaba porque el harness de tests estaba roto y no compilaba ningún spec
 * (`tsconfig.spec.json` heredaba `exclude: ["**\/*.spec.ts"]`), así que nadie lo vio.
 * Reemplazado por un smoke real del componente raíz.
 */
describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        // AppComponent inyecta PwaActualizacionService, que a su vez inyecta SwUpdate.
        // `enabled: false` da la instancia con `isEnabled === false` sin registrar nada:
        // es lo mismo que ocurre en el dev server, así que el componente toma el camino
        // del fallback por version.json y no toca el navegador.
        provideServiceWorker('ngsw-worker.js', { enabled: false })
      ]
    }).compileComponents();
  });

  it('se crea', () => {
    const fixture = TestBed.createComponent(AppComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('arranca con el sidebar cerrado y alterna al togglear', () => {
    const app = TestBed.createComponent(AppComponent).componentInstance;

    expect(app.sidebarOpen).toBeFalse();

    app.toggleSidebar();
    expect(app.sidebarOpen).toBeTrue();

    app.closeSidebar();
    expect(app.sidebarOpen).toBeFalse();
  });

  it('oculta el sidebar en las rutas públicas', () => {
    const app = TestBed.createComponent(AppComponent).componentInstance;
    const urlSpy = spyOnProperty(app.router, 'url', 'get');

    urlSpy.and.returnValue('/login');
    expect(app.showSidebar).toBeFalse();

    urlSpy.and.returnValue('/password-recovery');
    expect(app.showSidebar).toBeFalse();
  });

  it('muestra el sidebar en las rutas protegidas', () => {
    const app = TestBed.createComponent(AppComponent).componentInstance;
    spyOnProperty(app.router, 'url', 'get').and.returnValue('/lotes');

    expect(app.showSidebar).toBeTrue();
  });
});
