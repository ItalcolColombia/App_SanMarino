# Plan — Reporte Técnico Semanal Postura (Sanmarino): Levante + Producción vs Guía Genética

**Fecha:** 2026-07-26 · **Empresa objetivo:** Agroavícola Sanmarino (Colombia, company_id=1 en prod; habilitación por menú, sin hardcodes de empresa en código).

## 1. Objetivo

Un **solo módulo** nuevo "Reporte Técnico Semanal" con **dos opciones** (Levante / Producción) que replica los dos reportes semanales oficiales de Sanmarino:

- `Técnico Levante A382.xlsx` → hoja **"Resumen Semanal Galpón de Levante"**: filas = semanas de edad 1–25, bloque **Hembras** (mortalidad, descarte, error sexaje, M+D% guía, retiro total acum real vs guía, alimento sem kg / acum kg / gr-ave-día real vs guía / incremento / acumulado gr-ave real vs guía, peso real vs guía + ganancia + %desv, uniformidad real vs guía + CV, nutrición kcal/%prot + acumulados/ave) y bloque **Machos** (igual sin uniformidad/nutrición). Una hoja por galpón + hojas "Gral" consolidadas por sublote (A382-A) y lote (A382).
- `TEC. PRODUCCION- A346.xlsx` → hoja **"Resumen Semanal de Producción"**: filas = semanas 25–56+, aves H/M + apareo M:H, mortalidad-descarte H y M vs guía (%sem, %acum), producción total de huevos (sem, acum, H.T.A.A vs guía, %a/d vs guía), huevos incubables (sem, acum, %HI vs guía, H.I.A.A vs guía), alimento H y M (sem kg, acum, gr/a/d vs guía, increm), conversión gr/HI, peso huevo real vs estándar + masa lote vs masa guía, peso corporal H y M vs guía + %desv + uniformidad, relación M/H, infertilidad guía, nacimientos (guía; reales fase 2), acumulado pollitos. Una hoja por galpón + consolidadas por sublote y "Gral".

Todo agregado por **semana de vida** desde los seguimientos diarios, comparado contra `guia_genetica_sanmarino_colombia` (raza + año tabla genética + company + semana).

## 2. Decisiones de arquitectura

1. **Patrón fn SQL + service delgado** (patrón moderno del repo, referencia `fn_informe_semanal_pollo_engorde` + `InformeSemanalPolloEngordeService`). ⛔ No replicar el patrón legacy LINQ de `ReporteTecnicoService` (3.110 líneas).
2. **Reutilizar las fns canónicas existentes** `fn_indicadores_levante_postura(p_lote_id)` y `fn_indicadores_produccion_postura(...)` SIN tocarlas (no cambiar su firma: otras pantallas dependen). Lo que falta se agrega con **fns hermanas** (patrón `fn_clasificacion_huevo_items_produccion`: mismos params, misma resolución de lote, misma fórmula de semana, mismas fuentes, solo CTEs sin TEMP TABLE):
   - `fn_reporte_semanal_levante_extras(p_lote_id int)` → por semana 1–25: fecha_fin_semana, conteos y kg POR SEXO (mort/sel/err sem y acum, consumo_kg sem y acum H/M), consumo acumulado gr/ave real H/M, kcal/prot reales (kcal_al_h, prot_al_h del seguimiento; kcal/prot ave acum), y columnas de guía no expuestas por la fn existente: `retiro_ac_hembras_guia`, `retiro_ac_machos_guia`, `consumo_acum_hembras_guia`, `consumo_acum_machos_guia`, `mortalidad_sem_machos_guia`, `kcal_sem_hembras_guia`, `prot_sem_hembras_guia`, `alim_hembras_guia`, `kcal_acum_hembras_guia`, `prot_acum_hembras_guia`, `aves_machos_inicio/fin_semana`.
   - `fn_reporte_semanal_produccion_extras(p_company_id, p_lote_postura_produccion_id, p_lote_id, p_semana_desde, p_semana_hasta, p_fecha_desde, p_fecha_hasta)` → por semana ≥25: fecha_fin_semana, consumo gr/a/d H y M real (la fn existente da kg; el gr/a/d guía = `gr_ave_dia_h/m`), conversión gr/HI real (=(consH+consM)*1000/huevos_inc) y guía (`gr_huevo_inc`), masa lote real (=%prod * peso_huevo/100) y guía (`masa_huevo`), apareo real (avesM/avesH*100) y guía (`apareo`), `%HI` acumulado real y guía (`aprov_ac`), infertilidad guía (`grasa_porcentaje` NO — es `100-nacim`? NO: usar columna guía específica si existe; ver §6), nacimiento guía (`nacim_porcentaje`), pollitos por ave guía (`pollito_aa`), producción acumulada (huevos tot/inc acumulados), `%HI` semanal ya existe en fn base (verificar; si no, emitirlo).
