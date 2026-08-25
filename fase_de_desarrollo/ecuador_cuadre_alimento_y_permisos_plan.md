# Ecuador — Cuadre de alimento que no cierra + 3 permisos (reportes de Lady Malave)

> **Origen:** cuatro reportes de **LADY SOLANGE MALAVE RAMIREZ** (`ladymalave@ecuitalcol.com`,
> `user_id a2a9a9ce-173b-402d-b234-accf719264eb`, rol **«Ecuador Administrador»** `role_id=10`,
> empresa **ItalcolEcuador `company_id=3`**), 25-ago-2026.
>
> **Base de medición:** copia de **producción** restaurada en la BD local `sanmarinoapplocal:5433`
> el 25-ago-2026. Todo número de este plan está medido sobre esa copia, no estimado.

---

## 0. Los cuatro pedidos, tal como llegaron

| # | Pedido | Estado del diagnóstico |
|---|---|---|
| **1** | En Gestión de Alimento, la pestaña **Cuadre de alimento** sigue mostrando descuadre en **Sacachún 3A / núcleo 685062 / galpón G0044 / lote 2603** aunque el usuario ya borró los registros sobrantes. Hay que **arreglarlo de raíz** y agregar **«editar saldo» desde la pestaña, que se aplique en cadena en todo lado**. | ✅ **Causa raíz encontrada y medida.** Ver §1. |
| **2** | Lady Malave no tiene el permiso para **editar las aves de un lote y que cuadre en cadena**; asignárselo y validar que editar el lote afecte los seguimientos diarios. | ⚠️ **Con una pregunta abierta.** Ver §2. |
| **3** | Poner permisos a las opciones de **Gestión de Usuarios**: con el permiso `gestion_usuarios` se puede crear/editar/eliminar; sin él, solo ver el listado y el detalle. | ✅ Mapeado. Ver §3. |
| **4** | Darle a Lady Malave el permiso de **registrar con fecha retroactiva** (hoy bloqueado a un rango). Hacerlo por migración. | ✅ Mapeado, es el más simple. Ver §4. |

---

## 1. Cuadre de alimento — por qué borrar los registros no lo arregló

### 1.1 Lo que se midió

`fn_cuadre_alimento_engorde(3)` sobre la copia de producción devuelve **un solo galpón descuadrado en
todo ItalcolEcuador**, y es exactamente el que reportó la usuaria:

```
granja        nucleo   galpon  lote  lote_nombre  ultimo_seg   saldo_tabla  mov_post   stock    descuadre  filas_neg
Sacachun 3A   685062   G0044   207   2603         2026-08-24      7.720,00      0,00  12.720,00  -5.000,00      0
```

- **Saldo de la tabla diaria: 7.720 kg** — y es el correcto: los ingresos vivos del ciclo
  (6.000 del 11-ago ítem 4 + 5.000 del 12-ago ítem 5 + 10.000 del 21-ago ítem 5 = 21.000) menos los
  consumos vivos (13.280) dan exactamente 7.720.
- **Stock del galpón: 12.720 kg** — 5.000 kg de más.
- `filas_negativas = 0`: no hay días en rojo. **Es un problema de kilos, no de fechas.**

El desglose por ítem del galpón cierra la atribución al kilo:

| ítem | Σ movimientos | `inventario_gestion_stock` | diferencia |
|---|---|---|---|
| 3 | 0,000 | 0,000 | 0,000 |
| 4 | 0,000 | 0,000 | 0,000 |
| **5** | **7.720,000** | **12.720,000** | **+5.000,000** |

### 1.2 La causa raíz: **eliminar un ingreso NO devuelve el stock**

La usuaria cargó dos veces la remisión **63705** (5.000 kg) y borró la repetida. Se ve en el
histórico:

```
15930  INV_INGRESO  2026-08-12  5.000  ítem 5  ref 63705  anulado=f   (mov 12073, vivo)
17292  INV_INGRESO  2026-08-12  5.000  ítem 5  ref 63705  anulado=t   (mov 12983, borrado el 19-ago)
```

El movimiento `12983` **ya no existe** en `inventario_gestion_movimiento` y su fila del histórico
quedó `anulado = true` — o sea, **la tabla diaria dejó de contarlo, que es lo correcto**. Pero
`inventario_gestion_stock` conservó los 5.000 kg. El invariante
`saldo == stock − movimientos posteriores` se rompe y **no se puede volver a cerrar desde la
pantalla**: nada de lo que haga la usuaria toca ese stock.

El defecto está escrito, con todas las letras, en el código:

- **[`InventarioGestionService.Ingreso.cs:747-752`](backend/src/ZooSanMarino.Infrastructure/Services/InventarioGestion/Funciones/InventarioGestionService.Ingreso.cs:747)**
  — `EliminarIngresoAsync`: *«**No modifica stock.** Marca `anulado=true` … y elimina físicamente el
  registro»*. Y el cuerpo, efectivamente, no toca stock.
- **[`InventarioGestionController.cs:515-518`](backend/src/ZooSanMarino.API/Controllers/InventarioGestionController.cs:515)**
  — el mismo endpoint documenta lo contrario: *«Elimina un ingreso …: **revierte stock** y marca
  `anulado=true`»*.

**El contrato publicado y el comportamiento real se contradicen.** Quien lea el controller cree que
el stock vuelve; no vuelve.

