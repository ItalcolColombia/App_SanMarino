# Análisis de Módulos de Seguimiento Diario

## Resumen Ejecutivo

Este documento analiza cómo se guardan y calculan las cantidades de aves en los módulos de seguimiento diario (Producción y Levante) para implementar correctamente el cálculo de aves actuales en el módulo de movimiento de aves.

---

## 1. MÓDULO DE PRODUCCIÓN (Seguimiento Diario de Producción)

### 1.1 Tabla de Base de Datos
- **Tabla Principal**: `SeguimientoProduccion`
- **Tabla de Registro Inicial**: `ProduccionLotes`

### 1.2 Servicio
- **Servicio**: `ProduccionDiariaService`
- **Método de Creación**: `CreateAsync(CreateProduccionDiariaDto dto)`

### 1.3 Registro Inicial
Cuando se levanta un lote en producción, se crea un registro en `ProduccionLotes` con:
- `AvesInicialesH` (int): Cantidad inicial de hembras
- `AvesInicialesM` (int): Cantidad inicial de machos
- `FechaInicio` (DateTime): Fecha de inicio de producción
- `LoteId` (string): ID del lote

### 1.4 Campos que Afectan las Aves (en SeguimientoProduccion)
Los registros diarios en `SeguimientoProduccion` tienen los siguientes campos que reducen las aves:

- **`MortalidadH`** (int): Mortalidad de hembras
- **`MortalidadM`** (int): Mortalidad de machos
- **`SelH`** (int): Selección/descuento de hembras
- **`SelM`** (int): Selección/descuento de machos (si aplica)

### 1.5 Cálculo de Aves Actuales en Producción

```csharp
// Aves iniciales (desde ProduccionLotes)
var hembrasIniciales = produccionLote.AvesInicialesH;
var machosIniciales = produccionLote.AvesInicialesM;

// Sumar descuentos acumulados desde SeguimientoProduccion
var totalMortalidadH = seguimientos.Sum(s => s.MortalidadH);
var totalMortalidadM = seguimientos.Sum(s => s.MortalidadM);
var totalSeleccionH = seguimientos.Sum(s => s.SelH);
var totalSeleccionM = seguimientos.Sum(s => s.SelM);

// Calcular aves actuales
var hembrasActuales = Math.Max(0, hembrasIniciales - totalMortalidadH - totalSeleccionH);
var machosActuales = Math.Max(0, machosIniciales - totalMortalidadM - totalSeleccionM);
var totalAvesActuales = hembrasActuales + machosActuales;
```

### 1.6 Movimientos de Aves que Afectan Producción
- Los movimientos de aves **completados** que salen del lote se registran en `SeguimientoProduccion` como descuentos
- Los movimientos que entran al lote se registran como entradas positivas

---

## 2. MÓDULO DE LEVANTE (Seguimiento Diario de Levante)

### 2.1 Tabla de Base de Datos
- **Tabla Principal**: `SeguimientoLoteLevante`
- **Tabla de Registro Inicial**: `Lotes` (tabla principal de lotes)

### 2.2 Servicio
- **Servicio**: `SeguimientoLoteLevanteService`
- **Método de Creación**: `CreateAsync(SeguimientoLoteLevanteDto dto)`

### 2.3 Registro Inicial
Cuando se crea un lote de levante, se guarda en la tabla `Lotes` con:
- `HembrasL` (int?): Cantidad inicial de hembras
- `MachosL` (int?): Cantidad inicial de machos
- `MortCajaH` (int?): Mortalidad en caja de hembras (al momento de encasetamiento)
- `MortCajaM` (int?): Mortalidad en caja de machos (al momento de encasetamiento)
- `FechaEncaset` (DateTime?): Fecha de encasetamiento

### 2.4 Campos que Afectan las Aves (en SeguimientoLoteLevante)
Los registros diarios en `SeguimientoLoteLevante` tienen los siguientes campos que reducen las aves:

- **`MortalidadHembras`** (int): Mortalidad de hembras
- **`MortalidadMachos`** (int): Mortalidad de machos
- **`SelH`** (int): Selección/descuento de hembras
- **`SelM`** (int): Selección/descuento de machos
- **`ErrorSexajeHembras`** (int): Error de sexaje (hembras que resultaron ser machos)
- **`ErrorSexajeMachos`** (int): Error de sexaje (machos que resultaron ser hembras)

### 2.5 Cálculo de Aves Actuales en Levante

