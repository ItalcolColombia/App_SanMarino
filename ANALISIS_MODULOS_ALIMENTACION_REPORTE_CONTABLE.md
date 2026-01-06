# 📊 ANÁLISIS COMPLETO: MÓDULOS QUE ALIMENTAN EL REPORTE CONTABLE

## 🎯 OBJETIVO

Analizar todos los módulos que proporcionan datos al reporte contable semanal, incluyendo:
- APIs disponibles
- Parámetros de entrada
- Campos de respuesta
- Estructura de datos
- Cómo se relacionan con el reporte contable

---

## 1. 📦 MÓDULO: GESTIÓN DE INVENTARIO (Inventory Management)

### 📍 Controlador: `FarmInventoryController` y `FarmInventoryMovementsController`

**Ruta Base:** `/api/farms/{farmId}/inventory`

### 🔌 APIs Disponibles

#### 1.1. **Inventario (Stock Actual)**

| Método | Endpoint | Descripción | Parámetros | Response |
|--------|----------|-------------|------------|----------|
| `GET` | `/api/farms/{farmId}/inventory` | Lista inventario de la granja | `farmId` (int), `q` (string?, opcional - búsqueda) | `FarmInventoryDto[]` |
| `GET` | `/api/farms/{farmId}/inventory/{id}` | Obtiene un ítem de inventario | `farmId` (int), `id` (int) | `FarmInventoryDto` o 404 |
| `POST` | `/api/farms/{farmId}/inventory` | Crea o reemplaza inventario | `farmId` (int), Body: `FarmInventoryCreateRequest` | `FarmInventoryDto` (201) |
| `PUT` | `/api/farms/{farmId}/inventory/{id}` | Actualiza inventario | `farmId` (int), `id` (int), Body: `FarmInventoryUpdateRequest` | `FarmInventoryDto` o 404 |
| `DELETE` | `/api/farms/{farmId}/inventory/{id}` | Elimina inventario | `farmId` (int), `id` (int), `hard` (bool?, query) | 204 o 404 |
| `GET` | `/api/farms/{farmId}/inventory/kardex` | Obtiene Kardex de un producto | `farmId` (int), `catalogItemId` (int, query), `from` (DateTime?, query), `to` (DateTime?, query) | `KardexItemDto[]` |
| `POST` | `/api/farms/{farmId}/inventory/stock-count` | Aplica conteo físico | `farmId` (int), Body: `StockCountRequest` | 204 |

#### 1.2. **Movimientos de Inventario**

| Método | Endpoint | Descripción | Parámetros | Response |
|--------|----------|-------------|------------|----------|
| `POST` | `/api/farms/{farmId}/inventory/movements/in` | Registra entrada de producto | `farmId` (int), Body: `InventoryEntryRequest` | `InventoryMovementDto` (201) |
| `POST` | `/api/farms/{farmId}/inventory/movements/out` | Registra salida de producto | `farmId` (int), Body: `InventoryExitRequest` | `InventoryMovementDto` (201) |
| `POST` | `/api/farms/{farmId}/inventory/movements/transfer` | Registra traslado entre granjas | `farmId` (int), Body: `InventoryTransferRequest` | `TransferResponse` (201) con `out` e `In` |
| `POST` | `/api/farms/{farmId}/inventory/movements/adjust` | Registra ajuste (+/-) | `farmId` (int), Body: `InventoryAdjustRequest` | `InventoryMovementDto` (201) |
| `GET` | `/api/farms/{farmId}/inventory/movements` | Lista movimientos (paginado) | `farmId` (int), `from` (DateTime?, query), `to` (DateTime?, query), `catalogItemId` (int?, query), `codigo` (string?, query), `type` (string?, query), `page` (int, query), `pageSize` (int, query) | `PagedResult<InventoryMovementDto>` |
| `GET` | `/api/farms/{farmId}/inventory/movements/{movementId}` | Obtiene movimiento por ID | `farmId` (int), `movementId` (int) | `InventoryMovementDto` o 404 |

### 📋 DTOs y Estructura de Datos