**El mismo defecto, por duplicado**, en
**[`InventarioGestionService.Traslado.cs:961-966`](backend/src/ZooSanMarino.Infrastructure/Services/InventarioGestion/Funciones/InventarioGestionService.Traslado.cs:961)**
— `EliminarTrasladoAsync`: *«No modifica stock»*. Borrar un traslado deja el **origen corto** y el
**destino largo**, los dos permanentes.

Y existe la prueba de que la forma correcta ya está escrita en el mismo servicio:
**`AnularMovimientoHistoricoAsync`** (`DELETE /movimientos/{id}`,
[`StockMutacion.cs:171-207`](backend/src/ZooSanMarino.Infrastructure/Services/InventarioGestion/Funciones/InventarioGestionService.StockMutacion.cs:171))
**sí revierte el stock**, dentro de una transacción, y **rechaza la anulación si no hay stock
suficiente** para revertirla. Son dos caminos para la misma operación con comportamientos opuestos:
el de «Ingresos» rompe el cuadre, el del «Histórico» no.

> Esto explica también por qué el ítem 4 del mismo galpón **sí** cerró en 0: su ingreso duplicado
> (mov `11453`, 5.000 kg) se eliminó por el camino que sí revierte.

### 1.3 Lo que hay que construir

#### **F1 — Cerrar la fuga (la causa raíz)**

1. `EliminarIngresoAsync` pasa a **revertir el stock** con el mismo patrón probado de
   `AnularMovimientoHistoricoAsync`: transacción + `DescontarStockAtomicoAsync` + rechazo explícito
   cuando el stock ya se consumió (`«No se puede eliminar este ingreso: los kilos ya se consumieron;
   corrija el consumo primero»`).
2. `EliminarTrasladoAsync` revierte **los dos extremos** del grupo (devuelve al origen, descuenta del
   destino), también en una sola transacción y con el mismo rechazo.
3. Se corrige el doc-comment del controller para que diga lo que el código hace.
4. **Cálculo puro nuevo** en `Application/Calculos/ReversionMovimientoInventarioCalculos.cs`: dado el
   movimiento y el stock actual, qué delta aplica a cada ubicación y si la reversión es posible.
   Tests xUnit obligatorios (gate de CI).

#### **F2 — «Editar saldo» desde la pestaña de Cuadre, en cadena**

El pedido literal es *«que tenga algo que sea editar saldo desde este tap y se realice en cadena en
todo lado»*. **El saldo de la tabla diaria no es un campo: es un derivado.** No se puede escribir; se
corrige el insumo que está mal. Y hay dos lados que pueden estar mal, con arreglos opuestos:

| Situación | Quién tiene razón | Qué hay que escribir |
|---|---|---|
| G0044: stock 12.720 vs tabla 7.720 | **La tabla** (el ingreso se borró bien) | Bajar el **stock** 5.000 kg |
| Panamá G0475: tabla 21.216 vs stock 2.566 | **El stock** (alguien ya lo corrigió a mano) | Bajar la **tabla** 18.650 kg |

Por eso la acción es **«Cuadrar galpón»**, no «editar saldo a secas»: un modal que muestra los dos
números, pide **los kilos reales que hay en el galpón** y un **motivo obligatorio**, muestra la
**previsualización exacta de lo que va a escribir de cada lado**, y al confirmar deja
`descuadre_kg = 0` por construcción.

Del lado del stock ya existe el primitivo correcto (`AjusteStock` / `EliminacionStock`, que **no**
llegan a la tabla diaria — se espejan como `INV_OTRO`, que la fn no lee).
Del lado de la tabla **falta** un primitivo, y se agrega uno nuevo y aislado:

- Dos `movement_type` nuevos, **`AjusteCuadreTablaEntrada`** y **`AjusteCuadreTablaSalida`** →
  `tipo_evento` **`INV_AJUSTE_CUADRE_ENTRADA`** / **`_SALIDA`**. **No tocan stock**, a propósito.
- `fn_seguimiento_diario_engorde` (**v17**) aprende a leerlos en sus **5 CTE**
  (`apert_mov`, `hist_full`, `hist_alimento`, `docs_por_fecha`, `fechas_universo`) y en los `CASE`
  de signo; `fn_cuadre_alimento_engorde` los cuenta en `mov_post`.

> **Por qué DOS tipos y no uno con la cantidad firmada:** `AjusteStock` guarda `Math.Abs(delta)` y
> por eso **perdió el signo para siempre** — es la razón por la que hoy no se puede revertir
> automáticamente. Poniendo el signo en el tipo (como ya hacen `TrasladoEntrada`/`TrasladoSalida`)
> no hay nada que perder, y ningún otro lector del inventario tiene que aprender a leer cantidades
> negativas.

> 🔴 **Por qué así y no «que la fn lea `INV_OTRO`»** (el arreglo de fondo que dejó anotado el
> diagnóstico V8): hacer que la fn lea `INV_OTRO` **mueve el saldo de todas las empresas de golpe**
> —Ecuador tiene 5 galpones con 41.210 kg de ajustes dentro del ciclo activo **y cuadra en los 36**—
> y ya hizo naufragar dos intentos (v15 y v16, los dos revertidos por el gate). Con `tipo_evento`
> **nuevos**, en cambio, el gate de paridad multipaís da **diferencia cero por construcción**: no hay
> una sola fila histórica que los lleve. Y así salió, medido: **0 en las 7 columnas del diff, en las
> dos empresas, sobre 6.851 filas.** El arreglo de fondo queda nombrado como deuda, en su propio
> plan, no colgado de esta entrega.

