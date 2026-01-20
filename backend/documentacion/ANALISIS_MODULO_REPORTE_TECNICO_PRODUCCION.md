# Análisis Completo: Módulo Reporte Técnico Producción SanMarino

## 📋 Índice
1. [Análisis del Módulo de Seguimiento Diario Producción](#1-análisis-del-módulo-de-seguimiento-diario-producción)
2. [Análisis del Módulo de Traslados de Huevos](#2-análisis-del-módulo-de-traslados-de-huevos)
3. [Estructura de Tablas y Relaciones](#3-estructura-de-tablas-y-relaciones)
4. [Campos del Seguimiento Diario de Producción](#4-campos-del-seguimiento-diario-de-producción)
5. [Propuesta de Tabs para el Nuevo Módulo](#5-propuesta-de-tabs-para-el-nuevo-módulo)

---

## 1. Análisis del Módulo de Seguimiento Diario Producción

### 1.1 Entidad Principal: `SeguimientoProduccion`
**Tabla BD:** `produccion_diaria`

### 1.2 Campos de la Entidad

#### Campos Básicos
| Campo | Tipo | Descripción | Tabla BD |
|-------|------|-------------|----------|
| `Id` | `int` | Identificador único | `id` |
| `Fecha` | `DateTime` | Fecha del registro | `fecha_registro` |
| `LoteId` | `string` | ID del lote (text en BD) | `lote_id` (text) |

#### Mortalidad y Selección
| Campo | Tipo | Descripción | Tabla BD |
|-------|------|-------------|----------|
| `MortalidadH` | `int` | Mortalidad de hembras en el día | `mortalidad_hembras` |
| `MortalidadM` | `int` | Mortalidad de machos en el día | `mortalidad_machos` |
| `SelH` | `int` | Selección/retiro de hembras | `sel_h` |

#### Consumo de Alimento
| Campo | Tipo | Descripción | Tabla BD |
|-------|------|-------------|----------|
| `ConsKgH` | `decimal` | Consumo de alimento hembras (kg) | `cons_kg_h` |
| `ConsKgM` | `decimal` | Consumo de alimento machos (kg) | `cons_kg_m` |
| `TipoAlimento` | `string` | Tipo de alimento usado | `tipo_alimento` |

#### Producción de Huevos - Totales
| Campo | Tipo | Descripción | Tabla BD |
|-------|------|-------------|----------|
| `HuevoTot` | `int` | Total de huevos producidos | `huevo_tot` |
| `HuevoInc` | `int` | Huevos incubables | `huevo_inc` |

#### Producción de Huevos - Clasificadora (Incubables)
| Campo | Tipo | Descripción | Tabla BD | Nota |
|-------|------|-------------|----------|------|
| `HuevoLimpio` | `int` | Huevos limpios | `huevo_limpio` | Parte de `HuevoInc` |
| `HuevoTratado` | `int` | Huevos tratados | `huevo_tratado` | Parte de `HuevoInc` |

**Fórmula:** `HuevoLimpio + HuevoTratado = HuevoInc`

#### Producción de Huevos - Clasificadora (No Incubables)
| Campo | Tipo | Descripción | Tabla BD | Nota |
|-------|------|-------------|----------|------|
| `HuevoSucio` | `int` | Huevos sucios | `huevo_sucio` | Parte de `HuevoTot` |
| `HuevoDeforme` | `int` | Huevos deformes | `huevo_deforme` | Parte de `HuevoTot` |
| `HuevoBlanco` | `int` | Huevos blancos | `huevo_blanco` | Parte de `HuevoTot` |
| `HuevoDobleYema` | `int` | Huevos doble yema | `huevo_doble_yema` | Parte de `HuevoTot` |
| `HuevoPiso` | `int` | Huevos de piso | `huevo_piso` | Parte de `HuevoTot` |
| `HuevoPequeno` | `int` | Huevos pequeños | `huevo_pequeno` | Parte de `HuevoTot` |
| `HuevoRoto` | `int` | Huevos rotos | `huevo_roto` | Parte de `HuevoTot` |
| `HuevoDesecho` | `int` | Huevos desecho | `huevo_desecho` | Parte de `HuevoTot` |
| `HuevoOtro` | `int` | Otros tipos de huevos | `huevo_otro` | Parte de `HuevoTot` |

**Fórmula:** `HuevoSucio + HuevoDeforme + HuevoBlanco + HuevoDobleYema + HuevoPiso + HuevoPequeno + HuevoRoto + HuevoDesecho + HuevoOtro + HuevoInc = HuevoTot`

#### Peso y Etapa
| Campo | Tipo | Descripción | Tabla BD |
|-------|------|-------------|----------|
| `PesoHuevo` | `decimal` | Peso promedio del huevo (g) | `peso_huevo` |
| `Etapa` | `int` | Etapa de producción (1: 25-33, 2: 34-50, 3: >50) | `etapa` |

#### Pesaje Semanal (Opcional, registro una vez por semana)
| Campo | Tipo | Descripción | Tabla BD |
|-------|------|-------------|----------|
| `PesoH` | `decimal?` | Peso promedio hembras (kg) | `peso_h` |
| `PesoM` | `decimal?` | Peso promedio machos (kg) | `peso_m` |
| `Uniformidad` | `decimal?` | Uniformidad del lote (%) | `uniformidad` |
| `CoeficienteVariacion` | `decimal?` | Coeficiente de variación (CV) | `coeficiente_variacion` |
| `ObservacionesPesaje` | `string?` | Observaciones del pesaje | `observaciones_pesaje` |

#### Otros Campos
| Campo | Tipo | Descripción | Tabla BD |
|-------|------|-------------|----------|
| `Observaciones` | `string?` | Observaciones generales | `observaciones` |
| `Metadata` | `JsonDocument?` | Metadata JSONB (consumo original, tipo de ítem, etc.) | `metadata` |

### 1.3 Relaciones

**NOTA IMPORTANTE:** No hay relación de navegación directa con `Lote` porque:
- `SeguimientoProduccion.LoteId` es `string` (text en BD)
- `Lote.LoteId` es `int?`
- Son tipos incompatibles para foreign key
- Para acceder al Lote, se debe convertir manualmente el string a int

### 1.4 Índices
- **Índice único:** `(LoteId, Fecha)` - Previene registros duplicados por lote y fecha

---

## 2. Análisis del Módulo de Traslados de Huevos

### 2.1 Entidad Principal: `TrasladoHuevos`
**Tabla BD:** `traslado_huevos`

### 2.2 Campos de la Entidad

#### Información del Traslado
| Campo | Tipo | Descripción | Tabla BD |
|-------|------|-------------|----------|
| `Id` | `int` | Identificador único | `id` |
| `NumeroTraslado` | `string` | Número único del traslado (generado automáticamente) | `numero_traslado` |
| `FechaTraslado` | `DateTime` | Fecha del traslado | `fecha_traslado` |
| `TipoOperacion` | `string` | "Venta" o "Traslado" | `tipo_operacion` |

#### Lote Origen
| Campo | Tipo | Descripción | Tabla BD |
|-------|------|-------------|----------|
| `LoteId` | `string` | ID del lote origen (VARCHAR) | `lote_id` |
| `GranjaOrigenId` | `int` | ID de la granja origen | `granja_origen_id` |

#### Destino (si es traslado)
| Campo | Tipo | Descripción | Tabla BD |
|-------|------|-------------|----------|
| `GranjaDestinoId` | `int?` | ID de la granja destino (null si es venta) | `granja_destino_id` |
| `LoteDestinoId` | `string?` | ID del lote destino (null si es venta) | `lote_destino_id` |
| `TipoDestino` | `string?` | "Granja", "Planta", null si es venta | `tipo_destino` |

#### Motivo y Descripción
| Campo | Tipo | Descripción | Tabla BD |
|-------|------|-------------|----------|
| `Motivo` | `string?` | Motivo del traslado/venta | `motivo` |
| `Descripcion` | `string?` | Descripción detallada | `descripcion` |

#### Cantidades por Tipo de Huevo
| Campo | Tipo | Descripción | Tabla BD |
|-------|------|-------------|----------|
| `CantidadLimpio` | `int` | Cantidad de huevos limpios | `cantidad_limpio` |
| `CantidadTratado` | `int` | Cantidad de huevos tratados | `cantidad_tratado` |
| `CantidadSucio` | `int` | Cantidad de huevos sucios | `cantidad_sucio` |
| `CantidadDeforme` | `int` | Cantidad de huevos deformes | `cantidad_deforme` |
| `CantidadBlanco` | `int` | Cantidad de huevos blancos | `cantidad_blanco` |
| `CantidadDobleYema` | `int` | Cantidad de huevos doble yema | `cantidad_doble_yema` |
| `CantidadPiso` | `int` | Cantidad de huevos de piso | `cantidad_piso` |
| `CantidadPequeno` | `int` | Cantidad de huevos pequeños | `cantidad_pequeno` |
| `CantidadRoto` | `int` | Cantidad de huevos rotos | `cantidad_roto` |
| `CantidadDesecho` | `int` | Cantidad de huevos desecho | `cantidad_desecho` |
| `CantidadOtro` | `int` | Cantidad de otros tipos | `cantidad_otro` |
| `TotalHuevos` | `int` | **Calculado:** Suma de todas las cantidades | (propiedad calculada) |

#### Estado y Usuario
| Campo | Tipo | Descripción | Tabla BD |
|-------|------|-------------|----------|
| `Estado` | `string` | "Pendiente", "Completado", "Cancelado" | `estado` |
| `UsuarioTrasladoId` | `int` | ID del usuario que realizó el traslado | `usuario_traslado_id` |
| `UsuarioNombre` | `string?` | Nombre del usuario | `usuario_nombre` |

#### Fechas de Procesamiento
| Campo | Tipo | Descripción | Tabla BD |
|-------|------|-------------|----------|
| `FechaProcesamiento` | `DateTime?` | Fecha en que se procesó | `fecha_procesamiento` |
| `FechaCancelacion` | `DateTime?` | Fecha de cancelación | `fecha_cancelacion` |

#### Observaciones
| Campo | Tipo | Descripción | Tabla BD |
|-------|------|-------------|----------|
| `Observaciones` | `string?` | Observaciones del traslado | `observaciones` |

### 2.3 Funcionalidad del Servicio

#### `TrasladoHuevosService`

**Métodos principales:**
1. **`CrearTrasladoHuevosAsync`**: Crea un nuevo traslado/venta de huevos
   - Valida disponibilidad de huevos usando `IDisponibilidadLoteService`
   - Crea el registro en `traslado_huevos`
   - Genera número de traslado automáticamente
   - Procesa automáticamente el traslado (aplica descuentos)

2. **`ProcesarTrasladoAsync`**: Procesa un traslado pendiente
   - Marca como "Completado"
   - Aplica descuento en `produccion_diaria` (resta huevos trasladados)
   - Las reducciones se calculan automáticamente en `DisponibilidadLoteService`

3. **`ObtenerTrasladosPorLoteAsync`**: Obtiene todos los traslados de un lote

### 2.4 Relación con Producción Diaria

Cuando se procesa un traslado:
- Se busca el registro de `produccion_diaria` más reciente del lote para la fecha del traslado
- Si no existe, se crea uno nuevo con valores **negativos** para descontar
- Se resta `TotalHuevos` del traslado del campo `HuevoTot` del registro diario

**Ejemplo:**
- Registro diario: `HuevoTot = 1000`
- Traslado procesado: `TotalHuevos = 200`
- Resultado: Se crea/modifica registro con `HuevoTot = -200` o se resta del existente

---

## 3. Estructura de Tablas y Relaciones

### 3.1 Tabla: `produccion_diaria` (SeguimientoProduccion)

```
produccion_diaria
├── id (PK, SERIAL)
├── lote_id (TEXT) - NO tiene FK directa a lotes
├── fecha_registro (TIMESTAMP)
├── mortalidad_hembras (INTEGER)
├── mortalidad_machos (INTEGER)
├── sel_h (INTEGER)
├── cons_kg_h (NUMERIC)
├── cons_kg_m (NUMERIC)
├── huevo_tot (INTEGER)
├── huevo_inc (INTEGER)
├── huevo_limpio (INTEGER)
├── huevo_tratado (INTEGER)
├── huevo_sucio (INTEGER)
├── huevo_deforme (INTEGER)
├── huevo_blanco (INTEGER)
├── huevo_doble_yema (INTEGER)
├── huevo_piso (INTEGER)
├── huevo_pequeno (INTEGER)
├── huevo_roto (INTEGER)
├── huevo_desecho (INTEGER)
├── huevo_otro (INTEGER)
├── tipo_alimento (VARCHAR)
├── peso_huevo (NUMERIC)
├── etapa (INTEGER)
├── peso_h (NUMERIC, nullable)
├── peso_m (NUMERIC, nullable)
├── uniformidad (NUMERIC, nullable)
├── coeficiente_variacion (NUMERIC, nullable)
├── observaciones_pesaje (TEXT, nullable)
├── observaciones (TEXT, nullable)
└── metadata (JSONB, nullable)
```

**Índices:**
- `UNIQUE (lote_id, fecha_registro)` - Previene duplicados

### 3.2 Tabla: `traslado_huevos` (TrasladoHuevos)

```
traslado_huevos
├── id (PK, SERIAL)
├── numero_traslado (VARCHAR, UNIQUE)
├── fecha_traslado (TIMESTAMP)
├── tipo_operacion (VARCHAR) - "Venta" o "Traslado"
├── lote_id (VARCHAR)
├── granja_origen_id (INTEGER)
├── granja_destino_id (INTEGER, nullable)
├── lote_destino_id (VARCHAR, nullable)
├── tipo_destino (VARCHAR, nullable)
├── motivo (VARCHAR, nullable)
├── descripcion (TEXT, nullable)
├── cantidad_limpio (INTEGER)
├── cantidad_tratado (INTEGER)
├── cantidad_sucio (INTEGER)
├── cantidad_deforme (INTEGER)
├── cantidad_blanco (INTEGER)
├── cantidad_doble_yema (INTEGER)
├── cantidad_piso (INTEGER)
├── cantidad_pequeno (INTEGER)
├── cantidad_roto (INTEGER)
├── cantidad_desecho (INTEGER)
├── cantidad_otro (INTEGER)
├── estado (VARCHAR) - "Pendiente", "Completado", "Cancelado"
├── usuario_traslado_id (INTEGER)
├── usuario_nombre (VARCHAR, nullable)
├── fecha_procesamiento (TIMESTAMP, nullable)
├── fecha_cancelacion (TIMESTAMP, nullable)
├── observaciones (TEXT, nullable)
└── [campos de auditoría: company_id, created_by_user_id, etc.]
```

### 3.3 Tabla: `produccion_lote` (ProduccionLote)

**Registro inicial de producción** - Se crea cuando un lote entra a producción (semana 25+)

```
produccion_lote
├── id (PK, SERIAL)
├── lote_id (INTEGER o VARCHAR según implementación)
├── fecha_inicio_produccion (DATE)
├── hembras_iniciales (INTEGER)
├── machos_iniciales (INTEGER)
├── huevos_iniciales (INTEGER)
├── tipo_nido (VARCHAR) - "Jansen", "Manual", "Vencomatic"
├── nucleo_produccion_id (VARCHAR)
├── granja_id (INTEGER)
└── ciclo (VARCHAR) - "normal", "2 Replume", "D: Depopulación"
```

### 3.4 Relaciones entre Tablas

```
Lote (lotes)
  └── (LoteId: int) 
      │
      ├── ProduccionLote (produccion_lote)
      │   └── (LoteId: int/VARCHAR) - Registro inicial
      │
      ├── SeguimientoProduccion (produccion_diaria)
      │   └── (LoteId: string) - Registros diarios
      │       └── [NO FK directa, conversión manual string → int]
      │
      └── TrasladoHuevos (traslado_huevos)
          └── (LoteId: string) - Traslados/ventas de huevos
              └── [Afecta produccion_diaria al procesarse]
```

---

## 4. Campos del Seguimiento Diario de Producción

### 4.1 Cómo se Crea el Seguimiento

**Frontend:** `ModalSeguimientoDiarioComponent`

**Flujo:**
1. Usuario selecciona lote de producción (semana 25+)
2. Abre modal de seguimiento diario
3. Completa formulario con:
   - Fecha de registro
   - Mortalidad (hembras y machos)
   - Selección de hembras
   - Consumo de alimento (hembras y machos) con unidad (kg/g)
   - Tipo de ítem y alimento específico (desde inventario de granja)
   - Producción de huevos:
     - Totales e incubables
     - Clasificadora completa (limpio, tratado, sucio, deforme, etc.)
   - Peso del huevo
   - Etapa (calculada automáticamente según semana)
   - Pesaje semanal (opcional, una vez por semana)
   - Observaciones

4. Al guardar, se envía `CrearSeguimientoRequest` al backend
5. Backend crea registro en `produccion_diaria`

**Backend:** `ProduccionService.CrearSeguimientoAsync`

**Validaciones:**
- No puede haber dos registros para el mismo lote y fecha
- Consumo se convierte a kg si viene en gramos
- Etapa se calcula automáticamente si no se proporciona
- Se guarda metadata con consumo original y tipo de ítem

### 4.2 Campos Calculados y Acumulados

**Para reportes, se calculan:**
- Mortalidad acumulada (hembras y machos)
- Porcentaje de mortalidad (diario y acumulado)
- Selección acumulada
- Consumo acumulado (kg)
- Producción de huevos acumulada
- Eficiencia de producción (% incubables / totales)
- Promedios semanales
- Comparación con guía genética (si está disponible)

---

## 5. Propuesta de Tabs para el Nuevo Módulo

### 5.1 Estructura General

**Módulo:** `Reporte Técnico Producción SanMarino`
**Ruta:** `/reporte-tecnico-produccion`

**Filtros (iguales al módulo de levante):**
- Granja
- Núcleo
- Galpón
- Lote
- Tipo de consolidación (Sublote / Consolidado)
- Fechas (opcional, para filtrar por rango)

### 5.2 Tabs Propuestos

#### Tab 1: **Reporte Diario Hembras**
**Similar a:** `TablaDatosDiariosHembrasComponent` (levante)

**Columnas:**
- FECHA
- EDAD (días desde inicio producción)
- AVES ANTES DE MORTALIDAD
- MORTALIDAD HEMBRAS
- SELECCIÓN HEMBRAS
- % MORTALIDAD DIA
- % SELECCIÓN DIA
- CONSUMO KG HEMBRAS
- HUEVOS TOTALES
- HUEVOS INCUBABLES
- % EFICIENCIA PRODUCCIÓN
- PESO HUEVO (g)
- OBSERVACIONES

**Datos fuente:** `SeguimientoProduccion` filtrado por lote y fechas

#### Tab 2: **Reporte Diario Machos**
**Similar a:** `TablaDatosDiariosMachosComponent` (levante)

**Columnas:**
- FECHA
- EDAD (días desde inicio producción)
- AVES ANTES DE MORTALIDAD
- MORTALIDAD MACHOS
- % MORTALIDAD DIA
- CONSUMO KG MACHOS
- OBSERVACIONES

**Datos fuente:** `SeguimientoProduccion` filtrado por lote y fechas

#### Tab 3: **Registro Semanal**
**Similar a:** `TablaLevanteCompletaComponent` (levante administrativo)

**Columnas agrupadas por semana:**
- SEMANA
- FECHA INICIO / FIN
- EDAD (semanas desde inicio producción)

**HEMBRAS:**
- Saldo inicial
- Mortalidad acumulada
- Selección acumulada
- % Mortalidad acumulada
- % Selección acumulada
- Consumo acumulado (kg)
- Huevos totales acumulados
- Huevos incubables acumulados
- % Eficiencia producción
- Peso huevo promedio
- Peso promedio hembras (si hay pesaje)
- Uniformidad (si hay pesaje)

**MACHOS:**
- Saldo inicial
- Mortalidad acumulada
- % Mortalidad acumulada
- Consumo acumulado (kg)
- Peso promedio machos (si hay pesaje)

**GUÍA GENÉTICA (valores amarillos):**
- Mortalidad guía hembras
- Mortalidad guía machos
- Consumo guía hembras
- Consumo guía machos
- Producción guía (huevos)
- Peso guía hembras
- Peso guía machos
- Uniformidad guía

**Datos fuente:** 
- `SeguimientoProduccion` agrupado por semana
- `IGuiaGeneticaService` para valores de guía

#### Tab 4: **Registro Semana Hembras**
**Similar a:** `TablaLevanteSemanalHembrasComponent`

**Enfoque:** Solo datos de hembras, similar al tab 3 pero filtrado

#### Tab 5: **Registro Semana Machos**
**Similar a:** `TablaLevanteSemanalMachosComponent`

**Enfoque:** Solo datos de machos, similar al tab 3 pero filtrado

#### Tab 6: **Traslados de Huevos** (NUEVO - específico de producción)
**Componente:** `TablaTrasladosHuevosComponent` (nuevo)

**Columnas:**
- FECHA TRASLADO
- NÚMERO TRASLADO
- TIPO OPERACIÓN (Venta/Traslado)
- DESTINO (Granja/Planta)
- CANTIDAD LIMPIO
- CANTIDAD TRATADO
- CANTIDAD SUCIO
- CANTIDAD DEFORME
- CANTIDAD BLANCO
- CANTIDAD DOBLE YEMA
- CANTIDAD PISO
- CANTIDAD PEQUEÑO
- CANTIDAD ROTO
- CANTIDAD DESECHO
- CANTIDAD OTRO
- TOTAL HUEVOS
- ESTADO
- OBSERVACIONES

**Datos fuente:** `TrasladoHuevos` filtrado por lote y fechas

**Funcionalidad adicional:**
- Mostrar traslados que afectan el período seleccionado
- Agrupar por semana si es necesario
- Mostrar totales acumulados de traslados

#### Tab 7: **Clasificadora de Huevos** (NUEVO - específico de producción)
**Componente:** `TablaClasificadoraHuevosComponent` (nuevo)

**Columnas:**
- FECHA
- EDAD (días)
- HUEVOS TOTALES
- HUEVOS INCUBABLES
- HUEVOS LIMPIOS
- HUEVOS TRATADOS
- HUEVOS SUCIOS
- HUEVOS DEFORMES
- HUEVOS BLANCOS
- HUEVOS DOBLE YEMA
- HUEVOS PISO
- HUEVOS PEQUEÑOS
- HUEVOS ROTOS
- HUEVOS DESECHO
- HUEVOS OTRO
- % EFICIENCIA (incubables / totales)
- PESO HUEVO PROMEDIO

**Datos fuente:** `SeguimientoProduccion` - campos de clasificadora

**Agrupación opcional:** Por semana para ver tendencias

---

## 6. Consideraciones Técnicas

### 6.1 Backend - Nuevos Endpoints Necesarios

**En `ReporteTecnicoController` o nuevo `ReporteTecnicoProduccionController`:**

1. **`GET /api/ReporteTecnico/produccion/tabs/{loteId}`**
   - Similar a `/levante/tabs/{loteId}`
   - Retorna: `ReporteTecnicoProduccionConTabsDto`
   - Incluye: datos diarios hembras, datos diarios machos, datos semanales, traslados

2. **`GET /api/ReporteTecnico/produccion/completo/{loteId}`**
   - Similar a `/levante/completo/{loteId}`
   - Retorna: `ReporteTecnicoProduccionCompletoDto`
   - Para el módulo administrativo (si se crea)

3. **`GET /api/ReporteTecnico/produccion/traslados/{loteId}`**
   - Obtiene traslados de huevos del lote
   - Filtrado por fechas opcional

### 6.2 DTOs Necesarios

**Backend:**
- `ReporteTecnicoProduccionDiarioHembrasDto`
- `ReporteTecnicoProduccionDiarioMachosDto`
- `ReporteTecnicoProduccionSemanalDto`
- `ReporteTecnicoProduccionConTabsDto`
- `ReporteTecnicoProduccionCompletoDto`
- `TrasladoHuevosReporteDto` (extensión de `TrasladoHuevosDto`)

**Frontend:**
- Interfaces TypeScript equivalentes en `reporte-tecnico-produccion.service.ts`

### 6.3 Servicios Backend

**`ReporteTecnicoProduccionService`** (ya existe, extender):
- `GenerarReporteDiarioHembrasAsync`
- `GenerarReporteDiarioMachosAsync`
- `GenerarReporteSemanalAsync`
- `GenerarReporteConTabsAsync`
- `ObtenerTrasladosHuevosAsync`

### 6.4 Componentes Frontend Nuevos

1. `TablaDatosDiariosHembrasProduccionComponent`
2. `TablaDatosDiariosMachosProduccionComponent`
3. `TablaProduccionSemanalComponent`
4. `TablaProduccionSemanalHembrasComponent`
5. `TablaProduccionSemanalMachosComponent`
6. `TablaTrasladosHuevosComponent`
7. `TablaClasificadoraHuevosComponent`

### 6.5 Diferencias Clave con Levante

1. **Producción de Huevos:** Campo principal en producción, no existe en levante
2. **Clasificadora de Huevos:** Múltiples tipos de huevos (limpio, tratado, sucio, etc.)
3. **Traslados de Huevos:** Módulo específico que afecta la producción diaria
4. **Etapas:** Basadas en semanas de producción (25-33, 34-50, >50) vs semanas de levante (1-25)
5. **Pesaje Semanal:** Opcional, se registra una vez por semana
6. **No hay separación por género en consumo diario:** Se registra consumo de hembras y machos por separado, pero en la misma tabla

---

## 7. Resumen de Campos Clave para Reportes

### 7.1 Campos Diarios (de `SeguimientoProduccion`)

**Mortalidad:**
- `MortalidadH`, `MortalidadM`

**Selección:**
- `SelH`

**Consumo:**
- `ConsKgH`, `ConsKgM`

**Producción:**
- `HuevoTot`, `HuevoInc`
- Todos los campos de clasificadora

**Peso:**
- `PesoHuevo`

**Pesaje Semanal (opcional):**
- `PesoH`, `PesoM`, `Uniformidad`, `CoeficienteVariacion`

### 7.2 Campos de Traslados (de `TrasladoHuevos`)

**Información:**
- `FechaTraslado`, `NumeroTraslado`, `TipoOperacion`, `Estado`

**Cantidades:**
- Todos los campos `Cantidad*` (Limpio, Tratado, Sucio, etc.)
- `TotalHuevos` (calculado)

### 7.3 Campos Calculados para Reportes

**Acumulados:**
- Mortalidad acumulada hembras/machos
- Selección acumulada hembras
- Consumo acumulado hembras/machos
- Producción acumulada (huevos totales/incubables)

**Porcentajes:**
- % Mortalidad (diario y acumulado)
- % Selección (diario y acumulado)
- % Eficiencia producción (incubables / totales)

**Promedios:**
- Peso huevo promedio
- Consumo promedio diario
- Producción promedio diaria

**Comparación con Guía:**
- Diferencia mortalidad
- Diferencia consumo
- Diferencia producción
- Diferencia peso

---

## 8. Próximos Pasos

1. ✅ Análisis completo de módulos existentes
2. ⏳ Diseño de DTOs backend
3. ⏳ Implementación de servicios backend
4. ⏳ Creación de endpoints API
5. ⏳ Diseño de componentes frontend
6. ⏳ Implementación de componentes frontend
7. ⏳ Integración con módulo administrativo (opcional)

---

**Fecha de Análisis:** 2025-01-19
**Autor:** Análisis Automatizado
**Versión:** 1.0
