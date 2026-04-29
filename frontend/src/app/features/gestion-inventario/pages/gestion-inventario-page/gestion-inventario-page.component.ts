// src/app/features/gestion-inventario/pages/gestion-inventario-page/gestion-inventario-page.component.ts
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import {
  faBoxesStacked,
  faArrowDown,
  faArrowRight,
  faList,
  faBook,
  faFilter,
  faClockRotateLeft,
  faTruck,
  faFileExport,
  faPen,
  faTrash,
  faEye,
  faChevronDown,
  faChevronUp
} from '@fortawesome/free-solid-svg-icons';

import { HasPermissionDirective } from '../../../../core/auth/has-permission.directive';
import {
  GestionInventarioService,
  InventarioGestionFilterDataDto,
  InventarioGestionHistoricoFiltrosDto,
  InventarioGestionStockDto,
  InventarioGestionMovimientoDto,
  ItemInventarioEcuadorDto,
  InventarioGestionTransitoPendienteDto
} from '../../services/gestion-inventario.service';

type TabKey = 'stock' | 'ingresos' | 'traslados' | 'transito' | 'historico' | 'items';
type TrasladoModo = 'mismaGranja' | 'interGranja';

@Component({
  selector: 'app-gestion-inventario-page',
  standalone: true,
  imports: [CommonModule, FormsModule, FontAwesomeModule, HasPermissionDirective],
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
  faTransito = faTruck;
  faExport = faFileExport;
  faPen = faPen;
  faTrash = faTrash;
  faEye = faEye;
  faChevronDown = faChevronDown;
  faChevronUp = faChevronUp;

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
  confirmAction: 'ingreso' | 'traslado' | 'recepcionTransito' | 'deleteStock' | 'anularMovHistorico' | null = null;
  showAlertModal = false;
  alertType: 'success' | 'error' = 'success';
  alertTitle = '';
  alertText = '';

  // Filtros (para stock y formularios)
  selectedFarmId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  // Concepto dinámico (desde item_inventario_ecuador.concepto) — ingresos/traslados/catálogo
  selectedConcept: string = '';
  /** Filtro de concepto solo en pestaña Stock (vacío = todos los conceptos). */
  stockConceptFilter = '';
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
  /** Origen del ingreso (solo concepto Alimento): planta | granja | bodega. */
  ingresoOrigenTipo: 'planta' | 'granja' | 'bodega' = 'planta';
  /** Granja de origen (otra granja) o granja de la bodega de procedencia. */
  ingresoOrigenFarmId: number | null = null;
  /** Texto opcional: bodega / referencia cuando origen es bodega. */
  ingresoOrigenBodegaTexto = '';
  ingresoItems: ItemInventarioEcuadorDto[] = [];
  submittingIngreso = false;
  /** Fecha del ingreso (yyyy-MM-dd) para el histórico; se define en el modal. */
  ingresoFechaMovimiento = '';
  showIngresoFechaModal = false;
  ingresoFechaDraft = '';

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
  /** Fecha del traslado (yyyy-MM-dd) para el histórico; por defecto hoy. */
  trasladoFechaMovimiento = '';
  trasladoItems: ItemInventarioEcuadorDto[] = [];
  submittingTraslado = false;
  /** Stock disponible en origen (para el ítem seleccionado). null = no cargado o sin stock. */
  originStockQuantity: number | null = null;
  loadingOriginStock = false;

  /** Destino del traslado para estado: granja | planta */
  trasladoDestinoTipo: 'granja' | 'planta' = 'granja';

  /** Misma granja (solo alimento, galpones distintos) vs otra granja (stock pasa a tránsito). */
  trasladoModo: TrasladoModo = 'mismaGranja';

  // Tránsito (recepción inter-granja)
  transitoList: InventarioGestionTransitoPendienteDto[] = [];
  transitoFarmFilter: number | null = null;
  loadingTransito = false;
  /** Fila seleccionada para completar recepción en destino. */
  recepcionPendiente: InventarioGestionTransitoPendienteDto | null = null;
  recepcionToNucleoId: string | null = null;
  recepcionToGalponId: string | null = null;
  submittingRecepcion = false;

  // Histórico tab
  historicoFarmId: number | null = null;
  /** Filtro por ubicación (inventario por granja EC/PA). */
  historicoNucleoId: string | null = null;
  historicoGalponId: string | null = null;
  /** Si > 0, el API filtra por lote (y deriva granja/núcleo/galpón). */
  historicoLoteId: number | null = null;
  historicoFechaDesde = '';
  historicoFechaHasta = '';
  historicoEstado = '';
  /** Búsqueda por código o nombre de ítem (catálogo Ecuador). */
  historicoItemSearch = '';
  /** Filtro exacto por concepto del ítem (valores ya presentes en histórico). */
  historicoConcepto = '';
  /** Filtro exacto por tipo de ítem en catálogo (valores ya presentes en histórico). */
  historicoTipoItem = '';
  /** Metadatos para desplegables (lotes en granjas asignadas, conceptos/tipos/estados en movimientos). */
  historicoMeta: InventarioGestionHistoricoFiltrosDto | null = null;
  /** Detalle completo de una fila (modal). */
  historicoDetalleMov: InventarioGestionMovimientoDto | null = null;
  /** Filtro por movementType en BD (opcional; puede combinarse con tipo operación). */
  historicoMovementType = '';
  /** Etiqueta legible de operación (mapeada en API). */
  historicoTipoOperacion = '';
  historicoUnit = '';
  historicoReferenceContains = '';
  historicoReasonContains = '';
  historicoTransferGroupId = '';
  historicoItemInventarioEcuadorId: number | null = null;
  /** Ubicación de origen (traslados): requiere granja si hay núcleo/galpón. */
  historicoFromFarmId: number | null = null;
  historicoFromNucleoId: string | null = null;
  historicoFromGalponId: string | null = null;
  /** Panel colapsable: ubicación fina, catálogo exacto, texto, UUID, origen, tipo movimiento técnico. */
  historicoFiltrosAvanzadosAbiertos = false;

  // Items tab
  itemsFilterType: string = ''; // concepto
  itemsSearch = '';

  /** Stock: edición y eliminación */
  showStockEditModal = false;
  stockEditRow: InventarioGestionStockDto | null = null;
  stockEditQuantity = 0;
  stockEditUnit = '';
  /** Fecha de primer ingreso en ubicación (yyyy-MM-dd), editable en modal. */
  stockEditFechaIngreso = '';
  stockEditReason = '';
  submittingStockEdit = false;
  pendingDeleteStock: InventarioGestionStockDto | null = null;
  pendingAnularMovimiento: InventarioGestionMovimientoDto | null = null;

  private allCatalogItems: ItemInventarioEcuadorDto[] = [];

  constructor(
    private svc: GestionInventarioService,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.ingresoFechaMovimiento = this.todayYmd();
    this.trasladoFechaMovimiento = this.todayYmd();
    this.loadFilterData();
    this.loadCatalogItems();
  }

  setTab(tab: TabKey): void {
    this.activeTab = tab;
    this.closeAlertModal();
    if (tab === 'stock') this.loadStock();
    if (tab === 'historico') {
      this.loadHistoricoMeta();
      this.loadMovimientos();
    }
    if (tab === 'items') this.loadItemsList();
    if (tab === 'transito') this.loadTransitos();
  }

  readonly estadosHistorial: string[] = [
    'Entrada planta',
    'Entrada granja',
    'Entrada bodega',
    'Transferencia a granja',
    'Transferencia a planta',
    'Consumo',
    'Pendiente destino',
    'Tránsito',
    'Recibido desde tránsito',
    'Rechazado destino',
    'Ajuste manual',
    'Eliminación registro'
  ];

  /** Lotes en granjas asignadas + valores distintos de concepto/tipo/estado en movimientos. */
  private loadHistoricoMeta(): void {
    this.svc.getHistoricoFiltros().subscribe({
      next: (data) => {
        this.historicoMeta = data;
        const fo = data.farmsOrigen ?? [];
        const no = data.nucleosOrigen ?? [];
        const go = data.galponesOrigen ?? [];
        if (fo.length > 0) {
          this.filterData = this.filterData
            ? { ...this.filterData, farmsOrigen: fo, nucleosOrigen: no, galponesOrigen: go }
            : {
                farmsOrigen: fo,
                farmsDestino: fo,
                nucleosOrigen: no,
                nucleosDestino: [],
                galponesOrigen: go,
                galponesDestino: []
              };
        }
      },
      error: () => {
        this.historicoMeta = null;
      }
    });
  }

  /** Opciones de estado: valores ya guardados en BD + lista de referencia para etiquetas habituales. */
  get historicoEstadosOptions(): string[] {
    const api = this.historicoMeta?.estadosEnHistorico ?? [];
    const set = new Set<string>([...api, ...this.estadosHistorial]);
    return Array.from(set).sort((a, b) => a.localeCompare(b, 'es'));
  }

  get historicoConceptosOptions(): string[] {
    return this.historicoMeta?.conceptosEnHistorico ?? [];
  }

  get historicoTiposItemOptions(): string[] {
    return this.historicoMeta?.tiposItemEnHistorico ?? [];
  }

  get historicoMovementTypesOptions(): string[] {
    const raw = this.historicoMeta?.movementTypesEnHistorico ?? [];
    return [...raw].sort((a, b) => a.localeCompare(b));
  }

  get historicoUnidadesOptions(): string[] {
    const raw = this.historicoMeta?.unidadesEnHistorico ?? [];
    return [...raw].sort((a, b) => a.localeCompare(b));
  }

  get historicoTiposOperacionOptions(): string[] {
    const raw = this.historicoMeta?.tiposOperacionEnHistorico ?? [];
    return [...raw].sort((a, b) => a.localeCompare(b, 'es'));
  }

  /** Ítems del catálogo para filtro exacto por ID (además de búsqueda texto). */
  get historicoItemsCatalogSorted(): ItemInventarioEcuadorDto[] {
    return [...this.allCatalogItems].sort((a, b) =>
      (a.codigo ?? '').localeCompare(b.codigo ?? '', undefined, { numeric: true })
    );
  }

  /**
   * Lotes de las granjas asignadas; opcionalmente acotados por granja, núcleo y galpón (misma ubicación que el registro del lote).
   */
  get historicoLotesFiltered() {
    const list = this.historicoMeta?.lotes ?? [];
    let out = list;
    if (this.historicoFarmId != null) {
      out = out.filter(l => l.granjaId === this.historicoFarmId);
    }
    const nid = (this.historicoNucleoId ?? '').trim();
    if (nid) {
      out = out.filter(l => (l.nucleoId ?? '').trim() === nid);
    }
    const gid = (this.historicoGalponId ?? '').trim();
    if (gid) {
      out = out.filter(l => (l.galponId ?? '').trim() === gid);
    }
    return out;
  }

  /** Si el lote elegido ya no entra en el subconjunto (p. ej. cambió galpón), lo quitamos. */
  private clearHistoricoLoteIfNotInFiltered(): void {
    const lid = this.historicoLoteId;
    if (lid == null || lid <= 0) return;
    const ok = this.historicoLotesFiltered.some(l => l.loteId === lid);
    if (!ok) this.historicoLoteId = null;
  }

  openHistoricoDetalle(m: InventarioGestionMovimientoDto): void {
    this.historicoDetalleMov = m;
  }

  closeHistoricoDetalle(): void {
    this.historicoDetalleMov = null;
  }

  loadMovimientos(): void {
    const porLote = this.historicoLoteId != null && this.historicoLoteId > 0;
    if (!porLote && this.historicoFarmId == null && (this.historicoNucleoId || this.historicoGalponId)) {
      this.openAlertModal(
        'error',
        'Validación',
        'Para filtrar por núcleo o galpón de registro elija una granja o un lote (el API no aplica ubicación sin granja).'
      );
      return;
    }
    if ((this.historicoFromNucleoId || this.historicoFromGalponId) && this.historicoFromFarmId == null) {
      this.openAlertModal(
        'error',
        'Validación',
        'Para filtrar por núcleo o galpón de origen elija primero la granja de origen.'
      );
      return;
    }
    const tg = (this.historicoTransferGroupId ?? '').trim();
    if (tg && !this.isHistoricoTransferGroupIdValid(tg)) {
      this.openAlertModal('error', 'Validación', 'El grupo de traslado no tiene formato de GUID válido.');
      return;
    }

    this.loading = true;
    const params: Parameters<GestionInventarioService['getMovimientos']>[0] = {};
    if (this.historicoFarmId != null) params.farmId = this.historicoFarmId;
    if (this.historicoFechaDesde) params.fechaDesde = this.historicoFechaDesde;
    if (this.historicoFechaHasta) params.fechaHasta = this.historicoFechaHasta;
    if (this.historicoEstado) params.estado = this.historicoEstado;
    const qItem = (this.historicoItemSearch ?? '').trim();
    if (qItem) params.search = qItem;
    const c = (this.historicoConcepto ?? '').trim();
    if (c) params.concepto = c;
    const ti = (this.historicoTipoItem ?? '').trim();
    if (ti) params.tipoItem = ti;
    const mt = (this.historicoMovementType ?? '').trim();
    if (mt) params.movementType = mt;
    const to = (this.historicoTipoOperacion ?? '').trim();
    if (to) params.tipoOperacion = to;
    const u = (this.historicoUnit ?? '').trim();
    if (u) params.unit = u;
    const refC = (this.historicoReferenceContains ?? '').trim();
    if (refC) params.referenceContains = refC;
    const reasC = (this.historicoReasonContains ?? '').trim();
    if (reasC) params.reasonContains = reasC;
    if (tg) params.transferGroupId = tg;
    if (this.historicoItemInventarioEcuadorId != null && this.historicoItemInventarioEcuadorId > 0) {
      params.itemInventarioEcuadorId = this.historicoItemInventarioEcuadorId;
    }
    if (this.historicoFromFarmId != null) params.fromFarmId = this.historicoFromFarmId;
    if (this.historicoFromNucleoId) params.fromNucleoId = this.historicoFromNucleoId;
    if (this.historicoFromGalponId) params.fromGalponId = this.historicoFromGalponId;
    if (porLote) {
      params.loteId = this.historicoLoteId!;
    } else {
      if (this.historicoNucleoId) params.nucleoId = this.historicoNucleoId;
      if (this.historicoGalponId) params.galponId = this.historicoGalponId;
    }
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

  get historicoNucleosFiltered(): { nucleoId: string; nucleoNombre: string }[] {
    if (!this.filterData || this.historicoFarmId == null) return [];
    return this.filterData.nucleosOrigen
      .filter(n => n.granjaId === this.historicoFarmId)
      .map(n => ({ nucleoId: n.nucleoId, nucleoNombre: n.nucleoNombre }));
  }

  get historicoGalponesFiltered(): { galponId: string; galponNombre: string }[] {
    if (!this.filterData || this.historicoFarmId == null) return [];
    return this.filterData.galponesOrigen
      .filter(g => g.granjaId === this.historicoFarmId && (!this.historicoNucleoId || g.nucleoId === this.historicoNucleoId))
      .map(g => ({ galponId: g.galponId, galponNombre: g.galponNombre }));
  }

  onHistoricoFarmChange(): void {
    this.historicoNucleoId = null;
    this.historicoGalponId = null;
    const lid = this.historicoLoteId;
    if (lid != null && lid > 0) {
      const ok = this.historicoLotesFiltered.some(l => l.loteId === lid);
      if (!ok) this.historicoLoteId = null;
    }
  }

  /** Texto del desplegable de lote (incluye granja cuando filtra «todas»). */
  historicoLoteOptionLabel(l: { loteId: number; loteNombre: string; fase: string | null; granjaId: number }): string {
    const farm = this.getFarmName(l.granjaId);
    const phase = (l.fase ?? '').trim();
    const phasePart = phase ? ` · ${phase}` : '';
    return `${farm} — ${l.loteNombre}${phasePart} · #${l.loteId}`;
  }

  onHistoricoNucleoChange(): void {
    this.historicoGalponId = null;
    this.clearHistoricoLoteIfNotInFiltered();
  }

  onHistoricoGalponChange(): void {
    this.clearHistoricoLoteIfNotInFiltered();
  }

  onHistoricoFromFarmChange(): void {
    this.historicoFromNucleoId = null;
    this.historicoFromGalponId = null;
  }

  onHistoricoFromNucleoChange(): void {
    this.historicoFromGalponId = null;
  }

  toggleHistoricoFiltrosAvanzados(): void {
    this.historicoFiltrosAvanzadosAbiertos = !this.historicoFiltrosAvanzadosAbiertos;
  }

  get historicoFromNucleosFiltered(): { nucleoId: string; nucleoNombre: string }[] {
    if (!this.filterData || this.historicoFromFarmId == null) return [];
    return this.filterData.nucleosOrigen
      .filter(n => n.granjaId === this.historicoFromFarmId)
      .map(n => ({ nucleoId: n.nucleoId, nucleoNombre: n.nucleoNombre }));
  }

  get historicoFromGalponesFiltered(): { galponId: string; galponNombre: string }[] {
    if (!this.filterData || this.historicoFromFarmId == null) return [];
    return this.filterData.galponesOrigen
      .filter(
        g =>
          g.granjaId === this.historicoFromFarmId &&
          (!this.historicoFromNucleoId || g.nucleoId === this.historicoFromNucleoId)
      )
      .map(g => ({ galponId: g.galponId, galponNombre: g.galponNombre }));
  }

  /** Con lote seleccionado el API usa la ubicación del lote; se ignoran núcleo/galpón manuales. */
  onHistoricoLoteChange(): void {
    if (this.historicoLoteId != null && this.historicoLoteId > 0) {
      this.historicoNucleoId = null;
      this.historicoGalponId = null;
    }
  }

  trackByHistoricoMov = (_: number, m: InventarioGestionMovimientoDto) => m.id;

  formatFechaMovimiento(iso: string): string {
    if (!iso) return '—';
    const d = new Date(iso);
    return isNaN(d.getTime()) ? iso : d.toLocaleDateString('es', { dateStyle: 'short' }) + ' ' + d.toLocaleTimeString('es', { hour: '2-digit', minute: '2-digit' });
  }

  /** Fecha de ingreso en la grilla de stock (solo fecha, sin hora). */
  formatFechaIngresoStock(iso: string | null | undefined): string {
    if (!iso) return '—';
    const d = new Date(iso);
    return isNaN(d.getTime()) ? String(iso) : d.toLocaleDateString('es', { dateStyle: 'long' });
  }

  /** Convierte fecha del API a yyyy-MM-dd para input type="date". */
  fechaIngresoStockToYmd(iso: string | null | undefined): string {
    if (!iso) return '';
    const head = String(iso).trim().match(/^(\d{4})-(\d{2})-(\d{2})/);
    if (head) return `${head[1]}-${head[2]}-${head[3]}`;
    const d = new Date(iso);
    if (isNaN(d.getTime())) return '';
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');
    return `${d.getFullYear()}-${mm}-${dd}`;
  }

  /** Ubicación del movimiento (granja de registro + núcleo/galpón si aplica). */
  ubicacionRegistroMovimiento(m: InventarioGestionMovimientoDto): string {
    const g = m.granjaNombre ?? String(m.farmId);
    const n = m.nucleoNombre ?? m.nucleoId ?? '';
    const gp = m.galponNombre ?? m.galponId ?? '';
    if (!n && !gp) return g;
    return `${g} · Núc. ${n || '—'} · Galp. ${gp || '—'}`;
  }

  /** Origen/destino según tipo: contraparte del traslado o procedencia. */
  otroExtremoMovimiento(m: InventarioGestionMovimientoDto): string {
    if (m.fromFarmId == null && !m.fromGranjaNombre) return '—';
    const g = m.fromGranjaNombre ?? (m.fromFarmId != null ? String(m.fromFarmId) : '');
    const n = m.fromNucleoNombre ?? m.fromNucleoId ?? '';
    const gp = m.fromGalponNombre ?? m.fromGalponId ?? '';
    if (!n && !gp) return g;
    return `${g} · Núc. ${n || '—'} · Galp. ${gp || '—'}`;
  }

  /** Exporta el histórico cargado a CSV (abre en Excel con UTF-8). */
  exportHistoricoCsv(): void {
    const rows = this.movimientosList;
    if (!rows.length) {
      this.openAlertModal('error', 'Sin datos', 'Pulse Consultar primero para cargar movimientos.');
      return;
    }
    const escape = (s: string | number | null | undefined): string => {
      const t = (s ?? '').toString();
      if (/[",\n\r]/.test(t)) return `"${t.replace(/"/g, '""')}"`;
      return t;
    };
    const headers = [
      'Fecha',
      'Tipo operación',
      'Estado',
      'Código ítem',
      'Nombre ítem',
      'Concepto (mostrado)',
      'Concepto catálogo',
      'Tipo ítem catálogo',
      'Cantidad',
      'Unidad',
      'Ubicación registro (granja / núcleo / galpón)',
      'Otro extremo u origen (granja / núcleo / galpón)',
      'Referencia',
      'Motivo y detalle (lote / contexto)',
      'Grupo traslado (UUID)'
    ];
    const lines = [headers.join(',')];
    for (const m of rows) {
      lines.push(
        [
          escape(this.formatFechaMovimiento(m.createdAt)),
          escape(m.tipoOperacion ?? m.movementType),
          escape(m.estado ?? ''),
          escape(m.itemCodigo),
          escape(m.itemNombre),
          escape(m.itemType),
          escape(m.itemConcepto ?? ''),
          escape(m.itemTipoItem ?? ''),
          escape(m.quantity),
          escape(m.unit),
          escape(this.ubicacionRegistroMovimiento(m)),
          escape(this.otroExtremoMovimiento(m)),
          escape(m.reference ?? ''),
          escape(m.reason ?? ''),
          escape(m.transferGroupId ?? '')
        ].join(',')
      );
    }
    const blob = new Blob(['\ufeff' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `inventario-gestion-historico-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(a.href);
  }

  loadFilterData(): void {
    this.loading = true;
    this.svc.getFilterData().subscribe({
      next: (data) => {
        this.filterData = data;
        const orig = data.farmsOrigen ?? [];
        if (orig.length === 1) {
          const only = orig[0].id;
          if (this.selectedFarmId == null) this.selectedFarmId = only;
          // Destino de ingreso: todas las granjas empresa; preseleccionar si la única asignada está en catálogo destino
          if (this.ingresoFarmId == null && (data.farmsDestino ?? []).some(f => f.id === only)) {
            this.ingresoFarmId = only;
          }
          if (this.fromFarmId == null) this.fromFarmId = only;
          if (this.transitoFarmFilter == null) this.transitoFarmFilter = only;
        }
        this.loading = false;
        this.applyInventoryQueryParamsFromRoute();
      },
      error: () => {
        this.loading = false;
        this.openAlertModal('error', 'Error', 'Error al cargar filtros.');
      }
    });
  }

  /**
   * Desde Seguimiento engorde: ?farmId=&nucleoId=&galponId=&tab=traslados
   * Preselecciona ubicación y pestaña (p. ej. traslados de alimento).
   */
  private applyInventoryQueryParamsFromRoute(): void {
    const q = this.route.snapshot.queryParamMap;
    const farm = q.get('farmId');
    if (farm) {
      const n = parseInt(farm, 10);
      if (!Number.isNaN(n)) {
        this.selectedFarmId = n;
        if (this.fromFarmId == null) this.fromFarmId = n;
        if (this.ingresoFarmId == null && (this.filterData?.farmsDestino ?? []).some(f => f.id === n)) {
          this.ingresoFarmId = n;
        }
        if (this.transitoFarmFilter == null) this.transitoFarmFilter = n;
      }
    }
    const nucleo = q.get('nucleoId');
    if (nucleo) {
      this.selectedNucleoId = nucleo;
      if (this.fromNucleoId == null) this.fromNucleoId = nucleo;
    }
    const galpon = q.get('galponId');
    if (galpon) {
      this.selectedGalponId = galpon;
      if (this.fromGalponId == null) this.fromGalponId = galpon;
    }
    const tab = q.get('tab');
    const tabKeys: TabKey[] = ['stock', 'ingresos', 'traslados', 'transito', 'historico', 'items'];
    if (tab && (tabKeys as string[]).includes(tab)) {
      this.setTab(tab as TabKey);
    } else if (this.activeTab === 'stock') {
      this.loadStock();
    }
  }

  loadStock(): void {
    if (!this.filterData) return;
    this.loading = true;
    const params: any = {};
    if (this.selectedFarmId != null) params.farmId = this.selectedFarmId;
    if (this.selectedNucleoId) params.nucleoId = this.selectedNucleoId;
    if (this.selectedGalponId) params.galponId = this.selectedGalponId;
    const conceptoFiltro = (this.stockConceptFilter ?? '').trim();
    if (conceptoFiltro) params.itemType = conceptoFiltro;
    const q = (this.searchTerm ?? '').trim();
    if (q) params.search = q;
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
    return this.filterData.nucleosOrigen
      .filter(n => n.granjaId === this.selectedFarmId)
      .map(n => ({ nucleoId: n.nucleoId, nucleoNombre: n.nucleoNombre }));
  }

  get galponesFiltered(): { galponId: string; galponNombre: string }[] {
    if (!this.filterData || this.selectedFarmId == null) return [];
    return this.filterData.galponesOrigen
      .filter(g => g.granjaId === this.selectedFarmId && (!this.selectedNucleoId || g.nucleoId === this.selectedNucleoId))
      .map(g => ({ galponId: g.galponId, galponNombre: g.galponNombre }));
  }

  fromNucleosFiltered(): { nucleoId: string; nucleoNombre: string }[] {
    if (!this.filterData || this.fromFarmId == null) return [];
    return this.filterData.nucleosOrigen
      .filter(n => n.granjaId === this.fromFarmId)
      .map(n => ({ nucleoId: n.nucleoId, nucleoNombre: n.nucleoNombre }));
  }
  fromGalponesFiltered(): { galponId: string; galponNombre: string }[] {
    if (!this.filterData || this.fromFarmId == null) return [];
    return this.filterData.galponesOrigen
      .filter(g => g.granjaId === this.fromFarmId && (!this.fromNucleoId || g.nucleoId === this.fromNucleoId))
      .map(g => ({ galponId: g.galponId, galponNombre: g.galponNombre }));
  }
  /** Núcleos del destino del traslado: misma granja → catálogo origen; inter-granja → catálogo empresa. */
  toNucleosFiltered(): { nucleoId: string; nucleoNombre: string }[] {
    if (!this.filterData || this.toFarmId == null) return [];
    const nucleos = this.trasladoModo === 'interGranja' ? this.filterData.nucleosDestino : this.filterData.nucleosOrigen;
    return nucleos
      .filter(n => n.granjaId === this.toFarmId)
      .map(n => ({ nucleoId: n.nucleoId, nucleoNombre: n.nucleoNombre }));
  }
  toGalponesFiltered(): { galponId: string; galponNombre: string }[] {
    if (!this.filterData || this.toFarmId == null) return [];
    const galpones = this.trasladoModo === 'interGranja' ? this.filterData.galponesDestino : this.filterData.galponesOrigen;
    return galpones
      .filter(g => g.granjaId === this.toFarmId && (!this.toNucleoId || g.nucleoId === this.toNucleoId))
      .map(g => ({ galponId: g.galponId, galponNombre: g.galponNombre }));
  }

  ingresoNucleosFiltered(): { nucleoId: string; nucleoNombre: string }[] {
    if (!this.filterData || this.ingresoFarmId == null) return [];
    return this.filterData.nucleosDestino
      .filter(n => n.granjaId === this.ingresoFarmId)
      .map(n => ({ nucleoId: n.nucleoId, nucleoNombre: n.nucleoNombre }));
  }
  ingresoGalponesFiltered(): { galponId: string; galponNombre: string }[] {
    if (!this.filterData || this.ingresoFarmId == null) return [];
    return this.filterData.galponesDestino
      .filter(g => g.granjaId === this.ingresoFarmId && (!this.ingresoNucleoId || g.nucleoId === this.ingresoNucleoId))
      .map(g => ({ galponId: g.galponId, galponNombre: g.galponNombre }));
  }

  get showNucleoGalpon(): boolean {
    return this.isAlimentoConcept(this.selectedConcept);
  }

  /** Stock: mostrar filtros/columnas núcleo+galpón si el filtro es «todos» o alimento. */
  get stockShowNucleoGalpon(): boolean {
    const c = (this.stockConceptFilter ?? '').trim();
    if (!c) return true;
    return this.isAlimentoConcept(c);
  }

  onFarmChange(): void {
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
  }
  onNucleoChange(): void {
    this.selectedGalponId = null;
  }

  onStockConceptFilterChange(): void {
    const c = (this.stockConceptFilter ?? '').trim();
    if (c && !this.isAlimentoConcept(c)) {
      this.selectedNucleoId = null;
      this.selectedGalponId = null;
    }
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
      this.ingresoOrigenTipo = 'planta';
      this.ingresoOrigenFarmId = null;
      this.ingresoOrigenBodegaTexto = '';
      this.fromNucleoId = null;
      this.fromGalponId = null;
      this.toNucleoId = null;
      this.toGalponId = null;
    }
    if (this.trasladoModo === 'mismaGranja' && !this.showNucleoGalpon) {
      this.trasladoModo = 'interGranja';
    }
  }

  onTrasladoModoChange(): void {
    this.toNucleoId = null;
    this.toGalponId = null;
    if (this.trasladoModo === 'mismaGranja' && this.fromFarmId != null) {
      this.toFarmId = this.fromFarmId;
    }
    this.loadOriginStock();
  }

  onTrasladoFromFarmChange(): void {
    if (this.trasladoModo === 'mismaGranja' && this.fromFarmId != null) {
      this.toFarmId = this.fromFarmId;
    }
    this.loadOriginStock();
  }

  /** Cambia tipo de origen en ingreso (Alimento): limpia campos que no aplican. */
  onIngresoOrigenTipoChange(): void {
    this.ingresoOrigenFarmId = null;
    this.ingresoOrigenBodegaTexto = '';
    if (this.ingresoOrigenTipo === 'planta') {
      this.ingresoReason = '';
    }
  }

  onIngresoDestinoFarmChange(): void {
    this.ingresoNucleoId = null;
    this.ingresoGalponId = null;
  }

  onIngresoDestinoNucleoChange(): void {
    this.ingresoGalponId = null;
  }

  submitIngreso(): void {
    if (!this.ingresoFechaMovimiento?.trim()) {
      this.openAlertModal('error', 'Validación', 'Indique la fecha del movimiento.');
      return;
    }
    if (this.ingresoFarmId == null || this.ingresoItemInventarioEcuadorId == null || this.ingresoQuantity <= 0) {
      this.openAlertModal('error', 'Validación', 'Complete granja, ítem y cantidad.');
      return;
    }
    if (this.showNucleoGalpon) {
      if (this.ingresoOrigenTipo === 'granja') {
        if (this.ingresoOrigenFarmId == null) {
          this.openAlertModal('error', 'Validación', 'Indique la granja de origen del material.');
          return;
        }
        if (this.ingresoOrigenFarmId === this.ingresoFarmId) {
          this.openAlertModal('error', 'Validación', 'La granja de origen debe ser distinta a la granja de destino del ingreso.');
          return;
        }
      }
      if (this.ingresoOrigenTipo === 'bodega' && this.ingresoOrigenFarmId == null) {
        this.openAlertModal('error', 'Validación', 'Indique la granja a la que pertenece la bodega de procedencia.');
        return;
      }
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
    if (!this.trasladoFechaMovimiento?.trim()) {
      this.openAlertModal('error', 'Validación', 'Indique la fecha del traslado.');
      return;
    }

    if (this.trasladoModo === 'mismaGranja') {
      if (!this.showNucleoGalpon) {
        this.openAlertModal('error', 'Validación', 'Entre galpones de la misma granja solo aplica a concepto Alimento.');
        return;
      }
      if (this.fromFarmId !== this.toFarmId) {
        this.openAlertModal('error', 'Validación', 'En este modo origen y destino deben ser la misma granja.');
        return;
      }
      if (!this.fromNucleoId || !this.fromGalponId || !this.toNucleoId || !this.toGalponId) {
        this.openAlertModal('error', 'Validación', 'Para alimento debe seleccionar Núcleo y Galpón en origen y destino.');
        return;
      }
      if (this.fromGalponId === this.toGalponId && this.fromNucleoId === this.toNucleoId) {
        this.openAlertModal('error', 'Validación', 'El galpón de destino debe ser distinto al de origen.');
        return;
      }
    } else {
      if (this.fromFarmId === this.toFarmId) {
        this.openAlertModal('error', 'Validación', 'En traslado entre granjas la granja destino debe ser distinta a la de origen.');
        return;
      }
      if (this.showNucleoGalpon) {
        if (!this.fromNucleoId || !this.fromGalponId) {
          this.openAlertModal('error', 'Validación', 'Para alimento debe indicar Núcleo y Galpón de origen.');
          return;
        }
        // Destino núcleo/galpón opcional en salida (solo como referencia)
      } else {
        if (this.fromNucleoId || this.fromGalponId || this.toNucleoId || this.toGalponId) {
          this.openAlertModal('error', 'Validación', 'Para ítems no alimento no use Núcleo/Galpón en traslado entre granjas.');
          return;
        }
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
    const msg =
      this.trasladoModo === 'interGranja'
        ? `¿Enviar solicitud de traslado a otra granja? No se descuenta stock en origen hasta que la granja destino confirme recepción (${this.trasladoQuantity} ${this.trasladoSelectedUnit}).`
        : `¿Registrar traslado de ${this.trasladoQuantity} ${this.trasladoSelectedUnit}?`;
    this.openConfirmModal('Confirmar traslado', msg, 'traslado');
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

  get farmsOrigen(): { id: number; name: string }[] {
    return this.filterData?.farmsOrigen?.map(f => ({ id: f.id, name: f.name })) ?? [];
  }

  get farmsDestino(): { id: number; name: string }[] {
    return this.filterData?.farmsDestino?.map(f => ({ id: f.id, name: f.name })) ?? [];
  }

  getFarmName(farmId: number | null): string {
    if (farmId == null) return '—';
    const hit =
      this.farmsOrigen.find(f => f.id === farmId) ?? this.farmsDestino.find(f => f.id === farmId);
    return hit?.name ?? String(farmId);
  }

  onToDestinoNucleoChange(): void {
    this.toGalponId = null;
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
  openConfirmModal(
    title: string,
    text: string,
    action: 'ingreso' | 'traslado' | 'recepcionTransito' | 'deleteStock' | 'anularMovHistorico'
  ): void {
    this.confirmTitle = title;
    this.confirmText = text;
    this.confirmAction = action;
    this.showConfirmModal = true;
  }
  closeConfirmModal(): void {
    this.showConfirmModal = false;
    this.confirmAction = null;
    this.pendingDeleteStock = null;
    this.pendingAnularMovimiento = null;
  }
  confirm(): void {
    const action = this.confirmAction;
    const rowToDelete = this.pendingDeleteStock;
    const movAnular = this.pendingAnularMovimiento;
    this.showConfirmModal = false;
    this.confirmAction = null;
    this.pendingDeleteStock = null;
    this.pendingAnularMovimiento = null;
    if (action === 'ingreso') this.doIngreso();
    if (action === 'traslado') this.doTraslado();
    if (action === 'recepcionTransito') this.doRecepcionTransito();
    if (action === 'deleteStock') this.doDeleteStock(rowToDelete);
    if (action === 'anularMovHistorico') this.doAnularMovimientoHistorico(movAnular);
  }

  /** Solo Consumo / Ingreso: revierte stock y elimina la fila del histórico. */
  puedeAnularMovimientoHistorico(m: InventarioGestionMovimientoDto): boolean {
    const t = (m.movementType ?? '').trim().toLowerCase();
    return t === 'consumo' || t === 'ingreso';
  }

  beginAnularMovimientoHistorico(m: InventarioGestionMovimientoDto): void {
    this.pendingAnularMovimiento = m;
    const tipo = (m.movementType ?? '').toLowerCase() === 'consumo' ? 'consumo' : 'ingreso';
    this.openConfirmModal(
      'Anular movimiento en histórico',
      `¿Anular este registro (${tipo}) de ${m.quantity} ${m.unit} — ${m.itemNombre}? ` +
        `Se revertirá el efecto en el stock de la ubicación y se eliminará la línea del histórico.`,
      'anularMovHistorico'
    );
  }

  private doAnularMovimientoHistorico(m: InventarioGestionMovimientoDto | null): void {
    if (!m) return;
    this.loading = true;
    this.svc.anularMovimientoHistorico(m.id, 'Anulado desde histórico gestión inventario').subscribe({
      next: () => {
        this.loading = false;
        this.openAlertModal('success', 'Listo', 'Movimiento anulado y stock actualizado.');
        this.loadMovimientos();
        if (this.activeTab === 'stock') this.loadStock();
      },
      error: (err) => {
        this.loading = false;
        this.openAlertModal('error', 'Error', err.error?.message || 'No se pudo anular el movimiento.');
      }
    });
  }

  openStockEdit(row: InventarioGestionStockDto): void {
    this.stockEditRow = row;
    this.stockEditQuantity = Number(row.quantity);
    this.stockEditUnit = (row.unit ?? 'kg').trim() || 'kg';
    this.stockEditFechaIngreso = this.fechaIngresoStockToYmd(row.fechaIngreso) || this.todayYmd();
    this.stockEditReason = '';
    this.showStockEditModal = true;
  }

  closeStockEditModal(): void {
    this.showStockEditModal = false;
    this.stockEditRow = null;
    this.submittingStockEdit = false;
  }

  submitStockEdit(): void {
    const row = this.stockEditRow;
    if (!row) return;
    if (this.stockEditQuantity < 0) {
      this.openAlertModal('error', 'Validación', 'La cantidad no puede ser negativa.');
      return;
    }
    const fechaIngreso = (this.stockEditFechaIngreso ?? '').trim();
    if (!fechaIngreso) {
      this.openAlertModal('error', 'Validación', 'Indique la fecha de registro (ingreso en ubicación).');
      return;
    }
    const unit = (this.stockEditUnit ?? '').trim() || row.unit || 'kg';
    this.submittingStockEdit = true;
    this.svc
      .actualizarStock(row.id, {
        quantity: this.stockEditQuantity,
        unit,
        reason: this.stockEditReason?.trim() || null,
        fechaIngreso
      })
      .subscribe({
        next: () => {
          this.submittingStockEdit = false;
          this.closeStockEditModal();
          this.openAlertModal('success', 'Listo', 'Stock actualizado. El ajuste quedó registrado en el histórico.');
          this.loadStock();
        },
        error: (err) => {
          this.submittingStockEdit = false;
          this.openAlertModal('error', 'Error', err.error?.message || 'No se pudo actualizar el stock.');
        }
      });
  }

  beginDeleteStock(row: InventarioGestionStockDto): void {
    this.pendingDeleteStock = row;
    const q = (row.quantity ?? 0) > 0;
    this.openConfirmModal(
      'Eliminar registro de stock',
      q
        ? `¿Eliminar el stock de «${row.itemNombre}» en ${row.granjaNombre ?? row.farmId}? Se registrará una salida por ${row.quantity} ${row.unit} en el histórico.`
        : `¿Eliminar el registro de stock de «${row.itemNombre}» en ${row.granjaNombre ?? row.farmId}? (cantidad actual: 0)`,
      'deleteStock'
    );
  }

  private doDeleteStock(row: InventarioGestionStockDto | null): void {
    if (!row) return;
    this.loading = true;
    this.svc.eliminarStock(row.id).subscribe({
      next: () => {
        this.loading = false;
        this.openAlertModal('success', 'Listo', 'Registro de stock eliminado.');
        this.loadStock();
      },
      error: (err) => {
        this.loading = false;
        this.openAlertModal('error', 'Error', err.error?.message || 'No se pudo eliminar el registro.');
      }
    });
  }

  /** Alineado con Guid.TryParse del API (con o sin guiones, opcionalmente entre llaves). */
  private isHistoricoTransferGroupIdValid(raw: string): boolean {
    let s = raw.trim();
    if (s.startsWith('{') && s.endsWith('}')) s = s.slice(1, -1).trim();
    const compact = s.replace(/-/g, '');
    if (compact.length !== 32 || !/^[0-9a-f]+$/i.test(compact)) return false;
    if (!s.includes('-')) return true;
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(s);
  }

  private todayYmd(): string {
    const d = new Date();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');
    return `${d.getFullYear()}-${mm}-${dd}`;
  }

  /** Texto legible para la fecha de ingreso seleccionada. */
  formatIngresoFechaDisplay(): string {
    const ymd = (this.ingresoFechaMovimiento ?? '').trim();
    if (!ymd) return '—';
    const [y, m, day] = ymd.split('-').map(Number);
    if (!y || !m || !day) return ymd;
    const dt = new Date(y, m - 1, day);
    return isNaN(dt.getTime()) ? ymd : dt.toLocaleDateString('es', { dateStyle: 'long' });
  }

  openIngresoFechaModal(): void {
    this.ingresoFechaDraft = this.ingresoFechaMovimiento?.trim() || this.todayYmd();
    this.showIngresoFechaModal = true;
  }

  closeIngresoFechaModal(): void {
    this.showIngresoFechaModal = false;
  }

  confirmIngresoFechaModal(): void {
    const d = (this.ingresoFechaDraft ?? '').trim();
    if (!d) {
      this.openAlertModal('error', 'Validación', 'Seleccione una fecha.');
      return;
    }
    this.ingresoFechaMovimiento = d;
    this.showIngresoFechaModal = false;
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
    const esAlimento = this.showNucleoGalpon;
    const tipoOrigen = esAlimento ? this.ingresoOrigenTipo : 'planta';
    let reason: string | null;
    if (!esAlimento || tipoOrigen === 'planta') {
      reason = this.ingresoReasonPlanta;
    } else if (tipoOrigen === 'granja') {
      const fromFarmName =
        this.ingresoOrigenFarmId != null ? this.farmsDestino.find(f => f.id === this.ingresoOrigenFarmId)?.name : null;
      reason = fromFarmName
        ? `Ingreso desde granja: ${fromFarmName}${this.ingresoReason ? '. ' + this.ingresoReason : ''}`
        : this.ingresoReason || null;
    } else {
      const farmBodega =
        this.ingresoOrigenFarmId != null ? this.farmsDestino.find(f => f.id === this.ingresoOrigenFarmId)?.name : null;
      const bodegaTxt = (this.ingresoOrigenBodegaTexto ?? '').trim();
      const extra = this.ingresoReason?.trim();
      reason = [
        'Ingreso desde bodega',
        farmBodega ? `(granja ${farmBodega})` : null,
        bodegaTxt ? `Bodega: ${bodegaTxt}` : null,
        extra || null
      ]
        .filter(Boolean)
        .join('. ');
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
      origenTipo: tipoOrigen,
      origenFarmId: esAlimento && (tipoOrigen === 'granja' || tipoOrigen === 'bodega') ? this.ingresoOrigenFarmId : null,
      origenBodegaDescripcion: esAlimento && tipoOrigen === 'bodega' ? (this.ingresoOrigenBodegaTexto || null) : null,
      fechaMovimiento: this.ingresoFechaMovimiento?.trim() || null
    }).subscribe({
      next: () => {
        this.submittingIngreso = false;
        this.openAlertModal('success', 'Listo', 'Ingreso registrado correctamente.');
        this.ingresoQuantity = 0;
        this.ingresoReference = '';
        this.ingresoReason = '';
        this.ingresoOrigenBodegaTexto = '';
        this.ingresoFechaMovimiento = this.todayYmd();
        this.loadStock();
      },
      error: (err) => {
        this.submittingIngreso = false;
        this.openAlertModal('error', 'Error', err.error?.message || 'Error al registrar ingreso.');
      }
    });
  }

  loadTransitos(): void {
    this.loadingTransito = true;
    this.svc.getTransitosPendientes(this.transitoFarmFilter).subscribe({
      next: (list) => {
        this.transitoList = list ?? [];
        this.loadingTransito = false;
      },
      error: () => {
        this.loadingTransito = false;
        this.openAlertModal('error', 'Error', 'No se pudo cargar el tránsito pendiente.');
      }
    });
  }

  beginRecepcion(row: InventarioGestionTransitoPendienteDto): void {
    this.recepcionPendiente = row;
    this.recepcionToNucleoId = row.destinoNucleoIdHint;
    this.recepcionToGalponId = row.destinoGalponIdHint;
    // Tras pintar el formulario inline en la tarjeta, acercar la vista (listas largas).
    setTimeout(() => {
      const id = `transito-recepcion-${row.transferGroupId}`;
      document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }, 0);
  }

  cancelRecepcion(): void {
    this.recepcionPendiente = null;
    this.recepcionToNucleoId = null;
    this.recepcionToGalponId = null;
  }

  recepcionNeedsNucleoGalpon(itemId: number): boolean {
    const item = this.allCatalogItems.find(i => i.id === itemId);
    if (!item) return false;
    return this.isAlimentoConcept(item.concepto ?? item.tipoItem);
  }

  submitRecepcionForm(): void {
    const row = this.recepcionPendiente;
    if (!row) return;
    const needNg = this.recepcionNeedsNucleoGalpon(row.itemInventarioEcuadorId);
    if (needNg && (!this.recepcionToNucleoId || !this.recepcionToGalponId)) {
      this.openAlertModal('error', 'Validación', 'Para alimento indique Núcleo y Galpón de recepción en la granja destino.');
      return;
    }
    if (!needNg && (this.recepcionToNucleoId || this.recepcionToGalponId)) {
      this.openAlertModal('error', 'Validación', 'Para ítems no alimento la recepción es solo a nivel granja (sin Núcleo/Galpón).');
      return;
    }
    const detalle =
      row.pendienteDespachoOrigen === true
        ? ' (Solicitud antigua) Se descontará origen si aún no se hizo y se sumará en destino.'
        : ' El origen ya fue descontado al enviar el traslado; solo se sumará el stock en destino.';
    this.openConfirmModal(
      'Confirmar recepción',
      `¿Registrar ingreso en ${row.toGranjaNombre ?? 'granja destino'} por la cantidad indicada?${detalle}`,
      'recepcionTransito'
    );
  }

  private doRecepcionTransito(): void {
    const row = this.recepcionPendiente;
    if (!row?.transferGroupId) return;
    const needNg = this.recepcionNeedsNucleoGalpon(row.itemInventarioEcuadorId);
    this.submittingRecepcion = true;
    this.svc
      .registrarRecepcionTransito({
        transferGroupId: row.transferGroupId,
        toFarmId: row.toFarmId,
        toNucleoId: needNg ? this.recepcionToNucleoId : null,
        toGalponId: needNg ? this.recepcionToGalponId : null
      })
      .subscribe({
        next: () => {
          this.submittingRecepcion = false;
          this.cancelRecepcion();
          this.openAlertModal('success', 'Listo', 'Recepción registrada. El inventario ya figura en destino.');
          this.loadTransitos();
          this.loadStock();
        },
        error: (err) => {
          this.submittingRecepcion = false;
          this.openAlertModal('error', 'Error', err.error?.message || 'Error al registrar recepción.');
        }
      });
  }

  recepcionNucleosForFarm(farmId: number): { nucleoId: string; nucleoNombre: string }[] {
    if (!this.filterData) return [];
    return this.filterData.nucleosDestino
      .filter(n => n.granjaId === farmId)
      .map(n => ({ nucleoId: n.nucleoId, nucleoNombre: n.nucleoNombre }));
  }

  recepcionGalponesForFarm(farmId: number): { galponId: string; galponNombre: string }[] {
    if (!this.filterData) return [];
    return this.filterData.galponesDestino
      .filter(g => g.granjaId === farmId && (!this.recepcionToNucleoId || g.nucleoId === this.recepcionToNucleoId))
      .map(g => ({ galponId: g.galponId, galponNombre: g.galponNombre }));
  }

  onRecepcionNucleoChange(): void {
    this.recepcionToGalponId = null;
  }

  private doTraslado(): void {
    this.submittingTraslado = true;
    const inter = this.trasladoModo === 'interGranja';
    let toNucleo: string | null = null;
    let toGalpon: string | null = null;
    if (this.showNucleoGalpon) {
      if (inter) {
        toNucleo = this.toNucleoId?.trim() ? this.toNucleoId : null;
        toGalpon = this.toGalponId?.trim() ? this.toGalponId : null;
      } else {
        toNucleo = this.toNucleoId;
        toGalpon = this.toGalponId;
      }
    }
    this.svc.registrarTraslado({
      fromFarmId: this.fromFarmId!,
      fromNucleoId: this.showNucleoGalpon ? this.fromNucleoId : null,
      fromGalponId: this.showNucleoGalpon ? this.fromGalponId : null,
      toFarmId: this.toFarmId!,
      toNucleoId: toNucleo,
      toGalponId: toGalpon,
      itemInventarioEcuadorId: this.trasladoItemInventarioEcuadorId!,
      quantity: this.trasladoQuantity,
      unit: this.trasladoSelectedUnit === '—' ? 'kg' : this.trasladoSelectedUnit,
      reference: this.trasladoReference || null,
      reason: this.trasladoReason || null,
      destinoTipo: this.trasladoDestinoTipo,
      fechaMovimiento: this.trasladoFechaMovimiento?.trim() || null
    }).subscribe({
      next: () => {
        this.submittingTraslado = false;
        const ok =
          this.trasladoModo === 'interGranja'
            ? 'Traslado enviado a tránsito. El stock ya se descontó en origen; la granja destino debe confirmar en la pestaña Tránsito para sumar el ítem allí.'
            : 'Traslado registrado correctamente.';
        this.openAlertModal('success', 'Listo', ok);
        this.trasladoQuantity = 0;
        this.trasladoReference = '';
        this.trasladoReason = '';
        this.trasladoFechaMovimiento = this.todayYmd();
        this.loadStock();
        if (this.trasladoModo === 'interGranja') this.loadTransitos();
      },
      error: (err) => {
        this.submittingTraslado = false;
        this.openAlertModal('error', 'Error', err.error?.message || 'Error al registrar traslado.');
      }
    });
  }
}
