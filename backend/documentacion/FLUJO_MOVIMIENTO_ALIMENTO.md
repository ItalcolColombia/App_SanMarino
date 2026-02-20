# 📋 Flujo Completo - Movimiento de Alimento

## 🎯 Resumen
Este documento describe el flujo completo para registrar movimientos de alimento en el sistema, incluyendo la ruta de la API, estructura de datos y validaciones.

---

## 🔗 Ruta del Endpoint API

### **POST** - Registrar Movimiento de Alimento (Entrada)

```
POST /api/farms/{farmId}/inventory/movements/in
POST /farms/{farmId}/inventory/movements/in
```

**Parámetros de URL:**
- `farmId` (int, requerido): ID de la granja donde se registra el movimiento

**Headers:**
```
Content-Type: application/json
Authorization: Bearer {token}
```

**Body (JSON):**
```json
{
  "catalogItemId": 123,
  "quantity": 100.5,
  "unit": "kg",
  "fechaMovimiento": "2024-02-03T10:30:00Z",
  "documentoOrigen": "RVN",
  "tipoEntrada": "Entrada Nueva",
  "galponDestinoId": "GAL-001",
  "reference": "FAC-2024-001",
  "reason": "Alimento recibido de planta"
}
```

**Campos del Request:**

| Campo | Tipo | Requerido | Descripción |
|-------|------|-----------|-------------|
| `catalogItemId` | int | Sí* | ID del producto (alimento) del catálogo |
| `codigo` | string | Sí* | Código del producto (alternativa a catalogItemId) |
| `quantity` | decimal | Sí | Cantidad (debe ser > 0) |
| `unit` | string | Sí | Unidad: "kg", "und", "l", "bultos" |
| `fechaMovimiento` | string (ISO) | No | Fecha del movimiento (si no se envía, usa fecha actual) |
| `documentoOrigen` | string | Sí | Tipo: "Autoconsumo", "RVN", "EAN" |
| `tipoEntrada` | string | Sí | Tipo: "Entrada Nueva", "Traslado entre galpon", "Traslados entre granjas" |
| `galponDestinoId` | string | No | ID del galpón destino (opcional) |
| `reference` | string | No | Referencia del documento (factura, remisión, etc.) |
| `reason` | string | No | Motivo u observaciones adicionales |
| `metadata` | object | No | Metadata adicional en formato JSON |

*Debe especificarse `catalogItemId` o `codigo` (uno de los dos)

**Respuesta Exitosa (201 Created):**
```json
{
  "id": 456,
  "farmId": 1,
  "catalogItemId": 123,
  "codigo": "ALI-001",
  "nombre": "Alimento Concentrado Pollo",
  "quantity": 100.5,
  "movementType": "Entry",
  "unit": "kg",
  "reference": "FAC-2024-001",
  "reason": "Alimento recibido de planta",
  "origin": null,
  "destination": null,
  "transferGroupId": null,
  "documentoOrigen": "RVN",
  "tipoEntrada": "Entrada Nueva",
  "galponDestinoId": "GAL-001",
  "fechaMovimiento": "2024-02-03T10:30:00Z",
  "metadata": {},
  "responsibleUserId": "user-123",
  "createdAt": "2024-02-03T10:30:00Z"
}
```

**Errores Posibles:**
- `400 Bad Request`: Datos inválidos (cantidad <= 0, campos requeridos faltantes)
- `404 Not Found`: Granja o producto no encontrado
- `500 Internal Server Error`: Error del servidor

---

## 🔄 Flujo Completo del Sistema

### 1. **Frontend - Componente de Movimiento de Alimento**

**Archivo:** `frontend/src/app/features/inventario/components/movimiento-alimento-form/movimiento-alimento-form.component.ts`

**Flujo:**
1. Usuario selecciona granja → Se cargan los galpones de esa granja
2. Usuario busca/selecciona tipo de alimento (filtrado por tipo "alimento")
3. Usuario ingresa:
   - Cantidad y unidad
   - Fecha del movimiento
   - Documento origen (Autoconsumo, RVN, EAN)
   - Tipo de entrada (Entrada Nueva, Traslado entre galpon, Traslados entre granjas)
   - Galpón destino (opcional)
   - Referencia y motivo (opcionales)
