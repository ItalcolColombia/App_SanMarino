// src/app/features/vacunacion/pages/reportes-cumplimiento/reportes-cumplimiento.page.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { VacunacionService } from '../../services/vacunacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { exportarCumplimientoExcel } from '../../funciones/exportar-cumplimiento-excel.funcion';
import {
  FarmDtoLite,
  LineaProductiva,
  LINEA_PRODUCTIVA_LABEL,
  VacunacionCumplimientoLoteDto,
} from '../../models/vacunacion.model';

@Component({
  selector: 'app-reportes-cumplimiento',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './reportes-cumplimiento.page.html',
})
export class ReportesCumplimientoPage implements OnInit {
  readonly lineaLabel = LINEA_PRODUCTIVA_LABEL;
  readonly lineas: LineaProductiva[] = ['Levante', 'Produccion', 'Engorde'];

  granjas: FarmDtoLite[] = [];
  granjaSeleccionadaId: number | null = null;
  lineaSeleccionada: LineaProductiva | null = null;
  fechaDesde: string | null = null;
  fechaHasta: string | null = null;

  filas: VacunacionCumplimientoLoteDto[] = [];
  cargando = false;
  consultado = false;

  constructor(private vacunacionSvc: VacunacionService, private toast: ToastService) {}

  async ngOnInit(): Promise<void> {
    try {
      const data = await firstValueFrom(this.vacunacionSvc.getFilterData());
      this.granjas = data.granjas;
    } catch {
      this.toast.error('No se pudieron cargar las granjas.');
    }
  }

  async consultar(): Promise<void> {
    this.cargando = true;
    this.consultado = true;
    try {
      this.filas = await firstValueFrom(
        this.vacunacionSvc.getCumplimiento({
          granjaIds: this.granjaSeleccionadaId ? [this.granjaSeleccionadaId] : null,
          lineaProductiva: this.lineaSeleccionada,
          fechaDesde: this.fechaDesde,
          fechaHasta: this.fechaHasta,
        })
      );
    } catch {
      this.toast.error('No se pudo generar el reporte de cumplimiento.');
    } finally {
      this.cargando = false;
    }
  }

  exportar(): void {
    if (!this.filas.length) {
      this.toast.warning('No hay datos para exportar.');
      return;
    }
    exportarCumplimientoExcel(this.filas);
  }
}
