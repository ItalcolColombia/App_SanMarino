# Análisis: Unificación de tablas de Seguimiento Diario (Levante, Producción, Reproductora)

## Objetivo

Evaluar si los tres módulos de seguimiento diario (Levante, Producción, Lote Reproductora) pueden consolidarse en **una sola tabla** con un campo que identifique el tipo (`tipo_seguimiento`: `'levante'` | `'produccion'` | `'reproductora'`), para evitar mantener la misma información en varios lugares.

---

## 1. Estado actual: tres tablas

| Módulo            | Tabla BD                  | Entidad C#                 | Clave natural / FK |
|-------------------|---------------------------|----------------------------|---------------------|
| Seguimiento Levante    | `seguimiento_lote_levante`   | `SeguimientoLoteLevante`   | `LoteId` (int) → lotes |
| Seguimiento Producción | `produccion_diaria`          | `SeguimientoProduccion`    | `LoteId` (text) + Fecha (único por lote+fecha) |
| Seguimiento Reproductora | `lote_seguimientos`       | `LoteSeguimiento`          | `(LoteId, ReproductoraId)` (string) → lote_reproductoras |

---

## 2. Campos que se envían al guardar (por módulo)

### 2.1 Levante (`seguimiento_lote_levante`)

| Campo (BD / DTO)        | Tipo     | Uso |
|-------------------------|----------|-----|
| LoteId                  | int      | FK a lotes |
| FechaRegistro           | DateTime | Fecha del registro |
| MortalidadHembras        | int      | |
| MortalidadMachos        | int      | |
| SelH, SelM               | int      | |
| ErrorSexajeHembras/Machos | int    | |
| ConsumoKgHembras         | double   | |
| ConsumoKgMachos          | double?  | |
| TipoAlimento            | string   | |
| Observaciones            | string?  | |
| KcalAlH, ProtAlH, KcalAveH, ProtAveH | double? | Solo levante |
| Ciclo                    | string   | Normal / Reforzado |
| PesoPromH, PesoPromM     | double?  | |
| UniformidadH, UniformidadM | double? | |
| CvH, CvM                 | double?  | |
| Metadata                 | jsonb    | ítems detalle (itemsHembras/Machos) |
| ItemsAdicionales         | jsonb    | vacunas, medicamentos, etc. |
| ConsumoAguaDiario/Ph/Orp/Temperatura | double? | Agua (Ecuador/Panamá) |

### 2.2 Producción (`produccion_diaria`)

| Campo (BD / DTO)        | Tipo     | Uso |
|-------------------------|----------|-----|
| LoteId                  | string   | (text en BD; viene de ProduccionLote) |
| Fecha                   | DateTime | fecha_registro |
| MortalidadH, MortalidadM | int     | mortalidad_hembras/machos |
| SelH, SelM              | int      | |
| ConsKgH, ConsKgM        | decimal  | consumo kg hembras/machos |
| HuevoTot, HuevoInc      | int      | **Solo producción** |
| HuevoLimpio, HuevoTratado | int    | **Solo producción** |
| HuevoSucio, HuevoDeforme, HuevoBlanco, HuevoDobleYema, HuevoPiso, HuevoPequeno, HuevoRoto, HuevoDesecho, HuevoOtro | int | **Solo producción** |
| TipoAlimento            | string   | |
| Observaciones           | string?  | |
| PesoHuevo               | decimal  | **Solo producción** |
| Etapa                   | int      | 1: 25-33, 2: 34-50, 3: >50 **Solo producción** |
| PesoH, PesoM             | decimal? | Pesaje semanal |
| Uniformidad, CoeficienteVariacion | decimal? | Pesaje (un solo valor por lote) |
| ObservacionesPesaje     | string?  | **Solo producción** |
| Metadata                | jsonb    | |
| ConsumoAgua*             | double?  | Agua (Ecuador/Panamá) |

### 2.3 Reproductora (`lote_seguimientos`)

