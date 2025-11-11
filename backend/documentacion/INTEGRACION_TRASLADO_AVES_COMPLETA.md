# INTEGRACIÓN COMPLETA: TRASLADO DE AVES CON SEGUIMIENTO DIARIO

## 📋 RESUMEN

Se han implementado las integraciones pendientes del módulo de **Traslado de Aves** con los módulos de **Seguimiento Diario Levante** y **Seguimiento Diario Producción** para registrar automáticamente retiros de aves cuando se registran mortalidades o selecciones.

---

## ✅ CAMBIOS IMPLEMENTADOS

### 1. Método Helper en MovimientoAvesService

**Archivo**: `backend/src/ZooSanMarino.Infrastructure/Services/MovimientoAvesService.cs`

**Método agregado**: `RegistrarRetiroDesdeSeguimientoAsync`

**Funcionalidad**:
- Crea automáticamente un movimiento de tipo "Retiro" desde seguimiento diario
- Busca o crea el inventario del lote si no existe
- Valida disponibilidad de aves antes de crear el retiro
- Procesa automáticamente el movimiento para actualizar el inventario
- Resta las aves del inventario del lote

**Parámetros**:
```csharp
int loteId,
int hembrasRetiradas,
int machosRetirados,
int mixtasRetiradas,
DateTime fechaMovimiento,
string fuenteSeguimiento, // "Levante" o "Produccion"
string? observaciones = null
```

**Flujo**:
1. Valida que hay aves para retirar (> 0)
2. Obtiene información del lote (granja, núcleo, galpón)
3. Busca inventario activo del lote
4. Si no existe inventario, lo crea con cantidades iniciales del lote
5. Valida disponibilidad suficiente
6. Crea movimiento de tipo "Retiro"
7. Procesa automáticamente el movimiento
8. Actualiza inventario restando las aves retiradas

---

### 2. Integración en SeguimientoLoteLevanteService

**Archivo**: `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoLoteLevanteService.cs`

**Cambios**:
- Inyectado `IMovimientoAvesService` como dependencia
- Agregada llamada a `RegistrarRetiroDesdeSeguimientoAsync` en `CreateAsync`
- Agregada llamada a `RegistrarRetiroDesdeSeguimientoAsync` en `UpdateAsync`

**Lógica de retiros**:
- **Hembras retiradas**: `MortalidadHembras + SelH`
- **Machos retirados**: `MortalidadMachos + SelM`
- **Mixtas retiradas**: `0` (los seguimientos levante no tienen mixtas)

**Comportamiento**:
- Solo registra retiro si hay mortalidades o selecciones > 0
- Si falla el registro del retiro, no falla el guardado del seguimiento (log error)
- Fecha del movimiento = fecha del registro del seguimiento

---

### 3. Integración en ProduccionDiariaService

**Archivo**: `backend/src/ZooSanMarino.Infrastructure/Services/ProduccionDiariaService.cs`

**Cambios**:
- Inyectado `IMovimientoAvesService` como dependencia
- Agregada llamada a `RegistrarRetiroDesdeSeguimientoAsync` en `CreateAsync`
- Agregada llamada a `RegistrarRetiroDesdeSeguimientoAsync` en `UpdateAsync`

**Lógica de retiros**:
- **Hembras retiradas**: `MortalidadH + SelH`
- **Machos retirados**: `MortalidadM`
- **Mixtas retiradas**: `0` (los seguimientos producción no tienen mixtas)

**Comportamiento**:
- Convierte `LoteId` de string a int antes de llamar al método
- Solo registra retiro si hay mortalidades o selecciones > 0
- Si falla el registro del retiro, no falla el guardado del seguimiento (log error)

---

### 4. Ajuste en Validación de Movimientos

**Archivo**: `backend/src/ZooSanMarino.Infrastructure/Services/MovimientoAvesService.cs`

**Cambio en `ValidarMovimientoAsync`**:
- Permite movimientos de tipo "Retiro" sin destino
- Los retiros solo requieren origen (lote o inventario)
- Mantiene validaciones estrictas para traslados normales

---

### 5. Registro de Servicios en Program.cs

**Archivo**: `backend/src/ZooSanMarino.API/Program.cs`

