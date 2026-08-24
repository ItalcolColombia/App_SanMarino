import { Component, OnInit, ViewChild, inject, ChangeDetectionStrategy } from '@angular/core';
import { DecimalPipe, DatePipe } from '@angular/common';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';

import { HttpClient } from '@angular/common/http';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ToastService } from '../../../../shared/services/toast.service';
import { AvisoValidacionService } from '../../../../shared/services/aviso-validacion.service';
import { ValidacionSeguimientoService } from '../../../../shared/services/validacion-seguimiento.service';
import { UserPermissionService } from '../../../../core/auth/user-permission.service';
import { MENSAJE_GUARDADO_SIN_RED, esRespuestaPendiente } from '../../../../shared/offline/funciones/respuesta-pendiente.funcion';
import { finalize, map, tap } from 'rxjs/operators';

import { GalponService } from '../../../galpon/services/galpon.service';
import { GalponDetailDto } from '../../../galpon/models/galpon.models';

import { LoteService, LoteDto } from '../../../lote/services/lote.service';

import {
  ProduccionService,
  SeguimientoItemDto,
  CrearSeguimientoRequest,
  CrearProduccionLoteRequest,
  ProduccionLoteDetalleDto,
  ExisteProduccionLoteResponse
} from '../../services/produccion.service';

import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { NucleoService, NucleoDto } from '../../services/nucleo.service';
import { FiltroSelectComponent, FilterDataResponse, LotePosturaProduccionFilterItem } from '../filtro-select/filtro-select.component';
import { environment } from '../../../../../environments/environment';
import { TabsPrincipalComponent } from '../tabs-principal/tabs-principal.component';
import { ModalRegistroInicialComponent } from '../modal-registro-inicial/modal-registro-inicial.component';
import { ModalSeguimientoDiarioComponent } from '../modal-seguimiento-diario/modal-seguimiento-diario.component';
import { ModalAnalisisComponent } from '../modal-analisis/modal-analisis.component';
import { ModalDetalleSeguimientoComponent } from '../modal-detalle-seguimiento/modal-detalle-seguimiento.component';
import { ConfirmationModalComponent, ConfirmationModalData } from '../../../../shared/components/confirmation-modal/confirmation-modal.component';
import {
  ModalTrasladoAvesSeguimientoComponent,
  OrigenTrasladoInfo
} from '../../../../features/traslados-aves/components/modal-traslado-aves-seguimiento/modal-traslado-aves-seguimiento.component';
import { TrasladoAvesResultSegDto } from '../../../../features/traslados-aves/services/traslados-aves.service';
import { LotePosturaProduccionService, CierreLoteProduccionResumenDto } from '../../../lote/services/lote-postura-produccion.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { firstValueFrom } from 'rxjs';
import { take } from 'rxjs/operators';

@Component({
  selector: 'app-lote-produccion-list',
  standalone: true,
  imports: [
    DecimalPipe,
    DatePipe,
    FormsModule,
    ReactiveFormsModule,
    FiltroSelectComponent,
    TabsPrincipalComponent,
    ModalRegistroInicialComponent,
    ModalSeguimientoDiarioComponent,
    ModalAnalisisComponent,
    ModalDetalleSeguimientoComponent,
    ConfirmationModalComponent,
    ModalTrasladoAvesSeguimientoComponent
],
  templateUrl: './lote-produccion-list.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./lote-produccion-list.component.scss']
})
export class LoteProduccionListComponent implements OnInit {
  // ================== constantes / sentinelas ==================
  readonly SIN_GALPON = '__SIN_GALPON__';

  readonly filterDataUrl = `${environment.apiUrl}/SeguimientoProduccion/filter-data`;

  // ================== catálogos (otros) ==================
  granjas: FarmDto[] = [];
  nucleos: NucleoDto[] = [];
  galpones: Array<{ id: string; label: string }> = [];
  private filterData: FilterDataResponse | null = null;
  private allNucleos: NucleoDto[] = [];
  private allGalpones: Array<{ galponId: string; galponNombre: string; nucleoId: string; granjaId: number }> = [];

  // ================== selección / filtro ==================
  selectedGranjaId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  selectedLoteId: number | null = null;
  
  // Filtros de fecha
  filtroDesde: string | null = null;
  filtroHasta: string | null = null;

  // ================== datos ==================
  private allLotes: LoteDto[] = [];
  lotes: LoteDto[] = [];
  seguimientos: SeguimientoItemDto[] = [];

  selectedLote: LoteDto | null = null;
  produccionLote: ProduccionLoteDetalleDto | null = null;
  currentProduccionLoteId: number | null = null;
  /** Lote postura producción seleccionado (flujo LPP). Incluye aves, estado. */
  selectedLoteLPP: LotePosturaProduccionFilterItem | null = null;
  /** Información general del lote (desde backend) para el header del tab Seguimiento. */
  informacionLote: import('../../services/produccion.service').InformacionLoteDto | null = null;

  // Datos para el modal de registro inicial
  modalLoteNombre: string = '';
  modalNucleoAsignado: string = '';
  modalNucleosDisponibles: Array<{ nucleoId: string, nucleoNombre: string }> = [];
  /** Aves disponibles para encasetar en producción: desde resumen Levante (saldo) o aves iniciales del lote si no pasó por Levante */
  avesDisponiblesParaRegistro: { saldoHembras: number; saldoMachos: number } | null = null;

  // ================== UI ==================
  loading = false;
  modalRegistroInicialOpen = false;
  @ViewChild(ModalRegistroInicialComponent) modalRegistroInicial?: ModalRegistroInicialComponent;
  
  modalSeguimientoDiarioOpen = false;
  @ViewChild(ModalSeguimientoDiarioComponent) modalSeguimientoDiario?: ModalSeguimientoDiarioComponent;
  analisisOpen = false;
  modalDetalleSeguimientoOpen = false;
  seguimientoIdParaDetalle: number | null = null;
  editingSeguimiento: SeguimientoItemDto | null = null;

  // Traslado de Aves desde Seguimiento Diario (R3)
  trasladoAvesModalOpen = false;
  trasladoAvesOrigen: OrigenTrasladoInfo | null = null;

