# 📊 Tracker de Estado — Validación Entidades ↔ BD y Alineación para Producción

**Plan de referencia:** [fase_de_desarrollo/17_validacion_entidades_vs_bd_INDICE.md](fase_de_desarrollo/17_validacion_entidades_vs_bd_INDICE.md)
→ **Parte A** (mapeo entidad↔tabla↔relaciones, todos los campos) · **Parte B** (auditoría funciones/triggers/vistas + plan de migración)

**Requerimiento origen:** alinear el backend con la BD de pruebas para que el despliegue a
producción cree automáticamente todos los objetos (tablas, funciones, triggers, vistas) vía
migraciones EF, sin depender de scripts SQL manuales.

**Fecha:** 2026-05-31
**Estado global:** ✅ **COMPLETADO Y VALIDADO LOCALMENTE.** Migración
`20260531180558_AddMissingDbFunctionsTriggersAndViews` creada, compila y aplica sin error.

---

## ✅ Checklist

### Fase 1 — Introspección de la BD
- [x] Conexión a `localhost:5432/sanmarinoapplocal` (psql de libpq).
- [x] Listar tablas (77) + columnas (1611) + PKs + FKs (142).
- [x] Listar funciones (16), triggers (7) y vistas (3).
- [x] Extraer DDL real con `pg_get_functiondef/viewdef/triggerdef`.

### Fase 2 — Cruce código ↔ BD
- [x] Mapear entidad ↔ tabla (configs EF + entidades).
- [x] Cruzar cada función/trigger/vista contra el contenido de las migraciones EF.
- [x] Identificar objetos SIN migración: 13 funciones, 6 triggers, 3 vistas.

### Fase 3 — Documentación
- [x] Parte A: mapeo entidad↔tabla↔relaciones con todos los campos.
- [x] Parte B: auditoría + desalineaciones + plan de migración.
- [x] Índice del paquete.

### Fase 4 — Migración
- [x] `dotnet ef migrations add AddMissingDbFunctionsTriggersAndViews`.
- [x] Rellenar Up()/Down() idempotente (CREATE OR REPLACE func/view; DROP+CREATE triggers).
- [x] `dotnet build` Infrastructure ✅ 0 errores.
- [x] `dotnet ef database update` ✅ aplica sin error; objetos persisten; smoke tests OK.

### Fase 5 — Validación sobre COPIA DE PRODUCCIÓN ✅ (2026-05-31)
- [x] Restaurada copia de prod en `sanmarinoapplocal` y arrancado el back (aplicó migraciones).
- [x] **0 migraciones pendientes**; mi migración `20260531180558` aplicada como última (59 aplicadas).
- [x] Objetos presentes: 16 funciones, 7 triggers, 3 vistas (nombres exactos verificados).
- [x] Smoke tests OK con datos reales: `fn_seguimiento_diario_engorde(5)`, `fn_indicadores_pollo_engorde(5,0,1)`, vistas con datos (3536/3495/75 filas).
- [x] Sin tablas faltantes inesperadas (solo Ecuador/Panamá descartadas + `_ignored_produccion_diaria`).
- [x] **CONCLUSIÓN: el deploy a producción pasará sin error.**

### Fase 5b — Nueva migración: recálculo de saldo de alimento con función FINAL ✅ (2026-05-31)
- [x] Detectado: `20260528212753` recalcula saldos con fn v4, pero `20260531034622` mejoró la función SIN re-recalcular → saldos quedaban con lógica vieja.
- [x] Creada migración `20260531184044_RecalcularSaldoAlimentoEngorde20260531` (respaldo en `_migracion_saldo_alimento_2026_05_31` + UPDATE masivo con fn final; idempotente).
- [x] Simulación completa de deploy sobre copia cruda de prod (estaba en `20260525131406`): 12 migraciones pendientes aplicadas sin error.
- [x] **Resultado:** persistido == función final (0 discrepancias). Cadena: crudo→v4 = 1935 cambios; v4→final = **0 cambios** (la fn final da el mismo saldo que v4 para los datos actuales).
- [x] **Impacto total que verá prod:** 1935/3495 saldos cambian (vs crudo), prom 37.366 kg, máx 253.254 kg — driven por `20260528212753`. Respaldo completo en ambas tablas `_migracion_saldo_alimento_*`.

### Fase 6 — Notas / pendientes
- [x] Separación seguimiento por país (Ecuador/Panamá): **descartada e intencional**. Decisión
      2026-05-31: **solo documentar, no tocar código** (Parte B §6.1). Quedan controllers/servicios
      cableados que fallarían si se llaman; limpieza opcional a futuro.
- [ ] (Opcional) Decidir destino de tablas huérfanas `user_paises`, `guia_semana`.
- [ ] Commit + deploy por flujo normal (ECS aplica migraciones al arrancar).

---

## 📦 Entregables
- `fase_de_desarrollo/17_validacion_entidades_vs_bd_INDICE.md`
- `fase_de_desarrollo/17_validacion_entidades_vs_bd_PARTE_A_mapeo.md`
- `fase_de_desarrollo/17_validacion_entidades_vs_bd_PARTE_B_auditoria.md`
- `backend/src/.../Migrations/20260531180558_AddMissingDbFunctionsTriggersAndViews.cs`
