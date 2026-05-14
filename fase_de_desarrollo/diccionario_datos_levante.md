# IV. Diccionario de Datos y Mapeo Relacional - Fase Levante

## 1. Identificación de Entidades y Campos

### 1.1 Entidad: `LotePosturaBase` 
**Tabla**: `public.lote_postura_base`  
**Propósito**: Registro maestro que agrupa lotes de postura (creación rápida). Nodo principal para asociar lotes de levante, producción y seguimientos.

| Campo | Tipo | Nullable | PK/FK | Descripción |
|-------|------|----------|-------|-------------|
| `lote_postura_base_id` | INT | NO | **PK** | Identificador único del lote base (auto-incremento) |
| `lote_nombre` | VARCHAR(200) | NO | - | Nombre del lote base (ej: K324) |
| `codigo_erp` | VARCHAR(80) | SÍ | - | Código ERP del lote |
| `cantidad_hembras` | INT | NO | - | Cantidad inicial de hembras en el lote base |
| `cantidad_machos` | INT | NO | - | Cantidad inicial de machos en el lote base |
| `cantidad_mixtas` | INT | NO | - | Cantidad de aves mixtas asociadas al lote base |
| `pais_id` | INT | SÍ | - | ID del país del lote |
| `company_id` | INT | NO | - | ID de la empresa propietaria (auditoría) |
| `created_by_user_id` | INT | NO | - | ID del usuario que creó el registro |
| `created_at` | TIMESTAMP | NO | - | Fecha/hora de creación |
| `updated_by_user_id` | INT | SÍ | - | ID del usuario que actualizó el registro |
| `updated_at` | TIMESTAMP | SÍ | - | Fecha/hora de última actualización |
| `deleted_at` | TIMESTAMP | SÍ | - | Fecha/hora de eliminación lógica |

**Índices**:
- `ix_lote_postura_base_company` (company_id)
- `ix_lote_postura_base_codigo_erp` (codigo_erp)

**Restricciones de integridad**:
- `ck_lpb_nonneg_counts`: cantidad_hembras ≥ 0 AND cantidad_machos ≥ 0 AND cantidad_mixtas ≥ 0

---

### 1.2 Entidad: `LotePosturaLevante`
**Tabla**: `public.lote_postura_levante`  
**Propósito**: Lote de postura en etapa Levante. Tabla independiente con datos específicos de levante (distribución por galpón, seguimiento semanal 1-25).

