// src/app/features/traslados/pages/inventario-dashboard/inventario-dashboard.component.ts
import { Component, OnInit, signal, effect, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { firstValueFrom, forkJoin } from 'rxjs';

import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { HierarchicalFilterComponent } from '../../../../shared/components/hierarchical-filter/hierarchical-filter.component';

import { LoteDto } from '../../../lote/services/lote.service';
import {
  TrasladosAvesService,
  InventarioAvesDto,
  InventarioAvesSearchRequest,
  ResumenInventarioDto,
  CreateMovimientoAvesDto,
} from '../../services/traslados-aves.service';

import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { NucleoService, NucleoDto } from '../../../nucleo/services/nucleo.service';
import { GalponService } from '../../../galpon/services/galpon.service';
import { GalponDetailDto } from '../../../galpon/models/galpon.models';
import { Company, CompanyService } from '../../../../core/services/company/company.service';
// 🔴 Importa el servicio de Lotes
import { LoteService } from '../../../lote/services/lote.service';

@Component({
  selector: 'app-inventario-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, SidebarComponent, HierarchicalFilterComponent],
  templateUrl: './inventario-dashboard.component.html',
  styleUrls: ['./inventario-dashboard.component.scss']
})
export class InventarioDashboardComponent implements OnInit {
  // ====== State (signals) ======
  resumen = signal<ResumenInventarioDto | null>(null);

  private inventariosBase = signal<InventarioAvesDto[]>([]);
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

  // Filtros cascada
  selectedCompanyId: number | null = null;
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
    private loteService: LoteService
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
    if (this.selectedCompanyId == null) return this.farms;
    return this.farms.filter(f => f.companyId === this.selectedCompanyId);
  }

  get nucleosFiltered(): NucleoDto[] {
    if (this.selectedFarmId != null) return this.nucleos.filter(n => n.granjaId === this.selectedFarmId);
    if (this.selectedCompanyId != null) {
      const ids = new Set(this.farmsFiltered.map(f => f.id));
      return this.nucleos.filter(n => ids.has(n.granjaId));
    }
    return this.nucleos;
  }

  get galponesFiltered(): GalponDetailDto[] {
    let arr = this.galpones;
    if (this.selectedFarmId != null) {
      arr = arr.filter(g => g.granjaId === this.selectedFarmId);
    } else if (this.selectedCompanyId != null) {
      const ids = new Set(this.farmsFiltered.map(f => f.id));
      arr = arr.filter(g => ids.has(g.granjaId));
    }
    if (this.selectedNucleoId != null) arr = arr.filter(g => g.nucleoId === this.selectedNucleoId);
    return arr;
  }

  onCompanyChange(val: number | null): void {
    this.selectedCompanyId = val;
    if (this.selectedFarmId != null && !this.farmsFiltered.some(f => f.id === this.selectedFarmId)) this.selectedFarmId = null;
    if (this.selectedNucleoId != null && !this.nucleosFiltered.some(n => n.nucleoId === this.selectedNucleoId)) this.selectedNucleoId = null;
    if (this.selectedGalponId != null && !this.galponesFiltered.some(g => g.galponId === this.selectedGalponId)) this.selectedGalponId = null;

    this.resetLoteIfNotInContext();
    this.cargarLotesParaGalpon(this.selectedGalponId); // 🔴 recarga/limpia lotes
    this.recomputeList();
  }

  onFarmChange(val: number | null): void {
    this.selectedFarmId = val;
    if (this.selectedNucleoId != null && !this.nucleosFiltered.some(n => n.nucleoId === this.selectedNucleoId)) this.selectedNucleoId = null;
    if (this.selectedGalponId != null && !this.galponesFiltered.some(g => g.galponId === this.selectedGalponId)) this.selectedGalponId = null;

    this.resetLoteIfNotInContext();
    this.cargarLotesParaGalpon(this.selectedGalponId); // 🔴
    this.recomputeList();
  }

  onNucleoChange(val: string | null): void {
    this.selectedNucleoId = val;
    if (this.selectedGalponId != null && !this.galponesFiltered.some(g => g.galponId === this.selectedGalponId)) this.selectedGalponId = null;

    this.resetLoteIfNotInContext();
    this.cargarLotesParaGalpon(this.selectedGalponId); // 🔴
    this.recomputeList();
  }

  onGalponChange(val: string | null): void {
    this.selectedGalponId = val;
    this.resetLoteIfNotInContext();
    this.cargarLotesParaGalpon(val); // 🔴 carga real por galpón
    this.recomputeList();
  }

  resetFilters(): void {
    this.filtro = '';
    this.selectedCompanyId = null;
    this.selectedFarmId = null;
    this.selectedNucleoId = null;
    this.selectedGalponId = null;

    this.selectedLoteId = null;
    delete this.filtros.loteId;

    this.lotesForGalpon.set([]); // 🔴
    this.recomputeList();
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

    // Cascada
    if (this.selectedCompanyId != null) res = res.filter(inv => this.farmById[inv.granjaId]?.companyId === this.selectedCompanyId);
    if (this.selectedFarmId != null)    res = res.filter(inv => inv.granjaId === this.selectedFarmId);
    if (this.selectedNucleoId != null)  res = res.filter(inv => String(inv.nucleoId ?? '') === String(this.selectedNucleoId ?? ''));
    if (this.selectedGalponId != null)  res = res.filter(inv => String(inv.galponId ?? '') === String(this.selectedGalponId ?? ''));

    // 🔴 Filtrar por LOTE si viene del select superior
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
      this.selectedCompanyId ||
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
    } else {
      delete this.filtros.loteId;
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
}
