import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, FormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';

import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faPlus, faPen, faTrash, faTimes, faEye, faFilter } from '@fortawesome/free-solid-svg-icons';
import { ThousandSeparatorDirective } from '../../../lote/components/lote-list/lote-list.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { ConfirmationModalComponent, ConfirmationModalData } from '../../../../shared/components/confirmation-modal/confirmation-modal.component';

import {
  LoteEngordeService,
  LoteAveEngordeDto,
  CreateLoteAveEngordeDto,
  UpdateLoteAveEngordeDto,
  LoteFormDataResponse
} from '../../services/lote-engorde.service';
import { LoteReproductoraAveEngordeService, LoteReproductoraAveEngordeDto, AvesDisponiblesDto } from '../../../lote-reproductora-ave-engorde/services/lote-reproductora-ave-engorde.service';
import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { NucleoDto } from '../../../nucleo/services/nucleo.service';
import { GalponDetailDto } from '../../../galpon/models/galpon.models';
import { User } from '../../../../core/services/user/user.service';
import { GalponService } from '../../../galpon/services/galpon.service';
import { Company } from '../../../../core/services/company/company.service';
import { GuiaGeneticaService } from '../../../lote/services/guia-genetica.service';

@Component({
  selector: 'app-lote-engorde-list',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FontAwesomeModule,
    FormsModule,
    ThousandSeparatorDirective,
    ConfirmationModalComponent
  ],
  templateUrl: './lote-engorde-list.component.html',
  styleUrls: ['./lote-engorde-list.component.scss']
})
export class LoteEngordeListComponent implements OnInit {
  faPlus = faPlus;
  faPen = faPen;
  faTrash = faTrash;
  faTimes = faTimes;
  faEye = faEye;
  faFilter = faFilter;

  loading = false;
  modalOpen = false;
  editing: LoteAveEngordeDto | null = null;
  selectedLote: LoteAveEngordeDto | null = null;
  /** Lotes reproductora de pollo engorde del lote en detalle (estado Cerrado/Disponible) */
  lotesReproductora: LoteReproductoraAveEngordeDto[] = [];
  loadingReproductora = false;
  /** Aves disponibles (hembra/macho) después de reducciones y lotes reproductora creados */
  avesDisponibles: AvesDisponiblesDto | null = null;
  /** Cargando detalle del lote (getById) al abrir Ver detalle */
  loadingDetail = false;
  /** Lote reproductora seleccionado para ver su detalle (modal) */
  selectedReproductor: LoteReproductoraAveEngordeDto | null = null;

  filtro = '';
  sortKey: 'edad' | 'fecha' = 'edad';
  sortDir: 'asc' | 'desc' = 'desc';

  form!: FormGroup;

  farms: FarmDto[] = [];
  nucleos: NucleoDto[] = [];
  galpones: GalponDetailDto[] = [];
  tecnicos: User[] = [];
  razasDisponibles: string[] = [];
  anosDisponibles: number[] = [];
  selectedRaza = '';
  loadingAnos = false;
  razaValida = true;
  companies: Company[] = [];

  farmMap: Record<number, string> = {};
  nucleoMap: Record<string, string> = {};
  galponMap: Record<string, string> = {};
  techMap: Record<string, string> = {};
  private farmById: Record<number, FarmDto> = {};

  lotes: LoteAveEngordeDto[] = [];
  viewLotes: LoteAveEngordeDto[] = [];

  selectedCompanyId: number | null = null;
  selectedFarmId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  filterCompanyOptions: { id: number; label: string }[] = [];
  filterFarmOptions: { id: number; name: string }[] = [];
  filterNucleoOptions: { nucleoId: string; nucleoNombre: string }[] = [];
  filterGalponOptions: { galponId: string; galponNombre: string }[] = [];