| Campo | Tipo | Nullable | PK/FK | Descripción |
|-------|------|----------|-------|-------------|
| `lote_postura_levante_id` | INT | NO | **PK** | Identificador único del lote levante (auto-incremento) |
| **Ubicación** | | | | |
| `granja_id` | INT | NO | **FK** | ID de la granja |
| `nucleo_id` | VARCHAR(64) | SÍ | **FK** (composite con granja_id) | ID del núcleo dentro de la granja |
| `galpon_id` | VARCHAR(64) | SÍ | **FK** | ID del galpón |
| **Identificación del Lote** | | | | |
| `lote_nombre` | VARCHAR(200) | NO | - | Nombre del lote (ej: K324A, K324B) |
| `lote_id` | INT | SÍ | **FK** | FK a tabla `lotes` (cuando existe registro en lotes) |
| `lote_padre_id` | INT | SÍ | - | ID del lote padre (para lotes derivados) |
| `lote_postura_levante_padre_id` | INT | SÍ | **FK** (self) | Referencia al lote levante padre (relación self-referencial para padres/hijos) |
| **Datos Iniciales de Encasetamiento** | | | | |
| `fecha_encaset` | DATE | SÍ | - | Fecha de encasetamiento del lote |
| `hembras_l` | INT | SÍ | - | Número inicial de hembras |
| `machos_l` | INT | SÍ | - | Número inicial de machos |
| `peso_inicial_h` | DOUBLE PRECISION | SÍ | - | Peso promedio inicial de hembras (kg) |
| `peso_inicial_m` | DOUBLE PRECISION | SÍ | - | Peso promedio inicial de machos (kg) |
| `unif_h` | DOUBLE PRECISION | SÍ | - | Uniformidad inicial de hembras (%) |
| `unif_m` | DOUBLE PRECISION | SÍ | - | Uniformidad inicial de machos (%) |
| **Mortalidad en Caja** | | | | |
| `mort_caja_h` | INT | SÍ | - | Mortalidad en caja de hembras |
| `mort_caja_m` | INT | SÍ | - | Mortalidad en caja de machos |
| **Genética** | | | | |
| `raza` | VARCHAR(80) | SÍ | - | Raza genética (ej: Cobb, Ross) |
| `ano_tabla_genetica` | INT | SÍ | - | Año de la tabla/guía genética |
| `linea` | VARCHAR(80) | SÍ | - | Línea genética |
| `tipo_linea` | VARCHAR(80) | SÍ | - | Tipo de línea (Hembra, Macho, Mixto) |
| `codigo_guia_genetica` | VARCHAR(80) | SÍ | - | Código único de la guía genética |
| `linea_genetica_id` | INT | SÍ | - | ID de la configuración de línea genética |
| **Control y Seguimiento** | | | | |
| `aves_encasetadas` | INT | SÍ | - | Total de aves encasetadas |
| `edad_inicial` | INT | SÍ | - | Edad inicial en días |
| **Aves Actuales** | | | | |
| `aves_h_inicial` | INT | SÍ | - | Hembras vivas al inicio (después de mortalidad en caja) |
| `aves_m_inicial` | INT | SÍ | - | Machos vivos al inicio (después de mortalidad en caja) |
| `aves_h_actual` | INT | SÍ | - | Hembras vivas a la fecha actual |
| `aves_m_actual` | INT | SÍ | - | Machos vivos a la fecha actual |
| **Datos Mixtos** | | | | |
| `mixtas` | INT | SÍ | - | Número de aves mixtas |
| `peso_mixto` | DOUBLE PRECISION | SÍ | - | Peso promedio de aves mixtas (kg) |
| **Metadata** | | | | |
| `tecnico` | VARCHAR(120) | SÍ | - | Nombre del técnico responsable |
| `lote_erp` | VARCHAR(80) | SÍ | - | Código del lote en sistema ERP |
| `estado_traslado` | VARCHAR(50) | SÍ | - | Estado: null, "normal", "trasladado", "en_transferencia" |
| `edad` | INT | SÍ | - | **Edad actual en semanas (1-25)** |
| `estado` | VARCHAR(50) | SÍ | - | Estado general del lote |
| `etapa` | VARCHAR(50) | SÍ | - | Etapa actual ("Levante", "Produccion", etc.) |
| `estado_cierre` | VARCHAR(20) | SÍ | - | "Abierto" (semanas 1-25) o "Cerrado" (semana 26) |
| `empresa_id` | INT | SÍ | - | ID de la empresa (contexto multi-tenancy) |
| `usuario_id` | INT | SÍ | - | ID del usuario propietario |
| `pais_id` | INT | SÍ | - | ID del país |
| `pais_nombre` | VARCHAR(120) | SÍ | - | Nombre del país |
| `empresa_nombre` | VARCHAR(200) | SÍ | - | Nombre de la empresa |
| `regional` | VARCHAR(100) | SÍ | - | Región geográfica |

**Índices**:
- `ix_lote_postura_levante_granja` (granja_id)
- `ix_lote_postura_levante_nucleo` (nucleo_id)
- `ix_lote_postura_levante_galpon` (galpon_id)
- `ix_lote_postura_levante_lote` (lote_id)
- `ix_lote_postura_levante_padre` (lote_postura_levante_padre_id)

**Restricciones de integridad**:
- `ck_lpl_nonneg_counts`: (hembras_l ≥ 0 OR NULL) AND (machos_l ≥ 0 OR NULL) AND (mixtas ≥ 0 OR NULL) AND (aves_encasetadas ≥ 0 OR NULL)
- `ck_lpl_nonneg_pesos`: (peso_inicial_h ≥ 0 OR NULL) AND (peso_inicial_m ≥ 0 OR NULL) AND (peso_mixto ≥ 0 OR NULL)

---

### 1.3 Entidad: `SeguimientoDiario` (Tabla Unificada)
**Tabla**: `public.seguimiento_diario`  
**Propósito**: Tabla unificada de seguimientos diarios para levante, producción, reproductora y engorde. En contexto de Levante, `tipo_seguimiento = 'levante'`.

