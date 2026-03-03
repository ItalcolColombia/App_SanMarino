// src/app/features/config/geography/city-list/city-list.component.ts
import {
  Component,
  OnInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators
} from '@angular/forms';
import { Observable, finalize } from 'rxjs';
import { FontAwesomeModule, FaIconLibrary } from '@fortawesome/angular-fontawesome';
import {
  faPlus,
  faPen,
  faTrash,
  faCheck,
  faTimes,
  faSave
} from '@fortawesome/free-solid-svg-icons';

import {
  CityService,
  CityDto,
  CreateCityDto,
  UpdateCityDto
} from '../../../../core/services/city/city.service';
import {
  DepartmentService,
  DepartamentoDto
} from '../../../../core/services/department/department.service';
import {
  CountryService,
  PaisDto
} from '../../../../core/services/country/country.service';

@Component({
  selector: 'app-city-list',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FontAwesomeModule, ],
  templateUrl: './city-list.component.html',
  styleUrls: ['./city-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CityListComponent implements OnInit {
  faPlus  = faPlus;
  faPen   = faPen;
  faTrash = faTrash;
  faCheck = faCheck;
  faTimes = faTimes;
  faSave  = faSave;

  countries: PaisDto[]                 = [];
  departments: DepartamentoDto[]       = [];
  filteredDepartments: DepartamentoDto[] = [];
  departmentMap: Record<number,string> = {};
  cities: CityDto[]                    = [];

  cityForm!: FormGroup;
  showModal = false;
  editingCity: CityDto | null = null;
  loading = false;

  constructor(
    library: FaIconLibrary,
    private fb: FormBuilder,
    private citySvc: CityService,
    private deptSvc: DepartmentService,
    private countrySvc: CountryService,
    private cdr: ChangeDetectorRef
  ) {
    library.addIcons(faPlus, faPen, faTrash, faCheck, faTimes, faSave);

    this.cityForm = this.fb.group({
      countryId:    [null, Validators.required],
      departmentId: [null, Validators.required],
      name:         ['', Validators.required],
      active:       [true]
    });

    // Cuando cambie país, filtramos departamentos y reseteamos el combo
    this.cityForm.get('countryId')!.valueChanges.subscribe((cid: number) => {
      this.filteredDepartments = this.departments.filter(d => d.paisId === cid);
      this.cityForm.get('departmentId')!.setValue(null);
    });
  }

  ngOnInit(): void {
    this.loadAll();
  }

  private loadAll(): void {
    this.loading = true;

    this.countrySvc.getAll().subscribe(list => {
      this.countries = list;
      this.cdr.markForCheck();
    });

    this.deptSvc.getAll().subscribe(list => {
      this.departments = list;
      this.departmentMap = {};
      list.forEach(d => this.departmentMap[d.departamentoId] = d.departamentoNombre);
      this.cdr.markForCheck();
    });

    this.citySvc.getAll()
      .pipe(finalize(() => {
        this.loading = false;
        this.cdr.markForCheck();
      }))
      .subscribe(list => {
        this.cities = list;
        this.cdr.markForCheck();
      });
  }

  newCity(): void {
    this.editingCity = null;
    this.cityForm.reset({ countryId: null, departmentId: null, name: '', active: true });
    this.showModal = true;
    this.cdr.markForCheck();
  }

  editCity(id: number): void {
    const city = this.cities.find(x => x.municipioId === id);
    if (!city) return;
    this.editingCity = city;

    const dept = this.departments.find(d => d.departamentoId === city.departamentoId)!;
    this.cityForm.setValue({
      countryId:    dept.paisId,
      departmentId: city.departamentoId,
      name:         city.municipioNombre,
      active:       true
    });

    this.showModal = true;
    this.cdr.markForCheck();
  }

  saveCity(): void {
    if (this.cityForm.invalid) {
      this.cityForm.markAllAsTouched();
      return;
    }

    const { departmentId, name, active } = this.cityForm.value as {
      departmentId: number;
      name: string;
      active: boolean;
    };

    let call$: Observable<any>;
    if (this.editingCity) {
      const dto: UpdateCityDto = {
        municipioId:     this.editingCity.municipioId,
        municipioNombre: name,
        departamentoId: departmentId,  // uso departmentId aquí
        active
      };
      call$ = this.citySvc.update(dto);
    } else {
      const dto: CreateCityDto = {
        municipioNombre: name,
        departamentoId : departmentId,
        active
      };
      call$ = this.citySvc.create(dto);
    }

    this.loading = true;
    call$
      .pipe(finalize(() => {
        this.loading = false;
        this.showModal = false;
        this.loadAll();
      }))
      .subscribe();
  }

  deleteCity(id: number): void {
    if (!confirm('¿Eliminar esta ciudad?')) return;
    this.loading = true;
    this.citySvc.delete(id)
      .pipe(finalize(() => {
        this.loading = false;
        this.loadAll();
      }))
      .subscribe();
  }

  cancel(): void {
    this.showModal = false;
    this.cityForm.reset();
    this.cdr.markForCheck();
  }
}
