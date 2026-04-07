// src/app/features/lote/pages/lote-list/lote-list.component.ts
import { Component, OnInit, Directive, ElementRef, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule, FormBuilder, FormGroup, Validators, FormsModule, NgControl
} from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';
import { finalize } from 'rxjs/operators';
import { forkJoin } from 'rxjs';

import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faPlus, faPen, faTrash, faTimes, faEye, faArrowRight, faMagnifyingGlass } from '@fortawesome/free-solid-svg-icons';
import { ModalTrasladoLoteComponent } from '../modal-traslado-lote/modal-traslado-lote.component';
import { FiltroSelectComponent } from '../../../lote-levante/pages/filtro-select/filtro-select.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmationModalComponent, ConfirmationModalData } from '../../../../shared/components/confirmation-modal/confirmation-modal.component';

import {
  LoteService, LoteDto, CreateLoteDto, UpdateLoteDto, LoteMortalidadResumenDto,
  TrasladoLoteRequest, TrasladoLoteResponse, LoteFormDataResponse
} from '../../services/lote.service';
import { LotePosturaLevanteService, LotePosturaLevanteDto } from '../../services/lote-postura-levante.service';
import { LotePosturaProduccionService, LotePosturaProduccionDto } from '../../services/lote-postura-produccion.service';
import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { NucleoService, NucleoDto } from '../../../nucleo/services/nucleo.service';
import { GalponService } from '../../../galpon/services/galpon.service';
import { GalponDetailDto } from '../../../galpon/models/galpon.models';
import { UserService, UserDto, User } from '../../../../core/services/user/user.service';
import { Company, CompanyService } from '../../../../core/services/company/company.service';
import { GuiaGeneticaService } from '../../services/guia-genetica.service';
import { LotePosturaBaseService, LotePosturaBaseDto, CreateLotePosturaBaseDto } from '../../services/lote-postura-base.service';

/* ============================================================
   Directiva standalone: separador de miles (es-CO) y enteros
   ============================================================ */
@Directive({
  selector: 'input[appThousandSeparator]',
  standalone: true
})
export class ThousandSeparatorDirective {
  private readonly formatter = new Intl.NumberFormat('es-CO', {
    minimumFractionDigits: 0,
    maximumFractionDigits: 0
  });
  private decimalSeparator = ',';
  private thousandSeparator = '.';

  constructor(
    private el: ElementRef<HTMLInputElement>,
    private ngControl: NgControl
  ) {}

  @HostListener('focus')
  onFocus() {
    const raw = this.unformat(this.el.nativeElement.value);
    this.el.nativeElement.value = raw ?? '';
  }

  @HostListener('input', ['$event'])
  onInput(e: Event) {
    const input = e.target as HTMLInputElement;
    const raw = this.unformat(input.value);
    const numeric = this.toNumber(raw);
    this.ngControl?.control?.setValue(
      isNaN(numeric) ? null : Math.round(numeric),
      { emitEvent: true, emitModelToViewChange: false }
    );
  }

  @HostListener('blur')
  onBlur() {
    const controlVal = this.ngControl?.control?.value;
    if (controlVal === null || controlVal === undefined || controlVal === '') {
      this.el.nativeElement.value = '';
      return;
    }
    const n = Number(controlVal);
    this.el.nativeElement.value = isNaN(n) ? '' : this.formatter.format(n);
  }

  private unformat(val: string): string {
    if (!val) return '';
    return val
      .replace(new RegExp('\\' + this.thousandSeparator, 'g'), '')
      .replace(this.decimalSeparator, '.')
      .replace(/[^\d.-]/g, '');
  }
  private toNumber(val: string): number { return parseFloat(val); }
}

/* ============================================================
   Componente
   ============================================================ */
@Component({
  selector: 'app-lote-list',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    HttpClientModule,
    FontAwesomeModule,
    FormsModule,
    ThousandSeparatorDirective,
    ModalTrasladoLoteComponent,
    FiltroSelectComponent,
    ConfirmationModalComponent
  ],
  templateUrl: './lote-list.component.html',
  styleUrls: ['./lote-list.component.scss']
})
export class LoteListComponent implements OnInit {
  // Iconos
  faPlus = faPlus; faPen = faPen; faTrash = faTrash; faTimes = faTimes; faEye = faEye; faArrowRight = faArrowRight;
  faMagnifyingGlass = faMagnifyingGlass;

  // Estado UI
  loading = false;
  modalOpen = false;
  baseModalOpen = false;
  editing: LoteDto | null = null;
  selectedLote: LoteDto | null = null;
  selectedLoteLevante: LotePosturaLevanteDto | null = null;
  selectedLoteProduccion: LotePosturaProduccionDto | null = null;
  
  // Modal de traslado
  modalTrasladoOpen = false;
  loteParaTrasladar: LoteDto | null = null;
  loadingTraslado = false;

  // Pestaña: Lote = lotes (primera por defecto), Levante = lote_postura_levante, Producción = lote_postura_produccion
  activeTab: 'levante' | 'lote' | 'produccion' = 'lote';

  // Búsqueda y orden
  filtro = '';
  sortKey: 'edad' | 'fecha' = 'edad';
  sortDir: 'asc' | 'desc' = 'desc';

  // Form
  form!: FormGroup;
  baseForm!: FormGroup;

  // Datos maestros
  farms:    FarmDto[]   = [];
  nucleos:  NucleoDto[] = [];
  galpones: GalponDetailDto[] = [];
  tecnicos: User[]   = [];

  // Datos para raza y línea genética
  razasDisponibles: string[] = [];
  anosDisponibles: number[] = [];
  selectedRaza: string = '';
  selectedAnoTabla: number | null = null;
  loadingAnos: boolean = false;
  razaValida: boolean = true;
  companies: Company[]  = [];

  // Parent lot functionality
  esLotePadre: boolean = false;
  lotesPadresDisponibles: LoteDto[] = [];
  filteredLotesPadres: LoteDto[] = [];
  loadingLotesPadres: boolean = false;
  
  // Filtros para lote padre (cascada)
  selectedGranjaPadreId: number | null = null;
  selectedNucleoPadreId: string | null = null;
  selectedGalponPadreId: string | null = null;

  // Mapas
  farmMap:   Record<number, string> = {};
  nucleoMap: Record<string, string> = {};
  galponMap: Record<string, string> = {};
  techMap:   Record<string, string> = {};
  private farmById: Record<number, FarmDto> = {};