| Campo | Tipo | Nullable | PK/FK | Descripción |
|-------|------|----------|-------|-------------|
| `id` | BIGINT | NO | **PK** | Identificador único (identity always) |
| **Discriminador y Claves** | | | | |
| `tipo_seguimiento` | VARCHAR(20) | NO | - | Discriminador: 'levante', 'produccion', 'reproductora', 'engorde' |
| `lote_id` | VARCHAR(64) | NO | - | ID genérico del lote (string, para compatibilidad multi-tipo) |
| `lote_postura_levante_id` | INT | SÍ | **FK** | FK a `lote_postura_levante.lote_postura_levante_id` (solo cuando tipo='levante') |
| `lote_postura_produccion_id` | INT | SÍ | - | FK a lote_postura_produccion (solo cuando tipo='produccion') |
| `reproductora_id` | VARCHAR(64) | SÍ | - | ID de reproductora (solo cuando tipo='reproductora') |
| **Fecha y Auditoría** | | | | |
| `fecha` | DATE | NO | - | **Fecha del registro de seguimiento** |
| `created_by_user_id` | VARCHAR(64) | SÍ | - | ID del usuario que creó el registro |
| `created_at` | TIMESTAMP | NO | - | Fecha/hora de creación |
| `updated_at` | TIMESTAMP | SÍ | - | Fecha/hora de última actualización |
| **Mortalidad y Selección** | | | | |
| `mortalidad_hembras` | INT | SÍ | - | Número de hembras muertas en el día |
| `mortalidad_machos` | INT | SÍ | - | Número de machos muertos en el día |
| `sel_h` | INT | SÍ | - | Aves hembra seleccionadas/descartadas en el día |
| `sel_m` | INT | SÍ | - | Aves macho seleccionadas/descartadas en el día |
| `error_sexaje_hembras` | INT | SÍ | - | Aves mal sexadas como hembras |
| `error_sexaje_machos` | INT | SÍ | - | Aves mal sexadas como machos |
| **Consumo de Alimento** | | | | |
| `consumo_kg_hembras` | DECIMAL(12, 3) | SÍ | - | Consumo de alimento de hembras en kg |
| `consumo_kg_machos` | DECIMAL(12, 3) | SÍ | - | Consumo de alimento de machos en kg |
| `tipo_alimento` | VARCHAR(100) | SÍ | - | Tipo/nombre del alimento suministrado |
| **Pesos y Uniformidad (Levante)** | | | | |
| `peso_prom_hembras` | DOUBLE PRECISION | SÍ | - | Peso promedio de hembras (kg) |
| `peso_prom_machos` | DOUBLE PRECISION | SÍ | - | Peso promedio de machos (kg) |
| `uniformidad_hembras` | DOUBLE PRECISION | SÍ | - | Uniformidad de hembras (%) |
| `uniformidad_machos` | DOUBLE PRECISION | SÍ | - | Uniformidad de machos (%) |
| `cv_hembras` | DOUBLE PRECISION | SÍ | - | Coeficiente de variación de hembras |
| `cv_machos` | DOUBLE PRECISION | SÍ | - | Coeficiente de variación de machos |
| **Métricas Nutrimentales (Levante)** | | | | |
| `kcal_al_h` | DOUBLE PRECISION | SÍ | - | Kcal por alimento de hembras |
| `prot_al_h` | DOUBLE PRECISION | SÍ | - | Proteína por alimento de hembras |
| `kcal_ave_h` | DOUBLE PRECISION | SÍ | - | Kcal por ave de hembras |
| `prot_ave_h` | DOUBLE PRECISION | SÍ | - | Proteína por ave de hembras |
| **Agua (Solo Ecuador/Panamá)** | | | | |
| `consumo_agua_diario` | DOUBLE PRECISION | SÍ | - | Consumo de agua en litros |
| `consumo_agua_ph` | DOUBLE PRECISION | SÍ | - | Nivel de pH del agua |
| `consumo_agua_orp` | DOUBLE PRECISION | SÍ | - | Nivel de ORP (mV) del agua |
| `consumo_agua_temperatura` | DOUBLE PRECISION | SÍ | - | Temperatura del agua (°C) |
| **Ciclo y Observaciones** | | | | |
| `ciclo` | VARCHAR(50) | SÍ | - | Tipo de ciclo ("Normal" por defecto) |
| `observaciones` | TEXT | SÍ | - | Observaciones generales del día |
| **Metadata Flexible** | | | | |
| `metadata` | JSONB | SÍ | - | Datos adicionales en formato JSON |
| `items_adicionales` | JSONB | SÍ | - | Ítems adicionales (vacunas, medicamentos) que no son alimentos |

