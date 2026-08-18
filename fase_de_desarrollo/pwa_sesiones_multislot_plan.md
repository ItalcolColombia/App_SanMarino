# PWA — Sesiones multi-slot por dispositivo

**Fecha:** 2026-08-18
**Objetivo:** que **varios operarios se turnen la misma tablet sin internet**. Hoy no se puede: el
dispositivo guarda UNA sola sesión y entra el último que hizo login.

**Planes previos que este continúa:**
[`pwa_offline_first_plan.md`](pwa_offline_first_plan.md) (plan madre, decisiones D1-D7 / B1-B10) ·
[`pwa_f2_consulta_offline_plan.md`](pwa_f2_consulta_offline_plan.md) (partición de caché) ·
[`pwa_f3_captura_offline_plan.md`](pwa_f3_captura_offline_plan.md) (outbox, R9) ·
[`pwa_alistamiento_campo_plan.md`](pwa_alistamiento_campo_plan.md) (D6) ·
[`pwa_auditoria_acceso_offline_2026-08-12.md`](pwa_auditoria_acceso_offline_2026-08-12.md) (§2b: el hallazgo que origina este plan)

> **Método.** Todo lo que sigue está verificado contra el código de HOY, no contra los planes. Donde
> el tracker o un plan viejo dice una cosa y el código dice otra, **manda el código** (CLAUDE.md
> §Regla de schema). Las tres discrepancias encontradas están marcadas con 🔎.

---

## 0. Estado medido — qué está hecho y qué no

### ✅ La partición de la caché YA está, y es correcta

El tracker dice *«la partición de la caché ya está preparada; el storage de sesión no»*. **Es cierto,
verificado archivo por archivo:**

| Pieza | Archivo | Estado |
|---|---|---|
| Clave de partición `{userId}\|{companyId}\|{paisId}` | `frontend/src/app/shared/offline/funciones/clave-particion.funcion.ts` | ✅ implementada y **fail-closed** (devuelve `null` si falta cualquiera de los tres; `0` y `''` cuentan como ausencia) |
| Store `consultas` indexado por partición | `frontend/src/app/shared/offline/offline-db.ts:34-39` | ✅ índice `por_particion` |
| Store `outbox` indexado por partición | `frontend/src/app/shared/offline/offline-db.ts:43-45` | ✅ índice `por_particion`, más `companyId`/`paisId`/`userId` en cada fila |
| Purga por partición | `offline-db.ts:123` + `cache-consultas.service.ts:131` | ✅ existe `purgarParticionDe` |
| El SW no cachea API | `frontend/ngsw-config.json` | ✅ solo `assetGroups`, **cero `dataGroups`** ⇒ el Service Worker no puede filtrar respuestas entre usuarios |
| Gate D6 por sesión | `shared/offline/funciones/decidir-cache-offline.funcion.ts` | ✅ super admin y multiempresa no cachean |

**Conclusión: el trabajo de datos está hecho.** Lo que falta es (a) el llavero de sesiones, (b) tres
caminos que hoy asumen «un solo usuario» y rompen el aislamiento apenas haya dos.

### 🔴 Lo que falta, y los tres defectos que el multi-slot destapa

**F-1 — Una sola sesión, en texto plano.**
`frontend/src/app/core/auth/token-storage.service.ts:7` — `const KEY = 'auth_session'`, clave única.
`save()` (líneas 39-45) escribe `JSON.stringify(session)` en `localStorage` **o** `sessionStorage` y
borra el otro.

> 🔎 **Discrepancia 1.** CLAUDE.md y `pwa_auditoria_acceso_offline_2026-08-12.md:52` afirman que la
> sesión está **cifrada con AES**. **Es falso.** El `EncryptionService` (`core/auth/encryption.service.ts`)
> cifra el **tráfico** con el backend (login, menú, `X-Secret-Up`), nunca el storage. La sesión —token
> incluido— está en texto plano. El plan madre ya lo había medido: **B8** dice textual *«La afirmación
> de que el storage está cifrado con AES es falsa»*. Este plan **no arrastra la premisa equivocada**:
> la corrige para el llavero nuevo (§1.3) y deja el `auth_session` activo como está (§6).

**F-2 — 🔴 El `authGuard` mata la jornada de 16 h a los 60 minutos.**
`core/auth/auth.guard.ts:28` rechaza el token vencido y llama `auth.logout()`. El JWT dura **60 min**
(`backend/src/ZooSanMarino.API/appsettings.json:9`, `"DurationInMinutes": 60`). Y `logout()` →
`TokenStorageService.clear()` → **`purgarTodo()`**, que borra la caché de **todas** las particiones
del dispositivo.

