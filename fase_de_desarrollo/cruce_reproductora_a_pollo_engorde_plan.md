# Plan: Cruce Automático Seguimiento Reproductora → Seguimiento Pollo Engorde
**Fecha:** 2026-06-02
**Módulo afectado:** Seguimiento Diario Reproductora (origen) → Seguimiento Diario Pollo de Engorde (destino)
**Tipo:** Función + Trigger en BD (PL/pgSQL) + ajustes UI read-only
**Referencia funcional:** `/Users/chelsycardona/Downloads/requerimiento panama 2.xlsx`

---

## ⚠️ TABLA CANÓNICA (documentado 2026-06-02)

**La tabla canónica del Seguimiento Diario Pollo de Engorde es `seguimiento_diario_aves_engorde`.**
Es la única que existe en BD (3.537 registros) y la que usa el módulo real para guardar registros normales de cada lote.

| Entidad EF | Tabla | ¿Existe en BD? | Uso |
|---|---|---|---|
| `SeguimientoDiarioAvesEngorde` | `seguimiento_diario_aves_engorde` | ✅ Sí | **Canónica** — la usa el módulo `features/aves-engorde` vía `SeguimientoAvesEngordeEcuadorService` |
| `SeguimientoDiarioAvesEngordeEcuador` | `seguimiento_diario_aves_engorde_ecuador` | ❌ No existe | Fantasma — 0 referencias |
| `SeguimientoDiarioAvesEngordePanama` | `seguimiento_diario_aves_engorde_panama` | ❌ No existe | Fantasma — solo lo referencia su propio servicio muerto |

> **Regla:** todo lo de Seguimiento Pollo Engorde (incluido este cruce) apunta a **`seguimiento_diario_aves_engorde`**.
> La diferencia por país (mostrar consumo/peso M/H, qq) se resuelve con **filtros de país en el frontend** (storage / `*appShowIfCountry`), NO con tablas separadas.
> Las tablas/entidades `_ecuador` y `_panama` son fantasmas; su limpieza queda pendiente (no se aborda en este plan).

---

## 1. Objetivo

Durante los **primeros 7 días de vida** del lote, el módulo *Seguimiento Diario Pollo de Engorde* **no se digita manualmente**: se **alimenta automáticamente** cruzando/consolidando los seguimientos diarios de los **lotes reproductora** asociados al lote de pollo engorde.

