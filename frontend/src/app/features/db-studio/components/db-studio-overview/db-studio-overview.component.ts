// src/app/features/db-studio/components/db-studio-overview/db-studio-overview.component.ts
import { Component, Input, Output, EventEmitter, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { 
  faDatabase, faTable, faChartBar, faServer, faHdd, 
  faUsers, faClock, faArrowUp, faArrowDown, faRefresh
} from '@fortawesome/free-solid-svg-icons';
import { Subject, takeUntil } from 'rxjs';

import { DbStudioService, DatabaseAnalysisDto, SchemaDto, TableDto } from '../../data/db-studio.service';

export interface DbStudioOverviewEvent {
  type: 'refresh' | 'analyze' | 'schema-selected' | 'table-selected';
  data?: any;
}

@Component({
  selector: 'app-db-studio-overview',
  standalone: true,
  imports: [CommonModule, FontAwesomeModule],
  templateUrl: './db-studio-overview.component.html',
  styleUrls: ['./db-studio-overview.component.scss']
})
export class DbStudioOverviewComponent implements OnInit, OnDestroy {
  @Input() loading: boolean = false;
  @Input() selectedSchema: string = 'public';

  @Output() action = new EventEmitter<DbStudioOverviewEvent>();

  // Iconos
  faDatabase = faDatabase;
  faTable = faTable;
  faChartBar = faChartBar;
  faServer = faServer;
  faHdd = faHdd;
  faUsers = faUsers;
  faClock = faClock;
  faTrendingUp = faArrowUp;
  faTrendingDown = faArrowDown;
  faRefresh = faRefresh;

  // Estado
  databaseAnalysis = signal<DatabaseAnalysisDto | null>(null);
  schemas = signal<SchemaDto[]>([]);
  tables = signal<TableDto[]>([]);
  lastAnalysis = signal<Date | null>(null);

  // Computed
  topTablesBySize = computed(() => {
    const tables = this.tables();
    return tables
      .filter(t => t.size)
      .sort((a, b) => this.parseSize(b.size!) - this.parseSize(a.size!))
      .slice(0, 5);
  });

  topTablesByRows = computed(() => {
    const tables = this.tables();
    return tables
      .filter(t => t.rows)
      .sort((a, b) => (b.rows || 0) - (a.rows || 0))
      .slice(0, 5);
  });

  private destroy$ = new Subject<void>();

  constructor(private dbService: DbStudioService) {}

  ngOnInit(): void {
    this.loadData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadData(): void {
    this.loadSchemas();
    this.loadTables();
    this.analyzeDatabase();
  }

  private loadSchemas(): void {
    this.dbService.getSchemas().pipe(takeUntil(this.destroy$)).subscribe({
      next: (schemas) => {
        this.schemas.set(schemas);
      },
      error: (err) => {
        console.error('Error loading schemas:', err);
      }
    });
  }

  private loadTables(): void {
    this.dbService.getTables(this.selectedSchema).pipe(takeUntil(this.destroy$)).subscribe({
      next: (tables) => {
        this.tables.set(tables);
      },
      error: (err) => {
        console.error('Error loading tables:', err);
      }
    });
  }

  private analyzeDatabase(): void {
    this.dbService.analyzeDatabase().pipe(takeUntil(this.destroy$)).subscribe({
      next: (analysis) => {
        this.databaseAnalysis.set(analysis);
        this.lastAnalysis.set(new Date());
      },
      error: (err) => {
        console.error('Error analyzing database:', err);
      }
    });
  }

  onRefresh(): void {
    this.loadData();
    this.action.emit({ type: 'refresh' });
  }

  onAnalyze(): void {
    this.analyzeDatabase();
    this.action.emit({ type: 'analyze' });
  }

  onSchemaSelect(schema: string): void {
    this.selectedSchema = schema;
    this.loadTables();
    this.action.emit({ type: 'schema-selected', data: schema });
  }

  onTableSelect(table: TableDto): void {
    this.action.emit({ type: 'table-selected', data: table });
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

  parseSize(sizeStr: string): number {
    if (!sizeStr) return 0;
    
    const match = sizeStr.match(/(\d+(?:\.\d+)?)\s*(KB|MB|GB|TB)/i);
    if (!match) return 0;
    
    const value = parseFloat(match[1]);
    const unit = match[2].toUpperCase();
    
    const multipliers: Record<string, number> = {
      'KB': 1024,
      'MB': 1024 * 1024,
      'GB': 1024 * 1024 * 1024,
      'TB': 1024 * 1024 * 1024 * 1024
    };
    
    return value * (multipliers[unit] || 1);
  }

  getSchemaIcon(schema: SchemaDto): string {
    if (schema.name === 'public') return '🏠';
    if (schema.name === 'information_schema') return 'ℹ️';
    if (schema.name.startsWith('pg_')) return '⚙️';
    return '📁';
  }

  getTableIcon(table: TableDto): string {
    switch (table.kind.toLowerCase()) {
      case 'table': return '📋';
      case 'view': return '👁️';
      case 'materialized view': return '📊';
      default: return '📄';
    }
  }

  getTrendIcon(value: number, previous?: number): string {
    if (!previous) return '';
    return value > previous ? 'trending-up' : 'trending-down';
  }

  getTrendColor(value: number, previous?: number): string {
    if (!previous) return 'neutral';
    return value > previous ? 'positive' : 'negative';
  }

  formatLastAnalysis(): string {
    const last = this.lastAnalysis();
    if (!last) return 'Nunca';
    
    const now = new Date();
    const diff = now.getTime() - last.getTime();
    const minutes = Math.floor(diff / (1000 * 60));
    
    if (minutes < 1) return 'Hace un momento';
    if (minutes < 60) return `Hace ${minutes} min`;
    
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `Hace ${hours}h`;
    
    const days = Math.floor(hours / 24);
    return `Hace ${days}d`;
  }
}
