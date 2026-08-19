# Tracker de estado

> **Depurado el 16-ago-2026** (47 bloques cerrados + 28 pendientes obsoletos), **revalidado contra
> el código** ese mismo día, **limpiado de nuevo el 17-ago-2026** (V11) y **el 18-ago-2026**
> (**26 bloques archivados, 4.317 → 2.176 líneas**): los bloques que quedaron 100 % `- [x]` y
> commiteados salieron del archivo y viven abajo, en una línea cada uno.
> Nada se perdió: el texto completo está en git (`git show <commit>:tracker_estado.md`); el tracker
> previo a la primera depuración, en `git show fd542b9:tracker_estado.md`.
>
> Regla de sesiones en paralelo: cada sesión toca **sólo su bloque**; los bloques nuevos van **al
> final**. ⚠️ **V8 (descuadres de alimento de Panamá) está reservada para otra sesión — no tocar.**

> **Convención de marcas (triaje 18-ago-2026).** Hasta hoy `- [ ]` quería decir «sin cerrar» por
> razones muy distintas —una tarea, un hallazgo medido, una decisión tuya, un paso de un admin
> externo— y eso hacía imposible automatizar el barrido. Ahora cada marca dice una sola cosa:
>
> | Marca | Significa | ¿Un agente lo puede ejecutar? |
> |---|---|---|
> | `- [ ]` | **Tarea ejecutable**: hay código que escribir y una verificación que correr | ✅ sí |
> | `- [x]` | Hecho y verificado | — |
> | `- [!]` | **Requiere una decisión tuya** o un OK explícito (varias son irreversibles en prod) | ❌ no, hasta que decidas |
> | `- [~]` | **Fuera del repo**: admin de Microsoft 365, paso manual en pantalla, generar secretos, deploy | ❌ no |
> | `- [i]` | **Hallazgo o nota**: el registro de un hecho medido. No hay nada que ejecutar | ❌ no hay acción |
>
> Consecuencia práctica: `grep -c '^- \[ \]' tracker_estado.md` cuenta **sólo trabajo real**.
> Al cerrar una tarea usá `- [x]`; al abrir una nueva, `- [ ]`. Un hallazgo nace `- [i]`, no `- [ ]`.

| Pend. | Bloque abierto | Quién lo destraba |
|---|---|---|
| 4 | Envío de correo: SMTP rechazado por política del tenant | **admin de Microsoft 365** |
| 4 | Referencia `Inicio` + liquidación de corridas anteriores (engorde) | **decisión de negocio** |
| 1 | ItalJira: barrido de sobregiro de aves | **decisión** (correr el detector contra prod) |
| 2 | Reporte Contable — Selección en RESUMEN + Movimientos de Huevo | **decisión** (corte 24/25 sem · K345) |
| 1 | Migraciones Masivas — retirar tipos | **decisión** (¿sale «Venta Engorde»?) |
| 1 | Migraciones Masivas — sólo Sanmarino | **decisión** (¿Santa Reyes conserva el módulo?) |
| 1 | Lote cerrado que absorbe el ciclo siguiente (KM 86) | operación (cerrar por pantalla) |
| 2 | v16 de engorde — marca `para_proximo_ciclo` | rediseño (persistir la atribución) |
| 1 | PWA — auditoría de acceso offline | **decisión** |
| 2 | PWA — punto de retoma | **push + merge a `main-produccion`** |
| 2 | PWA — brecha para salir a producción | **push + merge** |
| 1 | Gerencia: Panel de control | post-deploy manual (rol + menú en la UI) |
| 1 | Bitácora agosto 2026 — V8.6 | **V8 reservada para otra sesión** |
| 1 | V19 · kardex de bultos de la GRANJA | **decisión** (el saldo sobreestima) |
| 2 | V20 · saldo negativo del lote 12 (KM 86) | **decisión irreversible en prod** |
| 4 | V25 · trabajo derivado del triaje | 2 tareas + el detector contra prod |
| 1 | V27 · Engorde FASE B | **decisión de producto** (rediseño del modelo de entrega) |
| 1 | V28 · columna «Próx. ciclo» | smoke manual en pantalla |
| 7 | V30 · Santa Reyes — Italapp | **aprobación del cliente** + estructura física y códigos ERP |
| 2 | V39 · B1 — revocación de sesión | 1 tarea (cerrar la ventana de gracia) + vigencia en la TaskDef |
| 4 | PWA — lo único que falta probar en un equipo real | **un Android y dos operarios** |

> **45 pendientes reales al 18-ago-2026** (9 tareas · 17 decisiones · 19 fuera del repo), repartidos
> en **21 bloques abiertos**. Los `- [i]` —45— son hallazgos, no pendientes: no entran en la cuenta.
>
> - **9 `- [ ]` son tareas ejecutables.** Varias son *features* que piden su propio plan según el
>   workflow de CLAUDE.md, no entran en «una tarea = un commit».
> - **17 `- [!]` esperan una decisión tuya.** Varias tocan producción de forma irreversible
>   (los lotes de Ecuador · los 15 días traslapados de K345 · el lote 12 de KM 86 · los lotes 2601).
>   Ningún agente las debe correr solo.
> - **19 `- [~]` están fuera del repo**: admin de Microsoft 365, secretos de prod, pasos manuales en
>   pantalla, el merge a `main-produccion` y los 4 smokes que piden un equipo real.
>
> ⚠️ La tabla de arriba se calcula contando las marcas de cada bloque. Si al cerrar algo no cuadra,
> el que miente es el resumen, no el bloque: recontá con
> `grep -c '^- \[ \]' tracker_estado.md` y sus variantes `[!]` / `[~]`.

## Entregado y archivado

Bloques cerrados al 100 % y commiteados. Se resumen acá; el detalle completo está en el commit.

| Fecha | Bloque | Commit | Qué dejó |
|---|---|---|---|
| 05ago26 | Gastos de inventario — las 10 líneas con `concepto = 'insumo'` | `2cab258` | Migración data-only con regla dinámica; el catálogo y la auditoría intactos |
| 16ago26 | Gastos de inventario — rango de fechas del consumo | `90f97ad` | Rango Desde/Hasta que acota **igual** la tabla y el Excel · smoke en pantalla en V11 |
| 17ago26 | **V9 · Barrido de pendientes** | `1771bd0` `aadd97b` `4a070e8` `f6d2f56` `a19807b` | 2 gates de CI que cortan lo que sólo se ve en pantalla · guard del despacho de aves reservadas en postura · soft-delete en cascada · Implementación ↔ ItalJira · vacunación W1.1-W1.2 |
| 17ago26 | **V10 · Vacunación W1.3 + W1.4** | `bd935cb` | CRUD de plantillas + pantalla; `efectiva` explica **por qué** un lote quedó sin plan |
| 17ago26 | **Vacunación W2** — materializador | `f2794c6` | La plantilla baja al cronograma; idempotente y **nunca borra** |
| 17ago26 | **Vacunación W3** — bandeja de «hoy me toca» | `59496a8` | `fn_vacunacion_pendientes` + aviso de fuera de rango antes del 400 |
| 17ago26 | **Vacunación W4** — alcance por ubicación | `056a371` | Las 2 fns respetan `restrict_locations` (fail-closed) · **cierra la serie W** |
| 06ago26 | **Tracker** | `b34e629` | Consolidado de sublotes y paridad de reportes por fase |
| 08ago26 | **Auditoría de cierre** | `362155c` | «alimento previo al encaset» + fix del chip (SOLO LECTURA, sin código) |
| 12ago26 | **PWA F3.1** | `c44e0a4` | Captura offline (outbox) con idempotencia real |
| 17ago26 | **V11 · Cierre de los smokes pendientes + limpieza del tracker** | `d74c667` | — |
| 17ago26 | **V12 · V7.27** | `addd777` | el saldo de alimento y el cuadre ignoran `validado` |
| 17ago26 | **Cola de baja prioridad** | `a579d53` | mirar sólo cuando se toque producción |
| 17ago26 | **V13 · Saldo de aves de levante** | `48e9d6a` | cuatro consumidores, dos fórmulas |
| 17ago26 | **V14 · Bloquear el consumo cuando no hay stock del alimento** | `f79fd45` | — |
| 17ago26 | **V15 · La excepción D4 (alimento previo al encaset) es inalcanzable desde la UI** | `6b7abe7` | — |
| 17ago26 | **V16 · Fase 3 de R2** | `a886e90` | señalar el alimento que queda al liquidar |
| 17ago26 | **V17 · V8** | `55c1b40` | los descuadres de alimento de Panamá tienen nombre |
| 17ago26 | **V18 · El saldo guardado se separó de la fn en Panamá** | `ead4635` | y la liquidación lo congela |
| 17ago26 | **V21 · V19.3.4** | `e3762fd` | el aviso del kardex de bultos, verificado EN PANTALLA |
| 17ago26 | **V22 · Aire en el bundle: las pantallas de administración salen del arranque** | `6f083c7` | — |
| 17ago26 | **V23 · B10** | `56f7caa` | el Super Admin deja de ser un correo en el código |
| 18ago26 | **V24 · La empresa activa se valida (cierra el hallazgo V23.3)** | `75213a9` | — |
| 18ago26 | **V26 · Engorde FASE A** | `07f1bee` | la marca `para_proximo_ciclo` vuelve a ser inerte |
| 18ago26 | **V29 · PWA F-3** | `7a64d43` | el push deja de firmar el trabajo de un operario con la identidad de otro |
| 18ago26 | **V31 · PWA F-4** | `8b2a096` | la pantalla de rescate deja de mostrar (y de borrar) lo que capturó otro |
| 18ago26 | **V32 · PWA F-2** | `d1ac0ef` | el `authGuard` deja de matar la jornada de 16 h a los 60 minutos |
| 18ago26 | **V33 · PWA F-5** | `0a3b661` | cerrar sesión dejaba de borrar el alistamiento de los demás |
| 18ago26 | **V34 · PWA multi-slot paso 5** | `9b6b157` | el llavero, en lógica pura y sin UI |
| 18ago26 | **V35 · PWA multi-slot paso 6** | `aa32fcc` | el llavero deja de ser inerte: se anota el slot al hacer login |
| 18ago26 | **V36 · PWA multi-slot paso 7** | `b7270d4` | el selector de perfil, con las tres decisiones de UX tomadas |
| 18ago26 | **V37 · PWA multi-slot paso 8** | `1786f98` | el sidebar cierra el circuito: aparcar, cerrar sesión y borrar el equipo |
| 18ago26 | **V38 · PWA multi-slot** | `6e4fe7f` | la jornada de 16 h por slot se cuenta desde el último contacto real |

---


# Tracker — Envío de correo: migración a Microsoft Graph API (retiro de auth básica SMTP)

**Plan:** [`fase_de_desarrollo/envio_correo_graph_api_plan.md`](fase_de_desarrollo/envio_correo_graph_api_plan.md)
**Fecha:** 2026-08-05 · Bloque propio — no tocar desde otras sesiones

Producción no envía correos: Microsoft retiró la **auth básica para SMTP Client Submission** en
Exchange Online (rechazo desde 01-mar-2026, refuerzo total 30-abr-2026; error
`550 5.7.30 Basic authentication is not supported for Client Submission`).
**Blocker:** `System.Net.Mail.SmtpClient` no soporta XOAUTH2 ⇒ no alcanza con cambiar la contraseña,
hay que cambiar el emisor. **Decisión del usuario: Microsoft Graph API.**
Único punto de envío real: `EmailQueueProcessorService:213-305` (el resto sólo encola).

## Fase 0 — Auditoría y plan
- [x] Mapeado el flujo completo: 3 encoladores (`EmailService`, `TicketService`, `AuthService`) → `email_queue` → 1 solo emisor
- [x] Confirmada la causa con fuentes de Microsoft (timeline y código de error)
- [x] Verificado que no hay paquetes de Graph/MailKit/AWS SES en los `.csproj`
- [x] Plan escrito + decisión de transporte confirmada por el usuario

## Fase 1 — Abstracción y cálculo puro
- [x] `Application/Interfaces/IEmailSender.cs` + `EnvioCorreoResultado`
- [x] `Application/Calculos/EnvioCorreoCalculos.cs` (resolver proveedor, clasificar errores, payload, vigencia de token)

## Fase 2 — Transportes (Infrastructure)
- [x] `Email/SmtpEmailSender.cs` — traslado literal del código de hoy (dev local + rollback)
- [x] `Email/GraphTokenProvider.cs` — client_credentials + caché con margen de 5 min
- [x] `Email/GraphEmailSender.cs` — `POST /v1.0/users/{buzon}/sendMail`, 202 = OK, reintento único ante 401
- [x] `Email/SinTransporteEmailSender.cs` — transporte nulo con diagnóstico (evita el crash de arranque)

## Fase 3 — Cableado
- [x] `EmailQueueProcessorService` delega en `IEmailSender` (retries/estados/metadata intactos);
      263 líneas de SMTP inline eliminadas del procesador (580 → 317 líneas)
- [x] Se elimina el `throw` del constructor (podía tumbar el arranque en ECS) → log crítico
- [x] `Program.cs`: `AddHttpClient("graph-email")` + registro del `IEmailSender` resuelto por config
- [x] `appsettings.json` / `appsettings.Development.json` con `Email:Provider` + `Email:Graph` (sin secretos)
- [x] `ecs-taskdef-new-aws.json`: `Email__Provider=auto` + `Email__Graph__*` vacíos ⇒ desplegar la
      TaskDef **no cambia nada** hasta que carguen las credenciales; ahí conmuta solo
- [x] `backend/documentacion/MIGRACION_CORREO_GRAPH_API.md` (app registration paso a paso)
- [x] Los 3 documentos con instrucciones ya muertas (habilitar SMTP AUTH / App Password) marcados ⛔ OBSOLETO

## Fase 4 — Tests (gate CI)
- [x] `EnvioCorreoCalculosTests` — **53 tests**: tabla de decisión completa del proveedor
      (incluye retrocompatibilidad dev local y provider explícito sin config ⇒ NO cae a SMTP en silencio),
      vigencia del token, payload de `sendMail` serializado, clasificación 401/403/404/429/5xx y diagnósticos

## Fase 5 — Validación
- [x] `dotnet build` — **0 errores, 0 advertencias**
- [x] `dotnet test` — **1.626 Application + 1 Domain verdes** (1.573 previos + 53 nuevos)
- [x] Smoke 1 (sin config Graph): elige **SMTP** — `📧 Transporte de correo: SMTP (smtp.office365.com:587)`,
      retrocompatibilidad de desarrollo local intacta
- [x] Smoke 2 (con credenciales Graph): elige **Graph** — `transporte: graph`, buzón correcto
- [x] Smoke 3 (`provider=graph` con config incompleta): log **crítico** con las variables que faltan y
      **la aplicación arranca igual** (antes esto tumbaba el arranque del `HostedService` en ECS)
- [x] BD local sin tocar (`email_queue` 60 failed / 52 sent, idéntico antes y después; 0 filas `pending`)
- [x] Sin procesos huérfanos — puerto 5499 libre
- [x] Commit acotado (sin footer de atribución)

## Fase 6 — ⚠️ CORRECCIÓN DEL DIAGNÓSTICO (05-ago-2026, tras el aviso del usuario)

El usuario avisó que el arreglo era mucho más chico («solo hay que cambiarle el protocolo»).
**Tenía razón en que mi diagnóstico estaba mal.** Yo había atribuido la falla al retiro global de la
auth básica **por la fecha del anuncio de Microsoft, sin haber visto nunca el error real** (el
usuario no lo tenía a mano). Con la BD local ya sincronizada con producción, el error apareció.

**Error real** (`email_queue` id 112, 05-ago-2026 12:35 UTC):
`530 5.7.57 Client not authenticated` + `535 5.7.139 ... did not meet the criteria to be
authenticated successfully. Contact your administrator.` — **NO** es `550 5.7.30`.

- [x] Probe SMTP a mano (`EHLO`→`STARTTLS`→`AUTH LOGIN`): **`235 Authentication successful`**
      ⇒ la auth básica de este tenant SIGUE VIVA y las credenciales son válidas
- [x] Handshake con TLS 1.2 / 1.3 / default: los tres autentican ⇒ **la versión de TLS no es la causa**
- [x] Puerto 465 (TLS implícito): cerrado en Office 365 ⇒ descartado (y `SmtpClient` tampoco puede)
- [x] 🔴 Hipótesis del orden `UseDefaultCredentials`/`Credentials`: reprodujo el error exacto en
      **.NET Framework** (PowerShell), pero un test en **.NET 10** demostró que ahí NO borra las
      credenciales ⇒ **descartada**. Casi la publico como causa raíz corriendo el experimento en el
      runtime equivocado; el test con la premisa falsa se eliminó
- [x] ✅ **Envío REAL con el bloque `SmtpClient` idéntico al de la app, sobre .NET 10 → ENVIADO OK**
      (2 correos de prueba entregados a `zootecnico@sanmarino.com.co`)
- [x] Config desplegada verificada: idéntica a la del repo (587 / EnableSsl=true / mismas credenciales)
- [x] Último envío exitoso en la cola: **3-jun-2026**; desde ahí fallan todos, sin cambios en el emisor

**Conclusión:** credenciales ✅, código ✅, protocolo ✅. Lo que rechaza es una **política del tenant
según el origen de la conexión** (el propio Exchange dice *"Contact your administrator"*).
**El código no puede arreglarlo.**

- [x] Diagnósticos de `SmtpEmailSender` reescritos: dejan de culpar a la contraseña y al retiro de
      auth básica; ahora indican Conditional Access / SMTP AUTH y los comandos exactos para el admin
- [x] `MIGRACION_CORREO_GRAPH_API.md` §1 reescrito con el diagnóstico verificado y la tabla de
      hipótesis descartadas + los dos caminos de solución
- [x] `dotnet build` 0/0 · `dotnet test` 1.626 + 1 verdes

### Pendiente del usuario — Camino A (rápido, si el admin puede)
- [~] Conditional Access / Security Defaults: ¿bloquea legacy auth por ubicación o IP? Excluir el origen
- [~] `Get-CASMailbox 'zootecnico@sanmarino.com.co' | Select SmtpClientAuthenticationDisabled` ⇒ debe dar `False`
- [~] `Get-TransportConfig | Select SmtpClientAuthenticationDisabled` ⇒ debe dar `False`

### Pendiente del usuario — Camino B (sólo si el A no se puede)
- [~] Migrar a OAuth 2.0 / Microsoft Graph. La implementación completa está en el commit `c7b6834`
      (`git show c7b6834`): emisor Graph, proveedor de token con caché e instructivo del app
      registration. Se revirtió a pedido del usuario para dejar un solo transporte.

## Fase 7 — Simplificación a SMTP-only (pedido del usuario: «más fácil y desplegar de una vez»)

- [x] Eliminados `GraphEmailSender`, `GraphTokenProvider` y `SinTransporteEmailSender`
- [x] `EnvioCorreoCalculos` reducido a lo de SMTP: `HayConfiguracionSmtp`, `ClasificarErrorSmtp`,
      `EsRechazoPorPolitica`, `DiagnosticoSinConfiguracion`
- [x] `Program.cs`: `AddSingleton<IEmailSender, SmtpEmailSender>()` — se fue el `AddHttpClient`, el
      switch de proveedor y `Email:Provider`. Si falta config SMTP, avisa y NO tumba el arranque
- [x] `Email:Graph` fuera de `appsettings.json` / `appsettings.Development.json`; `Email__Provider`
      y `Email__Graph__*` fuera de la TaskDef ⇒ **las variables desplegadas quedan idénticas a hoy**
- [x] Doc reescrita como `DIAGNOSTICO_CORREO_OFFICE365.md` (la de migración se eliminó); los 3 docs
      viejos con banner corregido — ya no dicen «migró a Graph» ni culpan a la contraseña
- [x] Se conserva lo que sí aportaba el refactor: procesador delgado (580→317 líneas), diagnósticos
      honestos en `email_queue.error_message` y sin `throw` en el constructor del `HostedService`
- [x] Tests reescritos (24): configuración, clasificación con los `error_type` HISTÓRICOS y detección
      del rechazo por política. Incluye el hueco conocido `"timed out"` ≠ `"timeout"`, documentado
      y **conservado** (cambiarlo alteraría el `error_type` de filas ya existentes)
- [x] `dotnet build` 0/0 · `dotnet test` **1.601 Application + 1 Domain verdes**
- [x] Smoke local: arranca con `transporte: smtp`, sin log crítico; puerto 5499 liberado
- [x] Commit acotado (sin footer de atribución)

### Evidencia adicional hallada en Fase 7
El historial de `email_queue` por mes muestra un corte **limpio**, no intermitente:
feb-may 2026 = **45 enviados / 0 fallidos**; junio corta y desde ahí 0 enviados / 47 fallidos.
Y el mismo síntoma ya había ocurrido en nov-2025/ene-2026, resolviéndose **del lado administrativo**
(a partir de febrero el envío volvió solo, sin tocar el emisor). Refuerza que la causa es del tenant.

> ⚠️ **Desplegar no arregla el correo.** El código ya envía bien (probado sobre .NET 10 con las
> credenciales de producción). El destrabe está en Microsoft 365 — ver Camino A.

### 🔴 Deuda detectada al pasar (fuera de alcance, requiere trabajo propio)
- Credenciales en texto plano commiteadas: contraseña SMTP (`appsettings.json:77`,
  `appsettings.Development.json:30`, `ecs-taskdef-new-aws.json:48`), cadena de conexión de RDS prod
  y clave JWT en la TaskDef. Deben rotarse y moverse a Secrets Manager.

---

# Corrección de la referencia `Inicio` + liquidación de corridas anteriores (pollo engorde)

**Plan:** [`fase_de_desarrollo/correccion_referencia_inicio_engorde_plan.md`](fase_de_desarrollo/correccion_referencia_inicio_engorde_plan.md)
**Fecha:** 2026-08-05

## Parte A — Corrección de datos por migración
- [x] A1 Los 4 lotes con `Inicio` ≠ encaset quedaban fuera de toda auditoría (`referencia_confiable = false`)
- [x] A2 Clasificadas DOS causas opuestas: 5 y 7 con `Inicio` de plantilla (25.000/25.000 del 2026-03-23, 6 lotes) · 30 con `aves_encasetadas` inflado
- [x] A3 Evidencia bloque 1: capacidad del galpón (22-25 mil en otros ciclos, 50.000 = doble) + el lote 7 cierra en **0 exacto en ambos sexos**
- [x] A4 Evidencia bloque 2: bajo el `Inicio` ambos sexos cierran en **0 exacto**; bajo el encaset sobran 700 H y 700 M (excedente partido en dos)
- [x] A5 Reglas dinámicas probadas contra TODA la base: bloque 1 alcanza solo 5 y 7, bloque 2 solo el 30 — ninguna nombra ids
- [x] A6 Simulación en transacción + `ROLLBACK` antes de tocar nada
- [x] A7 Migración `20260805170000_CorreccionInicioHistorialYEncasetEngorde` (data-only, Designer clonado, sin tocar ModelSnapshot) + SQL trazable en `backend/sql/`
- [x] A8 Aplicada en local con `ASPNETCORE_ENVIRONMENT=Development` (host 127.0.0.1:5433 verificado en el log de EF)
- [x] V1 `dotnet build` 0/0 · `dotnet test` **1.573 + 1 verdes**
- [x] V2 Re-ejecución del SQL ⇒ `UPDATE 0` / `UPDATE 0` (idempotente)
- [x] V3 `fn_cuadre_aves_engorde`: **0 descuadrados** confiables · sin referencia confiable **de 4 a 1**
- [x] V4 Lote 30: 11.300 − 2.484 − 8.816 = **0 exacto**
- [!] ⚠️ **Pendiente (decisión de negocio):** id 132 (19.387 vs 19.187, 200 aves) — activo y sin ventas, la conservación no discrimina; necesita el documento físico de encasetamiento
- [!] ⚠️ **Pendiente (decisión de negocio):** ids 3, 4, 6, 8 — encaset 50.000 **y** `Inicio` de plantilla: los dos números son ficticios, cero movimientos. El detector no los ve porque compara `ih + im` sin mixtas

## Parte B — Liquidación de corridas anteriores: BLOQUEADA, no puede ir por migración
- [x] B1 🔴 Liquidar es una transacción de 5 pasos (estado + avance del ERP de granja + **copia congelada** + saldo + resumen). El código: *«sin copia no hay liquidación»*. Una migración SQL saltearía 4 de los 5
- [x] B2 🔴 El criterio «galpón con corrida posterior» alcanza 75 lotes e **incluye 22 de Panamá con 801.882 aves VIVAS** y seguimiento del 2026-08-03 (allá conviven varias corridas por galpón)
- [x] B3 Candidatos reales medidos — Ecuador: **39 con saldo 0** (grupo A) · 12 residuales < 1 % (602 aves) · 2 con saldo significativo (1.119 aves)
- [x] B4 Orden obligatorio verificado: el *Gate B1* impide editar `aves_encasetadas` de un lote liquidado ⇒ **corregir ANTES de cerrar** (por eso el lote 30 se corrigió primero)
- [!] ⏸️ **Esperando confirmación:** cerrar el grupo A (39 lotes de Ecuador) recorriendo el endpoint real de cierre. Irreversible sobre producción ⇒ requiere OK explícito sobre la lista
- [!] ⏸️ Grupos B y C (14 lotes con aves pendientes) — revisión aparte · Panamá **no se toca**

---

# Tracker — ItalJira: historias, tareas y tiempos fuera del módulo de Tickets

**Plan:** [`fase_de_desarrollo/italjira_gestion_historias_tareas_plan.md`](fase_de_desarrollo/italjira_gestion_historias_tareas_plan.md)
**Fecha:** 2026-08-07 · **Bloque propio — no tocar desde otras sesiones**

Pedido: sacar la gestión del área de desarrollo fuera de Tickets a un módulo nuevo **ItalJira**
(Tickets queda con «Mis solicitudes» y «Bandeja de gestión»), agregar el nivel **HISTORIA** encima de
las tareas (historia → tarea → subtarea/bug), permitir tareas nacidas en desarrollo (sin ticket), y
sembrar por migración el histórico REAL de lo ya desarrollado, asignado a `moiesbbuga@gmail.com`.

**Decisiones del usuario:** D1 = tabla nueva `historias` (3 niveles reales) · D2 = mover rutas a
`/italjira` con redirect · D3 = histórico mixto (historias por módulo + una tarea por plan de
`fase_de_desarrollo/`, con fechas reales de git).

## Fase 0 — Auditoría y plan
- [x] Modelo actual auditado: `Ticket` / `TicketTarea` (`ticket_id` **NOT NULL**) / `TicketTiempo`,
      servicios partial, 3 controllers, 6 menús en BD, rutas y páginas del front
- [x] Plan escrito con el DDL, las reglas de negocio y los casos de prueba
- [x] Decisiones D1/D2/D3 confirmadas por el usuario

### Resultado (07-ago-2026)

## Fase 1 — Backend: datos ✔
- [x] Entidad `Historia` + `HistoriaEstados` (alias explícito de `TicketTareaEstados`: un solo vocabulario en los dos niveles del tablero)
- [x] `TicketTarea.TicketId` a `long?` + `HistoriaId` · `Ticket.HistoriaId` · `TicketTiempo.TicketId` a `long?`
- [x] Blast radius del nullable: **solo 5 sitios** (2 proyecciones a DTO + 3 `Contains` en LINQ), todos ajustados con `!= null && …Value`
- [x] `HistoriaConfiguration` (FK `ON DELETE SET NULL`) + 3 configurations existentes + `DbSet<Historia>`
- [x] Migración M1 `20260807075318_AddHistoriasItalJira` idempotente, aplicada en local
- [x] ⚠️ EF arrastró al ModelSnapshot `seguimiento_diario_levante.venta_aves_hembras/machos` de OTRA sesión
      (`20260806235000` las creó por SQL dejando el snapshot atrás **a propósito**). Se **excluyeron
      del Up/Down** de M1 (ya existen en la BD) y se conservó la actualización del snapshot: es
      exactamente la reconciliación que esa migración anticipaba en su comentario

