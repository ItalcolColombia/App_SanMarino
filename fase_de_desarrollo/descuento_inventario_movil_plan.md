# Plan — Descuento de inventario desde la app móvil

**Estado:** EJECUTADO (22-ago-2026). F1-F7 completos y verificados — ver el detalle en
`tracker_estado.md`, bloque "F1, F2, F3 y F4", "F5", "F7" y "Plan cerrado". F6 quedó fuera de
alcance por decisión del usuario (medido sin excepciones: EC/PA no operan producción postura,
Colombia no tiene reproductora — construirlo sería superficie sin usuario).
**Fecha:** 22-ago-2026.
**Alcance:** que un seguimiento diario cargado en `zootecnicoapp` descuente stock, en los 4 módulos, sin romper el camino web.

---

## 0. El hallazgo que reordena todo el plan

Los cuatro diseños asumen que el problema es un hueco de backend. **No lo es, o no todavía.** La app móvil no manda ítems de inventario. Ninguno. En ningún módulo.

```
grep -rn "itemsHembras|itemsMachos|itemInventarioEcuadorId|catalogItemId" zootecnicoapp/lib/
→ 0 resultados
```

Y es deliberado. `zootecnicoapp/lib/core/api/seguimientos_api.dart:126-127` lo dice con todas las letras:

> `// El backend acepta el consumo directo en kg con estas claves; sin ítems de`
> `// inventario no descuenta stock, que es justo lo que esta fase quiere.`

Lo confirma el mensaje del commit `1cdabbc`: *«Tambien se elimino el editor de items dinamicos… Queda intacto en la carpeta del design system para cuando se implemente el descuento de stock.»*

Lo que la app manda hoy es un **escalar más un string libre**: `tipoAlimento` (un `AppField` de texto, `zootecnicoapp/lib/screens/seguimiento_screen.dart:397`) y `consumoKgHembras`/`consumoKgMachos` (engorde/levante), `consumoHembras`+`unidadConsumoHembras` (reproductora), `consumoH`+`unidadConsumoH` (producción).

**Consecuencia dura:** el gate de 4 condiciones (`!separa && modelo != Ninguno && hay array de ítems && ítem con id>0`) falla en la **tercera** para todo el tráfico móvil. Se pueden implementar los cuatro diseños completos y la app seguiría sin descontar un kilo.

Por eso el plan **no empieza por el backend**: empieza por medir, sigue por arreglar los defectos que el móvil va a *activar*, y recién al final enciende el interruptor. El interruptor es la app.

---

## 1. Estado real medido (BD local, `:5433`, solo SELECT)

| id | empresa | país | `maneja_alimento_por_galpon` | `maneja_inventario_por_silo` | `requiere_validacion_seguimiento_diario` |
|---|---|---|---|---|---|
| 1 | Agroavicola Sanmarino | CO | f | f | f |
| 3 | ItalcolEcuador | EC | **t** | f | f |
| 4 | Demo | CO | f | f | f |
| 5 | ItalcolPanama | PA | **t** | f | **t** |
| 6 | Santa Reyes | CO | f | **t** | f |

- `farms` con override de `maneja_alimento_por_galpon`: **0**. Hoy manda siempre la empresa.
- `inventario_gestion_stock`: **0 negativos sobre 583 filas**. Línea base limpia.
- Panamá (5) tiene doble validación ⇒ `separa = true` ⇒ **el camino directo que tocan estos cambios no corre en Panamá**. Cualquier smoke "de Panamá" sobre la rama directa prueba otra cosa.
- Santa Reyes (6) es la única por silo, y es Colombia.

**Correcciones a los diseños** (medidas, no opinadas):
- `SyncController.cs` tiene **52 líneas**, no 200. `SyncOperacionConfiguration.cs` tiene **32**. Las citas `:177-179`, `:181-200` y `:126-148` del diseño *requiere-cuadre* no existen: reanclar por símbolo antes de implementar.
- `ProduccionService` **no** inyecta `IInventarioGestionService` (grep = 0 hits). El diseño *hueco-producción* acierta.
- `RegistrarConsumoNivelGranjaAsync` **sí** fecha el movimiento (`InventarioGestionService.cs:1697`, `ResolveMovimientoCreatedAt(req.FechaMovimiento)`). El que **no** fecha es el ingreso: `:1757` hardcodea `CreatedAt = DateTimeOffset.UtcNow`. El bloqueante del escéptico de *arreglos-de-fondo* es real y está confirmado.

---

## 2. Fases

### F0 — Medición y decisiones (BLOQUEA TODO, sin código)

Nada de lo que sigue se escribe hasta cerrar esto. Son preguntas cuya respuesta cambia el diseño, no el detalle.

**F0.1 — Mediciones en PROD (no sirve la BD local).**

| # | Qué medir | Qué decide |
|---|---|---|
| a | Lotes de producción EC/PA con `galpon_id` o `nucleo_id` vacío | Cada uno pasa de «guarda sin descontar» a **400 al guardar** (`ValidacionConsumo.cs`: «Para ítem tipo alimento debe indicar Núcleo y Galpón»). Si hay alguno, se corrige el dato ANTES o el módulo queda inutilizable |
| b | `farms.maneja_alimento_por_galpon` de las granjas EC/PA | Consumo e ingreso tienen reglas de ubicación distintas: una granja en nivel-granja descuenta bien y **revienta al devolver** |
| c | Lotes reproductora en empresa Colombia | Hoy en local: **cero**. Si en prod también, F6.2 nace sin datos que la ejerciten y hay que decirlo, no marcarlo «verificado» |
| d | ¿Alguna empresa EC/PA con doble validación? | Si la hay, el fix de núcleo/galpón en `SepararAsync` deja de ser preventivo y entra en el mismo despliegue + backfill de reservas |
| e | Lotes de producción EC/PA que comparten galpón con un `lote_ave_engorde` vivo | `fn_lote_ave_engorde_id_desde_ubicacion` atribuye por (farm, núcleo, galpón) **sin fecha y sin fase**: sus kilos entran al saldo del lote de engorde |
| f | `lotes.pais_id` NULL en lotes de producción | En local 3 de 5 filas de `lote_postura_produccion` tienen `pais_id` NULL ⇒ el camino que corre de verdad es el **fallback granja→departamento→país** (`ProduccionService.cs:76-82`), y ningún diseño lo prueba |

