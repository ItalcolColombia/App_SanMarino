# Tracker — Recepción de tránsito con distribución en varios galpones

**Plan:** [`fase_de_desarrollo/recepcion_transito_distribucion_galpones_plan.md`](fase_de_desarrollo/recepcion_transito_distribucion_galpones_plan.md)
**Fecha:** 2026-07-26

Objetivo: al aceptar un traslado de **alimento** en la pestaña **Tránsito**, poder **distribuir la cantidad
recibida entre varios galpones** de la granja destino (hoy solo se recibe en uno). Ítems no alimento y granjas
con inventario a nivel granja **no cambian**.

---

## Backend

- [x] `InventarioGestionDtos.cs`: `InventarioGestionRecepcionDestinoDto` + `Distribucion` opcional en el request + `InventarioGestionRecepcionTransitoResultDto`
- [x] `Application/Calculos/InventarioGestionRecepcionDistribucionCalculos.cs` (NUEVO, puro)
- [x] `IInventarioGestionService.RegistrarRecepcionTransitoAsync` → devuelve el result DTO
- [x] `InventarioGestionService.RegistrarRecepcionTransitoAsync`: delega al cálculo + persiste N stocks / N movimientos
- [x] Validación de pertenencia (núcleo, galpón) a la granja destino en el camino distribuido
- [x] **Fix** `GetTrasladosAsync`: `ToDictionaryAsync` por `TransferGroupId` revienta con N entradas → agrupar
- [x] `InventarioGestionController`: respuesta aditiva `{ destino, movimiento, destinos, movimientos }`

## Tests (backend)

- [x] `InventarioGestionRecepcionDistribucionCalculosTests.cs` (NUEVO) — 24 casos
- [x] Camino clásico (sin distribución) con mensajes byte a byte idénticos
- [x] Suma exacta / suma incorrecta / duplicados / cantidad ≤ 0 / fila incompleta / nivel granja / filas vacías / tolerancia

## Frontend

- [x] `gestion-inventario.service.ts`: tipos `InventarioGestionRecepcionDestino` + `distribucion?` en el request
- [x] Componente: estado `recepcionDistribuir` + `recepcionDestinos[]` (alta/baja de filas, núcleo→galpón en cascada)
- [x] Componente: totales (distribuido / total / faltante) y validación espejo del backend
- [x] Componente: envío del payload con `distribucion`
- [x] HTML: toggle un galpón / varios galpones + tabla de destinos + contador
- [x] SCSS: estilos de la tabla de distribución (tokens existentes)

## Validación

- [x] `cd backend && dotnet build` — 0 errores, 0 advertencias
- [x] `cd backend && dotnet test` — 861/861 verdes
- [x] `cd frontend && yarn build` — 0 errores (solo el warning preexistente de bundle budget)
- [x] Smoke API local (JWT minteado, empresa ItalcolEcuador): traslado 1.000 kg 43→40 recibido **400/350/250** en G0040/G0041/G0042 → 3 stocks + 3 movimientos, tránsito cerrado, reintento rechazado
- [x] Smoke API: rechazos correctos (suma que no cuadra, galpón repetido, galpón de otra granja, cantidad 0) y **recepción clásica** en un galpón sin cambios (1 destino, reason original)
- [x] Regresión pestaña **Traslados** con N entradas por grupo → HTTP 200 (antes hubiera reventado el `ToDictionary`)
- [x] Smoke UI en dev server (sesión inyectada): toggle, precarga del restante al agregar fila, contador «Cuadra con la cantidad en tránsito», guardado real 400/320 verificado en BD, consola sin errores
- [x] BD local revertida al estado original y servidores detenidos (sin procesos huérfanos)

---

# Tracker — Tab «Huevos» en Seguimiento Diario Levante (semana 14+) y arrastre automático a Producción al liquidar

> ♻️ **Bloque RESTAURADO** (2026-07-26). Otra sesión lo sobrescribió al aplicar la regla vieja de «borrar todo el tracker».
> Su trabajo está **terminado y validado pero SIN COMMITEAR** (46 archivos en el working tree), así que su estado no se puede perder.

**Plan:** [fase_de_desarrollo/huevos_levante_semana14_arrastre_produccion_plan.md](fase_de_desarrollo/huevos_levante_semana14_arrastre_produccion_plan.md)

**Decisiones del usuario:** flag por empresa (`captura_huevos_en_levante`) · sin huevos en reportes de levante en esta fase · «Huevos iniciales (al cerrar)» pasa a readonly con el total calculado.

## Fase 0 — Análisis
- [x] Exploración exhaustiva (workflow 8 agentes + síntesis) del esquema, CRUD levante/producción, liquidación, front de ambos modales, fórmulas de semana, impacto en reportes
- [x] Verificación directa de los 4 hallazgos críticos: las 13 columnas de huevos YA existen en `seguimiento_diario_levante`; el mapper de levante manda 15 `null`; `MovimientoAvesCalculos.SemanaDesdeEncaset` es la fórmula canónica; `SeguimientoLoteLevante` es letra muerta (sin DbSet)
- [x] Plan escrito en `fase_de_desarrollo/`

