# v16 de engorde — la atribución del alimento marcado, PERSISTIDA como hecho

**Fecha:** 2026-08-18 · **HEAD:** `56f7caa` · **Autor del plan:** sesión de planificación (no escribe código)
**Continúa:** [`marca_proximo_ciclo_rediseno_plan.md`](marca_proximo_ciclo_rediseno_plan.md) (el modelo de ENTREGA)
y el bloque del tracker *«v16 de engorde — FASE 1 IMPLEMENTADA»*, que terminó en **NO-GO / REVERTIDA**.
**Cierra los 3 pendientes** que ese bloque dejó abiertos en «Lo que queda para el rediseño».

> ⚠️ **STEP 2 pendiente:** quien implemente esto agrega su bloque **AL FINAL** de `tracker_estado.md`,
> separado por `---`, sin tocar bloques ajenos (CLAUDE.md §⚙️). Esta sesión **no** escribió el tracker.

---

## 0. Estado de partida — VERIFICADO CONTRA EL CÓDIGO DE HOY

> **Regla de oro (CLAUDE.md §🔍): el código actual manda.** El bloque del tracker describe una
> implementación que **no existe en el repo**. Todo lo que sigue está medido, no citado.

### 0.1 Lo que el tracker declara hecho y NO está

| Afirmación del tracker | Verificación de hoy | Veredicto |
|---|---|---|
| **F1.6** migraciones `20260809120000_FnAlimentoMarcadoAtribucionEngorde` y `20260809120100_FnSeguimientoEngordeV16EntregaCicloSiguiente` | `backend/src/ZooSanMarino.Infrastructure/Migrations/` salta de `20260808130000_FnSeguimientoEngordeV15…` a `20260810002504_AddStockClaveNaturalUnica`. **No existen** | ❌ FALSO (ya lo señaló V12.5.1) |
| **F1.1** `backend/sql/fn_alimento_marcado_atribucion.sql` (543 líneas) | no está en `backend/sql/`; en la BD local `pg_proc` devuelve **0** para `fn_alimento_marcado_atribucion` y `fn_alimento_base_cedente_engorde` | ❌ FALSO |
| **F1.3** `Application/Calculos/AtribucionAlimentoMarcadoCalculos.cs` + espejos C# «revertidos a v14» | el archivo no existe. `SaldoAlimentoEngordeCalculos.EntraPorMarcaProximoCiclo` (**:215-234**) y `ExcluidoDeFilaDiariaPorMarca` (**:251-252**) siguen en **v15**, sin revertir | ❌ FALSO |
| **F1.4** `SaldoAlimentoEngordeAplicador.RecalcularVecinosSiHayAlimentoMarcadoAsync` | `grep -r` en todo `backend/`: **0 hits**. El aplicador sólo tiene `RecalcularPorLoteAsync` (:43) y `RecalcularPorUbicacionAsync` (:80) | ❌ FALSO |
| `backend/sql/verificar_marca_proximo_ciclo.sql` (566 líneas, «el gate ejecutable») | no existe | ❌ FALSO |
| índice parcial `ix_lote_hist_para_proximo_ciclo` («no se tocó, otra sesión lo estaba creando») | `pg_indexes` local: **0 filas** | ❌ NO EXISTE |
| **Fase 2a HECHA** — «la columna *Próx. ciclo* está pintada en el tab Histórico» | el `<th>Próx. ciclo</th>` está en `inventario-historial-page.component.html:327` = pantalla **Historial → Ingresos**, que el plan original ya daba por existente (§4.3, «el Historial → Ingresos sí lo pinta»). El **tab Histórico de `gestion-inventario-page`** (15 `<th>`, `.html:1215-1231`) **no** lo tiene, y su DTO `InventarioGestionMovimientoDto` (`InventarioGestionDtos.cs:181`, campo en **:214**) **sí** lo trae | ⚠️ **F2a.1 SIGUE PENDIENTE** — se confundieron dos pantallas |
| **Fase 3 (R2) cerrada en V16** | cierto: `AnomaliaAlimentoLiquidadoCalculos.cs`, `GET /liquidados-con-alimento`, tab «Cuadre alimento», tests T1-T8 | ✅ CIERTO |

### 0.2 «El predicado de R1 ya existe en el archivo» — medio cierto

El tracker dice: *«Arreglar los 4 guards… El predicado ya existe en el archivo: es el de `lotes_ajenos`
(v11) aplicado al destino en vez de a mí»*. Contra el SQL real
(`backend/sql/fn_seguimiento_diario_engorde.sql`, **1.027 líneas, v15**):

- **Los 4 guards existen y son literales**, idénticos entre sí:
  `AND (rs.fecha_min IS NULL OR NOT COALESCE(h.para_proximo_ciclo, FALSE))` en
  **:615** (`hist_full`), **:761** (`hist_alimento`), **:790** (`docs_por_fecha`), **:826** (`fechas_universo`).
- **El predicado de convivencia sí existe, dos veces**: como `NOT EXISTS` en `lotes_ajenos` (**:427-443**,
  el complemento) y como `EXISTS` positivo en `consumo_galpon_por_fecha` (**:470-478**).
- **Pero se aplica a `h.lote_ave_engorde_id`** (la etiqueta que el movimiento trae del inventario),
  **no a un destino: la fn NO tiene hoy el concepto de «lote destino»** en ninguna línea. El único
  parecido es la guarda de `apert_mov` (**:525-537**), que pregunta si *otro ciclo se interpuso*, no
  quién recibe.

⇒ La **forma** del predicado existe; el **sujeto** al que aplicarlo, no. Y bajo el modelo de este plan
no hace falta ninguno de los dos: **los 4 guards se BORRAN** (§4, R1).

### 0.3 Dos hechos que nadie escribió y cambian el diseño

**(a) La mitigación del checkbox es SOLO de front.** `mostrarParaProximoCicloIngreso` devuelve `false`
(`gestion-inventario-page.component.ts:1083`) y `puedeMarcarDestinoCiclo` exige que la marca ya esté
puesta (`inventario-historial-page.component.ts:282`). **La API no tiene ninguna guarda**:
`InventarioGestionIngresoRequest.ParaProximoCiclo` (`InventarioGestionDtos.cs:147`) se persiste tal cual
en `RegistrarIngresoAsync` (**:612**) y en el camino Colombia (**:1757**), y
`ActualizarDestinoCicloIngresoAsync` (**:2742**) acepta `true` sin condiciones. Swagger, la PWA, la carga
masiva o un script pueden reintroducir hoy mismo el defecto vivo de v15 — el que rompe la conservación
en **729 de 2.210 casos reales, hasta 37.467 kg**.

**(b) El espejo C# de la marca es inalcanzable en producción.** `EntraPorMarcaProximoCiclo` y
`ExcluidoDeFilaDiariaPorMarca` sólo se evalúan si el llamador pasa `ciclosDelGalpon`
(`SeguimientoAvesEngordeCalculos.cs:82, :98-107, :144, :162-171, :201, :227-229`). `grep` de
`ciclosDelGalpon` fuera de `Calculos/`: **0 hits en `backend/src`** — sólo lo pasan los xUnit. O sea: **la
fn aplica la marca y el espejo C# no.** La divergencia SQL↔C# que CLAUDE.md prohíbe ya existe, hoy.

### 0.4 Topología medida (esto es lo que gobierna el gate)

Consulta de solo lectura sobre la BD local (`127.0.0.1:5433/sanmarinoapplocal`, credenciales de
`backend/src/ZooSanMarino.API/appsettings.Development.json`):

