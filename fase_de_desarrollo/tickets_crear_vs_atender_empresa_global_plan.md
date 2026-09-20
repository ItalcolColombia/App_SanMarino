# Plan — Tickets: separar ABRIR de ATENDER, la empresa correcta y el alcance EMPRESA / GLOBAL

> 19-sep-2026 · Estado: **plan, sin código**. Hay DDL (F2 y F3) y datos de prod (F1): nada se implementa
> sin el OK explícito de las decisiones del final.

## Pedido

1. «Cuando voy al usuario y le parametrizo un ticket **pasa a ser resolutor**». Los roles tendrían que servir
   para **abrir** tickets, con control **interno por empresa**, y aparte un control del **admin global** donde se
   elige el resolutor global o el de la empresa.
2. En **Santa Reyes** se habilitó a **Lenin** para crear tickets y **no le aparece resolutor**, aunque el usuario
   (Jose Moises) es resolutor global de Desarrollo en todas las empresas.
3. **Alexander Mejía** (Sanmarino) aparece como **«Global»** y él es solo de Colombia.
4. Los globales solo los puede elegir **el admin de todas las empresas**.
5. Explicar **por qué la confusión** y por qué hay que habilitar «siempre», persona por persona, a cada usuario.

## Diagnóstico — medido sobre la copia local de producción (18-sep-2026 19:30)

Hay **cinco causas**; ninguna sola explica todo, y juntas producen exactamente los tres síntomas.

### Causa 1 · La misma pestaña «Tickets» significa dos cosas opuestas

| Dónde | Qué dice la pestaña | Qué guarda realmente |
|---|---|---|
| Usuarios → Tickets | «Quién puede CREAR» | `ticket_perfil_usuario.nivel` (NORMAL / IMPLEMENTADOR) — **abrir** |
| Roles → Tickets | «Quién RECIBE (plantilla del rol)» | `ticket_resolutor_rol` — **atender** |

Quien busca «que este rol pueda abrir Desarrollo» entra a Roles → Tickets, prende *Desarrollo* y lo que
consigue es que **todos los del rol pasen a ser resolutores**. Pasó el **18-sep-2026**: al rol **Costos**
(Panamá; su único permiso de tickets es `tickets.crear`) le prendieron DESARROLLO. Resultado medido:

- Isaac Mares y Rafael Moreno (Panamá) quedaron **resolutores de Desarrollo** y aparecen en el desplegable de
  «Asignar a» de los tickets **de Sanmarino**, con la etiqueta «Global».
- **Siguen sin poder abrir un solo ticket**: son 2 de los usuarios «mudos» de la causa 3.

Además, al **asignar un rol** a un usuario, `UserService` **copia** la plantilla del rol a `ticket_resolutores`
(`SeedTicketPerfilesAsync`), y esa copia **no se borra** si después le quitan el rol: se queda resolutor para
siempre. La copia es innecesaria: el listado de asignables ya lee la plantilla del rol en vivo.

### Causa 2 · Se guarda en la empresa DEL QUE EDITA, no en la del usuario / rol (esto es lo de Lenin)

`TicketPerfilService.GetEffectiveCompanyIdAsync()` toma la **empresa activa del administrador**. Si el admin está
parado en Sanmarino y edita a un usuario de Santa Reyes, el perfil se escribe con `company_id = 1`:

| Usuario | Pertenece a | Perfil guardado en | Efecto |
|---|---|---|---|
| **Lenin Castilla** (15-sep) | 6 Santa Reyes | **1 Sanmarino** | en Santa Reyes sigue NORMAL |
| Admin Santa Reyes (4-sep) | 6 | **1** | (lo salva el permiso `tickets.admin`) |
| usuario 1 demo / usuario 2 demo | 4 Demo | **1** | en Demo siguen sin perfil |
| Lady Solange Malave | 3 Ecuador | 3 **y** 1 (inactivo) | duplicado |

