# Corrección de los bugs anotados durante la sesión EC (25-ago-2026)

> Pedido del usuario: *«corrige todos los bugs anotados completos»*. Este plan recoge **todo lo que
> quedó anotado como defecto** a lo largo de la sesión —los que venían del tracker EC3 y los que
> aparecieron al verificar el liquidador— y los lleva a estado cerrado o a un *no aplicar* razonado.

Tracker: bloque **EC5** · Plan hermano: [`plazo_validacion_desde_creacion_plan.md`](plazo_validacion_desde_creacion_plan.md)

---

## 1. Inventario: qué se anotó y de dónde salió

| # | Defecto | Dónde se anotó | Muerde hoy |
|---|---|---|---|
| 1 | El push offline de la PWA (`Sync/SyncPushService.cs`) es el único escritor de seguimientos que no usa `ModoCargaHistorica` ⇒ un día capturado offline y sincronizado >24 h después nace `EN_RETRASO` | tracker EC3.3 | Sí, cuando se use offline |
| 2 | `ValidacionSeguimientoService.MarcarValidadoAsync` escribe `Confirmado` (dispara el cruce) pero **nunca llama** a `RetiroAvesEngordeAplicador.SincronizarCruceAsync` | tracker EC3.3 | No — el front usa el endpoint específico |
| 3 | Permiso huérfano `movimientos_pollo_engorde.vender_lotes_cerrados`: lo lee **sólo el front**, el backend gatea por `omitirGateLiquidado`. El usuario con el permiso habilita el formulario y el guardado le rebota | §9.8 del plan hermano | Sí, a quien tenga el permiso |
| 4 | `LeerPendientesDelLoteAsync` (rama Engorde) filtra por lote **sin `company_id`**, a diferencia de `LeerEstadoAsync` | tracker EC3, observación | Latente — **muerde al encender el flag en Ecuador** |
| 5 | **Liquidar sin la venta registrada borra el encasetamiento**: la fn reescribe `aves_iniciales` como `bajas + ventas` y el cierre **congela** esa foto. Nada lo impide | §9.8 del plan hermano | Prospectivo: **610.704 aves** en 17 lotes de Panamá |
| 6 | `fn_cuadre_aves_engorde` no mira `estado_operativo_lote` ⇒ el detector que debería avisar del #5 devuelve `cuadra = true` | §9.8 del plan hermano | Latente |
| 7 | `vw_seguimiento_pollo_engorde` (Power BI): en lote cerrado **Total ≠ H + M** en la misma fila | §9.8 del plan hermano | Latente |

### Lo que NO entra en este plan (y por qué)

- **La regla de huecos y el plazo desde `created_at`** — es una **feature**, no un bug, y el usuario la
  reservó explícitamente para otra sesión. El diseño ya está escrito en §9 del plan hermano.
- **La guarda sin fecha (§9.1)** — no es un defecto de hoy: es un **prerrequisito** de esa feature.
  Hoy la guarda bloquea por vencidos y la fecha no participa de la decisión.
- **La reapertura no decrementa el código ERP** — está documentado como intencional en el código
  (`LoteAveEngordeService.cs:905`) y en la memoria del proyecto.
- **La ejecución del barrido de Panamá (EC2)** — bloqueada por deploy + una decisión de operación,
  no por código.

---

## 2. Alcance medido (por qué el orden de severidad es ése)

Medido el 25-ago-2026 sobre la copia de producción:

- **ItalcolPanama tiene CERO lotes liquidados.** El daño de #5 es **100 % prospectivo** — todavía no
  se perdió ni un ave. Por eso la guarda llega a tiempo y es la prioridad.
- **ItalcolEcuador tiene 97 liquidados**, de los cuales **8 perdieron más de 100 aves** (3.368 en
  total). Es el caso de control: cuando la venta está registrada, `bajas + ventas == encasetadas` y
  liquidar cierra el saldo en 0 limpio, sin perder nada.
