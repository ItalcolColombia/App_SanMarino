import { Component, OnInit, OnDestroy, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { DbStudioService } from '../../data/db-studio.service';
import {
  SchemaDto, TableDto, ViewDto, FunctionDto, ColumnDto, IndexDto, ForeignKeyDto, TableStatsDto,
  QueryPageDto, MyAccessDto, ObjectGrantDto, ActivitySnapshot, PoolStats, AccessLevel
} from '../../models/db-studio.models';
import {
  construirWherePk, filasACsv, descargarTexto, formatCell, antiguedad, colorEstado
} from '../../funciones/db-studio.funciones';

type Tab = 'explorer' | 'sql' | 'permissions' | 'activity';
type DetailTab = 'data' | 'columns' | 'indexes' | 'fks' | 'definition' | 'source';
interface SelectedObject { schema: string; name: string; kind: 'table' | 'view' | 'function'; }

@Component({
  selector: 'app-db-studio-main',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './db-studio-main.component.html',
  styleUrls: ['./db-studio-main.component.scss']
})
export class DbStudioMainComponent implements OnInit, OnDestroy {
  private db = inject(DbStudioService);

  // expuestos al template
  readonly formatCell = formatCell;
  readonly antiguedad = antiguedad;
  readonly colorEstado = colorEstado;

  // ---- estado global ----
  loading = signal(false);
  error = signal<string | null>(null);
  tab = signal<Tab>('explorer');
  access = signal<MyAccessDto | null>(null);
  isAdmin = computed(() => this.access()?.isAdmin ?? false);

  // ---- explorador ----
  schemas = signal<SchemaDto[]>([]);
  selectedSchema = signal<string>('public');
  tables = signal<TableDto[]>([]);
  views = signal<ViewDto[]>([]);
  functions = signal<FunctionDto[]>([]);
  treeFilter = signal('');
  selected = signal<SelectedObject | null>(null);
  detailTab = signal<DetailTab>('data');

  columns = signal<ColumnDto[]>([]);
  indexes = signal<IndexDto[]>([]);
  foreignKeys = signal<ForeignKeyDto[]>([]);
  stats = signal<TableStatsDto | null>(null);
  viewDefinition = signal<string | null>(null);
  functionSource = signal<string | null>(null);
  selectedFunction = signal<FunctionDto | null>(null);

  // ---- grilla de datos ----
  page = signal<QueryPageDto | null>(null);
  offset = signal(0);
  readonly pageSize = 50;
  canWriteSelected = signal(false);
  editingIndex = signal<number | null>(null);
  editBuffer = signal<Record<string, unknown>>({});
  newRow = signal<Record<string, unknown> | null>(null);

  filteredTables = computed(() => {
    const f = this.treeFilter().toLowerCase();
    return f ? this.tables().filter(t => t.name.toLowerCase().includes(f)) : this.tables();
  });
  filteredViews = computed(() => {
    const f = this.treeFilter().toLowerCase();
    return f ? this.views().filter(v => v.name.toLowerCase().includes(f)) : this.views();
  });

  // ---- consola SQL ----
  sqlText = signal('');
  sqlResult = signal<{ columns: string[]; rows: Record<string, unknown>[]; affected?: number; ms?: number; error?: string } | null>(null);
  sqlBusy = signal(false);

  // ---- permisos ----
  grants = signal<ObjectGrantDto[]>([]);
  grantForm = signal<{ userId: string; schema: string; object: string; level: AccessLevel }>(
    { userId: '', schema: 'public', object: '', level: 'read' });

  // ---- actividad ----
  activity = signal<ActivitySnapshot | null>(null);
  pool = signal<PoolStats | null>(null);
  autoRefresh = signal(false);
  private timer?: any;

  ngOnInit(): void {
    this.db.myAccess().subscribe({
      next: a => { this.access.set(a); this.loadSchemas(); },
      error: e => this.fail(e)
    });
  }

  ngOnDestroy(): void { this.stopAuto(); }

  // ===================== Navegación =====================
  setTab(t: Tab): void {
    this.tab.set(t);
    if (t === 'permissions' && this.grants().length === 0) this.loadGrants();
    if (t === 'activity') this.refreshActivity();
    if (t !== 'activity') this.stopAuto();
  }

  // ===================== Explorador =====================
  loadSchemas(): void {
    this.loading.set(true);
    this.db.getSchemas().subscribe({
      next: s => { this.schemas.set(s); this.loading.set(false); this.loadObjects(); },
      error: e => this.fail(e)
    });
  }

  onSchemaChange(schema: string): void {
    this.selectedSchema.set(schema);
    this.selected.set(null);
    this.page.set(null);
    this.loadObjects();
  }

  loadObjects(): void {
    const schema = this.selectedSchema();
    this.loading.set(true);
    this.db.getTables(schema).subscribe({
      next: t => { this.tables.set(t.filter(x => x.kind !== 'VIEW' && x.kind !== 'MATERIALIZED VIEW')); this.loading.set(false); },
      error: e => this.fail(e)
    });
    this.db.getViews(schema).subscribe({ next: v => this.views.set(v), error: () => {} });
    if (this.isAdmin()) this.db.getFunctions(schema).subscribe({ next: f => this.functions.set(f), error: () => {} });
  }

  selectObject(name: string, kind: 'table' | 'view' | 'function'): void {
    const schema = this.selectedSchema();
    this.selected.set({ schema, name, kind });
    this.viewDefinition.set(null);
    this.functionSource.set(null);
    this.editingIndex.set(null);
    this.newRow.set(null);

    if (kind === 'function') {
      this.selectedFunction.set(this.functions().find(f => f.name === name) ?? null);
      this.detailTab.set('source');
      this.loadFunctionSource();
      return;
    }

    this.detailTab.set('data');
    this.offset.set(0);
    this.canWriteSelected.set(this.computeCanWrite(schema, name));
    this.loadDetail();
    this.loadData();
  }

  private computeCanWrite(schema: string, name: string): boolean {
    if (this.isAdmin()) return true;
    return (this.access()?.objects ?? []).some(
      o => o.schema === schema && o.object === name && o.accessLevel === 'write');
  }

  loadDetail(): void {
    const s = this.selected(); if (!s) return;
    this.db.getTableDetails(s.schema, s.name).subscribe({
      next: d => {
        this.columns.set(d.columns); this.indexes.set(d.indexes);
        this.foreignKeys.set(d.foreignKeys); this.stats.set(d.stats);
      },
      error: e => this.fail(e)
    });
  }

  loadFunctionSource(): void {
    const s = this.selected(); if (!s) return;
    this.db.getFunctionSource(s.schema, s.name).subscribe({
      next: r => this.functionSource.set(r.definition),
      error: e => this.fail(e)
    });
  }

  loadViewDefinition(): void {
    const s = this.selected(); if (!s || s.kind !== 'view') return;
    if (this.viewDefinition() !== null) return;
    this.db.getViewDefinition(s.schema, s.name).subscribe({
      next: r => this.viewDefinition.set(r.definition),
      error: e => this.fail(e)
    });
  }

  loadData(): void {
    const s = this.selected(); if (!s) return;
    this.loading.set(true);
    this.db.preview(s.name, { schema: s.schema, limit: this.pageSize, offset: this.offset() }).subscribe({
      next: p => { this.page.set(p); this.loading.set(false); },
      error: e => this.fail(e)
    });
  }

  nextPage(): void { this.offset.set(this.offset() + this.pageSize); this.loadData(); }
  prevPage(): void { this.offset.set(Math.max(0, this.offset() - this.pageSize)); this.loadData(); }

  dataColumns(): string[] { return this.page()?.columns ?? this.columns().map(c => c.name); }

  // ---- edición de filas ----
  startEdit(i: number, row: Record<string, unknown>): void {
    this.editingIndex.set(i);
    this.editBuffer.set({ ...row });
  }
  cancelEdit(): void { this.editingIndex.set(null); }

  setEdit(col: string, value: unknown): void {
    this.editBuffer.set({ ...this.editBuffer(), [col]: value });
  }
  setNew(col: string, value: unknown): void {
    const r = this.newRow(); if (!r) return;
    this.newRow.set({ ...r, [col]: value });
  }

  saveEdit(original: Record<string, unknown>): void {
    const s = this.selected(); if (!s) return;
    const where = construirWherePk(original, this.columns());
    const buf = this.editBuffer();
    const data: Record<string, unknown> = {};
    for (const c of this.dataColumns()) if (buf[c] !== original[c]) data[c] = buf[c];
    if (Object.keys(data).length === 0) { this.cancelEdit(); return; }
    this.db.updateData(s.schema, s.name, data, where).subscribe({
      next: () => { this.editingIndex.set(null); this.loadData(); },
      error: e => this.fail(e)
    });
  }

  deleteRow(row: Record<string, unknown>): void {
    const s = this.selected(); if (!s) return;
    if (!confirm('¿Eliminar esta fila? Esta acción no se puede deshacer.')) return;
    const where = construirWherePk(row, this.columns());
    this.db.deleteData(s.schema, s.name, where).subscribe({
      next: () => this.loadData(), error: e => this.fail(e)
    });
  }

  startInsert(): void {
    const blank: Record<string, unknown> = {};
    for (const c of this.columns()) blank[c.name] = null;
    this.newRow.set(blank);
  }
  cancelInsert(): void { this.newRow.set(null); }
  saveInsert(): void {
    const s = this.selected(); const row = this.newRow(); if (!s || !row) return;
    this.db.insertData(s.schema, s.name, [row]).subscribe({
      next: () => { this.newRow.set(null); this.offset.set(0); this.loadData(); },
      error: e => this.fail(e)
    });
  }

  exportCsv(): void {
    const p = this.page(); const s = this.selected(); if (!p || !s) return;
    descargarTexto(`${s.schema}_${s.name}.csv`, filasACsv(this.dataColumns(), p.rows), 'text/csv');
  }

  // ===================== Consola SQL =====================
  runSql(confirm = false): void {
    const sql = this.sqlText().trim(); if (!sql) return;
    this.sqlBusy.set(true); this.sqlResult.set(null);
    this.db.executeSql(sql, confirm).subscribe({
      next: r => {
        this.sqlBusy.set(false);
        if (!r.success) { this.sqlResult.set({ columns: [], rows: [], error: r.error }); return; }
        this.sqlResult.set({
          columns: r.data?.columns ?? [], rows: r.data?.rows ?? [],
          affected: r.affectedRows, ms: r.executionTime
        });
      },
      error: e => { this.sqlBusy.set(false); this.sqlResult.set({ columns: [], rows: [], error: this.msg(e) }); }
    });
  }

  // ===================== Permisos =====================
  loadGrants(): void {
    this.db.getGrants().subscribe({ next: g => this.grants.set(g), error: e => this.fail(e) });
  }
  patchGrantForm(part: Partial<{ userId: string; schema: string; object: string; level: AccessLevel }>): void {
    this.grantForm.set({ ...this.grantForm(), ...part });
  }
  addGrant(): void {
    const f = this.grantForm();
    if (!f.userId || !f.object) { this.error.set('UserId y objeto son requeridos.'); return; }
    this.db.upsertGrant({ userId: f.userId, schema: f.schema, object: f.object, accessLevel: f.level }).subscribe({
      next: () => { this.loadGrants(); this.error.set(null); },
      error: e => this.fail(e)
    });
  }
  revokeGrant(id: number): void {
    this.db.revokeGrant(id).subscribe({ next: () => this.loadGrants(), error: e => this.fail(e) });
  }

  // ===================== Actividad =====================
  refreshActivity(): void {
    this.db.getActivity().subscribe({ next: a => this.activity.set(a), error: e => this.fail(e) });
    this.db.getPoolStats().subscribe({ next: p => this.pool.set(p), error: () => {} });
  }
  toggleAuto(): void {
    this.autoRefresh.set(!this.autoRefresh());
    if (this.autoRefresh()) { this.refreshActivity(); this.timer = setInterval(() => this.refreshActivity(), 4000); }
    else this.stopAuto();
  }
  private stopAuto(): void {
    if (this.timer) { clearInterval(this.timer); this.timer = undefined; }
    if (this.autoRefresh()) this.autoRefresh.set(false);
  }

  cancelBackend(pid: number): void {
    this.db.cancelBackend(pid).subscribe({ next: () => this.refreshActivity(), error: e => this.fail(e) });
  }
  terminateBackend(pid: number): void {
    if (!confirm(`¿Terminar la sesión PID ${pid}?`)) return;
    this.db.terminateBackend(pid).subscribe({ next: () => this.refreshActivity(), error: e => this.fail(e) });
  }

  // ===================== util =====================
  private fail(e: unknown): void { this.loading.set(false); this.error.set(this.msg(e)); }
  private msg(e: any): string { return e?.error?.message ?? e?.message ?? 'Error inesperado'; }
  dismissError(): void { this.error.set(null); }
}