| Empresa | Lotes | Galpones | Pares **secuenciales** | Pares con **hueco** | Pares que **conviven** |
|---|---|---|---|---|---|
| **ItalcolEcuador** | 121 | 36 | **143** | **142** (7.190 días de hueco) | **0** |
| **ItalcolPanama** | 65 | 40 | **0** | 0 | **7** |

> 🔑 **Los dos pendientes viven en países distintos.** `DIFERIDO` (entregar al ciclo siguiente) sólo
> puede ocurrir en **Ecuador**; **R1 / convivencia sólo existe en Panamá**. El gate multipaís no es
> ceremonia: es el único instrumento que ve las dos mitades a la vez. El incidente de julio fue
> exactamente el error simétrico (se midió en Panamá y rompió Ecuador).

### 0.5 Líneas base del día (re-medir antes de tocar nada)

| Medida | Valor HOY | Nota |
|---|---|---|
| `fn_seguimiento_diario_engorde` instalada en local | **v15** | igual que el `.sql` del repo |
| Marcas `para_proximo_ciclo` | **0** en `lote_registro_historico_unificado` y **0** en `inventario_gestion_movimiento` | el rediseño sigue siendo libre |
| `fn_cuadre_alimento_engorde(NULL)` | **66 filas · 5 descuadrados** (todos Panamá) | ⚠️ los bloques viejos dicen «61 / 1»: **está caduco** (V16.6.1, V17.0.1) |
| Cuadre por empresa (V16.5, smoke real) | Ecuador **36 galpones / 0 descuadrados** · Panamá **30 / 5**, 19 con días en negativo | |
| Liquidaciones congeladas vigentes | **90**, de ellas **28 con `saldo_alimento_kg > 0`** | 2 son anomalía viva (V16.5.1) |
| Última migración aplicada en local | `20260818042406_SuperAdminPorDato` | |
| Lotes con seguimiento | Ecuador 118 · Panamá 37 | universo del gate |

---

## 1. Enfoque arquitectónico y por qué

### 1.1 La causa raíz, en una frase

El veredicto del round 4 lo dice sin rodeos: *«la atribución es un veredicto **recalculado en lectura**
sobre estado mutable, pero la liquidación congela **un solo** extremo ⇒ el handoff se parte»*. Los dos
bloqueantes son consecuencias de eso:

- **Liquidar el CEDENTE escondía kilos**: la re-lectura flipeaba a `NEUTRO_CEDENTE_LIQUIDADO`, la
  apertura del destino caía de 3.000 → 0, el cuadre de 0,00 → −3.000, y la foto congelada del cedente
  seguía diciendo «Entrega al ciclo siguiente, salida 3.000». **3.000 kg reales sin tabla diaria viva.**
- **Liquidar el DESTINO los duplicaba**: Σ galpón 8.640 → 11.640 (**+3.000 kg creados**) con
  `descuadre_kg = 0,00` en los dos estados ⇒ **el detector era ciego**.

### 1.2 El cambio de modelo

> ### PRINCIPIO RECTOR — LA ATRIBUCIÓN ES UN HECHO, NO UN VEREDICTO
>
> La atribución (cedente, destino, kg, fecha de entrega) se **decide y se ESCRIBE una vez**, en el
> momento de marcar (o de materializar). La fn diaria no la deriva: la **lee**. Congelar un extremo
> deja de poder cambiar lo que ve el otro, porque no queda nada que recalcular.

Se conserva **entero** el modelo de ENTREGA del plan anterior (§2.3): la fila de ingreso nunca se borra;
el cedente emite una **salida sintética** en su último día visible; el destino la recibe en su
**apertura**; los kg están **topados** por el saldo real del cedente. Lo único que cambia —y es lo que
convierte 4 rondas de guardas en una entrega estable— es **dónde vive el veredicto**.

### 1.3 Tres fases, en este orden y por este motivo

| Fase | Qué hace | Por qué va sola |
|---|---|---|
| **A — desarmar** | La marca vuelve a ser **inerte** en la fn (v16a = v14 + las columnas de apertura de v15) + guarda de servidor que impide poner marcas nuevas | Cierra un defecto **VIVO** (§0.3a) sin modelo nuevo. Su gate es *demostrable*: con 0 marcas la salida es byte a byte idéntica en las dos empresas |
| **B — persistir** | Tabla del hecho + escritor C# puro + la fn v16b que la lee + recálculo de ambos extremos | Es el rediseño. Entra sobre una base v14 limpia, no sobre v15 |
| **C — ver** | Columna en el tab Histórico **real**, bandeja de reservados, mensaje del endpoint con el estado resuelto | R3 operativo. Ahora es barato: la bandeja es un `SELECT` sobre el hecho |

**Fase A no es opcional ni cosmética.** Sin ella, cualquier trabajo posterior se mide contra una fn que
ya rompe la conservación cuando hay marcas, y el A/B deja de ser legible. Además arregla la divergencia
SQL↔C# de §0.3b **borrando** el espejo muerto en vez de completarlo.

### 1.4 Trade-offs explícitos

**Por qué una tabla nueva y no columnas en el histórico**

- La entrega es **el único dato de alimento con alcance de LOTE**. Todo el resto de la fn
  (`hist_full`, `hist_alimento`, `docs_por_fecha`, `fechas_universo`) filtra por **ubicación**
  (granja+núcleo+galpón) y por diseño no conoce lotes. Meter la entrega ahí obliga a introducir un
  predicado por lote en cuatro CTE que nunca lo tuvieron.
- El hecho necesita **ciclo de vida propio** (`PENDIENTE → VIGENTE → ANULADA`, más `sellada`) y
  auditoría propia. `lote_registro_historico_unificado` lo llena un trigger **AFTER INSERT** que no
  propaga ningún `UPDATE` (invariante de CLAUDE.md), y el endpoint ya arrastra un **fallback frágil** de
  búsqueda del espejo (`InventarioGestionService.cs:2747-2759`, «con dos ingresos idénticos puede marcar
  el otro»). Colgarse de la clave real `origen_tabla + origen_id` una sola vez es más barato que
  repetir ese fallback.

**Alternativas descartadas (para que no vuelvan a proponerse)**

| Alternativa | Por qué NO |
|---|---|
| Materializar la entrega como par **real** `INV_TRASLADO_SALIDA` + `INV_TRASLADO_ENTRADA` (que es lo que la operación hace físicamente, D2b) | La fn lee alimento con **alcance galpón**: una salida y una entrada en el **mismo** galpón se cancelan exactamente. La entrega sería **invisible** para `hist_alimento`, y además tocaría `inventario_gestion_stock`, que hoy no distingue ciclos |
| Columnas `lote_destino_id / kg_diferido / fecha_entrega` en `lote_registro_historico_unificado` + espejo | Viable y más barata, pero mezcla «qué pasó en el inventario» con «cómo lo atribuimos», hereda el fallback frágil y no tiene dónde poner `sellada`/`anulada_motivo` sin ensuciar una tabla de auditoría de 6 índices |
| Otra guarda más en la fn (la 5.ª) | Es lo que fracasó 4 veces. Cada guarda **mudó el defecto de lugar** |
| Tocar `fn_cuadre_alimento_engorde` | Fue **exactamente** el error de la ronda 2 (+5.000 kg permanentes en 33/35 galpones). El cuadre tiene que seguir siendo el detector **independiente**: si forma parte del fix, deja de poder validarlo |

