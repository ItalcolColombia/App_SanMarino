// src/app/features/db-studio/pages/explorer/explorer.page.ts
import { ChangeDetectionStrategy, Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DbStudioService, SchemaDto, TableDto, ColumnDto, QueryPageDto, IndexDto, ForeignKeyDto, TableStatsDto } from '../../data/db-studio.service';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';

@Component({
  standalone: true,
  selector: 'app-db-explorer',
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterModule, SidebarComponent],
  templateUrl: './explorer.page.html',
  styleUrls: ['./explorer.page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExplorerPage implements OnInit {
  private api = inject(DbStudioService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  loading = signal(false);
  error = signal<string | null>(null);

  schemas = signal<SchemaDto[]>([]);
  selectedSchema = signal<string>('public');

  tables = signal<TableDto[]>([]);
  selectedTable = signal<string | null>(null);

  // Detalles de la tabla seleccionada
  tableDetails = signal<{
    table: TableDto;
    columns: ColumnDto[];
    indexes: IndexDto[];
    foreignKeys: ForeignKeyDto[];
    stats: TableStatsDto;
  } | null>(null);

  previewPage = signal<QueryPageDto | null>(null);

  // Paginación
  limit = signal(20);
  offset = signal(0);

  // Computed
  hasTableSelected = computed(() => !!this.selectedTable());
  hasTableDetails = computed(() => !!this.tableDetails());

  ngOnInit(): void {
    this.loadSchemas();
    this.loadTables();

    // Cargar tabla desde query params
    this.route.queryParams.subscribe(params => {
      if (params['schema'] && params['table']) {
        this.selectedSchema.set(params['schema']);
        this.selectedTable.set(params['table']);
        this.loadTables();
        this.loadTableDetails();
      }
    });
  }

  loadSchemas() {
    this.loading.set(true);
    this.api.getSchemas().subscribe({
      next: s => { this.schemas.set(s); this.loading.set(false); },
      error: err => { this.error.set(this.msg(err)); this.loading.set(false); }
    });
  }

  loadTables() {
    this.loading.set(true);
    this.api.getTables(this.selectedSchema()).subscribe({
      next: t => { this.tables.set(t); this.loading.set(false); },
      error: err => { this.error.set(this.msg(err)); this.loading.set(false); }
    });
  }

  selectSchema(sc: string) {
    if (this.selectedSchema() === sc) return;
    this.selectedSchema.set(sc);
    this.selectedTable.set(null);
    this.tableDetails.set(null);
    this.previewPage.set(null);
    this.offset.set(0);
    this.loadTables();
  }

  selectTable(tb: string) {
    this.selectedTable.set(tb);
    this.loadTableDetails();
    this.loadPreview();
  }

  loadTableDetails() {
    const sc = this.selectedSchema();
    const tb = this.selectedTable();
    if (!tb) return;

    this.loading.set(true);
    this.api.getTableDetails(sc, tb).subscribe({
      next: details => {
        this.tableDetails.set(details);
        this.loading.set(false);
      },
      error: err => {
        this.error.set(this.msg(err));
        this.loading.set(false);
      }
    });
  }

  loadPreview() {
    const sc = this.selectedSchema();
    const tb = this.selectedTable();
    if (!tb) return;
    this.loading.set(true);
    this.api.preview(tb, {
      schema: sc === 'public' ? undefined : sc,
      limit: this.limit(),
      offset: this.offset()
    }).subscribe({
      next: p => { this.previewPage.set(p); this.loading.set(false); },
      error: err => { this.error.set(this.msg(err)); this.loading.set(false); }
    });
  }

  pageNext() {
    this.offset.set(this.offset() + this.limit());
    this.loadPreview();
  }
  pagePrev() {
    this.offset.set(Math.max(0, this.offset() - this.limit()));
    this.loadPreview();
  }

  // 🔑 util para header/filas dinámicas sin pipes extra
  keys(row: Record<string, unknown> | undefined | null): string[] {
    return row ? Object.keys(row) : [];
  }

  // navegación a crear
  goCreateTable() {
    this.router.navigate(['create-table'], { relativeTo: this.route.parent });
  }

  goQueryConsole() {
    this.router.navigate(['query'], { relativeTo: this.route.parent });
  }

  goDataManagement() {
    const sc = this.selectedSchema();
    const tb = this.selectedTable();
    if (!tb) return;

    this.router.navigate(['data', sc, tb], { relativeTo: this.route.parent });
  }

  goIndexManagement() {
    const sc = this.selectedSchema();
    const tb = this.selectedTable();
    if (!tb) return;

    this.router.navigate(['indexes', sc, tb], { relativeTo: this.route.parent });
  }

  goBack() {
    this.router.navigate([''], { relativeTo: this.route.parent });
  }

  // Acciones de tabla
  exportTable() {
    const sc = this.selectedSchema();
    const tb = this.selectedTable();
    if (!tb) return;

    this.api.exportTable(sc, tb, 'sql').subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${sc}_${tb}.sql`;
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: (err) => this.error.set(this.msg(err))
    });
  }

  analyzeTable() {
    const sc = this.selectedSchema();
    const tb = this.selectedTable();
    if (!tb) return;

    this.loading.set(true);
    this.api.getTableStats(sc, tb).subscribe({
      next: (stats) => {
        // Actualizar estadísticas en los detalles
        const details = this.tableDetails();
        if (details) {
          this.tableDetails.set({ ...details, stats });
        }
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(this.msg(err));
        this.loading.set(false);
      }
    });
  }

  // Utilidades
  formatNumber(num: number): string {
    return num.toLocaleString('es-ES');
  }

  formatBytes(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  private msg(err: any): string {
    return err?.error?.message ?? err?.error?.title ?? err?.message ?? 'Error desconocido';
  }
}
