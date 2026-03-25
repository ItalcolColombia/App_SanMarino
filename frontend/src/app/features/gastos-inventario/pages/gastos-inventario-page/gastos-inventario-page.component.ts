import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { FiltroSelectComponent, FilterDataResponse } from '../../../lote-levante/pages/filtro-select/filtro-select.component';
import { ToastService } from '../../../../shared/services/toast.service';
import {
  CreateInventarioGastoRequest,
  InventarioGastoExportRowDto,
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
  fechaDesde: string | null = null;
  fechaHasta: string | null = null;
  conceptoFilter: string = '';
  estadoFilter: '' | 'Activo' | 'Eliminado' = '';
  searchTerm: string = '';

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
  formFecha: string = this.todayYmd();
  formConcepto: string = '';
  formObservaciones: string = '';

  items: InventarioGastoItemStockDto[] = [];
  selectedItemId: number | null = null;
  selectedItem: InventarioGastoItemStockDto | null = null;
  qtyToAdd: number | null = null;

  lineas: Array<InventarioGastoLineaRequest & { codigo: string; nombre: string; unidad: string; stockCantidad: number }> = [];

  // Detail modal
  detail: any = null;

  // Confirm modal
  confirmOpen = false;
  confirmTitle = '';
  confirmText = '';
  confirmAction: 'save' | 'delete' | null = null;
  confirmRow: InventarioGastoListItemDto | null = null;

  constructor(private api: InventarioGastosService, private toast: ToastService) {}

  get qtyExceedsStock(): boolean {
    if (!this.selectedItem || this.qtyToAdd == null) return false;
    const qty = Number(this.qtyToAdd);
    if (!Number.isFinite(qty) || qty <= 0) return false;
    return qty > (this.selectedItem.stockCantidad ?? 0);
  }

  async ngOnInit(): Promise<void> {
    await this.loadConceptos();
    await this.refresh();
  }

  private todayYmd(): string {
    const d = new Date();
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  async loadConceptos(): Promise<void> {
    try {
      this.conceptos = await firstValueFrom(this.api.getConceptos());
    } catch {
      this.conceptos = [];
    }
  }

  async refresh(): Promise<void> {
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
          concepto: this.conceptoFilter?.trim() || undefined,
          search: this.searchTerm?.trim() || undefined,
          estado: this.estadoFilter || undefined
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
    this.formNucleoId = this.selectedNucleoId;
    this.formGalponId = this.selectedGalponId;
    this.formLoteId = this.selectedLoteId;
    this.formFecha = this.todayYmd();
    this.formConcepto = '';
    this.formObservaciones = '';
    this.items = [];
    this.selectedItemId = null;
    this.selectedItem = null;
    this.qtyToAdd = null;
    this.lineas = [];
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

  openConfirm(title: string, text: string, action: 'save' | 'delete', row?: InventarioGastoListItemDto): void {
    this.confirmTitle = title;
    this.confirmText = text;
    this.confirmAction = action;
    this.confirmRow = row ?? null;
    this.confirmOpen = true;
  }

  closeConfirm(): void {
    this.confirmOpen = false;
    this.confirmAction = null;
    this.confirmRow = null;
  }

  async confirmYes(): Promise<void> {
    const action = this.confirmAction;
    const row = this.confirmRow;
    this.closeConfirm();
    if (action === 'save') {
      await this.save();
    }
    if (action === 'delete' && row) {
      await this.eliminar(row);
    }
  }

  async onConceptoChange(): Promise<void> {
    this.items = [];
    this.selectedItemId = null;
    this.selectedItem = null;
    this.qtyToAdd = null;
    if (!this.formFarmId || !this.formConcepto?.trim()) {
      return;
    }
    try {
      this.items = await firstValueFrom(this.api.getItems({ farmId: this.formFarmId, concepto: this.formConcepto.trim() }));
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
    const existing = this.lineas.find(l => l.itemInventarioEcuadorId === this.selectedItem!.itemInventarioEcuadorId);
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
      codigo: this.selectedItem.codigo,
      nombre: this.selectedItem.nombre,
      unidad: this.selectedItem.unidad,
      stockCantidad: this.selectedItem.stockCantidad
    });
    this.toast.success('Ítem agregado.', 'Gasto inventario', 2500);
  }

  removeLinea(itemId: number): void {
    this.lineas = this.lineas.filter(l => l.itemInventarioEcuadorId !== itemId);
    this.toast.info('Ítem removido.', 'Gasto inventario', 2500);
  }

  async save(): Promise<void> {
    if (this.loading) return; // evita doble click/doble POST
    if (!this.formFarmId) {
      this.error = 'Seleccione una granja.';
      return;
    }
    if (!this.formLoteId) {
      this.error = 'Seleccione un lote.';
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
      fecha: this.formFecha,
      observaciones: this.formObservaciones?.trim() || null,
      concepto: this.formConcepto.trim(),
      lineas: this.lineas.map(l => ({ itemInventarioEcuadorId: l.itemInventarioEcuadorId, cantidad: l.cantidad }))
    };
    try {
      await firstValueFrom(this.api.create(payload));
      this.closeModal();
      this.toast.success('Gasto registrado y stock descontado.', 'Éxito');
      await this.refresh();
    } catch (e: any) {
      this.error = e?.error?.error ?? 'No se pudo registrar el gasto.';
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

  exportExcel(): void {
    if (this.exporting) return;
    this.exporting = true;
    this.error = null;
    this.api
      .export({
        farmId: this.selectedFarmId ?? undefined,
        nucleoId: this.selectedNucleoId ?? undefined,
        galponId: this.selectedGalponId ?? undefined,
        loteAveEngordeId: this.selectedLoteId ?? undefined,
        fechaDesde: this.fechaDesde ?? undefined,
        fechaHasta: this.fechaHasta ?? undefined,
        concepto: this.conceptoFilter?.trim() || undefined,
        search: this.searchTerm?.trim() || undefined,
        estado: this.estadoFilter || undefined
      })
      .subscribe({
        next: rows => {
          this.exporting = false;
          const csv = this.buildGastosExportCsv(rows ?? []);
          const blob = new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8' });
          const a = document.createElement('a');
          a.href = URL.createObjectURL(blob);
          a.download = `gastos-inventario-${new Date().toISOString().slice(0, 10)}.csv`;
          a.click();
          URL.revokeObjectURL(a.href);
          this.toast.success(`Se exportaron ${rows?.length ?? 0} fila(s).`, 'Exportar');
        },
        error: (e: any) => {
          this.exporting = false;
          const msg = e?.error?.error ?? e?.message ?? 'No se pudo exportar.';
          this.error = msg;
          this.toast.error(msg, 'Exportar');
        }
      });
  }

  private buildGastosExportCsv(rows: InventarioGastoExportRowDto[]): string {
    const esc = (v: string | number | null | undefined): string => {
      const t = v == null ? '' : String(v);
      if (/[",\n\r]/.test(t)) return `"${t.replace(/"/g, '""')}"`;
      return t;
    };
    const headers = [
      'Fecha',
      'Granja',
      'Galpón',
      'Lote',
      'Código ítem',
      'Nombre ítem',
      'Tipo ítem',
      'Cantidad',
      'Unidad',
      'Stock antes',
      'Stock después',
      'Fecha registro'
    ];
    const lines = [headers.join(',')];
    for (const r of rows) {
      lines.push(
        [
          esc(r.fecha?.slice?.(0, 10) ?? r.fecha),
          esc(r.granjaNombre),
          esc(r.galponNombre),
          esc(r.loteNombre),
          esc(r.itemCodigo),
          esc(r.itemNombre),
          esc(r.itemTipo),
          esc(r.cantidad),
          esc(r.unidad),
          esc(r.stockAntes),
          esc(r.stockDespues),
          esc(r.createdAt)
        ].join(',')
      );
    }
    return lines.join('\r\n');
  }
}