**F0.2 — Decisiones de negocio (necesitan OK escrito, el backend no las puede tomar).**

1. **¿Ecuador/Panamá operan producción postura por esta ruta?** `ProduccionService.cs:29-33` dice que no («*Ecuador/Panamá no operan producción postura por esta ruta*»). El front web dice que sí (tiene rama `isEcuadorOrPanama`). O el comentario caducó o el requerimiento es nuevo. **Sin esta respuesta, F6.1 no se escribe.**
2. **Cuando el descuento falla, ¿qué se pierde: el día de campo o el descuento?** En offline el dato de campo es lo caro (el galponero ya se fue del galpón). Determina F3 (propagar) vs F7 (`requiere_cuadre`).
3. **¿«Resolver» un cuadre repone kilos o sólo marca visto?** Si repone, aparece una segunda fórmula para el mismo número y el diseño cambia.
4. **¿La app selecciona el ítem de un catálogo, o el `tipoAlimento` de texto libre se mantiene en paralelo?** Determina el tamaño de F5.

**Gate F0:** documento de decisiones firmado en el tracker. Sin él, `git checkout` de todo lo demás.

---

### F1 — Cálculo puro a `Application/Calculos/` (cero cambio de comportamiento)

Lo único que se puede empezar hoy sin esperar F0. Es refactor puro: mover código sin tocar resultados.

**Archivos nuevos**
- `backend/src/ZooSanMarino.Application/Calculos/ItemConsumoCalculos.cs`
  `AcumularPorOrigen(itemsHembras, itemsMachos) -> Dictionary<ItemConsumoKey, decimal>` — **movido byte a byte** desde `ProduccionService.cs:93-115` (prioridad `itemInventarioEcuadorId` > `catalogItemId`, `id<=0` se ignora, `siloId>0` entra en la clave, g→kg).
  `AgruparPorSilo(porOrigen) -> IEnumerable<(int? SiloId, Dictionary<ItemConsumoKey,decimal> PorClave)>`.
- `backend/src/ZooSanMarino.Application/Calculos/ConsumoDiffCalculos.cs`
  `Incrementos(viejos, nuevos)` y `Movimientos(viejos, nuevos)` con **orden estable por (Id, EsItemInventario, SiloId)**. Hoy ese bucle está escrito inline tres veces (levante `Crud.cs:256`, engorde EC `Crud.cs`, producción `Seguimiento.cs:637-647`) y ninguna es testeable.
- `backend/src/ZooSanMarino.Application/Calculos/MetadataItemSeguimientoCalculos.cs`
  `AMetadata(ItemSeguimientoDto) -> Dictionary<string,object?>` con `siloId` sólo si `> 0`. Sube el privado `ItemAMetadata`.
- `backend/src/ZooSanMarino.Application/Calculos/FechaMovimientoSeguimientoCalculos.cs`
  `Resolver(DateTime fechaRegistro, DateTime? capturadoAtDispositivo, DateTime ahoraServidorUtc) => fechaRegistro.Date`. Los dos parámetros extra entran **a propósito sin usarse**: son la especificación ejecutable de «el reloj del dispositivo no es autoritativo». El test fija que se ignoran.

**Decisión de diseño (resuelve un "serio" del escéptico de F6.1):** **NO se aplana `ItemConsumoKey` a `int`.** El doc-comment de `ItemConsumoKey.cs` dice que los rangos de `catalogo_items` e `item_inventario_ecuador` **se solapan** y que aplanar «*produce descuentos rechazados (o cruzados) por colisión de ids*». El argumento de que el front manda los dos ids iguales es una invariante del *cliente*, no del contrato — y `SyncPushService` deserializa payloads crudos de la cola offline de un dispositivo, que puede traer cualquier cosa. La clave tipada se conserva de punta a punta.

**Delegación:** `ProduccionService.cs:93` queda como delegador de una línea. **No** se migran todavía los call sites de engorde/levante: hacerlos delegar cambia el orden de iteración del `HashSet` y por lo tanto el orden de las filas de movimiento. Refactor aparte, con su propio testigo.

**Tests xUnit** (`backend/tests/ZooSanMarino.Application.Tests/`)
- `ItemConsumoCalculosTests.cs` — **equivalencia acotada**: construir el JSON con la forma que emite `ItemAMetadata` y afirmar `AcumularPorOrigen(items) == ParseMetadataItemsToKgPorOrigen(json)`. Casos: sólo `catalogItemId`; sólo `itemInventarioEcuadorId`; los dos (gana el de inventario); `'g'`/`'gramos'`/`'gramo'` vs `'kg'`; `id<=0` se ignora; hembras+machos del mismo ítem se suman; dos filas del mismo ítem en silos distintos **no** se colapsan.
  ⚠️ La equivalencia vale **sólo para metadata sin `itemsGenerales`**: `ParseMetadataItemsToKgPorOrigen` también acumula ese bloque y `CrearSeguimientoRequest` no lo declara. Un `[Fact]` documenta qué pasa si aparece (el metadata cuenta más kilos que el request) en vez de afirmar una igualdad falsa.
- `ConsumoDiffCalculosTests.cs` — alta consume, baja devuelve, ítem que desaparece devuelve todo, sin cambios no emite movimiento, dos claves con mismo `Id` y distinto `SiloId` no se colapsan, dos con distinto `EsItemInventario` tampoco, y el orden de salida es determinista.
- `MetadataItemSeguimientoCalculosTests.cs` — JSON serializado **string contra string** contra la proyección actual, para `SiloId` null / 0 / 7.
- `FechaMovimientoSeguimientoCalculosTests.cs` — devuelve la fecha del formulario aunque el reloj del dispositivo sea de otro día, aunque venga con el año roto, y aunque el reloj del servidor esté a 500 días; normaliza a `.Date`.
- `InventarioConsumoGateTests.cs` (existe) — verificar que sigue verde: 1→`ModeloBNivelGranja`, 2/3→`ModeloB`, null/otro→`Ninguno`.

