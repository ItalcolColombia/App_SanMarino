// src/app/features/db-studio/components/table-management/table-management.component.ts
import { Component, Input, Output, EventEmitter, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { 
  faTable, faEye, faEdit, faTrash, faPlus, faRefresh, faSearch,
  faDatabase, faColumns, faKey, faDownload, faUpload, faTimes
} from '@fortawesome/free-solid-svg-icons';
import { Subject, takeUntil } from 'rxjs';

import { DbStudioService, TableDto, TableStatsDto, ColumnDto, IndexDto, ForeignKeyDto } from '../../data/db-studio.service';

export interface TableManagementEvent {
  type: 'table-selected' | 'table-action' | 'create-table' | 'refresh';
  table?: TableDto;
  action?: 'view' | 'edit' | 'delete' | 'export' | 'import' | 'structure';
}

@Component({
  selector: 'app-table-management',
  standalone: true,
  imports: [CommonModule, FormsModule, FontAwesomeModule],
  templateUrl: './table-management.component.html',
  styleUrls: ['./table-management.component.scss']
})
export class TableManagementComponent implements OnInit, OnDestroy {
  @Input() selectedSchema: string = 'public';
  @Input() loading: boolean = false;

  @Output() action = new EventEmitter<TableManagementEvent>();

  // Iconos
  faTable = faTable;
  faEye = faEye;
  faEdit = faEdit;
  faTrash = faTrash;
  faPlus = faPlus;
  faRefresh = faRefresh;
  faSearch = faSearch;
  faDatabase = faDatabase;
  faColumns = faColumns;
  faKey = faKey;
  faDownload = faDownload;
  faUpload = faUpload;
  faTimes = faTimes;

  // Estado
  tables = signal<TableDto[]>([]);
  tableStats = signal<Map<string, TableStatsDto>>(new Map());
  searchTerm = signal<string>('');
  selectedTable = signal<TableDto | null>(null);
  viewMode = signal<'grid' | 'list'>('grid');
  sortBy = signal<'name' | 'size' | 'rows' | 'type'>('name');
  sortOrder = signal<'asc' | 'desc'>('asc');

  // Computed
  filteredTables = computed(() => {
    const tables = this.tables();
    const search = this.searchTerm().toLowerCase();
    const sortBy = this.sortBy();
    const sortOrder = this.sortOrder();

    let filtered = tables.filter(table => 
      table.name.toLowerCase().includes(search) ||
      table.kind.toLowerCase().includes(search)
    );

    // Ordenar
    filtered.sort((a, b) => {
      let aValue: any, bValue: any;
      
      switch (sortBy) {
        case 'name':
          aValue = a.name;
          bValue = b.name;
          break;
        case 'size':
          aValue = this.parseSize(a.size || '0');
          bValue = this.parseSize(b.size || '0');
          break;
        case 'rows':
          aValue = a.rows || 0;
          bValue = b.rows || 0;
          break;
        case 'type':
          aValue = a.kind;
          bValue = b.kind;
          break;
        default:
          return 0;
      }

      if (sortOrder === 'asc') {
        return aValue > bValue ? 1 : -1;
      } else {
        return aValue < bValue ? 1 : -1;
      }
    });

    return filtered;
  });

  private destroy$ = new Subject<void>();

  constructor(private dbService: DbStudioService) {}

  ngOnInit(): void {
    this.loadTables();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ===================== CARGA DE DATOS =====================

  private loadTables(): void {
    this.dbService.getTables(this.selectedSchema).pipe(takeUntil(this.destroy$)).subscribe({
      next: (tables) => {
        this.tables.set(tables);
        this.loadTableStats(tables);
      },
      error: (err) => {
        console.error('Error loading tables:', err);
      }
    });
  }

  private loadTableStats(tables: TableDto[]): void {
    const statsMap = new Map<string, TableStatsDto>();
    
    tables.forEach(table => {
      this.dbService.getTableStats(table.name, table.schema).pipe(takeUntil(this.destroy$)).subscribe({
        next: (stats) => {
          statsMap.set(table.name, stats);
          this.tableStats.set(new Map(statsMap));
        },
        error: (err) => {
          console.error(`Error loading stats for table ${table.name}:`, err);
        }
      });
    });
  }

  // ===================== ACCIONES DE TABLA =====================

  selectTable(table: TableDto): void {
    this.selectedTable.set(table);
    this.action.emit({ type: 'table-selected', table });
  }

  onTableAction(table: TableDto, action: 'view' | 'edit' | 'delete' | 'export' | 'import' | 'structure'): void {
    this.action.emit({ type: 'table-action', table, action });
  }

  onCreateTable(): void {
    this.action.emit({ type: 'create-table' });
  }

  onRefresh(): void {
    this.loadTables();
    this.action.emit({ type: 'refresh' });
  }

  // ===================== BÚSQUEDA Y FILTROS =====================

  onSearchChange(searchTerm: string): void {
    this.searchTerm.set(searchTerm);
  }

  onSortChange(sortBy: 'name' | 'size' | 'rows' | 'type'): void {
    if (this.sortBy() === sortBy) {
      this.sortOrder.set(this.sortOrder() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortBy.set(sortBy);
      this.sortOrder.set('asc');
    }
  }

  toggleViewMode(): void {
    this.viewMode.set(this.viewMode() === 'grid' ? 'list' : 'grid');
  }

  // ===================== UTILIDADES =====================

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

  getTableTypeClass(table: TableDto): string {
    switch (table.kind.toLowerCase()) {
      case 'table': return 'type-table';
      case 'view': return 'type-view';
      case 'materialized view': return 'type-materialized';
      case 'sequence': return 'type-sequence';
      case 'index': return 'type-index';
      default: return 'type-other';
    }
  }

  formatTableSize(size?: string): string {
    if (!size) return 'N/A';
    return size;
  }

  formatRowCount(count?: number): string {
    if (!count) return '0';
    return count.toLocaleString('es-ES');
  }

  private parseSize(sizeStr: string): number {
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

  getTableStats(table: TableDto): TableStatsDto | undefined {
    return this.tableStats().get(table.name);
  }
}
