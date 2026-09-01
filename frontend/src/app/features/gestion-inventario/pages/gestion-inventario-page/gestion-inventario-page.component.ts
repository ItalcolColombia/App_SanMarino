// src/app/features/gestion-inventario/pages/gestion-inventario-page/gestion-inventario-page.component.ts
import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

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
  faChevronUp,
  faScaleBalanced
} from '@fortawesome/free-solid-svg-icons';

import { HasPermissionDirective } from '../../../../core/auth/has-permission.directive';
import { UserPermissionService } from '../../../../core/auth/user-permission.service';
import { CuadreAlimentoEngordeComponent } from '../../components/cuadre-alimento-engorde/cuadre-alimento-engorde.component';
import { CountryFilterService } from '../../../../core/services/country/country-filter.service';
import { exportarStockExcel } from '../../funciones/exportar-stock-excel.funcion';
import {
  esFechaIngresoOfrecible,
  esFechaMovimientoPermitida,
  extremosFechaIngreso,
  hintFechaIngreso,
  mensajeFechaFueraDeVentana,
  mensajeFechaIngresoFueraDeVentana,
  ventanaFechaMovimiento,
  PERMISO_FECHA_RETROACTIVA,
  type VentanaFechaIngreso
} from '../../funciones/ventana-fecha-movimiento.funcion';
import { hintVentanaFechaRegistro } from '../../../../shared/utils/fecha/ventana-fecha-registro.funcion';
import {
  formatFechaMovimiento as formatFechaMovimientoFn,
  formatFechaIngresoStock as formatFechaIngresoStockFn,
  fechaIngresoStockToYmd as fechaIngresoStockToYmdFn,
  ubicacionRegistroMovimiento as ubicacionRegistroMovimientoFn,
  otroExtremoMovimiento as otroExtremoMovimientoFn,
  siloOptionLabel as siloOptionLabelFn
} from '../../funciones/gestion-inventario-page-formato.funcion';
import { RecepcionDestinoRow } from '../../models/recepcion-destino-row.model';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import {
  GestionInventarioService,
  InventarioGestionFilterDataDto,
  InventarioGestionHistoricoFiltrosDto,
  InventarioGestionStockDto,
  InventarioGestionMovimientoDto,
  ItemInventarioDto,
  InventarioGestionTransitoPendienteDto,
  InventarioGestionSiloDto,
  InventarioGestionIngresoRequest,
  InventarioGestionTrasladoRequest
} from '../../services/gestion-inventario.service';

type TabKey = 'stock' | 'ingresos' | 'traslados' | 'transito' | 'historico' | 'items' | 'cuadre';
type TrasladoModo = 'mismaGranja' | 'interGranja';

@Component({
  selector: 'app-gestion-inventario-page',
  standalone: true,
  imports: [FormsModule, FontAwesomeModule, HasPermissionDirective, CuadreAlimentoEngordeComponent],
  templateUrl: './gestion-inventario-page.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./gestion-inventario-page.component.scss']
})
export class GestionInventarioPageComponent implements OnInit {
  faStock = faBoxesStacked;
  faCuadre = faScaleBalanced;
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

  /**
   * ¿Alguna fila del stock tiene kilos SEPARADOS por un seguimiento sin validar?
   *
   * Se calcula una sola vez al cargar la lista y no en un getter: el template lo evalúa en cada ciclo
   * de detección de cambios y recorrer el arreglo ahí es trabajo repetido por nada.
   *
   * Cuando es `false` —toda empresa sin doble validación— las columnas Separado y Disponible no se
   * dibujan y la tabla queda exactamente como estaba.
   */
  stockConReservas = false;
  movimientosList: InventarioGestionMovimientoDto[] = [];
  // Paginación client-side del histórico (perf: no renderizar miles de filas de una).
  // El export CSV sigue usando movimientosList completo.
  readonly historicoPageSize = 100;
  historicoPage = 0;
  movimientosPagina: InventarioGestionMovimientoDto[] = [];
  itemsList: ItemInventarioDto[] = [];
  loading = false;
  /** Descarga del Excel de stock en curso (consulta aparte, sin filtro de granja). */
  exportandoStock = false;

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
  ingresoItems: ItemInventarioDto[] = [];
  submittingIngreso = false;
  /** Fecha del ingreso (yyyy-MM-dd) para el histórico; se define en el modal. */
  ingresoFechaMovimiento = '';
  showIngresoFechaModal = false;
  ingresoFechaDraft = '';
  /** «Este alimento es para el PRÓXIMO ciclo/encasetamiento de este galpón» (D2). Solo aplica a alimento con galpón. */
  ingresoParaProximoCiclo = false;

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
  trasladoItems: ItemInventarioDto[] = [];
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
  /** true = repartir lo recibido entre varios galpones de la granja destino (solo alimento por galpón). */
  recepcionDistribuir = false;
  /** Filas del reparto; su suma debe igualar la cantidad en tránsito. */
  recepcionDestinos: RecepcionDestinoRow[] = [];
  /** Tolerancia al comparar la suma del reparto (espejo de InventarioGestionRecepcionDistribucionCalculos). */
  private readonly recepcionToleranciaSuma = 0.0001;

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

  private allCatalogItems: ItemInventarioDto[] = [];

  // ── Inventario por SILO (Santa Reyes) ───────────────────────────────────────
  // El silo/bodega ES la ubicación del saldo: núcleo y galpón solo acotan qué silos se ofrecen
  // (un silo puede alimentar varios galpones, así que el saldo NO se parte por galpón).
  // Con el flag apagado nada de esto se pinta ni se envía: `manejaPorSilo` es false y el backend
  // rechaza cualquier silo que llegue igual.

  /** Silos elegibles para el ingreso (según granja y, si hay, galpón de destino). */
  ingresoSilos: InventarioGestionSiloDto[] = [];
  ingresoSiloId: number | null = null;

  /** Silos elegibles en el origen y el destino del traslado. */
  trasladoSilosOrigen: InventarioGestionSiloDto[] = [];
  trasladoSilosDestino: InventarioGestionSiloDto[] = [];
  fromSiloId: number | null = null;
  toSiloId: number | null = null;

  /** Silos de la granja que recibe un tránsito inter-granja. */
  recepcionSilos: InventarioGestionSiloDto[] = [];
  recepcionToSiloId: number | null = null;

  /**
   * Fase 3 (paso 3) — Colombia opera el inventario modelo B unificado a NIVEL GRANJA:
   * el alimento NO usa núcleo/galpón (stock B con nucleo/galpon NULL). Se calcula una vez
   * (el país activo no cambia dentro de la vista) para no romper change detection.
   * Ecuador/Panamá quedan igual (alimento con núcleo/galpón).
   */
  readonly isColombiaInventario: boolean;

  /**
   * Extremos de la ventana de fechas de los movimientos manuales (mes en curso ∪ últimos 15 días →
   * hoy, o sin piso con el permiso de fecha retroactiva). Campos, no getters: el template los lee en
   * cada ciclo y tienen que ser referencias estables. `min` es `string | null`: `null` = sin piso,
   * el template lo liga con `[attr.min]` para que el atributo directamente no exista.
   */
  fechaMovimientoMin: string | null = '';
  fechaMovimientoMax = '';
  hintFechaMovimientoTexto = '';