> **La fecha del ajuste de tabla la pone el sistema, no el usuario:** es la del último seguimiento
> del ciclo. Un movimiento fechado *después* de `seg_max` es, por definición del cuadre, un
> «movimiento posterior» — se restaría del esperado y el galpón quedaría igual de descuadrado. Por
> eso tampoco pasa por la ventana de fechas retroactivas: esa guarda existe para las fechas que
> **tipea una persona**.

#### **F3 — Reparar G0044 sin tocar la BD a mano**

Con F2 en pie, **la propia usuaria cierra su caso desde la pantalla**: «Cuadrar galpón» → kilos
reales `7.720` → motivo «remisión 63705 duplicada, eliminada el 19-ago» → el sistema escribe un
`AjusteStock` de −5.000 kg y **nada** del lado de la tabla (que ya estaba bien). Queda auditado, con
autor y motivo. **No hace falta un `UPDATE` a producción.**

#### **F4 — Que el descuadre no vuelva a pasar callado**

- La pestaña muestra hoy el número pero **no dice qué hacer**. Se agrega, por fila, la **causa
  probable atribuida** (ajuste manual dentro del ciclo / ingreso eliminado sin reversión / días en
  rojo) reusando `CuadreAlimentoEngordeCalculos`, y el botón de cuadrar.
- Se separan visualmente las **dos señales que hoy se mezclan**: `descuadre_kg` (kilos) y
  `filas_negativas` (días que cerraron en rojo). Son problemas distintos y la receta vieja que los
  sumaba daba 23 galpones donde había 8.

### 1.4 Estado del resto del parque (para no vender un arreglo parcial)

| Empresa | Galpones | Con descuadre en kg | Con días en rojo | Kg absolutos |
|---|---|---|---|---|
| **ItalcolEcuador** | 37 | **1** | 0 | **5.000,0** |
| **ItalcolPanama** | 31 | **12** | 16 | **55.866,5** |

Ecuador queda en **0 descuadrados** al cerrar G0044. **Panamá no entra en esta entrega**: sus 12
casos son de otra naturaleza (ajustes manuales ya aplicados, cargas históricas re-fechadas, consumo
sin seguimiento detrás) y varios requieren decisión de operación, no software. Con F2 desplegado, la
herramienta para cerrarlos existe; el trabajo de cerrarlos se planifica aparte.

---

## 2. Permiso para «editar las aves de un lote y que cuadre en cadena»

### 2.1 El desarrollo existe y está identificado

Es el commit **`a9fd721`** (21-ago-2026), *«feat(lotes): corregir las aves de un lote que ya tiene
seguimiento (engorde + postura)»*: `AjusteEncasetamientoCalculos` (puro, 24 tests), el inicial se
**reemplaza** y el saldo vivo se corre por el **delta**, propagando a `aves_encasetadas`, al registro
`Inicio` del historial, al maestro, y en postura a `lote_etapa_levante` y `lote_postura_produccion`.

### 2.2 🔴 Lo que se midió y contradice el supuesto del pedido

**Ese desarrollo no tiene un permiso propio.** Los gates que existen sobre el formulario de lote de
engorde son
[`lote-engorde-list.component.html:261`](frontend/src/app/features/lote-engorde/components/lote-engorde-list/lote-engorde-list.component.html:261)
(`editar_registro`) y `:266` (`eliminar_registro`). El módulo de lotes de **postura** no tiene **ningún**
`appHasPermission`. Y el rol «Ecuador Administrador» de Lady **ya tiene `editar_registro`**
(y también `eliminar_registro`, `abrir_lote`, `liquidar_lote` y
`cuadrar_ingresos_traslados_seguimiento`).

Cruzando **todas** las keys de permiso usadas en gates del front contra sus 18 permisos efectivos, lo
único del alcance que le falta es `liquidacion.aplicar_correccion` — que es de liquidación, no de
aves.

**Conclusión: no hay un permiso que asignarle para esto.** El pedido del usuario es que ella lo
tenga; como la llave no existe, **se crea**: `lote.corregir_aves`.

### 2.3 🔴 Y hay algo peor que la llave faltante: **el gate de hoy es cosmético**

`LoteAveEngordeController` (`PUT {loteAveEngordeId}`, `:101`) y `LoteController` (`PUT`, `:107`)
**no tienen un solo `[Authorize(Policy=…)]` ni chequeo de permiso**. Solo aplica el
`FallbackPolicy = RequireAuthenticatedUser` de [`Program.cs:531`](backend/src/ZooSanMarino.API/Program.cs:531).
Es decir: **cualquier usuario autenticado con acceso a la granja puede hacer el `PUT` con curl y el
ajuste de encasetamiento se aplica igual, sin `editar_registro`.** El `*appHasPermission` del front
solo esconde el botón.

Por eso el permiso nuevo se enforcea **en el backend**, con el patrón canónico del repo
(`_current.Permissions.Contains(key)` → `Forbid()`, como
[`ValidacionSeguimientoService.Validar.cs:35`](backend/src/ZooSanMarino.Infrastructure/Services/ValidacionSeguimiento/Funciones/ValidacionSeguimientoService.Validar.cs:35)
y `VacunacionPlantillaController.cs:39`), y **además** en el front.

### 2.4 Lo que se construye

1. **Permiso `lote.corregir_aves`** por migración (`permissions` + `company_permissions`
   `CROSS JOIN companies` + `role_permissions`). Se otorga **heredando de `editar_registro`**
   (patrón `20260817200000`: `role_permissions` de quien ya tiene la key origen) para que **nadie
   gane ni pierda acceso el día del deploy** — hoy quien puede editar el lote seguirá pudiendo.
   Eso ya le llega a «Ecuador Administrador», que tiene `editar_registro`.