O sea: un operario sin señal, a los 61 minutos, en la primera navegación **queda deslogueado y con la
caché borrada**, sin red para volver a entrar. La decisión **D4** (jornada de 16 h) está prolijamente
implementada en `funciones/politica-sesion.funcion.ts` para el camino del **timer** (eso fue B2), pero
el camino del **guard** nunca se tocó y la anula. El propio código lo sabe a medias: el comentario de
la ruta `/diagnostico` (`app.config.ts:98`) dice que existe para *«sesión vencida sin red para
renovarla»* — describe este bug como si fuera clima.

**Esto ya está roto hoy con un solo usuario.** Multi-slot no lo causa: lo vuelve imposible de ignorar,
porque una sesión aparcada se retoma justamente después de más de 60 minutos.

**F-3 — 🔴 El sync empuja las capturas de TODOS con el token del que esté activo.**
`shared/offline/sync.service.ts:71` — `const todas = await this.outbox.listarTodas()`: **toda la cola,
sin filtrar por partición**. El `AuthInterceptor` le pega el token de la sesión activa, y el servidor
estampa el autor desde el token e ignora el del cuerpo (B5, implementado en el camino de sync). Con
dos usuarios en la tablet:

- **Misma empresa** → las capturas de A **se aplican firmadas por B**. Falsificación silenciosa de
  autoría, con 200 OK.
- **Empresa distinta** → el servidor rechaza `empresa_no_autorizada`, y
  `funciones/clasificar-resultado-push.funcion.ts:48` clasifica ese código como **`reintentar`**, no
  `bandeja` (a propósito: se lo consideró transitorio). Resultado: la captura de A **reintenta para
  siempre con backoff**, nunca aterriza y **no aparece como fallo en ningún lado**.

Los dos caminos ya son alcanzables hoy (A captura sin red → cierra sesión → el outbox sobrevive por
R9 → B entra con red → el `effect` de reconexión de `SyncService` dispara). Multi-slot lo vuelve la
rutina diaria.

**F-4 — 🔴 `/diagnostico` no tiene `authGuard` y muestra el payload de todos.**
La ruta se registró **sin guard a propósito** (`app.config.ts:98-107`): es la pantalla de rescate. Su
doc-comment (`features/diagnostico/diagnostico-page.component.ts:29-31`) afirma *«No expone ningún
dato de negocio —build, estado del SW, cuota y nombres de caché»*. **Eso era verdad en F1 y dejó de
serlo en F3.1b**: hoy `recargar()` (línea 99) llama `outbox.listarTodas()` y el template pinta «Ver lo
capturado» con el JSON completo de **cada** captura, de cualquier usuario y cualquier empresa. En una
tablet compartida, cualquiera que la levante lee todo lo capturado por todos, **sin sesión**.

> 🔎 **Discrepancia 2.** El comentario del componente describe un alcance que el componente ya no
> tiene. Se corrige en este plan.

**F-5 — El logout borra la caché de todos.**
`token-storage.service.ts:156-174`: `clear()` y `clearAllTemporal()` llaman `purgarTodo()`, que vacía
el store `consultas` **entero**. El botón del sidebar (`shared/components/sidebar/sidebar.component.ts:98-104`)
usa `logout({ hard: true })`. Con slots, que A cierre sesión no puede destruir el alistamiento de B —
que cuesta una visita a la oficina con wifi.

> 🔎 **Discrepancia 3.** El outbox **sí** está a salvo: `purgarTodo` (`offline-db.ts:148`) toca solo
> `STORE_CONSULTAS`. La invariante R9 se respeta. Verificado.

---

## 1. Enfoque arquitectónico y trade-offs

### 1.1 La decisión que ordena todo: `auth_session` NO se toca

**Opción descartada (a): una clave por usuario, `auth_session:<userId>`, y adiós a la clave fija.**
Es el diseño «limpio» y el que sugiere la auditoría. Costo medido: **43 archivos** inyectan
`TokenStorageService`, **27** llaman `storage.get()`, y hay **5 lecturas crudas** de
`localStorage.getItem('auth_session')` fuera del servicio (`shared/services/menu.service.ts:143`,
`core/auth/auth.service.ts:272`, `:306`, `:338`, y el propio `token-storage.service.ts:84`, `:117`,
`:152` vía `KEY`). Renombrar la clave obliga a auditar los 43 y deja un bug latente en cualquiera que
se escape.

**Opción elegida (b): `auth_session` sigue siendo, byte a byte, la sesión ACTIVA.** El multi-slot se
construye **al lado**, como un llavero de sesiones aparcadas. Activar un slot = escribir su blob en
`auth_session` y recargar.

Consecuencia: **cero cambios** en el interceptor, los guards de permisos, los 33 módulos de features y
los 190 componentes. La superficie de cambio queda contenida en `core/auth/` + 4 archivos puntuales.
Es la aplicación directa de *«mínima superficie de cambio»*.

Costo asumido: hay dos representaciones de una sesión (activa en claro, aparcada cifrada). Se paga con
un único punto de conversión (`LlaveroSesionesService.aparcar()` / `.activar()`), que es donde viven
los tests.