3. **Consolidación** (por sublote/lote base y "Gral") en C# con clase **pura** `Application/Calculos/ReporteTecnicoSemanalCalculos.cs` + tests xUnit: conteos/kg/huevos = SUMA entre galpones; pesos/uniformidad/CV/peso huevo = PROMEDIO SIMPLE entre galpones con dato (igual que las hojas "Gral" del Excel, verificado en sus fórmulas `SUM(...)/3`).
4. **Resolución de sublotes**: lote base (`lote_postura_base_id`) → `lotes` / `lote_postura_levante` / `lote_postura_produccion` (mismo camino que `ReporteTecnicoLevanteFilterDataService`). El service llama las fns 1 vez por sublote (pocos galpones por lote; la fn de levante usa TEMP TABLE y no es LATERAL-izable) y arma tabs + consolidado.
5. **Empresa efectiva fail-closed**: `ICurrentUser.CompanyId` (ya pisado por `ActiveCompanyMiddleware`) + recorte de granjas por `ILocationScopeResolver` (patrón `InformeSemanalPolloEngordeService` líneas 66-80).
6. **Habilitación solo Sanmarino** = menú (regla 5 CLAUDE.md): migración seed idempotente que crea el ítem bajo el grupo "Reportes" y lo inserta en `company_menus` SOLO para `name='Agroavicola Sanmarino'` y en `role_menus` SOLO para roles de esa empresa que ya tengan el hermano `/reportes-tecnicos`. **Sin flag nuevo en `companies`** (es un módulo on/off, no un cambio de comportamiento; mismo criterio que Informe Semanal Panamá).
7. **Export Excel en el FRONT** con `exportarAoaMultiHojaExcel` (patrón moderno; NO agregar un cuarto *ExcelService EPPlus): una hoja por galpón + hoja consolidada, cabeceras de 2 niveles armadas en `funciones/construir-aoa-*.funcion.ts` (funciones puras).
8. **Filtros**: reutilizar los filter-data existentes (`GET api/ReporteTecnico/levante/filter-data` para levante y el de producción) con el diseño global `filter-card` / `filter-steps`.

## 3. Backend — archivos

