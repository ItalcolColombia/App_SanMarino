# PWA — cierre de huecos (H1–H4)

**Fecha:** 2026-09-01 · **Estado:** plan, sin implementar
**Antecedentes:** [`pwa_f3_captura_offline_plan.md`](pwa_f3_captura_offline_plan.md) ·
[`pwa_f4_offline_edicion_plan.md`](pwa_f4_offline_edicion_plan.md) ·
[`pwa_f4_mapeo_modulos_pendientes.md`](pwa_f4_mapeo_modulos_pendientes.md) ·
[`pwa_sesiones_multislot_plan.md`](pwa_sesiones_multislot_plan.md)
**Operación:** [`../frontend/PWA.md`](../frontend/PWA.md)

> Los cuatro huecos que el usuario eligió cerrar, de una auditoría de 7 hecha contra el código del
> 1-sep-2026. **Todo lo que sigue está medido, no supuesto**; donde una medición contradice un plan
> anterior, se dice cuál y por qué (CLAUDE.md §Regla de schema: manda el código de hoy).

---

## 0. Lo que se midió (1-sep-2026)

| # | Hecho medido | Dónde |
|---|---|---|
| 1 | 🔴 **`GET /api/Sync/cuadres` y `POST /cuadres/{id}/resolver` existen y no los llama nadie.** `grep -rn "cuadres" frontend/src` → **0 resultados**: ni servicio, ni ruta, ni menú | `API/Controllers/SyncController.cs:58,68` |
| 2 | El cliente clasifica `requiere_cuadre` como **`'borrar'`** — y está bien: el día **sí** se guardó. El hueco no es del operario, es que **el supervisor nunca se entera** | `funciones/clasificar-resultado-push.funcion.ts:41` |
| 3 | El emisor sí existe desde el 22-ago (`6f17d44`): `StockInsuficienteException` en los 6 sitios reales de «no hay stock», atrapada en `SyncPushService` | `Application/Exceptions/StockInsuficienteException.cs` · `Services/Sync/Funciones/*.cs` |
| 4 | `verificar-cuadre-solo-en-sync.js` es un **gate de máquina**: falla el CI si algo fuera de `Services/Sync/` asigna ese estado | `.github/workflows/deploy-production.yml` |
| 5 | 🔴 **`backend/ecs-taskdef-new-aws.json:38` sigue en `JwtSettings__DurationInMinutes = 60`** y la TaskDef pisa el `appsettings` ⇒ la jornada de 16 h **no existe en campo** | `backend/ecs-taskdef-new-aws.json:38` |
| 6 | No se pudo verificar la TaskDef **viva**: las credenciales AWS de esta máquina responden `UnrecognizedClientException` | `aws ecs describe-services` |
| 7 | La fila capturada sin red sigue **invisible**: las 4 pantallas recargan de la caché de lectura, que no la tiene. Único rastro: toast + contador | los 4 componentes de lista |
| 8 | 🔴 **`InventarioGastoService.CreateAsync` SÍ mueve stock**: llama `_inventario.RegistrarConsumoAsync` por línea (`:568`), que es exactamente el camino que lanza `StockInsuficienteException` | `Services/InventarioGastoService.cs:455,568` |
| 9 | ⇒ **El mapeo de F4 está equivocado en su única fila de nivel 1.** Dice «No mueve stock: registra un gasto contra lote». Gastos es **nivel 2** | `pwa_f4_mapeo_modulos_pendientes.md` §Nivel 1 |
| 10 | `CreateAsync` abre **transacción incondicional** (`:527`) ⇒ reventaría dentro de la del push | `InventarioGastoService.cs:527` |
| 11 | La ventana de fecha de gastos vive en el **CONTROLLER**, no en el service (`ValidarVentanaFechaRegistro`) ⇒ una rama de sync que llame al service **la saltea** | `InventarioGastosController.cs:171` |
| 12 | Lista cacheable hoy: **89 endpoints · 55 cacheables · 34 excluidos · 0 sin decidir** | `scripts/verificar-lista-cacheable.js` |

---

## 1. Alcance

### Dentro

| | Hueco | Capa |
|---|---|---|
| **H1** | La bandeja de `requiere_cuadre` no tiene pantalla | front + migración de menú |
| **H2** | La jornada de 16 h no existe en producción (token de 60 min) | fuera del repo (TaskDef) |
| **H3** | La fila capturada sin red es invisible | front, 4 pantallas |
| **H4** | Gastos de inventario no se puede guardar sin red | back + front |

