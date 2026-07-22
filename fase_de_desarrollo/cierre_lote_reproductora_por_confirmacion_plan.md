# Plan — Cierre del lote reproductora engorde por CONFIRMACIÓN (no por registro)

## Contexto / problema
El lote reproductora aves de engorde se **cierra automáticamente al completar 7 días REGISTRADOS**
(`numRegistros >= 7`). Al cerrarse, el front deshabilita el botón **Confirmar**
(`[disabled]="isLoteReproductoraCerrado"`). Resultado: un lote con los 7 días cargados pero **sin
confirmar** queda trabado — nunca se pueden confirmar sus registros y por lo tanto **nunca cruzan a
pollo engorde**. Incluso confirmando día a día, el día 7 (que dispara el cierre) jamás sería confirmable.

## Regla de negocio correcta (definida con el usuario)
1. El lote **cierra SOLO cuando los 7 días están CONFIRMADOS** (la confirmación es la que sincroniza
   hacia pollo engorde). Mientras haya registros pendientes, el lote sigue **Vigente**.
2. **Cierre 100% por confirmación**: se **elimina** `aves <= 0` como disparador de cierre (decisión del
   usuario). Un lote con aves agotadas y registros a medias queda "Vigente" hasta confirmar los 7.
3. El **"restante de aves"** hacia pollo engorde (`GetAvesDisponiblesAsync` → `sieteDiasCompletos`)
   también se libera **solo con los 7 CONFIRMADOS** (coherente con el cruce diario, que ya depende de
   la confirmación).

Con esto el flujo queda: registrar 7 días (lote Vigente) → confirmar uno por uno (cada confirmado cruza
vía trigger) → al confirmar el 7º el lote pasa a **Cerrado** y se libera el saldo de aves a pollo engorde.

## Enfoque arquitectónico
Cambio **backend-only**. El front NO se toca:
- `isSeguimientoCompleto` (bloquea *Crear*) usa `seguimientos.length >= 7` (registros) → sigue igual.
- `isLoteReproductoraCerrado` lee `estado`; con estado confirmado-based, durante la confirmación el lote
  es "Vigente" → el ✓ Confirmar se habilita solo. Cuando cierra, ya están todos confirmados → no hay
  botón que trabar. El `[disabled]` queda como guard inofensivo (refactor ≠ cambio de comportamiento).

La regla pura del estado se **extrae a `Application/Calculos`** (patrón CLAUDE.md) para poder testearla.

## Archivos a crear/modificar
1. **CREAR** `backend/src/ZooSanMarino.Application/Calculos/ReproductoraEngordeCalculos.cs`
   - `static class` con `CalcularEstado(avesEncasetadas, ventas, mort, sel, err, numConfirmados, dias=7)`
     → `(Estado, AvesActuales)`. Regla: `Estado = numConfirmados >= dias ? "Cerrado" : "Vigente"`.
     `AvesActuales = Max(0, encaset - mort - sel - err - ventas)` (se conserva para el saldo mostrado).
2. **MODIFICAR** `backend/src/ZooSanMarino.Infrastructure/Services/LoteReproductoraAveEngordeService.cs`
   - `ReproStats`: agregar `NumConfirmados`.
   - `GetReproStatsAsync`: `NumConfirmados = g.Sum(s => s.Confirmado ? 1 : 0)`.
   - `CalcularEstado` (privado): delega en `ReproductoraEngordeCalculos.CalcularEstado`; el 6º arg pasa a
     ser `numConfirmados`.
   - Call sites (5): pasar `st?.NumConfirmados ?? 0` en vez de `st?.Num ?? 0` (el `Map`/DTO conserva
     `Num` total para el flag "seguimiento completo").
   - `GetAvesDisponiblesAsync`: en el `Count(...)` interno de `nReproCompletos` agregar `&& s.Confirmado`.
3. **MODIFICAR** `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoDiarioLoteReproductoraService.cs`
   - `DeleteAsync` guard: reemplazar `cerrado = avesActuales <= 0 || numRegistros >= 7` por
     `cerrado = numConfirmados >= 7` (contar confirmados). Eliminar el cálculo muerto de bajas/ventas/
     avesActuales que solo alimentaba ese guard.
4. **CREAR** `backend/tests/ZooSanMarino.Application.Tests/ReproductoraEngordeCalculosTests.cs`

## Cambios de BD/SQL
**Ninguno.** La columna `confirmado` ya existe en prod (migración `20260722015730`, desplegada en
`8d1956b` / TaskDef :128). Solo cambia lógica de lectura.

## Reglas / invariantes que NO cambian
- Máx. 7 días de registro (`CreateAsync` topa en 7 filas).
- Confirmado = solo lectura (Editar bloqueado; para corregir, eliminar).
- Eliminar sigue gateado por reapertura con novedad (pero ahora "cerrado" = 7 confirmados, coherente con
  el estado que ve el front).
- `AvesActuales` (saldo mostrado) se sigue calculando igual.

## Casos de prueba (xUnit sobre la lógica pura)
- 0 confirmados → "Vigente".
- 6 confirmados → "Vigente".
- 7 confirmados → "Cerrado".
- aves agotadas (encaset - bajas = 0) con < 7 confirmados → sigue "Vigente" (decisión: no cierra por aves).
- `AvesActuales` = Max(0, encaset - mort - sel - err - ventas); nunca negativo.

## Efecto colateral positivo
El lote trabado hoy (7 registros, 0 confirmados) se **desbloquea solo** al desplegar: su estado se
recalcula a "Vigente" (0 < 7 confirmados) → el ✓ Confirmar se habilita. Sin tocar la BD.

## Validación
`cd backend && dotnet build` (0 errores/sin nuevas advertencias) + `dotnet test`. Deploy: aparte, con
confirmación explícita del usuario (flujo push a `main-produccion`, verificación post-deploy ECS).