**Gate F1**
```bash
cd backend && dotnet build && dotnet test
```
0 errores, sin advertencias nuevas. **No se corren los .sql de paridad**: esta fase no toca ninguna consulta ni ningún movimiento. Si algún testigo se moviera, el refactor no fue puro y se revierte.

**Riesgo: BAJO.** Es la fase segura y es la que habilita el gate de CI de todo lo demás.

---

### F2 — La fecha del movimiento (RIESGOSA: mueve el saldo diario)

Sin esto, cada consumo que mande el móvil queda fechado el día en que hubo señal, no el día del galpón. Medido en local hoy: **814/817** consumos de levante y **4.536/6.555** de engorde ya están fechados en un día distinto al del seguimiento (hasta 565 días). El móvil, que sincroniza en lote cuando recupera red, lo empeora estructuralmente.

**F2.0 — Resolver el empate de las 12:00 ANTES de tocar los services. Es bloqueante.**

`ResolveMovimientoCreatedAt` (`InventarioGestionService.cs:94-100`) ancla a **12:00:00Z exactas**. Y `fn_seguimiento_diario_engorde` ordena intra-día por `created_at` (verificado con `pg_get_functiondef`):

```sql
SELECT DATE(h.fecha_operacion) AS f, h.created_at AS ts, ...
SUM(delta) OVER (ORDER BY f, ts ROWS UNBOUNDED PRECEDING) AS p,
ROW_NUMBER()    OVER (ORDER BY f DESC, ts DESC)           AS rn_desc
```

Hoy el consumo cae a `UtcNow` (más tarde que un ingreso de las 12:00) y el orden es determinista. Al fechar el consumo, **ingreso y consumo del mismo día empatan en 12:00:00.000Z y el orden pasa a ser arbitrario** ⇒ el saldo corriente puede cerrar el día en rojo. Eso es exactamente `filas_negativas`, la señal que CLAUDE.md separa de `descuadre_kg`.

*(El empate ya existe hoy para la carga masiva de engorde, que sí fecha: `AlimentoEngorde.cs:391`. F2 lo vuelve la norma en vez de la excepción.)*

**Resolución elegida:** anclas distintas y deterministas — el ingreso se queda en **12:00Z**, el consumo pasa a **18:00Z**. Es el orden físicamente correcto (primero entra el alimento, después se come) y no toca SQL compartido. Se implementa como parámetro opcional de ancla en `ResolveMovimientoCreatedAt` con default 12, de modo que ningún llamador actual cambia. La lógica de las anclas baja a `FechaMovimientoSeguimientoCalculos` con su test.

**Alternativa si el equipo prefiere no mover la hora:** desempatar en la fn por `movement_type`/`id`. Es más caro (toca cálculo compartido multipaís, con todo su gate) y no lo recomiendo para esta fase.

**F2.1 — Tapar el hueco estructural de Colombia.** `IColombiaInventarioConsumoService` no tiene **dónde** poner la fecha: `AplicarConsumoAsync` / `AplicarDevolucionAsync` / `AplicarDiffAsync` no la reciben. Agregar `DateTime? fechaMovimiento = null` a los tres (aditivo, todos los llamadores compilan igual) y propagarlo en `ColombiaInventarioConsumoService`.

**F2.2 — El bloqueante confirmado.** `InventarioGestionService.cs:1757` — `CreatedAt = DateTimeOffset.UtcNow` en el movimiento `Ingreso` de nivel granja. **Va en el MISMO commit que F2.1.** Sin él, en una edición el ajuste positivo va al día del seguimiento y su devolución va a hoy: los dos lados del mismo diff en días distintos, que es peor que el estado actual.

**F2.3 — Cerrar la lista de call sites por grep, no por lectura.** Los diseños enumeran las altas y se dejan afuera las ediciones y borrados. La lista completa:

```bash
grep -rn "AplicarConsumoAsync\|AplicarDiffAsync\|AplicarDevolucionAsync\|RegistrarConsumoAsync\|RegistrarIngresoAsync" backend/src/ | grep -v "/Interfaces/"
```

Cubre, como mínimo: levante `Crud.cs` (alta/edición/borrado), engorde Ecuador `Crud.cs` (íd.), engorde `SeguimientoAvesEngordeService.Crud.cs` (íd. — **es otro service pero escribe la MISMA tabla**; dejarlo con otro criterio de fecha es «dos verdades sobre el mismo kardex»), reproductora (el **alta ya fecha bien**; la edición no), producción `Seguimiento.cs`, `ValidacionSeguimientoService.Validar.cs` (la rama `ModeloB` ya fecha, la de Colombia no puede), y `MigracionService.AlimentoPostura.cs` (la `fecha` ya está en la mano en el `foreach`; engorde ya la pasa, postura se olvidó).

**Decisión explícita, no por omisión:** la devolución **por eliminación** se fecha en el día del **borrado**, no en el del seguimiento. Es un hecho de hoy, no una corrección del pasado.

**F2.4 — Higiene.** Reemplazar los `Console.WriteLine` de reproductora por `_logger?.LogError`. A Console no lo lee nadie en ECS. No arregla nada, hace visible lo que hoy no se ve.

**Tests:** `FechaMovimientoSeguimientoCalculosTests` (F1) + los de las anclas.

**Gate F2 — el más caro del plan.**
```bash
# ANTES (congela línea base)
psql ... -f backend/sql/verificar_paridad_saldo_engorde.sql
psql ... -f backend/sql/verificar_cuadre_alimento_engorde.sql
psql ... -f backend/sql/verificar_paridad_saldo_levante.sql
psql ... -f backend/sql/verificar_paridad_seguimiento_produccion.sql
cd backend && dotnet build && dotnet test
# DESPUÉS (compara)
psql ... -f backend/sql/verificar_paridad_saldo_engorde.sql
psql ... -f backend/sql/verificar_cuadre_alimento_engorde.sql
```
- Gate multipaís: **toda empresa que no sea la objetivo, 0 en todas las columnas.**
- Leer **`filas_negativas` aparte de `descuadre_kg`**. `filas_negativas` es la columna que mide el empate de F2.0; si sube, F2.0 está mal resuelto y se revierte. No usar la receta vieja `abs(descuadre_kg)>1 OR filas_negativas>0`: mezcla kilos con días en rojo e infla el número con residuos de ~1e-11.
- Testigo propio: por origen, contar los `Consumo` cuya fecha en el `reference` difiere de `(created_at AT TIME ZONE 'UTC')::date`. **Las filas viejas no se tocan** — el fix es hacia adelante. Sólo los movimientos creados después del deploy tienen que dar 0.

