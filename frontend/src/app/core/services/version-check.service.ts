import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

import { BUILD_ID } from '../build-info';

/**
 * Detecta que se desplegó una versión nueva del front y recarga la página.
 *
 * Resuelve el problema de que un usuario con sesión activa se quede con el bundle
 * viejo después de un despliegue y empiece a hablarle mal al backend nuevo.
 *
 * ## Por qué ya no lee index.html
 *
 * La versión anterior descargaba `/index.html` cada 5 minutos y le sacaba el
 * timestamp a un `<meta name="app-version">` que `scripts/inject-version.js`
 * escribía **después** de `ng build`. Esa mutación post-build invalida el SHA1 que
 * el Service Worker guarda en `ngsw.json` para index.html, y el SW arranca en safe
 * mode y se desactiva solo sin avisar (ver `scripts/build-version.js`).
 *
 * Ahora:
 *  - la versión propia (`BUILD_ID`) viene **compilada dentro del bundle**, así que es
 *    exactamente la del código que está corriendo;
 *  - la versión publicada se consulta en `/version.json`, un archivo chico que nginx
 *    sirve con `no-cache` y que **nunca** entra en la tabla de hashes del SW.
 *
 * En desarrollo local `BUILD_ID` vale `'dev'` y el chequeo queda apagado.
 */
@Injectable({
  providedIn: 'root'
})
export class VersionCheckService {
  private readonly CHECK_INTERVAL = 5 * 60 * 1000; // Cada 5 minutos
  private readonly VERSION_JSON_PATH = '/version.json';

  private readonly http = inject(HttpClient);

  /** Versión con la que se compiló este bundle. Es la referencia de comparación. */
  private readonly currentVersion = BUILD_ID;

  private checkInterval: ReturnType<typeof setInterval> | null = null;

  /** En un build local no hay versión publicada contra la cual comparar. */
  private get habilitado(): boolean {
    return this.currentVersion !== 'dev';
  }

  /**
   * Arranca el chequeo periódico de versión.
   */
  startVersionChecking(): void {
    if (!this.habilitado) {
      return;
    }

    // Chequeo inmediato al arrancar
    this.checkVersion();

    // Y después, periódico
    this.checkInterval = setInterval(() => {
      this.checkVersion();
    }, this.CHECK_INTERVAL);
  }

  /**
   * Detiene el chequeo periódico.
   */
  stopVersionChecking(): void {
    if (this.checkInterval) {
      clearInterval(this.checkInterval);
      this.checkInterval = null;
    }
  }

  /**
   * Consulta la versión publicada y recarga si difiere de la compilada.
   */
  private checkVersion(): void {
    this.fetchPublishedVersion().subscribe(publicada => {
      if (publicada && publicada !== this.currentVersion) {
        this.handleNewVersion();
      }
    });
  }

  /**
   * Baja `/version.json` con cache-busting. Devuelve `null` si no se pudo leer
   * (sin red, 404, JSON inválido): sin dato no se toma ninguna decisión.
   */
  private fetchPublishedVersion(): Observable<string | null> {
    const cacheBuster = `?v=${Date.now()}`;

    return this.http.get<{ buildId?: string }>(this.VERSION_JSON_PATH + cacheBuster, {
      headers: {
        'Cache-Control': 'no-cache',
        'Pragma': 'no-cache'
      }
    }).pipe(
      map(res => (res && typeof res.buildId === 'string' && res.buildId ? res.buildId : null)),
      catchError(error => {
        console.warn('Version check failed:', error);
        return of(null);
      })
    );
  }

  /**
   * Hay versión nueva: recarga completa para bajar el bundle actualizado.
   */
  private handleNewVersion(): void {
    // Dejar de chequear para no encadenar recargas
    this.stopVersionChecking();

    // Un instante de margen para cualquier limpieza pendiente, y recarga
    setTimeout(() => {
      window.location.reload();
    }, 1000);
  }

  /**
   * Chequeo manual (útil para pruebas o un botón de "buscar actualizaciones").
   */
  checkForUpdates(): Observable<boolean> {
    if (!this.habilitado) {
      return of(false);
    }

    return this.fetchPublishedVersion().pipe(
      map(publicada => {
        if (publicada && publicada !== this.currentVersion) {
          this.handleNewVersion();
          return true;
        }
        return false;
      })
    );
  }
}
