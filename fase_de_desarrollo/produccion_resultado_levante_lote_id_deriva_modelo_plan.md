# `produccion_resultado_levante.lote_id` — cerrar la deriva que ensucia TODA migración nueva (19-sep-2026)

## Síntoma

Cualquier `dotnet ef migrations add <Nombre>`, de cualquier sesión y sobre cualquier módulo, sale con
un `AlterColumn` que nadie pidió:

```csharp
migrationBuilder.AlterColumn<string>(name: "lote_id", table: "produccion_resultado_levante",
    type: "character varying(64)", nullable: false, oldClrType: typeof(string), oldType: "text");
```

Hoy la única defensa es que cada sesión se acuerde de borrar el hunk a mano (se hizo en
`20260920010340_AddAlcanceYNivelCreacionTickets`, ver
[tickets_crear_vs_atender_empresa_global_plan.md](tickets_crear_vs_atender_empresa_global_plan.md)).
El día que alguien no lo note, producción recibe un cambio de tipo que nadie decidió.

## Diagnóstico — quién escribe la columna y qué guarda

Medido el 19-sep-2026 sobre la copia local de producción (`sanmarinoapplocal:5433`):

| Pregunta | Respuesta medida |
|---|---|
| Tipo real en la BD | `lote_id text NOT NULL` (`character_maximum_length` = NULL) |
| Contenido | 11 filas, `max(length(lote_id))` = **2**, `min` = 1, no numéricos = **0** |
| Quién la escribe | **solo** `sp_recalcular_seguimiento_levante(l_lote_id text)` — `backend/sql/sp_recalcular_seguimiento_levante.sql:48` hace `delete … where lote_id = l_lote_id` y `:132` inserta `l_lote_id as lote_id` |
| Quién la lee | `SeguimientoLoteLevanteService.GetResultadoAsync` (EF, entidad **keyless**, solo lectura) y `backend/sql/verificar_paridad_levante_varios_registros_dia.sql` |
| Escrituras desde C# | ninguna: `ProduccionResultadoLevanteConfig` declara `HasNoKey()` y el service solo proyecta a DTO |

La tabla es **derivada**: el SP hace `DELETE` + `INSERT` completo por lote en cada llamada. No es
origen de nada y no tiene FK. Sí tiene dos índices sobre `(lote_id, fecha)` —el único
`produccion_resultado_levante_lote_id_fecha_key` y `idx_res_levante_lote_fecha`— y una PK `id` que el
modelo EF no mapea (entidad `HasNoKey()`, y el snapshot no la conoce: EF nunca intentará tocarla).
Cambiar el tipo de `lote_id` reconstruiría esos dos índices: otro motivo para no tocar la columna.

### El tipo real ya estaba decidido: `text`

No es una pregunta abierta. La migración
[`20260912130000_ProduccionResultadoLevanteLoteIdTexto`](../backend/src/ZooSanMarino.Infrastructure/Migrations/20260912130000_ProduccionResultadoLevanteLoteIdTexto.cs)
(commit `ef63823`, 12-sep-2026) ya convirtió la columna a `text` de forma idempotente y dejó el `Down`
**vacío a propósito**: «el SP escribe texto y volver a integer lo rompería». Si la columna fuera
`integer`, el `where lote_id = l_lote_id` del SP sería `integer = text` → Postgres no tiene ese
operador y el endpoint volvería al 500 que ese commit arregló.

Es además coherente con el resto del repo: `lote_id` es varchar/text y siempre numérico; el legado
tipado (`lote_id_int`) está NULL en el 100 % de producción y nadie lo escribe (memoria
`lote-id-int-legado-mata-lectores-levante`).

### Causa raíz de la deriva — no era la BD, era una faceta del modelo

`ProduccionResultadoLevanteConfig` mapea la propiedad `int LoteId` con:

```csharp
b.Property(x => x.LoteId).HasColumnName("lote_id").HasConversion<string>();
```

`HasConversion<string>()` sobre un `int` resuelve al convertidor **`NumberToStringConverter<int>`**,
cuyos *mapping hints* por defecto traen **`size: 64`**. Sin un tipo de columna explícito, ese `size`
es lo que decide el tipo de almacenamiento ⇒ el modelo calcula `character varying(64)`, mientras el
snapshot (escrito a mano en `ef63823` para que coincidiera con la BD) dice `text`. Modelo ≠ snapshot,
en un punto que ninguna migración puede cerrar: por eso reaparece en **cada** `migrations add`.

De ahí sale el `64` que a simple vista no viene de ningún lado del repo: `grep "64"` no lo encuentra
porque nadie lo escribió.

## Enfoque — alinear el modelo, no la BD

