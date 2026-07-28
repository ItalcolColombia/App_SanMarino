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

---

# Tracker — Seguimiento pollo engorde MIXTO (Panamá): Excel mixto + descuento de aves mixtas

**Plan:** [fase_de_desarrollo/seguimiento_engorde_mixto_panama_plan.md](fase_de_desarrollo/seguimiento_engorde_mixto_panama_plan.md)
**Fecha:** 2026-07-27

**Decisiones del usuario:** el descuento impacta **maestro del lote + movimiento auditado**, y aplica a
**los dos caminos** (formulario diario y carga masiva). Gate del Excel = flag por empresa; gate del bucket
de descuento = datos del lote (mixto = `mixtas > 0 && hembras_l == 0 && machos_l == 0`).

## Fase A — Excel / plantilla mixta

- [x] `Company.SeguimientoEngordeMixto` + `CompanyConfiguration`
- [x] Migración schema `20260727161113_AddSeguimientoEngordeMixtoCompany` (ADD COLUMN IF NOT EXISTS)
- [x] Migración data-only `20260727161200_SeedSeguimientoEngordeMixtoPanama` (ItalcolPanama, `IS DISTINCT FROM`)
- [x] `MigracionEsquemas`: alias mixtos en las columnas de género (constantes `Mix*` compartidas plantilla↔parseo)
- [x] `MigracionEsquemas.SeguimientoPolloEngordeMixto` (18 columnas, sin columnas por sexo)
- [x] `MigracionService.SeguimientoEngorde`: plantilla, dropdowns e instrucciones según el flag
- [x] ~~Flag en las proyecciones de `CompanyDto`~~ — NO aplica: la plantilla la arma el backend, el front no gatea nada con este flag. Se agrega el día que la UI lo necesite.
- [x] Tests de esquema: contrato plantilla↔parseo, 9 alias mixtos, sin columnas por sexo, regresión de encabezados viejos

## Fase B — Descuento de aves mixtas

- [x] `Application/Calculos/RetiroAvesEngordeCalculos.cs` (puro) + `RetiroAvesEngordeAplicador` (compartido por los dos services)
- [x] `RetiroAvesEngordeCalculosTests.cs` — 20 casos (los 8 del plan + reparto, netos y regresión por sexo)
- [x] Create descuenta maestro + fila `BAJA_SEGUIMIENTO` — en los DOS services (carga masiva y formulario diario)
- [x] Update compensa por delta (devuelve aves si baja la mortalidad) — en los dos services
- [x] Delete revierte y anula la fila del histórico — en los dos services
- [x] Doble descuento evitado SIN tocar la función SQL: el aplicador no descuenta si `aves_encasetadas = 0` (única rama donde la fn deriva la inicial del maestro). Verificado en BD: los 134 lotes tienen `aves_encasetadas > 0` ⇒ no afecta a ninguno. Se descartó re-aplicar la fn porque el repo tiene dos versiones del archivo y re-aplicarla podría regresar prod.
- [x] `CorreccionAvesDisponiblesEngordeService`: la conservación resta las bajas YA APLICADAS (filas `BAJA_SEGUIMIENTO`), no la mortalidad registrada ⇒ los lotes viejos conservan la fórmula anterior

## Entregables

- [x] Excel de ejemplo final `CargaMasiva_Seguimiento_Engorde_PANAMA_MIXTO.xlsx` (18 columnas, 0 desconocidas)
- [x] Plantilla descargable verificada por test: todos los títulos mixtos los acepta el esquema de parseo

## Validación

- [x] `dotnet build` (Application + Infrastructure) — 0 errores, 0 advertencias
- [x] `dotnet test` — 1004/1004 verdes
- [ ] **PENDIENTE** smoke de la plantilla descargada con flag ON/OFF: requiere reiniciar el backend local, que está corriendo en otra sesión (PID 38200). Migraciones sin aplicar en la BD local por el mismo motivo; el SQL generado se verificó con `dotnet ef migrations script`.

---

# Tracker — Hora de encasetamiento define el primer día con registro (engorde y reproductora)

**Plan:** [fase_de_desarrollo/hora_encasetamiento_primer_registro_plan.md](fase_de_desarrollo/hora_encasetamiento_primer_registro_plan.md)
**Fecha:** 2026-07-27

**Decisiones del usuario:** corte **13:00** (`>= 13:00` ⇒ el primer consumo va al día siguiente) ·
la **edad NO se recorre**: se sigue contando desde `fecha_encaset`, así que un lote tardío arranca en
edad 1 (Día 2). Solo cambia cuál es el primer día con registro válido ⇒ **no se toca ninguna función SQL**.

