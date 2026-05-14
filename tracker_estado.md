# Memoria de Desarrollo — Tracker Activo

**Feature Actual:** Deploy Cross-Platform Mac + Windows  
**Fase Actual:** Plan definido — pendiente implementación  
**Módulo:** `Makefile` + `backend/scripts/deploy-backend-ecs.sh` + `frontend/scripts/deploy-frontend-ecs.sh`  
**Archivo de tarea:** `fase_de_desarrollo/08_deploy_cross_platform.md`

---

> Feature anterior (en espera): BUG Conciliación Saldo Inicial Aves — `fase_de_desarrollo/07_bug_conciliacion_saldo_inicial_aves.md`

> Historial de features completadas: `tracker_historico/`

---

## Estado de Implementación — Deploy Cross-Platform (08)

| ID | Tarea | Estado | Notas |
|----|-------|--------|-------|
| T1 | Makefile: variables `OPEN_CMD` / `CHECK_PORT` según OS | ⏳ Pendiente | Usar `ifeq ($(OS),Windows_NT)` en Makefile |
| T2 | `deploy-backend-ecs.sh`: `sed` portable Mac/Linux | ⏳ Pendiente | Reemplazar `sed -i.bak` por bloque `darwin*/else`; eliminar `.bak` |
| T3 | `deploy-frontend-ecs.sh`: verificar `sed` portable | ✅ Ya implementado | El script ya tiene detección `darwin*/else` en L96-102 |
| T4 | Limpiar builder `sanmarino-builder` (buildx obsoleto) | ⏳ Pendiente | `docker buildx rm sanmarino-builder` — 1 comando manual |

### Fixes ya aplicados en esta sesión (pre-plan)
| Fix | Archivo | Descripción |
|-----|---------|-------------|
| Endpoint rename | `UsersController.cs` + `user.service.ts` | `admin-reset-password` → `reset-password` (AWS WAF bloqueaba "admin") |
| make instalado | PowerShell profile | GnuWin32 + PATH + UTF-8 (`chcp 65001`) |
| buildx → docker build | `deploy-backend-ecs.sh` + `deploy-frontend-ecs.sh` | Evita error TLS de cert corporativo en container buildx |

---

## Próximos Pasos

1. Implementar T4 (limpiar buildx): `docker buildx rm sanmarino-builder`
2. Implementar T2 (sed backend): editar `backend/scripts/deploy-backend-ecs.sh`
3. Implementar T1 (Makefile OS detect): editar `Makefile` raíz
4. QA: correr `make deploy-all` en Windows → correr en Mac y confirmar ambos OK

---

## Contexto de Feature Anterior (BUG-07 — en espera)

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