## Fase 2 — Backend: lógica ✔
- [x] `Application/Calculos/HistoriaCalculos.cs` — código correlativo, normalización, sellado de fechas
      (DELEGA en `TicketTareaCalculos`, no lo copia), avance, conteo, rango de roadmap y traducción
      `EstadoTrabajoDeCaso` (las 9 fases del caso al vocabulario de tareas)
- [x] **48 tests xUnit** nuevos (`HistoriaCalculosTests`), incluido el que impide duplicar `Reordenar`
- [x] `HistoriaDtos` (12 records) + `IHistoriaService` + `HistoriaService` (ancla + `Funciones/Backlog`)
- [x] `TicketTareaService.Historias.cs` — partial del MISMO servicio: `ticket_tareas` conserva un
      único escritor, y las dos vistas comparten proyección, reordenamiento y reglas de fecha
- [x] `ProyectarTareasAsync` generalizada a `IQueryable<TicketTarea>`: una sola fórmula para el panel
      del caso y para ItalJira
- [x] `ItalJiraController` (`/api/italjira`, 17 endpoints) + DI en `Program.cs`
- [x] Alcance: ItalJira **no filtra por empresa** (espeja la bandeja de gestión de tickets); la puerta
      es el permiso `tickets.gestionar` / `tickets.admin`, ya configurado en los roles

## Fase 3 — Menús ✔
- [x] Migración M2 `20260807150000_MenusItalJiraFueraDeTickets`: grupo `italjira` + **UPDATE EN SITIO**
      de las 4 vistas (conserva `role_menus`/`company_menus`/`menu_permissions` porque referencian
      `menu_id`) + menú nuevo `italjira.backlog` heredado de quien ya ve el Tablero
- [x] `tickets.admin` pasa a `italjira.configuracion`: la ruta deja de contener `admin` (AWS WAF)
- [x] Verificado en BD: Tickets con 2 items · ItalJira con 5 · 6 roles y 2 empresas conservados intactos

## Fase 4 — Frontend ✔
- [x] `features/italjira/`: routes, `models/historia.models.ts` (re-exporta lo compartido con tickets),
      `services/italjira.service.ts`, `funciones/` (2 puras + README), `components/historia-modal/`
- [x] Páginas MUDADAS con `git mv` (historia preservada): tablero, roadmap, panel, mis-asignados y
      admin-tickets → `configuracion` (clase `ItalJiraConfiguracionComponent`)
- [x] Página nueva **Backlog**: árbol historia → tarea → subtarea/bug, bandeja «sin historia»,
      indicadores, filtros, exportación a Excel (helper compartido) y modales de historia/tarea
- [x] `TareaModalComponent` REUTILIZADO (no se duplicó): el contenedor agrega la historia destino
- [x] Redirects de las 5 rutas viejas + ruta lazy `italjira` en `app.config.ts`
- [x] `changeDetection: Eager` explícito en los 2 componentes nuevos
- [x] `ToastService` / `ConfirmDialogService` / helper de Excel: cero `alert`/`confirm`/`XLSX` inline

## Fase 5 — Histórico real ✔
- [x] Fechas reales extraídas de git para los 198 planes de `fase_de_desarrollo/`
      (`--diff-filter=A` para el alta, `git log -1` para el fin) + título = H1 de cada plan
- [x] Curado en **20 historias por módulo**; TIPO derivado de la naturaleza del plan
      (129 TAREA · 32 BUG · 22 MEJORA · 20 DOCUMENTACION)
- [x] Migración M3 `20260807160000_SeedHistorialDesarrolloItalJira` (+ partial `.Seed.cs` con ~1.900
      líneas generadas): **20 historias / 203 tareas**, todo LISTO salvo «ItalJira», que queda
      EN_CURSO porque es esta misma entrega
- [x] Identidad POR EMAIL con fail-open (si el usuario no existe en el entorno, siembra 0 y no tumba
      el arranque). ⚠️ El int de auditoría **no es la cédula**: la de este usuario (3177120174) no
      entra en un `integer` — se toma el `created_by_user_id` que ya usan sus propios tickets
- [x] Idempotente: historias por `codigo`, tareas por `(historia_id, titulo)`

## Fase 6 — Validación ✔
- [x] `dotnet build` Infrastructure **0/0** y API **0/0** (a salida aparte: el `bin` del API lo tiene
      tomado un `ZooSanMarino.API.exe` **ajeno** en :5002 — proceso de otra sesión, NO se mató)
- [x] `dotnet test` **1.914 Application + 1 Domain**, todo verde
- [x] `yarn build` OK (único warning: bundle budget preexistente)
- [x] **Smoke HTTP** (backend propio :5499, JWT + X-Secret-Up minteados), 11 pasos: backlog inicial
      20/212/19 → crear historia → tarea → subtarea + bug (heredan historia del padre) → 3,5 h de
      worklog con `ticket_id` NULL → avance 33 % → 100 % → agrupar un caso real (4 trabajos, 75 %) →
      tablero 7 columnas y roadmap 2026-05-08→2026-08-07 → borrar la historia deja las 3 tareas
      VIVAS y sueltas → limpieza y estado final idéntico al inicial
- [x] **Smoke UI** (front :4300 + backend :5499, sesión inyectada en `localStorage.auth_session`):
      backlog con las 20 historias y sus tareas, bandeja con los 19 casos reales, modal de historia y
      de tarea abren/cierran **dos veces** sin colgarse, y las 5 rutas viejas redirigen
      (`/tickets/tablero|roadmap|panel|admin|asignados` → `/italjira/...`)
- [x] BD local devuelta a su estado exacto (20 historias del seed, 203 tareas agrupadas, 6 worklogs,
      0 tickets con historia); sin procesos huérfanos; `environment.ts` y `.claude/launch.json`
      restaurados byte a byte y el `bin/smoke-italjira` eliminado

## 🔴 Dos bugs que cazó el smoke (corregidos)

1. **El CHECK `ck_ticket_tareas_no_huerfana` rompía la propia bandeja de sueltas.** Exigía que toda
   tarea tuviera caso, historia o padre; pero una tarea con los tres en NULL es el estado LEGÍTIMO de
   «sin historia» — el que se crea con «+ Tarea suelta» y al que vuelve el trabajo cuando se borra su
   épica. Con el CHECK, `DELETE /historias/{id}` daba **500**. Se retiró de M1 (con `DROP … IF EXISTS`
   defensivo por si alguna base intermedia lo llegó a tener).
2. **El desplegable de columna de cada tarea mostraba siempre «Backlog».** `[value]` en el `<select>`
   (y también `[selected]` en la `<option>`) se aplican ANTES de que el `@for` registre las opciones.
   Fix: `[ngModel]` + `(ngModelChange)`, cuyo accessor reasigna el valor cuando las opciones terminan
   de registrarse. Verificado en pantalla: los 5 selectores pasaron de `BACKLOG` a `LISTO`.

Además, `GetSinAgruparAsync` / la bandeja del backlog dejaron de filtrar `ParentTareaId == null`: al
borrar una historia, sus subtareas quedaban invisibles en las tres pantallas. Ahora la bandeja trae el
árbol completo y el front lo anida.

## Fase 4 — §2.3 Barrido de sobregiro de aves (decisión del usuario: medir primero, sin tocar código)

Pregunta: si el seguimiento diario bloqueara «no cargar más bajas que aves disponibles», ¿cuántas
escrituras históricas quedarían rechazadas y en qué empresas?

- [x] B1 Detector `backend/sql/verificar_sobregiro_aves_postura.sql` (**solo lectura**, hermano de
      `verificar_paridad_saldo_engorde.sql`). Aritmética NO inventada: base y exclusión de filas
      copiadas de `fn_indicadores_levante_postura`; bajas = `SaldoAvesLevanteCalculos.BajasNetas`;
      producción sobre `fn_seguimiento_diario_produccion`. **Sin clamp** (el `GREATEST(0,…)` es lo que
      esconde el sobregiro)
- [x] B2 **Validación cruzada de la fórmula**: producción da saldo idéntico al `saldo_aves_h/m` que la
      propia fn expone en **5/5 LPP**; levante reproduce el **−460** del lote 123 exacto. La medición
      no es una fórmula nueva
- [x] B3 **RESULTADO — 1 sola fila en toda la BD local**:
      · **Levante**: 1 de 902 filas (11 lotes) — el ya conocido **lote 123 «LOTE 235A» de Demo**,
        03-ago-2026: **40 disponibles contra 500 bajas cargadas**. **Agroavícola Sanmarino: 0**
      · **Producción**: **0 de 933 filas** (5 LPP), 0 lotes con saldo final negativo
      · Alcance real del barrido: solo Sanmarino y Demo tienen datos de postura; ItalcolEcuador,
        ItalcolPanamá y Santa Reyes tienen **0 filas** ⇒ el bloqueo no los toca
- [x] B4 🔑 **Hallazgo de diseño que cambia la regla**: **4 lotes de levante y 1 LPP tocan saldo
      exactamente 0**, que es el cierre LEGÍTIMO (lote agotado). La regla tiene que ser
      **`bajas <= disponibles`**, NO `saldo > 0` — exigir `> 0` rompería el cierre normal de todos
      esos lotes. Y explica por qué el soft-check REQ-011b está doblemente mal: compara `saldo == 0`
      exacto ⇒ **salta en el caso legítimo y NO salta en el sobregiro real**
- [x] B5 Margen de operación: levante 6 lotes holgados / 4 en cero / 1 negativo; producción 3
      holgados / 1 con margen 1-50 / 1 en cero. Ningún lote «casi» sobregira ⇒ el bloqueo no
      generaría falsos rechazos por operación normal
- [!] **Pendiente de decisión**: re-correr el detector contra el dump de PROD antes de implementar
      (la BD local es un dump de fecha incierta y solo tiene 2 empresas con postura). Si prod
      confirma un número parecido, el bloqueo es de riesgo bajo

### Hallazgo lateral del barrido (NO tocado)
- [x] **Tres fórmulas distintas para el saldo de levante** — **CERRADO en V13/V13.7** (17ago26):
      resultaron ser **cuatro** consumidores y hoy las cuatro descuentan la venta. Texto original:
      `fn_indicadores_levante_postura`
      **NO descuenta ventas** (`r_aves_fin := v_aves_acum - mort - sel - err - tras_sal + tras_ing`),
      mientras que `fn_resumen_semanal_ra_pesadas_levante` y `fn_reporte_semanal_levante_extras`
      **sí** desde `b315612` / `20260806235000`, y `SaldoAvesLevanteCalculos.BajasNetas` también.
      Hoy no se nota (solo 2 filas en toda la BD tienen venta), pero viola «una sola fórmula por
      número» y va a divergir en cuanto se registren ventas de verdad.
      ✅ Verificado de paso: el espejo `fn_indicadores_levante_postura.sql` **sí está al día**
      (cuerpo idéntico a la definición viva) — no hay una segunda bomba de tiempo ahí

---

# Reporte Contable — Selección en RESUMEN + hoja de Movimientos de Huevo

Plan: [reporte_contable_resumen_seleccion_y_huevos_plan.md](fase_de_desarrollo/reporte_contable_resumen_seleccion_y_huevos_plan.md)
Origen: hallazgos 3 y 4 del correo de conciliación del lote K345
([análisis](fase_de_desarrollo/conciliacion_lote_k345_niza_iii_analisis.md) §8).

## Cambio 1 — columna Selección en la hoja RESUMEN
- [x] `ReporteContableResumenCalculos` (Application/Calculos): acumulado puro del resumen semanal
- [x] Reescribir `EscribirResumenSemanal` data-driven (12 columnas, Selección tras Mortalidad)
- [x] Tests xUnit del acumulado

## Cambio 2 — hoja MOVIMIENTOS HUEVOS en el Excel
- [x] `GenerarExcel(reporte, movimientosHuevos = null)` — parámetro opcional, sin romper el caller
- [x] Hoja espejo de la pantalla (POSTURA · HVTO FÉRTIL · HVO COMERCIAL · HUEVO DESECHO + movimientos)
- [x] `ReporteContableController.ExportarExcel` resuelve los movimientos y los pasa

## Validación
- [x] `dotnet build` sin errores ni advertencias nuevas
- [x] `dotnet test` verde
- [x] Smoke: exportar Excel de un lote con producción y cuadrar contra la BD

## Validación cruzada contra los informes de Verenice (lote S-369AB)
- [x] Recuperar el `.xlsm` de levante (viene truncado: sin central directory del ZIP)
- [x] Mapa de columnas del informe → campos de la aplicación (levante y producción)
- [x] Identificar qué campos del informe **no tienen dónde guardarse** en la app
- [x] Contrastar los datos cargados de S-369 contra el informe e informar diferencias

## Alineación de la carga masiva de LEVANTE (hallazgo de la validación contra Verenice)
Análisis: [validacion_informes_verenice_s369_analisis.md](fase_de_desarrollo/validacion_informes_verenice_s369_analisis.md)
- [x] `MigracionEsquemas.SeguimientoLevante`: Coef. Variación H/M, Observaciones Pesaje y los 4 de agua
- [x] `MigracionService.Historicos.cs`: lectura de las columnas nuevas + instrucciones de la plantilla
- [x] `fn_migracion_seguimiento_levante`: recordset + UPDATE + INSERT (espejo `.sql` y migración EF)
- [x] Migración `20260807190000_FnMigracionLevantePesajeYAgua` (+ Designer clonado)
- [x] Tests xUnit del esquema (9) y smoke de la fn en transacción revertida
- [x] **Descartado (era un dato mío equivocado)**: el modal de levante SÍ captura el C.V. — los controles
      se llaman `cvH`/`cvM` y el servicio los mapea a `CvHembras`/`CvMachos`
      (`SeguimientoLoteLevanteService.Mapeos.cs:173`). El hueco estaba solo en la carga masiva, ya cerrado
- [!] **Pendiente de decisión (técnica + costos)**: el corte levante/producción quedó en 24 semanas
      en S-369 y el informe de Verenice usa 25 ⇒ ~17.332 kg cambian de etapa en una conciliación

## Corte de etapa: bloqueo del doble conteo levante/producción
- [x] `CorteEtapaPosturaCalculos` (Application/Calculos): regla pura + mensajes, 10 tests xUnit
- [x] `SeguimientoLoteLevanteService.EnsureDiaSinAporteDeProduccionAsync` en el alta de levante
- [x] `ProduccionService.EnsureDiaSinAporteDeLevanteAsync` en el alta de producción
- [x] La regla mira el APORTE (consumo/bajas), no la existencia de la fila: el arrastre de huevos del
      levante crea filas de producción de solo huevos y esas NO deben chocar
- [x] Barrido de la BD: el traslape existe solo en K345 (15 días) ⇒ el guard no rompe nada existente
- [x] `dotnet build` + `dotnet test` (1.939 en verde)
- [!] **Pendiente, requiere OK explícito**: limpiar los 15 días traslapados de K345 (el guard impide
      nuevos, los existentes siguen ahí). Hay que decidir cuál de las dos filas queda antes de tocar datos

## Entrega
- [x] Respuesta final para costos con las correcciones aplicadas:
      [conciliacion_k345_respuesta_final_con_correcciones.md](fase_de_desarrollo/conciliacion_k345_respuesta_final_con_correcciones.md)

---

# Migraciones Masivas — retirar los tipos «Ventas / Movimiento de Aves / Movimiento de Huevos»

**Plan:** [`fase_de_desarrollo/migraciones_masivas_retiro_tipos_ventas_movimientos_plan.md`](fase_de_desarrollo/migraciones_masivas_retiro_tipos_ventas_movimientos_plan.md)
**Fecha:** 2026-08-07

Pedido: las ventas y los traslados ya se cargan **dentro** del seguimiento diario (hojas
`Movimientos Aves` / `Movimientos Huevos` de las plantillas de Levante y Producción), así que las
tres cajitas «Próximamente» de la Fase 3 sobran. Además el tile queda ilegible: el badge
«Sin permiso para carga masiva» (nowrap) aplasta la descripción a una palabra por línea.

## Auditoría previa (el código manda)
- [x] Los 3 enum members solo se referencian en `TipoMigracion.cs` + 1 test — no llegan a `ProcesarAsync`
- [x] `MigracionService.MovimientosAves/.MovimientosHuevos` son HOJAS del seguimiento, no estos tipos — no se tocan
- [x] `migracion_masiva.tipo` es varchar con `tipo.ToString()` ⇒ borrar miembros no corre ordinales
- [x] `VentaPolloEngorde` está implementado y en uso ⇒ queda (pendiente confirmación del usuario)

## Backend
- [x] `TipoMigracion.cs`: borrar `Ventas`/`MovimientoAves`/`MovimientoHuevos` del enum y del catálogo
- [x] `MigracionEsquemas.Para()`: mensaje del `_ =>` sin referencia a «Fase 3»
- [x] `MigracionService.Operaciones.cs`: comentario de cabecera + mensaje del `_ =>` de elegibles
- [x] `MigracionEsquemasTests.Para_TipoSinEsquema_Lanza`: usar un valor no definido del enum

## Frontend
- [x] `models/migracion.model.ts`: sacar los 3 del union `TipoMigracionCodigo`
- [x] `selector-tipo-migracion.component.ts`: sacar sus 3 íconos
- [x] `selector-tipo-migracion.component.ts`: layout del tile — metadatos (Fase + badge) debajo del texto

## Validación
- [x] `cd backend && dotnet build` — 0 errores; única advertencia CS8625 en `MigracionMovimientosAvesCalculosTests.cs:184`, PREEXISTENTE
- [x] `cd backend && dotnet test` — 1.992 Application + 1 Domain, 0 fallos
- [x] `cd frontend && yarn build` — 0 errores (solo el warning de bundle budget preexistente).
      ⚠️ Trampa propia: puse backticks dentro de un comentario CSS del bloque `styles` inline ⇒ cortaron
      el template literal y el compilador tiró «Failed to resolve styles at position 1 to a string».
      **Nunca usar backticks dentro de un `styles`/`template` inline.**
- [x] Layout verificado en el navegador con una página aislada que copia el CSS y el markup finales:
      ANTES reproduce el defecto de la captura (badge sobre el título, descripción en 1 palabra/línea);
      DESPUÉS: 6 tiles, descripción completa a 2 líneas y chips alineados al pie
- [x] Plantillas intactas por código: `MigracionService.Historicos.cs:137-144` sigue agregando las hojas
      `Movimientos Aves` (levante+producción) y `Movimientos Huevos` (producción); la aplicación en :851
- [x] Sin procesos huérfanos (no se levantó back ni front) · commit acotado (sin footer de atribución)
- [!] **Pendiente de decisión del usuario**: ¿el tile «Venta Engorde» (`VentaPolloEngorde`) también sale?
      Hoy queda: está implementado y en uso (fn `fn_migracion_venta_engorde` v2 con despachos), y la venta
      de engorde NO se registra desde el seguimiento diario

---

# Migraciones Masivas — permiso de POSTURA, tiles por permiso y módulo solo para Sanmarino

**Plan:** [`fase_de_desarrollo/migraciones_masivas_retiro_tipos_ventas_movimientos_plan.md`](fase_de_desarrollo/migraciones_masivas_retiro_tipos_ventas_movimientos_plan.md) (sección 7)
**Fecha:** 2026-08-07 · Continúa el bloque commiteado en `cbc922c`

Pedido: (a) en prod no se puede cargar postura «porque el permiso no existe»; (b) los cargadores sin
permiso deben OCULTARSE, no salir en gris; (c) el módulo debe quedar solo para Sanmarino Colombia.

## Diagnóstico (contra el dump de prod en la BD local)
- [x] `carga_masiva_postura` SÍ existe como fila (la creó `20260714115357`); lo que falta es la
      ASIGNACIÓN: ningún rol de Sanmarino la tenía. `Implementador Sanmarino Colombia` (3 usuarios)
      tenía solo `carga_masiva_pollo_engorde` ⇒ los tiles de postura salían bloqueados
- [x] `Menus_GetForUserAsync` arma el menú desde **`role_menus`** y NO lee `company_menus` ⇒ para
      ocultar el módulo hay que limpiar las DOS tablas, no solo la de empresa
- [x] `AddMigracionesMasivasMenu` lo sembró heredando de «Lotes» ⇒ quedó en las 5 empresas

## Backend — migración `20260807230000_RestringirMigracionesMasivasASanmarino`
- [x] Re-asegura que existan `carga_masiva_postura` y `carga_masiva_pollo_engorde` (NOT EXISTS)
- [x] `company_menus`: solo «Agroavicola Sanmarino» (borra el resto, garantiza la de Sanmarino)
- [x] `role_menus`: conserva solo roles de uso EXCLUSIVO de Sanmarino (un rol compartido se retira)
- [x] `role_permissions`: `carga_masiva_postura` al rol exclusivo de Sanmarino que YA tenía el de engorde
- [x] Todo por `companies.name` / `menus.route` / `permissions.key`, nunca por id (difieren local↔prod)
- [x] `Down` restaura el punto de partida reheredando de «Lotes»
- [x] Designer clonado del último migration; ModelSnapshot intacto (data-only)

## Frontend
- [x] `funciones/filtrar-tipos-visibles.funcion.ts` (PURA): descarta estructura, no implementados y
      líneas sin permiso. **Fail-closed**: lista de permisos vacía ⇒ no se ofrece nada
- [x] Página: `toSignal(permissions$)` + `tiposVisibles` = `filtrarTiposVisibles(...)` · `sinPermisos`
- [x] Aviso «No tenés permisos de carga masiva asignados» nombrando las dos claves exactas a pedir
- [x] Selector: queda 100% presentacional — se elimina `UserPermissionService`, `tienePermiso`,
      `mensajeSinPermiso`, `onClick` y los estilos `tile--locked` / `tile--soon` (código muerto)
- [x] `funciones/README.md` actualizado

## Validación
- [x] `cd backend && dotnet build` — 0 errores (solo la advertencia CS8625 preexistente)
- [x] `cd frontend && yarn build` — 0 errores (solo el bundle budget preexistente)
- [x] Migración simulada en la BD local **dentro de una transacción con ROLLBACK**: `company_menus`
      5 → 1; `role_permissions` +1 (rol 32); 2ª corrida seguida = todos los contadores en 0 (idempotente)
- [x] Filtro de `role_menus` probado rama por rama (sembrado y revertido): se retiran los roles de
      otra empresa, el rol SIN usuarios y el rol COMPARTIDO Sanmarino+Ecuador; se conservan solo los
      exclusivos de Sanmarino
- [x] BD local sin cambios (todo bajo ROLLBACK) · sin procesos huérfanos
- [!] ⚠️ **Efecto colateral a confirmar con el usuario**: «solo Sanmarino» le quita el módulo a
      **Santa Reyes** (2 roles que HOY tienen ambos permisos) y a **ItalcolPanama / Demo / Ecuador**.
      Si Santa Reyes debe conservarlo, hay que agregar su nombre a la lista de empresas habilitadas

---

# Tracker — Lote cerrado que absorbe el ciclo siguiente (KM 86) + ventana de mes actual en Inventario

**Plan:** [`fase_de_desarrollo/lote_cerrado_absorbe_ciclo_siguiente_y_ventana_mes_inventario_plan.md`](fase_de_desarrollo/lote_cerrado_absorbe_ciclo_siguiente_y_ventana_mes_inventario_plan.md)
**Fecha:** 2026-08-07 · Ticket de operación Ecuador (granja KM 86, lote 2601, Galpon-1 y Galpon-2)

Pedido: (a) la grilla de un lote que terminó en ABRIL muestra ingresos de julio; (b) que en Gestión de
Inventario solo se pueda cargar movimientos manualmente con fecha del mes actual.

## Diagnóstico (contra el dump de prod en la BD local :5433)
- [x] Captura identificada: `fn_seguimiento_diario_engorde(2)` reproduce edad y saldos byte a byte
- [x] Causa raíz: `rango_final.fecha_max` NULL (lote `Abierto` + saldo que nunca llega a 0) ⇒ grilla sin tope
- [x] Asimetría confirmada: v11/v12 excluyen ciclos ajenos en la APERTURA, nunca en el CIERRE
- [x] Alcance medido en las 2 empresas con engorde: solo 2 lotes invadidos (EC 2 y 86); **Panamá 0**
- [x] Los ingresos de julio son CORRECTOS (son del lote 2603): el error es a qué lote se los muestra
- [x] Plan escrito + decisiones D1-D4 confirmadas por el usuario

## Parte A — fn v14: corte por ciclo siguiente (la versión vigente era la v13, no la v12)
- [x] `backend/sql/fn_seguimiento_diario_engorde.sql` v14 (CTE `corte_ciclo_siguiente` + `LEAST` en `rango_final`).
      `LEAST` ignora los NULL ⇒ un lote sin ciclo posterior conserva su corte de v13 y uno activo sigue sin tope
- [x] Migración `20260808010000_FnSeguimientoEngordeV14CorteCicloSiguiente` (+ `.Fn.cs` con v14 y v13 verbatim,
      Designer clonado, ModelSnapshot intacto, `Down` = v13). Aplicada en local con `dotnet-ef` 10
- [x] `SaldoAlimentoEngordeCalculos.ResolverInicioCicloSiguiente` / `.ResolverFechaMaxGrilla` (puro, hermanas de
      las de v11/v12) + `CorteCicloEngordeCalculosTests` — 12 casos
- [x] Gate multipaís antes/después: **ItalcolPanama NO-OP** (los 6 de `dif_saldo_aves`/`dif_consumo` son un
      artefacto preexistente del script —claves (lote,fecha) duplicadas—, idéntico en la corrida de línea base)
- [x] Comparación fila a fila de los 140 lotes: **solo cambian 2**, lote 2 (31 filas) y lote 86 (1 fila);
      0 diferencias de saldo/aves/ingreso/consumo/documento en las filas que quedan
- [x] **0 filas con seguimiento real perdidas** (5.722 esperadas == 5.722 presentes): solo desaparecen
      filas movimiento-only. Los ciclos siguientes del galpón (72, 104) quedan intactos
- [x] `fn_cuadre_alimento_engorde` 22 → 22 y `fn_cuadre_aves_engorde` 1 → 1 (sin regresión)
- [x] Resultado: la grilla del lote 2601 / Galpon-1 termina el **2026-04-20 con 1.600 kg** (antes 2026-08-03 con 206.450)

## Parte B — Ventana de mes actual (D1 todo movimiento manual · D2 todas las empresas · D3 hasta hoy)
- [x] `Application/Calculos/VentanaFechaMovimientoInventarioCalculos.cs` (puro) + 12 tests xUnit.
      `DiaOperativo` = UTC−5 (CO/EC/PA sin DST): sin eso, las últimas 5 h del mes el servidor ya está en el
      mes siguiente y rechaza la fecha de HOY que el usuario ve en pantalla
- [x] Gate en el CONTROLLER (`ValidarVentanaFecha`) en las 5 puertas manuales: `POST /ingreso`, `POST /traslado`,
      `PUT /ingresos/{id}/fecha`, `PUT /traslados/{gid}/fecha`, `PUT /stock/{id}` (`FechaIngreso`)
- [x] **NUNCA en el service**: `RegistrarIngreso/Traslado/ConsumoAsync` los llaman la carga masiva, los 4 services
      de seguimiento (devoluciones al editar/borrar) e `InventarioGastoService`, que fechan histórico a propósito.
      `POST /consumo` no se toca (el front nunca lo llama)
