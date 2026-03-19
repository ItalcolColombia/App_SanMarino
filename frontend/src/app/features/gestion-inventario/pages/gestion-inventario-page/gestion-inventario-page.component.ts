// src/app/features/gestion-inventario/pages/gestion-inventario-page/gestion-inventario-page.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faBoxesStacked, faArrowDown, faArrowRight, faList, faBook, faFilter, faClockRotateLeft } from '@fortawesome/free-solid-svg-icons';

import { GestionInventarioService, InventarioGestionFilterDataDto, InventarioGestionStockDto, InventarioGestionMovimientoDto, ItemInventarioEcuadorDto } from '../../services/gestion-inventario.service';

type TabKey = 'stock' | 'ingresos' | 'traslados' | 'historico' | 'items';

@Component({
  selector: 'app-gestion-inventario-page',
  standalone: true,
  imports: [CommonModule, FormsModule, FontAwesomeModule],
  templateUrl: './gestion-inventario-page.component.html',
  styleUrls: ['./gestion-inventario-page.component.scss']
})
export class GestionInventarioPageComponent implements OnInit {
  faStock = faBoxesStacked;
  faIngreso = faArrowDown;
  faTraslado = faArrowRight;
  faList = faList;
  faBook = faBook;
  faFilter = faFilter;
  faHistorico = faClockRotateLeft;

  activeTab: TabKey = 'stock';
  filterData: InventarioGestionFilterDataDto | null = null;
  stockList: InventarioGestionStockDto[] = [];
  movimientosList: InventarioGestionMovimientoDto[] = [];
  itemsList: ItemInventarioEcuadorDto[] = [];
  loading = false;

  // Modales (confirmación y mensajes)
  showConfirmModal = false;
  confirmTitle = '';
  confirmText = '';
  confirmAction: 'ingreso' | 'traslado' | null = null;
  showAlertModal = false;
  alertType: 'success' | 'error' = 'success';
  alertTitle = '';
  alertText = '';

  // Filtros (para stock y formularios)
  selectedFarmId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  // Concepto dinámico (desde item_inventario_ecuador.concepto)
  selectedConcept: string = '';
  conceptos: string[] = [];
  searchTerm = '';

  // Form ingreso
  ingresoFarmId: number | null = null;
  ingresoNucleoId: string | null = null;
  ingresoGalponId: string | null = null;
  ingresoItemInventarioEcuadorId: number | null = null;
  ingresoQuantity = 0;
  ingresoReference = '';
  ingresoReason = '';
  /** Origen del ingreso: Planta (motivo fijo) o Granja (origen desde otra granja). */
  ingresoOrigenTipo: 'planta' | 'granja' = 'planta';
  /** Granja de origen cuando ingresoOrigenTipo === 'granja'. */
  ingresoOrigenFarmId: number | null = null;
  ingresoItems: ItemInventarioEcuadorDto[] = [];
  submittingIngreso = false;

  // Form traslado
  fromFarmId: number | null = null;
  fromNucleoId: string | null = null;
  fromGalponId: string | null = null;
  toFarmId: number | null = null;
  toNucleoId: string | null = null;
  toGalponId: string | null = null;
  trasladoItemInventarioEcuadorId: number | null = null;
  trasladoQuantity = 0;
  trasladoReference = '';
  trasladoReason = '';
  trasladoItems: ItemInventarioEcuadorDto[] = [];
  submittingTraslado = false;
  /** Stock disponible en origen (para el ítem seleccionado). null = no cargado o sin stock. */
  originStockQuantity: number | null = null;
  loadingOriginStock = false;

  /** Destino del traslado para estado: granja | planta */
  trasladoDestinoTipo: 'granja' | 'planta' = 'granja';

  // Histórico tab
  historicoFarmId: number | null = null;
  historicoFechaDesde = '';
  historicoFechaHasta = '';
  historicoEstado = '';

  // Items tab
  itemsFilterType: string = ''; // concepto
  itemsSearch = '';

  private allCatalogItems: ItemInventarioEcuadorDto[] = [];

  constructor(private svc: GestionInventarioService) {}

  ngOnInit(): void {
    this.loadFilterData();
    this.loadCatalogItems();
  }