**Riesgo: MEDIO-ALTO.** Cambia la fecha con que nacen los movimientos ⇒ cambia el saldo **por día** (no el total) en engorde, que es donde vive la tabla diaria y el cuadre por galpón. Se mide con los dos .sql congelados antes.

---

### F3 — Atomicidad del descuento (RIESGOSA: lo que hoy da 200 puede pasar a 400)

Hoy, en Ecuador/Panamá, el consumo se aplica **después** del `SaveChanges` del día, dentro de un `catch` que sólo loguea (`SeguimientoLoteLevanteService.Crud.cs:141`, y sus gemelos en `:330` y `:425`). Resultado: día guardado, inventario intacto, **200 OK**.

En la web eso es un faltante que alguien puede notar. **En el móvil se vuelve permanente:** `SyncPushService.AplicarUnaAsync` commitea el efecto **y** la fila de idempotencia como `aplicada`, el dispositivo saca la operación de su outbox y no reintenta nunca. El faltante queda invisible para siempre.

Peor: bajo la transacción ambiente del push, si `RegistrarConsumoAsync` falla **entre** el UPDATE de stock y el INSERT del movimiento, el catch se lo come y el push commitea igual — stock descontado **sin fila que lo explique**, que es exactamente lo que el comentario de `InventarioGestionService.cs:1578-1579` dice que no puede pasar.

**Decisión:** adoptar en `ModeloB` la forma que Colombia ya tiene al lado. Transacción **condicional** (`_ctx.Database.CurrentTransaction is null ? BeginTransactionAsync() : null` — el patrón literal de `Crud.cs:102-104`), día y descuento adentro, y **borrar el try/catch**: que la excepción suba.

**Tres precisiones que los diseños se saltan y sin las cuales esto sale mal:**

1. **En `ModeloB` hoy NO hay transacción.** El `SaveChanges` del día ya autocommiteó (levante `Crud.cs:124`, engorde EC `:175`, reproductora `:292` — en reproductora el grep de `BeginTransactionAsync|CurrentTransaction` no encuentra **nada**). Borrar el catch sin agregar la transacción condicional deja el día **guardado** y responde 400 por la ruta web. Hay que reordenar los tres `CreateAsync`, y en reproductora eso implica cuidado extra: `ent.Id` se necesita para el `reference` y para `SepararAsync`.
2. **O se aplica a las 8 rutas o a ninguna.** 4 módulos × (alta, edición). Media medida = dos semánticas de fallo sobre la misma tabla. Incluye `SeguimientoAvesEngordeService` (Panamá), que escribe la MISMA tabla que el de Ecuador.
3. **Los reintentos eternos ya están acotados** — y esto baja el riesgo de F3 de forma decisiva. El commit `5ce6fe6` («*la cola reintentaba para siempre y marcaba dias que el servidor rechazo*») puso techo de 5 intentos y hace que la marca del día muera con la fila rechazada. Sin ese commit, F3 habría convertido cada `error_interno` en un reintento infinito. **Con él, F3 es viable.**

**Delta operativo acotado:** la pre-validación de stock previa a persistir (commit `ecbdce5`, 17-ago-2026) ya rechaza el caso común (no hay stock) **antes** de guardar. Lo que queda para el catch son: la carrera de F4, ítem borrado entre validar y descontar, regla de silo, y error/timeout de BD.

**Tests:** no bajan a `Calculos` y hay que decirlo en vez de simular cobertura — la transacción y el orden validar/persistir/aplicar son EF, no lógica pura. `Application.Tests` sólo referencia `Application`. Su red es el smoke.

**Gate F3**
```bash
cd backend && dotnet build && dotnet test
psql ... -f backend/sql/verificar_paridad_saldo_engorde.sql     # antes/después, 0 en toda empresa
```
Smoke con backend local (matar `:5002` antes, apagarlo después con el puerto libre confirmado):
```bash
cd zootecnicoapp
dart run tool/smoke_backend.dart admin.ecuador@italcol.com 123456789 --modulo=engorde
dart run tool/smoke_backend.dart admin.ecuador@italcol.com 123456789 --modulo=reproductora
dart run tool/smoke_backend.dart admin.panama@italcol.com 123456789 --modulo=engorde
```
Los pasos 6-8 crean un seguimiento real y lo borran. Verificar además, a mano: forzar el fallo (ítem borrado entre validar y descontar) ⇒ **400 y CERO filas nuevas** en la tabla de seguimiento; por la ruta de sync ⇒ `rechazada`/`regla_de_negocio`, **sin** fila en `sync_operaciones`, y el día vuelve a quedar libre en el dispositivo.

**Riesgo: ALTO operativamente.** Se mide con el conteo de seguimientos con ítems que hoy **no** tienen su movimiento de consumo (línea base local: 306 en Ecuador, 1 en Panamá, todos anteriores a jun-2026 — o sea la forma del fallo antes de que existiera la pre-validación). Para filas nuevas tiene que quedar en 0.

---

### F4 — Concurrencia del descuento a nivel granja (Colombia)

`RegistrarConsumoNivelGranjaAsync` (`InventarioGestionService.cs:1658-1671`) lee la fila **rastreada**, decide y muta en memoria (`stock.Quantity -= req.Quantity`). No hay concurrency token en ningún lado del repo (`\d inventario_gestion_stock` no tiene columna de versión). EF emite `UPDATE ... SET quantity=@absoluto WHERE id=@id`: dos transacciones solapadas sobre la misma fila producen pérdida **determinista**, no un interleaving raro.

Y el stock nivel granja es **UNO por (granja, ítem), compartido por todos los lotes de la granja**. N tablets de la misma granja recuperando señal a la vez es el peor caso posible.

