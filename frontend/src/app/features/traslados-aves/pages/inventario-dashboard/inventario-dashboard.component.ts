// src/app/features/traslados/pages/inventario-dashboard/inventario-dashboard.component.ts
import { Component, OnInit, signal, effect, computed, ChangeDetectionStrategy } from '@angular/core';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { firstValueFrom, forkJoin } from 'rxjs';

import {
  calcularTotalAves as calcularTotalAvesFn,
  formatearFecha as formatearFechaFn,
  formatearNumero as formatearNumeroFn,
  normalize as normalizeFn,
  calcularEdadDias as calcularEdadDiasFn,
  toYMD as toYMDFn,
  ymdToIsoNoon as ymdToIsoNoonFn,
  obtenerTipoMovimientoClass as obtenerTipoMovimientoClassFn,
  obtenerEstadoClass as obtenerEstadoClassFn
} from '../../funciones/inventario-dashboard-formato.funcion';
import { puedeAnularMovimientoAves as puedeAnularMovimientoAvesFn } from '../../funciones/inventario-dashboard-movimiento.funcion';
import { fechaTrasladoHistorialLote as fechaTrasladoHistorialLoteFn } from '../../funciones/fecha-traslado-historial-lote.funcion';
import { ModalTrasladoLoteComponent } from '../../../lote/components/modal-traslado-lote/modal-traslado-lote.component';
import { ModalTrasladoHuevosComponent } from '../../../traslados-huevos/components/modal-traslado-huevos/modal-traslado-huevos.component';
import { FiltroSelectComponent } from '../../../lote-produccion/pages/filtro-select/filtro-select.component';

import { LoteDto } from '../../../lote/services/lote.service';
import { InventarioAvesService } from '../../services/inventario-aves.service';
import {
  TrasladosAvesService,
  InventarioAvesDto,
  InventarioAvesSearchRequest,
  ResumenInventarioDto,
  DisponibilidadLoteDto,
  CrearTrasladoAvesDto,
  TrasladoLoteRequest,
  TrasladoLoteResponse,
  HistorialTrasladoLoteDto,
  TrasladoHuevosDto,
} from '../../services/traslados-aves.service';

import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { NucleoService, NucleoDto } from '../../../nucleo/services/nucleo.service';
import { GalponService } from '../../../galpon/services/galpon.service';
import { GalponDetailDto } from '../../../galpon/models/galpon.models';
import { Company, CompanyService } from '../../../../core/services/company/company.service';
// 🔴 Importa el servicio de Lotes
import { LoteService } from '../../../lote/services/lote.service';
import { TrasladoNavigationService, TrasladoUnificado } from '../../../../core/services/traslado-navigation/traslado-navigation.service';
import { SeguimientoLoteLevanteService, CreateSeguimientoLoteLevanteDto } from '../../../lote-levante/services/seguimiento-lote-levante.service';
import { LoteProduccionService, CreateLoteProduccionDto } from '../../../lote-produccion/services/lote-produccion.service';
import { UserPermissionService } from '../../../../core/auth/user-permission.service';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';
import {
  extremosVentanaRegistro,
  hintVentanaFechaRegistro,
  PERMISO_FECHA_RETROACTIVA
} from '../../../../shared/utils/fecha/ventana-fecha-registro.funcion';

@Component({
  selector: 'app-inventario-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, ModalTrasladoLoteComponent, ModalTrasladoHuevosComponent, FiltroSelectComponent],
  templateUrl: './inventario-dashboard.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./inventario-dashboard.component.scss']
})
export class InventarioDashboardComponent implements OnInit {
  // ====== State (signals) ======
  resumen = signal<ResumenInventarioDto | null>(null);

  inventariosBase = signal<InventarioAvesDto[]>([]);
  inventarios = signal<InventarioAvesDto[]>([]);

  loading = signal<boolean>(false);
  error = signal<string | null>(null);
  totalRecords = signal<number>(0);
  currentPage = signal<number>(1);

  filtros: InventarioAvesSearchRequest = {
    soloActivos: true,
    sortBy: 'lote_id',
    sortDesc: false,
    page: 1,
    pageSize: 20
  };

  // Helpers
  hasError   = computed(() => !!this.error());
  isLoading  = computed(() => this.loading());
  totalPages = computed(() => Math.max(1, Math.ceil((this.totalRecords() || 0) / (this.filtros.pageSize || 20))));

  // Catálogos
  farms: FarmDto[] = [];
  nucleos: NucleoDto[] = [];
  galpones: GalponDetailDto[] = [];
  companies: Company[] = [];

  farmMap: Record<number, string> = {};
  nucleoMap: Record<string, string> = {};
  galponMap: Record<string, string> = {};
  private farmById: Record<number, FarmDto> = {};

  // Filtros cascada (sin compañía, solo granja/núcleo/galpón)
  selectedFarmId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;

  // Búsqueda / orden
  filtro = '';
  sortKey: 'edad' | 'fecha' = 'edad';
  sortDir: 'asc' | 'desc' = 'desc';

  // 🔴 Estado para el filtro de lote (fuera del modal)
  selectedLoteId: string | null = null;
  lotesForGalpon = signal<Array<{ id: string; label: string }>>([]); // lista final de lotes para el select
  lotesLoading = signal<boolean>(false);                              // loading del select

  // 🔴 Lotes completos cargados (para filtrado)
  allLotes: LoteDto[] = [];
  lotesDisponibles: LoteDto[] = []; // Lotes filtrados por granja/núcleo/galpón

  // ====== Lote Seleccionado para Detalles ======
  loteSeleccionado = signal<InventarioAvesDto | null>(null);
  loteCompleto = signal<LoteDto | null>(null);
  movimientosLote = signal<TrasladoUnificado[]>([]);
  loadingMovimientos = signal<boolean>(false);

  // ====== Tabs de Histórico ======
  tabHistorialActivo = signal<'lotes' | 'aves' | 'huevos'>('lotes');
  
  // ====== Tabs de Registros (solo cuando hay lote seleccionado) ======
  tabRegistrosActivo = signal<'huevos' | 'aves' | 'lotes'>('huevos');
  historialTrasladosLote = signal<HistorialTrasladoLoteDto[]>([]);
  loadingHistorialLotes = signal<boolean>(false);
  movimientosAvesLote = signal<TrasladoUnificado[]>([]);
  trasladosHuevosLote = signal<TrasladoHuevosDto[]>([]);
  loadingTrasladosHuevos = signal<boolean>(false);

  // ====== Modal Traslado de Lote ======
  modalTrasladoLoteAbierto = signal<boolean>(false);
  procesandoTrasladoLote = signal<boolean>(false);
  tipoTrasladoSeleccionado = signal<'lote' | 'aves' | 'huevos' | null>(null);

  // 🔴 Computed: ¿Hay lote seleccionado completo?
  get tieneLoteSeleccionadoCompleto(): boolean {
    return !!this.selectedLoteId && !!this.loteCompleto();
  }

  /** Empresas sin machos en postura: no se capturan ni se muestran (SR-DEF-1). */
  ocultaMachosEnPostura = false;