### 1.5 Una sola fórmula por número — quién es dueño y quién es test

| Número | **Dueño** | **Test / espejo** |
|---|---|---|
| Tabla diaria: `saldo_alimento_kg`, apertura, universo de filas | **`fn_seguimiento_diario_engorde` (SQL)** | `SeguimientoAvesEngordeCalculos` como *especificación ejecutable* (ya declarado en `SeguimientoAvesEngordeService.SaldoAlimento.cs:14-16`) + el gate de paridad |
| `saldo_alimento_kg` **persistido** en `seguimiento_diario_aves_engorde` | la misma fn, vía `SaldoAlimentoEngordeAplicador` (**escribe desde la fn**, `:43-56`) | `IS DISTINCT FROM` ⇒ idempotente |
| **La atribución** (cedente, destino, `fecha_entrega`, `kg_entregados`, estado) | **C# puro: `EntregaAlimentoCicloEngordeCalculos`**, evaluado **una vez** al escribir el hecho | xUnit + la fila persistida. **La fn NO la re-deriva: la LEE** |
| El **tope** (kg entregables ≤ saldo del cedente a `fecha_entrega`) | la **fn** (es su saldo) — el escritor la **consulta** y **congela** el resultado en la fila | test de integración del escritor |
| El cuadre por galpón | `fn_cuadre_alimento_engorde` — **no se toca** | invariante independiente del fix |

La inversión respecto de la v16 anterior es la clave: antes **SQL era dueño de la atribución** y la
recalculaba en cada lectura; ahora **SQL es lector** y el dueño escribe una sola vez.

### 1.6 Las dos reglas que cierran el partido del handoff

1. **Simetría por construcción.** La entrega es **una** fila leída por **dos** lotes: el cedente la ve
   como salida, el destino como crédito de apertura. Ninguno de los dos puede dejar de honrarla
   unilateralmente ⇒ los bloqueantes 1 y 2 son **inconstruibles**.
2. **Sellado.** Una entrega `VIGENTE` cuyo cedente **o** destino tenga liquidación congelada vigente
   (`liquidacion_lote_engorde_congelada.anulada_at IS NULL`) queda **`sellada = true`: inmutable**. No se
   anula, no cambia de kg, no se re-materializa. Sin esta regla, anular el hecho después de congelar un
   extremo reabre exactamente el agujero del round 4.

---

## 2. Archivos a crear o modificar (rutas verificadas)

### 2.1 FASE A — la marca vuelve a ser inerte

| Archivo | Cambio |
|---|---|
| `backend/sql/fn_seguimiento_diario_engorde.sql` | **v16a.** Quitar los **5** lugares donde v15 interpreta el booleano: el disyunto marcado de `apert_mov` (**:512-551**, colapsando `:525-537` y el `NOT …` de `:541`) y los 4 guards de **:615 / :761 / :790 / :826**. **Se conservan** `apertura_alimento_kg` y `apertura_documentos` (parte A de v15: ortogonal, ya en prod y sin relación con la marca). Cabecera nueva con el changelog v16a |
| `backend/src/ZooSanMarino.Infrastructure/Migrations/<ts>_FnSeguimientoEngordeV16aMarcaInerte.cs` + `.Fn.cs` + `.Designer.cs` | NUEVA. Mismo patrón de partición que `20260808130000_FnSeguimientoEngordeV15AperturaVisibleYMarcaCiclo.{cs,Fn.cs,Designer.cs}`, con el SQL **byte a byte** igual al `.sql`. **La firma NO cambia ⇒ `CREATE OR REPLACE`**, sin `DROP FUNCTION`. `Down()` repone v15 VERBATIM. Designer clonado del último real; **ModelSnapshot intacto** |
| `backend/src/ZooSanMarino.Application/Calculos/SaldoAlimentoEngordeCalculos.cs` | **Borrar** `EntraPorMarcaProximoCiclo` (**:193-234**) y `ExcluidoDeFilaDiariaPorMarca` (**:236-252**): código muerto que además hoy **miente** respecto de la fn (§0.3b) |
| `backend/src/ZooSanMarino.Application/Calculos/SeguimientoAvesEngordeCalculos.cs` | **Borrar el parámetro opcional `ciclosDelGalpon`** de las 3 firmas (**:82**, **:144**, **:201**) y sus usos (**:98-107**, **:162-171**, **:227-229**). Ningún llamador de producción lo pasa. Aritmética del camino v14 **intacta** |
| `backend/tests/ZooSanMarino.Application.Tests/AperturaAlimentoEngordeV15CalculosTests.cs` | Reescribir los casos de la marca (**:158-160, :209-231, :333-335**) como *«la marca no cambia nada»*. Los de `apertura_alimento_kg`/`apertura_documentos` **se conservan** |
| `backend/src/ZooSanMarino.Infrastructure/Services/InventarioGestionService.cs` | **Guarda de servidor** (cierra §0.3a): mientras la feature esté apagada, `RegistrarIngresoAsync` (**:612**), el camino Colombia (**:1757**) y `ActualizarDestinoCicloIngresoAsync` (**:2742**) rechazan `ParaProximoCiclo = true` con `400` y mensaje explicativo. **Quitar** una marca existente sigue permitido (R3: nunca dejar kilos sin corregir). El aviso de `:639` vuelve a evaluarse siempre |

### 2.2 FASE B — el hecho persistido

**Dominio / persistencia**

| Archivo | Cambio |
|---|---|
| `backend/src/ZooSanMarino.Domain/Entities/AlimentoEntregaCicloEngorde.cs` | NUEVO (§3.2) |
| `backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/AlimentoEntregaCicloEngordeConfiguration.cs` | NUEVO — `ToTable("alimento_entrega_ciclo_engorde")`, índices, CHECKs |
| `backend/src/ZooSanMarino.Infrastructure/Persistence/ZooSanMarinoContext.cs` | `DbSet<AlimentoEntregaCicloEngorde>` |

**Cálculo puro (el dueño de la atribución)**

| Archivo | Cambio |
|---|---|
| `backend/src/ZooSanMarino.Application/Calculos/EntregaAlimentoCicloEngordeCalculos.cs` | NUEVO, `static`, sin EF. `ResolverCedente`, `ResolverDestino`, `Conviven`, `ClasificarEstado`, `FechaEntrega`, `TopeEntrega`, `PuedeAnular`, `Describir`. Cubre los **11 casos** de §3 del plan original + los 3 estados que el gate anterior descubrió |
| `backend/tests/ZooSanMarino.Application.Tests/EntregaAlimentoCicloEngordeCalculosTests.cs` | NUEVO. Helper que construye **un galpón completo** (ciclos con encaset, primer/último seguimiento, congelación, ventana). Si el helper no puede expresar «destino sin seguimiento» o «cedente sin respaldo», **se arregla el helper primero** (G3) |

**Servicio (partial, patrón CLAUDE.md §🧩)**