**Cambios**:
- Reordenados servicios para registrar `IMovimientoAvesService` **antes** de `SeguimientoLoteLevanteService` y `ProduccionDiariaService`
- Esto asegura que la inyección de dependencias funcione correctamente

**Orden de registro**:
```csharp
// Sistema de Inventario de Aves (registrado antes para inyección en seguimientos)
builder.Services.AddScoped<IInventarioAvesService, InventarioAvesService>();
builder.Services.AddScoped<IHistorialInventarioService, HistorialInventarioService>();
builder.Services.AddScoped<IMovimientoAvesService, MovimientoAvesService>();

builder.Services.AddScoped<ISeguimientoLoteLevanteService, SeguimientoLoteLevanteService>();
builder.Services.AddScoped<IProduccionDiariaService, ProduccionDiariaService>();
```

---

## 🔄 FLUJOS DE INTEGRACIÓN

### Flujo 1: Registro de Seguimiento Diario Levante con Mortalidades

```
1. Usuario registra seguimiento diario levante
   ↓
2. SeguimientoLoteLevanteService.CreateAsync()
   ↓
3. Guarda seguimiento en BD
   ↓
4. Si hay mortalidades/selecciones > 0:
   ↓
5. MovimientoAvesService.RegistrarRetiroDesdeSeguimientoAsync()
   - TipoMovimiento = "Retiro"
   - CantidadHembras = MortalidadHembras + SelH
   - CantidadMachos = MortalidadMachos + SelM
   ↓
6. Crea MovimientoAves (estado: "Pendiente")
   ↓
7. Procesa automáticamente el movimiento
   ↓
8. Actualiza InventarioAves:
   - Inventario.CantidadHembras -= hembrasRetiradas
   - Inventario.CantidadMachos -= machosRetirados
   ↓
9. MovimientoAves.Estado = "Completado"
   ↓
10. Retorna seguimiento guardado exitosamente
```

### Flujo 2: Registro de Seguimiento Diario Producción con Mortalidades

```
1. Usuario registra seguimiento diario producción
   ↓
2. ProduccionDiariaService.CreateAsync()
   ↓
3. Guarda seguimiento en BD
   ↓
4. Si hay mortalidades/selecciones > 0:
   ↓
5. Convierte LoteId (string) → int
   ↓
6. MovimientoAvesService.RegistrarRetiroDesdeSeguimientoAsync()
   - TipoMovimiento = "Retiro"
   - CantidadHembras = MortalidadH + SelH
   - CantidadMachos = MortalidadM
   ↓
7. Crea y procesa movimiento automáticamente
   ↓
8. Actualiza inventario
   ↓
9. Retorna seguimiento guardado exitosamente
```

---

## 📊 DATOS QUE SE REGISTRAN

### Movimiento de Retiro Creado Automáticamente

**Campos principales**:
- `TipoMovimiento`: "Retiro"
- `LoteOrigenId`: ID del lote donde se registró el seguimiento
- `GranjaOrigenId`: Granja del lote
- `NucleoOrigenId`: Núcleo del lote (si existe)
- `GalponOrigenId`: Galpón del lote (si existe)
- `CantidadHembras`: Suma de mortalidad + selección de hembras
- `CantidadMachos`: Suma de mortalidad + selección de machos
- `CantidadMixtas`: 0 (no aplica para seguimientos)
- `Estado`: "Completado" (procesado automáticamente)
- `FechaMovimiento`: Fecha del registro del seguimiento
- `MotivoMovimiento`: "Retiro automático desde seguimiento diario (Levante/Produccion)"
- `Observaciones`: Detalle de mortalidades y selecciones + observaciones del seguimiento
- `UsuarioMovimientoId`: Usuario que registró el seguimiento
- `NumeroMovimiento`: Generado automáticamente (ej: "MOV-20251015-000001")

---

## 🛡️ VALIDACIONES Y ERRORES

### Validaciones Implementadas

1. **Validación de disponibilidad**:
   - Si no existe inventario, verifica que las cantidades del lote sean suficientes
   - Si existe inventario, verifica que el inventario tenga suficientes aves

2. **Manejo de errores**:
   - Si falla el registro del retiro, el seguimiento **SÍ se guarda**
   - Los errores se registran en consola (TODO: mejorarlo con logging apropiado)
   - El usuario puede continuar trabajando normalmente

