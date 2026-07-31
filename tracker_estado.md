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

## Fase 12 - Validacion de edad, descuento de aves y cuadre (lote 142)

### Resultado de la validacion

| Punto | Estado |
|---|---|
| Numeracion desde dia 1 (engorde) | **OK** - edad backend 0..40 -> pantalla dia 1..41 |
| Numeracion desde dia 1 (reproductora) | **OK** - edad 0..6 -> dia 1..7, ambas reproductoras |
| Descuento de aves en el REPORTE | **OK** - 48.430 - 1.830 = 46.600 |
| Descuento de aves en el MAESTRO | **BUG** - 46.981 (+381, las bajas de los dias 1-7) |
| Inventario de alimento | **OK** - kardex y stock coinciden en 2.235,332 |
| Saldo de alimento del REPORTE | **BUG** - 12.869,459 (+10.634,13) |

### Bug 1 - las bajas de los dias 1-7 no llegaban al maestro (CORREGIDO)

Los dias 1-7 los inserta el trigger SQL del cruce, sin pasar por el service, asi que su mortalidad
nunca movia `hembras_l/machos_l`. El maestro es el stock que valida ventas y traslados: el sistema
habria dejado despachar 381 aves ya muertas.

- [x] `RetiroAvesEngordeAplicador.SincronizarCruceAsync`: aplica las bajas del cruce con la MISMA
      logica idempotente de los dias 8+; revierte las filas de historico cuyo seguimiento ya no existe
      (el cruce borra y recrea sus registros al regenerarse)
- [x] Llamado desde `ConfirmarAsync` (tambien en el camino idempotente, que es la via de backfill de
      lotes ya cargados) y desde `DeleteAsync` de reproductora
- [x] Verificado en el lote 142: maestro 46.981 -> **46.600**, hembras -197 y machos -184
- [x] Idempotente: 4 confirmaciones seguidas dejan el maestro en 46.600

### Bug 2 - el saldo de alimento del reporte se inflaba (CORREGIDO A MEDIAS)

El piso en 0 recortaba el saldo cada dia que se iba negativo; como las llegadas estan fechadas
despues del consumo, cada recorte regalaba alimento inexistente.

- [x] `SeguimientoAvesEngordeCalculos`: quitados los dos pisos (apertura y bucle principal)
- [x] `fn_seguimiento_diario_engorde` **v9**: quitado el reseteo de base de Lindley (deshace el M1 de
      v6, may-2026) en la apertura y en la columna final. **Habia DOS motores del mismo saldo** y el
      diagnostico inicial solo vio el de C#; sin esto quedaban desalineados
- [x] Tests actualizados + 2 nuevos con el caso real (llegadas fechadas despues del consumo)
- [x] Los dos motores ahora coinciden: -9.894,306 en ambos
- [x] Corregido tambien el tercer factor (ver abajo) -> el reporte cierra contra el inventario

### Hallazgo 3 - alimento anterior al encasetamiento (CORREGIDO)

El calculo descarta los movimientos anteriores a la fecha de encaset (para no arrastrar alimento de
ciclos previos del galpon). Pero en engorde el PREINICIADOR llega antes que los pollitos:

```
Ingreso 04/06 (PREINICIADOR, 4 dias antes del encaset)   12.129,638 kg  <- descartado
Ingresos desde el encaset (25 movimientos)              147.231,698 kg
Saldo del reporte                                        -9.894,306 kg
Stock real                                                2.235,332 kg
Diferencia                                               12.129,638 kg  = el ingreso descartado
```

**Decision del usuario: ventana previa configurable.**

- [x] `Application/Calculos/VentanaAlimentoPrevioCalculos.cs` (puro): default 10 dias, tope 30,
      normalizacion fail-safe. 10 queda por debajo del vacio sanitario tipico (10-14) para que la
      ventana no alcance el cierre del ciclo anterior
- [x] `companies.dias_alimento_previo_encaset` (int NOT NULL DEFAULT 10) - parametro operativo, no
      flag de comportamiento: cada empresa lo ajusta a su vacio sanitario
- [x] Aplicado en el C# (`SeguimientoAvesEngordeCalculos`, el service lee el valor de la empresa) y
      en la fn SQL v9 (`lote_info.fecha_corte_alimento`, join a `companies`)
- [x] Migracion `20260728045739_AddVentanaAlimentoPrevioEncaset` (columna + fn v9, idempotente),
      generada con EF tools 10 y verificada aplicandose sola al arrancar
- [x] `VentanaAlimentoPrevioCalculosTests` (12 casos) + 1 test nuevo del saldo con ventana

### CUADRE FINAL DEL LOTE 142 (verificado punta a punta)

| | reporte | referencia | |
|---|---|---|---|
| Dia del primer registro | 1 | edad 0 | OK |
| Dia del ultimo registro | 41 | edad 40 | OK |
| Aves ultimo dia | 46.600 | maestro 46.600 = encasetadas - bajas | **OK** |
| Alimento ultimo dia | 2.235,332 kg | stock real 2.235,332 kg | **OK** |

El saldo diario ahora es honesto: el 17/07 marca -1.133,080 kg (ese dia el consumo supero lo
recibido) y el 18/07 se recupera a 2.235,332 con la ultima llegada.

- [x] `dotnet build` 0/0 - `dotnet test` **1185/1185** - `yarn build` OK

### Validacion

- [x] `dotnet build` 0/0 - `dotnet test` **1167/1167**
- [x] `fn_seguimiento_diario_engorde` v9 aplicada en la BD local
- [ ] Migracion EF para publicar la fn v9 (pendiente de la decision del hallazgo 3)

## Fase 13 - Validacion sobre la BD de PRODUCCION + reemplazo de dias ya cargados

BD local reemplazada por el dump de produccion (183 migraciones, sin la nuestra). Estado del lote 142
en prod: **virgen** — 0 seguimientos de engorde, 0 de reproductora, 0 stock, aves intactas
(24.265 H + 24.165 M = 48.430).

### Migracion aplicada sola al arrancar

- [x] `20260728045739_AddVentanaAlimentoPrevioEncaset` (columna + fn v9) se aplico al bootear
- [x] Las 5 empresas quedaron con `dias_alimento_previo_encaset = 10`

### Carga del archivo unico sobre datos de produccion

- [x] Dry-run: `Validado`, 0 errores, saldo proyectado 2.235,332
- [x] Import: **72 filas** (14 reproductora + 34 engorde + 24 ingresos), 0 errores
- [x] 41 dias del **1 al 41**, 14 registros de reproductora confirmados
- [x] AVES: encasetadas 48.430 - bajas 1.830 = **46.600**, y el maestro tambien 46.600
- [x] ALIMENTO: reporte **2.235,332 kg** = stock real 2.235,332 kg
      (PREINICIADOR 0,000 - INICIACION 0,000 - ENGORDE 2.235,332)

### Cambio pedido: una fecha ya cargada se REEMPLAZA, ya no se omite

- [x] `existentes` pasa de HashSet a diccionario con el id del registro
- [x] Las filas ya cargadas van a `actualizables` -> `UpdateAsync`, que ajusta aves e inventario por
      la DIFERENCIA contra lo que habia (reemplazar con los mismos valores no mueve nada)
- [x] **Los dias 1-7 del cruce siguen omitiendose**: su fuente es reproductora y pisarlos desde la
      hoja Datos los dejaria peleados con su origen
- [x] El dry-run AVISA cuantos dias se van a reemplazar y con que fechas
- [x] **Bug encontrado y corregido**: EF reventaba con `Unexpected entry.EntityState: Detached` en la
      segunda actualizacion. El DbContext es scoped y arrastra todo el import; `UpdateAsync` recalcula
      el saldo de TODO el lote y deja los 41 seguimientos en el tracker. Se limpia el ChangeTracker
      antes de cada actualizacion
- [x] Probado con 2 dias modificados: mort 29->50 y 20->5, consumo 2.086,546->2.500 y ->1.673,092
      - dias: siguen 41 (no duplica)
      - aves: 46.600 -> **46.594** (-6, exactamente el delta de mortalidad)
      - stock: **2.235,332 sin cambios** (el consumo total no vario)
- [x] Restaurado con el archivo definitivo: todo vuelve a 46.600 aves y 2.235,332 kg

- [x] `dotnet build` 0/0 - `dotnet test` **1185/1185**

## Fase 14 - Fix del saldo proyectado al recargar un archivo ya importado

Reportado desde la pantalla: al validar el archivo con el lote YA cargado, el reporte anunciaba
"Saldo proyectado de AV. SUPER POLLO ENGORDE: 2.235,332 inicial + 122.923,435 entradas − 120.688,103
consumo = **4.470,664 kg**" — el doble del real. Importar no habria hecho eso (la idempotencia omite
los movimientos), pero el numero asustaba y era falso.

### Causa

La simulacion de balance sumaba TODO lo del archivo, sin descontar lo que se iba a omitir:

- las 24 entradas de la hoja `Alimento` ya estaban aplicadas -> se omiten, pero se contaban
- los 34 dias de la hoja `Datos` se ACTUALIZAN, o sea que su salida neta es el DELTA contra lo ya
  descontado, no el consumo entero
- los 14 dias de reproductora omitidos no aportaban su consumo, y las entradas si -> descuadre

### Correcciones

- [x] `ClavesMovimientosExistentesAsync` se consulta ANTES de la simulacion; los movimientos ya
      aplicados no entran como entradas
- [x] Para los dias que se actualizan, la salida es `consumo nuevo − consumo ya descontado`
      (se lee del `metadata` del registro existente). Un alimento que el dia tenia y el archivo ya no
      trae se devuelve al galpon
- [x] El reporte filtra por `!= 0` y no por `> 0`: bajar el consumo de un dia da movimiento NEGATIVO
      y con `> 0` no se informaba
- [x] Mensaje legible cuando el movimiento es negativo: "+ 500,000 devueltos (el archivo baja el
      consumo ya cargado)" en vez de "− -500,000"

### Front: solo-advertencias ya no se ve como un fallo

El panel decia "4 registro(s) — no se inserto ninguna fila con error" aunque los 4 fueran avisos y el
archivo estuviera OK.

- [x] `hayErroresReales()` / `conteoErrores()`: con solo advertencias el panel es AMBAR y dice
      "✅ Sin errores — N aviso(s) para revisar antes de importar"
- [x] Con errores reales se conserva el panel rojo y el texto original

### Verificacion (BD de produccion)

- [x] Revalidar el archivo con el lote ya cargado: **ya no proyecta saldo** (no hay movimiento neto)
- [x] Reimportarlo: stock 2.235,332 y aves 46.600 **sin moverse**
- [x] Archivo con 500 kg MENOS de consumo el 18/07: proyecta "2.235,332 + 500,000 devueltos =
      2.735,332" y al importar el stock queda exactamente en 2.735,332
- [x] Restaurado con el archivo definitivo: 41 dias (1 al 41), 46.600 aves, 2.235,332 kg
- [x] `dotnet build` 0/0 - `dotnet test` **1186/1186** - `yarn build` OK

---

# Tracker — Informe RA Pesadas (Parámetros + Gráficos)

**Plan:** [`fase_de_desarrollo/informe_ra_pesadas_parametros_plan.md`](fase_de_desarrollo/informe_ra_pesadas_parametros_plan.md)
**Fecha:** 2026-07-28
**Fuente:** `Requerimiento sanmarino 2026/Informe RA Pesadas Parámetros - Gráficos 2025 v1.xlsb`

Decisión de arquitectura: **NO es un módulo nuevo ni varios reportes sueltos** — se extiende
`reporte-tecnico-semanal` (que ya cubre 80-85 % de las hojas 2/3/4/6/7) con dos modos:
Resumen (todos los lotes × 1 semana) y Detalle de lote (el actual + 2 tabs nuevos).

## Fase 0 — Validación (CERRADA)

- [x] Lectura y volcado de las 10 hojas del `.xlsb` (pyxlsb)
- [x] Mapeo hoja por hoja contra lo ya implementado (`ReporteTecnicoSemanal`, fns SQL, front)
- [x] Granularidad verificada: clave `(lote, edad)` única en LEV (1.825) y PROD (2.960) ⇒ nivel lote
- [x] `GRANJA` del Excel = granja + núcleo (`Niza 3 mod 1` = NIZA III / Modulo I)
- [x] Guía genética: app tiene 2021/2022/2023/**2026**/G21; el Excel compara contra 2024/2025/2025EC
- [x] Completitud de la guía 2026 AP auditada (77 filas: mort 77, apareo 77, alim 77, unif 25, masa 52)
- [x] Huecos H1-H8 documentados con evidencia (§4 del plan)
- [x] Llenado real de columnas dudosas medido: GrasaH 0 %, PechugaM 0 %, Fertilidad 0 %, Venta 3 %
- [x] Regional `ECUADOR` inexistente en `master_lists`; PIMAN/PARAISO mal clasificadas
- [x] `catalogo_items.metadata` sin energía/proteína ⇒ nutrición de machos no calculable hoy
- [x] Plan escrito con decisiones D1-D5

## Fase 1 — Decisiones (CERRADA 2026-07-28)

- [x] **D1** → **extender la guía 2026 AP hasta la semana 97** con la curva de reciclaje del Excel
- [x] **D2** → bonificación **fuera** de la fase 1
- [x] **D3** → **no** se crea la regional `Ecuador` (era pseudo-regional del Excel; Ecuador aún no tiene postura)
- [x] **D4** → `VentaH`/`VentaM` **sí**, mapeados a movimientos de aves
- [x] **D5** → **sí**, energía/proteína en `catalogo_items.metadata`
- [x] Hallazgo: las 5 guías `*R` del Excel son **una sola curva de 28 semanas relativas**, desplazada por
      lote (arranques en edad 65/68/70/71) → la extensión fija deja hasta 6 semanas de desfase (§4.1 del plan)

### Bloqueantes previos a codificar (no son decisiones de diseño)

- [x] **D4 RESUELTO por el usuario**: venta = salidas del módulo **Movimientos de Aves** con tipo
      «Venta». El tipo sale de la lista maestra `movimiento_de_aves_tipo_movimiento` (hoy `Traslado`
      y `Venta`), NO de una constante ⇒ se compara por CONTENIDO en minúsculas, igual que el front
      (`esTipoVenta`), para que no se rompa con «Venta de aves» ni con un cambio de mayúsculas
