import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DbStudioService, CreateTableDto } from '../../data/db-studio.service';
import { FormArray, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

type IdentityMode = 'always' | 'by_default' | null;

type ColumnFG = FormGroup<{
  name: FormControl<string>;
  type: FormControl<string>;
  nullable: FormControl<boolean>;
  default: FormControl<string | null>;
  identity: FormControl<IdentityMode>;
}>;

type CreateTableForm = FormGroup<{
  schema: FormControl<string>;
  table: FormControl<string>;
  columns: FormArray<ColumnFG>;
}>;

@Component({
  standalone: true,
  selector: 'app-create-table',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './create-table.page.html',
  styleUrls: ['./create-table.page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CreateTablePage {
  private fb = inject(FormBuilder);
  private api = inject(DbStudioService);
  private router = inject(Router);

  loading = signal(false);
  error = signal<string | null>(null);

  form: CreateTableForm = this.fb.group({
    schema: this.fb.nonNullable.control('public', { validators: [Validators.required] }),
    table:  this.fb.nonNullable.control('',       { validators: [Validators.required] }),
    columns: this.fb.array<ColumnFG>([])
  });

  get columnsFA(): FormArray<ColumnFG> {
    return this.form.controls.columns;
  }

  private buildColumn(): ColumnFG {
    return this.fb.group({
      name:     this.fb.nonNullable.control('',    { validators: [Validators.required] }),
      type:     this.fb.nonNullable.control('text',{ validators: [Validators.required] }),
      nullable: this.fb.nonNullable.control(true),
      default:  this.fb.control<string | null>(null),
      identity: this.fb.control<IdentityMode>(null)
    });
  }

  addColumn() {
    this.columnsFA.push(this.buildColumn());
  }

  removeColumn(ix: number) {
    this.columnsFA.removeAt(ix);
  }

  submit() {
    this.error.set(null);

    if (this.form.invalid || this.columnsFA.length === 0) {
      this.error.set('Completa el formulario y agrega al menos una columna.');
      return;
    }

    // getRawValue ya coincide con CreateTableDto (mismos nombres/estructuras)
    const dto = this.form.getRawValue() as unknown as CreateTableDto;

    this.loading.set(true);
    this.api.createTable(dto).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/db-studio']);
      },
      error: err => {
        this.error.set(err?.error?.message ?? err?.message ?? 'Error');
        this.loading.set(false);
      }
    });
  }
}
