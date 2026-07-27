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

> ✅ **Estado: decisiones cerradas, Fase 0.C EN CURSO.** (2026-07-27)

## Fase 0 — Análisis
- [x] Exploración exhaustiva (workflow 14 agentes, 981 lecturas): 8 áreas de inventario (postura, engorde, inventario, movimientos/ventas, granjas/catálogos, auth/sesión, plataforma backend, build/hosting) + 3 de riesgo (volumetría, reglas de negocio, precedentes batch) + 3 críticas adversariales (datos/conflictos, seguridad/tenancy, entrega/operación)
- [x] Volumetría **medida**, no estimada, con `octet_length(row_to_json(x)::text)` contra `sanmarinoapplocal:5433` (solo lectura): 2-4 MB por operario típico · 8,2 MB peor caso medido · 15 MB sin ventana ⇒ **el tamaño no es el problema**
- [x] Verificado que `zootecnicoapp/` (Flutter) es un scaffold vacío de mayo-2026 — la decisión PWA no descarta trabajo previo
- [x] Confirmado punto de partida cero: sin `@angular/service-worker`, sin manifest, sin IndexedDB, sin idempotencia, sin control de concurrencia, sin tombstones
- [x] Plan escrito en `fase_de_desarrollo/`

## Decisiones del usuario — CERRADAS (2026-07-27)
- [x] **D1** Alcance de escritura v1 = **lista blanca de captura diaria** (§3.2). Ventas y movimientos a v2
- [x] **D2** **Fase 0 completa (C → B → A) primero**, PWA después. (Se descartó el piloto en paralelo)
- [x] **D3** **No cifrar** el dato en reposo + minimizar (sin precios ni facturación) + TTL duro + purga en logout
- [x] **D4** Sesión offline de **una jornada (12-16 h)**, condicionada a B1 (revocación real)
- [x] **D5** **Solo Android** ⇒ Background Sync disponible, sin la eviction de 7 días de Safari iOS, sin necesidad de sync explícita en primer plano
- [x] **D6** **Opt-in** por rol y por dispositivo registrado; prohibido para cuentas con alcance global/multiempresa
- [x] **D7 VERIFICADO contra prod** — el origen real es **ECS + nginx tras el ALB** (`Server: nginx`, sin `Via`/`X-Cache`/`X-Amz-Cf-Id`). El `frontend/deploy/*.json` de S3+CloudFront describe **otra cuenta AWS** (`021891592771` vs `196080479890` del pipeline) ⇒ camino muerto

## Hallazgos de la verificación contra prod (2026-07-27) — corrigen supuestos del plan
| Punto | Lo que decía el plan | Lo medido |
|---|---|---|
| C2 | `try_files $uri $uri/ /index.html` se traga todo | `.js` inexistente **ya daba 404** (el regex de assets tiene `try_files $uri =404`). El bug real es que **`.json` y `.webmanifest` devolvían 200 `text/html`** con el index |
| C3 | nginx marca todo `.js` como immutable | ✅ confirmado: `polyfills-*.js` sale con `max-age=31536000, immutable` ⇒ `ngsw-worker.js` habría quedado cacheado un año |
| C5 | index.html y los .js salen sin CSP ni HSTS | ✅ confirmado: la respuesta de `/` solo trae `X-Content-Type-Options` y `X-Frame-Options` |
| C3/C2 | Hay que tocar behaviors de CloudFront | **No aplica**: no hay CloudFront en el camino |
| C6 | El harness de Karma "compila 0 specs" | Peor: `ng test` **fallaba** con TS18003. `tsconfig.spec.json` heredaba `exclude: ["**/*.spec.ts"]` de `tsconfig.json`, que en TS gana sobre `include` |

## Fase 0.C — Higiene de entrega (sin tocar funcionalidad)