## Backend

- [x] `TimeOnly? HoraEncasetamiento` en `LoteAveEngorde` y `LoteReproductoraAveEngorde` + configuraciones (`time`)
- [x] Migración `20260727170032_AddHoraEncasetamientoLotesEngorde` (ADD COLUMN IF NOT EXISTS, nullable, sin backfill)
- [x] `Application/Calculos/EncasetamientoCalculos.cs` (corte 13:00, primer día, edad mínima, motivo para el mensaje)
- [x] `EncasetamientoCalculosTests.cs` — 16 casos (incluye 12:00 y 13:00 exactos, fin de mes, bisiesto y la ventana de la reproductora)
- [x] `ReproductoraEngordeCalculos.EsEdadSeguimientoValida` con `edadMinima` opcional (default 0 ⇒ llamadas previas intactas)
- [x] `SeguimientoDiarioLoteReproductoraService`: Create + Update usan la edad mínima + mensaje que explica la hora
- [x] `MigracionService.SeguimientoReproductora`: idem en carga masiva
- [x] `MigracionService.SeguimientoEngorde`: fecha mínima = primer día, con mensaje que explica la hora
- [x] La hora viaja en los DTOs de lote engorde y reproductora (create/update/detail/list)

## Frontend

- [x] Campo "Hora de encasetamiento" (opcional) al crear/editar lote engorde
- [x] Campo "Hora encasetamiento" (opcional) en los dos formularios de lote reproductora
- [x] `modal-seguimiento-reproductora`: `minFechaYmd` arranca en el primer día (espejo del corte 13:00 en el front)

## Validación

- [x] `dotnet build` 0 errores/0 advertencias · `dotnet test` 1020/1020
- [x] `ng build` 0 errores (solo el warning preexistente de bundle budget)
- [ ] **PENDIENTE** smoke end-to-end: requiere aplicar la migración en la BD local y reiniciar el backend, que está corriendo en otra sesión.

---

# Fix — deploy del frontend roto en CI (`MODULE_NOT_FOUND` en el build de Docker)

Plan: [fase_de_desarrollo/fix_deploy_frontend_dockerignore_plan.md](fase_de_desarrollo/fix_deploy_frontend_dockerignore_plan.md)

Run fallido **82085199647** (2026-07-27 17:46 UTC), job `Frontend — Build & Deploy`, paso 7.
Causa: `.dockerignore` dejaba pasar `scripts/inject-version.js`, borrado por `76a2903` al renombrarlo a
`build-version.js` ⇒ `scripts/` llegaba vacío al contexto y `COPY scripts ./scripts` no se quejaba.

## Cambios

- [x] `frontend/.dockerignore`: lista blanca apunta a `scripts/build-version.js` + comentario que la ata al Dockerfile
- [x] `frontend/Dockerfile`: `COPY scripts ./scripts` → `COPY scripts/build-version.js ./scripts/build-version.js` (falla ruidoso e inmediato si el contexto no lo trae)

## Validación

Docker Desktop no levanta en esta máquina (el proceso muere al arrancar), así que se validó la misma
cadena que corre dentro de la imagen, fuera de Docker, con el Node portable 22.23.1:

- [x] `node scripts/build-version.js prepare` → sella `src/app/core/build-info.ts`
- [x] `yarn build --configuration docker` → exit 0; inicial 1.94 MB contra el tope de **error** de 2.5 MB de esa configuración (solo los 2 warnings de budget preexistentes)
- [x] `node scripts/build-version.js emit` → `dist/browser/version.json` con el buildId
- [x] El `BUILD_ID` quedó DENTRO de `main-*.js` (confirma que el sellado en dos fases no muta el output ya hasheado)
- [x] `src/app/core/build-info.ts` restaurado a `'dev'`; no se commitea el timestamp
- [x] Auditado el resto del job, que en CI nunca se ejecutó: assets salen de `src/` (copiado), no hay `public/` ni `ngsw-config.json`, y el paso "Validar nginx…" extrae su asset de referencia con un patrón que sí matchea (`polyfills-*.js`)
- [x] El paso "Validar nginx y política de caché del borde" corrió por primera vez en CI y pasó sus 13 chequeos (`Borde OK`), con `polyfills-5CFQRCPP.js` como asset de referencia — el patrón sí matchea

## Despliegue

