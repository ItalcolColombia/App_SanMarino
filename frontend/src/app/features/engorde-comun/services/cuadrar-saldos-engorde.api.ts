import { Observable } from 'rxjs';
import {
  FilaExcelCuadrarSaldosDto,
  AccionCorreccionCuadrarSaldosDto,
  CuadrarSaldosValidarResponseDto,
  CuadrarSaldosAplicarResponseDto
} from '../models/cuadrar-saldos-engorde.models';

/**
 * Contrato multi-país del flujo "Cuadrar saldos" de engorde.
 * Cada país provee su implementación real vía DI:
 *   providers: [{ provide: CuadrarSaldosEngordeApi, useExisting: SeguimientoAvesEngordeService }]
 * (Colombia) o `useExisting: SeguimientoAvesEngordePanamaService` (Panamá).
 */
export abstract class CuadrarSaldosEngordeApi {
  abstract cuadrarSaldosValidar(
    loteId: number,
    filasExcel: FilaExcelCuadrarSaldosDto[]
  ): Observable<CuadrarSaldosValidarResponseDto>;

  abstract cuadrarSaldosAplicar(
    loteId: number,
    acciones: AccionCorreccionCuadrarSaldosDto[],
    filasExcel?: FilaExcelCuadrarSaldosDto[]
  ): Observable<CuadrarSaldosAplicarResponseDto>;
}
