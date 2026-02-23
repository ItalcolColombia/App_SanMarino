# Módulo Lote Postura - Tablas y Configuración

## Resumen

Nueva configuración para lotes postura en el módulo de lote, con tablas independientes para **Levante** y **Producción**, más tabla de auditoría histórica.

## Tablas

### 1. `lote_postura_levante`

Todos los campos de la tabla `lotes` más campos específicos:

| Campo | Descripción |
|-------|-------------|
| `lote_id` | Referencia al lote original (lotes) |
| `lote_padre_id` | Lote padre |
| `lote_postura_levante_padre_id` | Referencia al lote postura levante padre (auto-referencia) |
| `aves_h_inicial` | Aves hembras iniciales |
| `aves_m_inicial` | Aves machos iniciales |
| `aves_h_actual` | Aves hembras actuales |
| `aves_m_actual` | Aves machos actuales |
| `empresa_id` | Empresa |
| `usuario_id` | Usuario |
| `estado` | Estado del lote |
| `etapa` | Etapa |
| `edad` | Edad del lote en días |

### 2. `lote_postura_produccion`

Todos los campos de `lotes` más campos específicos y **clasificación de huevos**:

**Campos específicos:** Igual que levante (`lote_id`, `lote_padre_id`, `lote_postura_levante_id`, `aves_h_inicial`, `aves_m_inicial`, `aves_h_actual`, `aves_m_actual`, `empresa_id`, `usuario_id`, `estado`, `etapa`, `edad`).

**Clasificación huevos:**
- `huevo_tot`, `huevo_inc`
- `huevo_limpio`, `huevo_tratado`
- `huevo_sucio`, `huevo_deforme`, `huevo_blanco`, `huevo_doble_yema`
- `huevo_piso`, `huevo_pequeno`, `huevo_roto`, `huevo_desecho`, `huevo_otro`
- `peso_huevo`

### 3. `historico_lote_postura`

Tabla de auditoría: cada registro creado o actualizado en `lote_postura_levante` o `lote_postura_produccion` genera una entrada aquí.

- `tipo_lote`: 'LotePosturaLevante' | 'LotePosturaProduccion'
- `tipo_registro`: 'Creacion' | 'Actualizacion'
- `snapshot`: JSONB con copia del registro al momento de la operación

Los triggers en BD insertan automáticamente en `historico_lote_postura` en cada INSERT/UPDATE.

## Archivos

| Archivo | Descripción |
|---------|-------------|
| `Entities/LotePosturaLevante.cs` | Entidad C# |
| `Entities/LotePosturaProduccion.cs` | Entidad C# |
| `Entities/HistoricoLotePostura.cs` | Entidad historial |
| `Configurations/LotePosturaLevanteConfiguration.cs` | Configuración EF |
| `Configurations/LotePosturaProduccionConfiguration.cs` | Configuración EF |
| `Configurations/HistoricoLotePosturaConfiguration.cs` | Configuración EF |
| `sql/create_lote_postura_tables.sql` | Script SQL para crear tablas y triggers |

## Despliegue

Ejecutar el script SQL antes de usar el módulo:

```bash
psql -U usuario -d basedatos -f backend/sql/create_lote_postura_tables.sql
```
