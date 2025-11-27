# 📦 Análisis Completo del Módulo de Inventario de Productos

## 📋 Índice
1. [Resumen Ejecutivo](#resumen-ejecutivo)
2. [Arquitectura General](#arquitectura-general)
3. [Backend - Entidades y Modelos](#backend---entidades-y-modelos)
4. [Backend - Servicios](#backend---servicios)
5. [Backend - Controladores y API](#backend---controladores-y-api)
6. [Frontend - Estructura](#frontend---estructura)
7. [Frontend - Componentes](#frontend---componentes)
8. [Flujos de Funcionalidad](#flujos-de-funcionalidad)
9. [Configuraciones y Mapeos](#configuraciones-y-mapeos)
10. [Endpoints API Completos](#endpoints-api-completos)

---

## 📊 Resumen Ejecutivo

El módulo de **Inventario de Productos** gestiona el stock de productos (alimentos/insumos) por granja, permitiendo:

- ✅ **Gestión de Stock**: Inventario actual por granja y producto
- ✅ **Movimientos**: Entradas y salidas de productos
- ✅ **Traslados**: Transferencias entre granjas con trazabilidad
- ✅ **Ajustes**: Corrección de diferencias (mermas, daños)
- ✅ **Kardex**: Historial de movimientos por producto
- ✅ **Conteo Físico**: Conciliación de inventario físico vs sistema
- ✅ **Catálogo**: Administración de ítems (alimentos/insumos)

**Rutas principales:**
- Frontend: `/inventario-management` → Componente de pestañas
- Frontend: `/inventario` → Módulo lazy-loaded
- Backend: `/api/farms/{farmId}/inventory/*`

---

## 🏗️ Arquitectura General

### Estructura de Capas (Backend)
```
ZooSanMarino.Domain/
  ├── Entities/
  │   ├── FarmProductInventory.cs      # Stock actual por granja
  │   ├── FarmInventoryMovement.cs     # Historial de movimientos
  │   └── CatalogItem.cs               # Catálogo de productos
  └── Enums/
      └── InventoryMovementType.cs      # Tipos de movimiento

ZooSanMarino.Application/
  ├── DTOs/
  │   ├── FarmInventoryDtos.cs         # DTOs de inventario
  │   └── FarmInventoryMovementDtos.cs # DTOs de movimientos
  └── Interfaces/
      ├── IFarmInventoryService.cs
      ├── IFarmInventoryMovementService.cs
      └── IFarmInventoryReportService.cs

ZooSanMarino.Infrastructure/
  ├── Services/
  │   ├── FarmInventoryService.cs      # CRUD de inventario
  │   ├── FarmInventoryMovementService.cs # Movimientos
  │   └── FarmInventoryReportService.cs   # Reportes/Kardex
  ├── Persistence/
  │   ├── Configurations/
  │   │   └── FarmInventoryMovementConfiguration.cs
  │   └── ZooSanMarinoContext.cs
  └── Migrations/
      └── ... (tablas creadas)

ZooSanMarino.API/
  └── Controllers/
      ├── FarmInventoryController.cs       # CRUD inventario + Kardex
      └── FarmInventoryMovementsController.cs # Movimientos
```

### Estructura de Capas (Frontend)
```
frontend/src/app/features/inventario/
  ├── components/
  │   ├── inventario-tabs/           # Pestañas principales
  │   ├── inventario-list/            # Lista de stock (pestaña Stock)
  │   ├── movimientos-form/           # Entrada/Salida
  │   ├── traslado-form/             # Traslado entre granjas
  │   ├── ajuste-form/               # Ajustes de inventario
  │   ├── kardex-list/               # Historial Kardex
  │   ├── conteo-fisico/             # Conteo físico
  │   └── catalogo-alimentos-tab/    # Catálogo (embebido)
  ├── services/
  │   └── inventario.service.ts      # Servicio Angular
  ├── inventario.module.ts
  └── inventario-routing.module.ts
```

---

## 🗄️ Backend - Entidades y Modelos

### 1. **FarmProductInventory** (Stock Actual)

**Archivo:** `backend/src/ZooSanMarino.Domain/Entities/FarmProductInventory.cs`

**Propósito:** Representa el stock actual de un producto en una granja específica.

```csharp
public class FarmProductInventory
{
    public int Id { get; set; }
    
    // Claves foráneas
    public int FarmId { get; set; }              // Granja
    public int CatalogItemId { get; set; }        // Producto del catálogo
    
    // Datos de inventario
    public decimal Quantity { get; set; }          // Cantidad actual (numeric(18,3))
    public string Unit { get; set; } = "kg";      // Unidad (kg, und, l, etc.)
    public string? Location { get; set; }         // Ubicación (bodega/galpón/estante)
    public string? LotNumber { get; set; }         // Número de lote
    public DateTime? ExpirationDate { get; set; }  // Fecha de vencimiento
    public decimal? UnitCost { get; set; }        // Costo unitario (numeric(18,2))
    
    // Metadata y estado
    public JsonDocument Metadata { get; set; } = JsonDocument.Parse("{}");
    public bool Active { get; set; } = true;
    public string? ResponsibleUserId { get; set; } // Usuario responsable
    
    // Timestamps
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    
    // Navegación
    public Farm Farm { get; set; } = null!;
    public CatalogItem CatalogItem { get; set; } = null!;
}
```

**Tabla BD:** `farm_product_inventory`

**Características:**
- **Upsert lógico**: Se actualiza si existe (FarmId + CatalogItemId), sino se crea
- **Relaciones**: FK a `farms` y `catalogo_items` (CASCADE DELETE)
- **Índices**: En (FarmId, CatalogItemId) para búsquedas eficientes

---

### 2. **FarmInventoryMovement** (Historial de Movimientos)

**Archivo:** `backend/src/ZooSanMarino.Domain/Entities/FarmInventoryMovement.cs`

**Propósito:** Registra todos los movimientos que afectan el inventario.

```csharp
public class FarmInventoryMovement
{
    public int Id { get; set; }
    public int FarmId { get; set; }
    public int CatalogItemId { get; set; }
    public decimal Quantity { get; set; }        // Cantidad (siempre positiva)
    public InventoryMovementType MovementType { get; set; } // Tipo de movimiento
    public string Unit { get; set; } = "kg";
    public string? Reference { get; set; }       // Referencia externa
    public string? Reason { get; set; }           // Motivo
    public Guid? TransferGroupId { get; set; }    // Para vincular traslados
    public JsonDocument Metadata { get; set; } = JsonDocument.Parse("{}");
    public string? ResponsibleUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    
    // Navegación
    public Farm Farm { get; set; } = null!;
    public CatalogItem CatalogItem { get; set; } = null!;
}
```

**Tabla BD:** `farm_inventory_movements`

**Tipos de Movimiento (Enum):**
```csharp
public enum InventoryMovementType
{
    Entry,        // Entrada (+)
    Exit,         // Salida (-)
    TransferOut,  // Salida por traslado (-)
    TransferIn,   // Entrada por traslado (+)
    Adjust        // Ajuste (+/- según signo)
}
```

**Características:**
- **Solo lectura**: Los movimientos NO se modifican, solo se crean
- **Trazabilidad**: Cada movimiento afecta el stock en `FarmProductInventory`
- **Grupos de traslado**: `TransferGroupId` vincula movimientos de salida/entrada en traslados

---

### 3. **CatalogItem** (Catálogo de Productos)

**Archivo:** `backend/src/ZooSanMarino.Domain/Entities/CatalogItem.cs`

**Propósito:** Define los productos disponibles (alimentos/insumos).

```csharp
public class CatalogItem
{
    public int Id { get; set; }
    public string Codigo { get; set; } = null!;      // Código único
    public string Nombre { get; set; } = null!;      // Nombre del producto
    public JsonDocument Metadata { get; set; } = JsonDocument.Parse("{}");
    public bool Activo { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

**Tabla BD:** `catalogo_items`

---

## 🔧 Backend - Servicios

### 1. **FarmInventoryService** (CRUD de Inventario)

**Archivo:** `backend/src/ZooSanMarino.Infrastructure/Services/FarmInventoryService.cs`

**Interfaz:** `IFarmInventoryService`

**Métodos principales:**

```csharp
// Consultas
Task<List<FarmInventoryDto>> GetByFarmAsync(int farmId, string? q, CancellationToken ct);
Task<FarmInventoryDto?> GetByIdAsync(int farmId, int id, CancellationToken ct);

// Escritura
Task<FarmInventoryDto> CreateOrReplaceAsync(int farmId, FarmInventoryCreateRequest req, CancellationToken ct);
Task<FarmInventoryDto?> UpdateAsync(int farmId, int id, FarmInventoryUpdateRequest req, CancellationToken ct);
Task<bool> DeleteAsync(int farmId, int id, bool hard = false, CancellationToken ct);
```

**Lógica destacada:**
- **CreateOrReplaceAsync**: Upsert por (FarmId, CatalogItemId)
- **Búsqueda**: Soporta filtro por texto (nombre/código del producto)
- **Validaciones**: Verifica existencia de granja y producto, cantidad no negativa

---

### 2. **FarmInventoryMovementService** (Movimientos)

**Archivo:** `backend/src/ZooSanMarino.Infrastructure/Services/FarmInventoryMovementService.cs`

**Interfaz:** `IFarmInventoryMovementService`

**Métodos principales:**

```csharp
// Movimientos básicos
Task<InventoryMovementDto> PostEntryAsync(int farmId, InventoryEntryRequest req, CancellationToken ct);
Task<InventoryMovementDto> PostExitAsync(int farmId, InventoryExitRequest req, CancellationToken ct);

// Traslados
Task<(InventoryMovementDto Out, InventoryMovementDto In)> PostTransferAsync(
    int fromFarmId, InventoryTransferRequest req, CancellationToken ct);

// Ajustes
Task<InventoryMovementDto> PostAdjustAsync(int farmId, InventoryAdjustRequest req, CancellationToken ct);

// Consultas
Task<PagedResult<InventoryMovementDto>> GetPagedAsync(int farmId, MovementQuery q, CancellationToken ct);
Task<InventoryMovementDto?> GetByIdAsync(int farmId, int movementId, CancellationToken ct);
```

**Lógica destacada:**

1. **PostEntryAsync**:
   - Incrementa `Quantity` en `FarmProductInventory`
   - Crea movimiento tipo `Entry`
   - Validación: cantidad positiva

2. **PostExitAsync**:
   - Decrementa `Quantity` en `FarmProductInventory`
   - Validación: stock suficiente, cantidad positiva
   - Crea movimiento tipo `Exit`

3. **PostTransferAsync**:
   - Crea 2 movimientos: `TransferOut` (origen) y `TransferIn` (destino)
   - Vinculados por `TransferGroupId` (Guid)
   - Validación: granjas diferentes, stock suficiente en origen
   - Transacción atómica (ambos o ninguno)

4. **PostAdjustAsync**:
   - Permite `Quantity` positivo (suma) o negativo (resta)
   - Valida que el saldo final no sea negativo
   - Crea movimiento tipo `Adjust`

**Patrón de diseño:**
- **GetOrCreateInventoryAsync**: Si no existe inventario, lo crea con Quantity=0
- **Transacciones**: Todos los métodos usan transacciones DB para garantizar consistencia
- **Resolución de ítem**: Puede recibir `CatalogItemId` o `Codigo` (busca en catálogo)

---

### 3. **FarmInventoryReportService** (Reportes)

**Archivo:** `backend/src/ZooSanMarino.Infrastructure/Services/FarmInventoryReportService.cs`

**Interfaz:** `IFarmInventoryReportService`

**Métodos:**

```csharp
Task<IEnumerable<KardexItemDto>> GetKardexAsync(
    int farmId, int catalogItemId, DateTime? from, DateTime? to, CancellationToken ct);

Task ApplyStockCountAsync(int farmId, StockCountRequest req, CancellationToken ct);
```

**Lógica destacada:**

1. **GetKardexAsync**:
   - Filtra movimientos por granja, producto y rango de fechas
   - Calcula saldo acumulado iterativamente
   - Retorna lista con: Fecha, Tipo, Referencia, Cantidad (con signo), Saldo, Motivo

2. **ApplyStockCountAsync**:
   - Recibe conteos físicos y compara con stock del sistema
   - Genera diferencias como ajustes automáticos
   - Crea movimientos de tipo `Adjust` con motivo "Conteo físico"

---

## 🌐 Backend - Controladores y API

### 1. **FarmInventoryController**

**Archivo:** `backend/src/ZooSanMarino.API/Controllers/FarmInventoryController.cs`

**Ruta base:** `api/farms/{farmId}/inventory`

**Endpoints:**

| Método | Ruta | Descripción | Request/Response |
|--------|------|-------------|------------------|
| GET | `/` | Lista inventario de la granja | Query: `?q=` (búsqueda opcional)<br>Response: `FarmInventoryDto[]` |
| GET | `/{id}` | Obtiene un ítem de inventario | Response: `FarmInventoryDto` o 404 |
| POST | `/` | Crea o reemplaza inventario | Body: `FarmInventoryCreateRequest`<br>Response: `FarmInventoryDto` (201) |
| PUT | `/{id}` | Actualiza inventario | Body: `FarmInventoryUpdateRequest`<br>Response: `FarmInventoryDto` o 404 |
| DELETE | `/{id}` | Elimina (soft/hard) | Query: `?hard=false`<br>Response: 204 o 404 |
| GET | `/kardex` | Obtiene Kardex de un producto | Query: `?catalogItemId=&from=&to=`<br>Response: `KardexItemDto[]` |
| POST | `/stock-count` | Aplica conteo físico | Body: `StockCountRequest`<br>Response: 204 |

---

### 2. **FarmInventoryMovementsController**

**Archivo:** `backend/src/ZooSanMarino.API/Controllers/FarmInventoryMovementsController.cs`

**Ruta base:** `api/farms/{farmId}/inventory/movements`

**Endpoints:**

| Método | Ruta | Descripción | Request/Response |
|--------|------|-------------|------------------|
| POST | `/in` | Registra entrada | Body: `InventoryEntryRequest`<br>Response: `InventoryMovementDto` (201) |
| POST | `/out` | Registra salida | Body: `InventoryExitRequest`<br>Response: `InventoryMovementDto` (201) |
| POST | `/transfer` | Traslado entre granjas | Body: `InventoryTransferRequest` (incluye `toFarmId`)<br>Response: `{out: ..., In: ...}` (201) |
| POST | `/adjust` | Ajuste de inventario | Body: `InventoryAdjustRequest`<br>Response: `InventoryMovementDto` (201) |
| GET | `/` | Lista movimientos (paginado) | Query: `?from=&to=&catalogItemId=&codigo=&type=&page=&pageSize=`<br>Response: `PagedResult<InventoryMovementDto>` |
| GET | `/{movementId}` | Obtiene un movimiento | Response: `InventoryMovementDto` o 404 |

---

## 🎨 Frontend - Estructura

### Rutas

- `/inventario-management` → `InventarioTabsComponent` (componente standalone)
- `/inventario` → Módulo lazy-loaded (mismo componente)

### Módulo

**Archivo:** `frontend/src/app/features/inventario/inventario.module.ts`

- Módulo Angular con routing
- Componentes standalone (no requiere imports)

---

## 🧩 Frontend - Componentes

### 1. **InventarioTabsComponent** (Contenedor Principal)

**Archivo:** `frontend/src/app/features/inventario/components/inventario-tabs/inventario-tabs.component.ts`

**Funcionalidad:** Pestañas que organizan todas las funcionalidades del inventario.

**Pestañas:**
- `mov` - Entrada/Salida (MovimientosFormComponent)
- `tras` - Traslado (TrasladoFormComponent)
- `ajuste` - Ajuste (AjusteFormComponent)
- `kardex` - Kardex (KardexListComponent)
- `conteo` - Conteo físico (ConteoFisicoComponent)
- `stock` - Stock actual (InventarioListComponent)
- `catalogo` - Catálogo (CatalogoAlimentosTabComponent)

---

### 2. **InventarioListComponent** (Stock Actual)

**Archivo:** `frontend/src/app/features/inventario/components/inventario-list/inventario-list.component.ts`

**Funcionalidad:**
- Muestra el inventario actual de una granja seleccionada
- Filtro por texto (código/nombre/ubicación/lote)
- Selección de granja en dropdown
- Recarga automática al cambiar granja

**Métodos principales:**
- `load()`: Carga inventario de la granja seleccionada
- `getFarmName(id)`: Helper para mostrar nombre de granja

---

### 3. **MovimientosFormComponent** (Entrada/Salida)

**Archivo:** `frontend/src/app/features/inventario/components/movimientos-form/movimientos-form.component.ts`

**Funcionalidad:**
- Formulario para registrar entradas o salidas
- Toggle entre tipo `in` (entrada) y `out` (salida)
- Selección de granja y producto
- Campos: cantidad, unidad, referencia, motivo

**Validaciones:**
- Granja requerida
- Producto requerido
- Cantidad > 0

---

### 4. **TrasladoFormComponent** (Traslado entre Granjas)

**Archivo:** `frontend/src/app/features/inventario/components/traslado-form/traslado-form.component.ts`

**Funcionalidad:**
- Formulario para trasladar productos entre granjas
- Selección de granja origen y destino
- Validación: granjas diferentes
- Campos similares a movimientos

---

### 5. **AjusteFormComponent** (Ajustes)

**Archivo:** `frontend/src/app/features/inventario/components/ajuste-form/ajuste-form.component.ts`

**Funcionalidad:**
- Formulario para ajustar inventario
- Selector de signo: `+1` (sumar) o `-1` (restar)
- Permite corregir diferencias

---

### 6. **KardexListComponent** (Historial Kardex)

**Archivo:** `frontend/src/app/features/inventario/components/kardex-list/kardex-list.component.ts`

**Funcionalidad:**
- Consulta el historial de movimientos (Kardex) por producto
- Filtros: granja, producto, rango de fechas
- Muestra: Fecha, Tipo, Referencia, Cantidad (coloreado por signo), Saldo, Motivo

---

### 7. **ConteoFisicoComponent** (Conteo Físico)

**Archivo:** `frontend/src/app/features/inventario/components/conteo-fisico/conteo-fisico.component.ts`

**Funcionalidad:**
- Carga el stock actual de una granja
- Permite ingresar conteo físico para cada producto
- Calcula diferencias automáticamente
- Envía ajustes al backend al guardar

**Interfaz:**
```typescript
interface ConteoRow {
  catalogItemId: number;
  codigo: string;
  nombre: string;
  unit: string;
  sistema: number;      // Stock del sistema
  conteo: number | null; // Conteo físico (editable)
}
```

---

## 🔄 Flujos de Funcionalidad

### 1. Flujo de Entrada de Producto

```
Usuario → MovimientosFormComponent
  ↓ (Selecciona: Granja, Producto, Cantidad, etc.)
  ↓ POST /api/farms/{farmId}/inventory/movements/in
  ↓ FarmInventoryMovementService.PostEntryAsync()
  ↓ [TRANSACCIÓN]
    1. Obtiene/Crea FarmProductInventory
    2. Incrementa Quantity += cantidad
    3. Crea FarmInventoryMovement (Entry)
  ↓ [COMMIT]
  ↓ Response: InventoryMovementDto
  ↓ Frontend: Muestra confirmación
```

### 2. Flujo de Traslado entre Granjas

```
Usuario → TrasladoFormComponent
  ↓ (Selecciona: Granja Origen, Granja Destino, Producto, Cantidad)
  ↓ POST /api/farms/{fromFarmId}/inventory/movements/transfer
  ↓ FarmInventoryMovementService.PostTransferAsync()
  ↓ [TRANSACCIÓN]
    1. Valida stock suficiente en origen
    2. Decrementa stock en origen (TransferOut)
    3. Crea FarmInventoryMovement (TransferOut) con TransferGroupId
    4. Incrementa stock en destino (TransferIn)
    5. Crea FarmInventoryMovement (TransferIn) con mismo TransferGroupId
  ↓ [COMMIT]
  ↓ Response: {out: ..., In: ...}
  ↓ Frontend: Muestra confirmación
```

### 3. Flujo de Kardex (Consulta Historial)

```
Usuario → KardexListComponent
  ↓ (Selecciona: Granja, Producto, Rango de fechas)
  ↓ GET /api/farms/{farmId}/inventory/kardex?catalogItemId=&from=&to=
  ↓ FarmInventoryReportService.GetKardexAsync()
  ↓
    1. Consulta FarmInventoryMovements filtrados
    2. Ordena por fecha ascendente
    3. Calcula saldo acumulado iterativamente
    4. Retorna KardexItemDto[]
  ↓
  ↓ Frontend: Muestra tabla con historial y saldos
```

### 4. Flujo de Conteo Físico

```
Usuario → ConteoFisicoComponent
  ↓ (Selecciona granja → carga stock actual)
  ↓ Muestra tabla: Sistema | Conteo (editable)
  ↓ Usuario ingresa conteos
  ↓ Guardar → POST /api/farms/{farmId}/inventory/stock-count
  ↓ FarmInventoryReportService.ApplyStockCountAsync()
  ↓
    Para cada ítem:
    1. Calcula diferencia = conteo - sistema
    2. Si diferencia != 0:
       - Ajusta FarmProductInventory.Quantity
       - Crea FarmInventoryMovement (Adjust) con motivo "Conteo físico"
  ↓
  ↓ Frontend: Muestra confirmación
```

---

## ⚙️ Configuraciones y Mapeos

### 1. FarmInventoryMovementConfiguration

**Archivo:** `backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/FarmInventoryMovementConfiguration.cs`

**Configuración:**
- Tabla: `farm_inventory_movements`
- Conversión de enum: `InventoryMovementType` → string en BD
- Índices: (FarmId, CatalogItemId), MovementType, TransferGroupId
- FK: Farm, CatalogItem (Restrict)

### 2. CatalogItemConfiguration

**Archivo:** `backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/CatalogItemConfiguration.cs`

**Configuración:**
- Tabla: `catalogo_items`
- Índice único en `Codigo`
- Índices: Activo, Nombre

### 3. FarmProductInventory (Mapeo implícito)

- Tabla: `farm_product_inventory`
- Mapeo automático por convenciones de EF Core
- FK: Farm, CatalogItem (Cascade Delete)

### 4. Registro de Servicios (Program.cs)

```csharp
builder.Services.AddScoped<IFarmInventoryService, FarmInventoryService>();
builder.Services.AddScoped<IFarmInventoryMovementService, FarmInventoryMovementService>();
builder.Services.AddScoped<IFarmInventoryReportService, FarmInventoryReportService>();
```

---

## 📡 Endpoints API Completos

### Base URL: `/api/farms/{farmId}/inventory`

#### Inventario (Stock)
```
GET    /api/farms/{farmId}/inventory              # Lista inventario
GET    /farms/{farmId}/inventory                   # Alias (sin /api)
GET    /api/farms/{farmId}/inventory/{id}          # Obtiene por ID
POST   /api/farms/{farmId}/inventory               # Crea/Reemplaza
PUT    /api/farms/{farmId}/inventory/{id}          # Actualiza
DELETE /api/farms/{farmId}/inventory/{id}?hard=    # Elimina (soft/hard)
```

#### Movimientos
```
POST   /api/farms/{farmId}/inventory/movements/in           # Entrada
POST   /api/farms/{farmId}/inventory/movements/out          # Salida
POST   /api/farms/{farmId}/inventory/movements/transfer      # Traslado
POST   /api/farms/{farmId}/inventory/movements/adjust        # Ajuste
GET    /api/farms/{farmId}/inventory/movements              # Lista (paginado)
GET    /api/farms/{farmId}/inventory/movements/{movementId} # Por ID
```

#### Reportes
```
GET    /api/farms/{farmId}/inventory/kardex?catalogItemId=&from=&to=  # Kardex
POST   /api/farms/{farmId}/inventory/stock-count                     # Conteo físico
```

### Todos los endpoints también tienen alias sin `/api`:
- `/farms/{farmId}/inventory/*`

---

## 🔐 Seguridad y Validaciones

### Validaciones Backend:
- ✅ Existencia de granja y producto antes de operaciones
- ✅ Stock suficiente para salidas y traslados
- ✅ Cantidades positivas en entradas/salidas
- ✅ Granjas diferentes en traslados
- ✅ Saldo no negativo después de ajustes

### Transacciones:
- ✅ Todos los movimientos usan transacciones DB
- ✅ Rollback automático en caso de error

### Auditoría:
- ✅ `ResponsibleUserId`: Capturado del JWT (ICurrentUser) o enviado explícitamente
- ✅ Timestamps: `CreatedAt`, `UpdatedAt` automáticos

---

## 📝 Notas Técnicas

1. **Upsert de Inventario**: La operación `CreateOrReplaceAsync` busca por (FarmId, CatalogItemId) y actualiza si existe, crea si no.

2. **Resolución de Producto**: Los requests pueden enviar `CatalogItemId` o `Codigo`; el servicio resuelve al ID correspondiente.

3. **Unidad por defecto**: Si no se especifica, se usa `"kg"` como unidad.

4. **Metadata JSONB**: Ambas entidades soportan metadata JSONB para extensibilidad.

5. **Soft Delete**: Por defecto, `DeleteAsync` marca `Active = false`; con `hard=true` elimina físicamente.

6. **Grupos de Traslado**: Los traslados usan `TransferGroupId` (Guid) para vincular el movimiento de salida y entrada en una misma operación.

---

## 🚀 Mejoras Futuras Sugeridas

1. **Notificaciones de stock bajo**: Alertas cuando el inventario está por debajo de un umbral
2. **Múltiples ubicaciones**: Soporte para múltiples ubicaciones por producto en la misma granja
3. **Cálculo de costos**: FIFO/LIFO para cálculo de costos
4. **Exportación**: PDF/Excel de Kardex y reportes de inventario
5. **Historial de cambios**: Auditoría de cambios en `FarmProductInventory`
6. **Validaciones de lote**: Alertas de vencimiento próximo

---

**Última actualización:** 2025-01-XX  
**Versión del documento:** 1.0






