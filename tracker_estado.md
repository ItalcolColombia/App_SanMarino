# Tracker de estado

> **Depurado el 16-ago-2026** (47 bloques cerrados + 28 pendientes obsoletos), **revalidado contra
> el código** ese mismo día, y **limpiado de nuevo el 17-ago-2026** (V11): los bloques que quedaron
> 100 % `- [x]` y commiteados salieron del archivo y viven abajo, en una línea cada uno.
> Nada se perdió: el texto completo está en git (`git show <commit>:tracker_estado.md`); el tracker
> previo a la primera depuración, en `git show fd542b9:tracker_estado.md`.
>
> Regla de sesiones en paralelo: cada sesión toca **sólo su bloque**; los bloques nuevos van **al
> final**. ⚠️ **V8 (descuadres de alimento de Panamá) está reservada para otra sesión — no tocar.**

| Pend. | Bloque abierto | Quién lo destraba |
|---|---|---|
| 4 | Envío de correo: SMTP rechazado por política del tenant | **admin de Microsoft 365** |
| 4 | Referencia `Inicio` + liquidación de corridas anteriores (engorde) | **decisión de negocio** |
| 1 | Consolidado de sublotes — C12 cerrado; queda la pata de inventario | **operación**: ¿qué alimento eran esos 750 kg? |
| 2 | ItalJira: barrido de sobregiro de aves | **decisión** (correr el detector contra prod) |
| 2 | Reporte Contable — Selección en RESUMEN + Movimientos de Huevo | **decisión** (corte 24/25 sem · K345) |
| 1 | Migraciones Masivas — retirar tipos | **decisión** (¿sale «Venta Engorde»?) |
| 1 | Migraciones Masivas — sólo Sanmarino | **decisión** (¿Santa Reyes conserva el módulo?) |
| 2 | Lote cerrado que absorbe el ciclo siguiente (KM 86) | operación (cerrar por pantalla) |
| 6 | Auditoría «alimento previo al encaset» | **decisión** + gate multipaís |
| 4 | v16 de engorde — marca `para_proximo_ciclo` | rediseño (persistir la atribución) |
| 4 | PWA F3.1 — captura offline | fuera de alcance declarado (F4, B1, B8, B10) |
| 3 | PWA — auditoría de acceso offline | **decisión** + sesiones multi-slot |
| 3 | PWA — punto de retoma | **push + merge a `main-produccion`** |
| 6 | PWA — brecha para salir a producción | **push + merge** + B1/B8 |
| 1 | Gerencia: Panel de control | post-deploy manual (rol + menú en la UI) |
| 6 | Bitácora agosto 2026 (W/I · V3 · V5 · V7 · V8) | **V8 reservada** (6) — V7.27 lo cerró V12 |
| 5 | V12 · V7.27 — referencia de la doble validación | verificación en **prod** (¿hay filas viejas?) |

> **55 pendientes al 17-ago-2026** (eran 67 → 51 tras V11, y V12 cierra V7.27 pero deja 5 puntos
> declarados de lo que NO hizo). De esos, **~26 esperan una decisión del usuario, un admin externo o
> un deploy**, y el resto es código. **Ya no queda ningún smoke pendiente**: los dos que seguían vivos
> se corrieron en V11 y V12 corrió el suyo contra un clon. **V7.27 quedó cerrado en V12**, con el gate
> de paridad multipaís corrido antes y después (0 diferencias en las dos empresas).

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
- [ ] Conditional Access / Security Defaults: ¿bloquea legacy auth por ubicación o IP? Excluir el origen
- [ ] `Get-CASMailbox 'zootecnico@sanmarino.com.co' | Select SmtpClientAuthenticationDisabled` ⇒ debe dar `False`
- [ ] `Get-TransportConfig | Select SmtpClientAuthenticationDisabled` ⇒ debe dar `False`

### Pendiente del usuario — Camino B (sólo si el A no se puede)
- [ ] Migrar a OAuth 2.0 / Microsoft Graph. La implementación completa está en el commit `c7b6834`
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
- [ ] ⚠️ **Pendiente (decisión de negocio):** id 132 (19.387 vs 19.187, 200 aves) — activo y sin ventas, la conservación no discrimina; necesita el documento físico de encasetamiento
- [ ] ⚠️ **Pendiente (decisión de negocio):** ids 3, 4, 6, 8 — encaset 50.000 **y** `Inicio` de plantilla: los dos números son ficticios, cero movimientos. El detector no los ve porque compara `ih + im` sin mixtas

## Parte B — Liquidación de corridas anteriores: BLOQUEADA, no puede ir por migración
- [x] B1 🔴 Liquidar es una transacción de 5 pasos (estado + avance del ERP de granja + **copia congelada** + saldo + resumen). El código: *«sin copia no hay liquidación»*. Una migración SQL saltearía 4 de los 5
- [x] B2 🔴 El criterio «galpón con corrida posterior» alcanza 75 lotes e **incluye 22 de Panamá con 801.882 aves VIVAS** y seguimiento del 2026-08-03 (allá conviven varias corridas por galpón)
- [x] B3 Candidatos reales medidos — Ecuador: **39 con saldo 0** (grupo A) · 12 residuales < 1 % (602 aves) · 2 con saldo significativo (1.119 aves)
- [x] B4 Orden obligatorio verificado: el *Gate B1* impide editar `aves_encasetadas` de un lote liquidado ⇒ **corregir ANTES de cerrar** (por eso el lote 30 se corrigió primero)
- [ ] ⏸️ **Esperando confirmación:** cerrar el grupo A (39 lotes de Ecuador) recorriendo el endpoint real de cierre. Irreversible sobre producción ⇒ requiere OK explícito sobre la lista
- [ ] ⏸️ Grupos B y C (14 lotes con aves pendientes) — revisión aparte · Panamá **no se toca**

---

# Tracker — Consolidado de sublotes y paridad de reportes por fase

**Fecha:** 2026-08-06 · **Pedido:** «un lote padre puede tener varios sublotes con fechas de llegada
distintas; al unirlos el consolidado debe cuadrar. Validá en reportes y descargas qué falta por fase».

## El consolidado cuadra
- [x] K1 **Consolidado = suma de los tabs**, celda por celda: 240 celdas en levante y 240 en
      producción (10 campos × 24 semanas cada uno) · **0 diferencias**. La unión es por semana de
      EDAD, no por fecha, que es como la hace el informe
- [x] K2 Levante consolidado vs `Registro Semanal general`: **24/24** semanas × 8 métricas
- [x] K3 Producción consolidado vs `SEMANAL GENERAL`: **22/23** — la única celda son los 5 huevos

## Cuatro reportes de PRODUCCIÓN estaban caídos (los cuatro salieron al cargar un lote real)
- [x] R1 🔴 `POST /obtener` (diario y semanal) daba **500** — `Column 'PesoHuevo' is null`. La entidad
      declaraba `peso_huevo` no anulable y la columna sí lo es (sus hermanas `peso_h`, `peso_m`,
      `uniformidad` siempre fueron anulables). Un día sin pesaje reventaba la consulta entera.
      **Nunca había pasado porque ninguna carga anterior escribió un NULL ahí**: de 934 filas, los
      únicos 3 nulos son de esta carga
- [x] R2 🔴 `POST /obtener-tabs` daba **404** «Nullable object must have a value» por el mismo nulo
      casteado a `double`
- [x] R3 🔴 `GET /diario/{lppId}` y `GET /cuadro/{lppId}` devolvían **vacío para TODAS las empresas**:
      leían de `seguimiento_diario_levante` filtrando `tipo_seguimiento='produccion'`, donde no hay
      ni una fila (924 filas, todas de levante). La fuente canónica es `seguimiento_diario_produccion`
- [x] R4 Arreglos: entidad `PesoHuevo` → `decimal?` (alineada a la columna y a sus hermanas) con
      `?? 0` en los 5 consumidores que necesitan valor —convención que el código ya usaba con
      `if (PesoHuevo > 0)`—; las 2 llamadas de `ObtenerDatosDiariosPorLPPAsync` apuntadas a la fuente
      canónica; migración `AlinearPesoHuevoProduccionANullable` (DDL no-op donde ya es nullable, con
      `Down` que rellena nulos con 0 antes de volver a NOT NULL)
- [x] R5 Verificado después: `diario/{lppId}` **168 días**, `cuadro` **24 filas**, `obtener` diario y
      semanal **200**, `obtener-tabs` **200** con 329 diarios por galpón y 47 semanales
- [x] R6 Riesgo de regresión **nulo** en R3: la fuente anterior está vacía para todas las empresas,
      así que solo pueden pasar de «vacío» a «con datos»
- [x] R7 `dotnet build` **0/0** · `dotnet test` **1.705 verdes**

## Lo que queda documentado como pendiente
- [x] P1 **Producción no tiene diario consolidado** (`GET diario/consolidado`), levante sí. Es el
      hueco de paridad más visible con un lote padre de varios sublotes
- [x] P2 `clasificacion-huevo-comercio` responde vacío — lee de la tabla canónica, así que no es el
      mismo problema; falta confirmar por qué filtra
- [x] P3 La `curva` de levante devuelve 0 puntos (el `resumen` de levante sí trae datos)
- [x] P4 `REPORTES_POR_FASE.md` publicado junto a los archivos, con el inventario endpoint por
      endpoint, las descargas de cada fase y los 3 pendientes

## Cierre de P1-P3 + el bug de dirección del traslado (deja el ciclo listo para desplegar)
- [x] C1 **P1 cerrado** — `GET /api/ReporteTecnicoProduccion/diario/consolidado?lotePosturaBaseId=`.
      La consolidación ya existía (`POST obtener` → `ConsolidarDatosDiarios`); solo faltaba la ruta
      GET de paridad con levante. **La ruta literal va declarada ANTES de `diario/{loteId}`**, si no
      el binder intenta parsear «consolidado» como `int` y devuelve 400
- [x] C2 **P2 cerrado** — `clasificacion-huevo-comercio` era el **tercer** sitio del bug de R3: leía
      de `seguimiento_diario_levante` con `tipo_seguimiento='produccion'`. Repuntado a la fuente
      canónica: de 0 a **24 filas**
- [x] C3 **P3 cerrado** — la `curva` de levante devolvía 0 puntos porque el commit de la curva
      (`145348b`) agregó `p_sem_anio IS NULL OR (...)` a los dos espejos `.sql` **pero no generó
      migración**. La fn de producción se redesplegó después por otra migración y se llevó el guard;
      la de levante quedó en la del 28-jul, con `<weeknum> = p_sem_anio` a secas ⇒ con NULL evalúa a
      NULL y devuelve **cero filas**. Roto **en prod y en todas las empresas**
- [x] C4 Al desplegar la fn corregida apareció un segundo bug: `part` con `PARTITION BY fin_sem`.
      `fin_sem` sale del encaset de **cada** lote, así que dos sublotes del mismo lote padre con
      fechas de llegada distintas nunca comparten esa fecha ⇒ cada uno solo en su partición y todos
      con `part = 1` en vez de ~0,50 y ~0,50 (justo el caso S-369). Se particiona por la semana
      **calendario**, materializada como `sem_cal` para que el filtro y la ventana usen la misma
      expresión
- [x] C5 Migración `20260806194500_CurvaLevanteAceptaSemanaNula` (data-only, Designer clonado,
      ModelSnapshot intacto, `DROP FUNCTION IF EXISTS` + `CREATE OR REPLACE`)
- [x] C6 **Gate multipaís** de la fn: versión previa desplegada en paralelo con otro nombre y
      comparada fila a fila en el modo de UNA semana, **todas las empresas × las 53 semanas**:
      39 filas, **0 diferencias** (0 solo-en-nuevo, 0 solo-en-viejo). Curva: **0 → 39 filas / 8
      lotes**. `part` suma 1 en cada semana calendario con saldo positivo
- [x] C7 🔴 **El traslado entre sublotes movía las aves de un solo lado.** La `Salida` de A y el
      `Ingreso` de B escribían filas **idénticas** en `movimiento_aves` (`Traslado`, origen=A,
      destino=B), así que la idempotencia del segundo encontraba la del primero, lo daba por
      duplicado y **lo omitía sin acreditar las aves**: B cerraba en 795 machos en vez de 991 y el
      importador igual decía «Procesado». Hasta ahora se esquivaba disfrazando el débito de `Venta`
- [x] C8 Arreglo: cada fila lleva su marca de dirección en `descripcion` (`Carga masiva:
      SALIDA/INGRESO/VENTA` — verificado que la columna estaba 100% NULL en las 27 filas existentes)
      y la clasificación sale de `MigracionMovimientosAvesCalculos.LadoDelMovimiento`, que **cae al
      heurístico histórico cuando la fila no tiene marca** ⇒ los datos viejos conservan su
      comportamiento. 8 tests nuevos
- [x] C9 Verificado en caliente sobre BD limpia: A 1162→**966** y B 795→**991**; al reimportar los
      dos, **0 procesadas / 1 omitida** cada uno y los saldos quietos (idempotencia intacta). Los
      archivos de carga vuelven al modelado correcto `Salida`+`Ingreso`
- [x] C10 Revalidación completa tras todos los cambios: **665 días comparados campo a campo, 0
      diferencias**; consolidado **480 celdas, 0 diferencias**; los **19 endpoints** de las dos fases
      responden 200 con datos. Único desvío contra el Excel: los 5 huevos del galpón 9 del 24-jun,
      descuadre del propio informe
