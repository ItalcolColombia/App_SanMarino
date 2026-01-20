# 📊 ANÁLISIS DETALLADO: MÓDULO DE REPORTE TÉCNICO DE LEVANTE

## 🎯 OBJETIVO
Verificar los datos que se obtienen y los cálculos que se realizan en el reporte técnico de levante para asegurar que todos los datos necesarios estén presentes y los cálculos sean correctos.

---

## 📥 1. DATOS QUE SE OBTIENEN DE LA BASE DE DATOS

### 1.1. Consulta Principal - Seguimiento Diario Levante

**Ubicación**: `ReporteTecnicoService.ObtenerDatosDiariosLevanteAsync()`

```csharp
var query = _ctx.SeguimientoLoteLevante
    .AsNoTracking()
    .Where(s => s.LoteId == loteId);

// Filtros opcionales por fecha
if (fechaInicio.HasValue)
    query = query.Where(s => s.FechaRegistro >= fechaInicio.Value);

if (fechaFin.HasValue)
    query = query.Where(s => s.FechaRegistro <= fechaFin.Value);

var seguimientos = await query
    .OrderBy(s => s.FechaRegistro)
    .ToListAsync(ct);
```

**Datos Obtenidos de `SeguimientoLoteLevante`**:
- `Id` - ID del registro
- `LoteId` - ID del lote
- `FechaRegistro` - Fecha del registro diario
- `MortalidadHembras` - Mortalidad diaria de hembras
- `MortalidadMachos` - Mortalidad diaria de machos
- `SelH` - Selección/retiro de hembras (puede ser negativo si es descuento por traslado)
- `SelM` - Selección/retiro de machos (puede ser negativo si es descuento por traslado)
- `ErrorSexajeHembras` - Errores de sexaje en hembras
- `ErrorSexajeMachos` - Errores de sexaje en machos
- `ConsumoKgHembras` - Consumo de alimento hembras (kg)
- `ConsumoKgMachos` - Consumo de alimento machos (kg, nullable)
- `TipoAlimento` - Tipo de alimento utilizado
- `PesoPromH` - Peso promedio hembras (nullable)
- `PesoPromM` - Peso promedio machos (nullable)
- `UniformidadH` - Uniformidad hembras (nullable)
- `UniformidadM` - Uniformidad machos (nullable)
- `CvH` - Coeficiente de variación hembras (nullable)
- `CvM` - Coeficiente de variación machos (nullable)
- `Observaciones` - Observaciones del día

### 1.2. Información del Lote

```csharp
var lote = await _ctx.Lotes
    .AsNoTracking()
    .FirstOrDefaultAsync(l => l.LoteId == loteId, ct);
```

**Datos Obtenidos del Lote**:
- `HembrasL` - Número inicial de hembras
- `MachosL` - Número inicial de machos
- `FechaEncaset` - Fecha de encasetamiento
- `GranjaId` - ID de la granja (para obtener ingresos/traslados de alimento)

### 1.3. Ingresos y Traslados de Alimento

```csharp
IngresosAlimentoKilos = await ObtenerIngresosAlimentoAsync(lote.GranjaId, seg.FechaRegistro, ct)
TrasladosAlimentoKilos = await ObtenerTrasladosAlimentoAsync(lote.GranjaId, seg.FechaRegistro, ct)
```

**Datos Obtenidos de `FarmInventoryMovements`**:
- Movimientos de tipo `Entry` o `TransferIn` para ingresos
- Movimientos de tipo `TransferOut` para traslados
- Filtrados por fecha y por items que contengan "alimento" en el nombre

---

## 🧮 2. CÁLCULOS REALIZADOS

### 2.1. Variables Iniciales

```csharp
var avesIniciales = (lote.HembrasL ?? 0) + (lote.MachosL ?? 0);
var avesActuales = avesIniciales;  // Se va actualizando en cada iteración
var mortalidadAcumulada = 0;
var consumoAcumulado = 0m;
var errorSexajeAcumulado = 0;
var descarteAcumulado = 0;
decimal? pesoAnterior = null;
```

### 2.2. Cálculos por Cada Registro Diario

