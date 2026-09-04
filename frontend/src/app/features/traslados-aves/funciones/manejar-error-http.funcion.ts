// src/app/features/traslados-aves/funciones/manejar-error-http.funcion.ts
// Traduccion de un HttpErrorResponse al mensaje que ve el usuario.
//
// Funcion PURA (sin `this`, sin DI): la compartian los 3 dominios que vivian dentro de
// `TrasladosAvesService` y al partirlo habria quedado copiada 3 veces. Los mensajes son los
// mismos de siempre, incluido el 409 ("No hay suficientes aves para el traslado"), que es el
// que el operario ve cuando el backend rechaza por stock.
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';

export function manejarErrorHttp(error: HttpErrorResponse, origen = 'TrasladosAvesService'): Observable<never> {
  let errorMessage = 'Error desconocido';

  if (error.error instanceof ErrorEvent) {
    errorMessage = `Error: ${error.error.message}`;
  } else {
    switch (error.status) {
      case 400:
        errorMessage = 'Datos inválidos en la solicitud';
        break;
      case 401:
        errorMessage = 'No autorizado. Inicie sesión nuevamente';
        break;
      case 404:
        errorMessage = 'Recurso no encontrado';
        break;
      case 409:
        errorMessage = 'Conflicto: No hay suficientes aves para el traslado';
        break;
      case 500:
        errorMessage = 'Error interno del servidor';
        break;
      default:
        errorMessage = `Error ${error.status}: ${error.message}`;
    }
  }

  console.error(`Error en ${origen}:`, error);
  return throwError(() => new Error(errorMessage));
}
