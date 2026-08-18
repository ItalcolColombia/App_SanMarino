# PWA F4 — Editar/borrar offline, grafo de operaciones y la clase (b) `requiere_cuadre`

**Fecha:** 2026-08-18
**Plan madre:** [`pwa_offline_first_plan.md`](pwa_offline_first_plan.md) §5.4 (escritura), §5.5 (conflictos), §5.6 (cierre con colas abiertas)
**Antecedente inmediato:** [`pwa_f3_captura_offline_plan.md`](pwa_f3_captura_offline_plan.md) (F3.1–F3.3, ya en `main`)
**Inventario de módulos (NO se rehace acá):** [`pwa_f4_mapeo_modulos_pendientes.md`](pwa_f4_mapeo_modulos_pendientes.md)

> Este plan cubre los dos pendientes declarados «fuera de alcance» de F3.1:
> **(1)** editar/borrar offline · grafo de ops (`client_entity_id`) · modelo `202 + batch_id`, y
> **(2)** la clase (b) `requiere_cuadre`, *modelada y sin emisor*.
> El inventario de qué módulo entra en qué orden ya está medido en el mapeo; acá se construye la
> **infraestructura** que esos módulos necesitan y se corrige lo que hoy está roto.

---

## 0. Estado medido contra el código de HOY (18-ago-2026)

Todo lo de esta tabla se leyó del código actual. **Donde el tracker o un comentario del código
discrepan con lo medido, manda el código** (CLAUDE.md §Regla de schema).

| # | Hecho medido | Dónde |
|---|---|---|
| 1 | `sync_operaciones.estado` es `character varying(20)` **libre** ⇒ guardar `requiere_cuadre` (15 chars) **no necesita migración** | `Persistence/Configurations/SyncOperacionConfiguration.cs` · `Migrations/20260812050558_AddSyncOperaciones.cs` |
| 2 | `ux_sync_operaciones_client_op_id` (UNIQUE) existe y es la garantía real; `BuscarRegistroAsync` es el camino rápido | `SyncOperacionConfiguration.cs` · `Services/Sync/SyncPushService.cs:193` |
| 3 | 🔴 **`requiere_cuadre` no tiene emisor NI lector.** 3 apariciones en `backend/src` (todas literales de doc) y 4 en `frontend/src` (modelo + clasificador + spec). `SyncController` expone **solo** `POST push`: no hay ningún `GET` que lea `sync_operaciones` | `Calculos/SyncPushCalculos.cs:47` · `DTOs/Sync/SyncPushDtos.cs:43` · `Domain/Entities/SyncOperacion.cs:37` · `API/Controllers/SyncController.cs` |
| 4 | 🔴 **El comentario `SyncPushCalculos.cs:42-45` («el alta de levante no valida saldos») está DESACTUALIZADO.** Las **cuatro** capturas validan stock hoy, y lanzan **antes** de persistir | ver #5 |
| 5 | Sitios de validación de stock en el camino que YA sincroniza offline: levante `Crud.cs:94` (CO) y `:121` (EC/PA) en `CreateAsync`, `:256`/`:297` en `UpdateAsync`; engorde `Crud.cs:150,172,390,417`; producción `ProduccionService.Seguimiento.cs:247,643`; reproductora `SeguimientoDiarioLoteReproductoraService.cs:281,446` | `Infrastructure/Services/**` |
| 6 | El rechazo por falta de stock es `InvalidOperationException` con el texto de `StockAtomicoCalculos.MensajeStockInsuficiente` / `StockConsumoValidacionCalculos.MotivoStockInsuficiente`. **No existe ninguna excepción de dominio tipada** en el repo | `Application/Calculos/StockAtomicoCalculos.cs:24` · `Application/Calculos/StockConsumoValidacionCalculos.cs:47,75` |
| 7 | ⇒ **Consecuencia viva:** hoy una captura offline de un día cuyo alimento ya no está en el galpón vuelve como `regla_de_negocio` ⇒ el cliente la manda a la **bandeja** (`clasificar-resultado-push.funcion.ts`, set `DEFINITIVOS`) ⇒ **el dato de campo queda varado**. Es exactamente el escenario que §5.5 del plan madre prohíbe. **No es deuda futura de F4: es un defecto vivo de F3** | `funciones/clasificar-resultado-push.funcion.ts:22-27` |
| 8 | `lote_registro_historico_unificado`: única marca de anulación es `anulado boolean NOT NULL DEFAULT FALSE`. **No hay `anulado_at`, ni motivo, ni usuario.** Clave natural `uq_lote_hist_origen (origen_tabla, origen_id)` | `backend/sql/create_lote_registro_historico_unificado.sql:45,47` |
| 9 | La anulación está **garantizada por la BASE** para dos orígenes: `trg_inventario_gestion_movimiento_lote_hist_del` (AFTER DELETE) y `_cancel` (AFTER UPDATE OF `movement_type`), y `trg_movimiento_pollo_engorde_lote_hist_anula` | `backend/sql/trg_inventario_gestion_anular_historico.sql` · `create_lote_registro_historico_unificado.sql:291-315` |
| 10 | 🔴 Para `seguimiento_diario_aves_engorde` la anulación es **solo C#** (`RetiroAvesEngordeAplicador`, `OrigenTabla = "seguimiento_diario_aves_engorde"`, `TipoEventoBaja = "BAJA_SEGUIMIENTO"`). **No hay trigger.** Y el `DeleteAsync` de engorde corre sus `SaveChanges` sueltos dentro de `try/catch` que se tragan la excepción | `Services/RetiroAvesEngordeAplicador.cs:23,26` · `SeguimientoAvesEngordeEcuadorService.Crud.cs:535,551,577,592,596` |
| 11 | Levante y producción **no** escriben el histórico directo: llegan por el `INV_CONSUMO` de su alimento (`referencia = "Seguimiento lote levante #{id} …"`), y su borrado genera un **ingreso compensatorio** (`RegistrarIngresoAsync` / `AplicarDevolucionAsync`), no una anulación | `SeguimientoLoteLevanteService.Crud.cs:390-430` |
| 12 | 🔴 **`SeguimientoDiarioService.DeleteAsync` abre `BeginTransactionAsync` INCONDICIONAL (`:680`) y se alcanza HOY desde el borrado de levante.** Un borrado dentro de la transacción del push reventaría con `InvalidOperationException` ⇒ el push lo clasificaría como `regla_de_negocio` y lo mandaría a la bandeja en silencio | `Services/SeguimientoDiarioService.cs:663,680` |
| 13 | Transacción **condicional** ya aplicada en 8 sitios: levante `Crud.cs:102,259,396`; producción `Seguimiento.cs:252,648,733`; engorde `Crud.cs:155,395`. **`SeguimientoAvesEngordeEcuadorService.DeleteAsync` no abre ninguna** y **`SeguimientoDiarioLoteReproductoraService` no abre ninguna en todo el archivo** | grep `CurrentTransaction` |
| 14 | Los cuatro `PUT` **reusan el DTO de alta** (`CreateSeguimientoLoteLevanteRequest`, `CrearSeguimientoRequest`, …). No existe DTO de update ⇒ un PUT es un **reemplazo total**, que es justo lo que §5.5 prohíbe sincronizar tarde | los 4 controllers |
| 15 | Los cuatro borrados son **HARD delete** (`Remove(ent)`), no soft-delete | `SeguimientoDiarioService.cs:688,714` · `ProduccionService.Seguimiento.cs:715,741,749` · `SeguimientoAvesEngordeEcuadorService.Crud.cs:596` · `SeguimientoDiarioLoteReproductoraService.cs:662` |
| 16 | 🔴 **Después de guardar sin red, la fila capturada es INVISIBLE.** Las 4 pantallas cierran el modal y recargan la lista desde la caché de lectura, que no la tiene. El único rastro es el toast + el contador de la barra | los 4 componentes de lista, ver §3 |
| 17 | `offline-db.ts` está en **v2** (`consultas` + `outbox`). Agregar un campo plano **no** necesita bump; agregar un **índice** sí, y además la firma `PASOS_MIGRACION: (db) => void` **no recibe la transacción de upgrade**, así que `createIndex` sobre un store existente hoy es imposible sin cambiarla | `shared/offline/offline-db.ts:5,32,90-92` |
| 18 | `ResultadoPush.entidadId` llega del servidor y **nunca se lee** en el cliente | `models/outbox.model.ts:73` |
| 19 | El CI corre 3 verificadores (`verificar-change-detection.js`, `verificar-lista-cacheable.js`, `verificar-ngsw.js`). **No hay ninguno que ate el mapa `ruta → tipo` del cliente al catálogo `SyncPushCalculos.Tipos.Todos` del servidor** | `.github/workflows/deploy-production.yml:109,113` · `frontend/scripts/` |
| 20 | **A4 sigue vivo:** un `GET` escribe `aves_h_actual`/`aves_m_actual` y toca `UpdatedAt` | `Services/Funciones/ProduccionService.Consultas.cs:178-184` |
| 21 | `GET /api/CuadreAlimentoEngorde` ya existe y es **otra cosa** (invariante de alimento por galpón). Colisión de nombre a evitar | `API/Controllers/CuadreAlimentoEngordeController.cs` |
| 22 | El mapeo dice `POST /api/MovimientoPolloEngorde/Panama`. **No existe**: el controller de Panamá solo expone `POST /api/MovimientoPolloEngordePanama/venta-despacho` | `API/Controllers/MovimientoPolloEngordePanamaController.cs:26` |
| 23 | Servicios que **romperían dentro de la transacción del push** tal como están (abren tx incondicional): `InventarioGastoService` (`:527`, `:635`), `MovimientoPolloEngordePanamaService:102`, `TrasladoHuevosService:375`, `TrasladoAvesDesdeSegService.Traslado.cs:30`, `SeguimientoProduccionService.DeleteAsync:350`, `SeguimientoDiarioService.DeleteAsync:680` | grep `BeginTransaction` |
| 24 | `InventarioGestionService` **ya es push-safe**: su `EnTransaccionAsync` comprueba `CurrentTransaction` y lo usan los 6 call-sites de stock | `InventarioGestion/Funciones/InventarioGestionService.StockAtomico.cs:194-206` |