| Campo (BD / DTO)        | Tipo     | Uso |
|-------------------------|----------|-----|
| LoteId                  | string   | Parte FK |
| ReproductoraId          | string   | **Solo reproductora** (parte FK → lote_reproductoras) |
| Fecha                   | DateTime | |
| PesoInicial, PesoFinal  | decimal? | **Solo reproductora** |
| MortalidadH, MortalidadM | int?    | |
| SelH, SelM, ErrorH, ErrorM | int?   | |
| TipoAlimento            | string?  | |
| ConsumoAlimento         | decimal? | kg hembras |
| ConsumoKgMachos         | decimal? | kg machos |
| Observaciones           | string?  | |
| Ciclo                   | string   | Normal / Reforzado |
| PesoPromH, PesoPromM    | double?  | |
| UniformidadH, UniformidadM | double? | |
| CvH, CvM                | double?  | |
| Metadata                | jsonb    | ítems detalle |
| ItemsAdicionales        | jsonb    | |
| ConsumoAgua*             | double?  | Agua (Ecuador/Panamá) |

---

## 3. Comparación: campos comunes vs exclusivos

### 3.1 Campos comunes a los tres (mismo significado o fácil unificación)

| Concepto unificado     | Levante        | Producción     | Reproductora   |
|------------------------|----------------|----------------|----------------|
| Fecha                  | FechaRegistro  | Fecha          | Fecha          |
| LoteId                 | LoteId (int)   | LoteId (text)  | LoteId (string) |
| Mortalidad hembras     | MortalidadHembras | MortalidadH | MortalidadH    |
| Mortalidad machos      | MortalidadMachos  | MortalidadM | MortalidadM    |
| SelH, SelM             | SelH, SelM     | SelH, SelM     | SelH, SelM     |
| Error sexaje H/M       | ErrorSexajeHembras/Machos | (no)   | ErrorH, ErrorM |
| Consumo kg hembras     | ConsumoKgHembras | ConsKgH      | ConsumoAlimento |
| Consumo kg machos      | ConsumoKgMachos  | ConsKgM       | ConsumoKgMachos |
| TipoAlimento           | TipoAlimento   | TipoAlimento   | TipoAlimento   |
| Observaciones          | Observaciones  | Observaciones  | Observaciones  |
| Peso prom H/M          | PesoPromH/M    | PesoH, PesoM   | PesoPromH/M    |
| Uniformidad            | UniformidadH/M | Uniformidad (1) | UniformidadH/M |
| CV                     | CvH, CvM       | CoeficienteVariacion (1) | CvH, CvM |
| Metadata               | Metadata       | Metadata       | Metadata       |
| ItemsAdicionales       | ItemsAdicionales | (no)        | ItemsAdicionales |
| Consumo agua (4)       | Sí             | Sí            | Sí             |
| Ciclo                  | Ciclo          | (no)          | Ciclo          |

### 3.2 Campos exclusivos por tipo

| Solo LEVANTE           | Solo PRODUCCIÓN                    | Solo REPRODUCTORA   |
|------------------------|------------------------------------|----------------------|
| ErrorSexajeHembras/Machos (si no unificamos nombre) | HuevoTot, HuevoInc              | ReproductoraId       |
| KcalAlH, ProtAlH, KcalAveH, ProtAveH | HuevoLimpio, HuevoTratado, HuevoSucio, … (clasificadora) | PesoInicial, PesoFinal |
|                        | PesoHuevo, Etapa                   |                      |
|                        | ObservacionesPesaje                |                      |
|                        | (ProduccionLoteId al crear – no se guarda en BD como FK en produccion_diaria) | |

Nota: en Producción la “clave” de negocio es LoteId + Fecha (un seguimiento por lote por día). En Reproductora es (LoteId, ReproductoraId, Fecha). En Levante sería (LoteId, Fecha).

---