#### `FarmInventoryDto` (Stock Actual)
```csharp
public class FarmInventoryDto
{
    public int Id { get; set; }
    public int FarmId { get; set; }
    public int CatalogItemId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public decimal Quantity { get; set; }        // Cantidad actual
    public string Unit { get; set; } = "kg";     // Unidad (kg, und, l, bultos)
    public string? Location { get; set; }        // Ubicación (bodega/galpón/estante)
    public string? LotNumber { get; set; }       // Número de lote
    public DateTime? ExpirationDate { get; set; } // Fecha de vencimiento
    public decimal? UnitCost { get; set; }       // Costo unitario
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

#### `InventoryMovementDto` (Movimiento)
```csharp
public class InventoryMovementDto
{
    public int Id { get; set; }
    public int FarmId { get; set; }
    public int CatalogItemId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public decimal Quantity { get; set; }              // Cantidad
    public string MovementType { get; set; } = null!;  // Entry|Exit|TransferIn|TransferOut|Adjust
    public string Unit { get; set; } = "kg";          // Unidad
    public string? Reference { get; set; }             // Referencia
    public string? Reason { get; set; }               // Motivo
    public string? Origin { get; set; }                // Origen (para entradas)
    public string? Destination { get; set; }          // Destino (para salidas)
    public Guid? TransferGroupId { get; set; }        // ID de grupo de traslado
    public JsonDocument? Metadata { get; set; }        // Metadata adicional
    public string? ResponsibleUserId { get; set; }    // Usuario responsable
    public DateTimeOffset CreatedAt { get; set; }      // Fecha del movimiento
}
```

#### `InventoryEntryRequest` (Entrada)
```csharp
public class InventoryEntryRequest
{
    public int? CatalogItemId { get; set; }      // ID del producto
    public string? Codigo { get; set; }          // Código del producto (alternativa)
    public decimal Quantity { get; set; }        // Cantidad (positivo)
    public string? Unit { get; set; }           // Unidad (kg, bultos, etc.)
    public string? Reference { get; set; }       // Referencia
    public string? Reason { get; set; }         // Motivo
    public string? Origin { get; set; }          // Origen (ej: "Planta Sanmarino")
    public JsonDocument? Metadata { get; set; } // Metadata adicional
}
```

#### `InventoryTransferRequest` (Traslado)
```csharp
public class InventoryTransferRequest : InventoryEntryRequest
{
    public int ToFarmId { get; set; }           // Granja destino
}
```

#### `KardexItemDto` (Kardex)
```csharp
public class KardexItemDto
{
    public DateTime Fecha { get; set; }
    public string Tipo { get; set; } = null!;  // Entry|Exit|TransferIn|TransferOut|Adjust
    public string? Referencia { get; set; }
    public decimal Cantidad { get; set; }       // +entrada / -salida
    public string Unidad { get; set; } = "kg";
    public decimal Saldo { get; set; }          // Saldo acumulado
    public string? Motivo { get; set; }
}
```

### 🔍 Campos Relevantes para Reporte Contable

**Para BULTO (Bultos de Alimento):**
- `CatalogItemId`: ID del producto (alimento)
- `MovementType`: 
  - `"Entry"` → Entradas de bultos
  - `"TransferOut"` → Traslados de bultos (salidas)
  - `"TransferIn"` → Traslados de bultos (entradas)
- `Quantity`: Cantidad en bultos (si `Unit = "bultos"`) o en kg (necesita conversión)
- `Unit`: Unidad de medida (`"kg"`, `"bultos"`, `"und"`, etc.)
- `CreatedAt`: Fecha del movimiento
- `FarmId`: ID de la granja

**Consulta para obtener entradas de bultos:**
```csharp
GET /api/farms/{farmId}/inventory/movements?type=Entry&catalogItemId={alimentoId}&from={fechaInicio}&to={fechaFin}
```

**Consulta para obtener traslados de bultos:**
```csharp
GET /api/farms/{farmId}/inventory/movements?type=TransferOut&catalogItemId={alimentoId}&from={fechaInicio}&to={fechaFin}
```

---

## 2. 🐔 MÓDULO: TRASLADOS DE AVES (Bird Transfers)

### 📍 Controlador: `MovimientoAvesController`

**Ruta Base:** `/api/MovimientoAves`

### 🔌 APIs Disponibles

| Método | Endpoint | Descripción | Parámetros | Response |
|--------|----------|-------------|------------|----------|
| `GET` | `/api/MovimientoAves` | Obtiene todos los movimientos | Ninguno | `MovimientoAvesDto[]` |
| `POST` | `/api/MovimientoAves/search` | Búsqueda paginada con filtros | Body: `MovimientoAvesSearchRequest` | `PagedResult<MovimientoAvesDto>` |
| `GET` | `/api/MovimientoAves/{id}` | Obtiene movimiento por ID | `id` (int) | `MovimientoAvesDto` o 404 |
| `GET` | `/api/MovimientoAves/numero/{numeroMovimiento}` | Obtiene por número | `numeroMovimiento` (string) | `MovimientoAvesDto` o 404 |
| `POST` | `/api/MovimientoAves` | Crea nuevo movimiento | Body: `CreateMovimientoAvesDto` | `MovimientoAvesDto` (201) |
| `POST` | `/api/MovimientoAves/{id}/procesar` | Procesa movimiento pendiente | `id` (int), Body: `ProcesarMovimientoRequest` | `ResultadoMovimientoDto` |
| `POST` | `/api/MovimientoAves/{id}/cancelar` | Cancela movimiento | `id` (int), Body: `CancelarMovimientoRequest` | `ResultadoMovimientoDto` |
| `POST` | `/api/MovimientoAves/traslado-rapido` | Traslado rápido | Body: `TrasladoRapidoRequest` | `ResultadoMovimientoDto` (201) |
| `POST` | `/api/MovimientoAves/validar` | Valida movimiento | Body: `CreateMovimientoAvesDto` | `ValidacionMovimientoDto` |
| `GET` | `/api/MovimientoAves/pendientes` | Movimientos pendientes | Ninguno | `MovimientoAvesDto[]` |
| `GET` | `/api/MovimientoAves/lote/{loteId}` | Movimientos por lote | `loteId` (int) | `MovimientoAvesDto[]` |
| `GET` | `/api/MovimientoAves/usuario/{usuarioId}` | Movimientos por usuario | `usuarioId` (int) | `MovimientoAvesDto[]` |
| `GET` | `/api/MovimientoAves/recientes` | Movimientos recientes | `dias` (int, query, default: 7) | `MovimientoAvesDto[]` |
| `GET` | `/api/MovimientoAves/estadisticas` | Estadísticas | `fechaDesde` (DateTime?, query), `fechaHasta` (DateTime?, query) | `EstadisticasMovimientoDto` |

### 📋 DTOs y Estructura de Datos

#### `MovimientoAvesDto` (Movimiento de Aves)
```csharp
public record MovimientoAvesDto(
    int Id,
    string NumeroMovimiento,                    // Número único del movimiento
    DateTime FechaMovimiento,                   // Fecha del movimiento
    string TipoMovimiento,                      // "Traslado", "Venta", "Ajuste", "Liquidacion"
    
    // Origen
    UbicacionMovimientoDto? Origen,             // Ubicación origen
    
    // Destino
    UbicacionMovimientoDto? Destino,            // Ubicación destino
    
    // Cantidades
    int CantidadHembras,                        // Cantidad de hembras
    int CantidadMachos,                         // Cantidad de machos
    int CantidadMixtas,                         // Cantidad de mixtas
    int TotalAves,                              // Total de aves
    
    // Estado y información
    string Estado,                              // "Pendiente", "Completado", "Cancelado"
    string? MotivoMovimiento,                   // Motivo del movimiento
    string? Observaciones,                      // Observaciones
    
    // Usuario
    int UsuarioMovimientoId,                    // ID del usuario
    string? UsuarioNombre,                      // Nombre del usuario
    
    // Fechas
    DateTime? FechaProcesamiento,                // Fecha de procesamiento
    DateTime? FechaCancelacion,                 // Fecha de cancelación
    DateTime CreatedAt                          // Fecha de creación
);
```

#### `UbicacionMovimientoDto` (Ubicación)
```csharp
public record UbicacionMovimientoDto(
    int? LoteId,                                // ID del lote
    string? LoteNombre,                         // Nombre del lote
    int? GranjaId,                              // ID de la granja
    string? GranjaNombre,                       // Nombre de la granja
    string? NucleoId,                           // ID del núcleo
    string? NucleoNombre,                       // Nombre del núcleo
    string? GalponId,                           // ID del galpón
    string? GalponNombre                        // Nombre del galpón
);
```

#### `CreateMovimientoAvesDto` (Crear Movimiento)
```csharp
public sealed class CreateMovimientoAvesDto
{
    public DateTime FechaMovimiento { get; set; } = DateTime.UtcNow;
    public string TipoMovimiento { get; set; } = "Traslado"; // Traslado, Venta, Ajuste, Liquidacion
    
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
    