- [ ] Confirmar si se implementa la variante anclada de la curva de reciclaje o la extensión fija (§4.1)
- [ ] Verificar en prod el `regional_id = 5` huérfano de la granja PIMAN

## Fase 2 — Resumen semanal (hoja 1) — NUEVO

### SQL (HECHO)

- [x] `backend/sql/fn_resumen_semanal_ra_pesadas_levante.sql` — set-based (CTEs + ventanas), NO itera lotes
- [x] `backend/sql/fn_resumen_semanal_ra_pesadas_produccion.sql` — sobre el flujo **LPP**, no el de `lotes`
      (el Detalle llama la fn base con `LotePosturaProduccionId`; usar `lotes.fecha_inicio_produccion`
      daría otra semana de vida y no cuadraría)
- [x] Semana del año = **WEEKNUM de Excel** (US, arranca domingo), no ISO — verificado 1825/1825 filas
      contra el archivo (ISO solo coincide en 1736)
- [x] `PART` = saldo hembras del lote / Σ saldo hembras de la selección
- [x] Arrastre (LOCF) del peso por sexo con ventanas, sin bucle
- [x] **Bug encontrado y corregido**: la edad de la guía se compara distinto en cada etapa —
      levante usa TEXTO EXACTO (`btrim(edad) = sem::text`) y producción PARSEA a número
      (`fn_parse_edad_numerica`). La semana 25 tiene dos filas (`'25'` cierre de levante,
      `'25P'` arranque de producción) y un `regexp` genérico tomaba la equivocada. El desempate
      quedó EXPLÍCITO en la fn de producción para no depender del plan de ejecución
- [x] Migración EF `20260728120000_AddFnResumenSemanalRaPesadas` (data-only, Designer clonado, idempotente)
- [x] Migración EF **solo-datos** `20260728120100_ExtenderGuia2026ApSemanas77a97` (D1): 21 filas,
      semanas 77-97, `INSERT ... WHERE NOT EXISTS`. Los errores `#DIV/0!` del Excel entran NULL
- [x] `dotnet build` 0 errores / 0 advertencias · migraciones aplicadas en local sin error

### Equivalencia Resumen ↔ Detalle (requisito duro) — VERIFICADA

- [x] Levante: 4 lotes × todas sus semanas = **79 filas, 0 diferencias** en 17 columnas contra
      `fn_reporte_semanal_levante_extras` + `fn_indicadores_levante_postura`
- [x] Producción: lote sintético (sembrado y revertido con ROLLBACK) = **8 semanas, 0 diferencias**
      en 16 columnas contra `fn_indicadores_produccion_postura`

### Backend C# (HECHO)

- [x] `ResumenSemanalRaPesadasDtos.cs`: request + filas levante/producción + totales + respuestas
- [x] `ResumenSemanalRaPesadasCalculos.cs` (PURO): semana WEEKNUM, rango de la semana,
      normalización de etapa, participación y promedios ponderados
- [x] **Participación recalculada tras el recorte por alcance** — la fn SQL la computa con una
      ventana sobre TODAS sus filas; si el backend quita lotes que el usuario no ve, las
      participaciones dejan de sumar 1 y los ponderados salen mal
- [x] `ReporteTecnicoSemanalService.Resumen.cs` (partial) + interfaz + `POST api/ReporteTecnicoSemanal/resumen`
- [x] Ponderado por saldo de hembras, NO promedio simple; los lotes sin valor no cuentan como 0
- [x] `dotnet build` 0 errores / 0 advertencias
- [x] `dotnet test` **1219/1219** (+33 nuevos en `ResumenSemanalRaPesadasCalculosTests`)

### Smoke del endpoint (HECHO — backend dev local, JWT minteado)

- [x] Levante Sanmarino sem 20/2025 → 2 filas, cifras idénticas a las de la fn SQL
      (K345A part 0,41948 saldo 7.726 · K345B part 0,58052 saldo 10.692)
- [x] Producción con datos → mapeo EF correcto en las 23 columnas (`htaa`, `hiaa`, `grHuevoInc`,
      `pesoMachoSobreHembra`, `lotePosturaProduccionId`…). Salieron 3 lotes en una semana
      (el sintético + los 2 LPP reales de local): una fila por lote, como debe ser
- [x] Etapa inválida → 400 · semana 99 → 400 · semana sin datos → 200 con lista vacía (no error)
- [x] Empresa Demo → 0 filas (sin fuga cross-empresa)
- [x] Semilla de prueba eliminada (0 filas ZZTEST, 4 LPP originales) y backend detenido (:5002 libre)

### Front (HECHO)

- [x] `models/resumen-semanal-ra-pesadas.model.ts` — espejo 1:1 de los DTOs
- [x] `funciones/columnas-resumen-ra-pesadas.funcion.ts` — spec ÚNICA de columnas: alimenta la
      tabla EN PANTALLA y el Excel (una columna nueva aparece en los dos lados)
- [x] `funciones/construir-aoa-resumen-ra-pesadas.funcion.ts` — hoja AOA del export
- [x] `funciones/semana-excel.funcion.ts` — WEEKNUM puro (solo para preseleccionar la semana)
- [x] `pages/resumen-semanal-main/` — filtros año/semana/etapa + regional/ciclo/traslado,
      tabla con cabeceras agrupadas y fila de TOTAL, export a Excel
- [x] `pages/informe-ra-pesadas-main/` — shell con los dos modos (Resumen / Detalle).
      La página del Detalle queda INTACTA; la ruta `/reporte-tecnico-semanal` se conserva
      (es la sembrada en `menus`/`role_menus`), solo apunta al shell
- [x] `changeDetection: ChangeDetectionStrategy.Eager` explícito en los dos componentes nuevos
- [x] Vista precalculada (celdas y totales) — el template no aloca por ciclo (NG0103)
- [x] Regional/Ciclo se resuelven en el BACKEND (son parámetros de la fn), no filtrando en cliente:
      filtrar en cliente dejaría la fila de totales peleada con las filas visibles
- [x] Arranca en la semana ANTERIOR, no en la actual: la semana en curso está incompleta y el
      reporte abría vacío, que se lee como si estuviera roto
- [x] `yarn build` — 0 errores (solo el warning preexistente de bundle budget)

### Smoke UI (HECHO — dev server + backend local, sesión inyectada)

- [x] Levante sem 20/2025 → 2 lotes con las MISMAS cifras que la fn SQL y el endpoint
      (K345A 41,95 % · 7.726 · unif 90,9 · %DifPeso −6,89)
- [x] Fila TOTAL: saldos suman (18.418 / 2.253) y los indicadores ponderan — unif 93,0
      (el promedio simple daría 92,7, así que la ponderación se ve)
- [x] Producción sem 30/2025 → 2 lotes, cabeceras agrupadas correctas, `% Guía` vacío en la
      semana 25 (comportamiento REQ-012b ya conocido, igual que el Detalle)
- [x] Semana sin datos → mensaje con el rango de fechas, no error
- [x] Cambio de modo Resumen → Detalle → Resumen dos veces, sin quedarse colgado
- [x] Consola del navegador sin errores

### Pendiente

- [x] Columnas `Venta H/M` en el Detalle de producción (grupo «Venta aves»)
      - [x] Solo cuentan las **Completadas**: una venta pendiente o cancelada no sacó aves
      - [x] Lote de **origen** (una venta es una salida) y agrupadas por semana de vida con la misma
            fórmula que «HI Cargado», para que caigan en la misma fila
      - [x] Las anteriores al encaset se ignoran (dato inconsistente, no una «semana 0»)
      - [x] En el consolidado SUMAN entre galpones (son conteos de aves)
      - [x] 5 tests nuevos · smoke con datos sembrados: 1.500 H / 100 M en la semana 28 sumando dos
            ventas completadas, dejando fuera una pendiente y un traslado; semilla eliminada

---

# Tracker — Carga masiva de Postura: alimento con inventario real, huevos completos y validaciones

**Plan:** [fase_de_desarrollo/migracion_masiva_postura_alimento_inventario_plan.md](fase_de_desarrollo/migracion_masiva_postura_alimento_inventario_plan.md)
**Fecha:** 2026-07-28

**Decisiones del usuario:** paridad total con Engorde (hoja `Alimento` + consumo por ítem que **descuenta**
stock + simulación de balance) · nivel de stock por el **flag efectivo `granja ?? empresa`** · huevos de
Producción **completos ahora**: las 11 categorías (Sanmarino) **y** `huevoItems` por flag (Santa Reyes) ·
**un lote por archivo**.

**Hallazgos que condicionan el diseño:** la carga masiva de postura hoy **no toca inventario** (la fn SQL lo
declara en su cabecera) mientras el alta manual sí valida stock y descuenta · `RegistrarIngresoAsync` **lanza**
si le mandan núcleo/galpón a una granja de nivel granja (Sanmarino y Santa Reyes lo son) ⇒ la ubicación por
defecto no puede ser la del lote tal cual · ambas tablas ya tienen `metadata jsonb` y las 11 columnas
`huevo_*` ⇒ **cero migraciones de schema** · `FilasOmitidas` de postura hoy siempre reporta 0.

## Fase 0 — Análisis
- [x] Mapa del flujo vigente (elegibilidad, parseo, fn plpgsql, idempotencia, merge, descuento de aves)
- [x] Comparación efecto por efecto contra el alta manual (`SeguimientoLoteLevanteService.Crud` / `ProduccionService`)
- [x] Verificación en BD local de los flags por empresa y del nivel real del stock (Sanmarino/SR = granja)
- [x] Verificación de que `metadata` y las 11 columnas `huevo_*` ya existen en las dos tablas
- [x] Plan escrito en `fase_de_desarrollo/`

## Fase 1 — Backend: cálculo puro + esquemas + tests (gate CI)
- [x] `Application/Calculos/MigracionPosturaCalculos.cs` (NUEVO, puro): posición de stock por nivel,
      normalización de ubicación, etapa [1,3], consumo directo vs por ítems, referencias de inventario,
      resolución de totales de huevos entre sus tres fuentes
- [x] `MigracionEsquemas`: `SeguimientoLevante` 15 → 36 columnas · `SeguimientoProduccion` 12 → 32
      (columnas compartidas definidas UNA vez para que las dos líneas no se desincronicen)
- [x] `MigracionEsquemas`: `AlimentoPostura` (reusa la hoja `Alimento` de engorde) y `HuevosPostura` (hoja `Huevos`)
- [x] `MigracionPosturaCalculosTests.cs` (NUEVO) — 45 casos
- [x] Test de **retro-compatibilidad**: encabezados viejos (15 y 12) validan sin faltantes ni desconocidos,
      y las columnas históricas conservan su ORDEN como prefijo (los operarios pegan bloques enteros)
- [x] `dotnet test` verde — **1284/1284** (973 previos + 311)
- [x] ⚠️ Gotcha xUnit: `[InlineData(…, 0, …)]` sobre un parámetro `double?` explota con
      `ArgumentException` (Int32 no convierte a Nullable&lt;Double&gt;) — hay que escribir `0d`

## Fase 2 — Backend: funciones SQL v2
- [x] `backend/sql/fn_migracion_seguimiento.sql`: `metadata`, las 11 categorías + `peso_huevo` (levante) y
      `tipo_alimento` + `cons_separado` (producción) en las dos fns
- [x] Contrato del consumo de Producción: `cons_separado` ausente/false ⇒ total en `cons_kg_h` y
      `cons_kg_m = 0` (histórico intacto); `true` ⇒ separado por sexo como `ProduccionService`
- [x] Firma **intacta** (`CREATE OR REPLACE`, sin `DROP FUNCTION`) — patrón `20260714022321`
- [x] Migración `20260728130000_FnMigracionSeguimientoPosturaAlimentoYHuevos` (sin DDL: `metadata` y las
      11 columnas `huevo_*` ya existían en ambas tablas)
- [x] Aplicada en BD local :5433 (⚠️ el `dotnet-ef` de `tools-ef10` necesita `DOTNET_ROOT=~/.dotnet`,
      si no busca .NET 10 en `C:\Program Files\dotnet` y falla con exit 150)
- [x] **Smoke SQL con ROLLBACK** (lote 116 levante · lote 114 producción con LPP temporal):
      - JSON **viejo** ⇒ idéntico a antes: levante con `metadata` NULL y huevos en 0; producción con
        `cons_kg_h = 920` (800+120), `cons_kg_m = 0` y `tipo_alimento = ''`
      - JSON **nuevo** ⇒ `metadata.itemsHembras` y `metadata.huevoItems` persistidos, las 11 categorías,
        `peso_huevo`, y producción con `cons_kg_h = 700` / `cons_kg_m = 95` separados
      - reimportar ⇒ **0 filas** (idempotencia intacta) · aves descontadas de forma incremental
        (7405→7399 H, 738→737 M en levante; 7575→7564 H, 1003→1002 M en producción)

## Fase 3 — Backend: alimento e inventario
- [x] `MigracionService.AlimentoPostura.cs` (NUEVO): contexto de inventario del lote (nivel efectivo
      `granja ?? empresa` + modelo de consumo por país), hoja `Alimento` y descuento del consumo
- [x] `LeerHojaAlimentoAsync` de engorde **refactorizada** para recibir `(destinoDefault, manejaPorGalpon)`
      en vez del lote de engorde ⇒ una sola implementación sirve a las dos líneas
- [x] Ubicación normalizada según nivel con **Advertencia, no excepción** (`AjustarUbicacionAlNivel`).
      🔴 Sin esto `RegistrarIngresoAsync` LANZA en Sanmarino/Santa Reyes ("no use Núcleo/Galpón") y la
      fila se perdía con un mensaje de infraestructura. De paso arregla el mismo caso en engorde Colombia
- [x] El consumo suelto de la hoja usa `RegistrarConsumoNivelGranjaAsync` cuando la granja es nivel
      granja (`RegistrarConsumoAsync` exige galpón **sin mirar el flag**) + `SaveChanges` explícito
- [x] `RegistrarConsumoNivelGranjaAsync` ahora respeta `FechaMovimiento` (antes fijaba `UtcNow`, lo que
      rompía la idempotencia por fecha). Aditivo: ningún llamador actual la pasa ⇒ sin cambio de conducta
- [x] Simulación de balance (`SimularBalancePosturaAsync`) + **rechazo del archivo entero** con el
      faltante exacto + saldo proyectado por posición como Advertencia (también en dry-run)