**Decisión:** descuento en dos etapas. (1) Leer **sin rastreo** con `BuscarStockSinRastreoAsync` y conservar **byte a byte** el throw actual y su mensaje (`Stock insuficiente para '{codigo} - {nombre}' (granja {farmId}): disponible {x:0.###}, requerido {y:0.###}.`) para el caso normal. (2) Reemplazar la mutación por `DescontarStockAtomicoAsync`, que ya existe y ya usa Ecuador/Panamá.

**Dos trampas que hay que cerrar en el mismo commit o no hacer nada:**

1. **No dejar un régimen mixto.** `RegistrarIngresoNivelGranjaAsync` (`:1712-1766`) sigue siendo read-modify-write **rastreado**. Si en la misma unidad de trabajo el ingreso toca la fila antes que el consumo, el `SaveChanges` escribe el absoluto en memoria y **pisa el descuento crudo** — el footgun que documenta `StockAtomico.cs:44-48`. Camino concreto y real: `AplicarDiffAsync` itera un `HashSet` (orden no determinista) y dos `ItemConsumoKey` distintas pueden resolver al mismo `itemBId` y por lo tanto a la misma fila. **Convertir también el ingreso a `SumarStockAtomicoAsync`**, o F4 cambia una pérdida entre transacciones por una pérdida dentro de una sola.
2. **La carga masiva de engorde también llama a este método**, gateada por el flag alimento-por-galpón y **no por el país** (`MigracionService.AlimentoEngorde.cs:390-400`, sin filtro de país). Envolver el *cuerpo* del método en `EnTransaccionAsync` daría **una transacción por ítem** y aplicación parcial donde hoy es todo-o-nada (contrato escrito en `IColombiaInventarioConsumoService.cs:20-23`). **Ir por la variante conservadora:** exigir transacción ambiente (throw si `CurrentTransaction is null`) y abrirla en los 2 llamadores de `MigracionService`, envolviendo el **bucle completo** de ítems.

**Lógica pura:** ampliar `StockAtomicoCalculos` con `MensajeStockInsuficienteNivelGranja(codigo, nombre, farmId, disponible, requerido)` devolviendo **exactamente** el literal de hoy, con test de igualdad byte a byte. Sin eso, el refactor puede cambiar el mensaje que lee el usuario sin que nadie se entere. Usarlo **también** en la rama de la carrera, no sólo en la pre-lectura, para que el reporte de la carga masiva no pierda ítem y granja.

**Tests:** `StockAtomicoCalculosTests` (existe) para el mensaje. **El test de carrera no cabe en `Application.Tests`** (sólo referencia `Application`) y hay que decirlo en vez de fingir cobertura: va como testigo de dos sesiones `psql` — `BEGIN` en ambas, consumo de 100 kg cada una sobre una fila de 150. Hoy el resultado es 50 (una se perdió); después, una de las dos falla y el saldo queda en 50 con dos movimientos, o en 50 con uno.

**Gate F4**
```bash
psql ... -f backend/sql/verificar_paridad_stock_clave_natural.sql
psql ... -c "SELECT count(*) FILTER (WHERE quantity<0) FROM inventario_gestion_stock;"   # debe seguir en 0
cd backend && dotnet build && dotnet test
```

**Riesgo: ALTO** — toca a toda empresa Colombia y a la carga masiva de engorde de todas. Es la fase que más conviene desplegar sola.

---

### F5 — La app emite ítems (EL INTERRUPTOR)

Hasta acá no cambió nada para el usuario móvil. Esta fase es la que enciende la feature.

**F5.1 — El kill switch va en el servidor, no en la tienda.**
Una app desplegada no se revierte con `git revert`: hay revisión de tienda de por medio. Por eso el emisor de ítems se gatea con una **columna tipada en `companies`**, nombrada por el comportamiento: `descuenta_inventario_desde_movil boolean NOT NULL DEFAULT false`. Viaja en el payload de sesión que la app ya lee (`companyPaises`, igual que `paisId`) y la app la respeta **fail-closed**: ausente o error ⇒ `false` ⇒ manda el escalar de hoy.

⛔ Nada de `if (empresa == 'X')` ni de derivar la decisión del país. `PerfilPais` decide por `paisId` porque el control de agua **es** una cuestión de país; esto no lo es.

Migración EF idempotente (`ADD COLUMN IF NOT EXISTS`), generada con `dotnet ef migrations add` para que actualice snapshot y Designer, y **después** se le reemplaza el cuerpo del `Up()` por el SQL con `IF NOT EXISTS` — el estilo de `20260821230000_AddLoteHuevoItems.cs`. Nunca escribir el archivo a mano.

**F5.2 — El selector de ítem.** Hoy `tipoAlimento` es un `AppField` de texto libre (`seguimiento_screen.dart:397`). Con el flag encendido pasa a ser un selector alimentado por el stock de la granja. El editor de ítems dinámicos **ya existe** en la carpeta del design system: el commit `1cdabbc` lo dejó ahí *«para cuando se implemente el descuento de stock»*.

**F5.3 — El contrato del id: la app manda `itemInventarioEcuadorId`, siempre.**
Esto no es un detalle. La reproductora **web** hace lo contrario y está mal: `modal-seguimiento-reproductora.component.ts:361` arma el dropdown con `id: item.itemInventarioEcuadorId` y las líneas `:506` y `:510` lo emiten como `catalogItemId`, dejando `itemInventarioEcuadorId` en null. Eso mete un id de `item_inventario_ecuador` en el campo del catálogo, y `ItemConsumoKey` lo marca `EsItemInventario = false` ⇒ se resuelve por la tabla equivocada. La app **no copia ese patrón**: manda el id en el campo que le corresponde.

**F5.4 — Rechazo fail-closed en el backend.** Bajo `ModeloB`, una `ItemConsumoKey` con `EsItemInventario == false` se rechaza con mensaje propio, en vez de mandar un id de `catalogo_items` a buscarse en `item_inventario_ecuador`. Test que lo cubre como **rechazo**, no como igualdad.

**F5.5 — Silo.** Ninguna vía de entrada de reproductora manda `siloId` (`grep -i silo` sobre la carpeta del feature = 0 resultados; la plantilla Excel de migraciones masivas tampoco lo tiene). Para Santa Reyes (única por silo) eso convierte «no descuenta» en «no deja guardar». **Mientras el selector de silo no exista, el flag de F5.1 no se enciende para empresas con `maneja_inventario_por_silo = true`.** Se declara, no se descubre en campo.