- [x] **C1** Eliminada la mutación post-build de `index.html`
  - `scripts/inject-version.js` **borrado**; nace `scripts/build-version.js` con dos fases: `prepare` (antes del build, sella el buildId en `src/app/core/build-info.ts` ⇒ entra al bundle y se hashea normal) y `emit` (después, escribe `dist/browser/version.json`, archivo NUEVO que nunca entra en la tabla de hashes del SW)
  - `src/index.html`: fuera el `<meta name="app-version" content="BUILD_TIMESTAMP_PLACEHOLDER">`
  - `VersionCheckService` reescrito: compara `BUILD_ID` compilado contra `/version.json` (antes se bajaba el `index.html` entero cada 5 min y lo parseaba con regex). En local `BUILD_ID='dev'` ⇒ el chequeo se apaga
  - `Dockerfile`: `prepare && yarn build && emit`
  - ⚠️ **Gotcha**: `build-info.ts` debe declarar `BUILD_ID: string` explícito; sin el tipo, TS infiere el literal del timestamp y `BUILD_ID !== 'dev'` no compila (TS2367). Lo cazó el build
- [x] **C2** Fallback a `index.html` **solo para navegaciones** — bloque `location ~* \.(json|webmanifest|map|txt|xml|wasm|webp|avif|mp4|webm|pdf|zip|csv|xlsx)$` con `try_files $uri =404`
- [x] **C3** `no-cache` en los archivos de control del SW — bloques `location =` dedicados para `ngsw.json`, `ngsw-worker.js`, `safety-worker.js`, `worker-basic.min.js`, `manifest.webmanifest`, `version.json` e `index.html`, **antes** del regex de assets (si no, el worker cae en la regla `immutable` de un año). `manifest.webmanifest` además fija `application/manifest+json` (nginx no lo trae en su `mime.types`)
- [x] **C4** Un solo origen — `nginx.conf` y `frontend/README.md` declaran ECS+nginx; los 6 archivos de S3+CloudFront movidos a `frontend/deploy/ARCHIVADO-s3-cloudfront/` con README que explica por qué están muertos y qué habría que replicar si se vuelve a poner un CDN. **NO** se tocaron `ecs-taskdef*.json` ni `ecr-policy-frontend.json` (sí están en uso)
- [x] **C5** Herencia de headers — nace `frontend/nginx-security-headers.conf`, incluido por **cada** `location`. Se agregaron `worker-src 'self'` y `manifest-src 'self'` a la CSP. Se quitaron los `X-RateLimit-Limit: 100` / `X-RateLimit-Remaining: 99` hardcodeados de nginx (eran informativos y **mentían**: valor constante, y el rate limiter real es el del backend)
- [x] **C6** Gate de tests real en el pipeline — job `tests` (xUnit + Karma) del que dependen ambos deploys
  - 🔴 **Causa raíz del harness muerto**: `tsconfig.spec.json` no declaraba `exclude`, así que heredaba `exclude: ["**/*.spec.ts","**/*.test.ts"]` de `tsconfig.json`. En TypeScript `exclude` gana sobre `include` ⇒ 0 archivos ⇒ `ng test` moría con **TS18003**. Fix: `"exclude": []`
  - Faltaba `stylePreprocessorOptions.includePaths` en el target `test` de `angular.json` (sí estaba en `build`) ⇒ `@use 'shared/styles/module-styles'` no resolvía
  - `app.component.spec.ts` era el scaffold de Angular CLI y afirmaba un `title === 'frontend'` y un `<h1>Hello, frontend</h1>` que esta app **nunca tuvo**. Reemplazado por un smoke real (crea, toggle del sidebar, `showSidebar` en rutas públicas vs protegidas)
  - 5 specs de detalle/formulario fallaban con **NG0201** (`ActivatedRoute` sin proveer): `city-detail`, `country-detail`, `department-detail`, `list-detail`, `farm-form`. Agregados `provideRouter([])` + `provideHttpClient()` + `provideHttpClientTesting()`
  - Resultado: **71/71 SUCCESS**, exit 0 (de `ng test` que ni arrancaba)