## 4. Propuesta: tabla única con `tipo_seguimiento`

### 4.1 Idea general

- Una sola tabla, por ejemplo: `seguimiento_diario` (o `seguimiento_diario_unificado`).
- Campo obligatorio: **`tipo_seguimiento`** con valores `'levante'`, `'produccion'`, `'reproductora'`.
- Clave natural según tipo:
  - **Levante:** (tipo_seguimiento, lote_id, fecha).
  - **Producción:** (tipo_seguimiento, lote_id, fecha).
  - **Reproductora:** (tipo_seguimiento, lote_id, reproductora_id, fecha).

### 4.2 Opción A: muchas columnas (todas las actuales, muchas NULL)

- Se unifican nombres (p. ej. mortalidad_hembras, mortalidad_machos, consumo_kg_hembras, consumo_kg_machos, error_sexaje_h, error_sexaje_m, etc.).
- Se añaden todas las columnas específicas de cada tipo; en los registros de otro tipo quedan NULL.
- Ventaja: consultas SQL simples, índices por columna.
- Desventaja: tabla muy ancha, muchos NULL, cambios de modelo afectan a una tabla grande.

Ejemplo mínimo de estructura:

```sql
-- Ejemplo conceptual (no ejecutar tal cual sin revisar tipos y FKs)
CREATE TABLE seguimiento_diario (
  id                    SERIAL PRIMARY KEY,
  tipo_seguimiento      VARCHAR(20) NOT NULL CHECK (tipo_seguimiento IN ('levante','produccion','reproductora')),
  lote_id               VARCHAR(64) NOT NULL,
  reproductora_id       VARCHAR(64) NULL,  -- solo reproductora
  fecha                 TIMESTAMPTZ NOT NULL,

  -- Comunes
  mortalidad_hembras    INT NULL,
  mortalidad_machos     INT NULL,
  sel_h                 INT NULL,
  sel_m                 INT NULL,
  error_sexaje_h        INT NULL,
  error_sexaje_m        INT NULL,
  consumo_kg_hembras    NUMERIC(12,3) NULL,
  consumo_kg_machos     NUMERIC(12,3) NULL,
  tipo_alimento        VARCHAR(100) NULL,
  observaciones        TEXT NULL,
  ciclo                VARCHAR(50) NULL,
  peso_prom_h          DOUBLE PRECISION NULL,
  peso_prom_m          DOUBLE PRECISION NULL,
  uniformidad_h        DOUBLE PRECISION NULL,
  uniformidad_m        DOUBLE PRECISION NULL,
  cv_h                 DOUBLE PRECISION NULL,
  cv_m                 DOUBLE PRECISION NULL,
  consumo_agua_diario  DOUBLE PRECISION NULL,
  consumo_agua_ph      DOUBLE PRECISION NULL,
  consumo_agua_orp     DOUBLE PRECISION NULL,
  consumo_agua_temperatura DOUBLE PRECISION NULL,
  metadata             JSONB NULL,
  items_adicionales    JSONB NULL,

  -- Solo reproductora
  peso_inicial         NUMERIC(10,3) NULL,
  peso_final           NUMERIC(10,3) NULL,

  -- Solo levante
  kcal_al_h            DOUBLE PRECISION NULL,
  prot_al_h            DOUBLE PRECISION NULL,
  kcal_ave_h           DOUBLE PRECISION NULL,
  prot_ave_h           DOUBLE PRECISION NULL,

  -- Solo producción
  huevo_tot            INT NULL,
  huevo_inc            INT NULL,
  huevo_limpio         INT NULL,
  huevo_tratado        INT NULL,
  huevo_sucio          INT NULL,
  huevo_deforme        INT NULL,
  huevo_blanco         INT NULL,
  huevo_doble_yema     INT NULL,
  huevo_piso           INT NULL,
  huevo_pequeno        INT NULL,
  huevo_roto           INT NULL,
  huevo_desecho        INT NULL,
  huevo_otro           INT NULL,
  peso_huevo           DOUBLE PRECISION NULL,
  etapa                INT NULL,
  peso_h               NUMERIC(8,2) NULL,
  peso_m               NUMERIC(8,2) NULL,
  uniformidad          NUMERIC(5,2) NULL,
  coeficiente_variacion NUMERIC(5,2) NULL,
  observaciones_pesaje TEXT NULL,

  UNIQUE (tipo_seguimiento, lote_id, COALESCE(reproductora_id,''), fecha)
);
```