### 1.2 Tres capas de storage, con secretos separados de lo que se pinta

| Clave | Contenido | Cifrado | Por qué |
|---|---|---|---|
| `auth_session` | La sesión **activa**, tal cual hoy | ❌ (igual que hoy) | Compatibilidad total con los 43 consumidores |
| `italgranja.slots.indice` | **Padrón**: `slotId`, `userId`, nombre, email, empresa, `companyId`, `paisId`, `ultimoUsoEn`, `ultimoContactoOkEn`, `saltB64`, `intentosFallidos` | ❌ **a propósito** | El selector tiene que pintarse **sin red y sin PIN**. Si estuviera cifrado haría falta una llave del dispositivo — o sea, una llave en el bundle, que es lo que B9 llama «teatro» |
| `italgranja.slots.<slotId>` | El `AuthSession` completo del usuario aparcado (token, menú, permisos) | ✅ **AES-GCM real** | Son N tokens juntos: el activo de mayor valor del dispositivo |

**Trade-off explícito:** el padrón revela **quiénes usan esa tablet y de qué empresa**. Se acepta —es
lo que hace posible el selector offline— y es **estrictamente menos** de lo que se expone hoy: hoy la
única sesión guardada entrega en claro el token, el menú y los permisos.

**Lo que NO va al padrón:** el contador de capturas pendientes. Se deriva del outbox por partición en
el momento de pintar (`leerOperaciones(db, particion)`). Duplicarlo sería un segundo número para la
misma verdad, y ya sabemos cómo termina eso (CLAUDE.md §Una sola fórmula por número).

### 1.3 El cifrado del llavero: se reabre D3 **solo** para el llavero

La decisión **D3** del plan madre eligió la opción **(b) no cifrar** el dato en reposo, y **B9**
advierte que cifrar con el `EncryptionService` actual *«sería teatro: llave pública en el bundle, salt
fijo `'sanmarino-salt'`, AES-CBC sin MAC»*. **Las dos siguen vigentes y este plan las respeta:** el
dato de negocio en IndexedDB **sigue sin cifrar**.

Pero D3 opción **(a)** existe y está escrita textual en el plan madre: *«Cifrar con llave derivada de
PIN/WebAuthn (`crypto.subtle`, salt aleatorio, AES-GCM…)»*. Se aplica **únicamente al llavero**, que
es donde el cálculo cambia: un blob de negocio robado es un snapshot viejo; N tokens robados son N
sesiones vivas de 16 h que **nadie puede revocar** (B1 no existe, ver §6).

**Cómo, sin repetir el error de B9:**

- `crypto.subtle` **exclusivamente**. Nada de `EncryptionService`, nada de crypto-js.
- **PBKDF2-SHA256, 210 000 iteraciones, salt aleatorio de 16 bytes por slot** (guardado en el padrón;
  un salt es público por diseño). Nada del `'sanmarino-salt'` fijo.
- **AES-GCM** (con MAC), IV aleatorio de 12 bytes por escritura.
- La llave se deriva del **PIN del usuario**, no de nada que viaje en el bundle. Es la diferencia
  entre cifrado y ofuscación: **la tablet robada no contiene la llave.**
- `CryptoKey` con `extractable: false`, nunca persistida.
- **Fail-closed:** sin `crypto.subtle` (contexto no seguro) el llavero **se deshabilita entero** y la
  app se comporta exactamente como hoy —una sola sesión—. No hay respaldo débil. En prod la PWA es
  HTTPS y en dev es `localhost`; los dos son contexto seguro.

### 1.4 Por qué hay PIN (no es una comodidad)

El servidor **estampa el autor desde el token** (B5). Si activar un slot no pidiera nada, el operario
B activaría el slot de A y **todo lo que capture queda firmado por A**. No es un problema de
privacidad: es de auditoría e integridad de los datos de campo.

El PIN de 6 dígitos **no se compara**: es la entrada del KDF. No se guarda hash, ni un booleano
`pinCorrecto` que alguien pueda dar vuelta con el inspector. PIN equivocado ⇒ el tag GCM no valida ⇒
**el blob no se puede descifrar**. No hay bypass posible desde el cliente.

- Se define al **aparcar** (la primera vez que ese usuario toca «Cambiar de usuario»).
- **5 intentos fallidos ⇒ el slot se destruye**: se borra el blob y el padrón queda marcado
  `requiere_reingreso`. Su caché de consultas se purga; **su outbox NO se toca** (R9, invariante).
- **PIN olvidado = no hay recuperación offline, por construcción.** Se entra normal con red. Es una
  limitación honesta y hay que decirla en el alistamiento, no descubrirla en el galpón.

### 1.5 Activar un slot **recarga la página**. No es pereza.

