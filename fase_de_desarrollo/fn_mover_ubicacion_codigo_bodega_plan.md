# Plan — `fn_rekey_nucleo` copia `codigo_bodega`/`descripcion_bodega` al mover núcleo

## Problema
La migración `20260725175311_AddInfraErpAvicolaSantaReyes` (Santa Reyes, en curso en el checkout
principal, sin commitear aún) agrega a `nucleos` dos columnas ERP:
`codigo_bodega varchar(20) NULL` y `descripcion_bodega varchar(200) NULL`.

`backend/sql/fn_mover_ubicacion.sql` define `fn_rekey_nucleo` (mover núcleo entre granjas con
patrón insert-repoint-delete, porque la granja es parte de la PK). Su `INSERT INTO nucleos`
usa **lista explícita de columnas** que no incluye las nuevas → al mover un núcleo, la copia
en la granja destino perdería `codigo_bodega`/`descripcion_bodega` **en silencio**.

Hallazgos de la auditoría (BD local `sanmarinoapplocal:5433`, real):
- `nucleos` tiene exactamente 11 columnas: las 9 que la función ya copia + las 2 nuevas.
  No hay más columnas faltantes.
- **Ninguna** de las 3 funciones del archivo (`fn_mover_lote`, `fn_mover_galpon`,
  `fn_rekey_nucleo`) existe en la BD local: el commit original `100c343` aplicó el SQL
  **fuera de banda** (solo prod), sin migración. No hay reproducibilidad.

## Enfoque
1. **`backend/sql/fn_mover_ubicacion.sql`** (fuente de verdad): agregar las 2 columnas al
   INSERT/SELECT de `fn_rekey_nucleo` + comentario-advertencia (la lista explícita debe
   mantenerse al agregar columnas a `nucleos`). Sin otros cambios de lógica.
2. **Migración EF idempotente** `20260725210000_FnMoverUbicacionCopiaBodegaNucleo`:
   - Timestamp **posterior** a las 2 migraciones sin commitear de Santa Reyes
     (`…175311` y `…190000`) para que en cualquier BD las columnas se creen antes.
   - `Up()`:
     a. Defensivo: `ALTER TABLE nucleos ADD COLUMN IF NOT EXISTS` de las 2 columnas
        (mismo patrón "columnas defensivas" que usa `AddInfraErpAvicolaSantaReyes` con
        `menus`) → la función nunca referencia columnas inexistentes aunque las ramas
        mergeen en otro orden. Converge con la migración de Santa Reyes (ambas IF NOT EXISTS).
     b. `CREATE OR REPLACE` de las **3 funciones** (contenido íntegro del .sql actualizado):
        prod solo ve el cambio real en `fn_rekey_nucleo`; local/BDs nuevas ganan las 3
        funciones que hoy faltan (reproducibilidad).
   - `Down()`: restaura la versión previa de `fn_rekey_nucleo` (sin las 2 columnas).
     No borra funciones (prod las tiene fuera de banda) ni columnas (son de la migración
     de Santa Reyes).
   - Designer clonado de la última migración del worktree; **ModelSnapshot intacto**
     (la migración no cambia el modelo — el modelo con `CodigoBodega` vive en el
     checkout principal, sin commitear).
3. Sin cambios C#/front: `NucleoService.MoverAsync` llama la función por nombre.

## Archivos
- `backend/sql/fn_mover_ubicacion.sql` — modificar (INSERT/SELECT de `fn_rekey_nucleo`).
- `backend/src/ZooSanMarino.Infrastructure/Migrations/20260725210000_FnMoverUbicacionCopiaBodegaNucleo.cs` — nuevo.
- `backend/src/ZooSanMarino.Infrastructure/Migrations/20260725210000_FnMoverUbicacionCopiaBodegaNucleo.Designer.cs` — nuevo (clonado).

## Reglas de negocio
- Mover núcleo debe conservar TODO el dato del núcleo, incluidos los códigos ERP de bodega
  (pass-through de Santa Reyes; solo visibles con `companies.maneja_codigos_erp_avicola=true`).
- Refactor sin cambio de comportamiento fuera del fix: mismas validaciones, mismo orden de
  UPDATEs, misma auditoría (conserva creación, estampa `updated_by/updated_at` al copiar).

## Casos de prueba
- `dotnet build` 0 errores.
- `database update` local (dotnet-ef 10 de `~/.dotnet/tools-ef10/`, desde Infrastructure,
  `--startup-project .`) aplica solo la migración nueva.
- Verificación en BD: las 3 funciones existen; `pg_get_functiondef(fn_rekey_nucleo)` incluye
  `codigo_bodega`/`descripcion_bodega`.
- Smoke transaccional (BEGIN…ROLLBACK, sin efecto permanente): crear núcleo temporal con
  bodega en granja A, `fn_rekey_nucleo` → granja B, verificar que la copia conserva
  `codigo_bodega`/`descripcion_bodega`; colisión y origen inexistente ya cubiertos por RAISE.
