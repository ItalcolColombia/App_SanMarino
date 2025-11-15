import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DbStudioService } from '../../data/db-studio.service';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';

@Component({
  standalone: true,
  selector: 'app-data-management',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './data-management.page.html',
  styleUrls: ['./data-management.page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DataManagementPage {
  private fb = inject(FormBuilder);
  private api = inject(DbStudioService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  loading = signal(false);
  error = signal<string | null>(null);
  success = signal<string | null>(null);

  // Datos de la tabla actual
  schema = signal<string>('');
  table = signal<string>('');

  // Formularios
  insertForm: FormGroup;
  updateForm: FormGroup;
  deleteForm: FormGroup;

  // Datos de ejemplo para mostrar estructura
  sampleData = signal<Record<string, any>[]>([]);

  constructor() {
    this.insertForm = this.fb.group({
      data: this.fb.control('', [Validators.required])
    });

    this.updateForm = this.fb.group({
      data: this.fb.control('', [Validators.required]),
      where: this.fb.control('', [Validators.required])
    });

    this.deleteForm = this.fb.group({
      where: this.fb.control('', [Validators.required])
    });

    // Obtener parámetros de la ruta o query params
    this.route.params.subscribe(params => {
      if (params['schema'] && params['table']) {
        this.schema.set(params['schema']);
        this.table.set(params['table']);
        this.loadSampleData();
      }
    });

    // Fallback a query params para compatibilidad
    this.route.queryParams.subscribe(params => {
      if (params['schema'] && params['table'] && !this.schema()) {
        this.schema.set(params['schema']);
        this.table.set(params['table']);
        this.loadSampleData();
      }
    });
  }

  loadSampleData(): void {
    const schema = this.schema();
    const table = this.table();
    if (!schema || !table) return;

    this.loading.set(true);
    this.api.preview(table, { schema, limit: 5 }).subscribe({
      next: (result) => {
        this.sampleData.set(result.rows);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(this.getErrorMessage(err));
        this.loading.set(false);
      }
    });
  }

  insertData(): void {
    if (this.insertForm.invalid) return;

    try {
      const data = JSON.parse(this.insertForm.value.data);
      const schema = this.schema();
      const table = this.table();

      this.loading.set(true);
      this.api.insertData(schema, table, Array.isArray(data) ? data : [data]).subscribe({
        next: () => {
          this.success.set('Datos insertados correctamente');
          this.insertForm.reset();
          this.loadSampleData();
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(this.getErrorMessage(err));
          this.loading.set(false);
        }
      });
    } catch (error) {
      this.error.set('Formato JSON inválido');
    }
  }

  updateData(): void {
    if (this.updateForm.invalid) return;

    try {
      const data = JSON.parse(this.updateForm.value.data);
      const where = JSON.parse(this.updateForm.value.where);
      const schema = this.schema();
      const table = this.table();

      this.loading.set(true);
      this.api.updateData(schema, table, data, where).subscribe({
        next: () => {
          this.success.set('Datos actualizados correctamente');
          this.updateForm.reset();
          this.loadSampleData();
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(this.getErrorMessage(err));
          this.loading.set(false);
        }
      });
    } catch (error) {
      this.error.set('Formato JSON inválido');
    }
  }

  deleteData(): void {
    if (this.deleteForm.invalid) return;

    try {
      const where = JSON.parse(this.deleteForm.value.where);
      const schema = this.schema();
      const table = this.table();

      this.loading.set(true);
      this.api.deleteData(schema, table, where).subscribe({
        next: () => {
          this.success.set('Datos eliminados correctamente');
          this.deleteForm.reset();
          this.loadSampleData();
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(this.getErrorMessage(err));
          this.loading.set(false);
        }
      });
    } catch (error) {
      this.error.set('Formato JSON inválido');
    }
  }

  goBack(): void {
    this.router.navigate(['explorer'], { relativeTo: this.route.parent });
  }

  private getErrorMessage(error: any): string {
    return error?.error?.message ||
           error?.error?.title ||
           error?.message ||
           'Error desconocido';
  }

  // Utilidades para mostrar estructura de datos
  getSampleKeys(): string[] {
    const sample = this.sampleData()[0];
    return sample ? Object.keys(sample) : [];
  }

  formatSampleValue(value: any): string {
    if (value === null || value === undefined) return 'null';
    if (typeof value === 'string') return `"${value}"`;
    return String(value);
  }
}