| Archivo | Cambio |
|---|---|
| `…/Infrastructure/Services/AlimentoEntregaCicloEngorde/AlimentoEntregaCicloEngordeService.cs` | NUEVO — **ancla**: usings, campos, ctor, la interfaz `: IAlimentoEntregaCicloEngordeService` |
| `…/AlimentoEntregaCicloEngorde/Funciones/…Service.Materializar.cs` | NUEVO — resuelve extremos, **consulta el tope a la fn**, escribe/actualiza el hecho, recalcula **ambos** extremos |
| `…/AlimentoEntregaCicloEngorde/Funciones/…Service.Consulta.cs` | NUEVO — bandeja de reservados (filtro por empresa efectiva, **fail-closed**) |
| `…/AlimentoEntregaCicloEngorde/Funciones/…Service.Anulacion.cs` | NUEVO — anulación con la guarda de **sellado** |
| `backend/src/ZooSanMarino.Application/Interfaces/IAlimentoEntregaCicloEngordeService.cs` · `…/DTOs/AlimentoEntregaCicloDtos.cs` | NUEVOS |
| `backend/src/ZooSanMarino.API/Controllers/AlimentoEntregaCicloEngordeController.cs` | NUEVO — `GET /api/AlimentoEntregaCicloEngorde` (bandeja), `POST /{id}/anular`. ⚠️ **nada de rutas con «admin»** (el WAF devuelve 403 a cualquier path que la contenga) |

**Integración con lo existente**

| Archivo | Cambio |
|---|---|
| `…/Services/SaldoAlimentoEngordeAplicador.cs` | Agregar `RecalcularAmbosExtremosAsync(ctx, cedenteId, destinoId, …)` = dos `RecalcularPorLoteAsync`. No cambia la aritmética: sigue escribiendo **desde la fn** |
| `…/Services/SeguimientoAvesEngorde/Funciones/SeguimientoAvesEngordeService.SaldoAlimento.cs` (**:43**) | **Cruce de umbral (carga masiva)**: tras recalcular su lote, si el galpón tiene entregas `PENDIENTE`, invocar el materializador de la ubicación |
| `…/Services/SeguimientoAvesEngordeEcuador/Funciones/SeguimientoAvesEngordeEcuadorService.SaldoAlimento.cs` (**:184**) | Idem (formulario diario Ecuador). Son los **dos** caminos que escriben seguimiento de engorde |
| `…/Services/InventarioGestionService.cs` (**:612**, **:1757**, **:2713-2769**) | Levantar la guarda de Fase A y delegar en el materializador. El endpoint devuelve el **estado resuelto** («se difiere al lote X», «queda reservado: el ciclo destino todavía no existe») en vez de un texto fijo. `RefrescarSaldoAlimentoEngordeAsync` (**:2766**) se conserva |
| `…/Services/LoteAveEngordeService.cs` (**:648**, `CerrarLoteAsync`) | Antes de congelar: **sellar** las entregas vigentes del lote (cedente o destino). No mueve ningún número; sólo las vuelve inmutables |
| `…/Services/LiquidacionCongeladaAplicador.cs` (**:100**) | Verificar que el sellado ocurra **antes** de tomar la foto (el saldo del último día se lee de la columna persistida) |
| `backend/sql/fn_seguimiento_diario_engorde.sql` | **v16b** (§3.3) |
| `backend/sql/create_alimento_entrega_ciclo_engorde.sql` | NUEVO — espejo del DDL + triggers, convención de `backend/sql/` |
| `backend/sql/verificar_entrega_ciclo_engorde.sql` | NUEVO, **line endings LF** (`psql.exe` duplica el CR) — el gate G1 (§5.2) |
| `backend/sql/fn_cuadre_alimento_engorde.sql` | **NO SE TOCA** |

### 2.3 FASE C — visibilidad (R3)

| Archivo | Cambio |
|---|---|
| `frontend/src/app/features/gestion-inventario/pages/gestion-inventario-page/gestion-inventario-page.component.html` (**:1215-1231**) + `.ts` | **F2a.1 real**: columna «Próx. ciclo» en el tab **Histórico**. El dato ya viaja en `InventarioGestionMovimientoDto` (`InventarioGestionDtos.cs:214`) |
| `…/inventario-historial-page/inventario-historial-page.component.{html,ts}` (**:327, :359-369** / **:282**) | El badge pasa a mostrar el **estado del hecho** (Reservado / Entregado al lote X / Inerte por convivencia), no sólo el booleano. `puedeMarcarDestinoCiclo` vuelve a habilitar el alta cuando Fase B esté GO |
| `frontend/src/app/features/gestion-inventario/…/alimento-reservado/` (NUEVO) | Bandeja: lista por empresa/granja con estado y motivo, corrección en línea. **`changeDetection: ChangeDetectionStrategy.Eager` explícito** (CLAUDE.md: omitirlo en v22 = OnPush = modal colgado en «Cargando…»). `ToastService` + `ConfirmDialogService`; export por `shared/utils/excel/exportar-tabla-excel.funcion.ts` |
| `frontend/src/app/features/gestion-inventario/services/gestion-inventario.service.ts` (**:656**) | Método de la bandeja + tipos |

---

## 3. Cambios de BD / SQL / migraciones

### 3.1 Orden y contenido de las migraciones (todas **idempotentes**)

| # | Migración | Contenido | Idempotencia |
|---|---|---|---|
| 1 | `<ts>_FnSeguimientoEngordeV16aMarcaInerte` | `CREATE OR REPLACE FUNCTION fn_seguimiento_diario_engorde` (firma igual) | `CREATE OR REPLACE` |
| 2 | `<ts>_RecalcularSaldoAlimentoEngordeV16a` | Realinea `seguimiento_diario_aves_engorde.saldo_alimento_kg` con la fn | Molde **exacto** de `20260818010000_RecalcularSaldoAlimentoEngordePersistido`: backup `CREATE TABLE IF NOT EXISTS` + `INSERT … WHERE NOT EXISTS` + `UPDATE … IS DISTINCT FROM` |
| 3 | `<ts>_AddAlimentoEntregaCicloEngorde` | `CREATE TABLE IF NOT EXISTS` + índices + el índice parcial `ix_lote_hist_para_proximo_ciclo` (**hoy no existe**) | `IF NOT EXISTS` en todo |
| 4 | `<ts>_TriggersAnulacionEntregaCicloEngorde` | Propagación de anulación desde el movimiento origen | `CREATE OR REPLACE FUNCTION` + `DROP TRIGGER IF EXISTS` + `CREATE TRIGGER` |
| 5 | `<ts>_FnSeguimientoEngordeV16bEntregaPersistida` | La fn lee la tabla del hecho (firma igual) | `CREATE OR REPLACE` |
| 6 | `<ts>_RecalcularSaldoAlimentoEngordeV16b` | Igual que la #2, después de v16b | idem |

> ⚠️ **Por qué la #2 va sí o sí, aunque «con 0 marcas nada cambia».** Desde esta máquina **no se puede
> consultar prod** (RDS en VPC privada, ECS Exec deshabilitado — P.3 del tracker), así que **no se puede
> afirmar** que prod tenga 0 marcas. La migración de recálculo es idempotente y con 0 marcas mueve 0
> filas: cuesta nada y elimina la única forma en que la #1 podría dejar la columna persistida
> desalineada. Precedente directo: la fn cambió dos veces sin recálculo y dejó **109 filas / 36 lotes de
> Panamá** divergentes, **6 de ellos en el último día** — el que la liquidación congela para siempre.

### 3.2 La tabla del hecho

