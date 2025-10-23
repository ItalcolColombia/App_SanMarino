// src/app/features/db-studio/components/db-studio-sidebar/db-studio-sidebar.component.ts
import { Component, Input, Output, EventEmitter, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { 
  faDatabase, faTable, faSearch, faFilter, faChevronDown, 
  faChevronRight, faEye, faEdit, faTrash, faPlus, faRefresh
} from '@fortawesome/free-solid-svg-icons';
import { Subject, takeUntil } from 'rxjs';

import { DbStudioService, SchemaDto, TableDto } from '../../data/db-studio.service';

export interface DbStudioSidebarEvent {
  type: 'schema-selected' | 'table-selected' | 'table-action' | 'refresh';
  schema?: string;
  table?: TableDto;
  action?: 'view' | 'edit' | 'delete' | 'export';
}

@Component({
  selector: 'app-db-studio-sidebar',
  standalone: true,
  imports: [CommonModule, FormsModule, FontAwesomeModule],
  templateUrl: './db-studio-sidebar.component.html',
  styleUrls: ['./db-studio-sidebar.component.scss']
})
export class DbStudioSidebarComponent implements OnInit, OnDestroy {
  @Input() selectedSchema: string = 'public';
  @Input() selectedTable: string | null = null;
  @Input() loading: boolean = false;

  @Output() action = new EventEmitter<DbStudioSidebarEvent>();

  // Iconos
  faDatabase = faDatabase;
  faTable = faTable;
  faSearch = faSearch;
  faFilter = faFilter;
  faChevronDown = faChevronDown;
  faChevronRight = faChevronRight;
  faEye = faEye;
  faEdit = faEdit;
  faTrash = faTrash;
  faPlus = faPlus;
  faRefresh = faRefresh;

  // Estado
  schemas = signal<SchemaDto[]>([]);
  tables = signal<TableDto[]>([]);
  searchTerm = signal<string>('');
  expandedSchemas = signal<Set<string>>(new Set(['public']));
  
  // Computed
  filteredSchemas = computed(() => {
    const schemas = this.schemas();
    const search = this.searchTerm().toLowerCase();
    
    if (!search) return schemas;
    
    return schemas.filter(schema => 
      schema.name.toLowerCase().includes(search)
    );
  });

  filteredTables = computed(() => {
    const tables = this.tables();
    const search = this.searchTerm().toLowerCase();
    
    if (!search) return tables;
    
    return tables.filter(table => 
      table.name.toLowerCase().includes(search) ||
      table.kind.toLowerCase().includes(search)
    );
  });

  private destroy$ = new Subject<void>();

  constructor(private dbService: DbStudioService) {}

  ngOnInit(): void {
    this.loadSchemas();
    this.loadTables();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
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

  onSchemaSelect(schema: string): void {
    if (this.selectedSchema === schema) return;
    
    this.selectedSchema = schema;
    this.selectedTable = null;
    this.loadTables();
    this.action.emit({ type: 'schema-selected', schema });
  }

  onTableSelect(table: TableDto): void {
    this.selectedTable = table.name;
    this.action.emit({ type: 'table-selected', table });
  }

  onTableAction(table: TableDto, action: 'view' | 'edit' | 'delete' | 'export'): void {
    this.action.emit({ type: 'table-action', table, action });
  }

  onRefresh(): void {
    this.loadSchemas();
    this.loadTables();
    this.action.emit({ type: 'refresh' });
  }

  onSearchChange(searchTerm: string): void {
    this.searchTerm.set(searchTerm);
  }

  toggleSchemaExpansion(schema: string): void {
    const expanded = new Set(this.expandedSchemas());
    if (expanded.has(schema)) {
      expanded.delete(schema);
    } else {
      expanded.add(schema);
    }
    this.expandedSchemas.set(expanded);
  }

  isSchemaExpanded(schema: string): boolean {
    return this.expandedSchemas().has(schema);
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
    if (!size) return '';
    return size;
  }

  getSchemaIcon(schema: SchemaDto): string {
    if (schema.name === 'public') return '🏠';
    if (schema.name === 'information_schema') return 'ℹ️';
    if (schema.name.startsWith('pg_')) return '⚙️';
    return '📁';
  }
}
