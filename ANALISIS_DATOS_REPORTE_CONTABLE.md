# 📊 ANÁLISIS DE DATOS PARA REPORTE CONTABLE

## 🎯 Objetivo
Identificar qué datos se están usando actualmente en el módulo de reportes contables y qué datos faltan que deben incluirse, segmentados por semana.

---

## 📋 DATOS ACTUALES EN EL REPORTE CONTABLE

### ✅ Datos que YA se están usando:

1. **Información del Lote**
   - `LotePadreId` y `LotePadreNombre`
   - `GranjaId` y `GranjaNombre`
   - `NucleoId` y `NucleoNombre`
   - `FechaPrimeraLlegada` (fecha mínima de encaset de todos los sublotes)

2. **Consumo de Alimentos** ✅
   - **Fuente Levante**: `SeguimientoLoteLevante.ConsumoKgHembras` + `ConsumoKgMachos`
   - **Fuente Producción**: `SeguimientoProduccion.ConsKgH` + `ConsKgM`
   - Se agrupa por semana contable
   - Se consolida diariamente y semanalmente

3. **Semanas Contables**
   - Se calculan desde `FechaPrimeraLlegada` hasta hoy
   - Cada semana = 7 días calendario
   - Se puede filtrar por semana específica

---

## ❌ DATOS FALTANTES QUE SE NECESITAN

### 1. **ENTRADAS (Entries) - Aves Iniciales del Lote**

**¿Qué se necesita?**
- Cantidad inicial de hembras al inicio del lote
- Cantidad inicial de machos al inicio del lote
- Total de entradas

**¿Dónde está la información?**

#### Para Lotes en LEVANTE:
- **Tabla**: `lotes`
- **Campos**: 
  - `HembrasL` (int?) - Cantidad inicial de hembras
  - `MachosL` (int?) - Cantidad inicial de machos
- **Entidad**: `Lote.HembrasL`, `Lote.MachosL`

#### Para Lotes en PRODUCCIÓN:
- **Tabla**: `produccion_lotes`
- **Campos**:
  - `aves_iniciales_h` (int) - Cantidad inicial de hembras
  - `aves_iniciales_m` (int) - Cantidad inicial de machos
- **Entidad**: `ProduccionLote.AvesInicialesH`, `ProduccionLote.AvesInicialesM`

**Implementación sugerida:**
```csharp
// En ReporteContableService.ObtenerEntradasInicialesAsync()
// Para cada lote, verificar si está en levante o producción
// Si tiene ProduccionLote -> usar avesInicialesH/M
// Si no -> usar Lote.HembrasL/MachosL
```

---

### 2. **MORTALIDAD (Mortality)**

**¿Qué se necesita?**
- Mortalidad de hembras por semana
- Mortalidad de machos por semana
- Mortalidad total por semana
- Identificar en qué semanas hubo mayor mortalidad

**¿Dónde está la información?**

#### Para Lotes en LEVANTE:
- **Tabla**: `seguimiento_lote_levante`
- **Campos**:
  - `MortalidadHembras` (int) - Mortalidad diaria de hembras
  - `MortalidadMachos` (int) - Mortalidad diaria de machos
- **Entidad**: `SeguimientoLoteLevante.MortalidadHembras`, `SeguimientoLoteLevante.MortalidadMachos`

#### Para Lotes en PRODUCCIÓN:
- **Tabla**: `produccion_diaria` (SeguimientoProduccion)
- **Campos**:
  - `MortalidadH` (int) - Mortalidad diaria de hembras
  - `MortalidadM` (int) - Mortalidad diaria de machos
- **Entidad**: `SeguimientoProduccion.MortalidadH`, `SeguimientoProduccion.MortalidadM`

**Implementación sugerida:**
```csharp
// Agrupar por semana contable y sumar mortalidades diarias
// Incluir en ReporteContableSemanalDto:
// - MortalidadHembrasSemanal
// - MortalidadMachosSemanal
// - MortalidadTotalSemanal
```

---

### 3. **SELECCIÓN (Selection) - Retiro de Aves**