Hay estado en memoria por todos lados: `BehaviorSubject` con listas de la empresa en los ~33 módulos
de features, y el caché de flags de `core/services/company-config/active-company-config.service.ts`
(que además solo se limpia cuando **cambia el `activeCompanyId`**, líneas 173-180 — correcto, pero es
un razonamiento caso por caso que no quiero repetir 33 veces).

Cambiar de slot **sin recargar** significa auditar cada servicio con estado y confiar en que ninguno
se olvidó. Con `window.location.reload()` la garantía es estructural: **ningún objeto de la empresa A
puede sobrevivir a la sesión de B.** Cuesta 1-2 s servidos por el SW, sin red. Se toma.

### 1.6 Qué pasa con la caché HTTP y el outbox de cada uno

| | Caché de consultas (`consultas`) | Cola de capturas (`outbox`) |
|---|---|---|
| **Se reconstruye** | Sí, se vuelve a pedir | ❌ **No existe en ningún otro lado** |
| Al aparcar un slot | se conserva | se conserva |
| Al **cerrar sesión** de un slot | se purga **solo esa partición** | se conserva (R9) |
| Al expulsar un slot por LRU | se purga esa partición | se conserva (R9) |
| Al «borrar el dispositivo» | `purgarTodo()` | se conserva (R9) |
| Quién lo empuja al servidor | — | **solo el dueño**, con su propio token (§2, fix F-3) |

La asimetría es la misma regla de F3 y no se negocia: **nada borra la cola salvo la confirmación del
servidor o una persona.**

---

## 2. Archivos a crear y modificar (rutas verificadas)

### Nuevos

| Archivo | Qué |
|---|---|
| `frontend/src/app/core/auth/models/slot-sesion.model.ts` | `SlotSesion`, `PadronSlots`, `ResultadoActivacion` |
| `frontend/src/app/core/auth/funciones/llavero-sesiones.funcion.ts` | **Puro**: alta/actualización de una entrada del padrón, tope de slots, elección de víctima LRU, regla de «no expulsar con pendientes» |
| `frontend/src/app/core/auth/funciones/llavero-sesiones.funcion.spec.ts` | Tests del anterior |
| `frontend/src/app/core/auth/funciones/cripto-llavero.funcion.ts` | **Puro** (async): `derivarLlave(pin, salt)`, `sellar(session, llave)`, `abrir(blob, llave)`. Solo `crypto.subtle`. Devuelve `null` si no hay contexto seguro |
| `frontend/src/app/core/auth/funciones/cripto-llavero.funcion.spec.ts` | Round-trip, PIN incorrecto, sin `crypto.subtle` |
| `frontend/src/app/core/auth/llavero-sesiones.service.ts` | Orquestador delgado: I/O de `localStorage`, delega en las dos funciones puras |
| `frontend/src/app/features/auth/selector-usuario/selector-usuario.component.ts` + `.html` | Selector de perfil. `changeDetection: ChangeDetectionStrategy.Eager` (estado mutable + `await` de cripto e IndexedDB) |
| `frontend/src/app/shared/offline/funciones/filtrar-operaciones-particion.funcion.ts` + `.spec.ts` | **Puro**: de toda la cola, las que le tocan a la partición activa y ya cumplieron el backoff |

### Modificados

| Archivo | Cambio |
|---|---|
| `core/auth/token-storage.service.ts` | Separar las purgas: `clear()` deja de llamar `purgarTodo()` y pasa a `purgarParticionDe(identidadActual())`. Se agrega `borrarDispositivo()` con el `purgarTodo()` de hoy. `identidadActual()` (línea 19) ya existe y se reusa |
| `core/auth/auth.service.ts` | `logout(opts)` acepta `'slot' \| 'dispositivo'`; nuevo `cambiarDeUsuario(pin)` que aparca y navega al selector |
| `core/auth/auth.guard.ts` | Deja de decidir solo; consulta la política pura (fix **F-2**). Sin red **no** llama `logout()` (purga) |
| `core/auth/funciones/politica-sesion.funcion.ts` | Nueva `evaluarAccesoOffline({ tokenVencido, enLinea, ahora, ultimoContactoOk, operacionesPendientes })` |
| `core/auth/funciones/politica-sesion.funcion.spec.ts` | Casos de la nueva función |
| `shared/offline/sync.service.ts` | `enviarPendientes()` filtra por la partición ACTIVA (fix **F-3**). Inyecta `TokenStorageService` y delega en la función pura nueva |
| `shared/components/sidebar/sidebar.component.ts` + `.html` | «Cambiar de usuario» (nuevo) y «Cerrar sesión» con la semántica nueva; «Borrar el dispositivo» detrás de confirmación |
| `features/diagnostico/diagnostico-page.component.ts` + `.html` | Enmascarar el payload de las filas ajenas (fix **F-4**) y corregir el doc-comment de las líneas 29-31 |
| `features/auth/login/login.component.ts` | Tras un login OK, registrar/refrescar el slot en el padrón |
| `app.config.ts` | Ruta `selector-usuario` con `loadComponent`, **sin** `authGuard` (por definición la abre alguien que todavía no tiene sesión activa) |

