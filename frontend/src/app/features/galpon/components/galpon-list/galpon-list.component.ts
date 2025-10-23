// src/app/features/galpon/pages/galpon-list/galpon-list.component.ts
import { Component, OnInit,Input  } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule, FormBuilder, FormGroup, Validators, FormsModule
} from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';

import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faPlus, faPen, faTrash, faEye } from '@fortawesome/free-solid-svg-icons';

import { GalponService } from '../../services/galpon.service';
import { GalponDetailDto, CreateGalponDto, UpdateGalponDto } from '../../models/galpon.models';

import { NucleoService, NucleoDto } from '../../../nucleo/services/nucleo.service';
import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { MasterListService } from '../../../../core/services/master-list/master-list.service';
import { Company, CompanyService } from '../../../../core/services/company/company.service';

interface NucleoOption { id: string; label: string; granjaId: number; }

@Component({
  selector: 'app-galpon-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, SidebarComponent, FontAwesomeModule, FormsModule],
  templateUrl: './galpon-list.component.html',
  styleUrls: ['./galpon-list.component.scss']
})
export class GalponListComponent implements OnInit {
    @Input() embedded = false;
  // Iconos
  faPlus = faPlus; faPen = faPen; faTrash = faTrash; faEye = faEye;

  // Estado
  loading = false;
  filtro = '';

  // Datos base
  companies: Company[] = [];
  farms: FarmDto[] = [];
  allNucleos: NucleoDto[] = [];
  allGalpones: GalponDetailDto[] = [];

  // Vistas filtradas (para combos de la cabecera)
  farmsFilteredG: FarmDto[] = [];
  nucleosFilteredG: NucleoDto[] = [];

  // Resultado de tabla
  viewGalpones: GalponDetailDto[] = [];

  // Selecciones de filtros (cabecera)
  selectedCompanyId: number | null = null;
  selectedFarmId: number | null = null;
  selectedNucleoId: string | null = null;

  // Modal Crear/Editar
  modalOpen = false;
  form!: FormGroup;
  editing: GalponDetailDto | null = null;

  // Modal Detalle
  detailOpen = false;
  selectedDetail: GalponDetailDto | null = null;

  // Combos del modal
  nucleoOptions: NucleoOption[] = [];
  typegarponOptions: string[] = [];

  constructor(
    private fb: FormBuilder,
    private svc: GalponService,
    private nucleoSvc: NucleoService,
    private farmSvc: FarmService,
    private companySvc: CompanyService,
    private mlSvc: MasterListService
  ) {}

  // ======================
  // Init
  // ======================
  ngOnInit(): void {
    this.form = this.fb.group({
      galponId:     ['', Validators.required],
      galponNombre: ['', Validators.required],
      nucleoId:     ['', Validators.required],
      granjaId:     [null, Validators.required],
      ancho:        [''],
      largo:        [''],
      tipoGalpon:   ['']
    });

    // Carga maestros y datos
    this.loading = true;
    forkJoin({
      companies: this.companySvc.getAll(),
      farms:     this.farmSvc.getAll(),
      nucleos:   this.nucleoSvc.getAll(),
      galpones:  this.svc.getAll()
    })
    .pipe(finalize(() => this.loading = false))
    .subscribe(({ companies, farms, nucleos, galpones }) => {
      this.companies = companies ?? [];
      this.farms = farms ?? [];
      this.allNucleos = nucleos ?? [];
      this.allGalpones = galpones ?? [];

      // Opciones para el select de Núcleo en el modal
      const farmNameById = new Map(this.farms.map(f => [f.id, f.name]));
      this.nucleoOptions = this.allNucleos.map(n => ({
        id: n.nucleoId,
        granjaId: n.granjaId,
        label: `${n.nucleoNombre} (Granja ${farmNameById.get(n.granjaId) ?? '#' + n.granjaId})`
      }));

      // Filtros de cabecera (cascada)
      this.farmsFilteredG = [...this.farms];
      this.nucleosFilteredG = [...this.allNucleos];

      // Resultado inicial
      this.recomputeList();
    });

    // Master list (tipo de galpón)
    this.mlSvc.getByKey('type_galpon').subscribe({
      next: ml => this.typegarponOptions = ml?.options ?? [],
      error: () => this.typegarponOptions = []
    });

    // Autollenado de granjaId cuando cambia el núcleo en el modal
    this.form.get('nucleoId')!.valueChanges.subscribe((id: string) => {
      const sel = this.allNucleos.find(x => x.nucleoId === id);
      this.form.patchValue({ granjaId: sel?.granjaId ?? null }, { emitEvent: false });
    });
  }

  // ======================
  // Filtros de cabecera
  // ======================
  onCompanyChangeList(companyId: number | null): void {
    this.selectedCompanyId = companyId;
    // filtrar granjas por compañía
    this.farmsFilteredG = companyId == null
      ? [...this.farms]
      : this.farms.filter(f => f.companyId === companyId);

    // filtrar núcleos por granjas resultantes
    const farmIds = new Set(this.farmsFilteredG.map(f => f.id));
    this.nucleosFilteredG = [...this.allNucleos].filter(n => farmIds.has(n.granjaId));

    // reset cascada
    this.selectedFarmId = null;
    this.selectedNucleoId = null;

    this.recomputeList();
  }