2. **Backend:** el chequeo va en `LoteAveEngordeService` / `LoteService` **solo cuando el delta de
   aves es distinto de cero** (`AjusteEncasetamientoCalculos.SinCambio(delta)`), no en todo el `PUT`:
   editar el técnico o la regional no es corregir aves y no debe pedir el permiso nuevo.
3. **Front:** los campos de aves del form de **engorde**
   (`lote-engorde-list.component.html:537-551`) y de **postura**
   (`lote-list.component.html:486` — hoy **sin ningún gate**) quedan deshabilitados sin el permiso.
   ⚠️ Postura hoy está completamente abierto: agregar el gate ahí **sí** puede quitarle a alguien
   algo que hoy hace, por eso la herencia desde `editar_registro` es obligatoria.

### 2.5 Tres cosas que hacen que «el permiso no funcione» sin que el permiso tenga la culpa

Van escritas porque los tres síntomas se ven idénticos y llevan a diagnosticar la tabla equivocada:

1. **No se ve hasta re-login.** Los permisos viajan dentro de la sesión cifrada
   (`UserPermissionService` lee `session$ → user.permisos`), no se consultan por acción. Después de
   la migración la usuaria **tiene que cerrar sesión y volver a entrar** (o cambiar de empresa).
2. **Si el lote está liquidado, ningún permiso de edición alcanza.**
   `LiquidacionCongeladaGateCalculos.ValidarEscritura(..., EditarLote)` (`LoteAveEngordeService.cs:619`)
   rechaza antes de todo; hay que reabrirlo, y reabrir pide `abrir_lote`.
3. **El alcance de granja puede comerse la acción en silencio.** `UpdateAsync` hace
   `if (allowed.Count == 0) return null` → **404**, con el permiso puesto. El filtro real es la
   tabla **`user_farms`** (vía `FarmService.cs:365-388`), **no** `user_farm_scopes` — mirar la tabla
   equivocada cuesta la tarde.

### 2.6 Validación pedida

Smoke en local, sobre un lote de Ecuador con seguimiento cargado: editar las aves en Gestión de Lotes
⇒ la serie diaria de `fn_seguimiento_diario_engorde` se corre por el delta **conservando las bajas**,
y `fn_cuadre_aves_engorde` sigue dando `cuadra = true` (su invariante es
`aves_encasetadas == Inicio.total`). Es exactamente el smoke del commit `a9fd721`, repetido con un
lote de ella.

---

## 3. Permiso `usuarios.gestionar` para las opciones de Gestión de Usuarios

> **Nombre:** el pedido lo llama «gestión usuario». La convención del repo es `modulo.accion`
> (documentada en `20260714112951:14`), así que la key es **`usuarios.gestionar`**.
> ⛔ **No se reusa la key legacy `manage_users`** (`PermissionSeed.cs:14`): no la consulta nadie, no
> respeta la convención, y arrastra el link de `menu_permissions` de `MenuSeed.cs:34`.

### 3.1 Lo que hay hoy: **18 acciones, ninguna gateada** (no 6)

- Front — [`user-management/`](frontend/src/app/features/config/user-management): el botón **Crear**
  vive en el **componente padre**
  ([`user-management.component.html:11`](frontend/src/app/features/config/user-management/user-management.component.html:11))
  y **cinco botones por fila** en el hijo
  ([`tabla-lista-registro.component.html`](frontend/src/app/features/config/user-management/pages/tabla-lista-registro/tabla-lista-registro.component.html)):
  asignar granjas `:129`, editar `:137`, reset password `:145`, sesiones `:153`, eliminar `:161` —
  **duplicados** en la vista de tarjetas móvil (`:220`, `:229`, `:238`, `:247`, `:256`).
  Y hay **12 acciones más** dentro de los modales: guardar el modal, empresas y roles del usuario,
  ejecutar el reset, revocar una sesión / todas, asignar y quitar granja, **toggle «administrador de
  granja»** (escalada de privilegios), toggle «granja por defecto», abrir y **guardar el alcance
  granular** (define qué datos ve el usuario).
- Back — [`UsersController.cs:12`](backend/src/ZooSanMarino.API/Controllers/UsersController.cs:12):
  un `[Authorize]` de clase y **ningún** chequeo de permiso en los 15 endpoints.
  [`UserFarmController.cs:11-13`](backend/src/ZooSanMarino.API/Controllers/UserFarmController.cs:11):
  el `[Authorize]` está **comentado**; sus 17 endpoints los salva únicamente la `FallbackPolicy`.

**Dos correcciones que la verificación adversarial trajo y que cambian el diseño:**

- 🟢 **El alcance granular YA está gateado del lado del servidor**:
  `UserFarmScopeService.EnsureCallerPuedeAdministrarAlcanceAsync` (`:33-57`) exige Super Admin o un
  rol admin **de la empresa de la granja**, fail-closed, y el controller lo traduce a 403.
  **No hay que volver a gatearlo** — sí hay que ocultar el botón para no ofrecer un 403.
- 🔴 **`menu_permissions` SÍ gatea, y del lado del servidor** (`MenuService.cs:54-55` y `:217-218`,
  `RoleCompositeService.cs:685-686` filtran por `RequiredKeys.Intersect(userPermKeys)`).
  **Medido en la copia de producción: tiene 17 filas** — todas de tickets / ItalJira / gerencia — y
  **ninguna** del menú «Usuarios» (id 13, route `/config/users`), que es justamente por lo que el
  módulo se ve. ⛔ **La migración NO debe insertar una fila ahí**: si lo hace, el menú «Usuarios»
  **desaparece** para todos los que no tengan la key nueva — lo contrario de lo pedido.