- [x] Front: `funciones/ventana-fecha-movimiento.funcion.ts` (pura, espejo del backend) + `min`/`max` y leyenda en
      los 3 datepickers de movimiento (alta de ingreso, alta de traslado, ajuste de stock) y en el de edición de
      fecha del histórico + validación previa al submit en los 5 caminos
- [x] Los filtros «Fecha desde/hasta» del histórico NO se tocan (son filtros, no fechas de movimiento)

## Validación
- [x] `dotnet build` — 0 errores (solo la advertencia CS8625 preexistente)
- [x] `dotnet test` — **2.028 Application + 1 Domain, 0 fallos** (+24 nuevos)
- [x] `yarn build` (Node portable 22.23.1) — OK, solo el warning de bundle budget preexistente
- [x] Smoke HTTP real (back :5002 Dev, JWT + X-Secret-Up minteados) de las 5 puertas: mes anterior y mañana
      dan **400 con el mensaje de la ventana**; hoy pasa y llega al servicio (200, o el error de dominio esperado)
- [x] **BD local restaurada exacta**: el smoke escribió 3 movimientos, 2 registros de stock y corrió la fecha del
      movimiento 1 (doc 52968, granja 38 / G0035). Todo revertido; la fecha original (2026-02-07) se recuperó por
      los documentos correlativos vecinos (52912/52913/52925/52971, todos de esa fecha) y quedó **verificada por el
      gate**: la corrida posterior es idéntica a la del cambio (5.804 filas, 0 diferencias de valor)
- [x] Tablas temporales del gate eliminadas · sin procesos huérfanos (5002/5499/4200 sin listeners)
- [x] Commit acotado (sin footer de atribución). ⚠️ NO se commiteó
      `fase_de_desarrollo/ingreso_alimento_fecha_real_ingreso_inicial_ciclo_plan.md` (propuesta de OTRA
      sesión en curso) ni `.devpilot/events.jsonl`

### Aviso a la operación (fuera de alcance del código)
- [~] Los lotes 2601 de Galpon-1 (id 2) y Galpon-2 (id 12) siguen en estado `Abierto`: cerrarlos POR
      PANTALLA (liquidar es una transacción de 5 pasos, no va por migración)
- [x] El lote 12 arrastra apertura negativa (−9.020 kg) — **AUDITADO en V20** (17ago26): no es una
      apertura sino el saldo FINAL de su serie, y son **9.020 kg de consumo sin ingreso** que dejó la
      reconstrucción «Cuadre saldos Excel». No contagia al ciclo siguiente. **Completar la carga exige
      las remisiones físicas** ⇒ decisión pendiente en V20.4

---

# v16 de engorde — FASE 1 REVERTIDA (NO-GO del gate): la marca `para_proximo_ciclo` NO llegó a entregar

**Plan:** [`fase_de_desarrollo/marca_proximo_ciclo_rediseno_plan.md`](fase_de_desarrollo/marca_proximo_ciclo_rediseno_plan.md)
**Fecha:** 2026-08-09 · Bloque propio — no tocar desde otras sesiones
**Continúa** el bloque «Rediseño de la marca `para_proximo_ciclo` — v16 con ENTREGA al ciclo siguiente»
(Fase 0 = plan). Base: HEAD `d6aeccb`. **Esta sesión NO commitea** (lo hace el orquestador).

> ⛔ **CORRECCIÓN (18-ago-2026) — esto NO se entregó.** El título decía «FASE 1 IMPLEMENTADA» y los
> ítems de abajo describen archivos que **nunca llegaron a un commit**. No fue trabajo perdido ni
> historial corrupto: fue una **reversión deliberada tras el NO-GO del gate**. El propio commit que
> escribió estas líneas —`8424557`— se titula «deshabilita marcar alimento para el próximo ciclo hasta
> su rediseño» y tiene 4 archivos: el plan, dos componentes Angular y este tracker. **Cero backend.**
>
> Medido: `git log --all --diff-filter=A` por los paths exactos no devuelve nada · la fn del repo y la
> instalada en local siguen en **v15** · las fns de atribución y el índice
> `ix_lote_hist_para_proximo_ciclo` **no existen** · `__EFMigrationsHistory` local tiene 298 filas =
> 298 archivos, **0 huérfanas y ninguna `20260809*`**, o sea que NO se reprodujo el modo de falla
> SIGSEGV. **Riesgo de despliegue: cero** — lo que nunca estuvo en un commit nunca estuvo en una imagen.
>
> Los ítems quedan como `- [i]` en vez de borrarse: describen un intento real, y sus dos bloqueantes
> medidos (§🔴 más abajo) son justamente la razón por la que **no se recrean**. El rediseño correcto
> está en [`v16_engorde_atribucion_persistida_plan.md`](fase_de_desarrollo/v16_engorde_atribucion_persistida_plan.md);
> la investigación completa, en [`v12_5_1_migraciones_v16_ausentes_informe.md`](fase_de_desarrollo/v12_5_1_migraciones_v16_ausentes_informe.md).
> Cierra **V12.5.1**.

## Qué se escribió — y se revirtió sin llegar a un commit

- [i] ⛔ **F1.1** `backend/sql/fn_alimento_marcado_atribucion.sql` (NUEVO, 543 líneas) — dueño único de la
      atribución. Dos funciones: `fn_alimento_base_cedente_engorde(INT)` (el TOPE: último día visible
      del cedente + su saldo ahí) y `fn_alimento_marcado_atribucion(INT,TEXT,TEXT)` (el veredicto por
      movimiento) + el índice parcial `ix_lote_hist_para_proximo_ciclo`
- [i] ⛔ **F1.2** `fn_seguimiento_diario_engorde` **v16**: las 4 exclusiones de v15 revertidas a v14 y la
      marca convertida en dos términos **ADITIVOS** — `+kg_diferido` en la apertura del DESTINO y
      `−kg_diferido` como `traslado_salida_kg` del CEDENTE en su último día visible
- [i] ⛔ **F1.3** espejo C# `Application/Calculos/AtribucionAlimentoMarcadoCalculos.cs` (NUEVO) +
      `SaldoAlimentoEngordeCalculos` y `SeguimientoAvesEngordeCalculos` **revertidos a v14** (la marca
      ya no los toca) + 33 tests nuevos que CONSTRUYEN las topologías
- [i] ⛔ **F1.4** cruce de umbral: `SaldoAlimentoEngordeAplicador.RecalcularVecinosSiHayAlimentoMarcadoAsync`,
      llamado desde los dos services de seguimiento (carga masiva y formulario Ecuador)
- [i] ⛔ **F1.5** **el cuadre NO se tocó** — ni una línea de `fn_cuadre_alimento_engorde`
- [i] ⛔ **F1.6** 2 migraciones EF idempotentes con el SQL **byte a byte** de los `.sql`:
      `20260809120000_FnAlimentoMarcadoAtribucionEngorde` y
      `20260809120100_FnSeguimientoEngordeV16EntregaCicloSiguiente` (Down = v15 VERBATIM, Designer
      clonado del último real, **ModelSnapshot intacto**)
- [i] ⛔ `backend/sql/verificar_marca_proximo_ciclo.sql` (NUEVO, 566 líneas, LF) — el gate ejecutable

## El cambio de modelo, en una línea

`apert_mov`, `hist_full`, `hist_alimento`, `docs_por_fecha` y `fechas_universo` vuelven a la forma de
**v14 exacta**. La marca no quita nada de ninguna parte: agrega una **fila de entrega** al cedente y un
**crédito de apertura** al destino, por los mismos kg. Por eso R3 («invisible» nunca es una respuesta)
pasa de condición a vigilar a **propiedad estructural**, y una fila negativa es imposible por
construcción (un solo delta, en el último día, topado por el saldo propio).

## 🔴 DOS DEFECTOS QUE ENCONTRÓ EL GATE Y QUE NO ESTABAN EN EL PLAN

1. **La entrega recibida movía el CIERRE del destino.** `saldo_close` → `rango_final.fecha_max` se
   alimenta de la apertura; al sumarle el crédito, el ciclo destino cerraba más tarde, **ampliaba su
   ventana visible** y absorbía movimientos que no eran suyos. Medido: **37 probes con la conservación
   rota, hasta 14.320 kg**. Fix: `saldo_running` usa `apertura_alimento_base` (v14) y solo `pt_calc` y
   la columna expuesta usan la apertura efectiva. Es la misma asimetría que ya obliga a dejar la
   entrega fuera de `hist_full` (si entrara, movería la fecha donde ella misma se escribe).
2. **Diferir alimento que el ciclo cedente estaba consumiendo descuadra el ciclo activo.** En
   **43/G0055** el lote 86 (seg 02-jun→18-jul) cierra con **1.100 kg «de saldo»**, pero el stock físico
   del galpón (**4.540 kg**) coincide EXACTO con el saldo del ciclo activo 193 ⇒ esos 1.100 kg son un
   **fantasma contable** (la anomalía R2 que ya existe a escala: 24 de 84 liquidaciones congelaron con
   saldo > 0). Entregarlos movía `fn_cuadre_alimento_engorde` de **1 → 2 galpones descuadrados** en los
   17 probes de ese galpón — la firma exacta de la ronda 2. Fix: guarda nueva
   **`NEUTRO_DENTRO_DEL_CEDENTE`** (`d <= cedente.ult_seg` ⇒ la marca es inerte).

⚠️ **La guarda 2 se aparta del plan**: el caso de prueba **P4** (37/G0025, `id 6337` del 19-may dentro
del rango del lote 70) esperaba `DIFERIDO` y ahora da `NEUTRO_DENTRO_DEL_CEDENTE`. Se eligió el
invariante del cuadre por encima del veredicto escrito. **Consecuencia a decidir por producto:** el
feature queda acotado al ingreso que cae en el **HUECO entre ciclos** —que es el caso que el propio
plan identifica como el real (39 de 110 encasets 2026 de Ecuador, §9.3)— y NO cubre el alimento que
llega mientras el lote anterior sigue en seguimiento.

## Semántica final: 17 estados, ninguno deja kilos invisibles

`DIFERIDO` · `DIFERIDO_PARCIAL` · `IGNORADA_ANULADO` · `IGNORADA_NO_ENTRADA` · `NEUTRO_SIN_DESTINO` ·
`NEUTRO_SIN_CEDENTE` · `NEUTRO_CEDENTE_SIN_SEGUIMIENTO` · `NEUTRO_DESTINO_SIN_SEGUIMIENTO` ·
`NEUTRO_CONVIVENCIA` · `NEUTRO_DENTRO_DEL_DESTINO` · `NEUTRO_DESTINO_LIQUIDADO` ·
`NEUTRO_CEDENTE_LIQUIDADO` · `NEUTRO_YA_VISIBLE_EN_DESTINO` · `NEUTRO_DENTRO_DEL_CEDENTE` ·
`NEUTRO_CEDENTE_SIN_CIERRE` · `NEUTRO_FUERA_DEL_CEDENTE` · `NEUTRO_SIN_RESPALDO`

Tres estados **no anticipados por el plan** y por qué existen:
- `NEUTRO_CEDENTE_LIQUIDADO`: una foto congelada no se reescribe ⇒ la entrega no se escribiría y el
  destino recibiría kg sin contraparte (suma ≠ 0).
- `NEUTRO_YA_VISIBLE_EN_DESTINO`: si el movimiento ya entra a la apertura natural del destino (v11+v12),
  diferirlo lo contaría **dos veces**. Es lo que mantiene la conservación exacta en 0,00.
- `NEUTRO_DENTRO_DEL_CEDENTE`: el defecto 2 de arriba.

## Resultados del gate (BD local, dump tipo prod, todo en tx con ROLLBACK)

**G0 — identidad SIN marcas (necesaria, jamás suficiente).** `EXCEPT ALL` bidireccional, las dos
empresas, las 5 fns: **0 / 0 en todas**.
`fn_seguimiento_diario_engorde` 5.804 filas · `fn_cuadre_alimento_engorde` 61 · `fn_cuadre_aves_engorde`
172 · `fn_reporte_diario_costos_engorde` 224 · `fn_informe_semanal_pollo_engorde` 898.

**G1 — censo con la marca PRENDIDA.** `backend/sql/verificar_marca_proximo_ciclo.sql`,
**1.406 movimientos / 64 galpones**, tres fases:

| | Fase A (BD tal cual) | Fase B (sin congeladas) | Fase C (ingreso sintético en el hueco) |
|---|---|---|---|
| I1 filas negativas nuevas | **0** de 1.406 | **0** de 1.406 | **0** de 17 |
| I2 conservación, desvío máx. | **0,0000 kg** | **0,0000 kg** | **0,0000 kg** |
| I3 marcados que se vuelven invisibles | **0** | **0** | **0** |
| I4 documento en más lotes sin diferir | **0** | — | — |
| I5 cuadre | **no se movió** en ningún probe | — | **1 → 2 sin marca, 2 → 1 CON marca** |
| I6 convivencia (4 pares) | `dif_saldo` **0,00** con y sin marca | — | — |

- Línea base del cuadre re-medida: **61 filas, 1 descuadrado preexistente (Panamá, lote 182)**.
- Filas diarias ya negativas en HEAD: **91** — I1 mide filas negativas **nuevas**, no el total.
- **I7 rendimiento:** `fn_cuadre_alimento_engorde(NULL)` **0,62 s** (v16) vs **0,49 s** (HEAD) = **1,27×**
  (umbral 1,5×).
- Rastro al terminar: **0 marcas** en `lote_registro_historico_unificado` y en
  `inventario_gestion_movimiento`; **0 filas** del ingreso sintético.

**🔴 Lo que el censo NO puede demostrar, y por eso existe la fase C.** En el dump local **ningún
movimiento real** cae en la ventana que habilita `DIFERIDO` (después del último seguimiento del cedente
y antes de la ventana de apertura del destino): las fases A y B terminan con **0 probes DIFERIDO**, así
que por sí solas prueban que la marca *no rompe nada*, no que la entrega *funcione*. La fase C inyecta
el ingreso que falta (3.000 kg) en 17 pares secuenciales reales, bombeando también
`inventario_gestion_stock`, y compara el MISMO movimiento con el booleano en `FALSE` y en `TRUE`.
El único par con respaldo (**43/G0055, 86 → 193, 19-jul**) da exactamente lo diseñado:

| | sin marca | con marca |
|---|---|---|
| saldo final del cedente 86 | 4.100 kg | **1.100 kg** (entregó 3.000) |
| `apertura_alimento_kg` del destino 193 | 0 kg | **3.000 kg** |
| galpones descuadrados | **2** (el bug: el stock subió y el ciclo activo no lo ve) | **1** (= línea base) |
| conservación / filas negativas nuevas | — | **0,00 kg / 0** |

**G3 — tests C# que construyen las topologías.** `AtribucionAlimentoMarcadoCalculosTests` (NUEVO, 33
tests) con un helper que arma un **galpón completo** (ciclos con encaset, primer y último seguimiento,
congelación, ventana) y el estado del cedente como dato ⇒ se pueden expresar «destino sin seguimiento»,
«cedente sin respaldo», «destino liquidado», «ciclos que conviven». `dotnet test`: **2.168 pasan, 0
fallan**. Prueba de mutación registrada más abajo.

**Builds:** `dotnet build` Application **0/0**, Infrastructure **0/0**. `ModelSnapshot` sin tocar.

## Prueba de mutación (G3) — comentar cada guarda y ver el test en rojo

Se comentó cada guarda nueva, se corrió `dotnet test` y se verificó que los tests se ponen ROJOS.
Una guarda cuyo test sigue verde al quitarla no está testeada. **12 de 12 en rojo, 0 falsos verdes:**

| guarda comentada | resultado |
|---|---|
| R1 convivencia (`Conviven`) | 🔴 1 test falla |
| caso 10 · `d >= destino.PrimerSeg` | 🔴 1 |
| caso 5 · destino congelado | 🔴 1 |
| caso 5b · cedente congelado | 🔴 1 |
| Option F · ya visible en la apertura del destino | 🔴 1 |
| anti-abuso · `d <= cedente.UltimoSeg` | 🔴 1 |
| `d > baseCedente.FechaMax` | 🔴 1 |
| caso 3 · destino sin seguimiento | 🔴 1 |
| caso 9 · cedente sin seguimiento | 🔴 1 |
| caso 8 · solo entradas de alimento | 🔴 2 |
| caso 7 · movimiento anulado | 🔴 1 |
| tope · piso en 0 | 🔴 1 |

Script reproducible: el de la sesión comenta el fragmento, corre los tests y restaura el fuente.

## Estado dejado en la BD local

- [x] `fn_alimento_base_cedente_engorde`, `fn_alimento_marcado_atribucion`,
      `ix_lote_hist_para_proximo_ciclo` y `fn_seguimiento_diario_engorde` v16 **instalados**
- [x] `__EFMigrationsHistory` **NO se tocó a mano** (última sigue siendo `20260808130000`): las dos
      migraciones nuevas son idempotentes y las aplica EF sola al levantar el backend
- [x] **0 marcas** y **0 filas sintéticas**: todo el gate corre en transacción con `ROLLBACK`
- [x] Tablas temporales de línea base (`tmp_*`) eliminadas · sin procesos vivos

## Lo que NO entra en esta fase — REVALIDADO 16ago26

> ⚠️ Esta lista quedó **obsoleta por el NO-GO de abajo**: la reversión borró
> `AtribucionAlimentoMarcadoCalculos.cs`, `fn_alimento_marcado_atribucion.sql` y
> `verificar_marca_proximo_ciclo.sql` (verificado: los 3 archivos NO existen hoy). Todo lo que
> dependía de «el helper» murió con ellos. El hilo vivo es **«Lo que queda para el rediseño»**.

- [x] ✅ **Fase 2a — HECHA**: la columna «Próx. ciclo» **sí está pintada** en el tab Histórico
      (`inventario-historial-page.component.html:327` el `<th>`, `:362` el badge, `:369` el botón de
      quitar la marca). El tracker la daba por no hecha
- [x] ~~Fase 2b~~ · ~~Mensaje del endpoint~~ · ~~Decisión sobre `NEUTRO_DENTRO_DEL_CEDENTE`~~ —
      **sin objeto**: dependían del helper de la v16, que ya no existe. Vuelven a la mesa sólo si se
      retoma el rediseño
- [x] **Fase 3** — señalamiento de la anomalía R2. **CERRADA en el bloque V16** (17ago26), que la cita textual. Cuando se escribió seguía viva e independiente de la v16:
      columnas informativas en el cuadre, reporte de «liquidados con alimento sin trasladar»
      (24 de 84 lotes = 28,6 %, 111.821 kg) y el falso positivo del aviso de liquidación (fallback a
      stock de núcleo). Dato revalidado: `GET /api/CuadreAlimentoEngorde` **sigue sin un solo
      consumidor en el front** (0 archivos en `frontend/src` lo nombran)

## G4 — el que corrige NO declara GO

Esta sesión **escribió** la v16, así que **no declara GO**. El gate lo tiene que ejecutar y leer una
sesión que no la escribió: `psql ... -f backend/sql/verificar_marca_proximo_ciclo.sql`.

## VEREDICTO DE LA RONDA 4: **NO-GO — REVERTIDA** (y la marca queda DESHABILITADA en la UI)

El gate lo corrieron dos verificadores independientes (ninguno escribió la v16) y un juez sin permiso
de editar. **C1 = NO-GO · C2 = GO-CON-RESERVAS · juez = NO-GO.** La diferencia entre los dos: C1 abrió
la **foto congelada** de la liquidación y C2 no.

### Lo que SÍ mejoró respecto de las 3 rondas previas
- **Filas negativas nuevas por la marca: 0** (0 de 64 galpones reales, 0 de 75 pares sintéticos, 0 de
  2.210 movimientos). El invariante que hundió la ronda 3 quedó cerrado.
- **Cuadre vs HEAD: 0 movimientos empeoran** (A/B uno a uno: 0 peor · 729 mejor · 1.481 iguales).
- **Los tests MUERDEN**: 14/14 mutantes muertos, 0 sobrevivientes (el predicado viejo pone 4 en rojo).
- **R1 convivencia: CUMPLE** — 4 pares reales, 29 movimientos marcados, 113 filas, `EXCEPT ALL` 0 y 0.

### Por qué igual es NO-GO: el handoff se parte al liquidar
- 🔴 **Liquidar el CEDENTE esconde kilos**: tras una entrega válida (apertura destino 3.000, descuadre
  0,00), congelar el cedente —el procedimiento normal de R2— flipea a `NEUTRO_CEDENTE_LIQUIDADO`:
  apertura del destino 3.000→0, cuadre 0,00→−3.000, y la foto congelada del cedente sigue diciendo
  «Entrega al ciclo siguiente, salida 3.000». **3.000 kg reales sin ninguna tabla diaria viva.** (R3 ✗)
- 🔴 **Liquidar el DESTINO los duplica**: Σ galpón 8.640→11.640 (**+3.000 kg creados**) con
  `descuadre_kg = 0,00 en ambos estados` ⇒ **el detector es ciego**. HEAD no puede producir esto.
- **Causa raíz**: la atribución es un veredicto **recalculado en lectura** sobre estado mutable, pero la
  liquidación congela **un solo** extremo ⇒ el handoff se parte. El rediseño correcto es **persistir la
  atribución como hecho** (cedente, destino, kg, fecha) en el momento de marcar.
- Alcance: **0 movimientos DIFERIDO** en 1.680 marcados reales ⇒ la Fase 1 verde mide un no-op; el único
  par que alcanza el estado es justo el que rompen los dos bloqueantes.

### 🔴 HALLAZGO QUE OBLIGA A ACTUAR SOBRE `801b14f` (lo ya commiteado)
Bajo **HEAD/v15**, marcar un movimiento **rompe la conservación en 729 de 2.210 casos reales**
(hasta **37.467 kg** que desaparecen de toda tabla diaria) y HEAD produce **208 filas negativas**.
Motivo: los 4 guards de la fn (`hist_full`, `hist_alimento`, `docs_por_fecha`, `fechas_universo`) le
quitan el movimiento a **TODO** lote con seguimiento —incluidos los que **CONVIVEN** con el destino— y
en esos galpones ninguna apertura lo vuelve a tomar. **El checkbox ya estaba en producción.**

- [x] **Mitigación commiteada**: el checkbox del alta se oculta (`mostrarParaProximoCicloIngreso` ⇒
      `false`) y el historial solo permite **QUITAR** una marca existente, nunca poner una nueva
      (`puedeMarcarDestinoCiclo` exige `paraProximoCiclo === true`). La columna, el endpoint, el badge y
      la migración `20260808120000` **quedan intactos**: se apaga la puerta de entrada, no la feature.
      Regla del dueño del producto respetada: el alimento marcado nunca queda invisible ni sin corregir.
- [x] `yarn build` (Node portable 22.23.1) — 0 errores, único warning el de bundle budget preexistente

### Reversión (verificada)
- [x] Working tree: `git checkout -- backend` + borrados los untracked del intento
      (`fn_alimento_marcado_atribucion.sql`, `verificar_marca_proximo_ciclo.sql`,
      `AtribucionAlimentoMarcadoCalculos.cs` + test, migraciones `20260809120000_*` y `20260809120100_*`).
      **Se conservan** el plan `marca_proximo_ciclo_rediseno_plan.md` y este bloque del tracker
- [x] BD local: fn diaria reinstalada desde HEAD (`DROP` + `CREATE`, cambió el `RETURNS TABLE`) ·
      `fn_alimento_marcado_atribucion` y `fn_alimento_base_cedente_engorde` dropeadas ·
      `__EFMigrationsHistory` **NO se tocó** (las migraciones nuevas nunca se registraron) · el índice
      `ix_lote_hist_para_proximo_ciclo` **NO se tocó** (otra sesión lo estaba creando)
- [x] Verificado: 0 marcados · 0 fns auxiliares · 0 rastros en la fn (`DIFERIDO`/`NEUTRO_`/`cedente`) ·
      `apertura_alimento_kg` presente (= v15 correcta) · última migración `20260808130000` ·
      cuadre 61 filas / 1 descuadrado (el preexistente de Panamá)

### Lo que queda para el rediseño (con las 3 reglas ya definidas por el usuario)
- [!] **Persistir la atribución como hecho** en el momento de marcar (cedente, destino, kg, fecha), en
      vez de recalcularla en lectura. **La infraestructura ya entró INERTE el 18-ago (bloque V27, al
      final): tabla del hecho, triggers, cálculo dueño, 34 tests, mutación 17/17.** Pasa a `- [!]`
      porque el gate demostró que el mecanismo de ENTREGA **no puede dispararse nunca** —0 de 53 pares
      con hueco tienen un cedente que llegue vivo al día de la entrega— y el rediseño correcto
      (ampliar la ventana D4 del destino) **es una decisión de producto**: V27.1
- [x] Arreglar los 4 guards de la fn para que respeten R1 (un lote que **convive** con el destino debe
      seguir viendo el movimiento). **Cerrado el 18-ago por la FASE A (bloque V26, al final).** No se
      arreglaron: se **BORRARON**, que es lo que manda el plan nuevo — mientras no exista la
      atribución persistida, la marca no puede quitarle el movimiento a nadie. Medido: con la marca
      prendida, la v15 le sacaba **21 filas a Panamá** (la topología que CONVIVE) y 3 a Ecuador; la
      v16a, **0**
- [~] Fase 2 (visibilidad/corrección R3) · ~~Fase 3 (señalamiento de R2)~~ **CERRADA en V16** ·
      **F2a.1 HECHA el 18-ago (bloque V28): la columna «Próx. ciclo» en el tab Histórico.** Queda
      F2a.2 (smoke en pantalla: no tengo sesión para entrar a la app) y F2b (bandeja de
      reservados), que depende de la Fase B frenada en V27.1

---

# PWA — auditoría de acceso offline (menú muerto · primer ingreso · acciones operativas)

**Informe:** [fase_de_desarrollo/pwa_auditoria_acceso_offline_2026-08-12.md](fase_de_desarrollo/pwa_auditoria_acceso_offline_2026-08-12.md)
**Fecha:** 2026-08-12

## 1. Menú «Lote Reproductora» (id 9) — revisión aparte
- [x] `company_menus`: **ninguna empresa**. `role_menus`: **3 roles** (Auxiliar de Granja, Líder
      técnico, Director técnico) ⇒ **lo ven igual**, porque el sidebar sale de `role_menus`
- [x] 🔴 **Carga `SeguimientoLoteLevanteModule`**, no un módulo de reproductora: la entrada no abre
      lo que su nombre dice
- [x] La reproductora de **postura** no existe como pantalla de captura (no hay endpoint propio); el
      único `SeguimientoDiarioLoteReproductora` es el de **pollo engorde**, exclusivo de Panamá
- [x] **Para la PWA no hay nada que apagar**: no existe captura de reproductora de postura que
      pudiera encolarse. Mientras la ruta cargue levante, encola como levante, que es lo correcto
- [!] **Decisión del usuario:** quitar el menú a esos 3 roles hasta que el módulo exista, o corregir
      la etiqueta. Hoy un técnico entra por «Lote Reproductora» y carga levante sin darse cuenta

## 2. Primer ingreso y menú sin internet
- [x] ✅ **El menú sobrevive sin red**: vive en la sesión persistida, no se re-pide. `ensureLoaded()`
      cae a storage y `preloadMyMenu()` hace `catchError` al menú que ya tenía. Que `roles` esté
      excluido de la caché HTTP **no lo afecta**
- [x] ✅ Perder la red **no cierra la sesión** (B2), con tope duro de 16 h (D4)
- [x] 🔴 **El primer ingreso exige red** (`POST /auth/login` + reCAPTCHA en prod) ⇒ alistamiento:
      instalar y entrar una vez con señal, **por cada usuario**
