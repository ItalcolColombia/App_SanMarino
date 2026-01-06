# 📊 ANÁLISIS COMPLETO: REPORTE CONTABLE SEMANAL

## 🎯 OBJETIVO DEL REPORTE

**Tipo de Reporte:** Informe Semanal de Levante Reproductoras (Código: FR-RP-10)

**Elaborado por:** Líder Técnico  
**Enviado a:** Contabilidad  
**Frecuencia:** Semanal  
**Alcance:** Un solo reporte por lote padre (consolida todos los sublotes)

**Regla de Semana Contable:**
- La semana contable inicia cuando se registra la llegada del primer lote o sublote
- Cada semana = 7 días calendario consecutivos
- Ejemplo: Si llegan el miércoles, la semana contable es miércoles a martes (7 días)

---

## 📋 ESTRUCTURA DEL REPORTE (Basado en Ejemplo Excel)

### 1. **ENCABEZADO DEL REPORTE**

```
INFORME SEMANAL DE LEVANTE REPRODUCTORAS
Granja: [Nombre Granja]
Lote: [Nombre Lote Padre]
Galpón: [Número Galpón]
Semana del: [Fecha Inicio] al [Fecha Fin]
Edad: [X.X semanas]
```

### 2. **SECCIÓN: AVES (Hembras y Machos)**

#### 2.1. Saldo Semana Anterior
- **Hembras:** Cantidad de hembras al final de la semana anterior
- **Machos:** Cantidad de machos al final de la semana anterior

#### 2.2. Entradas
- **Hembras:** Cantidad de hembras que ingresaron al lote (solo en la primera semana o si hay nuevas entradas)
- **Machos:** Cantidad de machos que ingresaron al lote (solo en la primera semana o si hay nuevas entradas)

#### 2.3. Mortalidad (Diaria y Acumulada)
- **Hembras:** Mortalidad diaria y total semanal
- **Machos:** Mortalidad diaria y total semanal
- **Semana Acumulado:** Suma total de mortalidad de la semana

#### 2.4. Selección (Diaria y Acumulada)
- **Hembras:** Selección diaria y total semanal
- **Machos:** Selección diaria y total semanal (solo en levante)
- **Semana Acumulado:** Suma total de selección de la semana

#### 2.5. Saldo Aves (Balance Diario)
- **Hembras:** Saldo diario de hembras (calculado)
- **Machos:** Saldo diario de machos (calculado)
- **Fórmula:** `Saldo Día N = Saldo Día N-1 - Mortalidad - Selección - Ventas - Traslados`

### 3. **SECCIÓN: BULTO (Bultos de Alimento)**

#### 3.1. Saldo Anterior
- Saldo de bultos al inicio de la semana

#### 3.2. Traslados (Salidas)
- Bultos trasladados a otras granjas/lotes
- Total semanal acumulado

#### 3.3. Entradas
- Bultos que ingresaron al lote/granja
- Total semanal acumulado

#### 3.4. Consumo
- **Hembra:** Consumo diario de bultos para hembras
- **Macho:** Consumo diario de bultos para machos
- **Semana Acumulado:** Total de consumo semanal

#### 3.5. Saldo
- Saldo actual de bultos al final de cada día
- **Fórmula:** `Saldo = Saldo Anterior + Entradas - Traslados - Consumo`

### 4. **SECCIÓN: PRODUCTO (Similar a BULTO)**

Misma estructura que BULTO pero para otros productos (medicamentos, vacunas, etc.)

---

## 🔍 ANÁLISIS DE DATOS REQUERIDOS

### ✅ DATOS QUE YA EXISTEN EN EL SISTEMA

1. **Información del Lote**
   - ✅ `LotePadreId`, `LotePadreNombre`
   - ✅ `GranjaId`, `GranjaNombre`
   - ✅ `NucleoId`, `NucleoNombre`
   - ✅ `GalponId` (si está disponible)
   - ✅ `FechaPrimeraLlegada`

2. **Consumo de Alimento (Kg)**
   - ✅ **Levante:** `SeguimientoLoteLevante.ConsumoKgHembras` + `ConsumoKgMachos`
   - ✅ **Producción:** `SeguimientoProduccion.ConsKgH` + `ConsKgM`
   - ⚠️ **FALTA:** Conversión de Kg a Bultos (necesita factor de conversión)

