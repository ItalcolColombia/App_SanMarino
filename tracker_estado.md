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
