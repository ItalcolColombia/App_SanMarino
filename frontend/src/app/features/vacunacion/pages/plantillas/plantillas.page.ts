// src/app/features/vacunacion/pages/plantillas/plantillas.page.ts
import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { VacunacionService } from '../../services/vacunacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { HasPermissionDirective } from '../../../../core/auth/has-permission.directive';
import { ModalPlantillaComponent } from '../../components/modal-plantilla/modal-plantilla.component';
import { ModalItemPlantillaComponent } from '../../components/modal-item-plantilla/modal-item-plantilla.component';
import {
  advertenciaPlantilla,
  describirAlcance,
  describirFranja,
  describirObjetivo,
  describirVigencia,
  ordenarItemsPlantilla,
} from '../../funciones/describir-plantilla.funcion';
import { exportarPlantillasExcel } from '../../funciones/exportar-plantillas-excel.funcion';
import {
  FarmDtoLite,
  LINEA_PRODUCTIVA_LABEL,
  LineaProductiva,
  VacunacionLoteOpcionDto,
  VacunacionVacunaOpcionDto,
} from '../../models/vacunacion.model';
import {
  VacunacionPlantillaDetalleDto,
  VacunacionPlantillaDto,
  VacunacionPlantillaEfectivaDto,
  VacunacionPlantillaItemDto,
} from '../../models/vacunacion-plantilla.model';

/** Fila de la lista con sus textos ya resueltos: referencias estables, sin funciones en el template. */
interface FilaPlantilla {
  plantilla: VacunacionPlantillaDto;
  alcance: string;
  vigencia: string;
  advertencia: string | null;
}

/** Ítem con su objetivo y franja ya formateados. */
interface FilaItemPlantilla {
  item: VacunacionPlantillaItemDto;
  objetivo: string;
  franja: string;
}

@Component({
  changeDetection: ChangeDetectionStrategy.Eager,
  selector: 'app-vacunacion-plantillas',
  standalone: true,
  imports: [CommonModule, FormsModule, HasPermissionDirective, ModalPlantillaComponent, ModalItemPlantillaComponent],
  templateUrl: './plantillas.page.html',
})
export class PlantillasPage implements OnInit {
  readonly lineaLabel = LINEA_PRODUCTIVA_LABEL;
  readonly lineas: LineaProductiva[] = ['Levante', 'Produccion', 'Engorde'];
  readonly trackByFila = (_: number, f: FilaPlantilla): number => f.plantilla.id;
  readonly trackByItem = (_: number, f: FilaItemPlantilla): number => f.item.id;
  readonly trackByGranja = (_: number, g: FarmDtoLite): number => g.id;
  readonly trackByLote = (_: number, l: VacunacionLoteOpcionDto): string => `${l.lineaProductiva}-${l.loteId}`;

  filas: FilaPlantilla[] = [];
  filtroLinea: LineaProductiva | null = null;
  filtroNombre = '';
  soloActivas = false;

  seleccionada: VacunacionPlantillaDetalleDto | null = null;
  filasItems: FilaItemPlantilla[] = [];

  vacunas: VacunacionVacunaOpcionDto[] = [];
  granjas: FarmDtoLite[] = [];
  lotes: VacunacionLoteOpcionDto[] = [];
  lotesFiltrados: VacunacionLoteOpcionDto[] = [];
  granjaPreviewId: number | null = null;
  lotePreview: VacunacionLoteOpcionDto | null = null;
  efectiva: VacunacionPlantillaEfectivaDto | null = null;

  cargando = false;
  cargandoDetalle = false;
  cargandoPreview = false;

  modalPlantillaAbierto = false;
  modalItemAbierto = false;
  plantillaEditar: VacunacionPlantillaDetalleDto | null = null;
  itemEditar: VacunacionPlantillaItemDto | null = null;

  /** Crudo, para el export (las filas son solo presentación). */
  private plantillas: VacunacionPlantillaDto[] = [];

  constructor(
    private vacunacionSvc: VacunacionService,
    private toast: ToastService,
    private confirmDialog: ConfirmDialogService
  ) {}