3. **Semanas Contables**
   - ✅ Cálculo de semanas desde `FechaPrimeraLlegada`
   - ✅ Filtrado por semana específica

---

### ❌ DATOS FALTANTES QUE SE NECESITAN

#### 1. **ENTRADAS INICIALES DE AVES**

**¿Qué se necesita?**
- Cantidad inicial de hembras al inicio del lote
- Cantidad inicial de machos al inicio del lote
- Entradas adicionales durante el período (si las hay)

**¿Dónde está la información?**

**Para Lotes en LEVANTE:**
- **Tabla:** `lotes`
- **Campos:** 
  - `HembrasL` (int?) - Cantidad inicial de hembras
  - `MachosL` (int?) - Cantidad inicial de machos
- **Entidad:** `Lote.HembrasL`, `Lote.MachosL`

**Para Lotes en PRODUCCIÓN:**
- **Tabla:** `produccion_lotes`
- **Campos:**
  - `aves_iniciales_h` (int) - Cantidad inicial de hembras
  - `aves_iniciales_m` (int) - Cantidad inicial de machos
- **Entidad:** `ProduccionLote.AvesInicialesH`, `ProduccionLote.AvesInicialesM`

**Implementación:**
```csharp
// Obtener entradas iniciales por lote
// Si tiene ProduccionLote -> usar avesInicialesH/M
// Si no -> usar Lote.HembrasL/MachosL
```

---

#### 2. **SALDO SEMANA ANTERIOR**

**¿Qué se necesita?**
- Saldo final de hembras de la semana anterior
- Saldo final de machos de la semana anterior

**¿Cómo se calcula?**
- Para la primera semana: Saldo inicial = Entradas iniciales
- Para semanas siguientes: Saldo inicial = Saldo final semana anterior
- **Fórmula:** `Saldo Final Semana N = Saldo Inicial - Mortalidad - Selección - Ventas - Traslados`

**Implementación:**
```csharp
// Calcular saldo acumulativo semana por semana
// Primera semana: usar entradas iniciales
// Semanas siguientes: usar saldo final de semana anterior
```

---

#### 3. **MORTALIDAD**

**¿Qué se necesita?**
- Mortalidad diaria de hembras y machos
- Mortalidad total semanal (acumulada)
- Identificar semanas con mayor mortalidad

**¿Dónde está la información?**

**Para Lotes en LEVANTE:**
- **Tabla:** `seguimiento_lote_levante`
- **Campos:**
  - `MortalidadHembras` (int) - Mortalidad diaria de hembras
  - `MortalidadMachos` (int) - Mortalidad diaria de machos
- **Entidad:** `SeguimientoLoteLevante.MortalidadHembras`, `SeguimientoLoteLevante.MortalidadMachos`

**Para Lotes en PRODUCCIÓN:**
- **Tabla:** `produccion_diaria` (SeguimientoProduccion)
- **Campos:**
  - `MortalidadH` (int) - Mortalidad diaria de hembras
  - `MortalidadM` (int) - Mortalidad diaria de machos
- **Entidad:** `SeguimientoProduccion.MortalidadH`, `SeguimientoProduccion.MortalidadM`

**Implementación:**
```csharp
// Agrupar por semana contable y sumar mortalidades diarias
// Incluir en ReporteContableSemanalDto:
// - MortalidadHembrasSemanal
// - MortalidadMachosSemanal
// - MortalidadTotalSemanal
```

---

#### 4. **SELECCIÓN**

**¿Qué se necesita?**
- Selección diaria de hembras y machos
- Selección total semanal (acumulada)

**¿Dónde está la información?**

**Para Lotes en LEVANTE:**
- **Tabla:** `seguimiento_lote_levante`
- **Campos:**
  - `SelH` (int) - Selección de hembras diaria
  - `SelM` (int) - Selección de machos diaria
- **Entidad:** `SeguimientoLoteLevante.SelH`, `SeguimientoLoteLevante.SelM`

**Para Lotes en PRODUCCIÓN:**
- **Tabla:** `produccion_diaria` (SeguimientoProduccion)
- **Campos:**
  - `SelH` (int) - Selección de hembras diaria
  - **NOTA:** En producción típicamente NO hay selección de machos
- **Entidad:** `SeguimientoProduccion.SelH`

**Implementación:**
```csharp
// Agrupar por semana contable y sumar selecciones diarias
// Incluir en ReporteContableSemanalDto:
// - SeleccionHembrasSemanal
// - SeleccionMachosSemanal (solo para levante)
```