- [x] ~~🔴 **El dispositivo guarda UNA sola sesión**~~ — **CERRADO por V34-V38** (18ago26, `9b6b157`
      → `6e4fe7f`). El llavero (`core/auth/llavero-sesiones.service.ts`) aparca cada sesión cifrada en
      su propio slot y activar uno es escribir su blob en `auth_session`: **el segundo operario ya
      entra sin red**, con su PIN. `auth_session` sigue siendo clave única a propósito —el multi-slot
      se construyó AL LADO— para no tocar interceptor, guards ni los ~190 componentes. Texto original:
      *No hay «los usuarios registrados» en plural: entra el último que hizo login. Dos operarios
      turnándose en la misma tablet ⇒ el segundo no puede entrar sin red.* Único caso donde la
      afirmación vieja sigue valiendo: **sin `crypto.subtle`** el llavero se apaga entero (fail-closed)
      y el dispositivo vuelve a ser de una sola sesión

## 3. Acciones operativas sin red — se CONSULTAN, no se guardan
- [x] Con caché de lectura (✅ ver / ❌ guardar): gastos de inventario · gestión de inventario ·
      historial · inventario de aves · movimiento de aves · movimiento pollo engorde (+Panamá) ·
      traslados · huevos · venta de aves
- [x] Con outbox (✅ guardar): **solo** las 4 capturas diarias (levante, producción, pollo engorde,
      reproductora engorde)
- [x] No es un olvido: es la decisión **D1** («ventas y movimientos a v2»). Los movimientos tocan
      stock y saldos, son de dos lados (origen/destino) y varios crean entidades que otras
      referencian ⇒ necesitan la clase `requiere_cuadre` **con emisor** y el grafo `client_entity_id`
- [i] **F4 (movimientos offline)** queda planteado, con sus prerrequisitos: A4, B1, B8 · ~~B10~~ cerrado en V23

## Corrección de una sospecha propia
- [x] `movimientos-huevos` **no** es un hueco de la lista blanca: es sub-ruta de `ReporteContable`,
      que está excluido a propósito (contabilidad). El verificador tenía razón

---

# 📍 PWA — PUNTO DE RETOMA (última actualización: 12-ago-2026)

> Bloque de continuidad. Una sesión nueva empieza acá: dice dónde quedó todo, qué está bloqueado
> y qué decisiones esperan al usuario. Los detalles viven en los bloques de arriba y en
> `fase_de_desarrollo/`.

## Estado real por fase

| Fase | Estado | Commits |
|---|---|---|
| F0.C higiene de entrega | ✅ | `76a2903` |
| F0.B seguridad de sesión | 🟡 **parcial** — B2, B3, B7, B4, B9 hechos · **faltan B1, B5(parcial), B6(parcial), B8** · ~~B10~~ cerrado en V23 (`56f7caa`) | `f139dfd`, `4616dfa` |
| F0.A integridad de datos | 🟡 **9 de 10** — falta **A4** (medido, con gate) | `44b2400`, `60d3125` |
| F1 shell instalable | ✅ | `8ecb7c6` |
| F2 consulta offline | ✅ | — |
| Alistamiento de campo (persist + D6) | 🟡 mitad de D6: falta **opt-in por rol y dispositivo** | `b8821cb` |
| Gate del borde (deploy front) | ✅ arreglado, **sin desplegar** | `6f410db` |
| **F3.1** captura offline levante | ✅ | `c44e0a4` |
| **F3.1b** hueco de UX | ✅ | `de3ea10` |
| **F3.2** captura producción | ✅ | `b681a50` |
| **F3.3** captura engorde (pollo + reproductora) | ✅ | `505c13b` |
| Auditoría de acceso offline | ✅ | `30c6865` |

## 🔴 Lo primero que hay que saber

**La PWA sigue SIN desplegarse.** Prod sirve el build del **07-ago** (`/version.json`) y
`ngsw.json` da 404. El fix del gate (`6f410db`) está en `main` y **no** en `main-produccion`; el job
del front corta antes del push a ECR. **Verificar con `curl` antes de depurar cualquier fantasma.**
Requiere push, que el usuario no autorizó todavía.

## Decisiones que esperan al usuario (bloquean trabajo)

- [~] **Merge `main` → `main-produccion`** para desplegar la PWA (arrastra migraciones; el contenedor
      tiene `RunMigrations=true`).
      ⚠️ **Revalidado 16ago26 — SIGUE SIN DESPLEGARSE, y la brecha creció**: `main-produccion` está en
      `cdd5561` y le faltan **25 commits** de `main`. Ya no arrastra sólo la PWA: también las
      migraciones de silos de Santa Reyes, la doble validación y los fixes de V7. Cuanto más se
      demore, más grande el salto de un solo deploy
- [x] ~~**Menú «Lote Reproductora» (id 9)**~~ — RESUELTO: migración
      `20260812080000_OcultarMenuLoteReproductoraPostura`. Etiqueta corregida a «Seguimiento
      Reproductora Postura» y **desasignado de todos los roles**; la fila del menú se conserva
- [x] ~~**Sesiones multi-slot por dispositivo**~~ — **HECHO en V34-V38** (18ago26), archivadas arriba.
      Quedó marcado como pendiente después de resuelto: es el mismo patrón que el commit `30fe5a2`
      documentó (V34.15 y V35.19 esperaban una decisión ya tomada). Lo que queda del multi-slot **no
      es código**: es el smoke **S-1** del bloque «PWA — lo único que falta probar en un equipo real».
      Texto original: *es lo ÚNICO que bloquea «varios usuarios sin internet». Hoy `auth_session` es
      clave única ⇒ un usuario por tablet*
- [~] **B8**: rotar las 4 llaves de `environment.prod.ts` — **el usuario debe generarlas**, no se
      inventan secretos de prod

## Próximos trabajos, en orden sugerido

1. **Desplegar** y hacer la verificación post-deploy + instalar en un Android real (nada de F1/F2/F3
   se probó nunca en producción)
2. ~~**B1**~~ — hecho en **V39**. Falta el paso que no está en el repo: subir
   `JwtSettings__DurationInMinutes` a 960 en la TaskDef de ECS, que es lo único que hace real la
   jornada de 16 h (el `appsettings` no manda ahí — V39.15)
3. **B5/B6** completos (~~B10~~ ya cerrado en V23), y **A4** con su gate de paridad
4. **F4 — movimientos offline** → **mapeado en
   [`fase_de_desarrollo/pwa_f4_mapeo_modulos_pendientes.md`](fase_de_desarrollo/pwa_f4_mapeo_modulos_pendientes.md)**:
   los módulos por nivel de dificultad, sus bloqueantes y el patrón a copiar
5. **Opt-in de D6 por rol y dispositivo** (flag en BD + registro de dispositivos)

## Trampas verificadas en esta sesión (no repetirlas)

- El smoke HTTP local necesita **JWT minteado + `X-Secret-Up` cifrado** (AES-256-CBC, PBKDF2 con salt
  `sanmarino-salt`, 10000 iter). El token dura **1 h**. Los `DELETE` por API dan **403** con ese JWT
- Al limpiar por SQL: el histórico unificado se marca **`anulado = true`**, nunca se borra
- El bundle del front está al borde del techo de error (se subió a **2.05 MB**); cualquier import
  eager nuevo lo rompe
- La carrera del índice único **no se reproduce** por HTTP: el `SELECT` previo gana. El índice está
  probado solo a nivel BD
- Levantar el backend bloquea los DLL: hay que **detenerlo antes de compilar**

---

# PWA — validación de estado y brecha real para salir a producción

**Fecha:** 2026-08-12 · **Tipo:** auditoría de cierre (no se escribió código funcional)
**Método:** todo lo de este bloque está **medido** — build, tests, `curl` contra el ALB y logs de
GitHub Actions. Nada se tomó del tracker sin verificarlo contra el código.

## 1. Lo que está construido y verde (medido hoy)

- [x] **Build del front**: `yarn build` con Node 22.23.1 → **0 errores**. Emite `ngsw.json`,
      `manifest.webmanifest`, `ngsw-worker.js` y `safety-worker.js`
- [x] **Bundle inicial: 2.00 MB contra un techo de error de 2.05 MB** ⇒ quedan **~50 kB**. El único
      warning es el de budget preexistente (1.5 MB de warning, 501 kB por encima)
- [x] **Tests del front**: `yarn test --watch=false --browsers=ChromeHeadless` → **288 verdes / 288**
- [x] **Tests del backend**: `dotnet test` (Application.Tests) → **2278 verdes / 2278**
- [x] **F1 shell**: SW registrado con `registerWhenStable:30000` y `enabled: !isDevMode()`;
      `PwaBarraEstadoComponent` montado en `app.component.html` con los 3 avisos y prioridad definida
- [x] **F2 consulta offline**: `verificar-lista-cacheable.js` → **79 endpoints, 50 cacheables, 29
      excluidos a propósito, 0 sin decisión**. El agujero de 23/78 de F2 está cerrado
- [x] **F3 captura offline**: los **4** tipos despachan (`levante`, `produccion`, `engorde`,
      `reproductora_engorde`); las **5** pantallas que guardan muestran el toast de pendiente
      (levante tiene 2 caminos). Idempotencia por `ux_sync_operaciones_client_op_id` en BD
- [x] **El envío automático SÍ está cableado**: `provideAppInitializer` instancia `SyncService` con
      `import()` diferido ⇒ el `effect` de reconexión queda registrado en el arranque. (Se sospechó lo
      contrario porque la barra lo carga lazy; **es falso**, verificado en `app.config.ts:83-88`)
- [x] **La cola sobrevive a la purga y al logout**: `purgarParticion`/`purgarTodo` tocan **solo**
      `STORE_CONSULTAS`; `STORE_OUTBOX` no se toca nunca
- [x] **D6 (mitad de datos)** y la persistencia de cuota, implementados y con tests

## 2. 🔴 El hallazgo que corrige el modelo del tracker