  // Lotes (tab Lote = tabla lotes)
  lotes: LoteDto[] = [];
  viewLotes: LoteDto[] = [];
  // Lotes postura base (creación rápida)
  lotesPosturaBase: LotePosturaBaseDto[] = [];
  baseLotesOptions: LotePosturaBaseDto[] = [];
  selectedBaseLote: LotePosturaBaseDto | null = null;
  // Lotes postura levante (tab Levante = tabla lote_postura_levante)
  lotesLevante: LotePosturaLevanteDto[] = [];
  viewLotesLevante: LotePosturaLevanteDto[] = [];
  // Lotes postura producción (tab Producción = tabla lote_postura_produccion)
  lotesProduccion: LotePosturaProduccionDto[] = [];
  viewLotesProduccion: LotePosturaProduccionDto[] = [];
  lotesReproductora: any[] = [];
  lotesLevanteAsociados: LotePosturaLevanteDto[] = [];
  lotesProduccionAsociados: LotePosturaProduccionDto[] = [];
  activeDetailTab: 'levante' | 'produccion' | 'reproductora' = 'levante';

  // Filtros en cascada (lista) — opciones derivadas de los lotes cargados
  selectedCompanyId: number | null = null;
  selectedFarmId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  filterCompanyOptions: { id: number; label: string }[] = [];
  filterFarmOptions: { id: number; name: string }[] = [];
  filterNucleoOptions: { nucleoId: string; nucleoNombre: string }[] = [];
  filterGalponOptions: { galponId: string; galponNombre: string }[] = [];

  // Modal confirmación eliminar
  confirmOpen = false;
  confirmData: ConfirmationModalData = {
    title: 'Eliminar lote',
    message: 'Esta acción no se puede deshacer.',
    confirmText: 'Eliminar',
    cancelText: 'Cancelar',
    type: 'warning',
    showCancel: true,
  };
  pendingDelete: LoteDto | null = null;

  // Carga del modal (solo al abrir crear/editar)
  loadingModal = false;

  // Filtros (modal)
  nucleosFiltrados:   NucleoDto[] = [];
  galponesFiltrados:  GalponDetailDto[] = [];
  filteredNucleos:    NucleoDto[] = [];
  filteredGalpones:   GalponDetailDto[] = [];

  // Resúmenes
  resumenMap: Record<string, LoteMortalidadResumenDto> = {};
  resumenSelected: LoteMortalidadResumenDto | null = null;

  constructor(
    private fb:        FormBuilder,
    private loteSvc:   LoteService,
    private lotePosturaLevanteSvc: LotePosturaLevanteService,
    private lotePosturaProduccionSvc: LotePosturaProduccionService,
    private lotePosturaBaseSvc: LotePosturaBaseService,
    private farmSvc:   FarmService,
    private nucleoSvc: NucleoService,
    private galponSvc: GalponService,
    private userSvc:   UserService,
    private companySvc: CompanyService,
    private guiaGeneticaSvc: GuiaGeneticaService,
    private toastService: ToastService
  ) {}

  // Método de prueba para diagnosticar problemas
  testFarmService(): void {
    console.log('=== Test Farm Service ===');
    this.farmSvc.testConnection().subscribe({
      next: (response) => {
        console.log('✅ Test exitoso:', response);
      },
      error: (error) => {
        console.error('❌ Test falló:', error);
      }
    });
  }

  // ===================== Ciclo de vida ======================
  ngOnInit(): void {
    this.initForm();
    this.initBaseForm();
    this.loadData();

    // Lote base -> autogenerar nombre
    this.form.get('lotePosturaBaseId')!.valueChanges.subscribe((val) => {
      const id = val != null && val !== '' ? Number(val) : null;
      this.onBaseLoteChange(isNaN(Number(id)) ? null : id);
    });

    // Chains del modal (usan datos cargados al abrir el modal)
    this.form.get('granjaId')!.valueChanges.subscribe(granjaIdVal => {
      const granjaId = Number(granjaIdVal);
      this.nucleosFiltrados = this.nucleos.filter(n => Number(n.granjaId) === granjaId);
      this.filteredNucleos = this.nucleosFiltrados;
      this.galponesFiltrados = [];
      this.filteredGalpones = [];
      this.form.get('galponId')?.setValue(null, { emitEvent: false });

      const primerNucleo = this.nucleosFiltrados[0]?.nucleoId ?? null;
      this.form.patchValue({ nucleoId: primerNucleo });
      if (primerNucleo && this.galpones.length > 0) {
        const filtrados = this.galpones.filter(
          g => Number(g.granjaId) === granjaId && String(g.nucleoId) === String(primerNucleo)
        );
        this.galponesFiltrados = [...filtrados];
        this.filteredGalpones = this.galponesFiltrados;
        const primerGalpon = this.galponesFiltrados[0]?.galponId ?? null;
        this.form.patchValue({ galponId: primerGalpon }, { emitEvent: false });
      }
    });

    this.form.get('nucleoId')!.valueChanges.subscribe((nucleoIdVal: string | number | null) => {
      const granjaId = Number(this.form.get('granjaId')?.value);
      const nucleoId = nucleoIdVal != null ? String(nucleoIdVal) : null;
      if (granjaId && nucleoId) {
        if (this.galpones.length > 0) {
          const filtrados = this.galpones.filter(
            g => Number(g.granjaId) === granjaId && String(g.nucleoId) === nucleoId
          );
          this.galponesFiltrados = [...filtrados];
          this.filteredGalpones = this.galponesFiltrados;
          const primerGalpon = this.galponesFiltrados[0]?.galponId ?? null;
          this.form.patchValue({ galponId: primerGalpon }, { emitEvent: false });
        } else {
          this.galponSvc.getByGranjaAndNucleo(granjaId, nucleoId).subscribe(data => {
            this.galponesFiltrados = [...data];
            this.filteredGalpones = this.galponesFiltrados;
            const primerGalpon = this.galponesFiltrados[0]?.galponId ?? null;
            this.form.patchValue({ galponId: primerGalpon }, { emitEvent: false });
          });
        }
      } else {
        this.galponesFiltrados = [];
        this.filteredGalpones = [];
        this.form.get('galponId')?.setValue(null, { emitEvent: false });
      }
    });

    // Totales (encasetadas)
    this.form.get('hembrasL')!.valueChanges.subscribe(() => this.actualizarEncasetadas());
    this.form.get('machosL')!.valueChanges.subscribe(() => this.actualizarEncasetadas());

    // Chain: Raza -> Año Tabla Genética
    this.form.get('raza')!.valueChanges.subscribe(raza => {
      console.log('=== LoteList: Cambio en raza ===');
      console.log('Nueva raza seleccionada:', raza);
      
      this.selectedRaza = raza;
      this.anosDisponibles = [];
      this.form.patchValue({ anoTablaGenetica: null });
      
      if (raza) {
        console.log('Cargando años para raza:', raza);
        this.loadAnosDisponibles(raza);
      } else {
        console.log('Raza vacía, no cargando años');
      }
    });

    // Chain: Es Sublote -> mostrar/ocultar filtros de lote padre
    // NOTA: esLotePadre en el formulario significa "es sublote" (tiene padre)
    this.form.get('esLotePadre')!.valueChanges.subscribe(esSublote => {
      this.esLotePadre = esSublote;
      if (esSublote) {
        // Si es sublote, debe seleccionar un lote padre
        this.cargarLotesPadres();
      } else {
        // Si no es sublote, no tiene padre
        this.form.patchValue({ lotePadreId: null });
        this.selectedGranjaPadreId = null;
        this.selectedNucleoPadreId = null;
        this.selectedGalponPadreId = null;
        this.filteredLotesPadres = [];
      }
    });
    
    // Inicializar el valor de esLotePadre desde el formulario
    this.esLotePadre = this.form.get('esLotePadre')?.value || false;
  }

