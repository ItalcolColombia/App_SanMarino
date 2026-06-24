# 32 — Informe Semanal Pollo de Engorde (Panamá) — PLAN

> Reporte nuevo: **"Informe Semanal Pollo de Engorde - Panamá"**. Ítem de menú nuevo + migración de menú.
> Patrón de diseño: módulo `indicador-ecuador` (liquidación técnica pollo engorde) — front+back+fn SQL.
> Fuente de verdad del layout: `C:/Users/SAN MARINO/Downloads/Libro3 (1).xlsx` (hoja "1 AL 5 DE MAY DEL 2026").

## 0. Estado / decisiones tomadas

- **Conexión local**: `appsettings.json` (base) apunta a **PROD RDS**; `appsettings.Development.json` apunta a **local** `Host=localhost;Port=5433;Database=sanmarinoapplocal`. ⇒ **Todo comando EF se corre con `ASPNETCORE_ENVIRONMENT=Development`** (ese es el "siempre apunta a otros lados").
- **Tabla de datos reales**: `seguimiento_diario_aves_engorde` (genérica; 3704 filas en local). Las tablas `_panama`/`_ecuador` **no existen** en la BD local; los reportes actuales (indicador-ecuador) también leen la genérica. ⇒ la fn lee la genérica.
- **Ventas**: `movimiento_pollo_engorde` (compartida; `tipo_movimiento IN ('Venta','Despacho','Retiro')`, `estado <> 'Cancelado'`, `deleted_at IS NULL`).
- **Comparación contra "Tabla genética Ecuador"** (`guia_genetica_ecuador_detalle`): **NO se conecta aún** (decisión del usuario: falta definir si el estándar se suma por semana o se toma el valor del día 7). Las columnas "Tabla" salen **NULL placeholder**, con punto de enganche marcado en la fn.
- **Agrupación**: la fn devuelve una fila por **(lote, semana de vida 1..N)**. El front renderiza filas por lote y una fila **CONSOLIDADO** por semana (= "agregado por semana de toda la selección", respuesta del usuario). El Excel muestra ambos.
- **Validación**: los lotes del Excel (DOÑA MARIA, TROFARELLO) son de prod, no están en local. Se valida que los **agregados crudos** de la fn coincidan con SQL manual sobre lotes locales (Lote 31 Panamá id 95; lotes Ecuador). Validación visual: diferida.

## 1. Decodificación del Excel (layout por bloques semanales)

Hoja = una **semana calendario**. Dentro, bloques `SEMANA N.1..5` = **semana de vida** del lote. Cada bloque: filas por lote (granja-galpón-lote) + fila `CONSOLIDADO`.

Columnas por fila:
| Col | Significado | Origen | Tipo |
|---|---|---|---|
| GRANJA (A) | nombre lote (DOÑA MARIA-B-2) | `lote_ave_engorde.lote_nombre` | real |
| AVES (B) | aves del lote | `aves_encasetadas` | real |
| SEMANA (C) | semana de vida | edad/7 (CEIL) | real |
| CONSUMO Tabla (D) | estándar acumulado g/ave | guía genética (`alimento_acumulado_g`) | **TABLA (placeholder)** |
| CONSUMO Real (E) | consumo acumulado g/ave | Σ`consumo_kg`/aves·1000 | real |
| PESO Tabla (F) | peso corporal estándar g | guía (`peso_corporal_g`) | **TABLA** |
| PESO Real (G) | peso promedio g | `peso_prom_*` última del semana | real |
| GANANCIA Tabla (H/I) | peso_tabla(sem)-peso_tabla(sem-1) | guía | **TABLA** |
| GANANCIA Real (I/J) | peso(sem)-peso(sem-1) | derivado | real |
| CONVERSIÓN Tabla (J/K) | `ca` estándar | guía | **TABLA** |
| CONVERSIÓN Real (K/L) | consumo_g_ave/peso_g (=E/G) | derivado | real |
| MORTALIDAD TABLA (L/M) | mort+sel estándar % | guía (`mortalidad_seleccion_diaria`) | **TABLA** |
| MORTALIDAD NATURAL (M/N) | mort semana % | Σmort/saldo·100 | real |
| SELECCIÓN (N/O) | sel semana % | Σsel/saldo·100 | real |
| MORTALIDAD TOTAL (O/P) | natural+selección | derivado | real |
| Peso Llegada (Q, sem1) | peso inicial g | primer peso / `peso_inicial_*` | real |
| VPI (H sem1 / P sem2+) | referencia cruzada a otra semana (otro archivo) | externo | placeholder |
| R/S/T | % Real vs Tabla = (real/tabla)·100 | derivado (requiere Tabla) | placeholder hasta conectar |
| CONSUMO DE AGUA: ML (V), RELACIÓN (W) | agua ml y agua/consumo | `consumo_agua_diario` | real |

**Reglas (derivados):**
- CONSUMO Real (g/ave) = `Σ consumo_kg (encaset..fin semana) / aves_encasetadas · 1000` (acumulado).
- GANANCIA Real = `peso_real(sem) − peso_real(sem−1)`; semana 1 = `peso − peso_llegada`.
- CONVERSIÓN Real = `consumo_g_ave / peso_real_g`.
- MORT/SEL % = `Σ (mort|sel) en la semana / saldo_inicio_semana · 100`. `saldo_inicio_semana = aves_encasetadas − Σ(mort+sel+ventas) de semanas previas`. (DENOMINADOR a confirmar contra cliente: encaset vs saldo vivo — documentar.)
- CONSOLIDADO: AVES=SUM; resto = AVERAGE de las filas reales de la semana.

## 2. Backend / BD

