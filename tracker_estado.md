# Tracker de Estado — Fix Producción 405 + Migraciones Traslados

**Plan:** [fase_de_desarrollo/fix_produccion_405_traslados_plan.md](./fase_de_desarrollo/fix_produccion_405_traslados_plan.md)
**Fecha:** 2026-05-26
**Estado:** ⏸ En análisis — esperando decisión del usuario sobre estrategia

---

## Causa raíz confirmada

1. TaskDef:92 (imagen `20260526-0924`) crasheó 3 veces con SIGSEGV → ECS rollbackeó a TaskDef:90 (imagen vieja del 21/may).
2. Migración `20260525041719_AddTrasladoAcumuladosLPPandSeguimiento` ejecuta `ALTER TABLE produccion_seguimiento` pero esa tabla no existe en RDS prod (existe `seguimiento_diario_produccion_reproductoras`).
3. Faltan 8 migraciones del módulo Traslados (24-25/may) por aplicar en RDS.

---

## Checklist — Fase de análisis ✅

- [x] Identificar workflow del CI/CD que no desplegó
- [x] Confirmar paths-filter como causa del salto de jobs (commit `3f4a48e`)
- [x] Validar que el código local tiene `LotePosturaBaseController.PUT`
- [x] Inventariar entidades/columnas nuevas del módulo Traslados
- [x] Inventariar scripts SQL 046-052 + migración `AddFarmIdErpCreate`
- [x] Verificar estado real del servicio ECS (TaskDef:90 corriendo, no 92)
- [x] Identificar exit code 139 / SIGSEGV en tareas fallidas
- [x] Conectar a RDS prod y verificar schema vs entidades
- [x] Confirmar tablas/columnas faltantes en prod
- [x] Documentar plan formal

## Checklist — Fase de remediación ⏸ (pendiente aprobación)

- [ ] Decidir estrategia (Opción A / B / C — ver plan)
- [ ] Ajustar script `051_add_traslado_columns_produccion_seguimiento.sql` (nombre de tabla)
- [ ] Resolver desajuste `ProduccionSeguimientoConfiguration.ToTable(...)` vs tabla real en prod
- [ ] Setear `Database__RunMigrations=false` en TaskDef producción
- [ ] Aplicar scripts SQL en orden en RDS prod
- [ ] INSERT manual en `__EFMigrationsHistory` para marcar migraciones aplicadas
- [ ] Re-deploy backend (esperar `rolloutState=COMPLETED` en TaskDef nueva)
- [ ] Smoke test: `PUT /api/LotePosturaBase/{id}` retorna 200/204 (no 405)
- [ ] Verificar endpoints traslados levante + producción
- [ ] Confirmar con usuario que el problema reportado se resolvió
- [ ] (Solo después de validar fix) push del commit `3f4a48e` y merge a `main-produccion`
