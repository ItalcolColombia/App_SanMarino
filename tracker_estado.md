# Tracker — Traslado de aves desde seguimiento: fechas puras a MEDIODÍA + match por día calendario

**Plan:** [`fase_de_desarrollo/traslado_aves_seg_fechas_mediodia_plan.md`](fase_de_desarrollo/traslado_aves_seg_fechas_mediodia_plan.md)
**Fecha:** 2026-07-31

Objetivo: `TrasladoAvesDesdeSegService` deja de escribir fechas puras a medianoche (Npgsql legacy las relee
en el día ANTERIOR en Bogotá) — ancla a mediodía (patrón carga masiva `3453b09`) y matchea la fila diaria
existente por día calendario (rango ±1 día + recorte en memoria) para que traslado UI y carga masiva del
mismo día se detecten mutuamente. Refactor de correctitud de fechas: cero cambios de cantidades/lógica.

## Código

- [x] Orquestador `EjecutarTrasladoDesdeSegAsync`: `fechaAncla = FechasPuras.AnclarMediodiaUtc(...)` reemplaza a `fechaDate`; `FechaMovimiento = fechaAncla`
- [x] `AplicarSalidaLevanteAsync`: match por rango de día calendario (helper `RangoDiaCalendario`) + `Fecha = fechaAncla` en fila nueva
- [x] `AplicarIngresoLevanteAsync`: ídem
- [x] `AplicarSalidaProduccionAsync`: ídem + `FechaTraslado = fechaAncla`
- [x] `AplicarIngresoProduccionAsync`: ídem

## Validación

- [x] `cd backend && dotnet build` — 0 errores, 0 advertencias
- [x] `cd backend && dotnet test` — verde (1.480 Application + 1 Domain)
- [x] Smoke local 1 (backend :5002 Dev, JWT + X-Secret-Up minteados, LPL 7→6 Sanmarino, fecha enviada a MEDIANOCHE UTC): `movimiento_aves.fecha_movimiento` = **12:00 UTC** exacto; `seguimiento_diario_levante.fecha` = mediodía anclado (17:00 UTC = 12:00-05 por la TZ Bogotá de la sesión local; en prod, sesión UTC ⇒ 12:00 UTC — mismo almacenamiento que ya producen las filas de la carga masiva por el mismo mapeo EF; el día calendario queda a salvo en cualquier TZ)
- [x] Smoke local 1b: SEGUNDO traslado el mismo día → EXTIENDE las filas existentes (500/50 acumulado, 0 filas nuevas)
- [x] Smoke local 2: import carga masiva (hoja Movimientos Aves, Salida misma fecha/cantidades que el traslado UI) → **filasOmitidas=1** (la idempotencia detecta el TSD; antes del fix duplicaba) y la fila del día nuevo sí aplica (MGA a 12:00 UTC, LPL descontado exacto)
- [x] Smoke local 3: traslado UI sobre el día con fila creada por la carga masiva → EXTIENDE esa fila (111+50=161 H / 11+5=16 M, count=1 fila del día)
- [x] BD local restaurada al snapshot (LPL 6/7/8 idénticos, segs 114:38/116:144/115:0, movimiento_aves max id 18, cohortes 0, migracion_masiva max 164, espejo producción intacto) + backend del smoke detenido (puerto 5002 libre)
- [x] Commit acotado (sin footer de atribución)

---

# Tracker — Carga masiva Levante: VENTA de aves en la hoja + E2E de ciclo completo en NIZA I

**Plan:** [fase_de_desarrollo/carga_masiva_levante_movimientos_aves_plan.md](fase_de_desarrollo/carga_masiva_levante_movimientos_aves_plan.md) (continuación del bloque commiteado en `3453b09`)
**Fecha:** 2026-07-31 (2ª ronda)

**Pedido del usuario:** validar que la plantilla cubra TODO el ciclo de levante (seguimiento, traslados, VENTA de aves, ingreso y traslado de alimento), crear en Sanmarino la estructura granja NIZA → núcleo 1 → galpón 1 → lote nuevo, armar el archivo de carga en el Escritorio, importarlo y cuadrar al final aves vivas + stock de alimento + ventas + triggers.

## Tipo «Venta» en la hoja Movimientos Aves
- [x] Enum `MovimientoAvesMigracion.Venta` + sinónimos (`venta`, `ventas`, `venta de aves`, `venta aves`); columna nueva **Motivo** (8 columnas, opciones Salida/Ingreso/Venta)
- [x] Venta: contraparte prohibida (Advertencia si viene), descuento de aves con clamp 0, `venta_aves_cantidad += H+M` y `venta_aves_motivo` en la fila diaria (espejo del módulo Movimiento de Aves), SIN columnas de traslado ni acumulados; auditoría `movimiento_aves` tipo Venta sin destino con `motivo_movimiento`
- [x] Idempotencia extendida: la clave lee Traslado Y Venta (venta = lote como origen); proyección de saldo cuenta las ventas como salidas
- [x] Tests: 4 sinónimos de Venta + esquema 8 columnas / 3 opciones — `dotnet test` **1.483 verdes**

## Bugs cazados por el E2E (corregidos)
- [x] 🔴 `tipo_alimento` derivado de 2 alimentos con nombres largos supera el varchar(100) y la fn moría entera con 22001 → truncado a 100 en `ResolverTipoAlimento`
- [x] 🔴 **PREEXISTENTE (afectaba también a engorde)**: `ClavesMovimientosExistentesAsync` mapeaba el movement_type literal `"Traslado"`, que el servicio nunca emite (los reales: `TrasladoSalida`/`TrasladoEntrada`/`TrasladoInterGranjaSalida`) ⇒ un traslado ya aplicado era invisible al reimportar y el balance lo volvía a contar, rechazando por stock un archivo medio aplicado → mapeadas las patas reales (entrada = destino; inter-granja = destino en `From*`; `TrasladoInterGranjaEntrada` = Recepción) + granjas ORIGEN en el filtro

## E2E ciclo completo (estructura por API + archivo en el Escritorio + import real)
- [x] Estructura creada por API (backend propio :5499, JWT + X-Secret-Up minteados): núcleo `1` y galpón `1` en **NIZA I** (granja 4) + lote **LOTE NIZA E2E** (id 130, encaset 2026-06-01, 10.000 H / 1.000 M, raza AP 2026) — el trigger `lotes→lote_postura_levante` creó el espejo (LPL 22) y el lote salió elegible
- [x] **Archivo en el Escritorio**: `Carga_Masiva_Seguimiento_Levante_LOTE_NIZA_E2E.xlsx` — plantilla real del sistema (5 hojas) llenada: 7 días (mortalidad 10/2, selección 5/1, pesos, uniformidad) + alimento por ítem de inventario (2 alimentos H desde el día 5 + 1 M) + huevos días 6-7 + hoja Alimento (Ingreso 3.000 kg, Ingreso 200 kg, Traslado inter-granja 500 kg → LA ESMERALDA) + hoja Movimientos Aves (Venta 100/50 con motivo, Salida 500 → lote 115, Ingreso 200 ← lote 116)
- [x] Validar (dry-run) con saldos proyectados exactos → **Import Procesado 7 filas** → reimport **0 procesadas / 13 omitidas** (7 días + 3 alimento + 3 aves)

## Cuadre final (todo verificado en BD)
- [x] **Aves vivas**: `10.000 − 70 mort − 35 sel − 100 venta − 500 salida + 200 ingreso = 9.495 H` · `1.000 − 14 − 7 − 50 = 929 M` — exacto en `aves_h_actual`/`aves_m_actual`; acumulados LPL 200 ingreso / 500 salida
- [x] **Alimento**: ítem 150: `3.000 − 500 traslado − 2.100 consumo = 400 kg` · ítem 151: `200 − 150 = 50` · ítem 155: `6.000 − 210 = 5.790` · **17 movimientos Consumo = 2.460 kg** con la referencia byte a byte del alta manual · tránsito de 500 kg hacia LA ESMERALDA en estado Tránsito (pendiente de recepción por pantalla)
- [x] **Ventas**: día 3 con `venta_aves_cantidad = 150` y motivo «Venta descarte E2E»; auditoría MGA tipo Venta sin destino
- [x] **Triggers**: espejo LPL al crear el lote ✔ · histórico unificado 20/20 movimientos con su fila, 0 huérfanos, `lote_ave_engorde_id` NULL (granja sin engorde) ✔
- [x] Huevos en semana 1-2 aceptados (tab fijo) con totales derivados (50/80) · cohorte del ingreso (origen 116, encaset heredado 2025-10-16)
- [x] **Contrapartes 114/115/116 intactas** (movimientos unilaterales) · `dotnet build` 0/0 · `dotnet test` 1.483 · backend de smoke detenido
- [x] Los datos del E2E (núcleo 1, galpón 1, lote 130 y su carga) quedan en la BD local **a propósito** para revisarlos por pantalla
- [x] Commit acotado (sin footer de atribución)

## Fase G — Cierre del lote 130 y cruce a producción (validación, sin cambios de código)

- [x] `GET /api/LotePosturaLevante/22/resumen-cierre` → **9.495 H / 929 M disponibles** y **130 huevos** a arrastrar (100 limpio + 30 tratado, incubables 130), sin producción previa
- [x] `POST /api/LiquidacionCierreLoteLevante/22/guardar` → liquidación guardada (mortalidad H 0,7 % = 70/10.000)
- [x] `POST /api/LotePosturaLevante/22/cerrar` (huevosIniciales 130, inicio producción 2026-06-08) → estado **Cerrado**
- [x] **LPP 10 creado**: lote 130, granja 4 / núcleo 1 / galpón 1, aves iniciales Y actuales **9.495 / 929** (= aves vivas del cierre), `huevos_iniciales` 130, inicio 2026-06-08
- [x] **Arrastre de huevos verificado**: fila de sistema en `seguimiento_diario_produccion` del 2026-06-08 con `huevo_tot` 130 / `huevo_inc` 130 / limpio 100 / tratado 30, `tipo_alimento` 'N/A' y la **marca `arrastreHuevosLevante`** en metadata (si registran ese día, los huevos se SUMAN — ventana de merge abierta)
- [x] Espejo `espejo_huevo_produccion` creado (1 fila) · liquidación en `liquidacion_cierre_lote_levante` (1 fila)
- [x] **Lote 130 elegible para la carga masiva de SeguimientoProduccion** (Cerrado + liquidado + LPP) — la siguiente etapa del ciclo ya puede cargarse por Excel
- [x] Nota: `lotes.fase` queda 'Levante' (el cierre por pantalla tampoco la actualiza; el sistema deriva producción por la existencia del LPP — paridad conservada)
- [x] Gotcha del smoke: el 404 inicial del resumen era el **alcance granular** — `LoadLevanteTrackedOrNullAsync` filtra por granjas asignadas del usuario del token; con un guid de usuario rol Admin pasa. El backend de smoke quedó detenido

## Fase H — Carga masiva de PRODUCCIÓN completa + E2E (2026-07-31/08-01, 3ª ronda)

**Pedido del usuario:** producción con TODOS los campos del módulo (incluida **agua**), movimientos de **huevos a planta y venta**, traslados/ventas de **aves** también en producción, **dos alimentos por sexo** validados en ambas fases, plantilla de producción al nivel de la de levante y archivo E2E en el Escritorio.

### Código
- [x] `MigracionEsquemas.SeguimientoProduccion` 32→**43 columnas** (opcionales al final): Error Sexaje H/M, Peso H/M (g) corporal, Uniformidad, Coef. Variación, Observaciones Pesaje, Consumo Agua (L), pH Agua, ORP Agua (mV), Temperatura Agua (°C)
- [x] `MigracionEsquemas.MovimientosHuevosProduccion` (hoja **«Movimientos Huevos»**, 18 columnas: Fecha, Tipo Traslado/Venta, 11 categorías, Tipo Destino Planta/Cliente/Empresa, Destino, Motivo, Descripción, Observaciones)
- [x] `MigracionMovimientosHuevosCalculos` (NUEVO, puro): `TryOperacion` (sinónimos), `TipoDestinoEfectivo` (defaults de la UI), `ClaveArchivo` — + 6 tests
- [x] Partial `MigracionService.MovimientosHuevos.cs` (NUEVO): INSERT directo `traslado_huevos` **Completado** + número `HUE-` en 2º SaveChanges + **un recálculo ABSOLUTO del espejo al final** (patrón spec F3; NUNCA el servicio vivo — auto-procesa, valida contra un espejo desactualizado a mitad de carga y TRAGA excepciones — y NUNCA `seguimiento_diario_levante`, donde vive el trigger ⇒ doble descuento). Disponibilidad proyectada por categoría = **Error** (criterio del módulo vivo). Idempotencia por (día, operación, 11 cantidades)
- [x] Hoja «Movimientos Aves» **generalizada a producción**: contrapartes por espejo LPP, salida/ingreso espejan `TrasladoAvesDesdeSegService` rama producción (traslado_salida/ingreso_*, traslado_hembras legacy, lote_destino_id, fecha_traslado, acumulados `produccion_traslado_*`, salida SIN clamp — paridad); **Venta en producción SIN columnas propias** (el vivo escribe sel_h/mortalidad NEGATIVOS — hack que corrompe contadores y NO se replica): descuento + nota en observaciones + auditoría
- [x] **fn v-next** (`20260801023000_FnMigracionProduccionCamposCompletos`, CREATE OR REPLACE misma firma): claves jsonb aditivas peso_h/m, uniformidad, coef_variacion, obs_pesaje, agua_diario/ph/orp/temp (merge con COALESCE en Pasos 0/1, crudo en INSERT); `err_h/err_m` dejan de mandarse en 0
- [x] 🔴 **Hueco real cazado por el E2E**: el INSERT de la fn exigía `l.fase='Produccion'`, pero el cierre de levante NUNCA actualiza `lotes.fase` ⇒ un lote cerrado por el flujo normal solo cargaba el día del arrastre (1/7). El filtro cayó: el criterio real es el JOIN al LPP vivo (mismo que `DeterminarFaseLote`) + la elegibilidad C#
- [x] 🔴 **Hueco preexistente cerrado**: la carga masiva de producción nunca recalculaba el espejo de huevos (no hay trigger en `seguimiento_diario_produccion`) ⇒ la disponibilidad quedaba desactualizada. Ahora se recalcula una vez al final del import (absoluto/idempotente), vía `IEspejoHuevoProduccionSyncService` opcional en el ctor
- [x] `dotnet build` 0/0 · `dotnet test` **1.501 verdes**

### E2E producción (lote 130 / LPP 11) — 9/9 y cuadres EXACTOS
- [x] **Dos alimentos H y DOS M en LEVANTE** validados por dry-run (lote 115): los 4 ítems parseados con su consumo y balance por ítem
- [x] **Archivo en el Escritorio**: `Carga_Masiva_Seguimiento_Produccion_LOTE_NIZA_E2E.xlsx` — plantilla real (6 hojas: Datos 43 col, Alimento, Movimientos Aves, Movimientos Huevos, Referencias con lotes de producción, Instrucciones) llenada: 7 días con 2 alimentos H + 2 M, huevos por categorías, peso corporal, uniformidad/CV, agua (1500 L, pH 7.2, ORP 650, 22.5 °C), etapa, error sexaje · Ingreso alimento 500 kg · Venta aves 200/20 + Salida 300→lote 13 + Ingreso 100←13 · Traslado huevos a planta 2900 + Venta 2000
- [x] Import **Procesado 7 filas** (día del cierre MERGEADO: 3600 + 130 arrastrados = 3730) · reimport **0 / 13 omitidas**
- [x] **Cuadre aves**: `9.495 − 56 (mort+sel+err) − 200 venta − 300 salida + 100 ingreso = 9.039 H` · `929 − 7 − 20 = 902 M` · acumulados 300/100 · lote 13 contraparte INTACTO (5.315/581)
- [x] **Cuadre huevos (espejo)**: histórico tot 25.330 / inc 24.630 / limpio 21.100 / tratado 3.530 · dinámico tot **20.430** / inc 19.730 / limpio 16.600 / tratado 3.130 (= histórico − 2 movimientos HUE Completado) — exacto
- [x] **Cuadre alimento**: 147→8.600 · 148→15.300 · 154→430 (500−70) · 155→5.650 · **28 consumos = 2.310 kg** con la referencia del alta manual
- [x] Agua/pesaje persistidos fila a fila (1500 L / 7.2 pH / peso H 1450→1456 / uniformidad 88 / err sexaje 1) · cohorte del ingreso con encaset heredado del K345A (2025-01-28)
- [x] Datos del E2E quedan en la BD local a propósito · backend de smoke detenido
- [x] Commit acotado (sin footer de atribución)

---

# Tracker — Seguimiento Diario PRODUCCIÓN: fn SQL canónica + reducción de services + invariantes

**Plan:** [`fase_de_desarrollo/seguimiento_produccion_fn_canonica_plan.md`](fase_de_desarrollo/seguimiento_produccion_fn_canonica_plan.md)
**Fecha:** 2026-07-31 · **Sesión propia — no tocar desde otras sesiones**

Objetivo: `fn_seguimiento_diario_produccion` (patrón engorde v13, LANGUAGE sql) como única fórmula de la
grilla diaria y sus derivados; conmutar lecturas (grilla + re-source de las 3 fns semanales); partir
`ProduccionService` en partials con cálculo puro testeado; espejo de huevos con un solo dueño; índice único
defensivo; arreglo del hack de venta negativa.

## Fase 0 — Exploración y plan
- [x] Exploración exhaustiva con 8 agentes en paralelo (service, patrón engorde, 4 fns SQL, espejo, aves, lectores, front, BD viva)
- [x] Plan escrito con decisiones y trade-offs
- [x] Decisiones D1-D4 confirmadas por el usuario: **D1=(b)** recálculo C# único dueño · **D2=persistir TODOS** los campos del modal · **D3=venta estilo carga masiva** · **D4=saldo CON error de sexaje**

## Fase 1 — fn_seguimiento_diario_produccion v1
- [x] `backend/sql/fn_seguimiento_diario_produccion.sql` — LANGUAGE sql STABLE, ~70 columnas snake_case, casts explícitos, dedup día Bogotá, universo seguimientos ∪ movimientos (filas movimiento-only con seg_id NULL), ORDER BY fecha+COALESCE(seg_id,0). Pesaje del lote (peso_h/m, uniformidad, CV) en NUMERIC para paridad decimal exacta
- [x] **Hallazgo/decisión de diseño**: los movimientos se cuentan solo desde `fecha_inicio_produccion` (los previos son del LEVANTE y ya viven en aves_h_inicial — el GET viejo los contaba de nuevo: lote 130 daba 8.646 en vez de 9.039). Divergencia deliberada documentada en el changelog v1
- [x] Migración `20260801060000_AddFnSeguimientoDiarioProduccion` (+ .Fn.cs verbatim + Designer clonado) aplicada en local
- [x] `Application/Calculos/SeguimientoDiarioProduccionCalculos.cs` (especificación ejecutable: dedup, semana, saldo, acumulados, % postura)
- [x] Tests xUnit — 16 verdes con testigos reales (lote 130: 9.495→9.039 H / 929→902 M, 25.330/24.630 huevos)
- [x] Validación en BD viva: LPP 11 → 9.039/902 exactos · LPP 6 → saldo 21 (= almacenado) y 2.091.450 huevos (= espejo) · LPP 7 → 5.315 al último día real (= almacenado) · legacy y fila huérfana con paridad · 2,5 ms para 301 días

## Fase 2 — Conmutar lecturas
- [x] `verificar_paridad_seguimiento_produccion.sql` (gate multipaís reusable) + línea base congelada (613 filas, Sanmarino+Demo, segunda pasada 0/0/0)
- [x] Grilla `GET /api/Produccion/seguimiento` sobre la fn vía `SqlQueryRaw<SeguimientoProduccionTablaFilaDto>`: contrato histórico byte a byte (CreatedAt=fecha y UpdatedAt=null conservados) + campos ADITIVOS (errorSexaje*, unif/CV por sexo, ciclo, es_traslado+splits+destino que el front ya esperaba, edad/semana, saldos, acumulados, % postura). Filas movimiento-only excluidas del listado
- [x] `informacion-lote` delega el saldo en la última fila de la fn (D4): lote 130 queda 9.039 (antes el GET lo «sanaba» a 8.646 por el doble descuento de movimientos del levante) — diff justificado; los 2 agregados de movimiento_aves del GET se eliminaron
- [x] Re-source F2 `fn_indicadores_produccion_postura` — salida **byte a byte idéntica** (baselines LPP 6/7/9/11) y 132→102 ms
- [x] Re-source F3 `fn_clasificacion_huevo_items_produccion` — byte a byte idéntica
- [x] Re-source F4 `fn_resumen_semanal_ra_pesadas_produccion` (CROSS JOIN LATERAL por LPP) — byte a byte idéntica (matrices 53 semanas × 3 empresas/años). ⚠️ El espejo .sql traía un `PARTITION BY fin_sem` en `part` NUNCA migrado (part=1 con encasets distintos): realineado a la ventana global desplegada
- [x] EXPLAIN antes/después: grilla ~2,5 ms · F2 mejora 132→102 ms · F4 0,4→14 ms (**justificado**: la fn diaria computa la serie completa por lote; costo absoluto trivial para un reporte bajo demanda y elimina la 3ª copia del bloque dual-fuente) · migración `20260801090000_FnsSemanalesProduccionSobreFnDiaria` (Down = 3 versiones previas verbatim)

## Fase 3 — Reducción de services
- [x] `ProduccionService` (1.682 líneas) partido en partials `Funciones/`: ancla 408 (ctor + helpers compartidos + interfaz) + Seguimiento 624 + Consultas 428 + Lotes 276 — partición completa verificada por reconstrucción línea a línea, namespace plano, CRLF preservado
- [x] Espejo: 24 SumAsync → 2 agregaciones `GroupBy` + **empresa por datos del LPP** (antes `ICurrentUser` salteaba el recálculo cross-empresa en silencio)
- [x] Fixes en alta/edición: fecha anclada a MEDIODÍA (`AnclarMediodiaUtc`) · edición re-valida duplicado por día (400 histórico, no 500 de índice) · edición valida empresa de la fila (isMine) · edición PRESERVA la marca de arrastre (antes la borraba y rompía la idempotencia del re-arrastre)
- [x] `dotnet build` 0/0 · `dotnet test` 1.516 verdes (1.500 + 16 nuevos)

## Fase 4 — Invariantes
- [x] (D1=b) Migración `20260801071000_RetirarTriggerEspejoHuevoProduccionLegacy` (DROP trigger + fn legacy, verificado 0/0 en BD); `backend/sql/trigger_espejo...sql` marcado ⛔ RETIRADO; `SeguimientoDiarioService` bloquea `tipo='produccion'` (CRUD genérico dormido, único disparador posible del camino viejo)
- [x] Cuadre de lectura espejo: `backend/sql/verificar_cuadre_espejo_huevo_produccion.sql` — 5/5 LPP con descuadre 0/0
- [x] Migración defensiva `20260801070000_IndiceUnicoSeguimientoProduccionDia`: creados `ix_..._lote_id_fecha_registro` (el que declara el modelo) + `ux_..._lote_dia_utc` (el invariante real por día); con duplicados solo RAISE WARNING, jamás tira el arranque
- [x] (D3) Venta/traslados MOV- convergidos: producción SIN ±Sel — venta = nota + auditoría (patrón carga masiva), traslados a columnas `traslado_*` + acumulados `ProduccionTraslado*` del LPP + FK al LPP + fecha mediodía + match por rango de día; cancelación/edición revierten splits; **eliminar movimientos Completados BLOQUEADO** (se cancela, no se elimina). Sin backfill: 0 filas negativas en el dump de prod
- [x] (D2) Persistir TODOS: columnas nuevas `ciclo`, `uniformidad_hembras/machos`, `cv_hembras/machos` (migración `20260801050324`, tipos espejo de la tabla legacy) + create/update/merge + DTO respuesta. Front NO se toca: el modal ya enviaba y rehidrataba esos campos (round-trip curado sin `yarn build`)

## Smoke HTTP (backend propio :5499, JWT + X-Secret-Up minteados)
- [x] Grilla LPP 11: 7 filas, día 1 inicio 9.495/pct 39,28/err 1 → último saldo **9.039/902**, acum 25.330 · LPP 7: 301 filas (sin movimiento-only), último 5.315, acum 1.541.184
- [x] informacion-lote LPP 11: avesActuales **9.039/902** (fórmula única viva)
- [x] Alta con campos D2 (err 2/1, unif 90,5/85,25, cv 5,5/6,25, ciclo Normal) → GET devuelve TODO (round-trip curado) → saldo 9.032/899 (con err) → DELETE 204 → 9.039/902 y espejo restaurados exactos (7 filas, 25.330/20.430)
- [x] Indicadores por API (F2 re-sourced): 43 semanas (44 − corte semana 26 del front)
- [x] Backend de smoke DETENIDO (puerto 5499 libre) · BD local consistente

## Fase 5 — Congelamiento liquidación
- [x] Análisis: NO aplica (módulo de liquidación de producción eliminado; molde de la fn queda listo)

## Cierre
- [x] Smoke HTTP local (:5499, JWT + X-Secret-Up minteados) — ver bloque Smoke arriba; backend detenido, puerto libre
- [x] BD local consistente (lote 130 restaurado exacto tras el ciclo alta/delete del smoke; datos E2E intactos), sin procesos huérfanos
- [x] `dotnet test` suite COMPLETA: 1.516 Application + 1 Domain, todo verde · `dotnet build` 0/0
- [x] Commit acotado `4034b8f` (36 archivos, git add explícito, sin footer; `.claude/settings.local.json` ajeno NO tocado)
- [x] Deuda documentada para tandas futuras: filas TSD con lpp NULL siguen fuera de la rama LPP (candidata v2 de la fn con decisión propia) · `SeguimientoProduccionService` legacy sigue anclando a medianoche (con el índice por día su duplicado ahora falla limpio) · hueco Reporte Contable Mov. Huevos (chip de tarea aparte) · `historico_semanal` del espejo queda columna muerta (DROP con OK explícito)

## Ronda 2 («sigue hasta dejar todo funcional») — deuda saldada

- [x] **fn v2 — filas TSD visibles** (`20260801110000_FnSeguimientoProduccionV2FilasTsdVisibles`): la rama LPP suma las filas de traslado con lpp NULL del mismo lote base, marcadas con la columna nueva `fila_sin_lpp`; la grilla del LPP ya muestra los traslados hechos desde la pantalla de seguimiento. Las 3 fns semanales las EXCLUYEN (`AND NOT fila_sin_lpp`) — baselines re-verificados **byte a byte idénticos**; el saldo no cambia (mort/sel/err = 0 en esas filas y el movimiento entra por movimiento_aves). Probado con fila TSD sintética en transacción con ROLLBACK: visible en grilla (splits 50/5), invisible en indicadores, saldo intacto. `informacion-lote` cuenta el mismo universo (Registros/MinFecha alineados con la grilla)
- [x] **Writer legacy `SeguimientoProduccionService` alineado**: match del día por RANGO (`RangoDiaUtc`, antes `== medianoche` no veía las filas ancladas a mediodía y creaba duplicados que ahora violarían el índice único con 500), fecha guardada ANCLADA a mediodía, y la edición re-valida duplicado por día (400 histórico)
- [x] **Reporte Contable — Movimientos de Huevos con fuente dual**: la sección leía SOLO la tabla legacy (lotes nuevos invisibles y «No se encontraron registros» al derivar fechas); ahora canónica + legacy con dedup por (lote, día) «gana el más temprano», fechas min/máx de ambas tablas y rango superior exclusivo (las filas a mediodía del último día ya no se cortan). ⚠️ El chip de tarea espejo de este fix fue INICIADO en otra sesión (worktree aparte): al integrarla, comparar contra este cambio ya commiteado y descartar el duplicado
- [x] Validación ronda 2: build 0/0 · tests 1.516 + 1 verdes · gate de paridad 0/0/0 (Sanmarino y Demo) · smoke HTTP en :5499 con números idénticos (9.039/902, 25.330; lpp7 301/5.315) · backend detenido, puerto libre, BD consistente

## Ronda 3 — reconciliación Reporte Contable + DROP historico_semanal (pedidos explícitos)

- [x] **Reconciliación del chip del Reporte Contable** (merge `6de9ea9`): la rama `claude/exciting-khorana-2289c9` (base `21a5c81`) traía una versión MÁS completa que el fix inline de `5a3b220` — alcance **padre + sublotes** (la topología nueva no crea hijos: sin esto un lote como el 130 ni entraba al reporte), cálculo puro `ReporteContableHuevosCalculos` (dedup con desempate determinista `EsLegacy`) y **13 tests**. Se mergeó tomando SU versión del service y aplicando encima el rango sargable sin `.Date` (gotcha date_trunc TZ-sesión) + corte exclusivo al día siguiente (filas a mediodía del último día). Rama borrada; ⚠️ el worktree `determined-agnesi-104f60` no se pudo remover (Permission denied — la sesión del chip retiene archivos): borrarlo a mano cuando esa sesión cierre (`git worktree remove` o eliminar la carpeta + `git worktree prune`)
- [x] **DROP `historico_semanal`** (OK explícito del usuario; migración `20260801120000_DropHistoricoSemanalEspejoHuevoProduccion`): columna jsonb + índice GIN eliminados de `espejo_huevo_produccion` (vacía en el 100 % de las filas, sin escritores vivos ni lectores; el detalle semanal es derivable de `seguimiento_diario_produccion`). Idempotente (IF EXISTS); entidad y Configuration sin la propiedad; los 3 scripts históricos de `backend/sql/` anotados para que nadie la recree. Verificado en local: columna 0, índice 0, cuadre del espejo sigue 5/5 en cero
- [x] Validación ronda 3: build 0/0 · tests **1.529 Application + 1 Domain** verdes (incluye los 13 del chip) · commit acotado


---

# Tracker — Reporte Contable: sección "Movimientos de Huevos" dual-fuente (legacy + seguimiento_diario_produccion)

**Plan:** [fase_de_desarrollo/reporte_contable_movimientos_huevos_dual_fuente_plan.md](fase_de_desarrollo/reporte_contable_movimientos_huevos_dual_fuente_plan.md)
**Fecha:** 2026-08-01

Objetivo: `ObtenerReporteMovimientosHuevosAsync` lee solo la tabla legacy (0 filas de producción en local Y prod
⇒ sección siempre vacía). Merge dual-fuente con el criterio canónico de las fns de producción (por lote+día
calendario Bogotá gana el timestamp más temprano) + alcance padre+sublotes (la topología LPP nueva no crea hijos:
el lote 130 hoy ni siquiera entra al método).

## Código
- [x] `Application/Calculos/ReporteContableHuevosCalculos.cs` (NUEVO, puro): `FilaHuevosDia` + `MergeDualFuentePorDia` (dedup por lote+día Bogotá, gana ts más temprano, empate→legacy) + `MenorFechaNoDefault`/`MayorFechaNoDefault`
- [x] `ReporteContableService.ObtenerReporteMovimientosHuevosAsync`: alcance padre+sublotes (seguimientos, traslados, nombres); el throw "No se encontraron sublotes" se elimina (el padre garantiza ≥1 lote — la topología LPP no crea hijos)
- [x] Flujo SemanaContable y flujo sin fechas: min/max de fechas considerando AMBAS fuentes
- [x] Consulta principal: legacy intacta (Where por timestamp crudo) + `SeguimientoProduccion` (rango `.Date` como el fallback dual existente ~493-496) + merge

## Tests
- [x] `ReporteContableHuevosCalculosTests` — 12 casos (passthrough por fuente, gana ts más temprano en ambos sentidos, empate→legacy, multi-lote mismo día, dedup intra-fuente, orden salida, min/max con default)

## Validación
- [x] Smoke ANTES (código actual, backend :5499 Dev, JWT+X-Secret-Up minteados): lote 13 → 400 "No se encontraron registros de producción" · lote 130 → 400 "No se encontraron sublotes" (sección muerta: legacy con 0 filas de producción en local Y prod)
- [x] `cd backend && dotnet build` — 0 errores, 0 advertencias
- [x] `cd backend && dotnet test` — **1.513 Application + 1 Domain verdes** (12 nuevos)
- [x] Smoke DESPUÉS lote 130: HTTP 200, 7 días (08–14 jun), TotalPostura **25.330**, inc 24.630, día del cierre 3.730 (incluye 130 arrastrados), planta **2.900** (10-jun), venta **2.000** (12-jun) — todo igual al cuadre E2E de Fase H
- [x] Smoke DESPUÉS lote 13 (padre+hijo 14): HTTP 200, 304 días, TotalPostura **3.632.634**, HvtoFertil 3.484.872, TrasladoAPlanta 2.395.894 — las 5 cifras EXACTAS contra SQL directo (el merge no duplica ni pierde)
- [x] Smoke flujo SemanaContable (lote 13, semana 2): HTTP 200, rango 23→29 jul, postura 32.374 = SQL exacto
- [x] Backend de smoke detenido + commit acotado (sin footer de atribución)

> ⚠️ Reconciliación (01-ago-2026, sesión fn canónica): este bloque venía de la sesión del chip
> (rama `claude/exciting-khorana-2289c9`, base `21a5c81`, anterior a la fn canónica). Se MERGEÓ a
> main tomando su versión del service (alcance padre+sublotes + `ReporteContableHuevosCalculos`
> puro con tests — más completa que el fix inline de `5a3b220`) y aplicando encima el rango
> sargable sin `.Date` + corte exclusivo al día siguiente (filas ancladas a mediodía).

---

# Tracker — Gastos de inventario: reporte sin eliminados + hoja de existencias completas

**Plan:** [`fase_de_desarrollo/gastos_inventario_reporte_estado_existencias_plan.md`](fase_de_desarrollo/gastos_inventario_reporte_estado_existencias_plan.md)
**Fecha:** 2026-08-05 · Módulo transversal (hoy datos solo en ItalcolEcuador; company 5 comparte catálogo)

Novedad del usuario final: el Excel del módulo trae también los consumos eliminados y solo muestra
las referencias que tuvieron consumo. Pedido de Moises: auditar el servicio de la tabla, lo que
exporta el Excel, el filtro de eliminados y el retorno a inventario al eliminar.

**Decisiones:** D1 = hoja de existencias con **saldo actual + consumo del rango** (sin kardex histórico) ·
D2 = el reporte **excluye eliminados SIEMPRE**; el historial queda en pantalla con filtro de Estado.

## Fase 0 — Auditoría (contra BD local, dump tipo-prod)
- [x] ✅ **Retorno a inventario VALIDADO**: 38/38 gastos eliminados con su devolución — 0 sin devolución, 0 líneas descuadradas, 0 cantidades descuadradas
- [x] 🔴 Confirmado el bug del reporte: `ExportAsync` no filtra estado ⇒ 46 filas Eliminado + 421 Activo en el archivo
- [x] 🔴 El CSV descarta `Estado`/`DeletedAt` aunque el DTO ya los trae
- [x] 🟠 La UI nunca manda `estado`; `fn_inventario_gastos_search` con `p_estado NULL` devuelve todo
- [x] 🟠 `DeleteAsync` busca el gasto sin `CompanyId` (módulo transversal)
- [x] Plan escrito + decisiones D1/D2 confirmadas

## Fase 1 — Backend
- [x] B1 `ExportAsync` excluye `Estado = 'Eliminado'` **incondicionalmente** (antes de aplicar `req.Estado`, así que pedirlo explícitamente tampoco los trae); `SearchAsync` NO cambia — la tabla sigue pudiendo mostrar el historial
- [x] B2 `fn_inventario_gastos_existencias` (`backend/sql/` + migración idempotente `20260805120000_AddFnInventarioGastosExistencias` con Designer clonado, sin tocar el ModelSnapshot)
- [x] B2 Endpoint `GET /api/inventario-gastos/existencias` + `InventarioGastoExistenciaDto`/`Row`/`Request` + interfaz
- [x] B3 `DeleteAsync` fail-closed por empresa (busca el gasto con `CompanyId`; empresa inválida ⇒ `UnauthorizedAccessException`)
- [x] B4 `InventarioGastoReporteCalculos` (puro: `EsGastoEliminado`/`EsGastoActivo`/`ClaveOrdenConcepto`/`EtiquetaConcepto`) + **21 tests xUnit**

## Fase 2 — Frontend
- [x] F1 `models/inventario-gasto.model.ts` con todos los tipos + `InventarioGastoExistenciaDto` y `EstadoGastoFiltro`; el servicio los **re-exporta** (imports existentes intactos)
- [x] F2 `funciones/exportar-gastos-inventario-excel.funcion.ts` (pura: `construirHojasReporteGastos`/`construirFilas*`/`describirFiltros`) sobre `exportarMultiHojaExcel` + README con las 2 reglas que el reporte no puede romper
- [x] F3 Filtro Estado (Activos por defecto / Eliminados / Todos) aplicado a la tabla + `limpiarFiltros` lo resetea; leyenda explicando el alcance del Excel
- [x] F4 Servicio `existencias(...)` + `buildParams` compartido; **CSV a mano eliminado** (63 líneas de código muerto)

## Fase 3 — Validación
- [x] `dotnet build` — **0 errores, 0 advertencias**
- [x] `dotnet test` — **1.550 Application + 1 Domain verdes** (1.529 previos + 21 nuevos)
- [x] `yarn build` (Node portable 22.23.1) — OK; único warning el de *bundle budget* preexistente
- [x] SQL (9 casos): universo 1.310 = 10 granjas × 131 ítems = catálogo completo · 1.114 filas sin consumo presentes (121 con saldo > 0) · `saldo_actual` == stock **0 diferencias** · consumo fn 318.719,220 == gastos activos (los 5.612,225 eliminados fuera) · filtros granja/concepto/rango OK
- [x] Smoke HTTP (:5499, JWT + X-Secret-Up minteados): `/export` **421 filas, 0 eliminados** (antes 467 con 46) · `/export?estado=Eliminado` ⇒ 0 filas · tabla 316/38/354 según filtro · `/existencias` 1.310 filas con los 9 conceptos
- [x] Multiempresa: company 5 ⇒ 0 existencias / 0 export, **sin fuga de Ecuador**
- [x] `DELETE` cross-empresa (gasto 354 de Ecuador con sesión company 5) ⇒ **HTTP 400** y el gasto sigue `Activo`; conteos 316/38 idénticos al inicio
- [x] **Verificación UI** (front :4200 + back :5002, sesión dev inyectada en `auth_session`): la pantalla abre en **316 registros** (antes 354) · el selector filtra 38 Eliminados / 354 Todos / 316 Activos con los estados correctos en la columna · «Exportar Excel» dispara `/export` + `/existencias` en paralelo (200/200) y produce **`gastos-inventario_20260805.xlsx`** (magic `PK`, 716 KB, ya no `.csv`) con toast «421 consumo(s) y 1310 existencia(s)» · **0 errores de consola**
- [x] Backend y front de smoke detenidos — puertos 4200/5002/5499 libres, sin procesos huérfanos
- [x] Commit acotado (sin footer de atribución)

---

# Fix — «Aves disponibles» difiere entre Seguimiento diario y Venta (pollo engorde)

**Plan:** [`fase_de_desarrollo/fix_disponibilidad_aves_venta_engorde_plan.md`](fase_de_desarrollo/fix_disponibilidad_aves_venta_engorde_plan.md)
**Fecha:** 2026-08-05 · Ticket de operación: CAROLINA G4 lote 2603 y Sacachun 3A G2 lote 2602

Novedad: el seguimiento diario dice 40 aves y la venta 33; la operación no puede despachar.
**Hipótesis del ticket descartada** (no suma aves del lote cerrado 2601: los dos «7» son coincidencia).
**Causa raíz:** el fix de doble descuento de jul-26 (`BajasPendientesDeAplicar`) se aplicó solo al
seguimiento; la venta sigue restando las bajas ya aplicadas al maestro ⇒ las cuenta dos veces.
**El correcto es 40** (= `fn_seguimiento_diario_engorde`). Impacto: 50 lotes / 31.062 aves (PA 30, EC 20).

## Fase 0 — Diagnóstico (contra BD local, dump tipo-prod)
- [x] Ambos números reproducidos exactos: seguimiento `762−722=40`, venta `762−729=33`
- [x] Identidad de conservación verificada: `13.700 − 12.931 − 7 = 762 = machos_l`
- [x] Fuente de verdad: `fn_seguimiento_diario_engorde(97).saldo_aves = 40` (49 d)
- [x] `BajasPendientesDeAplicar` tiene 1 solo consumidor productivo (la venta no lo usa)
- [x] Los 3 caminos de venta convergen en `GetAvesDisponiblesLotesAsync` ⇒ un único punto de arreglo
- [x] Impacto medido por empresa + 49 lotes activos listados (incluye Sacachun 3A: 194 vs 0)
- [x] Plan escrito

## Fase 1 — Backend (sin migración: es aritmética en C#)
- [x] C1 `AvesDisponiblesEngordeCalculos.DisponiblesPorSexo` (puro, encapsula la fórmula completa)
- [x] C2 🔴 `MovimientoPolloEngordeService.ResumenDisponibilidad` carga `BAJA_SEGUIMIENTO` y delega
- [x] C3 `LoteReproductoraAveEngordeService.GetAvesDisponiblesAsync` delega (resultado idéntico)

## Fase 2 — Tests (gate CI)
- [x] T1-T2 casos del ticket (CAROLINA G4 = 40 · Sacachun 3A = 194, antes 0)
- [x] T3 retrocompatibilidad sin filas `BAJA_SEGUIMIENTO` (lote 2601 G4 = 7 en ambas fórmulas)
- [x] T4 equivalencia seguimiento == venta
- [x] T5-T8 reservas pendientes, clamp a 0, rama `sieteDiasCompletos`, bajas mixtas

## Fase 3 — Validación
- [x] `dotnet build` **0 errores / 0 advertencias**
- [x] `dotnet test` **verde: 1.566 tests, 0 fallos**
- [x] Paridad con la grilla: **Panamá 29/29 exacto (desvío 0)** · **Ecuador 31/32** (antes 12/32)
- [x] Gate multipaís: **0 lotes bajan** su disponibilidad · **0 lotes sin `BAJA_SEGUIMIENTO` cambian**
      (retrocompatibilidad total) · 50 corregidos / 118 intactos / **31.062 aves recuperadas**
- [x] Sin procesos huérfanos (solo consultas psql puntuales)
- [x] Commit acotado (sin footer de atribución)

### Hallazgo aparte (NO es de este fix, no se toca)
- Kilometro 61 · Galpon-1 · lote 2604 (id 107): el maestro tiene **17 aves de más** frente a la
  identidad `encaset − ventas − bajas_aplicadas` (24.374 − 0 − 140 = 24.234, maestro = 24.251).
  Es el único lote activo que no cuadra con la grilla; el fix lo acerca de −123 a −17 pero no puede
  corregir un maestro desfasado. Requiere su propia auditoría de datos antes de tocar nada.

## Fase 4 — Corrección de DATOS + prevención (pedido 05-ago-2026)
- [x] A1 Auditoría del universo: identidad canónica = `inicio − ventas − BAJA_SEGUIMIENTO − ajustes fantasma`
- [x] A2 🔴 **8 lotes «2601» NO eran un bug**: corrección deliberada previa (plan `correccion_aves_disponibles_engorde_2601_plan.md` §2.3), lotes liquidados, rastro de 8 filas `Ajuste` = 1.552 aves ⇒ **NO TOCADOS** (corregirlos revivía aves fantasma)
- [x] A3 Clasificados los 6 restantes: 1 con efecto visible (107), 1 solo cruce por sexo (184), 4 con `Inicio` ≠ encaset ⇒ revisión manual
- [x] A4 Origen exacto del descuadre del 107: la fila `BAJA_SEGUIMIENTO` del 24-07 (5 H + 12 M) nunca llegó al maestro; el registro se creó retroactivo el 30-07
- [x] C4 Simulación en transacción + `ROLLBACK`: 2 lotes, Panamá 0, 0 negativos, 2ª pasada 0 filas
- [x] C5 Migración `20260805150000_CorreccionMaestroAvesEngordeIdentidad` (data-only, 3 guardas) + SQL en `backend/sql/`
- [x] C6 Prevención (a): los 4 `catch` que silenciaban el fallo del descuento a `Console` ahora van al `_logger` con lote y seguimiento
- [x] C7 Prevención (b): `fn_cuadre_aves_engorde` + migración `20260805160000_AddFnCuadreAvesEngorde` (detector del invariante, hermana de `fn_cuadre_alimento_engorde`)
- [x] V1 Migraciones aplicadas en local; re-ejecutar el SQL de corrección da `UPDATE 0` (idempotente)
- [x] V2 **Cuadre final: 0 descuadrados** — Ecuador 104/108 (4 en revisión manual), **Panamá 60/60**
- [x] V3 Paridad pantalla vs grilla: **Ecuador 32/32** (era 31/32) · **Panamá 29/29**
- [x] V4 `dotnet build` 0/0 · `dotnet test` 1.566 verdes · sin procesos huérfanos
- [ ] ⚠️ **Pendiente (no bloqueante, decisión de negocio):** 4 lotes con `Inicio` ≠ `aves_encasetadas`
      (5 y 7 dicen 50.000 vs 25.542/22.681 reales; 30 y 132). Hoy muestran el número correcto.

---

# Fix — borrar/editar un seguimiento viejo infla el maestro de aves (pollo engorde)

**Plan:** [`fase_de_desarrollo/fix_baseline_bajas_seguimiento_engorde_plan.md`](fase_de_desarrollo/fix_baseline_bajas_seguimiento_engorde_plan.md)
**Fecha:** 2026-08-05 · Continúa el «Hallazgo aparte» del fix `3998aa2` (lote 107 con 17 aves de más)

**Causa raíz:** `SincronizarAsync` tomaba el baseline de las **columnas del seguimiento** en vez de su
**fila del histórico**, que es la única prueba de lo que se descontó. Borrar un registro de la cohorte
anterior al aplicador (< 2026-07-27 17:58, sin fila) **acreditaba aves que nunca se debitaron**, y
`UpsertHistorico` hacía no-op silencioso (`if (fila is not null)`) ⇒ maestro inflado **sin rastro**:
ni fila anulada, ni `updated_at`, ni auditoría.

## Fase 0 — Auditoría (solo lectura, sin tocar datos)
- [x] Identidad de conservación recalculada: maestro 107 = 10.860 + 13.374 = **24.234** ⇒ hoy CUADRA
- [x] Hipótesis 1 **cruce reproductora descartada**: 26/26 seguimientos con `origen_cruce = false`, 0 filas en `_backup_bajas_cruce_engorde_20260729`
- [x] Hipótesis 2 **anuladas/duplicadas descartada**: las 10 `BAJA_SEGUIMIENTO` vivas, `uq_lote_hist_origen` impide duplicados
- [x] Hipótesis 3 **ajuste manual previo descartada**: sin filas `Ajuste`/`AjusteResync` para 107/184
- [x] Hipótesis 4 **CONFIRMADA**: borrado del #8652 (07-24, pre-aplicador, sin fila) el 07-30 16:50; #10595 (5 H + 12 M = **17**) es el único con fila
- [x] Fecha de corte del aplicador fijada: primera fila `BAJA_SEGUIMIENTO` **2026-07-27 17:58:29**
- [x] ⚠️ Detectado por `xmin` que el maestro del 107 **ya lo corrigió SQL crudo externo** (txn `52399`, 2 filas: 107 y 184, sin `updated_at` ni auditoría, base restaurada con `xmin` uniforme `52338`) — **no fue esta sesión**
- [x] Alcance sistémico: **0 lotes descuadrados** (identidad por sexo y total) · exposición **4.797 seguimientos sin fila / 102 lotes / 158.092 aves** (EC) + 32 (PA)
- [x] Plan escrito

## Fase 1 — Backend (sin migración ni backfill: no hay descuadre que reparar)
- [x] C1 `RetiroAvesEngordeCalculos.BaselineAplicado(RetiroAves?)` — puro; fila ausente/anulada ⇒ `(0,0)`; mixtas al bucket machos
- [x] C2 🔴 `SincronizarAsync` lee la fila por `(origen_tabla, origen_id)` y **deriva el baseline solo**; deja de recibir `viejas`
- [x] C3 `UpsertHistoricoAsync` → `UpsertHistorico`: recibe la fila ya cargada (una consulta menos) y es síncrono
- [x] C4 `origen_id` fuera de rango `int` ⇒ no toca el maestro (antes lo movía a ciegas, sin traza)
- [x] C5 6 llamadas actualizadas (3 Ecuador + 3 carga masiva) + wrapper `SincronizarBajasAvesAsync`
- [x] C6 `SincronizarCruceAsync` simplificado: el baseline sale de la misma fila ⇒ comportamiento idéntico

## Fase 2 — Tests (gate CI)
- [x] T1-T3 `BaselineAplicado`: sin fila `(0,0)` · por sexo `(H,M)` · mixta `(0,X)`
- [x] T4 borrar un seguimiento sin fila **no devuelve aves**, con contraste explícito del bug (inflaba 5 H + 12 M)
- [x] T5 editar un seguimiento sin fila descuenta el **total nuevo**, no el delta
- [x] T6-T8 regresión camino normal: alta+borrado simétricos por sexo **y** mixto; edición con fila viva mueve solo el delta

## Fase 3 — Validación
- [x] `dotnet build` — **0 errores, 0 advertencias**
- [x] `dotnet test` — **1.573 Application + 1 Domain verdes** (1.565 previos + 8 nuevos)
- [x] Desfase del maestro medido con `fn_cuadre_aves_engorde` (commit `75f7980`, sesión paralela) — **no se duplicó la fórmula**
- [x] Guard complementario `backend/sql/verificar_bajas_seguimiento_sin_aplicar.sql`: huérfanas vivas **0 filas**; cohorte sin fila como termómetro
- [x] Reconciliado con `75f7980` (misma área): su cambio era `Console.WriteLine` → `_logger?.LogError`, **no tocaba el baseline** ⇒ arreglos complementarios, conflicto solo textual
- [x] Datos **no modificados** por esta sesión (solo `SELECT`; `pageinspect` creada y eliminada)
- [x] Sin procesos huérfanos (solo consultas psql puntuales)
- [x] Commit acotado (sin footer de atribución)

---

# Tracker — Envío de correo: migración a Microsoft Graph API (retiro de auth básica SMTP)

**Plan:** [`fase_de_desarrollo/envio_correo_graph_api_plan.md`](fase_de_desarrollo/envio_correo_graph_api_plan.md)
**Fecha:** 2026-08-05 · Bloque propio — no tocar desde otras sesiones

Producción no envía correos: Microsoft retiró la **auth básica para SMTP Client Submission** en
Exchange Online (rechazo desde 01-mar-2026, refuerzo total 30-abr-2026; error
`550 5.7.30 Basic authentication is not supported for Client Submission`).
**Blocker:** `System.Net.Mail.SmtpClient` no soporta XOAUTH2 ⇒ no alcanza con cambiar la contraseña,
hay que cambiar el emisor. **Decisión del usuario: Microsoft Graph API.**
Único punto de envío real: `EmailQueueProcessorService:213-305` (el resto sólo encola).

## Fase 0 — Auditoría y plan
- [x] Mapeado el flujo completo: 3 encoladores (`EmailService`, `TicketService`, `AuthService`) → `email_queue` → 1 solo emisor
- [x] Confirmada la causa con fuentes de Microsoft (timeline y código de error)
- [x] Verificado que no hay paquetes de Graph/MailKit/AWS SES en los `.csproj`
- [x] Plan escrito + decisión de transporte confirmada por el usuario

## Fase 1 — Abstracción y cálculo puro
- [x] `Application/Interfaces/IEmailSender.cs` + `EnvioCorreoResultado`
- [x] `Application/Calculos/EnvioCorreoCalculos.cs` (resolver proveedor, clasificar errores, payload, vigencia de token)

## Fase 2 — Transportes (Infrastructure)
- [x] `Email/SmtpEmailSender.cs` — traslado literal del código de hoy (dev local + rollback)
- [x] `Email/GraphTokenProvider.cs` — client_credentials + caché con margen de 5 min
- [x] `Email/GraphEmailSender.cs` — `POST /v1.0/users/{buzon}/sendMail`, 202 = OK, reintento único ante 401
- [x] `Email/SinTransporteEmailSender.cs` — transporte nulo con diagnóstico (evita el crash de arranque)

## Fase 3 — Cableado
- [x] `EmailQueueProcessorService` delega en `IEmailSender` (retries/estados/metadata intactos);
      263 líneas de SMTP inline eliminadas del procesador (580 → 317 líneas)
- [x] Se elimina el `throw` del constructor (podía tumbar el arranque en ECS) → log crítico
- [x] `Program.cs`: `AddHttpClient("graph-email")` + registro del `IEmailSender` resuelto por config
- [x] `appsettings.json` / `appsettings.Development.json` con `Email:Provider` + `Email:Graph` (sin secretos)
- [x] `ecs-taskdef-new-aws.json`: `Email__Provider=auto` + `Email__Graph__*` vacíos ⇒ desplegar la
      TaskDef **no cambia nada** hasta que carguen las credenciales; ahí conmuta solo
- [x] `backend/documentacion/MIGRACION_CORREO_GRAPH_API.md` (app registration paso a paso)
- [x] Los 3 documentos con instrucciones ya muertas (habilitar SMTP AUTH / App Password) marcados ⛔ OBSOLETO

## Fase 4 — Tests (gate CI)
- [x] `EnvioCorreoCalculosTests` — **53 tests**: tabla de decisión completa del proveedor
      (incluye retrocompatibilidad dev local y provider explícito sin config ⇒ NO cae a SMTP en silencio),
      vigencia del token, payload de `sendMail` serializado, clasificación 401/403/404/429/5xx y diagnósticos

## Fase 5 — Validación
- [x] `dotnet build` — **0 errores, 0 advertencias**
- [x] `dotnet test` — **1.626 Application + 1 Domain verdes** (1.573 previos + 53 nuevos)
- [x] Smoke 1 (sin config Graph): elige **SMTP** — `📧 Transporte de correo: SMTP (smtp.office365.com:587)`,
      retrocompatibilidad de desarrollo local intacta
- [x] Smoke 2 (con credenciales Graph): elige **Graph** — `transporte: graph`, buzón correcto
- [x] Smoke 3 (`provider=graph` con config incompleta): log **crítico** con las variables que faltan y
      **la aplicación arranca igual** (antes esto tumbaba el arranque del `HostedService` en ECS)
- [x] BD local sin tocar (`email_queue` 60 failed / 52 sent, idéntico antes y después; 0 filas `pending`)
- [x] Sin procesos huérfanos — puerto 5499 libre
- [x] Commit acotado (sin footer de atribución)

## Fase 6 — ⚠️ CORRECCIÓN DEL DIAGNÓSTICO (05-ago-2026, tras el aviso del usuario)

El usuario avisó que el arreglo era mucho más chico («solo hay que cambiarle el protocolo»).
**Tenía razón en que mi diagnóstico estaba mal.** Yo había atribuido la falla al retiro global de la
auth básica **por la fecha del anuncio de Microsoft, sin haber visto nunca el error real** (el
usuario no lo tenía a mano). Con la BD local ya sincronizada con producción, el error apareció.

**Error real** (`email_queue` id 112, 05-ago-2026 12:35 UTC):
`530 5.7.57 Client not authenticated` + `535 5.7.139 ... did not meet the criteria to be
authenticated successfully. Contact your administrator.` — **NO** es `550 5.7.30`.

- [x] Probe SMTP a mano (`EHLO`→`STARTTLS`→`AUTH LOGIN`): **`235 Authentication successful`**
      ⇒ la auth básica de este tenant SIGUE VIVA y las credenciales son válidas
- [x] Handshake con TLS 1.2 / 1.3 / default: los tres autentican ⇒ **la versión de TLS no es la causa**
- [x] Puerto 465 (TLS implícito): cerrado en Office 365 ⇒ descartado (y `SmtpClient` tampoco puede)
- [x] 🔴 Hipótesis del orden `UseDefaultCredentials`/`Credentials`: reprodujo el error exacto en
      **.NET Framework** (PowerShell), pero un test en **.NET 10** demostró que ahí NO borra las
      credenciales ⇒ **descartada**. Casi la publico como causa raíz corriendo el experimento en el
      runtime equivocado; el test con la premisa falsa se eliminó
- [x] ✅ **Envío REAL con el bloque `SmtpClient` idéntico al de la app, sobre .NET 10 → ENVIADO OK**
      (2 correos de prueba entregados a `zootecnico@sanmarino.com.co`)
- [x] Config desplegada verificada: idéntica a la del repo (587 / EnableSsl=true / mismas credenciales)
- [x] Último envío exitoso en la cola: **3-jun-2026**; desde ahí fallan todos, sin cambios en el emisor

**Conclusión:** credenciales ✅, código ✅, protocolo ✅. Lo que rechaza es una **política del tenant
según el origen de la conexión** (el propio Exchange dice *"Contact your administrator"*).
**El código no puede arreglarlo.**

- [x] Diagnósticos de `SmtpEmailSender` reescritos: dejan de culpar a la contraseña y al retiro de
      auth básica; ahora indican Conditional Access / SMTP AUTH y los comandos exactos para el admin
- [x] `MIGRACION_CORREO_GRAPH_API.md` §1 reescrito con el diagnóstico verificado y la tabla de
      hipótesis descartadas + los dos caminos de solución
- [x] `dotnet build` 0/0 · `dotnet test` 1.626 + 1 verdes

### Pendiente del usuario — Camino A (rápido, si el admin puede)
- [ ] Conditional Access / Security Defaults: ¿bloquea legacy auth por ubicación o IP? Excluir el origen
- [ ] `Get-CASMailbox 'zootecnico@sanmarino.com.co' | Select SmtpClientAuthenticationDisabled` ⇒ debe dar `False`
- [ ] `Get-TransportConfig | Select SmtpClientAuthenticationDisabled` ⇒ debe dar `False`

### Pendiente del usuario — Camino B (sólo si el A no se puede)
- [ ] Migrar a OAuth 2.0 / Microsoft Graph. La implementación completa está en el commit `c7b6834`
      (`git show c7b6834`): emisor Graph, proveedor de token con caché e instructivo del app
      registration. Se revirtió a pedido del usuario para dejar un solo transporte.

## Fase 7 — Simplificación a SMTP-only (pedido del usuario: «más fácil y desplegar de una vez»)

- [x] Eliminados `GraphEmailSender`, `GraphTokenProvider` y `SinTransporteEmailSender`
- [x] `EnvioCorreoCalculos` reducido a lo de SMTP: `HayConfiguracionSmtp`, `ClasificarErrorSmtp`,
      `EsRechazoPorPolitica`, `DiagnosticoSinConfiguracion`
- [x] `Program.cs`: `AddSingleton<IEmailSender, SmtpEmailSender>()` — se fue el `AddHttpClient`, el
      switch de proveedor y `Email:Provider`. Si falta config SMTP, avisa y NO tumba el arranque
- [x] `Email:Graph` fuera de `appsettings.json` / `appsettings.Development.json`; `Email__Provider`
      y `Email__Graph__*` fuera de la TaskDef ⇒ **las variables desplegadas quedan idénticas a hoy**
- [x] Doc reescrita como `DIAGNOSTICO_CORREO_OFFICE365.md` (la de migración se eliminó); los 3 docs
      viejos con banner corregido — ya no dicen «migró a Graph» ni culpan a la contraseña
- [x] Se conserva lo que sí aportaba el refactor: procesador delgado (580→317 líneas), diagnósticos
      honestos en `email_queue.error_message` y sin `throw` en el constructor del `HostedService`
- [x] Tests reescritos (24): configuración, clasificación con los `error_type` HISTÓRICOS y detección
      del rechazo por política. Incluye el hueco conocido `"timed out"` ≠ `"timeout"`, documentado
      y **conservado** (cambiarlo alteraría el `error_type` de filas ya existentes)
- [x] `dotnet build` 0/0 · `dotnet test` **1.601 Application + 1 Domain verdes**
- [x] Smoke local: arranca con `transporte: smtp`, sin log crítico; puerto 5499 liberado
- [x] Commit acotado (sin footer de atribución)

### Evidencia adicional hallada en Fase 7
El historial de `email_queue` por mes muestra un corte **limpio**, no intermitente:
feb-may 2026 = **45 enviados / 0 fallidos**; junio corta y desde ahí 0 enviados / 47 fallidos.
Y el mismo síntoma ya había ocurrido en nov-2025/ene-2026, resolviéndose **del lado administrativo**
(a partir de febrero el envío volvió solo, sin tocar el emisor). Refuerza que la causa es del tenant.

> ⚠️ **Desplegar no arregla el correo.** El código ya envía bien (probado sobre .NET 10 con las
> credenciales de producción). El destrabe está en Microsoft 365 — ver Camino A.

### 🔴 Deuda detectada al pasar (fuera de alcance, requiere trabajo propio)
- Credenciales en texto plano commiteadas: contraseña SMTP (`appsettings.json:77`,
  `appsettings.Development.json:30`, `ecs-taskdef-new-aws.json:48`), cadena de conexión de RDS prod
  y clave JWT en la TaskDef. Deben rotarse y moverse a Secrets Manager.

---

# Corrección de la referencia `Inicio` + liquidación de corridas anteriores (pollo engorde)

**Plan:** [`fase_de_desarrollo/correccion_referencia_inicio_engorde_plan.md`](fase_de_desarrollo/correccion_referencia_inicio_engorde_plan.md)
**Fecha:** 2026-08-05

## Parte A — Corrección de datos por migración
- [x] A1 Los 4 lotes con `Inicio` ≠ encaset quedaban fuera de toda auditoría (`referencia_confiable = false`)
- [x] A2 Clasificadas DOS causas opuestas: 5 y 7 con `Inicio` de plantilla (25.000/25.000 del 2026-03-23, 6 lotes) · 30 con `aves_encasetadas` inflado
- [x] A3 Evidencia bloque 1: capacidad del galpón (22-25 mil en otros ciclos, 50.000 = doble) + el lote 7 cierra en **0 exacto en ambos sexos**
- [x] A4 Evidencia bloque 2: bajo el `Inicio` ambos sexos cierran en **0 exacto**; bajo el encaset sobran 700 H y 700 M (excedente partido en dos)
- [x] A5 Reglas dinámicas probadas contra TODA la base: bloque 1 alcanza solo 5 y 7, bloque 2 solo el 30 — ninguna nombra ids
- [x] A6 Simulación en transacción + `ROLLBACK` antes de tocar nada
- [x] A7 Migración `20260805170000_CorreccionInicioHistorialYEncasetEngorde` (data-only, Designer clonado, sin tocar ModelSnapshot) + SQL trazable en `backend/sql/`
- [x] A8 Aplicada en local con `ASPNETCORE_ENVIRONMENT=Development` (host 127.0.0.1:5433 verificado en el log de EF)
- [x] V1 `dotnet build` 0/0 · `dotnet test` **1.573 + 1 verdes**
- [x] V2 Re-ejecución del SQL ⇒ `UPDATE 0` / `UPDATE 0` (idempotente)
- [x] V3 `fn_cuadre_aves_engorde`: **0 descuadrados** confiables · sin referencia confiable **de 4 a 1**
- [x] V4 Lote 30: 11.300 − 2.484 − 8.816 = **0 exacto**
- [ ] ⚠️ **Pendiente (decisión de negocio):** id 132 (19.387 vs 19.187, 200 aves) — activo y sin ventas, la conservación no discrimina; necesita el documento físico de encasetamiento
- [ ] ⚠️ **Pendiente (decisión de negocio):** ids 3, 4, 6, 8 — encaset 50.000 **y** `Inicio` de plantilla: los dos números son ficticios, cero movimientos. El detector no los ve porque compara `ih + im` sin mixtas

## Parte B — Liquidación de corridas anteriores: BLOQUEADA, no puede ir por migración
- [x] B1 🔴 Liquidar es una transacción de 5 pasos (estado + avance del ERP de granja + **copia congelada** + saldo + resumen). El código: *«sin copia no hay liquidación»*. Una migración SQL saltearía 4 de los 5
- [x] B2 🔴 El criterio «galpón con corrida posterior» alcanza 75 lotes e **incluye 22 de Panamá con 801.882 aves VIVAS** y seguimiento del 2026-08-03 (allá conviven varias corridas por galpón)
- [x] B3 Candidatos reales medidos — Ecuador: **39 con saldo 0** (grupo A) · 12 residuales < 1 % (602 aves) · 2 con saldo significativo (1.119 aves)
- [x] B4 Orden obligatorio verificado: el *Gate B1* impide editar `aves_encasetadas` de un lote liquidado ⇒ **corregir ANTES de cerrar** (por eso el lote 30 se corrigió primero)
- [ ] ⏸️ **Esperando confirmación:** cerrar el grupo A (39 lotes de Ecuador) recorriendo el endpoint real de cierre. Irreversible sobre producción ⇒ requiere OK explícito sobre la lista
- [ ] ⏸️ Grupos B y C (14 lotes con aves pendientes) — revisión aparte · Panamá **no se toca**

---

# Descargar Excel del stock de TODAS las granjas (Gestión de Inventario)

**Plan:** [`fase_de_desarrollo/exportar_stock_inventario_excel_plan.md`](fase_de_desarrollo/exportar_stock_inventario_excel_plan.md)
**Fecha:** 2026-08-05 · **Alcance:** front-only (backend ya soporta `farmId` opcional)

## Fase 1 — Análisis
- [x] A1 `GET /inventario-gestion/stock` sin `farmId` ya devuelve todas las granjas asignadas (scope empresa+país+user, fail-closed) — cero cambios de backend
- [x] A2 El nivel (galpón para alimento / granja para el resto) lo resuelve el backend (`AlimentoNivelResolver`) — el front no vuelve a decidir

## Fase 2 — Función pura (`funciones/`)
- [x] B1 `funciones/README.md` con la convención del módulo (calcada del canónico movimientos-pollo-engorde)
- [x] B2 `funciones/exportar-stock-excel.funcion.ts` — `cabecerasStockExcel` + `construirFilasStockExcel` (puras) + `exportarStockExcel` (usa `shared/utils/excel`, prohibido XLSX inline)

## Fase 3 — Componente + UI
- [x] C1 `descargarStockExcel()`: consulta propia SIN `farmId`, respeta concepto/búsqueda, delega en la función pura
- [x] C2 Botón «Descargar Excel (todas las granjas)» en la cabecera de la tarjeta Stock + SCSS agrupado con el del Histórico (sin duplicar reglas)
- [x] C3 Nota en el hint de filtros: el Excel siempre trae todas las granjas
- [x] C4 `InventarioGestionStockDto`: `granjaNombre`/`nucleoNombre`/`galponNombre` a `string | null` (el API los manda null; el tipo decía `?: string`)

## Fase 4 — Tests
- [x] D1 `exportar-stock-excel.funcion.spec.ts` — **12 specs** verdes (alimento con galpón, otros a nivel granja, fallbacks nombre/id, Colombia sin ubicación, fecha sin corrimiento de zona, cantidad numérica, lista vacía, orden, cabeceras)

## Fase 5 — Validación
- [x] E1 `yarn build` — 0 errores (único warning: bundle budget preexistente)
- [x] E2 `yarn test` — **118/118 verdes** (106 previos + 12 nuevos)
- [x] E3 Smoke UI contra backend local (ItalcolEcuador, usuario con 10 granjas):
      · **B (el crítico)** con granja «BODEGA PRINCIAL KM 86» seleccionada: la grilla muestra 38 filas de 1 granja y el export pide `/stock` **sin farmId** ⇒ 464 filas / **10 granjas** / 135 con galpón (calza exacto con la BD)
      · Contenido del `.xlsx` verificado: título, subtítulos («Granjas: todas las asignadas (10)», «Concepto: todos», «Registros: 464»), 9 cabeceras, alimento con `CAROLINA | N1 | GALPON 1`, no-alimento con `—`, cantidad numérica
      · **C** Concepto=Alimento ⇒ 135 filas / 8 granjas, todas con galpón; subtítulo lo documenta
      · **D** Búsqueda sin resultados ⇒ modal «Sin datos», **0 descargas**
      · **E** Triple clic ⇒ 1 sola petición y 1 solo archivo; botón «Generando…» deshabilitado y luego restaurado
      · **F** Colombia (sin columnas Núcleo/Galpón) cubierto por test unitario; no se smokeó en UI por falta de sesión Colombia
- [x] E4 Sin procesos huérfanos — 4200 y 5002 libres

## Revisión 2 — dos hojas por concepto (Alimento / Otros conceptos)
Pedido: *«que descargue todos los conceptos, una hoja que sea alimento y la otra otros conceptos,
así tenemos varios tipos en un solo archivo»*.

- [x] R1 Validado el manejo de conceptos vigente. **Hallazgo (dato, no bug del export):** el catálogo
      tiene el mismo concepto escrito con distinta capitalización (`Otros insumos` / `Otros Insumos`,
      `alimento` / `Alimento`) y el desplegable los lista como opciones separadas, mientras el filtro
      del backend es *case-insensitive* ⇒ elegir cualquiera de las dos trae las mismas filas
- [x] R2 Verificado que 167 ítems tienen `concepto IS NULL` (no cadena vacía) ⇒ `Concepto ?? TipoItem`
      resuelve bien a `alimento`; por eso la partición compara **en minúsculas**
- [x] R3 `esFilaAlimento` + `particionarStockPorConcepto` + `construirHojasStockExcel` (puras)
- [x] R4 La partición se decide por **concepto**, NO por «tiene galpón» (un alimento a nivel granja
      sigue yendo a la hoja Alimento)
- [x] R5 Hoja `Otros conceptos` sin columnas de ubicación, con escape defensivo si algún registro
      llegara con núcleo/galpón
- [x] R6 El export deja de aplicar también el filtro de **concepto**; sigue respetando la búsqueda de ítem
- [x] R7 Botón «Descargar Excel (todo el stock)» + nota y tooltip actualizados
- [x] R8 Tests: **25 specs** de la función (partición, hojas, sin-registros, Colombia, mapeo)
- [x] R9 `yarn build` 0 errores · `yarn test` **131/131 verdes**
- [x] R10 Smoke: con **granja BODEGA PRINCIAL KM 86 + concepto Alimento** la grilla queda en **0 filas**
      y el export igual pide `/stock` sin parámetros ⇒ **464 filas / 10 granjas**
- [x] R11 Contenido del `.xlsx` verificado sobre el XML: hojas `Alimento` (135 filas de 8 granjas, con
      Núcleo/Galpón) y `Otros conceptos` (329 filas de 10 granjas, 7 columnas). **135 + 329 = 464**:
      ninguna fila perdida ni duplicada; ninguna fila de alimento se coló en la segunda hoja
- [x] R12 Hoja vacía: búsqueda que solo matchea no-alimento ⇒ hoja `Alimento` con «Sin registros para
      este grupo.» y estructura intacta; la búsqueda sí viaja (`?search=AV0374`)
- [x] R13 Sin errores de consola; todas las llamadas 200. Servicios detenidos (4200 y 5002 libres)

---

# Gastos de inventario — las 10 líneas con `concepto = 'insumo'` (item 57 · AV0351)

**Plan:** [`fase_de_desarrollo/concepto_insumo_snapshot_gastos_plan.md`](fase_de_desarrollo/concepto_insumo_snapshot_gastos_plan.md)
**Fecha:** 2026-08-05 · **Alcance:** datos (empresa 3 ItalcolEcuador)
**Antecedente:** deuda que la sesión `claude/priceless-bhabha-c60ee5` (commit `84bf74f`) dejó fuera de
alcance por considerarla una hipótesis. Esta sesión la cierra con evidencia.

## Fase 1 — Investigación del origen
- [x] A1 Reproducido en BD local: 10 filas, `concepto = 'insumo'` exacto (6 bytes, sin caracteres ocultos), repartidas en **10 cabeceras distintas** (una línea cada una)
- [x] A2 **Un solo escritor**: `InventarioGastoService.CreateAsync` (491/503). Sin carga masiva, sin seed, sin `INSERT` crudo (los 2 `.sql` del módulo solo leen)
- [x] A3 **Entró por pantalla**: 10 auditorías `Crear` con payload de UI, 8 días (2026-07-14 → 2026-07-27), **4 usuarios** distintos; una con `Eliminar` motivo «Eliminación desde UI (gasto #135)»
- [x] A4 **El writer nunca cambió**: `git log -S` sobre `Concepto = item.Concepto` y sobre el mensaje del guard ⇒ un único commit, `b6f5d16` (2026-03-25, alta del módulo). Con el código de hoy esas filas **son imposibles**
- [x] A5 ⇒ el `concepto` del item 57 **sí fue distinto**: era `insumo`. El guard de la línea 447 habría rechazado el request si no
- [x] A6 **Testigo independiente**: `20260717192803_SeedItemInventarioPanamaDesdeEcuador` clona el catálogo 3→5 copiando `src.concepto` sin transformar, el **2026-07-17 15:34** (en plena ventana). Su copia de AV0351 (item 356) **sigue hoy en `insumo`** y es la **única divergencia entre los 148 códigos compartidos**
- [x] A7 `insumo` **nunca fue un concepto**: es un `tipo_item` (29 ítems de la empresa 3 lo tienen). El item 467 (alta 2026-08-04) muestra la combinación correcta `tipo_item = insumo` + `concepto = Otros insumos`
- [x] A8 Mientras duró, `GetConceptosAsync` **ofrecía `insumo`** en el desplegable: los usuarios lo eligieron de la lista, no lo inventaron
- [x] A9 **La corrección del catálogo ya ocurrió**, entre las 08:17 y las 17:05 del **2026-07-27** (última línea `insumo` vs. primera del mismo ítem con `Otros insumos`)
- [x] A10 …y fue **por fuera de la aplicación**: `updated_at` del item 57 sigue en 2026-03-23 (seed masivo) aunque `UpdateAsync` (177-178) y la importación por Excel (249-250) **siempre** lo tocan ⇒ SQL crudo, sin auditoría
- [x] A11 `xmin` descartado como fechador: las 467 filas comparten `xmin = 52338` (restauración de dump en bloque)
- [x] A12 ⚠️ **CORRECCIÓN de atribución** (el mensaje del commit `2cab258` dice otra cosa): el cambio de datos a mitad de la investigación —467→469 líneas, reaparición de los duplicados de capitalización, item 356 de vuelta en `insumo`— **no** lo causó la rama hermana aplicando y revirtiendo su `20260805180000`, sino la **restauración de la BD local desde prod** que hizo el usuario a las **18:42:30** del 2026-08-05 (confirmado: el directorio de `sanmarinoapplocal` fue recreado a esa hora). La conclusión operativa no cambia: la `20260805180000` **no** estaba aplicada en ninguna de las dos lecturas (no está desplegada, así que el dump de prod no la trae)

## Fase 2 — Decisión
- [x] B1 **Opción (a) confirmada por el usuario**: corregir las 10 filas a `Otros insumos`. El motivo del «fuera de alcance» ya no aplica — está probado que `insumo` es el `tipo_item` mal cargado del mismo producto, no una categorización de negocio distinta

## Fase 3 — Implementación de (a)
- [x] C1 Simulación `BEGIN; … ROLLBACK`: **UPDATE 10**, segunda pasada **UPDATE 0**, total invariante, detector a 0, y verificado que tras el `ROLLBACK` las 10 filas siguen en `insumo`
- [x] C2 Migración `20260805190000_CorregirConceptoInsumoSnapshotGastos` (data-only, Designer clonado de la `…170000`, `ModelSnapshot` **sin tocar** — verificado con `git diff`). Regla dinámica de 4 condiciones, sin ids ni etiquetas de negocio. `Down()` no restaura (irreversible por diseño, documentado)
- [x] C3 Aplicada a la BD local con `ASPNETCORE_ENVIRONMENT=Development` forzado — ⚠️ el `appsettings.json` base apunta a **RDS prod**; EF confirmó `Host: 127.0.0.1 | Port: 5433`. Una sola migración pendiente (la mía)
- [x] C4 `verificar_conceptos_catalogo_inventario.sql` **consulta 4: de 10 líneas a 0**. Las consultas 1 y 2 siguen con filas a propósito: son el alcance de la migración hermana (`20260805180000`), que **no está desplegada** y por eso tampoco viene en el dump de prod
- [x] C5 Conteos empresa 3: `Otros insumos` **196 → 206**, `insumo` **desaparece**, total de líneas **469 invariante** (T6)
- [x] C6 T1 las 10 filas en `Otros insumos` · T2 idempotencia `UPDATE 0` · T3 cero líneas de sola capitalización tocadas · el **catálogo no se tocó** (items 57 y 356 intactos)
- [x] C7 **El rastro histórico sobrevive**: `inventario_gasto_auditoria` conserva `"concepto":"insumo"` en el payload `Crear` de las 10 cabeceras
- [x] C8 `dotnet build` **0 errores / 0 warnings** · `dotnet test` **1602/1602 verdes** (1601 Application + 1 Domain)
- [x] C9 Sin procesos huérfanos (no se levantaron servicios)

## Fase 4 — Integración en `main` y validación sobre BD restaurada de prod (2026-08-05)
- [x] D1 `main` adelantado por **fast-forward** a `2cab258` (estaba limpio en `abe3643`; la rama ya tenía main incluido, sin merge commit)
- [x] D2 ✅ **La migración corrió contra el dump fresco de producción**, no contra datos locales viejos: la restauración fue a las 18:42:30 y el `database update` después. O sea las 10 filas existían tal cual en **prod** y quedaron corregidas
- [x] D3 `dotnet build` desde main: **0 errores / 0 warnings**
- [x] D4 `dotnet ef database update` desde main: *«No migrations were applied. The database is already up to date.»* · `Host: 127.0.0.1 | Port: 5433` confirmado
- [x] D5 **Historial alineado exacto**: 214 migraciones en el código de main = 214 en `__EFMigrationsHistory`; **cero** en la BD que no estén en el código y **cero** sin aplicar. ⇒ prod venía con las 213 de main y la 214ª es la nueva
- [x] D6 `dotnet ef migrations has-pending-model-changes`: *«No changes have been made to the model since the last migration»* ⇒ el `ModelSnapshot` quedó sano pese al Designer clonado
- [x] D7 `dotnet test` desde main: **1602/1602 verdes**
- [x] D8 Datos revalidados sobre la BD restaurada: 10 filas en `Otros insumos`, `insumo` en cero, total **469 invariante**, idempotencia `UPDATE 0`, catálogo intacto, auditoría conserva el valor viejo
- [x] D9 Sin procesos huérfanos · sin push ni deploy (siguen requiriendo pedido explícito)

### Pendiente de coordinación con la rama hermana
- [ ] Al integrar con `claude/priceless-bhabha-c60ee5`: el comentario de la consulta 4 de
      `backend/sql/verificar_conceptos_catalogo_inventario.sql` («Deuda conocida al 05-ago-2026:
      10 líneas con 'insumo'…») queda **obsoleto** — esa deuda ya está cerrada por esta migración

---

# Tracker — Traslado de aves: destino cross-granja/galpón en Engorde + fecha de registro visible

**Plan:** [`fase_de_desarrollo/traslado_aves_destino_cross_granja_y_fecha_registro_plan.md`](fase_de_desarrollo/traslado_aves_destino_cross_granja_y_fecha_registro_plan.md)
**Fecha:** 2026-08-05

**Pedido:** trasladar aves (pollo engorde y postura levante/producción) hacia **otras granjas y otros galpones**, y
distinguir **fecha del traslado** (la que edita el usuario) de **fecha de creación del registro** (`created_at`).

**Auditoría previa (el código manda):** postura YA tiene cascada de destino cross-granja y fecha editable; engorde NO
(select plano acotado a la granja filtrada). `created_at` YA se guarda y YA viaja en ambos DTOs, pero **no se pinta
en ninguna pantalla**. ⇒ sin migraciones: falta la cascada en engorde y exponer la fecha de registro.

## Backend — catálogo de lotes engorde para DESTINO
- [x] B1 `ILoteAveEngordeService.GetAllAsync(bool paraDestino = false)` (default preserva los llamadores existentes)
- [x] B2 `LoteAveEngordeService`: propagado a `AplicarScopeUbicacionAsync(q, paraDestino)` — patrón `LotePosturaLevanteService`; restricción por granjas asignadas SIN tocar
- [x] B3 `GET /api/LoteAveEngorde?paraDestino=true`

## Backend — simetría de destino en el movimiento
- [x] B4 `RellenarDestinoDesdeLoteDestinoSiFaltaAsync` + `UbicacionDelLoteDestinoAsync` (gemelos del lado origen) en `MovimientoPolloEngordeService.Crud.cs`; la decisión pura vive en `MovimientoPolloEngordeCalculos.ResolverUbicacionDestino`
- [x] B5 Regla **campo por campo** (corregida durante el smoke): elegir solo la granja en la cascada dejaba núcleo/galpón nulos aunque el lote destino los define ⇒ ahora lo explícito manda por campo y lo que falta se completa del lote
- [x] B6 `MovimientoPolloEngordeDestinoCalculosTests` — 11 casos (explícito completo, granja sin galpón, sin granja, núcleo vacío ×3, galpón sin núcleo, sin lote destino ×2, lote sin núcleo/galpón)

## Frontend — cascada de destino en el modal de engorde
- [x] F1 `lote-engorde.service.ts`: `getAll(paraDestino = false)`
- [x] F2 Modal engorde TS: inyectados Farm/Nucleo/Galpon/LoteEngorde + controles y handlers de cascada (carga perezosa: solo cuando el tipo no es Venta)
- [x] F3 Modal engorde HTML: bloque «Destino del traslado» (Granja → Núcleo → Galpón → Lote) reemplaza el select plano; al EDITAR se muestra el destino como texto (el update DTO no lo lleva, el select no guardaba nada)
- [x] F4 `mapear-movimiento-dto.funcion.ts` + service DTO: envía `granjaDestinoId`/`nucleoDestinoId`/`galponDestinoId` (nulos en venta)
- [x] F4b `funciones/filtrar-lotes-destino.funcion.ts` (pura) + README del módulo actualizado; `destinoOpciones` pasa de getter a campo con referencia estable (un getter que aloca por ciclo rompe el CD)

## Frontend — punto de entrada del traslado (hallazgo del smoke, NO estaba en el plan inicial)
- [x] E1 🔴 **En engorde no existía forma de crear un traslado**: `create()` fijaba `ventaPorGranjaMode = true` siempre ⇒ la cascada quedaba inalcanzable
- [x] E2 Botón **«Nuevo traslado»** + `crearTraslado()` / `canOpenTraslado` / `lotesTrasladoOrigen` (lotes ABIERTOS de la granja, sin exigir ventas registradas) + reset de `trasladoMode` al cerrar
- [x] E3 Modal: `@Input() trasladoMode` / `lotesOrigenTraslado`, select de lote ORIGEN, tipo fijado a `Traslado` y bloqueado, disponibilidad real del origen vía `aves-disponibles-lotes` (mismo número que valida el backend) y destino obligatorio antes de confirmar

## Frontend — fecha de registro visible (columna + detalle + Excel)
- [x] F5 Lista engorde: columnas «Fecha traslado» y «Registrado» (+ `createdAt` propagado en `FilaDespachoGrupo` / `agrupar-despachos.funcion.ts`, colspans 12→13)
- [x] F6 Detalle engorde: «Fecha del traslado» + «Registrado el»; en el formulario, nota que distingue ambas fechas
- [x] F7 `exportar-ventas-excel.funcion.ts`: cabeceras «Fecha traslado» + «Registrado»
- [x] F8 Lista postura `movimientos-aves`: línea «Reg. dd/MM/yyyy HH:mm» en la celda «N° / Fecha» (+ estilo `.mov-registro`) y nota en el modal

## Validación
- [x] V1 `cd backend && dotnet build` — **0 errores / 0 advertencias**
- [x] V2 `cd backend && dotnet test` — **1613 verdes** (1612 Application + 1 Domain; baseline 1601 + 11 casos nuevos)
- [x] V3 `cd frontend && yarn build` — 0 errores (único warning: bundle budget preexistente)
- [x] V4 **Smoke UI real** (front :4200 + back :5002 + BD local :5433, sesión inyectada en localStorage):
      - Traslado creado desde CAROLINA (granja 45, galpón G0061) hacia **Sacachun 3A (granja 41, galpón G0043)** — otra granja Y otro galpón
      - `granja_destino_id=41`, `nucleo_destino_id=685062`, `galpon_destino_id=G0043` — núcleo y galpón **autocompletados por el backend** desde el lote (en la cascada solo se eligió la granja)
      - Fecha de traslado **retroactiva** 2026-08-01 vs `created_at` 2026-08-05 21:35 ⇒ las dos fechas conviven y se ven distintas en la tabla
      - Al completar, las aves se mueven de verdad: origen 952→942 H, destino 673→683 H
      - Excel exportado contiene ambas cabeceras y la fila `1/8/2026 | 5/8/2026, 21:35:56`
      - Lista de postura renderiza «10/07/2026» + «Reg. 17/07/2026 09:22» (datos reales con 7 días de diferencia que hasta ahora eran invisibles)
- [x] V5 **BD local restaurada**: el movimiento de prueba se revirtió por el flujo de la app (estado `Anulado`, `deleted_at` seteado) y los maestros volvieron exactos (99: 952 H / 90: 673 H)
- [x] V6 Sin procesos huérfanos (backend y dev server detenidos; sesión de smoke borrada del navegador)
- [x] V7 Commit acotado (sin footer de atribución)

### Fuera de alcance (deuda preexistente detectada, no tocada)
- Los `movimiento_aves` tipo TSD de la BD local tienen `company_id = 0` y por eso el listado de postura no los
  muestra — es la deuda ya registrada en `movimientos-tsd-company-id-gate`, ajena a este cambio.
- `movimientos-aves` no tiene exportación a Excel; el punto «Excel» del pedido se cubrió en engorde, que sí la tiene.

---

# Tracker — Cohortes: cuántas aves, de dónde y con qué edad en el lote receptor

**Plan:** [`fase_de_desarrollo/cohortes_edades_lote_receptor_plan.md`](fase_de_desarrollo/cohortes_edades_lote_receptor_plan.md)
**Fecha:** 2026-08-06

**Auditoría previa:** el mecanismo (`lote_aves_cohortes`) existe y es correcto, pero solo lo escriben 2 de los 3
caminos de postura y **engorde no lo tiene en absoluto**. Además: el traslado desde seguimiento deja
`lote_destino_id` NULL (hueco de duplicación en la carga masiva) y el techo de venta de engorde no sube cuando un
lote recibe aves (se marcarían como sobreventa). Detalle en el plan.

## Modelo
- [x] M1 `LoteAvesCohorte` + config: `granja_origen_id` / `nucleo_origen_id` / `galpon_origen_id` (nullable ⇒ las cohortes viejas quedan en null)
- [x] M2 Entidad + config nuevas `LoteEngordeAvesCohorte` → `lote_engorde_aves_cohortes` (incluye mixtas)
- [x] M3 `DbSet` + migración `20260806031924_AddCohortesEngordeYUbicacionOrigen` reescrita a SQL crudo **idempotente**; aplicada local y verificada: segunda pasada de `ADD COLUMN IF NOT EXISTS` sin error

## Escritura
- [x] W1 Engorde: cohorte al COMPLETAR (mismo `SaveChanges` que acredita el maestro); baja lógica al eliminar
- [x] W2 Postura `MOV-*`: `RegistrarCohorteDestinoMovimientoAsync` tras crear la fila de entrada; idempotente por movimiento; baja lógica al cancelar
- [x] W3 Postura TSD: `LoteDestinoId = destino.LoteBaseId` — además de la trazabilidad, **cierra un hueco de duplicación**: la idempotencia de la carga masiva busca por `LoteDestinoId == loteId` y no veía estos traslados
- [x] W4 Ubicación de origen congelada en los 4 escritores (TSD, carga masiva, MOV, engorde)

## Lectura
- [x] R1 `BaselineConCohortes` + `PropiasDelLote` + `DescribirUbicacionOrigen` (puros) — 9 casos xUnit nuevos
- [x] R2 Auditoría de ventas engorde: el techo suma las cohortes VIGENTES (no las anuladas). Se resolvió leyendo las cohortes en vez de escribir en `historial_lote_pollo_engorde`, que **no tiene soft-delete** y habría exigido filas negativas al revertir
- [x] R3 DTO: `UbicacionOrigen` por cohorte + `HembrasPropias`/`MachosPropias` del lote
- [x] R4 `GET /api/MovimientoPolloEngorde/cohortes/{loteAveEngordeId}` (mismo DTO que postura ⇒ el componente se reutiliza sin cambios)

## Frontend
- [x] U1 Columna «Procedencia» + fila «Propias» con cantidades + nota que explica que las bajas son por lote
- [x] U2 `app-edades-lote` con `@Input() linea: 'postura' | 'engorde'` montado en la pantalla de engorde (recarga al guardar un movimiento)

## Validación
- [x] V1 `dotnet build` **0 errores / 0 advertencias** · `dotnet test` **1622 verdes** (1621 Application + 1 Domain)
- [x] V2 `yarn build` sin errores (solo el warning de bundle budget preexistente)
- [x] V3 ✅ **Smoke engorde COMPLETO** (lote 99 encaset 17-jun, CAROLINA/G0061 → lote 90 encaset 3-jun, Sacachun 3A/G0043):
      - `Pendiente` ⇒ **0 cohortes** (la cohorte nace al completar, no al crear)
      - `Completado` ⇒ cohorte con `fecha_encaset_cohorte = 17-jun` (la del **ORIGEN**, no la del receptor) y procedencia congelada `CAROLINA · 668786 · G0061`
      - Panel de edades: fila «Propias» 673 H/570 M **edad 64 días (sem 10)** + fila «Recibidas» 100 H/40 M **edad 50 días (sem 8)** ⇒ dos edades en el mismo lote
      - Techo de venta: `encasetadasH` **13.640 → 13.740** y `M` **15.051 → 15.091**, `exceso = 0`, estado OK
      - Eliminado ⇒ cohorte **anulada** (`deleted_at`, no borrada), maestros restaurados y techo de vuelta a **13.640/15.051**
- [x] V4 ✅ **Smoke postura `MOV-*` COMPLETO** tras desbloquear la vía (ver el bloque siguiente del tracker)
- [x] V5 Regresión: un movimiento sin lote destino no crea cohorte (guardas explícitas) y sin cohortes el techo devuelve el `Inicio` idéntico (test dedicado)
- [x] V6 BD local restaurada al snapshot exacto (`movimiento_aves` max id 18, `movimiento_pollo_engorde` max id 1806, 0 cohortes) · servidores detenidos · commit sin footer de atribución

### ✅ RESUELTO — el bug que bloqueaba el camino `MOV-*`

`inventario_aves.lote_id` e `historial_inventario.lote_id` eran **`character varying`** en la BD mientras
las entidades declaran **`int`**. Toda consulta que los comparara moría con
`42883: operator does not exist: character varying = integer`, y como `ProcesarMovimientoAsync` guarda
`Estado = "Completado"` ANTES de tocar el inventario y `CreateAsync` sólo hace `LogError`, **el movimiento
quedaba marcado como completado sin haber movido una sola ave**.

Corregido con el OK del usuario en la migración `20260806050306_AlinearLoteIdInventarioAvesAInteger`
(ver el bloque de tracker siguiente).

---

# Tracker — Alinear `lote_id` de inventario a `integer` (desbloquea el traslado `MOV-*`)

**Plan:** [`fase_de_desarrollo/cohortes_edades_lote_receptor_plan.md`](fase_de_desarrollo/cohortes_edades_lote_receptor_plan.md) (§5)
**Fecha:** 2026-08-06 · **Pedido:** «realizá la corrección, aplicá siempre migraciones y validá que esté funcionando correctamente»

`inventario_aves.lote_id` e `historial_inventario.lote_id` eran `character varying` con entidades que
declaran `int`. Por la regla «el código manda» de `CLAUDE.md` gana el código (`lotes.lote_id` ya es
`integer`), así que se alinean las dos columnas a `integer`.

## Auditoría previa al DDL
- [x] A1 Tipos reales: `inventario_aves.lote_id` y `historial_inventario.lote_id` = `character varying`; `lotes.lote_id` = `integer`
- [x] A2 Datos: **ambas tablas VACÍAS** en el dump de producción ⇒ conversión sin riesgo de pérdida
- [x] A3 Dependencias: **sin FK**, **sin vistas**; único índice sobre la columna (`ix_*_lote_id`) que Postgres reconstruye solo
- [x] A4 Barrido de otras tablas con `lote_id varchar`: `seguimiento_diario_levante` es **correcta** (su entidad `SeguimientoDiario` usa `string`); `lote_galpones` / `lote_reproductoras` / `lote_seguimientos` / `produccion_lotes` / `traslado_huevos` también (entidades `string`). Solo estas dos estaban desalineadas

## Migración
- [x] M1 `20260806050306_AlinearLoteIdInventarioAvesAInteger` — EF la generó vacía (el modelo ya cree `int`; el desvío era solo de la BD) ⇒ DDL escrito a mano, `ModelSnapshot` sin tocar
- [x] M2 **Idempotente**: sale sin hacer nada si la columna no existe o ya es `integer` (probado: segunda pasada → `NOTICE ... sale sin hacer nada`)
- [x] M3 **Defensiva**: antes de convertir cuenta filas nulas/vacías/no numéricas y aborta con mensaje explícito en vez de romper con un cast críptico o descartar datos en silencio
- [x] M4 Guard probado con dato malo en `BEGIN/ROLLBACK`: `ERROR: No se puede alinear public.inventario_aves.lote_id a integer: 1 fila(s)...`
- [x] M5 `Down()` inverso e idempotente
- [x] M6 Aplicada: las 3 columnas en `integer`, índices reconstruidos

## Validación funcional — camino `MOV-*` de postura
- [x] V1 Primer intento con los lotes 115/116: el 42883 **desapareció** (`inventario_aves` ya se escribe) pero las aves no se movieron ⇒ **no era otro bug**: esos lotes están en semana ~42 (Producción por edad) y solo tienen espejo de Levante, así que ambos caminos de descuento salen por sus guardas. Dato de prueba inadecuado
- [x] V2 Se crearon 2 lotes de validación en LA ESMERALDA realmente en levante: **130** (encaset 07-jun, semana 9, G0319) y **131** (encaset 07-jul, semana 5, G0320)
- [x] V3 Traslado 400 H + 50 M de 130 → 131 por el módulo «Movimientos de Aves»:
      - **Aves movidas**: 130 `5000/500 → 4600/450` · 131 `3000/300 → 3400/350`
      - **Filas diarias**: SALIDA en el origen, INGRESO en el destino
      - **Cohorte creada**: receptor 131, origen 130, procedencia congelada **LA ESMERALDA · 591408 · G0319**, `fecha_encaset_cohorte = 07-jun` (la del **ORIGEN**, no la del receptor 07-jul)
      - **Panel de edades**: «Propias» 3.000 H/300 M **edad 30 días (sem 5)** + «Recibidas» 400 H/50 M **edad 60 días (sem 9)** ⇒ dos edades conviviendo
      - **Cuadre**: propias 3.000 + recibidas 400 = **3.400 = saldo actual** ✔
- [x] V4 Reversión por **Cancelar**: cohorte **anulada** (`deleted_at`, no borrada) y aves devueltas exactas (130 `5000/500`, 131 `3000/300`)

## Cierre
- [x] C1 `dotnet build` **0/0** · `dotnet test` **1622 verdes**
- [x] C2 `dotnet ef database update` ⇒ *«already up to date»* · `has-pending-model-changes` ⇒ *«No changes»* (el DDL a mano no ensució el snapshot)
- [x] C3 BD restaurada al snapshot exacto: `movimiento_aves` max id **18**, `lotes` max **129**, cohortes **0**, `inventario_aves` **0**, `historial_inventario` **0**, lotes 115/116 intactos
- [x] C4 Sin procesos huérfanos · commit sin footer de atribución

---

# Tracker — Archivos de carga masiva del lote S-369AB (postura: levante + producción + alimento)

**Plan:** [`fase_de_desarrollo/carga_masiva_s369ab_postura_plan.md`](fase_de_desarrollo/carga_masiva_s369ab_postura_plan.md)
**Fecha:** 2026-08-06 · **Pedido:** «con estos archivos construir los archivos de migración que necesita
la carga masiva» para el lote S-369 (regional Centro), en granja de pruebas hasta galpón de pruebas.

## Lectura de las fuentes
- [x] F1 `INFORME TECNICO LEVANTE S-369AB.xlsm` venía **truncado** (ZIP sin central directory) → reconstruido entrada por entrada; única pérdida `calcChain.xml` (caché de fórmulas, irrelevante)
- [x] F2 El lote son DOS sublotes: **S-369A** encaset 2025-08-30 (10.167 H / 1.472 M) y **S-369B** encaset 2025-09-05 (10.291 H / 1.521 M) = 20.458 H / 2.993 M
- [x] F3 Las hojas `… general 369AB` consolidan **por EDAD, no por fecha** (0/175 discrepancias por índice vs 172/175 por fecha) ⇒ el consolidado se arma sumando A+B **por calendario**
- [x] F4 Conciliación hembras: `20.458 − 669 mort − 614 sexaje − 157 desc = 19.018` al 2026-02-19 = arranque exacto de producción, 0 días inexplicados
- [x] F5 Conciliación machos: 4 saltos sin columna → A→B 196 (traslado interno, se anula), **150 el 09-feb**, **140 el 17-feb**, −20 el 23-feb (ya está en producción). Con los 290 retiros cierra en **1.957** = arranque de producción
- [x] F6 Producción 2026-02-20→2026-07-30 (161 días); las 9 «Entradas (+)» son todas **negativas** = salidas de aves
- [x] F7 `CONSUMOS S369.xlsx`: 7 bloques de alimento; el etiquetado H/M **no es fiable por bloque** (el consumo de hembras de PRODUCCION II está bajo la columna M) → regla de sexo por tipo de alimento, validada por magnitud g/ave
- [x] F8 Nunca hay más de 2 tipos de alimento por sexo en un día (0/335) ⇒ entra en los 4 slots de la plantilla

## Contrato del importador (leído del código)
- [x] C1 Esquemas exactos de `SeguimientoLevante`, `SeguimientoProduccion`, hoja `Alimento`, `Movimientos Aves`, `Movimientos Huevos`
- [x] C2 **Trampa**: una *Advertencia* dentro del bloque de la fila la descarta en silencio ⇒ con slots de alimento, `Consumo H/M (kg)` va VACÍO; con las 11 categorías, `Huevo Total`/`Incubable` van VACÍOS
- [x] C3 Hoja `Huevos` **prohibida** (Sanmarino tiene `clasificacion_huevo_por_items = false` ⇒ error fail-closed)
- [x] C4 `Movimientos Aves` tipo **Salida** exige contraparte existente ⇒ los retiros van como **Venta**
- [x] C5 Hoja `Alimento`: `Origen` vacío (con `granja`/`bodega` el ingreso falla SIEMPRE) y ubicación vacía (stock a nivel granja en Sanmarino)
- [x] C6 Gate de stock: rechazo total del archivo si el consumo supera `stock + entradas del archivo`

## Estado de la BD local (solo lectura)
- [x] B1 Granja de pruebas viva = `farms.id 44` «Pruebas Moises»; única ubicación `nucleo 883195` + `galpon G0443`
- [x] B2 **No existe la raza «ROSS AP»** en Sanmarino → `raza = 'AP'`, `ano_tabla_genetica = 2026`
- [x] B3 Regional «Centro» = `master_list_options.id 57`; la granja 44 apunta a `regional_id 27` (**huérfano**)
- [x] B4 Granja 44 con **0 stock** de inventario ⇒ el alimento tiene que entrar por la hoja `Alimento`
- [x] B5 Mapeo de los 7 alimentos al catálogo por **código** (hay 3 ítems con el nombre idéntico `PRODUCCION III REPRODUCTORA PESADA`)
- [x] B6 No existe ningún lote `S-369`/`S369`: sin riesgo de duplicado

## Generación de los archivos
- [x] G1 `Carga_Masiva_Levante_S-369AB.xlsx` — Datos **174** filas (2025-08-30→2026-02-19) · Alimento **37** ingresos · Movimientos Aves **2** (los retiros de 150 y 140 machos, tipo Venta)
- [x] G2 `Carga_Masiva_Produccion_S-369AB.xlsx` — Datos **161** filas (2026-02-20→2026-07-30) · Alimento **58** ingresos · Movimientos Aves **9** (las «Entradas (+)» negativas)
- [x] G3 Verificación automática: **todos los chequeos OK**
      - encabezados byte a byte iguales al esquema, en A1, sin duplicados; fechas únicas, ordenadas, ≥ encaset y ≤ hoy
      - ninguna fila mezcla slots de alimento con `Consumo H/M (kg)` ni categorías con `Huevo Total` (las dos advertencias que descartan filas en silencio)
      - hoja `Alimento`: `Origen` y ubicación vacías, `Movimiento`=Ingreso, clave de idempotencia única
      - **aves**: levante `20.458 − 1.440 = 19.018 H` · `2.993 − 746 − 290 = 1.957 M` → producción `19.018 − 672 − 338 = 18.008 H` · `1.957 − 193 − 130 = 1.634 M`
      - **huevos**: 2.213.857 totales / 2.032.069 incubables (= columna Apto del informe, exacto)
      - **alimento**: consumo del archivo idéntico al informe día a día (dif 0,000 kg); ningún ítem negativo tras encadenar levante→producción
      - hallazgo: el informe fuente tiene un desvío propio de **5 huevos** el 2026-06-30 (col «Producción Huevos» 14.038 vs su clasificación 14.043) — se cargó la clasificación
- [x] G4 `LEEME_S-369AB.md` junto a los archivos: ficha de alta del lote (granja 44 / núcleo 883195 / galpón G0443, encaset 2025-08-30, 20.458 H + 2.993 M, **raza `AP` y no «ROSS AP»**, año 2026), los 4 pasos operativos y las 5 salvedades
- [x] G5 Scripts reproducibles copiados a `…/lote carga masiva pruebas/scripts/` (`recover.py` repara el .xlsm truncado, `construir.py` genera, `verificar.py` valida)

---

# Tracker — `tipo_alimento` desborda varchar(100) y tumba el guardado del seguimiento diario

**Plan:** [`fase_de_desarrollo/tipo_alimento_varchar_desborde_plan.md`](fase_de_desarrollo/tipo_alimento_varchar_desborde_plan.md)
**Fecha:** 2026-08-06 · **Pedido:** «implementá las migraciones y las correcciones y realizá la validación en local»

Reportado como «falla al guardar el lote A374A» (Sanmarino Colombia). Diagnóstico reproducido: el front
concatena los nombres de los alimentos en `tipo_alimento` (`varchar(100)`) y el TERCER alimento pasa de
100 ⇒ `22001` ⇒ `DbUpdateException` ⇒ 500 con el texto genérico de EF. No es el lote.

## Diagnóstico (cerrado)
- [x] D1 Inner exception real capturada: `22001: value too long for type character varying(100)`
- [x] D2 Repro: 3 alimentos (113 chars) → 500 idéntico al reporte · 2 alimentos (76) → 201
- [x] D3 Confirmado en datos: `max(length(tipo_alimento))` de TODA la tabla = **79** (nunca entró un tercero)
- [x] D4 Rollback verificado íntegro (0 filas, aves y stock intactos) ⇒ sin datos corruptos

## Backend — lógica pura + tests
- [x] B1 `Application/Calculos/TipoAlimentoCalculos.cs` (`MaxLongitud = 500`, `Recortar`)
- [x] B2 `Application/Calculos/ErrorPersistenciaCalculos.cs` (`DescribirErrorSql`, `null` si no mapeado)
- [x] B3 Tests xUnit T1-T8 de `TipoAlimentoCalculos`
- [x] B4 Tests xUnit E1-E5 de `ErrorPersistenciaCalculos`

## Backend — aplicación de la red de seguridad
- [x] B5 `SeguimientoLoteLevanteService.Mapeos.cs` — create + update
- [x] B6 `SeguimientoAvesEngordeService.Crud.cs` — create + update
- [x] B7 `SeguimientoAvesEngordeEcuadorService.Crud.cs` — create + update
- [x] B8 `MigracionService.Historicos.cs` — el `MaxTipoAlimento = 100` local pasa a delegar (deja de mutilar)
- [x] B9 `Program.cs` — el handler global traduce el `SqlState` en vez de devolver el texto de EF

## BD
- [x] M1 `SeguimientoDiarioConfiguration` (levante) a `HasMaxLength(500)`. ⚠️ **Engorde NO se amplió**: al
      aplicar la 1ª versión en local, Postgres devolvió `0A000 cannot alter type of a column used by a
      view or rule` — la vista de Power BI `vw_seguimiento_pollo_engorde` cuelga de
      `seguimiento_diario_aves_engorde.tipo_alimento`. Sus configurations vuelven a 100
      (`TipoAlimentoCalculos.MaxLongitudEngorde`) y quedan cubiertas por el recorte
- [x] M2 Migración `20260806063157_AmpliarTipoAlimentoSeguimientos`, DDL **idempotente** a mano: omite si
      la columna no existe, si ya es ≥500, o si tiene vistas dependientes (WARNING en vez de fallar —
      un deploy que no aplica el ancho es recuperable; uno que no arranca, no)
- [x] M3 `Down()` inverso, aborta si hay datos que no entrarían en 100
- [x] M4 Aplicada en local + segunda pasada = no-op
- [x] M5 `has-pending-model-changes` → «No changes» (snapshot alineado)

## Validación
- [x] V1 `dotnet build` 0 errores / 0 advertencias nuevas
- [x] V2 `dotnet test` verde
- [x] V3 Smoke S1 — 3 alimentos en A374A (lote 116) → **201** con el `tipo_alimento` completo
- [x] V4 Smoke S2 — control de 2 alimentos sin regresión
- [x] V5 Smoke S3/S4/S5 — inventario y aves exactos, edición y borrado
- [x] V6 Smoke S6 — 600 chars → recorte a 500, sin 500 HTTP
- [x] V7 BD local restaurada al snapshot exacto + sin procesos huérfanos
- [x] V8 Commit sin footer de atribución

---

# Tracker — E2E del lote S-369: alta, carga de levante y validación de los reportes

**Plan:** [`fase_de_desarrollo/carga_masiva_s369ab_postura_plan.md`](fase_de_desarrollo/carga_masiva_s369ab_postura_plan.md)
**Fecha:** 2026-08-06 · **Pedido:** «validá la información a cargar, registrá el lote base y el lote,
cargá el levante con la guía AP 2026, y que los reportes de levante y el semanal cuadren con el Excel»

Backend local propio en `:5499` (Development, BD `sanmarinoapplocal:5433`), JWT + `X-Secret-Up` minteados.
Backup previo en `snapshot_pre_S369.dump`.

## Alta y carga
- [x] A1 `lote_postura_base` **S-369** (id 30) + lotes **S-369A** (135, encaset 30-ago, 10.167 H / 1.472 M) y **S-369B** (136, encaset 05-sep, 10.291 H / 1.521 M), granja 44 / núcleo 883195 / galpón G0443, raza `AP`, año tabla `2026`
- [x] A2 **Validar** (dry-run): 0 errores; los 3 avisos de saldo proyectado de alimento coinciden al gramo con lo calculado antes de importar
- [x] A3 **Importar**: A 174/174 y B 168/168 filas, 0 errores
- [x] A4 Saldos: **9.484 + 9.534 = 19.018 H** y **966 + 991 = 1.957 M** = arranque exacto del informe de producción
- [x] A5 Inventario: 37 ingresos (257.900 kg) y 553 consumos (247.269,6 kg) a nivel **granja**; saldo final 10.630,40 kg en 3 ítems — idéntico a lo proyectado
- [x] A6 Guía genética: `vw_guia_genetica_por_lote_postura` resuelve **AP / 2026**, semanas 1-25, con los MISMOS valores que las columnas «Tabla» del Excel (21/26/30/34/35/37/39/42 g/ave; 147/329/539/778/1.024/1.284 acum; pesos 145/260/380/490/590/680)

## Validación de reportes
- [x] R1 **Reporte diario** vs archivo cargado: **0 discrepancias** en 174 días (mortalidad, selección, error de sexaje y consumo por sexo)
- [x] R2 **Reporte semanal** vs agregación propia de los mismos datos: **0 diferencias** en 25 semanas ⇒ la agregación semanal del sistema es exacta
- [x] R3 **`/api/ReporteTecnicoSemanal/levante` (el de Sanmarino) vs `Registro Semanal general 369AB`: 24 de 25 semanas IDÉNTICAS** — saldo, mortalidad, selección, error de sexaje, consumo kg, g/ave/día, peso corporal y uniformidad, al decimal. La semana 25 sale parcial **a propósito**: el corte de levante es el 19-feb y los 7 días restantes pertenecen al informe de producción
- [x] R4 Totales del ciclo: selección **157/157** y error de sexaje **614/614** exactos; mortalidad 669 vs 676 e igual con el consumo (212.906,2 vs 221.874,2 kg) — la diferencia es **exactamente** la de esos 7 días post-corte

## Hallazgos (defectos reales, NO corregidos: requieren confirmación)
- [x] H1 🔴 **Un lote histórico nace como «Producción» y desaparece de los reportes de levante.** `LoteService.cs:340` deriva `fase = semanasDesdeEncaset >= 26 ? "Produccion" : "Levante"`, así que cualquier lote con encaset de más de 26 semanas nace en Producción; el trigger lo copia a `lote_postura_levante.etapa/estado`. `ReporteTecnicoService.cs:2557` filtra `lpl.Etapa == "Levante"` y `ReporteTecnicoSemanalService.Levante.cs:25` filtra `l.Fase != "Produccion"` ⇒ **los dos reportes salen vacíos**. La carga masiva sí lo acepta (su elegibilidad solo pide un LPL vivo), así que el dato entra y el reporte no lo ve. Se corrigió a mano en local para poder validar
- [x] H2 🔴 **Tocar `lotes` resetea las aves vivas.** `trg_lotes_sync_lote_postura_levante` hace, en su rama UPDATE, `aves_h_actual = NEW.hembras_l` / `aves_m_actual = NEW.machos_l` sin condición: editar cualquier campo del lote (técnico, regional, fase…) devuelve el saldo al encasetamiento. Acá habría borrado el descuento de las 1.440 hembras y 1.036 machos
- [x] H3 🔴 **`ReporteTecnico/levante/obtener` no descuenta el error de sexaje del saldo.** `ReporteTecnicoService.cs:2916` `hembraActual = avesHInicialesTotal - acMortH - acSelH` (y el diario en `:2750` `saldoH -= mortH + selH`) — falta `acErrH`, que sí se calcula dos líneas más abajo para `retAcH`. Efecto medido: el reporte cierra en **19.632** hembras contra las **19.018** reales del maestro y del Excel (614 aves, 3,2 %), y arrastra el g/ave/día (`:2944` divide por ese saldo): semana 24 **109,81 vs 113,35** g/ave/día. El reporte semanal de Sanmarino sí lo descuenta bien
- [x] H4 🟠 **Una `Salida` con contraparte bloquea el `Ingreso` del lote destino.** La Salida escribe `lote_destino_id = <B>` y la idempotencia del Ingreso busca «un Traslado/Venta del mismo día, mismas cantidades, con este lote como destino» ⇒ lo toma por duplicado y lo omite **sin acreditar las aves**. Medido: B cerraba en **795** machos en vez de 991. En los archivos se modela el débito como `Venta` (sin destino) y el crédito como `Ingreso`
- [x] H5 🟡 El informe fuente tiene un desvío propio de **5 huevos** el 2026-06-30 (col «Producción Huevos» 14.038 vs su clasificación 14.043)

## Cierre
- [x] C1 Backend detenido, puerto 5499 libre, **0 procesos dotnet** huérfanos
- [x] C2 Estado final en local: base **S-369** (30) con **S-369A** (135) y **S-369B** (136) cargados y visibles en los reportes; granja 44 con 10.630,40 kg de saldo en 3 ítems
- [x] C3 Nada preexistente fue tocado: las 588 filas de seguimiento previas quedaron con 0 modificaciones

## 2ª ronda — alineación total (pedido: «realizá el cambio del alcance para dejar todo alineado y corregido»)

- [x] A1 `AmpliarTipoAlimentoEngorde`: las 3 tablas de engorde a varchar(500) **recreando las 3 vistas de
      Power BI** (captura definición + dueño + GRANTs vía `aclexplode` + comments; drop de la más
      dependiente a la más base; recreación inversa restaurando todo). Sin renombrar: Power BI apunta ahí
- [x] A2 Todo el bloque en `BEGIN … EXCEPTION WHEN OTHERS` (subtransacción) ⇒ **no puede tumbar el deploy**;
      ejerció de verdad en la validación (`text || "char"` sin cast → degradó a WARNING con las 3 vistas
      intactas, en vez de abortar el arranque). Corregido con `relkind::text`
- [x] A3 Un solo tope: `TipoAlimentoCalculos.MaxLongitud = 500` para las 4 tablas; se elimina `MaxLongitudEngorde`
- [x] A4 Red de seguridad CENTRALIZADA en `SeguimientoDiarioService` (los 3 puntos de escritura de la tabla
      unificada: alta, edición y merge sobre traslado) ⇒ cubre también a `LoteSeguimientoService`, que
      delega ahí y no estaba protegido. Se quita la duplicada de `SeguimientoLoteLevanteService.Mapeos`
- [x] A5 Migración de la 1ª ronda quedó marcada como aplicada sin efecto (el guard la saltó) ⇒ se
      des-marcó en local y se reaplicó, probando el camino real del deploy
- [x] A6 `ZooSanMarinoContextModelSnapshot` realineado a mano: la migración de otra sesión lo regeneró
      desde un modelo anterior al cambio y dejó engorde en 100 ⇒ `has-pending-model-changes` en verde
- [x] A7 **Vistas verificadas idénticas** tras el ALTER: definición byte a byte, mismo dueño, mismas
      columnas (35/57/65) y mismas filas (5.663 / 170 / 5.736)
- [x] A8 Escritura real de 300 chars en `seguimiento_diario_aves_engorde` (BEGIN/ROLLBACK) → acepta
- [x] A9 2ª pasada del `Up` = no-op · `dotnet build` 0/0 · `dotnet test` verde · smoke S1/S2/S6 repetido
- [x] A10 BD restaurada (aves 7405/738, stock 588.5/9360/320, 144 segs del lote 116) · trabajo de la otra
      sesión intacto · sin procesos huérfanos

---

# Tracker — Corrección de los 3 defectos del E2E S-369 (con gate multiempresa)

**Plan:** [`fase_de_desarrollo/carga_masiva_s369ab_postura_plan.md`](fase_de_desarrollo/carga_masiva_s369ab_postura_plan.md)
**Fecha:** 2026-08-06 · **Pedido:** «corregí los puntos con validación QA de cada uno, que no aparezcan
errores, porque son cosas que pueden pasar entre empresas»

## 1 · Un lote histórico nacía en «Producción» y desaparecía de los reportes de levante
- [x] 1a `Application/Calculos/FaseLoteCalculos.cs`: la fase pasa a ser un dato **opcional** de entrada.
      Con `Fase` vacía se conserva **byte a byte** la derivación anterior (≥ 26 semanas ⇒ Producción)
- [x] 1b `CreateLoteDto.Fase` y `UpdateLoteDto.Fase` (nullable); `LoteService` delega en el cálculo.
      En `UpdateAsync` la fase **solo** se toca si el DTO la trae — editar un lote nunca la recalcula
- [x] 1c Fase inválida ⇒ `ArgumentException` ⇒ HTTP 400, sin crear nada
- [x] 1d **18 tests** (`FaseLoteCalculosTests`): el corte en 26 semanas intacto, los 61 valores de
      `Resolver(null, 0..60)` idénticos a `DerivarPorEdad`, normalización y rechazos

## 2 · Editar un lote reseteaba las aves vivas
- [x] 2a Migración `20260806074742_ArreglarTriggerSyncLotePosturaLevanteNoPisarAvesVivas`:
      la rama UPDATE del trigger deja de hacer `aves_h_actual = NEW.hembras_l`
- [x] 2b El saldo vivo ahora se corre por el **delta** del encasetamiento, con `GREATEST(0, …)`.
      `aves_*_inicial` sigue espejando `hembras_l`/`machos_l`. La rama INSERT no cambia
- [x] 2c Idempotente (`CREATE OR REPLACE`) y con `Down()` que restituye el comportamiento previo

## 3 · El reporte técnico de levante no descontaba el error de sexaje (ni los traslados)
- [x] 3a `Application/Calculos/SaldoAvesLevanteCalculos.cs`: especificación ejecutable de
      `fn_reporte_semanal_levante_extras` — `saldo = inicial − mort − sel − error_sexaje − salidas + ingresos`
- [x] 3b `ReporteTecnicoService` delega en ese cálculo en el bucle **diario** y en el **semanal**;
      se agregaron los 4 campos de traslado a las 2 proyecciones de `SegLevanteParaReporte`
- [x] 3c **21 tests** (`SaldoAvesLevanteCalculosTests`), incluidos los cierres reales del S-369
      (19.018 H y 1.957 M) y el número que devolvía el bug (19.632)

## QA
- [x] Q1 `dotnet build` **0 errores / 0 advertencias** · `dotnet test` **1.689 verdes, 0 fallos**
- [x] Q2 **QA-1 (fase)** 7/7 OK: encaset viejo sin fase ⇒ Producción (igual que antes); con
      `fase=Levante` ⇒ Levante y el espejo lo hereda; encaset reciente sin fase ⇒ Levante; fase
      inválida ⇒ 400 sin dejar el lote a medias
- [x] Q3 **QA-2 (trigger)** 5/5 OK: editar el técnico **no** mueve el saldo (antes lo devolvía al
      encaset); corregir el encaset +50 H/+10 M corre el saldo de 70/6 a 120/16 conservando las
      bajas; un encaset menor que las bajas satura en 0 sin negativos
- [x] Q4 **QA-3 · gate multiempresa** — 130 semanas de **2 empresas** (Sanmarino y Demo) comparadas
      contra la fn canónica: **42 semanas CORREGIDAS · 0 REGRESIONES** · 57 ya coincidían ·
      23 diferencias preexistentes que el cambio no toca (lote A374A, cuya fn devuelve saldos
      negativos que el reporte satura en 0, igual antes que ahora)
- [x] Q5 Los 8 casos que aún no igualan a la fn **mejoran todos** su distancia al valor canónico
      (K345A 63→1, K345B 16→2, Demo 10.200→5.100…), **0 empeoran**. El residuo es de dos diferencias
      preexistentes y fuera de este cambio: la fn no satura en 0 y toma la base de `hembras_l` con
      fallback al primer ingreso, mientras el reporte usa `Σ aves_h_inicial` de los sublotes
- [x] Q6 Caso que originó todo: `ReporteTecnico/levante/obtener` sobre el S-369 pasó de **14 a 24 de
      25 semanas idénticas** al Excel; semana 24 saldo **19.018** (antes 19.632) y **113,35** g/ave/día
      (antes 109,81). La 25 sigue parcial por el corte de fase, como debe ser
- [x] Q7 Backend detenido, puerto 5499 libre; los lotes de QA borrados y el S-369 intacto (9.484/966 + 9.534/991)
- [x] Q8 No se commiteó trabajo de la otra sesión: `dotnet ef migrations add` había arrastrado al
      `ModelSnapshot` un cambio ajeno de `tipo_alimento` (100→500) — revertido, y el Designer de mi
      migración alineado al modelo de HEAD

---

# Tracker — Ciclo completo S-369: levante → cierre → liquidación → producción

**Plan:** [`fase_de_desarrollo/carga_masiva_s369ab_postura_plan.md`](fase_de_desarrollo/carga_masiva_s369ab_postura_plan.md)
**Fecha:** 2026-08-06 · **Pedido:** «cerrá el lote en la fecha que tiene, cargá producción, registrá
traslados/movimientos/ventas en los módulos que corresponda y validá que el Excel y los reportes de la
app estén alineados». Empresa **Demo excluida** a pedido: solo Agroavicola Sanmarino.

## Corrección del corte de fase (hallazgo)
- [x] F1 Cada sublote hace **168 días exactos de levante (24 semanas)** y pasa a producción al día 169.
      Verificado: el saldo del día 168 es idéntico a las aves con las que arranca su hoja de producción
      — A `2026-02-13 → 9.484/966` y B `2026-02-19 → 9.534/991`. El corte anterior (uno solo, 2026-02-19)
      le daba a A **6 días de más** que ya estaban en producción
- [x] F2 Producción también se parte por sublote: `DIARIO A` (168 días, galpones 9-10) y `DIARIO B`
      (161 días, galpones 11-13). Las columnas de peso están **corridas una posición** entre las dos hojas

## Carga
- [x] C1 4 archivos regenerados: levante A/B (168 días c/u) y producción A/B (168 y 161 días)
- [x] C2 Levante importado: **168/168** filas cada uno, 0 errores; saldos 9.484/966 y 9.534/991
- [x] C3 **Liquidación** calculada y guardada por sublote (A: mort 3,56 % · sel 0,85 % · sexaje 2,31 % ·
      retiro acum 6,72 % · B: 2,98 / 0,69 / 3,68 / 7,36)
- [x] C4 **Cierre en la fecha real**: `P-S-369A` arranca 2026-02-14 y `P-S-369B` 2026-02-20, con
      9.484/966 y 9.534/991 — exactamente el saldo de su levante
- [x] C5 Producción importada: **168/168** y **161/161** filas, 0 errores. Cierra en 9.020/810 y 8.952/813
      con 1.142.573 y 1.115.079 huevos — los cuatro números idénticos al informe
- [x] C6 **Ajuste de inventario trazable**: el consumo del informe supera a las compras de `CONSUMOS`
      en **2,8 kg** de PREPOSTURA (86.462,8 vs 86.460,0). En vez de recortar el consumo —que es el que
      produce el gr/ave— el faltante entra como un `Ingreso` con referencia `AJUSTE-CUADRE-001560`

## Módulos (el historial quedó donde corresponde)
- [x] M1 **Movimientos de Aves**: 6 movimientos en S-369A y 8 en S-369B, con número `MGA-*`, fecha, tipo,
      cantidades, estado `Completado` y motivo. `GET /api/MovimientoAves/lote/{id}` los devuelve todos
- [x] M2 **Cohortes**: el Ingreso de 196 machos dejó su cohorte en el receptor con la procedencia y
      `fecha_encaset_cohorte = 2025-08-30` (la del **origen**, no la del receptor)
- [x] M3 **Traslado de Huevos**: 0 registros — el informe fuente no trae movimientos de huevo
- [x] M4 **Inventario**: 96 ingresos (792.181,8 kg) y 1.190 consumos (764.149,7 kg); histórico unificado
      con 1.286 filas y **0 anuladas**, cuadrando con los movimientos

## Validación contra el Excel
- [x] V1 **Levante — semanal consolidado: 24 de 24 semanas IDÉNTICAS** en las 8 métricas (saldo,
      mortalidad, selección, error de sexaje, consumo kg, g/ave/día, peso corporal y uniformidad).
      Con el corte corregido ya no queda ninguna semana parcial
- [x] V2 **Producción — por sublote (24 y 23 semanas)**: mortalidad, selección, consumo kg, huevos
      totales y huevos aptos **idénticos en todas las semanas** (única excepción: los 5 huevos del
      2026-06-30, desvío propio del archivo fuente)
- [x] V3 🔴 **El saldo de aves de producción NO descuenta las ventas** → investigado y localizado:
      - La brecha aparece justo en la primera venta y al final vale **+114 en A y +224 en B**:
        exactamente el total vendido de cada uno
      - Causa: en producción una `Venta` descuenta `aves_h_actual` y deja la auditoría en
        `movimiento_aves` + una **nota en observaciones**, pero **no escribe ninguna columna numérica**
        en `seguimiento_diario_produccion` (en levante sí existe `venta_aves_cantidad`)
      - El reporte reconstruye el saldo desde las filas diarias, así que no la ve. El punto exacto es
        `fn_indicadores_produccion_postura`: `v_aves_h_act := GREATEST(0, v_aves_h_act - r_mort_h - r_sel_h)`
        — sin ventas ni traslados
      - Arrastra el g/ave/día, que divide por ese saldo (semana 48 de A: 159,74 vs 162,39)
      - **NO corregido en este turno**: esa fn es una cadena de 3 niveles compartida con el módulo de
        Indicadores de todas las empresas y merece su propio cambio con el gate multipaís completo,
        igual que se hizo con `ReporteTecnicoService`. Pendiente de OK

---

# Tracker — Alinear el saldo de aves de PRODUCCIÓN (los dos caminos)

**Fecha:** 2026-08-06 · **Pedido:** «realizá los dos y al final dejá todo alineado y codificado,
tanto en la carga masiva como en los seguimientos diarios de producción».

## Camino 1 · La venta deja su cantidad en la fila diaria
- [x] 1a `SeguimientoProduccion` + configuración: `VentaAvesHembras`, `VentaAvesMachos`, `VentaAvesMotivo`.
      Split por **sexo** (levante usa una sola columna sumada, que no sirve porque el saldo de
      producción se lleva por sexo)
- [x] 1b Migración `20260806092854_VentaAvesEnFilaDiariaProduccion`: columnas idempotentes
      (`ADD COLUMN IF NOT EXISTS`) + **backfill** desde `movimiento_aves` que solo toca filas en cero
- [x] 1c Los **dos** escritores la llenan: la carga masiva (`MigracionService.MovimientosAves`) y el
      módulo de Movimientos de Aves (`MovimientoAvesService.SeguimientoDiario`). Antes los dos
      escribían únicamente una nota de texto en `observaciones`
- [x] 1d Backfill verificado: S-369A **114 H / 63 M** en 4 días y S-369B **224 H / 67 M** en 5 días,
      con su motivo — exactamente las ventas de `movimiento_aves`

## Camino 2 · La fn descuenta ventas, retiros, traslados y selección de machos
- [x] 2a Migración `20260806093256_SaldoProduccionDescuentaVentasYTraslados`
- [x] 2b `_seg` incorpora `sel_m`, `mov_venta_*`, `mov_retiro_*` y `mov_traslado_in/out_*` — todos ya
      los exponía `fn_seguimiento_diario_produccion` (agrega `movimiento_aves` por día), así que no
      hizo falta una dependencia nueva
- [x] 2c El decremento pasa a `− mort − sel_h − venta − retiro − salidas + ingresos` para hembras y
      `− mort − sel_m − venta − retiro − salidas + ingresos` para machos
- [x] 2d 🔴 **Tercer hueco encontrado en el camino**: la fn **nunca leyó la selección de machos** —
      ni para el saldo ni para la salida (`SeleccionMachos` del reporte estaba **fijo en 0** en
      `ReporteTecnicoSemanalCalculos:410`). Eran otras **61** y **77** aves de más. Se agregó
      `seleccion_machos` a la fn (DROP + CREATE, la firma cambia), al `IndicadorProduccionSemanalBdRow`
      y al mapeo; el % de retiro de machos ahora incluye la selección, igual que el de hembras
- [x] 2e `SaldoAvesLevanteCalculos.MovimientoDia` suma `Venta` y `Retiro` — sigue siendo la
      especificación ejecutable de la fórmula, ahora para las dos fases

## QA
- [x] Q1 `dotnet build` **0/0** · `dotnet test` **1.697 verdes** (8 tests nuevos con los cierres
      reales del S-369 y el número que devolvía el bug)
- [x] Q2 **Gate multiempresa** sobre los 6 lotes de producción de la BD: **118 semanas CORREGIDAS,
      17 sin cambio, 0 REGRESIONES**
- [x] Q3 **Saldo de aves: coincide con el Excel en las 24 + 23 semanas** de los dos sublotes.
      A cierra en **9.020 H / 810 M** y B en **8.952 H / 813 M** — antes daba 9.134/871 y 9.176/890
- [x] Q4 Mortalidad, selección, consumo kg, huevos totales y huevos aptos: idénticos en todas las semanas
- [x] Q5 Diferencia que **queda y es previa**: el `gr/ave/día` de 4 semanas difiere hasta 1,08 g
      porque la fn divide por el **censo de inicio** de semana (`saldo + mort + sel`, marcado en el
      código como «desviación preservada») y el Excel divide por el saldo de cierre. No lo toqué:
      es un criterio de denominador anterior a este cambio, no una consecuencia suya
- [x] Q6 Backend detenido, puerto libre

## Alineación final del gr/ave/día al Excel
- [x] G1 El `gr/ave/día` de **producción** dividía por un censo de inicio reconstruido
      (`fin + mortalidad + selección`); el informe divide por las aves al **CIERRE** de la semana
      («No. Final de aves»), que es lo que **levante ya hacía**
- [x] G2 Helper puro `ReporteTecnicoSemanalCalculos.GrAveDia(kg, días, avesFin)` y los **4 sitios**
      que lo calculaban (levante tab y consolidado, producción tab y consolidado) pasan a compartirlo
- [x] G3 **8 tests** con los valores del informe (S-369A sem 47 → 162,83 · sem 48 → 162,39 ·
      S-369B sem 47 → 161,77) y el que devolvía antes (161,75), más bordes de días/aves en cero
- [x] G4 `dotnet build` **0/0** · `dotnet test` **1.705 verdes**
- [x] G5 Smoke multiempresa del reporte de producción: 4 bases, HTTP 200 en todas, 135 semanas con
      gr/ave calculado y **0 valores negativos**

## Cuadre final contra el Excel
- [x] **Levante: 24 de 24 semanas idénticas**
- [x] **Producción S-369B: 23 de 23 idénticas**
- [x] **Producción S-369A: 23 de 24** — la única diferencia del ciclo completo son los **5 huevos**
      del 2026-06-30, desvío del propio archivo fuente (la columna «Producción Huevos» dice 14.038 y
      su propia clasificación suma 14.043)

## Validación exhaustiva del flujo contra el Excel
- [x] V1 **N1 · día a día**: 665 días (168+168 levante, 168+161 producción) comparados campo por
      campo contra su hoja fuente — mortalidad H/M, selección H/M, error de sexaje H/M, consumo kg H/M,
      las 9 categorías de huevo, huevo total y peso del huevo. **0 diferencias**
- [x] V2 **N2 · reportes**: levante **24/24** semanas × 8 métricas · producción **23/23** (B) y
      **23/24** (A) × 7 métricas. La única celda distinta de las 71 semanas son los 5 huevos
- [x] V3 **N3 · invariantes**: los 6 saldos de fase exactos (A 10.167/1.472 → 9.484/966 → 9.020/810 ·
      B 10.291/1.521 → 9.534/991 → 8.952/813); el cierre entrega a producción exactamente las aves
      con las que arranca su hoja. Inventario de 7 ítems y 13 movimientos de aves con su historial
- [x] V4 **Origen de los 5 huevos localizado**: galpón 9, **martes 24-jun-2026** — recolección 2.549
      contra clasificación 2.554. Son dos registros independientes del mismo día en la hoja del
      galpón (columna `N` vs bloque `AQ..BI`) que `DIARIO A` arrastra por fórmula. Único día
      descuadrado del ciclo: 0 en los galpones 10/11/12/13 y 0 en los otros 828 días del galpón 9.
      El sistema cargó la **clasificación**, que es de donde el propio Excel deriva su columna Total
- [x] V5 **Segunda desviación del fuente documentada**: las hojas «general» suman **por número de
      fila, no por fecha** (161 filas cuadran por fila y solo 1 por fecha). Por eso el desvío del
      24-jun aparecía rotulado 30-jun, y por eso la carga se hizo por sublote
- [x] V6 `VALIDACION_S-369.md` publicado junto a los archivos, con el flujo en orden, los 3 niveles
      de validación y las 2 desviaciones del fuente

---

# Tracker — Consolidado de sublotes y paridad de reportes por fase

**Fecha:** 2026-08-06 · **Pedido:** «un lote padre puede tener varios sublotes con fechas de llegada
distintas; al unirlos el consolidado debe cuadrar. Validá en reportes y descargas qué falta por fase».

## El consolidado cuadra
- [x] K1 **Consolidado = suma de los tabs**, celda por celda: 240 celdas en levante y 240 en
      producción (10 campos × 24 semanas cada uno) · **0 diferencias**. La unión es por semana de
      EDAD, no por fecha, que es como la hace el informe
- [x] K2 Levante consolidado vs `Registro Semanal general`: **24/24** semanas × 8 métricas
- [x] K3 Producción consolidado vs `SEMANAL GENERAL`: **22/23** — la única celda son los 5 huevos

## Cuatro reportes de PRODUCCIÓN estaban caídos (los cuatro salieron al cargar un lote real)
- [x] R1 🔴 `POST /obtener` (diario y semanal) daba **500** — `Column 'PesoHuevo' is null`. La entidad
      declaraba `peso_huevo` no anulable y la columna sí lo es (sus hermanas `peso_h`, `peso_m`,
      `uniformidad` siempre fueron anulables). Un día sin pesaje reventaba la consulta entera.
      **Nunca había pasado porque ninguna carga anterior escribió un NULL ahí**: de 934 filas, los
      únicos 3 nulos son de esta carga
- [x] R2 🔴 `POST /obtener-tabs` daba **404** «Nullable object must have a value» por el mismo nulo
      casteado a `double`
- [x] R3 🔴 `GET /diario/{lppId}` y `GET /cuadro/{lppId}` devolvían **vacío para TODAS las empresas**:
      leían de `seguimiento_diario_levante` filtrando `tipo_seguimiento='produccion'`, donde no hay
      ni una fila (924 filas, todas de levante). La fuente canónica es `seguimiento_diario_produccion`
- [x] R4 Arreglos: entidad `PesoHuevo` → `decimal?` (alineada a la columna y a sus hermanas) con
      `?? 0` en los 5 consumidores que necesitan valor —convención que el código ya usaba con
      `if (PesoHuevo > 0)`—; las 2 llamadas de `ObtenerDatosDiariosPorLPPAsync` apuntadas a la fuente
      canónica; migración `AlinearPesoHuevoProduccionANullable` (DDL no-op donde ya es nullable, con
      `Down` que rellena nulos con 0 antes de volver a NOT NULL)
- [x] R5 Verificado después: `diario/{lppId}` **168 días**, `cuadro` **24 filas**, `obtener` diario y
      semanal **200**, `obtener-tabs` **200** con 329 diarios por galpón y 47 semanales
- [x] R6 Riesgo de regresión **nulo** en R3: la fuente anterior está vacía para todas las empresas,
      así que solo pueden pasar de «vacío» a «con datos»
- [x] R7 `dotnet build` **0/0** · `dotnet test` **1.705 verdes**

## Lo que queda documentado como pendiente
- [x] P1 **Producción no tiene diario consolidado** (`GET diario/consolidado`), levante sí. Es el
      hueco de paridad más visible con un lote padre de varios sublotes
- [x] P2 `clasificacion-huevo-comercio` responde vacío — lee de la tabla canónica, así que no es el
      mismo problema; falta confirmar por qué filtra
- [x] P3 La `curva` de levante devuelve 0 puntos (el `resumen` de levante sí trae datos)
- [x] P4 `REPORTES_POR_FASE.md` publicado junto a los archivos, con el inventario endpoint por
      endpoint, las descargas de cada fase y los 3 pendientes

## Cierre de P1-P3 + el bug de dirección del traslado (deja el ciclo listo para desplegar)
- [x] C1 **P1 cerrado** — `GET /api/ReporteTecnicoProduccion/diario/consolidado?lotePosturaBaseId=`.
      La consolidación ya existía (`POST obtener` → `ConsolidarDatosDiarios`); solo faltaba la ruta
      GET de paridad con levante. **La ruta literal va declarada ANTES de `diario/{loteId}`**, si no
      el binder intenta parsear «consolidado» como `int` y devuelve 400
- [x] C2 **P2 cerrado** — `clasificacion-huevo-comercio` era el **tercer** sitio del bug de R3: leía
      de `seguimiento_diario_levante` con `tipo_seguimiento='produccion'`. Repuntado a la fuente
      canónica: de 0 a **24 filas**
- [x] C3 **P3 cerrado** — la `curva` de levante devolvía 0 puntos porque el commit de la curva
      (`145348b`) agregó `p_sem_anio IS NULL OR (...)` a los dos espejos `.sql` **pero no generó
      migración**. La fn de producción se redesplegó después por otra migración y se llevó el guard;
      la de levante quedó en la del 28-jul, con `<weeknum> = p_sem_anio` a secas ⇒ con NULL evalúa a
      NULL y devuelve **cero filas**. Roto **en prod y en todas las empresas**
- [x] C4 Al desplegar la fn corregida apareció un segundo bug: `part` con `PARTITION BY fin_sem`.
      `fin_sem` sale del encaset de **cada** lote, así que dos sublotes del mismo lote padre con
      fechas de llegada distintas nunca comparten esa fecha ⇒ cada uno solo en su partición y todos
      con `part = 1` en vez de ~0,50 y ~0,50 (justo el caso S-369). Se particiona por la semana
      **calendario**, materializada como `sem_cal` para que el filtro y la ventana usen la misma
      expresión
- [x] C5 Migración `20260806194500_CurvaLevanteAceptaSemanaNula` (data-only, Designer clonado,
      ModelSnapshot intacto, `DROP FUNCTION IF EXISTS` + `CREATE OR REPLACE`)
- [x] C6 **Gate multipaís** de la fn: versión previa desplegada en paralelo con otro nombre y
      comparada fila a fila en el modo de UNA semana, **todas las empresas × las 53 semanas**:
      39 filas, **0 diferencias** (0 solo-en-nuevo, 0 solo-en-viejo). Curva: **0 → 39 filas / 8
      lotes**. `part` suma 1 en cada semana calendario con saldo positivo
- [x] C7 🔴 **El traslado entre sublotes movía las aves de un solo lado.** La `Salida` de A y el
      `Ingreso` de B escribían filas **idénticas** en `movimiento_aves` (`Traslado`, origen=A,
      destino=B), así que la idempotencia del segundo encontraba la del primero, lo daba por
      duplicado y **lo omitía sin acreditar las aves**: B cerraba en 795 machos en vez de 991 y el
      importador igual decía «Procesado». Hasta ahora se esquivaba disfrazando el débito de `Venta`
- [x] C8 Arreglo: cada fila lleva su marca de dirección en `descripcion` (`Carga masiva:
      SALIDA/INGRESO/VENTA` — verificado que la columna estaba 100% NULL en las 27 filas existentes)
      y la clasificación sale de `MigracionMovimientosAvesCalculos.LadoDelMovimiento`, que **cae al
      heurístico histórico cuando la fila no tiene marca** ⇒ los datos viejos conservan su
      comportamiento. 8 tests nuevos
- [x] C9 Verificado en caliente sobre BD limpia: A 1162→**966** y B 795→**991**; al reimportar los
      dos, **0 procesadas / 1 omitida** cada uno y los saldos quietos (idempotencia intacta). Los
      archivos de carga vuelven al modelado correcto `Salida`+`Ingreso`
- [x] C10 Revalidación completa tras todos los cambios: **665 días comparados campo a campo, 0
      diferencias**; consolidado **480 celdas, 0 diferencias**; los **19 endpoints** de las dos fases
      responden 200 con datos. Único desvío contra el Excel: los 5 huevos del galpón 9 del 24-jun,
      descuadre del propio informe
- [x] C11 `dotnet build` **0 errores** · `dotnet test` **1.715 verdes**
- [ ] C12 Pendiente ajeno: `A374A` y `LOTE 235A` tienen **saldo de hembras negativo**, lo que deja
      sin `part` a las semanas donde son el único lote. Preexistente, fuera del alcance de esta tarea

---

# Tracker — Tickets como CASOS tipo Jira: tareas, tablero, tiempos y solicitante delegado

**Plan:** [`fase_de_desarrollo/18_tickets_jira_casos_tareas_tablero_plan.md`](fase_de_desarrollo/18_tickets_jira_casos_tareas_tablero_plan.md)
**Fecha:** 2026-08-06

Pedido: (1) poder indicar de qué usuario del sistema viene una solicitud; (2) módulo tipo Jira sobre
los tickets — casos con tareas/historias, tablero con drag & drop, tiempos y fases de desarrollo,
solo para `tickets.admin`; (3) *Mis solicitudes* profesional con línea de tiempo por caso.
Decisiones del usuario: fases = **ampliar estados del caso Y tablero de tareas**; "a nombre de" =
**solo el admin global**; entrega = **todo de una**.

## BD (1 migración EF idempotente)
- [x] B1 `tickets`: `solicitante_user_guid`, `solicitante_user_id`, `prioridad`, `orden_tablero`, `horas_estimadas`, `fecha_limite`, `fecha_inicio_plan`, `fecha_fin_plan`
- [x] B2 `ticket_notas.tipo_evento` (NULL = comentario humano ⇒ notas existentes intactas)
- [x] B3 Tabla `ticket_tareas` + `ticket_tiempos` + índices (`IF NOT EXISTS`)
- [x] B4 Seed de menú `tickets.tablero` y `tickets.roadmap` gated por `tickets.admin` (por `route`, no por id)

## Backend
- [x] D1 `TicketEstados`: + `EN_DOCUMENTACION` / `EN_REVISION` y transiciones ampliadas sin quitar ninguna previa
- [x] D2 `TicketPrioridades` + entidades `TicketTarea` / `TicketTiempo` + configurations + DbSets
- [x] D3 `Ticket`: campos de gestión y solicitante delegado
- [x] A1 Cálculo puro: `TicketMetricasCalculos`, `TicketTimelineCalculos`, `TicketTareaCalculos`
- [x] A2 DTOs nuevos + extensión compatible de los existentes (parámetros al final con default)
- [x] I1 `TicketTareaService` (partial + `Funciones/`) + DI
- [x] I2 `TicketService`: solicitante delegado (create, visibilidad, `EsCreador`, correos), gestión del caso (prioridad/planificación/asignado/mover), tablero, roadmap, timeline, métricas
- [x] C1 `TicketTareasController` + endpoints nuevos en `TicketsController` (ninguna ruta con `admin` — WAF)

## Frontend
- [x] F1 Modelos + servicios (tareas, tiempos, tablero, timeline)
- [x] F2 `pages/tablero` — kanban CDK con drag & drop, filtros y tarjeta rica
- [x] F3 `pages/roadmap` — timeline/gantt tipo el screenshot de Jira
- [x] F4 Componentes: `ticket-timeline`, `prioridad-badge`, `sla-chip`, `tarea-card`, `tarea-modal`, `worklog-panel`
- [x] F5 Rediseño `mis-tickets` (tarjetas pro + línea de tiempo + resumen por estado)
- [x] F6 Rediseño `ticket-detalle` (layout Jira: principal + sidebar de detalles)
- [x] F7 `ticket-create`: selector de solicitante solo con `tickets.admin`
- [x] F8 Rutas + menú + gating por permiso

## Tests y validación
- [x] T1 xUnit: no-regresión de transiciones + nuevas fases
- [x] T2 xUnit: métricas/SLA, timeline, reordenamiento kanban, código de tarea
- [x] V1 `dotnet build` 0 errores · `dotnet test` verde
- [x] V2 `dotnet ef database update` en la BD local (:5433) sin error
- [x] V3 `cd frontend && yarn build` 0 errores
- [x] V4 Smoke: crear a nombre de otro usuario, caso viejo abre bien, drag & drop persiste, worklog suma
- [x] V5 Sin procesos huérfanos + commit acotado

---

## Base de aves de los lotes poblados por TRASLADO (saldos negativos y saldos al doble)
Plan: este bloque. Motivo: el usuario aclara que **un lote sin aves encasetadas es legítimo** —hay
lotes que reciben aves de otros lotes—, así que forzar `hembras_l > 0` sería incorrecto. El bug está
en cómo el reporte resuelve la base de esos lotes.

- [x] T1 Reproducido el mecanismo exacto contra la BD. El filtro `reg_ok` **descarta las filas de
      puro traslado más allá de la semana 25** —que son justamente las que traen las aves— y el
      fallback de base lee de `reg` (sin filtrar) **una sola fila** (`LIMIT 1`) sacando de ahí LOS
      DOS SEXOS
- [x] T2 🔴 **Defecto 1 — saldo NEGATIVO.** Lote 116 (A374A, Sanmarino) recibió 1.010 machos el
      08-jun y 7.617 hembras el 11-jun, en filas distintas. El `LIMIT 1` tomó la de machos ⇒
      `base_h = 0` ⇒ el reporte le restaba igual 122 de mortalidad y 90 de error de sexaje ⇒ **−212
      durante 14 semanas**, mientras el maestro decía 7.405
- [x] T3 🔴 **Defecto 2 — saldo AL DOBLE.** Cuando el traslado cae DENTRO de la ventana de 25
      semanas, la fila la suma la acumulación *y además* se usa como base ⇒ las aves cuentan dos
      veces. Lote 124: 5.100 hembras reportadas como **10.200**. Igual en 128 (29.475 vs 19.475) y
      129 (9.000 vs 6.000)
- [x] T4 El mismo fallback estaba replicado en **las tres** fns de levante:
      `fn_resumen_semanal_ra_pesadas_levante`, `fn_reporte_semanal_levante_extras`,
      `fn_indicadores_levante_postura`
- [x] T5 Arreglo idéntico en las tres: la base por traslado pasa a ser la **SUMA POR SEXO** de los
      ingresos de las filas que la ventana **descarta**. Sigue siendo `COALESCE`, **no** suma: un
      lote con encaset propio conserva su número exacto y el fallback solo entra con encaset 0/NULL
- [x] T6 **Gate multipaís** (las 3 versiones previas desplegadas en paralelo con sufijo `_V0` y
      comparadas fila a fila, todas las empresas): cambian **únicamente los mismos 4 lotes** en las
      3 fns; 0 filas de diferencia en todo el resto. S-369 (142/143) queda byte a byte idéntico
- [x] T7 **Contraste contra el testigo independiente**: los 4 lotes pasan a coincidir EXACTO con
      `lote_postura_levante.aves_h_actual/aves_m_actual` (116→7.405/738 · 124→4.870 · 128→19.385 ·
      129→6.000). Antes ninguno coincidía
- [x] T8 Migración `20260806211500_BaseAvesPorTrasladoEnLevante` con las 3 fns (data-only, Designer
      clonado, ModelSnapshot intacto, `DROP FUNCTION IF EXISTS` + `CREATE OR REPLACE`)
- [x] T9 `dotnet build` 0 errores · `dotnet test` **1.834 verdes** · migración aplicada en local
- [x] T10 Reconstruir S-369 desde los 4 archivos (mi test del traslado dejó la BD local sucia: la
      limpieza borró `movimiento_aves` y los contadores del maestro pero **no las columnas de
      traslado de la fila diaria**, así que B quedó con `traslado_ingreso_machos = 392` = 196×2 y A
      con la venta vieja del workaround Y la salida nueva a la vez). **No es un bug del código** —
      lo confirma que la fila de A tiene 196 y no 392— pero había que rehacer el ciclo para validar
      que los archivos producen el resultado correcto de punta a punta. **Rehecho desde cero**: A
      queda con salida 196 y SIN la venta espuria, B con ingreso 196 (no 392), y B cierra en **991
      machos** — el fix del traslado probado de punta a punta con el modelado correcto
- [x] T11 Revalidación completa tras la reconstrucción: **665 días campo a campo, 0 diferencias** ·
      consolidado levante **24/24** y producción **22/23** (los 5 huevos conocidos del informe) ·
      semanal de levante **0 diferencias** · producción 168 y 161 días con 1.142.573 y 1.115.079
      huevos, exactamente lo que predicen los archivos
- [x] T12 **7 de 8 lotes de todas las empresas coinciden EXACTO con el maestro** y no queda ningún
      saldo negativo salvo el del lote 123, que es dato genuinamente sobregirado (X1)

### Lo que este bloque NO arregla (dos hallazgos separados, ambos previos y sin tocar)
- [x] X1 🔴 **El seguimiento diario acepta bajas mayores al saldo.** Caso probado: lote 123 (Demo)
      tenía base 5.303, una salida de 5.100 el 06-jul y ~85 aves vivas; el **03-ago alguien cargó
      500 muertes**. El reporte muestra −460 (es honesto) y el maestro lo tapa con el clamp
      mostrando 0. El único control existente es REQ-011b
      (`SeguimientoLoteLevanteService.Crud.cs:357`), que su propio doc-comment declara *«soft-check,
      NO bloqueo duro»*: solo escribe `LogWarning`, va envuelto en un `try/catch` que se traga todo,
      y compara `saldo == 0` exacto ⇒ con saldo negativo o con 5 aves y 100 de mortalidad **no
      dispara**. Convertirlo en bloqueo rechaza escrituras que hoy pasan ⇒ decisión del usuario
- [x] X2 **RESUELTO — el saldo de levante no descontaba las VENTAS.** Con la BD limpia el desvío
      quedó aislado y exacto: S-369B daba **1.281 machos** contra **991** del maestro, y la
      diferencia eran **290 = las dos ventas** (150 el 09-feb + 140 el 17-feb). Era además una
      violación de «una sola fórmula por número»: el camino C# (`ReporteTecnicoService` sobre
      `SaldoAvesLevanteCalculos`) SÍ las descuenta y coincide con el informe; las dos fns SQL no
  - [x] X2.1 La fila diaria de levante solo tenía `venta_aves_cantidad` (TOTAL, sin sexo) mientras
        el saldo va POR SEXO ⇒ se replica lo que producción ya tenía: `venta_aves_hembras/machos`
        en entidad, configuration y BD, con backfill idempotente desde `movimiento_aves` (el dueño
        del número). El backfill encontró exactamente las 2 ventas y las repartió bien
  - [x] X2.2 Los **cuatro** puntos de escritura pueblan el split: carga masiva
        (`MigracionService.MovimientosAves`), alta por UI, cancelación y edición
        (`MovimientoAvesService.SeguimientoDiario`)
  - [x] X2.3 Las dos fns restan la venta del saldo, y las filas de puro traslado pasadas de la
        semana 25 ya no se descartan si traen venta (una fila con venta es una fila con dato) ni se
        usan como base
  - [x] X2.4 Migración `20260806235000_VentaAvesEnFilaDiariaLevante`. ⚠️ Designer clonado y
        **ModelSnapshot intacto pese a agregar 2 propiedades de entidad**: regenerarlo con
        `migrations add` arrastraría los cambios EN VUELO de la otra sesión que trabaja Tickets en
        este repo. El DDL es idempotente, así que el desfase no tiene consecuencias
  - [x] X2.5 **Gate**: de 39 filas del resumen y 137 del Detalle en todas las empresas cambia **un
        solo lote** (S-369B, el único con ventas en levante). Las 3 fuentes convergen en **991** y
        **los 8 lotes de todas las empresas cuadran con el maestro**, salvo el 123 (X1), donde el
        reporte es el honesto y el maestro miente por el clamp
  - [x] X2.6 Revalidación completa: **665 días, 0 diferencias** · semanal levante 24/24 ·
        consolidado **480 celdas, 0 diferencias** · **19/19 endpoints** con datos ·
        `dotnet test` **1.834 verdes**
  - [x] X2.7 Al aplicar la migración por EF se aplicó también `20260806235814_AddTicketsJiraCasosTareas`,
        de la otra sesión, sobre la BD local compartida. No es destructivo (crea sus tablas) pero
        queda anotado: no era mía

### Evidencia de la validación (2026-08-06)
- `dotnet build` **0 errores / 0 advertencias** · `dotnet test` **1.834 verdes** (1.715 previos + 119 nuevos)
- `yarn build` (Node portable 22.23.1) **0 errores**; único warning el de *bundle budget* preexistente
- Migración `20260806235814_AddTicketsJiraCasosTareas` aplicada en la BD local (:5433) y **verificada en
  caliente**: 8 columnas en `tickets`, `ticket_notas.tipo_evento`, tablas `ticket_tareas`/`ticket_tiempos`
  con sus índices, fila en `__EFMigrationsHistory` y los 2 menús nuevos con su `menu_permissions`
- **Smoke funcional end-to-end (backend :5501, JWT + X-Secret-Up minteados): 44 verificaciones, 0 fallas**
  - Gate de "a nombre de": un gestor sin `tickets.admin` recibe 400; el admin crea el caso y el
    solicitante queda en la usuaria delegada, con nota de sistema `SISTEMA_SOLICITANTE`
  - La usuaria delegada **ve el caso en «Mis solicitudes»** y para ella `soySolicitante = true`;
    el admin que lo registró **sí puede gestionarlo** (`Tomar` OK)
  - Fases nuevas: mover a `EN_DOCUMENTACION` y `EN_REVISION` OK; **arrastrar a `CERRADO` rechazado**
    (el cierre lo confirma el solicitante)
  - Tareas: código correlativo `-T1`/`-T2`, subtarea anidada, mover a `LISTO` sella `fecha_fin_real`
    y el `orden` de cada columna queda 0..n-1 sin huecos
  - Tiempos: 2,5 h + 1 h = 3,5 h, desvío −4,5 h contra la estimación de 8 h, y 40 h en un registro rechazado
  - Línea de tiempo: 18 eventos ordenados (CREADO/SISTEMA/APERTURA/ESTADO/TAREA/TIEMPO), visible también
    para la solicitante; tablero con 7 columnas y roadmap con las 3 tareas del caso
  - Buscador de solicitantes: 3 resultados para el admin, **vacío fail-closed** sin `tickets.admin`
  - **No-regresión**: los 14 casos preexistentes listan, abren, salen con `prioridad=MEDIA`,
    `estadoSla=SIN_SLA` y con su línea de tiempo derivada
- Dato de prueba borrado de la BD local (caso 15 + sus 3 tareas, 2 tiempos y 10 notas) y backend del
  smoke detenido (:5501 libre; el :5002 del usuario quedó intacto)

### Verificación visual en el navegador (2026-08-07, front :4200 + back :5002)
Sesión inyectada en `localStorage` (admin y luego la usuaria delegada). **Cero errores de consola**,
todas las llamadas `/api/tickets/*` en 200.
- **Tablero**: las 7 columnas pobladas (3/1/1/2/1/4/6), resumen «19 casos · 3 sin arrancar · 5 en curso
  · 1 vencidos · 13 h registradas»; 18 tarjetas `cdk-drag` y las 7 listas conectadas por sus ids
- **Roadmap**: eje semanal 20-jul → 24-ago, barras posicionadas por %, marcador de HOY, leyenda de
  prioridad; al desplegar un caso aparecen sus 4 tareas anidadas con su estado
- **Mis solicitudes**: tarjetas con código/tipo/prioridad/SLA («Vencido · 3 h», «En tiempo · 9 d») y
  barra de avance; «Ver seguimiento» despliega la línea de tiempo (13 eventos con autor y fecha)
- **Detalle**: banda «Solicitud de KARINA … · registrada por Jose Moises», sidebar con solicitante +
  registrado por + planificado + compromiso, métricas (5,5 h de 12 · avance 25 %), pestañas
  Actividad/Comentarios/Tareas/Tiempos, panel de tareas en lista y en tablero, worklog con 46 % de la
  estimación y −6,5 h de desvío
- **Formulario nuevo**: el bloque «Registrar a nombre de otro usuario» solo para el admin, con
  búsqueda en vivo (3 resultados para «karina») y selector de prioridad
- **Vista del SOLICITANTE** (Karina): ve sus 2 casos marcados «registrado por soporte», abre el
  detalle y el seguimiento (18 eventos), y **NO** ve el panel de gestión, ni la pestaña Tiempos, ni
  el botón de crear tareas
- 🔧 **Corregido en el pase**: la línea de tiempo mostraba el alta de cada tarea **dos veces** (la nota
  de sistema + el evento derivado). Se quitó la nota de sistema al crear (el evento ya se deriva de la
  fila); mover una tarea sí sigue dejando su nota. Verificado: crear = 1 evento, mover = 1 evento
- ⚠️ **No se pudo capturar pantalla ni arrastrar con el mouse**: el panel del navegador no estaba
  desplegado, así que la página no compone frames (`screenshot` y `left_click_drag` quedan
  bloqueados). Todo lo anterior se verificó por DOM, red y consola

### 🔴 Hueco de despliegue detectado y cerrado (2026-08-07)
Al revisar si la entrega quedaba lista para producción apareció que **crear el menú no alcanza para
que se vea**: `RoleCompositeService.Menus_GetForUserAsync` arma el árbol desde `role_menus` y solo cae
al filtro por permisos cuando el rol no tiene NINGÚN menú asignado. La migración anterior sembraba
`menus` + `menu_permissions`, así que en local `tickets.tablero` y `tickets.roadmap` figuraban con
**0 roles** ⇒ en prod habrían quedado invisibles para todos (y no asignables en la UI de roles hasta
tener fila en `company_menus`).

- [x] Migración data-only `20260807030500_SeedMenusTableroRoadmapEnRolesYEmpresas` (Designer clonado,
      **ModelSnapshot intacto**, idempotente con `WHERE NOT EXISTS`): copia los dos menús nuevos a los
      roles y empresas que YA tienen `tickets.admin` o `tickets.gestion`. No habilita nada a nadie
      nuevo: el gate sigue siendo `menu_permissions`
- [x] Verificado en local: de **0 → 6 roles y 2 empresas** en cada menú nuevo; reaplicar el SQL inserta
      **0 filas** (idempotente); `GET /api/roles/menus/me` del admin devuelve las 5 entradas del grupo
      Tickets, con «Tablero de casos» y «Roadmap» incluidas
- [x] ⚠️ **Gotcha de sesiones paralelas**: `dotnet ef migrations add` capturó cambios de OTRA sesión
      (`venta_aves_hembras`/`venta_aves_machos` en `seguimiento_diario_levante`, que tienen entidad pero
      todavía no migración). Se descartó esa migración generada y se restauró el ModelSnapshot; la
      data-only se escribió a mano con el Designer clonado
- [x] `dotnet build` 0 errores · `dotnet test` **1.834 verdes**

---

## Tab «Indicadores» de Levante y Producción — guía genética + UX unificada
Plan: [19_indicadores_levante_produccion_ux_plan.md](fase_de_desarrollo/19_indicadores_levante_produccion_ux_plan.md)

### Validación contra la guía genética (con el lote S-369 real, guía AP 2026)
- [x] I1 **Levante: 24/24 exactas** en las 4 columnas de guía (`consumoTablaHembras`,
      `pesoTablaHembras`, `mortTablaHembras`, `unifTabla`). Sin hallazgos
- [x] I2 **Producción: correctas 24/24** en `porcentajeProduccionGuia`, `consumoGuia H/M`,
      `mortalidadGuia H/M`, `huevosTotalesGuia`, `huevosIncubablesGuia`, `pesoHuevoGuia`,
      `retiroAcumulado*Guia` y `pesoGuia H/M` (la fn divide /1000: la guía guarda gramos)
- [x] I3 Dos falsos positivos documentados para que nadie los «arregle»: la semana 25 tiene DOS
      filas en la guía (`25` levante y `25P` producción) y la fn usa bien la `25P`; y el % de
      producción usa aves vivas corrientes, no el promedio inicio/fin
- [x] I4 🔴 **`uniformidadGuia` = 0 en las 24 semanas** — arreglado en la capa de presentación (ver I5).
      Diagnóstico: La guía no trae uniformidad para edades de
      producción (solo 25 de 98 filas la tienen, todas de levante); la fn la lee bien como NULL y
      después la pisa con `g_unif := COALESCE(g_unif, 0)`. Se lee como «la guía exige 0 %» en vez de
      «sin dato». Igual con `g_peso_h/m`. El COALESCE es deliberado (parity con un `ParseDouble`
      viejo) ⇒ el arreglo va explícito y medido
- [x] I5 ⚠️ **NO se tocó la fn: el espejo `.sql` está desincronizado de producción.** Al intentar el
      arreglo en `fn_indicadores_produccion_postura` descubrí que
      `backend/sql/fn_indicadores_produccion_postura.sql` **no coincide con lo desplegado**: le falta
      la columna `seleccion_machos`, que agregó la migración `SaldoProduccionDescuentaVentasYTraslados`
      y que el espejo nunca recibió. Lo desplegué en local y dejó la fn en **68 columnas en vez de
      69** ⇒ habría roto `IndicadorProduccionSemanalBdRow.SeleccionMachos` en runtime. Detectado por
      el gate y restaurado desde la definición viva. **Reconciliar el espejo queda como tarea aparte
      con su propio gate**; meterlo en un cambio de UX era arrastrar riesgo. El síntoma se arregló
      donde es seguro: `hayGuiaUniformidad()` trata el 0 como ausencia y la UI pinta «—» (verificado
      en el navegador: las 5 primeras semanas muestran «—»)

### UX — cada tab tiene la mitad de lo bueno
- [x] I6 Levante tiene chips de contexto, modal de Fórmulas y resumen acumulado; **le faltan**
      estados de carga/error y la leyenda de desvío
- [x] I7 Producción tiene carga/error/leyenda; **le faltan** chips, Fórmulas y resumen acumulado, y
      arrastra `style=` inline en el encabezado
- [x] I8 `frontend/src/styles/indicadores-tab.scss` (registrado en `styles.scss`) con los bloques comunes, tokens del sistema de diseño
      (prohibido hardcodear color)
- [x] I9 Sin cambio: la cabecera de producción **ya decía «%Prod Real»**. La columna «Eficiencia» que
      vi al principio es de la tabla de LEVANTE, que es otra métrica

### Quitar el tab «Reporte semanal»
- [x] I10 Solo **levante** lo tenía («🗓️ Reporte semana»); producción no. Eliminar marcado, rama
      `@if`, el estado `reporteSemana`, `buildReporteSemanaFilas`, `exportReporteSemanaExcel`, la
      interfaz `ReporteSemanaFila` y el SCSS huérfano
- [x] I11 `ng build` **correcto** (único warning: el de bundle budget preexistente que el repo acepta)
- [x] I12 **Verificado en el navegador** con el lote S-369 real (front :4300, back :5002 con
      `AllowedOrigins__1` por variable de entorno, sesión inyectada en `localStorage`):
      · Levante: 3 tabs sin «Reporte semana», encabezado + 4 chips + leyenda nueva + 24 filas +
        resumen acumulado, **0 clases viejas**
      · Producción: encabezado + 4 chips + leyenda + 23 filas, **0 estilos inline**, `loading-state`
        y `error-state` reemplazados, y **`Unif Guía` mostrando «—»** en vez de 0
      · Colores resueltos desde los tokens: naranja acción, verde solo éxito (#16A34A), rojo solo
        peligro (#DC2626)

### Layout: aprovechar el ancho en monitor (2026-08-07)
Feedback del usuario sobre las capturas: *«tiene mucho espacio alrededor y tengo que bajar»*, *«el chat
está abajo cuando puede estar a un lado»*, *«todo es hacia abajo cuando tenemos espacio en los lados»*.

- [x] **Nuevo ticket**: contenedor `max-w-5xl` → `max-w-[1500px]` y el formulario pasa a **dos columnas**
      en `lg+` (izquierda: título, tipo, resolutor, descripción, prioridad · derecha: notificados,
      imágenes, adjuntos), con «a nombre de» a lo ancho arriba. Alto del form 825 px contra el scroll
      largo de antes
- [x] **Detalle**: contenedor a `max-w-[1700px]` y **tres columnas** en `xl` — caso (629 px) ·
      conversación (499 px) · gestión (369 px). El chat **deja de ser pestaña**: vive en su columna,
      con los mensajes scrolleando dentro y el redactor fijo abajo. Conversación y gestión son
      `sticky` (con `self-start`, que es lo que les da margen para desplazarse), así que el caso
      scrollea sin que se vayan de pantalla. En `lg` baja a dos columnas (caso + gestión, chat debajo)
      y en móvil a una
- [x] 🔴 **Bug de layout encontrado y corregido**: el detalle sacaba **scroll horizontal a toda la
      página** en pantallas medianas. Era un *grid blowout* — el ancho mínimo del contenido (el stepper
      de 7 fases con `whitespace-nowrap`) estiraba la pista del grid a 996 px dentro de un contenedor
      de 705. Fix: `min-w-0` en las columnas del grid + el stepper scrollea dentro de su propia caja
      (`overflow-x-auto` con `w-max min-w-full`) y entre `md` y `lg` solo renderiza la etiqueta de la
      fase actual, para no reservar el ancho de las otras seis
- [x] Verificado en 1600 / 768 / 375 px: **cero desborde horizontal** en los tres, y el stepper de 7
      fases entra completo. `yarn build` 0 errores (solo el warning de bundle budget preexistente)

### Panel de control del administrador + reporte a Excel (2026-08-07)
Pedido: *«quiero filtros y datos arriba — efectividad, cantidad de casos, tareas terminadas con las
pendientes, promedio de respuesta, estado de ticket… control por país… y descargar un reporte que
muestre países, ticket, tiempos de implementación, planificación, bien detallado en Excel»* y, al
revisarlo, *«también necesito filtrar por empresa»*.

- [x] `TicketIndicadoresCalculos` (puro): resumen (volumen, efectividad, % resueltos, tareas
      terminadas/pendientes, promedios de primera respuesta / resolución / confirmación de cierre,
      vencidos y por vencer, sin responsable, horas) + desgloses por **país**, **empresa**, estado,
      tipo, prioridad y responsable. Los promedios **ignoran** las filas sin el dato en vez de
      contarlas como cero, y la efectividad solo mide los casos que tenían compromiso
- [x] Filtros ampliados y COMPARTIDOS por tablero, roadmap, panel y reporte (un solo
      `TicketTableroFiltro`, armado en un helper del controller para que no se desincronicen):
      **multi-país**, **multi-empresa**, rango de fechas, estado, tipo, prioridad, semáforo de SLA,
      responsable y búsqueda libre. El filtro de SLA se traduce a condiciones sobre `fecha_limite`
      para que lo resuelva la BD y no el backend en memoria
- [x] `GET /api/tickets/indicadores` y `GET /api/tickets/reporte` (ninguna ruta con `admin` — WAF)
- [x] Página `pages/panel` (`/tickets/panel`): 6 KPIs arriba, alertas de vencidos / por vencer / sin
      responsable, y desgloses por país, empresa, estado, tipo, prioridad y responsable
- [x] **Descarga a Excel** con el helper compartido `exportarMultiHojaExcel` (no `XLSX` inline):
      6 hojas — Indicadores · Por país · Por empresa · Casos · Tareas · Tiempos —, cada una con los
      filtros aplicados en el encabezado. La hoja Casos trae 29 columnas: país, empresa, solicitante,
      registrado por, responsable, fechas, SLA, tiempos de primera respuesta y resolución,
      planificación, estimadas/registradas/desvío y avance de tareas
- [x] Migración data-only `20260807062000_SeedMenuPanelIndicadoresTickets` (Designer clonado,
      ModelSnapshot intacto, idempotente): menú + `menu_permissions` + `role_menus` + `company_menus`.
      Verificado: **6 roles y 2 empresas**
- [x] `dotnet build` 0 errores · `dotnet test` **1.864 verdes** (30 nuevos de indicadores) ·
      `yarn build` 0 errores
- [x] **Smoke API: 24 + 11 verificaciones, 0 fallas** — efectividad 0/4, tareas 2 listas / 7
      pendientes, promedios 14,26 h y 169,51 h, desgloses por los 6 cortes; multi-país y
      multi-empresa suman exacto y se combinan entre sí; SLA=VENCIDO coincide con el resumen; el
      tablero, el roadmap y el reporte respetan el mismo filtro
- [x] **Smoke UI**: chips de país y empresa filtran en vivo (19 → 13 casos con ItalcolEcuador, y la
      tabla queda con esa sola empresa); el `.xlsx` descargado trae las 6 hojas y dice
      «Empresas: ItalcolEcuador» en el encabezado. Cero desborde horizontal

---

# Tracker — Reconciliar el espejo `.sql` de `fn_indicadores_produccion_postura` + `uniformidad_guia` NULL

**Plan:** [`fase_de_desarrollo/reconciliacion_espejo_fn_indicadores_produccion_plan.md`](fase_de_desarrollo/reconciliacion_espejo_fn_indicadores_produccion_plan.md)
**Fecha:** 2026-08-07 · Continúa el handoff de postura (§2.1 «bomba de tiempo» + §2.2)
**Bloque propio — no tocar desde otras sesiones** (hay una sesión de Tickets con trabajo abierto)

## Fase 0 — Auditoría
- [x] A1 Migración vigente = `20260806093256`; su constante `FnConSaldoCorregido` vs la definición
      **viva** (`pg_get_functiondef`, normalizada): **0 diferencias** ⇒ lo desplegado es lo que
      despliega la migración
- [x] A2 Diff normalizado espejo vs viva: 220 líneas = **exactamente los 9 deltas** de esa migración
      + el formato de `pg_get_functiondef`. **Ninguna divergencia oculta**
- [x] A3 Ningún otro `.sql` redefine la fn (los otros 6 que la nombran son comentarios o scripts de
      verificación)
- [x] A4 Cadena de `uniformidad_guia` auditada punta a punta y **toda nullable**: `BdRow` `double?`
      → `Dec(double?)` → DTO `decimal?` → front `number | null`, `hayGuiaUniformidad()` ya trata
      null/undefined/0 como ausencia, `redondearFila()` deja pasar null
- [x] A5 Plan escrito
- [x] A6 🔎 **Corrección al handoff**: el «CRLF inflado (`
`)» era **artefacto del volcado**
      (psql.exe en Windows duplica los CR al escribir por pipe). Medido dentro de la BD el cuerpo
      tiene **1.964 CR y 1.964 LF** — balanceado. Lo inflado son las **líneas en blanco**
      (1.965 líneas para 457 útiles, ~3 blancos antes y después de cada línea real)

## Fase 1 — Espejo reconciliado (sin cambio de comportamiento)
- [x] E1 `RETURNS TABLE` + `seleccion_machos`
- [x] E2 `DECLARE`: `v_cum_sel_m` + `r_sel_m` + venta/retiro/traslado H y M
- [x] E3 CTE `_seg` rama LPP y rama lote: `sel_m` + 8 columnas `mov_*`
- [x] E4 Agregación semanal: 9 `SUM` + 9 destinos del `INTO`
- [x] E5 Acumulado `v_cum_sel_m`, `retiro_sem_m`/`retiro_ac_m`/`r_aves_m_inicio` con selección de machos
- [x] E6 Decremento del saldo con ventas/retiros/traslados
- [x] E7 `seleccion_machos := r_sel_m;` en la emisión
- [x] E8 Comentario **obsoleto corregido**: la versión desplegada aún dice «Machos sin selección en
      esta fn (… solo resta mort_m)», que su propio cambio volvió falso
- [x] E9 CHANGELOG + regla en la cabecera del `.sql` («este archivo es el ESPEJO; si lo cambiás va
      con su migración y su gate; nunca `psql -f` sin verificar que está al día»)

## Fase 2 — `uniformidad_guia` NULL (único cambio de comportamiento)
- [x] U1 `g_unif := COALESCE(g_unif, 0);` eliminado, con el porqué documentado y la aclaración de
      que `g_cons_*`/`g_mort_*`/`g_peso_*`/`g_retiro_ac_*` conservan el 0 a propósito
- [x] U2 Migración `20260807140000_UniformidadGuiaProduccionNull` — `CREATE OR REPLACE` (la firma NO
      cambia), `Down()` = espejo + COALESCE restaurado, Designer clonado, **ModelSnapshot intacto**
- [x] U3 Comentario del front actualizado; el guard contra 0 **se conserva** (cubre backends sin la
      migración y un 0 genuino)

## Fase 3 — Gate de fn compartida (§5 del handoff)
- [x] G1 Universo: **5 empresas × 8 LPP** (flujo LPP, ventana completa **y** semanas 30-40) +
      **5 × 6 lotes** (flujo legacy) = 70 llamadas por versión ⇒ **179 filas**.
      ⚠️ Gotcha: la fn hace `CREATE TEMP TABLE _seg` **sin dropearla** ⇒ una sola llamada por
      transacción; el gate corre en autocommit, una sentencia por llamada (no `CROSS JOIN LATERAL`)
- [x] G2 🥇 **Prueba de fidelidad del port**: espejo reconciliado **+ COALESCE restaurado** (`_v0`)
      vs fn viva ⇒ `EXCEPT` **0 en los dos sentidos** y **0 diferencias en las 68 columnas**
- [x] G3 Aislamiento por columna (el `EXCEPT` marcaba las 179 en ambos sentidos sin decir por qué):
      **`uniformidad_guia` es la única distinta**. `diferencia_uniformidad` **0 diffs** — se cumple la
      predicción de que `fn_dif_pct` ya devolvía NULL con guía = 0.
      ⚠️ Gotcha: `JOIN … USING (lpp, lote)` da **0 filas** porque esas claves traen NULL ⇒
      `ON n.x IS NOT DISTINCT FROM v.x`
- [x] G4 Dirección: **179/179 `0 → NULL`**, `0` valores reales perdidos, `0` NULL→valor.
      Es data-driven, no hardcode: con guías AP no hay uniformidad en edades ≥25; donde la guía sí la
      define (R308 2021, fila `25P` = 90) la fn ahora la mostraría en vez de 0
- [x] G5 La fn desplegada devuelve **69 columnas**, `seleccion_machos` en la **posición 15**, y su
      salida coincide exacto con la esperada (`EXCEPT` 0/0). Bonus: el cuerpo pasó de **1.965 a 499
      líneas** (se fue la inflación de blancos)
- [x] G6 `dotnet build` de Infrastructure **0/0** · `dotnet test` **1.864 verdes** ·
      `ng build` OK (único warning: bundle budget preexistente).
      ⚠️ El `dotnet build` de la solución falla por **MSB3021/MSB3027**: un `ZooSanMarino.API.exe`
      **ajeno** (PID 5060, otra sesión) tiene tomados los DLL. No es error de compilación y **no se
      mató el proceso ajeno**
- [x] G7 **Smoke HTTP real** (backend propio :5499, `ASPNETCORE_ENVIRONMENT=Development`, JWT +
      X-Secret-Up minteados): `POST /api/Produccion/indicadores-semanales` (LPP 7) ⇒ **HTTP 200**,
      44 semanas, **`uniformidadGuia` null en 44/44** y `diferenciaUniformidad` null en 44/44.
      ⚠️ Gotcha: el backend **NO ignora `PORT`** (el handoff dice lo contrario) —
      `Program.cs:89` hace `Configuration["PORT"] ?? "5002"` + `UseUrls`, que **gana sobre
      `ASPNETCORE_URLS`**. Se levanta con `PORT=5499`
- [x] G8 Limpieza: backend de smoke detenido (5499 libre, el **ajeno de :5002 intacto**), `_v0`/`_v1`
      y las 4 tablas `_gate_*` borradas de la BD local, migración registrada en
      `__EFMigrationsHistory`. Commit acotado, `git add` archivo por archivo, sin footer de atribución

## Aplicación en la BD local (nota de método)
- [x] La migración **no se pudo aplicar con `dotnet ef database update`**: EF necesita compilar el
      startup project (API) y ese binario lo tiene tomado el proceso ajeno. Se aplicó ejecutando el
      **SQL extraído de la propia migración** (`FnUniformidadGuiaNull`, no del espejo) y recién
      después se registró en `__EFMigrationsHistory` — con el efecto **verificado presente** (69
      columnas y salida idéntica a la esperada), que es la condición que exige CLAUDE.md. En el
      deploy la aplica EF sola, como siempre

## 🔴 Hallazgo NUEVO (fuera del alcance de este bloque, no se tocó)
- [ ] **`seleccion_machos` es un callejón sin salida**: la fn lo emite y
      `IndicadorProduccionSemanalBdRow.SeleccionMachos` lo materializa, pero
      `IndicadorProduccionSemanalDto` **no tiene el campo** y `IndicadoresProduccionCalculos` **no lo
      mapea** ⇒ el valor se calcula y se descarta; el front nunca lo ve (`grep seleccionMachos` en
      `features/lote-produccion/` = 0 resultados). Verificado por API: la respuesta no trae la clave.
      Es un cabo suelto de la misma `20260806093256`; exponerlo cambia el contrato del DTO y pide
      decidir dónde va la columna (tabla + Excel) ⇒ tarea aparte
      · **Tomado y resuelto** en el bloque «Exponer `seleccion_machos`…» del final de este archivo

---

# Exponer `seleccion_machos` en indicadores semanales de PRODUCCIÓN

**Plan:** [`fase_de_desarrollo/exponer_seleccion_machos_indicadores_produccion_plan.md`](fase_de_desarrollo/exponer_seleccion_machos_indicadores_produccion_plan.md)
**Fecha:** 2026-08-07 · Continúa el hallazgo abierto del bloque anterior. **Sin migración**: la fn ya
emite la columna, esto solo la deja llegar al front.

## Verificación previa (la aritmética ya estaba bien, no se toca)
- [x] V1 Confirmado **contra la fn desplegada en la BD local** (`pg_get_functiondef`, no el espejo
      `.sql`): la firma incluye `seleccion_machos`, el saldo hace
      `v_aves_m_act - r_mort_m - r_sel_m` y el %retiro de machos usa `(r_mort_m + r_sel_m)`
- [x] V2 `20260807140000` (la última que recrea la fn) conserva las tres cosas ⇒ no hay regresión
      pendiente de la `20260806093256`
- [x] V3 `grep "new IndicadorProduccionSemanalDto"` ⇒ **un solo sitio de construcción** (`MapRow`),
      así que insertar el campo en medio del `record` posicional es seguro (si faltara el mapeo, no
      compila por aridad)
- [x] V4 La fn **no** emite `porcentaje_seleccion_machos` (solo el de hembras) ⇒ se expone el conteo;
      el % de machos no se replica en TypeScript (una sola fórmula por número)

## Backend
- [x] B1 `IndicadorProduccionSemanalDto`: + `int SeleccionMachos` en el bloque Selección (pos. 15,
      igual que la fn y el BdRow)
- [x] B2 `IndicadoresProduccionCalculos.MapRow`: + `r.SeleccionMachos` (int→int, sin conversión)
- [x] B3 Test xUnit: `SampleRow.SeleccionMachos = 3` (valor ≠ 0 a propósito: `SeleccionHembras` es 0 y
      un mapeo faltante habría pasado como falso verde) + aserción en
      `MapRow_CopiaTodosLosCamposEnteros`

## Frontend — decisión del usuario: tabla + Excel, **solo conteo**
- [x] F1 `produccion.service.ts`: + `seleccionMachos: number` en la interfaz del DTO
- [x] F2 `tabla-lista-indicadores.component.html`: `<th>Sel M</th>` + `<td>` tras `%Sel H`
- [x] F3 `tabla-lista-indicadores.component.ts` → `buildIndicadoresRows()`: `SeleccionM` tras `PorcSelH`
- [x] F4 **Bug de layout preexistente corregido de paso**: el `colspan` del grupo «Mortalidad /
      Selección» decía **8** con **10** subcolumnas debajo (quedó viejo al agregar `Sel H`/`%Sel H`)
      ⇒ corría 2 columnas la fila de encabezados. Ahora **11**. Sin `nth-child` en el SCSS y el
      detalle usa `colspan="999"`, así que nada más dependía del número

## Gates
- [x] G1 `dotnet build` (con el SDK **10** de `~/.dotnet/dotnet.exe`; el `dotnet` del PATH es 9 y
      falla con `NETSDK1045`)
- [x] G2 `dotnet test`
- [x] G3 `yarn build` del front. ⚠️ Gotcha del worktree: **no tiene `node_modules`** ⇒ se enlazó por
      *junction* al del repo principal antes de compilar
- [x] G4 Smoke API: `POST /api/Produccion/indicadores-semanales` con `PORT=5499` ⇒ la clave
      `seleccionMachos` ahora viaja en el JSON

## Fase 2 — `%Sel M` emitido desde la fn (cierra el pendiente que dejó la fase 1)
- [x] M1 Migración `20260807180000_PorcentajeSeleccionMachosProduccion`: `DROP + CREATE` (la firma
      cambia), Down restituye la previa completa. El SQL se generó **desde el cuerpo exacto de
      `20260807140000`** con 4 inserciones puntuales, cada una con guard de ocurrencia única
- [x] M2 **Verificado byte a byte**: quitando las 6 líneas insertadas, el cuerpo nuevo es idéntico al
      previo ⇒ cambio aditivo puro, ninguna otra columna se movió
- [x] M3 Designer clonado del de `20260807140000` (misma ModelSnapshot; no toca entidades)
- [x] G5 **Gate de paridad** con la receta de [[espejo-sql-desincronizado-y-gate]]: la versión nueva
      se desplegó primero con **otro nombre** (`..._gate`) para no tocar la fn que usaba el backend
      ajeno de `:5002`. `EXCEPT ALL` en ambos sentidos sobre las **69 columnas** previas, los **6
      lotes** de producción de la BD local ⇒ **0 diferencias** en 135 filas.
      ⚠️ Las 135 filas son todas de la **empresa 1**: los 2 lotes de la empresa 4 no tienen
      seguimiento cargado, así que el gate cubre una sola empresa por falta de datos, no por diseño
      · Gotcha confirmado: la fn crea `TEMP TABLE ... ON COMMIT DROP` ⇒ **1 llamada por
      transacción**; dos en la misma consulta fallan con `relation "_seg" already exists`
- [x] B4 `IndicadorProduccionSemanalBdRow` + DTO (`decimal PorcentajeSeleccionMachos`) + `MapRow` +
      test de conversión sin pérdida
- [x] F5 Front: `porcentajeSeleccionMachos` en la interfaz, columna «%Sel M», `PorcSelM` en el Excel,
      colspan del grupo 11 → **12**. Estructura verificada: 61 = 61 = 61
- [x] G6 **La migración la aplicó EF sola** al arrancar el backend (`Database:RunMigrations=true`).
      NO se tocó `__EFMigrationsHistory` a mano — el `INSERT` manual quedó además bloqueado por el
      clasificador de permisos, que es el comportamiento correcto según CLAUDE.md. Antes se verificó
      que la única pendiente real era ésta (los 4 `*.Fn.cs` que figuran como pendientes son
      `partial class` de migraciones ya aplicadas)
- [x] G7 `dotnet build` 0 errores · `dotnet test` 1864+1 verdes · `ng build` OK · smoke ⇒ 200, 44
      semanas, ambas claves en las 44 y `%Sel M` coincidiendo con la fórmula en **44/44**
- [x] G8 Limpieza: fn `..._gate` y tablas `gate_selm_*` borradas, backend de smoke detenido
      (5499 libre, el ajeno de :5002 intacto)

---

# Tracker — ItalJira: historias, tareas y tiempos fuera del módulo de Tickets

**Plan:** [`fase_de_desarrollo/italjira_gestion_historias_tareas_plan.md`](fase_de_desarrollo/italjira_gestion_historias_tareas_plan.md)
**Fecha:** 2026-08-07 · **Bloque propio — no tocar desde otras sesiones**

Pedido: sacar la gestión del área de desarrollo fuera de Tickets a un módulo nuevo **ItalJira**
(Tickets queda con «Mis solicitudes» y «Bandeja de gestión»), agregar el nivel **HISTORIA** encima de
las tareas (historia → tarea → subtarea/bug), permitir tareas nacidas en desarrollo (sin ticket), y
sembrar por migración el histórico REAL de lo ya desarrollado, asignado a `moiesbbuga@gmail.com`.

**Decisiones del usuario:** D1 = tabla nueva `historias` (3 niveles reales) · D2 = mover rutas a
`/italjira` con redirect · D3 = histórico mixto (historias por módulo + una tarea por plan de
`fase_de_desarrollo/`, con fechas reales de git).

## Fase 0 — Auditoría y plan
- [x] Modelo actual auditado: `Ticket` / `TicketTarea` (`ticket_id` **NOT NULL**) / `TicketTiempo`,
      servicios partial, 3 controllers, 6 menús en BD, rutas y páginas del front
- [x] Plan escrito con el DDL, las reglas de negocio y los casos de prueba
- [x] Decisiones D1/D2/D3 confirmadas por el usuario

### Resultado (07-ago-2026)

## Fase 1 — Backend: datos ✔
- [x] Entidad `Historia` + `HistoriaEstados` (alias explícito de `TicketTareaEstados`: un solo vocabulario en los dos niveles del tablero)
- [x] `TicketTarea.TicketId` a `long?` + `HistoriaId` · `Ticket.HistoriaId` · `TicketTiempo.TicketId` a `long?`
- [x] Blast radius del nullable: **solo 5 sitios** (2 proyecciones a DTO + 3 `Contains` en LINQ), todos ajustados con `!= null && …Value`
- [x] `HistoriaConfiguration` (FK `ON DELETE SET NULL`) + 3 configurations existentes + `DbSet<Historia>`
- [x] Migración M1 `20260807075318_AddHistoriasItalJira` idempotente, aplicada en local
- [x] ⚠️ EF arrastró al ModelSnapshot `seguimiento_diario_levante.venta_aves_hembras/machos` de OTRA sesión
      (`20260806235000` las creó por SQL dejando el snapshot atrás **a propósito**). Se **excluyeron
      del Up/Down** de M1 (ya existen en la BD) y se conservó la actualización del snapshot: es
      exactamente la reconciliación que esa migración anticipaba en su comentario

## Fase 2 — Backend: lógica ✔
- [x] `Application/Calculos/HistoriaCalculos.cs` — código correlativo, normalización, sellado de fechas
      (DELEGA en `TicketTareaCalculos`, no lo copia), avance, conteo, rango de roadmap y traducción
      `EstadoTrabajoDeCaso` (las 9 fases del caso al vocabulario de tareas)
- [x] **48 tests xUnit** nuevos (`HistoriaCalculosTests`), incluido el que impide duplicar `Reordenar`
- [x] `HistoriaDtos` (12 records) + `IHistoriaService` + `HistoriaService` (ancla + `Funciones/Backlog`)
- [x] `TicketTareaService.Historias.cs` — partial del MISMO servicio: `ticket_tareas` conserva un
      único escritor, y las dos vistas comparten proyección, reordenamiento y reglas de fecha
- [x] `ProyectarTareasAsync` generalizada a `IQueryable<TicketTarea>`: una sola fórmula para el panel
      del caso y para ItalJira
- [x] `ItalJiraController` (`/api/italjira`, 17 endpoints) + DI en `Program.cs`
- [x] Alcance: ItalJira **no filtra por empresa** (espeja la bandeja de gestión de tickets); la puerta
      es el permiso `tickets.gestionar` / `tickets.admin`, ya configurado en los roles

## Fase 3 — Menús ✔
- [x] Migración M2 `20260807150000_MenusItalJiraFueraDeTickets`: grupo `italjira` + **UPDATE EN SITIO**
      de las 4 vistas (conserva `role_menus`/`company_menus`/`menu_permissions` porque referencian
      `menu_id`) + menú nuevo `italjira.backlog` heredado de quien ya ve el Tablero
- [x] `tickets.admin` pasa a `italjira.configuracion`: la ruta deja de contener `admin` (AWS WAF)
- [x] Verificado en BD: Tickets con 2 items · ItalJira con 5 · 6 roles y 2 empresas conservados intactos

## Fase 4 — Frontend ✔
- [x] `features/italjira/`: routes, `models/historia.models.ts` (re-exporta lo compartido con tickets),
      `services/italjira.service.ts`, `funciones/` (2 puras + README), `components/historia-modal/`
- [x] Páginas MUDADAS con `git mv` (historia preservada): tablero, roadmap, panel, mis-asignados y
      admin-tickets → `configuracion` (clase `ItalJiraConfiguracionComponent`)
- [x] Página nueva **Backlog**: árbol historia → tarea → subtarea/bug, bandeja «sin historia»,
      indicadores, filtros, exportación a Excel (helper compartido) y modales de historia/tarea
- [x] `TareaModalComponent` REUTILIZADO (no se duplicó): el contenedor agrega la historia destino
- [x] Redirects de las 5 rutas viejas + ruta lazy `italjira` en `app.config.ts`
- [x] `changeDetection: Eager` explícito en los 2 componentes nuevos
- [x] `ToastService` / `ConfirmDialogService` / helper de Excel: cero `alert`/`confirm`/`XLSX` inline

## Fase 5 — Histórico real ✔
- [x] Fechas reales extraídas de git para los 198 planes de `fase_de_desarrollo/`
      (`--diff-filter=A` para el alta, `git log -1` para el fin) + título = H1 de cada plan
- [x] Curado en **20 historias por módulo**; TIPO derivado de la naturaleza del plan
      (129 TAREA · 32 BUG · 22 MEJORA · 20 DOCUMENTACION)
- [x] Migración M3 `20260807160000_SeedHistorialDesarrolloItalJira` (+ partial `.Seed.cs` con ~1.900
      líneas generadas): **20 historias / 203 tareas**, todo LISTO salvo «ItalJira», que queda
      EN_CURSO porque es esta misma entrega
- [x] Identidad POR EMAIL con fail-open (si el usuario no existe en el entorno, siembra 0 y no tumba
      el arranque). ⚠️ El int de auditoría **no es la cédula**: la de este usuario (3177120174) no
      entra en un `integer` — se toma el `created_by_user_id` que ya usan sus propios tickets
- [x] Idempotente: historias por `codigo`, tareas por `(historia_id, titulo)`

## Fase 6 — Validación ✔
- [x] `dotnet build` Infrastructure **0/0** y API **0/0** (a salida aparte: el `bin` del API lo tiene
      tomado un `ZooSanMarino.API.exe` **ajeno** en :5002 — proceso de otra sesión, NO se mató)
- [x] `dotnet test` **1.914 Application + 1 Domain**, todo verde
- [x] `yarn build` OK (único warning: bundle budget preexistente)
- [x] **Smoke HTTP** (backend propio :5499, JWT + X-Secret-Up minteados), 11 pasos: backlog inicial
      20/212/19 → crear historia → tarea → subtarea + bug (heredan historia del padre) → 3,5 h de
      worklog con `ticket_id` NULL → avance 33 % → 100 % → agrupar un caso real (4 trabajos, 75 %) →
      tablero 7 columnas y roadmap 2026-05-08→2026-08-07 → borrar la historia deja las 3 tareas
      VIVAS y sueltas → limpieza y estado final idéntico al inicial
- [x] **Smoke UI** (front :4300 + backend :5499, sesión inyectada en `localStorage.auth_session`):
      backlog con las 20 historias y sus tareas, bandeja con los 19 casos reales, modal de historia y
      de tarea abren/cierran **dos veces** sin colgarse, y las 5 rutas viejas redirigen
      (`/tickets/tablero|roadmap|panel|admin|asignados` → `/italjira/...`)
- [x] BD local devuelta a su estado exacto (20 historias del seed, 203 tareas agrupadas, 6 worklogs,
      0 tickets con historia); sin procesos huérfanos; `environment.ts` y `.claude/launch.json`
      restaurados byte a byte y el `bin/smoke-italjira` eliminado

## 🔴 Dos bugs que cazó el smoke (corregidos)

1. **El CHECK `ck_ticket_tareas_no_huerfana` rompía la propia bandeja de sueltas.** Exigía que toda
   tarea tuviera caso, historia o padre; pero una tarea con los tres en NULL es el estado LEGÍTIMO de
   «sin historia» — el que se crea con «+ Tarea suelta» y al que vuelve el trabajo cuando se borra su
   épica. Con el CHECK, `DELETE /historias/{id}` daba **500**. Se retiró de M1 (con `DROP … IF EXISTS`
   defensivo por si alguna base intermedia lo llegó a tener).
2. **El desplegable de columna de cada tarea mostraba siempre «Backlog».** `[value]` en el `<select>`
   (y también `[selected]` en la `<option>`) se aplican ANTES de que el `@for` registre las opciones.
   Fix: `[ngModel]` + `(ngModelChange)`, cuyo accessor reasigna el valor cuando las opciones terminan
   de registrarse. Verificado en pantalla: los 5 selectores pasaron de `BACKLOG` a `LISTO`.

Además, `GetSinAgruparAsync` / la bandeja del backlog dejaron de filtrar `ParentTareaId == null`: al
borrar una historia, sus subtareas quedaban invisibles en las tres pantallas. Ahora la bandeja trae el
árbol completo y el front lo anida.

## Fase 4 — §2.3 Barrido de sobregiro de aves (decisión del usuario: medir primero, sin tocar código)

Pregunta: si el seguimiento diario bloqueara «no cargar más bajas que aves disponibles», ¿cuántas
escrituras históricas quedarían rechazadas y en qué empresas?

- [x] B1 Detector `backend/sql/verificar_sobregiro_aves_postura.sql` (**solo lectura**, hermano de
      `verificar_paridad_saldo_engorde.sql`). Aritmética NO inventada: base y exclusión de filas
      copiadas de `fn_indicadores_levante_postura`; bajas = `SaldoAvesLevanteCalculos.BajasNetas`;
      producción sobre `fn_seguimiento_diario_produccion`. **Sin clamp** (el `GREATEST(0,…)` es lo que
      esconde el sobregiro)
- [x] B2 **Validación cruzada de la fórmula**: producción da saldo idéntico al `saldo_aves_h/m` que la
      propia fn expone en **5/5 LPP**; levante reproduce el **−460** del lote 123 exacto. La medición
      no es una fórmula nueva
- [x] B3 **RESULTADO — 1 sola fila en toda la BD local**:
      · **Levante**: 1 de 902 filas (11 lotes) — el ya conocido **lote 123 «LOTE 235A» de Demo**,
        03-ago-2026: **40 disponibles contra 500 bajas cargadas**. **Agroavícola Sanmarino: 0**
      · **Producción**: **0 de 933 filas** (5 LPP), 0 lotes con saldo final negativo
      · Alcance real del barrido: solo Sanmarino y Demo tienen datos de postura; ItalcolEcuador,
        ItalcolPanamá y Santa Reyes tienen **0 filas** ⇒ el bloqueo no los toca
- [x] B4 🔑 **Hallazgo de diseño que cambia la regla**: **4 lotes de levante y 1 LPP tocan saldo
      exactamente 0**, que es el cierre LEGÍTIMO (lote agotado). La regla tiene que ser
      **`bajas <= disponibles`**, NO `saldo > 0` — exigir `> 0` rompería el cierre normal de todos
      esos lotes. Y explica por qué el soft-check REQ-011b está doblemente mal: compara `saldo == 0`
      exacto ⇒ **salta en el caso legítimo y NO salta en el sobregiro real**
- [x] B5 Margen de operación: levante 6 lotes holgados / 4 en cero / 1 negativo; producción 3
      holgados / 1 con margen 1-50 / 1 en cero. Ningún lote «casi» sobregira ⇒ el bloqueo no
      generaría falsos rechazos por operación normal
- [ ] **Pendiente de decisión**: re-correr el detector contra el dump de PROD antes de implementar
      (la BD local es un dump de fecha incierta y solo tiene 2 empresas con postura). Si prod
      confirma un número parecido, el bloqueo es de riesgo bajo

### Hallazgo lateral del barrido (NO tocado)
- [ ] **Tres fórmulas distintas para el saldo de levante**: `fn_indicadores_levante_postura`
      **NO descuenta ventas** (`r_aves_fin := v_aves_acum - mort - sel - err - tras_sal + tras_ing`),
      mientras que `fn_resumen_semanal_ra_pesadas_levante` y `fn_reporte_semanal_levante_extras`
      **sí** desde `b315612` / `20260806235000`, y `SaldoAvesLevanteCalculos.BajasNetas` también.
      Hoy no se nota (solo 2 filas en toda la BD tienen venta), pero viola «una sola fórmula por
      número» y va a divergir en cuanto se registren ventas de verdad.
      ✅ Verificado de paso: el espejo `fn_indicadores_levante_postura.sql` **sí está al día**
      (cuerpo idéntico a la definición viva) — no hay una segunda bomba de tiempo ahí

---

# Reporte Contable — Selección en RESUMEN + hoja de Movimientos de Huevo

Plan: [reporte_contable_resumen_seleccion_y_huevos_plan.md](fase_de_desarrollo/reporte_contable_resumen_seleccion_y_huevos_plan.md)
Origen: hallazgos 3 y 4 del correo de conciliación del lote K345
([análisis](fase_de_desarrollo/conciliacion_lote_k345_niza_iii_analisis.md) §8).

## Cambio 1 — columna Selección en la hoja RESUMEN
- [x] `ReporteContableResumenCalculos` (Application/Calculos): acumulado puro del resumen semanal
- [x] Reescribir `EscribirResumenSemanal` data-driven (12 columnas, Selección tras Mortalidad)
- [x] Tests xUnit del acumulado

## Cambio 2 — hoja MOVIMIENTOS HUEVOS en el Excel
- [x] `GenerarExcel(reporte, movimientosHuevos = null)` — parámetro opcional, sin romper el caller
- [x] Hoja espejo de la pantalla (POSTURA · HVTO FÉRTIL · HVO COMERCIAL · HUEVO DESECHO + movimientos)
- [x] `ReporteContableController.ExportarExcel` resuelve los movimientos y los pasa

## Validación
- [x] `dotnet build` sin errores ni advertencias nuevas
- [x] `dotnet test` verde
- [x] Smoke: exportar Excel de un lote con producción y cuadrar contra la BD

## Validación cruzada contra los informes de Verenice (lote S-369AB)
- [x] Recuperar el `.xlsm` de levante (viene truncado: sin central directory del ZIP)
- [x] Mapa de columnas del informe → campos de la aplicación (levante y producción)
- [x] Identificar qué campos del informe **no tienen dónde guardarse** en la app
- [x] Contrastar los datos cargados de S-369 contra el informe e informar diferencias

## Alineación de la carga masiva de LEVANTE (hallazgo de la validación contra Verenice)
Análisis: [validacion_informes_verenice_s369_analisis.md](fase_de_desarrollo/validacion_informes_verenice_s369_analisis.md)
- [x] `MigracionEsquemas.SeguimientoLevante`: Coef. Variación H/M, Observaciones Pesaje y los 4 de agua
- [x] `MigracionService.Historicos.cs`: lectura de las columnas nuevas + instrucciones de la plantilla
- [x] `fn_migracion_seguimiento_levante`: recordset + UPDATE + INSERT (espejo `.sql` y migración EF)
- [x] Migración `20260807190000_FnMigracionLevantePesajeYAgua` (+ Designer clonado)
- [x] Tests xUnit del esquema (9) y smoke de la fn en transacción revertida
- [x] **Descartado (era un dato mío equivocado)**: el modal de levante SÍ captura el C.V. — los controles
      se llaman `cvH`/`cvM` y el servicio los mapea a `CvHembras`/`CvMachos`
      (`SeguimientoLoteLevanteService.Mapeos.cs:173`). El hueco estaba solo en la carga masiva, ya cerrado
- [ ] **Pendiente de decisión (técnica + costos)**: el corte levante/producción quedó en 24 semanas
      en S-369 y el informe de Verenice usa 25 ⇒ ~17.332 kg cambian de etapa en una conciliación

## Corte de etapa: bloqueo del doble conteo levante/producción
- [x] `CorteEtapaPosturaCalculos` (Application/Calculos): regla pura + mensajes, 10 tests xUnit
- [x] `SeguimientoLoteLevanteService.EnsureDiaSinAporteDeProduccionAsync` en el alta de levante
- [x] `ProduccionService.EnsureDiaSinAporteDeLevanteAsync` en el alta de producción
- [x] La regla mira el APORTE (consumo/bajas), no la existencia de la fila: el arrastre de huevos del
      levante crea filas de producción de solo huevos y esas NO deben chocar
- [x] Barrido de la BD: el traslape existe solo en K345 (15 días) ⇒ el guard no rompe nada existente
- [x] `dotnet build` + `dotnet test` (1.939 en verde)
- [ ] **Pendiente, requiere OK explícito**: limpiar los 15 días traslapados de K345 (el guard impide
      nuevos, los existentes siguen ahí). Hay que decidir cuál de las dos filas queda antes de tocar datos

## Entrega
- [x] Respuesta final para costos con las correcciones aplicadas:
      [conciliacion_k345_respuesta_final_con_correcciones.md](fase_de_desarrollo/conciliacion_k345_respuesta_final_con_correcciones.md)

---

# Tracker — Reporte Diario Área de Costos: POSTURA (levante + producción)

**Plan:** [`fase_de_desarrollo/reporte_diario_costos_postura_plan.md`](fase_de_desarrollo/reporte_diario_costos_postura_plan.md)
**Fecha:** 2026-08-07 · **Sesión propia — no tocar desde otras sesiones**

Reporte diario para el área de costos de **Agroavícola San Marino (Colombia)**, sobre **lote base**, con 3
pestañas (Aves · Alimento · Huevos) y filtros regional/granja/lote base/fase/fechas. Validación contra el
lote base **S-369** (sublotes S-369A id 144 y S-369B id 145, granja Pruebas Moises 44), cargado por carga
masiva desde informes reales. **Es POSTURA**: engorde solo se usa como molde de arquitectura.

## Fase 0 — Exploración y decisiones
- [x] Auditadas las fuentes: `seguimiento_diario_levante` (77 col) y `seguimiento_diario_produccion` (68 col)
- [x] 🔑 **Producción SÍ tiene fn diaria canónica** (`fn_seguimiento_diario_produccion`, expone las 11
      categorías de huevo + `metadata` con los ítems de alimento) y **levante NO**
      (`fn_indicadores_levante_postura` es **semanal**) ⇒ producción se reusa por LATERAL, levante se lee
      de la tabla dentro de la fn nueva (un solo lugar con ese criterio)
- [x] 🔑 **Invariante de huevo verificado en datos reales**: `huevo_tot = Σ 11 categorías` y
      `huevo_inc = limpio + tratado` (7.799 = 7.799 el 15-may; 1.021.041 = 992.662+28.379 acumulado)
- [x] 🔴 **Hallazgo**: el Reporte Contable muestra «HVTO FÉRTIL» y «HVO COMERCIAL» con el **mismo número**
      (ambos = limpio+tratado). Documentado como deuda; NO se toca en esta entrega
- [x] Datos de S-369 medidos: levante 168+168 días, producción 168+161 días, 0 días duplicados
- [x] ⚠️ `traslado_huevos` **sin filas** para 144/145 ⇒ ventas/traslado a planta se validan con el lote 13
- [x] ⚠️ `farms.regional_id=27` de Pruebas Moises no resuelve a `master_list_options` ⇒ regional vacía
- [x] Plan escrito con enfoque, DDL, reglas de negocio y 25 casos de prueba
- [x] Decisiones D1-D4 confirmadas por el usuario: **D1** huevo `fértil=inc / comercial=sucio+deforme+
      blanco+doble_yema+piso+pequeño / inservible=roto+desecho+otro` (partición exacta) · **D2** lote base
      **opcional**, filas por lote:galpón · **D3** fase **Levante|Producción|Ambas** · **D4** alimento
      **una fila por ítem**

## Fase 1 — BD ✔
- [x] `backend/sql/fn_reporte_diario_costos_postura.sql` (LANGUAGE sql STABLE, corte de día
      `AT TIME ZONE 'America/Bogota'`, `DISTINCT ON` gana el timestamp más temprano = mismo criterio
      que la fn canónica de producción)
- [x] Migración idempotente `20260807200000_AddFnReporteDiarioCostosPostura` con el `.sql` embebido
      **verbatim** (Designer clonado, ModelSnapshot intacto) ⇒ el espejo no puede desincronizarse
- [x] Aplicada por EF al arrancar (nunca a mano en `__EFMigrationsHistory`) y verificada contra S-369
- [x] 🔑 **Corrección de diseño**: la fn devuelve el huevo **CRUDO** (11 categorías). La clasificación
      D1 se movió a C# puro y testeado — calcularla también en SQL era la 2ª implementación del mismo
      número
- [x] 🔴 **Hueco cazado en la UI**: el metadata de alimento tiene DOS formas — camino 2 trae `nombre`
      (S-369) y camino 1 solo `catalogItemId` (K345, lotes viejos). Sin resolver contra
      `catalogo_items` / `item_inventario_ecuador`, la columna «tipo alimento» salía
      **«Sin especificar»** en todos los lotes viejos. También se cubrió el 2º formato de
      `tipo_alimento` (`"x / y"` sin prefijo de sexo)
- [x] 🔑 `venta_aves_hembras/machos` por `LEFT JOIN` con `seg_id`: la fn canónica NO las expone y su
      `mov_venta_*` (de `movimiento_aves`) vale 0 en los lotes de carga masiva

## Fase 2 — Application ✔
- [x] `DTOs/ReporteDiarioCostosPosturaDtos.cs` (+ `HuevoCrudo` y `ParticionCuadra`)
- [x] `Interfaces/IReporteDiarioCostosPosturaService.cs`
- [x] `Calculos/ReporteDiarioCostosPosturaCalculos.cs` (PURO: `ClasificarHuevo` = único dueño de D1,
      `NormalizarFase`, `EtiquetaLoteGalpon`, totales de aves/alimento/huevo)

## Fase 3 — Infrastructure + API ✔
- [x] `Services/ReporteDiarioCostosPostura/ReporteDiarioCostosPosturaService.cs` — delgado y
      fail-closed (empresa efectiva + granjas asignadas + alcance granular por `LotePermitido`)
- [x] `Controllers/ReporteDiarioCostosPosturaController.cs` → `POST /api/ReporteDiarioCostosPostura/generar`
- [x] DI en `Program.cs`
- [x] Migración `20260807201000_AddMenuReporteDiarioCostosPostura`: menú bajo «Reportes», 9 roles
      heredados de `/reporte-contable` (incluye **«costos Sanmarino»**) y `company_menus` **SOLO
      Agroavicola Sanmarino** (habilitarlo en otras empresas es decisión de negocio desde la UI)

## Fase 4 — Tests (gate CI) ✔
- [x] `ReporteDiarioCostosPosturaCalculosTests` — **25 casos** con testigos reales de S-369B
      (días 15-may y 15-jun, acumulado del ciclo, invariante `inc == limpio + tratado`, fila
      inconsistente que NO se cuadra a la fuerza, 2 alimentos del mismo sexo, sinónimos de fase)

## Fase 5 — Frontend ✔
- [x] `features/reporte-diario-costos-postura/` (models · funciones puras + README · service · página)
- [x] 3 pestañas, cascada regional → granja → lote base, `changeDetection: Eager` explícito
- [x] Export Excel de 3 hojas con `exportarAoaMultiHojaExcel` (sin `XLSX` inline), `ToastService`,
      cero `alert`/`confirm`, vista precalculada con referencias estables
- [x] Ruta lazy `/reporte-diario-costos-postura` en `app.config.ts`

## Fase 6 — Validación ✔
- [x] `dotnet build` 0 errores / 0 advertencias · `dotnet test` **1.992 verdes** · `yarn build` OK
      (único warning: bundle budget preexistente)
- [x] **15/15 testigos SQL** contra S-369: 168/168 y 168/161 días · mort 307/125 · sel 71/308 ·
      err 379/3 · venta 0/290 y 224/67 · consumo 104.073,6/16.772,4 y 237.626,8/18.703 ·
      huevo 1.115.079 con fértil 1.021.041
- [x] **Partición D1 exacta**: diferencia **0** en los dos lotes · **0 descuadres** de ítems de
      alimento vs `consumo_kg_*` en las 1.267 filas · **0 ítems sin nombre**
- [x] **Cruce independiente contra el Reporte Contable** (lote K345, regional Occidente): postura
      **3.632.634**, fértil **3.484.872**, traslado a planta **2.395.894** — idénticos a los del
      smoke ya validado de ese módulo
- [x] Fail-closed verificado: empresa Demo ⇒ 0 filas · `p_granja_ids` vacío ⇒ 0 filas
- [x] Smoke HTTP (JWT + X-Secret-Up minteados): 665 filas con lote base, filtros de fase (incluida
      «Producción» con acento), rango de fechas y regional
- [x] **Smoke UI** (front :4300 + backend propio, sesión en `localStorage.auth_session`): filtros
      poblados (6 regionales / 30 granjas / 4 lotes base), reporte de 1.267 registros, las 3 pestañas
      pintan, Excel de 3 hojas descargado (blob 1,2 MB) y **página abierta 3 veces sin colgarse**
- [x] Aritmética cruzada en pantalla: 665 (S-369) + 602 (K345) = **1.267** · fértil 5.558.965 +
      comercial 245.251 + inservible 86.070 = **5.890.286** = huevo total
- [ ] Sin procesos huérfanos · commit acotado (sin footer de atribución)

### Notas para la siguiente tanda
- ⚠️ `traslado_huevos` **no tiene filas** para S-369A/B ⇒ «ventas de huevo» y «traslado a planta»
  salen en 0 para ese lote. La columna se validó con K345 (2.395.894 a planta).
- ⚠️ `farms.regional_id = 27` de *Pruebas Moises* no resuelve a ninguna opción de `master_list_options`
  ⇒ esa granja queda fuera del filtro por regional (sale con regional vacía).
- 🔴 **Deuda ajena documentada**: el Reporte Contable muestra «HVTO FÉRTIL» y «HVO COMERCIAL» con el
  **mismo número** (ambos = limpio + tratado). No se tocó en esta entrega.
- Levante sigue sin fn diaria canónica: si algún día nace `fn_seguimiento_diario_levante`, este
  reporte debe re-sourcearse sobre ella y verificarse byte a byte.

---

# El nombre de lote es único POR GALPÓN, no por granja
📄 Plan: [lote_nombre_duplicado_por_galpon_plan.md](fase_de_desarrollo/lote_nombre_duplicado_por_galpon_plan.md)

Origen: ticket «Falla en fecha registro levante semana 6 lote A374A galpón 4». La causa del ticket
(`tipo_alimento varchar(100)`) ya se resolvió en `2a35d63` y se desplegó el 07-ago-2026; acá van los
dos defectos laterales que aparecieron al diagnosticarlo.

## Diagnóstico
- [x] Ticket ubicado: `lote_id 114` = A374A / LA ESMERALDA / Módulo II / `G0326` (galpón 4)
- [x] Causa del ticket confirmada (3er alimento ⇒ 22001) y deploy verificado: TaskDef `sanmarino-back-task:151`, imagen `4fcafbd…`, rollout COMPLETED
- [x] Las filas de 09/06/2026 y 12/06/2026 NO son registros incompletos: son traslados (SALIDA 1.010 M y 7.617 H)
- [x] Regla confirmada por el usuario: **el mismo nombre de lote SÍ puede repetirse en galpones distintos**
- [x] `GetLetrasDisponiblesAsync` (alcance por galpón) está BIEN ⇒ no se toca
- [x] Regresión encontrada: `EnsureLoteNombreNoDuplicadoAsync` (17-jul-2026, `b917ad9`) valida por granja ⇒ hoy bloquea el patrón legítimo 114/116 y 115/117

## Backend — alcance de la guarda
- [x] `Application/Calculos/LoteNombreDuplicadoCalculos.cs` (PURO: normaliza, decide, arma mensaje)
- [x] `LoteService.EnsureLoteNombreNoDuplicadoAsync` recibe `galponId` y delega en el cálculo puro
- [x] Los 2 llamadores (Create/Update) pasan `dto.GalponId`

## Frontend — combo «Lote» del seguimiento diario
- [x] `[compareWith]` + `compararLoteId` en `modal-create-edit` (el control guarda texto y las opciones número)

## Tests y validación
- [x] `tests/ZooSanMarino.Application.Tests/LoteNombreDuplicadoCalculosTests.cs` — 10 casos del plan
- [x] `dotnet build` 0/0 (Infrastructure) · `dotnet test` 1992 verdes (24 nuevos) · `yarn build` OK (solo warning de bundle budget)
- [x] Sin procesos huérfanos (no se levantó servidor propio: otra sesión tiene el back/front corriendo)
- [ ] Verificación visual del combo en el navegador — pendiente: el dev server de este repo lo ocupa otra sesión
- [x] Commit acotado `226a5a4` (sin footer de atribución)

---

# Gastos de inventario — elegir el rango de fechas del consumo (tabla + Excel)
📄 Plan: [gastos_inventario_rango_fechas_plan.md](fase_de_desarrollo/gastos_inventario_rango_fechas_plan.md)

Pedido: «al momento de descargar pueda elegir de qué fecha hasta qué fecha necesito el consumo de
productos, para así no tener que bajar todos los consumos realizados». Backend y BD **no se tocan**:
`search`, `export` y `existencias` ya aceptan `fechaDesde`/`fechaHasta` — la UI nunca los enviaba.

## Diagnóstico
- [ ] Confirmado: los 3 endpoints ya filtran por rango; `buildParams` del servicio ya los serializa
- [ ] Confirmado: `FiltrosReporteGastos.fechaDesde/Hasta` ya existían y `describirFiltros` ya los imprime
- [ ] Confirmado: `inventario_gasto.fecha` es columna `date` ⇒ sin corrimiento de zona, filtro inclusivo

## Frontend
- [ ] `funciones/rango-fechas-gastos.funcion.ts` (PURA): presets, validación y sufijo de archivo
- [ ] `funciones/exportar-...-excel.funcion.ts`: rango en el nombre del archivo + subtítulo de Existencias
- [ ] Página: estado `fechaDesde`/`fechaHasta`, propagación a `refresh()` / `exportExcel()` / `limpiarFiltros()`
- [ ] HTML: campos Desde/Hasta + atajos + aviso de rango inválido · SCSS de los chips
- [ ] `funciones/README.md`: índice actualizado

## Validación
- [ ] `cd frontend && yarn build` (0 errores)
- [ ] Smoke en pantalla: rango aplicado ⇒ tabla acotada y Excel con las mismas filas
- [ ] Sin rango ⇒ comportamiento idéntico al actual (nombre de archivo incluido)
- [ ] Sin procesos huérfanos · commit acotado (sin footer de atribución)

---

# Manual de carga masiva de POSTURA (documento para implementación)
📄 Entregable: [manual_carga_masiva_postura.html](fase_de_desarrollo/manual_carga_masiva_postura.html)

Pedido: manual para la persona encargada de implementar la carga masiva de postura — campos de cada
hoja, en qué estado debe estar el lote base, orden levante → cierre → producción, y el contacto con
Gestión de Inventario (alimento) y las ventas de aves/huevos. **No toca código del repo.**

## Fuente del contrato (regla «el código manda»)
- [x] `Application/Calculos/MigracionEsquemas.cs` — columnas exactas, obligatoriedad, alias y opciones
- [x] `Services/Migracion/Funciones/MigracionService.Historicos.cs` — elegibilidad, parseo, merge de arrastre
- [x] `MigracionService.AlimentoPostura.cs` + `.AlimentoEngorde.cs` — gate de stock, idempotencia, `Origen`
- [x] `DTOs/CreateLoteDto.cs` + `GuiaGeneticaRequisitoCalculos.cs` — campos del lote base y exigencia raza/año
- [x] Archivos reales del lote S-369AB como ejemplo verificado de cada hoja

## Contenido
- [x] Ruta completa (6 pasos, cuáles son Excel y cuáles por pantalla)
- [x] Lote base: campos, `Fase=Levante` explícito, raza/año contra la guía
- [x] Compuerta de elegibilidad levante vs. producción (las 3 condiciones encadenadas)
- [x] Hoja `Datos` levante (43 col.) y producción (43 col.) con extracto real renderizado
- [x] Hoja `Alimento` (14 col.) + orden interno del importador + gate de stock
- [x] Hoja `Movimientos Aves` (8 col.): Salida / Ingreso / Venta unilaterales
- [x] Hoja `Movimientos Huevos` (18 col.) y hoja `Huevos` (por ítems, solo Santa Reyes)
- [x] Las 5 trampas silenciosas + las 2 advertencias que NO descartan fila
- [x] Checklist de entrega y cuadre esperado

## Validación
- [x] Publicado como artifact compartible
- [x] Versión **Word** (`Manual_Carga_Masiva_Postura.docx`, 17 pág.) + PDF, con índice numerado
- [x] Renderizado y revisado página por página (Word COM → PDF → PNG con pymupdf); corregidos:
      nombres de columna pegados (Word descarta runs de solo espacio), filas y callouts partidos
      por el salto de página (`cantSplit`), interlineado del título de portada
- [ ] Capturas de pantalla del módulo en el navegador — pendiente: requiere login del usuario
- [x] Sin procesos huérfanos (back :5002 y front :4200 los levantó otra sesión; no se detuvieron)

---

# Migraciones Masivas — retirar los tipos «Ventas / Movimiento de Aves / Movimiento de Huevos»

**Plan:** [`fase_de_desarrollo/migraciones_masivas_retiro_tipos_ventas_movimientos_plan.md`](fase_de_desarrollo/migraciones_masivas_retiro_tipos_ventas_movimientos_plan.md)
**Fecha:** 2026-08-07

Pedido: las ventas y los traslados ya se cargan **dentro** del seguimiento diario (hojas
`Movimientos Aves` / `Movimientos Huevos` de las plantillas de Levante y Producción), así que las
tres cajitas «Próximamente» de la Fase 3 sobran. Además el tile queda ilegible: el badge
«Sin permiso para carga masiva» (nowrap) aplasta la descripción a una palabra por línea.

## Auditoría previa (el código manda)
- [x] Los 3 enum members solo se referencian en `TipoMigracion.cs` + 1 test — no llegan a `ProcesarAsync`
- [x] `MigracionService.MovimientosAves/.MovimientosHuevos` son HOJAS del seguimiento, no estos tipos — no se tocan
- [x] `migracion_masiva.tipo` es varchar con `tipo.ToString()` ⇒ borrar miembros no corre ordinales
- [x] `VentaPolloEngorde` está implementado y en uso ⇒ queda (pendiente confirmación del usuario)

## Backend
- [x] `TipoMigracion.cs`: borrar `Ventas`/`MovimientoAves`/`MovimientoHuevos` del enum y del catálogo
- [x] `MigracionEsquemas.Para()`: mensaje del `_ =>` sin referencia a «Fase 3»
- [x] `MigracionService.Operaciones.cs`: comentario de cabecera + mensaje del `_ =>` de elegibles
- [x] `MigracionEsquemasTests.Para_TipoSinEsquema_Lanza`: usar un valor no definido del enum

## Frontend
- [x] `models/migracion.model.ts`: sacar los 3 del union `TipoMigracionCodigo`
- [x] `selector-tipo-migracion.component.ts`: sacar sus 3 íconos
- [x] `selector-tipo-migracion.component.ts`: layout del tile — metadatos (Fase + badge) debajo del texto

## Validación
- [x] `cd backend && dotnet build` — 0 errores; única advertencia CS8625 en `MigracionMovimientosAvesCalculosTests.cs:184`, PREEXISTENTE
- [x] `cd backend && dotnet test` — 1.992 Application + 1 Domain, 0 fallos
- [x] `cd frontend && yarn build` — 0 errores (solo el warning de bundle budget preexistente).
      ⚠️ Trampa propia: puse backticks dentro de un comentario CSS del bloque `styles` inline ⇒ cortaron
      el template literal y el compilador tiró «Failed to resolve styles at position 1 to a string».
      **Nunca usar backticks dentro de un `styles`/`template` inline.**
- [x] Layout verificado en el navegador con una página aislada que copia el CSS y el markup finales:
      ANTES reproduce el defecto de la captura (badge sobre el título, descripción en 1 palabra/línea);
      DESPUÉS: 6 tiles, descripción completa a 2 líneas y chips alineados al pie
- [x] Plantillas intactas por código: `MigracionService.Historicos.cs:137-144` sigue agregando las hojas
      `Movimientos Aves` (levante+producción) y `Movimientos Huevos` (producción); la aplicación en :851
- [x] Sin procesos huérfanos (no se levantó back ni front) · commit acotado (sin footer de atribución)
- [ ] **Pendiente de decisión del usuario**: ¿el tile «Venta Engorde» (`VentaPolloEngorde`) también sale?
      Hoy queda: está implementado y en uso (fn `fn_migracion_venta_engorde` v2 con despachos), y la venta
      de engorde NO se registra desde el seguimiento diario

---

# Tracker — Reporte Diario Costos Postura: el levante nunca salía + lote base multi-granja

**Plan:** [`fase_de_desarrollo/reporte_diario_costos_postura_levante_vacio_y_multigranja_plan.md`](fase_de_desarrollo/reporte_diario_costos_postura_levante_vacio_y_multigranja_plan.md)
**Fecha:** 2026-08-07

**Pedido:** el reporte no trae nada para lotes con levante (NIZA III, granja de pruebas), y un lote base
puede quedar repartido en varias granjas (levante en NIZA III, producción en NIZA I) — el reporte tiene
que seguir al lote base y decir en qué granja pasó cada fase.

## Diagnóstico (contra el dump de producción, BD local :5433)
- [x] `lev_dedup` filtra `s.lote_id_int IS NOT NULL`; en prod las **588 filas** de `seguimiento_diario_levante` la tienen **NULL** (100 %) ⇒ **0 filas de levante en toda la empresa**
- [x] `grep "LoteIdInt" backend/src` = **0 coincidencias**: ningún C# escribe esa columna; solo `fn_migracion_seguimiento_levante` la setea en sus INSERT (por eso S-369 validó en local y prod no)
- [x] Sanmarino tiene 6 lotes: K345A/B (NIZA III, 176+175 días de levante) y A374A ×2 (LA ESMERALDA, 144+38, **sin producción** ⇒ salía vacío)
- [x] Traslape K345: **15 días** con fila en las dos etapas; 14 son doble conteo real (16.952 kg) y 1 tiene la fila de levante vacía
- [x] El traslado **NO crea un lote nuevo**: pisa `lotes.granja_id` ⇒ el reporte re-atribuía TODO el histórico a la granja nueva (verificado: NIZA III pasa de 953 filas a 0)
- [x] `fn_mover_lote` pisaba la granja **sin registrar** en `historial_traslado_lote` (`TrasladarLoteAsync` sí lo hace)
- [x] `edad_dias`/`semana` de producción no cuadraban con la fn canónica: **301/301 filas** de K345B desfasadas 3 días, 129 con semana distinta
- [x] Ninguna pestaña ni hoja de Excel mostraba la granja por fila

## BD / SQL
- [x] `fn_reporte_diario_costos_postura` v2: `lev_dedup` por `lote_id` (texto) + guardas `tipo_seguimiento`/`reproductora_id`
- [x] Granja **vigente el día** vía `historial_traslado_lote`; filtro `p_granja_ids` matchea la actual O cualquiera histórica
- [x] `edad_dias`/`semana` de producción desde la fn canónica (levante conserva su `fecha_encaset`)
- [x] `fn_mover_lote` registra el traslado en `historial_traslado_lote` cuando cambia de granja
- [x] Migraciones EF idempotentes con el `.sql` embebido verbatim (`20260807220000` y `20260807221000`, Designer clonado, ModelSnapshot intacto)

## Backend
- [x] DTOs: `DiaEnAmbasEtapas`/`ExcluidoDelTotal` en la fila; `Ubicaciones`/`DiasDuplicados`/`TotalesExcluidos`/`AlcanceExpandidoPorLoteBase` en el reporte
- [x] `MarcarDuplicados` delegando en `CorteEtapaPosturaCalculos.HayDobleConteo` + `Ubicaciones` + `TotalesExcluidos`
- [x] `ConstruirTotales` ignora las filas excluidas (sin marcas queda idéntico)
- [x] Service: el lote base expande el alcance a las granjas asignadas (fail-closed intacto)
- [x] `GET /api/ReporteDiarioCostosPostura/lotes-base` (catálogo por dónde están los lotes, scoped al usuario)

## Frontend
- [x] Modelo + service apuntando al catálogo nuevo
- [x] Columna **Granja** en las 3 pestañas y en las 3 hojas del Excel
- [x] Bloque «Dónde se hizo cada fase» + aviso de días duplicados **cuantificado** + nota de alcance expandido
- [x] Filas excluidas atenuadas (`.rdc-tr--excluida`) y marcadas «NO SUMA» en el Excel
- [x] Cascada del filtro por `granjaIds`; `granja` en las track keys (la etiqueta lote:galpón ya colisiona entre granjas)

## Tests / validación
- [x] `ReporteDiarioCostosPosturaCalculosTests`: +11 casos (marcado, arrastre de solo huevos, cuantificación, ubicaciones, no regresión)
- [x] `dotnet build` 0 errores / sin advertencias nuevas · `dotnet test` **2.004 en verde**
- [x] `yarn build` OK (solo el warning de bundle preexistente)
- [x] P1-P13 del plan: gate de paridad de producción en las **5 empresas** (0 diferencias), traslado simulado en transacción revertida, smoke API y smoke UI
- [x] Sin procesos huérfanos (back :5002 y front :4200 detenidos, sin listeners)
- [x] Commit acotado (sin footer de atribución)
---

# Migraciones Masivas — permiso de POSTURA, tiles por permiso y módulo solo para Sanmarino

**Plan:** [`fase_de_desarrollo/migraciones_masivas_retiro_tipos_ventas_movimientos_plan.md`](fase_de_desarrollo/migraciones_masivas_retiro_tipos_ventas_movimientos_plan.md) (sección 7)
**Fecha:** 2026-08-07 · Continúa el bloque commiteado en `cbc922c`

Pedido: (a) en prod no se puede cargar postura «porque el permiso no existe»; (b) los cargadores sin
permiso deben OCULTARSE, no salir en gris; (c) el módulo debe quedar solo para Sanmarino Colombia.

## Diagnóstico (contra el dump de prod en la BD local)
- [x] `carga_masiva_postura` SÍ existe como fila (la creó `20260714115357`); lo que falta es la
      ASIGNACIÓN: ningún rol de Sanmarino la tenía. `Implementador Sanmarino Colombia` (3 usuarios)
      tenía solo `carga_masiva_pollo_engorde` ⇒ los tiles de postura salían bloqueados
- [x] `Menus_GetForUserAsync` arma el menú desde **`role_menus`** y NO lee `company_menus` ⇒ para
      ocultar el módulo hay que limpiar las DOS tablas, no solo la de empresa
- [x] `AddMigracionesMasivasMenu` lo sembró heredando de «Lotes» ⇒ quedó en las 5 empresas

## Backend — migración `20260807230000_RestringirMigracionesMasivasASanmarino`
- [x] Re-asegura que existan `carga_masiva_postura` y `carga_masiva_pollo_engorde` (NOT EXISTS)
- [x] `company_menus`: solo «Agroavicola Sanmarino» (borra el resto, garantiza la de Sanmarino)
- [x] `role_menus`: conserva solo roles de uso EXCLUSIVO de Sanmarino (un rol compartido se retira)
- [x] `role_permissions`: `carga_masiva_postura` al rol exclusivo de Sanmarino que YA tenía el de engorde
- [x] Todo por `companies.name` / `menus.route` / `permissions.key`, nunca por id (difieren local↔prod)
- [x] `Down` restaura el punto de partida reheredando de «Lotes»
- [x] Designer clonado del último migration; ModelSnapshot intacto (data-only)

## Frontend
- [x] `funciones/filtrar-tipos-visibles.funcion.ts` (PURA): descarta estructura, no implementados y
      líneas sin permiso. **Fail-closed**: lista de permisos vacía ⇒ no se ofrece nada
- [x] Página: `toSignal(permissions$)` + `tiposVisibles` = `filtrarTiposVisibles(...)` · `sinPermisos`
- [x] Aviso «No tenés permisos de carga masiva asignados» nombrando las dos claves exactas a pedir
- [x] Selector: queda 100% presentacional — se elimina `UserPermissionService`, `tienePermiso`,
      `mensajeSinPermiso`, `onClick` y los estilos `tile--locked` / `tile--soon` (código muerto)
- [x] `funciones/README.md` actualizado

## Validación
- [x] `cd backend && dotnet build` — 0 errores (solo la advertencia CS8625 preexistente)
- [x] `cd frontend && yarn build` — 0 errores (solo el bundle budget preexistente)
- [x] Migración simulada en la BD local **dentro de una transacción con ROLLBACK**: `company_menus`
      5 → 1; `role_permissions` +1 (rol 32); 2ª corrida seguida = todos los contadores en 0 (idempotente)
- [x] Filtro de `role_menus` probado rama por rama (sembrado y revertido): se retiran los roles de
      otra empresa, el rol SIN usuarios y el rol COMPARTIDO Sanmarino+Ecuador; se conservan solo los
      exclusivos de Sanmarino
- [x] BD local sin cambios (todo bajo ROLLBACK) · sin procesos huérfanos
- [ ] ⚠️ **Efecto colateral a confirmar con el usuario**: «solo Sanmarino» le quita el módulo a
      **Santa Reyes** (2 roles que HOY tienen ambos permisos) y a **ItalcolPanama / Demo / Ecuador**.
      Si Santa Reyes debe conservarlo, hay que agregar su nombre a la lista de empresas habilitadas
- [ ] Smoke en prod tras el deploy: usuario de Sanmarino ve solo sus 2 tiles de postura + engorde;
      usuario de otra empresa ya no ve el ítem de menú

---

# Tracker — Lote cerrado que absorbe el ciclo siguiente (KM 86) + ventana de mes actual en Inventario

**Plan:** [`fase_de_desarrollo/lote_cerrado_absorbe_ciclo_siguiente_y_ventana_mes_inventario_plan.md`](fase_de_desarrollo/lote_cerrado_absorbe_ciclo_siguiente_y_ventana_mes_inventario_plan.md)
**Fecha:** 2026-08-07 · Ticket de operación Ecuador (granja KM 86, lote 2601, Galpon-1 y Galpon-2)

Pedido: (a) la grilla de un lote que terminó en ABRIL muestra ingresos de julio; (b) que en Gestión de
Inventario solo se pueda cargar movimientos manualmente con fecha del mes actual.

## Diagnóstico (contra el dump de prod en la BD local :5433)
- [x] Captura identificada: `fn_seguimiento_diario_engorde(2)` reproduce edad y saldos byte a byte
- [x] Causa raíz: `rango_final.fecha_max` NULL (lote `Abierto` + saldo que nunca llega a 0) ⇒ grilla sin tope
- [x] Asimetría confirmada: v11/v12 excluyen ciclos ajenos en la APERTURA, nunca en el CIERRE
- [x] Alcance medido en las 2 empresas con engorde: solo 2 lotes invadidos (EC 2 y 86); **Panamá 0**
- [x] Los ingresos de julio son CORRECTOS (son del lote 2603): el error es a qué lote se los muestra
- [x] Plan escrito + decisiones D1-D4 confirmadas por el usuario

## Parte A — fn v14: corte por ciclo siguiente (la versión vigente era la v13, no la v12)
- [x] `backend/sql/fn_seguimiento_diario_engorde.sql` v14 (CTE `corte_ciclo_siguiente` + `LEAST` en `rango_final`).
      `LEAST` ignora los NULL ⇒ un lote sin ciclo posterior conserva su corte de v13 y uno activo sigue sin tope
- [x] Migración `20260808010000_FnSeguimientoEngordeV14CorteCicloSiguiente` (+ `.Fn.cs` con v14 y v13 verbatim,
      Designer clonado, ModelSnapshot intacto, `Down` = v13). Aplicada en local con `dotnet-ef` 10
- [x] `SaldoAlimentoEngordeCalculos.ResolverInicioCicloSiguiente` / `.ResolverFechaMaxGrilla` (puro, hermanas de
      las de v11/v12) + `CorteCicloEngordeCalculosTests` — 12 casos
- [x] Gate multipaís antes/después: **ItalcolPanama NO-OP** (los 6 de `dif_saldo_aves`/`dif_consumo` son un
      artefacto preexistente del script —claves (lote,fecha) duplicadas—, idéntico en la corrida de línea base)
- [x] Comparación fila a fila de los 140 lotes: **solo cambian 2**, lote 2 (31 filas) y lote 86 (1 fila);
      0 diferencias de saldo/aves/ingreso/consumo/documento en las filas que quedan
- [x] **0 filas con seguimiento real perdidas** (5.722 esperadas == 5.722 presentes): solo desaparecen
      filas movimiento-only. Los ciclos siguientes del galpón (72, 104) quedan intactos
- [x] `fn_cuadre_alimento_engorde` 22 → 22 y `fn_cuadre_aves_engorde` 1 → 1 (sin regresión)
- [x] Resultado: la grilla del lote 2601 / Galpon-1 termina el **2026-04-20 con 1.600 kg** (antes 2026-08-03 con 206.450)

## Parte B — Ventana de mes actual (D1 todo movimiento manual · D2 todas las empresas · D3 hasta hoy)
- [x] `Application/Calculos/VentanaFechaMovimientoInventarioCalculos.cs` (puro) + 12 tests xUnit.
      `DiaOperativo` = UTC−5 (CO/EC/PA sin DST): sin eso, las últimas 5 h del mes el servidor ya está en el
      mes siguiente y rechaza la fecha de HOY que el usuario ve en pantalla
- [x] Gate en el CONTROLLER (`ValidarVentanaFecha`) en las 5 puertas manuales: `POST /ingreso`, `POST /traslado`,
      `PUT /ingresos/{id}/fecha`, `PUT /traslados/{gid}/fecha`, `PUT /stock/{id}` (`FechaIngreso`)
- [x] **NUNCA en el service**: `RegistrarIngreso/Traslado/ConsumoAsync` los llaman la carga masiva, los 4 services
      de seguimiento (devoluciones al editar/borrar) e `InventarioGastoService`, que fechan histórico a propósito.
      `POST /consumo` no se toca (el front nunca lo llama)
- [x] Front: `funciones/ventana-fecha-movimiento.funcion.ts` (pura, espejo del backend) + `min`/`max` y leyenda en
      los 3 datepickers de movimiento (alta de ingreso, alta de traslado, ajuste de stock) y en el de edición de
      fecha del histórico + validación previa al submit en los 5 caminos
- [x] Los filtros «Fecha desde/hasta» del histórico NO se tocan (son filtros, no fechas de movimiento)

## Validación
- [x] `dotnet build` — 0 errores (solo la advertencia CS8625 preexistente)
- [x] `dotnet test` — **2.028 Application + 1 Domain, 0 fallos** (+24 nuevos)
- [x] `yarn build` (Node portable 22.23.1) — OK, solo el warning de bundle budget preexistente
- [x] Smoke HTTP real (back :5002 Dev, JWT + X-Secret-Up minteados) de las 5 puertas: mes anterior y mañana
      dan **400 con el mensaje de la ventana**; hoy pasa y llega al servicio (200, o el error de dominio esperado)
- [x] **BD local restaurada exacta**: el smoke escribió 3 movimientos, 2 registros de stock y corrió la fecha del
      movimiento 1 (doc 52968, granja 38 / G0035). Todo revertido; la fecha original (2026-02-07) se recuperó por
      los documentos correlativos vecinos (52912/52913/52925/52971, todos de esa fecha) y quedó **verificada por el
      gate**: la corrida posterior es idéntica a la del cambio (5.804 filas, 0 diferencias de valor)
- [x] Tablas temporales del gate eliminadas · sin procesos huérfanos (5002/5499/4200 sin listeners)
- [x] Commit acotado (sin footer de atribución). ⚠️ NO se commiteó
      `fase_de_desarrollo/ingreso_alimento_fecha_real_ingreso_inicial_ciclo_plan.md` (propuesta de OTRA
      sesión en curso) ni `.devpilot/events.jsonl`

### Aviso a la operación (fuera de alcance del código)
- [ ] Los lotes 2601 de Galpon-1 (id 2) y Galpon-2 (id 12) siguen en estado `Abierto`: cerrarlos POR
      PANTALLA (liquidar es una transacción de 5 pasos, no va por migración)
- [ ] El lote 12 arrastra apertura negativa (−9.020 kg): auditoría de datos aparte


---

# Tracker — Alimento previo al encaset: fecha real para contabilidad + «ingreso inicial del ciclo» (engorde y postura)

**Plan:** [fase_de_desarrollo/ingreso_alimento_fecha_real_ingreso_inicial_ciclo_plan.md](fase_de_desarrollo/ingreso_alimento_fecha_real_ingreso_inicial_ciclo_plan.md)
**Fecha:** 2026-08-07 · **Estado: PROPUESTA entregada, pendiente decisiones D1-D4 del usuario. SIN código.**

Pedido: el alimento llega 2-7 días antes del encaset; hoy la operación falsea la fecha al primer día de
consumo para que el seguimiento diario cuadre, y contabilidad pierde el día real de llegada.

## Fase 0 — Análisis y propuesta
- [x] Exploración con workflow de 5 agentes (fn engorde/ventana previa, módulo inventario, postura, encasetamiento, evidencia BD dump prod)
- [x] Diagnóstico: la ventana `dias_alimento_previo_encaset` YA absorbe el alimento previo en engorde pero es INVISIBLE (sin columna de apertura, sin documento, fila que «desaparece» al cargar el 1er seguimiento); postura no tiene NADA (el Reporte Contable además lo pierde por el `continue` de fechas sin dato del lote); `created_at` es la única fecha y la tipeada la pisa
- [x] Evidencia medida: Ecuador 110/110 ciclos con ingreso fechado el día 1 (workaround), Panamá 9/30 con fecha real 2-7 días antes; 28/75 ciclos encadenados EC con gap ≤ ventana (la fecha sola no atribuye)
- [x] ⚠️ Colisión identificada con la ventana de mes en curso (sesión paralela, sin commitear): bloquea la fecha real que cruza mes — conciliar D4 con esa sesión ANTES de que commitee
- [x] Plan/propuesta escrito (Partes A engorde / B inventario / C postura + D1-D4)
- [x] Revalidación pedida por el usuario («llega el 15, encaseto el 25»): SIMULADO en BD local con ROLLBACK — 10 días cae justo dentro de la ventana default y entra al saldo del día 1, pero INVISIBLE (ingreso 0, documento vacío); con 11 días el saldo BAJA en silencio al cargar el 1er seguimiento (10.000→6.800 medido). Ver §8 del plan

## Decisiones (aprobadas 07-ago — el usuario pidió arrancar con las recomendaciones)
- [x] D1 UNA fecha = la real + apertura visible como «ingreso inicial del ciclo»
- [x] D2 marca «para el próximo ciclo» en el ingreso (editable desde el historial)
- [x] D3 postura alcance mínimo (fix `continue` del Reporte Contable + fila de bultos con fecha real)
- [x] D4 excepción acotada a la ventana de mes (solo ingresos con encaset próximo en el galpón; el resto de la regla de 7339c61 intacta)

## Implementación (workflow multi-agente 08-ago: Opus código complejo · Sonnet código directo · QA final en Fable — 7 agentes, 0 errores)
- [x] B1 (opus) migración `20260808120000_AlimentoPrevioEncasetMarcaCiclo`: `para_proximo_ciclo` (mov + espejo + trigger CREATE OR REPLACE) + `registrado_at` (auditoría nunca pisada) + `PUT /ingresos/{id}/destino-ciclo` + excepción D4 solo en las 2 puertas de ingreso (con tope `dia <= hoy`, desvío documentado) + 22 tests. DDL probado en tx+ROLLBACK; espejos .sql actualizados
- [x] A2 (sonnet) `diasAlimentoPrevioEncaset` en CompanyDto/Create/Update + las 4 proyecciones (ToDto, Crud, CompanyResolver, CompanyPaisService) con clamp `NormalizarDias` + campo 0-30 en company-management (front)
- [x] C (opus) Reporte Contable: fila solo-bultos cuando la fecha tiene kardex sin dato del lote (`ReporteContableBultosCalculos` puro + 16 tests, acumulador legado como especificación ejecutable); semana 1 absorbe filas previas al encaset; gate 6 casos lote×fase — 5 con 0 diferencias y lote 13 Levante gana EXACTAMENTE la fila del bug (retiros 150,6375 del 10-abr que Producción ya mostraba)
- [x] A1+B2 (opus, high) fn v15: `apertura_alimento_kg`/`apertura_documentos` en la fila de fecha_min (DOUBLE PRECISION como sus hermanas; DROP FUNCTION previo porque cambia el RETURNS TABLE — pg_depend 0 dependientes) + override por marca con guarda anti-«dos ciclos después» + excepción fecha_min NULL (el flicker muere: los kg nunca desaparecen); migración `20260808130000` con Down=v14 verbatim; espejo C# + DTO; gate propio 5.804 filas/147 lotes 0 diferencias; apertura visible: Panamá 9 ciclos/70.030 kg (los que el plan predijo), Ecuador 2/7.200
- [x] B3 (sonnet) front inventario: checkbox «para el PRÓXIMO ciclo» (solo alimento+galpón, se resetea al cambiar destino) + badge/toggle en historial con ConfirmDialog+Toast + «capturado el» (registradoAt) + hint «Registrá la fecha REAL de llegada»
- [x] A3 (sonnet) front grilla engorde: badge «+X kg ingreso inicial (previo al encaset)» + documentos de apertura en la celda Documento, gateado por flag Y campo (levante/producción intactos)
- [x] QA (fable, high) — **VEREDICTO GO, cero defectos**: builds 0/0 · tests **2.091 + 1 verdes** · yarn build 0 errores · migraciones aplicadas y registradas en local (idempotencia probada sobre DDL pre-aplicado) · gate PROPIO v15 vs v14 (fn v14 reinstalada bajo nombre QA, EXCEPT doble NULL-safe): **0 diferencias en 5.804 filas / 197 lotes / 2 empresas** · cuadres sin regresión (2 descuadres PREEXISTENTES de datos: PA alimento lote 182, EC aves lote 132) · CRUD E2E a-f verde (escenario 15→25 con marca: ingreso 11 días antes que la v14 PERDÍA ahora abre con 3.000 kg y su factura; DELETE → espejo anulado; D4 200/400 correctos; clamp 45→30) · Reporte Contable K345 3.632.634 EXACTO · datos QA eliminados con 0 rastro, backend abatido
- [x] Commit acotado (sin footer de atribución)

### Hallazgo BLOQUEANTE aparte (NO tocado — tarea propia con su gate)
- `ObtenerDatosBultosAsync` pide PageSize=10000 pero `FarmInventoryMovementService.GetPagedAsync:447` clampa a **20** ⇒ el Reporte Contable solo ve los 20 movimientos de bultos más recientes de la granja (3 entradas históricas de granja 5 = 2.800 bultos invisibles). El fix C1 funciona para el caso real (alimento reciente) pero lo histórico queda estrangulado. Arreglarlo cambia números en muchos lotes ⇒ gate propio antes/después.

---

# Fix — Reporte Contable (postura): el kardex de BULTOS se estrangula en 20 movimientos

**Plan:** [`fase_de_desarrollo/reporte_contable_bultos_sin_tope_paginacion_plan.md`](fase_de_desarrollo/reporte_contable_bultos_sin_tope_paginacion_plan.md)
**Fecha:** 2026-08-08 · Bloque propio — no tocar desde otras sesiones
**Origen:** «Hallazgo BLOQUEANTE aparte» del QA de `801b14f` (bloque de arriba)

`ObtenerDatosBultosAsync` pide `PageSize = 10000` pero `FarmInventoryMovementService.GetPagedAsync:447`
clampa a **20** (`> 200 ⇒ 20`) ordenado por `created_at DESC`, y el filtro por `type_item='alimento'`
corre **en memoria después** de paginar ⇒ el reporte ve los 20 movimientos más recientes de la granja,
de cualquier ítem.

## Fase 0 — Diagnóstico (BD local, dump tipo-prod)
- [x] Estrangulamiento medido en granja 5 / lote 13 «K345A» — **peor de lo reportado**: el reporte veía
      **5 de los 58** movimientos de alimento de la granja y **CERO de sus 4 entradas**. Los 20 del tope
      son de la granja entera, así que 15 cupos se los comían ítems que no son alimento
- [x] Universo real: 4 entradas = 112.000 kg = **2.800 bultos** (2025-10-16 y 2026-01-09 de 1.250 c/u,
      2026-02-27 de 300) + 54 retiros = 20.528,900 kg = 513,2225 bultos
- [x] Plan escrito con paridad de filtros y criterio del gate

## Fase 1 — Gate ANTES (línea base congelada)
- [x] JSON de `GET /api/ReporteContable/generar` capturado para las 6 combinaciones lote×fase
      (backend propio :5499 vía `PORT`, JWT + X-Secret-Up minteados, usuario Admin de company 1)

## Fase 2 — Backend
- [x] C1 `ReporteContableBultosCalculos.RangoConsulta` (puro): ventana → `[desde, hasta+1d)`, corte
      superior **exclusivo** (created_at es timestamptz sin anclar a medianoche) y sin `.Date` sobre la
      columna (date_trunc usaría la zona de la SESIÓN)
- [x] C2 `ObtenerDatosBultosAsync` consulta `_ctx.FarmInventoryMovements` directo: granja + empresa +
      país + ítems de alimento + ventana, **todo traducido a SQL y sin tope**. El filtro por empresa pasa
      de condicional a incondicional (el método ya retornaba vacío sin `companyId` ⇒ fail-closed igual)
- [x] C3 Limpieza: fuera el parámetro `loteIds` (nunca se usaba) y la dependencia
      `IFarmInventoryMovementService`, que quedó sin ningún consumidor en el service (DI por
      `Program.cs:341`, sin `new` manual ⇒ sin impacto)

## Fase 3 — Tests (gate CI)
- [x] T1-T4 `RangoConsulta`: corte exclusivo al día siguiente (un movimiento de las 16:45 del último día
      ENTRA), normalización de hora, ventana de un día = 24 h, y `[Theory]` que verifica que el veredicto
      de la consulta **coincide con el de `GeneraFilaSoloBultos`** aguas abajo
- [x] Los 16 tests previos de `ReporteContableBultosCalculos` verdes sin tocarlos

## Fase 4 — Validación
- [x] `dotnet build` — 0 errores, 0 advertencias
- [x] `dotnet test` — **2.098 Application + 1 Domain verdes** (2.091 previos + 7 nuevos)
- [x] **Gate DESPUÉS: VEREDICTO GO.** Comparación campo a campo emparejando filas por clave
      (fecha+loteId), no por posición — el diff posicional inventaba 3 falsos «cambios de fecha» porque
      las filas nuevas corren los índices:
      - **4 controles negativos (granja 20): 0 diferencias, byte a byte**
      - lote 13 **Producción**: 333 campos modificados, **el 100 % columnas de bultos**, 0 filas nuevas
        (esas fechas ya tenían dato del lote, solo ganaron el kardex)
      - lote 13 **Levante**: 29 campos modificados, **todos de bultos**; 11 filas nuevas con **todas las
        columnas de aves en cero** (filas solo-bultos del feature C1) y 4 secciones semanales que nacen
        porque esa semana no tenía ninguna fila
      - **Invariante de aves: idéntico** — 374 (Levante) + 620 (Producción) + 300 (lote 116) agregados
        comparados, 0 cambios en entradas/mortalidad/selección/ventas/traslados/consumo ni en el saldo de
        ninguna fecha preexistente. Los 10 saldos «nuevos» son el saldo vigente arrastrado publicado en
        fechas que antes no tenían fila
- [x] **Trazabilidad exacta**: el kardex del reporte reproduce ahora las **6 fechas de la BD una a una**
      (1.250 / 1.250 / 300 entradas · 347,34 / 15,245 / 150,6375 retiros) en **ambas fases**;
      totales 2.800,0000 entradas y 513,2225 retiros = los 112.000 kg y 20.528,900 kg medidos en SQL.
      El **consumo NO se movió** (6.035,4875 Levante · 22.290,4025 Producción): viene de los seguimientos,
      no del kardex
- [x] Backend del smoke detenido (puerto 5499 libre), sin procesos huérfanos. BD **no modificada**
      (el gate son solo `GET` + `SELECT`)
- [x] Commit acotado (sin footer de atribución)

### Hallazgo aparte detectado al medir (NO tocado — requiere su propia auditoría de datos)
Los ítems de alimento de la **granja 20** (85, 89, 98, 99, 100) tienen `metadata->>'type_item'` **NULL**
⇒ el Reporte Contable no los reconoce como alimento y no cuenta **ninguno** de sus 236 movimientos, ni
antes ni después de este fix. Es un problema de **datos de catálogo**, no de este código — por eso esos
lotes sirvieron como control negativo. Decidir si se saneia el metadata o si el criterio pasa a
`farm_inventory_movements.item_type`.

---

# Fix — «Esto es alimento» vuelve a la columna + el clamp de paginación deja de degradar en silencio

**Plan:** [`fase_de_desarrollo/criterio_item_alimento_y_clamp_paginacion_plan.md`](fase_de_desarrollo/criterio_item_alimento_y_clamp_paginacion_plan.md)
**Fecha:** 2026-08-08 · Bloque propio — no tocar desde otras sesiones
**Continúa:** el fix `92cd918` (bloque de arriba). Pedido del usuario: implementarlo en todo, **encontrar
el factor donde sucede** y mejorarlo para que no vuelva a pasar.

Dos defectos que son el mismo: el Reporte Contable decidía «es alimento» leyendo
`metadata->>'type_item'`, el modelo VIEJO que ya nadie llena (NULL en el 80 %), en vez de la columna
`catalogo_items.item_type` (`NOT NULL`, 0 nulos, 3 índices) que nació para reemplazarlo; y el clamp
`pageSize > 200 ⇒ 20` está repetido en 3 servicios, con **7 pantallas del front pidiendo 1.000-2.000
ítems de catálogo y recibiendo 20**.

## Fase 0 — Diagnóstico
- [x] Las 3 fuentes del tipo de ítem medidas: columna `catalogo_items.item_type` **0 nulos de 435**
      con taxonomía completa · `metadata->>'type_item'` **NULL en el 80 %** · `farm_inventory_movements
      .item_type` poblada al **100 %**. `ReporteContableService` era el ÚNICO lector del jsonb en todo
      el backend (el front ya hace `item.itemType || item.metadata?.type_item`)
- [x] Causa raíz de la reincidencia: `CatalogItemService.CreateAsync` escribe la COLUMNA y **no** el
      metadata ⇒ todo ítem creado desde la UI moderna nacía invisible para el reporte
- [x] Impacto medido: **257 movimientos** invisibles (granja 20 entera = 236, granja 5 = 19, granja 87 = 2)
- [x] 🔴 **FACTOR identificado**: el clamp `pageSize > 200 ⇒ 20` degrada al MÍNIMO y está repetido en
      **3 servicios**; `CatalogItemService` lo tenía **activo**, con **7 pantallas del front pidiendo
      1.000-2.000 ítems y recibiendo 20** (`ajuste-form`, `conteo-fisico`, `kardex-list`,
      `traslado-form` y los modales de seguimiento de levante y producción, que además filtran por
      activo sobre esos 20)
- [x] Plan escrito

## Fase 1 — Gate ANTES
- [x] Línea base de 9 combinaciones lote×fase + smoke del factor. **Bug reproducido en vivo**:
      `catalogo?pageSize=1000` devolvía `items=20 / total=61 / pageSize=20`

## Fase 2 — Backend
- [x] C1 `ItemInventarioTipoCalculos` (puro): `EsTipoAlimento` tolerante a capitalización y espacios
      (el catálogo tiene filas «Alimento»; el resto del sistema ya comparaba así) + `TipoEfectivo`
      (manda el del movimiento, el catálogo respalda — patrón vigente en `FarmInventoryMovementService`)
- [x] C2 `ObtenerDatosBultosAsync` filtra por el tipo efectivo **dentro de la query**: desaparece el
      paso de traer el catálogo de la empresa a memoria (310 filas por llamada en Santa Reyes) y el
      filtro cae sobre columnas indexadas. Se conservan los filtros de empresa/activo del catálogo
- [x] C3 `PaginacionCalculos.NormalizarPageSize`: **pedir de más devuelve el TOPE, nunca el default**
- [x] C4 Los 3 servicios usan la normalización. Topes por naturaleza de la tabla: catálogo maestro
      **2.000** (máximo real 310, margen 6×) · movimientos y roles 200 (crecen sin techo). Las 7
      pantallas quedan arregladas **sin tocar una línea de frontend**
- [x] C5 `add_item_type_catalogo.sql` anotado: la columna es la fuente de verdad, el jsonb es VESTIGIAL

## Fase 3 — Tests (gate CI)
- [x] T1 `ItemInventarioTipoCalculosTests` — 33 casos: capitalización, los 9 tipos de la taxonomía real,
      «alimentos» no cuela por prefijo, precedencia movimiento/catálogo, y el caso exacto del bug
- [x] T2 `PaginacionCalculosTests` — 20 casos, incluido el que blinda el bug (`Assert.NotEqual(
      PageSizePorDefecto, size)` al pedir de más) y el que fija que el tope del catálogo cubre 310×6.
      ⚠️ Mi primer test estaba MAL formulado (esperaba que pedir 1.000 con tope 2.000 recortara a
      2.000): falló, y el corregido documenta que ese pedido pasa igual

## Fase 4 — Validación
- [x] `dotnet build` 0 errores / 0 advertencias nuevas · `dotnet test` **2.148 Application + 1 Domain**
      (2.098 previos + 50 nuevos)
- [x] **Gate DESPUÉS: VEREDICTO GO.**
      - **3 controles negativos (company 4, granjas sin movimientos): 0 diferencias byte a byte**
      - los 6 casos afectados: **todo campo modificado es de bultos**; las filas nuevas (3 a 18 por
        lote) tienen **todas las columnas de aves en cero**
      - las secciones semanales que nacen y los `fechaFin` que se corren un día quedaron **validados
        uno por uno**: cada fecha nueva corresponde a una fila de bultos real sin aves (p. ej.
        `fechaFin 2026-06-11 → 2026-06-12` porque esa semana ganó la fila de 106,7875 bultos de retiro)
      - **invariante de aves intacto**: 1.388 agregados comparados, 0 cambios
- [x] **Kardex == SQL, exacto en los 6 casos**: granja 5 → 3.913,8750 entradas / 755,2825 retiros ·
      granja 20 → 2.907,0000 / 2.608,6750, idénticos a la consulta con el criterio nuevo
- [x] **Smoke del factor**: `catalogo?pageSize=1000` pasa de `items=20` a **`items=61` (total=61)** ·
      `movimientos?pageSize=10000` pasa de 20 a **77** (`pageSize=200`, el tope)
- [x] Backend detenido (5499 y 5002 libres), sin procesos huérfanos. BD **no modificada** (solo GET/SELECT)
- [x] Commit acotado (sin footer de atribución)


---

# Auditoría de cierre — «alimento previo al encaset» + fix del chip (SOLO LECTURA, sin código)

**Fecha:** 2026-08-08 · Pedido del usuario: validar si el fix del chip quedó bien y qué falta cubrir.
**Método:** 5 lentes en paralelo + verificación adversarial de cada hallazgo (14 agentes, 39 hallazgos
crudos → 8 verificados → **7 confirmados, 1 refutado**).

## Veredicto sobre el chip (92cd918 + 8d5565c)
- [x] **Sin defectos propios.** `item_type` cubre los 2 valores reales (`alimento` 166 / `Alimento` 2),
      0 discrepancias columna vs jsonb, 0 movimientos con tipo contradictorio; `PaginacionCalculos`
      coherente en los 3 services; totales del commit reproducidos exactos en SQL; borrar `loteIds`
      fue correcto (no hay dato con qué filtrar: `galpon_destino_id` NULL en 326/326)
- [x] **Refutado** el único cargo contra el chip (supuesto corte en medianoche UTC): la agrupación por
      `CreatedAt.Date` ya existía y el camino nuevo ancla a MEDIODÍA UTC, que no cruza medianoche en
      ningún huso americano
- [x] Crítica válida: su gate comparó el reporte contra una consulta con **su mismo criterio**
      (auto-consistencia, no corrección), y al destapar 257 movimientos volvió MATERIALES 3 defectos
      preexistentes que estaban tapados

## Confirmados — pendientes de decisión del usuario
- [ ] 🔴 **§2.1 El saldo de bultos resta el consumo DOS VECES** (único número mal en pantalla HOY).
      El modal de seguimiento escribe un `Exit` de kardex `reason='Consumo diario'` con los MISMOS kg
      que graba en `consumo_kg_*`, y `AcumularSaldos` hace `− Retiros − ConsumoH − ConsumoM`.
      Verificado a mano: granja 87, 23-jun, 500 kg en el kardex Y 500 kg en el seguimiento.
      Escala: 253 movs / **131.778 kg**; 358 de 588 seguimientos caen el mismo día. Lo ven 9 roles.
      ⚠️ El arreglo NO va en `AcumularSaldos` (borraría el retiro legítimo de 3.280 kg): va aguas
      arriba (el `Exit` del modal, o excluirlos en `ObtenerDatosBultosAsync`). Falta el test con
      `Retiros>0` Y `Consumo>0` a la vez — los helpers actuales nunca ejercen la combinación
- [ ] 🟠 **§2.2 En postura el feature no entrega nada**: el Reporte Contable lee `farm_inventory_movements`
      y todo el feature escribe `inventario_gestion_movimiento`. **Sin puente** (0 triggers, la única
      pg_proc que la nombra solo LEE). Probado con ROLLBACK: el kardex queda 138 filas/146.260,5 kg
      antes y después. PREEXISTENTE (nace 2026-07-05 con la unificación Colombia), no lo introdujo el
      feature. Matiz: company 1 escribe en LOS DOS modelos (su último movimiento, 17-jul, fue al viejo
      por la ruta `/inventario` que sigue registrada sin guard de rol) ⇒ inventario partido sin puente
- [ ] 🟠 **§2.3a La excepción D4 es inalcanzable desde la UI**: backend + 184 líneas de test escritos,
      pero el front la bloquea en 3 lugares y **no existe endpoint** que exponga la ventana del galpón.
      El hint dice «Solo se admite el mes en curso» ⇒ instrucción activa a falsear la fecha.
      Afecta 39/110 encasets 2026 de Ecuador (35%) y 10/60 de Panamá. Ningún número sale mal: se
      pierde la fecha contable real, que es justo lo que contabilidad pidió
- [ ] 🟡 **§2.3b La marca rompe `fn_cuadre_alimento_engorde`** (A/B controlado: mismo ingreso, solo
      cambia el booleano ⇒ descuadre −5.000). CLAUDE.md declara que mover el cuadre de 0 es regresión.
      Matiz: no hay tablero (0 archivos en el front), es endpoint + LogWarning; transitorio salvo que
      el ciclo siguiente nunca arranque. **Impacto hoy: cero** (`para_proximo_ciclo` = 0 filas en BD)
- [ ] 🟡 **§2.3c Hueco de trazabilidad**: `fechas_universo` dejó el corte `>= fecha_corte_alimento`
      FUERA del disyunto de la marca ⇒ un ingreso marcado y fechado antes de `encaset−N` no genera
      fila en ningún lote hasta el primer seguimiento. Recrea el síntoma «el sistema se comió
      alimento» que motivó el feature. **Arreglo de UNA línea**, simétrico con `apert_mov`
- [ ] 🟡 **§2.4 Cada lote padre muestra el kardex de la GRANJA entera** (granja 20 tiene 4 padres ⇒ los
      4 reportes muestran los mismos 2.907 bultos; sumarlos da 11.628 vs 2.907 reales). Preexistente,
      no arreglable en la query (la tabla no tiene columna de lote). Peor: `AcumularSaldos` resta
      consumos POR LOTE de entradas POR GRANJA ⇒ el saldo no es ni de la granja ni del lote

## Verificado OK — no re-auditar
- [x] Infraestructura del feature: 242 migraciones BD = 242 código, 0 pendientes; trigger probado
      (marca TRUE → espejo TRUE) · espejo `.sql` **idéntico byte a byte** a la migración (63.459 chars)
- [x] **Gate multipaís sin regresión**: v13→v15 cambia EXACTAMENTE 32 filas (Ecuador, lotes 2 y 86,
      todas movimiento-only = lo que v14 declara); Panamá 747=747, **0 diferencias**. El descuadre vivo
      de Panamá es preexistente (reponiendo v13 da el mismo)
- [x] **Subir `dias_alimento_previo_encaset` a 30 en Ecuador es SEGURO** (simulado: 0 filas con saldo
      distinto en 5.804 filas/172 lotes, 0 negativos nuevos, cuadre sin cambios) — las guardas v11/v12
      lo contienen. Es la prueba que nadie había hecho antes de exponer el campo por pantalla
- [x] El caso testigo del usuario («llega el 15, encaseto el 25») **entra sin marca ni configuración**
- [x] Con marca: el ciclo siguiente abre en 5.000 kg; sin marca ese mismo ingreso dejaba al nuevo en
      **−300 kg**. El mecanismo nuclear funciona
- [x] Puntos ciegos que salieron limpios: las 88 granjas con `maneja_alimento_por_galpon` NULL heredan
      `true`; los 457 ingresos EC sin galpón son insumo/medicamento/gas (0 alimento) ⇒ el checkbox
      está en el 100% de los ingresos de alimento; editar/borrar un ingreso marcado conserva/anula bien
- [x] Sin datos de prueba de agentes anteriores en la BD (0 lotes/movimientos SIM/QA/TEST)

## No verificado (declarado)
- [ ] Descuadre persistido vs fn en Panamá (69 filas, hasta 23.355 kg): detectado, NO se determinó si
      necesita la migración `Recalcular…` que sí acompañó a v11 y v12 (este lote tocó la fn 2 veces sin ella)
- [ ] Los 31 hallazgos de severidad baja/informativa NO pasaron por verificación adversarial: son
      sospechas, no hechos


---

# v16 de engorde (coherencia de la marca `para_proximo_ciclo`) — INTENTADA Y **REVERTIDA**

**Fecha:** 2026-08-08 · Pedido: cerrar los 2 huecos §2.3b/§2.3c de la auditoría («fixes baratos»).
**Resultado: NO-GO tras 3 rondas. Nada commiteado; working tree y BD local restaurados a `362155c`.**

## Qué se intentó
Cerrar dos incoherencias de la marca `para_proximo_ciclo` (introducida en `801b14f`, **0 filas en uso**):
(a) `fn_cuadre_alimento_engorde` no conocía la marca ⇒ un movimiento marcado movía el cuadre de 0;
(b) `fechas_universo` dejaba el corte `fecha_corte_alimento` fuera del disyunto de la marca ⇒ un ingreso
marcado y fechado antes de `encaset−N` no aparecía en ninguna pantalla hasta el primer seguimiento.

## Por qué se revirtió — las 3 rondas, cada una con su contraejemplo reproducido en BD
1. **Ronda 1 (NO-GO ×2):** relajar el piso solo en `fechas_universo` hacía que la fila abierta por la marca
   volcara **todo el galpón-día** (13.000 kg por 5.000, con el alimento ajeno mostrándose a la vez en su
   propio lote); y un ingreso marcado se veía en **4 lotes** en vez de 1 (PA-67, 20.000 kg por 5.000) —
   o sea la v16 **empeoraba** la v15. Causa: el predicado «¿existe algún lote con primer seguimiento
   posterior?» no desempata entre lotes sin seguimiento, que son justo el caso de uso de la marca.
2. **Ronda 2 (NO-GO ×2):** con el criterio corregido a «ciclo destino = menor `fecha_encaset` posterior», el
   CTE `post` del cuadre quedó **sin cota inferior** ⇒ descontaba marcado histórico que el ciclo destino YA
   CONSUMIÓ (y por lo tanto ya no está en stock) ⇒ descuadre **+5.000 permanente**. Testigo: granja 37 /
   G0025 (cadena 53→70→189), mov 5.000 kg del 25-mar marcado, sin tocar stock: descuadrados 1→2; HEAD daba 0.
   Radio: **33 de 35 galpones de Ecuador** ya tienen ciclo anterior. Además, un marcado **sin encaset
   posterior** (= marcar antes de crear el lote siguiente, el flujo primario) quedaba invisible en el 100 %
   de las pantallas (v16: 0 lotes lo ven; HEAD: 4).
3. **Ronda 3 — veredicto final NO-GO:** con las 3 guardas nuevas del cuadre, el defecto se mudó al saldo:
   `pt_calc` acumula sobre **dos bases distintas dentro del mismo lote** (con y sin el piso `solo_marca`)
   ⇒ **6 de 59 galpones reales quedan con saldo NEGATIVO** en la tabla del lote destino, contra **0 de 59
   en HEAD** cambiando solo el booleano. Peor caso: granja 43 / G0055, ingreso de 5.600 kg ⇒ saldo **−8.840**.

## Lo que SÍ quedó probado (vale para el rediseño)
- **Identidad sin marcas siempre dio 0/0** en las 3 rondas (5.804 filas diaria, 61 cuadre, 172 aves, 224
  costos, 898 informe semanal, ambas empresas) ⇒ el gate de identidad **no puede ser la compuerta** de esta
  feature: con 0 marcas todo pasa siempre.
- **El desempate por `fecha_encaset` es el criterio correcto** (probado: 01-may→lote 121, 16-may→121,
  18-may→122) y cierra la multiplicación entre lotes sin seguimiento.
- **0 de 2.344 movimientos reales empeoran el cuadre** con la última versión — el problema que quedó vivo es
  del **saldo de la grilla**, no del cuadre.
- Topología «destino liquidado/congelado»: **no existe hoy** en la BD (búsqueda exhaustiva = 0).

## Aprendizajes de método (los caros)
- 🔴 **El que corrige no puede ser el que declara GO.** En la ronda 2 el agente de síntesis aplicó los fixes
  de las compuertas y se autoevaluó verde; la verificación independiente posterior encontró la regresión de
  los 6 galpones negativos.
- 🔴 **Tests en C# que no pueden construir la topología rota son falso verde.** Los 17 tests del primer
  intento pasaban CON los defectos adentro porque hardcodeaban `miPrimerSeguimiento: null`. `pt_calc` no
  tiene espejo C# y los `Calculos` no tienen llamador de producción ⇒ **la compuerta útil es SQL sobre datos
  reales**, con el invariante explícito «ninguna fila diaria queda negativa».
- El cuadre solo mira lotes CON seguimiento ⇒ es **ciego al lote destino recién creado**, que es justo donde
  aparecieron los negativos.

## Estado dejado (verificado)
- [x] Working tree limpio (`git status` solo `.devpilot/events.jsonl`, ajeno)
- [x] BD local restaurada: ambas fns reinstaladas desde HEAD (0 rastros de `marca_efectiva`/`marca_propia`/
      `marca_destino`, `apertura_alimento_kg` presente = v15 correcta), `ix_lote_hist_para_proximo_ciclo`
      dropeado, registro `20260808140000` borrado de `__EFMigrationsHistory` (última = `20260808130000`)
- [x] La columna `para_proximo_ciclo` y su trigger **NO se tocaron** (son de `20260808120000`, commiteada)
- [x] Cuadre en línea base: 1 descuadrado preexistente (Panamá lote 182) · 0 movimientos marcados
- [x] `dotnet build` Application y Infrastructure 0/0 · build servers apagados
- [x] El intento archivado en el scratchpad de la sesión (`intento_v16_modificados.patch` + los 3 archivos
      nuevos) por si sirve de base al rediseño

## Antes de reintentar — DECISIÓN DE PRODUCTO pendiente (no es código)
Qué debe hacer la marca cuando: (1) el galpón tiene **dos ciclos conviviendo**; (2) el lote destino queda
**liquidado** antes de consumir el alimento; (3) alguien marca y **nunca crea** el ciclo siguiente.
Sin esa definición, cada guarda nueva mueve el defecto de lugar (pasó 3 veces).
⚠️ Los 2 huecos originales siguen abiertos, con **impacto cero mientras nadie use la marca**.


---

# Rediseño de la marca `para_proximo_ciclo` — v16 con ENTREGA al ciclo siguiente

**Plan:** [`fase_de_desarrollo/marca_proximo_ciclo_rediseno_plan.md`](fase_de_desarrollo/marca_proximo_ciclo_rediseno_plan.md)
**Fecha:** 2026-08-08 · Bloque propio — no tocar desde otras sesiones
**Continúa:** el bloque «v16 de engorde … INTENTADA Y REVERTIDA» (commit `d6aeccb`). Ahora **sí** hay
decisión de producto: las 3 reglas de negocio (R1 conviven / R2 liquidación / R3 sin destino) las definió
el dueño del producto el 08-ago-2026 y son la especificación.

**Cambio de modelo (no es una cuarta guarda):** el diferimiento deja de ser un **borrado** de la fila de
ingreso y pasa a ser una **ENTREGA** —salida sintética en el último día visible del ciclo cedente, topada
por su propio saldo— que el ciclo destino recibe en su apertura. *La marca nunca quita kg de una pantalla
si no hay, en el mismo acto, otra pantalla que los reciba.* Con eso: R3 se cumple por construcción (nada
desaparece nunca), no pueden nacer filas negativas (un solo delta, en el último día, topado), `pt_calc`
conserva **una sola base**, y **`fn_cuadre_alimento_engorde` no se toca** (demostración en §2.4 del plan:
el cedente jamás es el ciclo activo que mira el cuadre).

## Fase 0 — Plan (STEP 1) · HECHO
- [x] Exploración leída (3 lentes: convivencia / liquidación / corrección) y cruzada con el código de HEAD
- [x] Topología de los 7 galpones testigo **verificada en la BD local** (solo lecturas, sin escrituras):
      37/G0025 `53→70→189` · 37/Galpon-11 `25→44→85` · 43/G0055 `57→16→86→193` · 96/PA-67 4 lotes **sin
      seguimiento y sin movimientos** · 105/G0491, 105/G0492, 106/G0479, 106/G0490 conviven
- [x] Confirmado: **0 marcas** en `lote_registro_historico_unificado` y en `inventario_gestion_movimiento`;
      el índice `ix_lote_hist_para_proximo_ciclo` **no existe** (quedó dropeado en la reversión)
- [x] Plan escrito con: 3 reglas → 5 decisiones de diseño justificadas por dato · semántica completa en
      **11 casos** (ninguno termina en «no se ve en ningún lado») · fases · compuerta · 12 casos de prueba
      con galpones reales · qué no se toca

## Fase 1 — NÚCLEO · **entra AHORA** (pendiente de implementar)
- [ ] F1.1 `backend/sql/fn_alimento_marcado_atribucion.sql` — dueña ÚNICA de la atribución (destino por
      `fecha_encaset` mínima posterior, cedente por `fecha_encaset` máxima anterior, convivencia por solape,
      tope, estado) + índice parcial `ix_lote_hist_para_proximo_ciclo`
- [ ] F1.2 `fn_seguimiento_diario_engorde` **v16**: revertir a v14 las 4 exclusiones de v15 (líneas 615,
      761, 790, 826) · `apert_mov` por `lote_destino_id` · CTE `entrega_ciclo_siguiente` + tope · marca solo
      en ENTRADAS · guardas de destino sin seguimiento / destino congelado / cedente sin seguimiento /
      `d >= destino.prim_seg`. **La firma NO cambia ⇒ `CREATE OR REPLACE`, sin `DROP FUNCTION`**
- [ ] F1.3 Espejo C#: `SaldoAlimentoEngordeCalculos` (reescribir `EntraPorMarcaProximoCiclo`, reemplazar
      `ExcluidoDeFilaDiariaPorMarca`) + `SeguimientoAvesEngordeCalculos` (líneas 100, 164, 228)
- [ ] F1.4 Recálculo al **cruzar el umbral**: primer seguimiento de un lote en un galpón con marcados ⇒
      `SaldoAlimentoEngordeAplicador.RecalcularPorUbicacionAsync`
- [ ] F1.5 Decisión registrada: **el cuadre NO se toca** en Fase 1 (la prueba es del gate, no del fix)
- [ ] F1.6 3 migraciones EF idempotentes (índice · helper · fn v16) + espejo `.sql` **byte a byte**
      (un `.sql` cambiado sin migración queda MUERTO)

## Fase 2a — Visibilidad barata (R3) · **entra AHORA**
- [ ] F2a.1 Columna «Próx. ciclo» en el tab **Histórico** de Gestión de Inventario (el backend ya la
      devuelve en `InventarioGestionService.cs:1806`; el front no la pinta en ninguna de sus 15 columnas)
- [ ] F2a.2 Verificar en pantalla la fila de **entrega** (etiqueta y signo) en la grilla de engorde

## Fase 2b — Bandeja de alimento reservado · **NO entra ahora**
- [ ] Endpoint + pantalla con `estado`/`motivo` del helper y corrección en línea (el
      `PUT /ingresos/{id}/destino-ciclo` ya existe). Se difiere: R3 ya queda cumplido por la Fase 1

## Fase 3 — Señalamiento de la anomalía R2 · **NO entra ahora**
- [ ] F3.1 Columnas informativas en el cuadre (`marcado_no_diferible_kg`, `liquidado_con_saldo_kg`)
      ⚠️ cambia el `RETURNS TABLE` ⇒ exige `DROP FUNCTION` y toca una fn con 5 consumidores
- [ ] F3.2 Reporte «liquidados con alimento sin trasladar» — hoy **24 de 84 (28,6 %), 111.821 kg**
- [ ] F3.3 Bug del aviso de liquidación: el fallback a stock de **núcleo**
      (`modal-liquidacion-lote-engorde.component.ts:375-383`) muestra stock de galpones vecinos —
      **7 de 11 galpones de SAN GUILLERMO** avisan con 19.160 kg ajenos
- [ ] F3.4 `GET /api/CuadreAlimentoEngorde` no tiene **ningún** consumidor en el front

## Compuerta (el gate manda; los 4 aprendizajes de las rondas fallidas van adentro)
- [ ] **G0 — identidad SIN marcas: NECESARIA, JAMÁS SUFICIENTE.** Las 3 rondas dieron 0/0 siempre, incluida
      la que producía negativos. `verificar_paridad_saldo_engorde.sql` antes/después, las 5 fns, ambas
      empresas. **Nadie declara GO con esto**
- [ ] **G1 — A/B con la marca PRENDIDA sobre movimientos REALES** (`backend/sql/verificar_marca_proximo_ciclo.sql`,
      nuevo, LF): censo de los ~59 galpones / ~2.344 movimientos, `SAVEPOINT` por movimiento, `ROLLBACK` y
      verificación de 0 rastro
- [ ] I1 **ninguna fila diaria negativa** = 0 en todo el universo (ronda 3: 6 de 59)
- [ ] I2 **conservación suma cero** por galpón (apertura + filas) invariante vs HEAD
- [ ] I3 **visibilidad R3**: 0 movimientos marcados invisibles
- [ ] I4 **no multiplicación**: mismo número de ciclos que lo cuentan, con y sin marca (ronda 1: 4 lotes)
- [ ] I5 **cuadre** sin alejarse de 0 · línea base 61 filas, 1 preexistente (Panamá lote 182) (ronda 2: +5.000)
- [ ] I6 **R1 convivencia**: `dif_saldo` = 0,00 en los 4 pares (10.699,52 · 17.761,52 · 1.576,47 · 19.393,56)
- [ ] I7 **rendimiento** del cuadre ≤ 1,5× la línea base
- [ ] **G3 — tests C# que CONSTRUYEN las topologías** (los 17 del primer intento pasaron con los defectos
      adentro por hardcodear `miPrimerSeguimiento: null`): los 11 casos de la tabla + **prueba de mutación
      registrada** (comentar cada guarda ⇒ el test tiene que ponerse rojo)
- [ ] **G4 — el que corrige NO declara GO**: el gate lo lee una sesión que no escribió la v16

## Casos de prueba con galpones reales (veredicto esperado escrito de antemano)
- [ ] P1 96/PA-67 (4 lotes sin seguimiento) ⇒ NEUTRO, idéntico a HEAD · P2/P3 los 4 pares que conviven ⇒
      `dif_saldo` 0,00 · P4 37/G0025 `id 6337`/`6245` ⇒ DIFERIDO limpio · P5 `id 13266` anulado ⇒ inerte ·
      P6 37/Galpon-11 `id 9087` ⇒ NEUTRO sin destino · **P7 43/G0055 `id 14047` (04-ago, 5.600 kg) ⇒ NEUTRO:
      es el testigo del −8.840 de la ronda 3** · P8 salidas 0173…0188 ⇒ IGNORADA_NO_ENTRADA · P9 `id 7189`
      ⇒ DIFERIDO_PARCIAL topado · P10 destino congelado (construido en tx) ⇒ NEUTRO · P11 cruce de umbral ⇒
      refresco del saldo persistido · **P12 granja 42/G0049 lote 132, 7.000 kg doc `005-001-000063560` ⇒ la
      fila conserva `ingreso 7.000 / saldo 11.260 / documento`** (regresión E1 de la auditoría)

## No se toca (y por qué)
- [x] `fn_cuadre_alimento_engorde` (fórmula) — no lo necesita y tocarlo fue el error de la ronda 2; tiene
      que seguir siendo el detector **independiente**
- [x] Rama congelada (84 fotos) · columna `para_proximo_ciclo` + trigger (ya commiteados) ·
      `vw_seguimiento_pollo_engorde` (Power BI, reimplementación aparte — divergencia documentada) ·
      ventana D4 `dias_alimento_previo_encaset` (§2.3a, otro feature) · descuadre persistido de Panamá
      (69 filas, preexistente) · decidir por país/empresa (anti-patrón prohibido por CLAUDE.md)
- [x] `ReporteContableService.cs`, `ReporteContableBultosCalculos.cs`, `FarmInventoryMovementService.cs`,
      `CatalogItemService.cs`, `.devpilot/` — **sesiones paralelas**

---

# v16 de engorde — FASE 1 IMPLEMENTADA: la marca `para_proximo_ciclo` ENTREGA en vez de borrar

**Plan:** [`fase_de_desarrollo/marca_proximo_ciclo_rediseno_plan.md`](fase_de_desarrollo/marca_proximo_ciclo_rediseno_plan.md)
**Fecha:** 2026-08-09 · Bloque propio — no tocar desde otras sesiones
**Continúa** el bloque «Rediseño de la marca `para_proximo_ciclo` — v16 con ENTREGA al ciclo siguiente»
(Fase 0 = plan). Base: HEAD `d6aeccb`. **Esta sesión NO commitea** (lo hace el orquestador).

## Qué quedó implementado

- [x] **F1.1** `backend/sql/fn_alimento_marcado_atribucion.sql` (NUEVO, 543 líneas) — dueño único de la
      atribución. Dos funciones: `fn_alimento_base_cedente_engorde(INT)` (el TOPE: último día visible
      del cedente + su saldo ahí) y `fn_alimento_marcado_atribucion(INT,TEXT,TEXT)` (el veredicto por
      movimiento) + el índice parcial `ix_lote_hist_para_proximo_ciclo`
- [x] **F1.2** `fn_seguimiento_diario_engorde` **v16**: las 4 exclusiones de v15 revertidas a v14 y la
      marca convertida en dos términos **ADITIVOS** — `+kg_diferido` en la apertura del DESTINO y
      `−kg_diferido` como `traslado_salida_kg` del CEDENTE en su último día visible
- [x] **F1.3** espejo C# `Application/Calculos/AtribucionAlimentoMarcadoCalculos.cs` (NUEVO) +
      `SaldoAlimentoEngordeCalculos` y `SeguimientoAvesEngordeCalculos` **revertidos a v14** (la marca
      ya no los toca) + 33 tests nuevos que CONSTRUYEN las topologías
- [x] **F1.4** cruce de umbral: `SaldoAlimentoEngordeAplicador.RecalcularVecinosSiHayAlimentoMarcadoAsync`,
      llamado desde los dos services de seguimiento (carga masiva y formulario Ecuador)
- [x] **F1.5** **el cuadre NO se tocó** — ni una línea de `fn_cuadre_alimento_engorde`
- [x] **F1.6** 2 migraciones EF idempotentes con el SQL **byte a byte** de los `.sql`:
      `20260809120000_FnAlimentoMarcadoAtribucionEngorde` y
      `20260809120100_FnSeguimientoEngordeV16EntregaCicloSiguiente` (Down = v15 VERBATIM, Designer
      clonado del último real, **ModelSnapshot intacto**)
- [x] `backend/sql/verificar_marca_proximo_ciclo.sql` (NUEVO, 566 líneas, LF) — el gate ejecutable

## El cambio de modelo, en una línea

`apert_mov`, `hist_full`, `hist_alimento`, `docs_por_fecha` y `fechas_universo` vuelven a la forma de
**v14 exacta**. La marca no quita nada de ninguna parte: agrega una **fila de entrega** al cedente y un
**crédito de apertura** al destino, por los mismos kg. Por eso R3 («invisible» nunca es una respuesta)
pasa de condición a vigilar a **propiedad estructural**, y una fila negativa es imposible por
construcción (un solo delta, en el último día, topado por el saldo propio).

## 🔴 DOS DEFECTOS QUE ENCONTRÓ EL GATE Y QUE NO ESTABAN EN EL PLAN

1. **La entrega recibida movía el CIERRE del destino.** `saldo_close` → `rango_final.fecha_max` se
   alimenta de la apertura; al sumarle el crédito, el ciclo destino cerraba más tarde, **ampliaba su
   ventana visible** y absorbía movimientos que no eran suyos. Medido: **37 probes con la conservación
   rota, hasta 14.320 kg**. Fix: `saldo_running` usa `apertura_alimento_base` (v14) y solo `pt_calc` y
   la columna expuesta usan la apertura efectiva. Es la misma asimetría que ya obliga a dejar la
   entrega fuera de `hist_full` (si entrara, movería la fecha donde ella misma se escribe).
2. **Diferir alimento que el ciclo cedente estaba consumiendo descuadra el ciclo activo.** En
   **43/G0055** el lote 86 (seg 02-jun→18-jul) cierra con **1.100 kg «de saldo»**, pero el stock físico
   del galpón (**4.540 kg**) coincide EXACTO con el saldo del ciclo activo 193 ⇒ esos 1.100 kg son un
   **fantasma contable** (la anomalía R2 que ya existe a escala: 24 de 84 liquidaciones congelaron con
   saldo > 0). Entregarlos movía `fn_cuadre_alimento_engorde` de **1 → 2 galpones descuadrados** en los
   17 probes de ese galpón — la firma exacta de la ronda 2. Fix: guarda nueva
   **`NEUTRO_DENTRO_DEL_CEDENTE`** (`d <= cedente.ult_seg` ⇒ la marca es inerte).

⚠️ **La guarda 2 se aparta del plan**: el caso de prueba **P4** (37/G0025, `id 6337` del 19-may dentro
del rango del lote 70) esperaba `DIFERIDO` y ahora da `NEUTRO_DENTRO_DEL_CEDENTE`. Se eligió el
invariante del cuadre por encima del veredicto escrito. **Consecuencia a decidir por producto:** el
feature queda acotado al ingreso que cae en el **HUECO entre ciclos** —que es el caso que el propio
plan identifica como el real (39 de 110 encasets 2026 de Ecuador, §9.3)— y NO cubre el alimento que
llega mientras el lote anterior sigue en seguimiento.

## Semántica final: 17 estados, ninguno deja kilos invisibles

`DIFERIDO` · `DIFERIDO_PARCIAL` · `IGNORADA_ANULADO` · `IGNORADA_NO_ENTRADA` · `NEUTRO_SIN_DESTINO` ·
`NEUTRO_SIN_CEDENTE` · `NEUTRO_CEDENTE_SIN_SEGUIMIENTO` · `NEUTRO_DESTINO_SIN_SEGUIMIENTO` ·
`NEUTRO_CONVIVENCIA` · `NEUTRO_DENTRO_DEL_DESTINO` · `NEUTRO_DESTINO_LIQUIDADO` ·
`NEUTRO_CEDENTE_LIQUIDADO` · `NEUTRO_YA_VISIBLE_EN_DESTINO` · `NEUTRO_DENTRO_DEL_CEDENTE` ·
`NEUTRO_CEDENTE_SIN_CIERRE` · `NEUTRO_FUERA_DEL_CEDENTE` · `NEUTRO_SIN_RESPALDO`

Tres estados **no anticipados por el plan** y por qué existen:
- `NEUTRO_CEDENTE_LIQUIDADO`: una foto congelada no se reescribe ⇒ la entrega no se escribiría y el
  destino recibiría kg sin contraparte (suma ≠ 0).
- `NEUTRO_YA_VISIBLE_EN_DESTINO`: si el movimiento ya entra a la apertura natural del destino (v11+v12),
  diferirlo lo contaría **dos veces**. Es lo que mantiene la conservación exacta en 0,00.
- `NEUTRO_DENTRO_DEL_CEDENTE`: el defecto 2 de arriba.

## Resultados del gate (BD local, dump tipo prod, todo en tx con ROLLBACK)

**G0 — identidad SIN marcas (necesaria, jamás suficiente).** `EXCEPT ALL` bidireccional, las dos
empresas, las 5 fns: **0 / 0 en todas**.
`fn_seguimiento_diario_engorde` 5.804 filas · `fn_cuadre_alimento_engorde` 61 · `fn_cuadre_aves_engorde`
172 · `fn_reporte_diario_costos_engorde` 224 · `fn_informe_semanal_pollo_engorde` 898.

**G1 — censo con la marca PRENDIDA.** `backend/sql/verificar_marca_proximo_ciclo.sql`,
**1.406 movimientos / 64 galpones**, tres fases:

| | Fase A (BD tal cual) | Fase B (sin congeladas) | Fase C (ingreso sintético en el hueco) |
|---|---|---|---|
| I1 filas negativas nuevas | **0** de 1.406 | **0** de 1.406 | **0** de 17 |
| I2 conservación, desvío máx. | **0,0000 kg** | **0,0000 kg** | **0,0000 kg** |
| I3 marcados que se vuelven invisibles | **0** | **0** | **0** |
| I4 documento en más lotes sin diferir | **0** | — | — |
| I5 cuadre | **no se movió** en ningún probe | — | **1 → 2 sin marca, 2 → 1 CON marca** |
| I6 convivencia (4 pares) | `dif_saldo` **0,00** con y sin marca | — | — |

- Línea base del cuadre re-medida: **61 filas, 1 descuadrado preexistente (Panamá, lote 182)**.
- Filas diarias ya negativas en HEAD: **91** — I1 mide filas negativas **nuevas**, no el total.
- **I7 rendimiento:** `fn_cuadre_alimento_engorde(NULL)` **0,62 s** (v16) vs **0,49 s** (HEAD) = **1,27×**
  (umbral 1,5×).
- Rastro al terminar: **0 marcas** en `lote_registro_historico_unificado` y en
  `inventario_gestion_movimiento`; **0 filas** del ingreso sintético.

**🔴 Lo que el censo NO puede demostrar, y por eso existe la fase C.** En el dump local **ningún
movimiento real** cae en la ventana que habilita `DIFERIDO` (después del último seguimiento del cedente
y antes de la ventana de apertura del destino): las fases A y B terminan con **0 probes DIFERIDO**, así
que por sí solas prueban que la marca *no rompe nada*, no que la entrega *funcione*. La fase C inyecta
el ingreso que falta (3.000 kg) en 17 pares secuenciales reales, bombeando también
`inventario_gestion_stock`, y compara el MISMO movimiento con el booleano en `FALSE` y en `TRUE`.
El único par con respaldo (**43/G0055, 86 → 193, 19-jul**) da exactamente lo diseñado:

| | sin marca | con marca |
|---|---|---|
| saldo final del cedente 86 | 4.100 kg | **1.100 kg** (entregó 3.000) |
| `apertura_alimento_kg` del destino 193 | 0 kg | **3.000 kg** |
| galpones descuadrados | **2** (el bug: el stock subió y el ciclo activo no lo ve) | **1** (= línea base) |
| conservación / filas negativas nuevas | — | **0,00 kg / 0** |

**G3 — tests C# que construyen las topologías.** `AtribucionAlimentoMarcadoCalculosTests` (NUEVO, 33
tests) con un helper que arma un **galpón completo** (ciclos con encaset, primer y último seguimiento,
congelación, ventana) y el estado del cedente como dato ⇒ se pueden expresar «destino sin seguimiento»,
«cedente sin respaldo», «destino liquidado», «ciclos que conviven». `dotnet test`: **2.168 pasan, 0
fallan**. Prueba de mutación registrada más abajo.

**Builds:** `dotnet build` Application **0/0**, Infrastructure **0/0**. `ModelSnapshot` sin tocar.

## Prueba de mutación (G3) — comentar cada guarda y ver el test en rojo

Se comentó cada guarda nueva, se corrió `dotnet test` y se verificó que los tests se ponen ROJOS.
Una guarda cuyo test sigue verde al quitarla no está testeada. **12 de 12 en rojo, 0 falsos verdes:**

| guarda comentada | resultado |
|---|---|
| R1 convivencia (`Conviven`) | 🔴 1 test falla |
| caso 10 · `d >= destino.PrimerSeg` | 🔴 1 |
| caso 5 · destino congelado | 🔴 1 |
| caso 5b · cedente congelado | 🔴 1 |
| Option F · ya visible en la apertura del destino | 🔴 1 |
| anti-abuso · `d <= cedente.UltimoSeg` | 🔴 1 |
| `d > baseCedente.FechaMax` | 🔴 1 |
| caso 3 · destino sin seguimiento | 🔴 1 |
| caso 9 · cedente sin seguimiento | 🔴 1 |
| caso 8 · solo entradas de alimento | 🔴 2 |
| caso 7 · movimiento anulado | 🔴 1 |
| tope · piso en 0 | 🔴 1 |

Script reproducible: el de la sesión comenta el fragmento, corre los tests y restaura el fuente.

## Estado dejado en la BD local

- [x] `fn_alimento_base_cedente_engorde`, `fn_alimento_marcado_atribucion`,
      `ix_lote_hist_para_proximo_ciclo` y `fn_seguimiento_diario_engorde` v16 **instalados**
- [x] `__EFMigrationsHistory` **NO se tocó a mano** (última sigue siendo `20260808130000`): las dos
      migraciones nuevas son idempotentes y las aplica EF sola al levantar el backend
- [x] **0 marcas** y **0 filas sintéticas**: todo el gate corre en transacción con `ROLLBACK`
- [x] Tablas temporales de línea base (`tmp_*`) eliminadas · sin procesos vivos

## Lo que NO entra en esta fase (y sigue pendiente)

- [ ] **Fase 2a** — columna «Próx. ciclo» en el tab Histórico (el backend ya la devuelve en
      `InventarioGestionService.cs:1806`; el front no la pinta) y verificación en pantalla de la fila
      de entrega
- [ ] **Fase 2b** — bandeja de alimento reservado (el helper ya devuelve `estado` y `motivo` listos
      para la UI)
- [ ] **Fase 3** — señalamiento de la anomalía R2 (columnas informativas en el cuadre, reporte de
      liquidados con alimento sin trasladar, el falso positivo del aviso de liquidación)
- [ ] **Mensaje del endpoint** `ActualizarDestinoCicloAsync`: sigue con texto fijo; debería reportar el
      estado resuelto por el helper («se difiere al lote X» / «queda reservado»)
- [ ] **Decisión de producto** sobre `NEUTRO_DENTRO_DEL_CEDENTE` (ver el ⚠️ de arriba)

## G4 — el que corrige NO declara GO

Esta sesión **escribió** la v16, así que **no declara GO**. El gate lo tiene que ejecutar y leer una
sesión que no la escribió: `psql ... -f backend/sql/verificar_marca_proximo_ciclo.sql`.

## VEREDICTO DE LA RONDA 4: **NO-GO — REVERTIDA** (y la marca queda DESHABILITADA en la UI)

El gate lo corrieron dos verificadores independientes (ninguno escribió la v16) y un juez sin permiso
de editar. **C1 = NO-GO · C2 = GO-CON-RESERVAS · juez = NO-GO.** La diferencia entre los dos: C1 abrió
la **foto congelada** de la liquidación y C2 no.

### Lo que SÍ mejoró respecto de las 3 rondas previas
- **Filas negativas nuevas por la marca: 0** (0 de 64 galpones reales, 0 de 75 pares sintéticos, 0 de
  2.210 movimientos). El invariante que hundió la ronda 3 quedó cerrado.
- **Cuadre vs HEAD: 0 movimientos empeoran** (A/B uno a uno: 0 peor · 729 mejor · 1.481 iguales).
- **Los tests MUERDEN**: 14/14 mutantes muertos, 0 sobrevivientes (el predicado viejo pone 4 en rojo).
- **R1 convivencia: CUMPLE** — 4 pares reales, 29 movimientos marcados, 113 filas, `EXCEPT ALL` 0 y 0.

### Por qué igual es NO-GO: el handoff se parte al liquidar
- 🔴 **Liquidar el CEDENTE esconde kilos**: tras una entrega válida (apertura destino 3.000, descuadre
  0,00), congelar el cedente —el procedimiento normal de R2— flipea a `NEUTRO_CEDENTE_LIQUIDADO`:
  apertura del destino 3.000→0, cuadre 0,00→−3.000, y la foto congelada del cedente sigue diciendo
  «Entrega al ciclo siguiente, salida 3.000». **3.000 kg reales sin ninguna tabla diaria viva.** (R3 ✗)
- 🔴 **Liquidar el DESTINO los duplica**: Σ galpón 8.640→11.640 (**+3.000 kg creados**) con
  `descuadre_kg = 0,00 en ambos estados` ⇒ **el detector es ciego**. HEAD no puede producir esto.
- **Causa raíz**: la atribución es un veredicto **recalculado en lectura** sobre estado mutable, pero la
  liquidación congela **un solo** extremo ⇒ el handoff se parte. El rediseño correcto es **persistir la
  atribución como hecho** (cedente, destino, kg, fecha) en el momento de marcar.
- Alcance: **0 movimientos DIFERIDO** en 1.680 marcados reales ⇒ la Fase 1 verde mide un no-op; el único
  par que alcanza el estado es justo el que rompen los dos bloqueantes.

### 🔴 HALLAZGO QUE OBLIGA A ACTUAR SOBRE `801b14f` (lo ya commiteado)
Bajo **HEAD/v15**, marcar un movimiento **rompe la conservación en 729 de 2.210 casos reales**
(hasta **37.467 kg** que desaparecen de toda tabla diaria) y HEAD produce **208 filas negativas**.
Motivo: los 4 guards de la fn (`hist_full`, `hist_alimento`, `docs_por_fecha`, `fechas_universo`) le
quitan el movimiento a **TODO** lote con seguimiento —incluidos los que **CONVIVEN** con el destino— y
en esos galpones ninguna apertura lo vuelve a tomar. **El checkbox ya estaba en producción.**

- [x] **Mitigación commiteada**: el checkbox del alta se oculta (`mostrarParaProximoCicloIngreso` ⇒
      `false`) y el historial solo permite **QUITAR** una marca existente, nunca poner una nueva
      (`puedeMarcarDestinoCiclo` exige `paraProximoCiclo === true`). La columna, el endpoint, el badge y
      la migración `20260808120000` **quedan intactos**: se apaga la puerta de entrada, no la feature.
      Regla del dueño del producto respetada: el alimento marcado nunca queda invisible ni sin corregir.
- [x] `yarn build` (Node portable 22.23.1) — 0 errores, único warning el de bundle budget preexistente

### Reversión (verificada)
- [x] Working tree: `git checkout -- backend` + borrados los untracked del intento
      (`fn_alimento_marcado_atribucion.sql`, `verificar_marca_proximo_ciclo.sql`,
      `AtribucionAlimentoMarcadoCalculos.cs` + test, migraciones `20260809120000_*` y `20260809120100_*`).
      **Se conservan** el plan `marca_proximo_ciclo_rediseno_plan.md` y este bloque del tracker
- [x] BD local: fn diaria reinstalada desde HEAD (`DROP` + `CREATE`, cambió el `RETURNS TABLE`) ·
      `fn_alimento_marcado_atribucion` y `fn_alimento_base_cedente_engorde` dropeadas ·
      `__EFMigrationsHistory` **NO se tocó** (las migraciones nuevas nunca se registraron) · el índice
      `ix_lote_hist_para_proximo_ciclo` **NO se tocó** (otra sesión lo estaba creando)
- [x] Verificado: 0 marcados · 0 fns auxiliares · 0 rastros en la fn (`DIFERIDO`/`NEUTRO_`/`cedente`) ·
      `apertura_alimento_kg` presente (= v15 correcta) · última migración `20260808130000` ·
      cuadre 61 filas / 1 descuadrado (el preexistente de Panamá)

### Lo que queda para el rediseño (con las 3 reglas ya definidas por el usuario)
- [ ] **Persistir la atribución como hecho** en el momento de marcar (cedente, destino, kg, fecha), en
      vez de recalcularla en lectura: es la única forma de que la liquidación de un extremo no parta el
      handoff. Es un cambio de modelo de datos, no una guarda más
- [ ] Arreglar los 4 guards de la fn para que respeten R1 (un lote que **convive** con el destino debe
      seguir viendo el movimiento). El predicado ya existe en el archivo: es el de `lotes_ajenos` (v11)
      aplicado al destino en vez de a mí
- [ ] Fase 2 (visibilidad/corrección R3) y Fase 3 (señalamiento de la anomalía R2) del plan

---

# PWA F1 — shell instalable, autoactualizable y con kill switch

**Plan:** [fase_de_desarrollo/pwa_f1_shell_plan.md](fase_de_desarrollo/pwa_f1_shell_plan.md)
**Contexto:** F0.C cerrada (`76a2903`), F0.B parcial (`f139dfd`). El borde ya sirve `ngsw.json`,
`ngsw-worker.js`, `safety-worker.js` y `manifest.webmanifest` con `no-cache` — pero el Service Worker
nunca existió. Esta entrega es la F1 del plan madre.

⛔ **Fuera de alcance, explícito:** escritura offline (outbox/push). Sigue bloqueada por F0.A/F0.B
(sin idempotencia, sin concurrencia, sin tombstones en el backend).

## Shell y build
- [x] `@angular/service-worker` en `package.json` (versión alineada a Angular 22)
- [x] `ngsw-config.json` — assetGroups `app` (prefetch) + `assets` (lazy); **sin `dataGroups`**
- [x] `angular.json` — `serviceWorker` en `production` y `docker`; manifest y safety-worker como assets
- [x] `provideServiceWorker` con `!isDevMode()` + `registerWhenStable:30000`
- [x] `scripts/verificar-ngsw.js` — el build falla si un SHA1 de `ngsw.json` no coincide con el disco
- [x] `Dockerfile` copia `ngsw-config.json` y corre el verificador; `.dockerignore` con la lista blanca al día

## Instalabilidad
- [x] `manifest.webmanifest` (name, short_name, start_url, display standalone, theme/background)
- [x] Iconos 192/512 `any` + 192/512 `maskable` + apple-touch 180, generados por script reproducible
- [x] `index.html` — link al manifest, `theme-color`, metas de iOS

## Ciclo de vida
- [x] `PwaActualizacionService` con `SwUpdate` + banner (sin recarga forzada) + fallback `version.json`
- [x] `VersionCheckService` **eliminado** (dos autoridades de recarga = bucle)
- [x] `ConexionService` (online/offline) + indicador
- [x] `PwaInstalacionService` (`beforeinstallprompt`) + botón de instalar
- [x] `safety-worker.js` — desregistra y limpia CacheStorage, **NO toca IndexedDB** + `make pwa-panic`

## Diagnóstico
- [x] `/diagnostico` sin `authGuard`, sin datos de negocio: build, estado del SW (safe mode incluido),
      `storage.estimate()`, persistencia, caches

## Validación
- [x] Tests Karma de las funciones puras (`decidirActualizacion`, `formatearBytes`, `resumirEstadoSw`)
- [x] `yarn build` 0 errores · `yarn test` verde
- [x] Pruebas en vivo sobre build de producción servido en localhost: SW activo, manifest, iconos,
      **red cortada**, 404 de asset inexistente, kill switch

## Resultado de las pruebas en vivo (build de producción servido en :4400 con las reglas de nginx)

Servidor: `frontend/scripts/servir-pwa-local.js` (replica no-cache de control, 404 de assets y
fallback solo en navegaciones). `localhost` es contexto seguro ⇒ el SW se registra sin HTTPS.

- [x] SW registrado (`ngsw-worker.js`, scope `/`) y **controlando** tras la segunda carga ·
      114 recursos del shell + 3 de assets precacheados (~9 MB)
- [x] Manifest 200 con `application/manifest+json`, `display: standalone`, `theme #F5821F`;
      los **4 iconos** declarados resuelven 200
- [x] `ngsw.json` / `ngsw-worker.js` / `safety-worker.js` / `version.json` → 200 `no-cache`
- [x] `/chunk-que-no-existe.js` → **404**, no el index (criterio §9 del plan madre)
- [x] **Servidor APAGADO** ⇒ `/diagnostico` (ruta lazy) carga completa desde la caché del SW
- [x] Banner "Sin conexión" aparece al evento `offline`
- [x] **Ciclo de actualización real**: `prepare → build → emit → verificar-ngsw` ⇒ el banner
      aparece solo, se aplica con el botón, el bundle cambia (`main-5R4LC3MN` → `main-6VNNBUWV`)
      y el `buildId` en pantalla queda **igual al de `/version.json`**. Sin bucle de recarga
- [x] **Kill switch** (procedimiento exacto de `make pwa-panic`): 0 registros de SW, 0 cachés,
      y la base IndexedDB de prueba **INTACTA** — la regla que protege el futuro outbox
- [x] Recuperación tras el kill switch: el SW vuelve a registrarse y activarse solo
- [x] Consola sin errores (el único 404 es el provocado a propósito)

### 🔴 Hallazgo del gate en su primera corrida
`verificar-ngsw.js` falló apenas se escribió, con un SHA1 divergente en `/safety-worker.js`:
**`@angular/build` escribe su propio `safety-worker.js` ENCIMA del asset, después de haberlo
hasheado para `ngsw.json`**. Es exactamente el modo de falla que el gate existe para atrapar —
se habría desplegado una imagen que arranca perfecto y deja el SW en **safe mode silencioso**.
Resuelto eliminando la copia propia (la de Angular ya hace `unregister()` + borra solo cachés
`ngsw:` y nunca toca IndexedDB) y excluyendo `safety-worker.js` y `worker-basic.min.js` de los
`assetGroups`: el kill switch no debe servirse desde la caché del SW que viene a matar.

## Fuera de alcance (explícito, no pendiente de pulir)
Escritura offline (outbox + push). Bloqueada por F0.A/F0.B: el backend no tiene idempotencia,
ni control de concurrencia, ni tombstones, y los saldos son contadores read-modify-write con
`Math.Max(0,…)` (no reversibles). Al cerrar F1 la app es una PWA instalable cuyo **shell** anda
sin red; los **datos** siguen requiriendo conexión. Documentado en `frontend/PWA.md`.

---

# F0.A · A1 + A2 — el stock de inventario deja de perder escrituras

**Plan:** [fase_de_desarrollo/f0a_stock_atomico_plan.md](fase_de_desarrollo/f0a_stock_atomico_plan.md)
**Contexto:** items A1 y A2 de `pwa_offline_first_plan.md` §4.A. **Son bugs de producción de HOY**,
reproducibles con dos pestañas; el offline solo los multiplicaría por N dispositivos. Prerrequisito
de F2/F3.

## Medición previa (local, refresh del dump de prod)
- [x] 539 filas de stock · **0 grupos duplicados** · **0 FKs** apuntando a `stock.id` ⇒ el índice
      único se puede crear y consolidar no rompe nada

## A1 — clave natural única
- [x] Migración `AddStockClaveNaturalUnica`, idempotente: consolida duplicados (suma, se queda
      `MIN(id)`) **antes** de crear el índice. Sin esto, duplicados vivos en prod harían fallar la
      migración al arrancar el contenedor (`RunMigrations=true`) → exit 139 / rollback silencioso
- [x] Índice único de **expresión** con `COALESCE(nucleo_id,'')`/`COALESCE(galpon_id,'')`: sin el
      COALESCE, `NULL <> NULL` deja duplicarse todo el modelo a nivel granja (Colombia + granjas con
      `maneja_alimento_por_galpon = false`)
- [x] Se conserva el índice no único existente (el de expresión no resuelve las igualdades de las
      consultas ⇒ quitarlo sería regresión de plan)
- [x] Upsert `INSERT ... ON CONFLICT DO UPDATE` en los 4 sitios de buscar-o-insertar

## A2 — descuento atómico
- [x] `UPDATE ... SET quantity = quantity - @q WHERE id = @id AND quantity >= @q`; **0 filas = rechazo**
- [x] Aplicado en los 4 sitios de read-modify-write (consumo, traslado misma granja, tránsito
      inter-granja, distribución)
- [x] Lecturas previas al descuento pasan a `AsNoTracking()` (que el tracker no reescriba la fila)
- [x] Transacción explícita solo si no hay una ambiente (`CurrentTransaction is null`)

## Validación
- [x] Tests xUnit de la lógica pura
- [x] Pruebas SQL en transacción + ROLLBACK: consolidación, rechazo del índice único (incluido el
      caso NULL), UPDATE condicional con saldo suficiente e insuficiente
- [x] `dotnet build` + `dotnet test`
- [x] Cuadre de alimento de engorde sin moverse de 61 filas / 1 descuadrado (Panamá preexistente)

## Pruebas de concurrencia REALES (dos sesiones psql simultáneas contra la BD local)

Es el punto: los dos defectos son carreras, así que probarlos de a una operación no prueba nada.

- [x] **A2 — descuento.** Fila con saldo **150**, dos consumos concurrentes de **100**:
      sesión A → `UPDATE 1` · sesión B → **`UPDATE 0`** (se bloqueó en el lock de fila de A y al
      liberarse reevaluó el `WHERE` contra el valor nuevo) · **saldo final 50**.
      Con el código anterior los dos pasaban la validación en C# y el saldo quedaba en **−50**.
- [x] **A1 — inserción.** Dos upserts concurrentes sobre la misma clave natural (40 + 40):
      resultado **1 fila con 80**, en vez de dos filas de 40 con una invisible.
- [x] Datos de prueba borrados; la tabla vuelve a **539 filas**.

## Validación
- [x] DDL probado en transacción + ROLLBACK con duplicados sembrados: consolidación (2 grupos,
      3 filas absorbidas), rechazo del índice con ubicación **y a nivel granja** (`NULL,NULL`),
      `UPDATE` condicional (1 fila con saldo / 0 sin saldo), idempotencia
- [x] `dotnet build` 0 errores · `dotnet test` **2.163 verdes** (12 nuevos de `StockAtomicoCalculos`)
- [x] Migración aplicada en local: índice creado, 539 filas intactas, 0 duplicados
- [x] Cuadre de alimento de engorde **61 filas / 1 descuadrado** — idéntico al estado previo
      (el descuadre preexistente de Panamá)

## ⚠️ Brecha que queda ABIERTA, a propósito
Los dos métodos **a nivel granja de Colombia** (`RegistrarConsumoNivelGranjaAsync` /
`RegistrarIngresoNivelGranjaAsync`) **NO se hicieron atómicos**. Su contrato dice explícitamente
*«NO SaveChanges/tx aquí: el orquestador externo commitea»*, y de sus cuatro llamadores, **tres**
(`ProduccionService.Seguimiento`) abren transacción pero el de **carga masiva**
(`MigracionService.AlimentoPostura:131`) no. Con escritura diferida eso hoy funciona; con SQL
inmediato, el descuento se auto-commitearía y el movimiento quedaría pendiente ⇒ **ventana de
escritura parcial nueva**. Cerrarla requiere primero envolver el camino de carga masiva en su propia
transacción. Se deja anotado en vez de introducir un modo de falla que este cambio no puede verificar.

---

# PWA F2 — consulta offline

**Plan:** [fase_de_desarrollo/pwa_f2_consulta_offline_plan.md](fase_de_desarrollo/pwa_f2_consulta_offline_plan.md)
**Contexto:** F1 (`8ecb7c6`) dejó la app instalable con el shell sin red, pero toda pantalla con
datos queda vacía sin conexión. **Riesgo de integridad: cero** — es solo lectura.

## Capa de datos
- [x] `shared/offline/offline-db.ts` — IndexedDB con **migraciones acumulativas**
      (`for v = oldVersion+1..newVersion`; un salto v1→v3 debe correr los dos pasos)
- [x] `claveParticion` **fail-closed**: `{userId}|{companyId}|{paisId}|{método} {url}`; sin alguno
      de los tres ⇒ no se lee NI se escribe (degradar a clave parcial es cómo se filtra entre empresas)
- [x] `decidirCacheable`: **lista blanca** de endpoints operativos + solo GET. Excluidos a propósito
      `ReporteDiarioCostos*`, `ReporteContable`, `DbStudio`, `Auth`, `Users`, `Roles`, `session` (D3)
- [x] `vigenciaCache`: TTL duro de 16 h (jornada offline de D4); vencida ⇒ **no se sirve**

## Integración
- [x] Interceptor: red primero, caché **solo** ante `status === 0`
- [x] Purga de la partición en logout y en cambio de empresa
- [x] Aviso en la UI de que se está viendo una consulta guardada
- [x] Estado de la caché en `/diagnostico`

## Validación
- [x] Tests Karma de las 3 funciones puras + migración acumulativa de IndexedDB
- [x] `yarn build` + `yarn test`
- [x] En vivo: con red guarda · **sin red sirve** · sin caché previa error normal · cambio de
      empresa y logout purgan

## Hallazgo del chequeo de cobertura (el que justificó escribir el script)

La lista blanca escrita "a ojo" cubría **23 de los 78** endpoints que la app realmente pide, y tenía
**7 entradas que no existen**, una de ellas un typo (`lotepostorabase` por `loteposturabase`). Ese
modo de falla es silencioso: no rompe el build, no rompe ningún test, y el único síntoma es que esa
pantalla no anda sin red — cosa que no se descubre en la oficina, se descubre en la granja.

`scripts/verificar-lista-cacheable.js` contrasta la lista contra los `${environment.apiUrl}/X` del
código. Estado final: **50 cacheables · 28 excluidos con motivo escrito · 0 sin decisión · 0 fantasma**.
No falla el build a propósito: "¿este módulo tiene que andar sin red?" es una decisión de producto,
no algo que un script resuelva. Lo que impide es dejar un endpoint sin mirar.

## Pruebas
- [x] **Integración con IndexedDB REAL** en Chrome (`offline-cache.interceptor.spec.ts`): con red
      guarda · **sin red sirve lo guardado** · sin nada guardado propaga el error · un **500 NO se
      tapa** con caché · endpoint fuera de la lista blanca ni se guarda ni se sirve · la caché de
      **otra empresa no se sirve** · purga por logout y por cambio de empresa · fail-closed sin identidad
- [x] `yarn build` 0 errores (solo el budget preexistente) · `yarn test` **199 verdes** (155 → 199)
- [x] `verificar-ngsw.js` OK — sigue sin `dataGroups`
- [x] En vivo: la base `italgranja-offline v1` se crea sola, `/diagnostico` muestra la sección
      "Consultas guardadas", y con el **servidor apagado** la app carga y el banner de sin conexión aparece

### Gotcha que costó una vuelta
El primer intento de la suite murió con **7 timeouts**. La causa no estaba en las pruebas:
`CacheConsultasService` deja su conexión a IndexedDB abierta, y **una conexión abierta bloquea
indefinidamente `deleteDatabase`**, así que la limpieza entre pruebas colgaba y Jasmine culpaba a la
prueba. Se agregó `cerrarConexion()` al servicio (útil también para recrear el esquema en caliente) y
las esperas fijas se cambiaron por sondeo con tope — un sleep calibrado en esta máquina es un test
intermitente en el CI.

## Lo que sigue para la captura offline (F3)
Sigue bloqueada por F0.A/F0.B. Hechos: **A1 y A2** (`44b2400`). Pendientes: A3-A10 y B1/B4/B5/B6/B8/B10.

---

# F0.A — auditoría del estado real + A5 (lápidas de borrado)

**Auditoría:** [fase_de_desarrollo/f0a_auditoria_estado_2026-08-09.md](fase_de_desarrollo/f0a_auditoria_estado_2026-08-09.md)

## Auditoría: el inventario del plan madre estaba desactualizado en 3 de 10 ítems
Verificado contra las funciones/triggers **vivos** en la BD y grep sobre `backend/src`, no contra el plan.

- [x] **A1, A2** — hechos (`44b2400`, esta sesión)
- [x] **A3** — ya estaba hecho por **otra sesión** (migración `20260806074742`): la rama UPDATE del
      trigger ya corre el saldo **por delta** y no lo pisa
- [x] **A8** — ya estaba hecho: `InventarioGestionConsumoRequest.FechaMovimiento` existe
- [x] **A10** — ya estaba hecho: **0 triggers** en `seguimiento_diario_produccion` y no existe
      ninguna función `%espejo%huevo%`
- [x] **A4 — el plan pide algo que hoy ROMPERÍA el número.** El síntoma es real (un `GET` escribe
      `aves_*_actual` y bumpea `updated_at`), pero `AvesHActual` tiene **6+ escritores incrementales**
      y ese "self-heal" recalcula desde `fn_seguimiento_diario_produccion`: hoy **es lo que mantiene la
      columna bien**. Sacarlo dejaría a todos leyendo la deriva. La corrección correcta es el patrón
      `SaldoAlimentoEngordeAplicador` (la fn como única autoridad), con gate de paridad
- [x] **A6 — requiere medir antes de tocar.** Hay **dos** únicos redundantes, ambos por `lote_id`.
      Cambiar un índice único por lo que dice un plan sin verificar la colisión con datos es lo que
      la regla de schema de CLAUDE.md prohíbe
- [x] **A9 — pendiente y es zona minada.** Confirmado que sigue con `ORDER BY … DESC LIMIT 1` sin
      filtro de vida del lote. Es el mismo terreno donde la ventana de alimento previo rompió Ecuador
      y donde la marca «próximo ciclo» se intentó 4 veces y se revirtió. Exige el gate de paridad
      multipaís antes de tocarla

## A5 (primera parte) — lápidas de borrado, sin cambiar comportamiento
- [x] Migración `20260810031057_AddSyncTombstones`: tabla `sync_tombstones` + función genérica
      `trg_sync_tombstone()` + trigger `AFTER DELETE` en las **4 tablas operativas**
- [x] **Puramente aditivo**: sin soft delete, sin filtro global, sin una línea de C# que lo lea. Los
      borrados siguen funcionando igual — ahora además dejan constancia
- [x] Se guardan **solo claves de negocio** (lote, fecha, ubicación, ítem), nunca la fila entera:
      guardar la fila sería una copia paralela de datos operativos que nadie audita
- [x] DDL probado en transacción + ROLLBACK: lápida creada, el borrado saca **exactamente 1 fila**,
      `company_id`/`farm_id` capturados donde existen, idempotente
- [x] Aplicada en local y probada **en vivo**: 4 triggers activos; borrar un seguimiento de levante
      deja `clave={"fecha":…, "lote_id":"123", "lote_postura_levante_id":15}`
- [x] `dotnet build` 0 errores · `dotnet test` **2.163 verdes** · cuadre de engorde **61/1**
      (sin moverse) · stock 539 filas

**Por qué se despliega ahora aunque nadie lo lea:** lo que se borra sin dejar lápida **no se puede
reconstruir después**. Cuando exista la sincronización, ya va a haber historia de borrados en vez de
arrancar de cero.

## Orden recomendado para lo que queda
**A5 (2ª parte: soft delete)** → **A7** (consolidar los dos escritores de levante) → **A6** (medir
primero) → **A4** (aplicador + gate de paridad) → **A9** (último, con gate multipaís y en horario de
baja operación).

---

# F0.A · A7 — una sola regla de saldo de aves para levante

**Contexto:** item A7 de [f0a_auditoria_estado_2026-08-09.md](fase_de_desarrollo/f0a_auditoria_estado_2026-08-09.md).

## El defecto, confirmado leyendo los tres caminos
`SeguimientoDiarioService` escribía la fila pero **no** movía el saldo de levante en `Update`/`Delete`;
lo hacía el módulo (`SeguimientoLoteLevanteService.Crud.cs`) **después** de llamarlo. Resultado:

| Camino | Editar / borrar mortalidad de levante | Saldo de aves |
|---|---|---|
| Módulo de levante | ✅ | se movía |
| `PUT`/`DELETE /api/SeguimientoDiario` | ✅ | **quedaba intacto** |
| Módulo `LoteSeguimiento` | ✅ | **quedaba intacto** |

O sea: la fila corregida y el saldo mintiendo. Producción **sí** lo hacía bien dentro del service —
la asimetría era solo de levante.

## Lo hecho
- [x] `UpdateAsync` aplica el delta de levante **revirtiendo lo viejo y aplicando lo nuevo**, simétrico
      con el bloque de producción que ya existía
- [x] `DeleteAsync` devuelve las aves (`RestaurarAvesLevanteAsync`) en **los dos caminos**, incluido el
      de traslado, y **dentro de la transacción** para que una falla del borrado se lleve la devolución
- [x] Las **4** aplicaciones duplicadas del módulo de levante eliminadas (si no, se descontaría dos veces)
- [x] Código muerto borrado: `DescontarAvesEnLotePosturaLevanteAsync` y
      `AjustarAvesEnLotePosturaLevanteAsync` (grep: 0 llamadas restantes)
- [x] `DescuentoAvesSeguimientoCalculos` (puro) + **18 tests xUnit**

## La prueba que hace que esto sea un refactor y no un cambio de comportamiento
El módulo aplicaba el **delta neto** (`viejo − nuevo`) y el service **revierte y reaplica**. El test
`RevertirYAplicarEsIgualAlDeltaNeto` fija que las dos formas dan el mismo saldo en 9 escenarios,
**clamp incluido** (saldo en cero, viejo mayor que el saldo, nuevo mayor que el saldo…). Sin esa
equivalencia, mover la regla habría sido cambiar números históricos.

Queda además fijado por test que el `Math.Max(0, …)` hace la operación **no reversible** (descontar 10
sobre un saldo de 3 deja 0, y revertir deja 10, no 3) — es una de las razones por las que F3 sigue
bloqueada.

## Validación
- [x] `dotnet build` 0 errores · `dotnet test` **2.181 verdes** (2.163 → 2.181)
- [x] Cuadre de alimento de engorde **61 filas / 1 descuadrado**, sin moverse
- [x] Grep: queda **exactamente un** aplicador del saldo de levante

## ⚠️ Lo que NO se probó
**No se corrió un smoke HTTP** de los tres endpoints: `PlatformSecretMiddleware` exige el header
`X-Secret-Up` cifrado y montarlo no era rápido. La afirmación *"ahora los tres caminos mueven el
saldo"* está verificada por lectura del código y por los tests de la aritmética, **no** por una
corrida punta a punta. Antes de desplegar conviene: editar y borrar un seguimiento de levante por cada
uno de los tres caminos y verificar `lote_postura_levante.aves_h_actual` en cada paso.