- [x] Descuento del consumo delegando en el MISMO camino del alta manual
      (`IColombiaInventarioConsumoService` nivel granja / `IInventarioGestionService` nivel galpón)
- [x] Referencia del movimiento byte a byte igual a la del alta manual, resuelta por query posterior
      `(lote, fecha)` — sin cambiar la firma de la fn
- [x] **Fix**: `FilasOmitidas` real (postura reportaba 0 siempre) y exclusión de las fechas ya existentes
      del descuento ⇒ reimportar no descuenta dos veces. Las filas "solo traslado" NO cuentan como
      existentes (la fn las mergea, así que ese día sí se procesa)
- [x] Las consultas de fechas evitan `.Date` en el WHERE (EF lo traduce a `date_trunc`, que trunca en la
      zona de la sesión y pierde bordes): rango ±1 día y recorte fino en memoria
- [x] `IColombiaInventarioConsumoService?` inyectado en el ancla de `MigracionService`
- [x] Runner genérico `EjecutarHistoricoAsync` conservado intacto para **venta engorde**; postura usa
      `EjecutarHistoricoPosturaAsync`
- [x] `dotnet build` (solución completa) 0 errores / 0 advertencias · `dotnet test` **1285/1285**

## Fase 4 — Backend: huevos y plantillas
- [x] `MigracionService.HuevosPostura.cs` (NUEVO): hoja `Huevos` (fecha + ítem + cantidad) validada
      contra `catalogo_items` `item_type='huevo'` de la empresa dueña de la GRANJA, con gate
      `clasificacion_huevo_por_items` **fail-closed** (flag OFF + hoja presente ⇒ Error explícito)
- [x] Las 11 categorías en la hoja `Datos` de Producción; con hoja `Huevos` quedan en 0 y el total sale
      de los ítems (regla de `HuevoItemsCalculos`); mezclar ambas fuentes ⇒ Error
- [x] Huevos de Levante (semana ≥ 14 + `captura_huevos_en_levante`) — cierra el **P2** del bloque de huevos.
      Total e incubables se DERIVAN del desglose, como en el modal
- [x] Plantillas: hojas `Alimento` y `Huevos` + `Referencias` (alimentos e ítems de huevo con su código)
      + dropdowns + `Instrucciones` que explican el nivel de stock del lote y el rechazo por faltante
- [x] Validaciones R8: fecha &lt; encaset (Error), fecha futura (Advertencia), `Etapa ∈ [1,3]` (Error),
      unidad kg/qq, alimento inexistente/ambiguo, consumo directo ignorado por traer ítems (Advertencia),
      total explícito que discrepa del desglose (Advertencia), fecha de la hoja `Huevos` sin fila en `Datos` (Error)

## Fase 5 — Validación
- [x] **Smoke API local — 23/23 verdes** (backend propio en :5399 para no tocar el del usuario; JWT +
      X-Secret-Up minteados; lote 116 A374A de la granja 20, alimento a nivel granja, ítem 199 con 320 kg)
- [x] 🔴 **Excel viejo (15 columnas)** ⇒ fila idéntica a la de siempre: consumo directo 250,5/30,
      `tipo_alimento='PRE'`, `metadata` NULL, huevos en 0, inventario **sin tocar**
- [x] Consumo por ítem ⇒ stock descontado de verdad y movimiento con la referencia **byte a byte** del
      alta manual (`Seguimiento lote levante #1102 2026-07-06`)
- [x] 🔴 **Stock insuficiente** ⇒ dry-run e import rechazan el archivo ENTERO, 0 filas insertadas, con el
      faltante exacto y diciendo "en la granja" (no "en el galpón")
- [x] Hoja `Alimento` ⇒ el ingreso de 6.000 kg habilita el consumo de 5.000 que antes se rechazaba;
      el movimiento queda en su **fecha real** (2026-07-08), no en la de la corrida
- [x] 🔴 **Reimportar** ⇒ 0 filas nuevas, `FilasOmitidas ≥ 1` (antes siempre 0) y **sin doble descuento**
- [x] Núcleo/Galpón en granja de nivel granja ⇒ Advertencia + movimiento aplicado a nivel granja
      (con el código anterior `RegistrarIngresoAsync` habría lanzado)
- [x] Validaciones: fecha < encaset · alimento inexistente · unidad inválida · lote no elegible para producción
- [x] Huevos en levante (Sanmarino, semana ≥ 14) ⇒ `huevo_tot=990` y `huevo_inc=950` **derivados** del
      desglose 800/150/40, `peso_huevo=57.8`
- [x] **Balance verificado en BD**: stock 320 − 100 + 6.000 − 5.000 + 50 = **1.270 kg exactos**
- [x] `dotnet build` (solución) 0 errores / 0 advertencias · `dotnet test` **1285/1285**
- [x] `yarn build` **no aplica**: cero archivos del front tocados. Verificado que
      `construir-resumen-resultado.funcion.ts` ya pinta `filasOmitidas` y las advertencias del saldo
- [x] BD local **restaurada al estado exacto** (0 filas de smoke, stock 320,000, aves 7405/738,
      0 registros de auditoría) y backend de smoke detenido — sin procesos huérfanos
- [ ] Smoke UI en dev server — **pendiente**: en la BD local no hay ningún lote elegible para
      Producción (requiere levante cerrado + liquidado + LPP) y el de Levante se validó por API

## Fase 6 — Cierre
- [x] Commit `7846200` acotado a esta tarea (16 archivos; el working tree ya no tenía trabajo de otras
      sesiones — el bloque del Resumen Semanal RA Pesadas se commiteó en `1b236bb`)
- [ ] Push y deploy — **pendientes de pedido explícito**

## Fase 7 — Ejercicio E2E del ciclo completo (levante → cierre → producción)

Pedido del usuario: plantillas con datos de ejercicio en el Escritorio y el ciclo real de punta a punta.

- [x] Lote de prueba **`ZZPRUEBA-MIG`** (id **130**, LPL 30) en granja 20 / núcleo 591408 / galpón G0319,
      encaset 2025-09-01, 5.000 H + 550 M, raza AP 2023
- [x] **Plantillas oficiales descargadas del endpoint y llenadas con los datos del ejercicio**, dejadas en
      el Escritorio: `Carga_Masiva_LEVANTE_ejemplo.xlsx` y `Carga_Masiva_PRODUCCION_ejemplo.xlsx`
      (4 hojas cada una: Datos · Alimento · Referencias · Instrucciones)
- [x] **Levante**: 14 días (2026-02-16 → 03-01, semanas 25-26) con alimento del inventario y huevos en los
      últimos 3 días · entrada de 8.000 kg en la hoja Alimento · dry-run con saldo proyectado · 14/14 importadas
- [x] **Liquidación + cierre**: aves disponibles 4.952 H / 536 M (descontadas por la carga masiva) y
      **804 huevos de levante** detectados para arrastrar · LPP creado · lote a fase Producción
- [x] El lote pasó a ser **elegible para carga masiva de producción** (antes de cerrar no lo era)
- [x] **Producción**: 7 días (2026-03-02 → 03-08) con las 11 categorías y alimento propio · 7/7 importadas ·
      reimportar ⇒ 0 filas
- [x] **Cuadre final verificado en BD**: levante 6.874 kg ⇒ stock 320 + 8.000 − 6.874 = **1.446 kg** ·
      producción 4.522 kg ⇒ stock 9.360 + 6.000 − 4.522 = **10.838 kg** · aves 5.000 → 4.952 (levante) →
      4.928 (producción) · 16.729 huevos de producción

### 🔴 Bug encontrado por el ejercicio y corregido

- [x] **El día del cierre se omitía en silencio.** El cierre de levante crea una fila de producción con los
      huevos arrastrados; cuando el Excel traía ESE día (el caso normal: es el primer día de producción),
      la carga lo contaba como "ya cargado" y **descartaba mortalidad, consumo y clasificación**. El alta
      manual, en cambio, hace **merge** (`ProduccionService.AplicarRequestSobreFilaArrastre`)
- [x] Fix: `ArrastresPendientesAsync` + suma con `HuevosLevanteCalculos.Sumar` en C# (que sabe leer la marca
      del metadata) y paso de merge nuevo en `fn_migracion_seguimiento_produccion` (`es_merge_arrastre`).
      La marca del arrastre se conserva y se cierra la ventana (`seguimientoRegistrado`), igual que el modal
- [x] Migración `20260728140000_FnMigracionProduccionMergeArrastreHuevos` aplicada en local
- [x] Advertencia explícita en el reporte: *"Es el día del cierre del levante: los N huevos arrastrados se
      SUMAN a los de esta fila…"*
- [x] **Verificado**: día 2026-03-02 con `huevo_tot = 2.674` (804 arrastrados + 1.870 del Excel),
      `huevo_limpio = 2.220` (720+1.500), `huevo_inc = 2.540` derivado, mortalidad/consumo/observaciones del
      archivo, y la marca con `seguimientoRegistrado = true`
- [x] 2 tests puros nuevos que fijan el contrato del merge · `dotnet test` **1304/1304**

- [ ] El lote `ZZPRUEBA-MIG` (id 130) **queda cargado en la BD local** para poder revisarlo por pantalla.
      Para borrarlo: `DELETE FROM seguimiento_diario_produccion WHERE lote_id=130; DELETE FROM
      seguimiento_diario_levante WHERE lote_id='130'; DELETE FROM lote_postura_produccion WHERE lote_id=130;
      DELETE FROM liquidacion_cierre_lote_levante WHERE lote_postura_levante_id=30; DELETE FROM
      lote_postura_levante WHERE lote_id=130; DELETE FROM lotes WHERE lote_id=130;`
- [x] **Corregido**: al cambiar de modo se perdían el año/semana del Resumen y el lote base del
      Detalle. Los dos modos ahora se OCULTAN en vez de destruirse (`[hidden]` en lugar de `@if`).
      Las gráficas siguen creándose solo cuando su propia vista está activa, así que no se montan
      canvas invisibles. Verificado ida y vuelta: el Resumen mantiene 2025/Semana 20 con sus 2
      filas y el Detalle mantiene K345 con sus 3 tabs y 25 semanas

## Fase 3 — Alimento por fase (hoja 5) — HECHO

> **D5 quedó SIN NECESIDAD de tocar el catálogo.** La energía/proteína de cada fase ya vive en la
> guía genética (`kcal_h`/`prot_h`/`kcal_m`/`prot_m`, los mismos valores que la hoja AUX), así que
> no hay que sembrar `catalogo_items.metadata` ni mapear nombres de ítem a fases —que era el
> paso frágil—. Si algún día se captura el alimento real, tiene precedencia automática.

- [x] `AlimentoPorFaseCalculos.cs` (PURO) — agrupa por fase, suma real y guía, DIF y %DIF, Total general
- [x] La FASE la fija la guía (`alim_h`/`alim_m`), no la edad: el corte depende de línea y año
- [x] Nutrición semanal por sexo en `ConstruirSemanasLevante` + guía extendida (`GuiaSemanaLevante`)
- [x] **Hallazgo**: `kcal_al_h`/`prot_al_h` NO se cargan en ningún registro (0 de 599) ⇒ sin respaldo
      la mitad hembra salía vacía. Regla uniforme: energía capturada si existe, si no la NOMINAL de
      la fase según la guía. En machos es la única fuente (su alimento no se captura)
- [x] **Bug encontrado**: el tab Consolidado salía vacío — no le pasaba la fase ni la nutrición.
      Los valores son POR AVE ⇒ se promedian (sumarlos multiplicaba la energía por nº de galpones)
- [x] Sin endpoint nuevo ni SQL nuevo: viaja en la respuesta de levante que ya existía
- [x] Front: vista «Alimento» (solo levante) con las 4 tablas + nota del criterio de machos
- [x] Tests: `AlimentoPorFaseCalculosTests` (16 casos)

## Fase 4 — Clasificación de huevo (hoja 8) — HECHO

> No hizo falta endpoint, SQL ni el flag de items: `fn_indicadores_produccion_postura` **ya devolvía
> los 11 conteos por semana** y nadie los estaba exponiendo. Solo había que sacarlos al DTO y
> calcular el % sobre el huevo total.

- [x] Conteos + % en `ReporteSemanalProduccionSemanaDto`, calculados en `ConstruirSemanasProduccion`
- [x] Mapeo `Deforme Blanco` = `huevo_deforme + huevo_blanco` (el Excel los trae juntos y la BD
      los guarda separados; sin sumar, el reporte mostraría la mitad)
- [x] Consolidado: los CONTEOS suman y los % se RECALCULAN sobre el total (promediar los % de cada
      galpón haría pesar igual al galpón chico que al grande)
- [x] Front: vista «Clasificación» (solo producción) con cabeceras agrupadas
- [x] Tests: `ClasificacionHuevoSemanalTests` (7 casos, incluido el consolidado 86 % vs 70 %)

## Fase 5 — Consolidado multi-lote de gráficas (hojas 3/4/7) — HECHO

> **Hallazgo que ajusta la especificación:** los números de las hojas de gráficas del archivo
> **no son reproducibles**. Igual que ALIMLev, están filtradas por una selección de lotes guardada
> en el Excel (un slicer): la hoja dice 1.129.682 aves iniciales cuando el dato completo son
> 1.773.976, y ningún filtro por año, regional, guía ni traslado reproduce ese subconjunto.
> Acá la selección son los **filtros que elige el usuario**, que además puede auditar.

- [x] Sin fn SQL nueva: las dos fns del Resumen aceptan `p_sem_anio = NULL` (= todas las semanas)
      y el pliegue por edad queda en cálculo puro sobre un conjunto ya recortado por la BD
- [x] `part` pasa a calcularse **particionado por semana calendario** — con todas las semanas, la
      ventana global habría mezclado las 52 del año
- [x] **Regresión verificada**: el modo de una sola semana sigue dando 79 filas / 0 diferencias
      contra el Detalle, igual que antes del cambio
- [x] `ConsolidarPorEdadLevante` / `...Produccion` (puro): agrupa por EDAD —no por fecha—, suma
      saldos y pondera indicadores; cuenta LOTES distintos, no filas
- [x] `POST api/ReporteTecnicoSemanal/curva` + vista «Curva del año» en el modo Resumen
- [x] 6 gráficas por etapa con la convención del repo (Real sólido, Guía punteada, hembras naranja
      y machos azul), eje X en edad
