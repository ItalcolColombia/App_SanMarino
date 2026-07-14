// src/app/features/vacunacion/pages/cronograma-administracion/cronograma-administracion.page.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { VacunacionService } from '../../services/vacunacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { HasPermissionDirective } from '../../../../core/auth/has-permission.directive';
import { ModalItemCronogramaComponent } from '../../components/modal-item-cronograma/modal-item-cronograma.component';
import { calcularEstadoVisual } from '../../funciones/calcular-estado-visual.funcion';
import { exportarCronogramaExcel } from '../../funciones/exportar-cronograma-excel.funcion';
import {
  FarmDtoLite,
  LineaProductiva,
  LINEA_PRODUCTIVA_LABEL,
  VacunacionCronogramaItemDto,
  VacunacionLoteOpcionDto,
  VacunacionVacunaOpcionDto,
} from '../../models/vacunacion.model';

@Component({
  selector: 'app-cronograma-administracion',
  standalone: true,
  imports: [CommonModule, FormsModule, HasPermissionDirective, ModalItemCronogramaComponent],
  templateUrl: './cronograma-administracion.page.html',
})
export class CronogramaAdministracionPage implements OnInit {
  readonly lineaLabel = LINEA_PRODUCTIVA_LABEL;
  readonly calcularEstadoVisual = calcularEstadoVisual;

  granjas: FarmDtoLite[] = [];
  lotes: VacunacionLoteOpcionDto[] = [];
  lotesFiltrados: VacunacionLoteOpcionDto[] = [];
  vacunas: VacunacionVacunaOpcionDto[] = [];

  granjaSeleccionadaId: number | null = null;
  loteSeleccionado: VacunacionLoteOpcionDto | null = null;

  items: VacunacionCronogramaItemDto[] = [];
  cargandoFiltros = false;
  cargandoCronograma = false;

  modalAbierto = false;
  itemEditar: VacunacionCronogramaItemDto | null = null;

  constructor(
    private vacunacionSvc: VacunacionService,
    private toast: ToastService,
    private confirmDialog: ConfirmDialogService
  ) {}

  async ngOnInit(): Promise<void> {
    await this.cargarFiltros();
  }

  async cargarFiltros(): Promise<void> {
    this.cargandoFiltros = true;
    try {
      const data = await firstValueFrom(this.vacunacionSvc.getFilterData());
      this.granjas = data.granjas;
      this.lotes = data.lotes;
      this.vacunas = data.vacunas;
    } catch {
      this.toast.error('No se pudieron cargar los datos de filtros (granjas/lotes/vacunas).');
    } finally {
      this.cargandoFiltros = false;
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

    this.cargandoCronograma = true;
    try {
      this.items = await firstValueFrom(this.vacunacionSvc.getCronogramaLote(lote.lineaProductiva, lote.loteId));
    } catch {
      this.toast.error('No se pudo cargar el cronograma del lote.');
    } finally {
      this.cargandoCronograma = false;
    }
  }

  abrirNuevo(): void {
    this.itemEditar = null;
    this.modalAbierto = true;
  }

  abrirEditar(item: VacunacionCronogramaItemDto): void {
    this.itemEditar = item;
    this.modalAbierto = true;
  }

  cerrarModal(): void {
    this.modalAbierto = false;
    this.itemEditar = null;
  }

  async onGuardado(): Promise<void> {
    this.cerrarModal();
    if (this.loteSeleccionado) await this.onLoteChange(this.loteSeleccionado);
  }

  async eliminar(item: VacunacionCronogramaItemDto): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: 'Eliminar vacuna del cronograma',
      message: `¿Eliminar "${item.itemInventarioNombre}" del cronograma de ${item.loteNombre}?`,
      type: 'warning',
      confirmText: 'Eliminar',
    });
    if (!ok) return;

    try {
      await firstValueFrom(this.vacunacionSvc.eliminarItem(item.id));
      this.toast.success('Ítem eliminado del cronograma.');
      if (this.loteSeleccionado) await this.onLoteChange(this.loteSeleccionado);
    } catch {
      this.toast.error('No se pudo eliminar el ítem.');
    }
  }

  exportar(): void {
    if (!this.items.length) {
      this.toast.warning('No hay ítems en el cronograma para exportar.');
      return;
    }
    exportarCronogramaExcel(this.items, this.loteSeleccionado?.loteNombre ?? '');
  }
}