  /**
   * D4 — ventana del INGRESO informada por el backend para la ubicación elegida. `null` mientras no
   * se consultó: ahí manda la ventana clásica. Los extremos y el hint son campos, no getters, por lo
   * mismo que los de arriba (referencias estables para el template).
   */
  ventanaIngreso: VentanaFechaIngreso | null = null;
  fechaIngresoMin: string | null = '';
  fechaIngresoMax = '';
  hintFechaIngresoTexto = '';

  /** Permiso `registros.fecha_retroactiva`: destraba el campo de fecha más allá de la ventana base. */
  readonly puedeRetroactivar: boolean;

  constructor(
    private svc: GestionInventarioService,
    private route: ActivatedRoute,
    private country: CountryFilterService,
    private userPermService: UserPermissionService,
    private confirmDialog: ConfirmDialogService
  ) {
    this.isColombiaInventario = this.country.isColombia();
    this.puedeRetroactivar = this.userPermService.has(PERMISO_FECHA_RETROACTIVA);
  }

  ngOnInit(): void {
    const hoy = new Date();
    const ventana = ventanaFechaMovimiento(hoy, this.puedeRetroactivar);
    this.fechaMovimientoMin = ventana.min;
    this.fechaMovimientoMax = ventana.max;
    this.hintFechaMovimientoTexto = hintVentanaFechaRegistro(hoy, this.puedeRetroactivar);
    this.aplicarVentanaIngreso(null);
    this.ingresoFechaMovimiento = this.todayYmd();
    this.trasladoFechaMovimiento = this.todayYmd();
    // Colombia: el inventario es a nivel granja → traslado siempre entre granjas (no galpón-a-galpón).
    if (this.isColombiaInventario) {
      this.trasladoModo = 'interGranja';
    }
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

  /** Lotes en granjas asignadas + valores distintos de concepto/tipo/estado en movimientos.
   *  Meta ESTÁTICA de la sesión (opciones de filtro): se cachea → no se re-pide al revisitar
   *  el tab Histórico. Los movimientos (datos que cambian) sí refrescan en cada visita. */
  private loadHistoricoMeta(): void {
    if (this.historicoMeta) return; // ya cargada esta sesión → evita petición redundante
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
    return this.listaEstable('hEstados', Array.from(set).sort((a, b) => a.localeCompare(b, 'es')), x => x);
  }

  get historicoConceptosOptions(): string[] {
    return this.listaEstable('hConceptos', this.historicoMeta?.conceptosEnHistorico ?? [], x => x);
  }

  get historicoTiposItemOptions(): string[] {
    return this.listaEstable('hTiposItem', this.historicoMeta?.tiposItemEnHistorico ?? [], x => x);
  }

  get historicoMovementTypesOptions(): string[] {
    const raw = this.historicoMeta?.movementTypesEnHistorico ?? [];
    return this.listaEstable('hMovTypes', [...raw].sort((a, b) => a.localeCompare(b)), x => x);
  }

  get historicoUnidadesOptions(): string[] {
    const raw = this.historicoMeta?.unidadesEnHistorico ?? [];
    return this.listaEstable('hUnidades', [...raw].sort((a, b) => a.localeCompare(b)), x => x);
  }

  get historicoTiposOperacionOptions(): string[] {
    const raw = this.historicoMeta?.tiposOperacionEnHistorico ?? [];
    return this.listaEstable('hTiposOp', [...raw].sort((a, b) => a.localeCompare(b, 'es')), x => x);
  }

  /** Ítems del catálogo para filtro exacto por ID (además de búsqueda texto). */
  get historicoItemsCatalogSorted(): ItemInventarioDto[] {
    return this.listaEstable(
      'hItemsCatalog',
      [...this.allCatalogItems].sort((a, b) => (a.codigo ?? '').localeCompare(b.codigo ?? '', undefined, { numeric: true })),
      x => String(x.id)
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
    return this.listaEstable(`hLotes|${this.historicoFarmId ?? ''}|${nid}|${gid}`, out, l => String(l.loteId));
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
        this.historicoPage = 0;
        this.recomputeHistoricoPagina();
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.openAlertModal('error', 'Error', 'Error al cargar histórico.');
      }
    });
  }

  // ===== Paginación client-side del histórico (perf) =====
  /** Recalcula el slice visible; se llama al cargar o al cambiar de página (no en getter → sin alocar por ciclo). */
  private recomputeHistoricoPagina(): void {
    const start = this.historicoPage * this.historicoPageSize;
    this.movimientosPagina = this.movimientosList.slice(start, start + this.historicoPageSize);
  }
  get historicoTotalPaginas(): number {
    return Math.max(1, Math.ceil(this.movimientosList.length / this.historicoPageSize));
  }
  historicoPrevPagina(): void {
    if (this.historicoPage > 0) { this.historicoPage--; this.recomputeHistoricoPagina(); }
  }
  historicoNextPagina(): void {
    if (this.historicoPage < this.historicoTotalPaginas - 1) { this.historicoPage++; this.recomputeHistoricoPagina(); }
  }

  get historicoNucleosFiltered(): { nucleoId: string; nucleoNombre: string }[] {
    if (!this.filterData || this.historicoFarmId == null) return GestionInventarioPageComponent._emptyList as never[];
    const computed = this.filterData.nucleosOrigen
      .filter(n => n.granjaId === this.historicoFarmId)
      .map(n => ({ nucleoId: n.nucleoId, nucleoNombre: n.nucleoNombre }));
    return this.listaEstable(`hNuc|${this.historicoFarmId}`, computed, x => x.nucleoId);
  }

  get historicoGalponesFiltered(): { galponId: string; galponNombre: string }[] {
    if (!this.filterData || this.historicoFarmId == null) return GestionInventarioPageComponent._emptyList as never[];
    const computed = this.filterData.galponesOrigen
      .filter(g => g.granjaId === this.historicoFarmId && (!this.historicoNucleoId || g.nucleoId === this.historicoNucleoId))
      .map(g => ({ galponId: g.galponId, galponNombre: g.galponNombre }));
    return this.listaEstable(`hGal|${this.historicoFarmId}|${this.historicoNucleoId ?? ''}`, computed, x => x.galponId);
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
    if (!this.filterData || this.historicoFromFarmId == null) return GestionInventarioPageComponent._emptyList as never[];
    const computed = this.filterData.nucleosOrigen
      .filter(n => n.granjaId === this.historicoFromFarmId)
      .map(n => ({ nucleoId: n.nucleoId, nucleoNombre: n.nucleoNombre }));
    return this.listaEstable(`hFromNuc|${this.historicoFromFarmId}`, computed, x => x.nucleoId);
  }

  get historicoFromGalponesFiltered(): { galponId: string; galponNombre: string }[] {
    if (!this.filterData || this.historicoFromFarmId == null) return GestionInventarioPageComponent._emptyList as never[];
    const computed = this.filterData.galponesOrigen
      .filter(
        g =>
          g.granjaId === this.historicoFromFarmId &&
          (!this.historicoFromNucleoId || g.nucleoId === this.historicoFromNucleoId)
      )
      .map(g => ({ galponId: g.galponId, galponNombre: g.galponNombre }));
    return this.listaEstable(`hFromGal|${this.historicoFromFarmId}|${this.historicoFromNucleoId ?? ''}`, computed, x => x.galponId);
  }

  /** Con lote seleccionado el API usa la ubicación del lote; se ignoran núcleo/galpón manuales. */
  onHistoricoLoteChange(): void {
    if (this.historicoLoteId != null && this.historicoLoteId > 0) {
      this.historicoNucleoId = null;
      this.historicoGalponId = null;
    }
  }

  trackByHistoricoMov = (_: number, m: InventarioGestionMovimientoDto) => m.id;

  formatFechaMovimiento(iso: string): string { return formatFechaMovimientoFn(iso); }

  /** Fecha de ingreso en la grilla de stock (solo fecha, sin hora). */
  formatFechaIngresoStock(iso: string | null | undefined): string { return formatFechaIngresoStockFn(iso); }

  /** Convierte fecha del API a yyyy-MM-dd para input type="date". */
  fechaIngresoStockToYmd(iso: string | null | undefined): string { return fechaIngresoStockToYmdFn(iso); }

  /** Ubicación del movimiento. Ver gestion-inventario-page-formato.funcion.ts. */
  ubicacionRegistroMovimiento(m: InventarioGestionMovimientoDto): string { return ubicacionRegistroMovimientoFn(m); }

  /** Origen/destino según tipo: contraparte del traslado o procedencia. */
  otroExtremoMovimiento(m: InventarioGestionMovimientoDto): string { return otroExtremoMovimientoFn(m); }

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
        // Recién acá se sabe si la empresa maneja silos: si preseleccionó granja, ya se pueden
        // ofrecer sus silos sin que el usuario tenga que tocar el selector de granja.
        if (this.manejaPorSilo) {
          // El gate por país de `ngOnInit` (Colombia ⇒ solo inter-granja, porque el inventario es
          // a nivel granja) NO aplica acá: con silos, bodega → silo DENTRO de la misma granja es
          // el movimiento habitual. Se vuelve al modo interno como default.
          if (this.trasladoModo === 'interGranja' && this.isColombiaInventario) {
            this.trasladoModo = 'mismaGranja';
            if (this.fromFarmId != null) this.toFarmId = this.fromFarmId;
          }
          this.loadIngresoSilos();
          this.loadTrasladoSilosOrigen();
          this.loadTrasladoSilosDestino();
        }
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
    const tabKeys: TabKey[] = ['stock', 'ingresos', 'traslados', 'transito', 'historico', 'items', 'cuadre'];
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
        this.stockConReservas = list.some(s => Number(s.reservadoKg ?? 0) !== 0);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.openAlertModal('error', 'Error', 'Error al cargar stock.');
      }
    });
  }

  /**
   * Descarga el stock COMPLETO en `.xlsx`: todas las granjas asignadas y todos los conceptos,
   * repartidos en dos hojas (Alimento con núcleo/galpón · Otros conceptos a nivel granja).
   *
   * Ignora a propósito los filtros de la pantalla salvo la búsqueda de ítem:
   * - granja/núcleo/galpón, porque el pedido es comparar TODAS las bodegas en un archivo;
   * - concepto, porque las dos hojas del archivo YA son la partición por concepto.
   *
   * Consulta propia al API (`stockList` puede venir recortado a una sola granja o concepto).
   */
  descargarStockExcel(): void {
    if (this.exportandoStock) return;
    this.exportandoStock = true;

    const params: { search?: string } = {};
    const q = (this.searchTerm ?? '').trim();
    if (q) params.search = q;

    this.svc.getStock(params).subscribe({
      next: (list) => {
        this.exportandoStock = false;
        if (!list.length) {
          this.openAlertModal('error', 'Sin datos', 'No hay stock para exportar en sus granjas asignadas.');
          return;
        }
        exportarStockExcel(list, {
          // Inventario por silo: núcleo/galpón van NULL en el 100 % de las filas → sale la columna
          // Silo en su lugar. Para el resto de empresas, exactamente lo de antes.
          incluirUbicacion: !this.isColombiaInventario && !this.manejaPorSilo,
          incluirSilo: this.manejaPorSilo,
          filtros: this.filtrosStockExport(list)
        });
      },
      error: () => {
        this.exportandoStock = false;
        this.openAlertModal('error', 'Error', 'No se pudo generar el Excel del stock.');
      }
    });
  }

  /**
   * Líneas de contexto comunes a las dos hojas del Excel. Dejan escrito que el archivo NO sigue
   * los filtros de granja ni de concepto de la pantalla (para que nadie lo lea como un recorte).
   */
  private filtrosStockExport(list: InventarioGestionStockDto[]): string[] {
    const granjas = new Set(list.map(s => s.granjaNombre ?? String(s.farmId)));
    const lineas = [
      `Granjas: todas las asignadas (${granjas.size}) — no aplica la granja seleccionada en pantalla`,
      'Conceptos: todos — repartidos en las hojas Alimento y Otros conceptos'
    ];
    const q = (this.searchTerm ?? '').trim();
    if (q) lineas.push(`Búsqueda de ítem aplicada: ${q}`);
    return lineas;
  }

  get nucleosFiltered(): { nucleoId: string; nucleoNombre: string }[] {
    if (!this.filterData || this.selectedFarmId == null) return GestionInventarioPageComponent._emptyList as never[];
    const computed = this.filterData.nucleosOrigen
      .filter(n => n.granjaId === this.selectedFarmId)
      .map(n => ({ nucleoId: n.nucleoId, nucleoNombre: n.nucleoNombre }));
    return this.listaEstable(`stkNuc|${this.selectedFarmId}`, computed, x => x.nucleoId);
  }

  get galponesFiltered(): { galponId: string; galponNombre: string }[] {
    if (!this.filterData || this.selectedFarmId == null) return GestionInventarioPageComponent._emptyList as never[];
    const computed = this.filterData.galponesOrigen
      .filter(g => g.granjaId === this.selectedFarmId && (!this.selectedNucleoId || g.nucleoId === this.selectedNucleoId))
      .map(g => ({ galponId: g.galponId, galponNombre: g.galponNombre }));
    return this.listaEstable(`stkGal|${this.selectedFarmId}|${this.selectedNucleoId ?? ''}`, computed, x => x.galponId);
  }

  /**
   * Estas listas se usan dentro de *ngFor en el template. Devolver un array nuevo en cada
   * ciclo de detección de cambios provoca NG0103 (Infinite change detection). El helper cachea
   * el array YA con su forma final; si el contenido es igual (mismos ids en el mismo orden)
   * devuelve la MISMA referencia previa para no romper el CD.
   */
  private readonly _listaFiltradaCache = new Map<string, unknown[]>();
  private static readonly _emptyList: readonly never[] = Object.freeze([]);
  private listaEstable<T>(key: string, computed: T[], idOf: (x: T) => string): T[] {
    if (computed.length === 0) return GestionInventarioPageComponent._emptyList as unknown as T[];
    const prev = this._listaFiltradaCache.get(key) as T[] | undefined;
    if (prev && prev.length === computed.length && prev.every((p, i) => idOf(p) === idOf(computed[i]))) {
      return prev;
    }
    this._listaFiltradaCache.set(key, computed);
    return computed;
  }

  fromNucleosFiltered(): { nucleoId: string; nucleoNombre: string }[] {
    if (!this.filterData || this.fromFarmId == null) return GestionInventarioPageComponent._emptyList as never[];
    const computed = this.filterData.nucleosOrigen
      .filter(n => n.granjaId === this.fromFarmId)
      .map(n => ({ nucleoId: n.nucleoId, nucleoNombre: n.nucleoNombre }));
    return this.listaEstable(`fromN|${this.fromFarmId}`, computed, x => x.nucleoId);
  }
  fromGalponesFiltered(): { galponId: string; galponNombre: string }[] {
    if (!this.filterData || this.fromFarmId == null) return GestionInventarioPageComponent._emptyList as never[];
    const computed = this.filterData.galponesOrigen
      .filter(g => g.granjaId === this.fromFarmId && (!this.fromNucleoId || g.nucleoId === this.fromNucleoId))
      .map(g => ({ galponId: g.galponId, galponNombre: g.galponNombre }));
    return this.listaEstable(`fromG|${this.fromFarmId}|${this.fromNucleoId ?? ''}`, computed, x => x.galponId);
  }
  /** Núcleos del destino del traslado: misma granja → catálogo origen; inter-granja → catálogo empresa. */
  toNucleosFiltered(): { nucleoId: string; nucleoNombre: string }[] {
    if (!this.filterData || this.toFarmId == null) return GestionInventarioPageComponent._emptyList as never[];
    const nucleos = this.trasladoModo === 'interGranja' ? this.filterData.nucleosDestino : this.filterData.nucleosOrigen;
    const computed = nucleos
      .filter(n => n.granjaId === this.toFarmId)
      .map(n => ({ nucleoId: n.nucleoId, nucleoNombre: n.nucleoNombre }));
    return this.listaEstable(`toN|${this.trasladoModo}|${this.toFarmId}`, computed, x => x.nucleoId);
  }
  toGalponesFiltered(): { galponId: string; galponNombre: string }[] {
    if (!this.filterData || this.toFarmId == null) return GestionInventarioPageComponent._emptyList as never[];
    const galpones = this.trasladoModo === 'interGranja' ? this.filterData.galponesDestino : this.filterData.galponesOrigen;
    const computed = galpones
      .filter(g => g.granjaId === this.toFarmId && (!this.toNucleoId || g.nucleoId === this.toNucleoId))
      .map(g => ({ galponId: g.galponId, galponNombre: g.galponNombre }));
    return this.listaEstable(`toG|${this.trasladoModo}|${this.toFarmId}|${this.toNucleoId ?? ''}`, computed, x => x.galponId);
  }

  ingresoNucleosFiltered(): { nucleoId: string; nucleoNombre: string }[] {
    if (!this.filterData || this.ingresoFarmId == null) return GestionInventarioPageComponent._emptyList as never[];
    const computed = this.filterData.nucleosDestino
      .filter(n => n.granjaId === this.ingresoFarmId)
      .map(n => ({ nucleoId: n.nucleoId, nucleoNombre: n.nucleoNombre }));
    return this.listaEstable(`ingN|${this.ingresoFarmId}`, computed, x => x.nucleoId);
  }
  ingresoGalponesFiltered(): { galponId: string; galponNombre: string }[] {
    if (!this.filterData || this.ingresoFarmId == null) return GestionInventarioPageComponent._emptyList as never[];
    const computed = this.filterData.galponesDestino
      .filter(g => g.granjaId === this.ingresoFarmId && (!this.ingresoNucleoId || g.nucleoId === this.ingresoNucleoId))
      .map(g => ({ galponId: g.galponId, galponNombre: g.galponNombre }));
    return this.listaEstable(`ingG|${this.ingresoFarmId}|${this.ingresoNucleoId ?? ''}`, computed, x => x.galponId);
  }

  /**
   * ¿La empresa activa ubica el inventario en SILOS? Fail-closed: si `filter-data` no trae el flag
   * (backend viejo, error de red) queda en `false` y la pantalla es exactamente la de siempre.
   */
  get manejaPorSilo(): boolean {
    return this.filterData?.companyManejaInventarioPorSilo === true;
  }

  get showNucleoGalpon(): boolean {
    // Colombia: inventario a nivel granja → nunca núcleo/galpón (aunque sea alimento).
    if (this.isColombiaInventario) return false;
    return this.isAlimentoConcept(this.selectedConcept);
  }

  /**
   * Con inventario por silo, núcleo y galpón siguen en pantalla pero como FILTRO de la lista de
   * silos: dejan de ser obligatorios porque no se persisten (el backend los anula).
   */
  get ubicacionEsFiltroDeSilo(): boolean {
    return this.manejaPorSilo;
  }

  /** Carga los silos elegibles de una ubicación; con el flag apagado el API devuelve `[]`. */
  private cargarSilos(
    farmId: number | null,
    nucleoId: string | null,
    galponId: string | null,
    asignar: (list: InventarioGestionSiloDto[]) => void
  ): void {
    if (!this.manejaPorSilo || farmId == null) {
      asignar([]);
      return;
    }
    this.svc.getSilos(farmId, nucleoId, galponId).subscribe({
      next: (list) => asignar(list ?? []),
      error: () => asignar([])
    });
  }

  /** Silos del destino del ingreso. Si el silo elegido ya no está en la lista, se limpia. */
  loadIngresoSilos(): void {
    this.cargarSilos(this.ingresoFarmId, this.ingresoNucleoId, this.ingresoGalponId, (list) => {
      this.ingresoSilos = list;
      if (this.ingresoSiloId != null && !list.some(s => s.id === this.ingresoSiloId)) {
        this.ingresoSiloId = null;
      }
    });
  }

  loadTrasladoSilosOrigen(): void {
    this.cargarSilos(this.fromFarmId, this.fromNucleoId, this.fromGalponId, (list) => {
      this.trasladoSilosOrigen = list;
      if (this.fromSiloId != null && !list.some(s => s.id === this.fromSiloId)) {
        this.fromSiloId = null;
      }
    });
  }

  loadTrasladoSilosDestino(): void {
    const farmId = this.trasladoModo === 'mismaGranja' ? this.fromFarmId : this.toFarmId;
    this.cargarSilos(farmId, this.toNucleoId, this.toGalponId, (list) => {
      this.trasladoSilosDestino = list;
      if (this.toSiloId != null && !list.some(s => s.id === this.toSiloId)) {
        this.toSiloId = null;
      }
    });
  }

  /** Etiqueta del selector: la bodega se distingue del silo porque no cuelga de ningún galpón. */
  siloOptionLabel(s: InventarioGestionSiloDto): string { return siloOptionLabelFn(s); }

  /**
   * INGRESO: ¿este ingreso se maneja a nivel GALPÓN? Se resuelve por la granja de destino
   * del ingreso (override) y, si hereda (null), por el default de la empresa; sin dato cae al país.
   * Reemplaza la decisión por país SOLO en el formulario de ingreso (traslado/stock siguen por país).
   */
  get ingresoEsPorGalpon(): boolean {
    if (!this.isAlimentoConcept(this.selectedConcept)) return false;
    const farms = this.filterData?.farmsDestino ?? [];
    const farm = this.ingresoFarmId != null ? farms.find(f => f.id === this.ingresoFarmId) : undefined;
    const override = farm?.manejaAlimentoPorGalpon;
    if (override === true) return true;
    if (override === false) return false;
    // override null/undefined (hereda empresa o sin granja seleccionada) → default empresa
    const companyDefault = this.filterData?.companyManejaAlimentoPorGalpon;
    if (companyDefault === true) return true;
    if (companyDefault === false) return false;
    // sin dato del backend → comportamiento por país (compatibilidad)
    return !this.isColombiaInventario;
  }

  /**
   * ⛔ DESHABILITADO (09-ago-2026) — la marca «para el próximo ciclo» NO se puede usar todavía.
   *
   * El checkbox se entregó en `801b14f`, pero la auditoría posterior midió que marcar un movimiento
   * ROMPE LA CONSERVACIÓN de kilos en **729 de 2.210 casos reales** (hasta 37.467 kg que desaparecen
   * de toda tabla diaria) y deja filas de la grilla en negativo: la fn diaria le quita el movimiento a
   * TODO lote con seguimiento, incluidos los que CONVIVEN con el ciclo destino, y en esos galpones
   * ninguna apertura lo vuelve a tomar. Cuatro intentos de arreglarlo terminaron revertidos porque
   * cada guarda mudaba el defecto de lugar (ver `fase_de_desarrollo/marca_proximo_ciclo_rediseno_plan.md`
   * y el bloque «v16 ... REVERTIDA» de `tracker_estado.md`).
   *
   * Se oculta el checkbox en vez de borrar la feature: la columna, el endpoint y el badge del historial
   * siguen vivos, y el historial permite QUITAR una marca existente (nunca dejar kilos invisibles).
   * Para reactivarlo, primero tiene que estar el rediseño (atribución persistida como hecho, no
   * recalculada en lectura) con su compuerta de no-negativos.
   */
  get mostrarParaProximoCicloIngreso(): boolean {
    return false;
  }

  /** Stock: mostrar filtros/columnas núcleo+galpón si el filtro es «todos» o alimento. */
  get stockShowNucleoGalpon(): boolean {
    // Inventario por silo: núcleo y galpón se persisten NULL a propósito → las columnas irían
    // vacías en el 100 % de las filas. En su lugar se muestra la columna Silo.
    if (this.manejaPorSilo) return false;
    // Colombia: stock a nivel granja → sin columnas núcleo/galpón.
    if (this.isColombiaInventario) return false;
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
      this.ingresoParaProximoCiclo = false;
      this.fromNucleoId = null;
      this.fromGalponId = null;
      this.toNucleoId = null;
      this.toGalponId = null;
    }
    // Con inventario por silo el traslado interno NO es cosa de alimento: bodega→silo mueve
    // insumos igual. Solo el modelo clásico obliga a salir de «misma granja» fuera de alimento.
    if (this.trasladoModo === 'mismaGranja' && !this.showNucleoGalpon && !this.manejaPorSilo) {
      this.trasladoModo = 'interGranja';
    }
  }

  onTrasladoModoChange(): void {
    this.toNucleoId = null;
    this.toGalponId = null;
    this.toSiloId = null;
    if (this.trasladoModo === 'mismaGranja' && this.fromFarmId != null) {
      this.toFarmId = this.fromFarmId;
    }
    this.loadOriginStock();
    this.loadTrasladoSilosDestino();
  }

  onTrasladoFromFarmChange(): void {
    if (this.trasladoModo === 'mismaGranja' && this.fromFarmId != null) {
      this.toFarmId = this.fromFarmId;
    }
    this.fromSiloId = null;
    this.loadOriginStock();
    this.loadTrasladoSilosOrigen();
    this.loadTrasladoSilosDestino();
  }

  /** Origen del traslado: núcleo/galpón acotan la lista de silos y el stock disponible. */
  onTrasladoFromUbicacionChange(): void {
    this.loadOriginStock();
    this.loadTrasladoSilosOrigen();
  }

  onTrasladoToFarmChange(): void {
    this.toSiloId = null;
    this.loadTrasladoSilosDestino();
  }

  /** Con inventario por silo el disponible se lee del SILO de origen, no del galpón. */
  onTrasladoFromSiloChange(): void {
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
    this.ingresoParaProximoCiclo = false;
    this.ingresoSiloId = null;
    this.loadIngresoSilos();
    this.loadVentanaIngreso();
  }

  onIngresoDestinoNucleoChange(): void {
    this.ingresoGalponId = null;
    this.ingresoParaProximoCiclo = false;
    this.loadIngresoSilos();
    this.loadVentanaIngreso();
  }

  onIngresoDestinoGalponChange(): void {
    this.ingresoParaProximoCiclo = false;
    this.loadIngresoSilos();
    this.loadVentanaIngreso();
  }

  /**
   * D4 — trae del backend la ventana de fechas del ingreso para la ubicación elegida. Es lo que hace
   * que el datepicker deje tipear la fecha REAL del alimento que llegó antes del encasetamiento, en
   * vez de empujar a falsearla al primer día del mes.
   *
   * Fail-OPEN a propósito (al revés que los flags de empresa): si no hay granja o la consulta falla,
   * se vuelve a la ventana clásica y el rechazo lo hace el controller, que es quien manda. Cerrar
   * acá sería reponer el bloqueo que este cambio viene a sacar.
   */
  private loadVentanaIngreso(): void {
    if (this.ingresoFarmId == null) {
      this.aplicarVentanaIngreso(null);
      return;
    }
    this.svc
      .getVentanaFechaIngreso({
        farmId: this.ingresoFarmId,
        nucleoId: this.ingresoNucleoId,
        galponId: this.ingresoGalponId
      })
      .subscribe({
        next: (v) => this.aplicarVentanaIngreso(v ?? null),
        error: () => this.aplicarVentanaIngreso(null)
      });
  }

  /** Deja los extremos y el hint listos para el template (referencias estables). */
  private aplicarVentanaIngreso(ventana: VentanaFechaIngreso | null): void {
    const hoy = new Date();
    this.ventanaIngreso = ventana;
    const extremos = extremosFechaIngreso(hoy, ventana, this.puedeRetroactivar);
    this.fechaIngresoMin = extremos.min;
    this.fechaIngresoMax = extremos.max;
    this.hintFechaIngresoTexto = hintFechaIngreso(hoy, ventana, this.puedeRetroactivar);
  }

  submitIngreso(): void {
    if (!this.ingresoFechaMovimiento?.trim()) {
      this.openAlertModal('error', 'Validación', 'Indique la fecha del movimiento.');
      return;
    }
    if (!this.validarVentanaFechaIngreso(this.ingresoFechaMovimiento)) return;
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
    if (this.manejaPorSilo) {
      // El silo es obligatorio para TODO concepto (alimento e insumos): en este modelo hasta la
      // bodega es una ubicación con saldo propio, no hay movimiento «a nivel granja».
      if (this.ingresoSiloId == null) {
        this.openAlertModal('error', 'Validación', 'Debe indicar el silo o la bodega donde queda el ingreso.');
        return;
      }
    } else if (this.ingresoEsPorGalpon && (!this.ingresoNucleoId || !this.ingresoGalponId)) {
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
    if (!this.validarVentanaFecha(this.trasladoFechaMovimiento)) return;

    if (this.manejaPorSilo) {
      // El silo ES la ubicación: núcleo y galpón no se validan porque no se persisten.
      if (this.fromSiloId == null) {
        this.openAlertModal('error', 'Validación', 'Debe indicar el silo o la bodega de ORIGEN.');
        return;
      }
      if (this.trasladoModo === 'mismaGranja') {
        if (this.fromFarmId !== this.toFarmId) {
          this.openAlertModal('error', 'Validación', 'En este modo origen y destino deben ser la misma granja.');
          return;
        }
        if (this.toSiloId == null) {
          this.openAlertModal('error', 'Validación', 'Debe indicar el silo o la bodega de DESTINO.');
          return;
        }
        if (this.fromSiloId === this.toSiloId) {
          this.openAlertModal('error', 'Validación', 'El silo de destino debe ser distinto al de origen.');
          return;
        }
      } else if (this.fromFarmId === this.toFarmId) {
        this.openAlertModal('error', 'Validación', 'En traslado entre granjas la granja destino debe ser distinta a la de origen.');
        return;
      }
    } else if (this.trasladoModo === 'mismaGranja') {
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
    // Con inventario por silo el disponible vive en el SILO: sin silo elegido no hay nada que medir
    // (y el saldo de la granja entera sería un número que nadie va a poder trasladar).
    if (this.manejaPorSilo && this.fromSiloId == null) {
      this.originStockQuantity = null;
      return;
    }
    if (!this.manejaPorSilo && this.showNucleoGalpon && (!this.fromNucleoId || !this.fromGalponId)) {
      this.originStockQuantity = null;
      return;
    }
    this.loadingOriginStock = true;
    this.originStockQuantity = null;
    const params: any = { farmId: this.fromFarmId };
    if (!this.manejaPorSilo && this.showNucleoGalpon) {
      params.nucleoId = this.fromNucleoId;
      params.galponId = this.fromGalponId;
    }
    const siloId = this.manejaPorSilo ? this.fromSiloId : null;
    this.svc.getStock(params).subscribe({
      next: (list) => {
        const row = list.find(
          s => s.itemInventarioEcuadorId === itemId && (siloId == null || s.siloId === siloId)
        );
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
    const computed = this.filterData?.farmsOrigen?.map(f => ({ id: f.id, name: f.name })) ?? [];
    return this.listaEstable('farmsOrigen', computed, x => String(x.id));
  }

  get farmsDestino(): { id: number; name: string }[] {
    const computed = this.filterData?.farmsDestino?.map(f => ({ id: f.id, name: f.name })) ?? [];
    return this.listaEstable('farmsDestino', computed, x => String(x.id));
  }

  getFarmName(farmId: number | null): string {
    if (farmId == null) return '—';
    const hit =
      this.farmsOrigen.find(f => f.id === farmId) ?? this.farmsDestino.find(f => f.id === farmId);
    return hit?.name ?? String(farmId);
  }

  onToDestinoNucleoChange(): void {
    this.toGalponId = null;
    this.loadTrasladoSilosDestino();
  }

  onToDestinoGalponChange(): void {
    this.loadTrasladoSilosDestino();
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

  private filterByConcept(items: ItemInventarioDto[], concept: string | null | undefined): ItemInventarioDto[] {
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
    // TK-2026-000019 — la unidad es SOLO LECTURA: viene del catálogo del ítem (el backend la
    // resuelve así e ignora la que se mande). Cuando este campo era editable, operación lo usaba
    // para tapar el «kg» que mostraba el stock y por eso la base quedó con «LT», «UND», «GALONES».
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
    if (!this.validarVentanaFecha(fechaIngreso)) return;
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

  /**
   * Guarda de la ventana de fechas (día 1 del mes en curso → hoy) de los movimientos cargados a
   * mano. La regla que manda es la del backend; acá se ahorra el viaje y se explica en pantalla.
   * Devuelve `true` si la fecha sirve; si no, abre el modal de error y devuelve `false`.
   */
  private validarVentanaFecha(ymd: string | null | undefined): boolean {
    const hoy = new Date();
    if (esFechaMovimientoPermitida(ymd, hoy, this.puedeRetroactivar)) return true;
    this.openAlertModal('error', 'Validación', mensajeFechaFueraDeVentana(hoy, this.puedeRetroactivar));
    return false;
  }

  /**
   * D4 — guarda de las puertas de INGRESO. Sólo corta lo que ninguna ventana admite (el futuro, o
   * antes del mínimo ofrecido); el hueco entre el mes en curso y la ventana del encasetamiento viaja
   * y lo resuelve el controller, que es la única punta que sabe qué encaset corresponde a esa fecha.
   */
  private validarVentanaFechaIngreso(ymd: string | null | undefined): boolean {
    const hoy = new Date();
    if (esFechaIngresoOfrecible(ymd, hoy, this.ventanaIngreso, this.puedeRetroactivar)) return true;
    this.openAlertModal(
      'error',
      'Validación',
      mensajeFechaIngresoFueraDeVentana(hoy, this.ventanaIngreso, this.puedeRetroactivar)
    );
    return false;
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
    if (!this.validarVentanaFechaIngreso(d)) return;
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
    this.enviarIngreso({
      farmId: this.ingresoFarmId!,
      nucleoId: this.ingresoEsPorGalpon ? this.ingresoNucleoId : null,
      galponId: this.ingresoEsPorGalpon ? this.ingresoGalponId : null,
      itemInventarioEcuadorId: this.ingresoItemInventarioEcuadorId!,
      quantity: this.ingresoQuantity,
      unit: this.ingresoSelectedUnit === '—' ? 'kg' : this.ingresoSelectedUnit,
      reference: this.ingresoReference || null,
      reason,
      origenTipo: tipoOrigen,
      origenFarmId: esAlimento && (tipoOrigen === 'granja' || tipoOrigen === 'bodega') ? this.ingresoOrigenFarmId : null,
      origenBodegaDescripcion: esAlimento && tipoOrigen === 'bodega' ? (this.ingresoOrigenBodegaTexto || null) : null,
      fechaMovimiento: this.ingresoFechaMovimiento?.trim() || null,
      paraProximoCiclo: this.mostrarParaProximoCicloIngreso ? this.ingresoParaProximoCiclo : false,
      // Solo se manda con el flag encendido: con el flag apagado el backend rechaza el silo.
      siloId: this.manejaPorSilo ? this.ingresoSiloId : null
    });
  }

  /**
   * Envía el ingreso y, si el backend avisa que esa remisión ya está cargada (409), pregunta antes
   * de insistir.
   *
   * 🔴 Existe porque `RegistrarIngresoAsync` no consultaba si el ingreso ya estaba: dos cargas del
   * mismo remito suman kilos que nunca entraron al galpón. Pasó en producción —y con DOS usuarios
   * distintos cargando el mismo remito, así que ninguna guarda de front lo habría atrapado—; el caso
   * se cerró corrigiendo los datos, sin tocar el camino que lo permite.
   *
   * Es un aviso confirmable y no un bloqueo: repetir una remisión es raro pero legítimo (una entrega
   * que llega en dos viajes). El backend decide, el usuario confirma y se reenvía con la bandera.
   */
  private enviarIngreso(payload: InventarioGestionIngresoRequest): void {
    this.svc.registrarIngreso(payload).subscribe({
      next: () => {
        this.submittingIngreso = false;
        this.openAlertModal('success', 'Listo', 'Ingreso registrado correctamente.');
        this.ingresoQuantity = 0;
        this.ingresoReference = '';
        this.ingresoReason = '';
        this.ingresoOrigenBodegaTexto = '';
        this.ingresoParaProximoCiclo = false;
        this.ingresoFechaMovimiento = this.todayYmd();
        this.loadStock();
      },
      error: (err) => {
        this.submittingIngreso = false;

        if (err?.status === 409 && err?.error?.duplicado === true) {
          void this.confirmarYReenviarIngreso(payload, err.error?.message);
          return;
        }

        this.openAlertModal('error', 'Error', err.error?.message || 'Error al registrar ingreso.');
      }
    });
  }

  private async confirmarYReenviarIngreso(
    payload: InventarioGestionIngresoRequest, mensaje?: string
  ): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: 'Esta remisión ya está cargada',
      message: mensaje || 'Ya hay un ingreso registrado con esta remisión en la misma ubicación.',
      type: 'warning',
      confirmText: 'Registrarlo igual'
    });
    if (!ok) return;

    this.submittingIngreso = true;
    this.enviarIngreso({ ...payload, confirmarDuplicado: true });
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
    this.recepcionDistribuir = false;
    this.recepcionDestinos = [];
    // Los silos son los de la granja QUE RECIBE, no los del origen del tránsito.
    this.recepcionToSiloId = null;
    if (this.manejaPorSilo) {
      // El hint de núcleo/galpón que trae el tránsito no aplica: acá la ubicación es el silo, y
      // dejarlo puesto haría que la validación de «solo a nivel granja» rechazara la recepción.
      this.recepcionToNucleoId = null;
      this.recepcionToGalponId = null;
    }
    this.cargarSilos(row.toFarmId, null, null, (list) => (this.recepcionSilos = list));
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
    this.recepcionDistribuir = false;
    this.recepcionDestinos = [];
    this.recepcionToSiloId = null;
    this.recepcionSilos = [];
  }

  // ===== Recepción distribuida entre galpones =====

  /**
   * Alterna entre recibir todo en un galpón y repartir entre varios. Al activar el reparto se
   * precarga una fila con el destino sugerido y la cantidad completa (el usuario la ajusta).
   */
  setRecepcionDistribuir(distribuir: boolean): void {
    this.recepcionDistribuir = distribuir;
    if (!distribuir) return;
    if (this.recepcionDestinos.length === 0) {
      this.recepcionDestinos = [
        {
          nucleoId: this.recepcionToNucleoId,
          galponId: this.recepcionToGalponId,
          quantity: this.recepcionPendiente?.quantity ?? null,
          siloId: this.recepcionToSiloId
        }
      ];
    }
  }

  addRecepcionDestino(): void {
    const restante = this.recepcionRestante();
    this.recepcionDestinos = [
      ...this.recepcionDestinos,
      { nucleoId: null, galponId: null, quantity: restante > 0 ? restante : null, siloId: null }
    ];
  }

  removeRecepcionDestino(index: number): void {
    this.recepcionDestinos = this.recepcionDestinos.filter((_, i) => i !== index);
  }

  onRecepcionDestinoNucleoChange(index: number): void {
    const fila = this.recepcionDestinos[index];
    if (fila) fila.galponId = null;
  }

  /** Cantidad ya repartida entre los galpones indicados. */
  recepcionDistribuido(): number {
    const total = this.recepcionDestinos.reduce((acc, d) => acc + (Number(d.quantity) || 0), 0);
    return this.redondearCantidad(total);
  }

  /** Cantidad que falta por repartir (negativa = se pasó de la cantidad en tránsito). */
  recepcionRestante(): number {
    const total = this.recepcionPendiente?.quantity ?? 0;
    return this.redondearCantidad(total - this.recepcionDistribuido());
  }

  /** Galpones de un núcleo concreto de la granja destino (lista estable por fila). */
  recepcionGalponesForNucleo(farmId: number, nucleoId: string | null): { galponId: string; galponNombre: string }[] {
    if (!this.filterData || !nucleoId) return GestionInventarioPageComponent._emptyList as never[];
    const computed = this.filterData.galponesDestino
      .filter(g => g.granjaId === farmId && g.nucleoId === nucleoId)
      .map(g => ({ galponId: g.galponId, galponNombre: g.galponNombre }));
    return this.listaEstable(`recGN|${farmId}|${nucleoId}`, computed, x => x.galponId);
  }

  /** Evita los residuos de coma flotante al sumar/restar cantidades. */
  private redondearCantidad(n: number): number {
    return Math.round(n * 10000) / 10000;
  }

  /** Valida el reparto con las mismas reglas (y mensajes) del backend. Devuelve el error o null. */
  private validarRecepcionDistribucion(cantidadTransito: number): string | null {
    if (this.manejaPorSilo) return this.validarRecepcionDistribucionPorSilo(cantidadTransito);

    const filas = this.recepcionDestinos.filter(d => !!d.nucleoId || !!d.galponId || (Number(d.quantity) || 0) !== 0);
    if (filas.length === 0) return 'Agregue al menos un galpón a la distribución.';

    const vistos = new Set<string>();
    for (const fila of filas) {
      if (!fila.nucleoId || !fila.galponId) return 'Cada destino de la distribución debe indicar Núcleo y Galpón.';
      if (!(Number(fila.quantity) > 0)) return 'Las cantidades de la distribución deben ser mayores a cero.';
      const clave = `${fila.nucleoId}|${fila.galponId}`;
      if (vistos.has(clave)) return `No repita el mismo galpón en la distribución (galpón ${fila.galponId}).`;
      vistos.add(clave);
    }

    const suma = this.recepcionDistribuido();
    if (Math.abs(suma - cantidadTransito) > this.recepcionToleranciaSuma) {
      return `La suma de la distribución (${suma}) debe ser igual a la cantidad en tránsito (${cantidadTransito}).`;
    }
    return null;
  }

  /**
   * Espejo por silo de {@link validarRecepcionDistribucion}: mismos mensajes que
   * `InventarioGestionRecepcionDistribucionCalculos.Resolver` con `porSilo = true`.
   */
  private validarRecepcionDistribucionPorSilo(cantidadTransito: number): string | null {
    const filas = this.recepcionDestinos.filter(d => d.siloId != null || (Number(d.quantity) || 0) !== 0);
    if (filas.length === 0) return 'Agregue al menos un silo a la distribución.';

    const vistos = new Set<number>();
    for (const fila of filas) {
      if (fila.siloId == null) return 'Cada destino de la distribución debe indicar el silo o la bodega.';
      if (!(Number(fila.quantity) > 0)) return 'Las cantidades de la distribución deben ser mayores a cero.';
      if (vistos.has(fila.siloId)) return `No repita el mismo silo en la distribución (silo ${fila.siloId}).`;
      vistos.add(fila.siloId);
    }

    const suma = this.recepcionDistribuido();
    if (Math.abs(suma - cantidadTransito) > this.recepcionToleranciaSuma) {
      return `La suma de la distribución (${suma}) debe ser igual a la cantidad en tránsito (${cantidadTransito}).`;
    }
    return null;
  }

  /** Con inventario por silo el reparto es SIEMPRE por silo, sea alimento o insumo. */
  recepcionNeedsSilo(): boolean {
    return this.manejaPorSilo;
  }

  recepcionNeedsNucleoGalpon(itemId: number): boolean {
    // Inventario por silo: la ubicación es el silo; núcleo/galpón no se piden ni se guardan.
    if (this.manejaPorSilo) return false;
    // Colombia: recepción a nivel granja → sin núcleo/galpón.
    if (this.isColombiaInventario) return false;
    const item = this.allCatalogItems.find(i => i.id === itemId);
    if (!item) return false;
    return this.isAlimentoConcept(item.concepto ?? item.tipoItem);
  }

  submitRecepcionForm(): void {
    const row = this.recepcionPendiente;
    if (!row) return;
    const needNg = this.recepcionNeedsNucleoGalpon(row.itemInventarioEcuadorId);
    const needSilo = this.recepcionNeedsSilo();
    const distribuye = (needNg || needSilo) && this.recepcionDistribuir;

    if (needSilo && !distribuye && this.recepcionToSiloId == null) {
      this.openAlertModal('error', 'Validación', 'Debe indicar el silo o la bodega de recepción en la granja destino.');
      return;
    }

    if (distribuye) {
      const error = this.validarRecepcionDistribucion(row.quantity);
      if (error) {
        this.openAlertModal('error', 'Validación', error);
        return;
      }
    } else {
      if (needNg && (!this.recepcionToNucleoId || !this.recepcionToGalponId)) {
        this.openAlertModal('error', 'Validación', 'Para alimento indique Núcleo y Galpón de recepción en la granja destino.');
        return;
      }
      if (!needNg && !needSilo && (this.recepcionToNucleoId || this.recepcionToGalponId)) {
        this.openAlertModal('error', 'Validación', 'Para ítems no alimento la recepción es solo a nivel granja (sin Núcleo/Galpón).');
        return;
      }
    }

    const detalle =
      row.pendienteDespachoOrigen === true
        ? ' (Solicitud antigua) Se descontará origen si aún no se hizo y se sumará en destino.'
        : ' El origen ya fue descontado al enviar el traslado; solo se sumará el stock en destino.';
    const unidadReparto = needSilo ? 'silo(s)' : 'galpón(es)';
    const reparto = distribuye
      ? ` Se repartirá entre ${this.recepcionDestinos.length} ${unidadReparto}.`
      : '';
    this.openConfirmModal(
      'Confirmar recepción',
      `¿Registrar ingreso en ${row.toGranjaNombre ?? 'granja destino'} por la cantidad indicada?${reparto}${detalle}`,
      'recepcionTransito'
    );
  }

  private doRecepcionTransito(): void {
    const row = this.recepcionPendiente;
    if (!row?.transferGroupId) return;
    const needNg = this.recepcionNeedsNucleoGalpon(row.itemInventarioEcuadorId);
    const needSilo = this.recepcionNeedsSilo();
    const distribuye = (needNg || needSilo) && this.recepcionDistribuir;
    this.submittingRecepcion = true;
    this.svc
      .registrarRecepcionTransito({
        transferGroupId: row.transferGroupId,
        toFarmId: row.toFarmId,
        toNucleoId: distribuye ? null : needNg ? this.recepcionToNucleoId : null,
        toGalponId: distribuye ? null : needNg ? this.recepcionToGalponId : null,
        toSiloId: distribuye || !needSilo ? null : this.recepcionToSiloId,
        distribucion: distribuye
          ? this.recepcionDestinos.map(d => ({
              nucleoId: needSilo ? null : d.nucleoId,
              galponId: needSilo ? null : d.galponId,
              quantity: Number(d.quantity) || 0,
              siloId: needSilo ? d.siloId : null
            }))
          : null
      })
      .subscribe({
        next: () => {
          this.submittingRecepcion = false;
          const destinos = this.recepcionDestinos.length;
          const unidad = needSilo ? 'silo(s)' : 'galpón(es)';
          this.cancelRecepcion();
          this.openAlertModal(
            'success',
            'Listo',
            distribuye
              ? `Recepción registrada y distribuida en ${destinos} ${unidad}. El inventario ya figura en destino.`
              : 'Recepción registrada. El inventario ya figura en destino.'
          );
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
    if (!this.filterData) return GestionInventarioPageComponent._emptyList as never[];
    const computed = this.filterData.nucleosDestino
      .filter(n => n.granjaId === farmId)
      .map(n => ({ nucleoId: n.nucleoId, nucleoNombre: n.nucleoNombre }));
    return this.listaEstable(`recN|${farmId}`, computed, x => x.nucleoId);
  }

  recepcionGalponesForFarm(farmId: number): { galponId: string; galponNombre: string }[] {
    if (!this.filterData) return GestionInventarioPageComponent._emptyList as never[];
    const computed = this.filterData.galponesDestino
      .filter(g => g.granjaId === farmId && (!this.recepcionToNucleoId || g.nucleoId === this.recepcionToNucleoId))
      .map(g => ({ galponId: g.galponId, galponNombre: g.galponNombre }));
    return this.listaEstable(`recG|${farmId}|${this.recepcionToNucleoId ?? ''}`, computed, x => x.galponId);
  }

  onRecepcionNucleoChange(): void {
    this.recepcionToGalponId = null;
  }

  private doTraslado(): void {
    this.submittingTraslado = true;
    const inter = this.trasladoModo === 'interGranja';
    let toNucleo: string | null = null;
    let toGalpon: string | null = null;
    // Modo por silo: núcleo/galpón viajan en null porque el backend los anula igual; mandarlos
    // solo agregaría ruido al request y confundiría al próximo lector.
    if (this.showNucleoGalpon && !this.manejaPorSilo) {
      if (inter) {
        toNucleo = this.toNucleoId?.trim() ? this.toNucleoId : null;
        toGalpon = this.toGalponId?.trim() ? this.toGalponId : null;
      } else {
        toNucleo = this.toNucleoId;
        toGalpon = this.toGalponId;
      }
    }
    this.enviarTraslado({
      fromFarmId: this.fromFarmId!,
      fromNucleoId: this.showNucleoGalpon && !this.manejaPorSilo ? this.fromNucleoId : null,
      fromGalponId: this.showNucleoGalpon && !this.manejaPorSilo ? this.fromGalponId : null,
      toFarmId: this.toFarmId!,
      toNucleoId: toNucleo,
      toGalponId: toGalpon,
      itemInventarioEcuadorId: this.trasladoItemInventarioEcuadorId!,
      quantity: this.trasladoQuantity,
      unit: this.trasladoSelectedUnit === '—' ? 'kg' : this.trasladoSelectedUnit,
      reference: this.trasladoReference || null,
      reason: this.trasladoReason || null,
      destinoTipo: this.trasladoDestinoTipo,
      fechaMovimiento: this.trasladoFechaMovimiento?.trim() || null,
      fromSiloId: this.manejaPorSilo ? this.fromSiloId : null,
      toSiloId: this.manejaPorSilo ? this.toSiloId : null
    });
  }

  /**
   * Envía el traslado y, si el backend avisa que la salida deja un día en rojo, lo pregunta y lo
   * reenvía confirmado.
   *
   * 🔴 Existe porque el stock y la tabla diaria NO son lo mismo: el stock se valida atómicamente
   * porque es físico y vive en el instante del guardado, pero la tabla se ordena por FECHA DECLARADA.
   * Una salida fechada hacia atrás pasa el control de stock y deja el día negativo igual. Pasó en
   * producción el 18-may-2026 en tres galpones de Ecuador —el ingreso que respaldaba los kilos se
   * cargó minutos antes, fechado ese mismo día, y las salidas se fecharon al 15 y 16— y nadie se
   * enteró hasta que se miró el dato tres meses después.
   *
   * Es un aviso confirmable y no un bloqueo: el ingreso puede entrar después, o la fecha corregirse
   * a continuación. El backend decide, el usuario confirma y se reenvía con la bandera.
   */
  private enviarTraslado(payload: InventarioGestionTrasladoRequest): void {
    this.svc.registrarTraslado(payload).subscribe({
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

        if (err?.status === 409 && err?.error?.diaEnRojo === true) {
          void this.confirmarYReenviarTraslado(payload, err.error?.message);
          return;
        }

        this.openAlertModal('error', 'Error', err.error?.message || 'Error al registrar traslado.');
      }
    });
  }

  private async confirmarYReenviarTraslado(
    payload: InventarioGestionTrasladoRequest, mensaje?: string
  ): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: 'Esta salida deja un día en rojo',
      message: mensaje || 'La tabla diaria del galpón origen quedaría en negativo con esta salida.',
      type: 'warning',
      confirmText: 'Registrarla igual'
    });
    if (!ok) return;

    this.submittingTraslado = true;
    this.enviarTraslado({ ...payload, confirmarDiaEnRojo: true });
  }
}