**No se toca la base.** La columna ya es lo que el sistema necesita (`text`), el único escritor
escribe texto, y `text` vs `varchar(64)` en Postgres solo se diferencia en un chequeo de largo que
aquí no aporta nada (el valor más largo medido tiene 2 caracteres). Cambiar el tipo en producción
sería DDL sin beneficio, con riesgo de truncar y contrario a la regla «ante desalineación, gana el
código actual» leída correctamente: el código actual **quiere texto**, y lo dice la migración de
septiembre.

El arreglo es una línea: declarar el tipo de almacenamiento explícito para que el `size: 64` del
convertidor deje de decidir.

```csharp
b.Property(x => x.LoteId)
    .HasColumnName("lote_id")
    .HasConversion<string>()
    .HasColumnType("text");
```

La entidad sigue siendo `int LoteId` (el service filtra `r.LoteId == loteId` con un `int`; cambiarla a
`string` sería tocar comportamiento sin necesidad).

## Archivos

| Archivo | Cambio |
|---|---|
| `backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/ProduccionResultadoLevanteConfig.cs` | `.HasColumnType("text")` + comentario con la causa (los *mapping hints* del convertidor) |
| `backend/src/ZooSanMarino.Infrastructure/Migrations/ZooSanMarinoContextModelSnapshot.cs` | forma **canónica** que escribe EF: `Property<string>` + `IsRequired()`, sigue `text` (ver «Lo que se midió») |

## BD / SQL

- **Ninguna migración nueva.** La columna ya es `text` en local y en producción desde
  `20260912130000`, que corre sola al arrancar (`Database__RunMigrations=true`).
- Si en el futuro alguien quisiera acotar el largo (no hay motivo), el gate sería
  `select max(length(lote_id)) from produccion_resultado_levante;` antes del `ALTER`, y habría que
  tocar el SP en el mismo commit.

## Reglas de negocio

Ninguna cambia. La deriva es de metadatos del modelo: no altera el SQL que EF emite para leer la
tabla (`lote_id = @p` con el parámetro ya convertido a texto en ambos casos), ni la respuesta de
`GET /api/SeguimientoLoteLevante/por-lote/{id}/resultado`.

## Casos de prueba

1. **Gate de la deriva (el que motiva el trabajo).** Antes del cambio, generar una migración
   descartable ⇒ debe aparecer el `AlterColumn` a `character varying(64)`. Después del cambio, generar
   otra ⇒ `Up`/`Down` **vacíos** y snapshot sin diferencias contra `git`. Limpieza **a mano** (`rm` de
   los dos archivos + restaurar el snapshot del respaldo), nunca `dotnet ef migrations remove`: con más
   de una migración `(Pending)` borra la equivocada (memoria
   `dotnet-ef-migrations-remove-borra-la-equivocada`).
2. **Build y tests**: `dotnet build` 0 errores / sin advertencias nuevas y `dotnet test` sin regresión.
3. **Lectura real del endpoint**: el SQL que EF genera para `where r.LoteId == loteId` sigue enviando
   el parámetro como texto contra una columna `text` (se verifica leyendo el comando generado y
   comparando filas contra la consulta directa en psql para un lote con datos).
4. **Sin efecto en la BD**: ninguna migración nueva ⇒ `__EFMigrationsHistory` no se toca.

## Lo que se midió (19–21-sep-2026)

| Paso | Resultado |
|---|---|
| Migración descartable **antes** del cambio | `AlterColumn` a `character varying(64)` (`oldType: "text"`), idéntico al reportado; el snapshot pasaba a `Property<string>` / `varchar(64)` |
| Migración descartable **después** del cambio | `Up`/`Down` **vacíos** |
| Snapshot | EF lo reescribe de `Property<int>("LoteId").HasColumnType("text")` a `Property<string>("LoteId").IsRequired().HasColumnType("text")` |
| Segunda migración descartable (idempotencia) | `Up`/`Down` **vacíos** y snapshot **byte a byte idéntico** |

**Por qué se commitea el snapshot aunque el tipo no cambia.** La línea de `ef63823` estaba escrita a
mano (`Property<int>`, sin `IsRequired`) y no es lo que EF genera para una propiedad con convertidor
—escribe el tipo del proveedor (`string`) y lo marca requerido porque el `int` de origen no admite
nulos—. Mismo tipo de columna, misma nulabilidad: **no hay DDL**. Pero si se dejaba la versión a mano,
la próxima migración de cualquier sesión volvía a tocar esas líneas del snapshot sin motivo. Con la
forma canónica, la segunda generación no cambia ni un byte.

Las migraciones descartables se limpiaron **a mano** (`rm` de los dos archivos); nunca
`dotnet ef migrations remove`.
