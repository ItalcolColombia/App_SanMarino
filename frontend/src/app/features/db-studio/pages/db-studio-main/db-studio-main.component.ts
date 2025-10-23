// src/app/features/db-studio/pages/db-studio-main/db-studio-main.component.ts
import { Component, OnInit, OnDestroy, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';

import { DbStudioService, SchemaDto, TableDto, DatabaseAnalysisDto } from '../../data/db-studio.service';

// Componentes especializados
import { DbStudioHeaderComponent, DbStudioHeaderEvent } from '../../components/db-studio-header/db-studio-header.component';
import { DbStudioSidebarComponent, DbStudioSidebarEvent } from '../../components/db-studio-sidebar/db-studio-sidebar.component';
import { DbStudioOverviewComponent, DbStudioOverviewEvent } from '../../components/db-studio-overview/db-studio-overview.component';
import { SqlQueryComponent, SqlQueryEvent } from '../../components/sql-query/sql-query.component';
import { TableManagementComponent, TableManagementEvent } from '../../components/table-management/table-management.component';

export type DbStudioView = 'overview' | 'tables' | 'sql' | 'data' | 'explorer';

@Component({
  selector: 'app-db-studio-main',
  standalone: true,
  imports: [
    CommonModule,
    DbStudioHeaderComponent,
    DbStudioSidebarComponent,
    DbStudioOverviewComponent,
    SqlQueryComponent,
    TableManagementComponent
  ],
  templateUrl: './db-studio-main.component.html',
  styleUrls: ['./db-studio-main.component.scss']
})
export class DbStudioMainComponent implements OnInit, OnDestroy {
  private dbService = inject(DbStudioService);
  private router = inject(Router);

  // ====== Estado Global ======
  loading = signal<boolean>(false);
  error = signal<string | null>(null);
  connectionStatus = signal<'connected' | 'disconnected' | 'connecting'>('connected');
  
  // ====== Datos ======
  schemas = signal<SchemaDto[]>([]);
  tables = signal<TableDto[]>([]);
  databaseAnalysis = signal<DatabaseAnalysisDto | null>(null);
  
  // ====== Vista Actual ======
  activeView = signal<DbStudioView>('overview');
  selectedSchema = signal<string>('public');
  selectedTable = signal<TableDto | null>(null);
  
  // ====== Computed ======
  databaseName = computed(() => {
    const analysis = this.databaseAnalysis();
    return (analysis as any)?.databaseName || 'PostgreSQL';
  });

  private destroy$ = new Subject<void>();

  constructor() {}

  ngOnInit(): void {
    this.loadInitialData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadInitialData(): void {
    this.loading.set(true);
    this.connectionStatus.set('connecting');
    
    // Cargar datos iniciales
    Promise.all([
      this.loadSchemas(),
      this.loadTables(),
      this.analyzeDatabase()
    ]).finally(() => {
        this.loading.set(false);
      this.connectionStatus.set('connected');
    });
  }

  private async loadSchemas(): Promise<void> {
    try {
      const schemas = await this.dbService.getSchemas().toPromise();
      this.schemas.set(schemas || []);
    } catch (err) {
      this.handleError('Error cargando esquemas', err);
    }
  }

  private async loadTables(): Promise<void> {
    try {
      const tables = await this.dbService.getTables(this.selectedSchema()).toPromise();
      this.tables.set(tables || []);
    } catch (err) {
      this.handleError('Error cargando tablas', err);
    }
  }

  private async analyzeDatabase(): Promise<void> {
    try {
      const analysis = await this.dbService.analyzeDatabase().toPromise();
      this.databaseAnalysis.set(analysis || null);
    } catch (err) {
      this.handleError('Error analizando base de datos', err);
    }
  }

  private handleError(message: string, error: any): void {
    console.error(message, error);
    this.error.set(message);
    this.connectionStatus.set('disconnected');
  }

  // ====== Eventos del Header ======
  onHeaderAction(event: DbStudioHeaderEvent): void {
    switch (event.type) {
      case 'refresh':
        this.refreshData();
        break;
      case 'analyze':
        this.analyzeDatabase();
        break;
      case 'export':
        this.exportData();
        break;
      case 'import':
        this.importData();
        break;
      case 'settings':
        this.openSettings();
        break;
      case 'help':
        this.openHelp();
        break;
    }
  }

  // ====== Eventos del Sidebar ======
  onSidebarAction(event: DbStudioSidebarEvent): void {
    switch (event.type) {
      case 'schema-selected':
        this.selectSchema(event.schema!);
        break;
      case 'table-selected':
        this.selectTable(event.table!);
        break;
      case 'table-action':
        this.handleTableAction(event.table!, event.action!);
        break;
      case 'refresh':
        this.refreshData();
        break;
    }
  }

  // ====== Eventos del Overview ======
  onOverviewAction(event: DbStudioOverviewEvent): void {
    switch (event.type) {
      case 'refresh':
        this.refreshData();
        break;
      case 'analyze':
        this.analyzeDatabase();
        break;
      case 'schema-selected':
        this.selectSchema(event.data);
        break;
      case 'table-selected':
        this.selectTable(event.data);
        break;
    }
  }

  // ====== Eventos del SQL Query ======
  onSqlQueryAction(event: SqlQueryEvent): void {
    switch (event.type) {
      case 'query-executed':
        console.log('Query ejecutada:', event.data);
        // Opcional: mostrar notificación de éxito
        break;
      case 'query-saved':
        console.log('Query guardada:', event.data);
        // Opcional: mostrar notificación de guardado
        break;
      case 'query-deleted':
        console.log('Query eliminada:', event.data);
        // Opcional: mostrar notificación de eliminación
        break;
      case 'query-copied':
        console.log('Query copiada al portapapeles');
        // Opcional: mostrar notificación de copiado
        break;
    }
  }

  // ====== Eventos de Gestión de Tablas ======
  onTableManagementAction(event: TableManagementEvent): void {
    switch (event.type) {
      case 'table-selected':
        this.selectTable(event.table!);
        break;
      case 'table-action':
        this.handleTableAction(event.table!, event.action!);
        break;
      case 'create-table':
        console.log('Crear nueva tabla');
        // Implementar creación de tabla
        break;
      case 'refresh':
        this.loadTables();
        break;
    }
  }

  // ====== Navegación ======
  setActiveView(view: DbStudioView): void {
    this.activeView.set(view);
    
    // Navegar a rutas específicas si es necesario
    switch (view) {
      case 'explorer':
        this.router.navigate(['/db-studio/explorer']);
        break;
      case 'sql':
        this.router.navigate(['/db-studio/query-console']);
        break;
    }
  }

  selectSchema(schema: string): void {
    if (this.selectedSchema() === schema) return;
    
    this.selectedSchema.set(schema);
    this.selectedTable.set(null);
    this.loadTables();
  }

  selectTable(table: TableDto): void {
    this.selectedTable.set(table);
    this.setActiveView('data');
  }

  // ====== Acciones de Tabla ======
  handleTableAction(table: TableDto, action: 'view' | 'edit' | 'delete' | 'export' | 'import' | 'structure'): void {
    switch (action) {
      case 'view':
        this.selectTable(table);
        break;
      case 'edit':
        this.editTable(table);
        break;
      case 'delete':
        this.deleteTable(table);
        break;
      case 'export':
        this.exportTable(table);
        break;
      case 'import':
        this.importTable(table);
        break;
      case 'structure':
        this.viewTableStructure(table);
        break;
    }
  }

  private editTable(table: TableDto): void {
    // TODO: Implementar edición de tabla
    console.log('Edit table:', table);
  }

  private deleteTable(table: TableDto): void {
    // TODO: Implementar eliminación de tabla
    console.log('Delete table:', table);
  }

  private exportTable(table: TableDto): void {
    // TODO: Implementar exportación de tabla
    console.log('Export table:', table);
  }

  private importTable(table: TableDto): void {
    // TODO: Implementar importación de datos
    console.log('Import data to table:', table);
  }

  private viewTableStructure(table: TableDto): void {
    // TODO: Implementar vista de estructura de tabla
    console.log('View table structure:', table);
  }

  // ====== Acciones del Header ======
  private refreshData(): void {
    this.loadInitialData();
  }

  private exportData(): void {
    // TODO: Implementar exportación de datos
    console.log('Export data');
  }

  private importData(): void {
    // TODO: Implementar importación de datos
    console.log('Import data');
  }

  private openSettings(): void {
    // TODO: Implementar configuración
    console.log('Open settings');
  }

  private openHelp(): void {
    // TODO: Implementar ayuda
    console.log('Open help');
  }

  // ====== Acciones de Consulta ======
  private saveQuery(queryData: any): void {
    // TODO: Implementar guardado de consulta
    console.log('Save query:', queryData);
  }

  private loadQuery(): void {
    // TODO: Implementar carga de consulta
    console.log('Load query');
  }

  private exportQueryResults(): void {
    // TODO: Implementar exportación de resultados
    console.log('Export query results');
  }

  private importQuery(): void {
    // TODO: Implementar importación de consulta
    console.log('Import query');
  }

  // ====== Utilidades ======
  clearError(): void {
    this.error.set(null);
  }

  getTableIcon(table: TableDto): string {
    switch (table.kind.toLowerCase()) {
      case 'table': return '📋';
      case 'view': return '👁️';
      case 'materialized view': return '📊';
      case 'sequence': return '🔢';
      case 'index': return '📇';
      default: return '📄';
    }
  }

  getTableRowCount(table: TableDto): number {
    return (table as any).rowCount || 0;
  }

  // ====== Getters para el template ======
  get showOverview(): boolean {
    return this.activeView() === 'overview';
  }

  get showSqlConsole(): boolean {
    return this.activeView() === 'sql';
  }

  get showTables(): boolean {
    return this.activeView() === 'tables';
  }

  get showData(): boolean {
    return this.activeView() === 'data';
  }
}