  setTab(tab: TabKey): void {
    this.activeTab = tab;
    this.closeAlertModal();
    if (tab === 'stock') this.loadStock();
    if (tab === 'historico') this.loadMovimientos();
    if (tab === 'items') this.loadItemsList();
  }

  readonly estadosHistorial: string[] = ['Entrada planta', 'Entrada granja', 'Transferencia a granja', 'Transferencia a planta', 'Consumo'];

  loadMovimientos(): void {
    this.loading = true;
    const params: { farmId?: number; fechaDesde?: string; fechaHasta?: string; estado?: string } = {};
    if (this.historicoFarmId != null) params.farmId = this.historicoFarmId;
    if (this.historicoFechaDesde) params.fechaDesde = this.historicoFechaDesde;
    if (this.historicoFechaHasta) params.fechaHasta = this.historicoFechaHasta;
    if (this.historicoEstado) params.estado = this.historicoEstado;
    this.svc.getMovimientos(params).subscribe({
      next: (list) => {
        this.movimientosList = list ?? [];
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.openAlertModal('error', 'Error', 'Error al cargar histórico.');
      }
    });
  }

  formatFechaMovimiento(iso: string): string {
    if (!iso) return '—';
    const d = new Date(iso);
    return isNaN(d.getTime()) ? iso : d.toLocaleDateString('es', { dateStyle: 'short' }) + ' ' + d.toLocaleTimeString('es', { hour: '2-digit', minute: '2-digit' });
  }

  loadFilterData(): void {
    this.loading = true;
    this.svc.getFilterData().subscribe({
      next: (data) => {
        this.filterData = data;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.openAlertModal('error', 'Error', 'Error al cargar filtros.');
      }
    });
  }

  loadStock(): void {
    if (!this.filterData) return;
    this.loading = true;
    const params: any = {};
    if (this.selectedFarmId != null) params.farmId = this.selectedFarmId;
    if (this.selectedNucleoId) params.nucleoId = this.selectedNucleoId;
    if (this.selectedGalponId) params.galponId = this.selectedGalponId;
    if (this.selectedConcept) params.itemType = this.selectedConcept;
    if (this.searchTerm) params.search = this.searchTerm;
    this.svc.getStock(params).subscribe({
      next: (list) => {
        this.stockList = list;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.openAlertModal('error', 'Error', 'Error al cargar stock.');
      }
    });
  }

  get nucleosFiltered(): { nucleoId: string; nucleoNombre: string }[] {
    if (!this.filterData || this.selectedFarmId == null) return [];
    return this.filterData.nucleos
      .filter(n => n.granjaId === this.selectedFarmId)
      .map(n => ({ nucleoId: n.nucleoId, nucleoNombre: n.nucleoNombre }));
  }

  get galponesFiltered(): { galponId: string; galponNombre: string }[] {
    if (!this.filterData || this.selectedFarmId == null) return [];
    return this.filterData.galpones
      .filter(g => g.granjaId === this.selectedFarmId && (!this.selectedNucleoId || g.nucleoId === this.selectedNucleoId))
      .map(g => ({ galponId: g.galponId, galponNombre: g.galponNombre }));
  }

  fromNucleosFiltered(): { nucleoId: string; nucleoNombre: string }[] {
    if (!this.filterData || this.fromFarmId == null) return [];
    return this.filterData.nucleos
      .filter(n => n.granjaId === this.fromFarmId)
      .map(n => ({ nucleoId: n.nucleoId, nucleoNombre: n.nucleoNombre }));
  }
  fromGalponesFiltered(): { galponId: string; galponNombre: string }[] {
    if (!this.filterData || this.fromFarmId == null) return [];
    return this.filterData.galpones
      .filter(g => g.granjaId === this.fromFarmId && (!this.fromNucleoId || g.nucleoId === this.fromNucleoId))
      .map(g => ({ galponId: g.galponId, galponNombre: g.galponNombre }));
  }
  toNucleosFiltered(): { nucleoId: string; nucleoNombre: string }[] {
    if (!this.filterData || this.toFarmId == null) return [];
    return this.filterData.nucleos
      .filter(n => n.granjaId === this.toFarmId)
      .map(n => ({ nucleoId: n.nucleoId, nucleoNombre: n.nucleoNombre }));
  }
  toGalponesFiltered(): { galponId: string; galponNombre: string }[] {
    if (!this.filterData || this.toFarmId == null) return [];
    return this.filterData.galpones
      .filter(g => g.granjaId === this.toFarmId && (!this.toNucleoId || g.nucleoId === this.toNucleoId))
      .map(g => ({ galponId: g.galponId, galponNombre: g.galponNombre }));
  }

