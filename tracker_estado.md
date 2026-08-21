# Tracker de estado

> **Depurado el 20-ago-2026** — de **3.019 líneas a ~300**. Salieron los **32 bloques cerrados**
> (viven abajo, una línea cada uno) y se **aplicaron los veredictos de la auditoría V42** (19-ago),
> que nadie había bajado al archivo: **15 de los 45 pendientes estaban muertos** —ya resueltos,
> obsoletos o duplicados—. Quedan **26 pendientes reales**, consolidados arriba.
>
> **Nada se perdió.** El texto íntegro de cada bloque archivado está en
> `git show e971871:tracker_estado.md`; las depuraciones previas, en `git show fd542b9:...`,
> `git show 1b64551:...` y `git show 30fe5a2:...`.
>
> **Regla de sesiones en paralelo:** cada sesión toca **sólo su bloque**; los bloques nuevos van
> **al final**, separados por `---`. Nunca borres el bloque de otra sesión: ante la duda, agregá abajo.

| Marca | Significa | ¿Un agente lo puede ejecutar? |
|---|---|---|
| `- [ ]` | **Tarea ejecutable**: hay código que escribir y una verificación que correr | ✅ sí |
| `- [x]` | Hecho y verificado | — |
| `- [!]` | **Requiere una decisión tuya** o un OK explícito (varias son irreversibles en prod) | ❌ no, hasta que decidas |
| `- [~]` | **Fuera del repo**: admin de Microsoft 365, paso manual en pantalla, secretos, deploy | ❌ no |
| `- [i]` | **Hallazgo o nota**: el registro de un hecho medido. No hay nada que ejecutar | ❌ no hay acción |

---

## 🔴 Lo primero: 17 commits viven sólo en este disco

