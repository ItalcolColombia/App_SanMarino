import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';

const API = '/api/DbStudio';

// =====================================================
// INTERFACES PRINCIPALES
// =====================================================

export interface SchemaDto { 
  name: string; 
  tables: number; 
  description?: string;
}

export interface TableDto { 
  schema: string; 
  name: string; 
  kind: string; 
  rows: number;
  size?: string;
  description?: string;
}

export interface ColumnDto { 
  name: string; 
  dataType: string; 
  isNullable: boolean; 
  default?: string | null; 
  isPrimaryKey: boolean;
  maxLength?: number;
  precision?: number;
  scale?: number;
  isIdentity?: boolean;
  comment?: string;
}

export interface IndexDto {
  name: string;
  type: string;
  columns: string[];
  isUnique: boolean;
  isPrimary: boolean;
}

export interface ForeignKeyDto {
  name: string;
  column: string;
  referencedTable: string;
  referencedColumn: string;
  onDelete: string;
  onUpdate: string;
}

export interface QueryPageDto { 
  rows: Record<string, unknown>[]; 
  count: number; 
  limit: number; 
  offset: number;
  columns?: string[];
  executionTime?: number;
}

export interface QueryResultDto {
  success: boolean;
  data?: QueryPageDto;
  error?: string;
  affectedRows?: number;
  executionTime?: number;
}

// =====================================================
// DTOs PARA CREACIÓN Y MODIFICACIÓN
// =====================================================

export interface CreateTableDto {
  schema: string;
  table: string;
  columns: Array<{
    name: string;
    type: string;
    nullable: boolean;
    default?: string | null;
    identity?: 'always' | 'by_default' | null;
    maxLength?: number;
    precision?: number;
    scale?: number;
    comment?: string;
  }>;
  primaryKey?: string[] | null;
  uniques?: string[][] | null;
  indexes?: Array<{
    name: string;
    columns: string[];
    unique?: boolean;
  }>;
  foreignKeys?: Array<{
    name: string;
    column: string;
    referencedTable: string;
    referencedColumn: string;
    onDelete?: string;
    onUpdate?: string;
  }>;
}

export interface AddColumnDto { 
  name: string; 
  type: string; 
  nullable: boolean; 
  default?: string | null;
  maxLength?: number;
  precision?: number;
  scale?: number;
  comment?: string;
}

export interface AlterColumnDto { 
  newType?: string | null; 
  setNotNull?: boolean | null; 
  dropNotNull?: boolean | null; 
  setDefault?: string | null; 
  dropDefault?: boolean | null;
  newMaxLength?: number | null;
  newPrecision?: number | null;
  newScale?: number | null;
  newComment?: string | null;
}

export interface SelectQueryDto { 
  sql: string; 
  params?: Record<string, any>; 
  limit?: number; 
  offset?: number; 
}

export interface ExecuteQueryDto {
  sql: string;
  params?: Record<string, any>;
}

export interface TableStatsDto {
  tableName: string;
  schemaName: string;
  rowCount: number;
  tableSize: string;
  indexSize: string;
  totalSize: string;
  lastAnalyzed?: string;
}

export interface TableDependenciesDto {
  dependencies: Array<{ table: string; schema: string; type: string }>;
  dependents: Array<{ table: string; schema: string; type: string }>;
}

export interface DatabaseAnalysisDto {
  totalSchemas: number;
  totalTables: number;
  totalRows: number;
  totalSize: string;
  schemaAnalysis: Array<{
    schemaName: string;
    tableCount: number;
    totalRows: number;
    totalSize: string;
  }>;
  largestTables: Array<{
    schemaName: string;
    tableName: string;
    rowCount: number;
    size: string;
    indexCount: number;
    foreignKeyCount: number;
  }>;
  mostIndexedTables: Array<{
    schemaName: string;
    tableName: string;
    rowCount: number;
    size: string;
    indexCount: number;
    foreignKeyCount: number;
  }>;
}

// =====================================================
// SERVICIO PRINCIPAL
// =====================================================

@Injectable({ providedIn: 'root' })
export class DbStudioService {
  private http = inject(HttpClient);