- [x] **C7** `deploy-frontend` depende de `[tests, deploy-backend]` — con `if` explícito para que un `workflow_dispatch` con `deploy_backend=false` **no** saltee también el frontend, y usando `needs['deploy-backend']` (con guión hace falta notación de índice, `needs.deploy-backend` se parsearía como resta)
- [x] **C8** Rate limit por dispositivo para sincronización
  - `RateLimitingCalculos`: enum `AlcanceRateLimit {General, Auth, Sync}`, `EsRutaSync`, `AlcanceDeRuta`, `IdentidadCliente` (sync cuenta por `X-Device-Id`, cae a IP si falta) y claves de bloqueo por alcance
  - Sync queda **aislado**: no bloquea la IP ni es bloqueado por el bloqueo global ⇒ cinco tablets del mismo módem no se autobloquean ni tumban el login de la granja
  - La identidad sale de una **cabecera** y no del JWT porque el middleware corre en `Program.cs:596`, **antes** de `UseAuthentication()` (`:698`): `context.User` todavía está vacío
  - Límite propio `RateLimiting:MaxRequestsPerMinuteForSync` (default 300/min por dispositivo)
  - Tests: 14 casos nuevos en `RateLimitingCalculosTests` (incluido el escenario de las 5 tablets)

### Validación de Fase 0.C
- [x] `yarn build` — 0 errores (solo el warning de bundle budget preexistente)
- [x] Sellado de versión verificado punta a punta: el buildId aparece **dentro** de `main-*.js` y en `version.json` con el mismo valor; `dist/browser/index.html` sin placeholder ni `app-version`
- [x] `yarn test --watch=false --browsers=ChromeHeadless` — **71/71 SUCCESS**
- [x] `dotnet build` — 0 errores / 0 advertencias (⚠️ el backend de otra sesión bloquea los DLL: compilar con `-p:BaseOutputPath=<scratchpad>`)
- [x] `dotnet test` — **973/973** (972 Application + 1 Domain)
- [x] YAML del workflow parseado, dependencias de jobs verificadas (`tests` → `deploy-backend` → `deploy-frontend`) y los **17 scripts `run` pasan `bash -n`**
- [x] `nginx.conf` y `nginx-security-headers.conf`: llaves balanceadas, 11 de 12 `location` incluyen los headers (el 12° es `location ~ /\.` → `deny all`, deliberado)
- [x] **La validación de nginx se movió al pipeline** en vez de quedar como un chequeo local de una sola vez: nuevo step *"Validar nginx y política de caché del borde"* en `deploy-frontend`, que corre **después del build y ANTES del push a ECR** ⇒ una configuración rota no llega nunca a ECR ni a ECS. Hace `nginx -t` sobre la imagen, la levanta y verifica los criterios §9: 404 en `.js`/`.json`/`.webmanifest` inexistentes, 200 en ruta del SPA, `no-cache` en `version.json`/`index.html`, `immutable` en el asset con hash, y CSP+HSTS+`worker-src` en `/`, en el `.js` y en la ruta del SPA
  - ⚠️ El engine de Docker **no levantó** en la máquina local en esta sesión (Docker Desktop arranca pero `docker info` cuelga), así que el smoke en contenedor no se pudo correr acá. El script quedó escrito y es el mismo que ahora corre en CI. Riesgo acotado: si el `nginx.conf` estuviera mal, el step de CI falla antes de publicar
- [ ] Verificación post-deploy (criterios §9 del plan) contra prod, una vez desplegado:
      `curl -i /chunk-inexistente.js` → 404 · `curl -I` sobre los archivos de control → `Cache-Control: no-cache` · `curl -I /` → con CSP y HSTS

## Fase 0.B — Sesión y seguridad