- [x] Tests: 6 casos nuevos en `ResumenSemanalRaPesadasCalculosTests`
- [x] Smoke API: levante 25 puntos / 4 lotes · producción 24 puntos / 2 lotes con %Prod siguiendo
      la guía (30,0 vs 31,25 · 69,6 vs 63,25 · 82,5 vs 80,75) · etapa inválida 400 · Demo 0 puntos
- [x] Smoke UI: las 6 gráficas de cada etapa montan, el toggle vuelve a la tabla y consola limpia
- [x] `dotnet build` 0/0 · `dotnet test` 1313/1313 · `yarn build` 0 errores

## Fase 6 — Cierre — HECHO

- [x] Etiqueta de menú → «Informe RA Pesadas» (migración `20260728150000`, localiza por `route`,
      NUNCA por id: los ids difieren local↔prod). La RUTA no cambia, así no hay que re-asignar
      `role_menus`
- [x] Export Excel: el del Detalle ahora incluye las hojas nuevas — levante suma una hoja
      «ALIMLev» por tab y producción una «CLAS» por tab
- [x] Verificado sobre el archivo REAL generado (se intercepta el Blob y se cuentan las entradas
      del ZIP): levante y producción pasan de 3 a **6 hojas** cada uno
- [x] Regresión: los 30 tests de `ReporteTecnicoSemanalCalculos` siguen verdes
- [x] `dotnet build` 0/0 · `dotnet test` 1307/1307 · `yarn build` 0 errores
- [x] Consola del navegador limpia en todo el recorrido

## Validación independiente por agentes (2026-07-28) — granja real NIZA III / lote base K345

Dos validadores en paralelo, solo lectura, sobre la única granja de Sanmarino con datos en las dos
etapas (K345A/K345B en levante, P-K345A/P-K345B en producción, raza AP guía 2026).

### Resultados

- [x] **Levante**: los 7 puntos OK. Equivalencia Detalle↔Resumen **250 comparaciones / 0 diferencias**.
      ALIMLev sin fallas en 3 tabs × 4 tablas. Curva: 0 desviaciones en 14 indicadores × 25 edades.
      Ponderado confirmado (sem 20: 92,9899 ponderado vs 92,70 simple)
- [x] **Producción**: los 6 puntos OK. Equivalencia **1.584 comparaciones / 0 diferencias**
      (1.056 de campos núcleo + 528 de columnas de guía) sobre 45 semanas calendario, incluyendo el
      cruce de año y la semana 53. Clasificación: 968 conteos contrastados contra BD, 0 diferencias;
      consolidado con % recalculados y **0 casos** coincidiendo con el promedio simple. Venta: 264
      celdas en 0, sin falsos positivos. Curva ponderada confirmada (edad 35: 72,8876 vs 74,6570)
- [x] Ambos verificaron que las fns del Resumen **no llaman** a las del Detalle: son implementaciones
      independientes que coinciden valor por valor

### Bug encontrado y CORREGIDO — guía de la semana 25 en el Detalle

El loader en C# `CargarGuiaPorSemanaAsync` consultaba **sin `ORDER BY`** y se quedaba con la primera
fila; `ParseEdadSemana("25P")` devuelve 25, así que en la semana 25 ganaba `'25P'`.

- En **levante** eso era incorrecto: mostraba retiro acumulado **0,10** en vez de 4,03, rompiendo la
  monotonía de la guía (sem 24 = 3,93 → sem 25 = 0,10) y contradiciendo al Resumen, que sí desempata
- El mismo loader lo usan las dos etapas, con la preferencia **invertida**

- [x] El loader recibe `preferirVarianteProduccion`: levante toma la puramente numérica, producción
      la del sufijo; y ordena explícitamente para no depender del plan
- [x] Verificado: levante sem 25 pasa de 0,10 a **4,0269** y la monotonía se restaura
      (3,8347 → 3,9308 → 4,0269); unifGuía de null a 90; consumo acum de 847 a 11.501,23
- [x] Producción **sin cambios**: sigue en 0,10, que es su fila correcta
- [x] Detalle y Resumen ahora coinciden en las dos etapas en la semana 25

### Endurecimiento — `fn_indicadores_produccion_postura`

- [x] La misma colisión existía en la fn SQL (`LIMIT 1` sin `ORDER BY`). Hoy devolvía `'25P'` por el
      **ctid**, no por contrato: un `VACUUM FULL` o un re-seed la habrían cambiado en silencio
- [x] Migración `20260728160000` fija el desempate en la variante con sufijo — el valor que ya
      devolvía, ahora garantizado. Verificado post-migración: sigue en 0,10

### Denominadores de los % semanales — CORREGIDO (autorizado por el usuario aun estando en prod)

Antes de tocar nada se despejó la regla REAL contrastando fila a fila el archivo fuente sobre los
73 lotes. El resultado desmiente tanto lo que hacía el Detalle como la suposición inicial:

| Columna | Denominador | Filas que lo confirman |
|---|---|---|
| `%Mort` H/M | saldo al **INICIO** de la semana | 1401 H + 1311 M (ninguna con el final ni con base fija) |
| `%Sel` H/M | saldo al **FINAL** de la semana | 248 H + 488 M (ninguna con el inicial) |
| `%Err` H/M | saldo al **FINAL** de la semana | 142 H + 48 M |

Sí: el archivo usa bases DISTINTAS para mortalidad y para descarte. No es error de lectura.

- [x] El Detalle usaba la **base fija** en las seis columnas ⇒ no reproducía ninguna
- [x] Corregidas las 6, en la fila por lote y en el consolidado
- [x] `AvesHembrasInicio`/`AvesMachosInicio` expuestos en el DTO: el consolidado los SUMA en vez de
      reconstruirlos como fin + bajas, que ignoraría los traslados
- [x] Los ACUMULADOS siguen sobre base fija — ésos ya coincidían con el archivo y no se tocaron
- [x] El único test que fijaba la convención vieja (`…usan_base_fija_de_aves_iniciales`) se reescribió
      en 3 tests con los casos REALES del archivo (A320 edad 2 y A322 edad 13)
- [x] Verificado end-to-end: **100 comparaciones de `%Mort` Detalle vs Resumen, 0 diferencias**
- [x] `dotnet build` 0/0 · `dotnet test` **1320/1320** · `yarn build` 0 errores
- ⚠️ **Cambia el Reporte Técnico Semanal que ya está en producción** (comparte `ConstruirSemanasLevante`).
      Es intencional y autorizado: los números anteriores no reproducían el archivo oficial

### Hallazgos menores (no se tocaron)

- [ ] `uniformidadGuia` devuelve 0 donde la guía es NULL en producción: viene de un `COALESCE(...,0)`
      de la fn base, declarado deliberado para replicar el parseo legacy. Cosmético y preexistente
- [ ] Lote 116 (A374A, LA ESMERALDA) tiene `hembras_l` NULL con 212 retiros ⇒ saldo NEGATIVO. Es dato
      sucio de la BD local; el código se comporta bien (los pesos ≤ 0 se descartan), pero deja filas
      con `part` negativa o ponderados en null
- [ ] Nota de diseño confirmada: los indicadores de MACHOS se ponderan por saldo de HEMBRAS (es la
      definición de `part` del archivo), así que un lote sin hembras vivas pierde sus métricas de machos

## Despliegue a PRODUCCIÓN — 2026-07-29 (autorizado por el usuario)

- [x] `git push origin main` (15 commits) y merge a `main-produccion` → `7aef1f3`
- [x] Workflow **Deploy to Production** run `30420336431`: los 3 jobs en success
      (compuerta de tests · backend · frontend)
- [x] **Verificación post-deploy contra ECS** (obligatoria: el CLI reporta éxito aunque haya rollback):
      - TaskDef **140 → 141**, rollout `COMPLETED`, Running 1 / Pending 0 / Desired 1
      - Imagen desplegada = `…backend:7aef1f374ab07a7bd761d7ff20e86b07e50b355b`, **idéntica** al SHA que se quiso desplegar
      - Task `RUNNING` + `HEALTHY`, eventos con «has reached a steady state», sin ciclo de reinicios
      - Front `/version.json`: `2026-07-29T03:51:40.327Z` (antes `2026-07-28T06:56:49.076Z`)
- [x] Las 6 migraciones se aplicaron solas al arrancar (`Database__RunMigrations=true`). Que el
      contenedor levantara y llegara a steady state es la prueba: una migración fallida mata el
      proceso con SIGSEGV y ECS revierte
- [ ] **Aviso pendiente a los usuarios del Reporte Técnico Semanal**: los % semanales de mortalidad,
      descarte y error de sexaje cambiaron de denominador y sus números se movieron respecto de ayer
      (hembras ~2,8 % de desvío medio, machos ~13 %). Es intencional — antes no reproducían el
      archivo oficial— pero conviene decirlo para que no se lea como un error de datos
- [ ] No se pudo consultar `DescribeTargetHealth` (el usuario IAM no tiene ese permiso); la salud se
      confirmó por `healthStatus: HEALTHY` de la task y los eventos del servicio

⚠️ El paquete incluyó 3 commits de otra sesión (carga masiva que mueve inventario de alimento), con
su smoke de UI marcado como pendiente por ellos. Se desplegó por decisión explícita del usuario.

---

# Tracker — Cuadre de aves y alimento en pollo engorde (Panamá)

**Plan:** [`fase_de_desarrollo/cuadre_engorde_panama_aves_alimento_plan.md`](fase_de_desarrollo/cuadre_engorde_panama_aves_alimento_plan.md)
**Fecha:** 2026-07-29 · **Empresa:** ItalcolPanama (id 5) · BD: dump de producción restaurado en local

Objetivo: que el saldo de **aves** de la tabla de seguimiento coincida con el widget «Aves disponibles», y
que el saldo de **alimento** coincida con el stock de Gestión de inventario, con **inventario compartido por
galpón** cuando hay dos lotes en el mismo galpón.

## Fase 0 — Diagnóstico (cerrada)

- [x] Panorama de los 55 lotes de engorde de Panamá (29 con seguimiento)
- [x] **Aves A1**: `GetAvesDisponiblesAsync` doble-descuenta — el descuadre es *exactamente* `bajas_aplicadas` en los 26 lotes con 7 días completos
- [x] **Aves A2**: en 25 de 29 lotes las bajas de los 7 días del cruce nunca llegaron al maestro (0 filas `BAJA_SEGUIMIENTO` de cruce; faltante = bajas del cruce, exacto)
- [x] Maestro verificado SANO: `hembras_l+machos_l = aves_encasetadas − bajas_aplicadas` exacto en los 29 lotes
- [x] **Alimento F1**: ingresos con scope galpón + consumo con scope lote (verificado exacto en G0490)
- [x] **Alimento F2**: G0486 tiene encima los ingresos de G0485 (corrida B: 18 filas / 128.302,2 kg ≡ total de G0485)
- [x] **Alimento F3**: los 44 `AjusteStock` entran como `INV_OTRO` y ningún camino del saldo los mira
- [x] Identidad del stock verificada (error 0,0 en los 24 galpones): `stock = Σ Ingreso − Σ Consumo_inv + Σ Ajuste`
- [x] **Alimento F4**: DAYLAND (5 galpones) sin ningún `AjusteStock` → requiere confirmación operativa
- [x] Simulación de estrategias (`scratchpad/sim_final.sql`): Op1 8/25 · Op3 8/25 y empeora G0464 · Op3b 8/25 · Op4 2/25 · **Op2 25/25**

## Fase 1 — Aves (código + datos)

- [x] `Application/Calculos/AvesDisponiblesEngordeCalculos.cs` (NUEVO, puro) + **18 tests xUnit verdes**
- [x] `GetAvesDisponiblesAsync` deja de restar las bajas ya aplicadas al maestro (retrocompatible: sin filas `BAJA_SEGUIMIENTO` el pendiente es el total ⇒ fórmula previa intacta)
- [x] Migración `20260729100000_AplicarBajasCruceReproductoraAlMaestroEngorde`: **24 lotes, 167 días, 8.411 aves**; respaldo `_backup_bajas_cruce_engorde_20260729` para que el `Down` no toque las 17 filas preexistentes
- [x] `Down` **probado**: revierte exacto y respeta las 17 filas de los lotes 142/179/180/181; `mixtas` NULL ya no se convierte en 0
- [x] **Cuadre verificado: 26/26 lotes con los 7 días completos en dif = 0**; `bajas_sin_aplicar = 0` y conservación `encaset − bajas = maestro` intacta en los 29
- [ ] ⚠️ Lotes 179/180/181 aún dentro de los 7 días de reproductora: el widget resta las asignadas y muestra 0 / 12.195 (diseño vigente `AvesDevueltas = false`) — **decisión del usuario** si debe cambiar
- [ ] ⚠️ Lote 179: asignadas a reproductora 36.135 vs encaset 48.595 → 12.460 aves sin asignar (posible dato incompleto de la carga)

## Fase 2 — Alimento, scope de galpón (código)

- [x] `fn_seguimiento_diario_engorde` **v10**: nuevo CTE `consumo_galpon_por_fecha` usado por la apertura y `pt_calc` ⇒ `saldo(f) = ingresos(≤f) − consumo_del_galpón(≤f)`
- [x] **Solo cuentan los lotes que CONVIVEN** (rangos de seguimiento solapados). Primer intento sin ese filtro rompió Ecuador (1.037 filas / 22 lotes) porque sus galpones encadenan 3-4 ciclos sucesivos y los lotes viejos quedan en `Abierto` (`fecha_max` NULL)
- [x] `consumo_por_fecha` (el que alimenta `saldo_close`) se deja en scope LOTE: fija `fecha_max` y usar el consumo del galpón sería circular
- [x] Migración `20260729110000_FnSeguimientoEngordeV10ConsumoScopeGalpon` (idempotente, `CREATE OR REPLACE`) + `backend/sql/` sincronizado
- [x] `RecalcularSaldoAlimentoPorLoteAsync`: consumo a scope galpón en **los dos** services (carga masiva y formulario Ecuador), con `AsNoTracking()` para no persistir los ajenos
- [x] ~~Front~~ **no aplica**: el front solo consume `saldoAlimentoKg` del backend, no lo calcula (no quedan cálculos de saldo en TS)
- [x] 3 tests nuevos del cálculo puro (saldo compartido, retrocompatibilidad con un solo lote, dos lotes el mismo día)
- [x] **No-regresión verificada v9 vs v10 fila a fila**: Ecuador **0 filas distintas / 0 lotes**; Panamá 83 filas / 8 lotes (exactamente los 4 galpones compartidos); **aves 0 diferencias**; mismo número de filas por lote
- [x] Verificado en G0490: los lotes 168 y 169 muestran **el mismo saldo en cada fecha** (19.393,6 al 27/07)
- [ ] Menor: `seguimiento_diario_aves_engorde.saldo_alimento_kg` **persistido** queda con el valor viejo hasta que se toque un registro del lote (la tabla NO lo usa: la fn devuelve `pt` calculado en vivo)

