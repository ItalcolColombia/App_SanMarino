import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeguimientoItemDto, ProduccionService, IndicadorProduccionSemanalDto, IndicadoresProduccionResponse, IndicadoresProduccionRequest } from '../../services/produccion.service';
import { LoteDto } from '../../../lote/services/lote.service';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-tabla-lista-indicadores',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-lista-indicadores.component.html',
  styleUrls: ['./tabla-lista-indicadores.component.scss']
})
export class TablaListaIndicadoresComponent implements OnInit, OnChanges {
  @Input() seguimientos: SeguimientoItemDto[] = [];
  @Input() selectedLote: LoteDto | null = null;
  @Input() loading: boolean = false;

  indicadoresSemanales: IndicadorProduccionSemanalDto[] = [];
  loadingIndicadores = false;
  tieneDatosGuiaGenetica = false;
  error: string | null = null;
  expanded = new Set<number>(); // semanas expandidas para detalle clasificadora

  constructor(private produccionService: ProduccionService) { }

  ngOnInit(): void {
    this.cargarIndicadores();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['selectedLote'] || changes['seguimientos']) {
      this.cargarIndicadores();
    }
  }

  cargarIndicadores(): void {
    // Validar que tenemos un lote seleccionado con loteId
    if (!this.selectedLote || !this.selectedLote.loteId) {
      this.indicadoresSemanales = [];
      this.error = null;
      return;
    }

    this.loadingIndicadores = true;
    this.error = null;

    // Preparar request para el backend
    // El backend recibirá el loteId y buscará todos los registros de producción_diaria
    // Luego los agrupará por semana y calculará los indicadores
    const request: IndicadoresProduccionRequest = {
      loteId: this.selectedLote.loteId,
      semanaDesde: 26, // Solo desde semana 26 (producción)
      semanaHasta: null, // Sin límite superior
      fechaDesde: null,
      fechaHasta: null
    };

    // Llamar al servicio que invoca el API
    // El backend procesará:
    // 1. Buscar todos los seguimientos de producción_diaria para este lote
    // 2. Agruparlos por semana
    // 3. Calcular métricas por semana
    // 4. Comparar con guía genética si está disponible
    // 5. Retornar todos los indicadores calculados
    this.produccionService.obtenerIndicadoresSemanales(request)
      .pipe(
        finalize(() => {
          this.loadingIndicadores = false;
        })
      )
      .subscribe({
        next: (response: IndicadoresProduccionResponse) => {
          // El backend ya calculó todos los indicadores semanales
          // Solo los asignamos para mostrar en la tabla
          this.indicadoresSemanales = response.indicadores || [];
          this.tieneDatosGuiaGenetica = response.tieneDatosGuiaGenetica || false;
          this.error = null;

          console.log(`✅ Indicadores cargados: ${this.indicadoresSemanales.length} semanas`, response);
        },
        error: (err) => {
          console.error('❌ Error al cargar indicadores semanales:', err);
          this.error = err.error?.message || 'Error al cargar indicadores semanales. Por favor, intenta de nuevo.';
          this.indicadoresSemanales = [];
          this.tieneDatosGuiaGenetica = false;
        }
      });
  }

  // ================== HELPERS ==================

  formatearPorcentaje(valor?: number | null): string {
    if (valor === null || valor === undefined) return '—';
    const signo = valor > 0 ? '+' : '';
    return `${signo}${valor.toFixed(2)}%`;
  }

  getDiferenciaClass(diferencia?: number | null): string {
    if (diferencia === null || diferencia === undefined) return '';
    const abs = Math.abs(diferencia);
    if (abs <= 5) return 'diferencia-ok';
    if (abs <= 15) return 'diferencia-warning';
    return 'diferencia-danger';
  }

  getEstadoCumplimiento(diferencia?: number | null): { clase: string, texto: string } {
    if (diferencia === null || diferencia === undefined) {
      return { clase: 'estado-info', texto: 'Sin datos' };
    }
    const absDiferencia = Math.abs(diferencia);
    if (absDiferencia <= 5) {
      return { clase: 'estado-ok', texto: 'Óptimo' };
    } else if (absDiferencia <= 15) {
      return { clase: 'estado-aceptable', texto: 'Aceptable' };
    } else {
      return { clase: 'estado-problema', texto: 'Requiere atención' };
    }
  }

  toggleExpanded(semana: number): void {
    if (this.expanded.has(semana)) this.expanded.delete(semana);
    else this.expanded.add(semana);
  }

  isExpanded(semana: number): boolean {
    return this.expanded.has(semana);
  }

  // Consumo real g/ave/día (para mostrar siempre, con fallback seguro)
  consumoRealGrAveDiaH(ind: IndicadorProduccionSemanalDto): number | null {
    const aves = ind.avesHembrasInicioSemana || 0;
    const dias = ind.totalRegistros || 0;
    if (!aves || !dias) return null;
    return Number((ind.consumoKgHembras * 1000) / (dias * aves));
  }

  consumoRealGrAveDiaM(ind: IndicadorProduccionSemanalDto): number | null {
    const aves = ind.avesMachosInicioSemana || 0;
    const dias = ind.totalRegistros || 0;
    if (!aves || !dias) return null;
    return Number((ind.consumoKgMachos * 1000) / (dias * aves));
  }

  calcularEtapa(semana: number): string {
    if (semana >= 25 && semana <= 33) return 'Etapa 1';
    if (semana >= 34 && semana <= 50) return 'Etapa 2';
    if (semana > 50) return 'Etapa 3';
    return 'Inicial';
  }

  formatearFecha(fecha: string | Date | null | undefined): string {
    if (!fecha) return '—';

    try {
      const date = typeof fecha === 'string' ? new Date(fecha) : fecha;

      // Validar que la fecha sea válida
      if (isNaN(date.getTime())) {
        return '—';
      }

      // Formatear como DD/MM/YYYY
      return date.toLocaleDateString('es-ES', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric'
      });
    } catch (error) {
      console.warn('Error al formatear fecha:', fecha, error);
      return '—';
    }
  }
}
