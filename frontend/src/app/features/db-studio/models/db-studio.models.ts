// Tipos compartidos del módulo DB Studio (alineados con el contrato del backend, camelCase).

export interface SchemaDto { name: string; tables: number; description?: string; }

export interface TableDto {
  schema: string; name: string; kind: string; rows: number; size?: string; description?: string;
}

export interface ViewDto { schema: string; name: string; materialized: boolean; }

export interface FunctionDto {
  schema: string; name: string; arguments: string; returnType: string; kind: string; language: string;
}

export interface RoutineSourceDto { schema: string; name: string; definition: string; }

export interface ColumnDto {
  name: string; dataType: string; isNullable: boolean; default?: string | null; isPrimaryKey: boolean;
  maxLength?: number; precision?: number; scale?: number; isIdentity?: boolean; comment?: string;
}

export interface IndexDto { name: string; type: string; columns: string[]; isUnique: boolean; isPrimary: boolean; }

export interface ForeignKeyDto {
  name: string; column: string; referencedTable: string; referencedColumn: string; onDelete: string; onUpdate: string;
}

export interface TableStatsDto {
  tableName: string; schemaName: string; rowCount: number; tableSize: string; indexSize: string;
  totalSize: string; lastAnalyzed?: string;
}

export interface TableDetailsDto {
  table: TableDto; columns: ColumnDto[]; indexes: IndexDto[]; foreignKeys: ForeignKeyDto[]; stats: TableStatsDto;
}

export interface QueryPageDto {
  rows: Record<string, unknown>[]; count: number; limit: number; offset: number;
  columns?: string[]; executionTime?: number;
}

export interface QueryResultDto {
  success: boolean; data?: QueryPageDto; error?: string; affectedRows?: number; executionTime?: number;
}

export interface SqlClassificationDto {
  kind: string; isReadOnly: boolean; requiresConfirmation: boolean; reason?: string | null;
}

export interface DatabaseAnalysisDto {
  totalSchemas: number; totalTables: number; totalRows: number; totalSize: string;
}

// ---- Permisos / acceso ----
export type AccessLevel = 'read' | 'write';

export interface MyAccessItem { schema: string; object: string; accessLevel: AccessLevel; }
export interface MyAccessDto { isAdmin: boolean; objects: MyAccessItem[]; }

export interface ObjectGrantDto {
  id: number; userId: string; companyId: number; schema: string; object: string;
  accessLevel: AccessLevel; grantedByUserId: string; grantedAtUtc: string;
}

export interface GrantRequest { userId: string; schema: string; object: string; accessLevel: AccessLevel; }

// ---- Concurrencia ----
export interface ActivitySession {
  pid: number; userName?: string; applicationName?: string; clientAddr?: string; state?: string;
  waitEventType?: string; waitEvent?: string; query?: string; queryStart?: string; xactStart?: string;
  backendStart?: string; blockedBy: number[]; isCurrentSession: boolean;
}

export interface ActivitySnapshot {
  maxConnections: number; totalConnections: number; activeConnections: number; idleConnections: number;
  idleInTransaction: number; blockedConnections: number; sessions: ActivitySession[];
}

export interface PoolStats {
  poolMinSize: number; poolMaxSize: number; dbStudioConnections: number; applicationName: string;
}

export interface LockDto {
  pid: number; lockType?: string; mode?: string; granted: boolean; relation?: string; query?: string;
}

// ---- Requests de edición ----
export interface CreateColumn {
  name: string; type: string; nullable: boolean; default?: string | null;
  identity?: 'always' | 'by_default' | null; maxLength?: number; precision?: number; scale?: number;
}
export interface CreateTableRequest {
  schema: string; table: string; columns: CreateColumn[]; primaryKey?: string[] | null;
}
export interface AddColumnRequest {
  name: string; type: string; nullable: boolean; default?: string | null; maxLength?: number; precision?: number; scale?: number;
}
export interface CreateViewRequest { schema: string; name: string; selectSql: string; orReplace?: boolean; materialized?: boolean; }