### Primitivas obligatorias (CLAUDE.md §Sistema de diseño)

- `ToastService` para todo aviso. **Prohibido `alert()`.**
- `ConfirmDialogService` (`shared/services/confirm-dialog.service.ts`) para expulsar un slot, descartar
  capturas y borrar el dispositivo. **Prohibido `confirm()`**; los métodos pasan a `async`.
- `changeDetection: ChangeDetectionStrategy.Eager` **explícito** en el selector. Es exactamente el
  escenario del bug canónico: pantalla con `await` de `crypto.subtle` e IndexedDB; omitirlo en v22 = OnPush
  = selector colgado en «Cargando…».

---

## 3. Cambios de BD / SQL

**Ninguno. El plan es 100 % cliente.**

No hay migración EF, no hay SQL crudo, no hay columna nueva en `companies`. El endpoint
`POST /api/Sync/push` y la tabla `sync_operaciones` se usan **tal cual están**: el fix de F-3 es dejar
de mandarle a ese endpoint operaciones que no son del dueño del token — el servidor ya hacía lo
correcto rechazándolas.

**Dependencia de backend que este plan NO resuelve:** **B1** (`jti` + `sesiones_activas` + refresh).
Verificado: **no existe nada en el backend** (`grep` de `sesiones_activas|jti|refresh_token` sobre
`backend/src` → 0 resultados). Ver §6.

---

## 4. Reglas de negocio

**R-M1 — Tope: 4 slots por dispositivo.** El turno real son 2-3 operarios; el cuarto es margen. No es
arbitrario: cada slot cuesta una partición de caché contra una cuota finita, y el padrón es una lista
de blancos. Se acota a propósito.

**R-M2 — Expulsión: LRU por `ultimoUsoEn`, y NUNCA un slot con capturas pendientes.** Si los 4 tienen
pendientes, **se rechaza el quinto login** con un mensaje que dice qué hacer («conectate y enviá las
capturas de <fulano> antes de sumar otro usuario»). Fail-closed: antes que destruir trabajo de campo,
se niega la comodidad. Expulsar purga la caché de esa partición; **jamás su outbox** (R9).

**R-M3 — El outbox del que se va queda intacto, y visible.** Aparcar, cerrar sesión, expulsar y borrar
el dispositivo **no tocan la cola**. El padrón muestra por slot cuántas capturas esperan, así que
«dónde quedó lo que cargó Alex» tiene respuesta sin adivinar.

**R-M4 — Cada cola sale con el token de su dueño.** El push filtra por la partición activa. Es el fix
de F-3 y la regla que evita firmar el trabajo de A con la identidad de B.

**R-M5 — Al aparcar o cerrar sesión con capturas pendientes Y con red, se avisa.** `ConfirmDialogService`:
«Hay N capturas sin enviar. Si salís ahora quedan en la tablet hasta que <usuario> vuelva a entrar.
¿Enviarlas primero?» — con red se puede resolver en el acto; sin red es solo información, no un
bloqueo (bloquear a alguien que no tiene señal es encerrarlo).

**R-M6 — Qué borra cada salida.** Es un cambio de comportamiento sobre un botón que ya existe:

| Acción | Sesión | Caché propia | Caché de los otros | Outbox |
|---|---|---|---|---|
| **Cambiar de usuario** (nuevo) | aparcada, cifrada con PIN | se conserva | se conserva | intacto |
| **Cerrar sesión** (existe, cambia) | el slot se elimina | **se purga** (`purgarParticionDe`) | **se conserva** ← antes se borraba | intacto |
| **Borrar el dispositivo** (nuevo) | se eliminan todos los slots | se purga | **se purga** (`purgarTodo`) | intacto (R9) |

**R-M7 — El selector sin red muestra:** nombre, empresa, hace cuánto se usó, cuántas capturas esperan
y si el slot venció la jornada. **No** muestra permisos, menú ni nada que salga de descifrar el blob.
Un slot vencido (>16 h sin contacto, D4) se pinta apagado con «necesita conectarse»; se puede elegir
igual, pero lleva al login con red, no adentro.

**R-M8 — Jornada de 16 h por SLOT, no por dispositivo.** Cada entrada del padrón lleva su propio
`ultimoContactoOkEn`. Que B haya hablado con el servidor hace 5 minutos no le renueva la jornada a A.

**R-M9 — D6 se evalúa por slot.** `decidirCacheOffline` corre contra la sesión activa, así que sale
gratis; hay que **no romperlo**: un slot de super admin o de cuenta multiempresa sigue sin cachear
nada, aunque comparta tablet con operarios que sí cachean.

