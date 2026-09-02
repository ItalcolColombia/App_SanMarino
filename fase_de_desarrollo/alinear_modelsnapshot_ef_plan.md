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