- [x] Commit quirúrgico en `main` (solo el fix; el árbol tenía trabajo en curso de otra sesión, no se tocó)
- [x] Push a `main` → PR [#50](https://github.com/ItalcolColombia/App_SanMarino/pull/50) → merge → run [30292942680](https://github.com/ItalcolColombia/App_SanMarino/actions/runs/30292942680) **verde** (tests 1m9s · backend 6m30s · frontend 5m8s)
- [x] Verificación post-deploy en AWS: front TaskDef `sanmarino-front-task:134` y back `sanmarino-back-task:136`, ambos PRIMARY/COMPLETED 1/1, ambas imágenes con el tag `690053e0…` = SHA de `main-produccion`. Sin rollback silencioso.
- [x] Verificación en vivo (`https://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com`): `/version.json` → 200 `application/json` `no-cache` con `buildId 2026-07-27T18:24:04.616Z` (el de este run) · `/` → 200 con CSP y HSTS · `chunk-inexistente.js` y `ngsw.json` → 404 · `/lotes` → 200

---

# Tracker — Hora de encasetamiento en lotes que YA tienen seguimientos (retroactivo)

**Plan:** [fase_de_desarrollo/hora_encasetamiento_primer_registro_plan.md](fase_de_desarrollo/hora_encasetamiento_primer_registro_plan.md)
**Fecha:** 2026-07-27 · Continuación del commit f5765c7

Análisis exhaustivo con workflow de 56 agentes + verificación adversarial (45 hallazgos confirmados
sobre 50). Medición sobre el dump de prod: **101 de 102** lotes engorde ya arrancan después del día
del encaset ⇒ el radio real de impacto son **3 lotes** (1 engorde + 2 reproductoras del ejercicio).

## Fase 1 — cerrar fugas (NO toca datos históricos) — HECHA

- [x] **Regresión de f5765c7**: `horaEncasetamiento` no viajaba en el `save()` individual de lote
      reproductora (alta ni edición); solo en `saveBulk()`. La hora era inalcanzable desde la UI y
      cada edición la apagaba en silencio
- [x] `EncasetamientoRetroactivoCalculos` (puro): diagnóstico de compatibilidad hora ↔ registros existentes
- [x] `EncasetamientoRetroactivoCalculosTests` — 9 casos (incluye el caso real del ejercicio)
- [x] PUT del lote engorde: diagnostica antes de escribir la hora y rechaza con detalle
- [x] PUT del lote reproductora: idem
- [x] **Fuga principal**: el formulario diario de engorde no validaba la fecha contra el encaset — ni
      siquiera «no anterior al encaset». El Excel rechazaba lo que la pantalla aceptaba. Guarda
      agregada en Create y Update de los DOS services de engorde
- [x] `dotnet build` 0 errores/0 advertencias · `dotnet test` 1029/1029 · `ng build` 0 errores

## Fase 2 — decisiones del usuario aplicadas (27-jul-2026)

**Decisión 1:** «organizar» = **solo corregir la numeración en pantalla**. NO se mueve ninguna fecha
de registro ⇒ cero riesgo sobre datos históricos, kardex, informe semanal y liquidaciones.
**Decisión 2:** la regla de las 13:00 aplica a **una sola empresa** ⇒ flag por empresa.

- [x] `companies.primer_registro_segun_hora_llegada` (bool, default false) + configuración
- [x] Migración schema `20260727182440` (ADD COLUMN IF NOT EXISTS) + seed `20260727182540` para
      **ItalcolPanama** (verificado: el lote 142 «13 - 1» del ejercicio es de esa empresa)
- [x] `EncasetamientoCalculos.HoraEfectiva(hora, reglaActiva)`: con la regla apagada devuelve null ⇒
      la hora se ignora y la empresa queda byte a byte como antes
- [x] `PrimerRegistroPorHoraGate` (fail-closed): un único punto de resolución del flag para los 5
      puntos de captura + los 2 PUT de lote, para que la regla no se aplique distinto según el canal
- [x] Gate aplicado en: formulario diario reproductora (Create/Update), formulario diario engorde x2
      (Create/Update), carga masiva reproductora, carga masiva engorde, PUT lote engorde, PUT lote repro
- [x] El flag viaja en TODAS las proyecciones de `CompanyDto` (ToDto, Crud, Resolver, CompanyPais)
- [x] Front: flag en `ActiveCompanyConfigService` (fail-closed) + azúcar `primerRegistroSegunHoraLlegada()`
- [x] **Columna «Día»**: en un lote tardío la semana se numera 1..7 (antes 2..8). Es presentación pura;
      la edad real, la guía genética, los indicadores y el informe semanal NO se tocan
- [x] El modal de seguimiento recibe la hora solo si el flag está activo
- [x] `dotnet build` 0 errores/0 advertencias · `dotnet test` 1029/1029 · `ng build` 0 errores
- [x] SQL de las migraciones verificado con `dotnet ef migrations script`

**Descartado explícitamente:** mover las fechas de los registros (+1 día). Quedó probado que es
técnicamente viable (solo fila por fila en orden DESC), pero cambiaría kardex de alimento, informe
semanal y días de ciclo de lotes ya liquidados. El usuario eligió no tocar datos históricos.

## Bugs preexistentes detectados (independientes, sin tocar)

- [ ] `min` faltante en el input de fecha del modal de seguimiento engorde
- [ ] `SeguimientoDiarioLoteReproductoraService.DeleteAsync` no anula `INV_CONSUMO` ⇒ consumo duplicado
- [ ] Tope `totalRegistros >= 7` cuenta filas, no edades ocupadas (hay 8 slots 0..7 y 7 cupos)
- [ ] `UpdateLoteAveEngordeDto.HoraEncasetamiento` no distingue «no enviado» de «borrar» (clientes no-UI)
- [ ] El PUT del lote engorde no valida `EstadoOperativoLote` ⇒ acepta editar un lote liquidado

---

# Despliegue a producción — 2026-07-27 19:42 UTC

Run [30299439870](https://github.com/ItalcolColombia/App_SanMarino/actions/runs/30299439870) **verde**
(tests 1m18s · backend 5m19s · frontend 4m22s), vía PR [#51](https://github.com/ItalcolColombia/App_SanMarino/pull/51).

Contenido: `56edf3a` (regla de la hora de llegada por empresa) · `7639b79` (validar la hora contra los
seguimientos ya cargados) · `528b283` (encabezados MIXTOS que se leían en CERO) · `b0e38d3` (tracker).

## Gates antes de mergear

- [x] `dotnet build` → 0 errores / 0 advertencias
- [x] `dotnet test` → **1032/1032** (1031 Application + 1 Domain)
- [x] `yarn build` → exit 0 (solo el warning de bundle budget preexistente)
- [x] Migraciones revisadas: `20260727182440` (`ADD COLUMN IF NOT EXISTS`, `NOT NULL DEFAULT false`) y `20260727182540` (data-only, lookup por `name` + `IS DISTINCT FROM`), el seed ordenado después de la columna
- [x] `ItalcolPanama` existe con ese nombre exacto (id 5) ⇒ el seed matchea y no queda en no-op silencioso
- [x] Estado resultante en la BD local (derivada de dump de prod): Panamá `true`, las otras 4 empresas `false`

## Verificación post-deploy

- [x] `sanmarino-back-task:137` y `sanmarino-front-task:135`, ambos PRIMARY/COMPLETED 1/1, ambas imágenes con el tag `3c857860…` = SHA de `main-produccion`
- [x] Eventos del servicio backend: `deployment completed` + `has reached a steady state`, con drenaje normal de la tarea vieja. **Sin crash loop**, o sea que las dos migraciones se aplicaron bien al arrancar
- [x] Front en vivo: `/version.json` → `buildId 2026-07-27T19:50:20.123Z` (el de este run) · `/` → 200
- [x] Backend en vivo a través del ALB: 401 en endpoints protegidos ⇒ sirviendo y aplicando auth

---

# Hotfix — reCAPTCHA del login bloqueado por la CSP en producción

Plan: [fase_de_desarrollo/csp_recaptcha_login_plan.md](fase_de_desarrollo/csp_recaptcha_login_plan.md)

Causa: la CSP centralizada de la Fase 0.C (76a2903) empezó a aplicarse de verdad y su
`script-src`/`frame-src` no permitían los orígenes de Google reCAPTCHA ⇒ el widget no se
renderiza en el login de prod (el build SÍ es de producción; verificado en el bundle vivo).

- [x] Diagnóstico verificado contra prod (bundle con siteKey + CSP en vivo sin google)
- [x] `nginx-security-headers.conf`: `script-src` + google/gstatic recaptcha, `frame-src` explícito
- [x] Validación sin Docker (no levanta local): chequeo estático de la línea + gate C5 del pipeline valida la CSP en contenedor antes de publicar (checks nuevos de recaptcha)
- [ ] Commit
- [ ] Deploy (push a main-produccion) + verificación post-deploy (CSP en vivo + widget visible)

---

# Pollo engorde: numeración de día 1-based y pesaje al cierre de semana

Plan: [fase_de_desarrollo/dia_negocio_engorde_pesaje_plan.md](fase_de_desarrollo/dia_negocio_engorde_pesaje_plan.md)

La tabla de seguimiento de engorde arranca en «Edad 0» y el pesaje semanal se pide un día tarde
(edad 7 = primer día de la semana 2). La regla de la hora de llegada nunca se cableó en engorde.

**Decisiones:** solo pantalla + validaciones (la edad técnica, la guía genética, los indicadores y el
informe semanal NO se tocan) · la columna conserva el encabezado «Edad (días vida)» con número
1-based · el corrimiento del pesaje aplica SOLO con `primer_registro_segun_hora_llegada` activo.

## Backend

- [x] `EncasetamientoCalculos`: `DiaDeNegocio` + `SemanaDeNegocio` (puros)
- [x] `PesajeEngordeCalculos` nuevo: `EsDiaDePesajeObligatorio(dia)`
- [x] Carga masiva engorde: la advertencia de pesaje usa el día de negocio si el flag está activo
- [x] Tests xUnit nuevos + regresión con flag OFF

## Frontend

- [x] `engorde-comun/funciones/dia-negocio-engorde.funcion.ts` (espejo puro del backend)
- [x] Lista de seguimiento engorde: lee el flag y propaga la hora del lote
- [x] Tabla: columna Edad, columna Semana, filtro de semana y Excel sobre el día de negocio
- [x] Modal de seguimiento: `esPrimeraSemana` / `esDiaPesoObligatorio` sobre el día de negocio

## Validación

- [x] `dotnet build` + `dotnet test`
- [x] `yarn build`

---

# Alimento (ingresos/traslados) en el mismo archivo de carga masiva de engorde

Plan: [fase_de_desarrollo/carga_masiva_alimento_engorde_plan.md](fase_de_desarrollo/carga_masiva_alimento_engorde_plan.md)

Caso testigo: galpon 6 (`G0471`) de DAYLAND (granja 107, ItalcolPanama), lote `13 - 1` (id 142).
**Meta: dejar 2.235,33 kg en el inventario del galpon.**

**Decisiones del usuario:** hoja `Alimento` nueva en el mismo .xlsx - el consumo diario descuenta SOLO
por la via `Alimento 1/2 Mixto` (no se toca el backend del consumo directo) - la primera semana
(reproductora) tambien debe descontar.

## Fase 0 - Diagnostico

- [x] Balance verificado al kilo: 155.188,243 ingresado - 7.166,829 (semana 1) - 145.786,084 (dias 8-41) = **2.235,330**
- [x] Dry-run real contra `POST /api/Migracion/validar`: 2 filas con error (`PREINICIO`/`INICIO`/`ENGORDE` no son alimentos del catalogo)
- [x] Inventario de la granja 107 confirmado **vacio** (0 stock, 0 movimientos)
- [x] Confirmado que el consumo directo NO descuenta inventario (metadata null en las 34 filas)
- [x] Kardex cronologico: el saldo se va a negativo del 28/06 al 12/07 (fondo -10.634,13 el 05/07) => el orden de proceso (alimento primero) es parte del contrato
- [x] Verificado que el desglose por fases del usuario coincide al kilo con el agotamiento real (residuo 0,069 kg ajustado el 23/06)

## Fase 1 - Fix de usabilidad: mensajes con el titulo mixto

- [x] `FilaCruda.Encabezados` (clave normalizada -> texto original del Excel) + helper `EtiquetaColumna`
- [x] `LeerAlimentoSlot`, aviso de consumo ignorado y aviso de pesaje citan la columna real del archivo
- [x] Verificado en vivo: los errores ahora dicen `Alimento 1 Mixto` / `Peso Mixto (g)` (antes `Alimento 1 H`, columna inexistente en la plantilla mixta)

## Fase 2 - Hoja `Alimento`

- [x] `MigracionEsquemas.AlimentoEngorde` (14 columnas; solo Fecha/Alimento/Cantidad obligatorias)
- [x] `Application/Calculos/MigracionAlimentoCalculos.cs` (puro: movimiento, origen, simulacion, proyeccion, clave de idempotencia)
- [x] `MigracionService.AlimentoEngorde.cs` (partial nuevo: leer/validar/aplicar, delegando en `IInventarioGestionService`)
- [x] `LeerHojaOpcionalConEsquema`: hoja ausente = sin error (archivos previos intactos)
- [x] Orquestacion en `ProcesarSeguimientoEngordeAsync` (alimento -> simulacion -> seguimiento)
- [x] Simulacion fail-closed con el faltante exacto en kg; nada se inserta
- [x] Idempotencia por (movimiento, ubicacion, item, fecha, cantidad, referencia)
- [x] **Fix**: `decimal` conserva la escala; `2717.5` (Excel) y `2717.500` (numeric) daban claves distintas y el reintento duplicaba 2 ingresos. Formato fijo `0.000`
- [x] **Fix**: archivo con SOLO ingresos (hoja Datos vacia) cortaba como "archivo vacio" antes de leer la hoja
- [x] Plantilla: hoja `Alimento` + dropdown de alimentos + instrucciones
- [x] `MigracionAlimentoCalculosTests.cs` (49 casos)

## Fase 3 - Reproductora engorde descuenta la primera semana

- [x] `Alimento 1/2 H-M` en `MigracionEsquemas.SeguimientoReproductoraEngorde` (opcionales, aditivas)
- [x] Lectura en `MigracionService.SeguimientoReproductora.cs` (reusa `LeerAlimentoSlot`)
- [x] **Fix**: `CreateSeguimientoDiarioLoteReproductoraRequest.BuildMetadata` no persistia `itemInventarioEcuadorId` => el consumo NUNCA descontaba (registro guardado, inventario intacto)
- [x] Verificado en vivo: 300 + 450,5 kg descontados del galpon, movimientos fechados 12/06 y 13/06

## Fase 4 - Kardex ordenado

- [x] `FechaMovimiento` en `InventarioGestionConsumoRequest` (default null = comportamiento previo)
- [x] `RegistrarConsumoAsync` usa `ResolveMovimientoCreatedAt` (simetria con `RegistrarIngresoAsync`)
- [x] Seguimiento engorde y reproductora pasan la fecha del registro
- [x] Verificado: 24 ingresos 04/06-18/07 y 36 consumos 15/06-18/07 (antes todos caian el dia de la carga)

## Fase 5 - Front

- [x] El saldo proyectado por alimento viaja como advertencia y el reporte existente ya lo renderiza (sin cambio de flujo)
- [x] Columna "Fila" muestra "-" en los mensajes de archivo (fila 0), no un "0" que manda a buscar una fila inexistente

## Fase 6 - Verificacion punta a punta (backend de prueba en :5011, copia aislada)

- [x] Archivo real armado: `CargaMasiva_Engorde_GALPON6_con_ALIMENTO.xlsx` (Datos 34 filas + Alimento 24 filas)
- [x] Dry-run limpio: `Validado`, 0 errores, saldo proyectado **2.235,331 kg**
- [x] Import real: 58 filas procesadas, stock del galpon **2.235,331 kg** en AV. SUPER POLLO ENGORDE (INICIACION en 0)
- [x] Reintento idempotente: 0 procesadas / 58 omitidas, stock sin cambios
- [x] Fail-closed: quitando un ingreso de 14.239,771 kg => rechazo con "faltan 12.004,440 kg" y **cero** filas insertadas
- [x] Regresion: archivo sin hoja `Alimento` se comporta igual que antes (mismos 2 errores de fila)
- [x] Archivo con solo ingresos (hoja Datos vacia): 24 movimientos aplicados
- [x] `dotnet build` 0 errores / 0 advertencias - `dotnet test` **1153/1153** - `yarn build` OK (solo el warning de bundle budget preexistente)
- [x] BD local restaurada al estado previo (galpon 6 intacto: 41 seguimientos + 14 de reproductora) y sin procesos huerfanos

## Fase 7 - Movimiento `Consumo` en la hoja `Alimento`

Necesario para reparar lotes ya cargados: los 7 dias de la primera semana viven en reproductora y estan
CONFIRMADOS -> `UpdateAsync` los rechaza ("El registro esta confirmado y no puede editarse") y
`DeleteAsync` exige reabrir el lote con novedad. Sin una salida manual, ese alimento queda como sobrante
fantasma para siempre. Espeja `POST /inventario-gestion/consumo`, que ya existe en la pantalla.

- [x] `MovimientoAlimento.Consumo` + alias en `TryMovimiento`
- [x] Opcion "Consumo" en la columna Movimiento del esquema
- [x] Se aplica con `RegistrarConsumoAsync` (fecha del movimiento = fecha de la fila)
- [x] Cuenta como SALIDA en la simulacion de balance
- [x] Idempotencia: solo los consumos con referencia propia entran (los del seguimiento llevan
      "Seguimiento aves engorde #..." y no deben taparse entre si)
- [x] Advertencia si un Consumo viene sin Referencia (dos salidas iguales del mismo dia se tomarian por repetidas)
- [x] Tests + instrucciones de la plantilla

## Fase 8 - REPARACION DEL GALPON 6 (lote 142, ejecutada)

Archivo: `REPARACION_GALPON6_DAYLAND_lote13-1.xlsx` (Datos 34 filas + Alimento 24 ingresos + 7 consumos).

- [x] Backup completo en `backup_g6/` (8 tablas: seguimientos, lote, reproductoras, inventario, historial)
- [x] Borrados los 34 seguimientos de los dias 8-41 via `DELETE /api/SeguimientoAvesEngorde/{id}` (34/34 OK).
      Aves devueltas correctamente: hembras 22.816 -> 24.265 (+1.449 = mortalidad de esos dias)
- [x] Los 7 dias de cruce (origen_cruce) NO se tocaron
- [x] Dry-run: `Validado`, 0 errores, saldo proyectado 2.235,332
- [x] Import: 65 filas procesadas (34 seguimientos + 24 ingresos + 7 consumos)
- [x] **Inventario del galpon 6 = 2.235,332 kg** (PREINICIADOR 0,000 - INICIACION 0,000 - ENGORDE 2.235,332)
- [x] Kardex: 24 ingresos 04/06-18/07 + 43 consumos 08/06-18/07, con referencias `SEM1-*` (primera semana)
      y `Seguimiento aves engorde #...` (dias 8-41)
- [x] Aves del lote de vuelta en 22.816 / 24.165 / 0 (identico al estado previo)
- [x] Consumo total del lote 152.952,912 kg - identico al backup
- [x] Reintento idempotente: 0 procesadas / 65 omitidas
- [x] Unico dato distinto (y es una CORRECCION): el backup traia peso 572,04 repetido en 15/06, 22/06,
      29/06, 06/07 y 13/07 - restos del archivo `MIXTO 1`. Ahora los pesos quedan solo en los dias de
      pesaje reales (21/06 572,04 - 28/06 1.135 - 05/07 1.816 - 12/07 2.238,22), como en `MIXTO 2`
- [x] `dotnet build` 0/0 - `dotnet test` **1158/1158**

## Fase 9 - Hoja `Reproductora`: un solo cargue para todo el lote

Pedido: centralizar en un unico archivo. Cada hoja se identifica por NOMBRE y va a su modulo,
reutilizando las funciones que ya existen (no se duplica logica de validacion).

```
Excel  ├── Alimento       -> inventario (ingresos / traslados / recepciones / consumos)
       ├── Reproductora   -> seguimiento reproductora (dias 1-7, cruza solo a engorde)
       └── Datos          -> seguimiento engorde (dias 8+)
```

- [x] **Refactor sin cambio de comportamiento**: el parseo de reproductora sale de
      `ProcesarSeguimientoReproductoraAsync` a `ParsearFilasReproductoraAsync` (mismas reglas, mismos
      mensajes). La linea de migracion dedicada lo sigue usando igual
- [x] `MigracionEsquemas.ReproductoraEnHoja` = `SeguimientoReproductoraEngorde with { Hoja = "Reproductora" }`
      (mismas columnas, alias y orden: validar por una via u otra da identico resultado)
- [x] Lectura opcional de la hoja en `ProcesarSeguimientoEngordeAsync`
- [x] Orden de proceso: **Alimento -> Reproductora -> Datos** (el galpon tiene stock antes de que la
      primera semana y los dias 8+ lo consuman; la reproductora se confirma, que es lo que gatea el cruce)
- [x] El consumo de la primera semana entra en la simulacion de balance
- [x] Plantilla: la hoja se genera junto a Datos y Alimento + instrucciones del orden
- [x] Tests: nombres de hoja distintos, equivalencia columna a columna con la linea dedicada,
      claves de lectura identicas
- [x] **Smoke con las 3 hojas en un archivo** (lote 149): 36 filas procesadas
      - 28 registros de reproductora, todos CONFIRMADOS (3.080 kg)
      - 7 dias 1-7 de engorde generados por el CRUCE automatico (11/06-17/06)
      - 5 dias 8+ de engorde de la hoja Datos (19/06-23/06)
      - 3 ingresos de alimento (13.000 kg)
      - Inventario: PRE 1.920 / INI 550 / ENG 300 - exactamente lo proyectado en el dry-run
- [x] Fail-closed verificado en el mismo smoke: con ENGORDE en 3.000 kg y consumo de 3.700 rechaza
      con "faltan 700,000 kg"
- [x] Reintento idempotente: 0 procesadas / 36 omitidas, stock sin cambios
- [x] Regresion: la linea dedicada `SeguimientoReproductoraEngorde` (hoja "Datos") sigue funcionando
- [x] `dotnet build` 0/0 - `dotnet test` **1165/1165**
- [x] BD local limpia; galpon 6 sigue en **2.235,332 kg** y el lote 142 intacto (41 + 14 registros)

## Fase 10 - Recarga completa del galpon 6 con UN SOLO archivo (ejecutada)

Se limpio el seguimiento de reproductora y se recargo TODO el lote desde
`LOTE_13-1_GALPON6_COMPLETO_3HOJAS.xlsx` (Reproductora 14 + Datos 34 + Alimento 24).
Diferencia con la Fase 8: la primera semana ya NO entra como movimiento manual "Consumo" sino por su
propio camino -- el seguimiento de reproductora con `Alimento 1 H/M` -- asi que el kardex queda
trazable registro por registro.

### Limpieza (por los endpoints oficiales, no por SQL)

- [x] Backup v2 del estado post-reparacion (`backup_g6_v2/`, 6 tablas)
- [x] `POST /api/LoteReproductoraAveEngorde/{61,62}/reabrir` con novedad -> HTTP 200
- [x] 14 seguimientos de reproductora borrados por endpoint (permisos
      `seguimiento_reproductora_engorde.eliminar`; sin el, 403)
- [x] El trigger de cruce borro SOLO los 7 dias 1-7 de engorde (41 -> 34), como debe
- [x] 34 seguimientos de engorde borrados por endpoint (aves devueltas)
- [x] Inventario del galpon a cero

### Carga y resultado

- [x] Dry-run: `Validado`, 0 errores, saldo proyectado 2.235,332 (los 3 alimentos)
- [x] Import: **72 filas** (14 reproductora + 34 engorde + 24 ingresos), 0 errores
- [x] Reproductora: 14 registros, **los 14 confirmados**, 7.166,832 kg
- [x] Engorde dias 1-7: 7 registros generados por el CRUCE automatico (08/06-14/06)
- [x] Engorde dias 8-41: 34 registros (15/06-18/07)
- [x] **Inventario del galpon 6 = 2.235,332 kg** (PREINICIADOR 0,000 - INICIACION 0,000)
- [x] Kardex 100% trazable: 24 ingresos `LLEG-*` + 14 consumos `Seguimiento reproductora #...`
      + 36 consumos `Seguimiento aves engorde #...` = 152.952,912 kg. Ningun movimiento manual
- [x] Reintento idempotente: 0 procesadas / 72 omitidas
- [x] Contra el backup: 41 seguimientos, consumo 152.952,912, mortalidad 1.830, aves 22.816 / 24.165,
      14 de reproductora y stock 2.235,332 -- todo identico
- [x] Lotes reproductora sin cambios salvo el rastro de la reapertura (`novedad_apertura` +
      `reabierto_at`), que es auditoria legitima
- [x] `dotnet build` 0/0 - `dotnet test` **1165/1165** - sin procesos huerfanos

## Fase 11 - Archivo unico definitivo con guia y ejemplos

`CARGA_UNICA_LOTE_13-1_GALPON6.xlsx` — fusion de los 3 archivos del usuario en uno solo, con seis hojas:

| Hoja | Filas | Se carga |
|---|---|---|
| GUIA | 32 lineas | no (explicacion + cuadre del lote) |
| Reproductora | 14 | SI - dias 1-7 (con `Alimento 1 H/M`) |
| Datos | 34 | SI - dias 8-41 (con `Alimento 1/2 Mixto`) |
| Alimento | 24 | SI - llegadas al galpon |
| EJEMPLOS | 7 casos | no (traslado, recepcion, consumo, quintales, bodega) |
| REFERENCIAS | catalogo | no (8 alimentos, ubicacion, reproductoras, movimientos) |

- [x] 17 comentarios de celda explicando cada columna sensible sobre el propio encabezado
- [x] Hoja GUIA con el cuadre del lote y la regla clave (solo `Alimento 1/2` descuenta inventario)
- [x] Hoja EJEMPLOS con los casos que este lote no tiene, listos para copiar
- [x] **Bug propio detectado por el sistema**: una nota explicativa en la celda de la columna
      "Reproductora" se leyo como el nombre de una reproductora -> `ConErrores`. Las explicaciones se
      movieron a comentarios de celda (que el importador nunca lee). El fail-closed hizo su trabajo
- [x] **Fix de redondeo**: los kg reservados para la semana 1 se calculan sumando las CELDAS ya
      redondeadas a 3 decimales, no los decimales completos; con la suma original el PREINICIADOR
      quedaba con 1 g de sobrante inexistente
- [x] Validado e importado contra el backend: `Validado` 0 errores -> `Procesado` 72 filas
- [x] **Inventario: PREINICIADOR 0,000 · INICIACION 0,000 · ENGORDE 2.235,332 = 2.235,332 kg**
- [x] Reintento idempotente: 0 procesadas / 72 omitidas, stock sin cambios
- [x] Estado final del lote 142: 41 seguimientos + 14 de reproductora, aves 22.816 / 24.165