- [x] C11 `dotnet build` **0 errores** · `dotnet test` **1.715 verdes**
- [x] C12 **Revalidado el 17-ago-2026 a pedido del usuario. Se parte en dos: `A374A` ya no reproduce,
      `LOTE 235A` sí — y ahora se sabe exactamente por qué.**

      **Medición** (`fn_resumen_semanal_ra_pesadas_levante`, las 5 empresas × 2025 y 2026, que es
      donde vive `part`): Sanmarino **145 filas, 0 saldos negativos, 0 `part` nulos**. Demo 2026:
      **1 fila negativa y 1 `part` nulo**. El resto de las empresas no devuelve filas.

      **`A374A` — cerrado.** No hay ni un saldo negativo en Sanmarino. Lo que sí daba negativo era
      otra pantalla: el endpoint `/levante/completo/{loteId}` mostraba **−212** para ese lote porque
      su fórmula propia ignoraba el traslado de ENTRADA (medido hoy, antes de corregirlo; ver V13.7).
      Ese endpoint ya delega en `SaldoAvesLevanteCalculos` y A374A queda en **7.405**, que cuadra con
      los **7.408** que dice esta misma fn.

      **`LOTE 235A` — SIGUE ABIERTO, pero no es un problema de cálculo: son los datos.** Es el lote
      **123** (LA CAROLINA; el 124 de LA PRIMAVERA está sano), semana 21, `saldo_hembras = -460` y
      `part` nulo. El kardex lo explica solo:

      | Fecha | Movimiento | Saldo |
      |---|---|---|
      | 2026-07-06 | traslado de **salida 5.100** | 5.172 → **72** |
      | 2026-07-28 | 20 mortalidades | 52 |
      | 2026-07-30 | 10 mort + 1 sel + 1 err | **40** |
      | 2026-08-03 | **500 mortalidades** | **−460** |

      5.303 − 648 mort − 14 sel − 1 err − 5.100 trasladadas = −460, exacto. O sea: **se cargaron 500
      muertes sobre un lote que tenía 40 aves.** El lote 124, que recibió esas 5.100, deja de
      registrar mortalidad el 10-jul.

      **Las dos hipótesis, SIMULADAS en transacción y revertidas** (17ago26):

      | Hipótesis | Lote 123 | Lote 124 |
      |---|---|---|
      | Hoy | **−460** | 4.870 |
      | **A** — las 500 son del lote 124 y se imputaron mal | **40** ✓ | 4.370 ✓ |
      | **B** — error de digitación (500 → 50) | **−10** ✗ | — |

      **B queda descartada por aritmética**: el lote tenía **40** aves vivas, así que cualquier cifra
      mayor a 40 lo deja negativo. **A es la única que cierra**, y encaja con el patrón: el lote 124
      recibió las 5.100 y **dejó de registrar mortalidad el 10-jul**.

      **APLICADA la hipótesis A** (OK del usuario, 17ago26) — migración data-only
      `20260818000000_ReimputarSeguimiento235ALoteCorrecto`, idempotente y **por lookup de nombres**
      (empresa `Demo` + lote `LOTE 235A` + granja `LA CAROLINA`/`LA PRIMAVERA`), porque los ids de
      lote y granja difieren entre local y prod. Mueve el registro del 03-ago al sublote correcto y
      corrige los dos maestros (0 → **40** y 4.870 → **4.370**), que no se derivan: los mantiene la
      app de forma incremental.

      **Resultado verificado:** Demo en RA Pesadas queda en **0 `part` nulos y 0 saldos negativos**, y
      la base entera en **0 negativos** por las tres fuentes (`fn_indicadores_levante_postura`,
      maestro `lote_postura_levante`, kardex crudo). Segunda pasada de la migración: **sin efecto**
      (idempotencia probada). `dotnet build` 0 errores · `dotnet test` 2.755 + 1 en verde.

      🟠 **Lo que NO se tocó, y hay que decidir aparte:** esa misma fila arrastra un **Consumo de 750
      kg del ítem 208** asentado en la granja **95 (LA CAROLINA)**. Re-apuntarlo a la 90
      (LA PRIMAVERA) crearía **stock negativo de un ítem que esa granja nunca tuvo** — su stock es del
      ítem **412**. Corregir esa pata exige saber qué alimento se consumió realmente, que es decisión
      de operación.

      ⚠️ **Es la empresa Demo, no Sanmarino** (verificado: `lotes.company_id = 4` y la granja
      `LA CAROLINA` id 95 también es Demo; existen `CAROLINA` id 45 de Ecuador y `PRIMAVERA` id 9 de
      Sanmarino, nombres parecidos en otras empresas). ⛔ Tampoco se le puso piso 0 a la fn: el
      negativo es **la señal** de que el dato está mal; taparlo lo esconde (criterio del clamp de
      engorde)

      **Barrido de respaldo (17ago26): no hay ningún otro negativo en ninguna empresa.**
      `fn_indicadores_levante_postura` 0 · `fn_reporte_semanal_levante_extras` 0 ·
      `fn_resumen_semanal_ra_pesadas_levante` 0 en Sanmarino · maestro `lote_postura_levante` 0 en las
      5 empresas · kardex crudo: **el lote 123 es el único** en toda la base cuyas bajas superan su
      base

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
- [ ] **Pendiente de decisión**: re-correr el detector contra el dump de PROD antes de implementar
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
- [ ] **Pendiente de decisión (técnica + costos)**: el corte levante/producción quedó en 24 semanas
      en S-369 y el informe de Verenice usa 25 ⇒ ~17.332 kg cambian de etapa en una conciliación

## Corte de etapa: bloqueo del doble conteo levante/producción
- [x] `CorteEtapaPosturaCalculos` (Application/Calculos): regla pura + mensajes, 10 tests xUnit
- [x] `SeguimientoLoteLevanteService.EnsureDiaSinAporteDeProduccionAsync` en el alta de levante
- [x] `ProduccionService.EnsureDiaSinAporteDeLevanteAsync` en el alta de producción
- [x] La regla mira el APORTE (consumo/bajas), no la existencia de la fila: el arrastre de huevos del
      levante crea filas de producción de solo huevos y esas NO deben chocar