### Fuera, explícito

- **Editar/borrar offline y el grafo `client_entity_id`** — sigue siendo F4.2, no entra acá. H3 hace
  **ver** la fila pendiente; **no** la hace editable (§2.3).
- **Background Sync del service worker** — la cola sigue drenando con la app abierta (reconexión
  automática + botón «Enviar ahora»). Es el hueco 7 de la auditoría y no se eligió.
- **Los smokes C9–C13 en Android real** — requieren tablet y dos operarios; quedan en el tracker.
- **Niveles 2 restantes y nivel 3** (gestión de inventario, aves, movimientos, traslados, ventas).

---

## 2. Enfoque arquitectónico y trade-offs

### 2.1 H1 — la bandeja es una pantalla de LECTURA con un solo botón, y así tiene que quedarse

El backend ya decidió lo difícil y no se toca: `resolver` **sólo marca visto, no repone kilos**
(decisión del usuario en `6f17d44` — reponer sería una segunda fórmula para el mismo número, que es
exactamente el defecto que [[una sola fórmula por número]] prohíbe). La pantalla, entonces:

- **lista** lo que `GET /api/Sync/cuadres` devuelva (ya viene acotado a la empresa activa, fail-closed
  del lado del servidor — el front **no** filtra por su cuenta ni manda `companyId`),
- **muestra el `detalle`** —«qué ítem faltó, cuánto había, cuánto se pedía»—, que es el texto que
  convierte la fila en una acción posible,
- ofrece **un** botón: «Marcar como revisada» → `POST /cuadres/{id}/resolver`,
- y **dice en la propia pantalla** que resolver no repone stock: el ingreso se carga por el módulo de
  inventario, como siempre. Sin esa frase, el botón promete algo que no hace.

⚠️ **No se agrega ningún endpoint.** Si aparece la tentación de «reponer desde acá», se corta: es el
camino que el commit del emisor descartó a propósito.

### 2.2 H2 — subir el token es un cambio de producción, no de código

El repo **no** manda: la TaskDef viva pisa el `appsettings`. El archivo del repo
(`ecs-taskdef-new-aws.json:38`) se actualiza para que deje de mentir, pero **eso solo no cambia nada
en prod**. La acción real es sobre la TaskDef desplegada, la hace el usuario o se hace con
credenciales válidas, y va con verificación posterior (§6.2). Marcada `- [!]` en el tracker.

**Orden que no se puede invertir:** B1 (revocación de sesión, `c9a7349`) tiene que estar **desplegado
y verificado** antes de subir la vigencia. Una jornada de 16 h sin revocación real es una ventana de
acceso irrevocable en una tablet que se puede perder — es la condición D4 del plan madre, escrita
antes de que B1 existiera.

### 2.3 H3 — la fila pendiente se ve, y por diseño NO se puede exportar ni editar

Este es el punto donde F3 se frenó a propósito, y la razón sigue vigente: meter la fila capturada en
el array `seguimientos` la manda tres niveles abajo a componentes compartidos que **no pueden
distinguirla** de una guardada, y de ahí entra al Excel, a los indicadores y a la gráfica **como dato
real**. El servidor nunca la vio; un indicador calculado con ella es un número inventado.

La salida es fusionar **marcando**, y que la marca viaje con la fila:

```ts
// shared/offline/funciones/fusionar-pendientes.funcion.ts  (pura, sin `this`, sin DI)
fusionarPendientes<T>(filasDelServidor: T[], pendientes: OperacionPendiente[], opts): (T & { __pendiente?: true })[]
```

Reglas duras:

1. **La fusión se hace al cargar/refrescar y se guarda en un campo**, nunca en un getter del template
   (un getter que devuelve un array nuevo por ciclo rompe change detection — CLAUDE.md).
2. **El Excel y los indicadores filtran `__pendiente`.** No es opcional: es la razón por la que esto
   no se hizo en F3. Cada pantalla que exporta agrega el filtro **en la misma tanda**, con test.
3. La fila pendiente se pinta **deshabilitada para editar y borrar**: hoy no hay operación de edición
   offline (F4.2), así que un botón que abre un modal sobre una fila que el servidor no tiene sólo
   puede terminar en un PUT contra un id inexistente.
4. Sólo se fusiona **lo de la partición activa** — misma `identidadActual()` que usa el push. Lo ajeno
   sigue en la cola, intacto y sin verse (R9 del plan multi-slot).
5. La fila se ordena **por la fecha capturada**, junto a las del servidor, no en un bloque aparte: el
   operario busca el día, no busca «lo pendiente».