- Eso vuelve a **#6 y #7 latentes**: hoy casi no tienen víctimas. Se evalúan con la vara de
  *«¿el riesgo de tocar un objeto que consume Power BI supera al beneficio?»*.

---

## 3. Reglas que gobiernan estos parches

- **Refactor ≠ cambio de comportamiento.** Un lote de Ecuador con la venta registrada tiene que
  liquidar **exactamente igual que antes**. No negociable.
- **Lógica pura a `Application/Calculos/` + xUnit**, con el caso *«camino previo ⇒ idéntico»* siempre
  presente. Los tests son gate de CI.
- **Todo objeto de BD llega por migración**; el `.sql` es el espejo y viaja en el mismo commit.
- **Gate multipaís** si se toca cálculo compartido de alimento.
- **Sin procesos huérfanos**: el backend se levanta sólo para la verificación final y se apaga.

---

## 4. Veredicto y parches

Especificación adversarial: 7 specs + 4 refutaciones. **Las refutaciones mataron 3 de los 7 parches y
corrigieron 2 de los que quedaron.** Cuando spec y refutación se contradijeron, ganó la que citaba
código; los dos hallazgos decisivos se verificaron a mano antes de aplicar nada.

| # | Defecto | Veredicto | Detalle |
|---|---|---|---|
| 1 | Push offline PWA sin `ModoCargaHistorica` | ⏸️ **NO APLICAR — decisión del usuario** | §4.1 |
| 2 | `MarcarValidadoAsync` no sincroniza el cruce | ✅ **APLICADO** (con un error de la spec corregido) | §4.2 |
| 3 | Permiso `vender_lotes_cerrados` huérfano | ✅ **APLICADO**, en la dirección opuesta a la del enunciado | §4.3 |
| 4 | `LeerPendientesDelLoteAsync` sin `company_id` | ✅ **APLICADO** en las 4 ramas | §4.4 |
| 5 | Liquidar borra el encasetamiento | ⚠️ **APLICADO SOLO EL AVISO** — la guarda se descartó | §4.5 |
| 6 | `fn_cuadre_aves_engorde` ciega al cierre | ✅ **RESUELTO SIN DDL** | §4.6 |
| 7 | Vista Power BI: Total ≠ H + M | ⏸️ **NO APLICAR en este ciclo** | §4.7 |

### 4.1 · #1 — Queda a decisión del usuario, y no es un detalle

El defecto existe. Pero el parche **apaga `ValidarAlimentoObligatorio` sin decirlo**: ese guard corre
sólo dentro de `if (separa)` y su doc-comment (`SeparacionSeguimientoHelper.cs:25-26`) nombra
explícitamente *«el push de la PWA»* como el cliente que lo necesita. Días de campo viejos entrarían
**sin alimento, en silencio**, en Panamá.

Y compite con el plan vigente: `plazo_validacion_desde_creacion_plan.md:94` dice que los tres parches
(`ModoCargaHistorica`, el cruce, el push offline) **dejan de hacer falta** con el cambio de bloqueo.

> **Dos rutas, y hay que elegir una:**
> **(A)** parchear la carga histórica por operación, aceptando perder el guard de alimento en las
> capturas atrasadas. **(B)** hacer el paso 1 del plan hermano —el bloqueo pasa a ser «el registro
> anterior tiene que estar confirmado»—, que resuelve éste y los otros dos casos sin efectos
> colaterales. **Recomendada: (B).**

### 4.2 · #2 — Aplicado, y la spec traía un error que no truena

`ValidarAsync`/`DesvalidarAsync` ahora sincronizan el cruce con el maestro de aves. Tres precisiones
que salieron de verificar el código en vez de creerle a la spec:

1. 🔴 **La spec pasaba el id equivocado.** Para reproductora, `LeerEstadoAsync` devuelve en
   `LoteRefInt` el id del **lote de reproductora**, y `SincronizarCruceAsync` espera el de **engorde**.
   Pasarlo directo no lanza ninguna excepción: no encuentra el lote y no hace nada —o sincroniza el
   lote de engorde que por casualidad tenga ese id—. El helper resuelve el puente primero, igual que
   `SeguimientoDiarioLoteReproductoraService.SincronizarBajasCruceAsync`.
2. **El bug es independiente del flag**: `ValidarAsync` no consulta `RequiereValidacionAsync` en
   ninguna parte, así que aplica a todas las empresas, no sólo a la que tiene la doble validación.
3. **Idempotencia verificada** (`RetiroAvesEngordeAplicador.cs:160-176`, filtro `yaAplicados`): la
   llamada de más no duplica descuentos. Era el riesgo que habría hecho el arreglo peor que el bug.

La dirección de des-validar es la que más importaba: quitar `confirmado` re-dispara el cruce, que
**borra** los días 1-7, y sin sincronizar, las filas del histórico quedan apuntando a seguimientos que
ya no existen.

### 4.3 · #3 — Es un bug, pero al revés de como estaba enunciado

No es que al backend le falte honrar el permiso: **es que el permiso no puede existir para un lote
cerrado**. El backend rechaza toda escritura sobre un lote liquidado
(`LiquidacionCongeladaGateCalculos.ValidarEscritura` → 400) y mientras está cerrado los reportes leen
la copia congelada, así que una venta que entrara quedaría invisible. El camino real es **reabrir**.

Se hizo honesta la promesa: `bypassablePorPermiso` separa las dos causas — el permiso destraba la
**corrida anterior** (que el backend acepta, porque no tiene noción de «corrida vigente») y **no** el
lote cerrado. El hint al usuario y el mensaje de error lo dicen ahora. De paso, el predicado
`line.bloqueada && !puedeVenderLotesCerrados`, repetido **7 veces** en el HTML, pasó a una sola fuente
(`lineaBloqueadaEfectiva`).

### 4.4 · #4 — Fuga real, no sólo latente

`ObtenerPendientesAsync` **no valida** que el lote sea de la empresa activa, y
`LeerPendientesDelLoteAsync` buscaba sólo por id. Un usuario podía pedir los pendientes de un lote
ajeno y recibir sus fechas. Es exactamente el defecto que el propio repo ya había arreglado en
`ValidarAsync`/`DesvalidarAsync` (ver el doc-comment de `EsDeLaEmpresaActiva`).

Las 4 ramas quedan acotadas, fail-closed. Producción se filtra **por fila** (es la única tabla de
seguimiento que lleva `company_id`); las otras tres resuelven la empresa del lote. Que un lote
irresoluble devuelva vacío no afloja el bloqueo: `AsegurarPuedeRegistrarDiaAsync` ya corta antes por
`RequiereValidacionAsync`, que también depende de la empresa activa.

### 4.5 · #5 — La guarda se descartó, y con razón

**Corrección a lo que se había reportado en EC4.2**: liquidar **no** pierde aves «en silencio». El
modal ya tiene un banner `role="alert"` con la cifra exacta, y `puedeLiquidarPorAves` devuelve `true`
**a propósito**, con el motivo escrito: *«se permite liquidar aunque haya aves registradas (datos
pueden tener error)»*. Es un override informado y deliberado, no un descuido.

La guarda propuesta se descartó por cuatro razones, todas verificadas:
- **Reinstala un bloqueo que el repo degradó a aviso a propósito**, sin argumentar contra esa razón.
- **No evita el daño: pide consentimiento para él.** Si el usuario tilda, la foto truncada se congela
  exactamente igual que hoy.
- Introduce **una segunda fórmula** para el mismo número que ya calcula
  `LiquidacionCongeladaAplicador.CalcularResumenVivoAsync`.
- **Acusa mal.** Con tolerancia 0 bloquearía lotes que el negocio considera sanos (su propia auditoría
  alerta sobre **1 %**), y el hueco puede venir de un `Despacho`/`Retiro` (ver §5).

