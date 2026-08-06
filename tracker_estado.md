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