**Trade-off aceptado:** una fila pendiente y su equivalente del servidor pueden convivir un instante
(el push confirma entre el `GET` y el render). Se resuelve deduplicando por la clave natural del día
(`loteId` + `fecha`) y prefiriendo **la del servidor**. Se prefiere un parpadeo a un duplicado.

### 2.4 H4 — gastos es nivel 2, y lo destraba el emisor que ya existe

El mapeo lo puso en nivel 1 por «no mueve stock». **Se midió y sí lo mueve** (`RegistrarConsumoAsync`
por línea). Eso lo cambia todo: sin tratamiento, una captura offline de un gasto cuyo ítem ya no está
en el silo vuelve como `regla_de_negocio` ⇒ **bandeja** ⇒ el dato de campo queda varado. Es el
defecto vivo #7 del plan de F4, aplicado a un módulo nuevo.

La buena noticia es que la pieza que faltaba **se construyó el 22-ago**. El patrón de F7 se copia tal
cual, con una diferencia que hay que nombrar:

> En un seguimiento, el reintento guarda **el día sin los ítems de inventario**: el hecho de campo
> (mortalidad, huevos, peso) existe con independencia del alimento. **En un gasto no hay tal
> separación**: el gasto *es* el consumo.

⇒ La regla para gastos: **se registra el gasto y sus líneas, sin descontar stock**, con
`requiere_cuadre` y el `detalle` de qué faltó. Es coherente con lo que ya se decidió: el consumo
físico **ocurrió** en la granja; lo que está atrasado es el número del sistema. Perder el registro
sería peor que un saldo temporalmente desalineado, y la bandeja de H1 es justamente donde eso se ve.

**Lo que NO se hace:** descontar «hasta donde alcance». Un consumo parcial inventa un número que nadie
capturó.

### 2.5 La ventana de fecha: por qué la rama de sync la saltea, y por qué está bien

`ValidarVentanaFechaRegistro` vive en el controller a propósito (memoria
`ventana-fecha-inventario-va-en-el-controller`): el service lo comparten caminos que fechan histórico
adrede. La rama de sync llama **al service**, así que no pasa por esa guarda.

Es correcto y se deja así: la captura offline **es legítimamente retroactiva** —esa es toda su razón
de ser— y su antigüedad ya está acotada por dos cosas más duras que la ventana: la jornada de 16 h y
el `capturadoAtDispositivo` que viaja en la operación. Someterla a la ventana del formulario haría
que una captura del turno de la noche se rechace por la mañana.

**Se registra como decisión explícita** para que nadie la «arregle» después: queda en el doc-comment
de la rama de despacho.

---

## 3. Archivos a crear / modificar (rutas verificadas)

### 3.1 H1 — bandeja de cuadres (front)

**Nuevos**
- `frontend/src/app/features/cuadres-offline/services/cuadres-offline.service.ts` — `listar()` y `resolver(id)`.
- `frontend/src/app/features/cuadres-offline/models/cuadre-pendiente.model.ts` — espejo de `CuadrePendienteDto` (id, tipo, entidadId, detalle, deviceId, recibidoAt).
- `frontend/src/app/features/cuadres-offline/funciones/etiquetar-tipo-cuadre.funcion.ts` (+ spec) — `seguimiento_levante_crear` → «Seguimiento levante». Pura; sin ella el operario lee el identificador del contrato.
- `frontend/src/app/features/cuadres-offline/pages/cuadres-offline-page/` — componente + html + scss.

**Modificados**
- `frontend/src/app/app.config.ts` — ruta `cuadres-offline` con `canActivate: [authGuard]`, patrón de `inventario-gastos` (`:623`).
- `frontend/src/app/shared/offline/funciones/decidir-cacheable.funcion.ts` — **`sync/cuadres` va a EXCLUIDOS**, con su motivo: es una bandeja de supervisión que se mira con red; servirla vieja mostraría como pendiente algo ya resuelto. (Sin la entrada, `verificar-lista-cacheable.js` corta el CI — es el gate que ya falló dos veces.)

**Primitivas obligatorias** (CLAUDE.md §Sistema de diseño): `ToastService`, `ConfirmDialogService`
(`await this.confirmDialog.ask(...)` antes de resolver), `changeDetection: ChangeDetectionStrategy.Eager`
explícito, tokens de color del tema (nada hardcodeado).

### 3.2 H1 — migración de menú