**¿Qué se necesita?**
- Selección de hembras por semana
- Selección de machos por semana (solo en levante)
- Total de selecciones por semana

**¿Dónde está la información?**

#### Para Lotes en LEVANTE:
- **Tabla**: `seguimiento_lote_levante`
- **Campos**:
  - `SelH` (int) - Selección de hembras diaria
  - `SelM` (int) - Selección de machos diaria
- **Entidad**: `SeguimientoLoteLevante.SelH`, `SeguimientoLoteLevante.SelM`

#### Para Lotes en PRODUCCIÓN:
- **Tabla**: `produccion_diaria` (SeguimientoProduccion)
- **Campos**:
  - `SelH` (int) - Selección de hembras diaria
  - **NOTA**: En producción típicamente NO hay selección de machos
- **Entidad**: `SeguimientoProduccion.SelH`

**Implementación sugerida:**
```csharp
// Agrupar por semana contable y sumar selecciones diarias
// Incluir en ReporteContableSemanalDto:
// - SeleccionHembrasSemanal
// - SeleccionMachosSemanal (solo para levante)
```

---

### 4. **BALANCE DE AVES (Bird Balance)**

**¿Qué se necesita?**
- Saldo inicial de hembras al inicio de la semana
- Saldo inicial de machos al inicio de la semana
- Saldo final de hembras al final de la semana
- Saldo final de machos al final de la semana
- Total de aves vivas por semana

**¿Cómo se calcula?**

**Fórmula:**
```
Saldo Inicial Semana N = Saldo Final Semana N-1
Saldo Final Semana N = Saldo Inicial - Mortalidad - Selección - Ventas - Traslados
```

**Para la Primera Semana:**
```
Saldo Inicial = Entradas Iniciales (HembrasL/MachosL o avesInicialesH/M)
```

**Implementación sugerida:**
```csharp
// Calcular saldo acumulado semana por semana
// Incluir en ReporteContableSemanalDto:
// - SaldoInicioHembras
// - SaldoInicioMachos
// - SaldoFinHembras
// - SaldoFinMachos
// - TotalAvesVivas
```

---

### 5. **VENTAS Y TRASLADOS (Sales & Transfers)**

**¿Qué se necesita?**
- Ventas de hembras por semana
- Ventas de machos por semana
- Traslados de hembras por semana
- Traslados de machos por semana
- Total de aves vendidas/trasladadas por semana

**¿Dónde está la información?**

- **Tabla**: `movimiento_aves`
- **Campos**:
  - `TipoMovimiento` (string) - "Venta" o "Traslado"
  - `CantidadHembras` (int) - Cantidad de hembras
  - `CantidadMachos` (int) - Cantidad de machos
  - `FechaMovimiento` (DateTime) - Fecha del movimiento
  - `LoteOrigenId` (int) - ID del lote origen
  - `Estado` (string) - Solo considerar "Completado"
- **Entidad**: `MovimientoAves`

**Implementación sugerida:**
```csharp
// Filtrar por semana contable y tipo de movimiento
// Incluir en ReporteContableSemanalDto:
// - VentasHembrasSemanal
// - VentasMachosSemanal
// - TrasladosHembrasSemanal
// - TrasladosMachosSemanal
```

**Ejemplo de consulta:**
```csharp
var movimientos = await _ctx.MovimientoAves
    .AsNoTracking()
    .Where(m => m.LoteOrigenId == loteId &&
               m.FechaMovimiento >= semana.FechaInicio &&
               m.FechaMovimiento <= semana.FechaFin &&
               m.Estado == "Completado")
    .ToListAsync(ct);

var ventasH = movimientos
    .Where(m => m.TipoMovimiento == "Venta")
    .Sum(m => m.CantidadHembras);
```

---

### 6. **INGRESOS (Income) - Registro de Ingresos**

**¿Qué se necesita?**
- Si se registraron ingresos en la semana
- Monto de ingresos (si aplica)
- Tipo de ingreso (venta de aves, venta de huevos, etc.)

