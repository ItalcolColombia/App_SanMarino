# Alinear el ModelSnapshot con el modelo — dos columnas que EF no sabe que ya existen

> Detectado el 2-sep-2026 corriendo `dotnet-ef migrations has-pending-model-changes`.
> Anotado como *hallazgo de paso* en `tracker_estado.md` por la sesión de la Fase C del rename neutro.

## Qué está roto

`ZooSanMarinoContextModelSnapshot.cs` **no tiene** dos propiedades que el modelo sí tiene:

| Entidad | Propiedad | Columna | Tipo en BD |
|---|---|---|---|
| `HistorialTrasladoLote` | `DateOnly? FechaTraslado` | `historial_traslado_lote.fecha_traslado` | `date` |
| `EmailQueue` | `DateTime? NextRetryAt` | `email_queue.next_retry_at` | `timestamp without time zone` |

(El `FechaTraslado`/`DateTime?` que sí aparece en el snapshot es de **otra** entidad
—`seguimiento_diario_produccion`, que tiene su propia `fecha_traslado`—, no de esta.)

**Consecuencia:** la próxima migración que alguien genere arrastra un `AddColumn` de cada una.
Las dos columnas **ya existen** en la BD ⇒ ese `AddColumn` (que EF escribe sin `IF NOT EXISTS`)
falla al aplicarse ⇒ la app arranca, EF revienta la migración y el contenedor entra en
crash-loop. Es el incidente que CLAUDE.md documenta como raíz del proyecto.

## Por qué NO hace falta DDL

Las dos columnas **llegan por migración y la migración es idempotente**:

- `20260831170000_FechaTrasladoLote` → `ADD COLUMN IF NOT EXISTS fecha_traslado date`
- `20260901100000_EmailQueueNextRetryAt` → `ADD COLUMN IF NOT EXISTS next_retry_at timestamp`

Las dos están en `main` **y en `main-produccion`**, así que o ya se aplicaron en prod, o se
aplican solas en el próximo deploy (`Database__RunMigrations=true`). No hay ambiente donde la
columna pueda faltar después de desplegar. Medir prod no cambia la decisión: agregar hoy una
migración con DDL sería redundante en el mejor caso y destructiva en el peor.

Lo que faltó en aquellas dos migraciones fue **actualizar el snapshot**: se escribieron a mano
(`migrationBuilder.Sql`) con el `.Designer.cs` clonado del anterior, que es el patrón correcto
para una seed —no cambia el modelo— pero **no** para una que agrega una propiedad.

## Enfoque

Editar `ZooSanMarinoContextModelSnapshot.cs` a mano, agregando las dos propiedades en su lugar
alfabético con el formato que EF genera. **Sin migración nueva y sin DDL.**

- `FechaTraslado` → `b.Property<DateOnly?>("FechaTraslado").HasColumnType("date").HasColumnName("fecha_traslado")`,
  entre `CreatedByUserId` y `GalponDestinoId`.
- `NextRetryAt` → `b.Property<DateTime?>("NextRetryAt").HasColumnType("timestamp with time zone").HasColumnName("next_retry_at")`,
  entre `Metadata` y `ProcessedAt`.

El tipo del snapshot tiene que ser **el que el modelo produce**, no el que hay en la BD: el
snapshot es la foto del *modelo*, y si escribo `timestamp without time zone` para que "coincida
con la base", `has-pending-model-changes` seguiría diciendo que hay cambios pendientes. La
divergencia real `timestamptz` (modelo) vs `timestamp` (BD) de `next_retry_at` se anota abajo.

## Hallazgo 🔴 encontrado al auditar: una migración que EF no ve

`20260902140000_RenombraTablasYColumnasSinPais.cs` (commit `c784453`, **ya en `main`**) se
commiteó **sin su `.Designer.cs`** y sin el atributo `[Migration(...)]` en ningún lado.
`MigrationsAssembly` descubre migraciones filtrando por
`t.GetCustomAttribute<MigrationAttribute>()?.Id != null`: **sin ese atributo la migración no
existe para EF** — no aparece en `migrations list`, no se aplica en el deploy y no queda en
`__EFMigrationsHistory`. Nadie ve un error; simplemente no pasa nada.

Y esa migración es la que renombra `item_inventario_ecuador → item_inventario` y
`guia_genetica_ecuador_header/_detalle`. El código de `main` **ya mapea a los nombres nuevos**
(`ToTable("item_inventario")`), así que si se despliega `main` sin esa migración, el inventario
y la guía genética consultan tablas que en prod todavía se llaman con el sufijo → el módulo
entero deja de funcionar.