## Fase 1 — Backend: cálculo puro + tests (gate CI)
- [x] `Application/Calculos/HuevosLevanteCalculos.cs` (semana 14, `HuevosClasificacion`, Sumar/Delta, peso ponderado, marca en metadata)
- [x] `tests/ZooSanMarino.Application.Tests/HuevosLevanteCalculosTests.cs` — 42/42 verdes
- [x] `dotnet test` verde — 825/825 en toda la suite

## Fase 2 — Backend: flag de empresa
- [x] `Company.CapturaHuevosEnLevante` + `CompanyConfiguration` (`captura_huevos_en_levante`, default false)
- [x] Propagado en las 5 proyecciones (`CompanyDto`, `CompanyService.ToDto`, `CompanyService.Crud`, `CompanyResolver`, `CompanyPaisService`) + CreateCompanyDto/UpdateCompanyDto
- [x] Migración `20260726231137_AddCapturaHuevosEnLevanteCompany` (idempotente, `ADD COLUMN IF NOT EXISTS`)
- [x] Migración data-only `20260726231200_SeedCapturaHuevosEnLevante` (Sanmarino, `IS DISTINCT FROM`)
- [x] Aplicadas en BD local :5433 y verificadas (Sanmarino=t, resto=f)

## Fase 3 — Backend: captura de huevos en levante (semana 14+)
- [x] DTOs: `CreateSeguimientoLoteLevanteRequest` + `SeguimientoLoteLevanteDto` (13 campos opcionales al final)
- [x] `Mapeos.cs`: destapados los 15 `null` (Create + Update) y lectura en `MapToLevanteDto`
- [x] `Crud.cs`: gate semana 14 + flag por empresa (fail-closed por `farms.company_id`), `null` = conservar en Update
- [x] Blindaje de pérdida silenciosa en `SeguimientoDiarioService` (`teneManualExist`, `FilaSinContenido`, `MergearManualSobreTrasladoAsync`)
- [x] `dotnet build` 0 errores / 0 warnings nuevos

## Fase 4 — Backend: arrastre al liquidar + SUMA el mismo día
- [x] `IArrastreHuevosLevanteService` + `ArrastreHuevosLevanteService` (suma en BD, upsert con delta por marca, espejo)
- [x] Enganche transaccional en `LotePosturaLevanteService.CerrarLoteYCrearProduccionAsync`
- [x] Total de huevos en `GetResumenCierreAsync` (`CierreLoteLevanteResumenDto`)
- [x] Rama de merge en `ProduccionService.CrearSeguimientoAsync` (sólo filas marcadas ⇒ el 400 actual intacto)
- [x] DI en `Program.cs`
- [x] `dotnet build` + `dotnet test` verdes (0/0 warnings · 825/825)

## Fase 5 — Frontend
- [x] `capturaHuevosEnLevante` en `CompanyFlags` + `FLAGS_APAGADOS` (fail-closed)
- [x] `funciones/` puras: `totales-huevos-levante`, `semana-vida-levante` + `README.md`; `models/huevo-levante.model.ts`
- [x] `modal-create-edit`: 3er tab «Huevos», 12 controles, auto-cálculo memoizado, payload, rehidratación, reset
- [x] `seguimiento-lote-levante-list`: `[fechaEncaset]` bindeado, total readonly en el modal de cierre, toast en el error
- [x] `modal-detalle-seguimiento`: sección «Huevos»
- [x] `services/seguimiento-lote-levante.service.ts`: campos de huevos en los DTOs TS
- [x] `yarn build` 0 errores (sólo el warning de bundle budget preexistente)

## Fase 6b — Bug encontrado durante la validación (bloqueaba el merge)
- [x] `date_trunc('day', timestamptz)` trunca en la zona de la SESIÓN de la BD ⇒ `x.Fecha.Date == fecha.Date` en LINQ **nunca casaba** con una sesión no-UTC (la local es `America/Bogota`): el chequeo de duplicado de producción no encontraba la fila y el merge no se disparaba (creaba una 2ª fila del mismo día)
- [x] `FechasPuras.RangoDiaUtc` (puro, 4 tests nuevos) + aplicado en los 2 chequeos de duplicado de `ProduccionService` y en el lookup de `ArrastreHuevosLevanteService` — rango semiabierto UTC, correcto en cualquier zona y sargable
- [x] Efecto colateral (mejora): en una BD con sesión no-UTC el 400 por duplicado de producción ahora **sí** se detecta (antes se colaba una fila por día repetida)

## Fase 6 — Validación y cierre
- [x] Smoke API local con JWT minteado — **58/58 verdes** (20 captura en levante + 38 liquidación/arrastre)
- [x] Smoke UI en dev server con sesión inyectada — borde exacto día 90/91, totales, guardado real persistido, modal de cierre readonly, flag OFF sin cambios, consola limpia
- [x] Servidores detenidos (sin procesos huérfanos) + BD local restaurada al estado original
- [ ] Commit — **pendiente de confirmación** (46 archivos listos, working tree sin commitear)
- [x] Verificado contra la regla de `CLAUDE.md` (Angular 22 = OnPush por defecto): los 3 componentes tocados declaran `ChangeDetectionStrategy.Eager` explícito