### 3.2 Lo que se construye

1. **Permiso `usuarios.gestionar`** por migración data-only idempotente:
   `permissions` + `company_permissions` **`CROSS JOIN companies`** (fail-closed: sin esa fila el
   permiso no viaja en el JWT ni es asignable) + `role_permissions`. **Sin tocar `menu_permissions`.**
2. **🔴 Anti-lockout — el punto más delicado de toda la entrega.** Este permiso **invierte el
   default**: hoy *todos* crean/editan/borran. Sembrarlo solo a `role_id = 1` deja sin poder crear un
   usuario a quien administra usuarios en cada país el día del deploy. Se otorga **heredando de
   `role_menus`** por **route `/config/users`** (patrón `20260815010000:54-68`, localizando por route
   y jamás por id): todo rol que hoy ve el módulo lo conserva. Verificado por el caso de prueba 13.
3. **Backend — el chequeo va en el controller, NO en la policy `CanManageUsers`.**
   ⚠️ Esa policy se usa en **dos** endpoints ajenos a este módulo (`RoleController.cs:193` y
   `MenuController.cs:46`); endurecerla rompería la pantalla de Roles. Se usa el patrón canónico de
   11 controllers del repo: `if (!_current.Permissions.Contains("usuarios.gestionar")) return Forbid();`
   con la regla pura + tests en `Application/Calculos/`. Se aplica a la **escritura** de
   `UsersController` (`POST`, `PUT`, `PATCH`, `DELETE`, `POST {id}/reset-password`, y los
   `POST/PUT/DELETE {id}/farms…`) y a los de escritura de `UserFarmController`.
   **Las lecturas quedan abiertas** (`GET`, `GET {id}`, `GET {id}/farms`): es lo que pide el pedido.
4. **La segunda puerta de alta.** `POST /api/Auth/register`
   ([`AuthController.cs:144`](backend/src/ZooSanMarino.API/Controllers/AuthController.cs:144)) también
   crea usuarios con solo `[Authorize]`. Gatear únicamente `POST /api/Users` la deja abierta.
5. **Restaurar el `[Authorize]` comentado** de `UserFarmController.cs:13` en el mismo cambio.
6. **Front:** `*appHasPermission="'usuarios.gestionar'"` sobre el botón Crear del padre **y** sobre
   los 5 botones del hijo **en sus dos copias** (desktop y móvil) — olvidar la móvil deja la app
   abierta justo donde la usan en campo. Más los botones de los modales de granjas.
7. **«Ver detalle» hoy NO existe** — el pedido lo da por hecho. Se agrega reusando
   `modal-create-edit` en modo **solo lectura**, abierto para todos.
   ⚠️ Al hacerlo hay que cortar **todas** las escrituras que cuelgan de `saveUser()`, incluido el
   `PUT /api/ticket-perfiles/usuario/{id}` (`modal-create-edit.component.ts:441-449`), no solo el
   `PUT /api/Users`.

### 3.3 Dos deudas de permisos que se arrastran en la misma migración

Se aprovechan porque son exactamente el mismo archivo y el mismo riesgo; dejarlas afuera sería pasar
al lado de un botón roto:

- **`usuarios.revocar_sesion` nunca se sembró.** Está declarada en `RevocacionSesionCalculos.cs:73`,
  se exige en `SessionController.cs:151` y se testea — pero **ninguna migración la inserta**.
  Efecto medible hoy: el botón **«Sesiones activas»** del listado devuelve **403 a todo el que no
  sea super admin**, y la key ni siquiera es asignable desde la pantalla de Roles. Si no se siembra,
  el usuario va a leer ese 403 como un bug del permiso nuevo.
- **Cuatro keys fantasma**: `abrir_lote`, `liquidar_lote`, `cuadrar_ingresos_traslados_seguimiento`
  y `confirmar_despacho` existen en la tabla `permissions` de producción (medido) pero **no están en
  ningún seed ni migración**. Si alguna vez se recrea la BD desde migraciones, esos cuatro botones
  desaparecen para todo el mundo. Se siembran idempotentes (`WHERE NOT EXISTS` ⇒ no-op en prod).

---

## 4. Permiso `registros.fecha_retroactiva` para Lady Malave

El más simple de los cuatro: **está todo construido, solo falta la asignación.**

- El permiso existe (`permissions.id = 84`), lo creó
  [`20260820160000_SeedPermisoFechaRetroactivaRegistros.cs`](backend/src/ZooSanMarino.Infrastructure/Migrations/20260820160000_SeedPermisoFechaRetroactivaRegistros.cs).
- La regla vive en `VentanaFechaRegistroCalculos` (ventana base = **mes en curso ∪ últimos 15 días**,
  día operativo **UTC−5**; el futuro sigue cerrado **con o sin** permiso) y se aplica en el
  **controller**, vía `VentanaFechaRegistroGuard` — nunca en el service, porque el service lo comparten
  la carga masiva y las devoluciones, que fechan histórico a propósito.
- El front ya lo lee en 4 pantallas (gestión de inventario, historial de inventario, movimientos de
  pollo engorde, venta Panamá) a través de `UserPermissionService.has('registros.fecha_retroactiva')`.
- **Ya está habilitado para ItalcolEcuador** en `company_permissions` (medido: los 19 habilitados de
  la empresa 3 lo incluyen).
