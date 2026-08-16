# Doble validación: bugs por empresa y validación en las 5

**Fecha:** 16ago26 · Continúa [disponible_aves_menos_reservas_plan.md](disponible_aves_menos_reservas_plan.md) (V6),
que cerró dejando abierto el punto V6.X: *el camino con el flag ON en postura nunca se ejecutó*.

---

## 1. Por qué el hueco estaba justo ahí

La matriz de la BD local explica sola dónde están los bugs:

| company_id | Empresa | flag | levante | producción | engorde | reproductora |
|---|---|---|---|---|---|---|
| 1 | Agroavicola Sanmarino | OFF | 10 | 2 | 0 | 0 |
| 3 | ItalcolEcuador | OFF | 0 | 0 | 120 | 3 |
| 4 | Demo | OFF | 5 | 2 | 0 | 0 |
| 5 | ItalcolPanama | **ON** | 0 | 0 | 65 | 120 |
| 6 | Santa Reyes | OFF | 0 | 0 | 0 | 0 |

La única empresa con el flag encendido **no tiene un solo lote de postura**. Todo lo que se probó en
producción real fue engorde y reproductora. Los caminos **LEVANTE** y **PRODUCCIÓN** con el flag ON
nunca corrieron contra datos, y ahí es donde aparecieron los defectos.

---

## 2. Hallazgos

### H1 — El `pais_id` de la reserva no es el país resuelto ⇒ al validar NO se descuenta el alimento

`InventarioConsumoGate.ResolverModelo(paisId)` devuelve `Ninguno` para `0`/`null`, y
`ValidacionSeguimientoService.AplicarAlimentoAsync` hace `continue` sobre `Ninguno`. O sea: una reserva
guardada con país sin resolver **se marca APLICADA y el registro VALIDADO sin descontar un solo kilo**.

Peor: el kg se calcula ANTES del bucle (`var total = reservas.Sum(...)`) y se devuelve igual, así que
la respuesta del endpoint **informa los kilos como si se hubieran descontado**.

Cada Crud resuelve bien el país para gatear el descuento al guardar
(`ResolverPaisIdLoteAsync(granjaId, lote.PaisId)`, que cae a `farm.DepartamentoId → departamentos.PaisId`)
pero **pasa el crudo a la separación**:

| Módulo | Sitio | Qué pasa como país | Estado |
|---|---|---|---|
| Producción | `ProduccionService.Seguimiento.cs:265` | **`null` literal** | ❌ roto siempre |
| Levante | `SeguimientoLoteLevanteService.Crud.cs:138` y `:302` | `lote.PaisId` crudo | ❌ roto si la columna es NULL |
| Engorde | `SeguimientoAvesEngordeService.Crud.cs:268` y `:503` | `lote.PaisId` crudo | ❌ ídem |
| Engorde EC | `SeguimientoAvesEngordeEcuadorService.Crud.cs:199` y `:437` | `lote.PaisId` crudo | ❌ ídem |
| Reproductora | `SeguimientoDiarioLoteReproductoraService.cs:279` y `:424` | `await ResolverPaisIdPorGranjaAsync(...)` | ✅ único correcto |

**Alcance medido en la BD:**
- **Producción:** el `null` es literal ⇒ **el 100 % de los registros de toda empresa**. Sanmarino (2 lotes)
  y Demo (2) el día que enciendan el flag.
- **Levante:** los lotes 13 (`K345A`) y 14 (`K345B`) de **Sanmarino** tienen `pais_id` NULL y su país real
  vía granja es 1 (Colombia). 2 de 10 lotes.
- **Engorde/reproductora:** los 185 lotes de Ecuador y Panamá tienen `pais_id` poblado ⇒ hoy no se
  dispara. El código sigue mal: `ResolverPaisIdLoteAsync` existe justamente porque la columna puede venir
  vacía.

Como `paises` solo tiene 3 filas y las 3 mapean a un modelo, `Ninguno` **únicamente** puede significar
«país sin resolver». No es un caso legítimo: es siempre un bug.

### H2 — `SeguimientoProduccionService` frena el descuento de aves y no separa nada

`SeguimientoProduccionService.AplicarDescuentoLppAsync:461` abre con
`if (await RequiereValidacionSeguimientoAsync(ct)) return;` — con el flag ON deja de descontar las aves.
Pero ese service **nunca llama a `SepararAsync`**: tiene `_validacion` inyectado y no lo usa.