  /**
   * ID del lote BASE (tabla `lotes`) del lote seleccionado — lo pide el bloque
   * "Edades en el lote" (cohortes). En flujo LPP se resuelve desde el LPP; en el
   * flujo legacy `selectedLoteId` YA es el id base.
   */
  loteIdBaseSeleccionado: number | null = null;
  /** Contador que fuerza recarga del bloque de cohortes tras un traslado. */
  cohortesRefreshTrigger = 0;

  /** Modal de confirmación de eliminación de seguimiento diario */
  showDeleteConfirmModal = false;
  deleteConfirmId: number | null = null;
  deleteConfirmModalData: ConfirmationModalData = {
    title: 'Confirmar eliminación',
    message: '¿Estás seguro de eliminar este registro de seguimiento diario? Esta acción no se puede deshacer.',
    type: 'warning',
    confirmText: 'Eliminar',
    cancelText: 'Cancelar',
    showCancel: true
  };

  // ===== Cierre / reapertura del lote de PRODUCCIÓN =====
  // Cerrar no borra nada: bloquea crear, editar y eliminar seguimiento diario de este lote.
  /** 'cerrar' o 'abrir' según lo que se esté confirmando en el modal. */
  cierreLoteAccion: 'cerrar' | 'abrir' = 'cerrar';
  cierreLoteModalOpen = false;
  motivoCierreLote = '';
  loadingCierreLote = false;
  errorCierreLote: string | null = null;
  resumenCierreLote: CierreLoteProduccionResumenDto | null = null;

  private galponNameById = new Map<string, string>();
  private readonly http = inject(HttpClient);
  private readonly toast = inject(ToastService);
  /** Rechazos que el usuario TIENE que leer van en modal, no en toast. */
  private readonly aviso = inject(AvisoValidacionService);
  /** Doble validación: alerta de registros vencidos al entrar al lote. */
  private readonly validacionSvc = inject(ValidacionSeguimientoService);
  private readonly permSvc = inject(UserPermissionService);
  private readonly auth = inject(AuthService);

  constructor(private confirmDialog: ConfirmDialogService, 
    private farmSvc: FarmService,
    private nucleoSvc: NucleoService,
    private loteSvc: LoteService,
    private produccionSvc: ProduccionService,
    private galponSvc: GalponService,
    private lppSvc: LotePosturaProduccionService
  ) {}

  // ============ Cierre / reapertura del lote de PRODUCCIÓN ============

  /**
   * true si el lote seleccionado está cerrado. Cubre "Cerrada" (género de esta tabla) y "Cerrado",
   * igual que el guard del backend — la UI y la API no pueden discrepar en esto.
   */
  get loteProduccionCerrado(): boolean {
    const estado = (this.selectedLoteLPP?.estadoCierre ?? '').trim();
    return estado.toLowerCase().startsWith('cerrad');
  }

  /** true si se puede capturar/editar/eliminar seguimiento del lote seleccionado. */
  get puedeEditarSeguimiento(): boolean {
    return !!this.selectedLoteId && !this.loteProduccionCerrado;
  }

  /**
   * Corta la acción y avisa si el lote está cerrado. La UI ya oculta los botones; esto cubre el
   * estado desincronizado (otra pestaña cerró el lote) para no mandar un request que el backend va
   * a rechazar igual con un 400.
   */
  private bloqueadoPorLoteCerrado(): boolean {
    if (!this.loteProduccionCerrado) return false;
    this.toast.warning('El lote de producción está cerrado. Reábralo para poder registrar, editar o eliminar información.');
    return true;
  }

  openCierreLoteModal(accion: 'cerrar' | 'abrir'): void {
    const id = this.selectedLoteLPP?.lotePosturaProduccionId;
    if (id == null) return;

    this.cierreLoteAccion = accion;
    this.motivoCierreLote = '';
    this.errorCierreLote = null;
    this.resumenCierreLote = null;
    this.cierreLoteModalOpen = true;

    this.loadingCierreLote = true;
    this.lppSvc.getResumenCierre(id)
      .pipe(finalize(() => (this.loadingCierreLote = false)))
      .subscribe({
        next: r => (this.resumenCierreLote = r),
        error: err => {
          this.errorCierreLote = err?.error?.message ?? err?.message ?? 'No se pudo cargar el resumen del lote.';
        }
      });
  }

  closeCierreLoteModal(): void {
    this.cierreLoteModalOpen = false;
    this.motivoCierreLote = '';
    this.errorCierreLote = null;
    this.resumenCierreLote = null;
    this.loadingCierreLote = false;
  }

  async confirmarCierreLote(): Promise<void> {
    const id = this.selectedLoteLPP?.lotePosturaProduccionId;
    if (id == null) return;

    const motivo = (this.motivoCierreLote || '').trim();
    if (motivo.length < 3) {
      this.errorCierreLote = 'Indique el motivo (mínimo 3 caracteres).';
      return;
    }

    const sesion = await firstValueFrom(this.auth.session$.pipe(take(1)));
    const u = sesion?.user;
    const userId = (u?.id && String(u.id)) || (u?.userId != null ? String(u.userId) : '') || '';
    if (!userId) {
      this.errorCierreLote = 'No hay usuario en sesión.';
      return;
    }

    const cerrando = this.cierreLoteAccion === 'cerrar';
    const peticion = cerrando
      ? this.lppSvc.cerrarLote(id, { motivo, closedByUserId: userId })
      : this.lppSvc.abrirLote(id, { motivo, openedByUserId: userId });

    this.loadingCierreLote = true;
    this.errorCierreLote = null;
    peticion
      .pipe(finalize(() => (this.loadingCierreLote = false)))
      .subscribe({
        next: lpp => {
          // Se refleja el estado nuevo en el filtro sin recargar la pantalla entera: de él dependen
          // los botones de crear/editar/eliminar.
          if (this.selectedLoteLPP) {
            this.selectedLoteLPP = { ...this.selectedLoteLPP, estadoCierre: lpp?.estadoCierre ?? (cerrando ? 'Cerrada' : 'Abierta') };
          }
          this.closeCierreLoteModal();
          this.toast.success(cerrando ? 'Lote de producción cerrado.' : 'Lote de producción reabierto.');
        },
        error: err => {
          this.errorCierreLote =
            err?.error?.message ?? err?.message ??
            (cerrando ? 'No se pudo cerrar el lote.' : 'No se pudo abrir el lote.');
        }
      });
  }