- ❌ **Lo único que falta:** la fila en `role_permissions` para el rol «Ecuador Administrador».

**Migración data-only, idempotente, localizando el rol por NOMBRE + empresa** (no por id):

```sql
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM public.roles r
JOIN public.role_companies rc ON rc.role_id = r.id
JOIN public.companies c       ON c.id = rc.company_id
CROSS JOIN public.permissions p
WHERE r.name = 'Ecuador Administrador'
  AND c.name = 'ItalcolEcuador'
  AND p.key  = 'registros.fecha_retroactiva'
  AND EXISTS (SELECT 1 FROM public.company_permissions cp
              WHERE cp.company_id = c.id AND cp.permission_id = p.id AND cp.is_enabled)
  AND NOT EXISTS (SELECT 1 FROM public.role_permissions rp
                  WHERE rp.role_id = r.id AND rp.permission_id = p.id);
```

> El `EXISTS` sobre `company_permissions` no es decorativo: si la empresa no lo tiene habilitado, la
> asignación queda huérfana (no viaja en el JWT, la UI la muestra tachada) y el pedido no se cumple
> aunque la migración diga «OK».

⚠️ **Alcanza al otro usuario del mismo rol.** «Ecuador Administrador» tiene **2 usuarios**: el permiso
llega a los dos. Si se quiere que sea solo ella, hay que darle un rol propio — **decisión del
usuario**, no del código.

---

## 5. Archivos que se tocan

### Backend
| Archivo | Qué |
|---|---|
| `Services/InventarioGestion/Funciones/InventarioGestionService.Ingreso.cs` | F1 — `EliminarIngresoAsync` revierte stock |
| `Services/InventarioGestion/Funciones/InventarioGestionService.Traslado.cs` | F1 — `EliminarTrasladoAsync` revierte los dos extremos |
| `Services/InventarioGestion/Funciones/InventarioGestionService.StockMutacion.cs` | F2 — escritura del ajuste de cuadre |
| `Application/Calculos/ReversionMovimientoInventarioCalculos.cs` | **nuevo** — F1, puro + tests |
| `Application/Calculos/AjusteCuadreAlimentoCalculos.cs` | **nuevo** — F2, puro + tests |
| `Application/Calculos/GestionUsuariosAutorizacionCalculos.cs` | **nuevo** — §3, puro + tests |
| `Services/CuadreAlimentoEngordeService.cs` + `.Anomalias.cs` | F4 — causa atribuida por fila |
| `API/Controllers/CuadreAlimentoEngordeController.cs` | F2 — `POST /cuadrar-galpon` |
| `API/Controllers/InventarioGestionController.cs` | F1 — doc-comment corregido |
| `API/Controllers/UsersController.cs` | §3 — `Forbid()` por permiso en los endpoints de escritura |
| `API/Controllers/UserFarmController.cs` | §3 — `[Authorize]` restaurado + gate de escritura |
| `API/Controllers/AuthController.cs` | §3 — gate en `POST /register` (2ª puerta de alta) |
| `Services/LoteAveEngordeService.cs` + `Services/Lote/…AjusteEncasetamiento.cs` | §2 — gate del delta de aves |
| `backend/sql/fn_seguimiento_diario_engorde.sql` | F2 — `INV_AJUSTE_CUADRE` en las 5 CTE (**espejo**) |
| `backend/sql/fn_cuadre_alimento_engorde.sql` | F2 — idem (**espejo**) |
| `Migrations/…AjusteCuadreAlimentoEngorde.cs` | F2 — **el vehículo**: las 2 fn + el trigger |
| `Migrations/…SeedPermisoUsuariosGestionar.cs` | §3 — + `usuarios.revocar_sesion` + las 4 keys fantasma |
| `Migrations/…SeedPermisoLoteCorregirAves.cs` | §2 — hereda de `editar_registro` |
| `Migrations/…AsignaFechaRetroactivaEcuadorAdministrador.cs` | §4 |

> ⛔ **`Program.cs` NO se toca**: la policy `CanManageUsers` la usan `RoleController.cs:193` y
> `MenuController.cs:46`, ajenos a este módulo. El gate va en los controllers.

### Frontend
| Archivo | Qué |
|---|---|
| `features/gestion-inventario/components/cuadre-alimento-engorde/*` | F2/F4 — botón «Cuadrar galpón», causa por fila, 2 señales separadas |
| `features/gestion-inventario/components/cuadre-alimento-engorde/modal-cuadrar-galpon/*` | **nuevo** — modal (con `changeDetection: Eager`) |
| `features/gestion-inventario/services/cuadre-alimento-engorde.service.ts` | F2 — `cuadrarGalpon()` |
| `features/config/user-management/pages/tabla-lista-registro/*` | §3 — gates ×2 (desktop **y** móvil) + botón «Ver detalle» |
| `features/config/user-management/user-management.component.html` | §3 — gate del botón Crear (vive en el **padre**) |
| `features/config/user-management/components/modal-create-edit/*` | §3 — modo solo lectura |
| `features/config/user-management/components/asignar-usuario-granja/*` | §3 — gates de asignar/quitar/toggle admin |
| `features/lote-engorde/components/lote-engorde-list/*` | §2 — gate de los campos de aves |
| `features/lote/components/lote-list/*` | §2 — gate de los campos de aves (hoy **sin ningún gate**) |

---

## 6. Casos de prueba

