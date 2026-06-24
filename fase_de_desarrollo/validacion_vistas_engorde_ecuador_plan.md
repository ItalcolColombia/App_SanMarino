# Plan — Validación y alineación de vistas Pollo Engorde Ecuador con sus funciones corregidas

> Estado: **PROPUESTA — pendiente de OK del usuario antes de cualquier DDL.**
> Fecha: 2026-06-24

## 1. Contexto y enfoque arquitectónico

Dos módulos de Pollo Engorde tienen su **lógica corregida en funciones SQL** (`fn_*`), pero las
**vistas que consume Power BI** quedaron desfasadas (faltan campos / lógica vieja). El objetivo es
**alinear las vistas a las funciones SIN renombrar** (Power BI ya las referencia por nombre) y
**agregar los campos faltantes**.

### Verdad del esquema (verificado en BD local `sanmarinoapplocal` :5433)
- Tabla con datos: **`seguimiento_diario_aves_engorde`** (3.704 filas).
- `seguimiento_diario_aves_engorde_ecuador` / `_panama`: **NO existen** en la BD (el split de
  `20260517104629` no está materializado; código y funciones usan la tabla base). → **El código manda**:
  no se toca el nombre de tabla; las vistas/funciones siguen leyendo `seguimiento_diario_aves_engorde`.
- Vistas desplegadas (nombres a CONSERVAR): `vw_seguimiento_pollo_engorde`,
  `vw_indicadores_diarios_engorde`, `vw_liquidacion_ecuador_pollo_engorde`
  (registradas en migración `20260531180558_AddMissingDbFunctionsTriggersAndViews`).

## 2. Diferencias encontradas (vista ↔ función corregida)

### 2.1 `vw_seguimiento_pollo_engorde` ↔ `fn_seguimiento_diario_engorde` (v7)
Campos presentes en tabla/función y AUSENTES en la vista:
- `uniformidad_hembras`, `uniformidad_machos`
- `cv_hembras`, `cv_machos`
- `consumo_agua_ph`, `consumo_agua_orp`, `consumo_agua_temperatura`
- `ciclo`
- `historico_consumo_alimento` (jsonb)
- `despacho_peso_neto`, `despacho_peso_tara`, `despacho_promedio_peso_ave` (fix R3.5 peso individual)
- `created_by_user_id`

Lógica: `saldo_alimento_kg_calculado` de la vista ya usa Lindley/M1 (coincide con fn v6).
Diferencia estructural: la fn incluye **días con solo movimiento**; la vista solo filas con seguimiento.
→ Decisión pendiente: ¿la vista debe incluir días movimiento-only? (impacto en Power BI).

### 2.2 `vw_liquidacion_ecuador_pollo_engorde` ↔ `fn_indicadores_pollo_engorde` (R1/R2/R3.1)
Bloque de liquidación AUSENTE en la vista:
- `merma_unidades`, `merma_kilos`, `merma_porcentaje` (R1: NULL si Costos no registró merma)
- `ajuste_aves`, `porcentaje_ajuste`, `produccion_kilo_en_pie`, `total_kilos_despachados_cliente`
- `aves_sobrante`, `dias_engorde`, `ratio_sacrificadas`
- `fecha_alistamiento`, `fecha_inicio_lote`, `fecha_cierre_lote`, `fecha_liquidacion`

Conservar los extras de tiempo real de la vista (no están en fn): `aves_actuales`, `tiene_aves`,
`lote_cerrado_logico`, `cerrado_por_aves_cero`, `cerrado_por_reproductores_vendidos`,
`aves_trasladadas_rep`, `cantidad_lotes_reproductores`, `fecha_cierre_efectiva`.

### 2.3 `vw_indicadores_diarios_engorde` (diario vs guía genética Ecuador)
Ya alineada con su SQL de repo. Sin `fn_` fuente. Pendiente confirmar con el usuario qué corrección
se esperaba (¿desglose por sexo? ¿campos extra de la guía?).

## 3. Cambios de BD / SQL
- **Migración EF idempotente** que hace `CREATE OR REPLACE VIEW` de las vistas en alcance,
  manteniendo nombres y el orden de columnas existente AL INICIO (no romper Power BI), y
  **agregando las columnas nuevas al final**.
- `CREATE OR REPLACE VIEW` falla si cambia el tipo/orden de columnas existentes → si hubiera que
  reordenar, usar `DROP VIEW` + `CREATE` dentro de la misma migración (idempotente con `IF EXISTS`).
- Reaplicar `GRANT SELECT ... TO "usrDWH"` y `OWNER` tras recrear (Power BI/DWH).
- Sin cambios de tabla salvo que falte alguna columna física (no es el caso: todas existen).

## 4. Reglas de negocio a preservar
- Aritmética idéntica a las funciones (mismos `ROUND`, NULLs R1, conversión ajustada 2.7/4.5).
- No alterar columnas existentes que Power BI ya mapea (nombres ni semántica).
- Refactor = sin cambio de comportamiento en lo ya presente; solo se AGREGA.

## 5. Casos de prueba / validación
- Comparar fila a fila vista vs función para un lote conocido (ej. lote con merma registrada y otro sin).
- `SELECT * FROM vw_liquidacion_ecuador_pollo_engorde WHERE lote_ave_engorde_id = X`
  vs `SELECT * FROM fn_indicadores_pollo_engorde(X)` → campos nuevos deben coincidir.
- Seguimiento: comparar `saldo_alimento_kg_calculado`, despachos y nuevas mediciones para 2-3 lotes.
- `cd backend && dotnet build` (0 errores) + `dotnet ef database update` local sin error.

## 6. Pasos (resumen)
1. [ ] Confirmar alcance con el usuario (qué vistas, días movimiento-only, spec de indicadores diarios).
2. [ ] Rebuild `vw_liquidacion_ecuador_pollo_engorde` con bloque merma/ajuste/producción.
3. [ ] Rebuild `vw_seguimiento_pollo_engorde` con mediciones/agua/ciclo/historico/despacho peso individual.
4. [ ] (Si aplica) ajustar `vw_indicadores_diarios_engorde` según spec.
5. [ ] Migración EF idempotente + GRANTs.
6. [ ] Validar en local y reportar diff antes de prod.
