# Tracker: Cruce Reproductora → Pollo Engorde
**Plan:** [cruce_reproductora_a_pollo_engorde_plan.md](fase_de_desarrollo/cruce_reproductora_a_pollo_engorde_plan.md)
**Fecha:** 2026-06-02
**Estado:** ✅ IMPLEMENTADO Y VALIDADO en local (T1–T6 pasan). Pendiente: deploy + 1 refinamiento menor.

---

## Tabla canónica
**`seguimiento_diario_aves_engorde`** — destino del cruce. (`_ecuador`/`_panama` son fantasmas, sin tocar.)

## Caso de prueba real
Lote pollo engorde **85** = 2 reproductora (repro 6 y 7) ≡ LOTE 94-1/94-2 del Excel. Cruce verificado contra el Excel.

---

## Checklist de implementación

### BD ✅
- [x] Migración EF `AddOrigenCruceToSeguimientoEngorde` (columna `origen_cruce`, idempotente) — aplicada local
- [x] Migración EF `AddFnCruceReproductoraEngorde` (función + trigger + índice, SQL inline) — aplicada local ⬅️ se aplica sola en deploy (patrón del proyecto)
- [x] Script espejo `backend/sql/fn_cruce_reproductora_a_engorde.sql` (referencia)
- [x] Pruebas T1–T6 ✅
  - T1 lote único (copia directa) ✅
  - T2 cruce 2 lotes (peso ponderado 56/72/90 = Excel) ✅
  - T3 días incompletos no se cruzan ✅
  - T4 editar reproductora → trigger recalcula (+100 → +100) ✅
  - T5 eliminar reproductora → cruce desaparece ✅
  - T6 inventario sin doble descuento (por diseño) ✅

### Backend C# ✅
- [x] DTO `SeguimientoLoteLevanteDto.OrigenCruce`
- [x] `MapToDto` Ecuador mapea `OrigenCruce`
- [x] Bloqueo Update/Delete manual de registros `origen_cruce=true` (mensaje claro)

### Frontend ✅
- [x] Filas de cruce read-only + badge "🔄 Auto" (detecta `createdByUserId === 'SYSTEM_CRUCE'`, sin tocar la fn crítica de 547 líneas)
- [x] Tabla diaria: columna "Consumo día (kg)" = total; + columnas "Consumo hembras/machos (kg)" SOLO en Panamá (`isPanama` por storage)
- [x] colspan del empty-state ajustado (+2 en Panamá)

### Ajuste tipo_alimento del cruce ✅ (validado vs Excel del usuario)
- [x] La fn extrae el nombre limpio del alimento: `'H: X / M: X'` → `'X'` (ej: "SUPER POLLITO PREINICIO-ELITE"), en vez de "Cruce reproductora"
- [x] Validado contra `Seguimiento_engorde_94_20260602.xlsx`: consH=316.994, consM=362, pesoH/M=56, alimento OK

---

## Decisiones aplicadas
- Cruce por EDAD: `edad = fecha_registro − fecha_encasetamiento` (día siguiente al encaset = edad 1).
- Aves vivas = inicio − Σ(mortalidad + selección + error) de edades < d.
- Solo edades 1..7. Día 8+ digitable normal.
- Registro destino read-only, `created_by_user_id='SYSTEM_CRUCE'`.

### Devolución de aves al lote tras 7 días ✅ (automática, cálculo dinámico)
- [x] `GetAvesDisponiblesAsync`: detecta si todos los reproductora tienen ≥7 registros
- [x] Si completos → revierte asignación: disponibles = inicial − mortCaja(lote+repro) − bajas seguimiento engorde (incluye cruce)
- [x] Si no completos → 0 (comportamiento actual)
- [x] Validado SQL: lote 85 → 0/0 actual; simulado 7d → H=28.473, M=27.639 (= inicial − bajas cruce)
- [x] Frontend: desglose por género visible (chips H/M) + mensajes condicionales

### Estado y detalle de lotes reproductora ✅
- [x] Estado pasa a "Cerrado" cuando `avesActuales<=0` OR `numRegistros>=7` (antes solo por aves agotadas)
- [x] Origen del estado documentado: `LoteReproductoraAveEngordeService.CalcularEstado` → frontend mapea Vigente→Abierto
- [x] DTO enriquecido: NumRegistros, EdadDias, AvesActualesHembras/Machos, SieteDiasCompletos
- [x] Tabla detalle lote engorde: + Edad, Registros (n/7), Estado Abierto/Cerrado, Apertura H/M, Actual H/M, Total + fila TOTAL
- [x] Cuadro informativo: lotes, aves apertura, aves vivas actuales, bajas acumuladas, recogida 7 días
- [x] Validado lote 85: ambos 7/7 → Cerrado; actuales 28.625 / 27.593 (= captura)

## Pendiente (menor / opcional)
- [ ] Bloquear ALTA manual en pollo engorde para fechas que ya tienen cruce (hoy: Update/Delete sí bloqueados; Create coexistiría sin chocar por índice parcial).
- [ ] Deploy a prod: aplicar migración + correr `backend/sql/fn_cruce_reproductora_a_engorde.sql` (la fn/trigger NO van en migración EF; se corren como script, igual que las otras fn del proyecto).

## Pendiente aparte (no abordar ahora)
- Limpieza entidades/tablas fantasma `_ecuador` y `_panama`.