---

#### 5. **VENTAS Y TRASLADOS**

**¿Qué se necesita?**
- Ventas de hembras y machos por semana
- Traslados de hembras y machos por semana
- Total de aves vendidas/trasladadas

**¿Dónde está la información?**

- **Tabla:** `movimiento_aves`
- **Campos:**
  - `TipoMovimiento` (string) - "Venta" o "Traslado"
  - `CantidadHembras` (int) - Cantidad de hembras
  - `CantidadMachos` (int) - Cantidad de machos
  - `FechaMovimiento` (DateTime) - Fecha del movimiento
  - `LoteOrigenId` (int) - ID del lote origen
  - `Estado` (string) - Solo considerar "Completado"
- **Entidad:** `MovimientoAves`

**Implementación:**
```csharp
// Filtrar por semana contable y tipo de movimiento
// Incluir en ReporteContableSemanalDto:
// - VentasHembrasSemanal
// - VentasMachosSemanal
// - TrasladosHembrasSemanal
// - TrasladosMachosSemanal
```

---

#### 6. **BULTO (Bultos de Alimento)**

**¿Qué se necesita?**
- Saldo anterior de bultos
- Traslados de bultos (salidas)
- Entradas de bultos
- Consumo de bultos (hembras y machos)
- Saldo actual de bultos

**¿Dónde está la información?**

**Consumo (Kg):**
- ✅ Ya existe en `SeguimientoLoteLevante` y `SeguimientoProduccion`
- ⚠️ **FALTA:** Factor de conversión Kg → Bultos
- ⚠️ **FALTA:** Entradas de bultos al lote/granja

**Entradas y Traslados de Bultos:**
- **Tabla:** `farm_inventory_movements`
- **Campos:**
  - `MovementType` (string) - "Entry" (entrada), "TransferOut" (traslado salida), "TransferIn" (traslado entrada)
  - `Quantity` (decimal) - Cantidad en bultos
  - `CatalogItemId` (int) - ID del producto (alimento)
  - `FarmId` (int) - ID de la granja
  - `CreatedAt` (DateTimeOffset) - Fecha del movimiento
- **Entidad:** `FarmInventoryMovement`

**Implementación:**
```csharp
// 1. Obtener entradas de bultos desde farm_inventory_movements
//    WHERE MovementType = 'Entry' AND CatalogItemId = [ID_ALIMENTO]
// 2. Obtener traslados desde farm_inventory_movements
//    WHERE MovementType IN ('TransferOut', 'TransferIn')
// 3. Convertir consumo Kg a bultos usando factor de conversión
// 4. Calcular saldo: Saldo Anterior + Entradas - Traslados - Consumo
```

**⚠️ NOTA IMPORTANTE:**
- Necesita identificar qué `CatalogItemId` corresponde al alimento
- Necesita factor de conversión: 1 bulto = X kg (típicamente 40-50 kg)
- Puede necesitar filtrar por lote específico si el inventario está a nivel de granja

---

#### 7. **PRODUCTO (Otros Productos)**

Similar a BULTO pero para otros productos (medicamentos, vacunas, etc.)

**Implementación:**
```csharp
// Similar a BULTO pero filtrar por CatalogItemId de otros productos
// Puede ser un array de productos o un producto específico
```

---

## 🔄 FLUJO COMPLETO DEL SISTEMA

### BACKEND FLOW

