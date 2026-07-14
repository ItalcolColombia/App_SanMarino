// src/app/features/vacunacion/pages/registro-aplicacion/registro-aplicacion.page.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { VacunacionService } from '../../services/vacunacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { HasPermissionDirective } from '../../../../core/auth/has-permission.directive';
import { ModalRegistroAplicacionComponent } from '../../components/modal-registro-aplicacion/modal-registro-aplicacion.component';
import { calcularEstadoVisual } from '../../funciones/calcular-estado-visual.funcion';
import { exportarHistorialExcel } from '../../funciones/exportar-historial-excel.funcion';
import {
  FarmDtoLite,
  LINEA_PRODUCTIVA_LABEL,
  VacunacionCronogramaItemDto,
  VacunacionLoteOpcionDto,
} from '../../models/vacunacion.model';

@Component({
  selector: 'app-registro-aplicacion',
  standalone: true,
  imports: [CommonModule, FormsModule, HasPermissionDirective, ModalRegistroAplicacionComponent],
  templateUrl: './registro-aplicacion.page.html',
})
export class RegistroAplicacionPage implements OnInit {
  readonly lineaLabel = LINEA_PRODUCTIVA_LABEL;
  readonly calcularEstadoVisual = calcularEstadoVisual;

  granjas: FarmDtoLite[] = [];
  lotes: VacunacionLoteOpcionDto[] = [];
  lotesFiltrados: VacunacionLoteOpcionDto[] = [];

  granjaSeleccionadaId: number | null = null;
  loteSeleccionado: VacunacionLoteOpcionDto | null = null;

  items: VacunacionCronogramaItemDto[] = [];
  cargando = false;

  modalAbierto = false;
  itemRegistrar: VacunacionCronogramaItemDto | null = null;

  constructor(private vacunacionSvc: VacunacionService, private toast: ToastService) {}

  async ngOnInit(): Promise<void> {
    try {
      const data = await firstValueFrom(this.vacunacionSvc.getFilterData());
      this.granjas = data.granjas;
      this.lotes = data.lotes;
    } catch {
      this.toast.error('No se pudieron cargar los datos de filtros.');
    }
  }

  onGranjaChange(): void {
    this.lotesFiltrados = this.granjaSeleccionadaId
      ? this.lotes.filter((l) => l.granjaId === this.granjaSeleccionadaId)
      : [];
    this.loteSeleccionado = null;
    this.items = [];
  }

  async onLoteChange(lote: VacunacionLoteOpcionDto | null): Promise<void> {
    this.loteSeleccionado = lote;
    this.items = [];
    if (!lote) return;

    this.cargando = true;
    try {
      this.items = await firstValueFrom(this.vacunacionSvc.getCronogramaLote(lote.lineaProductiva, lote.loteId));
    } catch {
      this.toast.error('No se pudo cargar el cronograma del lote.');
    } finally {
      this.cargando = false;
    }
  }

  abrirRegistro(item: VacunacionCronogramaItemDto): void {
    this.itemRegistrar = item;
    this.modalAbierto = true;
  }

  cerrarModal(): void {
    this.modalAbierto = false;
    this.itemRegistrar = null;
  }

  async onGuardado(): Promise<void> {
    this.cerrarModal();
    if (this.loteSeleccionado) await this.onLoteChange(this.loteSeleccionado);
  }

  exportarHistorial(): void {
    if (!this.items.length) {
      this.toast.warning('No hay registros para exportar.');
      return;
    }
    exportarHistorialExcel(this.items, this.loteSeleccionado?.loteNombre ?? '');
  }
}