  // ================== INIT ==================
  ngOnInit(): void {
    // Granjas, núcleos, galpones y lotes (producción) se cargan vía filter-data en FiltroSelect.
  }

  onFilterDataLoaded(data: FilterDataResponse): void {
    this.filterData = data;
    this.granjas = data.farms ?? [];
    this.allNucleos = data.nucleos ?? [];
    this.allGalpones = data.galpones ?? [];
    this.allLotes = (data.lotes ?? []).map((l: any) => {
      const lppId = l.lotePosturaProduccionId ?? l.loteId;
      return {
        loteId: lppId,
        loteNombre: l.loteNombre,
        granjaId: l.granjaId,
        nucleoId: l.nucleoId ?? undefined,
        galponId: l.galponId ?? undefined
      };
    }) as LoteDto[];
    (data.galpones ?? []).forEach(g => {
      if (g.galponId) this.galponNameById.set(String(g.galponId).trim(), (g.galponNombre || g.galponId).trim());
    });
  }

  // ================== CARGA GALPONES ==================
  private loadGalponCatalog(): void {
    this.galponNameById.clear();
    if (!this.selectedGranjaId) return;

    if (this.selectedNucleoId) {
      this.galponSvc.getByGranjaAndNucleo(this.selectedGranjaId, this.selectedNucleoId).subscribe({
        next: rows => this.fillGalponMap(rows),
        error: () => this.galponNameById.clear(),
      });
      return;
    }

    this.galponSvc.search({ granjaId: this.selectedGranjaId, page: 1, pageSize: 1000, soloActivos: true })
      .subscribe({
        next: res => this.fillGalponMap(res?.items || []),
        error: () => this.galponNameById.clear(),
      });
  }

  private fillGalponMap(rows: GalponDetailDto[] | null | undefined): void {
    for (const g of rows || []) {
      const id = String(g.galponId).trim();
      if (!id) continue;
      this.galponNameById.set(id, (g.galponNombre || id).trim());
    }
    this.buildGalponesFromLotes();
  }

  // ================== CASCADA DE FILTROS ==================
  onGranjaChange(granjaId: number | null): void {
    this.selectedGranjaId = granjaId;
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.seguimientos = [];
    this.galpones = [];
    this.lotes = [];
    this.selectedLote = null;
    this.produccionLote = null;
    this.currentProduccionLoteId = null;
    this.nucleos = [];

    if (!this.selectedGranjaId) return;

    if (this.filterData) {
      const gid = Number(this.selectedGranjaId);
      this.nucleos = this.allNucleos.filter(n => n.granjaId === gid);
      this.applyFiltersToLotes();
      this.buildGalponesFromFilterData();
      return;
    }

    this.nucleoSvc.getByGranja(this.selectedGranjaId).subscribe({
      next: rows => (this.nucleos = rows || []),
      error: () => (this.nucleos = [])
    });

    this.reloadLotesThenApplyFilters();
    this.loadGalponCatalog();
  }

  onNucleoChange(nucleoId: string | null): void {
    this.selectedNucleoId = nucleoId;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    this.seguimientos = [];
    this.selectedLote = null;
    this.produccionLote = null;
    this.currentProduccionLoteId = null;

    if (this.filterData) {
      this.applyFiltersToLotes();
      this.buildGalponesFromFilterData();
      return;
    }

    this.applyFiltersToLotes();
    this.loadGalponCatalog();
  }

  onGalponChange(galponId: string | null): void {
    this.selectedGalponId = galponId;
    this.selectedLoteId = null;
    this.seguimientos = [];
    this.selectedLote = null;
    this.produccionLote = null;
    this.currentProduccionLoteId = null;
    this.applyFiltersToLotes();
  }

  onLoteChange(loteId: number | null): void {
    this.selectedLoteId = loteId;
    this.seguimientos = [];
    this.selectedLote = null;
    this.produccionLote = null;
    this.currentProduccionLoteId = null;
    this.selectedLoteLPP = null;
    this.informacionLote = null;
    this.loteIdBaseSeleccionado = null;
    this.filtroDesde = null;
    this.filtroHasta = null;

    if (!this.selectedLoteId) return;

    // Alarma al entrar al lote: si hay registros vencidos sin validar hay que verlo ANTES de
    // ponerse a cargar el día siguiente, porque además el backend va a rechazar ese alta.
    this.avisarPendientesDeValidacion(this.selectedLoteId);

    this.loading = true;

    const lppFromFilter = this.filterData?.lotes?.find((l: any) =>
      (l.lotePosturaProduccionId ?? l.loteId) === this.selectedLoteId
    ) as LotePosturaProduccionFilterItem | undefined;

    if (lppFromFilter) {
      this.selectedLoteLPP = lppFromFilter;
      this.resolverLoteIdBaseLPP(lppFromFilter.lotePosturaProduccionId);
      this.selectedLote = {
        loteId: lppFromFilter.lotePosturaProduccionId,
        loteNombre: lppFromFilter.loteNombre,
        granjaId: lppFromFilter.granjaId,
        nucleoId: lppFromFilter.nucleoId ?? undefined,
        galponId: lppFromFilter.galponId ?? undefined
      } as LoteDto;
      this.currentProduccionLoteId = 0;
      this.loadInformacionLoteLPP();
      this.loadSeguimientos();
      return;
    }

    // Flujo legacy (no LPP): el id seleccionado YA es el del lote base.
    this.loteIdBaseSeleccionado = this.selectedLoteId;

    this.loteSvc.getById(this.selectedLoteId).subscribe({
      next: l => (this.selectedLote = l || null),
      error: () => (this.selectedLote = null)
    });

    this.produccionSvc.existsProduccionLote(this.selectedLoteId).subscribe({
      next: (response: ExisteProduccionLoteResponse) => {
        this.currentProduccionLoteId = response.produccionLoteId || null;

        if (response.exists && this.currentProduccionLoteId) {
          this.produccionSvc.getProduccionLote(this.selectedLoteId!).subscribe({
            next: (detalle) => {
              this.produccionLote = detalle;
              this.loadSeguimientos();
            },
            error: () => {
              this.produccionLote = null;
              this.loading = false;
            }
          });
        } else {
          this.produccionLote = null;
          this.loading = false;
        }
      },
      error: () => {
        this.produccionLote = null;
        this.loading = false;
      }
    });
  }