3. **Validación de movimientos tipo Retiro**:
   - Los retiros no requieren destino
   - Solo requieren origen (lote o inventario)
   - Mantienen todas las demás validaciones

---

## 📝 EJEMPLOS DE USO

### Ejemplo 1: Seguimiento Levante con Mortalidades

**Input del usuario**:
```
Lote: 123
Fecha: 2025-10-15
MortalidadHembras: 5
MortalidadMachos: 2
SelH: 3
SelM: 1
```

**Resultado**:
- Seguimiento guardado ✅
- Movimiento de retiro creado automáticamente:
  - Hembras retiradas: 8 (5 + 3)
  - Machos retirados: 3 (2 + 1)
  - Tipo: "Retiro"
  - Estado: "Completado"
- Inventario actualizado:
  - CantidadHembras -= 8
  - CantidadMachos -= 3

---

### Ejemplo 2: Seguimiento Producción con Mortalidades

**Input del usuario**:
```
Lote: "456" (string)
Fecha: 2025-10-15
MortalidadH: 10
MortalidadM: 5
SelH: 2
```

**Resultado**:
- Seguimiento guardado ✅
- Movimiento de retiro creado automáticamente:
  - Hembras retiradas: 12 (10 + 2)
  - Machos retirados: 5
  - Tipo: "Retiro"
  - Estado: "Completado"
- Inventario actualizado

---

## 🔍 MÉTODOS Y ARCHIVOS MODIFICADOS

### Backend

1. **`IMovimientoAvesService.cs`**
   - Agregado método `RegistrarRetiroDesdeSeguimientoAsync`

2. **`MovimientoAvesService.cs`**
   - Implementado `RegistrarRetiroDesdeSeguimientoAsync`
   - Ajustado `ValidarMovimientoAsync` para permitir retiros sin destino

3. **`SeguimientoLoteLevanteService.cs`**
   - Inyectado `IMovimientoAvesService`
   - Integración en `CreateAsync`
   - Integración en `UpdateAsync`

4. **`ProduccionDiariaService.cs`**
   - Inyectado `IMovimientoAvesService`
   - Integración en `CreateAsync`
   - Integración en `UpdateAsync`

5. **`Program.cs`**
   - Reordenado registro de servicios para inyección correcta

---

## ✅ ESTADO DE INTEGRACIONES

| Integración | Estado | Descripción |
|-------------|--------|-------------|
| Seguimiento Diario Levante → MovimientoAves | ✅ **Completado** | Registra retiros automáticamente al crear/actualizar seguimiento |
| Seguimiento Diario Producción → MovimientoAves | ✅ **Completado** | Registra retiros automáticamente al crear/actualizar seguimiento |
| Procesamiento Automático de Retiros | ✅ **Completado** | Los retiros se procesan automáticamente al crearse |
| Actualización de Inventarios | ✅ **Completado** | El inventario se actualiza automáticamente al procesar retiros |

---

## 🚀 PRÓXIMOS PASOS RECOMENDADOS

1. **Mejorar logging**:
   - Reemplazar `Console.WriteLine` por `ILogger`
   - Registrar errores de integración en logs estructurados

2. **Manejo de transacciones**:
   - Considerar usar transacciones para garantizar consistencia
   - Si falla el retiro, ¿se debe revertir el seguimiento?

3. **Optimización**:
   - Cachear información del lote para evitar queries repetidas
   - Validar rendimiento con grandes volúmenes de datos

4. **Testing**:
   - Crear tests unitarios para `RegistrarRetiroDesdeSeguimientoAsync`
   - Tests de integración para flujos completos

5. **Frontend**:
   - Mostrar movimientos de retiro creados automáticamente
   - Notificar al usuario cuando se crea un retiro automático

---

## 📚 DOCUMENTACIÓN RELACIONADA

- [Análisis Completo del Módulo de Traslado de Aves](./MODULO_TRASLADO_AVES_ANALISIS_COMPLETO.md)
- [Análisis del Módulo de Inventario de Productos](./MODULO_INVENTARIO_PRODUCTOS_ANALISIS.md)

---

**Fecha de implementación**: 2025-10-15  
**Versión**: 1.0.0  
**Estado**: ✅ Implementado y listo para pruebas



