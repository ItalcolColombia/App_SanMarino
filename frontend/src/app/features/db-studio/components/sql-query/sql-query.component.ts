// src/app/features/db-studio/components/sql-query/sql-query.component.ts
import { Component, Input, Output, EventEmitter, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { 
  faPlay, faStop, faSave, faTrash, faCopy, faDownload,
  faDatabase, faTable, faHistory, faCode, faCheck, faTimes
} from '@fortawesome/free-solid-svg-icons';
import { Subject, takeUntil } from 'rxjs';

import { DbStudioService, QueryResultDto, QueryPageDto } from '../../data/db-studio.service';

export interface SqlQueryEvent {
  type: 'query-executed' | 'query-saved' | 'query-deleted' | 'query-copied';
  data?: any;
}

export interface SavedQuery {
  id: string;
  name: string;
  sql: string;
  type: 'SELECT' | 'INSERT' | 'UPDATE' | 'DELETE' | 'CREATE' | 'ALTER' | 'DROP' | 'OTHER';
  createdAt: Date;
  lastExecuted?: Date;
}

@Component({
  selector: 'app-sql-query',
  standalone: true,
  imports: [CommonModule, FormsModule, FontAwesomeModule],
  templateUrl: './sql-query.component.html',
  styleUrls: ['./sql-query.component.scss']
})
export class SqlQueryComponent implements OnInit, OnDestroy {
  @Input() selectedSchema: string = 'public';
  @Input() loading: boolean = false;

  @Output() action = new EventEmitter<SqlQueryEvent>();

  // Iconos
  faPlay = faPlay;
  faStop = faStop;
  faSave = faSave;
  faTrash = faTrash;
  faCopy = faCopy;
  faDownload = faDownload;
  faDatabase = faDatabase;
  faTable = faTable;
  faHistory = faHistory;
  faCode = faCode;
  faCheck = faCheck;
  faTimes = faTimes;

  // Estado
  sqlQuery = signal<string>('');
  queryResult = signal<QueryResultDto | null>(null);
  isExecuting = signal<boolean>(false);
  savedQueries = signal<SavedQuery[]>([]);
  queryHistory = signal<QueryResultDto[]>([]);
  selectedQuery = signal<SavedQuery | null>(null);
  
  // Computed
  queryType = computed(() => {
    const query = this.sqlQuery().trim().toUpperCase();
    if (query.startsWith('SELECT')) return 'SELECT';
    if (query.startsWith('INSERT')) return 'INSERT';
    if (query.startsWith('UPDATE')) return 'UPDATE';
    if (query.startsWith('DELETE')) return 'DELETE';
    if (query.startsWith('CREATE')) return 'CREATE';
    if (query.startsWith('ALTER')) return 'ALTER';
    if (query.startsWith('DROP')) return 'DROP';
    return 'OTHER';
  });

  hasResults = computed(() => {
    const result = this.queryResult();
    return result?.success && result.data && result.data.rows.length > 0;
  });

  private destroy$ = new Subject<void>();

  constructor(private dbService: DbStudioService) {}

  ngOnInit(): void {
    this.loadSavedQueries();
    this.loadQueryHistory();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ===================== EJECUCIÓN DE QUERIES =====================

  async executeQuery(): Promise<void> {
    const query = this.sqlQuery().trim();
    if (!query) return;

    this.isExecuting.set(true);
    
    try {
      const queryType = this.queryType();
      
      if (queryType === 'SELECT') {
        await this.executeSelectQuery(query);
      } else {
        await this.executeOtherQuery(query);
      }
      
      this.addToHistory();
      this.action.emit({ type: 'query-executed', data: { query, result: this.queryResult() } });
      
    } catch (error) {
      console.error('Error executing query:', error);
      this.queryResult.set({
        success: false,
        error: error instanceof Error ? error.message : 'Error desconocido'
      });
    } finally {
      this.isExecuting.set(false);
    }
  }

  private async executeSelectQuery(query: string): Promise<void> {
    const result = await this.dbService.runSelect({
      sql: query,
      limit: 1000,
      offset: 0
    }).pipe(takeUntil(this.destroy$)).toPromise();

    this.queryResult.set({
      success: true,
      data: result || undefined,
      executionTime: result?.executionTime
    });
  }

  private async executeOtherQuery(query: string): Promise<void> {
    const result = await this.dbService.executeQuery({
      sql: query
    }).pipe(takeUntil(this.destroy$)).toPromise();

    this.queryResult.set(result || null);
  }

  stopExecution(): void {
    this.isExecuting.set(false);
  }

  // ===================== GESTIÓN DE QUERIES GUARDADAS =====================

  saveQuery(name: string): void {
    const query = this.sqlQuery().trim();
    if (!query || !name) return;

    const savedQuery: SavedQuery = {
      id: this.generateId(),
      name,
      sql: query,
      type: this.queryType(),
      createdAt: new Date(),
      lastExecuted: new Date()
    };

    const queries = [...this.savedQueries(), savedQuery];
    this.savedQueries.set(queries);
    this.saveToLocalStorage();

    this.action.emit({ type: 'query-saved', data: savedQuery });
  }

  loadSavedQuery(query: SavedQuery): void {
    this.sqlQuery.set(query.sql);
    this.selectedQuery.set(query);
  }

  deleteSavedQuery(queryId: string): void {
    const queries = this.savedQueries().filter(q => q.id !== queryId);
    this.savedQueries.set(queries);
    this.saveToLocalStorage();

    if (this.selectedQuery()?.id === queryId) {
      this.selectedQuery.set(null);
    }

    this.action.emit({ type: 'query-deleted', data: queryId });
  }

  copyQueryToClipboard(): void {
    const query = this.sqlQuery();
    navigator.clipboard.writeText(query).then(() => {
      this.action.emit({ type: 'query-copied', data: query });
    });
  }

  // ===================== HISTORIAL =====================

  private addToHistory(): void {
    const result = this.queryResult();
    if (!result) return;

    const history = [result, ...this.queryHistory().slice(0, 49)]; // Mantener últimos 50
    this.queryHistory.set(history);
    this.saveHistoryToLocalStorage();
  }

  loadFromHistory(result: QueryResultDto): void {
    this.queryResult.set(result);
  }

  clearHistory(): void {
    this.queryHistory.set([]);
    this.saveHistoryToLocalStorage();
  }

  // ===================== EXPORTACIÓN =====================

  exportResults(): void {
    const result = this.queryResult();
    if (!result?.data?.rows) return;

    const csv = this.convertToCSV(result.data.rows, result.data.columns || []);
    this.downloadCSV(csv, 'query-results.csv');
  }

  private convertToCSV(data: Record<string, unknown>[], columns: string[]): string {
    if (data.length === 0) return '';

    const headers = columns.length > 0 ? columns : Object.keys(data[0]);
    const csvRows = [headers.join(',')];

    for (const row of data) {
      const values = headers.map(header => {
        const value = row[header];
        if (value === null || value === undefined) return '';
        if (typeof value === 'string' && value.includes(',')) {
          return `"${value.replace(/"/g, '""')}"`;
        }
        return String(value);
      });
      csvRows.push(values.join(','));
    }

    return csvRows.join('\n');
  }

  private downloadCSV(csv: string, filename: string): void {
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const link = document.createElement('a');
    const url = URL.createObjectURL(blob);
    link.setAttribute('href', url);
    link.setAttribute('download', filename);
    link.style.visibility = 'hidden';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }

  // ===================== UTILIDADES =====================

  private generateId(): string {
    return Date.now().toString(36) + Math.random().toString(36).substr(2);
  }

  private loadSavedQueries(): void {
    try {
      const saved = localStorage.getItem('db-studio-saved-queries');
      if (saved) {
        const queries = JSON.parse(saved).map((q: any) => ({
          ...q,
          createdAt: new Date(q.createdAt),
          lastExecuted: q.lastExecuted ? new Date(q.lastExecuted) : undefined
        }));
        this.savedQueries.set(queries);
      }
    } catch (error) {
      console.error('Error loading saved queries:', error);
    }
  }

  private saveToLocalStorage(): void {
    try {
      localStorage.setItem('db-studio-saved-queries', JSON.stringify(this.savedQueries()));
    } catch (error) {
      console.error('Error saving queries:', error);
    }
  }

  private loadQueryHistory(): void {
    try {
      const history = localStorage.getItem('db-studio-query-history');
      if (history) {
        this.queryHistory.set(JSON.parse(history));
      }
    } catch (error) {
      console.error('Error loading query history:', error);
    }
  }

  private saveHistoryToLocalStorage(): void {
    try {
      localStorage.setItem('db-studio-query-history', JSON.stringify(this.queryHistory()));
    } catch (error) {
      console.error('Error saving query history:', error);
    }
  }

  formatExecutionTime(time?: number): string {
    if (!time) return '';
    return `${time}ms`;
  }

  formatRowCount(count?: number): string {
    if (!count) return '0';
    return count.toLocaleString('es-ES');
  }

  // Helper methods for template
  getObjectKeys(obj: Record<string, unknown>): string[] {
    return Object.keys(obj);
  }

  convertToString(value: unknown): string {
    return String(value);
  }
}
