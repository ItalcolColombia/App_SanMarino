# Plan — Fix: "aves vivas" (tabla diaria / liquidación) ignora mortalidad en caja (mort_caja_h/m)

## Diagnóstico (confirmado con datos reales, lote 77 "2603" Sacachun 3b G0049, Ecuador)

Dos cálculos independientes de "aves vivas" para el mismo lote de engorde dan resultados distintos:

1. **`GET /api/SeguimientoAvesEngordeEcuador/por-lote/{id}/tabla-diaria`** (función SQL
   `fn_seguimiento_diario_engorde`, columna `saldo_aves`) y **`GET .../resumen-liquidacion`**
   (`LiquidacionEngordeCalculos.CalcularAvesVivas`, usado también por Colombia) →
   parten de `aves_encasetadas` / historial "Inicio" y **NO restan `mort_caja_h`/`mort_caja_m`**
   (mortalidad de aves que llegaron muertas en la caja de transporte, capturada una sola vez al
   crear/editar el lote, campo "Mort. caja H/M" en el modal de `lote-engorde-list`). Para el lote
   77: `20121 (encasetadas) − 2357 (bajas seguimiento) − 17747 (ventas) = 17` → la tabla diaria
   muestra 17 aves vivas.

2. **`GET /api/LoteReproductoraAveEngorde/{id}/aves-disponibles`** (`GetAvesDisponiblesAsync`,
   widget "Aves disponibles" en la pantalla de seguimiento) y **`CalcularHembrasVivasAsync`**
   (usado al crear/editar un registro diario en ambos países, Ecuador y Colombia) **SÍ restan
   `mort_caja_h`/`mort_caja_m`** del maestro (`hembras_l`/`machos_l`). Para el lote 77:
   `1145 (hembras_l) − 17 (mort_caja_h) − 664 (mort. seg) − 464 (sel. seg) = 0` hembras;
   `1229 (machos_l) − 646 − 583 = 0` machos → el widget muestra 0/0 y bloquea nuevos registros
   ("no se puede hacer seguimiento").

**Conclusión:** el camino de escritura/validación (crear registro, widget de disponibilidad) YA
tiene la regla correcta ("mort. caja" se resta una vez del total inicial). Los dos caminos de
**solo lectura/reporte** (tabla diaria SQL y resumen de liquidación, éste último compartido con
Colombia) son los que están desactualizados y **sobreestiman las aves vivas exactamente en el
monto de `mort_caja_h + mort_caja_m`**.

### Alcance real del bug (auditoría completa, no solo lote 77)

Query en `sanmarinoapplocal` (local, réplica de prod): de TODOS los `lote_ave_engorde` de la
compañía, solo 2 tienen `mort_caja_h`/`mort_caja_m` > 0:
- `id=1` "LT-55" — **soft-deleted** (`deleted_at` no nulo), sin impacto visible.
- `id=77` "2603" (Sacachun 3b, galpón G0049) — el caso reportado por el usuario. Único lote
  activo afectado hoy.

Los otros 28 lotes nombrados "2603" (corrida 03, otras granjas/galpones) tienen
`mort_caja_h`/`mort_caja_m` en NULL → no están afectados por este bug (el fix es un no-op para
ellos: restar 0 no cambia nada). No hay "novedad"/observación en ningún registro de
`seguimiento_diario_aves_engorde` del lote 77 que explique el descuadre — no es por ventas
pendientes sin confirmar (se verificó: todos los movimientos del lote 77 están `Completado` o
`Anulado`, ninguno `Pendiente`). El origen es puramente el campo `mort_caja_h=17` (cargado el
2026-07-13) no propagado a los cálculos de lectura.

## Fix

1. **`backend/src/ZooSanMarino.Application/Calculos/LiquidacionEngordeCalculos.cs`**
   `CalcularAvesVivas` gana un parámetro `mortCajaTotal` (resta adicional, `Math.Max(0, …)`).
   Aritmética sin cambios cuando `mortCajaTotal == 0` (caso de casi todos los lotes existentes).

2. **`SeguimientoAvesEngordeEcuadorService.Consultas.cs` y
   `SeguimientoAvesEngorde/Funciones/SeguimientoAvesEngordeService.Consultas.cs` (Colombia)**
   `GetLiquidacionResumenAsync`: proyectar `MortCajaH`/`MortCajaM` del lote y pasar la suma a
   `CalcularAvesVivas`.

3. **`backend/sql/fn_seguimiento_diario_engorde.sql`** (v8): CTE `lote_info` expone
   `mort_caja_h + mort_caja_m`; CTE `aves_iniciales` resta ese total en las ramas que parten de
   `aves_encasetadas`/`suma_hm` (todas menos la rama `cerrado`, que ya fuerza el cierre en 0 por
   construcción propia y no depende de `aves_encasetadas`). Clamp a `GREATEST(0, …)` por fila.

4. **Migración EF nueva** (`CREATE OR REPLACE FUNCTION`, idempotente) que reaplica la función
   corregida — mismo patrón que las 7 migraciones anteriores de esta función. `Down()` documenta
   cómo revertir a v7 (no se implementa automáticamente, solo referencia).

5. **Test** `tests/ZooSanMarino.Application.Tests/LiquidacionEngordeCalculosTests.cs`: caso
   existente (mortCaja=0, equivalencia con comportamiento previo) + caso nuevo (mortCaja>0,
   replica el lote 77: `20121, mortCaja=17, bajas=2357, ventas=17747 → 0`).

## Validación

- `dotnet build` (0 errores) + `dotnet test` del proyecto Application.Tests.
- Aplicar la migración a `sanmarinoapplocal` local (`dotnet ef database update`) y volver a
  correr `SELECT * FROM fn_seguimiento_diario_engorde(77)` — última fila `saldo_aves` debe dar 0
  (antes 17), igualando el widget "Aves disponibles".
- Confirmar que la fn sigue devolviendo lo mismo para un lote sin `mort_caja` (p. ej. 76 o 78 de
  la misma corrida) — no debe cambiar nada (no-op).
- **No se toca RDS prod desde este entorno**: la migración se aplica sola en el próximo deploy
  (`Database:RunMigrations=true`), como toda migración de este repo.
