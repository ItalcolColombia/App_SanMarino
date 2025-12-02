// src/app/features/traslados/pages/inventario-dashboard/inventario-dashboard.component.ts
import { Component, OnInit, signal, effect, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { firstValueFrom, forkJoin } from 'rxjs';

import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { HierarchicalFilterComponent } from '../../../../shared/components/hierarchical-filter/hierarchical-filter.component';
import { ModalTrasladoLoteComponent } from '../../../lote/components/modal-traslado-lote/modal-traslado-lote.component';

import { LoteDto } from '../../../lote/services/lote.service';
import {
  TrasladosAvesService,
  InventarioAvesDto,
  InventarioAvesSearchRequest,
  ResumenInventarioDto,
  CreateMovimientoAvesDto,
  DisponibilidadLoteDto,
  CrearTrasladoAvesDto,
  CrearTrasladoHuevosDto,
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

@Component({
  selector: 'app-inventario-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, SidebarComponent, HierarchicalFilterComponent, ModalTrasladoLoteComponent],
  templateUrl: './inventario-dashboard.component.html',
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

  // ====== Modal Traslado ======
  modalTrasladoAbierto = signal<boolean>(false);
  trasladoForm!: FormGroup;

  loteOrigenSeleccionado = signal<LoteDto | null>(null);
  loteDestinoSeleccionado = signal<LoteDto | null>(null);

  inventarioOrigen = signal<InventarioAvesDto | null>(null);
  inventarioDestino = signal<InventarioAvesDto | null>(null);

  procesandoTraslado = signal<boolean>(false);
  errorTraslado = signal<string | null>(null);
  exitoTraslado = signal<boolean>(false);

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

  constructor(
    private trasladosService: TrasladosAvesService,
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
    private produccionService: LoteProduccionService
  ) {
    this.initTrasladoForm();

    effect(() => {
      if (this.inventarioOrigen()) this.validarCantidades();
    });
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
      const r = await firstValueFrom(this.trasladosService.getResumenInventario());
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
      const result = await firstValueFrom(this.trasladosService.searchInventarios(this.filtros));
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

  // ===================== Modal Traslado ======================
  private initTrasladoForm(): void {
    this.trasladoForm = this.fb.group({
      usuarioRealizaId: [null, [Validators.required, Validators.min(1)]],
      usuarioRecibeId:  [null, [Validators.required, Validators.min(1)]],
      fechaMovimiento:  [this.hoyISO(), [Validators.required]],
      tipoMovimiento:   ['TRASLADO', [Validators.required]],
      observaciones:    [''],
      cantidadHembras:  [0, [Validators.required, Validators.min(0)]],
      cantidadMachos:   [0, [Validators.required, Validators.min(0)]],
    });

    this.trasladoForm.get('cantidadHembras')?.valueChanges.subscribe(() => this.validarCantidades());
    this.trasladoForm.get('cantidadMachos')?.valueChanges.subscribe(() => this.validarCantidades());
  }

  isSubmitEnabled(): boolean {
    const f = this.trasladoForm;
    if (!f) return false;

    const h = Number(f.get('cantidadHembras')?.value) || 0;
    const m = Number(f.get('cantidadMachos')?.value) || 0;

    const hErr = !!f.get('cantidadHembras')?.errors;
    const mErr = !!f.get('cantidadMachos')?.errors;

    return f.valid
      && !!this.loteOrigenSeleccionado()
      && !!this.loteDestinoSeleccionado()
      && (h + m > 0)
      && !hErr
      && !mErr;
  }

  private hoyISO(): string {
    const d = new Date();
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  }

  onLoteOrigenSeleccionado(lote: LoteDto | null): void {
    this.loteOrigenSeleccionado.set(lote);
    this.inventarioOrigen.set(null);
    if (lote) this.cargarInventarioOrigen(lote.loteId);
  }

  onLoteDestinoSeleccionado(lote: LoteDto | null): void {
    this.loteDestinoSeleccionado.set(lote);
    this.inventarioDestino.set(null);
    if (lote) this.cargarInventarioDestino(lote.loteId);
  }

  private async cargarInventarioOrigen(loteId: number): Promise<void> {
    try {
      const inv = await firstValueFrom(this.trasladosService.getInventarioByLote(String(loteId)));
      this.inventarioOrigen.set(inv || null);
      this.validarCantidades();
    } catch (err) {
      console.error('Error cargando inventario origen:', err);
      this.inventarioOrigen.set(null);
      this.errorTraslado.set('Error al cargar el inventario del lote origen');
    }
  }

  private async cargarInventarioDestino(loteId: number): Promise<void> {
    try {
      const inv = await firstValueFrom(this.trasladosService.getInventarioByLote(String(loteId)));
      this.inventarioDestino.set(inv || null);
      this.errorTraslado.set(null);
    } catch (err: any) {
      // Si 404, deja inv=null sin bloquear
      if (err?.status === 404) {
        this.inventarioDestino.set(null);
        this.errorTraslado.set(null);
        return;
      }
      console.error('Error cargando inventario DESTINO:', err);
      this.inventarioDestino.set(null);
      this.errorTraslado.set('Error al cargar el inventario del lote destino');
    }
  }

  private validarCantidades(): void {
    const inv = this.inventarioOrigen();
    const hCtrl = this.trasladoForm.get('cantidadHembras');
    const mCtrl = this.trasladoForm.get('cantidadMachos');

    const clean = (ctrl: any) => {
      const errs = { ...(ctrl?.errors || {}) };
      delete (errs as any).exceedsAvailable;
      ctrl?.setErrors(Object.keys(errs).length ? errs : null);
    };
    clean(hCtrl);
    clean(mCtrl);

    if (!inv) return;

    const h = Number(hCtrl?.value) || 0;
    const m = Number(mCtrl?.value) || 0;

    if (h > inv.cantidadHembras) {
      hCtrl?.setErrors({ ...(hCtrl?.errors || {}), exceedsAvailable: { max: inv.cantidadHembras, actual: h } });
    }
    if (m > inv.cantidadMachos) {
      mCtrl?.setErrors({ ...(mCtrl?.errors || {}), exceedsAvailable: { max: inv.cantidadMachos, actual: m } });
    }

    if (h + m === 0) {
      this.errorTraslado.set('Debe trasladar al menos una ave');
    } else if (this.errorTraslado() === 'Debe trasladar al menos una ave') {
      this.errorTraslado.set(null);
    }
  }

  abrirModalTraslado(): void {
    this.modalTrasladoAbierto.set(true);
    this.limpiarFormularioTraslado();
  }
  cerrarModalTraslado(): void {
    this.modalTrasladoAbierto.set(false);
    this.limpiarFormularioTraslado();
  }
  private limpiarFormularioTraslado(): void {
    this.trasladoForm.reset({
      cantidadHembras: 0,
      cantidadMachos: 0,
      usuarioRealizaId: null,
      usuarioRecibeId: null,
      fechaMovimiento: this.toYMD(new Date()),
      tipoMovimiento: 'TRASLADO',
      observaciones: ''
    });
    this.loteOrigenSeleccionado.set(null);
    this.loteDestinoSeleccionado.set(null);
    this.inventarioOrigen.set(null);
    this.inventarioDestino.set(null);
    this.errorTraslado.set(null);
    this.exitoTraslado.set(false);
  }

  async procesarTraslado(): Promise<void> {
    if (!this.isSubmitEnabled()) return;

    const origen = this.loteOrigenSeleccionado();
    const destino = this.loteDestinoSeleccionado();
    if (!origen || !destino) return;

    if (String(origen.loteId) === String(destino.loteId)) {
      this.errorTraslado.set('El lote origen y destino no pueden ser el mismo');
      return;
    }

    this.procesandoTraslado.set(true);
    this.errorTraslado.set(null);

    try {
      const f = this.trasladoForm.value as any;
      const fechaIso = this.ymdToIsoNoon(f.fechaMovimiento);

      const obsExtras = `Realiza:${f.usuarioRealizaId} | Recibe:${f.usuarioRecibeId}`;
      const observaciones = [String(f.observaciones || '').trim(), obsExtras].filter(Boolean).join(' — ');

      const payload: CreateMovimientoAvesDto = {
        loteOrigenId: String(origen.loteId),
        loteDestinoId: String(destino.loteId),
        cantidadHembras: Number(f.cantidadHembras) || 0,
        cantidadMachos: Number(f.cantidadMachos) || 0,
        tipoMovimiento: f.tipoMovimiento || 'TRASLADO',
        observaciones,
        fechaMovimiento: new Date(fechaIso)
      };

      await firstValueFrom(this.trasladosService.createMovimiento(payload));

      this.exitoTraslado.set(true);
      this.cargarResumen();
      this.cargarInventarios();
      setTimeout(() => this.cerrarModalTraslado(), 1000);
    } catch (err: any) {
      console.error('Error procesando traslado:', err);
      this.errorTraslado.set(err.message || 'Error al procesar el traslado');
    } finally {
      this.procesandoTraslado.set(false);
    }
  }

  getTotalAves(): number {
    const hembras = this.trasladoForm.get('cantidadHembras')?.value || 0;
    const machos = this.trasladoForm.get('cantidadMachos')?.value || 0;
    return hembras + machos;
  }

  trasladarTodasLasHembras(): void {
    const inv = this.inventarioOrigen();
    if (inv) this.trasladoForm.get('cantidadHembras')?.setValue(inv.cantidadHembras);
  }
  trasladarTodosLosMachos(): void {
    const inv = this.inventarioOrigen();
    if (inv) this.trasladoForm.get('cantidadMachos')?.setValue(inv.cantidadMachos);
  }
  trasladarTodo(): void {
    this.trasladarTodasLasHembras();
    this.trasladarTodosLosMachos();
  }

  navegarATraslados(): void { this.abrirModalTraslado(); }
  navegarAMovimientos(): void { this.router.navigate(['historial'], { relativeTo: this.route }); }
  navegarANuevoTraslado(): void {
    // Usar ruta absoluta para evitar problemas de navegación
    this.router.navigate(['/traslados-aves/nuevo']);
  }

  // ===================== Utilidades ==========================
  calcularTotalAves(inv: InventarioAvesDto): number {
    return (inv?.cantidadHembras || 0) + (inv?.cantidadMachos || 0);
  }

  formatearFecha(fecha: Date | string): string {
    if (!fecha) return '—';
    const d = typeof fecha === 'string' ? new Date(fecha) : fecha;
    return d.toLocaleDateString('es-CO', {
      year: 'numeric', month: '2-digit', day: '2-digit',
      hour: '2-digit', minute: '2-digit'
    });
  }

  formatearNumero(n: number): string {
    return (n ?? 0).toLocaleString('es-CO', { maximumFractionDigits: 0 });
  }

  private normalize(s: string): string {
    return (s || '').toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '');
  }

  calcularEdadDias(fecha?: string | Date | null): number {
    if (!fecha) return 0;
    const inicio = new Date(fecha);
    const hoy = new Date();
    const msDia = 1000 * 60 * 60 * 24;
    return Math.floor((hoy.getTime() - inicio.getTime()) / msDia) + 1;
  }

  private toYMD(input: Date | string): string {
    const d = typeof input === 'string' ? new Date(input) : input;
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  private ymdToIsoNoon(ymd: string): string {
    return new Date(`${ymd}T12:00:00`).toISOString();
  }

  // TrackBy
  trackByInventarioId(_: number, item: InventarioAvesDto): number { return item.id; }
  trackByGranjaId(_: number, item: any): number { return item.granjaId; }

  async editarInventario(id: number): Promise<void> {
    try {
      await this.router.navigate(['../inventario', id, 'editar'], { relativeTo: this.route });
    } catch (err) {
      console.error('Navegación a edición falló:', err);
      alert('No se pudo abrir la edición del inventario.');
    }
  }

  async ajustarInventario(loteId: string): Promise<void> {
    if (!loteId) return;
    try {
      const hStr = window.prompt(`Nuevo valor de HEMBRAS para el lote ${loteId} (número entero):`, '0');
      if (hStr === null) return;
      const mStr = window.prompt(`Nuevo valor de MACHOS para el lote ${loteId} (número entero):`, '0');
      if (mStr === null) return;

      const cantidadHembras = Number(hStr);
      const cantidadMachos = Number(mStr);
      if (!Number.isFinite(cantidadHembras) || !Number.isFinite(cantidadMachos) || cantidadHembras < 0 || cantidadMachos < 0) {
        alert('Valores inválidos. Deben ser enteros ≥ 0.');
        return;
      }

      const tipoEvento = window.prompt('Tipo de evento:', 'AJUSTE_MANUAL') || 'AJUSTE_MANUAL';
      const observaciones = window.prompt('Observaciones (opcional):', '') || '';

      const ajuste = { cantidadHembras, cantidadMachos, tipoEvento, observaciones };
      await firstValueFrom(this.trasladosService.ajustarInventario(loteId, ajuste));

      await this.cargarResumen();
      await this.cargarInventarios();
      alert('Ajuste aplicado con éxito.');
    } catch (err: any) {
      console.error('Error al ajustar inventario:', err);
      this.error.set(err?.message || 'Error al ajustar el inventario');
      alert(this.error());
    }
  }

  async verTrazabilidad(loteId: string): Promise<void> {
    try {
      await this.router.navigate(['historial', loteId], { relativeTo: this.route });
    } catch (err) {
      console.error('No se pudo abrir la trazabilidad:', err);
      alert('No se pudo abrir la trazabilidad del lote.');
    }
  }

  async eliminarInventario(id: number): Promise<void> {
    if (!confirm('¿Está seguro de que desea eliminar este inventario? Esta acción no se puede deshacer.')) return;

    try {
      await firstValueFrom(this.trasladosService.deleteInventario(id));
      await this.cargarInventarios();
    } catch (err: any) {
      console.error('Error al eliminar inventario:', err);
      this.error.set(err?.message || 'Error al eliminar el inventario');
      alert(this.error());
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
    console.log(`[DEBUG] ========== onLoteSelectChange LLAMADO ==========`);
    console.log(`[DEBUG] Valor recibido: ${val} (tipo: ${typeof val})`);
    
    this.selectedLoteId = val;
    if (val) {
      this.filtros.loteId = val;
      // Cargar información completa del lote seleccionado
      const loteIdNum = parseInt(val, 10);
      console.log(`[DEBUG] LoteId parseado: ${loteIdNum} (es válido: ${!isNaN(loteIdNum)})`);
      
      if (!isNaN(loteIdNum)) {
        console.log(`[DEBUG] Cargando información del lote ${loteIdNum}...`);
        this.loteService.getById(loteIdNum).subscribe({
          next: (lote) => {
            console.log(`[DEBUG] ✅ Lote cargado:`, lote);
            this.loteCompleto.set(lote);
            
            // Buscar el inventario correspondiente para mostrar detalles
            const inventario = this.inventariosBase().find(inv => String(inv.loteId) === val);
            console.log(`[DEBUG] Inventario encontrado en inventariosBase:`, inventario ? 'Sí' : 'No');
            
            if (inventario) {
              // Si encontramos el inventario, usar el método completo
              console.log(`[DEBUG] Usando método seleccionarLote() con inventario existente`);
              this.seleccionarLote(inventario);
            } else {
              // Si no está en inventarios, crear un inventario "virtual" para mostrar los registros
              console.log(`[DEBUG] Creando inventario virtual para lote ${val}`);
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
              console.log(`[DEBUG] Estableciendo loteSeleccionado con inventario virtual`);
              this.loteSeleccionado.set(inventarioVirtual);
              this.tabRegistrosActivo.set('huevos');
              
              // Cargar todos los registros
              console.log(`[DEBUG] Iniciando carga de todos los registros...`);
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
      console.log(`[DEBUG] Limpiando selección de lote`);
      delete this.filtros.loteId;
      this.loteCompleto.set(null);
      this.seleccionarLote(null);
    }
    this.recomputeList();
    console.log(`[DEBUG] ========== FIN onLoteSelectChange ==========`);
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
    console.log(`[DEBUG] ========== seleccionarLote LLAMADO ==========`);
    console.log(`[DEBUG] Inventario recibido:`, inventario);
    
    this.loteSeleccionado.set(inventario);
    console.log(`[DEBUG] loteSeleccionado establecido:`, this.loteSeleccionado());

    if (inventario) {
      // Inicializar tab de registros al primer tab con datos disponibles
      // Prioridad: Huevos > Aves > Lotes
      this.tabRegistrosActivo.set('huevos');
      console.log(`[DEBUG] Tab activo establecido a: huevos`);
      
      // Cargar información completa del lote
      const loteIdNum = parseInt(inventario.loteId, 10);
      console.log(`[DEBUG] LoteId parseado: ${loteIdNum} (es válido: ${!isNaN(loteIdNum)})`);
      
      if (!isNaN(loteIdNum)) {
        this.loteService.getById(loteIdNum).subscribe({
          next: (lote) => {
            console.log(`[DEBUG] ✅ Lote completo cargado:`, lote);
            this.loteCompleto.set(lote);
          },
          error: (err) => {
            console.error(`[ERROR] Error al cargar lote completo:`, err);
            this.loteCompleto.set(null);
          }
        });

        // Cargar movimientos del lote
        console.log(`[DEBUG] Iniciando carga de datos para lote ${loteIdNum}...`);
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
      console.log(`[DEBUG] Limpiando selección (inventario es null)`);
      this.loteCompleto.set(null);
      this.movimientosLote.set([]);
      this.historialTrasladosLote.set([]);
      this.trasladosHuevosLote.set([]);
    }
    console.log(`[DEBUG] ========== FIN seleccionarLote ==========`);
  }

  private async cargarMovimientosLote(loteId: number): Promise<void> {
    this.loadingMovimientos.set(true);
    try {
      console.log(`[DEBUG] ========== INICIANDO CARGA DE MOVIMIENTOS DE AVES ==========`);
      console.log(`[DEBUG] LoteId recibido: ${loteId} (tipo: ${typeof loteId})`);
      
      // Usar el endpoint directo que retorna TODOS los movimientos sin límite
      console.log(`[DEBUG] Llamando a API: getMovimientosAvesPorLote(${loteId})`);
      const movimientosAves = await firstValueFrom(
        this.trasladosService.getMovimientosAvesPorLote(loteId)
      );
      console.log(`[DEBUG] ✅ Respuesta del API recibida:`, movimientosAves);
      console.log(`[DEBUG] Cantidad de registros: ${movimientosAves?.length || 0}`);
      
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
      
      console.log(`[DEBUG] ✅ Movimientos unificados procesados:`, movimientosUnificados);
      console.log(`[DEBUG] Estableciendo signals con ${movimientosUnificados.length} registros`);
      this.movimientosAvesLote.set(movimientosUnificados);
      this.movimientosLote.set(movimientosUnificados);
      console.log(`[DEBUG] ✅ Signals actualizados. Valores actuales:`, {
        movimientosAvesLote: this.movimientosAvesLote().length,
        movimientosLote: this.movimientosLote().length
      });
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
      console.log(`[DEBUG] ========== FIN CARGA DE MOVIMIENTOS DE AVES ==========`);
    }
  }

  private async cargarHistorialTrasladosLote(loteId: number): Promise<void> {
    this.loadingHistorialLotes.set(true);
    try {
      console.log(`[DEBUG] Cargando historial de traslados para lote ${loteId}`);
      const historial = await firstValueFrom(
        this.trasladosService.getHistorialTrasladosLote(loteId)
      );
      console.log(`[DEBUG] Historial recibido:`, historial);
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
      console.log(`[DEBUG] ========== INICIANDO CARGA DE TRASLADOS DE HUEVOS ==========`);
      console.log(`[DEBUG] LoteId recibido: ${loteId} (tipo: ${typeof loteId})`);
      
      // Usar el endpoint directo de traslados de huevos
      console.log(`[DEBUG] Llamando a API: getTrasladosHuevosPorLote(${loteId})`);
      const traslados = await firstValueFrom(
        this.trasladosService.getTrasladosHuevosPorLote(loteId)
      );
      console.log(`[DEBUG] ✅ Respuesta del API recibida:`, traslados);
      console.log(`[DEBUG] Cantidad de registros: ${traslados?.length || 0}`);
      
      // Asegurar que las fechas se conviertan correctamente
      const trasladosProcesados: TrasladoHuevosDto[] = (traslados || []).map(t => ({
        ...t,
        fechaTraslado: typeof t.fechaTraslado === 'string' ? new Date(t.fechaTraslado) : t.fechaTraslado,
        fechaProcesamiento: t.fechaProcesamiento ? (typeof t.fechaProcesamiento === 'string' ? new Date(t.fechaProcesamiento) : t.fechaProcesamiento) : undefined,
        fechaCancelacion: t.fechaCancelacion ? (typeof t.fechaCancelacion === 'string' ? new Date(t.fechaCancelacion) : t.fechaCancelacion) : undefined,
        createdAt: typeof t.createdAt === 'string' ? new Date(t.createdAt) : t.createdAt,
        updatedAt: t.updatedAt ? (typeof t.updatedAt === 'string' ? new Date(t.updatedAt) : t.updatedAt) : undefined
      }));
      
      console.log(`[DEBUG] ✅ Traslados de huevos procesados:`, trasladosProcesados);
      console.log(`[DEBUG] Estableciendo signal con ${trasladosProcesados.length} registros`);
      this.trasladosHuevosLote.set(trasladosProcesados);
      console.log(`[DEBUG] ✅ Signal actualizado. Valor actual:`, this.trasladosHuevosLote());
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
      console.log(`[DEBUG] ========== FIN CARGA DE TRASLADOS DE HUEVOS ==========`);
    }
  }


  obtenerTipoMovimientoClass(tipo: string): string {
    const tipoLower = tipo?.toLowerCase() || '';
    if (tipoLower.includes('traslado')) return 'badge--info';
    if (tipoLower.includes('retiro') || tipoLower.includes('salida')) return 'badge--danger';
    if (tipoLower.includes('entrada')) return 'badge--success';
    if (tipoLower.includes('ajuste')) return 'badge--warning';
    return 'badge--default';
  }

  obtenerEstadoClass(estado: string): string {
    const estadoLower = estado?.toLowerCase() || '';
    if (estadoLower === 'completado') return 'badge--success';
    if (estadoLower === 'pendiente') return 'badge--warning';
    if (estadoLower === 'cancelado') return 'badge--danger';
    return 'badge--default';
  }

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
  tabActivo = signal<'aves' | 'huevos'>('aves');
  trasladoRetiroForm!: FormGroup;
  trasladoHuevosForm!: FormGroup;
  disponibilidadLote = signal<DisponibilidadLoteDto | null>(null);
  loadingDisponibilidad = signal<boolean>(false);
  procesandoRetiro = signal<boolean>(false);
  errorRetiro = signal<string | null>(null);
  exitoRetiro = signal<boolean>(false);

  // Tipos de huevo
  tiposHuevo = [
    { key: 'limpio', label: 'Limpio' },
    { key: 'tratado', label: 'Tratado' },
    { key: 'sucio', label: 'Sucio' },
    { key: 'deforme', label: 'Deforme' },
    { key: 'blanco', label: 'Blanco' },
    { key: 'dobleYema', label: 'Doble Yema' },
    { key: 'piso', label: 'Piso' },
    { key: 'pequeno', label: 'Pequeño' },
    { key: 'roto', label: 'Roto' },
    { key: 'desecho', label: 'Desecho' },
    { key: 'otro', label: 'Otro' }
  ];

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
  }): Promise<void> {
    this.procesandoTrasladoLote.set(true);
    try {
      const dto: TrasladoLoteRequest = {
        loteId: data.loteId,
        granjaDestinoId: data.granjaDestinoId,
        nucleoDestinoId: data.nucleoDestinoId,
        galponDestinoId: data.galponDestinoId,
        observaciones: data.observaciones
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
      alert(err?.message || 'Error al procesar el traslado de lote');
    } finally {
      this.procesandoTrasladoLote.set(false);
    }
  }

  abrirModalTrasladoRetiro(tipo: 'aves' | 'huevos' = 'aves'): void {
    if (!this.tieneLoteSeleccionadoCompleto) return;

    this.tipoTrasladoSeleccionado.set(tipo);
    const lote = this.loteCompleto();
    if (lote) {
      this.cargarDisponibilidadLote(String(lote.loteId));
    }

    this.initTrasladoRetiroForm();
    this.initTrasladoHuevosForm();
    this.tabActivo.set(tipo);
    this.modalTrasladoRetiroAbierto.set(true);
    this.errorRetiro.set(null);
    this.exitoRetiro.set(false);
  }

  cerrarModalTrasladoRetiro(): void {
    this.modalTrasladoRetiroAbierto.set(false);
    this.trasladoRetiroForm.reset();
    this.trasladoHuevosForm.reset();
    this.disponibilidadLote.set(null);
    this.tabActivo.set('aves');
    this.errorRetiro.set(null);
    this.exitoRetiro.set(false);
    this.tipoTrasladoSeleccionado.set(null);
  }

  private initTrasladoRetiroForm(): void {
    const inventario = this.obtenerInventarioLoteSeleccionado();

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

    // Validar máximos disponibles
    if (inventario) {
      this.trasladoRetiroForm.get('cantidadHembras')?.addValidators(
        Validators.max(inventario.cantidadHembras)
      );
      this.trasladoRetiroForm.get('cantidadMachos')?.addValidators(
        Validators.max(inventario.cantidadMachos)
      );
    }

    // Actualizar validadores según tipo de operación
    this.trasladoRetiroForm.get('tipoOperacion')?.valueChanges.subscribe(tipo => {
      this.actualizarValidadoresAves(tipo);
    });
  }

  private initTrasladoHuevosForm(): void {
    const huevosControls: any = {
      tipoOperacion: ['Venta', [Validators.required]], // Venta, Traslado
      fechaTraslado: [new Date().toISOString().split('T')[0], [Validators.required]],
      granjaDestinoId: [null],
      loteDestinoId: [null],
      tipoDestino: [null],
      motivo: ['', []],
      descripcion: ['', []],
      observaciones: ['']
    };

    // Agregar controles para cada tipo de huevo
    this.tiposHuevo.forEach(tipo => {
      huevosControls[`cantidad${tipo.key.charAt(0).toUpperCase() + tipo.key.slice(1)}`] = [0, [Validators.min(0)]];
    });

    this.trasladoHuevosForm = this.fb.group(huevosControls);

    // Actualizar validadores según tipo de operación
    this.trasladoHuevosForm.get('tipoOperacion')?.valueChanges.subscribe(tipo => {
      this.actualizarValidadoresHuevos(tipo);
    });
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

  private actualizarValidadoresHuevos(tipo: string): void {
    const granjaDestino = this.trasladoHuevosForm.get('granjaDestinoId');
    const tipoDestino = this.trasladoHuevosForm.get('tipoDestino');
    const motivo = this.trasladoHuevosForm.get('motivo');
    const descripcion = this.trasladoHuevosForm.get('descripcion');

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
      },
      error: (error) => {
        console.error('Error cargando disponibilidad:', error);
        this.disponibilidadLote.set(null);
        this.loadingDisponibilidad.set(false);
      }
    });
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
      this.errorRetiro.set(err?.message || 'Error al procesar el retiro/traslado de aves');
    } finally {
      this.procesandoRetiro.set(false);
    }
  }

  // 🔴 Procesar traslado de huevos
  async procesarTrasladoHuevos(): Promise<void> {
    if (!this.trasladoHuevosForm.valid || !this.tieneLoteSeleccionadoCompleto) return;

    const lote = this.loteCompleto();
    if (!lote) return;

    const formValue = this.trasladoHuevosForm.value;

    // Validar que haya al menos un huevo seleccionado
    let totalHuevos = 0;
    this.tiposHuevo.forEach(tipo => {
      const cantidad = formValue[`cantidad${tipo.key.charAt(0).toUpperCase() + tipo.key.slice(1)}`] || 0;
      totalHuevos += cantidad;
    });

    if (totalHuevos <= 0) {
      this.errorRetiro.set('Debe especificar al menos un huevo a trasladar');
      return;
    }

    this.procesandoRetiro.set(true);
    this.errorRetiro.set(null);

    try {
      const fechaTraslado = typeof formValue.fechaTraslado === 'string'
        ? new Date(formValue.fechaTraslado)
        : (formValue.fechaTraslado instanceof Date ? formValue.fechaTraslado : new Date());

      const dto: CrearTrasladoHuevosDto = {
        loteId: String(lote.loteId),
        fechaTraslado: fechaTraslado,
        tipoOperacion: formValue.tipoOperacion,
        cantidadLimpio: formValue.cantidadLimpio || 0,
        cantidadTratado: formValue.cantidadTratado || 0,
        cantidadSucio: formValue.cantidadSucio || 0,
        cantidadDeforme: formValue.cantidadDeforme || 0,
        cantidadBlanco: formValue.cantidadBlanco || 0,
        cantidadDobleYema: formValue.cantidadDobleYema || 0,
        cantidadPiso: formValue.cantidadPiso || 0,
        cantidadPequeno: formValue.cantidadPequeno || 0,
        cantidadRoto: formValue.cantidadRoto || 0,
        cantidadDesecho: formValue.cantidadDesecho || 0,
        cantidadOtro: formValue.cantidadOtro || 0,
        granjaDestinoId: formValue.granjaDestinoId ? Number(formValue.granjaDestinoId) : undefined,
        loteDestinoId: formValue.loteDestinoId ? String(formValue.loteDestinoId) : undefined,
        tipoDestino: formValue.tipoDestino,
        motivo: formValue.motivo,
        descripcion: formValue.descripcion,
        observaciones: formValue.observaciones
      };

      await firstValueFrom(this.trasladosService.crearTrasladoHuevos(dto));

      this.exitoRetiro.set(true);
      await this.cargarInventarios();
      await this.cargarResumen();

      if (this.loteCompleto()) {
        const loteIdNum = parseInt(String(this.loteCompleto()!.loteId), 10);
        if (!isNaN(loteIdNum)) {
          this.cargarMovimientosLote(loteIdNum);
        }
        // Recargar disponibilidad
        this.cargarDisponibilidadLote(String(this.loteCompleto()!.loteId));
        // Recargar traslados de huevos
        this.cargarTrasladosHuevosLote(String(this.loteCompleto()!.loteId));
      }

      // Mantener modal abierto por 3 segundos mostrando éxito, luego cerrar automáticamente
      setTimeout(() => {
        this.cerrarModalTrasladoRetiro();
      }, 3000);

    } catch (err: any) {
      console.error('Error al procesar traslado de huevos:', err);
      this.errorRetiro.set(err?.message || 'Error al procesar el traslado de huevos');
    } finally {
      this.procesandoRetiro.set(false);
    }
  }
}