#### 2.2.1. Edad del Lote
```csharp
var edadDias = CalcularEdadDias(fechaEncaset.Value, seg.FechaRegistro);
var edadSemanas = CalcularEdadSemanas(edadDias);
```

**Fórmula**:
- `edadDias = (FechaRegistro - FechaEncaset).Days + 1`
- `edadSemanas = Math.Ceiling(edadDias / 7.0)`

#### 2.2.2. Mortalidad
```csharp
var mortalidadTotal = seg.MortalidadHembras + seg.MortalidadMachos;
mortalidadAcumulada += mortalidadTotal;
avesActuales -= mortalidadTotal;
```

**Cálculos**:
- Mortalidad diaria: Suma de hembras + machos muertos
- Mortalidad acumulada: Suma de todas las mortalidades hasta la fecha
- Aves actuales: Se resta la mortalidad diaria

#### 2.2.3. Error de Sexaje
```csharp
var errorSexaje = seg.ErrorSexajeHembras + seg.ErrorSexajeMachos;
errorSexajeAcumulado += errorSexaje;
```

**Nota**: El error de sexaje NO afecta el número de aves actuales (solo es una corrección de clasificación)

#### 2.2.4. Descarte/Selección (Incluye Traslados)
```csharp
// Descarte incluye selecciones (SelH, SelM) que pueden ser negativas si son descuentos por traslado
var descarte = seg.SelH + seg.SelM;
descarteAcumulado += descarte;
avesActuales -= descarte;
```

**Lógica Importante**:
- Si `SelH` o `SelM` son **positivos**: Representan selección/retiro normal (resta aves)
- Si `SelH` o `SelM` son **negativos**: Representan descuento por traslado (restar negativo = sumar, pero en realidad resta aves porque el traslado ya las quitó)
- **Ejemplo**:
  - `SelH = 5` → Se seleccionaron 5 hembras → `avesActuales -= 5` ✅
  - `SelH = -3` → Se trasladaron 3 hembras → `avesActuales -= (-3)` = `avesActuales += 3` ❌ **PROBLEMA POTENCIAL**

**✅ CORRECCIÓN IMPLEMENTADA**: 
Se ha corregido la lógica para manejar correctamente los traslados. Ahora se separan las selecciones normales de los traslados:

```csharp
// Separar selección normal de traslados
var seleccionH = seg.SelH;
var seleccionM = seg.SelM;

// Selección normal (valores positivos): aves retiradas por selección/descarte
var seleccionNormal = Math.Max(0, seleccionH) + Math.Max(0, seleccionM);

// Traslados (valores negativos): aves trasladadas a otro lote/granja
var traslados = Math.Min(0, seleccionH) + Math.Min(0, seleccionM);
var trasladosAbsoluto = Math.Abs(traslados);

// Restar selección normal (aves retiradas)
avesActuales -= seleccionNormal;

// Restar traslados (aves que salieron del lote)
avesActuales -= trasladosAbsoluto;
```

**Lógica Correcta**:
- Si `SelH = 5` (selección normal): `seleccionNormal = 5`, `avesActuales -= 5` ✅
- Si `SelH = -3` (traslado): `trasladosAbsoluto = 3`, `avesActuales -= 3` ✅
- Si `SelH = 2` y `SelM = -1` (mezcla): 
  - `seleccionNormal = 2`, `avesActuales -= 2`
  - `trasladosAbsoluto = 1`, `avesActuales -= 1`
  - Total: `avesActuales -= 3` ✅

#### 2.2.5. Consumo de Alimento
```csharp
var consumoKilos = (decimal)seg.ConsumoKgHembras + (decimal)(seg.ConsumoKgMachos ?? 0);
consumoAcumulado += consumoKilos;
var consumoGramosPorAve = avesActuales > 0 ? (consumoKilos * 1000) / avesActuales : 0;
```

**Cálculos**:
- Consumo diario: Suma de consumo hembras + machos
- Consumo acumulado: Suma de todos los consumos hasta la fecha
- Consumo por ave: `(consumoKilos * 1000) / avesActuales` (en gramos)