  async ngOnInit(): Promise<void> {
    await Promise.all([this.cargar(), this.cargarFilterData()]);
  }

  /** Combos: las vacunas hacen falta para el modal de ítem y los lotes para la vista previa. */
  private async cargarFilterData(): Promise<void> {
    try {
      const data = await firstValueFrom(this.vacunacionSvc.getFilterData());
      this.vacunas = data.vacunas;
      this.granjas = data.granjas;
      this.lotes = data.lotes;
      this.aplicarFiltroLotes();
    } catch {
      this.toast.error('No se pudieron cargar las vacunas y los lotes.');
    }
  }

  async cargar(): Promise<void> {
    this.cargando = true;
    try {
      this.plantillas = await firstValueFrom(this.vacunacionSvc.getPlantillas(this.filtroLinea, this.soloActivas));
      this.construirFilas();
    } catch {
      this.toast.error('No se pudieron cargar las plantillas del plan.');
    } finally {
      this.cargando = false;
    }
  }

  /** El filtro por nombre se aplica en memoria: la lista de planes de una empresa es corta. */
  construirFilas(): void {
    const q = this.filtroNombre.trim().toLowerCase();
    const lista = q
      ? this.plantillas.filter(
          (p) => p.nombre.toLowerCase().includes(q) || (p.raza ?? '').toLowerCase().includes(q)
        )
      : this.plantillas;

    this.filas = lista.map((p) => ({
      plantilla: p,
      alcance: describirAlcance(p),
      vigencia: describirVigencia(p),
      advertencia: advertenciaPlantilla(p),
    }));
  }

  async onFiltroLineaChange(): Promise<void> {
    this.cerrarDetalle();
    await this.cargar();
  }

  async onSoloActivasChange(): Promise<void> {
    this.cerrarDetalle();
    await this.cargar();
  }

  async seleccionar(fila: FilaPlantilla): Promise<void> {
    this.cargandoDetalle = true;
    try {
      const detalle = await firstValueFrom(this.vacunacionSvc.getPlantilla(fila.plantilla.id));
      this.aplicarDetalle(detalle);
    } catch {
      this.toast.error('No se pudo cargar el detalle de la plantilla.');
    } finally {
      this.cargandoDetalle = false;
    }
  }

  private aplicarDetalle(detalle: VacunacionPlantillaDetalleDto): void {
    this.seleccionada = detalle;
    this.filasItems = ordenarItemsPlantilla(detalle.items).map((item) => ({
      item,
      objetivo: describirObjetivo(item.unidadObjetivo, item.valorObjetivo),
      franja: describirFranja(item.rangoDiasAntes, item.rangoDiasDespues),
    }));
  }

  cerrarDetalle(): void {
    this.seleccionada = null;
    this.filasItems = [];
  }

  private async recargarDetalle(): Promise<void> {
    if (!this.seleccionada) return;
    const detalle = await firstValueFrom(this.vacunacionSvc.getPlantilla(this.seleccionada.id));
    this.aplicarDetalle(detalle);
  }

  // ─── Plantilla ────────────────────────────────────────────────────────────

  abrirNuevaPlantilla(): void {
    this.plantillaEditar = null;
    this.modalPlantillaAbierto = true;
  }

  abrirEditarPlantilla(detalle: VacunacionPlantillaDetalleDto): void {
    this.plantillaEditar = detalle;
    this.modalPlantillaAbierto = true;
  }

  cerrarModalPlantilla(): void {
    this.modalPlantillaAbierto = false;
    this.plantillaEditar = null;
  }

  async onPlantillaGuardada(dto: VacunacionPlantillaDetalleDto): Promise<void> {
    this.cerrarModalPlantilla();
    await this.cargar();
    // Queda seleccionada la que se acaba de guardar: si es nueva, el paso siguiente es cargarle vacunas.
    this.aplicarDetalle(dto);
    await this.refrescarPreview();
  }

