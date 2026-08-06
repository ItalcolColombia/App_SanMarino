import {
  ChangeDetectionStrategy, Component, Input, OnChanges, SimpleChanges, inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { catchError, finalize, of } from 'rxjs';

import { TrasladosAvesService } from '../../services/traslados-aves.service';
import { CohortesLoteDto, FilaEdadLote } from '../../models/cohorte-lote.model';
import { construirFilasEdadesLote } from '../../funciones/construir-filas-edades-lote.funcion';

/**
 * Bloque "Edades en el lote" (Fase 3 — cohortes).
 *
 * Muestra las aves PROPIAS del lote (edad desde su encasetamiento) y una fila por cada
 * cohorte recibida por traslado desde otro lote, que conserva la edad de su lote origen.
 *
 * Reutilizable: se monta igual en seguimiento diario de Levante, de Producción y en pollo Engorde.
 * - `linea` elige el endpoint: `postura` (lote base de `lotes`) o `engorde` (`lote_ave_engorde`).
 *   Las dos devuelven el MISMO DTO, así que el render y el armado de filas son idénticos.
 * - Con 0 cohortes queda como una línea informativa → se puede mostrar SIEMPRE, sin flag.
 * - `loteId` nulo ⇒ NO hace ninguna llamada HTTP.
 * - Las edades las calcula el BACKEND; acá no se recalcula ninguna.
 *
 * Orquestador delgado: el armado de filas vive en `funciones/construir-filas-edades-lote.funcion.ts`
 * y el resultado se memoiza en `filas` (referencia estable: nada de getters que alocan por ciclo).
 */
@Component({
  selector: 'app-edades-lote',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './edades-lote.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./edades-lote.component.scss']
})
export class EdadesLoteComponent implements OnChanges {

  /**
   * ID del lote receptor. En `postura` es el lote BASE (tabla `lotes`); en `engorde` es el
   * `loteAveEngordeId`. Null ⇒ el bloque no se muestra ni consulta.
   */
  @Input() loteId: number | null = null;

  /** Línea del lote: define contra qué endpoint de cohortes se consulta. */
  @Input() linea: 'postura' | 'engorde' = 'postura';

  /**
   * Cambiá este valor (p. ej. `refresh++`) para forzar una recarga sin cambiar de lote
   * — se usa tras ejecutar un traslado desde el modal.
   */
  @Input() refreshTrigger = 0;

  private readonly trasladoSvc = inject(TrasladosAvesService);

  /** Filas memoizadas (aves propias + cohortes). Referencia estable entre ciclos de CD. */
  filas: FilaEdadLote[] = [];
  /** Cantidad de cohortes recibidas (0 ⇒ hint discreto en vez de tabla ancha). */
  cantidadCohortes = 0;
  loteNombre = '';
  cargando = false;
  error: string | null = null;

  /** Evita que una respuesta vieja pise a una nueva (cambio rápido de lote). */
  private peticionSeq = 0;

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['loteId'] && !changes['refreshTrigger'] && !changes['linea']) return;
    this.cargar();
  }

  private cargar(): void {
    const loteId = this.loteId;
    this.error = null;

    if (loteId == null) {
      this.filas = [];
      this.cantidadCohortes = 0;
      this.loteNombre = '';
      this.cargando = false;
      return;
    }

    const seq = ++this.peticionSeq;
    this.cargando = true;

    const peticion$ = this.linea === 'engorde'
      ? this.trasladoSvc.getCohortesLoteEngorde(loteId)
      : this.trasladoSvc.getCohortesLote(loteId);

    peticion$
      .pipe(
        catchError(() => of(null as CohortesLoteDto | null)),
        finalize(() => { if (seq === this.peticionSeq) this.cargando = false; })
      )
      .subscribe(dto => {
        if (seq !== this.peticionSeq) return;   // respuesta obsoleta
        if (!dto) {
          this.filas = [];
          this.cantidadCohortes = 0;
          this.loteNombre = '';
          this.error = 'No se pudieron cargar las edades del lote.';
          return;
        }
        this.filas = construirFilasEdadesLote(dto);
        this.cantidadCohortes = dto.cohortes?.length ?? 0;
        this.loteNombre = (dto.loteNombre ?? '').trim();
      });
  }
}