```csharp
// Aves iniciales (desde Lotes)
var hembrasIniciales = lote.HembrasL ?? 0;
var machosIniciales = lote.MachosL ?? 0;
var mortCajaH = lote.MortCajaH ?? 0;
var mortCajaM = lote.MortCajaM ?? 0;

// Sumar descuentos acumulados desde SeguimientoLoteLevante
var totalMortalidadH = seguimientos.Sum(s => s.MortalidadHembras);
var totalMortalidadM = seguimientos.Sum(s => s.MortalidadMachos);
var totalSeleccionH = seguimientos.Sum(s => s.SelH);
var totalSeleccionM = seguimientos.Sum(s => s.SelM);
var totalErrorSexajeH = seguimientos.Sum(s => s.ErrorSexajeHembras);
var totalErrorSexajeM = seguimientos.Sum(s => s.ErrorSexajeMachos);

// Calcular aves actuales
var hembrasActuales = Math.Max(0, hembrasIniciales - mortCajaH - totalMortalidadH - totalSeleccionH - totalErrorSexajeH);
var machosActuales = Math.Max(0, machosIniciales - mortCajaM - totalMortalidadM - totalSeleccionM - totalErrorSexajeM);
var totalAvesActuales = hembrasActuales + machosActuales;
```

### 2.6 Movimientos de Aves que Afectan Levante
- Los movimientos de aves **completados** que salen del lote se registran en `SeguimientoLoteLevante` como descuentos
- Los movimientos que entran al lote se registran como entradas positivas

---

## 3. DETERMINACIÓN DE ETAPA (Producción vs Levante)

### 3.1 Criterio
La etapa se determina por la **semana actual** del lote:
- **Levante**: Semanas 1-25 (días 0-174 desde `FechaEncaset`)
- **Producción**: Semana 26 en adelante (días 175+ desde `FechaEncaset`)

### 3.2 Cálculo de Semana
```csharp
var diasDesdeEncaset = (fechaActual - lote.FechaEncaset.Value.Date).Days;
var semanaActual = (diasDesdeEncaset / 7) + 1;

var tipoLote = semanaActual >= 26 ? "Produccion" : "Levante";
```

---

## 4. IMPLEMENTACIÓN REQUERIDA

### 4.1 Endpoint Actualizado: `GetInformacionLote`

El endpoint debe:
1. Determinar la etapa del lote (Producción o Levante)
2. Obtener las aves iniciales según la etapa:
   - **Producción**: Desde `ProduccionLotes.AvesInicialesH` y `AvesInicialesM`
   - **Levante**: Desde `Lotes.HembrasL` y `MachosL`
3. Calcular descuentos acumulados desde los registros diarios:
   - **Producción**: Desde `SeguimientoProduccion`
   - **Levante**: Desde `SeguimientoLoteLevante`
4. Incluir movimientos de aves completados que afectan el lote
5. Retornar:
   - Aves iniciales (hembras, machos)
   - Aves actuales calculadas (hembras, machos, mixtas, total)
   - Etapa del lote (Producción o Levante)
   - Información adicional del lote

### 4.2 Campos que Afectan al Lote

#### En Producción:
- Mortalidad (MortalidadH, MortalidadM)
- Selección (SelH, SelM)
- Movimientos de aves completados (salidas y entradas)

#### En Levante:
- Mortalidad en caja (MortCajaH, MortCajaM) - solo al inicio
- Mortalidad diaria (MortalidadHembras, MortalidadMachos)
- Selección (SelH, SelM)
- Error de sexaje (ErrorSexajeHembras, ErrorSexajeMachos)
- Movimientos de aves completados (salidas y entradas)

---

## 5. REFERENCIAS DE CÓDIGO

### 5.1 Servicios Existentes con Lógica Similar
- `DisponibilidadLoteService.ObtenerDisponibilidadAvesAsync()` - Calcula para Levante
- `LiquidacionTecnicaProduccionService.CalcularMetricasAcumuladas()` - Calcula para Producción
- `SeguimientoLoteLevanteService.CalcularHembrasVivasAsync()` - Calcula hembras vivas en Levante

### 5.2 Entidades
- `SeguimientoProduccion` - Registros diarios de producción
- `SeguimientoLoteLevante` - Registros diarios de levante
- `ProduccionLote` - Registro inicial de producción
- `Lote` - Registro inicial de levante (y general)

---

## 6. CONCLUSIÓN

Para el módulo de movimiento de aves, necesitamos:
1. Actualizar `GetInformacionLoteAsync` para calcular aves actuales desde registros diarios
2. Determinar correctamente la etapa (Producción o Levante)
3. Incluir todos los descuentos: mortalidades, selecciones, errores de sexaje, movimientos
4. Retornar información completa y precisa del estado actual del lote