  async eliminarPlantilla(fila: FilaPlantilla): Promise<void> {
    const p = fila.plantilla;
    const ok = await this.confirmDialog.ask({
      title: 'Eliminar plantilla del plan',
      message:
        `¿Eliminar "${p.nombre}" (${fila.alcance})? Se dan de baja sus ${p.cantidadItems} vacuna(s) programadas. ` +
        'Los cronogramas ya cargados en los lotes NO se tocan.',
      type: 'warning',
      confirmText: 'Eliminar',
    });
    if (!ok) return;

    try {
      await firstValueFrom(this.vacunacionSvc.eliminarPlantilla(p.id));
      this.toast.success('Plantilla eliminada.');
      if (this.seleccionada?.id === p.id) this.cerrarDetalle();
      await this.cargar();
      await this.refrescarPreview();
    } catch {
      this.toast.error('No se pudo eliminar la plantilla.');
    }
  }

  // ─── Ítems ────────────────────────────────────────────────────────────────

  abrirNuevoItem(): void {
    this.itemEditar = null;
    this.modalItemAbierto = true;
  }

  abrirEditarItem(fila: FilaItemPlantilla): void {
    this.itemEditar = fila.item;
    this.modalItemAbierto = true;
  }

  cerrarModalItem(): void {
    this.modalItemAbierto = false;
    this.itemEditar = null;
  }

  async onItemGuardado(): Promise<void> {
    this.cerrarModalItem();
    try {
      await this.recargarDetalle();
      // El conteo de la lista cambió, así que la lista también se refresca.
      await this.cargar();
      await this.refrescarPreview();
    } catch {
      this.toast.error('La vacuna se guardó pero no se pudo refrescar la pantalla.');
    }
  }

  async eliminarItem(fila: FilaItemPlantilla): Promise<void> {
    if (!this.seleccionada) return;
    const ok = await this.confirmDialog.ask({
      title: 'Quitar vacuna del plan',
      message: `¿Quitar "${fila.item.itemInventarioNombre}" (${fila.objetivo}) de esta plantilla?`,
      type: 'warning',
      confirmText: 'Quitar',
    });
    if (!ok) return;

    try {
      await firstValueFrom(this.vacunacionSvc.eliminarItemPlantilla(this.seleccionada.id, fila.item.id));
      this.toast.success('Vacuna quitada del plan.');
      await this.recargarDetalle();
      await this.cargar();
      await this.refrescarPreview();
    } catch {
      this.toast.error('No se pudo quitar la vacuna del plan.');
    }
  }

  // ─── Vista previa: qué plantilla le toca a un lote ─────────────────────────

  aplicarFiltroLotes(): void {
    this.lotesFiltrados = this.granjaPreviewId
      ? this.lotes.filter((l) => l.granjaId === this.granjaPreviewId)
      : [];
  }

  onGranjaPreviewChange(): void {
    this.lotePreview = null;
    this.efectiva = null;
    this.aplicarFiltroLotes();
  }

  async onLotePreviewChange(lote: VacunacionLoteOpcionDto | null): Promise<void> {
    this.lotePreview = lote;
    this.efectiva = null;
    if (!lote) return;

    this.cargandoPreview = true;
    try {
      this.efectiva = await firstValueFrom(
        this.vacunacionSvc.getPlantillaEfectiva(lote.lineaProductiva, lote.loteId)
      );
    } catch (err: any) {
      this.toast.error(err?.error?.error ?? 'No se pudo resolver la plantilla del lote.');
    } finally {
      this.cargandoPreview = false;
    }
  }

  /** Tras tocar el plan, la vista previa abierta queda desactualizada: se vuelve a resolver. */
  private async refrescarPreview(): Promise<void> {
    if (this.lotePreview) await this.onLotePreviewChange(this.lotePreview);
  }

  // ─── Export ───────────────────────────────────────────────────────────────

  async exportar(): Promise<void> {
    if (!this.plantillas.length) {
      this.toast.warning('No hay plantillas para exportar.');
      return;
    }

    try {
      // El detalle de cada plantilla se pide para la segunda hoja: la lista solo trae el conteo.
      const detalles = await Promise.all(
        this.plantillas.map((p) => firstValueFrom(this.vacunacionSvc.getPlantilla(p.id)))
      );
      exportarPlantillasExcel(this.plantillas, detalles);
    } catch {
      this.toast.error('No se pudo armar el Excel del plan.');
    }
  }
}
