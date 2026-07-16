// src/app/features/vacunacion/pages/reportes-cumplimiento/reportes-cumplimiento.page.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { VacunacionService } from '../../services/vacunacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { exportarCumplimientoExcel } from '../../funciones/exportar-cumplimiento-excel.funcion';
import { calcularKpisCumplimiento, KpisCumplimiento } from '../../funciones/calcular-kpis-cumplimiento.funcion';
import { estadoVisualDe, EstadoVisual } from '../../funciones/calcular-estado-visual.funcion';
import {
  FarmDtoLite,
  LineaProductiva,
  LINEA_PRODUCTIVA_LABEL,
  VacunacionCumplimientoFiltroRequest,
  VacunacionCumplimientoLoteDto,
  VacunacionCumplimientoDetalleDto,
  VacunacionLoteOpcionDto,
} from '../../models/vacunacion.model';

/** Fila del detalle con el badge de estado precalculado (sin funciones en template). */
interface FilaDetalle {
  d: VacunacionCumplimientoDetalleDto;
  estado: EstadoVisual;
}

@Component({
  selector: 'app-reportes-cumplimiento',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './reportes-cumplimiento.page.html',
})
export class ReportesCumplimientoPage implements OnInit {
  readonly lineaLabel = LINEA_PRODUCTIVA_LABEL;
  readonly lineas: LineaProductiva[] = ['Levante', 'Produccion', 'Engorde'];
  readonly trackByGranja = (_: number, g: FarmDtoLite): number => g.id;
  readonly trackByLoteOpcion = (_: number, l: VacunacionLoteOpcionDto): string => `${l.lineaProductiva}-${l.loteId}`;
  readonly trackByFilaLote = (_: number, f: VacunacionCumplimientoLoteDto): string => `${f.lineaProductiva}-${f.loteId}`;
  readonly trackByFilaDetalle = (_: number, f: FilaDetalle): number => f.d.itemId;

  granjas: FarmDtoLite[] = [];
  lotesOpciones: VacunacionLoteOpcionDto[] = [];
  lotesFiltrados: VacunacionLoteOpcionDto[] = [];

  granjaSeleccionadaId: number | null = null;
  loteSeleccionado: VacunacionLoteOpcionDto | null = null;
  lineaSeleccionada: LineaProductiva | null = null;
  fechaDesde: string | null = null;
  fechaHasta: string | null = null;

  filas: VacunacionCumplimientoLoteDto[] = [];
  detalle: VacunacionCumplimientoDetalleDto[] = [];
  filasDetalle: FilaDetalle[] = [];
  kpis: KpisCumplimiento | null = null;

  vista: 'lotes' | 'detalle' = 'lotes';
  cargandoFiltros = false;
  cargando = false;
  consultado = false;

  constructor(private vacunacionSvc: VacunacionService, private toast: ToastService) {}

  async ngOnInit(): Promise<void> {
    this.cargandoFiltros = true;
    try {
      const data = await firstValueFrom(this.vacunacionSvc.getFilterData());
      this.granjas = data.granjas;
      this.lotesOpciones = data.lotes;
    } catch {
      this.toast.error('No se pudieron cargar las granjas.');
    } finally {
      this.cargandoFiltros = false;
    }
  }

  onGranjaChange(): void {
    this.loteSeleccionado = null;
    this.lotesFiltrados = this.granjaSeleccionadaId
      ? this.lotesOpciones.filter((l) => l.granjaId === this.granjaSeleccionadaId)
      : [];
  }

  async consultar(): Promise<void> {
    this.cargando = true;
    this.consultado = true;
    const req: VacunacionCumplimientoFiltroRequest = {
      granjaIds: this.granjaSeleccionadaId ? [this.granjaSeleccionadaId] : null,
      loteIds: this.loteSeleccionado ? [this.loteSeleccionado.loteId] : null,
      lineaProductiva: this.loteSeleccionado?.lineaProductiva ?? this.lineaSeleccionada,
      fechaDesde: this.fechaDesde,
      fechaHasta: this.fechaHasta,
    };
    try {
      const [filas, detalle] = await Promise.all([
        firstValueFrom(this.vacunacionSvc.getCumplimiento(req)),
        firstValueFrom(this.vacunacionSvc.getCumplimientoDetalle(req)),
      ]);
      this.filas = filas;
      this.detalle = detalle;
      this.filasDetalle = detalle.map((d) => ({ d, estado: this.estadoDetalle(d) }));
      this.kpis = calcularKpisCumplimiento(filas);
    } catch {
      this.toast.error('No se pudo generar el reporte de cumplimiento.');
      this.filas = [];
      this.detalle = [];
      this.filasDetalle = [];
      this.kpis = null;
    } finally {
      this.cargando = false;
    }
  }

  /** Reusa la misma presentación de estados del cronograma para el detalle del reporte. */
  private estadoDetalle(d: VacunacionCumplimientoDetalleDto): EstadoVisual {
    return estadoVisualDe(d.estado, d.diasDesviacion, d.incumplido);
  }

  exportar(): void {
    if (!this.filas.length) {
      this.toast.warning('No hay datos para exportar.');
      return;
    }
    exportarCumplimientoExcel(this.filas, this.detalle, this.filtrosLegibles());
  }

  private filtrosLegibles(): string[] {
    const partes: string[] = [];
    if (this.granjaSeleccionadaId) {
      const g = this.granjas.find((x) => x.id === this.granjaSeleccionadaId);
      partes.push(`Granja: ${g?.name ?? this.granjaSeleccionadaId}`);
    }
    if (this.loteSeleccionado) partes.push(`Lote: ${this.loteSeleccionado.loteNombre}`);
    if (this.lineaSeleccionada) partes.push(`Línea: ${LINEA_PRODUCTIVA_LABEL[this.lineaSeleccionada]}`);
    if (this.fechaDesde) partes.push(`Desde: ${this.fechaDesde}`);
    if (this.fechaHasta) partes.push(`Hasta: ${this.fechaHasta}`);
    return partes.length ? [partes.join(' · ')] : [];
  }
}
