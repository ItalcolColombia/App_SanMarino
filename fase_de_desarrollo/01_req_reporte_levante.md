# Requerimiento: Reporte Técnico Levante

## Contexto Técnico
Módulo: Reporte Técnico San Marino.

Fase Activa: Levante (Semanas 1 a 25).

Entidades Principales Identificadas:
- `lote_postura_base` (Padre)
- `lote_postura_levante` (Hijo/Seguimiento)
- Tabla Genética

Objetivo: Unificar la lógica de filtros y consolidación de datos comparando el seguimiento diario/semanal contra la guía genética.

## I. Lógica de Filtros (Cascada y Dependencia)
Los filtros deben evaluarse en el backend para retornar listas validadas:

- Granja: Asociadas al ID del usuario en sesión.
- Núcleo: Depende de la Granja.
- Galpón: Depende del Núcleo.
- Lote Base (`lote_postura_base`):
  - Regla: Solo retorna los lotes base que tengan lotes hijos (`lote_postura_levante`) asignados al Galpón seleccionado.
  - Etapa: Fija en "Levante" para esta fase.
- Tipo de Reporte: "Consolidado" o "Por Lote de Seguimiento".
- Lote de Seguimiento (`lote_postura_levante`):
  - Regla: Solo se habilita si el tipo de reporte es "Por Lote de Seguimiento".
  - Muestra los lotes hijos asociados al Lote Base en la etapa de Levante.
- Periodicidad: "Diario" o "Semanal". Define la agrupación temporal de salida.

## II. Motor de Cálculo y Agrupación
Dependiendo del Tipo de Reporte, el servicio ejecutará dos caminos:

### Camino A: Consolidado (Por Lote Base)
- Identifica el `lote_postura_base`.
- Obtiene todos los IDs de `lote_postura_levante` asociados.
- Consulta el seguimiento diario de todos esos IDs.
- Agrupa por día/semana sumando valores absolutos.
- Recalcula porcentajes sobre el total unificado del lote base.

### Camino B: Por Lote de Seguimiento
- Recibe el ID directo de `lote_postura_levante`.
- Consulta su seguimiento diario/semanal de forma aislada.

## III. Cruce Genético (Levante)
- Identificar la Raza y el Año en la información del lote en encasetamiento.
- Traer la tabla genética correspondiente a esa combinación.
- Determinar la "Edad en Semanas" del lote (Semana 1 a 25).
- Realizar el Join de los datos procesados con la Guía Genética en esa misma semana.
- Mostrar la comparación: Real vs. Esperado.

## IV. Diccionario de Datos y Mapeo Relacional

> **Referencia**: Análisis arquitectónico completo disponible en [`diccionario_datos_levante.md`](./diccionario_datos_levante.md)

### Resumen Ejecutivo del Mapeo

#### Entidades Principales (3)

1. **`lote_postura_base`** — Registro maestro que agrupa lotes
   - PK: `lote_postura_base_id`
   - Campos: nombre, cantidades (H/M/mixtas)
   - Auditoría: company_id, created_at, updated_at

2. **`lote_postura_levante`** — Lote en fase Levante (semanas 1-25)
   - PK: `lote_postura_levante_id`
   - FK: `lote_id` → tabla `lotes`
   - FK: `lote_postura_levante_padre_id` (self-referential)
   - Campos clave: `fecha_encaset`, `raza`, `ano_tabla_genetica`, `edad` (semanas)
   - Estados: `estado`, `etapa`, `estado_cierre` ("Abierto" ó "Cerrado")

3. **`seguimiento_diario`** (tabla unificada) — Seguimientos diarios (levante, producción, reproductora)
   - PK: `id` (BIGINT)
   - FK: `lote_postura_levante_id` → `lote_postura_levante` (solo si `tipo_seguimiento = 'levante'`)
   - Campos: `fecha`, `mortalidad_h/m`, `consumo_kg_h/m`, `peso_prom_h/m`, `uniformidad_h/m`, `kcal_al_h`, `prot_al_h`, etc.

#### Camino de Relaciones (JOINs)

**Ruta principal**: `LotePosturaBase` → (vía `Lote`) → `LotePosturaLevante` → `SeguimientoDiario`

```sql
-- Obtener todos los seguimientos de un LotePosturaBase
SELECT sd.*
FROM lote_postura_base lpb
INNER JOIN lotes l ON lpb.lote_postura_base_id = l.lote_postura_base_id
INNER JOIN lote_postura_levante lpl ON l.lote_id = lpl.lote_id
INNER JOIN seguimiento_diario sd ON lpl.lote_postura_levante_id = sd.lote_postura_levante_id
WHERE lpb.lote_postura_base_id = @id
  AND lpl.etapa = 'Levante'
  AND sd.tipo_seguimiento = 'levante'
  AND sd.fecha BETWEEN @fechaInicio AND @fechaFin
ORDER BY lpl.lote_postura_levante_id, sd.fecha;
```

**Ruta directa**: `LotePosturaLevante` → `SeguimientoDiario`

```sql
-- Obtener seguimientos de un único lote levante
SELECT sd.*
FROM seguimiento_diario sd
WHERE sd.lote_postura_levante_id = @lotePosturaLevanteId
  AND sd.tipo_seguimiento = 'levante'
ORDER BY sd.fecha;
```

#### Tabla Unificada: SeguimientoDiario

La tabla `seguimiento_diario` es **polimórfica** (discriminador `tipo_seguimiento`):
- `'levante'` → FK a `lote_postura_levante_id`, campos: `kcal_al_h`, `prot_al_h`, `kcal_ave_h`, `prot_ave_h`
- `'produccion'` → FK a `lote_postura_produccion_id`, campos: `huevo_tot`, `huevo_limpio`, etc.
- `'reproductora'` → FK a `reproductora_id`
- `'engorde'` → sin FK específica

**Nota crítica**: Siempre filtrar por `tipo_seguimiento = 'levante'` en queries para Levante.

#### Campos Críticos por Caso de Uso

| Caso de Uso | Tabla Principal | Filtros Clave | Cálculos |
|-------------|-----------------|---------------|----------|
| **Consolidado (por LotePosturaBase)** | lote_postura_levante | granja_id, galpon_id, lote_postura_base_id | SUM(mortalidad), AVG(peso), porcentajes |
| **Por Lote Levante** | seguimiento_diario | lote_postura_levante_id, fecha | Datos directos sin agrupación |
| **Cruce Genético** | seguimiento_diario + guia_genetica_* | edad (semana), raza, ano_tabla_genetica | Real vs. Esperado |

**Documento detallado**: Para tablas completas de campos, índices, restricciones y casos SQL complejos, consultar `diccionario_datos_levante.md`.