```sql
CREATE TABLE IF NOT EXISTS alimento_entrega_ciclo_engorde (
    id                 BIGSERIAL PRIMARY KEY,
    company_id         INT           NOT NULL,
    farm_id            INT           NOT NULL,
    nucleo_id          TEXT          NOT NULL DEFAULT '',
    galpon_id          TEXT          NOT NULL,
    -- el movimiento del que nace (clave REAL, la misma de uq_lote_hist_origen)
    origen_tabla       TEXT          NOT NULL,
    origen_id          BIGINT        NOT NULL,
    hist_id            BIGINT        NULL,
    fecha_movimiento   DATE          NOT NULL,
    kg_movimiento      NUMERIC(18,3) NOT NULL,
    numero_documento   TEXT          NULL,
    -- el HECHO
    lote_cedente_id    INT           NULL,
    lote_destino_id    INT           NULL,
    fecha_entrega      DATE          NULL,
    kg_entregados      NUMERIC(18,3) NOT NULL DEFAULT 0,
    kg_no_diferible    NUMERIC(18,3) NOT NULL DEFAULT 0,   -- residuo = anomalía R2
    estado             TEXT          NOT NULL,             -- PENDIENTE|VIGENTE|INERTE|ANULADA
    motivo             TEXT          NULL,                 -- texto para la UI
    sellada            BOOLEAN       NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(), created_by_user_id TEXT,
    anulada_at TIMESTAMPTZ NULL, anulada_por_user_id TEXT, anulada_motivo TEXT,
    CONSTRAINT ck_entrega_kg_solo_vigente
        CHECK (kg_entregados = 0 OR (estado = 'VIGENTE' AND lote_cedente_id IS NOT NULL
               AND lote_destino_id IS NOT NULL AND fecha_entrega IS NOT NULL)),
    CONSTRAINT ck_entrega_kg_no_negativos CHECK (kg_entregados >= 0 AND kg_no_diferible >= 0)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_entrega_ciclo_origen
    ON alimento_entrega_ciclo_engorde (origen_tabla, origen_id) WHERE estado <> 'ANULADA';
CREATE INDEX IF NOT EXISTS ix_entrega_ciclo_ubicacion
    ON alimento_entrega_ciclo_engorde (farm_id, nucleo_id, galpon_id, fecha_movimiento);
CREATE INDEX IF NOT EXISTS ix_entrega_ciclo_cedente
    ON alimento_entrega_ciclo_engorde (lote_cedente_id) WHERE estado = 'VIGENTE';
CREATE INDEX IF NOT EXISTS ix_entrega_ciclo_destino
    ON alimento_entrega_ciclo_engorde (lote_destino_id) WHERE estado = 'VIGENTE';

-- el que faltaba desde el intento anterior (verificado: hoy NO existe)
CREATE INDEX IF NOT EXISTS ix_lote_hist_para_proximo_ciclo
    ON lote_registro_historico_unificado (farm_id, nucleo_id, galpon_id, fecha_operacion)
    WHERE para_proximo_ciclo;
```

**Estados** (colapsan los 17 del intento anterior a 4 persistidos + `motivo` legible):

| estado | significado | efecto en la fn |
|---|---|---|
| `PENDIENTE` | hay intención, todavía no hay destino operativo (casos 3, 4, 9 de §3 del plan original) | **ninguno** = idéntico a HEAD. Aparece en la bandeja (R3) |
| `VIGENTE` | handoff escrito: cedente, destino, fecha, kg topados | salida en el cedente + crédito en la apertura del destino |
| `INERTE` | la marca no aplica: convivencia (R1), ya visible en el destino, `d` dentro del cedente/destino, salida, anulado | **ninguno** |
| `ANULADA` | el hecho se deshizo (por el usuario o porque el movimiento origen se anuló) | **ninguno**; la fila **queda** con `anulada_motivo` |

### 3.3 La fn v16b — qué se agrega exactamente

Sobre la base **v16a (= v14 + columnas de apertura)**:

1. **CTE `entrega_cedente`** — `SELECT fecha_entrega, SUM(kg_entregados)` de las entregas `VIGENTE` con
   `lote_cedente_id = p_lote_id`. Se fusiona como `traslado_salida_kg` en `hist_alimento` (**:733**) y
   como delta negativo en `hist_full` (**:587**), aporta documento a `docs_por_fecha` (**:766**) y fecha a
   `fechas_universo` (**:800**).
2. **CTE `entrega_destino`** — `SUM(kg_entregados)` de las entregas `VIGENTE` con
   `lote_destino_id = p_lote_id`. Suma a la apertura.
3. 🔴 **La asimetría obligatoria (defecto #1 del gate anterior, 37 probes rotos, hasta 14.320 kg).**
   `saldo_running` (**:633-643**) → `saldo_close` (**:647**) → `rango_final.fecha_max` (**:660-671**) debe
   seguir usando la **apertura BASE** (`apertura_alimento`, **:559-572**), **sin** el crédito recibido. Si el
   crédito entra ahí, el ciclo destino cierra más tarde, **amplía su ventana visible** y absorbe
   movimientos ajenos. Sólo `pt_calc` y la columna expuesta `apertura_alimento_kg` usan la apertura
   **efectiva** (base + crédito). Es la misma razón por la que la entrega del cedente **no** puede entrar
   en la serie que fija su propia `fecha_entrega`.
4. **Una sola base en `pt_calc`.** La entrega es un delta más, exactamente como un
   `INV_TRASLADO_SALIDA` real. **Prohibido** un segundo piso/serie (fue el defecto de la ronda 3:
   6 de 59 galpones con saldo negativo).
5. **La rama CONGELADA (**:304-327**) no se toca.** Un lote liquidado conserva su foto.
6. La firma **no cambia** ⇒ `CREATE OR REPLACE`, sin `DROP FUNCTION`.

### 3.4 Anulación — el histórico se ANULA, nunca se abandona

`lote_registro_historico_unificado` la llena un trigger **AFTER INSERT**; ningún `UPDATE`/`DELETE` del
origen se propaga solo. La tabla nueva es una tabla espejo más ⇒ **replica el patrón** que ya garantizan
`trg_inventario_gestion_movimiento_lote_hist_del` y `_cancel` (verificados en la BD local):

- `AFTER UPDATE` sobre `inventario_gestion_movimiento` cuando pasa a anulado ⇒ la entrega pasa a
  `ANULADA` con `anulada_motivo = 'movimiento origen anulado'`.
- `AFTER DELETE` ⇒ idem. **Nunca `DELETE` sobre la entrega.**
- Si la entrega está **`sellada`**, el trigger igual la anula (no se puede impedir desde un trigger sin
  romper el inventario) **pero deja `anulada_motivo` explícito**, y el caso aparece como anomalía en
  `GET /liquidados-con-alimento`. Ver §6, riesgo 1.

---

## 4. Reglas de negocio — R1, R2 y R3 tal como están definidas

Citadas textualmente de `marca_proximo_ciclo_rediseno_plan.md` §1 (las definió el dueño del producto el
08-ago-2026; son la **especificación**, no sugerencias).

### R1 — CICLOS QUE CONVIVEN

> *«Si dos ciclos conviven en el mismo galpón, el alimento marcado pertenece **A LOS DOS**. La marca no
> tiene que desempatar entre ciclos que conviven: comparten bodega, exactamente como ya se comportan hoy
> los movimientos SIN marcar (predicado CONVIVEN de v10 / `lotes_ajenos` de v11). La marca solo decide
> entre ciclos **SECUENCIALES** (los que no se solapan).»*
> **Decisión D1:** la marca es **NO-OP** cuando el destino **convive** con el cedente.

**Cómo se cumple acá — y por qué NO se agrega ningún predicado a la fn.** El plan original ya lo dice:
lo que rompe R1 son **4 líneas de v15**. Bajo el modelo del hecho:

- Los 4 guards (**:615, :761, :790, :826**) **se borran** en Fase A. La fila de ingreso vuelve a verse en
  **todo** lote con seguimiento — conviva o no — igual que en v14.
- La decisión de convivencia se toma **una vez, en el escritor C#** (`Conviven` en
  `EntregaAlimentoCicloEngordeCalculos`): si conviven ⇒ `estado = INERTE`, `kg_entregados = 0` ⇒ la fn no
  hace absolutamente nada. R1 pasa a ser **propiedad estructural**, no una guarda que hay que recordar.