Medido hoy con `git fetch --dry-run` (el remoto no tiene novedades): `origin/main` sigue en
`6e4fe7f` (18-ago) y `origin/main-produccion` en `79aeccf` (PR #74, 18-ago). **Nada de lo hecho del
18-ago a hoy está respaldado ni desplegado**:

| Commits | Qué es |
|---|---|
| `c9a7349` | **V39/B1** — revocación de sesión, la deuda más urgente de la PWA |
| `473ac16` `d3a91dd` `26007db` | los **3 arreglos del saldo de bultos** del reporte contable |
| `e4475fc` | su validación contra la copia de producción: **13 de 13** |
| `d0d9334` | el modal de engorde **descontaba inventario que el backend ya descuenta** |
| `434293f` `d57329c` `e971871` | **X1** — el id de galpón ocupado + los 3 galpones de Modulo IV (migración) |
| `8c141e6` `ccd0941` | **V49** (cuadre de engorde) y **V50** (gate del `.sql`, corta el CI) |
| `f33c700` `fb314ae` `501d51b` `a2ec07c` `30fe5a2` `f1d608c` | refactor, docs y mediciones |

Es el **«Riesgo #1»** que el bloque de la PWA levantó el 12-ago con 18 commits, repetido tal cual.

---

## Pendientes reales — 24, revalidados el 20-ago-2026 (segunda pasada, con AWS + local back/front)

**6 tareas · 6 decisiones · 12 fuera del repo.** Cada uno trae su bloque de origen para recuperar el
detalle con `git show e971871:tracker_estado.md`.

> **Segunda pasada la misma tarde**, con herramientas que la primera no tenía: credenciales AWS reales
> (`aws sts get-caller-identity` respondió) y el proyecto local levantado (back :5002 en un worktree
> aislado + front :4200, Node/​dotnet portables). Bajó 2: **C8** se cerró de verdad (smoke real en el
> navegador) y **A3** se degradó a hallazgo (el código y el caso que lo motivó ya estaban resueltos,
> nadie lo había cruzado). Los demás bloqueantes se **confirmaron**, no se asumieron — el detalle de
> cada verificación va en su propia línea.

### A · Tareas ejecutables — hay código o comandos que correr

- [x] **A1 · Push + merge + verificación post-deploy de los 17 commits.** **Resuelto el 20-ago-2026
      fuera de esta sesión** (push+merge a `main-produccion` los hizo otra sesión/el usuario, PR #75,
      commit `652366a`) — **verificado acá con el checklist obligatorio de CLAUDE.md §🚀**:
      `aws ecs describe-services` → TaskDef viva `sanmarino-back-task:161`, `rolloutState COMPLETED`,
      1/1 running; `aws ecs describe-task-definition` → imagen
      `...backend:652366ab1959a2c6d3a7cd54a08feff4e62e4420`, coincide EXACTO con el merge commit de
      PR #75. No es un rollback silencioso. Arrastra `20260820055219_SeedGalponesModuloIvNizaIii` (X1),
      `AddSesionesActivas` (V39) y `20260819120000_SeedTicketPlanItalappSantaReyes` (X3, caso
      Santa Reyes en ItalJira) — las tres migraciones ya corrieron en prod
      (`Database__RunMigrations=true`)
- [ ] **A2 · V39.13 — cerrar la ventana de gracia** de los tokens sin `jti`: hoy `Evaluar` devuelve
      `Legado` y los acepta. Va **después** de A1 y de verificar la revocación en prod.
      ⚠️ Trampa: `SesionActivaService` devuelve `Legado` **también ante un fallo de BD** (fail-open
      deliberado) — borrar esa rama sin distinguir los dos usos convierte una caída de BD en un
      **logout masivo**. Commit propio y explícito.
      *Releído el código hoy (`SesionActivaService.EvaluarAsync`): el diagnóstico sigue exacto —
      `jti` vacío ⇒ retorna sin consultar nada, rama `Legado` intacta*
- [ ] **A4 · V30.7 · H1 Santa Reyes** — flags en `companies` + catálogo de ítems + silo en el form de
      ingreso a granja + homologación ERP + seed de las 5 guías genéticas (540 filas). **Detalle
      granular y estado real → V52 (F0-F2)**
- [ ] **A5 · V30.8 · H2** — semanas por raza (hoy hardcodeadas en
      `modal-seguimiento-diario.component.ts:1463`), consumo sólo hembras, ocultar machos y error de
      sexaje **en UI** (⚠️ no borrar del modelo: lo consumen los saldos), tipos de inventario.
      **→ V52 (F3-F6)**
- [ ] **A6 · V30.9 · H3** — huevos: incubables→sin clasificar, los 7 ítems, primera postura por raza
      con vigencia ≤ semana 22, PNC por catálogo (⚠️ sin tocar las 11 columnas físicas). **→ V52 (F7-F8)**
- [ ] **A7 · V30.10 · H4** — traslados: aves (exponer `Placa`/`Conductor`/`Sellos` en postura — **ya
      existen en `MovimientoAves`**, falta la UI) y huevos (bodega destino desplegable) + no regresión
      multipaís. **→ V52 (F9-F12)**

> **A4-A7 quedaron DESBLOQUEADAS el 20-ago-2026**: el usuario confirmó en sesión que B5 (aprobación
> del cliente) y C7 (entrega de estructura física + códigos ERP) ya se dieron. Ejecución en curso,
> ver **V52** (checklist granular F0-F12, calcado del desglose real ya sembrado en ItalJira
> `TK-2026-000172`). **A3 se retiró de esta lista** — bajó de categoría, ver el hallazgo nuevo en
> «Muertos» más abajo.

### B · Decisiones tuyas

- [!] **B1 · Lote 12 (KM 86 / Galpon-2): cargar los 9.020 kg.** Ya elegiste la opción (b) —existen las
      remisiones físicas—; **falta el dato de origen**: fecha y kg de cada remisión. El ciclo corre
      17-feb→22-abr-2026 y cierra en −9.020 kg. Va **antes** de cerrar el lote (C5).
      *Orígenes: V20.4 · V25.5.4 · V25.6.6 — eran tres entradas del mismo pendiente*
- [!] **B2 · Los 7 galpones de Panamá con kilos** (69.620,5 kg; el 8.º, G0475, ya está explicado por un
      ajuste posterior). Corregirlos es alimento real: exige decidir si manda el stock o la tabla
      diaria, **simularlo en transacción y revertir** antes de aplicar, con el gate de paridad corrido
      antes y después. Es el bloque **V8**, reservado. *Orígenes: V8.6 · V49.6.1*
- [!] **B3 · La herencia del descuadre entre ciclos + el modelo de entrega.** Hoy el descuadre pasa al
      lote siguiente **en silencio** (medido: G0483 arrastró 23.300,0 kg del lote 187 al 190). La tabla
      `alimento_entrega_ciclo_engorde` existe justamente para eso y está **INERTE**: nadie la lee. Y el
      gate demostró que el mecanismo de ENTREGA **no puede dispararse nunca** (0 de 53 pares con hueco),
      así que el rediseño correcto es **ampliar la ventana D4 del destino**. Es una sola decisión de
      producto, y desbloquea **F2b** (la bandeja de reservados del tab Histórico).
      *Orígenes: V27.1 · V49.6.2 · v16 FASE 1 (F2b)*
- [!] **B4 · Lotes con `Inicio` ficticio (ids 3, 4, 6, 8)**: encaset 50.000 **y** `Inicio` de plantilla,
      cero movimientos — los dos números son inventados. El detector no los ve; el arreglo exige el
      documento físico de encasetamiento. *Origen: bloque «referencia `Inicio`»*
- [x] **B5 · Santa Reyes — aprobación del cliente** (V30.5) del alcance, el cronograma y los supuestos
      (§13 del Word). **Confirmada por el usuario el 20-ago-2026** (en sesión, no hay documento en el
      repo que lo respalde). Desbloquea A4-A7 → ejecución en **V52**
- [!] **B6 · Grupos B y C de Ecuador** (lotes con aves pendientes): **re-medir antes de decidir** — los
      31 abiertos tienen aves y la lista vieja de 39 ya no existe. Panamá **no se toca**

### C · Fuera del repo

- [~] **C1 · Correo caído desde el 3-jun-2026** (85 envíos fallidos, el último el 17-ago). Lo destraba el
      **admin de Microsoft 365**. Camino A: revisar si Conditional Access / Security Defaults bloquea
      legacy auth por ubicación o IP, y que `Get-CASMailbox` / `Get-TransportConfig` devuelvan
      `SmtpClientAuthenticationDisabled = False` para el buzón `zootecnico`. Camino B (sólo si el A no
      se puede): volver a OAuth 2.0 / Graph — la implementación completa está en `git show c7b6834`
- [~] **C2 · Subir `JwtSettings__DurationInMinutes` a 960 en la TaskDef.** La viva (160) trae **60** y
      **pisa** el `appsettings`. Mientras siga así, el `authGuard` expulsa al minuto 61 sin señal: la
      jornada offline de 16 h **no existe** para el operario. Va después de verificar B1 en prod (A1)
- [~] **C3 · B8 — rotar las 4 llaves de `environment.prod.ts`.** Las genera el usuario; no se generan acá
- [~] **C4 · Gerencia, post-deploy manual** (la migración no lo hace, a propósito): en Roles y Permisos
      crear/elegir el rol → asignarle **sólo** `tickets.indicadores` → asignarle el menú
      **Gerencia › Panel de control**. Hasta entonces el módulo no lo ve nadie
- [~] **C5 · Cerrar por pantalla los lotes 2601** de Galpon-1 (id 2) y Galpon-2 (id 12): siguen
      `Abierto` con aves vivas (773 y 1.082). Liquidar es una transacción de 5 pasos, no va por
      migración. Para el id 12, **después de B1**
- [~] **C6 · Re-correr el detector de sobregiro de aves contra el dump de PROD.** No es una decisión, es
      un **bloqueo de acceso**: RDS en VPC privada, ECS Exec deshabilitado, IAM sin permisos.
      *Reverificado hoy con credenciales AWS reales* (`aws sts get-caller-identity` responde,
      cuenta `196080479890`): `enableExecuteCommand` = **false** en el servicio ECS, y el TCP al
      endpoint del RDS (`reproductoras-pesadas...rds.amazonaws.com:5432`) **da timeout** desde esta
      máquina — sigue bloqueado, ahora confirmado y no sólo heredado
- [x] **C7 · Santa Reyes debe entregar** la estructura física real (núcleos, galpones, silos, bodegas) y
      los códigos ERP (CO, bodegas, ubicaciones, centros de costo). **Confirmada entregada por el
      usuario el 20-ago-2026** (en sesión). ⚠️ No encontré en Desktop/Downloads un archivo nuevo con
      fecha ≥19-ago distinto de los ya usados en Fase 1 (`Granja.xlsx`/`Items.xlsx`/`Lotes.xlsx`,
      25-jul) — si la entrega llegó por otro canal (correo, verbal), F1.2 la toma como viene; si
      aparece un Excel nuevo, actualizar el seed antes de dar F1.2 por cerrado
- [~] **C9 · Alistamiento de la PWA con red, por usuario y por dispositivo** — **precondición de C10-C13**:
      instalar, entrar una vez (login y reCAPTCHA exigen red) y **visitar las pantallas** que se van a
      usar, o la caché está vacía y ningún escenario significa nada
- [~] **C10 · S-1 · Dos operarios turnándose sin red** — el caso que motiva el multi-slot entero: A
      trabaja, aparca con su PIN, entra B, y cada uno ve **su** caché y **su** cola
- [~] **C11 · S-2 · `/diagnostico` con una sesión ajena activa y sin ninguna sesión**: la fila aparece
      **sin payload**, sin «Copiar captura» y sin «Descartar». Necesita dos sesiones reales
- [~] **C12 · S-3 · Más de 60 min de reloj offline** para ver que el `authGuard` ya no expulsa dentro de
      la jornada. ⚠️ Con la vigencia de producción todavía en 60 min (C2) **este escenario falla hoy**:
      es justamente la comprobación que justifica subirla
- [~] **C13 · S-4 · Revocar una sesión con capturas sin enviar**: al volver la red, 401 con motivo
      «revocada» y las capturas **siguen** en `/diagnostico`. Y con dos device-id distintos detrás de la
      misma IP, las dos colas drenan **sin bloquearse entre sí**

---

> **Bloque abierto — el único con narrativa viva.** Todo lo demás está archivado abajo.

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

- [x] V30.5 (→ **B5**) **Aprobación del cliente** del alcance, el cronograma y los supuestos (§13 del Word).
      **Confirmada 20-ago-2026.** Ejecución arrancó → **V52**
- [x] V30.6 (→ **C7**) Santa Reyes debe entregar, **a más tardar el mar 18-ago-2026 (un día antes del
      inicio)**, la estructura física real (núcleos, galpones, silos, bodegas) y los códigos ERP
      (CO, bodegas, ubicaciones, centros de costo). **Confirmada entregada 20-ago-2026** (2 días
      tarde). ⚠️ En el plan de 2 semanas **F1.2 corre el día 1**: no hay holgura para esperarlos —
      era el riesgo **Alto** #1 del documento, ver nota en V52 sobre dónde vive el archivo
- [i] V30.7 (→ **A4**) H1 · Fundaciones: flags en `companies` + catálogo de ítems + silo en el form de ingreso
      a granja + homologación ERP + seed de las 5 guías genéticas (540 filas)
- [i] V30.8 (→ **A5**) H2 · Ciclo de vida del ave: semanas por raza (hoy hardcodeadas en
      `modal-seguimiento-diario.component.ts:1463`), consumo solo hembras, ocultar machos y error de
      sexaje **en UI** (⚠️ no borrar del modelo: lo consumen los saldos), tipos de inventario
- [i] V30.9 (→ **A6**) H3 · Huevos: renombrar incubables→sin clasificar, los 7 ítems, primera postura por raza
      con vigencia ≤ semana 22, PNC por catálogo (⚠️ sin tocar las 11 columnas físicas), eficiencia
      cuadrada contra el total de granja
- [i] V30.10 (→ **A7**) H4 · Traslados (días 9-10): aves (exponer `Placa`/`Conductor`/`Sellos` en postura) y
      huevos (bodega destino desplegable), + pruebas de no regresión multipaís + despliegue
- [i] V30.11 El **acompañamiento** (F12.2, semana del 2 al 8-sep) quedó **fuera de las 10 jornadas**,
      declarado como bajo demanda. Si se promete dentro del plan, la entrega se corre un día
- [i] V30.12 **Bloque commiteado el 18ago26 21:00** — hasta ese momento vivía sólo en el working tree,
      y el plan al que enlaza (`santa_reyes_requerimientos_italapp_plan.md`) estaba **sin trackear**: un
      commit de cualquier otra sesión lo habría borrado sin dejar rastro. Los dos entregables comerciales
      que declaran V30.2 y V30.3 **existen** en el Escritorio (`.xlsx` 35 kB, `.docx` 27 kB), verificado.
      Commitear **no** cierra el bloque: la ejecución (V30.7-V30.10) sigue abierta
- [i] V30.13 (→ **B5** + **C7**) ⏰ **El cronograma arranca mañana y los dos destrabes siguen sin resolverse.** Hoy es
      **mar 18-ago-2026**: es el último día del plazo de V30.6 (estructura física + códigos ERP) y el día
      previo al inicio (mié 19-ago), con **V30.5 —la aprobación del cliente— todavía sin dar**. Como
      F1.2 corre el día 1 y el plan no tiene holgura, cada día que se demore cualquiera de los dos
      corre la entrega del **1-sep** en la misma medida. Es el riesgo Alto #1 del documento,
      materializándose: decisión tuya, ningún agente lo destraba

---

# Trabajo en vuelo de OTRAS sesiones (20-ago-2026) — NO PISAR

> Estas sesiones **todavía no escribieron su bloque**. Cuando lo hagan, va **al final de este
> archivo**, no acá. Esta sección existe para que nadie borre ni commitee lo que no es suyo.

- [i] **X2 · Permiso de fecha retroactiva + ventana base de 15 días** — **activa ahora mismo**
      (creciendo mientras se escribía esta línea: pasó de 9 a **más de 30 archivos** sin commitear
      entre la primera y la segunda pasada de esta sesión).
      Plan: [`fase_de_desarrollo/permiso_fecha_retroactiva_registros_plan.md`](fase_de_desarrollo/permiso_fecha_retroactiva_registros_plan.md).
      Toca `VentanaFechaRegistroCalculos`/`Guard` (nuevos), 7+ controllers de inventario/traslados,
      y ya llegó al **frontend** (traslados de aves/huevos, movimientos, gastos-inventario) + nueva
      migración `20260820160000_SeedPermisoFechaRetroactivaRegistros`. Toca **la guarda de fecha del
      inventario**: cualquier trabajo sobre `InventarioGestionController` o la ventana D4 choca con
      ella — coordiná antes de tocar. No se lee ni se resume más porque sigue en movimiento; el detalle
      real es el propio `git status` en el momento en que la retomes
- [i] **X3 · El plan de Italapp registrado como caso en ItalJira** — plan
      [`fase_de_desarrollo/ticket_italjira_plan_italapp_santa_reyes_plan.md`](fase_de_desarrollo/ticket_italjira_plan_italapp_santa_reyes_plan.md)
      + migración `20260819120000_SeedTicketPlanItalappSantaReyes` (data-only: historia, caso,
      13 tareas y 29 subtareas = 100 h, solicitante Lenin / empresa Santa Reyes). Por pedido explícito
      del usuario **esa sesión no toca este archivo**: **V30** sigue siendo el dueño del estado.
      ⛔ No apliques esa migración contra una copia de producción que se use para validar otra cosa
      (lo registró V48.1.3)
- [i] **Los archivos de X2 y X3 están sin commitear**: un `git add -A` de cualquier otra sesión se
      los lleva puestos. Commiteá sólo lo tuyo, por ruta

---

## Entregado y archivado

Bloques cerrados al 100 % y commiteados. El detalle completo de cada uno está en el commit; el texto
de los bloques archivados hoy, en `git show e971871:tracker_estado.md`.

| Fecha | Bloque | Commit | Qué dejó |
|---|---|---|---|
| 05ago26 | Gastos de inventario — las 10 líneas con `concepto = 'insumo'` | `2cab258` | Migración data-only con regla dinámica; el catálogo y la auditoría intactos |
| 16ago26 | Gastos de inventario — rango de fechas del consumo | `90f97ad` | Rango Desde/Hasta que acota **igual** la tabla y el Excel |
| 17ago26 | **V9 · Barrido de pendientes** | `1771bd0` `aadd97b` `4a070e8` `f6d2f56` `a19807b` | 2 gates de CI · guard del despacho de aves reservadas · soft-delete en cascada · vacunación W1.1-W1.2 |
| 17ago26 | **V10 · Vacunación W1.3 + W1.4** | `bd935cb` | CRUD de plantillas + pantalla; `efectiva` explica **por qué** un lote quedó sin plan |
| 17ago26 | **Vacunación W2** — materializador | `f2794c6` | La plantilla baja al cronograma; idempotente y **nunca borra** |
| 17ago26 | **Vacunación W3** — bandeja de «hoy me toca» | `59496a8` | `fn_vacunacion_pendientes` + aviso de fuera de rango antes del 400 |
| 17ago26 | **Vacunación W4** — alcance por ubicación | `056a371` | Las 2 fns respetan `restrict_locations` (fail-closed) · **cierra la serie W** |
| 06ago26 | **Tracker** | `b34e629` | Consolidado de sublotes y paridad de reportes por fase |
| 08ago26 | **Auditoría de cierre** | `362155c` | «alimento previo al encaset» + fix del chip (sólo lectura) |
| 12ago26 | **PWA F3.1** | `c44e0a4` | Captura offline (outbox) con idempotencia real |
| 17ago26 | **V11 · Cierre de smokes + limpieza del tracker** | `d74c667` | — |
| 17ago26 | **V12 · V7.27** | `addd777` | el saldo de alimento y el cuadre ignoran `validado` |
| 17ago26 | **Cola de baja prioridad** | `a579d53` | mirar sólo cuando se toque producción |
| 17ago26 | **V13 · Saldo de aves de levante** | `48e9d6a` | cuatro consumidores, dos fórmulas |
| 17ago26 | **V14 · Bloquear el consumo cuando no hay stock** | `f79fd45` | — |
| 17ago26 | **V15 · La excepción D4 es inalcanzable desde la UI** | `6b7abe7` | — |
| 17ago26 | **V16 · Fase 3 de R2** | `a886e90` | señalar el alimento que queda al liquidar |
| 17ago26 | **V17 · V8** | `55c1b40` | los descuadres de alimento de Panamá tienen nombre |
| 17ago26 | **V18 · El saldo guardado se separó de la fn en Panamá** | `ead4635` | y la liquidación lo congela |
| 17ago26 | **V21 · V19.3.4** | `e3762fd` | el aviso del kardex de bultos, verificado EN PANTALLA |
| 17ago26 | **V22 · Aire en el bundle** | `6f083c7` | las pantallas de administración salen del arranque |
| 17ago26 | **V23 · B10** | `56f7caa` | el Super Admin deja de ser un correo en el código |
| 18ago26 | **V24 · La empresa activa se valida** | `75213a9` | cierra el hallazgo V23.3 |
| 18ago26 | **V26 · Engorde FASE A** | `07f1bee` | la marca `para_proximo_ciclo` vuelve a ser inerte |
| 18ago26 | **V29 · PWA F-3** | `7a64d43` | el push deja de firmar el trabajo de un operario con la identidad de otro |
| 18ago26 | **V31 · PWA F-4** | `8b2a096` | la pantalla de rescate deja de mostrar (y borrar) lo que capturó otro |
| 18ago26 | **V32 · PWA F-2** | `d1ac0ef` | el `authGuard` deja de matar la jornada de 16 h a los 60 minutos |
| 18ago26 | **V33 · PWA F-5** | `0a3b661` | cerrar sesión dejaba de borrar el alistamiento de los demás |
| 18ago26 | **V34-V38 · PWA multi-slot** | `9b6b157` `aa32fcc` `b7270d4` `1786f98` `6e4fe7f` | el llavero, el selector de perfil, el sidebar y la jornada por slot |
| — | — | — | **▼ archivados en la depuración del 20-ago-2026 ▼** |
| 05ago26 | **Correo — SMTP-only** tras el retiro de la auth básica | `c7b6834` (Graph, revertido) | un solo transporte; lo que falta es del admin M365 → **C1** |
| 06ago26 | **Referencia `Inicio` + liquidación de corridas anteriores** | `d341223` | migración de corrección; queda **B4** y **B6** |
| 07ago26 | **ItalJira — historias, tareas y tiempos** | `f8f887a` | módulo entregado + 2 bugs que cazó el smoke; el detector → **C6** |
| 08ago26 | **Reporte Contable — Selección en RESUMEN + Movimientos de Huevo** | `d299a8a` | validado contra los informes de Verenice; el corte de etapa quedó como hallazgo, ver «Muertos» |
| 07ago26 | **Migraciones Masivas — retirar tipos Ventas/Aves/Huevos** | `cbc922c` | «Venta Engorde» **se queda** (decidido en V25.7.1) |
| 07ago26 | **Migraciones Masivas — permiso POSTURA, sólo Sanmarino** | `07c9c0c` | Santa Reyes lo pierde: decidido **y ya aplicado en datos** |
| 09ago26 | **Lote cerrado que absorbe el ciclo siguiente (KM 86)** | `7339c61` | fn v14 + ventana de mes actual; cerrar los 2601 → **C5** |
| 18ago26 | **v16 de engorde — FASE 1 revertida (NO-GO del gate)** | `f4b96e3` `07f1bee` | la marca quedó inerte y deshabilitada; el rediseño → **B3** |
| 12ago26 | **PWA — auditoría de acceso offline** | `30c6865` | el menú 9 ya no lo tiene nadie (0 filas) |
| 12ago26 | **PWA — punto de retoma** | `88f1d3d` | **superado**: la PWA se desplegó el 18-ago (PR #74) |
| 12ago26 | **PWA — brecha para salir a producción** | `71836ff` | el deploy se hizo y se verificó; quedan **C9-C13** |
| 13ago26 | **Gerencia — Panel de control (`tickets.indicadores`)** | `6e3b167` | solo-lectura global; el rol y el menú → **C4** |
| 17ago26 | **Bitácora de sesiones agosto (W/I · V3 · V5 · V7 · V8)** | `1b64551` | 107 casillas cerradas; lo único vivo era V8.6 → **B2** |
| 17ago26 | **V19 · el kardex de bultos es de la GRANJA** | `7a1f678` | el aviso en el reporte; el número fino lo cerró V41 |
| 17ago26 | **V20 · saldo negativo del lote 12 (KM 86)** | `01eaa4b` | auditoría de solo lectura: no se contagia; la carga → **B1** |
| 18ago26 | **V25 · triaje del tracker + 5 planes en paralelo** | `babad34` `6ce89cc` | lote 132 y K345 implementados; lo vivo quedó en A/B/C |
| 18ago26 | **V27 · Engorde FASE B — el hecho persistido entra INERTE** | `5763fcb` | tabla, triggers, 34 tests, mutación 17/17; activarla → **B3** |
| 18ago26 | **V28 · columna «Próx. ciclo» en el tab Histórico** | `7325f95` | smoke real 20-ago (local, ver «Muertos»): header + scroll OK |
| 18ago26 | **V39 · B1 — revocación de sesión (`jti` + `sesiones_activas`)** | `c9a7349` | lista blanca por `jti`; **sin desplegar** → **A1 / A2 / C2** |
| 19ago26 | **V40 · el kardex de bultos restaba el consumo dos veces** (medición) | `a2ec07c` | la cifra por lote padre, con su query |
| 19ago26 | **V41 · arreglado el doble conteo, en las DOS ramas** | `473ac16` | `retiros` es de la GRANJA y `consumo` del LOTE: granos distintos |
| 19ago26 | **V42 · auditoría de los 45 pendientes** | `473ac16` | 15 muertos con su evidencia — **aplicados en esta depuración** |
| 19ago26 | **V43 · el arrastre semanal del kardex de bultos** | `d3a91dd` | el resumen dejó de contradecir a su propio detalle |
| 19ago26 | **V44 · `AcumularSaldos` recortaba a 0** | `26007db` | contrato incumplido; radio de impacto medido: cero |
| 19ago26 | **V45 · el escritor de inventario del front** | `d0d9334` | dead code que era un doble descuento, con gate para que no vuelva |
| 19ago26 | **V46 · `TouchUserUpdatedAt` eliminado** | `f33c700` | la decisión estaba tomada y el commit se había perdido |
| 19ago26 | **V47 · correcciones a V45** | `fb314ae` | salidas de la verificación adversarial |
| 19ago26 | **V48 · los 3 arreglos contra la COPIA DE PRODUCCIÓN** | `e4475fc` | **13 de 13**, y nada se movió de lo que no debía |
| 19ago26 | **X1 · Núcleo 4 de NIZA III + id de galpón que choca** | `434293f` `d57329c` `e971871` | `galpon_id` es PK global; los 3 galpones llegan por migración |
| 20ago26 | **V49 · el descuadre de Panamá: eran 8, no 23** | `8c141e6` | y **se hereda entre ciclos**; lo que queda → **B2 / B3** |
| 20ago26 | **V50 · el `.sql` es el espejo, la migración el vehículo** | `ccd0941` | 4.º gate del CI: nada aplica `backend/sql/` solo |

---

## Muertos — no volver a abrirlos

Verificados uno a uno en la auditoría **V42** (19-ago) y bajados al archivo hoy. Si alguno reaparece
en un plan viejo, esta tabla es la respuesta.

| Pendiente | Veredicto | Evidencia |
|---|---|---|
| Merge `main`→`main-produccion` para desplegar **la PWA** | **YA_RESUELTO** | PR #74, run `32178414139` success, `/version.json` 18-ago 19:54Z, `ngsw.json` 200 |
| Verificación post-deploy de ese merge | **YA_RESUELTO** | TaskDef `sanmarino-back-task:160` ↔ imagen `79aeccfa…` ↔ `rolloutState COMPLETED` |
| Invariante de `company_permissions` antes/después | **OBSOLETO** | pedía una foto «antes» del 18-ago que ya no es tomable |
| Avisar del menú «Lote Reproductora» | **OBSOLETO** | la migración entró el 12-ago (`6980fa3`); el aviso llegaría 7 días tarde |
| Menú «Lote Reproductora» a 3 roles | **YA_RESUELTO** | 0 filas en `role_menus` y en `company_menus`; su etiqueta real era otra |
| Lote 132 (19.387 vs 19.187) | **YA_RESUELTO** | BD = 19.187 · `fn_cuadre_aves_engorde(NULL)` → 0 y 0 |
| Limpiar los 15 días traslapados de **K345** | **YA_RESUELTO** | ejecutado en V25.8.3 (`6ce89cc`), ya desplegado con el PR #74 |
| ¿Sale el tile «Venta Engorde»? | **YA_RESUELTO** | decidido en V25.7.1: **se queda** |
| Santa Reyes pierde Migraciones Masivas | **YA_RESUELTO** | decidido y ya aplicado en datos |
| V25.8.7 «falta desplegar» las 2 migraciones | **YA_RESUELTO** | entraron con el PR #74 |
| Cerrar el grupo A (39 lotes de Ecuador) | **OBSOLETO** | la lista de 39 no existe; lo que queda es más chico → **B6** |
| Corte levante/producción 24 vs 25 semanas (`A3`, 2ª pasada 20-ago) | **PROBABLEMENTE YA_RESUELTO** | El caso que motivó V25.6.2 (S-369, ~17.332 kg) se cerró como ticket `TK-2026-000020` el 14-ago: **no era bug de código**, faltaban 7 días de datos en la carga masiva. Y el corte de 25 semanas (175 días) ya está en el código **desde el 17-jul** (`957330f`, commit REQ-012b) — un mes ANTES de que V25.6.2 se escribiera —, tanto en `ObtenerLotesProduccionAsync` como en `LiquidacionCierreLoteLevanteService`. Grep sobre `backend/sql/fn_*` y `vw_*`: **no hay una tercera constante de 24 semanas** escondida en reportes. Lo único real: el cierre de un lote («Cerrar lote») **no tiene gate por semana** — es 100 % manual, cualquier operario cierra a la semana que quiera. Si la intención de V25.6.2 era ESO (forzar el cierre a la semana 25), es una decisión de producto nueva, no «falta implementar un corte que ya existe» — confirmar con costos antes de escribir código |
| C8 · smoke de la columna «Próx. ciclo» | **YA_RESUELTO, verificado hoy** | Local: back en worktree aislado (`dotnet build` 0 errores) + front Angular en :4200, sesión inyectada, empresa real (Agroavicola Sanmarino). Tab Histórico con 100 filas reales: header «Próx. ciclo» presente (índice 12), `overflow-x:auto` con `scrollWidth 1704 > clientWidth 1175` — desborda **dentro** de su contenedor, no rompe la página. 0 marcas en BD ⇒ todas «—», como predijo el bloque. Sin escritura permanente: un toggle de prueba (`id=5705`) se hizo y se revirtió en la misma sesión |
| V20.4.1 decisión sobre el lote 12 | **YA_RESUELTO como decisión** | duplicado de V25.5.4 → **B1** |
| «Persistir la atribución como hecho» | **DUPLICADO** | de V27.1 → **B3** |
| V19.2.1 opción (a) vs (b) del kardex | **OBSOLETO** | V40.8 midió que (a) empeora; lo cerró **V41** |
| V8.6 · simular la corrección + gate de paridad | **ABSORBIDO** | es el paso de ejecución de **B2**, no un pendiente aparte |
| S-1 a S-4 «bloqueados por el deploy» | **DESBLOQUEADOS** | la PWA está en prod: los 4 smokes ya son ejecutables → **C10-C13** |

---

## V51 — Permiso de fecha retroactiva + ventana base de 15 días (20-ago-2026)

**Plan:** [`fase_de_desarrollo/permiso_fecha_retroactiva_registros_plan.md`](fase_de_desarrollo/permiso_fecha_retroactiva_registros_plan.md)

Pedido: permiso que habilite fechar hacia atrás en los registros manuales, y ampliar la ventana base
del "solo mes en curso" a `MIN(1 del mes, hoy − 15)` (el día 1 no dejaba cargar lo del día anterior).
Alcance: movimientos/traslados de aves, pollo engorde, huevos, alimento + gestión de inventario y
gastos. Fuera (por instrucción explícita): tickets/ItalJira, Implementación, Vacunación; y por
naturaleza del dato: filtros de reporte y fechas que van al futuro (`fecha_vencimiento`, lotes
programados).

- [x] `VentanaFechaRegistroCalculos` (nuevo, Application/Calculos): ventana base + permiso
      `registros.fecha_retroactiva`; `VentanaFechaMovimientoInventarioCalculos` delega en él y
      conserva la excepción D4 (alimento previo al encaset) encima — una sola fórmula por número
- [x] `VentanaFechaRegistroGuard` (extensión de `ControllerBase`) + **17 puertas** guardadas en 7
      controllers (creación y edición, no solo alta) — `POST /InventarioGestion/consumo` y los
      endpoints de venta/traslado disparados desde seguimiento diario quedaron fuera con motivo
      documentado en el plan
- [x] Migración `SeedPermisoFechaRetroactivaRegistros` (data-only, Designer clonado con diff de 4
      líneas): permiso + `company_permissions` en las 5 empresas + rol Admin. **Verificado contra la
      BD local real**: aplica sin SIGSEGV, idempotente (`INSERT 0 0` al re-correr el SQL a mano),
      Swagger 200 con el DTO `Min` ahora nullable
- [x] Frontend: `shared/utils/fecha/ventana-fecha-registro.funcion.ts` (canónica) + 11 formularios
      (`[attr.min]`/`[max]`, no `[min]`: con permiso el atributo desaparece) + de paso, 7 manejadores
      de error que descartaban el mensaje real del backend (`error.message` en vez de
      `error.error?.message`) quedaron corregidos — sin eso el 400 del permiso nunca le habría
      llegado al usuario
- [x] `dotnet build` 0 errores (20 warnings preexistentes, ninguno nuevo) · `dotnet test` **2936/2936**
      (incluye 76 tests de la ventana, 3 archivos viejos actualizados a la regla ampliada + equivalencia
      a mitad de mes) · `yarn build` 0 errores, 0 warnings · gate `verificar-sql-llega-por-migracion.js`
      en verde
- [i] **El bloque de este pendiente se perdió una vez**: escrito con `cat >> tracker_estado.md` sin
      commitear, la limpieza de esta misma tarde (`13a969a`/`7a8d1fe`) lo pisó al reescribir el
      archivo. Recuperado y committeado de inmediato — la lección de
      `como-trabaja-el-usuario.md` era literal.

---

## V52 — Santa Reyes: ejecución H1-H4 (F0-F12), 20-ago-2026

**Desbloqueada esta sesión** (B5 y C7 confirmadas por el usuario). Plan técnico:
[`fase_de_desarrollo/santa_reyes_requerimientos_italapp_plan.md`](fase_de_desarrollo/santa_reyes_requerimientos_italapp_plan.md).
Checklist calcado **1:1** del desglose ya sembrado en ItalJira (caso `TK-2026-000172`, historia
`HIS-2026-0024`) para que el avance real y lo que ve el cliente sean la misma lista — cada ítem trae
su código de tarea/subtarea. Fuente técnica de qué ya existe vs qué falta: §2 del plan (auditoría
18-ago, releída y confirmada vigente el 20-ago vía `git log` — nada de esto se tocó entretanto).

⚠️ **No expone al cliente** que buena parte de la base ya existía antes del plan comercial (silos,
clasificación de huevo, guía genética, `Placa/Conductor/Sellos`) — así lo decidió el usuario en V30.4.

- [x] **F0 · Parametrización por empresa** (6h)
  - [x] F0.1 Banderas de comportamiento de Santa Reyes en la ficha de empresa (BD, servicio, pantalla admin)
        — 3 flags nuevos (`ConsumoAlimentoSoloHembras`, `OcultaMachosEnPostura` bool;
        `HuevoPrimeraPosturaHastaSemana` int? — no es booleano, va como campo numérico propio igual
        que `diasAlimentoPrevioEncaset`, no entra en `flags-empresa.funcion.ts`). Wireado en las 8
        capas backend (entidad, 3 DTOs, EF config, `CompanyService` x2, `CompanyResolver` x2,
        `CompanyPaisService`) + admin UI (catálogo de 2 flags + input numérico, contador
        `totalFlags` se ajusta solo). Migración `20260820083544_AddFlagsSantaReyesCicloVidaYHuevo`
        idempotente (`ADD/DROP COLUMN IF EXISTS`), aplicada y re-corrida a mano sin error. `dotnet
        build` 0 errores · `dotnet test` **2936/2936** (sin regresión, ningún cálculo nuevo todavía) ·
        `yarn build` 0 errores. **Los 3 flags nacen en `false`/`null` en TODAS las empresas
        (incluida Santa Reyes) a propósito**: se encienden recién en el commit que construya F4/F5/F7
        (la lógica que los consume), mismo patrón que el resto de los flags del repo — prender un
        flag sin nada que lo lea generaría un toggle visible sin efecto
        - Verificación: build+test verdes de punta a punta + cruce manual carácter-por-carácter de
          los 5 `formControlName="huevoPrimeraPosturaHastaSemana"` (HTML↔TS) — Angular NO valida esa
          coincidencia en compilación, un typo ahí solo revienta en runtime. **No pude hacer el smoke
          visual en navegador**: requería mintear una sesión de `moiesbbuga@gmail.com` (JWT a mano o
          hash de contraseña) y el clasificador de seguridad de Auto Mode lo bloqueó dos veces —
          correctamente, es fabricar credenciales de un usuario real aunque sea en BD local. Queda
          pendiente que alguien lo abra una vez en pantalla (Configuración → Empresas, grupo
          "Postura") para el visto bueno final
  - [x] F0.2 Catálogo de ítems de huevo y alimento de Santa Reyes (creación y carga inicial) — **YA
        EXISTÍA, verificado en BD 20-ago**: 45 ítems alimento (`item_inventario_ecuador`) + **21 ítems
        huevo en `catalogo_items`** (`item_type='huevo'`) cubriendo los 7 tipos Primera del plan
        (Rojo/Blanco/Criollo/Gallina Feliz/Bonegg/Azur/Libre de Jaula Certificado) + variantes PNC
        (Manchado/Picado/Fárfara/Decolorado) + variantes "primeras posturas sin clas" por raza
        (Rojo/Blanco/Criollo). ⚠️ Hallazgo para F8.1: **falta "Enyemado"** — ninguna raza lo tiene hoy
        en el catálogo, y el plan lo pide junto a Manchado/Decolorado/Picado/Fárfara
- [x] **F1 · Estructura física de granja y códigos ERP** (10h) — **verificado 20-ago: YA ESTABA HECHO**
      de fases anteriores (Fase 1 de `santa-reyes-implementacion` + Fases B-D de silos), sin escribir
      código nuevo
  - [x] F1.1 Silo como estructura física de la granja: alta, edición, listado, asociación a galpón y lote
        — botón "Silos y bodega" en `farm-list` (gateado por `manejaInventarioPorSilo`) abre
        `modal-silos-granja`: marca silos del catálogo maestro, crea la bodega de la granja, y por
        cada ubicación asignada tiene su propio sub-modal de edición ERP (`abrirErp`/`guardarErp`).
        La lectura literal del audit 18-ago ("falta exponerlo en el form de ingreso a granja") es una
        imprecisión: una granja sin `id` no puede tener silos asignados todavía — configurarlos
        DESPUÉS de crear la granja, desde su fila en la lista, es el flujo correcto, no un gap
  - [x] F1.2 Códigos ERP por nivel: granja=CO, núcleo=bodega, silo/bodega=ubicación, lote=centro de costo
        — los 4 niveles YA tienen campo + input editable: `Farm.CodigoBodega/CentroOperacion/
        CodigoInstalacion` (`farm-list`, gateado por `manejaCodigosErp`), `Nucleo.CodigoBodega`
        (`nucleo-list`), `FarmSilo.CodigoErpUbicacion/CentroOperacion/CodigoBodega` (sub-modal ERP de
        `modal-silos-granja`, DTOs ya los aceptan), `LotePosturaBase.CodigoErp`
        (`lote-list`, `formControlName="codigoErp"`). Nada que construir; falta cargar los códigos
        REALES que entregó el cliente (dato, no código)
- [~] **F2 · Guías genéticas** (10h)
  - [x] F2.1 Carga de las 5 líneas (Babcock Brown, Hy Line Brown, Lohmann LSL, Criolla, Azur), sem 18-125
        — **corrección del usuario en sesión: va en una TABLA PROPIA**, no en
        `guia_genetica_sanmarino_colombia` (esa es compartida con postura y pollo engorde de otras
        empresas, ~50 columnas para casos que Santa Reyes no usa). Se creó `guia_genetica_santa_reyes`
        (entidad `GuiaGeneticaSantaReyes`, migración `AddTablaGuiaGeneticaSantaReyes`, esquema
        idempotente) + seed de datos (`SeedGuiaGeneticaSantaReyes`, data-only, idempotente por
        `(company_id, codigo_guia_genetica)`, fail-open si la empresa no existe). **615 filas, no 540**:
        recontado contra el Excel real del cliente (`~/Downloads/Guías Genéticas.xlsx`), son **5 líneas
        × 123 semanas (18-140)**, no 108 — la cifra del plan comercial estaba mal, corregida acá.
        Mapeo: SEM→edad, % PROD. TAB→prod_porcentaje, % Mort Acum.→retiro_ac_h (acumulada),
        Gramo/Ave/Día→gr_ave_dia_h, redondeado a 2 decimales (el Excel trae artefactos de coma
        flotante de 15+ dígitos). Criolla no trae producción desde semana 101 (40 filas con
        `prod_porcentaje NULL`, retiro/consumo sí poblados) — dato real del cliente, no un bug.
        Verificado: 615/615 filas, round-trip Down→Up sin duplicar, `codigo_guia_genetica` calculado
        igual que `ExcelImportService.ComputeCodigo` (Raza+AnioGuia+Edad) para que una futura
        reimportación por Excel lo reconozca
  - [~] F2.2 Asociación de la línea genética al lote + uso en indicadores y reportes
        — **mayormente hecho**, un chokepoint + 6 sitios de consumo:
        - `GuiaGeneticaService` (backend de `api/guia-genetica`, el que alimenta el selector de raza
          en `modal-create-edit-lote`/`lote-list`): ahora mira primero `guia_genetica_santa_reyes` y
          cae a la compartida si la empresa no tiene filas propias — cubre los 6 métodos públicos
          (`ObtenerGuiaGeneticaAsync`, rango, existe, razas, años, producción)
        - `LoteService` (Create/Update): el gate "raza/año obligatorios si la empresa tiene guía"
          ahora mira las dos tablas vía `GuiaGeneticaLookup` (nuevo, compartido) — antes Santa Reyes
          iba a quedar SIEMPRE en modo "sin guía" (raza libre) aunque F2.1 ya hubiera cargado la suya
        - `LiquidacionCierreLoteLevanteService`, `LiquidacionTecnicaComparacionService`,
          `ReporteTecnicoSemanal` (los 3 usan `GuiaGeneticaLookup.ObtenerFilasCompatiblesAsync`, que
          arma filas `ProduccionAvicolaRaw` transitorias — no persistidas — con los 3 campos que la
          tabla de Santa Reyes sí tiene; `peso_h`/`uniformidad`/`cons_ac_h` quedan `null`, igual que
          ya pasa hoy con cualquier fila incompleta de la guía compartida)
        - `LiquidacionTecnicaService`/`LiquidacionTecnicaComparacionService` (la parte reproductora)
          **no se tocaron a propósito**: `LiquidacionTecnicaService` lee
          `seguimiento_diario_levante_reproductoras` — es de REPRODUCTORA, Santa Reyes no cría
          reproductoras (compra pollita de un día), no aplica
        - ⚠️ **Gap conocido, no cerrado**: `ReporteTecnicoProduccionService` (3 sitios) y
          `ReporteTecnicoService` (2 sitios) tienen consultas DIRECTAS a `ProduccionAvicolaRaw`
          además de las que ya pasan por `IGuiaGeneticaService` — son archivos de 1000-2700 líneas
          y al menos una de esas consultas (`ReporteTecnicoProduccionService.cs:~1107`) **no filtra
          por `company_id`**, así que no es seguro tocarla sin antes entender si es a propósito o un
          bug preexistente. Quedó sin tocar para no meter una regresión en un reporte financiero bajo
          presión de tiempo — a retomar con más cuidado
        - Validado: `dotnet build` 0 errores (21 warnings preexistentes) · `dotnet test` **2936/2936**
          sin regresión
- [x] **F3 · Semanas de producción por raza** (10h) — commit `6df9a98`
  - [x] F3.1 Levante por raza: 8 sem alistamiento + 16 sem levante
  - [x] F3.2 Producción: 4 sem levante-en-granja-de-producción + 74 sem postura (rojas/criollas) u 84 (blancas/Azur)
        — **auditado el `.docx` fuente** (`~/Downloads/Requerimientos de Italapp.docx`, sección
        "Consumo de alimento"), no solo la fila resumida del plan §2: el caso de prueba original
        quedaba ambiguo (¿edad global del ave o semana relativa a producción?). El texto confirma
        "desde la creación del Item" ⇒ edad global, el mismo contador que ya usa toda la app
        (`FaseLoteCalculos`, la guía genética por `Edad`). Diseño completo en §6 del plan
        (`fase_de_desarrollo/santa_reyes_requerimientos_italapp_plan.md`).
        - Flag nuevo `Company.SemanasCicloPosturaPorRaza` (8 capas, mismo patrón que
          `ConsumoAlimentoSoloHembras` de F0.1), migración idempotente
          `20260820102832_AddFlagSemanasCicloPosturaPorRaza`, ON solo en Santa Reyes (misma migración
          que trae el cálculo que lo consume, no queda un toggle sin efecto).
        - `SemanasCicloPosturaCalculos` (backend, puro) + espejo TS
          `shared/utils/fecha/semanas-ciclo-postura.funcion.ts`: alistamiento sem 1-8, levante 9-24
          (igual en los dos grupos de raza), levante en producción 25-28, postura 29-102
          (rojas/criollas) o 29-112 (blancas/Azur). Raza no reconocida ⇒ `null`, no se adivina el
          grupo — el caller muestra «—» o cae al comportamiento de siempre.
        - **Dos conceptos "etapa" distintos, auditados para no confundirlos**: `FaseLoteCalculos`
          (backend, umbral de 26 semanas) solo clasifica la `Fase` Levante/Producción al
          crear/editar un lote y filtra reportes — el paso real de módulo es manual, así que **no se
          tocó** (fuera del alcance literal del requerimiento). El campo `Etapa` 1/2/3 del modal de
          producción (`calcularEtapa`/`getEtapaLabel`, dato informativo exportado, ningún saldo lo
          consume aritméticamente) sí es el mismo tipo de dato que pide el cliente — ahí se conectó
          el cálculo por raza.
        - Modal de producción: nuevo `@Input() raza` (`selectedLote.raza`, ya viajaba un nivel
          arriba, solo faltaba pasarlo); con flag ON y raza reconocida muestra "Levante en
          producción"/"Postura"/"Fuera de ciclo" en vez de "Etapa 1/2/3"; con flag OFF o raza no
          reconocida, byte a byte igual que siempre.
        - Modal de levante: campo nuevo de solo lectura con la etapa — **el form real es
          `modal-create-edit` (el que usan `tabs-principal`/`seguimiento-lote-levante-list`), NO
          `seguimiento-lote-form`**, que resultó huérfano (solo enrutado en `/nuevo` y `/editar/:id`,
          sin campo de consumo de machos ni flags de empresa) — mismo gotcha que ya advertía
          CLAUDE.md para `lote-list` vs `modal-create-edit-lote`. Reusa `semanaVidaLevante` (la
          fórmula ya documentada como canónica) en vez de sumar una tercera variante de cálculo de
          semana al repo.
        - Validado: `dotnet build` 0 errores (21 warnings preexistentes) · `dotnet test`
          **2959/2959** (23 nuevos, sin regresión) · `dotnet ef database update` aplicado en local
          sin error · `yarn build` 0 errores. Sin smoke visual en navegador (mismo bloqueo del
          clasificador de seguridad que F0.1 — minteo de sesión).
- [x] **F4 · Consumo de alimento solo hembras** (8h) — commit `107cf3c`
  - [x] F4.1 Retirar consumo de machos del seguimiento diario de producción — `modal-seguimiento-diario`
        (lote-produccion): bloque «Machos» (ítems dinámicos, sin botón «+ agregar» visible — código
        ya huérfano de UI) envuelto en `@if (!consumoAlimentoSoloHembras)`. Flag propagado a
        `ActiveCompanyConfigService` (faltaba: F0.1 solo lo había llevado a la pantalla de admin de
        empresas, no al servicio de flags en runtime que consumen los formularios — gap real,
        corregido acá)
  - [x] F4.2 Retirar consumo de machos del seguimiento diario de levante — `modal-create-edit`
        (lote-levante, el form real, no `seguimiento-lote-form` que es huérfano): bloque «🐓 Machos»
        (con su «+ Agregar alimento (machos)») envuelto igual. El array `itemsMachos` ya nacía vacío
        acá (a diferencia de producción, no tiene ítem fijo obligatorio) — solo hacía falta ocultar
        la UI y vaciar el array si el flag llega ON con un registro viejo ya hidratado
        - Validado: `yarn build` 0 errores
- [~] **F5 · Mortalidad, pesaje y ventas** (9h) — F5.1+F5.2 hechos (commit `b93a053`), F5.3 documentado sin implementar
  - [x] F5.1 Retirar el concepto de error de sexaje del registro diario — fila «Error de sexaje»
        (Hembras y Machos) retirada ENTERA de `modal-seguimiento-diario` (producción) y
        `modal-create-edit` (levante), gateada por `ocultaMachosEnPostura` (F0.1, propagado recién
        acá a `ActiveCompanyConfigService` — mismo gap que `consumoAlimentoSoloHembras` en F4)
  - [x] F5.2 Ocultar columna de machos en mortalidad, selección, peso y uniformidad — columna
        Machos (+ CV, misma tabla) oculta en los mismos 2 formularios; grid CSS con modificador
        `.compare-grid--sin-machos` (2 columnas en vez de 3, ajusta bordes `nth-child`) en los 2
        `.scss`. `mortalidadM`/`selM`/`errorSexajeMachos` tienen `Validators.required` pero
        arrancan en `0` (valor válido, no "vacío" para Angular) — ocultar el input no bloquea el
        guardado, confirmado por lectura de código, no hacía falta tocar validadores
  - [!] F5.3 Campo machos sobre el total de aves en el registro de ventas — **sin implementar**,
        requiere una decisión de UX que no voy a adivinar. Texto LITERAL del cliente (auditado
        `~/Downloads/Requerimientos de Italapp.docx`, no solo la fila resumida del plan):
        *"Desaparece el concepto de error de sexaje, y que en ventas aparezca campo machos sobre
        el total de las aves"*. Vive en `movimientos-aves` (modal-movimiento-aves, tipo Venta),
        un módulo COMPARTIDO con traslados y con historial de bugs de doble conteo
        ([[aves-disponibles-venta-doble-descuento]]) — hoy tiene `cantidadHembras`/`cantidadMachos`
        como campos independientes con su propio chequeo de disponibilidad; no está claro si
        "machos sobre el total" pide (a) un campo Machos de solo-informe junto a un campo Total
        único, o (b) otra cosa. Alto riesgo de regresión en un módulo multi-empresa si se adivina
        mal — a definir con el cliente o el usuario antes de tocar código
        - Validado (F5.1+F5.2): `yarn build` 0 errores; cruce manual del nombre de la propiedad
          `ocultaMachosEnPostura` entre `.ts`/`.html` en los 2 formularios (11 usos cada uno)
  - [x] **Gap cerrado (20-ago-2026, migración `20260820220645`):** F4 y F5.1+F5.2 construyeron y
        probaron la UI pero **ningún commit encendió los flags para Santa Reyes** — mismo "toggle
        sin efecto" que F0.1 advertía evitar. Verificado por consulta directa a `companies` en la
        BD local: `consumo_alimento_solo_hembras`/`oculta_machos_en_postura` seguían en `false`.
        Migración data-only idempotente los enciende para Santa Reyes; re-verificado por consulta
        directa, ahora `true` en las dos. Sanmarino/Demo siguen en `false`
- [x] **F6 · Tipos de inventario** (3h) — commit pendiente de crear en esta sesión
  - [x] F6.1 Limitar tipos de ítem de inventario a Alimento y Aves — flag nuevo
        `limitaTiposInventarioAlimentoYAves` (8 capas backend + `flags-empresa.funcion.ts` +
        `active-company-config.service.ts`). Módulo real: `CatalogoAlimentosListComponent`
        (`config/catalogo-alimentos`, con su modal de alta/edición embebido) — se agregó `'aves'`
        a `CatalogItemType` y `tiposItem` pasó de array fijo a getter (referencia estable, no
        rompe cambio de detección) que devuelve `['alimento','aves']` con el flag ON o los 6 tipos
        de siempre con el flag OFF. `CatalogoAlimentosFormComponent` (rutas `nuevo`/`editar/:id`
        del mismo módulo) es **huérfano** — verificado sin `routerLink` ni `.navigate` hacia esas
        rutas en todo el repo — no se tocó. Backend no valida `ItemType` contra lista cerrada: sin
        cambios ahí, es acotar la UI, no agregar una regla nueva. Sin ítems `itemType='aves'`
        existentes en ninguna empresa, nada que reclasificar
        - Validado: `dotnet build` 0 errores (20-21 warnings preexistentes) · `dotnet test`
          **2959/2959** · `yarn build` 0 errores · migración aplicada y verificada en BD local
          (Santa Reyes `true`, Sanmarino/Demo `false`)
- [~] **F7 · Huevo sin clasificar y primera postura** (17h) — diseño técnico en §8 del plan
      (20-ago-2026, sesión de continuación). **Hallazgo central: la mecánica ya existía** — con
      `clasificacionHuevoPorItems` (Santa Reyes, encendido desde F0.2) el bloque entero de
      "Huevos Incubables" queda oculto (`@if (!clasificacionHuevoPorItems)`) y se reemplaza por el
      selector de ítems del catálogo; la palabra "Incubables" no aparece en el flujo de Santa Reyes
      hoy. El gap real era más chico: nombres, vigencia y 2 campos sin gatear (ver F7.1/F7.4 y F8.2)
  - [x] F7.1 Renombrar huevos incubables → huevos sin clasificar — reinterpretado tras auditar
        `modal-seguimiento-diario`: el rename que faltaba era el de los ÍTEMS del catálogo, no un
        label de formulario (ese ya estaba oculto). 6 de los 7 ítems "Primera" de Santa Reyes
        (Rojo/Blanco/Criollo/Gallina Feliz/Bonegg/Libre de Jaula Certificado) se llamaban sin el
        prefijo "SIN CLASIFICAR" — solo Azur ya lo traía (prueba de que el patrón correcto ya
        existía). Migración data-only pendiente de aplicar (ver nota de migración abajo) renombra
        los 6 a `HUEVO SIN CLASIFICAR <RAZA>`
  - [x] F7.2 Los 7 ítems: rojo, blanco, criollo, gallina feliz, Azur, Boneg, libre de jaula — **ya
        existían los 7** en `catalogo_items` (verificado F0.2, re-confirmado acá), no había nada que
        crear
  - [ ] F7.3 Huevo de primera postura: selección de raza + definición al crear el lote — **sin
        construir, ambigüedad real** (ver §8.3 del plan): el texto ("especificar los huevos que va a
        producir" al crear el lote) no deja claro si pide una UI nueva en el alta de lote o si la
        clasificación por ítems que ya existe en el seguimiento diario alcanza. No se adivina
  - [x] F7.4 Vigencia: habilitada hasta el último día de semana 22, deshabilitada desde el primer día
        de semana 23 — `HuevoPrimeraPosturaCalculos.EsVigente` (backend, con tests xUnit) + espejo
        `esVigentePrimeraPostura` (`items-huevo-catalogo.funcion.ts`); ítems marcados
        `metadata.primeraPostura=true` (los 3 que existen: Rojo/Blanco/Criollo) se deshabilitan en el
        `<select>` fuera de vigencia. `Company.HuevoPrimeraPosturaHastaSemana` existía desde F0.1 sin
        un solo consumidor (grep confirmó 0 usos) — este commit lo cablea y lo pone en 22 para Santa
        Reyes vía migración. **Alcance deliberado: solo UI** (no rechaza en el guardado) — mismo
        criterio "solo UI" que el resto de la familia de flags; extender a validación de guardado
        queda documentado en §8.2 del plan para cuando se confirme que hace falta
- [ ] **F8 · Productos no conformes y panel de eficiencia** (7h)
  - [ ] F8.1 Renombrar PNC: Manchado, Decolorado, Enyemado, Picado, Fárfara — sin construir. Catálogo
        actual (11 ítems `Pnc`) no cubre las 5 categorías por raza: falta "Enyemado" completo (0
        ítems, hallazgo ya conocido desde F0.2) y "Decolorado" solo existe para Rojo. No se inventan
        cantidades/nombres sin confirmar con el cliente
  - [x] F8.2 Retirar huevo tratado, peso promedio y tipo de alimento del registro de producción —
        `huevoTratado` ya estaba oculto (vive dentro del bloque `!clasificacionHuevoPorItems`);
        `pesoHuevo`/`tipoAlimento` **no tenían ningún gate** (gap real, encontrado auditando el
        template junto con F7) — envueltos en `@if (!clasificacionHuevoPorItems)` acá. Los controles
        conservan su valor por defecto (`0`/`'Standard'`), siguen siendo válidos para
        `Validators.required` y se guardan igual — cambio de UI, no de contrato
  - [ ] F8.3 Panel de eficiencia con la nueva nomenclatura + cuadre suma huevos = total granja — sin
        construir. El texto fuente (párrafo 68 del .docx) es contradictorio con F7.1 tal como está
        escrito y no hay pantalla "Panel de eficiencia" en el repo hoy — ver §8.3 del plan. A
        confirmar con el cliente si es pantalla nueva o ajuste de nomenclatura sobre un reporte
        existente antes de tocar nada (son reportes financieros)
- [~] **F9 · Traslado de aves** (5h) — captura + listado hechos (20-ago-2026), comprobante sin tocar
  - [x] F9.1 Ocultar machos en el traslado de aves — el traslado real de postura NO es
        `traslado-form`/`trasladoRapido` (roto, ver hallazgo abajo): es
        `modal-traslado-aves-seguimiento` → `TrasladosAvesService.ejecutarTrasladoDesdeSegDiario` →
        `POST api/Traslados/aves-desde-seguimiento`. Campo Machos envuelto en
        `@if (!ocultaMachosEnPostura)`, gateado igual que F4/F5
  - [x] F9.2 Campos de transporte: placa, precinto, conductor — **capturados**, agregados a
        `TrasladoAvesDesdeSegDiarioDto` (front+back), y a la construcción del `MovimientoAves` en
        `TrasladoAvesDesdeSegService.Traslado.cs` (la entidad ya tenía `Placa`/`Conductor`/`Sellos`
        desde antes).
  - [x] F9.2b **Reflejo en el listado (20-ago-2026, sesión de continuación).** `movimientosAves`/
        `TrasladoUnificadoDto` (`TrasladoNavigationController.GetByLote` → `MovimientoAvesCompletoDto`,
        que ya construye `MovimientoAvesService.Mapeo.cs` desde la entidad) no traían Placa/
        Conductor/Sellos — se agregaron los 3 campos como parámetros **opcionales al final** de los 2
        records (no rompe ningún otro caller posicional) y se propagan en la única fábrica de cada
        uno. Frontend: `TrasladoUnificado` (interfaz) + columna nueva "Transporte" en la tabla de
        Aves de `movimientos-list.component` — **una sola columna compacta** ("Placa: … · Cond.: … ·
        Precinto: …", solo los valores presentes), mismo patrón ya usado en esa tabla para
        Cantidad (H:/M:) y en la de Huevos para Detalle (L:/T:/S:) — se descartó agregar 3 columnas
        sueltas a una tabla que ya tenía 8 (quedaría muy ancha) y una fila expandible (más estado,
        sin necesidad real todavía)
  - [ ] F9.2c **Comprobante — sin construir.** No existe una pantalla/printable de comprobante de
        traslado hoy (ninguna ruta ni componente con ese nombre); no está claro si el pedido es un
        PDF descargable, una vista de detalle imprimible, o el mismo listado alcanza. A definir antes
        de construir algo — mismo criterio que F5.3/F7.3/F8.3 (no adivinar UX)
  - [i] **Hallazgo de paso, fuera de Santa Reyes**: `POST api/MovimientoAves/traslado-rapido`
        (usado por `traslado-form.component`, ruta `/traslados-aves/traslados`) tiene el DTO del
        frontend completamente desalineado del que espera el backend (`loteOrigenId`/`loteDestinoId`
        vs. `LoteId`+`GranjaOrigenId`/`GranjaDestinoId` — ni los nombres ni el modelo de datos
        coinciden). `int.Parse(request.LoteId)` con `LoteId` sin bindear debería tirar excepción en
        cualquier uso real — la pantalla probablemente nunca completa un traslado hoy. **No es un bug
        de Santa Reyes**, afecta a cualquier empresa que use esa pantalla. Flagueado aparte
        (`task_88856448`), no tocado acá
  - [i] **Nota metodológica**: esta fue la tarea más cara de la sesión en tiempo — 3 intentos fallidos
        de envolver el campo Machos en `@if` antes de dar con el error real. La causa: el `</div>`
        que cierra el campo Machos está PEGADO al `</div>` que cierra el `cantidades-grid` que lo
        contiene (misma indentación, visualmente indistinguibles), y las primeras 2 veces incluí el
        segundo por error dentro de mi nuevo `@if`, cerrando el div padre ANTES de que su propio
        `@if` cerrara — Angular exige que todo lo abierto dentro de un `@if`/`@for` cierre DENTRO del
        mismo bloque. `ng build` (NG5002) es la única fuente de verdad confiable acá — el conteo
        manual de llaves/divs por lectura visual falló 3 veces seguidas en un archivo con muchos
        bloques `@if (mismaCondición)` hermanos y anidados mezclados. La próxima vez: cambio mínimo,
        `yarn build` después de CADA paso, no acumular varios cambios de estructura antes de compilar
- [~] **F10 · Traslado de huevos** (5h) — bug real encontrado y cerrado (21-ago-2026, sesión de
      continuación) en los 2 formularios oficiales + el listado, F10.1 (UX de bodega de salida) sigue
      sin resolver. Diseño técnico completo en §9 del plan (`santa_reyes_requerimientos_italapp_plan.md`)
  - [!] **4º lugar con el mismo bug, encontrado pero NO tocado**: `traslados-aves/pages/inventario-
        dashboard` (~1800 líneas, pantalla de aterrizaje real de `/traslados-aves`) tiene su propia
        reimplementación del formulario de traslado de huevos (sin selector de ítems) — mismo síntoma
        de disponible 0 para Santa Reyes. Componente grande, sin auditar a fondo; spawneado aparte
        (`task_b8e26e02`) en vez de arriesgar un edit grande sin dominarlo. Detalle en §9.5 del plan
  - [x] **Bug encontrado auditando F10.1, no era la pregunta de UX que parecía**: la disponibilidad
        de huevos para traslado/venta se calculaba SOLO desde las 11 columnas legacy
        (`espejo_huevo_produccion.huevo_*_dinamico`), que F0.2/F7 dejan en `0` para Santa Reyes
        (clasificación por ítems). En cuanto Santa Reyes cargara producción real, **no iba a poder
        trasladar ni vender un solo huevo** — disponible `0` en las 11 categorías aunque el total
        fuera correcto — y ni siquiera había una validación real de disponibilidad para ese caso
        (el chequeo contra las 11 en `0` pasaba trivialmente). Cerrado de raíz:
        - `TrasladoHuevos.TotalHuevos` pasó de propiedad calculada (nunca mapeada por EF) a columna
          real (`ADD COLUMN IF NOT EXISTS total_huevos`, migración
          `20260821030415_SantaReyesF10TrasladoHuevosPorItems`, con backfill de las filas
          existentes) + `Metadata` (jsonb) nueva para el desglose por ítems
        - `HuevoItemsCalculos.CalcularDisponibilidad` (producido − transferido por `catalogItemId`,
          nunca negativo) — cálculo puro nuevo, 6 tests xUnit
        - `DisponibilidadLoteService`: `ObtenerDisponibilidadHuevoItemsLPPAsync` +
          `ValidarDisponibilidadHuevoItemsLPPAsync` (lee `SeguimientoProduccion.Metadata` producido
          y `TrasladoHuevos.Metadata` de traslados `Completado` transferido, en memoria — mismo
          estilo que el resto del archivo)
        - `EspejoHuevoProduccionSyncService`: `movTot` pasó de sumar las 11 columnas de
          `TrasladoHuevos` a sumar `TrasladoHuevos.TotalHuevos` — con el total ahora siempre
          correcto (legacy o por ítems), `HuevoTotDinamico` resta bien los traslados por ítems
          también. Único cambio en ese archivo
        - `TrasladoHuevosService.CrearTrasladoHuevosAsync`: con `HuevoItems` en el payload exige
          LPP, valida con `HuevoItemsCalculos.Validar` + la nueva disponibilidad por ítem, persiste
          `Metadata`+`TotalHuevos` con las 11 `Cantidad*` en 0. **Sin `HuevoItems`, byte a byte igual
          que siempre** (mismo flujo legacy, sin tocar)
        - Frontend: los DOS formularios vivos (`traslado-huevos-form` en `/traslados-huevos/nuevo` y
          `modal-traslado-huevos` embebido en la lista — los dos permiten crear) reemplazan la
          grilla de 11 tipos fijos por el selector de ítems del catálogo de F7 (mismas funciones
          puras reusadas, cero duplicación) cuando `clasificacionHuevoPorItems` está ON
        - **Gap conocido, documentado, no cerrado**: `ActualizarTrasladoHuevosAsync` (editar un
          traslado `Pendiente`) sigue solo con las 11 columnas legacy — riesgo bajo porque el alta
          procesa en el mismo request (nunca queda "Pendiente" el tiempo suficiente para editarse
          salvo que el procesamiento automático falle)
        - Validado: `dotnet build`/`dotnet test` (pendiente de correr en esta sesión tras el cambio,
          ver bloque de validación al pie) + migración aplicada y re-verificada en BD local
  - [ ] F10.1 Bodega de salida como desplegable (destinos de la granja, sin digitación libre) —
        **sigue sin resolver, ambigüedad real** (§9.3 del plan): "Traslado" (no Venta) hoy no
        captura destino en absoluto (`granjaDestinoId` se manda `undefined` siempre); la lista
        `traslado_de_huevos_planta_destino` (Venta→Planta) es una lista maestra de la EMPRESA, no
        por granja. No está claro si el pedido es agregar destino a "Traslado" o cambiar el alcance
        de esa lista — no se adivina, a confirmar con el cliente
  - [x] F10.2 Tipos de huevo del traslado alineados al catálogo nuevo — cerrado como parte del fix
        de arriba (mismo selector de ítems de F7, reusado en los 2 formularios)
  - [x] **Lado de lectura, mismo bug (21-ago-2026, misma sesión, tras "seguí con lo que se pueda"):**
        auditando el LISTADO de traslados (`traslados-huevos-list`, la pantalla real detrás de
        `/traslados-huevos` → `redirectTo: 'lista'`) encontré que sufría el mismo problema que el
        fix de arriba, ya con datos reales de por medio:
        - `getTotalHuevos()` re-sumaba las 11 columnas legacy (todas en 0 para Santa Reyes) en vez
          de usar la columna `totalHuevos` ya correcta que el backend ahora persiste — esto rompía
          la columna "Total huevos" de la tabla Y los resúmenes "Ventas completadas"/"Traslados
          completados" del panel superior, que hubieran mostrado 0 aunque el traslado sí movió
          huevos reales. Corregido: usa `traslado.totalHuevos` directo
        - Panel "Inventario disponible"/"Producción acumulada" (espejo dinámico/histórico): mismo
          patrón "Incubables" + grid de 11 categorías en 0 — gateado por `clasificacionHuevoPorItems`
          igual que en el resto de F7/F10; el dinámico muestra el desglose por ítem
          (`disponibilidad.huevoItemsDisponibles`, ya expuesto por el backend), el histórico no tiene
          equivalente por ítem construido todavía así que solo oculta lo que mentía (queda el Total,
          que sí es correcto)
        - Tabla: nueva columna "Clasif. por ítems" (compacta, mismo patrón que "Transporte" de F9)
          para no dejar 11 columnas en 0 sin ningún indicio de que el dato real vive en otro lado
        - **Detalle ("Ver" en la tabla) no necesitó cambios**: ya abre `modal-traslado-huevos` en
          modo solo lectura, que el fix de arriba ya dejó mostrando `huevoItems` correctamente
        - Validado: `yarn build` 0 errores; balance de `<div>`/`@if` verificado con grep contra
          `git show HEAD:...` antes de compilar (mismo chequeo que atajó los 2 `</div>` de más de la
          tanda anterior)
- [ ] **F11 · Pruebas** (8h)
  - [ ] F11.1 Pruebas automatizadas de los cálculos y reglas nuevas
  - [ ] F11.2 No regresión sobre empresas productivas (Sanmarino, Panamá, Ecuador) — gate multipaís si toca `*SaldoAlimento*`/`fn_seguimiento_diario_*`
  - [ ] F11.3 Pruebas asistidas con el usuario de Santa Reyes sobre datos reales
- [ ] **F12 · Despliegue** (2h)
  - [ ] F12.1 Despliegue a producción y verificación posterior (TaskDef↔imagen↔`/version.json`)

---

## X4 — Fix mismatch front/back en `traslado-rapido` (20-ago-2026)

**Sin relación con Santa Reyes** — hallazgo de paso durante F9 (arriba, `task_88856448`), bug
general que afecta a cualquier empresa. Plan:
[`fase_de_desarrollo/fix_traslado_rapido_aves_mismatch_plan.md`](fase_de_desarrollo/fix_traslado_rapido_aves_mismatch_plan.md).

`traslado-form.component` (ruta `/traslados-aves/traslados`) arma un request con
`loteOrigenId`/`loteDestinoId` pero `POST api/MovimientoAves/traslado-rapido` bindea
`TrasladoRapidoRequest` (`LoteId` único + granja/núcleo/galpón origen-destino) — ningún nombre
coincide, `LoteId` queda sin bindear.

- [x] X4.1 Smoke "antes" — HECHO: POST real a `traslado-rapido` con el payload exacto del front
      (JWT+SECRET_UP de dev minteados a mano contra el backend local) → **400
      `LoteId field is required`**, no el 500 esperado por lectura estática (`[ApiController]`
      rechaza el modelo por nullable-reference-types antes de llegar al `int.Parse` de la línea
      469). Conclusión de fondo intacta: la pantalla nunca completa un traslado
- [x] X4.2 Redirigido `traslados-aves/traslados` → `traslados-aves/nuevo` (pantalla que ya
      funciona, mismo DTO en las 2 puntas) en `app.config.ts`, con comentario explicando el porqué
- [x] X4.3 Borrado `pages/traslado-form/` (`.ts`/`.html`/`.scss`) + `trasladoRapido()` /
      `TrasladoRapidoRequest` / `TrasladoRapidoResponse` de `traslados-aves.service.ts` — grep
      posterior confirma 0 referencias colgantes (fuera de comentarios propios y de un
      `traslado-form` homónimo sin relación en `features/inventario/`, no tocado)
- [x] X4.4 Borrado `traslados-aves.module.ts` + `traslados-aves-routing.module.ts` (huérfanos —
      `app.config.ts` es el routing real standalone; nada más los importaba)
- [x] X4.5 Validado de punta a punta:
      - `yarn build` (prod) → **0 errores**, 121s, sin warnings nuevos (solo el preexistente de
        `package.json` sin `license`)
      - Smoke "después" real en navegador (backend :5002 + front :4200 en el worktree, sesión JWT
        de dev minteada e inyectada en `localStorage`): navegar a `/traslados-aves/traslados`
        redirige a `/traslados-aves/nuevo` (título "Nuevo Traslado de Aves"), la pantalla carga
        **datos reales** de la BD local (14 lotes: K345A/B, A374A/B, S369A/B, A402A/B; ~28
        granjas) sin errores de consola — confirma que el destino del redirect es una pantalla
        viva, no solo "no crashea"
      - Backend: sin cambios de código → no hace falta `dotnet build`/`dotnet test` (el backend sí
        se levantó limpio para el smoke "antes": 0 errores, migraciones al día)
      - `netstat` confirma :5002 y :4200 libres al terminar (servers detenidos)
- [i] Backend `TrasladoRapidoAsync`/`TrasladoRapidoDto` **no se tocan**: quedan sin caller en el
      front pero son consistentes puertas adentro; fusionarlos con `Lote/trasladar` (mismo
      concepto: reubicar un lote) es una decisión aparte, no bloquea este fix
- [x] **X4 cerrado.**

---

## X5 — Limpieza dead code backend: `TrasladoRapido*` + `ITrasladoAvesService` (20-ago-2026)

Follow-up directo de X4 (arriba). Plan:
[`fase_de_desarrollo/limpieza_traslado_rapido_backend_dead_code_plan.md`](fase_de_desarrollo/limpieza_traslado_rapido_backend_dead_code_plan.md).

- [x] X5.1 **Verificado que esta rama NO tenía el fix de X4** (`bd5e712` no es ancestro de `main`/
      `HEAD` — vive solo en el worktree `claude/reverent-saha-d10a85`, sin mergear). Hasta este
      punto, `traslado-form.component.ts` en esta rama seguía llamando `trasladoRapido()`: la
      cadena backend todavía tenía un caller (roto, pero caller), borrarla habría sido regresión
- [x] X5.2 Merge de `claude/reverent-saha-d10a85` a esta rama (commit `9207b78`; worktree de esa
      rama estaba limpio, nada en progreso). Sin conflictos de código — un conflicto trivial en
      `.devpilot/events.jsonl` (log de telemetría append-only), resuelto concatenando ambos lados
      (commit `c9c6583`)
- [x] X5.3 Verificación post-merge: `grep -rn "TrasladoRapido" frontend/src` → 0 resultados,
      `pages/traslado-form/` ya no existe. Recién acá la cadena backend es dead code real
- [x] X5.4 Borrado en backend: acción `TrasladoRapido` + clase `TrasladoRapidoRequest`
      (`MovimientoAvesController.cs`), firma `TrasladoRapidoAsync` (`IMovimientoAvesService.cs`),
      implementación (`MovimientoAvesService.Traslados.cs` — sin tocar los 4 stubs
      `NotImplementedException` que comparten archivo, fuera de alcance), `TrasladoRapidoDto`
      (`MovimientoAvesDto.cs`) — commit `ae40e72`
- [x] X5.5 Borrado `ITrasladoAvesService.cs` completo — interfaz sin implementación, sin registro
      DI (`Program.cs` solo registra `ITrasladoAvesDesdeSegService`, servicio distinto), sin
      ningún consumidor (`TrasladosController.CrearTrasladoAves` ya construye el DTO inline) —
      mismo commit `ae40e72`
- [x] X5.6 Opción elegida: **borrar, no fusionar con `Lote/trasladar`** — cero callers reales,
      fusionar habría sido preservar capacidad que nadie usa (contra la regla de no diseñar para
      hipotéticos de CLAUDE.md), y arriesgar equivalencia de comportamiento entre dos
      implementaciones que ya divergieron en semántica
- [x] X5.7 Validado: `dotnet build` (portable SDK 10.0.301) → **0 errores**, 21 warnings (todos
      preexistentes, ninguno en los archivos tocados). `dotnet test` → **2960/2960 passed** (1
      `ZooSanMarino.Domain.Tests` + 2959 `ZooSanMarino.Application.Tests`), 0 fallos — ninguna
      prueba referenciaba `TrasladoRapido*`/`ITrasladoAvesService`
- [i] Deuda flagueada, no resuelta acá: 5 documentos en `backend/documentacion/` (análisis/diseño
      históricos, no specs activas ni Postman) describen `traslado-rapido` como contrato vigente;
      quedan desactualizados. Cero impacto en runtime/CI — fuera del pedido explícito de limpieza
      backend, spawneado aparte (`task_df96f56e`)
- [i] **Nota operativa:** un primer intento de esta limpieza escribió sus cambios (plan, este
      bloque y los 6 borrados de código) en el checkout principal
      (`C:\Users\SAN MARINO\Desktop\App_SanMarino\`) en vez de este worktree — un path sin el
      segmento `.claude\worktrees\awesome-goodall-4a90a7\` resuelve al checkout principal, que
      además otra sesión tenía en uso. Revertido con permiso explícito del usuario antes de
      reintentar acá, en el path correcto
- [x] **X5 cerrado.**

---

## X6 — Ganancia diaria (g) engorde: dividir por los días reales entre pesajes (21-ago-2026)

Ticket de Lady Malave (validación de indicadores del seguimiento diario de pollo engorde): peso
corporal, alimento diario, alimento acumulado, conversión y mortalidad+selección están OK; ganancia
diaria no dividía por los días transcurridos cuando el pesaje deja de ser diario (1ª semana a
diario, luego cada 4 días). Plan:
[`fase_de_desarrollo/ganancia_diaria_engorde_intervalo_pesaje_plan.md`](fase_de_desarrollo/ganancia_diaria_engorde_intervalo_pesaje_plan.md).

- [x] X6.1 Confirmado que la tabla del ticket (columnas Peso corporal / Ganancia diaria / Alimento
      diario / Alimento acum. / Conversión / Mortalidad+selección, Registro vs Guía) sale de un
      único cálculo real: `engorde-comun/services/indicadores-diarios-engorde-compute.service.ts`
      (`aves-engorde/services/indicadores-diarios-engorde-compute.service.ts` es un shim
      `export *` hacia el mismo archivo, no una segunda implementación a duplicar el fix)
- [x] X6.2 Fix: `gananciaDiariaRealG` ya comparaba contra el último peso REALMENTE registrado
      (`ultimoPesoMedido`, según la recomendación de Moises de no comparar contra el día calendario
      anterior) pero no dividía el delta entre los días transcurridos desde ese pesaje. Se agrega
      `ultimoPesoDia` y se divide entre `Math.max(1, dia - ultimoPesoDia)` — generaliza a cualquier
      intervalo real, no asume 4 días fijos. Pesaje diario (divisor 1) queda sin cambios
- [x] X6.3 Tests nuevos:
      `engorde-comun/services/indicadores-diarios-engorde-compute.service.spec.ts` — pesaje diario
      (sin cambio), pesaje cada 4 días (delta/4), intervalo distinto de 4, día sin peso (`null`,
      no mueve el acumulador), primer pesaje contra `pesoIni` en el día 0
- [x] X6.4 Migración data-only `20260821040000_SeedTicketGananciaDiariaEngordeLadyMalave`
      (módulo Tickets, patrón `SeedTicketPlanItalappSantaReyes` — Designer clonado del
      ModelSnapshot actual, sin cambio de schema): siembra el caso creado por Lady Malave
      (`ladymalave@ecuitalcol.com`), auto-asignado a `moiesbbuga@gmail.com`, ya en estado
      SOLUCIONADO con `solucion_descripcion` explicando el fix — lista para que ella la confirme y
      cierre desde la pantalla. Fail-open si falta cualquiera de los dos usuarios; idempotente por
      `titulo`
- [x] X6.5 Validado: `dotnet build` backend (0 errores, 21 warnings preexistentes) + `yarn build`
      frontend (0 errores) + spec nuevo (`ng test --include=...`, ChromeHeadless): **5/5 SUCCESS**.
      SQL de la migración corrido dos veces contra la BD local dentro de `BEGIN;...ROLLBACK;`:
      resuelve a Lady Malave (empresa 3) y al administrador (`assigned_to_user_id 496236603`,
      coincide con el valor ya conocido de sus propios casos), inserta 1 sola fila y la 2da pasada
      la encuentra sin duplicar — 0 filas persistidas en la BD real tras el `ROLLBACK`
- [x] **X6 cerrado.**

---

## X7 — Auditoría de no-regresión Santa Reyes sobre postura: 2 hallazgos, 2 fixes, 2 tickets cerrados (21-ago-2026)

Pedido del usuario: validar que la implementación de Santa Reyes (flags F0.1-F10) no afecte a
Sanmarino en postura y otros módulos. Auditoría con 4 agentes en paralelo (solo lectura) sobre:
gating de los 5 flags de comportamiento en seguimiento diario, el refactor `GuiaGeneticaLookup` (6
sitios compartidos por todas las empresas), traslados de aves/huevos (F7/F9/F10), y build/tests/BD
real. Resultado: aislamiento multi-tenant correcto (solo Santa Reyes y, en 2 flags no relacionados,
Demo a propósito, tienen algo encendido — Sanmarino/Panamá/Ecuador en `false` en los 7 flags), pero
2 hallazgos reales que sí tocan a Sanmarino.

- [x] X7.1 **Hallazgo 1 (introducido por el trabajo de Santa Reyes, afecta a cualquier empresa):**
      `TrasladoHuevos.TotalHuevos` pasó de calcularse en vivo a columna persistida al agregar el
      desglose por ítems (F10, commit `650f43a`), pero `ActualizarTrasladoHuevosAsync` dejaba
      editar las 11 cantidades legacy de un traslado `Pendiente` sin recalcular el total — el valor
      obsoleto llegaba al espejo de producción, al descuento diario y al listado al completar el
      traslado. Fix: recálculo condicional (solo si el traslado no usa `HuevoItems`) inmediatamente
      después de aplicar los cambios de cantidades, en
      `TrasladoHuevosService.ActualizarTrasladoHuevosAsync` (commit `75023c2`)
- [x] X7.2 **Hallazgo 2 (preexistente, NO causado por Santa Reyes, confirmado con datos reales):**
      `ReporteTecnicoProduccionService.cs` tenía 3 consultas a la guía genética compartida
      (`ProduccionAvicolaRaw`) sin filtrar por `company_id`. Confirmado en la BD local: Sanmarino
      (empresa 1) y Demo (empresa 4) comparten raza `AP`/año `2026` con 77 de 77 semanas
      solapadas — el reporte podía mostrarle a una empresa valores de guía genética de otra. Fix:
      agregado `p.CompanyId == _currentUser.CompanyId` a las 3 consultas (commit `8625d40`)
- [x] X7.3 Validado cada fix por separado: `dotnet build` (portable SDK 10.0.301, 0 errores,
      mismos 21 warnings preexistentes) + `dotnet test` (**2975/2975**, 0 fallos)
- [x] X7.4 Migración data-only `20260821125030_SeedTicketsFixesAuditoriaSantaReyes` (mismo patrón
      `SeedTicketPlanItalappSantaReyes`/`SeedTicketGananciaDiariaEngordeLadyMalave`: Designer
      clonado, sin cambio de schema) siembra **TK-2026-000177** (hallazgo 1, prioridad ALTA) y
      **TK-2026-000178** (hallazgo 2, prioridad CRITICA), ambos a nombre de la empresa **Santa
      Reyes** (forzada por nombre — el módulo auditado es el que ellos usan — no la empresa por
      defecto del creador), creados y **cerrados** (no solo solucionados: `Estado=CERRADO`,
      `FechaCierreSolicitante` y `CerradoPorUserId` poblados, mismos campos que
      `TicketService.Gestion.cs` usa al cerrar desde la pantalla) por `moiesbbuga@gmail.com`, que
      detectó, aplicó y validó ambos fixes en la misma sesión. SQL validado dos veces dentro de
      `BEGIN;...ROLLBACK;` antes de aplicar de verdad (idempotencia confirmada); aplicado con
      `dotnet ef database update` (arrastró también la migración de X6, ya commiteada y pendiente
      desde antes) y verificado en la BD real: ambos tickets `CERRADO`, `company_id=6`,
      `cerrado_ok=true` (commit `78f4366`)
- [x] **X7 cerrado.**

---

## X8 — Ajuste de encasetamiento: editar las aves de un lote que ya tiene seguimiento (21-ago-2026)

Ticket: crearon un lote de pollo engorde con la cantidad de aves equivocada y siguieron cargando el
seguimiento diario; ahora necesitan **editar el lote y sumarle** (o restarle) aves y que la
correccion baje en cascada a seguimientos, reportes, consumo, disponibilidad y ventas. Aplica
tambien a **postura**. Plan:
[`fase_de_desarrollo/ajuste_encasetamiento_lote_plan.md`](fase_de_desarrollo/ajuste_encasetamiento_lote_plan.md).

Decisiones del usuario: restar por debajo de lo consumido => **bloquear con detalle**; en postura la
correccion llega a **levante Y produccion**.

### Diagnostico (cerrado)

- [x] X8.1 En engorde el form edita el **saldo vivo**, no el encasetamiento: `lote_ave_engorde.hembras_l/
      machos_l/mixtas` los descuenta `RetiroAvesEngordeAplicador` + ventas, mientras `aves_encasetadas`
      es el inicial historico. Medido en BD local: lote id 5 => `aves_encasetadas=25.542` contra
      `maestro=1.840`
- [x] X8.2 `actualizarEncasetadas()` (lote-engorde-list.component.ts:1031) hace
      `avesEncasetadas = hembrasL + machosL` y esta enganchado por `valueChanges` (273-274) **y** por
      `(input)` en el HTML (524, 528) => tocar `# Hembras` **pisa el inicial con el saldo** y la fn
      vuelve a restar las bajas ya descontadas. No hay hoy ningun camino correcto para sumar/restar
- [x] X8.3 Linea base del invariante congelada: `fn_cuadre_aves_engorde(NULL)` => **191 lotes,
      0 descuadrados, 0 sin referencia**. Los 191 tienen fila `Inicio` en `historial_lote_pollo_engorde`
- [x] X8.4 En postura la semantica es la OPUESTA (`lotes.hembras_l` **si** es el inicial) y el trigger
      `trg_lotes_sync_lote_postura_levante` ya corre el delta sobre `lote_postura_levante.aves_h_actual`
      (migracion `20260806074742`). Quedan fuera `lote_etapa_levante.aves_inicio_*` — que **gana** sobre
      `lotes.hembras_l` en `GetMortalidadResumenAsync` — y todo `lote_postura_produccion`

### Ejecucion

- [x] X8.5 `AjusteEncasetamientoCalculos` (puro) + `AjusteEncasetamientoCalculosTests`: delta por
      sexo, aplicacion con clamp y bucket mixto (Panama), diagnostico del primer dia negativo,
      reversibilidad, no-op. **24 casos, todos verdes**
- [x] X8.6 Engorde backend: `LoteAveEngordeService.UpdateAsync` aplica **delta** en vez de pisar;
      escribe `aves_encasetadas` + fila `Inicio` + maestro en la misma unidad de trabajo (preserva el
      invariante de `fn_cuadre_aves_engorde`) y audita con `TipoRegistro=AjusteEncaset`, invisible
      para la conservacion igual que `AjusteResync`. El `DetailDto` expone `inicialHembras/Machos/
      Mixtas` por subconsulta correlacionada
- [x] X8.7 Engorde frontend: el form edita el **inicial** (no el saldo), bloque de aviso con el saldo
      vivo, `mixtas` suma al total encasetado (antes quedaba en 0 en lotes mixtos de Panama), helpers
      puros en `funciones/aves-encasetadas.funcion.ts` + README, y el 400 del gate llega al
      `ToastService` (el controller responde string plano, no `{message}`)
- [x] X8.8 Postura backend: partial `Funciones/LoteService.AjusteEncasetamiento.cs` propaga el delta a
      `lote_etapa_levante` y a `lote_postura_produccion` (`aves_h_inicial`/`hembras_iniciales_prod`/
      `aves_h_actual`) **preservando los NULL** (materializarlos cambiaria cual columna gana el
      `COALESCE` de la fn). `lote_postura_levante` se deja al trigger: no se duplica la formula
- [x] X8.9 Postura frontend: aviso en el form de edicion + mismo manejo del 400
- [x] X8.10 **Validado**: `dotnet build` 0 errores / 0 warnings · `dotnet test` **2999/2999** ·
      `yarn build` 0 errores

### Verificacion contra datos reales (smoke aislado)

> Clon `sanmarino_smoke_x8` (`TEMPLATE sanmarinoapplocal`) + content root propio + backend en :5501.
> `pg_stat_activity` confirmo 1 sola conexion, al clon. Al terminar: puerto libre, clon borrado.

- [x] X8.11 **Engorde, lote 107 (Ecuador, 42 seguimientos)**: PUT sin tocar aves ⇒ **cero cambios** y
      cero filas de auditoria. Sumar 500 H ⇒ inicial 10.917→11.417, encaset 24.374→24.874, saldo
      10.775→**11.275** (las bajas se conservan) y **toda la serie diaria +500** (dia 1:
      24.345→24.845; dia 42: 3.967→4.467). Restar 200 ⇒ los tres bajan 200. Testigo
      `fn_cuadre_aves_engorde` en **0 descuadrados / 0 sin referencia** despues de cada ajuste
- [x] X8.12 **Gate al restar**: bajar el lote 107 a 200 aves ⇒ **400** con dia y faltante
      («el 17/07/2026 ... por 10 ave(s) ... lleva 20407 aves consumidas») y **nada escrito**: ni el
      lote, ni el historial
- [x] X8.13 **Postura, lote 13 (K345A, Sanmarino, 168 dias de levante + 301 de produccion)**: sumar
      500 H ⇒ las **6 copias** corregidas — `lotes` 7.999→8.499, `lote_etapa_levante` 7.999→8.499,
      `lote_postura_levante` inicial y actual →8.499, `lote_postura_produccion` inicial 7.597→8.097,
      `hembras_iniciales_prod` →8.097 y `aves_h_actual` 5.315→**5.815** (conserva las bajas de
      produccion). `fn_seguimiento_diario_produccion` +500 en los 301 dias, 0 negativos
- [x] X8.14 🔴 **El smoke encontro 2 defectos que ni el compilador ni los tests unitarios veian**:
      (a) EF no traduce una llamada a metodo propio dentro del arbol de expresion — la subconsulta del
      `Inicio` reventaba en runtime con *The LINQ expression could not be translated* y dejaba el GET
      del modulo en 500; se escribio en linea. (b) el gate de postura media **solo la serie de
      levante**: en el lote 13 (levante 739 aves, produccion 2.492) bajar la base a 1.232 pasaba el
      filtro y hundia `lpp.aves_h_inicial` de 7.597 a 0 por clamp, en silencio. Corregido a la serie
      del **ciclo completo** y fijado con el test `Medir_solo_una_etapa_del_ciclo_deja_pasar_...`
- [x] X8.15 **Gate multipais (CLAUDE.md §Invariantes)** sobre el clon, linea base congelada antes y
      comparada despues: `dif_saldo_alimento`, `dif_ingreso`, `dif_consumo`, `dif_documento`,
      `filas_nuevas` y `filas_que_desaparecen` = **0 en las dos empresas**. Unico diff:
      `dif_saldo_aves = 42` en ItalcolEcuador, **atribuido lote por lote**: las 42 filas son todas del
      lote 107 y todas cambian exactamente +500 — Panama en 0. `fn_cuadre_alimento_engorde` da
      **68 galpones / 11 con kilos / 19 con dias rojos, identico con y sin el cambio** (deuda
      preexistente, no se movio)
- [x] **X8 cerrado.**

---

## X9 — Las grillas mostraban el SALDO bajo el rotulo «aves encasetadas» (21-ago-2026)

Reporte del usuario: en Gestion de lotes y en el detalle del lote, las columnas de hembras y machos
"se estan moviendo" con el seguimiento diario y ya no suman las aves encasetadas — «hay unos que
dicen 19.100 y algo, pero si uno suma los dos dan menos». El encasetamiento es historico del lote y
no se puede tocar. Continuacion de X8; mismo plan:
[`fase_de_desarrollo/ajuste_encasetamiento_lote_plan.md`](fase_de_desarrollo/ajuste_encasetamiento_lote_plan.md).

- [x] X9.1 **Reproducido con datos reales.** Engorde: la grilla pintaba `hembrasL`/`machosL` (el
      SALDO) junto a `avesEncasetadas` (la BASE) ⇒ **123 de 124 lotes de Ecuador se veian mal**. El
      caso que el usuario nombro es el lote 24: la columna decia **19.120** y las de al lado
      mostraban 1.103 + 2.552 = **3.655**; el encasetamiento real es 9.061 + 10.059 = 19.120. Peor
      caso, lote 19: encaset 51.438 contra 2.832 mostrados
- [x] X9.2 Engorde arreglado: grilla y panel de detalle pasan a mostrar el **encasetamiento**
      (`inicialHembras/Machos/Mixtas`, que X8 ya expone en el DTO), rotulos explicitos «Hembras
      encaset.» / «Machos encaset.», y el saldo **no se pierde**: el detalle gana la fila «Aves vivas
      hoy (saldo)» con su desglose H/M/X. Accesores en el componente que devuelven **numeros**, no
      objetos, para no romper la deteccion de cambios; total via
      `totalEncasetadoDelLote` en `funciones/`
- [x] X9.3 Postura: los tabs **Levante** y **Produccion** tenian el mismo defecto
      (`avesHActual ?? ...` bajo el rotulo «encaset.»). Corregidos a `hembrasL ?? avesHInicial ...`
      ⚠️ **el orden importa**: `avesHInicial` en produccion NO es el encasetamiento sino las aves que
      sobrevivieron al levante — medido en P-K345B, encaset 12.587 (10.991+1.596) contra un inicio de
      produccion de 11.526 ⇒ con `avesHInicial` primero, la columna no cuadraba con el total. En
      levante los dos coinciden por construccion (trigger; 21 de 21 lotes verificados)
- [i] X9.4 **Los tabs Levante y Produccion de postura estan COMENTADOS en el HTML desde el commit
      `cd9b1a7` (25-may-2026)** — no hay forma de llegar a ellos desde la UI. El tab vivo («Lotes
      Seguimientos») usa `hembrasL`, que en postura SI es el encasetamiento, y **ya estaba
      correcto**. O sea: en postura el fix deja el codigo bien para cuando se reactiven, pero **hoy
      no cambia nada visible**. El defecto que el usuario ve es solo de engorde
- [x] X9.5 Barrido del resto del front: los otros 3 sitios que derivan `hembrasL + machosL`
      (`tabla-registro-list`, `lote-produccion/tabs-principal`, `shared/hierarchical-filter`) son
      todos `LoteDto` de **postura**, donde esa columna es la base ⇒ correctos, no se tocan
- [x] X9.6 **Verificado en pantalla** (build de produccion servido en :4310 con proxy al backend de
      smoke en :5501 contra un clon de la BD; sesion inyectada en `localStorage`): la grilla de
      engorde pinta «HEMBRAS ENCASET. / MACHOS ENCASET. / AVES ENCASET.» y las **6 primeras filas
      suman exacto** (lote 24 → 9.061 + 10.059 = 19.120). El detalle del lote 24 muestra
      encasetamiento 9.061 / 10.059 / 19.120 **y** «Aves vivas hoy (saldo) 3.655 · H: 1.103 · M:
      2.552 · X: 0». Sin spinner colgado; los 2 errores de consola son NG05604 (Service Worker de la
      PWA, que el servidor de prueba no sirve), ajenos al cambio
- [x] X9.7 Contraste por HTTP sobre los mismos endpoints que alimentan las grillas: engorde
      **124 lotes / 0 columnas que no suman**, levante **16 / 0**, produccion **2 / 0** (antes 2 de 2
      no cuadraban). `yarn build` 0 errores. Entorno de prueba cerrado: puertos libres, clon borrado
- [x] **X9 cerrado.**

---

## X10 — Tickets de X8 y X9 en ItalJira (21-ago-2026)

Pedido del usuario: registrar en ItalJira los dos casos ya resueltos, para dejar el tablero alineado
con el codigo.

- [x] X10.1 Migracion data-only `20260821160000_SeedTicketsAjusteEncasetamientoLote` (patron
      `SeedTicketsFixesAuditoriaSantaReyes`: Designer clonado del ModelSnapshot actual, sin cambio de
      schema; el SQL vive en el partial `.Seed.cs`). Siembra **2 casos, ya CERRADOS**, a nombre de
      **ItalcolEcuador** (resuelta por nombre, no por id) — es donde los dos se reportaron y se
      midieron. Creador, asignado y cerrador: `moiesbbuga@gmail.com`, resuelto **por email**.
      Fail-open si falta el usuario o la empresa; idempotente por `titulo`
- [x] X10.2 **TK-2026-000178** (CRITICA) — «Lote engorde: editar las aves de un lote con seguimiento
      reescribia el encasetamiento con el saldo». Causa, medicion (lote 5: 25.542 contra 1.840),
      solucion y la validacion completa del smoke; commit `a9fd721`
- [x] X10.3 **TK-2026-000179** (ALTA) — «Gestion de lotes: las columnas de hembras y machos mostraban
      el saldo, no las aves encasetadas». Incluye el caso que nombro operacion (lote 24: 19.120
      contra 3.655), el alcance real en postura y **la trampa del fallback de produccion**
      (`aves_h_inicial` NO es el encasetamiento); commit `299c816`
- [x] X10.4 SQL validado **dos veces dentro de `BEGIN; ... ROLLBACK;`** antes de aplicar: la 2da
      pasada reusa los mismos ids y no duplica ⇒ idempotente. Confirmado que el ROLLBACK no dejo
      rastro (0 filas, 175 tickets totales, igual que antes)
- [x] X10.5 Aplicado de verdad sobre la BD local: **TK-2026-000178 y TK-2026-000179**, ambos
      `CERRADO`, `company_id=3`, con `fecha_solucion`, `fecha_cierre_solicitante` y
      `cerrado_por_user_id` poblados. `notificado_correo=false` (es SQL, no pasa por la cola de
      correo). Migracion registrada en `__EFMigrationsHistory` **en la misma transaccion que el
      seed** — el efecto esta realmente en la base, que es la condicion que exige CLAUDE.md §🗄️
      *(se aplico por psql y no por `dotnet ef` porque el backend de otra sesion tiene tomado el
      `bin/`; `UseArtifactsOutput` rompe `GetEFProjectMetadata`)*
- [x] X10.6 Validado: `dotnet build` 0 errores / 0 warnings · `dotnet test` **2999/2999**
- [!] X10.7 **Decision pendiente del usuario:** reactivar los tabs «Lotes en Levante» y «Lotes en
      Produccion» de postura, comentados en el HTML desde `cd9b1a7` (25-may-2026). Su codigo quedo
      corregido en X9.3, pero descomentarlos es una decision de producto — se comentaron a proposito
      y este trabajo no los toca. Queda anotado en el propio TK-2026-000179
- [x] **X10 cerrado** (salvo X10.7, que espera decision).