**Tests**
```bash
cd zootecnicoapp && dart analyze && flutter test
```
Cubrir en Dart: con el flag apagado el payload es **idéntico byte a byte** al de hoy (test de igualdad de mapa contra un literal congelado); con el flag encendido aparece el array con `itemInventarioEcuadorId` poblado y `catalogItemId` en null; `tipoAlimento` sigue viajando (el backend lo arma desde los ítems, ver `zootecnicoapp/lib/core/alimento_obligatorio.dart:36`).

**Gate F5 — el smoke real, que es el que importa:**
```bash
cd zootecnicoapp
dart run tool/smoke_backend.dart admin.ecuador@italcol.com 123456789 --modulo=engorde
dart run tool/smoke_backend.dart admin.ecuador@italcol.com 123456789 --modulo=levante
dart run tool/smoke_backend.dart admin.ecuador@italcol.com 123456789 --modulo=produccion
dart run tool/smoke_backend.dart admin.ecuador@italcol.com 123456789 --modulo=reproductora
```
Con el flag **apagado** primero: los 8 pasos verdes y **cero** movimientos de inventario nuevos. Después con el flag **encendido** en una empresa: los 8 pasos verdes y el movimiento de consumo con la fecha del día del seguimiento.

**Regla de despliegue, la más importante del plan:**
> **F5 no se despliega a producción antes que F2, F3 y F4.** Hoy el móvil no descuenta, así que los defectos de fecha, atomicidad y concurrencia son latentes. El día que F5 sale, los tres se vuelven daño real y simultáneo.

**Riesgo: MEDIO**, y es el único revertible en caliente (apagar el flag).

---

### F6 — Huecos por módulo (sólo si F0 dice que existen)

**F6.1 — Producción Ecuador/Panamá.** Depende de la decisión F0.2 #1. Si se aprueba:
- Extender el `.Select(l => new { l.GranjaId, l.PaisId })` de `ProduccionService.cs:72` a `{ GranjaId, NucleoId, GalponId, PaisId }`. Misma query, mismos filtros.
- Inyectar `IInventarioGestionService` y `ILogger` como parámetros **opcionales al final** del ctor (patrón de `SeguimientoAvesEngordeEcuadorService`), así el registro de DI no se toca.
- Rama EC/PA en alta, edición **y** borrado — las tres, con validación previa y la forma transaccional de F3. Sólo el alta dejaría el inventario a la deriva en cuanto alguien corrige kilos, que es la operación más frecuente.
- **Propagar el `SiloId` del grupo al request del descuento.** Si se valida agrupando por silo y se descuenta sin silo, se valida contra una fila de stock y se descuenta de otra: las dos mitades tienen que resolver la misma clave.
- **Los `request.GranjaId/NucleoId/GalponId` del DTO NO se usan.** Están declarados y muertos (grep = 0 usos). Tomar la ubicación del request sería dejar que el cliente elija de qué galpón se descuenta — el anti-patrón `AutoNombrePorCorrida` que CLAUDE.md prohíbe. La ubicación sale de la fila del lote.
- **Bug latente que este cambio activa:** `SepararAsync` recibe `granjaId ?? 0, null, null`. Con un lote EC/PA en `ModeloB`, validar esa reserva revienta con «Para ítem tipo alimento debe indicar Núcleo y Galpón». Pasar núcleo y galpón resueltos. Si F0.1(d) encuentra una empresa EC/PA con doble validación, entra en el mismo despliegue **más backfill** de las reservas ya escritas con NULL.
- **La devolución por borrado se acota:** sólo se devuelve si consta que se descontó (existe movimiento con `reference LIKE 'Seguimiento producción #<id>%'`). Hay **seis** escritores más de `seguimiento_produccion` que no tocan inventario, y sus filas se borran por el mismo método. Confiar en el metadata inflaría el stock.

**F6.2 — Reproductora Colombia.** Depende de F0.1(c). **Prerrequisito duro:** el bug de `catalogItemId` de F5.3 en la web. Simulado sobre las 3 empresas Colombia, el camino del catálogo no acierta ni una vez: 131 ítems rechazan el alta y **37 descuentan OTRO ítem en silencio**. Habilitar la rama Colombia de reproductora **antes** de arreglar el origen del id es peor que no habilitarla. Y decidir por escrito qué hace la **edición** con los registros ya guardados, porque el «viejo» se reconstruye desde ese metadata mal tipado.

Además: usar `ent.LoteReproductoraAveEngordeId` y no `dto.LoteId` en la rama nueva (con `dto.LoteId` un cliente podría descontar de la granja de otro lote), y no heredarlo a la rama vieja — cambiaría el comportamiento de Ecuador/Panamá.

**Gate F6**
```bash
psql ... -f backend/sql/verificar_paridad_seguimiento_produccion.sql   # 1ª congela, 2ª compara
psql ... -f backend/sql/verificar_cuadre_alimento_engorde.sql          # OBLIGATORIO aunque parezca ajeno
```
El segundo no es opcional: cada consumo nuevo dispara `trg_inventario_gestion_movimiento_lote_hist`, que atribuye la fila a un lote de engorde por `fn_lote_ave_engorde_id_desde_ubicacion(farm, nucleo, galpon)` — **sin fecha y sin mirar la fase**. Un lote de producción EC/PA que comparta galpón con un lote de engorde vivo le mete sus kilos al saldo de ese lote. Es la medición F0.1(e).

---

### F7 — `requiere_cuadre` (OPCIONAL, depende de F0.2 #2)

Sólo si la decisión de negocio es «se conserva el día de campo y el faltante queda visible». Si es «se sacrifica el día», F3 ya cierra el tema y F7 no se escribe.

