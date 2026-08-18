// src/app/features/vacunacion/pages/cronograma-administracion/cronograma-administracion.page.ts
import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { VacunacionService } from '../../services/vacunacion.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { HasPermissionDirective } from '../../../../core/auth/has-permission.directive';
import { ModalItemCronogramaComponent } from '../../components/modal-item-cronograma/modal-item-cronograma.component';
import { construirFilasCronograma, trackByFilaCronograma, FilaCronograma } from '../../funciones/construir-filas-cronograma.funcion';
import { calcularKpisCronograma, KpisCronograma } from '../../funciones/calcular-kpis-cronograma.funcion';
import { exportarCronogramaExcel } from '../../funciones/exportar-cronograma-excel.funcion';
import { resumirImpactoLote } from '../../funciones/resumir-impacto-materializacion.funcion';
import {
  FarmDtoLite,
  LINEA_PRODUCTIVA_LABEL,
  VacunacionCronogramaItemDto,
  VacunacionLoteOpcionDto,
  VacunacionVacunaOpcionDto,
} from '../../models/vacunacion.model';
import { VacunacionMaterializacionLoteDto } from '../../models/vacunacion-materializador.model';

@Component({
  changeDetection: ChangeDetectionStrategy.Eager,
  selector: 'app-cronograma-administracion',
  standalone: true,
  imports: [CommonModule, FormsModule, HasPermissionDirective, ModalItemCronogramaComponent],
  templateUrl: './cronograma-administracion.page.html',
})
export class CronogramaAdministracionPage implements OnInit {
  readonly lineaLabel = LINEA_PRODUCTIVA_LABEL;
  readonly trackByFila = trackByFilaCronograma;
  readonly trackByGranja = (_: number, g: FarmDtoLite): number => g.id;
  readonly trackByLote = (_: number, l: VacunacionLoteOpcionDto): string => `${l.lineaProductiva}-${l.loteId}`;

  granjas: FarmDtoLite[] = [];
  lotes: VacunacionLoteOpcionDto[] = [];
  lotesFiltrados: VacunacionLoteOpcionDto[] = [];
  vacunas: VacunacionVacunaOpcionDto[] = [];

  granjaSeleccionadaId: number | null = null;
  loteSeleccionado: VacunacionLoteOpcionDto | null = null;
  filtroLote = '';

  /** Filas con estado visual precalculado (referencias estables — sin funciones en template). */
  filas: FilaCronograma[] = [];
  kpis: KpisCronograma | null = null;

  cargandoFiltros = false;
  cargandoCronograma = false;
  aplicandoPlan = false;

  /** Sólo cuando hay algo para escribir; si el lote está al día, es `null` y no se muestra aviso. */
  pendienteDelPlan: VacunacionMaterializacionLoteDto | null = null;

  modalAbierto = false;
  itemEditar: VacunacionCronogramaItemDto | null = null;

  /** Fuente cruda para el export (las filas son solo presentación). */
  private items: VacunacionCronogramaItemDto[] = [];

  constructor(
    private vacunacionSvc: VacunacionService,
    private toast: ToastService,
    private confirmDialog: ConfirmDialogService
  ) {}

  async ngOnInit(): Promise<void> {
    await this.cargarFiltros();
  }

  async cargarFiltros(refrescar = false): Promise<void> {
    this.cargandoFiltros = true;
    try {
      const data = await firstValueFrom(
        refrescar ? this.vacunacionSvc.refrescarFilterData() : this.vacunacionSvc.getFilterData()
      );
      this.granjas = data.granjas;
      this.lotes = data.lotes;
      this.vacunas = data.vacunas;
      this.aplicarFiltroLotes();
    } catch {
      this.toast.error('No se pudieron cargar los datos de filtros (granjas/lotes/vacunas).');
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
    this.pendienteDelPlan = null;
    if (!lote) return;

    this.cargandoCronograma = true;
    try {
      this.items = await firstValueFrom(this.vacunacionSvc.getCronogramaLote(lote.lineaProductiva, lote.loteId));
      this.filas = construirFilasCronograma(this.items);
      this.kpis = calcularKpisCronograma(this.items);
    } catch {
      this.toast.error('No se pudo cargar el cronograma del lote.');
    } finally {
      this.cargandoCronograma = false;
    }

    await this.revisarPlanDelLote(lote);
  }

  // ─── Aviso: este lote tiene vacunas del plan sin bajar (W2) ───────────────

  /**
   * Pregunta si al lote le falta algo de su plan. Va aparte del cronograma —y después— porque es
   * información secundaria: si falla o tarda, la pantalla ya mostró lo que el usuario vino a ver.
   *
   * <p>Es una LECTURA. La materialización no se dispara sola al abrir el cronograma: las filas
   * nacerían a nombre de quien pasó a mirar la pantalla, y este módulo existe justamente para poder
   * decir quién programó qué y cuándo.</p>
   */
  private async revisarPlanDelLote(lote: VacunacionLoteOpcionDto): Promise<void> {
    try {
      const preview = await firstValueFrom(
        this.vacunacionSvc.previewMaterializacionLote(lote.lineaProductiva, lote.loteId)
      );
      this.pendienteDelPlan = preview.conteos.escribeAlgo ? preview : null;
    } catch {
      // Sin permiso de plantillas, o el lote no resuelve a ninguna: no es un error de esta pantalla.
      this.pendienteDelPlan = null;
    }
  }

  get resumenPendienteDelPlan(): string {
    return this.pendienteDelPlan ? resumirImpactoLote(this.pendienteDelPlan.conteos) : '';
  }

  /** Baja el plan a ESTE lote. El impacto ya está en pantalla en el aviso, así que sólo se confirma. */
  async aplicarPlanAlLote(): Promise<void> {
    const lote = this.loteSeleccionado;
    const pendiente = this.pendienteDelPlan;
    if (!lote || !pendiente) return;

    const ok = await this.confirmDialog.ask({
      title: 'Aplicar el plan a este lote',
      message:
        `Se van a escribir en el cronograma de ${lote.loteNombre}: ${resumirImpactoLote(pendiente.conteos)} ` +
        'Lo ya aplicado y lo cargado a mano no se toca, y no se borra ninguna fila.',
      type: 'info',
      confirmText: 'Aplicar',
    });
    if (!ok) return;

    this.aplicandoPlan = true;
    try {
      await firstValueFrom(this.vacunacionSvc.aplicarMaterializacionLote(lote.lineaProductiva, lote.loteId));
      this.toast.success('El plan quedó aplicado al cronograma del lote.');
      await this.onLoteChange(lote);
    } catch (err: any) {
      this.toast.error(err?.error?.error ?? 'No se pudo aplicar el plan al lote.');
    } finally {
      this.aplicandoPlan = false;
    }
  }

  abrirNuevo(): void {
    this.itemEditar = null;
    this.modalAbierto = true;
  }

  abrirEditar(fila: FilaCronograma): void {
    this.itemEditar = fila.item;
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

  async eliminar(fila: FilaCronograma): Promise<void> {
    const item = fila.item;
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