### F1 — reversión de stock al eliminar
1. Ingreso de 1.000 kg en un galpón limpio → stock 1.000, saldo 1.000, descuadre 0.
2. `DELETE /ingresos/{id}` → stock **0**, saldo 0, **descuadre 0**. *(hoy: stock 1.000, descuadre −1.000)*
3. Ingreso 1.000 → consumo 400 → `DELETE /ingresos/{id}` → **400 (Bad Request)** con el mensaje de
   kilos ya consumidos; stock y saldo intactos.
4. Traslado A→B de 500 kg → `DELETE /traslados/{gid}` → A recupera 500, B pierde 500, **ambos** con
   descuadre 0.
5. **Regresión**: `AnularMovimientoHistoricoAsync` sigue comportándose exactamente igual.

### F2 — cuadrar galpón
6. Galpón con stock 12.720 / tabla 7.720 → cuadrar a **7.720** → escribe `AjusteStock` −5.000 y
   **nada** del lado tabla → `descuadre_kg = 0`.
7. Galpón con tabla 21.216 / stock 2.566 → cuadrar a **2.566** → escribe `INV_AJUSTE_CUADRE`
   −18.650,4 y **nada** del lado stock → `descuadre_kg = 0`.
8. Motivo vacío → rechazado.
9. **Gate multipaís obligatorio** (`verificar_paridad_saldo_engorde.sql` antes/después): con la fn
   nueva y **cero** filas `INV_AJUSTE_CUADRE`, **todas** las empresas dan **0 en todas las columnas**.
10. **Cuadre global**: Ecuador pasa de 1 descuadrado a **0**. Panamá **no se mueve** (12 y 55.866,5 kg).

### §2 — corregir las aves del lote
11. Usuario **con** `lote.corregir_aves`: edita las aves de un lote de engorde ⇒ 200 y la serie
    diaria se corre por el delta conservando las bajas.
12. Usuario **sin** el permiso: los campos de aves no se editan, y el `PUT` a mano con un delta ≠ 0
    responde **403** (hoy responde 200 — el gate es cosmético).
13. **No-op**: `PUT` cambiando solo el técnico, con las aves iguales ⇒ **200 sin permiso**. El gate
    mira el delta, no el verbo.
14. `fn_cuadre_aves_engorde(NULL)` sigue en **0 descuadrados / 0 sin referencia** (línea base X8:
    191 lotes).

### §3 — gestión de usuarios
15. Usuario **con** `usuarios.gestionar`: ve y usa los botones; los `POST/PUT/DELETE` responden 2xx.
16. Usuario **sin** el permiso: ve el listado y «Ver detalle»; los botones **no se renderizan** ni en
    desktop **ni en móvil**; `POST /api/Users` y `POST /api/Auth/register` a mano responden **403**.
17. **Regresión anti-lockout**: tras la migración, **todo rol con el menú `/config/users`** tiene la
    key (comparar el set de roles antes/después; ningún rol que hoy administre puede quedar afuera).
18. **El menú «Usuarios» sigue visible para todos**: verificar que `menu_permissions` sigue **sin
    filas** para ese menú — si se insertó una, el módulo desaparece para quien no tenga la key.
19. El botón **«Sesiones activas»** deja de dar 403 a un admin de empresa (queda sembrada
    `usuarios.revocar_sesion`).

### §4 — fecha retroactiva
20. Lady Malave carga un ingreso de inventario con fecha de **hace 3 meses** → **200**.
21. Un usuario del rol «Auxilar Granja» (sin el permiso) con la misma fecha → **400** con el mensaje
    de ventana.
22. Fecha **futura** → **400 para los dos**.
23. ⚠️ **Los tres casos exigen re-login previo**: el permiso viaja dentro de la sesión cifrada, no se
    consulta por acción. Sin cerrar sesión, la usuaria va a reportar que «no pasó nada».

---

## 7. Riesgos y compuertas

| Riesgo | Mitigación |
|---|---|
| Tocar `fn_seguimiento_diario_engorde` mueve el saldo de otras empresas | `tipo_evento` **nuevo** ⇒ diferencia cero por construcción + gate de paridad multipaís antes/después, obligatorio |
| Revertir stock en `EliminarIngreso` rompe caminos que hoy dependen de que **no** revierta | Auditar los llamadores; el rechazo por stock insuficiente es un **400 explícito**, no un `NullReferenceException` |
| El gate de `usuarios.gestionar` deja a todos sin poder crear usuarios | La migración hereda de `role_menus` por **route** `/config/users`, y el caso 17 lo verifica |
| Insertar `menu_permissions` esconde el menú «Usuarios» a quien no tenga la key | La migración **no lo toca**; el caso 18 verifica que la tabla siga vacía para ese menú |
| Endurecer la policy `CanManageUsers` rompe la pantalla de Roles | No se toca la policy; el gate va en el controller (patrón de 11 controllers) |
| El gate de `lote.corregir_aves` le quita a alguien algo que hoy hace (postura está abierto) | Se hereda de `editar_registro`: nadie gana ni pierde el día del deploy |
| «El permiso no funciona» | Es re-login, lote liquidado o `user_farms` — los tres síntomas son idénticos; §2.5 los separa |
| El permiso de fecha retroactiva alcanza al 2º usuario del rol | Declarado arriba; si molesta, rol propio (decisión del usuario) |
| Un `.sql` nuevo que no llegue a producción | **Toda** fn/trigger va por migración en el mismo commit; `node backend/scripts/verificar-sql-llega-por-migracion.js` corta el CI |
| Sesiones en paralelo pisando el tracker | Bloque propio al final de `tracker_estado.md`, separado por `---` |

---

## 8. Dos hallazgos latentes que se dejan documentados y NO se tocan