Notas para cuando se escriba, que ahorran un rediseño:
- La política **no puede viajar en la excepción**: en Colombia el throw ocurre dentro de la transacción del push y el `catch` hace `RollbackAsync`. Tiene que actuar en la **decisión**, antes del throw, con un contexto scoped bidireccional (baja la política, suben los hallazgos) para que la marca y el efecto commiteen juntos.
- `sync_operaciones.estado varchar(20)` acepta `requiere_cuadre` (15) y `error_codigo varchar(40)` acepta `divergencia_stock` (17): **esas dos no se migran**. Sí hay que agregar columnas de detalle y de resolución — sin `cuadre_resuelto_at` la bandeja no se vacía nunca y en una semana nadie la mira.
- **La bandeja de servidor no es opcional.** El cliente **borra** la operación al ver `requiere_cuadre` (`clasificar-resultado-push.funcion.ts:41-43` ⇒ `'borrar'`): en cuanto la feature funciona, la evidencia desaparece del dispositivo. Y `SyncController` hoy expone **sólo** el POST de push.
- **Con doble validación (`separa = true`) esta pieza queda apagada por completo** — o sea, en Panamá no emite nunca. Decirlo, no descubrirlo.
- No nombrar la ruta con «admin»: el WAF devuelve 403 a cualquier path que la contenga.
- Gate de máquina obligatorio en el **mismo commit**: un script que falle el CI si la política se asigna fuera de `SyncPushService`. Sin él, el primer service que quiera «que le funcione» la prende por su cuenta. (Ojo: `backend/scripts/` tiene **un** verificador; los otros tres del workflow viven en `frontend/scripts`.)

---

## 3. Paralelismo

```
F0 ─────────────────────────────────────────────► (bloquea F6 y F7)
     │
     ├─ F1 (cálculo puro) ──┐
     │                      ├─► F2 (fecha) ──┐
     │                      │                ├─► DESPLIEGUE 1 (F2+F4)
     │                      └─► F4 (concurrencia) ─┘
     │                                             │
     ├─ F5.2 (UI del selector, desarrollo) ────────┼─► F3 (atomicidad) ─► DESPLIEGUE 2
     │                                             │
     └──────────────────────────────────────────► F5 (encender) ─► DESPLIEGUE 3
                                                        │
                                                        └─► F6 ─► F7
```

**En paralelo, sin conflicto:**
- **F1** con cualquier cosa: sólo agrega archivos en `Calculos/` y `tests/`.
- **F5.2** (la UI Flutter del selector) con todo el backend: son repos distintos dentro del monorepo, cero archivos compartidos. Es el trabajo de mayor plazo, conviene arrancarlo temprano y **shippearlo tarde**.
- **F2** y **F4**: tocan `InventarioGestionService.cs` los dos, pero regiones distintas (`ResolveMovimientoCreatedAt` + `:1757` vs `:1658-1766`). Si dos sesiones los toman a la vez, coordinar; si es una sola, van juntos en un despliegue.
- Las mediciones de **F0.1** con todo: son SELECT.

**NO en paralelo, secuencia obligada:**
- **F2.0 antes que F2.1-F2.4.** Resolver el empate de las 12:00 antes de tocar ningún service. Al revés se despliega una regresión de `filas_negativas` sin saberlo.
- **F2 y F4 antes que F3.** Cambiar la semántica de fallo sobre un descuento mal fechado y con pérdida por carrera es depurar dos bugs a la vez.
- **F2, F3, F4 antes que F5.** Es la regla dura del plan.
- **F5.3 (contrato del id) antes que F6.2.** Habilitar reproductora Colombia con el id en el campo equivocado descuenta el ítem incorrecto en silencio.
- **F3 antes que F7.** `requiere_cuadre` sobre un catch mudo produce marcas sin efecto: la peor combinación posible.

**Despliegues sugeridos, en horario de baja operación, con verificación post-deploy de la TaskDef real (`aws ecs describe-services` → `describe-task-definition` → comparar imagen):**
1. F1 + F2 + F4 (backend, sin cambio visible para el usuario).
2. F3 (backend, cambia códigos de respuesta en EC/PA).
3. F5 con el flag **apagado**, y encender empresa por empresa.
4. F6, F7 según F0.

---

## 4. Lo que NO entra y por qué

| Fuera de alcance | Motivo |
|---|---|
| **Backfill de los ~5.350 movimientos mal fechados** | Mueve saldos históricos y la tabla diaria de engorde hacia atrás hasta 565 días, y `verificar_cuadre_alimento_engorde.sql` lo vería como una regresión masiva. Va en su propio commit, con el cuadre congelado antes y comparado después. Y un descuadre **no se resuelve cerrando el lote: se hereda al ciclo siguiente**, porque el stock es del galpón y el saldo es del ciclo activo. F2 es **hacia adelante**. |
| **Arreglar el `catalogItemId` de la reproductora web** | Es un bug real y medido (37 ítems descontarían el ítem equivocado), pero es del front web y tiene su propio riesgo de regresión sobre datos ya guardados. Este plan sólo garantiza que **la app móvil** manda la forma correcta (F5.3) y que la rama Colombia de reproductora queda **gateada apagada** hasta que se arregle (F6.2). Ticket aparte. |
| **`ModeloA` (`IFarmInventoryConsumoService`)** | Muerto: `InventarioConsumoGate.ResolverModelo` nunca lo devuelve. No se toca ni se borra en este plan. |
| **Aves insuficientes como clase de divergencia** | No tiene emisor. El único throw por aves está en `MovimientoAvesService.Crud.cs:201`, que no es un tipo de sync. F7, si se hace, emite **sólo por stock**. |
| **Edición y borrado offline (F4.1/F4.2 del plan PWA)** | Alcance propio. `requiere_cuadre` marca la **operación**, no la entidad ni el saldo, así que no son prerrequisito. |
| **Migrar engorde/levante a `ConsumoDiffCalculos`** | Cambiaría el orden de iteración del `HashSet` y por lo tanto el orden de las filas de movimiento. Refactor aparte con su propio testigo. En este plan sólo los consume el código nuevo. |
| **Producción Ecuador/Panamá si F0.2 #1 dice que no operan** | El comentario de `ProduccionService.cs:29-33` puede seguir vigente. Construir el camino «por si acaso» agrega superficie sin usuario. |
| **Ventana de fecha sobre `FechaRegistro` de seguimientos** | Los controllers de seguimiento no llaman a `ValidarVentanaFechaRegistro`. Al fechar el kardex por `FechaRegistro`, un seguimiento viejo mueve el movimiento a esa fecha — correcto para el kardex, pero abre la puerta a mover saldos históricos sin permiso de retroactividad. Se **registra como riesgo conocido**; cambiarlo es otra decisión. |
| **Quitar el flag de F5.1 una vez estable** | Se queda. Es el único kill switch que no depende de la tienda de aplicaciones. |

