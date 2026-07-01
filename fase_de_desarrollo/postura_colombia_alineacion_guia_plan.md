# Plan — Alineación Postura Colombia (Levante + Producción) contra Guía Genética

> **Origen:** Matriz funcional de la líder Verenice (10 REQ) — `Downloads/Plantilla de lider Funcional AppZootenico Verenice 12jun26.xlsx`.
> **Entregable de análisis:** [`Matriz_Requerimientos_Postura_Colombia_CORREGIDA.xlsx`](./Matriz_Requerimientos_Postura_Colombia_CORREGIDA.xlsx) (matriz corregida + estado real en código + solución + mapeo guía↔indicador + plan).
> **Empresa:** Agroavícola Sanmarino (`company_id = 1`, Colombia).

## 1. Enfoque arquitectónico

Fuente de verdad = **código actual + BD local** (`sanmarinoapplocal`). Se corrige la observación de campo, se documenta el estado real y se propone la solución. Muchos REQ ya están **parcialmente implementados** (el gap es exponerlos o corregir la lógica de comparación con la guía), no partir de cero.

**Pieza central:** la **guía genética real de Colombia** es la tabla `guia_genetica_sanmarino_colombia` (entidad `ProduccionAvicolaRaw`), **por semana** (`edad` = semana de vida). Levante = 1–25, Producción ≥ 26. La guía de Ecuador/Panamá (`GuiaGeneticaEcuador*`) **no se toca**. El **Reporte Técnico Sanmarino** (`reportes-tecnicos`) ya unifica Levante + Producción; el feature `reporte-tecnico-produccion` está **deprecado**.

## 2. Archivos / componentes / servicios

**Backend**
- `Infrastructure/Services/IndicadoresProduccionService.cs` — fórmulas producción y comparativos vs guía (REQ-004/005). Bugs de unidades a corregir (~L509 %prod /(H+M); ~L576 huevos/día vs `h_total_aa`).
- `Infrastructure/Services/ReporteTecnicoService.cs` — agregación semanal levante, consolidado (REQ-002/003/008).
- `Infrastructure/Services/LiquidacionCierreLoteLevanteService.cs` — liquidación levante vs guía (semana 25).
- `Infrastructure/Services/GuiaGeneticaService.cs` / `ExcelImportService.cs` — lectura/carga de la guía.
- `Application/Calculos/` — extraer aritmética pura + tests xUnit.
- Nuevo endpoint: **guía por semana** (raza, año, edad) para reportes/gráficas.

**Frontend**
- `features/seguimiento-diario-lote-reproductora/…/modal-seguimiento-reproductora.*` — Uniformidad/CV (comentados), alimento H/M, %Retiro, día calendario, fecha traslado (REQ-007/009).
- `features/reportes-tecnicos/components/tabla-levante-semanal-*` — **guía hardcodeada** `datosGuiaGeneticaEstaticos` → leer de BD (REQ-002/010).
- Componentes de **gráficas** de `reportes-tecnicos` — comparativas pro H/M + guía por semana (REQ-010).
- `features/catalogo-alimentos` — alimento "Pollita iniciación" y separación H/M.
- `features/lote-levante/…/tabs-principal.component.html:361` — "Bajas total" → "Mortalidad total" (REQ-001).

## 3. Cambios BD / SQL

- **DATA-000 (con confirmación):** `UPDATE` de `ano_tabla_genetica` 2023→2026 en lotes de `company_id = 1` (raza AP) en `lote_postura_produccion` / `lote_postura_levante` / `lotes`. (2026 ya cargada; 2024 no existe.)
- **REQ-006 (fase final):** migración EF **idempotente** para estado del registro (Abierto/Cerrado) + auditoría. Solo tras decisión de negocio.
- Resto: sin cambios de schema; se corrige lógica de cálculo/lectura.

## 4. Reglas de negocio / fórmulas (corregidas)

Ver hoja **"3. Mapeo Guía↔Indicador"** del Excel. Resumen:
- `% Producción = huevos_totales_día / saldo HEMBRAS × 100` (excluir machos del denominador).
- `H.T.A.A = Σ huevos_totales / hembras_iniciales` (acumulado). `H.I.A.A = Σ incubables / hembras_iniciales`.
- `gr/ave/día = (Σ consumo_semana_kg × 1000 / 7) / saldo` (promedio diario, **no** sumatoria).
- `Consumo acum g/ave = Σ consumo_kg × 1000 / aves_iniciales`.
- `% Retiro sem = (Mort+Sel de la semana) / saldo de la semana × 100`; `% Retiro acum = (MortAc+SelAc)/aves_iniciales`.
- Comparación **por semana**: guía indexada por `edad`; flujos = suma de 7 días, peso/uniformidad = promedio.

## 5. Casos de prueba

- Guía 2026 AP, semana 25: liquidación levante resuelve `cons_ac_h`, `gr_ave_dia_h`, `retiro_ac_h`, `peso_h`, `uniformidad` (no null).
- Producción semana 30: `%prod` real usa solo hembras; HTAA/HIAA acumulados vs `h_total_aa`/`h_inc_aa`.
- Consumo gr/ave/día semana 1 levante ≈ 20–22 g (no 157). 
- Lote company 1 tras DATA-000: comparativos ≠ vacío.
- Consolidado A+B+C: flujos = suma; peso = promedio ponderado por saldo.
- xUnit sobre `Application/Calculos` para cada fórmula.

## 6. Orden de ejecución

Fases 0→9 en la hoja **"4. Plan de Implementación"**. Quick-wins primero (0: datos, 1: guía en BD, 7: glosario), luego fórmulas (2), gráficas (3), levante diario (4/5/6), consolidación (8) y flujo de edición (9, requiere negocio). Build + tests por fase; sin procesos huérfanos.