Va en el mismo arreglo porque es el mismo defecto (una migración sin su Designer) y porque el
Designer que hay que escribir es justamente el que ancla el snapshot corregido.

## Archivos

- `backend/src/ZooSanMarino.Infrastructure/Migrations/ZooSanMarinoContextModelSnapshot.cs` (editar)
- `backend/src/ZooSanMarino.Infrastructure/Migrations/20260902140000_RenombraTablasYColumnasSinPais.Designer.cs` (crear)

## Casos de prueba

1. `migrations has-pending-model-changes` ⇒ "No changes have been made to the model since the last migration."
2. `migrations list` ⇒ `20260902140000_RenombraTablasYColumnasSinPais` aparece (antes: ausente).
3. `dotnet build` de la solución ⇒ 0 errores.
4. Diff del snapshot ⇒ exactamente 2 bloques `b.Property`, nada más.
5. Diff `snapshot` vs `Designer` nuevo ⇒ solo los 4 cambios de forma (using, atributos, nombre de
   clase, `BuildModel`→`BuildTargetModel`).

## Anotado, no arreglado acá

`email_queue.next_retry_at` está en BD como `timestamp without time zone` mientras el modelo la
declara `timestamp with time zone` (el default de Npgsql para `DateTime?`, la config no fija
tipo). Funciona —Npgsql manda `timestamptz` y Postgres castea con la zona de sesión, que es UTC—
pero es la misma clase de desalineación que ya mordió en `sesiones_activas`. Alinearla es un
cambio de comportamiento sobre la cola de correo: entrega propia.

---

# Fase 2 — las otras 3 migraciones sin Designer (pedido explícito del usuario)

En la Fase 1 las dejé anotadas y sin tocar porque hacerlas visibles con el `Up()` como estaba era
peligroso. El usuario pidió arreglarlas, así que se hacen **con la guarda puesta**: primero
idempotentes, después visibles.

## De dónde salen (medido, no supuesto)

`git log --diff-filter=AD` sobre sus `.Designer.cs`: **nunca existieron**. EF no las vio jamás. El
schema se aplicó **a mano** con scripts que además insertaban el id en `__EFMigrationsHistory` —el
anti-patrón que CLAUDE.md señala como causa raíz del peor incidente del proyecto—:

| migración | script que la aplicó a mano |
|---|---|
| `20260521100000_AddFechaAlistamientoLoteEngorde` | `backend/sql/apply_fecha_alistamiento_lote_engorde.sql` |
| `20260521110000_AddPesosRealesMovimientoEngorde` | `backend/sql/apply_pesos_reales_movimiento_engorde.sql` |
| `20260524180000_AddFarmIdErpCreateToLotePosturaBase` | `backend/sql/053_sync_produccion_traslados_prod.sql` |

Los tres ids están en el `__EFMigrationsHistory` local. En prod **no se puede medir desde acá**, y
por eso el orden importa: la idempotencia va primero, para que el caso «el id no está registrado»
sea un no-op en vez de un crash-loop.

## Qué se hace

1. **`Up()` idempotente** en las dos que usaban `AddColumn` (EF lo escribe **sin** `IF NOT EXISTS`):
   pasan a `migrationBuilder.Sql` con `ADD COLUMN IF NOT EXISTS`. La tercera ya era SQL idempotente
   y **no se toca**.
2. **`.Designer.cs`** para las tres, con `[Migration(...)]` — que es lo único que EF necesita para
   descubrirlas.
3. El `BuildTargetModel` se reconstruye **de la época**, no de hoy: se parte del Designer de la
   migración anterior y se le agregan exactamente las propiedades que introduce cada una. Clonar el
   snapshot actual sobre una migración de mayo pondría un modelo que es falso.

## Tipos: dos divergencias que quedan anotadas, no "arregladas"

- **`peso_bruto_real` / `peso_tara_real`**: el modelo dice `double?` ⇒ `double precision`, y eso crea
  la migración. El script manual las creó **`numeric(12,3)`**, y eso es lo que hay en la base local
  (medido). O sea: donde se aplicó a mano el peso **se redondea a 3 decimales** y en una base creada
  desde migraciones no. El código manda ⇒ la migración conserva `double precision`. Cambiar el tipo
  del peso en cualquiera de los dos sentidos es un cambio de comportamiento sobre datos de báscula.
- **`fecha_alistamiento`**: la base la tiene `date` y el modelo la declara `timestamp with time zone`.
  Misma familia que `next_retry_at`. Previo, funciona, no se toca.

## Los `Down()` no son revertibles, y tampoco lo eran antes

