// src/app/features/config/farm-management/farm-management.component.ts
import { Component, OnInit, ChangeDetectionStrategy, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  FormArray,
  Validators
} from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Subject, takeUntil, finalize } from 'rxjs';
import { SidebarComponent } from '../../../shared/components/sidebar/sidebar.component';
import { FontAwesomeModule, FaIconLibrary } from '@fortawesome/angular-fontawesome';
import {
  faTractor,
  faPlus,
  faTrash,
  faPen,
  faEye,
  faTimes
} from '@fortawesome/free-solid-svg-icons';
import { CompanyService } from '../../../core/services/company/company.service';
import { ActiveCompanyService } from '../../../core/auth/active-company.service';
import { CompanySelectorComponent } from '../../../shared/components/company-selector/company-selector.component';
import { CompanyTestComponent } from './company-test.component';
import { CompanyAdminTestComponent } from '../../test/company-admin-test/company-admin-test.component';
import { environment } from '../../../../environments/environment';

interface House {
  id: number;
  name: string;
}
interface Nucleus {
  id: number;
  name: string;
  houses: House[];
}
interface Farm {
  id?: number;
  name: string;
  companyId: number | null;
  address: string;
  nuclei: Nucleus[];
}

@Component({
  selector: 'app-farm-management',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    SidebarComponent,
    FontAwesomeModule,
    CompanySelectorComponent,
    CompanyTestComponent,
    CompanyAdminTestComponent
  ],
  templateUrl: './farm-management.component.html',
  styleUrls: ['./farm-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FarmManagementComponent implements OnInit, OnDestroy {
  // Icons (template los usa directamente)
  faTractor = faTractor;
  faPlus    = faPlus;
  faTrash   = faTrash;
  faPen     = faPen;
  faEye     = faEye;
  faTimes   = faTimes;

  // Loading para overlays/toast
  loading = false;
  loadingCompanies = false;

  // Empresas cargadas desde el servicio
  companies: any[] = [];
  companyMap: Record<number, string> = {};
  
  // Empresa activa
  activeCompany: string | null = null;

  farms: Farm[] = [
    {
      id: 1,
      name: 'Granja Central',
      companyId: 1,
      address: 'Calle 123',
      nuclei: [
        { id: 11, name: 'Núcleo A', houses: [{ id: 111, name: 'Galpón 1' }] }
      ]
    },
    {
      id: 2,
      name: 'Estela',
      companyId: 2,
      address: 'Calle 143-43',
      nuclei: [
        { id: 21, name: 'Núcleo A', houses: [{ id: 2, name: 'Galpón 4' }, { id: 5, name: 'Galpón 3' }] },
        { id: 22, name: 'Núcleo D', houses: [{ id: 1, name: 'Galpón 1' }, { id: 3, name: 'Galpón 9' }] }
      ]
    },
    {
      id: 3,
      name: 'San Pedro',
      companyId: 1,
      address: 'Calle 45-543',
      nuclei: [
        { id: 31, name: 'Núcleo B', houses: [{ id: 311, name: 'Galpón 1' }, { id: 312, name: 'Galpón 2' }] },
        { id: 32, name: 'Núcleo C', houses: [{ id: 321, name: 'Galpón 3' }, { id: 322, name: 'Galpón 4' }] }
      ]
    }
  ];

  // Formulario y modales
  form!: FormGroup;
  modalOpen   = false;
  detailOpen  = false;
  editing     = false;
  selectedFarm: Farm | null = null;

  private nextId = 1; // se recalcula en ngOnInit
  private destroy$ = new Subject<void>();

  constructor(
    private fb: FormBuilder,
    private http: HttpClient,
    private companyService: CompanyService,
    private activeCompanyService: ActiveCompanyService,
    library: FaIconLibrary
  ) {
    library.addIcons(faTractor, faPlus, faTrash, faPen, faEye, faTimes);
  }

  ngOnInit() {
    this.loadCompanies();
    this.setupActiveCompanySubscription();

    // Calcular siguiente ID disponible
    const maxId = Math.max(0, ...this.farms.map(f => f.id || 0));
    this.nextId = maxId + 1;

    // Construir formulario
    this.form = this.fb.group({
      id:        [null],
      name:      ['', Validators.required],
      companyId: [null, Validators.required],
      address:   [''],
      nuclei:    this.fb.array([])
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ===== Carga de empresas =====
  private loadCompanies(): void {
    this.loadingCompanies = true;
    this.companyService.getAll()
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loadingCompanies = false)
      )
      .subscribe({
        next: (companies) => {
          this.companies = companies;
          this.updateCompanyMap();
          console.log('Empresas cargadas en farm-management:', companies);
        },
        error: (error) => {
          console.error('Error cargando empresas:', error);
          // Fallback a datos demo si falla la carga
          this.companies = [
            { id: 1, name: 'Agroavicola San Marino' },
            { id: 2, name: 'Piko' }
          ];
          this.updateCompanyMap();
        }
      });
  }

  private updateCompanyMap(): void {
    this.companyMap = this.companies.reduce((m, c) => {
      m[c.id] = c.name;
      return m;
    }, {} as Record<number, string>);
  }

  private setupActiveCompanySubscription(): void {
    this.activeCompanyService.activeCompany$
      .pipe(takeUntil(this.destroy$))
      .subscribe(company => {
        this.activeCompany = company;
        console.log('Empresa activa en farm-management:', company);
        // Recargar empresas cuando cambie la empresa activa
        this.loadCompanies();
      });
  }

  onCompanyChanged(companyName: string): void {
    console.log('Empresa cambiada en farm-management:', companyName);
    // Las empresas se recargarán automáticamente por la suscripción
  }

  // ===== Helpers Form =====
  get nucleiFA(): FormArray {
    return this.form.get('nuclei') as FormArray;
  }

  housesFA(nucIndex: number): FormArray {
    return this.nucleiFA.at(nucIndex).get('houses') as FormArray;
  }

  // ===== Núcleos =====
  newNucleus() {
    this.nucleiFA.push(
      this.fb.group({
        id:     [Date.now()],
        name:   ['', Validators.required],
        houses: this.fb.array([])
      })
    );
  }

  removeNucleus(i: number) {
    this.nucleiFA.removeAt(i);
  }

  // ===== Galpones =====
  addHouse(nucIndex: number) {
    this.housesFA(nucIndex).push(
      this.fb.group({
        id:   [Date.now()],
        name: ['', Validators.required]
      })
    );
  }

  removeHouse(nucIndex: number, houseIndex: number) {
    this.housesFA(nucIndex).removeAt(houseIndex);
  }

  // ===== Modal Crear/Editar =====
  openModal(farm?: Farm) {
    this.editing = !!farm;
    this.modalOpen = true;
    this.form.reset({ id: null, name: '', companyId: null, address: '' });
    this.nucleiFA.clear();

    if (farm) {
      // Patch de campos simples
      this.form.patchValue({
        id:        farm.id ?? null,
        name:      farm.name ?? '',
        companyId: farm.companyId ?? null,
        address:   farm.address ?? ''
      });

      // Reconstruir núcleos + galpones
      (farm.nuclei || []).forEach(n => {
        const housesFA = this.fb.array(
          (n.houses || []).map(h =>
            this.fb.group({
              id:   [h.id],
              name: [h.name, Validators.required]
            })
          )
        );

        this.nucleiFA.push(
          this.fb.group({
            id:     [n.id],
            name:   [n.name, Validators.required],
            houses: housesFA
          })
        );
      });
    }
  }

  closeModal() {
    this.modalOpen = false;
  }

  // ===== Guardar =====
  save() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading = true;

    const farm: Farm = this.form.getRawValue();

    if (this.editing && farm.id != null) {
      // Update inmutable para OnPush
      this.farms = this.farms.map(f => (f.id === farm.id ? { ...farm } : f));
    } else {
      farm.id = this.nextId++;
      this.farms = [...this.farms, { ...farm }];
    }

    this.loading = false;
    this.closeModal();
  }

  // ===== Eliminar =====
  deleteFarm(id: number) {
    if (!confirm('¿Eliminar esta granja?')) return;
    this.loading = true;
    this.farms = this.farms.filter(f => f.id !== id);
    this.loading = false;
  }

  // ===== Detalle (solo lectura) =====
  openDetail(farm: Farm) {
    this.selectedFarm = farm;
    this.detailOpen = true;
  }

  closeDetail() {
    this.detailOpen = false;
    this.selectedFarm = null;
  }
}