- `backend/src/ZooSanMarino.Infrastructure/Migrations/<ts>_SeedMenuCuadresOffline.cs` — data-only,
  Designer clonado, sin tocar ModelSnapshot. Localiza **por `route`**, `INSERT … WHERE NOT EXISTS`,
  icono **dentro del `ICON_MAP` de `menu.service.ts`** (verificar antes de elegirlo: un nombre fuera
  del mapa dibuja el ítem sin icono).

### 3.3 H3 — fila pendiente

**Nuevos**
- `frontend/src/app/shared/offline/funciones/fusionar-pendientes.funcion.ts` (+ spec).

**Modificados** (los 4, con su filtro de exportación)
- `features/lote-levante/pages/seguimiento-lote-levante-list/…`
- `features/lote-produccion/pages/lote-produccion-list/…`
- `features/aves-engorde/pages/seguimiento-aves-engorde-list/…`
- `features/seguimiento-diario-lote-reproductora/pages/seguimiento-diario-lote-reproductora-list/…`

### 3.4 H4 — gastos offline

**Backend**
- `Application/Calculos/SyncPushCalculos.cs` — tipo `gasto_inventario_crear` en `Tipos` + `Tipos.Todos`.
- `Infrastructure/Services/Sync/Funciones/SyncPushService.Gastos.cs` — **nuevo partial**, namespace
  plano `ZooSanMarino.Infrastructure.Services`, rama de despacho que llama `IInventarioGastoService.CreateAsync`.
- `Infrastructure/Services/Sync/SyncPushService.cs` — entrada en `DespacharAsync`.
- `Infrastructure/Services/InventarioGastoService.cs:527` — **transacción condicional**
  (`CurrentTransaction is null ? Begin() : null`) y su `Commit` condicionado. `DeleteAsync` (`:635`)
  se deja como está: no entra al push.

**Frontend**
- `shared/offline/funciones/decidir-encolable.funcion.ts` — `POST /api/inventario-gastos/?$` → `gasto_inventario_crear`.
- `features/gastos-inventario/pages/gastos-inventario-page/…` — toast con `esRespuestaPendiente`.

---

## 4. Cambios de BD / SQL / migraciones

| Migración | Qué hace | Idempotencia |
|---|---|---|
| `<ts>_SeedMenuCuadresOffline` | Ítem de menú «Cuadres sin conexión» → `/cuadres-offline`; habilitado en `company_menus` para las empresas que ya tienen la PWA en uso, y asignado a los roles de supervisión | `INSERT … WHERE NOT EXISTS`, localizando por `route` y por nombre de rol/empresa, **nunca por id** |

**No hay DDL.** `sync_operaciones` ya tiene `detalle`, `cuadre_resuelto_at` y `cuadre_resuelto_por`
(migración `20260822224615`), y el índice parcial de la bandeja. H4 no agrega columnas: reusa
`inventario_gastos` tal cual.

⚠️ Ningún `fn_*.sql` / `vw_*.sql` nuevo ⇒ el gate `verificar-sql-llega-por-migracion.js` no aplica.

---

## 5. Reglas de negocio

1. **La bandeja es de la empresa activa y de nadie más.** El front no manda `companyId`; el servidor
   ya resuelve fail-closed. Si el back devolviera vacío, la pantalla dice «no hay cuadres
   pendientes», nunca «error».
2. **Resolver no repone kilos.** La pantalla lo dice con esas palabras.
3. **Resolver es idempotente hacia el usuario:** el `404` de una fila ya resuelta (o de otra empresa)
   se traduce a «esa fila ya no está pendiente» + refresco, no a un toast de error rojo.
4. **Una fila pendiente nunca entra a un Excel, a un indicador ni a una gráfica.**
5. **Una fila pendiente no se edita ni se borra** desde la tabla (no existe la operación offline).
6. **Un gasto sin stock se registra sin descontar**, con `requiere_cuadre` y detalle. Nunca parcial.
7. **La rama de sync de gastos no aplica la ventana de fecha del formulario**, a propósito (§2.5).
8. **Todo `requiere_cuadre` sigue naciendo sólo en `Services/Sync/`** — lo hace cumplir un gate de
   máquina; H4 respeta el mismo camino.

---

## 6. Casos de prueba

### 6.1 Karma — funciones puras (co-locadas)

- `fusionar-pendientes.funcion.spec.ts`: fila pendiente marcada · orden por fecha ·
  **dedupe prefiriendo la del servidor** · sin operaciones ⇒ el array del servidor **por referencia**
  (no una copia nueva: cambiar la referencia sin motivo repinta la tabla entera) · operaciones de
  **otra partición ignoradas** · outbox vacío / lista vacía / ambas vacías.
