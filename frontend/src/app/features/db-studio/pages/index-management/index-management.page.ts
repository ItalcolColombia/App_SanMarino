import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DbStudioService, IndexDto } from '../../data/db-studio.service';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';

@Component({
  standalone: true,
  selector: 'app-index-management',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './index-management.page.html',
  styleUrls: ['./index-management.page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class IndexManagementPage {
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

  // Índices existentes
  indexes = signal<IndexDto[]>([]);

  // Formulario para crear índice
  createIndexForm: FormGroup;

  constructor() {
    this.createIndexForm = this.fb.group({
      name: this.fb.control('', [Validators.required]),
      columns: this.fb.control('', [Validators.required]),
      unique: this.fb.control(false)
    });

    // Obtener parámetros de la ruta o query params
    this.route.params.subscribe(params => {
      if (params['schema'] && params['table']) {
        this.schema.set(params['schema']);
        this.table.set(params['table']);
        this.loadIndexes();
      }
    });

    // Fallback a query params para compatibilidad
    this.route.queryParams.subscribe(params => {
      if (params['schema'] && params['table'] && !this.schema()) {
        this.schema.set(params['schema']);
        this.table.set(params['table']);
        this.loadIndexes();
      }
    });
  }

  loadIndexes(): void {
    const schema = this.schema();
    const table = this.table();
    if (!schema || !table) return;

    this.loading.set(true);
    this.api.getIndexes(table, schema).subscribe({
      next: (indexes) => {
        this.indexes.set(indexes);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(this.getErrorMessage(err));
        this.loading.set(false);
      }
    });
  }

  createIndex(): void {
    if (this.createIndexForm.invalid) return;

    try {
      const columns = this.createIndexForm.value.columns
        .split(',')
        .map((col: string) => col.trim())
        .filter((col: string) => col.length > 0);

      const indexData = {
        name: this.createIndexForm.value.name,
        columns: columns,
        unique: this.createIndexForm.value.unique
      };

      const schema = this.schema();
      const table = this.table();

      this.loading.set(true);
      this.api.createIndex(schema, table, indexData).subscribe({
        next: () => {
          this.success.set('Índice creado correctamente');
          this.createIndexForm.reset();
          this.loadIndexes();
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(this.getErrorMessage(err));
          this.loading.set(false);
        }
      });
    } catch (error) {
      this.error.set('Error al procesar las columnas');
    }
  }

  dropIndex(indexName: string): void {
    if (!confirm(`¿Estás seguro de que quieres eliminar el índice "${indexName}"?`)) {
      return;
    }

    const schema = this.schema();
    const table = this.table();

    this.loading.set(true);
    this.api.dropIndex(schema, table, indexName).subscribe({
      next: () => {
        this.success.set('Índice eliminado correctamente');
        this.loadIndexes();
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(this.getErrorMessage(err));
        this.loading.set(false);
      }
    });
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

  // Utilidades para mostrar información de índices
  getIndexType(index: IndexDto): string {
    if (index.isPrimary) return 'PRIMARY KEY';
    if (index.isUnique) return 'UNIQUE';
    return index.type.toUpperCase();
  }

  getIndexColumns(index: IndexDto): string {
    return index.columns.join(', ');
  }
}

