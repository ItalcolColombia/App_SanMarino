// src/app/features/db-studio/components/db-studio-sql-console/db-studio-sql-console.component.ts
import { Component, Input, Output, EventEmitter, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { 
  faPlay, faStop, faSave, faFolderOpen, faTrash, faCopy,
  faDownload, faUpload, faHistory, faLightbulb, faExclamationTriangle,
  faCheckCircle, faTimesCircle, faClock, faDatabase
} from '@fortawesome/free-solid-svg-icons';
import { Subject, takeUntil } from 'rxjs';

import { DbStudioService, QueryPageDto, QueryResultDto } from '../../data/db-studio.service';

export interface DbStudioSqlConsoleEvent {
  type: 'execute' | 'save' | 'load' | 'clear' | 'export' | 'import';
  data?: any;
}

interface QueryHistory {
  id: string;
  sql: string;
  params: string;
  timestamp: Date;
  executionTime?: number;
  success: boolean;
}

@Component({
  selector: 'app-db-studio-sql-console',
  standalone: true,
  imports: [CommonModule, FormsModule, FontAwesomeModule],
  templateUrl: './db-studio-sql-console.component.html',
  styleUrls: ['./db-studio-sql-console.component.scss']
})
export class DbStudioSqlConsoleComponent implements OnInit, OnDestroy {
  @Input() loading: boolean = false;
  @Input() selectedSchema: string = 'public';

  @Output() action = new EventEmitter<DbStudioSqlConsoleEvent>();

  // Iconos
  faPlay = faPlay;
  faStop = faStop;
  faSave = faSave;
  faFolderOpen = faFolderOpen;
  faTrash = faTrash;
  faCopy = faCopy;
  faDownload = faDownload;
  faUpload = faUpload;
  faHistory = faHistory;
  faLightbulb = faLightbulb;
  faExclamationTriangle = faExclamationTriangle;
  faCheckCircle = faCheckCircle;
  faTimesCircle = faTimesCircle;
  faClock = faClock;
  faDatabase = faDatabase;

  // Estado
  sql = signal<string>('SELECT * FROM information_schema.tables LIMIT 10;');
  paramsText = signal<string>('{}');
  limit = signal<number>(100);
  offset = signal<number>(0);
  
  queryResult = signal<QueryPageDto | null>(null);
  queryError = signal<string | null>(null);
  executionTime = signal<number>(0);
  
  queryHistory = signal<QueryHistory[]>([]);
  showHistory = signal<boolean>(false);
  showParams = signal<boolean>(true);
  
  // Computed
  isValidJson = computed(() => {
    try {
      JSON.parse(this.paramsText());
      return true;
    } catch {
      return false;
    }
  });

  hasResults = computed(() => {
    const result = this.queryResult();
    return result && result.rows && result.rows.length > 0;
  });

  totalRows = computed(() => {
    const result = this.queryResult();
    return result ? result.rows.length : 0;
  });

  private destroy$ = new Subject<void>();

  constructor(private dbService: DbStudioService) {}

  ngOnInit(): void {
    this.loadQueryHistory();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadQueryHistory(): void {
    const saved = localStorage.getItem('db-studio-query-history');
    if (saved) {
      try {
        const history = JSON.parse(saved).map((h: any) => ({
          ...h,
          timestamp: new Date(h.timestamp)
        }));
        this.queryHistory.set(history);
      } catch (err) {
        console.error('Error loading query history:', err);
      }
    }
  }

  private saveQueryHistory(): void {
    const history = this.queryHistory().slice(-50); // Mantener solo los últimos 50
    localStorage.setItem('db-studio-query-history', JSON.stringify(history));
  }

  executeQuery(): void {
    if (!this.sql().trim()) return;

    const startTime = Date.now();
    this.queryError.set(null);

    let params: Record<string, any> = {};
    try {
      params = JSON.parse(this.paramsText());
    } catch {
      this.queryError.set('Parámetros no son JSON válido.');
      return;
    }

    this.dbService.runSelect({
      sql: this.sql(),
      params,
      limit: this.limit(),
      offset: this.offset()
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: (result) => {
        const endTime = Date.now();
        this.executionTime.set(endTime - startTime);
        this.queryResult.set(result);
        
        // Guardar en historial
        this.addToHistory({
          sql: this.sql(),
          params: this.paramsText(),
          executionTime: endTime - startTime,
          success: true
        });
      },
      error: (err) => {
        const endTime = Date.now();
        this.executionTime.set(endTime - startTime);
        this.queryError.set(err?.error?.message ?? err.message ?? 'Error ejecutando consulta');
        
        // Guardar en historial
        this.addToHistory({
          sql: this.sql(),
          params: this.paramsText(),
          executionTime: endTime - startTime,
          success: false
        });
      }
    });
  }

  private addToHistory(query: Partial<QueryHistory>): void {
    const historyItem: QueryHistory = {
      id: Date.now().toString(),
      sql: query.sql || '',
      params: query.params || '{}',
      timestamp: new Date(),
      executionTime: query.executionTime,
      success: query.success || false
    };

    const currentHistory = this.queryHistory();
    this.queryHistory.set([historyItem, ...currentHistory]);
    this.saveQueryHistory();
  }

  clearQuery(): void {
    this.sql.set('');
    this.paramsText.set('{}');
    this.queryResult.set(null);
    this.queryError.set(null);
    this.executionTime.set(0);
  }

  loadFromHistory(historyItem: QueryHistory): void {
    this.sql.set(historyItem.sql);
    this.paramsText.set(historyItem.params);
    this.showHistory.set(false);
  }

  deleteFromHistory(historyItem: QueryHistory): void {
    const currentHistory = this.queryHistory();
    this.queryHistory.set(currentHistory.filter(h => h.id !== historyItem.id));
    this.saveQueryHistory();
  }

  copyQuery(): void {
    navigator.clipboard.writeText(this.sql()).then(() => {
      // TODO: Mostrar notificación de éxito
    });
  }

  exportResults(): void {
    const result = this.queryResult();
    if (!result) return;

    const csv = this.convertToCSV(result.rows);
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `query_results_${new Date().toISOString().split('T')[0]}.csv`;
    a.click();
    window.URL.revokeObjectURL(url);
  }

  private convertToCSV(data: any[]): string {
    if (data.length === 0) return '';

    const headers = Object.keys(data[0]);
    const csvHeaders = headers.join(',');
    const csvRows = data.map(row => 
      headers.map(header => {
        const value = row[header];
        return typeof value === 'string' && value.includes(',') 
          ? `"${value}"` 
          : value;
      }).join(',')
    );

    return [csvHeaders, ...csvRows].join('\n');
  }

  onSave(): void {
    this.action.emit({ type: 'save', data: { sql: this.sql(), params: this.paramsText() } });
  }

  onLoad(): void {
    this.action.emit({ type: 'load' });
  }

  onExport(): void {
    this.exportResults();
    this.action.emit({ type: 'export' });
  }

  onImport(): void {
    this.action.emit({ type: 'import' });
  }

  // Utilidades
  formatExecutionTime(time: number): string {
    if (time < 1000) return `${time}ms`;
    return `${(time / 1000).toFixed(2)}s`;
  }

  formatNumber(num: number): string {
    return num.toLocaleString('es-ES');
  }

  getQueryKeys(): string[] {
    const result = this.queryResult();
    if (result?.rows && result.rows.length > 0) {
      return Object.keys(result.rows[0]);
    }
    return [];
  }

  toggleHistory(): void {
    this.showHistory.set(!this.showHistory());
  }

  toggleParams(): void {
    this.showParams.set(!this.showParams());
  }

  // Sugerencias SQL básicas
  getSqlSuggestions(): string[] {
    const suggestions = [
      'SELECT * FROM information_schema.tables LIMIT 10;',
      'SELECT table_name, table_type FROM information_schema.tables WHERE table_schema = \'public\';',
      'SELECT column_name, data_type FROM information_schema.columns WHERE table_name = \'users\';',
      'SELECT COUNT(*) as total_rows FROM users;',
      'SELECT * FROM pg_stat_activity;'
    ];
    
    return suggestions;
  }

  insertSuggestion(suggestion: string): void {
    this.sql.set(suggestion);
  }
}