**Restricciones de integridad**:
- `tipo_seguimiento` IN ('levante', 'produccion', 'reproductora', 'engorde')
- Cuando `tipo_seguimiento = 'levante'`, `lote_postura_levante_id` DEBE estar presente

---

## 2. Mapa de Relaciones: El Camino de los Datos

### 2.1 Diagrama Conceptual

```
┌──────────────────────┐
│  LotePosturaBase     │
│  (Maestro/Padre)     │
│                      │
│ lote_postura_base_id │◄──────┐
└──────────────────────┘       │ (Indirecto vía Lote)
                               │
                    ┌──────────┴──────────────────┐
                    │                             │
          ┌─────────▼─────────┐      ┌──────────────────────┐
          │  Lote             │      │  LotePosturaLevante  │
          │  (Intermediario)  │      │  (Hijo/Levante)      │
          │                   │◄─────┤                      │
          │ lote_id (PK)      │      │ lote_id (FK)         │
          │ lote_postura_     │      │ lote_postura_levante_│
          │ base_id (FK)      │      │ padre_id (self-ref)  │
          └─────────┬─────────┘      └──────────────────────┘
                    │
                    │ (FK: lote_id)
                    │
          ┌─────────▼─────────────────────────────────┐
          │    SeguimientoDiario                      │
          │    (Tabla Unificada)                      │
          │                                           │
          │  id (PK)                                  │
          │  lote_id (genérico string)                │
          │  lote_postura_levante_id (FK)             │
          │  tipo_seguimiento = 'levante'             │
          │  fecha, mortalidad, consumo, pesos, etc.  │
          └───────────────────────────────────────────┘
```

### 2.2 Relaciones Detalladas

#### **Relación 1: LotePosturaBase → Lote → LotePosturaLevante**

**Conexión Indirecta (a través de tabla Lote)**:
- `LotePosturaBase.lote_postura_base_id` ← `Lote.lote_postura_base_id` (FK)
- `Lote.lote_id` → `LotePosturaLevante.lote_id` (FK)

**Cardinalidad**:
- 1 LotePosturaBase puede tener N Lotes
- 1 Lote puede tener 1 LotePosturaLevante (relación 1:1 lógica, no física)

**SQL JOIN (para obtener todos los LotePosturaLevante de un LotePosturaBase)**:
```sql
SELECT lpl.*
FROM lote_postura_levante lpl
INNER JOIN lotes l ON lpl.lote_id = l.lote_id
WHERE l.lote_postura_base_id = @lotePosturaBaseId
  AND lpl.etapa = 'Levante';
```

#### **Relación 2: LotePosturaLevante → SeguimientoDiario**

**Conexión Directa (FK)**:
- `LotePosturaLevante.lote_postura_levante_id` ← `SeguimientoDiario.lote_postura_levante_id` (FK)
- Válido solo cuando `SeguimientoDiario.tipo_seguimiento = 'levante'`

**Cardinalidad**:
- 1 LotePosturaLevante tiene N SeguimientoDiario (relación 1:N)
- 1 SeguimientoDiario pertenece a 1 LotePosturaLevante (cuando tipo='levante')

**SQL JOIN (para obtener todos los seguimientos de un lote levante en un rango de fechas)**:
```sql
SELECT sd.*
FROM seguimiento_diario sd
WHERE sd.lote_postura_levante_id = @lotePosturaLevanteId
  AND sd.tipo_seguimiento = 'levante'
  AND sd.fecha BETWEEN @fechaInicio AND @fechaFin
ORDER BY sd.fecha ASC;
```

#### **Relación 3: LotePosturaLevante → LotePosturaLevante (Self-Referential)**

**Conexión Self-Referential (para padres/hijos)**:
- `LotePosturaLevante.lote_postura_levante_padre_id` → `LotePosturaLevante.lote_postura_levante_id` (FK)

