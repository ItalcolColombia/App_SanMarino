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
- [ ] **F1 — calculo puro a `Application/Calculos/`**: `ItemConsumoCalculos`, `ConsumoDiffCalculos`,
      `MetadataItemSeguimientoCalculos`, `FechaMovimientoSeguimientoCalculos`. Refactor puro, cero
      cambio de comportamiento, solo agrega archivos. Hoy ese bucle esta inline TRES veces y ninguna
      es testeable
- [ ] **F5.2 — la UI del selector de items en Flutter**: repos distintos, cero archivos compartidos
      con el backend. Es el trabajo de mayor plazo: conviene arrancarlo temprano y shippearlo tarde

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
- [ ] F7 (`requiere_cuadre`) sigue sin arrancar

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
- [x] **`SyncService` con tests** (23ago26) — `test/sync_service_test.dart`, 31 casos: los 6
      `TipoFallo`, la guarda de reentrada, el orden de la cola (I4), el endpoint congelado (I5) y las
      filas agotadas (I17). Validados **con mutación**: se rompió una regla por vez y las 9 las
      detectó el test que nombra su invariante. Se agregó una costura de tiempos al service
      (`demoraDeteccion`/`demoraExito`, defaults idénticos) para que la suite no espere 3,9 s por caso.