  constructor(private confirmDialog: ConfirmDialogService, private toast: ToastService, 
    private trasladosService: TrasladosAvesService,
    private inventarioAvesService: InventarioAvesService,
    private farmService: FarmService,
    private nucleoService: NucleoService,
    private galponService: GalponService,
    private companyService: CompanyService,
    private router: Router,
    private route: ActivatedRoute,
    private fb: FormBuilder,
    // 🔴 Inyecta LoteService
    private loteService: LoteService,
    // 🔴 Inyecta TrasladoNavigationService para movimientos
    private trasladoNavigationService: TrasladoNavigationService,
    // 🔴 Servicios para seguimiento diario
    private seguimientoLevanteService: SeguimientoLoteLevanteService,
    private produccionService: LoteProduccionService,
    private userPermService: UserPermissionService,
    private companyConfig: ActiveCompanyConfigService
  ) {
    this.companyConfig.getFlags().subscribe({
      next: f => (this.ocultaMachosEnPostura = !!f?.ocultaMachosEnPostura),
      error: () => (this.ocultaMachosEnPostura = false)
    });
  }

  /**
   * Ventana de fechas de los tres formularios de este dashboard (traslado de aves entre lotes,
   * retiro/traslado de aves, traslado de huevos): todos escriben movimientos/traslados cargados a
   * mano y comparten la misma regla — mes en curso ∪ últimos 15 días, o sin piso con el permiso.
   */
  private get puedeRetroactivar(): boolean {
    return this.userPermService.has(PERMISO_FECHA_RETROACTIVA);
  }

  get fechaTrasladoMin(): string | null {
    return extremosVentanaRegistro(new Date(), this.puedeRetroactivar).min;
  }

  get fechaTrasladoMax(): string {
    return extremosVentanaRegistro(new Date(), this.puedeRetroactivar).max;
  }

  get fechaTrasladoHint(): string {
    return hintVentanaFechaRegistro(new Date(), this.puedeRetroactivar);
  }

  ngOnInit(): void {
    this.cargarDatosMaestros();
    this.cargarResumen();
    this.cargarInventarios();
    this.cargarTodosLosLotes(); // 🔴 Cargar todos los lotes para filtrado
  }

  // 🔴 Cargar todos los lotes
  private cargarTodosLosLotes(): void {
    this.loteService.getAll().subscribe({
      next: (lotes) => {
        this.allLotes = lotes || [];
        this.aplicarFiltrosALotes();
      },
      error: (err) => {
        console.error('Error al cargar lotes:', err);
        this.allLotes = [];
        this.lotesDisponibles = [];
      }
    });
  }

  // 🔴 Filtrar lotes según granja/núcleo/galpón seleccionado
  private aplicarFiltrosALotes(): void {
    if (!this.selectedFarmId) {
      this.lotesDisponibles = [];
      this.lotesForGalpon.set([]);
      return;
    }

    let filtered = this.allLotes.filter(l => l.granjaId === this.selectedFarmId);

    if (this.selectedNucleoId) {
      filtered = filtered.filter(l => String(l.nucleoId ?? '') === String(this.selectedNucleoId));
    }

    if (this.selectedGalponId) {
      filtered = filtered.filter(l => String(l.galponId ?? '') === String(this.selectedGalponId));
    }

    this.lotesDisponibles = filtered;

    // Actualizar select de lotes
    const mapped = filtered.map(l => ({
      id: String(l.loteId),
      label: l.loteNombre ? `${l.loteNombre} (#${l.loteId})` : `Lote #${l.loteId}`
    }));
    this.lotesForGalpon.set(mapped);

    // Validar que el lote seleccionado siga existiendo
    if (this.selectedLoteId && !mapped.some(l => l.id === this.selectedLoteId)) {
      this.selectedLoteId = null;
      delete this.filtros.loteId;
      this.loteCompleto.set(null);
    }
  }

  // ===================== Cargas API =========================
  async cargarResumen(): Promise<void> {
    try {
      this.error.set(null);
      const r = await firstValueFrom(this.inventarioAvesService.getResumenInventario());
      this.resumen.set(r || null);
    } catch (err: any) {
      console.error('Error al cargar resumen:', err);
      this.error.set(err.message || 'Error al cargar el resumen del inventario');
    }
  }

