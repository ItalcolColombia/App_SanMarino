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
import { faPlus, faPen, faTrash, faTimes, faEye, faArrowRight } from '@fortawesome/free-solid-svg-icons';
import { ModalTrasladoLoteComponent } from '../modal-traslado-lote/modal-traslado-lote.component';
import { FiltroSelectComponent } from '../../../lote-levante/pages/filtro-select/filtro-select.component';
import { ToastService } from '../../../../shared/services/toast.service';

import {
  LoteService, LoteDto, CreateLoteDto, UpdateLoteDto, LoteMortalidadResumenDto,
  TrasladoLoteRequest, TrasladoLoteResponse
} from '../../services/lote.service';
import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { NucleoService, NucleoDto } from '../../../nucleo/services/nucleo.service';
import { GalponService } from '../../../galpon/services/galpon.service';
import { GalponDetailDto } from '../../../galpon/models/galpon.models';
import { UserService, UserDto, User } from '../../../../core/services/user/user.service';
import { Company, CompanyService } from '../../../../core/services/company/company.service';
import { GuiaGeneticaService } from '../../services/guia-genetica.service';

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
    FiltroSelectComponent
  ],
  templateUrl: './lote-list.component.html',
  styleUrls: ['./lote-list.component.scss']
})
export class LoteListComponent implements OnInit {
  // Iconos
  faPlus = faPlus; faPen = faPen; faTrash = faTrash; faTimes = faTimes; faEye = faEye; faArrowRight = faArrowRight;

  // Estado UI
  loading = false;
  modalOpen = false;
  editing: LoteDto | null = null;
  selectedLote: LoteDto | null = null;
  
  // Modal de traslado
  modalTrasladoOpen = false;
  loteParaTrasladar: LoteDto | null = null;
  loadingTraslado = false;

  // Búsqueda y orden
  filtro = '';
  sortKey: 'edad' | 'fecha' = 'edad';
  sortDir: 'asc' | 'desc' = 'desc';

  // Form
  form!: FormGroup;

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

  // Lotes
  lotes: LoteDto[] = [];
  viewLotes: LoteDto[] = [];
  lotesReproductora: any[] = [];

  // Filtros en cascada (lista)
  selectedCompanyId: number | null = null;
  selectedFarmId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;

  // Control de visibilidad del filtro de compañía
  showCompanyFilter: boolean = false;

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

    forkJoin({
      farms:     this.farmSvc.getAll(),
      nucleos:   this.nucleoSvc.getAll(),
      galpones:  this.galponSvc.getAll(),
      tecnicos:  this.userSvc.getAll(),
      companies: this.companySvc.getAll(),
      razas:     this.guiaGeneticaSvc.getRazasDisponibles(),
    }).subscribe(({ farms, nucleos, galpones, tecnicos, companies, razas }) => {
      // Catálogos
      this.farms = farms;
      this.farmById = {};
      farms.forEach(f => { this.farmById[f.id] = f; this.farmMap[f.id] = f.name; });

      this.nucleos = nucleos;
      nucleos.forEach(n => this.nucleoMap[n.nucleoId] = n.nucleoNombre);

      this.galpones = galpones;
      galpones.forEach(g => this.galponMap[g.galponId] = g.galponNombre);

      this.tecnicos = tecnicos;
      tecnicos.forEach(u => {
        if (u.id) {
          this.techMap[u.id] = `${u.surName || ''} ${u.firstName}`;
        }
      });

      this.companies = companies;
      
      // Determinar si mostrar el filtro de compañía
      this.showCompanyFilter = companies.length > 1;
      
      // Si solo hay una compañía, seleccionarla automáticamente
      if (companies.length === 1 && companies[0].id) {
        this.selectedCompanyId = companies[0].id;
      }

      // Razas disponibles
      this.razasDisponibles = razas;

      // Lotes
      this.loadLotes();
    });