- [x] Barrido de la BD: el traslape existe solo en K345 (15 días) ⇒ el guard no rompe nada existente
- [x] `dotnet build` + `dotnet test` (1.939 en verde)
- [ ] **Pendiente, requiere OK explícito**: limpiar los 15 días traslapados de K345 (el guard impide
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
- [ ] **Pendiente de decisión del usuario**: ¿el tile «Venta Engorde» (`VentaPolloEngorde`) también sale?
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
- [ ] ⚠️ **Efecto colateral a confirmar con el usuario**: «solo Sanmarino» le quita el módulo a
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
- [ ] Los lotes 2601 de Galpon-1 (id 2) y Galpon-2 (id 12) siguen en estado `Abierto`: cerrarlos POR
      PANTALLA (liquidar es una transacción de 5 pasos, no va por migración)
- [x] El lote 12 arrastra apertura negativa (−9.020 kg) — **AUDITADO en V20** (17ago26): no es una
      apertura sino el saldo FINAL de su serie, y son **9.020 kg de consumo sin ingreso** que dejó la
      reconstrucción «Cuadre saldos Excel». No contagia al ciclo siguiente. **Completar la carga exige
      las remisiones físicas** ⇒ decisión pendiente en V20.4

---

# Auditoría de cierre — «alimento previo al encaset» + fix del chip (SOLO LECTURA, sin código)

**Fecha:** 2026-08-08 · Pedido del usuario: validar si el fix del chip quedó bien y qué falta cubrir.
**Método:** 5 lentes en paralelo + verificación adversarial de cada hallazgo (14 agentes, 39 hallazgos
crudos → 8 verificados → **7 confirmados, 1 refutado**).

## Veredicto sobre el chip (92cd918 + 8d5565c)
- [x] **Sin defectos propios.** `item_type` cubre los 2 valores reales (`alimento` 166 / `Alimento` 2),
      0 discrepancias columna vs jsonb, 0 movimientos con tipo contradictorio; `PaginacionCalculos`
      coherente en los 3 services; totales del commit reproducidos exactos en SQL; borrar `loteIds`
      fue correcto (no hay dato con qué filtrar: `galpon_destino_id` NULL en 326/326)
- [x] **Refutado** el único cargo contra el chip (supuesto corte en medianoche UTC): la agrupación por
      `CreatedAt.Date` ya existía y el camino nuevo ancla a MEDIODÍA UTC, que no cruza medianoche en
      ningún huso americano
- [x] Crítica válida: su gate comparó el reporte contra una consulta con **su mismo criterio**
      (auto-consistencia, no corrección), y al destapar 257 movimientos volvió MATERIALES 3 defectos
      preexistentes que estaban tapados

## Confirmados — pendientes de decisión del usuario
- [x] 🟠 **§2.3a La excepción D4 es inalcanzable desde la UI** — **CERRADO en V15** (17ago26): se
      agregaron los dos GET que exponen la ventana del galpón y el front dejó de ser más estricto que
      el backend. Ver el bloque «V15 · La excepción D4…» al final. Texto original: backend + 184 líneas de test escritos,
      pero el front la bloquea en 3 lugares y **no existe endpoint** que exponga la ventana del galpón.
      El hint dice «Solo se admite el mes en curso» ⇒ instrucción activa a falsear la fecha.
      Afecta 39/110 encasets 2026 de Ecuador (35%) y 10/60 de Panamá. Ningún número sale mal: se
      pierde la fecha contable real, que es justo lo que contabilidad pidió
- [ ] 🟡 **§2.3b La marca rompe `fn_cuadre_alimento_engorde`** — ⚠️ **MITIGADO, NO RESUELTO**
      (revalidado 16ago26): la ronda 4 de la v16 ocultó el checkbox del alta y el historial sólo deja
      **quitar** una marca, nunca poner una nueva ⇒ la puerta de entrada está cerrada y hay 0 marcas
      vivas. El defecto de la fn sigue ahí para las marcas que ya existan. Texto original:
      (A/B controlado: mismo ingreso, solo
      cambia el booleano ⇒ descuadre −5.000). CLAUDE.md declara que mover el cuadre de 0 es regresión.
      Matiz: no hay tablero (0 archivos en el front), es endpoint + LogWarning; transitorio salvo que
      el ciclo siguiente nunca arranque. **Impacto hoy: cero** (`para_proximo_ciclo` = 0 filas en BD)
- [ ] 🟡 **§2.3c Hueco de trazabilidad**: `fechas_universo` dejó el corte `>= fecha_corte_alimento`
      FUERA del disyunto de la marca ⇒ un ingreso marcado y fechado antes de `encaset−N` no genera
      fila en ningún lote hasta el primer seguimiento. Recrea el síntoma «el sistema se comió
      alimento» que motivó el feature. **Arreglo de UNA línea**, simétrico con `apert_mov`
- [ ] 🟡 **§2.4 Cada lote padre muestra el kardex de la GRANJA entera** (granja 20 tiene 4 padres ⇒ los
      4 reportes muestran los mismos 2.907 bultos; sumarlos da 11.628 vs 2.907 reales). Preexistente,
      no arreglable en la query (la tabla no tiene columna de lote). Peor: `AcumularSaldos` resta
      consumos POR LOTE de entradas POR GRANJA ⇒ el saldo no es ni de la granja ni del lote

## Verificado OK — no re-auditar
- [x] Infraestructura del feature: 242 migraciones BD = 242 código, 0 pendientes; trigger probado
      (marca TRUE → espejo TRUE) · espejo `.sql` **idéntico byte a byte** a la migración (63.459 chars)
- [x] **Gate multipaís sin regresión**: v13→v15 cambia EXACTAMENTE 32 filas (Ecuador, lotes 2 y 86,
      todas movimiento-only = lo que v14 declara); Panamá 747=747, **0 diferencias**. El descuadre vivo
      de Panamá es preexistente (reponiendo v13 da el mismo)
- [x] **Subir `dias_alimento_previo_encaset` a 30 en Ecuador es SEGURO** (simulado: 0 filas con saldo
      distinto en 5.804 filas/172 lotes, 0 negativos nuevos, cuadre sin cambios) — las guardas v11/v12
      lo contienen. Es la prueba que nadie había hecho antes de exponer el campo por pantalla
- [x] El caso testigo del usuario («llega el 15, encaseto el 25») **entra sin marca ni configuración**
- [x] Con marca: el ciclo siguiente abre en 5.000 kg; sin marca ese mismo ingreso dejaba al nuevo en
      **−300 kg**. El mecanismo nuclear funciona
- [x] Puntos ciegos que salieron limpios: las 88 granjas con `maneja_alimento_por_galpon` NULL heredan
      `true`; los 457 ingresos EC sin galpón son insumo/medicamento/gas (0 alimento) ⇒ el checkbox
      está en el 100% de los ingresos de alimento; editar/borrar un ingreso marcado conserva/anula bien
- [x] Sin datos de prueba de agentes anteriores en la BD (0 lotes/movimientos SIM/QA/TEST)

## No verificado (declarado)
- [x] Descuadre persistido vs fn en Panamá — **RESUELTO en V18** (17ago26): **sí necesita** la
      migración `Recalcular…`. Hoy son 109 filas / 36 lotes (Ecuador 0), y **6 lotes tienen el último
      día divergente**, que es el que la liquidación congela para siempre. Texto original: (69 filas, hasta 23.355 kg): detectado, NO se determinó si
      necesita la migración `Recalcular…` que sí acompañó a v11 y v12 (este lote tocó la fn 2 veces sin ella)
- [ ] Los 31 hallazgos de severidad baja/informativa NO pasaron por verificación adversarial: son
      sospechas, no hechos

---

# v16 de engorde — FASE 1 IMPLEMENTADA: la marca `para_proximo_ciclo` ENTREGA en vez de borrar

**Plan:** [`fase_de_desarrollo/marca_proximo_ciclo_rediseno_plan.md`](fase_de_desarrollo/marca_proximo_ciclo_rediseno_plan.md)
**Fecha:** 2026-08-09 · Bloque propio — no tocar desde otras sesiones
**Continúa** el bloque «Rediseño de la marca `para_proximo_ciclo` — v16 con ENTREGA al ciclo siguiente»
(Fase 0 = plan). Base: HEAD `d6aeccb`. **Esta sesión NO commitea** (lo hace el orquestador).

## Qué quedó implementado

- [x] **F1.1** `backend/sql/fn_alimento_marcado_atribucion.sql` (NUEVO, 543 líneas) — dueño único de la
      atribución. Dos funciones: `fn_alimento_base_cedente_engorde(INT)` (el TOPE: último día visible
      del cedente + su saldo ahí) y `fn_alimento_marcado_atribucion(INT,TEXT,TEXT)` (el veredicto por
      movimiento) + el índice parcial `ix_lote_hist_para_proximo_ciclo`
- [x] **F1.2** `fn_seguimiento_diario_engorde` **v16**: las 4 exclusiones de v15 revertidas a v14 y la
      marca convertida en dos términos **ADITIVOS** — `+kg_diferido` en la apertura del DESTINO y
      `−kg_diferido` como `traslado_salida_kg` del CEDENTE en su último día visible
- [x] **F1.3** espejo C# `Application/Calculos/AtribucionAlimentoMarcadoCalculos.cs` (NUEVO) +
      `SaldoAlimentoEngordeCalculos` y `SeguimientoAvesEngordeCalculos` **revertidos a v14** (la marca
      ya no los toca) + 33 tests nuevos que CONSTRUYEN las topologías
- [x] **F1.4** cruce de umbral: `SaldoAlimentoEngordeAplicador.RecalcularVecinosSiHayAlimentoMarcadoAsync`,
      llamado desde los dos services de seguimiento (carga masiva y formulario Ecuador)
- [x] **F1.5** **el cuadre NO se tocó** — ni una línea de `fn_cuadre_alimento_engorde`
- [x] **F1.6** 2 migraciones EF idempotentes con el SQL **byte a byte** de los `.sql`:
      `20260809120000_FnAlimentoMarcadoAtribucionEngorde` y
      `20260809120100_FnSeguimientoEngordeV16EntregaCicloSiguiente` (Down = v15 VERBATIM, Designer
      clonado del último real, **ModelSnapshot intacto**)
- [x] `backend/sql/verificar_marca_proximo_ciclo.sql` (NUEVO, 566 líneas, LF) — el gate ejecutable

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
- [ ] **Fase 3** — señalamiento de la anomalía R2. **Sigue vivo y es independiente de la v16**:
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
- [ ] **Persistir la atribución como hecho** en el momento de marcar (cedente, destino, kg, fecha), en
      vez de recalcularla en lectura: es la única forma de que la liquidación de un extremo no parta el
      handoff. Es un cambio de modelo de datos, no una guarda más
- [ ] Arreglar los 4 guards de la fn para que respeten R1 (un lote que **convive** con el destino debe
      seguir viendo el movimiento). El predicado ya existe en el archivo: es el de `lotes_ajenos` (v11)
      aplicado al destino en vez de a mí
- [ ] Fase 2 (visibilidad/corrección R3) y Fase 3 (señalamiento de la anomalía R2) del plan

---

# PWA F3.1 — Captura offline (outbox) con idempotencia real

**Plan:** [fase_de_desarrollo/pwa_f3_captura_offline_plan.md](fase_de_desarrollo/pwa_f3_captura_offline_plan.md)
**Fecha:** 2026-08-12

**Contexto medido:** F1/F2/alistamiento construidos; prod todavía sirve el build del 07-ago porque el
gate del borde corta el job del front (`6f410db` está en `main`, no en `main-produccion`).
`Idempotency-Key`, `client_op_id` y outbox **no existían** en el código fuente.

## Backend — datos
- [x] Entidad `SyncOperacion` + configuración con **UNIQUE (`client_op_id`)** + `DbSet`
- [x] Migración `20260812050558_AddSyncOperaciones` con SQL crudo `IF NOT EXISTS`; aplicada en
      local :5433 y **re-corrida a mano**: los tres statements avisan «already exists, skipping»

## Backend — lógica
- [x] `Application/Calculos/SyncPushCalculos.cs` (puro: lote, identidad, contrato, empresa, reloj)
- [x] `SyncPushDtos` + `ISyncPushService` + `SyncPushService` (ancla + `Funciones/SyncPushService.Levante.cs`)
- [x] `POST /api/Sync/push` + DI
- [x] 🔴 Transacción **condicional** en `SeguimientoLoteLevanteService.Crud.cs` (3 sitios):
      `CurrentTransaction is null ? BeginTransaction() : null`. Sin ambiente se comporta idéntico
      a hoy; con ella participa, que es lo que permite commitear efecto + marca de idempotencia juntos
- [x] El push **ignora `X-Active-Company`**: la empresa sale de la operación y se valida contra
      `user_companies`. Fail-closed, sin reasignar (B6 en el camino de sync)
- [x] El servidor **estampa el autor** e ignora el del cuerpo (B5 en el camino de sync)
- [x] 🔴 **Un rechazo NO deja registro**: se re-evalúa en cada intento. Grabarlo dejaría un
      `empresa_no_autorizada` transitorio congelado para siempre
- [x] `SyncPushCalculosTests` — 22 casos

## Frontend
- [x] `offline-db.ts` **v2** con store `outbox` (paso acumulativo)
- [x] `models/outbox.model.ts`
- [x] `funciones/decidir-encolable.funcion.ts` — 🔑 no es una lista de rutas sino un mapa
      **ruta → tipo de operación**: una entrada sin tipo del lado del servidor no se puede escribir
      sin que se note (la lista blanca «a ojo» de F2 cubría 23 de 78)
- [x] `funciones/backoff.funcion.ts` (exponencial + jitter + `Retry-After` con prioridad)
- [x] `funciones/clasificar-resultado-push.funcion.ts`
- [x] `outbox.service.ts` · `sync.service.ts` (empuje al reconectar, lotes de 25, freno ante 429/503)
- [x] Rama de mutación en `offline-cache.interceptor.ts`: **202** + `__offlinePendiente`, y si el
      encolado falla se propaga el error de red (nunca decir «guardado» sin haber guardado)
- [x] Bandeja de pendientes en `/diagnostico` con «Enviar ahora» y descarte con confirmación
- [x] Seam `TRABAJO_PENDIENTE_OFFLINE` conectado al outbox (estaba sin implementar desde F0.B)
- [x] 🔴 **El outbox NO se purga** por logout, cambio de empresa ni kill switch. `purgarTodo` solo
      toca `consultas`; probado que la migración v1→v2 no pierde lo ya guardado
- [x] `sync` agregado a los EXCLUIDOS de la caché: 50 cacheables / 29 excluidos / **0 sin decidir**

## Validación
- [x] `dotnet build` 0 errores / 0 warnings · `dotnet test` **2.269 verdes** (2.237 → 2.269)
- [x] `yarn build` 0 errores (único warning: budget preexistente) · `yarn test` **275** (221 → 275)
- [x] `verificar-ngsw.js` OK (126 archivos, sin `dataGroups`, kill switch publicado)
- [x] **Smoke HTTP contra el back real** (JWT de dev + `X-Secret-Up`, usuario de una sola empresa):
      push aplica y crea la fila; **reenviar el mismo lote 2 veces más devuelve `replay:true` con el
      mismo `entidadId` y deja UNA sola fila**
- [x] **B5 probado con datos**: el payload mandaba `createdByUserId` falso y la fila quedó con el
      usuario del token
- [x] Rechazos tipados verificados uno por uno: `empresa_no_autorizada` (empresa ajena y `0`),
      `contrato_obsoleto`, `validacion` (uuid inválido), `regla_de_negocio` (lote inexistente) —
      todos con **cero filas** escritas
- [x] Datos del smoke **borrados por la API** (no por SQL): el saldo de aves volvió exacto
      (20→34 hembras = 2+3+4+5, 0→1 machos), y `sync_operaciones` quedó en 0
- [x] Sin procesos huérfanos. El backend queda **corriendo a propósito** en :5002 (lo pidió el
      usuario para validar en la app); se levantó como server gestionado, no suelto

### Validación con los dos perfiles de operario reales (12-ago)
- [x] **Ambos son de UNA sola empresa** ⇒ D6 los deja cachear, a diferencia del super admin:
      `alexlondono@sanmarino.com.co` → empresa 1 (Agroavicola Sanmarino) ·
      `ladymalave@ecuitalcol.com` → empresa 3 (ItalcolEcuador)
- [x] **Postura (Alex) funciona de punta a punta**: push contra su lote real 116 (A374A) aplica
      (id 1108), el reenvío devuelve `replay:true`, `created_by_user_id` queda con SU guid, y el
      intento de escribir en la empresa 3 se rechaza `empresa_no_autorizada`
- [x] Limpieza por la API: saldo del lote 116 volvió exacto (7.402→7.405 hembras, 737→738 machos)

### 🔴 Lo que NO se pudo probar (y por qué importa)
- [ ] **La carrera NO reprodujo el defecto.** Con 2 y con 8 POST simultáneos del mismo `clientOpId`
      siempre salió 1 fila **incluso con el índice único borrado**: el `SELECT` previo ya ve la fila
      commiteada del ganador, así que la ventana no se abrió. Lo que sí quedó probado es que el
      índice **rechaza** el duplicado (23505, en transacción revertida). O sea: el `SELECT` es el
      camino rápido y el índice el respaldo, pero **el respaldo no se ejercitó de punta a punta**
- [x] 🟢 **Hueco de UX CERRADO** (ver bloque siguiente)

## Fuera de alcance (documentado, sigue abierto)
- [ ] Editar/borrar offline · grafo de ops (`client_entity_id`) · modelo `202 + batch_id`
- [ ] Clase (b) `requiere_cuadre`: modelada en la tabla y en el cliente, **sin emisor todavía**
- [ ] B1 (revocación de sesión), B8 (rotar las 4 llaves), B10 (super admin a datos), A4

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
- [ ] **Decisión del usuario:** quitar el menú a esos 3 roles hasta que el módulo exista, o corregir
      la etiqueta. Hoy un técnico entra por «Lote Reproductora» y carga levante sin darse cuenta

## 2. Primer ingreso y menú sin internet
- [x] ✅ **El menú sobrevive sin red**: vive en la sesión persistida, no se re-pide. `ensureLoaded()`
      cae a storage y `preloadMyMenu()` hace `catchError` al menú que ya tenía. Que `roles` esté
      excluido de la caché HTTP **no lo afecta**
- [x] ✅ Perder la red **no cierra la sesión** (B2), con tope duro de 16 h (D4)
- [x] 🔴 **El primer ingreso exige red** (`POST /auth/login` + reCAPTCHA en prod) ⇒ alistamiento:
      instalar y entrar una vez con señal, **por cada usuario**
- [ ] 🔴 **El dispositivo guarda UNA sola sesión** (`auth_session`, clave única). No hay «los usuarios
      registrados» en plural: entra el último que hizo login. Dos operarios turnándose en la misma
      tablet ⇒ el segundo no puede entrar sin red. **Soportar varios exige sesiones multi-slot**
      (la partición de la caché ya está preparada; el storage de sesión no)

## 3. Acciones operativas sin red — se CONSULTAN, no se guardan
- [x] Con caché de lectura (✅ ver / ❌ guardar): gastos de inventario · gestión de inventario ·
      historial · inventario de aves · movimiento de aves · movimiento pollo engorde (+Panamá) ·
      traslados · huevos · venta de aves
- [x] Con outbox (✅ guardar): **solo** las 4 capturas diarias (levante, producción, pollo engorde,
      reproductora engorde)
- [x] No es un olvido: es la decisión **D1** («ventas y movimientos a v2»). Los movimientos tocan
      stock y saldos, son de dos lados (origen/destino) y varios crean entidades que otras
      referencian ⇒ necesitan la clase `requiere_cuadre` **con emisor** y el grafo `client_entity_id`
- [ ] **F4 (movimientos offline)** queda planteado, con sus prerrequisitos: A4, B1, B8, B10

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
| F0.B seguridad de sesión | 🟡 **parcial** — B2, B3, B7, B4, B9 hechos · **faltan B1, B5(parcial), B6(parcial), B8, B10** | `f139dfd`, `4616dfa` |
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

- [ ] **Merge `main` → `main-produccion`** para desplegar la PWA (arrastra migraciones; el contenedor
      tiene `RunMigrations=true`).
      ⚠️ **Revalidado 16ago26 — SIGUE SIN DESPLEGARSE, y la brecha creció**: `main-produccion` está en
      `cdd5561` y le faltan **25 commits** de `main`. Ya no arrastra sólo la PWA: también las
      migraciones de silos de Santa Reyes, la doble validación y los fixes de V7. Cuanto más se
      demore, más grande el salto de un solo deploy
- [x] ~~**Menú «Lote Reproductora» (id 9)**~~ — RESUELTO: migración
      `20260812080000_OcultarMenuLoteReproductoraPostura`. Etiqueta corregida a «Seguimiento
      Reproductora Postura» y **desasignado de todos los roles**; la fila del menú se conserva
- [ ] **Sesiones multi-slot por dispositivo**: es lo ÚNICO que bloquea «varios usuarios sin
      internet». Hoy `auth_session` es clave única ⇒ un usuario por tablet
- [ ] **B8**: rotar las 4 llaves de `environment.prod.ts` — **el usuario debe generarlas**, no se
      inventan secretos de prod

## Próximos trabajos, en orden sugerido

1. **Desplegar** y hacer la verificación post-deploy + instalar en un Android real (nada de F1/F2/F3
   se probó nunca en producción)
2. **B1** (jti + `sesiones_activas` + refresh) — prerrequisito de la jornada de 16 h: hoy un
   dispositivo perdido **no se puede revocar**
3. **B5/B6/B10** completos, y **A4** con su gate de paridad
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

- [ ] 🔴 **Un solo usuario por dispositivo.** `auth_session` es clave única en `localStorage`: dos
      operarios turnándose en la misma tablet ⇒ el segundo no entra sin red. Exige sesiones
      multi-slot
- [ ] 🔴 **Alistamiento con red, por usuario y por dispositivo**: instalar, entrar una vez (login y
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
- [ ] 🟠 **Aire en el bundle** — ⚠️ **cifra corregida 16ago26**: el build de hoy da **initial 1,84 MB**
      contra un techo de error de **2,05 MB** (`angular.json:62`) ⇒ quedan **~210 kB de aire**, no 50 kB.
      El riesgo sigue (un import eager grande rompe el build de prod) pero el margen es 4× el anotado.
      El warning de 1,50 MB se supera desde hace rato y es el único que sale en verde. Texto original:
      cualquier import eager nuevo rompe el build de prod

## 6. Deuda conocida que viaja con esto (ya documentada, sigue abierta)

- [ ] **B1** revocación de sesión (`jti` + `sesiones_activas` + refresh) — el más urgente: una tablet
      perdida no se puede revocar y la jornada offline dura 16 h
- [ ] **B8** rotar las 4 llaves de `environment.prod.ts` · **B10** super admin por email → a datos ·
      **A4** self-heal al patrón aplicador · **B5/B6** fuera del camino de sync
- [ ] **F4**: todo lo que no sean las 4 capturas diarias **se consulta pero no se guarda** sin red
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
- [ ] **Post-deploy manual** (no lo hace la migración, a propósito): en Roles y Permisos crear/elegir
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

# V11 · Cierre de los smokes pendientes + limpieza del tracker (17ago26)

**Plan:** [`fase_de_desarrollo/v11_cierre_smokes_y_limpieza_tracker_plan.md`](fase_de_desarrollo/v11_cierre_smokes_y_limpieza_tracker_plan.md)
Pedido: «continuá con el track y limpiá lo que completó». Bloque propio — no tocar desde otras
sesiones. **V8 sigue reservada.**

Del triage de V9.0 sobrevive poco accionable: casi todo lo abierto espera una decisión del usuario, un
admin externo o un deploy. Lo que **sí** se puede hacer sin dependencias son **dos smokes** (los únicos
que el tracker declara vivos) y una revalidación.

## V11.0 — Revalidación de pendientes contra el código ✔
- [x] V11.0.1 **V3.X cerrado sin escribir una línea**: las dos mitades ya estaban resueltas por V5 y
      V9.2, y el checkbox se había quedado sin marcar (evidencia en su bloque, arriba)

## V11.1 — Smoke A: el ciclo Implementación ↔ ItalJira ✔ (cierra V9.5.17)
- [x] V11.1.1 🔴 **El primer intento de aislamiento FALLÓ y hay que saberlo**: levanté el backend con
      `ConnectionStrings__ZooSanMarinoContext` apuntando al clon y **la variable no tuvo efecto**.
      `Program.cs:112-123` **pisa a propósito** el connection string con el de
      `appsettings.Development.json` cuando el entorno es Development («para que la conexión local no
      sea sobrescrita por env vars»). Resultado: el backend fue a la BD **compartida** y, con
      `RunMigrations: true`, le aplicó las **12 migraciones pendientes**. El `PORT` sí se respeta, así
      que nada avisó. **Decisión del usuario: dejarlas aplicadas** — es el mismo estado al que llega
      cualquier `make back` sobre `main`, y revertir exigía correr 12 `Down()` (seeds que borran filas,
      tablas de vacunación que se dropean)
- [x] V11.1.2 Filas del smoke **revertidas quirúrgicamente**: plan 2, sus 4 puntos, historias 24-25 y
      tareas 375-379. **NO se tocaron** las historias 21-23 ni las tareas 346-374: ésas las creó el
      seed de las migraciones (16:34), no el smoke (16:37) — la hora de creación las separa
- [x] V11.1.3 **Aislamiento que sí funciona** (y es el único que funciona en Development): content
      root propio en el scratchpad con su copia de `appsettings.Development.json` apuntando al clon
      (`SetBasePath(ContentRootPath)` es lo que lee esa ruta). **Verificado por `pg_stat_activity`
      antes de correr**: la única conexión iba al clon y la compartida tenía cero
- [x] V11.1.4 **Ciclo completo: 44 verificaciones, 0 fallas.** Plan + 3 puntos → enlaza los 3 → 2.ª
      pasada **no recrea nada** → punto nuevo enlaza **sólo el nuevo** → tarjeta a LISTO ⇒ punto
      **completado con fecha y autor** → sacarla ⇒ vuelve a pendiente y **limpia el sello** → punto
      **confirmado NO lo desconfirma el tablero** → historia borrada ⇒ la rehace **sin duplicar las
      tarjetas vivas** → tarjeta borrada ⇒ rehace **una sola**
- [x] V11.1.5 🔴 **Bug real que cazó el smoke**: `POST /planes/{id}/italjira` respondía **200 a un
      usuario sin `tickets.gestionar`** cuando el plan ya estaba enlazado. El permiso se apoyaba en que
      los servicios de ItalJira lanzaran **al crear**; sin nada que crear no se los llamaba, nadie
      miraba el permiso —y encima le sellaba `updated_by` al plan—. La misma llamada, el mismo usuario,
      **contestaba distinto según el estado de los datos**
- [x] V11.1.6 Arreglo **sin duplicar la regla**: `IHistoriaService.PuedeGestionarItalJira()` expone la
      que ya existía (`HistoriaService.PuedeGestionar`) y `SincronizarConItalJiraAsync` la exige antes
      de tocar nada. Probado en los dos caminos (con trabajo pendiente y sin él ⇒ **400 con motivo**),
      y que el camino bueno sigue igual (`tickets.gestionar` 200 · `tickets.admin` 200 · idempotencia)

## V11.2 — Smoke B: rango de fechas en Gastos de inventario ✔
- [x] V11.2.1 **Con rango** (2026-07-01 → 07-31): la tabla baja de **401 a 224** cabeceras y **la BD
      dice exactamente 224**, con mín/máx dentro del rango. El Excel trae **300 líneas** contra las
      **300 que cuenta la BD**, subtítulo *«Filtros — Rango: 2026-07-01 a 2026-07-31»* en las dos
      hojas, **cero fechas fuera del rango** en todo el libro y el rango en el nombre del archivo
      (`gastos-inventario_2026-07-01_a_2026-07-31_20260817.xlsx`)
- [x] V11.2.2 **Sin rango**: 401 filas, 522 líneas en el Excel (= las 522 de la BD), subtítulo
      *«Filtros — todos»* y nombre **sin sufijo** (`gastos-inventario_20260817.xlsx`) ⇒ comportamiento
      idéntico al previo. «Limpiar» vuelve a este estado exacto
- [x] V11.2.3 Extra no pedido pero que valía probar: **rango invertido** ⇒ aviso *«La fecha «Desde» no
      puede ser mayor que la fecha «Hasta»»* y **«Actualizar» y «Exportar Excel» deshabilitados** —
      no consulta ni descarga con un rango inválido
- [x] V11.2.4 El Excel se leyó **sin abrirlo** (hook de `createObjectURL` + `click`; SheetJS escribe
      el libro sin comprimir ⇒ el XML se lee directo), así se probó el contenido y no sólo que bajó

## V11.3 — Limpieza del tracker ✔
- [x] V11.3.1 **7 bloques archivados** (596 líneas: 2.359 → 1.763), cada uno con su commit verificado
      con `git log` antes de borrarlo. Quedan resumidos en la tabla «Entregado y archivado»
- [x] V11.3.2 **Todo bloque con al menos un `- [ ]` quedó entero**, sin excepción — incluida **V8**,
      que sigue reservada para otra sesión. De 67 pendientes a **51**

## V11.4 — Validación y cierre ✔
- [x] V11.4.1 `dotnet build` **0 errores** (9 advertencias, las preexistentes) · `dotnet test`
      **2.745 Application + 1 Domain en verde**. El cambio es una delegación de permiso a una regla
      que ya tenía dueño y sus tests: lo que lo cubre es el smoke, no un test nuevo — decirlo así en
      vez de inventar un unitario que no probaría nada
- [x] V11.4.2 **BD compartida verificada al terminar**: 0 filas del smoke, `implementacion_planes` 1,
      `inventario_gasto` 401 intactos. Clon **dropeado**. Puertos `5002/5499/5501/4200/4300` libres.
      Sesión de prueba borrada del `localStorage`
- [x] V11.4.3 El build de smoke salió a `--artifacts-path` en el scratchpad: el `bin/` del repo **no
      se tocó**, así que no pelea con el backend de otra sesión

### Honestidad sobre lo que NO se probó
- El smoke A corre con **JWT minteado**: prueba el gate de permiso del servicio (que es donde vive),
  **no** el login real ni el middleware de empresa activa.
- El smoke B corrió contra un **clon** de la BD: los datos son reales (401 gastos de ItalcolEcuador),
  pero es una copia — nada de lo que se miró venía de producción.
- El arreglo de permiso **no tiene test unitario nuevo**: `PuedeGestionarItalJira()` delega en una
  regla ya testeada y el punto del arreglo es *dónde* se la llama, que sólo se ve ejecutando.

---

# V12 · V7.27 — el saldo de alimento y el cuadre ignoran `validado` (17ago26)

**Plan:** [`fase_de_desarrollo/v727_saldo_alimento_ignora_validado_plan.md`](fase_de_desarrollo/v727_saldo_alimento_ignora_validado_plan.md)
Pedido: «seguí con V7.27 y el gate multipaís». Bloque propio — no tocar desde otras sesiones.
**V8 sigue reservada.**

Último pendiente abierto del bloque V7. La mitad del **cuadre** ya la cerró V7.37/V7.38; esta entrega
audita la mitad del **saldo**.

## V12.0 — Auditoría: la respuesta NO era filtrar la fn ✔
- [x] V12.0.1 Que `fn_seguimiento_diario_engorde` no mire `validado` es **correcto y deliberado**: el
      alimento se consumió el día que se cargó el seguimiento; validar confirma el movimiento de
      inventario, no el consumo. Filtrarla cambiaría el número de TODAS las empresas — incluidas las
      que tienen el flag apagado y arrastran filas `validado=false` anteriores al fix H6
- [x] V12.0.2 🔴 **Lo que sí estaba roto:** `ValidacionSeguimientoService.AplicarAlimentoAsync` armaba
      la referencia con `$"Seguimiento {modulo.ToLowerInvariant()} #…"` ⇒ escribía `Seguimiento engorde #`,
      `Seguimiento levante #` y `Seguimiento produccion #`, **tres literales que no existen en ninguna
      otra parte del sistema**. Los Cruds escriben `Seguimiento aves engorde #`, `Seguimiento lote
      levante #` y `Seguimiento producción #` (con tilde). Sólo reproductora coincidía

## V12.1 — Las dos consecuencias, medidas ✔
- [x] V12.1.1 🔴 **Desvalidar inflaba el saldo del galpón.** La fn excluye los `INV_INGRESO` que genera
      el seguimiento (`LIKE 'Seguimiento aves engorde #%'`) por ser reversiones contables; la
      devolución de la desvalidación no matcheaba ⇒ entraba como alimento nuevo mientras el
      seguimiento seguía restando su consumo. **Reproducido en transacción revertida** (lote 168,
      ItalcolPanama): 500 kg devueltos movían el saldo **+500,000** y el `ingreso_alimento_kg`
      **+500,000**; la misma fila con la referencia que la fn sí reconoce movía **0**
- [x] V12.1.2 Y arrastraba al cuadre: al desvalidar, `stock − reservado` vuelve a su valor y
      `saldo_tabla` no ⇒ **descuadre inventado** en un galpón que estaba cuadrado
- [x] V12.1.3 🔴 **El consumo validado no se podía atribuir a su lote**:
      `vw_validacion_alimento_engorde_por_lote` atribuye por `LIKE 'Seguimiento aves engorde #%'` +
      `substring(reference from '#(...)')`, así que lo reportaba como `consumo_no_posteado` — falso
      positivo del tipo que esa vista existe para cazar. Mismo problema en
      `revertir_anulacion_inv_consumo_seguimiento.sql`

## V12.2 — El arreglo: hablar el vocabulario de cada módulo ✔
- [x] V12.2.1 `ReservaSeguimientoCalculos.PrefijoReferenciaModulo` + `ReferenciaInventario(...)` —
      dueño único del literal, puro y con tests. Mismo patrón que
      `MigracionPosturaCalculos.ReferenciaConsumoLevante/Produccion`, que existe por esta misma razón
- [x] V12.2.2 `AplicarAlimentoAsync` delega en él en vez de armar la cadena a mano
- [x] V12.2.3 **No se tocó ninguna función SQL**: con la referencia correcta, los 10 lectores que ya
      existen (fn del saldo, cuadre, reporte diario, vista Power BI, 7 consultas EF, 2 vistas de
      conciliación) tratan bien el movimiento sin cambiar una línea. Descartado ensanchar el filtro en
      los 10: cinco veces más superficie para el mismo resultado, y cada copia es una oportunidad de
      que una se quede atrás
- [x] V12.2.4 **8 tests xUnit** del literal por módulo, anclados contra los prefijos que escriben los
      Cruds y contra el filtro literal de la fn

## V12.3 — Gate multipaís y verificación ✔
- [x] V12.3.1 **Gate ANTES**: línea base de **6.291 filas** congelada (`verificar_paridad_saldo_engorde.sql`)
- [x] V12.3.2 Simulación SQL con la referencia NUEVA sobre el lote 168 ⇒ saldo **0,000** e ingreso
      **0,000** de diferencia; y el consumo de validar queda atribuible (`seg_id` extraído == esperado)
- [x] V12.3.3 **Gate DESPUÉS** ⇒ **0 en todas las columnas** en las dos empresas con lotes
      (ItalcolEcuador 5.253 filas · ItalcolPanama 1.038), 0 filas que desaparecen, 0 filas nuevas,
      6.210 esperadas == 6.210 presentes
- [x] V12.3.4 `dotnet build` **0 errores** (9 advertencias, las preexistentes) · `dotnet test`
      **2.753 Application + 1 Domain en verde** (eran 2.745: +8 nuevos)
- [x] V12.3.5 **Smoke runtime contra un CLON** (`sanmarinoapp_v727`, backend en `:5501` con content
      root propio; aislamiento verificado por `pg_stat_activity`: 1 conexión al clon, **0 a la
      compartida**). ItalcolPanama, flag ON, lote 168, 80 kg:

| paso | saldo último día | ingreso total | stock | descuadre (endpoint) |
|---|---|---|---|---|
| baseline | 10.609,560 | 181.980,747 | 10.609,560 | 0 |
| pendiente | 10.529,560 | 181.980,747 | 10.609,560 | 0 |
| validado | 10.529,560 | 181.980,747 | 10.529,560 | 0 |
| **desvalidado** | **10.529,560** | **181.980,747** | 10.609,560 | **≈0 (2,7e-11)** |

      Desvalidar devuelve el sistema **exactamente** al estado «pendiente». Referencias escritas:
      `[INV_CONSUMO] Seguimiento aves engorde #11629 2026-08-14 (validado)` y
      `[INV_INGRESO] … (devolución por quitar la validación)`
- [x] V12.3.6 **Contrafactual sobre la fila real que escribió el backend**: renombrada al literal
      viejo, el saldo salta de **10.529,560 a 10.609,560 (+80,000)** y el ingreso del día de **0,000
      a 80,000**. Es el defecto, medido sobre datos escritos por el propio backend
- [x] V12.3.7 **Limpieza**: clon dropeado, tablas del gate (`_paridad_saldo_*`) dropeadas, BD
      compartida sin residuos (lote 168 con sus 42 seguimientos, 0 reservas, 0 referencias viejas),
      flags en su valor original, puertos 5002/5499/5501 libres

## Lo que NO se hizo, dicho explícitamente
- [x] V12.4.1 **PROD: no puede haber filas con los literales viejos — y no hace falta consultar la BD
      para saberlo.** La doble validación **nunca se desplegó**. Verificado contra AWS: el servicio
      `sanmarino-back-task-service-75khncfa` corre la TaskDef **158**, único deployment (PRIMARY /
      COMPLETED, 14-ago-2026 22:36), imagen `…backend:cdd5561`. Ese commit (`cdd5561`, merge del PR
      #71, 14-ago 22:31) **no contiene**: las 4 migraciones de doble validación, la carpeta
      `Services/ValidacionSeguimiento/` ni la entidad `SeguimientoReservaAlimento`. Sin
      `AplicarAlimentoAsync` en el binario **no hay camino de código que escriba esos literales**, así
      que el resultado no depende del esquema de la BD: aunque las migraciones se hubieran aplicado
      por otra vía, sin el código no se genera ni una fila. En local también hay **cero**
- [x] V12.4.2 **Corolario: el defecto nunca llega a producción.** El arreglo y la feature están los
      dos en `main` sin desplegar, así que el primer deploy que lleve la doble validación ya lleva el
      literal correcto. **No hace falta migración data-only** — la que se había previsto
      (`replace(referencia, 'Seguimiento engorde #', 'Seguimiento aves engorde #')`) queda sin objeto
- [x] V12.4.3 **La asimetría de levante/producción/reproductora NO es alcanzable — medido, ya no es
      una corazonada.** Una devolución de esos módulos sólo inflaría el saldo de engorde si compartiera
      `(granja, núcleo, galpón)` con un lote de engorde. En la base: **15 galpones de postura y 76 de
      engorde, cero solapados** en las 5 empresas. Queda documentado como condición a vigilar, no como
      deuda: si algún día un galpón se reusa entre fases, revisar este filtro
- [ ] V12.4.4 **No se filtró la fn por `validado`** — ver V12.0.1. Diferir también el saldo es un
      cambio de modelo que pide su propio plan y su propio gate

## Dos observaciones que NO son de esta entrega
- [ ] V12.5.1 **Para el bloque «v16 de engorde — marca `para_proximo_ciclo`»**: ese bloque declara
      implementadas las migraciones `20260809120000_FnAlimentoMarcadoAtribucionEngorde` y
      `20260809120100_FnSeguimientoEngordeV16EntregaCicloSiguiente`, pero **no están en el repo**:
      `backend/sql/fn_seguimiento_diario_engorde.sql` sigue en **v15** y la BD local también. Ese
      trabajo nunca se commiteó. Esta entrega **no toca la fn**, así que no lo bloquea ni lo pisa
- [ ] V12.5.2 **Para V8**: el lote 168 («patrón C», el descuadre de 250 kg) ya **no reproduce** —
      medido en la BD compartida **antes** de tocar nada: `saldo_tabla 10.609,560 · mov_post 0 ·
      stock 10.609,560 · descuadre 0,000`. El cuadre de ItalcolPanama está hoy en **5 descuadrados /
      54.795,359 kg**, no en los 6 / 55.045,359 del baseline de V8. Revalidar esa tabla antes de
      trabajarla

---

# Cola de baja prioridad — mirar sólo cuando se toque producción

Va al final a propósito: **nada de acá bloquea desarrollo**. La verificación contra prod resultó
innecesaria (V12.4.1: sin el código desplegado no hay fila que buscar), así que estos puntos se miran
recién cuando haya un deploy de por medio.

- [ ] P.1 ⚠️ **¿El esquema de prod quedó por delante del binario?** El usuario dijo «la base de datos
      está actualizada en AWS» mientras el servicio corría la imagen del 14-ago (TaskDef 158). Si las
      migraciones se aplicaron por fuera del deploy, es el modo de falla que documenta CLAUDE.md
      («migración aplicada = binario viejo inválido» → exit 139 / SIGSEGV al arrancar). **Se resuelve
      solo con el próximo deploy**, que lleva el código que corresponde a esas migraciones.
      Comprobación de un renglón cuando haya acceso:
      `SELECT migration_id FROM "__EFMigrationsHistory" WHERE migration_id LIKE '202608%' ORDER BY 1 DESC LIMIT 10;`
- [ ] P.2 **Verificación post-deploy obligatoria** cuando salga la doble validación (CLAUDE.md §🚀):
      `describe-services` → TaskDef y `rolloutState`, `describe-task-definition` → imagen, y comparar
      contra la que se pretendía desplegar. ECS hace rollback silencioso y el CLI igual dice
      «completado»
- [ ] P.3 **Desde esta máquina no se puede consultar prod** y no vale la pena forzarlo: RDS en VPC
      privada (`10.4.6.6`, psql timeout), **ECS Exec deshabilitado** en el servicio, y el usuario IAM
      sin `rds:DescribeDBInstances` ni `ssm:DescribeInstanceInformation`. Habilitar ECS Exec exige
      redeploy de producción ⇒ sólo con pedido explícito. Para consultas puntuales, DB Studio

---

# V13 · Saldo de aves de levante — cuatro consumidores, dos fórmulas (17ago26)

**Plan:** [`fase_de_desarrollo/saldo_levante_una_sola_formula_plan.md`](fase_de_desarrollo/saldo_levante_una_sola_formula_plan.md)
Pedido: «seguí en todo lo que es desarrollo». Bloque propio — no tocar desde otras sesiones.
**V8 sigue reservada.**

Sale del re-triage de los pendientes: retoma el hallazgo abierto del bloque ItalJira («Tres fórmulas
distintas para el saldo de levante»), que resultó ser **cuatro consumidores** y estar **divergiendo
hoy**, no en el futuro.

## V13.0 — Re-triage de lo que quedaba abierto ✔
- [x] V13.0.1 Repasados los 16 bloques abiertos: casi todo espera **decisión del usuario**, un **admin
      externo** o un **deploy**. Lo único accionable en código sin dependencias es este hallazgo
- [x] V13.0.2 Verificado que sigue vivo (no como V11.0.1, que ya estaba resuelto): la línea 396 de
      `fn_indicadores_levante_postura` es `r_aves_fin := v_aves_acum − mort − sel − err − tras_sal +
      tras_ing`, sin venta, y la fn ni siquiera declara una variable de venta

## V13.1 — El estado real: son CUATRO, no tres
- [x] V13.1.1 **Descuentan la venta:** `fn_reporte_semanal_levante_extras` y
      `fn_resumen_semanal_ra_pesadas_levante` (comentario propio: *«el saldo tiene que descontarla o el
      reporte sobrestima el lote»*)
- [x] V13.1.2 **NO la descuentan:** `fn_indicadores_levante_postura` y **`ReporteTecnicoService`** —
      este último no estaba en el hallazgo original: sus 4 call sites construyen
      `MovimientoDia(mort, sel, err, trasSal, trasIng)` y dejan `Venta` en su default `0`
- [x] V13.1.3 `SaldoAvesLevanteCalculos` (la especificación ejecutable) **sí** contempla la venta en
      `BajasNetas`, pero **su único consumidor nunca se la pasa**: la spec está bien y nadie la usa
      completa

## V13.2 — La divergencia es visible HOY (medida)
- [x] V13.2.1 Mismo lote, misma semana, dos conteos (Sanmarino): lote **143** sem 23 → 10.626 vs
      10.476 (**150**), sem 24 → 10.619 vs 10.329 (**290**); lote **142** sem 24 → 10.646 vs 10.450
      (**196**). La diferencia es **exactamente** la venta acumulada
- [x] V13.2.2 Sólo 2 lotes tienen ventas hoy (143: 290 aves en 2 filas · 142: 196 en 1), y en los dos
      `venta_aves_cantidad` coincide con `venta_aves_hembras + venta_aves_machos`. Por eso «no se
      notaba»: no porque no pase, sino porque casi nadie registró ventas de levante todavía

## V13.3 — Arreglo
- [x] V13.3.1 `fn_indicadores_levante_postura` descuenta la venta en el mixto y por sexo, con la misma
      convención que el resto de la fn (el total mixto se arma como `h + m`)
- [x] V13.3.2 Migración EF con `CREATE OR REPLACE` (la firma no cambia); `Down()` = cuerpo actual
      VERBATIM
- [x] V13.3.3 Tests: la venta es una baja como cualquier otra (venta y traslado de salida por la misma
      cantidad ⇒ mismo saldo)

## V13.4 — Verificación
- [x] V13.4.1 **Paridad** `fn_indicadores_levante_postura` vs `fn_reporte_semanal_levante_extras` en
      TODOS los lotes, antes y después: antes 3 filas con diferencia (142 y 143), después **0**
- [x] V13.4.2 **Lotes sin ventas: 0 filas cambiadas.** El arreglo no puede mover un número donde no
      hubo venta
- [x] V13.4.3 El resto de las columnas de la fn (peso, uniformidad, consumo, % mortalidad) intactas
- [x] V13.4.4 `dotnet build` 0 errores · `dotnet test` en verde · BD sin residuos, puertos libres

## V13.7 — `ReporteTecnicoService` y el barrido completo (17ago26)

Pedido: «si arreglás ReporteTecnicoService también corregí todo lo que encuentres». El barrido
encontró **más de lo anunciado**: no era una fórmula que faltaba descontar la venta, eran **tres
implementaciones distintas** del saldo dentro del mismo archivo y **una cuarta muerta** en el front.

### El censo real
- [x] V13.7.1 **Descartado primero lo peligroso**: verificado que la venta **no venía plegada** en
      otra variable (`mortH`, `selH`, `errH`, `trasSal*`, `trasIng*` salen tal cual del seguimiento),
      así que sumarla no la contaba dos veces
- [x] V13.7.2 La proyección `SegLevanteParaReporte` **no traía** los splits de venta: hubo que
      agregarlos a la clase y a las **2** consultas EF que la llenan
- [x] V13.7.3 **`GenerarDiariosConsolidados`** (`/levante/tabs/{loteId}`, diario) — usaba la spec pero
      sin venta ⇒ arreglado
- [x] V13.7.4 **`GenerarSemanalesConsolidados`** (`/levante/tabs/{loteId}`, semanal) — ídem, con
      acumuladores `acVentaH/M` nuevos ⇒ arreglado
- [x] V13.7.5 🔴 **`GenerarReporteLevanteCompletoAsync`** (`/levante/completo/{loteId}`, que el front
      **sí llama**) tenía su **propia fórmula a mano**: `ini − mort − sel − err`. Sin traslados, sin
      venta y **sin piso en 0**. Ni siquiera usaba `SaldoAvesLevanteCalculos`. Ahora delega en la spec

### Lo que estaba mostrando ese endpoint (medido, 11 lotes)

| LPL | Lote | Hembras ANTES | Hembras DESPUÉS | Qué lo causaba |
|---|---|---|---|---|
| 8 | A374A | **−212** | 7.405 | recibió 8.627 aves y el ingreso no se contaba |
| 16 | LOTE 235A | **−230** | 4.870 | recibió 5.100 |
| 20 | LOTE 237A | **−615** | 19.385 | recibió 20.000 |
| 6 | A374A | 15.161 | 7.544 | entregó 8.627 y la salida no se restaba |
| 19 | LOTE 237 | 26.034 | 34 | entregó 26.000 |
| 34/35 | S369A/B | 9.484 / 1.085 | 9.484 / 795 | sólo venta (196 y 290) |

- [x] V13.7.6 **Mostraba aves NEGATIVAS** en los lotes que reciben un traslado y **el doble** en los
      que lo entregan. No era un detalle de la venta: el endpoint estaba roto para cualquier lote
      trasladado. 4 lotes de Sanmarino y 7 de Demo
- [x] V13.7.7 El valor corregido **cuadra con la otra fuente**: A374A queda en 7.405 y
      `fn_resumen_semanal_ra_pesadas_levante` dice 7.408 para esa semana

### Código muerto retirado (era una CUARTA fórmula)
- [x] V13.7.8 `lote-levante/pages/tabla-indicadores-diarios` + `services/indicadores-diarios-compute.service.ts`
      (498 líneas) calculaban el saldo **en el front** como `avesH − mortH − selH`: sin error de
      sexaje, sin traslados, sin venta. Quedaron huérfanos cuando `fn_indicadores_levante_postura` se
      llevó el cálculo al backend — **nadie los importaba ni los montaba**. Retirados
- [x] V13.7.9 🟠 **El build del front cazó un enganche que la lectura no vio**: el `.scss` de ese
      componente muerto lo `@use`-aba el componente de indicadores de **engorde**. Se movió a
      `engorde-comun/.../tabla-indicadores-diarios-base.scss`, que es donde vive su único consumidor

### Verificación
- [x] V13.7.10 `dotnet build` **0 errores** (9 advertencias preexistentes) · `dotnet test` **2.755 + 1
      en verde** · `yarn build` OK (único warning, el de bundle budget preexistente)
- [x] V13.7.11 Impacto medido lote por lote en SQL reproduciendo las dos fórmulas: **11 lotes** se
      mueven, y son exactamente los que tienen traslado o venta. Ningún lote sin movimientos cambia

### Señalamiento a otro bloque (NO toco su checkbox)
- [x] V13.7.12 **C12 revalidado a pedido del usuario y actualizado en su propio bloque.** Se parte en
      dos: **`A374A` cerrado** (0 saldos negativos en Sanmarino; el −212 venía del endpoint
      `/levante/completo`, que este cambio corrigió a 7.405 ⇒ cuadra con los 7.408 de la fn) y
      **`LOTE 235A` sigue abierto pero deja de ser un misterio**: el lote 123 trasladó 5.100 de sus
      5.172 aves el 06-jul y el **03-ago le cargaron 500 mortalidades cuando tenía 40**. No es cálculo,
      son datos, y elegir entre «son del lote 124» o «es un error de digitación» es una decisión de
      operación. Ver el detalle en el bloque «Consolidado de sublotes»

### Lo que queda igual, a propósito
- [x] V13.7.13 `RetiroAcumulado` **sigue sin contar la venta** — es correcto y está documentado en la
      spec: el «% retiro acumulado» es mortalidad + selección + error de sexaje; la venta se reporta
      en su propia columna y meterla ahí inflaría el indicador

## Fuera de alcance, con su evidencia
- [x] V13.5.1 ~~`ReporteTecnicoService` NO se toca en esta entrega~~ — **entró**, por pedido
      explícito del usuario, y con él salieron dos fórmulas más (ver V13.7). El texto original decía: Alimenta el Reporte Técnico
      Semanal, que operación y costos ya leen; moverle el saldo pide verificación contra el informe
      impreso, no un cambio al pasar. Queda el hallazgo documentado con sus 4 call sites
      (`ReporteTecnicoService.cs:2828`, `:2830`, `:3007`, `:3009`)

## Resultado de la verificación (17ago26)
- [x] V13.6.1 **Paridad ANTES**: 155 filas congeladas · **3 desalineadas** (peor: 290 aves), Demo 0
- [x] V13.6.2 **Paridad DESPUÉS**: `dif_vs_extras` = **0** en las dos empresas con lotes; el detalle de
      filas desalineadas queda **vacío**. Las dos funciones dan el mismo número
- [x] V13.6.3 **Cambiaron exactamente 3 filas**, las de los 2 lotes con venta. Demo: **0**. Un lote sin
      ventas no movió un solo número
- [x] V13.6.4 `peso_cierre` y `consumo_total_semana` **intactos** en las 3. `mortalidad_sem` se movió en
      **1** (lote 143 sem 24: 0,065876 → 0,066819) y es **correcto**: su denominador son las aves al
      inicio de la semana, o sea el cierre de la semana 23, que bajó por la venta
- [x] V13.6.5 `dotnet build` **0 errores** · `dotnet test` **2.755 + 1 en verde** (+2) · migración
      aplicada en local sin error · tablas de paridad dropeadas · puertos libres
- [x] V13.6.6 El test anclado con el **kardex real** del lote 143 (11.812 encasetadas − 432 mort − 379
      sel − 382 error de sexaje − 290 venta) reproduce **10.329**, que es lo que muestran las dos
      pantallas hoy; y **10.619** sin la venta, que es lo que mostraba Indicadores antes

---

# V14 · Bloquear el consumo cuando no hay stock del alimento (17ago26)

**Plan:** [`fase_de_desarrollo/bloquear_consumo_sin_stock_plan.md`](fase_de_desarrollo/bloquear_consumo_sin_stock_plan.md)
Pedido: «en los seguimientos diarios se tiene que validar que no se pueda realizar consumo si no se
tiene stock del alimento seleccionado». Bloque propio — no tocar desde otras sesiones.
**V8 sigue reservada.**

## V14.0 — Diagnóstico ✔
- [x] V14.0.1 **La misma regla tiene hoy dos tratamientos.** Colombia (`ModeloBNivelGranja`) valida
      con `ValidarStockConsumoAsync` **antes de persistir** y hace rollback ⇒ bloquea. Ecuador y
      Panamá (`ModeloB`, núcleo+galpón) guardan el seguimiento **primero** y aplican el consumo
      después dentro de `try { … } catch { LogError }`
- [x] V14.0.2 🔴 **No es que no valide: es que nadie escucha.**
      `InventarioGestionService.RegistrarConsumoAsync` sí lanza `MensajeStockInsuficiente` (con
      `UPDATE … WHERE quantity >= …` atómico), pero el `catch` se lo come. El registro queda guardado
      con sus kg y el inventario intacto — el código lo llama «flujo tolerante»
- [x] V14.0.3 **Censo: 10 sitios en 4 servicios**, alta y edición —
      `SeguimientoLoteLevanteService.Crud` (:129/:297), `SeguimientoAvesEngordeService.Crud`
      (:247/:485), `SeguimientoAvesEngordeEcuadorService.Crud` (:180/:419) y
      `SeguimientoDiarioLoteReproductoraService` (:306/:455). Los `catch` de reproductora ni siquiera
      loguean: escriben a `Console.WriteLine`
- [x] V14.0.4 **No hay stock negativo en la base hoy** (570 filas, 0 negativas, 242 en cero), así que
      esto se ataja antes de que aparezca — no es una limpieza

## V14.1 — Implementación
- [x] V14.1.1 `IInventarioGestionService.ValidarStockConsumoAsync(farmId, nucleo, galpón, byItem)` —
      el tercer validador, el que faltaba para modelo B con ubicación. Mensaje que **nombra el ítem y
      el faltante**, no un genérico
- [x] V14.1.2 La validación corre **ANTES de persistir** en los 10 sitios (hoy el bloque va después
      del `CreateAsync`): es lo que hace que el rechazo deje la base intacta
- [x] V14.1.3 El `catch` deja de tragar el stock insuficiente; se conserva el manejo de otros fallos
- [x] V14.1.4 Tests del cálculo puro del mensaje + de la decisión (T1-T5 del plan)

## V14.2 — Verificación
- [x] V14.2.1 `dotnet build` 0 errores · `dotnet test` en verde
- [x] V14.2.2 Smoke: alta con alimento sin stock ⇒ **400** y **ni seguimiento ni inventario** cambian;
      con stock ⇒ 201 y stock descontado; edición que sube el consumo por encima del stock ⇒ 400
- [x] V14.2.3 Colombia **sin cambios** (su camino ya bloqueaba)

## Fuera de alcance, dicho
- [ ] V14.3.1 `MigracionService.AlimentoEngorde/AlimentoPostura` (carga histórica, entra por
      `ModoCargaHistorica`) e `InventarioGastoService` (ya llama sin tragar el error)

## V14.4 — Resultado del smoke (17ago26)

Contra un **clon** (`sanmarinoapp_stock`, backend en `:5501` con content root propio; aislamiento
verificado por `pg_stat_activity`: 1 conexión al clon, **0 a la compartida**). ItalcolEcuador —flag de
doble validación **apagado**, que es el camino donde vivía el hueco—, lote 150, galpón G0048, con
2.080 kg del ítem 2 y 0 kg del ítem 4.

| Caso | Respuesta | ¿Se guardó? |
|---|---|---|
| Alimento con stock en **0** | **400** · *«AV. POLLITO PREINICIADOR»: se piden 50 kg y hay 0 kg* | **no** (24 → 24 registros) |
| Ítem inexistente | **400** · *el ítem no existe* (validación previa, ya estaba) | **no** |
| Más kilos de los que hay (5.000 vs 2.080) | **400** con las dos cifras | **no** |
| Con stock suficiente (80 kg) | **201** | sí · stock 2.080 → **2.000** |

- [x] V14.4.1 El rechazo deja la base **intacta**: ni el seguimiento ni el stock se mueven. Es lo que
      no se podía lograr desde el `catch`, que corría cuando el registro ya estaba guardado
- [x] V14.4.2 El mensaje nombra el **producto real** («AV. POLLITO PREINICIADOR»), no el id ni un
      genérico, y dice qué hacer: registrar el ingreso antes del consumo
- [x] V14.4.3 `dotnet build` **0 errores** (9 advertencias preexistentes) · `dotnet test` **2.763 + 1
      en verde** (+8) · clon dropeado · puertos libres

## V14.6 — Panamá con la doble validación APAGADA (17ago26, pedido del usuario)

El flag se apagó **en un clon** (`sanmarinoapp_pa`), no en la base compartida: es estado que otras
sesiones usan, y V7 dejó como norma restaurarlo. La base compartida quedó verificada con
`ItalcolPanama = true`, igual que antes.

Lote 168, granja 106 / núcleo 791385 / galpón G0490. Stock: ítem 213 = 10.609,560 kg · ítem 214 = 0.

| Caso | Respuesta | ¿Se guardó? |
|---|---|---|
| Ítem con stock en **0** (214, 50 kg) | **400** · *«AV. SUPER POLLITO INICIACION»: se piden 50 kg y hay 0 kg* | **no** (42 → 42) |
| **99.999 kg** sobre 10.609 (213) | **400** con las dos cifras | **no** |
| 80 kg con stock (213) | **201** | sí · 42 → 43 · stock 10.609,560 → **10.529,560** |

- [x] V14.6.1 **Con el flag apagado Panamá entra por el camino que tenía el hueco** y ahora queda
      cerrado: el rechazo no deja ni el seguimiento ni el movimiento de inventario
- [x] V14.6.2 **Descuenta, no separa**: `reservas ACTIVA = 0` después del alta buena y el registro nace
      `validado = true`, que es exactamente lo que V7.17 fijó para el flag apagado
      (`Validado = !separa`). O sea que apagar el flag devuelve el comportamiento clásico completo
- [x] V14.6.3 **Clon dropeado · flag compartido intacto en `true` · puertos libres**

## Decisión tomada
- [x] V14.7.1 **El flag de Panamá NO se apaga.** Decisión del usuario (17ago26): *«era solo para
      probar, dejalo así»*. Panamá sigue con la doble validación encendida. No hubo nada que revertir:
      la prueba corrió sobre el clon `sanmarinoapp_pa`, que se dropeó, y la base compartida quedó
      verificada con `ItalcolPanama = true`.

      Queda escrito para la próxima vez que se plantee: apagarlo de verdad es una **migración
      data-only** y significa que Panamá deja de usar la doble validación que entregó V7 —los
      seguimientos vuelven a descontar al guardar, se acaban las reservas y el botón Validar
      desaparece de sus pantallas (`requiereValidacion` es fail-closed)—. Y antes hay que confirmar
      que no queden **reservas ACTIVAS**: esos registros quedarían separados sin nadie que los aplique

## Lo que quedó fuera, dicho
- [x] V14.5.1 **Panamá no cambia hoy**: tiene la doble validación **encendida**, así que el alta
      separa en vez de descontar y su comprobación de stock la hace `RegistrarConsumoAsync` dentro de
      la transacción de `ValidarAsync`. El guard nuevo lo cubre igual el día que se apague el flag
- [x] V14.5.2 **Colombia sin cambios**: su camino (`ModeloBNivelGranja`) ya validaba antes de
      persistir. Se verificó que no se tocó ninguna de sus dos llamadas
- [x] V14.5.3 El `catch` **se conserva** para otros fallos: no se convierte un problema transitorio de
      inventario en un 500 al guardar el día. Lo que ya no puede llegar ahí es el stock insuficiente,
      porque lo cortó la validación previa

---

# V15 · La excepción D4 (alimento previo al encaset) es inalcanzable desde la UI (17ago26)

**Plan:** [`fase_de_desarrollo/ventana_fecha_ingreso_alimento_previo_ui_plan.md`](fase_de_desarrollo/ventana_fecha_ingreso_alimento_previo_ui_plan.md)
Pedido: «seguí con el siguiente pendiente del tracker». Bloque propio — no tocar desde otras sesiones.
**V8 sigue reservada.**

## V15.0 — Re-triage de lo que quedaba abierto ✔
- [x] V15.0.1 Repasados los bloques abiertos tras cerrar V14: la mayoría espera **decisión del
      usuario** (lotes 132 / 3,4,6,8; cierre del grupo A; K345; tile Venta Engorde; Santa Reyes y
      Migraciones Masivas), un **admin externo** (correo Office 365) o un **deploy** (P.1-P.3, PWA)
- [x] V15.0.2 🔑 **Descartado el bloque de la marca `para_proximo_ciclo` (§2.3b, §2.3c, los 4 guards,
      «persistir la atribución»)**: no es sólo que hoy valga 0 (0 marcas en BD, puerta de entrada
      cerrada por la mitigación de la ronda 4). Es que **el rediseño ya declarado devuelve
      `apert_mov`, `hist_full`, `hist_alimento`, `docs_por_fecha` y `fechas_universo` a la forma de
      v14 exacta** ⇒ arreglar §2.3c hoy es trabajo que ese rediseño tira. Se deja como estaba
- [x] V15.0.3 Queda **§2.3a**, el 🟠 de mayor severidad del bloque de auditoría, y es el único
      accionable sin dependencias: **no es una decisión de producto pendiente, es una feature a
      medio terminar**. El backend YA acepta la fecha (D4 vivo en las 2 puertas de ingreso, con 184
      líneas de test); el front es **más estricto que el backend** y encima empuja a falsear la fecha

## V15.1 — Diagnóstico
- [x] V15.1.1 Verificado sitio por sitio: `EsFechaPermitidaConEncasetProximo` +
      `MensajeFueraDeVentanaConEncaset` escritos, `ResolverVentanaAlimentoPrevioEncasetAsync` y
      `…DeIngresoAsync` implementados, y `POST /ingreso` (:163) + `PUT /ingresos/{id}/fecha` (:401)
      los usan. **Lo único que falta es el GET que exponga la ventana** y que el front la respete
- [x] V15.1.2 Censo del front: **4** datepickers atan la ventana, pero sólo **2** son puertas D4
      (modal de fecha del ingreso y «Nueva fecha» del historial). **Traslado y stock conservan la
      regla dura**, igual que el backend
- [x] V15.1.3 🔴 **Por qué NO se replica la regla completa en TS**: el encaset que manda es el más
      cercano con `fecha_encaset >= fecha del movimiento` ⇒ **depende de la fecha que el usuario
      elija**. Un espejo en el front resolvería otro encaset y rechazaría fechas que el backend
      acepta — el mismo defecto, del otro lado

## V15.2 — Implementación
- [x] V15.2.1 `ExtremosVentanaIngreso(hoy, proximoEncaset, dias)` — cálculo **puro** nuevo: corre el
      `min` hacia atrás sólo si el intervalo del encaset intersecta `[hoy−30, hoy]`; el `max` es
      siempre hoy
- [x] V15.2.2 Dos GET nuevos en `InventarioGestionController`, delegando en los resolvers existentes
- [x] V15.2.3 Front: extremos dinámicos en los 2 datepickers D4 + hint que nombra el encaset real;
      la guarda deja de bloquear dentro de los 30 días y deja hablar al controller
- [x] V15.2.4 Tests T1-T7 del cálculo puro

## V15.3 — Verificación
- [x] V15.3.1 `dotnet build` 0 errores · `dotnet test` en verde
- [x] V15.3.2 `yarn build` sin errores nuevos
- [x] V15.3.3 Smoke: alta de ingreso con fecha del mes anterior **dentro** de la ventana ⇒ 200; la
      misma **fuera** ⇒ 400 nombrando el encaset; traslado y stock siguen cortando en el día 1

## Fuera de alcance, dicho
- [x] V15.4.1 No se toca ninguna función SQL ⇒ **no aplica el gate multipaís**. No se toca la marca
      `para_proximo_ciclo` ni `dias_alimento_previo_encaset` de ninguna empresa

## V15.5 — Lo que el smoke corrigió del diseño (17ago26)

- [x] V15.5.1 🔴 **El primer endpoint resolvía la ventana con HOY y no encontraba nada.** El resolver
      devuelve el encaset más cercano **`>= fecha`**, así que preguntando con hoy el encaset de la
      semana pasada —justo el que justifica la fecha que hay que ofrecer— queda invisible y la ventana
      volvía a salir recortada. Se corrigió: sin `fecha` explícita el GET resuelve desde el **piso de
      30 días**, que da el encaset cuya ventana llega más atrás (cualquier posterior abre menos).
      Lo cazó el smoke, no la lectura del código

## V15.6 — Resultado del smoke (17ago26)

Contra un **clon** (`sanmarinoapp_d4`, backend en `:5501` con content root propio; aislamiento
verificado por `pg_stat_activity`: 1 conexión al clon, **0 a la compartida**). ItalcolEcuador
(`dias_alimento_previo_encaset` = 10), hoy = 17-ago-2026.

| Caso | Ubicación | Respuesta |
|---|---|---|
| `GET ventana-fecha-ingreso` con encaset del **10-ago** | granja 37 / G0031 | `min 2026-07-31` · ayuda nombra el encaset y el rango |
| `GET` **sin galpón** | granja 37 | `min 2026-08-01` · `proximoEncaset: null` · ayuda genérica |
| `GET` con encaset del **18-ago** (ventana entera dentro del mes) | granja 45 / G0057 | `min 2026-08-01` — **no promete una excepción que no agrega días** |
| `POST /ingreso` fecha **31-jul** (mes anterior, DENTRO) | granja 37 / G0031 | **200** ✅ *lo que el front no dejaba tipear* |
| `POST /ingreso` fecha **25-jul** (FUERA) | granja 37 / G0031 | **400** nombrando encaset y rango |
| `POST /ingreso` fecha **31-jul** en galpón cuya ventana no llega a julio | granja 45 / G0057 | **400** — la excepción es **por galpón**, no por empresa |
| `PUT /ingresos/{id}/fecha` a **01-ago** / a **20-jul** | granja 37 / G0031 | **200** / **400** |
| `POST /traslado` y `PUT /stock/{id}` con **31-jul** | granja 37 / G0031 | **400** con el mensaje **sin** mención a la excepción |

- [x] V15.6.1 **El `min` que ofrece la pantalla es exactamente la primera fecha que el backend
      acepta** (31-jul en G0031): no ofrece de menos —que era el defecto— ni promete lo que el
      controller va a rechazar
- [x] V15.6.2 **Las tres puertas de regla dura no se movieron**: traslado, fecha de traslado y stock
      siguen cortando en el día 1 del mes, con su mensaje original
- [x] V15.6.3 `dotnet build` **0 errores** (9 advertencias preexistentes) · `dotnet test` **2.774 + 1
      en verde** (+11, los T1-T7) · `yarn build` OK (único warning, el de bundle budget preexistente)
- [x] V15.6.4 **Clon dropeado · BD compartida sin una sola fila del smoke · puertos 5002/5501/4200 libres**

## Observación honesta, fuera de alcance
- [ ] V15.7.1 El alta con fecha del mes anterior devuelve **200 + `avisoFechaFueraDeCiclo`**: la
      ventana D4 (`encaset − dias`) puede arrancar **un día antes** que el corte efectivo de la fn,
      que además respeta el fin del ciclo anterior (`corte_apertura` de v12). En el smoke el 31-jul se
      admite pero el aviso dice que el ciclo 2604 cuenta el alimento desde el 01-ago. **No es un
      defecto de esta entrega** —el aviso es preexistente y hace justo lo que debe: avisar sin
      bloquear—, pero queda escrito por si se decide alinear las dos fechas

---

# V16 · Fase 3 de R2 — señalar el alimento que queda al liquidar (17ago26)

**Plan:** [`fase_de_desarrollo/senalamiento_anomalia_r2_fase3_plan.md`](fase_de_desarrollo/senalamiento_anomalia_r2_fase3_plan.md)
Pedido: «seguí con la Fase 3 de R2» — el pendiente que dejó abierto el bloque de la v16 de engorde
(«Fase 3 — señalamiento de la anomalía R2. Sigue vivo y es independiente de la v16»).
Bloque propio — no tocar desde otras sesiones. **V8 sigue reservada.**

## V16.0 — Diagnóstico revalidado contra la BD ✔
- [x] V16.0.1 **La anomalía creció**: 90 liquidaciones congeladas vigentes (todas ItalcolEcuador),
      **28 con `saldo_alimento_kg > 0` = 137.521 kg** (el plan de julio decía 24 de 84 y 111.821 kg).
      Otras **20** son copias de backfill con el saldo en NULL: no se les puede inventar un número
- [x] V16.0.2 **El falso positivo del aviso de liquidación también creció**: 15 lotes verían kilos de
      OTROS galpones (EC abiertos 4 · EC cerrados 10 · PA abierto 1), con 124.810 + 318.605 + 77.737 kg
      ajenos según el caso
- [x] V16.0.3 🔑 **Ninguna granja guarda hoy el alimento de engorde a nivel núcleo**: Ecuador y Panamá
      lo tienen por galpón (136 y 85 filas), Sanmarino y Demo a nivel granja (núcleo y galpón vacíos).
      Por eso el fallback a núcleo solo puede traer kilos ajenos — pero el stock **sin galpón** sí es
      del lote y no se puede borrar sin romper a las empresas de nivel granja
- [x] V16.0.4 `GET /api/CuadreAlimentoEngorde` **sigue sin un solo consumidor en el front** (revalidado)

## V16.1 — Alcance: qué de la Fase 3 entra y qué no
- [x] V16.1.1 ❌ **`marcado_no_diferible_kg` NO entra — sin objeto**: dependía de
      `fn_alimento_marcado_atribucion`, borrada en la reversión de la ronda 4 (verificado: no existe),
      y hay 0 movimientos marcados
- [x] V16.1.2 ❌ **`liquidado_con_saldo_kg` NO entra como columna de `fn_cuadre_alimento_engorde`**:
      cambiar su `RETURNS TABLE` obliga a `DROP FUNCTION` sobre una fn que leen 5 consumidores y
      dispara el gate multipaís, para mover un número que además es de otro grano (por lote liquidado,
      no por galpón activo). Entra como endpoint propio que lee la foto congelada donde ya está
- [x] V16.1.3 ✅ Entran **F3.2** (reporte de liquidados con alimento sin trasladar), **F3.3** (falso
      positivo del aviso) y **F3.4** (exponer el cuadre en el front, en la misma pantalla)

## V16.2 — Implementación
- [x] V16.2.1 `Application/Calculos/AnomaliaAlimentoLiquidadoCalculos.cs` — puro: `KgSinTrasladar`,
      `KgSinRespaldo`, `Clasificar`, `Describir`; tolerancia 1 kg, la misma del cuadre
- [x] V16.2.2 DTO + `ObtenerLiquidadosConAlimentoAsync` en el service del cuadre (partial nuevo, LINQ
      que traduce a SQL, empresa efectiva fail-closed) + `GET /liquidados-con-alimento`
- [x] V16.2.3 Front: servicio + componente con los 2 paneles + tab `cuadre` en Gestión de Inventario
- [x] V16.2.4 F3.3: el modal de liquidación parte el stock por ubicación — kilos de otros galpones no
      alimentan el número, ni el aviso, ni el botón «Realizar traslado»
- [x] V16.2.5 Tests T1-T8 (xUnit) + spec del cálculo puro del front

## V16.3 — Verificación
- [x] V16.3.1 `dotnet build` **0 errores** (9 advertencias preexistentes) · `dotnet test` **2.788 +
      1 en verde** (+14, los T1-T8 y sus variantes)
- [x] V16.3.2 `yarn build` OK (único warning, el de bundle budget preexistente) · `tsc -p
      tsconfig.spec.json` limpio · **Karma sobre el spec nuevo: 7 de 7 en verde** (ChromeHeadless)
- [x] V16.3.3 Smoke **EJECUTANDO** los 2 endpoints (detalle en V16.5). Los endpoints son de solo
      lectura ⇒ no hizo falta clonar la BD y no se escribió una sola fila
- [x] V16.3.4 `fn_cuadre_alimento_engorde` **sin tocar** (`git diff backend/sql` vacío). Su línea
      base local sí se movió respecto del 09-ago, por datos de otras sesiones — ver V16.6

## Fuera de alcance, dicho
- [x] V16.4.1 No se **bloquea** la liquidación con alimento pendiente: la regla del dueño del producto
      es señalar, no impedir. `puedeLiquidarPorAves` queda como está
- [x] V16.4.2 No se corrige ningún dato histórico: los 28 lotes congelados con saldo quedan como están

## Señalamiento a otro bloque (NO toco su checkbox)
- [x] V16.7.1 Esto **cierra** el pendiente «Fase 3 — señalamiento de la anomalía R2» que quedó abierto
      en el bloque *«v16 de engorde — FASE 1 IMPLEMENTADA»* (sección «Lo que NO entra en esta fase»).
      Su checkbox se deja como está —es de otra sesión—; lo que entregó esta Fase 3 y lo que
      deliberadamente NO entró (las 2 columnas en `fn_cuadre_alimento_engorde`) está en V16.1

## V16.5 — Resultado del smoke (17ago26)

Backend propio en `:5501` con content root del API (los 2 endpoints son **solo lectura**: no hizo
falta clonar la BD, y no se escribió una sola fila). Sesión de ItalcolEcuador.

| Llamada | Respuesta |
|---|---|
| `GET /liquidados-con-alimento` (EC) | **200** · 90 liquidaciones vigentes · **28 con saldo** · 20 sin dato congelado — **idéntico al SQL** |
| `GET /liquidados-con-alimento?soloAnomalias=true` | **200** · **2 filas** |
| `GET /CuadreAlimentoEngorde?soloConProblemas=true` (EC) | **200** · 36 galpones · 36 cuadran · **0 descuadrados** |
| `GET /CuadreAlimentoEngorde?soloConProblemas=true` (PA) | **200** · 30 galpones · **5 descuadrados** · 19 con días en negativo |
| `GET /liquidados-con-alimento` (Agroavicola Sanmarino, sin engorde) | **200** · 0 filas · estado vacío explicado |

- [x] V16.5.1 🔑 **El titular «28 de 90 liquidaciones dejaron alimento» es engañoso, y el reporte lo
      desarma: 26 de esas 28 SÍ trasladaron el sobrante.** Sólo **2** son anomalía viva:
      · lote **61** (45/G0057, CAROLINA): saldo congelado 2.880 kg, salidas 800 ⇒ **2.080 kg sin
      trasladar y stock 0** ⇒ `Sin respaldo físico` — los consumió otro ciclo;
      · lote **86** (43/G0055, Sacachún 2): saldo 15.540, salidas 14.440 ⇒ **1.100 kg pendientes** con
      9.980 kg de stock que los respalda ⇒ `Pendiente en el galpón`. **Son exactamente los 1.100 kg
      que el gate de la v16 documentó como «fantasma contable» de ese galpón** — el reporte los
      encuentra solo, sin la fn de atribución que se revirtió
- [x] V16.5.2 **La columna «Ciclo siguiente» es la que hace accionable la fila**: en los dos casos ya
      hay otro lote encasetado en el galpón (2603 del 10-jun y 2604 del 03-ago), así que la decisión
      es «trasladar» o «dejar constancia de que lo toma el ciclo siguiente», no «buscar 2.080 kg»
- [x] V16.5.3 **Smoke de UI (front `:4200` + back `:5002`, sesión inyectada en `localStorage`)**: el
      tab **Cuadre alimento** monta, los dos paneles cargan y **apagan el spinner en pantalla**
      (`changeDetection: Eager`, el bug recurrente de v22 no aparece); 0 errores en consola
- [x] V16.5.4 **F3.3 verificado sobre el caso real**: lote 211, SAN GUILLERMO 37/198400/Galpon-11.
      El modal muestra **0 kg** de «Alimento disponible (inventario galpón)», **no** dispara «Hay
      alimento en inventario», **no** ofrece «Realizar traslado», y pinta aparte los **49.080 kg** de
      los 9 galpones vecinos diciendo que *no son de este lote*. Antes ese número era el que salía como
      alimento del galpón
- [x] V16.5.5 **Sin regresión en el camino bueno**: lote 108 (39/464969/G0038, con 15.390 kg propios)
      sigue mostrando sus 15.390 kg, el aviso y el botón «Realizar traslado», con 0 filas ajenas
- [x] V16.5.6 **Backend y front apagados · puertos 4200/5002/5501 libres · BD compartida sin una sola
      escritura del smoke**

## V16.6 — Un dato de la verificación que hay que decir

- [ ] V16.6.1 ⚠️ **La línea base del cuadre en la BD local ya no es «61 filas / 1 descuadrado»**: hoy
      `fn_cuadre_alimento_engorde(NULL)` devuelve **66 filas y 5 descuadrados, todos de Panamá**
      (granja 106 DOÑA MARIA: G0483 +23.300 kg, G0475 +18.650, G0481 −9.805, G0476 +2.496 y el
      preexistente G0477/lote 182 +544). Ecuador sigue en 0. **Nada de esto lo produjo esta entrega**
      —no se tocó una línea de SQL (`git diff backend/sql` vacío) y los endpoints solo leen—: es la BD
      local, que otras sesiones movieron desde el 09-ago. Queda anotado porque el número viejo estaba
      escrito como referencia en varios bloques, y porque **son descuadres que ahora una pantalla
      muestra**: alguien tiene que mirar si son de la carga local o si Panamá los tiene en prod

---

# V17 · V8 — los descuadres de alimento de Panamá tienen nombre (17ago26)

**Plan:** [`fase_de_desarrollo/descuadres_alimento_panama_diagnostico_plan.md`](fase_de_desarrollo/descuadres_alimento_panama_diagnostico_plan.md)
Pedido: «seguí con el siguiente pendiente del tracker» ⇒ el bloque **V8**, que quedó ABIERTO desde el
16ago26 y que **V16.6.1** volvió a poner sobre la mesa (la pantalla nueva del cuadre los dejó a la
vista). Bloque propio — no tocar desde otras sesiones.

## V17.0 — Línea base re-medida ✔
- [x] V17.0.1 **La tabla de 6 filas de V8 ya no es la de hoy**: son **5 descuadrados / 54.795,4 kg**.
      Y una cambió de lote: G0483 pasó del 187 al **190** al arrancar el ciclo siguiente conservando el
      mismo descuadre de 23.300 kg ⇒ **el descuadre viaja con el GALPÓN, no con el lote**
- [x] V17.0.2 Panamá: 30 galpones, 5 descuadrados, **19 con días en negativo**. Ecuador: 36 galpones,
      **0 y 0**

## V17.1 — Los 5 patrones, resueltos
- [x] V17.1.1 ✅ **V8.5 contestado**: la MISMA fn da 0/0 en Ecuador ⇒ **es dato de Panamá, no el
      cálculo**. Contraejemplo más fuerte: Ecuador hace **400 ajustes manuales de stock (1.989.212 kg)**
      contra 73 de Panamá y aun así cuadra — el ajuste por sí solo no descuadra
- [x] V17.1.2 ✅ **V8.1 cerrado (patrón C)**: el lote 168 hoy da `saldo 10.609,560 · stock 10.609,560 ·
      mov_post 0 · descuadre 0,000`. Al cargarse un seguimiento posterior al movimiento, éste dejó de
      ser «posterior» y el descuadre **se disolvió sin tocar un dato** ⇒ era el corte por fecha, no un
      error. 6 → 5
- [x] V17.1.3 🔑 **CAUSA RAÍZ del patrón A — la hipótesis de V8 era la equivocada.** No es «alimento
      que entró sin registrarse»: es que **la operación corrige el inventario editando o borrando el
      STOCK** (`AjusteStock` / `EliminacionStock`), y esos movimientos se espejan como **`INV_OTRO`**,
      que `fn_seguimiento_diario_engorde` **no lee en ninguno de sus 5 lugares**
      (`apert_mov`, `hist_full`, `hist_alimento`, `docs_por_fecha`, `fechas_universo`)
- [x] V17.1.4 ✅ **V8.3 cerrado con aritmética exacta, no con hipótesis**:
      · **G0477** (+544,0) = un `AjusteStock` de **544,0** kg del 29-jul — exacto;
      · **G0475** (+18.650,4) = un `EliminacionStock` de **18.650,356** kg del 07-ago — exacto;
      · **G0483** (+23.300,0) = **12.500** (ingreso duplicado el 01-ago cuyo registro de stock borraron
      ese mismo día, dejando vivo el `INV_INGRESO`) **+ 10.800** (ajuste del ítem 213 de 24.000 → 1.200,
      de los que 12.000 nunca estuvieron en el histórico) — exacto.
      **42.494,4 kg de 54.795,4 (78 %) son correcciones manuales de inventario**, no alimento perdido
- [x] V17.1.5 ✅ **V8.2 cerrado — la sospecha del cruce «33 - 1» ↔ «33 - 1» NO se sostiene.** El
      inventario de los dos galpones está internamente consistente (G0483 y G0481 cuadran movimiento a
      movimiento contra su stock) y cada descuadre tiene su propia causa: G0483 es patrón A (ajustes) y
      G0481 es patrón B (fechas). No hay alimento imputado al galpón equivocado
- [x] V17.1.6 **Los 2 descuadres que NO son ajustes**: · **G0476** (+2.496) tiene el inventario
      consistente pero **dos lotes conviviendo** (185 y 202) y 43.251 kg de consumo en inventario
      contra 32.708 kg de seguimiento ⇒ consumo sin seguimiento detrás; · **G0481** (−9.805, 7 días
      negativos) arranca su seguimiento el 05-ago con la tabla **ya en negativo** ⇒ es patrón B
- [x] V17.1.7 ✅ **V8.4 cerrado — datado**: el lote **161** (G0472, 28 días negativos, descuadre 0)
      tiene su primer ingreso fechado el **22-jun** (11.779,9 kg) y el siguiente el **08-jul**, contra
      **32.977,3 kg** de consumo hasta el 07-jul. Y el dato que lo explica todo: **los 22 ingresos se
      registraron el mismo día, el 28-jul**, con la fecha puesta hacia atrás ⇒ **carga histórica de un
      mes entero**. El total cuadra: lo que está mal es CUÁNDO. **Re-fecharlos exige las remisiones
      físicas** — cualquier reparto inventado cuadra igual de bien, así que es decisión de operación

## V17.2 — Lo único que se implementa: que el cuadre DIGA lo que encontró
- [x] V17.2.1 `CuadreAlimentoEngordeCalculos` + DTO: `AjustesManualesKg` / `AjustesManualesCount` y un
      detalle que los nombre. **El `descuadre_kg` NO se mueve**: un ajuste manual no es ruido de
      medición como la reserva de V7.37, es una corrección real que hay que decidir — se informa, no se
      compensa
- [x] V17.2.2 El service agrega los `AjusteStock`/`EliminacionStock` por ubicación **dentro de la
      ventana del ciclo activo** (los anteriores ya los tomó la apertura al arrancar el ciclo)
- [x] V17.2.3 Front: columna «Ajustes manuales» en el panel del cuadre
- [x] V17.2.4 Tests T1-T6 (sin ajustes ⇒ el detalle queda **byte a byte** como hoy). T5 cazó un
      defecto de redacción antes de que lo viera nadie: el plural salía «5 vezces»

## V17.3 — Verificación
- [x] V17.3.1 `dotnet build` **0 errores** (8 advertencias, una menos que antes; ninguna nueva) ·
      `dotnet test` **2.794 + 1 en verde** (+6)
- [x] V17.3.2 `yarn build` OK (único warning, el de bundle budget preexistente)
- [x] V17.3.3 Smoke de las 2 empresas ejecutando el endpoint: **Panamá** marca los 3 galpones
      (G0483 3 ajustes/35.302 kg · G0475 5/25.862,5 · G0477 1/544,0 exacto) y deja **sin texto** a los
      2 que no son ajustes (G0481 y G0476); **Ecuador** queda idéntico — 36/36 cuadran y **0 filas** con
      el texto nuevo
- [x] V17.3.4 `git diff backend/sql` **vacío** ⇒ no aplica el gate multipaís · puertos libres

## Lo que NO se toca, dicho
- [x] V17.4.1 **Cero correcciones de datos.** Ni los 42.494 kg de ajustes, ni las fechas de los lotes
      161 y 142, ni el consumo sin seguimiento de G0476. Cada uno necesita el documento físico y el OK
      del usuario, y V8.6 exige simular + revertir + gate antes de tocar nada
- [x] V17.4.2 **No se hace que `fn_seguimiento_diario_engorde` lea `INV_OTRO`.** Es el arreglo de fondo
      —que la corrección de stock llegue a la tabla diaria— pero mueve el saldo de TODAS las empresas y
      exige el gate de paridad multipaís completo: va en su propio plan, con su propia compuerta.
      **Queda como el pendiente técnico más importante que deja este diagnóstico**
- [x] V17.4.3 **No se bloquea el ajuste manual de stock**: es la herramienta con la que la operación
      arregla sus errores. Lo que faltaba era que dejara rastro visible en el cuadre

## Señalamiento al bloque V8 (marco sus checkboxes porque estaba «para otra sesión» y la tomé)
## V17.6 — Lo que el smoke corrigió de este mismo plan (17ago26)

- [x] V17.6.1 🔴 **Escribí que Ecuador cuadra «porque sus ajustes son viejos y los absorbe la apertura».
      Es falso y el smoke lo desmintió**: Ecuador tiene **5 galpones con ajustes DENTRO del ciclo activo
      (41.210 kg)** y los **36 cuadran**. El sentido tampoco lo explica: las dos empresas ajustan
      mayormente hacia abajo (Ecuador −1.330.717 kg en 229 ajustes; Panamá −334.567 en 56). Corregido en
      el plan, en el service y en el doc del cálculo
- [x] V17.6.2 **Lo que SÍ queda probado**: el hueco estructural (`INV_OTRO` invisible para las 5 CTE de
      la fn) y que en 3 galpones de Panamá el descuadre se reconstruye al kilo desde las correcciones
      manuales. El ajuste es la **primera pista**, no el veredicto — por eso se informa y **no** se resta
      del descuadre

- [x] V17.5.1 V8.1 · V8.2 · V8.3 · V8.4 · V8.5 quedan **cerrados por este bloque** (evidencia en V17.1).
      **V8.6 sigue abierto por definición**: es el protocolo para el día que se corrija algo, y hoy no
      se corrigió nada

---

# V18 · El saldo guardado se separó de la fn en Panamá — y la liquidación lo congela (17ago26)

**Plan:** [`fase_de_desarrollo/saldo_alimento_persistido_vs_fn_panama_plan.md`](fase_de_desarrollo/saldo_alimento_persistido_vs_fn_panama_plan.md)
Pedido: «seguí con el siguiente pendiente del tracker» ⇒ el «No verificado (declarado)» del bloque
*«Auditoría de cierre — alimento previo al encaset»*: *«Descuadre persistido vs fn en Panamá (69 filas,
hasta 23.355 kg): NO se determinó si necesita la migración `Recalcular…`»*.
**Respuesta: sí la necesita.** Bloque propio — no tocar desde otras sesiones.

## V18.0 — Medición ✔
- [x] V18.0.1 Comparación fila a fila por `seg_id` de la columna guardada contra la fn:
      **ItalcolPanama 109 filas / 36 lotes** (peor **23.355,0 kg**, Σ absoluta 682.885 kg) ·
      **ItalcolEcuador 0 de 5.189** (abiertos y cerrados). El dato de la auditoría (69) creció a 109
- [x] V18.0.2 🔴 **Por qué importa, y no estaba escrito**: `LiquidacionCongeladaAplicador` toma el saldo
      del **último día directo de la columna guardada** y lo escribe en la copia congelada. Una foto
      congelada no se reescribe ⇒ si la columna está desalineada ese día, el número queda mal **para
      siempre**, y de ahí lo leen Costos, el modal de liquidación y el reporte de «liquidados con
      alimento sin trasladar» de V16
- [x] V18.0.3 **6 lotes de Panamá tienen HOY el último día divergente** (peor **9.844 kg**): si se
      liquidan antes de recalcular, congelan un saldo que después nadie puede corregir
- [x] V18.0.4 **La forma de la divergencia**: la diferencia de un día es **exactamente** el ingreso que
      la fn atribuye al día siguiente, y al día siguiente las dos fuentes vuelven a coincidir ⇒ columna
      escrita con otra atribución de fecha. Más una **cola acumulativa** en los últimos días (la columna
      dejó de actualizarse)
- [x] V18.0.5 **Descartadas con datos** dos explicaciones plausibles: no es la doble validación (las 109
      filas están `validado = true` sin `validado_at`, igual que las 912 que coinciden) ni «movimiento
      registrado después» por sí solo (lo tienen el 90,8 % de las que difieren **y** el 94,5 % de las
      que no)

## V18.1 — Simulación antes de escribir nada ✔
- [x] V18.1.1 `BEGIN` → recálculo desde la fn → verificación → `ROLLBACK`: cambia **109 filas, todas de
      ItalcolPanama** (682.885 kg de movimiento absoluto), **0 de ItalcolEcuador**, y deja **0
      divergencias**. Medido dentro de la misma transacción, revertido

## V18.2 — La migración
- [x] V18.2.1 `20260818010000_RecalcularSaldoAlimentoEngordePersistido`, calcada de
      `20260730141000_RecalcularSaldoAlimentoEngordeV12`: backup con `WHERE NOT EXISTS`, `UPDATE` con
      `IS DISTINCT FROM` (idempotente) y `Down` que restaura. El valor sale de la **propia fn** — una
      sola fórmula por número
- [x] V18.2.2 Designer clonado del último real · **ModelSnapshot intacto** (verificado: `git status` no lo toca)

## V18.3 — Verificación
- [x] V18.3.1 `dotnet build` **0 errores** · `dotnet test` **2.794 + 1 en verde**
- [x] V18.3.2 `dotnet ef database update` (tools EF 10, desde Infrastructure) aplicó sin error:
      **109 filas cambiadas, TODAS de ItalcolPanama** (7 venían en NULL; peor delta **23.355,0 kg**),
      **0 de ItalcolEcuador**. Divergencias después: **0**. Backup de 6.258 filas para el `Down`
- [x] V18.3.3 **Idempotencia probada, no declarada**: se volvió a correr el mismo `Up()` ⇒
      `INSERT 0 0` en el backup y `UPDATE 0` en el recálculo
- [x] V18.3.4 `fn_cuadre_alimento_engorde` congelado antes y comparado después: **`diff` vacío en las
      66 filas** (mismo descuadre, mismos días negativos). El número que mira operación no se movió
- [x] V18.3.5 `git diff backend/sql` **vacío** ⇒ ninguna función SQL tocada

## V18.5 — El efecto que se buscaba
- [x] V18.5.1 **Los 6 lotes de Panamá que iban a congelar un saldo equivocado quedaron en 0**: hoy
      ningún lote de ninguna de las dos empresas tiene el último día divergente (Ecuador 0 de 118,
      Panamá 0 de 37). Lo que se liquide desde ahora congela el mismo número que muestra la grilla

## Fuera de alcance, dicho
- [x] V18.4.1 **No se toca `fn_seguimiento_diario_engorde`**: la columna se alinea a la fn, nunca al
      revés
- [x] V18.4.2 **No se corrige la causa de fondo** de la cola acumulativa (que el recálculo no corra en
      todos los caminos que mueven un día ya cargado). Esta migración deja la foto alineada hoy; que no
      se vuelva a desalinear es otro trabajo, con su propio plan
- [x] V18.4.3 **No se tocan las copias congeladas** ya existentes: las 90 de Ecuador quedan como están
      (y allí la columna ya coincidía)

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
- [ ] V19.2.1 Hoy el saldo es `entradas de la GRANJA − consumos de ESTE padre` ⇒ **sobreestima** tanto
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
- [ ] V20.4.1 **Decisión pendiente sobre el lote 12**: (a) dejarlo —no contagia a nadie y Ecuador sigue
      con 0 descuadrados—; (b) **completar la reconstrucción** cargando los 9.020 kg faltantes con su
      fecha real desde las remisiones físicas (la única corrección legítima); (c) liquidarlo como está,
      que **congelaría −9.020 para siempre** (V18: la foto no se reescribe)
- [ ] V20.4.2 ⚠️ **Si se decide cerrar los lotes 2 y 12** —el otro pendiente del mismo bloque—, para el
      **12 conviene resolver esto primero**: liquidar antes de completar la carga congela el negativo

## Fuera de alcance, dicho
- [x] V20.5.1 **Cero correcciones de datos**: ni los 9.020 del lote 12, ni los 4 congelados de Ecuador,
      ni los 3 de Panamá (ésos ya los cubre V17)
- [x] V20.5.2 **No se toca ninguna fn ni `LiquidacionCongeladaAplicador`** (V20.3 explica por qué la
      convención actual es la correcta)

---

# V21 · V19.3.4 — el aviso del kardex de bultos, verificado EN PANTALLA (17ago26)

**Plan:** [`fase_de_desarrollo/verificacion_visual_aviso_bultos_plan.md`](fase_de_desarrollo/verificacion_visual_aviso_bultos_plan.md)
Pedido: «seguí con el siguiente pendiente del tracker» ⇒ **V19.3.4**, la única verificación que la
sesión anterior dejó explícitamente «pendiente de la próxima sesión que abra esa pantalla», y el
único abierto que no espera decisión de nadie. Bloque propio — no tocar desde otras sesiones.

## V21.0 — Re-triage de lo que quedaba abierto ✔
- [x] V21.0.1 Repasados los bloques abiertos: siguen esperando **decisión del usuario** (lotes 132 /
      3,4,6,8; grupo A; K345; tile Venta Engorde; Santa Reyes; lote 12 de V20; Fase 2 de V19), un
      **admin externo** (correo Office 365) o un **deploy/push** (P.1-P.3, PWA). §2.3b y §2.3c siguen
      descartados por V15.0.2
- [x] V21.0.2 Elegido **V19.3.4**: es el último tramo sin verificar de una entrega ya commiteada
      (`2f94a01`) y no depende de nadie

## V21.1 — Lo que la verificación encontró: el aviso estaba bien, la PANTALLA no ✔

- [x] V21.1.1 Padres vivos por granja re-medidos hoy: **LA ESMERALDA 4** (114 · 115 · 116 · 117),
      MANGOS 4, MIRALINDO 2, NIZA III 1, Demo 1 c/u ⇒ la foto de V19.0.2 sigue vigente
- [x] V21.1.2 🔑 **V19.3.4 no era una limitación del harness.** Al conducir la cascada real, el tab de
      la semana —el que abre el panel BULTO— resultó **inusable**: cada `ref` moría entre leer y
      hacer clic. La causa la dice la propia consola de Angular: **`NG0956`** *(«track by identity
      caused re-creation of the entire collection»)* + **`NG0100`**, repetidos en cada ciclo
- [x] V21.1.3 **Causa raíz**: `get semanasParaSubloteActual()` proyectaba las semanas en CADA lectura
      ⇒ array nuevo de objetos nuevos por ciclo de change detection, y la plantilla lo recorría con
      `track reporteSemanal` (identidad). Angular destruía y recreaba los tabs y el panel de la semana
      activa —donde vive la sección BULTO y el aviso de V19— sin parar. Es exactamente lo que CLAUDE.md
      prohíbe: *«no conviertas getters usados en el template en getters que devuelven arrays/objetos
      nuevos por ciclo»*
- [x] V21.1.4 **Medido, no deducido**: el nodo del tab cambiaba de identidad cada 300 ms
      (`mismoNodo === false`) y quedaba **sin rótulo** (40 px de ancho, `textContent` vacío) aunque
      `getTabLabel(44)` devolvía *«Sem 44 (13/8-19/8)»*. Es decir: **el usuario veía un tab en blanco**
- [x] V21.1.5 El getter se leía ~15 veces por ciclo (3 `@for` + `@if` + `getTabLabel` + los 8
      totalizadores), y cada lectura re-proyectaba el reporte entero

## V21.2 — Arreglo: memorizar el getter (sin tocar el cálculo)
- [x] V21.2.1 El cuerpo del getter se movió **tal cual** a `calcularSemanasParaSublote(r, sublote)`
      —misma aritmética, mismo orden, mismo arrastre de saldos— y el getter ahora memoriza el
      resultado contra sus DOS únicas entradas (`reporte()` y `selectedSublote`)
- [x] V21.2.2 `track reporteSemanal` → `track reporteSemanal.semanaContable` en los 3 `@for` (la
      semana es única en la lista). Es el arreglo que nombra el propio NG0956
- [x] V21.2.3 **Sin cambio de comportamiento**: sin sub-lote elegido se sigue devolviendo el array del
      reporte tal cual (la misma referencia de antes); con sub-lote, la proyección es la misma función

## V21.3 — Verificación
- [x] V21.3.1 `yarn build` **0 errores** (único warning: el de bundle budget, preexistente)
- [x] V21.3.2 `yarn test` **325 SUCCESS** — incluye los **6 casos nuevos** de
      `frontend/src/tests/reporte-contable-semanas-memo.spec.ts`, que fijan el invariante roto
      (mismas entradas ⇒ misma referencia) y que memorizar no movió los números (arrastre de saldos)
- [x] V21.3.3 **T-VIS-1 (positivo)** — lote **114 A374A / LA ESMERALDA / Levante**, semana 44: el aviso
      **pintado en pantalla**, bajo el título BULTO y encima de la tabla (1116×32 px, borde ámbar
      `#fbbf24`, fondo `#fffbeb`, `role="note"`), con el texto completo de V19. **Con captura**
- [x] V21.3.4 **T-VIS-2 (control)** — lote **13 K345A / NIZA III / Levante**, semana 81: el panel BULTO
      se pinta y **no hay un solo `.alcance-aviso`** en toda la página (`lotesPadreEnGranja: 1`).
      **Con captura**
- [x] V21.3.5 Recorrido completo (carga → cascada → generar → abrir la semana) con **0 mensajes
      `NG0xxx`** en consola: NG0956 y NG0100 desaparecieron. El tab ahora es estable y **muestra
      «Sem 44 (13/8-19/8)»**
- [x] V21.3.6 **Cero escrituras en la BD compartida**: `farm_inventory_movements` 326 = 326, `lotes`
      17 = 17 antes y después. Backend y front apagados · puertos **5002 / 4200 / 9333 libres**

## Lo que NO se tocó, dicho
- [x] V21.4.1 **Ni un número del reporte**: el arreglo es de render. `proyectarSemanaParaSublote`,
      `AcumularSaldos` y todo el backend quedaron intactos (`git diff backend` vacío)
- [x] V21.4.2 **V19.2.1 sigue abierto** (el saldo coherente: entradas de la granja − consumos de este
      padre). Es decisión de producto, no entra acá
- [x] V21.4.3 Los 4 lotes padres de LA ESMERALDA se muestran en el combo como **«A374» los cuatro**,
      sin nada que los distinga (el `codigoErp` viene vacío). No se tocó: es otra pantalla y otro
      pedido, pero conviene saberlo — hoy hay que adivinar cuál se está eligiendo
- [x] V21.4.4 `GET /movimientos-huevos?lotePadreId=114&semanaContable=44` responde **400** para ese
      lote en Levante. **Preexistente** y ajeno a esta entrega (el cambio es 100 % del front); queda
      anotado porque salió a la vista durante el smoke