| Acción | Archivo |
|---|---|
| NUEVO | `backend/sql/fn_reporte_semanal_levante_extras.sql` (spec) |
| NUEVO | `backend/sql/fn_reporte_semanal_produccion_extras.sql` (spec) |
| NUEVO | Migración EF `AddFnsReporteTecnicoSemanalPostura` (Up = DROP IF EXISTS + CREATE de ambas fns; Down = DROP) |
| NUEVO | Migración EF `AddMenuReporteTecnicoSemanal` (data-only, Designer clonado; seed menú + company_menus/role_menus SOLO Sanmarino) |
| NUEVO | `Application/DTOs/ReporteTecnicoSemanal/` → `ReporteSemanalLevanteExtrasRow.cs`, `ReporteSemanalProduccionExtrasRow.cs` (rows fn, snake_case exacto), `ReporteTecnicoSemanalDtos.cs` (request + semana levante/producción + tab por galpón + consolidado + header info) |
| NUEVO | `Application/Interfaces/IReporteTecnicoSemanalService.cs` |
| NUEVO | `Application/Calculos/ReporteTecnicoSemanalCalculos.cs` (puro: zip fila base+extras → DTO semana; consolidación multi-galpón; incrementos guía; derivadas) |
| NUEVO | `Infrastructure/Services/ReporteTecnicoSemanal/ReporteTecnicoSemanalService.cs` (ancla) + `Funciones/ReporteTecnicoSemanalService.Levante.cs` + `Funciones/ReporteTecnicoSemanalService.Produccion.cs` |
| NUEVO | `API/Controllers/ReporteTecnicoSemanalController.cs` — `[Authorize]`, `POST api/ReporteTecnicoSemanal/levante` y `POST api/ReporteTecnicoSemanal/produccion` (request: LotePosturaBaseId, opcional GranjaId/GalponId/rangos) |
| EDIT | `API/Program.cs` → registrar `IReporteTecnicoSemanalService` |
| NUEVO | `backend/tests/ZooSanMarino.Application.Tests/ReporteTecnicoSemanalCalculosTests.cs` |

### Reglas críticas de las fns nuevas
- Semana levante: `floor((fecha_bogota - fecha_encaset)/7)+1`, `LEAST(25, ...)`, cero filas si encaset NULL/futuro, excluir filas de puro traslado post-25 (copiar de `fn_indicadores_levante_postura`).
- Semana producción: `((reg_date - enc_date)/7)+1` desde encaset ORIGINAL, descartar <25, UNION ALL `seguimiento_diario_levante` (tipo='produccion') + `seguimiento_diario_produccion` con `DISTINCT ON` día Bogotá (gana el más temprano) (copiar de `fn_indicadores_produccion_postura`).
- Guía: join por company + raza (btrim/lower) + `anio_guia = ano_tabla_genetica::text` + edad=semana; TODO casteado con `NULLIF(btrim(x),'')` / `f_safe_numeric`; producción semana 25 → guía NULL (no romper).
- Nombres de columnas de salida = snake_case EXACTO de las props del DTO (`..._hembras`, no `_h`).
- Acumulados = bajas_acumuladas / base inicial * 100 (nunca suma de % semanales). Saldo: `aves_fin = aves_ini - mort - sel - err + tras_in - tras_out`.

## 4. Frontend — archivos

```
frontend/src/app/features/reporte-tecnico-semanal/
├── models/reporte-tecnico-semanal.model.ts     # espejo TS de los DTOs
├── services/reporte-tecnico-semanal.service.ts # POST levante / produccion + filter-data reuse
├── funciones/
│   ├── README.md
│   ├── construir-aoa-levante.funcion.ts        # AOA multi-hoja (por galpón + Gral) formato Excel
│   └── construir-aoa-produccion.funcion.ts
└── pages/reporte-tecnico-semanal-main/
    ├── reporte-tecnico-semanal-main.component.ts / .html / .scss
```
- Pantalla: `filter-card` con cascada Granja→Núcleo→Galpón(opcional)→Lote Base + toggle **Levante | Producción** + Generar/Limpiar/Exportar.
- Resultado: tabs (Consolidado + un tab por galpón/sublote), tabla semanal con cabecera agrupada de 2 niveles (bloques Hembras/Machos en levante; bloques del Excel de producción), filas real con columnas guía adyacentes; `ChangeDetectionStrategy` con ViewModels precalculados (sin getters que alocan — NG0103).
- Ruta `'/reporte-tecnico-semanal'` en `app.config.ts` (`loadComponent`, `canActivate:[authGuard]`), ANTES del `**`.
- Export: `exportarAoaMultiHojaExcel` — 1 hoja por galpón + "Gral", nombre `Reporte_Tecnico_Semanal_{Levante|Produccion}_{lote}_{yyyymmdd}.xlsx`.