    // Chains del modal
    this.form.get('granjaId')!.valueChanges.subscribe(granjaId => {
      this.nucleosFiltrados = this.nucleos.filter(n => n.granjaId === Number(granjaId));
      this.filteredNucleos  = this.nucleosFiltrados;
      const primerNucleo = this.nucleosFiltrados[0]?.nucleoId ?? null;
      this.form.patchValue({ nucleoId: primerNucleo });

      this.galponesFiltrados = [];
      this.filteredGalpones  = [];
      this.form.get('galponId')?.setValue(null);
    });

    this.form.get('nucleoId')!.valueChanges.subscribe((nucleoId: string | null) => {
      const granjaId = Number(this.form.get('granjaId')!.value);
      if (granjaId && nucleoId) {
        this.galponSvc.getByGranjaAndNucleo(granjaId, nucleoId).subscribe(data => {
          this.galponesFiltrados = data;
          this.filteredGalpones  = data;
          const primerGalpon = this.galponesFiltrados[0]?.galponId ?? null;
          this.form.patchValue({ galponId: primerGalpon });
        });
      } else {
        this.galponesFiltrados = [];
        this.filteredGalpones  = [];
        this.form.get('galponId')?.setValue(null);
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

  // ===================== Carga ==============================
  private loadLotes(): void {
    this.loading = true;
    this.loteSvc.getAll()
      .pipe(finalize(() => this.loading = false))
      .subscribe(list => {
        this.lotes = list;
        this.recomputeList();

        const calls = list.map(l => this.loteSvc.getResumenMortalidad(l.loteId));
        if (calls.length) {
          forkJoin(calls).subscribe({
            next: (resumenes) => resumenes.forEach(r => { this.resumenMap[r.loteId] = r; }),
            error: () => {}
          });
        }
      });
  }

  // ===================== Filtros (lista) ====================
  get farmsFilteredL(): FarmDto[] {
    if (this.selectedCompanyId == null) return this.farms;
    return this.farms.filter(f => f.companyId === this.selectedCompanyId);
  }
  get nucleosFilteredL(): NucleoDto[] {
    if (this.selectedFarmId != null) return this.nucleos.filter(n => n.granjaId === this.selectedFarmId);
    if (this.selectedCompanyId != null) {
      const ids = new Set(this.farmsFilteredL.map(f => f.id));
      return this.nucleos.filter(n => ids.has(n.granjaId));
    }
    return this.nucleos;
  }
  get galponesFilteredL(): GalponDetailDto[] {
    let arr = this.galpones;
    if (this.selectedFarmId != null) {
      arr = arr.filter(g => g.granjaId === this.selectedFarmId);
    } else if (this.selectedCompanyId != null) {
      const ids = new Set(this.farmsFilteredL.map(f => f.id));
      arr = arr.filter(g => ids.has(g.granjaId));
    }
    if (this.selectedNucleoId != null) arr = arr.filter(g => g.nucleoId === this.selectedNucleoId);
    return arr;
  }

  onCompanyChangeList(val: number | null) {
    this.selectedCompanyId = val;
    if (this.selectedFarmId != null && !this.farmsFilteredL.some(f => f.id === this.selectedFarmId)) this.selectedFarmId = null;
    if (this.selectedNucleoId != null && !this.nucleosFilteredL.some(n => n.nucleoId === this.selectedNucleoId)) this.selectedNucleoId = null;
    if (this.selectedGalponId != null && !this.galponesFilteredL.some(g => g.galponId === this.selectedGalponId)) this.selectedGalponId = null;
    this.recomputeList();
  }
  onFarmChangeList(val: number | null) {
    this.selectedFarmId = val;
    if (this.selectedNucleoId != null && !this.nucleosFilteredL.some(n => n.nucleoId === this.selectedNucleoId)) this.selectedNucleoId = null;
    if (this.selectedGalponId != null && !this.galponesFilteredL.some(g => g.galponId === this.selectedGalponId)) this.selectedGalponId = null;
    this.recomputeList();
  }
  onNucleoChangeList(_val: string | null) {
    if (this.selectedGalponId != null && !this.galponesFilteredL.some(g => g.galponId === this.selectedGalponId)) this.selectedGalponId = null;
    this.recomputeList();
  }
  resetListFilters() {
    this.filtro = '';
    // No resetear selectedFarmId ya que es obligatorio
    this.selectedCompanyId = null;
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.recomputeList();
  }

  // ===================== Ordenamiento =======================
  onSortKeyChange(v: 'edad'|'fecha') { this.sortKey = v; this.recomputeList(); }
  onSortDirChange(v: 'asc'|'desc') { this.sortDir = v; this.recomputeList(); }

  // ===================== Recompute (filtros + orden) ========
  recomputeList() {
    // Si no hay granja seleccionada, no mostrar lotes
    if (!this.selectedFarmId) {
      this.viewLotes = [];
      return;
    }

    const term = this.normalize(this.filtro);
    let res = [...this.lotes];

    // Filtrar obligatoriamente por granja seleccionada
    res = res.filter(l => l.granjaId === this.selectedFarmId);

    // Filtros adicionales (opcionales)
    if (this.selectedCompanyId != null) res = res.filter(l => this.farmById[l.granjaId]?.companyId === this.selectedCompanyId);
    if (this.selectedNucleoId != null)  res = res.filter(l => (l.nucleoId ?? null) === this.selectedNucleoId);
    if (this.selectedGalponId != null)  res = res.filter(l => (l.galponId ?? null) === this.selectedGalponId);

    if (term) {
      res = res.filter(l => {
        const haystack = [
          l.loteId ?? 0,
          l.loteNombre ?? '',
          this.nucleoMap[l.nucleoId ?? ''] ?? '',
          this.farmMap[l.granjaId] ?? '',
          this.galponMap[l.galponId ?? ''] ?? ''
        ].map(s => this.normalize(String(s))).join(' ');
        return haystack.includes(term);
      });
    }

    res = this.sortLotes(res);
    this.viewLotes = res;
  }

  private sortLotes(arr: LoteDto[]): LoteDto[] {
    const val = (l: LoteDto): number | null => {
      if (!l.fechaEncaset) return null;
      if (this.sortKey === 'edad') return this.calcularEdadDias(l.fechaEncaset);
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

  // ===================== Acciones UI ========================
  openDetail(lote: LoteDto): void {
    this.selectedLote = lote;
    this.resumenSelected = null;

    this.loteSvc.getResumenMortalidad(lote.loteId).subscribe({
      next: (r) => this.resumenSelected = r,
      error: () => this.resumenSelected = null
    });

    this.loteSvc.getReproductorasByLote(lote.loteId).subscribe(r => {
      this.lotesReproductora = r;
    });
  }

  openModal(l?: LoteDto): void {
    this.editing = l ?? null;

    if (l) {
      this.form.patchValue({
        ...l,
        fechaEncaset: l.fechaEncaset
          ? new Date(l.fechaEncaset).toISOString().substring(0, 10)
          : null
      });

      this.nucleosFiltrados  = this.nucleos.filter(n => n.granjaId === l.granjaId);
      this.filteredNucleos   = this.nucleosFiltrados;
      this.galponesFiltrados = this.galpones.filter(g =>
        g.granjaId === l.granjaId && g.nucleoId === String(l.nucleoId ?? '')
      );
      this.filteredGalpones = this.galponesFiltrados;

      // Inicializar campos de sublote
      // Si tiene lotePadreId, es un sublote
      const esSublote = !!l.lotePadreId;
      this.esLotePadre = esSublote;
      this.form.patchValue({ 
        esLotePadre: esSublote,
        lotePadreId: l.lotePadreId || null
      });
      
      if (esSublote && l.lotePadreId) {
        // Si es sublote y tiene padre, cargar los filtros del lote padre
        this.loteSvc.getById(l.lotePadreId).subscribe({
          next: (lotePadre) => {
            this.selectedGranjaPadreId = lotePadre.granjaId;
            this.selectedNucleoPadreId = lotePadre.nucleoId || null;
            this.selectedGalponPadreId = lotePadre.galponId || null;
            this.cargarLotesPadres();
          }
        });
      } else {
        // Si no es sublote, limpiar filtros
        this.selectedGranjaPadreId = null;
        this.selectedNucleoPadreId = null;
        this.selectedGalponPadreId = null;
      }

    } else {
      // Para creación: no establecer loteId (la base de datos lo generará automáticamente)
      this.form.reset({
        loteId: null, // Vacío - auto-incremento numérico
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

      this.nucleosFiltrados  = this.filteredNucleos  = [];
      this.galponesFiltrados = this.filteredGalpones = [];
      
      // Inicializar campos de sublote para creación (por defecto no es sublote)
      this.form.patchValue({ esLotePadre: false, lotePadreId: null });
      this.esLotePadre = false;
      this.selectedGranjaPadreId = null;
      this.selectedNucleoPadreId = null;
      this.selectedGalponPadreId = null;
    }

    this.modalOpen = true;
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

    this.loadingLotesPadres = true;
    this.loteSvc.getAll().subscribe({
      next: (lotes) => {
        // Filtrar solo lotes que pueden ser padres (sin lotePadreId y no el lote actual)
        this.lotesPadresDisponibles = lotes.filter(l => 
          !l.lotePadreId && l.loteId !== this.editing?.loteId
        );
        this.filteredLotesPadres = [...this.lotesPadresDisponibles];
        this.loadingLotesPadres = false;
      },
      error: () => {
        this.loadingLotesPadres = false;
      }
    });
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
    op$.pipe(finalize(() => {
      this.loading = false;
      this.modalOpen = false;
      this.loadLotes();
    })).subscribe();
  }

  delete(l: LoteDto): void {
    if (!confirm(`¿Eliminar lote “${l.loteNombre}”?`)) return;
    this.loading = true;
    this.loteSvc.delete(l.loteId)
      .pipe(finalize(() => {
        this.loading = false;
        this.loadLotes();
      }))
      .subscribe();
  }

  // ===================== Helpers ===========================
  get selectedFarmName(): string {
    if (!this.selectedFarmId) return '';
    return this.farmMap[this.selectedFarmId] || '';
  }

  calcularEdadDias(fechaEncaset?: string | Date | null): number {
    if (!fechaEncaset) return 0;
    const inicio = new Date(fechaEncaset);
    const hoy = new Date();
    const msDia = 1000 * 60 * 60 * 24;
    return Math.floor((hoy.getTime() - inicio.getTime()) / msDia) + 1;
  }

  /** @deprecated Usar calcularEdadDias() en su lugar */
  calcularEdadSemanas(fechaEncaset?: string | Date | null): number {
    if (!fechaEncaset) return 0;
    const inicio = new Date(fechaEncaset);
    const hoy = new Date();
    const msSem = 1000 * 60 * 60 * 24 * 7;
    return Math.floor((hoy.getTime() - inicio.getTime()) / msSem) + 1;
  }

  calcularFase(fechaEncaset?: string | Date | null): 'Levante' | 'Producción' | 'Desconocido' {
    if (!fechaEncaset) return 'Desconocido';
    // Usar días en lugar de semanas: Levante < 175 días (25 semanas * 7 días)
    return this.calcularEdadDias(fechaEncaset) < 175 ? 'Levante' : 'Producción';
  }

  formatNumber(value: number | null | undefined): string {
    if (value == null) return '0';
    return value.toLocaleString('es-CO', { minimumFractionDigits: 0, maximumFractionDigits: 0 });
  }
  formatOrDash(val?: number | null): string {
    return (val === null || val === undefined) ? '—' : this.formatNumber(val);
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

  // ===================== MÉTODOS DE TRASLADO =====================
  
  openTrasladoModal(lote: LoteDto): void {
    // Validar que el lote no esté ya trasladado
    if (lote.estadoTraslado === 'trasladado') {
      this.toastService.warning('Este lote ya ha sido trasladado anteriormente.', 'Lote ya Trasladado');
      return;
    }

    this.loteParaTrasladar = lote;
    this.modalTrasladoOpen = true;
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
          // Mostrar mensaje de éxito con toast
          const mensaje = `${response.message}\n\n` +
            `Lote original: #${response.loteOriginalId} (Trasladado)\n` +
            `Nuevo lote: #${response.loteNuevoId} (En Transferencia)`;
          this.toastService.success(mensaje, 'Traslado Exitoso', 6000);
          this.closeTrasladoModal();
          this.loadLotes(); // Recargar la lista de lotes
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