Lo que sí se aplicó: **el banner ahora dice qué pasa si continuás** —que el encasetamiento pasa a
valer `bajas + ventas` y esa foto queda congelada— en vez de sólo avisar que hay aves. Y dice
«registrá la **venta**», no «venta o despacho», porque un despacho no alimenta ese número (§5).

### 4.6 · #6 — Resuelto sin tocar la fn

`fn_cuadre_aves_engorde` **no tiene un solo consumidor en runtime** (cero `SqlQueryRaw`/`FromSql` en
todo el backend). Agregarle columnas no crea una alarma: crea una consulta que alguien tendría que
escribir igual, a cambio de `DROP FUNCTION` + cambio de firma + migración + Designer clonado.

Se resolvió con [`backend/sql/verificar_salidas_aves_engorde.sql`](../backend/sql/verificar_salidas_aves_engorde.sql),
de solo lectura: lo ya perdido, lo que se perdería al liquidar, y la trampa latente de §5.

### 4.7 · #7 — No en este ciclo

Cuatro razones para no tocar la vista ahora: el consumidor es **externo** (Power BI) y no pidió el
cambio; el `Down` propuesto **no es round-trip seguro** (un re-`Up` quedaría en no-op silencioso con
la vista rota); el arreglo **falla abierto** en un lote poblado por traslado (`aves_encasetadas = 0`,
caso legítimo y documentado); y sobre todo **apagaría la única señal visible en Power BI de un lote
liquidado sin su venta**, justo antes de que Panamá liquide.

---

## 5. El hallazgo que vale más que varios de los parches

> 🔴 **Hay cinco definiciones distintas de «salida de aves» en el mismo módulo.**

- El trigger `trg_lote_hist_desde_movimiento_pollo_engorde` emite `VENTA_AVES` **sólo** si
  `tipo_movimiento = 'Venta'` (`create_lote_registro_historico_unificado.sql`).
- `MovimientoPolloEngordeService.EsSalidaVenta` cuenta `Venta | Despacho | Retiro`.
- `fn_indicadores_pollo_engorde` cuenta además `Traslado`.

Consecuencia: un **Despacho descuenta aves del maestro pero no alimenta `total_ventas`**, así que al
liquidar esas aves también desaparecen — y cualquier guarda construida sobre `total_ventas` acusaría
mal al usuario.

**Medido el 25-ago-2026: hoy no tiene víctimas.** El sistema entero tiene sólo movimientos `Venta`
(1.455 completados, 469 anulados) y el histórico tiene exactamente **1.455** filas `VENTA_AVES`:
calzan uno a uno. La trampa se arma el día que alguien registre el primer `Despacho`. Queda medida en
el chequeo 3 del `.sql` nuevo.

---

## 6. Deuda registrada, sin parche

- **`PermisoDesvalidar` no tiene caso `Reproductora`** (`ValidacionSeguimientoService.cs:49-54`): cae
  al `_ => "seguimiento_engorde.desvalidar"`. Agregar el permiso propio obliga a un seed y dejaría
  afuera a quien hoy puede des-validar.
- **`fn_cuadre_aves_engorde` es ciega a `mixtas`**: un lote 100 % mixto da `cuadra = true` pase lo que
  pase. Inocuo en Panamá (las mixtas viven en H/M), no en una empresa que llene `mixtas` de verdad.
- **La vista de Power BI está congelada en la era v7 de la fn**: su `aves_iniciales` no resta
  `mort_caja`, que `fn_seguimiento_diario_engorde` resta desde v8 ⇒ `saldo_aves_vivas` **ya** discrepa
  de `saldo_aves`, y el `EXCEPT` de paridad de `20260624165752` hoy no da 0.
- **El permiso `vender_lotes_cerrados` queda como permiso de UI sin contraparte en el servidor.**
  Quien lo lea en el futuro como autorización de negocio se va a equivocar igual que el enunciado
  original del #3.