**5 de los 10 perfiles** están en una empresa a la que el usuario no pertenece. Lo mismo pasa con la plantilla
del rol: la fila de **Costos** (rol de Panamá) quedó guardada en **Sanmarino**.

**Por qué nadie lo vio:** el `GET` del perfil usa la *misma* empresa equivocada. Cuando el admin reabre a Lenin
desde Sanmarino, la pantalla le muestra «Implementador» y todo parece bien. Solo mirando desde Santa Reyes se ve
vacío.

### Causa 3 · Abrir un caso depende de cada PERSONA y de que exista resolutor del tipo en esa empresa

`GetTiposPermitidosAsync`: sin perfil → **NORMAL** → solo Soporte y Dudas; y un tipo sin resolutor en la empresa
**no se ofrece** (ver memoria `tipo-de-ticket-sin-resolutor-no-existe`). Lo único que sube a IMPLEMENTADOR sin ir
persona por persona son `tickets.gestionar` / `tickets.admin`, que además dan poder de **gestionar** la bandeja.
No existe una forma de decir «este rol puede abrir Desarrollo». Simulación del formulario «Nuevo caso» para cada
usuario activo en cada empresa a la que pertenece:

| Empresa | Usuarios | Con permiso de tickets | **No pueden abrir NINGÚN tipo** | Tipos con resolutor |
|---|---|---|---|---|
| Sanmarino | 20 | 7 | 0 | los 4 |
| Ecuador | 23 | 2 | 0 | los 4 |
| Demo | 3 | 3 | 0 | Desarrollo, Req., Soporte |
| Panamá | 17 | 8 | **2** (Isaac, Rafael — rol Costos) | solo Desarrollo, Req. |
| Santa Reyes | 4 | 4 | **3** (Lenin, Diego Ospina, Sebastián Zubieta) | solo Desarrollo, Req. |

En Santa Reyes y Panamá **nadie atiende Soporte ni Dudas**, así que todo usuario NORMAL ve el formulario sin
ningún tipo: el endpoint responde `200 []`, no hay error, el botón no se puede usar. La única salida hoy es
**subir a cada persona a Implementador a mano** (el «habilitar siempre»)… y por la causa 2, si el admin no está
parado en esa empresa, eso tampoco funciona.

### Causa 4 · «Global» significa dos cosas, y ninguna es la que se lee

1. **La etiqueta**: `pais_id NULL` se muestra como **«Global»** (`GetAsignablesInternalAsync`), pero solo quiere
   decir «todos los países *de esa fila de esa empresa*». En Sanmarino **todos** los asignables salen «Global».
   **Alexander Mejía** sale «Global» porque su rol *Sistemas sanmarino* tiene Soporte/Dudas con país NULL. Medido:
   **no aparece en ninguna otra empresa** — es un problema de etiqueta, no de alcance.
2. **Por qué quedó NULL**: el editor pone «Global» como país por defecto a cualquiera cuyo rol **contenga** «admin»
   (`roles.some(r => r.toLowerCase().includes('admin'))`) — el mismo anti-patrón por substring que el repo ya
   prohibió para el catálogo global (`Admin Panama`, `Santa Reyes Administrador`… entran).
3. **El global de verdad** (el de Jose Moises en Desarrollo) no existe como concepto: es la fila del rol `Admin`
   **repetida en cada empresa** (1, 3, 4, 5, 6) + que la membresía del rol **no se filtra por empresa**. Ese mismo
   atajo es el que dejó entrar a los de Costos (Panamá) en Sanmarino.

### Causa 5 · Nadie controla quién escribe

- `api/ticket-perfiles` **no tiene ningún gate**: cualquier sesión puede `PUT usuario/{su propio id}` y hacerse
  Implementador o resolutor de cualquier tipo, o reescribir la plantilla de cualquier rol.
- `GET api/tickets/global` (todos los tickets de **todas** las empresas) y `global/resolutores` **no chequean
  nada**: Lenin puede listar los casos de Panamá.