## Fase 3 — Alimento, datos (autorizada por el usuario el 2026-07-29)

Migración `20260729120000_CuadreAlimentoEngordePanama`, con respaldo y `Down` probado.

- [x] **G0486**: anuladas las **18 filas** de la corrida espuria (128.302,2 kg — idénticas en filas y kg al total de G0485). Guarda fail-safe: solo anula si el conjunto es exactamente ese, si no no toca nada. El galpón pasa de **+127.168,2 a −1.134,0**
- [x] **DAYLAND**: ingreso de cuadre datado en los **5 galpones** (23.677,8 kg), calculado contra los datos del momento, no contra constantes → los 5 quedan en **0,0 exacto**
- [x] **Grupo C intacto** por decisión del usuario: el `NOT EXISTS` sobre `AjusteStock` excluye por construcción los 11 galpones que ya ajustaste a mano
- [x] El inventario NO se toca en ningún caso: solo el histórico que alimenta el seguimiento
- [x] `Down` **probado**: restaura los 218.183,3 kg de G0486, borra los 5 ajustes y elimina el respaldo

## Validación

- [x] `dotnet build` **0 errores / 0 advertencias** · `dotnet test` **1341/1341 verdes** (21 nuevos)
- [x] `yarn build` no aplica: no se tocó ningún archivo del front
- [x] Cuadre **aves**: 26/26 lotes con los 7 días completos en dif = 0
- [x] Cuadre **alimento**: **13/25 galpones en 0** (antes 8). Descuadre total **158.374 → 8.662 kg (−94,5 %)**. Los 12 restantes son el grupo C que el usuario decidió dejar (máx. 1.860 kg) más G0486 en −1.134
- [x] Objetos temporales de diagnóstico eliminados (`fn_seg_engorde_v9_tmp`, `_snap_antes_cruce`); sin procesos huérfanos
- [ ] Smoke UI en G0490 (pendiente: requiere levantar back+front)

## ⚠️ Fase 3 REESCRITA — el inventario era el que estaba mal, no el seguimiento

Hallazgo al analizar los 12 galpones de diferencia pequeña: **el inventario nunca descontó el consumo de
los 7 días del cruce de reproductora** (mismo bug que las aves — el cruce escribe por SQL directo).
Verificado al decimal en **19 de 25 galpones**: `cons_seguimiento − cons_inventario = cons_de_los_días_de_cruce`.
Todo ese consumo es **AV. POLLITO PREINICIADOR (ítem 223)**.

- [x] Caso testigo G0460: el desfase de 7.484,4 kg son los 7 primeros días (10-16 jun) sin descontar; del 18 jun en adelante coinciden exacto
- [x] ⇒ **El stock estaba inflado, no el seguimiento**. El saldo correcto es el lógico: `ingresos − consumo real`
- [x] Los ajustes manuales de la operación iban en la dirección correcta y en varios galpones dieron **exacto** (G0490 llevó el 223 de 8.935,862 a 0 = el consumo del cruce 8.935,9; ídem G0469, G0470, G0472, GALPON)
- [x] **Esto invalidó la Fase 3 original para DAYLAND** (subía el seguimiento al stock inflado: G0460 a 14.151,5 en vez de 6.667,2) → migración `20260729120000` **reescrita** antes de desplegar
- [x] Decisión del usuario: manda el **saldo lógico** y se ajusta el **inventario**; en G0461 se registra el ingreso faltante de 6.622,5 kg
- [x] Migración reescrita aplicada sobre el dump de producción: **25/25 galpones en 0,0 exacto** (descuadre total 237.752,7 → **0,0 kg**)
- [x] Bug propio detectado y corregido: el saldo objetivo no excluía las «devoluciones por eliminación» como sí hace la fn → G0479 quedaba con 590 kg
- [x] Sin impacto cross-empresa: 0 movimientos y 0 stocks de Ecuador/Colombia tocados
- [x] Aves siguen en **26/26**, 0 bajas sin aplicar, conservación intacta
- [x] **Idempotencia**: una 2ª corrida tocaría 0 galpones (delta máx 0,005 kg)
- [x] `Down` probado: restaura los stocks (G0460 vuelve a 14.151,5), borra los 24 movimientos de ajuste y el ingreso de G0461, y desanula G0486
- [x] **Determinismo**: `Down` + `Up` deja los 66 stocks idénticos
- [x] `dotnet build` 0/0 · `dotnet test` **1341/1341** verdes

## Validación sobre el dump de PRODUCCIÓN actual (2026-07-29)

BD recargada desde producción (190 migraciones aplicadas, la última `20260728160000`); las 3 nuevas
entraron pendientes, como pasaría en el arranque de ECS.

- [x] **Las 3 migraciones aplican sin error** en secuencia sobre el dump limpio (si una fallara, en prod mataría el contenedor con SIGSEGV)
- [x] Estado inicial reproducido: **8.411** bajas del cruce sin aplicar, conservación del maestro **intacta** (0 rotas), 22 de 26 lotes descuadrados, alimento **237.752,7 kg** de descuadre
- [x] **AVES: 26/26** lotes con los 7 días completos en dif 0 · 0 bajas sin aplicar · 0 conservaciones rotas · respaldo con 167 filas / 8.411 aves / 24 lotes (idéntico a la corrida anterior)
- [x] **ALIMENTO: 13/25** galpones en 0 (antes 8) · descuadre **237.752,7 → 8.663,8 kg (−96,4 %)**
- [x] **0 galpones con saldo distinto entre sus lotes** ⇒ el inventario compartido por galpón funciona
- [x] La guarda fail-safe de G0486 encontró exactamente las **18 filas / 128.302,2 kg** y anuló solo esas: 135.339,1 → 7.036,9
- [x] DAYLAND: 5 ajustes por **23.677,8 kg** recalculados contra los datos del dump → los 5 galpones en **0,0 exacto**
- [x] **NO-REGRESIÓN: Ecuador 0 lotes con saldo de alimento cambiado (delta 0,0) y 0 con saldo de aves cambiado**; Panamá 14 lotes de alimento y 0 de aves (la tabla de aves siempre estuvo bien)
- [x] **IDEMPOTENCIA verificada**: re-ejecutando los bloques de datos → `UPDATE 0`, `UPDATE 0`, `INSERT 0 0`; 0 maestros y 0 bajas modificadas
- [x] Objetos temporales de validación eliminados; los dos respaldos de las migraciones quedan en pie (los usa el `Down`)

## Validación cruzada con el Reporte Diario de Costos Engorde (2026-07-29)

El reporte consume `fn_seguimiento_diario_engorde` por LATERAL, así que es el mejor test de que el
cambio de scope (v10) no rompió nada aguas abajo.

- [x] **DAYLAND (granja 107)**, 9 días: `consumo_total_kg`, `mort_sel_total` y `aves_vivas_total` con **0 diferencias** contra la suma de los lotes uno por uno
- [x] **DOÑA MARIA (granja 106)**, 8 días — la que tiene los galpones compartidos G0490 y G0479: **0 diferencias** en las 3 métricas
- [x] **Sin doble conteo en galpón compartido**: el reporte da 7.529,7 kg para G0490 el 27/07 y la suma directa de los lotes 168+169 da exactamente 7.529,7
- [x] **Lote 142** (el de más registros, 41): encaset 48.430 − bajas 1.830 = **46.600** = saldo final · ingresos 155.188,2 − consumo 152.952,9 = **2.235,3** = saldo final = **stock del inventario de G0471**
- [x] `saldo_alimento_kg` persistido coincide con el que calcula la fn (G0464: 66.565,813)
- [x] ⚠️ **Hallazgo preexistente CORREGIDO** (`fn_reporte_diario_costos_engorde` **v2**): el `stock_kg` salía del jsonb `historico_consumo_alimento`, que guarda el saldo **por alimento consumido ese día**, no el total del galpón. G0464 al 22/07: reporte 46.229,2 (solo SUPER POLLO ENGORDE) vs 66.565,8 del galpón (3 ítems). Era estructural: 738 de 2.103 registros divergentes en Ecuador y 451 de 470 en Panamá

## Corrección del Reporte de Costos — `fn_reporte_diario_costos_engorde` v2

- [x] El `stock_kg` ahora se **deriva** de `ingresos(≤fecha) − consumo(≤fecha)` por alimento, con los mismos filtros que la fn de seguimiento (excluye INV_INGRESO del propio seguimiento y devoluciones por eliminación), acumulado sobre todo el histórico
- [x] Un alimento con stock **aparece aunque ese día no se consuma** (FULL JOIN) — sin eso el total nunca cerraría
- [x] **Verificado 0,0 exacto en las 12 granjas de las DOS empresas** contra `ingresos − consumo` del alcance
- [x] En Panamá cuadra además contra Gestión de inventario: **DAYLAND, MENDOZA y TROFARELLO en 0,0**
- [x] `consumo_total_kg`, `mort_sel_total` y `aves_vivas_total` **NO cambian** (0 diferencias en 9 días)
- [x] Migración `20260729130000_FnReporteCostosEngordeV2StockDerivado` (idempotente, `CREATE OR REPLACE`) + `backend/sql/` sincronizado
- [ ] ⚠️ **DOÑA MARIA queda con 544,0 kg** contra inventario: es **G0477**, cuyo lote (182) todavía no tiene ningún seguimiento, así que la migración de cuadre no lo cubrió (filtra por lotes con registros). Su stock lo ajustaste a mano de 12.413,58 a 11.869,59 y no hay consumo registrado que lo justifique — **necesita tu decisión**
- [ ] ⚠️ **Ecuador NO cuadra contra su inventario** (SAN GUILLERMO 206.318 kg, Kilometro 86 172.984 kg…): descuadres de datos preexistentes, nunca se cuadró esa empresa. Además tiene ingresos **sin galpón** (CAROLINA: 211.361,8 kg en bodega de granja) que el reporte no incluye porque su alcance son los galpones de los lotes

## Reporte de Costos: alimento desde las FUENTES REALES (flag por empresa)

Migración `20260729224401_ReporteCostosAlimentoDesdeFuentesReales` + fn v3.
El usuario pidió que el reporte no cruce con el jsonb sino con **inventario y seguimiento diario**.

- [x] Flag tipado `companies.reporte_costos_alimento_desde_fuentes_reales` (default FALSE), ON solo en **ItalcolPanama** — no un `if (empresa == X)`, según la regla de features por empresa
- [x] Con el flag **ON**: consumo del **seguimiento diario** (`consumo_dia_kg`) + stock de **ingresos del histórico − consumo**
- [x] El jsonb queda con **un único uso**: repartir los días con 2+ alimentos, donde `tipo_alimento` los concatena con " / " y el reparto real solo está ahí. **Nunca decide un total**
- [x] **Por qué es flag y no cambio global**: el desglose necesita que `tipo_alimento` sea el nombre del ítem. En Panamá lo es; en **Ecuador viene con prefijo de sexo** («H: AV. SUPER POLLO ENGORDE») en los 4.638 registros y no cruzaría con el inventario
- [x] Motivo de fondo: el jsonb está **INCOMPLETO** — suma 1.554.181,4 kg contra los **1.706.089,8 kg** de consumo real del seguimiento
- [x] **NO-REGRESIÓN byte a byte**: v1 vs v3 en Ecuador (flag OFF) → **0 diferencias en las 267 filas**, incluido el JSON de alimentos
- [x] En Panamá (flag ON) cambia **solo** el JSON de alimentos (84 filas); `consumo_total_kg`, `mort_sel_total` y `aves_vivas_total` con **0 diferencias**
- [x] **0 nombres compuestos** en el desglose (el reparto los resolvió) y solo los **3 ítems reales** de Panamá
- [x] El desglose **suma exacto el consumo del día** en las 84 filas (con el jsonb esto no cerraba)
- [x] Consumo por alimento coincide **0,00** con el seguimiento (122 comparaciones)
- [x] Stock cierra **0,0** contra Gestión de inventario en DAYLAND, MENDOZA y TROFARELLO
- [x] `Down` restaura la fn v1 **antes** de eliminar la columna (la v3 la lee)
- [x] `dotnet build` 0/0 · `dotnet test` **1341/1341**
- [ ] ℹ️ 36 filas con stock negativo por alimento, todas en AV. POLLITO PREINICIADOR (MENDOZA −2.143,7 · TROFARELLO hasta −10.767,4): se consumió más de lo que se registró como ingreso. Mismo criterio sin piso que la fn de seguimiento; el TOTAL del galpón cuadra igual

## Despliegue a PRODUCCIÓN — 2026-07-29 (autorizado por el usuario)

### Tanda 1 — cuadre de aves y alimento (ya estaba desplegada al retomar)

- [x] `main` → `main-produccion` vía **PR #56** → merge `6f23d06` · workflow run `30492866352` **success**
- [x] Imagen en ECS `backend:6f23d06273b1…` **idéntica** al SHA de `main-produccion`; TaskDef **143**, rollout `COMPLETED`
- [x] Verificado que la migración `20260729120000` que quedó en prod es la **versión corregida** (la que ajusta el inventario), no la primera que subía el seguimiento al stock inflado — el diff contra local está vacío. Importaba porque una migración ya registrada en `__EFMigrationsHistory` **no se vuelve a aplicar**
- [x] Las 3 migraciones se aplicaron solas al arrancar (`Database__RunMigrations=true`); que el contenedor llegara a steady state es la prueba (una migración fallida mata el proceso con SIGSEGV y ECS revierte)

### Tanda 2 — Reporte de Costos desde fuentes reales