- `etiquetar-tipo-cuadre.funcion.spec.ts`: los 4 tipos conocidos + uno desconocido ⇒ se muestra el
  identificador crudo, no `undefined`.
- `decidir-encolable.funcion.spec.ts`: `POST /api/inventario-gastos` ⇒ `gasto_inventario_crear`;
  `POST /api/inventario-gastos/123/algo` ⇒ `null`; `GET` ⇒ `null`.
- **La prueba que prueba la prueba** (receta de D6): con el filtro de `__pendiente` desactivado, el
  test de exportación **tiene que fallar**. Un test que no falla cuando se rompe lo que dice proteger
  no prueba nada.

### 6.2 xUnit — backend

- `SyncPushCalculos`: `gasto_inventario_crear` reconocido en `Tipos.Todos`; un tipo desconocido sigue
  devolviendo `contrato_obsoleto`.
- Rama de gastos: stock suficiente ⇒ `aplicada` **y stock descontado**; stock insuficiente ⇒
  `requiere_cuadre`, gasto **creado**, stock **sin cambio**, `detalle` no vacío.
- Idempotencia: el mismo `clientOpId` dos veces ⇒ una sola fila de `inventario_gastos` y `replay`.
- Transacción condicional: la rama corre dentro de la transacción del push sin lanzar.

### 6.3 Verificación de H2 (post-cambio en prod)

```bash
# 1) ¿Qué TaskDef corre realmente?
aws ecs describe-services --cluster devSanmarinoZoo --services sanmarino-back-task-service-75khncfa \
  --region us-east-2 --query 'services[0].{TaskDef:taskDefinition,Running:runningCount}'
# 2) ¿Con qué vigencia?
aws ecs describe-task-definition --task-definition <arn> --region us-east-2 \
  --query "taskDefinition.containerDefinitions[0].environment[?name=='JwtSettings__DurationInMinutes']"
```
Y en el navegador: decodificar el `exp` del JWT recién emitido ⇒ **960 minutos**, no 60. Sin ese
segundo paso el cambio no está verificado (ECS hace rollback silencioso).

### 6.4 Smoke doble por empresa

Toda pantalla nueva se prueba en una empresa **con** capturas offline y en una **sin** ninguna
(Demo/Sanmarino ⇒ bandeja vacía, cero cambios visibles en el resto).

### 6.5 Validación de build

`cd frontend && yarn build` (0 errores; único warning aceptado: el budget preexistente — ⚠️ el techo
de error está en **2.05 MB** y el bundle está al borde: si H1 lo empuja, el servicio se carga con
`import()` diferido, como ya se hizo con `sync.service`) · `cd backend && dotnet build && dotnet test`
· `node frontend/scripts/verificar-lista-cacheable.js` · `verificar-change-detection.js` ·
`verificar-cuadre-solo-en-sync.js`.

---

## 7. Riesgos y lo que este plan NO hace

### Riesgos

| Riesgo | Mitigación |
|---|---|
| El bundle del front pasa el techo de 2.05 MB | La bandeja es lazy (`loadComponent`) y no entra al bundle inicial |
| El ítem de menú no aparece en prod (ids distintos local↔prod) | Localizar **siempre por `route`**; verificar en pantalla tras el deploy |
| Fusionar pendientes rompe change detection en una tabla grande | Fusión en un campo, nunca en un getter; `Eager` explícito |
| La rama de gastos deja stock sin descontar sin que nadie lo vea | Es exactamente lo que H1 hace visible. **H1 se entrega antes que H4** |
| H2 sube la vigencia antes de que B1 esté verificado en prod | Orden explícito en §2.2; el tracker lo marca `- [!]` |

### Lo que este plan NO hace

- No construye editar/borrar offline ni el grafo `client_entity_id` (F4.2).
- No agrega Background Sync.
- No corre los smokes C9–C13 (necesitan Android real).
- No repone stock desde la bandeja, ni acá ni nunca por ese camino.
- No toca la app móvil Flutter: postea directo a cada endpoint, no pasa por `Sync/push`.

---

## 8. Orden de entrega

**H1 → H3 → H4**, y **H2 en paralelo** (es del usuario, no del repo).

H1 primero porque hace **visible** el estado que H4 va a empezar a producir: entregar H4 antes sería
sumar filas a una bandeja que nadie puede abrir.