  confirmOpen = false;
  confirmData: ConfirmationModalData = {
    title: 'Eliminar lote de engorde',
    message: 'Esta acción no se puede deshacer.',
    confirmText: 'Eliminar',
    cancelText: 'Cancelar',
    type: 'warning',
    showCancel: true,
  };
  pendingDelete: LoteAveEngordeDto | null = null;

  loadingModal = false;
  nucleosFiltrados: NucleoDto[] = [];
  galponesFiltrados: GalponDetailDto[] = [];
  filteredNucleos: NucleoDto[] = [];
  filteredGalpones: GalponDetailDto[] = [];

  constructor(
    private fb: FormBuilder,
    private loteEngordeSvc: LoteEngordeService,
    private loteReproductoraSvc: LoteReproductoraAveEngordeService,
    private farmSvc: FarmService,
    private galponSvc: GalponService,
    private guiaGeneticaSvc: GuiaGeneticaService,
    private toastService: ToastService
  ) {}

  ngOnInit(): void {
    this.initForm();
    this.loadLotes();

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
        this.form.patchValue({ galponId: this.galponesFiltrados[0]?.galponId ?? null }, { emitEvent: false });
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
          this.form.patchValue({ galponId: this.galponesFiltrados[0]?.galponId ?? null }, { emitEvent: false });
        } else {
          this.galponSvc.getByGranjaAndNucleo(granjaId, nucleoId).subscribe(data => {
            this.galponesFiltrados = [...data];
            this.filteredGalpones = this.galponesFiltrados;
            this.form.patchValue({ galponId: this.galponesFiltrados[0]?.galponId ?? null }, { emitEvent: false });
          });
        }
      } else {
        this.galponesFiltrados = [];
        this.filteredGalpones = [];
        this.form.get('galponId')?.setValue(null, { emitEvent: false });
      }
    });

    this.form.get('hembrasL')!.valueChanges.subscribe(() => this.actualizarEncasetadas());
    this.form.get('machosL')!.valueChanges.subscribe(() => this.actualizarEncasetadas());

    this.form.get('raza')!.valueChanges.subscribe(raza => {
      this.selectedRaza = raza ?? '';
      this.anosDisponibles = [];
      this.form.patchValue({ anoTablaGenetica: null });
      if (raza) this.loadAnosDisponibles(raza);
    });
  }

  private initForm(): void {
    this.form = this.fb.group({
      loteAveEngordeId: [null],
      loteNombre: ['', Validators.required],
      granjaId: [null, Validators.required],
      nucleoId: [null],
      galponId: [null],
      regional: [''],
      fechaEncaset: [null, Validators.required],
      hembrasL: [null],
      machosL: [null],
      pesoInicialH: [null],
      pesoInicialM: [null],
      unifH: [null],
      unifM: [null],
      mortCajaH: [null],
      mortCajaM: [null],
      raza: ['', Validators.required],
      anoTablaGenetica: [null, Validators.required],
      linea: [''],
      tipoLinea: [''],
      codigoGuiaGenetica: [''],
      tecnico: [''],
      mixtas: [null],
      pesoMixto: [null],
      avesEncasetadas: [null],
      loteErp: [''],
    });
  }

  private loadLotes(): void {
    this.loading = true;
    this.loteEngordeSvc.getAll()
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: (list) => {
          this.lotes = list;
          this.buildFilterOptionsFromLotes();
          this.recomputeList();
        },
        error: () => this.toastService.error('No se pudo cargar la lista de lotes de engorde.', 'Error')
      });
  }

  private buildFilterOptionsFromLotes(): void {
    const companies = new Map<number, string>();
    const farms = new Map<number, string>();
    const nucleos = new Map<string, string>();
    const galpones = new Map<string, string>();
    for (const l of this.lotes) {
      if (l.companyId != null) companies.set(l.companyId, `Compañía ${l.companyId}`);
      if (l.farm?.id != null) farms.set(l.farm.id, l.farm.name ?? '');
      if (l.nucleo?.nucleoId) nucleos.set(l.nucleo.nucleoId, l.nucleo.nucleoNombre ?? l.nucleo.nucleoId);
      if (l.galpon?.galponId) galpones.set(l.galpon.galponId, l.galpon.galponNombre ?? l.galpon.galponId);
    }
    this.filterCompanyOptions = Array.from(companies.entries()).map(([id, label]) => ({ id, label }));
    this.filterFarmOptions = Array.from(farms.entries()).map(([id, name]) => ({ id, name }));
    this.filterNucleoOptions = Array.from(nucleos.entries()).map(([nucleoId, nucleoNombre]) => ({ nucleoId, nucleoNombre }));
    this.filterGalponOptions = Array.from(galpones.entries()).map(([galponId, galponNombre]) => ({ galponId, galponNombre }));
  }

  onCompanyChangeList(_val: number | null) { this.recomputeList(); }
  onFarmChangeList(_val: number | null) { this.recomputeList(); }
  onNucleoChangeList(_val: string | null) { this.recomputeList(); }
  resetListFilters(): void {
    this.filtro = '';
    this.selectedCompanyId = null;
    this.selectedFarmId = null;
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.recomputeList();
  }

  onSortKeyChange(v: 'edad' | 'fecha') { this.sortKey = v; this.recomputeList(); }
  onSortDirChange(v: 'asc' | 'desc') { this.sortDir = v; this.recomputeList(); }

  recomputeList(): void {
    const term = this.normalize(this.filtro);
    let res = [...this.lotes];
    if (this.selectedCompanyId != null) res = res.filter(l => (l.companyId ?? null) === this.selectedCompanyId);
    if (this.selectedFarmId != null) res = res.filter(l => l.granjaId === this.selectedFarmId);
    if (this.selectedNucleoId != null) res = res.filter(l => (l.nucleoId ?? null) === this.selectedNucleoId);
    if (this.selectedGalponId != null) res = res.filter(l => (l.galponId ?? null) === this.selectedGalponId);
    if (term) {
      res = res.filter(l => {
        const haystack = [
          l.loteAveEngordeId ?? 0,
          l.loteNombre ?? '',
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

  private sortLotes(arr: LoteAveEngordeDto[]): LoteAveEngordeDto[] {
    const val = (l: LoteAveEngordeDto): number | null => {
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

  private loadModalData(): void {
    this.loadingModal = true;
    this.loteEngordeSvc.getFormData()
      .pipe(finalize(() => this.loadingModal = false))
      .subscribe({
        next: (data) => this.applyFormDataResponse(data),
        error: () => this.toastService.error('No se pudieron cargar los datos del formulario.', 'Error')
      });
  }

  private applyFormDataResponse(data: LoteFormDataResponse): void {
    const raw = data as unknown as Record<string, unknown>;
    const farms = (raw['farms'] ?? raw['Farms'] ?? []) as FarmDto[];
    const nucleos = (raw['nucleos'] ?? raw['Nucleos'] ?? []) as NucleoDto[];
    const galpones = (raw['galpones'] ?? raw['Galpones'] ?? []) as GalponDetailDto[];
    const tecnicos = (raw['tecnicos'] ?? raw['Tecnicos'] ?? []) as User[];
    const companies = (raw['companies'] ?? raw['Companies'] ?? []) as Company[];
    const razas = (raw['razas'] ?? raw['Razas'] ?? []) as string[];

    this.farms = farms;
    this.farmById = {};
    this.farmMap = {};
    farms.forEach(f => { this.farmById[f.id] = f; this.farmMap[f.id] = f.name; });
    this.nucleos = nucleos;
    this.nucleoMap = {};
    nucleos.forEach(n => { this.nucleoMap[n.nucleoId] = n.nucleoNombre ?? n.nucleoId; });
    this.galpones = galpones;
    this.galponMap = {};
    galpones.forEach(g => { this.galponMap[g.galponId] = g.galponNombre ?? g.galponId; });
    this.tecnicos = tecnicos;
    this.techMap = {};
    tecnicos.forEach(u => { if (u.id) this.techMap[u.id] = `${u.surName ?? ''} ${u.firstName ?? ''}`.trim(); });
    this.companies = companies;
    this.razasDisponibles = Array.isArray(razas) ? [...razas] : [];

    const granjaId = this.editing?.granjaId ?? this.form.get('granjaId')?.value;
    const nucleoId = this.editing?.nucleoId ?? this.form.get('nucleoId')?.value;
    this.nucleosFiltrados = granjaId != null ? this.nucleos.filter(n => n.granjaId === Number(granjaId)) : [];
    this.filteredNucleos = this.nucleosFiltrados;
    this.galponesFiltrados = granjaId != null && nucleoId != null
      ? this.galpones.filter(g => g.granjaId === Number(granjaId) && g.nucleoId === String(nucleoId))
      : [];
    this.filteredGalpones = this.galponesFiltrados;
    this.applyModalFormState();
  }

  private applyModalFormState(): void {
    const l = this.editing;
    if (l) {
      this.selectedRaza = l.raza ?? '';
      this.form.patchValue({
        loteAveEngordeId: l.loteAveEngordeId,
        loteNombre: l.loteNombre ?? '',
        granjaId: l.granjaId,
        nucleoId: l.nucleoId ?? null,
        galponId: l.galponId ?? null,
        regional: l.regional ?? '',
        fechaEncaset: l.fechaEncaset ? new Date(l.fechaEncaset).toISOString().substring(0, 10) : null,
        hembrasL: l.hembrasL ?? null,
        machosL: l.machosL ?? null,
        pesoInicialH: l.pesoInicialH ?? null,
        pesoInicialM: l.pesoInicialM ?? null,
        unifH: l.unifH ?? null,
        unifM: l.unifM ?? null,
        mortCajaH: l.mortCajaH ?? null,
        mortCajaM: l.mortCajaM ?? null,
        raza: l.raza ?? '',
        anoTablaGenetica: l.anoTablaGenetica ?? null,
        linea: l.linea ?? '',
        tipoLinea: l.tipoLinea ?? '',
        codigoGuiaGenetica: l.codigoGuiaGenetica ?? '',
        tecnico: l.tecnico ?? '',
        mixtas: l.mixtas ?? null,
        pesoMixto: l.pesoMixto ?? null,
        avesEncasetadas: l.avesEncasetadas ?? null,
        loteErp: l.loteErp ?? '',
      });
      this.nucleosFiltrados = this.nucleos.filter(n => n.granjaId === l.granjaId);
      this.filteredNucleos = this.nucleosFiltrados;
      this.galponesFiltrados = this.galpones.filter(g => g.granjaId === l.granjaId && g.nucleoId === String(l.nucleoId ?? ''));
      this.filteredGalpones = this.galponesFiltrados;
      if (this.selectedRaza) this.loadAnosDisponibles(this.selectedRaza);
    } else {
      this.selectedRaza = '';
      this.anosDisponibles = [];
      this.form.reset({
        loteAveEngordeId: null,
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
        mixtas: null,
        pesoMixto: null,
        avesEncasetadas: null,
        loteErp: '',
      });
      this.nucleosFiltrados = this.filteredNucleos = [];
      this.galponesFiltrados = this.filteredGalpones = [];
    }
  }

  openModal(l?: LoteAveEngordeDto): void {
    this.editing = l ?? null;
    this.modalOpen = true;
    this.loadModalData();
  }

  openDetail(lote: LoteAveEngordeDto): void {
    const id = lote.loteAveEngordeId;
    if (id == null) return;
    this.selectedLote = null;
    this.lotesReproductora = [];
    this.avesDisponibles = null;
    this.selectedReproductor = null;
    this.loadingDetail = true;
    this.loteEngordeSvc.getById(id).pipe(finalize(() => this.loadingDetail = false)).subscribe({
      next: (detail) => {
        this.selectedLote = detail;
        this.loadingReproductora = true;
        this.loteReproductoraSvc.getAll(id).pipe(finalize(() => this.loadingReproductora = false)).subscribe({
          next: (list) => { this.lotesReproductora = [...(list ?? [])]; },
          error: () => { this.lotesReproductora = []; }
        });
        this.loteReproductoraSvc.getAvesDisponibles(id).subscribe({
          next: (aves) => { this.avesDisponibles = aves; },
          error: () => { this.avesDisponibles = null; }
        });
      },
      error: () => {
        this.toastService.error('No se pudo cargar el detalle del lote.', 'Error');
      }
    });
  }

  openReproductorDetail(r: LoteReproductoraAveEngordeDto): void {
    this.selectedReproductor = r;
  }

  closeReproductorDetail(): void {
    this.selectedReproductor = null;
  }

  save(): void {
    if (this.form.invalid) return;
    const raw = this.form.value;
    const toNum = (v: unknown): number | undefined =>
      v === null || v === undefined || v === '' ? undefined : Number(v);
    const dto: CreateLoteAveEngordeDto | UpdateLoteAveEngordeDto = {
      loteNombre: String(raw.loteNombre ?? '').trim(),
      granjaId: Number(raw.granjaId),
      nucleoId: raw.nucleoId != null && raw.nucleoId !== '' ? String(raw.nucleoId) : undefined,
      galponId: raw.galponId != null && raw.galponId !== '' ? String(raw.galponId) : undefined,
      regional: raw.regional != null && raw.regional !== '' ? String(raw.regional).trim() : undefined,
      fechaEncaset: raw.fechaEncaset ? new Date(raw.fechaEncaset + 'T00:00:00Z').toISOString() : undefined,
      hembrasL: toNum(raw.hembrasL),
      machosL: toNum(raw.machosL),
      pesoInicialH: toNum(raw.pesoInicialH),
      pesoInicialM: toNum(raw.pesoInicialM),
      unifH: toNum(raw.unifH),
      unifM: toNum(raw.unifM),
      mortCajaH: toNum(raw.mortCajaH),
      mortCajaM: toNum(raw.mortCajaM),
      raza: raw.raza != null && raw.raza !== '' ? String(raw.raza).trim() : undefined,
      anoTablaGenetica: toNum(raw.anoTablaGenetica),
      linea: raw.linea != null && raw.linea !== '' ? String(raw.linea).trim() : undefined,
      tipoLinea: raw.tipoLinea != null && raw.tipoLinea !== '' ? String(raw.tipoLinea).trim() : undefined,
      codigoGuiaGenetica: raw.codigoGuiaGenetica != null && raw.codigoGuiaGenetica !== '' ? String(raw.codigoGuiaGenetica).trim() : undefined,
      tecnico: raw.tecnico != null && raw.tecnico !== '' ? String(raw.tecnico).trim() : undefined,
      mixtas: toNum(raw.mixtas),
      pesoMixto: toNum(raw.pesoMixto),
      avesEncasetadas: toNum(raw.avesEncasetadas),
      loteErp: raw.loteErp != null && raw.loteErp !== '' ? String(raw.loteErp).trim() : undefined,
    } as CreateLoteAveEngordeDto | UpdateLoteAveEngordeDto;
    if (this.editing) {
      (dto as UpdateLoteAveEngordeDto).loteAveEngordeId = Number(raw.loteAveEngordeId) || this.editing.loteAveEngordeId;
    }
    const op$ = this.editing
      ? this.loteEngordeSvc.update(dto as UpdateLoteAveEngordeDto)
      : this.loteEngordeSvc.create(dto as CreateLoteAveEngordeDto);
    this.loading = true;
    op$.pipe(finalize(() => { this.loading = false; })).subscribe({
      next: () => {
        this.modalOpen = false;
        this.toastService.success(
          this.editing ? 'Lote de engorde actualizado correctamente.' : 'Lote de engorde registrado correctamente.',
          'Listo'
        );
        this.loadLotes();
      },
      error: (err) => {
        const msg = err?.error?.message || err?.message || 'Error al guardar el lote de engorde.';
        this.toastService.error(msg, 'Error');
      }
    });
  }

  delete(l: LoteAveEngordeDto): void {
    this.pendingDelete = l;
    this.confirmData = {
      ...this.confirmData,
      message: `¿Eliminar el lote de engorde "${l.loteNombre}"? Esta acción no se puede deshacer.`,
    };
    this.confirmOpen = true;
  }

  onConfirmDelete(): void {
    const l = this.pendingDelete;
    if (l == null) { this.confirmOpen = false; return; }
    this.confirmOpen = false;
    this.pendingDelete = null;
    this.loading = true;
    this.loteEngordeSvc.delete(l.loteAveEngordeId).pipe(finalize(() => this.loading = false)).subscribe({
      next: () => {
        this.toastService.success('Lote de engorde eliminado correctamente.', 'Listo');
        this.loadLotes();
      },
      error: (err) => {
        this.toastService.error(err?.error?.message || err?.message || 'Error al eliminar.', 'Error');
      }
    });
  }

  onCancelConfirm(): void {
    this.confirmOpen = false;
    this.pendingDelete = null;
  }

  get selectedFarmName(): string {
    if (!this.selectedFarmId) return '';
    const opt = this.filterFarmOptions.find(f => f.id === this.selectedFarmId!);
    return opt ? opt.name : this.farmMap[this.selectedFarmId] || '';
  }

  get activeFiltersCount(): number {
    let n = 0;
    if (this.selectedCompanyId != null) n++;
    if (this.selectedFarmId != null) n++;
    if (this.selectedNucleoId != null) n++;
    if (this.selectedGalponId != null) n++;
    if ((this.filtro || '').trim()) n++;
    return n;
  }

  calcularEdadDias(fechaEncaset?: string | Date | null): number {
    if (!fechaEncaset) return 0;
    const inicio = new Date(fechaEncaset);
    const hoy = new Date();
    return Math.floor((hoy.getTime() - inicio.getTime()) / (1000 * 60 * 60 * 24)) + 1;
  }

  formatNumber(value: number | null | undefined): string {
    if (value == null) return '0';
    return value.toLocaleString('es-CO', { minimumFractionDigits: 0, maximumFractionDigits: 0 });
  }
  formatOrDash(val?: number | null): string {
    return (val === null || val === undefined) ? '—' : this.formatNumber(val);
  }

  getFarmName(id: number): string { return this.farmMap[id] || '–'; }
  getNucleoName(id?: string | null): string { return id ? (this.nucleoMap[id] || '–') : '–'; }
  getTecnicoDisplayName(t: User): string {
    const name = `${t.surName ?? ''} ${t.firstName ?? ''}`.trim();
    return name || '–';
  }
  getGalponName(id?: string | null): string { return id ? (this.galponMap[id] || '–') : '–'; }

  actualizarEncasetadas(): void {
    const h = +this.form.get('hembrasL')?.value || 0;
    const m = +this.form.get('machosL')?.value || 0;
    this.form.get('avesEncasetadas')?.setValue(h + m);
  }

  private normalize(s: string): string {
    return (s || '').toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '');
  }

  trackByLote = (_: number, l: LoteAveEngordeDto) => l.loteAveEngordeId;

  private loadAnosDisponibles(raza: string): void {
    if (!raza?.trim()) { this.anosDisponibles = []; this.loadingAnos = false; return; }
    this.loadingAnos = true;
    this.razaValida = true;
    this.guiaGeneticaSvc.obtenerInformacionRaza(raza).subscribe({
      next: (info) => {
        this.anosDisponibles = info.anosDisponibles;
        this.razaValida = info.esValida;
        this.loadingAnos = false;
      },
      error: () => {
        this.anosDisponibles = [];
        this.razaValida = false;
        this.loadingAnos = false;
      }
    });
  }
}