4. Al enviar el formulario:
   - Se valida que todos los campos requeridos estén completos
   - Se construye el payload con los datos
   - Se llama a `InventarioService.postEntry(farmId, payload)`

### 2. **Frontend - Servicio**

**Archivo:** `frontend/src/app/features/inventario/services/inventario.service.ts`

**Método:**
```typescript
postEntry(farmId: number, payload: InventoryEntryRequest): Observable<InventoryMovementDto>
```

**Implementación:**
- Hace POST a: `${apiUrl}/farms/${farmId}/inventory/movements/in`
- Envía el payload con todos los campos del movimiento de alimento
- Retorna el movimiento creado

### 3. **Backend - Controlador**

**Archivo:** `backend/src/ZooSanMarino.API/Controllers/FarmInventoryMovementsController.cs`

**Endpoint:**
```csharp
[HttpPost("in")]
public async Task<IActionResult> PostEntry(
    int farmId,
    [FromBody] InventoryEntryRequest req,
    CancellationToken ct = default)
```

**Flujo:**
1. Recibe el request con `farmId` y `InventoryEntryRequest`
2. Llama a `FarmInventoryMovementService.PostEntryAsync()`
3. Retorna el movimiento creado con código 201 Created

### 4. **Backend - Servicio**

**Archivo:** `backend/src/ZooSanMarino.Infrastructure/Services/FarmInventoryMovementService.cs`

**Método:** `PostEntryAsync()`

**Flujo:**
1. **Validaciones:**
   - Verifica que la cantidad sea positiva
   - Resuelve el ID del producto (por `catalogItemId` o `codigo`)
   - Valida que el producto exista

2. **Obtiene datos de la granja:**
   - Obtiene `company_id` y `pais_id` de la granja
   - Si la granja no existe, lanza excepción

3. **Transacción de base de datos:**
   - Inicia una transacción
   - Obtiene o crea el inventario del producto en la granja
   - Incrementa la cantidad en el inventario
   - Crea el registro de movimiento con todos los campos:
     - `FarmId`, `CatalogItemId`, `CompanyId`, `PaisId`
     - `Quantity`, `Unit`, `MovementType` = "Entry"
     - `DocumentoOrigen`, `TipoEntrada`, `GalponDestinoId`, `FechaMovimiento`
     - `Reference`, `Reason`, `Metadata`
     - `ResponsibleUserId` (del usuario autenticado)
   - Guarda los cambios
   - Confirma la transacción

4. **Retorna:**
   - Mapea el movimiento a `InventoryMovementDto`
   - Incluye todos los campos nuevos

### 5. **Base de Datos**

**Tabla:** `farm_inventory_movements`

**Campos nuevos agregados:**
- `documento_origen` (VARCHAR(50)): Tipo de documento origen
- `tipo_entrada` (VARCHAR(50)): Tipo de entrada
- `galpon_destino_id` (VARCHAR(50)): ID del galpón destino
- `fecha_movimiento` (TIMESTAMPTZ): Fecha del movimiento

**Script SQL:**
```sql
-- Ejecutar este script para agregar los campos
-- Archivo: backend/sql/add_campos_movimiento_alimento_inventario.sql
```

**Tabla:** `farm_product_inventory`

**Actualización:**
- Se actualiza automáticamente la cantidad del producto en la granja
- Se incrementa según la cantidad del movimiento

---

## 📊 Estructura de Datos

### InventoryEntryRequest (Backend)
```csharp
public class InventoryEntryRequest
{
    public int? CatalogItemId { get; set; }
    public string? Codigo { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Reference { get; set; }
    public string? Reason { get; set; }
    public string? Origin { get; set; }
    public string? DocumentoOrigen { get; set; }      // Nuevo
    public string? TipoEntrada { get; set; }          // Nuevo
    public string? GalponDestinoId { get; set; }      // Nuevo
    public DateTimeOffset? FechaMovimiento { get; set; } // Nuevo
    public JsonDocument? Metadata { get; set; }
}
```

