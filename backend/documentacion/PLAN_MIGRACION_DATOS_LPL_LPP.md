# Plan de Migración de Datos: Lote → LPL → LPP

## 1. Contexto

### Antes (estructura legacy)
- **lotes**: tabla única con `fase` = 'Levante' o 'Produccion'. Todo se creaba aquí.
- **seguimiento_lote_levante**: seguimiento diario Levante (LoteId int → lotes).
- **produccion_diaria**: seguimiento diario Producción (LoteId varchar, referenciaba lotes de producción).
- **produccion_lotes** / **produccion_diaria_lote**: catálogo de lotes en producción (LoteId varchar).
- El cambio de estado (Levante → Producción) se hacía manualmente o por lógica de negocio.

### Ahora (estructura LPL/LPP)
- **lote_postura_levante (LPL)**: creado automáticamente por trigger al insertar/actualizar en `lotes`. Levante hasta semana 25.
- **lote_postura_produccion (LPP)**: creado por trigger cuando LPL alcanza semana 26. Se crean sublotes H/M (hembras/machos).
- **seguimiento_diario**: tabla unificada con `tipo_seguimiento` ('levante'|'produccion'|'reproductora'), `lote_postura_levante_id`, `lote_postura_produccion_id`.
- **Triggers**: `trg_lotes_sync_lote_postura_levante`, `trg_lote_postura_levante_cerrar_produccion`.

---

## 2. Flujo de datos actual (nuevo)

1. **Crear lote** → INSERT en `lotes` → trigger crea registro en `lote_postura_levante`.
2. **Seguimiento Levante** → INSERT en `seguimiento_diario` (tipo='levante') con `lote_postura_levante_id`.
3. **Semana 26** → Trigger en LPL detecta edad >= 26 → cierra LPL, crea LPP-H y LPP-M con `aves_h_actual`, `aves_m_actual` del levante.
4. **Seguimiento Producción** → INSERT en `seguimiento_diario` (tipo='produccion') con `lote_postura_produccion_id`.

---

## 3. Datos que permanecen en tablas legacy

| Tabla origen | Dato | Destino |
|--------------|------|---------|
| **lotes** (Fase=Levante) | Lotes en levante sin LPL | **lote_postura_levante** |
| **lotes** (Fase=Produccion) | Lotes en producción sin LPP | **lote_postura_produccion** |
| **seguimiento_lote_levante** | Seguimientos levante sin lote_postura_levante_id | **seguimiento_diario** + link LPL |
| **produccion_diaria** | Seguimientos producción sin seguimiento_diario o sin LPP | **seguimiento_diario** + link LPP |

---

## 4. Cálculo de aves para cierre Levante

Para lotes que ya pasaron semana 25 en el pasado, hay que:

1. **Identificar** lotes en `lotes` con fase='Levante' y edad (por fecha_encaset o edad_inicial) >= 26 semanas.
2. **Calcular aves finales** desde `seguimiento_diario` (tipo='levante') o `seguimiento_lote_levante`:
   - `aves_h_actual` = aves_iniciales_h - SUM(mortalidad_hembras) - SUM(sel_h) - SUM(error_sexaje_hembras)
   - `aves_m_actual` = aves_iniciales_m - SUM(mortalidad_machos) - SUM(sel_m) - SUM(error_sexaje_machos)
3. **Cerrar LPL** con estado_cierre='Cerrado'.
4. **Crear LPP** (H y M) con esos aves como iniciales.

---

## 5. Orden de ejecución de la migración

```
PASO 1: Migrar lotes → lote_postura_levante
        (lotes sin LPL correspondiente)

PASO 2: Vincular seguimiento_diario (tipo=levante) con lote_postura_levante_id
        (usando lote_id de seguimiento_diario → lote_id de LPL)

PASO 3: Para LPL con edad >= 26 que estén Abiertos:
        - Calcular aves_h_actual, aves_m_actual desde seguimiento
        - Cerrar LPL
        - Crear LPP-H y LPP-M

PASO 4: Migrar produccion_diaria → seguimiento_diario (si no está unificado)
        O vincular seguimiento_diario (tipo=produccion) con lote_postura_produccion_id
        (mapeando lote_id varchar → LPP por nombre o por lote_id)

PASO 5: Para lotes legacy (lotes con Fase=Produccion) sin LPP:
        - Crear LPP desde datos del lote
        - Vincular seguimiento_diario con lote_postura_produccion_id

PASO 6: Backfill espejo_huevo_produccion (ya existe script)
```

---

## 6. Consideraciones

### 6.1 Mapeo lote_id (varchar) ↔ LoteId (int)
- `seguimiento_diario.lote_id` es VARCHAR(64).
- `lotes.lote_id` es INT.
- En producción, `produccion_diaria.lote_id` puede ser el **nombre del lote** o un código, no siempre el LoteId numérico.
- Hay que establecer regla: si `lote_id` es numérico → mapear a lotes.LoteId; si es nombre → mapear por lote_nombre.

### 6.2 Sublotes H/M
- El trigger crea **dos** LPP por cada LPL: `{nombre}-H` y `{nombre}-M`.
- En legacy, puede haber un solo lote producción por grupo (ej. "QLK345" sin -H/-M).
- Migración: crear un solo LPP con nombre legacy, o crear H y M según aves_h/aves_m.

### 6.3 Idempotencia
- Los scripts deben poder ejecutarse múltiples veces sin duplicar datos.
- Usar `INSERT ... ON CONFLICT DO NOTHING` o `WHERE NOT EXISTS`.

### 6.4 Volumen
- Ejecutar en transacciones por lotes (batches) para no bloquear la BD.
- Usar `LIMIT` + cursor o procesar por `company_id` / `granja_id`.

---

## 7. Archivos a crear

| Archivo | Descripción |
|---------|-------------|
| `sql/migracion_01_lotes_to_lpl.sql` | INSERT en LPL para lotes sin LPL |
| `sql/migracion_02_seguimiento_levante_link_lpl.sql` | UPDATE seguimiento_diario SET lote_postura_levante_id |
| `sql/migracion_03_cerrar_lpl_crear_lpp.sql` | Cierre LPL y creación LPP para edad>=26 |
| `sql/migracion_04_produccion_diaria_to_seguimiento.sql` | Copiar produccion_diaria → seguimiento_diario (si falta) |
| `sql/migracion_05_seguimiento_produccion_link_lpp.sql` | UPDATE seguimiento_diario SET lote_postura_produccion_id |
| `sql/migracion_06_lotes_produccion_to_lpp.sql` | Crear LPP desde lotes Fase=Produccion legacy |
| `sql/migracion_completa.sql` | Orquestador que ejecuta todos en orden |