- [x] `git push origin main` (`2f58e22..9a753ea`) → **PR #57** → merge `9f1d374`
- [x] Workflow run `30498125763`: los **3 jobs en success** (Tests · Backend · Frontend)
- [x] **Verificación post-deploy contra ECS** (obligatoria: el CLI reporta éxito aunque haya rollback):
      - TaskDef **143 → 144**, rollout `COMPLETED`, Running 1 / Pending 0 / Desired 1
      - Imagen `backend:9f1d374fdb78e600626a7890448de20c97d53b8a`, **idéntica** al SHA que se quiso desplegar
      - Task `RUNNING` + **`HEALTHY`**; eventos «deployment completed» y «has reached a steady state», sin ciclo de reinicios
      - Front `/version.json`: `2026-07-29T23:09:36.168Z`
      - `/api/health` responde 401 (protegido por JWT) — la salud la confirma el health check de ECS
- [x] La migración del flag `reporte_costos_alimento_desde_fuentes_reales` se aplicó al arrancar; el flag queda ON solo en ItalcolPanama y OFF en las otras 4 empresas

### Pendiente operativo

- [ ] **G0477 (DOÑA MARIA): editar los 544 kg a mano** en Gestión de inventario — decisión del usuario. Su lote (182) se encasetó el 27/07 y todavía no tiene seguimiento cargado, así que la migración de cuadre no lo cubrió. Ingresos 12.413,6 · stock 11.869,6
- [ ] **Ecuador sigue sin cuadrar** contra su inventario (SAN GUILLERMO 206.318 kg, Kilometro 86 172.984 kg): mismo bug del consumo del cruce sin descontar, más ingresos sin galpón. Trabajo aparte, del tamaño del de Panamá

---

# Tracker — Diagnóstico: saldo de alimento de la grilla ≠ stock (ItalcolEcuador)

**Diagnóstico:** [`fase_de_desarrollo/cuadre_engorde_ecuador_diagnostico_saldo_alimento.md`](fase_de_desarrollo/cuadre_engorde_ecuador_diagnostico_saldo_alimento.md)
**Requerimiento marco:** [`fase_de_desarrollo/cuadre_engorde_ecuador_requerimiento.md`](fase_de_desarrollo/cuadre_engorde_ecuador_requerimiento.md)
**Fecha:** 2026-07-29 · **Alcance de este bloque:** SOLO diagnóstico. No se tocó código, ni SQL, ni datos.

Reporte de operación: en Kilometro 22 / N1 / Galpon-2 / lote 2603, la grilla muestra saldo **3.560 kg** el
primer día cuando el ingreso fue 12.000 y el consumo 480 (esperado 11.520). El usuario aclara que *«solo es en
lo visual porque en el stock sí tenemos lo correcto»*.

## Diagnóstico
- [x] Identificado el lote testigo: `lote_ave_engorde_id = 98` (Kilometro 22, G0036 = «Galpon-2», encaset 14-jun)
- [x] Reproducido en la BD local (dump de prod): `fn_seguimiento_diario_engorde(98)` devuelve 3.560, igual que la pantalla
- [x] Verificado que la fn local es **v10 con ventana v9**, la misma de producción (`pg_proc`)
- [x] **Causa raíz**: la apertura da **−7.960 kg** con 4 movimientos del **ciclo anterior** (lote 65, «2602»), no del 2603
- [x] Mecanismo: la ventana v9 (`fecha_encaset − 10 d` = 04-jun) entra en el ciclo anterior, que cerró el 01-jun
- [x] Asimetría confirmada: se excluyen los 9.000 kg de `(devolución por eliminación)` pero se cuentan los 8.120 kg de traslados de salida
- [x] **El dato persistido está SANO**: `seguimiento_diario_aves_engorde.saldo_alimento_kg` = 11.520 día 1 y 11.380 el 28-jul
- [x] **Cuadra al kilo con el inventario**: stock de G0036 = 10.180 + 1.200 = **11.380 kg** = saldo persistido
- [x] Desvío **constante de −7.960 kg en las 43 filas** ⇒ corrimiento de apertura, no error acumulativo
- [x] Localizada la divergencia: `SeguimientoAvesEngordeEcuadorService.SaldoAlimento.cs:273` llama a la apertura **sin** `diasAlimentoPrevio` (corte viejo, correcto), mientras la fn y `SeguimientoAvesEngordeService` sí aplican la ventana
- [x] Confirmado que es **regresión del 28-jul** (commit `36a8bab`), no deuda histórica

## Alcance medido (dump de prod, 2026-07-29)
- [x] Ecuador, 103 lotes con seguimiento: **63 coinciden · 26 la grilla muestra de MENOS (98.506 kg) · 14 de más (78.501 kg)**
- [x] Aperturas negativas fantasma: **Ecuador 26 lotes / −98.692 kg** · **Panamá 0** (sus 9 aperturas positivas son el caso legítimo de v9)
- [x] Explicado por qué Panamá salió limpio: no encadena ciclos sucesivos por galpón (diferencia D4 del requerimiento)

## Pendiente de decisión del usuario (NADA aplicado)
- [ ] Elegir corrección: (1) acotar la ventana al ciclo propio · (2) simetrizar el filtro de devoluciones (insuficiente por sí sola) · (3) que la grilla lea la columna persistida
- [ ] Auditar los **14 lotes donde la grilla muestra de más** (el lote 20 arrastra +37.880 desde una apertura positiva de 19.880 kg)
- [ ] Al corregir: regresión fila a fila de **Panamá con 0 diferencias** + `dotnet build` + `dotnet test` (1.341)

## Parte 2 — Validación de cierre lote/ciclo/galpón en Ecuador (2026-07-29)

- [x] Estructura: 103 lotes · 35 galpones · 4 corridas (2601=ciclo 1 … 2604=ciclo 4); `numero_corrida` está NULL, la corrida vive en `lote_nombre`
- [x] Verificada la atribución del histórico: 0 ingresos y 0 salidas sin lote, 0 movimientos apuntando a un lote de otro galpón
- [x] ⚠️ Pero `lote_ave_engorde_id` **NO sirve como clave de ciclo**: 14 ciclos tienen su alimento cargado contra el ciclo vecino (890.465 kg). La app usa galpón+fecha, así que no la afecta
- [x] **Nivel galpón (ancla: stock físico): 29/35 cierran EXACTO**; 6 descuadran, 36.799 kg (muy por debajo de los ~490.000 kg que sugería el requerimiento, que mezclaba la bodega de granja)
- [x] **Traspaso entre ciclos (por fecha, sin atribución): 68 traspasos · 54 cuadran · 14 no**; los grandes son del 2601→2602 (carga retroactiva)
- [x] **Ciclo activo (lo que ve la operación): 25 OK · 7 solo la grilla mal · 2 ambos mal · 1 solo el guardado**
- [x] Identificados los **7 galpones del bug de ventana** (28.330 kg): Km22 G0036, Km86 G0039, Km61 G0038, S3b G0048, S2 G0051, S3b G0047, S2 G0052
- [x] Identificados los **3 errores PERSISTENTES de datos**: Km61 G0037 (−10.000), Km86 G0040 (−2.400), CAROLINA G0058 (+480)
- [x] **Hallazgo nuevo**: `RecalcularSaldoAlimentoPorLoteAsync` solo corre al crear/editar un seguimiento ⇒ un ingreso posterior al último día cargado nunca actualiza `saldo_alimento_kg` (S3b G0047 8.470 kg, G0048 10.000 kg el 29-jul)
- [x] ⇒ **Descartada la opción 3** (que la grilla lea el persistido): dejaría la pantalla congelada. La corrección va por acotar la ventana
- [x] **Confirmada la hipótesis de Costos**: los 10 galpones con problema son 6 de corrida 2603 y 4 de corrida 2604; **cero en 2601 y 2602**. Es estructural: la ventana solo alcanza la limpieza del ciclo anterior si ese ciclo existe ⇒ el bug no puede aparecer antes del tercer ciclo

### Pendiente de decisión
- [ ] Corregir la ventana (opción 1) → arregla los 7 galpones sin tocar datos
- [ ] Corregir los 3 descuadres de datos (Km61 G0037, Km86 G0040, CAROLINA G0058)
- [ ] Decidir si el saldo persistido debe recalcularse también al registrar un movimiento de inventario
- [ ] Decidir si se sanean los 6 galpones con descuadre histórico (no afectan lo que ve la operación hoy)

---

# Tracker — Fix: la apertura de alimento deja de heredar el ciclo anterior del galpón

**Plan:** [`fase_de_desarrollo/fix_apertura_alimento_ciclo_anterior_plan.md`](fase_de_desarrollo/fix_apertura_alimento_ciclo_anterior_plan.md)
**Diagnóstico:** [`fase_de_desarrollo/cuadre_engorde_ecuador_diagnostico_saldo_alimento.md`](fase_de_desarrollo/cuadre_engorde_ecuador_diagnostico_saldo_alimento.md)
**Fecha:** 2026-07-29 · Ecuador (activo) + Panamá (preventivo)

Criterio: un lote cuyo ciclo NO se solapa con el mío es **ajeno** y sus movimientos no entran en mi
saldo. Mismo predicado «conviven» que v10 ya usa para el consumo.

## Fase 0 — Simulación previa
- [x] Simulado sobre el dump de prod: Ecuador 30 lotes tocados, negativas 26 → 6, fantasma −98.692 → −13.800
- [x] **Panamá: 0 lotes tocados** (no-op exacto)
- [x] Lote testigo 98: apertura −7.960 → 0

## Fase 1 — SQL
- [x] `fn_seguimiento_diario_engorde.sql` → **v11**: CTE `lotes_ajenos` + filtro en `apert_mov`
- [x] ⚠️ **Corregido en el camino**: el primer intento filtraba además `hist_full`, `hist_alimento`, `fechas_universo` y `docs_por_fecha` y **rompía 4 galpones de CAROLINA** (−2.800 kg c/u). Causa: `lote_ave_engorde_id` NO distingue ciclos — el sistema etiqueta el movimiento con el lote VIGENTE, así que el preiniciador del ciclo nuevo queda con el id del viejo (G0059: 2.800 kg de SM0175 del 16/06 atribuidos al lote 63 siendo del 96). La propiedad solo vale ANTES del primer seguimiento
- [x] Aplicada en BD local y verificada contra el caso testigo

## Fase 2 — Cálculo puro + tests
- [x] `SaldoAlimentoEngordeCalculos.EsDeCicloAjeno` + `ResolverLotesAjenos` (espejo del CTE)
- [x] `SeguimientoAvesEngordeCalculos`: parámetro opcional `lotesAjenos` (default null ⇒ comportamiento idéntico)
- [x] `AperturaAlimentoCicloAnteriorCalculosTests.cs` (NUEVO) — 16 casos (los 8 del plan + bordes y `ResolverLotesAjenos`)

## Fase 3 — Services (unificar los tres caminos)
- [x] `SeguimientoAvesEngordeService.SaldoAlimento.cs` (carga masiva): resuelve y pasa `lotesAjenos`
- [x] `SeguimientoAvesEngordeEcuadorService.SaldoAlimento.cs` (form diario): `lotesAjenos` + **adopta la ventana** + **quita los dos pisos en 0** (v9). Los tres caminos quedan con la misma fórmula

## Fase 4 — Migraciones
- [x] `20260730090000_FnSeguimientoEngordeV11AperturaSinCicloAnterior` (CREATE OR REPLACE, `Down` restaura la v10 completa)
- [x] `20260730091000_RecalcularSaldoAlimentoEngordeV11` (datos, idempotente, backup en `_backup_saldo_alimento_engorde_v11`)
- [x] Designer clonado del último (sin tocar ModelSnapshot: ninguna cambia el modelo)

## Fase 5 — Validación
- [x] `dotnet build` **0 errores / 0 advertencias**
- [x] `dotnet test` **1.357 verdes** (1.341 + 16 nuevos)
- [x] **Panamá fila a fila: 0 diferencias** en saldo, aves, ingreso y documento (5.619 filas / 134 lotes comparadas v10 vs v11)
- [x] Ecuador: 1.425 filas de saldo corregidas · **ninguna fila de seguimiento perdida** (5.495 antes y después); las 24 filas que desaparecen no tenían seguimiento
- [x] **Lote 98: 11.520 el día 1 · 11.380 al cierre = stock físico** ✅
- [x] **Ecuador: 35/35 galpones OK** contra el stock (era 25/35). Superó el objetivo de ≥32
- [x] **Panamá: 25/25 galpones OK** (era 19/25). Su dato guardado arrastraba 176.761 kg de error desde antes del cuadre del 29-jul y el recálculo lo dejó en **0,0**
- [x] Dato guardado == grilla en las **5.495 filas** (0 discrepancias): se acaba la doble verdad
- [x] Migración de datos **idempotente**: 2ª corrida `UPDATE 0`
- [x] **`Down` probado**: fn v10 restaurada y los 5.543 saldos vueltos al original (0 distintos)
- [x] **Up→Down→Up determinista**: las mismas 818 filas (651 Ecuador + 167 Panamá)
- [x] BD local queda con las migraciones aplicadas; tablas de snapshot temporales eliminadas

### Efecto colateral resuelto sin proponérselo
El recálculo también corrige los **3 descuadres persistentes** del diagnóstico (Kilometro 61 G0037 −10.000 kg,
Kilometro 86 G0040 −2.400, CAROLINA G0058 +480) y el **dato guardado que se quedaba viejo**: al reescribirse
desde la fn, el saldo persistido incorpora los ingresos posteriores al último seguimiento.
⚠️ Sigue pendiente que se recalcule **en caliente** al registrar un movimiento de inventario: hoy
`RecalcularSaldoAlimentoPorLoteAsync` solo corre al crear/editar un seguimiento, así que volverá a desfasarse
hasta el próximo registro diario. La grilla, que recalcula en vivo, ya no se ve afectada.

## Fase 6 — Enganche del recálculo a los movimientos de inventario (2026-07-30)

> Cierra el pendiente de la Fase 5. Plan: Parte 2 de `fix_apertura_alimento_ciclo_anterior_plan.md`.