### InventoryEntryRequest (Frontend)
```typescript
export interface InventoryEntryRequest {
  catalogItemId?: number;
  codigo?: string;
  quantity: number;
  unit?: string;
  reference?: string;
  reason?: string;
  origin?: string;
  documentoOrigen?: string;      // Nuevo
  tipoEntrada?: string;          // Nuevo
  galponDestinoId?: string;      // Nuevo
  fechaMovimiento?: string;      // Nuevo (ISO date)
  metadata?: any;
}
```

---

## ✅ Validaciones

### Frontend:
- Granja seleccionada (requerido)
- Producto seleccionado (requerido)
- Cantidad > 0 (requerido)
- Unidad seleccionada (requerido)
- Fecha del movimiento (requerido)
- Documento origen seleccionado (requerido)
- Tipo de entrada seleccionado (requerido)
- Galpón destino (opcional)

### Backend:
- Cantidad debe ser positiva
- Producto debe existir (por ID o código)
- Granja debe existir
- Usuario autenticado (para `responsibleUserId`)

---

## 🧪 Ejemplo de Uso

### cURL
```bash
curl -X POST "https://api.example.com/api/farms/1/inventory/movements/in" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "catalogItemId": 123,
    "quantity": 100.5,
    "unit": "kg",
    "fechaMovimiento": "2024-02-03T10:30:00Z",
    "documentoOrigen": "RVN",
    "tipoEntrada": "Entrada Nueva",
    "galponDestinoId": "GAL-001",
    "reference": "FAC-2024-001",
    "reason": "Alimento recibido de planta"
  }'
```

### JavaScript/TypeScript
```typescript
const payload: InventoryEntryRequest = {
  catalogItemId: 123,
  quantity: 100.5,
  unit: 'kg',
  fechaMovimiento: new Date().toISOString(),
  documentoOrigen: 'RVN',
  tipoEntrada: 'Entrada Nueva',
  galponDestinoId: 'GAL-001',
  reference: 'FAC-2024-001',
  reason: 'Alimento recibido de planta'
};

this.inventarioService.postEntry(farmId, payload).subscribe({
  next: (movement) => {
    console.log('Movimiento registrado:', movement);
  },
  error: (err) => {
    console.error('Error:', err);
  }
});
```

---

## 📝 Notas Importantes

1. **Empresa y País:** Se obtienen automáticamente de la granja seleccionada
2. **Usuario Responsable:** Se obtiene del token JWT del usuario autenticado
3. **Fecha del Movimiento:** Si no se envía, se usa la fecha/hora actual del servidor
4. **Inventario:** Se actualiza automáticamente al registrar el movimiento
5. **Transacciones:** Todo el proceso se ejecuta en una transacción para garantizar consistencia

---

## 🔍 Verificación del Flujo

Para verificar que todo funciona correctamente:

1. ✅ Ejecutar el SQL: `backend/sql/add_campos_movimiento_alimento_inventario.sql`
2. ✅ Compilar frontend: `cd frontend && yarn build`
3. ✅ Compilar backend: `cd backend && dotnet build`
4. ✅ Probar el endpoint con Postman o cURL
5. ✅ Verificar que el movimiento se guarda en la base de datos con todos los campos
6. ✅ Verificar que el inventario se actualiza correctamente

---

## 📍 Ruta Completa para Agregar a la Base de Datos

**Script SQL:**
```
backend/sql/add_campos_movimiento_alimento_inventario.sql
```

**Ejecutar en PostgreSQL:**
```bash
psql -U usuario -d nombre_base_datos -f backend/sql/add_campos_movimiento_alimento_inventario.sql
```

O desde psql:
```sql
\i backend/sql/add_campos_movimiento_alimento_inventario.sql
```

---

**Última actualización:** 2024-02-03
