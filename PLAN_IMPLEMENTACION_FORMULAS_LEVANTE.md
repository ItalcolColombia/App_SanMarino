# 📋 PLAN DE IMPLEMENTACIÓN: FÓRMULAS REGISTRO DIARIO LEVANTE

## 🎯 OBJETIVO
Implementar todas las fórmulas necesarias para calcular los campos del registro diario de levante según la estructura del Excel.

---

## 📊 CLASIFICACIÓN DE CAMPOS

### ✅ CAMPOS DE ENTRADA (Verde - Se Capturan en el Formulario)

#### Datos Generales:
- ✅ `IDLoteRAP` - Desde lote seleccionado
- ✅ `Regional` - Desde lote
- ✅ `GRANJA` - Desde lote
- ✅ `Lote` - Desde lote
- ✅ `RAZA` - Desde lote
- ✅ `AñoG` - Desde lote
- ✅ `NÚCLEOL` - Desde lote
- ✅ `AÑON` - Desde lote
- ✅ `Edad` - Calculado automáticamente
- ✅ `Fecha` - Seleccionada en formulario
- ⚠️ `SemAño` - **FALTA CALCULAR** (semana del año)
- ⚠️ `TRASLADO` - **FALTA DETECTAR** (cuando SelH/SelM < 0)
- ✅ `Observaciones` - Campo de texto

#### Inventario Inicial:
- ✅ `HEMBRAINI` - Desde lote
- ✅ `MACHOINI` - Desde lote

#### Hembras - Producción y Manejo:
- ⚠️ `Hembra` - **CALCULAR** (hembras vivas)
- ✅ `MortH` - Campo numérico
- ✅ `SelH` - Campo numérico (puede ser negativo)
- ✅ `ErrorH` - Campo numérico
- ✅ `ConsKgH` - Campo numérico
- ⚠️ `PesoH` - **FALTA EN FORMULARIO** (existe en entidad)
- ⚠️ `UniformH` - **FALTA EN FORMULARIO** (existe en entidad)
- ⚠️ `%CVH` - **FALTA EN FORMULARIO** (existe en entidad)

#### Hembras - Nutrición:
- ⚠️ `KcalAlH` - **FALTA EN FORMULARIO** (existe en entidad, se calcula automáticamente)
- ⚠️ `%ProtAlH` - **FALTA EN FORMULARIO** (existe en entidad, se calcula automáticamente)
- ⚠️ `KcalAveH` - **CALCULAR** (existe en entidad, se calcula automáticamente)
- ⚠️ `ProtAveH` - **CALCULAR** (existe en entidad, se calcula automáticamente)

#### Machos - Producción y Manejo:
- ⚠️ `SaldoMacho` - **CALCULAR** (machos vivos)
- ✅ `MortM` - Campo numérico
- ✅ `SelM` - Campo numérico (puede ser negativo)
- ✅ `ErrorM` - Campo numérico
- ⚠️ `ConsKgM` - **FALTA EN FORMULARIO** (existe en entidad)
- ⚠️ `PesoM` - **FALTA EN FORMULARIO** (existe en entidad)
- ⚠️ `UniformM` - **FALTA EN FORMULARIO** (existe en entidad)
- ⚠️ `%CVM` - **FALTA EN FORMULARIO** (existe en entidad)

#### Machos - Nutrición:
- ❌ `KcalAlM` - **FALTA COMPLETAMENTE** (no existe en entidad)
- ❌ `%ProtAlM` - **FALTA COMPLETAMENTE** (no existe en entidad)
- ❌ `KcalAveM` - **FALTA COMPLETAMENTE** (no existe en entidad)
- ❌ `ProtAveM` - **FALTA COMPLETAMENTE** (no existe en entidad)

---

## 🧮 CAMPOS CALCULADOS (Fórmulas)