---

# Tracker — Venta Pollo Engorde: peso diferido en Panamá + carga masiva completa

**Plan:** [fase_de_desarrollo/venta_engorde_panama_peso_diferido_y_carga_masiva_plan.md](fase_de_desarrollo/venta_engorde_panama_peso_diferido_y_carga_masiva_plan.md)
**Fecha:** 2026-07-26

**Decisiones del usuario:** carga masiva **multi-lote** con despacho/factura y peso prorrateado · **corregir** la idempotencia de `fn_migracion_venta_engorde` (rango de día + `numero_despacho`) · peso diferido en **ambos sentidos** (cargar al confirmar **y** corregir venta ya `Completada`).

## Fase 0 — Análisis
- [x] Exploración exhaustiva (workflow 7 agentes, 336 lecturas) de venta Panamá, peso/prorrateo, front de ventas, carga masiva, flags por empresa y ciclo de vida en BD
- [x] Verificación directa de los hallazgos críticos: el camino «sin peso» ya existe en el backend Panamá (`tienePeso`, sólo lo bloquea la línea 65) · el trigger del espejo **no** escucha `peso_bruto`/`peso_tara` · `ReprorratearPesoTrasEdicionAsync` ya escribe los 9 campos sobre líneas Completadas a propósito · sin NOT NULL/CHECK ⇒ (B) no requiere DDL
- [x] Plan escrito en `fase_de_desarrollo/`

## Fase 1 — Backend: cálculo puro + tests (gate CI)
- [x] `ValidarPesoObligatorioEnVenta` con parámetro `pesoDiferidoPermitido = false` (el default deja los 6 tests actuales verdes)
- [x] `MigracionCalculos.TryHora` (serial Excel / `DateTime` / `TimeSpan` / texto 12h y 24h) + `TryBooleanoSiNo`
- [x] Tests: flag OFF byte a byte idéntico · default apagado · flag ON ambos null · peso parcial sigue lanzando · mismos mensajes con peso inválido · `TryHora` (24h/12h/serial/serial+fecha/borde 24:00/texto) · `TryBooleanoSiNo`
- [x] `dotnet test` verde — **912/912** (⚠️ el `dotnet` del PATH es 9.0.301: usar `~/.dotnet/dotnet.exe` 10.0.301)

## Fase 2 — Backend: flag de empresa
- [x] `Company.VentaEngordePesoDiferido` + `CompanyConfiguration` (`venta_engorde_peso_diferido`, default `false`)
- [x] Propagado en las proyecciones (`CompanyDto`, `CompanyService.ToDto`, `CompanyService.Crud` ×2, `CompanyResolver` ×2, `CompanyPaisService`) + Create/UpdateCompanyDto
- [x] Migración idempotente `20260727003154_AddVentaEngordePesoDiferidoCompany` (`ADD COLUMN IF NOT EXISTS`)
- [x] Migración data-only `20260727003300_SeedVentaEngordePesoDiferidoPanama` (ItalcolPanama, `IS DISTINCT FROM`, Designer clonado)
- [x] `dotnet build` 0 errores / 0 advertencias
- [x] Aplicadas en BD local :5433 y verificadas — ItalcolPanama = `t`, Sanmarino/Ecuador/Demo/Santa Reyes = `f`

## Fase 3 — Backend: peso diferido + registro de peso
- [x] `EmpresaPermitePesoDiferidoAsync`: resolución fail-closed por `farms.company_id` de la granja del despacho (no por país ni por la empresa del token)
- [x] `MovimientoPolloEngordePanamaService.cs:65` delega con el flag (conservando el literal `"Venta"`)
- [x] `MovimientoPolloEngordeService.RegistrarPeso.cs` (partial nuevo): peso por FACTURA + `Confirmar` opcional, en transacción; escribe los 9 campos con el prorrateo de la creación
- [x] Decisión: **no** se reusa `ReprorratearPesoTrasEdicionAsync` — en despachos de 1 sola línea deja `peso_*_real` y los `*_global` en NULL (rama «movimiento simple»), lo que dejaría la venta distinta de una con peso el mismo día
- [x] DTOs `RegistrarPesoFacturaRequest` / `RegistrarPesoFacturaResponse` + método en `IMovimientoPolloEngordeService`
- [x] Endpoint `POST /api/MovimientoPolloEngorde/factura/{facturaId:guid}/registrar-peso`
- [x] `dotnet build` 0 errores / 0 advertencias