**⚠️ NOTA IMPORTANTE:**
Actualmente **NO existe una tabla específica de ingresos** en el sistema. Los ingresos se pueden inferir de:
1. **Ventas de Aves**: `MovimientoAves` con `TipoMovimiento = "Venta"`
2. **Ventas de Huevos**: `TrasladoHuevos` con `TipoOperacion = "Venta"`

**Implementación sugerida:**
```csharp
// Incluir en ReporteContableSemanalDto:
// - TieneIngresos (bool) - Si hubo ventas en la semana
// - IngresosPorVentasAves (decimal?) - Monto si está disponible
// - IngresosPorVentasHuevos (decimal?) - Monto si está disponible
```

**⚠️ RECOMENDACIÓN:**
Si se necesita registrar montos de ingresos, sería necesario:
1. Agregar campos de precio/monto en `MovimientoAves` para ventas
2. Agregar campos de precio/monto en `TrasladoHuevos` para ventas de huevos
3. O crear una tabla separada de `Ingresos` que relacione con movimientos

---

### 7. **CONSUMO DE PRODUCTOS (Product Consumption)**

**¿Qué se necesita?**
- Cantidad de bultos de alimento que entraron al lote
- Otros productos (medicamentos, vacunas, etc.)

**Estado actual:**
- ✅ Ya se está capturando consumo de alimento (kg)
- ❌ Falta: Entradas de bultos/productos al lote
- ❌ Falta: Consumo de agua, medicamentos, vacunas (marcados como TODO en el código)

**¿Dónde buscar información de entradas de productos?**
- Buscar en módulos de inventario de granja
- Buscar en módulos de recepción de productos
- Puede que no exista aún y necesite implementarse

---

## 📊 ESTRUCTURA DE DATOS PROPUESTA

### DTOs Actualizados

```csharp
// ConsumoDiarioContableDto - YA EXISTE, solo agregar campos faltantes
public record ConsumoDiarioContableDto
{
    public DateTime Fecha { get; init; }
    public int LoteId { get; init; }
    public string LoteNombre { get; init; } = string.Empty;
    
    // Consumos (YA EXISTEN)
    public decimal ConsumoAlimento { get; init; }
    public decimal ConsumoAgua { get; init; }
    public decimal ConsumoMedicamento { get; init; }
    public decimal ConsumoVacuna { get; init; }
    public decimal OtrosConsumos { get; init; }
    public decimal TotalConsumo { get; init; }
    
    // NUEVOS CAMPOS
    public int MortalidadHembras { get; init; }
    public int MortalidadMachos { get; init; }
    public int SeleccionHembras { get; init; }
    public int SeleccionMachos { get; init; }
    public int VentasHembras { get; init; }
    public int VentasMachos { get; init; }
    public int TrasladosHembras { get; init; }
    public int TrasladosMachos { get; init; }
    public int SaldoHembras { get; init; }
    public int SaldoMachos { get; init; }
}

// ReporteContableSemanalDto - ACTUALIZAR
public record ReporteContableSemanalDto
{
    // Información de semana (YA EXISTE)
    public int SemanaContable { get; init; }
    public DateTime FechaInicio { get; init; }
    public DateTime FechaFin { get; init; }
    public int LotePadreId { get; init; }
    public string LotePadreNombre { get; init; } = string.Empty;
    public List<string> Sublotes { get; init; } = new();
    
    // Consumos (YA EXISTE)
    public decimal ConsumoTotalAlimento { get; init; }
    public decimal ConsumoTotalAgua { get; init; }
    public decimal ConsumoTotalMedicamento { get; init; }
    public decimal ConsumoTotalVacuna { get; init; }
    public decimal OtrosConsumos { get; init; }
    public decimal TotalGeneral { get; init; }
    
    // NUEVOS CAMPOS - Entradas
    public int EntradasInicialesHembras { get; init; }
    public int EntradasInicialesMachos { get; init; }
    public int TotalEntradas { get; init; }
    
    // NUEVOS CAMPOS - Mortalidad
    public int MortalidadHembrasSemanal { get; init; }
    public int MortalidadMachosSemanal { get; init; }
    public int MortalidadTotalSemanal { get; init; }
    public decimal PorcentajeMortalidadSemanal { get; init; }
    
    // NUEVOS CAMPOS - Selección
    public int SeleccionHembrasSemanal { get; init; }
    public int SeleccionMachosSemanal { get; init; }
    public int TotalSeleccionSemanal { get; init; }
    
    // NUEVOS CAMPOS - Ventas y Traslados
    public int VentasHembrasSemanal { get; init; }
    public int VentasMachosSemanal { get; init; }
    public int TrasladosHembrasSemanal { get; init; }
    public int TrasladosMachosSemanal { get; init; }
    public int TotalVentasSemanal { get; init; }
    public int TotalTrasladosSemanal { get; init; }
    
    // NUEVOS CAMPOS - Balance de Aves
    public int SaldoInicioHembras { get; init; }
    public int SaldoInicioMachos { get; init; }
    public int SaldoFinHembras { get; init; }
    public int SaldoFinMachos { get; init; }
    public int TotalAvesVivas { get; init; }
    
    // NUEVOS CAMPOS - Ingresos
    public bool TieneIngresos { get; init; }
    public decimal? IngresosPorVentasAves { get; init; }
    public decimal? IngresosPorVentasHuevos { get; init; }
    
    // Detalle diario (YA EXISTE)
    public List<ConsumoDiarioContableDto> ConsumosDiarios { get; init; } = new();
}
```