- `tickets.admin` = «administración global» en tablero, roadmap, búsqueda y «a nombre de», pero lo tienen roles
  **de una empresa** (`Santa Reyes Administrador`, `Admin Demo`, `Lider Demanda & Delivery`).
- `PuedeVerTicketAsync`: un resolutor de un tipo ve los tickets de ese tipo **de cualquier empresa** de su país.

### Relación con el trabajo pendiente `PERFIL-TICKETS-AL-CREAR` (16-sep, sin commitear, otra sesión)

Ese cambio (el modal de usuario salta a la pestaña Tickets al crear) es **compatible pero insuficiente**: su
diagnóstico asumió que el problema era no poder llegar a la pestaña, y lo probó con el admin y el usuario en la
**misma** empresa. Con la causa 2 viva, un usuario de Santa Reyes creado desde Sanmarino sigue quedando NORMAL.
F1 lo vuelve correcto; con F2 deja de ser el camino principal. **No lo toco ni lo commiteo** (no es mío).

## Modelo propuesto — dos preguntas, dos lugares, nunca mezclados

| Pregunta | Dónde se decide | Quién lo configura |
|---|---|---|
| **¿Quién puede ABRIR y de qué tipo?** | **ROL** (nivel de creación) + excepción por persona | admin de la empresa, para SU empresa |
| **¿Quién ATIENDE?** — alcance **EMPRESA** | rol o persona, en la empresa del ticket | admin de la empresa, para SU empresa |
| **¿Quién ATIENDE?** — alcance **GLOBAL** (todas las empresas) | rol o persona | **solo el admin global** (super admin o rol `Admin`/`Administrador` exacto = policy `AdminEmpresas`) |

Invariantes:
- **Dar permiso de abrir NUNCA crea un resolutor.** Son tablas, pantallas y endpoints distintos.
- **Todo se guarda en la empresa del USUARIO / ROL**, nunca en la empresa activa del que edita. Ambigüedad ⇒ 400
  (fail-closed).
- **«Global» se muestra solo para alcance GLOBAL.** Una fila de empresa se etiqueta con el nombre de la empresa.
- **Una sola fórmula de «asignable»**: la usa el desplegable, la valida `CreateAsync`, la valida `TransferirAsync`
  y la usa la visibilidad del ticket (hoy son tres implementaciones con reglas distintas).

## Fases

### F1 — La empresa correcta + gates mínimos + datos (arregla a Lenin; chico, urgente)

Sin DDL. Sin cambio de modelo.

- **Nuevo** `Application/Calculos/TicketPerfilEmpresaCalculos.cs` (puro): `ResolverEmpresa(empresaActiva,
  empresasDelDestino)` → la activa si el destino pertenece a ella; si no, la única del destino; si tiene varias o
  ninguna ⇒ `null` (el service responde 400 «el usuario pertenece a varias empresas: cambiá a la empresa donde
  querés configurarlo»).
- `TicketPerfilService`: `Get/UpsertPerfilUsuarioAsync` resuelven con `user_companies` del usuario;
  `Get/UpsertPerfilRolAsync` con `role_companies` del rol. El DTO de salida suma `CompanyId` + `CompanyName` para
  que la pantalla diga **en qué empresa** está configurando.
- **Nuevo** `Application/Calculos/TicketPerfilAutorizacionCalculos.cs` (puro) + gate en `TicketPerfilesController`
  (escrituras): pasa `AdminEmpresas`, o `tickets.admin` / rol administrador de empresa **sobre usuarios y roles de
  su empresa activa**; nadie se edita a sí mismo salvo `AdminEmpresas`. 403 con mensaje.