```
1. Usuario selecciona Lote Padre y Semana Contable
   ↓
2. Frontend: ReporteContableMainComponent
   - Valida que sea lote padre
   - Llama a reporteContableService.generarReporte()
   ↓
3. Backend: ReporteContableController.GenerarReporte()
   - Recibe: lotePadreId, semanaContable (opcional)
   ↓
4. Backend: ReporteContableService.GenerarReporteAsync()
   
   a) Validar y obtener lote padre
      - Verificar que existe y es lote padre (LotePadreId == null)
      - Obtener información: Granja, Núcleo, FechaEncaset
   
   b) Obtener sublotes
      - WHERE LotePadreId == lotePadreId
      - Incluir lote padre en la lista para consolidación
   
   c) Calcular semanas contables
      - FechaPrimeraLlegada = MIN(FechaEncaset de todos los lotes)
      - CalcularSemanasContables(fechaPrimeraLlegada, hoy)
      - Cada semana = 7 días calendario
   
   d) Obtener datos diarios (NUEVO - debe implementarse)
      - ObtenerEntradasInicialesAsync() → Entradas iniciales por lote
      - ObtenerDatosLevanteAsync() → Mortalidad, Selección, Consumo (levante)
      - ObtenerDatosProduccionAsync() → Mortalidad, Selección, Consumo (producción)
      - ObtenerVentasYTrasladosAsync() → Ventas y traslados de aves
      - ObtenerEntradasBultosAsync() → Entradas de bultos
      - ObtenerTrasladosBultosAsync() → Traslados de bultos
      - ObtenerConsumoBultosAsync() → Consumo convertido a bultos
   
   e) Calcular saldos acumulativos
      - Primera semana: Saldo inicial = Entradas iniciales
      - Semanas siguientes: Saldo inicial = Saldo final semana anterior
      - Saldo diario: Saldo anterior - Mortalidad - Selección - Ventas - Traslados
      - Saldo bultos: Saldo anterior + Entradas - Traslados - Consumo
   
   f) Consolidar por semana
      - Agrupar datos diarios por semana contable
      - Sumar totales semanales
      - Crear ReporteContableSemanalDto para cada semana
   
   g) Retornar ReporteContableCompletoDto
      - Información del lote padre
      - Lista de ReporteContableSemanalDto
      ↓
5. Frontend recibe ReporteContableCompletoDto
   ↓
6. Frontend: TablaResumenSemanalContableComponent
   - Muestra resumen semanal consolidado
   ↓
7. Frontend: TablaDetalleDiarioContableComponent
   - Muestra detalle diario por semana
```

---

### FRONTEND FLOW

```
1. Usuario accede a /reporte-contable
   ↓
2. ReporteContableMainComponent.ngOnInit()
   - Carga granjas disponibles
   ↓
3. Usuario selecciona filtros (Granja → Núcleo → Galpón → Lote)
   - onGranjaChange() → Carga núcleos
   - onNucleoChange() → Filtra lotes
   - onGalponChange() → Filtra lotes
   - onLoteChange() → Valida que sea lote padre, carga semanas contables
   ↓
4. Usuario selecciona semana contable (opcional)
   - Si no selecciona, muestra todas las semanas
   ↓
5. Usuario hace clic en "Generar Reporte"
   - generarReporte()
   - Valida filtros
   - Llama a reporteContableService.generarReporte()
   - Muestra loading
   ↓
6. Recibe ReporteContableCompletoDto
   - reporte.set(reporte)
   - Oculta loading
   ↓
7. Renderiza reporte
   - TablaResumenSemanalContableComponent: Resumen semanal
   - TablaDetalleDiarioContableComponent: Detalle diario por semana
   ↓
8. Usuario puede exportar a Excel
   - exportarExcel()
   - Llama a reporteContableService.exportarExcel()
   - Descarga archivo Excel
```

---

## 📊 ESTRUCTURA DE DATOS PROPUESTA

### DTOs Actualizados

