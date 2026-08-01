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