  ingresoNucleosFiltered(): { nucleoId: string; nucleoNombre: string }[] {
    if (!this.filterData || this.ingresoFarmId == null) return [];
    return this.filterData.nucleos
      .filter(n => n.granjaId === this.ingresoFarmId)
      .map(n => ({ nucleoId: n.nucleoId, nucleoNombre: n.nucleoNombre }));
  }
  ingresoGalponesFiltered(): { galponId: string; galponNombre: string }[] {
    if (!this.filterData || this.ingresoFarmId == null) return [];
    return this.filterData.galpones
      .filter(g => g.granjaId === this.ingresoFarmId && (!this.ingresoNucleoId || g.nucleoId === this.ingresoNucleoId))
      .map(g => ({ galponId: g.galponId, galponNombre: g.galponNombre }));
  }

  get showNucleoGalpon(): boolean {
    return this.isAlimentoConcept(this.selectedConcept);
  }

  onFarmChange(): void {
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
  }
  onNucleoChange(): void {
    this.selectedGalponId = null;
  }

  private applyConceptToIngresoTrasladoLists(): void {
    const filtered = this.filterByConcept(this.allCatalogItems, this.selectedConcept);
    this.ingresoItems = filtered;
    this.trasladoItems = filtered;

    if (this.ingresoItemInventarioEcuadorId != null && !filtered.some(x => x.id === this.ingresoItemInventarioEcuadorId)) {
      this.ingresoItemInventarioEcuadorId = null;
    }
    if (this.trasladoItemInventarioEcuadorId != null && !filtered.some(x => x.id === this.trasladoItemInventarioEcuadorId)) {
      this.trasladoItemInventarioEcuadorId = null;
    }
  }

  onItemTypeChange(): void {
    this.applyConceptToIngresoTrasladoLists();
    if (!this.showNucleoGalpon) {
      this.ingresoNucleoId = null;
      this.ingresoGalponId = null;
      this.fromNucleoId = null;
      this.fromGalponId = null;
      this.toNucleoId = null;
      this.toGalponId = null;
    }
  }

  submitIngreso(): void {
    if (this.ingresoFarmId == null || this.ingresoItemInventarioEcuadorId == null || this.ingresoQuantity <= 0) {
      this.openAlertModal('error', 'Validación', 'Complete granja, ítem y cantidad.');
      return;
    }
    if (this.showNucleoGalpon && (!this.ingresoNucleoId || !this.ingresoGalponId)) {
      this.openAlertModal('error', 'Validación', 'Para alimento debe seleccionar Núcleo y Galpón.');
      return;
    }
    this.openConfirmModal(
      'Confirmar ingreso',
      `¿Registrar ingreso de ${this.ingresoQuantity} ${this.ingresoSelectedUnit}?`,
      'ingreso'
    );
  }

  submitTraslado(): void {
    if (this.fromFarmId == null || this.toFarmId == null || this.trasladoItemInventarioEcuadorId == null || this.trasladoQuantity <= 0) {
      this.openAlertModal('error', 'Validación', 'Complete origen, destino, ítem y cantidad.');
      return;
    }
    if (this.showNucleoGalpon) {
      if (!this.fromNucleoId || !this.fromGalponId || !this.toNucleoId || !this.toGalponId) {
        this.openAlertModal('error', 'Validación', 'Para alimento debe seleccionar Núcleo y Galpón en origen y destino.');
        return;
      }
    }
    const available = this.originStockQuantity;
    if (available == null || available <= 0) {
      this.openAlertModal(
        'error',
        'Sin stock en origen',
        'El producto no tiene existencia (stock en cero) en el origen. Realice un ingreso y valide que tenga existencia en el sistema.'
      );
      return;
    }
    if (this.trasladoQuantity > available) {
      this.openAlertModal(
        'error',
        'Stock insuficiente',
        `La cantidad no puede ser mayor al stock en origen (${available} ${this.trasladoSelectedUnit}).`
      );
      return;
    }
    this.openConfirmModal(
      'Confirmar traslado',
      `¿Registrar traslado de ${this.trasladoQuantity} ${this.trasladoSelectedUnit}?`,
      'traslado'
    );
  }

