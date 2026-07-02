# Plan C1 — Indicadores de levante (postura Colombia) → función SQL

> Objetivo: mover el cálculo de indicadores semanales de levante del **front** a una **función SQL**,
> para que el front solo pinte. **Replicar EXACTO** el comportamiento actual (decisión del usuario):
> mismos números, incluidos los bugs históricos. Riesgo: aritmética; mitigación: test de equivalencia.

## Fuente de verdad (algoritmo actual del front)
`frontend/.../lote-levante/pages/tabla-lista-indicadores/tabla-lista-indicadores.component.ts`
- Recibe `@Input seguimientos: SeguimientoLoteLevanteDto[]` (crudos) + `selectedLote`.
- Los valores son **doubles sin redondear** (se formatean al pintar) → en SQL usar **`double precision` (float8)** para bit-exactitud con IEEE-754 de JS, **mismo orden de operaciones**.

### Agrupación en semanas
- `semana = floor(floor((fechaRegistro − fechaEncaset)/1día)/7) + 1`, acotado a `[1,25]`. Día de encaset = semana 1. Fechas a mediodía local (evita desfase UTC).
- Registros de cada semana ordenados por fecha (YYYY-MM-DD).

### Acumuladores iterativos (semana a semana, en orden)
Inicio: `avesAcum = lote.avesEncasetadas`; `mortAcum=0`; `selAcum=0`; `pesoAnterior = lote.pesoInicialH`; `pesoTablaAnterior=0`.
Tras cada semana: `avesAcum = avesFin`; `mortAcum += mortalidadSem`; `selAcum += seleccionSem`; `pesoAnterior = pesoCierre`; `pesoTablaAnterior = pesoTabla`.
→ **Dependencia secuencial** (pesoCierre arrastra el último peso conocido) ⇒ portar como **PL/pgSQL con FOR loop** (mapea 1:1 al `for` del TS; menor riesgo de divergencia que un CTE recursivo).

### Por semana (float8, sin redondear)
- `mortalidadTotal = Σ(mortH+mortM)`, `seleccionTotal = Σ(selH+selM)`, `consumoTotal(kg) = Σ(consumoKgH+consumoKgM)`, `errorSexajeTotal = Σ(errH+errM)`.
- `avesFin = avesInicio − mortalidadTotal − seleccionTotal`.
- **Peso/uniformidad**: tomar el ÚLTIMO registro de la semana con `pesoPromH>0 || pesoPromM>0` (si ninguno, el último registro). `pesoPromedio = (pH>0&&pM>0)?(pH+pM)/2 : (pH>0?pH:pM)`; si `<=0` ⇒ `pesoAnterior` (carry-forward). Igual para `unifReal` con uniformidadH/M.
- `consumoTotalGramos = consumoTotal*1000`; `avesPromedio=(avesInicio+avesFin)/2`; `diasConRegistro = nº registros`.
- `consumoDiarioPorAve = (avesProm>0 && dias>0) ? consumoTotalGramos/(avesProm*dias) : 0`.
- Guía por semana (`obtenerGuiaSemana` — PIN: confirmar qué día de la guía usa y tabla `guia_genetica_sanmarino_colombia` / Ecuador mixto): `consumoTablaPorAve = (guia.consumoH+guia.consumoM)/2`; `pesoTabla=(guia.pesoH+guia.pesoM)/2`; `unifTabla=guia.uniformidad`; `mortTabla=(guia.mortH+guia.mortM)/2`.
- `gananciaSemana = pesoPromedio − pesoAnterior`; `consumoTotalPorAve = avesProm>0 ? consumoTotalGramos/avesProm : 0`; `conversionAlimenticia = gananciaSemana>0 ? consumoTotalPorAve/gananciaSemana : 0`; `gananciaDiariaAcumulada = gananciaSemana/7`.
- `gananciaTabla = (pesoTabla>0 && pesoTablaAnterior>0) ? pesoTabla−pesoTablaAnterior : 0`.
- Porcentajes (base avesInicio): `mortalidadSem`, `seleccionSem`, `errorSexajeSem` = total/avesInicio*100 (0 si avesInicio=0); `mortalidadMasSeleccion = mortalidadSem+seleccionSem`.
- `eficiencia = consumoTotalPorAve>0 ? gananciaSemana/consumoTotalPorAve : 0`; `supervivencia = avesInicio>0 ? avesFin/avesInicio : 0`; `ip = vpi = eficiencia*supervivencia`.
- Acumulados %: `mortalidadAcum += mortalidadSem`, `seleccionAcum += seleccionSem`, `mortalidadMasSeleccionAcum = suma`.
- `pisoTermicoVisible = !!guia.pisoTermicoRequerido`.
- `difPesoPct = pesoTabla>0 ? (pesoPromedio−pesoTabla)/pesoTabla*100 : 0`.
- Campos de contexto (region/granja/nave/sublote/observaciones): del lote/último registro → pueden venir del JOIN o quedarse en front (no son cálculo).

## Enfoque arquitectónico
1. `fn_indicadores_levante_postura(p_lote_id int, p_company_id int) RETURNS TABLE(...)` (float8), PL/pgSQL con loop de semanas (espejo del TS).
2. DTO `IndicadorSemanalLevanteDto` + endpoint `GET SeguimientoLoteLevante/por-lote/{id}/indicadores` que delega `SqlQueryRaw`.
3. Front: `tabla-lista-indicadores` y `graficas-principal` consumen el DTO (solo pintan); se elimina el cómputo cliente y sus llamadas a la guía.
4. **Test de equivalencia** (crítico, va PRIMERO): fijar la salida actual del front para lotes reales (P-K345A y otro) como golden y comparar contra la fn, campo a campo, tolerancia 0 (o epsilon float mínimo).

## Orden de ejecución (un paso por ciclo)
- [ ] Paso 0 — Golden: capturar salida actual del front (JSON de `IndicadorSemanal[]`) para 2 lotes reales vía E2E; guardar como fixture.
- [ ] Paso 1 — `fn_indicadores_levante_postura` en `/backend/sql/` + migración idempotente; probar en local.
- [ ] Paso 2 — Test de equivalencia fn vs golden (ajustar hasta 0 diferencias).
- [ ] Paso 3 — DTO + endpoint que delega en la fn.
- [ ] Paso 4 — Front consume el endpoint; quitar cómputo cliente (dejar solo formato/pintado). Validar E2E que la tabla se ve idéntica.
- [ ] Paso 5 — Repetir para `graficas-principal` (mismos datos) y C2 (producción).

## Riesgos / salvaguardas
- Bit-exactitud JS↔Postgres: usar float8 + mismo orden; el golden test lo garantiza. NO usar numeric/round salvo donde el front redondea (no lo hace en el cálculo).
- Guía: confirmar día exacto usado por `obtenerGuiaSemana` (Colombia vs Ecuador mixto) — punto a pinar en Paso 1.
- No tocar vistas Power BI. Migración idempotente. `main` intacto.
- Postura Colombia arrastra bugs de comparación vs guía → se REPLICAN exacto (decisión usuario); documentarlos como "conocidos" para tratarlos aparte.