- [x] Verificado el mecanismo: `lote_registro_historico_unificado` es **tabla física** poblada por el trigger `trg_inventario_gestion_movimiento_lote_hist` (`AFTER INSERT` sobre `inventario_gestion_movimiento`) ⇒ la fila existe en el mismo `SaveChanges` y un recálculo posterior ya la ve
- [x] Confirmado por qué la atribución no sirve como clave de ciclo: `fn_lote_ave_engorde_id_desde_ubicacion` devuelve **el lote de id más alto del galpón al momento de insertar** (respalda la decisión de la Parte 1 de filtrar solo en la apertura)
- [x] `SaldoAlimentoEngordeAplicador` (NUEVO, estático con `DbContext`, patrón `RetiroAvesEngordeAplicador`): recalcula **desde la fn**, no en C#, para que el dato guardado sea idéntico a la grilla por construcción
- [x] `TipoEventoInventarioCalculos` (NUEVO, puro): espejo de `fn_tipo_evento_inventario` + la regla de qué movimientos afectan el saldo, **fail-closed** ante un tipo nuevo sin mapear
- [x] `TipoEventoInventarioCalculosTests.cs` (NUEVO) — 29 casos
- [x] **12 llamadas en 10 métodos** de `InventarioGestionService`: ingreso, traslado misma granja (los 2 galpones), traslado inter-granja (origen), recepción de tránsito (N galpones), actualizar fecha de ingreso y de traslado, eliminar ingreso (+ rama huérfana) y eliminar traslado
- [x] NO enganchados a propósito: consumo (lo aporta el seguimiento, se duplicaría), ajuste/eliminación de stock (`INV_OTRO`, invisible al saldo) y nivel granja (sin galpón no hay lote)
- [x] Política de error: **no tumba la operación de inventario** (la proyección se puede reconstruir; la grilla ya muestra bien). Se registra con `ILogger` y un lote corrupto no bloquea el galpón
- [x] `dotnet build` 0/0 · `dotnet test` **1.386 verdes** (1.357 + 29)
- [x] Smoke en BD (transacción revertida): ingreso de 5.000 kg el 28-jul → el trigger escribe el histórico en el acto, la grilla pasa a 16.380 y el aplicador lleva el persistido de 11.380 a **16.380 = grilla** (`UPDATE 1`); 2ª pasada `UPDATE 0`

### ⚠️ Límite estructural medido (no es defecto del enganche)
Un movimiento fechado **estrictamente después del último seguimiento** no puede reflejarse en la columna:
`saldo_alimento_kg` tiene una fila por día de seguimiento y ese día no existe. Comprobado: ingreso el
30-jul con último seguimiento el 28-jul ⇒ la grilla pasa a 16.380 (fila propia de movimiento) y el
`UPDATE` toca 0 filas. Se resuelve solo al cargar el seguimiento siguiente. El caso que rompió
Kilometro 61 G0037 —ingreso fechado EN un día con seguimiento— sí queda cubierto.

### Huecos PREEXISTENTES encontrados de paso (NO corregidos, descuadran grilla y dato guardado por igual)
- [ ] `AnularMovimientoHistoricoAsync` borra el movimiento pero **deja huérfana** su fila del histórico ⇒ el saldo sigue contando el ingreso anulado
- [ ] `RechazarTransitoPendienteAsync` cambia el `movement_type` del movimiento, pero como el trigger es solo `AFTER INSERT` el histórico conserva el tipo viejo y sigue viendo la salida

### Confirmaciones y extra (mapeo con workflow, 2026-07-30)
- [x] **El ciclo de DI era real**: `SeguimientoAvesEngordeService:32` y `…EcuadorService:32` YA inyectan `IInventarioGestionService?` y los cuatro son `Scoped` ⇒ inyectar al revés daba `circular dependency`. El aplicador estático con `DbContext` era la única salida limpia (mismo motivo que `RetiroAvesEngordeAplicador`)
- [x] **`InventarioGestionService` es el ÚNICO escritor EF de `InventarioGestionMovimiento`** ⇒ MigracionService, InventarioGasto, ColombiaInventarioConsumo, SeguimientoLoteLevante y el Puente Panamá **heredan el enganche sin código nuevo**. No hay un segundo camino que se escape
- [x] Conteo corregido: son **12 llamadas en 10 métodos** (no 11 en 8)
- [x] `RegistrarIngresoNivelGranjaAsync` NO se engancha también porque **no hace `SaveChanges`** (commitea el orquestador): un hook ahí leería datos no persistidos
- [x] **Índice nuevo** `20260730120000_IndiceHistoricoUnificadoPorGranjaFecha`: la tabla no tenía ninguno por granja. Medido con EXPLAIN ANALYZE: fn completa **10,3 → 2,7 ms**; consulta del histórico por ubicación **Seq Scan 4,3 ms → Bitmap Index Scan 0,55 ms**. Solo `(farm_id, fecha_operacion)` porque núcleo/galpón se comparan con `COALESCE(TRIM(...))`, que no es sargable
- [x] Migración aplicada en local y **idempotente** (2ª corrida: `already exists, skipping`)


## Fase 7 - Ticket seguimientos de engorde en negativo (2026-07-30)

> Plan: Parte 3 de `fix_apertura_alimento_ciclo_anterior_plan.md`.

- [x] Acotado: solo el **saldo de alimento**; cero aves negativas y cero consumos negativos
- [x] Estado **hoy en produccion** (v10): Ecuador **330 filas / 27 lotes / -1.175.479 kg** (2601:22 · 2602:213 · 2603:89 · 2604:6); Panama 43 / 19
- [x] **Causa**: la v11 tapaba medio agujero. El trigger atribuye el movimiento al lote de id **mas alto al momento de INSERTAR**, asi que la limpieza del ciclo anterior queda con el id del lote VIEJO (la caza `lotes_ajenos`) **o del NUEVO** (no la cazaba nadie). Testigo: SAN GUILLERMO G0033, dos salidas del 13/03 por 5.160 kg - el mismo dia en que cerro el ciclo previo - etiquetadas con el lote nuevo
- [x] **fn v12**: `corte_apertura = GREATEST(encaset - N, fin_ciclo_anterior + 1)`. Los dos criterios son complementarios
- [x] Espejo en C#: `ResolverFinCicloAnterior` + `ResolverCorteApertura` (puros) y los dos services
- [x] 9 tests nuevos (incluido el caso SAN GUILLERMO y el no-toca-el-preiniciador)
- [x] Migraciones `20260730140000_FnSeguimientoEngordeV12AperturaCorteCicloAnterior` + `20260730141000_RecalcularSaldoAlimentoEngordeV12`
- [x] **Resultado: Ecuador 330 -> 25 filas (27 -> 5 lotes); corridas ACTIVAS 2603+2604 de 95 filas a CERO**; Panama sin cambio
- [x] `dotnet build` 0/0 · `dotnet test` **1.395 verdes** · Panama 0 diferencias fila a fila · Down probado · recalculo idempotente · persistido == grilla

### Los 25 que quedan NO son defecto de formula (no se tocan)
- **Lote 12** (Km86 G0040, -9.020): alimento **registrado tarde** - el galpon recibio 182.630 kg fechados despues de que el ciclo cerro
- **Lotes 16, 7, 15** (Sacachun 2, 1 fila c/u): **fila de limpieza** posterior al ultimo seguimiento; el traslado de cierre saca mas de lo calculado
- **Lote 14**: -1 kg de redondeo
- **Panama (43 filas / 19 lotes)**: mismo tipo. Es el deficit real que **v9 decidio mostrar tal cual**


## Fase 8 - Cierre de los dos huecos preexistentes del historico (2026-07-30)

> Plan: Parte 4 de `fix_apertura_alimento_ciclo_anterior_plan.md`.

- [x] Causa comun: el trigger del historico es **solo AFTER INSERT**, asi que ningun UPDATE/DELETE del movimiento se propaga. Cada camino que deshace un movimiento tiene que anular su fila a mano
- [x] Nuevo helper `AnularHistoricoDelMovimientoAsync` (clave `origen_tabla`+`origen_id`, con fallback por ubicacion+item+cantidad, igual que `EliminarIngresoAsync`)
- [x] `AnularMovimientoHistoricoAsync`: anula el historico ANTES de borrar el movimiento (ya no deja huerfana)
- [x] `RechazarTransitoPendienteAsync`: anula el historico al rechazar (antes la salida seguia descontando)
- [x] **Medido: 93 filas huerfanas en la BD**, de las cuales solo **6 (43.640 kg)** inflan el saldo; el resto son devoluciones por eliminacion, INV_CONSUMO o INV_OTRO, que el saldo ya descarta
- [x] **Simulado anularlas: EMPEORA.** Manda 5 ciclos cerrados de saldo 0 a negativo (-11.940 / -5.970 / -4.000 / -1.140 / -790) y el cuadre contra el stock no mejora (35/35 y 25/25 antes y despues)
- [x] ⇒ **NO hay migracion de datos**: esas 6 filas son alimento real que el lote consumio y son las que hacen cerrar esos ciclos en 0
- [x] No se puede repetir: la anulacion ya exige que el stock alcance para revertir, asi que solo puede anular alimento que sigue en bodega
- [x] Rechazo de transito: **0 movimientos rechazados en la BD**, el hueco era real en codigo pero sin datos afectados
- [x] `dotnet build` 0/0 · `dotnet test` **1.395 verdes**
- [x] Smoke en BD con contraste del comportamiento viejo: anulado 16.380 -> 11.380 correcto; borrado sin anular se quedaba en 16.380 con fila huerfana; rechazo 14.380 -> 16.380
- [x] Sin test unitario: el arreglo es Infrastructure (EF+SQL) y el proyecto de tests solo referencia Application. La regla pura ya esta cubierta por `TipoEventoInventarioCalculosTests`


## Fase 9 - Prevencion: los 5 puntos (2026-07-30)

> Plan: `fase_de_desarrollo/prevencion_descuadres_alimento_engorde_plan.md`

- [x] **P1 — La BD garantiza el invariante.** Dos triggers en `inventario_gestion_movimiento` (`_del` AFTER DELETE, `_cancel` AFTER UPDATE del movement_type), copiando el patron que `movimiento_pollo_engorde` ya usaba. **Probado con un DELETE por SQL crudo que nunca pasa por el C#**: el historico queda anulado y el saldo vuelve de 16.380 a 11.380
- [x] P1 — NO se paso a borrado logico a proposito: obligaria a auditar todas las lecturas y una omitida resucita movimientos. El AFTER DELETE cierra el agujero igual para la correctitud, con mucho menos riesgo
- [x] **P2 — `fn_cuadre_alimento_engorde`** + `CuadreAlimentoEngordeCalculos` (puro, 9 tests) + `GET /api/CuadreAlimentoEngorde` (empresa activa, fail-closed, loguea Warning). **Hoy: Ecuador 35/35, Panama 25/25, 0,0 kg**
- [x] P2 — ⚠️ Bug propio detectado al estrenarla: tomaba el saldo de la ULTIMA fila y ademas restaba los movimientos posteriores (doble conteo, 24/35 falsos). Tiene que ser el saldo en el ultimo dia de seguimiento. Corregido
- [x] **P3 — Una sola implementacion.** Los dos services delegan en `SaldoAlimentoEngordeAplicador`, que escribe desde la fn. El service de Ecuador paso de **363 a 187 lineas**. Verificado: persistido == grilla en las 5.495 filas
- [x] **P4 — Gate multipais.** `verificar_paridad_saldo_engorde.sql`, mismo comando dos veces. **Probado que detecta** 3 diferencias inyectadas e identifica el galpon exacto. Regla vinculante agregada a `CLAUDE.md` (seccion nueva «Invariantes que NO se pueden romper»)
- [x] **P5 — Aviso fuera de ciclo.** `AvisoFechaFueraDeCicloCalculos` (puro, 8 tests) + campo aditivo en `InventarioGestionStockDto`, cableado en ingreso y traslado (los dos galpones). Avisa, no bloquea
- [x] Migracion `20260730160000_PrevencionDescuadresAlimentoEngorde` (idempotente, con `Down`)
- [x] `dotnet build` 0/0 · `dotnet test` **1.417 verdes** (1.395 + 22)

### Pendiente anotado (no bloquea)
- [ ] Borrado logico de `inventario_gestion_movimiento` para trazabilidad (auditar todas las lecturas antes)
- [ ] Pantalla de front para el cuadre: hoy solo existe el endpoint

---

## Congelar la liquidacion de un lote de pollo engorde (2026-07-30 → CERRADO 2026-07-31)

> Plan: `fase_de_desarrollo/congelar_liquidacion_lote_engorde_plan.md`
> Estado: **IMPLEMENTADO Y VALIDADO** (2026-07-31). Motivo: la tabla diaria se recalcula en cada
> request, y el cambio de formula del 28-jul movio solas corridas cerradas hacia meses.

### Decisiones tomadas (no reabrir sin motivo)
- [x] El `if` de lectura va **dentro de `fn_seguimiento_diario_engorde`** (v13), no en C#: 4 reportes entran por `CROSS JOIN LATERAL` a la misma fn
- [x] ⚠️ **CAMBIO sobre el plan, justificado por medicion**: la v13 **SIGUE en `LANGUAGE sql`** (no plpgsql). El supuesto del plan («hoy tampoco la inlinea») era FALSO: la v12 SI se inlineaba en los LATERAL (verificado con EXPLAIN — Subquery Scan vs Function Scan) y la variante plpgsql multiplico ×2.8 el Reporte de Costos (min 264 vs 95 ms). La conmutacion quedo como **UNION ALL con quals excluyentes + One-Time Filter** (la alternativa §2.2 que el plan descarto por legibilidad): con copia vigente la rama viva NO se ejecuta (verificado en el plan de ejecucion), la rama congelada lee 2.7× mas rapido que el recalculo (3.0 vs 8.3 ms/lote), y en la medicion menos ruidosa (EXPLAIN ANALYZE de una llamada) la v13 ejecuta MAS rapido que v12 (15.2 vs 18.2 ms) — el ruido de la maquina local (±3× entre corridas identicas) supera cualquier diferencia entre versiones
- [x] Estructura **mixta**: cabecera relacional (con el resumen tipado + `metadata` jsonb con raza/guia/ventana) + detalle **relacional** (`..._fila`, las 47 columnas del `RETURNS TABLE` con tipos VERBATIM). El detalle NO se mapea en EF
- [x] La senal de gate sigue siendo `estado_operativo_lote='Cerrado'`; la copia es un derivado y el invariante lo garantiza el `UNIQUE` parcial (`WHERE anulada_at IS NULL`) + trigger `trg_lote_ave_engorde_anula_congelada`
- [x] Reapertura = **anular** la copia (no DELETE), en la misma transaccion. Re-liquidar crea copia nueva (historial completo de versiones)
- [x] **NO se bloquea el inventario** (bodega por galpon). El saldo persistido queda auto-reparable: el aplicador lee de la fn ya congelada
- [x] Fuera de alcance y **sigue moviendose** (rotulado en pantalla): pestanas Indicadores/Graficas (calculan en el navegador contra la guia viva), Liquidacion Tecnica Ecuador, vista Power BI
- [x] Sin flag por empresa: integridad transversal

