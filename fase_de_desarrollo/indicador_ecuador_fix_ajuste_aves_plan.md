# Plan — Fix Ajuste de Aves / % Ajuste (Indicador Ecuador)

## Problema
`ajuste_aves` y `porcentaje_ajuste` solo se calculaban cuando el lote tenía merma registrada
(gate `merma_registrada`) y restaban la merma. El requerimiento: deben calcularse para **TODOS**
los lotes con la fórmula:

- **Ajuste de aves** = `aves_encasetadas − aves_vendidas − (mortalidad + selección)`
- **% Ajuste** = `(ajuste_aves / aves_encasetadas) × 100`

(En el DTO: `mortalidad` ya incluye selección; `aves_sacrificadas` = aves vendidas/despacho.)

## Enfoque arquitectónico
1. **Frontend** (ya hecho): recalcula localmente en matriz, tarjetas, reporte y Excel —
   ignora el valor del backend para no depender del redeploy del SQL.
2. **Backend SQL** (ya editado + aplicado a BD local): `fn_indicadores_pollo_engorde` y
   `vw_liquidacion_ecuador_pollo_engorde` con la fórmula nueva, sin gate de merma.
3. **Migración EF** (esta tarea): vehículo de despliegue. El proyecto versiona funciones/vistas
   dentro de migraciones que ejecutan `CREATE OR REPLACE` vía `migrationBuilder.Sql(...)`.
   Como `Database:RunMigrations=true`, se aplica sola al desplegar a prod.

## Archivos
- `backend/src/ZooSanMarino.Infrastructure/Migrations/<ts>_UpdateFnIndicadoresAjusteAvesTodosLotes.cs`
  - `Up()`: CREATE OR REPLACE de la función + DROP/CREATE de la vista (versión con fix).
  - `Down()`: restaura la versión previa (con gate `merma_registrada` y resta de merma).
  - `suppressTransaction: true` (igual que migraciones previas de funciones).
- Espejo exacto de `backend/sql/fn_indicadores_pollo_engorde.sql` y
  `backend/sql/vw_liquidacion_ecuador_pollo_engorde.sql` (ya corregidos).

## Cambio puntual en el SQL (Up vs Down)
**Función** (bloque SELECT final):
- Up:   `(aves_encasetadas - aves_sacrificadas - mortalidad)::INT` + ROUND %.
- Down: `CASE WHEN merma_registrada THEN (... - mortalidad - COALESCE(merma_unidades,0)) END`.

**Vista** (`ajuste_aves`, `porcentaje_ajuste`):
- Up:   `(aves_encasetadas - aves_sacrificadas - mort_sel_padre)::integer`.
- Down: `CASE WHEN merma_registrada THEN (... - COALESCE(merma_unidades_raw,0)) END`.

## Casos de prueba
- `dotnet ef migrations add` genera Up/Down vacíos (sin cambios de modelo) → se llenan a mano.
- `dotnet ef database update` local sin error (idempotente; ya aplicado a mano).
- Verificar en BD: `ajuste_aves = enc - vend - mort` para lotes con y sin merma; no NULL.
- `dotnet build` 0 errores.
- Prod: aplica sola en deploy (confirmar antes de desplegar).
