// src/app/features/vacunacion/pages/registro-aplicacion/registro-aplicacion.page.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { VacunacionService } from '../../services/vacunacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { HasPermissionDirective } from '../../../../core/auth/has-permission.directive';
import { ModalRegistroAplicacionComponent } from '../../components/modal-registro-aplicacion/modal-registro-aplicacion.component';
import { construirFilasCronograma, trackByFilaCronograma, FilaCronograma } from '../../funciones/construir-filas-cronograma.funcion';
import { calcularKpisCronograma, KpisCronograma } from '../../funciones/calcular-kpis-cronograma.funcion';
import { exportarHistorialExcel } from '../../funciones/exportar-historial-excel.funcion';
import {
  FarmDtoLite,
  LINEA_PRODUCTIVA_LABEL,
  VacunacionCronogramaItemDto,
  VacunacionLoteOpcionDto,
  VacunacionUsuarioOpcionDto,
} from '../../models/vacunacion.model';

@Component({
  selector: 'app-registro-aplicacion',
  standalone: true,
  imports: [CommonModule, FormsModule, HasPermissionDirective, ModalRegistroAplicacionComponent],
  templateUrl: './registro-aplicacion.page.html',
})
export class RegistroAplicacionPage implements OnInit {
  readonly lineaLabel = LINEA_PRODUCTIVA_LABEL;
  readonly trackByFila = trackByFilaCronograma;
  readonly trackByGranja = (_: number, g: FarmDtoLite): number => g.id;
  readonly trackByLote = (_: number, l: VacunacionLoteOpcionDto): string => `${l.lineaProductiva}-${l.loteId}`;

  granjas: FarmDtoLite[] = [];
  lotes: VacunacionLoteOpcionDto[] = [];
  lotesFiltrados: VacunacionLoteOpcionDto[] = [];
  usuarios: VacunacionUsuarioOpcionDto[] = [];

  granjaSeleccionadaId: number | null = null;
  loteSeleccionado: VacunacionLoteOpcionDto | null = null;
  filtroLote = '';

  filas: FilaCronograma[] = [];
  kpis: KpisCronograma | null = null;

  cargandoFiltros = false;
  cargando = false;

  modalAbierto = false;
  itemRegistrar: VacunacionCronogramaItemDto | null = null;

  private items: VacunacionCronogramaItemDto[] = [];

  constructor(private vacunacionSvc: VacunacionService, private toast: ToastService) {}

  async ngOnInit(): Promise<void> {
    this.cargandoFiltros = true;
    try {
      const data = await firstValueFrom(this.vacunacionSvc.getFilterData());
      this.granjas = data.granjas;
      this.lotes = data.lotes;
      // ?? []: tolera un backend anterior sin 'usuarios' (ventana de despliegue rolling)
      this.usuarios = data.usuarios ?? [];
    } catch {
      this.toast.error('No se pudieron cargar los datos de filtros.');
    } finally {
      this.cargandoFiltros = false;
    }
  }

  onGranjaChange(): void {
    this.filtroLote = '';
    this.loteSeleccionado = null;
    this.items = [];
    this.filas = [];
    this.kpis = null;
    this.aplicarFiltroLotes();
  }

  aplicarFiltroLotes(): void {
    let lista = this.granjaSeleccionadaId
      ? this.lotes.filter((l) => l.granjaId === this.granjaSeleccionadaId)
      : [];
    const q = this.filtroLote.trim().toLowerCase();
    if (q) lista = lista.filter((l) => l.loteNombre.toLowerCase().includes(q));
    this.lotesFiltrados = lista;
  }

  async onLoteChange(lote: VacunacionLoteOpcionDto | null): Promise<void> {
    this.loteSeleccionado = lote;
    this.items = [];
    this.filas = [];
    this.kpis = null;
    if (!lote) return;

    this.cargando = true;
    try {
      this.items = await firstValueFrom(this.vacunacionSvc.getCronogramaLote(lote.lineaProductiva, lote.loteId));
      this.filas = construirFilasCronograma(this.items);
      this.kpis = calcularKpisCronograma(this.items);
    } catch {
      this.toast.error('No se pudo cargar el cronograma del lote.');
    } finally {
      this.cargando = false;
    }
  }

  puedeRegistrar(fila: FilaCronograma): boolean {
    return !fila.item.registro || fila.item.registro.estado === 'Pendiente';
  }

  responsableDe(fila: FilaCronograma): string {
    const r = fila.item.registro;
    if (!r) return '—';
    return r.aplicadoPorUserNombre ?? r.aplicadoPorNombreLibre ?? '—';
  }

  abrirRegistro(fila: FilaCronograma): void {
    this.itemRegistrar = fila.item;
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