Medido en transacción con `ROLLBACK`:

- `fecha_alistamiento` → la vista `vw_liquidacion_ecuador_pollo_engorde` depende de la columna.
- `peso_tara_real` → el trigger `trg_movimiento_pollo_engorde_lote_hist` (uno de los que llenan
  `lote_registro_historico_unificado`) depende de la columna.

Los dos fallan con *«other objects depend on it»*, exactamente igual que el `DropColumn` original:
no es una regresión. **No se les pone `CASCADE`** — borraría una vista y un trigger del histórico en
silencio.

## Casos de prueba

1. Idempotencia por transacción con `ROLLBACK`, **dos corridas seguidas**: la 2ª tiene que avisar
   `NOTICE: ... already exists, skipping`.
2. Auditoría de visibilidad: **cero** clases `: Migration` sin su id en algún `[Migration(...)]`.
3. `dotnet build` 0 errores; `has-pending-model-changes` sin cambios; `migrations list` muestra las
   tres.
4. BD local intacta: `__EFMigrationsHistory` y los tipos de columna, iguales antes y después.

---

# Fase 3 — alinear el tipo de `peso_bruto_real` / `peso_tara_real`

Pedido explícito del usuario tras la Fase 2, donde la divergencia quedó anotada.

## La dirección: la BD se alinea al modelo, no al revés

`numeric(12,3)` (BD) → **`double precision`** (modelo). Tres razones, medidas:

1. **El código manda** (regla de `CLAUDE.md` §🔍). La entidad dice `double?` y la config no fija tipo.
2. **Las otras 6 columnas `peso_*` de la misma tabla ya son `double precision`** — `peso_bruto`,
   `peso_tara`, `peso_neto` y sus `_global`. Estas dos eran las únicas distintas, y por accidente: la
   migración que las crea nunca fue visible para EF y el schema salió de
   `apply_pesos_reales_movimiento_engorde.sql`, que eligió `NUMERIC(12,3)`.
3. **Alinear al revés cuesta muchísimo más y gana nada**: habría que pasar el CLR a `decimal` en la
   entidad, 4 lugares de los DTOs y 6 services, y dejaría estas dos columnas siendo las raras de su
   propia tabla.

## El redondeo a 3 decimales no se pierde — nunca vivió en la columna

Lo hace `MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea` con `Math.Round(…, 3)` sobre bruto,
tara, neto **y el residuo**. El otro camino que escribe estas columnas (`OrganizarPeso`, que copia
`PesoBruto`/`PesoTara` tal cual) tampoco depende del recorte: medido, **0 filas** de `peso_bruto` y
`peso_tara` tienen más de 3 decimales.

## Lo que obliga a tocar el trigger

`trg_movimiento_pollo_engorde_lote_hist` es `AFTER INSERT OR UPDATE OF … peso_tara_real …`. Una lista
de columnas explícita **fija** la columna: Postgres rechaza el `ALTER TYPE` con *«cannot alter type of
a column used in a trigger definition»*. Se saca y se vuelve a poner **desde `pg_get_triggerdef`** —la
versión realmente desplegada, no un literal del repo (lección de la Fase C)— y todo va en la misma
transacción, así que el histórico unificado no deja de llenarse ni un instante.

## Gate multipaís

`fn_seguimiento_diario_engorde` nombra `peso_tara_real`, así que aplica el gate de `CLAUDE.md`. Corrido
en transacción con `ROLLBACK`, para los **184** lotes del histórico: **6.789 filas, 0 diferencias fila
a fila**. Era esperable y quedó medido: la fn lee la columna de
`lote_registro_historico_unificado`, que sigue siendo `numeric(18,3)` — el recorte a 3 decimales del
histórico esta migración no lo toca.

## Casos de prueba (todos corridos)

1. `Up` dos veces: la 2ª avisa `NOTICE: … ya no son numeric` ⇒ idempotente. Igual el `Down`.
2. Tipos tras `Up`: los dos `double precision`. Tras `Down`: los dos `numeric(12,3)`.
3. Valores de los 2 pesos, fila a fila: **0 diferencias exactas** (no redondeadas) tras `Up`, y
   **0** en el round-trip `Up`→`Down`.
4. Triggers: **idénticos** a los de antes, después del `Up` y después del `Down`.
5. `fn_seguimiento_diario_engorde` ×184 lotes: 0 diferencias.
6. El modelo **no cambia** ⇒ el `ModelSnapshot` no se toca; el Designer clona el snapshot actual.
7. BD local intacta tras el `ROLLBACK`.