Los dos salieron de la verificación adversarial. Ninguno está causando daño hoy y arreglarlos movería
números sin que nadie lo haya pedido — pero quedan escritos para que el próximo que los vea no crea
que descubrió un incendio.

1. **La CTE `stock` de `fn_cuadre_alimento_engorde` no filtra por tipo de ítem.** Suma
   `inventario_gestion_stock.quantity` de todos los ítems del galpón, mientras la anomalía R2 (en el
   mismo servicio) sí filtra `tipo_item LIKE 'alimento%'`. **Medido: no afecta a nadie hoy** — de las
   232 filas de stock de los galpones que entran al cuadre, todo lo que no es alimento está en 0
   (consulta de control incluida en el tracker). Es un falso positivo esperando un galpón que guarde
   medicamento y alimento a la vez.
2. **Las dos definiciones de «ajuste manual» no coinciden.** El backend
   (`CuadreAlimentoEngordeService.cs:146`) cuenta desde el **primer** seguimiento sin tope superior y
   **filtra por `CompanyId`**; el script `verificar_cuadre_alimento_engorde.sql:61` cuenta desde el
   **último** seguimiento y **no filtra empresa**. Son ventanas **anidadas**, no opuestas: la del
   backend contiene a la del script. Da números distintos para el mismo galpón según dónde se mire.

---

## 9. Lo que se midió (25-ago-2026, copia de producción local)

### El gate multipaís de `fn_seguimiento_diario_engorde` — **PASADO**

`backend/sql/verificar_paridad_saldo_engorde.sql`, mismo comando antes y después de la v17:

| Empresa | Filas base | Desaparecen | Nuevas | dif saldo | dif aves | dif ingreso | dif consumo | dif documento |
|---|---|---|---|---|---|---|---|---|
| ItalcolEcuador | 5.501 | **0** | **0** | **0** | **0** | **0** | **0** | **0** |
| ItalcolPanama | 1.350 | **0** | **0** | **0** | **0** | **0** | **0** | **0** |

6.765 filas de seguimiento esperadas == 6.765 presentes. `fn_cuadre_alimento_engorde(NULL)`
idéntico antes y después. **Cero por construcción**, no por suerte: ninguna fila del histórico lleva
los tipos nuevos.

### El ajuste de cuadre, probado en las dos direcciones (transacción revertida)

`backend/sql/verificar_ajuste_cuadre_alimento_engorde.sql`:

| Galpón | Situación | Antes | Después |
|---|---|---|---|
| **G0044** (Sacachún 3A, Ecuador) | sobra stock | saldo 7.720,0 · stock 12.720,0 · **descuadre −5.000,0** | stock 7.720,0 · **descuadre 0,0** |
| **G0475** (Doña María, Panamá) | sobra tabla | saldo 21.216,4 · stock 2.566,0 · **descuadre 18.650,4** | saldo 2.566,0 · **descuadre 0,0** |

**ItalcolEcuador queda en 0 galpones descuadrados.** El caso de Panamá es el que hasta hoy **no
tenía arreglo posible desde ninguna pantalla**: el `movement_type` nuevo recorrió trigger →
histórico (`INV_AJUSTE_CUADRE_SALIDA`, fechado en el último seguimiento) → fn v17 → cuadre.

> G0475 conserva su `filas_negativas = 1`, y está bien: es la **otra** señal —un día que cerró en
> rojo— que un ajuste de kilos no toca ni debe tocar. Por eso ahora son dos columnas distintas.

### Las tres migraciones de permisos (transacción revertida)

| Verificación | Resultado |
|---|---|
| Permisos nuevos | `usuarios.gestionar`, `usuarios.revocar_sesion`, `lote.corregir_aves` |
| Las 4 keys fantasma | `INSERT 0` — ya existen en prod, no-op exacto |
| `company_permissions` | 10 filas nuevas (5 empresas × 2 keys) |
| **Anti-lockout de usuarios** | **12 roles** reciben `usuarios.gestionar` = los 12 que hoy ven `/config/users` |
| **Anti-lockout de lotes** | **13 roles** reciben `lote.corregir_aves` = los 13 que hoy tienen `editar_registro` |
| Lady Malave | queda con `registros.fecha_retroactiva` efectivo |
| `menu_permissions` de `/config/users` | **0 filas** — el menú se sigue viendo |

---

## 10. Dos defectos propios, cazados antes de compilar

Los dos son la misma clase de error —**recalcular un invariante sin todos sus términos**— y los dos
habrían pasado la revisión de código porque el número «se ve bien»:

1. **`fila.StockKg` es la suma de TODOS los ítems del galpón.** Escribir ahí los kilos totales sobre
   un solo ítem lo habría inflado por lo que valen los demás. Se aplica el **delta**, no el
   absoluto. Lo insidioso: con un solo ítem con saldo —el caso normal, y el de G0044— las dos formas
   dan lo mismo. Se habría roto en el primer galpón con dos alimentos.
2. **`DescuadreKg` no es `saldo − (stock − movPost)`.** Viene corregido por lo **reservado** por la
   doble validación (`DescuadreAjustadoPorReservas`). Ignorarlo habría dejado el galpón descuadrado
   **por el monto reservado, justo después de una pantalla que dijo «cuadrado»**. Ecuador tiene 0
   reservas activas y Panamá 12.609,7 kg en 3: se habría desplegado sin que nadie lo viera en las
   pruebas de Ecuador. Ahora `ReservadoActivoKg` viaja en el DTO y tiene tests propios.