  /** Carga el stock en origen para el ítem seleccionado (para mostrar disponible y validar). */
  loadOriginStock(): void {
    const itemId = this.trasladoItemInventarioEcuadorId;
    if (this.fromFarmId == null || itemId == null) {
      this.originStockQuantity = null;
      return;
    }
    if (this.showNucleoGalpon && (!this.fromNucleoId || !this.fromGalponId)) {
      this.originStockQuantity = null;
      return;
    }
    this.loadingOriginStock = true;
    this.originStockQuantity = null;
    const params: any = { farmId: this.fromFarmId };
    if (this.showNucleoGalpon) {
      params.nucleoId = this.fromNucleoId;
      params.galponId = this.fromGalponId;
    }
    this.svc.getStock(params).subscribe({
      next: (list) => {
        const row = list.find(s => s.itemInventarioEcuadorId === itemId);
        this.originStockQuantity = row ? Number(row.quantity) : 0;
        this.loadingOriginStock = false;
      },
      error: () => {
        this.originStockQuantity = null;
        this.loadingOriginStock = false;
      }
    });
  }

  loadItemsList(): void {
    const base = this.itemsSearch ? this.itemsSearch : null;
    // Para catálogo, traemos todo y filtramos por concepto para tener la lista dinámica
    const list = this.filterByConcept(this.allCatalogItems, this.itemsFilterType);
    const q = (base ?? '').trim().toLowerCase();
    this.itemsList = !q
      ? list
      : list.filter(i => (i.codigo ?? '').toLowerCase().includes(q) || (i.nombre ?? '').toLowerCase().includes(q));
  }

  get farms(): { id: number; name: string }[] {
    return this.filterData?.farms?.map(f => ({ id: f.id, name: f.name })) ?? [];
  }

  /** Motivo cuando origen es Planta (solo lectura). */
  get ingresoReasonPlanta(): string {
    return 'Llegada a planta';
  }

  /** Unidad del ítem de ingreso seleccionado (solo lectura). */
  get ingresoSelectedUnit(): string {
    if (this.ingresoItemInventarioEcuadorId == null) return '—';
    const item = this.ingresoItems.find(i => i.id === this.ingresoItemInventarioEcuadorId);
    return (item?.unidad ?? 'kg').trim() || '—';
  }

  /** Unidad del ítem de traslado seleccionado (solo lectura). */
  get trasladoSelectedUnit(): string {
    if (this.trasladoItemInventarioEcuadorId == null) return '—';
    const item = this.trasladoItems.find(i => i.id === this.trasladoItemInventarioEcuadorId);
    return (item?.unidad ?? 'kg').trim() || '—';
  }

  private loadCatalogItems(): void {
    this.svc.getItemsByType(null, null, true).subscribe({
      next: (list) => {
        this.allCatalogItems = list ?? [];
        this.conceptos = Array.from(
          new Set(
            this.allCatalogItems
              .map(i => (i.concepto ?? i.tipoItem ?? '').trim())
              .filter(x => !!x)
          )
        ).sort((a, b) => a.localeCompare(b));

        if (!this.selectedConcept) {
          const prefer = this.conceptos.find(c => this.isAlimentoConcept(c));
          this.selectedConcept = prefer ?? (this.conceptos[0] ?? '');
        }
        if (!this.itemsFilterType) this.itemsFilterType = '';

        this.applyConceptToIngresoTrasladoLists();
        this.loadItemsList();
      },
      error: () => {
        this.openAlertModal('error', 'Error', 'No se pudo cargar el catálogo de ítems.');
      }
    });
  }

  private isAlimentoConcept(concept: string | null | undefined): boolean {
    return (concept ?? '').trim().toLowerCase() === 'alimento';
  }