- `TicketsController` `global` y `global/resolutores`: exigir `tickets.admin` (hoy cualquiera). F4 lo endurece.
- **Migración data-only** `…_CorregirEmpresaPerfilesTickets` (Designer clonado, snapshot intacto, idempotente,
  reglas genéricas, **no por id**):
  1. `ticket_perfil_usuario` con `company_id` fuera de `user_companies`: si el usuario tiene **una** empresa y no
     hay fila allí ⇒ se muda (`UPDATE company_id`); si ya hay fila allí o tiene varias ⇒ `activo = false`.
     Esperado sobre la copia: Lenin 1→6, Admin Santa Reyes 1→6, usuario 1/2 demo 1→4; Lady (1) ya inactivo.
  2. `ticket_resolutor_rol` con `company_id` fuera de `role_companies` del rol **y** que no sea el rol
     administrador de la aplicación (nombre exacto) ⇒ `activo = false`. Esperado: la fila Costos/DESARROLLO en
     Sanmarino. Las del rol `Admin` quedan: son la intención global, F3 las formaliza.
- **Nuevo** `backend/sql/verificar_perfiles_tickets_empresa.sql` (solo lectura, congela/compara): perfiles fuera
  de su empresa, filas de rol fuera de su empresa, **usuarios mudos por empresa** (la tabla de la causa 3).

### F2 — Abrir tickets por ROL (termina con el «habilitar siempre»)

- DDL idempotente: `ALTER TABLE roles ADD COLUMN IF NOT EXISTS ticket_nivel_creacion varchar(20) NULL` + CHECK
  (`NORMAL`/`IMPLEMENTADOR`). NULL = el rol no define ⇒ NORMAL, **idéntico a hoy**. Entidad `Role`, config EF,
  migración con snapshot.
- **Nuevo** `Application/Calculos/TicketNivelEfectivoCalculos.cs`: nivel = el mayor entre (permiso
  gestionar/admin), (nivel de los roles del usuario en esa empresa — mismo conjunto de roles que arma los permisos
  de la sesión) y (perfil personal en esa empresa). Con todos los roles en NULL el resultado es **byte a byte**
  el de hoy (test de equivalencia).
- `TicketPerfilService.GetTiposPermitidosAsync` delega en esa función. El perfil por persona queda como
  **excepción para subir** (no baja lo que da el rol; se documenta en pantalla).
- Contrato: `TicketResolutorRolDto` + `NivelCreacion`; `UpsertTicketResolutorRolRequest.NivelCreacion` opcional
  (null = no tocar ⇒ compatible hacia atrás).
- Front `ticket-perfil-editor`, modo **rol**, dos bloques separados y rotulados:
  **① «Qué puede ABRIR quien tenga este rol»** (Normal / Implementador) — primero y visible;
  **② «Qué ATIENDE (resolutor)»** — debajo, con el aviso «esto hace que le LLEGUEN tickets».
  Modo **usuario**: «Por su rol en *Santa Reyes* puede abrir: …» + la excepción personal, con la empresa visible.
- Front `ticket-create`: estado vacío explícito cuando `tipos-permitidos` = [] («Tu empresa no tiene quién atienda
  los tipos que podés abrir; avisale al administrador») en vez del formulario mudo.

### F3 — Alcance EMPRESA / GLOBAL del resolutor + una sola fórmula

- DDL idempotente: `alcance varchar(10) NOT NULL DEFAULT 'EMPRESA'` + CHECK en `ticket_resolutor_rol` y
  `ticket_resolutores`. Constantes `TicketAlcance` en Domain.
- Datos (misma migración, idempotente): una fila (rol, tipo, país) del rol administrador de la aplicación activa
  en **todas** las empresas ⇒ **una** fila `GLOBAL` y las copias por empresa `activo = false`. Esperado: `Admin`
  / DESARROLLO (hoy repetida en 1, 3, 4, 5 y 6). El resto queda `EMPRESA` (Alexander incluido).
- **Nuevo** `Application/Calculos/TicketResolutorAlcanceCalculos.cs`: `Aplica(alcance, filaEmpresa,
  ticketEmpresa)`, `Etiqueta(alcance, nombreEmpresa)` («Global» solo para GLOBAL) y `PuedeEscribir(...)`
  (GLOBAL o empresa ajena ⇒ solo `AdminEmpresas`).