```csharp
// DatoDiarioContableDto - Datos diarios completos
public record DatoDiarioContableDto
{
    public DateTime Fecha { get; init; }
    public int LoteId { get; init; }
    public string LoteNombre { get; init; } = string.Empty;
    
    // AVES
    public int EntradasHembras { get; init; }
    public int EntradasMachos { get; init; }
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
    
    // CONSUMO (Kg)
    public decimal ConsumoAlimentoHembras { get; init; }
    public decimal ConsumoAlimentoMachos { get; init; }
    public decimal ConsumoAgua { get; init; }
    public decimal ConsumoMedicamento { get; init; }
    public decimal ConsumoVacuna { get; init; }
    
    // BULTO
    public decimal SaldoBultosAnterior { get; init; }
    public decimal TrasladosBultos { get; init; }
    public decimal EntradasBultos { get; init; }
    public decimal ConsumoBultosHembras { get; init; }
    public decimal ConsumoBultosMachos { get; init; }
    public decimal SaldoBultos { get; init; }
}

// ReporteContableSemanalDto - Resumen semanal
public record ReporteContableSemanalDto
{
    // Información de semana
    public int SemanaContable { get; init; }
    public DateTime FechaInicio { get; init; }
    public DateTime FechaFin { get; init; }
    public int LotePadreId { get; init; }
    public string LotePadreNombre { get; init; } = string.Empty;
    public List<string> Sublotes { get; init; } = new();
    
    // AVES - Saldo Semana Anterior
    public int SaldoAnteriorHembras { get; init; }
    public int SaldoAnteriorMachos { get; init; }
    
    // AVES - Entradas
    public int EntradasHembras { get; init; }
    public int EntradasMachos { get; init; }
    public int TotalEntradas { get; init; }
    
    // AVES - Mortalidad
    public int MortalidadHembrasSemanal { get; init; }
    public int MortalidadMachosSemanal { get; init; }
    public int MortalidadTotalSemanal { get; init; }
    
    // AVES - Selección
    public int SeleccionHembrasSemanal { get; init; }
    public int SeleccionMachosSemanal { get; init; }
    public int TotalSeleccionSemanal { get; init; }
    
    // AVES - Ventas y Traslados
    public int VentasHembrasSemanal { get; init; }
    public int VentasMachosSemanal { get; init; }
    public int TrasladosHembrasSemanal { get; init; }
    public int TrasladosMachosSemanal { get; init; }
    public int TotalVentasSemanal { get; init; }
    public int TotalTrasladosSemanal { get; init; }
    
    // AVES - Saldo Final
    public int SaldoFinHembras { get; init; }
    public int SaldoFinMachos { get; init; }
    public int TotalAvesVivas { get; init; }
    
    // BULTO - Resumen Semanal
    public decimal SaldoBultosAnterior { get; init; }
    public decimal TrasladosBultosSemanal { get; init; }
    public decimal EntradasBultosSemanal { get; init; }
    public decimal ConsumoBultosHembrasSemanal { get; init; }
    public decimal ConsumoBultosMachosSemanal { get; init; }
    public decimal SaldoBultosFinal { get; init; }
    
    // CONSUMO (Kg) - Resumen Semanal
    public decimal ConsumoTotalAlimento { get; init; }
    public decimal ConsumoTotalAgua { get; init; }
    public decimal ConsumoTotalMedicamento { get; init; }
    public decimal ConsumoTotalVacuna { get; init; }
    
    // Detalle diario
    public List<DatoDiarioContableDto> DatosDiarios { get; init; } = new();
}

// ReporteContableCompletoDto - Reporte completo
public record ReporteContableCompletoDto
{
    public int LotePadreId { get; init; }
    public string LotePadreNombre { get; init; } = string.Empty;
    public int GranjaId { get; init; }
    public string GranjaNombre { get; init; } = string.Empty;
    public string? NucleoId { get; init; }
    public string? NucleoNombre { get; init; }
    public string? GalponId { get; init; }
    public string? GalponNombre { get; init; }
    public DateTime FechaPrimeraLlegada { get; init; }
    public int SemanaContableActual { get; init; }
    public DateTime FechaInicioSemanaActual { get; init; }
    public DateTime FechaFinSemanaActual { get; init; }
    public List<ReporteContableSemanalDto> ReportesSemanales { get; init; } = new();
}
```

---

## 🔧 IMPLEMENTACIÓN DETALLADA

### BACKEND: ReporteContableService.cs

#### Método Principal: GenerarReporteAsync()