- Sobre la afirmación del tracker (§0.2): el predicado de convivencia **existe** (`lotes_ajenos` :427-443)
  pero se aplica a la etiqueta del movimiento, no a un destino, y **la fn no conoce destinos**. Si algún
  día hiciera falta un guard, con el hecho persistido sería trivial
  (`NOT EXISTS (SELECT 1 FROM lotes_ajenos la WHERE la.id = e.lote_destino_id)`) — pero **D1 + D3 lo
  vuelven innecesario**: la fila del cedente nunca se quita.
- **Superficie de riesgo = Panamá** (7 pares que conviven, 0 secuenciales). Es lo que mide P16 y el
  `EXCEPT ALL` del gate.

### R2 — LIQUIDACIÓN

> *«Al liquidar un lote el galpón tiene que quedar en **CERO**; el procedimiento operativo es que al
> cerrar el lote **trasladan** el alimento sobrante fuera del galpón. "Lote destino liquidado con
> alimento marcado pendiente" **no** es un caso a modelar con guardas: es una **ANOMALÍA que el sistema
> debe SEÑALAR** (el cuadre ya es el detector natural), no esconder ni compensar.»*
> **Decisión D2:** nada de guardas compensatorias; el diferimiento se **topa** al saldo real del cedente
> y el excedente **se señala**. **D2b:** modelar el diferimiento como la operación que la realidad hace.

**Cómo se cumple acá.** El tope se calcula **una vez** contra la fn y se congela en `kg_entregados`; el
residuo queda en `kg_no_diferible` y lo lee el reporte que **ya existe** (`GET /liquidados-con-alimento`,
V16.2.2, con su tab en Gestión de Inventario). La liquidación **no se bloquea** (V16.4.1): el sellado no
impide liquidar, sólo vuelve inmutable el hecho. Y el cuadre sigue siendo el **detector independiente**:
no se toca una línea.

### R3 — SIN DESTINO

> *«Si un movimiento marcado no tiene ciclo destino todavía, el alimento **DEBE VERSE** en algún lado
> para que la operación pueda marcarlo/corregirlo. **"Invisible" NUNCA es una respuesta válida.**
> Prohibido cualquier diseño donde kilos reales dejen de aparecer en toda pantalla.»*
> **D3:** la fila de ingreso **nunca** se borra. **D3b:** fail-closed hacia HEAD.

**Cómo se cumple acá.** Con los 4 guards borrados, **no existe camino de código que quite una fila de
`fechas_universo`**: R3 deja de ser una condición a vigilar y pasa a ser estructural — que es
exactamente lo que el plan original perseguía y v15 no logró. Encima, el estado `PENDIENTE` es
enumerable ⇒ la bandeja de Fase C convierte «se ve en la grilla del cedente» en «la operación lo
encuentra sin saber dónde buscar».

### Los 11 casos de §3 del plan original, mapeados a estados persistidos

| # | Caso | estado persistido |
|---|---|---|
| 1 | Convivencia (R1) | `INERTE` · motivo «comparte bodega con el ciclo destino» |
| 2 | Secuencial con destino operativo | **`VIGENTE`** |
| 3 | Destino sin seguimiento | `PENDIENTE` → materializa al cruzar el umbral |
| 4 | Sin destino | `PENDIENTE` |
| 5 | Destino liquidado/congelado | `INERTE` |
| 5b | Cedente liquidado/congelado | `INERTE` (una foto congelada no se reescribe: sin contraparte, la suma ≠ 0) |
| 6 | Sin respaldo (tope) | `VIGENTE` parcial + `kg_no_diferible`, o `INERTE` si el tope da 0 |
| 7 | Movimiento anulado | `ANULADA` (o no se crea) |
| 8 | Salida marcada | `INERTE` — la marca se restringe a **entradas dentro de la fn**; el endpoint ya lo restringe (`InventarioGestionService.cs:2732-2734`) pero la fn **no puede confiar en eso** (carga masiva y espejo escriben por otros caminos) |
| 9 | Cedente sin seguimiento | `PENDIENTE` (no hay `fecha_entrega` donde escribir) |
| 10 | `d >= destino.prim_seg` | `INERTE` — `prim_seg` puede **preceder** al encaset (lote 175: encaset 17-jul, primer seg 16-jul) |
| — | Ya visible en la apertura natural del destino (v11+v12) | `INERTE` — diferirlo lo contaría **dos veces**; es lo que mantiene la conservación en 0,00 |
| — | `d <= cedente.ult_seg` (dentro del cedente) | `INERTE` — ver §6, riesgo 2 |
| 11 | Movimiento sin galpón | no marcable (el endpoint lo rechaza, **:2739-2740**) |

**Regla de cierre (D3b):** cualquier condición no contemplada ⇒ `PENDIENTE` o `INERTE`, nunca `VIGENTE`.
Fail-closed hacia HEAD, el único estado ya validado en producción.

---

## 5. Casos de prueba

### 5.1 G0 — GATE MULTIPAÍS (obligatorio, CLAUDE.md §🛡️) — **cómo se corre y qué se compara**

Se corre **ANTES y DESPUÉS de cada fase que toque la fn** (A y B por separado; nunca las dos juntas):

```bash
# 0) línea base LIMPIA (si quedó una vieja, se descarta a propósito)
psql "postgresql://postgres:123456789@127.0.0.1:5433/sanmarinoapplocal" -c "DROP TABLE IF EXISTS _paridad_saldo_base;"

# 1) ANTES del cambio — congela (5.804+ filas: TODOS los lotes de las 2 empresas)
psql "postgresql://postgres:123456789@127.0.0.1:5433/sanmarinoapplocal" -f backend/sql/verificar_paridad_saldo_engorde.sql

# 2) aplicar la fn nueva (dotnet ef database update, o el .sql a mano en local)

# 3) DESPUÉS — el MISMO comando, sin flags
psql "postgresql://postgres:123456789@127.0.0.1:5433/sanmarinoapplocal" -f backend/sql/verificar_paridad_saldo_engorde.sql
```

**Qué se compara y con qué umbral** (el script agrupa por empresa; clave `(lote_id, fecha, seg_id)`):

| Bloque del script | Fase A (objetivo: **nadie**) | Fase B (objetivo: **ItalcolEcuador**) |
|---|---|---|
| `DIFERENCIAS POR EMPRESA` → `filas_que_desaparecen`, `filas_nuevas`, `dif_saldo_alimento`, `dif_saldo_aves`, `dif_ingreso`, `dif_consumo`, `dif_documento` | **0 en TODAS las columnas, en las DOS empresas.** Con 0 marcas es demostrable, no una esperanza | **ItalcolPanama: 0 en TODAS.** ItalcolEcuador: 0 mientras no haya entregas `VIGENTE`; con entregas inyectadas, cada fila distinta se justifica **una por una** por escrito |
| `NINGUNA fila de seguimiento puede desaparecer` | `esperadas == presentes` | idem |
| `LOTES CON MAYOR CAMBIO DE SALDO` | debe salir **vacío** | sólo lotes de Ecuador, y sólo los del probe |