- [ ] **B1** `jti` + tabla `sesiones_activas` + refresh token (hoy **no hay forma de revocar una sesión**). Prerrequisito de la decisión D4 (jornada de 16 h): el tope de jornada ya está implementado en B2, pero sin B1 no se puede revocar un dispositivo perdido antes de que venza
- [x] **B2** `SessionTimeoutService` consciente del modo sin conexión
  - La política se extrajo a `core/auth/funciones/politica-sesion.funcion.ts` (**pura, con 16 tests**) y el servicio quedó de orquestador
  - 🔴 **Perder la red ya NO cierra la sesión.** Antes, 2 heartbeats con `status 0` (~3 min sin señal) llamaban a `endSession('sin_conexion')`, que purga el storage y manda al login — y sin red el usuario **no puede volver a entrar** (el login necesita el backend, y en prod además reCAPTCHA, que necesita alcanzar a Google). El motivo `'sin_conexion'` desapareció; ahora solo marca el modo sin conexión
  - Sin red la **inactividad tampoco cierra** (mismo motivo: sería irreversible)
  - Con **operaciones pendientes de sincronizar no se cierra por tiempo** bajo ninguna circunstancia (cerrar purga, y purgar destruye capturas de campo)
  - Tope duro de jornada: **16 h sin contacto con el servidor** (extremo alto de D4), medido desde el último heartbeat OK y no desde la última actividad
  - Estado de conexión expuesto (`enLinea$`) para el indicador de modo sin red de F1, y listeners `online`/`offline` del navegador
  - Seam `TRABAJO_PENDIENTE_OFFLINE` (`InjectionToken`, `optional`, default 0) que implementará el outbox en F3. Existe desde ahora porque la política **necesita** consultarlo
- [x] **B3** 401 de autenticación vs 401 de plataforma
  - `PlatformSecretMiddleware` tipifica sus 3 rechazos: cabecera `X-Auth-Failure: platform-secret` + `errorCode` en el cuerpo; los tres bloques duplicados se unificaron en `RechazarAsync` conservando 401 y el cuerpo histórico
  - `core/auth/funciones/debe-cerrar-sesion-por-401.funcion.ts` (**pura, 10 tests**) + el interceptor delega
  - La señal se lee del **cuerpo**, no de la cabecera: en dev el front (`:4200`) y el back (`:5002`) son orígenes distintos y una cabecera custom no es legible sin `Access-Control-Expose-Headers`. La cabecera queda de respaldo para mismo origen y para `curl`/logs
  - Sin esto, **rotar el SECRET_UP deslogueaba a todos los dispositivos a la vez** — y con la PWA se llevaría puesta la cola de sincronización
- [ ] **B4** Llevar a server-side los gates de escritura hoy front-only (~46 `*appHasPermission` vs ~7 chequeos en controllers)
- [ ] **B5** El servidor estampa el autor desde el token e ignora `dto.CreatedByUserId` — **3 sitios localizados**: `SeguimientoDiarioService.cs:291` (create), `:889` (update) y `SeguimientoDiarioLoteReproductoraService.cs:228`. ⚠️ Antes de tocarlo hay que verificar que ningún camino interno (carga masiva / arrastre) dependa de mandar el autor en el DTO
- [ ] **B6** Eliminar el fallback silencioso de empresa (`ActiveCompanyMiddleware.cs:129-136`) y la confianza en `X-Active-Pais`
- [x] **B7** `setActiveCompany()` mueve nombre + id + país + logo a la vez
  - 🔴 El bug: solo escribía `activeCompany` (el **nombre**). El interceptor manda también `X-Active-Company-Id` y el backend **prefiere el id** ⇒ al cambiar de empresa la UI mostraba una y el backend respondía por la del login. Del lado del cliente, todo lo que lee `activeCompanyId` (flags por empresa, listas maestras, menús de rol, listado de granjas) seguía en la anterior
  - `core/auth/funciones/resolver-empresa-activa.funcion.ts` (**pura, 12 tests**) resuelve contra `companyPaises`, tolera camelCase y PascalCase, y es **fail-closed**: si no resuelve, no cambia nada y devuelve `false`
  - `company-selector` deja de emitir `companyChanged` cuando el cambio no ocurrió
  - Relevante para la PWA: el snapshot offline se particiona por `{userId, companyId}`; un id desincronizado escribiría capturas en la partición equivocada
- [ ] **B8** Rotar las 4 llaves de `environment.prod.ts` (quemadas en git). ⚠️ Requiere que el usuario genere y cargue los valores nuevos; yo puedo dejar el mecanismo (variables de build) pero no los secretos
- [x] **B9** Política de dato en reposo **decidida** (D3): no cifrar + minimizar (sin precios ni facturación) + TTL duro + purga en logout. Queda por *implementar* junto con el repositorio offline en F2
- [ ] **B10** Super admin por email hardcodeado (`ActiveCompanyMiddleware.cs:52` y `:116`) → a datos, reusando el patrón `roles.is_company_admin`

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