```csharp
public async Task<ReporteContableCompletoDto> GenerarReporteAsync(
    GenerarReporteContableRequestDto request,
    CancellationToken ct = default)
{
    // 1. Validar y obtener lote padre
    var lotePadre = await _ctx.Lotes
        .AsNoTracking()
        .Include(l => l.Farm)
        .Include(l => l.Nucleo)
        .FirstOrDefaultAsync(l => l.LoteId == request.LotePadreId && 
                                 l.CompanyId == _currentUser.CompanyId &&
                                 l.DeletedAt == null &&
                                 l.LotePadreId == null, ct);

    if (lotePadre == null)
        throw new InvalidOperationException($"Lote padre con ID {request.LotePadreId} no encontrado");

    // 2. Obtener sublotes
    var sublotes = await _ctx.Lotes
        .AsNoTracking()
        .Where(l => l.LotePadreId == request.LotePadreId &&
                   l.CompanyId == _currentUser.CompanyId &&
                   l.DeletedAt == null)
        .ToListAsync(ct);

    var todosLotes = new List<Lote> { lotePadre };
    todosLotes.AddRange(sublotes);

    // 3. Calcular fecha primera llegada
    var fechaPrimeraLlegada = todosLotes
        .Where(l => l.FechaEncaset.HasValue)
        .Select(l => l.FechaEncaset!.Value)
        .DefaultIfEmpty(DateTime.Today)
        .Min();

    // 4. Calcular semanas contables
    var semanasContables = CalcularSemanasContables(fechaPrimeraLlegada, DateTime.Today);
    var semanasAFiltrar = request.SemanaContable.HasValue
        ? semanasContables.Where(s => s.Semana == request.SemanaContable.Value).ToList()
        : semanasContables;

    // 5. Obtener entradas iniciales
    var entradasIniciales = await ObtenerEntradasInicialesAsync(todosLotes, ct);

    // 6. Obtener datos diarios completos
    var datosDiarios = await ObtenerDatosDiariosCompletosAsync(todosLotes, entradasIniciales, ct);

    // 7. Calcular saldos acumulativos
    var datosConSaldos = CalcularSaldosAcumulativos(datosDiarios, entradasIniciales, semanasContables);

    // 8. Consolidar por semana
    var reportesSemanales = semanasAFiltrar.Select(semana => 
    {
        var datosSemana = datosConSaldos
            .Where(d => d.Fecha >= semana.FechaInicio && d.Fecha <= semana.FechaFin)
            .ToList();

        return ConsolidarSemanaContable(
            semana.Semana,
            semana.FechaInicio,
            semana.FechaFin,
            request.LotePadreId,
            lotePadre.LoteNombre ?? string.Empty,
            sublotes.Select(s => s.LoteNombre ?? string.Empty).ToList(),
            datosSemana,
            semanasContables
        );
    }).ToList();

    // 9. Obtener semana contable actual
    var semanaActual = semanasContables
        .Where(s => s.FechaInicio <= DateTime.Today && s.FechaFin >= DateTime.Today)
        .FirstOrDefault();

    var semanaActualFinal = semanaActual.Semana == 0 
        ? semanasContables.FirstOrDefault() 
        : semanaActual;

    return new ReporteContableCompletoDto
    {
        LotePadreId = lotePadre.LoteId ?? 0,
        LotePadreNombre = lotePadre.LoteNombre ?? string.Empty,
        GranjaId = lotePadre.GranjaId,
        GranjaNombre = lotePadre.Farm?.Name ?? string.Empty,
        NucleoId = lotePadre.NucleoId,
        NucleoNombre = lotePadre.Nucleo?.NucleoNombre,
        GalponId = lotePadre.GalponId,
        FechaPrimeraLlegada = fechaPrimeraLlegada,
        SemanaContableActual = semanaActualFinal.Semana,
        FechaInicioSemanaActual = semanaActualFinal.FechaInicio,
        FechaFinSemanaActual = semanaActualFinal.FechaFin,
        ReportesSemanales = reportesSemanales
    };
}
```

#### Método: ObtenerEntradasInicialesAsync()

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

#### Método: ObtenerDatosDiariosCompletosAsync()

