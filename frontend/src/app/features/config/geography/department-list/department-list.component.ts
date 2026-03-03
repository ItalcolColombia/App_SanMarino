import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators
} from '@angular/forms';
import { FontAwesomeModule, FaIconLibrary } from '@fortawesome/angular-fontawesome';
import {
  faPlus,
  faPen,
  faTrash,
  faCheck,
  faTimes,
  faSave
} from '@fortawesome/free-solid-svg-icons';
import { DepartmentService, DepartamentoDto } from '../../../../core/services/department/department.service';
import { CountryService, PaisDto } from '../../../../core/services/country/country.service';
import { Observable } from 'rxjs/internal/Observable';

@Component({
  selector: 'app-department-list',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FontAwesomeModule
  ],
  templateUrl: './department-list.component.html',
  styleUrls: ['./department-list.component.scss']
})
export class DepartmentListComponent implements OnInit {
  faPlus  = faPlus;
  faPen   = faPen;
  faTrash = faTrash;
  faCheck = faCheck;
  faTimes = faTimes;
  faSave  = faSave;

  departments: DepartamentoDto[] = [];
  countries:   PaisDto[]        = [];
  countryMap:  Record<number,string> = {};

  showModal = false;
  editingDepartment: DepartamentoDto | null = null;
  departmentForm: FormGroup;
  loading = false;

  constructor(
    library: FaIconLibrary,
    private fb: FormBuilder,
    private deptSvc: DepartmentService,
    private countrySvc: CountryService
  ) {
    library.addIcons(faPlus, faPen, faTrash, faCheck, faTimes, faSave);

    this.departmentForm = this.fb.group({
      departamentoNombre: ['', Validators.required],
      paisId:             [null, Validators.required],
      active:             [true]
    });
  }

  ngOnInit(): void {
    this.loadCountries();
    this.loadDepartments();
  }

  private loadCountries() {
    this.countrySvc.getAll().subscribe({
      next: list => {
        this.countries  = list;
        this.countryMap = list.reduce((m, c) => {
          m[c.paisId] = c.paisNombre;
          return m;
        }, {} as Record<number,string>);
      },
      error: err => console.error('Error cargando países', err)
    });
  }

  private loadDepartments() {
    this.loading = true;
    this.deptSvc.getAll().subscribe({
      next: list => {
        this.departments = list;
        this.loading = false;
      },
      error: err => {
        console.error('Error cargando departamentos', err);
        this.loading = false;
      }
    });
  }

  newDepartment(): void {
    this.editingDepartment = null;
    this.departmentForm.reset({ departamentoNombre: '', paisId: null, active: true });
    this.showModal = true;
  }

  editDepartment(id: number): void {
    this.deptSvc.getById(id).subscribe({
      next: dto => {
        this.editingDepartment = dto;
        this.departmentForm.setValue({
          departamentoNombre: dto.departamentoNombre,
          paisId:             dto.paisId,
          active:             dto.active
        });
        this.showModal = true;
      },
      error: err => console.error('Error cargando departamento', err)
    });
  }

  saveDepartment(): void {
    if (this.departmentForm.invalid) {
      this.departmentForm.markAllAsTouched();
      return;
    }
    const v = this.departmentForm.value as {
      departamentoNombre: string;
      paisId:             number;
      active:             boolean;
    };
    this.loading = true;

    let call$: Observable<any>;
    if (this.editingDepartment) {
      const dto: DepartamentoDto = {
        departamentoId:     this.editingDepartment.departamentoId,
        departamentoNombre: v.departamentoNombre,
        paisId:             v.paisId,
        active:             v.active
      };
      call$ = this.deptSvc.update(dto);
    } else {
      call$ = this.deptSvc.create({
        departamentoNombre: v.departamentoNombre,
        paisId:             v.paisId,
        active:             v.active
      });
    }

    call$.subscribe({
      next: () => {
        this.loadDepartments();
        this.showModal = false;
        this.loading = false;
      },
      error: err => {
        console.error('Error guardando departamento', err);
        this.loading = false;
      }
    });
  }

  deleteDepartment(id: number): void {
    if (!confirm('¿Eliminar este departamento?')) return;
    this.loading = true;
    this.deptSvc.delete(id).subscribe({
      next: () => {
        this.loadDepartments();
        this.loading = false;
      },
      error: err => {
        console.error('Error eliminando departamento', err);
        this.loading = false;
      }
    });
  }

  cancel(): void {
    this.showModal = false;
    this.departmentForm.reset();
  }
}