  onFarmChangeList(farmId: number | null): void {
    this.selectedFarmId = farmId;

    // Si no hay granja seleccionada, tomar núcleos de todas las granjas filtradas por compañía
    if (farmId == null) {
      const farmIds = new Set(this.farmsFilteredG.map(f => f.id));
      this.nucleosFilteredG = this.allNucleos.filter(n => farmIds.has(n.granjaId));
    } else {
      this.nucleosFilteredG = this.allNucleos.filter(n => n.granjaId === farmId);
    }

    // reset núcleo
    this.selectedNucleoId = null;

    this.recomputeList();
  }

  onNucleoChangeList(nucleoId: string | null): void {
    this.selectedNucleoId = nucleoId;
    this.recomputeList();
  }

  resetListFilters(): void {
    this.selectedCompanyId = null;
    this.selectedFarmId = null;
    this.selectedNucleoId = null;
    this.filtro = '';

    this.farmsFilteredG = [...this.farms];
    this.nucleosFilteredG = [...this.allNucleos];

    this.recomputeList();
  }

  // Recalcular la tabla (viewGalpones)
  recomputeList(): void {
    const q = (this.filtro || '').toLowerCase().trim();

    this.viewGalpones = this.allGalpones.filter(g => {
      // Filtros de ids
      if (this.selectedCompanyId != null && g.company?.id !== this.selectedCompanyId) return false;
      if (this.selectedFarmId != null    && g.farm?.id    !== this.selectedFarmId)    return false;
      if (this.selectedNucleoId != null  && g.nucleoId    !== this.selectedNucleoId)  return false;

      // Búsqueda de texto
      if (!q) return true;
      const hay = [
        g.galponId ?? '',
        g.galponNombre ?? '',
        g.nucleo?.nucleoNombre ?? '',
        g.farm?.name ?? '',
        g.company?.name ?? '',
        g.tipoGalpon ?? ''
      ].join(' ').toLowerCase();
      return hay.includes(q);
    });
  }

  // ======================
  // Acciones Tabla
  // ======================
  showDetail(g: GalponDetailDto): void {
    this.selectedDetail = g;
    this.detailOpen = true;
  }

 delete(id: string): void {
  if (!confirm('¿Eliminar este galpón?')) return;
  this.loading = true;

  this.svc.delete(id)
    .pipe(finalize(() => this.loading = false))
    .subscribe({
      next: () => this.loadGalponesAgain(),
      error: (err) => console.error('No se pudo eliminar el galpón', err)
    });
}


  private loadGalponesAgain(): void {
    this.svc.getAll()
      .pipe(finalize(() => this.loading = false))
      .subscribe(list => {
        this.allGalpones = list ?? [];
        this.recomputeList();
      });
  }

  // ======================
  // Modal Crear/Editar
  // ======================
  openModal(g?: GalponDetailDto): void {
    this.editing = g ?? null;

    if (g) {
      // Modo edición: rellenar y bloquear ID
      this.form.reset({
        galponId:     g.galponId,
        galponNombre: g.galponNombre,
        nucleoId:     g.nucleoId,      // clave para guardar
        granjaId:     g.granjaId,
        ancho:        g.ancho ?? '',
        largo:        g.largo ?? '',
        tipoGalpon:   g.tipoGalpon ?? ''
      });
      this.form.get('galponId')?.disable();
    } else {
      // Modo creación: sugerir nuevo ID incremental simple
      const lastNum = this.allGalpones
        .map(x => parseInt(String(x.galponId || '').replace(/\D/g, ''), 10))
        .filter(n => !isNaN(n))
        .reduce((m, c) => Math.max(m, c), 0);
      const newId = `G${(lastNum + 1).toString().padStart(4, '0')}`;

      this.form.reset({
        galponId:     newId,
        galponNombre: '',
        nucleoId:     '',
        granjaId:     null,
        ancho:        '',
        largo:        '',
        tipoGalpon:   ''
      });
      this.form.get('galponId')?.enable();
    }

    this.modalOpen = true;
  }

  save(): void {
    if (this.form.invalid) return;

    const raw = this.form.getRawValue(); // incluye galponId si estaba disabled
    const payload: CreateGalponDto | UpdateGalponDto = {
      galponId:     raw.galponId,
      galponNombre: raw.galponNombre,
      nucleoId:     raw.nucleoId,
      granjaId:     raw.granjaId,
      ancho:        raw.ancho || null,
      largo:        raw.largo || null,
      tipoGalpon:   raw.tipoGalpon || null
    };

    this.loading = true;
    const call$ = this.editing
      ? this.svc.update(payload as UpdateGalponDto)
      : this.svc.create(payload as CreateGalponDto);

    call$
      .pipe(finalize(() => {
        this.loading = false;
        this.modalOpen = false;
        this.loadGalponesAgain();
      }))
      .subscribe();
  }

  // ======================
  // Helpers de vista
  // ======================
  getArea(g: GalponDetailDto | null): string {
    if (!g?.ancho || !g?.largo) return '–';
    const a = parseFloat(String(g.ancho));
    const l = parseFloat(String(g.largo));
    if (isNaN(a) || isNaN(l)) return '–';
    return (a * l).toFixed(2);
  }

  getGranjaNombreByNucleoId(nucleoId: string | null | undefined): string {
    if (!nucleoId) return '–';
    const n = this.allNucleos.find(x => x.nucleoId === nucleoId);
    const f = this.farms.find(y => y.id === n?.granjaId);
    return f?.name ?? '–';
  }
}
