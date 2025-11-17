# ANÁLISIS COMPLETO: MÓDULO DE TRASLADO DE AVES

## 📋 ÍNDICE
1. [Resumen Ejecutivo](#resumen-ejecutivo)
2. [Arquitectura del Módulo](#arquitectura-del-módulo)
3. [Backend - Análisis Detallado](#backend---análisis-detallado)
4. [Frontend - Análisis Detallado](#frontend---análisis-detallado)
5. [Integración con Módulos Relacionados](#integración-con-módulos-relacionados)
6. [Flujos de Datos y Operaciones](#flujos-de-datos-y-operaciones)
7. [Base de Datos](#base-de-datos)
8. [API Endpoints](#api-endpoints)
9. [Casos de Uso Principales](#casos-de-uso-principales)

---

## 📌 RESUMEN EJECUTIVO

El módulo de **Traslado de Aves** permite registrar movimientos de aves entre ubicaciones (granjas, núcleos, galpones) y entre lotes. Este módulo es fundamental para:

- **Registrar traslados** entre granjas y dentro de granjas
- **Ajustar inventarios** de aves (sumas y restas)
- **Rastrear movimientos** históricos
- **Integrar con seguimiento diario** (levante y producción) para registrar retiros y mortalidades
- **Gestionar inventarios** en tiempo real por lote y ubicación

### Funcionalidades Principales
1. ✅ Crear movimientos de traslado entre lotes y ubicaciones
2. ✅ Procesar movimientos pendientes (actualizar inventarios)
3. ✅ Cancelar movimientos
4. ✅ Búsqueda y filtrado avanzado de movimientos
5. ✅ Dashboard de inventario con resúmenes
6. ✅ Trazabilidad completa de movimientos
7. ✅ Validación de disponibilidad de aves antes de traslado

---

## 🏗️ ARQUITECTURA DEL MÓDULO

```
┌─────────────────────────────────────────────────────────────┐
│                    MÓDULO TRASLADO DE AVES                   │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────────┐        ┌──────────────────┐         │
│  │    FRONTEND       │◄───────►│     BACKEND      │         │
│  │  (Angular)        │   HTTP  │  (ASP.NET Core)  │         │
│  └──────────────────┘         └──────────────────┘         │
│         │                              │                    │
│         │                              │                    │
│         ▼                              ▼                    │
│  ┌──────────────────┐        ┌──────────────────┐         │
│  │   COMPONENTES    │        │     SERVICIOS     │         │
│  │   • Dashboard    │        │  • MovimientoAves │         │
│  │   • TrasladoForm │        │  • InventarioAves │         │
│  │   • Lista        │        │  • Historial      │         │
│  └──────────────────┘        └──────────────────┘         │
│         │                              │                    │
│         └──────────┬───────────────────┘                    │
│                    ▼                                        │
│         ┌──────────────────┐                               │
│         │  BASE DE DATOS    │                               │
│         │  (PostgreSQL)     │                               │
│         │  • movimiento_aves│                              │
│         │  • inventario_aves│                              │
│         │  • historial_inv  │                              │
│         └──────────────────┘                               │
└─────────────────────────────────────────────────────────────┘
```

---

## 🔧 BACKEND - ANÁLISIS DETALLADO

### 1. ENTIDADES DE DOMINIO

#### `MovimientoAves` (Entity)
**Ubicación**: `backend/src/ZooSanMarino.Domain/Entities/MovimientoAves.cs`

```csharp
public class MovimientoAves : AuditableEntity
{
    // Identificación
    public int Id { get; set; }
    public string NumeroMovimiento { get; set; } = string.Empty; // Ej: "MOV-20251015-000001"
    public DateTime FechaMovimiento { get; set; }
    public string TipoMovimiento { get; set; } = null!; // "Traslado", "Ajuste", "Liquidacion"
    
    // ORIGEN del movimiento
    public int? InventarioOrigenId { get; set; }
    public int? LoteOrigenId { get; set; }      // FK a lotes(lote_id)
    public int? GranjaOrigenId { get; set; }    // FK a farms(id)
    public string? NucleoOrigenId { get; set; }
    public string? GalponOrigenId { get; set; }
    
    // DESTINO del movimiento
    public int? InventarioDestinoId { get; set; }
    public int? LoteDestinoId { get; set; }      // FK a lotes(lote_id)
    public int? GranjaDestinoId { get; set; }    // FK a farms(id)
    public string? NucleoDestinoId { get; set; }
    public string? GalponDestinoId { get; set; }
    
    // Cantidades movidas
    public int CantidadHembras { get; set; }
    public int CantidadMachos { get; set; }
    public int CantidadMixtas { get; set; }
    
    // Información adicional
    public string? MotivoMovimiento { get; set; }
    public string? Observaciones { get; set; }
    public string Estado { get; set; } = "Pendiente"; // "Pendiente", "Completado", "Cancelado"
    
    // Usuario y fechas
    public int UsuarioMovimientoId { get; set; }
    public string? UsuarioNombre { get; set; }
    public DateTime? FechaProcesamiento { get; set; }
    public DateTime? FechaCancelacion { get; set; }
    
    // Propiedades calculadas
    public int TotalAves => CantidadHembras + CantidadMachos + CantidadMixtas;
    
    // Métodos de dominio
    public bool EsMovimientoValido() { ... }
    public void Procesar() { ... }
    public void Cancelar(string motivo) { ... }
}
```

**Relaciones**:
- `InventarioOrigen` → `InventarioAves` (1:N)
- `InventarioDestino` → `InventarioAves` (1:N)
- `LoteOrigen` → `Lote` (FK: `lote_origen_id` → `lotes.lote_id`)
- `LoteDestino` → `Lote` (FK: `lote_destino_id` → `lotes.lote_id`)
- `GranjaOrigen` → `Farm` (FK: `granja_origen_id` → `farms.id`)
- `GranjaDestino` → `Farm` (FK: `granja_destino_id` → `farms.id`)

#### `InventarioAves` (Entity)
**Ubicación**: `backend/src/ZooSanMarino.Domain/Entities/InventarioAves.cs`

```csharp
public class InventarioAves : AuditableEntity
{
    public int Id { get; set; }
    public int LoteId { get; set; }           // FK a lotes(lote_id)
    
    // Ubicación actual
    public int GranjaId { get; set; }           // FK a farms(id)
    public string? NucleoId { get; set; }
    public string? GalponId { get; set; }
    
    // Cantidades actuales
    public int CantidadHembras { get; set; }
    public int CantidadMachos { get; set; }
    public int CantidadMixtas { get; set; }
    
    // Estado y metadatos
    public DateTime FechaActualizacion { get; set; }
    public string? Observaciones { get; set; }
    public string Estado { get; set; } = "Activo"; // "Activo", "Trasladado", "Liquidado"
    
    // Propiedades calculadas
    public int TotalAves => CantidadHembras + CantidadMachos + CantidadMixtas;
    
    // Métodos de dominio
    public bool PuedeRealizarMovimiento(int hembras, int machos, int mixtas) { ... }
    public void AplicarMovimientoSalida(int hembras, int machos, int mixtas) { ... }
    public void AplicarMovimientoEntrada(int hembras, int machos, int mixtas) { ... }
    public void CambiarUbicacion(int granjaId, string? nucleoId, string? galponId) { ... }
}
```

---

### 2. CONFIGURACIONES EF CORE

#### `MovimientoAvesConfiguration`
**Ubicación**: `backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/MovimientoAvesConfiguration.cs`

**Tabla**: `movimiento_aves` (schema: `public`)

**Mapeo Principal**:
- `Id` → `id` (SERIAL PRIMARY KEY)
- `NumeroMovimiento` → `numero_movimiento` (VARCHAR(50), UNIQUE)
- `FechaMovimiento` → `fecha_movimiento` (TIMESTAMP WITH TIME ZONE)
- `TipoMovimiento` → `tipo_movimiento` (VARCHAR(50))
- `LoteOrigenId` → `lote_origen_id` (INTEGER, FK a `lotes.lote_id`)
- `LoteDestinoId` → `lote_destino_id` (INTEGER, FK a `lotes.lote_id`)
- `GranjaOrigenId` → `granja_origen_id` (INTEGER, FK a `farms.id`)
- `GranjaDestinoId` → `granja_destino_id` (INTEGER, FK a `farms.id`)
- `CantidadHembras` → `cantidad_hembras` (INTEGER, DEFAULT 0)
- `CantidadMachos` → `cantidad_machos` (INTEGER, DEFAULT 0)
- `CantidadMixtas` → `cantidad_mixtas` (INTEGER, DEFAULT 0)
- `Estado` → `estado` (VARCHAR(20), DEFAULT 'Pendiente')

**Índices**:
- `uq_movimiento_aves_numero_movimiento` (UNIQUE)
- `ix_movimiento_aves_fecha_movimiento`
- `ix_movimiento_aves_tipo_movimiento`
- `ix_movimiento_aves_estado`
- `ix_movimiento_aves_lote_origen_id`
- `ix_movimiento_aves_lote_destino_id`
- `ix_movimiento_aves_granjas` (composite: `granja_origen_id`, `granja_destino_id`)

---

### 3. SERVICIOS

#### `MovimientoAvesService`
**Ubicación**: `backend/src/ZooSanMarino.Infrastructure/Services/MovimientoAvesService.cs`
**Interface**: `IMovimientoAvesService`
**Dependencias**:
- `ZooSanMarinoContext` (DbContext)
- `ICurrentUser` (Usuario actual)
- `IInventarioAvesService` (Validación de disponibilidad)
- `IHistorialInventarioService` (Registro de historial)

**Métodos Principales**:

1. **`CreateAsync(CreateMovimientoAvesDto dto)`**
   - Crea un nuevo movimiento en estado "Pendiente"
   - Genera `NumeroMovimiento` automáticamente: `MOV-{yyyyMMdd}-{Id:D6}`
   - Valida que el movimiento sea válido antes de crearlo
   - Retorna `MovimientoAvesDto`

2. **`ProcesarMovimientoAsync(ProcesarMovimientoDto dto)`**
   - Cambia estado de "Pendiente" a "Completado"
   - Actualiza inventarios (resta del origen, suma al destino)
   - Si `AutoCrearInventarioDestino = true`, crea inventario en destino si no existe
   - Registra en historial

3. **`CancelarMovimientoAsync(CancelarMovimientoDto dto)`**
   - Cambia estado a "Cancelado"
   - Registra motivo de cancelación en observaciones

4. **`TrasladoRapidoAsync(TrasladoRapidoDto dto)`**
   - Crea y procesa un movimiento en una sola operación
   - Si `ProcesarInmediatamente = true`, procesa automáticamente

5. **`SearchAsync(MovimientoAvesSearchRequest request)`**
   - Búsqueda paginada con múltiples filtros
   - Filtros: número, tipo, estado, lotes, granjas, fechas, usuario
   - Ordenamiento configurable

6. **`SearchCompletoAsync(MovimientoAvesCompletoSearchRequest request)`**
   - Búsqueda con información completa de ubicaciones (nombres de granjas, lotes, etc.)
   - Retorna `MovimientoAvesCompletoDto` con datos enriquecidos

**Validaciones**:
- `ValidarMovimientoAsync()`: Verifica que las cantidades sean > 0, existe origen/destino, lotes diferentes
- `ValidarDisponibilidadAvesAsync()`: Verifica que haya suficientes aves en el origen
- `ValidarUbicacionDestinoAsync()`: Verifica que la granja destino exista

---

### 4. DTOs (Data Transfer Objects)

#### `MovimientoAvesDto` (Record)
**Ubicación**: `backend/src/ZooSanMarino.Application/DTOs/MovimientoAvesDto.cs`

```csharp
public record MovimientoAvesDto(
    int Id,
    string NumeroMovimiento,
    DateTime FechaMovimiento,
    string TipoMovimiento,
    UbicacionMovimientoDto? Origen,      // Lote, Granja, Núcleo, Galpón
    UbicacionMovimientoDto? Destino,
    int CantidadHembras,
    int CantidadMachos,
    int CantidadMixtas,
    int TotalAves,
    string Estado,
    string? MotivoMovimiento,
    string? Observaciones,
    int UsuarioMovimientoId,
    string? UsuarioNombre,
    DateTime? FechaProcesamiento,
    DateTime? FechaCancelacion,
    DateTime CreatedAt
);
```

#### `CreateMovimientoAvesDto` (Class)
```csharp
public sealed class CreateMovimientoAvesDto
{
    public DateTime FechaMovimiento { get; set; } = DateTime.UtcNow;
    public string TipoMovimiento { get; set; } = "Traslado";
    
    // Origen
    public int? InventarioOrigenId { get; set; }
    public int? LoteOrigenId { get; set; }
    public int? GranjaOrigenId { get; set; }
    public string? NucleoOrigenId { get; set; }
    public string? GalponOrigenId { get; set; }
    
    // Destino
    public int? InventarioDestinoId { get; set; }
    public int? LoteDestinoId { get; set; }
    public int? GranjaDestinoId { get; set; }
    public string? NucleoDestinoId { get; set; }
    public string? GalponDestinoId { get; set; }
    
    // Cantidades
    public int CantidadHembras { get; set; }
    public int CantidadMachos { get; set; }
    public int CantidadMixtas { get; set; }
    
    // Información adicional
    public string? MotivoMovimiento { get; set; }
    public string? Observaciones { get; set; }
    public int UsuarioMovimientoId { get; set; }
}
```

#### `MovimientoAvesSearchRequest` (Record)
```csharp
public sealed record MovimientoAvesSearchRequest(
    string? NumeroMovimiento = null,
    string? TipoMovimiento = null,
    string? Estado = null,
    int? LoteOrigenId = null,
    int? LoteDestinoId = null,
    int? GranjaOrigenId = null,
    int? GranjaDestinoId = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    int? UsuarioMovimientoId = null,
    string SortBy = "fecha_movimiento",
    bool SortDesc = true,
    int Page = 1,
    int PageSize = 20
);
```

#### `ResultadoMovimientoDto` (Record)
```csharp
public record ResultadoMovimientoDto(
    bool Success,
    string Message,
    int? MovimientoId,
    string? NumeroMovimiento,
    List<string> Errores,
    MovimientoAvesDto? Movimiento
);
```

---

### 5. CONTROLADOR API

#### `MovimientoAvesController`
**Ubicación**: `backend/src/ZooSanMarino.API/Controllers/MovimientoAvesController.cs`
**Ruta Base**: `/api/MovimientoAves`
**Autorización**: Requiere `[Authorize]`

**Endpoints**:

| Método | Ruta | Descripción |
|--------|------|-------------|
| `GET` | `/api/MovimientoAves` | Obtiene todos los movimientos |
| `POST` | `/api/MovimientoAves/search` | Búsqueda paginada con filtros |
| `GET` | `/api/MovimientoAves/{id}` | Obtiene un movimiento por ID |
| `GET` | `/api/MovimientoAves/numero/{numeroMovimiento}` | Obtiene por número de movimiento |
| `GET` | `/api/MovimientoAves/pendientes` | Obtiene movimientos pendientes |
| `GET` | `/api/MovimientoAves/lote/{loteId}` | Obtiene movimientos de un lote |
| `GET` | `/api/MovimientoAves/recientes` | Obtiene movimientos recientes (últimos N días) |
| `POST` | `/api/MovimientoAves` | Crea un nuevo movimiento |
| `POST` | `/api/MovimientoAves/{id}/procesar` | Procesa un movimiento pendiente |
| `POST` | `/api/MovimientoAves/{id}/cancelar` | Cancela un movimiento pendiente |
| `POST` | `/api/MovimientoAves/traslado-rapido` | Crea y procesa traslado en una operación |
| `POST` | `/api/MovimientoAves/validar` | Valida si un movimiento es posible |
| `GET` | `/api/MovimientoAves/estadisticas` | Obtiene estadísticas de movimientos |

---

## 🎨 FRONTEND - ANÁLISIS DETALLADO

### 1. ESTRUCTURA DE CARPETAS

```
frontend/src/app/features/traslados-aves/
├── components/
│   ├── traslado-navigation-card/
│   │   ├── traslado-navigation-card.component.ts
│   │   ├── traslado-navigation-card.component.html
│   │   └── traslado-navigation-card.component.scss
│   └── traslado-navigation-list/
│       ├── traslado-navigation-list.component.ts
│       ├── traslado-navigation-list.component.html
│       └── traslado-navigation-list.component.scss
├── pages/
│   ├── inventario-dashboard/
│   │   ├── inventario-dashboard.component.ts
│   │   ├── inventario-dashboard.component.html
│   │   └── inventario-dashboard.component.scss
│   ├── traslado-form/
│   │   ├── traslado-form.component.ts
│   │   ├── traslado-form.component.html
│   │   └── traslado-form.component.scss
│   ├── movimientos-list/
│   │   ├── movimientos-list.component.ts
│   │   ├── movimientos-list.component.html
│   │   └── movimientos-list.component.scss
│   ├── historial-trazabilidad/
│   │   ├── historial-trazabilidad.component.ts
│   │   ├── historial-trazabilidad.component.html
│   │   └── historial-trazabilidad.component.scss
│   └── traslado-navigation-demo/
│       ├── traslado-navigation-demo.component.ts
│       ├── traslado-navigation-demo.component.html
│       └── traslado-navigation-demo.component.scss
├── services/
│   └── traslados-aves.service.ts
├── traslados-aves-routing.module.ts
└── traslados-aves.module.ts
```

---

### 2. SERVICIO FRONTEND

#### `TrasladosAvesService`
**Ubicación**: `frontend/src/app/features/traslados-aves/services/traslados-aves.service.ts`
**Injectable**: `providedIn: 'root'`

**Interfaces TypeScript**:

```typescript
export interface MovimientoAvesDto {
  id: number;
  companyId: number;
  loteOrigenId: string;
  loteDestinoId: string;
  cantidadHembras: number;
  cantidadMachos: number;
  tipoMovimiento: string;
  observaciones?: string;
  fechaMovimiento: Date;
  createdAt: Date;
  updatedAt?: Date;
}

export interface CreateMovimientoAvesDto {
  loteOrigenId: string;
  loteDestinoId: string;
  cantidadHembras: number;
  cantidadMachos: number;
  tipoMovimiento: string;
  observaciones?: string;
  fechaMovimiento: Date;
}

export interface TrasladoRapidoRequest {
  loteOrigenId: string;
  loteDestinoId: string;
  cantidadHembras: number;
  cantidadMachos: number;
  observaciones?: string;
}

export interface TrasladoRapidoResponse {
  success: boolean;
  message: string;
  movimientoId?: number;
  inventarioOrigenActualizado?: { ... };
  inventarioDestinoActualizado?: { ... };
}
```

**Métodos Principales**:

```typescript
// MOVIMIENTOS
createMovimiento(dto: CreateMovimientoAvesDto): Observable<MovimientoAvesDto>
getMovimientoById(id: number): Observable<MovimientoAvesDto>
searchMovimientos(request: MovimientoAvesSearchRequest): Observable<PagedResult<MovimientoAvesDto>>
procesarMovimiento(id: number): Observable<MovimientoAvesDto>
cancelarMovimiento(id: number, motivo: string): Observable<MovimientoAvesDto>
trasladoRapido(request: TrasladoRapidoRequest): Observable<TrasladoRapidoResponse>

// INVENTARIO
getInventarioById(id: number): Observable<InventarioAvesDto>
getInventarioByLote(loteId: string): Observable<InventarioAvesDto>
searchInventarios(request: InventarioAvesSearchRequest): Observable<PagedResult<InventarioAvesDto>>
createInventario(dto: CreateInventarioAvesDto): Observable<InventarioAvesDto>
updateInventario(id: number, dto: UpdateInventarioAvesDto): Observable<InventarioAvesDto>
getResumenInventario(): Observable<ResumenInventarioDto>
```

---

### 3. COMPONENTES PRINCIPALES

#### `InventarioDashboardComponent`
**Ruta**: `/traslados-aves/dashboard`
**Funcionalidad**:
- Dashboard principal con resumen de inventario
- Lista de inventarios con filtros por granja, núcleo, galpón, lote
- Modal para crear traslados rápidos
- Visualización de cantidades actuales (hembras, machos, total)

**Características**:
- Filtros jerárquicos: Company → Farm → Núcleo → Galpón → Lote
- Búsqueda y ordenamiento
- Paginación
- Signals para estado reactivo (`signal`, `computed`)

#### `TrasladoFormComponent`
**Ruta**: `/traslados-aves/traslados`
**Funcionalidad**:
- Formulario para crear traslados entre lotes
- Validación de disponibilidad en tiempo real
- Carga automática de inventarios al seleccionar lotes
- Visualización de cantidades disponibles antes del traslado

**Validaciones**:
- Lotes diferentes
- Cantidades > 0
- Disponibilidad suficiente en origen

#### `MovimientosListComponent`
**Ruta**: `/traslados-aves/movimientos`
**Funcionalidad**:
- Lista de todos los movimientos con filtros avanzados
- Estados: Pendiente, Completado, Cancelado
- Acciones: Procesar, Cancelar
- Búsqueda por número, tipo, estado, fechas, lotes

#### `HistorialTrazabilidadComponent`
**Ruta**: `/traslados-aves/historial` o `/traslados-aves/historial/:loteId`
**Funcionalidad**:
- Trazabilidad completa de un lote
- Historial de todos los movimientos relacionados
- Visualización de eventos (entradas, salidas, ajustes)

---

### 4. RUTAS

**Configuración**: `frontend/src/app/app.config.ts`

```typescript
{
  path: 'traslados-aves',
  loadChildren: () => import('./features/traslados-aves/traslados-aves-routing.module').then(m => m.TrasladosAvesRoutingModule)
}
```

**Rutas del Módulo**: `frontend/src/app/features/traslados-aves/traslados-aves-routing.module.ts`

```typescript
const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: 'dashboard', loadComponent: () => import('./pages/inventario-dashboard/...') },
  { path: 'traslados', loadComponent: () => import('./pages/traslado-form/...') },
  { path: 'movimientos', loadComponent: () => import('./pages/movimientos-list/...') },
  { path: 'historial', loadComponent: () => import('./pages/historial-trazabilidad/...') },
  { path: 'historial/:loteId', loadComponent: () => import('./pages/historial-trazabilidad/...') }
];
```

---

## 🔗 INTEGRACIÓN CON MÓDULOS RELACIONADOS

### 1. SEGUIMIENTO DIARIO LEVANTE

**Módulo**: `SeguimientoLoteLevante`
**Entidad**: `SeguimientoLoteLevante`
**Campos relacionados**:
- `MortalidadHembras` (retiro por mortalidad)
- `MortalidadMachos` (retiro por mortalidad)
- `SelH` (selección de hembras - retiro)
- `SelM` (selección de machos - retiro)

**Integración Actual**:
- El seguimiento diario levante **registra mortalidades y selecciones** que son **retiros de aves**
- Estas operaciones deberían crear automáticamente movimientos en `MovimientoAves` con:
  - `TipoMovimiento = "Ajuste"` o `"Retiro"`
  - Restar del inventario del lote
  - Estado = "Completado" (se procesa automáticamente)

**Flujo Propuesto**:
```
SeguimientoLoteLevante.CreateAsync()
  ↓
  Si hay mortalidades/selecciones > 0:
    ↓
    Crear MovimientoAves:
      - TipoMovimiento: "Retiro"
      - CantidadHembras: MortalidadHembras + SelH
      - CantidadMachos: MortalidadMachos + SelM
      - Estado: "Completado"
      - ProcesarMovimientoAsync() → Resta del inventario
```

**Ubicación del Servicio**: `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoLoteLevanteService.cs`

---

### 2. SEGUIMIENTO DIARIO PRODUCCIÓN

**Módulo**: `SeguimientoProduccion` / `ProduccionDiaria`
**Entidad**: `SeguimientoProduccion` (tabla: `seguimiento_produccion`)
**Campos relacionados**:
- `MortalidadH` (mortalidad hembras)
- `MortalidadM` (mortalidad machos)
- `SelH` (selección hembras)

**Integración Actual**:
- Similar al seguimiento levante, registra retiros que deberían reflejarse en `MovimientoAves`

**Flujo Propuesto**:
```
ProduccionDiariaService.CreateAsync()
  ↓
  Si hay mortalidades/selecciones > 0:
    ↓
    Crear MovimientoAves:
      - TipoMovimiento: "Retiro"
      - CantidadHembras: MortalidadH + SelH
      - CantidadMachos: MortalidadM
      - Estado: "Completado"
      - ProcesarMovimientoAsync() → Resta del inventario
```

**Ubicación del Servicio**: `backend/src/ZooSanMarino.Infrastructure/Services/ProduccionDiariaService.cs`

---

### 3. SISTEMA DE LOTES

**Entidad**: `Lote`
**Relación**:
- `MovimientoAves.LoteOrigenId` → `Lote.LoteId`
- `MovimientoAves.LoteDestinoId` → `Lote.LoteId`
- `InventarioAves.LoteId` → `Lote.LoteId`

**Uso**:
- Los movimientos permiten trasladar aves entre lotes
- El inventario mantiene la cantidad actual por lote

---

### 4. SISTEMA DE GRANJAS

**Entidad**: `Farm`
**Relación**:
- `MovimientoAves.GranjaOrigenId` → `Farm.Id`
- `MovimientoAves.GranjaDestinoId` → `Farm.Id`
- `InventarioAves.GranjaId` → `Farm.Id`

**Uso**:
- Los movimientos pueden ser entre granjas o dentro de la misma granja
- El inventario incluye la ubicación (granja, núcleo, galpón)

---

## 🔄 FLUJOS DE DATOS Y OPERACIONES

### 1. FLUJO DE CREACIÓN DE TRASLADO

```
1. Usuario llena formulario (TrasladoFormComponent)
   ↓
2. Frontend valida datos
   ↓
3. POST /api/MovimientoAves
   Body: CreateMovimientoAvesDto
   ↓
4. MovimientoAvesService.CreateAsync()
   - Valida movimiento (ValidarMovimientoAsync)
   - Crea entidad MovimientoAves
   - Estado = "Pendiente"
   - Genera NumeroMovimiento: "MOV-{yyyyMMdd}-{Id:D6}"
   ↓
5. Guarda en BD (movimiento_aves)
   ↓
6. Retorna MovimientoAvesDto
   ↓
7. Frontend muestra éxito
```

---

### 2. FLUJO DE PROCESAMIENTO DE MOVIMIENTO

```
1. Usuario hace clic en "Procesar" (MovimientosListComponent)
   ↓
2. POST /api/MovimientoAves/{id}/procesar
   Body: { observaciones, autoCrearInventarioDestino }
   ↓
3. MovimientoAvesService.ProcesarMovimientoAsync()
   ↓
4. Valida que estado sea "Pendiente"
   ↓
5. Actualiza InventarioAves ORIGEN:
   - InventarioOrigen.AplicarMovimientoSalida(hembras, machos, mixtas)
   - Resta cantidades del inventario
   ↓
6. Actualiza/Crea InventarioAves DESTINO:
   - Si existe: InventarioDestino.AplicarMovimientoEntrada(hembras, machos, mixtas)
   - Si no existe y AutoCrearInventarioDestino = true:
     - Crea nuevo InventarioAves con cantidades
   ↓
7. Actualiza MovimientoAves:
   - Estado = "Completado"
   - FechaProcesamiento = DateTime.UtcNow
   ↓
8. Registra en HistorialInventario (opcional)
   ↓
9. Guarda cambios en BD
   ↓
10. Retorna ResultadoMovimientoDto
```

---

### 3. FLUJO DE REGISTRO DE RETIROS (Desde Seguimiento Diario)

**Integración Futura** (Pendiente de implementar):

```
1. Usuario registra seguimiento diario (levante o producción)
   ↓
2. SeguimientoLoteLevanteService.CreateAsync() o
   ProduccionDiariaService.CreateAsync()
   ↓
3. Si hay mortalidades/selecciones > 0:
   ↓
4. Crear MovimientoAves automáticamente:
   MovimientoAvesService.CreateAsync(new CreateMovimientoAvesDto {
     TipoMovimiento = "Retiro",
     LoteOrigenId = loteId,
     GranjaOrigenId = granjaId,
     CantidadHembras = mortalidadH + selH,
     CantidadMachos = mortalidadM + selM,
     Observaciones = "Retiro registrado desde seguimiento diario",
     Estado = "Pendiente"
   })
   ↓
5. Procesar automáticamente:
   MovimientoAvesService.ProcesarMovimientoAsync(new ProcesarMovimientoDto {
     MovimientoId = movimiento.Id,
     AutoCrearInventarioDestino = false
   })
   ↓
6. El inventario se actualiza automáticamente
```

---

### 4. FLUJO DE SUMA DE AVES (Entradas)

```
1. Usuario crea movimiento con TipoMovimiento = "Entrada" o "Ajuste"
   ↓
2. ProcesarMovimientoAsync():
   ↓
3. InventarioDestino.AplicarMovimientoEntrada(hembras, machos, mixtas)
   - Suma cantidades al inventario
   ↓
4. Actualiza MovimientoAves:
   - Estado = "Completado"
```

---

## 🗄️ BASE DE DATOS

### Tabla: `movimiento_aves`

```sql
CREATE TABLE movimiento_aves (
    id SERIAL PRIMARY KEY,
    numero_movimiento VARCHAR(50) NOT NULL UNIQUE,
    fecha_movimiento TIMESTAMP WITH TIME ZONE NOT NULL,
    tipo_movimiento VARCHAR(50) NOT NULL DEFAULT 'Traslado',
    
    -- Origen
    inventario_origen_id INTEGER,
    lote_origen_id INTEGER,              -- FK a lotes(lote_id)
    granja_origen_id INTEGER,            -- FK a farms(id)
    nucleo_origen_id VARCHAR(50),
    galpon_origen_id VARCHAR(50),
    
    -- Destino
    inventario_destino_id INTEGER,
    lote_destino_id INTEGER,              -- FK a lotes(lote_id)
    granja_destino_id INTEGER,            -- FK a farms(id)
    nucleo_destino_id VARCHAR(50),
    galpon_destino_id VARCHAR(50),
    
    -- Cantidades
    cantidad_hembras INTEGER NOT NULL DEFAULT 0,
    cantidad_machos INTEGER NOT NULL DEFAULT 0,
    cantidad_mixtas INTEGER NOT NULL DEFAULT 0,
    
    -- Información
    motivo_movimiento VARCHAR(500),
    observaciones VARCHAR(1000),
    estado VARCHAR(20) NOT NULL DEFAULT 'Pendiente', -- 'Pendiente', 'Completado', 'Cancelado'
    
    -- Usuario
    usuario_movimiento_id INTEGER NOT NULL,
    usuario_nombre VARCHAR(200),
    
    -- Fechas
    fecha_procesamiento TIMESTAMP WITH TIME ZONE,
    fecha_cancelacion TIMESTAMP WITH TIME ZONE,
    
    -- Auditoría
    company_id INTEGER NOT NULL,
    created_by_user_id INTEGER NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_by_user_id INTEGER,
    updated_at TIMESTAMP WITH TIME ZONE,
    deleted_at TIMESTAMP WITH TIME ZONE,
    
    -- Constraints
    CONSTRAINT ck_movimiento_aves_cantidades_positivas 
        CHECK (cantidad_hembras >= 0 AND cantidad_machos >= 0 AND cantidad_mixtas >= 0),
    CONSTRAINT ck_movimiento_aves_total_positivo 
        CHECK ((cantidad_hembras + cantidad_machos + cantidad_mixtas) > 0),
    CONSTRAINT ck_movimiento_aves_estado 
        CHECK (estado IN ('Pendiente', 'Completado', 'Cancelado')),
    
    -- Foreign Keys
    CONSTRAINT fk_movimiento_aves_inventario_origen_id 
        FOREIGN KEY (inventario_origen_id) 
        REFERENCES inventario_aves(id) ON DELETE RESTRICT,
    CONSTRAINT fk_movimiento_aves_inventario_destino_id 
        FOREIGN KEY (inventario_destino_id) 
        REFERENCES inventario_aves(id) ON DELETE RESTRICT,
    CONSTRAINT fk_movimiento_aves_lote_origen_id 
        FOREIGN KEY (lote_origen_id) 
        REFERENCES lotes(lote_id) ON DELETE RESTRICT,
    CONSTRAINT fk_movimiento_aves_lote_destino_id 
        FOREIGN KEY (lote_destino_id) 
        REFERENCES lotes(lote_id) ON DELETE RESTRICT,
    CONSTRAINT fk_movimiento_aves_granja_origen_id 
        FOREIGN KEY (granja_origen_id) 
        REFERENCES farms(id) ON DELETE RESTRICT,
    CONSTRAINT fk_movimiento_aves_granja_destino_id 
        FOREIGN KEY (granja_destino_id) 
        REFERENCES farms(id) ON DELETE RESTRICT
);

-- Índices
CREATE UNIQUE INDEX uq_movimiento_aves_numero_movimiento ON movimiento_aves(numero_movimiento);
CREATE INDEX ix_movimiento_aves_fecha_movimiento ON movimiento_aves(fecha_movimiento);
CREATE INDEX ix_movimiento_aves_tipo_movimiento ON movimiento_aves(tipo_movimiento);
CREATE INDEX ix_movimiento_aves_estado ON movimiento_aves(estado);
CREATE INDEX ix_movimiento_aves_lote_origen_id ON movimiento_aves(lote_origen_id);
CREATE INDEX ix_movimiento_aves_lote_destino_id ON movimiento_aves(lote_destino_id);
CREATE INDEX ix_movimiento_aves_granjas ON movimiento_aves(granja_origen_id, granja_destino_id);
```

### Tabla: `inventario_aves`

```sql
CREATE TABLE inventario_aves (
    id SERIAL PRIMARY KEY,
    lote_id INTEGER NOT NULL,             -- FK a lotes(lote_id)
    granja_id INTEGER NOT NULL,          -- FK a farms(id)
    nucleo_id VARCHAR(50),
    galpon_id VARCHAR(50),
    
    cantidad_hembras INTEGER NOT NULL DEFAULT 0,
    cantidad_machos INTEGER NOT NULL DEFAULT 0,
    cantidad_mixtas INTEGER NOT NULL DEFAULT 0,
    
    fecha_actualizacion TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    observaciones VARCHAR(1000),
    estado VARCHAR(20) NOT NULL DEFAULT 'Activo', -- 'Activo', 'Trasladado', 'Liquidado'
    
    -- Auditoría
    company_id INTEGER NOT NULL,
    created_by_user_id INTEGER NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_by_user_id INTEGER,
    updated_at TIMESTAMP WITH TIME ZONE,
    deleted_at TIMESTAMP WITH TIME ZONE,
    
    -- Foreign Keys
    CONSTRAINT fk_inventario_aves_lote_id 
        FOREIGN KEY (lote_id) 
        REFERENCES lotes(lote_id) ON DELETE RESTRICT,
    CONSTRAINT fk_inventario_aves_granja_id 
        FOREIGN KEY (granja_id) 
        REFERENCES farms(id) ON DELETE RESTRICT
);

-- Índices
CREATE INDEX ix_inventario_aves_lote_id ON inventario_aves(lote_id);
CREATE INDEX ix_inventario_aves_granja_id ON inventario_aves(granja_id);
CREATE INDEX ix_inventario_aves_estado ON inventario_aves(estado);
```

---

## 📡 API ENDPOINTS

### Base URL: `/api/MovimientoAves`

#### GET `/api/MovimientoAves`
Obtiene todos los movimientos.

**Respuesta**: `200 OK`
```json
[
  {
    "id": 1,
    "numeroMovimiento": "MOV-20251015-000001",
    "fechaMovimiento": "2025-10-15T10:30:00Z",
    "tipoMovimiento": "Traslado",
    "origen": { ... },
    "destino": { ... },
    "cantidadHembras": 100,
    "cantidadMachos": 50,
    "totalAves": 150,
    "estado": "Completado",
    ...
  }
]
```

#### POST `/api/MovimientoAves/search`
Búsqueda paginada con filtros.

**Request Body**:
```json
{
  "numeroMovimiento": "MOV-20251015",
  "tipoMovimiento": "Traslado",
  "estado": "Pendiente",
  "loteOrigenId": 123,
  "fechaDesde": "2025-10-01T00:00:00Z",
  "fechaHasta": "2025-10-31T23:59:59Z",
  "page": 1,
  "pageSize": 20,
  "sortBy": "fecha_movimiento",
  "sortDesc": true
}
```

**Respuesta**: `200 OK`
```json
{
  "items": [ ... ],
  "total": 150,
  "page": 1,
  "pageSize": 20
}
```

#### POST `/api/MovimientoAves`
Crea un nuevo movimiento.

**Request Body**:
```json
{
  "fechaMovimiento": "2025-10-15T10:00:00Z",
  "tipoMovimiento": "Traslado",
  "loteOrigenId": 123,
  "loteDestinoId": 456,
  "granjaOrigenId": 1,
  "granjaDestinoId": 2,
  "cantidadHembras": 100,
  "cantidadMachos": 50,
  "cantidadMixtas": 0,
  "motivoMovimiento": "Traslado entre granjas",
  "observaciones": "Traslado programado"
}
```

**Respuesta**: `201 Created`
```json
{
  "id": 1,
  "numeroMovimiento": "MOV-20251015-000001",
  "estado": "Pendiente",
  ...
}
```

#### POST `/api/MovimientoAves/{id}/procesar`
Procesa un movimiento pendiente.

**Request Body**:
```json
{
  "observaciones": "Procesado automáticamente",
  "autoCrearInventarioDestino": true
}
```

**Respuesta**: `200 OK`
```json
{
  "success": true,
  "message": "Movimiento procesado exitosamente",
  "movimientoId": 1,
  "numeroMovimiento": "MOV-20251015-000001",
  "errores": [],
  "movimiento": { ... }
}
```

---

## 📝 CASOS DE USO PRINCIPALES

### Caso 1: Traslado Entre Granjas

**Escenario**: Trasladar 100 hembras y 50 machos del Lote A (Granja 1) al Lote B (Granja 2).

**Pasos**:
1. Usuario selecciona `Lote A` como origen y `Lote B` como destino
2. Ingresa cantidades: 100 hembras, 50 machos
3. Sistema valida disponibilidad en `Lote A`
4. Sistema crea `MovimientoAves` (estado: "Pendiente")
5. Usuario procesa el movimiento
6. Sistema resta 100 hembras y 50 machos del inventario de `Lote A`
7. Sistema suma 100 hembras y 50 machos al inventario de `Lote B`
8. Sistema marca movimiento como "Completado"

---

### Caso 2: Registro de Retiros desde Seguimiento Diario

**Escenario**: En seguimiento diario levante se registran 5 hembras muertas y 2 machos muertos.

**Flujo Propuesto** (pendiente de implementar):
1. Usuario registra seguimiento diario con mortalidades
2. Sistema detecta mortalidades > 0
3. Sistema crea automáticamente `MovimientoAves`:
   - TipoMovimiento: "Retiro"
   - CantidadHembras: 5
   - CantidadMachos: 2
   - Estado: "Pendiente"
4. Sistema procesa automáticamente el movimiento
5. Sistema resta del inventario del lote
6. Sistema marca movimiento como "Completado"

---

### Caso 3: Ajuste de Inventario

**Escenario**: Corregir diferencias de inventario (merma, conteo físico).

**Pasos**:
1. Usuario crea movimiento con `TipoMovimiento = "Ajuste"`
2. Si es suma: `LoteOrigenId = null`, `LoteDestinoId = loteId`
3. Si es resta: `LoteOrigenId = loteId`, `LoteDestinoId = null` (o crear registro especial)
4. Procesar movimiento ajusta el inventario

---

### Caso 4: División de Lote

**Escenario**: Dividir un lote grande en dos lotes más pequeños.

**Pasos**:
1. Usuario crea nuevo lote (Lote B)
2. Usuario crea movimiento:
   - LoteOrigenId: Lote A
   - LoteDestinoId: Lote B
   - Cantidades a trasladar
3. Procesar movimiento actualiza ambos inventarios

---

## ✅ RESUMEN DE INTEGRACIONES PENDIENTES

### 1. Integración con Seguimiento Diario Levante
- **Estado**: ❌ Pendiente
- **Acción**: Modificar `SeguimientoLoteLevanteService.CreateAsync()` para crear `MovimientoAves` automáticamente cuando haya mortalidades/selecciones

### 2. Integración con Seguimiento Diario Producción
- **Estado**: ❌ Pendiente
- **Acción**: Modificar `ProduccionDiariaService.CreateAsync()` para crear `MovimientoAves` automáticamente cuando haya mortalidades/selecciones

### 3. Procesamiento Automático de Retiros
- **Estado**: ❌ Pendiente
- **Acción**: Al crear movimiento desde seguimiento diario, procesarlo automáticamente (estado: "Completado")

### 4. Sincronización de Inventarios
- **Estado**: ⚠️ Parcial
- **Acción**: Asegurar que el inventario siempre esté sincronizado con los movimientos procesados

---

## 📚 ARCHIVOS IMPORTANTES

### Backend
- `MovimientoAves.cs` (Entidad)
- `InventarioAves.cs` (Entidad)
- `MovimientoAvesConfiguration.cs` (EF Core Config)
- `MovimientoAvesService.cs` (Servicio)
- `IMovimientoAvesService.cs` (Interface)
- `MovimientoAvesController.cs` (API Controller)
- `MovimientoAvesDto.cs` (DTOs)

### Frontend
- `traslados-aves.service.ts` (Servicio Angular)
- `inventario-dashboard.component.ts` (Dashboard)
- `traslado-form.component.ts` (Formulario)
- `movimientos-list.component.ts` (Lista)
- `historial-trazabilidad.component.ts` (Trazabilidad)

### Base de Datos
- Tabla: `movimiento_aves`
- Tabla: `inventario_aves`
- Tabla: `historial_inventario` (opcional)

---

## 🔄 PRÓXIMOS PASOS

1. ✅ Documentación completa (este documento)
2. ❌ Implementar integración con seguimiento diario levante
3. ❌ Implementar integración con seguimiento diario producción
4. ❌ Mejorar UX del módulo (como se solicitó)
5. ❌ Agregar validaciones adicionales
6. ❌ Optimizar consultas para grandes volúmenes
7. ❌ Agregar reportes y estadísticas avanzadas

---

**Última actualización**: 2025-10-15
**Versión**: 1.0.0





