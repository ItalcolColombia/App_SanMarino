# 📊 ANÁLISIS COMPLETO DE MÓDULOS PARA REPORTES
## Sistema San Marino - Módulos: Seguimiento Diario, Levante, Producción, Lote y Traslados de Aves

---

## 📋 TABLA DE CONTENIDOS

1. [Módulo Lote](#1-módulo-lote)
2. [Módulo Seguimiento Diario - Levante](#2-módulo-seguimiento-diario---levante)
3. [Módulo Seguimiento Diario - Producción](#3-módulo-seguimiento-diario---producción)
4. [Módulo Producción (ProduccionLote)](#4-módulo-producción-produccionlote)
5. [Módulo Traslados de Aves (Movimientos)](#5-módulo-traslados-de-aves-movimientos)
6. [Módulo Inventario de Aves](#6-módulo-inventario-de-aves)

---

## 1. MÓDULO LOTE

### 📌 Entidad: `Lote`
**Tabla BD:** `lotes`

### Variables y Descripción:

| Variable | Tipo | Descripción | ¿Qué datos guarda? |
|----------|------|-------------|-------------------|
| **LoteId** | `int?` | Identificador único del lote (auto-incremento) | ID numérico del lote |
| **LoteNombre** | `string` | Nombre descriptivo del lote | Ej: "Lote-2025-001" |
| **GranjaId** | `int` | ID de la granja donde está el lote | FK a tabla `farms` |
| **NucleoId** | `string?` | ID del núcleo dentro de la granja | Identificador del núcleo |
| **GalponId** | `string?` | ID del galpón dentro del núcleo | Identificador del galpón |
| **Regional** | `string?` | Región geográfica del lote | Ej: "Norte", "Sur", "Centro" |
| **FechaEncaset** | `DateTime?` | Fecha en que se encasetaron las aves | Fecha de inicio del lote |
| **HembrasL** | `int?` | Cantidad inicial de hembras en el lote | Número de hembras al inicio |
| **MachosL** | `int?` | Cantidad inicial de machos en el lote | Número de machos al inicio |
| **PesoInicialH** | `double?` | Peso inicial promedio de hembras (kg) | Peso en kilogramos |
| **PesoInicialM** | `double?` | Peso inicial promedio de machos (kg) | Peso en kilogramos |
| **UnifH** | `double?` | Uniformidad inicial de hembras (%) | Porcentaje de uniformidad |
| **UnifM** | `double?` | Uniformidad inicial de machos (%) | Porcentaje de uniformidad |
| **MortCajaH** | `int?` | Mortalidad en caja de hembras | Número de hembras muertas en transporte |
| **MortCajaM** | `int?` | Mortalidad en caja de machos | Número de machos muertos en transporte |
| **Raza** | `string?` | Raza de las aves del lote | Ej: "Ross 308", "Cobb 500" |
| **AnoTablaGenetica** | `int?` | Año de la tabla genética de referencia | Año de la guía genética |
| **Linea** | `string?` | Línea genética de las aves | Línea específica de la raza |
| **TipoLinea** | `string?` | Tipo de línea (Hembra/Macho) | Clasificación de la línea |
| **CodigoGuiaGenetica** | `string?` | Código de la guía genética utilizada | Código de referencia |
| **LineaGeneticaId** | `int?` | ID de la línea genética en el sistema | FK a tabla de líneas genéticas |
| **Tecnico** | `string?` | Nombre del técnico responsable | Responsable técnico del lote |
| **Mixtas** | `int?` | Cantidad de aves mixtas (sin sexar) | Aves sin clasificación de sexo |
| **PesoMixto** | `double?` | Peso promedio de aves mixtas (kg) | Peso en kilogramos |
| **AvesEncasetadas** | `int?` | Total de aves encasetadas | Suma total de aves al inicio |
| **EdadInicial** | `int?` | Edad inicial de las aves (días) | Edad al momento del encaset |
| **LoteErp** | `string?` | Código del lote en sistema ERP externo | Código de integración |
| **EstadoTraslado** | `string?` | Estado del traslado del lote | "normal", "trasladado", "en_transferencia" |

### Relaciones:
- **Farm** (Granja): Relación con la granja
- **Nucleo**: Relación con el núcleo (opcional)
- **Galpon**: Relación con el galpón (opcional)
- **Reproductoras**: Lista de reproductoras asociadas al lote

### Campos de Auditoría (heredados de `AuditableEntity`):
- `CompanyId`: ID de la compañía
- `CreatedByUserId`: Usuario que creó el registro
- `CreatedAt`: Fecha de creación
- `UpdatedByUserId`: Usuario que actualizó
- `UpdatedAt`: Fecha de actualización
- `DeletedAt`: Fecha de eliminación (soft delete)

---

## 2. MÓDULO SEGUIMIENTO DIARIO - LEVANTE

### 📌 Entidad: `SeguimientoLoteLevante`
**Tabla BD:** `seguimiento_lote_levante`

### Variables y Descripción:

| Variable | Tipo | Descripción | ¿Qué datos guarda? |
|----------|------|-------------|-------------------|
| **Id** | `int` | Identificador único del seguimiento | ID del registro diario |
| **LoteId** | `int` | ID del lote al que pertenece | FK a tabla `lotes` |
| **FechaRegistro** | `DateTime` | Fecha del registro de seguimiento | Fecha del día registrado |
| **MortalidadHembras** | `int` | Cantidad de hembras muertas en el día | Número de hembras fallecidas |
| **MortalidadMachos** | `int` | Cantidad de machos muertos en el día | Número de machos fallecidos |
| **SelH** | `int` | Selección/retiro de hembras | Hembras retiradas del lote |
| **SelM** | `int` | Selección/retiro de machos | Machos retirados del lote |
| **ErrorSexajeHembras** | `int` | Errores de sexaje detectados en hembras | Corrección de clasificación |
| **ErrorSexajeMachos** | `int` | Errores de sexaje detectados en machos | Corrección de clasificación |
| **ConsumoKgHembras** | `double` | Consumo de alimento hembras (kg) | Kilogramos consumidos por hembras |
| **ConsumoKgMachos** | `double?` | Consumo de alimento machos (kg) | Kilogramos consumidos por machos (opcional) |
| **TipoAlimento** | `string` | Tipo de alimento utilizado | Nombre o código del alimento |
| **Observaciones** | `string?` | Observaciones generales del día | Notas y comentarios |
| **KcalAlH** | `double?` | Kilocalorías por kg de alimento (hembras) | Valor nutricional calculado |
| **ProtAlH** | `double?` | Proteína por kg de alimento (hembras) | Valor nutricional calculado |
| **KcalAveH** | `double?` | Kilocalorías por ave por día (hembras) | Métrica nutricional calculada |
| **ProtAveH** | `double?` | Proteína por ave por día (hembras) | Métrica nutricional calculada |
| **Ciclo** | `string` | Ciclo de alimentación | "Normal" o "Reforzado" |
| **PesoPromH** | `double?` | Peso promedio hembras (kg) | Peso en kilogramos (semanal) |
| **PesoPromM** | `double?` | Peso promedio machos (kg) | Peso en kilogramos (semanal) |
| **UniformidadH** | `double?` | Uniformidad de hembras (%) | Porcentaje de uniformidad |
| **UniformidadM** | `double?` | Uniformidad de machos (%) | Porcentaje de uniformidad |
| **CvH** | `double?` | Coeficiente de variación hembras | Medida de variabilidad |
| **CvM** | `double?` | Coeficiente de variación machos | Medida de variabilidad |

### Relaciones:
- **Lote**: Relación con el lote (FK)

### Cálculos Automáticos:
- `KcalAlH` y `ProtAlH`: Se calculan automáticamente según el tipo de alimento
- `KcalAveH` y `ProtAveH`: Se derivan del consumo y valores nutricionales

### Uso:
Este módulo registra el seguimiento diario de lotes en fase de **LEVANTE** (desde el encaset hasta la semana 25 aproximadamente).

---

## 3. MÓDULO SEGUIMIENTO DIARIO - PRODUCCIÓN

### 📌 Entidad: `SeguimientoProduccion`
**Tabla BD:** `produccion_diaria`

### Variables y Descripción:

| Variable | Tipo | Descripción | ¿Qué datos guarda? |
|----------|------|-------------|-------------------|
| **Id** | `int` | Identificador único del seguimiento | ID del registro diario |
| **Fecha** | `DateTime` | Fecha del registro | Fecha del día registrado |
| **LoteId** | `string` | ID del lote (texto en BD) | ID del lote como string |
| **MortalidadH** | `int` | Mortalidad de hembras en el día | Número de hembras fallecidas |
| **MortalidadM** | `int` | Mortalidad de machos en el día | Número de machos fallecidos |
| **SelH** | `int` | Selección/retiro de hembras | Hembras retiradas del lote |
| **ConsKgH** | `decimal` | Consumo de alimento hembras (kg) | Kilogramos consumidos por hembras |
| **ConsKgM** | `decimal` | Consumo de alimento machos (kg) | Kilogramos consumidos por machos |
| **HuevoTot** | `int` | Total de huevos producidos | Cantidad total de huevos |
| **HuevoInc** | `int` | Huevos incubables | Huevos aptos para incubación |
| **HuevoLimpio** | `int` | Huevos limpios | Clasificación de huevos |
| **HuevoTratado** | `int` | Huevos tratados | Clasificación de huevos |
| **HuevoSucio** | `int` | Huevos sucios | Clasificación de huevos |
| **HuevoDeforme** | `int` | Huevos deformes | Clasificación de huevos |
| **HuevoBlanco** | `int` | Huevos blancos | Clasificación de huevos |
| **HuevoDobleYema** | `int` | Huevos con doble yema | Clasificación de huevos |
| **HuevoPiso** | `int` | Huevos de piso | Clasificación de huevos |
| **HuevoPequeno** | `int` | Huevos pequeños | Clasificación de huevos |
| **HuevoRoto** | `int` | Huevos rotos | Clasificación de huevos |
| **HuevoDesecho** | `int` | Huevos de desecho | Clasificación de huevos |
| **HuevoOtro** | `int` | Otros tipos de huevos | Clasificación de huevos |
| **TipoAlimento** | `string` | Tipo de alimento utilizado | Nombre o código del alimento |
| **PesoHuevo** | `decimal` | Peso promedio del huevo (g) | Peso en gramos |
| **Etapa** | `int` | Etapa de producción | 1: Semana 25-33, 2: 34-50, 3: >50 |
| **Observaciones** | `string?` | Observaciones generales | Notas y comentarios |
| **PesoH** | `decimal?` | Peso promedio hembras (kg) - Semanal | Peso en kilogramos (registro semanal) |
| **PesoM** | `decimal?` | Peso promedio machos (kg) - Semanal | Peso en kilogramos (registro semanal) |
| **Uniformidad** | `decimal?` | Uniformidad del lote (%) - Semanal | Porcentaje de uniformidad |
| **CoeficienteVariacion** | `decimal?` | Coeficiente de variación - Semanal | Medida de variabilidad |
| **ObservacionesPesaje** | `string?` | Observaciones del pesaje semanal | Notas específicas del pesaje |

### Notas Importantes:
- **Clasificación de Huevos:**
  - `HuevoLimpio + HuevoTratado` = `HuevoInc` (huevos incubables)
  - `HuevoSucio + HuevoDeforme + HuevoBlanco + HuevoDobleYema + HuevoPiso + HuevoPequeno + HuevoRoto + HuevoDesecho + HuevoOtro` = `HuevoTot` (huevos totales)

- **Etapas de Producción:**
  - Etapa 1: Semanas 25-33 (Inicio de producción)
  - Etapa 2: Semanas 34-50 (Producción pico)
  - Etapa 3: Semanas >50 (Producción tardía)

- **Campos Semanales:**
  - `PesoH`, `PesoM`, `Uniformidad`, `CoeficienteVariacion` se registran una vez por semana

### Uso:
Este módulo registra el seguimiento diario de lotes en fase de **PRODUCCIÓN** (desde la semana 25 en adelante).

---

## 4. MÓDULO PRODUCCIÓN (ProduccionLote)

### 📌 Entidad: `ProduccionLote`
**Tabla BD:** `produccion_lotes`

### Variables y Descripción:

| Variable | Tipo | Descripción | ¿Qué datos guarda? |
|----------|------|-------------|-------------------|
| **Id** | `int` | Identificador único | ID del registro de producción |
| **LoteId** | `string` | ID del lote (VARCHAR en BD) | ID del lote como string |
| **FechaInicio** | `DateTime` | Fecha de inicio de producción | Fecha cuando el lote entra a producción |
| **AvesInicialesH** | `int` | Cantidad inicial de hembras | Número de hembras al inicio de producción |
| **AvesInicialesM** | `int` | Cantidad inicial de machos | Número de machos al inicio de producción |
| **HuevosIniciales** | `int` | Cantidad inicial de huevos | Huevos al inicio (si aplica) |
| **TipoNido** | `string` | Tipo de nido utilizado | "Jansen", "Manual", "Vencomatic" |
| **GranjaId** | `int` | ID de la granja | FK a tabla `farms` |
| **NucleoId** | `string` | ID del núcleo | Identificador del núcleo |
| **NucleoP** | `string?` | Núcleo de Producción | Núcleo específico de producción |
| **GalponId** | `string?` | ID del galpón | Identificador del galpón |
| **Ciclo** | `string` | Ciclo de producción | "normal", "2 Replume", "D: Depopulación" |

### Relaciones:
- **Seguimientos**: Colección de `ProduccionSeguimiento` (registros diarios)

### Uso:
Este módulo configura el **registro inicial** de un lote cuando entra a la fase de producción. Es un requisito previo para poder registrar seguimientos diarios de producción.

---

## 5. MÓDULO TRASLADOS DE AVES (MOVIMIENTOS)

### 📌 Entidad: `MovimientoAves`
**Tabla BD:** `movimiento_aves`

### Variables y Descripción:

| Variable | Tipo | Descripción | ¿Qué datos guarda? |
|----------|------|-------------|-------------------|
| **Id** | `int` | Identificador único del movimiento | ID del movimiento |
| **NumeroMovimiento** | `string` | Número único del movimiento | Ej: "MOV-20251015-000001" |
| **FechaMovimiento** | `DateTime` | Fecha del movimiento | Fecha en que se realiza el traslado |
| **TipoMovimiento** | `string` | Tipo de movimiento | "Traslado", "Ajuste", "Liquidacion" |
| **InventarioOrigenId** | `int?` | ID del inventario origen | FK a `inventario_aves` |
| **LoteOrigenId** | `int?` | ID del lote origen | FK a tabla `lotes` |
| **GranjaOrigenId** | `int?` | ID de la granja origen | FK a tabla `farms` |
| **NucleoOrigenId** | `string?` | ID del núcleo origen | Identificador del núcleo |
| **GalponOrigenId** | `string?` | ID del galpón origen | Identificador del galpón |
| **InventarioDestinoId** | `int?` | ID del inventario destino | FK a `inventario_aves` |
| **LoteDestinoId** | `int?` | ID del lote destino | FK a tabla `lotes` |
| **GranjaDestinoId** | `int?` | ID de la granja destino | FK a tabla `farms` |
| **NucleoDestinoId** | `string?` | ID del núcleo destino | Identificador del núcleo |
| **GalponDestinoId** | `string?` | ID del galpón destino | Identificador del galpón |
| **CantidadHembras** | `int` | Cantidad de hembras movidas | Número de hembras trasladadas |
| **CantidadMachos** | `int` | Cantidad de machos movidos | Número de machos trasladados |
| **CantidadMixtas** | `int` | Cantidad de aves mixtas movidas | Número de aves mixtas trasladadas |
| **MotivoMovimiento** | `string?` | Motivo del movimiento | Razón del traslado |
| **Observaciones** | `string?` | Observaciones del movimiento | Notas adicionales |
| **Estado** | `string` | Estado del movimiento | "Pendiente", "Completado", "Cancelado" |
| **UsuarioMovimientoId** | `int` | ID del usuario que realiza el movimiento | FK a tabla de usuarios |
| **UsuarioNombre** | `string?` | Nombre del usuario | Nombre del usuario que registra |
| **FechaProcesamiento** | `DateTime?` | Fecha de procesamiento | Fecha cuando se completa el movimiento |
| **FechaCancelacion** | `DateTime?` | Fecha de cancelación | Fecha cuando se cancela el movimiento |

### Propiedades Calculadas:
- **TotalAves**: `CantidadHembras + CantidadMachos + CantidadMixtas`

### Métodos de Dominio:
- `EsMovimientoValido()`: Valida si el movimiento puede procesarse
- `Procesar()`: Marca el movimiento como completado
- `Cancelar(motivo)`: Cancela el movimiento con un motivo
- `GenerarNumeroMovimiento()`: Genera número único automático
- `EsMovimientoInterno()`: Verifica si es movimiento dentro de la misma granja
- `EsMovimientoEntreGranjas()`: Verifica si es movimiento entre diferentes granjas

### Relaciones:
- **InventarioOrigen**: Relación con inventario origen
- **InventarioDestino**: Relación con inventario destino
- **LoteOrigen**: Relación con lote origen
- **LoteDestino**: Relación con lote destino
- **GranjaOrigen**: Relación con granja origen
- **GranjaDestino**: Relación con granja destino

### Uso:
Este módulo registra todos los **movimientos y traslados de aves** entre ubicaciones (granjas, núcleos, galpones, lotes).

---

## 6. MÓDULO INVENTARIO DE AVES

### 📌 Entidad: `InventarioAves`
**Tabla BD:** `inventario_aves`

### Variables y Descripción:

| Variable | Tipo | Descripción | ¿Qué datos guarda? |
|----------|------|-------------|-------------------|
| **Id** | `int` | Identificador único del inventario | ID del registro de inventario |
| **LoteId** | `int` | ID del lote | FK a tabla `lotes` |
| **GranjaId** | `int` | ID de la granja | FK a tabla `farms` |
| **NucleoId** | `string?` | ID del núcleo | Identificador del núcleo |
| **GalponId** | `string?` | ID del galpón | Identificador del galpón |
| **CantidadHembras** | `int` | Cantidad actual de hembras | Número de hembras en inventario |
| **CantidadMachos** | `int` | Cantidad actual de machos | Número de machos en inventario |
| **CantidadMixtas** | `int` | Cantidad actual de aves mixtas | Número de aves mixtas |
| **FechaActualizacion** | `DateTime` | Fecha de última actualización | Última fecha de modificación |
| **Observaciones** | `string?` | Observaciones del inventario | Notas adicionales |
| **Estado** | `string` | Estado del inventario | "Activo", "Trasladado", "Liquidado" |

### Propiedades Calculadas:
- **TotalAves**: `CantidadHembras + CantidadMachos + CantidadMixtas`

### Métodos de Dominio:
- `PuedeRealizarMovimiento(hembras, machos, mixtas)`: Valida si hay suficientes aves para el movimiento
- `AplicarMovimientoSalida(hembras, machos, mixtas)`: Aplica un movimiento de salida al inventario

### Relaciones:
- **Lote**: Relación con el lote
- **Granja**: Relación con la granja
- **Nucleo**: Relación con el núcleo (opcional)
- **Galpon**: Relación con el galpón (opcional)
- **MovimientosOrigen**: Colección de movimientos donde este inventario es origen
- **MovimientosDestino**: Colección de movimientos donde este inventario es destino

### Uso:
Este módulo mantiene el **inventario actual** de aves por ubicación. Se actualiza automáticamente cuando se procesan movimientos.

---

## 📊 RESUMEN DE DATOS POR MÓDULO

### Módulo Lote:
- **Datos principales**: Información inicial del lote, ubicación, características genéticas, cantidades iniciales de aves, pesos iniciales, uniformidades.

### Módulo Seguimiento Diario - Levante:
- **Datos principales**: Mortalidad diaria, selecciones, consumo de alimento, pesos semanales, uniformidades, errores de sexaje, métricas nutricionales.

### Módulo Seguimiento Diario - Producción:
- **Datos principales**: Mortalidad diaria, selecciones, consumo de alimento, producción de huevos (total e incubables), clasificación detallada de huevos, peso de huevo, etapa de producción, pesos semanales.

### Módulo Producción (ProduccionLote):
- **Datos principales**: Configuración inicial del lote para producción, aves iniciales, tipo de nido, ciclo de producción.

### Módulo Traslados de Aves:
- **Datos principales**: Movimientos de aves entre ubicaciones, cantidades movidas, origen y destino, estado del movimiento, usuario responsable.

### Módulo Inventario de Aves:
- **Datos principales**: Inventario actual de aves por ubicación, cantidades actuales de hembras, machos y mixtas.

---

## 🔗 RELACIONES ENTRE MÓDULOS

1. **Lote** → **SeguimientoLoteLevante**: Un lote tiene muchos registros de seguimiento diario en levante
2. **Lote** → **SeguimientoProduccion**: Un lote tiene muchos registros de seguimiento diario en producción
3. **Lote** → **ProduccionLote**: Un lote tiene un registro inicial de producción (1:1)
4. **Lote** → **MovimientoAves**: Un lote puede ser origen o destino de múltiples movimientos
5. **Lote** → **InventarioAves**: Un lote tiene un inventario actual de aves
6. **MovimientoAves** → **InventarioAves**: Los movimientos actualizan los inventarios

---

## 📝 NOTAS PARA CREACIÓN DE REPORTES

### Consideraciones Importantes:

1. **Tipos de Datos:**
   - `LoteId` en `SeguimientoProduccion` y `ProduccionLote` es `string` (texto)
   - `LoteId` en `Lote`, `SeguimientoLoteLevante` y `MovimientoAves` es `int` (numérico)
   - Al hacer joins, considerar conversión de tipos

2. **Fechas:**
   - Todas las fechas están en formato `DateTime` con timezone
   - Considerar zona horaria al generar reportes

3. **Campos Calculados:**
   - Muchos módulos tienen propiedades calculadas (ej: `TotalAves`)
   - Algunos campos se calculan automáticamente (ej: `KcalAlH`, `ProtAlH`)

4. **Estados:**
   - `MovimientoAves.Estado`: "Pendiente", "Completado", "Cancelado"
   - `InventarioAves.Estado`: "Activo", "Trasladado", "Liquidado"
   - `Lote.EstadoTraslado`: "normal", "trasladado", "en_transferencia"

5. **Etapas de Producción:**
   - Etapa 1: Semanas 25-33
   - Etapa 2: Semanas 34-50
   - Etapa 3: Semanas >50

6. **Clasificación de Huevos:**
   - Incubables: `HuevoLimpio + HuevoTratado`
   - Totales: Suma de todas las clasificaciones

---

## 🎯 VARIABLES CLAVE PARA REPORTES COMUNES

### Reporte de Mortalidad:
- `MortalidadHembras`, `MortalidadMachos` (SeguimientoLevante/Produccion)
- `FechaRegistro` para agrupar por período

### Reporte de Consumo:
- `ConsumoKgHembras`, `ConsumoKgMachos` (SeguimientoLevante)
- `ConsKgH`, `ConsKgM` (SeguimientoProduccion)

### Reporte de Producción de Huevos:
- `HuevoTot`, `HuevoInc` (SeguimientoProduccion)
- Clasificaciones detalladas de huevos

### Reporte de Traslados:
- `CantidadHembras`, `CantidadMachos`, `CantidadMixtas` (MovimientoAves)
- `LoteOrigenId`, `LoteDestinoId`, `GranjaOrigenId`, `GranjaDestinoId`

### Reporte de Inventario:
- `CantidadHembras`, `CantidadMachos`, `CantidadMixtas` (InventarioAves)
- `GranjaId`, `NucleoId`, `GalponId` para agrupar por ubicación

---

**Documento generado para análisis de módulos y creación de reportes**
**Fecha:** 2025-01-XX
**Sistema:** App San Marino