## Fase 4 — Backend: carga masiva completa (multi-lote)
- [x] `MigracionEsquemas.VentaPolloEngorde`: 11 → 26 columnas (ubicación + despacho + `Estado` + `Venta sobre mixtas`); `Peso Bruto/Tara` de `DobleOpc` a `DobleNoNeg` (antes aceptaba negativos)
- [x] Helpers `HoraOpc` / `BooleanoSiNo` en `MigracionService.Comun.cs`
- [x] Parser `MigracionService.VentaEngorde.cs` reescrito: resolución de lote por fila (reusa `CargarLotesEngordeUbicadosAsync` + `FiltrarPorUbicacion`) con fallback al contexto · validaciones nuevas (Estado, mixtas, peso completo, advertencia de `Completado` sin peso)
- [x] `ArmarDespachosVentaEngorde`: agrupa por N° Despacho + Fecha + Granja, asigna `factura_id` y **prorratea con `MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea`** (la misma función pura de la venta por pantalla ⇒ aritmética idéntica, sin duplicar el redondeo en plpgsql); error si el despacho trae pesos contradictorios entre filas
- [x] Hoja de instrucciones ampliada
- [x] `fn_migracion_venta_engorde` v2 — migración `20260727010000_FnMigracionVentaEngordeV2Despachos` (`CREATE OR REPLACE`, **sin** tocar `20260712190000`): 20 campos nuevos en el `jsonb_to_recordset` · `estado` por fila (`Pendiente` NO descuenta) · descuento sobre mixtas si `es_venta_mixta` (espeja `CompleteAsync`) · idempotencia por **rango de día** + `numero_despacho`
- [x] Diferencia deliberada vs `CompleteAsync`: la fn **no** pone `aves_encasetadas = 0` al vaciar el lote (es el denominador de los indicadores; hacerlo desde una carga histórica alteraría reportes ya publicados)
- [x] `backend/sql/fn_migracion_venta_engorde.sql` actualizado como fuente canónica
- [x] Tests de esquema: retro-compatibilidad con las 11 columnas viejas · solo `Fecha` requerida · presencia de los 15 campos del formulario · opciones de `Estado`
- [x] `dotnet build` 0 errores / 0 advertencias · `dotnet test` **916/916**
- [x] Migraciones aplicadas en BD local :5433

## Fase 5 — Frontend
- [x] `ventaEngordePesoDiferido` en `CompanyFlags` + `FLAGS_APAGADOS` + atajo `$` + azúcar + `mapFlags` + `publish` (fail-closed)
- [x] `funciones/prorateo-peso-despacho.funcion.ts` (pura): prorrateo sobre movimientos ya creados, espejo exacto del backend (3 decimales, residuo a la línea con más aves)
- [x] `components/modal-registro-peso/` nuevo — `ChangeDetectionStrategy.Eager` **explícito**, neto/promedio en vivo, tabla de reparto por lote con totales, y el botón cambia a «Guardar peso y confirmar venta» si el despacho está Pendiente
- [x] Listado: `completarMovimiento` y `completarGrupoDespacho` desvían al modal de peso cuando el despacho no tiene báscula · acción «⚖ Peso» para registrar/corregir · badge «⚖ Sin peso»
- [x] `modal-venta-panama`: `required` condicional por flag, mensaje de error reescrito (antes decía «Complete la fecha» ante cualquier invalidez), validación de peso a medias y ayuda en pantalla
- [x] `modal-movimiento-pollo-engorde`: `syncPesoValidators` gateado por el flag (sin esto no se podía **editar** la venta sin peso)
- [x] **Fix del bug que apagaba flags en silencio**: `UpdateCompanyDto` pasa los 6 flags a `bool?` y `CompanyService.Crud` usa `?? valorActual` ⇒ el form de Config→Empresas (que sólo manda datos de contacto) deja de apagar peso diferido, huevos por ítems, ERP, etc. al guardar
- [x] `yarn build` 0 errores (sólo el warning de bundle budget preexistente) — ⚠️ el Node del PATH es 22.15 y Angular 22 exige ≥ 22.22.3: usar `~/node-portable/node-v22.23.1-win-x64`

## Fase 5b — Cierre encontrado durante la validación
- [x] El gate del flag faltaba en los **otros dos** caminos de venta (`Crud.CreateAsync` y `VentaGranja`): el front ya dejaba enviar sin peso y el backend habría devuelto 400. Helper `EmpresaPermitePesoDiferidoAsync` movido al partial ancla de `MovimientoPolloEngordeService` y aplicado en los 3 caminos

## Fase 6 — Validación y cierre
- [x] **Smoke API local** (JWT + X-Secret-Up minteados, backend :5002, BD :5433):
  - venta multi-lote **sin peso** con flag ON → 200, `Pendiente`, los 9 campos de peso en NULL, `factura_id` compartido
  - `registrar-peso` + `confirmar` → `Completado`, neto 5.300,25 kg, kg/ave 3,5335, **aves descontadas** 10862/14008 → 9862/13508
  - 🔴 **trampa #1 verificada de punta a punta**: el espejo `lote_registro_historico_unificado` pasó de `peso_neto` NULL a 5300.250 (+ `peso_tara_real` y `promedio_peso_ave`) — con un UPDATE que sólo tocara `peso_bruto`/`peso_tara` habría quedado en 0 kg para siempre
  - corrección post-`Completado` (`confirmar:false`) → peso 6000, **estado y saldos de aves sin cambios**, espejo actualizado
  - flag **OFF** con la misma request → **400 con el mensaje histórico byte a byte**; peso a medias rechazado en ambos modos
