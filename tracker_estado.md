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