  // ===================== Init form ==========================
  private initForm(): void {
    this.form = this.fb.group({
      loteId:             [null], // Opcional - auto-incremento numérico
      loteNombre:         ['', Validators.required],
      lotePosturaBaseId:  [null], // solo UI (no persiste en Lote)
      granjaId:           [null, Validators.required],
      nucleoId:           [null],
      galponId:           [null],
      regional:           [''],
      fechaEncaset:       [null, Validators.required],
      hembrasL:           [null],
      machosL:            [null],
      pesoInicialH:       [null], // gramos
      pesoInicialM:       [null], // gramos
      unifH:              [null],
      unifM:              [null],
      mortCajaH:          [null],
      mortCajaM:          [null],
      raza:               ['', Validators.required],
      anoTablaGenetica:   [null],
      linea:              [''],
      tipoLinea:          [''],
      codigoGuiaGenetica: [''],
      tecnico:            [''],
      avesEncasetadas:    [null],
      loteErp:            [''],
      lineaGenetica:      [''],
      esLotePadre:        [false],
      lotePadreId:        [null]
    });
  }

  private initBaseForm(): void {
    this.baseForm = this.fb.group({
      loteNombre: ['', [Validators.required, Validators.minLength(2)]],
      codigoErp: [''],
      cantidadHembras: [0, [Validators.required, Validators.min(0)]],
      cantidadMachos: [0, [Validators.required, Validators.min(0)]],
      cantidadMixtas: [0, [Validators.required, Validators.min(0)]],
    });
  }

  // ===================== Carga ==============================
  private loadData(): void {
    this.loading = true;
    if (this.activeTab === 'levante') {
      this.lotePosturaLevanteSvc.getAll()
        .pipe(finalize(() => this.loading = false))
        .subscribe({
          next: (list) => {
            this.lotesLevante = list;
            this.buildFilterOptions();
            this.recomputeList();
          },
          error: () => this.toastService.error('No se pudo cargar la lista de lotes levante.', 'Error')
        });
    } else if (this.activeTab === 'produccion') {
      this.lotePosturaProduccionSvc.getAll()
        .pipe(finalize(() => this.loading = false))
        .subscribe({
          next: (list) => {
            this.lotesProduccion = list;
            this.buildFilterOptions();
            this.recomputeList();
          },
          error: () => this.toastService.error('No se pudo cargar la lista de lotes producción.', 'Error')
        });
    } else {
      this.loteSvc.getAll()
        .pipe(finalize(() => this.loading = false))
        .subscribe({
          next: (list) => {
            this.lotes = list;
            this.buildFilterOptions();
            this.recomputeList();
            this.loadLotesPosturaBase();
          },
          error: () => this.toastService.error('No se pudo cargar la lista de lotes.', 'Error')
        });
    }
  }

  private loadLotesPosturaBase(): void {
    // Solo se usa visualmente en el tab "Lote"
    this.lotePosturaBaseSvc.getAll().subscribe({
      next: (items) => this.lotesPosturaBase = items ?? [],
      error: () => { /* no bloquear el resto de la pantalla */ }
    });
  }

  getBaseLoteNombre(id?: number | null): string {
    if (!id) return '—';
    const found =
      (this.lotesPosturaBase ?? []).find(x => x.lotePosturaBaseId === id) ??
      (this.baseLotesOptions ?? []).find(x => x.lotePosturaBaseId === id);
    return found?.loteNombre ?? `#${id}`;
  }

  setTab(tab: 'levante' | 'lote' | 'produccion'): void {
    if (this.activeTab === tab) return;
    this.activeTab = tab;
    this.loadData();
  }