**Cardinalidad**:
- 1 LotePosturaLevante padre puede tener N LotePosturaLevante hijos
- 1 LotePosturaLevante hijo pertenece a 1 LotePosturaLevante padre (o NULL si es padre)

**Caso de Uso**: Cuando un lote se divide en múltiples sub-lotes (ej: K324A → K324A-01, K324A-02)

**SQL JOIN (para obtener todos los lotes hijos de un lote padre)**:
```sql
SELECT lpl_hijo.*
FROM lote_postura_levante lpl_hijo
WHERE lpl_hijo.lote_postura_levante_padre_id = @lotePosturaLevantePadreId;
```

---

### 2.3 Flujo Completo: LotePosturaBase → SeguimientoDiario

**Escenario**: Obtener todos los seguimientos diarios de todos los lotes levante que pertenecen a un LotePosturaBase.

```sql
-- QUERY MAESTRA: Consolidar seguimientos desde LotePosturaBase
SELECT 
    lpb.lote_postura_base_id,
    lpb.lote_nombre AS lote_base_nombre,
    lpl.lote_postura_levante_id,
    lpl.lote_nombre AS lote_levante_nombre,
    lpl.granja_id,
    lpl.nucleo_id,
    lpl.galpon_id,
    lpl.raza,
    lpl.ano_tabla_genetica,
    lpl.edad,  -- Semana actual (1-25)
    sd.id AS seguimiento_id,
    sd.fecha,
    sd.mortalidad_hembras,
    sd.mortalidad_machos,
    sd.consumo_kg_hembras,
    sd.consumo_kg_machos,
    sd.peso_prom_hembras,
    sd.peso_prom_machos,
    sd.uniformidad_hembras,
    sd.uniformidad_machos,
    sd.kcal_al_h,
    sd.prot_al_h,
    sd.kcal_ave_h,
    sd.prot_ave_h
FROM lote_postura_base lpb
INNER JOIN lotes l ON lpb.lote_postura_base_id = l.lote_postura_base_id
INNER JOIN lote_postura_levante lpl ON l.lote_id = lpl.lote_id
INNER JOIN seguimiento_diario sd ON lpl.lote_postura_levante_id = sd.lote_postura_levante_id
WHERE lpb.lote_postura_base_id = @lotePosturaBaseId
  AND lpl.etapa = 'Levante'
  AND sd.tipo_seguimiento = 'levante'
  AND sd.fecha BETWEEN @fechaInicio AND @fechaFin
ORDER BY lpl.lote_postura_levante_id, sd.fecha ASC;
```

---

### 2.4 Casos de Uso Específicos

#### **Caso A: Reporte Consolidado (Por LotePosturaBase)**

**Objetivo**: Agrupar datos de TODOS los lotes levante bajo un lote base.

**Lógica**:
1. Identificar `LotePosturaBase` seleccionado
2. Obtener todos los `LotePosturaLevante` (vía tabla `Lote`)
3. Agrupar `SeguimientoDiario` por día/semana
4. Sumar valores absolutos (mortalidad, consumo)
5. Recalcular porcentajes sobre el total unificado

**SQL**:
```sql
SELECT 
    sd.fecha,
    SUM(sd.mortalidad_hembras) AS total_mortalidad_h,
    SUM(sd.mortalidad_machos) AS total_mortalidad_m,
    SUM(sd.consumo_kg_hembras) AS total_consumo_h,
    SUM(sd.consumo_kg_machos) AS total_consumo_m,
    AVG(sd.peso_prom_hembras) AS prom_peso_h,
    AVG(sd.peso_prom_machos) AS prom_peso_m
FROM lote_postura_base lpb
INNER JOIN lotes l ON lpb.lote_postura_base_id = l.lote_postura_base_id
INNER JOIN lote_postura_levante lpl ON l.lote_id = lpl.lote_id
INNER JOIN seguimiento_diario sd ON lpl.lote_postura_levante_id = sd.lote_postura_levante_id
WHERE lpb.lote_postura_base_id = @lotePosturaBaseId
  AND sd.tipo_seguimiento = 'levante'
GROUP BY sd.fecha
ORDER BY sd.fecha ASC;
```

#### **Caso B: Reporte Por Lote de Seguimiento (LotePosturaLevante)**

**Objetivo**: Obtener seguimientos de UN ÚNICO lote levante.