  private loadSeguimientos(): void {
    if (!this.selectedLoteId) return;

    const query = this.selectedLoteLPP
      ? { lotePosturaProduccionId: this.selectedLoteId, desde: this.filtroDesde || undefined, hasta: this.filtroHasta || undefined, page: 1, size: 0 }
      : { loteId: this.selectedLoteId, desde: this.filtroDesde || undefined, hasta: this.filtroHasta || undefined, page: 1, size: 0 };

    this.produccionSvc.listarSeguimiento(query).pipe(finalize(() => (this.loading = false)))
    .subscribe({
      next: response => (this.seguimientos = response.items || []),
      error: () => (this.seguimientos = [])
    });
  }

  /** Refresca la Información General del lote (aves actuales, movimiento de aves) tras crear/editar/eliminar seguimiento. */
  refreshLoteInfo(): void {
    if (!this.selectedLoteId) return;

    if (this.selectedLoteLPP) {
      this.loadInformacionLoteLPP();
      return;
    }

    this.produccionSvc.getProduccionLote(this.selectedLoteId).subscribe({
      next: (detalle) => (this.produccionLote = detalle),
      error: () => {}
    });
  }

  /**
   * Resuelve el ID del lote BASE (tabla `lotes`) de un LPP. El filter-data sólo trae
   * `lotePosturaProduccionId`; el id base vive en el LPP completo.
   *
   * Alimenta DOS cosas con el mismo id: el bloque de cohortes (`loteIdBaseSeleccionado`) y
   * `selectedLote.loteId`, que hasta acá se poblaba con `lotePosturaProduccionId` — el id de OTRA
   * tabla. `[loteId]` del modal de seguimiento diario usa ese campo para pedir los silos asignados
   * al lote (`lote_silos.lote_id`, que referencia el lote BASE, no el LPP): con el id equivocado la
   * consulta no encontraba nada y el consumo de silo aparecía sin opciones en producción, aunque el
   * mismo lote sí las mostrara desde levante (que ya usaba el id correcto). Reasignar
   * `selectedLote` a un objeto nuevo dispara `ngOnChanges` en el modal (el componente es Eager) y
   * recarga los silos con el id ya corregido.
   */
  private resolverLoteIdBaseLPP(lotePosturaProduccionId: number): void {
    this.lppSvc.getById(lotePosturaProduccionId).subscribe({
      next: lpp => {
        // Si el usuario ya cambió de lote, no pisar el valor nuevo.
        if (this.selectedLoteLPP?.lotePosturaProduccionId !== lotePosturaProduccionId) return;
        const loteIdBase = lpp?.loteId ?? null;
        this.loteIdBaseSeleccionado = loteIdBase;
        if (loteIdBase != null && this.selectedLote) {
          this.selectedLote = { ...this.selectedLote, loteId: loteIdBase };
        }
      },
      error: () => {
        if (this.selectedLoteLPP?.lotePosturaProduccionId !== lotePosturaProduccionId) return;
        this.loteIdBaseSeleccionado = null;
      }
    });
  }

  private loadInformacionLoteLPP(): void {
    if (!this.selectedLoteId) return;
    // Solo para flujo LPP
    this.produccionSvc.obtenerInformacionLote(this.selectedLoteId).subscribe({
      next: (res) => (this.informacionLote = res?.informacionLote || null),
      error: () => (this.informacionLote = null)
    });
  }
  
  // ================== FILTROS DE FECHA ==================
  onFiltroFechaChange(): void {
    if (this.selectedLoteId) {
      this.loadSeguimientos();
    }
  }
  
  limpiarFiltrosFecha(): void {
    this.filtroDesde = null;
    this.filtroHasta = null;
    if (this.selectedLoteId) {
      this.loadSeguimientos();
    }
  }

