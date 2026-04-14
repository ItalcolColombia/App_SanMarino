import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeguimientoItemDto, ProduccionService, IndicadorProduccionSemanalDto, IndicadoresProduccionResponse, IndicadoresProduccionRequest } from '../../services/produccion.service';
import { LoteDto } from '../../../lote/services/lote.service';
import { finalize } from 'rxjs/operators';
import * as XLSX from 'xlsx';

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
  /** ID del lote en fase Producción (mismo que listado y modal seguimiento diario). Flujo legacy. */
  @Input() produccionLoteId: number | null = null;
  /** ID del lote postura producción (flujo LPP). Si viene, se prioriza sobre produccionLoteId. */
  @Input() lotePosturaProduccionId: number | null = null;
  @Input() loading: boolean = false;

  indicadoresSemanales: IndicadorProduccionSemanalDto[] = [];
  loadingIndicadores = false;
  tieneDatosGuiaGenetica = false;
  mensajeGuiaGenetica: string | null = null;
  error: string | null = null;
  expanded = new Set<number>(); // semanas expandidas para detalle clasificadora

  constructor(private produccionService: ProduccionService) { }

  ngOnInit(): void {
    this.cargarIndicadores();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['selectedLote'] || changes['seguimientos'] || changes['produccionLoteId'] || changes['lotePosturaProduccionId']) {
      this.cargarIndicadores();
    }
  }

  cargarIndicadores(): void {
    // Flujo LPP: priorizar lotePosturaProduccionId. Flujo legacy: produccionLoteId o selectedLote.loteId
    const lppId = this.lotePosturaProduccionId ?? null;
    const loteIdLegacy = (this.produccionLoteId && this.produccionLoteId > 0) ? this.produccionLoteId : (this.selectedLote?.loteId ?? null);
    const hasLpp = lppId != null && lppId > 0;
    const hasLegacy = loteIdLegacy != null && loteIdLegacy > 0;
    if (!hasLpp && !hasLegacy) {
      this.indicadoresSemanales = [];
      this.error = null;
      return;
    }

    this.loadingIndicadores = true;
    this.error = null;

    // Request al backend: LPP o legacy (misma fuente que el tab Seguimiento).
    // semanaDesde: 26 = inicio de producción (semana de vida).
    const request: IndicadoresProduccionRequest = {
      loteId: hasLegacy ? loteIdLegacy! : 0,
      lotePosturaProduccionId: hasLpp ? lppId! : null,
      semanaDesde: 26,
      semanaHasta: null,
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
          this.mensajeGuiaGenetica = response.mensajeGuiaGenetica ?? null;
          this.error = null;

          console.log(`✅ Indicadores cargados: ${this.indicadoresSemanales.length} semanas`, response);
        },
        error: (err) => {
          console.error('❌ Error al cargar indicadores semanales:', err);
          this.error = err.error?.message || 'Error al cargar indicadores semanales. Por favor, intenta de nuevo.';
          this.indicadoresSemanales = [];
          this.tieneDatosGuiaGenetica = false;
          this.mensajeGuiaGenetica = null;
        }
      });
  }

  descargarIndicadoresExcel(): void {
    const loteNombre = (this.selectedLote?.loteNombre || `Lote_${this.selectedLote?.loteId ?? ''}`)
      .trim()
      .replace(/[\\/:*?"<>|]+/g, '-')
      .replace(/\s+/g, ' ')
      .slice(0, 120) || 'lote';

    const maxSemana = this.indicadoresSemanales.length
      ? Math.max(...this.indicadoresSemanales.map(x => x.semana || 0))
      : 0;
    const stamp = new Date().toISOString().slice(0, 10);
    const filename = `produccion-lote-${loteNombre}-tap-indicador-semana-${maxSemana || 'NA'}-${stamp}.xlsx`;

    const rows = (this.indicadoresSemanales || []).map(ind => ({
      Semana: ind.semana,
      FechaInicio: this.formatearFecha(ind.fechaInicioSemana),
      FechaFin: this.formatearFecha(ind.fechaFinSemana),
      Registros: ind.totalRegistros,

      MortalidadH: ind.mortalidadHembras,
      MortalidadM: ind.mortalidadMachos,
      PorcMortH: ind.porcentajeMortalidadHembras,
      PorcMortM: ind.porcentajeMortalidadMachos,
      GuiaMortH: ind.mortalidadGuiaHembras,
      GuiaMortM: ind.mortalidadGuiaMachos,
      DifMortH: ind.diferenciaMortalidadHembras,
      DifMortM: ind.diferenciaMortalidadMachos,

      SeleccionH: ind.seleccionHembras,
      PorcSelH: ind.porcentajeSeleccionHembras,

      ConsumoKgH: ind.consumoKgHembras,
      ConsumoKgM: ind.consumoKgMachos,
      ConsumoKgTotal: ind.consumoTotalKg,
      ConsumoPromDiaKg: ind.consumoPromedioDiarioKg,
      ConsRealGrAveDiaH: this.consumoRealGrAveDiaH(ind),
      ConsGuiaGrAveDiaH: ind.consumoGuiaHembras,
      DifConsH: ind.diferenciaConsumoHembras,
      ConsRealGrAveDiaM: this.consumoRealGrAveDiaM(ind),
      ConsGuiaGrAveDiaM: ind.consumoGuiaMachos,
      DifConsM: ind.diferenciaConsumoMachos,

      HuevosTot: ind.huevosTotales,
      HuevosTotGuia: ind.huevosTotalesGuia,
      DifHuevosTot: ind.diferenciaHuevosTotales,
      HuevosInc: ind.huevosIncubables,
      HuevosIncGuia: ind.huevosIncubablesGuia,
      DifHuevosInc: ind.diferenciaHuevosIncubables,
      PromHuevosDia: ind.promedioHuevosPorDia,
      PorcProdReal: ind.eficienciaProduccion,
      PorcProdGuia: ind.porcentajeProduccionGuia,
      DifPorcProd: ind.diferenciaPorcentajeProduccion,
      PesoHuevoProm: ind.pesoHuevoPromedio,
      PesoHuevoGuia: ind.pesoHuevoGuia,
      DifPesoHuevo: ind.diferenciaPesoHuevo,

      PesoH: ind.pesoPromedioHembras,
      PesoHGuia: ind.pesoGuiaHembras,
      DifPesoH: ind.diferenciaPesoHembras,
      PesoM: ind.pesoPromedioMachos,
      PesoMGuia: ind.pesoGuiaMachos,
      DifPesoM: ind.diferenciaPesoMachos,
      UnifReal: ind.uniformidadPromedio,
      UnifGuia: ind.uniformidadGuia,
      DifUnif: ind.diferenciaUniformidad,
      CvProm: ind.coeficienteVariacionPromedio,

      HIni: ind.avesHembrasInicioSemana,
      MIni: ind.avesMachosInicioSemana,
      HFin: ind.avesHembrasFinSemana,
      MFin: ind.avesMachosFinSemana,

      HuevoLimpio: ind.huevosLimpios,
      HuevoTratado: ind.huevosTratados,
      HuevoSucio: ind.huevosSucios,
      HuevoDeforme: ind.huevosDeformes,
      HuevoBlanco: ind.huevosBlancos,
      HuevoDobleYema: ind.huevosDobleYema,
      HuevoPiso: ind.huevosPiso,
      HuevoPequeno: ind.huevosPequenos,
      HuevoRoto: ind.huevosRotos,
      HuevoDesecho: ind.huevosDesecho,
      HuevoOtro: ind.huevosOtro
    }));

    const ws = XLSX.utils.json_to_sheet(rows);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Indicadores');
    XLSX.writeFile(wb, filename);
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
    // "semana" viene del backend como SEMANA DE VIDA (edad).
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