### Hembras - Indicadores y Acumulados:
- ✅ `%MortH` - Fórmula: `(MortH / Hembra) * 100`
- ✅ `%MortHGUIA` - Desde guía genética
- ✅ `DifMortH` - Fórmula: `%MortH - %MortHGUIA`
- ✅ `ACMortH` - Acumulado: `SUM(MortH)`
- ✅ `%SelH` - Fórmula: `(SelH / Hembra) * 100`
- ✅ `ACSelH` - Acumulado: `SUM(SelH)`
- ✅ `%ErrH` - Fórmula: `(ErrorH / Hembra) * 100`
- ✅ `ACErrH` - Acumulado: `SUM(ErrorH)`
- ✅ `M+S+EH` - Fórmula: `MortH + SelH + ErrorH`
- ✅ `RetAcH` - Fórmula: `ACMortH + ACSelH + ACErrH`
- ✅ `%RetiroH` - Fórmula: `(RetAcH / HEMBRAINI) * 100`
- ✅ `RetiroHGUIA` - Desde guía genética
- ✅ `AcConsH` - Acumulado: `SUM(ConsKgH)`
- ✅ `ConsAcGrH` - Fórmula: `AcConsH * 1000`
- ✅ `ConsAcGrHGUIA` - Desde guía genética
- ✅ `GrAveDiaH` - Fórmula: `ConsAcGrH / (Hembra * Edad)`
- ✅ `GrAveDiaGUIAH` - Desde guía genética
- ⚠️ `IncrConsH` - Fórmula: `ConsKgH - LAG(ConsKgH)`
- ✅ `IncrConsHGUIA` - Desde guía genética
- ✅ `%DifConsH` - Fórmula: `((ConsAcGrH - ConsAcGrHGUIA) / ConsAcGrHGUIA) * 100`
- ✅ `PesoHGUIA` - Desde guía genética
- ✅ `%DifPesoH` - Fórmula: `((PesoH - PesoHGUIA) / PesoHGUIA) * 100`
- ✅ `UnifHGUIA` - Desde guía genética

### Machos - Indicadores y Acumulados:
- ✅ `%MortM` - Fórmula: `(MortM / SaldoMacho) * 100`
- ✅ `%MortMGUIA` - Desde guía genética
- ✅ `DifMortM` - Fórmula: `%MortM - %MortMGUIA`
- ✅ `ACMortM` - Acumulado: `SUM(MortM)`
- ✅ `%SelM` - Fórmula: `(SelM / SaldoMacho) * 100`
- ✅ `ACSelM` - Acumulado: `SUM(SelM)`
- ✅ `%ErrM` - Fórmula: `(ErrorM / SaldoMacho) * 100`
- ✅ `ACErrM` - Acumulado: `SUM(ErrorM)`
- ✅ `M+S+EM` - Fórmula: `MortM + SelM + ErrorM`
- ✅ `RetAcM` - Fórmula: `ACMortM + ACSelM + ACErrM`
- ✅ `%RetAcM` - Fórmula: `(RetAcM / MACHOINI) * 100`
- ✅ `RetiroMGUIA` - Desde guía genética
- ✅ `AcConsM` - Acumulado: `SUM(ConsKgM)`
- ✅ `ConsAcGrM` - Fórmula: `AcConsM * 1000`
- ✅ `ConsAcGrMGUIA` - Desde guía genética
- ✅ `GrAveDiaM` - Fórmula: `ConsAcGrM / (SaldoMacho * Edad)`
- ✅ `GrAveDiaMGUIA` - Desde guía genética
- ⚠️ `IncrConsM` - Fórmula: `ConsKgM - LAG(ConsKgM)`
- ✅ `IncrConsMGUIA` - Desde guía genética
- ✅ `DifConsM` - Fórmula: `ConsAcGrM - ConsAcGrMGUIA`
- ✅ `PesoMGUIA` - Desde guía genética
- ✅ `%DifPesoM` - Fórmula: `((PesoM - PesoMGUIA) / PesoMGUIA) * 100`
- ✅ `UnifMGUIA` - Desde guía genética