  async cargarInventarios(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const result = await firstValueFrom(this.inventarioAvesService.searchInventarios(this.filtros));
      if (result) {
        this.inventariosBase.set(result.items || []);
        this.totalRecords.set(result.total || 0);
        this.currentPage.set(result.page || 1);
        this.recomputeList();

        // 🔴 Si hay galpón seleccionado, recargar lotes de ese galpón
        if (this.selectedGalponId) {
          this.cargarLotesParaGalpon(this.selectedGalponId);
        }
      }
    } catch (err: any) {
      console.error('Error al cargar inventarios:', err);
      this.error.set(err.message || 'Error al cargar los inventarios');
      this.inventariosBase.set([]);
      this.inventarios.set([]);
      this.totalRecords.set(0);
      this.currentPage.set(1);
      this.lotesForGalpon.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  private cargarDatosMaestros(): void {
    forkJoin({
      farms: this.farmService.getAll(),
      nucleos: this.nucleoService.getAll(),
      galpones: this.galponService.getAll(),
      companies: this.companyService.getAll()
    }).subscribe(({ farms, nucleos, galpones, companies }) => {
      this.farms = farms || [];
      this.farmById = {};
      this.farmMap = {};
      this.farms.forEach(f => {
        this.farmById[f.id] = f;
        this.farmMap[f.id] = f.name;
      });

      this.nucleos = nucleos || [];
      this.nucleoMap = {};
      this.nucleos.forEach(n => (this.nucleoMap[n.nucleoId] = n.nucleoNombre));

      this.galpones = galpones || [];
      this.galponMap = {};
      this.galpones.forEach(g => (this.galponMap[g.galponId] = g.galponNombre));

      this.companies = companies || [];
    });
  }

  // 🔴 Obtener núcleos por granja (filtrado en cascada)
  private cargarNucleosPorGranja(granjaId: number | null): void {
    if (!granjaId) {
      this.nucleos = [];
      return;
    }

    this.nucleoService.getByGranja(granjaId).subscribe({
      next: (nucleos) => {
        this.nucleos = nucleos || [];
        this.nucleoMap = {};
        this.nucleos.forEach(n => (this.nucleoMap[n.nucleoId] = n.nucleoNombre));
      },
      error: (err) => {
        console.error('Error al cargar núcleos por granja:', err);
        this.nucleos = [];
      }
    });
  }

  // ===================== Paginación/orden (server) =========
  onPageChange(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.filtros.page = page;
    this.cargarInventarios();
  }

  onSortChange(sortBy: string): void {
    if (this.filtros.sortBy === sortBy) {
      this.filtros.sortDesc = !this.filtros.sortDesc;
    } else {
      this.filtros.sortBy = sortBy;
      this.filtros.sortDesc = false;
    }
    this.filtros.page = 1;
    this.cargarInventarios();
  }

  // ===================== Filtros cliente (cascada) =========
  get farmsFiltered(): FarmDto[] {
    return this.farms; // Todas las granjas disponibles (ya filtradas por permisos del usuario)
  }

  get nucleosFiltered(): NucleoDto[] {
    if (this.selectedFarmId != null) return this.nucleos.filter(n => n.granjaId === this.selectedFarmId);
    return this.nucleos;
  }

  get galponesFiltered(): GalponDetailDto[] {
    let arr = this.galpones;
    if (this.selectedFarmId != null) {
      arr = arr.filter(g => g.granjaId === this.selectedFarmId);
    }
    if (this.selectedNucleoId != null) arr = arr.filter(g => g.nucleoId === this.selectedNucleoId);
    return arr;
  }

  onFarmChange(val: number | null): void {
    this.selectedFarmId = val;
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    delete this.filtros.loteId;

    // 🔴 Cargar núcleos de la granja seleccionada
    this.cargarNucleosPorGranja(val);

    // 🔴 Aplicar filtros a lotes (solo por granja ahora)
    this.aplicarFiltrosALotes();

    this.recomputeList();

    // Limpiar selección de lote al cambiar granja
    this.seleccionarLote(null);
    this.loteCompleto.set(null);
  }

  onNucleoChange(val: string | null): void {
    this.selectedNucleoId = val;
    this.selectedGalponId = null;
    this.selectedLoteId = null;
    delete this.filtros.loteId;

    // 🔴 Aplicar filtros a lotes (por granja y núcleo)
    this.aplicarFiltrosALotes();

    this.recomputeList();
    this.loteCompleto.set(null);
  }

  onGalponChange(val: string | null): void {
    this.selectedGalponId = val;
    this.selectedLoteId = null;
    delete this.filtros.loteId;

    // 🔴 Aplicar filtros a lotes (por granja, núcleo y galpón)
    this.aplicarFiltrosALotes();

    this.recomputeList();
    this.loteCompleto.set(null);
  }

  resetFilters(): void {
    this.filtro = '';
    this.selectedFarmId = null;
    this.selectedNucleoId = null;
    this.selectedGalponId = null;

    this.selectedLoteId = null;
    delete this.filtros.loteId;

    this.lotesForGalpon.set([]);
    this.recomputeList();

    // Limpiar selección de lote
    this.seleccionarLote(null);
  }

  // ===================== Orden cliente ======================
  onSortKeyChange(v: 'edad' | 'fecha'): void {
    this.sortKey = v;
    this.recomputeList();
  }
  onSortDirChange(v: 'asc' | 'desc'): void {
    this.sortDir = v;
    this.recomputeList();
  }

  recomputeList(): void {
    const term = this.normalize(this.filtro);
    let res = [...this.inventariosBase()];

    // Cascada (sin compañía)
    if (this.selectedFarmId != null)    res = res.filter(inv => inv.granjaId === this.selectedFarmId);
    if (this.selectedNucleoId != null)  res = res.filter(inv => String(inv.nucleoId ?? '') === String(this.selectedNucleoId ?? ''));
    if (this.selectedGalponId != null)  res = res.filter(inv => String(inv.galponId ?? '') === String(this.selectedGalponId ?? ''));

    // Filtrar por LOTE si viene del select superior
    if (this.filtros.loteId) {
      res = res.filter(inv => String(inv.loteId) === String(this.filtros.loteId));
    }

    // Búsqueda libre
    if (term) {
      res = res.filter(inv => {
        const haystack = [
          inv.loteId ?? 0,
          inv.id ?? 0,
          this.nucleoMap[inv.nucleoId ?? ''] ?? '',
          this.farmMap[inv.granjaId] ?? '',
          this.galponMap[inv.galponId ?? ''] ?? ''
        ].map(s => this.normalize(String(s))).join(' ');
        return haystack.includes(term);
      });
    }

    // Orden en cliente
    res = this.sortInventarios(res);
    this.inventarios.set(res);
  }

  private sortInventarios(arr: InventarioAvesDto[]): InventarioAvesDto[] {
    const val = (inv: InventarioAvesDto): number | null => {
      if (!inv.fechaUltimoConteo) return null;
      if (this.sortKey === 'edad') return this.calcularEdadDias(inv.fechaUltimoConteo);
      const t = new Date(inv.fechaUltimoConteo).getTime();
      return isNaN(t) ? null : t;
    };

    return [...arr].sort((a, b) => {
      const av = val(a);
      const bv = val(b);
      if (av === null && bv === null) return 0;
      if (av === null) return 1;
      if (bv === null) return -1;
      const cmp = av - bv;
      return this.sortDir === 'asc' ? cmp : -cmp;
    });
  }

  // 🔴 Cargar lotes por galpón (robusto)
  private async cargarLotesParaGalpon(galponId: string | null): Promise<void> {
    // limpiar lista y selección si no hay galpón
    if (!galponId) {
      this.lotesForGalpon.set([]);
      this.selectedLoteId = null;
      delete this.filtros.loteId;
      return;
    }

    // 1) Primer intento: derivar de inventariosBase (rápido)
    const fromInventarios = Array.from(
      new Map(
        this.inventariosBase()
          .filter(inv => String(inv.galponId ?? '') === String(galponId))
          .map(inv => [String(inv.loteId), String(inv.loteId)])
      ).entries()
    ).map(([id, label]) => ({ id, label }));

    if (fromInventarios.length > 0) {
      this.lotesForGalpon.set(fromInventarios);
      // Validar que el lote seleccionado siga existiendo
      if (this.selectedLoteId && !fromInventarios.some(l => l.id === this.selectedLoteId)) {
        this.selectedLoteId = null;
        delete this.filtros.loteId;
      }
      return;
    }

    // 2) Segundo intento: pedirlo al backend (LoteService)
    try {
      this.lotesLoading.set(true);
      // Asumo que existe un endpoint tipo: getByGalponId(galponId: string)
      // DESPUÉS
      const lotes: LoteDto[] = await firstValueFrom(this.loteService.getByGalpon(galponId));

      const mapped = (lotes || []).map(l => ({
        id: String(l.loteId),
        label: l.loteNombre ? `${l.loteNombre} (#${l.loteId})` : String(l.loteId)
      }));
      this.lotesForGalpon.set(mapped);

      // Validar selección vigente
      if (this.selectedLoteId && !mapped.some(l => l.id === this.selectedLoteId)) {
        this.selectedLoteId = null;
        delete this.filtros.loteId;
      }
    } catch (e) {
      console.warn('No se pudieron obtener lotes por galpón vía servicio. Detalle:', e);
      this.lotesForGalpon.set([]);
      this.selectedLoteId = null;
      delete this.filtros.loteId;
    } finally {
      this.lotesLoading.set(false);
    }
  }

  // ===================== Utilidades ==========================
  calcularTotalAves(inv: InventarioAvesDto): number { return calcularTotalAvesFn(inv); }

  formatearFecha(fecha: Date | string): string { return formatearFechaFn(fecha); }

  /** Dia del traslado (no el de digitacion), sin corrimiento de zona. */
  fechaTrasladoLote(historial: HistorialTrasladoLoteDto): string { return fechaTrasladoHistorialLoteFn(historial); }

  formatearNumero(n: number): string { return formatearNumeroFn(n); }

  private normalize(s: string): string { return normalizeFn(s); }

  calcularEdadDias(fecha?: string | Date | null): number { return calcularEdadDiasFn(fecha); }

  private toYMD(input: Date | string): string { return toYMDFn(input); }

  private ymdToIsoNoon(ymd: string): string { return ymdToIsoNoonFn(ymd); }

  // TrackBy
  trackByInventarioId(_: number, item: InventarioAvesDto): number { return item.id; }
  trackByGranjaId(_: number, item: any): number { return item.granjaId; }

  async editarInventario(id: number): Promise<void> {
    try {
      await this.router.navigate(['../inventario', id, 'editar'], { relativeTo: this.route });
    } catch (err) {
      console.error('Navegación a edición falló:', err);
      this.toast.error('No se pudo abrir la edición del inventario.');
    }
  }

  async ajustarInventario(loteId: string): Promise<void> {
    if (!loteId) return;
    try {
      const hStr = window.prompt(`Nuevo valor de HEMBRAS para el lote ${loteId} (número entero):`, '0');
      if (hStr === null) return;
      // Empresas sin machos en postura no lo preguntan: el ajuste va con 0 (SR-DEF-1).
      const mStr = this.ocultaMachosEnPostura
        ? '0'
        : window.prompt(`Nuevo valor de MACHOS para el lote ${loteId} (número entero):`, '0');
      if (mStr === null) return;

      const cantidadHembras = Number(hStr);
      const cantidadMachos = Number(mStr);
      if (!Number.isFinite(cantidadHembras) || !Number.isFinite(cantidadMachos) || cantidadHembras < 0 || cantidadMachos < 0) {
        this.toast.warning('Valores inválidos. Deben ser enteros ≥ 0.');
        return;
      }

      const tipoEvento = window.prompt('Tipo de evento:', 'AJUSTE_MANUAL') || 'AJUSTE_MANUAL';
      const observaciones = window.prompt('Observaciones (opcional):', '') || '';

      const ajuste = { cantidadHembras, cantidadMachos, tipoEvento, observaciones };
      await firstValueFrom(this.inventarioAvesService.ajustarInventario(loteId, ajuste));

      await this.cargarResumen();
      await this.cargarInventarios();
      this.toast.success('Ajuste aplicado con éxito.');
    } catch (err: any) {
      console.error('Error al ajustar inventario:', err);
      const msg = err?.message || 'Error al ajustar el inventario';
      this.error.set(msg);
      this.toast.error(msg);
    }
  }

  async verTrazabilidad(loteId: string): Promise<void> {
    try {
      await this.router.navigate(['historial', loteId], { relativeTo: this.route });
    } catch (err) {
      console.error('No se pudo abrir la trazabilidad:', err);
      this.toast.error('No se pudo abrir la trazabilidad del lote.');
    }
  }

  async eliminarInventario(id: number): Promise<void> {
    if (!(await this.confirmDialog.ask({ title: 'Eliminar inventario', message: '¿Está seguro de que desea eliminar este inventario? Esta acción no se puede deshacer.', type: 'warning', confirmText: 'Eliminar' }))) return;

    try {
      await firstValueFrom(this.inventarioAvesService.deleteInventario(id));
      await this.cargarInventarios();
    } catch (err: any) {
      console.error('Error al eliminar inventario:', err);
      const msg = err?.message || 'Error al eliminar el inventario';
      this.error.set(msg);
      this.toast.error(msg);
    }
  }

  obtenerNombreGranja(granjaId: number | null | undefined): string {
    if (granjaId == null) return '—';
    return this.farmMap?.[granjaId] ?? `Granja ${granjaId}`;
  }

  obtenerNombreCompania(companyId: number | null | undefined): string {
    if (companyId == null) return '—';
    const c = this.companies?.find(x => x.id === companyId);
    return c ? c.name : `Compañía ${companyId}`;
  }

  tieneFiltrosAplicados(): boolean {
    return !!(
      this.selectedFarmId ||
      this.selectedNucleoId ||
      this.selectedGalponId ||
      (this.filtro && this.filtro.trim().length > 0) ||
      this.filtros.loteId ||
      this.filtros.granjaId ||
      this.filtros.nucleoId ||
      this.filtros.galponId ||
      this.filtros.estado ||
      this.filtros.fechaDesde ||
      this.filtros.fechaHasta ||
      this.filtros.sortBy ||
      this.filtros.sortDesc ||
      this.filtros.soloActivos === false
    );
  }

  // 🔴 Cambios al seleccionar un lote en el filtro superior
  onLoteSelectChange(val: string | null): void {
    
    
    
    this.selectedLoteId = val;
    if (val) {
      this.filtros.loteId = val;
      // Cargar información completa del lote seleccionado
      const loteIdNum = parseInt(val, 10);
      
      
      if (!isNaN(loteIdNum)) {
        
        this.loteService.getById(loteIdNum).subscribe({
          next: (lote) => {
            
            this.loteCompleto.set(lote);
            
            // Buscar el inventario correspondiente para mostrar detalles
            const inventario = this.inventariosBase().find(inv => String(inv.loteId) === val);
            
            
            if (inventario) {
              // Si encontramos el inventario, usar el método completo
              
              this.seleccionarLote(inventario);
            } else {
              // Si no está en inventarios, crear un inventario "virtual" para mostrar los registros
              
              const inventarioVirtual: InventarioAvesDto = {
                id: 0,
                loteId: val,
                granjaId: lote.granjaId,
                nucleoId: lote.nucleoId || '',
                galponId: lote.galponId || undefined,
                cantidadHembras: 0,
                cantidadMachos: 0,
                fechaUltimoConteo: new Date(),
                createdAt: new Date(),
                updatedAt: undefined,
                companyId: lote.companyId || 0
              };
              
              // Establecer el lote seleccionado y cargar todos los datos
              
              this.loteSeleccionado.set(inventarioVirtual);
              this.tabRegistrosActivo.set('huevos');
              
              // Cargar todos los registros
              
              this.cargarMovimientosLote(loteIdNum);
              this.cargarHistorialTrasladosLote(loteIdNum);
              this.cargarTrasladosHuevosLote(val);
            }
          },
          error: (err) => {
            console.error(`[ERROR] ❌ Error al cargar lote ${loteIdNum}:`, err);
            this.loteCompleto.set(null);
            this.loteSeleccionado.set(null);
            this.movimientosLote.set([]);
            this.trasladosHuevosLote.set([]);
            this.historialTrasladosLote.set([]);
          }
        });
      } else {
        // Si el loteId no es un número válido, limpiar
        console.warn(`[WARN] LoteId inválido: ${val}`);
        this.loteCompleto.set(null);
        this.loteSeleccionado.set(null);
      }
    } else {
      
      delete this.filtros.loteId;
      this.loteCompleto.set(null);
      this.seleccionarLote(null);
    }
    this.recomputeList();
    
  }

  private resetLoteIfNotInContext(): void {
    if (!this.selectedLoteId) return;
    const stillExists = this.lotesForGalpon().some(l => l.id === this.selectedLoteId);
    if (!stillExists) {
      this.selectedLoteId = null;
      delete this.filtros.loteId;
    }
  }

  // ===================== Selección de Lote ====================
  seleccionarLote(inventario: InventarioAvesDto | null): void {
    
    
    
    this.loteSeleccionado.set(inventario);
    

    if (inventario) {
      // Inicializar tab de registros al primer tab con datos disponibles
      // Prioridad: Huevos > Aves > Lotes
      this.tabRegistrosActivo.set('huevos');
      
      
      // Cargar información completa del lote
      const loteIdNum = parseInt(inventario.loteId, 10);
      
      
      if (!isNaN(loteIdNum)) {
        this.loteService.getById(loteIdNum).subscribe({
          next: (lote) => {
            
            this.loteCompleto.set(lote);
          },
          error: (err) => {
            console.error(`[ERROR] Error al cargar lote completo:`, err);
            this.loteCompleto.set(null);
          }
        });

        // Cargar movimientos del lote
        
        this.cargarMovimientosLote(loteIdNum);
        // Cargar historial de traslados de lotes
        this.cargarHistorialTrasladosLote(loteIdNum);
        // Cargar traslados de huevos
        this.cargarTrasladosHuevosLote(String(loteIdNum));
      } else {
        console.warn(`[WARN] LoteId inválido en inventario: ${inventario.loteId}`);
        this.loteCompleto.set(null);
        this.movimientosLote.set([]);
        this.historialTrasladosLote.set([]);
        this.trasladosHuevosLote.set([]);
      }
    } else {
      
      this.loteCompleto.set(null);
      this.movimientosLote.set([]);
      this.historialTrasladosLote.set([]);
      this.trasladosHuevosLote.set([]);
    }
    
  }

  private async cargarMovimientosLote(loteId: number): Promise<void> {
    this.loadingMovimientos.set(true);
    try {
      
      
      
      // Usar el endpoint directo que retorna TODOS los movimientos sin límite
      
      const movimientosAves = await firstValueFrom(
        this.trasladosService.getMovimientosAvesPorLote(loteId)
      );
      
      
      
      // Convertir MovimientoAvesDto[] a TrasladoUnificado[] para mantener compatibilidad
      const movimientosUnificados: TrasladoUnificado[] = (movimientosAves || []).map(m => ({
        id: m.id,
        numeroTraslado: m.numeroMovimiento,
        fechaTraslado: typeof m.fechaMovimiento === 'string' ? m.fechaMovimiento : m.fechaMovimiento.toISOString(),
        tipoOperacion: m.tipoMovimiento,
        tipoTraslado: 'Aves' as const,
        loteIdOrigen: (m.origen?.loteId ?? m.loteOrigenId)?.toString() || '',
        loteIdOrigenInt: m.origen?.loteId ?? m.loteOrigenId ?? undefined,
        granjaOrigenId: m.origen?.granjaId ?? m.granjaOrigenId ?? 0,
        granjaOrigenNombre: (m.origen?.granjaNombre ?? m.granjaOrigenNombre) || undefined,
        loteIdDestino: (m.destino?.loteId ?? m.loteDestinoId)?.toString(),
        loteIdDestinoInt: m.destino?.loteId ?? m.loteDestinoId ?? undefined,
        granjaDestinoId: m.destino?.granjaId ?? m.granjaDestinoId ?? undefined,
        granjaDestinoNombre: (m.destino?.granjaNombre ?? m.granjaDestinoNombre) || undefined,
        cantidadHembras: m.cantidadHembras,
        cantidadMachos: m.cantidadMachos,
        totalAves: m.totalAves ?? (m.cantidadHembras + m.cantidadMachos + (m.cantidadMixtas || 0)),
        estado: m.estado,
        motivo: m.motivoMovimiento || undefined,
        observaciones: m.observaciones || undefined,
        usuarioTrasladoId: m.usuarioMovimientoId,
        usuarioNombre: m.usuarioNombre || undefined,
        fechaProcesamiento: m.fechaProcesamiento ? (typeof m.fechaProcesamiento === 'string' ? m.fechaProcesamiento : m.fechaProcesamiento.toISOString()) : undefined,
        fechaCancelacion: m.fechaCancelacion ? (typeof m.fechaCancelacion === 'string' ? m.fechaCancelacion : m.fechaCancelacion.toISOString()) : undefined,
        createdAt: typeof m.createdAt === 'string' ? m.createdAt : m.createdAt.toISOString(),
        updatedAt: m.fechaProcesamiento ? (typeof m.fechaProcesamiento === 'string' ? m.fechaProcesamiento : m.fechaProcesamiento.toISOString()) : undefined,
        tieneSeguimientoProduccion: false
      }));
      
      
      
      this.movimientosAvesLote.set(movimientosUnificados);
      this.movimientosLote.set(movimientosUnificados);
    } catch (err: any) {
      console.error(`[ERROR] ❌ Error al cargar movimientos del lote ${loteId}:`, err);
      console.error(`[ERROR] Detalles del error:`, {
        message: err.message,
        status: err.status,
        error: err.error,
        url: err.url
      });
      this.movimientosLote.set([]);
      this.movimientosAvesLote.set([]);
    } finally {
      this.loadingMovimientos.set(false);
      
    }
  }

  private async cargarHistorialTrasladosLote(loteId: number): Promise<void> {
    this.loadingHistorialLotes.set(true);
    try {
      
      const historial = await firstValueFrom(
        this.trasladosService.getHistorialTrasladosLote(loteId)
      );
      
      this.historialTrasladosLote.set(historial || []);
    } catch (err: any) {
      console.error('Error al cargar historial de traslados de lotes:', err);
      this.historialTrasladosLote.set([]);
    } finally {
      this.loadingHistorialLotes.set(false);
    }
  }

  private async cargarTrasladosHuevosLote(loteId: string): Promise<void> {
    this.loadingTrasladosHuevos.set(true);
    try {
      
      
      
      // Usar el endpoint directo de traslados de huevos
      
      const traslados = await firstValueFrom(
        this.trasladosService.getTrasladosHuevosPorLote(loteId)
      );
      
      
      
      // Asegurar que las fechas se conviertan correctamente
      const trasladosProcesados: TrasladoHuevosDto[] = (traslados || []).map(t => ({
        ...t,
        fechaTraslado: typeof t.fechaTraslado === 'string' ? new Date(t.fechaTraslado) : t.fechaTraslado,
        fechaProcesamiento: t.fechaProcesamiento ? (typeof t.fechaProcesamiento === 'string' ? new Date(t.fechaProcesamiento) : t.fechaProcesamiento) : undefined,
        fechaCancelacion: t.fechaCancelacion ? (typeof t.fechaCancelacion === 'string' ? new Date(t.fechaCancelacion) : t.fechaCancelacion) : undefined,
        createdAt: typeof t.createdAt === 'string' ? new Date(t.createdAt) : t.createdAt,
        updatedAt: t.updatedAt ? (typeof t.updatedAt === 'string' ? new Date(t.updatedAt) : t.updatedAt) : undefined
      }));
      
      
      
      this.trasladosHuevosLote.set(trasladosProcesados);
      
    } catch (err: any) {
      console.error(`[ERROR] ❌ Error al cargar traslados de huevos para lote ${loteId}:`, err);
      console.error(`[ERROR] Detalles del error:`, {
        message: err.message,
        status: err.status,
        error: err.error,
        url: err.url
      });
      this.trasladosHuevosLote.set([]);
    } finally {
      this.loadingTrasladosHuevos.set(false);
      
    }
  }


  obtenerTipoMovimientoClass(tipo: string): string { return obtenerTipoMovimientoClassFn(tipo); }

  obtenerEstadoClass(estado: string): string { return obtenerEstadoClassFn(estado); }

  // 🔴 Helpers para el modal
  obtenerInventarioLoteSeleccionado(): InventarioAvesDto | null {
    const lote = this.loteCompleto();
    if (!lote) return null;
    return this.inventariosBase().find(inv => String(inv.loteId) === String(lote.loteId)) || null;
  }

  obtenerCantidadHembrasDisponibles(): number {
    const inv = this.obtenerInventarioLoteSeleccionado();
    return inv?.cantidadHembras || 0;
  }

  obtenerCantidadMachosDisponibles(): number {
    const inv = this.obtenerInventarioLoteSeleccionado();
    return inv?.cantidadMachos || 0;
  }

  getCantidadHuevoDisponible(tipoKey: string): number {
    const disponibilidad = this.disponibilidadLote();
    if (!disponibilidad || !disponibilidad.huevos) return 0;

    const keyMap: Record<string, keyof typeof disponibilidad.huevos> = {
      'limpio': 'limpio',
      'tratado': 'tratado',
      'sucio': 'sucio',
      'deforme': 'deforme',
      'blanco': 'blanco',
      'dobleYema': 'dobleYema',
      'piso': 'piso',
      'pequeno': 'pequeno',
      'roto': 'roto',
      'desecho': 'desecho',
      'otro': 'otro'
    };

    const propiedad = keyMap[tipoKey];
    if (!propiedad) return 0;

    const valor = disponibilidad.huevos[propiedad];
    return typeof valor === 'number' ? valor : 0;
  }

  // 🔴 Modal Traslado/Retiro
  modalTrasladoRetiroAbierto = signal<boolean>(false);
  trasladoRetiroForm!: FormGroup;
  disponibilidadLote = signal<DisponibilidadLoteDto | null>(null);
  loadingDisponibilidad = signal<boolean>(false);
  procesandoRetiro = signal<boolean>(false);
  errorRetiro = signal<string | null>(null);
  exitoRetiro = signal<boolean>(false);

  abrirModalTrasladoLote(): void {
    if (!this.tieneLoteSeleccionadoCompleto) return;
    this.tipoTrasladoSeleccionado.set('lote');
    this.modalTrasladoLoteAbierto.set(true);
  }

  cerrarModalTrasladoLote(): void {
    this.modalTrasladoLoteAbierto.set(false);
    this.tipoTrasladoSeleccionado.set(null);
  }

  async procesarTrasladoLote(data: {
    loteId: number;
    granjaDestinoId: number;
    nucleoDestinoId?: string | null;
    galponDestinoId?: string | null;
    observaciones?: string | null;
    fechaTraslado?: string | null;
  }): Promise<void> {
    this.procesandoTrasladoLote.set(true);
    try {
      const dto: TrasladoLoteRequest = {
        loteId: data.loteId,
        granjaDestinoId: data.granjaDestinoId,
        nucleoDestinoId: data.nucleoDestinoId,
        galponDestinoId: data.galponDestinoId,
        observaciones: data.observaciones,
        // El modal la emite desde siempre; por este camino se descartaba y todo traslado quedaba
        // fechado hoy. Null ⇒ el backend usa hoy, como antes de que el campo existiera.
        fechaTraslado: data.fechaTraslado || null
      };

      const response = await firstValueFrom(this.trasladosService.crearTrasladoLote(dto));
      
      if (response.success) {
        // Recargar datos
        await this.cargarInventarios();
        await this.cargarResumen();
        
        // Recargar historial de traslados de lotes
        if (this.loteCompleto()) {
          const loteIdNum = parseInt(String(this.loteCompleto()!.loteId), 10);
          if (!isNaN(loteIdNum)) {
            await this.cargarHistorialTrasladosLote(loteIdNum);
          }
        }

        // Cerrar modal después de un breve delay
        setTimeout(() => {
          this.cerrarModalTrasladoLote();
        }, 2000);
      }
    } catch (err: any) {
      console.error('Error al procesar traslado de lote:', err);
      this.toast.error(err?.message || 'Error al procesar el traslado de lote');
    } finally {
      this.procesandoTrasladoLote.set(false);
    }
  }

  /**
   * Traslado / venta de AVES del lote seleccionado. Los huevos ya no entran por acá: van a
   * `abrirModalTrasladoHuevos()`, que monta el modal del módulo de traslados de huevos.
   */
  abrirModalTrasladoRetiro(): void {
    if (!this.tieneLoteSeleccionadoCompleto) return;

    this.tipoTrasladoSeleccionado.set('aves');
    const lote = this.loteCompleto();
    if (lote) {
      this.cargarDisponibilidadLote(String(lote.loteId));
    }

    this.initTrasladoRetiroForm();
    this.resetDestinoTraslado();
    this.modalTrasladoRetiroAbierto.set(true);
    this.errorRetiro.set(null);
    this.exitoRetiro.set(false);
  }

  cerrarModalTrasladoRetiro(): void {
    this.modalTrasladoRetiroAbierto.set(false);
    this.trasladoRetiroForm.reset();
    this.resetDestinoTraslado();
    this.disponibilidadLote.set(null);
    this.errorRetiro.set(null);
    this.exitoRetiro.set(false);
    this.tipoTrasladoSeleccionado.set(null);
  }

  // ===================== Traslado / venta de HUEVOS =====================
  //
  // El dashboard tenía su PROPIO formulario de huevos (11 columnas legacy, sin selector de ítems):
  // para las empresas con `clasificacion_huevo_por_items` (Santa Reyes) mostraba 0 disponible en las
  // 11 categorías y no dejaba trasladar un solo huevo. Era el 4º lugar con ese bug —los otros 3 se
  // arreglaron en F10— y estaba anotado como pendiente en el tracker. En vez de reimplementar el
  // soporte de ítems por cuarta vez, se monta `ModalTrasladoHuevosComponent`, que ya lo tiene, ya
  // edita y ya es el que usa `/traslados-huevos/lista`. Mismo endpoint (`POST /api/traslados/huevos`).

  /** Modal de huevos (componente del módulo `traslados-huevos`) abierto. */
  modalHuevosAbierto = signal<boolean>(false);
  /** Lote base cuyo traslado de huevos se está registrando. */
  loteHuevosId = signal<number | null>(null);
  /**
   * Espejo de producción del lote. NO es opcional en la práctica: el traslado por ítems
   * (`clasificacion_huevo_por_items`, Santa Reyes) lo EXIGE en el backend
   * (`TrasladoHuevosService`), así que abrir el modal solo con `loteId` dejaría a esas empresas
   * igual de trabadas que con el formulario viejo. Se resuelve desde la disponibilidad del lote,
   * que ya lo trae.
   */
  loteHuevosLppId = signal<number | null>(null);
  /** El modal se abre recién cuando se resolvió el LPP (o se supo que no hay). */
  abriendoModalHuevos = signal<boolean>(false);

  abrirModalTrasladoHuevos(): void {
    if (!this.tieneLoteSeleccionadoCompleto) return;
    const lote = this.loteCompleto();
    if (!lote) return;

    const loteIdNum = Number(lote.loteId);
    if (!Number.isFinite(loteIdNum)) return;

    this.tipoTrasladoSeleccionado.set('huevos');
    this.loteHuevosId.set(loteIdNum);
    this.abriendoModalHuevos.set(true);

    // Fail-open: si la disponibilidad falla se abre igual con el lote base (flujo legacy de 11
    // columnas), que es exactamente lo que hacía el formulario anterior.
    this.trasladosService.getDisponibilidadLote(String(lote.loteId)).subscribe({
      next: (disp) => {
        this.disponibilidadLote.set(disp);
        this.loteHuevosLppId.set(disp?.lotePosturaProduccionId ?? null);
        this.abriendoModalHuevos.set(false);
        this.modalHuevosAbierto.set(true);
      },
      error: () => {
        this.loteHuevosLppId.set(null);
        this.abriendoModalHuevos.set(false);
        this.modalHuevosAbierto.set(true);
      }
    });
  }

  cerrarModalTrasladoHuevos(): void {
    this.modalHuevosAbierto.set(false);
    this.loteHuevosId.set(null);
    this.loteHuevosLppId.set(null);
    this.tipoTrasladoSeleccionado.set(null);
  }

  /** El modal guardó: se refresca lo que el traslado de huevos pudo mover. */
  onTrasladoHuevosGuardado(): void {
    const lote = this.loteCompleto();
    this.cerrarModalTrasladoHuevos();
    if (!lote) return;
    this.cargarTrasladosHuevosLote(String(lote.loteId));
    this.cargarDisponibilidadLote(String(lote.loteId));
  }

  // ===================== Destino del traslado de aves =====================
  //
  // Cascada Granja > Nucleo > Galpon > Lote con `app-filtro-select`, la misma primitiva que ya usa
  // el modal de movimientos-aves. Reemplaza al `<input type="text">` en el que el operario tenia
  // que tipear el id numerico del lote destino.
  //
  // ⚠️ `FiltroSelectComponent` es POLIMORFICO: con `[filterDataUrl]` emite el id de
  // `lote_postura_produccion` (lo que necesita traslados-huevos) y SIN el emite el id de LOTE BASE
  // (`/api/Produccion/lotes-produccion` devuelve `LoteDetailDto`). Aca se usa sin URL a proposito:
  // `CrearTrasladoAvesDto.loteDestinoId` es el lote base.

  selectedGranjaDestinoId: number | null = null;
  selectedNucleoDestinoId: string | null = null;
  selectedGalponDestinoId: string | null = null;
  selectedLoteDestinoId: number | null = null;

  onGranjaDestinoChange(granjaId: number | null): void {
    this.selectedGranjaDestinoId = granjaId;
    this.selectedNucleoDestinoId = null;
    this.selectedGalponDestinoId = null;
    this.selectedLoteDestinoId = null;
    this.trasladoRetiroForm?.patchValue({ granjaDestinoId: granjaId, loteDestinoId: null });
  }

  onNucleoDestinoChange(nucleoId: string | null): void {
    this.selectedNucleoDestinoId = nucleoId;
    this.selectedGalponDestinoId = null;
    this.selectedLoteDestinoId = null;
    this.trasladoRetiroForm?.patchValue({ loteDestinoId: null });
  }

  onGalponDestinoChange(galponId: string | null): void {
    this.selectedGalponDestinoId = galponId;
    this.selectedLoteDestinoId = null;
    this.trasladoRetiroForm?.patchValue({ loteDestinoId: null });
  }

  onLoteDestinoChange(loteId: number | null): void {
    this.selectedLoteDestinoId = loteId;
    this.trasladoRetiroForm?.patchValue({ loteDestinoId: loteId });
  }

  /** Limpia la cascada de destino (al abrir y al cerrar el modal). */
  private resetDestinoTraslado(): void {
    this.selectedGranjaDestinoId = null;
    this.selectedNucleoDestinoId = null;
    this.selectedGalponDestinoId = null;
    this.selectedLoteDestinoId = null;
  }

  private initTrasladoRetiroForm(): void {
    this.trasladoRetiroForm = this.fb.group({
      tipoOperacion: ['Venta', [Validators.required]], // Venta, Traslado
      fechaTraslado: [new Date().toISOString().split('T')[0], [Validators.required]],
      cantidadHembras: [0, [Validators.required, Validators.min(0)]],
      cantidadMachos: [0, [Validators.required, Validators.min(0)]],
      granjaDestinoId: [null],
      loteDestinoId: [null],
      tipoDestino: [null],
      motivo: ['', []],
      descripcion: ['', []],
      observaciones: ['']
    });

    // Actualizar validadores según tipo de operación
    this.trasladoRetiroForm.get('tipoOperacion')?.valueChanges.subscribe(tipo => {
      this.actualizarValidadoresAves(tipo);
    });

    this.applyDisponibilidadValidatorsRetiro();
  }

  /**
   * Máximos desde disponibilidad del lote o, si aún no cargó, desde inventario en pantalla.
   *
   * Antes esto exigía `tipoLote === 'Levante'`, y como el backend dejaba `aves` en null para todo
   * lote que ruteara a huevos, los lotes en producción caían siempre al inventario de pantalla.
   * Ahora `aves` viene siempre (también en producción, donde el lote igual tiene gallinas), así que
   * la condición correcta es que el bloque exista — no en qué fase está el lote.
   */
  private applyDisponibilidadValidatorsRetiro(): void {
    if (!this.trasladoRetiroForm) return;
    const d = this.disponibilidadLote();
    const hDisp = d?.aves?.hembrasVivas;
    const mDisp = d?.aves?.machosVivos;
    const inv = this.obtenerInventarioLoteSeleccionado();
    const maxH = hDisp != null && !Number.isNaN(Number(hDisp)) ? Number(hDisp) : (inv?.cantidadHembras ?? 999999);
    const maxM = mDisp != null && !Number.isNaN(Number(mDisp)) ? Number(mDisp) : (inv?.cantidadMachos ?? 999999);
    const ch = this.trasladoRetiroForm.get('cantidadHembras');
    const cm = this.trasladoRetiroForm.get('cantidadMachos');
    ch?.clearValidators();
    cm?.clearValidators();
    ch?.addValidators([Validators.required, Validators.min(0), Validators.max(maxH)]);
    cm?.addValidators([Validators.required, Validators.min(0), Validators.max(maxM)]);
    ch?.updateValueAndValidity({ emitEvent: false });
    cm?.updateValueAndValidity({ emitEvent: false });
  }

  private actualizarValidadoresAves(tipo: string): void {
    const granjaDestino = this.trasladoRetiroForm.get('granjaDestinoId');
    const tipoDestino = this.trasladoRetiroForm.get('tipoDestino');
    const motivo = this.trasladoRetiroForm.get('motivo');
    const descripcion = this.trasladoRetiroForm.get('descripcion');

    if (tipo === 'Venta') {
      granjaDestino?.clearValidators();
      tipoDestino?.clearValidators();
      motivo?.setValidators([Validators.required]);
      descripcion?.setValidators([Validators.required]);
    } else {
      granjaDestino?.setValidators([Validators.required]);
      tipoDestino?.setValidators([Validators.required]);
      motivo?.clearValidators();
      descripcion?.clearValidators();
    }

    granjaDestino?.updateValueAndValidity();
    tipoDestino?.updateValueAndValidity();
    motivo?.updateValueAndValidity();
    descripcion?.updateValueAndValidity();
  }

  private cargarDisponibilidadLote(loteId: string): void {
    this.loadingDisponibilidad.set(true);
    this.trasladosService.getDisponibilidadLote(loteId).subscribe({
      next: (disponibilidad) => {
        this.disponibilidadLote.set(disponibilidad);
        this.loadingDisponibilidad.set(false);
        this.applyDisponibilidadValidatorsRetiro();
      },
      error: (error) => {
        console.error('Error cargando disponibilidad:', error);
        this.disponibilidadLote.set(null);
        this.loadingDisponibilidad.set(false);
        this.applyDisponibilidadValidatorsRetiro();
      }
    });
  }

  /** Anular venta/traslado de aves: devuelve cantidades al inventario del lote (backend). */
  puedeAnularMovimientoAves(m: TrasladoUnificado): boolean { return puedeAnularMovimientoAvesFn(m); }

  async anularMovimientoAves(m: TrasladoUnificado): Promise<void> {
    if (!this.puedeAnularMovimientoAves(m)) return;
    // Primitiva del sistema de diseño en vez del `window.prompt()` nativo (CLAUDE.md §diseño).
    // `askText` devuelve `null` si el usuario cancela, y el texto si confirma — misma semántica
    // que tenía el prompt, así que el motivo que se guarda en la auditoría no cambia.
    const motivo = await this.confirmDialog.askText({
      title: 'Anular movimiento de aves',
      message: 'Las aves vuelven al inventario del lote si el movimiento ya había sido aplicado.',
      type: 'warning',
      confirmText: 'Anular movimiento',
      input: { label: 'Motivo de anulación', value: 'Anulado por usuario', placeholder: 'Motivo' }
    });
    if (motivo === null) return;
    const motivoFinal = motivo || 'Anulado por usuario';
    try {
      const res = await firstValueFrom(this.trasladosService.cancelarMovimiento(m.id, motivoFinal));
      if (!res?.success) {
        this.toast.error(res?.message || res?.errores?.join?.(', ') || 'No se pudo anular.');
        return;
      }
      await this.cargarInventarios();
      await this.cargarResumen();
      const lote = this.loteCompleto();
      if (lote) {
        const loteIdNum = parseInt(String(lote.loteId), 10);
        if (!isNaN(loteIdNum)) {
          await this.cargarMovimientosLote(loteIdNum);
          this.cargarDisponibilidadLote(String(lote.loteId));
        }
      }
    } catch (err: any) {
      this.toast.error(err?.error?.message || err?.message || 'No se pudo anular el movimiento.');
    }
  }

  // 🔴 Procesar retiro/traslado de aves
  async procesarRetiroTraslado(): Promise<void> {
    if (!this.trasladoRetiroForm.valid || !this.tieneLoteSeleccionadoCompleto) return;

    const lote = this.loteCompleto();
    if (!lote) return;

    const formValue = this.trasladoRetiroForm.value;
    const cantidadHembras = formValue.cantidadHembras || 0;
    const cantidadMachos = formValue.cantidadMachos || 0;
    const totalAves = cantidadHembras + cantidadMachos;

    if (totalAves <= 0) {
      this.errorRetiro.set('Debe especificar al menos una ave a retirar/trasladar');
      return;
    }

    const disp = this.disponibilidadLote();
    if (disp?.aves) {
      if (cantidadHembras > disp.aves.hembrasVivas) {
        this.errorRetiro.set(`Las hembras no pueden superar las disponibles (${disp.aves.hembrasVivas}).`);
        return;
      }
      if (cantidadMachos > disp.aves.machosVivos) {
        this.errorRetiro.set(`Los machos no pueden superar los disponibles (${disp.aves.machosVivos}).`);
        return;
      }
    }

    this.procesandoRetiro.set(true);
    this.errorRetiro.set(null);

    try {
      const fechaTraslado = typeof formValue.fechaTraslado === 'string'
        ? new Date(formValue.fechaTraslado)
        : (formValue.fechaTraslado instanceof Date ? formValue.fechaTraslado : new Date());

      if (formValue.tipoOperacion === 'Venta') {
        // Para venta, usar el nuevo endpoint de traslado de aves
        const dto: CrearTrasladoAvesDto = {
          loteId: String(lote.loteId),
          fechaTraslado: fechaTraslado,
          tipoOperacion: 'Venta',
          cantidadHembras: cantidadHembras,
          cantidadMachos: cantidadMachos,
          motivo: formValue.motivo,
          descripcion: formValue.descripcion,
          observaciones: formValue.observaciones
        };

        await firstValueFrom(this.trasladosService.crearTrasladoAves(dto));
      } else {
        // Para traslado, usar el nuevo endpoint
        const dto: CrearTrasladoAvesDto = {
          loteId: String(lote.loteId),
          fechaTraslado: fechaTraslado,
          tipoOperacion: 'Traslado',
          cantidadHembras: cantidadHembras,
          cantidadMachos: cantidadMachos,
          granjaDestinoId: formValue.granjaDestinoId ? Number(formValue.granjaDestinoId) : undefined,
          loteDestinoId: formValue.loteDestinoId ? String(formValue.loteDestinoId) : undefined,
          tipoDestino: formValue.tipoDestino,
          observaciones: formValue.observaciones
        };

        await firstValueFrom(this.trasladosService.crearTrasladoAves(dto));
      }

      this.exitoRetiro.set(true);
      await this.cargarInventarios();
      await this.cargarResumen();

      if (this.loteCompleto()) {
        const loteIdNum = parseInt(String(this.loteCompleto()!.loteId), 10);
        if (!isNaN(loteIdNum)) {
          this.cargarMovimientosLote(loteIdNum);
        }
        // Recargar disponibilidad para mostrar valores actualizados
        this.cargarDisponibilidadLote(String(this.loteCompleto()!.loteId));
      }

      // Mantener modal abierto por 3 segundos mostrando éxito, luego cerrar automáticamente
      setTimeout(() => {
        this.cerrarModalTrasladoRetiro();
      }, 3000);

    } catch (err: any) {
      console.error('Error al procesar retiro/traslado de aves:', err);
      this.errorRetiro.set(err?.error?.message || err?.error?.error || err?.message || 'Error al procesar el retiro/traslado de aves');
    } finally {
      this.procesandoRetiro.set(false);
    }
  }
}
