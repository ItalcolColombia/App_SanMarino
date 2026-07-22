# Plan — Guía genética Panamá: Ross 308 AP 2022 (mixto) + repunte de lotes

## Objetivo
Asignar a **Panamá** (empresa `company_id = 5`, `pais_id = 3`) la tabla genética oficial
**Aviagen "Yield Plus × Ross 308 AP · Objetivos de Rendimiento · 2022"**, sexo **mixto**, y
**eliminar la guía incorrecta** que hay cargada hoy. Reutiliza el módulo existente que usa
Ecuador (`guia_genetica_ecuador_header` / `_detalle`). Solo mixto (macho/hembra fuera de alcance).

## Fuente de datos (validada)
- PDF: `YPxRoss308-AP_BroilerPerformanceObjectives2022_ES.pdf` (Aviagen, edición 2022).
- Tabla **Mixto en gramos**: **57 filas, día 0 → 56**. Columnas del PDF ↔ columnas de BD:
  - Día → `dia`; Peso Corporal (g) → `peso_corporal_g`; Ganancia Diaria (g) → `ganancia_diaria_g`;
    Promedio Ganancia Diaria (g) → `promedio_ganancia_diaria_g`; Cantidad Alimento Diario (g) →
    `cantidad_alimento_diario_g`; Alimento Acumulado (g) → `alimento_acumulado_g`; CA → `ca`.
  - `mortalidad_seleccion_diaria = 0` (el PDF no trae columna de mortalidad).
- Extracción por coordenadas (pdfplumber), no transcripción manual. Validación aritmética:
  `ca ≈ alimento_acumulado / peso_corporal` cuadra a ±0.001 de día 7 a 56; los días 1-3 y 6
  difieren levemente por la metodología de la nota al pie (la CA incluye el peso inicial del
  pollito), y se almacenan **tal cual los imprime el PDF**.
- Celdas vacías del PDF en días tempranos (p. ej. día 1 sin "alimento diario") → `0`, misma
  convención que la guía de Ecuador ya cargada.

## Diagnóstico del estado actual (BD)
- Países: Colombia=1, Ecuador=2, **Panamá=3**.
- `guia_genetica_ecuador_header` id=1 → company 3 / país 2 (**Ecuador**), "Ross 308-AP", 2022,
  mixto+hembra+macho. **NO se toca.** (Sus valores difieren del PDF: es otra edición.)
- `guia_genetica_ecuador_header` id=2 → company 5 / país 3 (**Panamá**), "ROSS 308 AP", **2026**,
  solo mixto, **49 filas (día 1-49) con TODOS los pesos/ganancia/CA en 0.000** → inservible.
  **Esta es la que se elimina.**
- `lote_ave_engorde` de Panamá: 31 lotes, raza "ROSS 308 AP", `ano_tabla_genetica = 2026`,
  0 seguimientos (local).

## Decisión (confirmada por el usuario): Opción A
Guía en **año 2022** (fiel al PDF) **y repuntar los 31 lotes** de `ano_tabla_genetica` 2026 → 2022,
porque los indicadores de engorde resuelven la guía por `lote.raza` + `lote.anoTablaGenetica` +
`sexo='mixto'` (ver `indicadores-diarios-engorde-compute.service.ts`). Así la guía nueva enlaza con
los lotes existentes.

## Enfoque arquitectónico
- **Sin cambios de modelo/esquema.** Es una migración **de solo datos (DML)**: DELETE + INSERT + UPDATE.
- Migración EF idempotente (patrón del repo), sin editar `.csproj` ni el `ModelSnapshot`
  (el Designer clona el snapshot vigente 10.0.9). No usa `dotnet ef migrations add` para evitar el
  gotcha de build/lock con el backend del usuario corriendo.

## Archivos a crear
- `backend/src/ZooSanMarino.Infrastructure/Migrations/20260722220000_SeedGuiaGeneticaPanamaRoss308AP2022.cs`
- `backend/src/ZooSanMarino.Infrastructure/Migrations/20260722220000_SeedGuiaGeneticaPanamaRoss308AP2022.Designer.cs`
  (clon del Designer de `20260722210000_FixNombresLoteEngordePanamaPorLoteBase`, solo cambia id/clase).

## SQL (Up) — idempotente
1. `DELETE FROM guia_genetica_ecuador_header WHERE company_id=5 AND pais_id=3 AND upper(btrim(raza))='ROSS 308 AP' AND anio_guia IN (2022,2026)`
   (borra la mala 2026 y, en re-ejecución, la 2022 que insertó esta misma migración → converge). Cascada elimina el detalle.
2. `WITH nuevo AS (INSERT ... header (país 3, 'ROSS 308 AP', 2022, 'active', company 5, created_by 1369984321, now()) RETURNING id)`
   `INSERT ... detalle` 57 filas mixto vía `CROSS JOIN (VALUES ...)`.
3. `UPDATE lote_ave_engorde SET ano_tabla_genetica=2022 WHERE company_id=5 AND pais_id=3 AND ano_tabla_genetica=2026 AND upper(btrim(raza))='ROSS 308 AP'` (idempotente: re-ejecución = no-op).

## SQL (Down)
- Elimina la guía 2022 que agregó esta migración (`DELETE ... anio_guia=2022`). No restaura la guía
  vieja (datos rotos, no se preservan) ni revierte los lotes (no se puede distinguir con seguridad
  cuáles eran 2026). Comentado como reverso de una sola vía (mismo criterio que FixNombres...).

## Seguridad / verificación
- `created_by_user_id` **no tiene FK** → literal 1369984321 seguro en prod. `company_id` FK a
  `companies.id`, empresa 5 existe. `created_at` timestamptz → `now()`.
- Scope estricto por `company_id=5 AND pais_id=3 AND raza` → **no toca Ecuador ni Colombia.**

## Casos de prueba (local, `sanmarinoapplocal:5433`)
1. Aplicar migración → 0 headers Panamá con año 2026; 1 header (company 5, país 3, "ROSS 308 AP", 2022).
2. Detalle: 57 filas mixto, día 0-56, sin hembra/macho; spot-check día 7 (210/32/24/35/162/0.771) y día 56 (4374/88/77/234/7743/1.773).
3. Lotes: 0 lotes Panamá con `ano_tabla_genetica=2026`; 31 con 2022.
4. Ecuador (header 1) intacto: 3 sexos, 57 filas c/u.
5. Re-ejecutar el bloque Up → mismo estado final (idempotencia).
6. `dotnet build` del backend: 0 errores.

## Fuera de alcance
- Macho/hembra (solo mixto).
- Ecuador/Colombia.
- Cambios de UI/servicios (la guía se consume con el flujo existente).