```csharp
private async Task<List<DatoDiarioContableDto>> ObtenerDatosDiariosCompletosAsync(
    List<Lote> lotes,
    Dictionary<int, (int hembras, int machos)> entradasIniciales,
    CancellationToken ct)
{
    var datosDiarios = new List<DatoDiarioContableDto>();
    var loteIds = lotes.Where(l => l.LoteId.HasValue).Select(l => l.LoteId!.Value).ToList();
    var loteIdsString = loteIds.Select(id => id.ToString()).ToList();

    // Obtener datos de levante
    var datosLevante = await _ctx.SeguimientoLoteLevante
        .AsNoTracking()
        .Where(s => loteIds.Contains(s.LoteId))
        .ToListAsync(ct);

    // Obtener datos de producción
    var datosProduccion = await _ctx.SeguimientoProduccion
        .AsNoTracking()
        .Where(s => loteIdsString.Contains(s.LoteId))
        .ToListAsync(ct);

    // Obtener ventas y traslados
    var ventasTraslados = await ObtenerVentasYTrasladosAsync(loteIds, ct);

    // Obtener datos de bultos
    var datosBultos = await ObtenerDatosBultosAsync(loteIds, lotes.First().GranjaId, ct);

    // Consolidar datos diarios
    var todasLasFechas = datosLevante.Select(d => d.FechaRegistro.Date)
        .Union(datosProduccion.Select(d => d.Fecha.Date))
        .Union(ventasTraslados.Select(v => v.Key.fecha))
        .Distinct()
        .OrderBy(f => f)
        .ToList();

    foreach (var fecha in todasLasFechas)
    {
        foreach (var lote in lotes)
        {
            if (!lote.LoteId.HasValue) continue;

            var loteId = lote.LoteId.Value;
            var loteIdStr = loteId.ToString();

            // Datos de levante
            var levante = datosLevante
                .FirstOrDefault(d => d.LoteId == loteId && d.FechaRegistro.Date == fecha);

            // Datos de producción
            var produccion = datosProduccion
                .FirstOrDefault(d => d.LoteId == loteIdStr && d.Fecha.Date == fecha);

            // Ventas y traslados
            var (ventasH, ventasM, trasladosH, trasladosM) = ventasTraslados
                .TryGetValue((loteId, fecha), out var vt) ? vt : (0, 0, 0, 0);

            // Datos de bultos
            var bultos = datosBultos
                .FirstOrDefault(d => d.Fecha == fecha);

            var dato = new DatoDiarioContableDto
            {
                Fecha = fecha,
                LoteId = loteId,
                LoteNombre = lote.LoteNombre ?? string.Empty,
                
                // AVES
                EntradasHembras = fecha == entradasIniciales[loteId].hembras ? entradasIniciales[loteId].hembras : 0,
                EntradasMachos = fecha == entradasIniciales[loteId].machos ? entradasIniciales[loteId].machos : 0,
                MortalidadHembras = levante?.MortalidadHembras ?? produccion?.MortalidadH ?? 0,
                MortalidadMachos = levante?.MortalidadMachos ?? produccion?.MortalidadM ?? 0,
                SeleccionHembras = levante?.SelH ?? produccion?.SelH ?? 0,
                SeleccionMachos = levante?.SelM ?? 0,
                VentasHembras = ventasH,
                VentasMachos = ventasM,
                TrasladosHembras = trasladosH,
                TrasladosMachos = trasladosM,
                
                // CONSUMO (Kg)
                ConsumoAlimentoHembras = (decimal)(levante?.ConsumoKgHembras ?? produccion?.ConsKgH ?? 0),
                ConsumoAlimentoMachos = (decimal)(levante?.ConsumoKgMachos ?? produccion?.ConsKgM ?? 0),
                
                // BULTO (se calculará después con saldos)
                SaldoBultosAnterior = bultos?.SaldoAnterior ?? 0,
                TrasladosBultos = bultos?.Traslados ?? 0,
                EntradasBultos = bultos?.Entradas ?? 0,
                ConsumoBultosHembras = bultos?.ConsumoHembras ?? 0,
                ConsumoBultosMachos = bultos?.ConsumoMachos ?? 0,
            };

            datosDiarios.Add(dato);
        }
    }

    return datosDiarios.OrderBy(d => d.Fecha).ToList();
}
```

#### Método: CalcularSaldosAcumulativos()

```csharp
private List<DatoDiarioContableDto> CalcularSaldosAcumulativos(
    List<DatoDiarioContableDto> datosDiarios,
    Dictionary<int, (int hembras, int machos)> entradasIniciales,
    List<(int Semana, DateTime FechaInicio, DateTime FechaFin)> semanasContables)
{
    var datosConSaldos = new List<DatoDiarioContableDto>();
    var saldosPorLote = new Dictionary<int, (int hembras, int machos)>();
    var saldoBultos = 0m;

    // Inicializar saldos con entradas iniciales
    foreach (var (loteId, (hembras, machos)) in entradasIniciales)
    {
        saldosPorLote[loteId] = (hembras, machos);
    }

    foreach (var dato in datosDiarios.OrderBy(d => d.Fecha))
    {
        var loteId = dato.LoteId;
        
        // Obtener saldo anterior
        var (saldoHAnterior, saldoMAnterior) = saldosPorLote.GetValueOrDefault(loteId, (0, 0));

        // Calcular saldo actual de aves
        var saldoHActual = saldoHAnterior 
            + dato.EntradasHembras
            - dato.MortalidadHembras
            - dato.SeleccionHembras
            - dato.VentasHembras
            - dato.TrasladosHembras;

        var saldoMActual = saldoMAnterior
            + dato.EntradasMachos
            - dato.MortalidadMachos
            - dato.SeleccionMachos
            - dato.VentasMachos
            - dato.TrasladosMachos;

        // Actualizar saldos
        saldosPorLote[loteId] = (Math.Max(0, saldoHActual), Math.Max(0, saldoMActual));

        // Calcular saldo de bultos
        saldoBultos = saldoBultos
            + dato.EntradasBultos
            - dato.TrasladosBultos
            - dato.ConsumoBultosHembras
            - dato.ConsumoBultosMachos;

        var datoConSaldo = dato with
        {
            SaldoHembras = Math.Max(0, saldoHActual),
            SaldoMachos = Math.Max(0, saldoMActual),
            SaldoBultos = Math.Max(0, saldoBultos)
        };

        datosConSaldos.Add(datoConSaldo);
    }

    return datosConSaldos;
}
```

