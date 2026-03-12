// src/app/features/config/master-lists/modal-edit-master-list/modal-edit-master-list.component.ts
import {
  Component,
  Input,
  Output,
  EventEmitter,
  OnChanges,
  SimpleChanges
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormBuilder,
  FormGroup,
  FormArray,
  Validators,
  ReactiveFormsModule
} from '@angular/forms';
import { FontAwesomeModule, FaIconLibrary } from '@fortawesome/angular-fontawesome';
import {
  faList,
  faPlus,
  faTrash,
  faTimes,
  faSave
} from '@fortawesome/free-solid-svg-icons';
import {
  MasterListService,
  MasterListDto,
  MasterListOptionItemDto,
  UpdateMasterListDto
} from '../../../../core/services/master-list/master-list.service';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-modal-edit-master-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FontAwesomeModule],
  templateUrl: './modal-edit-master-list.component.html',
  styleUrls: ['./modal-edit-master-list.component.scss']
})
export class ModalEditMasterListComponent implements OnChanges {
  faList = faList;
  faPlus = faPlus;
  faTrash = faTrash;
  faTimes = faTimes;
  faSave = faSave;

  @Input() listId: number | null = null;
  @Output() closed = new EventEmitter<void>();
  @Output() saved = new EventEmitter<void>();

  listForm!: FormGroup;
  loading = false;
  loadError: string | null = null;

  constructor(
    private fb: FormBuilder,
    private svc: MasterListService,
    library: FaIconLibrary
  ) {
    library.addIcons(faList, faPlus, faTrash, faTimes, faSave);
    this.buildForm();
  }

  private buildForm(): void {
    this.listForm = this.fb.group({
      key: ['', [Validators.required, Validators.pattern(/^[\w-]+$/)]],
      name: ['', Validators.required],
      options: this.fb.array([this.fb.control('', Validators.required)])
    });
  }

  get options(): FormArray {
    return this.listForm.get('options') as FormArray;
  }

  get isOpen(): boolean {
    return this.listId != null;
  }

  ngOnChanges(changes: SimpleChanges): void {
    const idChange = changes['listId'];
    if (idChange?.currentValue != null && idChange.currentValue !== idChange.previousValue) {
      this.loadList(this.listId!);
    }
  }

  private loadList(id: number): void {
    this.loadError = null;
    this.loading = true;
    this.svc
      .getById(id)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (dto) => this.populateForm(dto),
        error: () => {
          this.loadError = 'No se pudo cargar la lista.';
        }
      });
  }

  /** Rellena el formulario; options pueden ser { id, value } o string. */
  private populateForm(dto: MasterListDto): void {
    this.listForm.patchValue({
      key: dto.key,
      name: dto.name
    });
    this.listForm.get('key')?.disable();

    const fa = this.options;
    fa.clear();
    if (dto.options?.length) {
      dto.options.forEach((opt: MasterListOptionItemDto | string) => {
        const value =
          typeof opt === 'object' && opt != null && 'value' in opt
            ? (opt as MasterListOptionItemDto).value
            : typeof opt === 'string'
              ? opt
              : '';
        fa.push(this.fb.control(value, Validators.required));
      });
    } else {
      fa.push(this.fb.control('', Validators.required));
    }
  }

  trackByIndex(index: number): number {
    return index;
  }

  addOption(): void {
    this.options.push(this.fb.control('', Validators.required));
  }

  removeOption(i: number): void {
    if (this.options.length > 1) {
      this.options.removeAt(i);
    }
  }

  close(): void {
    this.listId = null;
    this.loadError = null;
    this.closed.emit();
  }

  onBackdropClick(event: Event): void {
    if (event.target === event.currentTarget) {
      this.close();
    }
  }

  save(): void {
    if (this.listForm.invalid || this.listId == null) {
      this.listForm.markAllAsTouched();
      return;
    }
    const raw = this.listForm.getRawValue();
    const dto: UpdateMasterListDto = {
      id: this.listId,
      key: raw.key,
      name: raw.name,
      options: raw.options
    };
    this.loading = true;
    this.svc
      .update(dto)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: () => {
          this.close();
          this.saved.emit();
        },
        error: () => {
          this.loadError = 'Error al guardar. Intenta de nuevo.';
        }
      });
  }
}