Resultado con el flag ON: la mortalidad **no baja el saldo al guardar** y **tampoco existe reserva que
aplicar al validar**. Las bajas se evaporan.

El front hoy solo consume su `GET filter-data`, así que el `POST`/`PUT`/`DELETE` de
`SeguimientoProduccionController` es superficie de API viva pero no la vía de la pantalla. La contraparte
`ProduccionService` sí separa. Severidad menor por alcance, pero es el mismo defecto de fondo: un gate
sin su contrapartida.

### H3 — `ValidarAsync` / `DesvalidarAsync` no acotan por empresa

`LeerEstadoAsync` busca el registro **solo por id**, sin filtrar por compañía, aunque el mensaje de error
diga «no existe o no pertenece a la compañía». Un usuario con el permiso de validar puede validar por id
un registro de otra empresa y moverle el inventario y el saldo de aves.

Rompe la regla 3 de CLAUDE.md §🏢 (*empresa efectiva siempre por datos, fail-closed*).

### H4 — En engorde, validar con la empresa equivocada descarta el descuento en silencio

`ValidacionSeguimientoService.Validar.cs:244` pasa `_current.CompanyId` a
`RetiroAvesEngordeAplicador.SincronizarAsync`, que filtra `l.CompanyId == companyId` y **retorna sin hacer
nada** si no encuentra el lote. La reserva ya guarda el `company_id` del lote (`r.CompanyId`): usar el del
usuario hace que, combinado con H3, la validación cruzada marque el registro validado sin descontar aves.

---

## 3. Correcciones

| # | Archivo | Cambio |
|---|---|---|
| H1a | `ProduccionService.cs` | `ResolverGranjaYModeloAsync` devuelve además el `paisId` resuelto |
| H1b | `ProduccionService.Seguimiento.cs` | pasar ese `paisId` a `SeparacionSeguimientoHelper.Contexto` |
| H1c | levante / engorde / engorde EC (6 sitios) | pasar `await ResolverPaisIdLoteAsync(lote.GranjaId, lote.PaisId)` |
| H1d | `ValidacionSeguimientoService.Validar.cs` | `Ninguno` con kilos separados **lanza** en vez de `continue`; el total devuelto es el realmente aplicado |
| H2 | `SeguimientoProduccionService.cs` | separar cuando el flag está ON, igual que `ProduccionService` |
| H3 | `ValidacionSeguimientoService.cs` | `LeerEstadoAsync` acota por la empresa del lote (fail-closed) |
| H4 | `ValidacionSeguimientoService.Validar.cs` | usar `r.CompanyId` de la reserva, no `_current.CompanyId` |

**Cálculo puro + tests (gate CI):** la decisión «este país resuelve a un modelo de inventario o la
validación no puede aplicarse» pasa a `Application/Calculos` con tests xUnit que fijen: flag OFF idéntico
byte a byte, y país sin resolver ⇒ error, nunca «validado sin descontar».

---

## 4. Validación por empresa (lo que V6 dejó sin hacer)

Para cada una de las 5 empresas, con el flag **ON** y después **OFF**, sobre los módulos que esa empresa
tiene realmente:

| Empresa | Módulos a probar |
|---|---|
| Agroavicola Sanmarino | levante (incluido K345A, `pais_id` NULL) + producción |
| Demo | levante + producción |
| ItalcolPanama | engorde + reproductora (regresión: hoy funciona) |
| ItalcolEcuador | engorde + reproductora |
| Santa Reyes | sin lotes ⇒ solo se verifica que el flag no rompa nada |

Ciclo por módulo: guardar → hay reserva ACTIVA y el maestro/stock **no** se movió → validar → el maestro y
el stock **sí** se movieron por el número exacto → desvalidar → vuelve al valor previo → editar → la
reserva se reescribe → borrar → la reserva queda LIBERADA.

Cierre: flag restaurado a su valor original en las 5, base sin residuos, backend apagado y puertos libres.

---

## 5. Lo que agregó la auditoría (7 superficies + verificación adversarial, 65 agentes)

### H5 — La columna del ítem es polimórfica y tenía FK a una sola tabla → **bloqueaba a Colombia entero**