- `GetAsignablesInternalAsync`: `alcance = GLOBAL OR company_id = empresa del ticket`; etiqueta por alcance
  (Alexander ⇒ «Agroavícola Sanmarino»); se saca el N+1 de usuarios de paso.
- `TicketService.CreateAsync`, `TransferirAsync` y `PuedeVerTicketAsync` usan **la misma** regla (se cierra la
  asimetría que la memoria dejaba «a propósito»: hoy la API acepta asignar a quien el desplegable no ofrece).
- `UserService`: se retira la **copia** de la plantilla del rol al asignar roles; se retiran
  `ReaplicarPlantillaRolAsync`, su endpoint y el botón «Aplicar a usuarios del rol». Las 11 filas directas que
  existen quedan (medido: todas en la empresa de su usuario).
- Siembra de empresa nueva (`TicketPerfilAtencionSiembraCalculos.FilasFaltantes`): omite los tipos ya cubiertos
  por una fila GLOBAL activa del rol (parámetro nuevo con default vacío ⇒ los 21 tests actuales siguen iguales).
- Front editor: el selector de **país** se reemplaza por **«Atiende: Esta empresa (nombre) / 🌍 Todas las empresas
  (Global)»**; la opción Global solo si `isSuperAdmin` o `esAdminDeAplicacion(roles)` (se reusa
  `catalogos-globales.funcion.ts`; se elimina el `includes('admin')`). Default siempre «Esta empresa». El admin
  global además puede elegir **otra empresa** (así se configura, explícito, «Jose atiende Requerimiento de Santa
  Reyes»).

### F4 — `tickets.admin` deja de ser «global» (decisión aparte)

Hoy `tickets.admin` de un rol de empresa ve y gestiona **todas** las empresas (tablero, roadmap, búsqueda global,
«a nombre de», detalle). Propuesta: el alcance de todas las empresas pasa a exigir `AdminEmpresas`; `tickets.admin`
sin eso queda acotado a **su** empresa. Toca `TicketAlcancePanelCalculos`, `TicketService.Busqueda/Detalle/
Creacion/Gestion` y `TicketTareaService`. Cambia lo que ven Santa Reyes Administrador, Admin Demo y Lider Demanda
& Delivery ⇒ va solo con OK explícito.

## Reglas de negocio (resumen verificable)

1. Abrir: nivel efectivo = max(permiso gestionar/admin, nivel de sus roles en la empresa, excepción personal en la
   empresa). Sin nada ⇒ NORMAL.
2. Un tipo aparece en «Nuevo caso» solo si el nivel lo permite **y** hay ≥ 1 asignable en la empresa del ticket.
3. Asignable = fila activa del tipo con `alcance = GLOBAL` o `company_id = empresa del ticket` (+ país como hoy).
4. Escribir en la empresa X sobre un usuario/rol de X: admin de X o `AdminEmpresas`. GLOBAL o empresa ajena: solo
   `AdminEmpresas`. Uno mismo: solo `AdminEmpresas`.
5. Perfil y plantilla se guardan en la empresa del usuario/rol; ambiguo ⇒ 400.

## Casos de prueba

**xUnit (Application.Tests)**
- `TicketPerfilEmpresaCalculosTests`: activa ∈ destino; destino único ≠ activa (caso Lenin); varias sin la activa
  ⇒ null; ninguna ⇒ null.
- `TicketPerfilAutorizacionCalculosTests`: super admin / rol `Admin` exacto / `Admin Panama` (no global) /
  `Santa Reyes Administrador` sobre su empresa sí, sobre otra no, GLOBAL no / auto-edición.
- `TicketNivelEfectivoCalculosTests`: equivalencia con la lógica actual con roles NULL (tabla completa), rol
  IMPLEMENTADOR sin perfil, perfil NORMAL + rol IMPLEMENTADOR ⇒ IMPLEMENTADOR.