**R-M10 — El primer ingreso de cada usuario sigue exigiendo red.** No cambia y no se puede cambiar:
`POST /auth/login` es HTTP y en prod hay reCAPTCHA (`features/auth/login/login.component.ts:41`, activo
solo con `environment.production`). El alistamiento pasa de *«un usuario por tablet»* a *«hasta 4
usuarios alistados por tablet»* — cada uno tiene que entrar una vez con señal **y visitar las
pantallas que va a usar**, o su partición está vacía.

**R-M11 — El acceso offline con token vencido está acotado por tres cosas a la vez:** que no haya red,
que el slot esté dentro de sus 16 h, y el PIN. Con red, un token vencido sigue cerrando sesión
**exactamente como hoy**.

---

## 5. Casos de prueba

### 5.1 Unitarios (Karma, `.spec.ts` co-locado — la convención real: 70 co-locados vs. 3 en `src/tests/`)

**`llavero-sesiones.funcion.spec.ts`**
1. Alta de un slot en un padrón vacío ⇒ 1 entrada.
2. Re-login del mismo `userId` ⇒ **actualiza**, no duplica.
3. Quinto usuario con 4 slots ⇒ expulsa el de `ultimoUsoEn` más viejo.
4. Quinto usuario y el LRU tiene pendientes ⇒ elige el **siguiente** sin pendientes.
5. Quinto usuario y **los 4** tienen pendientes ⇒ rechaza con motivo tipado (R-M2).
6. Slot vencido (>16 h) ⇒ se marca vencido pero **no se borra solo** (borrar es purgar).

**`cripto-llavero.funcion.spec.ts`**
7. Round-trip: `abrir(sellar(s, llave), llave)` devuelve la sesión idéntica.
8. **PIN incorrecto ⇒ lanza** (tag GCM). Nunca devuelve basura ni `null` silencioso — devolver algo
   sería peor que fallar.
9. Dos slots con el mismo PIN ⇒ **blobs distintos** (salt e IV aleatorios).
10. Sin `crypto.subtle` ⇒ `null` y el llavero se deshabilita (fail-closed).

**`politica-sesion.funcion.spec.ts`** (los 4 que fijan que no hay regresión)
11. Token válido ⇒ pasa (idéntico a hoy).
12. Token vencido **con red** ⇒ cierra sesión (**byte a byte** el comportamiento de hoy).
13. Token vencido **sin red**, dentro de 16 h ⇒ **deja navegar**.
14. Token vencido **sin red**, pasadas 16 h ⇒ deniega, y **sin purgar**.
15. Los tests existentes de `evaluarFinDeSesion` siguen verdes sin tocarlos.

**`filtrar-operaciones-particion.funcion.spec.ts`**
16. Cola con ops de 2 particiones ⇒ devuelve solo la activa.
17. Las ajenas quedan **en la cola** (no se borran, no se marcan rechazadas).
18. Respeta `proximoIntentoEn` igual que hoy.
19. Partición `null` (identidad incompleta) ⇒ **no envía nada** (fail-closed).

### 5.2 🔴 Smoke S1 — dos operarios turnándose SIN RED (el caso que motiva el plan)

Con los dos perfiles reales de operario que el tracker ya usó, **ambos de una sola empresa** ⇒ D6 los
deja cachear:
- **A** = `alexlondono@sanmarino.com.co` → empresa 1 (Agroavícola Sanmarino)
- **B** = `ladymalave@ecuitalcol.com` → empresa 3 (ItalcolEcuador)

1. Alistamiento **con red**: A entra, visita sus pantallas, «Cambiar de usuario» y define PIN.
2. B entra, visita sus pantallas, «Cambiar de usuario» y define PIN.
3. **Modo avión.**
4. Selector: se ven los **dos** slots, con empresa y última vez. Sin red.
5. Entrar como A con PIN ⇒ ve **sus** pantallas cacheadas. Capturar un seguimiento ⇒ toast de
   pendiente + 202.
6. Cambiar a B con PIN ⇒ ve las **suyas**. Capturar.
7. Volver a A, y a B otra vez. **Dos vueltas completas** (el checklist de modales de CLAUDE.md).
8. **PIN equivocado** 1 vez ⇒ error claro y sigue afuera. 5 veces ⇒ el slot se destruye, y el
   **contador de capturas pendientes de ese slot NO baja**.
9. **La prueba de F-2:** con >60 min de reloj offline (o bajando `DurationInMinutes` en un build de
   prueba), navegar entre pantallas ⇒ **no desloguea y no borra la caché**. Hoy: desloguea y borra.
10. Cerrar la app, matarla, reabrirla sin red ⇒ el selector sigue con los dos slots.

**Criterio de aceptación:** los dos entran y capturan sin red, ninguno pierde su caché por lo que
haga el otro, y el conteo de pendientes por slot cuadra con lo capturado.