`seguimiento_reserva_alimento.item_inventario_ecuador_id` guarda un id cuya tabla la decide
`es_item_inventario` —la entidad ya lo documentaba—, pero la configuración EF le ponía FK dura a
`item_inventario_ecuador`. En la base local hay **435 `catalogo_items` y 208 no existen** como
`item_inventario_ecuador.id`: con el flag ON, guardar un seguimiento de postura en una empresa de
Colombia insertaba ese id y la FK lo rechazaba ⇒ **500**. No se vio antes porque la única empresa con
el flag encendido opera camino 2, donde los dos ids coinciden siempre.

Migración `20260816225138_QuitarFkPolimorficaReservaAlimento`, idempotente, `Down` sin reponer la FK.

### H6 — `validado` nacía en `false` con el flag APAGADO

Los Crud nunca seteaban la columna. El backfill dejó los 933 + 605 registros históricos en `true`,
pero **todo registro creado desde entonces nace en `false`**. El día que una empresa encienda el flag,
esos registros aparecen pendientes, pasan a EN RETRASO a las 24 h y **bloquean el alta de días nuevos**
de cada lote —sin tener nada que validar—. Ahora `Validado = !separa`: la columna significa
«su efecto ya se aplicó».

Y desvalidar un registro anterior al flag se niega: no tiene reservas que devolver, y marcarlo
pendiente habilitaba el doble descuento al reeditarlo.

### H7 — El saldo de aves de producción tenía TRES escritores

`lote_postura_produccion.aves_h_actual` **no es un maestro, es una caché**: `ProduccionService.Consultas`
la recalcula con `fn_seguimiento_diario_produccion` y la persiste. Y **ninguna fn del esquema mira
`validado`** (verificado: `prosrc ILIKE '%validado%'` ⇒ 0 filas). O sea que las bajas sin validar ya
están dentro del número.

Consecuencia: el disponible de traslado de producción le restaba la reserva **encima**, contando las
bajas dos veces — regresión introducida por el commit anterior (`bebac18`), que es exactamente el
doble descuento que ese commit decía estar evitando en la rama de levante. Y validar restaba una
tercera vez sobre la caché.

Se dejó **un solo dueño**: la fn. El disponible no resta la reserva y validar no mueve la caché.
Queda **documentado** que en producción la doble validación difiere el **alimento**, no el saldo de
aves: diferirlo también exige que la fn filtre por `validado`, y eso cambia el número de todas las
empresas ⇒ pide el gate de paridad multipaís, no este arreglo.

### H8 — Levante: una sola clave para dos espacios de ids

La reserva guarda `lote_ref_int = LotePosturaLevanteId ?? LoteId`, y al validar ese entero se pasaba
como **las dos** claves. En la base local los LPL 13/14 están soft-deleted mientras los `lote_id` 13/14
(K345A/B) viven: la colisión descuenta del lote equivocado, y esa consulta no filtra por empresa.
Ahora el par sale del registro, que es la fuente de verdad de a qué lote pertenece.

### Además: `main` no compilaba

El commit `bebac18` dejó `TrasladoAvesDesdeSegService.cs` usando `ReservaSeguimientoCalculos` y
`ModuloSeguimiento` sin el `using`. `dotnet build` fallaba con 10 errores. El «0 errores, 2602 tests
en verde» de esa sesión no es reproducible desde el commit.

---

## 6. Confirmado y NO corregido acá

| # | Hallazgo | Por qué no entra |
|---|---|---|
| V7.23 | El bloqueo por vencidos se evalúa **por fila**: corta la carga masiva histórica y el puente Panamá después del primer día | Necesita un gate por import, diseño propio |
| V7.24 | El guard de alimento mide solo el metadata; el puente manda los kg en `ConsumoKgHembras/Machos` ⇒ con el flag ON no importa un día | Ídem |
| V7.25 | Los traslados crean filas de producción `validado=false` sin reserva ⇒ a las 24 h bloquean el lote | Mismo patrón que H6, en otro escritor |
| V7.26 | Front: el botón Validar de producción se muestra con el flag apagado | Front, entrega aparte |
| V7.27 | El saldo de alimento y el cuadre de engorde ignoran `validado` | Tocar `fn_seguimiento_diario_engorde` exige el **gate de paridad multipaís** |

---

## 7. Estado de la validación

`dotnet build` 0 errores (1 warning preexistente ajeno) · `dotnet test` **2608 en verde** · migración
aplicada en local y FK confirmada eliminada · base sin residuos, flags en su valor original, puertos
libres.

**El smoke HTTP por empresa NO se corrió.** El clasificador de permisos bloqueó la generación del
header `X-Secret-Up` y la llamada autenticada al backend local. No se declara como hecho.
