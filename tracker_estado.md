# Tracker — Fix: "aves vivas" ignora mortalidad en caja (mort_caja_h/m) en engorde

Plan: [fix_aves_vivas_mort_caja_engorde_plan.md](fase_de_desarrollo/fix_aves_vivas_mort_caja_engorde_plan.md)

---

## Diagnóstico

- [x] Reproducir el descuadre reportado (lote 77 "2603" Sacachun 3b G0049): tabla diaria = 17 aves vivas vs widget "Aves disponibles" = 0/0
- [x] Identificar causa raíz: `fn_seguimiento_diario_engorde` y `LiquidacionEngordeCalculos.CalcularAvesVivas` no restan `mort_caja_h`/`mort_caja_m`; `GetAvesDisponiblesAsync` y `CalcularHembrasVivasAsync` sí lo hacen
- [x] Descartar ventas pendientes sin confirmar como causa (todos los movimientos del lote 77 están Completado/Anulado)
- [x] Auditar alcance: todos los lotes "2603" (corrida 03) + todos los lotes de la company con `mort_caja_h`/`mort_caja_m` > 0 → solo lote 77 activo (lote 1 "LT-55" también tiene mort_caja pero está soft-deleted)

## Backend — fix

- [x] `LiquidacionEngordeCalculos.CalcularAvesVivas`: nuevo parámetro `mortCajaTotal`
- [x] `SeguimientoAvesEngordeEcuadorService.Consultas.cs` (`GetLiquidacionResumenAsync`): proyectar y restar `MortCajaH/M`
- [x] `SeguimientoAvesEngordeService.Consultas.cs` Colombia (`GetLiquidacionResumenAsync`): ídem
- [x] `backend/sql/fn_seguimiento_diario_engorde.sql` v8: restar `mort_caja_h+mort_caja_m` en `aves_iniciales` (ramas no-cerrado)
- [x] Migración EF nueva (`20260714150000_FixFnSeguimientoEngordeMortCaja`, `CREATE OR REPLACE FUNCTION`, idempotente) sincronizada con el `.sql`
- [x] Test `LiquidacionEngordeCalculosTests.cs`: caso mortCaja=0 (equivalencia previa) + caso lote 77 (mortCaja=17 → 0)
- [x] `dotnet build` del proyecto Infrastructure (incluye Application/Domain) — 0 errores, 0 warnings. (No se pudo compilar el proyecto API: el backend del usuario está corriendo local y bloquea su bin/; no se tocó ese proceso.)
- [x] `dotnet test` (proyecto Application.Tests) — 336/336 en verde

## Validación local

- [x] Aplicar la función corregida a `sanmarinoapplocal` local (`psql -f backend/sql/fn_seguimiento_diario_engorde.sql`, CREATE OR REPLACE — no toca `__EFMigrationsHistory`)
- [x] `SELECT * FROM fn_seguimiento_diario_engorde(77)` → última fila `saldo_aves = 0` (antes 17) ✅ igualando el widget "Aves disponibles"
- [x] Confirmar no-op en lotes sin mort_caja (76 abierto, 80 cerrado): sin errores, valores coherentes
- [x] Reportar hallazgo + fix al usuario (sin deploy — la migración se aplica sola en el próximo deploy normal)