### 5.3 🔴 Smoke S2 — dos empresas distintas, caza de fugas

Mismo par A (empresa 1) / B (empresa 3), que es justamente el caso peligroso.

11. Con **B** activo, recorrer las pantallas cacheadas: **ningún dato de la empresa 1** en ningún lado
    (lotes, granjas, inventario, catálogos).
12. `/diagnostico` con B activo (y también **sin ninguna sesión activa**, que es como se abre en un
    rescate): las capturas de A se **listan** —para que nada parezca perdido— pero **sin payload**,
    sin «Copiar captura» y sin «Descartar». Fix de F-4.
13. Inspeccionar IndexedDB a mano: `consultas` y `outbox` con **claves de partición disjuntas**,
    `{userIdA}|1|<pais>` y `{userIdB}|3|<pais>`, cero cruces.
14. **Restaurar la red con B activo:** se empujan **solo** las capturas de B. Verificar en la BD que
    las de A **no se aplicaron**, no cambiaron de estado y **no quedaron rechazadas**. Antes del fix,
    acá salía `empresa_no_autorizada` en bucle de reintentos.
15. Cambiar a A, reconectar ⇒ las de A aterrizan con **el guid de A** en `created_by_user_id`.
16. Prueba de la falsificación de autoría: repetir 14-15 con **dos usuarios de la MISMA empresa**.
    Sin el fix, las capturas del aparcado se aplican firmadas por el activo. Con el fix, cada una
    queda con su autor.
17. «Cerrar sesión» de A ⇒ la caché de **B sigue entera** (R-M6). Antes se borraba.
18. Limpiar los datos del smoke **por la API**, no por SQL, y verificar que los saldos de aves
    vuelven exactos (el procedimiento que ya se usó en F3.1).

### 5.4 Regresión y bordes

19. **Un solo usuario** (el 100 % del parque hoy): login, uso, logout ⇒ comportamiento **idéntico** al
    actual. Es la prueba que protege a quien nunca va a usar multi-slot.
20. **D6:** aparcar una cuenta multiempresa o super admin ⇒ el slot existe pero **sigue sin cachear**
    (R-M9).
21. **Sin `crypto.subtle`** (contexto no seguro): no aparece «Cambiar de usuario», la app funciona como
    hoy con una sesión. Nada roto, nada a medias.
22. **Cambio de empresa** dentro de un mismo slot (`setActiveCompany`, `token-storage.service.ts:101`)
    ⇒ sigue purgando la partición que deja, como hoy.
23. **Cuota de almacenamiento** con 4 particiones llenas: mirar `navigator.storage.estimate()` en
    `/diagnostico`. Que el navegador desaloje la base entera se lleva puesto el outbox de los 4.
24. **Multi-pestaña:** el listener de `storage` (`token-storage.service.ts:184`) ya sincroniza
    `auth_session` entre pestañas. Verificar que activar un slot en una pestaña no deja a la otra con
    la sesión anterior en memoria (la recarga de §1.5 aplica a la pestaña que activa; la otra recibe
    el evento).

### 5.5 Validación de build

```bash
cd frontend && yarn build     # 0 errores. Margen de bundle: 967 kB de initial contra
                              # el techo de error de 2,05 MB (tras V22) ⇒ ~1,08 MB de aire
cd frontend && yarn test --watch=false --browsers=ChromeHeadless
```
El backend **no se toca**, así que no hay `dotnet build` ni `dotnet test` en este plan. El selector va
**lazy** (`loadComponent`), como las 27 rutas que V22 difirió.

---

## 6. Riesgos y qué NO hace este plan

### Riesgos

**Ri-1 — 🔴 El `authGuard` deja pasar un token vencido (sin red). Es el punto que necesita OK explícito.**
Es la única forma de que la jornada de 16 h de D4 sea real, y es la corrección de un bug que ya
existe. Lo que acota la ventana: no hay red, el slot está dentro de sus 16 h, y hay PIN. Lo que **no**
la acota: **B1 no existe**, así que una tablet perdida no se puede revocar. Con red el comportamiento
no cambia: token vencido ⇒ afuera.
*Argumento de por qué no amplía la exposición de datos:* lo que se ve navegando con token vencido es
la caché local **de ese mismo usuario**, que ya está en el dispositivo sin cifrar. No se agrega ningún
dato nuevo; se agrega quién puede llegar a él, y eso lo cierra el PIN.

**Ri-2 — Cambia el comportamiento de un botón que ya existe.** «Cerrar sesión» deja de borrar la caché
de todo el dispositivo. Es deliberado (R-M6) y es lo que hace viable el alistamiento compartido, pero
es una regresión potencial para quien esperaba el borrado total. Mitigación: el caso 19, y que
«Borrar el dispositivo» conserva el comportamiento viejo con nombre honesto.