- [x] **Smoke carga masiva**: plantilla con las **26 columnas** · archivo con las **11 viejas** válido sin faltantes · multi-lote con `N° Despacho` → 1 `factura_id`, prorrateo 2700+900 = 3600 = neto global y 3750+1250 = 5000 = bruto, kg/ave 1,8 en ambas, campos de despacho persistidos · `Estado='Pendiente'` → sin descuento · reimportar → 0 filas
- [x] 🔴 **Idempotencia corregida verificada**: recargar por Excel una venta creada por la UI a **mediodía UTC** → **0 filas** (con la clave vieja se habría duplicado la venta y descontado el lote dos veces)
- [x] 7 validaciones de fila comprobadas: mixtas con flag · peso a medias · Estado inválido · hora inválida · **peso negativo** (antes pasaba) · lote inexistente · bruto < tara
- [x] `dotnet build` 0 errores / 0 advertencias · `dotnet test` **916/916** · `yarn build` 0 errores
- [x] BD local restaurada (5 movimientos + 5 filas de espejo borrados, lote 106 devuelto a 10862/14008, flag de Ecuador vuelto a `false`, historial de migración limpiado) y backend detenido — sin procesos huérfanos
- [ ] Smoke UI en dev server — **pendiente**: los lotes de engorde de ItalcolPanama en la BD local tienen disponibilidad 0 (todas las aves asignadas a reproductora), así que la venta Panamá no se puede ejercitar acá; el flujo se validó por API sobre una empresa con el flag encendido temporalmente
- [ ] Commit acotado a los archivos de esta tarea (sin mezclar con los otros bloques)

## Fase 7 — Alineación pendiente (continúa en otra sesión)

**Traspaso:** [fase_de_desarrollo/CONTEXTO_TRASPASO_HUEVOS_LEVANTE.md](fase_de_desarrollo/CONTEXTO_TRASPASO_HUEVOS_LEVANTE.md) — estado, pendientes con archivo:línea, gotchas y receta de smoke. Pegar ese archivo al abrir el chat nuevo.

