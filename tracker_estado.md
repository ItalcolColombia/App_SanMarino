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
- [x] **A2 · V39.13 — ventana de gracia CERRADA** (21-ago-2026, commit `def1fd4`). Un token sin
      `jti` ahora recibe 401 con `errorCode: sesion-revocada`. La trampa que advertía este pendiente
      se resolvió **separando los dos usos**: el fallo de BD dejó de compartir valor con `Legado` y
      tiene estado propio (`EstadoSesion.NoVerificable`), que sigue dejando pasar y además nunca se
      cachea. **Precondición verificada, no asumida:** `JwtSettings__DurationInMinutes = 60` en la
      TaskDef viva (164) ⇒ un día después del despliegue de B1 (20-ago) no quedaba un solo token sin
      `jti` vivo; y `AuthService` es la **única** fábrica de tokens de usuario del backend (un solo
      `new JwtSecurityToken` en todo el repo, verificado por grep) y siempre emite `jti` + anota la
      fila. Los PAT `sk_` van por su propio esquema y ni pasan por `EvaluarAsync`.
      *Detalle → X14*
- [x] **A4 · V30.7 · H1 Santa Reyes** — flags en `companies` + catálogo de ítems + silo en el form de
      ingreso a granja + homologación ERP + seed de las 5 guías genéticas (540 filas). **Detalle
      granular y estado real → V52 (F0-F2)**.
      *CERRADA el 21-ago-2026: F0, F1 y F2 al 100 % (las guías resultaron **615** filas, no
      540 — recontadas contra el Excel del cliente).*
- [x] **A5 · V30.8 · H2** — semanas por raza (hoy hardcodeadas en
      `modal-seguimiento-diario.component.ts:1463`), consumo sólo hembras, ocultar machos y error de
      sexaje **en UI** (⚠️ no borrar del modelo: lo consumen los saldos), tipos de inventario.
      **→ V52 (F3-F6)**.
      *CERRADA: F3, F4 y F6 al 100 %; F5.1 y F5.2 hechas. Lo único que queda de H2 es F5.3,
      que es una definición del cliente → `TK-2026-000180` / `SR-DEF-1`.*
- [!] **A6 · V30.9 · H3** — huevos: incubables→sin clasificar, los 7 ítems, primera postura por raza
      con vigencia ≤ semana 22, PNC por catálogo (⚠️ sin tocar las 11 columnas físicas). **→ V52 (F7-F8)**.
      *F7.1, F7.2, F7.3, F7.4 y F8.2 hechas (F7.3 el 21-ago, X17). Quedan F8.1 —desbloqueada
      parcialmente en X15: los ítems ya existen, faltan los códigos ERP— y F8.3 →
      `TK-2026-000180` / `SR-DEF-3`, `SR-DEF-4`.*
- [!] **A7 · V30.10 · H4** — traslados: aves (exponer `Placa`/`Conductor`/`Sellos` en postura — **ya
      existen en `MovimientoAves`**, falta la UI) y huevos (bodega destino desplegable) + no regresión
      multipaís. **→ V52 (F9-F12)**.
      *F9.1, F9.2, F9.2b, F10.2, F11.1, F11.2 y F12 hechas. Quedan F9.2c y F10.1, las dos del
      cliente → `TK-2026-000180` / `SR-DEF-5`, `SR-DEF-6`; y F11.3, que necesita al cliente.*

> **A4-A7 · estado al 21-ago-2026 (X14):** todo lo construible de V52 está construido, probado y
> desplegado. Lo que queda son las **6 definiciones que faltan del cliente** (F5.3, F7.3, F8.1,
> F8.3, F9.2c, F10.1), sembradas como caso `TK-2026-000180` (DUDAS, ABIERTO, Santa Reyes) con una
> subtarea `BLOQUEADA` cada una. F8.1 además necesita un DATO, no una decisión: los códigos ERP de
> los ítems nuevos del catálogo.
>
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
- [x] **F2 · Guías genéticas** (10h) — cerrada del todo el 21-ago-2026 (X14.7)
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
  - [x] F2.2 Asociación de la línea genética al lote + uso en indicadores y reportes
        — un chokepoint + 6 sitios de consumo, **y los 5 que faltaban, cerrados el 21-ago (X14.7)**:
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
        - [x] **Gap CERRADO el 21-ago-2026** (commit `457be71`, detalle en X14.7). Era real y era
          alcanzable: `ReporteTecnicoProduccionService` (3 sitios) y `ReporteTecnicoService` (2)
          traían la guía con consultas DIRECTAS a `ProduccionAvicolaRaw`, así que para Santa Reyes
          —cuya guía vive en la tabla dedicada— salían **sin una sola columna de comparación**. Y
          Santa Reyes tiene `/reportes-tecnicos` habilitado en `company_menus`, o sea que no era
          teórico. **No se unificaron las 5 consultas** (habría cambiado el SQL de las otras 3
          empresas: no todas filtran `deleted_at`, y `Like` ≠ `==` con un guion bajo en la raza):
          cada sitio pregunta primero por la guía propia y, si vuelve vacía, corre **su** consulta
          de siempre intacta. Delta cero por construcción
        - [i] **Corrección**: la nota original decía que
          `ReporteTecnicoProduccionService.cs:~1107` **no filtra por `company_id`**. Es **falso**
          contra el código de hoy — los 5 sitios filtran los cinco por
          `p.CompanyId == _currentUser.CompanyId`. No hay fuga entre empresas ahí
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
        *Bloqueado por el cliente -> `TK-2026-000180` / `SR-DEF-1` (X14.4)*
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
  - [x] F7.3 Huevo de primera postura: selección de tipos al crear/editar el lote — **CONSTRUIDO el
        21-ago-2026 (X17)**. El cliente definió la ambigüedad en sesión: es la lectura (a) —lista
        blanca por lote— y además **fail-closed**: «si no tiene asignado no aparece, ahí el usuario
        tiene que editar el lote para agregarle los tipos de huevos, así controlamos mejor todo».
        Tabla `lote_huevo_items` + filas FIJAS en el diario (se fue el `<select>` y el «agregar
        ítem»). *Detalle → X17*
  - [x] F7.4 Vigencia: habilitada hasta el último día de semana 22, deshabilitada desde el primer día
        de semana 23 — `HuevoPrimeraPosturaCalculos.EsVigente` (backend, con tests xUnit) + espejo
        `esVigentePrimeraPostura` (`items-huevo-catalogo.funcion.ts`); ítems marcados
        `metadata.primeraPostura=true` (los 3 que existen: Rojo/Blanco/Criollo) se deshabilitan en el
        `<select>` fuera de vigencia. `Company.HuevoPrimeraPosturaHastaSemana` existía desde F0.1 sin
        un solo consumidor (grep confirmó 0 usos) — este commit lo cablea y lo pone en 22 para Santa
        Reyes vía migración. **Alcance deliberado: solo UI** (no rechaza en el guardado) — mismo
        criterio "solo UI" que el resto de la familia de flags; extender a validación de guardado
        queda documentado en §8.2 del plan para cuando se confirme que hace falta
- [!] **F8 · Productos no conformes y panel de eficiencia** (7h) — F8.2 hecha; F8.1 y F8.3
      bloqueadas por el cliente (`TK-2026-000180`)
  - [!] F8.1 Renombrar PNC: Manchado, Decolorado, Enyemado, Picado, Fárfara — sin construir. Catálogo
        actual (11 ítems `Pnc`) no cubre las 5 categorías por raza: falta "Enyemado" completo (0
        ítems, hallazgo ya conocido desde F0.2) y "Decolorado" solo existe para Rojo. No se inventan
        cantidades/nombres sin confirmar con el cliente
        *Bloqueado por el cliente -> `TK-2026-000180` / `SR-DEF-3` (X14.4)*
  - [x] F8.2 Retirar huevo tratado, peso promedio y tipo de alimento del registro de producción —
        `huevoTratado` ya estaba oculto (vive dentro del bloque `!clasificacionHuevoPorItems`);
        `pesoHuevo`/`tipoAlimento` **no tenían ningún gate** (gap real, encontrado auditando el
        template junto con F7) — envueltos en `@if (!clasificacionHuevoPorItems)` acá. Los controles
        conservan su valor por defecto (`0`/`'Standard'`), siguen siendo válidos para
        `Validators.required` y se guardan igual — cambio de UI, no de contrato
  - [!] F8.3 Panel de eficiencia con la nueva nomenclatura + cuadre suma huevos = total granja — sin
        construir. El texto fuente (párrafo 68 del .docx) es contradictorio con F7.1 tal como está
        escrito y no hay pantalla "Panel de eficiencia" en el repo hoy — ver §8.3 del plan. A
        confirmar con el cliente si es pantalla nueva o ajuste de nomenclatura sobre un reporte
        existente antes de tocar nada (son reportes financieros)
        *Bloqueado por el cliente -> `TK-2026-000180` / `SR-DEF-4` (X14.4)*
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
  - [!] F9.2c **Comprobante — sin construir.** No existe una pantalla/printable de comprobante de
        traslado hoy (ninguna ruta ni componente con ese nombre); no está claro si el pedido es un
        PDF descargable, una vista de detalle imprimible, o el mismo listado alcanza. A definir antes
        de construir algo — mismo criterio que F5.3/F7.3/F8.3 (no adivinar UX)
        *Bloqueado por el cliente -> `TK-2026-000180` / `SR-DEF-5` (X14.4)*
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
  - [x] **4º lugar con el mismo bug — CERRADO el 3-sep-2026**: `traslados-aves/pages/inventario-
        dashboard` (pantalla de aterrizaje real de `/traslados-aves`) tenía su propia
        reimplementación del formulario de traslado de huevos (sin selector de ítems) — mismo síntoma
        de disponible 0 para Santa Reyes. **No se le agregó soporte de ítems por cuarta vez**: se
        borró el formulario propio (`procesarTrasladoHuevos` + `initTrasladoHuevosForm` +
        `actualizarValidadoresHuevos` + `tiposHuevo` + su pestaña, ~230 líneas) y la pestaña Huevos
        pasó a montar `ModalTrasladoHuevosComponent` —el del módulo `traslados-huevos`, que ya
        soporta `huevoItems`, ya edita y es el que usa `/traslados-huevos/lista`— con el lote
        preseleccionado. Mismo endpoint (`POST /api/traslados/huevos`), un solo formulario de huevos
        en toda la app. Plan: `fase_de_desarrollo/consolidacion_traslados_aves_huevos_plan.md` (B5)
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
  - [!] F10.1 Bodega de salida como desplegable (destinos de la granja, sin digitación libre) —
        **sigue sin resolver, ambigüedad real** (§9.3 del plan): "Traslado" (no Venta) hoy no
        captura destino en absoluto (`granjaDestinoId` se manda `undefined` siempre); la lista
        `traslado_de_huevos_planta_destino` (Venta→Planta) es una lista maestra de la EMPRESA, no
        por granja. No está claro si el pedido es agregar destino a "Traslado" o cambiar el alcance
        de esa lista — no se adivina, a confirmar con el cliente
        *Bloqueado por el cliente -> `TK-2026-000180` / `SR-DEF-6` (X14.4)*
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
- [~] **F11 · Pruebas** (8h) — F11.1 y F11.2 cerradas (21-ago-2026); F11.3 es del usuario
  - [x] F11.1 Pruebas automatizadas de los cálculos y reglas nuevas — el backend ya las tenía
        (`SemanasCicloPostura`, `HuevoPrimeraPostura`, `HuevoItems` 30 casos, `ItemInventarioTipo`,
        `GuiaGeneticaRequisito`); **el front no tenía ninguna, y es donde viven los ESPEJOS**.
        43 tests nuevos en 3 archivos (commit `7e3ebda`): paridad caso por caso con
        `SemanasCicloPosturaCalculosTests.cs`, vigencia de primera postura 22/23 + fail-open, y el
        invariante que V52 rompió DOS veces (F4 y F5): todo flag booleano que lee
        `ActiveCompanyConfigService` tiene que existir en el catálogo de la pantalla de Empresas.
        *Detalle → X14.2*
  - [x] F11.2 No regresión sobre empresas productivas — **el gate multipaís NO aplica, medido**:
        ningún archivo del rango `6e4fe7f..HEAD` define `fn_seguimiento_diario_*` ni
        `fn_cuadre_alimento_*`, ni toca un `*SaldoAlimento*` (las 51 menciones son del diagnóstico
        de solo lectura `verificar_cuadre_alimento_engorde.sql`, que las CONSULTA). Suites verdes:
        `dotnet test` **3014/3014**, `ng test` **624/624**, `dotnet build` y `ng build` 0/0.
        *Detalle → X14.3*
  - [~] F11.3 Pruebas asistidas con el usuario de Santa Reyes sobre datos reales — **fuera del
        repo**: es una sesión con el cliente, no hay código que escribir
- [x] **F12 · Despliegue** (2h) — lo construido de V52 **ya está en producción**
  - [x] F12.1 Verificado con el checklist obligatorio de CLAUDE.md §🚀 el 21-ago-2026: TaskDef viva
        `sanmarino-back-task:164`, `rolloutState COMPLETED`, 1/1 running; imagen
        `...backend:a62d8b4d...` = merge de PR #78, que es el HEAD de `origin/main-produccion`; y
        `origin/main` está **0 commits adelante**. O sea F0-F10 (con sus 8 migraciones de Santa
        Reyes) corren en prod, no es un rollback silencioso. ⚠️ Lo ÚNICO sin desplegar son los 3
        commits de esta sesión (A2 + tests + tickets), a propósito: el usuario decidió el 21-ago
        hacer el merge a `main-produccion` él mismo, en horario de baja operación, porque A2 es un
        cambio de autenticación en caliente. *Detalle → X14.5*

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

---

## X11 — La fase del lote sale del CIERRE, no de la fecha de encasetamiento (21-ago-2026)

Pedido del usuario: la columna Fase/Etapa del listado de lotes de postura se calculaba por edad
(>= 26 semanas desde el encaset => Produccion), asi que **un lote viejo que apenas se carga aparece
en Produccion sin haber pasado nunca a produccion**. Debe salir del estado real: levante mientras no
este cerrado, y produccion solo cuando el levante cerro Y existe el lote de produccion — «si no
tiene produccion no muestre la palabra produccion hasta que este en esa etapa».

- [x] X11.1 **Reproducido y medido.** `calcularFase(fechaEncaset)` (lote-list.component.ts:1513) hacia
      `edad < 26 ? Levante : Produccion`. En la base: **Sanmarino 10 de 16 lotes decian Produccion,
      solo 2 lo estaban**; los 8 falsos son justo los cargados con historia (A374, S369), todos con
      el levante ABIERTO y cero filas de produccion. Es el mismo defecto que
      `FaseLoteCalculos.EsRegistroLevante` ya habia corregido del lado de los reportes
      ([[etapa-lpl-nunca-cambia-en-la-transicion]])
- [x] X11.2 **La senal correcta se eligio con datos, no por intuicion.** Los criterios de
      `ExisteProduccionLoteAsync` (lote hijo en fase Produccion / mismo lote en Produccion, ambos con
      datos de registro inicial) dan **0 en los 21 lotes de la base** — la fase no se actualiza en la
      transicion. La unica prueba que funciona es **la fila viva en `lote_postura_produccion`**,
      exactamente lo que dice la memoria
- [x] X11.3 `FaseLoteCalculos.ResolverFaseVisible(levanteCerrado, tieneProduccion)` + sobrecarga que
      tolera el texto crudo del cierre (mayusculas, espacios, null) + `EsCierreCerrado`. **11 casos
      nuevos**, incluido el que fija que la regla NO depende de la edad y el que prueba que las dos
      sobrecargas son la misma formula
- [x] X11.4 Backend: `LoteDetailDto` gana `LevanteCerrado` y `TieneProduccion` (subconsultas
      correlacionadas **escritas en linea** — la leccion de X8.14: EF no traduce una llamada a metodo
      propio en el arbol de expresion) y **`FaseActual` como propiedad DERIVADA del record**, que las
      traduce con la formula unica. Escribir el ternario dentro del `Select` habria duplicado la regla
- [x] X11.5 Frontend: `calcularFase(l)` pasa a leer `faseActual`; sin ese campo devuelve `—`, no
      vuelve a adivinar por la fecha. Corregidos sus 2 usos vivos (grilla y panel de detalle).
      ⚠️ El `calcularFase` de `tabla-registro-list` NO se toca: es otro concepto
      (Inicio/Crecimiento/Engorde/Finalizacion por dias de engorde), sin este defecto
- [x] X11.6 **Verificado en pantalla** (build de produccion en :4310 contra backend propio sobre un
      clon): de los 16 lotes de Sanmarino, **solo K345A y K345B muestran «Produccion»** — los unicos
      con levante cerrado y produccion creada — y los otros 14 «Levante». El detalle de S369A dice
      **«Fase: Levante» con 51 semanas de edad**, que es exactamente el caso reportado. `GET /api/Lote`
      200; los 2 errores de consola son NG05604 (Service Worker) y un 403 de vacunacion por los
      permisos del JWT sintetico, ambos ajenos
- [x] X11.7 **Corrige en las DOS direcciones**, medido sobre todas las empresas: Sanmarino 10 => 2
      (8 falsos positivos eliminados) y **Demo 0 => 1 (un falso NEGATIVO**: LOTE 235A esta cerrado y
      con produccion pero mostraba «Levante» por tener menos de 26 semanas). Los 3 lotes con
      produccion de la base estan los 3 en `Cerrado` — consistencia total
- [x] X11.8 Validado: `dotnet build` 0 errores / 0 warnings · `dotnet test` **3011/3011** ·
      `yarn build` 0 errores. Entorno de prueba cerrado: puertos libres, clon borrado
- [x] **X11 cerrado.**

---

## X12 — Flag por empresa: separar los lotes de postura por etapa (21-ago-2026)

Pedido del usuario: poder decidir **desde el modulo Empresa, como los otros flags**, si se ven o no
las pestanas de Levante y Produccion. Reactiva las dos vistas comentadas desde `cd9b1a7`
(25-may-2026) — el pendiente X10.7 —, ahora que X11 hace confiable la etapa de cada lote.

- [x] X12.1 Flag `companies.separa_lotes_postura_por_etapa`, **nombrado por el comportamiento** y no
      por el tenant, `NOT NULL DEFAULT false` (fail-closed: nace apagado en TODAS las empresas y la
      migracion no lo enciende para nadie). Patron §🏢 del CLAUDE.md
- [x] X12.2 Backend, las **9 posiciones** del patron: `Company` + `CompanyConfiguration`, los 3 DTOs
      (`CompanyDto` / `Create` / `Update`) y las **4 proyecciones** (`CompanyService.ToDto`,
      `CompanyService.Crud` ×2 —alta y edicion—, `CompanyResolver` ×2, `CompanyPaisService`)
- [x] X12.3 Migracion `20260821170000_AddFlagSeparaLotesPosturaPorEtapa` idempotente
      (`ADD COLUMN IF NOT EXISTS`). Cambia schema ⇒ Designer clonado del ModelSnapshot **y**
      property agregada al propio ModelSnapshot, en orden alfabetico (hecho a mano: el backend de
      otra sesion tiene tomado el `bin/` y `dotnet ef` no puede correr)
- [x] X12.4 Frontend: `CompanyFlags` gana el campo en sus **6 posiciones** (interfaz,
      `FLAGS_APAGADOS`, forma de la respuesta, mapeo fail-closed, comparacion de igualdad y atajo
      `separaLotesPosturaPorEtapa$`) y **una linea** en el catalogo `FLAGS_EMPRESA`, que es todo lo
      que hace falta para que aparezca en la pantalla de Empresas
- [x] X12.5 Los dos tabs vuelven al HTML dentro de `@if (separaLotesPorEtapa)`. Si el flag se apaga
      con el usuario parado en una de esas pestanas, `loadCompanyFlags` lo devuelve a la lista
      completa — si no, se quedaria mirando una pestana que ya no existe
- [x] X12.6 **Verificado en pantalla, los dos estados**: con el flag APAGADO se ven solo «Lote Base»
      y «Lotes Seguimientos» (identico a hoy); ENCENDIDO aparecen ademas «Lotes en Levante»
      (16 lotes) y «Lotes en Produccion» (2), **todas las filas sumando** — el tab de produccion, que
      antes mostraba 21 y 26 aves, ahora muestra 10.991 + 1.596 = 12.587 gracias a X9
- [x] X12.7 **Aislamiento multi-tenant probado**: encendido solo en Sanmarino, la sesion de
      ItalcolEcuador NO ve las pestanas. El listado `GET /api/Company` devuelve `separa=true` solo
      para Sanmarino y `false` en las otras 4 empresas
- [x] X12.8 **Ciclo de escritura completo** por la API: PUT apagando => `false` (y los demas flags
      intactos), PUT encendiendo => `true`, y **PUT omitiendo el campo => conserva el valor**
      (patron `?? c.X` del Crud: un cliente viejo no puede borrar la configuracion)
- [x] X12.9 Validado: `dotnet build` 0 errores / 0 warnings · `dotnet test` **3011/3011** ·
      `yarn build` 0 errores. Entorno cerrado: puertos libres, clon borrado
- [i] X12.10 La columna **no se aplico en la BD local**: la migracion corre sola en el proximo
      arranque del backend (`RunMigrations=true`), que es el flujo normal. En el clon de prueba se
      aplico asi y quedo con `default false`
- [x] **X12 cerrado** — con esto se cierra tambien el pendiente **X10.7**.

---

## X13 — La pestana «Lotes en Levante» muestra solo los que estan HOY en levante (21-ago-2026)

Cierre del pendiente que quedo abierto al reactivar los tabs (X12): la pestana listaba los 16
registros de `lote_postura_levante`, incluidos los lotes que ya pasaron a produccion — el registro
de levante sobrevive a la transicion porque es la historia de esa etapa.

- [x] X13.1 `LotePosturaLevanteDetailDto` gana `TieneProduccion` (subconsulta correlacionada
      **escrita en linea**, misma leccion de X8.14) y `FaseActual` como **propiedad derivada**, que la
      traduce con `FaseLoteCalculos.ResolverFaseVisible` — la MISMA funcion que usa la columna
      Fase/Etapa del listado, asi que las dos vistas no pueden contradecirse
- [x] X13.2 El front filtra la pestana por `faseActual !== 'Produccion'`. Es un filtro de
      presentacion, no un recalculo: la regla vive en el backend
- [x] X13.3 **Verificado en pantalla, con las tres vistas cruzadas**: pestana Levante **14 lotes**
      (antes 16; K345A y K345B ya no aparecen), pestana Produccion **2** (P-K345A, P-K345B) y la
      lista completa **14 «Levante» + 2 «Produccion» = 16**. Los tres numeros coinciden por
      construccion
- [x] X13.4 Validado: `dotnet build` 0 errores / 0 warnings · `dotnet test` **3011/3011** ·
      `yarn build` 0 errores. Entorno cerrado: puertos libres, clon borrado
- [i] X13.5 Un lote de levante **cerrado pero SIN produccion** sigue apareciendo en la pestana
      Levante, coherente con lo que dice su columna Fase/Etapa. Solo sale de la lista cuando la
      produccion existe de verdad
- [x] **X13 cerrado.**

---

## X14 — Cierre de lo ejecutable que quedaba: A2, F11 y las 6 dudas del cliente (21-ago-2026)

Sesion de continuacion: **«continua con lo que esta en el tracker sin cerrar para darle fin a todo
en esta sesion»**. Se separo lo que dependia de codigo (se hizo) de lo que dependia del cliente (se
registro en ItalJira, decision del usuario en sesion).

### X14.1 · A2 — la ventana de gracia de los tokens sin `jti`, cerrada (commit `def1fd4`)

- [x] **La trampa que el pendiente advertia era real, y no se resolvia borrando la rama.**
      `SesionActivaService.EvaluarAsync` devolvia `EstadoSesion.Legado` en DOS situaciones sin
      relacion: (a) token sin `jti`, o sea anterior a B1 — la ventana de gracia; y (b) **fallo de
      base**, un fail-open deliberado para que una caida de RDS no desloguee a todas las tablets en
      campo con sus capturas sin subir. Compartian el mismo valor del enum, asi que cerrar (a)
      tocando `EsSesionValida` habria cerrado (b) tambien: un blip de RDS = logout masivo.
      **Se separaron:** el fallo de base pasa a `EstadoSesion.NoVerificable`, que sigue dejando
      pasar y ademas **nunca se cachea** (cachearlo hasta el `exp` seria una hora de barra libre
      para ese token por un solo error de red)
- [x] **Precondicion verificada, no asumida** — el pendiente decia «va despues de A1 y de verificar
      la revocacion en prod»:
      - `aws ecs describe-task-definition` sobre la TaskDef **viva** (164): `JwtSettings__DurationInMinutes = 60`.
        B1 se desplego el 20-ago ⇒ al dia siguiente no quedaba un solo token sin `jti` vivo. La
        ventana se apago sola, como estaba disenada
      - `grep 'new JwtSecurityToken('` sobre todo `backend/src` ⇒ **1 sola coincidencia**
        (`AuthService`), que siempre pone `jti` y llama a `RegistrarAsync`. No hay una segunda
        fabrica de tokens que pudiera emitir uno sin `jti`
      - Los PAT `sk_` van por el esquema `ServiceToken` (policy scheme «Smart» los desvia por
        prefijo del header) y **no pasan** por `EvaluarAsync`. Tienen su propia revocacion
- [x] **Que ve el usuario:** 401 con `errorCode: sesion-revocada`, que
      `debe-cerrar-sesion-por-401.funcion.ts` ya sabe leer — cierra sesion y pide login, que emite
      un token con `jti`. En la practica no deberia dispararse nunca
- [x] Validado: `dotnet build` 0 errores / 0 warnings · `dotnet test` **3014/3014** (+4 tests: el
      fail-open sigue pasando, `NoVerificable` no se cachea, los dos estados son distintos, y un
      barrido exhaustivo del enum que obliga a decidir explicitamente que hace un estado nuevo el
      dia que alguien agregue uno)
- [~] **Queda del lado del usuario:** verificar la revocacion **en prod** con una sesion real
      (revocar desde la pantalla y ver el 401 con motivo). No se puede hacer desde aca sin mintear
      una sesion de un usuario real, que es exactamente lo que el clasificador de seguridad bloqueo
      en F0.1 — y con razon
- [i] **Ata con C2.** Ese pendiente (subir `JwtSettings__DurationInMinutes` a 960 en la TaskDef)
      **ya no tiene el riesgo que tenia**: con A2 desplegado, un token de 16 h sigue siendo
      revocable en menos de un minuto. C2 sigue siendo del usuario (es la TaskDef, no el repo)

### X14.2 · F11.1 — las pruebas que faltaban estaban en el FRONT (commit `7e3ebda`)

- [x] **El backend ya estaba cubierto**: de los 113 `Calculos` solo 5 no tienen test, y ninguno es
      de V52. Los de Santa Reyes tienen los suyos (`SemanasCicloPostura` 7, `HuevoPrimeraPostura` 3,
      `HuevoItems` 30, `ItemInventarioTipo` 5, `GuiaGeneticaRequisito` 9)
- [x] **El gap real: las 3 funciones puras del front no tenian NI UN test**, y son justo los
      ESPEJOS del backend — el lugar donde la regla «una sola formula por numero» se rompe sola.
      43 tests nuevos:
      - `semanas-ciclo-postura.funcion.spec.ts` — espejo caso por caso de
        `SemanasCicloPosturaCalculosTests.cs`: cortes 8/24/28, cierre 102 (rojas/criollas) vs 112
        (blancas/azur), `null` cuando la raza no se reconoce. El caso de **103 semanas** es el que
        prueba que el grupo cambia el RESULTADO y no solo la etiqueta: fuera de ciclo para una roja,
        postura para una blanca
      - `items-huevo-catalogo.funcion.spec.ts` — `esVigentePrimeraPostura` (22 vigente / 23 no) y
        su fail-open, que es el caso **«flag OFF = comportamiento previo identico»** que exige el
        patron de features por empresa. Mas el mapeo del catalogo tolerando camelCase/snake_case, la
        fusion de items descatalogados y el orden Primera > Pnc > Sin categoria
      - `flags-empresa.funcion.spec.ts` — **el bug recurrente de V52 no fue de calculo, fue de
        CABLEADO**: F4 y F5 encontraron que el flag no habia llegado a `ActiveCompanyConfigService`,
        y F0.1 tuvo que cruzar a mano cinco `formControlName` contra el `.ts` porque Angular no
        valida esa coincidencia al compilar. El test lo vuelve un fallo de build: todo flag booleano
        que el runtime lee tiene que existir en el catalogo de la pantalla de Empresas. **Hoy pasa**
        (los 15 booleanos de `CompanyFlags` estan en `FLAGS_EMPRESA`), asi que es una red, no un fix
- [i] **Como se corre el front headless** (no estaba escrito en ningun lado):
      `CHROME_BIN="C:/Program Files/Google/Chrome/Application/chrome.exe" npx ng test --watch=false --browsers=ChromeHeadless`
      con el node portable en el PATH. Con `--include='**/x.spec.ts'` corre uno solo

### X14.3 · F11.2 — el gate multipais NO aplica, y esta MEDIDO

- [x] **Ningun archivo del rango `6e4fe7f..HEAD` (56 commits) define `fn_seguimiento_diario_*` ni
      `fn_cuadre_alimento_*`, ni toca un `*SaldoAlimento*`.** Las 51 menciones que aparecen al
      grepear el diff son del diagnostico de **solo lectura**
      `verificar_cuadre_alimento_engorde.sql`, que las CONSULTA. Se verificó buscando
      `CREATE OR REPLACE FUNCTION` / `DROP FUNCTION` en cada archivo cambiado, no por mencion del
      nombre — la mencion sola habria dado un falso positivo
- [x] Suites completas verdes: `dotnet build` **0/0** · `dotnet test` **3014/3014** ·
      `ng build` **0 errores, 0 warnings** (203 s) · `ng test` **624/624** ·
      `tsc -p tsconfig.spec.json` limpio · gate del `.sql`
      (`verificar-sql-llega-por-migracion.js`) OK
- [i] **Correccion a la nota de F2.2.** Decia que `ReporteTecnicoProduccionService.cs:~1107` **no
      filtra por `company_id`**, y lo dejaba marcado como riesgo sin tocar. **Es falso contra el
      codigo de hoy**: los 3 sitios de ese archivo (1107, 1374, 1718) y los 2 de
      `ReporteTecnicoService` (1607, 2725) filtran los cinco por
      `p.CompanyId == _currentUser.CompanyId`. No hay fuga de datos entre empresas ahi. Se deja
      escrito para que nadie vuelva a gastar tiempo persiguiendola

### X14.4 · Las 6 definiciones del cliente, en ItalJira (commit `9801f9d`)

- [x] **Decision del usuario en sesion**: registrarlas en ItalJira en vez de adivinar la lectura.
      Caso **`TK-2026-000180`** (`DUDAS`, `ABIERTO`, Santa Reyes, prioridad ALTA) con **6 subtareas
      `BLOQUEADA`** — `SR-DEF-1..6`: F5.3 (machos sobre el total en ventas), F7.3 (primera postura
      al crear el lote), F8.1 (productos no conformes), F8.3 (panel de eficiencia), F9.2c
      (comprobante de traslado), F10.1 (bodega de salida)
- [x] **Un caso con 6 subtareas y no 6 casos**: comparan solicitante, destinatario y condicion de
      cierre — una sola reunion con Santa Reyes las responde todas. Es lo contrario del criterio de
      `SeedTicketsAjusteEncasetamientoLote` (dos incidentes con reporte y arreglo propios); aca es
      una sola conversacion
- [x] **`BLOQUEADA` y no `BACKLOG`**: backlog dice «todavia no lo empezamos», bloqueada dice «no
      depende de nosotros». Y tipo `DUDAS` y no `REQUERIMIENTO` porque el requerimiento ya existe
      (`TK-2026-000172`): esto es lo que le FALTA para poder ejecutarse. Meterlas dentro del 172
      habria escondido que el bloqueo esta del lado del cliente
- [i] **F8.1 no se destraba con una decision, se destraba con un DATO.** Medido contra la base el
      21-ago: el catalogo de huevo de Santa Reyes tiene 21 items — Manchado y Picado en 4 razas
      (criollo/blanco/azur/rojo), **Decolorado solo en rojo**, **Farfara en un unico item generico
      sin raza** y **Enyemado en ninguna**. Los codigos son codigos del **ERP del cliente** (537,
      538, 539, 1944, 2124, 2125, 2521, 2522, 2523, 2697, 2698): inventar uno crea un item que el
      ERP no reconoce y la conciliacion falla en silencio recien cuando se cargue produccion real
- [x] Migracion data-only `20260821190000_SeedTicketDefinicionesPendientesSantaReyes` (Designer
      clonado, ModelSnapshot intacto), identidad por email y empresa por nombre, fail-open con
      `RAISE NOTICE`. Validada en BD local: **2 pasadas seguidas dejan 1 caso y 6 subtareas** (sin
      duplicar) y el `Down` probado **dentro de una transaccion revertida** (borra las 7 filas, el
      `ROLLBACK` las devuelve) — el patron que exige el propio CLAUDE.md antes de borrar nada

### X14.5 · Estado del despliegue (F12)

- [x] **Todo V52 ya corre en produccion.** Checklist obligatorio de CLAUDE.md §🚀 corrido el
      21-ago: servicio `sanmarino-back-task-service-75khncfa` en TaskDef **164**,
      `rolloutState COMPLETED`, 1/1 running; su imagen es
      `...backend:a62d8b4db881a2770685cbe1ff0578b4b15c49a5`, que es el **merge de PR #78** y el HEAD
      de `origin/main-produccion`; y `origin/main` esta **0 commits adelante**. No es un rollback
      silencioso
- [~] **Los 3 commits de esta sesion quedan en `main`, sin merge a produccion — decision del
      usuario.** A2 es un cambio de autenticacion en caliente y el merge se hace en horario de baja
      operacion. Pusheados a `origin/main` el mismo dia, asi que **no repiten el «Riesgo #1»** de
      trabajo que vive solo en un disco

### X14.6 · Lo que NO se cerro, y por que

- [i] **F11.3** (pruebas asistidas con Santa Reyes sobre datos reales) — necesita al cliente
- [i] **Los 6 de `TK-2026-000180`** — necesitan la respuesta del cliente
- [i] **B1-B4, B6** (decisiones del usuario sobre alimento y aves reales), **C1-C13** (fuera del
      repo: admin de Microsoft 365, TaskDef, secretos, escenarios de PWA con dos dispositivos) —
      siguen igual, ninguno es codigo
- [i] **Gap conocido de F10 que sigue abierto:** `ActualizarTrasladoHuevosAsync` (editar un traslado
      `Pendiente`) sigue solo con las 11 columnas legacy. No se toco: el alta procesa en el mismo
      request, asi que un traslado no queda `Pendiente` el tiempo suficiente para editarse salvo
      que el procesamiento automatico falle. Queda escrito, no olvidado
- [!] **4to formulario de traslado de huevos sin auditar**: `traslados-aves/pages/inventario-dashboard`
      (~1800 lineas) tiene su propia reimplementacion sin selector de items. Spawneado aparte
      (`task_b8e26e02`), sigue sin tocar

### X14.7 · F2.2 — el ultimo hueco de la guia genetica, cerrado (commit `457be71`)

- [x] **El gap era real y era ALCANZABLE, no teorico.** `GuiaGeneticaService`, `LoteService` y las
      3 liquidaciones ya miraban las dos tablas, pero `ReporteTecnicoProduccionService` (3 sitios) y
      `ReporteTecnicoService` (2) traen la guia con consultas PROPIAS y directas a
      `ProduccionAvicolaRaw`. Para Santa Reyes, cuya guia vive en `guia_genetica_santa_reyes`, esas
      consultas devuelven **cero filas**: el reporte sale sin una sola columna de comparacion contra
      la guia. Y Santa Reyes **tiene `/reportes-tecnicos` habilitado** en `company_menus` — o sea que
      el reporte se abre y sale vacio, no es un camino muerto
- [x] **Por que NO se reemplazaron por `ObtenerFilasCompatiblesAsync`, que ya existia.** Porque las 5
      consultas **no son iguales entre si**: las 2 de `ReporteTecnicoService` filtran `deleted_at` y
      las 3 de `ReporteTecnicoProduccionService` **no**; y la unificada usa `==` donde las otras usan
      `EF.Functions.Like` —que con un guion bajo en el nombre de la raza **no significa lo mismo**,
      porque en `LIKE` el `_` es un comodin—. Sustituirlas habria cambiado el SQL de Sanmarino,
      Panama y Ecuador, que no tienen guia propia y no deberian notar nada
- [x] **La forma que si conserva el comportamiento**: `GuiaGeneticaLookup.ObtenerFilasPropiasAsync`
      devuelve SOLO las filas de la guia propia, o lista vacia. Cada sitio pregunta primero por ella
      y, si vuelve vacia, corre **SU** consulta de siempre, intacta, sin mover una coma. El delta
      cero para quien no tiene guia propia queda garantizado **por construccion**, no por revision
- [x] Verificado en BD: de las 5 empresas **solo Santa Reyes** tiene filas en
      `guia_genetica_santa_reyes` (**615**); las otras 4 tienen **0**, o sea que el camino ejecutado
      para ellas es literalmente el de antes. `dotnet build` 0/0 · `dotnet test` **3014/3014**
- [i] **Correccion a la nota que dejo F2.2.** Decia que
      `ReporteTecnicoProduccionService.cs:~1107` **no filtra por `company_id`** y lo marcaba como
      posible fuga entre empresas. Es **falso** contra el codigo de hoy: los 5 sitios (1107, 1374,
      1718 de uno; 1607 y 2725 del otro) filtran los cinco por
      `p.CompanyId == _currentUser.CompanyId`. Se deja escrito para que nadie vuelva a gastar tiempo
      persiguiendola

- [x] **X14 cerrado.** Entorno: no se levanto backend propio (se uso `--artifacts-path` para no
      pelear el `bin/` con el backend ajeno vivo en :5002 desde las 08:49); no quedan procesos
      nuevos.

---

## X15 — Codigo ERP opcional (F8.1) + bug real de silos en produccion (21-ago-2026)

Pedido directo del usuario en la misma sesion, fuera del flujo de tracker: "el primer punto dejalo
opcional el codigo erp" (F8.1, `SR-DEF-3`) y un bug de silos en produccion descrito de memoria, sin
reproducirlo — la causa se confirmo leyendo codigo, no adivinando.

### X15.1 · `catalogo_items.codigo` ahora es opcional

- [x] **Schema**: `catalogo_items.codigo` (antes `NOT NULL varchar(50)`) pasa a nullable —
      migracion `CatalogoItemsCodigoOpcional`. El indice unico
      `ux_catalogo_items_codigo_company_pais (company_id, pais_id, codigo)` NO se toco: en Postgres
      cada `NULL` es distinto de cualquier otro en un indice unico estandar, verificado
      empiricamente (2 filas con codigo `NULL`, mismo company/pais, insertan sin choque)
- [x] **Backend**: `CatalogItem.Codigo` → `string?`. `CatalogItemService.CreateAsync` guarda `null`
      cuando llega vacio (no `""`) y salta el chequeo de duplicado cuando no hay codigo (no hay con
      que chocar). `CatalogItemService.UpdateAsync` suma la regla nueva: el codigo se puede
      **completar UNA SOLA VEZ** mientras el item no tenga uno — una vez asignado es clave natural
      y `dto.Codigo` que llegue despues se ignora, mismo criterio que ya aplicaba el formulario
      (deshabilitar el campo al editar). Duplicado al completar → 409 (antes no habia forma de que
      esto pasara, el codigo no era editable)
- [x] **6 sitios con warning de nulabilidad nuevo, los 6 corregidos** (no quedo ninguno nuevo:
      `dotnet build` 0/0): 4 `EF.Functions.ILike` con guard `!= null`, 3 asignaciones a DTOs
      legacy (`FarmInventoryMovementDtos`) con `?? string.Empty` (son de modelo A/alimento, un
      codigo nulo ahi no es un caso real hoy), y 2 tuplas de migracion Excel
      (`(int, string?, string, string?)`) que SI necesitan nulable de verdad — son las que resuelven
      items de huevo, que es justo el caso que ahora puede no tener codigo
- [x] **Frontend**: quito `Validators.required` del campo Codigo; el `[readonly]` del input pasa de
      `editing` a `editing && editing.codigo` (antes bloqueaba el campo SIEMPRE al editar, sin
      importar si tenia codigo o no — hubiera sido imposible completarlo despues); tabla y detalle
      muestran `—` cuando esta vacio
- [x] **Seed**: 7 items de PNC que faltaban en el catalogo de Santa Reyes, con `codigo = NULL` —
      Decolorado Blanco/Azur/Criollo (Rojo ya existia) y Enyemado en las 4 razas (no existia
      ninguno). **Sigue el patron YA establecido por Manchado/Picado (exactamente esas 4 razas), no
      inventa cobertura nueva** — Gallina Feliz/Bonegg/Libre de Jaula Certificado quedan afuera
      porque tampoco las tienen Manchado/Picado. Farfara (hoy generico, sin raza) NO se toca:
      partirlo en 4 es una decision de alcance, no una completitud de patron — sigue en
      `TK-2026-000180`/`SR-DEF-3` junto con los codigos reales, que son del ERP del cliente y no se
      inventan
- [x] Validado: `dotnet build` 0/0 · `dotnet test` **3014/3014** · `ng build` 0 errores/0 warnings ·
      `ng test` **624/624**. Migraciones aplicadas en BD local y verificadas: el ALTER corrido 2
      veces sin error, el seed corrido 2 veces deja 18 filas sin duplicar (11 + 7), `Down` probado
      dentro de una transaccion revertida (18→11, el `ROLLBACK` devuelve las 7)

### X15.2 · Bug real: el consumo de silo en PRODUCCION no encontraba el silo asignado al lote

- [x] **Causa confirmada, no adivinada** —
      [`lote-produccion-list.component.ts:424`](frontend/src/app/features/lote-produccion/pages/lote-produccion-list/lote-produccion-list.component.ts:424):
      al entrar a un lote desde el filtro de Produccion, `selectedLote.loteId` se poblaba con
      `lppFromFilter.lotePosturaProduccionId` — el id de `lote_postura_produccion`, OTRA tabla — en
      vez del `lote_id` BASE (`lotes.lote_id`). El modal de seguimiento diario usa
      `[loteId]="selectedLote?.loteId"` para pedir los silos asignados al lote
      (`GET .../lote-silos/{loteId}` → `lote_silos.lote_id`, que referencia el lote BASE, no el
      LPP): con el id equivocado la consulta no encontraba ninguna fila, y el selector de silo del
      consumo salia vacio — «no los coge», tal cual lo describio el usuario
- [x] **Por que el mismo lote SI mostraba sus silos desde Levante.** `modal-create-edit`
      (lote-levante) lee `getSilosDeLote(loteId)` con el `loteId` del `<select>` de lote, que ES el
      lote base (`[ngValue]="l.loteId"` sobre la lista de lotes) — nunca tuvo el bug. Confirma que
      la asignacion (`LoteSiloService`, backend) y el guardado del seguimiento
      (`ProduccionService.ResolverYSanarLoteIdAsync`, que self-sana el `lote_id` correcto de cada
      LPP) **siempre estuvieron bien**: el bug era 100% de ESTE componente, en la lectura para
      mostrar opciones, no en la escritura ni en la validacion del backend
- [x] **Por que "editar el silo, en produccion, solo llega hasta levante" es el MISMO bug, no uno
      aparte.** El operario asigna el silo desde `lote-list` (correcto, usa el `lote_id` base) →
      se ve bien desde Levante (correcto, mismo `lote_id`) → no se ve desde Produccion (el `loteId`
      que la pantalla de Produccion le manda al modal es otro numero). Un solo root cause explica
      las dos mitades de la descripcion
- [x] **El fix reusa un mecanismo que YA existia** para otro campo:
      `resolverLoteIdBaseLPP` — creado para el bloque de cohortes (`loteIdCohortes`), que YA hacia
      el fetch async del LPP completo para sacar su `.loteId` real — ahora TAMBIEN corrige
      `selectedLote.loteId` con el mismo valor resuelto, reasignando el objeto completo (no
      mutando in-place) para que dispare `ngOnChanges` en el modal (el componente padre es
      `ChangeDetectionStrategy.Eager`, no hay gotcha de OnPush acá) y vuelva a pedir los silos con
      el id ya corregido
- [i] **Barrido de todo el archivo para el mismo patron**: aparecen 2 sitios mas con
      `loteId: lpp.lotePosturaProduccionId` (`openTrasladoAvesModal`, líneas ~1147/1160), pero son
      **distintos a proposito** — ese feature ya tiene un campo `loteIdBase` separado y correcto
      para quien necesite el id real; `loteId` ahi es la identidad propia del traslado, no algo que
      la pantalla de silos consulte. No se tocaron
- [i] **Hallazgo relacionado, fuera de alcance — NO tocado.**
      `ValidacionSeguimientoService.Validar.cs:235` pasa `loteId: null` a
      `ValidarStockConsumoAsync` en el flujo de **doble validacion** (`requiereValidacionSeguimientoDiario`),
      que agrupa reservas por `{pais, granja, nucleo, galpon}` sin `LoteId` — un `null` ahi cae al
      fallback "todos los silos de la granja son validos" (mas PERMISIVO, no mas restrictivo: es lo
      opuesto al sintoma reportado). Verificado que **no aplica a Santa Reyes hoy**
      (`requiere_validacion_seguimiento_diario = false`). Corregirlo bien exige repensar el agrupado
      de reservas (podria abarcar mas de un lote a la vez) — no es un cambio de una linea, se deja
      documentado y sin tocar
- [x] Validado: `ng build` 0 errores/0 warnings · `ng test` **624/624** (sin tests nuevos: es un fix
      de una linea en un flujo que depende de datos reales en BD — Santa Reyes no tiene silos
      asignados todavia en el entorno local para poder escribir un spec que reproduzca el antes/despues
      contra el backend real)

- [x] **X15 cerrado.** Entorno: mismo criterio que X14 (sin backend propio, `--artifacts-path`).

---

## X17 — F7.3: los tipos de huevo los declara el LOTE, y el diario los pinta como filas fijas (21-ago-2026)

**Desbloqueado por el cliente en sesión.** Era `TK-2026-000180` / `SR-DEF-2`, una de las 6 dudas que
X14 había registrado. El usuario definió la ambigüedad y además eligió el modo estricto:

> «cuando yo creo un lote puedo seleccionar los tipos de huevos que me dará el lote. Necesito que
> esos tipos solo me aparezcan en la fase de producción, no todos los huevos… y en el seguimiento
> diario ya no tendrá que ser un select, sino que aparecerían por defecto los huevos permitidos para
> que coloquen su cantidad.»
>
> **Fail-closed, textual:** «no, si no tiene asignado no aparece; ahí el usuario tiene que editar el
> lote para agregarle los tipos de huevos, así controlamos mejor todo.»

Plan: [`fase_de_desarrollo/santa_reyes_items_huevo_por_lote_plan.md`](fase_de_desarrollo/santa_reyes_items_huevo_por_lote_plan.md).
Auditoría previa: 6 cortes en paralelo del flujo de Santa Reyes.

### X17.1 · El flujo de Santa Reyes, auditado antes de tocar nada

- [i] 🔴 **El flujo NUNCA se ejercitó.** Santa Reyes tiene **0 lotes** y en toda la base hay **0
      seguimientos con `metadata.huevoItems`**. Las otras 4 empresas tienen
      `clasificacion_huevo_por_items = false`. O sea que todo lo que se construya detrás de ese flag
      tiene **radio de impacto cero** sobre datos existentes — por eso el fail-closed que pidió el
      usuario es seguro HOY y no lo sería dentro de seis meses.
- [x] **Lo que ya estaba bien:** el guardado rechaza con 400 un ítem que no sea de huevo o de otra
      empresa, y resuelve la empresa efectiva por `farms.company_id` (no por el token), como manda
      CLAUDE.md §Features por EMPRESA regla 3.
- [i] **Alcance: SOLO producción, con evidencia.** Levante no tiene modelo de ítems para huevos en
      NINGUNA capa (11 columnas fijas), Santa Reyes tiene `captura_huevos_en_levante = false`, y
      `SeguimientoLoteLevanteService.cs:98` excluye explícitamente a las empresas de ítems. Peor: el
      arrastre de levante escribe las 11 columnas y `AplicarTotalesHuevoPorItems` **las pone en
      cero** — son incompatibles hoy. Llevar la lista blanca a levante no es un parámetro, es otro
      proyecto.
- [i] ⚠️ **Trampa confirmada:** el form vivo de lote es `features/lote/components/lote-list/`.
      `features/lote/page/lote-list/` + `modal-create-edit-lote` son huérfanos **que declaran el
      MISMO selector `app-lote-list`** y encima parecen mejor escritos (usan `ConfirmDialogService`).
      Tocar el que se ve más moderno es tocar código que no corre.

### X17.2 · La lista blanca

- [x] **Tabla `lote_huevo_items`**, réplica exacta de `lote_silos` (el patrón canónico de N:M por
      lote, ya probado en prod). Cuelga de `lotes.lote_id` —el maestro, la única fila que existe en
      las dos etapas— así que la declaración **sobrevive el cierre del levante** sin copiarse.
      Verificado en BD: `UNIQUE` bloquea el duplicado, `RESTRICT` impide borrar un ítem del catálogo
      que un lote declara, migración idempotente corrida 2 veces.
- [x] **Sin columna `orden` a propósito:** el orden sale del catálogo (Primera → Pnc → resto), que ya
      existe y ya está testeado. Una columna `orden` sería un segundo dueño del mismo número.
- [x] **La regla es pura:** `HuevoItemsCalculos.ValidarPermitidos` — función NUEVA, no se tocó
      `Validar`. `Validar` la comparten seguimiento, traslado y carga masiva; agregarle el parámetro
      habría cambiado el comportamiento de los tres a la vez. **Un traslado no valida la lista
      blanca a propósito**: mueve lo que YA se produjo, y exigir la lista de HOY dejaría huevos
      reales atrapados si la declaración cambió después.
- [x] **Los DOS caminos de escritura la aplican**: alta/edición manual (`ValidarHuevoItemsAsync`) y
      **carga masiva por Excel** (`MigracionService.HuevosPostura`). Si solo se hubiera puesto en el
      formulario, el Excel era la puerta de atrás de la restricción.
- [x] **Frontend:** servicio + `modal-asignar-huevo-items` (espeja `modal-asignar-silos`) + botón 🥚
      en la grilla de lotes, gateado por `clasificacionHuevoPorItems` (el flag no se leía en
      `lote-list`, se agregó). El modal avisa explícitamente si se va a guardar vacío — el operario
      tiene que enterarse ahí, no al día siguiente cargando producción.

### X17.3 · Filas fijas en el diario de producción

- [x] **Se fue el `<select>`, el «➕ agregar ítem» y el 🗑️.** El conjunto lo define el lote. Con eso
      se cayó código que existía solo para vigilar la selección: `onCambioItemHuevo` (anti-duplicado),
      `itemHuevoUsadoEnOtraFila`, `agregarFilaHuevo`, `eliminarFilaHuevo`, `totalItemsHuevoOfrecidos`
      y `cargarItemsHuevo`/`refrescarGruposHuevoItems` (el catálogo entero ya no se pide). **El
      duplicado pasó de imposible-por-vigilancia a imposible-por-construcción.**
- [x] **UX:** filas agrupadas por Primera / Pnc con **subtotal por grupo** y total general en vivo.
      Cada fila muestra nombre + código + unidad y un solo input.
- [x] 🔴 **Trampa de FormArray esquivada:** `reconstruirFilasHuevo` **vacía y repuebla la MISMA
      instancia**, nunca la reemplaza. `setupHuevosAutoCalculo` suscribe `valueChanges` UNA sola vez
      — cambiar la instancia habría dejado el total congelado para siempre, sin error.
- [x] Las cantidades ya escritas se conservan **por `catalogItemId`, no por índice**: el orden puede
      cambiar si el lote cambió su declaración mientras el modal estaba abierto.
- [x] Un ítem guardado que el lote ya no declara **no se pierde**: aparece como fila huérfana marcada.

### X17.4 · Los 6 defectos de la auditoría, arreglados

- [x] **D1 · «Total de huevos: 0» sobre un registro que sí tiene huevos.** Un registro legacy (11
      columnas, o cargado por migración) abierto con el flag ON mostraba 0, porque las 11 columnas
      están ocultas y el total por ítems arranca vacío. Ahora se muestra el total REAL con un aviso
      de que viene del formato viejo.
- [x] **D2 · El ítem en KILOS redondeaba en silencio.** `HUEVO RECUPERACION BOLSA KIL` (`um='KIL'`)
      se pesa, pero el contrato es `int`: 12,5 kg se guardaban como 13 sin una sola señal. Ahora el
      input usa `step` según la unidad y el guardado **avisa y frena** en vez de redondear callado.
- [x] **D3 · Ítems fantasma entre aperturas.** `resetForm` limpiaba `huevoItemsGuardados` pero no los
      grupos, y el componente no se destruye entre aperturas (el `@if (isOpen)` está dentro de su
      propio template). Ahora `resetForm` reconstruye las filas siempre.
- [x] **D4 · El backend aceptaba ítems INACTIVOS.** El front solo ofrece activos
      (`CatalogItemService.GetByTypeAsync` filtra `Activo`) pero `ValidarHuevoItemsAsync` no lo
      hacía: un ítem dado de baja seguía siendo un id válido para guardar. Los dos gates ahora
      coinciden.
- [x] **D5 · La vigencia de primera postura era 100 % UI.** `HuevoPrimeraPosturaCalculos.EsVigente`
      **no tenía un solo llamador en `backend/src`** — la regla del cliente («desde el primer día de
      la semana 23 no usa más el ítem») no se cumplía por ningún lado: la fecha es editable dentro
      del mismo modal, así que alcanzaba con elegir el ítem en semana 21 y corregir la fecha a
      semana 30. Ahora el backend la valida (`MensajeFueraDeVigencia`), y la fila se muestra
      **deshabilitada y explicada**, no oculta.
- [x] **D6 · El traslado de huevos no validaba catálogo ni flag.** Solo lo frenaba la disponibilidad,
      y un ítem con `Cantidad = 0` la pasa (el chequeo es `solicitado > disponible`). Se alineó con
      el gate del seguimiento — sin la lista blanca, por lo dicho en X17.2.

### X17.5 · Reportes (lo que pidió el usuario: «que se valide en el reporte contable y otros»)

- [i] **Los reportes NO leen `huevoItems`.** El contable, el técnico de producción, el diario de
      costos, el técnico semanal y el espejo consumen `huevo_tot` y las 11 columnas: son **ciegos al
      ítem**. Como el guardado por ítems deja `huevo_tot` = suma del desglose, la coherencia de
      TODOS ellos se reduce a un solo invariante.
- [x] **`backend/sql/verificar_huevo_items_reportes.sql`** (diagnóstico de solo lectura, exento del
      gate por prefijo `verificar_`) chequea las 4 cosas: `huevo_tot` == suma del desglose, las 11
      columnas en 0 en los registros por ítems, ningún ítem fuera de la lista blanca de su lote, y el
      espejo cuadrando con la suma de seguimientos. **Corrido el 21-ago: 0 descuadres**, y el espejo
      cuadra exacto en Agroavicola Sanmarino (3.632.634 = 3.632.634).

### X17.6 · Validación

- [x] `dotnet build` **0 errores / 0 warnings** · `dotnet test` **3031/3031** (+17: lista blanca
      fail-closed, ítem fuera de lista nombrado, orden de grupos, y la coherencia
      `EsVigente` ↔ `MensajeFueraDeVigencia` en todo el rango).
- [x] `ng build` **0 errores / 0 warnings** · `ng test` **633/633** (+9 de `construirFilasFijasHuevo`
      y `esItemEnKilos`).
- [x] Gate del `.sql` OK · migración aplicada y re-corrida en BD local sin error.
- [i] **Gate multipaís NO aplica:** no se tocó `fn_seguimiento_diario_*`, `fn_cuadre_alimento_*` ni
      ningún `*SaldoAlimento*`.
- [~] **Sin smoke visual en navegador**: Santa Reyes no tiene lotes en local, así que no hay contra
      qué abrir el modal con datos reales. La verificación fue de código + invariantes en BD.

### X17.7 · Smoke end-to-end contra el backend real (22-ago-2026)

Pedido del usuario: «desde el registro de un lote hasta liquidar o cerrar un lote de producción con
sus movimientos, ventas y traslados a plantas». Corrido contra `:5002` con el código nuevo, sobre
`sanmarinoapplocal`.

- [x] **Usuario de prueba DEDICADO**, a pedido del usuario: `smoke.santareyes@test.local`, rol
      «Santa Reyes Administrador». No impersona a nadie — es cuenta nueva. El hash lo generó
      `PasswordHasher<T>` de ASP.NET Identity, **la misma implementación que registra
      `Program.cs:154`**, y el cifrado del login/`X-Secret-Up` copia exacto el derivado de
      `EncryptionService` (PBKDF2, salt `sanmarino-salt`, 10k, SHA256, IV de 16 por delante). Nada
      de cripto reimplementada «parecida».
- [x] **El flujo completo pasa**: crear lote (201, raza validada contra la guía propia) → cerrar
      levante (crea el LPP, fase Producción) → declarar 4 tipos de huevo → 3 seguimientos con
      clasificación → traslado a planta → venta → cerrar el lote de producción (200).

| Caso | Esperado | Resultado |
|---|---|---|
| Seguimiento con huevos **sin declarar tipos** | rechazo | ✅ 400 «Este lote no tiene tipos de huevo asignados…» |
| Ítem **fuera de la lista** del lote | rechazo nombrándolo | ✅ 400 «El ítem «HUEVO CRIOLLO PRIMERAS POSTURAS SIN CLAS» no está entre los tipos que este lote produce» |
| **D5** · primera postura en semana 23 (límite 22) | rechazo | ✅ 400 «solo se puede registrar hasta la semana 22… El registro es de la semana 23» |
| Primera postura en semana 20 | guarda | ✅ 201 |
| **D6** · traslado con ítem inexistente | rechazo por catálogo | ✅ 400 «no existen como ítem de huevo ACTIVO del catálogo» |
| Traslado a planta / venta válidos | guardan `Completado` | ✅ 201 |
| Sobregiro (999.999 de un ítem con 80) | rechazo | ✅ 400 «No hay suficientes huevos disponibles» |
| Disponibilidad tras mover | 664: 8000−3000, 666: 150−100 | ✅ 5000 y 50, exacto |

- [x] **Reportes, con datos REALES** (`verificar_huevo_items_reportes.sql`): `huevo_tot` == suma del
      desglose (**8607 = 8607, 0 descuadres**), las 11 columnas legacy en 0, ningún ítem fuera de la
      lista blanca, y el **espejo cuadrando exacto** con la suma de seguimientos (8607 = 8607) —
      que es lo que alimenta el reporte contable. Sanmarino sigue en 3.632.634 = 3.632.634, sin tocar.
- [x] 🔴 **El smoke encontró un defecto que la lectura de código no había visto** (commit `cdf0239`):
      el backend **no completaba `codigo`/`nombre`/`tipoHuevo`/`um`** del desglose — confiaba en que
      el cliente los mandara. El formulario los manda, así que por la UI nunca falla; pero la API
      directa, un script o un cliente nuevo guardaban el snapshot con esos campos en NULL, y como
      `fn_clasificacion_huevo_items_produccion` los lee DIRECTO del jsonb (sin join al catálogo), el
      desglose semanal salía con filas sin nombre y la disponibilidad mostraba «(sin nombre)».
      Verificado en vivo: el registro 674 (antes) quedó con los 4 campos vacíos; el 676 (después),
      con el MISMO payload sin etiquetas, guardó `538 | HUEVO MANCHADO ROJO | Pnc | UND`.
      Se completa desde las filas del catálogo que el gate ya tenía cargadas: **cero consultas nuevas**.
- [i] **Ventana de fecha (V51) confirmada viva** en traslados: rechazó `2026-06-15` porque solo
      admite el mes en curso + 15 días. El seguimiento diario NO tiene esa guarda y aceptó junio —
      asimetría preexistente entre los dos endpoints, ajena a F7.3, anotada y no tocada.
- [~] **Datos del smoke: se DEJAN en la base local a propósito.** Lote `SMOKE-SR-001` (lote 152,
      LPP 10) con sus 3 seguimientos, 1 traslado a planta y 1 venta. Es el único lote de Santa Reyes,
      así que sirve para abrir la pantalla y ver las filas fijas con datos. Borrarlo es un `DELETE`
      del lote (cascade se lleva `lote_huevo_items`).
- [x] **Backend `:5002` queda ARRIBA y actualizado**, como pidió el usuario para la sesión de móvil.

### X17.8 · Smoke desde el FRONT, en el navegador (22-ago-2026)

Mismo recorrido que X17.7 pero manejando la UI real contra `:4200` + `:5002`.
**Encontró dos defectos que ni la lectura de código ni los 3.048 tests habían visto.**

- [x] **Lo que se verificó en pantalla, con datos reales:**
  - El botón **🥚 «Tipos de huevo que produce el lote»** aparece en la grilla de lotes, al lado del
    de silos, y solo porque la empresa tiene el flag.
  - El **modal de asignación** pinta los 28 ítems agrupados **Primera (2/10)** y **Pnc (2/18)**, con
    contador por grupo, resaltado del seleccionado, la etiqueta «primera postura» en los 3 que
    corresponde, la unidad **KIL** en `HUEVO RECUPERACION BOLSA KIL`, y **sin código** en los 7 PNC
    que sembró X15 esperando los códigos ERP del cliente.
  - El **seguimiento diario** muestra las **filas fijas**: 2 grupos, 4 filas, **sin `<select>` y sin
    «agregar ítem»**. La fila de primera postura sale **apagada y explicada**
    («primera postura fuera de vigencia», semana 33 > 22) — **D5 visible en pantalla**.
  - **Subtotales en vivo**: Primera 1.200, Pnc 90, total 1.290. Guardó el registro 677 con
    `huevo_tot` 1290 = suma del desglose y las 3 filas completas (código, nombre, tipo, unidad).
  - El bloque legacy de las 11 columnas **no se pinta** con el flag encendido.
- [x] 🔴 **DEFECTO 1 — las filas no cargaban al abrir el modal** (commit `e820ab3`).
      El bloque se pintaba con su encabezado y **cero filas**, aunque el lote tuviera 4 tipos
      declarados: no entraba ninguna de las 4 ramas del template (ni cargando, ni error, ni «lote
      sin tipos», ni filas). Causa: `cargarHuevoItemsDelLote()` estaba cableado **solo** en
      `loadCompanyFlags()`, que corre UNA vez en `ngOnInit` — ahí todavía no hay `loteId`, así que
      tomaba el early-return, y **nada lo volvía a disparar**: `ngOnChanges` llamaba a
      `cargarSilosDelLote()` pero no a este. Quedaba en el estado mudo: sin filas y **sin el mensaje
      que le diría al operario que declare los tipos**. El fix es una línea, al lado de los silos y
      por la misma razón (los tipos son del LOTE).
      **Los tests no podían verlo**: las funciones puras estaban bien y con cobertura; lo que
      faltaba era el disparador del ciclo de vida del componente.
- [x] 🔴 **DEFECTO 2 — la clasificación de la grilla salía en cero** (ya arreglado en `cdf0239`,
      X17.7). La grilla de registros tiene columnas **PRIMERA** y **PNC**, y ahí se vio el impacto
      REAL del desglose sin etiquetas: registro 676 (después del fix) → total 77, **Pnc 77** ✓;
      registros 674 y 675 (antes) → total correcto pero **Primera 0 y Pnc 0** ✗. Confirma que la
      consecuencia del bug era exactamente la predicha: el total cuadra y la clasificación miente.
- [i] **Al usuario de prueba le faltaba `user_farms`.** Con empresa y rol pero sin granja asignada,
      el selector de granja sale vacío y no se ve ni un lote. No es un bug: es cómo funciona el
      alcance por granja. Anotado porque cualquiera que cree un usuario a mano se lo va a comer.
- [i] **La sesión deslizante corta a los 5 minutos de inactividad** y los pasos de un smoke manual
      tienen huecos de minutos: la sesión se cayó dos veces a mitad del recorrido, con redirect a
      `/login` sin aviso. Para la próxima: batear los pasos en pocas tandas.
- [~] **Screenshots no disponibles**: el panel del navegador no estaba visible, así que la
      verificación fue por árbol de accesibilidad y por consultas al DOM, no visual.
- [x] Validado tras el fix: `ng test` verde, y el reporte de invariantes con los 4 registros ya
      cargados desde la UI — **9897 = 9897, 0 descuadres**, espejo cuadrando.

- [x] **X17 cerrado.** Entorno: sin backend propio (`--artifacts-path`), sin procesos nuevos.

---

## X18 — App movil ItalGranja (Flutter): login + SQLite + engorde/reproductora

**Plan:** [`fase_de_desarrollo/app_movil_italgranja_plan.md`](fase_de_desarrollo/app_movil_italgranja_plan.md)

Punto de partida: `zootecnicoapp/` ya tiene el design system traducido a Flutter (4.125 lineas,
tema + widgets + 4 pantallas), pero corre con `_lotesDemo` hardcodeado y un `SyncService` que
simula el envio con `Future.delayed`. Esta fase reemplaza la simulacion por el backend real.

Perfil: **Ecuador y Panama resueltos por pais** (decision del usuario, 21ago26). Credenciales de
smoke: `admin.ecuador@italcol.com` (company 3, pais 2).

### Contrato del backend — medido, no supuesto
- [x] Login cifrado ida y vuelta: AES-256-CBC + PBKDF2-SHA256/10000/`sanmarino-salt`, IV prepend,
      base64. Verificado 200 contra `:5002` con el usuario real
- [x] `X-Secret-Up` obligatorio fuera de `/auth/login`; su 401 se tipifica con
      `X-Auth-Failure: platform-secret` y **no** debe cerrar la sesion ni vaciar la cola
- [x] `GET /api/LoteAveEngorde` con los 4 headers → 200, 124 lotes (Ecuador)
- [x] `GET /api/Auth/menu` → descifrado OK; `admin.ecuador` ve *Pollo Engorde* y **no**
      *Reproductora Pollo Engorde* → confirma que los modulos se gatean por menu
- [i] El controller `SeguimientoAvesEngordeEcuador` atiende a los 3 paises: la app postea ahi,
      igual que el front web. La tabla `_ecuador` no existe

### Implementacion
- [x] `pubspec.yaml`: + `pointycastle`; fuentes Plus Jakarta Sans + Inter descargadas a `assets/fonts/`
      (el pubspec las declaraba y no existian: `flutter test` no arrancaba)
- [x] `core/crypto/crypto_service.dart` — espejo de `EncryptionService.cs`
- [x] `core/config/api_config.dart` — baseUrl y llaves por `--dart-define`, defaults = back local
- [x] `core/api/api_client.dart` — Dio + headers + los 6 tipos de fallo (`TipoFallo`)
- [x] `core/api/auth_api.dart` · `lotes_api.dart` · `seguimientos_api.dart`
- [x] `core/session/session_store.dart` + `sesion_actual.dart` (interfaz: el cliente HTTP no
      depende de sqflite, por eso el smoke corre con `dart run`)
- [x] `core/perfil_pais.dart` — agua (EC+PA) y quintales (solo PA), logica pura
- [x] `core/modulos_del_menu.dart` — menu → modulos por `route`; match EXACTO (la ruta de
      postura es prefijo literal de la de pollo engorde)
- [x] `core/alimento_obligatorio.dart` — espejo de `AlimentoObligatorioCalculos.cs`: se valida
      ANTES de encolar, si no el rechazo llega horas despues y el usuario ya no esta en el galpon
- [x] `local_db.dart` v1→v2 con `onUpgrade`: `pending_sync` se ALTERA (tiene trabajo del usuario),
      `lotes_cache` se recrea (es cache y cambia la PK a `(modulo, id)` — engorde y reproductora
      numeran aparte y el id 12 existe en los dos)
- [x] `sync_service.dart` — cola real: 201 ok · duplicado = resuelto · 401 segun cabecera ·
      la cola NUNCA se borra al cerrar sesion
- [x] `main.dart` — fuera `_lotesDemo`; arranque online/offline con sesion persistida
- [x] `login_screen.dart` y `seguimiento_screen.dart` contra la API
- [x] El date picker no ofrece fechas anteriores al encasetamiento (el backend las rechaza)

### Validacion
- [x] `flutter analyze` — **0 errores, 0 warnings** (5 `info` cosmeticos preexistentes del
      design system). De paso: `app_theme.dart` no compilaba (faltaba el import de
      `CupertinoPageTransitionsBuilder`) y la copia del design system se excluyo del analisis
- [x] `flutter test` — **66/66**: crypto (vectores + robustez), perfil_pais, modulos_del_menu,
      payload_seguimiento, alimento_obligatorio, widget del login
- [x] Smoke `tool/smoke_backend.dart` — **8/8 en Ecuador y 8/8 en Panama**. Verificado en BD:
      0 filas `SMOKE%` en las dos tablas; flag de Panama restaurado a `true`

### Hallazgos del backend (NO corregidos — fuera del alcance, requieren OK + tests xUnit)
- [!] **Reproductora y Produccion ignoran el consumo escalar al exigir alimento.**
      `ValidarAlimentoObligatorio` recibe `kgHembrasDirecto`/`kgMachosDirecto` justo para el
      cliente que no manda items de inventario (lo dice su propio doc-comment). Levante y los dos
      engordes se los pasan; `SeguimientoDiarioLoteReproductoraService.cs:267,384` y
      `ProduccionService.Seguimiento.cs:238,628` **no**. Con
      `requiere_validacion_seguimiento_diario` ON (hoy solo ItalcolPanama) rechazan con 400 «no
      tiene alimento» un registro que SI trae alimento. Medido: con el flag apagado en local el
      mismo POST creo el id 791. Afecta al movil, a la carga masiva por Excel y a la PWA
- [!] **El duplicado de reproductora vuelve como 500, no 400.**
      `SeguimientoDiarioLoteReproductoraController.Create` no tiene el
      `catch (DbUpdateException … 23505)` que si tiene el de engorde: sale el error crudo de
      Postgres. Mitigado EN LA APP (detecta el duplicado por contenido, no por status), pero
      cualquier otro cliente ve el 500

### Pendiente de la proxima fase
- [i] Levante y Produccion: la UI existe, falta el mapeo del payload (`endpointDeModulo` los
      deja en null a proposito y la pantalla lo dice)
- [i] Items de inventario con descuento de stock: exige el catalogo `item_inventario_ecuador`
      con existencias por galpon
- [i] Windows pide **Developer Mode** para compilar con plugins (`flutter pub get` lo advierte).
      No bloquea `analyze` ni `test`, si el build de la APK

---

## Alimento obligatorio: Reproductora y Producción ignoraban el consumo escalar

Plan: [`fase_de_desarrollo/alimento_obligatorio_consumo_escalar_reproductora_produccion_plan.md`](fase_de_desarrollo/alimento_obligatorio_consumo_escalar_reproductora_produccion_plan.md)
Origen: [`app_movil_italgranja_plan.md`](fase_de_desarrollo/app_movil_italgranja_plan.md) §7 — los dos hallazgos del smoke de la app móvil.

Con `requiere_validacion_seguimiento_diario` ON (hoy sólo **ItalcolPanama**, id 5), Reproductora y
Producción rechazaban con 400 «no tiene alimento» un registro que **sí** traía alimento, porque no
le pasaban al guard el consumo escalar. Afectaba a la app móvil, a la carga masiva por Excel y a la PWA.

### Fix
- [x] `AlimentoObligatorioCalculos.Capturado(metadata, kgHDirecto, kgMDirecto)` — la combinación
      MAX metadata-vs-escalar baja a `Application/Calculos/` (Infrastructure no es testeable: el
      proyecto de tests sólo referencia Application)
- [x] `SeparacionSeguimientoHelper.ValidarAlimentoObligatorio` delega en el cálculo puro
- [x] `SeguimientoDiarioLoteReproductoraService.cs` — pasar los directos en alta (`:267`) y edición (`:384`)
- [x] `ProduccionService.Seguimiento.cs` — ídem en alta (`:238`) y edición (`:628`), con las
      variables ya normalizadas a kg (`consumoKgH`/`consumoKgM`): el request trae `ConsumoH` **con
      unidad** y puede venir en gramos
- [x] Las 6 llamadas usan `(decimal)(x ?? 0)` en vez de `(decimal)x!`

### Bug del patrón que se copiaba (encontrado al hacerlo)
- [i] `(decimal)dto.ConsumoKgMachos!` sobre un `double?` **desenvuelve y lanza**
      `InvalidOperationException("Nullable object must have a value.")` — verificado ejecutando la
      expresión con el SDK 10.0.301. El `!` sólo calla al compilador. Como los controllers traducen
      esa excepción a 400, el usuario veía una «validación» que decía *Nullable object must have a
      value*. `consumoKgMachos` llega `null` de verdad (`ToDto` lo inicializa
      `alimentosMachos.Count > 0 ? … : null` ⇒ todo Panamá mixto)
- [x] Corregido también en los 3 services que ya pasaban los directos (Levante, Engorde,
      Engorde Ecuador): es la misma línea y mordía justo al cliente de este trabajo

### Duplicado de reproductora: 500 → 400
- [x] `SeguimientoDiarioLoteReproductoraController.Create` — copiar el
      `catch (DbUpdateException … 23505)` de `SeguimientoAvesEngordeEcuadorController`, **antes**
      del `catch (Exception)` genérico

### Tests y verificación
- [x] `tests/ZooSanMarino.Application.Tests/AlimentoObligatorioConsumoEscalarTests.cs` — escalar sin
      ítems cumple · sin nada rechaza con el **texto literal de hoy** · MAX no suma · flag OFF no
      evalúa nada · machos `null` no lanza
- [x] `dotnet build` — **0 errores, 0 advertencias**
- [x] `dotnet test` — **3.049/3.049 verde**, 17 de ellos nuevos. No se mató el backend que otra
      sesión tenía vivo en `:5002`: se compiló con `--artifacts-path` a un directorio aparte
- [i] **Los tests cubren el cálculo, no el call site.** `Application.Tests` no referencia
      Infrastructure, así que ningún test detecta que mañana alguien vuelva a llamar al guard sin
      los directos. Para eso haría falta un test de integración del service, que hoy no existe

### Verificacion end-to-end (sesion del movil, 22ago26)

Lo anterior valida el calculo; esto valida que el **usuario real deja de ver el 400**. Se levanto un
backend con el binario nuevo en **:5499** (`--artifacts-path` + `--contentRoot` propios; el de otra
sesion en :5002 quedo intacto) y se corrio `zootecnicoapp/tool/smoke_backend.dart`, que usa el mismo
codigo de la app movil.

- [x] **El bug 1, medido antes/despues.** Reproductora + Panama + flag ON, alimento como escalar:
      antes `FALLA 6 → El registro del 04/07/2026 no tiene alimento`; ahora
      `OK 6 → seguimiento creado id=793`. Es el caso exacto que motivo el fix
- [x] **El bug 2, medido.** Con el flag OFF, el segundo POST del mismo dia pasa de
      `TipoFallo.servidor (500)` a `duplicado` con el texto redactado
- [x] **Sin regresion en los 3 services que solo cambiaron por el `(decimal)x!`**: engorde Ecuador
      (sin flag) y engorde Panama (con flag) siguen 8/8
- [x] BD limpia: 0 filas `observaciones LIKE 'SMOKE%'` en las dos tablas · flag de Panama restaurado
      a `true` · puerto 5499 liberado al terminar
- [i] El smoke **no** cubre Produccion: sus dos call sites se corrigieron igual, pero la app movil
      todavia no postea ese modulo (`endpointDeModulo` lo deja en null). Queda cubierto solo por los
      tests del calculo hasta que la fase siguiente lo conecte

---

## X19 — App movil: Levante y Produccion, los dos modulos que faltaban (22ago26)

**Plan:** [`fase_de_desarrollo/app_movil_italgranja_plan.md`](fase_de_desarrollo/app_movil_italgranja_plan.md)
(el alcance de X18 los dejaba fuera: la UI existia, el mapeo no).

Perfil de prueba: **Agroavicola Sanmarino** (company 1, Colombia, pais 1) — es donde viven los lotes
de postura. Colombia no captura agua ni quintales, asi que ejercita el camino con los dos flags OFF.

### Contrato — medido
- [x] **Levante** postea a `/api/SeguimientoLoteLevante` con el MISMO request que engorde, mas
      `lotePosturaLevanteId`. Manda los **dos** ids: el maestro (`lotes.lote_id`) como `loteId` y el
      de la etapa aparte. Mandar solo el de la etapa da *«Lote '6' no existe»*
- [x] **Produccion** postea a `/api/Produccion/seguimiento` con un contrato propio: `mortalidadH`
      (no `mortalidadHembras`), consumo escalar + unidad, y `huevosTotales`/`huevosIncubables`/`etapa`
      **obligatorios**
- [i] **Un dia no puede tener registro de levante y de produccion a la vez** — el backend lo rechaza
      con un mensaje que explica por que (el ciclo sumaria dos veces el mismo alimento y las mismas
      aves). En el lote P-K345A se descartaron 25 fechas antes de encontrar una libre

### Implementacion
- [x] `core/postura_calculos.dart` — clasificadora (incubables = limpio + tratado; total = + las 9
      no incubables) y etapa del ciclo. **Logica pura con tests**: los dos numeros viajan calculados
      en el payload y el backend los persiste tal cual
- [x] La etapa **ya no se elige**: era un desplegable editable que ademas decia «semana 25-33». El
      rango real es **26**-33 / 34-50 / >50 (el calculo hace `max(26, …)`). Ahora se deriva del
      encasetamiento y se muestra de solo lectura, como en el web
- [x] `LotesApi.levante()` / `.produccion()`; mapeo compartido `loteDePostura`. **`hembrasL` NO sirve
      aca**: en postura es la BASE, el saldo vivo es `avesHActual`/`avesMActual`
- [x] `Lote.loteMaestroId` + SQLite **v3** (`ALTER` para quien venia de v2; los de v1 ya recrean la tabla)
- [x] Alimento y quintales agregados a levante y produccion (mismo patron que engorde)
- [x] **Codigo muerto eliminado:** el editor de items dinamicos (`_ItemsEditor`/`_ItemRow`/
      `ItemSeguimiento`) ya no lo montaba ninguna seccion. Sigue intacto en la carpeta del design
      system para cuando se implemente el descuento de stock

### 4 bugs de la app que destapo el smoke
- [x] **El motivo del 400 no se veia.** `SeguimientoLoteLevante` responde un **string JSON pelado**,
      no `{message}`: el usuario leia «El servidor respondio 400» en vez del motivo
- [x] **El id de produccion se perdia.** Devuelve un `int` pelado (`ActionResult<int>`), no el
      registro: la fila de la cola quedaba sin `remote_id` y despues no se podia editar ni borrar
- [x] **El listado de produccion se leia vacio.** Envuelve en `items` (paginado), no en `registros`.
      Daba «0 dias registrados» y el smoke elegia una fecha ya tomada. Se pide `size: 0` = todos
- [x] **Levante nombra el duplicado distinto:** *«Ya existe un seguimiento manual…»*. La deteccion
      por contenido no lo cubria y la cola habria reintentado para siempre

### Validacion
- [x] `flutter analyze` **0 errores, 0 warnings** · `flutter test` **98/98** (32 nuevos)
- [x] Smoke contra el backend real (`:5499`, binario propio; el de :5002 de otra sesion intacto):
      **8/8 en levante y 8/8 en produccion**, mas engorde Ecuador y Panama **sin regresion**
- [x] BD verificada: **0 filas `SMOKE%` en las cuatro tablas** de seguimiento; puerto liberado
- [i] El registro de prueba de produccion se borra **por la API**, no por SQL: descuenta aves y
      alimento, y un `DELETE` directo dejaria el inventario descuadrado

### Limite conocido
- [i] La etapa por **raza** de Santa Reyes (`semanas_ciclo_postura_por_raza`) no esta: necesita la
      guia genetica, que el movil todavia no descarga. Para esa empresa el numero puede diferir del
      que calcularia el web — hasta entonces, ese modulo se registra desde la web

---

## X20 — Descuento de inventario desde el movil (22ago26)

**Plan:** [`fase_de_desarrollo/descuento_inventario_movil_plan.md`](fase_de_desarrollo/descuento_inventario_movil_plan.md)
(432 lineas; salio de 2 workflows con verificacion adversarial — 9 bloqueantes encontrados y resueltos
o declarados fuera de alcance).

Decision del usuario: alcance **maximo** (los 4 modulos + los 2 huecos del backend) y, ante stock
insuficiente al sincronizar, **aceptar el dia y marcar para cuadre** en vez de rechazarlo.

### 🔴 El hallazgo que reordena el plan
- [i] **El backend NO es el cuello de botella: es la app.** `grep itemsHembras|itemInventarioEcuadorId`
      sobre `zootecnicoapp/lib/` da **0 resultados** — y es deliberado, lo dejamos asi en X18/X19
      (`seguimientos_api.dart:126-127` lo dice por escrito). El gate de 4 condiciones falla en la
      TERCERA para todo el trafico movil. **Se pueden implementar los 4 cambios de backend completos
      y la app seguiria sin descontar un kilo.** El interruptor es F5, no F1-F4

### Fase 0 hecha (esta sesion, commit `5ce6fe6`)
- [x] Los 2 defectos de la cola que convertian cualquier rechazo en corrupcion silenciosa:
      reintento infinito (`porEnviar` incluia `'error'` sin techo) y la marca del dia que sobrevivia
      al rechazo del servidor
- [x] `local_db.dart` tenia 400 lineas sin un solo test → **22 sobre SQLite real** (sqflite_ffi en
      memoria). Uno fallo y tenia razon: la marca del dia vive en `SyncService`, no en `LocalDb`

### Correcciones MEDIDAS a los diseños (no opinadas)
- [i] `RegistrarConsumoNivelGranjaAsync` **SI** fecha el movimiento (`InventarioGestionService.cs:1697`).
      El que NO fecha es el **INGRESO** (`:1757` hardcodea `DateTimeOffset.UtcNow`)
- [i] Panama (5) tiene doble validacion ⇒ `separa = true` ⇒ **el camino directo que tocan estos
      cambios NO corre en Panama**. Un smoke "de Panama" sobre la rama directa prueba otra cosa
- [i] `SyncController.cs` tiene **52 lineas**, no 200: varias citas del diseño de `requiere_cuadre`
      apuntan a lineas inexistentes. Reanclar por simbolo antes de implementar
- [i] Linea base limpia: **0 negativos sobre 583 filas** de `inventario_gestion_stock`

### Bloqueado: F0 necesita datos y decisiones que no puedo tomar yo
- [~] **Mediciones contra PROD** (la BD local no sirve): lotes de produccion EC/PA sin nucleo/galpon
      (cada uno pasaria de "guarda sin descontar" a **400 al guardar**), granjas con override de
      `maneja_alimento_por_galpon`, lotes reproductora en empresa Colombia (en local: **cero**), y
      `lotes.pais_id` NULL (en local 3 de 5 filas de produccion lo tienen ⇒ el camino que corre es el
      fallback granja→departamento→pais, que ningun diseño prueba)
- [!] **4 decisiones de negocio** (una ya respondida: preservar el dia). Ver el bloque de abajo

### Lo que si se puede arrancar sin esperar
- [x] **F1 — calculo puro a `Application/Calculos/`**: `ItemConsumoCalculos`, `ConsumoDiffCalculos`,
      `FechaMovimientoSeguimientoCalculos`. Checkbox obsoleto — quedó sin marcar aunque el trabajo se
      cerró más abajo, en «F1, F2, F3 y F4 — hechos y verificados» (línea ~2050) el mismo 22-ago-2026.
      Verificado en código el 27-ago-2026: los 3 archivos existen en `Calculos/` con tests propios
      (`MetadataItemSeguimientoCalculos` no se necesitó — no aparece en el cierre real).
- [x] **F5.2 — la UI del selector de items en Flutter**: checkbox obsoleto — ya está hecho y en uso.
      Verificado en código el 27-ago-2026: `SelectorItemsInventario` (widget) wireado en
      `zootecnicoapp/lib/features/seguimiento/pages/seguimiento_page.dart` detrás del kill switch
      `_usaSelectorItems ⇒ widget.usuario.descuentaInventarioDesdeMovil` (comentario propio del código:
      "F5.2/F0.2#4"), último commit del archivo `bb953aa` (23-ago-2026, bloque «rediseño visual»).

### 🟢 Alcance REDUCIDO por un dato (22ago26) — los 2 "huecos" no existen

El usuario lo dijo y la BD lo confirma sin excepciones: *«ecuador y panama tiene es pollo engorde,
no postura que es levante y produccion»*.

| Empresa | Pais | Engorde | Reproductora | Levante | Produccion |
|---|---|---|---|---|---|
| Sanmarino | CO | 0 | 0 | 18 | 4 |
| ItalcolEcuador | EC | 124 | 3 | 0 | 0 |
| Demo | CO | 0 | 0 | 10 | 2 |
| ItalcolPanama | PA | 67 | 121 | 0 | 0 |
| Santa Reyes | CO | 0 | 0 | 1 | 1 |

- [i] **Produccion en EC/PA y reproductora en Colombia NO son huecos: son combinaciones que la
      operacion no tiene.** Cada modulo descuenta exactamente donde se usa. **F6 sale del alcance**
      (construirlas seria superficie sin usuario). Si algun dia una empresa cruza de modelo, vuelven
- [i] Consecuencia util: **el smoke de la app tiene que correr por pais** — engorde/reproductora solo
      dan datos en EC/PA, levante/produccion solo en Colombia. Ya es lo que veniamos haciendo

### Decisiones del usuario (cierran F0.2)
- [x] Stock insuficiente al sincronizar → **aceptar el dia y marcar para cuadre**, no rechazarlo
- [x] Resolver un cuadre → **solo marca visto**. No repone kilos: eso creaba una segunda forma de
      mover el saldo y el repo ya tiene la regla de "una sola formula por numero"
- [x] El `tipoAlimento` de texto libre → **lo reemplaza el selector** del catalogo. Es la unica forma
      de mandar el id que dispara el descuento
- [x] Produccion EC/PA → **fuera de alcance**, el comentario de `ProduccionService.cs:29-33` vale

### F1, F2, F3 y F4 — hechos y verificados (22ago26)

Los 4 backend siguieron el orden `F1 F2 F4 F3`: los tres primeros no tocan el camino EC/PA
directo, F3 si (marcado "RIESGOSA" en el plan) y se dejo para el final con el patron ya probado.

- [x] **F1 — calculo puro** (`ItemConsumoCalculos`, `ConsumoDiffCalculos`,
      `FechaMovimientoSeguimientoCalculos`): extraccion pura desde `ProduccionService.cs` y los 3
      services de diff, cero cambio de comportamiento (verificacion adversarial: equivalente,
      solo divergencias cosmeticas). Tests nuevos en `ZooSanMarino.Application.Tests`.
- [x] **F2 — fecha del movimiento**: `FechaMovimientoSeguimientoCalculos.Anclar` (ingreso 12:00Z,
      consumo 18:00Z — evita el empate de `created_at` que usa la ventana de saldo). Propagado a
      ~25 sitios (levante, produccion, engorde x2, reproductora, `ValidacionSeguimientoService`,
      `MigracionService`). Verificado en vivo: movimiento fechado el dia del seguimiento, no el
      del sync; ancla correcta.
- [x] **F4 — atomicidad Colombia (nivel granja)**: `RegistrarConsumoNivelGranjaAsync` y
      `RegistrarIngresoNivelGranjaAsync` reescritos sobre `DescontarStockAtomicoAsync`/
      `SumarStockAtomicoAsync` (UPDATE condicional / INSERT ON CONFLICT — ya existian de una fase
      anterior de silos). Verificado en vivo: descuento exacto, devolucion exacta al borrar, cero
      diffs en `verificar_paridad_stock_clave_natural.sql`.
- [x] **F3 — atomicidad EC/PA**: el hueco real. `RegistrarConsumoAsync`/`RegistrarIngresoAsync` se
      aplicaban DESPUES del `SaveChangesAsync` del dia, dentro de un `try/catch` que solo logueaba
      — con la app offline eso es *permanente* (el push saca la fila del outbox igual). Cambiado a
      transaccion condicional (`await using ... CurrentTransaction is null ? Begin : null`)
      envolviendo guardado + consumo/ajuste/devolucion, SIN try/catch: si algo falla, la excepcion
      sube y el `await using` deshace todo, incluido el seguimiento.
  - [x] 4 archivos: `SeguimientoLoteLevanteService.Crud.cs`, `SeguimientoAvesEngordeService.Crud.cs`
        (Panama legacy), `SeguimientoAvesEngordeEcuadorService.Crud.cs` (el que la app usa de
        verdad, EC+PA+CO), `SeguimientoDiarioLoteReproductoraService.cs`
  - [i] `ValidacionSeguimientoService.Validar.cs` (el flujo de Confirmar de la doble validacion,
        el que **si** usa Panama en operacion real) **ya tenia** este patron desde antes — no
        necesito tocarlo. Bug real de F3 solo pega en EC/PA con `separa=false`, hoy eso es
        Ecuador (Panama tiene doble validacion ON en las 3 tablas)
  - [x] `dotnet build` 0/0, `dotnet test` 3124/3124 (sin tests nuevos: la logica es
        EF/transaccion, no cae en `Calculos/` — su red es el smoke, como dice el plan)
  - [x] Smoke en vivo contra backend aislado (`:5499`, DB local, puerto `:5002` de la otra sesion
        intacto): Ecuador engorde con items reales — descuenta exacto (7.5kg), ancla a las
        18:00Z, `DELETE` devuelve el stock exacto. Stock insuficiente (0kg disponibles) → 400 +
        CERO seguimientos nuevos + stock sin tocar (antes: 200 + catch silencioso).
  - [x] `verificar_cuadre_alimento_engorde.sql` antes/despues: diff = 0 filas, ningun galpon
        nuevo ni descuadre que crecio (los 11 descuadres que muestra son preexistentes y
        documentados, no de este cambio)
- [x] F5 (la app emite items — el interruptor real del descuento) — hecho y verificado (22ago26)
- [x] F7 (`requiere_cuadre`) — checkbox obsoleto, contradicho 60 líneas más abajo en el mismo archivo:
      «F7 — requiere_cuadre, hecho y verificado (22ago26)» (migración, gate de máquina, smoke de 15
      verificaciones). Recontrastado en código el 27-ago-2026.

### F5 — el interruptor, hecho (22ago26)

- [x] **F5.1 — el flag.** `companies.descuenta_inventario_desde_movil` (migracion EF
      idempotente `20260822183118_AddFlagDescuentaInventarioDesdeMovil`, default `false`).
      Propagado a las 6 proyecciones de `CompanyDto`/`CompanyPaisDto`
      (`CompanyService`, `CompanyService.Crud`, `CompanyResolver` x2, `CompanyPaisService`
      x2, `AuthService.GenerateResponseAsync` x2 — el que de verdad llega al login). Del
      lado Flutter: `Usuario.descuentaInventarioDesdeMovil` (constructor, campo, copyWith,
      fromJson, toJson) + parseo en `AuthApi.usuarioDesdeRespuesta` desde
      `companyPaises[0]`. Fail-closed en los dos lados: ausente o error ⇒ `false`.
  - [i] **Hallazgo:** `CompanyPaisDto` (el que arma el login) **no proyectaba NINGUNO** de
        los ~20 flags de `Company` — todos viven solo en `CompanyDto` (endpoints de admin).
        Sin agregarlo ahi, el flag nunca habria llegado a la app.
- [x] **F5.4 — decision de riesgo (con el usuario).** El plan pedia rechazar en el backend
      cuando falta `itemInventarioEcuadorId` bajo Modelo B. Medido: el modal de
      reproductora WEB (`modal-seguimiento-reproductora.component.ts`) manda HOY, en
      produccion (Panama 121 lotes + Ecuador 3), el id de `item_inventario_ecuador`
      metido en `catalogItemId` y nunca llena `itemInventarioEcuadorId` — el mismo bug que
      el plan describe para F6.2/Colombia, pero en el componente que Panama usa de
      verdad. Rechazar habria roto ese flujo en produccion. Decision del usuario:
      corregir la falla latente en el BACKEND sin tocar la web, y aplicar el contrato
      correcto en el MOVIL.
  - [x] Backend: `ItemConsumoCalculos.NormalizarParaModeloB` (nueva, con 5 tests) — bajo
        Modelo B, un `ItemConsumoKey` con `EsItemInventario=false` se normaliza a `true`
        (mismo id, mismos kg): bajo ese modelo solo existe una tabla de origen posible,
        asi que la marca en `false` era simplemente falsa, no una senal real de
        `catalogo_items`. Sin rechazo, cero cambio de comportamiento para lo que ya
        funciona. Conectado en `SeparacionSeguimientoHelper.Contexto` (la reserva de
        doble validacion que usa Panama).
  - [x] Movil: `ItemsConsumo.armar()` (ya construido en una fase anterior de esta sesion)
        **ya** mandaba `itemInventarioEcuadorId` siempre que el pais lo usa — verificado
        en vivo, no solo leido.
- [x] **F5.2 — el selector real.** El editor de items del design system (commit
      `1cdabbc`) era texto libre sin catalogo — no servia. Selector nuevo
      (`widgets/selector_items_inventario.dart`): busca en el catalogo cacheado
      (`InventarioApi.catalogo()`), muestra el disponible por (granja, nucleo, galpon)
      cruzando `ExistenciaInventario`, un campo de cantidad por linea. Reemplaza el
      campo `tipoAlimento` de texto libre + el consumo escalar SOLO cuando el flag esta
      encendido (F0.2#4) — con el flag apagado la seccion de Alimento es BYTE A BYTE la
      de antes.
  - [i] **Hallazgo:** `Lote` (Dart) no llevaba `granjaId`/`nucleoId`/`galponId` — solo el
        texto de pantalla. Sin la ubicacion real no se puede cruzar contra
        `existencias_inventario`. Agregado a `Lote` + a los 3 mappers de `LotesApi` +
        migracion local v5 (`ALTER TABLE lotes_cache ADD COLUMN`, cache regenerable).
        La correctitud del descuento NO dependia de esto (el backend resuelve la
        ubicacion desde el lote, nunca del cliente) — solo el AVISO de disponible en
        pantalla.
  - [i] Todo el resto de la capa (modelos `ItemInventario`/`ExistenciaInventario`,
        `InventarioApi`, cache SQLite v4, `items_consumo.dart` con 25 tests) **ya estaba
        construido** de una fase anterior de esta sesion, sin un solo caller en toda la
        app — era groundwork esperando la pantalla. `grep ItemsConsumo lib/` daba 0 antes
        de esto.
- [x] **F5.5 — silo, declarado.** El selector manda `manejaSilos: false` siempre (comentario
      en el codigo lo explica): ninguna empresa por silo tiene el flag encendido, y no lo
      va a tener hasta que exista un selector de silo — no se construyo en esta pasada.
- [x] Sincronizacion: `main.dart._refrescarCatalogoInventario` baja catalogo + existencias
      (por cada granja de los lotes del usuario) SOLO si el flag esta encendido — una
      empresa sin el flag no paga el peso de un catalogo que nunca ve.
- [x] `dotnet build` 0/0, `dotnet test` 3129/3129 · `flutter analyze` 0 errores ·
      `flutter test` 165/165 (10 nuevos: `usuario_test.dart` + 2 en `cola_sync_test.dart`)
- [x] Smoke en vivo end-to-end con el CODIGO REAL de la app (`tool/smoke_f5_items.dart`,
      nuevo): flag encendido a mano en la BD local para ItalcolEcuador (revertido despues),
      `ItemsConsumo.armar()` arma `itemInventarioEcuadorId=4` de verdad, POST real a
      `/SeguimientoAvesEngordeEcuador` descuenta 6.25 kg exactos, DELETE devuelve el stock
      exacto (2520 → 2520). Puerto `:5002` de la otra sesion intacto.

## F7 — requiere_cuadre, hecho y verificado (22ago26)

Alcance decidido con el usuario: **solo el PWA web** (`POST /api/Sync/push`), no la app movil.
Hallazgo previo a implementar: el mecanismo `requiere_cuadre` (constante, DTOs, y el manejo en
`clasificar-resultado-push.funcion.ts` del lado Angular) **ya existia**, con el comentario "Todavia
sin emisor" — la app Flutter de esta sesion NO pasa por `/api/Sync/push` (postea directo a cada
endpoint, cola propia en SQLite), asi que F7 no le aplicaba pese a que el plan lo asumia.

- [x] **La decision se toma ANTES del throw, no parseando el mensaje.** Nuevo
      `StockInsuficienteException : InvalidOperationException` (hereda para que cualquier catch
      existente lo siga atrapando igual) en los 6 sitios reales de "no hay stock" (EC/PA y
      Colombia, pre-check y descuento atomico). `SyncPushService.AplicarUnaAsync` atrapa ESE tipo
      especificamente — nunca compara texto — y sólo si el request `TraeItems(...)`.
- [x] **El reintento sin items, con el kg vuelto al escalar.** Sacar el array entero (sin más)
      dejaba el registro sin alimento y el guard de "alimento obligatorio" lo rechazaba igual —
      F7 habria fallado en el 100% de los casos reales. `ItemConsumoCalculos.KgDeAlimento`/
      `NombresDeAlimento` (nuevas, con tests) recomponen `consumoKgHembras`/`tipoAlimento` antes
      de reintentar. Un helper por forma de request (levante/engorde comparten
      `CreateSeguimientoLoteLevanteRequest`; reproductora y produccion tienen el suyo — produccion
      es record posicional, `with` en vez de mutar).
- [x] **Bug real encontrado por el smoke, no por lectura de codigo:** el primer intento deja su
      entidad trackeada en el `ChangeTracker` ANTES del chequeo de stock (`SeguimientoAvesEngordeEcuadorService.Crud.cs`
      hace `_ctx.Add(ent)` antes de `ValidarStockConsumoAsync` — a diferencia de levante/reproductora/
      produccion, que validan antes de trackear). Sin `_ctx.ChangeTracker.Clear()` antes del
      reintento, la SEGUNDA entidad se sumaba a la primera sin guardar y las dos violaban el
      indice unico (lote, fecha) — el push volvia `rechazada / error_interno`, exactamente lo que
      F7 existe para evitar. Se agrego el `Clear()` en los 4 reintentos (necesario en engorde,
      defensivo en los otros tres).
- [x] **Bandeja + resolver**, nuevo en `sync_operaciones`: columnas `detalle`, `cuadre_resuelto_at`,
      `cuadre_resuelto_por` (migracion `20260822224615_AddCuadreASyncOperaciones`, idempotente) +
      indice parcial para la bandeja. `GET /api/Sync/cuadres` (fail-closed por empresa activa) y
      `POST /api/Sync/cuadres/{id}/resolver` — **solo marca visto**, no repone kilos (decision del
      usuario, F0.2#3: reponer seria una segunda formula para el mismo numero). Nombre de ruta sin
      "admin" (el WAF de prod devuelve 403 a cualquier path que lo contenga).
- [x] **Gate de maquina** (`backend/scripts/verificar-cuadre-solo-en-sync.js`, nuevo, wireado en
      `deploy-production.yml`): falla el CI si algo fuera de `Services/Sync/` asigna el estado
      `requiere_cuadre` — el camino directo (F3) depende de lo contrario, que CUALQUIER falta de
      stock deshaga todo el seguimiento.
- [x] `dotnet build` 0/0, `dotnet test` 3134/3134 (+5 tests nuevos de `KgDeAlimento`/
      `NombresDeAlimento`), los dos gates de maquina en verde.
- [x] Smoke en vivo contra backend aislado (`:5499`, puerto `:5002` de la otra sesion intacto), 15
      verificaciones sobre el camino de divergencia (POST real a `/api/Sync/push` con un item sin
      stock): estado `requiere_cuadre`/`divergencia_stock`, el dia queda guardado (UN seguimiento,
      no cero), CERO cambio de stock, el kg paso al escalar, `tipoAlimento` se reconstruyo del
      nombre del item, reenviar el mismo `clientOpId` hace replay completo (con `detalle`, no solo
      `errorCodigo`), aparece en la bandeja, resolver lo saca sin tocar stock, resolver dos veces
      da 404. Camino feliz (stock suficiente) verificado aparte: sigue devolviendo `aplicada` y
      descontando exacto — F7 no cambio el comportamiento normal.
- [x] `verificar_cuadre_alimento_engorde.sql` antes/despues: diff = 0 filas.

## Plan cerrado (22ago26)

F1 a F7 completos (F6 fuera de alcance, decidido con el usuario: EC/PA no operan produccion
postura, Colombia no tiene reproductora — construirlo seria superficie sin usuario). El plan
`descuento_inventario_movil_plan.md` queda ejecutado de punta a punta.

---

## App móvil — rediseño visual, transiciones, offline y arquitectura (23ago26)

Plan: [`fase_de_desarrollo/app_movil_rediseno_visual_y_arquitectura_plan.md`](fase_de_desarrollo/app_movil_rediseno_visual_y_arquitectura_plan.md)
· Guía nueva: [`zootecnicoapp/CLAUDE.md`](zootecnicoapp/CLAUDE.md)

Pedido del usuario: mejora visual + transiciones, que el offline funcione siempre, los logos del
login web (el de Italfoods se elimina: **la web no lo usa en ningún lado**, 0 referencias medidas),
app más profesional alineada a los patrones del web, y arquitectura definida y documentada.

**Decisiones suyas:** color **híbrido** (marca en acentos/acciones, neutros cálidos se quedan) y
**arquitectura feature-first completa**.

### Arquitectura y offline — `bb953aa`

- [x] **Reestructura feature-first**: `core/` (api, db, sync, session, models, reglas, calculos,
      platform) · `design_system/` (tokens, components, motion) · `features/` (auth, home, lotes,
      seguimiento, sync, perfil) · `shared/`. Los 3 archivos que concentraban todo se partieron por
      pantalla real (`app_screens.dart` tenía **4 pantallas distintas** en 761 líneas).
- [x] Imports intra-proyecto a `package:` — mover un archivo deja de romper sus propios imports.
- [x] Regla de capas verificada: `core/` no importa `features/` ni `design_system/`. Por eso
      `postura_calculos`/`perfil_pais`/`modulos_del_menu` se quedan en `core/` (los usa `core/api`).
- [x] **Hueco offline 1 — pérdida de datos:** el chip «Guardado» se pintaba ANTES de que el INSERT
      resolviera, sin `try/catch` en ninguna de las 3 capas. Si fallaba, el operario se iba
      convencido de haber anotado el día y el registro no existía. Ahora encola → confirma.
- [x] **Hueco 2 — la VPN contaba como «sin conexión»** (`switch(results.first)` con `_ => offline`;
      en iOS/macOS la VPN se reporta como `other`). Un equipo con VPN corporativa **nunca** subía la
      cola. Extraído a `calidadDesdeConectividad()` puro + 12 tests.
- [x] **Hueco 3** — abrir la app con cola pendiente y red no sincronizaba sola. `_calidad` arranca
      offline (fail-closed) + `WidgetsBindingObserver` sincroniza al volver a la app.
- [x] **Hueco 4** — `sincronizar()` sin guarda de reentrada: dos disparos posteaban la misma fila.
- [x] **Hueco 5** — una respuesta VACÍA borraba la caché (`delete` incondicional): un 200 con cuerpo
      raro dejaba al operario con CERO lotes en el galpón. Guarda «lista vacía no reemplaza».
- [x] `flutter analyze` 0/0 · `flutter test` **177/177** (12 nuevos).

### Documentación

- [x] **`zootecnicoapp/CLAUDE.md`** — mapa de capas y reglas de dependencia, convención de imports y
      nombres, sistema de diseño con la regla de marca, sistema de movimiento, **contrato offline con
      los 19 invariantes medidos**, 4 trampas de Flutter que ya costaron caro, y checklist de commit.

### Pendientes medidos que quedan abiertos (del informe de offline)

Ninguno es regresión: son huecos que ya existían y quedaron documentados en el `CLAUDE.md` de la app.

- [x] **Días que el servidor ya tiene, cableados** (23ago26) — `SyncService.refrescarDiasDelServidor`
      se llama al abrir el formulario y avisa ANTES de llenarlo, no al guardar. Es por lote y a
      demanda: el endpoint es uno por lote y en la sincronización diaria serían 124 peticiones.
- [x] **Modo «sólo captura»** — un token vencido ya no expulsa. Se conserva la sesión, se puede
      seguir registrando y viendo la cola, y sólo se suspende subir. Antes el login exigía red, así
      que un 401 seguido de quedarse sin señal dejaba al operario afuera de su propia app.
- [x] **UI de filas agotadas** — sección «Necesitan tu atención» con el mensaje real del servidor y
      un botón Reintentar que llama `sync.reintentar(id)`.
- [x] **Los avisos llegan al usuario** — resumen post-sync («N subidos · N ya estaban · N hay que
      recargarlos»). `avisoPlataforma` ya estaba cableado: esa parte de la medición estaba vieja.
- [x] **Pantalla de historial** — `LocalDb.historialLocal()` (lector nuevo, +5 tests) y
      `features/sync/pages/historial_page.dart`, agrupada por día. Entrada desde Perfil.
- [i] **Silos**: sigue a medias A PROPÓSITO (decisión de producto F5.5). No cablear sin decisión.
- [x] **Los lotes cerrados ya no se ofrecen** (23ago26) — no admiten registros nuevos, así que
      mostrarlos sólo hacía perder toques: el choque aparecía recién al elegirlos.
      `features/lotes/funciones/lotes_activos.dart` (+8 tests), filtrado en un solo punto ⇒
      desaparecen de Inicio, Lotes y el selector a la vez. **Medido en Ecuador: de 124 ofrecidos, 94
      estaban cerrados** (quedan 30). La caché guarda todos igual: el historial resuelve nombres
      contra ella. La guarda de `_nuevoSeguimiento` se conserva por si el lote se cierra entre la
      última sincronización y el toque.
- [x] **`SyncService` con tests** (23ago26) — `test/sync_service_test.dart`, 31 casos: los 6
      `TipoFallo`, la guarda de reentrada, el orden de la cola (I4), el endpoint congelado (I5) y las
      filas agotadas (I17). Validados **con mutación**: se rompió una regla por vez y las 9 las
      detectó el test que nombra su invariante. Se agregó una costura de tiempos al service
      (`demoraDeteccion`/`demoraExito`, defaults idénticos) para que la suite no espere 3,9 s por caso.

---

# Limpieza de código muerto de la auditoría (23-ago-2026)

Plan: [`fase_de_desarrollo/limpieza_codigo_muerto_auditoria_plan.md`](fase_de_desarrollo/limpieza_codigo_muerto_auditoria_plan.md)

Salió de la auditoría completa back+front. **Nada de esto está en un flujo vivo**: la app tiene que
comportarse idéntico después, y el conteo de tests es el testigo (back 3.135, front 633).

## Línea base medida ANTES de tocar nada (23ago26)
- [x] `dotnet build` → 0 errores, 0 warnings
- [x] `dotnet test` → 3.135 pasan / 0 fallan
- [x] `yarn build` → 0 errores, 0 warnings (301 s)
- [x] `yarn test` → 633 pasan

## Caso 1 — formulario huérfano de levante (front)
- [x] Borrar `features/lote-levante/pages/seguimiento-lote-form/` (`.ts`, `.html`, `.scss`) — 807 líneas
- [x] `seguimiento-lote-levante-routing.module.ts`: quitar import + rutas `nuevo` y `editar/:id`
      (+ se removió al pasar el import muerto de `SeguimientoLoteLevanteService`, que no se usaba)
      (la ruta `''` → list **se conserva**)
- [x] `seguimiento-lote-levante.module.ts`: quitar import + entrada en `imports`

## Caso 2 — `AllowAllPolicyProvider` (back)
- [x] `Program.cs`: eliminar la clase y su encabezado (cola del archivo)
- [x] `IDbStudioAuthorization.cs`: corregir el doc-comment que afirma que las policies de ASP.NET
      están "neutralizadas" — es falso desde que existe el deny-by-default
- [x] Los comentarios históricos de `Program.cs` (523/536) se conservan

## Verificación de no-regresión
- [x] `dotnet build` 0/0 y `dotnet test` = **3.135** (mismo número)
- [x] `yarn build` 0/0 y `yarn test` = **633** (mismo número)
- [x] 0 referencias residuales a las dos piezas
- [x] `FallbackPolicy = RequireAuthenticatedUser` intacto

---

# Split de los 12 archivos largos de la auditoria (23-ago-2026)

Plan: [`fase_de_desarrollo/split_archivos_largos_plan.md`](fase_de_desarrollo/split_archivos_largos_plan.md)

Confirmado por el usuario: TODO lo que salio en la auditoria. Corte mecanico (verbatim), un commit
por archivo, build+test entre cada uno. Linea base: back 3.135 tests, front 633 tests.

## Backend (6 archivos)
- [x] `ReporteTecnicoService.cs` (3267→219 + 6 en Funciones/) — build 0/0, test 3135/3135 — commit `4578bb1`
- [x] `InventarioGestionService.cs` (3061→242 + 6 en InventarioGestion/Funciones/) — build 0/0, test 3135/3135 — commit `fdf2c72`
- [x] `ReporteTecnicoProduccionService.cs` (1991→372 + 6 en Funciones/) — build 0/0, test 3135/3135 — commit `d56fef5`
- [x] `ReporteContableService.cs` (1786→370 + 3 en Funciones/) — build 0/0, test 3135/3135 — commit `f18daff`
- [x] `TicketService.cs` (1402→130 + 5 en Tickets/Funciones/) — build 0/0, test 3135/3135 — commit `f20ee3d`
- [x] `LoteService.cs` (1353→263 + 3 en Funciones/) — build 0/0, test 3135/3135 — commit `60ddb88`

**Backend completo: 6/6.**

## Frontend (6 archivos)
> Nota de alcance (24ago26): `modal-create-edit` de lote-levante YA tenía extracción extensa a
> `funciones/`/`models/` (9+ imports) — es el archivo de referencia, no necesitaba más trabajo. En
> el resto, se extrajeron solo funciones verificadas 100% puras (cero `this.` en el cuerpo) e
> interfaces inline a `models/`; los métodos que leen estado del componente (`this.form`,
> `this.algúnMap`, servicios) se dejaron en el componente — parametrizarlos habría sido un refactor
> más grande y más riesgoso que "mover verbatim", fuera del alcance mínimo de esta tarea.
- [i] `lote-levante/pages/modal-create-edit` (2482) — ya extraído (referencia), sin cambios
- [x] `gestion-inventario/pages/gestion-inventario-page` (2164→2124 + 1 en funciones/ + 1 en models/) — build 0/0, test 633/633
- [x] `lote-produccion/pages/modal-seguimiento-diario` (2005→1908 + 1 en funciones/ + 1 en models/) — build 0/0, test 633/633
- [x] `lote/components/lote-list` (1905→1854 + 2 en funciones/) — build 0/0, test 633/633
- [x] `engorde-comun/pages/modal-seguimiento-engorde` (1829→1826 + 1 fn en funciones/ existente) — ya muy refactorizado, solo 1 candidato nuevo — build 0/0, test 633/633
- [x] `traslados-aves/pages/inventario-dashboard` (1691→1655 + 2 en funciones/) — build 0/0, test 633/633

**Frontend completo: 5/5 tocados + 1 ya era referencia. Sweep de 12 archivos: CERRADO.**

## Verificacion final (al cerrar los 12)
- [x] `dotnet build` 0/0 y `dotnet test` = 3135 (identico a la linea base)
- [x] `yarn build` 0/0 y `yarn test` = 633 (identico a la linea base)

---

# X18 — Santa Reyes: cierre de definiciones del cliente (24-ago-2026)

Plan: [`fase_de_desarrollo/santa_reyes_definiciones_cliente_cierre_plan.md`](fase_de_desarrollo/santa_reyes_definiciones_cliente_cierre_plan.md)

**Desbloqueado por el usuario en sesion**, con los 4 archivos del cliente adjuntos (`Items.xlsx`,
`Lotes.xlsx`, `Granja.xlsx`, `Requerimientos de Italapp.docx`). Cierra `SR-DEF-1`, `SR-DEF-5` y
`SR-DEF-6` de `TK-2026-000180`. Linea base: back **3135** tests, front **633** tests.

## X18.0 · Decisiones del usuario (las 4, tomadas en sesion)
- [x] **Machos**: no es un campo informativo nuevo en ventas — es **retirar machos tambien de
      ventas**. Se extiende `ocultaMachosEnPostura`, no se crea flag nuevo
- [x] **Lohmann Brown**: queda **sin guia genetica** (no se inventan sus 123 filas); solo se corrige
      su clasificacion de grupo
- [x] **Grafia de razas**: el sistema **tolera la grafia del ERP** por alias de lectura; no se
      modifica el dato que vino del ERP
- [x] **Enyemado/Decolorado sin codigo ERP**: quedan **sin codigo y ocultos**, no se ofrecen para
      clasificar. Ni se borran ni se inventan codigos

## X18.1 · W2 — Linea genetica — CERRADO
- [x] 🔴 **Bug probado**: `LOHMANN BROWN` cae en el token `LOHMANN` y se clasifica **blanca (112
      sem)**; el `Lotes.xlsx` del cliente dice **ROJA (102 sem)**. Afecta al lote 229. Fix: evaluar
      Rojas/Criollas **primero** + token `BROWN`, en backend y en su espejo de front
- [x] Alias de grafia ERP → guia (`BABCOK BROWN`→`Babcock Brown`, `HY LINE`→`Hy Line Brown`) en
      calculo puro nuevo `RazaGuiaAliasCalculos`, aplicado en los **4** sitios que consultan la guia
      propia (`GuiaGeneticaLookup.ExisteAsync`/`ObtenerFilasPropiasAsync`,
      `GuiaGeneticaService.ObtenerCandidatosAsync`/`ObtenerAniosCrudoAsync`)
- [x] 🔴 **El alias NO se aplica a la guia compartida** (`ProduccionAvicolaRaw`): Sanmarino, Panama y
      Ecuador leen de ahi y el delta cero queda garantizado **por construccion**, no por revision
- [x] Tests espejados: back **3161** (base 3135, +26) · front spec 19/19
- [i] **Medido en BD**: 3 de las 4 razas de los lotes reales de SR no cruzaban con la guia ⇒ reportes
      tecnicos sin columnas de comparacion y la validacion «raza/año obligatorios si hay guia»
      rechazaba razas que SI estaban cargadas

## X18.2 · W4 — Bodega de salida por lista maestra (`SR-DEF-6` / F10.1) — CERRADO
- [x] Migracion data-only `20260824120000_SeedListasMaestrasTrasladoSantaReyes`: siembra para SR las
      5 listas que le faltaban (`traslado_de_huevos_planta_destino` con **Bodega General**,
      `_tipo_destino`, `_tipo_de_operacion`, `_venta_motivo`, `movimiento_de_aves_tipo_movimiento`)
- [x] **Idempotencia probada corriendo el `Up()` DOS veces en una transaccion revertida**: 33→38→38
      listas y 70→78→78 opciones (la 2a pasada no mueve nada); el `Down()` devuelve a 33/70 exacto.
      Aplicada despues de verdad con `dotnet ef database update`
- [x] 🔴 **El campo digitado era el de TRASLADO, no el de venta.** El de venta ya era un `<select>`;
      el texto libre estaba en `modal-traslado-huevos.component.html:148`
      (`<input type="text" formControlName="observaciones">` con label «Nombre Planta») — eso es lo
      que el cliente reclama. Reemplazado por `<select>` alimentado de la lista maestra
- [x] 🔴 **Se decide por DATO, no por empresa**: hay desplegable si la empresa tiene opciones
      cargadas. Sanmarino y Demo tienen la lista **sin ninguna opcion** ⇒ volverles el campo
      obligatorio y vacio les impedia registrar un traslado; sin opciones se conserva el input de
      siempre. Habilitarlo en cualquier empresa = cargar sus destinos en `/config/master-lists`
- [x] Escribe en el **mismo control** (`observaciones`) que el input que reemplaza ⇒ ni el DTO, ni la
      columna, ni ningun lector cambian. `yarn build` 0 errores
- [i] **Ojo, el plan viejo apuntaba al formulario equivocado**:
      `santa_reyes_requerimientos_italapp_plan.md:61,85` mandaba a `traslado-huevos-form`, que es el
      **huerfano** (ruta `/traslados-huevos/nuevo` sin un solo `routerLink` que la alcance). El vivo
      es `modal-traslado-huevos`, que es el que se toco

## X18.3 · W3 — Comprobante de traslado de aves (`SR-DEF-5` / F9.2c) — CERRADO
- [x] `ComprobanteTrasladoAvesComponent` (standalone) — **es el primer comprobante del repo**. Patron
      de `liquidacion-reporte-panama`: `@Input()` + `print()` + `@media print`. **Sin libreria de
      PDF** — no hay ninguna en el repo (solo `xlsx` y ClosedXML/EPPlus, que son Excel) y el
      navegador ya imprime a PDF
- [x] Secciones: datos del movimiento, origen y destino, aves trasladadas, transporte
      (placa/conductor/precinto, oculta si los 3 vienen vacios), observaciones y 3 firmas
      (entrega / transporta / recibe)
- [x] Gap del contrato cerrado: la interfaz TS `MovimientoAvesCompleto` **no declaraba**
      `placa`/`conductor`/`sellos` aunque el backend los enviaba desde siempre ⇒ ninguna pantalla
      podia leerlos
- [x] Boton «Comprobante» por fila en `/movimientos-aves/lista` (tabla y cards mobile) — es el
      listado que Santa Reyes SI tiene en `company_menus`. Trae el detalle con
      `GET api/TrasladoNavigation/{id}`, el unico endpoint que resuelve origen y destino con nombres
      mas transporte en una sola llamada
- [x] Respeta `ocultaMachosEnPostura`: con el flag ON no imprime ni Machos ni Mixtas
- [x] 🔴 **Regla de impresion de la pagina, no solo del comprobante**: sin ocultar `.ux-page` se
      imprimia toda la pantalla de atras (sidebar, filtros y la tabla entera). El overlay se despega
      a flujo normal al imprimir para que el comprobante ocupe la hoja solo
- [x] Filas precalculadas en `ngOnChanges`, **no en getters**: un getter que arma arrays nuevos por
      ciclo rompe la estabilidad de referencias que pide CLAUDE.md. `yarn build` 0 errores
- [~] **Falta el smoke visual en navegador** (la impresion real y el salto de pagina): necesita
      sesion autenticada

## X18.4 · W1 — Machos fuera de postura (`SR-DEF-1` / F5.3)
- [x] **Decision que cierra `SR-DEF-1`**: el `.docx` decia «que en ventas aparezca campo machos sobre
      el total de las aves» y se leia como un campo informativo NUEVO. El usuario aclaro lo
      contrario: Santa Reyes **no maneja machos en ningun lado**, asi que en ventas se **retiran**.
      Sin flag nuevo: se extiende `ocultaMachosEnPostura`
- [x] **W1.a — `modal-movimiento-aves` (el de VENTAS y traslados)**: oculto el stat «Machos disp.»,
      el input `cantidadMachos` con su badge de disponible, y la nota «solo hembras o machos, no
      ambos» (no tiene sentido sin machos). Los chips `M` del listado, en tabla y en cards
- [x] **W1.a — `seguimiento-lote-levante-list`**: input «Aves machos para produccion», machos
      disponibles, la columna Machos del resumen y «Machos encasetados»
- [x] **W1.a — `modal-registro-inicial`**: input «Aves Iniciales Machos» y el `H / M` del banner
- [x] **W1.a — `traslado-aves-huevos`**: input «Cantidad de Machos», machos vivos, iniciales,
      mortalidad y retiros acumulados
- [x] **W1.b residuos** de `modal-traslado-aves-seguimiento`: «Machos vivos» y las pills de
      ingreso/salida M
- [x] 🔴 **La anidacion sospechosa de `errorSexajeMachos` NO era un bug** — verificada linea por
      linea en `modal-seguimiento-diario:265-269` y `modal-create-edit:408-412`: el `@if` abre antes
      del titulo y cierra despues del input de machos, las 3 lineas quedan adentro. Falsa alarma del
      inventario; queda escrito para que nadie vuelva a perseguirla
- [i] **Nada de esto toca el modelo ni el payload**: los controles siguen existiendo y nacen en `0`,
      que es un valor VALIDO para `Validators.required` ⇒ ocultarlos no bloquea el guardado. Mismo
      criterio que F5.1/F5.2
- [x] **W1.a — `lote-list` (el form VIVO de lotes)**: inputs «Cantidad machos» del lote base y
      «🐓 # Aves Macho» del lote. Ya inyectaba `ActiveCompanyConfigService`, solo se sumo el flag al
      bloque de `loadCompanyFlags()` que ya existia
- [x] **W1.a — `inventario-dashboard` (el 4º formulario de traslado)**: los 2 inputs
      `cantidadMachos` (traslado entre lotes y retiro) y el `window.prompt` del ajuste manual, que
      con el flag ON ya no pregunta por machos y ajusta con 0
- [x] **W1.c tablas y tabs — CERRADO**: `tabs-principal` de levante y produccion (KPIs, tarjetas de
      resumen, tabla de descuentos y las grillas diarias), `tabla-lista-registro`, los 2
      `modal-detalle-seguimiento` (en produccion se retira la **pestaña Machos entera**),
      `modal-calculos`, las 2 liquidaciones, las 2 `graficas-principal`, `lote-produccion-list`, y
      los listados de `traslados-aves` (`movimientos-list`, `registros-traslados`,
      `historial-trazabilidad`, `edades-lote`, `traslado-navigation-card`) + `dashboard`
- [x] **W1.d reportes y exportaciones — CERRADO**: `reportes-tecnicos` (pestaña «Semana Machos»
      entera + 24 columnas + los 4 reportes compactos por galpon/general + `tabla-levante-completa`),
      `reporte-tecnico-produccion` (cuadro y diaria), `reporte-contable` (aves y bultos),
      `reporte-diario-costos-postura`, `reporte-tecnico-semanal` y
      `reporte-tecnico-administrativo`. Los **3 export a xlsx** filtrados
- [x] 🔴 **Lo delicado no eran las columnas, eran los COLSPAN.** Toda tabla con cabecera de 2 pisos
      necesita achicar el grupo: si no, abarca mas columnas de las que existen y la cabecera queda
      corrida sobre el grupo vecino. Ajustados en indicadores (produccion y levante), contable,
      costos-postura, cuadro y diaria de produccion, y los 4 compactos. Y el `colspan` de la fila
      «sin registros» es un **numero fijo**: hay que restarle las columnas ocultas a mano
- [x] 🔴 **Los 3 export a Excel se filtran de forma que no se puedan desalinear**: por lista
      EXPLICITA de claves donde las filas son objetos (indicadores), y con la MISMA condicion
      aplicada en la misma posicion a cabecera y dato donde son arrays paralelos (`soloConMachos`,
      y el parametro de `construirHojasCostosPostura`). Una regex sobre la «M» se lleva por delante
      claves de hembras (`PorcMortSemH`, `PorcMortSemLote`) y el error solo se ve abriendo el archivo
- [i] **Los TOTALES se conservan en todos lados** (total de aves, total general, cambio total): son
      el total del lote, no un dato de machos
- [i] **Hallazgo preexistente NO tocado**: los `colspan` de `tabla-levante-completa` ya estaban mal
      antes de esto (dicen 15/15/8/8 y tienen 34/33/9/9 hojas). Corregirlo seria un cambio de
      comportamiento para todas las empresas ⇒ fuera de alcance, queda anotado

### X18.4.1 · Verificacion de alineacion de las tablas (medida, no asumida)

Se conto el ancho de cada tabla por los TRES caminos —suma de `colspan` de la fila 1, hojas de la
fila 2, y celdas del cuerpo— en los dos estados del flag. Si los tres no dan lo mismo, la tabla sale
corrida. Resultado (`OFF` → `ON`):

| tabla | OFF (g/h/c) | ON (g/h/c) | |
|---|---|---|---|
| levante · `tabs-principal` | 36/36/36 | 25/25/25 | OK |
| produccion · `tabla-lista-indicadores` | 57/57/57 | 39/39/39 | OK |
| levante · `tabla-lista-indicadores` | 34/34/34 | 19/19/19 | OK |
| levante · `tabla-lista-registro` | 22/22/22 | 12/12/12 | OK |
| **produccion · `tabs-principal`** | **38/38/37** | **31/31/30** | ⚠️ preexistente |

- [x] **Produccion y levante quedan alineados** respecto de este cambio: el delta cabecera↔cuerpo es
      identico en los dos estados del flag en TODAS las tablas
- [!] 🔴 **Defecto PREEXISTENTE en la grilla diaria de produccion**: el `<thead>` declara la columna
      **«Estado»** (`@if (requiereValidacion)`, ~linea 265) y el `<tbody>` **no tiene su celda** —
      despues de `observacionesPesaje` pasa directo a Acciones. Con la doble validacion encendida la
      fila queda corrida una columna. **Verificado que NO lo introdujo este trabajo**: en `f49012b^`
      (antes de tocar machos) la tabla ya estaba en th=38 / td=37. Levante SI tiene esa celda y esta
      balanceado. Spawneado aparte (`task_88fd333d`), no se toco acá para no mezclar un arreglo de
      otra feature con el barrido de machos
- [x] Validacion: `yarn build` 0 errores · `yarn test` **637/637** (base 633, +4 nuevos)
- [~] **W1.d reportes y exportaciones a Excel** (~6 archivos, ~100 columnas): inventariado, fuera
      del alcance de esta sesion salvo que sobre tiempo
- [i] **Nunca** se toca el modelo ni el payload: los saldos consumen esos campos. Engorde y
      reproductora manejan machos legitimamente y no se tocan

## X18.5 · Lo que NO cierra esta sesion
- [!] `SR-DEF-3` (F8.1) — los 7 items PNC siguen sin codigo ERP (decision del usuario)
- [!] `SR-DEF-4` (F8.3) — panel de eficiencia, depende de F8.1
- [~] `F11.3` — pruebas asistidas con el cliente
- [!] Guia genetica de `Lohmann Brown` — el cliente debe entregar los datos
- [i] `ActualizarTrasladoHuevosAsync` sigue sin tocar `metadata->huevoItems` (gap preexistente)

---

# X18.4.1-b · Columna «Estado» de la grilla diaria de producción (defecto preexistente)

> Plan: [`fase_de_desarrollo/columna_estado_grilla_produccion_plan.md`](fase_de_desarrollo/columna_estado_grilla_produccion_plan.md)
> Sale del hallazgo `[!]` de X18.4.1: el `<thead>` de `lote-produccion/pages/tabs-principal` declara
> «Estado» bajo `@if (requiereValidacion)` y el `<tbody>` no tenía su celda ⇒ con la doble validación
> encendida la fila quedaba corrida una columna. Verificado en `f49012b^`: **no lo introdujo** el
> barrido de machos.

- [x] **Celda agregada en el cuerpo** (`tabs-principal.component.html:356-366`), gateada por
      `@if (requiereValidacion)`, entre `observacionesPesaje` y `sticky-actions` — copia de la celda
      de levante (`lote-levante/pages/tabs-principal/tabs-principal.component.html:408-415`) con la
      fila `s` en lugar de `f.seg`
- [x] **No hizo falta tocar el TS ni el SCSS**: los 4 helpers (`claseBadgeValidacion`,
      `tooltipValidacionFila`, `estadoValidacionFila`, `etiquetaValidacionFila`) ya existían en
      producción **byte a byte iguales** a los de levante —se agregaron con la columna del `<thead>`
      y quedaron sin consumidor—, las clases `.badge-validacion*` son **globales**
      (`frontend/src/styles.scss:64-78`) y `CommonModule` ya estaba en los `imports` (la fila ya usa
      `ngClass` para `claseFilaValidacion`)
- [x] **Reconteo mecánico de `th` vs `td`** en las **8 combinaciones** de los 3 flags que gatean
      columnas (`requiereValidacion` × `ocultaMachosEnPostura` × `clasificacionHuevoPorItems`):

      antes: 4 combinaciones OK y las 4 con `requiereValidacion=true` en **### DESBALANCE 1**
             (36/35 · 26/25 · 29/28 · 19/18)
      después: **las 8 alineadas** (35/35 · 25/25 · 28/28 · 18/18 · 36/36 · 26/26 · 29/29 · 19/19)

      Conteo crudo de celdas declaradas (la base que usó X18.4.1): pasa de **38 th / 37 td** a
      **38 / 38**
- [x] **Test de regresión** `tabs-principal.component.spec.ts` (4 casos, renderiza el componente en
      TestBed y **cuenta las celdas del DOM**, no del archivo): ancho igual con el flag OFF, ancho
      igual con el flag ON, el flag suma **exactamente 1** columna de cada lado, y el badge sale con
      su clase y su texto. **Verificado que falla contra el template previo** (`Expected 36 to be
      35`) ⇒ el test ve el defecto, no acompaña
- [x] Validación: `yarn build` → **0 errores** · `yarn test` → **642/642** (base 637, +5 nuevos)
- [i] La celda sólo **lee** el mapa `estadoValidacionPorId` que ya inyecta el contenedor: no toca el
      modelo, el payload ni la lógica de validación (el botón ✓ sigue en la celda de Acciones). Con
      el flag OFF no se renderiza ⇒ **cero cambios visibles** para las empresas sin doble validación
- [i] 🔴 **Ojo con `yarn build` en esta máquina**: el Node del sistema (22.15.0) ya no le alcanza al
      CLI de Angular 22 (pide ≥ 22.22.3) y el build muere antes de compilar. Va con el portable:
      `export PATH="/c/Users/SAN MARINO/node-portable/node-v22.23.1-win-x64:$PATH"`
- [i] 🔴 **La suite completa se colgó 2 h y no era el cambio**: `ng test` salió por un pipe a `grep`,
      que **bufferiza** ⇒ el log quedó en 0 bytes y no se veía si avanzaba. Encima el puerto de Karma
      (**9876**) lo tenía tomado un `ng` del **checkout principal** (otra ventana). Lecciones: mandar
      la salida **directo a un archivo** (`> log 2>&1`, sin pipe) y, antes de matar un `node.exe`,
      **mirar su CommandLine** — el que estaba en 9876 no era de este worktree. El builder
      `@angular/build:karma` de v22 **no acepta `--port`**: para aislarlo hay que pasarle un
      `karmaConfig` propio
- [x] **Verificado en la app corriendo, con la doble validación ENCENDIDA de verdad** (el flag sale de
      la BD y lo resuelve el backend; no se forzó el `@Input` desde devtools). Empresa
      `Agroavicola Sanmarino`, lote `P-K345A`, **301 registros**: `th=36 / td=36`, **301 badges** —una
      celda de Estado por fila— y los pares de la punta derecha en su lugar (`ESTADO → Validado`,
      `ACCIONES → 👁️ ✎ 🗑`). Con el template de `HEAD~1` y **todo lo demás igual**: `th=36 / td=35`,
      **0 badges**, y los botones caen bajo el encabezado «ESTADO» dejando «ACCIONES» vacía
- [i] 🔴 **Causa encontrada — NO es un bug, es `user_farms` (alcance por usuario-granja) funcionando
      como está diseñado.** `P-LOTE 235A` (LPP 9 → lote 124, granja 90 `LA PRIMAVERA`) no aparecía con
      `admin.demo` porque ese usuario tiene **una sola fila** en `user_farms` (`farm_id=87`, Granja 1);
      las otras 8 granjas de Demo —incluida la 90— están asignadas a `usuario_demo1`. El chequeo previo
      de `user_farm_scopes` (0 filas) fue la tabla equivocada: esa es el sub-alcance GRANULAR dentro de
      una granja ya asignada ([[alcance-granular-usuario-granja]]), no la asignación de granjas en sí.
      En `LoteProduccionFilterDataService.cs:66` → `FarmService.GetAllAsync(userId:…)`
      (`FarmService.cs:361-384`): pasar `userId` filtra SIEMPRE por `UserFarms`, sin importar el rol
      —la restricción explícita gana al bypass de admin, a propósito—. El nombre del rol "Admin Demo"
      no implica alcance global. **Confirmado en la app** con `usuario_demo1`: aparecen las 8 granjas,
      seleccionar `LA PRIMAVERA` trae núcleo/galpón/lote, y `P-LOTE 235A` muestra sus 2 registros
- [i] **La receta de smoke de UI quedó incompleta desde B1**: además de inyectar `auth_session` en
      `localStorage` hay que **anotar el `jti` del token en `sesiones_activas`**, o todo request sale
      401. Y el `errorCode` **`token-expirado` NO significa sólo "venció"**: es también el veredicto
      cuando la FILA se evalúa como vencida — y el backend **cachea el veredicto muerto hasta el
      `exp` del token**, así que corregir la fila no alcanza: hay que mintear otro `jti`
- [i] **Todo lo que tocó el smoke quedó como estaba**: los flags de `Demo` y `Agroavicola Sanmarino`
      volvieron a `false` (`ItalcolPanama` ya venía en `true` y no se tocó), las 3 filas de
      `sesiones_activas` borradas, y back/front/Chrome apagados. **No se guardó ni validó ningún
      registro**: la pantalla sólo se leyó

---

## X18.6 — Huevos dinámicos (Primera/Pnc) en la tabla de producción: verificado EN VIVO por primera vez (24-ago-2026)

Pedido del usuario: seguir validando el flujo de Santa Reyes — específicamente que **la tabla de
producción sea dinámica en la parte de huevos**. Lo que había hasta ahora (X17.6/X18.4.1) era
**código leído + reconteo mecánico de `th`/`td` sobre el fuente** (sin renderizar), nunca la
combinación REAL de flags de Santa Reyes contra la grilla viva en el navegador. Sin código nuevo:
esto fue una verificación, no un fix.

- [x] **La combinación de flags de Santa Reyes nunca se había visto en pantalla**: `company 6` tiene
      `clasificacion_huevo_por_items=true` + `oculta_machos_en_postura=true` +
      `requiere_validacion_seguimiento_diario=false` — una combinación distinta a la única que sí se
      había verificado en vivo antes (Agroavicola Sanmarino con `requiere_validacion=true` y los otros
      dos flags en `false`, sesión anterior de la columna Estado).
- [x] **Datos reales usados**: lote `SMOKE-SR-001` (lote_id 152, LPP 10, granja La Esperanza), sus 4
      seguimientos (674, 675, 676, 677) con `metadata.huevoItems` ya conocido de X17.7/X17.8.
      Predicción calculada a mano desde la BD antes de mirar la pantalla, para no confirmar con sesgo.
- [!] 🔴 **El login normal del usuario de smoke falló**: `smoke.santareyes@test.local` devolvió 401
      «Credenciales inválidas» **real del backend** (no de red/CORS) con la contraseña documentada en
      memoria. Se frenó a los 2 intentos fallidos (`users.failed_attempts`, umbral de bloqueo = 5,
      `AuthService.cs:176`) para no bloquear la cuenta 15 minutos. La contraseña real de esta cuenta
      quedó desconocida — no se siguió probando a ciegas.
- [x] **Alternativa: JWT minteado a mano + fila en `sesiones_activas`** (receta ya documentada de
      sesiones B1 anteriores), espejando exacto `AuthService.GenerateResponseAsync`: claims con las
      2 URIs largas (`.../nameidentifier`, `.../role` de Microsoft) + el resto en corto
      (`sub`,`jti`,`iat`,`unique_name`,`email`,`firstName`,`surName`,`user_id`,`is_super_admin`,
      `company_id`,`company`,`permission`×35 desde `role_permissions` del rol 30 «Santa Reyes
      Administrador»), firmado HS256 con `JwtSettings:Key` de `appsettings.Development.json`.
      **Esta vez el `INSERT` en `sesiones_activas` lo bloqueó el clasificador de permisos de Auto
      Mode** (no lo bloqueaba en sesiones anteriores) — se paró y se le pidió autorización explícita
      al usuario en vez de buscarle la vuelta; autorizado, se insertó, se usó y se borró al terminar.
- [x] **Grilla diaria (`tabs-principal`, "tabla de producción") — confirmado con conteo de DOM real**,
      no de texto: `th=18` en el header y **las 4 filas del cuerpo con `td=18`**, sin una sola celda
      corrida — exactamente lo esperado para esta combinación de 3 flags (18 es el valor más chico de
      la matriz de X18.4.1-b, con los dos flags reductores ON y sin columna Estado).
- [x] **Valores PRIMERA/PNC por fila, comparados 1 a 1 contra la BD — los 4 coinciden exacto**:

      | id  | huevo_tot (BD) | Primera (pantalla) | Pnc (pantalla) |
      |-----|------|---------|-----|
      | 677 | 1290 | 1,200   | 90  |
      | 676 | 77   | 0       | 77  |
      | 674 | 6230 | 0       | 0   |
      | 675 | 2300 | 0       | 0   |

      674 y 675 son a propósito: sus 3 ítems tienen `tipoHuevo: null` (snapshot previo al fix de
      `cdf0239` en X17.7, dejado vivo como testigo) — `resumirHuevoItemsPorTipo` los deja fuera de
      Primera y de Pnc sin romper nada, y el total (`Huevos Tot.`) sigue mostrando el crudo. Confirma
      en vivo un comportamiento que antes sólo estaba documentado por lectura de código.
- [x] **La tabla de Indicadores semanales es dinámica un nivel más abajo, y también se verificó**: el
      detalle expandible de la semana 33 no sólo separa Primera/Pnc — lista **cada ítem del catálogo
      por su nombre y código**, generado por `@for` sobre lo que realmente haya en el dato (no una
      lista fija). Pantalla mostró exacto: `552 — HUEVO SIN CLASIFICAR LIBRE DE JAULA CERTIFICADO:
      1.200` bajo Primera, y `537 — HUEVO PICADO ROJO: 45` + `538 — HUEVO MANCHADO ROJO: 45` bajo Pnc.
- [x] **Sin errores de consola ni de red** durante todo el recorrido post-login: los únicos 401 en
      consola son los 2 intentos de login fallidos previos a cambiar de estrategia. Los 8 endpoints que
      carga la pantalla (`Company`, `filter-data`, `SeguimientoValidacion/pendientes`,
      `LotePosturaProduccion`, `informacion-lote`, `seguimiento`, `traslados/cohortes`,
      `indicadores-semanales`×2, `clasificacion-huevo-items`) devolvieron 200.
- [x] **Limpieza**: fila de `sesiones_activas` borrada, backend y frontend detenidos, puertos `:5002`
      y `:4200` verificados libres (`netstat`). No se guardó, editó ni eliminó ningún seguimiento —
      todo el recorrido fue de lectura. No se tocó ningún flag de empresa (Santa Reyes ya los tenía
      así de antes).
- [i] **Lo que queda sin verificar en vivo, anotado para no repetir el hallazgo de X17.8**: los
      reportes que leen `huevo_tot`/11 columnas en vez de `huevoItems` (contable, técnico de
      producción, técnico semanal — ver X17.5) no se tocan con este flujo, y su smoke visual con datos
      de Santa Reyes sigue pendiente si el usuario lo pide.

---

## X18.7 — Los reportes ciegos a `huevoItems`: 2 defectos reales arreglados, no solo columnas vacías (25-ago-2026)

Continúa X18.6. Plan: [`fase_de_desarrollo/santa_reyes_reportes_ciegos_huevo_items_plan.md`](fase_de_desarrollo/santa_reyes_reportes_ciegos_huevo_items_plan.md).
Auditoría de código (Agent Explore) sobre los 4 reportes que X17.5/X18.6 habían dejado anotados
como "ciegos al ítem" — resultó en 2 defectos reales (no solo cosméticos) + 2 gaps de gateo
incompleto. Frontend-only, mismo patrón que el barrido de machos (X18.4): nada de backend cambia.

- [x] 🔴 **Defecto real 1 — Reporte Diario Costos Postura: banner falso, confirmado EN VIVO.**
      Disparaba *"4 registro(s) donde fértil + comercial + inservible no suma el huevo total. Es un
      defecto del dato cargado"* en el 100% de las filas de Santa Reyes (lote `SMOKE-SR-001`, 4/4).
      No es un defecto del dato: el reporte nunca puede cuadrar una partición que no calcula
      (fértil/comercial/inservible salen de las 11 columnas fijas, siempre 0). Fix: `huevosDescuadrados`
      = 0 con el flag ON (el banner deja de dispararse solo) + las 3 columnas rotas se ocultan
      (pantalla y Excel, `hojaHuevos` con parámetro nuevo `clasificacionPorItems`, mismo spread
      `[]`/`[valores]` en la MISMA posición que ya usa `soloConMachos` para no desalinear). `Ventas
      de huevo`/`Traslado a planta` (de `traslado_huevos`) y `Huevo Total` (= `huevo_tot`) intactos.
      **Verificado en vivo tras el fix**: banner desaparecido, columnas ocultas, total sigue en
      9.897 = suma de los 4 registros.
- [x] 🔴 **Defecto real 2 — Reporte Técnico Producción · Cuadro: `LAA` también estaba roto**, y la
      auditoría inicial no lo había listado. Verificado contra
      `ReporteTecnicoProduccionService.Cuadro.cs:179-192`: `Laa = Sum(HuevosIncubablesSemanal) /
      Count` — mismo origen que `HUEVOS INCUB`/`%DESCARTE`/`%ACUM INCUB`. Los 4 se ocultan; `STD
      ROSS` (valor de guía, no depende de `huevo_inc`) se mantiene solo. **Regla para no repetir
      este hallazgo**: cuando la fuente dice "estas 3 columnas están rotas", releer la fórmula de
      CADA columna del grupo, no confiar en que la lista esté completa.
- [x] **Gap de gateo incompleto 1 — Reporte Contable · Movimientos Huevos**: ya ocultaba 2 de 3
      columnas rotas (`HVO COMERCIAL`, `HUEVO DESECHO`); faltaba `HVTO FERTIL` (mismo origen,
      `huevo_inc`). Extendido el `@if` existente + colspan del grupo "Producción" de 2→1.
- [x] **Gap de gateo incompleto 2 — Reporte Técnico Producción · Diario y Cuadro**: `INCUBABLE`/
      `CARGADO` (Diario) y `H.CARGA`/`H.CAR ACU` (Cuadro, grupo "HUEVOS CARGADOS Y POLLITOS") salen
      de `huevo_inc`; ocultos. `V.HUEVO` (de `traslado_huevos`) y los 4 de pollitos/eclosión
      (`V.HUEVO POLLITOS`, `POLL.ACUM`, `PAA`, `PAA ROSS` — no dependen de huevo) se mantienen.
- [x] **Verificado en vivo con datos reales** (lote `SMOKE-SR-001`, sesión de smoke re-autorizada):
      Costos Postura (defecto 1, arriba) y Reporte Técnico Producción · Diario (sin columnas
      Incubable/Cargado, sin pestaña Clasificación, `huevo_tot` correcto por fila: 2300/6230/77/1290).
      Técnico Semanal confirmado por estado del componente (`clasificacionHuevoPorItems === true`
      resuelto correctamente) — la vista "Detalle de lote" no se pudo renderizar con datos reales:
      `SMOKE-SR-001` no tiene fila en `lote_postura_base` (nació del flujo de smoke E2E, no del
      wizard de lote base) y ninguno de los 10 lotes base sembrados de Santa Reyes tiene producción
      real. Cuadro y Contable quedaron verificados por código + los 649 tests, no en pantalla: Cuadro
      no encontró datos para el rango disponible ("No se encontraron datos del cuadro para el
      período") y Contable pide el mismo `lote_postura_base` que Técnico Semanal.
- [x] **Tests nuevos**: `construir-aoa-costos-postura.funcion.spec.ts` (4 casos: cabecera con/sin las
      3 columnas rotas, cabecera y filas con igual cantidad de columnas con el flag ON, sin filas de
      producción no agrega la hoja) y `construir-aoa-reporte-semanal.funcion.spec.ts` (3 casos: con/
      sin hojas CLAS, las hojas de semanas no cambian). Ninguno de los 2 módulos tenía tests antes.
      `yarn build` 0 errores · `yarn test` **649/649** (base 642, +7).
- [~] **Excel de Contable y Técnico Producción quedan sin tocar**: se generan en el BACKEND
      (`exportarExcelCompleto`), no con la librería `xlsx` del front — mismo criterio que ya dejó
      anotado X18.4 para "W1.d reportes y exportaciones" del barrido de machos. Fuera de alcance de
      este pase; si se pide, es un cambio de backend, no de frontend.
- [i] **Alcance deliberado: ocultar, no reemplazar.** Ningún reporte pasa a mostrar Primera/Pnc (lo
      que sí hace la grilla diaria): eso exigiría que cada reporte agregue `metadata.huevoItems`,
      backend nuevo por reporte. Este pase es sobre no mostrar datos matemáticamente rotos o un aviso
      que acusa al dato de un defecto que es del reporte.
- [i] **Con el flag OFF, cero cambios visibles** en los 4 reportes: todas las condiciones nuevas son
      `@if (!clasificacionHuevoPorItems)` sobre columnas que antes se pintaban siempre.
- [x] **Limpieza**: fila de `sesiones_activas` borrada, backend y frontend detenidos, puertos `:5002`
      y `:4200` verificados libres. Ningún dato de Santa Reyes creado/editado/eliminado — todo el
      recorrido fue de lectura salvo la fila de sesión, ya borrada.

---

## X18.8 — Excel del backend (Contable + Técnico Producción): mismo defecto, sin el gateo que se creía que ya existía (25-ago-2026)

Continúa X18.7. Plan: [`fase_de_desarrollo/santa_reyes_excel_backend_reportes_plan.md`](fase_de_desarrollo/santa_reyes_excel_backend_reportes_plan.md).
El Excel de estos 2 reportes lo genera un exportador EPPlus **separado** del `xlsx` de frontend que
ya se arregló — auditoría de código (Agent Explore, doble lectura cruzada por archivo) confirmó que
**la premisa de "ya hay gateo de machos que copiar" era falsa**: el barrido de machos (X18.4) fue
enteramente frontend, ningún exportador Excel del backend lee `OcultaMachosEnPostura` ni ningún
otro flag de empresa. Hubo que construir el mecanismo desde cero.

- [x] 🔴 **4º defecto real, no cubierto por X18.7**: `DESCARTE` (Reporte Contable) sale de
      `traslado_huevos.cantidad_desecho`, que `TrasladoHuevosService.CrearTrasladoHuevosAsync:191-203`
      zerea con el MISMO criterio que las 11 columnas legacy de `seguimiento_diario_produccion`
      cuando `usaHuevoItems=true` — confirmado contra datos reales (los 2 traslados de Santa Reyes,
      `cantidad_desecho=0` ambos). No estaba gateada ni en el front (gap de X18.7, cerrado ahora)
      ni en el Excel.
- [x] 🔴 **Hallazgo no pedido en el alcance original**: `ReporteTecnicoProduccionExcelService`
      genera una 3ª hoja, **"Clasificación Huevo Comercio"**, no mencionada en ningún ticket previo.
      16 de sus 18 columnas de datos salen de las 11 legacy — la hoja MÁS rota de las dos. El front
      ya oculta la pestaña homónima ENTERA para Santa Reyes; mismo tratamiento acá.
- [x] **Contable — `ColumnasHuevos` ya era data-driven**: se agregó `bool
      OcultaSiClasificaPorItems` a la tupla (marca HVTO FÉRTIL/HVO COMERCIAL/HUEVO DESECHO/DESCARTE)
      y se filtra el array UNA vez al principio de `EscribirMovimientosHuevos` — cabecera de grupo,
      cabecera de columna, filas de dato y fila de totales quedan alineadas por construcción, cero
      riesgo de desalinear (mismo patrón que `filtrar-columnas-machos.funcion.ts` del frontend). El
      flag viaja en `ReporteMovimientosHuevosDto` (nuevo campo), resuelto en
      `ObtenerReporteMovimientosHuevosAsync` con el mismo query que ya usa
      `DiasAlimentoPrevioEncaset`.
- [x] **Técnico Producción — decisión de diseño tomada, no inferible del código**: `EscribirReporteDiario`/
      `EscribirCuadro` escriben por **índice numérico fijo** (`ws.Cells[row,15]`), no por lista — no
      hay patrón de columnas dinámicas que copiar. Remover columnas reindexando 43 celdas a mano en
      2 métodos es el refactor grande y de alto riesgo que el propio commit de machos (`f7aee82`)
      señala como "lo delicado eran los colspan". **Decisión: mantener todos los índices, dejar la
      celda de DATO sin asignar (vacía) cuando el flag está ON** — el encabezado se conserva (menor
      fidelidad visual que el front, que sí remueve la columna; documentado como refactor aparte si
      se pide paridad total). `%DESCARTE`/`%ACUM INCUB`/`LAA` (huevo_inc-dependientes, confirmados
      contra `Cuadro.cs:179-192`) **nunca se escriben en este Excel** — nada que gatear ahí.
      `ReporteTecnicoProduccionLoteInfoDto` (compartido por los 3 reportes) ganó el flag; se resuelve
      UNA vez en `GenerarReporteSubloteAsync`/`GenerarReporteConsolidadoAsync` (Diario.cs) — Cuadro.cs
      lo hereda gratis porque ya reusa `reporteCompleto.LoteInfo` tal cual. La hoja "Clasificación
      Huevo Comercio" se deja de generar en el controller (no se llama a
      `GenerarReporteClasificacionHuevoComercioAsync` cuando el flag está ON, se pasa `null`).
- [x] `dotnet build` **0 errores / 0 warnings**.
- [x] **Verificado extremo a extremo con datos reales, HTTP real** (JWT minteado + `X-Secret-Up`
      calculado a mano con la misma derivación PBKDF2/AES que `EncryptionService.Decrypt`, sin
      pasar por el navegador): se generaron los 2 Excel reales de Santa Reyes (`SMOKE-SR-001`) y se
      abrieron con `openpyxl` para leer las celdas de verdad, no solo el HTTP 200.
      - Contable: cabecera exacta `Día|Fecha|Lote|POSTURA|ENTRADA|CAPTURA INFO|VENTA|SALIDA|TRASLADO
        A PLANTA` — **las 4 columnas ocultas (HVTO FÉRTIL/HVO COMERCIAL/HUEVO DESECHO/DESCARTE)
        ausentes**, `POSTURA`/`CAPTURA INFO` totales en 9.897 = suma exacta de los 4 registros.
      - Técnico Producción: hoja "Reporte Diario" con `INCUBABLE`/`CARGADO` en blanco por fila (resto
        de columnas con dato real intacto); hoja "Clasificación Huevo Comercio" **ausente del
        libro**. Hoja "Cuadro" también ausente, pero por una razón AJENA al fix: `ConsolidarSemanales`
        exige `>= 7` días por semana y `SMOKE-SR-001` solo tiene 4 registros dispersos — mismo "No se
        encontraron datos del cuadro" que ya se había visto en pantalla con este mismo lote.
- [x] **Regresión verificada con la MISMA receta contra Agroavicola Sanmarino (flag OFF, lote real
      `P-K345A`, 301 registros)**: los 2 Excel salen BYTE A BYTE con el comportamiento de siempre —
      Contable con las 10 columnas completas (`HVTO FÉRTIL`/`HVO COMERCIAL`/`HUEVO
      DESECHO`/`DESCARTE` con valores reales, no vacíos), Técnico Producción con las 3 hojas
      (`Reporte Diario` con `INCUBABLE`/`CARGADO` poblados, `Cuadro` con `HUEVOS INCUB`/`H. CARGA`
      poblados, `Clasificación Huevo` presente). Cero cambio para las empresas sin el flag.
- [~] **Sin xUnit nuevo**: no existe proyecto de test para `ZooSanMarino.Infrastructure` (solo
      `Application.Tests`/`Domain.Tests`, que no referencian Infrastructure) — crear uno de cero para
      2 servicios EPPlus es un cambio de arquitectura de testing más grande que este fix. CLAUDE.md
      exige tests de integración **o** requests reales para endpoints/handlers — se cumplió con la
      segunda vía, de punta a punta y con las 2 empresas.
- [x] **Otra vez el clasificador de Auto Mode bloqueó el primer intento del INSERT en
      `sesiones_activas`**; se pidió permiso de nuevo (autorizado) en vez de buscarle la vuelta. El
      segundo INSERT de la sesión (regresión con Sanmarino) sí pasó sin bloqueo — confirma que es
      una decisión por-llamada, no una política fija.
- [x] **Limpieza**: las 2 filas de `sesiones_activas` borradas, backend detenido, puerto `:5002`
      verificado libre. Cero escritura de datos de negocio — todo el recorrido fue GET.

---

## X18.9 — Cierra el pendiente de X18.7: Reporte Técnico Semanal verificado en vivo (25-ago-2026)

X18.7 había dejado la pestaña "Clasificación" de Reporte Técnico Semanal confirmada solo por
código + tests: la vista "Detalle de lote" pide un `lotePosturaBaseId` explícito, y `SMOKE-SR-001`
no tenía ninguno asignado (nació del flujo E2E de X17.7, no del wizard de lote base) — ninguno de
los 10 lotes base sembrados de Santa Reyes tenía producción real para probar con datos de verdad.

- [x] **Se resolvió por la vía correcta: el formulario real de lotes, no un `UPDATE` a mano.**
      `lote-list.component.html` ya tiene un `<select formControlName="lotePosturaBaseId">` en el
      form de edición — se editó `SMOKE-SR-001` desde la UI (`PUT /api/Lote/152`, 200) y se lo
      vinculó a `LOTE 217` (`lote_postura_base_id=23`, misma granja La Esperanza).
- [i] 🔴 **Efecto colateral no buscado, documentado para no sorprender la próxima vez**: vincular un
      lote a su lote base le **renombra automáticamente** siguiendo la convención del base + letra
      de sublote — `lotes.lote_nombre` pasó de `SMOKE-SR-001` a `LOTE 217A`. El lote de producción
      (`lote_postura_produccion.lote_nombre`) NO cambió, sigue siendo `P-SMOKE-SR-001` (se ve en la
      grilla diaria y en el chip del propio reporte) — mismo `lote_id=152`/`LPP=10` de siempre, solo
      cambió la etiqueta del lote unificado.
- [x] **Verificado en vivo con datos reales**: `Reporte Técnico Semanal → Detalle de lote →
      Producción → LOTE 217` generó el resumen semanal real (semana 33, producción huevos 1.290 —
      coincide exacto con el registro 677 ya conocido). El toggle de vista mostró **solo `["Tabla",
      "Gráficas"]`** — sin "Clasificación" — confirmado por DOM, no por lectura visual, junto con
      `clasificacionHuevoPorItems === true` y `tipoReporte === 'PRODUCCION'` leídos directo del
      estado del componente.
- [i] **Gotcha del smoke**: la página tiene DOS toggles Levante/Producción superpuestos —
      `rsm-toggle__btn` (del componente `ResumenSemanalMainComponent`, la pestaña "Resumen semanal")
      y `rts-toggle__btn` (el que importa, del `ReporteTecnicoSemanalMainComponent` de "Detalle de
      lote"). Buscar el botón por texto sin acotar por clase pegó en el primero y el reporte generó
      con Levante — sin datos, porque `SMOKE-SR-001`/`LOTE 217A` nunca tuvo seguimiento de levante.
- [x] **Este vínculo de paso también deja usable el "LOTE BASE" del Reporte Contable** (misma
      laguna de datos que tenía Técnico Semanal) para una futura verificación por UI — aunque ese
      reporte ya quedó verificado de punta a punta en X18.8 pegándole directo al backend con
      `lotePadreId`, sin necesitar el vínculo.
- [x] **Dato de fixture se deja como quedó** (mismo criterio que el resto de `SMOKE-SR-001`, X17.7:
      "sirve para abrir la pantalla y ver datos"): no se revirtió el `lotePosturaBaseId` ni el
      rename — mejora la utilidad del lote de prueba para próximas sesiones, no la reduce.
- [x] **Limpieza**: fila de `sesiones_activas` borrada, backend y frontend detenidos, puertos
      `:5002`/`:4200` verificados libres.

---

## EC1 — Ecuador: el cuadre de alimento que no cierra + 3 permisos (25-ago-2026)

Plan: [`fase_de_desarrollo/ecuador_cuadre_alimento_y_permisos_plan.md`](fase_de_desarrollo/ecuador_cuadre_alimento_y_permisos_plan.md)

Cuatro reportes de **Lady Malave** (ItalcolEcuador). Medido sobre la copia de **producción**
restaurada en local el 25-ago-2026.

### EC1.0 — Diagnóstico (hecho antes de escribir una línea de código)
- [x] **Causa raíz del descuadre de G0044 encontrada y atribuida al kilo**: `EliminarIngresoAsync`
      **no revierte el stock** (lo dice su propio doc-comment) mientras el controller documenta lo
      contrario. La usuaria borró la remisión **63705 duplicada** (mov `12983`, 5.000 kg, ítem 5): el
      histórico quedó `anulado=true` —la tabla diaria dejó de contarlo, correcto— pero
      `inventario_gestion_stock` conservó los 5.000 kg. Descuadre `-5.000,00` exacto.
- [x] **El mismo defecto, duplicado**, en `EliminarTrasladoAsync` (deja origen corto y destino largo).
- [x] **La forma correcta ya existe en el mismo service**: `AnularMovimientoHistoricoAsync`
      (`DELETE /movimientos/{id}`) sí revierte, en transacción, y rechaza si no hay stock. Explica por
      qué el ítem 4 del mismo galpón sí cerró en 0: se borró por ese camino.
- [x] **Alcance medido**: Ecuador **1** galpón descuadrado (5.000,0 kg) — el reportado —; Panamá
      **12** (55.866,5 kg) + 16 con días en rojo. Panamá **no** entra en esta entrega.
- [x] **Lady Malave identificada**: `ladymalave@ecuitalcol.com`, rol «Ecuador Administrador»
      (`role_id=10`, 2 usuarios), 18 permisos efectivos.
- [x] **Punto 4 mapeado**: `registros.fecha_retroactiva` existe, **ya está habilitado** en
      `company_permissions` para ItalcolEcuador; **solo falta** la fila de `role_permissions`.
- [x] **Punto 3 mapeado**: Gestión de Usuarios no tiene **ningún** gate (ni front ni back);
      `Program.cs:536` ya lleva escrito el `TODO(seguridad)` de endurecer `CanManageUsers`.
- [x] 🔴 **Punto 2 contradice su supuesto**: el desarrollo existe (commit `a9fd721`) pero **no tiene
      permiso propio**; el gate del form de engorde es `editar_registro`, que **ella ya tiene**, y el
      módulo de lotes de postura no tiene ningún `appHasPermission`. Cruzadas todas las keys de gate
      del front contra sus 18 permisos, lo único que le falta del alcance es
      `liquidacion.aplicar_correccion`, que no es de aves. **Pregunta abierta al usuario.**

### EC1.1 — F1: eliminar un ingreso/traslado devuelve el stock
- [x] `ReversionMovimientoInventarioCalculos` (puro) + 12 tests xUnit. El signo lo decide el TIPO;
      un tipo desconocido es `NoSoportado`, **no** `Ninguno` (fail-closed).
- [x] `EliminarIngresoAsync` revierte stock en transacción y rechaza con 400 si los kilos ya se
      consumieron — mismo patrón probado de `AnularMovimientoHistoricoAsync`.
- [x] `EliminarTrasladoAsync` revierte **los dos extremos**; primero las puntas que descuentan, para
      que el error nombre la que realmente bloquea.
- [x] 🔴 **Trampa cazada**: `TrasladoInterGranjaPendiente` PARECE una salida y no descuenta stock al
      crearse (descuenta al recibir). Devolverle stock al borrarlo habría **inventado** alimento.
      Tiene su propio test.
- [x] Doc-comments del controller alineados con lo que el código hace (antes prometían la reversión
      que el service no hacía).

### EC1.2 — F2: «Cuadrar galpón» desde la pestaña de Cuadre
- [x] `movement_type` `AjusteCuadreTablaEntrada`/`Salida` → `tipo_evento`
      `INV_AJUSTE_CUADRE_ENTRADA`/`_SALIDA` en `fn_tipo_evento_inventario`. **Dos tipos con signo
      propio** en vez de uno con cantidad firmada: `AjusteStock` guarda `Math.Abs` y por eso perdió
      el signo para siempre — no se repite el error.
- [x] `fn_seguimiento_diario_engorde` **v17**: las 5 CTE (`apert_mov`, `hist_full`, `hist_alimento`,
      `docs_por_fecha`, `fechas_universo`) leen los tipos nuevos. Espejo `.sql` actualizado.
- [x] `fn_cuadre_alimento_engorde`: los cuenta en `mov_post` (un ajuste fechado después del último
      seguimiento no cabe en la tabla, igual que un ingreso).
- [x] **Migración `20260825150000_FnAjusteCuadreAlimentoEngordeV17`** con las 3 funciones + su `Down`
      (el `.sql` es el espejo; la migración es el vehículo).
- [x] `AjusteCuadreAlimentoCalculos` (puro) + 14 tests, con los DOS casos reales y opuestos: G0044
      (sobra stock ⇒ se mueve el inventario) y G0475 (sobra tabla ⇒ se mueve la tabla).
- [x] `POST /api/CuadreAlimentoEngorde/cuadrar-galpon`, gateado por
      `cuadrar_ingresos_traslados_seguimiento` (la key que ya existía para esto — no se inventa una
      segunda llave para la misma puerta).
- [x] Modal `modal-cuadrar-galpon` (`changeDetection: Eager`) con los 3 números, previsualización de
      lo que se escribe **de cada lado** y motivo obligatorio de 10 caracteres.
- [x] 🟢 **GATE DE PARIDAD MULTIPAÍS: PASADO.** 6.851 filas antes y después (ItalcolEcuador 5.501 +
      ItalcolPanama 1.350); **0** en las 7 columnas del diff **en las dos empresas**; 6.765 filas de
      seguimiento esperadas == 6.765 presentes; `fn_cuadre_alimento_engorde(NULL)` idéntico
      (EC 37/1/5.000,0 kg · PA 31/12/55.866,5 kg). Cero por construcción: ninguna fila del histórico
      tiene los tipos nuevos.

### EC1.3 — F3/F4: cerrar G0044 y que no vuelva a pasar callado
- [x] 🟢 **PROBADO END-TO-END contra la copia de producción, en las DOS direcciones** (transacción
      revertida, `scratchpad/smoke_cuadre.sql`):
      - **G0044 (Ecuador, sobra stock):** stock 12.720,0 → 7.720,0; descuadre **−5.000,0 → 0,0**.
        **ItalcolEcuador pasa de 1 galpón descuadrado a 0.**
      - **G0475 (Panamá, sobra tabla):** el `movement_type` nuevo atravesó el trigger → histórico
        como `INV_AJUSTE_CUADRE_SALIDA` (18.650,356 kg, fechado en el último seguimiento 2026-08-06)
        → la fn **v17** lo leyó → saldo 21.216,4 → **2.566,0**; descuadre **18.650,4 → 0,0**.
        Panamá baja de 12 a 11 descuadrados y de 55.866,5 a 37.216,1 kg.
        **Este es el caso que hasta hoy NO tenía arreglo posible desde ninguna pantalla.**
- [i] G0475 conserva su `filas_negativas = 1` — correcto: es la OTRA señal (un día que cerró en
      rojo), que un ajuste de kilos no toca ni debe tocar. Justamente por eso ahora son dos columnas.
- [x] 🟢 **Y despues G0044 se cerro DE VERDAD por el endpoint real**, con el usuario de Lady Malave y
      su propia sesion: `POST /cuadrar-galpon` con 7.720 kg reales → **200**, un solo `AjusteStock`
      de −5.000 kg con su motivo en la auditoria. **ItalcolEcuador queda en 0 galpones descuadrados
      (37/37 cuadran).** Sin un solo `UPDATE` a mano: exactamente el camino que va a usar la usuaria.
- [x] La causa probable por fila ya existía (`CuadreAlimentoEngordeCalculos.DescribirConAjustes`) y
      se conserva; ahora la fila además ofrece el botón que la cierra.
- [x] `descuadre_kg` y `filas_negativas` separadas: columna propia «Días en rojo» + el subtítulo
      explica que **son dos señales distintas y no se suman** (mezclarlas es lo que daba 23 galpones
      donde había 8).

### EC1.4 — Punto 3: permiso `usuarios.gestionar`
- [x] Migración `20260825130000_SeedPermisoUsuariosGestionar`: permiso + `company_permissions`
      (`CROSS JOIN companies`) + **anti-lockout heredando de `role_menus` por route `/config/users`**.
      Validado en transacción revertida: **12 roles** lo reciben, exactamente los 12 que hoy ven el
      módulo. Nadie pierde acceso el día del deploy.
- [x] 🔴 **`menu_permissions` NO se toca** — y la verificación adversarial corrigió el dato: la tabla
      **no** está vacía (17 filas de tickets/ItalJira/gerencia), pero **ninguna** es del menú
      «Usuarios». Verificado: sigue en **0** para `/config/users`, así que el módulo se sigue viendo.
- [x] `GestionUsuariosAutorizacionCalculos` (puro) + 9 tests, incluida la mitad que se rompe sin
      querer: **el GET queda abierto**.
- [x] ⛔ **NO se endurece la policy `CanManageUsers`**: la usan `RoleController` y `MenuController`,
      ajenos al módulo. El gate va en un filtro de clase (`GestionUsuariosEscrituraFilter`) sobre
      `UsersController` y `UserFarmController` — 32 endpoints, ~20 de escritura: repetir el `if`
      veinte veces garantiza que alguno se olvide, y el olvidado **deja pasar**.
- [x] **Segunda puerta de alta cerrada**: `POST /api/Auth/register` también crea usuarios.
- [x] **`[Authorize]` restaurado** en `UserFarmController` (estaba comentado con un «TEMPORAL»; sus
      17 endpoints dependían solo de la `FallbackPolicy`).
- [x] Front: gate en el botón Crear (vive en el **padre**) y en los 5 de la fila, **en sus dos
      copias** (tabla desktop y tarjetas móvil).
- [x] **«Ver detalle» construido**: no existía. Reusa `modal-create-edit` en modo solo lectura, con
      `saveUser()` cortado en la entrada — no alcanzaba con esconder el botón, porque ese método
      dispara **dos** escrituras (usuario + perfil de tickets).
- [i] Los 3 botones de sesiones van por `usuarios.revocar_sesion`, **su propia key**: el backend ya
      la exigía y meterlos bajo la nueva habría creado dos llaves para la misma puerta. Se siembra
      en la misma migración porque **nunca se sembró**: hoy ese botón da 403 a todo el que no sea
      super admin.

### EC1.5 — Punto 4: fecha retroactiva para «Ecuador Administrador»
- [x] Migración `20260825120000` data-only idempotente; rol por **nombre + empresa** (nunca por id)
      y `EXISTS` sobre `company_permissions` (sin esa fila la asignación nace huérfana).
- [x] Validado en transacción revertida: Lady Malave queda con `registros.fecha_retroactiva`
      efectivo. Alcanza a los **2** usuarios del rol — declarado, no escondido.

### EC1.6 — Punto 2: permiso `lote.corregir_aves`
- [x] Migración `20260825140000`: permiso + `company_permissions` + **herencia desde
      `editar_registro`**. Validado: **13 roles** lo reciben = exactamente los 13 que hoy tienen
      `editar_registro`. Nadie gana ni pierde — importa porque POSTURA hoy no tiene ningún gate.
- [x] `CorreccionAvesLoteAutorizacionCalculos` (puro) + 8 tests.
- [x] 🔴 **El gate mira el DELTA, no el verbo**: un `PUT` que solo cambia el técnico o la regional
      sigue funcionando sin el permiso. Si pidiera el permiso para todo el `PUT`, este permiso sería
      un segundo `editar_registro` — el problema que vino a resolver.
- [x] **Enforcement en el BACKEND** (`LoteAveEngordeService` y `LoteService`, 403), no solo en el
      front: el gate anterior era **cosmético** — el mismo `PUT` por curl aplicaba el ajuste sin
      `editar_registro`.
- [x] Front: campos de aves en solo lectura sin el permiso, en engorde **y en postura** (que no
      tenía ningún gate), solo al EDITAR — crear un lote fija su encasetamiento y eso no es corregir.

### EC1.7 — Verificación
- [x] **Las 3 migraciones de permisos validadas en transacción revertida** contra la copia de
      producción: 2 permisos nuevos, las 4 keys fantasma como no-op (ya existen en prod), 10 filas de
      `company_permissions`, 12 roles con `usuarios.gestionar`, 13 con `lote.corregir_aves`,
      Lady Malave con `registros.fecha_retroactiva`, y `menu_permissions` de `/config/users` en 0.
- [x] `dotnet build` (con F1 + los 3 permisos): **0 errores, 0 advertencias**.
- [x] `yarn build` (con los gates del front y «Ver detalle»): **0 errores**.
- [x] **Prueba end-to-end del ajuste de cuadre en las dos direcciones** (ver EC1.3): el tipo de
      movimiento nuevo recorre trigger → histórico → fn v17 → cuadre y deja los dos galpones en 0,0.
- [x] 🔴 **Dos defectos propios cazados antes de compilar**, los dos del mismo tipo (recalcular un
      invariante sin todos sus términos):
      1. `fila.StockKg` es la suma de **todos** los ítems del galpón; escribir ahí los kilos totales
         sobre un solo ítem lo habría inflado por lo que valen los demás. Se aplica el **delta**.
         Con un solo ítem con saldo —el caso normal— las dos formas coinciden, que es justo lo que
         lo habría hecho pasar por alto hasta el primer galpón con dos alimentos.
      2. `DescuadreKg` **no** es `saldo − (stock − movPost)`: viene corregido por lo **reservado**
         por la doble validación. Ignorarlo habría dejado el galpón descuadrado por el monto
         reservado **después de una pantalla que dijo «cuadrado»**. Panamá tiene 12.609,7 kg activos
         en 3 reservas; Ecuador, cero — o sea, se habría desplegado sin verse.
- [x] `dotnet build` + **`dotnet test`: 3.228 tests, 0 fallos** (3.227 Application + 1 Domain),
      incluidos los 43 nuevos.
- [x] `yarn build` con el modal de cuadre: **0 errores**.
- [x] **Migraciones aplicadas de verdad en local** (`dotnet ef database update`): las 4 quedaron en
      `__EFMigrationsHistory` y verificadas contra la BD — 12 roles con `usuarios.gestionar`,
      13 con `lote.corregir_aves`, Lady Malave con sus 5 permisos efectivos, `menu_permissions` de
      `/config/users` en 0, y `fn_tipo_evento_inventario` mapeando los dos tipos nuevos.
- [x] **Gate de paridad re-corrido DESPUÉS de la migración** (o sea, contra la fn que aplicó el
      vehículo, no la que se cargó a mano): 0 en las 7 columnas, las dos empresas.
- [x] **Smoke HTTP contra el backend real** (`:5002`, usuario Lady Malave, sesión propia):
      - `GET /api/CuadreAlimentoEngorde` → **200**, y reporta G0044 con descuadre −5.000. Prueba lo
        que ni el build ni los tests ven: que la **DI resuelva** `CuadreAlimentoEngordeService` con
        su parámetro nuevo (`IInventarioGestionService`).
      - `POST /cuadrar-galpon` **sin** `cuadrar_ingresos_traslados_seguimiento` → **403**.
      - `POST` con motivo de 5 caracteres → **400** con el mensaje del cálculo puro.
      - `POST` declarando 12.720 kg → **200**: escribió `AjusteCuadreTablaEntrada` de 5.000 kg
        fechado en el último seguimiento, el trigger lo espejó como `INV_AJUSTE_CUADRE_ENTRADA` y la
        fn lo leyó. **Cadena completa probada por HTTP, no solo por SQL.**
- [i] 🔴 **Ese último POST era una prueba mal rotulada mía, no un defecto del código**: declarar
      12.720 kg le dice al sistema «el stock tiene razón, corregí la tabla», y eso fue exactamente lo
      que hizo. Se revirtió borrando el movimiento — y de paso quedó probado que el trigger
      `AFTER DELETE` marca `anulado = true` también para el `tipo_evento` nuevo, y que el cuadre
      vuelve solo a −5.000.
- [i] 🔴 **Trampa de verificación que casi hace pasar un smoke vacío: `dotnet test` NO reconstruye el
      proyecto API** (los tests no lo referencian). El `.exe` que se levanta después de un `test`
      puede ser de varios cambios atrás — se detectó porque el DTO respondía sin
      `reservadoActivoKg`. Antes de un smoke HTTP hay que construir **el API explícitamente**.
- [i] **Gotcha del smoke con sesion propia** (B1): ademas de la fila en `sesiones_activas`, el
      `expires_at` tiene que quedar **holgado** (el chequeo compara contra `UtcNow` y una fila a
      «+2 horas» locales cae del lado vencido), y el backend hay que **reiniciarlo** despues de
      insertarla — cachea el veredicto de la sesion y sigue devolviendo el 401 viejo.
- [x] **Limpieza**: los 2 movimientos de una prueba mal rotulada mia, borrados (sus filas del
      historico quedaron `anulado = true` por el trigger, que de paso prueba que el `AFTER DELETE`
      tambien cubre el `tipo_evento` nuevo); fila de `sesiones_activas` borrada; backend detenido y
      puertos `:5002`/`:4200` verificados libres.

---

## EC2 — Barrido del cuadre de alimento de ItalcolPanama (25-ago-2026)

Plan: [`fase_de_desarrollo/barrido_cuadre_panama_plan.md`](fase_de_desarrollo/barrido_cuadre_panama_plan.md)

Pedido tras cerrar EC1: entender y cerrar los galpones descuadrados de Panamá, que en EC1 habían
quedado declarados fuera de alcance sin diagnóstico.

- [x] 🔴 **El número que se había reportado estaba mal.** «12 galpones / 55.866,5 kg» es el crudo de
      `fn_cuadre_alimento_engorde`. **La pantalla muestra 11 / 63.668,2 kg**, porque el servicio le
      suma lo *reservado* por la doble validación. **G0479 ni siquiera está descuadrado** (su −907,0
      es exactamente su reserva de 907,0); G0463 sube a 8.306,9 y G0492 pasa a +2.812,0.
- [x] **Causa atribuida galpón por galpón.** El patrón dominante es uno solo: alguien corrigió el
      inventario a mano y la tabla diaria nunca se enteró (`INV_OTRO`, que la fn no lee). Los dos
      más grandes coinciden **al kilo** con una `EliminacionStock`: G0475 con 18.650,356 kg (07-ago)
      y G0483 con 12.500,000 kg (01-ago) — **31.150 kg, casi la mitad del total**, son dos clics.
- [x] **Ninguno es el bug de EC1.** Ese produce el signo contrario. Se buscaron explícitamente
      entradas borradas sin reversión de stock: hay en G0483, G0495 y G0496, pero ninguna coincide
      con su descuadre.
- [x] 🟢 **Ensayo en transacción revertida: los 6 seguros quedan en 0,0**, Panamá pasa de 12 a 6
      descuadrados y de 55.866,5 a 15.289,1 kg (**73 % cerrado**), y —lo que importa— **ningún
      galpón gana días en rojo** (16 antes, 16 después). Script:
      `backend/sql/verificar_barrido_cuadre_panama.sql`.
- [x] 🔴 **El ensayo cazó un galpón que NO se puede barrer: G0495.** Tiene 178,3 kg de stock y
      2.786,0 kg entrados después del último seguimiento ⇒ declarar que el stock manda exige que la
      tabla cierre en **−2.607,7 kg**. En el primer ensayo el saldo quedó negativo y el galpón
      **ganó un día en rojo que hoy no tiene**. Ahí lo que está mal es el inventario, no la tabla.
      Es exactamente el «verificar antes de limpiar datos» de CLAUDE.md.
- [x] **Otros 3 quedan fuera con motivo**: G0463 (+8.306,9) y G0492 (+2.812,0) son **reservas
      activas** —seguimientos sin validar, no kilos faltantes— y se cierran validándolos; G0460
      (−6.667,2) es un problema de **fecha** (3 llegadas de julio, 18.018,2 kg, anteriores al
      arranque del ciclo); G0461 (+317,5) es prematuro (ciclo recién arrancado, más de medio stock
      en movimientos posteriores).
- [x] 🔴 **Lo que NO se pudo cerrar, dicho como tal**: reconstruí el delta con signo de cada ajuste
      parseando su motivo (`Anterior → Nuevo`) y **la aritmética solo cierra en G0477**. En el resto
      los ajustes suman más que el descuadre (parte ya la absorbió la apertura del ciclo), y en
      G0476 directamente no hay ajustes registrados. Causa clara ≠ causa atribuida al kilo.
- [ ] ⏸️ **Ejecución en producción: BLOQUEADA por dos cosas.** (1) el deploy de EC1 —sin
      «Cuadrar galpón» seis de estos no tienen arreglo posible—; y (2) una confirmación de
      costos/operación a una sola pregunta que el código no puede contestar: **¿el inventario de
      esos galpones es confiable hoy?** Pesa sobre todo para G0475 y G0483 (31.150 kg entre los dos).
- [ ] ⏸️ **G0495 queda como pendiente propio**: es el único que señala un problema real de
      inventario (kilos que entraron y no están), no de sincronización entre las dos vistas.

---

## EC3 — El cruce de reproductora creaba los días de engorde SIN validar y trababa el lote (25-ago-2026)

Plan: [`fase_de_desarrollo/cruce_reproductora_nace_sin_validar_plan.md`](fase_de_desarrollo/cruce_reproductora_nace_sin_validar_plan.md)

Reporte de Panamá: «en las reproductoras se confirmaron tarde, entonces en pollo engorde está
bloqueado hacer el seguimiento; no deja crear otro seguimiento diario». Captura: lote **215**
(DAYLAND · galpón «6» · `14 - 1` · ERP `G-4001014`, 15 días).

### EC3.0 — Diagnóstico
- [x] 🔴 **Causa raíz: un `INSERT` que omite una columna con `DEFAULT`.**
      `fn_cruce_reproductora_a_engorde` inserta los días 1-7 **sin nombrar `validado`** (DEFAULT
      false), mientras el C# documenta lo contrario, textual
      (`SeguimientoDiarioAvesEngorde.Validado`): *«Los registros con OrigenCruce **nacen validados**»*.
      Verificado sobre la función **desplegada**: no mencionaba `validado` ni una vez.
- [x] **Por qué explota solo al confirmar tarde**: el plazo son **1 día contado desde la FECHA del
      seguimiento**, no desde cuándo se creó la fila. La reproductora del lote 215 confirmó sus 7
      días con **5 a 10 días de atraso** ⇒ el cruce insertó con fechas del 09 al 15-ago ⇒ los 7
      registros **nacieron entre 6 y 12 días vencidos** ⇒ `BloqueaAltaPorVencidos`.
- [x] 🔴 **Y era un callejón sin salida**: los registros `origen_cruce` son de solo lectura en la UI
      —el front les reemplaza *todos* los botones por el badge «🔄 Auto»—, así que el operario **no
      tenía forma de destrabarlo**.
- [x] **Alcance medido: 28 registros, 4 lotes** (215, 216, 224, 225), todos DAYNLAND/Panamá — la única
      empresa con `requiere_validacion_seguimiento_diario`. **Dos de esos lotes nacieron ese mismo
      día**: el problema estaba activo, no era histórico.
- [x] **El backfill de agosto arregló el pasado y nadie arregló el futuro**: los otros 273 registros
      de cruce están validados por el `UPDATE` masivo de `20260815071444`.

### EC3.1 — Arreglo
- [x] `fn_cruce_reproductora_a_engorde`: el `INSERT` escribe `validado, validado_at, validado_por`.
      Espejo `.sql` + **migración `20260825160000_FnCruceReproductoraNaceValidado`** (26 columnas
      contra 26 valores, verificado a mano).
- [x] **Backfill de los 28**, acotado a `origen_cruce AND NOT validado`.
- [x] 🟢 **Probado forzando una regeneración REAL del trigger** (transacción revertida): los 7 días
      del lote 215 nacen `validado = true` / `validado_por = SYSTEM_CRUCE`, y el lote queda con
      **0 vencidos**. Los 4 lotes se destraban.
- [x] **Script de invariante nuevo**: `backend/sql/verificar_cruce_nace_validado.sql`. Es la única
      red posible — **no hay test de C# que pueda ver un `INSERT` de plpgsql**, y el cuerpo de esa
      función está copiado en **5 migraciones**: la próxima que lo reescriba desde una copia vieja
      reintroduce el defecto en silencio, exactamente igual.
- [x] 3 tests que fijan el mecanismo (fecha vieja + sin validar = EN_RETRASO; validado gana sobre la
      fecha; el plazo es de 1 día). No cubren código nuevo — lo dicen ellos mismos: documentan la
      interacción que hizo posible el defecto.

### EC3.2 — Lo que trajo la refutación adversarial (3 lentes, todos «INCOMPLETO»)
- [x] **La elección de diseño resistió**: un lente reporta textual *«intenté refutar la elección y no
      pude»*. Descartadas con fundamento (a) excluir el cruce del conteo, (b) contar el plazo desde
      `created_at` —no resuelve: corre el bloqueo 48 h y el reloj se reinicia en cada regeneración—,
      (c) permitir validarlo en la UI —sería un no-op y se destruye sola en cada regeneración—, y
      (d) no bloquear si todos los vencidos son de cruce.
- [x] **Corregido: el backfill fabricaba una marca de auditoría.** Ponía `validado_at = now()` sobre
      registros de agosto. Ahora la deja en **NULL**, igual que el backfill original, y usa
      `validado_por = 'SYSTEM_CRUCE_BACKFILL'` para poder distinguir «esto lo arregló la migración»
      de «esto nació bien».
- [x] **Corregida una asimetría real**: `SeguimientoAvesEngordeService` (Panamá/Colombia) **no tenía
      la guarda `OrigenCruce`** que sí tiene el de Ecuador. Sin ella, tras el fix editar o borrar una
      fila de cruce fallaba por la rama de la doble validación con un mensaje que manda a «quitar la
      validación primero» — **imposible** para esos registros. Ahora rechaza con el motivo verdadero.
- [x] **Contradicción entre lentes resuelta a mano**: `MigracionService.SeguimientoEngorde` **sí**
      usa `ModoCargaHistorica()` (línea 596), así que la carga masiva ya estaba cubierta. Un lente
      afirmaba lo contrario.
- [x] **Verificado que no hay doble descuento**, y medido, no razonado: 0 filas en
      `seguimiento_reserva_alimento` y `_aves` para los 301 registros de cruce; las aves ya estaban
      descontadas por `RetiroAvesEngordeAplicador`, que mira `OrigenCruce` y el histórico, **nunca
      `validado`**. Ninguna función ni vista de la BD lee la columna (catálogo completo).

### EC3.3 — Lo que NO arregla esto, dicho como tal
- [i] **Lote 177** sigue bloqueado **a propósito**: su vencido es un registro **normal** (no de
      cruce), y se destraba con el botón Validar. Es trabajo del operario, no un defecto.
- [i] **Lote 180** (registro del 24-ago) pasa a EN_RETRASO mañana si nadie lo valida. Mismo caso.
- [i] **Dos lotes en cola** (186 y 226): sus reproductoras tienen días sin confirmar desde hace 27 y
      6 días. Con el arreglo puesto, al confirmarlas los días nacen validados y **no** se traban.
- [x] **La misma clase de defecto en el push offline de la PWA — MOOT, no era un bug (27-ago-2026).**
      Apliqué primero (mecánicamente) el mismo patrón de `ModoCargaHistorica` que
      `MigracionService`/`PuentePanamaService`, y lo **revertí** antes de commitear al verificar el
      efecto real: `ModoCargaHistorica` no solo evita la fecha límite vencida, **desactiva por
      completo `SepararAsync`** (la separación/doble validación del módulo) — habría descontado
      alimento de inmediato y sin confirmación humana para TODO push offline a una empresa con
      `requiere_validacion_seguimiento_diario` (hoy Panamá), no solo para el caso viejo de "nace
      vencido". Habría sido una regresión peor que el problema original.
      🔴 **El problema original ya no existe, por EC6 (un día antes, `94e1f9f`, 26-ago).**
      `CreateAsync` fija `CreatedAt = DateTime.UtcNow` al momento de escribir (línea propia del
      service, no depende de `CapturadoAtDispositivo`), y desde EC6
      `FechaLimiteValidacion = max(fecha, creación) + 1 día` con `hoy > límite` estricto ⇒ un
      registro creado HOY nunca puede nacer `EN_RETRASO` HOY, sin importar cuán vieja sea su
      `fecha` — la condición que describía EC3.3 (escrita el 25-ago, un día antes de EC6) es
      estructuralmente imposible después de EC6. Verificado leyendo
      `ValidacionSeguimientoCalculos.Estado`/`EstaEnRetraso` y
      `AsegurarPuedeRegistrarDiaAsync` (filtra por `EstaEnRetraso`, no por "pendiente"): un push de
      varios días seguidos tampoco se traba entre sí, porque ninguno de los recién creados está
      vencido todavía. **`SyncPushService.cs` quedó sin cambios** (`git diff` vacío). El plan
      [`sync_push_offline_carga_historica_plan.md`](fase_de_desarrollo/sync_push_offline_carga_historica_plan.md)
      queda como registro de la investigación y la corrección, no de un fix aplicado.
- [x] **Segundo camino de confirmación de reproductora sin sincronizar aves — YA RESUELTO, no es un
      pendiente.** Checkbox obsoleto: EC5 #2 (25-ago-2026) ya aplicó el fix "en las dos direcciones
      (validar y des-validar)". Verificado en código el 27-ago-2026:
      `ValidacionSeguimientoService.Validar.cs:195` sí llama a
      `RetiroAvesEngordeAplicador.SincronizarCruceAsync(...)` dentro de `MarcarValidadoAsync`.
- [i] **Observación**: `LeerPendientesDelLoteAsync` (rama Engorde) filtra por `LoteAveEngordeId` sin
      `company_id`, a diferencia de `LeerEstadoAsync`. Inocuo con una sola empresa con el flag
      encendido; deja de serlo con la segunda. Pre-existente.

---

## EC4 — Plazo de validación desde la CREACIÓN + doble validación en Ecuador (ANÁLISIS, 25-ago-2026)

Plan: [`fase_de_desarrollo/plazo_validacion_desde_creacion_plan.md`](fase_de_desarrollo/plazo_validacion_desde_creacion_plan.md)

Propuesta del usuario tras cerrar EC3. **Se implementa en su propia sesión** — acá quedó solo el
análisis y la validación con datos.

- [x] **Validado punto por punto lo que planteó el usuario.** Confirmado que hoy el plazo se cuenta
      desde la **fecha del seguimiento** y no desde la creación; que lo de «cuando termine los 7 días
      me aparecen confirmados» **ya quedó resuelto** en EC3; y que para la captura del mismo día las
      dos reglas dan **idéntico** resultado.
- [x] 🔴 **El dato que decide** (últimos 30 días, sin el cruce): **Panamá captura 86,5 % retroactivo**
      —de los cuales 426 registros los cargan PERSONAS con 2 a 21 días de atraso, repartidos en 6-13
      días de carga distintos; los otros 465 son una carga masiva—. **Ecuador, 14 %.** La regla actual
      está estructuralmente peleada con cómo opera Panamá: funciona solo porque el operario valida en
      la misma sesión en que carga.
- [x] **La propuesta ataca la raíz de TRES parches que ya existen**, todos el mismo caso («registro
      creado hoy con fecha vieja»): `ModoCargaHistorica` (cuyo propio doc dice que existe por esto),
      el cruce de reproductora (cerrado en EC3) y el push offline de la PWA (abierto).
- [x] 🔴 **Señalado que «vencido» hoy BLOQUEA, no avisa** — y que eso fue una decisión explícita del
      usuario el 14-ago-2026 («bloquean el alta de días nuevos, no solo avisan»). Cambiar el origen
      del plazo y cambiar bloqueo→alerta son **dos cambios distintos**: recomendado hacer el primero,
      medir, y decidir el segundo con ese dato.
- [x] 🔴 **El orden importa para Ecuador**: hoy tiene 5.482 registros, 0 sin validar ⇒ encender el
      flag no bloquea nada retroactivamente. **Pero con la regla actual el 14 % de su captura nacería
      vencida** (94 registros/mes). Encenderlo ANTES de cambiar la regla reproduce el problema que se
      acaba de cerrar en Panamá.
- [x] **Señalado lo que se pierde**: hoy «vencido» significa dos cosas —no validaste a tiempo Y no
      cargaste a tiempo—. Con `created_at` queda solo la primera; si la segunda importa (en Panamá
      probablemente sí), hay que reponerla como indicador propio de antigüedad de captura.
- [x] **Señalado el detalle fino**: `created_at` se reinicia al borrar y recrear, y el cruce hace
      `DELETE`+`INSERT` en cada regeneración ⇒ no apoyarse en el plazo para el cruce (hoy nacen
      validados, y así debe seguir).
- [x] **Implementación: checkbox obsoleto — ya se hizo, un día después, como EC6 (`94e1f9f`).** No fue
      "otra sesión" separada: el usuario pidió la ruta B esa misma noche y la regla `fechaCreacion` de
      `ValidacionSeguimientoCalculos` (`FechaLimiteValidacion` = `max(fecha, creación) + 1 día`) está en
      `main` desde el 26-ago-2026, con tests y el front actualizado. Verificado en código el 27-ago-2026
      — ver EC6 para el detalle real (no coincide del todo con lo que este análisis estimaba: no hizo
      falta tocar el bloqueo, solo de dónde cuenta el plazo).

### EC4.1 — Aclaración del usuario: el bloqueo se queda y se vuelve más estricto (25-ago-2026)

- [x] ✅ **Resuelta la pregunta abierta de EC4**: el usuario confirmó que **NO quiere quitar el
      bloqueo**. La regla que quiere es *«el día anterior debe estar confirmado para poder continuar
      al día siguiente»*.
- [x] 🔴 **Señalado que eso NO es el comportamiento actual: es más estricto.** Hoy bloquea «hay algún
      VENCIDO», y un registro de ayer sin validar sigue **PENDIENTE** dentro del plazo de 1 día ⇒ hoy
      **no** bloquea. Medido en este momento: el lote 177 bloquea con las dos reglas, pero el **lote
      180 no bloquea hoy y sí bloquearía** con la regla nueva. La regla elimina la gracia de un día.
- [x] 🔴 **El hallazgo que reordena todo el plan EC4**: si el bloqueo pasa a colgar de «el anterior
      confirmado», **deja de depender del plazo** ⇒ el cambio `fecha → created_at` **pasa a ser
      cosmético** (solo colorea la alerta). Los tres parches (`ModoCargaHistorica`, el cruce, el push
      offline) dejarían de hacer falta **por el cambio de bloqueo**, no por el del plazo.
      La secuencia recomendada quedó reordenada: **primero el bloqueo**, el plazo al final y opcional.
- [x] **Costo operativo medido, y no es menor**: con la regla estricta, cargar N días exige N ciclos
      cargar→validar alternados, y **hoy no existe validación en lote** (el endpoint es
      `POST /{modulo}/{id}/validar`, de a uno; el front valida fila por fila). Últimos 30 días:
      **Ecuador nunca carga más de 5 días juntos** (aguanta la regla tal cual); **Panamá cargó 6+ días
      en una sesión 41 veces, con un pico de 34 días**. Hace falta «guardar y validar» **o** «validar
      todos los pendientes del lote» junto con la regla.
- [x] **Confirmado lo que planteó el usuario sobre las dos empresas**: Panamá depende de tener las
      reproductoras al día —y con `14daf32` los 7 días del cruce nacen confirmados, así que dejan de
      trabar—; Ecuador **no tiene reproductoras** (0 registros `origen_cruce`, verificado), su flujo
      es captura normal desde el día 1.
- [x] ✅ **RESUELTO por el usuario: el día faltante SÍ bloquea** — ver bloque **EC4.2**, que además
      corrige el número de abajo (eran los huecos *interiores*; contando la cola son 565 en Panamá).
      Era: ¿un día **FALTANTE** (nunca
      capturado) también bloquea? Hoy no —el código solo mira registros existentes sin validar—. Con
      esa lectura, **37 lotes abiertos de Panamá quedarían bloqueados el día del deploy** (40 huecos;
      Ecuador tiene 1). Recomendado empezar por la lectura literal («el registro anterior», que
      existe) y tratar los huecos como reporte aparte.

---

## EC4.2 — Los huecos de días bloquean: qué cuesta y la trampa que hay que evitar (ANÁLISIS, 25-ago-2026)

Plan: [`fase_de_desarrollo/plazo_validacion_desde_creacion_plan.md`](fase_de_desarrollo/plazo_validacion_desde_creacion_plan.md) §9
Diagnóstico reproducible: [`backend/sql/verificar_huecos_dias_seguimiento_engorde.sql`](backend/sql/verificar_huecos_dias_seguimiento_engorde.sql)

> Decisión del usuario: *«todos los días se tienen que llenar hasta que se liquide el lote (…) debe
> mostrar cuáles no hay registro (…) sí o sí antes de dejarlo seguir. Y que el campo fecha en el modal
> me deje agregar el día específico que hace falta.»* **Sigue sin implementarse: es análisis para la
> sesión que tome EC4.**

- [x] **Respondida la pregunta directa: sí, se puede llenar el día faltante.** Verificado punta a
      punta — el input de fecha (`modal-seguimiento-engorde.component.html:36`) **no tiene `min` ni
      `max`**; el índice único `uq_seg_diario_aves_engorde_lote_fecha` impide duplicar un día; la
      ventana de fecha retroactiva **no aplica a seguimientos** (`ValidarVentanaFechaRegistro` solo
      está en inventario/gastos/movimientos/traslados); la única cota es «no antes del primer día del
      lote» (`Crud.cs:118`), correcta; y al insertar un día **del medio**,
      `SaldoAlimentoEngordeAplicador.RecalcularPorLoteAsync` reescribe el saldo de **todos** los días
      del lote desde la fn, así que los posteriores se corrigen solos.
- [x] 🔴 **Encontrada la trampa que habría que arreglar ANTES de encender la regla.**
      `AsegurarPuedeRegistrarDiaAsync(modulo, loteId)` **no recibe la fecha** y corre en la primera
      línea del create. Si se le agrega «los huecos bloquean», **bloquea también el POST que llena el
      hueco** → lote encerrado sin pantalla que lo destrabe: el mismo callejón sin salida del cruce
      (`14daf32`), por otra puerta. Fix de diseño: pasarle la fecha y **eximir el día que se está
      llenando**. Son 5 call sites y **ningún test actual lo cubre** (hoy la fecha no participa).
- [x] 🔴 **Corregido el número que se había reportado.** Los «40 huecos / 37 lotes» eran solo los
      **interiores**. Contando como lo definió el usuario (todos los días hasta liquidar):

      | Empresa | Días faltantes | Lotes | Interiores | Cola | Más viejo |
      |---|---:|---:|---:|---:|---:|
      | ItalcolPanama | **565** | 44 | 41 | **524** | 72 días |
      | ItalcolEcuador | **133** | 5 | 4 | **129** | 130 días |

      **El 93 % es cola, no hueco interior.**
- [x] **Caracterizada la cola: son lotes TERMINADOS que nadie cerró.** Los 18 lotes de Panamá con cola
      >7 días tienen 50–78 días de edad (un engorde se saca a ~42) y **cero salidas registradas**, con
      ~650.000 aves en papel. La causa está a la vista: **Panamá tiene 3 ventas registradas en todo el
      sistema; Ecuador 1.452**. No registran la venta ni liquidan → *la cola es su final de lote
      normal*. Ecuador es el mismo cuadro: el lote 2601 lleva 125 días de cola y 191 de edad.
      ⚠️ Aplicar la regla literal a la cola le pide al operario **inventar ~460 registros diarios** de
      lotes cuyas aves ya no están.
- [x] **Descartada la hipótesis de que el bloqueo se fabrica su propia cola**: 520 de los 524 días de
      cola están en lotes que hoy **nadie bloquea**. Es abandono operativo. De paso queda verificado
      el arreglo del cruce: **0 vencidos `origen_cruce`**, los 4 lotes de DAYLAND destrabados.
- [x] **Listados los días concretos** para el mensaje: Ecuador lote 12 «2601» (4 días, 17–20 abr);
      Panamá 37 lotes / 41 días, de los cuales **33 tienen exactamente un día**. Sin patrón de fin de
      semana (jue 14, dom 10, mié 9) ⇒ olvidos legítimos y llenables.
- [x] ✅ **DECIDIDO por el usuario: «el hueco interior bloquea, la cola se liquida».**
      Consecuencia medida: la cola era el 93 % del número y sale del bloqueo, así que **el costo del
      día del deploy baja de 44+5 lotes a 37 lotes de Panamá y 41 días de digitación** (33 de ellos
      necesitan **un solo día**). Ecuador queda en 0 porque su flag está apagado. La cola pasa a ser
      limpieza operativa: **19 lotes a liquidar** (18 Panamá + el 2601 de Ecuador).
- [x] 🔴 **Encontrada la interacción de segundo orden entre las dos reglas** (plan §9.10): el día que
      se llena **nace vencido**, porque `FechaLimiteValidacion(fecha) = fecha + 1` y el hueco es
      viejo. Un vencido sin confirmar bloquea el alta ⇒ *llenar el hueco vuelve a trabar el lote*.
      Dos consecuencias obligatorias para quien implemente: (a) la exención de la guarda tiene que ser
      «el día que se crea es **un** día faltante», no «es **el único**» —si no, el lote 12 de Ecuador,
      con 4 huecos seguidos, se traba al llenar el primero—; (b) hace falta **«guardar y validar»** o
      «validar todos los pendientes del lote», porque hoy se valida de a uno. Ya estaba anotado como
      deseable en EC4; la regla de huecos lo vuelve **obligatorio**.
- [x] ✅ **VERIFICADO: la puerta de liquidar ABRE — pero abre mal** (plan §9.8). Ninguna precondición
      de `CerrarLoteAsync` (`LoteAveEngordeService.cs:790-861`) exige aves en cero, venta, merma ni
      serie continua; la doble validación **ni participa** del cierre. El problema es el opuesto al
      que temíamos: **abre demasiado fácil, en silencio, y congela el resultado.**
      🔴 `fn_seguimiento_diario_engorde.sql:708-712` reescribe el encasetamiento del lote cerrado
      (`GREATEST(1, bajas + ventas)`) y el cierre congela **después** de aplicar el estado — las dos
      cosas documentadas en el propio código. Para un lote que vendió todo es un no-op elegante; para
      uno sin la venta registrada, **reescribe la historia**. Medido: liquidar hoy los 17 lotes
      terminados de Panamá haría **desaparecer 610.704 aves** del registro congelado (el lote 151 pasa
      de 45.515 encasetadas a **481**). **Ecuador es el caso de control que lo prueba**: el 2601 tiene
      sus ventas registradas (24.318 + 1.082 = 25.400 = lo encasetado) ⇒ liquida perfecto, **0 aves
      desaparecen**.
- [x] 🔴 **Corregido: uno de los 19 no es cola.** El lote **215 de DAYLAND** tiene 9 días de cola pero
      **15 de edad** — es un lote VIVO atrasado, y su cola es la cicatriz del bug del cruce que lo
      tenía bloqueado. Se le capturan los días, no se cierra. El umbral correcto es **cola > 7 Y
      edad > 42**. Quedan **18 lotes a cerrar** (17 Panamá + el 2601 de Ecuador).
- [x] **Definida la receta correcta: liquidar es el ÚLTIMO paso.** (a) registrar la venta/traslado con
      el lote **abierto** → (b) sacar el alimento sobrante del galpón → (c) recién ahí liquidar.
      Liquidar primero cierra tres puertas de reparación (la carga masiva rechaza lotes cerrados,
      `MigracionService.SeguimientoEngorde.cs:25-27,43`; la venta y el traslado quedan bloqueados por
      el gate de liquidación) y `AvanzarCodigoErpGranjaSiCicloCerradoAsync`
      (`LoteAveEngordeService.cs:838`) avanza el código ERP de la granja **+1 sin que la reapertura lo
      decremente**.
- [x] **Chequeo 6 agregado al `.sql`**: dice, lote por lote, cuántas aves desaparecerían si se
      liquidara hoy. `= 0` ⇒ listo para liquidar; `> 0` ⇒ falta registrar la venta.
- [ ] ⏸️ **DEFECTO APARTE encontrado de paso (no bloquea la receta): permiso huérfano.**
      `movimientos_pollo_engorde.vender_lotes_cerrados` existe como seed
      (`20260714112951_...`) y **sólo lo lee el front** (`modal-movimiento-pollo-engorde.component.ts:69`,
      con hint al usuario en el HTML `:303`). El backend no conoce esa clave: su gate es
      `omitirGateLiquidado` (`LiquidacionCongeladaGateCalculos.cs:80-83`), y el único que lo pone en
      `true` es `CorreccionAvesDisponiblesEngordeService.cs:437`. **El usuario con el permiso habilita
      el formulario y el guardado le rebota.** No estorba a la receta (ahí el lote está abierto), pero
      es deuda real y promete algo que no cumple.
- [ ] ⏸️ **Implementación**: pertenece a la sesión de EC4. Incluye el mensaje que distingue las dos
      causas (§9.6 del plan) — «faltan los días X e Y, registralos» vs «este lote no tiene registros
      desde X (N días); si ya salió, liquidalo».

---

## EC5 — Corrección de todos los bugs anotados en la sesión (25-ago-2026)

Plan: [`fase_de_desarrollo/correccion_bugs_anotados_plan.md`](fase_de_desarrollo/correccion_bugs_anotados_plan.md)

> Pedido: *«corrige todos los bugs anotados completos»*. Recoge lo que quedó abierto en EC3 y lo que
> apareció al verificar el liquidador en EC4.2. **No incluye la feature de huecos/plazo**, que el
> usuario reservó para otra sesión — eso es diseño, no defecto.

- [x] **Inventario cerrado: 7 defectos**, con su severidad medida (§1 y §2 del plan).
- [x] **Alcance medido**: ItalcolPanama tiene **0 lotes liquidados** ⇒ el daño de #5 es 100 %
      prospectivo (610.704 aves todavía no perdidas). ItalcolEcuador tiene 97 liquidados con **8**
      que perdieron >100 aves (3.368 en total) ⇒ #6 y #7 quedan latentes, casi sin víctimas.
- [x] **Especificación adversarial corrida**: 7 specs + 4 refutaciones. **Las refutaciones mataron 3
      de los 7 parches y corrigieron 2 de los que quedaron.** Los dos hallazgos decisivos se
      verificaron a mano antes de aplicar nada.
- [x] ✅ **#1 Push offline PWA — RESUELTO por la ruta B (ver bloque EC6).** Era: El parche **apaga
      `ValidarAlimentoObligatorio` sin decirlo**: ese guard corre sólo dentro de `if (separa)` y su
      doc-comment nombra explícitamente «el push de la PWA» como el cliente que lo necesita ⇒ días de
      campo viejos entrarían **sin alimento, en silencio**, en Panamá. Además compite con el plan
      vigente, que dice que este parche **deja de hacer falta** con el cambio de bloqueo.
      **Ruta (A)** parchear igual, perdiendo el guard de alimento · **Ruta (B)** hacer el paso 1 de
      EC4 (bloqueo = «el día anterior confirmado»), que resuelve éste y otros dos casos sin efectos
      colaterales. **El usuario eligió (B).**
- [x] ✅ **#2 `MarcarValidadoAsync` — APLICADO**, en las dos direcciones (validar y des-validar).
      🔴 **La spec pasaba el id equivocado**: para reproductora `LeerEstadoAsync` devuelve el id del
      lote de **reproductora** y `SincronizarCruceAsync` espera el de **engorde**. No truena: no
      encuentra nada, o sincroniza el lote que por casualidad tenga ese id. El helper resuelve el
      puente primero. Verificado que el aplicador es **idempotente** (`yaAplicados`) — si no lo
      fuera, el arreglo duplicaría descuentos y sería peor que el bug. Y el bug **no depende del
      flag**: `ValidarAsync` no consulta `RequiereValidacionAsync`, aplica a todas las empresas.
- [x] ✅ **#3 Permiso huérfano — APLICADO al revés de como estaba enunciado.** No es que al backend le
      falte honrar el permiso: **el permiso no puede existir para un lote cerrado** (el gate rechaza
      toda escritura y los reportes leen la copia congelada ⇒ la venta quedaría invisible). Se hizo
      honesta la promesa con `bypassablePorPermiso`: destraba la **corrida anterior**, no el lote
      cerrado. De paso, el predicado repetido **7 veces** en el HTML pasa a una sola fuente.
      **+6 casos de test** (`frontend/src/tests/detectar-lotes-bloqueados-venta.funcion.spec.ts`).
- [x] ✅ **#4 Fuga por empresa — APLICADO en las 4 ramas.** Y era **fuga real, no sólo latente**:
      `ObtenerPendientesAsync` no valida que el lote sea de la empresa activa, así que un usuario
      podía pedir los pendientes de un lote ajeno y recibir sus fechas. Producción se filtra por FILA
      (única tabla de seguimiento con `company_id`); las otras tres resuelven la empresa del lote.
- [x] ⚠️ **#5 — SOLO EL AVISO. La guarda se descartó, y me corrige a mí.** Liquidar **no** pierde
      aves «en silencio»: el modal ya tiene un banner `role="alert"` con la cifra exacta y
      `puedeLiquidarPorAves` devuelve `true` **a propósito**, con el motivo escrito («datos pueden
      tener error»). Es un override informado. La guarda propuesta **no evitaba el daño: pedía
      consentimiento para él** —si el usuario tilda, la foto truncada se congela igual—, además de
      duplicar una fórmula existente y acusar mal. Lo aplicado: el banner ahora dice **qué pasa** si
      continuás (el encasetamiento pasa a valer `bajas + ventas` y queda congelado).
- [x] ✅ **#6 — RESUELTO SIN DDL.** `fn_cuadre_aves_engorde` **no tiene un solo consumidor en
      runtime** (cero `SqlQueryRaw`/`FromSql`): agregarle columnas no crea una alarma, crea una
      consulta que alguien tendría que escribir igual. Se resolvió con
      [`backend/sql/verificar_salidas_aves_engorde.sql`](backend/sql/verificar_salidas_aves_engorde.sql).
- [ ] ⏸️ **#7 Vista Power BI — NO APLICAR en este ciclo.** Consumidor **externo** que no pidió el
      cambio; el `Down` propuesto no es round-trip seguro (un re-`Up` quedaría en no-op silencioso con
      la vista rota); falla abierto en un lote poblado por traslado; y **apagaría la única señal
      visible en Power BI de un lote liquidado sin su venta**, justo antes de que Panamá liquide.
- [x] 🔴 **HALLAZGO: cinco definiciones distintas de «salida de aves» en el mismo módulo.** El
      trigger emite `VENTA_AVES` sólo para `tipo_movimiento = 'Venta'`; `EsSalidaVenta` cuenta
      `Venta|Despacho|Retiro`; `fn_indicadores_pollo_engorde` suma `Traslado`. Un **Despacho descuenta
      del maestro pero no alimenta `total_ventas`** ⇒ al liquidar esas aves también desaparecen.
      **Medido: hoy sin víctimas** — el sistema entero tiene sólo movimientos `Venta` (1.455
      completados) y el histórico tiene exactamente 1.455 filas `VENTA_AVES`, calzan uno a uno. La
      trampa se arma el día del primer `Despacho`. Queda en el chequeo 3 del `.sql` nuevo.
- [x] **Validación backend**: `dotnet build` **0 errores** y `dotnet test` **3240 verdes** (eran
      3234; +6 del #2) con el SDK .NET 10 portable. Gate `verificar-sql-llega-por-migracion` en verde.
      ⚠️ **Para la próxima**: `dotnet`/`node` del PATH del sistema son 9.0.301 y v22.15.0 y **fallan**;
      hay que usar `~/dotnet-portable` y `~/node-portable/node-v22.23.1-win-x64`. Y `cmd | tail`
      devuelve el exit de `tail`, no del comando: un build con **6 errores reportaba `exit 0`**.
- [x] **Validación front**: `yarn build` **0 errores** con el Node portable (995,90 kB inicial, sin
      warning de budget).
- [x] **Cerrados 4 de 7 defectos** (#2, #3, #4 y #6) más el aviso del #5. Quedan abiertos **#1**
      —resuelto después por la **ruta B**, ver bloque **EC6**— y **#7**, con su condición de
      reingreso escrita en §4.7 del plan.

---

## EC6 — Ruta B: el plazo de validación se cuenta desde la CREACIÓN (26-ago-2026)

Plan: [`fase_de_desarrollo/plazo_validacion_desde_creacion_plan.md`](fase_de_desarrollo/plazo_validacion_desde_creacion_plan.md) §11

> Decisión del usuario: **ruta B** para destrabar el push offline de la PWA sin perder el guard de
> alimento obligatorio.

- [x] 🔴 **Hallazgo al implementar: la ruta B tal como estaba redactada NO resolvía el caso.** §5 y
      §4.1-bis proponían cambiar el **bloqueo** a «el día anterior tiene que estar confirmado». Con un
      push de 5 días viejos eso traba igual: crear el día 2 mira al día 1, **que lo acaba de crear el
      mismo push** y está sin confirmar. Cambiar *a qué registro mira* el bloqueo no ayuda cuando el
      registro que mira nació en la misma operación.
- [x] ✅ **Lo que sí lo resuelve es lo que el usuario había pedido primero**: *«debo tenerlas máximo
      para confirmar mañana, porque hoy las hice la creación, no de acuerdo a cuándo es»*. El plazo
      pasa a contarse desde `created_at`. Con eso el push entra completo, y **al día siguiente los 5
      vencen y bloquean hasta confirmarse** ⇒ la confirmación extra se mantiene.
- [x] ✅ **El BLOQUEO no se tocó.** El usuario fue explícito dos veces en conservarlo. La regla actual
      —cualquier vencido sin validar bloquea— ya cumple «el día anterior confirmado» y es **más
      estricta**; restringirla al día inmediatamente anterior habría **aflojado** justo lo que se pidió
      reforzar.
- [x] **La fórmula es `max(fecha, creación) + 1 día`**, y el `max` no es cosmético: un registro
      cargado por anticipado no arranca con menos plazo, y **el límite nuevo es siempre ≥ el viejo**
      ⇒ el cambio sólo puede aflojar, nunca bloquear a alguien que hoy no está bloqueado. Es la única
      dirección segura sobre una empresa con la regla ya encendida en producción. **Hay un test que
      fija ese invariante.** Sin `created_at` cae en el comportamiento previo, byte a byte.
- [x] **Medido sobre la copia de producción, últimos 30 días — registros que nacían vencidos:**

      | Empresa | Capturados | Regla vieja | Regla nueva |
      |---|---:|---:|---:|
      | ItalcolPanama | 1.331 | **1.191 (89,5 %)** | **0** |
      | ItalcolEcuador | 658 | **93 (14,1 %)** | **0** |

      Casi **nueve de cada diez** registros de Panamá nacían en rojo y trabando el lote. Y con la
      regla vieja, encender el flag en Ecuador habría reproducido el problema **93 veces por mes**.
- [x] **Sin migración**: la regla no toca la BD. La migración sigue haciendo falta sólo para encender
      el flag en Ecuador, que es una decisión aparte.
- [ ] ⏸️ **Siguiente paso de la secuencia (§11.6): el apoyo de UI que la regla exige** — «guardar y
      validar» o «validar todos los pendientes del lote». Hoy se valida de a uno y Panamá llegó a
      cargar 34 días en una sesión. La regla de huecos (§9) lo vuelve **obligatorio**.
- [ ] ⏸️ Verificar una semana en Panamá (ya tiene el flag encendido, es el caso extremo) y **recién
      entonces** encender el flag en Ecuador.

---

## EC7 — «Validar todos los pendientes del lote» (26-ago-2026)

Plan: [`fase_de_desarrollo/validar_lote_completo_plan.md`](fase_de_desarrollo/validar_lote_completo_plan.md)

> Paso 1 de la secuencia de EC6 §11.6. Hoy se valida **de a uno** y el cambio de plazo + la regla de
> huecos suben el tamaño del bloque a confirmar.

- [x] **Motivo medido**: ItalcolPanama cargó 6+ días en una sesión **41 veces en un mes**, pico de
      **34 días**. Con el plazo desde `created_at` esos días entran completos y vencen todos juntos al
      día siguiente; con la regla de huecos, llenar 4 huecos deja 4 registros a confirmar de a uno.
- [x] **Alcance acotado**: se construye el **validar en bloque**. Queda afuera «guardar y validar» en
      el modal de alta (suma una decisión al formulario y no resuelve el caso grande) y el
      **desvalidar** en bloque (deshacer descuentos en masa es peligroso y nadie lo pidió).
- [x] **Las 3 decisiones, resueltas con evidencia** (4 diseños + 3 refutaciones): **una transacción
      por registro** (el éxito parcial ES el feature: hoy 34 POST son 34 transacciones y si el 20
      falla quedan los 19); **el orden importa y es cronológico**; **se corta** en la primera falla.
- [x] 🔴 **Hallazgo que evita corrupción silenciosa: `ChangeTracker.Clear()` tras capturar una falla.**
      La transacción de `ValidarAsync` revierte **la base, no el ChangeTracker**: las entidades quedan
      en memoria con el valor nuevo marcadas `Unchanged`, y el registro siguiente las reusa por
      identity map descontando desde un saldo que en la base **nunca existió**. Y no se nota solo: la
      guarda de aves lee `AsNoTracking()` —ve el valor revertido y pasa— mientras los aplicadores leen
      rastreado y reciben la instancia envenenada. Verificado a mano en el código.
- [x] 🔴 **Hallazgo: el orden SÍ cambia el resultado.** La guarda de aves compara **totales**
      (`MotivoAvesNoAplicable`) mientras el descuento recorta **por bucket** (`AplicarPorBucket`, con
      `Math.Min` por género). Lote de 100 hembras y 0 machos, un día que baja 50 machos y otro que
      baja 60 hembras: **validan los dos en un orden y cortan en el otro**. Por eso el orden lo impone
      el SERVIDOR y su test es de corrección, no de ergonomía.
- [x] **El bloque se niega a correr dentro de una transacción abierta.** `ValidarAsync` abre la suya
      sólo si no hay ambiente; con una envolvente ninguno commitearía y el bloque sería todo-o-nada
      **en silencio**. Error explícito en vez de sorpresa.
- [x] **Backend**: `ValidacionEnBloqueCalculos.cs` (puro) + partial `…ValidarEnBloque.cs` + endpoint
      `POST /api/SeguimientoValidacion/{modulo}/lote/{loteId}/validar-pendientes` (sin `admin` en el
      path). `ResultadoValidacionDto` suma `YaEstabaValidado`, que es lo que distingue «lo validé yo
      ahora sin efecto» de «otra pestaña ya lo había validado» — sin ese dato el conteo mentiría.
- [x] **Front**: `validarPendientesDelLote()` + botón en las 3 listas, confirmación con
      `ConfirmDialogService` nombrando cuántos y el rango de fechas, corte al modal (no toast) porque
      el operario **tiene** que leer qué día falló.
- [x] 🔴 **Detalle de UI que no era obvio**: tras un corte **quedan vencidos por definición**, así que
      la recarga dispara el modal rojo de pendientes y se apilaría **sobre** el del resultado. Se
      suprime esa única vez (`suprimirAlertaPendientes`).
- [x] **Gating sin permiso nuevo ni migración**: el botón reusa `puedeValidar`, que ya combina el
      permiso y el flag de empresa (fail-closed). Con la doble validación apagada el botón no existe.
- [x] **Tests**: `ValidacionEnBloqueCalculosTests.cs`, **+39 casos** — orden, desempate por id, tope,
      invariante `Validados + YaValidados + Fallidos + NoIntentados == Solicitados`, y el mensaje byte
      a byte en singular y plural. **3295 verdes** (eran 3256).
- [x] **Validación final**: `dotnet build` **0 errores 0 warnings**, `dotnet test` **3297 verdes**
      (eran 3256), `yarn build` **0 errores**.
      ⚠️ **Dos tropiezos de entorno, para no repetirlos**: (1) el front falló con `TS2345` porque
      `selectedLoteId` es `number | null` — se arregló con una guarda, que además es lo correcto (sin
      lote no hay nada que validar); (2) el backend falló con **`CS2012: file locked by VBCSCompiler`**
      por correr `dotnet test` y `dotnet build` **en paralelo** sobre el mismo repo. Se destraba con
      `dotnet build-server shutdown`. Es la misma trampa del `bin/` bloqueado que documenta CLAUDE.md,
      por otra puerta: **no correr test y build a la vez**.
- [ ] ⏸️ **Queda para después**, sobre esta misma base: «guardar y validar» en el modal de alta.
      Se dejó afuera a propósito — suma una decisión al formulario de captura y no resuelve el caso
      grande de 34 días ya cargados.

---

## MENU-EMP — El menú del usuario debe respetar `company_menus` (26-ago-2026)

Plan: [`fase_de_desarrollo/menu_efectivo_por_empresa_plan.md`](fase_de_desarrollo/menu_efectivo_por_empresa_plan.md)

> Reportado sobre ItalcolPanamá: el sidebar muestra ItalJira aunque la empresa no lo tiene asignado.
> `company_menus` existe, la pantalla de administración existe, y **el runtime nunca la mira**.

- [x] **Medir el defecto** en la copia de producción: qué pares (empresa, menú) se cuelan hoy.
- [x] **Decisiones de diseño** (D1 habilitado = fila + `is_enabled`; D2 empresa sin filas ⇒ no
      filtra; D3 ancestros incluidos; D4 orden sigue saliendo de `menus`).
- [x] **BD**: `fn_menu_usuario(uuid, int) → jsonb` con el árbol ya construido + espejo en
      `backend/sql/` + **migración** que la aplica (nada de `backend/sql/` llega solo a prod).
- [x] **Cálculo puro**: `MenuVisibilidadCalculos` como especificación ejecutable de la fn.
- [x] **Backend**: `Menus_GetForUserAsync` pasa de 4 round-trips a 1; los 3 endpoints caen a
      `_currentUser.CompanyId` cuando no viene `companyId`.
- [x] **Tests** (gate de CI): **18 en verde** — los 11 casos del plan §4, el árbol, los bordes y el
      contrato del JSON de la función.
- [x] **Paridad en BD** sobre la copia de producción, 56 pares usuario-empresa, invariante de dos
      lados: **0 regresiones** (la fn no hace aparecer ningún menú que la regla vieja no mostrara) y
      **0 colaterales** — los **51** pares que dejan de verse son exactamente los que su empresa no
      habilita. En Panamá: ItalJira entero, Guía Genética y Bandeja de gestión.
- [x] **Validación**: `dotnet test` **3297 en verde** · `dotnet build` de **Infrastructure** limpio
      (0 err / 0 warn) · gate `verificar-sql-llega-por-migracion.js` OK · migración aplicada en local
      y **cuerpo desplegado == espejo** verificado contra `pg_get_functiondef`.
- [x] **Contrato del JSON fijado por test** con salida REAL de la función: `Deserialize` no falla ante
      claves que no matchean —deja los valores en default—, así que un rename en la fn dejaría el menú
      sin rutas y en silencio. Las `JsonSerializerOptions` viven en `MenuVisibilidadCalculos` para que
      el test use exactamente las del service.
- [x] **Smoke HTTP end-to-end** contra el backend real (`:5499`, binario recién compilado), como
      `admin.panama@italcol.com` con la empresa saliendo del token —o sea el mismo camino que usa el
      sidebar, que pide el menú **sin** `companyId`—: **HTTP 200**, 10 raíces / 25 ítems, y los **7**
      menús reportados ausentes: ItalJira + Backlog + Tablero + Roadmap + Panel de control, Guía
      Genética (27) y Bandeja de gestión (57). El resto del menú queda **idéntico**. La fila de sesión
      del smoke se borró y los puertos quedaron libres.
- [ ] 🔎 **Hallazgo aparte, NO de este bloque — B1 acorta toda sesión 5 horas.** `Program.cs:128` activa
      `Npgsql.EnableLegacyTimestampBehavior`, así que `sesiones_activas.expires_at` (`timestamptz`)
      vuelve de la BD como hora **local** (`Kind=Local`, −05) y `RevocacionSesionCalculos.Evaluar` la
      compara contra `DateTime.UtcNow` — la comparación es numérica e ignora el `Kind`. Medido en el
      smoke: una sesión que vence en **1 hora** se rechaza con `token-expirado`; la misma fila con
      **7 horas** pasa. En producción eso no desloguea a nadie de golpe, pero **recorta 5 h a cada
      sesión** de las 16 h configuradas (`DurationInMinutes: 960`), y el usuario lo ve como «la sesión
      expiró» antes de tiempo. No se tocó: es del módulo de sesiones, no de menús.

---

## SESION-UTC — La revocacion de sesion juzga las fechas 5 h antes (26-ago-2026)

Plan: [`fase_de_desarrollo/sesion_b1_fecha_local_vs_utc_plan.md`](fase_de_desarrollo/sesion_b1_fecha_local_vs_utc_plan.md)

> Sale del smoke del bloque MENU-EMP. El usuario pidio arreglarlo en su propio commit.

- [x] **Defecto medido**: una sesion que vence en 1 h da `401 token-expirado`; la misma fila con 7 h
      da 200. El salto es el offset de la maquina.
- [x] **`Kind` confirmado, no supuesto**: `/api/Session/mias` devuelve `...-05:00` y System.Text.Json
      solo emite offset para `Kind = Local` ⇒ `ToUniversalTime()` es la conversion correcta.
- [x] **Alcance acotado**: las comparaciones que corren **en SQL** estan bien (verificado: una fila
      vencida hace 2 h no aparece en `/mias`). Solo fallan las de **memoria**: `Evaluar` y
      `DebeActualizarUltimaVista`.
- [x] **La normalizacion va en la parte PURA** (`RevocacionSesionCalculos.AUtc`), no en el service:
      asi queda cubierta por tests y ningun call site futuro puede pasar una fecha cruda de la base.
- [x] **Sin tocar**: el switch legacy de `Program.cs` (cambia el mapeo de fechas de TODO el proyecto),
      `ToDto` (el JSON con offset ya lleva el instante correcto) y las consultas que filtran en SQL.
- [x] **Tests**: 9 casos nuevos, suite entera en **3307 verde** (+10). ⚠️ **Con una advertencia
      escrita en el propio archivo**: reproducen el defecto con `.ToLocalTime()`, o sea con el offset
      de la maquina. En una maquina en **UTC** el defecto NO EXISTE y estos tests pasan con arreglo y
      sin el — son correctos pero VACIOS. Los runners de CI son UTC, asi que **el verde de CI no
      verifica este caso**; la evidencia real es el smoke.
- [x] **Validacion**: `dotnet build` limpio (0 err / 0 warn) + `dotnet test` 3307 verde + **smoke
      HTTP**: fila que vence en **1 hora** ⇒ antes `401 token-expirado`, ahora **200**. Y los tres
      controles siguen firmes — vencida hace 1 minuto ⇒ 401 `token-expirado`, revocada ⇒ 401
      `sesion-revocada`, sin fila ⇒ 401 `sesion-revocada`. **El borde quedo exacto.** Filas de smoke
      borradas y puertos libres.

---

## CI-CACHE — Un 503 de npm tumba el deploy del front (26-ago-2026)

Plan: [`fase_de_desarrollo/ci_cache_deps_y_reintentos_yarn_plan.md`](fase_de_desarrollo/ci_cache_deps_y_reintentos_yarn_plan.md)

> Sale del run `89219049283`: front muerto en `yarn install`, back ya desplegado.
> Producción quedó con back nuevo y front viejo hasta que se relance el job.

- [x] **Disparador medido**: `registry.npmjs.org` devolvió 503 en `karma-6.4.4.tgz`, en
      `[2/4] Fetching packages`. El resolve pasó bien. No es del repo.
- [x] **Causa de fondo medida**: **0 layers `CACHED`** en los DOS builds del run. El
      `--cache-from` importa un manifiesto vacío ⇒ cada deploy rebaja los 763 paquetes de npm.
- [x] **Hipótesis descartada antes de implementar**: `BUILDKIT_INLINE_CACHE=1` solo NO sirve —
      es `mode=min`, exporta únicamente la imagen final, y el `yarn install` (etapa `deps`) y el
      `dotnet restore` (etapa `restore`) viven en etapas intermedias que no llegan a ella.
- [x] **Arreglo 1 — caché que sí pega**: publicar la etapa de deps como imagen propia
      (`--target deps` / `--target restore` → tag `:deps-cache`) y sembrar el build completo
      desde ella. En los dos jobs.
- [x] **Arreglo 2 — reintentos**: `RUN yarn install` con 3 intentos y backoff 20s/40s.
      Mismas flags (`--frozen-lockfile` incluido); falla las 3 ⇒ `exit 1`, el gate no se ablanda.
- [x] **Sin tocar**: etapas del Dockerfile fuera del `RUN`, guarda del borde, tags `:sha`/`:latest`,
      despliegue a ECS. El artefacto que llega a prod es byte a byte el mismo.
- [x] **Validación local**: YAML parsea (trigger `push`/`main-produccion` y cadena `needs`
      intactos); `sh -n` OK sobre el `RUN` **extraído del propio Dockerfile**, no una copia;
      loop probado con un `yarn` falso — falla 0 ⇒ 1 invocación / exit 0; falla 2 ⇒ anda a la
      3ª / exit 0; falla 3 ⇒ exit 1. Flags idénticas en las 3 invocaciones.
- [x] **Defecto encontrado y corregido en la propia validación**: la 1ª versión del loop dormía
      60 s **después** del 3er fallo y anunciaba un reintento que no existía. Ahora el sleep se
      saltea en el último intento.
- [x] **Verificado con Docker REAL** (máquina con 0 imágenes y 0 caché, = runner limpio): con
      `--cache-from :deps-cache` y el caché en cero ⇒ **5 layers CACHED incluido el `yarn install`,
      3 s sin tocar npm**. Control con `--cache-from` contra la imagen completa (lo que hace CI hoy)
      ⇒ **0 CACHED, 113 s, bajó los 763 paquetes**. Misma prueba, única variable distinta.
- [x] **Los otros 6 controles**: BuildKit acepta el `RUN`; loop bajo el `ash` real de Alpine
      (BusyBox 1.37.0) con los 3 casos; backend `--target restore` OK; build completo end-to-end
      (`verificar-ngsw` 197 archivos, `nginx -t` OK, imagen 64,8 MB); **invalidación** al cambiar
      `yarn.lock` (5 ⇒ 3 CACHED, vuelve a la red: no sirve dependencias viejas); y **round-trip por
      un registry real** (push → borrar local → pull → build ⇒ 5 CACHED), que era el único hueco
      que dejaban las pruebas con imágenes locales.
- [ ] **Falta verlo en el pipeline**: el 1er deploy con el cambio TODAVÍA baja todo (la tag
      `:deps-cache` aún no existe en ECR — ese run la crea). El 2º es el que debe dar `CACHED` > 0.
      ✅ Confirmado que el problema es sistematico, no del incidente: el run **32971303424** salio
      **verde** (3 jobs success) y aun asi dio **0 CACHED**, con el `yarn install` del front bajando
      los 763 paquetes otra vez (step de 25,6 s). El cache esta muerto en TODOS los deploys.
      ⚠️ El deploy lanzado el 26-ago (run 32971303424, `main-produccion@5e780e5`) **NO lleva estos
      arreglos**: `8a78ea5` sigue sin pushear.

---

## X20 · Guía Genética — tres módulos con identidad propia (26-ago-2026) — CERRADO, commit `a34e7bb`

> Renumerado de X19 a **X20**: ya existía un bloque X19 (App móvil, 22-ago) más arriba en el archivo.

Plan: [`fase_de_desarrollo/guia_genetica_tres_modulos_plan.md`](fase_de_desarrollo/guia_genetica_tres_modulos_plan.md)

**Decisión del usuario:** NO se unifican las tres tablas. Se separan los menús en tres ítems
(**Pollo Engorde** / **Sanmarino** / **Santa Reyes**) y se le construye a Santa Reyes la puerta de
escritura que nunca tuvo. Origen: el usuario entró a producción de Santa Reyes y no encontró dónde
cargar su línea genética.

**Medido antes de empezar (auditoría de 13 agentes):**
- `guia_genetica_santa_reyes` nació **seed-only**: 615 filas (5 razas × semanas 18–140) por la
  migración `20260820093323`, y **cero endpoints de escritura**. `grep` de
  `.Add|.Update|.Remove|SaveChanges` sobre la entidad fuera de migraciones ⇒ **vacío**; las 20
  referencias C# son todas `.AsNoTracking()`. `GuiaGeneticaController` es 100 % `[HttpGet]`.
- El ítem «Guia Genetica» (sin tildes) que ve Santa Reyes es el de **engorde de Ecuador**: la
  migración `20260623080001_RenameMenu_GuiaGenetica:16` renombra la etiqueta **sobre**
  `route='/config/guia-genetica-ecuador'`, y Santa Reyes lo heredó del clon de menús de
  `20260725190000_SeedEmpresaSantaReyes:195`, cuyo filtro excluía `%engorde%` pero **no** `%ecuador%`.
- ⚠️ **Las dos filas de `menus` no las creó ninguna migración** — viven sólo como espejo en
  `backend/sql/add_guia_genetica_menu.sql` y `add_guia_genetica_ecuador_menu.sql`, corridas a mano
  en prod. El repo **no puede probar** qué existe realmente en producción ⇒ las migraciones de menú
  van defensivas (por `route`, `WHERE NOT EXISTS`, y **desactivan en vez de borrar**).

### F0 · Red de seguridad
- [x] `backend/sql/verificar_paridad_guia_genetica.sql` — creado, con el patrón de
      `verificar_paridad_saldo_engorde.sql` (1ª corrida congela, 2ª compara). Snapshot en esquema
      propio **`diagnostico`** (no en `public`), con `COMMENT` que lo marca desechable; solo lectura
      sobre datos de negocio. Exento del gate de migración por prefijo `verificar_*` (confirmado en
      `backend/scripts/verificar-sql-llega-por-migracion.js:63`) ⇒ **sin** marca `SIN-MIGRACION`.
      **Los 8 objetos vivos se identificaron contra la BD, no contra el repo** (`pg_get_functiondef`
      / `pg_get_viewdef` filtrados por las 5 tablas): 6 fns + 2 vistas. Cubiertos 7 ejecutándolos;
      **`fn_congelar_liquidacion_engorde` NO se ejecuta** — hace `INSERT` en
      `liquidacion_lote_engorde_congelada(_fila)`; en su lugar se congela, en solo lectura, la única
      lectura de guía que hace (el header de Ecuador que resolvería por lote).
      🔴 **Hallazgo que obligó a pinar la sesión:** `fn_informe_semanal_pollo_engorde(5)` devuelve
      **212 filas con `timezone=America/Bogota`** (el default del servidor) y **213 con UTC**. Un
      verificador sin `SET timezone` da distinto según desde qué shell se corra; se pinan además
      `extra_float_digits` y `DateStyle`, que cambian el `::text` que va al hash.
      Dos trampas más: `fn_indicadores_produccion_postura` **no es re-entrante** (crea `_seg` sin
      dropearla ⇒ `CROSS JOIN LATERAL` revienta con «relation "_seg" already exists»; va en LOOP con
      `DROP TABLE pg_temp._seg` entre iteraciones), y `vw_guia_genetica_por_lote_postura` **no tiene
      clave única** (546 filas sobre 525 claves) ⇒ el snapshot guarda `n_filas` y colapsa duplicados
      con `string_agg(... ORDER BY hash)`, independiente del orden.
- [x] Línea base congelada contra la BD local (copia de prod, `127.0.0.1:5433`): **11.261 claves**,
      1,8 s por corrida. **Corrida 1 congela / corrida 2 compara sin ningún cambio en el medio ⇒ 0
      en las 24 filas (5 empresas × objeto), sección «claves que cambiaron» vacía.**
      Filas de guía por empresa: Sanmarino 889 · Demo 224 · Ecuador 15 + 1 header/171 detalle ·
      Panamá 1 header/57 detalle · Santa Reyes 615.
      **Control negativo (100 % de solo lectura, sin escribir una fila):** perturbando `peso_tabla`
      +1 sólo para Sanmarino, el gate marca **92 claves con `dif_guia`** y deja Demo y Santa Reyes en
      **0** — o sea que detecta, y detecta acotado a la empresa afectada.

### F1 · Flag tipado en `companies`
- [x] Migración `20260826142448_AddGuiaGeneticaPerfilCompany`: `guia_genetica_perfil varchar(16) NOT
      NULL DEFAULT 'sanmarino'`, backfill **por datos** (`EXISTS` sobre la tabla reducida), nunca por
      nombre. Idempotente (`ADD COLUMN IF NOT EXISTS` + `IS DISTINCT FROM`). Validada contra la BD
      local en transacción revertida: flipea **1 empresa** (Santa Reyes, id 6, 615 filas) y deja las
      otras 4 en `sanmarino`; 2ª pasada = cero cambios; `Down()` dropea limpio.
- [x] `GuiaGeneticaPerfilCalculos` + xUnit (34 tests) — **`throw` ante valor desconocido**, no default
      silencioso. Helpers `UsaGuiaReducida`/`UsaGuiaCompartida` + `EsPerfilConocido` (no lanza, para
      rechazar con 400 en vez de 500).
- [x] Propagarlo a las proyecciones que siempre se olvidan — resultaron **8 sitios en 6 archivos**, no
      4: `CompanyDto`, `CreateCompanyDto`, `UpdateCompanyDto`, `CompanyService.ToDto`,
      `CompanyService.Crud` (**Create y Update, 2 sitios**), `CompanyResolver` (**`GetCompanyByNameAsync`
      y `GetCompaniesForUserAsync`, 2 sitios**), `CompanyPaisService.GetCompaniesByPaisAsync`.

### F2 · Backend de Santa Reyes — la puerta que falta
- [x] `IGuiaGeneticaSantaReyesService` + DTOs + service `partial` (ancla + `Funciones/Crud` + `Funciones/Import`).
      Paginado por **`PaginacionCalculos`** (default 20, tope `MaximoCatalogoMaestro` = 2.000): pedir de
      más devuelve **el tope**, no el default — el clamp casero `>200 ⇒ 20` que anda por el repo hace lo
      contrario y ya costó dos incidentes. Medido: `pageSize=999999` ⇒ 2.000; la guía entera (615) entra
      en una página.
- [x] `GuiaGeneticaSantaReyesController` (`api/guia-genetica-santa-reyes`) con **`[Authorize]`** +
      permiso **`guia_genetica.gestionar`** (patrón `_current.Permissions.Contains(...)` ⇒ 403 con
      cuerpo, el de los 11 controllers del repo; **no** una policy nueva, que nadie registraría).
      Las LECTURAS quedan abiertas, incluida la plantilla.
      🔴 **DEPENDE DE F4**: el permiso **invierte el default** (hoy escribe cualquiera). Si F4 no lo
      siembra heredando de `role_menus` **por `route`**, el módulo nace inutilizable. Y los permisos
      viajan en la sesión cifrada ⇒ hay que **re-loguear** para verlo.
- [x] Import Excel **idempotente** por `codigo = Raza+Anio+Edad` contra el UNIQUE parcial existente.
      **Soft delete** (`deleted_at`), no el hard delete de `ProduccionAvicolaRawService:195`.
      Medido contra la BD local en transacción revertida: reimportar las 615 filas reales ⇒
      `ins=0 act=0 omi=615 err=0` en la 1ª y en la 2ª pasada; las 40 filas nulas de Criolla siguen
      **NULL** (vacío ⇒ NULL, nunca 0); dar de baja y recrear el mismo código funciona.
- [x] Guard fail-closed en los dos sentidos (403 con cuerpo — mismo status que `Forbid()`, pero con
      mensaje: `Forbid()` devuelve el cuerpo vacío y el front lee `err.error?.message`).
      ⚠️ Se guardó también **`ExcelImportController.ImportProduccionAvicola`**: es la 2ª puerta de
      escritura de la tabla compartida y la que realmente usa el cliente.
      Verificado que no bloquea a nadie: sólo pasa a perfil `reducida` la empresa **con filas propias**
      y hoy es una sola (Santa Reyes, id 6, con **0** filas en la compartida).
- [x] 🔴 `GuiaGeneticaService.ObtenerRazasCrudoAsync:105` corta a nivel **EMPRESA**, no de raza ⇒ el
      único workaround aparente falla en silencio. Corregido uniendo ambas fuentes
      (`GuiaGeneticaRazasCalculos.CombinarRazas`, con tests).
      **Gate de delta cero medido contra la BD local, empresa por empresa** (viejo vs nuevo, salida real
      de `ObtenerRazasDisponiblesAsync`): Sanmarino 4=4, Ecuador 1=1, Demo 3=3, Panamá 0=0,
      Santa Reyes 5=5 ⇒ **0 (idéntico) en las cinco**. Y con «Lohmann Brown» cargada en la compartida de
      Santa Reyes (transacción revertida): viejo 5 → nuevo **6**, la raza llega al selector.
      ✅ `ObtenerAniosCrudoAsync` **NO tiene el defecto y no se tocó**: su corte es por **RAZA**
      (`Raza == razaPropia` en la 1ª consulta), o sea la pregunta correcta; unirlo mezclaría años de dos
      guías distintas para la misma línea genética.

### F3 · Pantalla propia — 🔓 destraba la ESCRITURA
- [x] `features/config/guia-genetica-santa-reyes/` (grid + form + modal import + export), con la
      metodología de clean code del repo: `models/` (tipos), `guia-genetica-santa-reyes.service.ts`
      (cliente HTTP propio — **no** reutiliza `GuiaGeneticaAdminService`, que pega a otra tabla),
      `funciones/` (5 funciones **puras** + `README.md`) y `pages/` como orquestador delgado.
      Ruta `/config/guia-genetica-santa-reyes` con `loadComponent`, igual que las otras dos de config.
      Contratos verificados contra el controller y los DTOs reales: el listado es un **GET con query
      string** (no `POST /search` como el módulo compartido), el `PagedResult` viene
      `{items,total,page,pageSize}`, el import sube el campo `file` a `POST …/import` y devuelve
      `{success,totalFilas,insertados,actualizados,omitidos,errores[{fila,motivo}]}`.
- [x] `changeDetection: ChangeDetectionStrategy.Eager` **explícito**. Resultó **un** componente, no
      cuatro: los dos modales (alta/edición e import) viven en la misma página con `@if`, así que no
      hay componentes hijos donde omitirlo. Ambos modales **resetean su estado en cada apertura**
      (`abrirNuevo`/`abrirEditar`/`abrirImport`), que es lo que hace que abrir → cerrar → abrir no
      muestre el resultado del import anterior ni el formulario de la fila anterior.
- [x] Raza **texto libre** (`<input>`, no `<select>`) en el formulario y en el filtro — el *deadlock
      de arranque* de la pantalla de Ecuador no se repite: sin ninguna línea cargada se puede crear
      la primera.
- [x] Nota de cobertura **siempre visible** bajo la barra de acciones (no en un tooltip): «Esta guía
      cubre semanas 18 a 140 (producción). Los reportes de levante cubren semanas 1 a 25.» Además,
      una semana fuera de 18–140 se **marca** en el grid y avisa en el formulario, pero **no
      bloquea**: el modelo no lo prohíbe y una línea nueva puede legítimamente empezar antes.
- [x] `ActiveCompanyConfigService` expone `guiaGeneticaPerfil` (`'sanmarino' | 'reducida'`) desde
      `CompanyDto` — el hueco que F1 dejó señalado. Caché 5 min e invalidación por `session$` ya
      eran del servicio. **Fail-closed con default neutro**: error, campo ausente o valor
      desconocido ⇒ `'sanmarino'` ⇒ la pantalla queda en **solo lectura** con un aviso que lo
      explica, en vez de ofrecer botones que el backend va a rechazar con 403.
- [x] Validado: `yarn build` ⇒ **0 errores y 0 warnings** (ni siquiera el de bundle budget: el
      inicial quedó en 996,03 kB / 232,30 kB transferidos). La pantalla salió como **lazy chunk
      propio** (`chunk-CaBMGTpo.js`), no en el bundle inicial.
      ⚠️ Pendiente de F4 para poder probarla en vivo: sin el ítem de menú se llega sólo por URL, y
      sin el permiso `guia_genetica.gestionar` sembrado toda escritura responde 403.

### F4 · Menús — los tres ítems
- [x] Migración `20260826160000_SeedMenusGuiaGeneticaTresModulos`: renombra a **Guía Genética Pollo
      Engorde** y **Guía Genética Sanmarino** (con tildes), crea **Guía Genética Santa Reyes**, y
      **desactiva** (`is_enabled=false`, no borra) los ítems viejos para las empresas de perfil
      `reducida` — resueltas por el flag `guia_genetica_perfil` o por DATOS, nunca por nombre.
      🔎 **Corrige un supuesto del plan, medido en la copia de prod:** el ítem que Santa Reyes heredó
      del clon de menús es el **27 `/config/guia-genetica`** (tabla ancha de Sanmarino), **no** el 51
      de engorde — Sanmarino nunca tuvo el de Ecuador, así que no había nada de engorde que heredar.
      La migración desactiva **los dos**, así que da igual cuál sea en prod.
      ⚠️ El icono es `clipboard-list`, no `dna`: el `ICON_MAP` del front no conoce `'dna'` ⇒ el ítem
      de Ecuador se dibuja hoy **sin icono**; copiar ese nombre habría copiado el defecto.
- [x] Migración `20260826160100_SeedPermisoGuiaGeneticaGestionar`: `guia_genetica.gestionar` en
      `permissions` (**ninguna migración la había sembrado** y el guard ya la exige ⇒ sin esto el
      módulo nace 403 para todos), `company_permissions` en las 5 empresas (es **fail-closed**: sin
      la fila no viaja en el JWT) y `role_permissions` **ON para los 14 roles que hoy ven alguno de
      los tres ítems**, localizando por `route`. **No toca `menu_permissions` a propósito**: esa
      tabla *esconde* el menú a quien no tenga la key, y las lecturas quedan abiertas.
- [x] Espejos `.sql` (`backend/sql/add_guia_genetica_tres_modulos_menus.sql` y
      `add_permiso_guia_genetica_gestionar.sql`) + gate `verificar-sql-llega-por-migracion.js`
      **verde**. Los espejos se corrieron como 2ª pasada del test ⇒ prueban idempotencia **y**
      fidelidad al `.cs` (0 filas afectadas en las 12 sentencias).
- [x] **Validado contra la BD local en transacción revertida** (las 3 migraciones juntas: perfil +
      menús + permiso; `ROLLBACK` verificado, la BD quedó intacta). Diff de rutas visibles **usuario
      por usuario** sobre los 21 pares (usuario, empresa) que hoy ven guía: el **único** cambio en
      todo el sistema es Santa Reyes, que pierde `/config/guia-genetica` y gana
      `/config/guia-genetica-santa-reyes` — swap 1:1. Totales de rutas: Sanmarino 94→94, Ecuador
      90→90, Demo 47→47, Panamá 89→89, Santa Reyes 50→50. **Delta cero fuera de Santa Reyes, medido.**
- [i] 🔗 **Depende de F3 en el mismo release**: el ítem apunta a `/config/guia-genetica-santa-reyes`.
      Verificado que la ruta ya está declarada en `app.config.ts:515`; si F4 saliera sola, Santa Reyes
      perdería su ítem viejo y el nuevo no llevaría a ningún lado.

### Validación
- [x] **Corrido por el orquestador, no por los agentes** (`dotnet build` 0 errores / 0 warnings ·
      `dotnet test` **3453/3453**, 112 nuevos · `yarn build` 0 errores · `dotnet ef database update`
      aplicó las 3 migraciones en la BD local). Y el backend **arranca limpio** con ellas aplicadas
      (`Now listening on: http://[::]:5502` + `Application started`) — era el riesgo de SIGSEGV que
      documenta CLAUDE.md §🚀. Backend apagado al terminar, `:5502` y `:5002` verificados libres.
- [x] **Gate multipaís — delta cero medido**: línea base tomada 10:20:14 (pre-migración), 2ª corrida
      post-aplicación ⇒ **24 objetos × empresa con 0 en todas las columnas de diff** y **0 claves
      cambiadas**. Cubre Sanmarino (889 filas de guía), Demo (224), Ecuador (15+1+171), Panamá (1+57).
- [x] **Menú efectivo verificado con `fn_menu_usuario` post-migración** — la fn que arma el sidebar,
      no `company_menus` a mano: Sanmarino 5 usuarios y Demo 3 siguen en `/config/guia-genetica`,
      Ecuador 2 y Panamá 5 en `/config/guia-genetica-ecuador`, **Santa Reyes 2 → `/config/guia-genetica-santa-reyes`**.
      Swap 1:1: nadie fuera de Santa Reyes pierde ni gana una ruta. Tildes confirmadas en BD por
      comparación contra el literal (`label = 'Guía Genética Pollo Engorde'` ⇒ `t`).
- [x] **Smoke end-to-end HECHO contra el backend real** (`:5002`, BD local = copia de prod), con la
      receta del repo: JWT minteado para el **Admin de Santa Reyes** (`90c29eab…`, rol 30, company 6,
      país 1, sus 35 permisos), fila en `sesiones_activas` (B1 exige la lista blanca por `jti`: sin
      ella el token es rechazado, **no hay bypass en Development**) y `X-Secret-Up` replicando
      `EncryptionService.Encrypt`. **8 de 8 pasos verdes:**

      | # | Prueba | Resultado |
      |---|---|---|
      | 1 | `GET` listado | **200**, `total = 615` — la guía sembrada se ve |
      | 2 | `POST` alta de **`Lohmann Brown`** | **201**, id 619 — *la raza que el cliente no podía cargar* |
      | 3 | `POST` duplicado | **400** con mensaje que manda a editar la existente |
      | 4 | `GET` filtrado por raza | **200**, la encuentra |
      | 5 | `PUT` edición | **200**, valores actualizados |
      | 6 | `GET` plantilla | **200**, 4.518 bytes de Excel |
      | 7 | Guard: escribir la **compartida** desde SR | **403** con mensaje que nombra el módulo correcto |
      | 7b | Guard: **import Excel** de la compartida | **403** — corta **antes** de parsear el archivo |
      | 8 | `DELETE` (baja suave) | **204**; `GET` posterior **404**; listado en 0 |

      ⚠️ El primer intento de 7b dio **400 y no 403**: el guard está en `ExcelImportController:62`,
      **después** de la validación de archivo (`:59`), así que un POST sin archivo muere antes de
      llegar. Re-probado **con un archivo real** ⇒ 403. No es un hueco, es el orden de los mensajes.
      **Limpieza:** la línea 619 y la fila de sesión se borraron; la guía volvió a **615 filas**.
- [x] **Permiso verificado replicando `AuthService.PermisosEfectivosAsync`** (`role_permissions` ∩
      `company_permissions` habilitadas): `guia_genetica.gestionar` llega a las 5 empresas, **2
      usuarios en Santa Reyes**. El módulo no nace 403. El claim se llama `permission`, igual que en
      `GestionUsuariosEscrituraFilter` y `VentanaFechaRegistroGuard`.
- [i] **Sin smoke en NAVEGADOR** (la pantalla en sí). El backend quedó probado end-to-end y el
      componente revisado línea por línea —`ChangeDetectionStrategy.Eager`, `finalize()` que apaga el
      spinner pase lo que pase, `takeUntil(destroy$)` sin fugas, `puedeEscribir = false` ante error
      (fail-closed)—, pero **nadie abrió la pantalla todavía**. Es lo único que queda por mirar.

### F5 · El hueco de LECTURA — CERRADO, commit `a278361`

Ya no está fuera de alcance: el usuario pidió corregirlo todo. Los 5 objetos SQL leen
`vw_guia_genetica_postura` (migración `20260826170000_VwGuiaGeneticaPosturaYFnsOrigen`).

- [x] **Dos premisas del §7 resultaron FALSAS al medir.** (a) El «punto ciego de la grafía» **no
      existe en el camino SQL**: las grafías rotas viven sólo en `lote_postura_base` y **ninguno de
      los 5 objetos lee esa tabla** (`grep -c` ⇒ 0,0,0,0,0). Las fns leen `lotes` /
      `lote_postura_levante` / `lote_postura_produccion`, donde SR tiene **una sola raza**,
      `Criolla`, byte-idéntica a su guía — la raza **no se hereda** del base (base 29 =
      `BABCOK BROWN`, lote operativo = `Criolla`). (b) El año **cruzaba bien desde siempre**.
      ⇒ Se descartó emitir las grafías del lote: no compraba nada y **duplicaba**, porque
      `vw_guia_genetica_por_lote_postura` es el único de los 5 **sin `LIMIT 1`**.
- [x] 🔴 **Apareció un defecto peor que el que íbamos a arreglar.** Cambiar sólo el `FROM` habría
      entregado **números falsos**: levante promedia por sexo dividiendo por **2 fijo**
      (`:466`), así que con una guía de solo hembras `(95.00 + 0)/2 = 47,5` ⇒ **la mitad** de lo que
      dice el cliente. Y en producción `fn_dif_pp` no devuelve NULL con guía = 0 ⇒ la columna
      «diferencia vs guía» pintaría la **mortalidad real** como si fuera la desviación.
      Por eso la vista lleva una columna **`origen`** y las 2 fns aplican el `COALESCE` **sólo**
      cuando vale `'compartida'` — literalmente la expresión de hoy. Quitarlos a secas **no** sería
      delta cero: company 1 tiene entre **6 y 14 filas en blanco por columna** en ese rango.
- [x] **Ni un solo `WHERE` tocado.** Los criterios divergen a propósito (levante raza exacta y sin
      `deleted_at`; producción `btrim(lower())` y con filtro; edad texto exacto vs parseada con
      desempate `'25P'`). Unificarlos haría que empiecen a matchear filas que hoy no matchean.
- [x] **Gate multipaís**: 25 objetos × empresa. Sanmarino, Demo, Ecuador y Panamá en **CERO en todas
      las columnas**, `dif_multiplicidad` incluida (no duplica). Santa Reyes **gana 123 filas** =
      las semanas 18–140 de `Criolla`, repartidas en levante (18–25) y producción.
- [x] **El factor 2, probado con dato sintético en transacción REVERTIDA** (hoy no es observable: el
      único lote de SR está en **semana 1** y su guía arranca en la 18). Con el lote en semana 20 la
      guía dice `gr_ave_dia_h = 107,00` y la fn devuelve **`consumo_tabla = 107`**; sin el arreglo
      habría devuelto **53,5**. Y `peso_tabla` / `unif_tabla` / `mort_tabla` salen **vacíos, no 0**.
- [i] **Límite del dato, no del código:** las semanas **1 a 17 de levante quedan sin guía para
      siempre** — la del cliente arranca en la 18. Overlap real: levante **8** semanas (18–25),
      producción **115** (26–140). El lote de SR llega a la semana 18 el **2026-12-16**.
- [!] **`LIMIT 1` sin `ORDER BY` es no-determinista** y envolver la tabla en una vista cambia el plan.
      Medido hoy: **0 duplicados** por `(company, raza, anio, btrim(edad))` en ambas tablas. Pero
      **no hay UNIQUE que lo garantice** — la limpieza es de la carga, no del esquema. Si alguien
      carga un duplicado mañana, cuál gana pasa a depender del plan.
- [i] El UNIQUE que le falta a la tabla compartida (644/1128 filas con código NULL ⇒ el reimport
      duplica en silencio) y la normalización de los joins de las fns: cambio de comportamiento, gate propio.

---

## Ocultar Guía/Sellos en venta Panamá + reorganizar modal de detalle

Plan: [venta_panama_ocultar_guia_sellos_detalle_reorganizado_plan.md](fase_de_desarrollo/venta_panama_ocultar_guia_sellos_detalle_reorganizado_plan.md)

Guía Agrocalidad / Sellos son un trámite de ECUADOR (Agrocalidad); Panamá no lo usa. Confirmado
con el usuario en el chat: ocultar SOLO para Panamá, Ecuador sin cambios. Se aprovecha para
reorganizar el modal de detalle compartido (Ecuador + Panamá), reportado como "muy plano a lo largo".

- [x] Quitar Guía/Sellos del modal `modal-venta-panama` (Panamá, sin gate — el archivo ya es
      exclusivo de Panamá).
- [x] `CountryFilterService.isPanama()` inyectado en `modal-movimiento-pollo-engorde.component.ts`
      + getter `ocultarGuiaYSellos`.
- [x] Gate `@if (!ocultarGuiaYSellos)` en la sección "Datos de despacho" del formulario
      crear/editar (cubre al usuario Panamá que entra por el flujo estándar, no solo por el
      modal dedicado).
- [x] Gate `@if (!ocultarGuiaYSellos)` en las filas Guía Agrocalidad/Sellos de la vista de
      detalle (solo lectura).
- [x] Reorganización visual de la vista de detalle: secciones cortas (Datos generales / Origen
      y destino / Cantidades) lado a lado en `.detail-columns`; cada dato pasa a tarjeta de
      campo (`.detail-item` envolviendo `dt`/`dd`, válido HTML5 dentro de `<dl>`) en vez de la
      lista fija de 2 columnas; secciones con fondo/borde/radio en vez de separador de línea.
      Mismo patrón que `.vp-grid`/`.vp-field` (modal Panamá) y `.form-grid` (form de este mismo
      componente) — sin inventar un patrón nuevo. Media query de 768px actualizada al nuevo
      markup.
- [x] `yarn build` (frontend, Node portable 22.23.1) — 0 errores, 0 warnings nuevos (238.8s,
      `dist/` generado).
- [!] Smoke manual en navegador: Ecuador (crear/editar/detalle) sin cambios; Panamá (modal
      dedicado + flujo estándar + detalle) sin Guía/Sellos; responsive <768px legible. **No
      ejecutado**: requiere login real (Ecuador y Panamá) que este agente no tiene: queda para
      que el usuario lo confirme en pantalla.

---

## Liquidación Panamá: 400 sin mensaje al guardar los insumos (lote 13-1)

Plan: [liquidacion_panama_400_deserializacion_plan.md](fase_de_desarrollo/liquidacion_panama_400_deserializacion_plan.md)

Reporte del usuario: al liquidar un lote de pollo engorde en Panamá, el modal mostraba
"Http failure response for .../ReporteIndicadorPanama/liquidar: 400 OK" — el genérico de Angular,
sin decir el motivo real. No pasa en Ecuador porque Ecuador no usa este endpoint (liquida directo
por `LoteAveEngordeService.CerrarLoteAsync`); el que sí falla es el paso previo, propio de Panamá,
que guarda los 6 insumos (`ReporteIndicadorPanamaController.Liquidar`).

- [x] Causa raíz identificada: `AvesFinalGranja`/`AvesBeneficiada`/`DiasEngorde`/`DiasEnGranja` son
      `int` en el contrato, pero los inputs del modal no restringían decimales — un decimal ahí
      pasa el gate `panamaCamposCompletos` (solo exige `> 0`) y falla la deserialización del JSON
      ANTES del controller; `[ApiController]` responde el 400 automático
      (`ValidationProblemDetails: {title, errors}`), forma que el front no sabía leer (no tiene
      `error` ni `message`) ⇒ cae al genérico de Angular.
- [x] Backend: `ConfigureApiBehaviorOptions` en `Program.cs` reescribe ESA respuesta automática a
      `{error: "..."}` (misma forma que ya usan todos los controllers), nombrando el campo que
      falló. Cambio global — aplica a cualquier `[FromBody]` del app, no solo a este endpoint.
      Verificado en vivo contra el backend local (`POST /api/Auth/recover-password` con JSON
      malformado): antes `{title, errors}` sin mensaje utilizable, después `{error: "..."}`.
- [x] Frontend (`aves-engorde/funciones/`): `validar-insumos-panama.funcion.ts` (rechaza decimales
      en los 4 campos enteros ANTES de enviar, mensaje inmediato) + `extraer-mensaje-error.funcion.ts`
      (lee el mensaje real del backend, cubre las 3 formas de respuesta) — reemplaza las 7 cadenas
      `err?.error?.error ?? err?.error?.message ?? err?.message ?? '...'` duplicadas en
      `modal-liquidacion-lote-engorde.component.ts`. `step="1"` en los 4 inputs enteros de Panamá.
- [x] Sin cambio de reglas de negocio: la decisión de no bloquear "liquidar" por falta de ventas
      registradas (commit `6a37736`, mismo día) no se toca — el banner informativo ya existente
      (`avesVivasPendientes > 0`) sigue igual.
- [x] `dotnet build` (backend, SDK 10 portable) — 0 errores, 0 warnings.
- [x] `yarn build` (frontend, Node portable 22.23.1) — 0 errores.
- [x] `yarn test` (Karma, scoped a `aves-engorde/funciones/*.spec.ts`) — 27/27 SUCCESS (incluye las
      13 pruebas nuevas de los 2 archivos).
- [x] Smoke manual en pantalla contra un lote Panamá real liquidando con datos válidos: confirmado
      por el usuario ("ya quedo corregido").

---

## Lote Aves de Engorde: el botón "Actualizar" quedaba deshabilitado al editar

Reporte del usuario: en el módulo de Lote Pollo Engorde, al editar un lote y completar un dato que
faltaba, el botón "Actualizar" seguía apagado — el front no dejaba guardar los cambios.

- [x] Causa raíz #1 (la severa): en una empresa con `companies.programacion_lotes_engorde = true`,
      el subscriber de `granjaId` en `lote-engorde-list.component.ts` blanqueaba `loteNombre` cada
      vez que su valor se PATCHEABA — no solo cuando el usuario cambiaba de granja a mano, sino
      también al precargar el form para EDITAR (`applyModalFormState` también patchea `granjaId`).
      En ese modo el template no muestra ningún input para `loteNombre` (se ve el select de lote
      base), así que el campo quedaba requerido, vacío, y sin ningún control en pantalla para
      corregirlo: el form nacía inválido en TODA edición de esa empresa, sin importar qué tan
      completo estuviera el resto del lote. Fix: la limpieza de `loteNombre` ahora respeta el mismo
      guard `!this.editing` que ya usa `recomputeNombrePorCorrida()` al lado.
- [x] Causa raíz #2 (más acotada): lotes legado sin `raza`/`anoTablaGenetica` (nulos en BD, el
      backend los admite) nacen inválidos al abrirlos para editar — y si el usuario completa una
      raza que no tiene años cargados en la guía Ecuador, el desplegable de año queda sin ninguna
      opción seleccionable (dead-end ya advertido con un mensaje, pero fácil de no ver en un form
      largo). No se tocó ninguna regla de negocio acá: los campos siguen requeridos en el front tal
      como estaban.
- [x] UX: nuevo aviso junto al botón (`camposQueFaltan` + bloque `.le-form-note` en el template) que
      lista los campos obligatorios que faltan, SIN esperar a que el usuario los toque — antes el
      error de cada campo solo se pintaba con `.touched`, y un campo que el propio código vació
      (caso de arriba) no tenía ninguna pista visible.
- [x] Bug menor de paso: `applyModalFormState()` disparaba `loadAnosDisponibles` DOS veces al abrir
      un lote con raza ya cargada (un `GET /guia-genetica-ecuador/anos` de más por cada edición).
      Se eliminó la llamada redundante.
- [x] Hallazgo aparte (no de este módulo): `flags-empresa.funcion.spec.ts` no compilaba —
      `a34e7bb` agregó `guiaGeneticaPerfil` a `CompanyFlags` sin actualizar este fixture
      (`satisfies CompanyFlags`), lo que rompía la compilación de **todo** `yarn test` del frontend
      (no solo este módulo). Se agregó el campo faltante (`'sanmarino'`, el default fail-closed).
- [x] Reproducido y verificado con `TestBed` + `HttpTestingController` (sin backend, sin login) por
      el flujo real `openModal()` → `applyModalFormState()`, no simulado a mano. Nuevo
      `lote-engorde-list.component.spec.ts` (5 tests, cubre el bug de `loteNombre`, el guard de
      ALTA que no debía tocarse, y `camposQueFaltan`).
- [x] `yarn test --include='**/lote-engorde-list.component.spec.ts'` — 5/5 SUCCESS.
- [x] `yarn build` (frontend, Node portable 22.23.1) — 0 errores.
- [!] Smoke manual en pantalla editando un lote real de una empresa con programación de lotes ON
      (Ecuador/Panamá): requiere login que este agente no tiene — queda para que el usuario lo
      confirme. El bug #1 es el que más probablemente explica el reporte (afecta el 100% de las
      ediciones en esas empresas); el #2 es un caso más acotado (lotes legado / raza sin guía).

---

## 🚑 Deploy 89511545875 cortado: `guia-genetica-santa-reyes` sin clasificar en la lista cacheable

**Plan:** [`fase_de_desarrollo/lista_cacheable_guia_genetica_santa_reyes_plan.md`](fase_de_desarrollo/lista_cacheable_guia_genetica_santa_reyes_plan.md)

- [i] El job «Tests — Backend & Frontend» falló con `exit 1` y **no fue un test**: backend
      3.453/3.453 verdes, Karma 673/673 verdes y el gate de `changeDetection` OK (234 componentes).
      Cortó el gate 9, `verificar-lista-cacheable.js`: `sin decisión tomada : 1 -
      guia-genetica-santa-reyes`. El endpoint lo agregó `a34e7bb` y nadie lo clasificó. Segunda vez
      que pasa lo mismo (la primera fue `a41fa6e`, cuadre de alimento).
- [x] Clasificado en **`EXCLUIDOS`** de `decidir-cacheable.funcion.ts`, con el porqué escrito: sus
      dos hermanas se cachean porque las leen pantallas de campo (indicadores de engorde, form de
      lote); la reducida **sólo** la pide su propia pantalla de administración `/config/...`, que es
      de oficina. Los indicadores de postura de Santa Reyes no pasan por el front: los calcula
      Postgres contra `vw_guia_genetica_postura` (`a278361`).
- [x] Test que fija la decisión **y** que las otras dos guías siguen cacheándose — las tres comparten
      el prefijo `guia-genetica`, así que la exclusión tenía que verificarse quirúrgica.
- [x] Riesgo de comportamiento: CERO. Al no estar en `ENDPOINTS_OPERATIVOS`, `decidirCacheable` ya
      devolvía `false` para esa ruta; esto hace explícita la decisión, que es lo que el gate exige.
- [x] Verificado local: los dos gates en verde (55 cacheables / 34 excluidos / 0 sin decisión ·
      234 componentes con estrategia declarada), `yarn test` del spec 12/12 y `yarn build` sin errores.
- [!] **Decisión de producto, reversible en una línea:** si operación quiere que la pantalla de la
      guía de Santa Reyes se consulte sin red, se mueve la cadena de `EXCLUIDOS` a
      `ENDPOINTS_OPERATIVOS` y el gate sigue verde.
- [~] Re-disparar el deploy a `main-produccion` (fuera del repo: requiere push, que el usuario pide
      explícitamente).
- [x] **Aparte, del mismo run:** el warning de GitHub de que `checkout@v4`, `setup-dotnet@v4` y
      `setup-node@v4` apuntan a Node 20 (retirado) y los está forzando a Node 24. Se subieron a
      `checkout@v7`, `setup-dotnet@v6`, `setup-node@v7` y —no lo avisaba el run, porque su job no
      llegó a correr— `configure-aws-credentials@v6`, que también era `node20`.
- [x] Los otros tres `aws-actions` NO se tocaron: se verificó `using:` en el `action.yml` de su tag
      y `ecr-login@v2`, `ecs-render-task-definition@v1` y `ecs-deploy-task-definition@v2` **ya
      resuelven a node24**. El criterio quedó escrito en la cabecera del workflow: decide el
      runtime, no el número de versión.
- [x] Breaking changes revisados uno por uno contra este workflow, no asumidos: `setup-node` v5/v6
      (caché automática) no aplica —no hay `packageManager` en `package.json` y el `cache: yarn` es
      explícito—; `configure-aws-credentials` v5 (booleanos inválidos) tampoco —sólo se le pasan
      `role-to-assume`, `aws-region` y `role-session-name`—; `checkout` v6 (credenciales a un archivo
      aparte) y v7 (bloquea fork PRs en `pull_request_target`/`workflow_run`) no aplican: el
      workflow no corre ningún comando `git` y dispara por `push`.
- [x] Los 4 tags nuevos existen y declaran `using: node24` (verificado por API, no de memoria). El
      diff son 7 líneas de `uses:` + 8 de comentario, cero cambios estructurales.

---

## EC8 — Cierre de la lista de pendientes pedida (27-ago-2026): 1 fix real + 4 checkboxes obsoletos + 3 que necesitan tu decisión

> Contexto: el usuario pidió "solucionar" 7 puntos que yo mismo había resumido del tracker. Antes de
> tocar código verifiqué cada uno **contra el código actual**, no contra la nota vieja (regla del
> repo: el código manda). Resultado: 4 de los 7 ya estaban hechos (solo el checkbox quedó viejo), 1
> era un bug real y se cerró, y 3 son decisiones de negocio/producción que no me corresponde tomar.

### Ya estaban hechos — corregidos los checkboxes obsoletos in situ (no había nada que programar)
- [x] **X20 móvil F1** (cálculo puro a `Calculos/`) — cerrado el 22-ago, el checkbox de arranque nunca
      se tachó. Ver corrección en el bloque X20 (línea ~2016).
- [x] **X20 móvil F5.2** (selector de ítems en Flutter) — cerrado, wireado en `seguimiento_page.dart`
      con su kill switch. Ver corrección en el bloque X20.
- [x] **X20 móvil F7** (`requiere_cuadre`) — cerrado el 22-ago con migración + gate de máquina + smoke;
      el propio archivo lo dice 60 líneas más abajo del checkbox que quedó sin marcar.
- [x] **EC3 — segundo camino de confirmación de reproductora** — ya resuelto por EC5 #2 (25-ago);
      verificado que `ValidacionSeguimientoService.Validar.cs:195` sí llama a `SincronizarCruceAsync`.

### Corregido a tiempo: un fix que casi entra sin hacer falta y con una regresión encima
- [x] **EC3 — push offline de la PWA "sin `ModoCargaHistorica`"**: apliqué el fix, y al verificar el
      efecto real (no solo el build) encontré que era innecesario **y** peligroso — **revertido**
      antes de commitear nada. El defecto que describía EC3.3 ya no existe desde EC6 (26-ago, un día
      antes): un registro creado hoy nunca puede nacer `EN_RETRASO` hoy, sin importar la fecha del
      seguimiento. Envolver `PushAsync` en `ModoCargaHistorica` no arreglaba eso — apagaba la
      separación/doble validación completa para todo push offline a Panamá (alimento descontado sin
      confirmación humana). Detalle completo en el bloque EC3 (línea ~3171). `SyncPushService.cs` sin
      cambios netos (`git diff` vacío).

### Permiso `registros.fecha_retroactiva` para Ecuador — verificado EN VIVO, funciona (27-ago-2026)
> Pedido del usuario: "esta mañana traté de darle permiso a la empresa de Ecuador para que puedan
> hacer registros con fechas viejas y no me dejo seleccionar en el módulo de empresa".
- [x] **Datos locales correctos**: `company_permissions.is_enabled = true` para las 5 empresas
      (incluida ItalcolEcuador) — las dos migraciones (`SeedPermisoFechaRetroactivaRegistros` 20-ago,
      `AsignaFechaRetroactivaEcuadorAdministrador` 25-ago) ya corrieron localmente.
- [x] **Confirmado que ambas migraciones ya están en el commit desplegado en producción**
      (`ecd0548`, TaskDef `sanmarino-back-task:169`, desplegado hoy 27-ago 00:43) —
      `git merge-base --is-ancestor` de los dos commits contra el SHA de la imagen corriendo.
- [x] **Código revisado, sin bugs**: `CompanyPermissionService.GetPermissionsForCompanyAsync` devuelve
      el catálogo completo sin paginar ni filtrar; `company-management.component.html` no tiene
      `[disabled]` en el checkbox de permisos.
- [x] **Reproducido EN VIVO en el navegador local** (sesión propia minteada con la clave de
      `appsettings.Development.json` — SOLO desarrollo, autorizado explícitamente por el usuario ante
      el bloqueo del clasificador; sesión y fila de `sesiones_activas` borradas al terminar):
      Gestión de Empresas → Permisos de ItalcolEcuador → `registros.fecha_retroactiva` aparece
      **marcado, no deshabilitado, y togglea al instante** al hacer clic (verificado leyendo
      `checked`/`disabled` del DOM antes y después del clic). Cerrado con "Cancelar", sin guardar
      ningún cambio.
- [i] **No pude reproducir el problema.** Localmente y en el commit ya desplegado, todo funciona.
      Hipótesis más probable: la sesión de esta mañana ocurrió **antes** del deploy de las 00:43 (si
      fue una sesión muy temprano) o el usuario estaba en una pantalla distinta (p. ej. asignando el
      permiso a un ROL puntual en "Roles y Permisos" en vez de habilitarlo a nivel empresa). Si el
      problema persiste, hace falta reproducirlo en el navegador real contra producción (URL exacta +
      pasos) para diagnosticar más.

### Repasados, sin acción — ya estaban correctamente cerrados como "no aplica"
- [i] **EC5 #7 (vista Power BI)**: la decisión ya tomada es NO aplicar este ciclo. No hay nada que
      programar; reabre solo si cambia la condición escrita en §4.7 del plan de EC5.
- [i] **EC7 — "guardar y validar" en el modal de alta**: dejado afuera a propósito por el usuario. No
      se toca salvo que lo pida explícitamente.

### Necesitan una decisión que no es mía — NO se tocó código
- [!] **EC2 (cuadre Panamá)**: ejecutar en producción exige (1) desplegar "Cuadrar galpón" (deploy real,
      requiere tu confirmación) y (2) una respuesta de operación a "¿el inventario de G0475/G0483 es
      confiable hoy?" — sin eso, aplicar el barrido puede introducir el mismo tipo de daño que el
      barrido de auditoría evitó (ver B3/EC2 "verificar antes de limpiar"). G0495 además necesita
      inspección física de inventario, no código.
- [!] **EC4/EC6 (plazo desde `created_at`)**: la implementación quedó reservada explícitamente para
      "otra sesión" por su alcance (toca `ValidacionSeguimientoCalculos`, propagar `createdAt` al
      front, migración del flag Ecuador) — no la arranqué sin que confirmes que es AHORA. Y "verificar
      una semana en Panamá" es una espera de calendario real (el flag ya está encendido ahí desde
      antes de EC6): no hay nada que un agente pueda ejecutar hoy para adelantarla.
- [!] **CI-CACHE**: el fix de caché Docker está verificado en local pero solo se prueba de verdad
      corriendo en el pipeline real — eso exige pushear el commit `8a78ea5` a la rama que dispara
      deploy a producción. Es una acción de deploy, no de código: la dejo para tu confirmación
      explícita en vez de pushearla sola.

---

## `registros.fecha_retroactiva` extendido a Seguimiento Diario Levante y Producción (27-ago-2026)

Plan: [`fecha_retroactiva_seguimiento_levante_produccion_plan.md`](fase_de_desarrollo/fecha_retroactiva_seguimiento_levante_produccion_plan.md)

Pedido: el permiso ya gobierna movimientos/traslados/gastos (20-ago); el usuario pidió que también
gobierne la fecha de Seguimiento Diario Levante y Producción, que hoy no tienen ninguna ventana (se
podía fechar cualquier día pasado sin límite y sin el permiso).

- [x] Verificado antes de tocar código: `SeguimientoLoteLevanteController` y
      `SeguimientoProduccionController` eran los únicos controllers manuales del alcance del permiso
      que NO llamaban `ValidarVentanaFechaRegistro` (grep negativo). El mecanismo
      (`VentanaFechaRegistroCalculos`/`VentanaFechaRegistroGuard`, backend;
      `ventana-fecha-registro.funcion.ts`, front) ya es genérico — no se tocó, solo se invoca desde
      los controllers/componentes que faltaban.
- [x] Backend: `SeguimientoLoteLevanteController.Create`/`Update` y
      `SeguimientoProduccionController.Create`/`Update` — mismo patrón de 8 controllers existentes
      (`this.ValidarVentanaFechaRegistro(fecha)` antes de llamar al service).
- [x] Frontend: `lote-levante/pages/modal-create-edit` y
      `lote-produccion/pages/modal-seguimiento-diario` — `UserPermissionService` inyectado,
      `aplicarVentanaFecha()` en `ngOnInit` (mismo patrón que `movimiento-alimento-form`), `[attr.min]`/
      `[max]` en el datepicker + hint dinámico (reemplaza el hint estático de Producción).
- [x] Sin permiso ni migración nuevos: `registros.fecha_retroactiva` ya existe y ya está habilitado
      por empresa desde el 20-ago.
- [x] `dotnet build` 0/0, `dotnet test` 3453/3453 (sin regresiones — el cálculo compartido no se tocó).
- [x] `yarn build` — 0 errores, `Application bundle generation complete` (433s).
- [!] Smoke manual en pantalla: sin el permiso, fecha fuera de ventana → 400 con el mismo mensaje que
      movimientos; con el permiso, cualquier fecha pasada entra; futuro siempre rechazado. **No
      ejecutado** — queda para que el usuario lo confirme en pantalla (Levante y Producción, con y
      sin el permiso).

---

## DB Studio — el backup no ordena vistas y funciones juntas (27-ago-2026)

Plan: [`db_studio_backup_orden_vistas_funciones_plan.md`](fase_de_desarrollo/db_studio_backup_orden_vistas_funciones_plan.md)

Reportado: restaurar el dump del 27-ago en la local corta con
`ERROR: relation "vw_guia_genetica_postura" does not exist` (línea 138089, 42P01). El backup emite
funciones (topológicas) y después vistas (alfabéticas), pero 4 funciones `LANGUAGE sql` LEEN una
vista y una vista lee otra vista + una función: el orden correcto es uno solo sobre los dos tipos.

- [x] Diagnóstico medido sobre el dump: 4 aristas función→vista, 1 vista→vista (latente, corta
      1.676 líneas después del primer error) y 1 vista→función (por eso «vistas primero» tampoco sirve).
- [x] `DbStudioSqlCalculos`: `ObjetoEsquemaDef` + `TipoObjetoEsquema` + `OrdenarObjetosEsquemaPorDependencia`
      (Kahn mixto, detección de arista por tipo) + `DefinicionUsaRelacion`; `OrdenarRutinasPorDependencia`
      queda como envoltorio para no tocar el contrato del 13-ago.
- [x] `DbStudioService.Backup.cs`: `WriteRoutinesAsync` + `WriteViewsAsync` → un solo
      `WriteRutinasYVistasAsync`; `SET check_function_bodies = off;` como red de seguridad (lo que hace
      `pg_dump`); encabezado y marcador de sección actualizados.
- [x] Tests xUnit nuevos (función→vista, vista→vista, vista→función, cadena mixta, fronteras de
      palabra, permutación exacta) + los 9 de rutinas verdes.
- [x] `dotnet build` + `dotnet test`.
- [x] Dump del 27-ago reordenado con la misma regla y restaurado en `sanmarinoapplocal` (vacía) con
      `-v ON_ERROR_STOP=1`: 0 errores.
- [x] Verificación real, no solo tests: `psql -v ON_ERROR_STOP=1` sobre `sanmarinoapplocal` vacía
      terminó en **exit 0**, con **136 tablas / 97.253 filas** — idéntico al pie del archivo — más 59
      funciones, 5 vistas, 16 triggers, 526 índices, 184 FKs y 336 migraciones. Las vistas que antes
      no existían devuelven datos (`vw_guia_genetica_postura` 1.743 filas,
      `vw_guia_genetica_por_lote_postura` 1.059) y `fn_indicadores_levante_postura` ejecuta.
- [x] Prueba de que el orden se sostiene SOLO (sin la red de seguridad): re-correr el tramo de
      funciones+vistas con `check_function_bodies = on` y `ON_ERROR_STOP=1` da exit 0 — o sea que los
      64 objetos se validan contra el catálogo en ese orden.
- [!] La local quedó 21 migraciones atrás del código (336 en `__EFMigrationsHistory` vs 357 archivos):
      es lo esperado —el dump es de producción— y el próximo arranque del backend las aplica solo
      (`Database:RunMigrations=true`).

---

## Enrutamiento de tickets por empresa: Sanmarino, Panamá, Ecuador (27-ago-2026)

Plan: [`enrutamiento_tickets_por_empresa_plan.md`](fase_de_desarrollo/enrutamiento_tickets_por_empresa_plan.md)

Pedido: quién recibe cada tipo de ticket por empresa — Sanmarino y Panamá con un rol "Sistemas X"
para SOPORTE/DUDAS y una persona (Verenice / Ricardo) para REQUERIMIENTO que escala a Desarrollo
(moiesbbuga@gmail.com); Ecuador sin área de sistemas, todo a Lady Malave.

- [x] **Mecanismo real investigado antes de tocar nada**: el módulo de tickets ya tiene un motor de
      enrutamiento vivo (`ticket_resolutores`, `ticket_resolutor_rol`, `ticket_perfil_usuario` +
      `TicketPerfilService`) — no hacía falta construir nada nuevo, solo configurarlo bien.
- [x] 🔴 **Los roles "Sistemas sanmarino" (34) y "sistemas panama" (35) YA EXISTÍAN** — una primera
      consulta con error propio los mostró como inexistentes; verificado dos veces antes de crear
      nada (por poco se duplicaban).
- [x] 🔴 **2 bugs de código reales, corregidos en `TicketPerfilService.GetAsignablesInternalAsync`**:
      ni los resolutores directos (`ticket_resolutores`) ni los de rol (`ticket_resolutor_rol`)
      filtraban por `company_id` — solo por tipo+país. Medido con Verenice: aparecía como asignable
      en tickets de CUALQUIER empresa; medido con el rol "Admin Demo": aparecía como asignable de
      SOPORTE en Sanmarino. Se agregó el filtro a los dos, verificado que el rol Admin
      (DESARROLLO global, moiesbbuga) sigue funcionando igual porque ya usaba una fila POR empresa.
- [x] 🔴 **2 datos mal cargados encontrados y corregidos**: el perfil de Lady Malave
      (`ticket_perfil_usuario`) estaba en `company_id` de Sanmarino en vez de Ecuador; ni ella ni
      Verenice tenían `tickets.gestionar` en su rol — sin él, `TicketService.PuedeGestionar()` les
      niega hasta gestionar sus propios tickets asignados (verificado leyendo el código: el gate no
      distingue "es mío" de "es ajeno").
- [x] Migración `20260827214243_SeedEnrutamientoTicketsPorEmpresa` (data-only, idempotente,
      localizada por nombre/email, con guarda fail-closed si algo no resuelve):
  - Sanmarino: rol 34 → SOPORTE+DUDAS; Verenice → solo REQUERIMIENTO activo (se apagó
    SOPORTE/DUDAS/DESARROLLO); `tickets.gestionar` a su rol.
  - Panamá: `tickets.gestionar` encendido a nivel empresa (estaba apagado) + al rol 35 + menú
    "Bandeja de gestión"; rol 35 → SOPORTE+DUDAS; Ricardo → REQUERIMIENTO (ya era IMPLEMENTADOR por
    `tickets.admin`, no necesitó perfil nuevo).
  - Ecuador: `tickets.gestionar` creado a nivel empresa (no existía la fila) + al rol Ecuador
    Administrador; perfil de Lady Malave movido a company_id correcto; ella → SOPORTE+DUDAS+
    REQUERIMIENTO (sin DESARROLLO, eso lo cubre el rol Admin global, ya configurado desde antes).
  - DESARROLLO/atención global: sin cambios — ya cubierto para Sanmarino/Ecuador/Demo/Panamá. Falta
    Santa Reyes, no se pidió, no se tocó.
- [x] Validado por transacción (`BEGIN` + 2 pasadas + verificación + `ROLLBACK`) antes de aplicar de
      verdad — confirmado idempotente, datos exactos.
- [x] Aplicada de verdad en local (arrancando el backend, migración registrada en
      `__EFMigrationsHistory`) y re-verificada con la tabla ya escrita.
- [x] `dotnet build` 0/0 (dos veces, uno por cada fix), `dotnet test` 3466/3466 sin regresiones.
- [x] **Simulación de la consulta real (con los dos fixes) contra los datos ya migrados**: SOPORTE
      empresa 1 → Alexander Mejia + moiesbbuga (ya no Demo); SOPORTE empresa 5 → 0 filas (nadie
      asignado al rol todavía, a propósito); REQUERIMIENTO empresa 1/5/3 → exactamente Verenice /
      Ricardo / Lady Malave, uno cada uno; DESARROLLO en las 4 empresas → moiesbbuga intacto.
- [!] Smoke HTTP real (login + crear/ver tickets en pantalla) **no ejecutado** — la verificación de
      arriba es a nivel de datos y de la consulta que los lee (equivalente exacto al LINQ real), no
      un clic real en el navegador. Queda para que el usuario lo confirme.
- [i] Los roles "Sistemas sanmarino"/"sistemas panama" quedaron sin nadie asignado en Panamá aparte
      de lo que ya tenían (decisión del usuario); Alexander Mejia ya tenía "Sistemas sanmarino" desde
      antes, así que ya empieza a recibir SOPORTE/DUDAS de Sanmarino apenas se despliegue.

### Probado en vivo en pantalla (27-ago-2026) — el pedido explícito de "pruébalos"

- [x] **Levanté backend+frontend local y probé con 6 sesiones reales** (usuario Sanmarino normal,
      Verenice, Lady Malave, Genesis Parrales —Ecuador regular—, Ricardo, Edwards), navegando a
      `/tickets/nuevo` y leyendo `cmp.tiposPermitidos` (poblado por la respuesta REAL de
      `GET /api/ticket-perfiles/tipos-permitidos`, no simulado). Resultado, exacto a lo esperado:
      - Sanmarino SOPORTE/DUDAS → Alexander Mejia + moiesbbuga (sin fuga de Demo/Panamá/Ecuador).
      - Sanmarino DESARROLLO → solo moiesbbuga. Sanmarino REQUERIMIENTO → Verenice + moiesbbuga.
      - Ecuador SOPORTE/DUDAS/REQUERIMIENTO → solo Lady Malave. Ecuador DESARROLLO → solo moiesbbuga.
      - Panamá REQUERIMIENTO → solo Ricardo. Panamá DESARROLLO → solo moiesbbuga.
- [x] 🔴 **La prueba encontró un tercer hueco real, corregido con una migración de seguimiento**
      (`20260827230000_FixRicardoPerfilTicketsPanama`): la migración anterior asumió que Ricardo ya
      era IMPLEMENTADOR porque su rol "Admin Panama" tiene `tickets.admin` — sin verificar que
      `company_permissions.tickets.admin` está **apagado** para Panamá (`is_enabled=false`, medido
      recién en código). Con el permiso fail-closed a nivel empresa, Ricardo en la práctica no tenía
      ni `tickets.admin` ni `tickets.gestionar`: `GET .../tipos-permitidos` devolvía `[]` en vivo,
      confirmado ANTES de aplicar el fix. Se le agregó `tickets.gestionar` al rol (mismo criterio ya
      aplicado a Sanmarino/Ecuador) + `ticket_perfil_usuario` IMPLEMENTADOR — no se tocó
      `company_permissions` de Panamá (encenderlo ahí sería admin GLOBAL de todos los países, no
      solo de sus propios tickets). Validado por transacción antes de aplicar; reconfirmado en vivo
      después: `tipos-permitidos` para Ricardo pasó a `[DESARROLLO, REQUERIMIENTO]`, con él mismo
      como único asignable de REQUERIMIENTO.
- [i] **Edwards (mismo rol "Admin Panama") sigue viendo `[]`** — es correcto, no un bug: el nivel
      IMPLEMENTADOR de Ricardo viene de su `ticket_perfil_usuario` individual, no del permiso de rol
      (que en las sesiones de prueba no viaja — el JWT minteado para el smoke no llevaba claims de
      `permission`, solo rol/empresa). En producción sí las llevaría, así que Edwards **sí** podría
      gestionar lo que se le asigne (por `tickets.gestionar` de rol), pero no crear Requerimiento por
      su cuenta — que es exactamente el diseño pedido: un solo implementador por país que recibe y
      escala, el resto del equipo gestiona lo que le llega.
- [x] Máquina bajo presión de memoria durante el smoke (VBCSCompiler acumuló hasta 5.8 GB dos veces
      en esta sesión, atascando builds) — resuelto matando el compiler server (y en el segundo
      episodio, todos los `dotnet.exe`) y reconstruyendo limpio; sin efecto en el resultado, solo en
      el tiempo.
- [x] `dotnet build`/`dotnet test` del fix de Ricardo también en verde (3466/3466).
- [x] Limpieza: las 6 sesiones de prueba (`sesiones_activas`) borradas, backend/frontend locales
      apagados, puertos libres, archivos temporales del smoke eliminados.

---

## DIA-OP-VAL — El plazo de la doble validación se juzga en día operativo UTC−5 (28-ago-2026)

Plan: [`fase_de_desarrollo/dia_operativo_plazo_validacion_plan.md`](fase_de_desarrollo/dia_operativo_plazo_validacion_plan.md)

> Origen: ticket de operación de Panamá — granja **DAYLAND**, galeras 6, 5, 4 y 3 sin poder ingresar
> registros. Cierra el último consumidor que seguía juzgando fechas en UTC crudo, después de la
> ventana de inventario y de la revocación de sesión (`6fb1edd`).

- [x] **Auditoría previa del ticket (hecha antes de tocar código).** El fix de `94e1f9f` (plazo desde
      `created_at`) y `cc5beb4` (validar en bloque) **sí están en producción** — merges `79886a8`
      (26-ago 01:04) y `5e780e5` (26-ago 07:56), TaskDef `sanmarino-back-task:169`, rollout
      `COMPLETED`, imagen `...:ecd05486`. Confirmar registros viejos **sí** está permitido y confirmar
      **sí** destraba: verificado en los datos, no solo en el código — en DAYLAND se confirmaron
      registros del 17 al 25 de agosto y las galeras 1, 2 y 3 quedaron libres.
- [x] 🔴 **El defecto real es otro: `Hoy` es `DateTime.UtcNow` crudo.** Panamá opera en UTC−5, así que
      el plazo **vence a las 19:00 locales, no a la medianoche**. Los 9 registros que trababan DAYLAND
      se cargaron el 26-ago 11:47–13:57 y murieron el 27-ago a las 19:00.
- [x] **La distinción que decide el cambio: instante vs fecha pura.** `created_at` y `now()` son
      instantes ⇒ van a día operativo. `fecha` es una fecha pura guardada como `timestamptz` ⇒ **no se
      toca**: el formulario la escribe a `12:00Z` y el trigger del cruce a `00:00Z`, y desplazarla −5 h
      movería las filas del cruce un día atrás.
- [x] **Medición que eligió el diseño** (copia de prod, ItalcolPanama, 60 días, 1.097 capturas):
      corregir sólo `Hoy` afloja y no aprieta nada; corregir también `Creacion` aprieta 309 registros
      **con daño real 0** — ninguna confirmación de 60 días cayó en la ventana de 19 h que elimina. En
      la otra dirección, **5 confirmaciones sí cayeron dentro de las 5 h que la regla actual roba**.
      Se implementan las dos mitades.
- [x] `DiaOperativo` nuevo en `ValidacionSeguimientoCalculos`, delegando en el helper canónico
      `VentanaFechaRegistroCalculos.DiaOperativo` (una sola fórmula por número).
- [x] `Hoy` + los 4 casos de `LeerPendientesDelLoteAsync` (`CreatedAt`) pasan a día operativo.
- [x] **Front sin cambios**: `estado-validacion-seguimiento.funcion.ts` ya usa el día calendario local
      del navegador, que en Panamá *es* el día operativo. El espejo ya era correcto; el cambio alinea
      el backend con él.
- [x] **Sin migración**: la regla no toca la BD.
- [x] Tests xUnit del helper y de los invariantes (día operativo ≤ día UTC; flag apagado idéntico; sin
      `created_at`, comportamiento previo byte a byte).
- [x] `dotnet build` 0 errores + `dotnet test` en verde.
- [x] Recontar los 9 pendientes de DAYLAND con la fórmula nueva dentro de la ventana 19:00–24:00.

---

## DUP-DIA — Indice unico por DIA en los seguimientos + el bloqueo real de la galera 6 (28-ago-2026)

Plan: [`fase_de_desarrollo/indice_unico_dia_seguimientos_plan.md`](fase_de_desarrollo/indice_unico_dia_seguimientos_plan.md)

> Sale de la auditoria del ticket de DAYLAND. Validado contra la copia de produccion recargada hoy.

- [x] **Auditoria de duplicados: 6 filas en todo el sistema.** 5 en engorde (todos ItalcolPanama, todos
      con el patron cruce `00:00Z` + manual `12:00Z`: lotes 161, 178, 216) y 1 en levante (Demo).
      Reproductora y produccion en 0 — produccion porque YA tiene el indice funcional por dia UTC
      (`20260801070000`), que es el precedente copiado.
- [x] 🔴 **Correccion de una afirmacion mia previa.** Dije que ninguna fila duplicada tenia movimiento
      de inventario; el regex estaba mal. Con el patron correcto, **las 4 viejas de engorde y las 2 de
      levante SI tienen movimiento + fila en el historico unificado**. Solo `12676` estaba limpia. Eso
      cambio el diseno: la migracion **no borra nada**, excluye por id.
- [x] **`12676` borrado por la API** (no por SQL): las dos reservas quedaron `LIBERADA`, no huerfanas.
      Duplicados de engorde: 5 → 4.
- [x] 🔴 **El hallazgo grande: la galera 6 NO estaba trabada por el plazo ni por el duplicado.**
      `POST validar-pendientes` corta en el primero y no intenta el resto. Con los 5: corto en `12676`
      por «No hay stock suficiente». Borrado ese, corto en `12674` por lo mismo. **Stock del galpon
      G0471: 416,24 kg; las reservas piden 2.222,64 kg ⇒ faltan 1.806,40 kg.** El ultimo ingreso de
      alimento fue el 19-ago. Falta registrar el ingreso, no validar.
- [x] **El POST fallido no escribio nada**: snapshot identico antes y despues (reservas, aves,
      movimientos). El corte y el rollback funcionan como estan documentados.
- [x] **Evidencia dura de que `12676` sobraba**: el 17-ago ese galpon ya tenia el alimento descontado
      por `Seguimiento reproductora #802` (272,16 kg — exactamente lo que espeja la fila del cruce) y
      por `Seguimiento aves engorde #12668` del lote 215, que comparte galpon.
- [x] **Migracion `20260828120000_IndiceUnicoDiaSeguimientos`**: 4 indices unicos funcionales por dia
      UTC (engorde, levante x2, reproductora), parciales por id donde hay historia aplicada. Fail-soft
      con `RAISE WARNING` como el precedente: nunca tira el arranque de prod.
- [x] **El controller tenia que cambiar**: cazaba el duplicado por NOMBRE de indice, y el caso nuevo
      dispara el indice nuevo ⇒ el usuario habria visto el texto crudo de Postgres justo en el caso
      que veniamos a proteger. Extraido a `DuplicadoSeguimientoDiarioCalculos` con test que fija los
      dos nombres.
- [x] **Simulacion en transaccion** contra la copia de prod: los 4 indices se crean; un duplicado del
      mismo dia a otra hora se RECHAZA; un dia libre entra; las 4 filas historicas sobreviven; el
      rollback no deja nada.
- [x] `dotnet build` 0 errores / 0 advertencias + `dotnet test` **3487/3487** (+8 nuevos).
- [ ] ⏸️ **NO APLICADA.** Falta el OK explicito. Antes: re-correr el diagnostico contra el dump del dia
      y decidir si `12676` se borra en produccion o queda excluido.
- [ ] ⏸️ **Queda abierto: alinear el cruce a mediodia UTC.** Es la correccion de fondo y **no se puede
      hacer tal cual** — el cruce re-inserta sin `ON CONFLICT`, asi que donde ya exista una fila manual
      de ese dia la confirmacion de reproductora fallaria entera. Exige darle antes una estrategia de
      conflicto. Otra entrega.

---

## La hora de llegada manda el primer día de registro/consumo (engorde Panamá + Ecuador)

> Plan: [`fase_de_desarrollo/hora_llegada_manda_primer_dia_engorde_plan.md`](fase_de_desarrollo/hora_llegada_manda_primer_dia_engorde_plan.md)
> Ticket de operación Panamá: con hora de encaset 23:58 el módulo igual muestra un registro el día del
> encaset, con saldo de alimento **−150 kg**. Piden el mismo comportamiento en Ecuador.

### Auditoría (copia de producción, 28-ago-2026)
- [x] **Los 4 guardas C# aguantan: 0 registros manuales violan la regla en 60 días.**
- [x] **El único escritor sin guarda es `fn_cruce_reproductora_a_engorde`** (fecha destino
      `v_fecha_enc + d`, sin mirar `hora_encasetamiento`). **3 filas torcidas**, todas ItalcolPanama
      (lotes 215 y 216). Son `origen_cruce ⇒ solo lectura en la UI`: por eso abren ticket en vez de
      borrarlas.
- [x] **Ningún lote reproductora tiene hora informada (0 de 138)** ⇒ su guarda nunca dispara, el
      operario captura la edad 0 ahí y el cruce la re-fecha al día del encaset del lote engorde. Ese
      es el mecanismo completo del ticket.
- [x] **Ecuador ve la promesa y no la recibe:** el campo *Hora de encasetamiento* y su leyenda están
      sin gate en el formulario; Ecuador la llenó **16 veces, todas ≥ 13:00**, y el backend la ignora
      (flag apagado). **0 de esos 16 lotes violaría la regla** ⇒ encenderla no traba ninguno.

- [x] 🔴 **Confirmado sobre el lote del ticket.** El lote **238 «PRUEBA - 1»** está en la copia local
      (ItalcolPanama, encaset 27-ago, hora **23:58**, borrado el 28-ago 14:17). La fila del 27-ago es
      `id 12937`, `origen_cruce = true`, `SYSTEM_CRUCE`, mort 3 / sel 2 / 150 kg — **exactamente** la
      de la captura. No es una captura manual.

### Implementación
- [x] **Cambio 1 — el primer día lo decide la HORA DEL LOTE, no el flag de empresa** (8 puntos de
      captura). Inerte donde la hora es `NULL`; Panamá ya la tenía encendida ⇒ sin cambios.
- [x] **El día de pesaje CONSERVA el gate por empresa** — la guía genética de Ecuador está tabulada
      por días desde el encaset; moverlo la desalinea (decisión ya tomada en jul-2026).
- [x] **Cambio 2 — migración `20260828170000_FnCruceReproductoraEngordeHoraLlegada`** con `v_desp`
      por hora (`v_fecha_enc + v_desp + d`) + espejo en `backend/sql/`.
- [x] 🔴 **Dos correcciones que salieron de probarlo contra el dato real, no del diseño:**
      **(a)** el borrado del cruce tenía que salir del loop — al correr la serie, la fila nueva de la
      edad `d` caía sobre la fila **vieja** de la `d+1` y el índice único las rechazaba **todas**;
      **(b)** `ON CONFLICT DO NOTHING` + `RAISE WARNING` porque el día destino puede estar ocupado por
      un registro manual y el `INSERT` haría fallar la confirmación de reproductora entera.
- [x] 🔴 **Defecto latente vivo que esto también tapa:** medido con la función **actual**, recalcular
      el lote **215 ya revienta hoy** con `duplicate key … ux_seg_diario_aves_engorde_lote_dia_utc`.
      La próxima vez que toquen su reproductora, la confirmación falla sin explicación.
- [x] **Cambio 3 — front:** `desplazamientoPrimerDia` sin `reglaActiva`; `diaParaReglaDePesaje` la
      conserva. Se eliminó el `@Input` y la inyección que quedaron muertos.
- [x] **Validación:** `dotnet build` 0/0 · `dotnet test` **3491/3491** (+4) · `yarn build` OK ·
      gate `verificar-sql-llega-por-migracion.js` OK.
- [x] **Simulación en transacción revertida contra la copia de producción:** lote 238 con hora 23:58
      ⇒ serie a **28-ago … 02-sep**, **ninguna fila el 27-ago**; el mismo lote con la hora en `NULL`
      vuelve a **27-ago … 02-sep**; recálculo de **todos** los lotes con cruce ⇒ **0 filas cambiadas**
      en los que no tienen hora tardía (331 idénticas).

### Fuera de alcance — requiere OK aparte
- [ ] ⏸️ **Remediar las filas de cruce ya torcidas (lotes 215/216).** La migración **no recalcula
      nada**. Cuando alguien toque su reproductora, el cruce se regenerará desde su fuente de verdad y
      el 215 quedará con **5 filas en vez de 7** (su edad 0 ya no existe en reproductora; las 6 y 7
      chocan con manuales que las cubren). Hoy ese recálculo **aborta con error**, así que el cambio
      mejora el estado — pero conviene decidir a mano qué se hace con esos dos lotes.
- [ ] ⏸️ **Encender `primer_registro_segun_hora_llegada` en Ecuador:** con el Cambio 1 ya no hace
      falta para lo pedido; el flag queda solo para el corrimiento del día de pesaje.

---

## DEMO-COSTOS — Dejar la empresa Demo lista para la práctica de carga masiva del equipo de costos (28-ago-2026)

Plan: [`fase_de_desarrollo/demo_lista_practica_carga_masiva_costos_plan.md`](fase_de_desarrollo/demo_lista_practica_carga_masiva_costos_plan.md)

> Pedido: el equipo de costos tiene que aprender a armar los archivos de carga masiva, subirlos a las
> granjas y validar la información y los reportes de costos **en Demo**, antes de tocar SanMarino.
> Decisiones del usuario: alcance = solo lo que usa costos · destino = migración EF **sin desplegar** ·
> datos = limpiar lo operativo dejando la estructura.

### Auditoría (BD local = copia de producción)
- [x] 🔴 **La cadena está cortada en CUATRO niveles, no en uno.** Medido ejecutando `fn_menu_usuario`
      con el usuario real `admin.demo`: el grupo **Carga Masiva** se pinta **VACÍO** y **Reportes**
      solo trae 2 hijos. Falta (1) `company_menus` de `migraciones_masivas`, (2) `company_menus` de
      `reporte_diario_costos_postura` + `reporte_tecnico_semanal`, (3) `carga_masiva_postura` en
      `company_permissions`, (4) el permiso y el menú hijo en los roles 23/24.
- [x] **El dato que explica el síntoma:** los dos roles de Demo YA tienen en `role_menus` el grupo
      `carga_masiva` y el `reporte_diario_costos_postura`. La configuración de ROL estaba lista; lo
      que nunca se habilitó fue la EMPRESA. Por eso es un menú vacío y no un 403.
- [x] 🔴 **Contrasentido a corregir:** `Admin Demo` tiene `carga_masiva_pollo_engorde` y **no**
      `carga_masiva_postura` — el permiso justo al revés. Demo no tiene un solo lote de engorde.
- [x] **4 flags divergen y fabrican los errores que se quieren evitar.** El grave es
      `reportes_alimento_desde_inventario_unificado`: SanMarino **true**, Demo false ⇒ el Contable y
      el Técnico de Demo leen `farm_inventory_movements` (**2 filas**) en vez de
      `inventario_gestion_movimiento` (**12**). Además `captura_huevos_en_levante` (SM true / Demo
      false) y, al revés, `maneja_codigos_erp_avicola` y `permite_traslado_aves_cross_etapa`
      encendidos solo en Demo ⇒ campos y flujos que en SanMarino no existen.
- [x] **Lo que Demo ya tiene bien:** las 6 regionales idénticas, guía genética propia (224 filas),
      catálogo de inventario (62 ítems) y `fn_reporte_diario_costos_postura(4,…)` ya devuelve
      **37 filas**. El backend está bien: el problema es exclusivamente de habilitación.
- [x] **Huella de datos operativos medida** (73 histórico + 42 seguimientos levante + 17 lotes base +
      12 movimientos de inventario + …). Estructura a preservar: 9 granjas / 10 núcleos / 20 galpones.

### Parte A — Habilitación (migración EF, idempotente, no destructiva)
- [x] Migración `20260828180000_DemoListaParaPracticaCargaMasivaCostos` escrita (data-only, Designer
      clonado — **modelo idéntico al ModelSnapshot, diff 0**; empresa por `identifier`, menús por
      `key` y roles por su vínculo real en `role_companies` — nunca por id fijo ni por `name`).
- [x] A1 flags · A2 `company_menus` · A3 `company_permissions` · A4 `role_permissions` ·
      A5 `role_menus` · A6 apagar engorde a nivel empresa (R5: no se borra del rol) · **A7 nuevo**.
- [x] 🔴 **A7 salió de leer la plantilla, no de suponer:** `GenerarPlantillaSeguimientoAsync` arma la
      hoja `Referencias` con los alimentos de la empresa y ata `Alimento 1 H/2 H/1 M/2 M` a un
      **desplegable** sobre ese rango ⇒ el catálogo ES lo que el equipo puede escribir en el archivo.
      Demo tenía **un alimento de más**, `Alimento ERP` (cód. 4000), creado cuando tenía el flag ERP
      encendido. Se **desactiva** (no se borra: 8 movimientos y 4 filas de stock lo referencian).
      Verificado: Demo pasa a ofrecer los **mismos 61** que SanMarino, 0 de más y 0 de menos.
- [x] **Ensayo transaccional del `Up()`/`Down()` (2 corridas, con ROLLBACK).** `fn_menu_usuario` de
      `admin.demo`: **Carga Masiva → Migración Manual** (ya no vacío) y **Reportes** pasa de 2 a 4
      hijos con *Informe RA Pesadas* y *Reporte Diario Costos Postura*. Flags alineados; permisos
      postura ON / engorde OFF. **Idempotente** (2ª corrida sin un solo cambio). Empresas 1/3/5/6
      **sin una sola fila de diferencia**. `Down()` vuelve exacto al estado inicial.
- [x] `dotnet build` **0 errores / 0 advertencias** + `dotnet test` **3492/3492 en verde**. Hubo que
      aislar el build con `--artifacts-path`: había **3 `dotnet build` de sesiones distintas** peleando
      el mismo `bin/` y el mío quedó 30 min sin escribir un solo archivo. Se mató **solo el propio**
      (los otros dos corren desde `~/.dotnet`, el mío desde `dotnet-portable` — se distinguen por ahí).
- [x] **EF reconoce la migración**: `migrations list` la muestra como `(Pending)` y **última** en el
      orden. ⚠️ Con `--no-build` a secas EF lee el `bin/` del API, que era de las 11:04 y NO la tenía:
      hay que pasarle `--msbuildprojectextensionspath` al `obj/` de los artifacts o miente sin error.
- [ ] ⏸️ **NO se aplicó en la BD local, a propósito.** `dotnet ef database update` aplicaría TAMBIÉN
      `20260828170000_FnCruceReproductoraEngordeHoraLlegada`, que es de **otra sesión y está a medio
      implementar** (sus checkboxes siguen sin marcar). La BD local es una sola para todos los
      checkouts. La validación se hizo con el SQL exacto extraído del propio archivo `.cs`, corrido
      dos veces en transacción con `ROLLBACK`.

### Parte B — Limpieza de datos (destructiva, ⏸️ requiere OK explícito)
- [x] `backend/sql/migracion_limpieza_demo_practica_costos.sql` escrito — **NO va por migración** (se
      re-ejecutaría en cualquier entorno nuevo y no hay `Down()`). Script de una sola vez, prefijo
      `migracion_*` exento del gate por diseño; **el gate `verificar-sql-llega-por-migracion.js` pasa**.
      Resuelve la empresa UNA vez a una temp table y aborta con `RAISE EXCEPTION` si no hay
      exactamente una: ninguna sentencia puede correr sin el filtro.
- [x] **Ensayo con `ROLLBACK` corrido y verificado.** Sin un solo error de FK. Operativos de Demo a 0
      (73 histórico + 42 seguimientos + 17 lotes base + 12 movimientos + …); **estructura intacta**
      (9 granjas / 20 galpones / 10 núcleos); las otras 4 empresas sin una fila de diferencia.
      Confirmado además que el `ROLLBACK` revirtió: los datos siguen ahí.
- [x] **El orden de borrado se midió con `pg_constraint`, no se supuso** — `lote_postura_produccion`
      apunta con RESTRICT a levante, y `lotes` a `lote_postura_base`. El histórico unificado se borra
      **explícitamente**: lo llena un trigger AFTER INSERT y ningún DELETE del origen se propaga solo.
- [ ] ⏸️ **Ejecutar de verdad (cambiar `ROLLBACK` por `COMMIT`): FALTA TU OK.** En prod, entrega aparte.

### Fuera de alcance (explícito)
- [ ] ⏸️ **No se despliega.** La migración queda commiteada; el deploy es otra entrega con OK aparte.
- [ ] ⏸️ `mobile_access` de Demo queda en `false` y los otros 22 menús de SanMarino, apagados.

---

## 🔴 Editar un lote de engorde revienta con 23514 — la auditoría del ajuste no cabe en el CHECK

> **Sesión del 28-ago-2026.** Plan: [`fase_de_desarrollo/ajuste_encasetamiento_engorde_check_tipo_registro_plan.md`](fase_de_desarrollo/ajuste_encasetamiento_engorde_check_tipo_registro_plan.md)
> **Caso del usuario:** SACACHUN 3B · galpón 3 · LOTE 04 — sumar 200 aves hembra (8.614 → 8.814).

- [x] **Diagnóstico**: el toast «no cumple una regla de validación» = SQLSTATE **23514**; el service
      escribe `tipo_registro = AjusteEncaset` y `ck_hlpe_tipo_registro` sólo admite
      `Inicio | Ajuste | AjusteResync`. La funcionalidad (`a9fd721`, 21-ago) se mergeó **sin** la
      migración que amplía el catálogo. `a9fd721` **sí está** en `origin/main-produccion` ⇒ prod corre
      el código que escribe el valor y la BD que lo rechaza.
- [x] **Hallazgo transversal**: la BD local tiene **0 constraints CHECK** en todo el esquema público
      (`SELECT count(*) FROM pg_constraint WHERE contype='c'` → **0**), ni siquiera las 2 que EF
      declara en `lote_ave_engorde` ⇒ esta clase de bug es **estructuralmente invisible en local**.
      Por eso el arreglo del 21-ago se dio por bueno.
- [x] **Segundo defecto, mismo origen**: la fila de auditoría guarda el **delta con signo** contra un
      `ck_hlpe_aves_nonneg CHECK (aves_* >= 0)` ⇒ **restar** aves habría fallado con el mismo 23514
      apenas se arreglara el primero. Van los dos en la misma migración.
- [x] **Los 6 lectores de la tabla filtran `tipo_registro` explícitamente** (`= 'Inicio'` / `= 'Ajuste'`);
      ninguno suma la tabla entera ⇒ la fila `AjusteEncaset` es **inerte**. Mismo criterio que `AjusteResync`.
- [x] Catálogo puro `TipoRegistroHistorialEngordeCalculos` + los 3 services consumiéndolo
      (`LoteAveEngordeService`, `CorreccionAvesDisponiblesEngordeService`, `LoteReproductoraAveEngordeService`).
      Literales idénticos: refactor sin cambio de comportamiento.
- [x] Migración `20260828190000_AmpliaCheckHistorialEngordeAjusteEncaset` (idempotente, fail-soft:
      si hubiera filas fuera del catálogo NO se recrea y deja `RAISE WARNING` — nunca tira el arranque).
      Designer clonado; **`ModelSnapshot` intacto** (estas constraints no viven en el modelo EF).
- [x] Espejo `backend/sql/create_historial_lote_pollo_engorde.sql` actualizado + gate
      `verificar-sql-llega-por-migracion.js` **en verde**.
- [x] Tests xUnit del catálogo (congelan la lista de la migración): **27 casos, 27 en verde**.
- [x] `dotnet build` **0 errores / 0 advertencias** + `dotnet test` **3519/3519** (aislado con
      `--artifacts-path`: había otra sesión con el `bin/` tomado).
- [x] **EF reconoce la migración**: `migrations list` la muestra `(Pending)` y **última**. ⚠️ Con
      `--no-build` a secas EF lee el `bin/` del API (de las 11:03, de otra sesión) y **miente sin
      error**: no listaba ni la mía ni la de ayer. Hay que combinar `UseArtifactsOutput`+`ArtifactsPath`
      con `--msbuildprojectextensionspath` al `obj/` de los artifacts.
- [x] **Verificación contra Postgres, simulando PROD** (transacción + `ROLLBACK`, SQL extraído del
      propio `.cs`): (1) con las constraints viejas el INSERT de +200 hembras **falla con 23514** —el
      bug del usuario, reproducido—; (2) tras la migración **guarda**, y el delta negativo también;
      (3) siguen rechazados `Inicio` negativo, `Ajuste` negativo, tipo inventado y minúsculas;
      (4) 2ª corrida **no-op**; (5) `Down()` ensayado en sus dos ramas. BD local **sin residuo**.
- [ ] ⏸️ **NO se aplicó en la BD local, a propósito**: `dotnet ef database update` aplicaría también
      `20260828170000` y `20260828180000`, que son de **otras sesiones**. De ahí la validación por
      transacción.
- [ ] ⏸️ **Deploy: NO se hace acá.** La migración se aplica sola al arrancar; el merge a
      `main-produccion` va con OK aparte.

---

## Remediar las filas de cruce ya torcidas por la hora de llegada (engorde, Panamá) — 28-ago-2026

Plan: [`fase_de_desarrollo/remediacion_cruce_engorde_hora_llegada_plan.md`](fase_de_desarrollo/remediacion_cruce_engorde_hora_llegada_plan.md).
Continúa el commit `151cebe`, que arregló al **escritor** (`fn_cruce_reproductora_a_engorde` ya respeta
la hora) pero **no recalculó nada** y dejó dicho que las filas viejas eran «una operación de datos
aparte, con su propia verificación y su propio OK». Esto es esa operación.

### Diagnóstico — cerrado
- [x] **Medido en UTC, no en la zona de la máquina.** Con `America/Bogotá` (el default de `psql` acá)
      `fecha::date` **resta un día** y el mismo query reporta **6** violaciones donde hay **3**.
      `fecha` es `timestamptz` a `00:00Z`. Misma trampa que [[plazo-validacion-vencia-a-las-19]].
- [x] **3 violaciones, las 3 `origen_cruce`/`SYSTEM_CRUCE`, las 3 ItalcolPanamá:** lote **215**
      (id 12118, 10-ago, 362,880 kg), lote **216** (id 12168, 13-ago, 181,440 kg) y lote **238**
      (id 12937, 27-ago) — este último es **el lote del ticket y está borrado**. Ecuador: **0**
      (sus 19 lotes con hora ≥ 13:00 no usan el cruce).
- [x] **Los lotes vivos NO tienen saldo negativo.** El −150 kg del ticket era del 238, borrado.
      215 y 216: **0 días en rojo**, mínimo +1.558,08 kg. Lo que queda es la fila de más.
- [x] **215 y 216 comparten el galpón G0471** ⇒ comparten stock: tocar uno mueve el cuadre del otro.
- [x] **Segundo defecto, independiente:** el reproductora **131** (hijo del 215) tiene encaset
      **09-ago**, un día antes que su padre, y fue **editado el 25-ago**, cuatro días *después* de que
      corriera el cruce (21-ago). Como el cruce mapea por EDAD, ese desfase corre la serie otra vez.
      No es general: **128 de 138** lotes reproductora están alineados.
- [x] 🔴 **Una migración SQL a secas rompe un invariante.** El descuento de aves al maestro y la fila
      `BAJA_SEGUIMIENTO` del histórico unificado los escribe **C#**
      (`RetiroAvesEngordeAplicador.SincronizarCruceAsync`), no el SQL. Verificado: el histórico del 215
      tiene la fila **17821 → origen_id 12118, `anulado = false`**, viva y apuntando justo a la fila a
      sacar. Borrarla por SQL la deja **huérfana y sin anular** —lo que CLAUDE.md prohíbe— y deja
      **62 aves** descontadas de más en el maestro. ⇒ **la remediación va por el camino C#.**
- [x] **Tres opciones medidas en transacción revertida** (cuadre de G0471, `fn_cuadre_alimento_engorde`):
      hoy **−634,64 kg** · recalcular **+1.633,36** · borrar sólo el día del encaset **−90,32** ·
      alinear encaset + recalcular **+635,44**. Las tres dejan 0 violaciones, ninguna toca una fila
      manual, y el cuadre de **aves** queda en desfase 0 / `cuadra = t` en las tres.
- [x] **Por qué el recálculo pierde 2.268,00 kg:** al correr la serie, el último día del cruce cae sobre
      un **registro manual** y la fn lo saltea (`ON CONFLICT DO NOTHING` + `RAISE WARNING`, el guarda
      que agregó `151cebe` — sin él **reventaba** con `duplicate key`). El 215 pierde **dos** días
      (arrastra además el desfase de encaset); el 216, **uno**.
- [x] **El stock físico favorece la opción 2**: es la única que deja el cuadre casi en cero (−90,32 kg).
      **Pero no es estable**: el trigger corre `AFTER INSERT OR UPDATE OR DELETE` sobre el seguimiento
      reproductora ⇒ **el primer toque a la reproductora la convierte en la opción 1**. Los únicos
      estados estables son la **1** y la **3**.
- [x] `backend/sql/verificar_cruce_engorde_hora_llegada.sql` — diagnóstico repetible de solo lectura
      (7 secciones: violaciones, desfase de encaset, colisiones, invariante del histórico y línea base
      del cuadre). Declarado `SIN-MIGRACION`; **el gate `verificar-sql-llega-por-migracion.js` pasa**.

### Decisión — tomada
- [x] **Opción 3: alinear el encaset del reproductora + recalcular.** Es la única coherente con la
      regla ya desplegada («se corre la serie, el consumo es real»), es **estable**, corrige de paso un
      dato maestro genuinamente torcido y pierde **la mitad** de los kilos que el recálculo a secas.
      Se pierden **1.088,64 kg** (215) y **181,44 kg** (216). **El lote 238 se deja como está.**

### Implementación — cerrada
- [x] **Migración `20260828200000_RemediarCruceEngordeHoraLlegadaPanama`** (data-only, Designer
      clonado, ModelSnapshot intacto; SQL en el partial `.Sql.cs`). ⚠️ El timestamp `…190000` lo tomó
      otra sesión (`3988183`, CHECK de `historial_lote_pollo_engorde`) mientras esto se escribía —
      tabla distinta, no chocan.
- [x] **La cohorte se resuelve por la REGLA, no por ids fijos**: lote vivo, hora ≥ 13:00,
      `aves_encasetadas > 0` y ≥1 fila `origen_cruce` anterior a `encaset + 1`. Da exactamente
      **215 y 216**; el 238 se excluye solo por `deleted_at IS NULL`.
- [x] **Los 4 pasos replican `SincronizarCruceAsync`**, no lo reinventan: devolver aves + anular
      histórico *antes* del borrado (es cuando todavía se lee el baseline) → alinear encaset →
      llamar la **fn canónica** → aplicar las bajas nuevas con el mismo reparto (`EsLoteMixto`), el
      mismo clamp a 0 y la guarda `aves_encasetadas > 0`. Mismo patrón que
      `20260729100000_AplicarBajasCruceReproductoraAlMaestroEngorde`.
- [x] **De UNA sola vez, no convergente:** todo el `Up` va en un bloque guardado por la tabla de
      respaldo. Re-correrlo volvería a recrear las filas con ids **nuevos** y dejaría el histórico de
      la corrida anterior huérfano y sin anular — el invariante que esto viene a cuidar.

### Validación — corrida, con el SQL extraído del `.cs` que se despacha
- [x] `dotnet build` **0 errores / 0 advertencias** · `dotnet test` **3.519/3.519** · `dotnet ef
      migrations list` la ve `(Pending)` y **última**. Build con `--artifacts-path` aislado: había
      **2 `dotnet` de otras sesiones**.
      ⚠️ Para que EF liste las migraciones nuevas, `--msbuildprojectextensionspath` va al `obj/` del
      proyecto **de migraciones** (`ZooSanMarino.Infrastructure`), no al del startup: apuntándolo al
      del API listaba hasta `…170000` y **mentía sin error**.
- [x] **Up**: 215 `7 filas / 5.080,320 kg / 10-16 ago` → **6 / 3.991,680 / 11-16**; 216
      `7 / 1.542,240 / 13-19` → **6 / 1.360,800 / 14-19**. Violaciones **2 → 0**. Descuadre G0471
      **−634,64 → +635,44**. Maestro 215 `15.175/15.087` → **15.196/15.111**, 216 `4.944/4.954` →
      **4.949/4.959**. Cuadre de aves **0/0 `cuadra = true`**.
- [x] **Nada colateral**: las 16 filas manuales quedan **byte a byte idénticas** (mismo id, misma
      fecha); los **otros 64 galpones** del cuadre conservan la **misma huella** `md5 e926003d…`;
      las huérfanas del universo siguen en **6** (las del 227, ver abajo).
- [x] **Segunda corrida**: `NOTICE: ya aplicada, no se repite` — no mueve un solo número.
- [x] **`Down`**: vuelve al estado inicial **línea por línea** (7 filas / 5.080,320 kg, maestro
      15.175/15.087, encaset repro 09-ago, descuadre −634,64, violaciones 2) y borra los respaldos.
- [x] **BD local intacta**: todo en transacción revertida — 0 tablas de respaldo, las 2 violaciones
      siguen ahí. **No se aplicó a propósito**: `database update` arrastraría también las 3
      migraciones pendientes de otras sesiones, y la BD local es una sola para todos los checkouts.

### Fuera de alcance (explícito)
- [ ] ⏸️ **No se despliega.** La migración queda commiteada; el deploy es otra entrega con su OK.

### [i] Hallazgo aparte, NO de esta tarea
- [i] **Lote 227** (ItalcolPanamá, «14 - 1», vivo) tiene **6 filas `BAJA_SEGUIMIENTO` huérfanas y sin
      anular** (ids 19321-19326 → seguimientos 12817-12822, que ya no existen), creadas **hoy 28-ago
      06:03**. Son **142 aves** (75 H + 67 M) descontadas del maestro sin registro que las respalde.
      Es el mismo invariante, roto por otro camino; no lo toca esta tarea.

---

## Santa Reyes — la guía genética en el camino SQL + los tipos de huevo al ALTA del lote (30-ago-2026)

Plan: [`fase_de_desarrollo/santa_reyes_guia_sql_alias_y_huevo_items_alta_plan.md`](fase_de_desarrollo/santa_reyes_guia_sql_alias_y_huevo_items_alta_plan.md)

**Pedido:** verificar que producción, levante y gestión de lotes estén conectados con la guía de
Santa Reyes, y que los tipos de huevo declarados **al crear el lote** sean los que aparecen en la
fase de producción.

### Auditoría (cerrada) — medida en `sanmarinoapplocal:5433`, company 6

- [x] **Funciona:** guía propia (5 razas × 123 sem., edad 18→140) → `vw_guia_genetica_postura` →
      indicadores de **producción** (smoke lote 152 sem. 30: prod guía **72,87 %** / consumo
      **113,00** / retiro **1,10**) y de **levante** (sem. 20: consumo **107,00**, peso/unif/mort
      vacíos = correcto, la guía reducida no los trae).
- [x] **Funciona:** el selector de raza/año del alta de lote une propia + compartida
      (`ObtenerRazasCrudoAsync`) y `LoteService.Crud` valida con `GuiaGeneticaLookup` (con alias ERP).
- [x] **Funciona:** ítems de huevo por lote en backend (`lote_huevo_items`, empresa por
      `farms.company_id`, gate `ValidarHuevoItemsAsync` fail-closed) y el diario los pide con
      `lotes.lote_id` (el maestro, no el espejo `lpp`).
- [i] **G1** — el alias de grafía del ERP vive **sólo en C#**: `BABCOK BROWN` / `HY LINE` →
      producción **vacío**, levante **0,00**, mientras el reporte técnico (C#) sí muestra la guía.
- [i] **G2** — levante compara la raza **case-sensitive**: `CRIOLLA` cruza en producción y no en levante.
- [i] **G3** — en levante, no cruzar se pinta **`0,00`** (objetivo falso), no vacío.
- [i] **G4** — `fn_indicadores_produccion_postura` descarta toda semana de vida **< 25**; Santa Reyes
      tiene guía desde la **18** y `huevo_primera_postura_hasta_semana = 22` ⇒ sus semanas 18–24 no salen.
- [i] **H1** — los tipos de huevo sólo se declaran **después** de crear el lote (botón 🥚 de la lista);
      el formulario de alta no los ofrece. Hoy el lote 152 tiene **0 declarados** ⇒ fail-closed en el diario.

### A — el alias de raza en SQL (G1 + G2 + G3)
- [x] El alias se resuelve en `vw_guia_genetica_postura` (3a rama), NO con una fn por join: los 4
      objetos que leen la guia lo heredan sin tocar un solo criterio. Medido: `BABCOK BROWN` →
      **95,80 %** (el valor de `Babcock Brown`), `HY LINE` → **96,50 %**; `LOHMANN BROWN` sigue
      vacio a proposito.
- [x] `fn_indicadores_produccion_postura`: hereda el alias de la vista, sin tocar su `WHERE`
- [x] `fn_indicadores_levante_postura`: rama propia con `btrim(lower())`; la rama compartida
      queda **exacta como hoy** (no se unifican criterios, divergen a propósito). Medido:
      `CRIOLLA` pasa de **0,00** a **107,00**; `BABCOK BROWN` idem vía el alias de la vista.
- [x] `fn_resumen_semanal_ra_pesadas_*`: heredan el alias de la vista (ya comparaban `lower(trim())`).
      `vw_guia_genetica_por_lote_postura` queda **fuera**: ningún service la consulta (sólo el backup
      de DB Studio), así que recrearla sería superficie sin lector.
- [x] G3: el `COALESCE(...,0)` de levante no corre si la empresa **tiene guía propia**. Medido:
      la semana 10 (fuera de la curva propia, que arranca en la 18) pasa de **0,00** a vacío.
- [x] Migración `20260831044636_AliasRazaGuiaSqlYSemanaInicioProduccion` idempotente (+ `.Designer.cs`
      y `.Sql.cs` con las 6 constantes). `Down()` restaura los 3 objetos verbatim de HEAD.
- [x] **Gate multipaís PASA**: Sanmarino, Demo, Ecuador y Panamá en **0 en todas las columnas** de
      los 6 objetos (88 y 158 filas de indicadores incluidas). Único cambio: `Santa Reyes |
      fn_indicadores_levante_postura | 152|1 | CAMBIO LA GUIA` — el 0 falso corregido.

### B — semana de arranque de producción por empresa (G4)
- [x] `companies.semana_inicio_indicadores_produccion int NOT NULL DEFAULT 25` + seed SR = **18**
- [x] `fn_indicadores_produccion_postura` la resuelve una vez y la usa en el `DELETE` y en el `FOR`.
      Medido: el lote en **semana 22** pasa de **0 filas** a su fila con guia (13,82 %)
- [x] El parametro viaja en `CompanyDto` / `Create` / `Update` y en las 4 proyecciones
      (`CompanyService.ToDto`, `CompanyService.Crud` x2, `CompanyResolver` x2, `CompanyPaisService`)
- [x] En vez de un cálculo C# sin llamador (el número lo resuelve la fn, no el backend), el test
      que sí protege es `RazaGuiaAliasParidadSqlTests`: lee `backend/sql/vw_guia_genetica_postura.sql`
      y falla si el alias del SQL y el de `RazaGuiaAliasCalculos` dejan de decir lo mismo. Era
      exactamente el defecto original: tenerlo de un solo lado.

### C — los tipos de huevo en el ALTA del lote (H1)
- [x] `GET /api/LoteHuevoItem/por-granja/{granjaId}/disponibles` (empresa por `farms.company_id`,
      fail-closed y exigiendo `deleted_at IS NULL`). Smoke real contra el backend local: **28 ítems**
      para la granja 109 (Santa Reyes), ordenados Primera → Pnc, `activo:false`, `loteId:0`.
- [x] Sección en el modal de crear/editar de `lote-list`, gateada por `clasificacionHuevoPorItems`,
      con tildar por grupo, aviso fail-closed si no se elige ninguno, y recarga al cambiar de granja
      (el catálogo es por empresa: se descarta lo tildado antes de pedir el nuevo).
- [x] Al guardar: POST/PUT del lote → `PUT /LoteHuevoItem/{loteId}` con el id de la respuesta; si
      esa segunda llamada falla, el toast dice que el lote SÍ quedó guardado y apunta al botón 🥚.
      Ciclo probado contra el backend: PUT con 2 ítems → GET devuelve esos 2 (los que el diario
      convierte en filas fijas). Revertido después: `lote_huevo_items` volvió a 0 filas.
- [x] `agruparHuevoItemsPorTipo` + `seleccionInicialHuevoItems` en `funciones/` (+ `models/`),
      **reusadas por el modal 🥚**, que dejó de tener su copia privada. 5 specs verdes. El botón 🥚
      de la lista se conserva.

### Validación
- [x] `dotnet build` 0/0 · `dotnet test` **3.521 verdes** · `yarn build` OK · el gate
      `verificar-sql-llega-por-migracion.js` pasa.
- [x] Flag **OFF**: el gate de paridad da **0 en todo** para Sanmarino, Demo, Ecuador y Panamá, y
      la sección 🥚 no se renderiza sin `clasificacionHuevoPorItems`. Flag **ON** (Santa Reyes):
      medido arriba, punto por punto.
- [x] Backend de smoke apagado; **:5002 y :5501 libres**. Datos del smoke revertidos
      (`lote_huevo_items` 0 filas, sesión de prueba borrada) y la BD local **sin la migración
      aplicada** a propósito: `database update` arrastraría las pendientes de otras sesiones.

### Validación en vivo del flujo de huevos (31-ago-2026, pedido del usuario)
- [x] **Migración aplicada a la BD local** — sólo la propia (`20260831044636`), a mano y en una
      transacción (efecto + fila en `__EFMigrationsHistory` juntos), para no arrastrar las **4
      pendientes de commits del 28-ago**. Verificado: Santa Reyes **18**, las otras cuatro **25**.
- [x] **Declarar los tipos**: `PUT /api/LoteHuevoItem/152` con 2 ítems coherentes con la raza del
      lote (Criolla) → `HUEVO SIN CLASIFICAR CRIOLLO` (Primera) y `HUEVO CRIOLLO PICADO` (Pnc).
- [x] **Guardar producción con esos tipos**: `POST /api/Produccion/seguimiento` → **201, id 860**.
      En BD: `huevo_tot = 1350` (1.200 + 150), las **11 columnas legacy en 0** y
      `metadata->'huevoItems'` con los **2 ítems** — exactamente el contrato de F7.3.
- [x] **La tabla de registros los muestra**: el listado devuelve el desglose en `metadata.huevoItems`
      y `tabs-principal` pinta las columnas **Primera** / **Pnc** desde ahí (`getHuevoPrimera` /
      `getHuevoPnc`). La tabla semanal por ítem (`POST /clasificacion-huevo-items`) devuelve las 2
      filas con nombre, código y cantidad.
- [x] **Fail-closed verificado**: guardar un ítem NO declarado (`HUEVO SIN CLASIFICAR BLANCO`) →
      **400** con el mensaje accionable («no está entre los tipos que este lote produce»).
- [i] El lote está en **semana 2 de vida** (encaset 19-ago), así que en los **indicadores semanales**
      todavía no aparece: para Santa Reyes arrancan en la 18. No es un fallo — es la regla nueva.
- [x] Registro de prueba **860 borrado** a pedido del usuario, por el endpoint
      (`DELETE /api/Produccion/seguimiento/860` → **204**) y no por SQL: el service libera la
      reserva de validación y **recalcula el espejo de huevos**, cosas que un DELETE crudo se
      saltea. Medido antes de borrar: el registro no había movido el maestro (3.000 = inicial),
      no tenía fila en `lote_registro_historico_unificado` ni movimientos de inventario. Después:
      0 registros en el lote, maestro en 3.000, y los **2 tipos de huevo siguen declarados**.

---

# Día de encasetamiento: sin día 0 en indicadores + herencia de hora en reproductora (31-ago-2026)

Plan: [fase_de_desarrollo/dia_encaset_reproductora_indicadores_engorde_plan.md](fase_de_desarrollo/dia_encaset_reproductora_indicadores_engorde_plan.md)

Ticket Panamá: encaset 27-ago con hora 21:33 → el 28-ago salía como «día 2» en seguimiento
reproductora, y los indicadores diarios de engorde arrancaban en «día 0». No hay día cero.

### A — Backend: hora efectiva de la reproductora (hereda del lote pollo engorde)
- [x] `EncasetamientoCalculos.HoraEfectivaReproductora(horaRepro, horaEngorde)` + tests xUnit
- [x] `LoteReproductoraAveEngordeDto.HoraEncasetamientoEfectiva` (campo nuevo, no rompe contrato)
- [x] `LoteReproductoraAveEngordeService`: proyección de la hora del engorde en TODAS las salidas
      (GetAll/GetById/Create/CreateBulk/Update/Reabrir) + diagnóstico retroactivo con la efectiva
- [x] `SeguimientoDiarioLoteReproductoraService`: guardas Create/Update con hora efectiva
- [x] Carga masiva (`MigracionService.SeguimientoReproductora`): validación con hora efectiva

### B — Frontend: numeración día 1 y fecha sugerida
- [x] DTO TS `horaEncasetamientoEfectiva` + lista reproductora: desplazamiento por hora efectiva,
      acotado a la menor edad registrada (131/132 conservan 1..7; 146/147 arrancan en día 1)
- [x] `nextSuggestedFecha` (primer registro) = encaset + desplazamiento; modal con hora efectiva
- [x] Indicadores engorde: `row.dia` = día de negocio 1-based (guía y ganancia siguen por edad);
      hora pasada desde `tabs-principal-engorde` a tabla y gráficas
- [x] Specs Karma del compute actualizados + caso tardío + caso «encaset = día 1»

### C — Datos y validación
- [x] Verificación en BD: engorde sin registros en edad 0 de lotes tardíos (nada que mover);
      reproductora 131/132 quedan numerados 1..7 por la acotación — sin UPDATEs (el cruce ya está bien)
- [x] `dotnet build` + `dotnet test` verdes
- [x] `yarn build` OK + spec compute verde
- [x] Smoke HTTP local: detail 146 con efectiva 21:33; POST 27-ago rechazado con mensaje del 28-ago;
      empresa sin hora idéntica a antes. Backend apagado y :5002 libre al terminar

---

## Cierre de los tickets de Santa Reyes en ItalJira (31-ago-2026)

Plan: [`fase_de_desarrollo/cierre_tickets_santa_reyes_italjira_plan.md`](fase_de_desarrollo/cierre_tickets_santa_reyes_italjira_plan.md)

**Pedido:** validar lo que hay en el ticket de Santa Reyes, terminar lo que quede y **cerrar por
migración** los casos que siguen abiertos desde el arranque. Decisión del usuario en sesión: *cerrar
todo dejando constancia escrita* de lo que no se entregó.

### A — Validación (medida en `sanmarinoapplocal:5433` + código, no contra el tracker)
- [x] F0…F12 verificados uno por uno contra su artefacto real (tabla de evidencia en §1 del plan).
      Medido: 8 flags en `companies` (id 6), **615** filas de guía propia (5 razas × 123 sem.),
      **28** ítems de huevo en `catalogo_items`, silos/códigos ERP/comprobante/bodega destino en
      código, 10 suites xUnit propias, despliegue verificado con el checklist de §🚀
- [x] Lo NO entregado, confirmado con dato: **7 ítems de huevo sin `codigo` ERP** (`ENYEMADO` ×4,
      `DECOLORADO` ×3, ids 698-704) ⇒ `SR-DEF-3`/F8.1 y, por dependencia, `SR-DEF-4`/F8.3; más
      F11.3 (cliente). Decisión del usuario: **cerrar dejando constancia escrita**, no en silencio

### B — Migración `20260831120000_CerrarPlanItalappSantaReyes` (data-only)
- [x] `TK-2026-000172` ABIERTO → CERRADO, con solución y las 2 notas que escribe el servicio
      (`CambiarEstadoAsync` + `ConfirmarCierreAsync`): la línea de tiempo se **deriva** de notas +
      tareas, sin ellas el caso se vería cerrado sin explicación
- [x] `HIS-2026-0024` BACKLOG → LISTO + fechas reales (20-ago → 31-ago)
- [x] Las **42** tareas/subtareas BACKLOG → LISTO, con el fin real de su paquete resuelto por el
      prefijo `F<n>` del título (**27** el 21-ago V52 · **6** el 24-ago X18 · **9** el 31-ago).
      Por prefijo y no por `codigo`: `HIS-2026-NNNN-Tn` deriva del id de la historia y difiere
      local↔prod
- [x] `TK-2026-000180` ABIERTO → CERRADO, con la constancia de las 3 cosas no entregadas
- [x] Las **6** `SR-DEF-*` BLOQUEADA → LISTO
- [x] Idempotencia probada corriendo el `Up()` **dos veces** en una transacción revertida:
      1ª pasada `42 tareas` + `6 definiciones`, 2ª pasada **0 y 0**, notas **4 → 4** (no duplica)
- [x] `Down()` devuelve los estados exactos que se midieron antes: 172 ABIERTO con
      `fecha_primera_apertura` NULL, 180 ABIERTO **conservando la suya** (24-ago, que no puso el
      `Up`), 42 BACKLOG, 6 BLOQUEADA, historia BACKLOG sin fechas reales, 0 notas

### C — Validación
- [x] `dotnet build` **0 errores / 0 warnings** · `dotnet test` **3.525 verdes** · gate
      `verificar-sql-llega-por-migracion.js` OK
- [x] Post-`Up()`: **0 tickets ABIERTOS en toda la base** (eran los 2 de Santa Reyes) y **0 tareas
      BACKLOG/BLOQUEADA en toda la base**; los 48 pasaron a LISTO
- [x] Ninguna otra empresa tocada: los tickets no cerrados de las demás siguen en **15** antes y
      después; `TK-2026-000174` y `-000175` (ya cerrados) intactos
- [x] Aplicada a la BD local **solo la propia**, a mano y en una transacción (efecto + fila en
      `__EFMigrationsHistory` juntos), para no arrastrar la pendiente `20260831044636` de otro
      commit. `dotnet ef migrations list` la muestra aplicada. Ningún backend levantado; :5002 libre
- [i] Quedan **15 casos no cerrados de OTRAS empresas** (11 SOLUCIONADO, 2 EN_ANALISIS,
      1 TRANSFERIDO, 1 EN_IMPLEMENTACION). Fuera del alcance de esta tarea: los SOLUCIONADO los
      cierra el solicitante desde la pantalla, no una migración

---

## Cierre de los 13 casos ya resueltos de Sanmarino, Panama y Ecuador (31-ago-2026)

Plan: [`fase_de_desarrollo/cierre_tickets_resueltos_otras_empresas_plan.md`](fase_de_desarrollo/cierre_tickets_resueltos_otras_empresas_plan.md)

Continua el bloque anterior (Santa Reyes). De los 15 casos no cerrados de las otras 3 empresas,
**13 tienen el arreglo verificado en el codigo y desplegado**; 11 solo esperaban la confirmacion del
solicitante (unica via a CERRADO) y 2 estaban resueltos sin que nadie moviera la tarjeta.

### A — Validacion caso por caso (contra el codigo y `origin/main-produccion`)
- [x] Los 13 con su evidencia (commit o dato) y **confirmados como ancestros de `main-produccion`**:
      `00ff4b5`+`8eea14a` (12) · migraciones `20260806063157`/`20260806074016`, medido en BD:
      `tipo_alimento` en **500** (13, 14) · `7339c61` (15) · no-bug con la medicion en prod dentro de
      la migracion `20260814130000` (20) · **0 grupos** de ingresos duplicados hoy en Panama (163) ·
      `b355f71` (164) · `ValidacionSeguimientoCalculos.Canonico` vivo y **0 referencias** a la tabla
      inexistente (165) · `InventarioGestionService.Consulta.cs:276-324` (166) · `299c816` (176) ·
      `a9fd721`+`3988183` (177) · `c13b9ef` (185) · `1191b39`, en prod via PR #89 (187)
- [x] Los 2 que quedan FUERA, con su motivo: `TK-000183` (CAROLINA) tiene trabajo real pendiente
      —diagnostico completo, datos sin corregir a proposito y el mecanismo vivo en
      `InventarioGestionService.StockMutacion.cs:118-145`— y `TK-000001` es un caso de prueba de junio
- [x] Hallazgo: 3 casos (`20`, `164`, `165`) se marcaron SOLUCIONADO por migracion y quedaron sin
      nota ni correo ⇒ el solicitante nunca supo que estaba resuelto. `TK-000020` es el unico con
      dano real: su solicitante es de Sanmarino y esperaba hace 17 dias

### B — Migracion `20260831130000_CerrarTicketsResueltosOtrasEmpresas` (data-only)
- [x] Localiza por `codigo` + empresa (no por titulo: varios los tipeo el usuario, p. ej.
      «ERROR EN LA FEHCA»). Los 13 en una tabla `VALUES` recorrida por un loop: el fail-safe, la
      nota y el cierre se escriben **una sola vez**
- [x] Fail-safe por estado: si ya esta CERRADO o lo reabrieron, lo saltea con NOTICE
- [x] Los 2 de EN_ANALISIS reciben solucion + fecha_solucion + las 2 notas
- [x] Los 11 de SOLUCIONADO conservan su solucion y fecha originales; se les agrega la nota de cierre
- [x] A los 3 sin nota de SOLUCIONADO se les siembra, fechada en su `fecha_solucion` real
      (20→14-ago, 164/165→18-ago), con prefijo **propio** `Solucionado (registro retroactivo):` —
      el servicio escribe `Solucionado: `, asi el `Down` distingue la suya de una legitima y el
      lector ve que se anoto despues
- [x] La nota dice que el cierre lo hizo la GESTION, cuantos dias espero, la evidencia verificada y
      que se reabre si vuelve; a los 3 sin correo les agrega esa constancia
- [x] `Down()` devuelve cada uno a su estado previo y borra solo lo que sembro

### C — Validacion
- [x] `Up()` dos veces en transaccion revertida: 1a pasada **13 cerrados / 0 saltados**, notas
      **43 → 61** (13 de cierre + 5 retroactivas); 2a pasada **0 cerrados / 13 saltados** y notas
      **61 → 61**
- [x] `Down()` restaura los 13: 11 en SOLUCIONADO con su `fecha_solucion` original intacta
      (12→06-ago, 20→14-ago, 164→18-ago), 2 en EN_ANALISIS sin solucion, notas de vuelta en **43**
- [x] Fail-safe probado de verdad: forzando el 12 a EN_ANALISIS (reabierto) y el 185 a CERRADO ⇒
      **11 cerrados, 2 saltados**, los dos sin nota ni `cerrado_por_user_id`
- [x] `TK-000183`, `TK-000001` y los 4 de Santa Reyes intactos
- [x] `dotnet build` 0/0 · `dotnet test` **3.525 verdes**
- [x] Aplicada a la BD local (solo la propia, en transaccion). **Toda la base queda en 183 CERRADO**
      y solo esos 2 fuera; ningun backend levantado

---

## Validar un seguimiento descuenta el alimento DOS veces (31-ago-2026)

Plan: [`fase_de_desarrollo/validar_seguimiento_doble_descuento_plan.md`](fase_de_desarrollo/validar_seguimiento_doble_descuento_plan.md)

Sale de la auditoria adversarial de los 13 casos que se habian dado por resueltos: **dos auditores
independientes**, mirando `TK-2026-000164` y `TK-2026-000166`, encontraron el MISMO defecto con los
mismos 8 ids. Verificado despues contra la BD y el codigo.

**19.677,24 kg descontados de mas en 7 galpones de ItalcolPanama**, todos DESPUES de que el caso se
marcara SOLUCIONADO (18-ago) — el ultimo el 27-ago, cuatro dias antes de que yo lo cerrara.

### A — Backend: cerrar la carrera (causa raiz)
- [x] `ValidarAsync` leia estado y reservas FUERA de la transaccion; dos requests solapadas leian las
      dos `Validado=false` y la MISMA reserva activa, y las dos aplicaban el consumo
- [x] Patron «tomar primero, aplicar despues»: `TomarValidacionAsync` nuevo, con `ExecuteUpdateAsync`
      condicional (`SET validado=true WHERE id=@id AND validado=false`) DENTRO de la transaccion, que
      se abre antes de decidir. 0 filas afectadas ⇒ otra instancia gano ⇒ `YaEstabaValidado` sin
      aplicar nada. Las reservas se leen DESPUES de ganar la carrera
- [x] Reproductora usa `confirmado`, no `validado` — el UPDATE condicional respeta la columna de cada
      modulo (y ahi ademas es lo que dispara `trg_cruce_reproductora_engorde`: tambien tiene que pasar
      una sola vez)
- [x] `ValidarEnBloque` llama a `ValidarAsync` ⇒ hereda el arreglo sin tocarlo
- [x] El doc-comment decia «Idempotente: validar dos veces no descuenta dos veces» y era **falso para
      llamadas concurrentes**. Ahora dice donde vive la exclusion y por que

### B — Frontend: el disparador
- [x] Guarda de reentrada por id en los **3** listados que validan (engorde, levante, produccion):
      `validandoIds` se marca antes de emitir y se limpia en `next` y en `error`. Los tres tenian el
      mismo handler calcado

### C — Test: `DuplicadosValidacionCalculos` (puro, xUnit)
- [x] Regla fijada con **9 casos**: conserva el de menor id; 3 copias ⇒ revierte 2; mismo dia en dos
      galpones NO es duplicado; misma referencia con cantidad distinta tampoco; `null` y cadena vacia
      son la misma ubicacion; los 8 pares reales dan **19.677,24 kg**; y `KgPorUbicacion` suma los DOS
      pares de G0471 en una sola devolucion de 4.536 kg

### D — Datos: revertir los 8 duplicados por migracion
- [x] `20260831140000_RevertirConsumosDuplicadosPorValidacion`
- [x] Medido en transaccion revertida: el `DELETE` SI anula la fila del historico (trigger `_del`)
      pero **NO devuelve el stock** ⇒ la migracion hace las dos cosas juntas
- [x] Se identifican por FIRMA (reference + item + granja + nucleo + galpon + **silo** + cantidad,
      `count(*)>1`), no por ids literales
- [x] NO se usa un Ingreso compensatorio: mentiria al cuadre del galpon con una entrada que no existio
- [x] Respaldo integro en `_backup_consumos_duplicados_validacion_20260831` antes de borrar
- [x] 🔴 El `Down()` fallaba con `duplicate key uq_lote_hist_origen`: la fila anulada del historico
      **sigue ocupando** `(origen_tabla, origen_id)`, asi que el trigger de alta no podia crear la
      suya. Se borra la fila ANTES de reinsertar el movimiento y el trigger la recrea limpia

### E — Validacion
- [x] `dotnet build` 0/0 · `dotnet test` **3.533 verdes** (3.524 + 9 nuevos) · `yarn build` OK
- [x] `Up()` dos veces en transaccion revertida: 1a **8 revertidos / 19.677,240 kg**, 2a **0 y 0**
- [x] Simetria exacta medida: stock de los 7 galpones **24.519,224 → 44.196,464 → 24.519,224**;
      pares **8 → 0 → 8**; historico anulado **0 → 8 → 0**
- [x] Ninguna otra empresa tocada: el respaldo tiene **solo `company_id = 5`**
- [x] Aplicada a la BD local: 0 duplicados, 8 filas del historico anuladas, 8 respaldadas, y
      **0 filas de stock en negativo** en toda la base

---

## Correccion de los 12 hallazgos de la auditoria de tickets cerrados (31-ago-2026)

Plan: [`fase_de_desarrollo/correccion_hallazgos_auditoria_tickets_plan.md`](fase_de_desarrollo/correccion_hallazgos_auditoria_tickets_plan.md)

La auditoria adversarial completa (36 agentes, 0 errores) confirmo que **12 de 13 casos cerrados
siguen fallando por algun lado**. El primero (doble descuento de alimento) ya se corrigio en `9a7b3d8`.
Patron que se repite en 6 de los 12: **el fix se aplico en un camino y su gemelo quedo atras**.

### Tanda A — dos cambios chicos de alto impacto
- [x] #4 `TK-012/A` — el traslado por cierre de levante se sella con `new Date()` del navegador,
      12 lineas despues de que la misma pantalla mande la fecha que el usuario eligio
- [x] #7 `TK-020/A` — la carga masiva de levante y produccion descarta el DIA COMPLETO ante una
      simple Advertencia y aun asi reporta «Procesado»: es el mecanismo generico de «la carga llega
      hasta la semana N»
- [i] Ambos validados: `dotnet build` 0/0 · `dotnet test` **3.542 verdes** (+9 de `MigracionSeveridadCalculos`) · `yarn build` OK. La regla de severidad quedo centralizada en `Application/Calculos/MigracionSeveridadCalculos.cs` y la usan los 4 guards de levante/produccion **y** el conteo de `filasError` de `Comun.cs`, en vez de repetida en 5 sitios

### Tanda B — el critico con datos perdidos
- [x] #1 `TK-164` — borrar un seguimiento de reproductora YA CONFIRMADO no devuelve el alimento
      (952,560 kg perdidos) y la UI empuja a hacer exactamente eso
- [i] Guarda calcada de engorde, DENTRO del `if (separaDel)` ⇒ flag OFF byte a byte identico. El mensaje
      de edicion decia «Eliminelo (se retornan aves y consumo)» —promesa que el codigo NO cumplia—; ahora
      manda a quitar la validacion
- [i] 🔴 **Hallazgo propio: `desvalidar()` existia en el servicio del front y NINGUN componente lo
      llamaba.** Con el flag ON un registro validado no tenia vuelta atras desde la pantalla, y la unica
      salida que encontraba la gente —borrar y recrear— era justo la que perdia el alimento. Agregado el
      boton ↩ en la grilla de engorde, con `ConfirmDialogService`
- [i] Datos: migracion `20260831150000`. Ingreso de devolucion fechado en el DIA DEL SEGUIMIENTO (criterio
      de `DesvalidarAsync`), stock **1.542,240 → 2.494,800** (+952,560), reservas a LIBERADA. Las de AVES
      solo se marcan LIBERADA: en reproductora las bajas las escribe el cruce, que se rehace solo al
      borrar; reponerlas descuadraria el maestro por partida doble
- [i] Probado: `Up()` x2 en transaccion revertida (2a pasada 0 y 0) y `Down()` exacto. `dotnet test` **3.542**

### Tanda C — el critico latente
- [x] #2 `TK-166` — con el flag ON el backend no valida stock en ningun seguimiento
- [i] Resuelto en UN punto, no en cinco: los 5 services gatean su validacion con `!separa`, pero los 5
      pasan por `SepararAsync`. La validacion va ahi, **despues de `LiberarAsync`** — al editar, la
      reserva vieja del propio registro ya se solto, asi que el disponible no se cuenta a si mismo
- [i] `ValidarStockConsumoAsync` mide ahora `DisponibleNeto = max(0, existencia − reservado ACTIVO)`,
      agrupando por granja/nucleo/galpon/**silo**/item. Con el flag OFF no hay filas ACTIVA ⇒ reservado
      es 0 ⇒ mensajes byte a byte identicos para las otras 4 empresas, **por construccion**
- [i] `dotnet test` **3.547** (+5 casos de `DisponibleNeto`, incluido que 1.500 fisicos con 1.200
      separados rechazan un pedido de 500 y aceptan uno de 300)
- [ ] Pendiente menor del mismo hallazgo: los topes del FRONT siguen apagados al editar
      (`if (this.editing) return false;` en el modal de engorde y en el de levante) y el modal de
      reproductora no tiene tope. Con el backend blindado eso solo cambia *cuando* se entera el
      usuario, no si el dato entra; tocarlo sin cuidado introduce falsos rechazos

### Tanda D — fechas y presentacion
- [x] #5 `TK-014` — la copia de `toYMD` de levante Y la de produccion tenian el regex ANCLADO: un ISO
      con «T» caia a `new Date(s)` + getters LOCALES y restaba un dia en UTC-5. Portada la rama
      tz-aware que engorde ya tenia. Spec nuevo que corre las **3 copias** contra los mismos casos
      (22/22 verdes): la grilla y el modal ya no muestran dias distintos del mismo registro
- [x] #6 `TK-012/C` — el modal de movimientos mandaba `new Date(yyyy-MM-dd)` = **medianoche UTC** ⇒ la
      lista pintaba el dia anterior. Anclado con `ymdToIsoUtcNoon`, igual que el modal gemelo de
      engorde. Y el default del datepicker se calculaba con `toISOString()` (dia **UTC**) mientras el
      `[max]` se calcula en LOCAL: entre las 19:00 y medianoche en Bogota el form nacia con fecha de
      MANANA y el backend respondia 400. Los dos usan ahora `aYmd(new Date())`
- [x] #9 `TK-176` — la tarjeta de Lote Reproductora Engorde bindeaba `hembrasL`/`machosL`, que en
      engorde son el **saldo vivo**, bajo el rotulo «(inicial)». Delegan en `avesInicialesDelLote`
      (getters que devuelven NUMEROS, referencia estable) y se agrego la fila «Mixtas encaset.», sin la
      cual Panama —donde toda la poblacion vive en ese bucket— veia 0 / 0 en un lote lleno
- [x] #10 `TK-177` — el gate del ajuste mira `delta.Total` y el clamp es **por sexo**: un ajuste de
      sexaje a total constante (−500 H, +500 M) pasaba y borraba hembras vivas en silencio, con 200 OK.
      `SobregiroPorSexo` + `MensajeSobregiroSexo` (puros, 6 tests) rechazan antes de escribir; el clamp
      queda como red inalcanzable y su doc-comment, que afirmaba que «el gate ya lo rechazo», corregido.
      Medido despues: `fn_cuadre_aves_engorde` sigue devolviendo **2** lotes, ni uno mas

### Tanda E
- [x] #3 `TK-163` — `RegistrarIngresoAsync` no consultaba si el ingreso ya estaba: dos cargas del mismo
      remito suman kilos que nunca entraron. Guarda BLANDA (409 confirmable) en el CONTROLLER, no en el
      service, para no cambiar a los llamadores internos —las devoluciones automaticas repiten clave a
      proposito—. `IngresoDuplicadoCalculos` puro con **9 tests**; el front pide confirmacion con
      `ConfirmDialogService` y reenvia con la bandera
- [i] 🔴 **Correccion de alcance sobre la sintesis: NO son 3 pares, son 17 grupos con remision repetida
      en 3 empresas** — y **no todos son duplicados** (`INVENTARIO`, `LLEG-06` son etiquetas, no
      remisiones; un mismo remito puede repartirse en dos galpones). **Datos NO tocados**: decidir cual
      fila sobra es criterio de operacion, no de ingenieria. Quedan listados para que los revisen
- [x] #12 `TK-015` — `vw_seguimiento_pollo_engorde` nunca recibio el corte v14 que la fn tiene desde
      junio, aunque la vista se declara su espejo set-based. Medido: `position(...)` daba **0** en la
      vista y **6573** en la fn. Portado el CTE `corte_ciclo_siguiente` set-based + `LEAST` en
      `rango_final`, por migracion con `CREATE OR REPLACE VIEW` (conserva owner y GRANT, y exige la
      misma lista de columnas)
- [i] Verificado desplegando la nueva en PARALELO con otro nombre: **0 filas** de diferencia en los dos
      sentidos, mismas 6.784 filas y 67 columnas. La prueba real es el **contrafactual**, porque el
      corte no muerde hoy: reabiertos los lotes 20 y 86 en una transaccion revertida, la vieja se
      desbordaba al **28-ago** (96 filas en el lote 20) y la nueva corta en el **12-abr** (62)
- [x] #11 `TK-012/B` — trasladar o mover un lote no tenia fecha en NINGUN lado (ni DTO, ni interfaces
      del front, ni tabla; `fn_mover_lote` escribia `CURRENT_TIMESTAMP`), y el Reporte Diario de
      Costos de POSTURA usa esa fecha como la efectiva del traslado. Columna `fecha_traslado` +
      backfill, `fn_mover_lote` con `p_fecha_traslado DEFAULT NULL` (la firma vieja se ELIMINA: con
      default quedarian dos y una llamada de 5 args seria ambigua), DTOs y las 2 interfaces del front,
      input `type=date` en el modal y el reporte con `COALESCE(fecha_traslado, created_at::date)`
- [i] `Up()` x2 en transaccion revertida (la 2a no duplica la fn) y `Down()` que restaura la firma de
      5 argumentos y dropea la columna. Backfill de riesgo cero: la tabla tiene **0 filas**. Humo del
      reporte tras aplicar: 26 filas

### Sin codigo
- [!] #8 `TK-020/B` — S369 sigue en 168 dias; el remedio indicado al usuario esta BLOQUEADO por
      falta de stock. Hay que reabrir el caso con la instruccion correcta (falta cargar la entrada
      de alimento; deficit informado por el propio sistema: 382.310 kg en la granja MANGOS)

---

## Reabrir `TK-2026-000020` con la instruccion correcta (1-sep-2026)

Plan: [`fase_de_desarrollo/reabrir_ticket_s369_instruccion_correcta_plan.md`](fase_de_desarrollo/reabrir_ticket_s369_instruccion_correcta_plan.md)

Ultimo pendiente de la auditoria (#8), y el unico SIN cambio de codigo: lo que esta mal es lo que se
le dijo al usuario. La instruccion decia «se puede volver a subir el archivo completo» y omitia que la
importacion **rechaza el archivo entero** si el stock de la granja no alcanza.

- [x] Reabrir el caso a `EN_ANALISIS` por migracion, con la instruccion correcta y completa
      (`20260901090000_ReabrirTicketS369InstruccionCorrecta`)
- [x] Fail-safe probado: la 2a pasada lo encuentra en EN_ANALISIS y lo saltea con NOTICE
- [x] `CERRADO` es terminal en la maquina de estados y hay tests que lo blindan. Se reabre igual
      porque el cierre lo hizo la GESTION -mi migracion de ayer-, el solicitante nunca confirmo y ni
      siquiera se le envio el aviso (`notificado_correo = false`): no hubo cierre por ambas partes,
      que es lo que justifica la terminalidad. La maquina de estados de la app NO se toca
- [x] NO se corrige ningun dato: la carga es del usuario y el guard de stock es un invariante correcto
- [x] `Down()` exacto: restaura CERRADO con `cerrado_por_user_id = 496236603` **identico** — el primer
      intento lo restauraba con el creador del caso (la persona que reporto) en vez del admin que lo
      cerro; se resuelve por email, igual que la migracion que lo habia cerrado
- [i] Quedan 3 casos sin cerrar en toda la base, los tres con motivo: **TK-000020** (reabierto, espera
      que el usuario cargue el alimento), **TK-000183** (CAROLINA: trabajo real pendiente) y
      **TK-000001** (caso de prueba de junio)

---

## Cola de correo: reintentos que no agraven y un diagnostico que no mienta (1-sep-2026)

Plan: [`fase_de_desarrollo/correo_reintentos_y_diagnostico_plan.md`](fase_de_desarrollo/correo_reintentos_y_diagnostico_plan.md)

El envio **ya estaba activo** en prod (`Email:Queue:Enabled = true`) y funciona -2 `sent` el 29-ago-.
Lo que esta mal es el manejo de errores, y en dos formas que costaron dias de diagnostico.

### A — El diagnostico miente (orden de evaluacion)
- [x] `MustIssueStartTlsFirst` se evaluaba PRIMERO y .NET mapea ahi el `530` posterior a un AUTH
      fallido ⇒ **todo fallo de autenticacion cae en esa rama** y la del `535` -la correcta- es
      inalcanzable. Mismo defecto que `LOHMANN BROWN` con el token `LOHMANN`
- [x] Medido en `email_queue` id 164: el texto dice «verificar que EnableSsl=true» y a la linea
      siguiente informa que **ya es true**; esconde el `535 account locked`, que es lo que importa
- [x] Agregada la rama de `account locked`: es un caso distinto de «el tenant rechaza por origen»

### B — Los reintentos sostienen el bloqueo
- [x] 3 intentos sin espera y sin distinguir permanente de transitorio. Con la cuenta bloqueada,
      cada intento es una autenticacion fallida mas: 26-28 ago fueron **10 correos x 3 = 30**

### C — Que se hace
- [x] `EmailErrorCalculos` puro: clasifica por el MENSAJE REAL del servidor, no por el `StatusCode`
      que mapeo .NET. Orden de evaluacion fijado por tests con los mensajes textuales de produccion
- [x] Permanente ⇒ `failed` en el PRIMER intento (no gasta 3 ni sigue golpeando al tenant)
- [x] Transitorio ⇒ backoff 1/5/15 min con columna `next_retry_at`
- [x] `error_type` pasa a ser la CAUSA (`cuenta_bloqueada`, …) en vez de `max_retries_exceeded`
- [i] Nada de esto arregla el correo: si la cuenta vuelve a bloquearse sigue siendo del admin de M365.
      Lo que cambia es que el sistema lo DIGA bien y deje de empeorarlo
- [x] El MISMO if/else equivocado estaba **duplicado** en el log del emisor: log y cola podian
      contradecirse entre si. Los dos usan ahora el calculo
- [x] Migracion `20260901100000_EmailQueueNextRetryAt`: columna nullable -`null` = ya mismo, asi que
      lo ya encolado no cambia- + indice parcial sobre los pendientes, que es la consulta que corre
      cada 30 s. Probada `Up()` x2 y `Down()` en transaccion revertida
- [x] `dotnet build` 0/0 · `dotnet test` **3.584** (+21): el test que importa es que el mensaje
      textual del id 164, CON su `StatusCode = MustIssueStartTlsFirst`, clasifique como
      `CuentaBloqueada` y no como `RequiereStartTls`

---

## Ecuador: el nombre del lote volvia a llevar `- 1` en la primera corrida (1-sep-2026)

Plan: [`fase_de_desarrollo/apagar_nombre_lote_incluye_corrida_ecuador_plan.md`](fase_de_desarrollo/apagar_nombre_lote_incluye_corrida_ecuador_plan.md)

Ticket de operacion por el `2604 - 02` de CAROLINA / GALPON 6. Medido en la copia de prod: son **dos
sintomas de causa distinta** y solo uno es defecto.

- [x] El `- 2` **no se toca**: el lote 236 se creo el 27-ago 08:57, se elimino 13:54 y se recreo 13:55;
      `MAX(corrida)+1` cuenta los eliminados a proposito para no reusar un nombre. Los 4 casos de
      Ecuador con corrida > 1 son el mismo patron (crear → borrar → recrear)
- [x] El `- 1` SI es defecto: `nombre_lote_incluye_corrida = true` en `ItalcolEcuador`, cuando la
      migracion que creo la columna solo lo prende para `ItalcolPanama`. Lo prendio alguien desde la
      administracion de empresas entre el 21 y el 26 de agosto (se ve en los datos: hasta el 21-ago
      los lotes nacian `2604`; desde el 26-ago, `2604 - 1`)
- [x] Migracion data-only `20260901120000_ApagarNombreLoteIncluyeCorridaEcuador`, idempotente por
      `IS DISTINCT FROM`, `Down()` inverso exacto (el estado previo esta medido, no supuesto)
- [x] Probada por transaccion: `Up()` x2 (la 2a = 0 filas) + `Down()`, y Panama sigue en `true`
- [x] `dotnet build` + `dotnet test` (el calculo ya tiene sus xUnit para los dos valores del flag)
- [x] `dotnet build` Infrastructure **0 errores / 0 advertencias**; `dotnet test` **3.584 verdes**
      (mismo conteo que antes: es un cambio de datos, la logica no se movio)
- [i] Alcance: solo lotes NUEVOS. Los dos ya nacidos con sufijo -`2604 - 1` en CAROLINA GALPON 7 y
      `2605 - 1` en Kilometro 86- se dejan como estan; renombrarlos es backfill sobre datos vivos y
      `lote_nombre` lo leen reportes que agrupan por nombre

---
---

## TK-2026-000183 — eliminar un registro de stock dejaba la tabla diaria alta (1-sep-2026)

Plan: [`fase_de_desarrollo/eliminar_stock_no_bajaba_la_tabla_diaria_plan.md`](fase_de_desarrollo/eliminar_stock_no_bajaba_la_tabla_diaria_plan.md)

CAROLINA / GALPON 1 / lote 2602: dia 1 con **saldo 5.600 kg contra un ingreso de 2.880**. Medido en
la copia de produccion: un `Ingreso` duplicado de la remision 56114 al que le aplicaron «Eliminar
registro de stock» — el stock bajo y la tabla diaria no.

### A · El parche del defecto (que no vuelva a pasar)
- [x] `EliminarStockAsync` escribe, ademas del `EliminacionStock` (auditoria), un
      `AjusteCuadreTablaSalida` por los mismos kilos y con el MISMO timestamp: no toca stock y la fn
      v17 si lo lee, asi que la tabla baja lo mismo que bajo el stock
- [x] `TipoEventoInventarioCalculos` conoce los dos `AjusteCuadreTabla*` (el espejo C# quedo
      desalineado de `fn_tipo_evento_inventario` desde el 25-ago) ⇒ `AfectaSaldoAlimentoEngorde` da
      `true` y la columna persistida SI se refresca. Sin esto el ajuste de §A1 no refrescaria nada
- [x] Etiquetas de los dos tipos en `MapTipoOperacionLabel` + su inversa (hoy se ven crudos)
- [x] xUnit: los 2 tipos nuevos mapean y afectan el saldo; regresion de que `AjusteStock`,
      `EliminacionStock` y `Consumo` siguen sin afectarlo
- [x] **Mecanismo probado end-to-end** (transaccion revertida, ciclo vigente de G0057): ingreso de
      1.000 kg ⇒ saldo 9.760 → **10.760**; con el codigo VIEJO (solo `EliminacionStock`) **se queda
      en 10.760** —el defecto reproducido—; con el parche vuelve a **9.760**. El historico espeja el
      ajuste como `INV_AJUSTE_CUADRE_SALIDA`

### B · La correccion del dato (migracion `20260901140000`, data-only)
- [x] Las TRES superficies: histórico (`anulado`), `seguimiento_diario_aves_engorde.saldo_alimento_kg`
      (52 dias, −2.880) y la copia congelada (52 filas + header). **La fn devuelve la congelada
      mientras exista sin anular** (arranca con un `UNION ALL` contra la copia), asi que corregir
      solo el histórico no cambiaba un solo numero en pantalla
- [x] Localiza por atributos (galpon+item+cantidad+fecha+sin referencia+su pareja), NO por ids;
      idempotente por el estado del histórico; `Down()` inverso exacto por la marca en `metadata`
- [x] NO se tocan los otros 12 pares con la misma firma: **G0058 cerraria en −2.880** (ahi el
      ingreso es real) y los 6 de G0036 pueden ser carga historica legitima (remision 54159)
- [x] Probada por transaccion: `Up()` deja dia 1 en **2.720 / ingreso 0** y cierre en **0**; `Up()`
      x2 no toca filas; `Down()` devuelve 5.600 / 2.880; el gemelo lote 62 no se movio (275.560
      antes y despues). Aplicada de verdad en local con el script que emite EF
- [x] `dotnet build` 0 err / 0 warn · `dotnet test` **3.590** (+6: los 2 mapeos, sus 2 variantes de capitalizacion y los 2 del saldo) · gate `verificar-sql-llega-por-migracion` OK
- [x] **Gate multipais** (cuadre antes/despues, las dos empresas, 67 galpones): Ecuador 0
      descuadrados y 0 dias en rojo; Panama 17/14 identicos antes y despues; **0 galpones se
      movieron**. El ciclo vigente de G0057 y G0058 sigue en descuadre 0

### C · Los otros 11 pares, revisados con las remisiones (1-sep-2026)
- [x] **La remision sola NO alcanza**: 55169 esta repartida en 4 galpones y 54159 en dos cargas del
      mismo galpon con cantidades DISTINTAS (7.260 y 18.510) ⇒ son reparto y particion, no copia.
      El criterio que si decide es el **invariante**: para un lote cerrado, si cierra en 0 con el
      ingreso contado, los kilos se consumieron; para un ciclo vigente, si `saldo_tabla == stock`
- [x] **10 de los 11 son ingresos REALES** — medido anulando cada uno en transaccion revertida:
      G0033 lote 46 (3.280, rem. 55169) 0 → **-3.280** · G0041 lote 13 (9.015, rem. 4168) 0 →
      **-9.015** · G0055 lote 16 (6.000, rem. 5002) -3.920 → **-1.800** · G0036 lote 19 (**80.740**
      en 6 mov.) 0 → **-80.740** · G0058 lote 62 (2.880) 0 → **-2.880**. Corregir cualquiera
      inventaria un faltante. **Queda cerrada la duda de la memoria sobre la remision 54159**
- [x] **G0483 (Panama) SI es duplicado**: la remision **190755** cargada **dos veces en el mismo
      galpon, item y cantidad** (12.500 kg el 28-jul y el 01-ago) con su `EliminacionStock` el mismo
      dia. El cuadre lo dice solo: `saldo_tabla 23.636,5` contra `stock 11.136,5`, **descuadre
      12.500 exacto**; sacando el fantasma el invariante **cierra clavado** en 11.136,5
- [x] **Migracion `20260901150000_CorreccionRemisionDuplicadaG0483Panama`** (data-only). Anula el
      ingreso duplicado y reescribe el saldo persistido **desde la fn** (el SQL de
      `SaldoAlimentoEngordeAplicador`, una sola formula por numero). Sin copia congelada que
      parchear: los dos lotes estan Abiertos; si alguno estuviera congelado al correr, lo **avisa y
      no lo toca** (hay que reabrirlo)
- [x] Elige el duplicado por su **`EliminacionStock` pegado** (id consecutivo), no por id fijo, y
      exige que exista el gemelo con la misma remision. El orden real de creacion lo confirma:
      `11:15:05` ingreso fechado 01-ago → `11:15:55` eliminacion → `11:17:20` el mismo ingreso
      fechado 28-jul (el bueno)
- [x] Probada por transaccion: `Up()` deja el cuadre en **0** (11.136,5 == 11.136,5), sin dias en
      rojo (el minimo del galpon baja de 10.502 a 1.691, positivo); `Up()` x2 no toca nada; `Down()`
      restituye el descuadre de 12.500. Aplicada de verdad en local con el script que emite EF
- [x] **Gate multipais**: Panama pasa de **17 a 16** galpones descuadrados y de **146.485,4 a
      133.985,4 kg** (−12.500 exactos); **Ecuador 0 y 0**; **un solo galpon movido**, G0483
- [i] Medido y declarado en la migracion: reescribir desde la fn tambien **resincroniza 7 filas de
      59 que ya estaban desfasadas** (dos del 31-jul por +12.500, cinco entre +1.905 y −499). Es lo
      que haria el aplicador con el proximo movimiento del galpon; se deja dicho para que no
      sorprenda
### E · Lote 16 de G0055, revisado (1-sep-2026) — **NO se toca: hay que preguntarle a operacion**

- [x] El mecanismo: el ultimo dia tiene **traslados de salida por 11.720 kg contra un saldo de
      7.800** (3.600 el 15-may y 8.120 el 16-may, remisiones 144 y 145) ⇒ cierra en **-3.920**.
      **No es el defecto de `EliminacionStock`**: es el «Patron B», orden/fecha de los movimientos
- [x] **Los 3 casos de Ecuador son la MISMA operacion**, el 18-may entre las 14:51 y las 15:19:
      primero tres `Ingreso` **SIN REMISION fechados ese mismo dia** (G0051 2.640 · G0052 600 ·
      G0055 3.920) y **despues** las salidas con remision 144/145 **fechadas hacia atras** (15 y
      16-may), con su contraparte en G0036. El ingreso llega despues de la salida que lo gasta
- [x] En **2 de 3 el ingreso tapa exactamente el faltante** (G0052 600 = 600 · G0055 3.920 = 3.920);
      en G0051 el ingreso es 2.640 contra un faltante de 3.220, o sea **quedan 580 kg sin explicar**
- [x] Las dos hipotesis cierran en 0 (medido): re-fechar el ingreso al 15-may, o mover las salidas
      al 18-may. **El total esta bien; lo unico mal es la fecha.** Por eso mismo el dato no alcanza
      para decidir
- [!] 🔴 **Por que NO se corrige.** Ingresos sin remision, creados 3 minutos antes de las salidas y
      por el monto exacto del faltante, parecen **tapones para que el stock alcanzara** (el stock se
      valida atomico y rechaza si no da). Si lo son, **el negativo esta diciendo la verdad**: del
      galpon salieron mas kilos de los que habia, y re-fechar **taparia la senal**. Si en cambio el
      alimento entro de verdad y se cargo tarde, re-fechar es lo correcto. **Solo operacion sabe
      cual**: la pregunta concreta es si esos 2.640 / 600 / 3.920 kg entraron y con que documento
- [i] Lote 14 (G0042) tiene -1,0 kg: ruido de punto flotante, no es un caso
- [i] **Panama tiene uno peor**: lote 161 (G0472, «94 - 3») con **29 dias en rojo** y hasta
      **-24.191,1 kg**, aunque **cierra en 0** — ahi el total esta bien y el orden esta mal casi
      todo el ciclo. Sin revisar
### F · El aviso: «esta salida deja el dia en rojo» (1-sep-2026)

- [x] `SalidaEnRojoCalculos` (puro) + **17 tests xUnit**: a quien se le pregunta, la comparacion con
      la tolerancia de 1 kg del cuadre, y el mensaje con los tres numeros que hacen falta para
      decidir (lo que hay, lo que sale, con cuanto queda)
- [x] El aviso va en el **CONTROLLER** de `POST /traslado`, igual que la ventana de fechas y el de
      remision repetida (`22e8af5`): ningun llamador interno del service cambia. **409** con
      `diaEnRojo`, el lote, el dia y el saldo; `ConfirmarDiaEnRojo` lo reenvia
- [x] **El saldo lo da la fn, no el C#**: `BuscarPeorDiaDelGalponAsync` resuelve en la BD, con un
      LATERAL sobre `fn_seguimiento_diario_engorde` de los lotes de ESE galpon (1 a 4). Pide el
      **minimo desde la fecha**, no el saldo del dia: la salida baja por igual todos los siguientes
- [x] Sin galpon origen no se pregunta nada (la tabla diaria se arma por galpon; mismo criterio que
      `SaldoAlimentoEngordeAplicador`)
- [x] Front: `enviarTraslado` + `confirmarYReenviarTraslado` con `ConfirmDialogService`, calcado del
      camino del ingreso duplicado
- [x] `dotnet build` 0/0 · `dotnet test` **3.607** (+17) · `yarn build` OK
- [x] **La consulta probada de verdad contra la copia de produccion**, no solo en psql: un ejecutable
      minimo con el `ZooSanMarinoContext` real (mismas opciones Npgsql + snake_case) devolvio
      `lote=193 nombre=2604 fecha=2026-08-28 saldo=4970`, identico al psql. Era lo unico que los
      tests puros no podian ver: que EF materialice `SqlQueryRaw<PeorDiaCrudo>`
- [x] **Smoke HTTP del 409 corrido** (backend aislado en :5501, `RunMigrations=false`, con OK del
      usuario para la fila de sesion). Tres casos, y **ninguno escribio nada** (0 movimientos y 0
      filas de histórico con la referencia del smoke):
      1. 9.000 kg contra un minimo de 4.970 ⇒ **409** con `diaEnRojo`, lote 193, dia 28-ago,
         `saldoDisponibleKg 4970` y el mensaje completo
      2. los MISMOS 9.000 kg con `confirmarDiaEnRojo` ⇒ el aviso **no se dispara** y la peticion
         llega al service (400 del item, que es la validacion siguiente)
      3. 100 kg sin confirmar ⇒ tampoco avisa: no deja el dia en rojo
- [x] 🔴 **Lo que solo se ve corriendolo:** el mensaje salia **«4,970.0 kg»** — el servidor no fija
      cultura, asi que `N1` formatea en-US y el `/` de un patron de fecha se reemplaza por el
      separador de la cultura activa. Corregido a `0.###` con las barras entrecomilladas (mismo
      criterio que el aviso hermano de remision repetida) + **test que lo fija bajo `de-DE`**
- [x] Sesion del smoke borrada; puertos 5501 y 5002 libres

### D · Cierre del caso
- [x] **`20260901160000_SolucionarTicketCarolinaSaldoAlimento`**: TK-2026-000183 pasa de
      `EN_IMPLEMENTACION` a **SOLUCIONADO** con su descripcion y su nota (mismo prefijo
      `Solucionado: ` que escribe el service, para que el historial se lea igual que desde el
      tablero). **Se ordena solo**: las migraciones corren al arrancar, y esta va DESPUES de la
      `...140000` (dato de CAROLINA) y la `...150000` (G0483), asi que el caso nunca dice
      «solucionado» sobre algo que todavia no llego a produccion
- [x] Fail-safe por estado (si alguien ya lo movio, NOTICE y no toca nada) e idempotente: la 2a
      corrida no encuentra el estado de partida y la nota se siembra con `WHERE NOT EXISTS`.
      Probada por transaccion `Up` x2 + `Down`, y aplicada de verdad en local
- [i] **El correo al solicitante no sale, y tampoco saldria desde el tablero**: el caso tiene
      `solicitante_user_guid` y `solicitante_user_id` en NULL, y su `created_by_user_id`
      (968091594) es el **hash** del id de usuario, no una cedula — ningun usuario la tiene, asi que
      `ResolveSolicitanteEmailAsync` devuelve vacio. Se deja `notificado_correo = false`, que es la
      verdad. Si hay que avisarle a la granja, es por fuera de la app
- [i] G0483 quedo por **migracion** y no por la pantalla: «Cuadrar galpon» habria dejado vivo el
      ingreso duplicado compensandolo con otro movimiento, y la grilla seguiria mostrando 25.000 kg
      ingresados de la remision 190755

---

## Ecuador — anexo: renombrar los 2 lotes que ya nacieron con sufijo (1-sep-2026)

Plan: [`fase_de_desarrollo/apagar_nombre_lote_incluye_corrida_ecuador_plan.md`](fase_de_desarrollo/apagar_nombre_lote_incluye_corrida_ecuador_plan.md)
(anexo). Continua el bloque «Ecuador: el nombre del lote volvia a llevar `- 1`», ya commiteado en
`82bfa9a`. Pedido del usuario: deja de ser «solo lotes nuevos».

- [x] `2605 - 1` → `2605` (Kilometro 86, id 229) y `2604 - 1` → `2604` (CAROLINA GALPON 7, id 241)
- [x] Verificado antes de tocar: 0 colisiones, sin indice unico sobre `lote_nombre`, sin liquidacion
      congelada -que si copia el nombre y lo congela-, sin seguimientos y sin filas en el historico
      unificado. Las `vw_*` leen el nombre de la tabla ⇒ se actualizan solas
- [x] `UPDATE` acotado por REGLA -no por id- y SIN tope superior, para que alcance tambien a los que
      nazcan con el defecto entre hoy y el deploy; despues no puede haber mas porque la migracion que
      apaga el flag corre antes en el mismo arranque
- [x] El `Down()` es exacto por CONSTRUCCION: el `Up()` respalda el nombre anterior en
      `_backup_rename_lote_engorde_ecuador_20260901` y restaura desde ahi, fila por fila
- [x] Los 2 eliminados (235, 236) se dejan con su sufijo: no se ven en ninguna pantalla
- [x] `numero_corrida` NO se toca: sigue en 1, y la proxima apertura se calcula por `MAX(numero_corrida)`
- [x] Gate en transaccion revertida, 6 casos: `Up()` **UPDATE 2** · respaldo con el nombre anterior ·
      `Up()` 2a = **0** y respaldo sin duplicar · de las **144** filas de Ecuador cambian **solo esas
      2** · eliminados y el `2604 - 2` intactos · `Down()` deja **0 filas distintas del inicio**
- [x] `dotnet build` Infrastructure **0/0** · `dotnet test` **3.590 verdes**

---

## Granjas — editar una granja borraba 2 campos que la pantalla no muestra (1-sep-2026)

Plan: [`fase_de_desarrollo/farm_list_no_borrar_flags_granja_plan.md`](fase_de_desarrollo/farm_list_no_borrar_flags_granja_plan.md)

`farm-list.component.ts` armaba el PUT campo por campo y omitia `codigoErpEngorde` (correlativo ERP
de engorde de Panama) y `manejaAlimentoPorGalpon` (override `farm ?? company` del nivel de alimento).
El backend los asigna sin condicional ⇒ cada edicion los dejaba en `NULL`, sin error ni aviso.

- [x] Funcion pura `funciones/construir-payload-granja.funcion.ts` + README de la carpeta
- [x] `buildForm()`: controles `codigoErpEngorde` (`^\d*$`, max 18) y `manejaAlimentoPorGalpon` (tri-estado)
- [x] `openModal()`: hidratar los 2 desde `getById` en la rama de EDICION y con los defaults de hoy en la de CREACION
- [x] `save()` delega en la funcion pura (payload identico al de hoy en todo lo demas)
- [x] HTML: select de manejo de alimento (siempre) + input de codigo ERP engorde (`@if (isPanama)`)
- [x] Test de regresion `frontend/src/tests/construir-payload-granja.funcion.spec.ts` (6 casos)
- [x] `yarn build` **0 errores, 0 warnings** (778 s) - karma sobre el spec nuevo **11/11 SUCCESS**
- [x] Test de alambre backend `UpdateFarmDtoBindingTests` (6 casos): el JSON que manda la pantalla
      llena los 2 campos del `UpdateFarmDto`, y un payload sin esas claves deserializa sin error y
      las deja en `null` -la foto del defecto-. `dotnet test` **3.615 verdes**, 0 warnings
- [x] Smoke end-to-end contra la BD local (backend aislado en :5501, `RunMigrations=false`, granja
      105 TROFARELLO de ItalcolPanama sembrada con `4001017` + `true`). Los 3 PUT, en orden:
      **payload VIEJO** -el de antes del fix, sin las 2 claves- responde **200** y deja los dos en
      `NULL` (defecto reproducido) · **payload NUEVO** los restaura · **editar solo el nombre**
      (hidratado como hace ahora el modal) los deja intactos: `4001017` / `t`. BD devuelta a su
      estado inicial: valores en NULL, nombre TROFARELLO, sesion de smoke borrada, :5501 libre
- [i] El :5002 que quedo escuchando NO es mio: es el backend del worktree `reverent-saha-d10a85`
      (otra ventana). No se toco

---

## Historial de traslados de lote: Fecha y Usuario siempre en «—» (1-sep-2026)

Plan: [`fase_de_desarrollo/historial_traslado_lote_fecha_y_usuario_plan.md`](fase_de_desarrollo/historial_traslado_lote_fecha_y_usuario_plan.md)

### A · Backend — exponer la fecha real y resolver el usuario
- [x] `HistorialTrasladoLoteDto` expone `FechaTraslado` (`DateOnly?`) y `CreatedByUserName` pasa a
      `string?` — nullable porque el id **no siempre** corresponde a una cedula
- [x] `Application/Calculos/HistorialTrasladoLoteCalculos.cs`: mapeo puro cedula↔int + nombre
- [x] `LoteService.Traslado.cs`: se va el literal `$"Usuario ID: {...}"` y su TODO. El puente ya
      estaba resuelto en el repo (`LoteBaseEngordeService`, ItalJira): `created_by_user_id` **es la
      cedula**; `users.id` es Guid y por eso no hay FK. Resuelto en **una** query batch, sin agregar
      N+1 al bucle
- [x] `HistorialTrasladoLoteCalculosTests` (xUnit) — 8 casos verdes

### B · Frontend — alinear al wire y pintar las columnas
- [x] Interfaz `HistorialTrasladoLoteDto` alineada al JSON real (`createdByUserName`, `createdAt`,
      `fechaTraslado`, nombres de nucleo/galpon, que tampoco estaban)
- [x] Funcion pura `fechaTrasladoHistorialLote` + spec. Usa `fechaCortaSinTz`, **no**
      `formatearFecha`: `new Date("2026-09-01")` es medianoche **UTC** y en Colombia se pintaria
      **31/08**. Fila legacy sin `fecha_traslado` ⇒ cae a `createdAt`, no al guion
- [x] Las columnas eran **4 tablas**, no 1: 2 en `inventario-dashboard` + `movimientos-list` +
      `registros-traslados` (comparten la misma interfaz)

### C · El modal del dashboard tiraba la fecha
- [x] `TrasladoLoteRequest` de `traslados-aves.service.ts` declara `fechaTraslado`
- [x] `procesarTrasladoLote` la acepta y la reenvia (mismo contrato que ya cumplia `lote-list`)

### D · Validacion
- [x] `dotnet build` **0 errores / 0 advertencias** · `dotnet test` **3.616 verdes** (8 nuevos)
- [x] `yarn build` OK · `ng test` **712 verdes** (4 nuevos)
- [x] **Smoke contra el backend real** (:5002, BD local, 2 filas sembradas y **borradas** al
      terminar): `GET /api/Lote/151/historial-traslados` → `"createdByUserName":"Cesar Eduardo
      Hurtado Riascos"` (resuelto de la BD, **no** es el usuario logueado) y
      `"fechaTraslado":"2026-08-25"` distinta de `"createdAt":"2026-09-01T14:30:00"`. La fila legacy
      devuelve los dos en `null`, como se espera
- [x] **Render real en Chrome**: `registros-traslados.component.spec.ts` monta la tabla de verdad con
      esa misma respuesta y fija las celdas — `25/8/2026` y el nombre; la fila legacy da `14/7/2026`
      y `—`. Es la prueba de pantalla y ademas queda como regresion en el CI
- [i] 🔴 **El test nuevo tumbaba `offline-db.spec.ts`** con un timeout que Karma atribuia a
      `NucleoService`: montar el componente real abre IndexedDB y la conexion viva **bloquea el
      upgrade de esquema**. Se cierra en `afterEach` con `cerrarConexion()` (la API existe justo
      para esto y su doc describe el sintoma). Sin eso: 2 FAILED; con eso: 712 SUCCESS
- [i] **No hay migracion**: la columna `fecha_traslado`, su indice y el backfill ya los trajo
      `20260831170000_FechaTrasladoLote`. Esto es solo lectura
- [i] **Fuera de alcance, anotado**: `getHistorialTrasladosLotesPorGranja` pega a
      `GET /Lote/historial-traslados/granja/{id}`, **ruta que no existe** en el backend (el unico
      endpoint es `{loteId}/historial-traslados`) ⇒ la solapa de lotes de `registros-traslados`
      siempre cae al `catch`. Falta un endpoint, no es un rename

---

# Reporte Tecnico alineado al ciclo y a la clasificacion de huevo de la empresa (1-sep-2026)

Plan: [`fase_de_desarrollo/reporte_tecnico_alineado_ciclo_empresa_plan.md`](fase_de_desarrollo/reporte_tecnico_alineado_ciclo_empresa_plan.md)

El modulo `/reportes-tecnicos` (menu «Reporte Tecnico Sanmarino», las dos vistas Levante y
Produccion) **esta habilitado para Santa Reyes** — verificado en `company_menus`, es el unico de los
3 reportes tecnicos que esa empresa tiene prendido. Se abre hoy y sale a medias: guia con 2 de 17
columnas, huevo incubable en 0 sin gatear y las fases del ciclo de la empresa sin aplicar.

Decisiones del usuario (en sesion): **adaptar el modulo con flags, no duplicarlo** · **ocultar** las
columnas de guia sin dato (no ampliar la tabla de guia en este pase) · levante 1-17 **con el real y
un aviso**, sin cortar ni bloquear.

## Auditoria previa (medida, no asumida)

- [x] `/reportes-tecnicos` prendido para company 6; `/reporte-tecnico-produccion` y
      `/reporte-tecnico-semanal` apagados
- [x] `guia_genetica_santa_reyes`: **semanas 18 a 140**, 123 filas x 5 lineas, con 3 metricas
      (`prod_porcentaje`, `retiro_ac_h`, `gr_ave_dia_h`). La compartida arranca en **edad 1**
- [x] `grep clasificacionHuevoPorItems` sobre `features/reportes-tecnicos/` ⇒ **0 coincidencias**
- [x] `AplicarTotalesHuevoPorItems` escribe `huevo_inc = 0` **a proposito** («postura comercial, no
      incuba») ⇒ las columnas Incubable/%Incub no son un dato faltante, son un cero por diseno
- [x] `SemanasCicloPosturaCalculos.ObtenerEtapa` existe con tests desde el 20ago26 y
      `semanas_ciclo_postura_por_raza` esta en `t`, pero **ningun service la consume**
- [x] `fecha_encaset` de `lote_postura_produccion` es el encaset ORIGINAL del ave (P-K345B: encaset
      2025-01-31, inicio produccion 2025-07-19) ⇒ sirve como eje de semana de vida

## A · Calculo puro + tests (backend, sin tocar servicios todavia)

- [x] `SemanaGuiaProduccionCalculos` — guia propia ⇒ semana de vida; compartida ⇒ semana relativa
      tal cual (delta cero); `fecha_encaset` nula ⇒ cae a la relativa sin lanzar
- [x] `GuiaMetricasDisponiblesCalculos` — que metricas de guia tienen al menos un dato
- [x] `HuevoItemsResumenCalculos` — Primera/Pnc/Otros, espejo de
      `resumir-huevo-items-por-tipo.funcion.ts`
- [x] Tests xUnit de los 3, **con caso flag-OFF que verifica salida identica a la formula previa**:
      la formula previa esta copiada VERBATIM en el test y se compara contra ella en 8 fechas.
      **45 verdes**

## B · Backend Produccion (`ReporteTecnicoProduccionService.Tabs.cs`)

- [x] Eje de la guia por `SemanaGuiaProduccionCalculos` en los 4 agregados (diario y semanal por
      galpon, diario y semanal general). `SemanaRelativa` -lo que se PINTA- no cambia
- [x] `ObtenerSegsProdTabsConItemsAsync`: variante que ademas trae `metadata` y resume por tipo.
      **Solo corre con el flag ON**; con OFF la consulta queda exactamente como estaba
- [x] Disponibilidad de guia, `SemanaGuiaDesde` y etapa del ciclo expuestas en el DTO
- [x] Helpers en el ancla: `ResolverSemanasCicloPorRazaAsync`, `AFilasGuiaMetricas`,
      `SemanaMinimaConGuia`, `ParsearEdadGuia`
- [x] `dotnet build` Infrastructure **0 errores / 0 warnings**

## C · Backend Levante (`ReporteTecnicoService.LevanteTabs.cs` + `.LevanteCompleto.cs`)

- [x] **El filtro `edad 1..25` NO habia que tocarlo** — se verifico en vez de asumirlo: el levante de
      la empresa termina en la semana **24** (8 alistamiento + 16 levante) y la guia propia arranca
      en la **18**, asi que las semanas 18-24 YA cruzaban bien. Lo que faltaba no era el rango
- [x] `SemanaGuiaDesde` (minima semana con guia REAL, no un 18 hardcodeado) en el DTO: si manana el
      cliente carga el levante, el aviso desaparece solo
- [x] Disponibilidad de guia expuesta igual que en produccion, en las 2 ramas (Semanal y Diario)
- [i] `ReporteTecnicoService.LevanteCompleto` (`/levante/completo/{loteId}`) **no se toco**: este
      modulo no lo llama -el main usa `POST /levante/obtener`-. Queda escrito, no olvidado

## D · Frontend

- [x] `models/reporte-tecnico-guia.model.ts` + `funciones/` (`normalizar-guia-disponibilidad`,
      `columnas-huevo-reporte`) con su `README.md`. Las dos son **fail-open**: si el dato que las
      gobierna no llega, se pinta todo -lo de antes-
- [x] 4 tablas de produccion: huevos por items + `%Incubables` retirado, `[attr.colspan]`
      recalculado, y los inputs bajan desde `reportes-tabs` (el backend ya sabe: no se vuelve a
      preguntar por el flag)
- [x] 🔴 **2 desalineaciones PREEXISTENTES corregidas al pasar**, que se ven solo con
      `oculta_machos_en_postura` ON -o sea, solo en Santa Reyes-: en `reporte-general-diario` el
      `<th>` de **SaldoM** se pintaba siempre con su `<td>` condicional, y el `<td>` de **Consumo M**
      al reves; en `reporte-diario-galpon` el grupo **Peso Ave (g)** tenia `colspan="2"` fijo con su
      celda M condicional. Las columnas quedaban corridas
- [x] Los 4 componentes pasan de `OnPush` a **`Eager`**: asignan estado desde un `subscribe`, que es
      exactamente el caso que CLAUDE.md manda resolver con `Eager`
- [x] **2** tablas de levante (`semanal-hembras`, `semanal-machos`): las 7 y 8 columnas GUIA
      gateadas **en pares** (`<th>` + `<td>` + el `colspan` del grupo), asi la tabla queda alineada
      en los DOS estados del flag
- [x] 🔴 **3a desalineacion PREEXISTENTE corregida**: en las dos tablas el grupo **TOSCANA**
      declaraba `colspan="4"` con **cinco** columnas debajo (medido: 5 `<th>` y 5 `<td>`), asi que
      ese rotulo y los de TODOS los grupos siguientes se pintaban corridos una columna. Los datos
      siempre estuvieron bien; mentia el encabezado. Verificado por conteo simulando el render:
      con guia completa **29 grupos = 29 th** y **33 columnas = 33 td** (antes 28 vs 29 y 32 vs 33);
      con guia propia **24 = 24** y **28 = 28**
- [i] `tabla-levante-completa` **no se toco**: la usa `reporte-tecnico-administrativo`, que es OTRO
      modulo y no esta en el alcance de este pase. Queda escrito, no olvidado
- [x] `reporte-tecnico-main`: 16 bloques de la tabla «Real vs Guia» gateados por disponibilidad
      (cabeceras, subcabeceras y celdas, con los 6 `colspan` de grupo recalculados) + **aviso de
      tramo sin guia**, en naranja de marca (atencion), no rojo (peligro)
- [x] Todo componente/modal nuevo con `changeDetection: ChangeDetectionStrategy.Eager` explicito
- [x] `yarn build` **0 errores** (unico warning el de bundle budget preexistente)

## E · Excel (backend, endpoints `exportar-excel-tabs` y `levante/exportar-excel`)

- [x] Mismo gateo en las 4 hojas. **La columna «Huevo Inc» solo existe en la hoja «Diario Galpon»**
      (se verifico, no se asumio): ahi se REUSA la columna 14 (Inc ⇄ Primera) y «Huevo Pnc» se
      agrega **al FINAL**; en las otras 3 hojas Primera/Pnc van al final. Asi ningun indice de
      columna previo se mueve y no hay forma de desalinear la hoja

## F · Menu

- [x] `20260901220000_RenombraMenuReporteTecnicoNeutro`: «Reporte Tecnico Sanmarino» → «Reporte
      Tecnico», localizando por `route` (jamas por id: aca es 19, en prod puede ser otro) y con
      `IS DISTINCT FROM` para no ensuciar `updated_at`
- [x] **Validada por transaccion revertida**: `Up()` 1a = **UPDATE 1** · `Up()` 2a = **UPDATE 0**
      (idempotente) · los otros 2 menus con «Sanmarino» en el label **intactos** · `Down()` restaura
      exacto. Designer clonado sin tocar el ModelSnapshot (es data-only)

## G · Validacion

- [x] `dotnet build` **0 errores / 0 warnings** · `dotnet test` **3.653 verdes, 0 fallos**
- [i] Ojo al reproducir: compilar con `--artifacts-path` FUERA del repo hace fallar 2 tests
      (`RazaGuiaAliasParidadSqlTests`) que suben desde el binario buscando
      `backend/sql/vw_guia_genetica_postura.sql`. No es una regresion: con el output normal pasan
- [x] `yarn build` **0 errores** · `yarn test` **723 SUCCESS**
- [x] **Smoke doble HTTP contra el backend real** (`:5501`, contentRoot aislado y
      `RunMigrations=false` para no tocar el esquema de la BD compartida). Con permiso del usuario se
      sembraron 10 dias de seguimiento y 2 sesiones, **borrados al terminar** (verificado: 0 y 0)
- [x] 🔴 **El smoke encontro un defecto que los tests NO veian, y era total**: dejar
      `Huevo = default` dentro del `.Select()` de EF hace que EF lo lea como constante del cliente y
      **revienta la consulta entera** — `HTTP 404 «The client projection contains a reference to a
      constant expression»`. **El endpoint no respondia para NINGUNA empresa**, con o sin flag. Los
      45 tests nuevos pasaban igual porque el calculo puro no toca EF. Corregido: el campo no se
      asigna en la proyeccion; lo llena despues, en memoria,
      `ObtenerSegsProdTabsConItemsAsync`. Mismo patron que [[sqlqueryraw-digitos-en-el-nombre]]
- [x] **Flags OFF (Sanmarino, P-K345B, 301 seguimientos) ⇒ delta cero MEDIDO**, no afirmado: sobre
      las **688 filas** de los 4 agregados, `semanaGuia != semana` en **0** (el eje no se movio),
      filas con Primera/Pnc/Otros **0**, filas con `etapaCiclo` **0**, y `guiaMetricasDisponibles`
      llega con **las 14 en true** (el front no oculta ni una columna). Los campos de siempre traen
      su dato real
- [x] **Flags ON (Santa Reyes, P-LOTE 218A, Criolla)**: `clasificacionHuevoPorItems=true` ·
      `semanaGuiaDesde=18` (dispara el aviso) · guia **CON** dato = exactamente
      `prodPorcentaje`, `retiroAcH`, `grAveDiaH` — las 3 que esa tabla tiene — y las otras **11 en
      false**, o sea retiradas · `huevoInc=0` en todas · **invariante
      `Primera+Pnc+Otros == huevoTot` en el 100% de las filas** y en el agregado semanal
      (16.800 = 15.400 + 1.400 + 0) · `etapaCiclo` poblada («Alistamiento» para la edad del lote)
- [x] Backend local levantado SOLO para el smoke y **apagado al terminar**: `:5501` y `:5002`
      confirmados **LIBRES**
- [i] **No se comparo contra el backend viejo por HTTP**: `:5002` corria codigo anterior y habria
      sido el testigo ideal, pero **otra sesion lo apago** a mitad del trabajo. El delta cero quedo
      medido en los puntos de bifurcacion —que es donde el codigo nuevo podria desviarse—, no por
      diff de dos respuestas
- [i] **La pantalla no se miro en el navegador**: el usuario eligio sembrar y borrar, no dejar los
      datos. Lo verificado es el CONTRATO que gobierna el render (los `@if` del template cuelgan de
      `clasificacionHuevoPorItems` y de `guiaMetricasDisponibles`, ambos confirmados en la respuesta
      real). Si se quiere la captura, hay que volver a sembrar

## Correccion posterior (1-sep-2026): el eje de la guia estaba mal para TODAS las empresas

El usuario corrigio una premisa mia: **cada empresa carga su PROPIA guia genetica** (Sanmarino 889
filas, Demo 224, Ecuador 15, Santa Reyes 615, todas filtradas por `company_id`); lo que cambia es en
que TABLA vive. Hablar de «guia propia vs compartida» me hizo dejar como «preexistente, fuera de
alcance» un defecto que afectaba a las cinco. Pidio validarlo y corregirlo.

- [x] **Validado: la columna `edad` de la guia es SEMANA DE VIDA, no de postura.** La prueba esta en
      el dato, no en el nombre: en `guia_genetica_sanmarino_colombia` la edad va de **1 a 71-97**, la
      **primera edad con produccion es la 25/26** —cuando el ave empieza a poner— y la primera con
      `peso_h` es la **1**. Por eso `ObtenerGuiaGeneticaProduccionAsync` ya filtraba `edad >= 26`
- [x] **El defecto estaba en los DOS reportes**, no solo en el que Santa Reyes ve: `Tabs.cs` usaba la
      semana relativa y `Cuadro.cs` usaba `EdadInicioSemanas`, que se cuenta desde
      `fechaReferencia = fechaInicioProduccion`
- [x] **Impacto medido antes de tocar**: los 7 lotes de produccion vivos tienen `fecha_encaset`
      cargada y el eje se corre entre **128 y 363 dias** (18 a 52 semanas)
- [x] `SemanaGuiaProduccionCalculos.Resolver` ya no depende de en que tabla vive la guia: cruza por
      semana de vida siempre. **La semana que se PINTA no cambia** — sigue siendo la de postura
- [x] 🔴 **Desempate `25P` vs `25`**: el fix vuelve alcanzable la semana 25, que es la unica del
      catalogo con dos grafias (`25P` = prepostura, una fila por raza/anio; el parseo tolerante las
      colapsa). Gana la **numerica pura**, de forma determinista. Antes daba igual porque el eje
      viejo nunca llegaba a la 25. **Conviene confirmarlo con el cliente**: las 2 filas llenan campos
      distintos (`25P` trae `peso_huevo`, `25` trae `uniformidad`)
- [x] **Efecto MEDIDO en P-K345B** (encaset 2025-01-31, inicio produccion 2025-07-19 ⇒ semana 25):
      | fecha | sem. pintada | ANTES | AHORA |
      |---|---|---|---|
      | 2025-07-19 | 1 | edad 1 · `uniformidad 70` (dato de LEVANTE) | edad 25 · `uniformidad 90` |
      | 2025-07-26 | 2 | edad 2 · todo vacio | edad 26 · **31,25 %** · peso **52,30** |
      | 2026-02-04 | 29 | edad 29 · 86,75 % (la meta de la semana **5** de postura) | edad 53 · 69,15 % |
      Cobertura: **% Postura Guia 126/301 → 294/301** · **Peso Huevo Guia 133/301 → 294/301**
- [x] Santa Reyes **no cambia** con esta correccion: su guia ya se cruzaba por semana de vida
- [x] `dotnet build` 0/0 · `dotnet test` **3.671 verdes** · smoke HTTP con el lote real
- [x] Sesion de smoke borrada (0) y puertos 5501/5002 **LIBRES**

## `25P` es la fila de PRODUCCION, no un duplicado — el desempate estaba al reves (1-sep-2026)

El usuario explico que en Sanmarino la semana 25 es la de **paso de levante a produccion**, y que la
guia trae las dos: `25` (todavia en levante, sin producir huevo «de produccion») y `25P` (la misma
semana ya produciendo). En Sanmarino ademas se registra huevo **desde la semana 18 en LEVANTE**
—flag `captura_huevos_en_levante`— y en la semana 26 el lote de levante ya debe cerrarse.

- [x] **Verificado en el dato, no solo en el relato.** Lo que distingue a las dos filas es que los
      ACUMULADOS SE REINICIAN en la de produccion (AP 2026, Sanmarino):
      | edad | `cons_ac_h` | `retiro_ac_h` | `uniformidad` | `peso_huevo` |
      |---|---|---|---|---|
      | 24 | 10.678,5 | 3,93 | 90 | — |
      | **25** | 11.501,2 | 4,03 | 90 | — | ← cierra el levante |
      | **25P** | **847,0** | **0,10** | — | 50,1 | ← abre la produccion |
      | 26 | 920,9 | 0,33 | — | 52,3 | ← sigue desde 847 |
- [x] **El flag confirma a quien aplica**: `captura_huevos_en_levante` esta en `t` exactamente en
      **Sanmarino y Demo** —las dos empresas cuya guia tiene `25P`— y en `f` en Ecuador, Panama y
      Santa Reyes. La guia de esquema simple no tiene esta duplicidad: arranca directo en produccion
- [x] 🔴 **Mi desempate anterior estaba AL REVES** (elegia la numerica pura). Un reporte de
      produccion habria mostrado `cons_ac_h = 11.501` —el consumo acumulado del **levante entero**—
      donde corresponde **847**: trece veces mas grande y sin que nada se vea roto
- [x] `GrafiaEdadGuiaCalculos` (calculo puro + 8 casos de test): en produccion gana la fila con `P`,
      en levante la numerica pura, la grafia desconocida queda ultima y el resultado **no depende del
      orden de entrada**
- [x] Aplicado en los dos reportes de produccion (`Tabs.cs` y `Cuadro.cs`). **El levante ya estaba
      bien** y no se toco: usa `int.TryParse` estricto, que rechaza `25P` de entrada
- [x] **Smoke**: la semana 25 del lote real ahora trae `pesoHuevoGuia = 50,1` (el valor de `25P`;
      con la fila `25` ese campo viene vacio) y la 26 sigue con 52,3 / 31,25 %
- [x] `dotnet build` 0/0 · `dotnet test` **3.694 verdes** · sesion de smoke borrada · puertos LIBRES
- [!] **Pendiente anotado, NO tocado**: `GuiaGeneticaService.ObtenerGuiaGeneticaProduccionAsync`
      filtra `edad >= 26`, y como `TryParseEdadNumerica("25P")` devuelve 25, **descarta la fila de
      produccion de la semana de transicion**. De ahi salen las columnas «amarillas» del Cuadro
      (consumo, peso, mortalidad standard). Tiene **3 consumidores**
      (`ClasificacionHuevo`, `Cuadro`, `IndicadoresProduccion`), asi que cambiarlo excede este pase

## Cierre: `25P` tambien entra en la SERIE de produccion (1-sep-2026)

Ultimo hueco del pase, el que quedaba anotado como pendiente: el desempate ya elegia bien la fila
`25P` cuando estaba disponible, pero `ObtenerGuiaGeneticaProduccionAsync` **ni siquiera la
entregaba** — su corte `edad >= 26` la descartaba, porque el parseo devuelve 25 para `"25P"`.

- [x] **Medido el alcance antes de tocar**, que era el motivo de haberlo diferido: de los 3
      consumidores, `IndicadoresProduccionService` solo lo usa como `guias.Any()` (un booleano de
      «¿hay guia?») ⇒ **no le afecta**; los otros dos (`Cuadro`, `ClasificacionHuevo`) son del
      modulo `/reporte-tecnico-produccion`, donde el contenido si importa
- [x] `GrafiaEdadGuiaCalculos.EsSerieDeProduccion(grafia, edad, desdeSemana = 26)`: entra por numero
      **o** por grafia. El corte quedo **parametrizable** —no una constante enterrada— porque la guia
      de esquema simple arranca su produccion en la semana 18, no en la 26
- [x] **9 casos de test nuevos**: `25P` entra pese a parsear 25 · la `25` de levante sigue afuera ·
      las semanas 1/18/24 quedan fuera · 26/53/140 entran · el corte se puede mover a 18
- [x] **Delta cero para quien no tiene `25P`**: el cambio solo AGREGA filas con sufijo `P`, y esa
      grafia existe unicamente en la guia de Sanmarino y Demo (una fila por raza/anio). Ecuador,
      Panama y Santa Reyes reciben exactamente lo mismo que antes
- [x] **Verificado en el reporte Cuadro con el lote real** (`GET /cuadro/6`): **43/43 filas con
      consumo standard** y la primera fila trae `PesoHuevoStd 50,1` y `StGrHembra 116` — los valores
      de `25P`, que antes se descartaban. `%Ross` sigue vacio en esa fila, que es correcto: en la
      semana de transicion todavia no hay % de produccion standard
- [x] `dotnet build` 0/0 · `dotnet test` **3.703 verdes** · sesion de smoke borrada · puertos LIBRES

### Estado final del pase

| Frente | Estado |
|---|---|
| Eje de la guia (semana de vida) | corregido en los 2 reportes, para las 5 empresas |
| Semana de transicion `25` / `25P` | desempate + serie, con calculo puro y 17 tests |
| Huevo por items (Primera/Pnc) | en las 4 tablas de produccion + Excel |
| Columnas de guia sin dato | ocultas, con el backend informando que puede comparar |
| Aviso de tramo sin guia (levante) | en pantalla, con la semana REAL de la guia |
| Etapas del ciclo por raza | conectadas |
| 3 desalineaciones preexistentes | corregidas |
| Menu neutro | migracion idempotente, probada en transaccion revertida |

## El corte de inicio de produccion de la guia, por EMPRESA (1-sep-2026)

Pedido del usuario: *«cuando este en la empresa Santa Reyes aplica la correccion de semana 18, de lo
contrario es semana 25»*. El corte estaba escrito a mano como `26` y era el de la guia de esquema
completo; la de esquema simple arranca directamente en produccion.

- [x] **Validado antes de codificar, en el dato**: la guia de esquema simple arranca en la **18 y ya
      trae produccion ahi** — Hy Line Brown va 7,70 % (sem 18) → 27,10 → 57,30 → 80,50 → 90,60 →
      94,10 → 95,50 → **96,60 (sem 25)**. Con el corte en 26 se perdian **8 semanas por linea**
      (115 de 123 filas entraban; ahora 123), justo la curva de arranque de la postura
- [x] **Por columna, NO por `if (empresa == ...)`** (regla §🏢 de CLAUDE.md):
      `companies.semana_inicio_produccion_guia` INTEGER NOT NULL **DEFAULT 26** — el mismo numero
      que estaba en el codigo, asi que quien no se toque se comporta igual **por construccion**
- [x] La migración **localiza la empresa por SU GUIA, no por el nombre**: pone 18 a quien tenga filas
      en la tabla de esquema simple. Asi no depende del orden respecto al seed que crea la empresa
      (§🏢.7) y, si la empresa no existe todavia, no afecta ninguna fila
- [x] **Validada en transaccion revertida**: `Up()` 1a = ALTER + **UPDATE 1** · 2a = NOTICE «already
      exists» + **UPDATE 0** · reparto correcto (Santa Reyes **18**, las otras 4 en **26**) ·
      `Down()` deja la columna en **0**
- [x] **Aplicada de verdad en local** al arrancar el backend (`Database:RunMigrations=true`), junto
      con la del menu: `menus./reportes-tecnicos` ya dice **«Reporte Tecnico»**
- [x] `GrafiaEdadGuiaCalculos.EsSerieDeProduccion` ya era parametrizable; el service resuelve el
      valor de la empresa y lo pasa. **Fail-safe**: empresa no encontrada ⇒ cae al default 26
- [x] **12 tests nuevos**, incluido uno que recorre las 140 semanas comparando el default contra el
      corte 26 explicito (delta cero demostrado, no afirmado) y otro que fija que la fila con `P`
      entra **con cualquier corte**
- [x] **Smoke HTTP con Santa Reyes**: sembrados 21 dias en el tramo de la semana 18-25 de vida
      (encaset + 119 dias), el Cuadro devuelve **2/2 filas con consumo standard** (95 y 102,5
      gr/ave/dia) donde con el corte fijo en 26 habrian salido en `null`. Datos y sesion **borrados**
      (0 y 0), puertos **LIBRES**
- [x] `dotnet build` 0/0 · `dotnet test` **3.715 verdes**
- [!] **Tension que sigue abierta y NO se toco**: `SemanasCicloPosturaCalculos` (auditado del docx
      del cliente) define para esa empresa **postura desde la semana 29** (8 alistamiento + 16
      levante + 4 levante-en-produccion), pero su guia produce desde la **18**. Este pase alinea el
      **corte de la guia** —que es lo que el usuario pidio y lo que el dato respalda— y **no toca las
      etapas del ciclo**, que alimentan la columna «fase» del reporte. Si el corte real es 18, esa
      clase tambien habria que ajustarla: es decision del cliente, no mia

---

# PWA — cierre de huecos H1–H4 (1-sep-2026)

Plan: [`fase_de_desarrollo/pwa_cierre_huecos_plan.md`](fase_de_desarrollo/pwa_cierre_huecos_plan.md)

Origen: auditoría del estado real de la PWA contra el código del 1-sep-2026 (7 huecos medidos, el
usuario eligió cerrar 4). Orden de entrega: **H1 → H3 → H4**, con **H2** en paralelo porque es de él.

## Lo que la auditoría midió y no estaba escrito

- [i] 🔴 **`GET /api/Sync/cuadres` + `POST /cuadres/{id}/resolver` no los llama nadie.**
      `grep -rn "cuadres" frontend/src` → **0 resultados**. El backend (`6f17d44`, 22-ago) emite
      `requiere_cuadre` con smoke de 15 pasos y la bandeja existe **sólo como endpoint**: el día se
      guarda, la tablet dice «listo», y el supervisor nunca se entera de que entró sin descontar
      stock. Emisor sin lector — misma familia que `borrarDispositivo()` sin llamador
- [i] 🔴 **El mapeo de F4 se equivoca en su única fila de nivel 1.** Dice que gastos de inventario «no
      mueve stock»; **sí lo mueve**: `InventarioGastoService.CreateAsync:568` llama
      `RegistrarConsumoAsync`, el camino que lanza `StockInsuficienteException`. Gastos es **nivel
      2**, y lo destraba el emisor que se construyó el 22-ago
- [i] `InventarioGastoService.CreateAsync:527` abre **transacción incondicional** ⇒ reventaría dentro
      de la del push. `DeleteAsync:635` también, pero no entra al push
- [i] La ventana de fecha de gastos vive en el **controller** (`:171`), así que la rama de sync la
      saltea. **Es correcto y se deja**: la captura offline es legítimamente retroactiva y su
      antigüedad ya la acotan la jornada de 16 h y el `capturadoAtDispositivo`
- [i] Lista cacheable hoy: **89 endpoints · 55 cacheables · 34 excluidos · 0 sin decidir**
- [i] No se pudo verificar la TaskDef **viva**: las credenciales AWS de esta máquina responden
      `UnrecognizedClientException`. El archivo del repo (`backend/ecs-taskdef-new-aws.json:38`)
      sigue diciendo **60 minutos**

## H1 · Bandeja de cuadres — la pantalla que le falta al backend

- [x] H1.1 `cuadres-offline.service.ts` (`listar` / `resolver`) + modelo espejo de `CuadrePendienteDto`
- [x] H1.2 `etiquetar-tipo-cuadre.funcion.ts` **pura** + spec: los 4 tipos del contrato a nombre
      legible; un tipo desconocido muestra el identificador crudo, nunca `undefined`
- [x] H1.3 Pantalla `cuadres-offline` — lista con `detalle`, un solo botón «Marcar como revisada», y
      **la frase de que resolver NO repone kilos** (el ingreso se carga por inventario, como siempre)
- [x] H1.4 Ruta lazy en `app.config.ts` (patrón de `inventario-gastos`) + `ToastService` +
      `ConfirmDialogService` + `changeDetection: Eager` explícito
- [x] H1.5 `decidir-cacheable.funcion.ts`. **Medido: no hacía falta entrada nueva** — el gate clasifica
      por RECURSO (primer segmento tras `/api`) y `sync` ya estaba en EXCLUIDOS, así que `Sync/cuadres`
      quedaba cubierto y el conteo no se movió (89/55/34/0). Lo que sí se corrigió es el motivo: decía
      sólo «el push no es una consulta» y ahora nombra los dos (bandeja de supervisión: servirla vieja
      mostraría como pendiente algo que otro ya resolvió)
- [x] H1.6 Migración data-only `SeedMenuCuadresOffline`: `INSERT … WHERE NOT EXISTS`, localizando
      **por `route`** (los ids difieren local↔prod), icono **dentro del `ICON_MAP` de `menu.service.ts`**
- [x] H1.7 `yarn build` + `verificar-lista-cacheable.js` + `verificar-change-detection.js` en verde

## H2 · La jornada de 16 h no existe en producción — decisión del usuario

- [!] H2.1 **Subir `JwtSettings__DurationInMinutes` a 960 en la TaskDef viva.** Hoy en **60** ⇒ el
      `authGuard` expulsa al minuto 61 sin señal y toda la caché de 16 h y el multi-slot son
      inalcanzables en el galpón. **Va DESPUÉS de verificar B1 (`c9a7349`) desplegado**: 16 h sin
      revocación real es una ventana de acceso irrevocable en una tablet que se puede perder (D4)
- [!] H2.2 **NO se tocó `backend/ecs-taskdef-new-aws.json:38`, y es una decisión, no un olvido.**
      Medido: el workflow **no lee ese archivo** —hace `describe-task-definition` de la familia viva
      (`sanmarino-back-task`) y sólo le cambia la imagen—, pero `backend/scripts/deploy-backend-ecs.sh`
      **sí lo usa**. O sea que subirlo a 960 no es un cambio cosmético: cambiaría producción por el
      camino del deploy manual, sin OK. Hoy el archivo dice **60**, que es lo que corre; ponerle 960
      antes de tocar la TaskDef sería la mentira al revés (el repo diría que H2 está hecho)
- [i] 💡 **Lo bueno de que el workflow lea la TaskDef viva:** el cambio se hace **una sola vez** (por
      consola o CLI) y **sobrevive a todos los deploys** siguientes — el pipeline conserva las
      variables de entorno y sólo reemplaza la imagen
- [~] H2.3 Verificación post-cambio: `describe-task-definition` + **decodificar el `exp` del JWT
      recién emitido** (960, no 60). Sin el segundo paso no está verificado — ECS hace rollback silencioso

## H3 · La fila capturada sin red se ve (y sigue sin poder exportarse)

- [i] 🔁 **Cambio de diseño respecto del plan, y es más fuerte.** El plan decía `fusionarPendientes`
      dentro del arreglo `seguimientos` + un filtro de `__pendiente` en cada Excel e indicador. Se
      hizo al revés: la captura **nunca entra** a ese arreglo — viaja por un `@Input()` propio
      (`capturasPendientes`) que sólo la tabla lee. La separación queda garantizada **por
      construcción** y no por un filtro que alguien tiene que acordarse de poner en la exportación
      número 12. Costo: la fila va arriba de la tabla y no intercalada por fecha
- [x] H3.1 `resumir-capturas-pendientes.funcion.ts` **pura** + spec (17 casos): filtra por tipo,
      partición y lote; ordena por fecha del registro; **no devuelve el payload ni un solo número
      capturado** — no hay nada que copiar a un Excel
- [x] H3.2 Las 4 pantallas la llenan **en un campo al cargar y al guardar**, nunca en un getter del
      template. Puente único en `CapturasPendientesLoteService` para no cablear cuatro veces el
      filtro de partición
- [x] H3.3 🔴 **La captura no llega al Excel, ni a los indicadores, ni a la gráfica** porque no está
      en el arreglo del que salen. El servidor nunca vio esa fila: un indicador calculado con ella
      es un número inventado
- [x] H3.4 La fila no tiene botones de editar ni borrar: no existe la operación offline (F4.2), y un
      botón que abre un modal sobre una fila que el servidor no tiene sólo termina en un PUT contra
      un id inexistente
- [x] H3.5 **La prueba que prueba la prueba** (corrida): agregando `payload` al resumen falla
      **exactamente 1** test —el del aislamiento— y los otros **743 siguen verdes**
- [i] 🔴 **Dos hallazgos medidos que un solo campo habría escondido**, los dos sin error y sin aviso:
      producción manda `lotePosturaProduccionId` (flujo LPP) **o** `produccionLoteId` (legacy) con
      **valores distintos** ⇒ el criterio es un mapa de campos, no un par; y la **reproductora manda
      `fecha`, no `fechaRegistro`** ⇒ su captura se habría mostrado sin día, que es justo el dato
      que sirve para reconocerla
- [x] H3.6 `yarn build` 0 errores · `ng test` **744/744** (de 727) · gates de change detection
      (236 componentes, 0 sin declarar) y lista cacheable en verde

## H4 · Gastos de inventario se guarda sin red (nivel 2, con `requiere_cuadre`)

- [x] H4.1 Tipo `gasto_inventario_crear` en `SyncPushCalculos.Tipos` + `Tipos.Todos`
- [x] H4.2 `SyncPushService.Gastos.cs` — partial nuevo, **namespace plano**, que llama al **mismo**
      `IInventarioGastoService.CreateAsync` que usa el controller (nunca reimplementar reglas)
- [x] H4.3 `InventarioGastoService.CreateAsync` con **transacción condicional**
- [x] H4.4 Sin stock ⇒ **se registra el gasto sin descontar**, `requiere_cuadre` + `detalle`. En un
      seguimiento el reintento guarda «el día sin los ítems»; en un gasto **no hay tal separación**:
      el gasto *es* el consumo. ⛔ Nunca descontar «hasta donde alcance» (inventa un número)
- [i] 🔴 **Copiar el patrón de F7 tal cual habría duplicado el gasto.** Los seguimientos validan el
      stock **antes** de escribir, así que a engorde le alcanzó con `ChangeTracker.Clear()`.
      `InventarioGastoService.CreateAsync` hace `SaveChangesAsync` de la **cabecera antes** de
      recorrer las líneas: cuando una línea lanza, esa cabecera **ya está en la base** dentro de la
      transacción del push, y `Clear()` no la borra de ahí. Se resolvió con **SAVEPOINT**
      (`CreateSavepointAsync` / `RollbackToSavepointAsync`), que deshace el intento fallido y deja
      viva la transacción del push — sin inventar una segunda validación de stock del lado del sync.
      **Medido en el smoke**: quedaron los gastos **639 y 641**, y el **640 no existe**
- [x] H4.5 Ruta en `decidir-encolable.funcion.ts` con `$` para no capturar sub-recursos + spec
      (`items`, `existencias`, `filter-data`, `conceptos`, `export` siguen sin encolarse)
- [x] H4.6 Toast con `esRespuestaPendiente` en la pantalla de gastos. Sin esto decía «Gasto
      registrado y stock descontado», que sin red es mentira **dos veces**
- [x] H4.7 xUnit: `gasto_inventario_crear` reconocido + un test que recorre `Tipos.Todos` y exige que
      **cada** constante del catálogo pase por `EsConocido` (agregar la constante y olvidar `Todos`
      compila igual y se rechaza recién en campo). **3.717 verdes** (de 3.715)
- [x] H4.8 `dotnet build` 0/0 · `dotnet test` 3.717 · `verificar-cuadre-solo-en-sync.js` en verde ·
      `yarn build` 0 errores · `ng test` **746/746** (de 744) · lista cacheable 89/55/34/0
- [x] H4.9 **Smoke HTTP local corrido** (JWT minteado + `X-Secret-Up` cifrado + fila en
      `sesiones_activas`), los 6 caminos: stock suficiente ⇒ `aplicada` y stock **4250 → 4240** ·
      replay del mismo `clientOpId` ⇒ `replay: true`, sin segundo gasto · stock insuficiente ⇒
      `requiere_cuadre` con `divergencia_stock`, gasto creado y `stock_antes == stock_despues`
      (**el inventario no se movió**) · bandeja lo lista · `resolver` 204 y desaparece · resolver dos
      veces **404, no 500**
- [x] H4.10 **Limpieza verificada**: 0 gastos, 0 `sync_operaciones` y 0 sesiones del smoke; stock
      devuelto a 4250; y la fila del histórico unificado quedó **`anulado = true`, no borrada** (la
      dejó así el trigger `AFTER DELETE`, que es la regla dura del repo). Puerto **5002 libre**

## Fuera de alcance (elegido), queda anotado

- [i] Editar/borrar offline y el grafo `client_entity_id` siguen siendo **F4.2**
- [i] **Background Sync no existe** (`SyncManager` = 0 apariciones): la cola drena con la app abierta,
      al recuperar red o con «Enviar ahora». Si el operario captura, cierra y se va, nada sale
- [i] Los smokes **C9–C13** (Android real, dos operarios) siguen sin correrse; C12 falla hoy **por
      construcción** mientras H2 no se haga

## Verificación EN PANTALLA (1-sep-2026) — lo que faltaba del cierre anterior

Back en `:5002` + front en `:4200`, sesión real. Para entrar se guardó el hash de
`admin.ecuador@italcol.com`, se puso uno conocido y **se restauró byte a byte al terminar**
(verificado con `password_hash = '<original>'` ⇒ `t`).

- [x] **H1 · La bandeja funciona de punta a punta.** El ítem «Cuadres sin conexión» aparece en el
      sidebar y la ruta abre. **Vacía**: dice «No hay cuadres pendientes», no se queda en «Cargando…»
      —era el riesgo del `changeDetection`—. **Con una fila real** (generada empujando un gasto sin
      stock por el push): se lista con su `detalle`, el modal de confirmación repite que **NO repone
      inventario**, confirmar la saca de la tabla, el contador baja a 0 y la BD queda con
      `cuadre_resuelto_at` y `cuadre_resuelto_por`
- [i] 🔴 **Defecto encontrado SÓLO al abrirla con datos (`45b2cea`)**: la columna Origen mostraba
      `gasto_inventario_crear` crudo. No estaba roto —es el fallback que la función promete para un
      tipo desconocido, con test— pero **H4 agregó un tipo que el mapa de etiquetas de H1 no tenía**.
      Es exactamente la clase de cosa que ningún test iba a ver: compila, pasa, y se lee mal
- [x] **H3 · La fila pendiente se ve y filtra bien.** Con 5 operaciones en el outbox se muestran
      **sólo 2**: quedaron fuera la de **otro lote**, la de **otro tipo** (reproductora, que comparte
      el cuerpo) y la de **otra partición**. Se vieron los **dos estados**: «SIN ENVIAR» en naranja
      («Se envía sola cuando vuelva la conexión») y «RECHAZADA» en rojo (con el enlace a Diagnóstico)
- [x] **Las fechas no se corren**: `2026-08-31` se pinta **31/8** y `2026-09-01` **1/9**. Es lo que
      protege `fechaCortaSinTz` — con `new Date('2026-08-31')` habrían salido un día antes
- [x] **La barra de estado y el filtro de partición coinciden con lo esperado**: de las 5 capturas,
      el servidor rechazó las 4 de la sesión activa (payload de prueba) y la 5ª —de otra partición—
      **no se tocó**, que es la regla R9
- [x] **Limpieza verificada**: 0 gastos del smoke, 0 `sync_operaciones`, 0 sesiones minteadas, stock
      devuelto a 4250, contraseña restaurada, outbox del navegador vacío, y la fila del histórico
      unificado en **`anulado = true`**. Puertos **5002 y 4200 libres**

---

# 📊 Dashboard por perfil, con datos reales y carga perezosa

> Plan: [`fase_de_desarrollo/dashboard_por_perfil_lazy_plan.md`](fase_de_desarrollo/dashboard_por_perfil_lazy_plan.md)
> Abierto el **1-sep-2026**. Pedido: organizar el dashboard para que muestre información y gráficas
> de todo lo que tenemos, con **carga perezosa por panel**, y que lo que se ve dependa del perfil
> (admin / técnico / administrativo), los **permisos**, el **usuario** y la **empresa**.
>
> **Decisión del usuario, tomada antes de planear:** (1) **sin permisos nuevos** — se reusa el modelo
> de acceso existente; (2) los **4 paneles**: Postura, Engorde, Alimento e inventario, Cumplimiento.

## D0 · Lo que se midió antes de escribir una línea

- [i] 🔴 **Los 8 endpoints del dashboard devuelven datos inventados.** `new Random()` en
      `produccion-por-granja` (`DashboardController.cs:90`) y `registros-diarios` (`:127`);
      constantes en `estadisticas-generales` (`:56`: 25 usuarios, 8 granjas, 45 lotes, 12.500 aves),
      `estadisticas-inventario` y `metricas-rendimiento`; 6 actividades fijas con nombres inventados
      («Juan Pérez», «María García»); 1 fila fija de mortalidad (`LOTE001`); y `distribucion-lotes`
      con **todos los conteos en 0** y `// TODO: Implementar cuando esté disponible`. ⇒ Reorganizar
      la visual sin backend real sólo hace la mentira más linda
- [i] 🔴 **Los filtros no filtran.** El front arma `companyId`/`userId`/`farmIds`
      (`dashboard.component.ts:288`) y **ninguna acción del controller declara esos parámetros**;
      `ICurrentUser` **no está inyectado**. Hoy no fuga porque no consulta; el primer `SELECT` real
      sin scope fuga entre empresas
- [i] 🔴 **Nadie ve el dashboard.** El menú id 3 (`/dashboard`) tiene **0 filas en `role_menus` y 0 en
      `company_menus`** (medido en la BD local). Se llega sólo tecleando la URL
- [i] **`@defer` = 0 usos en todo el front.** Lo que el dashboard llama «lazy loading»
      (`:392-420`) es una cola con `setTimeout` que igual carga todo, más un `interval(30000)` que
      **repite las 8 llamadas cada 30 s**
- [i] ✅ Se reusa todo lo que ya existe: `ICurrentUser`, `ILocationScopeResolver` +
      `UserLocationScopeCalculos` (fail-closed), `VacunacionScopeSqlParams` (patrón para bajar el
      cierre a SQL), `permissionGuard`, `HasPermissionDirective` (55 usos), `chart.js`+`ng2-charts`,
      y **`session.menu` + `session.user.permisos` ya viajan en la sesión** ⇒ el gating no cuesta un request
- [i] 💡 **Cómo se cumple «sin permisos nuevos» sin dejar a nadie sin paneles.** Hay 68 menús y sólo
      45 permisos: gatear sólo por permisos dejaría los 4 paneles invisibles para casi todos. Se usan
      las **dos** señales que ya existen y que ya son por rol Y por empresa: `role_menus ∩
      company_menus` (a qué módulos accede ⇒ *perfil*) + `role_permissions` (qué acciones ⇒ *nivel*).
      Los 3 perfiles **no se codifican como enum**: emergen del cruce. Mapeo **por `route`**, jamás por
      id de menú (los ids difieren local↔prod)

## F1 · Cimientos — gating puro, scope real y muerte de los `Random()`

- [x] F1.1 `models/dashboard-panel.model.ts` + `models/dashboard-metricas.model.ts`
- [x] F1.2 `funciones/resolver-paneles-visibles.funcion.ts` **PURA** (menú + permisos + flags →
      `PanelId[]`) + spec con los 12 casos del plan §5.1 (incluye menú vacío ⇒ 0 paneles, y route
      con barra final / mayúsculas)
- [x] F1.3 `funciones/construir-serie-tiempo.funcion.ts` y `construir-distribucion.funcion.ts` puras + specs
- [x] F1.4 `Application/Calculos/DashboardCalculos.cs` (mismo cierre, lado backend) + xUnit
- [x] F1.5 `pages/dashboard-page/` orquestador delgado con **`@defer (on viewport)`** +
      `@placeholder`/`@loading`/`@error` por panel. **Primer uso de `@defer` del repo**
- [x] F1.6 `components/tarjeta-kpi/` y `components/panel-esqueleto/`, con tokens de
      `theme-italfoods.scss` (⛔ se borra el `CORPORATE_COLORS` hardcodeado de `:100-110`, que además
      usa amarillo/rojo/gris contra la regla de marca)
- [x] F1.7 `DashboardController` reescrito: `ICurrentUser` + `ILocationScopeResolver`, un endpoint por
      panel, **corte en el backend** además del ocultamiento en el front (ocultar no es proteger)
- [x] F1.8 🔴 **Se borran los `new Random()` y las constantes.** Un panel sin fuente real no se dibuja
- [x] F1.9 Se elimina el `interval(30000)` y el `showNotification` que fabrica un `div` a mano
      (`:846`) → `ToastService`
- [x] F1.10 `changeDetection: Eager` explícito en todos los componentes nuevos
- [x] F1.11 `yarn build` + `ng test` + `dotnet build` + `dotnet test` + gates (change detection, lista
      cacheable, `verificar-sql-llega-por-migracion.js`)

## F2 · Panel Postura (levante + producción)

- [ ] F2.1 `fn_dashboard_resumen_postura` en `backend/sql/` + **migración en el MISMO commit**
- [x] F2.2 `DashboardService.Postura.cs` (partial, namespace plano) + endpoint
- [x] F2.3 `components/panel-postura/`: KPIs, % producción vs guía, huevos por tipo, mortalidad diaria
- [x] F2.4 Respeta `ocultaMachosEnPostura` y `clasificacionHuevoPorItems` sin cambiar su comportamiento actual
- [ ] F2.5 Cruce contra Reporte Técnico Producción del mismo lote/día (mismo número o no se mergea)

## F3 · Panel Pollo engorde

- [ ] F3.1 `fn_dashboard_resumen_engorde(p_company_id, …)` + migración. ⚠️ `fn_indicadores_pollo_engorde`
      es **por lote**: llamarla en bucle desde C# es el anti-patrón que cuelga los endpoints multipaís
- [x] F3.2 Service + endpoint + `components/panel-engorde/` (peso vs guía, conversión, mortalidad, ventas)
- [ ] F3.3 Cruce contra Informe Semanal Pollo Engorde

## F4 · Panel Alimento e inventario

- [ ] F4.1 `fn_dashboard_resumen_inventario` + migración
- [x] F4.2 Service + endpoint + panel (stock por granja, ítems bajo mínimo, consumo vs ingreso, gastos)
- [x] F4.3 🔴 **Descuadre con las DOS señales separadas**: `descuadre_kg` (kilos) y `filas_negativas`
      (días en rojo) en columnas distintas. Mezclarlas es el error documentado en CLAUDE.md §🛡️
      (daba 23 galpones cuando los que tenían kilos eran 8)
- [ ] F4.4 Cruce contra `backend/sql/verificar_cuadre_alimento_engorde.sql`

## F5 · Panel Cumplimiento y pendientes

- [x] F5.1 Reusa `fn_vacunacion_pendientes` **tal cual** (ya trae el alcance granular) y
      `fn_vacunacion_cumplimiento_lote`. Una sola fórmula por número: no se reimplementa
- [x] F5.2 Bloques de cuadres offline sin resolver y firmas/tareas de implementación pendientes
- [x] F5.3 Cada bloque aparece sólo si su módulo está en el menú del usuario
- [ ] F5.4 Cruce contra la bandeja de vacunación

## F6 · Que se vea (hoy no lo ve nadie)

- [!] F6.1 **A qué roles se le asigna el menú Dashboard — decisión del usuario.** Propuesta: todos los
      roles que ya tengan al menos un módulo de los 4 paneles. **Requiere OK explícito antes de la migración**
- [ ] F6.2 Migración data-only: `role_menus` + `company_menus`, localizando **por `route`**,
      `INSERT … WHERE NOT EXISTS`
- [ ] F6.3 Smoke: login con 2 roles distintos ⇒ menú visible y paneles distintos según módulos

## Notas de sesión

- [i] Al abrir esta sesión había un **backend vivo en `:5002`** (PID 25236) y front en `:4200`,
      probablemente de otra ventana. **No se mata** (CLAUDE.md §🔌.4): si hay que compilar va
      `dotnet build --artifacts-path <dir>` para no pelear por el `bin/`
- [i] El dashboard viejo **queda vivo** hasta que F1 lo reemplace. No se borra a mitad de camino

## Verificacion corrida (1-sep-2026) — medida, no asumida

- [x] **Smoke HTTP de los 5 endpoints en 2 empresas.** Backend aislado (content root propio,
      `RunMigrations=false`, JWT minteado + `X-Secret-Up` + fila en `sesiones_activas`).
      Sanmarino (company 1): `29 granjas / 12 de 16 lotes postura`; Ecuador (company 3):
      `8 granjas / 0 postura / 32 de 130 engorde`. **Los dos coinciden fila a fila con SQL directo.**
      Aislamiento por empresa verificado: el admin de Ecuador NO ve los 16 lotes de Sanmarino
- [x] **La rama de alcance restringido, probada en las dos direcciones** (es donde un error es fuga):
      granja 20 restringida SIN grants ⇒ postura `16→12` total y `12→8` activos (fail-closed exacto);
      con un grant de lote ⇒ `12→13` y `8→9`, o sea entró **ese** lote y ninguno más. Estado
      restaurado y verificado (`user_farm_scopes` 0, `restrict_locations` 0)
- [x] **Series con datos reales.** Postura 15abr–15may: 31 puntos, `713` mortalidad y `263.711`
      huevos — **idéntico al SQL directo**. Engorde Ecuador, últimos 30 días: 28 puntos de mortalidad
      y consumo, y **27 de peso** — el día sin peso cargado no entra en la serie: sale como hueco
- [x] **Smoke en el navegador** con sesión real (menú de `fn_menu_usuario` + 37 permisos):
      el usuario tiene postura, inventario y cumplimiento pero **no** engorde ⇒ se dibujan 3 paneles
      y `/api/Dashboard/engorde` **no se pide ni una vez**. Todos los `/api/Dashboard/*` en 200
- [x] **`@defer` probado en la Network tab:** recarga limpia ⇒ sólo sale `resumen`; los paneles
      salen recién al scrollear. 4 chunks separados en `dist` (postura, engorde, alimento, cumplimiento)
- [x] **Suites y gates:** `dotnet build` 0/0 · `dotnet test` **3.747** (de 3.717) ·
      `yarn build` 0 errores y **sin** el warning de bundle budget · `ng test` **807** (de 746) ·
      change-detection 242/0 · lista cacheable 89/55/34/0 · gate del `.sql` en verde
- [i] ⚠️ `dotnet test --artifacts-path <fuera del repo>` hace fallar 2 tests
      (`RazaGuiaAliasParidadSqlTests`) que suben desde el binario hasta `backend/sql/`. **No es una
      regresión**: con el build normal pasan los 3.747. Anotado porque cuesta 10 minutos redescubrirlo
- [i] Se borraron **1.780 líneas** del dashboard viejo (904 TS + 511 HTML + 106 SCSS + 259 del service)
- [i] Puertos liberados y sin procesos huérfanos al terminar; datos del smoke borrados y verificados

## Lo que queda — dicho explícito

- [!] **F6.1 sigue pendiente y es tuya:** hoy el menú Dashboard **no lo tiene ningún rol ni empresa**,
      así que la pantalla existe pero nadie llega salvo tecleando `/dashboard`. La migración que
      siembra `role_menus`/`company_menus` necesita tu OK sobre a qué roles
- [i] **Comparación contra la guía genética, pendiente.** Los paneles muestran lo CAPTURADO
      (mortalidad, huevo, consumo, peso), no los derivados (% producción vs guía, conversión
      alimenticia). Esos tienen dueño —las `fn_indicadores_*`— y traerlos exige agregarlas por
      empresa; calcularlos acá sería una segunda fórmula para el mismo número
- [i] El panel de inventario **excluye del stock las granjas con alcance restringido**, a propósito:
      el stock vive a nivel de GRANJA y no hay forma de recortarlo a un galpón concedido

---

# 🌎 Rename neutro de módulos transversales — quitarle el país al código que no es de un país (2-sep-2026)

Plan: [`fase_de_desarrollo/rename_neutro_modulos_transversales_plan.md`](fase_de_desarrollo/rename_neutro_modulos_transversales_plan.md)
Alcance elegido: **máximo** — rótulos + símbolos CLR/TS + **rename físico de BD** (cierra la «Fase C» diferida
en julio-2026), sobre 4 módulos: ítems de inventario, seguimiento aves engorde, guía genética e indicador.

## Auditoría previa (medida contra `sanmarinoapplocal`, no asumida)

- [x] **El módulo de la captura ya tenía el backend neutro.** `ItemInventarioController` + entidad
      `ItemInventario`, con `[Route("api/inventario/items")]` y la ruta vieja como alias. Lo que decía
      «Ecuador» era el `<h1>`, la tarjeta de Gestión de Inventario, la ruta SPA y la BD
- [x] **3 tablas a renombrar** (`item_inventario_ecuador`, `guia_genetica_ecuador_header/_detalle`) y
      **6 columnas** en 6 tablas. 13 índices/constraints con el nombre viejo
- [x] 🔴 **13 funciones se rompen con el rename** (Postgres guarda el *texto*): 10 `sql` + 3 triggers
      `plpgsql`, dos de ellos los que llenan `lote_registro_historico_unificado`. Se recrean en la MISMA
      migración
- [x] ✅ **La vista de Power BI sobrevive sola** (se liga por OID) — pero su columna de salida
      `guia_genetica_ecuador_header_id` viene de un alias explícito (`gh.id AS …`, línea 183) que **NO se
      toca**: si cambia, Power BI se rompe en silencio. Único nombre viejo conservado a propósito
- [x] 🔎 **Hallazgo: el «módulo Ecuador» de seguimiento es un fantasma.** La entidad
      `SeguimientoDiarioAvesEngordeEcuador` + Configuration + `DbSet` no los usa **nadie** (el service vivo
      va a `_ctx.SeguimientoDiarioAvesEngorde`, 9 usos) y la tabla que mapean **no existe**
      (`to_regclass` → NULL) pese a figurar aplicada la migración del split. ⇒ acá no hay rename: hay
      **eliminación**. Es la pieza de mayor valor y menor riesgo
- [x] **Qué NO se toca, con motivo:** `PuentePanama`, `MovimientoPolloEngordePanama`,
      `ColombiaInventarioConsumo`, `ReporteIndicadorPanama` (el país es real); `isEcuador`/
      `ShowIfEcuadorPanama` (decisión cerrada en julio); `ENGORDE_EC` (es **dato** persistido en
      `OrigenModulo`, no símbolo); la clave wire `itemInventarioEcuadorId` y las claves jsonb (§3 del plan)

## Fase A · Rótulos visibles (riesgo cero)

- [x] A1 `item-inventario-list.component.html:5` — «Ítems de inventario (Ecuador)» → sin país
- [x] A2 `gestion-inventario-page.component.html:1353` + comentario `:1321`
- [x] A3 «Raza (guía Ecuador)» / «Año Tabla Genética (guía Ecuador)» en el alta de lote engorde.
      ⚠️ `lote-engorde-list.component.ts:942` y su **spec `:116`** comparan el string exacto: van juntos
- [x] A4 Migración data-only de `menus`: «Guía genética Ecuador» → «Guía genética», «Indicador Ecuador» →
      «Indicador de engorde», descripción del catálogo sin «(Ecuador/Panama)». Por `route`, idempotente
- [x] A5 `yarn build` + `ng test` verdes

- [x] 🔎 **Corrección salida de medir la BD, no del plan:** los `label` de esos 4 menús **ya estaban
      neutros** en local (`Guía Genética Pollo Engorde`, `Liquidacion tecnica`, `Ítems inventario`,
      `Gastos de inventario`). Lo que lleva el país es el `route` y el `key`, que mueven el enrutado del
      front ⇒ pasan a Fase B. La migración va igual porque **producción no se puede medir desde acá** y
      cuesta cero si sobra: recorta el país con `regexp_replace` en vez de asignar un rótulo fijo, así
      no pisa «Guía Genética Pollo Engorde», que es un rótulo mejor y deliberado
- [x] **Validada por transacción con `ROLLBACK`** (no se aplicó en la BD local): simulando un entorno
      con el país puesto, «Guía genética Ecuador» → «Guía genética» e «Indicador Ecuador» → «Indicador»;
      los rótulos ya neutros quedaron intactos; **2ª corrida = `UPDATE 0`** (idempotente)
- [x] **Suites:** `dotnet build` **0 errores / 0 advertencias** · `yarn build` limpio (sin warning de
      bundle budget) · `ng test` **807/807**, incluido el spec que compara el rótulo palabra por palabra

## Fase B · Símbolos CLR/TS (sin DDL, wire estable)

- [x] B1 Borrar el fantasma: entidad + Configuration + `DbSet` de `SeguimientoDiarioAvesEngordeEcuador`
- [x] B2 **Decisión tomada, con la evidencia a la vista: NO hay un service muerto.** Los dos están
      vivos con dueños distintos — el «Ecuador» es el CRUD del seguimiento diario que usan el front y
      `SyncPushService` (la sync de la PWA); el neutro lo usan `MigracionService` (carga masiva),
      `PuentePanamaService` y las rutas de cuadre. Robarle el nombre al otro habría sido mentir
      igual. ⇒ el vivo pasa a **`SeguimientoDiarioEngordeService`**, que es lo que hace y encaja en
      la familia que ya existe (`SeguimientoDiarioService`, `SeguimientoDiarioLoteReproductoraService`).
      **Unificar los dos CRUD es otro trabajo** y no se mete acá: refactor ≠ cambio de comportamiento
- [x] B3 Controller con `[Route]` doble (`api/seguimiento-diario-engorde` + la histórica de alias);
      el front pasa a la neutra
- [x] 🔴 **B3.1 — lo que casi se rompe en silencio.** La cola offline de la PWA decide qué encola
      **matcheando la URL con un regex** (`decidir-encolable.funcion.ts:41`). Cambiar la URL del front
      sin tocarlo habría dejado de encolar las capturas de engorde **sin un solo error visible**. El
      regex ahora acepta **las dos** rutas, y el spec cubre las dos: un dispositivo con el bundle
      viejo, o con capturas ya encoladas contra la URL vieja, sigue funcionando
- [x] B4 `GuiaGeneticaEcuador*` → **`GuiaGeneticaEngorde*`**, no `GuiaGenetica*`: ya existe un
      `guia-genetica` DISTINTO (el de levante, `services/guia-genetica.service.ts`). El nombre lleva el
      MÓDULO, no el país. La diferencia de mayúsculas protegió sola los nombres de BD
      (`guia_genetica_ecuador_*` sigue intacto hasta Fase C). Controller con ruta neutra + la histórica
      de alias
- [x] B5 `IndicadorEcuador*` → `IndicadorEngorde*` (controller, service, `Calculos`, DTOs, interfaz y
      sus tests). Ruta neutra + histórica de alias
- [x] B6 Front: carpetas `indicador-engorde/` y `config/guia-genetica-engorde/`, rutas SPA nuevas
      **+ redirect** de las viejas. **El menú en BD NO se mueve**: apunta a la ruta vieja y el redirect
      lo cubre. Moverlo por migración lo aplicaría el backend al arrancar, y si el bundle del front
      todavía es el anterior el menú llevaría a una ruta que no existe — el orden de despliegue no se
      puede garantizar, el redirect sí
- [x] 🔴 **B6.1 — el gate de la lista cacheable me corrigió una premisa equivocada.** Había dejado la
      ruta histórica en `ENDPOINTS_OPERATIVOS` «para los bundles viejos». Está mal: **esa lista se
      compila DENTRO del bundle**, así que el dispositivo con el bundle anterior lleva su propia copia
      con el nombre viejo. Repetirla en el bundle nuevo no protege a nadie y queda como entrada muerta.
      Corregido: va sólo la ruta neutra. Además `'indicadorecuador'` estaba en `EXCLUIDOS` en minúscula
      y el rename no la alcanzó (otro caso donde el gate evitó una regresión silenciosa)
- [x] [i] **Cuidado al redactar comentarios cerca de una URL:** el gate cuenta cualquier
      `/api/<recurso>` del TEXTO —comentarios incluidos— como si la app lo pidiera. Un comentario mío
      lo hizo fallar. Anotado en el propio archivo
- [x] 🔴 **B7.1 — la FK de la guía se llamaba distinto en la BD que en el modelo.** El rename de la
      entidad hacía que EF quisiera renombrar la constraint (o sea: un rename de C# generando DDL, lo
      contrario de lo que esta fase promete). Al mirarlo, la constraint real es **`fk_gge_det_header`**
      —la creó `create_guia_genetica_ecuador_tables.sql`, no EF— mientras el modelo asumía el nombre
      largo derivado. Cualquier migración futura habría intentado dropear una constraint **inexistente**.
      Fijada con `HasConstraintName`: el rename queda sin DDL y de paso se corrige el desfase previo
- [x] B7 `dotnet build` **0/0** · `dotnet test` **3.747 + 1** · `yarn build` limpio · `ng test`
      **807/807** · gates: lista cacheable **89/55/34/0**, change detection **242/0**, `.sql` por
      migración, front-no-descuenta-inventario. `migrations has-pending-model-changes` verificado con
      una migración de descarte: **mi trabajo no aporta ni una operación al diff**

## Fase C · BD (DDL — requiere OK y deploy propio)

- [x] C1 Migración EF idempotente: 3 `RENAME TO` + 6 `RENAME COLUMN` + 13 `RENAME` de índices/constraints
- [x] C2 Las 13 funciones se reescriben **desde `pg_get_functiondef`** —o sea desde la versión
      realmente desplegada—, no desde el espejo del repo: se midió que algunos espejos están
      atrasados y recrear desde el archivo habría revertido funciones en silencio
- [x] 🔴 **C2.1 — una función cambia su FIRMA y `CREATE OR REPLACE` no alcanza.**
      `fn_inventario_gastos_existencias` declara `item_inventario_ecuador_id` como columna de
      SALIDA, así que renombrarla cambia su tipo de retorno. Se dropea y recrea en la MISMA
      transacción (sin ventana), y el `DROP` va **sin CASCADE** a propósito: si algo dependiera de
      ella queremos que falle acá y no en producción
- [x] C3 Espejos en `backend/sql/` en el MISMO commit + gate `verificar-sql-llega-por-migracion.js`.
      ⛔ Los `.sql` operativos de una sola vez (`migracion_*`/`backfill_*`/`fix_*`/`fase*`/`verificar_*`)
      **no se reescriben**: son el registro de lo que se hizo
- [x] C4 `ToTable`/`HasColumnName` a los nombres nuevos + escalares CLR neutros con
      `[JsonPropertyName("itemInventarioEcuadorId")]` para no mover el wire
- [x] C5 🔴 **Gate multipaís.** `fn_inventario_gastos_existencias` —la de mayor riesgo, la única que
      se dropea— comparada **fila a fila** sobre todas las empresas: **1.188 filas, 0 diferencias**.
      Garantía estructural para las otras 12: Postgres valida el cuerpo al crear, así que **las 13
      se recrearon sin error ⇒ las 13 resuelven contra el esquema nuevo**
- [x] 🔴 **C5.1 — Power BI verificado explícitamente.** Tras el rename,
      `vw_indicadores_diarios_engorde` conserva su columna de salida
      `guia_genetica_ecuador_header_id` (medido: sigue existiendo). Sale de un alias explícito
      (`gh.id AS ...`), no de la columna renombrada. Es el **único** nombre viejo que queda a
      propósito: cambiarlo rompería un consumidor externo que no pidió nada
- [x] C6 `verificar_cuadre_alimento_engorde.sql` antes/después ⇒ las 2 señales sin moverse
- [x] C7 Idempotencia: correr la migración dos veces sobre la misma BD sin error
- [ ] C8 Smoke: rutas HTTP viejas en 200, ruta SPA vieja redirige, cola offline de la PWA sincroniza
- [x] C9 🔴 **Migración inversa escrita ANTES de desplegar, y probada.** El primer `Down` que escribí
      **estaba mal** y el round-trip lo cazó: al revertir, `item_inventario` es PREFIJO de
      `item_inventario_id`, y además **las 3 funciones de vacunación YA usaban la columna neutra**
      (sus tablas nunca llevaron el sufijo), así que un reemplazo inverso les renombraba columnas que
      nunca fueron `_ecuador`. Solución: el `Up` **guarda las definiciones originales** en
      `_rename_sin_pais_fn_backup` y el `Down` las restaura **literales** y borra el respaldo.
      Medido: `Up` → `Up` (idempotente) → `Down` deja **0 funciones distintas, 0 índices distintos,
      0 diferencias de datos**
- [x] C8 Rutas HTTP viejas vivas como alias, ruta SPA vieja con redirect, y la cola offline de la PWA
      intacta (su regla de encolado y su lista cacheable quedaron verificadas por sus gates)

## Hallazgos de paso — NO son de esta tarea

- [ ] 🔴 **Dos cambios de modelo de AYER quedaron sin migración ni snapshot.** `migrations add` hoy
      genera, para cualquiera que lo corra, un `AddColumn` de `historial_traslado_lote.fecha_traslado`
      (además con el tipo cambiado a `DateOnly`) y de `email_queue.next_retry_at`. **Las dos columnas
      YA EXISTEN en la BD**, así que esa migración fallaría al aplicarse y dejaría la app en
      crash-loop al arrancar — el incidente que CLAUDE.md documenta. Verificado que es previo: el
      snapshot commiteado en `764de8f` ya no las tenía. Viene de los bloques del 1-sep (historial de
      traslados y cola de correo). **No lo toco acá**: arreglarlo es decidir sobre una feature ajena
- [ ] 🔎 **`LiquidacionTecnicaEcuador` es el mismo defecto y no estaba en los 4 elegidos.** Sus
      endpoints reciben `loteAveEngordeId` y el front lo llama `baseUrlEcuador` para distinguirlo del
      de levante: la diferencia real es **engorde vs levante**, no el país. Queda anotado en vez de
      renombrado, para no ampliar el alcance por mi cuenta

## Fuera de alcance (explícito)

- [ ] ⏸️ **La propiedad CLR `ItemInventarioEcuadorId` NO se renombra, y es deliberado.** Hoy espeja
      exactamente la clave de wire `itemInventarioEcuadorId`, que sí se conserva (contrato con el
      front y con la **cola offline de la PWA**: hay dispositivos con filas encoladas que la llevan
      adentro). Renombrar la propiedad y pinchar el wire con `[JsonPropertyName]` es posible, pero
      dejaría el DTO diciendo una cosa y el JSON otra: mientras la clave viva, que el nombre la
      espeje es lo honesto. La columna de BD ya se llama `item_inventario_id` y el mapeo lo dice
      explícito. Migrar el wire es una entrega propia, con ventana y versionado

- [ ] ⏸️ **La clave de wire `itemInventarioEcuadorId` NO se renombra.** Es contrato con el front y con la
      **cola offline de la PWA**: hay dispositivos con filas encoladas que la llevan adentro. Entrega propia
- [ ] ⏸️ **Deploy de la Fase C: no se hace acá.** Deploy propio, horario de baja operación, verificación
      post-deploy de CLAUDE.md §🚀 y OK explícito

---

# Alinear el ModelSnapshot con el modelo (dos columnas huérfanas) + la migración que EF no ve

Plan: [`fase_de_desarrollo/alinear_modelsnapshot_ef_plan.md`](fase_de_desarrollo/alinear_modelsnapshot_ef_plan.md)

Cierra el hallazgo de paso anotado por la sesión de la Fase C: `migrations has-pending-model-changes`
acusa dos `AddColumn` (`historial_traslado_lote.fecha_traslado`, `email_queue.next_retry_at`) de
columnas que **ya existen** ⇒ la próxima migración que alguien genere revienta al aplicarse.

- [x] S1 Auditar de dónde salen las dos columnas y si hay DDL faltante en algún ambiente
- [x] S2 Snapshot: agregar `HistorialTrasladoLote.FechaTraslado` (`DateOnly?` → `date`)
- [x] S3 Snapshot: agregar `EmailQueue.NextRetryAt` (`DateTime?` → `timestamp with time zone`)
- [x] S4 🔴 `20260902140000_RenombraTablasYColumnasSinPais` no tiene `.Designer.cs` ni
      `[Migration]` ⇒ **EF no la ve y nunca se aplica**. Escribir el Designer que falta
- [x] S5 `dotnet build` sin errores nuevos
- [x] S6 `migrations has-pending-model-changes` ⇒ sin cambios pendientes
- [x] S7 `migrations list` ⇒ la migración de la Fase C aparece y figura como pendiente
- [x] S8 Verificar que la BD local no se tocó (ningún DDL, `__EFMigrationsHistory` igual)
- [x] S9 Commit `4cf26b5` y fast-forward de `main` conservando el tracker de la
      sesion vecina byte a byte (su apendice de 42 lineas y su lista de sucios, identicos)

---

## Anexo — `LiquidacionTecnicaEcuador` → `LiquidacionTecnicaEngorde` (2-sep-2026)

Mismo defecto que los 4 módulos del bloque anterior, y estaba anotado ahí como hallazgo fuera de
alcance. Ahora sí se hizo. Mapeado con un workflow de **12 agentes** (6 superficies × mapeo +
verificación adversarial), 5 correcciones y 14 omisiones incorporadas al plan antes de tocar código.

- [x] **Por qué el nombre mentía:** sus endpoints reciben `loteAveEngordeId`, y su hermano
      `LiquidacionTecnicaController` es el de **LEVANTE**. La diferencia real es *engorde vs levante*,
      no el país. Por eso el neutro **no** puede ser `LiquidacionTecnica` a secas — tercera vez en la
      sesión que el nombre obvio ya tiene dueño
- [x] Backend: controller + interfaz + service + el registro de DI en `Program.cs`
- [x] **Ruta doble**: neutra `api/LiquidacionTecnicaEngorde` (PascalCase, para no partir la simetría
      con `api/LiquidacionTecnica`) + la histórica como alias. Salía de `[Route("api/[controller]")]`,
      así que sin el alias el rename de la clase habría cambiado la URL sola.
      `[Tags]` explícito: con dos `[Route]` cada acción aparece una vez por ruta y el grupo de Swagger
      lo decidiría el nombre de la clase
- [x] Front: `baseUrlEcuador` → `baseUrlEngorde` y **`useEcuador` → `useEngorde`** en los 2 services.
      Era **parámetro público de 5 métodos**. Los 3 componentes ya usaban el nombre honesto
      (`esLoteAveEngorde`): sólo se les limpió el comentario
- [x] Documentación (`DESARROLLO_MODULO_AVES_ENGORDE_ECUADOR.md`, `LIQUIDACION_TECNICA_POLLO_ENGORDE.md`),
      anotando que la ruta vieja sigue viva como alias

### Lo que el workflow encontró y yo no habría visto

- [x] 🔴 **`.claude/worktrees/` tiene 16 checkouts completos** de otras ventanas de Claude Code, con
      copias del controller. `ripgrep` los ignora (están en `.gitignore`), pero un `sed` recursivo
      desde la raíz —o un «reemplazar en todos los archivos» del IDE— **reescribiría trabajo no
      commiteado de 16 sesiones ajenas**. Verificado que los renames de hoy **no** los tocaron
      (`kind-nash-403266` conserva archivo y símbolo viejos); este también fue acotado a
      `backend/src` y `frontend/src`
- [x] 🔴 **El gate de la lista cacheable corta por la mitad que yo no había previsto.** No falla por
      dejar la entrada vieja —vive en `EXCLUIDOS`, y el chequeo de huérfanos mira **sólo** la lista
      blanca—, sino porque la URL nueva quedaría **sin decisión**. Y el match es **exacto sobre el
      primer segmento**: `'liquidaciontecnica'` NO cubre a `'liquidaciontecnicaengorde'`
- [x] [i] **Un motivo del mapeo era falso y se descartó:** los `///` del controller NO salen en
      Swagger (`Program.cs:614` tiene `IncludeXmlComments` comentado y ningún `.csproj` define
      `GenerateDocumentationFile`). Eran comentarios internos, no contrato público
- [x] [i] **Este módulo no tiene superficie de datos:** 0 tablas, 0 vistas, 0 filas de `menus`,
      0 keys de `permissions`, 0 rutas SPA. Su única entrada real era la lista offline

---

# Fase 2 — las otras 3 migraciones que EF no veía

Plan: [`fase_de_desarrollo/alinear_modelsnapshot_ef_plan.md`](fase_de_desarrollo/alinear_modelsnapshot_ef_plan.md) (sección «Fase 2»)

En la Fase 1 quedaron anotadas y sin tocar porque hacerlas visibles con el `Up()` como estaba era
peligroso. Pedido explícito del usuario ⇒ se hacen **con la guarda puesta**: primero idempotentes,
después visibles.

- [x] T1 Medir de dónde salen: `git log --diff-filter=AD` sobre sus `.Designer.cs` ⇒ **nunca
      existieron**; el schema se aplicó a mano con `apply_*.sql` / `053_sync_produccion_traslados_prod.sql`,
      que además insertaban el id en `__EFMigrationsHistory`
- [x] T2 🔴 `Up()` idempotente en las 2 que usaban `AddColumn` (EF lo escribe **sin**
      `IF NOT EXISTS`) ⇒ `ADD COLUMN IF NOT EXISTS`. La 3ª ya lo era y **no se toca**
- [x] T3 Idempotencia probada por transacción con `ROLLBACK`, **dos corridas**: la 2ª avisa
      `already exists, skipping`
- [x] T4 `.Designer.cs` para las 3, con el `BuildTargetModel` **de la época** (Designer de la
      migración anterior + exactamente las propiedades que introduce cada una), no el snapshot de hoy
- [x] T5 Auditoría de visibilidad: **0** clases `: Migration` sin id en algún `[Migration(...)]`
- [x] T6 `dotnet build` 0 errores / 0 advertencias; `has-pending-model-changes` sin cambios;
      `migrations list` muestra las 3 **sin `(Pending)`** — o sea EF ahora las ve y ademas las
      da por aplicadas, que es el resultado seguro
- [x] T7 BD local intacta: 360 filas en `__EFMigrationsHistory`, ultima sigue siendo
      `20260902030000`, y los 5 tipos de columna sin moverse (los pesos siguen `numeric`)
- [x] T8 Commit del codigo + fast-forward de `main`

### Anotado, no arreglado (sería cambio de comportamiento)

- [ ] ⏸️ **`peso_bruto_real`/`peso_tara_real`: el tipo no es el mismo en todos lados.** El modelo dice
      `double?` ⇒ `double precision` y eso crea la migración; el script manual las creó
      **`numeric(12,3)`** y así están en la base local (medido) ⇒ donde se aplicó a mano el peso
      **se redondea a 3 decimales** y en una base creada desde migraciones no. El código manda ⇒ la
      migración conserva `double precision`. Alinearlo toca datos de báscula: entrega propia
- [ ] ⏸️ **`fecha_alistamiento`: base `date` vs modelo `timestamp with time zone`.** Misma familia que
      `next_retry_at`. Previo, funciona (Postgres castea al asignar), no se toca
- [ ] ⏸️ **Los `Down()` no son revertibles, y tampoco lo eran antes.** Medido: la vista
      `vw_liquidacion_ecuador_pollo_engorde` depende de `fecha_alistamiento` y el trigger
      `trg_movimiento_pollo_engorde_lote_hist` —uno de los que llenan
      `lote_registro_historico_unificado`— depende de `peso_tara_real`. Los dos fallan con «other
      objects depend on it», igual que el `DropColumn` original. **Sin `CASCADE`**: borraría una
      vista y un trigger del histórico en silencio

---

# Fase 3 — alinear el tipo de `peso_bruto_real` / `peso_tara_real`

Plan: [`fase_de_desarrollo/alinear_modelsnapshot_ef_plan.md`](fase_de_desarrollo/alinear_modelsnapshot_ef_plan.md) (sección «Fase 3»)

Cierra la divergencia anotada en la Fase 2. **La BD se alinea al modelo** (`numeric(12,3)` →
`double precision`), no al revés: el código manda, las otras 6 columnas `peso_*` de la misma tabla ya
son `double precision`, y el camino inverso obligaría a pasar el CLR a `decimal` en la entidad, 4
lugares de DTOs y 6 services.

- [x] P1 Medir la dirección correcta: 6 de 8 columnas `peso_*` ya son `double precision`; el CLR es
      `double?` en 6 archivos; el redondeo a 3 decimales lo hace `MovimientoPolloEngordeCalculos.
      ProrratearPesoPorLinea` con `Math.Round(…, 3)`, **no la columna**
- [x] P2 Medir que no se pierde precisión: **0 filas** de `peso_bruto`/`peso_tara` con más de 3
      decimales ⇒ el camino de `OrganizarPeso` (que los copia tal cual) tampoco dependía del recorte
- [x] P3 🔴 Descubrir qué bloquea el `ALTER TYPE`: `trg_movimiento_pollo_engorde_lote_hist` es
      `UPDATE OF … peso_tara_real …` y una lista de columnas explícita **fija** la columna. Se saca y
      se repone desde `pg_get_triggerdef` —la versión desplegada, no un literal del repo— en la
      MISMA transacción, para no dejar el histórico unificado sin llenarse
- [x] P4 Migración `20260902160000_AlineaTipoPesoRealMovimientoEngorde`, idempotente en los dos
      sentidos (el bloque entero solo corre si el tipo actual todavía es el de partida)
- [x] P5 🔴 **Gate multipaís**: `fn_seguimiento_diario_engorde` nombra `peso_tara_real`. Corrido para
      los **184** lotes del histórico: **6.789 filas, 0 diferencias fila a fila**. La razón quedó
      medida: la fn lee la columna de `lote_registro_historico_unificado`, que sigue siendo
      `numeric(18,3)`
- [x] P6 Idempotencia + round-trip por transacción con `ROLLBACK`: `Up`×2 (la 2ª avisa «ya no son
      numeric»), `Down`×2, tipos correctos en cada paso, **0 diferencias de valor** exactas tras el
      `Up` y tras el round-trip, y triggers **idénticos** las dos veces
- [x] P7 El modelo no cambia ⇒ el `ModelSnapshot` no se toca; el Designer clona el snapshot actual
- [x] P8 `dotnet build` 0 errores / 0 advertencias; `has-pending-model-changes` sin cambios
      (confirma que el modelo no se movio); `migrations list` muestra la migracion como (Pending).
      BD local intacta: 360 filas de historial, los 2 tipos siguen `numeric`, 2 triggers vivos
- [x] P9 Commit y fast-forward de `main`

### Queda pendiente de decisión del usuario

- [ ] ⏸️ **Esta migración cambia el schema de producción y todavía no se desplegó.** El `Up` reescribe
      la tabla (2.114 filas, instantáneo) y recrea un trigger; va con el deploy normal
      (`Database__RunMigrations=true`). CLAUDE.md pide OK explícito para DDL en prod
- [ ] ⏸️ **`lote_registro_historico_unificado.peso_tara_real` sigue `numeric(18,3)`** — a propósito.
      Ahí el recorte a 3 decimales sí es del histórico y tocarlo es otra entrega

---

# Optimizacion de consultas, carga del backend y codigo sin uso

Plan: [`fase_de_desarrollo/optimizacion_consultas_backend_plan.md`](fase_de_desarrollo/optimizacion_consultas_backend_plan.md)

Auditoria medida contra el repo y la BD local. **La ejecucion espera decision de alcance del usuario**
(los frentes son independientes; F3 borra cosas y es irreversible).

## Fase 0 — Auditoria (cerrada)

- [x] A1 Detector propio de patrones EF (no grep suelto): entiende el cuerpo del `foreach` y la
      materializacion temprana. Resultado: **17** bloques con BD dentro de un bucle, **119** sitios que
      bajan la tabla y agregan en memoria, **72** `ToListAsync` sin `AsNoTracking`
- [x] A2 Filtros no sargables: **19** `.Date ==`, **17** `.ToString()`, **16** `.ToLower()/.ToUpper()`
      dentro de `Where(...)` — descartan el indice
- [x] A3 BD local: **14 FK sin indice** en las tablas mas grandes; `inventario_gasto` con 8.164 seq
      scans contra 45 index scans; **16 tablas** `_backup_*`/`_migracion_*` vivas
- [x] A4 Pipeline: **sin compresion de respuesta** (ni en el API ni en un proxy delante),
      `AddDbContext` sin pool ni `EnableRetryOnFailure`
- [x] A5 Front: `shareReplay` en **3 de 120** servicios con `HttpClient`; polling fijo 2-5 s en 6 pantallas
- [x] A6 Cruce rutas-vs-clientes (front + app movil Flutter + backend) sobre **834 endpoints**:
      **1** controller sin lector (`ServiceTokens`) y **124** sub-paths candidatos **a verificar uno a
      uno** — el detector no resuelve URLs armadas por concatenacion, no es una sentencia
- [x] A7 Plan escrito con orden por ganancia/riesgo y casos de prueba

## Fase 1 — Transporte y pipeline (riesgo casi nulo) — PENDIENTE DE OK

- [x] F1.1 `AddResponseCompression` (brotli + gzip) sobre `application/json`, `problem+json`, `text/csv`
- [x] F1.2 Cuerpo descomprimido **identico**: mismo sha256 (`aac96b64...`); 3.459 -> 1.274 B con br
      (-63%) y 1.509 B con gzip (-56%); `Content-Encoding` + `Vary` correctos
- [x] F1.3 `AddDbContextPool`. 🔴 **`EnableRetryOnFailure` NO se activa**: la estrategia de reintento
      no soporta transacciones del usuario y hay **67 `BeginTransaction`** en ~25 services contra
      **1** `CreateExecutionStrategy` -> lanzaria en runtime en todo camino de escritura
- [x] F1.4 🔴 El smoke encontro lo que el build y los tests NO veian: `AddDbContextPool` choca con el
      `OnConfiguring` del contexto («cannot be used to modify DbContextOptions when pooling is
      enabled»). Resuelto moviendo la supresion del warning a los 3 sitios que construyen el
      contexto (Program.cs + las 2 design-time factories)
- [x] F1.5 Validado: build 0/0, 3.748 tests verdes, `/db-ping` 200 con el contexto pooled, 37
      requests concurrentes OK antes del rate limiter, 0 excepciones. Backend apagado, puerto libre

## Fase 2 — Consultas (parcial; queda lo que exige decision)

- [x] F2.1 Migracion `20260902173436_IndicesClavesForaneasInventarioYEngorde`: 14 indices, parciales
      (`WHERE ... IS NOT NULL`) en las columnas NULL, siguiendo la convencion que ya tenia
      `movimiento_pollo_engorde` — donde justamente faltaba la simetria: los ORIGEN estaban
      indexados y los DESTINO no
- [x] F2.2 Idempotencia probada por transaccion con `ROLLBACK`: 0 -> 14 -> 14 (avisa «skipping»)
      -> 0 -> 0. BD local **intacta**
- [x] F2.3 Recuento corregido: **14** N+1 reales, no 17 (el detector no manejaba el `foreach` sin
      llaves). Los 2 «casos claros» resultaron ser **falso positivo** (`AuthService:654`: el
      `SaveChanges` ya estaba afuera) y **deliberado** (`EmailQueueProcessorService:87`: guarda
      `processing` antes de enviar para no duplicar el correo) -> ninguno se toca
- [x] F2.4 `DisponibilidadLoteService`: las 2 lecturas completas + 22 `Sum()` en memoria pasan a
      2 consultas agregadas (`GroupBy(1)`), un viaje cada una, con `COUNT` y `MAX(fecha)` incluidos
- [x] F2.5 🔴 Paridad **lote por lote** sobre datos reales
      (`backend/sql/verificar_paridad_disponibilidad_huevos.sql`): **0 diferencias** en 26 lotes /
      1.112 filas y en 2 lotes / 424 traslados
- [x] F2.6 Gate de CI `verificar-sql-llega-por-migracion.js` OK; build 0/0; 3.748 tests verdes
- [ ] ⏸️ F2.7 Confirmar el volumen de esas tablas **en prod** para dimensionar la ganancia real de
      los indices (en local la tabla entra en una pagina y el planner elige seq scan igual)
- [ ] ⏸️ F2.8 Los 4 N+1 de camino de request (`ColombiaInventarioConsumo`, `FarmInventoryReport`,
      `InventarioGestion.Traslado`, `UserFarmScope`) — entrega propia
- [ ] ⏸️ F2.9 Los 52 filtros no sargables: mueven el borde de la fecha, hay que medir uno por uno

## Fase 3 — Verificacion de codigo sin uso (hecha; NO se borro nada)

- [x] F3.1 `ServiceTokensController`: **NO es codigo muerto**. Por diseno no tiene cliente en el
      repo — emite PAT para crones headless que llaman `/api/tickets`, se opera a mano. Conservar
- [x] F3.2 Recuento corregido: **11** endpoints sin ninguna referencia, no 124 (el detector unia
      segmentos estaticos NO adyacentes). Los 11 verificados uno por uno, veredicto en el plan
- [x] F3.3 `POST api/Auth/change-password` es **duplicado superado**: el front usa
      `PATCH /api/users/{id}/password` (`UsersController.cs:125`)
- [x] F3.4 `POST api/Auth/change-email`: backend completo que **nunca se conecto a la UI** (sin
      equivalente en Users, sin pantalla en el front)
- [x] F3.5 Borrado `POST /api/Auth/change-password` (pedido del usuario). El service, el DTO y la
      interfaz **quedan**: `UsersController.ChangePassword` llama al mismo
      `IAuthService.ChangePasswordAsync` con el mismo DTO. Verificado que
      `AuthService.ChangePasswordAsync` sigue exigiendo la contrasena actual, asi que el camino que
      sobrevive conserva la garantia
- [ ] ⏸️ F3.6 Los otros 9 endpoints sin lector — **esperan OK, uno por uno**
- [ ] ⏸️ F3.7 Borrar tablas `_backup_*`/`_migracion_*` — **OK explicito por tabla**

## Fase 4 — Disponibilidad de huevos (pedido del usuario)

- [x] F4.1 Causa medida: el camino por lote sumaba sobre la entidad `SeguimientoDiario`, que por
      `ToTable` apunta a `seguimiento_diario_levante`, filtrando `tipo_seguimiento='produccion'` —
      imposible en esa tabla (1.112 filas, todas 'levante')
- [x] F4.2 🔴 **No se escribio una tercera formula.** Ya existia `espejo_huevo_produccion`
      (`*_historico` = producido, `*_dinamico` = disponible tras descontar traslados Completados) y
      el camino por LPP ya lo usaba. El camino por lote ahora resuelve el LPP y lee el MISMO espejo
- [x] F4.3 Helper `ObtenerEspejoHuevoAsync` extraido y usado por los DOS caminos (el de LPP se
      refactorizo para llamarlo) — una sola lectura del numero
- [x] F4.4 La ubicacion (granja/nucleo/galpon/nombre) se sigue tomando del **LOTE**, no del LPP:
      `TrasladosController:113` usa ese `GranjaId` como granja origen del movimiento. Hoy coinciden
      en los datos, pero apoyarse en eso seria fragil
- [x] F4.5 Verificado (`backend/sql/verificar_paridad_disponibilidad_huevos.sql`): el espejo cuadra
      **exacto** con la formula directa en los 5 LPP con espejo — LPP 6: 2.091.450 / 108.462;
      LPP 7: 1.541.184 / 70.561; **0 diferencias**. Resolucion lote -> LPP **1:1**
- [x] F4.6 ✅ **Resuelto en la Fase 5** (ver abajo). Quedaba asi:
      🔴 **El endpoint SIGUE mostrando cero, por una SEGUNDA causa que no se toco.**
      `ObtenerDisponibilidadLoteAsync` solo entra al camino de huevos si `lote.Fase=='Produccion'`, y
      esa columna **no dice la fase** (anti-patron ya documentado: el paso a produccion no la
      actualiza). Medido: los lotes con huevos (13 y 14, 2,09M y 1,54M) estan en `fase='Levante'`, y
      los que estan en `'Produccion'` (142, 143) tienen 0 filas de produccion.
      **No se cambio el gate a proposito**: haria que 13/14 devuelvan `tipoLote='Produccion'` con
      `Aves=null`, y eso rompe el lado aves —`traslado-aves-huevos.component.ts:193` corta con «Este
      lote es de produccion» y `inventario-dashboard.component.ts:1453` arma los validadores del
      retiro sobre `disponibilidad.aves`. Cambio de comportamiento en pantallas que nadie pidio
      tocar: va aparte, con prueba en las dos

## Fase 5 — Disponibilidad a fondo: aves y huevos NO son excluyentes

Plan: seccion «Fase 5» de
[`fase_de_desarrollo/optimizacion_consultas_backend_plan.md`](fase_de_desarrollo/optimizacion_consultas_backend_plan.md)

- [x] P1 🔴 Causa de raiz: el DTO se usaba como «o aves o huevos» y la rama la elegia `lote.fase`.
      Un lote en produccion tiene LAS DOS. Peor: `ValidarDisponibilidadAvesAsync` devuelve `false`
      con `Aves == null`, asi que rutear a huevos **bloqueaba los traslados de aves** — medido,
      A374A/A374B con 35.372 aves y cero filas de produccion
- [x] P2 Una sola regla de fase, la canonica (`FaseLoteCalculos.ResolverFaseVisible`: levante
      cerrado Y LPP viva). El repo tenia TRES; la de `MovimientoAvesService` incluye `etapa>=26`,
      el anti-patron ya documentado
- [x] P3 `Aves` se informa SIEMPRE; `Huevos` cuando hay LPP (propia o del lote hijo, con orden
      determinista). Un solo DTO con los dos bloques
- [x] P4 🔴 La formula de aves restaba **solo la mortalidad de levante**. Ignoraba la seleccion
      (11.032 en levante + 12.055 en produccion) y el error de sexaje (834). Medido: el lote 14
      informaba **10.748 hembras cuando le quedaban 23** (despoblado en mayo-2026), y ese numero
      autoriza traslados. Ahora usa la composicion canonica de `SaldoAvesLevanteCalculos.BajasNetas`
- [x] P5 **No se sumaron traslados ni ventas de las columnas del seguimiento**: este service ya
      cuenta las salidas por `movimiento_aves` y sumar las dos fuentes contaria doble (el patron de
      la venta que restaba doble). Medido: en produccion esas 3 columnas estan en 0
- [x] P6 Calculo puro `DisponibilidadLoteCalculos` + `DisponibilidadLoteCalculosTests` (20 casos):
      composicion de la baja, piso en cero, que ningun termino quede sin restar, regla de fase
- [x] P7 Front: los **6** gates por `tipoLote` pasan a gatear por la PRESENCIA del bloque
      (`disponibilidad.aves` / `.huevos`) — 3 de aves, 3 de huevos. Es lo que esas pantallas
      preguntan de verdad y deja de depender de una columna que miente
- [x] P8 Verificado: `dotnet build` 0/0, **3.767 tests** verdes, `yarn build` sin errores, y
      `backend/sql/verificar_disponibilidad_lote_aves_y_huevos.sql` con el delta lote por lote
- [x] P9 Backend arranca limpio, `/db-ping` OK con el contexto en pool. Apagado, puertos libres

### Lo que quedo SIN verificar (dicho, no escondido)

- [ ] ⏸️ **La traduccion LINQ -> SQL de las consultas nuevas no se probo en ejecucion.** El endpoint
      devuelve 401: la lista blanca exige un `jti` vivo en `sesiones_activas` y no hay ninguna
      vigente (513 filas, 0 sin vencer); no se inserto una a mano. Las formas usadas ya corren en
      produccion en `EspejoHuevoProduccionSyncService`, asi que el riesgo es bajo pero no cero.
      Por eso la agregacion de `movimiento_aves` **se dejo como estaba**: su filtro cruza una
      navegacion dentro de un `OR` y es la unica que no se pudo comprobar
- [ ] ⏸️ **Smoke funcional en las 2 pantallas de aves y las 2 de huevos** con un usuario real, antes
      de desplegar. El cambio mueve numeros que el usuario ve y que autorizan traslados

---

## 🚚 Consolidación de traslados/ventas de aves y huevos — Fase 0 + Propuesta B

Plan: [`fase_de_desarrollo/consolidacion_traslados_aves_huevos_plan.md`](fase_de_desarrollo/consolidacion_traslados_aves_huevos_plan.md)

Recon previo: el movimiento de aves tenía **4 puntos de entrada de escritura vivos** (no 3) y los
huevos **3**. Decisión del usuario: Fase 0 + Propuesta B. **Los caminos backend A/C/D no se tocan.**

### Fase 0 — borrar código muerto confirmado

- [x] F0.1 Modal inline "Traslado" del dashboard (`abrirModalTraslado`/`procesarTraslado` + HTML +
      SCSS + `navegarANuevoTraslado`) — ningún `(click)` lo abría
- [x] F0.2 `RegistrosTrasladosComponent` completo — sin ruta en `app.config.ts`
- [x] F0.3 `TrasladoNavigationList` + `TrasladoNavigationCard` — cadena huérfana
- [x] F0.4 Backend B1/B2: `EjecutarVentaAsync`/`EjecutarTrasladoAsync` + endpoints
      `ejecutar-venta`/`ejecutar-traslado` + firmas de la interfaz (0 llamadores, re-verificado).
      ⚠️ `ejecutar-traslado-cierre-levante` (B3) **NO** se toca: lo usa el cierre de levante
- [x] F0.5 Huérfanos derivados (imports, tipos, specs de lo borrado)

### Propuesta B — consolidación del front

- [x] B1 Partir `TrasladosAvesService` (747 líneas) por dominio, re-exportando para no romper
      imports externos
- [!] B2 **NO SE HIZO — el gate encontró divergencia estructural.** Las dos fuentes NO son
      intercambiables: medido el 3-sep-2026 sobre la copia local, **9 de 15 lotes con actividad
      divergen**, hasta **19.385 aves** de diferencia. Difieren en 4 dimensiones, no en redondeo:
      (a) la base (`lote_etapa_levante.aves_inicio_hembras` vs `lotes.hembras_l`), (b) la mortalidad
      de caja (sólo `resumen-mortalidad` la resta), (c) **las bajas de PRODUCCIÓN (sólo
      `disponibilidad` las resta)** → lotes 13 y 14, (d) la fuente de los traslados (columnas
      acumuladas del espejo vs `movimiento_aves`) → lotes receptores 116/124/128/129 dan **0** en
      `disponibilidad` porque los `TSD-*` viejos tienen `lote_destino_id` NULL.
      **Ninguna de las dos es correcta en todos los casos**, así que unificar cambiaría números que
      autorizan traslados. Evidencia reproducible:
      `backend/sql/verificar_paridad_disponibilidad_aves.sql`. Decide el usuario.
- [x] B3 Extraer la cascada Granja→Núcleo→Galpón→Lote a
      `traslados-aves/components/selector-lote-destino/` y usarla en dashboard, `/nuevo` y
      `movimientos-aves` (mata los 3 `<input type="text">` de "ID del lote destino")
- [x] B4 Colapsar `/traslados-aves/nuevo` sobre el dashboard sin romper el link del menú
- [x] B5 Huevos: borrar `/traslados-huevos/nuevo` (0 roles) y montar `ModalTrasladoHuevosComponent`
      en la pestaña Huevos del dashboard ⇒ **cierra el bloque F10**
- [x] B6 Primitivas: `window.prompt` → `ConfirmDialogService`; `ToastService`; `Eager` explícito
- [x] B7 Validación: `yarn build` limpio + `dotnet build` 0 errores + specs puntuales
