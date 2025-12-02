# 📋 PLAN DE DESARROLLO: Mejora del Módulo de Traslado de Aves

## 🎯 Objetivo
Mejorar la funcionalidad de traslado de aves para que:
1. **Muestre claramente la disponibilidad** de hembras y machos en el modal de traslado
2. **Actualice automáticamente el inventario** del lote cuando se realiza un traslado/venta
3. **Mantenga actualizado el conteo de aves** en el lote después de cada movimiento

---

## 🔍 Análisis del Estado Actual

### ✅ Lo que ya funciona:
- El backend valida disponibilidad antes de crear el traslado
- El backend procesa automáticamente el movimiento al crearlo
- El frontend tiene código para mostrar disponibilidad (pero no se muestra correctamente)
- Existe el servicio `DisponibilidadLoteService` que calcula aves disponibles

### ❌ Lo que falta:
1. **Frontend**: La disponibilidad no se muestra claramente en el modal
   - El código existe pero puede no estar cargando correctamente
   - Los campos no muestran la disponibilidad de forma prominente

2. **Backend**: El inventario no se actualiza cuando se procesa un movimiento
   - `ProcesarMovimientoAsync` solo marca el movimiento como procesado
   - No actualiza `InventarioAves` restando las aves trasladadas
   - Solo aplica descuento en producción diaria (si el lote está en producción)

---

## 📝 Plan de Implementación

### **FASE 1: Mejorar Visualización de Disponibilidad en el Modal (Frontend)**

#### 1.1 Verificar y corregir carga de disponibilidad
- **Archivo**: `frontend/src/app/features/traslados-aves/pages/inventario-dashboard/inventario-dashboard.component.ts`
- **Acción**: 
  - Asegurar que `cargarDisponibilidadLote` se ejecute correctamente cuando se abre el modal
  - Verificar que el signal `disponibilidadLote` se actualice correctamente
  - Agregar logs de depuración si es necesario

#### 1.2 Mejorar visualización en el HTML
- **Archivo**: `frontend/src/app/features/traslados-aves/pages/inventario-dashboard/inventario-dashboard.component.html`
- **Acción**:
  - **Sección de disponibilidad visible**: Agregar una sección destacada que muestre:
    - Total de hembras disponibles
    - Total de machos disponibles
    - Total de aves disponibles
  - **Mejorar campos de entrada**:
    - Mostrar disponibilidad al lado de cada campo de forma más prominente
    - Formato: `♀ Hembras (Disponible: 1,250)` en lugar de solo `(Máx: 1250)`
    - Agregar indicador visual cuando se excede la disponibilidad
  - **Agregar validación visual**: Mostrar mensaje de error si se intenta trasladar más de lo disponible

#### 1.3 Agregar sección de resumen de disponibilidad
- **Ubicación**: Dentro del modal, antes de los campos de entrada
- **Contenido**:
  ```
  📊 Disponibilidad del Lote
  ┌─────────────────────────────────┐
  │ ♀ Hembras Disponibles: 1,250    │
  │ ♂ Machos Disponibles:   850     │
  │ 🐥 Total Aves Disponibles: 2,100│
  └─────────────────────────────────┘
  ```

---

### **FASE 2: Actualizar Inventario al Procesar Movimiento (Backend)**

#### 2.1 Modificar `ProcesarMovimientoAsync` para actualizar inventario
- **Archivo**: `backend/src/ZooSanMarino.Infrastructure/Services/MovimientoAvesService.cs`
- **Método**: `ProcesarMovimientoAsync`
- **Acción**:
  1. Después de marcar el movimiento como procesado (línea 204)
  2. Si el movimiento es de tipo "Traslado" o "Venta" y tiene `LoteOrigenId`:
     - Buscar o crear el `InventarioAves` del lote origen
     - Aplicar descuento usando `inventario.AplicarMovimientoSalida(hembras, machos, mixtas)`
     - Guardar cambios en la base de datos
  3. Si el movimiento es de tipo "Traslado" y tiene `LoteDestinoId`:
     - Buscar o crear el `InventarioAves` del lote destino
     - Aplicar entrada usando `inventario.AplicarMovimientoEntrada(hembras, machos, mixtas)`
     - Guardar cambios

#### 2.2 Crear método auxiliar para actualizar inventario
- **Archivo**: `backend/src/ZooSanMarino.Infrastructure/Services/MovimientoAvesService.cs`
- **Método nuevo**: `ActualizarInventarioPorMovimientoAsync`
- **Lógica**:
  ```csharp
  private async Task ActualizarInventarioPorMovimientoAsync(MovimientoAves movimiento)
  {
      // Si es salida (Venta o Traslado desde origen)
      if (movimiento.LoteOrigenId.HasValue && 
          (movimiento.TipoMovimiento == "Traslado" || movimiento.TipoMovimiento == "Venta"))
      {
          var inventarioOrigen = await ObtenerOCrearInventarioAsync(
              movimiento.LoteOrigenId.Value,
              movimiento.GranjaOrigenId);
          
          inventarioOrigen.AplicarMovimientoSalida(
              movimiento.CantidadHembras,
              movimiento.CantidadMachos,
              movimiento.CantidadMixtas);
          
          inventarioOrigen.UpdatedByUserId = _currentUser.UserId;
          inventarioOrigen.UpdatedAt = DateTime.UtcNow;
      }
      
      // Si es entrada (Traslado a destino)
      if (movimiento.LoteDestinoId.HasValue && movimiento.TipoMovimiento == "Traslado")
      {
          var inventarioDestino = await ObtenerOCrearInventarioAsync(
              movimiento.LoteDestinoId.Value,
              movimiento.GranjaDestinoId ?? movimiento.GranjaOrigenId);
          
          inventarioDestino.AplicarMovimientoEntrada(
              movimiento.CantidadHembras,
              movimiento.CantidadMachos,
              movimiento.CantidadMixtas);
          
          inventarioDestino.UpdatedByUserId = _currentUser.UserId;
          inventarioDestino.UpdatedAt = DateTime.UtcNow;
      }
      
      await _context.SaveChangesAsync();
  }
  ```