### 2.1 Función SQL `fn_informe_semanal_pollo_engorde`
Archivo canónico: `backend/sql/fn_informe_semanal_pollo_engorde.sql`. Se reusa `fn_seguimiento_diario_engorde(lote)` vía `CROSS JOIN LATERAL` (hereda edad/semana/saldo_aves/consumo/despachos correctos).

Firma:
```
fn_informe_semanal_pollo_engorde(
  p_company_id  INT,
  p_granja_ids  INT[]   DEFAULT NULL,   -- NULL/vacío = todas
  p_nucleo_id   TEXT    DEFAULT NULL,
  p_galpon_id   TEXT    DEFAULT NULL,
  p_lote_id     INT     DEFAULT NULL,
  p_fecha_desde DATE    DEFAULT NULL,   -- semana calendario (overlap)
  p_fecha_hasta DATE    DEFAULT NULL
) RETURNS TABLE (...)  LANGUAGE sql STABLE
```
Columnas de salida (reales + placeholders TABLA NULL): company_id, granja_id, granja_nombre, nucleo_id, galpon_id, galpon_nombre, lote_ave_engorde_id, lote_nombre, fecha_encaset, semana, edad_dia_fin, fecha_inicio_semana, fecha_fin_semana, aves_encasetadas, saldo_inicio_semana, saldo_fin_semana, mort_natural_unid, seleccion_unid, ventas_unid, mort_natural_pct, seleccion_pct, mortalidad_total_pct, consumo_semana_kg, consumo_acum_kg, consumo_real_g_ave, peso_real_g, peso_anterior_g, peso_llegada_g, ganancia_real_g, conversion_real, ventas_kg, edad_venta_prom, agua_ml, relacion_agua, **consumo_tabla_g, peso_tabla_g, ganancia_tabla_g, conversion_tabla, mortalidad_tabla_pct (TODAS NULL placeholder)**, pct_consumo, pct_peso, pct_conversion (NULL hasta conectar tabla).

Lógica: CTE `lotes` (filtros) → `CROSS JOIN LATERAL fn_seguimiento_diario_engorde` → agg por (lote,semana) → window (cum losses, lag peso, cum consumo) → filtro overlap de semana con [desde,hasta] → SELECT final.

### 2.2 Migración EF que envía la fn
`AddFnInformeSemanalPolloEngorde` (Up: `migrationBuilder.Sql(FN_SQL, suppressTransaction:true)`, Down: `DROP FUNCTION`). Crear desde el back con `dotnet ef migrations add` (no editar csproj).

### 2.3 Migración EF de menú
`AddMenu_InformeSemanalPolloEngordePanama` (idempotente: insert en `menus` + `role_menus` + `company_menus` WHERE NOT EXISTS; patrón de `AddMenu_SeguimientoAvesEngordePanama`). Ruta: `/reportes/informe-semanal-engorde-panama` (o bajo parent existente). Hereda roles/empresas del módulo indicador-ecuador / aves-engorde-panama.

### 2.4 Capa C# (delgada)
- `Application/DTOs/InformeSemanalPolloEngordeDtos.cs`: `InformeSemanalRequest`, `InformeSemanalRow` (proyección keyless 1:1 de la fn), `InformeSemanalFilaDto`, `InformeSemanalConsolidadoDto`, `InformeSemanalReporteDto`.
- `Application/Interfaces/IInformeSemanalPolloEngordeService.cs`.
- `Infrastructure/Services/InformeSemanalPolloEngordeService.cs`: arma NpgsqlParameters, `SqlQueryRaw<InformeSemanalRow>("SELECT * FROM fn_informe_semanal_pollo_engorde(...)")`, mapea a Dto, agrupa por semana y arma CONSOLIDADO.
- `API/Controllers/InformeSemanalPolloEngordeController.cs`: `POST api/InformeSemanalPolloEngorde/generar` (body = filtros). Auth + CompanyId de `ICurrentUser`.
- DI en `Program.cs`.

## 3. Frontend (Angular 20 standalone)
`frontend/src/app/features/informe-semanal-engorde/`:
- `models/informe-semanal.model.ts`
- `services/informe-semanal-engorde.service.ts` (contrato == DTO back)
- `pages/informe-semanal-engorde-list/` (component .ts/.html/.scss): filtros granja (multi/all/una), núcleo, galpón, lote, rango de fechas (semana); tabla agrupada por semana con CONSOLIDADO; columnas Tabla mostradas vacías ("—").
- Registrar ruta en `app.config.ts` con la misma ruta del menú.
- Reuso de selectores granja→núcleo→galpón→lote del patrón existente.

## 4. Documentación final
`backend/documentacion/INFORME_SEMANAL_POLLO_ENGORDE_PANAMA.md`: tablas fuente, contrato de la fn, fórmulas, de dónde sale la tabla genética (guia_genetica_ecuador_detalle) y la pregunta abierta (suma por semana vs día 7), y "dónde quedó el proceso" para continuar la integración.

## 5. Casos de prueba (data-correctness)
1. `fn(...)` para lote 95 (Panamá) por semana: Σ mort/sel/consumo == SQL manual sobre `seguimiento_diario_aves_engorde` agrupado por edad/7.
2. Ventas por semana == Σ `movimiento_pollo_engorde` (Venta/Despacho/Retiro) por semana.
3. saldo_fin_semana última == saldo_aves del último día (fn diaria).
4. Filtros: por granja única, por varias (array), todas (NULL), por galpón, por lote.
5. Overlap de fechas: una semana calendario devuelve la semana de vida correcta por lote.
6. Columnas Tabla = NULL; consolidado AVES=SUM, resto AVERAGE.
7. `dotnet build` 0 errores; migración aplica en local (Development) sin error.