- `TicketResolutorAlcanceCalculosTests`: aplica/no aplica por empresa, GLOBAL aplica en todas, etiquetas.
- `TicketPerfilAtencionSiembraCalculosTests`: los 21 actuales intactos + tipos cubiertos por GLOBAL se omiten.

**Migraciones (BEGIN…ROLLBACK sobre la copia, dos pasadas)**: 1.ª mueve 4 perfiles y apaga 1 fila de rol (F1) /
convierte 1 GLOBAL y apaga 4 copias (F3); 2.ª = 0 cambios; `verificar_perfiles_tickets_empresa.sql` antes/después.

**Smoke API + UI (backend aislado sobre un clon)**
1. Lenin en Santa Reyes: «Nuevo caso» ofrece Desarrollo y Requerimiento con «Jose Moises Desarrollo · Global».
2. Diego Ospina (Auxiliar de operación) tras poner su rol en Implementador (F2): ve Desarrollo/Requerimiento
   **sin tocar su usuario** y **sin aparecer como resolutor** en ningún lado.
3. Sanmarino, Soporte: Alexander sale «· Agroavícola Sanmarino», no «Global»; Isaac y Rafael ya no aparecen.
4. Admin parado en Sanmarino edita a un usuario de Santa Reyes ⇒ la fila queda en 6 y la pantalla lo dice.
5. Lenin: `PUT /api/ticket-perfiles/usuario/{su id}` ⇒ 403; `GET /api/tickets/global` ⇒ 403.
6. Santa Reyes Administrador: marcar GLOBAL ⇒ 403 / opción deshabilitada; resolutor de su empresa ⇒ 200.
7. Crear una empresa nueva ⇒ Desarrollo con asignable (GLOBAL) sin filas nuevas por empresa para Desarrollo.
8. Empresas sin cambios de configuración (Ecuador, Demo): mismo desplegable que antes, salvo la etiqueta.

**Validación**: `dotnet build` (0 errores, sin advertencias nuevas) + `dotnet test`; `yarn build`; gate
`verificar-sql-llega-por-migracion.js`; sin procesos huérfanos.

## Decisiones que necesito antes de implementar

1. **Cómo se define «puede abrir» por rol:** (a) *recomendado* — nivel en la pestaña Tickets del rol (bloque ①);
   (b) un permiso nuevo `tickets.crear_desarrollo` en la pestaña Permisos.
2. **Quién atiende Soporte y Dudas en Santa Reyes y Panamá** (hoy nadie ⇒ usuarios mudos): el admin global, el
   administrador de cada empresa, o lo configurás vos después de F1.
3. **Rol Costos (Panamá):** apagar la fila de resolutor que quedó en Sanmarino (F1) y darle al rol el nivel
   Implementador (F2), que es lo que se quiso hacer el 18-sep.
4. **F4** (`tickets.admin` deja de ver todas las empresas): sí / no / más adelante.

## Lo que NO se hace

- No se tocan los tickets existentes ni sus asignaciones.
- No se borra ninguna fila: todo lo que se corrige queda `activo = false` o se muda de empresa.
- No se toca el trabajo sin commitear de `PERFIL-TICKETS-AL-CREAR` (otra sesión).
- No se despliega sin pedido explícito.

---

## Decisiones tomadas (19-sep-2026) y qué se implementó

| Decisión del usuario | Cómo quedó |
|---|---|
| «Puede abrir» se define **por rol**, en la pestaña Tickets del rol | Columna `roles.ticket_nivel_creacion` (NULL = no define ⇒ NORMAL) + bloque ① en el editor |
| Soporte/Dudas de Santa Reyes y Panamá **los configura el usuario después** | La migración NO siembra resolutores nuevos; el verificador mide los «usuarios mudos» que quedan |
| Rol **Costos**: apagar la fila de resolutor y darle Implementador | Regla genérica en la migración: fila de rol NO administrador en empresa ajena ⇒ se apaga y, si era Desarrollo/Requerimiento, el rol recibe `IMPLEMENTADOR` |
| **F4** entra en esta entrega | `tickets.admin` administra su empresa activa; todas las empresas solo el admin global |