---

## ✅ CHECKLIST DE IMPLEMENTACIÓN

### BACKEND

- [ ] Actualizar `DatoDiarioContableDto` con todos los campos requeridos
- [ ] Actualizar `ReporteContableSemanalDto` con todos los campos requeridos
- [ ] Implementar `ObtenerEntradasInicialesAsync()` en `ReporteContableService`
- [ ] Implementar `ObtenerDatosDiariosCompletosAsync()` para consolidar datos
- [ ] Implementar `ObtenerDatosLevanteAsync()` para mortalidad y selección (levante)
- [ ] Implementar `ObtenerDatosProduccionAsync()` para mortalidad y selección (producción)
- [ ] Implementar `ObtenerVentasYTrasladosAsync()` para ventas y traslados de aves
- [ ] Implementar `ObtenerDatosBultosAsync()` para entradas, traslados y consumo de bultos
- [ ] Implementar `CalcularSaldosAcumulativos()` para calcular saldos semana por semana
- [ ] Actualizar `ConsolidarSemanaContable()` para incluir todos los nuevos datos
- [ ] Agregar factor de conversión Kg → Bultos (configurable)
- [ ] Identificar CatalogItemId del alimento para filtrar movimientos de inventario

### FRONTEND

- [ ] Actualizar interfaces TypeScript de DTOs
- [ ] Actualizar `TablaResumenSemanalContableComponent` para mostrar todos los datos
- [ ] Actualizar `TablaDetalleDiarioContableComponent` para mostrar detalle diario completo
- [ ] Agregar sección de BULTO en el reporte
- [ ] Agregar sección de PRODUCTO en el reporte (si aplica)
- [ ] Actualizar exportación a Excel para incluir todos los campos

---

## 📝 NOTAS IMPORTANTES

1. **Diferencia entre Levante y Producción:**
   - Levante: Usa `SeguimientoLoteLevante` y `Lote.HembrasL/MachosL`
   - Producción: Usa `SeguimientoProduccion` y `ProduccionLote.AvesInicialesH/M`

2. **Cálculo de Saldos:**
   - Debe ser acumulativo semana por semana
   - Primera semana: Saldo inicial = Entradas iniciales
   - Semanas siguientes: Saldo inicial = Saldo final semana anterior
   - Saldo diario: Saldo anterior - Mortalidad - Selección - Ventas - Traslados

3. **Bultos:**
   - Necesita factor de conversión: 1 bulto = X kg (configurable)
   - Entradas y traslados vienen de `FarmInventoryMovement`
   - Consumo se calcula desde consumo diario (Kg) convertido a bultos

4. **Ventas y Traslados:**
   - Solo considerar movimientos con `Estado = "Completado"`
   - Filtrar por `FechaMovimiento` dentro del rango de la semana

5. **Semana Contable:**
   - Inicia cuando llega el primer lote/sublote
   - Dura 7 días calendario consecutivos
   - Ejemplo: Si llegan miércoles, semana = miércoles a martes

---

## 🔗 REFERENCIAS

- **ReporteContableService**: `backend/src/ZooSanMarino.Infrastructure/Services/ReporteContableService.cs`
- **ReporteContableController**: `backend/src/ZooSanMarino.API/Controllers/ReporteContableController.cs`
- **ReporteTecnicoProduccionService**: `backend/src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoProduccionService.cs` (ejemplo de cómo obtener ventas/traslados)
- **FarmInventoryMovementService**: `backend/src/ZooSanMarino.Infrastructure/Services/FarmInventoryMovementService.cs` (para bultos)
- **Entidades**: 
  - `SeguimientoLoteLevante`
  - `SeguimientoProduccion`
  - `MovimientoAves`
  - `Lote`
  - `ProduccionLote`
  - `FarmInventoryMovement`