---

## 🔧 IMPLEMENTACIÓN SUGERIDA

### 1. Actualizar `ReporteContableService.cs`

#### Método: `ObtenerDatosDiariosCompletosAsync()`
```csharp
private async Task<List<DatoDiarioContableDto>> ObtenerDatosDiariosCompletosAsync(
    List<Lote> lotes,
    CancellationToken ct)
{
    var datosDiarios = new List<DatoDiarioContableDto>();
    var loteIds = lotes.Where(l => l.LoteId.HasValue).Select(l => l.LoteId!.Value).ToList();
    
    // 1. Obtener entradas iniciales
    var entradasIniciales = await ObtenerEntradasInicialesAsync(lotes, ct);
    
    // 2. Obtener datos de levante
    var datosLevante = await ObtenerDatosLevanteAsync(loteIds, ct);
    
    // 3. Obtener datos de producción
    var datosProduccion = await ObtenerDatosProduccionAsync(loteIds, ct);
    
    // 4. Obtener ventas y traslados
    var ventasTraslados = await ObtenerVentasYTrasladosAsync(loteIds, ct);
    
    // 5. Consolidar y calcular saldos
    // ... lógica de consolidación
    
    return datosDiarios;
}
```

#### Método: `ObtenerEntradasInicialesAsync()`
```csharp
private async Task<Dictionary<int, (int hembras, int machos)>> ObtenerEntradasInicialesAsync(
    List<Lote> lotes,
    CancellationToken ct)
{
    var entradas = new Dictionary<int, (int, int)>();
    var loteIds = lotes.Where(l => l.LoteId.HasValue).Select(l => l.LoteId!.Value).ToList();
    
    // Para lotes en producción
    var produccionLotes = await _ctx.ProduccionLotes
        .AsNoTracking()
        .Where(p => loteIds.Contains(p.LoteId))
        .ToListAsync(ct);
    
    foreach (var pl in produccionLotes)
    {
        entradas[pl.LoteId] = (pl.AvesInicialesH, pl.AvesInicialesM);
    }
    
    // Para lotes en levante (que no tienen ProduccionLote)
    foreach (var lote in lotes)
    {
        if (lote.LoteId.HasValue && !entradas.ContainsKey(lote.LoteId.Value))
        {
            entradas[lote.LoteId.Value] = (
                lote.HembrasL ?? 0,
                lote.MachosL ?? 0
            );
        }
    }
    
    return entradas;
}
```