  // ================== CARGA Y FILTRADO ==================
  private reloadLotesThenApplyFilters(): void {
    // Usar el nuevo endpoint que filtra lotes con semana >= 26
    this.loading = true;
    this.produccionSvc.obtenerLotesProduccion()
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (lotes) => {
          this.allLotes = lotes || [];
          this.applyFiltersToLotes();
          this.buildGalponesFromLotes();
        },
        error: (err) => {
          console.error('Error al cargar lotes de producción:', err);
          this.allLotes = [];
          this.applyFiltersToLotes();
          this.buildGalponesFromLotes();
        }
      });
  }

  private applyFiltersToLotes(): void {
    if (!this.selectedGranjaId) { this.lotes = []; return; }
    const gid = String(this.selectedGranjaId);

    let filtered = this.allLotes.filter(l => String(l.granjaId) === gid);

    if (this.selectedNucleoId) {
      const nid = String(this.selectedNucleoId);
      filtered = filtered.filter(l => String(l.nucleoId) === nid);
    }

    if (!this.selectedGalponId) { this.lotes = filtered; return; }

    if (this.selectedGalponId === this.SIN_GALPON) {
      this.lotes = filtered.filter(l => !this.hasValue(l.galponId));
      return;
    }

    const sel = this.normalizeId(this.selectedGalponId);
    this.lotes = filtered.filter(l => this.normalizeId(l.galponId) === sel);
  }

  private buildGalponesFromFilterData(): void {
    if (!this.selectedGranjaId) {
      this.galpones = [];
      return;
    }
    const gid = Number(this.selectedGranjaId);
    const nid = this.selectedNucleoId ? String(this.selectedNucleoId) : null;
    const list = this.allGalpones.filter(g => g.granjaId === gid && (!nid || g.nucleoId === nid));
    const result: Array<{ id: string; label: string }> = list.map(g => ({
      id: String(g.galponId).trim(),
      label: (g.galponNombre || g.galponId).trim()
    }));
    this.galpones = result.sort((a, b) =>
      a.label.localeCompare(b.label, 'es', { numeric: true, sensitivity: 'base' })
    );
  }

  private buildGalponesFromLotes(): void {
    if (!this.selectedGranjaId) {
      this.galpones = [];
      return;
    }

    const gid = String(this.selectedGranjaId);
    let base = this.allLotes.filter(l => String(l.granjaId) === gid);

    if (this.selectedNucleoId) {
      const nid = String(this.selectedNucleoId);
      base = base.filter(l => String(l.nucleoId) === nid);
    }

    const seen = new Set<string>();
    const result: Array<{ id: string; label: string }> = [];

    for (const l of base) {
      const id = this.normalizeId(l.galponId);
      if (!id) continue;
      if (seen.has(id)) continue;
      seen.add(id);
      const label = this.galponNameById.get(id) || id;
      result.push({ id, label });
    }

    this.galpones = result.sort((a, b) =>
      a.label.localeCompare(b.label, 'es', { numeric: true, sensitivity: 'base' })
    );
  }

  // ================== CRUD modal ==================
  create(): void {
    if (!this.selectedLoteId) return;
    if (this.bloqueadoPorLoteCerrado()) return;

    if (this.selectedLoteLPP) {
      this.editingSeguimiento = null;
      this.modalSeguimientoDiarioOpen = true;
      return;
    }

    this.produccionSvc.existsProduccionLote(this.selectedLoteId).subscribe({
      next: (response: ExisteProduccionLoteResponse) => {
        if (response.exists && response.produccionLoteId) {
          this.currentProduccionLoteId = response.produccionLoteId;
          this.editingSeguimiento = null;
          this.modalSeguimientoDiarioOpen = true;
        } else {
          this.loadLoteDataForModal();
        }
      },
      error: () => this.loadLoteDataForModal()
    });
  }

  // Cargar datos del lote para el modal de registro inicial (incl. aves disponibles desde Levante o lote)
  private loadLoteDataForModal(): void {
    if (!this.selectedLoteId) return;

    // 1. Obtener detalles del lote y resumen de mortalidad (aves disponibles) en paralelo
    this.loteSvc.getById(this.selectedLoteId).subscribe({
      next: (lote: any) => {
        this.modalLoteNombre = lote.loteNombre || '—';
        this.modalNucleoAsignado = lote.nucleo?.nucleoNombre || lote.nucleoId || '';

        const fallbackH = lote.hembrasL ?? 0;
        const fallbackM = lote.machosL ?? 0;

        // 2. Aves disponibles: si el lote pasó por Levante → saldo del resumen; si no → aves iniciales del lote
        this.loteSvc.getResumenMortalidad(this.selectedLoteId!).subscribe({
          next: (resumen) => {
            this.avesDisponiblesParaRegistro = {
              saldoHembras: resumen.saldoHembras ?? fallbackH,
              saldoMachos: resumen.saldoMachos ?? fallbackM
            };
            this.openRegistroInicialModalWithNucleos(lote.granjaId);
          },
          error: () => {
            this.avesDisponiblesParaRegistro = { saldoHembras: fallbackH, saldoMachos: fallbackM };
            this.openRegistroInicialModalWithNucleos(lote.granjaId);
          }
        });
      },
      error: () => {
        this.modalLoteNombre = '';
        this.modalNucleoAsignado = '';
        this.modalNucleosDisponibles = [];
        this.avesDisponiblesParaRegistro = null;
        this.modalRegistroInicialOpen = true;
      }
    });
  }

  private openRegistroInicialModalWithNucleos(granjaId: number | undefined): void {
    if (granjaId) {
      this.nucleoSvc.getByGranja(granjaId).subscribe({
        next: (nucleos: NucleoDto[]) => {
          this.modalNucleosDisponibles = nucleos.map(n => ({
            nucleoId: n.nucleoId,
            nucleoNombre: n.nucleoNombre || n.nucleoId
          }));
          this.modalRegistroInicialOpen = true;
        },
        error: () => {
          this.modalNucleosDisponibles = [];
          this.modalRegistroInicialOpen = true;
        }
      });
    } else {
      this.modalNucleosDisponibles = [];
      this.modalRegistroInicialOpen = true;
    }
  }

  edit(seg: SeguimientoItemDto): void {
    this.editingSeguimiento = seg;
    this.modalSeguimientoDiarioOpen = true;
  }

  async delete(id: number): Promise<void> {
    if (!(await this.confirmDialog.ask({ title: 'Eliminar registro', message: '¿Eliminar este registro?', type: 'warning', confirmText: 'Eliminar' }))) return;
    // TODO: Implementar delete en el servicio
    // this.produccionSvc.delete(id).subscribe(() => this.onLoteChange(this.selectedLoteId));
  }

  // ================== MODALES ==================
  cancelRegistroInicial(): void {
    this.modalRegistroInicialOpen = false;
    this.avesDisponiblesParaRegistro = null;
  }

  onSaveRegistroInicial(request: CrearProduccionLoteRequest): void {
    this.loading = true;
    this.produccionSvc.crearProduccionLote(request)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (produccionLoteId: number) => {
          // Mostrar mensaje de éxito en el modal
          if (this.modalRegistroInicial) {
            this.modalRegistroInicial.showSuccessMessage(produccionLoteId);
          }
        },
        error: (err) => {
          // Mostrar mensaje de error en el modal
          const errorMessage = err?.error?.message || err?.error?.detail || err?.message || 'Error desconocido al crear el registro inicial.';
          if (this.modalRegistroInicial) {
            this.modalRegistroInicial.showErrorMessage(errorMessage);
          }
          console.error('Error al crear registro inicial:', err);
        }
      });
  }

  onRegistroInicialSuccess(produccionLoteId: number): void {
    this.modalRegistroInicialOpen = false;
    this.currentProduccionLoteId = produccionLoteId;
    this.loadSeguimientos();

    // Abrir modal de seguimiento diario automáticamente después de un breve delay
    setTimeout(() => {
      this.editingSeguimiento = null;
      this.modalSeguimientoDiarioOpen = true;
    }, 500);
  }

  onRegistroInicialError(errorMessage: string): void {
    // El error ya se muestra en el modal, solo logueamos
    console.error('Error en registro inicial:', errorMessage);
  }

  cancelSeguimientoDiario(): void {
    this.modalSeguimientoDiarioOpen = false;
    this.editingSeguimiento = null;
  }