### Verificaciones previas (cerradas)
- [x] Conteo real: **EC 20 `Cerrado` (todos con liquidado_at) / 3 reabiertos / PA 0** — coincide con el borrador
- [x] `fn_reporte_indicadores_panama`: definida en la migracion `20260601190727`, **SI pasa por `fn_seguimiento_diario_engorde`** ⇒ queda congelada gratis (era el NO VERIFICADO §12 del plan)
- [x] `EXPLAIN ANALYZE` antes/despues — ver decision de arriba (la medicion CAMBIO el diseno: sql, no plpgsql)

### Implementacion
- [x] Migracion `20260731185300_AddLiquidacionLoteEngordeCongelada` (+ partial `.Fn.cs` con las fns v13/v12): 2 tablas + 3 indices + FK + `fn_congelar/anular/recongelar_liquidacion_engorde` + trigger + fn v13 + backfill — idempotente, con `Down()` que restaura la v12 VERBATIM
- [x] El cuerpo v12 dentro de la migracion se extrajo del `.sql` **por script** (byte a byte, no retipeado)
- [x] `backend/sql/fn_seguimiento_diario_engorde.sql` a v13 (header con la historia completa)
- [x] Entidad + Configuration + `DbSet` de la cabecera — **reescritos** (el borrador previo del working tree usaba jsonb + «la copia es la senal» + DELETE al reabrir: los 3 enfoques que el plan descarto)
- [x] `LiquidacionCongeladaGateCalculos` (lista cerrada de 10 operaciones, mensaje canonico, bypass explicito, variante con nombre de lote) + **24 tests** nuevos
- [x] `LiquidacionCongeladaAplicador` reescrito: Congelar/Anular/RecongelarYRefrescarResumen + **resumen centralizado** (`CalcularResumenVivoAsync` — el cuerpo que estaba DUPLICADO byte a byte en los dos services de seguimiento ahora vive una sola vez; `LeerResumenCongeladoAsync` con fallback a vivo si `total_aves_inicio IS NULL` = copia de backfill)
- [x] `CerrarLoteAsync` **transaccional**: estado → congelar (la foto se toma DESPUES de aplicar 'Cerrado') → realinear saldo persistido → resumen tipado sobre la cabecera. Si el congelado falla, la liquidacion falla entera
- [x] `AbrirLoteAsync` transaccional: anula con usuario+motivo reales ANTES del cambio de estado (el trigger queda de red y no pisa el motivo)
- [x] `ActualizarMermaAsync` actualiza los 2 campos de merma de la cabecera vigente (la merma post-liquidacion es la excepcion permitida)
- [x] Resumen desde la copia en los DOS services (`congeladaAt`/`fnVersion` aditivos en el DTO para el badge)
- [x] Gates B1-B10: editar/eliminar/hard-delete lote · aplicar CuadrarSaldos (el preview NO) · BackfillMetadata · seguimiento reproductora ×4 (corta el camino del trigger de cruce) · lotes reproductora ×5 (helper con `bloquearSiLiquidado`, el generador de codigo NO bloquea) · movimientos ×8 caminos (helper en el ancla, mensaje con nombre de lote, `CompleteAsync` con bypass) · venta Panama · insumos liquidacion Panama · puente Panama (rama YaExiste corta con estado "Liquidado")
- [x] `CorreccionAvesDisponiblesEngordeService`: bypass explicito en `CompleteAsync` + **re-congela** (`origen='correccion'`) y refresca el resumen si aplico cambios sobre un lote con copia vigente
- [x] Fuga multiempresa de `ReporteIndicadorPanamaService` CERRADA: `GuardarLiquidacionAsync` (throw) y `GetReporteAsync` (null/404) resuelven empresa activa + alcance granular fail-closed; + gate B9 (insumos bloqueados post-cierre)
- [x] Endpoint admin `POST /api/LoteAveEngorde/{id}/recongelar` (+ interfaz + service con scoping)
- [x] **Fix encontrado en el smoke**: `DELETE /api/LoteAveEngorde/{id}` y `/hard` devolvian **500** ante regla de negocio (sin catch de `InvalidOperationException`) → ahora 400 (mismo defecto clase que el 4b de produccion)
- [x] Front: badge «🧊 Liquidado · datos congelados» en la barra de tabs + badge con fecha y version en el modal + rotulos «Stock actual de bodega (vivo)» vs «Saldo de alimento liquidado (congelado)» + nota «calculado en vivo contra la guia vigente» en Indicadores y Graficas + campos opcionales en la interfaz TS
- [x] `backend/sql/verificar_congelado_engorde.sql`: chequeos 1-5 de invariantes (todos 0) + auditoria 6 copia-vs-vivo por **anulacion simulada en transaccion con ROLLBACK** (sin duplicar la formula)

### Validacion
- [x] **Gate multipais**: `verificar_paridad_saldo_engorde.sql` antes (linea base 5.600 filas) y despues — **0 diferencias en TODAS las columnas, EC (4.926) y PA (674)**, 5.495/5.495 filas presentes
- [x] Migracion aplicada en BD local :5433 — **backfill: 20 copias EC** (1.097 filas de detalle, 0 descuadres cabecera/detalle), reabiertos sin copia, PA 0; re-ejecucion no duplica
- [x] **Smoke SQL que define la feature**: +1.000 kg crudos a un lote congelado ⇒ hash IDENTICO (no se mueve); mismo experimento en un lote abierto ⇒ hash cambia. UPDATE crudo del estado ⇒ el trigger anula la copia y vuelve el vivo. Doble congelado rechazado. Re-congelado: 2 cabeceras / 1 vigente / checksum estable
- [x] `verificar_congelado_engorde.sql`: 1-5 en cero; auditoria 6: los 20 congelados «OK: el vivo no se ha movido»
- [x] **Smoke API** (backend :5002 Development, JWT + X-Secret-Up minteados — OJO: el X-Secret-Up NO es el formato OpenSSL de crypto-js sino PBKDF2('sanmarino-salt', 10k iter) + AES-CBC con IV prepuesto, replicando `encryptWithCryptoJS` del front):
  - tabla diaria de congelado devuelve la copia (64 filas) · resumen de backfill cae a vivo (`congeladaAt` null)
  - cerrar lote 46 ⇒ copia `origen='cierre'` con resumen tipado (13.500 aves, merma 3/12,5) · resumen desde la copia con `congeladaAt` + `fnVersion=v13` (datos del badge)
  - gates HTTP: editar 400 · seguimiento 400 (mensaje historico byte a byte) · venta 400 **con el nombre del lote** · eliminar 400 (era 500, corregido)
  - recongelar 200 · abrir ⇒ copia anulada con motivo real · re-cerrar ⇒ historial de 3 copias (cierre→recongelado→cierre), 1 sola vigente
  - fuga PA cerrada: GET reporte de lote PA con token EC ⇒ 404 · POST liquidar ⇒ 400
  - lote 46 **restaurado byte a byte** desde snapshot y copias de smoke borradas (quedan las 20 de backfill)
- [x] `dotnet build` 0 errores / 0 advertencias · `dotnet test` **1.481/1.481** (1.457 previos + 24 nuevos) · `yarn build` 0 errores (solo el warning de bundle budget preexistente)
- [x] Instructivo de operacion actualizado (seccion 6b: que significa «congelado» para Costos y como operar reapertura/merma/peso tardio)
- [x] Backend detenido, sin procesos huerfanos; BD local con el estado final correcto (20 copias backfill vigentes)

### Pendiente anotado (no bloquea)
- [ ] Smoke UI en dev server (badge/rotulos): los cambios de front son cosmeticos y compilan; validar visualmente en el proximo arranque de dev
- [ ] Al desplegar: correr `verificar_congelado_engorde.sql` contra RDS despues de la migracion (esperado: copias = lotes Cerrado de prod) y `verificar_paridad_saldo_engorde.sql` antes/despues

---

# Tracker — Carga masiva Seguimiento Levante: movimientos de aves + tab huevos fijo + ocultar estructura

**Plan:** [fase_de_desarrollo/carga_masiva_levante_movimientos_aves_plan.md](fase_de_desarrollo/carga_masiva_levante_movimientos_aves_plan.md)
**Fecha:** 2026-07-31

**Decisiones del usuario:** ocultar visualmente los tipos de estructura (Granjas/Núcleos/Galpones) · movimientos de aves en la carga de levante con **Salida** (valida que el lote destino exista, NO lo acredita) e **Ingreso** en tránsito (NO descuenta al origen) · tab de huevos de levante **fijo** (cae el gate de semana 14; el flag por empresa se conserva).

**Hallazgo clave del análisis:** la carga masiva de levante YA tiene hoja Alimento con inventario real (ingresos/traslados/consumos + balance que rechaza), huevos por 11 categorías e idempotencia — el trabajo nuevo es solo lo de arriba.

## Fase 0 — Análisis
- [x] Exploración exhaustiva (workflow 6 agentes, 192 lecturas): front tipos, backend migración levante, fn SQL vigente, movimientos/traslados de aves, gates de huevos, inventario alimento
- [x] Plan escrito en `fase_de_desarrollo/`

## Fase A — Front: ocultar tipos de estructura
- [x] `funciones/agrupar-tipo-migracion.funcion.ts`: `TIPOS_ESTRUCTURA` + `esTipoEstructura()`
- [x] `migraciones-masivas-page.component.ts`: computed `tiposVisibles` (el historial conserva la lista completa)
- [x] Template paso 1 usa `tiposVisibles()`

## Fase B — Backend: tab huevos fijo (gate de semana 14 cae)
- [x] `HuevosLevanteCalculos`: nueva regla `PermiteHuevos` (solo bloquea fecha anterior al encaset; sin encaset ⇒ permitido); cae `SemanaMinimaHuevosLevante`
- [x] `SeguimientoLoteLevanteService.AplicarGateHuevosLevanteAsync`: mensaje nuevo («fecha anterior al encasetamiento»)
- [x] `MigracionService.Historicos.cs`: cae la rama de semana del gate de la carga masiva + texto de Instrucciones
- [x] Tests `HuevosLevanteCalculosTests` reescritos (gate nuevo: cualquier semana desde el encaset; sin encaset ⇒ permitido)

## Fase C — Front: tab huevos fijo
- [x] `semana-vida-levante.funcion.ts`: `permiteHuevosEnLevante` nueva regla; cae `SEMANA_MINIMA_HUEVOS_LEVANTE`
- [x] `modal-create-edit`: `mostrarTabHuevos` = flag && nueva regla; payload conserva «tab oculto ⇒ null» (atado al flag)

## Fase D — Backend: hoja «Movimientos Aves» en la carga de levante
- [x] `MigracionEsquemas.MovimientosAvesLevante` (7 columnas) + tests de esquema (orden fijado, solo Fecha+Tipo requeridas)
- [x] `MigracionMovimientosAvesCalculos.cs` (NUEVO, puro): `TryMovimiento` (sinónimos), `ClaveArchivo`, `ProyectarSaldoAves` + 8 tests
- [x] Partial `MigracionService.MovimientosAves.cs`: `LeerHojaMovimientosAvesAsync` (contraparte por id/nombre + granja desambiguadora; Salida exige que exista, Ingreso opcional con Advertencia) + `AplicarMovimientosAvesLevanteAsync` (fila diaria extendida + acumulados LPL + clamp 0 + auditoría `movimiento_aves` `MGA-…` Completada + cohorte del ingreso) — espejo unilateral de `TrasladoAvesDesdeSegService`, sin tocar `inventario_aves` ni `MovimientoAvesService.CreateAsync`
- [x] `ProcesarSeguimientoLevanteAsync` lee la hoja; `EjecutarHistoricoPosturaAsync` aplica DESPUÉS de la fn; dry-run no aplica; `FilasOmitidas` suma los ya aplicados; advertencia de saldo de aves proyectado en negativo
- [x] Plantilla: hoja nueva solo levante + lotes en Referencias col H-J (con dropdown de contraparte) + 2 líneas de Instrucciones
- [x] 🔴 **Bug cazado por el smoke y corregido**: `FechaMovimiento`/`Fecha` a MEDIANOCHE se escribe como 00:00 UTC y Npgsql (legacy) la relee en hora local (19:00 del día ANTERIOR) ⇒ la clave de idempotencia comparaba otro día y el reimport DUPLICABA los movimientos. Fix: anclar a MEDIODÍA (patrón `ResolveMovimientoCreatedAt` del inventario)

## Fase E — Validación y cierre
- [x] `dotnet build` 0 errores / 0 advertencias
- [x] `dotnet test` verde — **1.481** (1.480 Application + 1 Domain; incluye los nuevos)
- [x] `yarn build` 0 errores (solo el warning de bundle budget preexistente)
- [x] Smoke API local **15/15** (backend propio :5499, JWT + X-Secret-Up minteados, lote 115/LPL 7 empresa Sanmarino): plantilla con 5 hojas y lotes en Referencias · import → fila diaria del día 1 con mort 10 + SALIDA 200/20 (espejo 6) y día 2 con mort 5/1 + **huevos en semana 2** (80 tot/50 limpio/peso 55.5, flag ON — tab fijo verificado) + INGRESO 300 (espejo 8) · LPL 7 = 15346−15−200+300 = 15431 H / 2293−1−20 = 2272 M con acumulados 300/200/20 · **LPL 6 y 8 intactos** (unilateral) · cohorte única ligada al MGA del ingreso con encaset del origen · reimport = 0 procesadas y 4 omitidas sin duplicar · rechazos: destino inexistente, nombre ambiguo (2 «A374A» reales), salida sin contraparte, tipo inválido, mismo lote
- [x] BD local restaurada al snapshot (0 segs lote 115, 0 MGA, 0 cohortes, LPL 7 = 15346/2293, migracion_masiva max id 164) y backend de smoke detenido — sin procesos huérfanos
- [x] Commit acotado a los archivos de esta tarea (sin footer de atribución)