  /** Construye opciones de filtro desde los datos cargados según la pestaña activa. */
  private buildFilterOptions(): void {
    const companies = new Map<number, string>();
    const farms = new Map<number, string>();
    const nucleos = new Map<string, string>();
    const galpones = new Map<string, string>();
    if (this.activeTab === 'levante') {
      for (const l of this.lotesLevante) {
        if (l.companyId != null) companies.set(l.companyId, `Compañía ${l.companyId}`);
        if (l.farm?.id != null) farms.set(l.farm.id, l.farm.name ?? '');
        if (l.nucleo?.nucleoId) nucleos.set(l.nucleo.nucleoId, l.nucleo.nucleoNombre ?? l.nucleo.nucleoId);
        if (l.galpon?.galponId) galpones.set(l.galpon.galponId, l.galpon.galponNombre ?? l.galpon.galponId);
      }
    } else if (this.activeTab === 'produccion') {
      for (const l of this.lotesProduccion) {
        if (l.companyId != null) companies.set(l.companyId, `Compañía ${l.companyId}`);
        if (l.farm?.id != null) farms.set(l.farm.id, l.farm.name ?? '');
        if (l.nucleo?.nucleoId) nucleos.set(l.nucleo.nucleoId, l.nucleo.nucleoNombre ?? l.nucleo.nucleoId);
        if (l.galpon?.galponId) galpones.set(l.galpon.galponId, l.galpon.galponNombre ?? l.galpon.galponId);
      }
    } else {
      for (const l of this.lotes) {
        if (l.companyId != null) companies.set(l.companyId, `Compañía ${l.companyId}`);
        if (l.farm?.id != null) farms.set(l.farm.id, l.farm.name ?? '');
        if (l.nucleo?.nucleoId) nucleos.set(l.nucleo.nucleoId, l.nucleo.nucleoNombre ?? l.nucleo.nucleoId);
        if (l.galpon?.galponId) galpones.set(l.galpon.galponId, l.galpon.galponNombre ?? l.galpon.galponId);
      }
    }
    this.filterCompanyOptions = Array.from(companies.entries()).map(([id, label]) => ({ id, label }));
    this.filterFarmOptions = Array.from(farms.entries()).map(([id, name]) => ({ id, name }));
    this.filterNucleoOptions = Array.from(nucleos.entries()).map(([nucleoId, nucleoNombre]) => ({ nucleoId, nucleoNombre }));
    this.filterGalponOptions = Array.from(galpones.entries()).map(([galponId, galponNombre]) => ({ galponId, galponNombre }));
  }

  // ===================== Filtros (lista) — opciones desde tabla ====================
  onCompanyChangeList(val: number | null) {
    this.selectedCompanyId = val;
    this.recomputeList();
  }
  onFarmChangeList(val: number | null) {
    this.selectedFarmId = val;
    this.recomputeList();
  }
  onNucleoChangeList(_val: string | null) {
    this.recomputeList();
  }
  resetListFilters() {
    this.filtro = '';
    this.selectedCompanyId = null;
    this.selectedFarmId = null;
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.recomputeList();
  }

  // ===================== Ordenamiento =======================
  onSortKeyChange(v: 'edad'|'fecha') { this.sortKey = v; this.recomputeList(); }
  onSortDirChange(v: 'asc'|'desc') { this.sortDir = v; this.recomputeList(); }

  // ===================== Recompute (filtros + orden) ========
  recomputeList() {
    const term = this.normalize(this.filtro);
    if (this.activeTab === 'levante') {
      let res = [...this.lotesLevante];
      if (this.selectedCompanyId != null) res = res.filter(l => (l.companyId ?? null) === this.selectedCompanyId);
      if (this.selectedFarmId != null) res = res.filter(l => l.granjaId === this.selectedFarmId);
      if (this.selectedNucleoId != null) res = res.filter(l => (l.nucleoId ?? null) === this.selectedNucleoId);
      if (this.selectedGalponId != null) res = res.filter(l => (l.galponId ?? null) === this.selectedGalponId);
      if (term) {
        res = res.filter(l => {
          const haystack = [
            l.lotePosturaLevanteId ?? 0,
            l.loteNombre ?? '',
            l.loteErp ?? '',
            l.farm?.name ?? '',
            l.nucleo?.nucleoNombre ?? '',
            l.galpon?.galponNombre ?? ''
          ].map(s => this.normalize(String(s))).join(' ');
          return haystack.includes(term);
        });
      }
      res = this.sortLotesLevante(res);
      this.viewLotesLevante = res;
    } else if (this.activeTab === 'produccion') {
      let res = [...this.lotesProduccion];
      if (this.selectedCompanyId != null) res = res.filter(l => (l.companyId ?? null) === this.selectedCompanyId);
      if (this.selectedFarmId != null) res = res.filter(l => l.granjaId === this.selectedFarmId);
      if (this.selectedNucleoId != null) res = res.filter(l => (l.nucleoId ?? null) === this.selectedNucleoId);
      if (this.selectedGalponId != null) res = res.filter(l => (l.galponId ?? null) === this.selectedGalponId);
      if (term) {
        res = res.filter(l => {
          const haystack = [
            l.lotePosturaProduccionId ?? 0,
            l.loteNombre ?? '',
            l.loteErp ?? '',
            l.farm?.name ?? '',
            l.nucleo?.nucleoNombre ?? '',
            l.galpon?.galponNombre ?? ''
          ].map(s => this.normalize(String(s))).join(' ');
          return haystack.includes(term);
        });
      }
      res = this.sortLotesProduccion(res);
      this.viewLotesProduccion = res;
    } else {
      let res = [...this.lotes];
      if (this.selectedCompanyId != null) res = res.filter(l => (l.companyId ?? null) === this.selectedCompanyId);
      if (this.selectedFarmId != null) res = res.filter(l => l.granjaId === this.selectedFarmId);
      if (this.selectedNucleoId != null) res = res.filter(l => (l.nucleoId ?? null) === this.selectedNucleoId);
      if (this.selectedGalponId != null) res = res.filter(l => (l.galponId ?? null) === this.selectedGalponId);
      if (term) {
        res = res.filter(l => {
          const haystack = [
            l.loteId ?? 0,
            l.loteNombre ?? '',
            l.loteErp ?? '',
            l.farm?.name ?? '',
            l.nucleo?.nucleoNombre ?? '',
            l.galpon?.galponNombre ?? ''
          ].map(s => this.normalize(String(s))).join(' ');
          return haystack.includes(term);
        });
      }
      res = this.sortLotes(res);
      this.viewLotes = res;
    }
  }