  // ===================== CONSULTAS BÁSICAS =====================
  
  getSchemas(): Observable<SchemaDto[]> {
    return this.http.get<SchemaDto[]>(`${API}/schemas`);
  }

  getTables(schema?: string): Observable<TableDto[]> {
    const params = new HttpParams({ fromObject: schema ? { schema } : {} });
    return this.http.get<TableDto[]>(`${API}/tables`, { params });
  }

  getTableDetails(schema: string, table: string): Observable<{
    table: TableDto;
    columns: ColumnDto[];
    indexes: IndexDto[];
    foreignKeys: ForeignKeyDto[];
    stats: TableStatsDto;
  }> {
    return this.http.get<any>(`${API}/tables/${encodeURIComponent(schema)}/${encodeURIComponent(table)}/details`);
  }

  getColumns(table: string, schema?: string): Observable<ColumnDto[]> {
    const url = schema
      ? `${API}/tables/${encodeURIComponent(schema)}/${encodeURIComponent(table)}/columns`
      : `${API}/tables/${encodeURIComponent(table)}/columns`;
    return this.http.get<ColumnDto[]>(url);
  }

  getIndexes(table: string, schema?: string): Observable<IndexDto[]> {
    const url = schema
      ? `${API}/tables/${encodeURIComponent(schema)}/${encodeURIComponent(table)}/indexes`
      : `${API}/tables/${encodeURIComponent(table)}/indexes`;
    return this.http.get<IndexDto[]>(url);
  }

  getForeignKeys(table: string, schema?: string): Observable<ForeignKeyDto[]> {
    const url = schema
      ? `${API}/tables/${encodeURIComponent(schema)}/${encodeURIComponent(table)}/foreign-keys`
      : `${API}/tables/${encodeURIComponent(table)}/foreign-keys`;
    return this.http.get<ForeignKeyDto[]>(url);
  }

  getTableStats(table: string, schema?: string): Observable<TableStatsDto> {
    const url = schema
      ? `${API}/tables/${encodeURIComponent(schema)}/${encodeURIComponent(table)}/stats`
      : `${API}/tables/${encodeURIComponent(table)}/stats`;
    return this.http.get<TableStatsDto>(url);
  }

  // ===================== PREVIEW Y CONSULTAS =====================

  preview(table: string, opts: { schema?: string; limit?: number; offset?: number } = {}): Observable<QueryPageDto> {
    const { schema, limit = 50, offset = 0 } = opts;
    const url = schema
      ? `${API}/tables/${encodeURIComponent(schema)}/${encodeURIComponent(table)}/preview`
      : `${API}/tables/${encodeURIComponent(table)}/preview`;
    return this.http.get<QueryPageDto>(url, { params: { limit, offset } as any });
  }

  runSelect(dto: SelectQueryDto): Observable<QueryPageDto> {
    return this.http.post<QueryPageDto>(`${API}/query/select`, {
      sql: dto.sql.trim(),
      params: dto.params ?? {},
      limit: dto.limit ?? 100,
      offset: dto.offset ?? 0
    });
  }

  executeQuery(dto: ExecuteQueryDto): Observable<QueryResultDto> {
    return this.http.post<QueryResultDto>(`${API}/query/execute`, {
      sql: dto.sql.trim(),
      params: dto.params ?? {}
    });
  }

  // ===================== CREACIÓN Y MODIFICACIÓN =====================

  createTable(dto: CreateTableDto): Observable<void> {
    return this.http.post<void>(`${API}/tables`, dto);
  }

  dropTable(schema: string, table: string, cascade: boolean = false): Observable<void> {
    return this.http.delete<void>(`${API}/tables/${encodeURIComponent(schema)}/${encodeURIComponent(table)}`, {
      params: { cascade: cascade.toString() }
    });
  }

  addColumn(schema: string, table: string, dto: AddColumnDto): Observable<void> {
    return this.http.post<void>(`${API}/tables/${encodeURIComponent(schema)}/${encodeURIComponent(table)}/columns`, dto);
  }