- [x] **P1** Columnas de huevos en la tabla diaria de levante + Excel — **3 columnas en pantalla** (🥚 Huevos total / incubables / peso, entre «Venta aves» y «Observaciones») y **las 14 en el Excel** (Tot + Inc + las 11 categorías + peso, insertadas antes del bloque de auditoría para no romper `colWidths`); flag de empresa leído con `ActiveCompanyConfigService` (fail-closed, el componente es `Eager`); `colspanRegistroDiario` +3. **Validado**: flag ON ⇒ 29 columnas y 0 filas desalineadas (39 filas), la fila con huevos muestra 520/480/57.8 y las 38 sin huevos muestran «—»; flag OFF ⇒ 26 columnas exactas, sin columnas de huevos, 0 desalineadas; `headers` y `rows` del Excel verificados 14 = 14 en el mismo orden; consola sin errores
- [ ] **P2** Carga masiva de levante acepta huevos — **diseño resuelto en el traspaso** (12 columnas: las 11 categorías + peso, `Requerida: false`, SIN Total/Incubable porque son derivados; gate en C# porque la fn no tiene `fecha_encaset`; la FIRMA de la fn no cambia ⇒ `CREATE OR REPLACE` + migración patrón `20260714022321`). 4 sitios: esquema · `MigracionService.Historicos` · `fn_migracion_seguimiento.sql` (3 puntos) · tests
- [ ] **P3** *(fase 2 acordada)* Huevos en `fn_indicadores_levante_postura` (RETURNS TABLE:73) y `fn_reporte_semanal_levante_extras` — requiere `DROP FUNCTION` + migración + DTOs + front; sin guía genética de huevos antes de la semana 26
- [x] **P4** `backend/sql/trigger_espejo_huevo_produccion_seguimiento_diario.sql` sincronizado: el trigger apuntaba a `public.seguimiento_diario` (inexistente) ⇒ reaplicarlo dejaba el trigger SIN crear; corregido a `seguimiento_diario_levante` (verificado en `pg_trigger` de la BD local) + cabecera con la historia de renombres y la migración `20260531180558` como fuente de verdad. **Cuerpo de la función comparado contra la migración: idéntico** (normalizado), así que el archivo ya no miente.
- [ ] **P5** Verificar en RDS prod el índice único `(lote_id, fecha_registro)` de `seguimiento_diario_produccion` que declara `SeguimientoProduccionConfiguration.cs:232` (en local NO existe); si se crea, revisar duplicados históricos primero **Dato nuevo (26-jul-2026):** en la BD local hay **0 grupos duplicados** por `(lote_id, fecha_registro::date)`, así que crear el índice ahí sería seguro. Query de verificación para prod: `select count(*) from (select lote_id, fecha_registro::date d, count(*) c from seguimiento_diario_produccion group by 1,2 having count(*)>1) x;` — si da >0 hay que depurar ANTES de crear el índice.
- [ ] **P6** *(opcional)* Modo «clasificación por ítems» (Santa Reyes) en levante — hoy fail-closed a propósito
- [ ] **P7** Confirmar la empresa del flag (hoy sólo `Agroavicola Sanmarino`, migración `20260726231200`)
- [ ] **P8** Decidir si `ReporteContableService` debe ver el arrastre (lee sólo `seguimiento_diario_levante` con `tipo='produccion'`) — inconsistencia preexistente, no doble conteo
- [ ] **P9** *(sólo documentar)* Pico esperado en los indicadores de producción el día del arrastre; y si se liquida antes de la semana 25 la fila no entra a `fn_indicadores_produccion_postura` (`DELETE ... sem_vida < 25`)

---

# Tracker — Reapertura validada de Levante + Cierre/Reapertura de Lote de Producción

**Plan:** [fase_de_desarrollo/cierre_levante_reapertura_validada_y_cierre_produccion_plan.md](fase_de_desarrollo/cierre_levante_reapertura_validada_y_cierre_produccion_plan.md)
**Fecha:** 2026-07-26

**Decisiones del usuario:** cierre + reapertura de producción **completo** (endpoints + UI) · LPP a **soft delete** al reabrir levante.

**Hallazgos que condicionan el diseño:** el cierre de levante ya crea filas en `seguimiento_diario_produccion` (arrastre de huevos + traslado de aves, ambas `tipo_alimento='N/A'`) ⇒ hay que distinguir sistema vs usuario · hoy **no existe** cierre de producción y `/api/Produccion/seguimiento` **no valida** `estado_cierre` al crear/editar/eliminar.

## Fase 0 — Análisis
- [x] Mapa del flujo cierre/reapertura levante→producción (endpoints, servicios, entidades, front)
- [x] Verificado que `tipo_alimento='N/A'` identifica las filas de sistema (el form manda `Validators.required` con default `'Standard'`; el merge sobrescribe con el alimento real)
- [x] Plan escrito en `fase_de_desarrollo/`

## Fase 1 — Backend: cálculo puro + tests (gate CI)
- [x] `Application/Calculos/CicloVidaPosturaCalculos.cs` (NUEVO): `RegistroProduccionResumen`, `EsRegistroDeUsuario`, `FiltrarRegistrosDeUsuario`, `EstaCerrado`/`EstaAbierto`, 3 constructores de mensaje
- [x] `tests/ZooSanMarino.Application.Tests/CicloVidaPosturaCalculosTests.cs` (NUEVO) — **33 casos** verdes
- [x] `dotnet test` verde — **949/949** en toda la suite (916 previos + 33)

## Fase 2 — Backend: reapertura validada de levante
- [x] DTO nuevo `ReaperturaLoteLevanteResumenDto` (puede reabrir, motivo del bloqueo, aviso, conteos y rango de fechas)
- [x] `GetResumenReaperturaAsync` + `GET /LotePosturaLevante/{id}/resumen-reapertura`
- [x] `AbrirLoteAsync`: bloqueo por registros de usuario (R1) y por LPP cerrado (R2), con la MISMA evaluación que alimenta el modal ⇒ UI y API no pueden discrepar
- [x] `AbrirLoteAsync`: **soft delete** del LPP (R3) en vez de `Remove`
- [x] `EliminarDependientesLoteProduccionAsync`: recibe los ids de las filas de sistema y **solo borra esos** (antes hacía DELETE por LPP a ciegas)
- [x] Filas de la tabla unificada atadas al LPP: dejan de borrarse y pasan a bloquear (el cierre no las crea ⇒ son de otro flujo)
- [x] `<summary>` de `ILotePosturaLevanteService.AbrirLoteAsync` corregido (prometía validar dependientes y no lo hacía)

## Fase 3 — Backend: cierre/reapertura de producción
- [x] Migración idempotente `20260727023150_AddEstadoCierreAuditoriaLotePosturaProduccion` (`ADD COLUMN IF NOT EXISTS` ×3, nullable, sin backfill) + entidad + configuration
- [x] Migración aplicada en BD local :5433
- [x] `CierreLoteProduccionDto.cs` (NUEVO) + 3 métodos en `ILotePosturaProduccionService`
- [x] `LotePosturaProduccionService`: `CerrarLoteAsync` / `AbrirLoteAsync` / `GetResumenCierreAsync` (R6, R7) con auditoría quién/cuándo/por qué y scoping fail-closed
- [x] `LotePosturaProduccionController`: `POST {id}/cerrar`, `POST {id}/abrir`, `GET {id}/resumen-cierre`
- [x] `ProduccionService`: guard `EnsureLoteProduccionAbiertoAsync` en Crear / Actualizar / Eliminar (R5) — antes NINGUNA de las tres validaba `estado_cierre`
- [x] `dotnet build` 0 errores / 0 advertencias (⚠️ el backend de otra sesión bloquea los DLL: compilar con `-p:BaseOutputPath=<scratchpad>`)

## Fase 4 — Frontend
- [x] Levante: el modal de reapertura consulta el resumen al abrirse y muestra bloqueo con detalle (lote, cantidad, rango de fechas) o aviso de recreación; botón y textarea deshabilitados si está bloqueado
- [x] Levante: tooltip del botón corregido (prometía una validación que no existía)
- [x] Producción: botones Cerrar / Abrir lote + modal de motivo con resumen del lote y auditoría del último cambio
- [x] Producción: gate de Nuevo registro / Traslado / ✎ Editar / 🗑 Eliminar + chip «🔒 Lote cerrado»; guard `bloqueadoPorLoteCerrado()` con toast para estado desincronizado
- [x] Servicios HTTP + DTOs TS de los 4 endpoints nuevos
- [x] `DecimalPipe`/`DatePipe` agregados a `lote-produccion-list` (el componente no los importaba)
- [x] `yarn build` 0 errores (solo el warning de bundle budget preexistente)

## Fase 4b — Defecto encontrado durante el smoke
- [x] `DELETE /api/Produccion/seguimiento/{id}` devolvía **500** ante una regla de negocio: el controller sólo tenía `catch (Exception)`. Se agregó `catch (InvalidOperationException) → 400` (POST y PUT ya lo hacían bien)

## Fase 5 — Validación y cierre
- [x] **Smoke API local** (backend propio en :5099 para no tocar el de la otra sesión; JWT + X-Secret-Up minteados):
  - 🔴 **caso clave**: cerrar levante con huevos crea la fila de arrastre en `seguimiento_diario_produccion` ⇒ `registrosProduccionSistema=1`, `puedeReabrir=true`. Contando «cualquier fila» la reapertura habría quedado bloqueada para siempre
  - reabrir permitido → LPP **soft-deleted** (`deleted_at` poblado, verificado en BD) + fila de sistema borrada; recerrar **recrea** el LPP (id nuevo) con el arrastre regenerado
  - con 1 seguimiento del usuario → `POST /abrir` **400** con el mensaje y el conteo exactos, y **LPP + sus 2 registros intactos** (verificado por API y en BD)
  - lote de producción cerrado → crear / editar / eliminar **400**; reabrir producción → las tres vuelven a funcionar
  - reabrir levante con producción cerrada → 400 pidiendo reabrir producción primero
  - motivo < 3 caracteres y cerrar un lote ya cerrado → 400; auditoría (`estado_cierre_motivo`/`_fecha`) persistida
- [x] **Smoke UI** en dev server :4300 (sesión inyectada; origen agregado por variable de entorno `AllowedOrigins__0`, sin tocar archivos compartidos):
  - modal de reapertura **bloqueado**: mensaje + detalle, «Abrir lote» deshabilitado, textarea deshabilitada, botón «Entendido»
  - modal **permitido**: aviso de que el LPP se elimina y se recrea, controles habilitados
  - abierto y cerrado **dos veces** (checklist de change detection): sin spinner colgado en ninguna iteración
  - producción: cerrar ⇒ desaparecen Traslado/✎/🗑, «Nuevo registro» deshabilitado y aparece «🔒 Lote cerrado»; reabrir ⇒ todo vuelve. Los 4 endpoints nuevos responden 200 en la Network tab
- [x] BD local restaurada al estado original (0 LPP de prueba, 0 seguimientos de prueba, `estado_cierre` de los 6 lotes igual que al inicio) · `environment.ts` revertido a :5002 · dev server y backend propios detenidos (sin procesos huérfanos; el backend :5002 de la otra sesión quedó intacto)
- [x] `dotnet build` 0 errores / 0 advertencias · `dotnet test` **949/949** · `yarn build` 0 errores
- [x] Commit

---

# Tracker — PWA offline-first con sincronización diferida (ANÁLISIS)

**Plan:** [fase_de_desarrollo/pwa_offline_first_plan.md](fase_de_desarrollo/pwa_offline_first_plan.md)
**Fecha:** 2026-07-26

Objetivo: que los módulos operativos funcionen sin red y sincronicen al recuperar conexión, como **PWA autoactualizable** (no app móvil nativa), dejando el desarrollo alineado para que lo nuevo nazca sirviendo a los dos modos.

> ⚠️ **Estado: análisis terminado, implementación NO iniciada.** El plan requiere 7 decisiones del usuario (§7 del plan) antes de escribir código.

## Fase 0 — Análisis
- [x] Exploración exhaustiva (workflow 14 agentes, 981 lecturas): 8 áreas de inventario (postura, engorde, inventario, movimientos/ventas, granjas/catálogos, auth/sesión, plataforma backend, build/hosting) + 3 de riesgo (volumetría, reglas de negocio, precedentes batch) + 3 críticas adversariales (datos/conflictos, seguridad/tenancy, entrega/operación)
- [x] Volumetría **medida**, no estimada, con `octet_length(row_to_json(x)::text)` contra `sanmarinoapplocal:5433` (solo lectura): 2-4 MB por operario típico · 8,2 MB peor caso medido · 15 MB sin ventana ⇒ **el tamaño no es el problema**
- [x] Verificado que `zootecnicoapp/` (Flutter) es un scaffold vacío de mayo-2026 — la decisión PWA no descarta trabajo previo
- [x] Confirmado punto de partida cero: sin `@angular/service-worker`, sin manifest, sin IndexedDB, sin idempotencia, sin control de concurrencia, sin tombstones
- [x] Plan escrito en `fase_de_desarrollo/`

## Decisiones pendientes del usuario (bloquean el arranque)
- [ ] **D1** Alcance de escritura en v1 (recomendado: lista blanca de captura diaria; ventas y movimientos a v2)
- [ ] **D2** ¿Fase 0 completa antes, o Fase 0.C + piloto de solo lectura en paralelo? (recomendado: en paralelo)
- [ ] **D3** Dato en reposo: cifrar con PIN/WebAuthn vs no cifrar + minimizar + TTL
- [ ] **D4** Vigencia de sesión offline: jornada vs 7 días (recomendado: jornada, con revocación real)
- [ ] **D5** Dispositivos objetivo (Android / iOS / ambos) — define si hace falta sync explícita en primer plano
- [ ] **D6** Modo offline global vs opt-in por rol y dispositivo (recomendado: opt-in; prohibido para alcance global)
- [ ] **D7** Verificar con `curl -I` contra prod cuál es el origen real del front (ECS+nginx vs S3+CloudFront)

## Fase 0.C — Higiene de entrega (sin tocar funcionalidad)
- [ ] **C1** Eliminar la mutación post-build de `index.html` (`Dockerfile:53-56` + `scripts/inject-version.js`) — invalida el hash de `ngsw.json` ⇒ SW en safe mode silencioso
- [ ] **C2** Fallback a `index.html` solo para navegaciones (nginx `try_files $uri =404` para extensiones conocidas + CloudFront sin CustomErrorResponse global)
- [ ] **C3** `no-cache` en `ngsw.json` / `ngsw-worker.js` / `safety-worker.js` / `manifest.webmanifest` / `index.html`
- [ ] **C4** Definir el único origen del front y borrar el camino muerto del repo
- [ ] **C5** Arreglar la herencia de headers de nginx (hoy index.html y los .js salen **sin CSP ni HSTS**)
- [ ] **C6** Gate de tests real en el pipeline (hoy 296 líneas de workflow, 0 `dotnet test` / `yarn test`) — arreglar antes el harness de Karma (compila 0 specs)
- [ ] **C7** `deploy-frontend: needs: [deploy-backend]`
- [ ] **C8** Política de rate limit propia para `/api/sync/*` por usuario/device (hoy bloquea la IP completa 3 min)

## Fase 0.B — Sesión y seguridad
- [ ] **B1** `jti` + tabla `sesiones_activas` + refresh token (hoy **no hay forma de revocar una sesión**)
- [ ] **B2** `SessionTimeoutService`: suspender idle/heartbeat logout en offline; nunca purgar con cola pendiente
- [ ] **B3** Distinguir 401 de autenticación vs 401 de plataforma
- [ ] **B4** Llevar a server-side los gates de escritura hoy front-only (~46 `*appHasPermission` vs ~7 chequeos en controllers)
- [ ] **B5** El servidor estampa el autor desde el token e ignora `dto.CreatedByUserId`
- [ ] **B6** Eliminar el fallback silencioso de empresa y la confianza en `X-Active-Pais`
- [ ] **B7** Corregir `setActiveCompany()` (cambia el nombre y nunca el id)
- [ ] **B8** Rotar las 4 llaves de `environment.prod.ts` (quemadas en git)
- [ ] **B9** Decidir política de dato en reposo (D3)
- [ ] **B10** Super admin por email hardcodeado → a datos

## Fase 0.A — Integridad de datos (varios son bugs de HOY)
- [ ] **A1** UNIQUE en la clave natural de `inventario_gestion_stock` + `ON CONFLICT DO UPDATE` — 🔴 explotable hoy con dos pestañas
- [ ] **A2** Descuento de stock como UPDATE atómico condicional
- [ ] **A3** `trigger_lotes_to_lote_postura_levante`: la rama UPDATE deja de pisar `aves_*_actual`
- [ ] **A4** Sacar el `SaveChangesAsync` de `ProduccionService.ObtenerInformacionLoteAsync` (lectura que escribe)
- [ ] **A5** `deleted_at` + soft delete + `sync_tombstones` en las tablas operativas
- [ ] **A6** Índice único de producción a `(lote_postura_produccion_id, fecha)`
- [ ] **A7** Consolidar los dos services que escriben `seguimiento_diario_levante` (`Program.cs:217` vs `:232`)
- [ ] **A8** `FechaOperacion` en el consumo + `fn_acumulado_entradas_alimento` por `(fecha_operacion, id)`
- [ ] **A9** `lote_ave_engorde_id` explícito + `fn_lote_ave_engorde_id_desde_ubicacion` por rango de vida
- [ ] **A10** Reemplazar el trigger acumulativo del espejo de huevos por el recálculo derivado ya existente

## Fases F1-F5 — ver §8 del plan (no desglosadas hasta que se cierren las decisiones)