**Lo que el tracker declara probado y lo que NO:** el smoke de F3 dejó probado el `replay:true`, el
estampado de autor (B5), los rechazos tipados y la limpieza por API. **La carrera del índice único NO
se reprodujo**: con 2 y con 8 POST simultáneos del mismo `clientOpId` siempre salió una fila *incluso
con el índice borrado*, porque el `SELECT` previo ya veía la fila commiteada del ganador. O sea: el
índice está probado **a nivel BD** (rechaza el duplicado con 23505) pero **no de punta a punta**.
Este plan no lo da por cerrado — ver §6.5.

---

## 1. Alcance

### Dentro

| Fase | Qué | Por qué en este orden |
|---|---|---|
| **F4.0** | **Emisor + lector de `requiere_cuadre`** para las 4 capturas que ya sincronizan | Es un **defecto vivo** (#7), no una preparación. Hoy se pierde dato de campo |
| **F4.1** | **Editar y borrar offline** de las 4 capturas: colapso local, op de patch, op de anulación, y la fila pendiente **visible** en la lista | Es el pendiente #1 del tracker y lo que el operario pide primero |
| **F4.2** | **Grafo de operaciones**: `client_entity_id` en las 4 tablas + `dependeDe` en el cliente + resolución en la misma transacción | Solo hace falta para el caso «editar algo que ya se intentó enviar» (§2.4) y para el nivel 3 del mapeo |
| **F4.3** | **`202 + batch_id`** — **condicional a una medición** (§2.6) | Sin la medición es construir infraestructura para un problema que puede no existir |

### Fuera, explícito

- **B1** (revocación de sesión), **B8** (rotar las 4 llaves), **A4** (self-heal al patrón aplicador).
  Se declaran como dependencia (§7.1), **no se resuelven acá**. Hay planes hermanos en curso para B1
  y para las sesiones multi-slot.
- **Sesiones multi-slot** por dispositivo.
- **Pull / delta sync** (§5.3 del plan madre) y los tombstones que exige.
- **Los módulos del mapeo** (gastos, gestión de inventario, inventario de aves, movimientos,
  traslados, huevos, ventas). Este plan construye lo que les falta y **deja el orden ya escrito** en
  [`pwa_f4_mapeo_modulos_pendientes.md`](pwa_f4_mapeo_modulos_pendientes.md); su implementación es
  F4.4 en adelante.
- **`lote cerrado` como clase (b).** El plan madre lo lista, y este plan lo **deja afuera a
  propósito** — ver §2.3.
- Marca de cuadre a nivel **entidad** (lote/stock). F4.0 marca la **operación**, no el saldo.
- Portar reglas de negocio a TypeScript (§6 del plan madre) y el corpus compartido C#↔TS.
- El **deploy**. Sigue pendiente de decisión del usuario y arrastra 25 commits.

---

## 2. Enfoque arquitectónico y trade-offs

### 2.1 El orden se invierte respecto del mapeo: `requiere_cuadre` va primero, y es un arreglo

El mapeo lo pone en tercer lugar («habilita el nivel 2»). Contra el código de hoy eso no alcanza: las
cuatro capturas **ya validan stock** (#5) y **ya lanzan antes de persistir**. El galponero que carga
el domingo un día de alimento que el lunes la oficina descargó del galpón, hoy recibe
`regla_de_negocio` y su captura queda en la bandeja de `/diagnostico` esperando que alguien la lea.
Perder el dato de campo es peor que un saldo temporalmente negativo — es literalmente el fundamento
de §5.5 — así que F4.0 arranca el plan.

### 2.2 El emisor: una excepción de dominio, no comparar textos

**Descartado — comparar el mensaje** contra `StockAtomicoCalculos.MensajeStockInsuficiente`. La
constante está marcada «no cambiar el texto» y ya hay flujos que la comparan, pero atar una decisión
de **integridad de datos** a la igualdad de una cadena en español es exactamente la clase de
acoplamiento que se rompe en silencio cuando alguien agrega un punto final.

**Descartado — un booleano en el DTO** (`permitirDescuadre` que manda el cliente). Es el
anti-patrón que CLAUDE.md nombra con nombre y apellido (`AutoNombrePorCorrida`: el front decide y el
back obedece). Además sería falsificable desde cualquier cliente.

**Elegido — subclase de excepción + política de ejecución ambiente:**

1. **`DivergenciaConElMundoException : InvalidOperationException`** (nueva, en
   `Domain/Excepciones/`). Se lanza en los sitios que hoy lanzan por stock/aves insuficientes.
   **Al ser subclase, todo `catch (InvalidOperationException)` existente sigue funcionando idéntico y
   el controller devuelve el mismo 400 con el mismo texto**: el camino online queda byte a byte igual.
   Lleva además datos estructurados (`Faltantes`, `Ubicacion`, `ItemId`) para el detalle del cuadre.
2. **`ISyncEjecucionContexto`** (scoped, `PoliticaDivergencia.Rechazar` por defecto). **Solo
   `SyncPushService` lo pone en `AplicarYMarcar`**, y lo restaura en un `finally`.
   *Trade-off admitido:* es estado ambiente, un olor. La alternativa —pasar la política como
   parámetro— cambia la firma pública de 4 interfaces de service y sus ~12 call-sites desde
   controllers, con riesgo de cambiar comportamiento en el camino online. Un flag scoped, apagado por
   defecto, escrito en **un solo lugar**, es la superficie menor. Se documenta como la única
   excepción y se cubre con un test que verifica que el default es `Rechazar`.
3. **La decisión es pura**: `SyncDivergenciaCalculos.Clasificar(...)` en
   `Application/Calculos/` decide `Rechazar | AplicarYMarcar` a partir del tipo de operación, la
   política y la clase de la falla. El service resuelve datos y delega.
4. **Aplicar igual, sin reintroducir la carrera.** El `UPDATE ... WHERE quantity >= @q` de
   `DescontarStockAtomicoAsync` es **a la vez** guarda de negocio y guarda de concurrencia. Quitarle
   el predicado sin más resucitaría A1. Por eso se agrega un hermano explícito
   `DescontarStockPermitiendoNegativoAsync` — misma sentencia única, sin el predicado de saldo —
   invocado **solo** cuando la política dice `AplicarYMarcar`. Sigue siendo una sola sentencia, así
   que la atomicidad se conserva; lo que cambia es que el saldo puede quedar negativo **y queda
   registrado que quedó así**.

### 2.3 Qué NO se emite como `requiere_cuadre`, y por qué

| Situación | Clase | Tratamiento | Motivo |
|---|---|---|---|
| Stock de alimento insuficiente | **(b)** | `requiere_cuadre` | El alimento ya se consumió; el número físico es el del galpón |
| Aves insuficientes | **(b)** | `requiere_cuadre` | La mortalidad ya ocurrió |
| **Lote cerrado** (`EnsureLoteLevanteAbiertoAsync`, guarda `Cerrado` de engorde) | **queda en (a)** | Sigue rechazando | Aplicar sobre un lote cerrado no es «saldo negativo temporal»: el LPP **ya se creó** con un saldo que ignora esa captura y **reabrir está bloqueado**. Es el escenario §5.6 completo, que necesita la ventana de gracia por `capturado_at` y la telemetría de colas abiertas — otro plan |
| Fecha futura, uuid inválido, contrato obsoleto, empresa no autorizada | **(a)** | Igual que hoy | Sin cambios |

### 2.4 Editar y borrar offline: tres casos, y solo uno necesita el grafo

La pregunta difícil es *«¿cómo se resuelve un editar sobre una op que todavía no subió?»*. La
respuesta depende de **una sola variable ya presente en el modelo**: `OperacionPendiente.intentos`.

| Caso | Estado | Resolución | ¿Grafo? |
|---|---|---|---|
| **1 — nunca salió del dispositivo** (`intentos === 0`) | `pendiente` | **Colapso local**: se reescribe el `payload` de la MISMA operación conservando el `clientOpId`. Borrar = sacar la op de la cola con `ConfirmDialogService`. El servidor nunca supo que existió | **No** |
| **2 — ya se intentó enviar** (`intentos > 0`), sin confirmación | `pendiente` con backoff | **Prohibido colapsar.** Un 504 pudo haberla aplicado. Se encola una op **dependiente** (`…_editar` / `…_borrar`) con `dependeDe: [clientOpId original]` que referencia la entidad por `clientEntityId`, no por id | **Sí** |
| **3 — confirmada** (`entidadId` conocido) | fuera de la cola | Op independiente con el `entidadId` real | No |

**La regla `intentos === 0 ⇒ colapsable` es lo que hace barato todo esto.** El caso real y frecuente
—el galponero corrige un número antes de salir del galpón, sin señal en ningún momento— cae entero en
el caso 1 y se resuelve reescribiendo un objeto en IndexedDB. El grafo solo se paga en el caso 2, que
es el raro (hubo un intento con red y falló).

Para que el caso 3 exista hay que **persistir el `entidadId`** que el servidor ya devuelve y que hoy
se tira (#18).

### 2.5 Grafo de operaciones vs. cola lineal

- **Cola lineal estricta (FIFO con bloqueo de cabeza).** Trivial de implementar. Descartada: una sola
  operación que cae a la bandeja congela **todo** lo que está detrás, incluidas capturas de otro lote
  y otra granja. En una jornada de 16 h eso es el día entero rehén de una fila mala.
- **Grafo con `dependeDe` explícito.** Solo se ponen en cuarentena los **descendientes** de la
  operación rechazada; los hermanos pasan. Cuesta: el cliente calcula la clausura transitiva y el
  servidor agrupa las dependientes para que commiteen juntas.

**Elegido: grafo, con el SUB-GRAFO como unidad atómica — no el lote.** Las operaciones independientes
conservan el resultado por operación de F3 (un rechazo no arrastra a las otras 24); un grupo de
dependencias commitea o revierte como uno. Esto es lo que mantiene viable la respuesta sincrónica
(§2.6): si la unidad atómica fuera el lote entero, un 200 parcial sería incoherente y el `202` dejaría
de ser una opción para volverse una obligación.

**`client_entity_id`:** columna `uuid NULL` en las 4 tablas de seguimiento, con **índice único
parcial** `WHERE client_entity_id IS NOT NULL`, poblada **solo** por el camino de sync. El servidor
resuelve `clientEntityId → id` **dentro de la misma transacción** y devuelve el mapa en la respuesta.
**Sin ids negativos y el cliente nunca reescribe referencias a posteriori** (plan madre).
*Alternativa descartada:* «que el cliente espere la confirmación del alta antes de permitir editar».
Es más simple y es inaceptable: significa que el operario no puede corregir una fila hasta tener
señal, que es justo la situación que la PWA existe para cubrir.

### 2.6 `202 + batch_id` vs. respuesta sincrónica

Hoy: `200` con un resultado por operación, lote de 25 (`MaxOperacionesPorLote`). El umbral que el plan
madre nombra: `OriginReadTimeout` de CloudFront = 30 s, y `RecalcularSaldoAlimentoPorLoteAsync`
reescribe **todos** los registros del lote por cada seguimiento — verificado: engorde lo dispara en
Create, Update **y Delete** (`Crud.cs:598` → `SaldoAlimento.cs:184` → `SaldoAlimentoEngordeAplicador`).
Con F4.1 sumando ediciones y borrados, el costo por operación **sube**.

| Opción | Qué cuesta | Qué resuelve |
|---|---|---|
| **(A) Sincrónico, lote más chico** | nada | Nada estructural: el tamaño del lote es una adivinanza y un lote pesado igual revienta |
| **(A′) Sincrónico con presupuesto de tiempo** | ~1 día. El servidor procesa hasta gastar un presupuesto (config, default 15 s) y devuelve las no tocadas con un código nuevo y estable `no_procesada_en_este_lote`. El cliente las conserva **sin penalidad de backoff** (no es un fallo) | El timeout. No resuelve «iOS mató la app a mitad del envío» más allá de lo que la idempotencia ya cubre |
| **(B) Asíncrono real: `202 + batch_id`** | tabla `sync_lotes`, `BackgroundService` + `Channel`, endpoint de consulta, máquina de estados en el cliente. **Y el trabajo corre fuera del request**: `ICurrentUser`, `ActiveCompanyMiddleware` y el estampado de autor viven en el scope de la petición | El timeout **y** el corte a mitad del envío |

**Elegido: decidir con una medición, no con una intuición.**

> **Medición gate de F4.3.** Cronometrar un lote de 25 operaciones del tipo más pesado (engorde, 25
> días del mismo lote, que es el peor caso porque cada una recalcula el lote completo) contra un
> dataset con volumen real, y **también el peor caso de UNA sola operación**.
> - Si 25 ops entran cómodas bajo 15 s ⇒ **(A′)** y F4.3 se cierra ahí.
> - Si 25 ops no entran pero **una sí** ⇒ **(A′)** con presupuesto: el lote se autolimita.
> - Si **una sola operación** supera el presupuesto ⇒ **(B)** es obligatorio; ningún tamaño de lote lo
>   evita.

En **(B)**, la autorización se **congela en la recepción**: el `user_guid` y la lista de empresas
habilitadas se guardan en la fila del lote mientras el token está vivo, y el worker **usa ese
snapshot** en vez de re-derivarlo. Es más fail-closed que recalcular sin token, y no contradice la
regla «un rechazo se re-evalúa en cada intento»: lo que se congela es la recepción, no el veredicto.

### 2.7 La precondición de UX que nadie escribió: la fila pendiente tiene que verse

No se puede editar lo que no se ve. Hoy (#16) las 4 pantallas cierran el modal y recargan desde la
caché de lectura, que no tiene la fila recién capturada. El único rastro es un toast que desaparece.

F4.1 agrega una función pura `fusionarPendientes(filasDelServidor, opsPendientes)` en
`shared/offline/funciones/` que mezcla el outbox en la lista que la pantalla muestra, marcando cada
fila fusionada como **pendiente** (badge). El editar/borrar sobre una fila pendiente enruta al outbox
en vez de a HTTP.

⚠️ **La fusión se hace al cargar/refrescar y se guarda en un campo**, nunca en un getter del template:
un getter que devuelve un array nuevo por ciclo rompe change detection (CLAUDE.md). Y todo componente
o modal nuevo lleva `changeDetection: ChangeDetectionStrategy.Eager` explícito.

### 2.8 El patch, no el PUT — y sin abrir un segundo camino de escritura

Los 4 `PUT` reusan el DTO de alta (#14): sincronizar uno tarde **reemplaza el objeto entero** y pisa
`metadata` (el `huevoItems` de Santa Reyes, la marca de arrastre de huevos) y las columnas
`traslado_*`. Es el descuadre en dos granjas que describe §5.5.

Por eso la operación de edición offline **no es un PUT**: su payload es
`{ entidadId | clientEntityId, camposCapturados: string[], valores: {…} }`.

**Y no crea un escritor paralelo.** El servidor: (1) lee la fila vigente, (2) aplica **solo** los
campos de `camposCapturados` con `SyncPatchCalculos.Fusionar(...)` (puro, con lista blanca de
«columnas del galponero» por tipo), (3) llama **al mismo `UpdateAsync` que usa el controller** con el
DTO completo ya fusionado. Así las guardas (`EnsureLoteLevanteAbiertoAsync`, validación de stock,
recálculo de saldo) corren exactamente una vez y en el lugar donde ya viven. El patch es un **paso de
fusión previo**, no una segunda fórmula.

### 2.9 Borrar offline = anular, nunca dejar un agujero

**Este es el riesgo central del plan.** Los hechos (#8–#12, #15):

- Los 4 borrados son **hard delete**.
- El histórico unificado lo llena un trigger **AFTER INSERT**: ningún `UPDATE`/`DELETE` del origen se
  propaga solo.
- Para `inventario_gestion_movimiento` y `movimiento_pollo_engorde` la anulación **la garantiza la
  base** (triggers `_del`, `_cancel`, `_anula`).
- Para `seguimiento_diario_aves_engorde` la garantía es **solo la disciplina del C#**, y encima dentro
  de `try/catch` que se tragan la excepción.
- El borrado de levante **no anula**: emite un **ingreso compensatorio**
  (`RegistrarIngresoAsync` / `AplicarDevolucionAsync`), y en EC/PA eso vive dentro de un `catch` que
  solo loguea.

Reglas que este plan impone:

1. **El borrado offline llama al MISMO `DeleteAsync` que llama el controller.** Nada de un
   «movimiento compensatorio» inventado por el camino de sync. Lo que hoy anula correctamente se
   preserva porque no se toca.
2. **Ninguna fila de `lote_registro_historico_unificado` se borra jamás desde el camino de sync**, y
   `sync_operaciones` no admite `DELETE` (solo se marca resuelta).
3. 🔴 **Se cierra el hueco de #10 con un trigger**, replicando el patrón que ya existe:
   `AFTER DELETE ON seguimiento_diario_aves_engorde` ⇒
   `UPDATE lote_registro_historico_unificado SET anulado = TRUE WHERE origen_tabla =
   'seguimiento_diario_aves_engorde' AND origen_id = OLD.id AND NOT anulado`.
   Es literalmente el argumento del header de `trg_inventario_gestion_anular_historico.sql`: *que el
   invariante lo garantice la base, no la disciplina del código*. El borrado offline es un **llamador
   nuevo** de un camino cuya anulación depende de C# dentro de un `catch`. **Es el cambio de backend
   más valioso de todo el plan.**
4. **Transacciones**: `SeguimientoDiarioService.DeleteAsync:680` pasa a condicional (mismo patrón
   quirúrgico ya aplicado 8 veces); `SeguimientoAvesEngordeEcuadorService.DeleteAsync` y el
   `DeleteAsync` de reproductora se envuelven en transacción condicional. Llamados desde el
   controller se comportan **idéntico** a hoy.

---

## 3. Archivos a crear / modificar (rutas verificadas)

### 3.1 Backend — F4.0 (`requiere_cuadre`)

| Acción | Ruta |
|---|---|
| **Crear** | `backend/src/ZooSanMarino.Domain/Excepciones/DivergenciaConElMundoException.cs` |
| **Crear** | `backend/src/ZooSanMarino.Application/Calculos/SyncDivergenciaCalculos.cs` (puro: clasifica falla + política ⇒ `Rechazar`/`AplicarYMarcar`, y arma el detalle del cuadre) |
| **Crear** | `backend/src/ZooSanMarino.Application/Interfaces/ISyncEjecucionContexto.cs` + impl scoped en `Infrastructure/Services/Sync/SyncEjecucionContexto.cs` |
| **Crear** | `backend/src/ZooSanMarino.Application/DTOs/Sync/SyncCuadreDtos.cs` |
| **Crear** | `backend/src/ZooSanMarino.Infrastructure/Services/Sync/Funciones/SyncPushService.Cuadre.cs` (partial: reintento bajo política + registro `requiere_cuadre`) |
| **Crear** | `backend/src/ZooSanMarino.Infrastructure/Services/Sync/SyncCuadreService.cs` + `Application/Interfaces/ISyncCuadreService.cs` (listar / resolver) |
| **Modificar** | `Application/Calculos/SyncPushCalculos.cs` — corregir el doc obsoleto de `Estados.RequiereCuadre` (#4); agregar los códigos `no_procesada_en_este_lote` y `dependencia_rechazada` a `Errores` |
| **Modificar** | `Infrastructure/Services/InventarioGestion/Funciones/InventarioGestionService.ValidacionConsumo.cs` — lanzar `DivergenciaConElMundoException` (subclase ⇒ sin cambio de comportamiento online) |
| **Modificar** | `Infrastructure/Services/InventarioGestionService.cs:1570,1581,1666` y `Infrastructure/Services/ColombiaInventarioConsumoService.cs:218` — idem |
| **Modificar** | `Infrastructure/Services/InventarioGestion/Funciones/InventarioGestionService.StockAtomico.cs` — agregar `DescontarStockPermitiendoNegativoAsync` |
| **Modificar** | `API/Controllers/SyncController.cs` — `GET /api/Sync/cuadres`, `POST /api/Sync/cuadres/{id}/resolver` |
| **Modificar** | `API/Program.cs:256` — DI de `ISyncEjecucionContexto` y `ISyncCuadreService` |

> **Nombres:** el endpoint y la pantalla se llaman **«Capturas por cuadrar»**, no «cuadre de
> alimento»: `GET /api/CuadreAlimentoEngorde` ya existe y es otra cosa (#21).

### 3.2 Backend — F4.1 / F4.2 (editar, borrar, grafo)

| Acción | Ruta |
|---|---|
| **Crear** | `Application/Calculos/SyncPatchCalculos.cs` (puro: lista blanca de «columnas del galponero» por tipo + fusión) |
| **Crear** | `Application/Calculos/SyncGrafoCalculos.cs` (puro: orden topológico, detección de ciclos, cuarentena de descendientes) |
| **Crear** | `Infrastructure/Services/Sync/Funciones/SyncPushService.Editar.cs` y `SyncPushService.Borrar.cs` |
| **Modificar** | `Application/Calculos/SyncPushCalculos.cs` — 8 tipos nuevos (`seguimiento_{levante,produccion,engorde,reproductora_engorde}_{editar,borrar}`) en `Tipos` y en `Tipos.Todos` |
| **Modificar** | `Application/DTOs/Sync/SyncPushDtos.cs` — `ClientEntityId`, `DependeDe`, y `MapaEntidades` en la respuesta |
| **Modificar** | `Infrastructure/Services/Sync/SyncPushService.cs` — agrupar sub-grafos, transacción por grupo |
| **Modificar** | `Infrastructure/Services/Sync/Funciones/SyncPushService.Levante.cs` — despacho de los 8 tipos nuevos |
| 🔴 **Modificar** | `Infrastructure/Services/SeguimientoDiarioService.cs:680` — transacción **condicional** (hoy incondicional, #12) |
| 🔴 **Modificar** | `Infrastructure/Services/SeguimientoAvesEngordeEcuador/Funciones/SeguimientoAvesEngordeEcuadorService.Crud.cs` (`DeleteAsync`, L490) — envolver en transacción condicional |
| 🔴 **Modificar** | `Infrastructure/Services/SeguimientoDiarioLoteReproductoraService.cs` (`DeleteAsync`, L598) — idem |
| **Modificar** | Las 4 entidades + `Configurations` para `ClientEntityId`: `Domain/Entities/SeguimientoDiario.cs`, `SeguimientoDiarioAvesEngorde.cs`, `SeguimientoProduccion.cs`, `SeguimientoDiarioLoteReproductoraAvesEngorde.cs` |

### 3.3 Frontend

| Acción | Ruta |
|---|---|
| **Modificar** | `frontend/src/app/shared/offline/offline-db.ts` — **v3**: store `recibos`, índices nuevos del `outbox` (`por_dependencia`, `por_entidad`), y **cambio de firma de `PASOS_MIGRACION` a `(db, tx) => void`** para poder hacer `createIndex` sobre un store existente (#17) |
| **Modificar** | `frontend/src/app/shared/offline/models/outbox.model.ts` — `clientEntityId?`, `dependeDe?`, `entidadId?`, `estado` suma `'bloqueada'`; tipo `ReciboCuadre` |
| **Crear** | `frontend/src/app/shared/offline/funciones/colapso-local.funcion.ts` (pura: decide colapsar vs. encadenar según `intentos`) |
| **Crear** | `frontend/src/app/shared/offline/funciones/grafo-dependencias.funcion.ts` (pura: clausura transitiva, orden de envío, cuarentena) |
| **Crear** | `frontend/src/app/shared/offline/funciones/fusionar-pendientes.funcion.ts` (pura: mezcla outbox + filas del servidor) |
| **Modificar** | `frontend/src/app/shared/offline/funciones/decidir-encolable.funcion.ts` — el mapa pasa a aceptar `PUT`/`DELETE` con id (`…/{id}$`) para los 4 recursos |
| **Modificar** | `frontend/src/app/shared/offline/funciones/clasificar-resultado-push.funcion.ts` — `requiere_cuadre` sigue devolviendo `borrar` **pero deja recibo**; nuevos códigos `no_procesada_en_este_lote` (reintentar sin penalidad) y `dependencia_rechazada` (bandeja) |
| **Modificar** | `frontend/src/app/shared/offline/outbox.service.ts` — `colapsar()`, `encadenar()`, `bloquearAntesDeEnviar()`, `guardarRecibo()` |
| **Modificar** | `frontend/src/app/shared/offline/sync.service.ts` — envío por sub-grafo, mapa `clientEntityId → id`, persistir `entidadId`, `202 + batch_id` si se adopta |
| **Modificar** | `frontend/src/app/shared/components/pwa-barra-estado/pwa-barra-estado.component.ts` (+ `.html`) — aviso «N capturas se aplicaron con diferencias» |
| **Modificar** | `frontend/src/app/features/diagnostico/diagnostico-page.component.ts` / `.html` — recibos de cuadre y estado del grafo (ya es `Eager`) |
| **Crear** | `frontend/src/app/features/sync-cuadres/` — bandeja «Capturas por cuadrar» del supervisor (`changeDetection: Eager`, `ToastService`, `ConfirmDialogService`) |
| **Modificar** (4) | Las cuatro listas, para fusionar pendientes y enrutar editar/borrar al outbox: `features/lote-levante/pages/seguimiento-lote-levante-list/seguimiento-lote-levante-list.component.ts` · `features/lote-produccion/pages/lote-produccion-list/lote-produccion-list.component.ts` · `features/aves-engorde/pages/seguimiento-aves-engorde-list/seguimiento-aves-engorde-list.component.ts` · `features/seguimiento-diario-lote-reproductora/pages/seguimiento-diario-lote-reproductora-list/seguimiento-diario-lote-reproductora-list.component.ts` |
| **Modificar** | `features/lote-levante/pages/seguimiento-lote-form/seguimiento-lote-levante-form.component.ts` — el formulario de página (hoy `OnPush`; **verificar** que el 202 repinta al editar offline) |
| **Crear** | `frontend/scripts/verificar-mapa-encolable.js` + entrada en `.github/workflows/deploy-production.yml` — corta el gate si un tipo del mapa del cliente no está en `SyncPushCalculos.Tipos.Todos` (#19) |

---

## 4. Cambios de BD / SQL / migraciones

Todas idempotentes (`IF NOT EXISTS`), y con su **espejo `.sql`** en `backend/sql/` para el trigger.

### 4.1 `AddClientEntityIdSeguimientos`

```sql
ALTER TABLE seguimiento_diario_levante                        ADD COLUMN IF NOT EXISTS client_entity_id uuid NULL;
ALTER TABLE seguimiento_diario_produccion                     ADD COLUMN IF NOT EXISTS client_entity_id uuid NULL;
ALTER TABLE seguimiento_diario_aves_engorde                   ADD COLUMN IF NOT EXISTS client_entity_id uuid NULL;
ALTER TABLE seguimiento_diario_lote_reproductora_aves_engorde ADD COLUMN IF NOT EXISTS client_entity_id uuid NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_seg_levante_client_entity
    ON seguimiento_diario_levante (client_entity_id) WHERE client_entity_id IS NOT NULL;
-- …ídem para las otras tres
```

Índice **parcial** a propósito: en un único normal los `NULL` tampoco colisionan, pero el parcial es
mucho más chico (las filas históricas son millones y todas nulas) y **declara la intención**: la
columna la puebla solo el camino offline.

### 4.2 `AddCuadreEnSyncOperaciones`

```sql
ALTER TABLE sync_operaciones ADD COLUMN IF NOT EXISTS detalle_cuadre      jsonb  NULL;
ALTER TABLE sync_operaciones ADD COLUMN IF NOT EXISTS resuelto_at         timestamptz NULL;
ALTER TABLE sync_operaciones ADD COLUMN IF NOT EXISTS resuelto_por_user_id integer NULL;
ALTER TABLE sync_operaciones ADD COLUMN IF NOT EXISTS nota_cuadre         text   NULL;

CREATE INDEX IF NOT EXISTS ix_sync_operaciones_cuadre_pendiente
    ON sync_operaciones (company_id, recibido_at)
    WHERE estado = 'requiere_cuadre' AND resuelto_at IS NULL;
```

`estado` **no** se toca: `varchar(20)` ya admite `requiere_cuadre` (#1).

### 4.3 🔴 `AnularHistoricoAlBorrarSeguimientoEngorde` — el más importante

Espejo en `backend/sql/trg_seguimiento_engorde_anular_historico.sql`, hermano exacto de
`trg_inventario_gestion_anular_historico.sql`:

```sql
CREATE OR REPLACE FUNCTION trg_lote_hist_anular_desde_seguimiento_engorde()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    UPDATE public.lote_registro_historico_unificado
       SET anulado = TRUE
     WHERE origen_tabla = 'seguimiento_diario_aves_engorde'
       AND origen_id    = OLD.id
       AND NOT anulado;
    RETURN OLD;
END;
$$;

DROP TRIGGER IF EXISTS trg_seguimiento_diario_aves_engorde_lote_hist_del
    ON public.seguimiento_diario_aves_engorde;
CREATE TRIGGER trg_seguimiento_diario_aves_engorde_lote_hist_del
AFTER DELETE ON public.seguimiento_diario_aves_engorde
FOR EACH ROW EXECUTE FUNCTION trg_lote_hist_anular_desde_seguimiento_engorde();
```

`origen_tabla` y `OLD.id` verificados contra `RetiroAvesEngordeAplicador.OrigenTabla` y contra el
`HasColumnName("id")` de `SeguimientoDiarioAvesEngordeConfiguration.cs:16`.

**Redundante con el C# de hoy y a propósito**: es idempotente (`AND NOT anulado`) y cubre el borrado
offline, el borrado por API y cualquier `DELETE` manual por SQL.

### 4.4 (Solo si la medición de §2.6 obliga a (B)) `AddSyncLotes`

`sync_lotes(id, batch_id uuid UNIQUE, user_id, user_guid, empresas_habilitadas int[], recibido_at,
estado, procesado_at)` + FK lógica desde `sync_operaciones.batch_id`.

### 4.5 Orden de despliegue

`main-produccion` está **25 commits atrás** y el contenedor tiene `Database__RunMigrations=true`: estas
migraciones se aplican **solas** al arrancar, junto con las 25 pendientes. El trigger de §4.3 debe ir
en la **misma migración** que el cambio de transacción condicional del §3.2: una migración aplicada
deja inválido a cualquier binario viejo (CLAUDE.md §Ciclo de vida del backend local, punto 5).

---

## 5. Reglas de negocio

### 5.1 Orden de aplicación

1. El cliente ordena por `creadoEn` y calcula los **sub-grafos** con `grafo-dependencias.funcion.ts`.
2. Un sub-grafo **no se parte entre lotes**: si no entra completo en el lote de 25, se manda en el
   siguiente. Partirlo dejaría la mitad aplicada.
3. El servidor aplica cada sub-grafo en **orden topológico** dentro de **una** transacción
   (`SyncGrafoCalculos.Ordenar`). Un ciclo se rechaza entero con `validacion` (es un bug del cliente,
   no un hecho del mundo).
4. Las operaciones independientes conservan el comportamiento de F3: resultado por operación, un
   rechazo no arrastra a las demás.
5. Un rechazo dentro de un sub-grafo pone a sus **descendientes** en `dependencia_rechazada`
   (bandeja, sin reintento ciego); los **hermanos** no se tocan.

### 5.2 Conflictos: qué pasa y qué ve el operario

| Situación | Clase | Servidor | Cliente | Lo que ve el operario |
|---|---|---|---|---|
| Falta alimento / faltan aves al sincronizar | **(b)** | Aplica, `estado='requiere_cuadre'`, `detalle_cuadre` con el faltante, genera la tarea | Borra la op de la cola **y deja recibo** | Toast + barra: *«Se guardó con diferencias de saldo. El supervisor lo va a revisar.»* — nunca un error rojo por algo que ya está aplicado |
| Lote cerrado mientras estaba offline | (a) | Rechaza | Bandeja | *«El lote se cerró. Esta captura necesita que la oficina reabra el lote.»* |
| El servidor ya tenía la operación | — | `replay:true` con la respuesta original | Borra | Nada. Es el caso sano |
| Editar una fila que la oficina borró | (a) | `regla_de_negocio` | Bandeja | *«La fila que estabas editando ya no existe.»* con el payload a la vista para poder recapturar |
| Editar una fila que la oficina ya editó | — | **Patch, no reemplazo**: solo se pisan los campos que el operario tocó | Borra | Nada. Los campos del sistema y los que él no tocó quedan como están |
| Op dependiente de una rechazada | (a) | `dependencia_rechazada` | Bandeja | *«Depende de otra captura que quedó pendiente de revisión.»* |
| Lote no procesado por presupuesto de tiempo | — | `no_procesada_en_este_lote` | Cola, **sin sumar intento ni backoff** | Nada. Sale en el siguiente envío |

### 5.3 Cuándo se emite `requiere_cuadre` — la regla exacta

Se emite **si y solo si** se cumplen las tres:

1. La falla es una `DivergenciaConElMundoException` (stock o aves insuficientes), **no** cualquier
   `InvalidOperationException`.
2. La operación llega por **`POST /api/Sync/push`** (política `AplicarYMarcar`). En el tráfico normal
   la política es `Rechazar` y el comportamiento es **byte a byte el de hoy, mensaje incluido**.
3. El tipo de operación está en el alcance de F4.0 (las 4 capturas diarias). Un tipo fuera de alcance
   sigue rechazando.

**Nunca** se emite por `validacion`, `contrato_obsoleto`, `empresa_no_autorizada`,
`duplicado_en_lote`, ni por lote cerrado (§2.3).

### 5.4 Editar / borrar: reglas duras

- `intentos === 0` ⇒ **colapso local** (mismo `clientOpId`). `intentos > 0` ⇒ **op encadenada**. No hay
  tercera opción, y la regla se evalúa **con la cola bloqueada** (`bloquearAntesDeEnviar`) para que un
  push en curso no cambie `intentos` en el medio.
- Descartar una op de la cola **solo** por decisión explícita de una persona vía
  `ConfirmDialogService`. Ningún camino automático borra trabajo no sincronizado — ni el logout, ni el
  cambio de empresa, ni el kill switch (regla de F3, no se relaja).
- El borrado offline **llama al `DeleteAsync` de siempre**. Prohibido inventar un movimiento
  compensatorio en el camino de sync.
- 🔴 **Ninguna fila de `lote_registro_historico_unificado` se borra.** Si una operación offline deshace
  un movimiento, su fila va con `anulado = true`.
- El autor y la empresa los sigue estampando el servidor (B5/B6 en el camino de sync). Un
  `…_borrar` **no** puede borrar una fila de otra empresa aunque el id exista.

---

## 6. Casos de prueba

### 6.1 xUnit — cálculo puro (`backend/tests/ZooSanMarino.Application.Tests/`)

**`SyncDivergenciaCalculosTests.cs`**
- `Con_politica_Rechazar_una_divergencia_se_rechaza_igual_que_hoy` — el default no cambia nada.
- `Con_politica_AplicarYMarcar_una_divergencia_devuelve_requiere_cuadre`.
- `Una_InvalidOperationException_comun_nunca_es_divergencia` (no basta con el texto).
- `Lote_cerrado_no_es_clase_b_ni_con_la_politica_encendida`.
- `El_detalle_del_cuadre_conserva_item_ubicacion_y_faltante`.

**`SyncPatchCalculosTests.cs`**
- `Fusionar_solo_toca_los_campos_declarados`.
- `🔴 Fusionar_nunca_pisa_metadata_ni_las_columnas_traslado` — el caso de las dos granjas de §5.5.
- `Un_campo_fuera_de_la_lista_blanca_se_ignora_y_se_reporta`.
- `Fusionar_con_camposCapturados_vacio_es_no_op`.

**`SyncGrafoCalculosTests.cs`**
- `Orden_topologico_respeta_las_dependencias`.
- `Un_ciclo_se_rechaza_entero_como_validacion`.
- `Un_rechazo_pone_en_cuarentena_solo_a_sus_descendientes`.
- `Un_subgrafo_que_no_entra_en_el_lote_no_se_parte`.

**`SyncPushCalculosTests.cs`** (extender las 22 existentes)
- `Los_ocho_tipos_nuevos_son_conocidos` · `El_catalogo_de_tipos_no_tiene_duplicados` (ya existe, debe
  seguir verde con 12 tipos) · `no_procesada_en_este_lote_no_es_un_rechazo`.

### 6.2 Karma (`frontend/src/app/shared/offline/funciones/*.spec.ts`)

- `colapso-local.funcion.spec.ts`: `intentos === 0` colapsa conservando el `clientOpId`;
  `intentos > 0` **nunca** colapsa; borrar con `intentos === 0` saca de la cola; con `intentos > 0`
  encadena.
- `fusionar-pendientes.funcion.spec.ts`: la fila pendiente aparece marcada; una op ya confirmada no se
  duplica contra la fila del servidor; el orden por fecha se conserva.
- `grafo-dependencias.funcion.spec.ts`: espejo de los casos de `SyncGrafoCalculosTests`.
- `clasificar-resultado-push.funcion.spec.ts` (extender): `requiere_cuadre` sigue devolviendo `borrar`
  **y** deja recibo; `no_procesada_en_este_lote` ⇒ `reintentar` **sin** sumar backoff;
  `dependencia_rechazada` ⇒ `bandeja`.
- `decidir-encolable.funcion.spec.ts` (extender): `PUT /api/SeguimientoLoteLevante/12` encola;
  `PUT /api/SeguimientoLoteLevante/12/algo` **no**; `DELETE` idem.
- `offline-db.spec.ts` (extender): migración **v1→v3** y **v2→v3** sin perder nada de `outbox`.
- Verificador nuevo `verificar-mapa-encolable.js` corriendo en CI.

### 6.3 🔴 Caso de anulación del histórico unificado (el que no puede fallar)

Smoke HTTP contra el back local (JWT minteado + `X-Secret-Up`), con aserciones SQL:

1. **Alta offline de un seguimiento de engorde** → push → existe **1** fila en
   `lote_registro_historico_unificado` con `origen_tabla='seguimiento_diario_aves_engorde'`,
   `tipo_evento='BAJA_SEGUIMIENTO'`, `anulado = false`.
2. **Edición offline** (patch de mortalidad) → push → **sigue habiendo 1 fila** (clave única
   `origen_tabla+origen_id`), con el total del día actualizado y `anulado = false`.
3. **Borrado offline** → push → la fila **existe todavía** y `anulado = true`.
   `SELECT count(*) FROM lote_registro_historico_unificado WHERE origen_id = <id>` = **1** antes y
   después: **cero borrados físicos**.
4. **Prueba del trigger, no del C#**: repetir (3) con la anulación de `RetiroAvesEngordeAplicador`
   neutralizada (hook de test). El trigger de §4.3 debe dejar `anulado = true` igual. Sin este caso,
   §4.3 no está probado — solo escrito.
5. **`GET /api/CuadreAlimentoEngorde` sigue en 0 descuadrados** antes y después de la secuencia
   (CLAUDE.md: *«el cuadre se mira, no se espera»*).
6. **Levante**: borrado offline ⇒ el ingreso compensatorio existe como fila **nueva**
   (`INV_INGRESO`) y la fila de consumo original **no se borró**. Documentar el resultado: son dos
   filas que se cancelan, no una anulación — y eso es lo que ya hace el camino online.

### 6.4 Gate multipaís (obligatorio, CLAUDE.md)

El borrado y la edición de engorde tocan el saldo de alimento
(`SaldoAlimentoEngordeAplicador.RecalcularPorLoteAsync`), así que aplica el gate completo:

```bash
psql ... -f backend/sql/verificar_paridad_saldo_engorde.sql   # antes (congela)
psql ... -f backend/sql/verificar_paridad_saldo_engorde.sql   # después (compara)
```

**Toda empresa que no sea el objetivo tiene que salir con 0 en todas las columnas.** Ecuador encadena
3-4 ciclos por galpón y Panamá no: medir contra una sola empresa ya costó 26 lotes con apertura
negativa.

### 6.5 La carrera que F3 no pudo reproducir

El tracker es honesto: con 2 y con 8 POST simultáneos del mismo `clientOpId` siempre salió **1** fila
**incluso con el índice único borrado**, porque `BuscarRegistroAsync` ya veía la fila commiteada del
ganador. El respaldo (el índice) está probado a nivel BD, **no de punta a punta**.

Lo que sí lo probaría, y este plan incluye:
- **Test de integración con el atajo desactivado**: un hook de test que salte `BuscarRegistroAsync`,
  dos tareas concurrentes con el mismo `clientOpId` ⇒ una aplica, la otra cae en el
  `catch (DbUpdateException) when (EsViolacionDeUnicidad)` y devuelve `Replay = true` con el **mismo**
  `EntidadId`. Es la única forma de ejercitar la rama `:162-175`, que hoy nunca se ejecuta en las
  pruebas.
- **Y el caso nuevo que F4 agrega**: dos operaciones del mismo sub-grafo llegando en lotes distintos
  (la dependiente antes que su padre, por reordenamiento de red) ⇒ la dependiente debe quedar
  `dependencia_rechazada` y **no** aplicarse contra un `clientEntityId` inexistente.

### 6.6 Smoke doble por empresa

Con los dos perfiles reales de operario ya identificados en F3 (`alexlondono@sanmarino.com.co`,
empresa 1 · `ladymalave@ecuitalcol.com`, empresa 3), y **limpieza por la API**; si la API da 403, por
SQL pero **anulando** el histórico, nunca borrándolo.

### 6.7 Validación de build

`cd backend && dotnet build` (0 errores, sin advertencias nuevas) + `dotnet test`; `cd frontend &&
yarn build` (único warning aceptado: el de budget) + `yarn test`. **Matar cualquier backend vivo antes
de compilar** y dejar el puerto libre al terminar.

---

## 7. Riesgos, dependencias y qué NO hace este plan

### 7.1 Dependencias declaradas (no se resuelven acá)

| Ítem | Estado | Por qué bloquea a F4 |
|---|---|---|
| **B1** — `jti` + `sesiones_activas` + refresh | Abierto. Plan hermano en curso | 🔴 **F4 sube la apuesta.** Hoy una tablet perdida expone **lectura**; con F4.1 esa misma tablet puede **borrar y editar** datos de campo sin red y sincronizarlos hasta 16 h después. Una sesión que no se puede revocar deja de ser una fuga y pasa a ser un vector destructivo. **B1 debería ir antes que F4.1** |
| **A4** — self-heal de `aves_*_actual` al patrón aplicador | Abierto, medido (#20) | Un `GET` que escribe y toca `UpdatedAt` envenena cualquier cursor por `updated_at`, que es lo que necesita tanto el pull futuro como la reconciliación «¿mi operación llegó?» del `202 + batch_id` |
| **B8** — rotar las 4 llaves de `environment.prod.ts` | Abierto. **Las genera el usuario** | Están en texto plano y quemadas en git |
| **B10** — super admin a datos | ✅ **Cerrado en V23** (`56f7caa`) | — |
| **Sesiones multi-slot** | Abierto, plan hermano | No bloquea a F4, pero sí a «dos operarios turnándose la misma tablet» |
| **Despliegue** | 🔴 `main-produccion` a **25 commits** de `main` | **Nada de F1/F2/F3 corrió nunca en producción.** Construir F4 encima de un F3 no probado en campo es el riesgo programático más grande de este plan |

### 7.2 Riesgos técnicos

| Riesgo | Mitigación |
|---|---|
| 🔴 **El histórico unificado queda con un agujero.** El borrado offline es un llamador nuevo de un camino cuya anulación depende de C# dentro de `try/catch` que se tragan la excepción | §4.3: el trigger `AFTER DELETE`, con su caso de prueba 6.3.4 que lo verifica **con el C# neutralizado** |
| **El patch se convierte en un segundo escritor** y diverge de `UpdateAsync` | El patch **fusiona y delega** en el `UpdateAsync` de siempre (§2.8). Ninguna regla se reimplementa |
| **La política ambiente se filtra** a una petición normal | Scoped, default `Rechazar`, escrita en un solo lugar y restaurada en `finally`; test que verifica el default; el `SyncEjecucionContexto` no se registra como singleton |
| **Permitir saldo negativo resucita A1** | El descuento sin predicado sigue siendo **una sola sentencia SQL** ⇒ atómico. Lo que se pierde es la guarda de negocio, no la de concurrencia, y solo bajo política explícita |
| **Colapsar una op que el servidor ya aplicó** | La regla `intentos === 0` es conservadora por diseño: ante la duda, encadena. Y el colapso corre con la cola bloqueada |
| **El bump de IndexedDB a v3 pierde la cola** | La cola es dato que **no existe en ningún otro lado**. Test explícito v1→v3 y v2→v3, y `PASOS_MIGRACION` ya itera todos los pasos intermedios |
| **Change detection**: modal nuevo que se queda en «Cargando…» | `Eager` explícito en todo lo nuevo; y revisar el `OnPush` de `seguimiento-lote-levante-form.component.ts:27` contra el 202 |
| **Bundle**: initial 967 kB / techo 2,05 MB | Lo nuevo va en chunks de feature (`loadComponent`), no en el bundle inicial. La bandeja de cuadres es una ruta perezosa |
| **`requiere_cuadre` sin lector = escritura silenciosa** | El lector (`GET /api/Sync/cuadres` + bandeja) es parte de F4.0, no de una fase posterior. **Si no se construye el lector, no se enciende el emisor** |
| **`SqlQueryRaw` con nombres que tienen dígitos / columnas no snake_case** | Trampas conocidas del repo: si el listado de cuadres usa SQL crudo, mapear con columnas snake_case y sin dígitos en el nombre del tipo |

### 7.3 Deuda que este plan **descubre** y no arregla

- **`SyncPushCalculos.cs:42-45` dice algo falso** («el alta de levante no valida saldos»). Se corrige
  el comentario en F4.0, pero vale anotar el patrón: un doc-comment que era cierto en F3.1 y dejó de
  serlo dos días después.
- **`lote-produccion-list.component.ts:722-726`**: `delete(id)` pide confirmación con
  `ConfirmDialogService` y después **no hace nada** (`// TODO`). El borrado que funciona es otro camino
  (`confirmDelete()` → `eliminarSeguimiento`). Código muerto con apariencia de camino vivo.
- **`seguimiento-lote-levante-form.component.ts:163`** navega a `/lote-levante`, ruta que el módulo no
  registra (el módulo vive en `daily-log/seguimiento`).
- La entidad `LoteRegistroHistoricoUnificado` **no mapea** `silo_id`, `peso_neto`, `peso_tara_real`
  ni `promedio_peso_ave`, que sí existen en la tabla.
- `ResultadoPush.entidadId` viaja del servidor al cliente y se tira (#18) — F4.1 lo empieza a usar.

### 7.4 Lo que este plan NO hace

- No lleva offline **ningún** módulo nuevo: ni gastos, ni gestión de inventario, ni inventario de
  aves, ni movimientos, ni traslados, ni huevos, ni ventas. Construye lo que les falta y respeta el
  orden ya escrito en el mapeo.
- No resuelve B1, B8 ni A4.
- No hace pull / delta sync ni tombstones.
- No trata «lote cerrado» como clase (b) (§2.3) ni implementa la ventana de gracia de §5.6.
- No marca el cuadre a nivel de **entidad** (lote/stock): marca la **operación**.
- No porta reglas de negocio a TypeScript ni construye el corpus compartido C#↔TS.
- No despliega. El merge `main → main-produccion` sigue esperando decisión del usuario.
