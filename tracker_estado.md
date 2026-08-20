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

- [ ] **A1 · Push + merge + verificación post-deploy de los 17 commits.** `git push origin main`,
      merge a `main-produccion` (dispara el deploy; las migraciones se aplican solas con
      `Database__RunMigrations=true`) y la **verificación obligatoria** de CLAUDE.md §🚀:
      TaskDef ↔ imagen ↔ `/version.json`. ECS revierte en silencio y el CLI igual dice «completado».
      Arrastra `20260820055219_SeedGalponesModuloIvNizaIii` (X1) y `AddSesionesActivas` (V39).
      **No lo ejecuté yo**: es push + deploy, y las dos acciones piden pedido explícito
      (ver [[como-trabaja-el-usuario]]) — confirmado que sigue 100 % listo (`dotnet build` 0 errores
      hoy en un worktree aislado; TaskDef viva = `sanmarino-back-task:160` = imagen `79aeccf`, sin
      ninguno de los 17)
- [ ] **A2 · V39.13 — cerrar la ventana de gracia** de los tokens sin `jti`: hoy `Evaluar` devuelve
      `Legado` y los acepta. Va **después** de A1 y de verificar la revocación en prod.
      ⚠️ Trampa: `SesionActivaService` devuelve `Legado` **también ante un fallo de BD** (fail-open
      deliberado) — borrar esa rama sin distinguir los dos usos convierte una caída de BD en un
      **logout masivo**. Commit propio y explícito.
      *Releído el código hoy (`SesionActivaService.EvaluarAsync`): el diagnóstico sigue exacto —
      `jti` vacío ⇒ retorna sin consultar nada, rama `Legado` intacta*
- [ ] **A4 · V30.7 · H1 Santa Reyes** — flags en `companies` + catálogo de ítems + silo en el form de
      ingreso a granja + homologación ERP + seed de las 5 guías genéticas (540 filas)
- [ ] **A5 · V30.8 · H2** — semanas por raza (hoy hardcodeadas en
      `modal-seguimiento-diario.component.ts:1463`), consumo sólo hembras, ocultar machos y error de
      sexaje **en UI** (⚠️ no borrar del modelo: lo consumen los saldos), tipos de inventario
- [ ] **A6 · V30.9 · H3** — huevos: incubables→sin clasificar, los 7 ítems, primera postura por raza
      con vigencia ≤ semana 22, PNC por catálogo (⚠️ sin tocar las 11 columnas físicas)
- [ ] **A7 · V30.10 · H4** — traslados: aves (exponer `Placa`/`Conductor`/`Sellos` en postura — **ya
      existen en `MovimientoAves`**, falta la UI) y huevos (bodega destino desplegable) + no regresión
      multipaís

> **A4-A7 están bloqueadas por B5** (aprobación del cliente). No arrancan antes. **A3 se retiró de
> esta lista** — bajó de categoría, ver el hallazgo nuevo en «Muertos» más abajo.

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
- [!] **B5 · Santa Reyes — aprobación del cliente** (V30.5) del alcance, el cronograma y los supuestos
      (§13 del Word). **Vencida**: el cronograma arrancaba el 19-ago y cada día de demora corre la
      entrega del 1-sep en la misma medida
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
- [~] **C7 · Santa Reyes debe entregar** la estructura física real (núcleos, galpones, silos, bodegas) y
      los códigos ERP (CO, bodegas, ubicaciones, centros de costo). Vencido el 18-ago; **F1.2 corre el
      día 1** y el plan no tiene holgura. Es el riesgo Alto #1 del documento
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

- [i] V30.5 (→ **B5**) **Aprobación del cliente** del alcance, el cronograma y los supuestos (§13 del Word).
      Nada arranca antes de esto
- [i] V30.6 (→ **C7**) Santa Reyes debe entregar, **a más tardar el mar 18-ago-2026 (un día antes del
      inicio)**, la estructura física real (núcleos, galpones, silos, bodegas) y los códigos ERP
      (CO, bodegas, ubicaciones, centros de costo). ⚠️ En el plan de 2 semanas **F1.2 corre el
      día 1**: no hay holgura para esperarlos. Es el riesgo **Alto** #1 del documento
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
