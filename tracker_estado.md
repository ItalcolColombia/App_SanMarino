# Tracker — Fix: descuento de aves en migración masiva de Seguimiento (Levante + Producción)

Plan: [migracion_seguimiento_levante_aves_fix_plan.md](fase_de_desarrollo/migracion_seguimiento_levante_aves_fix_plan.md) (incluye addendum de Producción)

**Tarea activa:** mismo bug que Levante, ahora en `fn_migracion_seguimiento_produccion`: recalculaba
`aves_h_actual/aves_m_actual` desde cero, ignorando traslados/movimientos de aves ya aplicados al
lote, y el dedup/merge no encontraba filas de traslado (matcheaba por `lote_postura_produccion_id`,
que esas filas nunca setean). Fix con el mismo patrón: descuento incremental + merge por lote_id
crudo + fecha calendario.

---

## Levante (cerrado, commit `2eab7f8`)
- [x] Ver detalle en el plan — fix, migración EF, smoke SQL + harness backend real, commit hecho.

## Producción — F1 Diagnóstico
- [x] Confirmar que `SeguimientoProduccionService` (no `SeguimientoDiarioService`) es el manual real de Producción
- [x] Confirmar mismo patrón Bug A: recompute total en `fn_migracion_seguimiento_produccion` ignora `ProduccionTrasladoIngreso/Salida*` y `MovimientoAvesService`
- [x] Encontrar diferencia clave: filas de traslado de Producción NO setean `lote_postura_produccion_id` → el dedup/merge debe matchear por `lote_id` crudo, no por ese FK
- [x] Confirmar columna real `fecha_registro` (no `fecha`) y mismo problema de representación horaria

## Producción — F2 Fix SQL (`fn_migracion_seguimiento_produccion`)
- [x] Reescribir función: merge sobre filas `es_traslado=true` sin datos manuales (match por `lote_id` + `fecha_registro::date`)
- [x] Reescribir función: insert igual que hoy para fechas sin ninguna fila previa (mismo `::date` fix en el dedup)
- [x] Reescribir función: descuento incremental sobre `aves_h_actual`/`aves_m_actual` (no recálculo total)
- [x] `DROP TABLE IF EXISTS` defensivo en las temporales (mismo fix que Levante)

## Producción — F3 Migración EF
- [x] `dotnet ef migrations add FixMigracionSeguimientoProduccionAvesIncremental` — esta vez usando la `DesignTimeDbContextFactory` propia de `ZooSanMarino.Infrastructure` (evita tocar el bin de `ZooSanMarino.API`, bloqueado por el backend en vivo del usuario)
- [x] `dotnet ef database update` aplicada correctamente vía EF (no vía psql directo) — de paso quedó registrada en `__EFMigrationsHistory` también la migración pendiente de Levante de la sesión anterior

## Producción — F4 Validación
- [x] Smoke SQL manual (transacción con `ROLLBACK`): LPP real `lote_postura_produccion_id=6` (lote 14) con traslado de ingreso de 300 hembras simulado + fila solo-traslado real (sin `lote_postura_produccion_id`) → migrar mortalidad nueva descuenta SOLO lo nuevo, conserva el traslado
- [x] Smoke SQL manual: reimport mismo archivo → idempotente, 0 filas, sin doble descuento
- [x] Smoke SQL manual: fecha con fila solo-traslado + fila Excel con mortalidad → se fusiona (encontrada por `lote_id` crudo, que el original NUNCA hacía), no se saltea
- [x] `dotnet build` 0 errores
- [ ] Harness de backend real (como Levante) — **NO ejecutado**: el lote de prueba local no cumple elegibilidad de Producción (Levante cerrado+liquidado) y fabricar esos prerequisitos habría mutado datos compartidos fuera del patrón rollback-safe; bloqueado por guardrail de seguridad al intentarlo. Revertido el único cambio que alcanzó a aplicarse (`lotes.fase` de vuelta a 'Levante'). Riesgo residual bajo: la ruta C# es idéntica a la de Levante ya probada end-to-end.
- [x] Scripts de scratch borrados; sin cambios persistidos fuera de las 2 migraciones

## Cierre
- [ ] Commit de esta mejora (Producción)
- [ ] Reportar al usuario, incluyendo la limitación de la F4 (harness real no corrido) para que decida si lo quiere igual
- [ ] Memoria del proyecto actualizada