### Comparativos y Errores de Sexaje:
- ✅ `%RelM/H` - Fórmula: `(SaldoMacho / Hembra) * 100`
- ✅ `ErrSexAcH` - Acumulado: `SUM(ErrorH)`
- ✅ `%ErrSxAcH` - Fórmula: `(ErrSexAcH / HEMBRAINI) * 100`
- ✅ `ErrSexAcM` - Acumulado: `SUM(ErrorM)`
- ✅ `%ErrSxAcM` - Fórmula: `(ErrSexAcM / MACHOINI) * 100`
- ✅ `DifConsAcH` - Fórmula: `ConsAcGrH - ConsAcGrHGUIA`
- ✅ `DifConsAcM` - Fórmula: `ConsAcGrM - ConsAcGrMGUIA`

### Nutrición Semanal - Hembras:
- ✅ `ALIMHGUÍA` - Desde guía genética
- ⚠️ `KcalSemH` - Suma semanal de `KcalAveH`
- ⚠️ `KcalSemAcH` - Acumulado semanal
- ✅ `KcalSemHGUIA` - Desde guía genética
- ✅ `KcalSemAcHGUIA` - Desde guía genética
- ⚠️ `ProtSemH` - Suma semanal de `ProtAveH`
- ⚠️ `ProtSemAcH` - Acumulado semanal
- ✅ `ProtSemHGUIA` - Desde guía genética
- ✅ `ProtSemAcHGUIA` - Desde guía genética

### Nutrición Semanal - Machos:
- ✅ `ALIMMGUÍA` - Desde guía genética
- ❌ `KcalSemM` - **FALTA** (requiere KcalAveM)
- ❌ `KcalSemAcM` - **FALTA** (requiere KcalAveM)
- ✅ `KcalSemMGUIA` - Desde guía genética
- ✅ `KcalSemAcMGUIA` - Desde guía genética
- ❌ `ProtSemM` - **FALTA** (requiere ProtAveM)
- ❌ `ProtSemAcM` - **FALTA** (requiere ProtAveM)
- ✅ `ProtSemMGUIA` - Desde guía genética
- ✅ `ProtSemAcMGUIA` - Desde guía genética

---

## 📝 PLAN DE IMPLEMENTACIÓN

### FASE 1: Agregar Campos Faltantes a la Entidad (Backend)

#### 1.1. Actualizar `SeguimientoLoteLevante.cs`:
```csharp
// Agregar campos de nutrición machos
public double? KcalAlM { get; set; }
public double? ProtAlM { get; set; }
public double? KcalAveM { get; set; }
public double? ProtAveM { get; set; }
```

#### 1.2. Actualizar `SeguimientoLoteLevanteDto.cs`:
```csharp
// Agregar campos de nutrición machos al DTO
double? KcalAlM, double? ProtAlM, double? KcalAveM, double? ProtAveM,
```

#### 1.3. Crear Migración:
- Agregar columnas `kcal_al_m`, `prot_al_m`, `kcal_ave_m`, `prot_ave_m` a tabla `seguimiento_lote_levante`

### FASE 2: Actualizar Formulario Frontend

#### 2.1. Agregar Campos al Formulario:
- Consumo kg machos (`ConsumoKgMachos`)
- Peso promedio hembras (`PesoPromH`)
- Peso promedio machos (`PesoPromM`)
- Uniformidad hembras (`UniformidadH`)
- Uniformidad machos (`UniformidadM`)
- Coeficiente de variación hembras (`CvH`)
- Coeficiente de variación machos (`CvM`)

#### 2.2. Integrar Catálogo de Alimentos:
- Permitir seleccionar alimento desde catálogo (similar a producción)
- Calcular automáticamente `KcalAlH`, `ProtAlH`, `KcalAlM`, `ProtAlM` desde el alimento seleccionado

### FASE 3: Implementar Cálculos en Backend

