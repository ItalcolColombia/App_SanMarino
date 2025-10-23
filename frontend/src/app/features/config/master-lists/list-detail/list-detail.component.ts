// src/app/features/config/master-lists/list-detail/list-detail.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormBuilder,
  FormGroup,
  FormArray,
  Validators,
  ReactiveFormsModule
} from '@angular/forms';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import { FontAwesomeModule, FaIconLibrary } from '@fortawesome/angular-fontawesome';
import {
  faList,
  faPlus,
  faTrash,
  faTimes,
  faSave
} from '@fortawesome/free-solid-svg-icons';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import {
  MasterListService,
  CreateMasterListDto,
  UpdateMasterListDto
} from '../../../../core/services/master-list/master-list.service';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-list-detail',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    FontAwesomeModule,
    SidebarComponent
  ],
  templateUrl: './list-detail.component.html',
  styleUrls: ['./list-detail.component.scss']
})
export class ListDetailComponent implements OnInit {
  // Icons
  public faList  = faList;
  public faPlus  = faPlus;
  public faTrash = faTrash;
  public faTimes = faTimes;
  public faSave  = faSave;

  // State
  public listForm!: FormGroup;
  public isEdit = false;
  public loading = false;

  constructor(
    private fb: FormBuilder,
    private router: Router,
    private route: ActivatedRoute,
    private svc: MasterListService,
    library: FaIconLibrary
  ) {
    library.addIcons(faList, faPlus, faTrash, faTimes, faSave);
  }

  ngOnInit(): void {
    this.listForm = this.fb.group({
      key:     ['', [Validators.required, Validators.pattern(/^[\w-]+$/)]],
      name:    ['', Validators.required],
      options: this.fb.array([ this.fb.control('', Validators.required) ])
    });

    const idParam = this.route.snapshot.paramMap.get('id');

    // Crear
    if (idParam === null || idParam === 'new') {
      this.isEdit = false;
      return;
    }

    // Editar
    const idNum = Number(idParam);
    if (isNaN(idNum)) {
      this.router.navigate(['/config/master-lists']);
      return;
    }

    this.isEdit = true;
    this.listForm.get('key')!.disable();
    this.loading = true;

    this.svc.getById(idNum)
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: dto => {
          this.listForm.patchValue({
            key:  dto.key,
            name: dto.name
          });

          const fa = this.options;
          fa.clear();
          if (dto.options?.length) {
            dto.options.forEach(opt =>
              fa.push(this.fb.control(opt, Validators.required))
            );
          } else {
            fa.push(this.fb.control('', Validators.required));
          }
        },
        error: () => this.router.navigate(['/config/master-lists'])
      });
  }

  // === Getters ===
  get options(): FormArray {
    return this.listForm.get('options') as FormArray;
  }

  // trackBy correcto para *ngFor
  trackByIndex(index: number): number {
    return index;
  }

  // === Actions ===
  addOption() {
    this.options.push(this.fb.control('', Validators.required));
  }

  removeOption(i: number) {
    if (this.options.length > 1) {
      this.options.removeAt(i);
    }
  }

  save() {
    if (this.listForm.invalid) {
      this.listForm.markAllAsTouched();
      return;
    }

    const raw = this.listForm.getRawValue();
    this.loading = true;

    if (this.isEdit) {
      const id = Number(this.route.snapshot.paramMap.get('id'));
      const dto: UpdateMasterListDto = {
        id,
        key: raw.key,     // aunque está disabled, getRawValue lo incluye
        name: raw.name,
        options: raw.options
      };
      this.svc.update(dto)
        .pipe(finalize(() => this.loading = false))
        .subscribe(() => this.router.navigate(['/config/master-lists']));
    } else {
      const dto: CreateMasterListDto = {
        key: raw.key,
        name: raw.name,
        options: raw.options
      };
      this.svc.create(dto)
        .pipe(finalize(() => this.loading = false))
        .subscribe(() => this.router.navigate(['/config/master-lists']));
    }
  }

  cancel() {
    this.router.navigate(['/config/master-lists']);
  }
}