> 🔴 **Panamá es el control, y no es un trámite.** Panamá tiene **0 pares secuenciales** ⇒ la feature no
> puede tocarla; y tiene **los 7 pares que conviven** ⇒ es donde vive R1. Cualquier número distinto de 0
> en Panamá **es** la regresión de R1. Simétricamente, Ecuador encadena 143 ciclos por galpón —topología
> que Panamá no tiene— y es donde la ventana de julio rompió 26 lotes y 330 filas.

**Cobertura obligatoria — las 5 fns, las 2 empresas** (`EXCEPT ALL` bidireccional, escala de referencia
del intento anterior): diaria **5.804** filas · cuadre **61→66** · aves **172** · costos **224** ·
informe semanal **898**.

**El cuadre se mira, no se espera** (CLAUDE.md):

```bash
psql ... -c "SELECT count(*) filas, count(*) FILTER (WHERE abs(descuadre_kg) > 1) descuadrados
             FROM fn_cuadre_alimento_engorde(NULL);"
```
Línea base **de hoy: 66 filas / 5 descuadrados, todos de Panamá** (⚠️ el «61 / 1» que citan varios
bloques del tracker está **caduco** — V16.6.1 y V17.0.1). Y por empresa, vía API:
`GET /api/CuadreAlimentoEngorde?soloConProblemas=true` ⇒ Ecuador **36 / 0**, Panamá **30 / 5**. **Si el
número de descuadrados sube en cualquier empresa, es regresión.**

**I7 rendimiento:** tiempo de `fn_cuadre_alimento_engorde(NULL)` (llama a la diaria 66 veces),
**≤ 1,5×** la línea base. Referencia del intento anterior con un helper mucho más caro: 1,27×.

### 5.2 G1 — A/B con la marca PRENDIDA, sobre movimientos REALES (`backend/sql/verificar_entrega_ciclo_engorde.sql`)

Censo, **no muestra**: todos los galpones con movimientos de alimento de **las dos empresas**. Por cada
movimiento candidato: `SAVEPOINT` → marcar (histórico **y** espejo) → materializar el hecho → recalcular
las 5 fns para **todos** los lotes del galpón → registrar deltas → `ROLLBACK TO`. Todo dentro de una
transacción, con verificación final de **0 rastro**.

| Id | Invariante | Umbral | Fracaso que ataja |
|---|---|---|---|
| **I1** | Ninguna fila diaria queda negativa **nueva** | 0, sin aumentar en ningún galpón | Ronda 3 (6 de 59) |
| **I2** | Conservación (suma cero) por galpón vs HEAD | `0,00 kg` | kg que se evaporan o se duplican |
| **I3** | Visibilidad (R3): todo marcado aparece en ≥ 1 lote | 0 invisibles | rondas 1-2 |
| **I4** | No multiplicación: lo cuenta el mismo nº de ciclos | igualdad exacta | Ronda 1 (se veía en 4 lotes) |
| **I5** | Cuadre: no se aleja de 0 en ningún galpón | ≤ línea base (**66 / 5**) | Ronda 2 (+5.000 en 33/35) |
| **I6** | R1: en los **7** pares que conviven de Panamá, `dif_saldo` sigue en **0,00** | exacto | romper la bodega compartida |
| **I7** | Rendimiento | ≤ 1,5× | helper anidado degradando 5 consumidores |
| **I8** 🆕 | **Liquidar el CEDENTE no esconde kilos**: tras una entrega `VIGENTE`, congelar el cedente deja la apertura del destino **intacta** y Σ galpón invariante | apertura sin cambio · `descuadre_kg` sin cambio | **bloqueante 1 del NO-GO** (3.000→0 y cuadre 0→−3.000) |
| **I9** 🆕 | **Liquidar el DESTINO no duplica**: Σ galpón invariante | `0,00 kg` creados | **bloqueante 2 del NO-GO** (8.640→11.640 con el detector ciego) |
| **I10** 🆕 | Hecho **sellado** ⇒ anular/re-materializar devuelve error y **no escribe** | 0 filas tocadas | reapertura del handoff partido |
| **I11** 🆕 | Anular el movimiento origen ⇒ la entrega queda `ANULADA`, **nunca borrada**, con motivo | 0 filas huérfanas | invariante del histórico unificado |

> 🔴 **La fase de inyección es obligatoria.** En el dump local **ningún movimiento real cae hoy en la
> ventana `DIFERIDO`**: el censo del intento anterior terminó con **0 probes DIFERIDO** en 1.680
> marcados, o sea que por sí solo probaba que la marca *no rompe nada*, no que la entrega *funcione*.
> El universo de inyección son los **142 pares con hueco de Ecuador** (7.190 días de hueco), bombeando
> también `inventario_gestion_stock`, y comparando el MISMO movimiento con el booleano en `FALSE` y en
> `TRUE`.

### 5.3 Casos con veredicto escrito de antemano

Se conservan **P1..P12** del plan original (§7) con sus veredictos —P1 96/PA-67 sin seguimiento,
P2/P3 los pares que conviven, P4 la cadena 53→70→189, P5 anulados, P6 sin ciclo posterior, P7 el testigo
del −8.840, P8 salidas marcadas, P9 sin respaldo, P10 destino liquidado, P11 cruce de umbral, P12 la
regresión E1 (42/G0049/lote 132: la fila del 06-ago conserva `ingreso 7.000 / saldo 11.260 / documento`)—
y se agregan los **cuatro que nacen del NO-GO**:

| # | Caso | Veredicto esperado |
|---|---|---|
| **P13** | Entrega `VIGENTE` (43/G0055, 86 → 193) y después **congelar el CEDENTE** | apertura del destino **sigue en 3.000**; Σ galpón invariante; el cedente conserva su fila de entrega en la foto. ⛔ Si la apertura cae a 0 ⇒ NO-GO |
| **P14** | La misma entrega y después **congelar el DESTINO** | Σ galpón **invariante**. ⛔ Si aparecen kg de la nada ⇒ NO-GO |
| **P15** | Anular el movimiento origen con la entrega `VIGENTE` y un extremo ya congelado | la entrega queda `ANULADA` con motivo **y la anomalía aparece** en `GET /liquidados-con-alimento`. Nada se compensa en silencio (R2) |
| **P16** | **R1 en Panamá**: marcar un ingreso en cada uno de los **7 pares que conviven** | `dif_saldo = 0,00` entre los dos lotes del par, `EXCEPT ALL` **0 y 0** contra HEAD. Es la prueba de que los 4 guards murieron |

### 5.4 G3 — tests C# que **construyen** las topologías (anti falso-verde)

- Ubicación: `backend/tests/ZooSanMarino.Application.Tests/EntregaAlimentoCicloEngordeCalculosTests.cs`.
- **Cobertura obligatoria: los 11 casos de §3 + los 3 estados extra**, uno por uno, con su valor esperado.
- **Prueba de mutación manual y registrada**: por cada guarda nueva, comentarla, correr `dotnet test` y
  verificar que se pone **en rojo**. Una guarda cuyo test sigue verde al quitarla **no está testeada**.
  El resultado va al tracker (el intento anterior registró 12/12 y 14/14 mutantes muertos: ese es el piso).
- ⚠️ `pt_calc` **no tiene espejo C#** ⇒ los tests C# **no son** la compuerta del saldo: la compuerta del
  saldo es G1 en SQL. Los tests son la compuerta de la **atribución**.