#### 3.1. Actualizar `SeguimientoLoteLevanteService.cs`:
- Calcular `KcalAveH` y `ProtAveH` automáticamente
- Calcular `KcalAveM` y `ProtAveM` automáticamente (cuando estén disponibles)
- Calcular `SemAño` (semana del año)
- Detectar `TRASLADO` cuando `SelH < 0` o `SelM < 0`

#### 3.2. Crear/Actualizar Stored Procedure `sp_recalcular_seguimiento_levante`:
- Implementar todas las fórmulas de acumulados
- Implementar cálculos de porcentajes
- Implementar cálculos de gramos por ave por día
- Integrar con tabla de guía genética para comparaciones
- Calcular nutrición semanal (agrupación por semana)

### FASE 4: Actualizar DTOs de Resultado

#### 4.1. Actualizar `ResultadoLevanteItemDto.cs`:
- Agregar campos faltantes:
  - `IncrConsH`, `IncrConsM`
  - `KcalSemH`, `KcalSemAcH`, `ProtSemH`, `ProtSemAcH`
  - `KcalSemM`, `KcalSemAcM`, `ProtSemM`, `ProtSemAcM`
  - `SemAño`
  - `TRASLADO`

#### 4.2. Actualizar `ProduccionResultadoLevante.cs`:
- Agregar columnas correspondientes a la tabla de snapshot

### FASE 5: Integración con Guía Genética

#### 5.1. Crear Servicio de Guía Genética:
- Obtener valores de guía según raza, edad, año
- Implementar comparaciones con valores reales

---

## ✅ CHECKLIST DE IMPLEMENTACIÓN

### Backend:
- [ ] Agregar campos `KcalAlM`, `ProtAlM`, `KcalAveM`, `ProtAveM` a entidad
- [ ] Crear migración para nuevos campos
- [ ] Actualizar DTOs con nuevos campos
- [ ] Implementar cálculo de `SemAño`
- [ ] Implementar detección de `TRASLADO`
- [ ] Actualizar `SeguimientoLoteLevanteService` para calcular valores nutricionales machos
- [ ] Crear/actualizar stored procedure con todas las fórmulas
- [ ] Agregar campos faltantes a `ResultadoLevanteItemDto`
- [ ] Agregar columnas faltantes a `ProduccionResultadoLevante`
- [ ] Integrar con servicio de guía genética

### Frontend:
- [ ] Agregar campo `ConsumoKgMachos` al formulario
- [ ] Agregar campos de pesaje semanal (PesoH, PesoM, UniformH, UniformM, CvH, CvM)
- [ ] Integrar catálogo de alimentos (selección por dropdown)
- [ ] Calcular automáticamente valores nutricionales desde alimento seleccionado
- [ ] Mostrar `SemAño` en la tabla de resultados
- [ ] Mostrar indicador de `TRASLADO` cuando corresponda

### Testing:
- [ ] Probar cálculo de acumulados
- [ ] Probar cálculo de porcentajes
- [ ] Probar cálculo de gramos por ave por día
- [ ] Probar comparaciones con guía genética
- [ ] Probar nutrición semanal
- [ ] Validar que todos los campos se muestren correctamente

---

## 🚀 PRIORIDADES

### Alta Prioridad:
1. Agregar campos faltantes al formulario (PesoH, PesoM, UniformH, UniformM, CvH, CvM, ConsKgM)
2. Implementar cálculo de `SemAño`
3. Actualizar stored procedure con fórmulas básicas (acumulados, porcentajes)

### Media Prioridad:
4. Agregar campos de nutrición machos a la entidad
5. Integrar catálogo de alimentos
6. Calcular automáticamente valores nutricionales

### Baja Prioridad:
7. Implementar nutrición semanal
8. Mejorar integración con guía genética

---

## 📚 REFERENCIAS

- `FORMULAS_REGISTRO_DIARIO_LEVANTE.md` - Documento con todas las fórmulas detalladas
- `ANALISIS_DATOS_REGISTRO_DIARIO_LEVANTE.md` - Análisis de datos disponibles vs requeridos

