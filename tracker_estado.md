# Tracker — Corrección global saldos aves engorde (caso 2602 / lote 73) ✅ COMPLETO EN LOCAL

**Plan:** [fase_de_desarrollo/correccion_saldos_engorde_2602_global_plan.md](fase_de_desarrollo/correccion_saldos_engorde_2602_global_plan.md)
(Antecedente 2601: [correccion_aves_disponibles_engorde_2601_plan.md](fase_de_desarrollo/correccion_aves_disponibles_engorde_2601_plan.md) · Tracker anterior en pausa: [merma_liquidacion_ecuador_plan.md](fase_de_desarrollo/merma_liquidacion_ecuador_plan.md))

## Diagnóstico
- [x] Lote 73 (2602): 9 ventas con factura `Pendiente` (5.224 H + 10.676 M) en histórico/tabla pero sin descontar maestro; `aves-disponibles` no restaba pendientes → 17.257 fantasma vs tabla 1.357
- [x] Scan global 85 lotes: lote 72 (2602) mismo caso (14 pendientes, 5.225 H + 10.893 M); lote 5 (2602) maestro no descontado por 29 ventas de abril (bug previo a may-2026): 23.630 aves infladas; lote 8 (2602) inconsistencia de carga sin evidencia → RevisionManual; 7/30 falsos positivos descartados por conservación
- [x] Sin nuevos cerrados con fantasma (los 8 de 2601 ya corregidos)

## Implementación (código)
- [x] `AvesDisponiblesDto` + `GetAvesDisponiblesAsync`: resta reserva Pendiente por género (paridad con `ResumenDisponibilidad`); campos `HembrasReservadasPendiente/MachosReservadasPendiente`
- [x] DTOs v2 (pendientes, vencidas H/M, drift, `HistorialInicioConfiable`, `TipoDescuadre`, acciones)
- [x] Servicio v2: confirmar pendientes vencidos (vía `CompleteAsync` real) → re-sync maestro (historial confiable o walk determinista viejas→nuevas con match exacto; si no cierra → RevisionManual sin tocar) → fantasma cerrados; `loteNombre` opcional (null = todos)
- [x] Auditoría tipificada: `'Ajuste'` (fantasma, participa en conservación) vs `'AjusteResync'` (sustituye descuento de ventas, NO participa) — corrige bug de idempotencia detectado en 2ª corrida (lote 5 doble ajuste, reparado con `backend/sql/tmp_repair_lote5_ajuste_resync.sql`)

## Migraciones (lo que llega a PROD en el deploy)
- [x] `20260611033748_FixFnSeguimientoEngordeVentasPostCierre` — fn v7 (ventas post-cierre como fila; saldo cierra en 0)
- [x] `20260611172121_CorreccionSaldosAvesEngorde2601y2602` — **corrección de DATOS**: CHECK +'AjusteResync' · confirma las 23 ventas Pendientes de 72/73 por ID (guard estado) · re-sync lote 5 (marcador AjusteResync) · fantasma 2601 en 8 lotes (marcador Ajuste, guard Cerrado). SQL legible: [backend/sql/correccion_saldos_aves_engorde_2601_2602.sql](backend/sql/correccion_saldos_aves_engorde_2601_2602.sql)
- [x] Simulación estado-prod en transacción: aplica exacto (5→1101/739 · 23→875 · 1357→Completado) + re-aplicación sin cambios + ROLLBACK limpio
- [x] Migración aplicada en local al arrancar la API → **no-op** verificado (maestros intactos, sin duplicados de auditoría)

## Validación final (API local :5002, BD local)
- [x] `dotnet build` 0 errores · `dotnet test` 2/2
- [x] `GET validar` global: 85 lotes, solo lote 8 → RevisionManual (driftTotal 36, sin ventas; decisión de negocio)
- [x] `POST corregir dryRun`: 0 lotes corregibles, 0 movimientos → idempotencia total
- [x] Disponibles == tabla diaria: lote 5 → 161 = 161 · lote 72 → 175 = 175 · lote 73 → 1.370 ≈ 1.357 (dif. 13 = sobreventa de género H documentada) · lote 23 → 0 (cerrado)
- [x] API quedó corriendo en :5002 con el build nuevo; temporales eliminados

## Pendiente (decisión del usuario)
- [ ] Lote 8 (2602, G0043): maestro 25.000/25.000 vs encaset 49.964, sin ventas, seguimiento abandonado en marzo → definir manualmente (¿cerrar/eliminar lote de prueba?)
- [ ] Commit + deploy a prod (las 2 migraciones se aplican solas al arrancar; verificación post-deploy de la sección 🚀 del CLAUDE.md)
- [ ] Post-deploy: `GET /api/LoteAveEngorde/aves-disponibles/validar` en prod para confirmar alineación