- Además: `dotnet build` 0 errores / sin advertencias nuevas · `dotnet test` verde (piso actual **2.788+**)
  · `cd frontend && yarn build` (único warning aceptado: el de *bundle budget* preexistente).

### 5.5 G4 y G5 — quién declara GO y con qué disciplina

- **G4 — el que corrige NO declara GO.** El gate lo ejecuta y lo lee una sesión que **no** escribió el
  fix, con los números crudos de I1..I11 por escrito en el tracker. En la ronda 2 el agente que aplicó
  los fixes se autoevaluó verde y la verificación independiente encontró después los 6 galpones negativos.
- **G5 — disciplina de BD y de procesos.** Toda escritura del gate en transacción con `ROLLBACK` y
  verificación final de 0 rastro. Scripts con line endings **LF**. **Matar cualquier backend antes de
  empezar**; levantarlo sólo para el smoke final y apagarlo enseguida (`netstat` para confirmar el puerto
  libre). Si hay otra ventana de Claude Code trabajando el repo, **no** matarle el proceso: usar
  `dotnet build --artifacts-path <dir>` y correr con `PORT=5501 --contentRoot <dir del API>`
  (`ASPNETCORE_URLS` no hace nada). **Migración aplicada = binario viejo inválido**: reiniciar el backend
  en el mismo paso en que se aplica.
- **Smoke doble por empresa**: ItalcolPanama (sin efecto visible) e ItalcolEcuador (el caso nuevo).

---

## 6. Riesgos y qué NO hace este plan

### 6.1 Riesgos abiertos

1. 🔴 **Movimiento origen anulado con un extremo ya congelado.** No tiene solución limpia: la foto
   congelada no se reescribe. Se elige **anular el hecho y SEÑALAR** (R2: señalar, no esconder), lo que
   deja la foto del cedente con una entrega sin contraparte viva. **Decisión de producto a ratificar
   por escrito** antes de implementar B.
2. 🔴 **`INERTE` cuando el movimiento cae DENTRO del ciclo cedente** (`d <= cedente.ult_seg`). El gate
   anterior demostró que diferir alimento que el cedente estaba consumiendo **descuadra el ciclo activo**
   (43/G0055: 1.100 kg «de saldo» que son un fantasma contable; el cuadre pasó de 1 → 2 galpones
   descuadrados en los 17 probes). Se mantiene la guarda ⇒ **el feature queda acotado al ingreso que cae
   en el HUECO entre ciclos**, que es 142 de 143 pares secuenciales de Ecuador y el caso que el propio
   plan identifica como el real (39 de 110 encasets 2026, §9.3). **Esto contradice el veredicto escrito
   del caso P4** (esperaba `DIFERIDO`). **Producto tiene que ratificarlo.**
3. **La transición es visible y retroactiva.** Un movimiento pasa de `PENDIENTE` a `VIGENTE` el día en
   que el destino carga su primer seguimiento, y la grilla del cedente **gana** su fila de entrega. Es
   inherente al feature; lo nuevo es que ahora *se ve* (fila explícita) en vez de que los kg se evaporen.
   Requiere el hook del cruce de umbral en **los dos** services de seguimiento, o la tabla persistida
   queda vieja.
4. **Rendimiento.** La fn gana un JOIN a la tabla del hecho y el cuadre la llama 66 veces. Con 0 entregas
   y el índice parcial el costo debería ser ≈ 0; **I7 lo mide, no se asume.**
5. **`vw_seguimiento_pollo_engorde` (Power BI) no verá las entregas**: es una reimplementación set-based
   que **no invoca la fn**. Divergencia **documentada**, hoy sin impacto (0 entregas). Queda como
   seguimiento, igual que en v15.
6. **La línea base local está contaminada por otras sesiones** (V16.6.1: los 5 descuadres de Panamá
   aparecieron entre el 09 y el 17-ago sin que nadie tocara SQL). **Medir la línea base el mismo día**,
   inmediatamente antes de tocar nada.
7. **Sesiones en paralelo.** Hay otra ventana editando `HttpCurrentUser.cs`, `ActiveCompanyMiddleware.cs`,
   `Services/CompanyResolver.cs` y `Calculos/EmpresaActivaCalculos.cs`. **Este plan no toca ninguno**: la
   empresa efectiva se resuelve con `GetEffectiveCompanyIdAsync` tal como está.
8. **No se puede verificar prod desde esta máquina** (RDS en VPC privada, ECS Exec deshabilitado, IAM sin
   permisos — P.3). Por eso las migraciones de recálculo van **siempre** y son idempotentes (§3.1).
   Además: `main-produccion` lleva ~25 commits de atraso ⇒ el primer deploy que lleve esto arrastra
   mucho más que esto.

### 6.2 Qué NO hace este plan

| Área | Motivo |
|---|---|
| **`fn_cuadre_alimento_engorde` (la fórmula)** | Bajo el modelo de entrega no lo necesita (§2.4 del plan original) y tocarlo fue **exactamente** el error de la ronda 2. Debe seguir siendo el detector **independiente**: si forma parte del fix, deja de poder validarlo |
| **Rama CONGELADA** (`liquidacion_lote_engorde_congelada[_fila]`) | 90 fotos vigentes. Una liquidación congelada no se reescribe |
| **Bloquear la liquidación con alimento pendiente** | R2 = señalar, no impedir. `puedeLiquidarPorAves` queda como está (V16.4.1) |
| **Corregir datos históricos** | Los 28 lotes congelados con saldo quedan como están (V16.4.2). Los **5 descuadres de Panamá** tampoco: V17 demostró que **42.494 de 54.795 kg (78 %) son correcciones manuales de inventario** (`AjusteStock`/`EliminacionStock` espejados como `INV_OTRO`, que la fn no lee), no alimento perdido |
| **Cambiar el esquema de `para_proximo_ciclo`** | Se conserva como **INTENCIÓN**; el hecho vive aparte. Migrar 0 filas es puro riesgo sin beneficio |
| **Sincronizar `vw_seguimiento_pollo_engorde`** | Dobla la superficie del cambio (riesgo 5) |
| **`dias_alimento_previo_encaset` / ventana D4** | Es **otro** feature; mezclarlo impide leer el A/B |
| **Decidir por empresa o país** (`if (pais == X)`) | CLAUDE.md lo prohíbe. La marca es **dato por movimiento**, no un flag de tenant: **no se agrega ninguna columna a `companies`** |
| **Commits, push, deploy** | Los hace el orquestador, con la verificación post-deploy de CLAUDE.md §🚀 (ECS hace rollback silencioso y el CLI igual dice «completado») |

---

## 7. Criterios de entrada y salida por fase

| Fase | Entra cuando | Sale (GO) cuando |
|---|---|---|
| **A** | línea base G0 + cuadre medidos hoy | G0 **0 en todas las columnas, las 2 empresas** · cuadre en 66/5 · `dotnet build`+`dotnet test` verdes · la API rechaza `paraProximoCiclo: true` |
| **B** | A en GO y mergeada | **I1..I11 sin excepción** · P1..P16 con su veredicto escrito · mutación 100 % muertos · G0 con Panamá en 0 · cuadre ≤ 66/5 · leído por **otra** sesión (G4) |
| **C** | B en GO | `yarn build` limpio · el spinner de la bandeja apaga **en pantalla** (abrir y cerrar dos veces) · smoke doble por empresa |