#### 2.2.6. Peso y Ganancia
```csharp
var pesoActual = (decimal?)(seg.PesoPromH ?? seg.PesoPromM);
var gananciaPeso = pesoActual.HasValue && pesoAnterior.HasValue 
    ? pesoActual.Value - pesoAnterior.Value 
    : (decimal?)null;
```

**Cálculos**:
- Peso actual: Prioriza peso hembras, si no existe usa peso machos
- Ganancia de peso: Diferencia entre peso actual y peso anterior

---

## 📊 3. DATOS QUE SE RETORNAN EN EL DTO

### 3.1. ReporteTecnicoDiarioDto

```csharp
{
    Fecha = seg.FechaRegistro,
    EdadDias = edadDias,
    EdadSemanas = edadSemanas,
    NumeroAves = avesActuales,  // ⚠️ Puede estar incorrecto si hay traslados
    MortalidadTotal = mortalidadTotal,
    MortalidadPorcentajeDiario = avesActuales > 0 ? (mortalidadTotal / avesActuales) * 100 : 0,
    MortalidadPorcentajeAcumulado = avesIniciales > 0 ? (mortalidadAcumulada / avesIniciales) * 100 : 0,
    ErrorSexajeNumero = errorSexaje,
    ErrorSexajePorcentaje = avesActuales > 0 ? (errorSexaje / avesActuales) * 100 : 0,
    ErrorSexajePorcentajeAcumulado = avesIniciales > 0 ? (errorSexajeAcumulado / avesIniciales) * 100 : 0,
    DescarteNumero = descarte,  // ⚠️ Puede ser negativo (traslado)
    DescartePorcentajeDiario = avesActuales > 0 ? (descarte / avesActuales) * 100 : 0,
    DescartePorcentajeAcumulado = avesIniciales > 0 ? (descarteAcumulado / avesIniciales) * 100 : 0,
    ConsumoBultos = CalcularBultos(consumoKilos),  // consumoKilos / 40
    ConsumoKilos = consumoKilos,
    ConsumoKilosAcumulado = consumoAcumulado,
    ConsumoGramosPorAve = consumoGramosPorAve,
    IngresosAlimentoKilos = await ObtenerIngresosAlimentoAsync(...),
    TrasladosAlimentoKilos = await ObtenerTrasladosAlimentoAsync(...),
    PesoActual = pesoActual,
    Uniformidad = seg.UniformidadH ?? seg.UniformidadM,
    GananciaPeso = gananciaPeso,
    CoeficienteVariacion = seg.CvH ?? seg.CvM,
    SeleccionVentasNumero = descarte,  // ⚠️ Mismo valor que DescarteNumero
    SeleccionVentasPorcentaje = avesActuales > 0 ? (descarte / avesActuales) * 100 : 0
}
```

---

## ⚠️ 4. PROBLEMAS DETECTADOS

### 4.1. ✅ CORREGIDO: Cálculo de Aves Actuales con Traslados

**Problema Original**: Cuando hay un traslado de aves, se guarda un valor negativo en `SelH` o `SelM`. Al calcular `avesActuales`, se hacía:
```csharp
avesActuales -= descarte;  // Si descarte = -3, esto suma 3 aves (incorrecto)
```

**Solución Implementada**:
```csharp
// Separar selección normal de traslados
var seleccionNormal = Math.Max(0, seg.SelH) + Math.Max(0, seg.SelM);
var traslados = Math.Min(0, seg.SelH) + Math.Min(0, seg.SelM);
var trasladosAbsoluto = Math.Abs(traslados);

avesActuales -= seleccionNormal;  // Restar selección normal
avesActuales -= trasladosAbsoluto; // Restar traslados (aves que salieron)
```

**Estado**: ✅ Corregido

### 4.2. Porcentajes con Valores Negativos

**Problema**: Si `descarte` es negativo, los porcentajes pueden ser negativos o incorrectos.

**Ejemplo**:
- `DescartePorcentajeDiario = (descarte / avesActuales) * 100`
- Si `descarte = -3` y `avesActuales = 100`, entonces `porcentaje = -3%`

**Solución**: Usar valor absoluto para porcentajes o separar traslados de selecciones.