    // Cantidades a mover
    public int CantidadHembras { get; set; }
    public int CantidadMachos { get; set; }
    public int CantidadMixtas { get; set; }
    
    // Información adicional
    public string? MotivoMovimiento { get; set; }
    public string? Observaciones { get; set; }
    
    public int UsuarioMovimientoId { get; set; }
}
```

#### `MovimientoAvesSearchRequest` (Búsqueda)
```csharp
public sealed record MovimientoAvesSearchRequest(
    string? NumeroMovimiento = null,
    string? TipoMovimiento = null,              // "Traslado", "Venta", "Ajuste", "Liquidacion"
    string? Estado = null,                       // "Pendiente", "Completado", "Cancelado"
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

### 🔍 Campos Relevantes para Reporte Contable

**Para Ventas y Traslados de Aves:**
- `TipoMovimiento`: 
  - `"Venta"` → Ventas de aves
  - `"Traslado"` → Traslados de aves
- `Estado`: Solo considerar `"Completado"` para el reporte
- `CantidadHembras`: Cantidad de hembras vendidas/trasladadas
- `CantidadMachos`: Cantidad de machos vendidos/trasladados
- `FechaMovimiento`: Fecha del movimiento (para filtrar por semana)
- `LoteOrigenId`: ID del lote origen (para filtrar por lote)

**Consulta para obtener ventas:**
```csharp
POST /api/MovimientoAves/search
Body: {
    "tipoMovimiento": "Venta",
    "estado": "Completado",
    "loteOrigenId": {loteId},
    "fechaDesde": {fechaInicio},
    "fechaHasta": {fechaFin}
}
```

**Consulta para obtener traslados:**
```csharp
POST /api/MovimientoAves/search
Body: {
    "tipoMovimiento": "Traslado",
    "estado": "Completado",
    "loteOrigenId": {loteId},
    "fechaDesde": {fechaInicio},
    "fechaHasta": {fechaFin}
}
```

**⚠️ IMPORTANTE:**
- Solo los movimientos con `Estado = "Completado"` afectan el inventario
- Los movimientos `"Pendiente"` no deben contarse en el reporte
- Los movimientos `"Cancelado"` no deben contarse en el reporte

---

## 3. 📊 MÓDULO: SEGUIMIENTO DIARIO DE LEVANTE (Daily Lifting Report)

### 📍 Controlador: `SeguimientoLoteLevanteController`

**Ruta Base:** `/api/SeguimientoLoteLevante`

### 🔌 APIs Disponibles

| Método | Endpoint | Descripción | Parámetros | Response |
|--------|----------|-------------|------------|----------|
| `GET` | `/api/SeguimientoLoteLevante/por-lote/{loteId}` | Obtiene todos los registros de un lote | `loteId` (int) | `SeguimientoLoteLevanteDto[]` |
| `POST` | `/api/SeguimientoLoteLevante` | Crea un nuevo registro diario | Body: `SeguimientoLoteLevanteDto` | `SeguimientoLoteLevanteDto` (201) |
| `PUT` | `/api/SeguimientoLoteLevante/{id}` | Edita un registro diario | `id` (int), Body: `SeguimientoLoteLevanteDto` | `SeguimientoLoteLevanteDto` |
| `DELETE` | `/api/SeguimientoLoteLevante/{id}` | Elimina un registro diario | `id` (int) | 204 o 404 |

### 📋 DTOs y Estructura de Datos

#### `SeguimientoLoteLevanteDto` (Registro Diario de Levante)
```csharp
public record SeguimientoLoteLevanteDto(
    int Id,                                      // ID del registro
    int LoteId,                                  // ID del lote
    DateTime FechaRegistro,                      // Fecha del registro
    
    // MORTALIDAD
    int MortalidadHembras,                       // Mortalidad diaria de hembras
    int MortalidadMachos,                        // Mortalidad diaria de machos
    
    // SELECCIÓN
    int SelH,                                    // Selección de hembras (retiradas)
    int SelM,                                    // Selección de machos (retiradas)
    
    // ERROR DE SEXAJE
    int ErrorSexajeHembras,                      // Error de sexaje hembras
    int ErrorSexajeMachos,                       // Error de sexaje machos
    
    // CONSUMO DE ALIMENTO
    double ConsumoKgHembras,                     // Consumo de alimento hembras (kg)
    double? ConsumoKgMachos,                     // Consumo de alimento machos (kg) - OPCIONAL
    string TipoAlimento,                         // Tipo de alimento
    
    // VALORES NUTRICIONALES (calculados)
    double? KcalAlH,                             // Kilocalorías por kg de alimento (hembras)
    double? ProtAlH,                             // Proteína por kg de alimento (hembras)
    double? KcalAveH,                            // Kilocalorías por ave por día (hembras)
    double? ProtAveH,                            // Proteína por ave por día (hembras)
    
    // CICLO
    string Ciclo,                                // "Normal" o "Reforzado"
    
    // PESO Y UNIFORMIDAD (OPCIONALES - semanales)
    double? PesoPromH,                           // Peso promedio hembras (kg)
    double? PesoPromM,                           // Peso promedio machos (kg)
    double? UniformidadH,                        // Uniformidad de hembras (%)
    double? UniformidadM,                        // Uniformidad de machos (%)
    double? CvH,                                 // Coeficiente de variación hembras
    double? CvM,                                 // Coeficiente de variación machos
    
    string? Observaciones                        // Observaciones
);
```

### 🔍 Campos Relevantes para Reporte Contable

**Para Mortalidad:**
- `MortalidadHembras`: Mortalidad diaria de hembras
- `MortalidadMachos`: Mortalidad diaria de machos
- `FechaRegistro`: Fecha del registro (para agrupar por semana)

**Para Selección:**
- `SelH`: Selección de hembras (retiradas)
- `SelM`: Selección de machos (retiradas)
- `FechaRegistro`: Fecha del registro (para agrupar por semana)

**Para Consumo:**
- `ConsumoKgHembras`: Consumo de alimento hembras (kg)
- `ConsumoKgMachos`: Consumo de alimento machos (kg)
- `TipoAlimento`: Tipo de alimento usado
- `FechaRegistro`: Fecha del registro (para agrupar por semana)

**Consulta para obtener datos de levante:**
```csharp
GET /api/SeguimientoLoteLevante/por-lote/{loteId}
```

**Filtrado por fecha (en el servicio):**
- Filtrar por `FechaRegistro` dentro del rango de la semana contable

---

## 4. 🥚 MÓDULO: SEGUIMIENTO DIARIO DE PRODUCCIÓN (Daily Production Monitoring)

### 📍 Controlador: `SeguimientoProduccionController` y `ProduccionDiariaController`

**Ruta Base:** `/api/SeguimientoProduccion` y `/api/ProduccionDiaria`

### 🔌 APIs Disponibles

#### 4.1. **SeguimientoProduccionController**

| Método | Endpoint | Descripción | Parámetros | Response |
|--------|----------|-------------|------------|----------|
| `GET` | `/api/SeguimientoProduccion` | Obtiene todos los registros | Ninguno | `SeguimientoProduccionDto[]` |
| `GET` | `/api/SeguimientoProduccion/{loteId}` | Obtiene por LoteId | `loteId` (int) | `SeguimientoProduccionDto[]` o 404 |
| `GET` | `/api/SeguimientoProduccion/filter` | Filtro por lote y/o fechas | Query params | `SeguimientoProduccionDto[]` |
| `POST` | `/api/SeguimientoProduccion` | Crea nuevo registro | Body: `CreateSeguimientoProduccionDto` | `SeguimientoProduccionDto` (201) |
| `PUT` | `/api/SeguimientoProduccion/{id}` | Actualiza registro | `id` (int), Body: `UpdateSeguimientoProduccionDto` | `SeguimientoProduccionDto` |
| `DELETE` | `/api/SeguimientoProduccion/{id}` | Elimina registro | `id` (int) | 204 o 404 |

#### 4.2. **ProduccionDiariaController**

| Método | Endpoint | Descripción | Parámetros | Response |
|--------|----------|-------------|------------|----------|
| `GET` | `/api/ProduccionDiaria` | Obtiene todos los registros | Ninguno | `ProduccionDiariaDto[]` |
| `GET` | `/api/ProduccionDiaria/{loteId}` | Obtiene por LoteId | `loteId` (string) | `ProduccionDiariaDto[]` |
| `POST` | `/api/ProduccionDiaria` | Crea nuevo registro | Body: `CreateProduccionDiariaDto` | `ProduccionDiariaDto` (201) |
| `PUT` | `/api/ProduccionDiaria/{id}` | Actualiza registro | `id` (int), Body: `UpdateProduccionDiariaDto` | `ProduccionDiariaDto` |
| `DELETE` | `/api/ProduccionDiaria/{id}` | Elimina registro | `id` (int) | 204 o 404 |

### 📋 DTOs y Estructura de Datos

#### `SeguimientoProduccionDto` (Registro Diario de Producción)
```csharp
public record SeguimientoProduccionDto(
    int Id,                                      // ID del registro
    DateTime Fecha,                              // Fecha del registro
    string LoteId,                               // ID del lote (string)
    
    // MORTALIDAD
    int MortalidadH,                             // Mortalidad diaria de hembras
    int MortalidadM,                             // Mortalidad diaria de machos
    
    // SELECCIÓN
    int SelH,                                    // Selección de hembras (retiradas)
    
    // CONSUMO DE ALIMENTO
    decimal ConsKgH,                             // Consumo de alimento hembras (kg)
    decimal ConsKgM,                             // Consumo de alimento machos (kg)
    
    // HUEVOS
    int HuevoTot,                                // Total de huevos
    int HuevoInc,                                // Huevos incubables
    
    // CLASIFICADORA DE HUEVOS - (Limpio, Tratado) = HuevoInc
    int HuevoLimpio,                             // Huevos limpios
    int HuevoTratado,                            // Huevos tratados
    
    // CLASIFICADORA DE HUEVOS - (Sucio, Deforme, Blanco, etc.) = HuevoTot
    int HuevoSucio,                              // Huevos sucios
    int HuevoDeforme,                            // Huevos deformes
    int HuevoBlanco,                             // Huevos blancos
    int HuevoDobleYema,                          // Huevos doble yema
    int HuevoPiso,                               // Huevos de piso
    int HuevoPequeno,                            // Huevos pequeños
    int HuevoRoto,                               // Huevos rotos
    int HuevoDesecho,                            // Huevos desecho
    int HuevoOtro,                               // Otros huevos
    
    // ALIMENTO Y ETAPA
    string TipoAlimento,                         // Tipo de alimento
    decimal PesoHuevo,                           // Peso del huevo (g)
    int Etapa,                                   // Etapa: 1 (semana 25-33), 2 (34-50), 3 (>50)
    
    // PESAJE SEMANAL (OPCIONAL - registro una vez por semana)
    decimal? PesoH,                              // Peso promedio hembras (kg)
    decimal? PesoM,                              // Peso promedio machos (kg)
    decimal? Uniformidad,                        // Uniformidad del lote (%)
    decimal? CoeficienteVariacion,               // Coeficiente de variación (CV)
    string? ObservacionesPesaje,                 // Observaciones del pesaje
    
    string? Observaciones                        // Observaciones generales
);
```

#### `CreateSeguimientoProduccionDto` (Crear Registro)
```csharp
public record CreateSeguimientoProduccionDto(
    DateTime Fecha,
    int LoteId,                                  // ID del lote (int)
    int MortalidadH,
    int MortalidadM,
    int SelH,
    decimal ConsKgH,
    decimal ConsKgM,
    int HuevoTot,
    int HuevoInc,
    // ... (mismos campos que SeguimientoProduccionDto)
);
```

#### `ProduccionDiariaDto` (Alternativa - DTO más simple)
```csharp
public record ProduccionDiariaDto(
    int Id,
    string LoteId,                               // ID del lote (string)
    DateTime FechaRegistro,
    int MortalidadHembras,
    int MortalidadMachos,
    int SelH,
    double ConsKgH,
    double ConsKgM,
    int HuevoTot,
    int HuevoInc,
    string TipoAlimento,
    string? Observaciones,
    double? PesoHuevo,
    int Etapa
);
```

### 🔍 Campos Relevantes para Reporte Contable

**Para Mortalidad:**
- `MortalidadH`: Mortalidad diaria de hembras
- `MortalidadM`: Mortalidad diaria de machos
- `Fecha` o `FechaRegistro`: Fecha del registro (para agrupar por semana)

**Para Selección:**
- `SelH`: Selección de hembras (retiradas)
- **NOTA:** En producción típicamente NO hay selección de machos
- `Fecha` o `FechaRegistro`: Fecha del registro (para agrupar por semana)

**Para Consumo:**
- `ConsKgH`: Consumo de alimento hembras (kg)
- `ConsKgM`: Consumo de alimento machos (kg)
- `TipoAlimento`: Tipo de alimento usado
- `Fecha` o `FechaRegistro`: Fecha del registro (para agrupar por semana)

**Consulta para obtener datos de producción:**
```csharp
GET /api/SeguimientoProduccion/{loteId}
// o
GET /api/ProduccionDiaria/{loteId}
```

**⚠️ NOTA IMPORTANTE:**
- `SeguimientoProduccion` usa `LoteId` como `string`
- `ProduccionDiaria` también usa `LoteId` como `string`
- Necesita conversión: `int.Parse(loteId)` o `loteId.ToString()`

---

## 5. 🔗 INTEGRACIÓN CON REPORTE CONTABLE

### 📊 Mapeo de Datos

#### 5.1. **Entradas Iniciales de Aves**

**Fuente:**
- **Levante:** `Lote.HembrasL`, `Lote.MachosL`
- **Producción:** `ProduccionLote.AvesInicialesH`, `ProduccionLote.AvesInicialesM`

**API:**
```csharp
// Obtener lote
GET /api/Lote/{loteId}

// Obtener ProduccionLote (si existe)
GET /api/ProduccionLote?loteId={loteId}
```

#### 5.2. **Mortalidad**

**Fuente:**
- **Levante:** `SeguimientoLoteLevante.MortalidadHembras`, `MortalidadMachos`
- **Producción:** `SeguimientoProduccion.MortalidadH`, `MortalidadM`

**API:**
```csharp
// Levante
GET /api/SeguimientoLoteLevante/por-lote/{loteId}

// Producción
GET /api/SeguimientoProduccion/{loteId}
```

#### 5.3. **Selección**

**Fuente:**
- **Levante:** `SeguimientoLoteLevante.SelH`, `SelM`
- **Producción:** `SeguimientoProduccion.SelH`

**API:**
```csharp
// Levante
GET /api/SeguimientoLoteLevante/por-lote/{loteId}

// Producción
GET /api/SeguimientoProduccion/{loteId}
```

#### 5.4. **Ventas y Traslados de Aves**

**Fuente:** `MovimientoAves`

**API:**
```csharp
POST /api/MovimientoAves/search
Body: {
    "tipoMovimiento": "Venta" | "Traslado",
    "estado": "Completado",
    "loteOrigenId": {loteId},
    "fechaDesde": {fechaInicio},
    "fechaHasta": {fechaFin}
}
```

**Campos:**
- `CantidadHembras`: Ventas/Traslados de hembras
- `CantidadMachos`: Ventas/Traslados de machos
- `FechaMovimiento`: Fecha del movimiento

#### 5.5. **Consumo de Alimento (Kg)**

**Fuente:**
- **Levante:** `SeguimientoLoteLevante.ConsumoKgHembras`, `ConsumoKgMachos`
- **Producción:** `SeguimientoProduccion.ConsKgH`, `ConsKgM`

**API:**
```csharp
// Levante
GET /api/SeguimientoLoteLevante/por-lote/{loteId}

// Producción
GET /api/SeguimientoProduccion/{loteId}
```

#### 5.6. **Entradas de Bultos**

**Fuente:** `FarmInventoryMovement` con `MovementType = "Entry"`

**API:**
```csharp
GET /api/farms/{farmId}/inventory/movements?type=Entry&catalogItemId={alimentoId}&from={fechaInicio}&to={fechaFin}
```

**Campos:**
- `Quantity`: Cantidad de bultos (si `Unit = "bultos"`) o kg (necesita conversión)
- `Unit`: Unidad de medida
- `CreatedAt`: Fecha del movimiento

#### 5.7. **Traslados de Bultos**

**Fuente:** `FarmInventoryMovement` con `MovementType = "TransferOut"`

**API:**
```csharp
GET /api/farms/{farmId}/inventory/movements?type=TransferOut&catalogItemId={alimentoId}&from={fechaInicio}&to={fechaFin}
```

**Campos:**
- `Quantity`: Cantidad de bultos trasladados
- `Unit`: Unidad de medida
- `CreatedAt`: Fecha del movimiento

#### 5.8. **Consumo de Bultos**

**Cálculo:**
- Convertir consumo de kg a bultos usando factor de conversión
- Factor típico: 1 bulto = 40-50 kg (configurable)

**Fórmula:**
```csharp
ConsumoBultos = ConsumoKg / FactorConversion
```

---

## 6. 📝 RESUMEN DE CONSULTAS PARA REPORTE CONTABLE

### Consulta Completa por Semana

```csharp
// 1. Obtener lote padre y sublotes
GET /api/Lote/{lotePadreId}
GET /api/Lote?lotePadreId={lotePadreId}

// 2. Obtener entradas iniciales
// Para cada lote:
GET /api/Lote/{loteId}  // HembrasL, MachosL
GET /api/ProduccionLote?loteId={loteId}  // AvesInicialesH, AvesInicialesM

// 3. Obtener mortalidad y selección (levante)
GET /api/SeguimientoLoteLevante/por-lote/{loteId}
// Filtrar por FechaRegistro entre fechaInicio y fechaFin

// 4. Obtener mortalidad y selección (producción)
GET /api/SeguimientoProduccion/{loteId}
// Filtrar por Fecha entre fechaInicio y fechaFin

// 5. Obtener ventas de aves
POST /api/MovimientoAves/search
Body: {
    "tipoMovimiento": "Venta",
    "estado": "Completado",
    "loteOrigenId": {loteId},
    "fechaDesde": {fechaInicio},
    "fechaHasta": {fechaFin}
}

// 6. Obtener traslados de aves
POST /api/MovimientoAves/search
Body: {
    "tipoMovimiento": "Traslado",
    "estado": "Completado",
    "loteOrigenId": {loteId},
    "fechaDesde": {fechaInicio},
    "fechaHasta": {fechaFin}
}

// 7. Obtener entradas de bultos
GET /api/farms/{farmId}/inventory/movements?type=Entry&catalogItemId={alimentoId}&from={fechaInicio}&to={fechaFin}

// 8. Obtener traslados de bultos
GET /api/farms/{farmId}/inventory/movements?type=TransferOut&catalogItemId={alimentoId}&from={fechaInicio}&to={fechaFin}
```

---

## 7. ⚠️ NOTAS IMPORTANTES

1. **Estado de Movimientos:**
   - Solo considerar movimientos con `Estado = "Completado"`
   - Los movimientos `"Pendiente"` no afectan el inventario
   - Los movimientos `"Cancelado"` no deben contarse

2. **Conversión de Unidades:**
   - Consumo se registra en **kg**
   - Bultos pueden estar en **bultos** o **kg**
   - Necesita factor de conversión: 1 bulto = X kg (configurable)

3. **Identificación de Alimento:**
   - Necesita `CatalogItemId` del producto "Alimento"
   - Puede obtenerse del catálogo de productos

4. **Filtrado por Fecha:**
   - Todas las consultas deben filtrar por rango de fechas de la semana contable
   - Semana contable = 7 días calendario consecutivos

5. **LoteId como String vs Int:**
   - `SeguimientoProduccion` usa `LoteId` como `string`
   - `MovimientoAves` usa `LoteOrigenId` como `int`
   - Necesita conversión: `int.Parse(loteId)` o `loteId.ToString()`

---

## 8. ✅ CHECKLIST DE IMPLEMENTACIÓN

- [ ] Identificar `CatalogItemId` del alimento en el catálogo
- [ ] Configurar factor de conversión kg → bultos
- [ ] Implementar consultas a APIs de inventario para bultos
- [ ] Implementar consultas a APIs de movimientos de aves para ventas/traslados
- [ ] Implementar consultas a APIs de seguimiento levante para mortalidad/selección/consumo
- [ ] Implementar consultas a APIs de seguimiento producción para mortalidad/selección/consumo
- [ ] Manejar conversión de tipos (string ↔ int) para LoteId
- [ ] Filtrar movimientos solo por estado "Completado"
- [ ] Agrupar datos por semana contable (7 días calendario)

---

## 🔗 REFERENCIAS

- **FarmInventoryController**: `backend/src/ZooSanMarino.API/Controllers/FarmInventoryController.cs`
- **FarmInventoryMovementsController**: `backend/src/ZooSanMarino.API/Controllers/FarmInventoryMovementsController.cs`
- **MovimientoAvesController**: `backend/src/ZooSanMarino.API/Controllers/MovimientoAvesController.cs`
- **SeguimientoLoteLevanteController**: `backend/src/ZooSanMarino.API/Controllers/SeguimientoLoteLevanteController.cs`
- **SeguimientoProduccionController**: `backend/src/ZooSanMarino.API/Controllers/SeguimientoProduccionController.cs`
- **ProduccionDiariaController**: `backend/src/ZooSanMarino.API/Controllers/ProduccionDiariaController.cs`











