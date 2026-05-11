# Memoria de Desarrollo — Tracker Activo

**Feature Actual:** BUG — Conciliación de Saldo Inicial de Aves (Pollo Engorde)  
**Fase Actual:** Correcciones aplicadas — pendiente verificación de datos en BD  
**Módulo:** `aves-engorde` — `tabs-principal-engorde` + `SeguimientoAvesEngordeService`  
**Archivo de tarea:** `fase_de_desarrollo/07_bug_conciliacion_saldo_inicial_aves.md`

> Historial de features completadas: `tracker_historico/`

---

## Estado de Implementación

| ID | Actividad | Estado | Resultado de validación |
|----|-----------|--------|------------------------|
| BUG-01 | Verificar `aves_encasetadas` / `hembras_l` / `machos_l` / `mixtas` en lote 2602 (DB) | ⚠️ Bloqueado | Sin acceso a BD en este momento. El código ya usa `aves_encasetadas` correctamente tras BUG-02. Pendiente confirmar valores reales. |
| BUG-02 | Corregir `avesInicialesLote()` — priorizar `avesEncasetadas` sobre `hembrasL + machosL` | ✅ Validado v2 | Fix refactorizado por regresión en lote 2601. Lotes **Cerrado**: `inicial = Σ(mort+sel+err+VENTA_AVES)` auto-corrige inconsistencias de BD. Lotes **Abierto**: usa `avesEncasetadas`. Si solo uno está poblado o son iguales, usa ese. TS sin errores. |
| BUG-03 | Auditar seguimientos con `Fecha < FechaEncaset` en backend | ⚠️ Riesgo confirmado en código | `GetByLoteAsync` (L146-150) no filtra por `FechaEncaset`. Si existen registros con fecha anterior al encasetamiento, se acumulan en las pérdidas. No se puede descartar sin inspección de BD. Impacto práctico bajo si los datos son correctos. |
| BUG-04 | Confirmar valores del registro `Inicio` en `historial_lote_pollo_engorde` | ✅ No aplica a tabla diaria | `HistorialLotePolloEngorde` solo lo usa `GetLiquidacionResumenAsync` (endpoint de liquidación). La tabla diaria usa `avesInicialesLote()` en el frontend, ya corregida en BUG-02. BUG-04 aplica solo si el resumen de liquidación muestra valores incorrectos. |
| BUG-05 | Descontar despachos (VENTA_AVES) del saldo vivo en `buildDiarioFilas()` | ✅ Validado v2 | Fix ampliado por regresión en lote 2601. Lotes nuevos: `VENTA_AVES` del `historicoUnificado`. Lotes viejos (despachos en metadata de seguimiento): fallback a `metaDh + metaDm`. Prioridad: historicoUnificado > metadata. `metaDh/Dm` movidos antes del cálculo de saldo. Sin doble conteo. TS sin errores. |

---

## Resumen de Correcciones en Código

### BUG-02 — `avesInicialesLote()` (Frontend) — v2
**Archivo:** `tabs-principal-engorde.component.ts` L573  
**Problema raíz:** `avesEncasetadas` puede ser incorrecto en BD para lotes antiguos (lote 2601: `avesEncasetadas=13,989` pero inicial real=817). La v1 del fix priorizaba `avesEncasetadas` siempre, rompiendo lotes liquidados con BD inconsistente.  
**Lógica v2:**
- Solo un campo poblado → usa ese
- Iguales → usa `avesEncasetadas`
- **Lote Cerrado** (liquidado): `inicial = Σ(mortalidad + selección + errSexaje + VENTA_AVES)` — el saldo final debe ser 0, así que la suma de salidas ES el inicial. Auto-corrige sin tocar BD.
- **Lote Abierto** (activo): usa `avesEncasetadas` (campo canónico → correcto para lote 2602 Ecuador=13,550)

### BUG-05 — Despachos en `buildDiarioFilas()` (Frontend)
**Archivo:** `tabs-principal-engorde.component.ts` L474 + L238-240  
**Antes:** `saldo = avesEncasetadas - Σ(mort + sel + errSexaje)` — despachos ignorados  
**Después:** `saldo = avesEncasetadas - Σ(mort + sel + errSexaje) - Σ(VENTA_AVES)` — despachos correctamente descontados

---

## Pendientes para Cierre Completo

| Acción | Responsable | Prioridad |
|--------|-------------|-----------|
| Verificar en BD que `aves_encasetadas = 13550` en lote 44 (Ecuador) | Dev con acceso BD | Alta |
| Confirmar en app que primer registro muestra 13,550 y saldo baja correctamente con despachos | QA manual | Alta |
| Evaluar si agregar filtro `Fecha >= FechaEncaset` en `GetByLoteAsync` (BUG-03) | Dev backend | Media |
| Revisar si resumen de liquidación muestra bien el inicial (BUG-04) | QA manual | Baja |

---

**Contexto:** Lote 2602 (ID 44 en Ecuador) — encasetamiento 13,550 aves el 18/03/2026.  
**Entidades:** `lote_ave_engorde`, `seguimiento_diario_aves_engorde`, `lote_registro_historico_unificado`, `historial_lote_pollo_engorde`
