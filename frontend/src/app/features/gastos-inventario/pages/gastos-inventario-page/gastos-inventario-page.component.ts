import { CommonModule } from '@angular/common';
import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { FiltroSelectComponent, FilterDataResponse } from '../../../lote-levante/pages/filtro-select/filtro-select.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';
import { LoteBaseEngordeApi, LoteBaseEngordeDto } from '../../../engorde-comun/services/lote-base-engorde.api';
import { GestionInventarioService, InventarioGestionSiloDto } from '../../../gestion-inventario/services/gestion-inventario.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { exportarGastosInventarioExcel } from '../../funciones/exportar-gastos-inventario-excel.funcion';
import { UserPermissionService } from '../../../../core/auth/user-permission.service';
import {
  extremosVentanaRegistro,
  hintVentanaFechaRegistro,
  PERMISO_FECHA_RETROACTIVA
} from '../../../../shared/utils/fecha/ventana-fecha-registro.funcion';
import {
  PRESETS_RANGO_GASTOS,
  RangoPresetGastos,
  calcularRangoPreset,
  validarRangoFechas
} from '../../funciones/rango-fechas-gastos.funcion';
import {
  CreateInventarioGastoRequest,
  EstadoGastoFiltro,
  InventarioGastoItemStockDto,
  InventarioGastoLineaRequest,
  InventarioGastoListItemDto,
  InventarioGastosService
} from '../../services/inventario-gastos.service';

type ModalMode = 'create' | 'detail' | null;

@Component({
  selector: 'app-gastos-inventario-page',
  standalone: true,
  imports: [CommonModule, FormsModule, FiltroSelectComponent],
  templateUrl: './gastos-inventario-page.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./gastos-inventario-page.component.scss']
})
export class GastosInventarioPageComponent implements OnInit {
  readonly filterDataUrl = `${environment.apiUrl}/inventario-gastos/filter-data`;

  loading = false;
  exporting = false;
  error: string | null = null;

  // List filters
  selectedFarmId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  selectedLoteId: number | null = null;

  /**
   * Estado de los gastos que muestra la tabla. Por defecto solo los **activos**: un gasto eliminado
   * ya devolvió su stock al inventario, así que no es consumo. Los otros valores permiten consultar
   * el historial de eliminados por pantalla — el reporte Excel los excluye siempre.
   */
  selectedEstado: EstadoGastoFiltro = 'Activo';

  /**
   * Rango de fechas del consumo (`yyyy-MM-dd`, ambos extremos inclusivos). Vacío = **todos** los
   * consumos, que es el comportamiento histórico del módulo. Acota por igual la tabla y las dos
   * hojas del Excel: lo que el usuario ve en pantalla es exactamente lo que descarga.
   */
  fechaDesde: string | null = null;
  fechaHasta: string | null = null;

  /** Atajos de rango ofrecidos en la tarjeta de filtros (referencia estable para el template). */
  readonly presetsRango = PRESETS_RANGO_GASTOS;

  conceptos: string[] = [];
  list: InventarioGastoListItemDto[] = [];

  // Modal create
  modalOpen = false;
  modalMode: ModalMode = null;
  modalTitle = '';

  formFarmId: number | null = null;
  formNucleoId: string | null = null;
  formGalponId: string | null = null;
  formLoteId: number | null = null;
  /**
   * Lote PROGRAMADO elegido en el modal (lote base aún sin encasetar). Excluyente con `formLoteId`:
   * el gasto se carga contra el lote real O contra la programación, nunca contra ambos.
   */
  formLoteBaseId: number | null = null;
  formFecha: string = this.todayYmd();
  formConcepto: string = '';
  formObservaciones: string = '';