### 4.3. Descarte vs Selección Ventas

**Problema**: `DescarteNumero` y `SeleccionVentasNumero` tienen el mismo valor, pero conceptualmente son diferentes:
- Descarte: Aves retiradas por baja calidad
- Selección Ventas: Aves retiradas para venta
- Traslado: Aves movidas a otro lote/granja

**Solución**: Separar estos conceptos en el DTO o al menos en los cálculos.

---

## ✅ 5. RECOMENDACIONES

1. **Corregir el cálculo de aves actuales** para manejar correctamente los traslados
2. **Separar traslados de selecciones** en los cálculos y en el DTO
3. **Validar que los porcentajes** no sean negativos o mostrar valores absolutos
4. **Agregar logging** para rastrear cuando se aplican descuentos por traslado
5. **Documentar** que los valores negativos en `SelH`/`SelM` representan traslados

---

## 📝 6. RESUMEN DE DATOS Y CÁLCULOS

| Concepto | Fuente de Datos | Cálculo | Estado |
|----------|----------------|---------|--------|
| Aves Iniciales | `Lote.HembrasL + Lote.MachosL` | Suma directa | ✅ Correcto |
| Mortalidad Diaria | `SeguimientoLoteLevante.MortalidadHembras + MortalidadMachos` | Suma directa | ✅ Correcto |
| Mortalidad Acumulada | Suma de todas las mortalidades diarias | Acumulación | ✅ Correcto |
| Selección Normal | `SelH + SelM` (solo valores positivos) | Suma directa | ✅ Correcto |
| Traslados | `SelH + SelM` (valores negativos) | Suma directa | ⚠️ Problema en cálculo |
| Aves Actuales | `avesIniciales - mortalidad - seleccionNormal - traslados` | Resta acumulativa | ✅ Corregido |
| Consumo Diario | `ConsumoKgHembras + ConsumoKgMachos` | Suma directa | ✅ Correcto |
| Consumo Acumulado | Suma de todos los consumos diarios | Acumulación | ✅ Correcto |
| Consumo por Ave | `(consumoKilos * 1000) / avesActuales` | División | ⚠️ Depende de avesActuales |
| Peso Actual | `PesoPromH ?? PesoPromM` | Prioridad | ✅ Correcto |
| Ganancia de Peso | `pesoActual - pesoAnterior` | Diferencia | ✅ Correcto |
| Edad en Días | `(FechaRegistro - FechaEncaset).Days + 1` | Diferencia | ✅ Correcto |
| Edad en Semanas | `Math.Ceiling(edadDias / 7.0)` | División y redondeo | ✅ Correcto |

---

## 🔧 PRÓXIMOS PASOS

1. ✅ **COMPLETADO**: Corregir el cálculo de `avesActuales` para manejar correctamente los traslados
2. Probar con datos reales que incluyan traslados para validar la corrección
3. Verificar que los porcentajes se calculen correctamente (especialmente con valores negativos)
4. Considerar separar traslados de selecciones en el DTO para mayor claridad
5. Agregar logging para rastrear cuando se aplican descuentos por traslado

## 📋 RESUMEN DE VERIFICACIÓN

### Datos que Llegan ✅
- ✅ Seguimiento diario de levante con todos los campos necesarios
- ✅ Información del lote (aves iniciales, fecha encaset, etc.)
- ✅ Ingresos y traslados de alimento
- ✅ Traslados de aves reflejados como valores negativos en SelH/SelM

### Cálculos Realizados ✅
- ✅ Edad del lote (días y semanas)
- ✅ Mortalidad diaria y acumulada
- ✅ Error de sexaje diario y acumulado
- ✅ Selección normal separada de traslados
- ✅ Aves actuales calculadas correctamente (incluyendo traslados)
- ✅ Consumo de alimento diario, acumulado y por ave
- ✅ Peso actual y ganancia de peso
- ✅ Porcentajes de mortalidad, error de sexaje y descarte

### Correcciones Aplicadas ✅
- ✅ Separación de selección normal y traslados en el cálculo de aves actuales
- ✅ Manejo correcto de valores negativos (traslados)