onSaveSeguimientoDiario(request: CrearSeguimientoRequest): void {
    this.loading = true;
    const isUpdate = !!this.editingSeguimiento;
    const id = this.editingSeguimiento?.id;

    // Sin red el interceptor encola la captura y devuelve un 202 sintético. Hay que mirarlo ANTES
    // del `map`, que descarta el cuerpo — y no decir "guardado", porque todavía está en la tablet.
    let capturaPendiente = false;

    const request$ = isUpdate && id != null
      ? this.produccionSvc.actualizarSeguimiento(id, request)
      : this.produccionSvc.crearSeguimiento(request).pipe(
          tap(respuesta => { capturaPendiente = esRespuestaPendiente(respuesta) !== null; }),
          map(() => undefined as void)
        );

    request$
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: () => {
          if (capturaPendiente) {
            this.toast.info(MENSAJE_GUARDADO_SIN_RED);
          } else if (this.modalSeguimientoDiario) {
            this.modalSeguimientoDiario.showSuccessMessage(isUpdate);
          }
          this.editingSeguimiento = null;
          this.modalSeguimientoDiarioOpen = false;
          this.loadSeguimientos();
          this.refreshLoteInfo();
        },
        error: (err) => {
          const errorMessage = err?.error?.message || err?.error?.detail || err?.message || 'Error desconocido al guardar el seguimiento diario.';
          if (this.modalSeguimientoDiario) {
            this.modalSeguimientoDiario.showErrorMessage(errorMessage);
          }
          // Además del mensaje dentro del modal de captura: si el formulario ya se cerró (o el
          // usuario no lo está mirando), el motivo del rechazo tiene que aparecer igual.
          void this.aviso.mensaje('No se pudo guardar el seguimiento', errorMessage);
          console.error('Error al guardar seguimiento diario:', err);
        }
      });
  }

  openAnalisis(): void {
    if (!this.selectedLoteId) return;
    this.analisisOpen = true;
  }

  closeAnalisis(): void {
    this.analisisOpen = false;
  }

  // ================== helpers ==================
  trackById = (_: number, r: SeguimientoItemDto) => r.id;
  trackByNucleo = (_: number, n: NucleoDto) => n.nucleoId;

  get selectedLoteNombre(): string {
    const l = this.lotes.find(x => x.loteId === this.selectedLoteId);
    return l?.loteNombre ?? (this.selectedLoteId?.toString() || '—');
  }

  get selectedGranjaName(): string {
    const g = this.granjas.find(x => x.id === this.selectedGranjaId);
    return g?.name ?? '';
  }

  get selectedNucleoNombre(): string {
    const n = this.nucleos.find(x => x.nucleoId === this.selectedNucleoId);
    return n?.nucleoNombre ?? '';
  }

  get selectedGalponNombre(): string {
    if (this.selectedGalponId === this.SIN_GALPON) return '— Sin galpón —';
    const id = (this.selectedGalponId ?? '').trim();
    return this.galponNameById.get(id) || id;
  }

  /** Edad (en días) a HOY desde fecha de inicio (mínimo 1). */
  calcularEdadDias(fechaInicio?: string | Date | null): number {
    const inicioYmd = this.toYMD(fechaInicio);
    const inicio = this.ymdToLocalNoonDate(inicioYmd);
    if (!inicio) return 0;
    const MS_DAY = 24 * 60 * 60 * 1000;
    const now = this.ymdToLocalNoonDate(this.todayYMD())!;
    return Math.max(1, Math.floor((now.getTime() - inicio.getTime()) / MS_DAY) + 1);
  }

  /** 
   * Calcula la edad en días desde la fecha de encasetamiento del lote (fechaEncaset).
   * Siempre usa la fecha de creación/encasetamiento del lote, no la de ProduccionLote.
   */
  calcularEdadDiasDesdeEncasetamiento(): number {
    if (!this.selectedLote?.fechaEncaset) return 0;
    return this.calcularEdadDias(this.selectedLote.fechaEncaset);
  }

  private hasValue(v: unknown): boolean {
    if (v === null || v === undefined) return false;
    const s = String(v).trim().toLowerCase();
    return !(s === '' || s === '0' || s === 'null' || s === 'undefined');
  }

  private normalizeId(v: unknown): string {
    if (v === null || v === undefined) return '';
    return String(v).trim();
  }

  // ================== HELPERS DE FECHA ==================
  private todayYMD(): string {
    const d = new Date();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');
    return `${d.getFullYear()}-${mm}-${dd}`;
  }

  private toYMD(input: string | Date | null | undefined): string | null {
    if (!input) return null;

    if (input instanceof Date && !isNaN(input.getTime())) {
      const y = input.getFullYear();
      const m = String(input.getMonth() + 1).padStart(2, '0');
      const d = String(input.getDate()).padStart(2, '0');
      return `${y}-${m}-${d}`;
    }

    const s = String(input).trim();

    // YYYY-MM-DD
    const ymd = /^(\d{4})-(\d{2})-(\d{2})$/;
    const m1 = s.match(ymd);
    if (m1) return `${m1[1]}-${m1[2]}-${m1[3]}`;

    // mm/dd/aaaa o dd/mm/aaaa
    const sl = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/;
    const m2 = s.match(sl);
    if (m2) {
      let a = parseInt(m2[1], 10);
      let b = parseInt(m2[2], 10);
      const yyyy = parseInt(m2[3], 10);
      let mm = a, dd = b;
      if (a > 12 && b <= 12) { mm = b; dd = a; }
      const mmS = String(mm).padStart(2, '0');
      const ddS = String(dd).padStart(2, '0');
      return `${yyyy}-${mmS}-${ddS}`;
    }

    // ISO (con T)
    const d = new Date(s);
    if (!isNaN(d.getTime())) {
      const y = d.getFullYear();
      const m = String(d.getMonth() + 1).padStart(2, '0');
      const day = String(d.getDate()).padStart(2, '0');
      return `${y}-${m}-${day}`;
    }

    return null;
  }

  private ymdToLocalNoonDate(ymd: string | null): Date | null {
    if (!ymd) return null;
    const d = new Date(`${ymd}T12:00:00`);
    return isNaN(d.getTime()) ? null : d;
  }

  // ================== MÉTODOS DE MODALES ==================
  openDailyTrackingModal(): void {
    if (!this.selectedLoteId) return;
    if (this.bloqueadoPorLoteCerrado()) return;
    if (this.selectedLoteLPP || this.produccionLote?.id) {
      this.editingSeguimiento = null;
      this.modalSeguimientoDiarioOpen = true;
    }
  }

  /**
   * Al editar, se obtiene el registro completo desde el backend (GET por id)
   * para que el modal muestre todos los campos (id, tipoAlimento, metadata items, etc.).
   */
  editDailyTracking(seguimiento: SeguimientoItemDto): void {
    if (this.bloqueadoPorLoteCerrado()) return;
    this.loading = true;
    this.produccionSvc.obtenerSeguimientoPorId(seguimiento.id)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (fullRecord) => {
          this.editingSeguimiento = fullRecord;
          this.modalSeguimientoDiarioOpen = true;
        },
        error: (err) => {
          console.error('Error al cargar seguimiento para editar:', err);
          const msg = err?.error?.message || err?.message || 'No se pudo cargar el registro. Intente de nuevo.';
          this.toast.error(msg);
        }
      });
  }

  /**
   * Modal rojo con los registros pendientes de validar del lote. Solo aparece si la empresa opera
   * con doble validación y hay al menos uno VENCIDO: avisar por los que todavía están en plazo
   * convertiría la alarma en ruido diario y dejaría de mirarse.
   */
  private avisarPendientesDeValidacion(loteId: number): void {
    this.validacionSvc.pendientes('PRODUCCION', loteId).subscribe(p => {
      this.requiereValidacion = p.requiereValidacion;

      // Se reemplaza el mapa entero en vez de mutarlo: una fila recién validada tiene que
      // DESAPARECER, y limpiar+rellenar deja una ventana con el estado a medias.
      const mapa = new Map<number, string>();
      for (const r of p.registros ?? []) mapa.set(r.seguimientoId, r.estado);
      this.estadoValidacionPorId = mapa;

      if (!p.requiereValidacion || p.vencidos <= 0 || !p.mensaje) return;
      void this.aviso.alertaPendientes(p.mensaje);
    });
  }

  openDetailModal(seguimiento: SeguimientoItemDto): void {
    this.seguimientoIdParaDetalle = seguimiento.id;
    this.modalDetalleSeguimientoOpen = true;
  }

  closeDetailModal(): void {
    this.modalDetalleSeguimientoOpen = false;
    this.seguimientoIdParaDetalle = null;
  }

  /** Abre el modal de confirmación para eliminar. Arma mensaje detallado si la fila
   *  tiene traslado (paridad con Levante — Feature 14). */
  deleteDailyTracking(id: number): void {
    if (this.bloqueadoPorLoteCerrado()) return;
    const seg = (this.seguimientos as any[]).find(s => s.id === id);
    this.deleteConfirmId = id;

    const ingH = seg?.trasladoIngresoHembras ?? 0;
    const ingM = seg?.trasladoIngresoMachos  ?? 0;
    const salH = seg?.trasladoSalidaHembras  ?? 0;
    const salM = seg?.trasladoSalidaMachos   ?? 0;
    const tieneTraslado = (ingH + ingM + salH + salM) > 0;
    const tieneManual = (seg?.mortalidadH || 0) + (seg?.mortalidadM || 0)
                      + (seg?.selH || 0) + (seg?.selM || 0)
                      + (seg?.huevosTotales || 0) > 0
                   || (seg?.consKgH || 0) > 0 || (seg?.consKgM || 0) > 0;

    if (!tieneTraslado) {
      const totH = (seg?.mortalidadH || 0) + (seg?.selH || 0);
      const totM = (seg?.mortalidadM || 0) + (seg?.selM || 0);
      this.deleteConfirmModalData = {
        title: '¿Eliminar seguimiento diario?',
        message:
`Vas a eliminar el seguimiento diario del lote "${this.selectedLoteNombre}" con fecha ${seg ? new Date(seg.fechaRegistro).toLocaleDateString('es-CO') : ''}.

Las aves descontadas por mortalidad / selección
(H: ${totH}, M: ${totM}) serán DEVUELTAS al lote.

Esta acción no se puede deshacer.`,
        confirmText: 'Sí, eliminar',
        cancelText: 'Cancelar',
        type: 'warning',
        showCancel: true,
        preformatted: true
      };
      this.showDeleteConfirmModal = true;
      return;
    }

    const esIngreso = (ingH + ingM) > 0;
    const direccion = esIngreso ? 'INGRESO' : 'SALIDA';
    const totalH = esIngreso ? ingH : salH;
    const totalM = esIngreso ? ingM : salM;
    const contraparteId = seg?.trasladoLoteContraparteId ?? null;
    const contraLabel = contraparteId
      ? `LPP #${contraparteId}`
      : 'Lote contraparte';

    const explicacion = esIngreso
      ? `• Este lote (${this.selectedLoteNombre}) DEVOLVERÁ ${totalH} hembras y ${totalM} machos al lote origen.
• ${contraLabel}: RECUPERA ${totalH} hembras y ${totalM} machos.
• El registro contraparte también será borrado (si no tiene datos manuales).`
      : `• ${contraLabel}: PIERDE ${totalH} hembras y ${totalM} machos.
• Este lote (${this.selectedLoteNombre}): RECUPERA ${totalH} hembras y ${totalM} machos.
• El registro contraparte también será borrado (si no tiene datos manuales).`;

    const extraManual = tieneManual
      ? `\n\nAdemás, este registro contiene datos manuales (mortalidad / selección).\nEsas aves serán DEVUELTAS a este lote.`
      : '';

    this.deleteConfirmModalData = {
      title: `⚠️ Eliminar traslado ${direccion}`,
      message:
`Estás eliminando un seguimiento diario que registra un TRASLADO DE ${direccion}:

• Fecha: ${seg ? new Date(seg.fechaRegistro).toLocaleDateString('es-CO') : ''}
• Aves del traslado: ${totalH} hembras / ${totalM} machos
• Contraparte: ${contraLabel}

Si continúas, se revertirá la operación en ambos lotes:
${explicacion}${extraManual}

Para volver a registrar el traslado tendrás que crearlo de nuevo desde el lote origen.

¿Deseas continuar con la eliminación?`,
      confirmText: 'Sí, eliminar y revertir',
      cancelText: 'Cancelar',
      type: 'warning',
      showCancel: true,
      preformatted: true
    };
    this.showDeleteConfirmModal = true;
  }

  /** Cierra el modal de confirmación de eliminación sin eliminar */
  closeDeleteConfirmModal(): void {
    this.showDeleteConfirmModal = false;
    this.deleteConfirmId = null;
  }

  /** Confirma la eliminación (llamado al pulsar "Eliminar" en el modal) */
  confirmDelete(): void {
    const id = this.deleteConfirmId;
    this.closeDeleteConfirmModal();
    if (id == null) return;

    this.loading = true;
    this.produccionSvc.eliminarSeguimiento(id).subscribe({
      next: () => {
        this.loading = false;
        this.onLoteChange(this.selectedLoteId);
        this.refreshLoteInfo();
      },
      error: (err) => {
        this.loading = false;
        console.error('Error al eliminar registro:', err);
        const errorMessage = err?.error?.message || err?.message || 'Error al eliminar el registro. Por favor, intenta nuevamente.';
        void this.aviso.mensaje('No se pudo eliminar el registro', errorMessage);
        this.onLoteChange(this.selectedLoteId);
      }
    });
  }

  exportarAnalisis(): void {
    // TODO: Implementar exportación de análisis
    
  }

  // =========== Traslado de Aves desde Seguimiento Diario (R3) ===========

  openTrasladoAvesModal(): void {
    const lpp = this.selectedLoteLPP;
    if (!lpp) return;

    // Feature 14: hacemos fetch fresh del LPP para obtener avesHActual + loteId
    // del backend, porque selectedLoteLPP viene del filter-data que puede tener
    // valores stale o parciales.
    this.lppSvc.getById(lpp.lotePosturaProduccionId).subscribe({
      next: (lppFresh) => {
        this.trasladoAvesOrigen = {
          loteId: lpp.lotePosturaProduccionId,
          loteIdBase: lppFresh.loteId ?? null,
          tipoLote: 'Produccion',
          loteNombre: lppFresh.loteNombre || lpp.loteNombre,
          avesHActual: lppFresh.avesHActual ?? lpp.avesHActual ?? 0,
          avesMActual: lppFresh.avesMActual ?? lpp.avesMActual ?? 0,
          fechaSeguimiento: this.ultimaFechaSeguimientoOrigen() ?? this.todayYMD()
        };
        this.trasladoAvesModalOpen = true;
      },
      error: () => {
        // Fallback: usar lo que tenemos en selectedLoteLPP
        this.trasladoAvesOrigen = {
          loteId: lpp.lotePosturaProduccionId,
          loteIdBase: null,
          tipoLote: 'Produccion',
          loteNombre: lpp.loteNombre,
          avesHActual: lpp.avesHActual ?? 0,
          avesMActual: lpp.avesMActual ?? 0,
          fechaSeguimiento: this.ultimaFechaSeguimientoOrigen() ?? this.todayYMD()
        };
        this.trasladoAvesModalOpen = true;
      }
    });
  }

  /** REQ-009a: última fecha (YYYY-MM-DD local) registrada en seguimiento diario del lote origen actual.
   * Evita el default "hoy" en UTC (toISOString) que en Colombia cruza de día después de ~19:00. */
  private ultimaFechaSeguimientoOrigen(): string | null {
    let max: string | null = null;
    for (const s of this.seguimientos) {
      const ymd = this.toYMD(s.fechaRegistro);
      if (ymd && (!max || ymd > max)) max = ymd;
    }
    return max;
  }

  onTrasladoAvesCompletado(result: TrasladoAvesResultSegDto): void {
    if (result.exitoso && this.selectedLoteLPP) {
      this.selectedLoteLPP = {
        ...this.selectedLoteLPP,
        avesHActual: result.avesHActualOrigen,
        avesMActual: result.avesMActualOrigen
      };
    }
    this.trasladoAvesModalOpen = false;
    // Fase 3: refrescar el bloque "Edades en el lote" (el lote sigue siendo el mismo).
    this.cohortesRefreshTrigger++;
  }

  // ─── Doble validación ───────────────────────────────────────────────────────

  readonly PERM_VALIDAR = 'seguimiento_produccion.validar';

  /** Flag de la empresa. En false la columna Estado no se muestra y nada cambia. */
  requiereValidacion = false;

  /** seguimientoId → estado. Solo trae los NO validados: lo ausente ya se descontó. */
  estadoValidacionPorId = new Map<number, string>();

  /**
   * Permiso de validar Y empresa en doble validación.
   *
   * El flag hacía falta: sin él, un usuario con el permiso veía el ✓ en una empresa que NO opera con
   * doble validación, donde el registro ya descontó al guardar. Apretarlo no rompe nada en el backend
   * —no hay reservas que aplicar— pero marca el registro como validado y lo deja de solo lectura sin
   * que nadie lo haya pedido. `requiereValidacion` viene de `/pendientes` y es fail-closed: ante error
   * queda en false y el botón no se muestra.
   */
  get puedeValidar(): boolean {
    return this.requiereValidacion && this.permSvc.has(this.PERM_VALIDAR);
  }

  /**
   * Valida un registro: aplica el consumo de alimento y el descuento de aves que estaban separados.
   * Se recarga el lote entero porque el descuento mueve saldos que la tabla ya está mostrando.
   */
  onValidarSeguimiento(seguimientoId: number): void {
    this.loading = true;
    this.validacionSvc.validar('PRODUCCION', seguimientoId).subscribe({
      next: r => {
        this.toast.success(
          `Registro validado. Se aplicaron ${r.kgAplicados ?? 0} kg de alimento y ${r.avesDescontadas ?? 0} aves.`,
          'Validado', 5000);
        this.onLoteChange(this.selectedLoteId);
      },
      error: err => {
        this.loading = false;
        void this.aviso.error(err, 'No se pudo validar el registro.', 'No se pudo validar');
      }
    });
  }
}