  items: InventarioGastoItemStockDto[] = [];
  /**
   * Empresa que ubica el inventario por silo (Santa Reyes). Fail-closed: apagado hasta que el flag
   * responda, así que la pantalla arranca igual que para todas las demás empresas.
   */
  manejaPorSilo = false;
  /** Silos y bodegas de la granja del modal: el gasto sale de uno de ellos. */
  silosDeGranja: InventarioGestionSiloDto[] = [];
  /** Silo elegido para la PRÓXIMA línea. Cada línea guarda el suyo. */
  formSiloId: number | null = null;
  selectedItemId: number | null = null;
  selectedItem: InventarioGastoItemStockDto | null = null;
  qtyToAdd: number | null = null;

  lineas: Array<InventarioGastoLineaRequest & { codigo: string; nombre: string; unidad: string; stockCantidad: number; siloNombre?: string | null }> = [];

  // Detail modal
  detail: any = null;

  /**
   * La empresa PROGRAMA sus lotes de engorde: se puede dar de baja insumos (p. ej. desinsectación)
   * contra un lote todavía no encasetado. Fail-closed: apagado hasta que el backend lo confirme.
   */
  programacionLotes = false;
  /** Programación completa de la empresa; se filtra por la granja del modal. */
  private lotesBase: LoteBaseEngordeDto[] = [];
  /** Lotes programados ofrecidos en el modal (activos y asignados a `formFarmId`). Referencia estable. */
  lotesBaseParaGranja: LoteBaseEngordeDto[] = [];

  constructor(
    private api: InventarioGastosService,
    private toast: ToastService,
    private confirmDialog: ConfirmDialogService,
    private companyConfig: ActiveCompanyConfigService,
    private loteBaseApi: LoteBaseEngordeApi,
    private gestionInventarioSvc: GestionInventarioService,
    private userPermService: UserPermissionService
  ) {}

  /** Ventana de fechas admitida para `formFecha` (mes en curso ∪ últimos 15 días). */
  private get puedeRetroactivar(): boolean {
    return this.userPermService.has(PERMISO_FECHA_RETROACTIVA);
  }

  get formFechaMin(): string | null {
    return extremosVentanaRegistro(new Date(), this.puedeRetroactivar).min;
  }

  get formFechaMax(): string {
    return extremosVentanaRegistro(new Date(), this.puedeRetroactivar).max;
  }

  get formFechaHint(): string {
    return hintVentanaFechaRegistro(new Date(), this.puedeRetroactivar);
  }

  get qtyExceedsStock(): boolean {
    if (!this.selectedItem || this.qtyToAdd == null) return false;
    const qty = Number(this.qtyToAdd);
    if (!Number.isFinite(qty) || qty <= 0) return false;
    return qty > (this.selectedItem.stockCantidad ?? 0);
  }

  async ngOnInit(): Promise<void> {
    this.companyConfig.programacionLotesEngorde().subscribe(activo => {
      this.programacionLotes = activo;
      if (activo) this.cargarLotesProgramados();
    });
    this.companyConfig.getFlags().subscribe(flags => {
      this.manejaPorSilo = flags.manejaInventarioPorSilo;
      if (this.manejaPorSilo && this.formFarmId) this.cargarSilosDeGranja();
    });
    await this.refresh();
  }

  /** Catálogo de lotes programados (solo si la empresa los maneja). */
  private cargarLotesProgramados(): void {
    this.loteBaseApi.getAll().subscribe({
      next: bases => {
        this.lotesBase = bases ?? [];
        this.recomputeLotesBaseParaGranja();
      },
      error: () => { this.lotesBase = []; this.lotesBaseParaGranja = []; }
    });
  }

  /**
   * Lotes programados que se pueden elegir en el modal: activos y asignados a la granja del gasto
   * (la misma regla de visibilidad que usa el selector al crear el lote). Solo se recalcula ante
   * eventos — nunca desde el template — para no romper el change detection.
   */
  private recomputeLotesBaseParaGranja(): void {
    const granjaId = this.formFarmId;
    this.lotesBaseParaGranja = granjaId
      ? this.lotesBase
          .filter(b => b.activo && (b.granjaIds ?? []).includes(granjaId))
          .sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'))
      : [];
    if (this.formLoteBaseId != null && !this.lotesBaseParaGranja.some(b => b.id === this.formLoteBaseId)) {
      this.formLoteBaseId = null;
    }
  }