- Para **Reproductora** habría que mantener la FK a `lote_reproductoras (lote_id, reproductora_id)`.
- Para **Producción** se podría seguir resolviendo `LoteId` desde `produccion_lotes` al crear y guardar ese mismo `lote_id` en la tabla unificada (sin FK obligatoria si hoy no la hay).
- Para **Levante** hoy la FK es a `lotes` con `LoteId` int; en la tabla unificada `lote_id` en texto unifica con los otros dos.

### 4.3 Opción B: columnas comunes + JSONB por tipo

- Columnas: `id`, `tipo_seguimiento`, `lote_id`, `reproductora_id` (nullable), `fecha`, más los campos **comunes** (mortalidad, sel, consumo, tipo_alimento, observaciones, ciclo, peso prom, uniformidad, cv, agua, metadata, items_adicionales).
- Un único JSONB, por ejemplo **`datos_especificos`**, donde cada tipo guarda solo lo suyo:
  - **Levante:** KcalAlH, ProtAlH, KcalAveH, ProtAveH, ErrorSexajeHembras/Machos (si no se suben a columna).
  - **Producción:** todos los Huevo*, PesoHuevo, Etapa, ObservacionesPesaje, PesoH, PesoM, Uniformidad, CoeficienteVariacion.
  - **Reproductora:** PesoInicial, PesoFinal.
- Ventaja: tabla más estable, menos columnas y menos NULL.
- Desventaja: consultas por campos específicos de producción/levante/reproductora requieren JSONB (índices GIN si se filtran por esos datos).

---

## 5. Resumen y recomendación

- **Sí es posible** unificar los tres en una sola tabla usando un campo **`tipo_seguimiento`** (`'levante'` | `'produccion'` | `'reproductora'`).
- Los campos que se envían al guardar tienen **muchos comunes** (mortalidad, sel, consumo, tipo alimento, observaciones, peso prom, uniformidad, CV, agua, metadata). Los distintos son sobre todo:
  - **Producción:** huevos (totales, incubables, clasificadora), peso huevo, etapa, pesaje semanal.
  - **Reproductora:** reproductora_id, peso_inicial, peso_final.
  - **Levante:** Kcal/Prot (y opcionalmente error sexaje si no se unifica nombre).

Recomendación práctica:

1. **Diseño:** Opción A si se prioriza simplicidad de consultas e informes por columna; Opción B si se prioriza un esquema más compacto y estable.
2. **Migración:** Script que lea de `seguimiento_lote_levante`, `produccion_diaria` y `lote_seguimientos`, normalice nombres y escriba en `seguimiento_diario` con `tipo_seguimiento` y, si aplica, `reproductora_id`.
3. **API y app:** Mantener los mismos endpoints y DTOs por módulo (Levante, Producción, Reproductora) y en el backend mapear a/desde la tabla unificada según `tipo_seguimiento`, para no cambiar contratos con el frontend.
4. **FK y unicidad:** Definir bien la clave única (tipo + lote_id + [reproductora_id] + fecha) y la FK de reproductora a `lote_reproductoras`; unificar `lote_id` en un solo tipo (p. ej. text) en la tabla unificada.

Si se desea, el siguiente paso puede ser bajar esto a un script SQL concreto (crear tabla + índices + migración de datos) y los cambios mínimos en C# (entidad + configuración EF) para una de las dos opciones (A o B).
