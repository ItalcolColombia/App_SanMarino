# 📋 Plan de Mejoras - Módulo de Gestión de Inventario

## 🎯 Objetivo
Mejorar el módulo de gestión de inventario para soportar:
- Registro completo de productos (alimento, vacuna, aseo)
- Cantidades en kilos o unidades
- Kardex por granja
- Carga masiva por Excel
- Asociación con empresa y país
- Nuevo modal de movimiento de inventario/bodega
- Movimiento de alimento con campos específicos

---

## 📊 Análisis de la Estructura Actual

### Backend

#### Entidades Existentes:
1. **FarmProductInventory** (`farm_product_inventory`)
   - Campos actuales: `id`, `farm_id`, `catalog_item_id`, `quantity`, `unit`, `location`, `lot_number`, `expiration_date`, `unit_cost`, `metadata`, `active`, `responsible_user_id`, `created_at`, `updated_at`
   - **Faltan**: `company_id`, `pais_id`

2. **FarmInventoryMovement** (`farm_inventory_movements`)
   - Campos actuales: `id`, `farm_id`, `catalog_item_id`, `quantity`, `movement_type`, `unit`, `reference`, `reason`, `origin`, `destination`, `transfer_group_id`, `metadata`, `responsible_user_id`, `created_at`
   - **Faltan**: `company_id`, `pais_id`, `documento_origen`, `tipo_entrada`, `galpon_destino_id`, `fecha_movimiento`

3. **CatalogItem** (`catalogo_items`)
   - Campos actuales: `id`, `codigo`, `nombre`, `metadata`, `activo`, `created_at`, `updated_at`
   - El `type_item` (alimento, vacuna, aseo) está en `metadata` como JSONB

#### Servicios Existentes:
- `FarmInventoryService`: CRUD de inventario
- `FarmInventoryMovementService`: Movimientos de inventario
- `FarmInventoryReportService`: Reportes y Kardex

#### Controladores Existentes:
- `FarmInventoryController`: Endpoints de inventario
- `FarmInventoryMovementsController`: Endpoints de movimientos

### Frontend

#### Componentes Existentes:
- `InventarioTabsComponent`: Pestañas principales
- `InventarioListComponent`: Lista de stock
- `MovimientosUnificadoFormComponent`: Formulario de movimientos
- `TrasladoFormComponent`: Traslados entre granjas
- `AjusteFormComponent`: Ajustes de inventario
- `KardexListComponent`: Historial Kardex
- `ConteoFisicoComponent`: Conteo físico
- `CatalogoAlimentosTabComponent`: Catálogo de productos

#### Servicio:
- `InventarioService`: Servicio Angular con todos los métodos

---

## 🔧 Mejoras a Implementar

### 1. Campos de Empresa y País

**SQL:**
```sql
-- Agregar company_id y pais_id a farm_product_inventory
ALTER TABLE farm_product_inventory 
ADD COLUMN company_id INTEGER,
ADD COLUMN pais_id INTEGER;

-- Agregar company_id y pais_id a farm_inventory_movements
ALTER TABLE farm_inventory_movements 
ADD COLUMN company_id INTEGER,
ADD COLUMN pais_id INTEGER;
```

**Backend:**
- Actualizar `FarmProductInventory` entity
- Actualizar `FarmInventoryMovement` entity
- Actualizar configuraciones de EF Core
- Actualizar DTOs
- Actualizar servicios para incluir company_id y pais_id automáticamente desde el contexto

### 2. Campos para Movimiento de Alimento

**SQL:**
```sql
-- Agregar campos específicos para movimiento de alimento
ALTER TABLE farm_inventory_movements 
ADD COLUMN documento_origen VARCHAR(50), -- 'Autoconsumo', 'RVN', 'EAN'
ADD COLUMN tipo_entrada VARCHAR(50),     -- 'Entrada Nueva', 'Traslado entre galpon', 'Traslados entre granjas'
ADD COLUMN galpon_destino_id VARCHAR(50),
ADD COLUMN fecha_movimiento TIMESTAMP WITH TIME ZONE;
```

**Backend:**
- Actualizar `FarmInventoryMovement` entity
- Actualizar DTOs de movimientos
- Actualizar servicios

### 3. Nuevo Modal de Movimiento de Inventario/Bodega

**Frontend:**
- Crear `ModalMovimientoInventarioComponent`
- Incluir campos:
  - Fecha
  - Cantidad (kg o unidades)
  - Tipo de producto (alimento, vacuna, aseo)
  - Documento origen (Autoconsumo, RVN, EAN)
  - Tipo de entrada (Nueva, Traslado entre galpones, Traslados entre granjas)
  - Galpón destino
  - Granja origen/destino
- Integrar con servicios existentes mejorados

### 4. Carga Masiva por Excel

**Backend:**
- Crear endpoint `POST /api/farms/{farmId}/inventory/import-excel`
- Procesar archivo Excel
- Validar datos
- Crear/actualizar inventario masivamente

**Frontend:**
- Agregar componente de carga de archivo
- Mostrar preview de datos
- Confirmar importación
- Mostrar resultados

### 5. Mejoras en Servicios

**Backend:**
- Mejorar validaciones
- Agregar logs
- Mejorar manejo de errores
- Optimizar consultas

**Frontend:**
- Mejorar manejo de errores
- Agregar loading states
- Mejorar UX con modales de confirmación

### 6. Kardex por Granja

**Backend:**
- Mejorar endpoint de Kardex para filtrar por granja
- Agregar filtros adicionales

**Frontend:**
- Mejorar componente de Kardex
- Agregar filtros por granja, producto, fecha

---

## 📝 Próximos Pasos

1. ✅ Analizar estructura actual (COMPLETADO)
2. ⏳ Crear SQL para nuevos campos
3. ⏳ Actualizar entidades y DTOs
4. ⏳ Actualizar servicios
5. ⏳ Crear nuevo modal de movimiento
6. ⏳ Implementar carga masiva
7. ⏳ Mejorar servicios existentes
8. ⏳ Actualizar frontend