  /** El gasto va contra el lote real O contra el programado: elegir uno limpia el otro. */
  onFormLoteRealChange(loteId: number | null): void {
    this.formLoteId = loteId;
    if (loteId != null) this.formLoteBaseId = null;
  }

  onFormLoteBaseChange(): void {
    if (this.formLoteBaseId != null) this.formLoteId = null;
  }

  private todayYmd(): string {
    const d = new Date();
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  /** Carga los conceptos del formulario, filtrados a los que tienen ítems con stock en `formFarmId`. */
  async loadConceptosParaFormFarm(): Promise<void> {
    if (!this.formFarmId) {
      this.conceptos = [];
      return;
    }
    try {
      this.conceptos = await firstValueFrom(this.api.getConceptos(this.formFarmId));
    } catch {
      this.conceptos = [];
    }
  }

  /** Mensaje del rango de fechas cuando es inconsistente; `null` si se puede consultar. */
  get rangoError(): string | null {
    return validarRangoFechas(this.fechaDesde, this.fechaHasta);
  }

  /** ¿Hay rango puesto? (para el badge/atajo «Todo el histórico» del template). */
  get rangoActivo(): boolean {
    return !!(this.fechaDesde || this.fechaHasta);
  }

  /** Cambio en Desde/Hasta: con rango válido recarga; con rango inválido deja el aviso y NO consulta. */
  onRangoChange(): void {
    const err = this.rangoError;
    if (err) {
      this.error = err;
      return;
    }
    this.error = null;
    void this.refresh();
  }

  /** Aplica un atajo de rango (Este mes, Mes anterior, …) y recarga la tabla. */
  aplicarPresetRango(preset: RangoPresetGastos): void {
    const rango = calcularRangoPreset(preset, new Date());
    this.fechaDesde = rango.desde;
    this.fechaHasta = rango.hasta;
    this.onRangoChange();
  }

  /** Quita el rango (vuelve a todo el histórico) y recarga. */
  limpiarRango(): void {
    this.fechaDesde = null;
    this.fechaHasta = null;
    this.onRangoChange();
  }

  async refresh(): Promise<void> {
    const rangoErr = this.rangoError;
    if (rangoErr) {
      this.error = rangoErr;
      return;
    }
    this.loading = true;
    this.error = null;
    try {
      const list = await firstValueFrom(
        this.api.search({
          farmId: this.selectedFarmId ?? undefined,
          nucleoId: this.selectedNucleoId ?? undefined,
          galponId: this.selectedGalponId ?? undefined,
          loteAveEngordeId: this.selectedLoteId ?? undefined,
          fechaDesde: this.fechaDesde ?? undefined,
          fechaHasta: this.fechaHasta ?? undefined,
          estado: this.selectedEstado || undefined
        })
      );
      this.list = list ?? [];
    } catch (e: any) {
      this.error = e?.error?.error ?? 'No se pudo cargar la lista de gastos.';
      this.list = [];
    } finally {
      this.loading = false;
    }
  }

  onFilterDataLoaded(_: FilterDataResponse): void {
    // no-op (solo evita llamadas extra en el componente filtro)
  }

  openCreate(): void {
    this.modalOpen = true;
    this.modalMode = 'create';
    this.modalTitle = 'Registrar gasto de inventario';
    this.formFarmId = this.selectedFarmId;
    // Núcleo/galpón no se piden para registrar el gasto (se descuenta y se referencia
    // a nivel granja + corrida); se dejan null para no heredar el filtro de lista.
    this.formNucleoId = null;
    this.formGalponId = null;
    this.formLoteId = this.selectedLoteId;
    this.formLoteBaseId = null;
    this.recomputeLotesBaseParaGranja();
    this.formFecha = this.todayYmd();
    this.formConcepto = '';
    this.formObservaciones = '';
    this.conceptos = [];
    this.items = [];
    this.selectedItemId = null;
    this.selectedItem = null;
    this.qtyToAdd = null;
    this.formSiloId = null;
    this.silosDeGranja = [];
    this.lineas = [];
    this.cargarSilosDeGranja();
    void this.loadConceptosParaFormFarm();
  }

  /** Al cambiar la granja del formulario: recarga conceptos (con stock en esa granja) y limpia la selección previa. */
  async onFormFarmChange(): Promise<void> {
    this.formConcepto = '';
    this.formSiloId = null;
    this.recomputeLotesBaseParaGranja();
    this.cargarSilosDeGranja();
    await this.loadConceptosParaFormFarm();
    await this.onConceptoChange();
  }

  /**
   * Silos y bodegas de la granja. Es la MISMA lista que valida el backend al registrar el consumo
   * (`farm_silos` de esa granja, activos): ofrecer otra terminaría en un 400 al guardar.
   * Fail-closed: ante error queda vacía y el modal avisa que la granja no tiene silos.
   */
  private cargarSilosDeGranja(): void {
    if (!this.manejaPorSilo || !this.formFarmId) {
      this.silosDeGranja = [];
      return;
    }
    this.gestionInventarioSvc.getSilos(this.formFarmId).subscribe({
      next: silos => { this.silosDeGranja = silos ?? []; },
      error: () => { this.silosDeGranja = []; }
    });
  }

  /** Cambiar de silo cambia el saldo ofrecido: los ítems se recargan acotados a ESE silo. */
  async onSiloChange(): Promise<void> {
    await this.onConceptoChange();
  }

  async openDetail(id: number): Promise<void> {
    this.modalOpen = true;
    this.modalMode = 'detail';
    this.modalTitle = `Detalle gasto #${id}`;
    this.detail = null;
    try {
      this.detail = await firstValueFrom(this.api.getById(id));
    } catch (e: any) {
      this.detail = { error: e?.error?.error ?? 'No se pudo cargar el detalle.' };
    }
  }

  closeModal(): void {
    this.modalOpen = false;
    this.modalMode = null;
    this.detail = null;
    this.error = null;
  }

  async confirmarGuardar(): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: 'Confirmar registro',
      message: '¿Guardar el gasto y descontar el stock de la granja?',
      type: 'warning',
      confirmText: 'Guardar'
    });
    if (!ok) return;
    await this.save();
  }

  async confirmarEliminar(row: InventarioGastoListItemDto): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: 'Confirmar eliminación',
      message: '¿Eliminar el gasto y devolver el stock al inventario?',
      type: 'error',
      confirmText: 'Eliminar'
    });
    if (!ok) return;
    await this.eliminar(row);
  }

  /** Limpia los filtros (granja/corrida/estado/rango) y recarga. El estado vuelve a «Activos». */
  limpiarFiltros(): void {
    this.selectedFarmId = null;
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.selectedEstado = 'Activo';
    this.fechaDesde = null;
    this.fechaHasta = null;
    this.refresh();
  }

  async onConceptoChange(): Promise<void> {
    this.items = [];
    this.selectedItemId = null;
    this.selectedItem = null;
    this.qtyToAdd = null;
    if (!this.formFarmId || !this.formConcepto?.trim()) {
      return;
    }
    // Con el flag puesto, sin silo elegido no se piden ítems: el saldo que se mostraría sería el de
    // toda la granja y el gasto descuenta de UN silo.
    if (this.manejaPorSilo && !this.formSiloId) return;
    try {
      this.items = await firstValueFrom(this.api.getItems({
        farmId: this.formFarmId,
        concepto: this.formConcepto.trim(),
        siloId: this.manejaPorSilo ? this.formSiloId : null
      }));
    } catch {
      this.items = [];
    }
  }

  onItemChange(): void {
    this.selectedItem = this.items.find(i => i.itemInventarioEcuadorId === this.selectedItemId) ?? null;
    this.qtyToAdd = null;
  }

  addLinea(): void {
    if (!this.selectedItem || !this.qtyToAdd || this.qtyToAdd <= 0) return;
    if (this.manejaPorSilo && !this.formSiloId) {
      this.error = 'Seleccione el silo o la bodega de la que sale el insumo.';
      return;
    }
    const siloLinea = this.manejaPorSilo ? this.formSiloId : null;
    // La línea se identifica por (ítem, silo): el mismo insumo sacado de dos silos son dos líneas,
    // porque el backend descuenta una fila de stock distinta por cada una.
    const existing = this.lineas.find(l =>
      l.itemInventarioEcuadorId === this.selectedItem!.itemInventarioEcuadorId &&
      (l.siloId ?? null) === siloLinea);
    const stock = this.selectedItem.stockCantidad ?? 0;
    this.error = null;
    if (existing) {
      const next = existing.cantidad + this.qtyToAdd;
      if (next > stock) {
        this.error = `No puede consumir más de lo disponible. Stock: ${this.formatNum(stock, 3)} ${this.selectedItem.unidad}.`;
        return;
      }
      existing.cantidad = next;
      return;
    }
    if (this.qtyToAdd > stock) {
      this.error = `No puede consumir más de lo disponible. Stock: ${this.formatNum(stock, 3)} ${this.selectedItem.unidad}.`;
      return;
    }
    this.lineas.push({
      itemInventarioEcuadorId: this.selectedItem.itemInventarioEcuadorId,
      cantidad: this.qtyToAdd,
      siloId: siloLinea,
      codigo: this.selectedItem.codigo,
      nombre: this.selectedItem.nombre,
      unidad: this.selectedItem.unidad,
      stockCantidad: this.selectedItem.stockCantidad,
      siloNombre: this.silosDeGranja.find(s => s.id === siloLinea)?.nombre ?? null
    });
    this.toast.success('Ítem agregado.', 'Gasto inventario', 2500);
  }

  removeLinea(itemId: number, siloId: number | null = null): void {
    this.lineas = this.lineas.filter(l =>
      !(l.itemInventarioEcuadorId === itemId && (l.siloId ?? null) === (siloId ?? null)));
    this.toast.info('Ítem removido.', 'Gasto inventario', 2500);
  }

  async save(): Promise<void> {
    if (this.loading) return; // evita doble click/doble POST
    if (!this.formFarmId) {
      this.error = 'Seleccione una granja.';
      return;
    }
    if (!this.formLoteId && !this.formLoteBaseId) {
      this.error = this.programacionLotes
        ? 'Seleccione un lote (real o programado).'
        : 'Seleccione un lote.';
      return;
    }
    if (!this.formConcepto?.trim()) {
      this.error = 'Seleccione un concepto.';
      return;
    }
    if (!this.lineas.length) {
      this.error = 'Agregue al menos una línea.';
      return;
    }
    // Validación final de existencia (por UI; backend también valida stock real).
    for (const l of this.lineas) {
      const stock = l.stockCantidad ?? 0;
      if (l.cantidad > stock) {
        this.error = `La línea ${l.codigo} supera el stock disponible (${this.formatNum(stock, 3)} ${l.unidad}).`;
        return;
      }
    }

    this.loading = true;
    this.error = null;
    const payload: CreateInventarioGastoRequest = {
      farmId: this.formFarmId,
      nucleoId: this.formNucleoId,
      galponId: this.formGalponId,
      loteAveEngordeId: this.formLoteId,
      loteBaseEngordeId: this.formLoteBaseId,
      fecha: this.formFecha,
      observaciones: this.formObservaciones?.trim() || null,
      concepto: this.formConcepto.trim(),
      lineas: this.lineas.map(l => ({
        itemInventarioEcuadorId: l.itemInventarioEcuadorId,
        cantidad: l.cantidad,
        // El silo solo viaja cuando la empresa lo maneja: sin flag el payload es el de siempre.
        ...(l.siloId ? { siloId: l.siloId } : {})
      }))
    };
    try {
      await firstValueFrom(this.api.create(payload));
      this.closeModal();
      this.toast.success('Gasto registrado y stock descontado.', 'Éxito');
      await this.refresh();
    } catch (e: any) {
      this.error = e?.error?.message ?? e?.error?.error ?? 'No se pudo registrar el gasto.';
      this.toast.error(this.error ?? 'No se pudo registrar el gasto.', 'Error');
    } finally {
      this.loading = false;
    }
  }

  async eliminar(row: InventarioGastoListItemDto): Promise<void> {
    if (this.loading) return;
    const motivo = `Eliminación desde UI (gasto #${row.id})`;
    this.loading = true;
    this.error = null;
    try {
      await firstValueFrom(this.api.delete(row.id, motivo));
      this.toast.success('Gasto eliminado y stock devuelto.', 'Éxito');
      await this.refresh();
    } catch (e: any) {
      this.error = e?.error?.error ?? 'No se pudo eliminar el gasto.';
      this.toast.error(this.error ?? 'No se pudo eliminar el gasto.', 'Error');
    } finally {
      this.loading = false;
    }
  }

  formatNum(v: number | null | undefined, decimals = 2): string {
    if (v == null || Number.isNaN(v)) return '—';
    return Number(v).toFixed(decimals);
  }

  /**
   * Descarga el reporte `.xlsx` de dos hojas: **Consumos** (sin eliminados — el backend los excluye)
   * y **Existencias** (todo el catálogo, tenga o no consumo). Las dos consultas van en paralelo y el
   * armado del libro lo hace la función pura de `funciones/`.
   *
   * El **rango de fechas de la pantalla viaja a las dos hojas**: la de Consumos trae solo las líneas
   * del período y la de Existencias acota «Consumido en el rango» al mismo período (el saldo actual
   * sigue siendo el de hoy). Sin rango, se descarga todo el histórico como siempre.
   */
  async exportExcel(): Promise<void> {
    if (this.exporting) return;
    const rangoErr = this.rangoError;
    if (rangoErr) {
      this.error = rangoErr;
      this.toast.error(rangoErr, 'Exportar');
      return;
    }
    this.exporting = true;
    this.error = null;
    try {
      const filtrosComunes = {
        farmId: this.selectedFarmId ?? undefined,
        nucleoId: this.selectedNucleoId ?? undefined,
        galponId: this.selectedGalponId ?? undefined,
        loteAveEngordeId: this.selectedLoteId ?? undefined,
        fechaDesde: this.fechaDesde ?? undefined,
        fechaHasta: this.fechaHasta ?? undefined
      };
      const [consumos, existencias] = await Promise.all([
        firstValueFrom(this.api.export(filtrosComunes)),
        firstValueFrom(this.api.existencias({
          farmId: this.selectedFarmId ?? undefined,
          fechaDesde: this.fechaDesde ?? undefined,
          fechaHasta: this.fechaHasta ?? undefined
        }))
      ]);

      exportarGastosInventarioExcel(consumos ?? [], existencias ?? [], {
        granjaNombre: this.selectedFarmId ? (consumos?.[0]?.granjaNombre ?? existencias?.[0]?.granjaNombre ?? null) : null,
        nucleoNombre: this.selectedNucleoId,
        galponNombre: this.selectedGalponId,
        loteNombre: this.list.find(r => r.loteAveEngordeId === this.selectedLoteId)?.loteNombre ?? null,
        fechaDesde: this.fechaDesde,
        fechaHasta: this.fechaHasta
      });
      const periodo = this.rangoActivo
        ? ` (${this.fechaDesde || 'inicio'} a ${this.fechaHasta || 'hoy'})`
        : ' (todo el histórico)';
      this.toast.success(
        `Se exportaron ${consumos?.length ?? 0} consumo(s) y ${existencias?.length ?? 0} existencia(s)${periodo}.`,
        'Exportar'
      );
    } catch (e: any) {
      const msg = e?.error?.error ?? e?.message ?? 'No se pudo exportar.';
      this.error = msg;
      this.toast.error(msg, 'Exportar');
    } finally {
      this.exporting = false;
    }
  }
}