#### 2.3 Crear método auxiliar para obtener o crear inventario
- **Archivo**: `backend/src/ZooSanMarino.Infrastructure/Services/MovimientoAvesService.cs`
- **Método nuevo**: `ObtenerOCrearInventarioAsync`
- **Lógica**:
  - Buscar `InventarioAves` activo para el lote
  - Si no existe, crearlo con las cantidades iniciales del lote
  - Retornar el inventario

---

### **FASE 3: Actualizar Disponibilidad en Frontend Después del Traslado**

#### 3.1 Recargar disponibilidad después de crear traslado
- **Archivo**: `frontend/src/app/features/traslados-aves/pages/inventario-dashboard/inventario-dashboard.component.ts`
- **Método**: `procesarRetiroTraslado`
- **Acción**: 
  - Después de crear el traslado exitosamente (línea 1496)
  - Llamar a `cargarDisponibilidadLote` para actualizar la disponibilidad mostrada
  - Esto asegura que si el usuario quiere hacer otro traslado, vea la disponibilidad actualizada

---

## 📊 Flujo Completo Esperado

### **Antes del Traslado:**
1. Usuario selecciona un lote usando los filtros
2. Usuario hace clic en "Traslado de Aves"
3. Se abre el modal y se carga la disponibilidad del lote
4. El modal muestra claramente:
   - Sección destacada con disponibilidad total
   - En cada campo: "♀ Hembras (Disponible: X)"
   - Validación en tiempo real si excede disponibilidad

### **Durante el Traslado:**
1. Usuario ingresa cantidades de hembras y machos
2. El sistema valida que no exceda la disponibilidad
3. Si excede, muestra error visual

### **Después del Traslado:**
1. Backend crea el movimiento
2. Backend procesa el movimiento automáticamente
3. Backend actualiza el inventario:
   - Resta aves del lote origen
   - Si es traslado, suma aves al lote destino
4. Frontend recarga la disponibilidad
5. El usuario ve la disponibilidad actualizada

---

## 🧪 Casos de Prueba

### **Caso 1: Traslado de Aves**
- **Precondición**: Lote tiene 1,000 hembras y 500 machos
- **Acción**: Trasladar 100 hembras y 50 machos a otro lote
- **Resultado esperado**:
  - Lote origen queda con 900 hembras y 450 machos
  - Lote destino recibe 100 hembras y 50 machos
  - Disponibilidad se actualiza en el frontend

### **Caso 2: Venta de Aves**
- **Precondición**: Lote tiene 1,000 hembras y 500 machos
- **Acción**: Vender 200 hembras y 100 machos
- **Resultado esperado**:
  - Lote queda con 800 hembras y 400 machos
  - Disponibilidad se actualiza en el frontend

### **Caso 3: Validación de Disponibilidad**
- **Precondición**: Lote tiene 100 hembras disponibles
- **Acción**: Intentar trasladar 150 hembras
- **Resultado esperado**:
  - El sistema muestra error: "No hay suficientes hembras disponibles (Disponible: 100)"
  - No se crea el movimiento

---

## 📁 Archivos a Modificar

### **Frontend:**
1. `frontend/src/app/features/traslados-aves/pages/inventario-dashboard/inventario-dashboard.component.ts`
2. `frontend/src/app/features/traslados-aves/pages/inventario-dashboard/inventario-dashboard.component.html`
3. `frontend/src/app/features/traslados-aves/pages/inventario-dashboard/inventario-dashboard.component.scss` (si es necesario para estilos)

### **Backend:**
1. `backend/src/ZooSanMarino.Infrastructure/Services/MovimientoAvesService.cs`
   - Modificar: `ProcesarMovimientoAsync`
   - Agregar: `ActualizarInventarioPorMovimientoAsync`
   - Agregar: `ObtenerOCrearInventarioAsync`

---

## ✅ Criterios de Aceptación

1. ✅ El modal muestra claramente la disponibilidad de hembras y machos
2. ✅ Los campos de entrada muestran la disponibilidad al lado del label
3. ✅ El sistema valida que no se exceda la disponibilidad
4. ✅ Al procesar un traslado/venta, el inventario del lote se actualiza automáticamente
5. ✅ La disponibilidad se actualiza en el frontend después del traslado
6. ✅ El conteo de aves en el lote se mantiene actualizado después de cada movimiento

---

## 🚀 Orden de Implementación Recomendado

1. **Primero**: FASE 2 (Backend) - Actualizar inventario al procesar movimiento
   - Esto asegura que la lógica de negocio esté correcta
   - Se puede probar con Postman/API directamente

2. **Segundo**: FASE 1 (Frontend) - Mejorar visualización
   - Una vez que el backend funciona, mejorar la UX

3. **Tercero**: FASE 3 (Frontend) - Recargar disponibilidad
   - Asegurar que la UI se actualice después de cada operación

---

## 📝 Notas Adicionales

- El sistema ya calcula disponibilidad considerando:
  - Aves iniciales del lote
  - Mortalidad acumulada
  - Retiros acumulados (movimientos completados)
- La actualización del inventario debe ser atómica (transacción)
- Si falla la actualización del inventario, el movimiento no debe procesarse