  private filterByConcept(items: ItemInventarioEcuadorDto[], concept: string | null | undefined): ItemInventarioEcuadorDto[] {
    const c = (concept ?? '').trim().toLowerCase();
    if (!c) return items;
    return items.filter(i => ((i.concepto ?? i.tipoItem ?? '').trim().toLowerCase() === c));
  }

  // ===== Modales =====
  openConfirmModal(title: string, text: string, action: 'ingreso' | 'traslado'): void {
    this.confirmTitle = title;
    this.confirmText = text;
    this.confirmAction = action;
    this.showConfirmModal = true;
  }
  closeConfirmModal(): void {
    this.showConfirmModal = false;
    this.confirmAction = null;
  }
  confirm(): void {
    const action = this.confirmAction;
    this.closeConfirmModal();
    if (action === 'ingreso') this.doIngreso();
    if (action === 'traslado') this.doTraslado();
  }

  openAlertModal(type: 'success' | 'error', title: string, text: string): void {
    this.alertType = type;
    this.alertTitle = title;
    this.alertText = text;
    this.showAlertModal = true;
  }
  closeAlertModal(): void {
    this.showAlertModal = false;
  }

  private doIngreso(): void {
    let reason: string | null;
    if (this.ingresoOrigenTipo === 'planta') {
      reason = this.ingresoReasonPlanta;
    } else {
      const fromFarmName = this.ingresoOrigenFarmId != null ? this.farms.find(f => f.id === this.ingresoOrigenFarmId)?.name : null;
      reason = fromFarmName ? `Ingreso desde granja: ${fromFarmName}${this.ingresoReason ? '. ' + this.ingresoReason : ''}` : (this.ingresoReason || null);
    }
    this.submittingIngreso = true;
    this.svc.registrarIngreso({
      farmId: this.ingresoFarmId!,
      nucleoId: this.showNucleoGalpon ? this.ingresoNucleoId : null,
      galponId: this.showNucleoGalpon ? this.ingresoGalponId : null,
      itemInventarioEcuadorId: this.ingresoItemInventarioEcuadorId!,
      quantity: this.ingresoQuantity,
      unit: this.ingresoSelectedUnit === '—' ? 'kg' : this.ingresoSelectedUnit,
      reference: this.ingresoReference || null,
      reason,
      origenTipo: this.ingresoOrigenTipo
    }).subscribe({
      next: () => {
        this.submittingIngreso = false;
        this.openAlertModal('success', 'Listo', 'Ingreso registrado correctamente.');
        this.ingresoQuantity = 0;
        this.ingresoReference = '';
        this.ingresoReason = '';
        this.loadStock();
      },
      error: (err) => {
        this.submittingIngreso = false;
        this.openAlertModal('error', 'Error', err.error?.message || 'Error al registrar ingreso.');
      }
    });
  }

  private doTraslado(): void {
    this.submittingTraslado = true;
    this.svc.registrarTraslado({
      fromFarmId: this.fromFarmId!,
      fromNucleoId: this.showNucleoGalpon ? this.fromNucleoId : null,
      fromGalponId: this.showNucleoGalpon ? this.fromGalponId : null,
      toFarmId: this.toFarmId!,
      toNucleoId: this.showNucleoGalpon ? this.toNucleoId : null,
      toGalponId: this.showNucleoGalpon ? this.toGalponId : null,
      itemInventarioEcuadorId: this.trasladoItemInventarioEcuadorId!,
      quantity: this.trasladoQuantity,
      unit: this.trasladoSelectedUnit === '—' ? 'kg' : this.trasladoSelectedUnit,
      reference: this.trasladoReference || null,
      reason: this.trasladoReason || null,
      destinoTipo: this.trasladoDestinoTipo
    }).subscribe({
      next: () => {
        this.submittingTraslado = false;
        this.openAlertModal('success', 'Listo', 'Traslado registrado correctamente.');
        this.trasladoQuantity = 0;
        this.trasladoReference = '';
        this.trasladoReason = '';
        this.loadStock();
      },
      error: (err) => {
        this.submittingTraslado = false;
        this.openAlertModal('error', 'Error', err.error?.message || 'Error al registrar traslado.');
      }
    });
  }
}