El cruce se implementa como **función + trigger en PostgreSQL** (no en C#), para tener un mapeo claro y centralizado a nivel de datos.

---

## 2. Análisis del Excel (fórmulas exactas)

El archivo modela 2 lotes reproductora (`LOTE 94-1`, `LOTE 94-2`) que alimentan 1 lote pollo engorde (`LOTE 94`).

### Por cada lote reproductora (día N)
| Variable | Origen |
|---|---|
| Machos vivos día N | `B6 = B5 − G5` → inicio − mortalidad machos acumulada |
| Hembras vivas día N | `C6 = C5 − H5` → inicio − mortalidad hembras acumulada |
| Consumo M/H, Mortalidad M/H, Selección M/H, Error sexaje M/H | valores digitados por día |
| Peso prom M/H | valor digitado por día |

### Cruce al pollo engorde (día N) — `LOTE 94`
| Campo destino | Fórmula | Tipo |
|---|---|---|
| Machos | `B23 = B5 + B15` | **SUMA** aves machos de cada lote |
| Hembras | `C23 = C5 + C15` | **SUMA** aves hembras |
| Edad | `D23 = 1` | misma edad (día N) |
| Consumo Machos | `E23 = E5 + E15` | **SUMA** |
| Consumo Hembras | `F23 = F5 + F15` | **SUMA** |
| Mortalidad Machos | `G23 = G5 + G15` | **SUMA** |
| Mortalidad Hembras | `H23 = H5 + H15` | **SUMA** |
| Selección M/H, Error sexaje M/H | suma análoga | **SUMA** |
| **Peso prom Machos** | `M23 = (B5·M5 + B15·M15) / B23` | **PROMEDIO PONDERADO por aves** |
| **Peso prom Hembras** | `N23 = (C5·N5 + C15·N15) / C23` | **PROMEDIO PONDERADO por aves** |

> **La única fórmula que cambia** al pasar de reproductora a pollo engorde es el **peso promedio**, que pasa de valor directo a **promedio ponderado por cantidad de aves vivas**.

---

## 3. Modelo de datos (tablas involucradas)

| Tabla | Rol | Vínculo |
|---|---|---|
| `lote_ave_engorde` | Lote pollo engorde (padre) | PK `lote_ave_engorde_id` |
| `lote_reproductora_ave_engorde` | Lotes reproductora (hijos) | FK `lote_ave_engorde_id` → padre; tiene `fecha_encasetamiento`, `aves_inicio_machos`, `aves_inicio_hembras` |
| `seguimiento_diario_lote_reproductora_aves_engorde` | **Fuente** del cruce | FK `lote_reproductora_ave_engorde_id` |
| **`seguimiento_diario_aves_engorde`** | **Destino del cruce (CANÓNICA)** | FK `lote_ave_engorde_id` |

### Validación de columnas (✅ NO se requiere jsonb)
`seguimiento_diario_aves_engorde` **ya tiene como columnas físicas**:
`consumo_kg_machos`, `consumo_kg_hembras`, `peso_prom_machos`, `peso_prom_hembras`, `mortalidad_machos/hembras`, `sel_m/h`, `error_sexaje_machos/hembras`, `qq_machos/hembras/mixtas`.
→ El consumo M/H separado **NO necesita jsonb**; las columnas existen.

---

## 4. Decisiones de diseño (confirmadas con el usuario)

1. **Clave de cruce = EDAD de vida (día N)**, calculada por cada lote reproductora como `edad = (fecha_registro − fecha_encasetamiento) + 1` (día de encaset = edad 1, coincide con Excel `D=1`).
2. **Registro unificado = solo lectura / automático.** El pollo engorde no se digita esos 7 días; se regenera desde el cruce. Para corregir se edita el lote reproductora.
3. **Inventario y descuento de aves = solo en reproductora.** El cruce NO vuelve a descontar inventario ni aves; es espejo informativo.

---

## 5. Regla de sincronización multi-lote

Para una **edad N** del lote pollo engorde:
- **Si tiene 2+ lotes reproductora:** el registro unificado solo se genera cuando **TODOS** los lotes reproductora tienen seguimiento para la edad N. Si falta alguno → no se genera (y si existía, se elimina).
- **Si tiene 1 solo lote reproductora:** se copia directo (el promedio ponderado de 1 elemento = el mismo peso).
- **Solo edades 1..7.** Edades > 7 no se cruzan.

---

## 6. Arquitectura de la solución

### 6.1 Cambio de schema (migración EF idempotente)
Marca para distinguir registros generados por cruce de los manuales, en la tabla canónica:
```sql
ALTER TABLE seguimiento_diario_aves_engorde
  ADD COLUMN IF NOT EXISTS origen_cruce boolean NOT NULL DEFAULT false;
```
- `origen_cruce = true` → registro generado por el trigger (read-only en UI, regenerable).
- Detalle de trazabilidad (lotes origen, edad) en la columna `metadata` (jsonb) existente:
  `{ "origenCruce": true, "edad": N, "lotesReproductora": [id1,id2], "fechasOrigen": [...] }`

### 6.2 Función PL/pgSQL: `fn_cruce_reproductora_a_engorde(p_lote_ave_engorde_id int)`
Reprocesa **todas las edades 1..7** del lote pollo engorde:

```
n_lotes := COUNT(lotes reproductora del lote_ave_engorde)
SI n_lotes = 0 → RETURN

PARA cada edad d EN 1..7:
  regs := seguimientos de los lotes reproductora cuya edad = d
          (edad = fecha_registro - fecha_encasetamiento + 1)
  n_con_registro := COUNT(DISTINCT lote_reproductora_id en regs)

  SI n_con_registro = n_lotes:   -- todos alineados
     -- aves vivas por lote al inicio del día d:
     --   aves_m_lote = aves_inicio_machos − Σ(mort_m+sel_m+err_m) días 1..d-1
     --   aves_h_lote = aves_inicio_hembras − Σ(mort_h+sel_h+err_h) días 1..d-1
     machos      := Σ aves_m_lote
     hembras     := Σ aves_h_lote
     consumo_m   := Σ consumo_kg_machos(d)
     consumo_h   := Σ consumo_kg_hembras(d)
     mort_m/h, sel_m/h, err_m/h := Σ por categoría
     peso_prom_m := Σ(aves_m_lote · peso_prom_m_lote(d)) / NULLIF(machos,0)
     peso_prom_h := Σ(aves_h_lote · peso_prom_h_lote(d)) / NULLIF(hembras,0)
     fecha_destino := fecha_encaset(lote_ave_engorde) + (d-1)

     UPSERT seguimiento_diario_aves_engorde
        (lote_ave_engorde_id, fecha=fecha_destino, …, origen_cruce=true)
  SINO:
     DELETE FROM seguimiento_diario_aves_engorde
       WHERE lote_ave_engorde_id = p_lote_ave_engorde_id
         AND origen_cruce = true AND <edad = d>
```

> **Índice único** para el UPSERT: `(lote_ave_engorde_id, fecha) WHERE origen_cruce`, o columna `edad` + unique `(lote_ave_engorde_id, edad) WHERE origen_cruce`.

### 6.3 Trigger
```sql
CREATE TRIGGER trg_cruce_reproductora_engorde
AFTER INSERT OR UPDATE OR DELETE
ON seguimiento_diario_lote_reproductora_aves_engorde
FOR EACH ROW EXECUTE FUNCTION trg_fn_cruce_reproductora_engorde();
```
La función trigger resuelve el `lote_ave_engorde_id` del lote reproductora afectado (NEW/OLD) y llama `fn_cruce_reproductora_a_engorde(lote_ave_engorde_id)`.

---

## 7. Fórmula de aves vivas (a confirmar en implementación)

El Excel resta **solo mortalidad** (`B6=B5−G5`). El resto del sistema (`CalcularHembrasVivasAsync`) resta **mortalidad + selección + error sexaje**.
**Recomendación:** restar todos los retiros (mort + sel + error) para consistencia. Las columnas sel/err en el Excel están en 0, por lo que ambos criterios dan igual en el ejemplo. ✅ A confirmar con usuario.

---

## 8. Cambios por componente

| Componente | Cambio |
|---|---|
| **Migración EF** | Agregar `origen_cruce` a `seguimiento_diario_aves_engorde` (idempotente). |
| **SQL `/backend/sql/`** | Script con `fn_cruce_reproductora_a_engorde` + función trigger + trigger. Idempotente (`CREATE OR REPLACE`). |
| **Backend C# (lectura)** | `SeguimientoAvesEngordeEcuadorService` / `SeguimientoAvesEngordeService`: exponer `origen_cruce` en el DTO; bloquear create/update/delete manual cuando `origen_cruce=true` o edad ≤ 7. |
| **Frontend lista** (`features/aves-engorde`) | Mostrar consumo M/H y peso prom M/H **según país (storage)**; marcar filas de cruce como read-only, badge "Automático". |
| **Frontend modal** | Deshabilitar alta manual para edades 1–7 (se llenan por cruce). |

---

## 9. Casos borde

1. **Editar un seguimiento reproductora ya cruzado** → trigger regenera la edad afectada.
2. **Eliminar un seguimiento reproductora** → si rompe la alineación, el registro unificado de esa edad se borra.
3. **Lote con 1 reproductora** → copia directa.
4. **Encasetamientos distintos entre lotes** → cruce por EDAD (no fecha); día-N alinea con día-N. La `fecha` destino se deriva del encaset del lote pollo engorde.
5. **Edad > 7** → ignorada por el cruce; el pollo engorde vuelve a ser digitable normal desde día 8 (a confirmar).
6. **Doble descuento** → evitado: el trigger escribe directo a la tabla destino, sin pasar por el servicio C# de inventario/aves.

---

## 10. Plan de pruebas

- **T1** 1 lote reproductora, 7 días → 7 registros unificados = copia directa.
- **T2** 2 lotes reproductora alineados → sumas + peso ponderado (validar contra Excel: M23=56, E23=362, etc.).
- **T3** 2 lotes, falta el día 3 de uno → no se genera día 3; al completarlo, aparece.
- **T4** Editar mortalidad en reproductora → recalcula machos vivos y peso ponderado.
- **T5** Eliminar un registro reproductora → desaparece el día unificado.
- **T6** Verificar que inventario/aves NO se descuentan dos veces.

---

## 11. Riesgos

- **Recálculo en trigger por fila:** función liviana (7 edades, pocos lotes); aceptable. Alternativa futura: statement-level.
- **Entidades fantasma `_ecuador`/`_panama`:** no afectan el cruce (apunta a la canónica), pero siguen ensuciando el modelo EF. Limpieza pendiente (fuera de alcance).
- **Multi-tenant:** la función respeta el aislamiento vía la relación de lotes (`lote_ave_engorde.company_id`).

---

## 12. Entregables

1. `backend/sql/fn_cruce_reproductora_a_engorde.sql` (función + trigger, idempotente).
2. Migración EF `AddOrigenCruceToSeguimientoEngorde`.
3. Ajustes servicio lectura + DTO (`origen_cruce`, bloqueo manual edades 1–7).
4. Ajustes frontend `aves-engorde` (columnas M/H por país, read-only cruce).
5. Pruebas T1–T6 documentadas.