El tracker decía «la PWA sigue sin desplegarse». Es cierto, pero **incompleto**, y la diferencia
importa. Del run **31546059845** (merge del PR #66, 11-ago 23:19), medido con `gh run view`:

| Job | Resultado |
|---|---|
| Tests — Backend & Frontend | ✅ |
| **Backend — Build & Deploy** | ✅ **desplegado** (6m28s) |
| **Frontend — Build & Deploy** | ❌ **cortó en «Validar nginx y política de caché del borde»**, antes del push a ECR |

Las dos únicas fallas del gate fueron, textual: `FALLA ngsw.json ausente -> 404` y
`FALLA manifest.webmanifest aus. -> 404`. Todo el resto del borde (CSP, HSTS, immutable, no-cache,
reCAPTCHA) pasó.

⇒ **Prod corre hoy un frontend del 07-ago contra un backend del 11-ago.** Confirmado con `curl`:
`/version.json` = `2026-08-07T12:47:50.194Z`; `ngsw.json`, `manifest.webmanifest` y `ngsw-worker.js`
siguen en **404**. No es solo la PWA la que está detenida: **12 commits que tocan `frontend/`** ya
están en `main-produccion` sin llegar al navegador (F1, F2, alistamiento, los flags de empresa, la
programación de lotes sin `isPanama()`, el gasto contra lote programado).

- [x] `nginx.conf` **ya tiene** los `location =` de `ngsw.json`, `ngsw-worker.js`, `safety-worker.js`
      y `manifest.webmanifest` con `no-cache` y `application/manifest+json` ⇒ el bloque **C4** que
      agrega `6f410db` debería pasar. El gate corregido no va a rebotar por esto

## 3. 🔴 Riesgo #1: los 18 commits viven SOLO en este disco

`origin/main` está en `df72b08`; el working tree en `6980fa3`. **18 commits sin pushear**, de los
cuales 6 tocan el front. Ahí están **F3 completo** (las 4 capturas offline), `company_permissions` y
**el fix del gate que desbloquea el deploy del front**. Un disco que se rompe hoy se lleva la fase 3
entera. Esto es más urgente que desplegar.

## 4. Camino mínimo para que salga a funcionar

1. [ ] **`git push origin main`** — deja de haber un único punto de falla
2. [ ] **Merge `main` → `main-produccion`** (dispara el deploy). Arrastra **5 migraciones** que se
       aplican solas (`RunMigrations=true`); las 5 son idempotentes (`IF NOT EXISTS` /
       `WHERE NOT EXISTS` / `IS DISTINCT FROM`), verificado archivo por archivo
3. [ ] **Verificación post-deploy obligatoria** (ECS revierte en silencio):
       TaskDef ↔ imagen ↔ `/version.json` con `buildId` posterior al run, y `ngsw.json` → **200**
4. [ ] **Invariante de `company_permissions`**: correr la consulta de los permisos efectivos por
       usuario **antes y después** del deploy; el diff debe ser vacío. Es lo único de este lote que
       puede dejar gente sin acceso, y el gate de escritura ya rechaza con 400
5. [ ] **Avisar del menú**: `OcultarMenuLoteReproductoraPostura` **quita** «Lote Reproductora» a todos
       los roles que lo tuvieran en prod. Es intencional (cargaba levante), pero se nota en el sidebar
6. [ ] **Instalar en un Android real y hacer el smoke con la red cortada.** Nada de F1/F2/F3 se probó
       nunca fuera de local: F3 se validó **por HTTP**, no abriendo el formulario sin señal

## 5. Lo que falta para que funcione BIEN en campo (no bloquea el deploy)

- [x] ~~🔴 **Un solo usuario por dispositivo.**~~ — **CERRADO por V34-V38** (18ago26). El llavero de
      slots ya lo resuelve; lo que falta es probarlo con dos operarios de verdad (**S-1**). Texto
      original: *`auth_session` es clave única en `localStorage`: dos operarios turnándose en la misma
      tablet ⇒ el segundo no entra sin red. Exige sesiones multi-slot*
- [~] 🔴 **Alistamiento con red, por usuario y por dispositivo**: instalar, entrar una vez (login y
      reCAPTCHA exigen red) y **visitar las pantallas** que se van a usar, o la caché está vacía
- [x] 🟠 ~~La bandeja de rechazos no muestra el payload~~ — **cerrado 17ago26**: cada fila trae
      «Ver lo capturado» con el método, la URL y el JSON, más «Copiar captura» para pegarlo en
      soporte o rehacerlo a mano. El payload **ya estaba guardado** desde F3.1; sólo no se pintaba.
      El diálogo de descarte ahora nombra el tipo y la fecha de lo que se va a perder
- [x] 🟠 ~~`/diagnostico` no está en ningún menú~~ — **cerrado 17ago26**: link fijo en el pie del
      sidebar, junto a «Cerrar Sesión». **No** sale de `role_menus` a propósito: es la pantalla de
      rescate y hacerla depender de un permiso la volvería inalcanzable justo cuando hace falta
      (mismo criterio que su ausencia de `authGuard`)
- [x] 🟠 ~~`verificar-lista-cacheable.js` no está atado ni al Dockerfile ni al CI~~ — **cerrado
      17ago26**: corre en el job de tests del CI y **corta**. La deriva que el tracker anticipaba ya
      había pasado: al atarlo aparecieron **5 endpoints sin decisión** (`silocatalogo`, `farmsilo`,
      `galponsilo`, `lotesilo` de Santa Reyes + `seguimientovalidacion`). Los 4 de silo entran a la
      lista blanca (en esa empresa el silo ES la ubicación del alimento ⇒ es estructura) y
      `seguimientovalidacion` va a EXCLUIDOS (es un gate de negocio; cachearlo congelaría un flag que
      la empresa puede apagar, y el cliente ya cae a `SIN_PENDIENTES` sin red). 84 endpoints:
      **54 cacheables / 30 excluidos / 0 sin decisión**
- [x] 🟠 ~~**Aire en el bundle**~~ — **CERRADO en V22** (17ago26): 27 rutas de administración y CRUD
      pasaron a `loadComponent`. **Initial 1,85 MB → 967,45 kB**, el margen contra el techo de 2,05 MB
      pasa de ~210 kB a **~1,08 MB**, y el build sale **sin una sola advertencia** por primera vez.
      La PWA no pierde offline: `ngsw.json` sigue precargando los 179 chunks. Texto original:
      ⚠️ **cifra corregida 16ago26**: el build de hoy da **initial 1,84 MB**
      contra un techo de error de **2,05 MB** (`angular.json:62`) ⇒ quedan **~210 kB de aire**, no 50 kB.
      El riesgo sigue (un import eager grande rompe el build de prod) pero el margen es 4× el anotado.
      El warning de 1,50 MB se supera desde hace rato y es el único que sale en verde

## 6. Deuda conocida que viaja con esto (ya documentada, sigue abierta)

- [x] ~~**B1** revocación de sesión~~ — **CERRADO en V39** (18ago26). El `jti` viaja en el token y
      `sesiones_activas` es una lista BLANCA: sin fila no hay sesión. Cambiar la contraseña o dar de
      baja al usuario apagan sus sesiones, que hasta hoy no invalidaban nada. El **refresh token
      quedó afuera con argumento** (§1.3 del plan): no sirve offline. Texto original: *el más
      urgente: una tablet perdida no se puede revocar y la jornada offline dura 16 h*. Redacción
      honesta de lo que quedó: **una tablet perdida queda fuera del sistema en cuanto ve la red;
      lo que ya se llevó, se lo llevó** (§6.2)
- [~] **B8** rotar las 4 llaves de `environment.prod.ts` · ~~**B10** super admin por email → a datos~~
      **CERRADO en V23** (17ago26: eran 14 sitios, no 2; hoy es `users.is_super_admin`, revocable sin
      deploy) · **A4** self-heal al patrón aplicador · **B5/B6** fuera del camino de sync
- [i] **F4**: todo lo que no sean las 4 capturas diarias **se consulta pero no se guarda** sin red
      (inventario, movimientos, traslados, huevos, ventas). Mapeado en
      [`fase_de_desarrollo/pwa_f4_mapeo_modulos_pendientes.md`](fase_de_desarrollo/pwa_f4_mapeo_modulos_pendientes.md)

---

# Módulo «Gerencia»: Panel de control en solo-lectura global (permiso `tickets.indicadores`)

**Plan:** [`fase_de_desarrollo/gerencia_panel_control_permiso_lectura_plan.md`](fase_de_desarrollo/gerencia_panel_control_permiso_lectura_plan.md)
**Sesión propia — no tocar los bloques de arriba (silos / gastos, en curso en otra ventana).**

Un rol de gerencia debe ver **solo** el Panel de control de ItalJira, con los indicadores de TODOS
los casos, sin heredar nada de `tickets.admin`. No se podía por datos: el alcance global lo decidía
`AplicarFiltroTablero` (`TicketService.Gestion.cs:326`) contra `tickets.admin`, así que un rol con
`tickets.gestionar` veía el panel **en cero** (solo sus casos asignados).

## Backend

- [x] B1 `Application/Calculos/TicketAlcancePanelCalculos.cs` — `TieneAlcanceGlobal(permisos, vistaSoloLectura)`
- [x] B2 `AplicarFiltroTablero(filtro, bool vistaSoloLectura = false)` delega en B1 (tablero y roadmap sin tocar)
- [x] B3 `GetIndicadoresAsync` / `GetReporteAsync` pasan `vistaSoloLectura: true`
- [x] B4 Tests xUnit `TicketAlcancePanelCalculosTests` — **16 casos** (los 9 del plan, varios como `[Theory]`)
- [x] B5 Migración data-only `20260813175406_MenuGerenciaPanelControl`: permiso + grupo `gerencia` + `gerencia.panel`
      (`/gerencia/panel`, ruta PROPIA: `parent_id` es único y las migraciones localizan por `route`)
- [x] B6 En la misma migración: `menu_permissions` (OR con `tickets.admin`) + **`company_permissions`**

## Frontend

- [x] F1 `features/gerencia/gerencia.routes.ts` — `/gerencia/panel` reutiliza `PanelComponent`
- [x] F2 `app.config.ts` — bloque lazy `path: 'gerencia'` con `authGuard`
- [x] F3 `TICKET_PERMS.indicadores`
- [x] F4 Los 3 `RouterLink` del panel (Tablero / Roadmap / Lista) van tras `@if (puedeVerItalJira)`

## Validación

- [x] V1 `dotnet build` 0 errores + `dotnet test` **2403 passed / 0 failed** (las 2 advertencias son preexistentes, en otros archivos)
- [x] V2 `yarn build` OK (solo el warning de bundle budget preexistente)
- [x] V3 Migración aplicada en la BD local (era la única pendiente)
- [x] V4 Smoke **sin** el permiso: regresión intacta
- [x] V5 Smoke **con** el permiso: abre las 2 vistas de lectura y NADA más
- [x] V6 Backend local apagado, puerto 5002 libre

### Smoke HTTP real (backend local, JWT minteado + `X-Secret-Up` cifrado)

Gerente = usuario **sin** casos asignados; los 17 casos de la BD local son de otro resolutor. Así el
contraste es limpio: si el alcance no se abre, el gerente ve 0.

| escenario | `/indicadores` | `/reporte` | `/tablero` | `/roadmap` |
|---|---|---|---|---|
| A. gerente + `tickets.gestionar` (**HOY**) | 0 | 0 | 0 | 0 |
| B. gerente + `tickets.indicadores` (**NUEVO**) | **17** | **17** | **0** | **0** |
| C. admin + `tickets.admin` (referencia) | 17 | — | 17 | — |

A = el bug que motivó el trabajo. B = arreglado **sin** abrir el tablero ni el roadmap ni por URL
directa. C = sin cambios.

### Hallazgos

1. **`company_permissions` es fail-closed por empresa** (`CompanyPermissionCalculos.cs:152`, regla R1).
   Un permiso que no esté habilitado ahí NO viaja en el JWT aunque el rol lo tenga. Verificado tras
   aplicar la migración: quedó habilitado en Sanmarino, Demo, ItalcolPanama y Santa Reyes —
   **ItalcolEcuador NO**, porque no tiene `tickets.admin` ni `tickets.gestionar` habilitados. Si el rol
   de gerencia va a ser de Ecuador, hay que habilitarlo ahí desde la UI primero.
2. **Los 3 accesos rápidos del panel apuntaban a vistas de ItalJira.** Un gerente los habría visto y
   el `permissionGuard` lo habría rebotado a `/home`. Ahora se ocultan sin permiso de gestión.
3. **`GetResolutoresAdminAsync` no tiene gate** (`TicketService.cs:414`) ⇒ la barra de filtros del
   panel funciona completa para el rol nuevo, sin abrirle nada más.
4. ⚠️ **Trampa del entorno:** había un `mint.py` viejo de otra sesión en `/tmp` que emitía un token
   fijo (Santa Reyes, sin claims `permission`) e ignoraba los argumentos. Los tres escenarios daban 0
   y parecía un bug del código. Los scripts de smoke van al scratchpad con nombre propio.

## Cierre

- [x] Commit sin footer de atribución (autor único moisesmurillo)
- [~] **Post-deploy manual** (no lo hace la migración, a propósito): en Roles y Permisos crear/elegir
      el rol de gerencia → asignarle **solo** `tickets.indicadores` → asignarle el menú
      **Gerencia › Panel de control**. Hasta entonces el módulo no lo ve nadie.

---

# Bitácora de sesiones — agosto 2026 (W/I · V3 · V5 · V7 · V8)

Bloque acumulativo: cada sesión agrega su sección `##` al final. Sólo quedan las que
tienen trabajo abierto. **V7 se conserva aunque esté cerrada porque V8 la cita**
(el lote 168 y su baseline salen de los smokes de V7).

## W/I · Vacunación viva + Implementación con firma en Home (15ago26)

Planes: [vacunacion_cronograma_vivo_plantillas_plan.md](fase_de_desarrollo/vacunacion_cronograma_vivo_plantillas_plan.md) ·
[implementacion_italjira_firma_home_plan.md](fase_de_desarrollo/implementacion_italjira_firma_home_plan.md)

Reporte: los combos de Vacunación e Implementación se quedaban en «Cargando…». **No era el backend**:
son los únicos 13 componentes del repo (de 222) que omitían `changeDetection` ⇒ en Angular 22 eso es
OnPush ⇒ el `finally { cargando = false }` tras el `await` nunca repintaba. Encima, el usuario pide
que el cronograma de vacunación se programe por empresa/línea/raza y avise cuándo toca, y que el plan
de implementación viva en ItalJira y termine con una firma manuscrita del usuario en Home.

### F0 — Fix de la demora (cerrado)
- [x] F0.1 Auditoría: 208/222 componentes declaran `changeDetection`; los 13 sin declarar son Vacunación (5) e Implementación (8)
- [x] F0.2 `ChangeDetectionStrategy.Eager` explícito en los 13 (convención del repo: 184 Eager / 24 OnPush)
- [x] F0.3 `yarn build` con node portable 22.23.1 — 0 errores (único warning: budget preexistente)
- [x] F0.4 Smoke visual: Cronograma pinta las **29 granjas** (antes «Cargando granjas…») y la cascada carga los lotes de MANGOS al instante; Registro y Reportes igual; Planes muestra «1 de 1 cronogramas»
- [x] F0.5 Gate anti-regresión: `frontend/scripts/verificar-change-detection.js` — cuenta paréntesis
      para leer el literal del decorador (una regex se corta con los `template`/`styles` inline),
      exige `changeDetection` explícito y **rechaza `Default`** (deprecado en v22). Atado al job de
      tests del CI y a `make gates-front`. Hoy: **223 componentes, 0 faltantes, 0 con `Default`**;
      probado por mutación (un componente sin la propiedad ⇒ exit 1 nombrando archivo y línea)

### W1 — Plantillas de vacunación por empresa/línea/raza
- [x] W1.1 Tablas `vacunacion_plan_plantilla` + `_item` + migración EF **idempotente** (V9.6)
- [x] W1.2 `VacunacionPlantillaCalculos` (raza exacta > comodín > `vigente_desde` > id) + **28 tests xUnit** (V9.6)
- [x] W1.3 CRUD backend + permisos — **cerrado 17ago26**, ver bloque «V10 · Vacunación W1.3 + W1.4»
- [x] W1.4 Front: pantalla de plantillas (levante/producción por semana, engorde por día) — **cerrado 17ago26** (mismo bloque)

### W2 — Materializador a los lotes — **CERRADO 17ago26** (`f2794c6`, bloque «Vacunación W2 — el materializador»)
- [x] W2.1 `origen_plantilla_item_id` + `generado_automatico` en `vacunacion_cronograma_items`
- [x] W2.2 `VacunacionMaterializadorCalculos` puro (faltantes / actualizables / preservados) + tests
- [x] W2.3 Servicio idempotente; **nunca** toca ítems ya aplicados ni los creados a mano
- [x] W2.4 Enganche al encaset + botón «aplicar a lotes activos» + preview de impacto antes de guardar

### W3 — Bandeja de pendientes y novedad fuera de rango — **CERRADO 17ago26** (bloque «Vacunación W3», al final del tracker)
- [x] W3.1 `GET /api/VacunacionRegistro/pendientes` (SQL, scoped por usuario)
- [x] W3.2 Front: la novedad se despliega sola al aplicar fuera de franja (hoy el back ya la exige y devuelve 400)
- [x] W3.3 Rótulo «Fuera de rango» con días de desviación (sin estados nuevos en BD)

### W4 — Scoping por núcleo/galpón/lote — **CERRADO 17ago26** (bloque «Vacunación W4», al final del tracker)
- [x] W4.1 `fn_vacunacion_filter_data` **y** `fn_vacunacion_pendientes` respetan
      `user_farms.restrict_locations` + `user_farm_scopes` (fail-closed). Las dos subieron juntas
- [x] W4.2 Mismo scoping en reportes de cumplimiento + smoke con usuario restringido (14/14).
      De paso cazó que `GET /cumplimiento` reventaba en runtime para todas las empresas

### I1..I5 — Implementación (elegido por el usuario como primera entrega)
- [x] I1.1 Columnas `implementacion_planes.historia_id` + `implementacion_tareas.ticket_tarea_id` (entidad, configuration, migración idempotente `20260815000000`, snapshot y Designer)
- [x] I1.2 **HECHO 17ago26** (ver V9.5): `POST /api/Implementacion/planes/{id}/italjira` crea la
      historia del plan y una tarea del tablero por punto. **Explícito, no automático** —hay entregas
      que no son trabajo de desarrollo y llenarían el backlog de épicas muertas— e **idempotente**
- [x] I1.3 **HECHO 17ago26**: los 4 sitios que mueven una tarjeta de columna reflejan el estado en
      el punto enlazado, **dentro de la misma transacción**. Un punto ya CONFIRMADO no se toca nunca
- [x] I2.1 `ImplementacionCalculos.TareaHabilitadaParaFirmar` (fail-closed) + tests xUnit
- [x] I2.2 `FirmarAsync`/`RechazarAsync` rechazan un punto todavía programado (backend, no solo UI)
- [x] I2.3 Front: el modal muestra el punto en lectura y «Aún no te toca firmar» en Mis tareas
- [x] I3.1 Columnas `firma_imagen` · `firma_tipo` · `contenido_hash` · `firmado_user_agent` · `firmado_ip` + CHECK del tipo
- [x] I3.2 `ValidarFirmaImagen` (PNG, canvas en blanco, base64 corrupto, tope de peso) + `CalcularContenidoHash` SHA-256 server-side + tests
- [x] I3.3 `FirmaCanvasComponent` compartido (pointer events: dedo, mouse y lápiz; export 600×200 con fondo blanco)
- [x] I3.4 El modal de firma pide trazo + nombre; el detalle muestra el trazo y avisa si el punto se editó después de firmado
- [x] I4.1 `GET /api/Implementacion/mis-pendientes-firma` (solo pendientes ya realizados, scoped al usuario)
- [x] I4.2 `PanelPendientesFirmaComponent` desplegable en el inicio (no se dibuja si no hay nada)
- [x] I5 **HECHO 17ago26**: el modal de participantes filtra por **rol de la empresa activa** y
      ofrece «marcar los N visibles». La empresa ya estaba: `SetParticipantesAsync` rechaza usuarios
      de otra (`UserCompanies`)

### Cierre de la entrega I2/I3/I4
- [x] Z.1 `dotnet build` 0 errores (el único warning, CS8602 en `SeguimientoLoteLevanteService.Crud.cs:217`, es preexistente y de un archivo que nadie tocó) + `dotnet test` **2572 en verde** + `yarn build` 0 errores
- [x] Z.2 Migración `20260815000000` aplicada en la BD local por psql — **sin** disparar las migraciones pendientes de la sesión paralela (trabajo ajeno sin commitear)
- [x] Z.3 Smoke HTTP del flujo: **14/14** (gate rechaza firmar un punto programado · aparece al completar · canvas en blanco rechazado · firma manuscrita persiste · el hash detecta la edición posterior · sale de pendientes al firmar)
- [x] Z.4 Smoke UI real: firma dibujada con eventos pointer en el canvas → PNG de **17 KB**, `firma_tipo=manuscrita`, hash, user-agent e IP guardados; el panel del inicio desaparece al quedar sin pendientes; el trazo se ve en el historial
- [x] Z.5 Datos de prueba borrados (BD vuelve a 1 plan / 11 tareas / 0 firmas) · backend apagado · `:5002` libre

### Hallazgo lateral (preexistente, fuera de esta entrega)
- [x] X.1 **CERRADO 17ago26** (ver V9.3): `DeletePlanAsync` y `DeleteTareaAsync` borran en cascada
      con el **mismo `deleted_at`** para todo el árbol. La cascada no es cosmética: mientras las hijas
      quedaban vivas, que no hubiera fuga dependía de que **cada consulta futura** se acordara de
      encadenar el filtro del padre

---

## V3 · `ENGORDE_EC` apuntaba a una tabla fantasma (15ago26)

Plan: [fix_engorde_ec_tabla_compartida_plan.md](fase_de_desarrollo/fix_engorde_ec_tabla_compartida_plan.md)

Validando los 14 flags módulo por módulo apareció que el formulario de engorde hace su CRUD contra el
controller **Ecuador** (que escribe en la tabla compartida y reserva como `ENGORDE_EC`) pero pide
pendientes y valida como `'ENGORDE'`. Las 3 ramas `ENGORDE_EC` de `ValidacionSeguimientoService` leen
`seguimiento_diario_aves_engorde_ecuador`, tabla que **no existe** aunque su migración figure aplicada.
Con el flag ON (ItalcolPanama): guardar revienta (42P01) y validar marcaría `validado=true` sin
descontar nada, dejando la reserva activa para siempre.

- [x] V3.1 Las 3 ramas `ENGORDE_EC` leen `_ctx.SeguimientoDiarioAvesEngorde` (tabla compartida)
- [x] V3.2 `ModuloSeguimiento.Canonico()`: la reserva se guarda y se busca con `ENGORDE`, así separar por Ecuador y validar por Colombia se encuentran (colapsar la tabla no alcanzaba — `ValidarAsync` filtra por `OrigenModulo`)
- [x] V3.3 `AsegurarPuedeRegistrarDiaAsync` en reproductora (único de los 5 sin bloqueo por vencidos)
- [x] V3.4 Doc en `SeguimientoDiarioAvesEngordeEcuador`: entidad sin uso, la tabla partida no es la fuente
- [x] V3.5 Tests xUnit del literal canónico (7 nuevos): colapsa, no toca al resto, misma clave separando y validando, y no invalida el literal en la API
- [x] V3.6 Ticket ItalJira `20260815140000` data-only: historia `LISTO` + caso `SOLUCIONADO` con 6 tareas y horas, **y caso aparte `EN_ANALISIS`** por el hallazgo que no se resolvió. Dry-run en transacción revertida: OK; 3 corridas seguidas no duplican nada
- [x] V3.7 `dotnet build` 0 errores 0 warnings · `dotnet test` **2581 en verde** (2574 + 7) · ModelSnapshot intacto · sin cambios en el front (el `'ENGORDE'` que ya mandaba pasa a ser correcto)
- [x] V3.8 Smoke HTTP con el flag ON (ItalcolPanama, lote 168 `60 - 3` / galpón G0490), backend ya reiniciado:
  - `GET /SeguimientoValidacion/configuracion` → `requiereValidacion: true`
  - `GET /ENGORDE_EC/pendientes` → **HTTP 200** devolviendo `modulo: ENGORDE`. **Antes moría con 42P01**
  - `POST /SeguimientoAvesEngordeEcuador` (el camino del front) → creó el id 11595 **sin reventar**; la reserva quedó con `origen_modulo = ENGORDE` (canónico) pese a entrar por Ecuador; stock 10609,560 y aves 8523 **sin moverse**; `validado = f`
  - `POST /ENGORDE/{id}/validar` (el módulo que manda el front) → `itemsAplicados 1 · kgAplicados 250,000 · avesDescontadas 5`. **Antes devolvía ceros y marcaba validado igual.** Stock 10609,560 → 10359,560 y aves 8523 → 8518; reservas a `APLICADA`
  - `DELETE` sobre un registro validado → rechazado; `desvalidar` devolvió los 250 kg y las 5 aves; `DELETE` tras des-validar liberó la reserva
  - **Base restituida al baseline**: stock 10609,560 · aves 8523 · 0 reservas activas · 42 seguimientos en el lote
- [x] V3.9 Migración `20260815140000` aplicada en el reinicio: **TK-2026-000167 SOLUCIONADO** · **TK-2026-000168 EN_ANALISIS** (el disponible) · **HIS-2026-0024 LISTO** con 6/6 tareas
- [x] V3.10 Commit

### Hallazgo pendiente (NO entra en esta entrega)
- [x] V3.X «Disponible = stock − reservas activas» — **CERRADO 17ago26 (revalidado contra el código)**.
      Las dos mitades quedaron resueltas por entregas posteriores, y el checkbox se había quedado sin
      marcar: **AVES** tiene hoy **dos** consumidores reales (`TrasladoAvesDesdeSegService.cs:73` y
      `MovimientoAvesService.Postura.cs:71`, este último el guard de la venta que agregó V9.2);
      **ÍTEMS** ya no exige decidir nada porque `ReservadoPorItemAsync` **fue eliminado** (V5.Y /
      V9.2.6) — `GetStockAsync` resuelve el disponible inline y con el silo en la clave, y la
      decisión pendiente («¿restar a `Quantity` o campo aparte?») la tomó V5: campo `Disponible`
      derivado en el DTO, leído por el front desde V5.6/V5.7. Verificado con `grep`: cero
      referencias vivas a `ReservadoPorItemAsync` fuera de un comentario y de dos migraciones

---

## V5 · Disponible = stock − reservas activas — TK-2026-000168 (15ago26)

Plan: [disponible_menos_reservas_inventario_plan.md](fase_de_desarrollo/disponible_menos_reservas_inventario_plan.md)

El hallazgo que V3 dejó abierto. Decisión del usuario: **campo `Disponible` en el DTO + front**, no
restarle la reserva a `Quantity` (que es la existencia física que operación concilia). `DisponibleKg`
pasa a ser **derivado** porque hay 9 sitios que construyen el DTO a mano y ninguno lo llenaría.

### ⚠️ Corrección del hallazgo: el backend YA lo hacía

Al abrir el código apareció que V3 lo había diagnosticado a medias. Es cierto que
`ReservadoPorItemAsync` y `ReservadoDeAvesAsync` no tienen un solo llamador, pero de ahí se concluyó
mal que el disponible no se calculaba: **`GetStockAsync` ya lo resuelve inline** —una consulta
agrupada, con el silo en la clave, normalizando núcleo/galpón y contando solo reservas `ACTIVA`— y ya
llenaba `ReservadoKg`/`DisponibleKg` con `ReservaSeguimientoCalculos.DisponibleAlimento`.

Así que V5.2 a V5.5 **no se hacen**: escribirlos habría sido una segunda implementación del mismo
número, justo lo que prohíbe *Una sola fórmula por número*. Lo que faltaba de verdad era el **front**,
que nunca leyó esos dos campos.

- [x] V5.1 `DisponibleKg` deja de ser parámetro posicional y pasa a propiedad calculada `Quantity − ReservadoKg`. **Era necesario**: los 9 sitios que arman el DTO a mano para ingreso/traslado/consumo lo dejaban en 0, y en cuanto el front lo leyera habrían dicho «no hay nada» sobre un galpón lleno
- [x] V5.2–V5.5 **descartados** (ver corrección de arriba). Se borró el `ReservaUbicacionCalculos` que ya había escrito
- [x] V5.6 Front: `reservadoKg`/`disponibleKg` + helper `saldoComprometible()` (cae a `quantity` si el campo no viene) · los 4 modales y `agruparStockPorItemSilo` acumulan el disponible
- [x] V5.7 Front: columnas **Separado** y **Disponible** en gestión de inventario, solo si alguna fila tiene reserva (campo calculado al cargar, no un getter que recorra el arreglo en cada ciclo); disponible negativo en rojo y sin recortar
- [x] V5.8 `dotnet build` 0 errores · `dotnet test` verde · `yarn build` con node 22.23.1, único warning el de budget preexistente
- [x] V5.9 Smoke sobre build propio en `:5501` (el `:5002` del usuario estaba caído y hay sesión paralela):
  - flag ON, tras guardar 400 kg sin validar → `quantity 10609,560` **intacto**, `reservado 400`, `disponible 10209,560`
  - tras borrar el registro → `reservado 0`, `disponible 10609,560`
  - flag OFF (Sanmarino, 20 filas) → **0 filas** con `disponible ≠ quantity` y **0** con `reservado ≠ 0`
  - base en el baseline · `:5501` liberado
- [x] V5.10 Commit

### Sigue abierto (NO entra acá)
- [x] V5.X El lado de **AVES** — **CERRADO 17ago26** (ver V9.2). El diagnóstico por nombre de archivo
      («0 archivos `*Venta*.cs`…») era **incompleto**: en engorde la venta ya está cubierta por otro
      mecanismo (`registradas − aplicadas`), y el hueco real estaba en **postura**, donde la venta
      descuenta el maestro con un `Math.Max(0, …)` que se come el sobregiro en silencio
- [x] V5.Y **`ReservadoPorItemAsync` eliminado** (17ago26): `GetStockAsync` ya resuelve el disponible
      inline y **con el silo en la clave**; el método muerto agrupaba SIN el silo ⇒ no era sólo
      redundante, en Santa Reyes habría devuelto otro número para el mismo ítem. En su lugar quedó el
      comentario que explica dónde vive la fórmula. `ReservadoDeAvesAsync` **se conserva**: tiene dos
      consumidores (traslados y, desde hoy, la venta de postura)

---

## V7 · Bugs de la doble validación por empresa + validación en las 5 (16ago26)

Plan: [doble_validacion_bugs_por_empresa_plan.md](fase_de_desarrollo/doble_validacion_bugs_por_empresa_plan.md)

Retoma el V6.X que quedó abierto: *el camino con el flag ON en postura nunca se ejecutó*. Auditoría de
7 superficies + verificación adversarial (65 agentes): el hueco no era solo de prueba, había defectos
reales esperando ahí.

### V7.0 — `main` no compilaba
- [x] El commit anterior (`bebac18`) dejó `TrasladoAvesDesdeSegService.cs` usando `ReservaSeguimientoCalculos` y `ModuloSeguimiento` **sin el `using`**. `dotnet build` fallaba con 10 errores CS0103/CS8130. El reporte de «0 errores, 2602 tests en verde» de esa sesión no es reproducible desde el commit

### H1 — El `pais_id` de la reserva no era el resuelto ⇒ validar no descontaba alimento
- [x] V7.1 `ProduccionService.ResolverGranjaYModeloAsync` devuelve también el `paisId` resuelto
- [x] V7.2 Producción pasa ese país a la separación (mandaba `null` literal ⇒ roto en el 100 % de los casos, toda empresa)
- [x] V7.3 Levante (2 sitios), engorde (2) y engorde EC (2) pasan el país resuelto en vez de `lote.PaisId` crudo. Sanmarino tiene 2 de 10 lotes con `pais_id` NULL (K345A/K345B)
- [x] V7.4 `AplicarAlimentoAsync` **lanza** cuando el país no resuelve y hay kilos separados, en vez del `continue` mudo; y el total devuelto es el realmente aplicado (antes informaba los kilos aunque no se moviera nada)
- [x] V7.5 `ReservaSeguimientoCalculos.MotivoAlimentoNoAplicable` + 6 tests xUnit

### H2 — Producción con el flag ON estaba rota en las tres operaciones
- [x] V7.6 `SeguimientoProduccionService`: frenaba el descuento de aves y **no separaba nada** ⇒ la mortalidad se evaporaba. Ahora separa en alta y edición, y libera al borrar
- [x] V7.7 `ProduccionService` **editar**: aplicaba el diff de inventario aunque el alta solo hubiera reservado (doble descuento al validar). Ahora reescribe la reserva
- [x] V7.8 `ProduccionService` **borrar**: devolvía stock que nunca salió (inflaba el inventario) y dejaba la reserva ACTIVA para siempre. Ahora libera
- [x] V7.9 Guard de editable en las dos: un registro validado no se edita ni se borra

### H3 — El saldo de aves de producción tenía TRES escritores
- [x] V7.10 `lote_postura_produccion.aves_h_actual` **no es un maestro, es una caché**: `ProduccionService.Consultas` la reescribe con `fn_seguimiento_diario_produccion`, y **ninguna fn del esquema mira `validado`** (verificado: `prosrc ILIKE '%validado%'` = 0 filas). O sea que las bajas sin validar ya están adentro
- [x] V7.11 El disponible de traslado en producción restaba la reserva **sobre un saldo que ya la incluía** — regresión introducida por `bebac18`, el mismo doble descuento que ese commit decía estar evitando. Quitada
- [x] V7.12 Validar ya no mueve esa caché (dejaba el número al doble hasta la siguiente consulta). Queda **documentado**: en producción la doble validación difiere el alimento, no el saldo de aves

### H4 — Empresa efectiva por datos
- [x] V7.13 `LeerEstadoAsync` resuelve y compara la empresa del lote (fail-closed). Validar/desvalidar buscaban **solo por id**: con el permiso puesto se podía aplicar el consumo de otra empresa
- [x] V7.14 Validar engorde usa el `company_id` de la reserva, no el del usuario (`SincronizarAsync` retorna en silencio si no matchea ⇒ validado sin descontar)

### H5 — La columna del ítem es polimórfica y tenía FK a una sola tabla
- [x] V7.15 **Bloqueaba a Colombia entero**: 208 de 435 `catalogo_items` no existen como `item_inventario_ecuador.id`, así que guardar un seguimiento de postura con el flag ON daba 500 por violación de FK. Migración `20260816225138_QuitarFkPolimorficaReservaAlimento`, idempotente, aplicada en local y verificada
- [x] V7.16 Entidad y configuración EF alineadas con el diseño (sin navegación al ítem)

### H6 — `validado` nacía en false con el flag APAGADO
- [x] V7.17 Los Crud nunca seteaban la columna: todo registro creado desde el backfill nacía `false`. El día que una empresa encendiera el flag, esos registros aparecían pendientes, pasaban a EN RETRASO a las 24 h y **bloqueaban el alta de días nuevos** de cada lote. Ahora `Validado = !separa` en los 4 Crud
- [x] V7.18 Desvalidar un registro **anterior** al flag se niega: no tiene reservas que devolver, y marcarlo pendiente habilitaba el doble descuento al reeditarlo

### H7 — Levante: una sola clave para dos espacios de ids
- [x] V7.19 Al validar, el aplicador recibía `LoteRefInt` como `lote_postura_levante_id` **y** como `lote_id`. En la base local los LPL 13/14 están soft-deleted mientras los `lote_id` 13/14 (K345A/B) viven: la colisión descontaba del lote equivocado, sin filtro de empresa. Ahora el par sale del registro

### Cierre
- [x] V7.20 `dotnet build` 0 errores (1 warning preexistente ajeno) · `dotnet test` **2608 en verde**
- [x] V7.21 Migración aplicada en local, FK confirmada eliminada, base sin residuos, flags en su valor original, `:5501` y `:5002` libres

### Validación por empresa — CORRIDA, las 5
Snapshot restaurable (`smoke_v7`) antes de tocar; al terminar **0 tablas con diferencia** contra el
baseline de las 129, flags en su valor original, puertos libres.

- [x] V7.22 **Sanmarino** · levante lpl 6 (con `lotes.pais_id` puesto en NULL a propósito, para reproducir K345A): reserva con **pais_id=1** (antes 0), stock y saldo quietos al guardar, validar baja **exactamente 100 kg y 5 aves**, desvalidar devuelve, borrar deja las reservas LIBERADAS. Producción lpp 7: ídem con 50 kg. **0 fallas**
- [x] V7.23 **Sanmarino con el flag APAGADO**: no se separa nada, el stock baja **al guardar**, y el registro nace `validado=true` — la regresión de H6 verificada en runtime
- [x] V7.24 **Demo** · levante lpl 15 y producción: reserva con país resuelto, ciclo completo. Un intento con stock insuficiente **se rechazó dejando todo intacto** (fail-closed correcto)
- [x] V7.25 **ItalcolPanama** · engorde lote 168: ciclo completo, reserva con el literal canónico `ENGORDE` y pais 3, stock 10.609,560 → 10.529,560 → 10.609,560. **Regresión OK**
- [x] V7.26 **ItalcolEcuador** · engorde lote 150 con el flag encendido: ciclo completo, pais 2, **0 fallas**
- [x] V7.27 **Santa Reyes** · sin lotes: el flag se lee, los 4 módulos responden `pendientes` sin romper, y **validar un registro de otra empresa se rechaza** — H4 verificado en runtime

### H8 — Encontrado POR el smoke: producción escribía `farm_id = 0`
- [x] V7.28 Un LPP vivo cuyo lote base está soft-deleted (Demo, lpp 8 → lote 119) hacía que `ResolverGranjaYModeloAsync` devolviera `(null, null, Ninguno)` y la reserva se insertara con `farm_id = 0` ⇒ **500 por FK**, dejando además el seguimiento persistido **sin reserva**. Ahora se rechaza con 400 y mensaje claro **antes** de persistir; verificado: sin registro huérfano

### Las dos observaciones del smoke: investigadas y cerradas
- [x] V7.29 **Corregida.** El clamp de `DescuentoAvesSeguimientoCalculos.AplicarDelta` recorta en 0 y su propio doc ya decía que eso hace la operación **no reversible** —y que cambiarlo movería saldos históricos, así que no se toca—. El arreglo va donde corresponde: **validar exige saldo suficiente y se rechaza si no alcanza**, igual que el alimento ya se rechaza con `ValidarStockConsumoAsync`. Así el clamp nunca se alcanza desde este camino. `ReservaSeguimientoCalculos.MotivoAvesNoAplicable` + 5 tests. Verificado en runtime: 8.554 bajas sobre un saldo de 8.544 → **400**, saldo intacto, registro sin validar; y el lote de Demo que antes se inflaba de 0 a 5 ahora queda en **0**
- [x] V7.30 **No era un bug.** `AlimentoObligatorioCalculos` documenta que `itemsGenerales` es la bolsa de «otros ítems» y **nunca satisface la regla** (`KgQueCuentan = KgHembras + KgMachos`), y el modal de engorde solo tiene los arrays `itemsHembras`/`itemsMachos`: en Panamá la columna Mixto escribe en **`itemsHembras`**. Mi smoke usó el bloque equivocado. Sin cambios

### H9 — Los traslados creaban filas que bloqueaban el lote
- [x] V7.31 Las 4 filas que arma `TrasladoAvesDesdeSegService.Traslado` (salida/ingreso de levante y de producción) nacían con `validado=false` sin reserva: en una empresa con el flag encendido aparecían pendientes y a las 24 h **bloqueaban el alta de días nuevos** sin haber nada que validar. El traslado ya movió el maestro ⇒ nacen validadas

### H10 — El botón Validar se veía con el flag apagado (y en más pantallas de las reportadas)
- [x] V7.32 `puedeValidar` miraba **solo el permiso** en las tres listas —producción, **levante** y **engorde**, no solo producción como decía el reporte—. Con el flag apagado el ✓ aparecía sobre registros que ya habían descontado al guardar, y apretarlo los dejaba de solo lectura sin que nadie lo pidiera. Ahora exige además `requiereValidacion`, que es fail-closed

### Barrido final
- [x] V7.33 **Las 5 empresas, de nuevo y completo.** Sanmarino (levante con `pais_id` NULL + producción + flag OFF + saldo insuficiente), Demo, ItalcolPanama, ItalcolEcuador, Santa Reyes (aislamiento entre empresas). Restaurado con **0 tablas con diferencia** sobre las 129
- [x] V7.34 `dotnet build` 0 errores · `dotnet test` **2613 en verde** · `yarn build` OK (único warning, el de budget preexistente) · flags originales, 0 reservas activas, puertos libres

### Los 3 que quedaban: resueltos

**V7.35 + V7.36 — un solo concepto: `ModoCargaHistorica()`.**
Una carga histórica no son días pendientes de validar: son días que ya pasaron y cuyo alimento ya se
consumió. Dentro del alcance que devuelve el método, la empresa se comporta como si no usara doble
validación (descuenta al guardar, las filas nacen validadas). Es un `IDisposable` y no un setter para
que se apague solo si el import se cae a la mitad; el contador es anidable y el servicio es `Scoped`,
así que el modo nunca cruza de una request a otra.
- [x] V7.35 `MigracionService` (import de engorde por Excel) y `PuentePanamaService.SincronizarAsync` envueltos. **Reproducido el defecto en runtime**: con el flag ON, el día histórico 1 entra y el día 2 devuelve *«el lote tiene un registro sin validar que superó el plazo (10/08/2026)»* — un lote de 40 días entraba con una sola fila
- [x] V7.36 Y de fondo: `ValidarAlimentoObligatorio` ahora **también mira los kg directos** (`ConsumoKgHembras/Machos`), no solo los ítems del metadata. Se toma el **máximo** por bloque, no la suma: cuando vienen los dos son el mismo alimento expresado dos veces. Aplicado en los 6 llamadores de levante y engorde

**V7.37 — el cuadre ya no cuenta lo separado como descuadre.**
Ninguna fn del esquema mira `validado`, así que `fn_seguimiento_diario_engorde` ya descontó el consumo
de un registro pendiente mientras el inventario todavía no lo movió: cada pendiente aparecía como un
descuadre por sus propios kilos. La reserva ACTIVA **es** ese movimiento pendiente, así que el stock
comparable es `stock − reservado` — el mismo «disponible» que ya muestra el inventario—.
- [x] V7.37 `CuadreAlimentoEngordeCalculos.DescuadreAjustadoPorReservas` + el service agrupa las reservas activas por ubicación. **No se toca ninguna fn SQL**, así que no hace falta el gate de paridad multipaís. Con el flag apagado no hay reservas ⇒ el número es idéntico al de antes (test) y un descuadre REAL sigue apareciendo aunque haya reservas (test)
- [x] V7.38 **Verificado en runtime** (ItalcolPanama, flag ON): con 80 kg separados el cuadre queda **byte a byte igual** — 6 descuadrados y 55.045,359 kg de error absoluto antes y después. Sin el ajuste ese galpón habría saltado a Descuadrado

### Cierre de los 3
- [x] V7.39 `dotnet build` 0 errores · `dotnet test` **2616 en verde** · base restaurada con **0 tablas con diferencia**, flags originales, 0 reservas activas, puertos libres

---

## V8 · Descuadres de alimento de ItalcolPanama — ABIERTO, para otra sesión (16ago26)

**Dato PREEXISTENTE.** No lo causó la doble validación: el baseline lo midió antes de tocar nada
(6 descuadrados / 55.045,359 kg) y quedó idéntico después de todo el trabajo de V7. Se levanta acá
porque el cuadre es el termómetro que la guía manda mirar, y hoy está en rojo.

**Cómo reproducir** (no hace falta backend):
```sql
SELECT * FROM fn_cuadre_alimento_engorde(5) WHERE abs(descuadre_kg) > 1 OR filas_negativas > 0
ORDER BY abs(descuadre_kg) DESC;
```
Invariante: `descuadre = saldo_tabla − (stock − movimientos_posteriores)`. Tolerancia 1 kg
(`CuadreAlimentoEngordeCalculos.ToleranciaKg`).

### Los 6 descuadrados — son TRES patrones, no uno

| # | Lote | Galpón | Núcleo | saldo_tabla | stock | mov_post | descuadre | negs |
|---|---|---|---|---|---|---|---|---|
| 1 | 187 «33 - 1» | G0483 | 180197 | 26.384,0 | 3.084,0 | 0 | **+23.300,0** | 0 |
| 2 | 165 «94 - 2» | G0475 | 147337 | 21.216,4 | 2.566,0 | 0 | **+18.650,4** | 1 |
| 3 | 199 «33 - 1» | G0481 | 180197 | **−4.446,0** | 5.359,0 | 0 | **−9.805,0** | 7 |
| 4 | 202 «86 - 3» | G0476 | 785639 | 4.976,0 | 2.480,0 | 0 | **+2.496,0** | 0 |
| 5 | 182 «86 - 1» | G0477 | 785639 | 555,0 | 11,0 | 0 | **+544,0** | 1 |
| 6 | 168 «60 - 3» | G0490 | 791385 | 10.609,6 | 10.609,6 | **250,0** | **+250,0** | 1 |

Todos en granja **106 (DOÑA MARIA)** y todos con el lote **Abierto**. Suman 55.045,4 kg.

**Patrón A — la tabla muestra MÁS de lo que hay (#1, #2, #4, #5; 44.990,4 kg).** `mov_post = 0`, así
que no es un movimiento tardío. Hipótesis a descartar en orden: (a) alimento que **entró** al galpón y
nunca se registró como ingreso de inventario —el stock quedó corto, no la tabla larga—; (b) consumos
que descontaron el stock por otra vía sin pasar por el seguimiento; (c) un traslado bodega→galpón sin
contraparte. Ojo con #1 y #3: **comparten núcleo 180197 y nombre de lote «33 - 1»**, y uno sobra
23.300 mientras el otro falta 9.805 — huele a alimento imputado al galpón equivocado entre dos lotes
del mismo núcleo.

**Patrón B — saldo NEGATIVO (#3, y 17 galpones más con `descuadre = 0` pero `filas_negativas > 0`).**
`saldo_tabla = −4.446` con 7 días cerrando en negativo: se consumió alimento cuya llegada no está
registrada. Los peores por cantidad de días en rojo son el **lote 161 (G0472) con 28 filas** y el
**lote 142 (G0471) con 17**, ambos **cuadran contra el inventario** — o sea que el total está bien y lo
que está mal es el **orden/fecha** de los ingresos: el consumo se registró antes que la entrada.
Es el mismo patrón que ya documentó el repo en la ventana de alimento previo al encaset.

**Patrón C — #6, y es el único que puede no ser un error de datos.** El descuadre (250,0) es
**exactamente `mov_post`**, con `saldo_tabla == stock`. O sea: hay un movimiento posterior al último
seguimiento y el corte por fecha lo cuenta de un lado y no del otro. **Empezar por acá**: es el más
barato de decidir y, si resulta ser un artefacto del cálculo, baja el conteo de 6 a 5 sin tocar un
solo dato. Aviso: el lote 168 es el que usaron los smokes de V7 — su baseline limpio es
`stock 10.609,560`, y ya volvió a ese valor.

### Cómo NO resolverlo
- ⛔ **No «cuadrar» anulando o borrando filas.** La guía es explícita y ya pasó: anular las 93 filas
  huérfanas del histórico parecía obvio y habría mandado 5 ciclos cerrados de saldo 0 a negativo.
  Simular en una transacción y revertirla ANTES de tocar nada.
- ⛔ **No tocar `fn_seguimiento_diario_engorde` ni `fn_cuadre_alimento_engorde`** sin el **gate de
  paridad multipaís** (`backend/sql/verificar_paridad_saldo_engorde.sql`, corrida ANTES y DESPUÉS):
  Ecuador encadena 3-4 ciclos por galpón, topología que Panamá no tiene, y ya se rompió así una vez.
- ⛔ **No mirar solo Panamá.** El mismo query con `fn_cuadre_alimento_engorde(3)` para Ecuador antes de
  concluir que el patrón es de una empresa.

### Checklist
- [x] V8.1 (cerrado en V17.1.2) Decidir el **patrón C** (#6, lote 168): ¿el descuadre de 250 kg es un movimiento real mal fechado o un artefacto del corte por fecha del cuadre?
- [x] V8.2 (cerrado en V17.1.5) Reconstruir el kardex de **#1 y #3** (núcleo 180197, lotes 187 y 199) y confirmar o descartar el cruce de imputación entre los dos «33 - 1»
- [x] V8.3 (cerrado en V17.1.4) Patrón A en **#2, #4, #5**: cruzar `inventario_gestion_movimiento` del galpón contra los ingresos del ERP para ubicar el alimento que entró sin registrarse
- [x] V8.4 (cerrado en V17.1.7) Patrón B: datar los ingresos de los lotes **161 (28 días negativos)** y **142 (17)**; el total cuadra, así que el arreglo es de FECHAS, no de cantidades
- [x] V8.5 (cerrado en V17.1.1) Correr el mismo cuadre en **ItalcolEcuador (3)** para saber si el patrón es de Panamá o del cálculo
- [ ] V8.6 Simular toda corrección en transacción + revertir, y correr el gate de paridad antes y después

### Hallazgos confirmados que NO entran en esta entrega

> ⚠️ **Revalidado 17ago26 contra el código: los 4 primeros ya estaban resueltos** en la misma entrega
> que los listó — la numeración se pisó (estos V7.23-V7.26 son los hallazgos; V7.31/32/35/36 son los
> arreglos). Se marcan con su evidencia. El único que sigue abierto es V7.27.

- [x] V7.23 El bloqueo por vencidos corta la carga masiva histórica y el puente Panamá — **cerrado por
      V7.35**: `ModoCargaHistorica()` envuelve `MigracionService.SeguimientoEngorde.cs` y
      `PuentePanamaService.Sincronizar.cs` (verificado: son los 2 llamadores)
- [x] V7.24 El guard de alimento obligatorio medía solo el metadata — **cerrado por V7.36**:
      `SeparacionSeguimientoHelper.ValidarAlimentoObligatorio` recibe `kgHembrasDirecto` /
      `kgMachosDirecto` (`SeparacionSeguimientoHelper.cs:35-37`)
- [x] V7.25 Los traslados creaban filas `validado=false` — **cerrado por V7.31**: las 4 filas nacen con
      `Validado = true` (`TrasladoAvesDesdeSegService.Traslado.cs:365,423,482,541`)
- [x] V7.26 El botón Validar con el flag apagado — **cerrado por V7.32**: `requiereValidacion` está en
      las 3 listas (levante, producción y engorde) y en sus 3 plantillas
- [x] V7.27 El saldo de alimento y el cuadre de engorde se recalculan ignorando `validado` — **cerrado
      por V12** (bloque al final): la respuesta no era filtrar la fn (que no mire `validado` es
      deliberado y correcto), sino que la doble validación escribía sus movimientos con una referencia
      que ningún lector de engorde reconoce ⇒ desvalidar inflaba el saldo del galpón. Gate multipaís
      corrido antes y después: **0 en todas las columnas, en las dos empresas**

---

# V19 · §2.4 — el kardex de bultos es de la GRANJA y el reporte no lo decía (17ago26)

**Plan:** [`fase_de_desarrollo/reporte_contable_bultos_alcance_granja_plan.md`](fase_de_desarrollo/reporte_contable_bultos_alcance_granja_plan.md)
Pedido: «seguí con el siguiente pendiente del tracker» ⇒ **§2.4**, el último 🟡 confirmado de la
auditoría de cierre que seguía abierto y sin depender de nadie (§2.3b y §2.3c los descartó V15.0.2
porque el rediseño de la marca los tira). Bloque propio — no tocar desde otras sesiones.

## V19.0 — Confirmado en el código y revalidado con datos ✔
- [x] V19.0.1 El reporte se genera **por lote padre** (`GenerarReporteAsync` exige `LotePadreId`) pero
      los movimientos de alimento se traen filtrando **solo por granja** (`m.FarmId == granjaId`).
      No hay filtro de lote porque **no hay dato con qué filtrar**
- [x] V19.0.2 **Cuántos casos hay**: Sanmarino tiene **3 granjas con más de un lote padre**
      (MANGOS 4 · LA ESMERALDA 4 · MIRALINDO 2) ⇒ **10 de sus 11 lotes padres** muestran un kardex que
      no es suyo. Demo: 5 granjas, 1 padre cada una, **0 afectados**
- [x] V19.0.3 🔑 **Por qué no se puede atribuir**: en Sanmarino los movimientos de alimento son de
      **nivel granja** (1.077 de 1.078 filas sin núcleo ni galpón) y los padres de cada granja
      **comparten el mismo núcleo**. La auditoría tenía razón: no es arreglable en la query
- [x] V19.0.4 Escala: LA ESMERALDA tiene **4.356 bultos de entradas y 3.830 de consumo** en toda su
      historia, y **4 reportes** los muestran como propios

## V19.1 — Fase 1: que el reporte DIGA de quién es el kardex
- [x] V19.1.1 `ReporteContableBultosCalculos.AdvertenciaAlcance(lotesPadreEnGranja, granjaNombre)` —
      puro: `null` cuando el padre es el único de la granja (sin ruido), aviso cuando comparte
- [x] V19.1.2 DTO + service: `LotesPadreEnGranja` y `AdvertenciaBultos`
- [x] V19.1.3 Front: el aviso bajo el título **BULTO** (`@Input` nuevo + `@if`, componente ya `Eager`)
- [x] V19.1.4 Tests T1-T5 (8 casos con los `[Theory]`)
- [x] V19.1.5 **Ningún número del reporte se mueve**, y se puede probar por el diff: en el service la
      ÚNICA línea eliminada es `ReportesSemanales = reportesSemanales` — reemplazada por la misma con
      una coma. `AcumularSaldos` y todo el cálculo quedan intactos

## V19.2 — Fase 2, que NO entra: el saldo coherente (decisión del usuario)
- [!] V19.2.1 Hoy el saldo es `entradas de la GRANJA − consumos de ESTE padre` ⇒ **sobreestima** tanto
      como consuman los otros padres. Las salidas son **(a)** restar el consumo de todos los lotes de la
      granja —el número pasa a ser verificable contra el inventario, pero **cambia una columna que
      Costos ya lee**— o **(b)** dejarlo con el aviso al lado. **Se recomienda (a)**; mover una columna
      de un reporte contable en uso es decisión de producto, no un refactor

## V19.3 — Verificación
- [x] V19.3.1 `dotnet build` **0 errores** · `dotnet test` **2.802 + 1 en verde** (+8)
- [x] V19.3.2 `yarn build` OK (la plantilla nueva type-chequea el binding)
- [x] V19.3.3 Smoke ejecutando `GET /api/ReporteContable/generar` con datos reales:
      · lote **114 (A374A, LA ESMERALDA)** ⇒ `lotesPadreEnGranja: 4` y el aviso completo
      *«Estos movimientos de alimento son de la GRANJA «LA ESMERALDA», que hoy tiene 4 lotes padres: el
      reporte de los otros 3 muestra los mismos kilos. NO sumar los reportes entre sí.»*
      · lote **13 (K345A, NIZA III)** ⇒ `lotesPadreEnGranja: 1` y **aviso `null`**
- [x] V19.3.4 ✔ **CERRADO en V21** (17ago26): el aviso se verificó pintado en pantalla en los dos
      casos (LA ESMERALDA con aviso, NIZA III sin él). Y no era el harness: el tab de la semana se
      destruía y recreaba en cada ciclo (NG0956/NG0100) y salía **sin rótulo**; V21 lo arregló.
      Texto original: ⚠️ **Lo que NO pude smokear**: el aviso PINTADO en pantalla. El panel de bultos vive
      dentro de la cascada de filtros del reporte (granja → lote → sublote → semana) y no logré
      conducirla desde el harness; el DTO sí llega con el campo al componente (verificado en runtime:
      `lotesPadreEnGranja: 4`). Queda como verificación visual pendiente de la próxima sesión que abra
      esa pantalla

---

# V20 · Auditoría del saldo negativo del lote 12 (KM 86 / G0040) — SOLO LECTURA (17ago26)

**Plan:** [`fase_de_desarrollo/auditoria_lote12_saldo_negativo_plan.md`](fase_de_desarrollo/auditoria_lote12_saldo_negativo_plan.md)
Pedido: «seguí con el siguiente pendiente del tracker» ⇒ *«El lote 12 arrastra apertura negativa
(−9.020 kg): auditoría de datos aparte»*, del bloque «Lote cerrado que absorbe el ciclo siguiente
(KM 86)». Bloque propio — no tocar desde otras sesiones. **Ni un dato corregido.**

## V20.0 — Qué es realmente el −9.020 ✔
- [x] V20.0.1 **No es una apertura**: su apertura es **0,0**. Es el saldo con el que **TERMINA** la
      serie del lote 12, y **21 de sus 63 días** cierran en rojo
- [x] V20.0.2 **La aritmética cierra exacta**: entradas netas **126.940,0 kg** (123.940 ingresos
      + 4.000 traslado entrada − 1.000 traslado salida) contra **135.960,0 kg** de consumo declarado por
      los seguimientos ⇒ **−9.020,0**
- [x] V20.0.3 🔑 **Causa: los ingresos son una RECONSTRUCCIÓN.** Los 19 `INV_INGRESO` del período
      llevan la referencia *«Cuadre saldos Excel — Insertar ingreso d…»*: el historial se rearmó desde
      una planilla y quedó **9.020 kg corto** frente al consumo cargado. Es **dato, no fórmula**
- [x] V20.0.4 **Corregirlo exige las remisiones físicas** de feb-abr 2026: sin el papel, cualquier
      ingreso inventado cuadra igual de bien (misma conclusión que V17 con los lotes 161 y 142)

## V20.1 — La buena noticia: no se contagia ✔
- [x] V20.1.1 El lote **73** (ciclo siguiente en G0040, encaset 24-abr) abre con **apertura vacía y
      saldo +5.280,0**. Las guardas de v11/v12 y el corte de v14 contienen el rojo dentro del lote 12.
      Por eso esto es auditoría y no urgencia

## V20.2 — El caso no es único ✔
- [x] V20.2.1 Censo de las dos empresas: **8 lotes cierran su serie en negativo** — Ecuador 1 abierto
      (el 12) y **4 cerrados** (16 −3.920 · 7 −3.220 · 15 −600 · 14 −1,0), Panamá 3 abiertos
      (−7.392,8; ya diagnosticados en V17 como patrón B)

## V20.3 — Lo que parecía una contradicción y NO lo es ✔
- [x] V20.3.1 De los 4 congelados de Ecuador, **tres tienen cabecera que no coincide con su detalle**:
      lote 15 cabecera **+14.000** (13-may) contra **−600** en la última fila (16-may); lote 7 **+3.180**
      contra **−3.220**. El lote 14 coincide porque no tuvo movimientos posteriores
- [x] V20.3.2 **Es la convención, no un defecto**: la cabecera guarda el saldo del último día **con
      SEGUIMIENTO** y la serie sigue con filas **solo-movimiento**. Es exactamente lo que hace
      `fn_cuadre_alimento_engorde`, que toma el saldo en `seg_max` y **no** el de la última fila —su
      comentario avisa que contarlo de las dos formas duplicaría los movimientos posteriores—. El
      reporte de V16 ya resta esas salidas por separado, así que **lee bien** los 14.000 del lote 15
- [x] V20.3.3 ⛔ **Queda escrito para que nadie lo «arregle»**: alinear la cabecera con la última fila
      rompería el reporte de V16 y el cuadre a la vez

## V20.4 — Qué hacer (necesita decisión, por eso no se hizo)
- [!] V20.4.1 **Decisión pendiente sobre el lote 12**: (a) dejarlo —no contagia a nadie y Ecuador sigue
      con 0 descuadrados—; (b) **completar la reconstrucción** cargando los 9.020 kg faltantes con su
      fecha real desde las remisiones físicas (la única corrección legítima); (c) liquidarlo como está,
      que **congelaría −9.020 para siempre** (V18: la foto no se reescribe)
- [!] V20.4.2 ⚠️ **Si se decide cerrar los lotes 2 y 12** —el otro pendiente del mismo bloque—, para el
      **12 conviene resolver esto primero**: liquidar antes de completar la carga congela el negativo

## Fuera de alcance, dicho
- [x] V20.5.1 **Cero correcciones de datos**: ni los 9.020 del lote 12, ni los 4 congelados de Ecuador,
      ni los 3 de Panamá (ésos ya los cubre V17)
- [x] V20.5.2 **No se toca ninguna fn ni `LiquidacionCongeladaAplicador`** (V20.3 explica por qué la
      convención actual es la correcta)

---

# V25 · Triaje del tracker + 5 planes en paralelo (18ago26)

Pedido del usuario: un loop que recorra los `- [ ]` del tracker, resuelva cada tarea en una sesión
aislada y la marque hecha. **El loop tal cual escrito no era viable acá** y el diagnóstico es el
entregable principal: en este tracker `- [ ]` no significaba «tarea», significaba «sin cerrar» por
cuatro razones distintas. La primera tarea que agarraba el loop era un comando de PowerShell contra
Exchange Online: ningún agente puede resolverla, así que o se colgaba en bucle o la marcaba hecha y
el tracker pasaba a mentir. Y entre los pendientes había 4 acciones irreversibles sobre producción.

## V25.1 — Convención de marcas
- [x] V25.1.1 `- [ ]` tarea ejecutable · `- [!]` decisión del usuario · `- [~]` fuera del repo ·
      `- [i]` hallazgo o nota. Leyenda al inicio del archivo. `grep -c '^- \[ \]'` pasa a contar solo
      trabajo real
- [x] V25.1.2 66 pendientes triados: 23 tareas (12 eran del bloque V24), 13 decisiones, 10 fuera del
      repo, 20 hallazgos. **Hoy quedan 9 tareas ejecutables en todo el tracker**
- [x] V25.1.3 Estado obsoleto corregido: la Fase 3 de R2 la había cerrado V16 (que la cita textual) y
      B10 seguía listado como pendiente en 5 sitios pese a estar cerrado en `56f7caa`

## V25.2 — Cinco planes, cinco sesiones aisladas
- [x] V25.2.1 `v16_engorde_atribucion_persistida_plan.md` · `4f15a0c`
- [x] V25.2.2 `b1_revocacion_sesion_plan.md` · `5d54dbd`
- [x] V25.2.3 `pwa_sesiones_multislot_plan.md` · `b0cf608`
- [x] V25.2.4 `pwa_f4_offline_edicion_plan.md` · `79d29f6`
- [x] V25.2.5 `v12_5_1_migraciones_v16_ausentes_informe.md` · `0d8eee0` — cierra **V12.5.1**
- [x] V25.2.6 Ninguna sesión compiló, tocó código, tocó el tracker ni commiteó (la sesión V24 estaba
      editando el backend en paralelo; un `dotnet build` le habría bloqueado el `bin/`)
- [x] V25.2.7 **V8.6 excluida a propósito**: el encabezado la reserva para otra sesión

## V25.3 — 🔴 Defectos VIVOS que encontraron los planes al medir
Ninguno es parte de los planes: son de hoy. Verificados en el código, no tomados del agente.

- [x] V25.3.1 🔴 **El outbox se sincroniza sin filtrar por partición.** — **cerrado en V29** (al final). `sync.service.ts:71` usa
      `OutboxService.listarTodas()`, cuyo propio doc-comment dice «toda la cola, sin filtrar».
      Alcanzable HOY con un solo slot: el JWT vence a los 60 min → `authGuard` hace `logout()` → el
      outbox **sobrevive** (`purgarTodo` limpia solo `STORE_CONSULTAS`) → entra otro operario y sus
      capturas se empujan con el token del nuevo. Misma empresa ⇒ quedan firmadas por otro; empresa
      distinta ⇒ `empresa_no_autorizada`, clasificado como *reintentar, no bandeja* ⇒ reintento
      infinito e invisible
- [x] V25.3.2 🔴 **`/diagnostico` muestra y borra el outbox de todos, sin login.** — **cerrado en V31** (al final). La ruta no lleva
      `authGuard` **a propósito** (es la pantalla de rescate) y esa decisión es correcta; lo que
      caducó es su premisa. El doc-comment dice «no expone ningún dato de negocio»: cierto en F1,
      falso desde F3.1 (`c44e0a4`), que agregó `listarTodas()` + `JSON.stringify` del payload + poder
      descartarlo
- [x] V25.3.3 🔴 **La mitigación de la marca `para_proximo_ciclo` es solo de front.** — **cerrado en V26.3**: `GuardarMarcaProximoCicloApagada` rechaza con 400 en los 3 caminos que persistían la marca (verificado: 3 llamadores en `InventarioGestionService.cs`). El tracker decía
      «la puerta de entrada está cerrada»: lo está la del navegador. La API sigue aceptando
      `ParaProximoCiclo` (`InventarioGestionDtos.cs:147, 214, 405, 427`) ⇒ el defecto de v15 es
      reintroducible desde Swagger o la PWA
- [i] V25.3.4 **`auth_session` se guarda en claro.** `token-storage.service.ts:41` hace
      `JSON.stringify(session)` directo al storage; CLAUDE.md afirma que va cifrado con AES
- [i] V25.3.5 **El authGuard mata la jornada offline de 16 h a los 60 min**: al vencer el JWT hace
      `logout()` y manda a `/login`, que sin red es un callejón sin salida
- [i] V25.3.6 **El espejo C# de la marca nunca corre en producción**: ningún llamador pasa
      `ciclosDelGalpon`, solo los xUnit ⇒ SQL y C# ya divergen, contra «una sola fórmula por número»
- [i] V25.3.7 **`requiere_cuadre` no es preparación para F4, es un defecto vivo de F3**: el comentario
      de `SyncPushCalculos.cs:42-45` dice que el alta de levante no valida saldos, y hoy es falso —
      las 4 capturas validan stock y lanzan antes de persistir, así que el dato de campo queda varado
      en la bandeja. Y no tiene lector: `SyncController` solo expone `POST push`

## V25.4 — Lección para el próximo que lea este archivo
- [i] V25.4.1 **Un `- [x]` de este tracker no garantiza que el código exista.** El bloque v16 declaraba
      entregadas 2 migraciones, 2 fns SQL, un espejo C#, un índice y un gate de 566 líneas: nada llegó
      a un commit. Antes de construir sobre un `- [x]`, verificalo contra el repo

## V25.5 — Ronda de decisiones: dos ya estaban resueltas y el tracker no lo sabía
Medido en la BD local (`sanmarinoapplocal`, datos hasta el 13-ago). ⚠️ **Es un dump de fecha incierta:
antes de concluir lo mismo de producción hace falta el acceso que bloquea V25.6.**

- [x] V25.5.1 **Grupo A: ya se cerró.** Ninguno de los **31** lotes abiertos de Ecuador tiene saldo 0
      —el más chico tiene 625 aves— y hay **64 cerrados el 06-ago**, el día siguiente a la medición
      del tracker, con 90 copias congeladas de liquidación. No hay lista de 39 que mostrar
- [x] V25.5.2 **El menú 9 ya no lo tiene nadie.** `role_menus` = 497 filas, **0 con `menu_id = 9`**;
      `company_menus` también 0. El tracker (12-ago) decía 3 roles. Su etiqueta real es «Seguimiento
      Reproductora Postura», no «Lote Reproductora». **Decisión del usuario: quitarlo — ya está**
- [x] V25.5.3 **Migraciones Masivas queda solo para Sanmarino** (decisión del usuario, 18ago26).
      Santa Reyes lo pierde junto con Panamá, Demo y Ecuador. La migración existente ya lo hace: sin cambios
- [ ] V25.5.4 **Lote 12 (KM 86 / Galpon-2): cargar los 9.020 kg.** Decisión del usuario: existen las
      remisiones físicas ⇒ opción (b), la única corrección legítima. Confirmado que el defecto sigue
      vivo: el ciclo corre 17-feb→22-abr-2026 y cierra en **−9.020 kg**. **Falta el dato de origen**
      (fecha y kg de cada remisión) para cargarlo con su fecha real
- [i] V25.5.5 Los lotes 2601 (id 2 y id 12) **siguen abiertos y con aves vivas** (773 y 1.082) ⇒ el
      aviso a operación sigue vigente, y para el 12 la carga va **antes** del cierre (V20.4.2)
- [i] V25.5.6 **El patrón se repite en los `- [!]`, no solo en los `- [x]`**: 2 de las 4 decisiones de
      esta ronda ya estaban ejecutadas. Antes de pedir una decisión, medir si sigue viva

## V25.6 — Decisiones tomadas (18ago26). Trabajo derivado, todavía SIN implementar
- [x] V25.6.1 **K345 · los 15 días traslapados: producción manda desde el primer huevo.** Los 14 de
      julio 2025 quedan como producción (tienen los huevos y **la misma mortalidad**, que hoy está
      duplicada) retirando la fila de levante pero **conservando el consumo de alimento**, reasignado
      a producción. El día suelto 7-abr-2026 se borra de levante: está vacío (mort 0, kg 0,000) sobre
      un día real de 4.277 huevos ⇒ no es traslape, es basura
- [x] V25.6.2 **Corte levante/producción: alinear a 25 semanas**, como el informe de Verenice
      (~17.332 kg cambian de etapa en S-369). ⚠️ Antes de tocar: auditar qué reportes, vistas y fns
      dependen del límite, y si es constante o configurable — mueve números que ya se mostraron
- [x] V25.6.3 **Lote 132: corregir el encaset a 19.187.** Migración data-only con regla dinámica, sin
      nombrar el id, como el lote 30. Deja la base con **0 lotes sin referencia confiable**. Va ANTES
      de cualquier cierre: el Gate B1 bloquea editar `aves_encasetadas` de un lote liquidado
- [x] V25.6.4 **Migraciones Masivas: solo Sanmarino.** Sin cambios, la migración vigente ya lo hace
- [x] V25.6.5 **Menú 9: quitarlo.** Ya estaba: 0 filas en `role_menus` y en `company_menus`
- [ ] V25.6.6 **Lote 12: cargar los 9.020 kg** con la fecha real de cada remisión (opción b).
      **Bloqueado esperando el dato de origen**: fecha y kg de cada remisión física. El ciclo corre
      17-feb→22-abr-2026
- [~] V25.6.7 Re-correr el detector de sobregiro contra el dump de PROD — **no es una decisión, es un
      bloqueo de acceso**: RDS en VPC privada, ECS Exec deshabilitado, IAM sin permisos
- [i] V25.6.8 Pendientes de preguntar: el tile «Venta Engorde» de Migraciones Masivas y V19.2.1 (el
      kardex de bultos de la GRANJA). Grupos B y C quedan a re-medir: los 31 abiertos tienen aves

## V25.7 — Las 2 últimas decisiones + la medición del kardex (V19.2.1)
- [x] V25.7.1 **El tile «Venta Engorde» se queda.** Verificado: tile en el front, `MigracionEsquemas`
      en el back y la fn v2 con despachos. La venta de engorde no se registra desde el seguimiento
      diario ⇒ la carga masiva es su **único** camino de entrada
- [i] V25.7.2 **Medición del kardex** (pedida antes de decidir): **10 de 11** lotes padres de Sanmarino
      afectados (LA ESMERALDA 4 · MANGOS 4 · MIRALINDO 2), Demo **0 de 5**, ninguna otra empresa tiene
      lotes padres de postura. Los «4 padres» de cada granja son **2 nombres × 2 galpones**
      (A374A/B, S369A/B): no son lotes ajenos, es el mismo nombre en distinto galpón
- [i] V25.7.3 🔑 **La imposibilidad de atribuir es del ESQUEMA, no de la query**:
      `inventario_gestion_movimiento` tiene `farm_id` y `from_farm_id` y **ninguna columna de lote**
- [i] V25.7.4 **Esto reencuadra la decisión: (a) y (b) no son excluyentes.** La opción (a) NO vuelve el
      número por lote —eso es imposible sin dato de lote en las entradas—: lo vuelve un número **de
      granja correctamente calculado**, mostrado en un reporte por lote. Sigue necesitando el rótulo
      de (b). (a) arregla la aritmética, (b) arregla lo que el reporte dice ser
- [i] V25.7.5 A favor de hacerlo: el cálculo **ya está extraído** a
      `Application/Calculos/ReporteContableBultosCalculos.cs` (static puro) ⇒ la corrección es local y
      testeable con xUnit, sin tocar el service
- [x] V25.7.6 ~~**Falta el número fino**~~ — **MEDIDO en V40** (18ago26, bloque al final). La cifra
      está lote por lote y validada contra el endpoint real en 8 de 8. Y cambió la pregunta: la opción
      (a) **empeora** un doble conteo que nadie había visto (`retiros` del inventario y `consumo` del
      seguimiento son el mismo alimento — V40.6). Antes de decidir entre (a) y (b), leer V40.8

## V25.8 — Implementadas: lote 132 y K345 (18ago26)
**Planes:** [`correccion_lote_132_encaset_plan.md`](fase_de_desarrollo/correccion_lote_132_encaset_plan.md) ·
[`correccion_k345_traslape_levante_produccion_plan.md`](fase_de_desarrollo/correccion_k345_traslape_levante_produccion_plan.md)
**Commits:** `c9d8280` (lote 132) · `6ce89cc` (K345)

- [x] V25.8.1 **Lote 132 → 19.187.** Migración data-only `20260818050000...`, Designer clonado,
      ModelSnapshot intacto. Regla dinámica que exige que el gap del encaset sea **exactamente** el
      desfase del maestro ⇒ alcanza 1 lote de 186. Espejo puro en
      `Application/Calculos/CuadreAvesEngordeCalculos.cs` + 7 tests xUnit
- [x] V25.8.2 **`fn_cuadre_aves_engorde(NULL)` pasó de 1 sin referencia confiable y 1 que no cuadra a
      0 y 0.** La base entera queda auditable por conservación
- [x] V25.8.3 **K345 → 0 días traslapados** (eran 15). Migración `20260818050100...`, que **rescata
      antes de borrar**
- [x] V25.8.4 🔑 **Lo que la decisión no contemplaba y apareció al medir**: el alimento **ya estaba**
      en producción (nada que reasignar), pero `sel_m` = 21 + 112 = **133 machos seleccionados**, el
      C.V. y la uniformidad vivían SOLO en levante. Un `DELETE` pelado los perdía
- [x] V25.8.5 Validación: simulación en transacción + `ROLLBACK` antes de cada aplicación ·
      `SUM(sel_m)` se conserva en **133** · kg de producción sin cambio en **18.159,0** · `peso_h` no
      pisado (3.341,40 y 3.307,20) · 15 filas respaldadas en
      `_backup_traslape_levante_k345_20260818` · tombstones 3 → 18 · 2ª corrida `UPDATE 0` /
      `DELETE 0` · `dotnet build` 0 errores (9 warnings preexistentes) · `dotnet test` **2.834 + 1
      verdes** · sin procesos huérfanos (5002/5499/5501 sin listeners)
- [i] V25.8.6 🟡 **Defecto nuevo, medido y NO arreglado**: `produccion_resultado_levante.ac_sel_m` no
      refleja los totales de levante — llega a **8** cuando el `sel_m` acumulado del lote 13 es
      **241**, y el lote 14 **ni figura** en esa tabla. Por eso no servía como respaldo de la
      selección. Tiene su propio alcance
- [ ] V25.8.7 **Falta desplegar**: las dos migraciones se aplican solas al arrancar
      (`Database__RunMigrations=true`), pero exigen la verificación post-deploy de CLAUDE.md §🚀

---

# V27 · Engorde FASE B — el hecho persistido entra INERTE, y el gate tumba el modelo de ENTREGA (18ago26)

**Plan:** [fase_de_desarrollo/v16_engorde_atribucion_persistida_plan.md](fase_de_desarrollo/v16_engorde_atribucion_persistida_plan.md) — **FASE B**.
**Continúa** el bloque V26 (Fase A). **Bloque propio.**

## 🔴 EL HALLAZGO: el modelo de ENTREGA no puede dispararse nunca

La Fase B se implementó completa —tabla del hecho, cálculo dueño, fn `v16b` que la lee— y el gate G1
la tumbó **antes de commitear la fn**. No es un bug de código: es el modelo.

**El mecanismo.** La entrega necesita (a) escribir una salida sintética en el **último día visible** del
cedente y (b) que el cedente **tenga saldo ese día** para poder entregarlo (el tope). Pero
`rango_final.fecha_max` se cierra apenas `saldo_close` encuentra la **primera** fecha ≥ último
seguimiento con saldo ≈ 0. Y **todo ciclo bien operado termina en 0**: es la propia regla R2 —«al
liquidar el lote trasladan el alimento sobrante fuera del galpón».

**Medido** sobre los 53 pares secuenciales con hueco de la BD local (7 granjas, Ecuador):

| | |
|---|---|
| pares con hueco entre ciclos | **53** |
| cedentes cuya grilla **llega** al día de la entrega | **0** |
| cedentes que terminan con **saldo > 0** | **2** |

⇒ Cuando el alimento llega al hueco, **el cedente ya vació su bodega**. No hay kilos que entregar ni
día donde escribir la entrega. El feature sólo podría dispararse cuando la operación dejó saldo
colgado — que es justamente **la anomalía que R2 manda señalar**, no el caso sano que motivó el pedido.

**Qué significa.** El alimento del hueco **no es del ciclo anterior** en ningún sentido contable: llega
después de que ese ciclo cerró. **No hay handoff que modelar.** Lo que necesita es que la apertura del
**DESTINO** alcance más atrás — o sea `dias_alimento_previo_encaset`, la ventana **D4**, que el propio
plan excluye como «otro feature» (§6.2).

- [!] V27.1 🔴 **Decisión de producto**: el rediseño correcto ya no es la entrega entre ciclos sino
      ampliar/parametrizar la ventana D4 del destino. Antes de escribir más código hace falta el OK:
      ¿se abandona el modelo de entrega y se encara D4? La Fase B queda **frenada acá**, no fallida
- [i] V27.2 Esto **contradice el §9.3 del plan original**, que daba por sentado que «el saldo del
      cedente lo cubre entero» en el caso feliz. Era una suposición nunca medida: hoy está medida

## Lo que SÍ entró (todo INERTE: nada lee la tabla todavía)

- [x] V27.3 **Tabla del hecho** `alimento_entrega_ciclo_engorde` + 4 índices + el índice parcial
      `ix_lote_hist_para_proximo_ciclo` que el intento anterior dijo crear y nunca existió. Migración
      `20260818130139`, espejo en `backend/sql/create_alimento_entrega_ciclo_engorde.sql`. Tipos
      `varchar(N)` alineados con la config EF (el plan esbozaba `TEXT`: habría generado un
      `AlterColumn` fantasma en el próximo `migrations add`)
- [x] V27.4 **Los 2 triggers de anulación en cascada**, probados en transacción: `DELETE` del
      movimiento ⇒ entrega `ANULADA` con motivo; `UPDATE` a `TrasladoInterGranjaRechazado` ⇒ idem;
      **ninguna fila se borra**. La condición es la **misma, literal**, que la de
      `trg_inventario_gestion_movimiento_lote_hist_cancel` — se verificó en `pg_get_triggerdef` en vez
      de inventar un `ILIKE '%anulad%'`, que no habría disparado nunca
- [x] V27.5 **`EntregaAlimentoCicloEngordeCalculos`** — dueño único de la atribución, puro, sin EF.
      Cubre los 11 casos del plan + los 3 estados extra. **Fail-closed**: nada termina en `VIGENTE`
      por accidente
- [x] V27.6 **34 tests** que **construyen** las topologías (galpón completo con encaset, primer y
      último seguimiento, congelación). `dotnet test` **2.858 + 1 verdes**
- [x] V27.7 **Prueba de mutación 17/17** (el piso del plan era 12): se desactivó cada guarda una por
      una y todas se pusieron en rojo. **0 guardas sin test.** ⚠️ Honestidad: 1 de las 17 (`cedente sin
      seguimiento`) murió por no compilar, no por un assert — es una señal más débil
- [x] V27.8 **Migración de recálculo** `20260818130200`, el hueco que dejó la Fase A: realinea
      `saldo_alimento_kg` con la fn. En local mueve **0 filas**; va igual porque desde esta máquina no
      se puede afirmar que prod tenga 0 marcas, y una foto congelada con la columna vieja queda mal
      para siempre
- [x] V27.9 **El gate `backend/sql/verificar_entrega_ciclo_engorde.sql`** (I1..I11, con fase de
      inyección sobre pares reales). Es el instrumento que produjo el hallazgo; se conserva para
      cualquier rediseño futuro

## Lo que NO entró, a propósito

- [i] V27.10 ⛔ **La fn `v16b` NO se commiteó.** Se escribió, se instaló en local, pasó el gate
      multipaís (**6.429 filas, 0 en las 7 columnas de diff, las dos empresas** — con 0 entregas es
      byte a byte igual a v16a) y **se revirtió** con `git checkout` al ver el hallazgo. La BD local
      quedó de vuelta en v16a, verificado con el gate. **Riesgo de despliegue: cero.**
- [i] V27.11 Tampoco entraron el service, el controller ni la bandeja: son la maquinaria para escribir
      un hecho que hoy nadie puede leer. Esperan V27.1
- [i] V27.12 ⚠️ **El gate tiene un hueco conocido**: la fase de inyección **no bombea**
      `inventario_gestion_stock`, así que I5 (cuadre) sube por construcción — son movimientos de
      histórico sin contraparte de stock. Está anotado en la cabecera del script. Antes de usar I5
      como veredicto hay que agregar ese `INSERT`

## Validación

- [x] V27.13 `dotnet build` **0 errores / 0 warnings** · `dotnet test` **2.858 + 1 verdes** ·
      gate multipaís **0 en todo, las dos empresas** · cuadre **67 / 8**, igual que antes ·
      migraciones aplicadas y revertidas · **0 rastro** del gate (0 entregas, 0 marcas, 0 inyectado) ·
      sin procesos huérfanos (`:5002` libre; nunca se levantó backend)

---

# V28 · Engorde F2a.1 — la columna «Próx. ciclo» en el tab Histórico (18ago26)

**Plan:** [fase_de_desarrollo/v16_engorde_atribucion_persistida_plan.md](fase_de_desarrollo/v16_engorde_atribucion_persistida_plan.md) — **FASE C / F2a.1**.
**Bloque propio.** Es la única parte de la Fase 2 que no depende de la Fase B (frenada en V27.1).

- [x] V28.1 **La columna entró donde faltaba.** El plan ya había medido que se confundieron dos
      pantallas: `Historial → Ingresos` **sí** pintaba la marca desde siempre; el **tab Histórico** de
      `gestion-inventario-page` (15 `<th>`) **no**. Ahora tiene 16, con el mismo badge naranja que la
      otra pantalla — que sea la misma marca vista desde otro lado y cambie de color hace dudar de si
      es lo mismo
- [x] V28.2 🔑 **«El dato ya viaja» era cierto sólo del lado del servidor.** El DTO del backend manda
      `ParaProximoCiclo` desde la migración `20260808120000`, pero la interfaz
      `InventarioGestionMovimientoDto` del front **no lo declaraba**: el campo llegaba en el JSON y
      TypeScript lo descartaba en silencio. El build lo cazó (`TS2339`). Sin esa línea la columna era
      imposible, y el plan la daba por resuelta
- [x] V28.3 Layout: `.historico-table-wrap` ya tiene `overflow-x: auto`, así que la 16.ª columna
      desborda dentro de su propio contenedor y no puede romper la página
- [x] V28.4 `cd frontend && yarn build` (Node portable 22.23.1) — **0 errores**, sin warnings (ni
      siquiera el de bundle budget). Backend sin tocar
- [~] V28.5 **Falta el smoke en pantalla** (F2a.2 del plan): es un paso manual en pantalla y hace
      falta una sesión de la app, así que ningún agente lo puede cerrar solo. Hoy la columna
      mostraría «—» en todas las filas (0 marcas en la BD): lo que hay que mirar es que el encabezado
      se vea y que la tabla siga scrolleando bien

---

# V30 · Santa Reyes — Requerimientos de Italapp (plan de trabajo)

Plan: [`fase_de_desarrollo/santa_reyes_requerimientos_italapp_plan.md`](fase_de_desarrollo/santa_reyes_requerimientos_italapp_plan.md)

Origen: dos archivos del cliente (18-ago-2026) — `Requerimientos de Italapp.docx` (7 módulos,
10 pantallas anotadas) y `Guías Genéticas.xlsx` (5 líneas × 108 semanas).

## Entregables comerciales

- [x] V30.1 Cronograma **v1** (descartado): 34 días hábiles a 1 actividad por día, entrega 5-oct
- [x] V30.1b Cronograma **v2 vigente** — el usuario puede sostener **jornadas de 10 h resolviendo
      varias actividades el mismo día**: **100 h en 10 jornadas**, mié 19-ago → **mar 1-sep-2026**,
      1 dev full-stack, 4 hitos, 12 paquetes, 29 actividades. Las 10 jornadas cuadran exactas en
      10 h cada una (verificado por script antes de generar los documentos)
- [x] V30.2 `~/Desktop/Plan_de_Trabajo_Santa_Reyes.xlsx` (v2.0) — 8 hojas: Portada · Hitos ·
      **Plan diario** · Cronograma · **Carga por jornada** · Alcance · Guías genéticas · Supuestos
      y riesgos. Recalculado con Excel: **0 celdas con error de fórmula**; `Hitos!F9`=100,
      `Cronograma!G37`=100, `Guías!C10`=540, y la fila «HORAS POR JORNADA» da **10 en los 10 días**
- [x] V30.3 `~/Desktop/Plan_de_Trabajo_Santa_Reyes.docx` (v2.0) — 11 páginas, 14 secciones, con
      plan diario, trazabilidad requerimiento→actividad y hoja de aprobación. Verificado
      renderizando el PDF página por página
- [i] V30.4 **Los dos documentos comerciales presentan TODO el alcance como trabajo por ejecutar**,
      por decisión explícita del usuario, incluido lo que ya tiene base en el repo (silos,
      clasificación por ítems, guía genética, `Placa`/`Conductor`/`Sellos` en `MovimientoAves`).
      Los tiempos son cortos **porque** esa base existe; no se declara así hacia afuera. El estado
      técnico real está en la §2 del plan

## Ejecución (sin arrancar)

- [!] V30.5 **Aprobación del cliente** del alcance, el cronograma y los supuestos (§13 del Word).
      Nada arranca antes de esto
- [~] V30.6 Santa Reyes debe entregar, **a más tardar el mar 18-ago-2026 (un día antes del
      inicio)**, la estructura física real (núcleos, galpones, silos, bodegas) y los códigos ERP
      (CO, bodegas, ubicaciones, centros de costo). ⚠️ En el plan de 2 semanas **F1.2 corre el
      día 1**: no hay holgura para esperarlos. Es el riesgo **Alto** #1 del documento
- [ ] V30.7 H1 · Fundaciones: flags en `companies` + catálogo de ítems + silo en el form de ingreso
      a granja + homologación ERP + seed de las 5 guías genéticas (540 filas)
- [ ] V30.8 H2 · Ciclo de vida del ave: semanas por raza (hoy hardcodeadas en
      `modal-seguimiento-diario.component.ts:1463`), consumo solo hembras, ocultar machos y error de
      sexaje **en UI** (⚠️ no borrar del modelo: lo consumen los saldos), tipos de inventario
- [ ] V30.9 H3 · Huevos: renombrar incubables→sin clasificar, los 7 ítems, primera postura por raza
      con vigencia ≤ semana 22, PNC por catálogo (⚠️ sin tocar las 11 columnas físicas), eficiencia
      cuadrada contra el total de granja
- [ ] V30.10 H4 · Traslados (días 9-10): aves (exponer `Placa`/`Conductor`/`Sellos` en postura) y
      huevos (bodega destino desplegable), + pruebas de no regresión multipaís + despliegue
- [i] V30.11 El **acompañamiento** (F12.2, semana del 2 al 8-sep) quedó **fuera de las 10 jornadas**,
      declarado como bajo demanda. Si se promete dentro del plan, la entrega se corre un día
- [i] V30.12 **Bloque commiteado el 18ago26 21:00** — hasta ese momento vivía sólo en el working tree,
      y el plan al que enlaza (`santa_reyes_requerimientos_italapp_plan.md`) estaba **sin trackear**: un
      commit de cualquier otra sesión lo habría borrado sin dejar rastro. Los dos entregables comerciales
      que declaran V30.2 y V30.3 **existen** en el Escritorio (`.xlsx` 35 kB, `.docx` 27 kB), verificado.
      Commitear **no** cierra el bloque: la ejecución (V30.7-V30.10) sigue abierta
- [!] V30.13 ⏰ **El cronograma arranca mañana y los dos destrabes siguen sin resolverse.** Hoy es
      **mar 18-ago-2026**: es el último día del plazo de V30.6 (estructura física + códigos ERP) y el día
      previo al inicio (mié 19-ago), con **V30.5 —la aprobación del cliente— todavía sin dar**. Como
      F1.2 corre el día 1 y el plan no tiene holgura, cada día que se demore cualquiera de los dos
      corre la entrega del **1-sep** en la misma medida. Es el riesgo Alto #1 del documento,
      materializándose: decisión tuya, ningún agente lo destraba

---

# V39 · B1 — revocación de sesión (`jti` + `sesiones_activas`), la deuda más urgente de la PWA (18ago26)

**Plan:** [fase_de_desarrollo/b1_revocacion_sesion_plan.md](fase_de_desarrollo/b1_revocacion_sesion_plan.md) (`5d54dbd`, STEP 1 ya escrito).
**Bloque propio.** Cierra el pendiente **B1** del bloque *«PWA — deuda conocida»*: hasta hoy un JWT
emitido era **irrevocable** —una tablet perdida seguía entrando hasta que venciera— y cambiar la
contraseña o desactivar al usuario **no invalidaba nada**.

## Orden de trabajo (§7 del plan)

- [x] V39.1 `RevocacionSesionCalculos` + **26 casos** xUnit (los 14 del plan, varios como `[Theory]`).
      Verde antes de tocar nada más
- [x] V39.2 Entidad `SesionActiva` + configuración + `DbSet` + migración `20260819001837_AddSesionesActivas`,
      idempotente (`IF NOT EXISTS`), DDL puro, sin FK ni DML. Probada **dentro de una transacción con
      ROLLBACK, dos pasadas**: la segunda avisa `already exists, skipping` y el único por `jti` rechaza
      el duplicado. Después aplicada en la BD local (era la única pendiente)
- [x] V39.3 `ISesionActivaService` + `SesionActivaService` con `IMemoryCache` (60 s si la sesión vale,
      hasta el `exp` si está muerta) y limpieza perezosa de vencidas de más de 30 días
- [x] V39.4 `jti` + `iat` en `AuthService`, registro de la sesión al emitir el token, y revocación en
      **los tres** caminos de cambio de contraseña + al desactivar y al eliminar al usuario
- [x] V39.5 `OnTokenValidated` + `OnChallenge` en el `JwtBearerEvents` **que ya existía** + DI
- [x] V39.6 `SessionController`: el heartbeat toca `last_seen_at` (con throttle) y su respuesta queda
      **byte a byte igual**; nuevos `mias`, `mias/{id}`, `de-usuario/{userId}` y `{id}` para administración
- [x] V39.7 Front: `device-id.funcion` (extraída del privado del outbox, que ahora delega) + `X-Device-Id`
      en el interceptor + `esSesionRevocada` + motivo `'revocada'` + `session-timeout`
- [x] V39.8 Front: modal «Sesiones activas» en `user-management` + «Mis dispositivos» en `profile`,
      los dos con `changeDetection: Eager` explícito y con `ConfirmDialogService` / `ToastService`
- [x] V39.9 `JwtSettings:DurationInMinutes` 60 → **960** en los dos `appsettings` (último: §6.4)
- [x] V39.10 Validación: `dotnet build` **0 errores / 0 warnings** · `dotnet test` **2.884 pasan** (+26)
      · `yarn build` **0 errores** (initial 992,16 kB; los 2 componentes nuevos van en chunks lazy)
      · `ng test` **573 pasan** (+24) · `verificar-lista-cacheable.js`: 87 endpoints, **0 sin decisión**
      (los 3 nuevos cuelgan de `session`, ya excluido ⇒ no hubo que tocar el gate)

## Lo que NO hace (§6.1 del plan)

- [i] V39.11 **Sin refresh token**: descartado con argumento (§1.3). No sirve offline —renovar exige
      red— y agrega un secreto de larga vida a un storage que hoy es JSON plano
- [i] V39.12 **No cifra el storage local** (B9/D3) ni rota las llaves (B8): el JWT de la tablet sigue
      siendo legible con DevTools. B1 garantiza que el aparato **no vuelve a entrar**, no que lo que ya
      se llevó esté a salvo (§6.2)
- [ ] V39.13 **Cerrar la ventana de gracia** de los tokens sin `jti`: hoy `Evaluar` devuelve `Legado` y
      los acepta. Como el token viejo dura lo que duró, a la hora del despliegue ya no queda ninguno; el
      cambio es borrar esa rama y dejar que caigan en `NoRegistrada`. Commit **posterior y explícito**,
      con su casilla propia — no «cuando haya tiempo»
- [~] V39.14 **Subir la vigencia en producción**: la TaskDef viva trae `JwtSettings__DurationInMinutes=60`
      como variable de entorno y **pisa** el `appsettings` (ver V39.15). Cambiarla a `960` es un paso en
      AWS y, según §6.4, sólo **después** de verificar la revocación en prod

## 🔑 Lo que apareció al hacerlo

- [i] V39.15 🔴 **El plan daba por hecho que `appsettings` manda la vigencia en prod, y no.** La TaskDef
      **viva** en ECS trae `JwtSettings__DurationInMinutes=60` (verificado con
      `aws ecs describe-task-definition`), y la variable de entorno gana sobre el JSON. O sea que el
      60→960 de V39.9 **sólo aplica en local**. Los `ecs-taskdef*.json` del repo (12 copias) son
      documentación: el workflow **no** los usa — hace `describe-task-definition` de la viva y sólo le
      cambia la imagen
- [i] V39.16 **Y así está bien**: es el orden que exige §6.4 —revocación primero, vigencia larga
      después—. Con la TaskDef sin tocar, el deploy lleva la revocación y **no puede** emitir tokens de
      16 h por accidente. Lo que hay que decir en voz alta: mientras esa variable siga en 60, **el
      defecto §0.6.2 sigue vivo en prod** (el `authGuard` expulsa al minuto 61 sin señal)
- [i] V39.17 **Tres caminos cambian la contraseña, no dos.** El plan nombraba `ChangePasswordAsync` y
      `AdminResetPasswordAsync`; existe además `ValidateAndUsePasswordResetTokenAsync` (el enlace por
      correo), que es **justo** el que se usa cuando alguien perdió el control de su cuenta. Los tres revocan
- [i] V39.18 **`BaseHttpService.delete` no admite cuerpo**, así que el motivo de la revocación —lo que
      queda en la auditoría— se manda con `HttpClient` directo y las mismas cabeceras autenticadas
- [i] V39.19 ⚠️ **Casi me llevo puesto el tracker.** Un script que reescribía el archivo entero
      (apertura en modo `w`) reventó a mitad por un emoji mal escapado y lo dejó en **0 bytes**, con el
      bloque V30 de otra sesión adentro y sin commitear. Se recuperó completo del volcado de la lectura
      previa (`git diff` = 50 inserciones, **0 borrados**). **Regla:** sobre un archivo compartido y
      sucio se **agrega**, o se escribe a un temporal y se concatena; nunca se trunca para reescribir

## Smoke §5.3 — contra el backend local, con la BD limpia al terminar

- [x] V39.20 **Login**: el JWT trae `jti` e `iat`, vigencia **960 min**, y aparece **una** fila con su
      `device_id` (`tablet-smoke-b1`), IP y user-agent
- [x] V39.21 **Heartbeat**: marca `last_seen_at` la primera vez y el segundo **no reescribe** (throttle
      de 5 min verificado contra la fila, no contra el log)
- [x] V39.22 **Revocar** desde el super admin ⇒ el token cae con **401** y cuerpo
      `{"errorCode":"sesion-revocada",…}` + cabecera `X-Auth-Failure` — el contrato que el front lee
- [x] V39.23 **Fail-closed**: un `jti` bien firmado pero **sin fila** también da 401. Es la diferencia
      con una lista negra, y se probó en vez de declararse
- [x] V39.24 **Ventana de gracia**: un token **sin `jti`** (minteado con la llave) pasa con 200
- [x] V39.25 **Cambio de contraseña** ⇒ sesiones vivas 1 → 0, y el token previo da 401
- [x] V39.26 **Usuario desactivado** (`PATCH /api/Users/{id}`) ⇒ sesiones vivas 1 → 0, y su token da 401
- [x] V39.27 **El PAT (`sk_…`) sigue intacto**: `GET /api/tickets/tablero` e `/indicadores` responden
      **200** y el conteo de `sesiones_activas` **no se mueve** — el esquema ServiceToken no pasa por
      `OnTokenValidated`, que era el riesgo de regresión para los crones
- [x] V39.28 **`/api/session/mias`** lista la sesión con `esLaActual=true` y **etiqueta de 8 caracteres**:
      el `jti` entero no viaja en el listado
- [x] V39.29 Usuario de smoke, PAT y filas de sesión **borrados** (`sesiones_activas` en 0). Backend
      local **apagado**; `:5002` y `:4200` libres
- [x] V39.30 ~~Faltan los smokes 9 y 11 del plan~~ — el pendiente **no desaparece**: es **S-4** del
      bloque «PWA — lo único que falta probar en un equipo real», donde vive una sola vez junto con
      los otros tres escenarios que piden aparato

---

# PWA — lo único que falta probar en un equipo real (consolidado 18ago26)

Seis bloques de la serie PWA (F-3, F-4, F-2, F-5, multi-slot 7 y 8) terminaron con **la misma línea
pendiente escrita seis veces**: «falta el smoke en un equipo real». Eso mantenía ~460 líneas de
trabajo terminado dentro del tracker sin que quedara nada por hacer en el código. Se juntan acá, con
el escenario concreto de cada uno, y los bloques de origen se archivan.

**Precondición común:** instalar la PWA en un Android y hacer el **alistamiento con red** (entrar una
vez y visitar las pantallas que se van a usar). Sin eso la caché está vacía y ninguno de estos
escenarios significa nada. Nada de F1/F2/F3 se probó nunca fuera de local.

- [~] S-1 **Dos operarios turnándose sin red** — el caso que motiva el multi-slot entero: A trabaja,
      aparca con su PIN, entra B, y cada uno ve **su** caché y **su** cola. Cubre lo que quedó
      pendiente en F-3 (el push no firma trabajo ajeno), F-5 (cerrar sesión no borra el alistamiento
      de los demás) y los pasos 7 y 8 del multi-slot
- [~] S-2 **`/diagnostico` con una sesión ajena activa y sin ninguna sesión**: la fila aparece **sin
      payload**, sin «Copiar captura» y sin «Descartar» (F-4). Necesita dos sesiones reales
- [~] S-3 **Más de 60 min de reloj offline** — o un build de prueba con `DurationInMinutes` bajo — para
      ver que el `authGuard` **ya no expulsa** dentro de la jornada (F-2). ⚠️ Con la vigencia de
      producción todavía en 60 min (V39.14), este escenario **falla en prod hoy**: es la comprobación
      que justifica subirla
- [~] S-4 **Revocar una sesión con capturas sin enviar**: al volver la red, 401 con motivo «revocada» y
      las capturas **siguen en `/diagnostico`** (B1, smoke 9 de su plan). Y con dos device-id distintos
      detrás de la misma IP, las dos colas drenan **sin bloquearse entre sí** (smoke 11)

- [i] Ninguno lo cierra un agente: los cuatro piden un aparato, o dos sesiones, o DevTools en modo
      offline. Son la deuda de verificación de toda la serie PWA, no de un bloque suelto

---

# V40 · V25.7.6 — el número fino del kardex de bultos, y el doble conteo que la decisión no contemplaba (18ago26)

**Cierra V25.7.6** (*«falta el número fino»*), el insumo que V19.2.1 pedía para decidir entre **(a)**
restar el consumo de todos los lotes de la granja y **(b)** dejarlo con el aviso al lado.
**Bloque propio.** No se tocó ni una línea del cálculo: es una medición de solo lectura.

**Script:** [`backend/sql/verificar_kardex_bultos_por_lote_padre.sql`](backend/sql/verificar_kardex_bultos_por_lote_padre.sql)
— reproduce en SQL puro `ReporteContableService.ObtenerDatosBultosUnificadoAsync` +
`ReporteContableBultosCalculos.AcumularSaldos`: la ventana de `dias_alimento_previo_encaset`, la
clasificación de `movement_type`, el factor de 40 kg/bulto, las filas «solo bultos» de C1 y el
recorte a 0 con su regla de reinicio por día calendario.

## V40.1 — La cifra, por lote padre (BD local `sanmarinoapplocal`, empresa 1, corte 18-ago-2026)

| Granja | Lote | id | entradas | retiros | cons. propio | cons. ajeno | **saldo hoy** | **(a)** | **sin doble conteo** |
|---|---|---|---|---|---|---|---|---|---|
| LA ESMERALDA | A374A | 114 | 4.348,2 | 3.830,0 | 536,0 | 3.137,8 | **509,7** | 494,9 | 518,2 |
| LA ESMERALDA | A374A | 116 | 4.348,2 | 3.830,0 | 2.008,9 | 1.664,9 | **494,9** | 494,9 | 518,2 |
| LA ESMERALDA | A374B | 115 | 4.348,2 | 3.830,0 | 1.128,9 | 2.544,9 | **505,9** | 494,9 | 518,2 |
| LA ESMERALDA | A374B | 117 | 4.348,2 | 3.830,0 | 0,0 | 3.673,8 | **518,2** | 494,9 | 518,2 |
| MANGOS | S369A | 142 | 6.373,6 | 5.997,2 | 2.976,0 | 3.021,2 | **0,0** | 0,0 | 376,4 |
| MANGOS | S369A | 144 | 6.373,6 | 5.997,2 | 0,0 | 5.997,2 | **376,4** | 0,0 | 376,4 |
| MANGOS | S369B | 143 | 6.373,6 | 5.997,2 | 3.021,2 | 2.976,0 | **0,0** | 0,0 | 376,4 |
| MANGOS | S369B | 145 | 6.373,6 | 5.997,2 | 0,0 | 5.997,2 | **376,4** | 0,0 | 376,4 |

En bultos de 40 kg, fase Levante. **MIRALINDO no aparece**: sus 2 padres (146 A402A, 147 A402B,
encaset jul-2026) no tienen ni una fila de seguimiento ni un movimiento de alimento en la ventana ⇒
su sección BULTO sale vacía y el problema todavía no los toca. **NIZA III** (K345A, 1 padre) entró
como control y no está afectada.

- [x] V40.2 **El delta de la opción (a), lote por lote**: LA ESMERALDA **14,8 · 0,0 · 11,0 · 23,3**;
      MANGOS **0,0 · 376,4 · 0,0 · 376,4**. Sumados por granja: **49,1** y **752,8** bultos
- [x] V40.3 🔑 **La opción (a) hace lo que promete: converge.** Los 4 padres de LA ESMERALDA pasan de
      cuatro saldos distintos (509,7 · 494,9 · 505,9 · 518,2) a **uno solo, 494,9**, y los 4 de MANGOS
      a **0,0**. Es exactamente lo que anticipó V25.7.4: (a) no devuelve el número por lote —imposible
      sin dato de lote en las entradas— sino un número **de granja bien calculado**
- [x] V40.4 **El daño de sumar, medido**: los 4 reportes de LA ESMERALDA suman hoy **2.028,7** bultos
      donde la granja tiene **518,2**; los de MANGOS suman **752,8** donde tiene **376,4**

## 🔴 V40.5 — Lo que apareció al reproducir la query: el consumo se resta DOS veces

- [i] V40.6 🔴 **`retiros` y `consumo` son el mismo alimento.** El saldo es
      `entradas − traslados − retiros − consumoH − consumoM`, donde `retiros` son los movimientos
      `Consumo` del inventario y `consumoH/M` el consumo del seguimiento diario. **Los movimientos
      `Consumo` los escribe el propio seguimiento**: su `reference` dice literal
      `«Seguimiento lote levante #1103 2025-08-3…»` y `«Consumo diario levante - Lote A374A»`
- [i] V40.7 **Medido por granja**, en kg de alimento: MANGOS **239.886,2 en inventario vs 239.886,2
      en seguimiento — idénticos al gramo**, 504 movimientos, todos con referencia de seguimiento.
      LA ESMERALDA 153.198,5 vs 146.952,5: 149.918,5 vienen de seguimientos (en dos formatos de
      referencia), **3.280,0 de una salida manual** (*«se realiza salida por entrada…»*) y ~2.966 de
      deriva entre los dos escritores
- [i] V40.8 🔴 **Esto reencuadra la decisión entera.** El saldo verificable contra el inventario es
      `entradas − traslados − retiros` = **518,2** (LA ESMERALDA) y **376,4** (MANGOS) — el mismo para
      los 4 padres, sin restar el consumo por segunda vez. Consecuencias:
      · **la opción (a) empeora el doble conteo**, no lo arregla: lleva LA ESMERALDA a 494,9 (−23,3
        contra el inventario) y deja MANGOS clavado en **0,0** para los 4 padres
      · los saldos **0,0** de los lotes 142 y 143 no dicen «el galpón está vacío»: son el recorte a 0
        de un acumulado que llegó a **−2.599,6** por restar 2.976 bultos dos veces
      · los únicos saldos que hoy dan bien son los de los padres **sin** seguimiento propio (117, 144,
        145), y dan bien **por accidente**: no tienen consumo que duplicar
- [i] V40.9 **Por qué no se arregló acá.** Mueve una columna de un reporte contable en uso —lo mismo
      que V19.2.1 dejó explícitamente como decisión de producto—, y el arreglo correcto ya no es
      elegir entre (a) y (b) sino **(c) dejar de restar el consumo dos veces**. Pide su propio plan,
      su espejo en `Application/Calculos/` con tests y el smoke doble. Queda planteado, no ejecutado

## 🟡 V40.10 — Segundo hallazgo: el resumen semanal no arrastra el saldo entre semanas vacías

- [i] V40.11 `ObtenerSaldoAnteriorSemana` (`ReporteContableService.cs:1064`) mira **sólo** la semana
      `actual − 1`. Si esa semana no tiene filas, devuelve `0` en vez de caminar hacia atrás hasta la
      última con datos ⇒ el resumen semanal y el detalle diario del **mismo reporte** se contradicen.
      Medido en el lote **114**: la semana 44 arranca con `saldoAnterior = 0` y cierra en **259,9**
      (1.481,2 − 1.221,3) mientras la última fila diaria dice **509,7**; entre la semana 37 y la 44
      hay 6 semanas sin filas. Igual en el **116** (259,9 vs 494,9). Los otros 6 lotes coinciden
- [i] V40.12 No se tocó, por la misma razón que V40.9: mueve una columna del reporte contable

## V40.13 — Validación

- [x] V40.14 **La reproducción se contrastó contra el endpoint real**, no se declaró:
      `GET /api/ReporteContable/generar?lotePadreId=<n>&faseLote=Levante` con el backend local en
      `:5002`. En **8 de 8** lotes padres coinciden el número de filas, las entradas, los retiros, el
      consumo y el **saldo de bultos de la última fila diaria** — los 8 valores de la columna
      «saldo hoy» de V40.1 salen del endpoint tal cual
- [i] V40.15 ⚠️ **Ojo con el campo que se compara**: `saldoBultosFinal` (resumen semanal) **no** es el
      saldo diario acumulado — es el defecto de V40.11. La comparación válida es contra
      `datosDiarios[].saldoBultos` de la última fila
- [x] V40.16 **Cero escrituras**: el script es `SELECT` + tablas `TEMP`; los `DROP TABLE IF EXISTS`
      sólo tocan las temporales de la propia sesión. Ninguna fila de negocio se modificó
- [x] V40.17 Backend local **apagado** al terminar: `:5002` y `:4200` sin listener (verificado con
      `netstat`, no con el log)
- [i] V40.18 El script suma el consumo de levante **y** de producción; el reporte lee sólo el de la
      fase pedida. En los 10 padres afectados da igual (son todos de levante), pero para un lote con
      las dos fases —como el 13 de NIZA III— el script y el reporte **no** son comparables

## V40.19 — Además: tres marcas obsoletas del multi-slot

- [x] V40.20 «Sesiones multi-slot por dispositivo» seguía como `- [ ]` en el punto de retoma, y otras
      dos afirmaciones (`- [i]`) decían que la tablet guarda **una sola** sesión. Las tres las cerró
      **V34-V38** (`9b6b157` → `6e4fe7f`): el llavero
      (`frontend/src/app/core/auth/llavero-sesiones.service.ts`) aparca sesiones cifradas por slot y
      activar una escribe su blob en `auth_session`, así que el segundo operario **ya entra sin red**.
      Es el mismo patrón que documentó `30fe5a2`: pendientes marcados después de resueltos. Lo que
      queda del multi-slot no es código, es el smoke **S-1**

---

# V41 · Arreglado el doble conteo del kardex de bultos, en las DOS ramas (19ago26)

**Plan:** [`fase_de_desarrollo/doble_conteo_kardex_bultos_plan.md`](fase_de_desarrollo/doble_conteo_kardex_bultos_plan.md)
**Cierra** el defecto que midió V40 y, con él, **la decisión V19.2.1** (abierta desde el 17-ago
esperando elegir entre dos opciones que la medición mostró insuficientes).
**Bloque propio.**

## 🔑 V41.0 — El punto profundo: `retiros` y `consumo` están en GRANOS distintos

- [i] V41.0.1 **`retiros` es de la GRANJA y `consumo` es de ESTE lote padre.** En el módulo unificado
      los `retiros` son los `Consumo` de `inventario_gestion_movimiento` — que **los escribe el propio
      seguimiento**, de **todos** los lotes de la granja. Restar los dos descuenta el consumo de ese
      padre **dos veces**
- [i] V41.0.2 **Por qué no se veía**: `AcumularSaldos` recorta cada día con `Math.Max(0m, …)`. El
      acumulado real de los lotes 142 y 143 (MANGOS) llegaba a **−2.599,6** y **−2.644,7** bultos y se
      publicaba como **0,0** — no como un negativo, sino como un galpón vacío
- [i] V41.0.3 **La invariante ya estaba escrita, en dos lugares**: el módulo viejo con el TIPO
      (`InventoryMovementType.ConsumoSeguimiento`, *«EXCLUIDOS de los 4 buckets del ReporteContable»*)
      y engorde con `AfectaSaldoAlimentoEngorde` (*«Contarlo acá lo descontaría dos veces»*). El
      unificado colapsó los dos conceptos en un solo `movement_type='Consumo'` y la traducción lo mandó
      entero a `Retiro`

## 🔴 V41.1 — El arreglo que parecía obvio y estaba MAL

- [i] V41.1.1 La primera implementación fue la de engorde: **excluir de `retiros` el consumo escrito
      por un seguimiento**. Compilaba y pasaba los 2.922 tests. **Y estaba mal**: al quitar `retiros` se
      pierde el consumo de los **otros** padres de la granja, que era justo lo que ese término
      aportaba. El saldo se disparaba a **3.730,2 · 2.257,3 · 3.137,3 · 4.266,2** y **dejaba de
      converger**
- [i] V41.1.2 🔑 **Lo delató calcular el número esperado ANTES de correr el smoke, no un test.** La
      lección: *engorde puede restar el consumo del seguimiento porque su kardex es **por galpón**, al
      mismo grano; el Contable no, porque el suyo es **por granja**.* Copiar el patrón sin comparar los
      granos era el error

## V41.2 — Las DOS ramas duplican, con firmas distintas

| | rama LEGACY (`farm_inventory_movements`) | rama UNIFICADA (`inventario_gestion_movimiento`) |
|---|---|---|
| quién duplicaba | el **front**, `postExit` (`reason='Consumo diario'` + `destination='Consumo'`) | el **backend**, `movement_type='Consumo'` |
| cuánto (empresa 1) | 252 movs · 131.278,3 kg | 930 movs · 420.016,0 kg |
| grano de `retiros` | mezcla: espejos del consumo + salidas reales | **la GRANJA entera** |
| arreglo | **excluir los espejos**; el consumo lo sigue aportando el seguimiento | **no restar el consumo del seguimiento**; `retiros` ya lo trae, y mejor |

- [x] V41.2.1 **`EsConsumoYaContabilizadoPorSeguimiento(reason, destination)`** — rama legacy.
      **Portado de `b853e95`** (8-ago-2026, rama `claude/heuristic-perlman-f10ea4`), que arregló esta
      rama con plan y tests y **nunca llegó a `main`**. Exige los DOS campos: la granja 20 tiene un
      `Exit` REAL de 3.280 kg con `destination='Devolución'` que sí tiene que restar
- [x] V41.2.2 **`DeltaDelSaldo(fila, retirosYaTraenElConsumo)`** — rama unificada. El saldo pasa a ser
      `entradas − traslados − retiros`
- [x] V41.2.3 **Es una decisión POR RAMA, no un cambio plano.** `DeltaDelSaldo(..., false)` devuelve la
      fila **idéntica**: un cambio plano en `AcumularSaldos` habría roto Demo, Ecuador y Panamá para
      arreglar Sanmarino y Santa Reyes

## V41.3 — El número, verificado contra el endpoint real

| Granja | Lote | id | antes | **después** |
|---|---|---|---|---|
| LA ESMERALDA | A374A | 114 | 509,7 | **518,2** |
| LA ESMERALDA | A374A | 116 | 494,9 | **518,2** |
| LA ESMERALDA | A374B | 115 | 505,9 | **518,2** |
| LA ESMERALDA | A374B | 117 | 518,2 | **518,2** |
| MANGOS | S369A | 142 | **0,0** | **376,4** |
| MANGOS | S369A | 144 | 376,4 | **376,4** |
| MANGOS | S369B | 143 | **0,0** | **376,4** |
| MANGOS | S369B | 145 | 376,4 | **376,4** |
| NIZA III (1 padre) | K345A | 13 | 3.123,5 | **3.158,6** |

- [x] V41.3.1 **Los 4 padres de una granja convergen a UN saldo.** Es lo que la opción (a) de V19.2.1
      perseguía, conseguido sin restar consumo ajeno — y sin el efecto que la habría hundido
- [i] V41.3.2 **Lo que NO resuelve, dicho:** el saldo sigue siendo un número **de granja** en un reporte
      **por lote** ⇒ el aviso de V19.1 sigue haciendo falta. Lo que desaparece es la **contradicción**
      de mostrar 4 saldos distintos del mismo kardex

## V41.4 — Lo que NO entró, y por qué

- [i] V41.4.1 **El recorte a 0 de `AcumularSaldos`.** Su doc dice que «el acumulador interno conserva el
      negativo» y **no lo conserva** (el carry entre días contiguos relee el valor recortado). Contrato
      incumplido, pero con este arreglo el recorte **deja de activarse** en los 9 lotes medidos ⇒
      arreglarlo no cambia nada acá y movería la rama vieja sin medición que lo respalde
- [i] V41.4.2 **`ObtenerSaldoAnteriorSemana` (V40.11).** No es criterio propio: **`b853e95` ya lo midió
      el 8-ago y lo dejó afuera con número** — arreglarlo cambia **72 encabezados** y hace que 50 de las
      80 semanas del lote 13 dejen de mostrar 0. **Decisión de producto, no de este arreglo**
- [i] V41.4.3 **El escritor del front** (`modal-seguimiento-engorde.component.ts:1833,1867`) sigue
      posteando al kardex legacy. Sacarlo sin medir puede dejar a Colombia sin descuento
- [i] V41.4.4 **La deriva entre los dos escritores del mismo consumo**: LA ESMERALDA 149.918,5 kg
      (inventario) vs 146.952,5 kg (seguimiento) = 74,2 bultos; MANGOS **0,0** (239.886,2 de los dos
      lados). Dos espejos del mismo hecho que ya no coinciden

## V41.5 — Validación

- [x] V41.5.1 `dotnet build` **0 errores**, 9 warnings (los mismos preexistentes de V25.8.5)
- [x] V41.5.2 `dotnet test` **2.896 pasan**, 0 fallos (+12 nuevos: 5 legacy, 3 unificada, 4 de firma)
- [x] V41.5.3 **Smoke contra el endpoint real**, backend local en `:5002`: **9 de 9 lotes padres OK**
      contra el número calculado de antemano. Filas, entradas, retiros y consumo idénticos; sólo se
      movió el saldo
- [x] V41.5.4 **Rama vieja sin regresión**: los 5 lotes padres de **Demo** (flag apagado) siguen en
      `entradas 0 · retiros 0 · saldo 0`, igual que antes
- [i] V41.5.5 **El gate `verificar_paridad_saldo_engorde.sql` NO aplica**: compara la salida de
      `fn_seguimiento_diario_engorde`, y este cambio es C# del Reporte Contable — no toca ninguna fn SQL
      ni ningún `*SaldoAlimento*`. Correrlo sería teatro
- [x] V41.5.6 Backend local **apagado**; `:5002` y `:4200` sin listener (verificado con `netstat`)

---

# V42 · Auditoría de los 45 pendientes contra el código de hoy — un tercio era ruido (19ago26)

Cada pendiente `- [ ]` / `- [!]` / `- [~]` verificado contra el código, la BD local, git, GitHub
Actions y AWS. **15 de 45 estaban YA_RESUELTOS u OBSOLETOS** (33 %), 28 VIVOS y 2 duplicados.
**Bloque propio.** Los bloques de origen **no se editaron** —son de otras sesiones—: acá queda el
veredicto con su evidencia, y quien retome cada bloque decide.

## 🔴 V42.0 — Lo que reordena todo: **la PWA YA ESTÁ DESPLEGADA**

- [i] V42.0.1 🔴 **El encabezado en rojo del bloque «PWA — PUNTO DE RETOMA» es FALSO desde el 18-ago.**
      Dice *«La PWA sigue SIN desplegarse … `ngsw.json` da 404»*. Medido hoy: el merge se hizo el
      **18-ago 14:45 -05 (PR #74)**, el run `32178414139` salió **success**, `/version.json` =
      `2026-08-18T19:54:28.749Z`, y `ngsw.json` · `manifest.webmanifest` · `ngsw-worker.js` responden
      **200**. Mata 3 pendientes (líneas 1010, 1116, 1117) y la advertencia
- [i] V42.0.2 **Verificación post-deploy: corrida y en verde.** TaskDef `sanmarino-back-task:160`,
      imagen `backend:79aeccfa…` = `git rev-parse main-produccion`, `rolloutState COMPLETED`, 1 tarea
      corriendo. **No hubo rollback silencioso**
- [i] V42.0.3 **Lo único que falta desplegar es `c9a7349` (V39 · B1, revocación de sesión)** — el único
      feature de `main` fuera de `main-produccion`. Los otros 3 commits de diferencia son docs

## V42.1 — Pendientes YA_RESUELTOS u OBSOLETOS (no hay nada que hacer)

| Línea | Pendiente | Veredicto | Evidencia |
|---|---|---|---|
| 1010 · 1116 · 1117 | Merge `main` → `main-produccion` para desplegar la PWA | **YA_RESUELTO** | PR #74, run success, `/version.json` 18-ago 19:54Z |
| 1120 | Verificación post-deploy obligatoria | **YA_RESUELTO** (corrida acá) | TaskDef 160 ↔ imagen ↔ version.json |
| 1122 | Invariante de `company_permissions` antes/después | **OBSOLETO** | pide una foto «antes» del 18-ago que ya no es tomable |
| 1125 | Avisar del menú «Lote Reproductora» | **OBSOLETO** | la migración entró el 12-ago (`6980fa3`); el aviso llega 7 días tarde |
| 264 | Lote 132 (19.387 vs 19.187) | **YA_RESUELTO** | BD = **19.187** · `fn_cuadre_aves_engorde(NULL)` → 0 y 0 |
| 483 | Limpiar los 15 días traslapados de K345 | **YA_RESUELTO** | ejecutado en V25.8.3 (`6ce89cc`) |
| 532 | ¿Sale el tile «Venta Engorde»? | **YA_RESUELTO** | decidido en V25.7.1: se queda |
| 581 | Santa Reyes pierde Migraciones Masivas | **YA_RESUELTO** | decidido y ya aplicado en datos |
| 941 | Menú «Lote Reproductora» a 3 roles | **YA_RESUELTO** | 0 filas en `role_menus` y `company_menus` |
| 1885 | V25.8.7 «Falta desplegar» las 2 migraciones | **YA_RESUELTO** | entraron con el PR #74 |
| 272 | Cerrar el grupo A (39 lotes de Ecuador) | **OBSOLETO** | la lista de 39 no existe; queda un caso más chico |
| 472 | Corte levante/producción 24 vs 25 semanas | **la decisión YA se tomó** | V25.6.2 |
| 1716 | V20.4.1 decisión sobre el lote 12 | **YA_RESUELTO** como decisión · **duplicado** de V25.5.4 | |
| 908 | «Persistir la atribución como hecho» | **DUPLICADO** de V27.1 | |
| 1649 | V19.2.1 opción (a) vs (b) | **OBSOLETO** | V40.8 midió que (a) empeora; lo cierra **V41** |

## V42.2 — Pendientes VIVOS, con lo que los destraba

- [i] V42.2.1 **Nada de esto lo puede cerrar un agente hoy.** El reparto: **4** esperan al admin de
      Microsoft 365 (el correo sigue roto: último envío exitoso **3-jun-2026**, 85 fallidos, el más
      reciente **17-ago**), **1** el merge+deploy de B1, **1** un paso en AWS, **2** la aprobación del
      cliente de Santa Reyes, **4** un Android y dos operarios (S-1 a S-4), **1** secretos de prod (B8),
      y el resto decisiones de negocio sobre datos de producción
- [i] V42.2.2 ⏰ **V39.14 confirmado contra AWS**: la TaskDef 160 —la que corre— sigue con
      `JwtSettings__DurationInMinutes=60`. Mientras siga así, **el `authGuard` expulsa al minuto 61 sin
      señal en producción**: la jornada offline de 16 h todavía no existe para el operario, aunque la
      PWA ya esté desplegada. El orden correcto sigue siendo B1 primero
- [i] V42.2.3 **V39.13** (cerrar la ventana de gracia) sigue **VIVO y bloqueado por el orden**: hay que
      desplegar B1 y verificar antes de borrar la rama `Legado`. ⚠️ Y trae una trampa: `SesionActivaService`
      devuelve `Legado` **también ante un fallo de BD** (fail-open deliberado) — borrar el estado sin
      distinguir los dos usos convertiría una caída de BD en un **logout masivo**
- [i] V42.2.4 **S-1 a S-4 ya son ejecutables por primera vez**: la PWA está en prod, así que los cuatro
      smokes que piden un aparato dejaron de estar bloqueados por el deploy
- [i] V42.2.5 **V30.5 / V30.6 / V30.13 (Santa Reyes) vencidos**: el cronograma arrancaba el 19-ago
      —hoy— y la aprobación del cliente y la estructura física siguen sin llegar
- [i] V42.2.6 **V30.10 está a medias sin que el tracker lo diga**: `Placa`/`Conductor`/`Sellos` ya
      existen en `MovimientoAves`; falta exponerlos en postura

## V42.3 — Lo que apareció y no era un pendiente

- [i] V42.3.1 🔴 **Una rama abandonada con el arreglo ya hecho.** `b853e95` (8-ago-2026, rama
      `claude/heuristic-perlman-f10ea4`) se titula literal *«fix(reportes): el saldo de bultos de
      postura restaba el consumo dos veces»* y trae plan, tests xUnit y gate sobre los 10 lote-padre ×
      2 fases. **No está en `main`.** Diez días después V40 volvió a descubrir el mismo defecto desde
      cero. Su parte legacy se **portó en V41.2.1**; el resto de su medición se conserva acá
- [i] V42.3.2 **Lección**: una rama `claude/*` con trabajo terminado y sin mergear es invisible para el
      tracker. Antes de abrir un defecto, `git log --all -S` sobre el síntoma cuesta 30 segundos
- [i] V42.3.3 **El resumen del encabezado quedó desactualizado** por esta misma auditoría: los conteos
      de «45 pendientes reales» incluyen los 15 de V42.1. No se reescribe acá para no pisar el trabajo
      de otras sesiones; quien depure el archivo tiene la tabla lista

---

# V43 · El arrastre semanal del kardex de bultos (19ago26)

**Plan:** [`fase_de_desarrollo/doble_conteo_kardex_bultos_plan.md`](fase_de_desarrollo/doble_conteo_kardex_bultos_plan.md) §4.4
**Cierra V40.11**, que V41.4.2 había dejado explícitamente afuera por ser decisión de producto.
**Decisión tomada por el usuario el 19-ago-2026**, con el radio de impacto sobre la mesa.
**Bloque propio.**

## V43.0 — El defecto

- [i] V43.0.1 `ObtenerSaldoAnteriorSemana` miraba **sólo** la semana `actual − 1`. Si esa semana no
      tenía filas devolvía `0` y la siguiente abría de cero — pero **el kardex de alimento es
      continuo**: una semana sin filas es un hueco del calendario, no un stock que se vació
- [i] V43.0.2 **El síntoma es una contradicción dentro del MISMO reporte**: en el lote 114 el
      encabezado de la semana 44 cerraba en **259,90** mientras la última fila de su propio detalle
      diario decía **518,23**. Entre la semana 37 y la 44 hay 6 semanas vacías

## V43.1 — Lo que entró

- [x] V43.1.1 **`ReporteContableBultosCalculos.SaldoAnteriorDeLaSemana(filas, semanaInicio)`** (nuevo,
      puro): el saldo del último día con fila **anterior al inicio de esta semana**, no sólo dentro de
      la previa. Dentro de un mismo día gana el mayor, que es la regla histórica (`Max`)
- [x] V43.1.2 El service delega y conserva un **fail-safe**: si la semana actual no está en la lista,
      el corte cae al día siguiente del fin de la anterior ⇒ comportamiento histórico exacto
- [x] V43.1.3 **Las aves NO se tocan.** Siguen leyéndose del último día de la semana anterior CON dato
      del lote: una fila solo-bultos no describe el inventario de aves. Son dos reglas distintas a
      propósito, y ahora el doc lo dice

## V43.2 — El radio de impacto, medido HOY (no heredado)

- [i] V43.2.1 **165 de 460 encabezados cambian (36 %)** en los 9 lotes padres de Sanmarino.
      Reparto: lote 13 → 39 · 144 y 145 → 27 c/u · 142 y 143 → 24 c/u · 114-117 → 6 c/u
- [i] V43.2.2 ⚠️ **La cifra de «72 encabezados» que citaba V41.4.2 era de `b853e95` (8-ago) y quedó
      obsoleta**: se midió antes de V41, sobre la rama legacy. Se volvió a medir de cero contra el
      endpoint real, con captura antes/después de los 460 encabezados
- [i] V43.2.3 **0 encabezados de AVES cambian.** Verificado empíricamente comparando
      `saldoAnteriorHembras/Machos` y `saldoFinHembras/Machos` en los 460: el arreglo no roza el
      inventario de aves
- [i] V43.2.4 **Lo que se ve distinto en pantalla**: las semanas **sin filas** dejan de mostrar `0,00`
      y muestran el saldo que la granja efectivamente tenía (lote 114, semanas 39 a 43: `258,32` en vez
      de `0,00`). Es el cambio visible que `b853e95` había marcado como decisión de producto

## V43.3 — Validación

- [x] V43.3.1 `dotnet build` **0 errores**, 9 warnings (los mismos preexistentes)
- [x] V43.3.2 `dotnet test` **2.902 pasan**, 0 fallos (+6)
- [x] V43.3.3 🔑 **La propiedad que el arreglo promete, verificada en los 9 lotes**: el último
      encabezado semanal y la última fila del detalle diario **coinciden exactamente** —
      518,23 · 518,23 · 518,23 · 518,23 · 376,42 · 376,42 · 376,42 · 376,42 · 3.158,59. Antes se
      contradecían en 2 de los 9
- [x] V43.3.4 Backend local **apagado**; `:5002` y `:4200` sin listener
