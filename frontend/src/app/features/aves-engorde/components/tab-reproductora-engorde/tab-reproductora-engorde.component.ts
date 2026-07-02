// Tab «R. Reproductora» — lotes de pollitos primera semana (lotes reproductoras).
// Orquestador delgado: carga lotes reproductora + seguimientos y delega TODO el
// cálculo a las funciones puras de ../../funciones. Solo lectura.
// Compartido por los paises que usan el modulo aves-engorde (Ecuador y Panama).
import { Component, Input, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import {
  LoteReproductoraAveEngordeService,
  LoteReproductoraAveEngordeDto
} from '../../../lote-reproductora-ave-engorde/services/lote-reproductora-ave-engorde.service';
import {
  SeguimientoDiarioLoteReproductoraService,
  SeguimientoLoteLevanteDto
} from '../../../seguimiento-diario-lote-reproductora/services/seguimiento-diario-lote-reproductora.service';

import { BloquePrimeraSemana, ResumenReproductora } from '../../models/reproductora-primera-semana.model';
import { construirBloquesReproductora } from '../../funciones/construir-bloques-reproductora.funcion';
import { calcularResumenVpi } from '../../funciones/calcular-resumen-vpi.funcion';

@Component({
  selector: 'app-tab-reproductora-engorde',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tab-reproductora-engorde.component.html',
  styleUrls: ['./tab-reproductora-engorde.component.scss']
})
export class TabReproductoraEngordeComponent implements OnChanges {
  @Input() loteAveEngordeId: number | null = null;

  /** Fase 2: mostrar columnas de guía genética (Consumo tabla / QQ tabla). */
  readonly mostrarGuiaGenetica = false;

  mostrarFormulas = false;
  cargando = false;
  error: string | null = null;
  bloques: BloquePrimeraSemana[] = [];
  resumen: ResumenReproductora | null = null;
  private cargadoParaLote: number | null = null;

  constructor(
    private lotesReproductoraSvc: LoteReproductoraAveEngordeService,
    private seguimientoSvc: SeguimientoDiarioLoteReproductoraService
  ) {}

  ngOnChanges(): void {
    if (this.loteAveEngordeId != null && this.cargadoParaLote !== this.loteAveEngordeId) {
      this.cargar();
    }
  }

  recargar(): void {
    this.cargadoParaLote = null;
    if (this.loteAveEngordeId != null) this.cargar();
  }

  private cargar(): void {
    const loteId = this.loteAveEngordeId!;
    this.cargando = true;
    this.error = null;
    this.bloques = [];
    this.resumen = null;

    this.lotesReproductoraSvc.getAll(loteId).subscribe({
      next: lotes => {
        const lista: LoteReproductoraAveEngordeDto[] = [...(lotes ?? [])];
        if (lista.length === 0) {
          this.cargando = false;
          this.cargadoParaLote = loteId;
          return;
        }
        forkJoin(
          lista.map(l =>
            this.seguimientoSvc
              .getByLoteReproductoraId(l.id)
              .pipe(catchError(() => of([] as SeguimientoLoteLevanteDto[])))
          )
        ).subscribe({
          next: seguimientosPorLote => {
            const insumos = lista.map((lote, i) => ({
              lote,
              seguimientos: seguimientosPorLote[i] ?? []
            }));
            this.bloques = construirBloquesReproductora(insumos);
            this.resumen = calcularResumenVpi(this.bloques);
            this.cargando = false;
            this.cargadoParaLote = loteId;
          },
          error: () => {
            this.error = 'No se pudieron cargar los seguimientos de los lotes reproductora.';
            this.cargando = false;
          }
        });
      },
      error: () => {
        this.error = 'No se pudieron cargar los lotes reproductora.';
        this.cargando = false;
      }
    });
  }

  trackByBloque = (_: number, b: BloquePrimeraSemana) => `${b.loteReproductoraId}-${b.sexo}`;

  formatNumber(value: number | null | undefined): string {
    if (value == null) return '—';
    return value.toLocaleString('es-CO', { minimumFractionDigits: 0, maximumFractionDigits: 0 });
  }

  formatDec(value: number | null | undefined, dec = 2): string {
    if (value == null) return '—';
    return value.toLocaleString('es-CO', { minimumFractionDigits: dec, maximumFractionDigits: dec });
  }

  formatPct(value: number | null | undefined): string {
    if (value == null) return '—';
    return `${value.toLocaleString('es-CO', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}%`;
  }

  /** Fecha ISO → dd/MM/yyyy usando solo la parte YYYY-MM-DD (sin corrimiento de zona horaria). */
  formatFechaUtc(iso: string | null): string {
    const ymd = (iso ?? '').slice(0, 10);
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(ymd);
    return m ? `${m[3]}/${m[2]}/${m[1]}` : '—';
  }
}