**Lógica**:
1. Recibir `LotePosturaLevanteId`
2. Consultar `SeguimientoDiario` directamente
3. Presentar datos sin agrupación (1 registro por día)

**SQL**:
```sql
SELECT *
FROM seguimiento_diario sd
WHERE sd.lote_postura_levante_id = @lotePosturaLevanteId
  AND sd.tipo_seguimiento = 'levante'
ORDER BY sd.fecha ASC;
```

#### **Caso C: Cruce Genético (Real vs. Esperado)**

**Objetivo**: Comparar métricas reales contra guía genética en la misma semana.

**Lógica**:
1. De `LotePosturaLevante`, extraer: `raza`, `ano_tabla_genetica`, `edad` (semana actual)
2. Traer tabla genética correspondiente (ej: `guia_genetica_ecuador_detalle`)
3. Filtrar por: empresa, raza, año, semana
4. Comparar campos: peso real vs. esperado, uniformidad, consumo, etc.

**SQL (ejemplo simplificado)**:
```sql
SELECT 
    lpl.lote_postura_levante_id,
    lpl.edad AS semana_actual,
    sd.fecha,
    -- Métricas reales
    sd.peso_prom_hembras AS peso_real_h,
    sd.peso_prom_machos AS peso_real_m,
    -- Métricas esperadas (de tabla genética)
    ggd.peso_semana_hembra AS peso_esperado_h,
    ggd.peso_semana_macho AS peso_esperado_m,
    -- Desviación
    (sd.peso_prom_hembras - ggd.peso_semana_hembra) AS desv_peso_h,
    (sd.peso_prom_machos - ggd.peso_semana_macho) AS desv_peso_m
FROM lote_postura_levante lpl
INNER JOIN seguimiento_diario sd ON lpl.lote_postura_levante_id = sd.lote_postura_levante_id
INNER JOIN guia_genetica_ecuador_detalle ggd 
    ON lpl.raza = ggd.raza
    AND lpl.ano_tabla_genetica = ggd.ano
    AND lpl.edad = ggd.semana
WHERE lpl.lote_postura_levante_id = @lotePosturaLevanteId
  AND sd.tipo_seguimiento = 'levante'
ORDER BY sd.fecha ASC;
```

---

## 3. Dependencias entre Campos

### 3.1 Campos Críticos para Filtrado (Cascada)

| Filtro | Tabla | Campo Clave | Depende De | Validación |
|--------|-------|-------------|-----------|------------|
| **Granja** | Farm | granja_id | Usuario en sesión | User.user_farms |
| **Núcleo** | Nucleo | nucleo_id, granja_id | Granja | lote_postura_levante.granja_id |
| **Galpón** | Galpon | galpon_id | Núcleo (lógico) | lote_postura_levante.galpon_id |
| **Lote Base** | lote_postura_base | lote_postura_base_id | (Ninguno) | Debe tener ≥1 lote levante |
| **Lote Levante** | lote_postura_levante | lote_postura_levante_id | Lote Base | lote_id INNER JOIN lotes |
| **Tipo Reporte** | - | - | - | "Consolidado" o "Por Lote" |
| **Periodicidad** | - | - | - | "Diario" o "Semanal" |

### 3.2 Campos Clave para Cálculos

| Cálculo | Campos Requeridos | Tabla | Notas |
|---------|------------------|-------|-------|
| **Edad en Semanas** | `fecha_encaset`, `fecha` actual | lote_postura_levante + seguimiento_diario | DATEDIFF(week, fecha_encaset, fecha) |
| **Mortalidad Acumulada** | `SUM(mortalidad_hembras)`, `SUM(mortalidad_machos)` | seguimiento_diario | Por día y acumulado |
| **Aves Vivas Actual** | `aves_h_inicial - SUM(mortalidad_h)` | lote_postura_levante + seguimiento_diario | Cálculo dinámico |
| **Consumo Promedio Diario** | `AVG(consumo_kg_hembras)` | seguimiento_diario | Por día/semana |
| **Peso Esperado** | `semana, raza, ano_tabla_genetica` | guia_genetica_* | Join con tabla genética |
| **Desviación Peso Real vs Esperado** | `peso_prom_real - peso_esperado` | seguimiento_diario + guia_genetica | Comparativo |

---

## 4. Información Adicional Importante