**Ri-3 — B1 pasa de urgente a obligatorio.** El plan madre ya lo decía: *«una jornada offline de 16 h
sin revocación es una ventana de acceso irrevocable. Es el más urgente»*. Con 4 slots son 4 ventanas.
**Recomendación: no desplegar multi-slot en más tablets que un piloto hasta que B1 exista.**

**Ri-4 — Cuota de almacenamiento ×4.** `core/pwa/almacenamiento-persistente.service.ts` ya pide
`navigator.storage.persist()`, pero conceder no es obligación del navegador. El tope de 4 y la
expulsión LRU son la contención; el caso 23 es la medición.

**Ri-5 — PIN olvidado = sin recuperación offline.** Es consecuencia directa de cifrar de verdad (§1.3):
si hubiera recuperación offline habría una puerta trasera, y entonces el cifrado no serviría. Va en el
instructivo de alistamiento.

**Ri-6 — El padrón revela quién usa la tablet.** Aceptado en §1.2. Alternativa (cifrar el padrón con
llave del dispositivo) descartada por B9: sería teatro y además rompería el selector offline.

**Ri-7 — Se agrega un segundo esquema de cifrado al front.** Uno para el tráfico
(`EncryptionService`, AES-CBC, llave del bundle) y otro para el llavero (`crypto.subtle`, AES-GCM,
llave del PIN). Confundirlos sería grave. Mitigación: el módulo nuevo **no importa** `EncryptionService`
ni crypto-js, y su doc-comment dice explícitamente por qué no puede reusarlo (B9).

### Lo que este plan NO hace

- ❌ **No implementa B1** (`jti` + `sesiones_activas` + refresh). Es backend, es su propio bloque, y es
  la dependencia declarada de Ri-1/Ri-3.
- ❌ **No hace que el primer login funcione sin red.** Login y reCAPTCHA necesitan red y van a seguir
  necesitándola. El alistamiento con wifi, por usuario y por tablet, sigue siendo obligatorio (R-M10).
- ❌ **No cifra el dato de negocio en reposo.** D3 sigue en (b). Solo el llavero pasa a (a).
- ❌ **No cifra el `auth_session` activo** (B8). Cambiarlo obliga a tocar `read()`
  (`token-storage.service.ts:176`), que es **síncrono** y alimenta el `BehaviorSubject` en el
  constructor, mientras `crypto.subtle` es asíncrono. Es un refactor propio, con su propio riesgo, y
  mezclarlo acá haría imposible saber qué rompió qué.
- ❌ **No implementa el opt-in de D6 por rol y dispositivo.** Necesita registro de flota
  (`dispositivos_sync`), que no existe. La **prohibición** de D6 sí sigue activa y se respeta por slot.
- ❌ **No toca F4** (movimientos, traslados, ventas e inventario offline). Se siguen **consultando** y
  no se guardan sin red. Ver [`pwa_f4_mapeo_modulos_pendientes.md`](pwa_f4_mapeo_modulos_pendientes.md).
- ❌ **No agrega telemetría de flota ni pantalla de administración de dispositivos.**
- ❌ **No cambia el JWT de 60 min** ni el `SessionTimeoutService`. La política de inactividad con red
  queda igual.
- ❌ **No despliega nada.** La PWA sigue sin salir a producción (`main-produccion` atrasado); esto se
  suma a lo que espera ese merge, no lo desbloquea.

---

## 7. Orden de implementación sugerido

Cada paso deja el repo compilando y con los tests verdes. Los dos primeros son **fixes de bugs que ya
existen** y tienen valor por sí solos, aunque el multi-slot se cancele después.

1. **F-3 — el sync filtra por partición.** Función pura + tests + cambio en `sync.service.ts`. Corrige
   la falsificación de autoría **hoy**.
2. **F-2 — el `authGuard` respeta D4.** `evaluarAccesoOffline` + tests + guard. Corrige la jornada de
   16 h **hoy**.
3. **F-4 — `/diagnostico` deja de mostrar el payload ajeno**, y se corrige su doc-comment.
4. **F-5 / R-M6 — separar las purgas** en `TokenStorageService` (`clear()` por partición,
   `borrarDispositivo()` con `purgarTodo()`).
5. **Llavero**: modelos, las dos funciones puras y sus tests. Sin UI todavía.
6. **`LlaveroSesionesService`** + registro del slot al hacer login.
7. **Selector de usuario** (`Eager`, lazy, sin `authGuard`) + ruta.
8. **Sidebar**: «Cambiar de usuario», «Cerrar sesión» con la semántica nueva, «Borrar el dispositivo».
9. **Smokes S1 y S2** en un Android real. Nada de F1/F2/F3 se probó nunca fuera de local: esta es la
   primera vez que hay una razón fuerte para hacerlo.

> **Tracker (STEP 2).** Al arrancar la implementación, agregar el bloque **al final** de
> `tracker_estado.md`, separado por `---`, sin tocar los bloques de otras sesiones.