  private sortLotes(arr: LoteDto[]): LoteDto[] {
    const val = (l: LoteDto): number | null => {
      if (!l.fechaEncaset) return null;
      if (this.sortKey === 'edad') return this.calcularEdadSemanas(l.fechaEncaset);
      const t = new Date(l.fechaEncaset).getTime();
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

  private sortLotesProduccion(arr: LotePosturaProduccionDto[]): LotePosturaProduccionDto[] {
    const val = (l: LotePosturaProduccionDto): number | null => {
      const edad = l.edad ?? (l.fechaEncaset ? this.calcularEdadSemanas(l.fechaEncaset) : null);
      if (this.sortKey === 'edad' && edad != null) return edad;
      if (l.fechaEncaset) {
        const t = new Date(l.fechaEncaset).getTime();
        return isNaN(t) ? null : t;
      }
      return null;
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

  private sortLotesLevante(arr: LotePosturaLevanteDto[]): LotePosturaLevanteDto[] {
    const val = (l: LotePosturaLevanteDto): number | null => {
      const edad = l.edad ?? (l.fechaEncaset ? this.calcularEdadSemanas(l.fechaEncaset) : null);
      if (this.sortKey === 'edad' && edad != null) return edad;
      if (l.fechaEncaset) {
        const t = new Date(l.fechaEncaset).getTime();
        return isNaN(t) ? null : t;
      }
      return null;
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

  // ===================== Acciones UI ========================
  openDetailProduccion(l: LotePosturaProduccionDto): void {
    this.selectedLoteProduccion = l;
    this.selectedLoteLevante = null;
    this.selectedLote = null;
  }

  openDetailLevante(l: LotePosturaLevanteDto): void {
    this.selectedLoteLevante = l;
    this.selectedLoteProduccion = null;
    this.selectedLote = null;
    this.lotePosturaLevanteSvc.getById(l.lotePosturaLevanteId).subscribe({
      next: (detail) => { this.selectedLoteLevante = detail; },
      error: () => { /* mantener datos de lista si falla */ }
    });
  }

  openDetail(lote: LoteDto): void {
    this.selectedLote = lote;
    this.selectedLoteLevante = null;
    this.selectedLoteProduccion = null;
    this.resumenSelected = null;
    this.activeDetailTab = 'levante';
    this.lotesLevanteAsociados = [];
    this.lotesProduccionAsociados = [];

    this.loteSvc.getResumenMortalidad(lote.loteId).subscribe({
      next: (r) => this.resumenSelected = r,
      error: () => this.resumenSelected = null
    });

    this.loteSvc.getReproductorasByLote(lote.loteId).subscribe({
      next: (r) => this.lotesReproductora = r ?? [],
      error: () => this.lotesReproductora = []
    });

    this.lotePosturaLevanteSvc.getByLoteId(lote.loteId).subscribe({
      next: (r) => this.lotesLevanteAsociados = r ?? [],
      error: () => this.lotesLevanteAsociados = []
    });

    this.lotePosturaProduccionSvc.getByLoteId(lote.loteId).subscribe({
      next: (r) => this.lotesProduccionAsociados = r ?? [],
      error: () => this.lotesProduccionAsociados = []
    });
  }

  setDetailTab(tab: 'levante' | 'produccion' | 'reproductora'): void {
    this.activeDetailTab = tab;
  }

  /** Carga todos los datos del modal crear/editar lote en una sola llamada (GET api/Lote/form-data). */
  private loadModalData(): void {
    this.loadingModal = true;
    this.loteSvc.getFormData()
      .pipe(finalize(() => (this.loadingModal = false)))
      .subscribe({
        next: (data) => this.applyFormDataResponse(data),
        error: () => this.toastService.error('No se pudieron cargar los datos del formulario.', 'Error')
      });
  }

  /**
   * Distribuye la respuesta de form-data a las propiedades que usa el modal
   * (cada select: Granja, Núcleo, Galpón, Raza; técnicos y companies para uso interno).
   */
  private applyFormDataResponse(data: LoteFormDataResponse): void {
    const raw = (data as unknown) as Record<string, unknown>;
    const farms = (raw['farms'] ?? raw['Farms'] ?? []) as FarmDto[];
    const nucleos = (raw['nucleos'] ?? raw['Nucleos'] ?? []) as NucleoDto[];
    const galpones = (raw['galpones'] ?? raw['Galpones'] ?? []) as GalponDetailDto[];
    const tecnicos = (raw['tecnicos'] ?? raw['Tecnicos'] ?? []) as User[];
    const companies = (raw['companies'] ?? raw['Companies'] ?? []) as Company[];
    const razas = (raw['razas'] ?? raw['Razas'] ?? []) as string[];

    this.farms = farms;
    this.farmById = {};
    this.farmMap = {};
    farms.forEach((f) => {
      this.farmById[f.id] = f;
      this.farmMap[f.id] = f.name;
    });

    this.nucleos = nucleos;
    this.nucleoMap = {};
    nucleos.forEach((n) => {
      this.nucleoMap[n.nucleoId] = n.nucleoNombre ?? n.nucleoId;
    });

    this.galpones = galpones;
    this.galponMap = {};
    galpones.forEach((g) => {
      this.galponMap[g.galponId] = g.galponNombre ?? g.galponId;
    });

    this.tecnicos = tecnicos;
    this.techMap = {};
    tecnicos.forEach((u) => {
      if (u.id) this.techMap[u.id] = `${u.surName ?? ''} ${u.firstName ?? ''}`.trim();
    });

    this.companies = companies;
    this.razasDisponibles = Array.isArray(razas) ? [...razas] : [];

    const granjaId = this.editing?.granjaId ?? this.form.get('granjaId')?.value;
    const nucleoId = this.editing?.nucleoId ?? this.form.get('nucleoId')?.value;
    this.nucleosFiltrados = granjaId != null ? this.nucleos.filter((n) => n.granjaId === Number(granjaId)) : [];
    this.filteredNucleos = this.nucleosFiltrados;
    this.galponesFiltrados =
      granjaId != null && nucleoId != null
        ? this.galpones.filter((g) => g.granjaId === Number(granjaId) && g.nucleoId === String(nucleoId))
        : [];
    this.filteredGalpones = this.galponesFiltrados;

    this.applyModalFormState();
  }

  private applyModalFormState(): void {
    const l = this.editing;
    if (l) {
      this.form.patchValue({
        ...l,
        fechaEncaset: l.fechaEncaset
          ? new Date(l.fechaEncaset).toISOString().substring(0, 10)
          : null
      });
      this.nucleosFiltrados = this.nucleos.filter(n => n.granjaId === l.granjaId);
      this.filteredNucleos = this.nucleosFiltrados;
      this.galponesFiltrados = this.galpones.filter(g =>
        g.granjaId === l.granjaId && g.nucleoId === String(l.nucleoId ?? '')
      );
      this.filteredGalpones = this.galponesFiltrados;
      const esSublote = !!l.lotePadreId;
      this.esLotePadre = esSublote;
      this.form.patchValue({ esLotePadre: esSublote, lotePadreId: l.lotePadreId || null });
      if (esSublote && l.lotePadreId) {
        this.loteSvc.getById(l.lotePadreId).subscribe({
          next: (lotePadre) => {
            this.selectedGranjaPadreId = lotePadre.granjaId;
            this.selectedNucleoPadreId = lotePadre.nucleoId || null;
            this.selectedGalponPadreId = lotePadre.galponId || null;
            this.cargarLotesPadres();
          }
        });
      } else {
        this.selectedGranjaPadreId = null;
        this.selectedNucleoPadreId = null;
        this.selectedGalponPadreId = null;
      }
    } else {
      this.form.reset({
        loteId: null,
        loteNombre: '',
        granjaId: null,
        nucleoId: null,
        galponId: null,
        regional: '',
        fechaEncaset: null,
        hembrasL: null,
        machosL: null,
        pesoInicialH: null,
        pesoInicialM: null,
        unifH: null,
        unifM: null,
        mortCajaH: null,
        mortCajaM: null,
        raza: '',
        anoTablaGenetica: null,
        linea: '',
        tipoLinea: '',
        codigoGuiaGenetica: '',
        tecnico: '',
        avesEncasetadas: null,
        loteErp: '',
        lineaGenetica: ''
      });
      this.nucleosFiltrados = this.filteredNucleos = [];
      this.galponesFiltrados = this.filteredGalpones = [];
      this.form.patchValue({ esLotePadre: false, lotePadreId: null });
      this.esLotePadre = false;
      this.selectedGranjaPadreId = null;
      this.selectedNucleoPadreId = null;
      this.selectedGalponPadreId = null;
    }
  }

  openModal(l?: LoteDto): void {
    this.editing = l ?? null;
    this.modalOpen = true;
    this.selectedBaseLote = null;
    this.form.patchValue({ lotePosturaBaseId: null }, { emitEvent: false });
    this.loadBaseLotesOptions();
    this.loadModalData();
  }

  private loadBaseLotesOptions(): void {
    this.lotePosturaBaseSvc.getAll().subscribe({
      next: (items) => {
        this.baseLotesOptions = items ?? [];
        // Si estamos editando y ya hay base asociada, reflejarla en UI
        const currentBaseId = this.form.get('lotePosturaBaseId')?.value;
        const id = currentBaseId != null ? Number(currentBaseId) : null;
        if (id) {
          this.selectedBaseLote = this.baseLotesOptions.find(b => b.lotePosturaBaseId === id) ?? null;
        }
      },
      error: () => this.baseLotesOptions = []
    });
  }

  onBaseLoteChange(baseId: number | null): void {
    const id = baseId != null ? Number(baseId) : null;
    if (!id) {
      this.selectedBaseLote = null;
      this.form.patchValue({ lotePosturaBaseId: null }, { emitEvent: false });
      return;
    }

    const base = this.baseLotesOptions.find(b => b.lotePosturaBaseId === id) ?? null;
    this.selectedBaseLote = base;
    this.form.patchValue({ lotePosturaBaseId: id }, { emitEvent: false });
    if (!base) return;

    const generatedName = this.generateNextLoteNameFromBase(base.loteNombre);
    this.form.patchValue({ loteNombre: generatedName }, { emitEvent: false });
  }

  private generateNextLoteNameFromBase(baseName: string): string {
    const cleanBase = (baseName ?? '').toString().trim();
    if (!cleanBase) return '';

    const taken = new Set<string>();
    for (const l of this.lotes ?? []) {
      const name = (l.loteNombre ?? '').toString().trim();
      if (!name) continue;
      if (!name.startsWith(cleanBase)) continue;

      // Formato esperado: K324A, K324B... (sin espacios)
      const suffix = name.substring(cleanBase.length).trim();
      if (!suffix) {
        taken.add(''); // base sin sufijo
        continue;
      }
      const letter = suffix.substring(0, 1).toUpperCase();
      if (/^[A-Z]$/.test(letter)) taken.add(letter);
    }

    const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'.split('');
    const next = alphabet.find(ch => !taken.has(ch));
    if (next) return `${cleanBase}${next}`;

    // fallback si ya se usaron A-Z
    let i = 1;
    while (taken.has(String(i))) i++;
    return `${cleanBase}${i}`;
  }

  openBaseModal(): void {
    this.baseForm.reset({
      loteNombre: '',
      codigoErp: '',
      cantidadHembras: 0,
      cantidadMachos: 0,
      cantidadMixtas: 0,
    });
    this.baseModalOpen = true;
  }

  saveBase(): void {
    if (this.baseForm.invalid) return;
    const v = this.baseForm.value;
    const dto: CreateLotePosturaBaseDto = {
      loteNombre: (v.loteNombre ?? '').toString().trim(),
      codigoErp: (v.codigoErp ?? '').toString().trim() || null,
      cantidadHembras: Number(v.cantidadHembras) || 0,
      cantidadMachos: Number(v.cantidadMachos) || 0,
      cantidadMixtas: Number(v.cantidadMixtas) || 0,
    };
    this.loading = true;
    this.lotePosturaBaseSvc.create(dto).pipe(finalize(() => this.loading = false)).subscribe({
      next: () => {
        this.baseModalOpen = false;
        this.toastService.success('Lote base registrado correctamente.', 'Listo');
        this.loadLotesPosturaBase();
      },
      error: (err) => {
        const msg = err?.error?.message || err?.message || 'Error al guardar el lote base.';
        this.toastService.error(msg, 'Error');
      }
    });
  }

  onEsLotePadreChange(event: Event): void {
    const target = event.target as HTMLInputElement;
    const checked = target?.checked ?? false;
    this.esLotePadre = checked;
    this.form.patchValue({ esLotePadre: checked });

    this.modalOpen = true;
  }

  // Métodos para lote padre
  cargarLotesPadres(): void {
    if (this.esLotePadre) {
      this.lotesPadresDisponibles = [];
      this.filteredLotesPadres = [];
      this.loadingLotesPadres = false;
      return;
    }
    this.loadingLotesPadres = false;
    this.lotesPadresDisponibles = this.lotes.filter(l =>
      !l.lotePadreId && l.loteId !== this.editing?.loteId
    );
    this.filteredLotesPadres = [...this.lotesPadresDisponibles];
  }

  onGranjaPadreChange(granjaId: number | null): void {
    this.selectedGranjaPadreId = granjaId;
    this.selectedNucleoPadreId = null;
    this.selectedGalponPadreId = null;
    this.form.patchValue({ lotePadreId: null });
    this.filtrarLotesPadres();
  }

  onNucleoPadreChange(nucleoId: string | null): void {
    this.selectedNucleoPadreId = nucleoId;
    this.selectedGalponPadreId = null;
    this.form.patchValue({ lotePadreId: null });
    this.filtrarLotesPadres();
  }

  onGalponPadreChange(galponId: string | null): void {
    this.selectedGalponPadreId = galponId;
    this.form.patchValue({ lotePadreId: null });
    this.filtrarLotesPadres();
  }

  filtrarLotesPadres(): void {
    let filtrados = [...this.lotesPadresDisponibles];

    if (this.selectedGranjaPadreId) {
      filtrados = filtrados.filter(l => l.granjaId === this.selectedGranjaPadreId);
    }

    if (this.selectedNucleoPadreId) {
      filtrados = filtrados.filter(l => l.nucleoId === this.selectedNucleoPadreId);
    }

    if (this.selectedGalponPadreId) {
      filtrados = filtrados.filter(l => l.galponId === this.selectedGalponPadreId);
    }

    this.filteredLotesPadres = filtrados;
  }

  save(): void {
    if (this.form.invalid) return;

    const raw = this.form.value;
    const formValue = this.form.value;
    const dto: CreateLoteDto | UpdateLoteDto = {
      ...raw,
      // Para creación: no enviar loteId (la base de datos lo generará automáticamente)
      // Para edición: enviar el loteId existente
      loteId: this.editing ? raw.loteId : undefined,
      lotePosturaBaseId: raw.lotePosturaBaseId ? Number(raw.lotePosturaBaseId) : null,
      granjaId: Number(raw.granjaId),
      nucleoId: raw.nucleoId ? String(raw.nucleoId) : undefined,
      galponId: raw.galponId ? String(raw.galponId) : undefined,
      fechaEncaset: raw.fechaEncaset
        ? new Date(raw.fechaEncaset + 'T00:00:00Z').toISOString()
        : undefined,
      // Si es sublote (esLotePadre = true), enviar el lotePadreId, sino null
      lotePadreId: formValue.esLotePadre ? (formValue.lotePadreId ? Number(formValue.lotePadreId) : null) : null
    };

    const op$ = this.editing
      ? this.loteSvc.update(dto as UpdateLoteDto)
      : this.loteSvc.create(dto as CreateLoteDto);

    this.loading = true;
    op$.pipe(finalize(() => { this.loading = false; })).subscribe({
      next: () => {
        this.modalOpen = false;
        this.toastService.success(
          this.editing ? 'Lote actualizado correctamente.' : 'Lote registrado correctamente.',
          'Listo'
        );
        this.loadData();
      },
      error: (err) => {
        const msg = err?.error?.message || err?.message || 'Error al guardar el lote.';
        this.toastService.error(msg, 'Error');
      }
    });
  }

  delete(l: LoteDto): void {
    this.pendingDelete = l;
    this.confirmData = {
      title: 'Eliminar lote',
      message: `¿Eliminar el lote "${l.loteNombre}"? Esta acción no se puede deshacer.`,
      confirmText: 'Eliminar',
      cancelText: 'Cancelar',
      type: 'warning',
      showCancel: true,
    };
    this.confirmOpen = true;
  }

  onConfirmDelete(): void {
    const l = this.pendingDelete;
    if (l == null) {
      this.confirmOpen = false;
      return;
    }
    this.confirmOpen = false;
    this.pendingDelete = null;
    this.loading = true;
    this.loteSvc.delete(l.loteId).pipe(finalize(() => this.loading = false)).subscribe({
      next: () => {
        this.toastService.success('Lote eliminado correctamente.', 'Listo');
        this.loadData();
      },
      error: (err) => {
        const msg = err?.error?.message || err?.message || 'Error al eliminar el lote.';
        this.toastService.error(msg, 'Error');
      }
    });
  }

  onCancelConfirm(): void {
    this.confirmOpen = false;
    this.pendingDelete = null;
  }

  // ===================== Helpers ===========================
  get selectedFarmName(): string {
    if (!this.selectedFarmId) return '';
    const opt = this.filterFarmOptions.find(f => f.id === this.selectedFarmId!);
    return opt ? opt.name : this.farmMap[this.selectedFarmId] || '';
  }

  calcularEdadDias(fechaEncaset?: string | Date | null): number {
    if (!fechaEncaset) return 0;
    const inicio = new Date(fechaEncaset);
    const hoy = new Date();
    const msDia = 1000 * 60 * 60 * 24;
    return Math.floor((hoy.getTime() - inicio.getTime()) / msDia) + 1;
  }

  /** Edad en semanas desde fechaEncaset. Día 0 = semana 1, días 7-13 = semana 2, etc. */
  calcularEdadSemanas(fechaEncaset?: string | Date | null): number {
    if (!fechaEncaset) return 1;
    const inicio = new Date(fechaEncaset);
    const hoy = new Date();
    const msSem = 1000 * 60 * 60 * 24 * 7;
    const semanas = Math.floor((hoy.getTime() - inicio.getTime()) / msSem);
    return Math.max(1, semanas + 1); // primera semana = 1, no 0
  }

  calcularFase(fechaEncaset?: string | Date | null): 'Levante' | 'Producción' | 'Desconocido' {
    if (!fechaEncaset) return 'Desconocido';
    return this.calcularEdadSemanas(fechaEncaset) < 26 ? 'Levante' : 'Producción';
  }

  formatNumber(value: number | null | undefined): string {
    if (value == null) return '0';
    return value.toLocaleString('es-CO', { minimumFractionDigits: 0, maximumFractionDigits: 0 });
  }
  formatOrDash(val?: number | null): string {
    return (val === null || val === undefined) ? '—' : this.formatNumber(val);
  }

  /** Normaliza estadoCierre para Levante (Abierto/Cerrado). Comparación insensible a mayúsculas. */
  estadoCierreLevante(l: LotePosturaLevanteDto): 'Abierto' | 'Cerrado' {
    const v = (l.estadoCierre ?? 'Abierto').toString().trim().toLowerCase();
    return v === 'cerrado' ? 'Cerrado' : 'Abierto';
  }

  /** Normaliza estadoCierre para Producción (Abierto/Cerrado). Comparación insensible a mayúsculas. */
  estadoCierreProduccion(l: LotePosturaProduccionDto): 'Abierto' | 'Cerrado' {
    const v = (l.estadoCierre ?? 'Abierta').toString().trim().toLowerCase();
    return v === 'cerrada' ? 'Cerrado' : 'Abierto';
  }

  // *** Helpers de vivas (faltaban y causaban el error del template) ***
  vivasH(l: LoteDto): number | null {
    const r = this.resumenMap[l.loteId];
    return r ? r.saldoHembras : null;
  }
  vivasM(l: LoteDto): number | null {
    const r = this.resumenMap[l.loteId];
    return r ? r.saldoMachos : null;
  }
  vivasTotales(l: LoteDto): number | null {
    const r = this.resumenMap[l.loteId];
    return r ? (r.saldoHembras + r.saldoMachos) : null;
  }
  // **********************************************************

  getFarmName(id: number): string { return this.farmMap[id] || '–'; }
  getNucleoName(id?: string | null): string { return id ? (this.nucleoMap[id] || '–') : '–'; }
  getGalponName(id?: string | null): string { return id ? (this.galponMap[id] || '–') : '–'; }

  actualizarEncasetadas(): void {
    const h = +this.form.get('hembrasL')?.value || 0;
    const m = +this.form.get('machosL')?.value || 0;
    this.form.get('avesEncasetadas')?.setValue(h + m);
  }

  onGranjaChange(granjaId: number): void {
    this.nucleosFiltrados = this.nucleos.filter(n => n.granjaId === Number(granjaId));
    this.filteredNucleos  = this.nucleosFiltrados;
    this.form.get('nucleoId')?.setValue(null);
    this.galponesFiltrados = this.filteredGalpones = [];
    this.form.get('galponId')?.setValue(null);
  }

  onNucleoChange(nucleoId: string): void {
    const granjaId = this.form.get('granjaId')?.value;
    if (granjaId && nucleoId) {
      this.galponSvc.getByGranjaAndNucleo(Number(granjaId), nucleoId).subscribe(data => {
        this.galponesFiltrados = this.filteredGalpones = data;
        this.form.get('galponId')?.setValue(null);
      });
    } else {
      this.galponesFiltrados = this.filteredGalpones = [];
    }
  }

  private normalize(s: string): string {
    return (s || '')
      .toLowerCase()
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '');
  }

  trackByLote = (_: number, l: LoteDto) => l.loteId;
  trackByLoteLevante = (_: number, l: LotePosturaLevanteDto) => l.lotePosturaLevanteId;
  trackByLoteProduccion = (_: number, l: LotePosturaProduccionDto) => l.lotePosturaProduccionId;

  // ===================== MÉTODOS DE TRASLADO =====================
  
  openTrasladoModal(lote: LoteDto): void {
    if (lote.estadoTraslado === 'trasladado') {
      this.toastService.warning('Este lote ya ha sido trasladado anteriormente.', 'Lote ya Trasladado');
      return;
    }
    this.loteParaTrasladar = lote;
    if (this.farms.length > 0 && this.nucleos.length > 0) {
      this.modalTrasladoOpen = true;
    } else {
      this.loadingTraslado = true;
      forkJoin({
        farms: this.farmSvc.getAll(),
        nucleos: this.nucleoSvc.getAll(),
      }).pipe(finalize(() => this.loadingTraslado = false)).subscribe({
        next: ({ farms, nucleos }) => {
          this.farms = farms;
          this.nucleos = nucleos;
          this.modalTrasladoOpen = true;
        },
        error: () => this.toastService.error('No se pudieron cargar granjas y núcleos.', 'Error')
      });
    }
  }

  closeTrasladoModal(): void {
    this.modalTrasladoOpen = false;
    this.loteParaTrasladar = null;
  }

  onConfirmarTraslado(data: {
    loteId: number;
    granjaDestinoId: number;
    nucleoDestinoId?: string | null;
    galponDestinoId?: string | null;
    observaciones?: string | null;
  }): void {
    const request: TrasladoLoteRequest = {
      loteId: data.loteId,
      granjaDestinoId: data.granjaDestinoId,
      nucleoDestinoId: data.nucleoDestinoId || null,
      galponDestinoId: data.galponDestinoId || null,
      observaciones: data.observaciones || null
    };

    this.loadingTraslado = true;
    this.loteSvc.trasladarLote(request).subscribe({
      next: (response: TrasladoLoteResponse) => {
        this.loadingTraslado = false;
        if (response.success) {
          this.toastService.success(response.message, 'Traslado Exitoso', 5000);
          this.closeTrasladoModal();
          this.loadData(); // Recargar la lista de lotes
        } else {
          this.toastService.error(response.message, 'Error en Traslado');
        }
      },
      error: (error) => {
        this.loadingTraslado = false;
        console.error('Error al trasladar lote:', error);
        const errorMessage = error?.error?.message || error?.message || 'Error desconocido al trasladar el lote';
        this.toastService.error(errorMessage, 'Error en Traslado');
      }
    });
  }

  // ===================== Métodos para años de tabla genética =====================
  
  private loadAnosDisponibles(raza: string): void {
    console.log('=== LoteList: loadAnosDisponibles() ===');
    console.log('Raza seleccionada:', raza);
    
    if (!raza || raza.trim() === '') {
      console.log('Raza vacía, limpiando años');
      this.anosDisponibles = [];
      this.loadingAnos = false;
      return;
    }

    this.loadingAnos = true;
    this.razaValida = true;
    
    console.log('Llamando al servicio obtenerInformacionRaza...');
    this.guiaGeneticaSvc.obtenerInformacionRaza(raza).subscribe({
      next: (info) => {
        console.log('✅ Respuesta del servicio:', info);
        this.anosDisponibles = info.anosDisponibles;
        this.razaValida = info.esValida;
        this.loadingAnos = false;
        
        console.log('Años disponibles:', this.anosDisponibles);
        
        if (!info.esValida) {
          console.warn(`No se encontraron años disponibles para la raza: ${raza}`);
        }
      },
      error: (error: any) => {
        console.error('❌ Error cargando años disponibles:', error);
        this.anosDisponibles = [];
        this.razaValida = false;
        this.loadingAnos = false;
      }
    });
  }

  // Por si llegan decimales desde otra fuente
  formatearnumeroEntero(controlName: string): void {
    const valor = this.form.get(controlName)?.value;
    if (valor != null && valor !== '') {
      const valorEntero = Math.round(Number(valor));
      this.form.get(controlName)?.setValue(valorEntero);
    } else {
      this.form.get(controlName)?.setValue(null);
    }
  }

}
