import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { MasterListService } from '../../../core/services/master-list/master-list.service';
import { unirOpcionesEmpresaVenta } from '../funciones/empresa-venta.funcion';

/**
 * Key de la lista maestra con las «Empresas de venta» del despacho de pollo engorde.
 *
 * ⚠️ Tiene que coincidir con la que siembra la migración
 * `20260919120000_SeedListaMaestraEmpresaVentaEngorde` (una lista por empresa y país, con «Planta» por defecto).
 * Una empresa nueva no la tiene hasta que se cree con esta key desde Config → Listas maestras (o por migración).
 */
export const EMPRESA_VENTA_ENGORDE_KEY = 'venta_pollo_engorde_empresa';

/**
 * Opciones del campo «Empresa de venta» (a quién se vendió o se envió el despacho) de la empresa y país activos.
 * Son parametrizables desde Listas maestras; nada de esto vive en el código.
 */
@Injectable({ providedIn: 'root' })
export class EmpresaVentaEngordeService {
  private readonly masterLists = inject(MasterListService);

  /**
   * Textos de la lista, en su orden, sin repetidos ni vacíos. **Fail-closed:** si la lista no existe para la
   * empresa (404), no tiene opciones o la petición falla, devuelve `[]` y la pantalla NO dibuja el campo ni el
   * filtro (queda igual que antes de este campo). Se pide en cada apertura: es una lista de pocos elementos y así
   * una opción recién agregada por un administrador aparece sin recargar la aplicación.
   */
  opciones(): Observable<string[]> {
    return this.masterLists.getByKey(EMPRESA_VENTA_ENGORDE_KEY).pipe(
      map((lista) =>
        unirOpcionesEmpresaVenta(lista?.optionValues ?? (lista?.options ?? []).map((o) => o?.value))
      ),
      catchError(() => of([] as string[]))
    );
  }
}