#### Método: `ObtenerVentasYTrasladosAsync()`
```csharp
private async Task<Dictionary<(int loteId, DateTime fecha), (int ventasH, int ventasM, int trasladosH, int trasladosM)>> 
    ObtenerVentasYTrasladosAsync(
    List<int> loteIds,
    CancellationToken ct)
{
    var movimientos = await _ctx.MovimientoAves
        .AsNoTracking()
        .Where(m => loteIds.Contains(m.LoteOrigenId ?? 0) &&
                   m.Estado == "Completado")
        .ToListAsync(ct);
    
    var resultado = new Dictionary<(int, DateTime), (int, int, int, int)>();
    
    foreach (var mov in movimientos)
    {
        if (!mov.LoteOrigenId.HasValue) continue;
        
        var key = (mov.LoteOrigenId.Value, mov.FechaMovimiento.Date);
        
        if (!resultado.ContainsKey(key))
        {
            resultado[key] = (0, 0, 0, 0);
        }
        
        var (vH, vM, tH, tM) = resultado[key];
        
        if (mov.TipoMovimiento == "Venta")
        {
            vH += mov.CantidadHembras;
            vM += mov.CantidadMachos;
        }
        else if (mov.TipoMovimiento == "Traslado")
        {
            tH += mov.CantidadHembras;
            tM += mov.CantidadMachos;
        }
        
        resultado[key] = (vH, vM, tH, tM);
    }
    
    return resultado;
}
```

---

## 📅 SEGMENTACIÓN POR SEMANA

### Cómo funciona actualmente:
1. Se calculan semanas contables desde `FechaPrimeraLlegada`
2. Cada semana = 7 días calendario
3. Los datos diarios se agrupan por semana

### Cómo agregar los nuevos datos:
1. **Mortalidad**: Agrupar `MortalidadHembras` y `MortalidadMachos` por semana
2. **Selección**: Agrupar `SelH` y `SelM` por semana
3. **Ventas/Traslados**: Filtrar `MovimientoAves` por `FechaMovimiento` dentro de la semana
4. **Balance**: Calcular acumulativamente semana por semana

---

## ✅ CHECKLIST DE IMPLEMENTACIÓN

- [ ] Actualizar `ConsumoDiarioContableDto` con campos de mortalidad, selección, ventas, traslados, saldos
- [ ] Actualizar `ReporteContableSemanalDto` con todos los nuevos campos
- [ ] Implementar `ObtenerEntradasInicialesAsync()` en `ReporteContableService`
- [ ] Implementar `ObtenerDatosLevanteAsync()` para mortalidad y selección
- [ ] Implementar `ObtenerDatosProduccionAsync()` para mortalidad y selección
- [ ] Implementar `ObtenerVentasYTrasladosAsync()` para ventas y traslados
- [ ] Implementar cálculo de saldos acumulativos semana por semana
- [ ] Actualizar método `ConsolidarSemanaContable()` para incluir todos los nuevos datos
- [ ] Actualizar frontend DTOs en TypeScript
- [ ] Actualizar componentes de visualización para mostrar los nuevos datos

---

## 📝 NOTAS IMPORTANTES

1. **Diferencia entre Levante y Producción:**
   - Levante: Usa `SeguimientoLoteLevante` y `Lote.HembrasL/MachosL`
   - Producción: Usa `SeguimientoProduccion` y `ProduccionLote.AvesInicialesH/M`

2. **Cálculo de Saldos:**
   - Debe ser acumulativo semana por semana
   - Primera semana: Saldo inicial = Entradas iniciales
   - Semanas siguientes: Saldo inicial = Saldo final semana anterior

3. **Ventas y Traslados:**
   - Solo considerar movimientos con `Estado = "Completado"`
   - Filtrar por `FechaMovimiento` dentro del rango de la semana

4. **Ingresos:**
   - Actualmente no hay tabla de ingresos
   - Se puede inferir de ventas, pero no hay montos
   - Considerar agregar campos de precio/monto si es necesario

---

## 🔗 REFERENCIAS

- **ReporteContableService**: `backend/src/ZooSanMarino.Infrastructure/Services/ReporteContableService.cs`
- **ReporteTecnicoProduccionService**: `backend/src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoProduccionService.cs` (ejemplo de cómo obtener ventas/traslados)
- **Entidades**: 
  - `SeguimientoLoteLevante`
  - `SeguimientoProduccion`
  - `MovimientoAves`
  - `Lote`
  - `ProduccionLote`











