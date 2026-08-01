# Plan — Seguimiento Diario de PRODUCCIÓN: fn SQL canónica + reducción de services + invariantes

**Fecha:** 2026-07-31 · **Tracker:** bloque propio al final de [`tracker_estado.md`](../tracker_estado.md)
**Objetivo:** replicar en postura el patrón de engorde (`fn_seguimiento_diario_engorde` v13): una función SQL
`LANGUAGE sql` como ÚNICA fórmula de la grilla diaria y sus derivados, lecturas pesadas empujadas a la BD,
espejo de huevos con UN solo dueño, y `ProduccionService` partido en partials con cálculo puro testeado.

> Exploración previa: 8 agentes en paralelo (service, patrón engorde, las 4 fns SQL existentes, espejo,
> flujos de aves, lectores/reportes, frontend, BD viva). Evidencia clave incorporada abajo con archivo:línea.

---

## 0. Estado actual (hallazgos que definen el diseño)

1. **La grilla hoy NO deriva nada**: `ListarSeguimientoAsync` (ProduccionService.cs:789) devuelve filas crudas;
   el único cálculo es `ConsumoKg = ConsKgH + ConsKgM` (:1051) y además falsea `CreatedAt: e.Fecha, UpdatedAt: null`
   (:1058-1059). Saldos/acumulados/% postura viven repartidos en `informacion-lote` (C#, :949-950) y en
   `fn_indicadores_produccion_postura` (SQL semanal).
2. **TRES fórmulas divergentes del saldo de aves**:
   - GET `informacion-lote` (:949-950): `Math.Max(0, iniciales − (mort+sel) − salidas_mov + entradas_mov)` —
     **sin** error_sexaje, **con** `SelM`, y **PERSISTE** en un GET (self-heal :953-958).
   - Escritores incrementales (`SeguimientoProduccionService.AplicarDescuentoLppAsync` :345-362 y
     `fn_migracion_seguimiento` paso 3 :436-447): `± (mort+sel+err)` con clamp.
   - fn semanal (20260728160000 :601-602): hembras `−(mort+sel)`, machos `−mort` (sin `sel_m`, sin err, sin movimientos).
   - **Inconsistencia viva medible**: lote 130 quedó en 9.039 H (carga masiva, con err=7); el próximo GET del
     header lo reescribiría a **9.046** (fórmula sin err). Hay que elegir dueño (decisión D4).
3. **Espejo de huevos**: el trigger `tr_espejo_huevo_produccion_aiud` vive sobre `seguimiento_diario_levante`
   y está **muerto** (0 filas `tipo='produccion'` en esa tabla; verificado en BD). El dueño de facto es
   `EspejoHuevoProduccionSyncService.RecalcularEspejoHuevoProduccionAsync` (absoluto/idempotente, 24 SumAsync),
   paridad exacta espejo↔Σ verificada en los 4 LPP con datos. `historico_semanal` está **vacío/NULL en el 100 %**
   de las filas y tiene **cero lectores** (grep front+back+sql). Decisión D1.
4. **Hack de venta**: `MovimientoAvesService.SeguimientoDiario.cs:317-376` escribe `SelH`/`MortalidadM`
   **negativos**. En el dump de prod hay **0 filas negativas** (el hack nunca se ejerció en datos reales) ⇒ el
   arreglo no necesita backfill (re-verificar en prod al desplegar). `EliminarMovimientoAsync` no revierte el ±
   (huérfanas posibles a futuro). Decisión D3.
5. **Campos que el modal manda y el back descarta** (round-trip roto silencioso): `errorSexajeHembras/Machos`
   (SÍ hay columnas; solo persiste en el merge de arrastre :571-572), `ciclo` (no hay columna; ya vive en
   `lotes.ciclo_produccion`), `uniformidadHembras/Machos` + `cvHembras/cvMachos` (no hay columnas, cero
   consumidores aguas abajo). La UI repinta 0/'Normal'/vacío al reabrir. Decisión D2.
6. **Bloque común copiado 3×** en las fns lectoras (F2 indicadores / F3 clasificación / F4 RA Pesadas):
   UNION dual-fuente (`seguimiento_diario_levante tipo='produccion'` + `seguimiento_diario_produccion`) +
   dedup `DISTINCT ON ((ts AT TIME ZONE 'America/Bogota')::date) ... ORDER BY ..., ts` (gana el más temprano) +
   semana `((reg_date − ref)/7)+1` + `ref = COALESCE(lev.encaset, lpp.encaset, lpp.inicio_prod)`.
   En la BD actual la tabla legacy tiene **0 filas de producción** ⇒ adoptar el bloque canónico en la fn nueva
   produce **0 diferencias** hoy y unifica a futuro.
7. **BD viva**: sin triggers sobre `seguimiento_diario_produccion`; **NO existe** el UNIQUE (lote_id, fecha_registro)
   que declara `SeguimientoProduccionConfiguration:232` (0 duplicados hoy, ni por timestamp ni por día UTC);
   anclas horarias mixtas (17:00Z masivo viejo, 05:00Z carga masiva, 12:00Z arrastre) — todas seguras por día
   UTC y por día Bogotá; 1 fila huérfana (id=1, lote_id=7 inexistente, company_id=0); 612 filas / 5 lotes local.
8. **Otros bugs confirmados en el camino** (se corrigen en fase 3): alta/edición guardan `request.FechaRegistro`
   sin `AnclarMediodiaUtc` (:420/:717); la edición no re-valida duplicado por día y **borra la marca de arrastre**
   (:746 reconstruye metadata sin `CopiarMarcaArrastre` ⇒ un re-arrastre dejaría de ser idempotente);
   `ActualizarSeguimientoAsync` no valida `CompanyId` de la fila que edita (:679-681); la grilla LPP no muestra
   filas de traslado TSD (tienen `lote_postura_produccion_id` NULL) y el GET no serializa `traslado_*` aunque el
   front tiene columnas para eso (siempre 0, tabs-principal.html:299-302).
9. **Liquidación de producción**: módulo ELIMINADO, nada vivo lee la tabla ⇒ el patrón "liquidación congelada"
   de engorde **no aplica** hoy (fase 5 = solo esta conclusión).
10. **Vistas Power BI**: ninguna lee `seguimiento_diario_produccion` (única de postura es
    `vw_guia_genetica_por_lote_postura`, solo guía). Nada que proteger ahí.

---

## D. Decisiones del usuario — TOMADAS (2026-08-01)

> **D1 = (b)** recálculo C# único dueño (trigger legacy retirado) · **D2 = persistir TODOS** los
> campos del modal (errorSexaje end-to-end + columnas nuevas ciclo/uniformidad-CV por sexo) ·
> **D3 = opción B** venta estilo carga masiva (sin ±Sel; traslados MOV- a columnas traslado_*;
> eliminar Completados bloqueado) · **D4 = saldo CON error de sexaje** (el header delega en la fn).
> Además, hallazgo de implementación: la fn cuenta movimientos SOLO desde fecha_inicio_produccion —
> los previos son del levante y ya viven en las aves iniciales del LPP (el GET viejo los descontaba
> DOS veces: lote 130 daba 8.646 en vez del 9.039 validado por el E2E). Tabla original de análisis:

| # | Decisión | Recomendación | Bloquea |
|---|---|---|---|
| D1 | Espejo de huevos: (a) trigger AIUD nuevo sobre `seguimiento_diario_produccion` vs **(b) recálculo C# único dueño + retirar trigger legacy** | (b): el dinámico depende también de `traslado_huevos` (un trigger no puede ser dueño único), el recálculo absoluto ya es la realidad operativa (paridad 4/4), y la carga masiva ya explota la idempotencia (1 recálculo vs N triggers + JSONB O(N²)). `historico_semanal` queda documentado como muerto (DROP en migración aparte solo con OK explícito) | Fase 4 |
| D2 | Campos descartados del modal | Mixta: **persistir `errorSexaje*`** (columnas ya existen; DTO+create/update+proyección) y **quitar del form** `ciclo` (mostrarlo read-only desde el lote) y `uniformidad/CV por sexo` (sin columna ni consumidor) | Fases 2-3 (DTO/front) |
| D3 | Venta negativa | **Opción B**: eliminar el ±Sel del módulo vivo (paridad con carga masiva: descuento LPP + observaciones + auditoría `movimiento_aves`), migrar los traslados `MOV-` a columnas `traslado_*` (mismo upsert que TSD) y **bloquear eliminación de movimientos Completados** (patrón TSD). Sin backfill (0 filas negativas en prod dump; re-censar en deploy) | Fase 1 (fórmula saldo limpia) |
| D4 | Fórmula ÚNICA del saldo de aves | `GREATEST(0, base − Σ(mort+sel+err) − Σ mov_out + Σ mov_in)` — **CON error de sexaje** (semántica de los escritores incrementales y de la carga masiva validada en el E2E). Implica alinear el GET `informacion-lote` a la fn (header del lote 130: se queda en 9.039, deja de "sanar" a 9.046) | Fase 1 (columnas saldo) y 2 |

`base = COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0)`; `mov_out` = `movimiento_aves` Completado
no borrado con el lote como origen (Venta+Traslado+Retiro); `mov_in` = Traslado con el lote como destino.
`aves_h_actual` pasa a ser **derivado verificable**, nunca fuente.

---

## Fase 1 — `fn_seguimiento_diario_produccion` v1 (la única fórmula)

**Patrón:** calco de engorde — `LANGUAGE sql STABLE`, un solo SELECT con CTEs + window functions (el inlining
en CROSS JOIN LATERAL es real; plpgsql fue ×2.8 más lento y `RETURN QUERY` no aplica assignment casts →
castear TODO agregado `::INT/::FLOAT8/::BIGINT`). Orden único `ORDER BY fecha, COALESCE(seg_id, 0)`.

**Firma:** `fn_seguimiento_diario_produccion(p_lote_postura_produccion_id INT, p_lote_id INT)`
— exactamente uno no-NULL; la resolución LPP/legacy y el gate de alcance quedan en C# (fail-closed, patrón engorde).

**Universo v1 (paridad estricta):** filas de las dos fuentes con el bloque canónico (UNION dual-fuente +
`DISTINCT ON` día Bogotá, gana el timestamp más temprano). Rama LPP filtra por `lote_postura_produccion_id`
(igual que hoy la grilla y las fns) ⇒ las filas TSD (lpp NULL) siguen invisibles en rama LPP — **deuda
documentada** para una v2 con decisión explícita, no cambio silencioso. Sin filas movimiento-only en v1
(los movimientos afectan el saldo vía acumulados aunque no tengan fila).

**Columnas (~55, snake_case):**
- Identificación: `seg_id BIGINT, fecha DATE, fuente TEXT ('sdp'|'sdl_legacy'), lote_id INT, lote_postura_produccion_id INT, company_id INT`
- Tiempo: `edad_dias INT, semana INT` (cruda `((fecha − ref)/7)+1`, **sin** piso 26 ni corte 25: el corte es
  del consumidor — F3 Santa Reyes no corta, R3)
- Crudos aves: `mortalidad_hembras, mortalidad_machos, sel_h, sel_m, error_sexaje_hembras, error_sexaje_machos INT`
- Consumo: `cons_kg_h, cons_kg_m, consumo_total_kg FLOAT8, tipo_alimento TEXT`
- Huevos: `huevo_tot, huevo_inc INT` + 11 categorías `INT`, `peso_huevo FLOAT8`
- Derivados huevos: `huevo_tot_acum BIGINT, huevo_inc_acum BIGINT, pct_postura_dia FLOAT8`
  (`= huevo_tot / aves_h_inicio_dia * 100`, 0 si no hay hembras — misma convención hen-day de F2 pero diaria)
- Movimientos del día (desde `movimiento_aves`): `mov_venta_h, mov_venta_m, mov_retiro_h, mov_retiro_m,
  mov_traslado_in_h, mov_traslado_in_m, mov_traslado_out_h, mov_traslado_out_m INT`
- Saldo (según D4): `aves_h_inicio_dia, aves_m_inicio_dia, saldo_aves_h, saldo_aves_m INT` con ventanas
  `w_ord`/`w_prev` (patrón engorde :763-777)
- Traslado crudo de la fila: `es_traslado BOOL, traslado_direccion TEXT, traslado_ingreso_hembras/machos,
  traslado_salida_hembras/machos INT, lote_destino_id, granja_destino_id INT`
- Pesaje: `peso_h, peso_m, uniformidad, coeficiente_variacion FLOAT8, observaciones_pesaje TEXT`
- Agua: `consumo_agua_diario, consumo_agua_ph, consumo_agua_orp, consumo_agua_temperatura FLOAT8`
- Otros: `etapa INT, observaciones TEXT, metadata JSONB, created_by_user_id INT, created_at, updated_at TIMESTAMPTZ`

**Reglas de fecha:** corte de día SIEMPRE `AT TIME ZONE 'America/Bogota'` (consistencia con F2/F3/F4 para que
el re-source no mueva días); jamás `date_trunc` dependiente de sesión. Movimientos por día calendario Bogotá
de `fecha_movimiento`.

**Espejo C# como especificación ejecutable:** `Application/Calculos/SeguimientoDiarioProduccionCalculos.cs`
(static, sin EF): dedup, semana, saldo, acumulados, % postura — con tests xUnit y números testigo reales
(lote 130: 9.495→9.039 H / 929→902 M con err; Σ huevos 25.330/24.630; lote 13/14 spot-checks contra la BD).
La fn es la dueña; el C# es el contrato (regla «una sola fórmula por número»).

**Migración:** `20260801130000_AddFnSeguimientoDiarioProduccion` — `Up()` = `CREATE OR REPLACE` (SQL en partial
`.Fn.cs` raw string), `Down()` = `DROP FUNCTION IF EXISTS`, Designer **clonado** del snapshot vigente
(`20260801023000`), sin tocar ModelSnapshot. Fuente canónica con changelog en
`backend/sql/fn_seguimiento_diario_produccion.sql`.

**Archivos:** `backend/sql/fn_seguimiento_diario_produccion.sql` (nuevo) ·
`Infrastructure/Migrations/20260801130000_*.cs + .Fn.cs + .Designer.cs` (nuevos) ·
`Application/Calculos/SeguimientoDiarioProduccionCalculos.cs` (nuevo) ·
`tests/ZooSanMarino.Application.Tests/SeguimientoDiarioProduccionCalculosTests.cs` (nuevo).

## Fase 2 — Conmutar lecturas a la fn

**2a. Grilla** (`GET /api/Produccion/seguimiento`): el service pasa a
`SqlQueryRaw<SeguimientoProduccionTablaFilaDto>("SELECT * FROM fn_seguimiento_diario_produccion({0}::int, {1}::int)")`
tras el gate de alcance; mapeo a `SeguimientoItemDto` **byte a byte** con el contrato actual (incluye conservar
`CreatedAt = fecha` y `UpdatedAt = null` — la mentira actual es parte de la paridad; corregirla sería otra decisión).
El DTO se EXTIENDE (aditivo) con: `errorSexaje*` (si D2), `saldoAvesH/M`, `pctPosturaDia`, acumulados y los
`traslado*` que el front ya espera (fix de las columnas siempre-0). Orden descendente y paginación en memoria
se conservan (hoy el front trae todo con size=0).
**2b. Re-source de las fns semanales** (elimina el bloque copiado 3×): `fn_indicadores_produccion_postura`,
`fn_clasificacion_huevo_items_produccion` y `fn_resumen_semanal_ra_pesadas_produccion` reemplazan sus CTEs
`crudos/dedup` por `FROM fn_seguimiento_diario_produccion(...)` (RA: `CROSS JOIN LATERAL` por LPP, patrón
Reporte de Costos `AS MATERIALIZED`). **Su aritmética semanal NO se toca** (saldo semanal sin `sel_m`/err es
desviación preservada de F2/F4 — R1; la fn diaria expone crudos y cada fn semanal sigue aplicando SU regla).
Una migración por fn con `Down()` = versión anterior verbatim.
**2c. GET `informacion-lote`**: delega el saldo en la fn (última fila: `saldo_aves_h/m`) según D4; el self-heal
persiste desde la fn (patrón `SaldoAlimentoEngordeAplicador`: escribir DESDE la fn, `IS DISTINCT FROM`).
**Fuera de alcance en esta tanda** (evaluados, con veredicto en la exploración): ReporteTecnicoService viejo
(fórmula de % mortalidad divergente — cambiar números exige decisión propia), ReporteContableService e
IndicadorEcuadorService (candidatos fuertes a LATERAL; plan aparte).

**Gates obligatorios de la fase 2:**
- `backend/sql/verificar_paridad_seguimiento_produccion.sql` (nuevo, modelo `verificar_paridad_saldo_engorde.sql`):
  congela línea base de la fn sobre TODOS los lotes de TODAS las empresas y diffea corrida contra corrida
  (tolerancia 0.001) + invariante «ninguna fila desaparece».
- Diff JSON del GET de grilla e informacion-lote antes/después (smoke HTTP, lotes 13/14/130/124): 0 diferencias
  salvo las columnas nuevas y lo decidido en D2/D4 (justificación escrita por número).
- Salida de las 3 fns semanales congelada antes/después: **byte a byte idéntica** en todas las empresas.
- `EXPLAIN ANALYZE` antes/después de: grilla (query EF actual vs fn), F2 por lote, F4 resumen. Umbral: no
  empeorar >15 %.

## Fase 3 — Reducción de services (refactor ≠ cambio de comportamiento)

- **Partials** (patrón movimientos-pollo-engorde, namespace plano): `ProduccionService.cs` (1539 líneas) →
  ancla (usings/ctor/interfaz/helpers) + `Funciones/ProduccionService.Seguimiento.cs` (alta/edición/borrado + merge
  arrastre) + `Funciones/ProduccionService.Consultas.cs` (grilla/por-id/informacion-lote) +
  `Funciones/ProduccionService.Lotes.cs` (lotes producción). Partición completa, doc-comments intactos.
- **Cálculo puro a `Application/Calculos/`** con tests: lo extraíble sin cambiar aritmética (derivación
  huevo_tot/inc ya está en `HuevosClasificacion`; conversión g→kg se extrae **conservando cada variante
  double/decimal por sitio** — unificarlas cambiaría bits: prohibido).
- **Espejo**: colapsar los 24 `SumAsync` en 2 agregaciones `GroupBy` (misma cifra, 24→2 round-trips) y resolver
  la empresa **por datos del LPP** (fail-closed) en vez de `ICurrentUser` (hoy un contexto distinto saltea el
  recálculo en silencio).
- **Fixes puntuales** (bugs del §0.8): anclar `FechasPuras.AnclarMediodiaUtc` en alta/edición; edición re-valida
  duplicado por día (rango, excluyéndose) y **preserva la marca de arrastre**; `ActualizarSeguimientoAsync`
  valida `CompanyId` de la fila editada; `ObtenerLotesProduccionAsync` proyecta en SQL (hoy 3 Include + Select
  en memoria).
- Validación: `dotnet build` 0/0 · `dotnet test` verde (1.501 + nuevos) · smoke.

## Fase 4 — Triggers e invariantes

- **Espejo (según D1=b):** migración `RetirarTriggerEspejoHuevoLegacy` (`DROP TRIGGER IF EXISTS ... ON
  seguimiento_diario_levante; DROP FUNCTION IF EXISTS fn_espejo_huevo_produccion_upsert();`), actualizar
  `backend/sql/trigger_espejo_huevo_produccion_seguimiento_diario.sql` y README para que nadie lo reinstale;
  **bloquear `tipo='produccion'`** en el CRUD genérico dormido `SeguimientoDiarioService` (única vía real de
  disparar el trigger hoy); `historico_semanal` queda documentado como columna muerta (DROP solo con OK aparte).
- **Cuadre de lectura** (patrón «el cuadre se mira»): consulta/función `fn_cuadre_espejo_huevo_produccion`
  que compare espejo vs Σ fuentes y devuelva descuadrados=0 (mitiga la ventana de doble SaveChanges del
  recálculo; endpoint opcional en tanda posterior).
- **Índice único** que declara `SeguimientoProduccionConfiguration:232` y la BD no tiene: migración **defensiva**
  `IndiceUnicoSeguimientoProduccionDia` — DO block que verifica duplicados; si hay, `RAISE WARNING` y NO crea
  (jamás tirar el arranque de prod); si no hay, crea `UNIQUE (lote_id, fecha_registro)` (alinea modelo↔BD) +
  `UNIQUE (lote_id, ((fecha_registro AT TIME ZONE 'UTC')::date))` (el invariante REAL de un-registro-por-día;
  `timezone(text,timestamptz)` es immutable). Local hoy: 0 duplicados verificados.
- **Venta (según D3):** quitar el ±Sel del módulo vivo, traslados `MOV-` a columnas `traslado_*`, bloquear
  eliminación de Completados. Censo en deploy: `sel_h<0 OR mortalidad_machos<0` (esperado 0, como en el dump).

## Fase 5 — Congelamiento de liquidación (solo análisis)

**Conclusión: NO aplica.** La «Liquidación Técnica Producción» fue eliminada; `LiquidacionTecnicaService` vivo
es solo levante; el cierre del LPP es un estado sin foto de números. El patrón congelado de engorde tendría
sentido recién si se re-crea una liquidación de producción — cuando exista ese módulo, la fn de esta tanda ya
deja listo el molde (mismas columnas/orden para una futura tabla `_congelada_fila` + UNION ALL gateado).

---

## Casos de prueba (resumen)

1. xUnit `SeguimientoDiarioProduccionCalculos`: dedup (más temprano gana), semana (bordes múltiplos de 7),
   saldo con/sin movimientos, clamp 0, % postura (0 hembras), acumulados; testigos lote 130 (9.039/902) y 13/14.
2. Paridad fila a fila TODAS las empresas (script + diffs JSON de grilla/informacion-lote/fns semanales) —
   0 diferencias o justificación escrita.
3. Espejo: alta/edición/borrado/traslado/carga masiva → cuadre exacto; reimport idempotente; cuadre-fn = 0.
4. Índice único: duplicar día en local → violación esperada; migración defensiva con duplicado sintético → WARNING sin crear.
5. Smoke HTTP local (backend propio :5499, JWT + X-Secret-Up minteados, lote 130) para grilla, informacion-lote,
   indicadores, clasificación, RA resumen.
6. `dotnet build` 0/0 · `dotnet test` verde · `yarn build` 0 errores si se toca front (D2).

## Riesgos y mitigaciones

- **R1 saldos múltiples**: la fn no fusiona las desviaciones semanales (F2/F4 conservan su regla); solo el
  saldo DIARIO se unifica según D4, con gate fila a fila.
- **R4 timezone**: corte Bogotá en la fn = mismo criterio de las lectoras; anclas existentes verificadas seguras.
- **R5 dedup**: contrato «gana el más temprano» se hereda tal cual.
- **R6 perf multipaís**: fn set-based por lote + LATERAL inlineable; EXPLAIN gate ±15 %.
- **R7**: la fn de migración (escritora) NO se toca; su matching por `lote_id crudo + fecha` se conserva.
- **R8/R10**: desempate guía '25P' y WEEKNUM Excel quedan DENTRO de las fns semanales (la diaria no los asume).
- Deudas documentadas (no tocadas): fila huérfana id=1 (lote 7, company 0), defaults `(now() AT TIME ZONE 'utc')`
  sobre timestamptz (+5 h en auditorías), `SeguimientoProduccionService` legacy con ancla a medianoche (con el
  índice por día su INSERT duplicado pasaría a fallar limpio — deseable), hueco Reporte Contable Mov. Huevos
  (solo lee la tabla legacy), universo TSD invisible en rama LPP (candidata v2).

## Validación final y commit

Commit acotado con `git add` explícito de los archivos de esta tanda (NUNCA `git add -A`), sin footer de
atribución. BD local consistente; backend de smoke detenido; sin procesos huérfanos.