  alterColumn(schema: string, table: string, column: string, dto: AlterColumnDto): Observable<void> {
    return this.http.patch<void>(`${API}/tables/${encodeURIComponent(schema)}/${encodeURIComponent(table)}/columns/${encodeURIComponent(column)}`, dto);
  }

  dropColumn(schema: string, table: string, column: string): Observable<void> {
    return this.http.delete<void>(`${API}/tables/${encodeURIComponent(schema)}/${encodeURIComponent(table)}/columns/${encodeURIComponent(column)}`);
  }

  // ===================== ÍNDICES =====================

  createIndex(schema: string, table: string, dto: {
    name: string;
    columns: string[];
    unique?: boolean;
  }): Observable<void> {
    return this.http.post<void>(`${API}/tables/${encodeURIComponent(schema)}/${encodeURIComponent(table)}/indexes`, dto);
  }

  dropIndex(schema: string, table: string, indexName: string): Observable<void> {
    return this.http.delete<void>(`${API}/tables/${encodeURIComponent(schema)}/${encodeURIComponent(table)}/indexes/${encodeURIComponent(indexName)}`);
  }

  // ===================== CLAVES FORÁNEAS =====================

  createForeignKey(schema: string, table: string, dto: {
    name: string;
    column: string;
    referencedTable: string;
    referencedColumn: string;
    onDelete?: string;
    onUpdate?: string;
  }): Observable<void> {
    return this.http.post<void>(`${API}/tables/${encodeURIComponent(schema)}/${encodeURIComponent(table)}/foreign-keys`, dto);
  }

  dropForeignKey(schema: string, table: string, fkName: string): Observable<void> {
    return this.http.delete<void>(`${API}/tables/${encodeURIComponent(schema)}/${encodeURIComponent(table)}/foreign-keys/${encodeURIComponent(fkName)}`);
  }

  // ===================== DATOS =====================

  insertData(schema: string, table: string, data: Record<string, any>[]): Observable<void> {
    return this.http.post<void>(`${API}/tables/${encodeURIComponent(schema)}/${encodeURIComponent(table)}/data`, { rows: data });
  }

  updateData(schema: string, table: string, data: Record<string, any>, where: Record<string, any>): Observable<void> {
    return this.http.patch<void>(`${API}/tables/${encodeURIComponent(schema)}/${encodeURIComponent(table)}/data`, { data, where });
  }

  deleteData(schema: string, table: string, where: Record<string, any>): Observable<void> {
    return this.http.delete<void>(`${API}/tables/${encodeURIComponent(schema)}/${encodeURIComponent(table)}/data`, {
      body: { where }
    });
  }

  // ===================== UTILIDADES =====================

  getDataTypes(): Observable<string[]> {
    return this.http.get<string[]>(`${API}/data-types`);
  }

  validateSql(sql: string): Observable<{ valid: boolean; error?: string }> {
    return this.http.post<{ valid: boolean; error?: string }>(`${API}/validate-sql`, { sql });
  }

  exportTable(schema: string, table: string, format: 'csv' | 'json' | 'sql' = 'sql'): Observable<Blob> {
    return this.http.get(`${API}/tables/${encodeURIComponent(schema)}/${encodeURIComponent(table)}/export`, {
      params: { format },
      responseType: 'blob'
    });
  }

  importTable(schema: string, table: string, file: File, format: 'csv' | 'json' = 'csv'): Observable<void> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('format', format);
    
    return this.http.post<void>(`${API}/tables/${encodeURIComponent(schema)}/${encodeURIComponent(table)}/import`, formData);
  }

  // ===================== ANÁLISIS Y DEPENDENCIAS =====================

  getTableDependencies(schema: string, table: string): Observable<TableDependenciesDto> {
    return this.http.get<TableDependenciesDto>(`${API}/tables/${encodeURIComponent(schema)}/${encodeURIComponent(table)}/dependencies`);
  }

  analyzeDatabase(): Observable<DatabaseAnalysisDto> {
    return this.http.get<DatabaseAnalysisDto>(`${API}/database/analyze`);
  }

  exportSchema(schema: string): Observable<Blob> {
    return this.http.get(`${API}/schemas/${encodeURIComponent(schema)}/export`, {
      responseType: 'blob'
    });
  }
}