### 4.1 Tabla Unificada: SeguimientoDiario

La tabla `seguimiento_diario` es una arquitectura **polimórfica** que consolida seguimientos de múltiples tipos:

| Tipo | Descripción | FK Principal | Campos Específicos |
|------|-------------|---------------|--------------------|
| `'levante'` | Levante (semanas 1-25) | lote_postura_levante_id | kcal_al_h, prot_al_h, kcal_ave_h, prot_ave_h |
| `'produccion'` | Producción (huevos) | lote_postura_produccion_id | huevo_tot, huevo_limpio, peso_huevo, etc. |
| `'reproductora'` | Reproductora | reproductora_id | peso_inicial, peso_final |
| `'engorde'` | Pollo de engorde | (ninguno, lote_id solo) | - |

**Nota**: Para Levante, siempre validar `tipo_seguimiento = 'levante'` en las consultas.

### 4.2 Metadata JSONB

Ambas columnas JSONB (`metadata`, `items_adicionales`) permiten almacenar datos flexibles:
- `metadata`: Información adicional del consumo (unidades originales, conversiones, etc.)
- `items_adicionales`: Vacunas, medicamentos u otros ítems no alimenticios

### 4.3 Campos de Agua (Ecuador/Panamá)

Los campos `consumo_agua_*` son específicos de ciertos países:
- `consumo_agua_diario`: Litros
- `consumo_agua_ph`: Nivel de pH (0-14)
- `consumo_agua_orp`: Oxidación-Reducción Potencial (mV)
- `consumo_agua_temperatura`: Grados Celsius

**Validación**: Solo pueden ser NO-NULL en contexto de Ecuador o Panamá.

### 4.4 Estado de Cierre del Lote

`LotePosturaLevante.estado_cierre`:
- `'Abierto'`: Lote activo (semanas 1-25)
- `'Cerrado'`: Lote completado (semana 26). Al cerrar, se crean lotes de producción (hembras/machos)

**Transición**: Cuando `edad >= 26`, el sistema cierra automáticamente el lote y crea registros en `lote_postura_produccion`.

---

## 5. Resumen de Claves Foráneas

| De Tabla | Campo FK | Hacia Tabla | Campo PK | Cardinalidad | Regla Borrado |
|----------|----------|-----------|----------|--------------|---------------|
| lote_postura_levante | granja_id | farms | granja_id | N:1 | RESTRICT |
| lote_postura_levante | nucleo_id, granja_id | nucleos | nucleo_id, granja_id | N:1 | RESTRICT |
| lote_postura_levante | galpon_id | galpones | galpon_id | N:1 | RESTRICT |
| lote_postura_levante | lote_id | lotes | lote_id | N:1 | RESTRICT |
| lote_postura_levante | lote_postura_levante_padre_id | lote_postura_levante | lote_postura_levante_id | N:1 (self) | RESTRICT |
| seguimiento_diario | lote_postura_levante_id | lote_postura_levante | lote_postura_levante_id | N:1 | RESTRICT |
| lote_postura_base | company_id | companies | company_id | N:1 | RESTRICT |
| lotes | lote_postura_base_id | lote_postura_base | lote_postura_base_id | N:1 | RESTRICT |

---

## 6. Notas Arquitectónicas

1. **No hay FK directa entre `LotePosturaBase` y `LotePosturaLevante`**
   - La relación es indirecta a través de la tabla `Lote`
   - Esto permite desacoplamiento y mayor flexibilidad

2. **`SeguimientoDiario` es polimórfica**
   - Usa el patrón "single table inheritance" (discriminador `tipo_seguimiento`)
   - Para Levante, usar siempre `tipo_seguimiento = 'levante'` en filtros

3. **`LotePosturaLevante.edad` debe estar sincronizado**
   - Se calcula como: `DATEDIFF(WEEK, fecha_encaset, fecha_actual)`
   - Debe actualizarse diariamente o en tiempo real

4. **Tabla genética es externa**
   - `guia_genetica_ecuador_*` (y similares) NO están en el diagrama
   - Deben estar en el mismo DB o accedibles via view/query

5. **Multi-tenancy**
   - `LotePosturaBase.company_id` y `LotePosturaLevante.empresa_id` están presentes
   - Siempre filtrar por empresa en queries