---

## 5. Cómo se revierte

**Principio:** ninguna fase de este plan reescribe datos históricos, así que revertir código siempre es seguro. Lo que **no** se revierte solo son las filas creadas mientras la versión mala estuvo viva.

| Fase | Reversión | Qué queda sucio |
|---|---|---|
| **F1** | `git revert`. Refactor puro. | Nada. |
| **F2** | `git revert`. Los movimientos ya creados **conservan su fecha** (correcta); los nuevos vuelven a `UtcNow`. | Un tramo de movimientos bien fechados entre otros mal fechados. Es una mejora aislada, no corrupción. **No intentar "des-fechar" nada.** |
| **F2.0 (anclas)** | Si `filas_negativas` sube en el gate, revertir sólo el ancla y volver a 12:00 para todo. Es un parámetro con default. | Nada. |
| **F3** | `git revert` restaura el catch mudo. | Los días que entre deploy y revert fallaron **en silencio**. Se recuperan con el testigo de F3 (seguimientos con ítems sin movimiento de consumo) y se corrigen por la pantalla de inventario. |
| **F4** | `git revert` restaura el read-modify-write. **Revertir consumo e ingreso juntos** — dejar uno atómico y el otro rastreado es peor que ninguno. | Nada, si se revierten los dos. |
| **F5** | **`UPDATE companies SET descuenta_inventario_desde_movil = false`.** Efecto inmediato, sin release, sin deploy. La app vuelve a mandar el escalar de hoy. | Nada. La columna se queda; la migración no se revierte. |
| **F6** | `git revert` de la rama del módulo. | Movimientos ya aplicados: quedan. Si el galpón resultó equivocado (riesgo F0.1(e)), se corrige con un ingreso normal por la pantalla de inventario — **nunca** con un UPDATE al stock. |
| **F7** | `git revert` + dejar la migración (columnas NULL, inertes). | Filas en `requiere_cuadre` sin bandeja para verlas. Consultarlas por SQL hasta reponer la pieza. |

**Si una tarea ECS crashea al arrancar tras un deploy de este plan:** exit `139` = SIGSEGV, casi siempre EF fallando una migración antes del primer log. Verificar `__EFMigrationsHistory` contra las migraciones del código **antes** de re-deployar. ⛔ Nunca insertar el registro a mano para «saltearla».

**Antes de dar por bueno cualquier deploy:** ECS hace rollback silencioso. `make deploy-*` dice «completado» corriendo la versión vieja. Verificar siempre qué TaskDef corre y qué imagen tiene, y compararla con la que se pretendía desplegar.

---

## 6. Dónde se resuelve cada bloqueante de los escépticos

| Diseño | Bloqueante | Resolución |
|---|---|---|
| producción EC/PA | catch mudo rompe atomicidad bajo la tx del push | **F3** (transacción condicional, la excepción sube) |
| producción EC/PA | fallo parcial deja metadata con más ítems de los descontados | **F3** (metadata e inventario no pueden divergir) + **F6.1** (devolución acotada a los que consta que descontaron) |
| producción EC/PA | *(serio)* aplanar `ItemConsumoKey` cruza ids | **F1**: no se aplana. Clave tipada de punta a punta + **F5.4** rechazo fail-closed |
| producción EC/PA | *(serio)* validar por silo y descontar sin silo | **F6.1**: se propaga el `SiloId` |
| reproductora CO | el metadata manda un id de inventario dentro de `catalogItemId` | **F5.3** (la app manda bien) + **F6.2** bloqueada hasta arreglar la web. Fix de la web: **fuera de alcance**, ticket aparte |
| reproductora CO | ningún camino manda `siloId` ⇒ Santa Reyes no podría guardar | **F5.5**: el flag no se enciende para empresas por silo hasta que exista el selector |
| arreglos de fondo | `RegistrarIngresoNivelGranjaAsync:1757` ignora la fecha | **F2.2**, mismo commit que F2.1. **Confirmado por mí** |
| arreglos de fondo | régimen mixto SQL crudo + entidad rastreada pisa el ajuste | **F4**, trampa 1: se convierten los dos o ninguno |
| arreglos de fondo | empate a las 12:00 rompe el orden intra-día de la fn de engorde | **F2.0**, bloqueante previo. **Confirmado por mí** en el `prosrc` |
| requiere-cuadre | el catch mudo de EC/PA es la ruta principal | **F3** es prerrequisito de **F7** |
| requiere-cuadre | producción EC/PA no tiene rama `ModeloB` | Es el alcance de **F6.1**; F7 para producción EC/PA depende de él |

---

## 7. Riesgo por fase, de un vistazo

| Fase | Riesgo | Cómo se mide |
|---|---|---|
| F0 | — | Documento de decisiones |
| F1 | **BAJO** | `dotnet test` |
| F2 | **MEDIO-ALTO** | `verificar_cuadre_alimento_engorde.sql`, columna **`filas_negativas`** aparte de `descuadre_kg` |
| F3 | **ALTO operativo** | Conteo de seguimientos con ítems sin movimiento de consumo (base local: 306 EC + 1 PA) |
| F4 | **ALTO** | `count(*) FILTER (quantity<0)` (base: **0/583**) + testigo de dos sesiones psql |
| F5 | **MEDIO**, revertible en caliente | Smoke de los 4 módulos, con flag apagado y encendido |
| F6 | **MEDIO** | `verificar_paridad_seguimiento_produccion.sql` + `verificar_cuadre_alimento_engorde.sql` |
| F7 | **MEDIO** | Gate de máquina del emisor único |

**Ciclo de vida del backend local, en toda fase:** matar cualquier proceso en `:5002` **antes** de empezar (o el `bin/` queda bloqueado con MSB3027), levantarlo **sólo** para el smoke final, apagarlo confirmando que el puerto quedó libre. Una migración aplicada invalida cualquier binario viejo que siga corriendo.