## 5. Migración de menú (data-only)

- INSERT `menus` bajo grupo Reportes (`key='reporte' OR label ILIKE 'Reportes'`), `route='/reporte-tecnico-semanal'`, `label='Reporte Técnico Semanal'`, `icon='chart-bar'` (existe en ICON_MAP), idempotente por route.
- `company_menus`: SOLO empresa `Agroavicola Sanmarino` (lookup por name, jamás id fijo).
- `role_menus`: SOLO roles de Sanmarino (join `role_companies` × `companies.name='Agroavicola Sanmarino'`) que ya tengan `/reportes-tecnicos`.
- Down simétrico (company_menus → role_menus → menus por route).

## 6. Reglas de negocio / mapeo guía

| Métrica Excel | Real (fuente) | Guía (columna `guia_genetica_sanmarino_colombia`) |
|---|---|---|
| M+D % guía (levante K) | — | `mort_sem_h` / `mort_sem_m` |
| Retiro total acum % (levante O/P) | (mort+sel+err) acum / aves iniciales | `retiro_ac_h` / `retiro_ac_m` |
| gr/ave/día alimento | consumo_kg*1000/(aves*7) | `gr_ave_dia_h` / `gr_ave_dia_m` |
| Acumulado gr/ave | consumo_kg_acum*1000/aves | `cons_ac_h` / `cons_ac_m` |
| Peso / Uniformidad | último pesaje de la semana (arrastre) | `peso_h`/`peso_m` (÷1000 si aplica), `uniformidad` |
| Nutrición (levante AF-AI) | `kcal_al_h`,`prot_al_h` + acumulados | `alim_h`,`kcal_sem_h`,`prot_h_sem` |
| H.T.A.A / H.I.A.A | huevos acum / aves alojadas | `h_total_aa` / `h_inc_aa` |
| % producción a/d | huevos_día/hembras vivas*100 | `prod_porcentaje` |
| % H.I. | inc/tot*100 (sem) y acum | `aprov_sem` / `aprov_ac` |
| Peso huevo / Masa | peso_huevo pesaje / %prod*peso/100 | `peso_huevo` / `masa_huevo` |
| Conversión gr/H.I. | (consH+consM)*1000/huevo_inc | `gr_huevo_inc` |
| Apareo M:H | avesM/avesH*100 | `apareo` |
| Infertilidad / Nacimiento / Pollitos | **fase 2** (traslado_huevos) — v1 solo guía | `nacim_porcentaje`, `pollito_aa` (infertilidad: no hay col guía dedicada → columna solo-real vacía v1) |

## 7. Casos de prueba (xUnit — `ReporteTecnicoSemanalCalculosTests`)

1. Zip base+extras produce semana completa; extras faltantes → columnas null sin excepción.
2. Consolidado: sumas de conteos/kg/huevos; promedio simple de pesos/unif solo entre galpones con dato; galpón sin dato en la semana no promedia.
3. Incremento consumo real y guía = valor - valor semana anterior; primera semana = valor.
4. % desviación peso = real/guía*100-100; guía 0/null → null (sin división por cero).
5. Retiro acum, conversión gr/HI, masa lote, apareo: fórmulas exactas §6, denominador 0 → null.
6. Producción semana 25 con guía NULL → fila presente, comparativos null.
7. Orden de semanas y huecos (semana sin registros) no rompen acumulados.

## 8. Validación

- `cd backend && dotnet build` (0 errores) + `dotnet test`.
- Migraciones probadas contra BD local `sanmarinoapplocal:5433` (dotnet-ef 10 de `~/.dotnet/tools-ef10`) + sanity SQL de ambas fns con un lote real.
- `cd frontend && yarn build` (0 errores; solo warning de bundle budget preexistente).
- Smoke doble: empresa Sanmarino (con datos) genera ambos reportes; el menú NO aparece para Demo/Santa Reyes.
- Sin procesos huérfanos al terminar.