### Backend

- **Cálculos puros nuevos** (`Application/Calculos/`), todos con tests xUnit:
  `TicketPerfilEmpresaCalculos` (en qué empresa vive el perfil), `TicketPerfilAutorizacionCalculos` (quién
  puede escribir, y que GLOBAL es solo del admin global), `TicketNivelEfectivoCalculos` (nivel = mayor entre
  permiso, roles y perfil personal; equivalencia con lo previo), `TicketResolutorAlcanceCalculos` (aplica /
  etiqueta / plan de guardado sin pisar otras empresas) y `TicketAlcanceAdministracionCalculos` (F4).
- `TicketAlcance` (EMPRESA/GLOBAL) en Domain; `alcance` en `ticket_resolutores` y `ticket_resolutor_rol`;
  `Role.TicketNivelCreacion`.
- **Una sola fórmula de asignable**: `TicketAsignablesConsulta` la usan el desplegable, `CreateAsync`,
  `TransferirAsync` y la visibilidad del caso (antes eran tres reglas distintas).
- `TicketPerfilService` reescrito: resuelve la empresa del usuario/rol, exige permiso (403 con motivo),
  devuelve `companyId`/`companyName`/`nivelPorRol`/`puedeElegirGlobal`, y el `companyId` opcional permite al
  admin global configurar otra empresa.
- `ICurrentUser.EsAdminEmpresas` (misma regla que la policy `AdminEmpresas`) resuelto en `HttpCurrentUser`.
- `UserService` ya **no copia** la plantilla del rol al asignar roles (y se retiran `SeedPerfilDesdeRol` /
  `ReaplicarPlantillaRol` con sus endpoints y su botón).
- F4 en `TicketService` (tablero/roadmap/panel, `tickets/global`, detalle, gestión de caso y «a nombre de»)
  y en `TicketTareaService`; `GET api/tickets/global` pasa a exigir `tickets.admin` (antes, nada).
- Siembra de empresa nueva: omite los tipos que ya cubre una fila GLOBAL.

### Migraciones

1. `20260920010340_AddAlcanceYNivelCreacionTickets` — DDL idempotente (`ADD COLUMN IF NOT EXISTS` + CHECK).
   Se le quitó el `AlterColumn` de `produccion_resultado_levante.lote_id` que EF quiso arrastrar: es una
   **deriva previa del modelo** (la entidad dice `int`, el snapshot decía `int/text`, la BD tiene `text`),
   ajena a este trabajo, y se dejó el snapshot como estaba en ese punto.
2. `20260920010400_CorregirEmpresaYAlcanceConfiguracionTickets` — data-only (Designer clonado): muda/apaga
   los perfiles fuera de su empresa, apaga la fila de Costos y le da el nivel, y convierte
   `Admin`/DESARROLLO en una fila GLOBAL apagando las 4 copias.

### Medido sobre un clon de la copia de producción

| | Antes | Después |
|---|---|---|
| Perfiles de apertura activos fuera de su empresa | 4 | **0** |
| Plantillas de rol activas en empresa ajena no esperadas | 1 (Costos) | **0** |
| Filas GLOBAL | 0 | 1 (`Admin`/Desarrollo → Jose Moises) |
| Copias por empresa que la GLOBAL ya cubre | — | **0** |
| Usuarios que no pueden abrir ningún tipo (Santa Reyes / Panamá) | 3 / 11 | **2 / 9** |

Los que siguen mudos son los que dependen de la decisión pendiente (nadie atiende Soporte/Dudas en esas dos
empresas): Diego Ospina y Sebastián Zubieta en Santa Reyes, y 9 usuarios de Panamá. Segunda corrida de la
migración: 0 filas afectadas.
